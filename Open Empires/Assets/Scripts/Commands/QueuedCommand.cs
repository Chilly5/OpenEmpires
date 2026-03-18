namespace OpenEmpires
{
    public enum QueuedCommandType { Move, AttackMove, Gather, Construct, DropOff, Patrol, Slaughter }

    public struct QueuedCommand
    {
        public QueuedCommandType Type;
        public FixedVector3 TargetPosition;
        public int ResourceNodeId;  // -1 if not gather
        public int BuildingId;      // -1 if not construct
        public FixedVector3 FacingDirection;
        public bool HasFacing;

        public static QueuedCommand MoveWaypoint(FixedVector3 targetPos)
        {
            return new QueuedCommand
            {
                Type = QueuedCommandType.Move,
                TargetPosition = targetPos,
                ResourceNodeId = -1,
                BuildingId = -1,
                FacingDirection = default,
                HasFacing = false
            };
        }

        public static QueuedCommand MoveWaypoint(FixedVector3 targetPos, FixedVector3 facing)
        {
            return new QueuedCommand
            {
                Type = QueuedCommandType.Move,
                TargetPosition = targetPos,
                ResourceNodeId = -1,
                BuildingId = -1,
                FacingDirection = facing,
                HasFacing = true
            };
        }

        public static QueuedCommand AttackMoveWaypoint(FixedVector3 targetPos)
        {
            return new QueuedCommand
            {
                Type = QueuedCommandType.AttackMove,
                TargetPosition = targetPos,
                ResourceNodeId = -1,
                BuildingId = -1,
                FacingDirection = default,
                HasFacing = false
            };
        }

        public static QueuedCommand GatherWaypoint(FixedVector3 targetPos, int resourceNodeId)
        {
            return new QueuedCommand
            {
                Type = QueuedCommandType.Gather,
                TargetPosition = targetPos,
                ResourceNodeId = resourceNodeId,
                BuildingId = -1,
                FacingDirection = default,
                HasFacing = false
            };
        }

        public static QueuedCommand ConstructWaypoint(int buildingId)
        {
            return new QueuedCommand
            {
                Type = QueuedCommandType.Construct,
                TargetPosition = default,
                ResourceNodeId = -1,
                BuildingId = buildingId,
                FacingDirection = default,
                HasFacing = false
            };
        }

        public static QueuedCommand PatrolWaypoint(FixedVector3 targetPos)
        {
            return new QueuedCommand
            {
                Type = QueuedCommandType.Patrol,
                TargetPosition = targetPos,
                ResourceNodeId = -1,
                BuildingId = -1,
                FacingDirection = default,
                HasFacing = false
            };
        }

        public static QueuedCommand DropOffWaypoint(int buildingId)
        {
            return new QueuedCommand
            {
                Type = QueuedCommandType.DropOff,
                TargetPosition = default,
                ResourceNodeId = -1,
                BuildingId = buildingId,
                FacingDirection = default,
                HasFacing = false
            };
        }

        public static QueuedCommand SlaughterWaypoint(FixedVector3 targetPos, int sheepUnitId)
        {
            return new QueuedCommand
            {
                Type = QueuedCommandType.Slaughter,
                TargetPosition = targetPos,
                ResourceNodeId = sheepUnitId, // Reuse field to hold sheep unit ID
                BuildingId = -1,
                FacingDirection = default,
                HasFacing = false
            };
        }
    }
}
