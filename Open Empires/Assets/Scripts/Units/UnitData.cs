using System.Collections.Generic;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace OpenEmpires
{
    public enum UnitState
    {
        Idle,
        Moving,
        MovingToGather,
        Gathering,
        MovingToBuild,
        Constructing,
        MovingToDropoff,
        DroppingOff,
        MovingToGarrison,
        InCombat,
        Dead,
        Following,          // Sheep following a unit
        MovingToSlaughter   // Villager walking to sheep to kill it
    }

    public class UnitData
    {
        public int Id;
        public int PlayerId;
        public UnitState State;
        public FixedVector3 SimPosition;
        public FixedVector3 PreviousSimPosition;

        // Pathfinding (tile coordinates)
        public List<Vector2Int> Path;
        public int CurrentPathIndex;

        // Gathering
        public int TargetResourceNodeId = -1;
        public Fixed32 GatherTimer;

        // Carrying (resource drop-off)
        public ResourceType CarriedResourceType;
        public int CarriedResourceAmount;
        public int CarryCapacity = 10;
        public int DropOffBuildingId = -1;

        // Construction
        public int ConstructionTargetBuildingId = -1;

        // Garrison
        public int TargetGarrisonBuildingId = -1;

        // Patrol
        public List<FixedVector3> PatrolWaypoints;
        public int PatrolCurrentIndex;
        public bool IsPatrolling;
        public bool PatrolForward = true;

        // Movement
        public Fixed32 MoveSpeed;
        public Fixed32 Radius;
        public Fixed32 Mass;
        public FixedVector3 FinalDestination;

        // Facing
        public FixedVector3 SimFacing;
        public FixedVector3 TargetFacing;
        public bool HasTargetFacing;

        // Formation
        public FixedVector3 FormationOffset;
        public FixedVector3 FormationFacing;
        public bool InFormation;
        public int FormationGroupId;    // 0 = no group
        public int FormationGroupSize;  // total units in this formation group
        public Fixed32 FormationMoveSpeed;
        public int FormationLeaderId = -1;  // -1 = no leader (is leader or not in formation)

        // Unit type
        public int UnitType; // 0=Villager, 1=Spearman, 2=Archer, 3=Horseman, 4=Scout, 5=Sheep, 6=ManAtArms, 7=Knight, 8=Crossbowman, 9=Monk
        public bool IsVillager;
        public bool IsDummy;
        public bool IsSheep;
        public bool IsHealer;
        public int HealTargetId = -1;
        public int FollowTargetId = -1;
        public int WanderCooldown; // Ticks until next idle wander (sheep)
        public FixedVector3 SpawnPosition; // Original position for wander radius

        public const int NeutralPlayerId = -1;

        // Idle timing
        public Fixed32 IdleTimer; // How long the unit has been in idle state

        // Player command override (suppresses AI aggro)
        public bool PlayerCommanded;
        public bool IsAttackMoving;

        // Leash: AI-aggroed units return home after chasing too far
        public FixedVector3 LeashOrigin;
        public FixedVector3 LeashFacing;
        public bool HasLeash;

        // Combat
        public int MaxHealth;
        public int CurrentHealth;
        public int AttackDamage;
        public Fixed32 AttackRange;
        public int AttackCooldownTicks;
        public int AttackCooldownRemaining;
        public int MeleeArmor;
        public int RangedArmor;
        public Fixed32 DetectionRange;
        public bool IsRanged;

        // Bonus damage (rock-paper-scissors)
        public int BonusDamageVsType = -1; // target UnitType, -1 = none
        public int BonusDamageAmount;
        public int BonusDamageVsType2 = -1; // second bonus target, -1 = none
        public int BonusDamageAmount2;

        // Charge
        public bool IsCharging;
        public int ChargeCooldownRemaining;
        public int ChargeStunRemaining;
        public int CombatTargetId = -1;
        public int CombatTargetBuildingId = -1;
        public int ChaseBlockedTicks;

        // Combat feedback (sim writes, view reads)
        public int LastAttackTick;
        public FixedVector3 LastAttackTargetPos;
        public int LastDamageTick;
        public FixedVector3 LastDamageFromPos;
        public int LastChargeHitTick;
        public FixedVector3 LastChargeHitFromPos;

        // Heal feedback (sim writes, view reads)
        public int LastHealTick;
        public int LastHealAmount;

        // Resource deposit feedback (sim writes, view reads)
        public int LastDepositTick;
        public int LastDepositAmount;
        public ResourceType LastDepositResourceType;

        // Saved path for combat interruption
        public List<Vector2Int> SavedPath;
        public int SavedPathIndex;
        public FixedVector3 SavedFinalDestination;
        public UnitState SavedState;
        public bool HasSavedPath;

        // Shift-queue waypoint commands
        public List<QueuedCommand> CommandQueue;

        public UnitData(int id, int playerId, FixedVector3 position, Fixed32 moveSpeed, Fixed32 radius, Fixed32 mass)
        {
            Debug.Assert(mass > Fixed32.Zero, $"Unit mass must be > 0 (got {mass}) to avoid division by zero in separation system");
            Id = id;
            PlayerId = playerId;
            SimPosition = position;
            PreviousSimPosition = position;
            State = UnitState.Idle;
            MoveSpeed = moveSpeed;
            Radius = radius;
            Mass = mass;
            SimFacing = new FixedVector3(Fixed32.Zero, Fixed32.Zero, Fixed32.One);
            Path = new List<Vector2Int>();
            CurrentPathIndex = 0;
            CommandQueue = new List<QueuedCommand>();
            PatrolWaypoints = new List<FixedVector3>();
        }

        public bool HasPath => Path != null && CurrentPathIndex < Path.Count;

        public void ClearPath()
        {
            Path.Clear();
            CurrentPathIndex = 0;
            HasTargetFacing = false;
            TargetFacing = default;
        }

        public void SetPath(List<Vector2Int> newPath)
        {
            Path = newPath;
            CurrentPathIndex = 0;
        }

        public void ClearFormation()
        {
            InFormation = false;
            FormationOffset = default;
            FormationFacing = default;
            FormationGroupId = 0;
            FormationGroupSize = 0;
            FormationMoveSpeed = Fixed32.Zero;
            FormationLeaderId = -1;
        }

        public void SavePathForCombat()
        {
            SavedPath = new List<Vector2Int>(Path.Count - CurrentPathIndex);
            for (int i = CurrentPathIndex; i < Path.Count; i++)
                SavedPath.Add(Path[i]);
            SavedPathIndex = 0;
            SavedFinalDestination = FinalDestination;
            SavedState = State;
            HasSavedPath = true;
        }

        public void RestoreSavedPath()
        {
            Path = SavedPath;
            CurrentPathIndex = SavedPathIndex;
            FinalDestination = SavedFinalDestination;
            State = SavedState;
            HasTargetFacing = false;
            HasSavedPath = false;
            SavedPath = null;
            CombatTargetId = -1;
            CombatTargetBuildingId = -1;
            HasLeash = false;
        }

        public void ClearSavedPath()
        {
            HasSavedPath = false;
            SavedPath = null;
        }

        public bool HasQueuedCommands => CommandQueue != null && CommandQueue.Count > 0;

        public QueuedCommand DequeueCommand()
        {
            var cmd = CommandQueue[0];
            CommandQueue.RemoveAt(0);
            return cmd;
        }

        public void ClearPatrol()
        {
            IsPatrolling = false;
            PatrolWaypoints?.Clear();
            PatrolCurrentIndex = 0;
            PatrolForward = true;
        }

        public void ClearCommandQueue()
        {
            CommandQueue.Clear();
        }

        public void StorePreviousPosition()
        {
            PreviousSimPosition = SimPosition;
        }
    }
}
