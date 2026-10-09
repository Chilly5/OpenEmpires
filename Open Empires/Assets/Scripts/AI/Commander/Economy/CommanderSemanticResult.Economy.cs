using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    public sealed partial class CommanderSemanticResult
    {
        private static CommanderWorkerAllocation ParseWorkerAllocation(JObject node)
        {
            CheckFields(node, "type", "mode", "countMode", "count", "workers", "destination", "dependsOn", "resultFromNode", "resourceAmount", "resourceAmountMode");
            if (!(node["workers"] is JObject worker) || !(node["destination"] is JObject destination)) throw new JsonException();
            CheckFields(worker, "state", "currentResource");
            CheckFields(destination, "resource", "sourceKind");
            var mode = RequiredSemanticEnum<CommanderWorkerAllocationMode>(node, "mode");
            var countMode = RequiredSemanticEnum<CommanderWorkerCountMode>(node, "countMode");
            int? count = node.Property("count") == null ? (int?)null : RequiredCount(node, 1, 200);
            var state = RequiredSemanticEnum<CommanderWorkerState>(worker, "state");
            ResourceType? current = worker.Property("currentResource") == null ? (ResourceType?)null
                : ParseResource(RequiredString(worker, "currentResource"));
            var resource = ParseResource(RequiredString(destination, "resource"));
            var source = destination.Property("sourceKind") == null ? ResourceSourceKind.Any
                : RequiredSemanticEnum<ResourceSourceKind>(destination, "sourceKind");
            var allocation = new CommanderWorkerAllocation(mode, countMode, count,
                new CommanderWorkerSelector(state, current), new CommanderResourceDestination(resource, source),
                node.Property("resourceAmount") == null ? (int?)null : RequiredBoundedInteger(node, "resourceAmount", 1, 1000000),
                node.Property("resourceAmountMode") == null ? (CommanderResourceAmountMode?)null : RequiredSemanticEnum<CommanderResourceAmountMode>(node, "resourceAmountMode"));
            if (!allocation.IsValid(200)) throw new JsonException();
            return allocation;
        }

        private static T RequiredSemanticEnum<T>(JObject value, string field) where T : struct
        {
            string name = RequiredString(value, field);
            if (!Enum.TryParse(name, false, out T parsed) || !Enum.IsDefined(typeof(T), parsed)
                || parsed.ToString() != name) throw FailField(value, field, SchemaFailureCode.InvalidEnum);
            return parsed;
        }
    }
}
