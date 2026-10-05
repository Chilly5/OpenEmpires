using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    // Resolves provider-authored semantic references using trusted, current simulation state.
    // Concrete entities remain game-side; they are never added to the provider contract.
    public sealed class CommanderSemanticReferenceResolver
    {
        private readonly GameSimulation simulation;

        public CommanderSemanticReferenceResolver(GameSimulation simulation)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        }

        public bool TryResolveOwnedAnchor(int playerId, CommanderSemanticAnchorSelector selector,
            int? ordinal, out BuildingData building)
        {
            building = null;
            if (!IsValidPlayer(playerId)) return false;

            switch (selector)
            {
                case CommanderSemanticAnchorSelector.MyTownCenter:
                    building = FindOwnedTownCenter(playerId, ordinal);
                    return building != null;

                case CommanderSemanticAnchorSelector.MyBarracks:
                    if (ordinal.HasValue) return false;
                    BuildingData townCenter = FindOwnedTownCenter(playerId, null);
                    if (townCenter == null) return false;
                    building = FindNearestOwnedBarracks(playerId, townCenter);
                    return building != null;

                case CommanderSemanticAnchorSelector.WorkedResource:
                    return false;

                default:
                    return false;
            }
        }

        public bool TryResolveVisibleResourceNearestToTownCenter(int playerId, ResourceType type,
            int? townCenterOrdinal, out ResourceNodeData node)
        {
            node = null;
            if (!IsValidPlayer(playerId) || !Enum.IsDefined(typeof(ResourceType), type)) return false;
            BuildingData townCenter = FindOwnedTownCenter(playerId, townCenterOrdinal);
            if (townCenter == null) return false;

            long bestDistance = long.MaxValue;
            IReadOnlyList<ResourceNodeData> resources = simulation.MapData.GetAllResourceNodes();
            for (int i = 0; i < resources.Count; i++)
            {
                ResourceNodeData candidate = resources[i];
                if (candidate == null || candidate.Type != type || candidate.IsDepleted
                    || simulation.FogOfWar.GetVisibility(playerId, candidate.TileX, candidate.TileZ)
                        != TileVisibility.Visible) continue;

                long distance = DistanceSquared(townCenter.OriginTileX, townCenter.OriginTileZ,
                    townCenter.TileFootprintWidth, townCenter.TileFootprintHeight,
                    candidate.TileX, candidate.TileZ,
                    candidate.FootprintWidth, candidate.FootprintHeight);
                if (node == null || distance < bestDistance
                    || (distance == bestDistance && candidate.Id < node.Id))
                {
                    node = candidate;
                    bestDistance = distance;
                }
            }
            return node != null;
        }

        public bool TryResolveWorkedResource(int playerId, ResourceType type,
            out ResourceNodeData node)
        {
            node = null;
            if (!IsValidPlayer(playerId) || !Enum.IsDefined(typeof(ResourceType), type)) return false;
            var worked = new HashSet<int>();
            List<UnitData> units = simulation.UnitRegistry.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
                if (units[i] != null && units[i].PlayerId == playerId && units[i].IsVillager
                    && units[i].TargetResourceNodeId >= 0)
                    worked.Add(units[i].TargetResourceNodeId);
            IReadOnlyList<ResourceNodeData> resources = simulation.MapData.GetAllResourceNodes();
            for (int i = 0; i < resources.Count; i++)
            {
                ResourceNodeData candidate = resources[i];
                if (candidate == null || candidate.Type != type || candidate.IsDepleted
                    || !worked.Contains(candidate.Id)
                    || simulation.FogOfWar.GetVisibility(playerId, candidate.TileX, candidate.TileZ)
                        != TileVisibility.Visible) continue;
                if (node == null || candidate.Id < node.Id) node = candidate;
            }
            return node != null;
        }

        private bool IsValidPlayer(int playerId)
        {
            return playerId >= 0 && playerId < simulation.FogOfWar.PlayerCount;
        }

        private BuildingData FindOwnedTownCenter(int playerId, int? ordinal)
        {
            int rank = ordinal ?? 1;
            if (rank < CommanderSemanticJson.MinimumTownCenterOrdinal
                || rank > CommanderSemanticJson.MaximumTownCenterOrdinal) return null;

            // Registry removal uses swap-with-last, so its enumeration order is not ID order.
            // Select the requested order statistic without allocating or mutating the registry.
            BuildingData previous = null;
            List<BuildingData> buildings = simulation.BuildingRegistry.GetAllBuildings();
            for (int selected = 0; selected < rank; selected++)
            {
                BuildingData next = null;
                for (int i = 0; i < buildings.Count; i++)
                {
                    BuildingData candidate = buildings[i];
                    if (!IsOwnedSurvivingType(candidate, playerId, BuildingType.TownCenter)
                        || (previous != null && candidate.Id <= previous.Id)) continue;
                    if (next == null || candidate.Id < next.Id) next = candidate;
                }
                if (next == null) return null;
                previous = next;
            }
            return previous;
        }

        private BuildingData FindNearestOwnedBarracks(int playerId, BuildingData townCenter)
        {
            BuildingData best = null;
            long bestDistance = long.MaxValue;
            List<BuildingData> buildings = simulation.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                BuildingData candidate = buildings[i];
                if (!IsOwnedSurvivingType(candidate, playerId, BuildingType.Barracks)) continue;

                long distance = DistanceSquared(townCenter.OriginTileX, townCenter.OriginTileZ,
                    townCenter.TileFootprintWidth, townCenter.TileFootprintHeight,
                    candidate.OriginTileX, candidate.OriginTileZ,
                    candidate.TileFootprintWidth, candidate.TileFootprintHeight);
                if (best == null || distance < bestDistance
                    || (distance == bestDistance && candidate.Id < best.Id))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private bool IsOwnedSurvivingType(BuildingData candidate, int playerId, BuildingType type)
        {
            return candidate != null && candidate.PlayerId == playerId && !candidate.IsDestroyed
                && simulation.GetEffectiveBuildingType(candidate) == type;
        }

        private static long DistanceSquared(int leftX, int leftZ, int leftWidth, int leftHeight,
            int rightX, int rightZ, int rightWidth, int rightHeight)
        {
            // Double tile-center coordinates keep odd footprint sizes exact without floats.
            long dx = 2L * leftX + leftWidth - (2L * rightX + rightWidth);
            long dz = 2L * leftZ + leftHeight - (2L * rightZ + rightHeight);
            return dx * dx + dz * dz;
        }
    }
}
