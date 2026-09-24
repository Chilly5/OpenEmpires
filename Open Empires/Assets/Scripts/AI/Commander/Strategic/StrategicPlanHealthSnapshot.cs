using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json;

namespace OpenEmpires
{
    public enum StrategicPlanHealthCategory
    {
        Unknown, Healthy, Paused, WaitingForResources, WaitingForPopulation,
        WaitingForPrerequisite, WaitingForConstruction, WaitingForProduction,
        TemporarilyBlocked, Completed, Cancelled, Failed
    }

    public sealed class StrategicPlanHealthResource
    {
        public ResourceType ResourceType { get; }
        public int PlanBudget { get; }
        public bool MilestoneRequirementKnown { get; }
        public int MilestoneRequirement { get; }
        public int Owned { get; }
        public int MilestoneDeficit { get; }
        public int PlanActiveReservation { get; }
        public int GlobalActiveReservation { get; }
        public int Available { get; }

        internal StrategicPlanHealthResource(ResourceType type, int budget, bool known,
            int requirement, int owned, int deficit, int planReserved, int globalReserved)
        {
            if (!Enum.IsDefined(typeof(ResourceType), type) || budget < 0 || requirement < 0
                || owned < 0 || deficit < 0 || planReserved < 0 || globalReserved < 0)
                throw new ArgumentOutOfRangeException(nameof(type));
            ResourceType = type; PlanBudget = budget; MilestoneRequirementKnown = known;
            MilestoneRequirement = known ? requirement : 0; Owned = owned;
            MilestoneDeficit = known ? deficit : 0; PlanActiveReservation = planReserved;
            GlobalActiveReservation = globalReserved;
            Available = Math.Max(0, owned - globalReserved);
        }
    }

    public sealed class StrategicPlanHealthChild
    {
        public int GoalId { get; }
        public bool CompletedHistorically { get; }
        public bool Retained { get; }
        public CommanderGoalStatus? FineStatus { get; }
        public StrategicPlanHealthCategory Category { get; }
        public int? BlockedDurationTicks { get; }
        public int? RequestedUnitType { get; }
        public int? ResolvedUnitType { get; }
        public int? TargetUnits { get; }
        public int? OwnedLivingUnits { get; }
        public int? MatchingQueuedUnits { get; }
        public int? RemainingOrders { get; }

        internal StrategicPlanHealthChild(int id, bool completed, bool retained,
            CommanderGoalStatus? status, StrategicPlanHealthCategory category, int? blockedDuration,
            int? requested, int? resolved, int? target, int? owned, int? queued, int? remaining)
        {
            if (id < 1 || blockedDuration < 0 || target < 0 || owned < 0 || queued < 0)
                throw new ArgumentOutOfRangeException(nameof(id));
            GoalId = id; CompletedHistorically = completed; Retained = retained;
            FineStatus = status; Category = category; BlockedDurationTicks = blockedDuration;
            RequestedUnitType = requested; ResolvedUnitType = resolved; TargetUnits = target;
            OwnedLivingUnits = owned; MatchingQueuedUnits = queued; RemainingOrders = remaining;
        }
    }

    public sealed class StrategicPlanHealthStatusCount
    {
        public CommanderGoalStatus? Status { get; }
        public int Count { get; }
        internal StrategicPlanHealthStatusCount(CommanderGoalStatus? status, int count)
        {
            if (status.HasValue && !Enum.IsDefined(typeof(CommanderGoalStatus), status.Value))
                throw new ArgumentOutOfRangeException(nameof(status));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            Status = status; Count = count;
        }
    }

    public sealed class StrategicPlanHealthSnapshot
    {
        private const int MaxMilestoneNameLength = 96;
        public int PlayerId { get; }
        public int PlanId { get; }
        public int CreatedTick { get; }
        public int Revision { get; }
        public int ObservedTick { get; }
        public StrategicPlanType PlanType { get; }
        public StrategicPlanStatus PlanStatus { get; }
        public int? MilestoneId { get; }
        public string MilestoneName { get; }
        public StrategicMilestoneStatus? MilestoneStatus { get; }
        public int CompletedMilestones { get; }
        public int TotalMilestones { get; }
        public int RequiredChildGoals { get; }
        public int CompletedChildGoals { get; }
        public int RetainedChildGoals { get; }
        public int MissingHistoryCount { get; }
        public int Population { get; }
        public int PopulationCap { get; }
        public int MaximumPopulation { get; }
        public int AllQueuedUnits { get; }
        public int OwnedProductionBuildings { get; }
        public StrategicPlanHealthCategory PrimaryHealthCategory { get; }
        public IReadOnlyList<StrategicPlanHealthCategory> SecondaryHealthCategories { get; }
        public IReadOnlyList<StrategicPlanHealthResource> Resources { get; }
        public IReadOnlyList<StrategicPlanHealthChild> Children { get; }
        public IReadOnlyList<StrategicPlanHealthStatusCount> RetainedChildStatusCounts { get; }

        internal StrategicPlanHealthSnapshot(StrategicPlan plan, StrategicMilestone milestone,
            int tick, int completedMilestones, int retained, int missing, CommanderContext context,
            int allQueued, StrategicPlanHealthCategory primary,
            List<StrategicPlanHealthCategory> secondary,
            List<StrategicPlanHealthResource> resources, List<StrategicPlanHealthChild> children,
            List<StrategicPlanHealthStatusCount> retainedCounts)
        {
            PlayerId = plan.OwnerPlayerId; PlanId = plan.StrategicPlanId;
            CreatedTick = plan.CreatedTick; Revision = plan.Revision; ObservedTick = tick;
            PlanType = plan.PlanType; PlanStatus = plan.Status;
            MilestoneId = milestone?.MilestoneId;
            MilestoneName = BoundMilestoneName(milestone?.Name);
            MilestoneStatus = milestone?.Status; CompletedMilestones = completedMilestones;
            TotalMilestones = plan.Milestones.Count;
            RequiredChildGoals = milestone?.RequiredChildGoals.Count ?? 0;
            CompletedChildGoals = milestone?.CompletedChildGoals.Count ?? 0;
            RetainedChildGoals = retained; MissingHistoryCount = missing;
            Population = context.Population; PopulationCap = context.PopulationCap;
            MaximumPopulation = context.MaximumPopulation; AllQueuedUnits = allQueued;
            OwnedProductionBuildings = context.Production.Count;
            PrimaryHealthCategory = primary;
            SecondaryHealthCategories = new ReadOnlyCollection<StrategicPlanHealthCategory>(
                new List<StrategicPlanHealthCategory>(secondary));
            Resources = new ReadOnlyCollection<StrategicPlanHealthResource>(
                new List<StrategicPlanHealthResource>(resources));
            Children = new ReadOnlyCollection<StrategicPlanHealthChild>(
                new List<StrategicPlanHealthChild>(children));
            RetainedChildStatusCounts = new ReadOnlyCollection<StrategicPlanHealthStatusCount>(
                new List<StrategicPlanHealthStatusCount>(retainedCounts));
        }

        private static string BoundMilestoneName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            if (name.Length <= MaxMilestoneNameLength) return name;
            int length = MaxMilestoneNameLength;
            if (char.IsHighSurrogate(name[length - 1])) length--;
            return name.Substring(0, length);
        }

        public string ToJson() => JsonConvert.SerializeObject(new
        {
            PlayerId, PlanId, CreatedTick, Revision, ObservedTick, PlanType, PlanStatus,
            MilestoneId, MilestoneName, MilestoneStatus, CompletedMilestones, TotalMilestones,
            RequiredChildGoals, CompletedChildGoals, RetainedChildGoals, MissingHistoryCount,
            Population, PopulationCap, MaximumPopulation, AllQueuedUnits,
            OwnedProductionBuildings, PrimaryHealthCategory, SecondaryHealthCategories,
            Resources = SerializeResources(), Children = SerializeChildren(),
            RetainedChildStatusCounts = SerializeStatusCounts()
        }, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None });

        private object[] SerializeResources()
        {
            var result = new object[Resources.Count];
            for (int i = 0; i < result.Length; i++)
            {
                StrategicPlanHealthResource r = Resources[i];
                result[i] = new { r.ResourceType, r.PlanBudget, r.MilestoneRequirementKnown,
                    r.MilestoneRequirement, r.Owned, r.MilestoneDeficit, r.PlanActiveReservation,
                    r.GlobalActiveReservation, r.Available };
            }
            return result;
        }

        private object[] SerializeChildren()
        {
            var result = new object[Children.Count];
            for (int i = 0; i < result.Length; i++)
            {
                StrategicPlanHealthChild c = Children[i];
                result[i] = new { c.GoalId, c.CompletedHistorically, c.Retained,
                    c.FineStatus, c.Category, c.BlockedDurationTicks, c.RequestedUnitType,
                    c.ResolvedUnitType, c.TargetUnits, c.OwnedLivingUnits,
                    c.MatchingQueuedUnits, c.RemainingOrders };
            }
            return result;
        }

        private object[] SerializeStatusCounts()
        {
            var result = new object[RetainedChildStatusCounts.Count];
            for (int i = 0; i < result.Length; i++)
            {
                StrategicPlanHealthStatusCount count = RetainedChildStatusCounts[i];
                result[i] = new { count.Status, count.Count };
            }
            return result;
        }
    }
}
