using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    public enum CommanderClarificationField { Count, Destination }

    public sealed class CommanderWorkerAllocationDraft
    {
        public CommanderWorkerAllocationMode Mode { get; }
        public CommanderWorkerCountMode CountMode { get; }
        public int? Count { get; }
        public CommanderWorkerSelector Workers { get; }
        public CommanderResourceDestination Destination { get; }
        public int? ResourceAmount { get; }
        public CommanderResourceAmountMode? ResourceAmountMode { get; }
        public IReadOnlyList<CommanderClarificationField> MissingFields { get; }
        internal CommanderWorkerAllocationDraft(CommanderWorkerAllocationMode mode, CommanderWorkerCountMode countMode,
            int? count, CommanderWorkerSelector workers, CommanderResourceDestination destination,
            int? resourceAmount = null, CommanderResourceAmountMode? resourceAmountMode = null)
        {
            Mode = mode; CountMode = countMode; Count = count; Workers = workers; Destination = destination;
            ResourceAmount = resourceAmount; ResourceAmountMode = resourceAmountMode;
            var missing = new List<CommanderClarificationField>();
            if (countMode == CommanderWorkerCountMode.Exact && !count.HasValue) missing.Add(CommanderClarificationField.Count);
            if (destination == null) missing.Add(CommanderClarificationField.Destination);
            MissingFields = missing.AsReadOnly();
        }
        internal JObject ToJson()
        {
            var worker = new JObject { ["state"] = Workers.State.ToString() };
            if (Workers.CurrentResource.HasValue) worker["currentResource"] = Workers.CurrentResource.Value.ToString();
            var result = new JObject { ["type"] = "AllocateWorkers", ["mode"] = Mode.ToString(), ["countMode"] = CountMode.ToString(), ["workers"] = worker };
            if (Count.HasValue) result["count"] = Count.Value;
            if (ResourceAmount.HasValue) result["resourceAmount"] = ResourceAmount.Value;
            if (ResourceAmountMode.HasValue) result["resourceAmountMode"] = ResourceAmountMode.Value.ToString();
            if (Destination != null) result["destination"] = new JObject { ["resource"] = Destination.Resource.ToString(), ["sourceKind"] = Destination.SourceKind.ToString() };
            return result;
        }
        internal CommanderWorkerAllocationDraft WithCount(int count) => new CommanderWorkerAllocationDraft(Mode, CountMode, count, Workers, Destination, ResourceAmount, ResourceAmountMode);
        internal CommanderSemanticResult CompleteResult() => CommanderSemanticJson.Parse(
            new JObject { ["outcome"] = "Request", ["nodes"] = new JArray(ToJson()) }.ToString(Formatting.None));
    }

    public sealed class CommanderPendingClarification
    {
        public CommanderWorkerAllocationDraft Draft { get; }
        public string OriginalText { get; }
        public string Question { get; }
        public int Turns { get; }
        internal int RuntimeGeneration { get; }
        internal long Sequence { get; }
        internal CommanderRequestTicket RequestTicket { get; }
        public long RequestId => RequestTicket?.Id ?? Sequence;
        internal CommanderPendingClarification(CommanderWorkerAllocationDraft draft, string original, string question,
            int generation, long sequence, int turns = 0, CommanderRequestTicket requestTicket = null)
        {
            Draft = draft; OriginalText = Bound(original, 1024); Question = Bound(question, 180);
            RuntimeGeneration = generation; Sequence = sequence; Turns = turns;
            RequestTicket = requestTicket;
        }
        internal string Serialize()
        {
            var missing = new JArray(); foreach (var field in Draft.MissingFields) missing.Add(field.ToString());
            return new JObject { ["draft"] = Draft.ToJson(), ["missingFields"] = missing, ["originalText"] = OriginalText, ["question"] = Question }.ToString(Formatting.None);
        }
        private static string Bound(string value, int maximum) => string.IsNullOrEmpty(value) ? string.Empty : value.Length <= maximum ? value : value.Substring(0, maximum);
    }

    public static class CommanderClarificationReplies
    {
        public static bool TryParseCount(string reply, out int count)
        {
            count = 0; string value = (reply ?? string.Empty).Trim().ToLowerInvariant();
            if (value.Length == 0 || value.Length > 40) return false;
            if (value.EndsWith(" villagers", StringComparison.Ordinal)) value = value.Substring(0, value.Length - 10).TrimEnd();
            else if (value.EndsWith(" villager", StringComparison.Ordinal)) value = value.Substring(0, value.Length - 9).TrimEnd();
            string[] words = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen", "twenty" };
            int word = Array.IndexOf(words, value);
            if (word >= 0) { count = word; return true; }
            for (int i = 0; i < value.Length; i++) if (value[i] < '0' || value[i] > '9') return false;
            return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out count);
        }
    }
}
