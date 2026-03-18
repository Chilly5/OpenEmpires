namespace OpenEmpires
{
    public struct DeleteUnitsCommand : ICommand
    {
        public CommandType Type => CommandType.DeleteUnits;
        public int PlayerId { get; set; }
        public int[] UnitIds;

        public DeleteUnitsCommand(int playerId, int[] unitIds)
        {
            PlayerId = playerId;
            UnitIds = unitIds;
        }
    }
}
