namespace OpenEmpires
{
    public struct GarrisonCommand : ICommand
    {
        public CommandType Type => CommandType.Garrison;
        public int PlayerId { get; set; }
        public int[] UnitIds;
        public int TargetBuildingId;

        public GarrisonCommand(int playerId, int[] unitIds, int targetBuildingId)
        {
            PlayerId = playerId;
            UnitIds = unitIds;
            TargetBuildingId = targetBuildingId;
        }
    }
}
