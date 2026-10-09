using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public sealed partial class CommanderGoalManager
    {
        private long nextRequestId = 1;
        private CommanderActionPlanCandidate pendingKnownAuthority;
        public bool RequestTracingEnabled { get; set; }

        internal CommanderRequestTicket BeginSemanticRequest(string input, int generation, Func<bool> isCurrent)
        {
            ThrowIfDisposed();
            if (input == null || input.Length > CommanderSemanticProviderRequest.MaximumPlayerMessageCharacters
                || generation < 0 || nextRequestId == long.MaxValue)
                throw new ArgumentException("The semantic request ticket is outside its bounds.");
            var ticket = new CommanderRequestTicket(nextRequestId++, this, input, generation, isCurrent);
            TraceRequest(ticket, "interpretation-started");
            return ticket;
        }

        internal void TraceRequest(CommanderRequestTicket ticket, string stage)
        {
            if (RequestTracingEnabled && ticket != null && ReferenceEquals(ticket.Owner, this))
                UnityEngine.Debug.Log($"[Commander] request={ticket.Id};stage={stage};owner={playerId};generation={ticket.Generation}");
        }

        internal void TraceActionPlan(CommanderActionPlanCandidate candidate, string stage)
        {
            if (RequestTracingEnabled && candidate != null && ReferenceEquals(candidate.Owner, this))
                UnityEngine.Debug.Log($"[Commander] request={candidate.RequestId};stage={stage};owner={playerId};generation={candidate.Generation};evidence={candidate.AuthorizationEvidence}");
        }

        // Assembly-internal typed control input, not a provider root list or English
        // entailment oracle. Provider-added prerequisites are not admitted here: the
        // existing game-side planners derive legitimate preparation after commitment.
        internal CommanderActionPlanCandidate PrepareGroundedActionPlan(
            CommanderSemanticResult interpretation, IReadOnlyList<CommanderIntent> independentlyTypedRoots,
            string originalInput, int generation)
        {
            ThrowIfDisposed();
            if (independentlyTypedRoots == null || independentlyTypedRoots.Count < 1
                || independentlyTypedRoots.Count > CommanderSemanticGraphAdmission.MaximumNodes)
                throw new ArgumentException("The trusted typed root scope is unavailable.");
            var roots = new CommanderIntent[independentlyTypedRoots.Count];
            var validator = new CommanderIntentValidator();
            for (int i = 0; i < roots.Length; i++)
            {
                roots[i] = independentlyTypedRoots[i];
                if (roots[i] is WatchFutureUnitsIntent)
                    throw new ArgumentException("A finite future-unit watcher requires its visible action-plan preview and player confirmation.");
                var validation = validator.Validate(roots[i], simulation, playerId);
                if (!validation.IsValid) throw new ArgumentException(validation.Reason);
            }
            var candidate = PrepareActionPlan(interpretation, originalInput, generation);
            var graph = candidate.Graph;
            if (graph.DynamicProgram != null || graph.Nodes.Count != roots.Length)
                throw new ArgumentException("The interpretation exceeds the independently fixed typed effect scope.");
            for (int i = 0; i < roots.Length; i++)
            {
                var effect = graph.Nodes[i];
                if (effect.DependsOn.Count != 0 || effect.ProducerFromNode.HasValue || effect.ResultFromNode.HasValue
                    || effect.Intent.GetType() != roots[i].GetType()
                    || !CommanderScopeEquivalence.SameIntent(effect.Intent, roots[i]))
                    throw new ArgumentException("The interpretation changed a trusted root, constraint or binding.");
            }
            candidate.SetTrustedTypedRoots(this, Array.AsReadOnly(roots));
            if (ApproveActionPlan(candidate, generation) == null)
                throw new ArgumentException("The grounded candidate is stale or unavailable.");
            return candidate;
        }

        internal CommanderActionPlanCandidate PrepareActionPlan(CommanderSemanticResult interpretation,
            string originalInput, int generation, StrategicPipeline strategicSource = null,
            Func<bool> isCurrentRequest = null, CommanderRequestTicket requestTicket = null)
        {
            ThrowIfDisposed();
            if (nextRequestId == long.MaxValue)
                throw new InvalidOperationException("Commander request identity space is exhausted.");
            if (strategicSource != null && (!strategicSource.UsesSimulation(simulation)
                || strategicSource.StrategicPlanner.PlayerId != playerId))
                throw new ArgumentException("The strategic runtime does not match this request.");
            if (!TryCompileActionPlan(interpretation, out var plan, out string reason))
                throw new ArgumentException(reason);
            if (!TryBindFutureProducers(plan, out var futureBindings, out reason))
                throw new ArgumentException(reason);
            if (requestTicket != null && !requestTicket.Claim(this, originalInput, generation))
                throw new ArgumentException("The request ticket is stale, foreign or already bound.");
            var candidate = new CommanderActionPlanCandidate(requestTicket?.Id ?? nextRequestId++, this,
                interpretation, plan, originalInput, generation, strategicSource,
                requestTicket != null ? requestTicket.IsCurrent : isCurrentRequest, futureBindings);
            TraceRequest(requestTicket, "candidate-compiled");
            return candidate;
        }

        internal CommanderActionPlanCandidate PrepareKnownIntentScope(CommanderSemanticResult interpretation,
            CommanderRequestTicket ticket, StrategicPipeline strategicSource, bool clarified)
        {
            if (interpretation?.IsValid != true || interpretation.Outcome != CommanderSemanticOutcome.Request
                || interpretation.Nodes.Count != 1 || ticket == null
                || interpretation.Nodes[0].Type == CommanderSemanticNodeType.StrategicObjective
                || interpretation.Nodes[0].Type == CommanderSemanticNodeType.WatchFutureUnits)
                throw new ArgumentException("Only the established narrow KnownIntent path can use automatic admission.");
            var candidate = PrepareActionPlan(interpretation, ticket.OriginalInput, ticket.Generation,
                strategicSource, ticket.IsCurrent, ticket);
            candidate.SetKnownIntentEvidence(this, clarified);
            if (ApproveActionPlan(candidate, ticket.Generation, strategicSource) == null)
                throw new ArgumentException("The KnownIntent request scope is stale.");
            return candidate;
        }

        internal CommanderIntentSubmission SubmitKnownScopedIntent(CommanderActionPlanCandidate scope,
            CommanderIntent intent, Func<CommanderIntentSubmission> submit)
        {
            if (pendingKnownAuthority != null || scope == null || submit == null
                || !scope.CanCommit(this, scope.Graph) || scope.Graph.DynamicProgram != null
                || scope.Graph.Nodes.Count != 1
                || (scope.AuthorizationEvidence != "KnownIntentAutomatic"
                    && scope.AuthorizationEvidence != "ClarifiedKnownIntentAutomatic")
                || !CommanderScopeEquivalence.SameIntent(intent, scope.Graph.Nodes[0].Intent))
                throw new InvalidOperationException("The narrow KnownIntent scope is unavailable or changed.");
            pendingKnownAuthority = scope;
            try { return submit(); }
            finally { pendingKnownAuthority = null; }
        }

        private void BindKnownAuthorityAtRegistration(CommanderGoal goal)
        {
            var scope = pendingKnownAuthority;
            if (scope == null) return;
            var intent = scope.Graph.Nodes[0].Intent;
            bool matches = goal.PlayerId == intent.PlayerId && (
                intent is EnsureUnitCountIntent unit && goal is EnsureUnitCountGoal produced
                    && produced.RequestedUnitType == unit.UnitType && produced.TargetTotal == unit.TargetTotal
                    && produced.IsExplicitNewProduction == unit.NewProductionCount.HasValue
                    && (!unit.NewProductionCount.HasValue || produced.HasResultConsumer
                        && produced.RequiredNewProductionCount == unit.NewProductionCount.Value)
                || intent is BuildStructureIntent build && goal is BuildStructureGoal structure
                    && structure.StructureType == build.StructureType && structure.Count == build.Count
                    && structure.PlacementAnchorSelector == build.PlacementAnchorSelector
                    && structure.PlacementAnchorOrdinal == build.PlacementAnchorOrdinal
                    && structure.PlacementRelation == build.PlacementRelation
                    && structure.PlacementResourceType == build.PlacementResourceType
                    && structure.PlacementSourceKind == build.PlacementSourceKind
                    && structure.ClearGapTiles == (build.ClearGapTiles ?? 1)
                || intent is AllocateWorkersIntent allocation && goal is AllocateWorkersGoal assigned
                    && CommanderScopeEquivalence.SameIntent(new AllocateWorkersIntent(playerId, assigned.Allocation, intent.Constraints), allocation)
                || intent is SetResourceAllocationIntent resources && goal is ResourceAllocationGoal gathering
                    && gathering.Resource == resources.Resource
                    && gathering.TargetWorkers == (resources.Mode == ResourceAllocationMode.Increase
                        ? planner.CountResourceWorkers(playerId, resources.Resource) + (resources.WorkerCount ?? 1)
                        : resources.WorkerCount)
                || intent is ReachAgeIntent age && goal is ReachAgeGoal reached
                    && reached.RequestedTarget == age.RequestedTarget && reached.TargetAge == age.TargetAge
                || intent is CapabilityActionIntent action && goal is CommanderCapabilityGoal capability
                    && CommanderScopeEquivalence.SameIntent(capability.Action, action));
            if (!matches || !scope.Consume(this, scope.Graph))
                throw new InvalidOperationException("The registered goal changed the admitted KnownIntent scope.");
            goal.RequestAuthority = scope;
            goal.RequestNodeIndex = 0;
            TraceActionPlan(scope, "goal-admitted");
        }

        internal CommanderActionPlanCandidate SingleLiveConfirmedActionPlan(out bool ambiguous)
        {
            ambiguous = false;
            CommanderActionPlanCandidate selected = null;
            foreach (var goal in activeGoals)
            {
                var scope = goal.RequestAuthority;
                if (goal.IsTerminal || scope == null || scope.Cancelled
                    || scope.AuthorizationEvidence == "KnownIntentAutomatic"
                    || scope.AuthorizationEvidence == "ClarifiedKnownIntentAutomatic") continue;
                if (selected != null && !ReferenceEquals(selected, scope)) { ambiguous = true; return null; }
                selected = scope;
            }
            return selected;
        }

        internal bool CancelActionPlan(CommanderActionPlanCandidate scope)
        {
            ThrowIfDisposed();
            if (scope == null || !ReferenceEquals(scope.Owner, this)
                || !ReferenceEquals(scope.Runtime, simulation)) return false;
            scope.Cancel();
            var affected = new List<int>();
            foreach (var goal in activeGoals)
                if (!goal.IsTerminal && ReferenceEquals(goal.RequestAuthority, scope)) affected.Add(goal.GoalId);
            foreach (int id in affected)
            {
                try { CancelGoal(id); }
                catch (Exception error)
                {
                    // Cancellation is already committed. One observer cannot prevent
                    // cancellation/release of another root in this exact request.
                    UnityEngine.Debug.LogWarning("[Commander] Request cancellation observer failed; type=" + error.GetType().Name);
                }
            }
            return affected.Count > 0;
        }

        // Trusted local confirmation callback. Nothing in provider JSON can invoke it.
        internal CommanderSemanticGraphPlan ApproveActionPlan(CommanderActionPlanCandidate candidate,
            int generation, StrategicPipeline strategicSource = null)
        {
            ThrowIfDisposed();
            return candidate != null && candidate.TryApprove(this, generation, strategicSource)
                ? candidate.Graph : null;
        }

        internal bool TryCompileActionPlan(CommanderSemanticResult interpretation,
            out CommanderSemanticGraphPlan plan, out string reason)
        {
            if (interpretation?.IsValid == true && interpretation.Outcome == CommanderSemanticOutcome.DynamicPlan)
                return CommanderDynamicCompiler.TryCompile(interpretation.DynamicPlan, simulation, this,
                    out plan, out reason);
            return CommanderSemanticGraphAdmission.TryAdmit(interpretation,
                new CommanderContextBuilder().Build(simulation, this), out plan, out reason);
        }
    }
}
