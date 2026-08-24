namespace OpenEmpires
{
    /// <summary>
    /// Debug spawn: drops a unit of any type straight onto the map, ignoring buildings, cost,
    /// age and civilisation requirements. Exists so unit models and animations can be inspected
    /// without first building a stable and waiting out a training timer.
    ///
    /// Goes through the command buffer like every other command rather than poking the
    /// simulation directly, so it lands on the same tick everywhere and leaves lockstep intact.
    /// </summary>
    public struct CheatSpawnUnitCommand : ICommand
    {
        public CommandType Type => CommandType.CheatSpawnUnit;
        public int PlayerId { get; set; }

        public int UnitType;
        public FixedVector3 Position;
        public int Count;

        /// <summary>Which player owns the spawned units; lets you place an enemy to fight.</summary>
        public int OwnerPlayerId;

        public CheatSpawnUnitCommand(int playerId, int unitType, FixedVector3 position, int count, int ownerPlayerId)
        {
            PlayerId = playerId;
            UnitType = unitType;
            Position = position;
            Count = count;
            OwnerPlayerId = ownerPlayerId;
        }
    }
}
