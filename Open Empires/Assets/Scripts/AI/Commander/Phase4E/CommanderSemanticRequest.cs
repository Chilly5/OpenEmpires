using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public enum CommanderSemanticOutcome
    {
        Request,
        Clarify,
        Unsupported,
        Answer, // Read-only conversation result; never admitted as a gameplay node.
        DynamicPlan
    }

    public enum CommanderSemanticNodeType
    {
        EnsureUnitCount,
        BuildStructure,
        SetResourceAllocation,
        ReachAge,
        MoveUnits,
        ScoutArea,
        PatrolArea,
        SetRallyPoint,
        AttackTarget,
        DefendArea,
        RetreatUnits,
        RepairTarget,
        ResearchTechnology,
        StrategicObjective,
        AllocateWorkers,
        WatchFutureUnits,
        GarrisonUnits,
        UngarrisonBuilding,
        DiscoverEnemy
    }

    // Provider-facing age vocabulary. Numeric game ages remain game-side data.
    public enum CommanderSemanticAgeTarget
    {
        Next,
        Feudal = 2,
        Castle = 3,
        Imperial = 4
    }

    public enum CommanderSemanticAnchorSelector
    {
        MyTownCenter,
        MyBarracks,
        WorkedResource,
        VisibleResource,
        PlayerMarker
    }

    // Near is nondirectional: deterministic placement considers cardinal sides and defaults
    // to one clear tile when no explicit gap was supplied. This enum contains no world position.
    public enum CommanderSemanticPlacementRelation
    {
        MapWest,
        MapEast,
        Near
    }

    public enum CommanderSemanticUnitSelector
    {
        Military,
        Scout,
        Villagers,
        Spearman,
        Archer,
        Knight,
        DamagedMilitary
    }

    public enum CommanderSemanticLocationSelector
    {
        PlayerBase,
        WorkedResource,
        VisibleResource,
        VisibleEnemy,
        RelativeToSelectedUnits,
        PlayerMarker
    }

    // Parsed provider data only. No player, entity, position, provenance, or command authority lives here.
    public sealed class CommanderSemanticNode
    {
        public CommanderSemanticNodeType Type { get; }
        public int? UnitType { get; }
        public BuildingType? BuildingType { get; }
        public ResourceType? ResourceType { get; }
        public int? Count { get; }
        public StrategicObjectiveType? StrategicObjectiveType { get; }
        public CommanderSemanticAgeTarget? AgeTarget { get; }
        public CommanderSemanticAnchorSelector? PlacementAnchorSelector { get; }
        public int? PlacementAnchorOrdinal { get; }
        public CommanderSemanticPlacementRelation? PlacementRelation { get; }
        public int? ClearGapTiles { get; }
        public CommanderSemanticUnitSelector? UnitSelector { get; }
        public bool AllMatchingUnits { get; }
        public CommanderSemanticLocationSelector? LocationSelector { get; }
        public CommanderTargetSelector? TargetSelector { get; }
        public CommanderProductionQuantityMode QuantityMode { get; }
        public TechnologyType? Technology { get; }
        // References are bounded semantic node indices, never game/entity IDs.
        public IReadOnlyList<int> DependsOn { get; }
        public int? ProducerFromNode { get; }
        public int? ResultFromNode { get; }
        public CommanderWorkerAllocation WorkerAllocation { get; }
        public int? ProducerOrdinal { get; }
        public CommanderFutureUnitAction? FutureAction { get; }
        public ResourceSourceKind? SourceKind { get; }
        public IReadOnlyList<CommanderConstraint> Constraints { get; }

        internal CommanderSemanticNode(CommanderSemanticNodeType type, int? unitType = null,
            BuildingType? buildingType = null, ResourceType? resourceType = null,
            int? count = null, StrategicObjectiveType? strategicObjectiveType = null,
            CommanderSemanticAnchorSelector? placementAnchorSelector = null,
            int? placementAnchorOrdinal = null,
            CommanderSemanticPlacementRelation? placementRelation = null,
            int? clearGapTiles = null,
            CommanderSemanticAgeTarget? ageTarget = null,
            CommanderSemanticUnitSelector? unitSelector = null,
            CommanderSemanticLocationSelector? locationSelector = null,
            TechnologyType? technology = null,
            IReadOnlyList<int> dependsOn = null,
            int? producerFromNode = null,
            int? resultFromNode = null,
            CommanderWorkerAllocation workerAllocation = null,
            IReadOnlyList<CommanderConstraint> constraints = null,
            CommanderTargetSelector? targetSelector = null,
            CommanderProductionQuantityMode quantityMode = CommanderProductionQuantityMode.TargetTotal,
            int? producerOrdinal = null,
            CommanderFutureUnitAction? futureAction = null,
            ResourceSourceKind? sourceKind = null,
            bool allMatchingUnits = false)
        {
            Type = type;
            UnitType = unitType;
            BuildingType = buildingType;
            ResourceType = resourceType;
            Count = count;
            StrategicObjectiveType = strategicObjectiveType;
            PlacementAnchorSelector = placementAnchorSelector;
            PlacementAnchorOrdinal = placementAnchorOrdinal;
            PlacementRelation = placementRelation;
            ClearGapTiles = clearGapTiles;
            AgeTarget = ageTarget;
            UnitSelector = unitSelector;
            AllMatchingUnits = allMatchingUnits;
            LocationSelector = locationSelector;
            TargetSelector = targetSelector;
            QuantityMode = quantityMode;
            ProducerOrdinal = producerOrdinal;
            FutureAction = futureAction;
            SourceKind = sourceKind;
            Technology = technology;
            ProducerFromNode = producerFromNode;
            ResultFromNode = resultFromNode;
            WorkerAllocation = workerAllocation;
            Constraints = new List<CommanderConstraint>(constraints ?? Array.Empty<CommanderConstraint>()).AsReadOnly();
            var normalizedDependencies = dependsOn != null
                ? new List<int>(dependsOn)
                : new List<int>();
            if (producerFromNode.HasValue && !normalizedDependencies.Contains(producerFromNode.Value))
                normalizedDependencies.Add(producerFromNode.Value);
            DependsOn = normalizedDependencies.AsReadOnly();
        }
    }

    public sealed partial class CommanderSemanticResult
    {
        // Local slot filling: change only one unresolved symbolic destination.
        // All actors, quantities, constraints and dependency edges stay detached and intact.
        internal CommanderSemanticResult WithMarkerDestination(CommanderSemanticLocationSelector location,
            ResourceType? resource = null)
        {
            if (IsValid && Outcome == CommanderSemanticOutcome.DynamicPlan && DynamicPlan != null
                && location == CommanderSemanticLocationSelector.PlayerBase && !resource.HasValue)
            {
                int count = 0;
                var dynamicNodes = new List<CommanderDynamicNode>();
                foreach (var n in DynamicPlan.Nodes)
                {
                    var parameters = new Dictionary<string, object>();
                    foreach (var pair in n.Parameters) parameters.Add(pair.Key, pair.Value);
                    if (parameters.TryGetValue("anchor", out object anchor) && (string)anchor == "PlayerMarker")
                    { parameters["anchor"] = "MyTownCenter"; count++; }
                    var inputs = new Dictionary<string, string>();
                    foreach (var pair in n.Inputs) inputs.Add(pair.Key, pair.Value);
                    dynamicNodes.Add(new CommanderDynamicNode(n.Id, n.Primitive, parameters, inputs,
                        new List<string>(n.DependsOn)));
                }
                return count == 1 ? new CommanderSemanticResult(true, Outcome, Array.Empty<CommanderSemanticNode>(),
                    SafeExplanation, dynamicPlan: new CommanderDynamicPlan(dynamicNodes, DynamicPlan.Constraints)) : null;
            }
            if (!IsValid || Outcome != CommanderSemanticOutcome.Request) return null;
            int markers = 0;
            foreach (var n in Nodes) if (n.LocationSelector == CommanderSemanticLocationSelector.PlayerMarker
                || n.PlacementAnchorSelector == CommanderSemanticAnchorSelector.PlayerMarker) markers++;
            if (markers != 1) return null;
            var nodes = new List<CommanderSemanticNode>();
            foreach (var n in Nodes)
            {
                if (n.PlacementAnchorSelector == CommanderSemanticAnchorSelector.PlayerMarker)
                {
                    if (location != CommanderSemanticLocationSelector.PlayerBase
                        && (location != CommanderSemanticLocationSelector.WorkedResource || !resource.HasValue)) return null;
                    nodes.Add(new CommanderSemanticNode(n.Type, n.UnitType, n.BuildingType, resource,
                        n.Count, n.StrategicObjectiveType,
                        location == CommanderSemanticLocationSelector.PlayerBase ? CommanderSemanticAnchorSelector.MyTownCenter
                            : CommanderSemanticAnchorSelector.WorkedResource,
                        n.PlacementAnchorOrdinal, n.PlacementRelation, n.ClearGapTiles, n.AgeTarget,
                        n.UnitSelector, n.LocationSelector, n.Technology, n.DependsOn, n.ProducerFromNode,
                        n.ResultFromNode, n.WorkerAllocation, n.Constraints, n.TargetSelector, n.QuantityMode,
                        n.ProducerOrdinal, n.FutureAction,
                        resource == OpenEmpires.ResourceType.Wood ? ResourceSourceKind.Tree
                            : resource == OpenEmpires.ResourceType.Gold ? ResourceSourceKind.GoldMine : n.SourceKind,
                        n.AllMatchingUnits));
                    continue;
                }
                if (n.LocationSelector != CommanderSemanticLocationSelector.PlayerMarker) { nodes.Add(n); continue; }
                nodes.Add(new CommanderSemanticNode(n.Type, n.UnitType, n.BuildingType, resource ?? n.ResourceType,
                    n.Count, n.StrategicObjectiveType, n.PlacementAnchorSelector, n.PlacementAnchorOrdinal,
                    n.PlacementRelation, n.ClearGapTiles, n.AgeTarget, n.UnitSelector, location, n.Technology,
                    n.DependsOn, n.ProducerFromNode, n.ResultFromNode, n.WorkerAllocation, n.Constraints,
                    n.TargetSelector, n.QuantityMode, n.ProducerOrdinal, n.FutureAction, n.SourceKind, n.AllMatchingUnits));
            }
            return new CommanderSemanticResult(true, CommanderSemanticOutcome.Request, nodes.AsReadOnly(), SafeExplanation);
        }
        public bool IsValid { get; }
        public CommanderSemanticOutcome Outcome { get; }
        public IReadOnlyList<CommanderSemanticNode> Nodes { get; }
        public string SafeExplanation { get; }
        public CommanderWorkerAllocationDraft PendingDraft { get; }
        public CommanderRequestGraphDraft PendingGraphDraft { get; }
        public CommanderDynamicPlan DynamicPlan { get; }

        private CommanderSemanticResult(bool isValid, CommanderSemanticOutcome outcome,
            IReadOnlyList<CommanderSemanticNode> nodes, string safeExplanation, CommanderWorkerAllocationDraft pendingDraft = null,
            CommanderDynamicPlan dynamicPlan = null, CommanderRequestGraphDraft pendingGraphDraft = null)
        {
            IsValid = isValid;
            Outcome = outcome;
            Nodes = nodes ?? Array.Empty<CommanderSemanticNode>();
            SafeExplanation = safeExplanation ?? string.Empty;
            PendingDraft = pendingDraft;
            PendingGraphDraft = pendingGraphDraft;
            DynamicPlan = dynamicPlan;
        }
    }
}
