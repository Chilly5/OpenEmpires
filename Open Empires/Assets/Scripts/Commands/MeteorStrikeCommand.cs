namespace OpenEmpires
{
    public struct MeteorStrikeCommand : ICommand
    {
        public CommandType Type => CommandType.MeteorStrike;
        public int PlayerId { get; set; }
        public int TargetTileX;
        public int TargetTileZ;

        public MeteorStrikeCommand(int playerId, int tileX, int tileZ)
        {
            PlayerId = playerId;
            TargetTileX = tileX;
            TargetTileZ = tileZ;
        }
    }
}
