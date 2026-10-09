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
        internal IReadOnlyDictionary<int, BuildingData> FutureProducerBindings { get; }
        public long RequestId { get; }
        public int PlayerId => Owner.PlayerId;
        public int Generation { get; }
        public string OriginalInput { get; }
        public string Preview { get; }
        public bool Cancelled { get; private set; }

        internal CommanderActionPlanCandidate(long requestId, CommanderGoalManager owner,
            CommanderSemanticResult interpretation, CommanderSemanticGraphPlan graph,
            string originalInput, int generation, StrategicPipeline strategicSource,
            Func<bool> isCurrentRequest, IReadOnlyDictionary<int, BuildingData> futureProducerBindings = null)
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
            FutureProducerBindings = futureProducerBindings ?? new Dictionary<int, BuildingData>();
            strategicRevision = StrategyRevision(strategicSource);
            var unitNames = new Dictionary<int, string>();
            foreach (var node in graph.Nodes)
            {
                int? requested = node.Intent is EnsureUnitCountIntent units ? units.UnitType
                    : node.Intent is WatchFutureUnitsIntent future ? future.UnitType : (int?)null;
                if (requested.HasValue) unitNames[node.Index] = CommanderIntentCatalog.GetUnitDisplayName(
                    Runtime.ResolveCivUnitType(PlayerId, requested.Value));
            }
            Preview = CommanderPlanPreview.Render(graph, unitNames);
        }

        internal bool TryApprove(CommanderGoalManager owner, int generation,
            StrategicPipeline currentStrategicSource)
        {
            if (Cancelled || approved || committed || Owner.IsDisposed || !HasLiveRequestLease()
                || !ReferenceEquals(owner, Owner) || !ReferenceEquals(Runtime, owner.Simulation)
                || generation != Generation || !ReferenceEquals(currentStrategicSource, strategicSource)
                || StrategyRevision(strategicSource) != strategicRevision
                || !Owner.AreFutureProducerBindingsCurrent(Graph, FutureProducerBindings)) return false;
            if (!Owner.TryCompileActionPlan(Interpretation, out var current, out _)
                || !CommanderScopeEquivalence.SameGraph(current, Graph)) return false;
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
                && Owner.AreFutureProducerBindingsCurrent(Graph, FutureProducerBindings)
                && StrategyRevision(strategicSource) == strategicRevision
                && Owner.TryCompileActionPlan(Interpretation, out var current, out _)
                && CommanderScopeEquivalence.SameGraph(current, Graph);

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

    internal static partial class CommanderPlanPreview
    {
        internal static string Render(CommanderSemanticGraphPlan graph, IReadOnlyDictionary<int, string> canonicalUnitNames = null)
        {
            var text = new StringBuilder("Action plan — not started.\n");
            foreach (var node in graph.Nodes)
            {
                string canonicalUnitName = null;
                canonicalUnitNames?.TryGetValue(node.Index, out canonicalUnitName);
                var dynamicSource = graph.DynamicProgram?.Nodes.FirstOrDefault(n => n.Id == node.DynamicNodeId);
                if (dynamicSource?.Primitive.Mechanic == CommanderDynamicMechanic.Produce)
                {
                    string id = dynamicSource.Parameter<string>("unit");
                    canonicalUnitName = KeybindManager.GetUnitTypeDisplayName(int.Parse(
                        id.Substring("unit:".Length), System.Globalization.CultureInfo.InvariantCulture));
                }
                string resultBuildingName=node.ResultFromNode.HasValue&&graph.Nodes[node.ResultFromNode.Value].Intent is BuildStructureIntent boundBuilding
                    ?Words(CommanderIntentCatalog.GetStructureDisplayName(boundBuilding.StructureType)):null;
                text.Append(node.Index + 1).Append(". ").Append(Describe(node.Intent, canonicalUnitName,resultBuildingName));
                if (node.Intent is EnsureUnitCountIntent units && !units.NewProductionCount.HasValue)
                    foreach (var quote in graph.ProductionExpectations)
                        if (quote.NodeIndex == node.Index)
                            text.Append("; expected ").Append(quote.NewCount).Append(" newly produced (existing and queued count toward the total)");
                if (node.DependsOn.Count > 0)
                {
                    text.Append("; after ").Append(string.Join(", ",node.DependsOn.Select(d=>"step "+(d+1))));
                }
                if (node.ProducerFromNode.HasValue)
                    text.Append("; ").Append(ProducerDescription(graph,node.ProducerFromNode.Value));
                if (node.ResultFromNode.HasValue)
                    text.Append("; ").Append(ResultDescription(graph,node.ResultFromNode.Value));
                if(dynamicSource!=null) AppendEffectBindings(text,graph,dynamicSource);
                foreach (var constraint in node.Intent.Constraints)
                {
                    // Lowering retains the immutable shared constraint objects. Print
                    // those once below, including ones not attached to this effect.
                    if(graph.DynamicProgram?.Constraints.Contains(constraint)==true)continue;
                    text.Append("; ").Append(ConstraintDescription(constraint));
                }
                text.Append('\n');
            }
            if (graph.DynamicProgram != null)
            {
                AppendSelections(text,graph);
                if(graph.DynamicProgram.Constraints.Count>0)
                {
                    text.Append("Shared restrictions:\n");
                    foreach(var constraint in graph.DynamicProgram.Constraints)
                        text.Append("  ").Append(ConstraintDescription(constraint)).Append('\n');
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

        private static string Describe(CommanderIntent intent, string canonicalUnitName = null,string resultBuildingName=null)
        {
            if (intent is EnsureUnitCountIntent units)
                return units.NewProductionCount.HasValue
                    ? "Produce " + units.NewProductionCount.Value + " new " + (canonicalUnitName ?? CommanderIntentCatalog.GetUnitDisplayName(units.UnitType))
                    : "Ensure " + (canonicalUnitName ?? CommanderIntentCatalog.GetUnitDisplayName(units.UnitType)) + " total " + units.TargetTotal;
            if (intent is BuildStructureIntent build)
                return "Build " + build.Count + " " + Words(CommanderIntentCatalog.GetStructureDisplayName(build.StructureType))
                    + (build.PlacementAnchorSelector.HasValue ? "; " + Relation(build.PlacementRelation?.ToString())
                        + " " + Anchor(build.PlacementAnchorSelector.Value.ToString())
                        + (build.PlacementAnchorOrdinal.HasValue?" number "+build.PlacementAnchorOrdinal:"")
                        + "; " + (build.ClearGapTiles??1) + "-tile clear gap between footprints"
                        + (build.PlacementResourceType.HasValue?" ("+build.PlacementResourceType+")":"") : "");
            if (intent is AllocateWorkersIntent workers)
            {
                var a = workers.Allocation;
                string selection=WorkerDescription(a.Workers.State.ToString(),a.Workers.CurrentResource?.ToString());
                string quantity=a.CountMode==CommanderWorkerCountMode.AllMatching
                    ? "Assign all currently "+selection+" (one-time snapshot)"
                    : a.Mode==CommanderWorkerAllocationMode.TargetTotal?"Target "+a.Count+" villagers in total"
                    : a.Mode==CommanderWorkerAllocationMode.Additional?"Assign "+a.Count+" additional "+selection
                    : "Assign exactly "+a.Count+" "+selection;
                return quantity+" to gather "+Destination(a.Destination.Resource,a.Destination.SourceKind)
                    + (a.ResourceAmount.HasValue ? "; " + a.ResourceAmount.Value
                        + (a.ResourceAmountMode == CommanderResourceAmountMode.AdditionalGathered ? " additional gathered income" : " stockpile target") : "");
            }
            if (intent is WatchFutureUnitsIntent future)
                return "Watch the next " + future.Count + " " + (canonicalUnitName ?? CommanderIntentCatalog.GetUnitDisplayName(future.UnitType))
                    + " from " + CommanderIntentCatalog.GetStructureDisplayName(future.ProducerType)
                    + (future.ProducerOrdinal.HasValue ? " number " + future.ProducerOrdinal.Value : " (only unambiguous owned producer)")
                    + " and " + (future.Action == CommanderFutureUnitAction.Gather
                        ? "gather " + future.Resource + " (" + future.SourceKind + ")"
                        : "patrol the worked " + future.Resource)
                    + "; no additional production";
            if (intent is SetResourceAllocationIntent allocation)
                return (allocation.Mode==ResourceAllocationMode.Increase?"Assign ":"Target ")
                    + (allocation.WorkerCount??(allocation.Mode==ResourceAllocationMode.Increase?1:throw new ArgumentException("Missing exact worker count.")))
                    + (allocation.Mode==ResourceAllocationMode.Increase?" additional villagers":" villagers in total")
                    + " to gather " + allocation.Resource;
            if (intent is ReachAgeIntent age)
                return "Reach " + (age.RequestedTarget==CommanderSemanticAgeTarget.Next?"the next age: ":"")
                    + ((CommanderSemanticAgeTarget)age.TargetAge) + " Age";
            if (intent is CapabilityActionIntent action)
                return DescribeAction(action,resultBuildingName);
            throw new ArgumentException("Unsupported preview effect.");
        }
    }
}
