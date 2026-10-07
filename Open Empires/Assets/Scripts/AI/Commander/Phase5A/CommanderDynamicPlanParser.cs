using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    // Deliberately a partial of the existing strict parser so its lexical guard is reused.
    // A structurally valid plan is NOT an executable or authorized candidate.
    public sealed partial class CommanderSemanticResult
    {
        internal static bool ParseDynamicTrusted(string raw, out CommanderDynamicPlan plan)
        {
            plan = null;
            try
            {
                if (string.IsNullOrWhiteSpace(raw) ||
                    raw.Length > CommanderDynamicPlan.MaximumCharacters) throw new JsonException();
                CheckStrictSyntax(raw);
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
                CheckFields(root, "outcome", "version", "nodes", "constraints");
                if (RequiredString(root, "outcome") != "DynamicPlan" ||
                    !(root["version"] is JValue version) ||
                    version.Type != JTokenType.Integer || version.Value<int>() != 1 ||
                    !(root["nodes"] is JArray items) || items.Count < 1 ||
                    items.Count > CommanderDynamicPlan.MaximumNodes) throw new JsonException();

                var nodes = new List<CommanderDynamicNode>(items.Count);
                foreach (JToken item in items)
                    nodes.Add(ParseDynamicNode(item as JObject));
                ValidateDynamicGraph(nodes);
                plan = new CommanderDynamicPlan(nodes, ParseConstraints(root));
                return true;
            }
            catch (Exception e) when (e is JsonException || e is ArgumentException ||
                e is FormatException || e is OverflowException)
            {
                plan = null;
                return false;
            }
        }

        private static CommanderDynamicNode ParseDynamicNode(JObject value)
        {
            if (value == null) throw new JsonException();
            CheckFields(value, "id", "mechanic", "parameters", "inputs", "dependsOn");
            string id = RequiredString(value, "id");
            if (!ValidDynamicId(id)) throw new JsonException();
            string mechanic = RequiredString(value, "mechanic");
            if (mechanic.Length > CommanderDynamicPlan.MaximumEnumCharacters) throw new JsonException();
            var primitive = CommanderDynamicPrimitiveRegistry.Find(mechanic);
            if (primitive == null || !(value["parameters"] is JObject parametersObject) ||
                !(value["inputs"] is JObject inputsObject) ||
                !(value["dependsOn"] is JArray dependenciesObject)) throw new JsonException();

            var parameters = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (JProperty property in parametersObject.Properties())
            {
                CommanderDynamicField field = null;
                foreach (var candidate in primitive.Parameters)
                    if (candidate.Name == property.Name) { field = candidate; break; }
                if (field == null) throw new JsonException();
                parameters.Add(property.Name, ParseDynamicValue(field.Kind, property.Value));
            }
            foreach (var field in primitive.Parameters)
                if (field.Required && !parameters.ContainsKey(field.Name)) throw new JsonException();

            var inputs = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JProperty property in inputsObject.Properties())
            {
                CommanderDynamicInput role = null;
                foreach (var candidate in primitive.Inputs)
                    if (candidate.Role == property.Name) { role = candidate; break; }
                if (role == null || property.Value.Type != JTokenType.String) throw new JsonException();
                string target = (string)property.Value;
                if (!ValidDynamicId(target)) throw new JsonException();
                inputs.Add(property.Name, target);
            }
            foreach (var role in primitive.Inputs)
                if (role.Required && !inputs.ContainsKey(role.Role)) throw new JsonException();

            var dependencies = new List<string>();
            foreach (JToken token in dependenciesObject)
            {
                if (token.Type != JTokenType.String) throw new JsonException();
                string target = (string)token;
                if (!ValidDynamicId(target) || dependencies.Contains(target)) throw new JsonException();
                dependencies.Add(target);
            }
            if (dependencies.Count + inputs.Count > CommanderDynamicPlan.MaximumReferencesPerNode)
                throw new JsonException();
            ValidateDynamicCombinations(primitive.Mechanic, parameters, inputs);
            return new CommanderDynamicNode(id, primitive, parameters, inputs, dependencies);
        }

        private static bool ValidDynamicId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > CommanderDynamicPlan.MaximumIdCharacters)
                return false;
            if (!((id[0] >= 'A' && id[0] <= 'Z') || (id[0] >= 'a' && id[0] <= 'z') || id[0] == '_'))
                return false;
            foreach (char c in id)
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')
                    || (c >= '0' && c <= '9') || c == '_' || c == '-')) return false;
            return true;
        }

        private static object ParseDynamicValue(CommanderDynamicFieldKind kind, JToken token)
        {
            if (kind == CommanderDynamicFieldKind.Count ||
                kind == CommanderDynamicFieldKind.Offset ||
                kind == CommanderDynamicFieldKind.ClearGapTiles)
            {
                if (token.Type != JTokenType.Integer) throw new JsonException();
                int n = token.Value<int>();
                int min = kind == CommanderDynamicFieldKind.Offset ? 0 : 1;
                int max = kind == CommanderDynamicFieldKind.ClearGapTiles ? 20 : 200;
                if (n < min || n > max) throw new JsonException();
                return n;
            }
            if (token.Type != JTokenType.String) throw new JsonException();
            string s = (string)token;
            int maximum = kind == CommanderDynamicFieldKind.UnitId ||
                kind == CommanderDynamicFieldKind.BuildingId
                ? CommanderDynamicPlan.MaximumCanonicalIdCharacters
                : CommanderDynamicPlan.MaximumEnumCharacters;
            if (string.IsNullOrEmpty(s) || s.Length > maximum) throw new JsonException();
            switch (kind)
            {
                case CommanderDynamicFieldKind.UnitId:
                    if (!s.StartsWith("unit:", StringComparison.Ordinal) ||
                        !int.TryParse(s.Substring(5), NumberStyles.None,
                            CultureInfo.InvariantCulture, out int unit) ||
                        unit < 0 || s != "unit:" + unit.ToString(CultureInfo.InvariantCulture))
                        throw new JsonException();
                    break;
                case CommanderDynamicFieldKind.BuildingId:
                    if (!s.StartsWith("building:", StringComparison.Ordinal) ||
                        !NamedEnum<BuildingType>(s.Substring(9))) throw new JsonException();
                    break;
                case CommanderDynamicFieldKind.Resource:
                    if (!NamedEnum<ResourceType>(s)) throw new JsonException();
                    break;
                case CommanderDynamicFieldKind.SourceKind:
                    if (!NamedEnum<ResourceSourceKind>(s)) throw new JsonException();
                    break;
                case CommanderDynamicFieldKind.WorkerState:
                    if (!NamedEnum<CommanderWorkerState>(s)) throw new JsonException();
                    break;
                case CommanderDynamicFieldKind.ResourceMode:
                    if (s != "Visible" && s != "Worked") throw new JsonException();
                    break;
                case CommanderDynamicFieldKind.Anchor:
                    if (s != "MyTownCenter" && s != "MyBarracks") throw new JsonException();
                    break;
                case CommanderDynamicFieldKind.Relation:
                    if (s != "Near" && s != "MapWest" && s != "MapEast")
                        throw new JsonException();
                    break;
                case CommanderDynamicFieldKind.QuantityMode:
                    if (s != "New" && s != "TargetTotal") throw new JsonException();
                    break;
                case CommanderDynamicFieldKind.UnitKind:
                    if (!NamedEnum<CommanderSemanticUnitSelector>(s)) throw new JsonException();
                    break;
                default: throw new JsonException();
            }
            return s;
        }

        private static bool NamedEnum<T>(string value) where T : struct
        {
            foreach (string name in Enum.GetNames(typeof(T)))
                if (name == value) return true;
            return false;
        }

        private static void ValidateDynamicCombinations(CommanderDynamicMechanic mechanic,
            Dictionary<string, object> parameters, Dictionary<string, string> inputs)
        {
            if (mechanic == CommanderDynamicMechanic.Build
                && (int)parameters["count"] > CommanderIntentValidator.MaximumStructureCount)
                throw new JsonException();
            if (mechanic == CommanderDynamicMechanic.SelectUnits && (int)parameters["count"] > 50)
                throw new JsonException();
            if (mechanic == CommanderDynamicMechanic.SelectUnits &&
                parameters.ContainsKey("unit") == parameters.ContainsKey("kind"))
                throw new JsonException();
            if (mechanic == CommanderDynamicMechanic.SelectWorkers &&
                parameters.ContainsKey("currentResource") &&
                (!parameters.TryGetValue("state", out object state) ||
                    (string)state != "Gathering")) throw new JsonException();
            if (mechanic == CommanderDynamicMechanic.ResolveLocation &&
                (Convert.ToInt32(parameters.ContainsKey("anchor")) +
                 Convert.ToInt32(inputs.ContainsKey("structures")) +
                 Convert.ToInt32(inputs.ContainsKey("resources"))) != 1)
                throw new JsonException();
            if (mechanic == CommanderDynamicMechanic.SelectResources ||
                mechanic == CommanderDynamicMechanic.AllocateWorkers)
            {
                var resource = (ResourceType)Enum.Parse(typeof(ResourceType),
                    (string)parameters["resource"]);
                var source = (ResourceSourceKind)Enum.Parse(typeof(ResourceSourceKind),
                    (string)parameters["sourceKind"]);
                if (!ResourceSourceRules.IsCompatible(resource, source)) throw new JsonException();
            }
        }

        private static void ValidateDynamicGraph(List<CommanderDynamicNode> nodes)
        {
            var byId = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < nodes.Count; i++)
                if (!byId.TryAdd(nodes[i].Id, i)) throw new JsonException();
            int aggregate = 0;
            foreach (var node in nodes)
            {
                foreach (string dep in node.DependsOn)
                    if (dep == node.Id || !byId.ContainsKey(dep)) throw new JsonException();
                foreach (var input in node.Inputs)
                {
                    if (!byId.TryGetValue(input.Value, out int target) ||
                        !Contains(node.DependsOn, input.Value)) throw new JsonException();
                    CommanderDynamicInput role = null;
                    foreach (var candidate in node.Primitive.Inputs)
                        if (candidate.Role == input.Key) { role = candidate; break; }
                    if (role == null || nodes[target].Primitive.Result != role.Accepts)
                        throw new JsonException();
                }
                if (node.Primitive.Mechanic != CommanderDynamicMechanic.PartitionWorkers &&
                    node.Parameters.TryGetValue("count", out object count))
                {
                    aggregate = checked(aggregate + (int)count);
                    if (aggregate > CommanderDynamicPlan.MaximumAggregateCount)
                        throw new JsonException();
                }
            }
            var state = new int[nodes.Count * 2];
            for (int i = 0; i < nodes.Count; i++)
                if (DynamicDepth(i, nodes, byId, state) >
                    CommanderDynamicPlan.MaximumDependencyDepth) throw new JsonException();

            // Selection is symbolic here. Only source-relative ranges are validated;
            // no worker runtime IDs are invented or exposed.
            var workerUses = new List<(int consumer, string root, int start, int end)>();
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Primitive.Mechanic == CommanderDynamicMechanic.PartitionWorkers)
                    DynamicWorkerSpan(i, nodes, byId);
                if (nodes[i].Inputs.TryGetValue("workers", out string source) &&
                    (nodes[i].Primitive.Mechanic == CommanderDynamicMechanic.Build ||
                     nodes[i].Primitive.Mechanic == CommanderDynamicMechanic.AllocateWorkers))
                {
                    var span = DynamicWorkerSpan(byId[source], nodes, byId);
                    workerUses.Add((i, span.root, span.start, span.end));
                }
            }
            for (int i = 0; i < workerUses.Count; i++)
                for (int j = i + 1; j < workerUses.Count; j++)
                {
                    var a = workerUses[i]; var b = workerUses[j];
                    if (a.root == b.root && a.start < b.end && b.start < a.end &&
                        !DynamicDependsOn(a.consumer, b.consumer, nodes, byId) &&
                        !DynamicDependsOn(b.consumer, a.consumer, nodes, byId))
                        throw new JsonException();
                }
        }

        private static bool Contains(IReadOnlyList<string> values, string target)
        {
            foreach (string value in values) if (value == target) return true;
            return false;
        }

        private static int DynamicDepth(int i, List<CommanderDynamicNode> nodes,
            Dictionary<string, int> byId, int[] state)
        {
            if (state[i] == 1) throw new JsonException();
            if (state[i] == 2) return state[nodes.Count + i];
            state[i] = 1;
            int depth = 0;
            foreach (string dep in nodes[i].DependsOn)
                depth = Math.Max(depth, 1 + DynamicDepth(byId[dep], nodes, byId, state));
            state[i] = 2;
            state[nodes.Count + i] = depth;
            return depth;
        }

        private static (string root, int start, int end) DynamicWorkerSpan(int i,
            List<CommanderDynamicNode> nodes, Dictionary<string, int> byId)
        {
            var node = nodes[i];
            if (node.Primitive.Mechanic == CommanderDynamicMechanic.SelectWorkers)
                return (node.Id, 0, node.Parameter<int>("count"));
            if (node.Primitive.Mechanic != CommanderDynamicMechanic.PartitionWorkers)
                throw new JsonException();
            var source = DynamicWorkerSpan(byId[node.Inputs["workers"]], nodes, byId);
            int offset = node.Parameter<int>("offset"), count = node.Parameter<int>("count");
            if (offset > source.end - source.start ||
                count > source.end - source.start - offset) throw new JsonException();
            return (source.root, source.start + offset, source.start + offset + count);
        }

        private static bool DynamicDependsOn(int node, int ancestor,
            List<CommanderDynamicNode> nodes, Dictionary<string, int> byId)
        {
            foreach (string dep in nodes[node].DependsOn)
            {
                int i = byId[dep];
                if (i == ancestor || DynamicDependsOn(i, ancestor, nodes, byId))
                    return true;
            }
            return false;
        }
    }
}
