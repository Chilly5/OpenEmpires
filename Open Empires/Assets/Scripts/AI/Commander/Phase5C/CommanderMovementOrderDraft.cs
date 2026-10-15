using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenEmpires
{
    // Pure, game-owned effect view over an already strictly parsed and compiled order.
    // The source graph remains attached for its exact execution bindings and revalidation.
    internal sealed class CommanderMovementOrderDraft
    {
        internal CommanderSemanticGraphPlan Graph { get; }
        internal CommanderGoalManager Owner { get; }
        internal GameSimulation Runtime { get; }
        internal long RequestId { get; }
        internal int Generation { get; }
        internal int MarkerX { get; }
        internal int MarkerZ { get; }
        internal IReadOnlyDictionary<int, CommanderBoundLocation> MarkerLocations { get; }
        internal int GameplayEffectCount => Graph.Nodes.Count;

        internal CommanderMovementOrderDraft(CommanderSemanticGraphPlan graph,
            CommanderGoalManager owner, long requestId, int generation, int markerX, int markerZ,
            IReadOnlyDictionary<int, CommanderBoundLocation> markerLocations)
        {
            if (!ContainsExistingMarkerMovement(graph) || owner == null || owner.IsDisposed
                || requestId < 1 || generation < 0 || markerLocations == null)
                throw new ArgumentException("The normalized movement order is unavailable.");
            Graph = graph;
            Owner = owner;
            Runtime = owner.Simulation;
            RequestId = requestId;
            Generation = generation;
            MarkerX = markerX;
            MarkerZ = markerZ;
            MarkerLocations = markerLocations;
        }

        internal static bool ContainsExistingMarkerMovement(CommanderSemanticGraphPlan graph)
            => graph?.Nodes != null && graph.Nodes.Any(node =>
                !node.ResultFromNode.HasValue && node.Intent is CapabilityActionIntent action
                && action.ActionType == CommanderCapabilityActionType.MoveUnits
                && action.LocationSelector.Kind == CommanderLocationSelectorKind.PlayerMarker
                && (graph.DynamicProgram == null || graph.DynamicProgram.Nodes.Any(source =>
                    source.Id == node.DynamicNodeId
                    && source.Primitive.Mechanic == CommanderDynamicMechanic.MoveUnits)));

        // This slice requires local player confirmation for a provider-interpreted
        // movement effect, regardless of the provider's Request/DynamicPlan spelling.
        internal bool RequiresPlayerConfirmation => true;

        internal bool MatchesRevision(CommanderSemanticGraphPlan current)
            => ReferenceEquals(Owner.Simulation, Runtime) && !Owner.IsDisposed
                && CommanderScopeEquivalence.SameGraph(Graph, current)
                && Owner.TryGetPlayerMarkerSnapshot(out int x, out int z)
                && x == MarkerX && z == MarkerZ;

        // A comparison of gameplay effects for fixtures and policy checks. The full
        // source graphs are still compared at approval so symbolic bindings survive.
        internal bool SameGameplayEffects(CommanderMovementOrderDraft other)
        {
            if (other == null || GameplayEffectCount != other.GameplayEffectCount
                || MarkerX != other.MarkerX || MarkerZ != other.MarkerZ
                || Graph.ConstructionForbidden != other.Graph.ConstructionForbidden
                || !CommanderScopeEquivalence.SameConstraints(
                    Graph.DynamicProgram?.Constraints ?? Array.Empty<CommanderConstraint>(),
                    other.Graph.DynamicProgram?.Constraints ?? Array.Empty<CommanderConstraint>())
                || Graph.ProductionExpectations.Count != other.Graph.ProductionExpectations.Count) return false;
            for (int i = 0; i < Graph.ProductionExpectations.Count; i++)
                if (Graph.ProductionExpectations[i].NodeIndex != other.Graph.ProductionExpectations[i].NodeIndex
                    || Graph.ProductionExpectations[i].NewCount != other.Graph.ProductionExpectations[i].NewCount)
                    return false;
            for (int i = 0; i < GameplayEffectCount; i++)
            {
                var a = Graph.Nodes[i]; var b = other.Graph.Nodes[i];
                if (a.ProducerFromNode != b.ProducerFromNode || a.ResultFromNode != b.ResultFromNode
                    || a.DependsOn.Count != b.DependsOn.Count
                    || !CommanderScopeEquivalence.SameIntent(a.Intent, b.Intent)) return false;
                for (int d = 0; d < a.DependsOn.Count; d++)
                    if (a.DependsOn[d] != b.DependsOn[d]) return false;
                bool aMarker = MarkerLocations.TryGetValue(i, out var aLocation);
                bool bMarker = other.MarkerLocations.TryGetValue(i, out var bLocation);
                if (aMarker != bMarker || aMarker && (aLocation.ClearGapTiles != bLocation.ClearGapTiles
                    || aLocation.Relation != bLocation.Relation)) return false;
            }
            return true;
        }
    }
}
