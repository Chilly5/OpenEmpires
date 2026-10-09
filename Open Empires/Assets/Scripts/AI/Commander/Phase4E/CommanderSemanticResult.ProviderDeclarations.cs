using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    public sealed partial class CommanderSemanticResult
    {
        private static IReadOnlyList<CommanderConstraint> ParseBuildConstraints(JObject node, bool requireDeclarations)
        {
            // Preserve the supplied constraints first, including their strict diagnostics.
            var constraints = ParseConstraints(node);
            if (!requireDeclarations) return constraints;
            if (!(node["builders"] is JObject builders))
                throw FailField(node, "builders", SchemaFailureCode.MissingOrWrongType);
            CheckFields(builders, "state", "count");
            RequiredBoundedInteger(builders, "count", 1, 1); // This ordinary construction path chooses one builder.
            string state = RequiredString(builders, "state");
            if (state != "Eligible" && state != "IdleOnly")
                throw FailField(builders, "state", SchemaFailureCode.InvalidEnum);
            bool alreadyIdleOnly = false;
            foreach (var constraint in constraints)
                if (constraint is PreferredWorkersConstraint) alreadyIdleOnly = true;
            if (state == "Eligible" && alreadyIdleOnly)
                throw FailField(builders, "state", SchemaFailureCode.IncompatibleDeclaration);
            if (state == "Eligible" || alreadyIdleOnly) return constraints;
            if (constraints.Count >= 4)
                throw FailField(node, "constraints", SchemaFailureCode.OutOfRange);
            var normalized = new List<CommanderConstraint>(constraints)
                { new PreferredWorkersConstraint(CommanderPreferredWorkerSource.IdleOnly) };
            return normalized.AsReadOnly();
        }

        private static string ReadProviderExecutionOrder(JObject root, int count)
        {
            // Omission on one node is harmless; omission on a compound is not an ordering declaration.
            if (count == 1 && root.Property("executionOrder") == null) return "Independent";
            string order = RequiredString(root, "executionOrder");
            if (order != "Independent" && order != "Sequential" && order != "DependencyGraph")
                throw FailField(root, "executionOrder", SchemaFailureCode.InvalidEnum);
            return order;
        }

        private static void ValidateProviderExecutionOrder(JObject root, JArray items,
            IReadOnlyList<CommanderSemanticNode> nodes, string order)
        {
            bool hasDependencies = false;
            for (int i = 0; i < nodes.Count; i++)
            {
                hasDependencies |= nodes[i].DependsOn.Count > 0;
                if (order != "Sequential" || i == 0) continue;
                bool hasPredecessor = false;
                foreach (int dependency in nodes[i].DependsOn)
                    if (dependency == i - 1) hasPredecessor = true;
                if (!hasPredecessor)
                    throw FailField((JObject)items[i], "dependsOn", SchemaFailureCode.MissingDependency);
            }
            if (order == "Independent" && hasDependencies || order == "DependencyGraph" && !hasDependencies)
                throw FailField(root, "executionOrder", SchemaFailureCode.IncompatibleDeclaration);
        }
    }
}
