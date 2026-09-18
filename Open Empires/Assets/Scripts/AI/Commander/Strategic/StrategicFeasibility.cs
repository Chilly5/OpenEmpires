using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    // A detached quote, not a plan or a reservation. Only the game-side planner builds it.
    public sealed class StrategicFeasibility
    {
        public StrategicObjectiveType Objective { get; }
        public bool Capable => RejectionReason.Length == 0;
        public string RejectionReason { get; }
        public IReadOnlyList<StrategicRequirementState> Costs { get; }

        internal StrategicFeasibility(StrategicObjectiveType objective, string reason,
            List<StrategicRequirementState> costs)
        {
            Objective = objective;
            RejectionReason = reason ?? string.Empty;
            Costs = Array.AsReadOnly(costs.ToArray());
        }
    }
}
