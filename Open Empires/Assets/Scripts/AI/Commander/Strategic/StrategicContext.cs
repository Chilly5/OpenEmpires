using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    // Strategic snapshots are detached values derived from the fog-safe CommanderContext.
    public sealed class StrategicContext
    {
        public int PlayerId { get; }
        public int SnapshotTick { get; }
        public IReadOnlyList<StrategicResourceState> Economy { get; }
        public StrategicPopulationState Population { get; }
        public IReadOnlyList<StrategicMilitaryState> Military { get; }
        public IReadOnlyList<StrategicProductionState> Production { get; }
        public IReadOnlyList<StrategicPlanState> ActivePlans { get; }
        public IReadOnlyList<StrategicVisibleResourceState> VisibleResources { get; }
        public IReadOnlyList<StrategicWorkerAllocationState> WorkerAllocation { get; }
        public int TotalWorkers { get; }
        public int TotalGatheringWorkers { get; }
        public int TotalMilitaryUnits { get; }
        public int ArmyStrengthEstimate { get; }
        public StrategicDefenseState Defense { get; }
        public StrategicThreatState Threat { get; }
        public string InformationBoundary =>
            "Owned state plus currently visible resource nodes and enemy military aggregates; "
            + "no hidden, explored-only, or predicted enemy data.";

        internal StrategicContext(int playerId, int snapshotTick,
            List<StrategicResourceState> economy, StrategicPopulationState population,
            List<StrategicMilitaryState> military, List<StrategicProductionState> production,
            List<StrategicPlanState> activePlans, List<StrategicVisibleResourceState> visibleResources,
            List<StrategicWorkerAllocationState> workerAllocation, int totalWorkers,
            StrategicDefenseState defense, StrategicThreatState threat)
        {
            PlayerId = playerId;
            SnapshotTick = snapshotTick;
            Economy = economy.AsReadOnly();
            Population = population ?? throw new ArgumentNullException(nameof(population));
            Military = military.AsReadOnly();
            Production = production.AsReadOnly();
            ActivePlans = activePlans.AsReadOnly();
            VisibleResources = visibleResources.AsReadOnly();
            WorkerAllocation = workerAllocation.AsReadOnly();
            TotalWorkers = Math.Max(0, totalWorkers);
            for (int i = 0; i < WorkerAllocation.Count; i++)
                TotalGatheringWorkers += WorkerAllocation[i].AssignedWorkers;
            for (int i = 0; i < Military.Count; i++)
            {
                TotalMilitaryUnits += Military[i].OwnedCount;
                ArmyStrengthEstimate += Military[i].StrengthEstimate;
            }
            Defense = defense ?? throw new ArgumentNullException(nameof(defense));
            Threat = threat ?? throw new ArgumentNullException(nameof(threat));
        }

        public string ToJson() => Newtonsoft.Json.JsonConvert.SerializeObject(this,
            new Newtonsoft.Json.JsonSerializerSettings
            {
                TypeNameHandling = Newtonsoft.Json.TypeNameHandling.None
            });
    }

    public sealed class StrategicResourceState
    {
        public ResourceType ResourceType { get; }
        public int CurrentAmount { get; }
        public int ReservedAmount { get; }
        public int AvailableAmount { get; }

        internal StrategicResourceState(ResourceType resourceType, int current, int reserved)
        {
            ResourceType = resourceType;
            CurrentAmount = current;
            ReservedAmount = reserved;
            AvailableAmount = Math.Max(0, current - reserved);
        }
    }

    public sealed class StrategicPopulationState
    {
        public int CurrentPopulation { get; }
        public int PopulationCap { get; }
        public int MaximumPopulation { get; }
        public int AvailableCapacity { get; }

        internal StrategicPopulationState(int current, int populationCap, int maximumPopulation)
        {
            CurrentPopulation = current;
            PopulationCap = populationCap;
            MaximumPopulation = maximumPopulation;
            AvailableCapacity = Math.Max(0, populationCap - current);
        }
    }

    public sealed class StrategicMilitaryState
    {
        public int UnitType { get; }
        public int OwnedCount { get; }
        public int QueuedCount { get; }
        public int StrengthEstimate { get; }

        internal StrategicMilitaryState(int unitType, int ownedCount, int queuedCount)
        {
            UnitType = unitType;
            OwnedCount = ownedCount;
            QueuedCount = queuedCount;
            StrengthEstimate = Math.Max(0, ownedCount);
        }
    }

    public sealed class StrategicWorkerAllocationState
    {
        public ResourceType ResourceType { get; }
        public int AssignedWorkers { get; }

        internal StrategicWorkerAllocationState(ResourceType resourceType, int assignedWorkers)
        {
            ResourceType = resourceType;
            AssignedWorkers = Math.Max(0, assignedWorkers);
        }
    }

    public sealed class StrategicDefenseState
    {
        public int DefensiveBuildingCount { get; }
        public int CapabilityEstimate { get; }

        internal StrategicDefenseState(int defensiveBuildingCount, int capabilityEstimate)
        {
            DefensiveBuildingCount = Math.Max(0, defensiveBuildingCount);
            CapabilityEstimate = Math.Max(0, capabilityEstimate);
        }
    }

    public sealed class StrategicThreatState
    {
        public IReadOnlyList<StrategicVisibleEnemyMilitaryState> VisibleEnemyMilitary { get; }
        public int VisibleEnemyMilitaryUnits { get; }
        public int VisibleEnemyMilitaryStrength { get; }

        internal StrategicThreatState(List<StrategicVisibleEnemyMilitaryState> visibleEnemyMilitary)
        {
            if (visibleEnemyMilitary == null)
                throw new ArgumentNullException(nameof(visibleEnemyMilitary));
            VisibleEnemyMilitary = visibleEnemyMilitary.AsReadOnly();
            for (int i = 0; i < VisibleEnemyMilitary.Count; i++)
            {
                VisibleEnemyMilitaryUnits += VisibleEnemyMilitary[i].VisibleCount;
                VisibleEnemyMilitaryStrength += VisibleEnemyMilitary[i].StrengthEstimate;
            }
        }
    }

    public sealed class StrategicVisibleEnemyMilitaryState
    {
        public int UnitType { get; }
        public int VisibleCount { get; }
        public int StrengthEstimate { get; }

        internal StrategicVisibleEnemyMilitaryState(int unitType, int visibleCount)
        {
            UnitType = unitType;
            VisibleCount = Math.Max(0, visibleCount);
            StrengthEstimate = VisibleCount;
        }
    }

    public sealed class StrategicProductionState
    {
        public string BuildingType { get; }
        public int ProductionBuildingCount { get; }
        public int UnderConstructionCount { get; }
        public int ActiveQueueCount { get; }
        public int QueuedUnitCount { get; }
        public int AvailableCapacity { get; }

        internal StrategicProductionState(string buildingType, int buildingCount,
            int underConstruction, int activeQueues, int queuedUnits, int availableCapacity)
        {
            BuildingType = buildingType;
            ProductionBuildingCount = buildingCount;
            UnderConstructionCount = underConstruction;
            ActiveQueueCount = activeQueues;
            QueuedUnitCount = queuedUnits;
            AvailableCapacity = Math.Max(0, availableCapacity);
        }
    }

    public sealed class StrategicPlanState
    {
        public int StrategicPlanId { get; }
        public string PlanType { get; }
        public string Status { get; }
        public string CurrentMilestone { get; }
        public string MilestoneStatus { get; }
        public IReadOnlyList<StrategicRequirementState> RequiredResources { get; }
        public IReadOnlyList<StrategicReservationState> Reservations { get; }

        internal StrategicPlanState(StrategicPlan plan,
            List<StrategicRequirementState> requirements,
            List<StrategicReservationState> reservations)
        {
            StrategicPlanId = plan.StrategicPlanId;
            PlanType = plan.PlanType.ToString();
            Status = plan.Status.ToString();
            CurrentMilestone = plan.CurrentMilestone?.Name ?? string.Empty;
            MilestoneStatus = plan.CurrentMilestone?.Status.ToString() ?? string.Empty;
            RequiredResources = requirements.AsReadOnly();
            Reservations = reservations.AsReadOnly();
        }
    }

    public sealed class StrategicRequirementState
    {
        public ResourceType ResourceType { get; }
        public int Amount { get; }

        internal StrategicRequirementState(ResourceType resourceType, int amount)
        {
            ResourceType = resourceType;
            Amount = amount;
        }
    }

    public sealed class StrategicReservationState
    {
        public int ReservationId { get; }
        public ResourceType ResourceType { get; }
        public int Amount { get; }
        public string Status { get; }

        internal StrategicReservationState(StrategicResourceReservation reservation)
        {
            ReservationId = reservation.ReservationId;
            ResourceType = reservation.ResourceType;
            Amount = reservation.Amount;
            Status = reservation.Status.ToString();
        }
    }

    public sealed class StrategicVisibleResourceState
    {
        public string ResourceType { get; }
        public int TileX { get; }
        public int TileZ { get; }
        public int RemainingAmount { get; }

        internal StrategicVisibleResourceState(CommanderVisibleResourceSnapshot resource)
        {
            ResourceType = resource.Type;
            TileX = resource.TileX;
            TileZ = resource.TileZ;
            RemainingAmount = resource.RemainingAmount;
        }
    }
}
