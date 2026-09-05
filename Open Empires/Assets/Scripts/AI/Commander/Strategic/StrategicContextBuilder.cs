using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public sealed class StrategicContextBuilder
    {
        public StrategicContext Build(CommanderContext commanderContext, StrategicPlanner planner)
        {
            if (commanderContext == null) throw new ArgumentNullException(nameof(commanderContext));
            if (planner == null) throw new ArgumentNullException(nameof(planner));
            if (commanderContext.PlayerId != planner.PlayerId)
                throw new ArgumentException("Strategic and tactical contexts must have the same owner.",
                    nameof(commanderContext));

            var economy = new List<StrategicResourceState>();
            foreach (ResourceType resourceType in Enum.GetValues(typeof(ResourceType)))
            {
                int current = GetCurrentAmount(commanderContext.Resources, resourceType);
                economy.Add(new StrategicResourceState(resourceType, current,
                    planner.GetReservedAmount(resourceType)));
            }

            var military = new List<StrategicMilitaryState>();
            int totalWorkers = 0;
            for (int i = 0; i < commanderContext.Units.Count; i++)
            {
                CommanderUnitSnapshot unit = commanderContext.Units[i];
                // Villagers (0) and sheep (5) are not part of army composition.
                if (unit.UnitType == 0)
                {
                    totalWorkers += unit.Count;
                    continue;
                }
                if (unit.UnitType == 5) continue;
                military.Add(new StrategicMilitaryState(unit.UnitType, unit.Count, unit.QueuedCount));
            }
            military.Sort((left, right) => left.UnitType.CompareTo(right.UnitType));

            var workerAllocation = new List<StrategicWorkerAllocationState>();
            for (int i = 0; i < commanderContext.WorkerAllocation.Count; i++)
            {
                CommanderWorkerAllocationSnapshot allocation = commanderContext.WorkerAllocation[i];
                workerAllocation.Add(new StrategicWorkerAllocationState(
                    allocation.ResourceType, allocation.AssignedWorkers));
            }

            var visibleEnemyMilitary = new List<StrategicVisibleEnemyMilitaryState>();
            for (int i = 0; i < commanderContext.VisibleEnemyMilitary.Count; i++)
            {
                CommanderVisibleEnemyMilitarySnapshot visible =
                    commanderContext.VisibleEnemyMilitary[i];
                visibleEnemyMilitary.Add(new StrategicVisibleEnemyMilitaryState(
                    visible.UnitType, visible.VisibleCount));
            }

            var productionByType = new SortedDictionary<string, ProductionAccumulator>(StringComparer.Ordinal);
            for (int i = 0; i < commanderContext.Buildings.Count; i++)
            {
                CommanderBuildingSnapshot building = commanderContext.Buildings[i];
                if (!building.CanProduce) continue;
                if (!productionByType.TryGetValue(building.EffectiveType, out ProductionAccumulator value))
                    value = default;
                value.Buildings++;
                if (building.IsUnderConstruction) value.UnderConstruction++;
                else if (building.TrainingQueue.Count > 0) value.ActiveQueues++;
                else value.AvailableCapacity++;
                value.QueuedUnits += building.TrainingQueue.Count;
                productionByType[building.EffectiveType] = value;
            }
            var production = new List<StrategicProductionState>();
            foreach (KeyValuePair<string, ProductionAccumulator> entry in productionByType)
                production.Add(new StrategicProductionState(entry.Key, entry.Value.Buildings,
                    entry.Value.UnderConstruction, entry.Value.ActiveQueues,
                    entry.Value.QueuedUnits, entry.Value.AvailableCapacity));

            var activePlans = new List<StrategicPlanState>();
            for (int i = 0; i < planner.Plans.Count; i++)
            {
                StrategicPlan plan = planner.Plans[i];
                if (plan.IsTerminal) continue;
                var requirements = new List<StrategicRequirementState>();
                for (int requirementIndex = 0;
                    requirementIndex < plan.RequiredResources.Count; requirementIndex++)
                {
                    StrategicResourceRequirement requirement = plan.RequiredResources[requirementIndex];
                    requirements.Add(new StrategicRequirementState(requirement.ResourceType, requirement.Amount));
                }
                var reservations = new List<StrategicReservationState>();
                IReadOnlyList<StrategicResourceReservation> owned =
                    planner.GetReservationsForPlan(plan.StrategicPlanId);
                for (int reservationIndex = 0; reservationIndex < owned.Count; reservationIndex++)
                    if (owned[reservationIndex].Status == StrategicResourceReservationStatus.Active)
                        reservations.Add(new StrategicReservationState(owned[reservationIndex]));
                activePlans.Add(new StrategicPlanState(plan, requirements, reservations));
            }

            var visibleResources = new List<StrategicVisibleResourceState>();
            for (int i = 0; i < commanderContext.VisibleResources.Count; i++)
                visibleResources.Add(new StrategicVisibleResourceState(
                    commanderContext.VisibleResources[i]));
            visibleResources.Sort(CompareVisibleResources);

            int militaryStrength = 0;
            for (int i = 0; i < military.Count; i++)
                militaryStrength += military[i].StrengthEstimate;
            int defensiveBuildings = 0;
            int defensiveBuildingStrength = 0;
            for (int i = 0; i < commanderContext.Buildings.Count; i++)
            {
                CommanderBuildingSnapshot building = commanderContext.Buildings[i];
                if (!building.IsCompleted) continue;
                if (building.EffectiveType == BuildingType.Tower.ToString())
                {
                    defensiveBuildings++;
                    defensiveBuildingStrength += 3;
                }
                else if (building.EffectiveType == BuildingType.Keep.ToString())
                {
                    defensiveBuildings++;
                    defensiveBuildingStrength += 8;
                }
            }

            return new StrategicContext(commanderContext.PlayerId, commanderContext.SnapshotTick,
                economy, new StrategicPopulationState(commanderContext.Population,
                    commanderContext.PopulationCap, commanderContext.MaximumPopulation),
                military, production, activePlans,
                visibleResources, workerAllocation, totalWorkers,
                new StrategicDefenseState(defensiveBuildings,
                    militaryStrength + defensiveBuildingStrength),
                new StrategicThreatState(visibleEnemyMilitary));
        }

        private static int GetCurrentAmount(CommanderResourceSnapshot resources,
            ResourceType resourceType)
        {
            switch (resourceType)
            {
                case ResourceType.Food: return resources.Food;
                case ResourceType.Wood: return resources.Wood;
                case ResourceType.Gold: return resources.Gold;
                case ResourceType.Stone: return resources.Stone;
                default: throw new ArgumentOutOfRangeException(nameof(resourceType));
            }
        }

        private static int CompareVisibleResources(StrategicVisibleResourceState left,
            StrategicVisibleResourceState right)
        {
            int type = string.CompareOrdinal(left.ResourceType, right.ResourceType);
            if (type != 0) return type;
            int x = left.TileX.CompareTo(right.TileX);
            return x != 0 ? x : left.TileZ.CompareTo(right.TileZ);
        }

        private struct ProductionAccumulator
        {
            public int Buildings;
            public int UnderConstruction;
            public int ActiveQueues;
            public int QueuedUnits;
            public int AvailableCapacity;
        }
    }
}
