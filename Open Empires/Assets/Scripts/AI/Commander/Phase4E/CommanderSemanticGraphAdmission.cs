using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    // A game-side, preflight-only representation of a compound semantic request.
    // Node references are indices in the bounded provider graph; they are never entity IDs.
    public sealed class CommanderSemanticGraphNode
    {
        public int Index { get; }
        public CommanderIntent Intent { get; }
        public IReadOnlyList<int> DependsOn { get; }
        public int? ProducerFromNode { get; }
        public int? ResultFromNode { get; }

        internal CommanderSemanticGraphNode(int index, CommanderIntent intent,
            IReadOnlyList<int> dependsOn, int? producerFromNode, int? resultFromNode)
        {
            Index = index;
            Intent = intent;
            DependsOn = dependsOn ?? Array.Empty<int>();
            ProducerFromNode = producerFromNode;
            ResultFromNode = resultFromNode;
        }
    }

    public sealed class CommanderSemanticGraphPlan
    {
        public IReadOnlyList<CommanderSemanticGraphNode> Nodes { get; }
        public IReadOnlyList<int> TopologicalOrder { get; }

        internal CommanderSemanticGraphPlan(IReadOnlyList<CommanderSemanticGraphNode> nodes,
            IReadOnlyList<int> topologicalOrder)
        {
            Nodes = nodes ?? Array.Empty<CommanderSemanticGraphNode>();
            TopologicalOrder = topologicalOrder ?? Array.Empty<int>();
        }
    }

    // Converts a parsed provider graph into trusted tactical intents without touching goals,
    // simulations, entities, workers, tiles, or commands. All graph validation is atomic:
    // failure returns no partially admitted plan.
    public static class CommanderSemanticGraphAdmission
    {
        public const int MaximumNodes = CommanderSemanticJson.MaximumNodes;
        private const string RejectedReason = "The compound semantic order was rejected by Commander validation.";

        public static bool TryAdmit(CommanderSemanticResult result, CommanderContext context,
            out CommanderSemanticGraphPlan plan, out string safeReason)
        {
            plan = null;
            safeReason = RejectedReason;
            if (result == null || context == null || !result.IsValid
                || result.Outcome != CommanderSemanticOutcome.Request
                || result.Nodes == null || result.Nodes.Count < 1
                || result.Nodes.Count > MaximumNodes || context.PlayerId < 0)
                return false;

            var admitted = new List<CommanderSemanticGraphNode>(result.Nodes.Count);
            int totalReferences = 0;
            for (int index = 0; index < result.Nodes.Count; index++)
            {
                CommanderSemanticNode node = result.Nodes[index];
                // Strategic objectives require their existing approval/authority path and
                // must never be smuggled through a tactical compound request.
                if (node == null || node.Type == CommanderSemanticNodeType.StrategicObjective)
                    return false;
                if (!CommanderSemanticAdmission.TryCreateTacticalIntent(node, context,
                    out CommanderIntent intent, out _))
                    return false;
                if (intent == null || intent.PlayerId != context.PlayerId
                    || intent.IntentLayer != CommanderIntentLayer.Tactical)
                    return false;

                var dependencies = new List<int>(node.DependsOn ?? Array.Empty<int>());
                if (!ValidateReferences(node, index, result.Nodes.Count, dependencies)) return false;
                totalReferences += dependencies.Count;
                if (totalReferences > CommanderSemanticJson.MaximumDependencyReferences) return false;
                admitted.Add(new CommanderSemanticGraphNode(index, intent,
                    dependencies.AsReadOnly(), node.ProducerFromNode, node.ResultFromNode));
            }

            // The parser already performs this check, but admission repeats it at the
            // mutation boundary so callers cannot construct or deserialize an unsafe result
            // in a future code path and bypass graph invariants.
            if (!ValidateProducerLinks(result.Nodes)) return false;
            if (!ValidateResultLinks(admitted)) return false;
            if (!TryTopologicalOrder(admitted, out var order)) return false;

            plan = new CommanderSemanticGraphPlan(admitted.AsReadOnly(), order);
            safeReason = string.Empty;
            return true;
        }

        private static bool ValidateReferences(CommanderSemanticNode node, int index,
            int nodeCount, IReadOnlyList<int> dependencies)
        {
            if (dependencies == null || dependencies.Count > CommanderSemanticJson.MaximumDependencyReferences)
                return false;
            for (int i = 0; i < dependencies.Count; i++)
            {
                int dependency = dependencies[i];
                if (dependency < 0 || dependency >= nodeCount || dependency == index) return false;
                for (int prior = 0; prior < i; prior++)
                    if (dependencies[prior] == dependency) return false;
            }
            if (node.ProducerFromNode.HasValue)
            {
                bool linked = false;
                for (int i = 0; i < dependencies.Count; i++)
                    if (dependencies[i] == node.ProducerFromNode.Value) { linked = true; break; }
                if (!linked) return false;
            }
            if (node.ResultFromNode.HasValue)
            {
                int resultSource = node.ResultFromNode.Value;
                if (resultSource < 0 || resultSource >= nodeCount || resultSource == index)
                    return false;
                bool linked = false;
                for (int i = 0; i < dependencies.Count; i++)
                    if (dependencies[i] == resultSource) { linked = true; break; }
                if (!linked) return false;
            }
            return true;
        }

        private static bool ValidateResultLinks(IReadOnlyList<CommanderSemanticGraphNode> admitted)
        {
            for (int i = 0; i < admitted.Count; i++)
            {
                CommanderSemanticGraphNode node = admitted[i];
                if (!node.ResultFromNode.HasValue) continue;
                int sourceIndex = node.ResultFromNode.Value;
                if (sourceIndex < 0 || sourceIndex >= admitted.Count || sourceIndex == i)
                    return false;
                CommanderIntent sourceIntent = admitted[sourceIndex].Intent;
                if (!(node.Intent is CapabilityActionIntent action)) return false;

                if (sourceIntent is EnsureUnitCountIntent ensure)
                {
                    if (!CanBindUnits(action, ensure.UnitType)) return false;
                }
                else if (sourceIntent is BuildStructureIntent build)
                {
                    if (action.ActionType != CommanderCapabilityActionType.SetRallyPoint)
                        return false;
                    if (action.StructureType.HasValue && action.StructureType.Value != build.StructureType)
                        return false;
                }
                else return false;
            }
            return true;
        }

        private static bool CanBindUnits(CapabilityActionIntent action, int unitType)
        {
            switch (action.UnitSelector.Kind)
            {
                case CommanderUnitSelectorKind.UnitType:
                    return action.UnitSelector.UnitType == unitType;
                case CommanderUnitSelectorKind.Scout:
                    return unitType == 4;
                case CommanderUnitSelectorKind.Villagers:
                    return unitType == 0;
                case CommanderUnitSelectorKind.Military:
                    return unitType != 0 && unitType != 4;
                default:
                    return false;
            }
        }

        private static bool ValidateProducerLinks(IReadOnlyList<CommanderSemanticNode> nodes)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                CommanderSemanticNode node = nodes[i];
                if (!node.ProducerFromNode.HasValue) continue;
                int producerIndex = node.ProducerFromNode.Value;
                if (node.Type != CommanderSemanticNodeType.EnsureUnitCount
                    || producerIndex < 0 || producerIndex >= nodes.Count || producerIndex == i)
                    return false;
                CommanderSemanticNode producer = nodes[producerIndex];
                if (producer.Type != CommanderSemanticNodeType.BuildStructure
                    || !producer.BuildingType.HasValue || producer.Count != 1
                    || !node.UnitType.HasValue || !CanProduce(producer.BuildingType.Value, node.UnitType.Value))
                    return false;
            }
            return true;
        }

        private static bool TryTopologicalOrder(IReadOnlyList<CommanderSemanticGraphNode> nodes,
            out IReadOnlyList<int> order)
        {
            var result = new List<int>(nodes.Count);
            var visiting = new bool[nodes.Count];
            var visited = new bool[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
                if (!Visit(i, nodes, visiting, visited, result))
                {
                    order = null;
                    return false;
                }
            order = result.AsReadOnly();
            return true;
        }

        private static bool Visit(int index, IReadOnlyList<CommanderSemanticGraphNode> nodes,
            bool[] visiting, bool[] visited, List<int> order)
        {
            if (visited[index]) return true;
            if (visiting[index]) return false;
            visiting[index] = true;
            var dependencies = nodes[index].DependsOn;
            // Original node order is retained for equal-ready nodes by visiting dependencies
            // in provider order; this makes graph submission deterministic.
            for (int i = 0; i < dependencies.Count; i++)
                if (!Visit(dependencies[i], nodes, visiting, visited, order)) return false;
            visiting[index] = false;
            visited[index] = true;
            order.Add(index);
            return true;
        }

        private static bool CanProduce(BuildingType producer, int unitType)
        {
            switch (unitType)
            {
                case 0: return producer == BuildingType.TownCenter;
                case 1: return producer == BuildingType.Barracks;
                case 2: return producer == BuildingType.ArcheryRange;
                case 4: return producer == BuildingType.Stables;
                case 7: return producer == BuildingType.Stables;
                default: return false;
            }
        }
    }
}
