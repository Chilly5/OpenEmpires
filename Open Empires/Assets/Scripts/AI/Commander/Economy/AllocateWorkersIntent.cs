using System.Collections.Generic;

namespace OpenEmpires
{
    public sealed class AllocateWorkersIntent : CommanderIntent
    {
        public CommanderWorkerAllocation Allocation { get; }
        public AllocateWorkersIntent(int playerId, CommanderWorkerAllocation allocation,
            IEnumerable<CommanderConstraint> constraints = null)
            : base(CommanderIntentType.AllocateWorkers, playerId, constraints) => Allocation = allocation;
    }
}
