namespace OpenEmpires
{
    public struct CheatGodModeCommand : ICommand
    {
        public CommandType Type => CommandType.CheatGodMode;
        public int PlayerId { get; set; }

        public CheatGodModeCommand(int playerId)
        {
            PlayerId = playerId;
        }
    }
}
