namespace OpenEmpires
{
    public struct SlaughterSheepCommand : ICommand
    {
        public CommandType Type => CommandType.SlaughterSheep;
        public int PlayerId { get; set; }
        public int[] VillagerIds;
        public int SheepUnitId;
        public bool IsQueued;
    }
}
