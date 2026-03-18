namespace OpenEmpires
{
    public struct AttackBuildingCommand : ICommand
    {
        public CommandType Type => CommandType.AttackBuilding;
        public int PlayerId { get; set; }
        public int[] UnitIds;
        public int TargetBuildingId;
        public bool IsQueued;

        public AttackBuildingCommand(int playerId, int[] unitIds, int targetBuildingId)
        {
            PlayerId = playerId;
            UnitIds = unitIds;
            TargetBuildingId = targetBuildingId;
            IsQueued = false;
        }
    }
}
