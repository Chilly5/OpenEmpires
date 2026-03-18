namespace OpenEmpires
{
    public struct HealingRainCommand : ICommand
    {
        public CommandType Type => CommandType.HealingRain;
        public int PlayerId { get; set; }
        public int TargetTileX;
        public int TargetTileZ;

        public HealingRainCommand(int playerId, int tileX, int tileZ)
        {
            PlayerId = playerId;
            TargetTileX = tileX;
            TargetTileZ = tileZ;
        }
    }
}
