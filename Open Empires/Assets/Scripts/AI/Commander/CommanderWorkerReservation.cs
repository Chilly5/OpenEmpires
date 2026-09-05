namespace OpenEmpires
{
    public enum CommanderWorkerReservationType { Gatherer, Builder }

    // Local tactical ownership, never serialized into gameplay commands or replicated.
    public readonly struct CommanderWorkerReservation
    {
        public int WorkerId { get; }
        public int PlayerId { get; }
        public int GoalId { get; }
        public CommanderWorkerReservationType ReservationType { get; }
        public int CreatedTick { get; }

        internal CommanderWorkerReservation(int workerId, int playerId, int goalId,
            CommanderWorkerReservationType reservationType, int createdTick)
        {
            WorkerId = workerId; PlayerId = playerId; GoalId = goalId;
            ReservationType = reservationType; CreatedTick = createdTick;
        }
    }
}
