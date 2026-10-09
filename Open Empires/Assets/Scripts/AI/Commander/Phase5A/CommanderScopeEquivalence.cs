using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    // Explicit semantic-scope version. Display/localization cannot authorize anything.
    // Every new intent/selector/constraint field must be added here with mutation tests.
    // Runtime/owner/request/generation/revision/single-use affinity stays on the candidate.
    internal static class CommanderScopeEquivalence
    {
        internal const int Version = 6;

        internal static bool SameIntent(CommanderIntent a, CommanderIntent b)
        {
            if (a == null || b == null || a.GetType() != b.GetType() || a.Type != b.Type
                || a.PlayerId != b.PlayerId || a.IntentLayer != b.IntentLayer
                || !SameConstraints(a.Constraints, b.Constraints)) return false;
            if (a is EnsureUnitCountIntent u && b is EnsureUnitCountIntent v)
                return u.UnitType == v.UnitType && u.TargetTotal == v.TargetTotal && u.NewProductionCount == v.NewProductionCount;
            if (a is BuildStructureIntent x && b is BuildStructureIntent y)
                return x.StructureType == y.StructureType && x.Count == y.Count
                    && x.PlacementAnchorSelector == y.PlacementAnchorSelector && x.PlacementAnchorOrdinal == y.PlacementAnchorOrdinal
                    && x.PlacementRelation == y.PlacementRelation && x.ClearGapTiles == y.ClearGapTiles
                    && x.PlacementResourceType == y.PlacementResourceType;
            if (a is SetResourceAllocationIntent r && b is SetResourceAllocationIntent s)
                return r.Resource == s.Resource && r.Mode == s.Mode && r.WorkerCount == s.WorkerCount;
            if (a is ReachAgeIntent age && b is ReachAgeIntent next)
                return age.RequestedTarget == next.RequestedTarget && age.TargetAge == next.TargetAge;
            if (a is AllocateWorkersIntent w && b is AllocateWorkersIntent z) return SameAllocation(w.Allocation, z.Allocation);
            if (a is WatchFutureUnitsIntent f && b is WatchFutureUnitsIntent g)
                return f.UnitType == g.UnitType && f.Count == g.Count
                    && f.ProducerType == g.ProducerType && f.ProducerOrdinal == g.ProducerOrdinal
                    && f.Action == g.Action && f.Resource == g.Resource && f.SourceKind == g.SourceKind;
            if (a is CapabilityActionIntent c && b is CapabilityActionIntent d)
                return c.ActionType == d.ActionType && c.Technology == d.Technology && c.StructureType == d.StructureType
                    && c.UnitSelector.Kind == d.UnitSelector.Kind && c.UnitSelector.UnitType == d.UnitSelector.UnitType
                    && c.UnitSelector.Count == d.UnitSelector.Count && c.LocationSelector.Kind == d.LocationSelector.Kind
                    && c.LocationSelector.ResourceType == d.LocationSelector.ResourceType
                    && c.LocationSelector.RadiusTiles == d.LocationSelector.RadiusTiles
                    && c.TargetSelector.HasValue == d.TargetSelector.HasValue
                    && (!c.TargetSelector.HasValue || c.TargetSelector.Value.Kind == d.TargetSelector.Value.Kind
                        && c.TargetSelector.Value.UnitType == d.TargetSelector.Value.UnitType
                        && c.TargetSelector.Value.StructureType == d.TargetSelector.Value.StructureType);
            return false; // Unknown runtime type fails closed, even when enum/display happens to match.
        }

        private static bool SameAllocation(CommanderWorkerAllocation a, CommanderWorkerAllocation b)
            => a != null && b != null && a.Workers != null && b.Workers != null
                && a.Destination != null && b.Destination != null && a.Mode == b.Mode && a.CountMode == b.CountMode
                && a.Count == b.Count && a.ResourceAmount == b.ResourceAmount && a.ResourceAmountMode == b.ResourceAmountMode
                && a.Workers.State == b.Workers.State && a.Workers.CurrentResource == b.Workers.CurrentResource
                && a.Destination.Resource == b.Destination.Resource && a.Destination.SourceKind == b.Destination.SourceKind;

        private static bool SameConstraints(IReadOnlyList<CommanderConstraint> a, IReadOnlyList<CommanderConstraint> b)
        {
            if (a == null || b == null || a.Count != b.Count || a.Count > 4) return false;
            var seenA = new HashSet<CommanderConstraintType>(); var seenB = new HashSet<CommanderConstraintType>();
            foreach (var item in b)
                if (item == null || !seenB.Add(item.Type) || !SameConstraint(item, item)) return false;
            foreach (var item in a)
            {
                if (item == null || !seenA.Add(item.Type)) return false;
                CommanderConstraint match = null;
                foreach (var candidate in b) if (candidate.Type == item.Type) { match = candidate; break; }
                if (!SameConstraint(item, match)) return false;
            }
            return true;
        }

        private static bool SameConstraint(CommanderConstraint a, CommanderConstraint b)
        {
            if (a == null || b == null || a.GetType() != b.GetType() || a.Type != b.Type) return false;
            if (a is NoConstructionConstraint && b is NoConstructionConstraint) return true;
            if (a is ProtectedResourceConstraint p && b is ProtectedResourceConstraint q)
                return p.Resource == q.Resource && p.MinimumWorkers == q.MinimumWorkers;
            if (a is PreferredWorkersConstraint w && b is PreferredWorkersConstraint x) return w.WorkerSource == x.WorkerSource;
            if (a is MaximumQueueConstraint m && b is MaximumQueueConstraint n) return m.MaximumQueue == n.MaximumQueue;
            if (a is ResourceSourceConstraint r && b is ResourceSourceConstraint s) return r.Resource == s.Resource && r.SourceKind == s.SourceKind;
            return false;
        }

        internal static bool SameGraph(CommanderSemanticGraphPlan a, CommanderSemanticGraphPlan b)
        {
            if (a == null || b == null || a.ConstructionForbidden != b.ConstructionForbidden
                || a.Nodes.Count != b.Nodes.Count || !SameSequence(a.TopologicalOrder, b.TopologicalOrder)
                || a.ProductionExpectations.Count != b.ProductionExpectations.Count
                || !SameDynamic(a.DynamicProgram, b.DynamicProgram)) return false;
            for (int i = 0; i < a.Nodes.Count; i++)
            {
                var x = a.Nodes[i]; var y = b.Nodes[i];
                if (x == null || y == null || x.Index != y.Index || x.ProducerFromNode != y.ProducerFromNode
                    || x.ResultFromNode != y.ResultFromNode || x.DynamicNodeId != y.DynamicNodeId
                    || !SameSequence(x.DependsOn, y.DependsOn) || !SameIntent(x.Intent, y.Intent)) return false;
            }
            for (int i = 0; i < a.ProductionExpectations.Count; i++)
                if (a.ProductionExpectations[i].NodeIndex != b.ProductionExpectations[i].NodeIndex
                    || a.ProductionExpectations[i].NewCount != b.ProductionExpectations[i].NewCount) return false;
            return true;
        }

        private static bool SameDynamic(CommanderDynamicPlan a, CommanderDynamicPlan b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (a.Nodes.Count != b.Nodes.Count || !SameConstraints(a.Constraints, b.Constraints)) return false;
            for (int i = 0; i < a.Nodes.Count; i++)
            {
                var x = a.Nodes[i]; var y = b.Nodes[i];
                if (x == null || y == null || x.Primitive == null || y.Primitive == null
                    || !ReferenceEquals(x.Primitive, CommanderDynamicPrimitiveRegistry.Find(x.Primitive.Id))
                    || !ReferenceEquals(y.Primitive, CommanderDynamicPrimitiveRegistry.Find(y.Primitive.Id))
                    || x.Id != y.Id || x.Primitive.Id != y.Primitive.Id || !SameSequence(x.DependsOn, y.DependsOn)
                    || x.Parameters.Count != y.Parameters.Count || x.Inputs.Count != y.Inputs.Count) return false;
                foreach (var parameter in x.Parameters)
                    if (!y.Parameters.TryGetValue(parameter.Key, out object value)
                        || !(parameter.Value is int || parameter.Value is string) || value == null
                        || parameter.Value.GetType() != value.GetType() || !parameter.Value.Equals(value)) return false;
                foreach (var input in x.Inputs)
                    if (!y.Inputs.TryGetValue(input.Key, out string value) || input.Value != value) return false;
            }
            return true;
        }

        private static bool SameSequence<T>(IReadOnlyList<T> a, IReadOnlyList<T> b)
        {
            if (a == null || b == null || a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (!EqualityComparer<T>.Default.Equals(a[i], b[i])) return false;
            return true;
        }
    }
}
