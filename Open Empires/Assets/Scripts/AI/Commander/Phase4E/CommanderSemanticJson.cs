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
        private const string InvalidExplanation = "I couldn't understand that request safely.";

        public static CommanderSemanticResult Parse(string raw)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(raw) || raw.Length > MaximumResponseCharacters)
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
                    default: throw new JsonException();
                }

                if (outcome != CommanderSemanticOutcome.Request)
                {
                    CheckFields(root, "outcome", "message");
                    string message = OptionalExplanation(root);
                    return new CommanderSemanticResult(true, outcome,
                        Array.Empty<CommanderSemanticNode>(), message);
                }

                CheckFields(root, "outcome", "nodes");
                if (!(root["nodes"] is JArray items) || items.Count < 1 || items.Count > MaximumNodes)
                    throw new JsonException();
                var nodes = new List<CommanderSemanticNode>(items.Count);
                foreach (JToken item in items)
                {
                    if (!(item is JObject node)) throw new JsonException();
                    nodes.Add(ParseNode(node));
                }
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
            switch (RequiredString(node, "type"))
            {
                case "EnsureUnitCount":
                    CheckFields(node, "type", "unit", "count");
                    int unit;
                    switch (RequiredString(node, "unit"))
                    {
                        case "Villager": unit = 0; break;
                        case "Spearman": unit = 1; break;
                        case "Archer": unit = 2; break;
                        case "Knight": unit = 7; break;
                        default: throw new JsonException();
                    }
                    return new CommanderSemanticNode(CommanderSemanticNodeType.EnsureUnitCount,
                        unitType: unit, count: RequiredCount(node, 0, 200));

                case "BuildStructure":
                    CheckFields(node, "type", "structure", "count");
                    return new CommanderSemanticNode(CommanderSemanticNodeType.BuildStructure,
                        buildingType: ParseBuilding(RequiredString(node, "structure")),
                        count: RequiredCount(node, 1, 20));

                case "SetResourceAllocation":
                    CheckFields(node, "type", "resource", "count");
                    return new CommanderSemanticNode(CommanderSemanticNodeType.SetResourceAllocation,
                        resourceType: ParseResource(RequiredString(node, "resource")),
                        count: RequiredCount(node, 0, 200));

                case "StrategicObjective":
                    CheckFields(node, "type", "objective");
                    return new CommanderSemanticNode(CommanderSemanticNodeType.StrategicObjective,
                        strategicObjectiveType: ParseObjective(RequiredString(node, "objective")));

                default: throw new JsonException();
            }
        }

        private static BuildingType ParseBuilding(string name)
        {
            // Exact, defined enum names only. Executability for the current context is checked at admission.
            foreach (BuildingType value in Enum.GetValues(typeof(BuildingType)))
                if (name == value.ToString()) return value;
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
            JToken token = value["count"];
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
                    while (++i < json.Length)
                    {
                        c = json[i];
                        if (c == '"') { closed = true; break; }
                        if (c < 32) throw new JsonException();
                        if (c == '\\')
                        {
                            if (++i >= json.Length || "\"\\/bfnrtu".IndexOf(json[i]) < 0)
                                throw new JsonException();
                            if (json[i] == 'u')
                                for (int d = 0; d < 4; d++)
                                    if (++i >= json.Length || !Uri.IsHexDigit(json[i])) throw new JsonException();
                        }
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
