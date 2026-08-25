using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public enum AIDifficulty { Easy, Medium, Hard }

    public class AIPlayerSystem
    {
        private readonly int playerId;
        public int PlayerId => playerId;
        private readonly GameSimulation sim;
        private readonly AIDifficulty difficulty;

        // ── Difficulty parameters ──────────────────────────────────────
        private readonly int thinkInterval;
        private readonly int maxVillagers;
        private readonly int attackThreshold;
        private readonly int retreatPercentInt;     // retreat when this % of army lost (0-100)
        private readonly int retreatCooldownTicks;
        private readonly bool useCounterUnits;
        private readonly bool useScouts;
        private readonly int defenseReactionTicks; // how quickly defense triggers

        // ── Economy state ──────────────────────────────────────────────
        private enum GamePhase { Early, Mid, Late }
        private const int MaxFarms = 16;

        // ── Opening sequence state ────────────────────────────────────
        private bool openingComplete;
        private int openingStep; // 0=build mill, 1=rally TC to wood, 2=build lumber yard, 3=done

        // ── Military state ─────────────────────────────────────────────
        private bool militaryToggle;
        private uint rngState;
        private readonly List<int> keyBuffer = new List<int>();

        // ── Combat state ───────────────────────────────────────────────
        private enum CombatState { Building, Assembling, Marching, Attacking, Retreating, Defending }
        private CombatState combatState = CombatState.Building;
        private int attackStartArmySize; // army size when attack began
        private FixedVector3 attackTargetPos;
        private int retreatCooldownEnd;
        private int defenseCooldownEnd;
        private int marchStartTick;
        private int firstMilitaryBuildingTick = -1;
        private int defenseModeStartTick;

        // ── Scouting state ─────────────────────────────────────────────
        private int scoutUnitId = -1;
        private bool scoutRequested;

        // Known enemy base positions (discovered by scouting / combat)
        private readonly Dictionary<int, FixedVector3> knownEnemyBases = new Dictionary<int, FixedVector3>();

        // ── Ally ping reactions ──────────────────────────────────────────
        // Latest unprocessed ping ID watermark (RecentPings.Tick); we only react to entries past this.
        private int lastProcessedPingTick = -1;
        private FixedVector3 pingAttackTarget;
        private int pingAttackUntilTick = -1;
        private FixedVector3 pingDefendTarget;
        private int pingDefendUntilTick = -1;
        private const int PingDirectiveDurationTicks = 900; // ~30s @ 30 ticks/s

        // Outbound ping rate-limit so we don't spam the team when a long attack drags on.
        private int lastEmittedHelpPingTick = -1000;
        private const int HelpPingCooldownTicks = 600; // ~20s

        // ── LLM-driven intent overrides ─────────────────────────────────
        // Set by ApplyIntent (called from GameSimulation.ProcessAiIntentCommand). All
        // identically applied on every client, so determinism holds.
        private int aggressionAttackOverride = -1;   // sentinel -1 = no override
        private int aggressionRetreatOverride = -1;  // sentinel -1 = no override
        private int aggressionOverrideUntilTick = -1;
        private int resourceWeightFoodOverride;      // delta in tenths
        private int resourceWeightWoodOverride;
        private int resourceWeightGoldOverride;
        private int resourceWeightStoneOverride;
        private int resourceOverrideUntilTick = -1;
        private int forceAgeUpTarget;                // 0 = none, 2/3 = push age
        private int forceAgeUpUntilTick = -1;

        // ── Expanded LLM control surface (build/train/rally/mix). All set by ApplyIntent
        //    and read deterministically inside the tick, so lockstep holds. ──
        private int pendingLlmBuildTick;             // retry gate for BuildStructure intents
        // Explicit commander orders: while active, the routine combat/scout logic stands down
        // and does NOT re-issue its own movement, so defend/regroup/retreat/scout actually stick.
        private int combatHoldUntilTick = -1;
        private int scoutOverrideUntilTick = -1;
        private bool prodMixActive;                  // SetProductionMix override active
        private int prodMixArchers, prodMixCavalry, prodMixInfantry; // relative weights 0..100
        private int prodMixUntilTick = -1;

        private struct TrainOrder { public int MenuType; public int Remaining; public int ExpiryTick; }
        private readonly List<TrainOrder> trainOrders = new List<TrainOrder>();
        private const int MaxTrainOrders = 8;

        public int EffectiveAttackThreshold(int currentTick)
        {
            if (currentTick < aggressionOverrideUntilTick && aggressionAttackOverride > 0)
                return aggressionAttackOverride;
            return attackThreshold;
        }

        public int EffectiveRetreatPercent(int currentTick)
        {
            if (currentTick < aggressionOverrideUntilTick && aggressionRetreatOverride > 0)
                return aggressionRetreatOverride;
            return retreatPercentInt;
        }

        // ── Unit-class filter for ping/intent-driven attacks (0 = all, 1 = archers, 2 = horsemen, 3 = spearmen)
        private int pingAttackUnitClass;
        private int pingDefendUnitClass;

        // ── Detachments: independent army sub-groups that attack-move on their own, so the
        //    teammate can split forces (e.g. "archers north, cavalry south"). Each detachment
        //    snapshots a deterministic set of unit ids; the main combat FSM ignores any unit
        //    that is currently detached (GetUndetachedCombatUnits) so it never re-merges them.
        //    All selection is integer/id-deterministic → lockstep-safe.
        private struct Detachment
        {
            public List<int> UnitIds;
            public FixedVector3 Target;
            public int UntilTick;
            public int LastIssuedTick;
        }
        private readonly List<Detachment> detachments = new List<Detachment>();
        private readonly HashSet<int> detachedUnitIds = new HashSet<int>();
        private readonly List<UnitData> detachCandidates = new List<UnitData>();
        private const int MaxDetachments = 4;
        private const int DetachmentReissueTicks = 50; // re-issue attack-move ~1.6s to stay committed

        // Autonomous splitting: let the AI peel off a raiding group on its own initiative.
        // Plain field (this class is part of the deterministic sim — same on every client).
        private bool enableAutonomousSplits = true;
        private int lastAutoSplitTick = -100000;
        private const int AutoSplitCooldownTicks = 60 * 30; // 60s

        // ── Villager orders: human/AI-commanded villager tasks (gather here, hide in the TC,
        //    repair this, build that). Mirrors the military detachment registry: reserved
        //    villagers are EXCLUDED from the auto-economy (AssignIdleVillagers) for the order's
        //    duration, so the economy brain never re-tasks a commanded villager. Deterministic:
        //    id-ordered selection, integer-only, registry lives in the lockstep sim.
        private enum VillagerTask { Gather, Protect, Build, Repair }
        private struct VillagerOrder
        {
            public VillagerTask Task;
            public List<int> UnitIds;
            public int ResourceType;       // Gather: which resource
            public FixedVector3 Target;     // Gather location / Protect fallback destination
            public int TargetBuildingId;    // Protect (garrison TC) / Repair / Build target
            public int UntilTick;
            public int LastIssuedTick;
        }
        private readonly List<VillagerOrder> villagerOrders = new List<VillagerOrder>();
        private readonly HashSet<int> reservedVillagerIds = new HashSet<int>();
        private readonly List<UnitData> villagerCandidates = new List<UnitData>();
        private const int MaxVillagerOrders = 6;
        private const int VillagerReissueTicks = 50;

        // Absolute gatherer-count override ("balance to exactly N/N/N/N"). Active while within
        // villagerTargetUntilTick; -1 entries fall back to the computed phase/age target.
        private int villagerTargetFood = -1, villagerTargetWood = -1, villagerTargetGold = -1, villagerTargetStone = -1;
        private int villagerTargetUntilTick = -1;

        // Autonomous villager care (auto-protect during raids). Same determinism rules as splits.
        private bool enableAutonomousVillagerCare = true;

        // ── Pending directives that wait on a trigger condition before applying.
        private struct PendingDirective
        {
            public int IntentKind;
            public int ParamA, ParamB, ParamC, ParamD;
            public int DurationTicks;
            public int TriggerType;
            public int TriggerMagnitude;
            public int EnqueuedTick;
            public int IssuerPlayerId;
        }
        private readonly List<PendingDirective> pendingDirectives = new List<PendingDirective>();
        private const int MaxPendingDirectives = 8;
        private const int PendingDirectiveTtlTicks = 30 * 60 * 30; // 30 min hard cap

        // Track recent enemy aggression so on_enemy_attack triggers can fire.
        private int lastEnemyAttackOnMeTick = -100000;

        // Public state accessors used by the LLM prompt builder (read-only, non-deterministic context).
        public string CombatStateName => combatState.ToString();
        public int ArmySize => cachedCombatUnits.Count;
        public int ActiveGroupCount => detachments.Count; // independent detachments currently out
        public int VillagerOrderCount => villagerOrders.Count; // commanded villager tasks active
        public int VillagerCount => cachedVillagers.Count;
        public int KnownEnemyBaseCount => knownEnemyBases.Count;
        public int CachedEnemySpearmen => cachedEnemySpearmen;
        public int CachedEnemyArchers => cachedEnemyArchers;
        public int CachedEnemyHorsemen => cachedEnemyHorsemen;
        public int LastEnemyAttackOnMeTick => lastEnemyAttackOnMeTick;

        // ── Building placement tracking ────────────────────────────────
        private int pendingHouseTick;
        private int pendingBarracksTick;
        private int pendingArcheryRangeTick;
        private int pendingStablesTick;
        private int pendingMillTick;
        private int pendingLumberYardTick;
        private int pendingMineTick;
        private int pendingFarmTick;
        private int pendingLandmarkTick;
        private int pendingMonasteryTick;
        private const int BuildRetryDelay = 60; // 2s cooldown

        // ── Cached base position ───────────────────────────────────────
        private int baseTileX;
        private int baseTileZ;
        private bool baseInitialized;

        // ── Builder tracking (prevents same-tick override) ────────────
        private readonly HashSet<int> assignedBuilderIds = new HashSet<int>();

        // ── Reusable lists ─────────────────────────────────────────────
        private readonly List<UnitData> tempVillagers = new List<UnitData>();
        private readonly List<UnitData> idleVillagersBuffer = new List<UnitData>();
        private readonly List<UnitData> tempCombatUnits = new List<UnitData>();
        private readonly List<UnitData> tempDefenders = new List<UnitData>();
        private readonly List<UnitData> tempFilteredUnits = new List<UnitData>();
        private readonly List<int> tempUnitIds = new List<int>();

        // ── Per-tick caches (rebuilt once at start of Tick) ──────────
        private readonly List<UnitData> cachedVillagers = new List<UnitData>();
        private readonly List<UnitData> cachedCombatUnits = new List<UnitData>();
        private readonly List<UnitData> cachedMyUnits = new List<UnitData>();
        private readonly List<BuildingData> cachedMyBuildings = new List<BuildingData>();
        private readonly HashSet<BuildingType> cachedBuildingTypes = new HashSet<BuildingType>();
        private int cachedEnemySpearmen, cachedEnemyArchers, cachedEnemyHorsemen;
        private int discoverEnemyCounter;

        // ── TC queue variation ──────────────────────────────────────
        private int tcQueueLimit;
        private int tcQueueLimitNextChange;

        public AIPlayerSystem(int playerId, GameSimulation sim, AIDifficulty difficulty = AIDifficulty.Medium)
        {
            this.playerId = playerId;
            this.sim = sim;
            this.difficulty = difficulty;

            switch (difficulty)
            {
                case AIDifficulty.Easy:
                    thinkInterval = 30;      // 1s
                    maxVillagers = 100;
                    attackThreshold = 8;
                    retreatPercentInt = 50;
                    retreatCooldownTicks = 1800; // 60s
                    useCounterUnits = false;
                    useScouts = false;
                    defenseReactionTicks = 90;   // 3s
                    break;
                case AIDifficulty.Hard:
                    thinkInterval = 10;      // 0.33s
                    maxVillagers = 100;
                    attackThreshold = 16;
                    retreatPercentInt = 25;
                    retreatCooldownTicks = 900;  // 30s
                    useCounterUnits = true;
                    useScouts = true;
                    defenseReactionTicks = 15;   // 0.5s
                    break;
                default: // Medium
                    thinkInterval = 15;      // 0.5s
                    maxVillagers = 100;
                    attackThreshold = 12;
                    retreatPercentInt = 35;
                    retreatCooldownTicks = 1350; // 45s
                    useCounterUnits = false;
                    useScouts = true;
                    defenseReactionTicks = 45;   // 1.5s
                    break;
            }

            rngState = (uint)(playerId * 31337 + 1); // xorshift needs non-zero seed
            tcQueueLimit = NextRandom(1, 12); // 1-11
            tcQueueLimitNextChange = NextRandom(300, 901); // change after 10-30s
        }

        private int NextRandom(int maxExclusive)
        {
            rngState ^= rngState << 13;
            rngState ^= rngState >> 17;
            rngState ^= rngState << 5;
            return (int)((rngState & 0x7FFFFFFF) % (uint)maxExclusive);
        }

        private int NextRandom(int minInclusive, int maxExclusive)
        {
            return minInclusive + NextRandom(maxExclusive - minInclusive);
        }

        private void RefreshCaches()
        {
            cachedVillagers.Clear();
            cachedCombatUnits.Clear();
            cachedMyUnits.Clear();
            cachedEnemySpearmen = 0;
            cachedEnemyArchers = 0;
            cachedEnemyHorsemen = 0;

            var allUnits = sim.UnitRegistry.GetAllUnits();
            for (int i = 0; i < allUnits.Count; i++)
            {
                var u = allUnits[i];
                if (u.State == UnitState.Dead) continue;

                if (u.PlayerId == playerId)
                {
                    cachedMyUnits.Add(u);
                    if (u.UnitType == 0)
                        cachedVillagers.Add(u);
                    else if (u.UnitType != 4 && !u.IsHuntable)
                        cachedCombatUnits.Add(u);
                }
                else if (!sim.AreAllies(u.PlayerId, playerId) && !u.IsHuntable)
                {
                    switch (u.UnitType)
                    {
                        case 1: case 12: cachedEnemySpearmen++; break;   // Spearman + Landsknecht
                        case 2: case 10: cachedEnemyArchers++; break;    // Archer + Longbowman
                        case 3: case 11: cachedEnemyHorsemen++; break;   // Horseman + Gendarme
                    }
                }
            }

            cachedMyBuildings.Clear();
            cachedBuildingTypes.Clear();
            var allBuildings = sim.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < allBuildings.Count; i++)
            {
                var b = allBuildings[i];
                if (b.PlayerId == playerId && !b.IsDestroyed)
                {
                    cachedMyBuildings.Add(b);
                    cachedBuildingTypes.Add(b.Type);
                }
            }
        }

        public void Tick(int currentTick)
        {
            if (currentTick % thinkInterval != 0) return;

            assignedBuilderIds.Clear();
            RefreshCaches();

            if (currentTick >= tcQueueLimitNextChange)
            {
                tcQueueLimit = NextRandom(1, 12);
                tcQueueLimitNextChange = currentTick + NextRandom(300, 901);
            }

            if (!baseInitialized)
                InitializeBase();

            PruneVillagerOrders(currentTick); // keep commanded villagers on task; refresh the reservation set
            TickEconomy(currentTick);

            if (useScouts)
                TickScouting(currentTick);

            militaryToggle = !militaryToggle;
            if (militaryToggle)
                TickMilitary(currentTick);

            TickAllyPings(currentTick);
            TickDefense(currentTick);

            PruneDetachments(currentTick); // keep split-off groups committed; rebuild the exclusion set
            TickCombat(currentTick);
            TickAutonomousSplit(currentTick);
            TickPendingDirectives(currentTick);

            // Discover enemy buildings visible to our units
            DiscoverEnemyBases();
        }

        // Walks pendingDirectives and activates any whose trigger condition has fired.
        // Runs identically on every client → deterministic activation.
        private void TickPendingDirectives(int currentTick)
        {
            for (int i = pendingDirectives.Count - 1; i >= 0; i--)
            {
                var p = pendingDirectives[i];
                if (currentTick - p.EnqueuedTick > PendingDirectiveTtlTicks)
                {
                    pendingDirectives.RemoveAt(i);
                    continue;
                }
                bool fire = false;
                switch (p.TriggerType)
                {
                    case 1: // delay (TriggerMagnitude = ticks)
                        fire = currentTick >= p.EnqueuedTick + p.TriggerMagnitude;
                        break;
                    case 2: // on_age_up (TriggerMagnitude = target age)
                        fire = sim.GetPlayerAge(playerId) >= p.TriggerMagnitude;
                        break;
                    case 3: // on_army_size (TriggerMagnitude = unit count)
                        fire = cachedCombatUnits.Count >= p.TriggerMagnitude;
                        break;
                    case 4: // on_enemy_attack
                        fire = (currentTick - lastEnemyAttackOnMeTick) < 300; // within 10s
                        break;
                }
                if (fire)
                {
                    ApplyIntentImmediate(p.IntentKind, p.ParamA, p.ParamB, p.ParamC, p.ParamD,
                        p.DurationTicks, currentTick);
                    pendingDirectives.RemoveAt(i);
                }
            }
        }

        // Filter `source` combat units into `dest` keeping only those of the requested class.
        // classFilter: 0=all, 1=archers (UnitType 2|10), 2=horsemen (UnitType 3|11), 3=spearmen (UnitType 1|12)
        private static void FilterCombatUnitsByClass(List<UnitData> source, int classFilter, List<UnitData> dest)
        {
            dest.Clear();
            if (classFilter <= 0)
            {
                dest.AddRange(source);
                return;
            }
            for (int i = 0; i < source.Count; i++)
            {
                var u = source[i];
                bool keep = false;
                switch (classFilter)
                {
                    case 1: keep = u.UnitType == 2 || u.UnitType == 10; break;
                    case 2: keep = u.UnitType == 3 || u.UnitType == 11; break;
                    case 3: keep = u.UnitType == 1 || u.UnitType == 12; break;
                }
                if (keep) dest.Add(u);
            }
        }

        // ── Initialization ─────────────────────────────────────────────

        private void InitializeBase()
        {
            var positions = sim.MapData.BasePositions;
            if (positions != null && positions.Length > playerId)
            {
                baseTileX = positions[playerId].x;
                baseTileZ = positions[playerId].y;
            }
            else
            {
                var tc = GetMyBuilding(BuildingType.TownCenter);
                if (tc != null)
                {
                    baseTileX = tc.OriginTileX;
                    baseTileZ = tc.OriginTileZ;
                }
            }
            baseInitialized = true;
        }

        // ── Economy ────────────────────────────────────────────────────

        private GamePhase DetectPhase()
        {
            bool hasBarracks = HasBuilding(BuildingType.Barracks);
            bool hasArchery = HasBuilding(BuildingType.ArcheryRange);
            bool hasStables = HasBuilding(BuildingType.Stables);

            int militaryBuildingCount = 0;
            if (hasBarracks) militaryBuildingCount++;
            if (hasArchery) militaryBuildingCount++;
            if (hasStables) militaryBuildingCount++;

            if (hasStables || militaryBuildingCount >= 2) return GamePhase.Late;
            if (hasBarracks || hasArchery) return GamePhase.Mid;
            return GamePhase.Early;
        }

        private void TickEconomy(int currentTick)
        {
            // Run opening build order before normal economy
            if (!openingComplete)
            {
                TickOpening(currentTick);
                return;
            }

            var resources = sim.ResourceManager.GetPlayerResources(playerId);
            int pop = sim.GetPopulation(playerId);
            int popCap = sim.GetPopulationCap(playerId);

            // 1. House building — maintain pop headroom
            if (popCap - pop <= 3 && popCap < sim.Config.MaxPopulation)
                TryPlaceBuilding(BuildingType.House, baseTileX, baseTileZ, currentTick, ref pendingHouseTick);

            // 2. Train villagers from TC
            if (pop < popCap && GetVillagerCount() < maxVillagers)
            {
                var tc = GetMyBuilding(BuildingType.TownCenter);
                if (tc != null && !tc.IsUnderConstruction && !tc.IsDestroyed && tc.TrainingQueue.Count < tcQueueLimit)
                {
                    if (resources.Food >= sim.Config.VillagerFoodCost)
                        Issue(new TrainUnitCommand(playerId, tc.Id, 0));
                }
            }

            // 3. Assign idle villagers every think tick
            AssignIdleVillagers(currentTick);

            // 4. Drop-off buildings near resources
            TryBuildDropoffBuildings(resources, currentTick);

            // 5. Farms when berries run low
            TryBuildFarms(resources, currentTick);

            // 6. Military buildings based on economy milestones (not tick count)
            TryBuildMilitaryBuildings(resources, currentTick);

            // 7. Age up via landmarks when ready
            TryBuildLandmark(resources, currentTick);
        }

        private void TickOpening(int currentTick)
        {
            var resources = sim.ResourceManager.GetPlayerResources(playerId);
            var basePos = sim.MapData.TileToWorldFixed(baseTileX, baseTileZ);
            int pop = sim.GetPopulation(playerId);
            int popCap = sim.GetPopulationCap(playerId);

            // Always keep training villagers and building houses during opening
            if (popCap - pop <= 3 && popCap < sim.Config.MaxPopulation)
                TryPlaceBuilding(BuildingType.House, baseTileX, baseTileZ, currentTick, ref pendingHouseTick);

            if (pop < popCap && GetVillagerCount() < maxVillagers)
            {
                var tc = GetMyBuilding(BuildingType.TownCenter);
                if (tc != null && !tc.IsUnderConstruction && !tc.IsDestroyed && tc.TrainingQueue.Count < tcQueueLimit)
                {
                    if (resources.Food >= sim.Config.VillagerFoodCost)
                        Issue(new TrainUnitCommand(playerId, tc.Id, 0));
                }
            }

            switch (openingStep)
            {
                case 0: // Build mill at berries with ALL starting villagers
                {
                    var berryNode = FindNearestResourceNode(basePos, ResourceType.Food, excludeFarms: true);
                    if (berryNode != null)
                    {
                        int footW, footH;
                        GetFootprint(BuildingType.Mill, out footW, out footH);
                        var tile = FindBuildableTile(berryNode.TileX, berryNode.TileZ, footW, footH, BuildingType.Mill);
                        if (tile.x >= 0)
                        {
                            // Gather all villager IDs to send them all to build
                            GetMyVillagers(tempVillagers);
                            tempUnitIds.Clear();
                            for (int i = 0; i < tempVillagers.Count; i++)
                                tempUnitIds.Add(tempVillagers[i].Id);

                            int[] ids = tempUnitIds.Count > 0 ? tempUnitIds.ToArray() : null;
                            Issue(new PlaceBuildingCommand(playerId, BuildingType.Mill, tile.x, tile.y, ids));
                            pendingMillTick = currentTick + BuildRetryDelay;
                        }
                    }
                    openingStep = 1;
                    break;
                }

                case 1: // Set TC rally to nearest woodline
                {
                    var woodNode = FindNearestResourceNode(basePos, ResourceType.Wood);
                    if (woodNode != null)
                    {
                        var tc = GetMyBuilding(BuildingType.TownCenter);
                        if (tc != null && !tc.IsDestroyed)
                            Issue(new SetRallyPointCommand(playerId, tc.Id, woodNode.Position, woodNode.Id));
                    }
                    openingStep = 2;
                    break;
                }

                case 2: // Build lumber yard at woodline once we have wood
                {
                    if (resources.Wood >= sim.Config.LumberYardWoodCost)
                    {
                        var woodNode = FindNearestResourceNode(basePos, ResourceType.Wood);
                        if (woodNode != null)
                            TryPlaceBuilding(BuildingType.LumberYard, woodNode.TileX, woodNode.TileZ, currentTick, ref pendingLumberYardTick);
                        openingStep = 3;
                    }
                    else
                    {
                        // While waiting for wood, assign any idle villagers
                        AssignIdleVillagers(currentTick);
                    }
                    break;
                }

                case 3: // Opening complete
                    openingComplete = true;
                    break;
            }
        }

        private void AssignIdleVillagers(int currentTick)
        {
            GetMyVillagers(tempVillagers);

            int foodGatherers = 0, woodGatherers = 0, goldGatherers = 0, stoneGatherers = 0;
            idleVillagersBuffer.Clear();
            var idleVillagers = idleVillagersBuffer;

            for (int i = 0; i < tempVillagers.Count; i++)
            {
                var v = tempVillagers[i];
                if (reservedVillagerIds.Contains(v.Id)) continue; // under a manual/auto order — hands off
                if (v.State == UnitState.Idle && !assignedBuilderIds.Contains(v.Id))
                {
                    if (v.IdleTimer < Fixed32.One) continue; // wait 1s before re-tasking
                    idleVillagers.Add(v);
                }
                else if (v.State == UnitState.Gathering || v.State == UnitState.MovingToGather
                    || v.State == UnitState.MovingToDropoff || v.State == UnitState.DroppingOff)
                {
                    ResourceType resType = v.CarriedResourceType;
                    if (v.TargetResourceNodeId >= 0)
                    {
                        var node = sim.MapData.GetResourceNode(v.TargetResourceNodeId);
                        if (node != null) resType = node.Type;
                    }
                    switch (resType)
                    {
                        case ResourceType.Food: foodGatherers++; break;
                        case ResourceType.Wood: woodGatherers++; break;
                        case ResourceType.Gold: goldGatherers++; break;
                        case ResourceType.Stone: stoneGatherers++; break;
                    }
                }
            }

            // Dynamic targets based on game phase and resource urgency
            var phase = DetectPhase();
            var resources = sim.ResourceManager.GetPlayerResources(playerId);

            int targetFood, targetWood, targetGold, targetStone;
            switch (phase)
            {
                case GamePhase.Early:
                    targetFood = 10; targetWood = 6; targetGold = 2; targetStone = 0;
                    break;
                case GamePhase.Mid:
                    targetFood = 8; targetWood = 7; targetGold = 4; targetStone = 0;
                    break;
                default: // Late
                    targetFood = 8; targetWood = 6; targetGold = 4; targetStone = 2;
                    break;
            }

            // Boost gold gathering in later ages for gold-cost units and landmarks
            int age = sim.GetPlayerAge(playerId);
            if (age >= 2) { targetGold += 2; targetWood -= 1; if (targetWood < 3) targetWood = 3; }
            if (age >= 3) { targetGold += 2; targetFood -= 1; if (targetFood < 4) targetFood = 4; }

            // Dynamic adjustment: if low on wood and need to build, boost wood
            if (resources.Wood < 100)
            {
                targetWood += 3;
                targetFood -= 2;
                if (targetFood < 4) targetFood = 4;
            }

            // LLM intent override: apply per-resource weight deltas while window is active
            if (currentTick < resourceOverrideUntilTick)
            {
                targetFood += resourceWeightFoodOverride;
                targetWood += resourceWeightWoodOverride;
                targetGold += resourceWeightGoldOverride;
                targetStone += resourceWeightStoneOverride;
                if (targetFood < 0) targetFood = 0;
                if (targetWood < 0) targetWood = 0;
                if (targetGold < 0) targetGold = 0;
                if (targetStone < 0) targetStone = 0;
            }

            // Absolute gatherer-count override ("balance to exactly N/N/N/N") takes precedence.
            if (currentTick < villagerTargetUntilTick)
            {
                if (villagerTargetFood >= 0) targetFood = villagerTargetFood;
                if (villagerTargetWood >= 0) targetWood = villagerTargetWood;
                if (villagerTargetGold >= 0) targetGold = villagerTargetGold;
                if (villagerTargetStone >= 0) targetStone = villagerTargetStone;
            }

            HashSet<int> claimedFarmIds = null; // lazy init
            for (int i = 0; i < idleVillagers.Count; i++)
            {
                var v = idleVillagers[i];
                ResourceType targetType;

                if (foodGatherers < targetFood)
                {
                    targetType = ResourceType.Food;
                    foodGatherers++;
                }
                else if (woodGatherers < targetWood)
                {
                    targetType = ResourceType.Wood;
                    woodGatherers++;
                }
                else if (goldGatherers < targetGold)
                {
                    targetType = ResourceType.Gold;
                    goldGatherers++;
                }
                else if (stoneGatherers < targetStone)
                {
                    targetType = ResourceType.Stone;
                    stoneGatherers++;
                }
                else
                {
                    targetType = ResourceType.Food;
                    foodGatherers++;
                }

                var node = FindNearestResourceNode(v.SimPosition, targetType, claimedFarmIds: claimedFarmIds);
                if (node != null)
                {
                    Issue(new GatherCommand(playerId, new int[] { v.Id }, node.Id));
                    if (node.IsFarmNode)
                    {
                        if (claimedFarmIds == null) claimedFarmIds = new HashSet<int>();
                        claimedFarmIds.Add(node.Id);
                    }
                }
            }
        }

        private void TryBuildDropoffBuildings(PlayerResources resources, int currentTick)
        {
            // Mill: build near distant food sources (berries)
            if (resources.Wood >= sim.Config.MillWoodCost)
            {
                // Find food gatherers whose target is far from any food drop-off
                GetMyVillagers(tempVillagers);
                int farFoodNodeTileX = 0, farFoodNodeTileZ = 0;
                bool foundFarFood = false;
                int worstFoodDistSq = 20 * 20; // threshold: 20 tiles

                for (int i = 0; i < tempVillagers.Count; i++)
                {
                    var v = tempVillagers[i];
                    if (v.TargetResourceNodeId < 0) continue;
                    if (v.State != UnitState.Gathering && v.State != UnitState.MovingToGather) continue;

                    var node = sim.MapData.GetResourceNode(v.TargetResourceNodeId);
                    if (node == null || node.Type != ResourceType.Food || node.IsFarmNode) continue;

                    int nearestDropSq = int.MaxValue;
                    var allBldgs = sim.BuildingRegistry.GetAllBuildings();
                    for (int j = 0; j < allBldgs.Count; j++)
                    {
                        var b = allBldgs[j];
                        if (b.PlayerId != playerId || b.IsDestroyed) continue;
                        if (b.Type != BuildingType.Mill && b.Type != BuildingType.TownCenter) continue;
                        int bdx = node.TileX - b.OriginTileX;
                        int bdz = node.TileZ - b.OriginTileZ;
                        int dSq = bdx * bdx + bdz * bdz;
                        if (dSq < nearestDropSq) nearestDropSq = dSq;
                    }

                    if (nearestDropSq > worstFoodDistSq)
                    {
                        worstFoodDistSq = nearestDropSq;
                        farFoodNodeTileX = node.TileX;
                        farFoodNodeTileZ = node.TileZ;
                        foundFarFood = true;
                    }
                }

                if (foundFarFood)
                    TryPlaceBuilding(BuildingType.Mill, farFoodNodeTileX, farFoodNodeTileZ, currentTick, ref pendingMillTick);
                else if (!HasBuilding(BuildingType.Mill))
                {
                    // Fallback: build first mill near closest berries
                    var berryNode = FindNearestResourceNode(
                        sim.MapData.TileToWorldFixed(baseTileX, baseTileZ), ResourceType.Food, excludeFarms: true);
                    if (berryNode != null)
                        TryPlaceBuilding(BuildingType.Mill, berryNode.TileX, berryNode.TileZ, currentTick, ref pendingMillTick);
                }
            }

            // Lumber yard: build near distant woodlines
            if (resources.Wood >= sim.Config.LumberYardWoodCost)
            {
                // Find wood gatherers whose target node is far from any drop-off
                GetMyVillagers(tempVillagers);
                int farNodeId = -1;
                int farNodeTileX = 0, farNodeTileZ = 0;
                int worstDistSq = 25 * 25; // threshold: 25 tiles

                for (int i = 0; i < tempVillagers.Count; i++)
                {
                    var v = tempVillagers[i];
                    if (v.CarriedResourceType != ResourceType.Wood &&
                        (v.State != UnitState.Gathering && v.State != UnitState.MovingToGather)) continue;
                    if (v.TargetResourceNodeId < 0) continue;

                    var node = sim.MapData.GetResourceNode(v.TargetResourceNodeId);
                    if (node == null || node.Type != ResourceType.Wood) continue;

                    // Find distance to nearest wood drop-off (lumber yard or TC)
                    int nearestDropSq = int.MaxValue;
                    var allBuildings = sim.BuildingRegistry.GetAllBuildings();
                    for (int j = 0; j < allBuildings.Count; j++)
                    {
                        var b = allBuildings[j];
                        if (b.PlayerId != playerId || b.IsDestroyed) continue;
                        if (b.Type != BuildingType.LumberYard && b.Type != BuildingType.TownCenter) continue;
                        int bdx = node.TileX - b.OriginTileX;
                        int bdz = node.TileZ - b.OriginTileZ;
                        int dSq = bdx * bdx + bdz * bdz;
                        if (dSq < nearestDropSq) nearestDropSq = dSq;
                    }

                    if (nearestDropSq > worstDistSq)
                    {
                        worstDistSq = nearestDropSq;
                        farNodeId = node.Id;
                        farNodeTileX = node.TileX;
                        farNodeTileZ = node.TileZ;
                    }
                }

                if (farNodeId >= 0)
                    TryPlaceBuilding(BuildingType.LumberYard, farNodeTileX, farNodeTileZ, currentTick, ref pendingLumberYardTick);
                else if (!HasBuilding(BuildingType.LumberYard))
                {
                    // Fallback: build first lumber yard near closest wood
                    var woodNode = FindNearestResourceNode(
                        sim.MapData.TileToWorldFixed(baseTileX, baseTileZ), ResourceType.Wood);
                    if (woodNode != null)
                        TryPlaceBuilding(BuildingType.LumberYard, woodNode.TileX, woodNode.TileZ, currentTick, ref pendingLumberYardTick);
                }
            }

            // Mine: build near distant gold/stone
            if (resources.Wood >= sim.Config.MineWoodCost)
            {
                GetMyVillagers(tempVillagers);
                int farMineNodeTileX = 0, farMineNodeTileZ = 0;
                bool foundFarMine = false;
                int worstMineDistSq = 20 * 20;

                for (int i = 0; i < tempVillagers.Count; i++)
                {
                    var v = tempVillagers[i];
                    if (v.TargetResourceNodeId < 0) continue;
                    if (v.State != UnitState.Gathering && v.State != UnitState.MovingToGather) continue;

                    var node = sim.MapData.GetResourceNode(v.TargetResourceNodeId);
                    if (node == null || (node.Type != ResourceType.Gold && node.Type != ResourceType.Stone)) continue;

                    int nearestDropSq = int.MaxValue;
                    var allBldgs = sim.BuildingRegistry.GetAllBuildings();
                    for (int j = 0; j < allBldgs.Count; j++)
                    {
                        var b = allBldgs[j];
                        if (b.PlayerId != playerId || b.IsDestroyed) continue;
                        if (b.Type != BuildingType.Mine && b.Type != BuildingType.TownCenter) continue;
                        int bdx = node.TileX - b.OriginTileX;
                        int bdz = node.TileZ - b.OriginTileZ;
                        int dSq = bdx * bdx + bdz * bdz;
                        if (dSq < nearestDropSq) nearestDropSq = dSq;
                    }

                    if (nearestDropSq > worstMineDistSq)
                    {
                        worstMineDistSq = nearestDropSq;
                        farMineNodeTileX = node.TileX;
                        farMineNodeTileZ = node.TileZ;
                        foundFarMine = true;
                    }
                }

                if (foundFarMine)
                    TryPlaceBuilding(BuildingType.Mine, farMineNodeTileX, farMineNodeTileZ, currentTick, ref pendingMineTick);
                else if (!HasBuilding(BuildingType.Mine))
                {
                    var goldNode = FindNearestResourceNode(
                        sim.MapData.TileToWorldFixed(baseTileX, baseTileZ), ResourceType.Gold);
                    if (goldNode != null)
                        TryPlaceBuilding(BuildingType.Mine, goldNode.TileX, goldNode.TileZ, currentTick, ref pendingMineTick);
                }
            }
        }

        private void TryBuildFarms(PlayerResources resources, int currentTick)
        {
            // Count actual farms from registry
            int farmCount = 0;
            var allBuildings = sim.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < allBuildings.Count; i++)
            {
                var b = allBuildings[i];
                if (b.PlayerId == playerId && b.Type == BuildingType.Farm && !b.IsDestroyed)
                    farmCount++;
            }
            if (farmCount >= MaxFarms) return;
            if (resources.Wood < sim.Config.FarmWoodCost) return;

            // Only count berries near our base (~30 tiles)
            int berryFood = 0;
            var nodes = sim.MapData.GetAllResourceNodes();
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n.Type == ResourceType.Food && !n.IsDepleted && !n.IsFarmNode)
                {
                    int bdx = n.TileX - baseTileX;
                    int bdz = n.TileZ - baseTileZ;
                    if (bdx * bdx + bdz * bdz < 30 * 30)
                        berryFood += n.RemainingAmount;
                }
            }

            if (berryFood > 500) return;

            // Build up to 4 farms per tick to avoid villager starvation
            int footW, footH;
            GetFootprint(BuildingType.Farm, out footW, out footH);

            for (int farmIter = 0; farmIter < 4; farmIter++)
            {
                if (farmCount >= MaxFarms) break;
                if (resources.Wood < sim.Config.FarmWoodCost) break;

                // Pick the mill with fewest nearby farms for distribution
                int farmCenterX = baseTileX, farmCenterZ = baseTileZ;
                int fewestNearbyFarms = int.MaxValue;

                for (int i = 0; i < allBuildings.Count; i++)
                {
                    var b = allBuildings[i];
                    if (b.PlayerId != playerId || b.Type != BuildingType.Mill || b.IsDestroyed || b.IsUnderConstruction) continue;

                    int nearbyFarms = 0;
                    for (int j = 0; j < allBuildings.Count; j++)
                    {
                        var f = allBuildings[j];
                        if (f.PlayerId != playerId || f.Type != BuildingType.Farm || f.IsDestroyed) continue;
                        int fdx = f.OriginTileX - b.OriginTileX;
                        int fdz = f.OriginTileZ - b.OriginTileZ;
                        if (fdx * fdx + fdz * fdz < 10 * 10)
                            nearbyFarms++;
                    }

                    if (nearbyFarms < fewestNearbyFarms)
                    {
                        fewestNearbyFarms = nearbyFarms;
                        farmCenterX = b.OriginTileX;
                        farmCenterZ = b.OriginTileZ;
                    }
                }

                // Fallback to TC if no mills
                if (fewestNearbyFarms == int.MaxValue)
                {
                    var tc = GetMyBuilding(BuildingType.TownCenter);
                    if (tc != null) { farmCenterX = tc.OriginTileX; farmCenterZ = tc.OriginTileZ; }
                }

                var tile = FindBuildableTile(farmCenterX, farmCenterZ, footW, footH, BuildingType.Farm);
                if (tile.x < 0) break;

                int[] villagerIds = FindIdleVillager();
                if (villagerIds == null) break;

                Issue(new PlaceBuildingCommand(playerId, BuildingType.Farm, tile.x, tile.y, villagerIds));
                for (int i = 0; i < villagerIds.Length; i++)
                    assignedBuilderIds.Add(villagerIds[i]);

                farmCount++;
            }
        }

        // ── Military Buildings (economy-milestone-based) ───────────────

        private void TryBuildMilitaryBuildings(PlayerResources resources, int currentTick)
        {
            int vilCount = GetVillagerCount();

            // Barracks: once we have 8+ villagers and stable food/wood
            if (!HasBuilding(BuildingType.Barracks) && vilCount >= 8
                && resources.Wood >= sim.Config.BarracksWoodCost && resources.Food >= 100)
            {
                TryPlaceBuilding(BuildingType.Barracks, baseTileX + 6, baseTileZ, currentTick, ref pendingBarracksTick);
            }

            // Archery Range: once we have barracks and 12+ villagers (requires Age 2)
            if (sim.GetPlayerAge(playerId) >= 2 && HasBuilding(BuildingType.Barracks) && !HasBuilding(BuildingType.ArcheryRange)
                && vilCount >= 12 && resources.Wood >= sim.Config.ArcheryRangeWoodCost)
            {
                TryPlaceBuilding(BuildingType.ArcheryRange, baseTileX + 6, baseTileZ + 6, currentTick, ref pendingArcheryRangeTick);
            }

            // Stables: once we have archery range and 16+ villagers with gold (requires Age 2)
            if (sim.GetPlayerAge(playerId) >= 2 && HasBuilding(BuildingType.ArcheryRange) && !HasBuilding(BuildingType.Stables)
                && vilCount >= 16 && resources.Wood >= sim.Config.StablesWoodCost && resources.Gold >= 150)
            {
                TryPlaceBuilding(BuildingType.Stables, baseTileX - 6, baseTileZ + 6, currentTick, ref pendingStablesTick);
            }

            // Monastery: once we have Age 3 and 20+ villagers
            if (sim.GetPlayerAge(playerId) >= 3 && !HasBuilding(BuildingType.Monastery)
                && vilCount >= 20 && resources.Wood >= sim.Config.MonasteryWoodCost)
            {
                TryPlaceBuilding(BuildingType.Monastery, baseTileX - 6, baseTileZ, currentTick, ref pendingMonasteryTick);
            }
        }

        // ── Landmark / Age Up ─────────────────────────────────────────

        private void TryBuildLandmark(PlayerResources resources, int currentTick)
        {
            if (sim.IsPlayerAgingUp(playerId)) return;
            int currentAge = sim.GetPlayerAge(playerId);

            int targetAge = currentAge + 1;
            var civ = sim.GetPlayerCivilization(playerId);
            if (!LandmarkDefinitions.HasChoices(civ, targetAge)) return;

            int vilCount = GetVillagerCount();

            // Difficulty-based villager thresholds for aging up
            int requiredVillagers;
            switch (difficulty)
            {
                case AIDifficulty.Hard:
                    requiredVillagers = targetAge == 2 ? 10 : targetAge == 3 ? 15 : 20;
                    break;
                case AIDifficulty.Easy:
                    requiredVillagers = targetAge == 2 ? 15 : targetAge == 3 ? 22 : 28;
                    break;
                default: // Medium
                    requiredVillagers = targetAge == 2 ? 12 : targetAge == 3 ? 18 : 24;
                    break;
            }
            // LLM intent override: when player asks to push for an age, halve the bar.
            if (currentTick < forceAgeUpUntilTick && forceAgeUpTarget >= targetAge)
                requiredVillagers = Mathf.Max(4, requiredVillagers / 2);
            if (vilCount < requiredVillagers) return;

            var (choiceA, choiceB) = LandmarkDefinitions.GetChoices(civ, targetAge);
            var landmarkId = NextRandom(2) == 0 ? choiceA : choiceB;
            var def = LandmarkDefinitions.Get(landmarkId);

            if (resources.Food < def.FoodCost || resources.Gold < def.GoldCost) return;

            if (currentTick < pendingLandmarkTick) return;

            int footW = def.FootprintWidth;
            int footH = def.FootprintHeight;
            var tile = FindBuildableTile(baseTileX, baseTileZ, footW, footH, BuildingType.Landmark);
            if (tile.x < 0)
            {
                pendingLandmarkTick = currentTick + BuildRetryDelay;
                return;
            }

            // Find up to 3 villagers for faster landmark construction
            int[] villagerIds = FindMultipleVillagers(3);
            if (villagerIds == null)
            {
                pendingLandmarkTick = currentTick + BuildRetryDelay;
                return;
            }

            var cmd = new PlaceBuildingCommand(playerId, BuildingType.Landmark, tile.x, tile.y, villagerIds);
            cmd.LandmarkIdValue = (int)landmarkId;
            Issue(cmd);
            for (int i = 0; i < villagerIds.Length; i++)
                assignedBuilderIds.Add(villagerIds[i]);
            pendingLandmarkTick = currentTick + BuildRetryDelay;
        }

        private int[] FindMultipleVillagers(int count)
        {
            GetMyVillagers(tempVillagers);
            tempUnitIds.Clear();
            // First pass: idle villagers
            for (int i = 0; i < tempVillagers.Count && tempUnitIds.Count < count; i++)
            {
                if (reservedVillagerIds.Contains(tempVillagers[i].Id)) continue; // don't poach commanded villagers
                if (tempVillagers[i].State == UnitState.Idle && !assignedBuilderIds.Contains(tempVillagers[i].Id))
                    tempUnitIds.Add(tempVillagers[i].Id);
            }
            // Second pass: gathering villagers to fill remaining slots
            for (int i = 0; i < tempVillagers.Count && tempUnitIds.Count < count; i++)
            {
                if (reservedVillagerIds.Contains(tempVillagers[i].Id)) continue;
                if (tempUnitIds.Contains(tempVillagers[i].Id)) continue;
                var state = tempVillagers[i].State;
                if (state == UnitState.Gathering || state == UnitState.MovingToGather || state == UnitState.MovingToDropoff)
                    tempUnitIds.Add(tempVillagers[i].Id);
            }
            return tempUnitIds.Count > 0 ? tempUnitIds.ToArray() : null;
        }

        // ── Military Training ──────────────────────────────────────────

        private void TickMilitary(int currentTick)
        {
            SetMilitaryRallyPoints();

            var resources = sim.ResourceManager.GetPlayerResources(playerId);

            // Fulfill explicit train requests from the teammate first, then fall back to
            // the AI's own production policy (a human-set production mix, counter mix, or default).
            TickTrainOrders(resources, currentTick);

            if (prodMixActive && currentTick < prodMixUntilTick)
                TrainByMix(resources);
            else if (useCounterUnits && difficulty == AIDifficulty.Hard)
                TrainCounterUnits(resources);
            else
                TrainDefaultMix(resources);
        }

        private void SetMilitaryRallyPoints()
        {
            SetRallyIfNeeded(BuildingType.Barracks);
            SetRallyIfNeeded(BuildingType.ArcheryRange);
            SetRallyIfNeeded(BuildingType.Stables);
            SetRallyIfNeeded(BuildingType.Monastery);
        }

        private void SetRallyIfNeeded(BuildingType type)
        {
            var building = GetMyBuilding(type);
            if (building == null || building.IsDestroyed || building.IsUnderConstruction) return;
            if (building.HasRallyPoint) return;

            FixedVector3 rallyPos;
            int choice = NextRandom(3);
            if (choice == 0 && knownEnemyBases.Count > 0)
            {
                // Toward nearest known enemy — 10 tiles from our base
                FixedVector3 enemyPos = default;
                var bestDist = Fixed32.FromInt(999);
                int bestKey = -1;
                keyBuffer.Clear();
                foreach (var key in knownEnemyBases.Keys) keyBuffer.Add(key);
                keyBuffer.Sort();
                for (int ki = 0; ki < keyBuffer.Count; ki++)
                {
                    int k = keyBuffer[ki];
                    if (sim.AreAllies(k, playerId)) continue;
                    var val = knownEnemyBases[k];
                    int dx = (val.x.Raw >> Fixed32.FractionalBits) - baseTileX;
                    int dz = (val.z.Raw >> Fixed32.FractionalBits) - baseTileZ;
                    int distSq = dx * dx + dz * dz;
                    var d = Fixed32.FromInt(distSq);
                    if (d < bestDist || (d == bestDist && k < bestKey))
                    {
                        bestDist = d;
                        bestKey = k;
                        enemyPos = val;
                    }
                }
                // Move 10 tiles toward enemy from base
                int eDx = (enemyPos.x.Raw >> Fixed32.FractionalBits) - baseTileX;
                int eDz = (enemyPos.z.Raw >> Fixed32.FractionalBits) - baseTileZ;
                int dist = Mathf.Max(1, Mathf.Abs(eDx) + Mathf.Abs(eDz));
                int rallyTileX = baseTileX + eDx * 10 / dist;
                int rallyTileZ = baseTileZ + eDz * 10 / dist;
                rallyPos = sim.MapData.TileToWorldFixed(rallyTileX, rallyTileZ);
            }
            else if (choice == 1)
            {
                // Near TC
                rallyPos = sim.MapData.TileToWorldFixed(baseTileX + 2, baseTileZ + 2);
            }
            else
            {
                // Near military building cluster
                rallyPos = sim.MapData.TileToWorldFixed(baseTileX + 6, baseTileZ + 3);
            }

            Issue(new SetRallyPointCommand(playerId, building.Id, rallyPos, -1));
        }

        private void GetResolvedCosts(int baseType, out int unitType, out int food, out int wood, out int gold)
        {
            unitType = sim.ResolveCivUnitType(playerId, baseType);
            gold = 0;
            switch (unitType)
            {
                case 10: food = sim.Config.LongbowmanFoodCost; wood = sim.Config.LongbowmanWoodCost; break;
                case 11: food = sim.Config.GendarmeFoodCost; wood = sim.Config.GendarmeWoodCost; break;
                case 12: food = sim.Config.LandsknechtFoodCost; wood = sim.Config.LandsknechtWoodCost; break;
                case 3: food = sim.Config.HorsemanFoodCost; wood = sim.Config.HorsemanWoodCost; break;
                case 2: food = sim.Config.ArcherFoodCost; wood = sim.Config.ArcherWoodCost; break;
                default: food = sim.Config.SpearmanFoodCost; wood = sim.Config.SpearmanWoodCost; break;
            }
        }

        private void TrainDefaultMix(PlayerResources resources)
        {
            // Default: train from available buildings evenly
            GetResolvedCosts(1, out _, out int spFood, out int spWood, out int spGold);
            TrainFromBuilding(BuildingType.Barracks, 1, spFood, spWood, spGold, resources);
            GetResolvedCosts(2, out _, out int arFood, out int arWood, out int arGold);
            TrainFromBuilding(BuildingType.ArcheryRange, 2, arFood, arWood, arGold, resources);

            if (HasBuilding(BuildingType.Stables))
            {
                GetResolvedCosts(3, out _, out int hrFood, out int hrWood, out int hrGold);
                TrainFromBuilding(BuildingType.Stables, 3, hrFood, hrWood, hrGold, resources);
            }

            // Train advanced units when Age 3+
            if (sim.GetPlayerAge(playerId) >= 3)
            {
                TrainFromBuilding(BuildingType.Barracks, 6, sim.Config.ManAtArmsFoodCost, 0, sim.Config.ManAtArmsGoldCost, resources);
                TrainFromBuilding(BuildingType.ArcheryRange, 8, sim.Config.CrossbowmanFoodCost, 0, sim.Config.CrossbowmanGoldCost, resources);
                if (HasBuilding(BuildingType.Stables))
                    TrainFromBuilding(BuildingType.Stables, 7, sim.Config.KnightFoodCost, 0, sim.Config.KnightGoldCost, resources);
                if (HasBuilding(BuildingType.Monastery))
                    TrainFromBuilding(BuildingType.Monastery, 9, sim.Config.MonkFoodCost, 0, sim.Config.MonkGoldCost, resources);
            }
        }

        private void TrainCounterUnits(PlayerResources resources)
        {
            // Use cached enemy composition from RefreshCaches()
            int enemySpearmen = cachedEnemySpearmen;
            int enemyArchers = cachedEnemyArchers;
            int enemyHorsemen = cachedEnemyHorsemen;

            int total = enemySpearmen + enemyArchers + enemyHorsemen;
            if (total < 3)
            {
                // Not enough data — use default mix
                TrainDefaultMix(resources);
                return;
            }

            // Counter: Spearmen beat Horsemen, Archers beat Spearmen, Horsemen beat Archers
            int dominantType = (enemyHorsemen >= enemySpearmen && enemyHorsemen >= enemyArchers) ? 1   // counter horsemen with spearmen
                             : (enemySpearmen >= enemyArchers) ? 2   // counter spearmen with archers
                             : 3;  // counter archers with horsemen

            // Train the counter unit type preferentially
            GetResolvedCosts(1, out _, out int spFood, out int spWood, out int spGold);
            GetResolvedCosts(2, out _, out int arFood, out int arWood, out int arGold);
            GetResolvedCosts(3, out _, out int hrFood, out int hrWood, out int hrGold);
            bool age3 = sim.GetPlayerAge(playerId) >= 3;
            switch (dominantType)
            {
                case 1: // Train Spearmen (counter horsemen)
                    TrainFromBuilding(BuildingType.Barracks, 1, spFood, spWood, spGold, resources);
                    if (age3)
                        TrainFromBuilding(BuildingType.Barracks, 6, sim.Config.ManAtArmsFoodCost, 0, sim.Config.ManAtArmsGoldCost, resources);
                    else
                        TrainFromBuilding(BuildingType.Barracks, 1, spFood, spWood, spGold, resources);
                    TrainFromBuilding(BuildingType.ArcheryRange, 2, arFood, arWood, arGold, resources);
                    break;
                case 2: // Train Archers (counter spearmen)
                    TrainFromBuilding(BuildingType.ArcheryRange, 2, arFood, arWood, arGold, resources);
                    if (age3)
                        TrainFromBuilding(BuildingType.ArcheryRange, 8, sim.Config.CrossbowmanFoodCost, 0, sim.Config.CrossbowmanGoldCost, resources);
                    else
                        TrainFromBuilding(BuildingType.ArcheryRange, 2, arFood, arWood, arGold, resources);
                    TrainFromBuilding(BuildingType.Barracks, 1, spFood, spWood, spGold, resources);
                    break;
                case 3: // Train Horsemen (counter archers)
                    if (HasBuilding(BuildingType.Stables))
                    {
                        TrainFromBuilding(BuildingType.Stables, 3, hrFood, hrWood, hrGold, resources);
                        if (age3)
                            TrainFromBuilding(BuildingType.Stables, 7, sim.Config.KnightFoodCost, 0, sim.Config.KnightGoldCost, resources);
                        else
                            TrainFromBuilding(BuildingType.Stables, 3, hrFood, hrWood, hrGold, resources);
                    }
                    TrainFromBuilding(BuildingType.Barracks, 1, spFood, spWood, spGold, resources);
                    break;
            }

            // Also train monks for healing when Age 3+ and have Monastery
            if (age3 && HasBuilding(BuildingType.Monastery))
                TrainFromBuilding(BuildingType.Monastery, 9, sim.Config.MonkFoodCost, 0, sim.Config.MonkGoldCost, resources);
        }

        private void TrainFromBuilding(BuildingType buildingType, int unitType, int foodCost, int woodCost, int goldCost, PlayerResources resources)
        {
            var building = GetMyBuilding(buildingType);
            if (building == null || building.IsUnderConstruction || building.IsDestroyed) return;
            int maxQueue = NextRandom(1, 16); // 1-15 inclusive
            if (building.TrainingQueue.Count >= maxQueue) return;

            int pop = sim.GetPopulation(playerId);
            int popCap = sim.GetPopulationCap(playerId);
            if (pop >= popCap) return;

            if (resources.Food >= foodCost && resources.Wood >= woodCost && resources.Gold >= goldCost)
                Issue(new TrainUnitCommand(playerId, building.Id, unitType));
        }

        // ── Defense ────────────────────────────────────────────────────

        private void TickDefense(int currentTick)
        {
            if (currentTick < defenseCooldownEnd) return;

            // Scan for enemy combat units near our base (~20 tile radius)
            // Use tile-space integer math to avoid Fixed32 overflow on large maps
            int detectionRadiusSq = 20 * 20;

            int threatCount = 0;
            FixedVector3 threatCenter = default;
            var allUnits = sim.UnitRegistry.GetAllUnits();

            for (int i = 0; i < allUnits.Count; i++)
            {
                var u = allUnits[i];
                if (u.State == UnitState.Dead) continue;
                if (u.PlayerId == playerId || sim.AreAllies(u.PlayerId, playerId)) continue;
                if (u.UnitType == 0) continue; // ignore villagers
                if (u.IsHuntable) continue;

                int dx = (u.SimPosition.x.Raw >> Fixed32.FractionalBits) - baseTileX;
                int dz = (u.SimPosition.z.Raw >> Fixed32.FractionalBits) - baseTileZ;
                int distSq = dx * dx + dz * dz;
                if (distSq < detectionRadiusSq)
                {
                    threatCount++;
                    threatCenter.x = threatCenter.x + u.SimPosition.x;
                    threatCenter.z = threatCenter.z + u.SimPosition.z;
                }
            }

            // Record the most recent "enemy attacking me" tick so on_enemy_attack triggered
            // directives can fire even if the alert ping is rate-limited away below.
            if (threatCount > 0) lastEnemyAttackOnMeTick = currentTick;

            // If our own base is under attack, broadcast a Help ping + chat line (rate-limited).
            if (threatCount > 0 && currentTick - lastEmittedHelpPingTick >= HelpPingCooldownTicks)
            {
                lastEmittedHelpPingTick = currentTick;
                var baseWorld = sim.MapData.TileToWorldFixed(baseTileX, baseTileZ);
                Issue(new PingCommand(playerId, baseWorld.x.Raw, baseWorld.z.Raw, PingType.Help));
                Issue(new AiChatCommand(playerId, AiChatLineType.UnderAttack));
            }

            // Also check ally TCs for threats
            if (threatCount == 0)
            {
                keyBuffer.Clear();
                foreach (var key in sim.FirstTownCenterIds.Keys) keyBuffer.Add(key);
                keyBuffer.Sort();
                for (int ki = 0; ki < keyBuffer.Count; ki++)
                {
                    int tcPlayerId = keyBuffer[ki];
                    if (tcPlayerId == playerId) continue;
                    if (!sim.AreAllies(tcPlayerId, playerId)) continue;
                    var allyTc = sim.BuildingRegistry.GetBuilding(sim.FirstTownCenterIds[tcPlayerId]);
                    if (allyTc == null || allyTc.IsDestroyed) continue;

                    int allyTcTileX = allyTc.SimPosition.x.Raw >> Fixed32.FractionalBits;
                    int allyTcTileZ = allyTc.SimPosition.z.Raw >> Fixed32.FractionalBits;

                    for (int i = 0; i < allUnits.Count; i++)
                    {
                        var u = allUnits[i];
                        if (u.State == UnitState.Dead) continue;
                        if (u.PlayerId == playerId || sim.AreAllies(u.PlayerId, playerId)) continue;
                        if (u.UnitType == 0) continue;
                        if (u.IsHuntable) continue;

                        int adx = (u.SimPosition.x.Raw >> Fixed32.FractionalBits) - allyTcTileX;
                        int adz = (u.SimPosition.z.Raw >> Fixed32.FractionalBits) - allyTcTileZ;
                        int adistSq = adx * adx + adz * adz;
                        if (adistSq < detectionRadiusSq)
                        {
                            threatCount++;
                            threatCenter.x = threatCenter.x + u.SimPosition.x;
                            threatCenter.z = threatCenter.z + u.SimPosition.z;
                        }
                    }
                    if (threatCount > 0) break; // respond to first ally threat found
                }
            }

            if (threatCount == 0 || (combatState == CombatState.Defending && currentTick > defenseModeStartTick + 600))
            {
                // No threat or defense timeout (20s) — return to building state
                if (combatState == CombatState.Defending)
                    combatState = CombatState.Building;
                if (threatCount == 0) return;
            }

            // Calculate average threat position
            var threatCountFixed = Fixed32.FromInt(threatCount);
            threatCenter.x = threatCenter.x / threatCountFixed;
            threatCenter.z = threatCenter.z / threatCountFixed;

            // If serious threat and currently attacking/marching, recall army
            if (threatCount >= 3 && (combatState == CombatState.Attacking || combatState == CombatState.Marching))
            {
                defenseModeStartTick = currentTick;
                combatState = CombatState.Defending;
            }

            // Rally all military to defend (only when threat is serious)
            if (threatCount >= 3)
            {
                GetMyCombatUnits(tempDefenders);
                if (tempDefenders.Count > 0)
                {
                    tempUnitIds.Clear();
                    for (int i = 0; i < tempDefenders.Count; i++)
                        tempUnitIds.Add(tempDefenders[i].Id);
                    var moveCmd = new MoveCommand(playerId, tempUnitIds.ToArray(), threatCenter);
                    moveCmd.IsAttackMove = true;
                    Issue(moveCmd);
                    defenseModeStartTick = currentTick;
                    combatState = CombatState.Defending;
                }
            }

            // Garrison endangered villagers in Town Center
            GarrisonVillagersNearThreat(threatCenter, currentTick);
        }

        private void GarrisonVillagersNearThreat(FixedVector3 threatCenter, int currentTick)
        {
            var tc = GetMyBuilding(BuildingType.TownCenter);
            if (tc == null || tc.IsDestroyed) return;

            // Use tile-space integer math to avoid Fixed32 overflow on large maps
            int dangerRadiusSq = 15 * 15;
            int threatTileX = threatCenter.x.Raw >> Fixed32.FractionalBits;
            int threatTileZ = threatCenter.z.Raw >> Fixed32.FractionalBits;
            GetMyVillagers(tempVillagers);
            tempUnitIds.Clear();

            for (int i = 0; i < tempVillagers.Count; i++)
            {
                var v = tempVillagers[i];
                int dx = (v.SimPosition.x.Raw >> Fixed32.FractionalBits) - threatTileX;
                int dz = (v.SimPosition.z.Raw >> Fixed32.FractionalBits) - threatTileZ;
                int distSq = dx * dx + dz * dz;
                if (distSq < dangerRadiusSq)
                    tempUnitIds.Add(v.Id);
            }

            if (tempUnitIds.Count == 0) return;
            Issue(new GarrisonCommand(playerId, tempUnitIds.ToArray(), tc.Id));

            // Autonomous care: register a short Protect order for the endangered villagers so
            // the auto-economy doesn't immediately march them back into the raid. They release
            // automatically when the order expires (PruneVillagerOrders) and rejoin gathering.
            if (enableAutonomousVillagerCare && currentTick >= autoProtectUntilTick)
            {
                var protectIds = new List<int>();
                for (int i = 0; i < tempUnitIds.Count; i++)
                    if (!reservedVillagerIds.Contains(tempUnitIds[i])) protectIds.Add(tempUnitIds[i]);
                if (protectIds.Count > 0)
                {
                    autoProtectUntilTick = currentTick + 300; // ~10s; refreshed while the raid persists
                    AddVillagerOrder(VillagerTask.Protect, protectIds, -1, threatCenter, tc.Id, autoProtectUntilTick, currentTick);
                    LlmDebug.Cmd($"AI{playerId} auto-protect: {protectIds.Count} villagers garrisoned from raid");
                }
            }
        }

        private int autoProtectUntilTick = -1;

        // ── Scouting ───────────────────────────────────────────────────

        private void TickScouting(int currentTick)
        {
            // Check if scout is still alive
            if (scoutUnitId >= 0)
            {
                var scout = sim.UnitRegistry.GetUnit(scoutUnitId);
                if (scout == null || scout.State == UnitState.Dead)
                    scoutUnitId = -1;
            }

            // Find any existing scout (starting scout or previously trained)
            if (scoutUnitId < 0)
            {
                var allUnits = sim.UnitRegistry.GetAllUnits();
                for (int i = 0; i < allUnits.Count; i++)
                {
                    var u = allUnits[i];
                    if (u.PlayerId == playerId && u.UnitType == 4 && u.State != UnitState.Dead)
                    {
                        scoutUnitId = u.Id;
                        scoutRequested = false;
                        break;
                    }
                }
            }

            // Train a new scout if we have stables and no scout
            if (scoutUnitId < 0 && !scoutRequested && HasBuilding(BuildingType.Stables))
            {
                var stables = GetMyBuilding(BuildingType.Stables);
                if (stables != null && !stables.IsUnderConstruction && !stables.IsDestroyed && stables.TrainingQueue.Count < 2)
                {
                    var resources = sim.ResourceManager.GetPlayerResources(playerId);
                    if (resources.Food >= sim.Config.ScoutFoodCost)
                    {
                        Issue(new TrainUnitCommand(playerId, stables.Id, 4)); // 4 = Scout
                        scoutRequested = true;
                    }
                }
            }

            // Send scout to random target when idle — but not while an explicit scout_area
            // order is in effect, so the commanded destination sticks.
            if (scoutUnitId >= 0 && currentTick >= scoutOverrideUntilTick)
            {
                var scout = sim.UnitRegistry.GetUnit(scoutUnitId);
                if (scout != null && scout.State == UnitState.Idle)
                {
                    var target = GenerateScoutTarget(scout.SimPosition);
                    Issue(new MoveCommand(playerId, new int[] { scoutUnitId }, target));
                }
            }
        }

        private FixedVector3 GenerateScoutTarget(FixedVector3 currentPos)
        {
            int mapSize = sim.MapData.Width;
            int margin = 15;
            int targetX, targetZ;

            if (NextRandom(4) == 0)
            {
                // 25%: head toward a map corner/edge (where bases tend to be)
                int corner = NextRandom(4);
                int jitter = NextRandom(30);
                switch (corner)
                {
                    case 0: targetX = margin + jitter; targetZ = margin + NextRandom(30); break;
                    case 1: targetX = mapSize - margin - jitter; targetZ = margin + NextRandom(30); break;
                    case 2: targetX = mapSize - margin - jitter; targetZ = mapSize - margin - NextRandom(30); break;
                    default: targetX = margin + jitter; targetZ = mapSize - margin - NextRandom(30); break;
                }
            }
            else
            {
                // 75%: random offset from current position (20-60 tiles)
                int curTileX = currentPos.x.Raw >> Fixed32.FractionalBits;
                int curTileZ = currentPos.z.Raw >> Fixed32.FractionalBits;
                int dx = NextRandom(-60, 61);
                int dz = NextRandom(-60, 61);
                // Ensure minimum distance
                if (dx > -20 && dx < 20) dx = dx >= 0 ? 20 : -20;
                if (dz > -20 && dz < 20) dz = dz >= 0 ? 20 : -20;
                targetX = curTileX + dx;
                targetZ = curTileZ + dz;
            }

            targetX = Mathf.Clamp(targetX, margin, mapSize - margin);
            targetZ = Mathf.Clamp(targetZ, margin, mapSize - margin);

            // Validate walkability — retry up to 10 times, then fallback to nearest walkable
            if (!sim.MapData.IsWalkable(targetX, targetZ))
            {
                bool found = false;
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    int rx = Mathf.Clamp(targetX + NextRandom(-15, 16), margin, mapSize - margin);
                    int rz = Mathf.Clamp(targetZ + NextRandom(-15, 16), margin, mapSize - margin);
                    if (sim.MapData.IsWalkable(rx, rz))
                    {
                        targetX = rx;
                        targetZ = rz;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    var walkable = GridPathfinder.FindNearestWalkableTile(sim.MapData, new Vector2Int(targetX, targetZ), 20);
                    if (walkable.x >= 0) { targetX = walkable.x; targetZ = walkable.y; }
                }
            }

            return sim.MapData.TileToWorldFixed(targetX, targetZ);
        }

        private void DiscoverEnemyBases()
        {
            // Only run every 5th think tick
            if (++discoverEnemyCounter % 5 != 0) return;

            // Check all enemy buildings to see if any of our units are nearby
            // Uses cachedMyUnits instead of scanning all units
            var detectionRangeSq = Fixed32.FromInt(25 * 25);
            var allBuildings = sim.BuildingRegistry.GetAllBuildings();

            for (int i = 0; i < allBuildings.Count; i++)
            {
                var b = allBuildings[i];
                if (b.IsDestroyed) continue;
                if (b.PlayerId == playerId || sim.AreAllies(b.PlayerId, playerId)) continue;
                if (knownEnemyBases.ContainsKey(b.PlayerId)) continue;

                // Only detect if we have a unit nearby — iterate our units only
                for (int j = 0; j < cachedMyUnits.Count; j++)
                {
                    var u = cachedMyUnits[j];
                    var dx = u.SimPosition.x - b.SimPosition.x;
                    var dz = u.SimPosition.z - b.SimPosition.z;
                    var distSq = dx * dx + dz * dz;
                    if (distSq < detectionRangeSq)
                    {
                        knownEnemyBases[b.PlayerId] = b.SimPosition;
                        break;
                    }
                }
            }
        }

        // ── Combat ─────────────────────────────────────────────────────

        // Reads pings issued by allies and translates them into short-lived
        // combat/defense overrides. Determinism is preserved because every client
        // sees the same RecentPings list in the same order.
        private void TickAllyPings(int currentTick)
        {
            var pings = sim.RecentPings;
            int watermark = lastProcessedPingTick;
            for (int i = 0; i < pings.Count; i++)
            {
                var p = pings[i];
                if (p.Tick <= lastProcessedPingTick) continue;
                if (p.PlayerId == playerId) continue;             // ignore self
                if (!sim.AreAllies(p.PlayerId, playerId)) continue; // ally-only

                var pos = new FixedVector3(
                    Fixed32.FromFloat(p.WorldX),
                    Fixed32.Zero,
                    Fixed32.FromFloat(p.WorldZ));

                switch (p.Type)
                {
                    case PingType.Attack:
                        pingAttackTarget = pos;
                        pingAttackUntilTick = currentTick + PingDirectiveDurationTicks;
                        pingAttackUnitClass = 0; // human pings don't specify class
                        // Nudge into Assembling if currently idle and we have any force at all.
                        if (combatState == CombatState.Building)
                            combatState = CombatState.Assembling;
                        Issue(new AiChatCommand(playerId, AiChatLineType.OnTheWayAttack));
                        break;
                    case PingType.Defend:
                    case PingType.Help:
                        pingDefendTarget = pos;
                        pingDefendUntilTick = currentTick + PingDirectiveDurationTicks;
                        pingDefendUnitClass = 0; // human pings don't specify class
                        DispatchDefendersToPing();
                        Issue(new AiChatCommand(playerId, AiChatLineType.OnTheWayDefend));
                        break;
                }
                if (p.Tick > watermark) watermark = p.Tick;
            }
            lastProcessedPingTick = watermark;
        }

        // Applied by GameSimulation.ProcessAiIntentCommand on every client. Deterministic
        // by construction: pure switch over ints, mutates only AI override fields. No
        // allocations on the hot path. DurationTicks is computed relative to currentTick
        // so windows expire identically on all clients.
        //
        // When triggerType != 0 the directive is parked in pendingDirectives and applied
        // later when its trigger condition fires. The trigger evaluation happens inside
        // Tick (TickPendingDirectives) which runs identically on every client.
        public void ApplyIntent(int intentKind, int paramA, int paramB, int paramC, int paramD,
            int durationTicks, int currentTick, int triggerType = 0, int triggerMagnitude = 0)
        {
            if (triggerType != 0)
            {
                if (pendingDirectives.Count >= MaxPendingDirectives) return;
                pendingDirectives.Add(new PendingDirective
                {
                    IntentKind = intentKind,
                    ParamA = paramA, ParamB = paramB, ParamC = paramC, ParamD = paramD,
                    DurationTicks = durationTicks,
                    TriggerType = triggerType,
                    TriggerMagnitude = triggerMagnitude,
                    EnqueuedTick = currentTick,
                });
                return;
            }

            ApplyIntentImmediate(intentKind, paramA, paramB, paramC, paramD, durationTicks, currentTick);
        }

        private void ApplyIntentImmediate(int intentKind, int paramA, int paramB, int paramC, int paramD,
            int durationTicks, int currentTick)
        {
            int until = currentTick + Mathf.Clamp(durationTicks, 30, 5400); // 1s..3min
            LlmDebug.Cmd($"AI{playerId} apply {(AiIntentKind)intentKind} A={paramA} B={paramB} C={paramC} D={paramD}");

            switch ((AiIntentKind)intentKind)
            {
                case AiIntentKind.SendGroup:
                {
                    var pos = new FixedVector3(new Fixed32(paramA), Fixed32.Zero, new Fixed32(paramB));
                    CreateDetachment(Mathf.Clamp(paramC, 0, 3), Mathf.Clamp(paramD, 1, 100), pos, until);
                    break;
                }
                case AiIntentKind.AttackAt:
                {
                    var pos = new FixedVector3(new Fixed32(paramA), Fixed32.Zero, new Fixed32(paramB));
                    ClearDetachments(); // an all-in attack recalls any split-off groups
                    pingAttackTarget = pos;
                    pingAttackUntilTick = until;
                    pingAttackUnitClass = Mathf.Clamp(paramC, 0, 3);
                    combatHoldUntilTick = -1; // an attack order overrides any defend/retreat hold
                    if (combatState == CombatState.Building)
                        combatState = CombatState.Assembling;
                    break;
                }
                case AiIntentKind.DefendAt:
                {
                    var pos = new FixedVector3(new Fixed32(paramA), Fixed32.Zero, new Fixed32(paramB));
                    ClearDetachments(); // pull split-off groups back to defend
                    pingDefendTarget = pos;
                    pingDefendUntilTick = until;
                    pingDefendUnitClass = Mathf.Clamp(paramC, 0, 3);
                    DispatchDefendersToPing();
                    // Hold at the defend point — don't let the combat FSM pull these units into an attack.
                    combatHoldUntilTick = until;
                    break;
                }
                case AiIntentKind.SetAggression:
                    aggressionAttackOverride = Mathf.Clamp(paramA, 2, 32);
                    aggressionRetreatOverride = Mathf.Clamp(paramB, 10, 80);
                    aggressionOverrideUntilTick = until;
                    break;
                case AiIntentKind.PrioritizeResource:
                {
                    int delta = Mathf.Clamp(paramB, -5, 10);
                    switch ((ResourceType)paramA)
                    {
                        case ResourceType.Food: resourceWeightFoodOverride = delta; break;
                        case ResourceType.Wood: resourceWeightWoodOverride = delta; break;
                        case ResourceType.Gold: resourceWeightGoldOverride = delta; break;
                        case ResourceType.Stone: resourceWeightStoneOverride = delta; break;
                    }
                    resourceOverrideUntilTick = until;
                    break;
                }
                case AiIntentKind.FocusEconomy:
                {
                    // Preset: more economy, less aggression.
                    int strength = Mathf.Clamp(paramA, 0, 100);
                    aggressionAttackOverride = Mathf.Clamp(attackThreshold + strength / 10, 2, 32);
                    aggressionRetreatOverride = Mathf.Clamp(retreatPercentInt + strength / 4, 10, 80);
                    aggressionOverrideUntilTick = until;
                    resourceWeightFoodOverride = strength / 25;   // +0..+4
                    resourceWeightWoodOverride = strength / 33;   // +0..+3
                    resourceOverrideUntilTick = until;
                    break;
                }
                case AiIntentKind.FocusMilitary:
                {
                    int strength = Mathf.Clamp(paramA, 0, 100);
                    aggressionAttackOverride = Mathf.Clamp(attackThreshold - strength / 12, 2, 32);
                    aggressionRetreatOverride = Mathf.Clamp(retreatPercentInt - strength / 5, 10, 80);
                    aggressionOverrideUntilTick = until;
                    resourceWeightGoldOverride = strength / 25;   // +0..+4
                    resourceWeightFoodOverride = -strength / 50;  // -0..-2
                    resourceOverrideUntilTick = until;
                    break;
                }
                case AiIntentKind.PushAgeUp:
                    forceAgeUpTarget = Mathf.Clamp(paramA, 2, 3);
                    forceAgeUpUntilTick = until;
                    break;
                case AiIntentKind.BuildStructure:
                {
                    var btype = (BuildingType)paramA;
                    int tileX = paramB >> Fixed32.FractionalBits;
                    int tileZ = paramC >> Fixed32.FractionalBits;
                    int builders = paramD > 0 ? Mathf.Clamp(paramD, 1, 10) : 1;
                    pendingLlmBuildTick = currentTick; // clear the retry gate for an immediate attempt
                    TryPlaceBuilding(btype, tileX, tileZ, currentTick, ref pendingLlmBuildTick, builders);
                    break;
                }
                case AiIntentKind.GatherWith:
                {
                    var pos = new FixedVector3(new Fixed32(paramA), Fixed32.Zero, new Fixed32(paramB));
                    var ids = ReserveVillagers(Mathf.Clamp(paramD, 1, 50), pos);
                    // ServiceGatherOrder handles drop-off building / construction-help / gather.
                    AddVillagerOrder(VillagerTask.Gather, ids, paramC, pos, -1, until, currentTick);
                    break;
                }
                case AiIntentKind.ProtectVillagers:
                {
                    var pos = new FixedVector3(new Fixed32(paramA), Fixed32.Zero, new Fixed32(paramB));
                    int count = paramC <= 0 ? cachedVillagers.Count : paramC;
                    var tc = GetMyBuilding(BuildingType.TownCenter);
                    int tcId = (tc != null && !tc.IsDestroyed) ? tc.Id : -1;
                    var ids = ReserveVillagers(count, pos);
                    AddVillagerOrder(VillagerTask.Protect, ids, -1, pos, tcId, until, currentTick);
                    break;
                }
                case AiIntentKind.RepairBuilding:
                {
                    var pos = new FixedVector3(new Fixed32(paramA), Fixed32.Zero, new Fixed32(paramB));
                    var dmg = FindNearestDamagedBuilding(pos, paramD);
                    if (dmg != null)
                    {
                        var ids = ReserveVillagers(Mathf.Clamp(paramC, 1, 10), dmg.SimPosition);
                        AddVillagerOrder(VillagerTask.Repair, ids, -1, dmg.SimPosition, dmg.Id, until, currentTick);
                    }
                    break;
                }
                case AiIntentKind.SetGatherTargets:
                    villagerTargetFood = Mathf.Clamp(paramA, -1, 60);
                    villagerTargetWood = Mathf.Clamp(paramB, -1, 60);
                    villagerTargetGold = Mathf.Clamp(paramC, -1, 60);
                    villagerTargetStone = Mathf.Clamp(paramD, -1, 60);
                    villagerTargetUntilTick = until;
                    break;
                case AiIntentKind.TrainUnits:
                    AddTrainOrder(paramA, Mathf.Clamp(paramB, 1, 30), until);
                    break;
                case AiIntentKind.SetProductionMix:
                    prodMixArchers = Mathf.Clamp(paramA, 0, 100);
                    prodMixCavalry = Mathf.Clamp(paramB, 0, 100);
                    prodMixInfantry = Mathf.Clamp(paramC, 0, 100);
                    prodMixActive = (prodMixArchers + prodMixCavalry + prodMixInfantry) > 0;
                    prodMixUntilTick = until;
                    break;
                case AiIntentKind.SetArmyRally:
                    SetAllMilitaryRally(new FixedVector3(new Fixed32(paramA), Fixed32.Zero, new Fixed32(paramB)));
                    break;
                case AiIntentKind.ScoutArea:
                    SendScoutTo(new FixedVector3(new Fixed32(paramA), Fixed32.Zero, new Fixed32(paramB)), until);
                    // Keep the scout on the commanded spot — suppress routine random re-tasking.
                    scoutOverrideUntilTick = until;
                    break;
                case AiIntentKind.RegroupArmy:
                    ClearDetachments(); // gather EVERYONE, including split-off groups
                    MoveAllCombatTo(new FixedVector3(new Fixed32(paramA), Fixed32.Zero, new Fixed32(paramB)));
                    // Gather and hold — don't let the FSM march them back out next tick.
                    combatState = CombatState.Building;
                    combatHoldUntilTick = until;
                    break;
                case AiIntentKind.RetreatToBase:
                    ClearDetachments(); // recall split-off groups to base
                    MoveAllCombatTo(new FixedVector3(new Fixed32(paramA), Fixed32.Zero, new Fixed32(paramB)));
                    // Stand down and play passive so TickCombat doesn't immediately re-commit.
                    combatState = CombatState.Building;
                    combatHoldUntilTick = until;
                    aggressionAttackOverride = 32; // highest threshold = least likely to attack
                    aggressionRetreatOverride = 70;
                    aggressionOverrideUntilTick = until;
                    break;
                case AiIntentKind.Research:
                    IssueResearch((TechnologyType)paramA);
                    break;
                case AiIntentKind.Acknowledge:
                case AiIntentKind.Decline:
                    // chat-only, no behavior change
                    break;
            }
        }

        // Send units to a defend-ping location. If pingDefendUnitClass is set (non-zero),
        // send 100% of that class; otherwise send roughly half of total combat units.
        private void DispatchDefendersToPing()
        {
            GetUndetachedCombatUnits(tempCombatUnits);
            if (tempCombatUnits.Count == 0) return;

            List<UnitData> dispatchSource;
            int sendCount;
            if (pingDefendUnitClass != 0)
            {
                FilterCombatUnitsByClass(tempCombatUnits, pingDefendUnitClass, tempFilteredUnits);
                if (tempFilteredUnits.Count == 0) return;
                dispatchSource = tempFilteredUnits;
                sendCount = tempFilteredUnits.Count;
            }
            else
            {
                dispatchSource = tempCombatUnits;
                sendCount = Mathf.Max(2, tempCombatUnits.Count / 2);
                sendCount = Mathf.Min(sendCount, tempCombatUnits.Count);
            }

            tempUnitIds.Clear();
            for (int i = 0; i < sendCount; i++)
                tempUnitIds.Add(dispatchSource[i].Id);
            Issue(new MoveCommand(playerId, tempUnitIds.ToArray(), pingDefendTarget));
        }

        private void TickCombat(int currentTick)
        {
            // Don't override defense state
            if (combatState == CombatState.Defending) return;

            // (combat unit gathering below uses GetUndetachedCombatUnits so the main army
            //  FSM never re-absorbs units that belong to an active detachment.)

            // Stand down while an explicit commander order (defend/regroup/retreat) is in effect:
            // hold position and don't re-issue our own movement. This also suppresses the
            // 5-minute forced-assembly path below for the duration of the order.
            if (currentTick < combatHoldUntilTick) return;

            GetUndetachedCombatUnits(tempCombatUnits);
            int armySize = tempCombatUnits.Count;

            switch (combatState)
            {
                case CombatState.Building:
                    if (firstMilitaryBuildingTick < 0 && (HasBuilding(BuildingType.Barracks) || HasBuilding(BuildingType.ArcheryRange) || HasBuilding(BuildingType.Stables)))
                        firstMilitaryBuildingTick = currentTick;

                    int effectiveThreshold = EffectiveAttackThreshold(currentTick);
                    if (armySize >= effectiveThreshold)
                    {
                        combatState = CombatState.Assembling;
                    }
                    else if (firstMilitaryBuildingTick > 0 && currentTick - firstMilitaryBuildingTick > 6000
                             && armySize >= effectiveThreshold / 2 && armySize >= 4)
                    {
                        combatState = CombatState.Assembling;
                    }
                    else if (currentTick >= 9000 && armySize >= 1)
                    {
                        combatState = CombatState.Assembling;
                    }
                    break;

                case CombatState.Assembling:
                {
                    // Honor an in-flight ally Attack ping over the AI's own target pick.
                    bool pingActive = currentTick < pingAttackUntilTick;
                    FixedVector3? targetPos = pingActive
                        ? pingAttackTarget
                        : GetEnemyTargetPosition();
                    if (targetPos.HasValue)
                    {
                        // Filter the dispatched units by class if the ping override specified one.
                        List<UnitData> dispatchSource = tempCombatUnits;
                        if (pingActive && pingAttackUnitClass != 0)
                        {
                            FilterCombatUnitsByClass(tempCombatUnits, pingAttackUnitClass, tempFilteredUnits);
                            dispatchSource = tempFilteredUnits;
                        }
                        if (dispatchSource.Count == 0)
                        {
                            combatState = CombatState.Building;
                            break;
                        }

                        attackTargetPos = targetPos.Value;
                        // Compute staging point ~15 tiles from target, toward our base
                        int targetTileX = attackTargetPos.x.Raw >> Fixed32.FractionalBits;
                        int targetTileZ = attackTargetPos.z.Raw >> Fixed32.FractionalBits;
                        int dx = baseTileX - targetTileX;
                        int dz = baseTileZ - targetTileZ;
                        int dist = Mathf.Max(1, Mathf.Abs(dx) + Mathf.Abs(dz));
                        int stagingX = targetTileX + dx * 15 / dist;
                        int stagingZ = targetTileZ + dz * 15 / dist;

                        // Validate staging point walkability
                        if (!sim.MapData.IsWalkable(stagingX, stagingZ))
                        {
                            var walkable = GridPathfinder.FindNearestWalkableTile(sim.MapData, new Vector2Int(stagingX, stagingZ), 20);
                            if (walkable.x >= 0) { stagingX = walkable.x; stagingZ = walkable.y; }
                        }

                        var stagingPos = sim.MapData.TileToWorldFixed(stagingX, stagingZ);

                        tempUnitIds.Clear();
                        for (int i = 0; i < dispatchSource.Count; i++)
                            tempUnitIds.Add(dispatchSource[i].Id);
                        Issue(new MoveCommand(playerId, tempUnitIds.ToArray(), stagingPos));

                        attackStartArmySize = dispatchSource.Count;
                        marchStartTick = currentTick;
                        combatState = CombatState.Marching;
                    }
                    else
                    {
                        combatState = CombatState.Building;
                    }
                    break;
                }

                case CombatState.Marching:
                {
                    // March/attack-move only the subset matching the active ping class filter.
                    bool pingActive = currentTick < pingAttackUntilTick && pingAttackUnitClass != 0;
                    List<UnitData> activeUnits;
                    if (pingActive)
                    {
                        FilterCombatUnitsByClass(tempCombatUnits, pingAttackUnitClass, tempFilteredUnits);
                        activeUnits = tempFilteredUnits;
                    }
                    else
                    {
                        activeUnits = tempCombatUnits;
                    }

                    // Check if any unit is within ~20 tiles of the target
                    int atkTileX = attackTargetPos.x.Raw >> Fixed32.FractionalBits;
                    int atkTileZ = attackTargetPos.z.Raw >> Fixed32.FractionalBits;
                    bool closeEnough = false;
                    for (int i = 0; i < activeUnits.Count; i++)
                    {
                        int ux = activeUnits[i].SimPosition.x.Raw >> Fixed32.FractionalBits;
                        int uz = activeUnits[i].SimPosition.z.Raw >> Fixed32.FractionalBits;
                        int udx = ux - atkTileX;
                        int udz = uz - atkTileZ;
                        if (udx * udx + udz * udz < 20 * 20)
                        {
                            closeEnough = true;
                            break;
                        }
                    }
                    if (closeEnough)
                    {
                        tempUnitIds.Clear();
                        for (int i = 0; i < activeUnits.Count; i++)
                            tempUnitIds.Add(activeUnits[i].Id);
                        if (tempUnitIds.Count > 0)
                        {
                            var marchCmd = new MoveCommand(playerId, tempUnitIds.ToArray(), attackTargetPos);
                            marchCmd.IsAttackMove = true;
                            Issue(marchCmd);
                        }
                        combatState = CombatState.Attacking;
                    }
                    else if (currentTick - marchStartTick > 300)
                    {
                        // Timeout — attack-move directly to target
                        tempUnitIds.Clear();
                        for (int i = 0; i < activeUnits.Count; i++)
                            tempUnitIds.Add(activeUnits[i].Id);
                        if (tempUnitIds.Count > 0)
                        {
                            var directCmd = new MoveCommand(playerId, tempUnitIds.ToArray(), attackTargetPos);
                            directCmd.IsAttackMove = true;
                            Issue(directCmd);
                        }
                        combatState = CombatState.Attacking;
                    }
                    break;
                }

                case CombatState.Attacking:
                    // Retreat when we've lost retreatPercentInt% of our army
                    int retreatAt = Mathf.Max(1, attackStartArmySize * (100 - EffectiveRetreatPercent(currentTick)) / 100);
                    if (armySize <= retreatAt)
                    {
                        if (armySize > 0)
                        {
                            tempUnitIds.Clear();
                            for (int i = 0; i < tempCombatUnits.Count; i++)
                                tempUnitIds.Add(tempCombatUnits[i].Id);
                            var homePos = sim.MapData.TileToWorldFixed(baseTileX, baseTileZ);
                            Issue(new MoveCommand(playerId, tempUnitIds.ToArray(), homePos));
                        }
                        retreatCooldownEnd = currentTick + retreatCooldownTicks;
                        combatState = CombatState.Retreating;
                    }
                    else
                    {
                        // Victory at target: no enemies nearby → pick next target
                        int atkTX = attackTargetPos.x.Raw >> Fixed32.FractionalBits;
                        int atkTZ = attackTargetPos.z.Raw >> Fixed32.FractionalBits;
                        bool enemyNearTarget = false;
                        var allBldgs = sim.BuildingRegistry.GetAllBuildings();
                        for (int i = 0; i < allBldgs.Count; i++)
                        {
                            var b = allBldgs[i];
                            if (b.PlayerId == playerId || b.IsDestroyed) continue;
                            if (sim.AreAllies(b.PlayerId, playerId)) continue;
                            int bx = b.SimPosition.x.Raw >> Fixed32.FractionalBits;
                            int bz = b.SimPosition.z.Raw >> Fixed32.FractionalBits;
                            int dx = bx - atkTX;
                            int dz = bz - atkTZ;
                            if (dx * dx + dz * dz < 25 * 25) { enemyNearTarget = true; break; }
                        }
                        if (!enemyNearTarget)
                            combatState = CombatState.Assembling;
                    }
                    break;

                case CombatState.Retreating:
                    if (currentTick >= retreatCooldownEnd)
                        combatState = CombatState.Building;
                    break;
            }
        }

        private FixedVector3? GetEnemyTargetPosition()
        {
            // Prune entries for players with no remaining buildings
            keyBuffer.Clear();
            foreach (var key in knownEnemyBases.Keys) keyBuffer.Add(key);
            for (int ki = 0; ki < keyBuffer.Count; ki++)
            {
                int k = keyBuffer[ki];
                bool hasBuilding = false;
                var allBuildings = sim.BuildingRegistry.GetAllBuildings();
                for (int bi = 0; bi < allBuildings.Count; bi++)
                {
                    if (allBuildings[bi].PlayerId == k && !allBuildings[bi].IsDestroyed)
                    {
                        hasBuilding = true;
                        break;
                    }
                }
                if (!hasBuilding) knownEnemyBases.Remove(k);
            }

            // 1. Use known enemy base positions from scouting (nearest one)
            if (knownEnemyBases.Count > 0)
            {
                FixedVector3? bestPos = null;
                int bestDistSq = int.MaxValue;
                int bestKey = int.MaxValue;

                keyBuffer.Clear();
                foreach (var key in knownEnemyBases.Keys) keyBuffer.Add(key);
                keyBuffer.Sort();
                for (int ki = 0; ki < keyBuffer.Count; ki++)
                {
                    int k = keyBuffer[ki];
                    // Skip allies
                    if (sim.AreAllies(k, playerId)) continue;

                    var val = knownEnemyBases[k];
                    int tx = val.x.Raw >> Fixed32.FractionalBits;
                    int tz = val.z.Raw >> Fixed32.FractionalBits;
                    int dx = tx - baseTileX;
                    int dz = tz - baseTileZ;
                    int distSq = dx * dx + dz * dz;
                    if (distSq < bestDistSq || (distSq == bestDistSq && k < bestKey))
                    {
                        bestDistSq = distSq;
                        bestKey = k;
                        bestPos = val;
                    }
                }

                if (bestPos.HasValue) return bestPos;
            }

            // 2. Fall back to FirstTownCenterIds — pick nearest non-allied TC
            {
                FixedVector3? bestPos = null;
                int bestDistSq = int.MaxValue;
                int bestKey = int.MaxValue;

                keyBuffer.Clear();
                foreach (var key in sim.FirstTownCenterIds.Keys) keyBuffer.Add(key);
                keyBuffer.Sort();
                for (int ki = 0; ki < keyBuffer.Count; ki++)
                {
                    int k = keyBuffer[ki];
                    if (k == playerId) continue;
                    if (sim.AreAllies(k, playerId)) continue;

                    var tc = sim.BuildingRegistry.GetBuilding(sim.FirstTownCenterIds[k]);
                    if (tc == null || tc.IsDestroyed) continue;

                    int tx = tc.SimPosition.x.Raw >> Fixed32.FractionalBits;
                    int tz = tc.SimPosition.z.Raw >> Fixed32.FractionalBits;
                    int dx = tx - baseTileX;
                    int dz = tz - baseTileZ;
                    int distSq = dx * dx + dz * dz;
                    if (distSq < bestDistSq || (distSq == bestDistSq && k < bestKey))
                    {
                        bestDistSq = distSq;
                        bestKey = k;
                        bestPos = tc.SimPosition;
                    }
                }

                if (bestPos.HasValue) return bestPos;
            }

            // 3. Fall back to nearest enemy building
            {
                BuildingData nearest = null;
                int nearestDistSq = int.MaxValue;
                var allBuildings = sim.BuildingRegistry.GetAllBuildings();
                for (int i = 0; i < allBuildings.Count; i++)
                {
                    var b = allBuildings[i];
                    if (b.PlayerId == playerId || b.IsDestroyed) continue;
                    if (sim.AreAllies(b.PlayerId, playerId)) continue;

                    int tx = b.SimPosition.x.Raw >> Fixed32.FractionalBits;
                    int tz = b.SimPosition.z.Raw >> Fixed32.FractionalBits;
                    int dx = tx - baseTileX;
                    int dz = tz - baseTileZ;
                    int distSq = dx * dx + dz * dz;
                    if (distSq < nearestDistSq)
                    {
                        nearestDistSq = distSq;
                        nearest = b;
                    }
                }

                return nearest?.SimPosition;
            }
        }

        // ── Building Placement ─────────────────────────────────────────

        private void TryPlaceBuilding(BuildingType type, int centerX, int centerZ, int currentTick, ref int pendingTick, int builderCount = 1)
        {
            if (currentTick < pendingTick) return;

            // Must have villager(s) to construct. builderCount>1 commits a whole crew.
            int[] villagerIds = builderCount > 1 ? FindMultipleVillagers(builderCount) : FindIdleVillager();
            if (villagerIds == null)
            {
                pendingTick = currentTick + BuildRetryDelay;
                return;
            }

            int footW, footH;
            GetFootprint(type, out footW, out footH);

            var tile = FindBuildableTile(centerX, centerZ, footW, footH, type);
            if (tile.x < 0)
            {
                pendingTick = currentTick + BuildRetryDelay;
                return;
            }

            Issue(new PlaceBuildingCommand(playerId, type, tile.x, tile.y, villagerIds));
            for (int i = 0; i < villagerIds.Length; i++)
                assignedBuilderIds.Add(villagerIds[i]);
            pendingTick = currentTick + BuildRetryDelay;
        }

        private int[] FindIdleVillager()
        {
            GetMyVillagers(tempVillagers);
            // First pass: prefer idle villagers
            for (int i = 0; i < tempVillagers.Count; i++)
            {
                if (reservedVillagerIds.Contains(tempVillagers[i].Id)) continue; // don't poach commanded villagers
                if (tempVillagers[i].State == UnitState.Idle && !assignedBuilderIds.Contains(tempVillagers[i].Id))
                    return new int[] { tempVillagers[i].Id };
            }
            // Second pass: pull a gathering villager if no idle ones
            for (int i = 0; i < tempVillagers.Count; i++)
            {
                if (reservedVillagerIds.Contains(tempVillagers[i].Id)) continue;
                var state = tempVillagers[i].State;
                if (state == UnitState.Gathering || state == UnitState.MovingToGather
                    || state == UnitState.MovingToDropoff)
                    return new int[] { tempVillagers[i].Id };
            }
            return null;
        }

        private Vector2Int FindBuildableTile(int centerX, int centerZ, int footprintW, int footprintH, BuildingType type)
        {
            int border = (type == BuildingType.Wall || type == BuildingType.Farm || type == BuildingType.StoneWall || type == BuildingType.StoneGate || type == BuildingType.WoodGate) ? 0 : 1;
            for (int radius = 1; radius <= 20; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        if (Mathf.Abs(dx) != radius && Mathf.Abs(dz) != radius) continue;

                        int tx = centerX + dx;
                        int tz = centerZ + dz;

                        if (IsAreaBuildable(tx, tz, footprintW, footprintH, border, type))
                            return new Vector2Int(tx, tz);
                    }
                }
            }
            return new Vector2Int(-1, -1);
        }

        private bool IsAreaBuildable(int tileX, int tileZ, int footW, int footH, int border, BuildingType type = BuildingType.House)
        {
            bool isFarm = type == BuildingType.Farm;
            for (int x = tileX - border; x < tileX + footW + border; x++)
                for (int z = tileZ - border; z < tileZ + footH + border; z++)
                    if (isFarm ? !sim.MapData.IsBuildableForFarm(x, z) : !sim.MapData.IsBuildable(x, z)) return false;
            return true;
        }

        private void GetFootprint(BuildingType type, out int w, out int h)
        {
            var cfg = sim.Config;
            switch (type)
            {
                case BuildingType.House: w = cfg.HouseFootprintWidth; h = cfg.HouseFootprintHeight; break;
                case BuildingType.Barracks: w = cfg.BarracksFootprintWidth; h = cfg.BarracksFootprintHeight; break;
                case BuildingType.TownCenter: w = cfg.TownCenterFootprintWidth; h = cfg.TownCenterFootprintHeight; break;
                case BuildingType.Mill: w = cfg.MillFootprintWidth; h = cfg.MillFootprintHeight; break;
                case BuildingType.LumberYard: w = cfg.LumberYardFootprintWidth; h = cfg.LumberYardFootprintHeight; break;
                case BuildingType.Mine: w = cfg.MineFootprintWidth; h = cfg.MineFootprintHeight; break;
                case BuildingType.ArcheryRange: w = cfg.ArcheryRangeFootprintWidth; h = cfg.ArcheryRangeFootprintHeight; break;
                case BuildingType.Stables: w = cfg.StablesFootprintWidth; h = cfg.StablesFootprintHeight; break;
                case BuildingType.Farm: w = cfg.FarmFootprintWidth; h = cfg.FarmFootprintHeight; break;
                case BuildingType.Tower: w = cfg.TowerFootprintWidth; h = cfg.TowerFootprintHeight; break;
                case BuildingType.Monastery: w = cfg.MonasteryFootprintWidth; h = cfg.MonasteryFootprintHeight; break;
                case BuildingType.Blacksmith: w = cfg.BlacksmithFootprintWidth; h = cfg.BlacksmithFootprintHeight; break;
                case BuildingType.Market: w = cfg.MarketFootprintWidth; h = cfg.MarketFootprintHeight; break;
                case BuildingType.University: w = cfg.UniversityFootprintWidth; h = cfg.UniversityFootprintHeight; break;
                case BuildingType.SiegeWorkshop: w = cfg.SiegeWorkshopFootprintWidth; h = cfg.SiegeWorkshopFootprintHeight; break;
                case BuildingType.Keep: w = cfg.KeepFootprintWidth; h = cfg.KeepFootprintHeight; break;
                case BuildingType.StoneWall: w = cfg.StoneWallFootprintWidth; h = cfg.StoneWallFootprintHeight; break;
                case BuildingType.StoneGate: w = cfg.StoneGateFootprintWidth; h = cfg.StoneGateFootprintHeight; break;
                case BuildingType.WoodGate: w = cfg.WoodGateFootprintWidth; h = cfg.WoodGateFootprintHeight; break;
                case BuildingType.Wonder: w = cfg.WonderFootprintWidth; h = cfg.WonderFootprintHeight; break;
                case BuildingType.Landmark: w = 4; h = 4; break;
                default: w = 2; h = 2; break;
            }
        }

        // ── Helpers ────────────────────────────────────────────────────

        private void GetMyVillagers(List<UnitData> result)
        {
            result.Clear();
            result.AddRange(cachedVillagers);
        }

        private int GetVillagerCount()
        {
            return cachedVillagers.Count;
        }

        private void GetMyCombatUnits(List<UnitData> result)
        {
            result.Clear();
            result.AddRange(cachedCombatUnits);
        }

        private bool HasBuilding(BuildingType type)
        {
            return cachedBuildingTypes.Contains(type);
        }

        private BuildingData GetMyBuilding(BuildingType type)
        {
            for (int i = 0; i < cachedMyBuildings.Count; i++)
            {
                if (cachedMyBuildings[i].Type == type)
                    return cachedMyBuildings[i];
            }
            return null;
        }

        private ResourceNodeData FindNearestResourceNode(FixedVector3 pos, ResourceType type, bool excludeFarms = false, HashSet<int> claimedFarmIds = null)
        {
            // Use tile-space integer math to avoid Fixed32 overflow on large maps
            int originTileX = pos.x.Raw >> Fixed32.FractionalBits;
            int originTileZ = pos.z.Raw >> Fixed32.FractionalBits;

            const int maxSearchDist = 80;
            const int maxSearchDistSq = maxSearchDist * maxSearchDist;

            var nodes = sim.MapData.GetAllResourceNodes();
            ResourceNodeData best = null;
            int bestDistSq = maxSearchDistSq;
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n.Type != type || n.IsDepleted) continue;
                if (excludeFarms && n.IsFarmNode) continue;

                // Skip occupied or already-claimed farms
                if (n.IsFarmNode)
                {
                    if (claimedFarmIds != null && claimedFarmIds.Contains(n.Id)) continue;
                    if (sim.IsFarmNodeOccupiedByAny(n.Id)) continue;
                }

                int dx = n.TileX - originTileX;
                int dz = n.TileZ - originTileZ;
                int distSq = dx * dx + dz * dz;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = n;
                }
            }
            return best;
        }

        // ── Expanded LLM control-surface helpers ────────────────────────
        // All run inside the deterministic tick (via ApplyIntent) and only enqueue
        // AI commands, so lockstep determinism holds across clients.

        private void AddTrainOrder(int menuType, int count, int expiryTick)
        {
            for (int i = 0; i < trainOrders.Count; i++)
            {
                if (trainOrders[i].MenuType == menuType)
                {
                    var o = trainOrders[i];
                    o.Remaining = Mathf.Min(60, o.Remaining + count);
                    o.ExpiryTick = expiryTick;
                    trainOrders[i] = o;
                    return;
                }
            }
            if (trainOrders.Count >= MaxTrainOrders) return;
            trainOrders.Add(new TrainOrder { MenuType = menuType, Remaining = count, ExpiryTick = expiryTick });
        }

        // Drain outstanding train requests, one unit per order per military tick,
        // respecting building availability, pop cap, queue depth and affordability.
        // ProcessTrainUnitCommand re-validates, but we pre-check so we only decrement
        // an order when the unit will actually be queued.
        private void TickTrainOrders(PlayerResources resources, int currentTick)
        {
            if (trainOrders.Count == 0) return;
            int spentFood = 0, spentWood = 0, spentGold = 0;
            int pop = sim.GetPopulation(playerId);
            int popCap = sim.GetPopulationCap(playerId);

            for (int i = trainOrders.Count - 1; i >= 0; i--)
            {
                var o = trainOrders[i];
                if (currentTick >= o.ExpiryTick || o.Remaining <= 0) { trainOrders.RemoveAt(i); continue; }
                if (!TryGetTrainSpec(o.MenuType, out var bt, out int food, out int wood, out int gold))
                {
                    trainOrders.RemoveAt(i);
                    continue;
                }
                var building = GetMyBuilding(bt);
                if (building == null || building.IsUnderConstruction || building.IsDestroyed) continue;
                if (building.TrainingQueue.Count >= 8) continue;
                if (o.MenuType != 0 && pop >= popCap) continue;
                if (resources.Food - spentFood < food || resources.Wood - spentWood < wood || resources.Gold - spentGold < gold)
                    continue;

                Issue(new TrainUnitCommand(playerId, building.Id, o.MenuType));
                LlmDebug.Cmd($"AI{playerId} train unit {o.MenuType} from {bt} ({o.Remaining - 1} left)");
                spentFood += food; spentWood += wood; spentGold += gold;
                pop++; // reserve a slot so multiple orders this tick don't overcommit
                o.Remaining--;
                trainOrders[i] = o;
            }
        }

        // Maps a menu unit type to its production building and resource cost.
        private bool TryGetTrainSpec(int menuType, out BuildingType building, out int food, out int wood, out int gold)
        {
            food = 0; wood = 0; gold = 0; building = BuildingType.Barracks;
            var cfg = sim.Config;
            switch (menuType)
            {
                case 0: building = BuildingType.TownCenter; food = cfg.VillagerFoodCost; return true;
                case 1: building = BuildingType.Barracks;     GetResolvedCosts(1, out _, out food, out wood, out gold); return true;
                case 2: building = BuildingType.ArcheryRange; GetResolvedCosts(2, out _, out food, out wood, out gold); return true;
                case 3: building = BuildingType.Stables;      GetResolvedCosts(3, out _, out food, out wood, out gold); return true;
                case 4: building = BuildingType.Stables;      food = cfg.ScoutFoodCost; wood = cfg.ScoutWoodCost; return true;
                case 6: building = BuildingType.Barracks;     food = cfg.ManAtArmsFoodCost;   gold = cfg.ManAtArmsGoldCost;   return true;
                case 7: building = BuildingType.Stables;      food = cfg.KnightFoodCost;      gold = cfg.KnightGoldCost;      return true;
                case 8: building = BuildingType.ArcheryRange; food = cfg.CrossbowmanFoodCost; gold = cfg.CrossbowmanGoldCost; return true;
                case 9: building = BuildingType.Monastery;    food = cfg.MonkFoodCost;        gold = cfg.MonkGoldCost;        return true;
            }
            return false;
        }

        // Train toward a human-set production ratio: each tick train the single role
        // furthest below its target share of the current army.
        private void TrainByMix(PlayerResources resources)
        {
            int total = prodMixArchers + prodMixCavalry + prodMixInfantry;
            if (total <= 0) { TrainDefaultMix(resources); return; }

            int curA = 0, curC = 0, curI = 0;
            for (int i = 0; i < cachedCombatUnits.Count; i++)
            {
                int ut = cachedCombatUnits[i].UnitType;
                if (ut == 2 || ut == 10 || ut == 8) curA++;
                else if (ut == 3 || ut == 11 || ut == 7) curC++;
                else if (ut == 1 || ut == 12 || ut == 6) curI++;
            }
            int curTotal = Mathf.Max(1, curA + curC + curI);
            // deficit > 0 means under target (cross-multiplied to stay integer).
            int defA = prodMixArchers * curTotal - curA * total;
            int defC = prodMixCavalry * curTotal - curC * total;
            int defI = prodMixInfantry * curTotal - curI * total;

            int role = 0, best = defA;
            if (defC > best) { best = defC; role = 1; }
            if (defI > best) { best = defI; role = 2; }
            TrainRole(role, resources);
        }

        // role: 0 = archers, 1 = cavalry, 2 = infantry.
        private void TrainRole(int role, PlayerResources resources)
        {
            bool age3 = sim.GetPlayerAge(playerId) >= 3;
            switch (role)
            {
                case 0:
                    GetResolvedCosts(2, out _, out int af, out int aw, out int ag);
                    TrainFromBuilding(BuildingType.ArcheryRange, 2, af, aw, ag, resources);
                    if (age3)
                        TrainFromBuilding(BuildingType.ArcheryRange, 8, sim.Config.CrossbowmanFoodCost, 0, sim.Config.CrossbowmanGoldCost, resources);
                    break;
                case 1:
                    if (HasBuilding(BuildingType.Stables))
                    {
                        GetResolvedCosts(3, out _, out int cf, out int cw, out int cg);
                        TrainFromBuilding(BuildingType.Stables, 3, cf, cw, cg, resources);
                        if (age3)
                            TrainFromBuilding(BuildingType.Stables, 7, sim.Config.KnightFoodCost, 0, sim.Config.KnightGoldCost, resources);
                    }
                    break;
                default:
                    GetResolvedCosts(1, out _, out int sf, out int sw, out int sg);
                    TrainFromBuilding(BuildingType.Barracks, 1, sf, sw, sg, resources);
                    if (age3)
                        TrainFromBuilding(BuildingType.Barracks, 6, sim.Config.ManAtArmsFoodCost, 0, sim.Config.ManAtArmsGoldCost, resources);
                    break;
            }
        }

        private void SetAllMilitaryRally(FixedVector3 pos)
        {
            for (int i = 0; i < cachedMyBuildings.Count; i++)
            {
                var b = cachedMyBuildings[i];
                if (b.IsDestroyed || b.IsUnderConstruction) continue;
                if (b.Type == BuildingType.Barracks || b.Type == BuildingType.ArcheryRange
                    || b.Type == BuildingType.Stables || b.Type == BuildingType.Monastery)
                    Issue(new SetRallyPointCommand(playerId, b.Id, pos, -1));
            }
        }

        // Scouts (UnitType 4) are not part of the combat army, so a direct move sticks.
        private void SendScoutTo(FixedVector3 pos, int untilTick)
        {
            for (int i = 0; i < cachedMyUnits.Count; i++)
            {
                var u = cachedMyUnits[i];
                if (u.State == UnitState.Dead) continue;
                if (u.UnitType == 4)
                {
                    Issue(new MoveCommand(playerId, new int[] { u.Id }, pos));
                    return;
                }
            }
            // No scout available — queue one so the AI can scout going forward.
            AddTrainOrder(4, 1, untilTick);
        }

        private void MoveAllCombatTo(FixedVector3 pos)
        {
            GetMyCombatUnits(tempCombatUnits);
            if (tempCombatUnits.Count == 0) return;
            tempUnitIds.Clear();
            for (int i = 0; i < tempCombatUnits.Count; i++)
                tempUnitIds.Add(tempCombatUnits[i].Id);
            Issue(new MoveCommand(playerId, tempUnitIds.ToArray(), pos));
        }

        // ── Detachments (independent army sub-groups) ──────────────────────

        // Combat units NOT currently assigned to a detachment — what the main combat FSM
        // is allowed to command. detachedUnitIds is kept fresh by PruneDetachments each tick.
        private void GetUndetachedCombatUnits(List<UnitData> result)
        {
            result.Clear();
            for (int i = 0; i < cachedCombatUnits.Count; i++)
            {
                if (detachedUnitIds.Contains(cachedCombatUnits[i].Id)) continue;
                result.Add(cachedCombatUnits[i]);
            }
        }

        private static bool UnitMatchesClass(UnitData u, int classFilter)
        {
            switch (classFilter)
            {
                case 1: return u.UnitType == 2 || u.UnitType == 10; // archers
                case 2: return u.UnitType == 3 || u.UnitType == 11; // horsemen
                case 3: return u.UnitType == 1 || u.UnitType == 12; // spearmen
                default: return true;                               // all
            }
        }

        // Peel off a deterministic subset of the army into a new independent attack-moving
        // group. classFilter 0..3; portionPct 1..100 of the AVAILABLE (not-already-detached)
        // units of that class. Selection is by ascending unit id → identical on every client.
        private void CreateDetachment(int classFilter, int portionPct, FixedVector3 target, int untilTick)
        {
            RebuildDetachedSet();
            detachCandidates.Clear();
            for (int i = 0; i < cachedCombatUnits.Count; i++)
            {
                var u = cachedCombatUnits[i];
                if (detachedUnitIds.Contains(u.Id)) continue;
                if (!UnitMatchesClass(u, classFilter)) continue;
                detachCandidates.Add(u);
            }
            if (detachCandidates.Count == 0) return;

            portionPct = Mathf.Clamp(portionPct, 1, 100);
            int count = Mathf.Clamp(detachCandidates.Count * portionPct / 100, 1, detachCandidates.Count);

            var ids = new List<int>(count);
            tempUnitIds.Clear();
            for (int i = 0; i < count; i++)
            {
                int id = detachCandidates[i].Id;
                ids.Add(id);
                detachedUnitIds.Add(id);
                tempUnitIds.Add(id);
            }

            if (detachments.Count >= MaxDetachments)
                detachments.RemoveAt(0); // drop the oldest to bound complexity / command volume
            detachments.Add(new Detachment { UnitIds = ids, Target = target, UntilTick = untilTick, LastIssuedTick = -100000 });

            var cmd = new MoveCommand(playerId, tempUnitIds.ToArray(), target);
            cmd.IsAttackMove = true;
            Issue(cmd);
            LlmDebug.Cmd($"AI{playerId} detach {count} (class {classFilter}, {portionPct}%) → group, total groups {detachments.Count}");
        }

        // Drop dead/transferred units, expire finished groups (survivors rejoin the main army),
        // and re-issue each live group's attack-move periodically so it stays committed.
        private void PruneDetachments(int currentTick)
        {
            for (int i = detachments.Count - 1; i >= 0; i--)
            {
                var d = detachments[i];
                for (int j = d.UnitIds.Count - 1; j >= 0; j--)
                {
                    var u = sim.UnitRegistry.GetUnit(d.UnitIds[j]);
                    if (u == null || u.State == UnitState.Dead || u.PlayerId != playerId)
                        d.UnitIds.RemoveAt(j);
                }
                if (d.UnitIds.Count == 0 || currentTick >= d.UntilTick)
                {
                    detachments.RemoveAt(i);
                    continue;
                }
                if (currentTick - d.LastIssuedTick >= DetachmentReissueTicks)
                {
                    tempUnitIds.Clear();
                    for (int j = 0; j < d.UnitIds.Count; j++) tempUnitIds.Add(d.UnitIds[j]);
                    var cmd = new MoveCommand(playerId, tempUnitIds.ToArray(), d.Target);
                    cmd.IsAttackMove = true;
                    Issue(cmd);
                    d.LastIssuedTick = currentTick;
                    detachments[i] = d; // write back the struct's value field
                }
            }
            RebuildDetachedSet();
        }

        private void RebuildDetachedSet()
        {
            detachedUnitIds.Clear();
            for (int i = 0; i < detachments.Count; i++)
            {
                var ids = detachments[i].UnitIds;
                for (int j = 0; j < ids.Count; j++)
                    detachedUnitIds.Add(ids[j]);
            }
        }

        private void ClearDetachments()
        {
            detachments.Clear();
            detachedUnitIds.Clear();
        }

        // Autonomous initiative: when the AI has a strong army and no human combat order is
        // in effect, peel off a cavalry third to harass the enemy while the main force keeps
        // building up. Deterministic (currentTick-gated, id-ordered selection); no LLM.
        private void TickAutonomousSplit(int currentTick)
        {
            if (!enableAutonomousSplits) return;
            if (currentTick - lastAutoSplitTick < AutoSplitCooldownTicks) return;
            if (currentTick < combatHoldUntilTick) return;             // human order active → don't interfere
            if (currentTick < pingAttackUntilTick) return;             // committed to a commanded attack
            if (detachments.Count > 0) return;                         // already have a group out
            if (combatState != CombatState.Building && combatState != CombatState.Assembling) return;

            int army = cachedCombatUnits.Count;
            if (army < 2 * EffectiveAttackThreshold(currentTick)) return; // only when comfortably strong

            int cavalry = 0;
            for (int i = 0; i < cachedCombatUnits.Count; i++)
                if (UnitMatchesClass(cachedCombatUnits[i], 2)) cavalry++;
            if (cavalry < 3) return; // need a meaningful raiding party

            var target = GetEnemyTargetPosition();
            if (!target.HasValue) return;

            CreateDetachment(2, 33, target.Value, currentTick + 45 * 30); // cavalry ~third, 45s raid
            lastAutoSplitTick = currentTick;
            LlmDebug.Cmd($"AI{playerId} autonomous raid: split cavalry to harass enemy");
        }

        private void IssueResearch(TechnologyType tech)
        {
            BuildingType bt = (tech == TechnologyType.BlacksmithDamage || tech == TechnologyType.BlacksmithDefense)
                ? BuildingType.Blacksmith
                : BuildingType.University;
            var b = GetMyBuilding(bt);
            if (b == null || b.IsDestroyed || b.IsUnderConstruction) return;
            Issue(new ResearchCommand(playerId, b.Id, tech));
        }

        // ── Villager orders (commanded/auto villager tasks) ────────────────

        // Deterministically reserve up to `count` villagers, preferring those nearest `nearPos`
        // (integer tile-distance, ties by ascending id), skipping already-reserved ones.
        // Returns the chosen id list (may be shorter than count, or empty).
        private List<int> ReserveVillagers(int count, FixedVector3 nearPos)
        {
            RebuildReservedSet();
            villagerCandidates.Clear();
            for (int i = 0; i < cachedVillagers.Count; i++)
            {
                var v = cachedVillagers[i];
                if (reservedVillagerIds.Contains(v.Id)) continue;
                villagerCandidates.Add(v);
            }
            if (villagerCandidates.Count == 0) return null;

            int anchorX = nearPos.x.Raw >> Fixed32.FractionalBits;
            int anchorZ = nearPos.z.Raw >> Fixed32.FractionalBits;
            // Stable insertion sort by (distSq, id) — deterministic, small lists.
            villagerCandidates.Sort((a, b) =>
            {
                int adx = (a.SimPosition.x.Raw >> Fixed32.FractionalBits) - anchorX;
                int adz = (a.SimPosition.z.Raw >> Fixed32.FractionalBits) - anchorZ;
                int bdx = (b.SimPosition.x.Raw >> Fixed32.FractionalBits) - anchorX;
                int bdz = (b.SimPosition.z.Raw >> Fixed32.FractionalBits) - anchorZ;
                long ad = (long)adx * adx + (long)adz * adz;
                long bd = (long)bdx * bdx + (long)bdz * bdz;
                if (ad != bd) return ad < bd ? -1 : 1;
                return a.Id.CompareTo(b.Id); // deterministic tie-break
            });

            count = Mathf.Clamp(count, 1, villagerCandidates.Count);
            var ids = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                int id = villagerCandidates[i].Id;
                ids.Add(id);
                reservedVillagerIds.Add(id);
            }
            return ids;
        }

        private void AddVillagerOrder(VillagerTask task, List<int> ids, int resourceType,
            FixedVector3 target, int targetBuildingId, int untilTick, int currentTick)
        {
            if (ids == null || ids.Count == 0) return;
            if (villagerOrders.Count >= MaxVillagerOrders)
                villagerOrders.RemoveAt(0); // drop oldest
            villagerOrders.Add(new VillagerOrder
            {
                Task = task, UnitIds = ids, ResourceType = resourceType,
                Target = target, TargetBuildingId = targetBuildingId,
                UntilTick = untilTick, LastIssuedTick = -100000,
            });
            for (int i = 0; i < ids.Count; i++) reservedVillagerIds.Add(ids[i]); // reserve now (same-tick exclusion)
            IssueVillagerOrder(villagerOrders[villagerOrders.Count - 1], currentTick);
        }

        // Emit the low-level command(s) that put a villager order's units on task.
        private void IssueVillagerOrder(VillagerOrder o, int currentTick)
        {
            if (o.UnitIds.Count == 0) return;
            switch (o.Task)
            {
                case VillagerTask.Gather:
                    ServiceGatherOrder(o, currentTick);
                    break;
                case VillagerTask.Protect:
                {
                    int[] ids = o.UnitIds.ToArray();
                    if (o.TargetBuildingId >= 0) Issue(new GarrisonCommand(playerId, ids, o.TargetBuildingId));
                    else Issue(new MoveCommand(playerId, ids, o.Target));
                    break;
                }
                case VillagerTask.Repair:
                {
                    int[] ids = o.UnitIds.ToArray();
                    if (o.TargetBuildingId >= 0) Issue(new RepairBuildingCommand(playerId, ids, o.TargetBuildingId));
                    break;
                }
                case VillagerTask.Build:
                    // Build is kicked off once at order creation via the normal placement path;
                    // nothing to re-issue here (units stay reserved until the order expires).
                    break;
            }
        }

        // Drives a gather order without breaking the natural gather→deposit cycle:
        //   1. If the proper drop-off is UNDER CONSTRUCTION within 5 tiles, finish it first.
        //   2. Else ensure a deposit exists within 5 tiles (build one if missing).
        //   3. (Re)assign only IDLE/off-task villagers to gather — never interrupt one that is
        //      already gathering the right resource or carrying it back to deposit. That
        //      interruption was the bug where commanded villagers never dropped off.
        private void ServiceGatherOrder(VillagerOrder o, int currentTick)
        {
            var resType = (ResourceType)o.ResourceType;
            BuildingType dropType = DepositTypeFor(resType);

            var uc = FindDropoffUnderConstruction(dropType, o.Target, 5 * 5);
            if (uc != null)
            {
                tempUnitIds.Clear();
                for (int i = 0; i < o.UnitIds.Count; i++)
                {
                    var u = sim.UnitRegistry.GetUnit(o.UnitIds[i]);
                    if (u == null || u.State == UnitState.Dead) continue;
                    if (u.State == UnitState.Constructing && u.ConstructionTargetBuildingId == uc.Id) continue; // already on it
                    tempUnitIds.Add(u.Id);
                }
                if (tempUnitIds.Count > 0)
                    Issue(new ConstructBuildingCommand(playerId, tempUnitIds.ToArray(), uc.Id));
                return; // hold gathering until the drop-off is up
            }

            var node = FindNearestResourceNode(o.Target, resType);
            if (node != null) EnsureDropoffForGather(resType, node, currentTick);

            for (int i = 0; i < o.UnitIds.Count; i++)
            {
                var u = sim.UnitRegistry.GetUnit(o.UnitIds[i]);
                if (u == null || u.State == UnitState.Dead) continue;
                if (IsOnGatherTask(u, resType)) continue; // cycling correctly — leave it alone
                var n = FindNearestResourceNode(u.SimPosition, resType);
                if (n != null) Issue(new GatherCommand(playerId, new int[] { u.Id }, n.Id));
            }
        }

        private static BuildingType DepositTypeFor(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Food: return BuildingType.Mill;
                case ResourceType.Wood: return BuildingType.LumberYard;
                default: return BuildingType.Mine; // Gold / Stone
            }
        }

        // A villager is "on task" (don't interrupt) if it's gathering/heading to the right
        // resource, or carrying that resource back to a deposit.
        private bool IsOnGatherTask(UnitData u, ResourceType resType)
        {
            if (u.State == UnitState.MovingToDropoff || u.State == UnitState.DroppingOff)
                return u.CarriedResourceType == resType;
            if (u.State == UnitState.Gathering || u.State == UnitState.MovingToGather)
            {
                var node = sim.MapData.GetResourceNode(u.TargetResourceNodeId);
                return node != null && node.Type == resType;
            }
            return false;
        }

        private BuildingData FindDropoffUnderConstruction(BuildingType dropType, FixedVector3 target, int withinSq)
        {
            int px = target.x.Raw >> Fixed32.FractionalBits;
            int pz = target.z.Raw >> Fixed32.FractionalBits;
            BuildingData best = null; long bestD = long.MaxValue; int bestId = int.MaxValue;
            for (int i = 0; i < cachedMyBuildings.Count; i++)
            {
                var b = cachedMyBuildings[i];
                if (b.IsDestroyed || !b.IsUnderConstruction || b.Type != dropType) continue;
                int dx = b.OriginTileX - px;
                int dz = b.OriginTileZ - pz;
                long d = (long)dx * dx + (long)dz * dz;
                if (d > withinSq) continue;
                if (d < bestD || (d == bestD && b.Id < bestId)) { bestD = d; bestId = b.Id; best = b; }
            }
            return best;
        }

        // Drop dead/finished orders, release their villagers, and re-issue live ones so the
        // auto-economy never reclaims them mid-task. Runs each think-tick before TickEconomy.
        private void PruneVillagerOrders(int currentTick)
        {
            for (int i = villagerOrders.Count - 1; i >= 0; i--)
            {
                var o = villagerOrders[i];
                for (int j = o.UnitIds.Count - 1; j >= 0; j--)
                {
                    var u = sim.UnitRegistry.GetUnit(o.UnitIds[j]);
                    if (u == null || u.State == UnitState.Dead || u.PlayerId != playerId || u.UnitType != 0)
                        o.UnitIds.RemoveAt(j);
                }

                bool done = o.UnitIds.Count == 0 || currentTick >= o.UntilTick;
                if (!done && (o.Task == VillagerTask.Repair || o.Task == VillagerTask.Build) && o.TargetBuildingId >= 0)
                {
                    var b = sim.BuildingRegistry.GetBuilding(o.TargetBuildingId);
                    if (b == null || b.IsDestroyed) done = true;
                    else if (o.Task == VillagerTask.Repair && !b.IsUnderConstruction && b.CurrentHealth >= b.MaxHealth) done = true;
                    else if (o.Task == VillagerTask.Build && !b.IsUnderConstruction) done = true;
                }

                if (done)
                {
                    villagerOrders.RemoveAt(i);
                    continue;
                }

                if (currentTick - o.LastIssuedTick >= VillagerReissueTicks)
                {
                    IssueVillagerOrder(o, currentTick);
                    o.LastIssuedTick = currentTick;
                    villagerOrders[i] = o; // write back struct value field
                }
            }
            RebuildReservedSet();
        }

        private void RebuildReservedSet()
        {
            reservedVillagerIds.Clear();
            for (int i = 0; i < villagerOrders.Count; i++)
            {
                var ids = villagerOrders[i].UnitIds;
                for (int j = 0; j < ids.Count; j++)
                    reservedVillagerIds.Add(ids[j]);
            }
        }

        private void ClearVillagerOrders()
        {
            villagerOrders.Clear();
            reservedVillagerIds.Clear();
        }

        // When a commanded gather targets a resource far from any deposit, place the proper
        // drop-off building (Mill for food, Lumber Yard for wood, Mine for gold/stone) next to
        // the node so villagers can deposit locally instead of walking back to the TC.
        private void EnsureDropoffForGather(ResourceType type, ResourceNodeData node, int currentTick)
        {
            BuildingType dropType;
            int cost;
            switch (type)
            {
                case ResourceType.Food: dropType = BuildingType.Mill; cost = sim.Config.MillWoodCost; break;
                case ResourceType.Wood: dropType = BuildingType.LumberYard; cost = sim.Config.LumberYardWoodCost; break;
                case ResourceType.Gold:
                case ResourceType.Stone: dropType = BuildingType.Mine; cost = sim.Config.MineWoodCost; break;
                default: return;
            }

            var resources = sim.ResourceManager.GetPlayerResources(playerId);
            if (resources.Wood < cost) return; // can't afford it — leave them depositing at the TC

            // Distance to the nearest existing deposit for this resource (that drop-off or a TC).
            int nearestSq = int.MaxValue;
            for (int i = 0; i < cachedMyBuildings.Count; i++)
            {
                var b = cachedMyBuildings[i];
                if (b.IsDestroyed) continue;
                if (b.Type != dropType && b.Type != BuildingType.TownCenter) continue;
                int dx = node.TileX - b.OriginTileX;
                int dz = node.TileZ - b.OriginTileZ;
                int dSq = dx * dx + dz * dz;
                if (dSq < nearestSq) nearestSq = dSq;
            }

            const int needDropWithinSq = 5 * 5; // already-close-enough deposit → don't build
            if (nearestSq <= needDropWithinSq) return;

            switch (dropType)
            {
                case BuildingType.Mill:       TryPlaceBuilding(BuildingType.Mill, node.TileX, node.TileZ, currentTick, ref pendingMillTick); break;
                case BuildingType.LumberYard: TryPlaceBuilding(BuildingType.LumberYard, node.TileX, node.TileZ, currentTick, ref pendingLumberYardTick); break;
                case BuildingType.Mine:       TryPlaceBuilding(BuildingType.Mine, node.TileX, node.TileZ, currentTick, ref pendingMineTick); break;
            }
            LlmDebug.Cmd($"AI{playerId} gather: placing {dropType} drop-off near commanded {type} node");
        }

        // Nearest damaged own building to `pos`; optional type filter (0 = any). Deterministic
        // (integer tile distance, ties by ascending id).
        private BuildingData FindNearestDamagedBuilding(FixedVector3 pos, int buildingType)
        {
            int px = pos.x.Raw >> Fixed32.FractionalBits;
            int pz = pos.z.Raw >> Fixed32.FractionalBits;
            BuildingData best = null;
            long bestDist = long.MaxValue;
            int bestId = int.MaxValue;
            for (int i = 0; i < cachedMyBuildings.Count; i++)
            {
                var b = cachedMyBuildings[i];
                if (b.IsDestroyed) continue;
                if (b.CurrentHealth >= b.MaxHealth) continue;
                if (buildingType != 0 && (int)b.Type != buildingType) continue;
                int dx = (b.SimPosition.x.Raw >> Fixed32.FractionalBits) - px;
                int dz = (b.SimPosition.z.Raw >> Fixed32.FractionalBits) - pz;
                long d = (long)dx * dx + (long)dz * dz;
                if (d < bestDist || (d == bestDist && b.Id < bestId))
                {
                    bestDist = d; bestId = b.Id; best = b;
                }
            }
            return best;
        }

        private void Issue(ICommand command)
        {
            sim.AiCommandBuffer.EnqueueCommand(command);
        }
    }
}
