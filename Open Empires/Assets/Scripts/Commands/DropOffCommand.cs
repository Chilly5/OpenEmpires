namespace OpenEmpires
{
    public struct DropOffCommand : ICommand
    {
        public CommandType Type => CommandType.DropOff;
        public int PlayerId { get; set; }
        public int[] UnitIds;
        public int TargetBuildingId;
        public bool IsQueued;

        public DropOffCommand(int playerId, int[] unitIds, int targetBuildingId)
        {
            PlayerId = playerId;
            UnitIds = unitIds;
            TargetBuildingId = targetBuildingId;
            IsQueued = false;
        }
    }
}
