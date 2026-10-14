using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    public sealed partial class CommanderSemanticResult
    {
        // Diagnostic metadata only. Never provider values, arbitrary keys, or authority.
        public string SchemaDiagnostic { get; private set; } = string.Empty;

        private enum SchemaFailureCode { UnexpectedField, MissingOrWrongType, OutOfRange, InvalidEnum, MissingDependency, IncompatibleDeclaration }

        private sealed class SchemaFieldFailure : JsonException
        {
            public string Detail { get; }
            public SchemaFieldFailure(string field, SchemaFailureCode code, string expected, string actual)
                : base("Invalid semantic schema field.")
            {
                string reason = code == SchemaFailureCode.UnexpectedField ? "unexpected-field"
                    : code == SchemaFailureCode.MissingOrWrongType ? "missing-or-wrong-type"
                    : code == SchemaFailureCode.OutOfRange ? "out-of-range"
                    : code == SchemaFailureCode.MissingDependency ? "missing-dependency"
                    : code == SchemaFailureCode.IncompatibleDeclaration ? "conflicting-declarations" : "invalid-enum";
                Detail = "field=" + field + ";code=" + reason + ";expected=" + expected + ";actual=" + actual;
            }
        }

        private static SchemaFieldFailure FailField(JObject container, string field, SchemaFailureCode code, JTokenType? expected = null)
        {
            string path = container?.Path ?? string.Empty;
            // Only paths through the schema's known containers may be exported.
            if (!Regex.IsMatch(path, @"^(pending\.)?(nodes\[(?:[0-9]|1[01])\])?(\.(placement|constraints\[[0-3]\]|workers|destination|target|builders|parameters|inputs))?$|^pending$|^missingSlots\[[0-3]\]$"))
                path = string.Empty;
            const string knownFields = "|outcome|nodes|type|structure|unit|count|countMode|placement|anchor|ordinal|relation|clearGapTiles|resource|resourceAmount|resourceAmountMode|sourceKind|constraints|mode|amount|builders|workers|destination|state|executionOrder|dependsOn|producerFromNode|resultFromNode|quantityMode|targetAge|technology|location|unitSelector|producer|producerOrdinal|action|currentResource|version|mechanic|parameters|inputs|building|producers|offset|id|message|pending|missingFields|missingSlots|node|path|alternatives|";
            string name = knownFields.Contains("|" + field + "|") ? field : "<unknown>";
            return new SchemaFieldFailure((path.Length == 0 ? "" : path + ".") + name, code,
                expected?.ToString() ?? (code == SchemaFailureCode.UnexpectedField ? "AllowedField" : "ValidValue"),
                container?[field]?.Type.ToString() ?? "Missing");
        }

        internal static JsonException InvalidClarificationSlot(JObject slot,bool knownType)
            => FailField(slot,knownType?"path":"type",SchemaFailureCode.IncompatibleDeclaration,JTokenType.String);

        private static void ValidatePendingGraphFieldTypes(JObject root, JObject graph)
        {
            // Preserve bounded schema paths before the draft's semantic checks. These
            // shapes were already rejected; this grants no validity or admission.
            if (!(graph["nodes"] is JArray nodes))
                throw FailField(graph, "nodes", SchemaFailureCode.MissingOrWrongType, JTokenType.Array);
            if (!(root["missingSlots"] is JArray slots))
                throw FailField(root, "missingSlots", SchemaFailureCode.MissingOrWrongType, JTokenType.Array);
            if (nodes.Count < 1 || nodes.Count > CommanderSemanticJson.MaximumNodes)
                throw FailField(graph, "nodes", SchemaFailureCode.OutOfRange, JTokenType.Array);
            if (slots.Count < 1 || slots.Count > 4)
                throw FailField(root, "missingSlots", SchemaFailureCode.OutOfRange, JTokenType.Array);
            for (int i = 0; i < nodes.Count; i++)
                if (!(nodes[i] is JObject))
                    throw new SchemaFieldFailure("pending.nodes[" + i + "]", SchemaFailureCode.MissingOrWrongType,
                        JTokenType.Object.ToString(), nodes[i].Type.ToString());
            for (int i = 0; i < slots.Count; i++)
            {
                if (!(slots[i] is JObject slot))
                    throw new SchemaFieldFailure("missingSlots[" + i + "]", SchemaFailureCode.MissingOrWrongType,
                        JTokenType.Object.ToString(), slots[i].Type.ToString());
                if (slot["node"]?.Type != JTokenType.Integer)
                    throw FailField(slot, "node", SchemaFailureCode.MissingOrWrongType, JTokenType.Integer);
                RequiredString(slot, "path");
                RequiredString(slot, "type");
                if (!(slot["alternatives"] is JArray))
                    throw FailField(slot, "alternatives", SchemaFailureCode.MissingOrWrongType, JTokenType.Array);
            }
        }

        private static ResourceType ReadResourceField(JObject container, string field)
        {
            string name = RequiredString(container, field);
            try { return ParseResource(name); }
            catch (JsonException) { throw FailField(container, field, SchemaFailureCode.InvalidEnum); }
        }
    }
}
