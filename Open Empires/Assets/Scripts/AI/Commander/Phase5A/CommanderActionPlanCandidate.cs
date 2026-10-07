using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenEmpires
{
    // Game-owned sidecar: never parsed from provider JSON or serialized on the game wire.
    // Approval is a local event on the exact immutable candidate, not provider metadata.
    public sealed class CommanderActionPlanCandidate
    {
        internal CommanderGoalManager Owner { get; }
        internal GameSimulation Runtime { get; }
        internal CommanderSemanticResult Interpretation { get; }
        internal CommanderSemanticGraphPlan Graph { get; }
        private readonly StrategicPipeline strategicSource;
        private readonly string strategicRevision;
        private readonly Func<bool> isCurrentRequest;
        private bool approved;
        private bool committed;
        private IReadOnlyList<CommanderIntent> trustedTypedRoots;
        private string knownIntentEvidence;
        public string AuthorizationEvidence { get; private set; } = "None";
        internal IReadOnlyList<CommanderIntent> AuthorizedRootEffects { get; private set; }
        public long RequestId { get; }
        public int PlayerId => Owner.PlayerId;
        public int Generation { get; }
        public string OriginalInput { get; }
        public string Preview { get; }
        public bool Cancelled { get; private set; }

        internal CommanderActionPlanCandidate(long requestId, CommanderGoalManager owner,
            CommanderSemanticResult interpretation, CommanderSemanticGraphPlan graph,
            string originalInput, int generation, StrategicPipeline strategicSource,
            Func<bool> isCurrentRequest)
        {
            if (originalInput == null || originalInput.Length > 1024 || generation < 0)
                throw new ArgumentException("The request envelope is outside its bounds.");
            RequestId = requestId;
            Owner = owner;
            Runtime = owner.Simulation;
            Interpretation = interpretation;
            Graph = graph;
            OriginalInput = originalInput;
            Generation = generation;
            this.strategicSource = strategicSource;
            this.isCurrentRequest = isCurrentRequest;
            strategicRevision = StrategyRevision(strategicSource);
            Preview = CommanderPlanPreview.Render(graph);
        }

        internal bool TryApprove(CommanderGoalManager owner, int generation,
            StrategicPipeline currentStrategicSource)
        {
            if (Cancelled || approved || committed || Owner.IsDisposed || !HasLiveRequestLease()
                || !ReferenceEquals(owner, Owner) || !ReferenceEquals(Runtime, owner.Simulation)
                || generation != Generation || !ReferenceEquals(currentStrategicSource, strategicSource)
                || StrategyRevision(strategicSource) != strategicRevision) return false;
            if (!Owner.TryCompileActionPlan(Interpretation, out var current, out _)
                || CommanderPlanPreview.Render(current) != Preview) return false;
            // These exact intents are read-only typed values; re-admission above prevents
            // e.g. a changed Next-age observation from silently repairing the approved plan.
            approved = true;
            AuthorizationEvidence = trustedTypedRoots != null ? "TrustedTypedInput"
                : knownIntentEvidence ?? "LocalPlanConfirmation";
            AuthorizedRootEffects = trustedTypedRoots ?? Array.AsReadOnly(Graph.Nodes.Select(n => n.Intent).ToArray());
            Graph.Authorization = this;
            Owner.TraceActionPlan(this, "approved");
            return true;
        }

        internal bool CanCommit(CommanderGoalManager owner, CommanderSemanticGraphPlan graph)
            => approved && !committed && !Cancelled && !Owner.IsDisposed
                && HasLiveRequestLease()
                && ReferenceEquals(owner, Owner) && ReferenceEquals(graph, Graph)
                && ReferenceEquals(Runtime, owner.Simulation)
                && StrategyRevision(strategicSource) == strategicRevision;

        internal bool Consume(CommanderGoalManager owner, CommanderSemanticGraphPlan graph)
        {
            if (!CanCommit(owner, graph)) return false;
            committed = true;
            return true;
        }

        internal void Cancel() { Cancelled = true; }

        internal void SetKnownIntentEvidence(CommanderGoalManager owner, bool clarified)
        {
            if (!ReferenceEquals(owner, Owner) || approved || committed || Cancelled
                || Graph.DynamicProgram != null || Graph.Nodes.Count != 1 || trustedTypedRoots != null)
                throw new InvalidOperationException("The established KnownIntent scope is unavailable.");
            knownIntentEvidence = clarified ? "ClarifiedKnownIntentAutomatic" : "KnownIntentAutomatic";
        }

        internal void SetTrustedTypedRoots(CommanderGoalManager owner, IReadOnlyList<CommanderIntent> roots)
        {
            if (!ReferenceEquals(owner, Owner) || approved || committed || Cancelled || roots == null)
                throw new InvalidOperationException("The trusted typed root evidence is unavailable.");
            trustedTypedRoots = Array.AsReadOnly(roots.ToArray());
        }

        private bool HasLiveRequestLease()
        {
            // Host-associated requests retain a live generation/cancellation guard all
            // the way to commit, even after their displayed pointer was detached.
            // Independent trusted typed callers are governed by their manager lifetime.
            try { return isCurrentRequest?.Invoke() ?? true; }
            catch (Exception) { return false; }
        }

        private static string StrategyRevision(StrategicPipeline pipeline)
        {
            if (pipeline == null) return "none";
            var planner = pipeline.StrategicPlanner;
            var key = new StringBuilder();
            foreach (var plan in planner.Plans)
                key.Append(plan.StrategicPlanId).Append(':').Append(plan.Revision)
                    .Append(':').Append(plan.Status).Append(';');
            return key.ToString();
        }
    }

    internal static class CommanderPlanPreview
    {
        internal static string Render(CommanderSemanticGraphPlan graph)
        {
            var text = new StringBuilder("Action plan — not started.\n");
            foreach (var node in graph.Nodes)
            {
                string canonicalUnitName = null;
                var dynamicSource = graph.DynamicProgram?.Nodes.FirstOrDefault(n => n.Id == node.DynamicNodeId);
                if (dynamicSource?.Primitive.Mechanic == CommanderDynamicMechanic.Produce)
                {
                    string id = dynamicSource.Parameter<string>("unit");
                    canonicalUnitName = KeybindManager.GetUnitTypeDisplayName(int.Parse(
                        id.Substring("unit:".Length), System.Globalization.CultureInfo.InvariantCulture));
                }
                text.Append(node.Index + 1).Append(". ").Append(Describe(node.Intent, canonicalUnitName));
                if (node.DependsOn.Count > 0)
                {
                    text.Append("; after");
                    foreach (int dependency in node.DependsOn) text.Append(' ').Append(dependency + 1);
                }
                if (node.ProducerFromNode.HasValue)
                    text.Append("; only new producer from step ").Append(node.ProducerFromNode.Value + 1);
                if (node.ResultFromNode.HasValue)
                    text.Append("; only exact result from step ").Append(node.ResultFromNode.Value + 1);
                foreach (var constraint in node.Intent.Constraints)
                {
                    if (constraint is ProtectedResourceConstraint floor)
                        text.Append("; protect ").Append(floor.Resource).Append(" workers >= ")
                            .Append(floor.MinimumWorkers?.ToString() ?? "current count");
                    else if (constraint is PreferredWorkersConstraint workers)
                        text.Append("; workers ").Append(workers.WorkerSource);
                    else if (constraint is MaximumQueueConstraint queue)
                        text.Append("; maximum queue ").Append(queue.MaximumQueue);
                    else if (constraint is NoConstructionConstraint)
                        text.Append("; construction forbidden (new and resumed)");
                    else if (constraint is ResourceSourceConstraint source)
                        text.Append("; prepare ").Append(source.Resource).Append(" only from ").Append(source.SourceKind);
                    else throw new ArgumentException("Unsupported preview constraint.");
                }
                text.Append('\n');
            }
            if (graph.DynamicProgram != null)
            {
                text.Append("Symbolic restrictions (fixed by this plan):\n");
                foreach (var node in graph.DynamicProgram.Nodes)
                {
                    text.Append("  ").Append(node.Id).Append(": ").Append(node.Primitive.Id).Append('(');
                    bool separator = false;
                    foreach (var parameter in node.Parameters.OrderBy(p => p.Key, StringComparer.Ordinal))
                    {
                        if (separator) text.Append(", ");
                        separator = true;
                        text.Append(parameter.Key).Append('=').Append(parameter.Value is int amount
                            ? amount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            : (string)parameter.Value);
                    }
                    foreach (var input in node.Inputs.OrderBy(p => p.Key, StringComparer.Ordinal))
                    {
                        if (separator) text.Append(", ");
                        separator = true;
                        text.Append(input.Key).Append("=result:").Append(input.Value);
                    }
                    text.Append(')');
                    if (node.DependsOn.Count > 0)
                        text.Append(" after ").Append(string.Join(",", node.DependsOn));
                    text.Append('\n');
                }
                text.Append("Worker selections are frozen once; partition roles cannot overlap. "
                    + "Locations use map directions and footprint-to-footprint clear gaps within bounded tolerance.\n");
            }
            bool noConstruction = graph.ConstructionForbidden;
            foreach (var node in graph.Nodes)
                foreach (var constraint in node.Intent.Constraints)
                    if (constraint is NoConstructionConstraint) noConstruction = true;
            text.Append(noConstruction
                ? "Preparation cannot construct buildings; use existing completed capacity or report a blocker. "
                : "Preparation may include canonical required buildings, population and resources within these effects. ");
            text.Append("No unrelated strategy is authorized. Approve plan or cancel plan.");
            if (text.Length > 8192) throw new ArgumentException("The normalized preview is too large.");
            return text.ToString();
        }

        internal static string RenderIntent(CommanderIntent intent)
            => Render(new CommanderSemanticGraphPlan(
                Array.AsReadOnly(new[] { new CommanderSemanticGraphNode(0, intent, Array.Empty<int>(), null, null) }),
                Array.AsReadOnly(new[] { 0 })));

        private static string Describe(CommanderIntent intent, string canonicalUnitName = null)
        {
            if (intent is EnsureUnitCountIntent units)
                return units.NewProductionCount.HasValue
                    ? "Produce " + units.NewProductionCount.Value + " new " + (canonicalUnitName ?? CommanderIntentCatalog.GetUnitDisplayName(units.UnitType))
                    : "Ensure " + (canonicalUnitName ?? CommanderIntentCatalog.GetUnitDisplayName(units.UnitType)) + " total " + units.TargetTotal;
            if (intent is BuildStructureIntent build)
                return "Build " + build.Count + " " + build.StructureType
                    + (build.PlacementAnchorSelector.HasValue ? "; " + build.PlacementRelation
                        + " " + build.PlacementAnchorSelector + " ordinal " + build.PlacementAnchorOrdinal
                        + "; footprint gap " + build.ClearGapTiles + "; resource " + build.PlacementResourceType : "");
            if (intent is AllocateWorkersIntent workers)
            {
                var a = workers.Allocation;
                return "Allocate workers " + a.Mode + " " + a.CountMode + " " + a.Count
                    + "; " + a.Workers.State + " from " + a.Workers.CurrentResource
                    + " to " + a.Destination.Resource + " source " + a.Destination.SourceKind;
            }
            if (intent is SetResourceAllocationIntent allocation)
                return "Allocate " + allocation.WorkerCount + " workers " + allocation.Mode + " " + allocation.Resource;
            if (intent is ReachAgeIntent age)
                return "Reach " + age.RequestedTarget + " (resolved age " + age.TargetAge + ")";
            if (intent is CapabilityActionIntent action)
                return action.ActionType + " " + action.UnitSelector.Count + " " + action.UnitSelector.Kind
                    + " unit type " + action.UnitSelector.UnitType + "; location " + action.LocationSelector.Kind
                    + " " + action.LocationSelector.ResourceType + "; radius " + action.LocationSelector.RadiusTiles
                    + "; structure " + action.StructureType + "; technology " + action.Technology;
            throw new ArgumentException("Unsupported preview effect.");
        }
    }
}
