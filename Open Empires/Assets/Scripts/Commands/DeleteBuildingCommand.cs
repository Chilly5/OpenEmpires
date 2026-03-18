namespace OpenEmpires
{
    public struct DeleteBuildingCommand : ICommand
    {
        public CommandType Type => CommandType.DeleteBuilding;
        public int PlayerId { get; set; }
        public int BuildingId;

        public DeleteBuildingCommand(int playerId, int buildingId)
        {
            PlayerId = playerId;
            BuildingId = buildingId;
        }
    }
}
