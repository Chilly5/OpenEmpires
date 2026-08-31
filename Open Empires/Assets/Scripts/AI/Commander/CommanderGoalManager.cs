using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public sealed class CommanderGoalManager
    {
        private const int PlanningIntervalTicks = 15;
        private readonly GameSimulation simulation;
        private readonly int playerId;
        private readonly CommanderPlanner planner;
        private readonly List<CommanderGoal> goals = new List<CommanderGoal>();
        private int nextGoalId = 1;
        private int lastEvaluatedTick = -1;

        public IReadOnlyList<CommanderGoal> Goals => goals;
        public CommanderGoal ActiveGoal { get; private set; }
        public event Action<CommanderGoal> GoalStatusChanged;

        public CommanderGoalManager(GameSimulation simulation, int playerId)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            this.playerId = playerId;
            planner = new CommanderPlanner(simulation);
        }

        public EnsureUnitCountGoal SubmitEnsureUnitCount(int requestedUnitType, int targetTotal,
            int maxQueueDepth = 3)
        {
            var goal = new EnsureUnitCountGoal(playerId, requestedUnitType, targetTotal, maxQueueDepth)
            {
                GoalId = nextGoalId++,
                CreatedTick = simulation.CurrentTick
            };
            goals.Add(goal);
            if (ActiveGoal == null || ActiveGoal.IsTerminal) ActiveGoal = goal;
            Debug.Log($"[Commander] Goal #{goal.GoalId} submitted: EnsureUnitCount unit={requestedUnitType} target={targetTotal}");
            return goal;
        }

        public bool CancelGoal(int goalId)
        {
            for (int i = 0; i < goals.Count; i++)
            {
                CommanderGoal goal = goals[i];
                if (goal.GoalId != goalId || goal.IsTerminal) continue;
                goal.SetStatus(CommanderGoalStatus.Cancelled, "Cancelled by the owning player.");
                if (ActiveGoal == goal) ActiveGoal = null;
                Debug.Log($"[Commander] Goal #{goal.GoalId} cancelled.");
                GoalStatusChanged?.Invoke(goal);
                return true;
            }
            return false;
        }

        public void Tick(int currentTick)
        {
            if (currentTick == lastEvaluatedTick) return;
            if (lastEvaluatedTick >= 0 && currentTick % PlanningIntervalTicks != 0) return;
            lastEvaluatedTick = currentTick;

            if (ActiveGoal == null || ActiveGoal.IsTerminal)
                ActiveGoal = FindNextActiveGoal();
            if (!(ActiveGoal is EnsureUnitCountGoal ensureGoal)) return;

            CommanderPlan plan = planner.Plan(ensureGoal, currentTick);
            ensureGoal.LastObservedOwnedCount = plan.OwnedCount;
            ensureGoal.LastObservedQueuedCount = plan.QueuedCount;
            bool changed = ensureGoal.SetStatus(plan.Status, plan.Reason);

            if (plan.Command != null && !ensureGoal.IsTerminal)
            {
                simulation.CommandBuffer.EnqueueCommand(plan.Command);
                if (plan.Command is GatherCommand)
                    ensureGoal.LastEconomyCommandTick = currentTick;
            }

            if (changed || plan.Command != null)
            {
                Debug.Log($"[Commander] Goal #{ensureGoal.GoalId}: status={ensureGoal.Status} "
                    + $"owned={plan.OwnedCount} queued={plan.QueuedCount} target={ensureGoal.TargetTotal}; {plan.Reason}");
                GoalStatusChanged?.Invoke(ensureGoal);
            }

            if (ensureGoal.IsTerminal) ActiveGoal = null;
        }

        private CommanderGoal FindNextActiveGoal()
        {
            for (int i = 0; i < goals.Count; i++)
                if (!goals[i].IsTerminal) return goals[i];
            return null;
        }
    }
}
