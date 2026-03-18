namespace OpenEmpires
{
    public struct CheatProductionCommand : ICommand
    {
        public CommandType Type => CommandType.CheatProduction;
        public int PlayerId { get; set; }

        public CheatProductionCommand(int playerId)
        {
            PlayerId = playerId;
        }
    }
}
