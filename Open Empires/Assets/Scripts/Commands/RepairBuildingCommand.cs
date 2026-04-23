namespace OpenEmpires
{
    public struct RepairBuildingCommand : ICommand
    {
        public CommandType Type => CommandType.RepairBuilding;
        public int PlayerId { get; set; }
        public int[] UnitIds;
        public int TargetBuildingId;
        public bool IsQueued;

        public RepairBuildingCommand(int playerId, int[] unitIds, int targetBuildingId)
        {
            PlayerId = playerId;
            UnitIds = unitIds;
            TargetBuildingId = targetBuildingId;
            IsQueued = false;
        }
    }
}