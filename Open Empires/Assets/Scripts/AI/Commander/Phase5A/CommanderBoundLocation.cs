using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    // Game-owned snapshot of one semantic anchor. Neither IDs nor coordinates are provider data.
    internal sealed class CommanderBoundLocation
    {
        private enum AnchorKind { Building, Resource, FutureBuild, PlayerMarker }
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
        private readonly IReadOnlyList<int> resourceSideIds;

        internal CommanderSemanticPlacementRelation Relation { get; }
        internal int ClearGapTiles { get; }
        internal bool IsResourceSide => resourceSideIds != null || kind == AnchorKind.Resource;

        private CommanderBoundLocation(AnchorKind kind, GameSimulation runtime,
            CommanderGoalManager owner, int playerId, int anchorId,
            int x, int z, int width, int height, BuildingType buildingType,
            ResourceType resourceType, ResourceSourceKind sourceKind, bool worked,
            BuildStructureGoal futureSource, CommanderSemanticPlacementRelation relation,
            int gap, IReadOnlyList<int> resourceSideIds = null)
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
            this.resourceSideIds = resourceSideIds;
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

        internal static CommanderBoundLocation FromPlayerMarker(GameSimulation runtime,
            CommanderGoalManager owner, int playerId, int tileX, int tileZ,
            CommanderSemanticPlacementRelation relation, int gap)
        {
            if (!ValidMarker(runtime, playerId, tileX, tileZ))
                throw new ArgumentException("The player destination marker is unavailable or hidden.");
            return new CommanderBoundLocation(AnchorKind.PlayerMarker, runtime, owner, playerId,
                -1, tileX, tileZ, 1, 1, default, default, default, false,
                null, relation, gap);
        }

        private static bool ValidMarker(GameSimulation runtime, int playerId, int x, int z)
        {
            return runtime != null && playerId >= 0 && playerId < runtime.FogOfWar.PlayerCount
                && x >= 0 && z >= 0 && x < runtime.MapData.Width && z < runtime.MapData.Height
                && runtime.FogOfWar.GetVisibility(playerId, x, z) == TileVisibility.Visible;
        }

        // A bounded, admission-time visible source set tied to one frozen owned anchor.
        // Provider supplies neither runtime node IDs nor world coordinates.
        internal static CommanderBoundLocation FromResourceSide(GameSimulation runtime,
            CommanderGoalManager owner, int playerId, BuildingData anchor,
            ResourceType type, ResourceSourceKind sourceKind, int gap)
        {
            if (!ValidBuilding(runtime, playerId, anchor, BuildingType.TownCenter)
                || !ResourceSourceRules.IsCompatible(type, sourceKind))
                throw new ArgumentException("The owned Town Center or resource/source pair is unavailable.");
            var ids = new List<int>();
            foreach (ResourceNodeData resource in runtime.MapData.GetAllResourceNodes())
                if (ValidResource(runtime, playerId, resource, type, sourceKind, false)
                    && (long)resource.TileX + resource.FootprintWidth + gap <= anchor.OriginTileX)
                    ids.Add(resource.Id);
            ids.Sort();
            if (ids.Count < 1 || ids.Count > 200)
                throw new ArgumentException("No bounded visible source set lies west of the owned Town Center.");
            return new CommanderBoundLocation(AnchorKind.Building, runtime, owner, playerId,
                anchor.Id, anchor.OriginTileX, anchor.OriginTileZ,
                anchor.TileFootprintWidth, anchor.TileFootprintHeight,
                BuildingType.TownCenter, type, sourceKind, false, null,
                CommanderSemanticPlacementRelation.MapWest, gap, Array.AsReadOnly(ids.ToArray()));
        }

        internal bool AllowsResource(GameSimulation currentRuntime, int currentPlayerId, ResourceNodeData resource)
        {
            if (kind == AnchorKind.Resource)
                return resource != null && resource.Id == anchorId
                    && TryResolve(currentRuntime, currentPlayerId, out _, out _, out _, out _);
            if (resourceSideIds == null || resource == null
                || !TryResolve(currentRuntime, currentPlayerId, out int x, out _, out _, out _)
                || (long)resource.TileX + resource.FootprintWidth + ClearGapTiles > x
                || !ValidResource(runtime, playerId, resource, resourceType, sourceKind, false)) return false;
            foreach (int id in resourceSideIds)
                if (id == resource.Id) return true;
            return false;
        }

        internal bool TryResolveResourceSidePoint(GameSimulation currentRuntime, int currentPlayerId,
            out FixedVector3 position, out int resourceId)
        {
            position = default;
            resourceId = -1;
            if (kind == AnchorKind.Resource)
            {
                if (!TryResolve(currentRuntime, currentPlayerId, out int x, out int z,
                    out _, out _)) return false;
                position = runtime.MapData.TileToWorldFixed(x, z);
                resourceId = anchorId;
                return true;
            }
            if (resourceSideIds == null) return false;
            foreach (int id in resourceSideIds)
            {
                ResourceNodeData candidate = runtime.MapData.GetResourceNode(id);
                if (!AllowsResource(currentRuntime, currentPlayerId, candidate)) continue;
                position = runtime.MapData.TileToWorldFixed(candidate.TileX, candidate.TileZ);
                resourceId = id;
                return true;
            }
            return false;
        }

        internal bool AllowsMoveTile(GameSimulation currentRuntime, int currentPlayerId,
            int resourceId, int x, int z)
        {
            if (kind == AnchorKind.Resource)
                return resourceId == anchorId
                    && TryResolve(currentRuntime, currentPlayerId, out int resourceX,
                        out int resourceZ, out _, out _)
                    && Math.Abs(x - resourceX) <= 10 && Math.Abs(z - resourceZ) <= 10;
            if (resourceSideIds == null || !TryResolve(currentRuntime, currentPlayerId,
                    out int anchorX, out _, out _, out _)
                || (long)x + ClearGapTiles >= anchorX) return false;
            ResourceNodeData resource = runtime.MapData.GetResourceNode(resourceId);
            return AllowsResource(currentRuntime, currentPlayerId, resource)
                && Math.Abs(x - resource.TileX) <= 10
                && Math.Abs(z - resource.TileZ) <= 10;
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
            if (kind == AnchorKind.PlayerMarker)
            {
                if (!ValidMarker(runtime, playerId, originX, originZ)) return false;
                x = originX; z = originZ; width = height = 1;
                return true;
            }
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
            return CountOwnedWorkers(runtime, playerId, resource) > 0;
        }

        internal static int CountOwnedWorkers(GameSimulation runtime, int playerId,
            ResourceNodeData resource)
        {
            if (resource == null) return 0;
            int count = 0;
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
                count++;
            }
            return count;
        }
    }
}
