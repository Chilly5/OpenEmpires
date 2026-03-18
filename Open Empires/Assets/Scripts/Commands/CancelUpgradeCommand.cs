namespace OpenEmpires
{
    public struct CancelUpgradeCommand : ICommand
    {
        public CommandType Type => CommandType.CancelUpgrade;
        public int PlayerId { get; set; }
        public int BuildingId;
        public int QueueIndex;

        public CancelUpgradeCommand(int playerId, int buildingId, int queueIndex)
        {
            PlayerId = playerId;
            BuildingId = buildingId;
            QueueIndex = queueIndex;
        }
    }
}