using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public enum CommanderTaskStatus { Waiting, Working, Blocked, Completed, Failed, Cancelled }

    // Detached values only. The UI never retains mutable goals, plans or command objects.
    public sealed class CommanderTaskStepSnapshot
    {
        public int GoalId { get; }
        public string Label { get; }
        public string Progress { get; }
        public string Blocker { get; }
        public CommanderTaskStatus Status { get; }
        internal CommanderTaskStepSnapshot(int id, string label, string progress, string blocker, CommanderTaskStatus status)
        { GoalId = id; Label = label; Progress = progress; Blocker = blocker; Status = status; }
    }

    public sealed class CommanderTaskCardSnapshot
    {
        public long RequestId { get; }
        public int Generation { get; }
        public int PlayerId { get; }
        public int CreatedTick { get; }
        public int StrategicPlanId { get; }
        public int StrategicRevision { get; }
        public long RuntimeToken { get; }
        public string Objective { get; }
        public string Progress { get; }
        public string CurrentStep { get; }
        public string Blocker { get; }
        public CommanderTaskStatus Status { get; }
        public bool CanCancel { get; }
        public IReadOnlyList<CommanderTaskStepSnapshot> Steps { get; }
        internal CommanderTaskCardSnapshot(long requestId, int generation, int playerId, int createdTick,
            int planId, int revision, long runtimeToken, string objective, string progress, string currentStep,
            string blocker, CommanderTaskStatus status, bool canCancel, CommanderTaskStepSnapshot[] steps)
        {
            RequestId = requestId; Generation = generation; PlayerId = playerId; CreatedTick = createdTick;
            StrategicPlanId = planId; StrategicRevision = revision; RuntimeToken = runtimeToken;
            Objective = objective; Progress = progress; CurrentStep = currentStep; Blocker = blocker;
            Status = status; CanCancel = canCancel; Steps = Array.AsReadOnly(steps);
        }
    }

    public sealed class CommanderTaskBoardSnapshot
    {
        public IReadOnlyList<CommanderTaskCardSnapshot> Cards { get; }
        public int ActiveCount { get; }
        internal CommanderTaskBoardSnapshot(CommanderTaskCardSnapshot[] cards, int activeCount)
        { Cards = Array.AsReadOnly(cards); ActiveCount = activeCount; }
    }

    public static class CommanderTaskBoardProjection
    {
        public const int TerminalHistory = 8;
        public const int MaximumCards = CommanderGoalManager.MaxActiveGoals + StrategicPlanner.MaxActivePlans + TerminalHistory;

        public static CommanderTaskBoardSnapshot Capture(CommanderGoalManager manager, StrategicPlanner planner, int generation)
        {
            if (manager == null || manager.IsDisposed || generation < 0)
                return new CommanderTaskBoardSnapshot(Array.Empty<CommanderTaskCardSnapshot>(), 0);
            var groups = new Dictionary<long, List<CommanderGoal>>();
            var order = new List<long>();
            var strategicChildren = new HashSet<int>();
            bool samePlanner = planner != null && planner.PlayerId == manager.PlayerId
                && planner.UsesContext(manager.Simulation, manager);
            if (samePlanner)
                foreach (var plan in planner.Plans)
                    foreach (int id in plan.ChildGoalIds) strategicChildren.Add(id);
            foreach (var goal in manager.Goals)
            {
                if (goal.PlayerId != manager.PlayerId || strategicChildren.Contains(goal.GoalId)) continue;
                long id = goal.RequestAuthority?.RequestId ?? -goal.GoalId;
                if (!groups.TryGetValue(id, out var list))
                { list = new List<CommanderGoal>(); groups.Add(id, list); order.Add(id); }
                list.Add(goal);
            }
            var activeCards = new List<CommanderTaskCardSnapshot>();
            var history = new List<CommanderTaskCardSnapshot>();
            for (int i = order.Count - 1; i >= 0; i--)
            {
                var card = GoalCard(manager, groups[order[i]], order[i], generation);
                (card.CanCancel ? activeCards : history).Add(card);
            }
            if (samePlanner)
                for (int i = planner.Plans.Count - 1; i >= 0; i--)
                {
                    var card = StrategicCard(planner.Plans[i], manager.TaskBoardRuntimeToken, generation);
                    (card.CanCancel ? activeCards : history).Add(card);
                }
            int active = activeCards.Count;
            var cards = new List<CommanderTaskCardSnapshot>(Math.Min(MaximumCards, active + history.Count));
            cards.AddRange(activeCards);
            for (int i = 0; i < history.Count && i < TerminalHistory && cards.Count < MaximumCards; i++)
                cards.Add(history[i]);
            return new CommanderTaskBoardSnapshot(cards.ToArray(), active);
        }

        public static bool TryCancel(CommanderGoalManager manager, StrategicPlanner planner,
            CommanderTaskCardSnapshot card, int currentGeneration)
        {
            if (manager == null || manager.IsDisposed || card == null || !card.CanCancel
                || card.Generation != currentGeneration || card.PlayerId != manager.PlayerId
                || card.RuntimeToken != manager.TaskBoardRuntimeToken) return false;
            if (card.StrategicPlanId > 0)
            {
                if (planner == null || planner.PlayerId != manager.PlayerId || !planner.UsesContext(manager.Simulation, manager)
                    || !planner.CaptureControlRequest(manager.PlayerId, card.StrategicPlanId,
                        StrategicPlanControlType.Cancel, out var request)
                    || request.ObservedRevision != card.StrategicRevision) return false;
                return planner.ApplyControl(request).Status == StrategicPlanControlStatus.Applied;
            }
            if (card.RequestId > 0)
            {
                foreach (var goal in manager.ActiveGoals)
                {
                    var scope = goal.RequestAuthority;
                    if (scope == null || scope.RequestId != card.RequestId
                        || scope.PlayerId != manager.PlayerId || !ReferenceEquals(scope.Owner, manager)
                        || !ReferenceEquals(scope.Runtime, manager.Simulation) || scope.Cancelled) continue;
                    return manager.CancelActionPlan(scope);
                }
                return false;
            }
            if (card.RequestId == long.MinValue) return false;
            long rawId = -card.RequestId;
            if (rawId < 1 || rawId > int.MaxValue) return false;
            var direct = manager.GetGoal((int)rawId);
            return direct != null && direct.RequestAuthority == null && direct.PlayerId == manager.PlayerId
                && direct.CreatedTick == card.CreatedTick && manager.CancelGoal((int)rawId);
        }

        private static CommanderTaskCardSnapshot GoalCard(CommanderGoalManager manager,
            List<CommanderGoal> goals, long id, int generation)
        {
            var steps = new CommanderTaskStepSnapshot[goals.Count];
            string current = string.Empty, progress = string.Empty, blocker = string.Empty;
            bool allComplete = true, failed = false, cancelled = false, blocked = false, working = false, live = false;
            for (int i = 0; i < goals.Count; i++)
            {
                var goal = goals[i];
                var status = Map(goal.Status);
                string label = GoalLabel(manager.Simulation, goal);
                string value = GoalProgress(manager.Simulation, goal);
                string reason = status == CommanderTaskStatus.Blocked || status == CommanderTaskStatus.Failed
                    || status == CommanderTaskStatus.Waiting ? goal.StatusReason : string.Empty;
                steps[i] = new CommanderTaskStepSnapshot(goal.GoalId, label, value, reason, status);
                allComplete &= status == CommanderTaskStatus.Completed;
                failed |= status == CommanderTaskStatus.Failed;
                cancelled |= status == CommanderTaskStatus.Cancelled;
                blocked |= status == CommanderTaskStatus.Blocked;
                working |= status == CommanderTaskStatus.Working;
                live |= !goal.IsTerminal;
                if (current.Length == 0 && !goal.IsTerminal)
                { current = label; progress = value; blocker = reason; }
            }
            if (current.Length == 0)
            { var step = steps[steps.Length - 1]; current = step.Label; progress = step.Progress; blocker = step.Blocker; }
            var aggregate = allComplete ? CommanderTaskStatus.Completed : failed ? CommanderTaskStatus.Failed
                : cancelled ? CommanderTaskStatus.Cancelled : blocked ? CommanderTaskStatus.Blocked
                : working ? CommanderTaskStatus.Working : CommanderTaskStatus.Waiting;
            string objective = goals[0].RequestAuthority?.OriginalInput;
            if (string.IsNullOrWhiteSpace(objective)) objective = steps[0].Label;
            return new CommanderTaskCardSnapshot(id, generation, manager.PlayerId, goals[0].CreatedTick,
                0, 0, manager.TaskBoardRuntimeToken, objective, progress, current, blocker, aggregate, live, steps);
        }

        private static CommanderTaskStatus Map(CommanderGoalStatus status) => status switch
        {
            CommanderGoalStatus.Completed => CommanderTaskStatus.Completed,
            CommanderGoalStatus.Failed => CommanderTaskStatus.Failed,
            CommanderGoalStatus.Cancelled => CommanderTaskStatus.Cancelled,
            CommanderGoalStatus.Blocked => CommanderTaskStatus.Blocked,
            CommanderGoalStatus.Planning => CommanderTaskStatus.Working,
            CommanderGoalStatus.Executing => CommanderTaskStatus.Working,
            _ => CommanderTaskStatus.Waiting
        };

        private static string GoalLabel(GameSimulation simulation, CommanderGoal goal)
        {
            if (goal is ReachAgeGoal age) return "Reach Age " + age.TargetAge;
            if (goal is AllocateWorkersGoal workers)
            {
                if (workers.Allocation.ResourceAmount.HasValue)
                    return (workers.Allocation.ResourceAmountMode == CommanderResourceAmountMode.AdditionalGathered
                        ? "Gather " : "Have ") + workers.Allocation.ResourceAmount.Value + " "
                        + workers.Allocation.Destination.Resource;
                return "Assign workers to " + workers.Allocation.Destination.Resource;
            }
            if (goal is BuildStructureGoal build) return "Build " + build.Count + " "
                + CommanderIntentCatalog.GetStructureDisplayName(build.StructureType);
            if (goal is EnsureUnitCountGoal units) return "Produce "
                + (units.IsExplicitNewProduction ? units.RequiredNewProductionCount : units.TargetTotal)
                + (units.IsExplicitNewProduction ? " new " : " total ")
                + CommanderIntentCatalog.GetUnitDisplayName(simulation.ResolveCivUnitType(goal.PlayerId, units.RequestedUnitType));
            if (goal is WatchFutureUnitsGoal future) return "Assign next " + future.Order.Count + " "
                + CommanderIntentCatalog.GetUnitDisplayName(simulation.ResolveCivUnitType(goal.PlayerId, future.Order.UnitType), future.Order.Count != 1)
                + " from " + CommanderIntentCatalog.GetStructureDisplayName(future.Order.ProducerType);
            if (goal is ResourceAllocationGoal allocation) return "Assign " + allocation.TargetWorkers
                + " workers to " + allocation.Resource;
            return goal.GoalType.ToString();
        }

        private static string GoalProgress(GameSimulation simulation, CommanderGoal goal)
        {
            if (goal is ReachAgeGoal age) return "Age " + simulation.GetPlayerAge(goal.PlayerId) + " / " + age.TargetAge;
            if (goal is AllocateWorkersGoal workers)
            {
                if (workers.Allocation.ResourceAmount.HasValue)
                {
                    long amount = workers.Allocation.ResourceAmountMode == CommanderResourceAmountMode.AdditionalGathered
                        ? Math.Max(0L, simulation.ResourceManager.GetGatheredIncome(goal.PlayerId,
                            workers.Allocation.Destination.Resource) - workers.GatheredIncomeAtActivation)
                        : Stockpile(simulation.ResourceManager.GetPlayerResources(goal.PlayerId),
                            workers.Allocation.Destination.Resource);
                    return (workers.Allocation.ResourceAmountMode == CommanderResourceAmountMode.AdditionalGathered
                        ? "Additional gathered " : "Stockpile ") + workers.Allocation.Destination.Resource
                        + ": " + amount + " / " + workers.Allocation.ResourceAmount.Value;
                }
                return "Assigned workers: " + workers.SelectedWorkerIds.Count + " / " + (workers.Allocation.Count?.ToString() ?? "all matching");
            }
            if (goal is BuildStructureGoal build) return "Completed buildings: " + goal.LastObservedOwnedCount
                + " / " + build.TargetTotal;
            if (goal is EnsureUnitCountGoal units) return units.IsExplicitNewProduction
                ? "Attributed new units: " + units.AttributedUnitIds.Count + " / " + units.RequiredNewProductionCount
                : "Owned units: " + goal.LastObservedOwnedCount + " / " + units.TargetTotal;
            if (goal is WatchFutureUnitsGoal future) return "Observed: " + future.ObservedCount
                + " / " + future.Order.Count + "; assigned: " + future.AssignedCount
                + "; interrupted: " + future.InterruptedCount;
            if (goal is ResourceAllocationGoal allocation) return "Workers: " + goal.LastObservedOwnedCount
                + " / " + allocation.TargetWorkers;
            return goal.StatusReason ?? string.Empty;
        }

        private static long Stockpile(PlayerResources resources, ResourceType type) => type switch
        {
            ResourceType.Food => resources.Food, ResourceType.Wood => resources.Wood,
            ResourceType.Gold => resources.Gold, ResourceType.Stone => resources.Stone, _ => 0
        };

        private static CommanderTaskCardSnapshot StrategicCard(StrategicPlan plan, long runtimeToken, int generation)
        {
            var steps = new CommanderTaskStepSnapshot[plan.Milestones.Count];
            int completed = 0;
            for (int i = 0; i < steps.Length; i++)
            {
                var milestone = plan.Milestones[i];
                var status = milestone.Status == StrategicMilestoneStatus.Completed ? CommanderTaskStatus.Completed
                    : milestone.Status == StrategicMilestoneStatus.Failed ? CommanderTaskStatus.Failed
                    : milestone.Status == StrategicMilestoneStatus.Active ? CommanderTaskStatus.Working
                    : CommanderTaskStatus.Waiting;
                if (status == CommanderTaskStatus.Completed) completed++;
                steps[i] = new CommanderTaskStepSnapshot(0, milestone.Name,
                    milestone.CompletedChildGoals.Count + " / " + milestone.RequiredChildGoals.Count + " goals", string.Empty, status);
            }
            var state = plan.Status == StrategicPlanStatus.Completed ? CommanderTaskStatus.Completed
                : plan.Status == StrategicPlanStatus.Failed ? CommanderTaskStatus.Failed
                : plan.Status == StrategicPlanStatus.Cancelled ? CommanderTaskStatus.Cancelled
                : plan.Status == StrategicPlanStatus.Active ? CommanderTaskStatus.Working : CommanderTaskStatus.Waiting;
            return new CommanderTaskCardSnapshot(0, generation, plan.OwnerPlayerId, plan.CreatedTick,
                plan.StrategicPlanId, plan.Revision, runtimeToken, plan.PlanType.ToString(),
                "Milestones: " + completed + " / " + steps.Length, plan.CurrentMilestone?.Name ?? string.Empty,
                plan.OutcomeMessage, state, !plan.IsTerminal, steps);
        }
    }
}
