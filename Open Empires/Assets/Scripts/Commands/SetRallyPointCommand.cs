namespace OpenEmpires
{
    public struct SetRallyPointCommand : ICommand
    {
        public CommandType Type => CommandType.SetRallyPoint;
        public int PlayerId { get; set; }
        public int BuildingId;
        public FixedVector3 Position;
        public int ResourceNodeId;
        public int TargetUnitId;
        public int TargetBuildingId;

        public SetRallyPointCommand(int playerId, int buildingId, FixedVector3 position, int resourceNodeId = -1, int targetUnitId = -1, int targetBuildingId = -1)
        {
            PlayerId = playerId;
            BuildingId = buildingId;
            Position = position;
            ResourceNodeId = resourceNodeId;
            TargetUnitId = targetUnitId;
            TargetBuildingId = targetBuildingId;
        }
    }
}
