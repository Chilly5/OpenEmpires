using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public sealed class CommanderContextBuilder
    {
        // Call once at submission on the simulation's owning (Unity main) thread.
        public CommanderContext Build(GameSimulation simulation, CommanderGoalManager manager)
        {
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            int player = manager.PlayerId;
            if (player < 0 || player >= simulation.PlayerCount) throw new ArgumentOutOfRangeException(nameof(manager));
            var resources = simulation.ResourceManager.GetPlayerResources(player);
            var counts = new SortedDictionary<int, int>();
            var queued = new SortedDictionary<int, int>();
            foreach (var unit in simulation.UnitRegistry.GetAllUnits())
                if (unit.PlayerId == player && unit.CurrentHealth > 0 && unit.State != UnitState.Dead)
                    counts[unit.UnitType] = counts.TryGetValue(unit.UnitType, out int count) ? count + 1 : 1;

            var buildings = new List<CommanderBuildingSnapshot>();
            var production = new List<CommanderBuildingSnapshot>();
            foreach (var building in simulation.BuildingRegistry.GetAllBuildings())
            {
                if (building.PlayerId != player || building.IsDestroyed) continue;
                foreach (int id in building.GarrisonedUnitIds)
                {
                    var unit = simulation.UnitRegistry.GetGarrisonedUnit(id);
                    if (unit != null && unit.PlayerId == player && unit.CurrentHealth > 0 && unit.State != UnitState.Dead)
                        counts[unit.UnitType] = counts.TryGetValue(unit.UnitType, out int count) ? count + 1 : 1;
                }
                var trainable = new List<int>();
                // Canonical base training IDs, resolved through the existing civilization rules.
                for (int type = 0; type <= 15; type++)
                    if (type != 5 && simulation.IsCompatibleProductionBuilding(player, building, type))
                    {
                        int resolved = simulation.ResolveCivUnitType(player, type);
                        if (!trainable.Contains(resolved)) trainable.Add(resolved);
                    }
                var queue = new List<int>(building.TrainingQueue);
                foreach (int type in queue)
                    queued[type] = queued.TryGetValue(type, out int count) ? count + 1 : 1;
                var snapshot = new CommanderBuildingSnapshot(building.Id, building.Type.ToString(),
                    simulation.GetEffectiveBuildingType(building).ToString(), building.IsUnderConstruction,
                    trainable, queue, building.TrainingTicksRemaining);
                buildings.Add(snapshot);
                if (snapshot.CanProduce && snapshot.IsCompleted) production.Add(snapshot);
            }
            var units = new List<CommanderUnitSnapshot>();
            foreach (int type in queued.Keys) if (!counts.ContainsKey(type)) counts[type] = 0;
            foreach (var entry in counts)
                units.Add(new CommanderUnitSnapshot(entry.Key, entry.Value,
                    queued.TryGetValue(entry.Key, out int count) ? count : 0));
            var technologies = new List<string>();
            foreach (TechnologyType technology in Enum.GetValues(typeof(TechnologyType)))
                if (simulation.HasTechnology(player, technology)) technologies.Add(technology.ToString());
            var goals = new List<CommanderGoalSnapshot>();
            foreach (var goal in manager.Goals)
            {
                if (goal.IsTerminal || goal.PlayerId != player) continue;
                string target = string.Empty; int amount = 0;
                if (goal is EnsureUnitCountGoal ensure) { target = CommanderIntentCatalog.GetUnitDisplayName(ensure.RequestedUnitType); amount = ensure.TargetTotal; }
                if (goal is BuildStructureGoal build) { target = build.StructureType.ToString(); amount = build.TargetTotal; }
                if (goal is ResourceAllocationGoal allocation) { target = allocation.Resource.ToString(); amount = allocation.TargetWorkers; }
                goals.Add(new CommanderGoalSnapshot(goal.GoalId, goal.GoalType.ToString(), goal.Status.ToString(), target, amount,
                    goal.PlayerId, goal.CreatedTick, goal.Priority, goal.ParentGoalId, goal.Lifecycle.ToString()));
            }
            var visibleResources = new List<CommanderVisibleResourceSnapshot>();
            foreach (var node in simulation.MapData.GetAllResourceNodes())
                // Same origin-tile visibility gate as CommanderPlanner. Explored is not current knowledge.
                if (!node.IsDepleted && simulation.FogOfWar.GetVisibility(player, node.TileX, node.TileZ) == TileVisibility.Visible)
                    visibleResources.Add(new CommanderVisibleResourceSnapshot(node.Type.ToString(), node.TileX, node.TileZ, node.RemainingAmount));
            var options = new List<CommanderUnitOptionSnapshot>();
            foreach (int type in new[] { 1, 2, 7 })
            {
                int resolved = simulation.ResolveCivUnitType(player, type);
                options.Add(new CommanderUnitOptionSnapshot(CommanderIntentCatalog.GetUnitDisplayName(type), resolved,
                    LandmarkDefinitions.GetUnitRequiredAge(resolved), simulation.GetPlayerAge(player)));
            }
            return new CommanderContext(player, simulation.CurrentTick,
                new CommanderResourceSnapshot(resources.Food, resources.Wood, resources.Gold, resources.Stone),
                simulation.GetPopulation(player), simulation.GetPopulationCap(player), simulation.Config.MaxPopulation,
                simulation.GetPlayerAge(player), simulation.GetPlayerCivilization(player).ToString(),
                buildings, units, production, technologies, goals, visibleResources, options);
        }
    }
}
