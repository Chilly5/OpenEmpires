namespace OpenEmpires
{
    public struct FollowUnitCommand : ICommand
    {
        public CommandType Type => CommandType.FollowUnit;
        public int PlayerId { get; set; }
        public int[] UnitIds;
        public int TargetUnitId;
        public bool IsQueued;
    }
}
