using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    public sealed partial class CommanderSemanticResult
    {
        private static CommanderWorkerAllocationDraft ParsePendingDraft(JObject root)
        {
            if (!(root["pending"] is JObject draft)) throw new JsonException();
            CheckFields(draft, "type", "mode", "countMode", "count", "workers", "destination");
            if (RequiredString(draft, "type") != "AllocateWorkers") throw new JsonException();
            // Validate all already-resolved criteria with the completed strict parser.
            // Fill only temporary validation placeholders; they never become draft facts.
            var probe = (JObject)draft.DeepClone();
            if (probe.Property("count") == null && RequiredString(probe, "countMode") == "Exact") probe["count"] = 1;
            if (probe.Property("destination") == null) probe["destination"] = new JObject { ["resource"] = "Food" };
            CommanderWorkerAllocation allocation = ParseWorkerAllocation(probe);
            var result = new CommanderWorkerAllocationDraft(allocation.Mode, allocation.CountMode,
                draft.Property("count") == null ? (int?)null : allocation.Count, allocation.Workers,
                draft.Property("destination") == null ? null : allocation.Destination);
            if (result.MissingFields.Count == 0 || !(root["missingFields"] is JArray missing)
                || missing.Count != result.MissingFields.Count) throw new JsonException();
            var seen = new System.Collections.Generic.HashSet<CommanderClarificationField>();
            foreach (var value in missing)
            {
                if (value.Type != JTokenType.String || !Enum.TryParse((string)value, false, out CommanderClarificationField field)
                    || field.ToString() != (string)value || !seen.Add(field) || !ContainsField(result, field)) throw new JsonException();
            }
            return result;
        }
        private static bool ContainsField(CommanderWorkerAllocationDraft draft, CommanderClarificationField field)
        { foreach (var missing in draft.MissingFields) if (missing == field) return true; return false; }
    }
}
