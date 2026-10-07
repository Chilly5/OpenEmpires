namespace OpenEmpires
{
    // Local observation only. Neither this identity nor its state participates in
    // command serialization, simulation decisions, or state checksums.
    internal sealed class TrainingOrderReceipt
    {
        internal GameSimulation Runtime { get; }
        internal object Issuer { get; }
        internal int ProducerId { get; }
        internal int PlayerId { get; }
        internal int ResolvedUnitType { get; }
        internal long Ordinal { get; }
        internal bool IsQueued { get; private set; } = true;
        internal bool IsCancelled { get; private set; }
        internal bool IsCompleted { get; private set; }

        internal TrainingOrderReceipt(GameSimulation runtime, object issuer, int producerId,
            int playerId, int resolvedUnitType, long ordinal)
        {
            Runtime = runtime;
            Issuer = issuer;
            ProducerId = producerId;
            PlayerId = playerId;
            ResolvedUnitType = resolvedUnitType;
            Ordinal = ordinal;
        }

        internal void MarkDequeued() => IsQueued = false;

        internal void MarkCancelled()
        {
            IsQueued = false;
            IsCancelled = true;
        }

        internal bool MarkCompleted()
        {
            if (IsCancelled || IsCompleted) return false;
            IsQueued = false;
            IsCompleted = true;
            return true;
        }
    }
}
