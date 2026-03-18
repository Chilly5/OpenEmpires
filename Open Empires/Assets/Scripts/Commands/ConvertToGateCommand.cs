namespace OpenEmpires
{
    public struct ConvertToGateCommand : ICommand
    {
        public CommandType Type => CommandType.ConvertToGate;
        public int PlayerId { get; set; }
        public int BuildingId;

        public ConvertToGateCommand(int playerId, int buildingId)
        {
            PlayerId = playerId;
            BuildingId = buildingId;
        }
    }
}
