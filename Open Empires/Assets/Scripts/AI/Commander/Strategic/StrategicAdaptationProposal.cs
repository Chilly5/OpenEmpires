using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace OpenEmpires
{
    // All values in this file are advisory copies. None can submit, reserve, or execute.
    public enum StrategicAdaptationReason { MaterialStrategicRequest }
    public enum StrategicAdaptationRequestKind { WorkerAllocation, BuildStructure, EnsureUnitCount }

    public sealed class StrategicAdaptationRequest
    {
        public StrategicAdaptationRequestKind Kind { get; }
        public ResourceType? ResourceType { get; }
        public int? WorkerTarget { get; }
        public BuildingType? StructureType { get; }
        public int? Count { get; }
        public bool? EnsureExisting { get; }
        public bool? SkipIfAgeUnavailable { get; }
        public int? UnitType { get; }
        public int? TargetTotal { get; }
        public int? MaximumQueue { get; }

        internal StrategicAdaptationRequest(StrategicResourceAllocationGoalRequest request)
        {
            Kind = StrategicAdaptationRequestKind.WorkerAllocation;
            ResourceType = request.ResourceType;
            WorkerTarget = request.WorkerTarget;
        }

        internal StrategicAdaptationRequest(StrategicBuildStructureGoalRequest request)
        {
            Kind = StrategicAdaptationRequestKind.BuildStructure;
            StructureType = request.StructureType;
            Count = request.Count;
            EnsureExisting = request.EnsureExisting;
            SkipIfAgeUnavailable = request.SkipIfAgeUnavailable;
        }

        internal StrategicAdaptationRequest(StrategicEnsureUnitCountGoalRequest request)
        {
            Kind = StrategicAdaptationRequestKind.EnsureUnitCount;
            UnitType = request.UnitType;
            TargetTotal = request.TargetTotal;
            MaximumQueue = request.MaximumQueue;
        }
    }

    public sealed class StrategicAdaptationMilestone
    {
        public int MilestoneId { get; }
        public int OrderIndex { get; }
        public IReadOnlyList<StrategicAdaptationRequest> Requests { get; }

        internal StrategicAdaptationMilestone(StrategicMilestone milestone,
            IReadOnlyList<StrategicAdaptationRequest> requests)
        {
            MilestoneId = milestone.MilestoneId;
            OrderIndex = milestone.OrderIndex;
            Requests = Array.AsReadOnly(requests.ToArray());
        }
    }

    public sealed class StrategicAdaptationResourceAmount
    {
        public ResourceType ResourceType { get; }
        public int Amount { get; }

        internal StrategicAdaptationResourceAmount(ResourceType type, int amount)
        {
            ResourceType = type;
            Amount = amount;
        }
    }

    public sealed class StrategicAdaptationBlockerChild
    {
        public int GoalId { get; }
        public StrategicPlanHealthCategory Category { get; }
        public CommanderGoalStatus? FineStatus { get; }

        internal StrategicAdaptationBlockerChild(StrategicPlanHealthChild child)
        {
            GoalId = child.GoalId;
            Category = child.Category;
            FineStatus = child.FineStatus;
        }
    }

    public sealed class StrategicAdaptationQuote
    {
        public StrategicObjectiveType Objective { get; }
        public bool Capable { get; }
        public string RejectionReason { get; }
        public IReadOnlyList<StrategicAdaptationResourceAmount> Costs { get; }

        internal StrategicAdaptationQuote(StrategicFeasibility quote)
        {
            Objective = quote.Objective;
            Capable = quote.Capable;
            RejectionReason = StrategicAdaptationProposalBuilder.Bound(quote.RejectionReason, 160);
            Costs = Array.AsReadOnly(quote.Costs.Select(c =>
                new StrategicAdaptationResourceAmount(c.ResourceType, c.Amount))
                .OrderBy(c => c.ResourceType).ToArray());
        }
    }

    // Capture is synchronous. A host must obtain plan and fresh owner-scoped health before
    // awaiting a provider, and retain this value rather than either live input object.
    public sealed class StrategicAdaptationSource
    {
        public int OwnerPlayerId { get; }
        public int PlanId { get; }
        public int CreatedTick { get; }
        public int Revision { get; }
        public StrategicPlanStatus Status { get; }
        public StrategicPlanType PlanType { get; }
        public int MilestoneId { get; }
        public StrategicMilestoneStatus MilestoneStatus { get; }
        public StrategicPlanHealthCategory BlockerCategory { get; }
        public IReadOnlyList<StrategicAdaptationResourceAmount> BlockerDeficits { get; }
        public IReadOnlyList<StrategicAdaptationBlockerChild> BlockerChildren { get; }
        public IReadOnlyList<StrategicAdaptationMilestone> Targets { get; }
        public IReadOnlyList<StrategicAdaptationResourceAmount> CanonicalBudget { get; }

        internal StrategicAdaptationSource(StrategicPlan plan, StrategicPlanHealthSnapshot health,
            IReadOnlyList<StrategicAdaptationMilestone> targets,
            IReadOnlyList<StrategicAdaptationResourceAmount> budget,
            IReadOnlyList<StrategicAdaptationResourceAmount> deficits,
            IReadOnlyList<StrategicAdaptationBlockerChild> children)
        {
            OwnerPlayerId = health.PlayerId;
            PlanId = health.PlanId;
            CreatedTick = health.CreatedTick;
            Revision = health.Revision;
            Status = health.PlanStatus;
            PlanType = health.PlanType;
            MilestoneId = health.MilestoneId.Value;
            MilestoneStatus = health.MilestoneStatus.Value;
            BlockerCategory = health.PrimaryHealthCategory;
            Targets = Array.AsReadOnly(targets.ToArray());
            CanonicalBudget = Array.AsReadOnly(budget.ToArray());
            BlockerDeficits = Array.AsReadOnly(deficits.ToArray());
            BlockerChildren = Array.AsReadOnly(children.ToArray());
        }
    }

    public sealed class StrategicAdaptationProposal
    {
        public StrategicAdaptationSource Source { get; }
        public int OwnerPlayerId => Source.OwnerPlayerId;
        public int SourcePlanId => Source.PlanId;
        public int SourceCreatedTick => Source.CreatedTick;
        public int SourceRevision => Source.Revision;
        public StrategicPlanStatus SourceStatus => Source.Status;
        public int SourceMilestoneId => Source.MilestoneId;
        public StrategicMilestoneStatus SourceMilestoneStatus => Source.MilestoneStatus;
        public StrategicPlanHealthCategory BlockerCategory => Source.BlockerCategory;
        public IReadOnlyList<StrategicAdaptationResourceAmount> BlockerDeficits => Source.BlockerDeficits;
        public IReadOnlyList<StrategicAdaptationBlockerChild> BlockerChildren => Source.BlockerChildren;
        public StrategicObjectiveType CurrentObjective { get; }
        public StrategicObjectiveType ProposedObjective { get; }
        public int PendingIntentId { get; }
        public IReadOnlyList<StrategicAdaptationMilestone> OldTargets => Source.Targets;
        public IReadOnlyList<StrategicAdaptationMilestone> ProposedTargets { get; }
        public IReadOnlyList<StrategicAdaptationResourceAmount> OldCanonicalBudget => Source.CanonicalBudget;
        public StrategicAdaptationQuote ProposedFeasibilityQuote { get; }
        public StrategicAdaptationReason Reason { get; }
        public string Explanation { get; }
        public bool RequiresPlayerApproval => true;

        internal StrategicAdaptationProposal(StrategicAdaptationSource source,
            StrategicObjectiveType proposed, int pendingId,
            IReadOnlyList<StrategicAdaptationMilestone> proposedTargets,
            StrategicAdaptationQuote quote, string explanation)
        {
            Source = source;
            CurrentObjective = StrategicAdaptationProposalBuilder.ObjectiveFor(source.PlanType);
            ProposedObjective = proposed;
            PendingIntentId = pendingId;
            ProposedTargets = Array.AsReadOnly(proposedTargets.ToArray());
            ProposedFeasibilityQuote = quote;
            Reason = StrategicAdaptationReason.MaterialStrategicRequest;
            Explanation = StrategicAdaptationProposalBuilder.Bound(explanation, 256);
        }

        public string ToJson() => JsonConvert.SerializeObject(new
        {
            OwnerPlayerId, SourcePlanId, SourceCreatedTick, SourceRevision,
            SourceStatus = SourceStatus.ToString(), SourceMilestoneId,
            SourceMilestoneStatus = SourceMilestoneStatus.ToString(),
            BlockerCategory = BlockerCategory.ToString(),
            BlockerDeficits = ResourceJson(BlockerDeficits),
            BlockerChildren = BlockerChildren.Select(c => new
            {
                c.GoalId, Category = c.Category.ToString(),
                FineStatus = c.FineStatus?.ToString()
            }).ToArray(),
            CurrentObjective = CurrentObjective.ToString(),
            ProposedObjective = ProposedObjective.ToString(), PendingIntentId,
            OldTargets = TargetJson(OldTargets), ProposedTargets = TargetJson(ProposedTargets),
            OldCanonicalBudget = ResourceJson(OldCanonicalBudget),
            ProposedFeasibilityQuote = ProposedFeasibilityQuote == null ? null : new
            {
                Objective = ProposedFeasibilityQuote.Objective.ToString(),
                ProposedFeasibilityQuote.Capable, ProposedFeasibilityQuote.RejectionReason,
                Costs = ResourceJson(ProposedFeasibilityQuote.Costs)
            },
            Reason = Reason.ToString(), Explanation, RequiresPlayerApproval
        }, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None });

        private static object[] ResourceJson(IReadOnlyList<StrategicAdaptationResourceAmount> values)
        {
            var result = new object[values.Count];
            for (int i = 0; i < result.Length; i++)
                result[i] = new { ResourceType = values[i].ResourceType.ToString(), values[i].Amount };
            return result;
        }

        private static object[] TargetJson(IReadOnlyList<StrategicAdaptationMilestone> values)
        {
            var result = new object[values.Count];
            for (int i = 0; i < result.Length; i++)
            {
                StrategicAdaptationMilestone milestone = values[i];
                var requests = new object[milestone.Requests.Count];
                for (int j = 0; j < requests.Length; j++)
                {
                    StrategicAdaptationRequest r = milestone.Requests[j];
                    requests[j] = new
                    {
                        Kind = r.Kind.ToString(),
                        ResourceType = r.ResourceType?.ToString(), r.WorkerTarget,
                        StructureType = r.StructureType?.ToString(), r.Count,
                        r.EnsureExisting, r.SkipIfAgeUnavailable, r.UnitType,
                        r.TargetTotal, r.MaximumQueue
                    };
                }
                result[i] = new { milestone.MilestoneId, milestone.OrderIndex, Requests = requests };
            }
            return result;
        }
    }

    public static class StrategicAdaptationProposalBuilder
    {
        public static StrategicAdaptationSource Capture(int trustedOwnerPlayerId,
            StrategicPlan plan, StrategicPlanHealthSnapshot health)
        {
            if (plan == null || health == null || trustedOwnerPlayerId < 0
                || plan.OwnerPlayerId != trustedOwnerPlayerId
                || health.PlayerId != trustedOwnerPlayerId
                || health.PlanId < 1 || plan.StrategicPlanId != health.PlanId
                || plan.CreatedTick != health.CreatedTick || plan.Revision != health.Revision
                || plan.Status != StrategicPlanStatus.Active
                || health.PlanStatus != StrategicPlanStatus.Active
                || !Enum.IsDefined(typeof(StrategicPlanType), plan.PlanType)
                || plan.PlanType != health.PlanType || plan.CurrentMilestone == null
                || !health.MilestoneId.HasValue || !health.MilestoneStatus.HasValue
                || plan.CurrentMilestone.MilestoneId != health.MilestoneId.Value
                || plan.CurrentMilestone.Status != health.MilestoneStatus.Value
                || !Enum.IsDefined(typeof(StrategicMilestoneStatus), health.MilestoneStatus.Value)
                || !Enum.IsDefined(typeof(StrategicPlanHealthCategory), health.PrimaryHealthCategory))
                return null;

            int id = plan.StrategicPlanId, tick = plan.CreatedTick, revision = plan.Revision;
            int milestoneId = plan.CurrentMilestone.MilestoneId;
            StrategicMilestoneStatus milestoneStatus = plan.CurrentMilestone.Status;
            if (!CopyTargets(plan, out List<StrategicAdaptationMilestone> targets)
                || plan.BudgetRequirements.Count > 16 || health.Resources.Count > 16
                || health.RequiredChildGoals > 128 || health.Children.Count != health.RequiredChildGoals)
                return null;
            var budget = new List<StrategicAdaptationResourceAmount>();
            var budgetTypes = new HashSet<ResourceType>();
            foreach (StrategicBudgetRequirement value in plan.BudgetRequirements)
            {
                if (!Enum.IsDefined(typeof(ResourceType), value.ResourceType)
                    || value.Amount < 0 || !budgetTypes.Add(value.ResourceType)) return null;
                budget.Add(new StrategicAdaptationResourceAmount(value.ResourceType, value.Amount));
            }
            budget.Sort((a, b) => a.ResourceType.CompareTo(b.ResourceType));
            var deficits = new List<StrategicAdaptationResourceAmount>();
            var deficitTypes = new HashSet<ResourceType>();
            foreach (StrategicPlanHealthResource value in health.Resources)
            {
                if (value == null || !Enum.IsDefined(typeof(ResourceType), value.ResourceType)
                    || !deficitTypes.Add(value.ResourceType)) return null;
                if (value.MilestoneDeficit > 0)
                    deficits.Add(new StrategicAdaptationResourceAmount(value.ResourceType,
                        value.MilestoneDeficit));
            }
            deficits.Sort((a, b) => a.ResourceType.CompareTo(b.ResourceType));
            var children = new List<StrategicAdaptationBlockerChild>();
            int lastGoalId = 0;
            foreach (StrategicPlanHealthChild child in health.Children)
            {
                if (child == null || child.GoalId <= lastGoalId
                    || !Enum.IsDefined(typeof(StrategicPlanHealthCategory), child.Category)
                    || child.FineStatus.HasValue
                        && !Enum.IsDefined(typeof(CommanderGoalStatus), child.FineStatus.Value))
                    return null;
                children.Add(new StrategicAdaptationBlockerChild(child));
                lastGoalId = child.GoalId;
            }
            if (plan.StrategicPlanId != id || plan.CreatedTick != tick || plan.Revision != revision
                || plan.Status != StrategicPlanStatus.Active || plan.OwnerPlayerId != trustedOwnerPlayerId
                || plan.CurrentMilestone?.MilestoneId != milestoneId
                || plan.CurrentMilestone.Status != milestoneStatus) return null;
            return new StrategicAdaptationSource(plan, health, targets, budget, deficits, children);
        }

        public static StrategicAdaptationProposal Build(StrategicAdaptationSource source,
            StrategicIntent pendingIntent, StrategicFeasibility quote, string explanation)
        {
            if (source == null || pendingIntent == null || source.Status != StrategicPlanStatus.Active
                || pendingIntent.PlayerId != source.OwnerPlayerId || pendingIntent.IntentId < 1
                || pendingIntent.Status != StrategicIntentStatus.Created
                || pendingIntent.Priority.HasValue
                || pendingIntent.Parameters.Count != 0
                    && !(pendingIntent.ObjectiveType == StrategicObjectiveType.AttackPreparation
                        && pendingIntent.Parameters.Count == 1
                        && pendingIntent.Parameters.TryGetValue("focus", out string focus)
                        && string.Equals(focus, "cavalry", StringComparison.Ordinal))
                || !Enum.IsDefined(typeof(StrategicObjectiveType), pendingIntent.ObjectiveType)
                || quote != null && (quote.Objective != pendingIntent.ObjectiveType
                    || quote.Costs == null || quote.Costs.Count > 16)) return null;
            try
            {
                var registry = StrategicPlanRegistry.CreateDefault();
                if (!new StrategicIntentValidator().Validate(pendingIntent,
                    source.OwnerPlayerId, registry).IsValid) return null;
                StrategicPlan template = registry.CreatePlan(pendingIntent);
                if (!CopyTargets(template, out List<StrategicAdaptationMilestone> targets)) return null;
                if (quote != null)
                {
                    var quoteTypes = new HashSet<ResourceType>();
                    foreach (StrategicRequirementState cost in quote.Costs)
                        if (cost == null || !Enum.IsDefined(typeof(ResourceType), cost.ResourceType)
                            || cost.Amount < 0 || !quoteTypes.Add(cost.ResourceType)) return null;
                }
                return new StrategicAdaptationProposal(source, pendingIntent.ObjectiveType,
                    pendingIntent.IntentId, targets,
                    quote == null ? null : new StrategicAdaptationQuote(quote), explanation);
            }
            catch (ArgumentException) { return null; }
            catch (InvalidOperationException) { return null; }
            catch (OverflowException) { return null; }
        }

        public static bool IsFresh(StrategicAdaptationProposal proposal,
            StrategicAdaptationSource fresh)
        {
            if (proposal == null || fresh == null || fresh.Status != StrategicPlanStatus.Active
                || proposal.OwnerPlayerId != fresh.OwnerPlayerId
                || proposal.SourcePlanId != fresh.PlanId
                || proposal.SourceCreatedTick != fresh.CreatedTick
                || proposal.SourceRevision != fresh.Revision
                || proposal.SourceStatus != fresh.Status
                || proposal.SourceMilestoneId != fresh.MilestoneId
                || proposal.SourceMilestoneStatus != fresh.MilestoneStatus
                || proposal.BlockerCategory != fresh.BlockerCategory
                || !SameResources(proposal.OldCanonicalBudget, fresh.CanonicalBudget)
                || !SameTargets(proposal.OldTargets, fresh.Targets)
                || proposal.BlockerDeficits.Count != fresh.BlockerDeficits.Count
                || proposal.BlockerChildren.Count != fresh.BlockerChildren.Count) return false;
            for (int i = 0; i < fresh.BlockerDeficits.Count; i++)
                if (proposal.BlockerDeficits[i].ResourceType != fresh.BlockerDeficits[i].ResourceType
                    || proposal.BlockerDeficits[i].Amount != fresh.BlockerDeficits[i].Amount)
                    return false;
            for (int i = 0; i < fresh.BlockerChildren.Count; i++)
                if (proposal.BlockerChildren[i].GoalId != fresh.BlockerChildren[i].GoalId
                    || proposal.BlockerChildren[i].Category != fresh.BlockerChildren[i].Category
                    || proposal.BlockerChildren[i].FineStatus != fresh.BlockerChildren[i].FineStatus)
                    return false;
            return true;
        }

        private static bool SameResources(IReadOnlyList<StrategicAdaptationResourceAmount> a,
            IReadOnlyList<StrategicAdaptationResourceAmount> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].ResourceType != b[i].ResourceType || a[i].Amount != b[i].Amount)
                    return false;
            return true;
        }

        private static bool SameTargets(IReadOnlyList<StrategicAdaptationMilestone> a,
            IReadOnlyList<StrategicAdaptationMilestone> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].MilestoneId != b[i].MilestoneId || a[i].OrderIndex != b[i].OrderIndex
                    || a[i].Requests.Count != b[i].Requests.Count) return false;
                for (int j = 0; j < a[i].Requests.Count; j++)
                {
                    StrategicAdaptationRequest left = a[i].Requests[j];
                    StrategicAdaptationRequest right = b[i].Requests[j];
                    if (left.Kind != right.Kind || left.ResourceType != right.ResourceType
                        || left.WorkerTarget != right.WorkerTarget
                        || left.StructureType != right.StructureType || left.Count != right.Count
                        || left.EnsureExisting != right.EnsureExisting
                        || left.SkipIfAgeUnavailable != right.SkipIfAgeUnavailable
                        || left.UnitType != right.UnitType || left.TargetTotal != right.TargetTotal
                        || left.MaximumQueue != right.MaximumQueue) return false;
                }
            }
            return true;
        }

        internal static StrategicObjectiveType ObjectiveFor(StrategicPlanType type) => type switch
        {
            StrategicPlanType.CavalryPressure => StrategicObjectiveType.AttackPreparation,
            StrategicPlanType.DefensivePreparation => StrategicObjectiveType.DefensivePreparation,
            StrategicPlanType.EconomicExpansion => StrategicObjectiveType.EconomicExpansion,
            StrategicPlanType.MilitaryReinforcement => StrategicObjectiveType.MilitaryReinforcement,
            StrategicPlanType.RangedReinforcement => StrategicObjectiveType.RangedReinforcement,
            StrategicPlanType.DefensiveTurtle => StrategicObjectiveType.DefensiveTurtle,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        internal static string Bound(string value, int limit)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Length <= limit) return value;
            int length = char.IsHighSurrogate(value[limit - 1]) ? limit - 1 : limit;
            return value.Substring(0, length);
        }

        private static bool CopyTargets(StrategicPlan plan,
            out List<StrategicAdaptationMilestone> targets)
        {
            targets = new List<StrategicAdaptationMilestone>();
            if (plan.Milestones.Count < 1 || plan.Milestones.Count > 32) return false;
            int totalRequests = 0;
            for (int i = 0; i < plan.Milestones.Count; i++)
            {
                StrategicMilestone milestone = plan.Milestones[i];
                if (milestone == null || milestone.MilestoneId < 1 || milestone.OrderIndex != i
                    || milestone.TacticalGoals.Count > 32) return false;
                var requests = new List<StrategicAdaptationRequest>();
                foreach (StrategicTacticalGoalRequest request in milestone.TacticalGoals)
                {
                    if (++totalRequests > 128 || request == null) return false;
                    switch (request)
                    {
                        case StrategicResourceAllocationGoalRequest worker
                            when Enum.IsDefined(typeof(ResourceType), worker.ResourceType)
                                && worker.WorkerTarget >= 0 && worker.WorkerTarget <= 100000:
                            requests.Add(new StrategicAdaptationRequest(worker)); break;
                        case StrategicBuildStructureGoalRequest building
                            when Enum.IsDefined(typeof(BuildingType), building.StructureType)
                                && building.Count > 0 && building.Count <= 100000:
                            requests.Add(new StrategicAdaptationRequest(building)); break;
                        case StrategicEnsureUnitCountGoalRequest units
                            when units.UnitType >= 0 && units.TargetTotal > 0
                                && units.TargetTotal <= 100000 && units.MaximumQueue > 0
                                && units.MaximumQueue <= 100000:
                            requests.Add(new StrategicAdaptationRequest(units)); break;
                        default: return false;
                    }
                }
                targets.Add(new StrategicAdaptationMilestone(milestone, requests));
            }
            return true;
        }
    }
}
