using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace OpenEmpires
{
    public enum StrategicAdvisoryTransition
    {
        WaitingForResources, WaitingForPopulation, WaitingForPrerequisite,
        WaitingForConstruction, WaitingForProduction, TemporarilyBlocked,
        Recovered, Paused, Resumed, Completed, Cancelled, Failed
    }

    public sealed class StrategicAdvisory
    {
        public int PlayerId { get; }
        public int PlanId { get; }
        public int CreatedTick { get; }
        public int ObservedTick { get; }
        public int Revision { get; }
        public StrategicAdvisoryTransition Transition { get; }
        public string Display { get; }

        internal StrategicAdvisory(int playerId, int planId, int createdTick, int observedTick,
            int revision, StrategicAdvisoryTransition transition, string display)
        {
            PlayerId = playerId; PlanId = planId; CreatedTick = createdTick;
            ObservedTick = observedTick; Revision = revision; Transition = transition;
            Display = display ?? string.Empty;
        }
    }

    public sealed class StrategicAdvisoryFeed
    {
        private const int MaxPlans = 32;
        private const int MaxOutput = 4;
        private readonly List<PlanState> plans = new List<PlanState>();

        private struct WaitKey : IEquatable<WaitKey>
        {
            public StrategicPlanHealthCategory Category;
            public ResourceType? Resource;

            public bool Equals(WaitKey other) => Category == other.Category && Resource == other.Resource;
        }

        private sealed class PlanState
        {
            public int PlayerId, PlanId, CreatedTick, Revision, ObservedTick;
            public StrategicPlanStatus Status;
            public List<WaitKey> Waits;
        }

        public IReadOnlyList<StrategicAdvisory> Observe(StrategicPlanHealthSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (!Enum.IsDefined(typeof(StrategicPlanStatus), snapshot.PlanStatus))
                return Array.Empty<StrategicAdvisory>();

            int index = plans.FindIndex(p => p.PlayerId == snapshot.PlayerId
                && p.PlanId == snapshot.PlanId && p.CreatedTick == snapshot.CreatedTick);
            if (index >= 0 && (snapshot.Revision < plans[index].Revision
                || snapshot.ObservedTick < plans[index].ObservedTick))
                return Array.Empty<StrategicAdvisory>();

            var currentWaits = CopyWaits(snapshot);
            if (index < 0)
            {
                if (plans.Count == MaxPlans) plans.RemoveAt(0);
                plans.Add(new PlanState { PlayerId = snapshot.PlayerId, PlanId = snapshot.PlanId,
                    CreatedTick = snapshot.CreatedTick, Revision = snapshot.Revision,
                    ObservedTick = snapshot.ObservedTick, Status = snapshot.PlanStatus,
                    Waits = currentWaits });
                return Array.Empty<StrategicAdvisory>();
            }

            PlanState previous = plans[index];
            if (snapshot.PlanStatus == StrategicPlanStatus.Paused)
                currentWaits = previous.Waits;
            var result = new List<StrategicAdvisory>(MaxOutput);
            if (snapshot.PlanStatus != previous.Status)
            {
                StrategicAdvisoryTransition? statusTransition = StatusTransition(snapshot.PlanStatus,
                    previous.Status);
                if (statusTransition.HasValue)
                    Add(result, snapshot, statusTransition.Value, StatusText(snapshot.PlanStatus));
            }

            bool terminal = snapshot.PlanStatus == StrategicPlanStatus.Completed
                || snapshot.PlanStatus == StrategicPlanStatus.Cancelled
                || snapshot.PlanStatus == StrategicPlanStatus.Failed;
            bool known = Enum.IsDefined(typeof(StrategicPlanHealthCategory),
                snapshot.PrimaryHealthCategory)
                && snapshot.PrimaryHealthCategory != StrategicPlanHealthCategory.Unknown;
            if (!terminal && known && snapshot.PlanStatus != StrategicPlanStatus.Paused)
            {
                for (int i = 0; i < previous.Waits.Count && result.Count < MaxOutput; i++)
                {
                    WaitKey old = previous.Waits[i];
                    if (!currentWaits.Contains(old))
                        Add(result, snapshot, StrategicAdvisoryTransition.Recovered,
                            "Plan " + Number(snapshot.PlanId) + " recovered from "
                            + CategoryText(old.Category, old.Resource) + ".");
                }
                for (int i = 0; i < currentWaits.Count && result.Count < MaxOutput; i++)
                {
                    WaitKey next = currentWaits[i];
                    if (!previous.Waits.Contains(next))
                        Add(result, snapshot, WaitTransition(next.Category),
                            WaitText(snapshot, next));
                }
            }

            previous.Revision = snapshot.Revision;
            previous.ObservedTick = snapshot.ObservedTick;
            previous.Status = snapshot.PlanStatus;
            if (known || terminal) previous.Waits = currentWaits;
            return new ReadOnlyCollection<StrategicAdvisory>(result);
        }

        public void Reset() => plans.Clear();

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static void Add(List<StrategicAdvisory> result, StrategicPlanHealthSnapshot snapshot,
            StrategicAdvisoryTransition transition, string display)
        {
            if (result.Count >= MaxOutput) return;
            result.Add(new StrategicAdvisory(snapshot.PlayerId, snapshot.PlanId,
                snapshot.CreatedTick, snapshot.ObservedTick, snapshot.Revision,
                transition, display.Length <= 200 ? display : display.Substring(0, 200)));
        }

        private static List<WaitKey> CopyWaits(StrategicPlanHealthSnapshot snapshot)
        {
            if (!Enum.IsDefined(typeof(StrategicPlanHealthCategory),
                snapshot.PrimaryHealthCategory)
                || snapshot.PrimaryHealthCategory == StrategicPlanHealthCategory.Unknown)
                return new List<WaitKey>();
            var categories = new List<StrategicPlanHealthCategory>();
            if (Enum.IsDefined(typeof(StrategicPlanHealthCategory), snapshot.PrimaryHealthCategory)
                && snapshot.PrimaryHealthCategory != StrategicPlanHealthCategory.Unknown)
                categories.Add(snapshot.PrimaryHealthCategory);
            foreach (StrategicPlanHealthCategory category in snapshot.SecondaryHealthCategories)
                if (Enum.IsDefined(typeof(StrategicPlanHealthCategory), category)
                    && category != StrategicPlanHealthCategory.Unknown && !categories.Contains(category))
                    categories.Add(category);
            categories.Sort();

            var waits = new List<WaitKey>();
            foreach (StrategicPlanHealthCategory category in categories)
            {
                if (!IsWait(category)) continue;
                if (category == StrategicPlanHealthCategory.WaitingForResources)
                {
                    var resources = new List<ResourceType>();
                    foreach (StrategicPlanHealthResource resource in snapshot.Resources)
                        if (resource.MilestoneRequirementKnown && resource.MilestoneDeficit > 0
                            && Enum.IsDefined(typeof(ResourceType), resource.ResourceType)
                            && !resources.Contains(resource.ResourceType))
                            resources.Add(resource.ResourceType);
                    resources.Sort();
                    if (resources.Count == 0)
                        waits.Add(new WaitKey { Category = category });
                    else foreach (ResourceType resource in resources)
                        waits.Add(new WaitKey { Category = category, Resource = resource });
                }
                else waits.Add(new WaitKey { Category = category });
            }
            return waits;
        }

        private static bool IsWait(StrategicPlanHealthCategory category) =>
            category == StrategicPlanHealthCategory.WaitingForResources
            || category == StrategicPlanHealthCategory.WaitingForPopulation
            || category == StrategicPlanHealthCategory.WaitingForPrerequisite
            || category == StrategicPlanHealthCategory.WaitingForConstruction
            || category == StrategicPlanHealthCategory.WaitingForProduction
            || category == StrategicPlanHealthCategory.TemporarilyBlocked;

        private static StrategicAdvisoryTransition WaitTransition(StrategicPlanHealthCategory category)
        {
            switch (category)
            {
                case StrategicPlanHealthCategory.WaitingForResources: return StrategicAdvisoryTransition.WaitingForResources;
                case StrategicPlanHealthCategory.WaitingForPopulation: return StrategicAdvisoryTransition.WaitingForPopulation;
                case StrategicPlanHealthCategory.WaitingForPrerequisite: return StrategicAdvisoryTransition.WaitingForPrerequisite;
                case StrategicPlanHealthCategory.WaitingForConstruction: return StrategicAdvisoryTransition.WaitingForConstruction;
                case StrategicPlanHealthCategory.WaitingForProduction: return StrategicAdvisoryTransition.WaitingForProduction;
                default: return StrategicAdvisoryTransition.TemporarilyBlocked;
            }
        }

        private static StrategicAdvisoryTransition? StatusTransition(StrategicPlanStatus now,
            StrategicPlanStatus before)
        {
            switch (now)
            {
                case StrategicPlanStatus.Paused: return StrategicAdvisoryTransition.Paused;
                case StrategicPlanStatus.Completed: return StrategicAdvisoryTransition.Completed;
                case StrategicPlanStatus.Cancelled: return StrategicAdvisoryTransition.Cancelled;
                case StrategicPlanStatus.Failed: return StrategicAdvisoryTransition.Failed;
                case StrategicPlanStatus.Active:
                    return before == StrategicPlanStatus.Paused
                        ? StrategicAdvisoryTransition.Resumed : (StrategicAdvisoryTransition?)null;
                default: return null;
            }
        }

        private static string StatusText(StrategicPlanStatus status)
        {
            switch (status)
            {
                case StrategicPlanStatus.Paused: return "Plan paused.";
                case StrategicPlanStatus.Active: return "Plan resumed.";
                case StrategicPlanStatus.Completed: return "Plan completed.";
                case StrategicPlanStatus.Cancelled: return "Plan cancelled.";
                default: return "Plan failed.";
            }
        }

        private static string CategoryText(StrategicPlanHealthCategory category,
            ResourceType? resource)
        {
            switch (category)
            {
                case StrategicPlanHealthCategory.WaitingForResources:
                    return resource.HasValue ? resource.Value + " resource wait" : "resource wait";
                case StrategicPlanHealthCategory.WaitingForPopulation: return "population wait";
                case StrategicPlanHealthCategory.WaitingForPrerequisite: return "prerequisite wait";
                case StrategicPlanHealthCategory.WaitingForConstruction: return "construction wait";
                case StrategicPlanHealthCategory.WaitingForProduction: return "production wait";
                default: return "temporary block";
            }
        }

        private static string WaitText(StrategicPlanHealthSnapshot snapshot, WaitKey wait)
        {
            string display = "Plan " + Number(snapshot.PlanId) + " waiting for "
                + CategoryText(wait.Category, wait.Resource);
            if (wait.Resource.HasValue)
                foreach (StrategicPlanHealthResource resource in snapshot.Resources)
                    if (resource.ResourceType == wait.Resource.Value
                        && resource.MilestoneRequirementKnown && resource.MilestoneDeficit > 0)
                    {
                        display += ": milestone deficit " + Number(resource.MilestoneDeficit);
                        break;
                    }
            return display + ".";
        }
    }
}
