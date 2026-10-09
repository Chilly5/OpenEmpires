using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenEmpires
{
    public sealed partial class CommanderGoalManager
    {
        internal int CancelFutureSubscriptions()
        {
            ThrowIfDisposed();
            int[] ids = activeGoals.OfType<WatchFutureUnitsGoal>()
                .Where(goal => !goal.IsTerminal).Select(goal => goal.GoalId).ToArray();
            foreach (int id in ids) CancelGoal(id);
            return ids.Length;
        }

        private bool TryBindFutureProducers(CommanderSemanticGraphPlan plan,
            out IReadOnlyDictionary<int, BuildingData> bindings, out string reason)
        {
            var selected = new Dictionary<int, BuildingData>();
            reason = string.Empty;
            foreach (var node in plan.Nodes)
            {
                if (!(node.Intent is WatchFutureUnitsIntent future)) continue;
                var choices = simulation.BuildingRegistry.GetAllBuildings()
                    .Where(b => b != null && b.PlayerId == playerId && !b.IsDestroyed
                        && !b.IsUnderConstruction
                        && simulation.GetEffectiveBuildingType(b) == future.ProducerType
                        && simulation.IsCompatibleProductionBuilding(playerId, b, future.UnitType))
                    .OrderBy(b => b.Id).ToArray();
                if (choices.Length == 0 || future.ProducerOrdinal.HasValue
                    && future.ProducerOrdinal.Value > choices.Length)
                {
                    bindings = null;
                    reason = "The requested owned producer is unavailable; no different building will substitute.";
                    return false;
                }
                if (!future.ProducerOrdinal.HasValue && choices.Length != 1)
                {
                    bindings = null;
                    reason = "Several owned producers match. Please specify which producer number (ordered by game identity).";
                    return false;
                }
                selected.Add(node.Index, choices[(future.ProducerOrdinal ?? 1) - 1]);
            }
            bindings = selected;
            return true;
        }

        internal bool AreFutureProducerBindingsCurrent(CommanderSemanticGraphPlan plan,
            IReadOnlyDictionary<int, BuildingData> bindings)
        {
            if (plan == null || bindings == null) return false;
            foreach (var node in plan.Nodes)
            {
                if (!(node.Intent is WatchFutureUnitsIntent future)) continue;
                if (!bindings.TryGetValue(node.Index, out BuildingData bound) || bound == null
                    || bound.PlayerId != playerId || bound.IsDestroyed || bound.IsUnderConstruction
                    || !ReferenceEquals(simulation.BuildingRegistry.GetBuilding(bound.Id), bound)
                    || simulation.GetEffectiveBuildingType(bound) != future.ProducerType
                    || !simulation.IsCompatibleProductionBuilding(playerId, bound, future.UnitType)) return false;
                // The number is a selector, not permission to silently switch producer
                // while the approval preview is open.
                var choices = simulation.BuildingRegistry.GetAllBuildings()
                    .Where(b => b != null && b.PlayerId == playerId && !b.IsDestroyed
                        && !b.IsUnderConstruction
                        && simulation.GetEffectiveBuildingType(b) == future.ProducerType
                        && simulation.IsCompatibleProductionBuilding(playerId, b, future.UnitType))
                    .OrderBy(b => b.Id).ToArray();
                if (!future.ProducerOrdinal.HasValue && choices.Length != 1
                    || future.ProducerOrdinal.HasValue && (choices.Length < future.ProducerOrdinal.Value
                        || !ReferenceEquals(choices[future.ProducerOrdinal.Value - 1], bound))) return false;
            }
            return true;
        }

        private void HandleFutureBirth(ProducerBirthObservation birth)
        {
            if (disposed || birth == null || !ReferenceEquals(birth.Runtime, simulation)
                || birth.PlayerId != playerId) return;
            foreach (var active in activeGoals)
            {
                if (!(active is WatchFutureUnitsGoal goal) || goal.IsTerminal
                    || goal.ObservedCount >= goal.Order.Count
                    || birth.Tick < goal.CreatedTick || !ReferenceEquals(birth.Producer, goal.BoundProducer)
                    || birth.ProducerId != goal.ProducerId
                    || birth.Producer.IsDestroyed || birth.Producer.PlayerId != playerId
                    || birth.UnitId < 0 || birth.BirthOrdinal <= goal.LastSeenBirthOrdinal) continue;
                UnitData unit = simulation.UnitRegistry.GetUnit(birth.UnitId);
                if (unit == null || unit.PlayerId != playerId || unit.UnitType != birth.UnitType) continue;
                goal.LastSeenBirthOrdinal = birth.BirthOrdinal;
                if (birth.UnitType != simulation.ResolveCivUnitType(playerId, goal.Order.UnitType)
                    || !AreDependenciesReady(goal, out _, out _)
                    || !goal.seenUnitIds.Add(birth.UnitId)) continue;
                goal.observedUnitIds.Add(birth.UnitId);
                GoalStatusChanged?.Invoke(goal);
                PublishEvent(CommanderGoalEventType.GoalProgressChanged, goal, birth.Tick);
            }
        }

        private void ObserveFutureHumanOrder(int[] subjects)
        {
            foreach (var active in activeGoals)
            {
                if (!(active is WatchFutureUnitsGoal goal) || goal.IsTerminal) continue;
                foreach (int id in subjects)
                    if (goal.seenUnitIds.Contains(id))
                    {
                        goal.interruptedUnitIds.Add(id);
                        SuppressFuturePendingUnit(goal, id);
                    }
            }
        }

        private void HandleFutureCommandProcessed(ICommand processed)
        {
            if (disposed || processed == null) return;
            foreach (var active in activeGoals)
            {
                if (!(active is WatchFutureUnitsGoal goal) || goal.IsTerminal) continue;
                foreach (var pair in goal.pendingCommands.ToArray())
                {
                    if (!ReferenceEquals(pair.Value, processed)) continue;
                    goal.pendingCommands.Remove(pair.Key);
                    if (goal.interruptedUnitIds.Contains(pair.Key)) continue;
                    UnitData unit = simulation.UnitRegistry.GetUnit(pair.Key);
                    if (unit == null || unit.PlayerId != playerId || unit.CurrentHealth <= 0
                        || unit.State == UnitState.Dead)
                    {
                        goal.rejectedUnitIds.Add(pair.Key);
                        continue;
                    }
                    bool accepted = false;
                    if (processed is GatherCommand && unit.TargetResourceNodeId >= 0)
                    {
                        ResourceNodeData node = simulation.MapData.GetResourceNode(unit.TargetResourceNodeId);
                        accepted = node != null && node.Type == goal.Order.Resource
                            && ResourceSourceRules.Matches(node, goal.Order.SourceKind);
                    }
                    else if (processed is PatrolCommand) accepted = unit.IsPatrolling;
                    else if (processed is SlaughterSheepCommand slaughter)
                        accepted = unit.State == UnitState.MovingToSlaughter
                            && unit.CombatTargetId == slaughter.SheepUnitId;
                    if (!accepted)
                    {
                        goal.rejectedUnitIds.Add(pair.Key);
                        continue;
                    }
                    goal.assignedUnitIds.Add(pair.Key);
                    GoalStatusChanged?.Invoke(goal);
                    PublishEvent(CommanderGoalEventType.GoalProgressChanged, goal, simulation.CurrentTick);
                }
            }
        }

        private void SuppressFuturePendingUnit(WatchFutureUnitsGoal goal, int unitId)
        {
            if (!goal.pendingCommands.TryGetValue(unitId, out ICommand pending)) return;
            goal.pendingCommands.Remove(unitId);
            if (!simulation.CommandBuffer.RemovePendingExact(pending))
                simulation.SuppressUnprocessedLocalAction(pending);
        }

        private void SuppressFuturePendingActions(WatchFutureUnitsGoal goal)
        {
            foreach (int id in goal.pendingCommands.Keys.ToArray())
                SuppressFuturePendingUnit(goal, id);
        }
    }
}
