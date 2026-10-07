using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    public sealed partial class CommanderSemanticResult
    {
        private static IReadOnlyList<CommanderConstraint> ParseConstraints(JObject node)
        {
            if (node.Property("constraints") == null) return Array.Empty<CommanderConstraint>();
            if (!(node["constraints"] is JArray items) || items.Count > 4) throw new JsonException();
            var result = new List<CommanderConstraint>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (!(item is JObject value)) throw new JsonException();
                string kind = RequiredString(value, "type");
                if (!seen.Add(kind)) throw new JsonException();
                switch (kind)
                {
                    case "NoConstruction":
                        CheckFields(value, "type");
                        result.Add(new NoConstructionConstraint());
                        break;
                    case "PreferredWorkers":
                        CheckFields(value, "type", "mode");
                        if (RequiredString(value, "mode") != "IdleOnly") throw new JsonException();
                        result.Add(new PreferredWorkersConstraint(CommanderPreferredWorkerSource.IdleOnly));
                        break;
                    case "MaximumQueue":
                        CheckFields(value, "type", "amount");
                        result.Add(new MaximumQueueConstraint(ConstraintAmount(value, 1, CommanderIntentValidator.MaximumQueuePolicy)));
                        break;
                    case "ProtectedResource":
                        CheckFields(value, "type", "resource", "amount");
                        result.Add(new ProtectedResourceConstraint(ParseResource(RequiredString(value, "resource")),
                            value.Property("amount") == null ? (int?)null : ConstraintAmount(value, 0, 200)));
                        break;
                    case "ResourceSource":
                        CheckFields(value, "type", "resource", "sourceKind");
                        var resource = ParseResource(RequiredString(value, "resource"));
                        string sourceName = RequiredString(value, "sourceKind");
                        if (!Enum.TryParse(sourceName, false, out ResourceSourceKind source)
                            || source.ToString() != sourceName || !ResourceSourceRules.IsDefined(source)
                            || !ResourceSourceRules.IsCompatible(resource, source)) throw new JsonException();
                        result.Add(new ResourceSourceConstraint(resource, source));
                        break;
                    default: throw new JsonException();
                }
            }
            return result.AsReadOnly();
        }

        private static int ConstraintAmount(JObject value, int minimum, int maximum)
        {
            if (value["amount"]?.Type != JTokenType.Integer) throw new JsonException();
            int amount = value["amount"].Value<int>();
            if (amount < minimum || amount > maximum) throw new JsonException();
            return amount;
        }
    }
}
