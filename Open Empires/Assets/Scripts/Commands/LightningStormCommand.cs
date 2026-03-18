namespace OpenEmpires
{
    public struct LightningStormCommand : ICommand
    {
        public CommandType Type => CommandType.LightningStorm;
        public int PlayerId { get; set; }
        public int TargetTileX;
        public int TargetTileZ;

        public LightningStormCommand(int playerId, int tileX, int tileZ)
        {
            PlayerId = playerId;
            TargetTileX = tileX;
            TargetTileZ = tileZ;
        }
    }
}
