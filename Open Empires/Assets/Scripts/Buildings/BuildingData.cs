using System.Collections.Generic;

namespace OpenEmpires
{
    public enum BuildingType
    {
        House,
        Barracks,
        TownCenter,
        Wall,
        Mill,
        LumberYard,
        Mine,
        ArcheryRange,
        Stables,
        Farm,
        Tower,
        Monastery,
        Landmark
    }

    public class BuildingData
    {
        public int Id;
        public int PlayerId;
        public BuildingType Type;
        public FixedVector3 SimPosition;

        // Health
        public int MaxHealth;
        public int CurrentHealth;
        public int Armor;
        public bool IsDestroyed => CurrentHealth <= 0;

        // Construction
        public bool IsUnderConstruction;
        public bool ConstructionStarted; // true once a villager actually begins building
        public int ConstructionTicksRemaining;
        public int ConstructionTicksTotal;
        public float ConstructionProgress => ConstructionTicksTotal > 0
            ? 1f - (float)ConstructionTicksRemaining / ConstructionTicksTotal : 1f;

        // Tile footprint
        public int OriginTileX;
        public int OriginTileZ;
        public int TileFootprintWidth;
        public int TileFootprintHeight;
        public int FoundationBorder;

        // Combat feedback (sim writes, view reads)
        public int LastDamageTick;
        public FixedVector3 LastDamageFromPos;

        // Training queue
        public List<int> TrainingQueue;
        public int TrainingTicksRemaining;
        public int TrainingTicksTotal;
        public bool IsTraining => TrainingQueue.Count > 0;
        public float TrainingProgress => TrainingTicksTotal > 0
            ? 1f - (float)TrainingTicksRemaining / TrainingTicksTotal : 0f;

        // Rally point
        public bool HasRallyPoint;
        public FixedVector3 RallyPoint;
        public bool RallyPointOnResource;
        public ResourceType RallyPointResourceType;
        public int RallyPointUnitId = -1;
        public bool RallyPointOnConstruction;
        public int RallyPointConstructionBuildingId = -1;

        // Wall grouping (links segments from same drag placement)
        public int WallGroupId;

        // Gate (wall segment that allows units to pass through)
        public bool IsGate;

        // Landmark identity (only meaningful when Type == Landmark)
        public LandmarkId LandmarkId;

        // Main Town Center flag (for initial/starting town centers vs player-built ones)
        public bool IsMainTownCenter;

        // Farm linkage to resource node
        public int LinkedResourceNodeId = -1;

        // Garrison
        public List<int> GarrisonedUnitIds;
        public int GarrisonCapacity;

        // Tower upgrades
        public bool HasArrowSlits;
        public bool HasCannonEmplacement;
        public bool HasStoneUpgrade;
        public bool HasVisionUpgrade;

        // Tower combat (for towers with attack capability)
        public int AttackDamage;
        public Fixed32 AttackRange;
        public Fixed32 DetectionRange;
        public int AttackCooldownTicks;
        public int AttackCooldownRemaining;
        public int BaseArrowCount = 1; // Base number of arrows shot per attack
        public int ArrowCount => Type == BuildingType.Tower 
            ? BaseArrowCount + GarrisonCount  // Towers get +1 arrow per garrisoned unit
            : (AttackDamage > 0 ? BaseArrowCount + GarrisonCount : 0); // Other buildings (TC) with attack
        public int LastAttackTick;
        public FixedVector3 LastAttackTargetPos;

        // Tower target tracking
        public int TowerTargetUnitId = -1;
        public int TowerTargetBuildingId = -1;
        public int CombatTargetUnitId = -1; // For general building combat

        // Tower upgrade system
        public bool IsUpgrading;
        public TowerUpgradeType CurrentUpgrade;
        public int UpgradeTicksRemaining;
        public int UpgradeTicksTotal;
        public List<TowerUpgradeType> UpgradeQueue;
        public float UpgradeProgress => UpgradeTicksTotal > 0
            ? 1f - (float)UpgradeTicksRemaining / UpgradeTicksTotal : 0f;

        public int GarrisonCount => GarrisonedUnitIds != null ? GarrisonedUnitIds.Count : 0;
        public bool CanGarrison => GarrisonCapacity > 0 && GarrisonCount < GarrisonCapacity;

        public BuildingData(int id, int playerId, BuildingType type, FixedVector3 position,
            int originTileX, int originTileZ, int footprintWidth, int footprintHeight)
        {
            Id = id;
            PlayerId = playerId;
            Type = type;
            SimPosition = position;
            OriginTileX = originTileX;
            OriginTileZ = originTileZ;
            TileFootprintWidth = footprintWidth;
            TileFootprintHeight = footprintHeight;
            TrainingQueue = new List<int>();
            UpgradeQueue = new List<TowerUpgradeType>();
            GarrisonedUnitIds = new List<int>();
        }

        public void EnqueueTraining(int unitType, int ticks)
        {
            TrainingQueue.Add(unitType);
            if (TrainingQueue.Count == 1)
            {
                TrainingTicksRemaining = ticks;
                TrainingTicksTotal = ticks;
            }
        }

        public int DequeueTraining()
        {
            if (TrainingQueue.Count == 0) return -1;
            int unitType = TrainingQueue[0];
            TrainingQueue.RemoveAt(0);
            return unitType;
        }

        public void EnqueueUpgrade(TowerUpgradeType upgradeType, int ticks)
        {
            UpgradeQueue.Add(upgradeType);
            if (UpgradeQueue.Count == 1)
            {
                IsUpgrading = true;
                CurrentUpgrade = upgradeType;
                UpgradeTicksRemaining = ticks;
                UpgradeTicksTotal = ticks;
            }
        }

        public TowerUpgradeType DequeueUpgrade()
        {
            if (UpgradeQueue.Count == 0) return default;
            TowerUpgradeType upgradeType = UpgradeQueue[0];
            UpgradeQueue.RemoveAt(0);
            return upgradeType;
        }
    }
}
