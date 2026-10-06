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
    }

    // Keep result construction and validity assignment inside the parser's own class.
    // Other runtime code can request parsing, but cannot mark arbitrary data valid.
    public sealed partial class CommanderSemanticResult
    {
        private const string InvalidExplanation = "I couldn't understand that request safely.";

        internal static CommanderSemanticResult ParseTrusted(string raw)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(raw) || raw.Length > CommanderSemanticJson.MaximumResponseCharacters)
                    throw new JsonException();
                CheckStrictSyntax(raw);

                JObject root;
                using (var reader = new JsonTextReader(new StringReader(raw))
                    { MaxDepth = 8, DateParseHandling = DateParseHandling.None })
                {
                    root = JObject.Load(reader, new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                        CommentHandling = CommentHandling.Load
                    });
                    if (reader.Read()) throw new JsonException();
                }

                string outcomeName = RequiredString(root, "outcome");
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

                CheckFields(root, "outcome", "nodes");
                if (!(root["nodes"] is JArray items) || items.Count < 1 || items.Count > CommanderSemanticJson.MaximumNodes)
                    throw new JsonException();
                var nodes = new List<CommanderSemanticNode>(items.Count);
                foreach (JToken item in items)
                {
                    if (!(item is JObject node)) throw new JsonException();
                    nodes.Add(ParseNode(node));
                }
                ValidateGraph(nodes);
                return new CommanderSemanticResult(true, outcome, nodes.AsReadOnly(), string.Empty);
            }
            catch (Exception error) when (error is JsonException || error is FormatException
                || error is OverflowException || error is ArgumentException)
            {
                // The provider's raw text and fields are not safe diagnostics for the player.
                return new CommanderSemanticResult(false, CommanderSemanticOutcome.Unsupported,
                    Array.Empty<CommanderSemanticNode>(), InvalidExplanation);
            }
        }

        private static CommanderSemanticNode ParseNode(JObject node)
        {
            IReadOnlyList<int> dependsOn = ParseDependencies(node);
            switch (RequiredString(node, "type"))
            {
                case "EnsureUnitCount":
                    CheckFields(node, "type", "unit", "count", "dependsOn", "producerFromNode");
                    int unit;
                    switch (RequiredString(node, "unit"))
                    {
                        case "Villager": unit = 0; break;
                        case "Spearman": unit = 1; break;
                        case "Archer": unit = 2; break;
                        case "Scout": unit = 4; break;
                        case "Knight": unit = 7; break;
                        default: throw new JsonException();
                    }
                    return new CommanderSemanticNode(CommanderSemanticNodeType.EnsureUnitCount,
                        unitType: unit, count: RequiredCount(node, 0, 200), dependsOn: dependsOn,
                        producerFromNode: ParseOptionalNodeIndex(node, "producerFromNode"));

                case "BuildStructure":
                    CheckFields(node, "type", "structure", "count", "placement", "dependsOn");
                    CommanderSemanticAnchorSelector? anchor = null;
                    int? ordinal = null;
                    CommanderSemanticPlacementRelation? relation = null;
                    int? clearGapTiles = null;
                    ResourceType? placementResource = null;
                    if (node.Property("placement") != null)
                    {
                        if (!(node["placement"] is JObject placement)) throw new JsonException();
                        CheckFields(placement, "anchor", "ordinal", "relation", "clearGapTiles", "resource");
                        switch (RequiredString(placement, "anchor"))
                        {
                            case "MyTownCenter": anchor = CommanderSemanticAnchorSelector.MyTownCenter; break;
                            case "MyBarracks": anchor = CommanderSemanticAnchorSelector.MyBarracks; break;
                            case "WorkedResource": anchor = CommanderSemanticAnchorSelector.WorkedResource; break;
                            default: throw new JsonException();
                        }
                        if (placement.Property("ordinal") != null)
                        {
                            if (anchor != CommanderSemanticAnchorSelector.MyTownCenter) throw new JsonException();
                            ordinal = RequiredBoundedInteger(placement, "ordinal",
                                CommanderSemanticJson.MinimumTownCenterOrdinal,
                                CommanderSemanticJson.MaximumTownCenterOrdinal);
                        }
                        switch (RequiredString(placement, "relation"))
                        {
                            case "MapWest": relation = CommanderSemanticPlacementRelation.MapWest; break;
                            case "MapEast": relation = CommanderSemanticPlacementRelation.MapEast; break;
                            case "Near": relation = CommanderSemanticPlacementRelation.Near; break;
                            default: throw new JsonException();
                        }
                        // Omission is intentional: the deterministic resolver owns its relation-specific default.
                        if (placement.Property("clearGapTiles") != null)
                        {
                            clearGapTiles = RequiredBoundedInteger(placement, "clearGapTiles",
                                CommanderSemanticJson.MinimumClearGapTiles,
                                CommanderSemanticJson.MaximumClearGapTiles);
                            if (relation == CommanderSemanticPlacementRelation.Near && clearGapTiles != 1)
                                throw new JsonException();
                        }
                        if (anchor == CommanderSemanticAnchorSelector.WorkedResource)
                        {
                            if (placement.Property("resource") == null || relation != CommanderSemanticPlacementRelation.Near)
                                throw new JsonException();
                            placementResource = ParseResource(RequiredString(placement, "resource"));
                        }
                        else if (placement.Property("resource") != null) throw new JsonException();
                    }
                    return new CommanderSemanticNode(CommanderSemanticNodeType.BuildStructure,
                        buildingType: ParseBuilding(RequiredString(node, "structure")),
                        count: RequiredCount(node, 1, 20),
                        placementAnchorSelector: anchor,
                        placementAnchorOrdinal: ordinal,
                        placementRelation: relation,
                        clearGapTiles: clearGapTiles,
                        resourceType: placementResource,
                        dependsOn: dependsOn);

                case "SetResourceAllocation":
                    CheckFields(node, "type", "resource", "count", "dependsOn");
                    return new CommanderSemanticNode(CommanderSemanticNodeType.SetResourceAllocation,
                        resourceType: ParseResource(RequiredString(node, "resource")),
                        count: RequiredCount(node, 0, 200), dependsOn: dependsOn);

                case "AllocateWorkers":
                    return new CommanderSemanticNode(CommanderSemanticNodeType.AllocateWorkers,
                        workerAllocation: ParseWorkerAllocation(node), dependsOn: dependsOn);

                case "ReachAge":
                    CheckFields(node, "type", "targetAge", "dependsOn");
                    return new CommanderSemanticNode(CommanderSemanticNodeType.ReachAge,
                        ageTarget: ParseAgeTarget(RequiredString(node, "targetAge")),
                        dependsOn: dependsOn);

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
            CheckFields(node, "type", "unitSelector", "count", "location", "resource", "structure", "dependsOn", "resultFromNode");
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
            int? resultFromNode = ParseOptionalNodeIndex(node, "resultFromNode");
            return new CommanderSemanticNode(action, count: count, resourceType: resource,
                buildingType: structure, unitSelector: unit, locationSelector: location,
                dependsOn: dependsOn, resultFromNode: resultFromNode);
        }

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
                    if (nodes[producer].Count != 1 || !CanProduce(producerType, node.UnitType.Value))
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
                        if (!source.UnitType.HasValue || !node.UnitSelector.HasValue)
                            throw new JsonException();
                        compatible = CanBindUnitResult(node.UnitSelector.Value, source.UnitType.Value);
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
                case CommanderSemanticUnitSelector.Spearman: return unitType == 1;
                case CommanderSemanticUnitSelector.Archer: return unitType == 2;
                case CommanderSemanticUnitSelector.Knight: return unitType == 7;
                case CommanderSemanticUnitSelector.Scout: return unitType == 4;
                case CommanderSemanticUnitSelector.Villagers: return unitType == 0;
                case CommanderSemanticUnitSelector.Military: return unitType != 0 && unitType != 4;
                default: return false;
            }
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


        private static BuildingType ParseBuilding(string name)
        {
            // Exact currently supported structure names only. Context-specific legality still
            // belongs to admission, but unsupported structure types fail at the JSON boundary.
            switch (name)
            {
                case "House": return BuildingType.House;
                case "Barracks": return BuildingType.Barracks;
                case "ArcheryRange": return BuildingType.ArcheryRange;
                case "Stables": return BuildingType.Stables;
                case "Tower": return BuildingType.Tower;
                case "TownCenter": return BuildingType.TownCenter;
                case "Mill": return BuildingType.Mill;
                default: throw new JsonException();
            }
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
                case "Feudal": return CommanderSemanticAgeTarget.Feudal;
                case "Castle": return CommanderSemanticAgeTarget.Castle;
                case "Imperial": return CommanderSemanticAgeTarget.Imperial;
                default: throw new JsonException();
            }
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
                if (!found) throw new JsonException();
            }
        }

        private static string RequiredString(JObject value, string name)
        {
            JToken token = value[name];
            if (token == null || token.Type != JTokenType.String) throw new JsonException();
            return (string)token;
        }

        private static int RequiredCount(JObject value, int minimum, int maximum)
        {
            return RequiredBoundedInteger(value, "count", minimum, maximum);
        }

        private static int RequiredBoundedInteger(JObject value, string name, int minimum, int maximum)
        {
            JToken token = value[name];
            if (token == null || token.Type != JTokenType.Integer) throw new JsonException();
            int number = token.Value<int>();
            if (number < minimum || number > maximum) throw new JsonException();
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
