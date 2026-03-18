namespace OpenEmpires
{
    public struct TsunamiCommand : ICommand
    {
        public CommandType Type => CommandType.Tsunami;
        public int PlayerId { get; set; }
        public int TargetTileX;
        public int TargetTileZ;
        public int DirectionX;  // Fixed32 raw value
        public int DirectionZ;  // Fixed32 raw value

        public TsunamiCommand(int playerId, int tileX, int tileZ, int dirX, int dirZ)
        {
            PlayerId = playerId;
            TargetTileX = tileX;
            TargetTileZ = tileZ;
            DirectionX = dirX;
            DirectionZ = dirZ;
        }
    }
}
