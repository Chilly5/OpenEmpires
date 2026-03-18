namespace OpenEmpires
{
    public struct TrainUnitCommand : ICommand
    {
        public CommandType Type => CommandType.TrainUnit;
        public int PlayerId { get; set; }
        public int BuildingId;
        public int UnitType;

        public TrainUnitCommand(int playerId, int buildingId, int unitType)
        {
            PlayerId = playerId;
            BuildingId = buildingId;
            UnitType = unitType;
        }
    }
}
