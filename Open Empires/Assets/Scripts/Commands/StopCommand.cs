namespace OpenEmpires
{
    public struct StopCommand : ICommand
    {
        public CommandType Type => CommandType.Stop;
        public int PlayerId { get; set; }
        public int[] UnitIds;

        public StopCommand(int playerId, int[] unitIds)
        {
            PlayerId = playerId;
            UnitIds = unitIds;
        }
    }
}
