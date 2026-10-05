using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenEmpires
{
    // Resolves semantic selectors against current, owned and visible simulation state.
    // No provider-authored entity id, worker id or coordinate is accepted here.
    internal sealed class CommanderCapabilityExecutor
    {
        private readonly GameSimulation simulation;

        public CommanderCapabilityExecutor(GameSimulation simulation)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        }

        public bool TryCreateCommand(CapabilityActionIntent intent, out ICommand command, out string reason)
        {
            return TryCreateCommand(intent, null, out command, out reason);
        }

        public bool TryCreateCommand(CapabilityActionIntent intent, CommanderResultBinding? binding,
            out ICommand command, out string reason)
        {
            command = null;
            reason = string.Empty;
            if (intent == null || intent.PlayerId < 0 || intent.PlayerId >= simulation.FogOfWar.PlayerCount)
            {
                reason = "The owning player is unavailable.";
                return false;
            }

            CommanderResultBinding resolvedBinding = binding.GetValueOrDefault();
            if (binding.HasValue && resolvedBinding.Kind == CommanderResultKind.Building
                && intent.ActionType != CommanderCapabilityActionType.SetRallyPoint)
            {
                reason = "The referenced result is a building, but this action requires units.";
                return false;
            }
            if (binding.HasValue && resolvedBinding.Kind == CommanderResultKind.Units
                && intent.ActionType == CommanderCapabilityActionType.SetRallyPoint)
            {
                reason = "The referenced result is a unit set, but this action requires a building.";
                return false;
            }

            if (intent.ActionType == CommanderCapabilityActionType.ResearchTechnology)
                return TryResearch(intent, out command, out reason);
            if (intent.ActionType == CommanderCapabilityActionType.RepairTarget)
                return TryRepair(intent, binding.HasValue && resolvedBinding.Kind == CommanderResultKind.Units
                        ? resolvedBinding.UnitIds : null,
                    out command, out reason);

            if (!TryResolveUnits(intent.PlayerId, intent.UnitSelector,
                binding.HasValue && resolvedBinding.Kind == CommanderResultKind.Units
                    ? resolvedBinding.UnitIds : null,
                out int[] unitIds))
            {
                reason = "No eligible owned units match the requested selector.";
                return false;
            }
            if (!TryResolveLocation(intent.PlayerId, intent.LocationSelector, unitIds,
                out FixedVector3 position, out ResourceNodeData resource,
                out UnitData enemyUnit, out BuildingData enemyBuilding))
            {
                reason = "No visible, legal target location matches the requested selector.";
                return false;
            }

            switch (intent.ActionType)
            {
                case CommanderCapabilityActionType.MoveUnits:
                case CommanderCapabilityActionType.DefendArea:
                case CommanderCapabilityActionType.RetreatUnits:
                case CommanderCapabilityActionType.ScoutArea:
                    command = new MoveCommand(intent.PlayerId, unitIds, position);
                    reason = "Resolved a bounded owned-unit movement action.";
                    return true;
                case CommanderCapabilityActionType.PatrolArea:
                    command = new PatrolCommand(intent.PlayerId, unitIds, position);
                    reason = "Resolved a bounded patrol around a visible game-side anchor.";
                    return true;
                case CommanderCapabilityActionType.SetRallyPoint:
                    return TryRally(intent.PlayerId, intent.StructureType, position, resource,
                        binding.HasValue && resolvedBinding.Kind == CommanderResultKind.Building
                            ? resolvedBinding.BuildingId : -1,
                        out command, out reason);
                case CommanderCapabilityActionType.AttackTarget:
                    if (enemyUnit != null)
                    {
                        command = new AttackUnitCommand(intent.PlayerId, unitIds, enemyUnit.Id);
                        reason = "Resolved a visible enemy unit target.";
                        return true;
                    }
                    if (enemyBuilding != null)
                    {
                        command = new AttackBuildingCommand(intent.PlayerId, unitIds, enemyBuilding.Id);
                        reason = "Resolved a visible enemy building target.";
                        return true;
                    }
                    reason = "No visible enemy target is available.";
                    return false;
                default:
                    reason = "Unsupported Commander capability.";
                    return false;
            }
        }

        private bool TryResolveUnits(int playerId, CommanderUnitSelector selector,
            IReadOnlyList<int> boundIds, out int[] ids)
        {
            if (boundIds != null)
            {
                if (boundIds.Count != selector.Count) { ids = null; return false; }
                var exact = new List<int>(boundIds.Count);
                for (int i = 0; i < boundIds.Count; i++)
                {
                    int id = boundIds[i];
                    if (exact.Contains(id)) { ids = null; return false; }
                    UnitData unit = simulation.UnitRegistry.GetUnit(id);
                    if (unit == null || unit.PlayerId != playerId || unit.CurrentHealth <= 0
                        || unit.State == UnitState.Dead || !MatchesSelector(unit, selector))
                    { ids = null; return false; }
                    exact.Add(id);
                }
                ids = exact.ToArray();
                return true;
            }
            var candidates = new List<UnitData>();
            foreach (UnitData unit in simulation.UnitRegistry.GetAllUnits())
            {
                if (unit == null || unit.PlayerId != playerId || unit.CurrentHealth <= 0
                    || unit.State == UnitState.Dead) continue;
                bool match;
                switch (selector.Kind)
                {
                    case CommanderUnitSelectorKind.Scout: match = unit.UnitType == 4; break;
                    case CommanderUnitSelectorKind.Villagers: match = unit.IsVillager; break;
                    case CommanderUnitSelectorKind.UnitType: match = unit.UnitType == selector.UnitType; break;
                    case CommanderUnitSelectorKind.DamagedMilitary:
                        match = !unit.IsVillager && unit.UnitType != 4 && unit.CurrentHealth < unit.MaxHealth; break;
                    default: match = !unit.IsVillager && !unit.IsSheep && unit.UnitType != 4; break;
                }
                if (match) candidates.Add(unit);
            }
            candidates.Sort((a, b) => a.Id.CompareTo(b.Id));
            if (candidates.Count < selector.Count) { ids = null; return false; }
            ids = candidates.Take(selector.Count).Select(unit => unit.Id).ToArray();
            return true;
        }

        private static bool MatchesSelector(UnitData unit, CommanderUnitSelector selector)
        {
            switch (selector.Kind)
            {
                case CommanderUnitSelectorKind.Scout: return unit.UnitType == 4;
                case CommanderUnitSelectorKind.Villagers: return unit.IsVillager;
                case CommanderUnitSelectorKind.UnitType: return unit.UnitType == selector.UnitType;
                case CommanderUnitSelectorKind.DamagedMilitary:
                    return !unit.IsVillager && !unit.IsSheep && unit.UnitType != 4
                        && unit.CurrentHealth < unit.MaxHealth;
                default: return !unit.IsVillager && !unit.IsSheep && unit.UnitType != 4;
            }
        }

        private bool TryResolveLocation(int playerId, CommanderLocationSelector selector, int[] selectedUnitIds,
            out FixedVector3 position, out ResourceNodeData resource,
            out UnitData enemyUnit, out BuildingData enemyBuilding)
        {
            position = default;
            resource = null;
            enemyUnit = null;
            enemyBuilding = null;
            BuildingData tc = simulation.BuildingRegistry.GetAllBuildings()
                .Where(b => b != null && b.PlayerId == playerId && !b.IsDestroyed
                    && simulation.GetEffectiveBuildingType(b) == BuildingType.TownCenter)
                .OrderBy(b => b.Id).FirstOrDefault();
            if (selector.Kind == CommanderLocationSelectorKind.PlayerBase
                )
            {
                if (tc == null) return false;
                position = tc.SimPosition;
                return true;
            }
            if (selector.Kind == CommanderLocationSelectorKind.RelativeToSelectedUnits)
            {
                if (selectedUnitIds == null || selectedUnitIds.Length == 0) return false;
                long x = 0;
                long z = 0;
                int count = 0;
                for (int i = 0; i < selectedUnitIds.Length; i++)
                {
                    UnitData selected = simulation.UnitRegistry.GetUnit(selectedUnitIds[i]);
                    if (selected == null || selected.PlayerId != playerId || selected.CurrentHealth <= 0
                        || selected.State == UnitState.Dead) continue;
                    x += selected.SimPosition.x.Raw;
                    z += selected.SimPosition.z.Raw;
                    count++;
                }
                if (count == 0) return false;
                position = new FixedVector3(new Fixed32((int)(x / count)), Fixed32.Zero,
                    new Fixed32((int)(z / count)));
                return true;
            }
            if (selector.Kind == CommanderLocationSelectorKind.WorkedResource
                || selector.Kind == CommanderLocationSelectorKind.VisibleResource)
            {
                if (!selector.ResourceType.HasValue) return false;
                var worked = new HashSet<int>(simulation.UnitRegistry.GetAllUnits()
                    .Where(u => u != null && u.PlayerId == playerId && u.IsVillager
                        && u.CurrentHealth > 0 && u.State != UnitState.Dead
                        && u.TargetResourceNodeId >= 0)
                    .Select(u => u.TargetResourceNodeId));
                resource = simulation.MapData.GetAllResourceNodes()
                    .Where(n => n != null && !n.IsDepleted && n.Type == selector.ResourceType.Value
                        && simulation.FogOfWar.GetVisibility(playerId, n.TileX, n.TileZ) == TileVisibility.Visible
                        && (selector.Kind == CommanderLocationSelectorKind.VisibleResource || worked.Contains(n.Id)))
                    .OrderBy(n => tc == null ? 0L : DistanceSquared(tc, n))
                    .ThenBy(n => n.Id).FirstOrDefault();
                if (resource == null) return false;
                position = resource.Position;
                return true;
            }
            if (selector.Kind != CommanderLocationSelectorKind.VisibleEnemy) return false;
            if (tc == null) return false;
            enemyUnit = simulation.UnitRegistry.GetAllUnits()
                .Where(u => u != null && u.PlayerId >= 0 && u.PlayerId != playerId
                    && !simulation.AreAllies(playerId, u.PlayerId) && u.CurrentHealth > 0
                    && simulation.FogOfWar.GetVisibility(playerId,
                        simulation.MapData.WorldToTile(u.SimPosition).x,
                        simulation.MapData.WorldToTile(u.SimPosition).y) == TileVisibility.Visible)
                .OrderBy(u => DistanceSquared(tc.SimPosition, u.SimPosition)).ThenBy(u => u.Id).FirstOrDefault();
            if (enemyUnit != null) { position = enemyUnit.SimPosition; return true; }
            enemyBuilding = simulation.BuildingRegistry.GetAllBuildings()
                .Where(b => b != null && b.PlayerId >= 0 && b.PlayerId != playerId
                    && !simulation.AreAllies(playerId, b.PlayerId) && !b.IsDestroyed
                    && simulation.FogOfWar.GetVisibility(playerId, b.OriginTileX, b.OriginTileZ) == TileVisibility.Visible)
                .OrderBy(b => DistanceSquared(tc.SimPosition, b.SimPosition)).ThenBy(b => b.Id).FirstOrDefault();
            if (enemyBuilding == null) return false;
            position = enemyBuilding.SimPosition;
            return true;
        }

        private bool TryRally(int playerId, BuildingType? requestedType, FixedVector3 position,
            ResourceNodeData resource, int boundBuildingId, out ICommand command, out string reason)
        {
            command = null;
            BuildingType type = requestedType ?? BuildingType.Barracks;
            BuildingData building = boundBuildingId >= 0
                ? simulation.BuildingRegistry.GetBuilding(boundBuildingId)
                : simulation.BuildingRegistry.GetAllBuildings()
                    .Where(b => b != null && b.PlayerId == playerId && !b.IsDestroyed && !b.IsUnderConstruction
                        && simulation.GetEffectiveBuildingType(b) == type)
                    .OrderBy(b => b.Id).FirstOrDefault();
            if (building != null && (building.PlayerId != playerId || building.IsDestroyed
                || building.IsUnderConstruction || simulation.GetEffectiveBuildingType(building) != type))
                building = null;
            if (building == null) { reason = "No completed owned production building matches the rally selector."; return false; }
            command = new SetRallyPointCommand(playerId, building.Id, position, resource?.Id ?? -1);
            reason = "Resolved a production-building rally point from current visible state.";
            return true;
        }

        private bool TryRepair(CapabilityActionIntent intent, IReadOnlyList<int> boundUnitIds,
            out ICommand command, out string reason)
        {
            command = null;
            if (intent.UnitSelector.Kind != CommanderUnitSelectorKind.Villagers)
            {
                reason = "Repair requires an owned-villager selector.";
                return false;
            }
            BuildingData target = simulation.BuildingRegistry.GetAllBuildings()
                .Where(b => b != null && b.PlayerId == intent.PlayerId && !b.IsDestroyed
                    && !b.IsUnderConstruction && b.CurrentHealth < b.MaxHealth)
                .OrderBy(b => b.Id).FirstOrDefault();
            if (target == null) { reason = "No damaged owned building is available for repair."; return false; }
            if (!TryResolveUnits(intent.PlayerId, intent.UnitSelector, boundUnitIds, out int[] workers))
            { reason = "No eligible owned villagers are available for repair."; return false; }
            command = new RepairBuildingCommand(intent.PlayerId, workers, target.Id);
            reason = "Resolved the nearest deterministic damaged-owned-building repair target.";
            return true;
        }

        private bool TryResearch(CapabilityActionIntent intent, out ICommand command, out string reason)
        {
            command = null;
            if (!intent.Technology.HasValue) { reason = "A technology is required."; return false; }
            if (simulation.HasTechnology(intent.PlayerId, intent.Technology.Value))
            { reason = "The requested technology is already researched."; return false; }
            simulation.GetTechnologySpec(intent.Technology.Value, out int requiredAge, out int foodCost,
                out int goldCost, out BuildingType buildingType, out _);
            if (simulation.GetPlayerAge(intent.PlayerId) < requiredAge)
            {
                reason = "The requested technology requires a later age.";
                return false;
            }
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(intent.PlayerId);
            if (resources.Food < foodCost || resources.Gold < goldCost)
            {
                reason = "The player lacks the canonical research resources.";
                return false;
            }
            BuildingData building = simulation.BuildingRegistry.GetAllBuildings()
                .Where(b => b != null && b.PlayerId == intent.PlayerId && !b.IsDestroyed && !b.IsUnderConstruction
                    && simulation.GetEffectiveBuildingType(b) == buildingType
                    && !b.ResearchQueue.Contains(intent.Technology.Value))
                .OrderBy(b => b.Id).FirstOrDefault();
            if (building == null) { reason = "No completed research building is available."; return false; }
            command = new ResearchCommand(intent.PlayerId, building.Id, intent.Technology.Value);
            reason = "Resolved a canonical research building and technology.";
            return true;
        }

        private static long DistanceSquared(BuildingData building, ResourceNodeData node)
        {
            long dx = building.OriginTileX - node.TileX;
            long dz = building.OriginTileZ - node.TileZ;
            return dx * dx + dz * dz;
        }

        private static long DistanceSquared(FixedVector3 a, FixedVector3 b)
        {
            long dx = a.x.Raw - b.x.Raw;
            long dz = a.z.Raw - b.z.Raw;
            return dx * dx + dz * dz;
        }
    }
}
