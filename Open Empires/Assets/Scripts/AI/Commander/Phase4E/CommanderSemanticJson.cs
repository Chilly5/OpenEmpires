using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    public static class CommanderSemanticJson
    {
        public const int MaximumResponseCharacters = 8192;
        public const int MaximumNodes = 4;
        public const int MinimumTownCenterOrdinal = 1;
        public const int MaximumTownCenterOrdinal = 8;
        public const int MinimumClearGapTiles = 1;
        public const int MaximumClearGapTiles = 20;
        public const int MaximumDependencyReferences = 8;
        public const int MaximumDependencyDepth = 4;

        public static CommanderSemanticResult Parse(string raw) => CommanderSemanticResult.ParseTrusted(raw);
        public static CommanderSemanticResult ParseProviderResponse(string raw)
            => CommanderSemanticResult.ParseTrusted(raw, requireProviderDeclarations: true);
    }

    // Keep result construction and validity assignment inside the parser's own class.
    // Other runtime code can request parsing, but cannot mark arbitrary data valid.
    public sealed partial class CommanderSemanticResult
    {
        private const string InvalidExplanation = "I couldn't understand that request safely.";

        internal static CommanderSemanticResult ParseTrusted(string raw, bool requireProviderDeclarations = false)
        {
            string failureStage = "syntax";
            int failureNode = -1;
            try
            {
                if (string.IsNullOrWhiteSpace(raw) || raw.Length > CommanderDynamicPlan.MaximumCharacters)
                    throw new JsonException();
                CheckStrictSyntax(raw);
                failureStage = "envelope";

                JObject root;
                using (var reader = new JsonTextReader(new StringReader(raw))
                    { MaxDepth = 12, DateParseHandling = DateParseHandling.None })
                {
                    root = JObject.Load(reader, new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                        CommentHandling = CommentHandling.Load
                    });
                    if (reader.Read()) throw new JsonException();
                }

                string outcomeName = RequiredString(root, "outcome");
                if (outcomeName == "DynamicPlan")
                {
                    if (!ParseDynamicTrusted(raw, out var dynamicPlan)) throw new JsonException();
                    return new CommanderSemanticResult(true, CommanderSemanticOutcome.DynamicPlan,
                        Array.Empty<CommanderSemanticNode>(), "Action plan requires validation and player confirmation.",
                        dynamicPlan: dynamicPlan);
                }
                if (raw.Length > CommanderSemanticJson.MaximumResponseCharacters) throw new JsonException();
                // Preserve the legacy boundary even though the common envelope loader
                // must allow the separately bounded DynamicPlan's deeper typed data.
                using (var legacyDepth = new JsonTextReader(new StringReader(raw)) { MaxDepth = 8 })
                    while (legacyDepth.Read()) { }
                CommanderSemanticOutcome outcome;
                switch (outcomeName)
                {
                    case "Request": outcome = CommanderSemanticOutcome.Request; break;
                    case "Clarify": outcome = CommanderSemanticOutcome.Clarify; break;
                    case "Unsupported": outcome = CommanderSemanticOutcome.Unsupported; break;
                    case "Answer": outcome = CommanderSemanticOutcome.Answer; break;
                    default: throw new JsonException();
                }

                if (outcome != CommanderSemanticOutcome.Request)
                {
                    if (outcome == CommanderSemanticOutcome.Clarify && root.Property("pending") != null)
                    {
                        CheckFields(root, "outcome", "message", "pending", "missingFields");
                        return new CommanderSemanticResult(true, outcome, Array.Empty<CommanderSemanticNode>(),
                            OptionalExplanation(root), ParsePendingDraft(root));
                    }
                    CheckFields(root, "outcome", "message");
                    string message = OptionalExplanation(root);
                    return new CommanderSemanticResult(true, outcome,
                        Array.Empty<CommanderSemanticNode>(), message);
                }

                if (requireProviderDeclarations) CheckFields(root, "outcome", "nodes", "executionOrder");
                else CheckFields(root, "outcome", "nodes");
                if (!(root["nodes"] is JArray items) || items.Count < 1 || items.Count > CommanderSemanticJson.MaximumNodes)
                    throw new JsonException();
                string executionOrder = requireProviderDeclarations
                    ? ReadProviderExecutionOrder(root, items.Count) : null;
                var nodes = new List<CommanderSemanticNode>(items.Count);
                foreach (JToken item in items)
                {
                    if (!(item is JObject node)) throw new JsonException();
                    failureStage = "node"; failureNode = nodes.Count;
                    nodes.Add(ParseNode(node, requireProviderDeclarations));
                }
                failureStage = "graph";
                ValidateGraph(nodes);
                if (requireProviderDeclarations) ValidateProviderExecutionOrder(root, items, nodes, executionOrder);
                return new CommanderSemanticResult(true, outcome, nodes.AsReadOnly(), string.Empty);
            }
            catch (Exception error) when (error is JsonException || error is FormatException
                || error is OverflowException || error is ArgumentException)
            {
                // The provider's raw text and fields are not safe diagnostics for the player.
                return new CommanderSemanticResult(false, CommanderSemanticOutcome.Unsupported,
                    Array.Empty<CommanderSemanticNode>(), InvalidExplanation)
                { SchemaDiagnostic = error is SchemaFieldFailure field ? field.Detail
                    : "field=$;code=invalid-schema;stage=" + failureStage + ";node=" + failureNode };
            }
        }

        private static CommanderSemanticNode ParseNode(JObject node, bool requireProviderDeclarations)
        {
            IReadOnlyList<int> dependsOn = ParseDependencies(node);
            switch (RequiredString(node, "type"))
            {
                case "EnsureUnitCount":
                    CheckFields(node, "type", "unit", "count", "dependsOn", "producerFromNode", "constraints", "quantityMode");
                    if (!CommanderIntentCatalog.TryResolveUnit(RequiredString(node, "unit"), out int unit)
                        || !CommanderIntentCatalog.IsSupportedUnit(unit)) throw new JsonException();
                    var quantityMode = CommanderProductionQuantityMode.TargetTotal;
                    if (node.Property("quantityMode") != null)
                    {
                        string quantity = RequiredString(node, "quantityMode");
                        if (quantity == "New") quantityMode = CommanderProductionQuantityMode.New;
                        else if (quantity != "TargetTotal") throw new JsonException();
                    }
                    return new CommanderSemanticNode(CommanderSemanticNodeType.EnsureUnitCount,
                        unitType: unit, count: RequiredCount(node, 0, 200), dependsOn: dependsOn,
                        producerFromNode: ParseOptionalNodeIndex(node, "producerFromNode"), constraints: ParseConstraints(node), quantityMode: quantityMode);

                case "BuildStructure":
                    if (requireProviderDeclarations)
                        CheckFields(node, "type", "structure", "count", "placement", "dependsOn", "constraints", "builders");
                    else CheckFields(node, "type", "structure", "count", "placement", "dependsOn", "constraints");
                    CommanderSemanticAnchorSelector? anchor = null;
                    int? ordinal = null;
                    CommanderSemanticPlacementRelation? relation = null;
                    int? clearGapTiles = null;
                    ResourceType? placementResource = null;
                    if (node.Property("placement") != null)
                    {
                        if (!(node["placement"] is JObject placement))
                            throw FailField(node, "placement", SchemaFailureCode.MissingOrWrongType);
                        CheckFields(placement, "anchor", "ordinal", "relation", "clearGapTiles", "resource", "sourceKind");
                        switch (RequiredString(placement, "anchor"))
                        {
                            case "MyTownCenter": anchor = CommanderSemanticAnchorSelector.MyTownCenter; break;
                            case "MyBarracks": anchor = CommanderSemanticAnchorSelector.MyBarracks; break;
                            case "WorkedResource": anchor = CommanderSemanticAnchorSelector.WorkedResource; break;
                            case "VisibleResource": anchor = CommanderSemanticAnchorSelector.VisibleResource; break;
                            default: throw FailField(placement, "anchor", SchemaFailureCode.InvalidEnum);
                        }
                        if (placement.Property("ordinal") != null)
                        {
                            if (anchor != CommanderSemanticAnchorSelector.MyTownCenter)
                                throw FailField(placement, "ordinal", SchemaFailureCode.IncompatibleDeclaration);
                            ordinal = RequiredBoundedInteger(placement, "ordinal",
                                CommanderSemanticJson.MinimumTownCenterOrdinal,
                                CommanderSemanticJson.MaximumTownCenterOrdinal);
                        }
                        switch (RequiredString(placement, "relation"))
                        {
                            case "MapWest": relation = CommanderSemanticPlacementRelation.MapWest; break;
                            case "MapEast": relation = CommanderSemanticPlacementRelation.MapEast; break;
                            case "Near": relation = CommanderSemanticPlacementRelation.Near; break;
                            default: throw FailField(placement, "relation", SchemaFailureCode.InvalidEnum);
                        }
                        // Omission is intentional: the deterministic resolver owns its relation-specific default.
                        if (placement.Property("clearGapTiles") != null)
                        {
                            clearGapTiles = RequiredBoundedInteger(placement, "clearGapTiles",
                                CommanderSemanticJson.MinimumClearGapTiles,
                                CommanderSemanticJson.MaximumClearGapTiles);
                            if (relation == CommanderSemanticPlacementRelation.Near && clearGapTiles != 1)
                                throw FailField(placement, "clearGapTiles", SchemaFailureCode.IncompatibleDeclaration);
                        }
                        if (anchor == CommanderSemanticAnchorSelector.WorkedResource
                            || anchor == CommanderSemanticAnchorSelector.VisibleResource)
                        {
                            if (placement.Property("resource") == null)
                                throw FailField(placement, "resource", SchemaFailureCode.MissingOrWrongType);
                            if (relation != CommanderSemanticPlacementRelation.Near)
                                throw FailField(placement, "relation", SchemaFailureCode.IncompatibleDeclaration);
                            placementResource = ReadResourceField(placement, "resource");
                        }
                        else if (placement.Property("resource") != null || placement.Property("sourceKind") != null) throw new JsonException();
                    }
                    ResourceSourceKind? placementSource = null;
                    if (node["placement"] is JObject resourcePlacement && resourcePlacement.Property("sourceKind") != null)
                    {
                        placementSource = RequiredSemanticEnum<ResourceSourceKind>(resourcePlacement, "sourceKind");
                        if (!placementResource.HasValue || !ResourceSourceRules.IsCompatible(placementResource.Value, placementSource.Value))
                            throw FailField(resourcePlacement, "sourceKind", SchemaFailureCode.IncompatibleDeclaration);
                    }
                    return new CommanderSemanticNode(CommanderSemanticNodeType.BuildStructure,
                        buildingType: ParseBuilding(RequiredString(node, "structure")),
                        count: RequiredCount(node, 1, 20),
                        placementAnchorSelector: anchor,
                        placementAnchorOrdinal: ordinal,
                        placementRelation: relation,
                        clearGapTiles: clearGapTiles,
                        resourceType: placementResource,
                        dependsOn: dependsOn, constraints: ParseBuildConstraints(node, requireProviderDeclarations), sourceKind: placementSource);

                case "SetResourceAllocation":
                    CheckFields(node, "type", "resource", "count", "dependsOn");
                    return new CommanderSemanticNode(CommanderSemanticNodeType.SetResourceAllocation,
                        resourceType: ParseResource(RequiredString(node, "resource")),
                        count: RequiredCount(node, 0, 200), dependsOn: dependsOn);

                case "AllocateWorkers":
                    return new CommanderSemanticNode(CommanderSemanticNodeType.AllocateWorkers,
                        workerAllocation: ParseWorkerAllocation(node), dependsOn: dependsOn,
                        resultFromNode: ParseOptionalNodeIndex(node, "resultFromNode"));

                case "WatchFutureUnits":
                    CheckFields(node, "type", "unit", "count", "producer", "producerOrdinal",
                        "action", "resource", "sourceKind", "location", "dependsOn");
                    if (!CommanderIntentCatalog.TryResolveUnit(RequiredString(node, "unit"), out int watchedUnit)
                        || !CommanderIntentCatalog.IsSupportedUnit(watchedUnit)) throw new JsonException();
                    BuildingType watchedProducer = ParseBuilding(RequiredString(node, "producer"));
                    string actionName = RequiredString(node, "action");
                    CommanderFutureUnitAction watchedAction;
                    if (actionName == "Gather") watchedAction = CommanderFutureUnitAction.Gather;
                    else if (actionName == "Patrol") watchedAction = CommanderFutureUnitAction.Patrol;
                    else throw new JsonException();
                    if (watchedAction == CommanderFutureUnitAction.Patrol
                        && (RequiredString(node, "location") != "WorkedResource"
                            || node.Property("sourceKind") != null)) throw new JsonException();
                    if (watchedAction == CommanderFutureUnitAction.Gather && node.Property("location") != null)
                        throw new JsonException();
                    ResourceType watchedResource = ParseResource(RequiredString(node, "resource"));
                    if (watchedAction == CommanderFutureUnitAction.Gather && watchedUnit != 0)
                        throw new JsonException();
                    ResourceSourceKind? watchedSource = node.Property("sourceKind") == null
                        ? (ResourceSourceKind?)null : RequiredSemanticEnum<ResourceSourceKind>(node, "sourceKind");
                    int? watchedOrdinal = node.Property("producerOrdinal") == null ? (int?)null
                        : RequiredBoundedInteger(node, "producerOrdinal", 1, 8);
                    if (!CanProduce(watchedProducer, watchedUnit)) throw new JsonException();
                    return new CommanderSemanticNode(CommanderSemanticNodeType.WatchFutureUnits,
                        unitType: watchedUnit, buildingType: watchedProducer, count: RequiredCount(node, 1, 50),
                        resourceType: watchedResource, dependsOn: dependsOn,
                        producerOrdinal: watchedOrdinal, futureAction: watchedAction, sourceKind: watchedSource);

                case "ReachAge":
                    CheckFields(node, "type", "targetAge", "dependsOn", "constraints");
                    return new CommanderSemanticNode(CommanderSemanticNodeType.ReachAge,
                        ageTarget: ReadAgeTarget(node),
                        dependsOn: dependsOn, constraints: ParseConstraints(node));

                case "MoveUnits":
                case "ScoutArea":
                case "PatrolArea":
                case "SetRallyPoint":
                case "AttackTarget":
                case "DefendArea":
                case "RetreatUnits":
                case "RepairTarget":
                    return ParseCapabilityNode(node, RequiredString(node, "type"), dependsOn);

                case "ResearchTechnology":
                    CheckFields(node, "type", "technology", "dependsOn");
                    return new CommanderSemanticNode(CommanderSemanticNodeType.ResearchTechnology,
                        technology: ParseTechnology(RequiredString(node, "technology")),
                        unitSelector: CommanderSemanticUnitSelector.Military,
                        locationSelector: CommanderSemanticLocationSelector.PlayerBase,
                        dependsOn: dependsOn);

                case "StrategicObjective":
                    CheckFields(node, "type", "objective", "dependsOn");
                    return new CommanderSemanticNode(CommanderSemanticNodeType.StrategicObjective,
                        strategicObjectiveType: ParseObjective(RequiredString(node, "objective")), dependsOn: dependsOn);

                default: throw new JsonException();
            }
        }

        private static CommanderSemanticNode ParseCapabilityNode(JObject node, string type,
            IReadOnlyList<int> dependsOn)
        {
            CheckFields(node, "type", "unitSelector", "count", "location", "resource", "structure", "dependsOn", "resultFromNode", "target");
            CommanderSemanticUnitSelector unit = ParseUnitSelector(RequiredString(node, "unitSelector"));
            int count = RequiredCount(node, 1, 50);
            CommanderSemanticLocationSelector location = ParseLocationSelector(RequiredString(node, "location"));
            ResourceType? resource = null;
            if (node.Property("resource") != null)
                resource = ParseResource(RequiredString(node, "resource"));
            if ((location == CommanderSemanticLocationSelector.WorkedResource
                || location == CommanderSemanticLocationSelector.VisibleResource) && !resource.HasValue)
                throw new JsonException();
            BuildingType? structure = null;
            if (node.Property("structure") != null)
            {
                structure = ParseBuilding(RequiredString(node, "structure"));
                if (type != "SetRallyPoint") throw new JsonException();
            }
            CommanderSemanticNodeType action;
            if (!Enum.TryParse(type, false, out action)) throw new JsonException();
            CommanderTargetSelector? target = node.Property("target") == null ? (CommanderTargetSelector?)null
                : ParseTargetSelector(node["target"]);
            if (target.HasValue && !target.Value.IsCompatible(
                (CommanderCapabilityActionType)Enum.Parse(typeof(CommanderCapabilityActionType), type),
                (CommanderLocationSelectorKind)Enum.Parse(typeof(CommanderLocationSelectorKind), location.ToString())))
                throw new JsonException();
            int? resultFromNode = ParseOptionalNodeIndex(node, "resultFromNode");
            return new CommanderSemanticNode(action, count: count, resourceType: resource,
                buildingType: structure, unitSelector: unit, locationSelector: location,
                dependsOn: dependsOn, resultFromNode: resultFromNode, targetSelector: target);
        }

        // Shared strict target vocabulary for both semantic requests and legacy DTOs.
        internal static CommanderTargetSelector ParseTargetSelector(JToken token)
        {
            if (!(token is JObject target)) throw new JsonException();
            string kind = RequiredString(target, "kind");
            if (kind == "UnitType")
            {
                CheckFields(target, "kind", "unit");
                string unit = RequiredString(target, "unit");
                if (!CommanderIntentCatalog.TryResolveUnit(unit, out int type)
                    || !CommanderIntentCatalog.IsSupportedUnit(type)) throw new JsonException();
                return new CommanderTargetSelector(type);
            }
            if (kind == "BuildingType")
            {
                CheckFields(target, "kind", "structure");
                string name = RequiredString(target, "structure");
                var building = CommanderContentNameResolver.ResolveBuilding(name,
                    GameKnowledgeCatalog.BuildCanonicalIdentityCatalog(), Civilization.English);
                if (building.Status != CommanderContentResolutionStatus.Resolved || building.Match.Landmark.HasValue) throw new JsonException();
                return new CommanderTargetSelector(building.Match.BuildingType);
            }
            throw new JsonException();
        }

        internal static JObject TargetSelectorJson(CommanderTargetSelector selector)
            => selector.Kind == CommanderTargetSelectorKind.UnitType
                ? new JObject { ["kind"] = "UnitType", ["unit"] = CommanderIntentCatalog.GetUnitDisplayName(selector.UnitType) }
                : new JObject { ["kind"] = "BuildingType", ["structure"] = selector.StructureType.Value.ToString() };

        private static CommanderSemanticUnitSelector ParseUnitSelector(string name)
        {
            switch (name)
            {
                case "Military": return CommanderSemanticUnitSelector.Military;
                case "Scout": return CommanderSemanticUnitSelector.Scout;
                case "Villagers": return CommanderSemanticUnitSelector.Villagers;
                case "Spearman": return CommanderSemanticUnitSelector.Spearman;
                case "Archer": return CommanderSemanticUnitSelector.Archer;
                case "Knight": return CommanderSemanticUnitSelector.Knight;
                case "DamagedMilitary": return CommanderSemanticUnitSelector.DamagedMilitary;
                default: throw new JsonException();
            }
        }

        private static CommanderSemanticLocationSelector ParseLocationSelector(string name)
        {
            switch (name)
            {
                case "PlayerBase": return CommanderSemanticLocationSelector.PlayerBase;
                case "WorkedResource": return CommanderSemanticLocationSelector.WorkedResource;
                case "VisibleResource": return CommanderSemanticLocationSelector.VisibleResource;
                case "VisibleEnemy": return CommanderSemanticLocationSelector.VisibleEnemy;
                case "RelativeToSelectedUnits": return CommanderSemanticLocationSelector.RelativeToSelectedUnits;
                default: throw new JsonException();
            }
        }

        private static IReadOnlyList<int> ParseDependencies(JObject node)
        {
            JProperty property = node.Property("dependsOn");
            if (property == null) return Array.Empty<int>();
            if (!(property.Value is JArray values) || values.Count > CommanderSemanticJson.MaximumDependencyReferences)
                throw new JsonException();
            var result = new List<int>(values.Count);
            foreach (JToken value in values)
            {
                if (value.Type != JTokenType.Integer) throw new JsonException();
                int index = value.Value<int>();
                if (index < 0 || result.Contains(index)) throw new JsonException();
                result.Add(index);
            }
            return result.AsReadOnly();
        }

        private static int? ParseOptionalNodeIndex(JObject node, string name)
        {
            JToken token = node[name];
            if (token == null) return null;
            if (token.Type != JTokenType.Integer) throw new JsonException();
            int index = token.Value<int>();
            if (index < 0) throw new JsonException();
            return index;
        }

        private static void ValidateGraph(IReadOnlyList<CommanderSemanticNode> nodes)
        {
            int totalReferences = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                CommanderSemanticNode node = nodes[i];
                totalReferences += node.DependsOn.Count;
                if (totalReferences > CommanderSemanticJson.MaximumDependencyReferences) throw new JsonException();
                for (int d = 0; d < node.DependsOn.Count; d++)
                {
                    int dependency = node.DependsOn[d];
                    if (dependency >= nodes.Count || dependency == i) throw new JsonException();
                }
                if (node.ProducerFromNode.HasValue)
                {
                    int producer = node.ProducerFromNode.Value;
                    if (node.Type != CommanderSemanticNodeType.EnsureUnitCount || producer >= nodes.Count
                        || producer == i || nodes[producer].Type != CommanderSemanticNodeType.BuildStructure)
                        throw new JsonException();
                    BuildingType producerType = nodes[producer].BuildingType.Value;
                    if (nodes[producer].Count < 1
                        || nodes[producer].Count > CommanderIntentValidator.MaximumStructureCount
                        || !CanProduce(producerType, node.UnitType.Value))
                        throw new JsonException();
                }
                if (node.ResultFromNode.HasValue)
                {
                    int resultSource = node.ResultFromNode.Value;
                    bool linkedResult = false;
                    for (int d = 0; d < node.DependsOn.Count; d++)
                        if (node.DependsOn[d] == resultSource) { linkedResult = true; break; }
                    if (resultSource >= nodes.Count || resultSource == i || !linkedResult)
                        throw new JsonException();
                    CommanderSemanticNode source = nodes[resultSource];
                    bool compatible = false;
                    if (source.Type == CommanderSemanticNodeType.EnsureUnitCount)
                    {
                        if (!source.UnitType.HasValue) throw new JsonException();
                        compatible = node.Type == CommanderSemanticNodeType.AllocateWorkers
                            ? source.UnitType.Value == 0 && source.QuantityMode == CommanderProductionQuantityMode.New
                                && node.WorkerAllocation != null
                                && node.WorkerAllocation.CountMode == CommanderWorkerCountMode.Exact
                                && node.WorkerAllocation.Count == source.Count
                            : node.UnitSelector.HasValue && CanBindUnitResult(node.UnitSelector.Value, source.UnitType.Value);
                    }
                    else if (source.Type == CommanderSemanticNodeType.BuildStructure)
                    {
                        compatible = node.Type == CommanderSemanticNodeType.SetRallyPoint
                            && (!node.BuildingType.HasValue
                                || node.BuildingType.Value == source.BuildingType.Value);
                    }
                    if (!compatible) throw new JsonException();
                }
            }
            for (int i = 0; i < nodes.Count; i++)
                if (DependencyDepth(nodes, i, new bool[nodes.Count]) > CommanderSemanticJson.MaximumDependencyDepth)
                    throw new JsonException();
        }

        private static int DependencyDepth(IReadOnlyList<CommanderSemanticNode> nodes, int index, bool[] visiting)
        {
            if (visiting[index]) throw new JsonException();
            visiting[index] = true;
            int depth = 1;
            for (int i = 0; i < nodes[index].DependsOn.Count; i++)
                depth = Math.Max(depth, 1 + DependencyDepth(nodes, nodes[index].DependsOn[i], visiting));
            visiting[index] = false;
            return depth;
        }

        private static bool CanBindUnitResult(CommanderSemanticUnitSelector selector, int unitType)
        {
            switch (selector)
            {
                case CommanderSemanticUnitSelector.Spearman: return unitType == 1 || unitType == 12;
                case CommanderSemanticUnitSelector.Archer: return unitType == 2 || unitType == 10;
                case CommanderSemanticUnitSelector.Knight: return unitType == 7;
                case CommanderSemanticUnitSelector.Scout: return unitType == 4;
                case CommanderSemanticUnitSelector.Villagers: return unitType == 0;
                case CommanderSemanticUnitSelector.Military: return CommanderIntentCatalog.IsSupportedUnit(unitType) && unitType != 0 && unitType != 4;
                default: return false;
            }
        }

        private static bool CanProduce(BuildingType producer, int unitType)
        {
            UnitKnowledge unit = GameKnowledgeCatalog.BuildCanonicalIdentityCatalog().FindUnitByType(unitType);
            return unit != null && CommanderIntentCatalog.IsSupportedUnit(unitType)
                && (BuildingType)unit.ProductionBuildingType == producer;
        }


        private static BuildingType ParseBuilding(string name)
        {
            // One trusted adapter catalog, not a second parser-only content whitelist.
            // Exact enum spelling rejects numeric/default-enum strings; discovery alone
            // still cannot make an unsupported construction mechanic executable.
            if (CommanderIntentCatalog.TryResolveStructure(name, out BuildingType structure)
                && CommanderIntentCatalog.IsSupportedStructure(structure)) return structure;
            throw new JsonException();
        }

        private static ResourceType ParseResource(string name)
        {
            switch (name)
            {
                case "Food": return ResourceType.Food;
                case "Wood": return ResourceType.Wood;
                case "Gold": return ResourceType.Gold;
                case "Stone": return ResourceType.Stone;
                default: throw new JsonException();
            }
        }

        private static StrategicObjectiveType ParseObjective(string name)
        {
            switch (name)
            {
                case "AttackPreparation": return StrategicObjectiveType.AttackPreparation;
                case "DefensivePreparation": return StrategicObjectiveType.DefensivePreparation;
                case "EconomicExpansion": return StrategicObjectiveType.EconomicExpansion;
                case "MilitaryReinforcement": return StrategicObjectiveType.MilitaryReinforcement;
                case "RangedReinforcement": return StrategicObjectiveType.RangedReinforcement;
                case "DefensiveTurtle": return StrategicObjectiveType.DefensiveTurtle;
                default: throw new JsonException();
            }
        }

        private static CommanderSemanticAgeTarget ParseAgeTarget(string name)
        {
            switch (name)
            {
                case "Next": return CommanderSemanticAgeTarget.Next;
                case "2":
                case "Feudal": return CommanderSemanticAgeTarget.Feudal;
                case "3":
                case "Castle": return CommanderSemanticAgeTarget.Castle;
                case "4":
                case "Imperial": return CommanderSemanticAgeTarget.Imperial;
                default: throw new JsonException();
            }
        }

        private static CommanderSemanticAgeTarget ReadAgeTarget(JObject node)
        {
            if (node["targetAge"]?.Type == JTokenType.Integer)
                return (CommanderSemanticAgeTarget)RequiredBoundedInteger(node, "targetAge", 2, 4);
            string name = RequiredString(node, "targetAge");
            try { return ParseAgeTarget(name); }
            catch (JsonException) { throw FailField(node, "targetAge", SchemaFailureCode.InvalidEnum); }
        }

        private static TechnologyType ParseTechnology(string name)
        {
            TechnologyType technology;
            if (!Enum.TryParse(name, false, out technology)
                || !Enum.IsDefined(typeof(TechnologyType), technology)) throw new JsonException();
            return technology;
        }

        private static void CheckFields(JObject value, params string[] allowed)
        {
            foreach (JProperty property in value.Properties())
            {
                bool found = false;
                foreach (string name in allowed)
                    if (property.Name == name) { found = true; break; }
                if (!found) throw FailField(value, property.Name, SchemaFailureCode.UnexpectedField);
            }
        }

        private static string RequiredString(JObject value, string name)
        {
            JToken token = value[name];
            if (token == null || token.Type != JTokenType.String)
                throw FailField(value, name, SchemaFailureCode.MissingOrWrongType);
            return (string)token;
        }

        private static int RequiredCount(JObject value, int minimum, int maximum)
        {
            return RequiredBoundedInteger(value, "count", minimum, maximum);
        }

        private static int RequiredBoundedInteger(JObject value, string name, int minimum, int maximum)
        {
            JToken token = value[name];
            if (token == null || token.Type != JTokenType.Integer)
                throw FailField(value, name, SchemaFailureCode.MissingOrWrongType);
            int number;
            try { number = token.Value<int>(); }
            catch (OverflowException) { throw FailField(value, name, SchemaFailureCode.OutOfRange); }
            if (number < minimum || number > maximum)
                throw FailField(value, name, SchemaFailureCode.OutOfRange);
            return number;
        }

        private static string OptionalExplanation(JObject value)
        {
            JToken token = value["message"];
            if (token == null) return string.Empty;
            if (token.Type != JTokenType.String) throw new JsonException();
            string message = (string)token;
            if (message.Length > 180) throw new JsonException();
            foreach (char c in message)
                if (char.IsControl(c) || c == '<' || c == '>') throw new JsonException();
            return message;
        }

        // Json.NET accepts JavaScript extensions such as comments, single quotes, NaN and
        // trailing commas. Restrict lexical input before its structural/depth validation.
        private static void CheckStrictSyntax(string json)
        {
            char previous = '\0';
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"')
                {
                    bool closed = false;
                    bool pendingHighSurrogate = false;
                    while (++i < json.Length)
                    {
                        c = json[i];
                        if (c == '"')
                        {
                            if (pendingHighSurrogate) throw new JsonException();
                            closed = true;
                            break;
                        }
                        if (c < 32) throw new JsonException();
                        if (c == '\\')
                        {
                            if (++i >= json.Length || "\"\\/bfnrtu".IndexOf(json[i]) < 0)
                                throw new JsonException();
                            c = json[i];
                            if (json[i] == 'u')
                            {
                                int codeUnit = 0;
                                for (int d = 0; d < 4; d++)
                                {
                                    if (++i >= json.Length || !Uri.IsHexDigit(json[i])) throw new JsonException();
                                    char hex = json[i];
                                    codeUnit = codeUnit * 16 + (hex <= '9' ? hex - '0'
                                        : char.ToUpperInvariant(hex) - 'A' + 10);
                                }
                                c = (char)codeUnit;
                            }
                        }
                        // Validate decoded UTF-16 before Json.NET can replace malformed
                        // escaped surrogates with a replacement character.
                        if (pendingHighSurrogate)
                        {
                            if (!char.IsLowSurrogate(c)) throw new JsonException();
                            pendingHighSurrogate = false;
                        }
                        else if (char.IsHighSurrogate(c)) pendingHighSurrogate = true;
                        else if (char.IsLowSurrogate(c)) throw new JsonException();
                    }
                    if (!closed) throw new JsonException();
                    previous = '"';
                }
                else if (c == ' ' || c == '\r' || c == '\n' || c == '\t') continue;
                else if (c == '{' || c == '[' || c == ':' || c == ',') previous = c;
                else if (c == '}' || c == ']')
                {
                    if (previous == ',') throw new JsonException();
                    previous = c;
                }
                else if (c == '-' || (c >= '0' && c <= '9'))
                {
                    if (c == '-')
                    {
                        if (++i >= json.Length || json[i] < '0' || json[i] > '9') throw new JsonException();
                    }
                    if (json[i] == '0' && i + 1 < json.Length && json[i + 1] >= '0' && json[i + 1] <= '9')
                        throw new JsonException();
                    while (i + 1 < json.Length && json[i + 1] >= '0' && json[i + 1] <= '9') i++;
                    previous = '0';
                }
                else throw new JsonException();
            }
        }
    }
}
