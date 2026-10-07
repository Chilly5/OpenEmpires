using System;

namespace OpenEmpires
{
    internal sealed partial class CommanderPlanner
    {
        private CommanderPlan PlanRestrictedPreparation(CommanderGoal parent, ResourceType resource,
            ResourceSourceKind source, int tick, int owned, int queued, string reason, ResourceType? preserve)
        {
            if (!parent.PreparationAllocators.TryGetValue(resource, out var preparation))
            {
                // A bounded planner-internal view, not a registered/independently
                // authorized goal. Existing economy resolution and native commands
                // retain all ownership, source, reachability and takeover rules.
                preparation = new AllocateWorkersGoal(parent.PlayerId,
                    new CommanderWorkerAllocation(CommanderWorkerAllocationMode.SelectedCount,
                        CommanderWorkerCountMode.Exact, 1,
                        new CommanderWorkerSelector(parent.UseIdleWorkersOnly ? CommanderWorkerState.Idle : CommanderWorkerState.Any),
                        new CommanderResourceDestination(resource, source)), parent.MaxDurationTicks);
                preparation.GoalId = parent.GoalId;
                preparation.ParentGoalId = parent.GoalId;
                preparation.RuntimeOwner = parent.RuntimeOwner;
                preparation.RequestAuthority = parent.RequestAuthority;
                preparation.RequestNodeIndex = parent.RequestNodeIndex;
                preparation.FrozenWorkerIds = parent.FrozenWorkerIds;
                foreach (var floor in parent.ProtectedWorkerMinimums)
                    preparation.ProtectedWorkerMinimums.Add(floor.Key, floor.Value);
                parent.PreparationAllocators.Add(resource, preparation);
            }
            // Concurrent age preparation can acquire opposing resource workers
            // after this view was first blocked. Refresh that floor on every retry,
            // preserving (never relaxing) earlier restrictions.
            if (preserve.HasValue)
                preparation.ProtectedWorkerMinimums[preserve.Value] = Math.Max(
                    preparation.ProtectedWorkerMinimums.TryGetValue(preserve.Value, out int existing) ? existing : 0,
                    CountResourceWorkers(parent.PlayerId, preserve.Value));
            preparation.HumanInterrupted |= parent.FrozenWorkerHumanOverride;
            var plan = PlanWorkerAllocation(preparation, tick);
            return new CommanderPlan(plan.Status == CommanderGoalStatus.Blocked
                ? CommanderGoalStatus.Blocked : CommanderGoalStatus.WaitingForResources,
                reason + " Source-restricted preparation: " + plan.Reason, owned, queued, plan.Command);
        }

        internal void ObservePreparationCommand(CommanderGoal parent, ICommand command)
        {
            foreach (var preparation in parent.PreparationAllocators.Values)
                if (preparation.NextCommandGroup < preparation.CommandGroups.Count
                    && ReferenceEquals(preparation.CommandGroups[preparation.NextCommandGroup], command))
                {
                    preparation.NextCommandGroup++;
                    preparation.LastIssuedSimulationTick = simulation.CurrentTick;
                    return;
                }
        }
    }
}
