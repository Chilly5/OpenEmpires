namespace OpenEmpires
{
    public struct UngarrisonCommand : ICommand
    {
        public CommandType Type => CommandType.Ungarrison;
        public int PlayerId { get; set; }
        public int BuildingId;

        public UngarrisonCommand(int playerId, int buildingId)
        {
            PlayerId = playerId;
            BuildingId = buildingId;
        }
    }
}
