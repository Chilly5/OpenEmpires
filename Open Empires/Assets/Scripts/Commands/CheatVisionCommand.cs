namespace OpenEmpires
{
    public struct CheatVisionCommand : ICommand
    {
        public CommandType Type => CommandType.CheatVision;
        public int PlayerId { get; set; }

        public CheatVisionCommand(int playerId)
        {
            PlayerId = playerId;
        }
    }
}
