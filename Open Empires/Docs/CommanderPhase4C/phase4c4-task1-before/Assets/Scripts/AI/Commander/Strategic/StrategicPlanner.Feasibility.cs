using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public sealed partial class StrategicPlanner
    {
        // Uses the planner's existing canonical game queries. No plan/template is instantiated.
        internal IReadOnlyList<StrategicFeasibility> QuoteFeasibility(CommanderContext context)
        {
            ThrowIfDisposed();
            var result = new List<StrategicFeasibility>();
            foreach (StrategicObjectiveType objective in Enum.GetValues(typeof(StrategicObjectiveType)))
                result.Add(QuoteObjective(context, objective));
            return result.AsReadOnly();
        }

        private StrategicFeasibility QuoteObjective(CommanderContext context, StrategicObjectiveType objective)
        {
            var sim = goalManager.Simulation;
            var totals = new SortedDictionary<ResourceType, int>();
            var plannedProduction = new HashSet<BuildingType>();
            string rejection = string.Empty;
            int neededPopulation = 0;
            int plannedPopulation = 0;
            int queuedPopulation = 0;
            int workers = 0;
            foreach (var unit in context.Units) if (unit.UnitType == 0) workers += unit.Count;
            foreach (var building in context.Buildings) queuedPopulation += building.TrainingQueue.Count;

            void Reject(string reason) { if (rejection.Length == 0) rejection = reason; }
            void Cost(ResourceType type, int amount)
            {
                if (amount > 0) totals[type] = checked((totals.TryGetValue(type, out int value) ? value : 0) + amount);
            }
            void Build(BuildingType type, int count, bool ensure = false, bool optional = false,
                bool netOfFoundations = false)
            {
                if (context.Age < LandmarkDefinitions.GetBuildingRequiredAge(type))
                {
                    if (!optional) Reject(type + " construction is not available at the current age.");
                    return;
                }
                int remaining = count;
                foreach (var building in context.Buildings)
                    if (building.Type == type.ToString())
                    {
                        if (!netOfFoundations && (ensure || building.IsUnderConstruction)) remaining--;
                        // Existing foundations are already funded and the normal executor can resume them.
                        if (building.IsUnderConstruction && workers > 0) plannedProduction.Add(type);
                    }
                remaining = Math.Max(0, remaining);
                if (remaining > 0)
                {
                    if (workers == 0) Reject("Construction requires an available owned worker.");
                    Cost(ResourceType.Food, checked(sim.GetBuildingFoodCost(type) * remaining));
                    Cost(ResourceType.Wood, checked(sim.GetBuildingWoodCost(type) * remaining));
                    Cost(ResourceType.Gold, checked(sim.GetBuildingGoldCost(type) * remaining));
                    Cost(ResourceType.Stone, checked(sim.GetBuildingStoneCost(type) * remaining));
                    plannedProduction.Add(type);
                    if (type == BuildingType.House) plannedPopulation += remaining * sim.Config.HousePopulation;
                    if (type == BuildingType.TownCenter) plannedPopulation += remaining * sim.Config.TownCenterPopulation;
                }
            }
            void Train(int requested, int target)
            {
                sim.GetUnitTrainingSpec(PlayerId, requested, out int resolved,
                    out int food, out int wood, out int gold, out _);
                int remaining = target;
                foreach (var unit in context.Units)
                    if (unit.UnitType == resolved) remaining -= unit.Count + unit.QueuedCount;
                remaining = Math.Max(0, remaining);
                if (remaining == 0) return;
                neededPopulation += remaining;
                if (context.Age < LandmarkDefinitions.GetUnitRequiredAge(resolved))
                    Reject("Required unit training is not available at the current age.");
                bool capacity = false;
                bool existingProducer = false;
                foreach (var building in context.Buildings)
                    if (Contains(building.TrainableUnitTypes, resolved))
                    {
                        existingProducer = true;
                        if (building.IsUnderConstruction ? workers > 0 : building.TrainingQueue.Count < 3)
                            capacity = true;
                    }
                if (sim.TryGetProductionBuildingType(PlayerId, requested, out BuildingType type))
                {
                    if (!capacity && !existingProducer && !plannedProduction.Contains(type))
                        Build(type, 1, ensure: true);
                    if (plannedProduction.Contains(type)) capacity = true;
                }
                if (!capacity) Reject("Required training capability or production capacity is unavailable.");
                Cost(ResourceType.Food, checked(food * remaining));
                Cost(ResourceType.Wood, checked(wood * remaining));
                Cost(ResourceType.Gold, checked(gold * remaining));
            }

            switch (objective)
            {
                case StrategicObjectiveType.AttackPreparation:
                    Build(BuildingType.Stables, 1, ensure: true);
                    Train(CommanderIntentCatalog.KnightUnitType, CavalryPressurePlan.KnightTarget);
                    break;
                case StrategicObjectiveType.DefensivePreparation:
                    Build(BuildingType.Barracks, 1, ensure: true);
                    Build(BuildingType.Tower, 2, optional: true);
                    Train(CommanderIntentCatalog.SpearmanUnitType, DefensivePreparationPlan.SpearmanTarget);
                    break;
                case StrategicObjectiveType.EconomicExpansion:
                    Build(BuildingType.TownCenter, 1);
                    Train(0, EconomicExpansionPlan.WorkerTarget);
                    break;
                case StrategicObjectiveType.MilitaryReinforcement:
                    Build(BuildingType.Barracks, 2);
                    Train(CommanderIntentCatalog.SpearmanUnitType, MilitaryReinforcementPlan.SpearmanTarget);
                    Train(CommanderIntentCatalog.ArcherUnitType, MilitaryReinforcementPlan.ArcherTarget);
                    break;
            }
            if (workers == 0) Reject("Strategic preparation requires owned workers.");
            int finalPopulation = context.Population + queuedPopulation + neededPopulation;
            if (finalPopulation > context.MaximumPopulation)
                Reject("Required units exceed maximum population capacity.");
            if (neededPopulation > 0 && context.PopulationCap <= context.Population + queuedPopulation)
                Reject("No population capacity is available.");
            int futureCapacity = context.PopulationCap + plannedPopulation;
            foreach (var building in context.Buildings)
                if (building.IsUnderConstruction)
                {
                    if (building.Type == BuildingType.House.ToString()) futureCapacity += sim.Config.HousePopulation;
                    if (building.Type == BuildingType.TownCenter.ToString()) futureCapacity += sim.Config.TownCenterPopulation;
                }
            if (finalPopulation <= context.MaximumPopulation && finalPopulation > futureCapacity)
                Build(BuildingType.House,
                    (finalPopulation - futureCapacity + sim.Config.HousePopulation - 1) / sim.Config.HousePopulation,
                    netOfFoundations: true);
            var costs = new List<StrategicRequirementState>();
            foreach (var entry in totals) costs.Add(new StrategicRequirementState(entry.Key, entry.Value));
            return new StrategicFeasibility(objective, rejection, costs);
        }

        private static bool Contains(IReadOnlyList<int> values, int value)
        {
            for (int i = 0; i < values.Count; i++) if (values[i] == value) return true;
            return false;
        }
    }
}
