namespace OpenEmpires
{
    public struct HealUnitCommand : ICommand
    {
        public CommandType Type => CommandType.HealUnit;
        public int PlayerId { get; set; }
        public int[] UnitIds;
        public int TargetUnitId;
        public bool IsQueued;
    }
}
