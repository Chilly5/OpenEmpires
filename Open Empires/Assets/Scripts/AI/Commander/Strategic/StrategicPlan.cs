using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public enum StrategicPlanType
    {
        CavalryPressure,
        DefensivePreparation,
        EconomicExpansion,
        MilitaryReinforcement
    }

    public enum StrategicPlanStatus
    {
        Created,
        Active,
        Paused,
        Completed,
        Failed,
        Cancelled
    }

    public enum StrategicPlanAuthority
    {
        Normal = 0,
        Emergency = 1,
        PlayerOverride = 2
    }

    public abstract class StrategicPlan
    {
        private readonly List<StrategicMilestone> milestones = new List<StrategicMilestone>();
        private readonly List<int> childGoalIds = new List<int>();
        private readonly List<StrategicResourceRequirement> requiredResources =
            new List<StrategicResourceRequirement>();
        private readonly List<StrategicBudgetRequirement> budgetRequirements =
            new List<StrategicBudgetRequirement>();
        private readonly List<int> resourceReservationIds = new List<int>();
        private int currentMilestoneIndex = -1;

        public int StrategicPlanId { get; internal set; }
        public int SourceIntentId { get; }
        public int OwnerPlayerId { get; }
        public StrategicPlanType PlanType { get; }
        public StrategicPlanAuthority Authority { get; internal set; }
        public StrategicIntentSource Source { get; internal set; }
        public StrategicMilestone CurrentMilestone => currentMilestoneIndex >= 0
            && currentMilestoneIndex < milestones.Count ? milestones[currentMilestoneIndex] : null;
        public StrategicPlanStatus Status { get; internal set; }
        public int CreatedTick { get; internal set; }
        public IReadOnlyList<int> ChildGoalIds => childGoalIds;
        public IReadOnlyList<StrategicMilestone> Milestones => milestones;
        public IReadOnlyList<StrategicResourceRequirement> RequiredResources => requiredResources;
        public IReadOnlyList<StrategicBudgetRequirement> BudgetRequirements => budgetRequirements;
        public IReadOnlyList<int> ResourceReservationIds => resourceReservationIds;
        public string OutcomeMessage { get; internal set; } = string.Empty;
        public string CompletionMessage { get; }
        public string CancellationMessage { get; }
        public bool IsTerminal => Status == StrategicPlanStatus.Completed
            || Status == StrategicPlanStatus.Failed
            || Status == StrategicPlanStatus.Cancelled;

        protected StrategicPlan(int ownerPlayerId, StrategicPlanType planType, int sourceIntentId,
            string completionResponse, string cancellationResponse)
        {
            if (ownerPlayerId < 0) throw new ArgumentOutOfRangeException(nameof(ownerPlayerId));
            if (sourceIntentId < 1) throw new ArgumentOutOfRangeException(nameof(sourceIntentId));
            OwnerPlayerId = ownerPlayerId;
            PlanType = planType;
            SourceIntentId = sourceIntentId;
            CompletionMessage = completionResponse ?? string.Empty;
            CancellationMessage = cancellationResponse ?? string.Empty;
            Status = StrategicPlanStatus.Created;
        }

        protected void AddMilestone(StrategicMilestone milestone)
        {
            if (milestone == null) throw new ArgumentNullException(nameof(milestone));
            if (milestone.OrderIndex != milestones.Count)
                throw new ArgumentException("Milestones must be added in contiguous order.", nameof(milestone));
            milestones.Add(milestone);
        }

        public void AddBudgetRequirement(ResourceType resourceType, int amount)
        {
            for (int i = 0; i < budgetRequirements.Count; i++)
                if (budgetRequirements[i].ResourceType == resourceType)
                    throw new InvalidOperationException("A plan can define only one budget requirement per resource type.");
            budgetRequirements.Add(new StrategicBudgetRequirement(resourceType, amount));
        }

        internal void SetBudgetFromSimulation(IReadOnlyList<StrategicResourceRequirement> requirements)
        {
            requiredResources.Clear();
            budgetRequirements.Clear();
            foreach (StrategicResourceRequirement requirement in requirements)
                AddRequiredResource(requirement.ResourceType, requirement.Amount);
        }

        protected void AddRequiredResource(ResourceType resourceType, int amount)
        {
            for (int i = 0; i < requiredResources.Count; i++)
                if (requiredResources[i].ResourceType == resourceType)
                    throw new InvalidOperationException("A plan can define only one requirement per resource type.");
            requiredResources.Add(new StrategicResourceRequirement(resourceType, amount));
            bool hasBudget = false;
            for (int i = 0; i < budgetRequirements.Count; i++)
            {
                if (budgetRequirements[i].ResourceType == resourceType)
                {
                    hasBudget = true;
                    break;
                }
            }
            if (!hasBudget)
            {
                budgetRequirements.Add(new StrategicBudgetRequirement(resourceType, amount));
            }
        }

        internal StrategicMilestone ActivateFirstMilestone()
        {
            if (milestones.Count == 0) throw new InvalidOperationException("A strategic plan requires at least one milestone.");
            currentMilestoneIndex = 0;
            milestones[0].SetStatus(StrategicMilestoneStatus.Active);
            return milestones[0];
        }

        internal StrategicMilestone AdvanceMilestone()
        {
            if (currentMilestoneIndex + 1 >= milestones.Count) return null;
            currentMilestoneIndex++;
            milestones[currentMilestoneIndex].SetStatus(StrategicMilestoneStatus.Active);
            return milestones[currentMilestoneIndex];
        }

        internal void AddChildGoal(int goalId)
        {
            if (!childGoalIds.Contains(goalId)) childGoalIds.Add(goalId);
        }


        internal void AddResourceReservation(int reservationId)
        {
            if (reservationId < 1) throw new ArgumentOutOfRangeException(nameof(reservationId));
            if (!resourceReservationIds.Contains(reservationId))
                resourceReservationIds.Add(reservationId);
        }
    }

    public sealed class CavalryPressurePlan : StrategicPlan
    {
        public const int FoodWorkerTarget = 10;
        public const int GoldWorkerTarget = 6;
        public const int WoodWorkerTarget = 4;
        public const int KnightTarget = 6;
        public const string CompletionResponse = "Cavalry preparation complete.";
        public const string CancellationResponse = "Cavalry preparation cancelled.";

        internal CavalryPressurePlan(int ownerPlayerId, int sourceIntentId)
            : base(ownerPlayerId, StrategicPlanType.CavalryPressure, sourceIntentId,
                CompletionResponse, CancellationResponse)
        {

            var economy = new StrategicMilestone(1, "Economic Foundation", 0);
            economy.AddTacticalGoal(new StrategicResourceAllocationGoalRequest(
                ResourceType.Food, FoodWorkerTarget));
            economy.AddTacticalGoal(new StrategicResourceAllocationGoalRequest(
                ResourceType.Gold, GoldWorkerTarget));
            economy.AddTacticalGoal(new StrategicResourceAllocationGoalRequest(ResourceType.Wood, WoodWorkerTarget));
            AddMilestone(economy);

            var infrastructure = new StrategicMilestone(2, "Infrastructure", 1);
            infrastructure.AddTacticalGoal(new StrategicBuildStructureGoalRequest(
                BuildingType.Stables, ensureExisting: true));
            AddMilestone(infrastructure);

            var army = new StrategicMilestone(3, "Army Preparation", 2);
            army.AddTacticalGoal(new StrategicEnsureUnitCountGoalRequest(
                CommanderIntentCatalog.KnightUnitType, KnightTarget));
            AddMilestone(army);
            AddMilestone(new StrategicMilestone(4, "Ready", 3));
        }
    }

    public sealed class DefensivePreparationPlan : StrategicPlan
    {
        public const int WoodWorkerTarget = 8;
        public const int StoneWorkerTarget = 4;
        public const int SpearmanTarget = 8;
        public const string CompletionResponse = "Defensive preparation complete.";
        public const string CancellationResponse = "Defensive preparation cancelled.";

        public DefensivePreparationPlan(int ownerPlayerId, int sourceIntentId)
            : base(ownerPlayerId, StrategicPlanType.DefensivePreparation, sourceIntentId,
                CompletionResponse, CancellationResponse)
        {

            var economy = new StrategicMilestone(1, "Economic Defense", 0);
            economy.AddTacticalGoal(new StrategicResourceAllocationGoalRequest(ResourceType.Food, 8));
            economy.AddTacticalGoal(new StrategicResourceAllocationGoalRequest(ResourceType.Wood, WoodWorkerTarget));
            AddMilestone(economy);
            var infrastructure = new StrategicMilestone(2, "Military Infrastructure", 1);
            infrastructure.AddTacticalGoal(new StrategicBuildStructureGoalRequest(BuildingType.Barracks, ensureExisting: true));
            AddMilestone(infrastructure);
            var towers = new StrategicMilestone(3, "Defensive Structures", 2);
            towers.AddTacticalGoal(new StrategicBuildStructureGoalRequest(BuildingType.Tower, 2, skipIfAgeUnavailable: true));
            AddMilestone(towers);
            var army = new StrategicMilestone(4, "Defense Army", 3);
            army.AddTacticalGoal(new StrategicEnsureUnitCountGoalRequest(CommanderIntentCatalog.SpearmanUnitType, SpearmanTarget));
            AddMilestone(army);
            AddMilestone(new StrategicMilestone(5, "Ready", 4));
        }
    }

    public sealed class EconomicExpansionPlan : StrategicPlan
    {
        public const int FoodWorkerTarget = 8;
        public const int WoodWorkerTarget = 6;
        public const int WorkerTarget = 20;
        public const string CompletionResponse = "Economic expansion complete.";
        public const string CancellationResponse = "Economic expansion cancelled.";

        public EconomicExpansionPlan(int ownerPlayerId, int sourceIntentId)
            : base(ownerPlayerId, StrategicPlanType.EconomicExpansion, sourceIntentId,
                CompletionResponse, CancellationResponse)
        {

            var boom = new StrategicMilestone(1, "Boom Foundation", 0);
            boom.AddTacticalGoal(new StrategicResourceAllocationGoalRequest(
                ResourceType.Food, FoodWorkerTarget));
            boom.AddTacticalGoal(new StrategicResourceAllocationGoalRequest(
                ResourceType.Wood, WoodWorkerTarget));
            boom.AddTacticalGoal(new StrategicResourceAllocationGoalRequest(ResourceType.Stone, 4));
            AddMilestone(boom);

            var infrastructure = new StrategicMilestone(2, "Economic Infrastructure", 1);
            infrastructure.AddTacticalGoal(new StrategicBuildStructureGoalRequest(
                BuildingType.TownCenter, 1));
            AddMilestone(infrastructure);

            var production = new StrategicMilestone(3, "Worker Production", 2);
            production.AddTacticalGoal(new StrategicEnsureUnitCountGoalRequest(
                0 /* Villager */, WorkerTarget));
            AddMilestone(production);

            AddMilestone(new StrategicMilestone(4, "Ready", 3));
        }
    }

    public sealed class MilitaryReinforcementPlan : StrategicPlan
    {
        public const int SpearmanTarget = 8;
        public const int ArcherTarget = 6;
        public const int FoodRequirement = 400;
        public const int WoodRequirement = 400;
        public const string CompletionResponse = "Military reinforcement complete.";
        public const string CancellationResponse = "Military reinforcement cancelled.";

        public MilitaryReinforcementPlan(int ownerPlayerId, int sourceIntentId)
            : base(ownerPlayerId, StrategicPlanType.MilitaryReinforcement, sourceIntentId,
                CompletionResponse, CancellationResponse)
        {
            AddRequiredResource(ResourceType.Food, FoodRequirement);
            AddRequiredResource(ResourceType.Wood, WoodRequirement);

            var capacity = new StrategicMilestone(1, "Production Capacity", 0);
            capacity.AddTacticalGoal(new StrategicBuildStructureGoalRequest(
                BuildingType.Barracks, 2));
            capacity.AddRequiredResource(ResourceType.Wood, 200);
            AddMilestone(capacity);

            var unitProduction = new StrategicMilestone(2, "Unit Production", 1);
            unitProduction.AddTacticalGoal(new StrategicEnsureUnitCountGoalRequest(
                CommanderIntentCatalog.SpearmanUnitType, SpearmanTarget));
            unitProduction.AddTacticalGoal(new StrategicEnsureUnitCountGoalRequest(
                CommanderIntentCatalog.ArcherUnitType, ArcherTarget));
            unitProduction.AddRequiredResource(ResourceType.Food, 400);
            unitProduction.AddRequiredResource(ResourceType.Wood, 200);
            AddMilestone(unitProduction);

            var assembly = new StrategicMilestone(3, "Assembly", 2);
            assembly.AddTacticalGoal(new StrategicEnsureUnitCountGoalRequest(
                CommanderIntentCatalog.SpearmanUnitType, SpearmanTarget));
            AddMilestone(assembly);

            AddMilestone(new StrategicMilestone(4, "Ready", 3));
        }
    }
}
