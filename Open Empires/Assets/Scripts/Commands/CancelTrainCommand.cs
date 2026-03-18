namespace OpenEmpires
{
    public struct CancelTrainCommand : ICommand
    {
        public CommandType Type => CommandType.CancelTrain;
        public int PlayerId { get; set; }
        public int BuildingId;
        public int QueueIndex;

        public CancelTrainCommand(int playerId, int buildingId, int queueIndex)
        {
            PlayerId = playerId;
            BuildingId = buildingId;
            QueueIndex = queueIndex;
        }
    }
}
