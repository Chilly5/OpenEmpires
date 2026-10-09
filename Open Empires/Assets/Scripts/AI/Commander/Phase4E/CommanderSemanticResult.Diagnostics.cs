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
            public SchemaFieldFailure(string field, SchemaFailureCode code)
                : base("Invalid semantic schema field.")
            {
                string reason = code == SchemaFailureCode.UnexpectedField ? "unexpected-field"
                    : code == SchemaFailureCode.MissingOrWrongType ? "missing-or-wrong-type"
                    : code == SchemaFailureCode.OutOfRange ? "out-of-range"
                    : code == SchemaFailureCode.MissingDependency ? "missing-dependency"
                    : code == SchemaFailureCode.IncompatibleDeclaration ? "conflicting-declarations" : "invalid-enum";
                Detail = "field=" + field + ";code=" + reason;
            }
        }

        private static SchemaFieldFailure FailField(JObject container, string field, SchemaFailureCode code)
        {
            string path = container?.Path ?? string.Empty;
            // Only paths through the schema's known containers may be exported.
            if (!Regex.IsMatch(path, @"^(nodes\[[0-3]\])?(\.(placement|constraints\[[0-3]\]|workers|destination|target|builders))?$"))
                path = string.Empty;
            const string knownFields = "|outcome|nodes|type|structure|unit|count|countMode|placement|anchor|ordinal|relation|clearGapTiles|resource|resourceAmount|resourceAmountMode|sourceKind|constraints|mode|amount|builders|workers|destination|state|executionOrder|dependsOn|producerFromNode|resultFromNode|quantityMode|targetAge|technology|location|unitSelector|producer|producerOrdinal|action|currentResource|";
            string name = knownFields.Contains("|" + field + "|") ? field : "<unknown>";
            return new SchemaFieldFailure((path.Length == 0 ? "" : path + ".") + name, code);
        }

        private static ResourceType ReadResourceField(JObject container, string field)
        {
            string name = RequiredString(container, field);
            try { return ParseResource(name); }
            catch (JsonException) { throw FailField(container, field, SchemaFailureCode.InvalidEnum); }
        }
    }
}
