namespace OpenEmpires
{
    public struct ConstructBuildingCommand : ICommand
    {
        public CommandType Type => CommandType.ConstructBuilding;
        public int PlayerId { get; set; }
        public int[] UnitIds;
        public int TargetBuildingId;
        public bool IsQueued;

        public ConstructBuildingCommand(int playerId, int[] unitIds, int targetBuildingId)
        {
            PlayerId = playerId;
            UnitIds = unitIds;
            TargetBuildingId = targetBuildingId;
            IsQueued = false;
        }
    }
}
