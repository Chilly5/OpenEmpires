namespace OpenEmpires
{
    public struct MoveCommand : ICommand
    {
        public CommandType Type => CommandType.Move;
        public int PlayerId { get; set; }
        public int[] UnitIds;
        public FixedVector3 TargetPosition;
        public FixedVector3[] FormationPositions;
        public FixedVector3 FacingDirection;
        public bool HasFacing;
        public bool PreserveFormation;
        public bool IsAttackMove;
        public bool IsQueued;

        public MoveCommand(int playerId, int[] unitIds, FixedVector3 targetPosition)
        {
            PlayerId = playerId;
            UnitIds = unitIds;
            TargetPosition = targetPosition;
            FormationPositions = null;
            FacingDirection = default;
            HasFacing = false;
            PreserveFormation = false;
            IsAttackMove = false;
            IsQueued = false;
        }

        public MoveCommand(int playerId, int[] unitIds, FixedVector3 targetPosition, FixedVector3[] formationPositions)
        {
            PlayerId = playerId;
            UnitIds = unitIds;
            TargetPosition = targetPosition;
            FormationPositions = formationPositions;
            FacingDirection = default;
            HasFacing = false;
            PreserveFormation = false;
            IsAttackMove = false;
            IsQueued = false;
        }

        public MoveCommand(int playerId, int[] unitIds, FixedVector3 targetPosition, FixedVector3[] formationPositions, FixedVector3 facingDirection)
        {
            PlayerId = playerId;
            UnitIds = unitIds;
            TargetPosition = targetPosition;
            FormationPositions = formationPositions;
            FacingDirection = facingDirection;
            HasFacing = true;
            PreserveFormation = false;
            IsAttackMove = false;
            IsQueued = false;
        }

        public MoveCommand(int playerId, int[] unitIds, FixedVector3 targetPosition, bool preserveFormation)
        {
            PlayerId = playerId;
            UnitIds = unitIds;
            TargetPosition = targetPosition;
            FormationPositions = null;
            FacingDirection = default;
            HasFacing = false;
            PreserveFormation = preserveFormation;
            IsAttackMove = false;
            IsQueued = false;
        }
    }
}
