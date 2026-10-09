using System.Collections.Generic;

namespace OpenEmpires
{
    public sealed class WatchFutureUnitsGoal : CommanderGoal
    {
        public WatchFutureUnitsIntent Order { get; }
        public int ObservedCount => observedUnitIds.Count;
        public int AssignedCount => assignedUnitIds.Count;
        public int InterruptedCount => interruptedUnitIds.Count;
        public int ProducerId => producerId;

        internal BuildingData BoundProducer;
        internal int producerId;
        internal long LastSeenBirthOrdinal;
        internal readonly List<int> observedUnitIds = new List<int>();
        internal readonly HashSet<int> assignedUnitIds = new HashSet<int>();
        internal readonly HashSet<int> interruptedUnitIds = new HashSet<int>();
        internal readonly HashSet<int> seenUnitIds = new HashSet<int>();
        internal readonly HashSet<int> issuedUnitIds = new HashSet<int>();
        internal readonly HashSet<int> rejectedUnitIds = new HashSet<int>();
        internal readonly Dictionary<int, ICommand> pendingCommands = new Dictionary<int, ICommand>();

        internal WatchFutureUnitsGoal(WatchFutureUnitsIntent order, BuildingData producer,
            int maxDurationTicks, long birthWatermark) : base(order.PlayerId, CommanderGoalType.WatchFutureUnits, maxDurationTicks)
        {
            Order = order;
            BoundProducer = producer;
            producerId = producer.Id;
            LastSeenBirthOrdinal = birthWatermark;
        }

        internal void ReleaseObservations()
        {
            BoundProducer = null;
            pendingCommands.Clear();
        }
    }
}
