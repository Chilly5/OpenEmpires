namespace OpenEmpires
{
    public struct PlaceBuildingCommand : ICommand
    {
        public CommandType Type => CommandType.PlaceBuilding;
        public int PlayerId { get; set; }
        public BuildingType BuildingType;
        public int TileX;
        public int TileZ;
        public int[] VillagerUnitIds;
        public bool IsQueued;
        public int LandmarkIdValue; // cast of LandmarkId, only meaningful when BuildingType == Landmark

        public PlaceBuildingCommand(int playerId, BuildingType buildingType, int tileX, int tileZ, int[] villagerUnitIds = null)
        {
            PlayerId = playerId;
            BuildingType = buildingType;
            TileX = tileX;
            TileZ = tileZ;
            VillagerUnitIds = villagerUnitIds;
            IsQueued = false;
            LandmarkIdValue = -1;
        }
    }
}
