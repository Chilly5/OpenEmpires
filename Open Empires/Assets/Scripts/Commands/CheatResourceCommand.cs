namespace OpenEmpires
{
    public struct CheatResourceCommand : ICommand
    {
        public CommandType Type => CommandType.CheatResource;
        public int PlayerId { get; set; }

        public CheatResourceCommand(int playerId)
        {
            PlayerId = playerId;
        }
    }
}
