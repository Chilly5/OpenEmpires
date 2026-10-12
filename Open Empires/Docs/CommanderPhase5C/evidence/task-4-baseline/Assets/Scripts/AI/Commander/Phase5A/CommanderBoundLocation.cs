using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    // Game-owned snapshot of one semantic anchor. Neither IDs nor coordinates are provider data.
    internal sealed class CommanderBoundLocation
    {
        private enum AnchorKind { Building, Resource, FutureBuild }
        private readonly AnchorKind kind;
        private readonly GameSimulation runtime;
        private readonly CommanderGoalManager owner;
        private readonly int playerId;
        private readonly int anchorId;
        private readonly int originX;
        private readonly int originZ;
        private readonly int footprintWidth;
        private readonly int footprintHeight;
        private readonly BuildingType buildingType;
        private readonly ResourceType resourceType;
        private readonly ResourceSourceKind sourceKind;
        private readonly bool worked;
        private readonly BuildStructureGoal futureSource;

        internal CommanderSemanticPlacementRelation Relation { get; }
        internal int ClearGapTiles { get; }

        private CommanderBoundLocation(AnchorKind kind, GameSimulation runtime,
            CommanderGoalManager owner, int playerId, int anchorId,
            int x, int z, int width, int height, BuildingType buildingType,
            ResourceType resourceType, ResourceSourceKind sourceKind, bool worked,
            BuildStructureGoal futureSource, CommanderSemanticPlacementRelation relation,
            int gap)
        {
            if (runtime == null || owner == null || playerId != owner.PlayerId
                || !ReferenceEquals(owner.Simulation, runtime)
                || gap < CommanderSemanticJson.MinimumClearGapTiles
                || gap > CommanderSemanticJson.MaximumClearGapTiles
                || (relation == CommanderSemanticPlacementRelation.Near && gap != 1)
                || (relation != CommanderSemanticPlacementRelation.Near
                    && relation != CommanderSemanticPlacementRelation.MapWest
                    && relation != CommanderSemanticPlacementRelation.MapEast))
                throw new ArgumentException("Invalid semantic location binding.");
            this.kind = kind;
            this.runtime = runtime;
            this.owner = owner;
            this.playerId = playerId;
            this.anchorId = anchorId;
            originX = x; originZ = z; footprintWidth = width; footprintHeight = height;
            this.buildingType = buildingType;
            this.resourceType = resourceType;
            this.sourceKind = sourceKind;
            this.worked = worked;
            this.futureSource = futureSource;
            Relation = relation;
            ClearGapTiles = gap;
        }

        internal static CommanderBoundLocation FromBuilding(GameSimulation runtime,
            CommanderGoalManager owner, int playerId, BuildingData building,
            CommanderSemanticPlacementRelation relation, int gap)
        {
            if (!ValidBuilding(runtime, playerId, building, null))
                throw new ArgumentException("The requested owned building anchor is unavailable.");
            return new CommanderBoundLocation(AnchorKind.Building, runtime, owner, playerId,
                building.Id, building.OriginTileX, building.OriginTileZ,
                building.TileFootprintWidth, building.TileFootprintHeight,
                runtime.GetEffectiveBuildingType(building), default, default, false,
                null, relation, gap);
        }

        internal static CommanderBoundLocation FromResource(GameSimulation runtime,
            CommanderGoalManager owner, int playerId, ResourceNodeData resource,
            ResourceType type, ResourceSourceKind sourceKind, bool worked,
            CommanderSemanticPlacementRelation relation, int gap)
        {
            if (!ValidResource(runtime, playerId, resource, type, sourceKind, worked))
                throw new ArgumentException("The requested visible resource anchor is unavailable.");
            return new CommanderBoundLocation(AnchorKind.Resource, runtime, owner, playerId,
                resource.Id, resource.TileX, resource.TileZ,
                resource.FootprintWidth, resource.FootprintHeight, default,
                type, sourceKind, worked, null, relation, gap);
        }

        internal static CommanderBoundLocation FromFutureBuild(GameSimulation runtime,
            CommanderGoalManager owner, int playerId, BuildStructureGoal source,
            CommanderSemanticPlacementRelation relation, int gap)
        {
            if (source == null || source.PlayerId != playerId || source.Count != 1)
                throw new ArgumentException("A future placement anchor must have one exact building result.");
            return new CommanderBoundLocation(AnchorKind.FutureBuild, runtime, owner, playerId,
                -1, -1, -1, 0, 0, source.StructureType, default, default, false,
                source, relation, gap);
        }

        internal bool TryResolve(GameSimulation currentRuntime, int currentPlayerId,
            out int x, out int z, out int width, out int height)
        {
            x = z = width = height = 0;
            if (!ReferenceEquals(runtime, currentRuntime) || currentPlayerId != playerId
                || owner.IsDisposed || !ReferenceEquals(owner.Simulation, currentRuntime))
                return false;
            if (kind == AnchorKind.Resource)
            {
                ResourceNodeData resource = runtime.MapData.GetResourceNode(anchorId);
                if (!ValidResource(runtime, playerId, resource, resourceType, sourceKind, worked)
                    || resource.TileX != originX || resource.TileZ != originZ
                    || resource.FootprintWidth != footprintWidth
                    || resource.FootprintHeight != footprintHeight) return false;
                x = resource.TileX; z = resource.TileZ;
                width = resource.FootprintWidth; height = resource.FootprintHeight;
                return true;
            }
            BuildingData building;
            if (kind == AnchorKind.FutureBuild)
            {
                if (futureSource == null || futureSource.PlayerId != playerId
                    || !ReferenceEquals(futureSource.RuntimeOwner, owner)
                    || futureSource.Count != 1
                    || futureSource.Status != CommanderGoalStatus.Completed
                    || futureSource.ResultCaptureTick < 0
                    || futureSource.ResultBuildingIds.Count != 1)
                    return false;
                building = runtime.BuildingRegistry.GetBuilding(futureSource.ResultBuildingIds[0]);
            }
            else building = runtime.BuildingRegistry.GetBuilding(anchorId);
            if (!ValidBuilding(runtime, playerId, building, buildingType)) return false;
            if (kind == AnchorKind.Building
                && (building.OriginTileX != originX || building.OriginTileZ != originZ
                    || building.TileFootprintWidth != footprintWidth
                    || building.TileFootprintHeight != footprintHeight)) return false;
            x = building.OriginTileX; z = building.OriginTileZ;
            width = building.TileFootprintWidth; height = building.TileFootprintHeight;
            return true;
        }

        internal static bool ValidBuilding(GameSimulation runtime, int playerId,
            BuildingData building, BuildingType? expectedType)
        {
            return building != null && building.PlayerId == playerId
                && !building.IsDestroyed && !building.IsUnderConstruction
                && (!expectedType.HasValue
                    || runtime.GetEffectiveBuildingType(building) == expectedType.Value)
                && building.TileFootprintWidth > 0 && building.TileFootprintHeight > 0
                && runtime.FogOfWar.GetVisibility(playerId, building.OriginTileX,
                    building.OriginTileZ) == TileVisibility.Visible;
        }

        internal static bool ValidResource(GameSimulation runtime, int playerId,
            ResourceNodeData resource, ResourceType type, ResourceSourceKind sourceKind,
            bool worked)
        {
            if (resource == null || resource.IsDepleted || resource.Type != type
                || !ResourceSourceRules.IsCompatible(type, sourceKind)
                || !ResourceSourceRules.Matches(resource, sourceKind)
                || !ResourceSourceRules.MatchesOwnedSource(resource, sourceKind, playerId,
                    runtime.BuildingRegistry)
                || !ValidFarmOwner(runtime, playerId, resource)
                || resource.FootprintWidth <= 0 || resource.FootprintHeight <= 0
                || runtime.FogOfWar.GetVisibility(playerId, resource.TileX,
                    resource.TileZ) != TileVisibility.Visible) return false;
            return !worked || IsWorked(runtime, playerId, resource);
        }

        private static bool ValidFarmOwner(GameSimulation runtime, int playerId,
            ResourceNodeData resource)
        {
            if (!resource.IsFarmNode) return true;
            BuildingData farm = runtime.BuildingRegistry.GetBuilding(resource.LinkedBuildingId);
            return farm != null && farm.PlayerId == playerId
                && runtime.GetEffectiveBuildingType(farm) == BuildingType.Farm
                && !farm.IsUnderConstruction && !farm.IsDestroyed;
        }

        internal static bool IsWorked(GameSimulation runtime, int playerId,
            ResourceNodeData resource)
        {
            List<UnitData> units = runtime.UnitRegistry.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                UnitData worker = units[i];
                if (worker == null || worker.PlayerId != playerId || !worker.IsVillager
                    || worker.CurrentHealth <= 0 || worker.State == UnitState.Dead
                    || worker.TargetResourceNodeId != resource.Id
                    || (worker.State != UnitState.Gathering
                        && worker.State != UnitState.MovingToGather
                        && worker.State != UnitState.MovingToDropoff
                        && worker.State != UnitState.DroppingOff)
                    || !ResourceSourceRules.MatchesOwnedSource(resource,
                        worker.GatherSourceKind, playerId, runtime.BuildingRegistry))
                    continue;
                return true;
            }
            return false;
        }
    }
}
