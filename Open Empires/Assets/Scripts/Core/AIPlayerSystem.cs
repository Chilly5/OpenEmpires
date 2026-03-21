using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public enum AIDifficulty { Easy, Medium, Hard }

    public class AIPlayerSystem
    {
        private readonly int playerId;
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
                    else if (u.UnitType != 4 && !u.IsSheep)
                        cachedCombatUnits.Add(u);
                }
                else if (!sim.AreAllies(u.PlayerId, playerId) && !u.IsSheep)
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

            TickEconomy(currentTick);

            if (useScouts)
                TickScouting(currentTick);

            militaryToggle = !militaryToggle;
            if (militaryToggle)
                TickMilitary(currentTick);

            TickDefense(currentTick);

            TickCombat(currentTick);

            // Discover enemy buildings visible to our units
            DiscoverEnemyBases();
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
            if (currentAge >= 3) return;

            int targetAge = currentAge + 1;
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
            if (vilCount < requiredVillagers) return;

            var civ = sim.GetPlayerCivilization(playerId);
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
                if (tempVillagers[i].State == UnitState.Idle && !assignedBuilderIds.Contains(tempVillagers[i].Id))
                    tempUnitIds.Add(tempVillagers[i].Id);
            }
            // Second pass: gathering villagers to fill remaining slots
            for (int i = 0; i < tempVillagers.Count && tempUnitIds.Count < count; i++)
            {
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

            if (useCounterUnits && difficulty == AIDifficulty.Hard)
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
                if (u.IsSheep) continue;

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
                        if (u.IsSheep) continue;

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

            if (tempUnitIds.Count > 0)
                Issue(new GarrisonCommand(playerId, tempUnitIds.ToArray(), tc.Id));
        }

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

            // Send scout to random target when idle
            if (scoutUnitId >= 0)
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

        private void TickCombat(int currentTick)
        {
            // Don't override defense state
            if (combatState == CombatState.Defending) return;

            GetMyCombatUnits(tempCombatUnits);
            int armySize = tempCombatUnits.Count;

            switch (combatState)
            {
                case CombatState.Building:
                    if (firstMilitaryBuildingTick < 0 && (HasBuilding(BuildingType.Barracks) || HasBuilding(BuildingType.ArcheryRange) || HasBuilding(BuildingType.Stables)))
                        firstMilitaryBuildingTick = currentTick;

                    if (armySize >= attackThreshold)
                    {
                        combatState = CombatState.Assembling;
                    }
                    else if (firstMilitaryBuildingTick > 0 && currentTick - firstMilitaryBuildingTick > 6000
                             && armySize >= attackThreshold / 2 && armySize >= 4)
                    {
                        combatState = CombatState.Assembling;
                    }
                    else if (currentTick >= 9000 && armySize >= 1)
                    {
                        combatState = CombatState.Assembling;
                    }
                    break;

                case CombatState.Assembling:
                    var targetPos = GetEnemyTargetPosition();
                    if (targetPos.HasValue)
                    {
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
                        for (int i = 0; i < tempCombatUnits.Count; i++)
                            tempUnitIds.Add(tempCombatUnits[i].Id);
                        Issue(new MoveCommand(playerId, tempUnitIds.ToArray(), stagingPos));

                        attackStartArmySize = armySize;
                        marchStartTick = currentTick;
                        combatState = CombatState.Marching;
                    }
                    else
                    {
                        combatState = CombatState.Building;
                    }
                    break;

                case CombatState.Marching:
                    // Check if any unit is within ~20 tiles of the target
                    int atkTileX = attackTargetPos.x.Raw >> Fixed32.FractionalBits;
                    int atkTileZ = attackTargetPos.z.Raw >> Fixed32.FractionalBits;
                    bool closeEnough = false;
                    for (int i = 0; i < tempCombatUnits.Count; i++)
                    {
                        int ux = tempCombatUnits[i].SimPosition.x.Raw >> Fixed32.FractionalBits;
                        int uz = tempCombatUnits[i].SimPosition.z.Raw >> Fixed32.FractionalBits;
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
                        for (int i = 0; i < tempCombatUnits.Count; i++)
                            tempUnitIds.Add(tempCombatUnits[i].Id);
                        var marchCmd = new MoveCommand(playerId, tempUnitIds.ToArray(), attackTargetPos);
                        marchCmd.IsAttackMove = true;
                        Issue(marchCmd);
                        combatState = CombatState.Attacking;
                    }
                    else if (currentTick - marchStartTick > 300)
                    {
                        // Timeout — attack-move directly to target
                        tempUnitIds.Clear();
                        for (int i = 0; i < tempCombatUnits.Count; i++)
                            tempUnitIds.Add(tempCombatUnits[i].Id);
                        var directCmd = new MoveCommand(playerId, tempUnitIds.ToArray(), attackTargetPos);
                        directCmd.IsAttackMove = true;
                        Issue(directCmd);
                        combatState = CombatState.Attacking;
                    }
                    break;

                case CombatState.Attacking:
                    // Retreat when we've lost retreatPercentInt% of our army
                    int retreatAt = Mathf.Max(1, attackStartArmySize * (100 - retreatPercentInt) / 100);
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

        private void TryPlaceBuilding(BuildingType type, int centerX, int centerZ, int currentTick, ref int pendingTick)
        {
            if (currentTick < pendingTick) return;

            // Must have an idle villager to construct
            int[] villagerIds = FindIdleVillager();
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
                if (tempVillagers[i].State == UnitState.Idle && !assignedBuilderIds.Contains(tempVillagers[i].Id))
                    return new int[] { tempVillagers[i].Id };
            }
            // Second pass: pull a gathering villager if no idle ones
            for (int i = 0; i < tempVillagers.Count; i++)
            {
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

                        if (IsAreaBuildable(tx, tz, footprintW, footprintH, border))
                            return new Vector2Int(tx, tz);
                    }
                }
            }
            return new Vector2Int(-1, -1);
        }

        private bool IsAreaBuildable(int tileX, int tileZ, int footW, int footH, int border)
        {
            for (int x = tileX - border; x < tileX + footW + border; x++)
                for (int z = tileZ - border; z < tileZ + footH + border; z++)
                    if (!sim.MapData.IsBuildable(x, z)) return false;
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

        private void Issue(ICommand command)
        {
            sim.AiCommandBuffer.EnqueueCommand(command);
        }
    }
}
