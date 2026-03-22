using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public class GameSimulation
    {
        public event Action<int> OnUnitDied;
        public event Action<int> OnBuildingDestroyed;
        public event Action<int, int, int> OnUnitTrained; // unitId, unitType, playerId
        public event Action<ProjectileData> OnProjectileCreated;
        public event Action<int> OnProjectileHit;
        public event Action<BuildingData> OnBuildingCreated;
        public event Action<int, int> OnUnitGarrisoned; // unitId, buildingId
        public event Action<int> OnUnitUngarrisoned; // unitId
        public event Action<int, int> OnSheepConverted; // sheepId, newPlayerId
        public event Action<int, int> OnSheepSlaughtered; // sheepId, carcassNodeId
        public event Action<int, FixedVector3, int> OnMeteorWarning; // playerId, position, impactTick
        public event Action<FixedVector3, List<int>> OnMeteorImpact; // position, knockedUnitIds
        public event Action<int, FixedVector3, int, int> OnHealingRainWarning; // playerId, position, startTick, endTick
        public event Action<FixedVector3> OnHealingRainEnd; // position
        public event Action<int, FixedVector3> OnLightningStormWarning; // playerId, position
        public event Action<FixedVector3, List<int>> OnLightningBolt; // boltPosition, knockedUnitIds
        public event Action<FixedVector3> OnLightningStormEnd; // position
        public event Action<int, FixedVector3, FixedVector3, int> OnTsunamiWarning; // playerId, origin, direction, impactTick
        public event Action<FixedVector3, FixedVector3, List<int>> OnTsunamiImpact; // origin, direction, hitUnitIds
        public UnitRegistry UnitRegistry { get; private set; }
        public BuildingRegistry BuildingRegistry { get; private set; }
        public ResourceManager ResourceManager { get; private set; }
        public MapData MapData { get; private set; }
        public CommandBuffer CommandBuffer { get; private set; }
        public CommandBuffer AiCommandBuffer { get; private set; }
        public FogOfWarData FogOfWar { get; private set; }
        public ProjectileRegistry ProjectileRegistry { get; private set; }

        private UnitMovementSystem movementSystem;
        private UnitSeparationSystem separationSystem;
        private UnitCombatSystem combatSystem;
        private SpatialGrid spatialGrid;
        private TowerCombatSystem towerCombatSystem;
        private ResourceGatheringSystem gatheringSystem;
        private FogOfWarSystem fogOfWarSystem;
        private BuildingTrainingSystem trainingSystem;
        private BuildingConstructionSystem constructionSystem;
        private TowerUpgradeSystem towerUpgradeSystem;
        private ProjectileSystem projectileSystem;
        private BuildingCombatSystem buildingCombatSystem;
        private SheepSystem sheepSystem;
        private UnitHealingSystem healingSystem;
        private List<AIPlayerSystem> aiPlayers = new List<AIPlayerSystem>();
        private SimulationConfig config;
        private int playerCount;
        private int[] playerTeamIds;
        public int[] PlayerTeamIds => playerTeamIds;
        private Civilization[] playerCivilizations;

        // Age progression state
        private int[] playerAges;              // all start at 1
        private bool[] playerAgingUp;          // true while landmark under construction
        private int[] playerAgingUpBuildingId; // building ID of in-progress landmark, -1 if none

        public int GetPlayerAge(int playerId)
        {
            if (playerAges != null && playerId >= 0 && playerId < playerAges.Length)
                return playerAges[playerId];
            return 1;
        }

        public bool IsPlayerAgingUp(int playerId)
        {
            if (playerAgingUp != null && playerId >= 0 && playerId < playerAgingUp.Length)
                return playerAgingUp[playerId];
            return false;
        }

        public Civilization GetPlayerCivilization(int playerId)
        {
            if (playerCivilizations != null && playerId >= 0 && playerId < playerCivilizations.Length)
                return playerCivilizations[playerId];
            return Civilization.English;
        }
        public void SetPlayerCivilizations(Civilization[] civs) { playerCivilizations = civs; }
        public BuildingType GetInfluenceBuildingType(int playerId)
        {
            return GetPlayerCivilization(playerId) == Civilization.English
                ? BuildingType.TownCenter
                : BuildingType.Mill;
        }
        public int ResolveCivUnitType(int playerId, int baseUnitType)
        {
            var civ = GetPlayerCivilization(playerId);
            return (civ, baseUnitType) switch
            {
                (Civilization.English, 2) => 10,           // Archer → Longbowman
                (Civilization.French, 3) => 11,            // Horseman → Gendarme
                (Civilization.HolyRomanEmpire, 1) => 12,   // Spearman → Landsknecht
                _ => baseUnitType,
            };
        }
        private int currentTick;
        private int nextFormationGroupId = 1;
        private int nextWallGroupId = 1;
        private List<int> dummyUnitIds = new List<int>();

        // Reusable buffers to avoid per-tick heap allocations
        private readonly List<int> expiredTeamsBuffer = new List<int>();
        private readonly List<int> tcKeysBuffer = new List<int>();
        private readonly List<UnitData> garrisonUnitsBuffer = new List<UnitData>();
        private readonly HashSet<int> reusableTeamSet1 = new HashSet<int>();
        private readonly HashSet<int> reusableTeamSet2 = new HashSet<int>();

        // Win condition tracking
        private Dictionary<int, int> firstTownCenterIds = new Dictionary<int, int>();
        public IReadOnlyDictionary<int, int> FirstTownCenterIds => firstTownCenterIds;
        public int PlayerCount => playerCount;

        private bool isMatchOver;
        private int winningTeamId = -1;
        private int matchEndTick = -1;
        public bool IsMatchOver => isMatchOver;
        public int WinningTeamId => winningTeamId;
        public int MatchEndTick => matchEndTick;
        public event Action<int> OnMatchEnded;

        // Surrender tracking
        private HashSet<int> surrenderedPlayers = new HashSet<int>();
        private HashSet<int> disconnectedPlayers = new HashSet<int>();
        public IReadOnlyCollection<int> SurrenderedPlayers => surrenderedPlayers;
        public IReadOnlyCollection<int> DisconnectedPlayers => disconnectedPlayers;

        private Dictionary<int, SurrenderVoteData> activeSurrenderVotes = new Dictionary<int, SurrenderVoteData>();
        public IReadOnlyDictionary<int, SurrenderVoteData> ActiveSurrenderVotes => activeSurrenderVotes;

        public event Action<int> OnPlayerSurrendered; // playerId
        public event Action<int, SurrenderVoteData> OnSurrenderVoteUpdated; // teamId, voteData (null = expired/cancelled)
        public event Action<int, int> OnPlayerAgedUp; // playerId, newAge

        // Meteor strike state
        private struct PendingMeteor
        {
            public int PlayerId;
            public FixedVector3 TargetPosition;
            public int ImpactTick;
        }
        private List<PendingMeteor> pendingMeteors = new List<PendingMeteor>();
        private int[] meteorCooldownTick;
        private readonly List<int> meteorKnockedBuffer = new List<int>();

        public int GetMeteorCooldownRemaining(int playerId)
        {
            if (meteorCooldownTick == null || playerId < 0 || playerId >= meteorCooldownTick.Length) return 0;
            int remaining = meteorCooldownTick[playerId] - currentTick;
            return remaining > 0 ? remaining : 0;
        }

        // Healing Rain state
        private struct PendingHealingRain
        {
            public int PlayerId;
            public FixedVector3 Center;
            public int StartTick;  // when healing begins (after warning)
            public int EndTick;    // when healing stops
        }
        private List<PendingHealingRain> pendingHealingRains = new List<PendingHealingRain>();
        private int[] healingRainCooldownTick;

        public int GetHealingRainCooldownRemaining(int playerId)
        {
            if (healingRainCooldownTick == null || playerId < 0 || playerId >= healingRainCooldownTick.Length) return 0;
            int remaining = healingRainCooldownTick[playerId] - currentTick;
            return remaining > 0 ? remaining : 0;
        }

        // Lightning Storm state
        private struct PendingLightningStorm
        {
            public int PlayerId;
            public FixedVector3 Center;
            public int FirstBoltTick;
            public int LastBoltTick;
            public int BoltsRemaining;
            public int NextBoltTick;
        }
        private List<PendingLightningStorm> pendingLightningStorms = new List<PendingLightningStorm>();
        private int[] lightningStormCooldownTick;

        public int GetLightningStormCooldownRemaining(int playerId)
        {
            if (lightningStormCooldownTick == null || playerId < 0 || playerId >= lightningStormCooldownTick.Length) return 0;
            int remaining = lightningStormCooldownTick[playerId] - currentTick;
            return remaining > 0 ? remaining : 0;
        }

        // Tsunami state
        private struct PendingTsunami
        {
            public int PlayerId;
            public FixedVector3 Origin;
            public FixedVector3 Direction; // normalized
            public int ImpactTick;
        }
        private List<PendingTsunami> pendingTsunamis = new List<PendingTsunami>();
        private int[] tsunamiCooldownTick;
        private readonly List<int> tsunamiHitBuffer = new List<int>();
        private readonly List<int> lightningKnockedBuffer = new List<int>();

        public int GetTsunamiCooldownRemaining(int playerId)
        {
            if (tsunamiCooldownTick == null || playerId < 0 || playerId >= tsunamiCooldownTick.Length) return 0;
            int remaining = tsunamiCooldownTick[playerId] - currentTick;
            return remaining > 0 ? remaining : 0;
        }

        // Deterministic PRNG for god powers (lightning bolt positions, etc.)
        private uint simRngState;

        private uint SimRngNext()
        {
            simRngState ^= simRngState << 13;
            simRngState ^= simRngState >> 17;
            simRngState ^= simRngState << 5;
            return simRngState;
        }

        // Cached Fixed32 config values — converted once at init to ensure both clients compute identical raw values
        private Fixed32 cachedTickDuration;
        private Fixed32 cachedSeparationStrength;
        private Fixed32 cachedFormationScatter;
        private Fixed32 cachedSpearmanMass;
        private Fixed32 cachedScatterThreshold;
        private Fixed32 cachedUnitRadius;
        private Fixed32 cachedProjectileSpeed;
        private Fixed32 cachedCannonRange;
        private Fixed32 cachedVisionUpgradeRangeBonus;

        // Config→Fixed32 cache: ensures identical float→fixed conversion on all clients (computed once at init)
        private Dictionary<float, Fixed32> fixedConfigCache = new Dictionary<float, Fixed32>();

        public Fixed32 ConfigToFixed32(float value)
        {
            if (!fixedConfigCache.TryGetValue(value, out var result))
            {
                result = new Fixed32((int)(value * 65536f));
                fixedConfigCache[value] = result;
            }
            return result;
        }

        // Per-system diagnostic hashes for desync debugging
        private uint[] lastSystemHashes = new uint[9];
        public uint[] LastSystemHashes => lastSystemHashes;
        public uint LastPreCmdHash { get; private set; }
        public uint LastPostCmdHash { get; private set; }
        public bool DebugSyncHashes; // enable per-system hashing (expensive — only for desync debugging)

        public string GetSystemHashDetail()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < lastSystemHashes.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(lastSystemHashes[i].ToString("X8"));
            }
            return sb.ToString();
        }

        public bool ProductionCheatActive { get; private set; }

        public int CurrentTick => currentTick;
        public SimulationConfig Config => config;

        public void LogStateBreakdown()
        {
            var units = UnitRegistry.GetAllUnits();
            uint uh = 0;
            for (int i = 0; i < units.Count; i++)
                uh = uh * 31 + (uint)units[i].Id + (uint)units[i].SimPosition.x.Raw;

            var blds = BuildingRegistry.GetAllBuildings();
            uint bh = 0;
            for (int i = 0; i < blds.Count; i++)
                bh = bh * 31 + (uint)blds[i].Id + (uint)blds[i].CurrentHealth;

            uint th = 0;
            for (int i = 0; i < playerTeamIds.Length; i++)
                th = th * 31 + (uint)playerTeamIds[i];

            // Resource node hash
            var resourceNodes = MapData.GetAllResourceNodesSorted();
            uint rnh = 0;
            for (int i = 0; i < resourceNodes.Count; i++)
            {
                rnh = rnh * 31 + (uint)resourceNodes[i].Id;
                rnh = rnh * 31 + (uint)resourceNodes[i].RemainingAmount;
            }

            // Per-player resource hash
            uint rh = 0;
            for (int p = 0; p < playerCount; p++)
            {
                var res = ResourceManager.GetPlayerResources(p);
                if (res != null)
                {
                    rh = rh * 31 + (uint)res.Food;
                    rh = rh * 31 + (uint)res.Wood;
                    rh = rh * 31 + (uint)res.Gold;
                    rh = rh * 31 + (uint)res.Stone;
                }
            }

            Debug.Log($"[SyncCheck] tick={currentTick} units={units.Count}:{uh:X8} " +
                $"blds={blds.Count}:{bh:X8} teams={th:X8} " +
                $"resNodes={resourceNodes.Count}:{rnh:X8} playerRes={rh:X8}");
        }

        public bool AreAllies(int playerA, int playerB)
        {
            return TeamHelper.AreAllies(playerTeamIds, playerA, playerB);
        }

        public void SetTeamAssignments(int[] teamIds)
        {
            playerTeamIds = teamIds;
        }

        /// <summary>
        /// Computes a checksum of the current game state for desync detection.
        /// This hash should be identical across all clients if the game is in sync.
        /// </summary>
        public uint ComputeStateChecksum()
        {
            uint hash = 17;

            // Hash current tick
            hash = hash * 31 + (uint)currentTick;

            // Hash unit state
            var units = UnitRegistry.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                hash = hash * 31 + (uint)unit.Id;
                hash = hash * 31 + (uint)unit.SimPosition.x.Raw;
                hash = hash * 31 + (uint)unit.SimPosition.z.Raw;
                hash = hash * 31 + (uint)unit.SimFacing.x.Raw;
                hash = hash * 31 + (uint)unit.SimFacing.z.Raw;
                hash = hash * 31 + (uint)unit.CurrentHealth;
                hash = hash * 31 + (uint)unit.State;
                hash = hash * 31 + (uint)unit.CombatTargetId;
                hash = hash * 31 + (uint)unit.CombatTargetBuildingId;
                hash = hash * 31 + (uint)unit.CarriedResourceType;
                hash = hash * 31 + (uint)unit.CarriedResourceAmount;
                hash = hash * 31 + (uint)unit.TargetResourceNodeId;
                hash = hash * 31 + (uint)unit.GatherTimer.Raw;
                hash = hash * 31 + (uint)unit.AttackCooldownRemaining;
                hash = hash * 31 + (uint)unit.CurrentPathIndex;
                hash = hash * 31 + (uint)(unit.Path != null ? unit.Path.Count : 0);
                hash = hash * 31 + (uint)unit.ConstructionTargetBuildingId;
                hash = hash * 31 + (uint)unit.DropOffBuildingId;
                hash = hash * 31 + (uint)unit.TargetGarrisonBuildingId;
                hash = hash * 31 + (unit.IsCharging ? 1u : 0u);
                hash = hash * 31 + (uint)unit.ChargeCooldownRemaining;
                hash = hash * 31 + (uint)unit.ChargeStunRemaining;
                hash = hash * 31 + (unit.IsPatrolling ? 1u : 0u);
                hash = hash * 31 + (uint)unit.PatrolCurrentIndex;
                hash = hash * 31 + (unit.PatrolForward ? 1u : 0u);
                hash = hash * 31 + (uint)(unit.PatrolWaypoints != null ? unit.PatrolWaypoints.Count : 0);
                hash = hash * 31 + (unit.PlayerCommanded ? 1u : 0u);
                hash = hash * 31 + (unit.IsAttackMoving ? 1u : 0u);
                hash = hash * 31 + (unit.HasLeash ? 1u : 0u);
                hash = hash * 31 + (unit.HasSavedPath ? 1u : 0u);
                hash = hash * 31 + (uint)unit.ChaseBlockedTicks;
            }

            // Hash building state
            var buildings = BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                hash = hash * 31 + (uint)building.Id;
                hash = hash * 31 + (uint)building.CurrentHealth;
                hash = hash * 31 + (building.IsDestroyed ? 1u : 0u);
                hash = hash * 31 + (building.IsUnderConstruction ? 1u : 0u);
                hash = hash * 31 + (building.IsGate ? 1u : 0u);
                hash = hash * 31 + (uint)building.ConstructionTicksRemaining;
                hash = hash * 31 + (uint)building.TrainingTicksRemaining;
                hash = hash * 31 + (uint)building.TrainingQueue.Count;
                for (int q = 0; q < building.TrainingQueue.Count; q++)
                    hash = hash * 31 + (uint)building.TrainingQueue[q];
                hash = hash * 31 + (uint)building.GarrisonCount;
                for (int g = 0; g < building.GarrisonCount; g++)
                    hash = hash * 31 + (uint)building.GarrisonedUnitIds[g];
            }

            // Hash resource node state
            var resourceNodes = MapData.GetAllResourceNodesSorted();
            for (int i = 0; i < resourceNodes.Count; i++)
            {
                var node = resourceNodes[i];
                hash = hash * 31 + (uint)node.Id;
                hash = hash * 31 + (uint)node.RemainingAmount;
                hash = hash * 31 + (node.IsDepleted ? 1u : 0u);
            }

            // Hash resources
            for (int playerId = 0; playerId < playerCount; playerId++)
            {
                var res = ResourceManager.GetPlayerResources(playerId);
                if (res != null)
                {
                    hash = hash * 31 + (uint)res.Food;
                    hash = hash * 31 + (uint)res.Wood;
                    hash = hash * 31 + (uint)res.Gold;
                    hash = hash * 31 + (uint)res.Stone;
                }
            }

            // Hash team assignments
            for (int i = 0; i < playerTeamIds.Length; i++)
                hash = hash * 31 + (uint)playerTeamIds[i];

            // Hash age progression state
            if (playerAges != null)
            {
                for (int i = 0; i < playerAges.Length; i++)
                {
                    hash = hash * 31 + (uint)playerAges[i];
                    hash = hash * 31 + (playerAgingUp[i] ? 1u : 0u);
                    hash = hash * 31 + (uint)(playerAgingUpBuildingId[i] + 1);
                }
            }

            // Hash win condition state
            hash = hash * 31 + (isMatchOver ? 1u : 0u);
            hash = hash * 31 + (uint)(winningTeamId + 1); // +1 so -1 maps to 0
            // Sort keys to ensure deterministic iteration order across clients
            tcKeysBuffer.Clear();
            tcKeysBuffer.AddRange(firstTownCenterIds.Keys);
            tcKeysBuffer.Sort();
            for (int i = 0; i < tcKeysBuffer.Count; i++)
            {
                hash = hash * 31 + (uint)tcKeysBuffer[i];
                hash = hash * 31 + (uint)firstTownCenterIds[tcKeysBuffer[i]];
            }

            // Hash cheat state
            hash = hash * 31 + (ProductionCheatActive ? 1u : 0u);

            // Hash meteor state
            for (int i = 0; i < pendingMeteors.Count; i++)
            {
                hash = hash * 31 + (uint)pendingMeteors[i].ImpactTick;
                hash = hash * 31 + (uint)pendingMeteors[i].TargetPosition.x.Raw;
                hash = hash * 31 + (uint)pendingMeteors[i].TargetPosition.z.Raw;
            }
            if (meteorCooldownTick != null)
            {
                for (int i = 0; i < meteorCooldownTick.Length; i++)
                    hash = hash * 31 + (uint)meteorCooldownTick[i];
            }

            // Hash healing rain state
            for (int i = 0; i < pendingHealingRains.Count; i++)
            {
                hash = hash * 31 + (uint)pendingHealingRains[i].StartTick;
                hash = hash * 31 + (uint)pendingHealingRains[i].EndTick;
                hash = hash * 31 + (uint)pendingHealingRains[i].Center.x.Raw;
                hash = hash * 31 + (uint)pendingHealingRains[i].Center.z.Raw;
            }
            if (healingRainCooldownTick != null)
                for (int i = 0; i < healingRainCooldownTick.Length; i++)
                    hash = hash * 31 + (uint)healingRainCooldownTick[i];

            // Hash lightning storm state
            for (int i = 0; i < pendingLightningStorms.Count; i++)
            {
                hash = hash * 31 + (uint)pendingLightningStorms[i].BoltsRemaining;
                hash = hash * 31 + (uint)pendingLightningStorms[i].NextBoltTick;
                hash = hash * 31 + (uint)pendingLightningStorms[i].Center.x.Raw;
                hash = hash * 31 + (uint)pendingLightningStorms[i].Center.z.Raw;
            }
            if (lightningStormCooldownTick != null)
                for (int i = 0; i < lightningStormCooldownTick.Length; i++)
                    hash = hash * 31 + (uint)lightningStormCooldownTick[i];

            // Hash tsunami state
            for (int i = 0; i < pendingTsunamis.Count; i++)
            {
                hash = hash * 31 + (uint)pendingTsunamis[i].ImpactTick;
                hash = hash * 31 + (uint)pendingTsunamis[i].Origin.x.Raw;
                hash = hash * 31 + (uint)pendingTsunamis[i].Origin.z.Raw;
            }
            if (tsunamiCooldownTick != null)
                for (int i = 0; i < tsunamiCooldownTick.Length; i++)
                    hash = hash * 31 + (uint)tsunamiCooldownTick[i];

            // Hash sim RNG state
            hash = hash * 31 + simRngState;

            return hash;
        }

        private uint ComputeQuickHash()
        {
            uint hash = 17;
            var units = UnitRegistry.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                hash = hash * 31 + (uint)units[i].Id;
                hash = hash * 31 + (uint)units[i].SimPosition.x.Raw;
                hash = hash * 31 + (uint)units[i].SimPosition.z.Raw;
                hash = hash * 31 + (uint)units[i].State;
            }
            return hash;
        }

        public GameSimulation(SimulationConfig config, int playerCount = 2, int[] teamAssignments = null, int[] aiPlayerIds = null, AIDifficulty aiDifficulty = AIDifficulty.Medium)
        {
            this.config = config;
            this.playerCount = playerCount;
            int mapSize = SimulationConfig.GetMapSize(playerCount);
            UnitRegistry = new UnitRegistry();
            BuildingRegistry = new BuildingRegistry();
            ResourceManager = new ResourceManager();
            MapData = new MapData(mapSize, mapSize);
            var (tiles, heights, basePositions, forestDensity) = MapGenerator.Generate(mapSize, mapSize, config.MapSeed, config.WaterThreshold, playerCount, teamAssignments);
            MapData.ApplyGenerationResult(tiles, heights, forestDensity);
            MapData.BasePositions = basePositions;
            CommandBuffer = new CommandBuffer();
            AiCommandBuffer = new CommandBuffer();
            movementSystem = new UnitMovementSystem();
            separationSystem = new UnitSeparationSystem();
            combatSystem = new UnitCombatSystem();
            spatialGrid = new SpatialGrid(Fixed32.FromInt(4));
            towerCombatSystem = new TowerCombatSystem();
            gatheringSystem = new ResourceGatheringSystem();
            fogOfWarSystem = new FogOfWarSystem();
            trainingSystem = new BuildingTrainingSystem();
            constructionSystem = new BuildingConstructionSystem();
            towerUpgradeSystem = new TowerUpgradeSystem();
            ProjectileRegistry = new ProjectileRegistry();
            projectileSystem = new ProjectileSystem();
            buildingCombatSystem = new BuildingCombatSystem();
            sheepSystem = new SheepSystem();
            healingSystem = new UnitHealingSystem();
            FogOfWar = new FogOfWarData(mapSize, mapSize, playerCount);
            FogOfWar.SetMapData(MapData);
            if (teamAssignments != null)
            {
                playerTeamIds = teamAssignments;
            }
            else
            {
                playerTeamIds = new int[playerCount];
                for (int i = 0; i < playerCount; i++)
                    playerTeamIds[i] = i; // FFA: each player is own team
            }
            currentTick = 0;
            meteorCooldownTick = new int[playerCount];
            healingRainCooldownTick = new int[playerCount];
            lightningStormCooldownTick = new int[playerCount];
            tsunamiCooldownTick = new int[playerCount];
            simRngState = (uint)(config.MapSeed + 42);
            if (simRngState == 0) simRngState = 1;

            // Age progression
            playerAges = new int[playerCount];
            playerAgingUp = new bool[playerCount];
            playerAgingUpBuildingId = new int[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                playerAges[i] = 1;
                playerAgingUp[i] = false;
                playerAgingUpBuildingId[i] = -1;
            }

            GridPathfinder.Initialize(MapData.Width, MapData.Height);
            MapData.ComputeHoleMap();

            // Validate config to prevent fixed-point division by zero
            Debug.Assert(config.SpearmanMass > 1f, "spearmanMass must be > 1 (villager mass) to avoid division by zero in scatter formula");
            // Cache Fixed32 conversions once — use explicit raw values for constants,
            // ConfigToFixed32 for config-driven values (ensures identical conversion on all clients)
            cachedTickDuration = new Fixed32(2184);        // 1/30 * 65536 ≈ 2184
            cachedSeparationStrength = ConfigToFixed32(config.SeparationStrength);
            cachedFormationScatter = ConfigToFixed32(config.FormationScatter);
            cachedSpearmanMass = ConfigToFixed32(config.SpearmanMass);
            cachedScatterThreshold = new Fixed32(655);     // 0.01 * 65536 = 655
            cachedUnitRadius = ConfigToFixed32(config.UnitRadius);
            cachedProjectileSpeed = ConfigToFixed32(config.ProjectileSpeed);
            cachedCannonRange = ConfigToFixed32(config.CannonRange);
            cachedVisionUpgradeRangeBonus = ConfigToFixed32(config.VisionUpgradeRangeBonus);

            // Create AI players
            if (aiPlayerIds != null)
            {
                for (int i = 0; i < aiPlayerIds.Length; i++)
                    aiPlayers.Add(new AIPlayerSystem(aiPlayerIds[i], this, aiDifficulty));
            }
            else if (teamAssignments == null && playerCount >= 2)
            {
                // Single-player default: all non-zero players are AI
                for (int i = 1; i < playerCount; i++)
                    aiPlayers.Add(new AIPlayerSystem(i, this, aiDifficulty));
            }
        }

        /// <summary>Single-player tick: flushes CommandBuffer internally.</summary>
        public void Tick()
        {
            if (isMatchOver) return;

            foreach (var unit in UnitRegistry.GetAllUnits())
                unit.StorePreviousPosition();

            var commands = CommandBuffer.FlushCommands();
            foreach (var command in commands)
                ProcessCommand(command);

            // AI players enqueue their commands, then we process them
            TickAIPlayers();
            var aiCommands = AiCommandBuffer.FlushCommands();
            foreach (var command in aiCommands)
                ProcessCommand(command);

            RunSystems();
            currentTick++;

            if (currentTick <= 100)
            {
                uint tickChecksum = ComputeStateChecksum();
                Debug.Log($"[SyncCheck] tick={currentTick} checksum={tickChecksum:X8}");
            }
        }

        /// <summary>Multiplayer tick: accepts an explicit, pre-sorted command list. AI runs deterministically on all clients.</summary>
        public void Tick(List<ICommand> commands)
        {
            if (isMatchOver) return;

            foreach (var unit in UnitRegistry.GetAllUnits())
                unit.StorePreviousPosition();

            bool earlyTick = currentTick < 100;
            if (earlyTick || DebugSyncHashes)
            {
                LastPreCmdHash = ComputeQuickHash();
                if (earlyTick)
                    Debug.Log($"[SyncCheck] tick={currentTick} PRE-CMD={LastPreCmdHash:X8} cmds={commands.Count}");
            }

            foreach (var command in commands)
                ProcessCommand(command);

            if (earlyTick || DebugSyncHashes)
            {
                LastPostCmdHash = ComputeQuickHash();
                if (earlyTick)
                    Debug.Log($"[SyncCheck] tick={currentTick} POST-CMD={LastPostCmdHash:X8}");
            }

            // AI players run deterministically on all clients — no network traffic needed
            TickAIPlayers();
            var aiCommands = AiCommandBuffer.FlushCommands();

            if (earlyTick)
            {
                uint preAiHash = ComputeQuickHash();
                Debug.Log($"[SyncCheck] tick={currentTick} PRE-AI-EXEC={preAiHash:X8} aiCmds={aiCommands.Count}");
            }

            foreach (var command in aiCommands)
                ProcessCommand(command);

            RunSystems();
            currentTick++;

            if (currentTick <= 2)
            {
                uint initChecksum = ComputeStateChecksum();
                Debug.LogWarning($"[SyncCheck] INIT tick={currentTick} checksum={initChecksum:X8} systemDetail={GetSystemHashDetail()}");
            }
            else if (currentTick <= 100)
            {
                uint tickChecksum = ComputeStateChecksum();
                Debug.Log($"[SyncCheck] tick={currentTick} checksum={tickChecksum:X8}");
            }
        }

        private void TickAIPlayers()
        {
            for (int i = 0; i < aiPlayers.Count; i++)
                aiPlayers[i].Tick(currentTick);
        }

        private void RunSystems()
        {
            // Build spatial grid (needed by separation and combat)
            spatialGrid.Build(UnitRegistry.GetAllUnits());
            bool hashSystems = DebugSyncHashes || currentTick <= 100;
            if (hashSystems) lastSystemHashes[0] = ComputeQuickHash(); // baseline

            sheepSystem.Tick(UnitRegistry, BuildingRegistry, spatialGrid, MapData, cachedTickDuration, currentTick, playerTeamIds, config,
                (sheepId, pid) => OnSheepConverted?.Invoke(sheepId, pid));

            movementSystem.Tick(UnitRegistry, MapData, cachedTickDuration);
            ProcessSlaughterArrivals();
            ProcessArrivedGarrisonUnits();
            ProcessPatrolArrivals();
            UpdateIdleTimers();
            if (hashSystems) lastSystemHashes[1] = ComputeQuickHash(); // after movement + garrison/patrol/idle

            var (deadUnitIds, deadBuildingIds) = combatSystem.Tick(UnitRegistry, BuildingRegistry, currentTick, cachedTickDuration, MapData, ProjectileRegistry, config, playerTeamIds, spatialGrid, cachedProjectileSpeed);
            if (hashSystems) lastSystemHashes[2] = ComputeQuickHash(); // placeholder (kept for index alignment)

            // Separation after combat so it has last word on positions
            separationSystem.Tick(UnitRegistry, MapData, cachedSeparationStrength, spatialGrid, playerTeamIds);

            // Meteor strike impacts
            ProcessPendingMeteors();
            // God power impacts
            ProcessPendingHealingRains();
            ProcessPendingLightningStorms();
            ProcessPendingTsunamis();
            if (hashSystems) lastSystemHashes[3] = ComputeQuickHash(); // after combat

            // Tower combat system
            towerCombatSystem.Tick(BuildingRegistry, UnitRegistry, ProjectileRegistry, config, currentTick, playerTeamIds, cachedProjectileSpeed, spatialGrid);
            if (hashSystems) lastSystemHashes[4] = ComputeQuickHash(); // after separation

            // Building combat (TC arrow slits)
            buildingCombatSystem.Tick(BuildingRegistry, UnitRegistry, ProjectileRegistry, config, currentTick, cachedTickDuration, playerTeamIds, cachedProjectileSpeed, spatialGrid);
            if (hashSystems) lastSystemHashes[5] = ComputeQuickHash(); // after tower + building combat

            // Fire events for newly created projectiles
            var newProjectiles = ProjectileRegistry.FlushNewlyCreated();
            for (int i = 0; i < newProjectiles.Count; i++)
                OnProjectileCreated?.Invoke(newProjectiles[i]);

            // Projectile system tick
            var (hitProjIds, projDeadUnitIds, projDeadBuildingIds) = projectileSystem.Tick(ProjectileRegistry, UnitRegistry, BuildingRegistry, cachedTickDuration, currentTick);
            for (int i = 0; i < hitProjIds.Count; i++)
                OnProjectileHit?.Invoke(hitProjIds[i]);
            for (int i = 0; i < projDeadUnitIds.Count; i++)
            {
                int deadId = projDeadUnitIds[i];
                UnitRegistry.RemoveUnit(deadId);
                OnUnitDied?.Invoke(deadId);
            }
            for (int i = 0; i < projDeadBuildingIds.Count; i++)
                CleanUpDestroyedBuilding(projDeadBuildingIds[i]);

            for (int i = 0; i < deadUnitIds.Count; i++)
            {
                int deadId = deadUnitIds[i];
                UnitRegistry.RemoveUnit(deadId);
                OnUnitDied?.Invoke(deadId);
            }
            for (int i = 0; i < deadBuildingIds.Count; i++)
                CleanUpDestroyedBuilding(deadBuildingIds[i]);
            if (hashSystems) lastSystemHashes[6] = ComputeQuickHash(); // after projectile + death cleanup

            CheckSurrenderVoteTimeout();
            CheckWinCondition();
            if (isMatchOver) return;

            gatheringSystem.Tick(UnitRegistry, MapData, ResourceManager, BuildingRegistry, config, cachedTickDuration, currentTick, GetInfluenceBuildingType);
            healingSystem.Tick(UnitRegistry, config, spatialGrid, playerTeamIds, currentTick, MapData, BuildingRegistry);
            if (hashSystems) lastSystemHashes[7] = ComputeQuickHash(); // after gathering

            // Construction system
            var completedBuildingIds = constructionSystem.Tick(UnitRegistry, BuildingRegistry, MapData, currentTick, cachedTickDuration, out var idledVillagers, out var startedBuildingIds);

            // When construction first starts on a template, mark tiles non-walkable and eject units
            for (int i = 0; i < startedBuildingIds.Count; i++)
            {
                var startedBuilding = BuildingRegistry.GetBuilding(startedBuildingIds[i]);
                if (startedBuilding != null)
                {
                    MapData.MarkBuildingTiles(startedBuilding.OriginTileX, startedBuilding.OriginTileZ,
                        startedBuilding.TileFootprintWidth, startedBuilding.TileFootprintHeight);
                    EjectUnitsFromBuildingFootprint(startedBuilding);
                }
            }

            for (int i = 0; i < completedBuildingIds.Count; i++)
            {
                var completedBuilding = BuildingRegistry.GetBuilding(completedBuildingIds[i]);
                if (completedBuilding == null) continue;

                if (completedBuilding.IsGate)
                {
                    MapData.ClearFoundationBorder(completedBuilding.OriginTileX, completedBuilding.OriginTileZ,
                        completedBuilding.TileFootprintWidth, completedBuilding.TileFootprintHeight, completedBuilding.FoundationBorder);
                    MapData.ClearBuildingTiles(completedBuilding.OriginTileX, completedBuilding.OriginTileZ,
                        completedBuilding.TileFootprintWidth, completedBuilding.TileFootprintHeight);
                }

                if (completedBuilding.Type == BuildingType.Farm)
                {
                    var farmNode = MapData.AddFarmResourceNode(ResourceType.Food,
                        completedBuilding.SimPosition, int.MaxValue);
                    farmNode.LinkedBuildingId = completedBuilding.Id;
                    completedBuilding.LinkedResourceNodeId = farmNode.Id;
                    MapData.MarkFarmTiles(completedBuilding.OriginTileX, completedBuilding.OriginTileZ,
                        completedBuilding.TileFootprintWidth, completedBuilding.TileFootprintHeight);
                }

                if (completedBuilding.Type == BuildingType.Landmark)
                {
                    int pid = completedBuilding.PlayerId;
                    var def = LandmarkDefinitions.Get(completedBuilding.LandmarkId);
                    playerAges[pid] = def.TargetAge;
                    playerAgingUp[pid] = false;
                    playerAgingUpBuildingId[pid] = -1;
                    OnPlayerAgedUp?.Invoke(pid, def.TargetAge);
                }

                // Eject any units that ended up on the building footprint during construction
                // (the construction chase logic can move villagers onto non-walkable tiles)
                EjectUnitsFromBuildingFootprint(completedBuilding);
            }
            AutoTaskFarmBuilders(idledVillagers);
            AutoSeekNearbyGathering(idledVillagers);
            AutoSeekNearbyConstruction(idledVillagers);

            // Tower upgrade system
            towerUpgradeSystem.Tick(BuildingRegistry, config, cachedCannonRange, cachedVisionUpgradeRangeBonus);

            // Training system (units freeze at 99% when pop-capped)
            var completions = trainingSystem.Tick(BuildingRegistry, config,
                (playerId, pending) => GetPopulation(playerId) + pending < GetPopulationCap(playerId),
                ProductionCheatActive);
            for (int i = 0; i < completions.Count; i++)
            {
                var c = completions[i];
                var building = BuildingRegistry.GetBuilding(c.BuildingId);
                if (building == null) continue;

                SpawnTrainedUnit(building, c.UnitType, c.PlayerId);
            }

            // Only update fog every 3rd tick (visual feature, 100ms delay imperceptible)
            if (currentTick % 3 == 0)
                fogOfWarSystem.Tick(FogOfWar, UnitRegistry, BuildingRegistry, MapData, playerCount, playerTeamIds, config);

            ProcessWaypointQueues();
            if (hashSystems) lastSystemHashes[8] = ComputeQuickHash(); // after construction + training + fog
        }

        private void UpdateIdleTimers()
        {
            var units = UnitRegistry.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (!unit.IsVillager) continue;
                
                if (unit.State == UnitState.Idle)
                {
                    unit.IdleTimer += cachedTickDuration;
                }
                else
                {
                    unit.IdleTimer = Fixed32.Zero;
                }
            }
        }

        private void ProcessWaypointQueues()
        {
            var units = UnitRegistry.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit.State == UnitState.Idle && unit.HasQueuedCommands)
                    PopAndExecuteNextQueuedCommand(unit);
            }
        }

        private void CheckWinCondition()
        {
            if (isMatchOver) return;

            // Surrender-based check (runs even with < 2 TCs, e.g. singleplayer)
            if (surrenderedPlayers.Count > 0)
            {
                reusableTeamSet1.Clear();
                HashSet<int> nonSurrenderedTeams = reusableTeamSet1;
                for (int pid = 0; pid < playerCount; pid++)
                {
                    if (surrenderedPlayers.Contains(pid)) continue;
                    int teamId = (pid < playerTeamIds.Length) ? playerTeamIds[pid] : pid;
                    nonSurrenderedTeams.Add(teamId);
                }

                if (nonSurrenderedTeams.Count <= 1)
                {
                    isMatchOver = true;
                    matchEndTick = currentTick;
                    if (nonSurrenderedTeams.Count == 1)
                    {
                        foreach (int teamId in nonSurrenderedTeams)
                            winningTeamId = teamId;
                    }
                    else
                    {
                        winningTeamId = -1; // all surrendered — no winner
                    }
                    OnMatchEnded?.Invoke(winningTeamId);
                    return;
                }
            }

            if (firstTownCenterIds.Count < 2) return;

            // A team is "alive" if it has at least one non-surrendered player AND at least one surviving TC
            // Sort keys for deterministic iteration order across clients
            reusableTeamSet2.Clear();
            HashSet<int> aliveTeams = reusableTeamSet2;
            tcKeysBuffer.Clear();
            tcKeysBuffer.AddRange(firstTownCenterIds.Keys);
            tcKeysBuffer.Sort();
            for (int i = 0; i < tcKeysBuffer.Count; i++)
            {
                int playerId = tcKeysBuffer[i];
                int tcId = firstTownCenterIds[playerId];

                // Skip surrendered players — their TC doesn't keep the team alive
                if (surrenderedPlayers.Contains(playerId)) continue;

                var tc = BuildingRegistry.GetBuilding(tcId);
                if (tc != null)
                {
                    int teamId = (playerId < playerTeamIds.Length) ? playerTeamIds[playerId] : playerId;
                    aliveTeams.Add(teamId);
                }
            }

            if (aliveTeams.Count > 1) return; // game continues

            isMatchOver = true;
            matchEndTick = currentTick;

            if (aliveTeams.Count == 1)
            {
                foreach (int teamId in aliveTeams)
                    winningTeamId = teamId;
            }
            else
            {
                winningTeamId = -1; // draw
            }

            OnMatchEnded?.Invoke(winningTeamId);
        }

        // ========== SURRENDER SYSTEM ==========

        public class SurrenderVoteData
        {
            public int InitiatorPlayerId;
            public int StartTick;
            public HashSet<int> YesVotes = new HashSet<int>();
            public HashSet<int> NoVotes = new HashSet<int>();
            public int TeamId;
        }

        private void ProcessSurrenderVoteCommand(SurrenderVoteCommand cmd)
        {
            if (surrenderedPlayers.Contains(cmd.PlayerId)) return;

            int teamId = (cmd.PlayerId < playerTeamIds.Length) ? playerTeamIds[cmd.PlayerId] : cmd.PlayerId;
            int teammateCount = GetTeammateCount(teamId);

            // 1v1 or solo on team: immediate surrender
            if (teammateCount <= 1)
            {
                SurrenderPlayer(cmd.PlayerId);
                return;
            }

            // Team game
            if (!activeSurrenderVotes.TryGetValue(teamId, out var vote))
            {
                // No active vote — only create one on a yes vote
                if (!cmd.VoteYes) return;

                vote = new SurrenderVoteData
                {
                    InitiatorPlayerId = cmd.PlayerId,
                    StartTick = currentTick,
                    TeamId = teamId
                };
                vote.YesVotes.Add(cmd.PlayerId);

                // Count disconnected teammates as automatic yes votes
                for (int pid = 0; pid < playerCount; pid++)
                {
                    if (pid == cmd.PlayerId) continue;
                    int pidTeam = (pid < playerTeamIds.Length) ? playerTeamIds[pid] : pid;
                    if (pidTeam == teamId && disconnectedPlayers.Contains(pid))
                        vote.YesVotes.Add(pid);
                }

                activeSurrenderVotes[teamId] = vote;
            }
            else
            {
                // Existing vote — record this player's vote
                if (cmd.VoteYes)
                {
                    vote.YesVotes.Add(cmd.PlayerId);
                    vote.NoVotes.Remove(cmd.PlayerId);
                }
                else
                {
                    vote.NoVotes.Add(cmd.PlayerId);
                    vote.YesVotes.Remove(cmd.PlayerId);
                }
            }

            // Check majority
            int needed = (teammateCount / 2) + 1;
            if (vote.YesVotes.Count >= needed)
            {
                // Surrender all non-surrendered players on this team
                OnSurrenderVoteUpdated?.Invoke(teamId, vote);
                for (int pid = 0; pid < playerCount; pid++)
                {
                    int pidTeam = (pid < playerTeamIds.Length) ? playerTeamIds[pid] : pid;
                    if (pidTeam == teamId && !surrenderedPlayers.Contains(pid))
                        SurrenderPlayer(pid);
                }
                activeSurrenderVotes.Remove(teamId);
                return;
            }

            // Check if it's impossible to reach majority (too many no votes)
            int possibleYes = teammateCount - vote.NoVotes.Count;
            if (possibleYes < needed)
            {
                // Vote failed
                activeSurrenderVotes.Remove(teamId);
                OnSurrenderVoteUpdated?.Invoke(teamId, null);
                return;
            }

            OnSurrenderVoteUpdated?.Invoke(teamId, vote);
        }

        private void SurrenderPlayer(int playerId)
        {
            if (!surrenderedPlayers.Add(playerId)) return;
            OnPlayerSurrendered?.Invoke(playerId);
        }

        public void MarkPlayerDisconnected(int playerId)
        {
            if (!disconnectedPlayers.Add(playerId)) return;
            SurrenderPlayer(playerId);

            // If there's an active surrender vote for their team, add as yes vote and re-evaluate
            int teamId = (playerId < playerTeamIds.Length) ? playerTeamIds[playerId] : playerId;
            if (activeSurrenderVotes.TryGetValue(teamId, out var vote))
            {
                vote.YesVotes.Add(playerId);
                vote.NoVotes.Remove(playerId);

                int teammateCount = GetTeammateCount(teamId);
                int needed = (teammateCount / 2) + 1;
                if (vote.YesVotes.Count >= needed)
                {
                    OnSurrenderVoteUpdated?.Invoke(teamId, vote);
                    for (int pid = 0; pid < playerCount; pid++)
                    {
                        int pidTeam = (pid < playerTeamIds.Length) ? playerTeamIds[pid] : pid;
                        if (pidTeam == teamId && !surrenderedPlayers.Contains(pid))
                            SurrenderPlayer(pid);
                    }
                    activeSurrenderVotes.Remove(teamId);
                }
                else
                {
                    OnSurrenderVoteUpdated?.Invoke(teamId, vote);
                }
            }
        }

        private void CheckSurrenderVoteTimeout()
        {
            // 60 seconds at 30 ticks/sec = 1800 ticks
            const int VoteTimeoutTicks = 1800;

            expiredTeamsBuffer.Clear();
            foreach (var kvp in activeSurrenderVotes)
            {
                if (currentTick - kvp.Value.StartTick > VoteTimeoutTicks)
                    expiredTeamsBuffer.Add(kvp.Key);
            }
            expiredTeamsBuffer.Sort(); // deterministic removal order

            for (int i = 0; i < expiredTeamsBuffer.Count; i++)
            {
                activeSurrenderVotes.Remove(expiredTeamsBuffer[i]);
                OnSurrenderVoteUpdated?.Invoke(expiredTeamsBuffer[i], null);
            }
        }

        private int GetTeammateCount(int teamId)
        {
            int count = 0;
            for (int pid = 0; pid < playerCount; pid++)
            {
                int pidTeam = (pid < playerTeamIds.Length) ? playerTeamIds[pid] : pid;
                if (pidTeam == teamId) count++;
            }
            return count;
        }

        private void PopAndExecuteNextQueuedCommand(UnitData unit)
        {
            while (unit.HasQueuedCommands)
            {
                var qc = unit.DequeueCommand();

                if (qc.Type == QueuedCommandType.Construct)
                {
                    var building = BuildingRegistry.GetBuilding(qc.BuildingId);
                    if (building == null || building.IsDestroyed || !building.IsUnderConstruction)
                        continue; // skip finished/destroyed, try next

                    unit.ClearSavedPath();
                    unit.ClearFormation();
                    unit.CombatTargetId = -1;
                    unit.CombatTargetBuildingId = -1;
                    unit.TargetResourceNodeId = -1;
                    unit.ConstructionTargetBuildingId = building.Id;
                    unit.GatherTimer = Fixed32.Zero;
                    unit.PlayerCommanded = true;
                    unit.IsAttackMoving = false;

                    // Build occupiedTiles from other units already heading to this building
                    var occupiedTiles = new HashSet<Vector2Int>();
                    var allUnits = UnitRegistry.GetAllUnits();
                    for (int u = 0; u < allUnits.Count; u++)
                    {
                        var other = allUnits[u];
                        if (other == unit || other.State == UnitState.Dead) continue;
                        if (other.ConstructionTargetBuildingId != building.Id) continue;
                        if (other.State == UnitState.MovingToBuild || other.State == UnitState.Constructing)
                            occupiedTiles.Add(MapData.WorldToTile(other.FinalDestination));
                    }

                    Vector2Int adjTile = FindNearestWalkableAdjacentTile(building, unit.SimPosition, occupiedTiles);
                    Vector2Int startTile = MapData.WorldToTile(unit.SimPosition);
                    var path = GridPathfinder.FindPath(MapData, startTile, adjTile, unit.PlayerId, BuildingRegistry);
                    if (path.Count > 0)
                    {
                        unit.SetPath(path);
                        unit.FinalDestination = MapData.TileToWorldFixed(adjTile.x, adjTile.y);
                        unit.State = UnitState.MovingToBuild;
                    }
                    else
                    {
                        unit.State = UnitState.Constructing;
                    }
                    return;
                }
                else if (qc.Type == QueuedCommandType.Gather)
                {
                    var node = MapData.GetResourceNode(qc.ResourceNodeId);
                    if (node == null || node.IsDepleted)
                        continue; // skip depleted, try next

                    unit.ClearSavedPath();
                    unit.ClearFormation();
                    unit.CombatTargetId = -1;
                    unit.CombatTargetBuildingId = -1;
                    unit.ConstructionTargetBuildingId = -1;
                    unit.TargetResourceNodeId = qc.ResourceNodeId;
                    unit.GatherTimer = Fixed32.Zero;
                    unit.PlayerCommanded = true;
                    unit.IsAttackMoving = false;

                    // Build occupiedTiles from other units already heading to this node
                    var occupiedTiles = new HashSet<Vector2Int>();
                    var allUnits = UnitRegistry.GetAllUnits();
                    for (int u = 0; u < allUnits.Count; u++)
                    {
                        var other = allUnits[u];
                        if (other == unit || other.State == UnitState.Dead) continue;
                        if (other.TargetResourceNodeId != qc.ResourceNodeId) continue;
                        if (other.State == UnitState.MovingToGather || other.State == UnitState.Gathering)
                            occupiedTiles.Add(MapData.WorldToTile(other.FinalDestination));
                    }

                    Vector2Int nodeOrigin = new Vector2Int(node.TileX, node.TileZ);
                    Vector2Int adjTile = FindNearestWalkableAdjacentTileForResource(nodeOrigin, node.FootprintWidth, node.FootprintHeight, unit.SimPosition, occupiedTiles);
                    Vector2Int startTile = MapData.WorldToTile(unit.SimPosition);
                    var path = GridPathfinder.FindPath(MapData, startTile, adjTile, unit.PlayerId, BuildingRegistry);
                    if (path.Count > 0)
                    {
                        unit.SetPath(path);
                        unit.FinalDestination = MapData.TileToWorldFixed(adjTile.x, adjTile.y);
                        unit.State = UnitState.MovingToGather;
                    }
                    else
                    {
                        unit.State = UnitState.Gathering;
                    }
                    return;
                }
                else if (qc.Type == QueuedCommandType.DropOff)
                {
                    var building = BuildingRegistry.GetBuilding(qc.BuildingId);
                    if (building == null || building.IsDestroyed || building.IsUnderConstruction)
                        continue; // skip destroyed/incomplete, try next
                    if (!IsDropOffBuilding(building.Type))
                        continue;

                    if (unit.CarriedResourceAmount <= 0)
                        continue; // nothing to drop off, skip

                    unit.ClearSavedPath();
                    unit.ClearFormation();
                    unit.CombatTargetId = -1;
                    unit.CombatTargetBuildingId = -1;
                    unit.ConstructionTargetBuildingId = -1;
                    unit.GatherTimer = Fixed32.Zero;
                    unit.DropOffBuildingId = building.Id;
                    unit.PlayerCommanded = true;

                    Vector2Int adjTile = FindNearestWalkableAdjacentTile(building, unit.SimPosition);
                    Vector2Int startTile = MapData.WorldToTile(unit.SimPosition);
                    var path = GridPathfinder.FindPath(MapData, startTile, adjTile, unit.PlayerId, BuildingRegistry);
                    if (path.Count > 0)
                    {
                        unit.SetPath(path);
                        unit.FinalDestination = MapData.TileToWorldFixed(adjTile.x, adjTile.y);
                        unit.State = UnitState.MovingToDropoff;
                    }
                    else
                    {
                        unit.State = UnitState.DroppingOff;
                    }
                    return;
                }
                else if (qc.Type == QueuedCommandType.Slaughter)
                {
                    int sheepId = qc.ResourceNodeId; // reused field
                    var sheep = UnitRegistry.GetUnit(sheepId);
                    if (sheep == null || sheep.State == UnitState.Dead || !sheep.IsSheep)
                        continue;

                    unit.ClearSavedPath();
                    unit.ClearFormation();
                    unit.CombatTargetId = sheepId;
                    unit.CombatTargetBuildingId = -1;
                    unit.TargetResourceNodeId = -1;
                    unit.ConstructionTargetBuildingId = -1;
                    unit.GatherTimer = Fixed32.Zero;
                    unit.PlayerCommanded = true;

                    Vector2Int startTile = MapData.WorldToTile(unit.SimPosition);
                    Vector2Int goalTile = MapData.WorldToTile(sheep.SimPosition);
                    var path = GridPathfinder.FindPath(MapData, startTile, goalTile, unit.PlayerId, BuildingRegistry);
                    if (path.Count > 0)
                    {
                        unit.SetPath(path);
                        unit.FinalDestination = sheep.SimPosition;
                        unit.State = UnitState.MovingToSlaughter;
                    }
                    else
                    {
                        unit.State = UnitState.Idle;
                    }
                    return;
                }
                else if (qc.Type == QueuedCommandType.Patrol)
                {
                    // Start fresh patrol from current position to queued target
                    unit.ClearSavedPath();
                    unit.ClearFormation();
                    unit.CombatTargetId = -1;
                    unit.CombatTargetBuildingId = -1;
                    unit.TargetResourceNodeId = -1;
                    unit.ConstructionTargetBuildingId = -1;
                    unit.DropOffBuildingId = -1;
                    unit.TargetGarrisonBuildingId = -1;
                    unit.GatherTimer = Fixed32.Zero;
                    unit.PlayerCommanded = false; // false so combat system auto-aggros (attack-move behaviour)
                    unit.IsAttackMoving = false;

                    unit.ClearPatrol();
                    unit.PatrolWaypoints.Add(unit.SimPosition);
                    unit.PatrolWaypoints.Add(qc.TargetPosition);
                    unit.PatrolCurrentIndex = 0;
                    unit.PatrolForward = true;
                    unit.IsPatrolling = true;

                    Vector2Int startTile = MapData.WorldToTile(unit.SimPosition);
                    Vector2Int goalTile = MapData.WorldToTile(qc.TargetPosition);
                    var path = GridPathfinder.FindPath(MapData, startTile, goalTile, unit.PlayerId, BuildingRegistry);
                    if (path.Count > 0)
                    {
                        unit.SetPath(path);
                        unit.FinalDestination = new FixedVector3(qc.TargetPosition.x, Fixed32.Zero, qc.TargetPosition.z);
                        unit.State = UnitState.Moving;
                    }
                    return;
                }
                else
                {
                    // Move or AttackMove
                    bool isAttackMove = qc.Type == QueuedCommandType.AttackMove;

                    Vector2Int startTile = MapData.WorldToTile(unit.SimPosition);
                    Vector2Int goalTile = MapData.WorldToTile(qc.TargetPosition);
                    var path = GridPathfinder.FindPath(MapData, startTile, goalTile, unit.PlayerId, BuildingRegistry);
                    if (path.Count > 0)
                    {
                        unit.SetPath(path);
                        unit.FinalDestination = new FixedVector3(qc.TargetPosition.x, Fixed32.Zero, qc.TargetPosition.z);
                        unit.State = UnitState.Moving;
                        unit.HasTargetFacing = qc.HasFacing;
                        unit.TargetFacing = qc.FacingDirection;
                        unit.PlayerCommanded = !isAttackMove;
                        unit.IsAttackMoving = isAttackMove;
                        unit.TargetResourceNodeId = -1;
                        unit.ConstructionTargetBuildingId = -1;
                        unit.GatherTimer = Fixed32.Zero;
                        unit.ClearSavedPath();
                        unit.CombatTargetId = -1;
                        unit.CombatTargetBuildingId = -1;
                    }
                    return;
                }
            }
        }

        private void ProcessCommand(ICommand command)
        {
            switch (command)
            {
                case MoveCommand move:
                    ProcessMoveCommand(move);
                    break;
                case GatherCommand gather:
                    ProcessGatherCommand(gather);
                    break;
                case StopCommand stop:
                    ProcessStopCommand(stop);
                    break;
                case AttackBuildingCommand attackBuilding:
                    ProcessAttackBuildingCommand(attackBuilding);
                    break;
                case AttackUnitCommand attackUnit:
                    ProcessAttackUnitCommand(attackUnit);
                    break;
                case TrainUnitCommand train:
                    ProcessTrainUnitCommand(train);
                    break;
                case SetRallyPointCommand rally:
                    ProcessSetRallyPointCommand(rally);
                    break;
                case PlaceBuildingCommand place:
                    ProcessPlaceBuildingCommand(place);
                    break;
                case ConstructBuildingCommand construct:
                    ProcessConstructBuildingCommand(construct);
                    break;
                case DropOffCommand dropOff:
                    ProcessDropOffCommand(dropOff);
                    break;
                case PlaceWallCommand placeWall:
                    ProcessPlaceWallCommand(placeWall);
                    break;
                case ConvertToGateCommand convertToGate:
                    ProcessConvertToGateCommand(convertToGate);
                    break;
                case CancelTrainCommand cancelTrain:
                    ProcessCancelTrainCommand(cancelTrain);
                    break;
                case UpgradeTowerCommand upgradeTower:
                    ProcessUpgradeTowerCommand(upgradeTower);
                    break;
                case CancelUpgradeCommand cancelUpgrade:
                    ProcessCancelUpgradeCommand(cancelUpgrade);
                    break;
                case GarrisonCommand garrison:
                    ProcessGarrisonCommand(garrison);
                    break;
                case UngarrisonCommand ungarrison:
                    ProcessUngarrisonCommand(ungarrison);
                    break;
                case PatrolCommand patrol:
                    ProcessPatrolCommand(patrol);
                    break;
                case CheatResourceCommand cheatRes:
                    ProcessCheatResourceCommand(cheatRes);
                    break;
                case CheatProductionCommand cheatProd:
                    ProcessCheatProductionCommand(cheatProd);
                    break;
                case CheatVisionCommand cheatVis:
                    ProcessCheatVisionCommand(cheatVis);
                    break;
                case DeleteUnitsCommand deleteUnits:
                    ProcessDeleteUnitsCommand(deleteUnits);
                    break;
                case DeleteBuildingCommand deleteBuilding:
                    ProcessDeleteBuildingCommand(deleteBuilding);
                    break;
                case SurrenderVoteCommand surrender:
                    ProcessSurrenderVoteCommand(surrender);
                    break;
                case SlaughterSheepCommand slaughter:
                    ProcessSlaughterSheepCommand(slaughter);
                    break;
                case FollowUnitCommand follow:
                    ProcessFollowUnitCommand(follow);
                    break;
                case HealUnitCommand heal:
                    ProcessHealUnitCommand(heal);
                    break;
                case MeteorStrikeCommand meteor:
                    ProcessMeteorStrikeCommand(meteor);
                    break;
                case HealingRainCommand healingRain:
                    ProcessHealingRainCommand(healingRain);
                    break;
                case LightningStormCommand lightningStorm:
                    ProcessLightningStormCommand(lightningStorm);
                    break;
                case TsunamiCommand tsunami:
                    ProcessTsunamiCommand(tsunami);
                    break;
            }
        }

        public void SpawnNeutralSheep()
        {
            var rng = new System.Random(config.MapSeed + 7777);
            int totalSheep = config.SheepPerPlayer * playerCount;
            int margin = 15;
            int mapW = MapData.Width;
            int mapH = MapData.Height;

            for (int i = 0; i < totalSheep; i++)
            {
                for (int attempt = 0; attempt < 50; attempt++)
                {
                    int tx = rng.Next(margin, mapW - margin);
                    int tz = rng.Next(margin, mapH - margin);

                    if (!MapData.IsWalkable(tx, tz)) continue;

                    FixedVector3 pos = MapData.TileToWorldFixed(tx, tz);
                    var sheep = UnitRegistry.CreateUnit(UnitData.NeutralPlayerId, pos,
                        Fixed32.FromFloat(config.SheepMoveSpeed),
                        Fixed32.FromFloat(config.SheepRadius),
                        Fixed32.FromFloat(config.SheepMass));
                    sheep.UnitType = 5;
                    sheep.IsSheep = true;
                    sheep.MaxHealth = config.SheepMaxHealth;
                    sheep.CurrentHealth = sheep.MaxHealth;
                    sheep.AttackDamage = 0;
                    sheep.AttackRange = Fixed32.Zero;
                    sheep.AttackCooldownTicks = 999;
                    sheep.DetectionRange = Fixed32.FromFloat(2f);
                    sheep.SpawnPosition = pos;
                    break;
                }
            }
        }

        private void ProcessSlaughterSheepCommand(SlaughterSheepCommand cmd)
        {
            var sheep = UnitRegistry.GetUnit(cmd.SheepUnitId);
            if (sheep == null || sheep.State == UnitState.Dead || !sheep.IsSheep) return;
            if (sheep.PlayerId != cmd.PlayerId) return; // can only slaughter own sheep

            for (int i = 0; i < cmd.VillagerIds.Length; i++)
            {
                var unit = UnitRegistry.GetUnit(cmd.VillagerIds[i]);
                if (unit == null || unit.State == UnitState.Dead) continue;
                if (!unit.IsVillager) continue;
                if (unit.PlayerId != cmd.PlayerId) continue;

                if (cmd.IsQueued)
                {
                    var qc = QueuedCommand.SlaughterWaypoint(sheep.SimPosition, cmd.SheepUnitId);
                    unit.CommandQueue.Add(qc);
                    if (unit.State == UnitState.Idle)
                        PopAndExecuteNextQueuedCommand(unit);
                    continue;
                }

                unit.ClearCommandQueue();
                unit.ClearSavedPath();
                unit.ClearFormation();
                unit.ClearPatrol();
                unit.CombatTargetId = cmd.SheepUnitId;
                unit.CombatTargetBuildingId = -1;
                unit.TargetResourceNodeId = -1;
                unit.ConstructionTargetBuildingId = -1;
                unit.DropOffBuildingId = -1;
                unit.TargetGarrisonBuildingId = -1;
                unit.GatherTimer = Fixed32.Zero;
                unit.PlayerCommanded = true;

                Vector2Int startTile = MapData.WorldToTile(unit.SimPosition);
                Vector2Int goalTile = MapData.WorldToTile(sheep.SimPosition);
                var path = GridPathfinder.FindPath(MapData, startTile, goalTile, unit.PlayerId, BuildingRegistry);
                if (path.Count > 0)
                {
                    unit.SetPath(path);
                    unit.FinalDestination = sheep.SimPosition;
                    unit.State = UnitState.MovingToSlaughter;
                }
            }
        }

        private void ProcessFollowUnitCommand(FollowUnitCommand cmd)
        {
            var target = UnitRegistry.GetUnit(cmd.TargetUnitId);
            if (target == null || target.State == UnitState.Dead) return;
            if (target.UnitType != 4) return; // must be a scout
            if (!AreAllies(target.PlayerId, cmd.PlayerId)) return;

            for (int i = 0; i < cmd.UnitIds.Length; i++)
            {
                var unit = UnitRegistry.GetUnit(cmd.UnitIds[i]);
                if (unit == null || unit.State == UnitState.Dead) continue;
                if (!unit.IsSheep) continue;
                if (!AreAllies(unit.PlayerId, cmd.PlayerId) && unit.PlayerId != UnitData.NeutralPlayerId) continue;

                unit.ClearCommandQueue();
                unit.ClearSavedPath();
                unit.FollowTargetId = cmd.TargetUnitId;
                unit.State = UnitState.Following;
                unit.PlayerId = cmd.PlayerId;
            }
        }

        private void ProcessHealUnitCommand(HealUnitCommand cmd)
        {
            var target = UnitRegistry.GetUnit(cmd.TargetUnitId);
            if (target == null || target.State == UnitState.Dead) return;
            if (!AreAllies(target.PlayerId, cmd.PlayerId)) return;

            for (int i = 0; i < cmd.UnitIds.Length; i++)
            {
                var unit = UnitRegistry.GetUnit(cmd.UnitIds[i]);
                if (unit == null || unit.State == UnitState.Dead) continue;
                if (!unit.IsHealer) continue;
                if (unit.PlayerId != cmd.PlayerId) continue;

                unit.HealTargetId = cmd.TargetUnitId;
                unit.PlayerCommanded = true;
                unit.ClearSavedPath();

                // Path to target
                var targetPos = target.SimPosition;
                var path = GridPathfinder.FindPath(MapData,
                    MapData.WorldToTile(unit.SimPosition),
                    MapData.WorldToTile(targetPos), unit.PlayerId, BuildingRegistry);
                if (path != null)
                {
                    unit.SetPath(path);
                    unit.FinalDestination = targetPos;
                    unit.State = UnitState.Moving;
                }
            }
        }

        private void ProcessHealingRainCommand(HealingRainCommand cmd)
        {
            if (cmd.PlayerId < 0 || cmd.PlayerId >= playerCount) return;
            if (currentTick < healingRainCooldownTick[cmd.PlayerId]) return;

            if (FogOfWar.GetVisibility(cmd.PlayerId, cmd.TargetTileX, cmd.TargetTileZ) != TileVisibility.Visible) return;

            var targetPos = MapData.TileToWorldFixed(cmd.TargetTileX, cmd.TargetTileZ);
            healingRainCooldownTick[cmd.PlayerId] = currentTick + config.HealingRainCooldownTicks;

            int startTick = currentTick + config.HealingRainWarningTicks;
            int endTick = startTick + config.HealingRainDurationTicks;
            pendingHealingRains.Add(new PendingHealingRain
            {
                PlayerId = cmd.PlayerId,
                Center = targetPos,
                StartTick = startTick,
                EndTick = endTick
            });

            OnHealingRainWarning?.Invoke(cmd.PlayerId, targetPos, startTick, endTick);
        }

        private void ProcessPendingHealingRains()
        {
            for (int i = pendingHealingRains.Count - 1; i >= 0; i--)
            {
                var rain = pendingHealingRains[i];

                // Still in warning phase
                if (currentTick < rain.StartTick) continue;

                // Expired
                if (currentTick >= rain.EndTick)
                {
                    pendingHealingRains.RemoveAt(i);
                    OnHealingRainEnd?.Invoke(rain.Center);
                    continue;
                }

                // Active: heal friendly units in radius
                var radiusFixed = ConfigToFixed32(config.HealingRainRadius);
                var radiusSq = radiusFixed * radiusFixed;
                int healAmount = config.HealingRainHealPerTick;

                var units = UnitRegistry.GetAllUnits();
                for (int u = 0; u < units.Count; u++)
                {
                    var unit = units[u];
                    if (unit.State == UnitState.Dead) continue;
                    if (!AreAllies(unit.PlayerId, rain.PlayerId)) continue;

                    var diff = unit.SimPosition - rain.Center;
                    diff = new FixedVector3(diff.x, Fixed32.Zero, diff.z);
                    var distSq = diff.x * diff.x + diff.z * diff.z;
                    if (distSq > radiusSq) continue;

                    if (unit.CurrentHealth < unit.MaxHealth)
                    {
                        unit.CurrentHealth += healAmount;
                        if (unit.CurrentHealth > unit.MaxHealth)
                            unit.CurrentHealth = unit.MaxHealth;
                    }
                }
            }
        }

        private void ProcessLightningStormCommand(LightningStormCommand cmd)
        {
            if (cmd.PlayerId < 0 || cmd.PlayerId >= playerCount) return;
            if (currentTick < lightningStormCooldownTick[cmd.PlayerId]) return;

            if (FogOfWar.GetVisibility(cmd.PlayerId, cmd.TargetTileX, cmd.TargetTileZ) != TileVisibility.Visible) return;

            var targetPos = MapData.TileToWorldFixed(cmd.TargetTileX, cmd.TargetTileZ);
            lightningStormCooldownTick[cmd.PlayerId] = currentTick + config.LightningStormCooldownTicks;

            int firstBoltTick = currentTick + config.LightningStormWarningTicks;
            int boltInterval = config.LightningStormDurationTicks / config.LightningStormBoltCount;
            if (boltInterval < 1) boltInterval = 1;

            pendingLightningStorms.Add(new PendingLightningStorm
            {
                PlayerId = cmd.PlayerId,
                Center = targetPos,
                FirstBoltTick = firstBoltTick,
                LastBoltTick = firstBoltTick + config.LightningStormDurationTicks,
                BoltsRemaining = config.LightningStormBoltCount,
                NextBoltTick = firstBoltTick
            });

            OnLightningStormWarning?.Invoke(cmd.PlayerId, targetPos);
        }

        private void ProcessPendingLightningStorms()
        {
            for (int i = pendingLightningStorms.Count - 1; i >= 0; i--)
            {
                var storm = pendingLightningStorms[i];

                if (currentTick < storm.FirstBoltTick) continue;

                if (storm.BoltsRemaining <= 0)
                {
                    pendingLightningStorms.RemoveAt(i);
                    OnLightningStormEnd?.Invoke(storm.Center);
                    continue;
                }

                if (currentTick >= storm.NextBoltTick)
                {
                    // Use deterministic RNG to pick bolt offset within storm radius
                    var radiusFixed = ConfigToFixed32(config.LightningStormRadius);
                    uint rx = SimRngNext();
                    uint rz = SimRngNext();
                    // Map to range [-radius, +radius]
                    int rawRadius = radiusFixed.Raw;
                    int offsetX = (int)(rx % (uint)(rawRadius * 2)) - rawRadius;
                    int offsetZ = (int)(rz % (uint)(rawRadius * 2)) - rawRadius;

                    var boltPos = new FixedVector3(
                        storm.Center.x + new Fixed32(offsetX),
                        storm.Center.y,
                        storm.Center.z + new Fixed32(offsetZ));

                    // Apply damage + knockback to units within bolt radius
                    var boltRadiusFixed = ConfigToFixed32(config.LightningStormBoltRadius);
                    var boltRadiusSq = boltRadiusFixed * boltRadiusFixed;
                    var boltKnockbackDist = ConfigToFixed32(config.LightningStormBoltKnockbackDist);
                    int allyDamage = (int)(config.LightningStormBoltDamage * config.MeteorAllyDamageMultiplier);

                    lightningKnockedBuffer.Clear();

                    var units = UnitRegistry.GetAllUnits();
                    for (int u = units.Count - 1; u >= 0; u--)
                    {
                        var unit = units[u];
                        if (unit.State == UnitState.Dead) continue;

                        var diff = unit.SimPosition - boltPos;
                        diff = new FixedVector3(diff.x, Fixed32.Zero, diff.z);
                        var distSq = diff.x * diff.x + diff.z * diff.z;
                        if (distSq > boltRadiusSq) continue;

                        bool isAlly = AreAllies(unit.PlayerId, storm.PlayerId);
                        int damage = isAlly ? allyDamage : config.LightningStormBoltDamage;

                        unit.CurrentHealth -= damage;
                        if (unit.CurrentHealth <= 0)
                        {
                            UnitRegistry.RemoveUnit(unit.Id);
                            OnUnitDied?.Invoke(unit.Id);
                            continue;
                        }

                        // Knockback away from bolt center
                        var dist = Fixed32.Sqrt(distSq);
                        if (dist.Raw > 0)
                        {
                            var direction = new FixedVector3(
                                diff.x / dist, Fixed32.Zero, diff.z / dist);
                            var falloff = Fixed32.One - (dist / boltRadiusFixed);
                            if (falloff.Raw < 0) falloff = Fixed32.Zero;

                            var displacement = new FixedVector3(
                                direction.x * boltKnockbackDist * falloff,
                                Fixed32.Zero,
                                direction.z * boltKnockbackDist * falloff);

                            unit.SimPosition = unit.SimPosition + displacement;

                            // Wall-slide clamp
                            Vector2Int tile = MapData.WorldToTile(unit.SimPosition);
                            if (!MapData.IsWalkable(tile.x, tile.y))
                            {
                                var slideX = new FixedVector3(unit.SimPosition.x, unit.SimPosition.y, unit.PreviousSimPosition.z);
                                Vector2Int tileX = MapData.WorldToTile(slideX);
                                if (MapData.IsWalkable(tileX.x, tileX.y))
                                    unit.SimPosition = slideX;
                                else
                                {
                                    var slideZ = new FixedVector3(unit.PreviousSimPosition.x, unit.SimPosition.y, unit.SimPosition.z);
                                    Vector2Int tileZ = MapData.WorldToTile(slideZ);
                                    if (MapData.IsWalkable(tileZ.x, tileZ.y))
                                        unit.SimPosition = slideZ;
                                    else
                                        unit.SimPosition = unit.PreviousSimPosition;
                                }
                            }
                        }

                        unit.ClearSavedPath();
                        unit.State = UnitState.Idle;
                        unit.CombatTargetId = -1;
                        unit.CombatTargetBuildingId = -1;
                        lightningKnockedBuffer.Add(unit.Id);
                    }

                    OnLightningBolt?.Invoke(boltPos, new List<int>(lightningKnockedBuffer));

                    // Update storm state
                    int boltInterval = config.LightningStormDurationTicks / config.LightningStormBoltCount;
                    if (boltInterval < 1) boltInterval = 1;
                    storm.BoltsRemaining--;
                    storm.NextBoltTick = currentTick + boltInterval;
                    pendingLightningStorms[i] = storm;
                }
            }
        }

        private void ProcessTsunamiCommand(TsunamiCommand cmd)
        {
            if (cmd.PlayerId < 0 || cmd.PlayerId >= playerCount) return;
            if (currentTick < tsunamiCooldownTick[cmd.PlayerId]) return;

            if (FogOfWar.GetVisibility(cmd.PlayerId, cmd.TargetTileX, cmd.TargetTileZ) != TileVisibility.Visible) return;

            var origin = MapData.TileToWorldFixed(cmd.TargetTileX, cmd.TargetTileZ);
            var direction = new FixedVector3(new Fixed32(cmd.DirectionX), Fixed32.Zero, new Fixed32(cmd.DirectionZ));
            direction = direction.Normalized();

            tsunamiCooldownTick[cmd.PlayerId] = currentTick + config.TsunamiCooldownTicks;

            int impactTick = currentTick + config.TsunamiWarningTicks;
            pendingTsunamis.Add(new PendingTsunami
            {
                PlayerId = cmd.PlayerId,
                Origin = origin,
                Direction = direction,
                ImpactTick = impactTick
            });

            OnTsunamiWarning?.Invoke(cmd.PlayerId, origin, direction, impactTick);
        }

        private void ProcessPendingTsunamis()
        {
            for (int i = pendingTsunamis.Count - 1; i >= 0; i--)
            {
                var tsunami = pendingTsunamis[i];
                if (currentTick < tsunami.ImpactTick) continue;

                pendingTsunamis.RemoveAt(i);

                var lengthFixed = ConfigToFixed32(config.TsunamiLength);
                var halfWidthFixed = ConfigToFixed32(config.TsunamiWidth / 2f);
                var pushDistFixed = ConfigToFixed32(config.TsunamiPushDist);

                // Perpendicular direction (rotate 90 degrees in XZ plane)
                var perpDir = new FixedVector3(-tsunami.Direction.z, Fixed32.Zero, tsunami.Direction.x);

                tsunamiHitBuffer.Clear();

                var units = UnitRegistry.GetAllUnits();
                for (int u = units.Count - 1; u >= 0; u--)
                {
                    var unit = units[u];
                    if (unit.State == UnitState.Dead) continue;

                    var diff = unit.SimPosition - tsunami.Origin;
                    diff = new FixedVector3(diff.x, Fixed32.Zero, diff.z);

                    // Project onto wave direction (along component)
                    var along = FixedVector3.Dot2D(diff, tsunami.Direction);
                    if (along.Raw < 0 || along > lengthFixed) continue;

                    // Project onto perpendicular (across component)
                    var across = FixedVector3.Dot2D(diff, perpDir);
                    if (across.Raw < 0) across = new Fixed32(-across.Raw);
                    if (across > halfWidthFixed) continue;

                    // Unit is hit
                    unit.CurrentHealth -= config.TsunamiDamage;

                    if (unit.CurrentHealth <= 0)
                    {
                        UnitRegistry.RemoveUnit(unit.Id);
                        OnUnitDied?.Invoke(unit.Id);
                        continue;
                    }

                    // Push unit in wave direction
                    var displacement = new FixedVector3(
                        tsunami.Direction.x * pushDistFixed,
                        Fixed32.Zero,
                        tsunami.Direction.z * pushDistFixed);

                    unit.SimPosition = unit.SimPosition + displacement;

                    // Wall-slide clamp to walkable
                    Vector2Int tile = MapData.WorldToTile(unit.SimPosition);
                    if (!MapData.IsWalkable(tile.x, tile.y))
                    {
                        var slideX = new FixedVector3(unit.SimPosition.x, unit.SimPosition.y, unit.PreviousSimPosition.z);
                        Vector2Int tileX = MapData.WorldToTile(slideX);
                        if (MapData.IsWalkable(tileX.x, tileX.y))
                        {
                            unit.SimPosition = slideX;
                        }
                        else
                        {
                            var slideZ = new FixedVector3(unit.PreviousSimPosition.x, unit.SimPosition.y, unit.SimPosition.z);
                            Vector2Int tileZ = MapData.WorldToTile(slideZ);
                            if (MapData.IsWalkable(tileZ.x, tileZ.y))
                                unit.SimPosition = slideZ;
                            else
                                unit.SimPosition = unit.PreviousSimPosition;
                        }
                    }

                    unit.ClearSavedPath();
                    unit.State = UnitState.Idle;
                    unit.CombatTargetId = -1;
                    unit.CombatTargetBuildingId = -1;
                    tsunamiHitBuffer.Add(unit.Id);
                }

                OnTsunamiImpact?.Invoke(tsunami.Origin, tsunami.Direction, new List<int>(tsunamiHitBuffer));
            }
        }

        private void ProcessMeteorStrikeCommand(MeteorStrikeCommand cmd)
        {
            if (cmd.PlayerId < 0 || cmd.PlayerId >= playerCount) return;
            if (currentTick < meteorCooldownTick[cmd.PlayerId]) return;

            // Validate visibility
            if (FogOfWar.GetVisibility(cmd.PlayerId, cmd.TargetTileX, cmd.TargetTileZ) != TileVisibility.Visible)
                return;

            var targetPos = MapData.TileToWorldFixed(cmd.TargetTileX, cmd.TargetTileZ);
            meteorCooldownTick[cmd.PlayerId] = currentTick + config.MeteorCooldownTicks;

            int impactTick = currentTick + config.MeteorWarningTicks;
            pendingMeteors.Add(new PendingMeteor
            {
                PlayerId = cmd.PlayerId,
                TargetPosition = targetPos,
                ImpactTick = impactTick
            });

            OnMeteorWarning?.Invoke(cmd.PlayerId, targetPos, impactTick);
        }

        private void ProcessPendingMeteors()
        {
            for (int m = pendingMeteors.Count - 1; m >= 0; m--)
            {
                var meteor = pendingMeteors[m];
                if (currentTick < meteor.ImpactTick) continue;

                pendingMeteors.RemoveAt(m);

                var center = meteor.TargetPosition;
                var radiusFixed = ConfigToFixed32(config.MeteorRadius);
                var radiusSq = radiusFixed * radiusFixed;
                var knockbackDistFixed = ConfigToFixed32(config.MeteorKnockbackDist);
                int allyDamage = (int)(config.MeteorDamage * config.MeteorAllyDamageMultiplier);

                meteorKnockedBuffer.Clear();

                // Unit damage + knockback
                var units = UnitRegistry.GetAllUnits();
                for (int i = units.Count - 1; i >= 0; i--)
                {
                    var unit = units[i];
                    if (unit.State == UnitState.Dead) continue;

                    var diff = unit.SimPosition - center;
                    diff = new FixedVector3(diff.x, Fixed32.Zero, diff.z); // ignore Y
                    var distSq = diff.x * diff.x + diff.z * diff.z;

                    if (distSq > radiusSq) continue;

                    bool isAlly = AreAllies(unit.PlayerId, meteor.PlayerId);
                    int damage = isAlly ? allyDamage : config.MeteorDamage;

                    unit.CurrentHealth -= damage;

                    if (unit.CurrentHealth <= 0)
                    {
                        UnitRegistry.RemoveUnit(unit.Id);
                        OnUnitDied?.Invoke(unit.Id);
                        continue;
                    }

                    // Knockback
                    var dist = Fixed32.Sqrt(distSq);
                    if (dist.Raw > 0)
                    {
                        var direction = new FixedVector3(
                            diff.x / dist,
                            Fixed32.Zero,
                            diff.z / dist);
                        var falloff = Fixed32.One - (dist / radiusFixed);
                        if (falloff.Raw < 0) falloff = Fixed32.Zero;

                        var displacement = new FixedVector3(
                            direction.x * knockbackDistFixed * falloff,
                            Fixed32.Zero,
                            direction.z * knockbackDistFixed * falloff);

                        unit.SimPosition = unit.SimPosition + displacement;

                        // Wall-slide clamp to walkable
                        Vector2Int tile = MapData.WorldToTile(unit.SimPosition);
                        if (!MapData.IsWalkable(tile.x, tile.y))
                        {
                            var slideX = new FixedVector3(unit.SimPosition.x, unit.SimPosition.y, unit.PreviousSimPosition.z);
                            Vector2Int tileX = MapData.WorldToTile(slideX);
                            if (MapData.IsWalkable(tileX.x, tileX.y))
                            {
                                unit.SimPosition = slideX;
                            }
                            else
                            {
                                var slideZ = new FixedVector3(unit.PreviousSimPosition.x, unit.SimPosition.y, unit.SimPosition.z);
                                Vector2Int tileZ = MapData.WorldToTile(slideZ);
                                if (MapData.IsWalkable(tileZ.x, tileZ.y))
                                    unit.SimPosition = slideZ;
                                else
                                    unit.SimPosition = unit.PreviousSimPosition;
                            }
                        }
                    }

                    unit.ClearSavedPath();
                    unit.State = UnitState.Idle;
                    unit.CombatTargetId = -1;
                    unit.CombatTargetBuildingId = -1;
                    meteorKnockedBuffer.Add(unit.Id);
                }

                // Building damage
                var buildings = BuildingRegistry.GetAllBuildings();
                for (int i = buildings.Count - 1; i >= 0; i--)
                {
                    var building = buildings[i];
                    var bDiff = building.SimPosition - center;
                    var bDistSq = bDiff.x * bDiff.x + bDiff.z * bDiff.z;
                    if (bDistSq > radiusSq) continue;

                    building.CurrentHealth -= config.MeteorBuildingDamage;
                    if (building.CurrentHealth <= 0)
                        CleanUpDestroyedBuilding(building.Id);
                }

                OnMeteorImpact?.Invoke(center, new List<int>(meteorKnockedBuffer));
            }
        }

        private void ProcessSlaughterArrivals()
        {
            var units = UnitRegistry.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (!unit.IsVillager) continue;
                if (unit.State != UnitState.Idle) continue;
                if (unit.CombatTargetId < 0) continue;

                var sheep = UnitRegistry.GetUnit(unit.CombatTargetId);
                if (sheep == null || !sheep.IsSheep)
                {
                    unit.CombatTargetId = -1;
                    TryAutoSlaughterFromIdle(unit);
                    continue;
                }

                // Sheep already dead — find the carcass at its position and gather from it
                if (sheep.State == UnitState.Dead)
                {
                    unit.CombatTargetId = -1;
                    var carcassNode = FindCarcassNear(sheep.SimPosition);
                    if (carcassNode != null && !carcassNode.IsDepleted)
                    {
                        unit.TargetResourceNodeId = carcassNode.Id;
                        unit.GatherTimer = Fixed32.Zero;
                        unit.State = UnitState.Gathering;
                        unit.PlayerCommanded = false;
                    }
                    else
                    {
                        TryAutoSlaughterFromIdle(unit);
                    }
                    continue;
                }

                // Check distance — must be close enough (within 2 units)
                Fixed32 dx = sheep.SimPosition.x - unit.SimPosition.x;
                Fixed32 dz = sheep.SimPosition.z - unit.SimPosition.z;
                Fixed32 distSq = dx * dx + dz * dz;
                Fixed32 slaughterRangeSq = Fixed32.FromFloat(2f) * Fixed32.FromFloat(2f);
                if (distSq > slaughterRangeSq)
                {
                    // Sheep moved — re-path to its current position
                    Vector2Int startTile = MapData.WorldToTile(unit.SimPosition);
                    Vector2Int goalTile = MapData.WorldToTile(sheep.SimPosition);
                    var repath = GridPathfinder.FindPath(MapData, startTile, goalTile, unit.PlayerId, BuildingRegistry);
                    if (repath.Count > 0)
                    {
                        unit.SetPath(repath);
                        unit.FinalDestination = sheep.SimPosition;
                        unit.State = UnitState.MovingToSlaughter;
                    }
                    else
                    {
                        // Can't path to this sheep — try another converted sheep nearby
                        unit.CombatTargetId = -1;
                        TryAutoSlaughterFromIdle(unit);
                    }
                    continue;
                }

                // Kill the sheep
                sheep.State = UnitState.Dead;
                sheep.CurrentHealth = 0;
                int sheepId = sheep.Id;

                // Create food carcass
                var carcass = MapData.AddCarcassResourceNode(ResourceType.Food, sheep.SimPosition, config.SheepSlaughterFood);

                // Clear combat target
                unit.CombatTargetId = -1;

                // Set villager to gather from carcass
                unit.TargetResourceNodeId = carcass.Id;
                unit.GatherTimer = Fixed32.Zero;
                unit.State = UnitState.Gathering;
                unit.PlayerCommanded = false;

                OnSheepSlaughtered?.Invoke(sheepId, carcass.Id);
                OnUnitDied?.Invoke(sheepId);
            }
        }

        private void TryAutoSlaughterFromIdle(UnitData unit)
        {
            Fixed32 range = Fixed32.FromInt(7);
            Fixed32 rangeSq = range * range;
            UnitData bestSheep = null;
            Fixed32 bestDistSq = rangeSq;

            var allUnits = UnitRegistry.GetAllUnits();
            for (int i = 0; i < allUnits.Count; i++)
            {
                var candidate = allUnits[i];
                if (!candidate.IsSheep) continue;
                if (candidate.State == UnitState.Dead) continue;
                if (candidate.PlayerId != unit.PlayerId) continue;

                Fixed32 dx = candidate.SimPosition.x - unit.SimPosition.x;
                Fixed32 dz = candidate.SimPosition.z - unit.SimPosition.z;
                if (Fixed32.Abs(dx) > range || Fixed32.Abs(dz) > range) continue;

                Fixed32 distSq = dx * dx + dz * dz;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestSheep = candidate;
                }
            }

            if (bestSheep == null) return;

            unit.CombatTargetId = bestSheep.Id;
            unit.CombatTargetBuildingId = -1;
            unit.TargetResourceNodeId = -1;
            unit.GatherTimer = Fixed32.Zero;
            unit.PlayerCommanded = false;

            Vector2Int startTile = MapData.WorldToTile(unit.SimPosition);
            Vector2Int goalTile = MapData.WorldToTile(bestSheep.SimPosition);
            var path = GridPathfinder.FindPath(MapData, startTile, goalTile, unit.PlayerId, BuildingRegistry);
            if (path.Count > 0)
            {
                unit.SetPath(path);
                unit.FinalDestination = bestSheep.SimPosition;
                unit.State = UnitState.MovingToSlaughter;
            }
            else
            {
                unit.CombatTargetId = -1;
            }
        }

        private ResourceNodeData FindCarcassNear(FixedVector3 position)
        {
            Fixed32 bestDistSq = Fixed32.FromFloat(4f); // within 2 units
            ResourceNodeData best = null;
            foreach (var node in MapData.GetAllResourceNodes())
            {
                if (!node.IsCarcass || node.IsDepleted) continue;
                FixedVector3 diff = node.Position - position;
                Fixed32 distSq = diff.x * diff.x + diff.z * diff.z;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = node;
                }
            }
            return best;
        }

        private void ProcessCheatResourceCommand(CheatResourceCommand cmd)
        {
            ResourceManager.AddResource(cmd.PlayerId, ResourceType.Food, 10000);
            ResourceManager.AddResource(cmd.PlayerId, ResourceType.Wood, 10000);
            ResourceManager.AddResource(cmd.PlayerId, ResourceType.Gold, 10000);
            ResourceManager.AddResource(cmd.PlayerId, ResourceType.Stone, 10000);
        }

        private void ProcessCheatProductionCommand(CheatProductionCommand cmd)
        {
            ProductionCheatActive = !ProductionCheatActive;
        }

        private void ProcessCheatVisionCommand(CheatVisionCommand cmd)
        {
            bool current = FogOfWar.HasVisionCheat(cmd.PlayerId);
            FogOfWar.SetVisionCheat(cmd.PlayerId, !current);
        }

        private void ProcessDeleteUnitsCommand(DeleteUnitsCommand cmd)
        {
            for (int i = 0; i < cmd.UnitIds.Length; i++)
            {
                var unit = UnitRegistry.GetUnit(cmd.UnitIds[i]);
                if (unit == null || unit.State == UnitState.Dead) continue;
                if (unit.PlayerId != cmd.PlayerId) continue;

                unit.CurrentHealth = 0;
                unit.State = UnitState.Dead;
                UnitRegistry.RemoveUnit(unit.Id);
                OnUnitDied?.Invoke(unit.Id);
            }
        }

        private void ProcessDeleteBuildingCommand(DeleteBuildingCommand cmd)
        {
            var building = BuildingRegistry.GetBuilding(cmd.BuildingId);
            if (building == null) return;
            if (building.PlayerId != cmd.PlayerId) return;

            if (building.IsUnderConstruction)
            {
                int woodCost = GetBuildingWoodCost(building.Type);
                int stoneCost = GetBuildingStoneCost(building.Type);
                int foodCost = GetBuildingFoodCost(building.Type);
                int goldCost = GetBuildingGoldCost(building.Type);

                // Expected health from construction progress alone
                int ticksElapsed = building.ConstructionTicksTotal - building.ConstructionTicksRemaining;
                int expectedHealth = (int)((long)building.MaxHealth * ticksElapsed / building.ConstructionTicksTotal);

                // Enemy damage is the deficit between expected and actual health
                int enemyDamage = expectedHealth - building.CurrentHealth;
                if (enemyDamage < 0) enemyDamage = 0;

                // Full refund minus proportion of enemy damage
                int refundWood = woodCost - (woodCost * enemyDamage / building.MaxHealth);
                int refundStone = stoneCost - (stoneCost * enemyDamage / building.MaxHealth);
                int refundFood = foodCost - (foodCost * enemyDamage / building.MaxHealth);
                int refundGold = goldCost - (goldCost * enemyDamage / building.MaxHealth);

                var resources = ResourceManager.GetPlayerResources(building.PlayerId);
                resources.Wood += refundWood;
                resources.Stone += refundStone;
                resources.Food += refundFood;
                resources.Gold += refundGold;
            }

            CleanUpDestroyedBuilding(cmd.BuildingId);
        }

        private void ProcessMoveCommand(MoveCommand cmd)
        {
            // === Retarget if click target is impassable or a hole ===
            Vector2Int cmdTargetTile = MapData.WorldToTile(cmd.TargetPosition);
            bool needsRetarget = !MapData.IsWalkable(cmdTargetTile.x, cmdTargetTile.y);

            if (needsRetarget)
            {
                Vector2Int newTarget = GridPathfinder.FindNearestWalkableTile(MapData, cmdTargetTile, 20);
                if (newTarget.x < 0) return;
                cmd.TargetPosition = MapData.TileToWorldFixed(newTarget.x, newTarget.y);
            }

            // === Validate formation connectivity ===
            // Ensure all formation positions are in the same connected walkable region as the main target.
            if (cmd.FormationPositions != null)
            {
                Vector2Int anchorTile = MapData.WorldToTile(cmd.TargetPosition);

                // Compute search radius: max Chebyshev distance from anchor to any formation pos + margin
                int maxChebyshev = 0;
                for (int fi = 0; fi < cmd.FormationPositions.Length; fi++)
                {
                    Vector2Int fpTile = MapData.WorldToTile(cmd.FormationPositions[fi]);
                    int chebyshev = Mathf.Max(Mathf.Abs(fpTile.x - anchorTile.x), Mathf.Abs(fpTile.y - anchorTile.y));
                    if (chebyshev > maxChebyshev) maxChebyshev = chebyshev;
                }
                int floodRadius = maxChebyshev * 2;

                HashSet<int> reachable = GridPathfinder.FloodFillWalkable(MapData, anchorTile, floodRadius);

                if (reachable.Count > 0)
                {
                    int mapWidth = MapData.Width;
                    for (int fi = 0; fi < cmd.FormationPositions.Length; fi++)
                    {
                        Vector2Int fpTile = MapData.WorldToTile(cmd.FormationPositions[fi]);
                        int key = fpTile.y * mapWidth + fpTile.x;
                        if (!reachable.Contains(key))
                        {
                            Vector2Int snapped = GridPathfinder.FindConnectedTileToward(fpTile, anchorTile, reachable, mapWidth, floodRadius);
                            if (snapped.x >= 0)
                                cmd.FormationPositions[fi] = MapData.TileToWorldFixed(snapped.x, snapped.y);
                        }
                    }
                }
            }

            // Assign a formation group ID when creating a new drag formation
            int formGroupId = 0;
            int formGroupSize = 0;
            if (cmd.UnitIds.Length > 1 && !cmd.IsQueued)
            {
                formGroupId = nextFormationGroupId++;
                // Count only alive units for formation group size
                for (int i = 0; i < cmd.UnitIds.Length; i++)
                {
                    var u = UnitRegistry.GetUnit(cmd.UnitIds[i]);
                    if (u != null && u.State != UnitState.Dead) formGroupSize++;
                }
            }

            // Compute formation move speed = slowest unit in group
            Fixed32 formationMoveSpeed = Fixed32.Zero;
            bool isFormationMove = (cmd.FormationPositions != null && cmd.HasFacing && !cmd.IsQueued)
                                || cmd.PreserveFormation;
            int unitCount = cmd.UnitIds.Length;
            if (isFormationMove && unitCount > 1)
            {
                bool first = true;
                for (int i = 0; i < unitCount; i++)
                {
                    var u = UnitRegistry.GetUnit(cmd.UnitIds[i]);
                    if (u == null || u.State == UnitState.Dead) continue;
                    if (first) { formationMoveSpeed = u.MoveSpeed; first = false; }
                    else formationMoveSpeed = Fixed32.Min(formationMoveSpeed, u.MoveSpeed);
                }
            }

            int formPosIdx = 0;
            for (int i = 0; i < cmd.UnitIds.Length; i++)
            {
                var unit = UnitRegistry.GetUnit(cmd.UnitIds[i]);
                if (unit == null || unit.State == UnitState.Dead) continue;

                FixedVector3 targetPos;
                bool hasFacing = cmd.HasFacing;
                FixedVector3 facingDir = cmd.FacingDirection;

                if (cmd.PreserveFormation && unit.InFormation)
                {
                    targetPos = cmd.TargetPosition + unit.FormationOffset;
                    hasFacing = true;
                    facingDir = unit.FormationFacing;
                    unit.FormationMoveSpeed = formationMoveSpeed;
                }
                else if (cmd.FormationPositions != null && formPosIdx < cmd.FormationPositions.Length)
                {
                    targetPos = cmd.FormationPositions[formPosIdx];

                    // Scale formation spacing proportionally to unit radius
                    // (cavalry 0.55 → 1.375x spread, infantry 0.4 → 1.0x)
                    Fixed32 radiusScale = unit.Radius / cachedUnitRadius;
                    if (radiusScale != Fixed32.One)
                    {
                        FixedVector3 offset = targetPos - cmd.TargetPosition;
                        targetPos = cmd.TargetPosition + new FixedVector3(
                            offset.x * radiusScale, offset.y, offset.z * radiusScale);
                    }

                    Fixed32 scatterFactor = Fixed32.Max(Fixed32.Zero,
                        Fixed32.One - (unit.Mass - Fixed32.One) / (cachedSpearmanMass - Fixed32.One));
                    Fixed32 scatter = cachedFormationScatter * scatterFactor;

                    if (scatter > cachedScatterThreshold)
                    {
                        uint hash = (uint)(unit.Id * 2654435761u + (uint)currentTick * 1103515245u);
                        int hashX = (int)(hash & 0xFFFF) - 32768;
                        int hashZ = (int)((hash >> 16) & 0xFFFF) - 32768;
                        Fixed32 normX = new Fixed32(hashX);
                        Fixed32 normZ = new Fixed32(hashZ);
                        targetPos.x = targetPos.x + normX * scatter;
                        targetPos.z = targetPos.z + normZ * scatter;
                    }

                    if (!cmd.IsQueued)
                    {
                        unit.FormationOffset = targetPos - cmd.TargetPosition;
                        if (cmd.HasFacing)
                        {
                            unit.FormationFacing = cmd.FacingDirection;
                            unit.InFormation = true;
                            unit.FormationGroupId = formGroupId;
                            unit.FormationGroupSize = formGroupSize;
                            unit.FormationMoveSpeed = formationMoveSpeed;
                        }
                        else
                        {
                            unit.InFormation = false;
                            unit.FormationGroupId = formGroupId;
                            unit.FormationGroupSize = formGroupSize;
                        }
                    }
                }
                else
                {
                    targetPos = cmd.TargetPosition;
                    if (!cmd.IsQueued)
                    {
                        unit.ClearFormation();
                        unit.FormationGroupId = formGroupId;
                        unit.FormationGroupSize = formGroupSize;
                    }
                }

                if (cmd.IsQueued)
                {
                    // Append to unit's command queue
                    QueuedCommand qc;
                    if (cmd.IsAttackMove)
                        qc = QueuedCommand.AttackMoveWaypoint(targetPos);
                    else if (hasFacing)
                        qc = QueuedCommand.MoveWaypoint(targetPos, facingDir);
                    else
                        qc = QueuedCommand.MoveWaypoint(targetPos);
                    unit.CommandQueue.Add(qc);

                    // If idle, immediately pop and execute
                    if (unit.State == UnitState.Idle)
                        PopAndExecuteNextQueuedCommand(unit);
                }
                else
                {
                    // Non-queued: clear queue and execute immediately
                    unit.ClearCommandQueue();
                    unit.IsAttackMoving = cmd.IsAttackMove;
                    unit.HasTargetFacing = hasFacing;
                    unit.TargetFacing = facingDir;

                    Vector2Int startTile = MapData.WorldToTile(unit.SimPosition);
                    Vector2Int goalTile = MapData.WorldToTile(targetPos);
                    List<Vector2Int> path = GridPathfinder.FindPath(MapData, startTile, goalTile, unit.PlayerId, BuildingRegistry);
                    unit.FormationLeaderId = -1;

                    if (path.Count > 0)
                    {
                        // Align FinalDestination with actual A* endpoint so waypoint line matches path
                        Vector2Int pathEnd = path[path.Count - 1];
                        if (pathEnd != goalTile)
                            targetPos = MapData.TileToWorldFixed(pathEnd.x, pathEnd.y);

                        unit.SetPath(path);
                        unit.FinalDestination = new FixedVector3(targetPos.x, Fixed32.Zero, targetPos.z);
                        unit.State = UnitState.Moving;
                        unit.PlayerCommanded = !cmd.IsAttackMove;
                    }
                    else
                    {
                        unit.ClearPath();
                        unit.State = UnitState.Idle;
                        unit.PlayerCommanded = false;
                    }

                    unit.TargetResourceNodeId = -1;
                    unit.ConstructionTargetBuildingId = -1;
                    unit.GatherTimer = Fixed32.Zero;
                    unit.ClearSavedPath();
                    unit.CombatTargetId = -1;
                    unit.CombatTargetBuildingId = -1;
                    unit.DropOffBuildingId = -1;
                    unit.TargetGarrisonBuildingId = -1;

                    // Sheep receiving a move command stops following
                    if (unit.IsSheep)
                    {
                        unit.FollowTargetId = -1;
                        // Check if original target is on or adjacent to a building — if so, sheep will run to it
                        unit.SheepTargetBuildingId = FindBuildingNearPosition(cmd.TargetPosition, unit.PlayerId);
                    }

                    unit.ClearPatrol();
                }

                formPosIdx++;
            }
        }

        private void ProcessGatherCommand(GatherCommand cmd)
        {
            var node = MapData.GetResourceNode(cmd.ResourceNodeId);
            if (node == null || node.IsDepleted) return;

            Vector2Int nodeOrigin = new Vector2Int(node.TileX, node.TileZ);
            // Pre-populate occupiedTiles from existing gatherers at the target node
            var occupiedTiles = new HashSet<Vector2Int>();
            var preExisting = UnitRegistry.GetAllUnits();
            for (int u = 0; u < preExisting.Count; u++)
            {
                var other = preExisting[u];
                if (other.State == UnitState.Dead) continue;
                if (other.TargetResourceNodeId != cmd.ResourceNodeId) continue;
                if (other.State == UnitState.MovingToGather || other.State == UnitState.Gathering)
                    occupiedTiles.Add(MapData.WorldToTile(other.FinalDestination));
            }

            // Track which farm nodes are taken (for one-villager-per-farm)
            var takenFarmNodeIds = new HashSet<int>();

            for (int i = 0; i < cmd.UnitIds.Length; i++)
            {
                var unit = UnitRegistry.GetUnit(cmd.UnitIds[i]);
                if (unit == null || unit.State == UnitState.Dead) continue;

                // Resource type switching: if carrying a different type, instantly deposit
                if (unit.CarriedResourceAmount > 0 && unit.CarriedResourceType != node.Type)
                {
                    ResourceManager.AddResource(unit.PlayerId, unit.CarriedResourceType, unit.CarriedResourceAmount);
                    unit.CarriedResourceAmount = 0;
                }

                if (cmd.IsQueued)
                {
                    var qc = QueuedCommand.GatherWaypoint(node.Position, cmd.ResourceNodeId);
                    unit.CommandQueue.Add(qc);

                    if (unit.State == UnitState.Idle)
                        PopAndExecuteNextQueuedCommand(unit);
                }
                else
                {
                    unit.ClearCommandQueue();
                    Vector2Int startTile = MapData.WorldToTile(unit.SimPosition);
                    bool assigned = false;

                    if (node.IsFarmNode)
                    {
                        // One villager per farm — check if occupied
                        bool farmTaken = takenFarmNodeIds.Contains(cmd.ResourceNodeId) || IsFarmNodeOccupied(cmd.ResourceNodeId, null);
                        int targetNodeId = cmd.ResourceNodeId;

                        // If taken, find a nearby unoccupied farm
                        if (farmTaken)
                            targetNodeId = FindNearbyUnoccupiedFarm(unit.SimPosition, takenFarmNodeIds);

                        if (targetNodeId >= 0)
                        {
                            var targetNode = MapData.GetResourceNode(targetNodeId);
                            if (targetNode != null)
                            {
                                Vector2Int farmNodeTile = MapData.WorldToTile(targetNode.Position);
                                var path = GridPathfinder.FindPath(MapData, startTile, farmNodeTile, unit.PlayerId, BuildingRegistry);
                                if (path.Count > 0)
                                {
                                    AssignUnitToGather(unit, targetNodeId, path, farmNodeTile);
                                    takenFarmNodeIds.Add(targetNodeId);
                                    assigned = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Normal resource: try adjacent tiles until one is pathfind-reachable
                        var triedTiles = new HashSet<Vector2Int>(occupiedTiles);
                        int attempts = 0;

                        while (true)
                        {
                            if (++attempts > 4) break; // Cap retry attempts
                            Vector2Int adjTile = FindNearestWalkableAdjacentTileForResource(nodeOrigin, node.FootprintWidth, node.FootprintHeight, unit.SimPosition, triedTiles);
                            if (adjTile == nodeOrigin) break; // All tiles exhausted

                            var path = GridPathfinder.FindPath(MapData, startTile, adjTile, unit.PlayerId, BuildingRegistry);
                            if (path.Count > 0)
                            {
                                occupiedTiles.Add(adjTile);
                                AssignUnitToGather(unit, cmd.ResourceNodeId, path, adjTile);
                                assigned = true;
                                break;
                            }
                            triedTiles.Add(adjTile);
                        }
                    }

                    if (!assigned)
                    {
                        if (!TryRedirectGatherToNearbyNode(unit, node.Type, cmd.ResourceNodeId, node.Position))
                        {
                            // Fallback: assign to primary tree ignoring occupancy (stacking is fine)
                            var fallbackTried = new HashSet<Vector2Int>();
                            int fallbackAttempts = 0;
                            while (true)
                            {
                                if (++fallbackAttempts > 4) break; // Cap retry attempts
                                Vector2Int adjTile = FindNearestWalkableAdjacentTileForResource(
                                    nodeOrigin, node.FootprintWidth, node.FootprintHeight,
                                    unit.SimPosition, fallbackTried);
                                if (adjTile == nodeOrigin) break;
                                var path = GridPathfinder.FindPath(MapData, startTile, adjTile, unit.PlayerId, BuildingRegistry);
                                if (path.Count > 0)
                                {
                                    AssignUnitToGather(unit, cmd.ResourceNodeId, path, adjTile);
                                    break;
                                }
                                fallbackTried.Add(adjTile);
                            }
                        }
                    }
                }
            }
        }

        private void AssignUnitToGather(UnitData unit, int resourceNodeId, List<Vector2Int> path, Vector2Int destTile)
        {
            unit.SetPath(path);
            unit.ClearFormation();
            unit.FinalDestination = MapData.TileToWorldFixed(destTile.x, destTile.y);
            unit.State = UnitState.MovingToGather;
            unit.TargetResourceNodeId = resourceNodeId;
            unit.ConstructionTargetBuildingId = -1;
            unit.GatherTimer = Fixed32.Zero;
            unit.ClearSavedPath();
            unit.PlayerCommanded = true;
            unit.CombatTargetId = -1;
            unit.CombatTargetBuildingId = -1;
            unit.DropOffBuildingId = -1;
            unit.TargetGarrisonBuildingId = -1;

            unit.ClearPatrol();
        }

        public bool IsFarmNodeOccupiedByAny(int farmNodeId)
        {
            var allUnits = UnitRegistry.GetAllUnits();
            for (int u = 0; u < allUnits.Count; u++)
            {
                var other = allUnits[u];
                if (other.State == UnitState.Dead) continue;
                if (other.TargetResourceNodeId != farmNodeId) continue;
                if (other.State == UnitState.MovingToGather || other.State == UnitState.Gathering
                    || other.State == UnitState.MovingToDropoff || other.State == UnitState.DroppingOff)
                    return true;
            }
            return false;
        }

        private bool IsFarmNodeOccupied(int farmNodeId, UnitData excludeUnit)
        {
            var allUnits = UnitRegistry.GetAllUnits();
            for (int u = 0; u < allUnits.Count; u++)
            {
                var other = allUnits[u];
                if (other == excludeUnit || other.State == UnitState.Dead) continue;
                if (other.TargetResourceNodeId != farmNodeId) continue;
                if (other.State == UnitState.MovingToGather || other.State == UnitState.Gathering
                    || other.State == UnitState.MovingToDropoff || other.State == UnitState.DroppingOff)
                    return true;
            }
            return false;
        }

        private int FindNearbyUnoccupiedFarm(FixedVector3 searchPos, HashSet<int> additionalTaken)
        {
            Fixed32 searchRange = Fixed32.FromInt(30);
            int bestId = -1;
            Fixed32 bestDistSq = searchRange * searchRange;

            foreach (var candidate in MapData.GetAllResourceNodes())
            {
                if (!candidate.IsFarmNode || candidate.IsDepleted) continue;
                if (additionalTaken != null && additionalTaken.Contains(candidate.Id)) continue;
                if (IsFarmNodeOccupied(candidate.Id, null)) continue;

                FixedVector3 diff = candidate.Position - searchPos;
                if (Fixed32.Abs(diff.x) > searchRange || Fixed32.Abs(diff.z) > searchRange) continue;

                Fixed32 distSq = diff.x * diff.x + diff.z * diff.z;
                if (distSq < bestDistSq || (distSq == bestDistSq && candidate.Id < bestId))
                {
                    bestDistSq = distSq;
                    bestId = candidate.Id;
                }
            }
            return bestId;
        }

        private void ProcessStopCommand(StopCommand cmd)
        {
            foreach (int unitId in cmd.UnitIds)
            {
                var unit = UnitRegistry.GetUnit(unitId);
                if (unit == null) continue;

                unit.ClearPath();
                unit.ClearFormation();
                unit.ClearCommandQueue();
                unit.State = UnitState.Idle;
                unit.TargetResourceNodeId = -1;
                unit.ConstructionTargetBuildingId = -1;
                unit.GatherTimer = Fixed32.Zero;
                unit.ClearSavedPath();
                unit.PlayerCommanded = false;
                unit.CombatTargetId = -1;
                unit.CombatTargetBuildingId = -1;
                unit.DropOffBuildingId = -1;
                unit.TargetGarrisonBuildingId = -1;

                unit.ClearPatrol();
            }
        }

        public BuildingData CreateBuilding(int playerId, BuildingType type, int tileX, int tileZ, bool underConstruction = false, bool isMainTownCenter = false)
        {
            int footprintW, footprintH, maxHealth, armor;
            switch (type)
            {
                case BuildingType.Barracks:
                    footprintW = config.BarracksFootprintWidth;
                    footprintH = config.BarracksFootprintHeight;
                    maxHealth = config.BarracksMaxHealth;
                    armor = config.BarracksArmor;
                    break;
                case BuildingType.TownCenter:
                    footprintW = config.TownCenterFootprintWidth;
                    footprintH = config.TownCenterFootprintHeight;
                    maxHealth = config.TownCenterMaxHealth;
                    armor = config.TownCenterArmor;
                    break;
                case BuildingType.Wall:
                    footprintW = config.WallFootprintWidth;
                    footprintH = config.WallFootprintHeight;
                    maxHealth = config.WallMaxHealth;
                    armor = config.WallArmor;
                    break;
                case BuildingType.Mill:
                    footprintW = config.MillFootprintWidth;
                    footprintH = config.MillFootprintHeight;
                    maxHealth = config.MillMaxHealth;
                    armor = config.MillArmor;
                    break;
                case BuildingType.LumberYard:
                    footprintW = config.LumberYardFootprintWidth;
                    footprintH = config.LumberYardFootprintHeight;
                    maxHealth = config.LumberYardMaxHealth;
                    armor = config.LumberYardArmor;
                    break;
                case BuildingType.Mine:
                    footprintW = config.MineFootprintWidth;
                    footprintH = config.MineFootprintHeight;
                    maxHealth = config.MineMaxHealth;
                    armor = config.MineArmor;
                    break;
                case BuildingType.ArcheryRange:
                    footprintW = config.ArcheryRangeFootprintWidth;
                    footprintH = config.ArcheryRangeFootprintHeight;
                    maxHealth = config.ArcheryRangeMaxHealth;
                    armor = config.ArcheryRangeArmor;
                    break;
                case BuildingType.Stables:
                    footprintW = config.StablesFootprintWidth;
                    footprintH = config.StablesFootprintHeight;
                    maxHealth = config.StablesMaxHealth;
                    armor = config.StablesArmor;
                    break;
                case BuildingType.Farm:
                    footprintW = config.FarmFootprintWidth;
                    footprintH = config.FarmFootprintHeight;
                    maxHealth = config.FarmMaxHealth;
                    armor = config.FarmArmor;
                    break;
                case BuildingType.Tower:
                    footprintW = config.TowerFootprintWidth;
                    footprintH = config.TowerFootprintHeight;
                    maxHealth = config.TowerMaxHealth;
                    armor = config.TowerArmor;
                    break;
                case BuildingType.Monastery:
                    footprintW = config.MonasteryFootprintWidth;
                    footprintH = config.MonasteryFootprintHeight;
                    maxHealth = config.MonasteryMaxHealth;
                    armor = config.MonasteryArmor;
                    break;
                case BuildingType.Blacksmith:
                    footprintW = config.BlacksmithFootprintWidth;
                    footprintH = config.BlacksmithFootprintHeight;
                    maxHealth = config.BlacksmithMaxHealth;
                    armor = config.BlacksmithArmor;
                    break;
                case BuildingType.Market:
                    footprintW = config.MarketFootprintWidth;
                    footprintH = config.MarketFootprintHeight;
                    maxHealth = config.MarketMaxHealth;
                    armor = config.MarketArmor;
                    break;
                case BuildingType.University:
                    footprintW = config.UniversityFootprintWidth;
                    footprintH = config.UniversityFootprintHeight;
                    maxHealth = config.UniversityMaxHealth;
                    armor = config.UniversityArmor;
                    break;
                case BuildingType.SiegeWorkshop:
                    footprintW = config.SiegeWorkshopFootprintWidth;
                    footprintH = config.SiegeWorkshopFootprintHeight;
                    maxHealth = config.SiegeWorkshopMaxHealth;
                    armor = config.SiegeWorkshopArmor;
                    break;
                case BuildingType.Keep:
                    footprintW = config.KeepFootprintWidth;
                    footprintH = config.KeepFootprintHeight;
                    maxHealth = config.KeepMaxHealth;
                    armor = config.KeepArmor;
                    break;
                case BuildingType.StoneWall:
                    footprintW = config.StoneWallFootprintWidth;
                    footprintH = config.StoneWallFootprintHeight;
                    maxHealth = config.StoneWallMaxHealth;
                    armor = config.StoneWallArmor;
                    break;
                case BuildingType.StoneGate:
                    footprintW = config.StoneGateFootprintWidth;
                    footprintH = config.StoneGateFootprintHeight;
                    maxHealth = config.StoneGateMaxHealth;
                    armor = config.StoneGateArmor;
                    break;
                case BuildingType.WoodGate:
                    footprintW = config.WoodGateFootprintWidth;
                    footprintH = config.WoodGateFootprintHeight;
                    maxHealth = config.WoodGateMaxHealth;
                    armor = config.WoodGateArmor;
                    break;
                case BuildingType.Wonder:
                    footprintW = config.WonderFootprintWidth;
                    footprintH = config.WonderFootprintHeight;
                    maxHealth = config.WonderMaxHealth;
                    armor = config.WonderArmor;
                    break;
                case BuildingType.Landmark:
                    footprintW = config.LandmarkFootprintWidth;
                    footprintH = config.LandmarkFootprintHeight;
                    maxHealth = 2500;
                    armor = 5;
                    break;
                default: // House
                    footprintW = config.HouseFootprintWidth;
                    footprintH = config.HouseFootprintHeight;
                    maxHealth = config.HouseMaxHealth;
                    armor = config.HouseArmor;
                    break;
            }

            // Center of the building footprint in world space
            FixedVector3 position = new FixedVector3(
                Fixed32.FromInt(tileX) + Fixed32.FromInt(footprintW) / Fixed32.FromInt(2),
                Fixed32.Zero,
                Fixed32.FromInt(tileZ) + Fixed32.FromInt(footprintH) / Fixed32.FromInt(2));

            var building = BuildingRegistry.CreateBuilding(playerId, type, position,
                tileX, tileZ, footprintW, footprintH);
            building.MaxHealth = maxHealth;
            building.Armor = armor;

            // Initialize tower combat capabilities
            if (type == BuildingType.Tower)
            {
                building.AttackDamage = config.TowerAttackDamage;
                building.AttackRange = ConfigToFixed32(config.TowerAttackRange);
                building.DetectionRange = ConfigToFixed32(config.TowerDetectionRange);
                building.AttackCooldownTicks = config.TowerAttackCooldownTicks;
                building.BaseArrowCount = 1;
                building.GarrisonCapacity = 5; // Towers can garrison 5 units
            }
            
            // Town Center combat and garrison
            if (type == BuildingType.TownCenter)
            {
                building.IsMainTownCenter = isMainTownCenter;
                building.AttackDamage = config.TownCenterArrowDamage;
                
                // Set attack range based on whether it's a main town center or not
                float attackRange = isMainTownCenter 
                    ? config.MainTownCenterAttackRange 
                    : config.SubsequentTownCenterAttackRange;
                building.AttackRange = ConfigToFixed32(attackRange);

                building.DetectionRange = ConfigToFixed32(attackRange);
                building.AttackCooldownTicks = config.TownCenterAttackCooldownTicks;
                building.GarrisonCapacity = config.TownCenterGarrisonCapacity;
            }

            if (underConstruction)
            {
                building.CurrentHealth = 1;
                building.IsUnderConstruction = true;
                building.ConstructionTicksTotal = GetConstructionTicks(type);
                building.ConstructionTicksRemaining = building.ConstructionTicksTotal;
                // Template tiles: units can walk through, but new buildings can't be placed here
                MapData.MarkTemplateTiles(tileX, tileZ, footprintW, footprintH);
            }
            else
            {
                building.CurrentHealth = building.MaxHealth;
                MapData.MarkBuildingTiles(tileX, tileZ, footprintW, footprintH);
            }

            building.FoundationBorder = (type == BuildingType.Wall || type == BuildingType.Farm || type == BuildingType.StoneWall || type == BuildingType.StoneGate || type == BuildingType.WoodGate) ? 0 : 1;
            MapData.MarkFoundationBorder(tileX, tileZ, footprintW, footprintH, building.FoundationBorder);

            if (type == BuildingType.TownCenter && !firstTownCenterIds.ContainsKey(playerId))
                firstTownCenterIds[playerId] = building.Id;

            return building;
        }

        public int GetConstructionTicks(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.House: return config.HouseConstructionTicks;
                case BuildingType.Barracks: return config.BarracksConstructionTicks;
                case BuildingType.TownCenter: return config.TownCenterConstructionTicks;
                case BuildingType.Wall: return config.WallConstructionTicks;
                case BuildingType.Mill: return config.MillConstructionTicks;
                case BuildingType.LumberYard: return config.LumberYardConstructionTicks;
                case BuildingType.Mine: return config.MineConstructionTicks;
                case BuildingType.ArcheryRange: return config.ArcheryRangeConstructionTicks;
                case BuildingType.Stables: return config.StablesConstructionTicks;
                case BuildingType.Farm: return config.FarmConstructionTicks;
                case BuildingType.Tower: return config.TowerConstructionTicks;
                case BuildingType.Monastery: return config.MonasteryConstructionTicks;
                case BuildingType.Blacksmith: return config.BlacksmithConstructionTicks;
                case BuildingType.Market: return config.MarketConstructionTicks;
                case BuildingType.University: return config.UniversityConstructionTicks;
                case BuildingType.SiegeWorkshop: return config.SiegeWorkshopConstructionTicks;
                case BuildingType.Keep: return config.KeepConstructionTicks;
                case BuildingType.StoneWall: return config.StoneWallConstructionTicks;
                case BuildingType.StoneGate: return config.StoneGateConstructionTicks;
                case BuildingType.WoodGate: return config.WoodGateConstructionTicks;
                case BuildingType.Wonder: return config.WonderConstructionTicks;
                case BuildingType.Landmark: return 3000; // default; actual value set from LandmarkDefinitions
                default: return config.HouseConstructionTicks;
            }
        }

        private void ProcessAttackBuildingCommand(AttackBuildingCommand cmd)
        {
            var building = BuildingRegistry.GetBuilding(cmd.TargetBuildingId);
            if (building == null || building.IsDestroyed) return;
            if (AreAllies(building.PlayerId, cmd.PlayerId)) return;

            var occupiedTiles = new HashSet<Vector2Int>();
            for (int i = 0; i < cmd.UnitIds.Length; i++)
            {
                var unit = UnitRegistry.GetUnit(cmd.UnitIds[i]);
                if (unit == null || unit.State == UnitState.Dead) continue;

                unit.ClearCommandQueue();
                unit.ClearSavedPath();
                unit.ClearFormation();
                unit.CombatTargetId = -1;
                unit.CombatTargetBuildingId = cmd.TargetBuildingId;
                unit.PlayerCommanded = false;
                unit.TargetResourceNodeId = -1;
                unit.ConstructionTargetBuildingId = -1;
                unit.GatherTimer = Fixed32.Zero;
                unit.DropOffBuildingId = -1;
                unit.TargetGarrisonBuildingId = -1;

                unit.ClearPatrol();

                // Find unique walkable tile adjacent to building using retry loop
                var triedTiles = new HashSet<Vector2Int>(occupiedTiles);
                Vector2Int startTile = MapData.WorldToTile(unit.SimPosition);
                bool assigned = false;

                while (true)
                {
                    Vector2Int adjTile = FindNearestWalkableAdjacentTile(building, unit.SimPosition, triedTiles);
                    if (triedTiles.Contains(adjTile)) break; // All tiles exhausted

                    var path = GridPathfinder.FindPath(MapData, startTile, adjTile, unit.PlayerId, BuildingRegistry);
                    if (path.Count > 0)
                    {
                        occupiedTiles.Add(adjTile);
                        unit.SetPath(path);
                        unit.FinalDestination = MapData.TileToWorldFixed(adjTile.x, adjTile.y);
                        unit.State = UnitState.Moving;
                        assigned = true;
                        break;
                    }
                    triedTiles.Add(adjTile);
                }

                if (!assigned)
                {
                    unit.State = UnitState.InCombat;
                }
            }
        }

        private void ProcessAttackUnitCommand(AttackUnitCommand cmd)
        {
            var target = UnitRegistry.GetUnit(cmd.TargetUnitId);
            if (target == null || target.State == UnitState.Dead) return;
            if (AreAllies(target.PlayerId, cmd.PlayerId)) return;

            for (int i = 0; i < cmd.UnitIds.Length; i++)
            {
                var unit = UnitRegistry.GetUnit(cmd.UnitIds[i]);
                if (unit == null || unit.State == UnitState.Dead) continue;

                unit.ClearCommandQueue();
                unit.ClearSavedPath();
                unit.ClearFormation();
                unit.CombatTargetId = cmd.TargetUnitId;
                unit.CombatTargetBuildingId = -1;
                unit.PlayerCommanded = true;
                unit.IsAttackMoving = false;
                unit.ChaseBlockedTicks = 0;
                unit.TargetResourceNodeId = -1;
                unit.ConstructionTargetBuildingId = -1;
                unit.GatherTimer = Fixed32.Zero;
                unit.DropOffBuildingId = -1;
                unit.TargetGarrisonBuildingId = -1;

                unit.ClearPatrol();

                // Pathfind toward the target unit's current position
                Vector2Int startTile = MapData.WorldToTile(unit.SimPosition);
                Vector2Int goalTile = MapData.WorldToTile(target.SimPosition);

                var path = GridPathfinder.FindPath(MapData, startTile, goalTile, unit.PlayerId, BuildingRegistry);
                if (path.Count > 0)
                {
                    unit.SetPath(path);
                    unit.FinalDestination = target.SimPosition;
                    unit.State = UnitState.Moving;
                }
                else
                {
                    unit.State = UnitState.InCombat;
                }
            }
        }

        private void ProcessTrainUnitCommand(TrainUnitCommand cmd)
        {
            var building = BuildingRegistry.GetBuilding(cmd.BuildingId);
            if (building == null || building.IsDestroyed) return;
            if (building.PlayerId != cmd.PlayerId) return;
            if (building.IsUnderConstruction) return;
            if (building.Type != BuildingType.Barracks &&
                building.Type != BuildingType.TownCenter &&
                building.Type != BuildingType.ArcheryRange &&
                building.Type != BuildingType.Stables &&
                building.Type != BuildingType.Monastery) return;

            // Age gate for units
            if (playerAges[cmd.PlayerId] < LandmarkDefinitions.GetUnitRequiredAge(cmd.UnitType)) return;

            // Resolve civilization-unique unit substitution
            int resolvedUnitType = ResolveCivUnitType(cmd.PlayerId, cmd.UnitType);

            // Check resource cost
            int foodCost, woodCost, goldCost, trainTime;
            switch (resolvedUnitType)
            {
                case 9: // Monk
                    foodCost = config.MonkFoodCost;
                    woodCost = 0;
                    goldCost = config.MonkGoldCost;
                    trainTime = config.MonkTrainTimeTicks;
                    break;
                case 8: // Crossbowman
                    foodCost = config.CrossbowmanFoodCost;
                    woodCost = 0;
                    goldCost = config.CrossbowmanGoldCost;
                    trainTime = config.CrossbowmanTrainTimeTicks;
                    break;
                case 7: // Knight
                    foodCost = config.KnightFoodCost;
                    woodCost = 0;
                    goldCost = config.KnightGoldCost;
                    trainTime = config.KnightTrainTimeTicks;
                    break;
                case 6: // Man-at-Arms
                    foodCost = config.ManAtArmsFoodCost;
                    woodCost = 0;
                    goldCost = config.ManAtArmsGoldCost;
                    trainTime = config.ManAtArmsTrainTimeTicks;
                    break;
                case 10: // Longbowman
                    foodCost = config.LongbowmanFoodCost;
                    woodCost = config.LongbowmanWoodCost;
                    goldCost = 0;
                    trainTime = config.LongbowmanTrainTimeTicks;
                    break;
                case 11: // Gendarme
                    foodCost = config.GendarmeFoodCost;
                    woodCost = config.GendarmeWoodCost;
                    goldCost = 0;
                    trainTime = config.GendarmeTrainTimeTicks;
                    break;
                case 12: // Landsknecht
                    foodCost = config.LandsknechtFoodCost;
                    woodCost = config.LandsknechtWoodCost;
                    goldCost = 0;
                    trainTime = config.LandsknechtTrainTimeTicks;
                    break;
                case 4:
                    foodCost = config.ScoutFoodCost;
                    woodCost = config.ScoutWoodCost;
                    goldCost = 0;
                    trainTime = config.ScoutTrainTimeTicks;
                    break;
                case 3:
                    foodCost = config.HorsemanFoodCost;
                    woodCost = config.HorsemanWoodCost;
                    goldCost = 0;
                    trainTime = config.HorsemanTrainTimeTicks;
                    break;
                case 2:
                    foodCost = config.ArcherFoodCost;
                    woodCost = config.ArcherWoodCost;
                    goldCost = 0;
                    trainTime = config.ArcherTrainTimeTicks;
                    break;
                case 0:
                    foodCost = config.VillagerFoodCost;
                    woodCost = 0;
                    goldCost = 0;
                    trainTime = config.VillagerTrainTimeTicks;
                    break;
                default: // 1 = spearman
                    foodCost = config.SpearmanFoodCost;
                    woodCost = config.SpearmanWoodCost;
                    goldCost = 0;
                    trainTime = config.SpearmanTrainTimeTicks;
                    break;
            }
            if (IsBuildingInFrenchLandmarkInfluence(building))
            {
                int discount = config.FrenchLandmarkTrainingDiscountPercent;
                foodCost = foodCost * (100 - discount) / 100;
                woodCost = woodCost * (100 - discount) / 100;
                goldCost = goldCost * (100 - discount) / 100;
            }

            var resources = ResourceManager.GetPlayerResources(cmd.PlayerId);
            if (resources.Food < foodCost || resources.Wood < woodCost || resources.Gold < goldCost) return;

            resources.Food -= foodCost;
            resources.Wood -= woodCost;
            resources.Gold -= goldCost;
            building.EnqueueTraining(resolvedUnitType, trainTime);
        }

        private void ProcessCancelTrainCommand(CancelTrainCommand cmd)
        {
            var building = BuildingRegistry.GetBuilding(cmd.BuildingId);
            if (building == null || building.IsDestroyed) return;
            if (building.PlayerId != cmd.PlayerId) return;
            if (!building.IsTraining) return;
            if (cmd.QueueIndex < 0 || cmd.QueueIndex >= building.TrainingQueue.Count) return;

            int unitType = building.TrainingQueue[cmd.QueueIndex];
            int foodCost, woodCost, goldCost;
            switch (unitType)
            {
                case 9: foodCost = config.MonkFoodCost; woodCost = 0; goldCost = config.MonkGoldCost; break;
                case 8: foodCost = config.CrossbowmanFoodCost; woodCost = 0; goldCost = config.CrossbowmanGoldCost; break;
                case 7: foodCost = config.KnightFoodCost; woodCost = 0; goldCost = config.KnightGoldCost; break;
                case 6: foodCost = config.ManAtArmsFoodCost; woodCost = 0; goldCost = config.ManAtArmsGoldCost; break;
                case 10: foodCost = config.LongbowmanFoodCost; woodCost = config.LongbowmanWoodCost; goldCost = 0; break;
                case 11: foodCost = config.GendarmeFoodCost; woodCost = config.GendarmeWoodCost; goldCost = 0; break;
                case 12: foodCost = config.LandsknechtFoodCost; woodCost = config.LandsknechtWoodCost; goldCost = 0; break;
                case 4: foodCost = config.ScoutFoodCost; woodCost = config.ScoutWoodCost; goldCost = 0; break;
                case 3: foodCost = config.HorsemanFoodCost; woodCost = config.HorsemanWoodCost; goldCost = 0; break;
                case 2: foodCost = config.ArcherFoodCost; woodCost = config.ArcherWoodCost; goldCost = 0; break;
                case 0: foodCost = config.VillagerFoodCost; woodCost = 0; goldCost = 0; break;
                default: foodCost = config.SpearmanFoodCost; woodCost = config.SpearmanWoodCost; goldCost = 0; break;
            }

            var resources = ResourceManager.GetPlayerResources(cmd.PlayerId);
            resources.Food += foodCost;
            resources.Wood += woodCost;
            resources.Gold += goldCost;

            building.TrainingQueue.RemoveAt(cmd.QueueIndex);

            if (cmd.QueueIndex == 0)
            {
                if (building.IsTraining)
                {
                    building.TrainingTicksRemaining = BuildingTrainingSystem.GetTrainTime(config, building.TrainingQueue[0]);
                    building.TrainingTicksTotal = building.TrainingTicksRemaining;
                }
                else
                {
                    building.TrainingTicksRemaining = 0;
                    building.TrainingTicksTotal = 0;
                }
            }
        }

        private void ProcessUpgradeTowerCommand(UpgradeTowerCommand cmd)
        {
            cmd.Execute(this);
        }

        private void ProcessCancelUpgradeCommand(CancelUpgradeCommand cmd)
        {
            var building = BuildingRegistry.GetBuilding(cmd.BuildingId);
            if (building == null || building.IsDestroyed) return;
            if (building.PlayerId != cmd.PlayerId) return;
            if (building.Type != BuildingType.Tower || !building.IsUpgrading) return;
            if (cmd.QueueIndex < 0 || cmd.QueueIndex >= building.UpgradeQueue.Count) return;

            var upgradeType = building.UpgradeQueue[cmd.QueueIndex];
            int cost;
            switch (upgradeType)
            {
                case TowerUpgradeType.ArrowSlits: cost = config.ArrowSlitsWoodCost; break;
                case TowerUpgradeType.CannonEmplacement: cost = config.CannonEmplacementWoodCost; break;
                case TowerUpgradeType.StoneUpgrade: cost = config.StoneUpgradeWoodCost; break;
                case TowerUpgradeType.VisionUpgrade: cost = config.VisionUpgradeWoodCost; break;
                default: cost = 100; break; // Default fallback
            }

            var resources = ResourceManager.GetPlayerResources(cmd.PlayerId);
            resources.Wood += cost;

            building.UpgradeQueue.RemoveAt(cmd.QueueIndex);

            // If we cancelled the current upgrade, start the next one or stop upgrading
            if (cmd.QueueIndex == 0)
            {
                if (building.UpgradeQueue.Count > 0)
                {
                    building.CurrentUpgrade = building.UpgradeQueue[0];
                    building.UpgradeTicksRemaining = config.TowerUpgradeTicks;
                    building.UpgradeTicksTotal = config.TowerUpgradeTicks;
                }
                else
                {
                    building.IsUpgrading = false;
                    building.UpgradeTicksRemaining = 0;
                    building.UpgradeTicksTotal = 0;
                }
            }
        }

        private void ProcessSetRallyPointCommand(SetRallyPointCommand cmd)
        {
            var building = BuildingRegistry.GetBuilding(cmd.BuildingId);
            if (building == null || building.IsDestroyed) return;
            if (building.PlayerId != cmd.PlayerId) return;

            building.HasRallyPoint = true;
            building.RallyPointUnitId = -1;

            // Rally to a unit (e.g. sheep)
            if (cmd.TargetUnitId >= 0)
            {
                var targetUnit = UnitRegistry.GetUnit(cmd.TargetUnitId);
                if (targetUnit != null && targetUnit.State != UnitState.Dead)
                {
                    building.RallyPoint = targetUnit.SimPosition;
                    building.RallyPointOnResource = false;
                    building.RallyPointOnConstruction = false;
                    building.RallyPointConstructionBuildingId = -1;
                    building.RallyPointUnitId = cmd.TargetUnitId;
                    return;
                }
            }

            // Snap rally point to resource center if set on a resource node
            if (cmd.ResourceNodeId >= 0)
            {
                var node = MapData.GetResourceNode(cmd.ResourceNodeId);
                if (node != null && !node.IsDepleted)
                {
                    building.RallyPoint = node.Position;
                    building.RallyPointOnResource = true;
                    building.RallyPointResourceType = node.Type;
                }
                else
                {
                    building.RallyPoint = cmd.Position;
                    building.RallyPointOnResource = false;
                }
                building.RallyPointOnConstruction = false;
                building.RallyPointConstructionBuildingId = -1;
            }
            // Rally to an under-construction building
            else if (cmd.TargetBuildingId >= 0)
            {
                var target = BuildingRegistry.GetBuilding(cmd.TargetBuildingId);
                if (target != null && target.IsUnderConstruction && !target.IsDestroyed)
                {
                    building.RallyPoint = target.SimPosition;
                    building.RallyPointOnResource = false;
                    building.RallyPointOnConstruction = true;
                    building.RallyPointConstructionBuildingId = cmd.TargetBuildingId;
                }
                else
                {
                    building.RallyPoint = cmd.Position;
                    building.RallyPointOnResource = false;
                    building.RallyPointOnConstruction = false;
                    building.RallyPointConstructionBuildingId = -1;
                }
            }
            else
            {
                building.RallyPoint = cmd.Position;
                building.RallyPointOnResource = false;
                building.RallyPointOnConstruction = false;
                building.RallyPointConstructionBuildingId = -1;
            }
        }

        private void ProcessPlaceBuildingCommand(PlaceBuildingCommand cmd)
        {
            var resources = ResourceManager.GetPlayerResources(cmd.PlayerId);

            // Landmark placement has special validation and cost
            if (cmd.BuildingType == BuildingType.Landmark)
            {
                if (cmd.LandmarkIdValue < 0) return;
                var landmarkId = (LandmarkId)cmd.LandmarkIdValue;
                var def = LandmarkDefinitions.Get(landmarkId);

                // Validate: civ matches, correct age, not already aging up, can afford
                if (def.Civ != GetPlayerCivilization(cmd.PlayerId)) return;
                if (playerAges[cmd.PlayerId] != def.TargetAge - 1) return;
                if (playerAgingUp[cmd.PlayerId]) return;
                if (resources.Food < def.FoodCost || resources.Gold < def.GoldCost) return;

                int footprintW = def.FootprintWidth;
                int footprintH = def.FootprintHeight;
                int border = 1;
                for (int x = cmd.TileX - border; x < cmd.TileX + footprintW + border; x++)
                    for (int z = cmd.TileZ - border; z < cmd.TileZ + footprintH + border; z++)
                        if (!MapData.IsBuildable(x, z)) return;

                resources.Food -= def.FoodCost;
                resources.Gold -= def.GoldCost;

                bool hasVillagers = cmd.VillagerUnitIds != null && cmd.VillagerUnitIds.Length > 0;
                var building = CreateBuilding(cmd.PlayerId, BuildingType.Landmark, cmd.TileX, cmd.TileZ, underConstruction: true);
                building.LandmarkId = landmarkId;
                building.MaxHealth = def.MaxHealth;
                building.Armor = def.Armor;
                building.ConstructionTicksTotal = def.ConstructionTicks;
                building.ConstructionTicksRemaining = def.ConstructionTicks;
                playerAgingUp[cmd.PlayerId] = true;
                playerAgingUpBuildingId[cmd.PlayerId] = building.Id;
                OnBuildingCreated?.Invoke(building);

                if (hasVillagers)
                {
                    if (cmd.IsQueued)
                    {
                        for (int i = 0; i < cmd.VillagerUnitIds.Length; i++)
                        {
                            var villager = UnitRegistry.GetUnit(cmd.VillagerUnitIds[i]);
                            if (villager == null || villager.State == UnitState.Dead || villager.PlayerId != cmd.PlayerId)
                                continue;
                            villager.CommandQueue.Add(QueuedCommand.ConstructWaypoint(building.Id));
                            if (villager.State == UnitState.Idle)
                                PopAndExecuteNextQueuedCommand(villager);
                        }
                    }
                    else
                    {
                        var occupiedTiles = new HashSet<Vector2Int>();
                        for (int i = 0; i < cmd.VillagerUnitIds.Length; i++)
                        {
                            var villager = UnitRegistry.GetUnit(cmd.VillagerUnitIds[i]);
                            if (villager == null || villager.State == UnitState.Dead || villager.PlayerId != cmd.PlayerId)
                                continue;
                            villager.ClearCommandQueue();
                            villager.ClearSavedPath();
                            villager.ClearFormation();
                            villager.CombatTargetId = -1;
                            villager.CombatTargetBuildingId = -1;
                            villager.TargetResourceNodeId = -1;
                            villager.ConstructionTargetBuildingId = building.Id;
                            villager.GatherTimer = Fixed32.Zero;
                            villager.PlayerCommanded = true;
                            villager.DropOffBuildingId = -1;
                            villager.TargetGarrisonBuildingId = -1;
                            villager.ClearPatrol();

                            Vector2Int adjTile = FindNearestWalkableAdjacentTile(building, villager.SimPosition, occupiedTiles);
                            occupiedTiles.Add(adjTile);
                            Vector2Int startTile = MapData.WorldToTile(villager.SimPosition);
                            var path = GridPathfinder.FindPath(MapData, startTile, adjTile, villager.PlayerId, BuildingRegistry);
                            if (path.Count > 0)
                            {
                                villager.SetPath(path);
                                villager.FinalDestination = MapData.TileToWorldFixed(adjTile.x, adjTile.y);
                                villager.State = UnitState.MovingToBuild;
                            }
                            else
                            {
                                villager.State = UnitState.Constructing;
                            }
                        }
                    }
                }
                return;
            }

            // Age gate for non-landmark buildings
            if (playerAges[cmd.PlayerId] < LandmarkDefinitions.GetBuildingRequiredAge(cmd.BuildingType)) return;

            int cost = GetBuildingWoodCost(cmd.BuildingType);
            int stoneCost = GetBuildingStoneCost(cmd.BuildingType);
            int foodCost = GetBuildingFoodCost(cmd.BuildingType);
            int goldCost = GetBuildingGoldCost(cmd.BuildingType);
            if (resources.Wood < cost || resources.Stone < stoneCost || resources.Food < foodCost || resources.Gold < goldCost) return;

            bool hasVillagers2 = cmd.VillagerUnitIds != null && cmd.VillagerUnitIds.Length > 0;

            // Get footprint size
            int footprintW2, footprintH2;
            switch (cmd.BuildingType)
            {
                case BuildingType.Barracks:
                    footprintW2 = config.BarracksFootprintWidth;
                    footprintH2 = config.BarracksFootprintHeight;
                    break;
                case BuildingType.TownCenter:
                    footprintW2 = config.TownCenterFootprintWidth;
                    footprintH2 = config.TownCenterFootprintHeight;
                    break;
                case BuildingType.Wall:
                    footprintW2 = config.WallFootprintWidth;
                    footprintH2 = config.WallFootprintHeight;
                    break;
                case BuildingType.Mill:
                    footprintW2 = config.MillFootprintWidth;
                    footprintH2 = config.MillFootprintHeight;
                    break;
                case BuildingType.LumberYard:
                    footprintW2 = config.LumberYardFootprintWidth;
                    footprintH2 = config.LumberYardFootprintHeight;
                    break;
                case BuildingType.Mine:
                    footprintW2 = config.MineFootprintWidth;
                    footprintH2 = config.MineFootprintHeight;
                    break;
                case BuildingType.ArcheryRange:
                    footprintW2 = config.ArcheryRangeFootprintWidth;
                    footprintH2 = config.ArcheryRangeFootprintHeight;
                    break;
                case BuildingType.Stables:
                    footprintW2 = config.StablesFootprintWidth;
                    footprintH2 = config.StablesFootprintHeight;
                    break;
                case BuildingType.Farm:
                    footprintW2 = config.FarmFootprintWidth;
                    footprintH2 = config.FarmFootprintHeight;
                    break;
                case BuildingType.Monastery:
                    footprintW2 = config.MonasteryFootprintWidth;
                    footprintH2 = config.MonasteryFootprintHeight;
                    break;
                case BuildingType.Blacksmith:
                    footprintW2 = config.BlacksmithFootprintWidth;
                    footprintH2 = config.BlacksmithFootprintHeight;
                    break;
                case BuildingType.Market:
                    footprintW2 = config.MarketFootprintWidth;
                    footprintH2 = config.MarketFootprintHeight;
                    break;
                case BuildingType.University:
                    footprintW2 = config.UniversityFootprintWidth;
                    footprintH2 = config.UniversityFootprintHeight;
                    break;
                case BuildingType.SiegeWorkshop:
                    footprintW2 = config.SiegeWorkshopFootprintWidth;
                    footprintH2 = config.SiegeWorkshopFootprintHeight;
                    break;
                case BuildingType.Keep:
                    footprintW2 = config.KeepFootprintWidth;
                    footprintH2 = config.KeepFootprintHeight;
                    break;
                case BuildingType.StoneWall:
                    footprintW2 = config.StoneWallFootprintWidth;
                    footprintH2 = config.StoneWallFootprintHeight;
                    break;
                case BuildingType.StoneGate:
                    footprintW2 = config.StoneGateFootprintWidth;
                    footprintH2 = config.StoneGateFootprintHeight;
                    break;
                case BuildingType.WoodGate:
                    footprintW2 = config.WoodGateFootprintWidth;
                    footprintH2 = config.WoodGateFootprintHeight;
                    break;
                case BuildingType.Wonder:
                    footprintW2 = config.WonderFootprintWidth;
                    footprintH2 = config.WonderFootprintHeight;
                    break;
                default:
                    footprintW2 = config.HouseFootprintWidth;
                    footprintH2 = config.HouseFootprintHeight;
                    break;
            }

            // Validate footprint + border area is buildable
            int border2 = (cmd.BuildingType == BuildingType.Wall || cmd.BuildingType == BuildingType.Farm || cmd.BuildingType == BuildingType.StoneWall || cmd.BuildingType == BuildingType.StoneGate || cmd.BuildingType == BuildingType.WoodGate) ? 0 : 1;
            bool isFarm = cmd.BuildingType == BuildingType.Farm;
            for (int x = cmd.TileX - border2; x < cmd.TileX + footprintW2 + border2; x++)
                for (int z = cmd.TileZ - border2; z < cmd.TileZ + footprintH2 + border2; z++)
                    if (isFarm ? !MapData.IsBuildableForFarm(x, z) : !MapData.IsBuildable(x, z)) return;

            resources.Wood -= cost;
            resources.Stone -= stoneCost;
            resources.Food -= foodCost;
            resources.Gold -= goldCost;
            var building2 = CreateBuilding(cmd.PlayerId, cmd.BuildingType, cmd.TileX, cmd.TileZ, underConstruction: hasVillagers2);
            OnBuildingCreated?.Invoke(building2);
            if (!hasVillagers2)
                EjectUnitsFromBuildingFootprint(building2);

            // Send all villagers to construct
            if (hasVillagers2)
            {
                if (cmd.IsQueued)
                {
                    // Shift-queue: append construct to each villager's command queue
                    for (int i = 0; i < cmd.VillagerUnitIds.Length; i++)
                    {
                        var villager = UnitRegistry.GetUnit(cmd.VillagerUnitIds[i]);
                        if (villager == null || villager.State == UnitState.Dead || villager.PlayerId != cmd.PlayerId)
                            continue;

                        villager.CommandQueue.Add(QueuedCommand.ConstructWaypoint(building2.Id));

                        if (villager.State == UnitState.Idle)
                            PopAndExecuteNextQueuedCommand(villager);
                    }
                }
                else
                {
                    var occupiedTiles = new HashSet<Vector2Int>();
                    for (int i = 0; i < cmd.VillagerUnitIds.Length; i++)
                    {
                        var villager = UnitRegistry.GetUnit(cmd.VillagerUnitIds[i]);
                        if (villager == null || villager.State == UnitState.Dead || villager.PlayerId != cmd.PlayerId)
                            continue;

                        villager.ClearCommandQueue();
                        villager.ClearSavedPath();
                        villager.ClearFormation();
                        villager.CombatTargetId = -1;
                        villager.CombatTargetBuildingId = -1;
                        villager.TargetResourceNodeId = -1;
                        villager.ConstructionTargetBuildingId = building2.Id;
                        villager.GatherTimer = Fixed32.Zero;
                        villager.PlayerCommanded = true;
                        villager.DropOffBuildingId = -1;
                        villager.TargetGarrisonBuildingId = -1;

                        villager.ClearPatrol();

                        Vector2Int adjTile = FindNearestWalkableAdjacentTile(building2, villager.SimPosition, occupiedTiles);
                        occupiedTiles.Add(adjTile);
                        Vector2Int startTile = MapData.WorldToTile(villager.SimPosition);

                        var path = GridPathfinder.FindPath(MapData, startTile, adjTile, villager.PlayerId, BuildingRegistry);
                        if (path.Count > 0)
                        {
                            villager.SetPath(path);
                            villager.FinalDestination = MapData.TileToWorldFixed(adjTile.x, adjTile.y);
                            villager.State = UnitState.MovingToBuild;
                        }
                        else
                        {
                            villager.State = UnitState.Constructing;
                        }
                    }
                }
            }
        }

        private void ProcessPlaceWallCommand(PlaceWallCommand cmd)
        {
            // Compute tile line using Bresenham's algorithm
            var tiles = WallLineHelper.ComputeWallLine(cmd.StartTileX, cmd.StartTileZ, cmd.EndTileX, cmd.EndTileZ);

            // Filter to buildable tiles only (no existing buildings or templates)
            var validTiles = new List<Vector2Int>();
            for (int i = 0; i < tiles.Count; i++)
            {
                if (MapData.IsBuildable(tiles[i].x, tiles[i].y))
                    validTiles.Add(tiles[i]);
            }
            if (validTiles.Count == 0) return;

            // Validate total cost
            BuildingType wallType = cmd.WallBuildingType;
            int woodPerSegment = GetBuildingWoodCost(wallType);
            int stonePerSegment = GetBuildingStoneCost(wallType);
            int totalWood = woodPerSegment * validTiles.Count;
            int totalStone = stonePerSegment * validTiles.Count;
            var resources = ResourceManager.GetPlayerResources(cmd.PlayerId);
            if (resources.Wood < totalWood || resources.Stone < totalStone) return;

            resources.Wood -= totalWood;
            resources.Stone -= totalStone;
            int wallGroupId = nextWallGroupId++;
            bool hasVillagers = cmd.VillagerUnitIds != null && cmd.VillagerUnitIds.Length > 0;

            // Create wall segment buildings
            var createdBuildings = new List<BuildingData>();
            for (int i = 0; i < validTiles.Count; i++)
            {
                var tile = validTiles[i];
                var building = CreateBuilding(cmd.PlayerId, wallType, tile.x, tile.y, underConstruction: hasVillagers);
                building.WallGroupId = wallGroupId;
                if (cmd.IsGate) building.IsGate = true;
                createdBuildings.Add(building);
                OnBuildingCreated?.Invoke(building);
                if (!hasVillagers)
                    EjectUnitsFromBuildingFootprint(building);
            }

            // Assign villagers to build segments sequentially
            if (hasVillagers && createdBuildings.Count > 0)
            {
                for (int v = 0; v < cmd.VillagerUnitIds.Length; v++)
                {
                    var villager = UnitRegistry.GetUnit(cmd.VillagerUnitIds[v]);
                    if (villager == null || villager.State == UnitState.Dead || villager.PlayerId != cmd.PlayerId)
                        continue;

                    if (!cmd.IsQueued)
                        villager.ClearCommandQueue();

                    // Assign first segment as immediate target, rest as queued
                    int startIdx = 0;
                    if (!cmd.IsQueued)
                    {
                        var firstBuilding = createdBuildings[startIdx];
                        startIdx = 1;

                        villager.ClearSavedPath();
                        villager.ClearFormation();
                        villager.CombatTargetId = -1;
                        villager.CombatTargetBuildingId = -1;
                        villager.TargetResourceNodeId = -1;
                        villager.ConstructionTargetBuildingId = firstBuilding.Id;
                        villager.GatherTimer = Fixed32.Zero;
                        villager.PlayerCommanded = true;
                        villager.DropOffBuildingId = -1;
                        villager.TargetGarrisonBuildingId = -1;

                        villager.ClearPatrol();

                        Vector2Int adjTile = FindNearestWalkableAdjacentTile(firstBuilding, villager.SimPosition);
                        Vector2Int startTile = MapData.WorldToTile(villager.SimPosition);
                        var path = GridPathfinder.FindPath(MapData, startTile, adjTile, villager.PlayerId, BuildingRegistry);
                        if (path.Count > 0)
                        {
                            villager.SetPath(path);
                            villager.FinalDestination = MapData.TileToWorldFixed(adjTile.x, adjTile.y);
                            villager.State = UnitState.MovingToBuild;
                        }
                        else
                        {
                            villager.State = UnitState.Constructing;
                        }
                    }

                    // Queue remaining segments
                    for (int s = startIdx; s < createdBuildings.Count; s++)
                        villager.CommandQueue.Add(QueuedCommand.ConstructWaypoint(createdBuildings[s].Id));

                    // If we queued and villager is idle, pop first queued command
                    if (cmd.IsQueued && villager.State == UnitState.Idle)
                        PopAndExecuteNextQueuedCommand(villager);
                }
            }
        }

        private void ProcessConvertToGateCommand(ConvertToGateCommand cmd)
        {
            var building = BuildingRegistry.GetBuilding(cmd.BuildingId);
            if (building == null || building.IsDestroyed) return;
            if (building.Type != BuildingType.Wall && building.Type != BuildingType.StoneWall && building.Type != BuildingType.StoneGate && building.Type != BuildingType.WoodGate) return;
            if (building.PlayerId != cmd.PlayerId) return;

            building.IsGate = !building.IsGate;

            // Gates now remain as building tiles but allow player-specific access
            // Only eject units when converting back to wall (non-gate)
            if (!building.IsUnderConstruction && !building.IsGate)
            {
                EjectUnitsFromBuildingFootprint(building);
            }
        }

        private void ProcessConstructBuildingCommand(ConstructBuildingCommand cmd)
        {
            var building = BuildingRegistry.GetBuilding(cmd.TargetBuildingId);
            if (building == null || building.IsDestroyed) return;
            if (!building.IsUnderConstruction) return;
            if (!AreAllies(building.PlayerId, cmd.PlayerId)) return;

            var occupiedTiles = new HashSet<Vector2Int>();
            for (int i = 0; i < cmd.UnitIds.Length; i++)
            {
                var unit = UnitRegistry.GetUnit(cmd.UnitIds[i]);
                if (unit == null || unit.State == UnitState.Dead) continue;
                if (unit.PlayerId != cmd.PlayerId) continue;

                if (cmd.IsQueued)
                {
                    var qc = QueuedCommand.ConstructWaypoint(cmd.TargetBuildingId);
                    unit.CommandQueue.Add(qc);

                    if (unit.State == UnitState.Idle)
                        PopAndExecuteNextQueuedCommand(unit);
                }
                else
                {
                    unit.ClearCommandQueue();
                    unit.ClearSavedPath();
                    unit.ClearFormation();
                    unit.CombatTargetId = -1;
                    unit.CombatTargetBuildingId = -1;
                    unit.TargetResourceNodeId = -1;
                    unit.ConstructionTargetBuildingId = cmd.TargetBuildingId;
                    unit.GatherTimer = Fixed32.Zero;
                    unit.PlayerCommanded = true;
                    unit.DropOffBuildingId = -1;
                    unit.TargetGarrisonBuildingId = -1;

                    unit.ClearPatrol();

                    var triedTiles = new HashSet<Vector2Int>(occupiedTiles);
                    Vector2Int startTile = MapData.WorldToTile(unit.SimPosition);
                    bool assigned = false;
                    int constructAttempts = 0;

                    while (true)
                    {
                        if (++constructAttempts > 4) break; // Cap retry attempts
                        Vector2Int adjTile = FindNearestWalkableAdjacentTile(building, unit.SimPosition, triedTiles);
                        if (triedTiles.Contains(adjTile)) break; // All tiles exhausted

                        var path = GridPathfinder.FindPath(MapData, startTile, adjTile, unit.PlayerId, BuildingRegistry);
                        if (path.Count > 0)
                        {
                            occupiedTiles.Add(adjTile);
                            unit.SetPath(path);
                            unit.FinalDestination = MapData.TileToWorldFixed(adjTile.x, adjTile.y);
                            unit.State = UnitState.MovingToBuild;
                            assigned = true;
                            break;
                        }
                        triedTiles.Add(adjTile);
                    }

                    if (!assigned)
                    {
                        unit.State = UnitState.Constructing;
                    }
                }
            }
        }

        private void ProcessDropOffCommand(DropOffCommand cmd)
        {
            var building = BuildingRegistry.GetBuilding(cmd.TargetBuildingId);
            if (building == null || building.IsDestroyed) return;
            if (building.PlayerId != cmd.PlayerId) return;
            if (building.IsUnderConstruction) return;
            if (!IsDropOffBuilding(building.Type)) return;

            for (int i = 0; i < cmd.UnitIds.Length; i++)
            {
                var unit = UnitRegistry.GetUnit(cmd.UnitIds[i]);
                if (unit == null || unit.State == UnitState.Dead) continue;
                if (unit.PlayerId != cmd.PlayerId) continue;

                if (cmd.IsQueued)
                {
                    var qc = QueuedCommand.DropOffWaypoint(cmd.TargetBuildingId);
                    unit.CommandQueue.Add(qc);

                    if (unit.State == UnitState.Idle)
                        PopAndExecuteNextQueuedCommand(unit);
                }
                else
                {
                    // Skip if not carrying anything
                    if (unit.CarriedResourceAmount <= 0) continue;

                    unit.ClearCommandQueue();
                    unit.ClearSavedPath();
                    unit.ClearFormation();
                    unit.CombatTargetId = -1;
                    unit.CombatTargetBuildingId = -1;
                    unit.ConstructionTargetBuildingId = -1;
                    unit.GatherTimer = Fixed32.Zero;
                    unit.DropOffBuildingId = cmd.TargetBuildingId;
                    unit.PlayerCommanded = true;

                    Vector2Int adjTile = FindNearestWalkableAdjacentTile(building, unit.SimPosition);
                    Vector2Int startTile = MapData.WorldToTile(unit.SimPosition);

                    var path = GridPathfinder.FindPath(MapData, startTile, adjTile, unit.PlayerId, BuildingRegistry);
                    if (path.Count > 0)
                    {
                        unit.SetPath(path);
                        unit.FinalDestination = MapData.TileToWorldFixed(adjTile.x, adjTile.y);
                        unit.State = UnitState.MovingToDropoff;
                    }
                    else
                    {
                        unit.State = UnitState.DroppingOff;
                    }
                }
            }
        }

        private void CleanUpDestroyedBuilding(int buildingId)
        {
            var building = BuildingRegistry.GetBuilding(buildingId);
            if (building != null)
            {
                // Eject garrisoned units before destroying building
                if (building.GarrisonCount > 0)
                    UngarrisonAll(building);

                // Cancel age-up if in-progress landmark is destroyed
                if (building.Type == BuildingType.Landmark)
                {
                    int pid = building.PlayerId;
                    if (playerAgingUp[pid] && playerAgingUpBuildingId[pid] == building.Id)
                    {
                        playerAgingUp[pid] = false;
                        playerAgingUpBuildingId[pid] = -1;
                    }
                }

                if (building.Type == BuildingType.Farm && building.LinkedResourceNodeId >= 0)
                    MapData.RemoveResourceNode(building.LinkedResourceNodeId);
                MapData.ClearFoundationBorder(building.OriginTileX, building.OriginTileZ,
                    building.TileFootprintWidth, building.TileFootprintHeight, building.FoundationBorder);
                MapData.ClearBuildingTiles(building.OriginTileX, building.OriginTileZ,
                    building.TileFootprintWidth, building.TileFootprintHeight);
                BuildingRegistry.RemoveBuilding(buildingId);
            }
            OnBuildingDestroyed?.Invoke(buildingId);
        }

        private void AutoTaskFarmBuilders(List<(int unitId, int buildingId)> idledVillagerPairs)
        {
            if (idledVillagerPairs.Count == 0) return;

            HashSet<int> takenFarmIds = null;

            for (int i = 0; i < idledVillagerPairs.Count; i++)
            {
                var unit = UnitRegistry.GetUnit(idledVillagerPairs[i].unitId);
                if (unit == null || unit.State != UnitState.Idle) continue;

                // Only auto-task to farms if villager just built a farm or a food drop-off building
                var building = BuildingRegistry.GetBuilding(idledVillagerPairs[i].buildingId);
                if (building == null) continue;
                bool builtFarm = building.Type == BuildingType.Farm;
                bool builtFoodDropOff = AcceptsResourceType(building.Type, ResourceType.Food);
                if (!builtFarm && !builtFoodDropOff) continue;

                // Try the farm they just built first
                int farmNodeId = -1;
                if (builtFarm && building.LinkedResourceNodeId >= 0)
                {
                    var farmNode = MapData.GetResourceNode(building.LinkedResourceNodeId);
                    if (farmNode != null && !farmNode.IsDepleted
                        && !IsFarmNodeOccupied(building.LinkedResourceNodeId, unit)
                        && (takenFarmIds == null || !takenFarmIds.Contains(building.LinkedResourceNodeId)))
                    {
                        farmNodeId = building.LinkedResourceNodeId;
                    }
                }

                // Fallback: find nearest unoccupied farm
                if (farmNodeId < 0)
                {
                    farmNodeId = FindNearbyUnoccupiedFarm(unit.SimPosition, takenFarmIds);
                }

                if (farmNodeId < 0) continue;

                var node = MapData.GetResourceNode(farmNodeId);
                if (node == null) continue;

                takenFarmIds ??= new HashSet<int>();
                takenFarmIds.Add(farmNodeId);

                Vector2Int farmTile = MapData.WorldToTile(node.Position);
                Vector2Int startTile = MapData.WorldToTile(unit.SimPosition);
                var path = GridPathfinder.FindPath(MapData, startTile, farmTile, unit.PlayerId, BuildingRegistry);
                if (path.Count > 0)
                {
                    unit.SetPath(path);
                    unit.ClearFormation();
                    unit.FinalDestination = MapData.TileToWorldFixed(farmTile.x, farmTile.y);
                    unit.State = UnitState.MovingToGather;
                    unit.TargetResourceNodeId = farmNodeId;
                    unit.ConstructionTargetBuildingId = -1;
                    unit.GatherTimer = Fixed32.Zero;
                    unit.ClearSavedPath();
                    unit.CombatTargetId = -1;
                    unit.CombatTargetBuildingId = -1;
                    unit.DropOffBuildingId = -1;
                    unit.TargetGarrisonBuildingId = -1;

                    unit.ClearPatrol();
                }
                else
                {
                    unit.State = UnitState.Gathering;
                    unit.TargetResourceNodeId = farmNodeId;
                    unit.ConstructionTargetBuildingId = -1;
                    unit.GatherTimer = Fixed32.Zero;
                    unit.CombatTargetId = -1;
                    unit.CombatTargetBuildingId = -1;
                    unit.DropOffBuildingId = -1;
                    unit.TargetGarrisonBuildingId = -1;

                    unit.ClearPatrol();
                }
            }
        }

        private void AutoSeekNearbyConstruction(List<(int unitId, int buildingId)> idledVillagerPairs)
        {
            if (idledVillagerPairs.Count == 0) return;

            for (int i = 0; i < idledVillagerPairs.Count; i++)
            {
                var unit = UnitRegistry.GetUnit(idledVillagerPairs[i].unitId);
                if (unit == null || unit.State != UnitState.Idle) continue;

                // Find nearest unfinished building within vision range
                Fixed32 visionRangeSq = unit.DetectionRange * unit.DetectionRange;
                BuildingData nearest = null;
                Fixed32 nearestDistSq = visionRangeSq;

                var buildings = BuildingRegistry.GetAllBuildings();
                for (int b = 0; b < buildings.Count; b++)
                {
                    var building = buildings[b];
                    if (!building.IsUnderConstruction || building.IsDestroyed) continue;
                    if (!AreAllies(building.PlayerId, unit.PlayerId)) continue;

                    FixedVector3 diff = building.SimPosition - unit.SimPosition;
                    // Overflow guard: skip if axis distance exceeds detection range
                    if (Fixed32.Abs(diff.x) > unit.DetectionRange || Fixed32.Abs(diff.z) > unit.DetectionRange) continue;

                    Fixed32 distSq = diff.x * diff.x + diff.z * diff.z;
                    if (distSq < nearestDistSq)
                    {
                        nearestDistSq = distSq;
                        nearest = building;
                    }
                }

                if (nearest == null) continue;

                // Build occupiedTiles from other units already heading to this building
                var occupiedTiles = new HashSet<Vector2Int>();
                var allUnits = UnitRegistry.GetAllUnits();
                for (int u = 0; u < allUnits.Count; u++)
                {
                    var other = allUnits[u];
                    if (other == unit || other.State == UnitState.Dead) continue;
                    if (other.ConstructionTargetBuildingId != nearest.Id) continue;
                    if (other.State == UnitState.MovingToBuild || other.State == UnitState.Constructing)
                        occupiedTiles.Add(MapData.WorldToTile(other.FinalDestination));
                }

                unit.ClearFormation();
                unit.CombatTargetId = -1;
                unit.CombatTargetBuildingId = -1;
                unit.TargetResourceNodeId = -1;
                unit.ConstructionTargetBuildingId = nearest.Id;
                unit.GatherTimer = Fixed32.Zero;

                Vector2Int adjTile = FindNearestWalkableAdjacentTile(nearest, unit.SimPosition, occupiedTiles);
                Vector2Int startTile = MapData.WorldToTile(unit.SimPosition);
                var path = GridPathfinder.FindPath(MapData, startTile, adjTile, unit.PlayerId, BuildingRegistry);
                if (path.Count > 0)
                {
                    unit.SetPath(path);
                    unit.FinalDestination = MapData.TileToWorldFixed(adjTile.x, adjTile.y);
                    unit.State = UnitState.MovingToBuild;
                }
                else
                {
                    unit.State = UnitState.Constructing;
                }
            }
        }

        private void AutoSeekNearbyGathering(List<(int unitId, int buildingId)> idledVillagerPairs)
        {
            if (idledVillagerPairs.Count == 0) return;

            for (int i = 0; i < idledVillagerPairs.Count; i++)
            {
                var unit = UnitRegistry.GetUnit(idledVillagerPairs[i].unitId);
                if (unit == null || unit.State != UnitState.Idle) continue;
                if (unit.HasQueuedCommands) continue;

                var building = BuildingRegistry.GetBuilding(idledVillagerPairs[i].buildingId);
                if (building == null || !IsDropOffBuilding(building.Type)) continue;

                Fixed32 visionRange = unit.DetectionRange;
                Fixed32 visionRangeSq = visionRange * visionRange;
                Fixed32 bestDistSq = visionRangeSq;
                int bestNodeId = -1;
                int bestPriority = int.MaxValue;
                bool isTownCenter = building.Type == BuildingType.TownCenter;

                foreach (var node in MapData.GetAllResourceNodes())
                {
                    if (node.IsDepleted) continue;
                    if (!AcceptsResourceType(building.Type, node.Type)) continue;
                    if (node.IsFarmNode && IsFarmNodeOccupied(node.Id, unit)) continue;

                    FixedVector3 diff = node.Position - unit.SimPosition;
                    if (Fixed32.Abs(diff.x) > visionRange || Fixed32.Abs(diff.z) > visionRange) continue;

                    Fixed32 distSq = diff.x * diff.x + diff.z * diff.z;
                    if (distSq >= visionRangeSq) continue;

                    if (isTownCenter)
                    {
                        int priority = ResourcePriority(node.Type);
                        if (distSq < bestDistSq || (distSq == bestDistSq && (priority < bestPriority || (priority == bestPriority && node.Id < bestNodeId))))
                        {
                            bestDistSq = distSq;
                            bestNodeId = node.Id;
                            bestPriority = priority;
                        }
                    }
                    else
                    {
                        if (distSq < bestDistSq || (distSq == bestDistSq && node.Id < bestNodeId))
                        {
                            bestDistSq = distSq;
                            bestNodeId = node.Id;
                        }
                    }
                }

                if (bestNodeId < 0) continue;

                var bestNode = MapData.GetResourceNode(bestNodeId);
                Vector2Int nodeOrigin = new Vector2Int(bestNode.TileX, bestNode.TileZ);

                // Farm nodes: villager stands on the farm tile
                if (bestNode.IsFarmNode)
                {
                    Vector2Int farmTile = MapData.WorldToTile(bestNode.Position);
                    Vector2Int startTile = MapData.WorldToTile(unit.SimPosition);
                    var path = GridPathfinder.FindPath(MapData, startTile, farmTile, unit.PlayerId, BuildingRegistry);
                    if (path.Count == 0) continue;

                    unit.SetPath(path);
                    unit.ClearFormation();
                    unit.FinalDestination = MapData.TileToWorldFixed(farmTile.x, farmTile.y);
                    unit.State = UnitState.MovingToGather;
                    unit.TargetResourceNodeId = bestNodeId;
                    unit.ConstructionTargetBuildingId = -1;
                    unit.GatherTimer = Fixed32.Zero;
                    unit.ClearSavedPath();
                    unit.CombatTargetId = -1;
                    unit.CombatTargetBuildingId = -1;
                    unit.DropOffBuildingId = -1;
                    unit.TargetGarrisonBuildingId = -1;

                    unit.ClearPatrol();
                    continue;
                }

                var occupiedTiles = new HashSet<Vector2Int>();
                var allUnits = UnitRegistry.GetAllUnits();
                for (int u = 0; u < allUnits.Count; u++)
                {
                    var other = allUnits[u];
                    if (other == unit || other.State == UnitState.Dead) continue;
                    if (other.TargetResourceNodeId != bestNodeId) continue;
                    if (other.State == UnitState.MovingToGather || other.State == UnitState.Gathering)
                        occupiedTiles.Add(MapData.WorldToTile(other.FinalDestination));
                }

                // Retry loop: try all walkable adjacent tiles until one is pathable
                Vector2Int startTile2 = MapData.WorldToTile(unit.SimPosition);
                var triedTiles = new HashSet<Vector2Int>(occupiedTiles);
                bool found = false;
                Vector2Int adjTile = default;
                List<Vector2Int> path2 = null;
                int seekAttempts = 0;
                while (true)
                {
                    if (++seekAttempts > 4) break; // Cap retry attempts
                    adjTile = FindNearestWalkableAdjacentTileForResource(nodeOrigin, bestNode.FootprintWidth, bestNode.FootprintHeight, unit.SimPosition, triedTiles);
                    if (adjTile == nodeOrigin) break;
                    path2 = GridPathfinder.FindPath(MapData, startTile2, adjTile, unit.PlayerId, BuildingRegistry);
                    if (path2.Count > 0) { found = true; break; }
                    triedTiles.Add(adjTile);
                }
                if (!found) continue;

                unit.SetPath(path2);
                unit.ClearFormation();
                unit.FinalDestination = MapData.TileToWorldFixed(adjTile.x, adjTile.y);
                unit.State = UnitState.MovingToGather;
                unit.TargetResourceNodeId = bestNodeId;
                unit.ConstructionTargetBuildingId = -1;
                unit.GatherTimer = Fixed32.Zero;
                unit.ClearSavedPath();
                unit.CombatTargetId = -1;
                unit.CombatTargetBuildingId = -1;
                unit.DropOffBuildingId = -1;
                unit.TargetGarrisonBuildingId = -1;

                unit.ClearPatrol();
            }
        }

        private static int ResourcePriority(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Food: return 0;
                case ResourceType.Wood: return 1;
                case ResourceType.Gold: return 2;
                case ResourceType.Stone: return 3;
                default: return 4;
            }
        }

        public int GetBuildingWoodCost(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.House: return config.HouseWoodCost;
                case BuildingType.Barracks: return config.BarracksWoodCost;
                case BuildingType.TownCenter: return config.TownCenterWoodCost;
                case BuildingType.Wall: return config.WallWoodCost;
                case BuildingType.Mill: return config.MillWoodCost;
                case BuildingType.LumberYard: return config.LumberYardWoodCost;
                case BuildingType.Mine: return config.MineWoodCost;
                case BuildingType.ArcheryRange: return config.ArcheryRangeWoodCost;
                case BuildingType.Stables: return config.StablesWoodCost;
                case BuildingType.Farm: return config.FarmWoodCost;
                case BuildingType.Tower: return config.TowerWoodCost;
                case BuildingType.Monastery: return config.MonasteryWoodCost;
                case BuildingType.Blacksmith: return config.BlacksmithWoodCost;
                case BuildingType.Market: return config.MarketWoodCost;
                case BuildingType.University: return config.UniversityWoodCost;
                case BuildingType.SiegeWorkshop: return config.SiegeWorkshopWoodCost;
                case BuildingType.Keep: return config.KeepWoodCost;
                case BuildingType.StoneWall: return 0;
                case BuildingType.StoneGate: return 0;
                case BuildingType.WoodGate: return config.WoodGateWoodCost;
                case BuildingType.Wonder: return config.WonderWoodCost;
                case BuildingType.Landmark: return 0; // landmarks use food/gold, not wood
                default: return 0;
            }
        }

        public int GetBuildingStoneCost(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.TownCenter: return config.TownCenterStoneCost;
                case BuildingType.Keep: return config.KeepStoneCost;
                case BuildingType.StoneWall: return config.StoneWallStoneCost;
                case BuildingType.StoneGate: return config.StoneGateStoneCost;
                case BuildingType.Wonder: return config.WonderStoneCost;
                default: return 0;
            }
        }

        public int GetBuildingFoodCost(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Wonder: return config.WonderFoodCost;
                default: return 0;
            }
        }

        public int GetBuildingGoldCost(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Wonder: return config.WonderGoldCost;
                default: return 0;
            }
        }

        public static bool IsDropOffBuilding(BuildingType type)
        {
            return type == BuildingType.TownCenter ||
                   type == BuildingType.Mill ||
                   type == BuildingType.LumberYard ||
                   type == BuildingType.Mine;
        }

        public static bool AcceptsResourceType(BuildingType buildingType, ResourceType resourceType)
        {
            switch (buildingType)
            {
                case BuildingType.TownCenter: return true;
                case BuildingType.Mill: return resourceType == ResourceType.Food;
                case BuildingType.LumberYard: return resourceType == ResourceType.Wood;
                case BuildingType.Mine: return resourceType == ResourceType.Gold || resourceType == ResourceType.Stone;
                default: return false;
            }
        }

        private void SpawnTrainedUnit(BuildingData building, int unitType, int playerId)
        {
            FixedVector3 spawnRef = building.HasRallyPoint ? building.RallyPoint : building.SimPosition;
            Vector2Int spawnTile = FindNearestWalkableAdjacentTile(building, spawnRef);
            FixedVector3 spawnPos = MapData.TileToWorldFixed(spawnTile.x, spawnTile.y);
            var unitData = CreateTrainedUnit(playerId, unitType, spawnPos);
            OnUnitTrained?.Invoke(unitData.Id, unitType, playerId);

            // Auto-move to rally point
            if (building.HasRallyPoint)
            {
                Vector2Int startTile = MapData.WorldToTile(spawnPos);

                // Rally to a unit (e.g. sheep → auto-slaughter)
                if (building.RallyPointUnitId >= 0)
                {
                    var targetUnit = UnitRegistry.GetUnit(building.RallyPointUnitId);
                    if (targetUnit != null && targetUnit.State != UnitState.Dead)
                    {
                        // Villager rallied to own sheep → slaughter
                        if (unitData.IsVillager && targetUnit.IsSheep && targetUnit.PlayerId == playerId)
                        {
                            unitData.ClearCommandQueue();
                            unitData.ClearSavedPath();
                            unitData.ClearFormation();
                            unitData.CombatTargetId = building.RallyPointUnitId;
                            unitData.CombatTargetBuildingId = -1;
                            unitData.TargetResourceNodeId = -1;
                            unitData.ConstructionTargetBuildingId = -1;
                            unitData.DropOffBuildingId = -1;
                            unitData.TargetGarrisonBuildingId = -1;
                            unitData.GatherTimer = Fixed32.Zero;
                            unitData.PlayerCommanded = true;

                            Vector2Int goalTile = MapData.WorldToTile(targetUnit.SimPosition);
                            var path = GridPathfinder.FindPath(MapData, startTile, goalTile, unitData.PlayerId, BuildingRegistry);
                            if (path.Count > 0)
                            {
                                unitData.SetPath(path);
                                unitData.FinalDestination = targetUnit.SimPosition;
                                unitData.State = UnitState.MovingToSlaughter;
                            }
                        }
                        else
                        {
                            // Generic rally to unit — just move to unit's current position
                            Vector2Int goalTile = MapData.WorldToTile(targetUnit.SimPosition);
                            var path = GridPathfinder.FindPath(MapData, startTile, goalTile, unitData.PlayerId, BuildingRegistry);
                            if (path.Count > 0)
                            {
                                unitData.SetPath(path);
                                unitData.FinalDestination = targetUnit.SimPosition;
                                unitData.State = UnitState.Moving;
                                unitData.PlayerCommanded = true;
                            }
                        }
                    }
                    else
                    {
                        // Target unit is dead/gone — just move to stored rally position
                        Vector2Int goalTile = MapData.WorldToTile(building.RallyPoint);
                        var path = GridPathfinder.FindPath(MapData, startTile, goalTile, unitData.PlayerId, BuildingRegistry);
                        if (path.Count > 0)
                        {
                            unitData.SetPath(path);
                            unitData.FinalDestination = GetSafeFinalDestination(building.RallyPoint, path);
                            unitData.State = UnitState.Moving;
                            unitData.PlayerCommanded = true;
                        }
                    }
                }
                // Rally point on a resource — send villager to gather
                else if (building.RallyPointOnResource && unitData.IsVillager)
                {
                    int nodeId = FindResourceNodeNearPosition(building.RallyPoint);
                    if (nodeId >= 0)
                    {
                        var node = MapData.GetResourceNode(nodeId);
                        bool assigned = false;
                        if (node.IsFarmNode)
                        {
                            Vector2Int farmTile = MapData.WorldToTile(node.Position);
                            var path = GridPathfinder.FindPath(MapData, startTile, farmTile, unitData.PlayerId, BuildingRegistry);
                            if (path.Count > 0)
                            {
                                AssignUnitToGather(unitData, nodeId, path, farmTile);
                                assigned = true;
                            }
                        }
                        else
                        {
                            Vector2Int nodeOrigin = new Vector2Int(node.TileX, node.TileZ);
                            var occupiedTiles = new HashSet<Vector2Int>();
                            var allUnits = UnitRegistry.GetAllUnits();
                            for (int u = 0; u < allUnits.Count; u++)
                            {
                                var other = allUnits[u];
                                if (other == unitData || other.State == UnitState.Dead) continue;
                                if (other.TargetResourceNodeId != nodeId) continue;
                                if (other.State == UnitState.MovingToGather || other.State == UnitState.Gathering)
                                    occupiedTiles.Add(MapData.WorldToTile(other.FinalDestination));
                            }
                            var triedTiles = new HashSet<Vector2Int>(occupiedTiles);
                            while (true)
                            {
                                Vector2Int adjTile = FindNearestWalkableAdjacentTileForResource(nodeOrigin, node.FootprintWidth, node.FootprintHeight, spawnPos, triedTiles);
                                if (adjTile == nodeOrigin) break;
                                var path = GridPathfinder.FindPath(MapData, startTile, adjTile, unitData.PlayerId, BuildingRegistry);
                                if (path.Count > 0)
                                {
                                    AssignUnitToGather(unitData, nodeId, path, adjTile);
                                    assigned = true;
                                    break;
                                }
                                triedTiles.Add(adjTile);
                            }
                        }
                        if (!assigned)
                        {
                            if (!TryRedirectGatherToNearbyNode(unitData, node.Type, nodeId, node.Position))
                            {
                                // Fallback: assign to primary tree ignoring occupancy (stacking is fine)
                                Vector2Int fbOrigin = new Vector2Int(node.TileX, node.TileZ);
                                var fallbackTried = new HashSet<Vector2Int>();
                                while (true)
                                {
                                    Vector2Int adjTile = FindNearestWalkableAdjacentTileForResource(
                                        fbOrigin, node.FootprintWidth, node.FootprintHeight,
                                        spawnPos, fallbackTried);
                                    if (adjTile == fbOrigin) break;
                                    var path = GridPathfinder.FindPath(MapData, startTile, adjTile, unitData.PlayerId, BuildingRegistry);
                                    if (path.Count > 0)
                                    {
                                        AssignUnitToGather(unitData, nodeId, path, adjTile);
                                        break;
                                    }
                                    fallbackTried.Add(adjTile);
                                }
                            }
                        }
                    }
                    else
                    {
                        // Target resource depleted — search for same type near rally point
                        if (!TryRedirectGatherToNearbyNode(unitData, building.RallyPointResourceType, -1, building.RallyPoint))
                        {
                            // Nothing nearby — just walk to rally point
                            Vector2Int goalTile = MapData.WorldToTile(building.RallyPoint);
                            var path = GridPathfinder.FindPath(MapData, startTile, goalTile, unitData.PlayerId, BuildingRegistry);
                            if (path.Count > 0)
                            {
                                unitData.SetPath(path);
                                unitData.FinalDestination = GetSafeFinalDestination(building.RallyPoint, path);
                                unitData.State = UnitState.Moving;
                                unitData.PlayerCommanded = true;
                            }
                        }
                    }
                }
                // Rally point on an under-construction building — send villager to build
                else if (building.RallyPointOnConstruction && unitData.IsVillager)
                {
                    var targetBuilding = BuildingRegistry.GetBuilding(building.RallyPointConstructionBuildingId);
                    if (targetBuilding != null && targetBuilding.IsUnderConstruction && !targetBuilding.IsDestroyed)
                    {
                        unitData.ClearFormation();
                        unitData.CombatTargetId = -1;
                        unitData.CombatTargetBuildingId = -1;
                        unitData.TargetResourceNodeId = -1;
                        unitData.ConstructionTargetBuildingId = targetBuilding.Id;
                        unitData.GatherTimer = Fixed32.Zero;
                        unitData.PlayerCommanded = true;
                        unitData.DropOffBuildingId = -1;
                        unitData.TargetGarrisonBuildingId = -1;
                        unitData.ClearPatrol();

                        var occupiedTiles = new HashSet<Vector2Int>();
                        var allUnits = UnitRegistry.GetAllUnits();
                        for (int u = 0; u < allUnits.Count; u++)
                        {
                            var other = allUnits[u];
                            if (other == unitData || other.State == UnitState.Dead) continue;
                            if (other.ConstructionTargetBuildingId != targetBuilding.Id) continue;
                            if (other.State == UnitState.MovingToBuild || other.State == UnitState.Constructing)
                                occupiedTiles.Add(MapData.WorldToTile(other.FinalDestination));
                        }

                        Vector2Int adjTile = FindNearestWalkableAdjacentTile(targetBuilding, spawnPos, occupiedTiles);
                        var path = GridPathfinder.FindPath(MapData, startTile, adjTile, unitData.PlayerId, BuildingRegistry);
                        if (path.Count > 0)
                        {
                            unitData.SetPath(path);
                            unitData.FinalDestination = MapData.TileToWorldFixed(adjTile.x, adjTile.y);
                            unitData.State = UnitState.MovingToBuild;
                        }
                        else
                        {
                            unitData.State = UnitState.Constructing;
                        }
                    }
                    else
                    {
                        // Building already finished or destroyed — just move to rally point
                        Vector2Int goalTile = MapData.WorldToTile(building.RallyPoint);
                        var path = GridPathfinder.FindPath(MapData, startTile, goalTile, unitData.PlayerId, BuildingRegistry);
                        if (path.Count > 0)
                        {
                            unitData.SetPath(path);
                            unitData.FinalDestination = GetSafeFinalDestination(building.RallyPoint, path);
                            unitData.State = UnitState.Moving;
                            unitData.PlayerCommanded = true;
                        }
                    }
                }
                else
                {
                    // Check for unfinished building at rally point
                    int rallyBuildingId = FindUnfinishedBuildingNearPosition(building.RallyPoint, playerId);
                    if (rallyBuildingId >= 0 && unitData.IsVillager)
                    {
                        var targetBuilding = BuildingRegistry.GetBuilding(rallyBuildingId);
                        unitData.ClearFormation();
                        unitData.CombatTargetId = -1;
                        unitData.CombatTargetBuildingId = -1;
                        unitData.TargetResourceNodeId = -1;
                        unitData.ConstructionTargetBuildingId = rallyBuildingId;
                        unitData.GatherTimer = Fixed32.Zero;
                        unitData.PlayerCommanded = true;
                        unitData.DropOffBuildingId = -1;
                        unitData.TargetGarrisonBuildingId = -1;

                        unitData.ClearPatrol();

                        Vector2Int adjTile = FindNearestWalkableAdjacentTile(targetBuilding, spawnPos);
                        var path = GridPathfinder.FindPath(MapData, startTile, adjTile, unitData.PlayerId, BuildingRegistry);
                        if (path.Count > 0)
                        {
                            unitData.SetPath(path);
                            unitData.FinalDestination = MapData.TileToWorldFixed(adjTile.x, adjTile.y);
                            unitData.State = UnitState.MovingToBuild;
                        }
                        else
                        {
                            unitData.State = UnitState.Constructing;
                        }
                    }
                    else
                    {
                        // Default: just move to rally point
                        Vector2Int goalTile = MapData.WorldToTile(building.RallyPoint);
                        var path = GridPathfinder.FindPath(MapData, startTile, goalTile, unitData.PlayerId, BuildingRegistry);
                        if (path.Count > 0)
                        {
                            unitData.SetPath(path);
                            unitData.FinalDestination = GetSafeFinalDestination(building.RallyPoint, path);
                            unitData.State = UnitState.Moving;
                            unitData.PlayerCommanded = true;
                        }
                    }
                }
            }
        }

        private FixedVector3 GetSafeFinalDestination(FixedVector3 desired, List<Vector2Int> path)
        {
            Vector2Int tile = MapData.WorldToTile(desired);
            if (MapData.IsWalkable(tile.x, tile.y))
                return desired;
            var lastTile = path[path.Count - 1];
            return MapData.TileToWorldFixed(lastTile.x, lastTile.y);
        }

        private UnitData CreateTrainedUnit(int playerId, int unitType, FixedVector3 spawnPos)
        {
            UnitData unitData;

            switch (unitType)
            {
                case 9: // Monk
                    unitData = UnitRegistry.CreateUnit(playerId, spawnPos,
                        ConfigToFixed32(config.MonkMoveSpeed),
                        cachedUnitRadius,
                        ConfigToFixed32(config.MonkMass));
                    unitData.UnitType = 9;
                    unitData.MaxHealth = config.MonkMaxHealth;
                    unitData.AttackDamage = config.MonkAttackDamage;
                    unitData.AttackRange = ConfigToFixed32(config.MonkAttackRange);
                    unitData.AttackCooldownTicks = config.MonkHealCooldownTicks;
                    unitData.MeleeArmor = config.MonkMeleeArmor;
                    unitData.RangedArmor = config.MonkRangedArmor;
                    unitData.DetectionRange = ConfigToFixed32(config.MonkDetectionRange);
                    unitData.IsHealer = true;
                    break;
                case 8: // Crossbowman
                    unitData = UnitRegistry.CreateUnit(playerId, spawnPos,
                        ConfigToFixed32(config.CrossbowmanMoveSpeed),
                        cachedUnitRadius,
                        ConfigToFixed32(config.CrossbowmanMass));
                    unitData.UnitType = 8;
                    unitData.MaxHealth = config.CrossbowmanMaxHealth;
                    unitData.AttackDamage = config.CrossbowmanAttackDamage;
                    unitData.AttackRange = ConfigToFixed32(config.CrossbowmanAttackRange);
                    unitData.AttackCooldownTicks = config.CrossbowmanAttackCooldownTicks;
                    unitData.MeleeArmor = config.CrossbowmanMeleeArmor;
                    unitData.RangedArmor = config.CrossbowmanRangedArmor;
                    unitData.DetectionRange = ConfigToFixed32(config.CrossbowmanDetectionRange);
                    unitData.IsRanged = true;
                    unitData.BonusDamageVsType = config.CrossbowmanBonusDamageVsType;
                    unitData.BonusDamageAmount = config.CrossbowmanBonusDamageAmount;
                    unitData.BonusDamageVsType2 = config.CrossbowmanBonusDamageVsType2;
                    unitData.BonusDamageAmount2 = config.CrossbowmanBonusDamageAmount2;
                    break;
                case 7: // Knight
                    unitData = UnitRegistry.CreateUnit(playerId, spawnPos,
                        ConfigToFixed32(config.KnightMoveSpeed),
                        ConfigToFixed32(config.CavalryRadius),
                        ConfigToFixed32(config.KnightMass));
                    unitData.UnitType = 7;
                    unitData.MaxHealth = config.KnightMaxHealth;
                    unitData.AttackDamage = config.KnightAttackDamage;
                    unitData.AttackRange = ConfigToFixed32(config.KnightAttackRange);
                    unitData.AttackCooldownTicks = config.KnightAttackCooldownTicks;
                    unitData.MeleeArmor = config.KnightMeleeArmor;
                    unitData.RangedArmor = config.KnightRangedArmor;
                    unitData.DetectionRange = ConfigToFixed32(config.KnightDetectionRange);
                    unitData.BonusDamageVsType = config.KnightBonusDamageVsType;
                    unitData.BonusDamageAmount = config.KnightBonusDamageAmount;
                    break;
                case 6: // Man-at-Arms
                    unitData = UnitRegistry.CreateUnit(playerId, spawnPos,
                        ConfigToFixed32(config.ManAtArmsMoveSpeed),
                        cachedUnitRadius,
                        ConfigToFixed32(config.ManAtArmsMass));
                    unitData.UnitType = 6;
                    unitData.MaxHealth = config.ManAtArmsMaxHealth;
                    unitData.AttackDamage = config.ManAtArmsAttackDamage;
                    unitData.AttackRange = ConfigToFixed32(config.ManAtArmsAttackRange);
                    unitData.AttackCooldownTicks = config.ManAtArmsAttackCooldownTicks;
                    unitData.MeleeArmor = config.ManAtArmsMeleeArmor;
                    unitData.RangedArmor = config.ManAtArmsRangedArmor;
                    unitData.DetectionRange = ConfigToFixed32(config.ManAtArmsDetectionRange);
                    break;
                case 12: // Landsknecht (HRE unique Spearman)
                    unitData = UnitRegistry.CreateUnit(playerId, spawnPos,
                        ConfigToFixed32(config.LandsknechtMoveSpeed),
                        cachedUnitRadius,
                        ConfigToFixed32(config.LandsknechtMass));
                    unitData.UnitType = 12;
                    unitData.MaxHealth = config.LandsknechtMaxHealth;
                    unitData.AttackDamage = config.LandsknechtAttackDamage;
                    unitData.AttackRange = ConfigToFixed32(config.LandsknechtAttackRange);
                    unitData.AttackCooldownTicks = config.LandsknechtAttackCooldownTicks;
                    unitData.MeleeArmor = config.LandsknechtMeleeArmor;
                    unitData.RangedArmor = config.LandsknechtRangedArmor;
                    unitData.DetectionRange = ConfigToFixed32(config.LandsknechtDetectionRange);
                    unitData.BonusDamageVsType = config.LandsknechtBonusDamageVsType;
                    unitData.BonusDamageAmount = config.LandsknechtBonusDamageAmount;
                    break;
                case 11: // Gendarme (French unique Horseman)
                    unitData = UnitRegistry.CreateUnit(playerId, spawnPos,
                        ConfigToFixed32(config.GendarmeMoveSpeed),
                        ConfigToFixed32(config.CavalryRadius),
                        ConfigToFixed32(config.GendarmeMass));
                    unitData.UnitType = 11;
                    unitData.MaxHealth = config.GendarmeMaxHealth;
                    unitData.AttackDamage = config.GendarmeAttackDamage;
                    unitData.AttackRange = ConfigToFixed32(config.GendarmeAttackRange);
                    unitData.AttackCooldownTicks = config.GendarmeAttackCooldownTicks;
                    unitData.MeleeArmor = config.GendarmeMeleeArmor;
                    unitData.RangedArmor = config.GendarmeRangedArmor;
                    unitData.DetectionRange = ConfigToFixed32(config.GendarmeDetectionRange);
                    unitData.BonusDamageVsType = config.GendarmeBonusDamageVsType;
                    unitData.BonusDamageAmount = config.GendarmeBonusDamageAmount;
                    break;
                case 10: // Longbowman (English unique Archer)
                    unitData = UnitRegistry.CreateUnit(playerId, spawnPos,
                        ConfigToFixed32(config.LongbowmanMoveSpeed),
                        cachedUnitRadius,
                        ConfigToFixed32(config.LongbowmanMass));
                    unitData.UnitType = 10;
                    unitData.MaxHealth = config.LongbowmanMaxHealth;
                    unitData.AttackDamage = config.LongbowmanAttackDamage;
                    unitData.AttackRange = ConfigToFixed32(config.LongbowmanAttackRange);
                    unitData.AttackCooldownTicks = config.LongbowmanAttackCooldownTicks;
                    unitData.MeleeArmor = config.LongbowmanMeleeArmor;
                    unitData.RangedArmor = config.LongbowmanRangedArmor;
                    unitData.DetectionRange = ConfigToFixed32(config.LongbowmanDetectionRange);
                    unitData.IsRanged = true;
                    unitData.BonusDamageVsType = config.LongbowmanBonusDamageVsType;
                    unitData.BonusDamageAmount = config.LongbowmanBonusDamageAmount;
                    break;
                case 4: // Scout
                    unitData = UnitRegistry.CreateUnit(playerId, spawnPos,
                        ConfigToFixed32(config.ScoutMoveSpeed),
                        ConfigToFixed32(config.CavalryRadius),
                        ConfigToFixed32(config.ScoutMass));
                    unitData.UnitType = 4;
                    unitData.MaxHealth = config.ScoutMaxHealth;
                    unitData.AttackDamage = config.ScoutAttackDamage;
                    unitData.AttackRange = ConfigToFixed32(config.ScoutAttackRange);
                    unitData.AttackCooldownTicks = config.ScoutAttackCooldownTicks;
                    unitData.MeleeArmor = config.ScoutMeleeArmor;
                    unitData.RangedArmor = config.ScoutRangedArmor;
                    unitData.DetectionRange = ConfigToFixed32(config.ScoutDetectionRange);
                    break;
                case 3: // Horseman
                    unitData = UnitRegistry.CreateUnit(playerId, spawnPos,
                        ConfigToFixed32(config.HorsemanMoveSpeed),
                        ConfigToFixed32(config.CavalryRadius),
                        ConfigToFixed32(config.HorsemanMass));
                    unitData.UnitType = 3;
                    unitData.MaxHealth = config.HorsemanMaxHealth;
                    unitData.AttackDamage = config.HorsemanAttackDamage;
                    unitData.AttackRange = ConfigToFixed32(config.HorsemanAttackRange);
                    unitData.AttackCooldownTicks = config.HorsemanAttackCooldownTicks;
                    unitData.MeleeArmor = config.HorsemanMeleeArmor;
                    unitData.RangedArmor = config.HorsemanRangedArmor;
                    unitData.DetectionRange = ConfigToFixed32(config.HorsemanDetectionRange);
                    unitData.BonusDamageVsType = config.HorsemanBonusDamageVsType;
                    unitData.BonusDamageAmount = config.HorsemanBonusDamageAmount;
                    break;
                case 2: // Archer
                    unitData = UnitRegistry.CreateUnit(playerId, spawnPos,
                        ConfigToFixed32(config.ArcherMoveSpeed),
                        cachedUnitRadius,
                        ConfigToFixed32(config.ArcherMass));
                    unitData.UnitType = 2;
                    unitData.MaxHealth = config.ArcherMaxHealth;
                    unitData.AttackDamage = config.ArcherAttackDamage;
                    unitData.AttackRange = ConfigToFixed32(config.ArcherAttackRange);
                    unitData.AttackCooldownTicks = config.ArcherAttackCooldownTicks;
                    unitData.MeleeArmor = config.ArcherMeleeArmor;
                    unitData.RangedArmor = config.ArcherRangedArmor;
                    unitData.DetectionRange = ConfigToFixed32(config.ArcherDetectionRange);
                    unitData.IsRanged = true;
                    unitData.BonusDamageVsType = config.ArcherBonusDamageVsType;
                    unitData.BonusDamageAmount = config.ArcherBonusDamageAmount;
                    break;
                case 0: // Villager
                    unitData = UnitRegistry.CreateUnit(playerId, spawnPos,
                        ConfigToFixed32(config.UnitMoveSpeed),
                        cachedUnitRadius,
                        ConfigToFixed32(config.VillagerMass));
                    unitData.UnitType = 0;
                    unitData.MaxHealth = config.VillagerMaxHealth;
                    unitData.AttackDamage = config.VillagerAttackDamage;
                    unitData.AttackRange = ConfigToFixed32(config.VillagerAttackRange);
                    unitData.AttackCooldownTicks = config.VillagerAttackCooldownTicks;
                    unitData.MeleeArmor = config.VillagerMeleeArmor;
                    unitData.RangedArmor = config.VillagerRangedArmor;
                    unitData.DetectionRange = ConfigToFixed32(config.VillagerDetectionRange);
                    unitData.CarryCapacity = config.VillagerCarryCapacity;
                    unitData.IsVillager = true;
                    break;
                default: // 1 = Spearman
                    unitData = UnitRegistry.CreateUnit(playerId, spawnPos,
                        Fixed32.FromInt(2),
                        cachedUnitRadius,
                        ConfigToFixed32(config.SpearmanMass));
                    unitData.UnitType = 1;
                    unitData.MaxHealth = config.SpearmanMaxHealth;
                    unitData.AttackDamage = config.SpearmanAttackDamage;
                    unitData.AttackRange = ConfigToFixed32(config.SpearmanAttackRange);
                    unitData.AttackCooldownTicks = config.SpearmanAttackCooldownTicks;
                    unitData.MeleeArmor = config.SpearmanMeleeArmor;
                    unitData.RangedArmor = config.SpearmanRangedArmor;
                    unitData.DetectionRange = ConfigToFixed32(config.SpearmanDetectionRange);
                    unitData.BonusDamageVsType = config.SpearmanBonusDamageVsType;
                    unitData.BonusDamageAmount = config.SpearmanBonusDamageAmount;
                    break;
            }
            unitData.CurrentHealth = unitData.MaxHealth;
            return unitData;
        }

        private Vector2Int FindNearestWalkableAdjacentTile(BuildingData building, FixedVector3 unitPos)
        {
            Vector2Int unitTile = MapData.WorldToTile(unitPos);
            Vector2Int best = unitTile;
            int bestDistSq = int.MaxValue;

            int minX = building.OriginTileX - 1;
            int maxX = building.OriginTileX + building.TileFootprintWidth;
            int minZ = building.OriginTileZ - 1;
            int maxZ = building.OriginTileZ + building.TileFootprintHeight;

            // Scan perimeter tiles around the building footprint
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    // Skip interior tiles (only perimeter)
                    if (x >= building.OriginTileX && x < building.OriginTileX + building.TileFootprintWidth &&
                        z >= building.OriginTileZ && z < building.OriginTileZ + building.TileFootprintHeight)
                        continue;

                    if (!MapData.IsWalkable(x, z)) continue;

                    int dx = x - unitTile.x;
                    int dz = z - unitTile.y;
                    int distSq = dx * dx + dz * dz;
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        best = new Vector2Int(x, z);
                    }
                }
            }

            return best;
        }

        private Vector2Int FindNearestWalkableAdjacentTile(BuildingData building, FixedVector3 unitPos, HashSet<Vector2Int> occupiedTiles)
        {
            Vector2Int unitTile = MapData.WorldToTile(unitPos);
            Vector2Int best = unitTile;
            int bestDistSq = int.MaxValue;

            int minX = building.OriginTileX - 1;
            int maxX = building.OriginTileX + building.TileFootprintWidth;
            int minZ = building.OriginTileZ - 1;
            int maxZ = building.OriginTileZ + building.TileFootprintHeight;

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (x >= building.OriginTileX && x < building.OriginTileX + building.TileFootprintWidth &&
                        z >= building.OriginTileZ && z < building.OriginTileZ + building.TileFootprintHeight)
                        continue;

                    if (!MapData.IsWalkable(x, z)) continue;

                    var tile = new Vector2Int(x, z);
                    if (occupiedTiles.Contains(tile)) continue;

                    int dx = x - unitTile.x;
                    int dz = z - unitTile.y;
                    int distSq = dx * dx + dz * dz;
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        best = tile;
                    }
                }
            }

            return best;
        }

        private Vector2Int FindNearestWalkableAdjacentTileForResource(Vector2Int nodeOrigin, int footprintW, int footprintH, FixedVector3 unitPos, HashSet<Vector2Int> occupiedTiles)
        {
            Vector2Int unitTile = MapData.WorldToTile(unitPos);
            Vector2Int best = nodeOrigin; // inside footprint = always unwalkable = valid failure sentinel
            int bestDistSq = int.MaxValue;

            // Scan the ring of tiles surrounding the full NxN footprint
            for (int x = nodeOrigin.x - 1; x <= nodeOrigin.x + footprintW; x++)
            {
                for (int z = nodeOrigin.y - 1; z <= nodeOrigin.y + footprintH; z++)
                {
                    // Skip tiles inside the footprint
                    if (x >= nodeOrigin.x && x < nodeOrigin.x + footprintW &&
                        z >= nodeOrigin.y && z < nodeOrigin.y + footprintH)
                        continue;

                    if (!MapData.IsWalkable(x, z)) continue;

                    var tile = new Vector2Int(x, z);
                    if (occupiedTiles != null && occupiedTiles.Contains(tile)) continue;

                    int dx = x - unitTile.x;
                    int dz = z - unitTile.y;
                    int distSq = dx * dx + dz * dz;
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        best = tile;
                    }
                }
            }

            return best;
        }

        private bool TryRedirectGatherToNearbyNode(UnitData unit, ResourceType type, int excludeNodeId, FixedVector3 searchCenter)
        {
            Fixed32 searchRange = Fixed32.FromInt(15);
            Fixed32 searchRangeSq = searchRange * searchRange;
            Fixed32 bestDistSq = searchRangeSq;
            int bestNodeId = -1;
            Vector2Int bestAdjTile = default;
            List<Vector2Int> bestPath = null;

            Vector2Int startTile = MapData.WorldToTile(unit.SimPosition);

            foreach (var candidate in MapData.GetAllResourceNodes())
            {
                if (candidate.Id == excludeNodeId || candidate.IsDepleted || candidate.Type != type)
                    continue;

                FixedVector3 diff = candidate.Position - searchCenter;
                // Overflow guard: skip if axis distance exceeds search range
                if (Fixed32.Abs(diff.x) > searchRange || Fixed32.Abs(diff.z) > searchRange) continue;

                Fixed32 distSq = diff.x * diff.x + diff.z * diff.z;
                if (distSq > bestDistSq || (distSq == bestDistSq && candidate.Id >= bestNodeId)) continue;

                // Check if this node has open adjacent tiles
                Vector2Int candOrigin = new Vector2Int(candidate.TileX, candidate.TileZ);

                // Skip occupancy check for redirect — occasional tile overlap is harmless
                var triedTiles = new HashSet<Vector2Int>();
                bool found = false;
                Vector2Int adj = default;
                List<Vector2Int> path = null;
                int redirectAttempts = 0;
                while (true)
                {
                    if (++redirectAttempts > 4) break; // Cap retry attempts
                    adj = FindNearestWalkableAdjacentTileForResource(candOrigin, candidate.FootprintWidth, candidate.FootprintHeight, unit.SimPosition, triedTiles);
                    if (adj == candOrigin) break; // All tiles exhausted
                    path = GridPathfinder.FindPath(MapData, startTile, adj, unit.PlayerId, BuildingRegistry);
                    if (path.Count > 0) { found = true; break; }
                    triedTiles.Add(adj);
                }
                if (!found) continue;

                bestNodeId = candidate.Id;
                bestAdjTile = adj;
                bestPath = path;
                bestDistSq = distSq;
            }

            if (bestNodeId < 0 || bestPath == null) return false;

            unit.ClearCommandQueue();
            AssignUnitToGather(unit, bestNodeId, bestPath, bestAdjTile);
            return true;
        }

        private int FindResourceNodeNearPosition(FixedVector3 position)
        {
            Fixed32 searchRadius = new Fixed32(98304); // 1.5 * 65536
            Fixed32 searchRadiusSq = searchRadius * searchRadius;
            int bestId = -1;
            Fixed32 bestDistSq = searchRadiusSq;

            foreach (var node in MapData.GetAllResourceNodes())
            {
                if (node.IsDepleted) continue;
                FixedVector3 diff = node.Position - position;
                // Overflow guard: skip if axis distance exceeds search radius
                if (Fixed32.Abs(diff.x) > searchRadius || Fixed32.Abs(diff.z) > searchRadius) continue;

                Fixed32 distSq = diff.x * diff.x + diff.z * diff.z;
                if (distSq < bestDistSq || (distSq == bestDistSq && node.Id < bestId))
                {
                    bestDistSq = distSq;
                    bestId = node.Id;
                }
            }
            return bestId;
        }

        private int FindUnfinishedBuildingNearPosition(FixedVector3 position, int playerId)
        {
            Vector2Int tile = MapData.WorldToTile(position);
            var buildings = BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (!b.IsUnderConstruction || b.IsDestroyed) continue;
                if (b.PlayerId != playerId) continue;

                if (tile.x >= b.OriginTileX - 1 && tile.x <= b.OriginTileX + b.TileFootprintWidth &&
                    tile.y >= b.OriginTileZ - 1 && tile.y <= b.OriginTileZ + b.TileFootprintHeight)
                {
                    return b.Id;
                }
            }
            return -1;
        }

        private void EjectUnitsFromBuildingFootprint(BuildingData building)
        {
            foreach (var unit in UnitRegistry.GetAllUnits())
            {
                if (unit.State == UnitState.Dead) continue;

                Vector2Int unitTile = MapData.WorldToTile(unit.SimPosition);
                if (unitTile.x >= building.OriginTileX && unitTile.x < building.OriginTileX + building.TileFootprintWidth &&
                    unitTile.y >= building.OriginTileZ && unitTile.y < building.OriginTileZ + building.TileFootprintHeight)
                {
                    Vector2Int adjTile = FindNearestWalkableAdjacentTile(building, unit.SimPosition);
                    unit.SimPosition = MapData.TileToWorldFixed(adjTile.x, adjTile.y);
                    unit.PreviousSimPosition = unit.SimPosition;

                    if (unit.State == UnitState.Moving || unit.State == UnitState.MovingToGather || unit.State == UnitState.MovingToBuild || unit.State == UnitState.MovingToDropoff || unit.State == UnitState.MovingToGarrison)
                    {
                        unit.Path?.Clear();
                        unit.State = UnitState.Idle;
                        if (unit.TargetGarrisonBuildingId >= 0)
                        {
                            unit.TargetGarrisonBuildingId = -1;
                            unit.ClearPatrol();
                        }
                    }
                }
            }
        }

        // ---- Patrol support ----

        private void ProcessPatrolCommand(PatrolCommand cmd)
        {
            foreach (int unitId in cmd.UnitIds)
            {
                var unit = UnitRegistry.GetUnit(unitId);
                if (unit == null || unit.State == UnitState.Dead) continue;
                if (unit.PlayerId != cmd.PlayerId) continue;

                if (!cmd.IsQueued)
                {
                    // Fresh patrol: set up A→B route
                    unit.ClearCommandQueue();
                    unit.ClearSavedPath();
                    unit.ClearFormation();
                    unit.ClearPatrol();

                    unit.PatrolWaypoints.Add(unit.SimPosition);
                    unit.PatrolWaypoints.Add(cmd.TargetPosition);
                    unit.PatrolCurrentIndex = 0;
                    unit.PatrolForward = true;
                    unit.IsPatrolling = true;

                    unit.TargetResourceNodeId = -1;
                    unit.ConstructionTargetBuildingId = -1;
                    unit.CombatTargetId = -1;
                    unit.CombatTargetBuildingId = -1;
                    unit.DropOffBuildingId = -1;
                    unit.TargetGarrisonBuildingId = -1;
                    unit.GatherTimer = Fixed32.Zero;
                    unit.PlayerCommanded = false; // false so combat system auto-aggros (attack-move behaviour)

                    // Path to first patrol target
                    Vector2Int startTile = MapData.WorldToTile(unit.SimPosition);
                    Vector2Int goalTile = MapData.WorldToTile(cmd.TargetPosition);
                    var path = GridPathfinder.FindPath(MapData, startTile, goalTile, unit.PlayerId, BuildingRegistry);
                    if (path.Count > 0)
                    {
                        unit.SetPath(path);
                        unit.FinalDestination = new FixedVector3(cmd.TargetPosition.x, Fixed32.Zero, cmd.TargetPosition.z);
                        unit.State = UnitState.Moving;
                    }
                }
                else if (unit.IsPatrolling)
                {
                    // Shift+patrol while already patrolling: extend route
                    unit.PatrolWaypoints.Add(cmd.TargetPosition);
                }
                else
                {
                    // Shift+patrol while busy: queue for later
                    unit.CommandQueue.Add(QueuedCommand.PatrolWaypoint(cmd.TargetPosition));
                }
            }
        }

        private void ProcessPatrolArrivals()
        {
            var units = UnitRegistry.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (!unit.IsPatrolling) continue;
                if (unit.State != UnitState.Idle) continue;

                // Advance patrol index in ping-pong fashion
                if (unit.PatrolForward)
                {
                    unit.PatrolCurrentIndex++;
                    if (unit.PatrolCurrentIndex >= unit.PatrolWaypoints.Count)
                    {
                        unit.PatrolForward = false;
                        unit.PatrolCurrentIndex = unit.PatrolWaypoints.Count - 2;
                    }
                }
                else
                {
                    unit.PatrolCurrentIndex--;
                    if (unit.PatrolCurrentIndex < 0)
                    {
                        unit.PatrolForward = true;
                        unit.PatrolCurrentIndex = 1;
                    }
                }

                // Clamp to valid range
                if (unit.PatrolCurrentIndex < 0 || unit.PatrolCurrentIndex >= unit.PatrolWaypoints.Count)
                {
                    unit.ClearPatrol();
                    continue;
                }

                var nextWaypoint = unit.PatrolWaypoints[unit.PatrolCurrentIndex];
                Vector2Int startTile = MapData.WorldToTile(unit.SimPosition);
                Vector2Int goalTile = MapData.WorldToTile(nextWaypoint);
                var path = GridPathfinder.FindPath(MapData, startTile, goalTile, unit.PlayerId, BuildingRegistry);
                if (path.Count > 0)
                {
                    unit.SetPath(path);
                    unit.FinalDestination = new FixedVector3(nextWaypoint.x, Fixed32.Zero, nextWaypoint.z);
                    unit.State = UnitState.Moving;
                }
                else
                {
                    unit.ClearPatrol();
                }
            }
        }

        // ---- Garrison support ----

        private void ProcessArrivedGarrisonUnits()
        {
            // Collect units to process first to avoid collection modification during iteration
            garrisonUnitsBuffer.Clear();
            foreach (var unit in UnitRegistry.GetAllUnits())
            {
                if (unit.TargetGarrisonBuildingId < 0) continue;
                if (unit.State == UnitState.Idle || unit.State == UnitState.MovingToGarrison)
                {
                    garrisonUnitsBuffer.Add(unit);
                }
            }

            // Process the collected units
            foreach (var unit in garrisonUnitsBuffer)
            {
                // Handle units that just arrived (Idle) or are still moving to garrison
                if (unit.State == UnitState.Idle)
                {
                    // Unit just arrived, try to garrison
                    ProcessUnitArrivalAtGarrison(unit);
                }
                else if (unit.State == UnitState.MovingToGarrison)
                {
                    // Unit is still moving, check if target is still valid
                    var targetBuilding = BuildingRegistry.GetBuilding(unit.TargetGarrisonBuildingId);
                    if (targetBuilding == null || targetBuilding.IsDestroyed || 
                        targetBuilding.PlayerId != unit.PlayerId || !targetBuilding.CanGarrison)
                    {
                        // Target became invalid, find alternative
                        RedirectToAlternativeGarrison(unit);
                    }
                }
            }
        }

        private void ProcessUnitArrivalAtGarrison(UnitData unit)
        {
            var targetBuilding = BuildingRegistry.GetBuilding(unit.TargetGarrisonBuildingId);
            
            // Check if the target building is still valid for garrison
            if (targetBuilding == null || targetBuilding.IsDestroyed || 
                targetBuilding.PlayerId != unit.PlayerId || !targetBuilding.CanGarrison)
            {
                // Target building is no longer valid, try to find another one
                RedirectToAlternativeGarrison(unit);
                return;
            }

            // Check if unit is adjacent to the building
            Vector2Int unitTile = MapData.WorldToTile(unit.SimPosition);
            bool adjacent = unitTile.x >= targetBuilding.OriginTileX - 2 &&
                unitTile.x <= targetBuilding.OriginTileX + targetBuilding.TileFootprintWidth + 1 &&
                unitTile.y >= targetBuilding.OriginTileZ - 2 &&
                unitTile.y <= targetBuilding.OriginTileZ + targetBuilding.TileFootprintHeight + 1;

            if (adjacent)
            {
                // Unit is adjacent, try to garrison
                if (!GarrisonUnit(unit, targetBuilding))
                {
                    // Building is now full, find alternative
                    RedirectToAlternativeGarrison(unit);
                }
            }
            else
            {
                // Unit arrived but not adjacent, path to building
                Vector2Int adjTile = FindNearestWalkableAdjacentTile(targetBuilding, unit.SimPosition);
                Vector2Int startTile = MapData.WorldToTile(unit.SimPosition);
                var path = GridPathfinder.FindPath(MapData, startTile, adjTile, unit.PlayerId, BuildingRegistry);
                if (path.Count > 0)
                {
                    unit.SetPath(path);
                    unit.FinalDestination = MapData.TileToWorldFixed(adjTile.x, adjTile.y);
                    unit.State = UnitState.MovingToGarrison;
                    unit.PlayerCommanded = true;
                }
                else
                {
                    // Can't path to building, find alternative
                    RedirectToAlternativeGarrison(unit);
                }
            }
        }

        private void RedirectToAlternativeGarrison(UnitData unit)
        {
            var alternativeBuilding = FindNearestAvailableGarrisonBuilding(unit.PlayerId, unit.SimPosition, unit.TargetGarrisonBuildingId);
            
            if (alternativeBuilding == null)
            {
                // No valid buildings available, cancel garrison
                unit.TargetGarrisonBuildingId = -1;
                unit.ClearPatrol();
                unit.State = UnitState.Idle;
                return;
            }
            
            // Found a new building, path to it
            Vector2Int adjTile = FindNearestWalkableAdjacentTile(alternativeBuilding, unit.SimPosition);
            Vector2Int startTile = MapData.WorldToTile(unit.SimPosition);
            var path = GridPathfinder.FindPath(MapData, startTile, adjTile, unit.PlayerId, BuildingRegistry);
            if (path.Count > 0)
            {
                unit.SetPath(path);
                unit.FinalDestination = MapData.TileToWorldFixed(adjTile.x, adjTile.y);
                unit.State = UnitState.MovingToGarrison;
                unit.TargetGarrisonBuildingId = alternativeBuilding.Id;
                unit.PlayerCommanded = true;
            }
            else
            {
                // Can't path to any building, cancel garrison
                unit.TargetGarrisonBuildingId = -1;
                unit.ClearPatrol();
                unit.State = UnitState.Idle;
            }
        }

        private BuildingData FindNearestAvailableGarrisonBuilding(int playerId, FixedVector3 unitPosition, int excludeBuildingId = -1)
        {
            BuildingData nearestBuilding = null;
            Fixed32 nearestDistance = Fixed32.FromInt(999);

            foreach (var building in BuildingRegistry.GetAllBuildings())
            {
                if (building.Id == excludeBuildingId) continue;
                if (building.PlayerId != playerId) continue;
                if (building.IsDestroyed || building.IsUnderConstruction) continue;
                if (building.GarrisonCapacity <= 0 || !building.CanGarrison) continue;

                Fixed32 distance = (unitPosition - building.SimPosition).Magnitude();
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestBuilding = building;
                }
            }

            return nearestBuilding;
        }

        // Returns the ID of a friendly completed building whose footprint or adjacent border contains the given tile, or -1.
        private int FindBuildingNearPosition(FixedVector3 position, int playerId)
        {
            Vector2Int tile = MapData.WorldToTile(position);
            var buildings = BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (b.IsDestroyed || b.IsUnderConstruction) continue;
                if (b.PlayerId != playerId) continue;
                // Check footprint + 1-tile border (since sheep path ends adjacent to building)
                if (tile.x >= b.OriginTileX - 1 && tile.x < b.OriginTileX + b.TileFootprintWidth + 1
                    && tile.y >= b.OriginTileZ - 1 && tile.y < b.OriginTileZ + b.TileFootprintHeight + 1)
                    return b.Id;
            }
            return -1;
        }

        private void ProcessGarrisonCommand(GarrisonCommand cmd)
        {
            // First, collect all valid units for garrison
            var validUnits = new List<UnitData>();
            foreach (int unitId in cmd.UnitIds)
            {
                var unit = UnitRegistry.GetUnit(unitId);
                if (unit == null || unit.State == UnitState.Dead) continue;
                if (unit.PlayerId != cmd.PlayerId) continue;
                validUnits.Add(unit);
            }

            if (validUnits.Count == 0) return;

            // Get all available garrison buildings for this player, sorted by distance from the first unit
            var availableBuildings = GetAvailableGarrisonBuildings(cmd.PlayerId, validUnits[0].SimPosition);
            if (availableBuildings.Count == 0) return;

            // Try to prioritize the specified building if it's available
            var specifiedBuilding = BuildingRegistry.GetBuilding(cmd.TargetBuildingId);
            if (specifiedBuilding != null && specifiedBuilding.PlayerId == cmd.PlayerId && 
                !specifiedBuilding.IsDestroyed && !specifiedBuilding.IsUnderConstruction && 
                specifiedBuilding.GarrisonCapacity > 0 && specifiedBuilding.CanGarrison)
            {
                // Move specified building to front if it exists in available list
                for (int i = 0; i < availableBuildings.Count; i++)
                {
                    if (availableBuildings[i].Id == specifiedBuilding.Id)
                    {
                        var temp = availableBuildings[0];
                        availableBuildings[0] = availableBuildings[i];
                        availableBuildings[i] = temp;
                        break;
                    }
                }
            }

            // Assign units to buildings, distributing across multiple buildings if needed
            int currentBuildingIndex = 0;
            foreach (var unit in validUnits)
            {
                // Find a building with available space
                BuildingData targetBuilding = null;
                int originalBuildingIndex = currentBuildingIndex;

                do
                {
                    if (currentBuildingIndex < availableBuildings.Count && availableBuildings[currentBuildingIndex].CanGarrison)
                    {
                        targetBuilding = availableBuildings[currentBuildingIndex];
                        break;
                    }
                    currentBuildingIndex = (currentBuildingIndex + 1) % availableBuildings.Count;
                } while (currentBuildingIndex != originalBuildingIndex);

                if (targetBuilding == null) break; // No buildings with space

                ProcessSingleUnitGarrison(unit, targetBuilding);

                // Move to next building if current one might be full
                if (!targetBuilding.CanGarrison)
                {
                    currentBuildingIndex = (currentBuildingIndex + 1) % availableBuildings.Count;
                }
            }
        }

        private List<BuildingData> GetAvailableGarrisonBuildings(int playerId, FixedVector3 referencePosition)
        {
            var buildings = new List<BuildingData>();

            foreach (var building in BuildingRegistry.GetAllBuildings())
            {
                if (building.PlayerId != playerId) continue;
                if (building.IsDestroyed || building.IsUnderConstruction) continue;
                if (building.GarrisonCapacity <= 0 || !building.CanGarrison) continue;
                buildings.Add(building);
            }

            // Sort by distance from reference position
            buildings.Sort((a, b) =>
            {
                var distA = (referencePosition - a.SimPosition).SqrMagnitude();
                var distB = (referencePosition - b.SimPosition).SqrMagnitude();
                int cmp = distA.CompareTo(distB);
                return cmp != 0 ? cmp : a.Id.CompareTo(b.Id);
            });

            return buildings;
        }

        private void ProcessSingleUnitGarrison(UnitData unit, BuildingData targetBuilding)
        {
            // Check if unit is adjacent to building (within 2 tiles of footprint)
            Vector2Int unitTile = MapData.WorldToTile(unit.SimPosition);
            bool adjacent = unitTile.x >= targetBuilding.OriginTileX - 2 &&
                unitTile.x <= targetBuilding.OriginTileX + targetBuilding.TileFootprintWidth + 1 &&
                unitTile.y >= targetBuilding.OriginTileZ - 2 &&
                unitTile.y <= targetBuilding.OriginTileZ + targetBuilding.TileFootprintHeight + 1;

            if (adjacent)
            {
                // Unit is adjacent, try to garrison immediately
                GarrisonUnit(unit, targetBuilding);
                // If garrison fails, the unit will be handled by ProcessArrivedGarrisonUnits next tick
            }
            else
            {
                // Set unit to move to building with intention to garrison
                Vector2Int adjTile = FindNearestWalkableAdjacentTile(targetBuilding, unit.SimPosition);
                Vector2Int startTile = MapData.WorldToTile(unit.SimPosition);
                var path = GridPathfinder.FindPath(MapData, startTile, adjTile, unit.PlayerId, BuildingRegistry);
                if (path.Count > 0)
                {
                    unit.ClearCommandQueue();
                    unit.ClearSavedPath();
                    unit.ClearFormation();
                    unit.SetPath(path);
                    unit.FinalDestination = MapData.TileToWorldFixed(adjTile.x, adjTile.y);
                    unit.State = UnitState.MovingToGarrison;
                    unit.PlayerCommanded = true;
                    unit.TargetResourceNodeId = -1;
                    unit.ConstructionTargetBuildingId = -1;
                    unit.TargetGarrisonBuildingId = targetBuilding.Id;
                    unit.CombatTargetId = -1;
                    unit.CombatTargetBuildingId = -1;
                    unit.DropOffBuildingId = -1;
                    unit.ClearPatrol();
                }
            }
        }

        private bool GarrisonUnit(UnitData unit, BuildingData building)
        {
            // Check if building can actually garrison this unit
            if (!building.CanGarrison)
            {
                return false;
            }

            unit.ClearPath();
            unit.ClearFormation();
            unit.ClearCommandQueue();
            unit.ClearSavedPath();
            unit.State = UnitState.Idle;
            unit.TargetResourceNodeId = -1;
            unit.ConstructionTargetBuildingId = -1;
            unit.TargetGarrisonBuildingId = -1;
            unit.CombatTargetId = -1;
            unit.CombatTargetBuildingId = -1;
            unit.DropOffBuildingId = -1;

            unit.ClearPatrol();
            unit.PlayerCommanded = false;

            // Move unit to building center (hidden from view)
            unit.SimPosition = building.SimPosition;
            unit.PreviousSimPosition = building.SimPosition;

            building.GarrisonedUnitIds.Add(unit.Id);

            // Move to garrison storage (won't be moved/attacked/etc)
            UnitRegistry.GarrisonUnit(unit.Id);

            OnUnitGarrisoned?.Invoke(unit.Id, building.Id);
            return true;
        }

        private void ProcessUngarrisonCommand(UngarrisonCommand cmd)
        {
            var building = BuildingRegistry.GetBuilding(cmd.BuildingId);
            if (building == null || building.IsDestroyed) return;
            if (building.PlayerId != cmd.PlayerId) return;
            if (building.GarrisonCount == 0) return;

            UngarrisonAll(building);
        }

        private void UngarrisonAll(BuildingData building)
        {
            for (int i = building.GarrisonedUnitIds.Count - 1; i >= 0; i--)
            {
                int unitId = building.GarrisonedUnitIds[i];

                // Re-add unit to registry
                var unit = UnitRegistry.RestoreUnit(unitId);
                if (unit == null) continue;

                // Place at nearest walkable adjacent tile
                Vector2Int adjTile = FindNearestWalkableAdjacentTile(building, building.SimPosition);
                FixedVector3 spawnPos = MapData.TileToWorldFixed(adjTile.x, adjTile.y);
                unit.SimPosition = spawnPos;
                unit.PreviousSimPosition = spawnPos;
                unit.State = UnitState.Idle;

                // Heal garrisoned units slightly (AoE-style)
                if (unit.CurrentHealth < unit.MaxHealth)
                    unit.CurrentHealth = System.Math.Min(unit.MaxHealth, unit.CurrentHealth + unit.MaxHealth / 10);

                OnUnitUngarrisoned?.Invoke(unitId);
            }
            building.GarrisonedUnitIds.Clear();
        }

        // ---- Target Dummy support ----

        public int CreateDummy(FixedVector3 position)
        {
            // Spawn as player 1 (enemy for player 0) so it can be attacked
            int dummyPlayerId = 1;
            var unitData = UnitRegistry.CreateUnit(dummyPlayerId, position,
                Fixed32.Zero, // zero move speed — stationary
                cachedUnitRadius,
                Fixed32.One); // mass must be > 0
            unitData.MaxHealth = 10000;
            unitData.CurrentHealth = unitData.MaxHealth;
            unitData.AttackDamage = 0;
            unitData.AttackRange = Fixed32.Zero;
            unitData.AttackCooldownTicks = 9999;
            unitData.MeleeArmor = 0;
            unitData.RangedArmor = 0;
            unitData.DetectionRange = Fixed32.Zero;
            unitData.IsDummy = true;

            dummyUnitIds.Add(unitData.Id);
            OnUnitTrained?.Invoke(unitData.Id, 1, dummyPlayerId); // type 1 = spearman visual
            return unitData.Id;
        }

        public void ClearAllDummies()
        {
            for (int i = 0; i < dummyUnitIds.Count; i++)
            {
                var unit = UnitRegistry.GetUnit(dummyUnitIds[i]);
                if (unit != null && unit.State != UnitState.Dead)
                {
                    unit.CurrentHealth = 0;
                    unit.State = UnitState.Dead;
                    OnUnitDied?.Invoke(unit.Id);
                    UnitRegistry.RemoveUnit(unit.Id);
                }
            }
            dummyUnitIds.Clear();
        }

        public int GetPopulation(int playerId)
        {
            return UnitRegistry.GetPopulation(playerId);
        }

        public int GetPopulationCap(int playerId)
        {
            int cap = 0;
            var allBuildings = BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < allBuildings.Count; i++)
            {
                var b = allBuildings[i];
                if (b.PlayerId != playerId || b.IsDestroyed || b.IsUnderConstruction)
                    continue;
                if (b.Type == BuildingType.House)
                    cap += config.HousePopulation;
                else if (b.Type == BuildingType.TownCenter)
                    cap += config.TownCenterPopulation;
            }
            return Mathf.Min(cap, config.MaxPopulation);
        }
        public bool IsBuildingInFrenchLandmarkInfluence(BuildingData building)
        {
            if (GetPlayerCivilization(building.PlayerId) != Civilization.French) return false;

            var allBuildings = BuildingRegistry.GetAllBuildings();
            int influenceRadius = config.LandmarkInfluenceRadius;

            for (int i = 0; i < allBuildings.Count; i++)
            {
                var b = allBuildings[i];
                if (b.Type != BuildingType.Landmark) continue;
                if (b.PlayerId != building.PlayerId) continue;
                if (b.IsDestroyed) continue;
                if (LandmarkDefinitions.Get(b.LandmarkId).Civ != Civilization.French) continue;

                int minX = b.OriginTileX - influenceRadius;
                int maxX = b.OriginTileX + b.TileFootprintWidth + influenceRadius;
                int minZ = b.OriginTileZ - influenceRadius;
                int maxZ = b.OriginTileZ + b.TileFootprintHeight + influenceRadius;

                // AABB overlap: check if any tile of the target building falls within influence
                int bMaxX = building.OriginTileX + building.TileFootprintWidth;
                int bMaxZ = building.OriginTileZ + building.TileFootprintHeight;
                if (bMaxX > minX && building.OriginTileX < maxX &&
                    bMaxZ > minZ && building.OriginTileZ < maxZ)
                    return true;
            }
            return false;
        }
    }
}
