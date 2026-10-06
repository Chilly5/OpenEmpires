namespace OpenEmpires
{
    public struct GatherCommand : ICommand
    {
        public CommandType Type => CommandType.Gather;
        public int PlayerId { get; set; }
        public int[] UnitIds;
        public int ResourceNodeId;
        public bool IsQueued;
        public ResourceSourceKind SourceKind;

        public GatherCommand(int playerId, int[] unitIds, int resourceNodeId, ResourceSourceKind sourceKind = ResourceSourceKind.Any)
        {
            PlayerId = playerId;
            UnitIds = unitIds;
            ResourceNodeId = resourceNodeId;
            IsQueued = false;
            SourceKind = sourceKind;
        }
    }
}
