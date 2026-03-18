namespace OpenEmpires
{
    public struct PatrolCommand : ICommand
    {
        public CommandType Type => CommandType.Patrol;
        public int PlayerId { get; set; }
        public int[] UnitIds;
        public FixedVector3 TargetPosition;
        public bool IsQueued;

        public PatrolCommand(int playerId, int[] unitIds, FixedVector3 targetPosition)
        {
            PlayerId = playerId;
            UnitIds = unitIds;
            TargetPosition = targetPosition;
            IsQueued = false;
        }
    }
}
