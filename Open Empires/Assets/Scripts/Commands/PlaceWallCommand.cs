namespace OpenEmpires
{
    public struct PlaceWallCommand : ICommand
    {
        public CommandType Type => CommandType.PlaceWall;
        public int PlayerId { get; set; }
        public int StartTileX;
        public int StartTileZ;
        public int EndTileX;
        public int EndTileZ;
        public int[] VillagerUnitIds;
        public bool IsQueued;
        public BuildingType WallBuildingType;
        public bool IsGate;

        public PlaceWallCommand(int playerId, int startTileX, int startTileZ, int endTileX, int endTileZ, int[] villagerUnitIds = null, BuildingType wallBuildingType = BuildingType.Wall, bool isGate = false)
        {
            PlayerId = playerId;
            StartTileX = startTileX;
            StartTileZ = startTileZ;
            EndTileX = endTileX;
            EndTileZ = endTileZ;
            VillagerUnitIds = villagerUnitIds;
            IsQueued = false;
            WallBuildingType = wallBuildingType;
            IsGate = isGate;
        }
    }
}
