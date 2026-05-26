namespace OpenEmpires
{
    public struct ToggleAutoProduceCommand : ICommand
    {
        public CommandType Type => CommandType.ToggleAutoProduce;
        public int PlayerId { get; set; }
        public int BuildingId;
        public bool Enabled;

        public ToggleAutoProduceCommand(int playerId, int buildingId, bool enabled)
        {
            PlayerId = playerId;
            BuildingId = buildingId;
            Enabled = enabled;
        }
    }
}
