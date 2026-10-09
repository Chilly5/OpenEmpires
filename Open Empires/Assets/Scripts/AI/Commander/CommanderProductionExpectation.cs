using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    // Detached game-owned quote, not provider authority or a duplicate gameplay database.
    internal sealed class CommanderProductionExpectation
    {
        internal int NodeIndex { get; }
        internal int Owned { get; }
        internal int Queued { get; }
        internal int NewCount { get; }
        internal long OtherContribution { get; }
        internal CommanderProductionExpectation(int index, int owned, int queued, int newCount, long otherContribution)
        { NodeIndex = index; Owned = owned; Queued = queued; NewCount = newCount; OtherContribution = otherContribution; }
    }

    internal static class CommanderProductionProjection
    {
        internal static bool TryProject(IReadOnlyList<CommanderSemanticGraphNode> nodes, CommanderContext context,
            out IReadOnlyList<CommanderProductionExpectation> expectations, out string reason)
        {
            expectations = null;
            reason = "The requested exact-new result does not match the production quantity. Nothing started; revise the count or request new units explicitly.";
            var quotes = new CommanderProductionExpectation[nodes.Count];
            var resolvedTypes = new int[nodes.Count];
            var ancestors = new HashSet<int>[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Index != i) return false;
                resolvedTypes[i] = -1;
                ancestors[i] = new HashSet<int>();
                var pending = new Stack<int>(nodes[i].DependsOn);
                while (pending.Count > 0)
                {
                    int dependency = pending.Pop();
                    if (dependency < 0 || dependency >= nodes.Count || dependency == i) return false;
                    if (!ancestors[i].Add(dependency)) continue;
                    foreach (int parent in nodes[dependency].DependsOn) pending.Push(parent);
                }
                if (!(nodes[i].Intent is EnsureUnitCountIntent ensure)) continue;
                resolvedTypes[i] = ensure.UnitType;
                foreach (var option in context.UnitOptions)
                    if (option.IntentUnit == CommanderIntentCatalog.GetUnitDisplayName(ensure.UnitType))
                    { resolvedTypes[i] = option.ResolvedUnitType; break; }
            }
            var visiting = new HashSet<int>();
            bool Quote(int i)
            {
                if (quotes[i] != null || !(nodes[i].Intent is EnsureUnitCountIntent ensure)) return true;
                if (!visiting.Add(i)) return false;
                int owned = 0, queued = 0;
                foreach (var unit in context.Units)
                    if (unit.UnitType == resolvedTypes[i])
                    {
                        if (unit.Count < 0 || unit.QueuedCount < 0) return false;
                        owned = unit.Count; queued = unit.QueuedCount;
                    }
                long otherContribution = (long)owned + queued;
                foreach (int ancestor in ancestors[i])
                    if (resolvedTypes[ancestor] == resolvedTypes[i])
                    {
                        if (!Quote(ancestor)) return false;
                        otherContribution += quotes[ancestor].NewCount;
                    }
                int count = ensure.NewProductionCount ?? (int)Math.Max(0L, (long)ensure.TargetTotal - otherContribution);
                foreach (var consumer in nodes)
                    if (consumer.ResultFromNode == i
                        && (count < 1 || !(consumer.Intent is CapabilityActionIntent action
                            && action.UnitSelector.Count == count
                            || consumer.Intent is AllocateWorkersIntent allocation
                                && allocation.Allocation.Count == count)))
                        return false;
                quotes[i] = new CommanderProductionExpectation(i, owned, queued, count, otherContribution);
                visiting.Remove(i); return true;
            }
            var result = new List<CommanderProductionExpectation>();
            for (int i = 0; i < nodes.Count; i++)
            { if (!Quote(i)) return false; if (quotes[i] != null) result.Add(quotes[i]); }
            // Only effectful parallel production competes with an exact-total
            // promise. An already satisfied sibling is harmless, not a blocker.
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].Intent is EnsureUnitCountIntent total && !total.NewProductionCount.HasValue)
                    foreach (var consumer in nodes)
                        if (consumer.ResultFromNode == i)
                            for (int other = 0; other < nodes.Count; other++)
                                if (other != i && resolvedTypes[other] == resolvedTypes[i] && quotes[other].NewCount > 0
                                    && !ancestors[i].Contains(other) && !ancestors[other].Contains(i))
                                {
                                    reason = "Overlapping total-production steps cannot promise disjoint exact-new results in parallel. Specify their order or clarify the intended new counts; nothing started.";
                                    return false;
                                }
            expectations = result.AsReadOnly(); reason = string.Empty; return true;
        }

        // The same canonical living-owned population includes garrisoned units.
        // Reused by native planning and receipt baseline capture; no balance facts.
        internal static HashSet<int> LivingOwnedIds(GameSimulation simulation, int player, int resolvedType)
        {
            var ids = new HashSet<int>();
            foreach (var unit in simulation.UnitRegistry.GetAllUnits())
                if (unit.PlayerId == player && unit.UnitType == resolvedType && unit.CurrentHealth > 0 && unit.State != UnitState.Dead)
                    ids.Add(unit.Id);
            foreach (var building in simulation.BuildingRegistry.GetAllBuildings())
                if (building.PlayerId == player && !building.IsDestroyed)
                    foreach (int id in building.GarrisonedUnitIds)
                    {
                        var unit = simulation.UnitRegistry.GetGarrisonedUnit(id);
                        if (unit != null && unit.PlayerId == player && unit.UnitType == resolvedType
                            && unit.CurrentHealth > 0 && unit.State != UnitState.Dead) ids.Add(id);
                    }
            return ids;
        }
    }
}
