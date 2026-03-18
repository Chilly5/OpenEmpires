namespace OpenEmpires
{
    public struct GatherCommand : ICommand
    {
        public CommandType Type => CommandType.Gather;
        public int PlayerId { get; set; }
        public int[] UnitIds;
        public int ResourceNodeId;
        public bool IsQueued;

        public GatherCommand(int playerId, int[] unitIds, int resourceNodeId)
        {
            PlayerId = playerId;
            UnitIds = unitIds;
            ResourceNodeId = resourceNodeId;
            IsQueued = false;
        }
    }
}
