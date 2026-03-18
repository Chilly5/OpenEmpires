namespace OpenEmpires
{
    public struct AttackUnitCommand : ICommand
    {
        public CommandType Type => CommandType.AttackUnit;
        public int PlayerId { get; set; }
        public int[] UnitIds;
        public int TargetUnitId;
        public bool IsQueued;

        public AttackUnitCommand(int playerId, int[] unitIds, int targetUnitId)
        {
            PlayerId = playerId;
            UnitIds = unitIds;
            TargetUnitId = targetUnitId;
            IsQueued = false;
        }
    }
}
