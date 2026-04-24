namespace OpenEmpires
{
    public struct CheatConstructionCommand : ICommand
    {
        public CommandType Type => CommandType.CheatConstruction;
        public int PlayerId { get; set; }

        public CheatConstructionCommand(int playerId)
        {
            PlayerId = playerId;
        }
    }
}
