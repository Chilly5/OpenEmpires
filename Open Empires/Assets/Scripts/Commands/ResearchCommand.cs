namespace OpenEmpires
{
    public struct ResearchCommand : ICommand
    {
        public CommandType Type => CommandType.Research;
        public int PlayerId { get; set; }
        public int BuildingId;
        public int TechType; // TechnologyType cast to int

        public ResearchCommand(int playerId, int buildingId, TechnologyType tech)
        {
            PlayerId = playerId;
            BuildingId = buildingId;
            TechType = (int)tech;
        }
    }
}
