using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public class ResourceGatheringSystem
    {
        private const int StrikeCooldownTicks = 20; // visual strike every 1s at 20 TPS

        private static readonly Fixed32 GatherRange = Fixed32.FromFloat(1.5f);
        private static readonly Fixed32 TurnRate = Fixed32.FromFloat(0.3f);
        private static readonly Fixed32 FacingThreshold = Fixed32.FromFloat(0.9f); // cos(~26 deg)
        private static readonly Fixed32 ChaseStuckThreshold = Fixed32.FromFloat(1.5f); // 1.5 seconds (was 5)
        private static readonly Fixed32 SheepIdlePreventionRange = Fixed32.FromInt(7);

        // Pre-built lookups rebuilt each tick to avoid O(N²) nested scans
        private readonly Dictionary<int, HashSet<Vector2Int>> occupiedTilesByNode = new Dictionary<int, HashSet<Vector2Int>>();
        private readonly Dictionary<int, HashSet<Vector2Int>> occupiedTilesByDropoff = new Dictionary<int, HashSet<Vector2Int>>();
        private readonly HashSet<int> occupiedFarmNodeIds = new HashSet<int>();

        private void BuildOccupancyLookups(UnitRegistry unitRegistry, MapData mapData)
        {
            occupiedTilesByNode.Clear();
            occupiedTilesByDropoff.Clear();
            occupiedFarmNodeIds.Clear();

            var allUnits = unitRegistry.GetAllUnits();
            for (int i = 0; i < allUnits.Count; i++)
            {
                var u = allUnits[i];
                if (u.State == UnitState.Dead) continue;

                // Track tiles occupied at resource nodes (for gathering/moving-to-gather)
                if ((u.State == UnitState.MovingToGather || u.State == UnitState.Gathering) && u.TargetResourceNodeId >= 0)
                {
                    if (!occupiedTilesByNode.TryGetValue(u.TargetResourceNodeId, out var nodeSet))
                    {
                        nodeSet = new HashSet<Vector2Int>();
                        occupiedTilesByNode[u.TargetResourceNodeId] = nodeSet;
                    }
                    nodeSet.Add(mapData.WorldToTile(u.FinalDestination));

                    // Track occupied farm nodes
                    var node = mapData.GetResourceNode(u.TargetResourceNodeId);
                    if (node != null && node.IsFarmNode)
                        occupiedFarmNodeIds.Add(u.TargetResourceNodeId);
                }

                // Track tiles occupied at drop-off buildings
                if ((u.State == UnitState.MovingToDropoff || u.State == UnitState.DroppingOff) && u.DropOffBuildingId >= 0)
                {
                    if (!occupiedTilesByDropoff.TryGetValue(u.DropOffBuildingId, out var dropSet))
                    {
                        dropSet = new HashSet<Vector2Int>();
                        occupiedTilesByDropoff[u.DropOffBuildingId] = dropSet;
                    }
                    dropSet.Add(mapData.WorldToTile(u.FinalDestination));

                    // Units dropping off from a farm still occupy that farm
                    if (u.TargetResourceNodeId >= 0)
                    {
                        var dropNode = mapData.GetResourceNode(u.TargetResourceNodeId);
                        if (dropNode != null && dropNode.IsFarmNode)
                            occupiedFarmNodeIds.Add(u.TargetResourceNodeId);
                    }
                }
            }
        }

        private HashSet<Vector2Int> GetOccupiedTilesForNode(int nodeId, UnitData excludeUnit, MapData mapData)
        {
            if (!occupiedTilesByNode.TryGetValue(nodeId, out var set))
                return null;
            // Remove the excluded unit's tile (cheap since we know the tile)
            var excludeTile = mapData.WorldToTile(excludeUnit.FinalDestination);
            if (set.Contains(excludeTile) && excludeUnit.TargetResourceNodeId == nodeId
                && (excludeUnit.State == UnitState.MovingToGather || excludeUnit.State == UnitState.Gathering))
            {
                var copy = new HashSet<Vector2Int>(set);
                copy.Remove(excludeTile);
                return copy;
            }
            return set;
        }

        private HashSet<Vector2Int> GetOccupiedTilesForDropoff(int buildingId, UnitData excludeUnit, MapData mapData)
        {
            if (!occupiedTilesByDropoff.TryGetValue(buildingId, out var set))
                return null;
            var excludeTile = mapData.WorldToTile(excludeUnit.FinalDestination);
            if (set.Contains(excludeTile) && excludeUnit.DropOffBuildingId == buildingId
                && (excludeUnit.State == UnitState.MovingToDropoff || excludeUnit.State == UnitState.DroppingOff))
            {
                var copy = new HashSet<Vector2Int>(set);
                copy.Remove(excludeTile);
                return copy;
            }
            return set;
        }

        public void Tick(UnitRegistry unitRegistry, MapData mapData, ResourceManager resourceManager,
            BuildingRegistry buildingRegistry, SimulationConfig config, Fixed32 tickDuration, int currentTick)
        {
            BuildOccupancyLookups(unitRegistry, mapData);

            foreach (var unit in unitRegistry.GetAllUnits())
            {
                // --- DroppingOff: deposit resources at TC ---
                if (unit.State == UnitState.DroppingOff)
                {
                    ProcessDropOff(unit, mapData, resourceManager, buildingRegistry, unitRegistry, currentTick);
                    continue;
                }

                if (unit.State != UnitState.Gathering)
                    continue;

                var node = mapData.GetResourceNode(unit.TargetResourceNodeId);

                // Farm occupancy: if another villager is already on this farm, find another farm
                if (node != null && node.IsFarmNode && IsFarmNodeOccupied(node.Id, unit))
                {
                    if (!TryReassignToNearbyFarm(unit, mapData, unitRegistry, buildingRegistry))
                    {
                        unit.State = UnitState.Idle;
                        unit.TargetResourceNodeId = -1;
                    }
                    continue;
                }

                if (node == null || node.IsDepleted)
                {
                    // Node gone — try to find nearby same-type node within vision
                    FixedVector3 searchPos = node != null ? node.Position : unit.SimPosition;
                    ResourceType searchType = node != null ? node.Type : unit.CarriedResourceType;
                    if (TryGatherNearbyNode(unit, searchPos, searchType, mapData, unitRegistry, buildingRegistry))
                        continue;
                    // No nearby food node — try to auto-slaughter a nearby sheep
                    if (searchType == ResourceType.Food && TryAutoSlaughterNearbySheep(unit, searchPos, unitRegistry, mapData, buildingRegistry))
                        continue;
                    // No nearby node — if carrying, drop off what we have
                    if (unit.CarriedResourceAmount > 0)
                    {
                        int tcId = FindNearestDropOffBuilding(unit, buildingRegistry);
                        if (tcId >= 0)
                        {
                            InitiateDropOffTrip(unit, tcId, buildingRegistry, mapData, unitRegistry);
                            continue;
                        }
                    }
                    unit.State = UnitState.Idle;
                    unit.TargetResourceNodeId = -1;
                    continue;
                }

                // Range check — measure to nearest point on footprint edge, not center
                Fixed32 halfW = Fixed32.FromInt(node.FootprintWidth) / Fixed32.FromInt(2);
                Fixed32 halfH = Fixed32.FromInt(node.FootprintHeight) / Fixed32.FromInt(2);
                Fixed32 nearestX = Fixed32.Max(node.Position.x - halfW, Fixed32.Min(unit.SimPosition.x, node.Position.x + halfW));
                Fixed32 nearestZ = Fixed32.Max(node.Position.z - halfH, Fixed32.Min(unit.SimPosition.z, node.Position.z + halfH));
                FixedVector3 toNode = new FixedVector3(nearestX - unit.SimPosition.x, Fixed32.Zero, nearestZ - unit.SimPosition.z);
                Fixed32 effectiveRange = GatherRange;
                // Overflow guard: skip multiply if axis distance exceeds range
                if (Fixed32.Abs(toNode.x) > effectiveRange || Fixed32.Abs(toNode.z) > effectiveRange)
                {
                    // Too far — chase toward node
                    Fixed32 absDx = Fixed32.Abs(toNode.x);
                    Fixed32 absDz = Fixed32.Abs(toNode.z);
                    Fixed32 approxDist = absDx > absDz ? absDx : absDz;
                    if (approxDist.Raw > 0)
                    {
                        FixedVector3 dir = toNode / approxDist;
                        Fixed32 step = unit.MoveSpeed * tickDuration;
                        FixedVector3 newPos = unit.SimPosition + dir * step;
                        Vector2Int newTile = mapData.WorldToTile(newPos);
                        if (mapData.IsWalkable(newTile.x, newTile.y))
                            unit.SimPosition = newPos;
                        unit.SimFacing = dir;
                    }
                    unit.GatherTimer += tickDuration;
                    if (unit.GatherTimer > ChaseStuckThreshold)
                    {
                        unit.GatherTimer = Fixed32.Zero;
                        if (node.Type != ResourceType.Food
                            || !TryAutoSlaughterNearbySheep(unit, unit.SimPosition, unitRegistry, mapData, buildingRegistry, SheepIdlePreventionRange))
                        {
                            unit.State = UnitState.Idle;
                            unit.TargetResourceNodeId = -1;
                        }
                    }
                    continue;
                }
                Fixed32 distSq = toNode.x * toNode.x + toNode.z * toNode.z;
                Fixed32 effectiveRangeSq = effectiveRange * effectiveRange;

                if (distSq > effectiveRangeSq)
                {
                    // Too far — chase toward node
                    Fixed32 dist = Fixed32.Sqrt(distSq);
                    if (dist.Raw > 0)
                    {
                        FixedVector3 dir = toNode / dist;
                        Fixed32 step = unit.MoveSpeed * tickDuration;
                        FixedVector3 newPos = unit.SimPosition + dir * step;
                        Vector2Int newTile = mapData.WorldToTile(newPos);
                        if (mapData.IsWalkable(newTile.x, newTile.y))
                            unit.SimPosition = newPos;
                        unit.SimFacing = dir;
                    }
                    unit.GatherTimer += tickDuration;
                    if (unit.GatherTimer > ChaseStuckThreshold)
                    {
                        unit.GatherTimer = Fixed32.Zero;
                        if (node.Type != ResourceType.Food
                            || !TryAutoSlaughterNearbySheep(unit, unit.SimPosition, unitRegistry, mapData, buildingRegistry, SheepIdlePreventionRange))
                        {
                            unit.State = UnitState.Idle;
                            unit.TargetResourceNodeId = -1;
                        }
                    }
                    continue;
                }

                // Turn toward node
                Fixed32 mag = Fixed32.Sqrt(distSq);
                if (mag.Raw > 0)
                {
                    FixedVector3 targetDir = toNode / mag;

                    FixedVector3 newFacing = new FixedVector3(
                        unit.SimFacing.x + (targetDir.x - unit.SimFacing.x) * TurnRate,
                        Fixed32.Zero,
                        unit.SimFacing.z + (targetDir.z - unit.SimFacing.z) * TurnRate
                    );
                    Fixed32 newFacingMag = newFacing.Magnitude();
                    if (newFacingMag.Raw > 0)
                        unit.SimFacing = newFacing / newFacingMag;

                    unit.HasTargetFacing = true;
                    unit.TargetFacing = targetDir;

                    // Check if facing the node
                    Fixed32 dot = unit.SimFacing.x * targetDir.x + unit.SimFacing.z * targetDir.z;
                    if (dot < FacingThreshold)
                        continue; // still turning, don't gather yet
                }

                // In range and facing — strike cooldown drives both visual and harvest
                if (unit.AttackCooldownRemaining > 0)
                {
                    unit.AttackCooldownRemaining--;
                }
                else
                {
                    int cooldown = StrikeCooldownTicks;
                    if (node.IsFarmNode && IsFarmInfluencedByMill(node, unit.PlayerId, buildingRegistry, config.MillInfluenceRadius))
                        cooldown = StrikeCooldownTicks * (100 - config.MillInfluenceGatherBonusPercent) / 100;
                    unit.AttackCooldownRemaining = cooldown;
                    unit.LastAttackTick = currentTick;
                    unit.LastAttackTargetPos = node.Position;
                    node.LastDamageTick = currentTick;
                    node.LastDamageFromPos = unit.SimPosition;

                    // Harvest 1 resource per strike into carry buffer
                    int harvested = node.Harvest(1);
                    if (harvested > 0)
                    {
                        unit.CarriedResourceType = node.Type;
                        unit.CarriedResourceAmount += harvested;
                        unit.GatherTimer = Fixed32.Zero;
                    }

                    // Clear tile when node depletes (deterministic sim layer)
                    if (node.IsDepleted)
                        mapData.ClearResourceTile(node.Id);

                    // Full — drop off at TC
                    if (unit.CarriedResourceAmount >= unit.CarryCapacity)
                    {
                        int tcId = FindNearestDropOffBuilding(unit, buildingRegistry);
                        if (tcId >= 0)
                        {
                            InitiateDropOffTrip(unit, tcId, buildingRegistry, mapData, unitRegistry);
                        }
                        // If no TC exists, villager stays at node (full but can't drop off)
                    }
                    else if (node.IsDepleted)
                    {
                        // Node depleted but not full — seek nearby same-type within vision
                        if (!TryGatherNearbyNode(unit, node.Position, node.Type, mapData, unitRegistry, buildingRegistry))
                        {
                            // No nearby food node — try to auto-slaughter a nearby sheep
                            if (node.Type == ResourceType.Food && TryAutoSlaughterNearbySheep(unit, node.Position, unitRegistry, mapData, buildingRegistry))
                            {
                                // Will walk to sheep and slaughter it
                            }
                            // No nearby node — drop off what we have, then Idle
                            else if (unit.CarriedResourceAmount > 0)
                            {
                                int tcId = FindNearestDropOffBuilding(unit, buildingRegistry);
                                if (tcId >= 0)
                                {
                                    InitiateDropOffTrip(unit, tcId, buildingRegistry, mapData, unitRegistry);
                                }
                                else
                                {
                                    unit.State = UnitState.Idle;
                                    unit.TargetResourceNodeId = -1;
                                }
                            }
                            else
                            {
                                unit.State = UnitState.Idle;
                                unit.TargetResourceNodeId = -1;
                            }
                        }
                    }
                }
            }
        }

        private void ProcessDropOff(UnitData unit, MapData mapData, ResourceManager resourceManager,
            BuildingRegistry buildingRegistry, UnitRegistry unitRegistry, int currentTick)
        {
            // Validate drop-off building still exists
            var building = buildingRegistry.GetBuilding(unit.DropOffBuildingId);
            if (building == null || building.IsDestroyed || building.IsUnderConstruction)
            {
                // TC destroyed — find another
                int newTcId = FindNearestDropOffBuilding(unit, buildingRegistry);
                if (newTcId >= 0)
                {
                    InitiateDropOffTrip(unit, newTcId, buildingRegistry, mapData, unitRegistry);
                    return;
                }
                // No TC available — go Idle, keep resources
                unit.State = UnitState.Idle;
                unit.DropOffBuildingId = -1;
                return;
            }

            // Deposit resources
            if (unit.CarriedResourceAmount > 0)
            {
                unit.LastDepositTick = currentTick;
                unit.LastDepositAmount = unit.CarriedResourceAmount;
                unit.LastDepositResourceType = unit.CarriedResourceType;
                resourceManager.AddResource(unit.PlayerId, unit.CarriedResourceType, unit.CarriedResourceAmount);
                unit.CarriedResourceAmount = 0;
            }
            unit.DropOffBuildingId = -1;

            // Manual drop-off (player right-clicked TC): go Idle, clear target
            if (unit.PlayerCommanded)
            {
                unit.State = UnitState.Idle;
                unit.TargetResourceNodeId = -1;
                unit.PlayerCommanded = false;
                return;
            }

            // Auto drop-off: return to resource node
            var node = mapData.GetResourceNode(unit.TargetResourceNodeId);
            if (node == null || node.IsDepleted)
            {
                // Original node depleted — search near its position for another same-type
                FixedVector3 searchPos = node != null ? node.Position : unit.SimPosition;
                ResourceType searchType = node != null ? node.Type : unit.CarriedResourceType;
                if (!TryGatherNearbyNode(unit, searchPos, searchType, mapData, unitRegistry, buildingRegistry))
                {
                    // No nearby food node — try to auto-slaughter a nearby sheep
                    if (searchType == ResourceType.Food && TryAutoSlaughterNearbySheep(unit, searchPos, unitRegistry, mapData, buildingRegistry))
                        return;
                    unit.State = UnitState.Idle;
                    unit.TargetResourceNodeId = -1;
                }
                return;
            }

            // Use pre-built occupiedTiles lookup
            var occupiedTiles = GetOccupiedTilesForNode(unit.TargetResourceNodeId, unit, mapData);

            // Farm: check if another villager took this farm while we were dropping off
            if (node.IsFarmNode)
            {
                if (IsFarmNodeOccupied(node.Id, unit))
                {
                    if (!TryReassignToNearbyFarm(unit, mapData, unitRegistry, buildingRegistry))
                    {
                        unit.State = UnitState.Idle;
                        unit.TargetResourceNodeId = -1;
                    }
                    return;
                }
            }

            // Path back to resource node
            Vector2Int startTile = mapData.WorldToTile(unit.SimPosition);
            Vector2Int targetTile;
            if (node.IsFarmNode)
            {
                // Farm: walk onto the farm tile directly
                targetTile = mapData.WorldToTile(node.Position);
            }
            else
            {
                Vector2Int nodeOrigin = new Vector2Int(node.TileX, node.TileZ);
                targetTile = FindNearestWalkableAdjacentTileForResource(nodeOrigin, node.FootprintWidth, node.FootprintHeight, unit.SimPosition, mapData, occupiedTiles);
            }
            var path = GridPathfinder.FindPath(mapData, startTile, targetTile, unit.PlayerId, buildingRegistry);
            if (path.Count > 0)
            {
                unit.SetPath(path);
                unit.FinalDestination = mapData.TileToWorldFixed(targetTile.x, targetTile.y);
                unit.State = UnitState.MovingToGather;
            }
            else if (!TryGatherNearbyNode(unit, node.Position, node.Type, mapData, unitRegistry, buildingRegistry))
            {
                if (node.Type != ResourceType.Food
                    || !TryAutoSlaughterNearbySheep(unit, unit.SimPosition, unitRegistry, mapData, buildingRegistry, SheepIdlePreventionRange))
                {
                    unit.State = UnitState.Idle;
                    unit.TargetResourceNodeId = -1;
                }
            }
        }

        private int FindNearestDropOffBuilding(UnitData unit, BuildingRegistry buildingRegistry)
        {
            int bestId = -1;
            Fixed32 bestDist = new Fixed32(int.MaxValue);

            var buildings = buildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (b.PlayerId != unit.PlayerId) continue;
                if (b.IsDestroyed || b.IsUnderConstruction) continue;
                if (!GameSimulation.IsDropOffBuilding(b.Type)) continue;
                if (!GameSimulation.AcceptsResourceType(b.Type, unit.CarriedResourceType)) continue;

                FixedVector3 diff = b.SimPosition - unit.SimPosition;
                Fixed32 absDx = Fixed32.Abs(diff.x);
                Fixed32 absDz = Fixed32.Abs(diff.z);
                // Use Chebyshev distance (max axis) as overflow-safe approximation for nearest check
                Fixed32 dist = absDx > absDz ? absDx : absDz;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestId = b.Id;
                }
            }
            return bestId;
        }

        private void InitiateDropOffTrip(UnitData unit, int buildingId, BuildingRegistry buildingRegistry, MapData mapData, UnitRegistry unitRegistry)
        {
            var building = buildingRegistry.GetBuilding(buildingId);
            if (building == null) return;

            // Use pre-built occupiedTiles lookup for drop-off
            var occupiedTiles = GetOccupiedTilesForDropoff(buildingId, unit, mapData);

            unit.DropOffBuildingId = buildingId;
            Vector2Int adjTile = FindNearestWalkableAdjacentTile(building, unit.SimPosition, mapData, occupiedTiles);
            Vector2Int startTile = mapData.WorldToTile(unit.SimPosition);
            var path = GridPathfinder.FindPath(mapData, startTile, adjTile, unit.PlayerId, buildingRegistry);
            if (path.Count > 0)
            {
                unit.SetPath(path);
                unit.FinalDestination = mapData.TileToWorldFixed(adjTile.x, adjTile.y);
                unit.State = UnitState.MovingToDropoff;
            }
            else
            {
                // Already adjacent — drop off immediately
                unit.State = UnitState.DroppingOff;
            }
        }

        private Vector2Int FindNearestWalkableAdjacentTile(BuildingData building, FixedVector3 unitPos, MapData mapData, HashSet<Vector2Int> occupiedTiles = null)
        {
            Vector2Int unitTile = mapData.WorldToTile(unitPos);
            Vector2Int best = unitTile;
            int bestDistSq = int.MaxValue;

            int minX = building.OriginTileX - 1;
            int maxX = building.OriginTileX + building.TileFootprintWidth;
            int minZ = building.OriginTileZ - 1;
            int maxZ = building.OriginTileZ + building.TileFootprintHeight;

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (x >= building.OriginTileX && x < building.OriginTileX + building.TileFootprintWidth &&
                        z >= building.OriginTileZ && z < building.OriginTileZ + building.TileFootprintHeight)
                        continue;

                    if (!mapData.IsWalkable(x, z)) continue;
                    if (occupiedTiles != null && occupiedTiles.Contains(new Vector2Int(x, z))) continue;

                    int dx = x - unitTile.x;
                    int dz = z - unitTile.y;
                    int distSq = dx * dx + dz * dz;
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        best = new Vector2Int(x, z);
                    }
                }
            }
            return best;
        }

        private bool TryGatherNearbyNode(UnitData unit, FixedVector3 searchPos, ResourceType type, MapData mapData, UnitRegistry unitRegistry, BuildingRegistry buildingRegistry)
        {
            int newNodeId = FindNearestSameTypeNode(searchPos, type, unit.DetectionRange, mapData);
            if (newNodeId < 0) return false;

            var newNode = mapData.GetResourceNode(newNodeId);
            if (newNode == null) return false;

            unit.TargetResourceNodeId = newNodeId;
            unit.GatherTimer = Fixed32.Zero;

            // Use pre-built occupiedTiles lookup
            var occupiedTiles = GetOccupiedTilesForNode(newNodeId, unit, mapData);

            Vector2Int startTile = mapData.WorldToTile(unit.SimPosition);
            Vector2Int targetTile;
            if (newNode.IsFarmNode)
            {
                targetTile = mapData.WorldToTile(newNode.Position);
            }
            else
            {
                Vector2Int nodeOrigin = new Vector2Int(newNode.TileX, newNode.TileZ);
                targetTile = FindNearestWalkableAdjacentTileForResource(nodeOrigin, newNode.FootprintWidth, newNode.FootprintHeight, unit.SimPosition, mapData, occupiedTiles);
            }
            var path = GridPathfinder.FindPath(mapData, startTile, targetTile, unit.PlayerId, buildingRegistry);
            if (path.Count > 0)
            {
                unit.SetPath(path);
                unit.FinalDestination = mapData.TileToWorldFixed(targetTile.x, targetTile.y);
                unit.State = UnitState.MovingToGather;
            }
            else
            {
                unit.State = UnitState.Idle;
                unit.TargetResourceNodeId = -1;
                return false;
            }
            return true;
        }

        private int FindNearestSameTypeNode(FixedVector3 searchPos, ResourceType type, Fixed32 visionRange, MapData mapData)
        {
            int bestId = -1;
            Fixed32 visionRangeSq = visionRange * visionRange;
            Fixed32 bestDistSq = visionRangeSq;

            foreach (var node in mapData.GetAllResourceNodes())
            {
                if (node.IsDepleted) continue;
                if (node.Type != type) continue;
                if (node.IsFarmNode) continue; // don't auto-reassign to farms

                FixedVector3 diff = node.Position - searchPos;
                // Overflow guard: skip if axis distance exceeds vision range
                if (Fixed32.Abs(diff.x) > visionRange || Fixed32.Abs(diff.z) > visionRange) continue;

                Fixed32 distSq = diff.x * diff.x + diff.z * diff.z;
                if (distSq < bestDistSq || (distSq == bestDistSq && node.Id < bestId))
                {
                    bestDistSq = distSq;
                    bestId = node.Id;
                }
            }
            return bestId;
        }

        private bool TryReassignToNearbyFarm(UnitData unit, MapData mapData, UnitRegistry unitRegistry, BuildingRegistry buildingRegistry)
        {
            Fixed32 searchRange = Fixed32.FromInt(30);
            Fixed32 searchRangeSq = searchRange * searchRange;
            int bestId = -1;
            Fixed32 bestDistSq = searchRangeSq;

            foreach (var candidate in mapData.GetAllResourceNodes())
            {
                if (!candidate.IsFarmNode || candidate.IsDepleted) continue;
                if (candidate.Id == unit.TargetResourceNodeId) continue;
                if (IsFarmNodeOccupied(candidate.Id, unit)) continue;

                FixedVector3 diff = candidate.Position - unit.SimPosition;
                if (Fixed32.Abs(diff.x) > searchRange || Fixed32.Abs(diff.z) > searchRange) continue;

                Fixed32 distSq = diff.x * diff.x + diff.z * diff.z;
                if (distSq < bestDistSq || (distSq == bestDistSq && candidate.Id < bestId))
                {
                    bestDistSq = distSq;
                    bestId = candidate.Id;
                }
            }

            if (bestId < 0) return false;

            var newFarm = mapData.GetResourceNode(bestId);
            if (newFarm == null) return false;

            Vector2Int farmTile = mapData.WorldToTile(newFarm.Position);
            Vector2Int startTile = mapData.WorldToTile(unit.SimPosition);
            var path = GridPathfinder.FindPath(mapData, startTile, farmTile, unit.PlayerId, buildingRegistry);

            unit.TargetResourceNodeId = bestId;
            unit.GatherTimer = Fixed32.Zero;

            if (path.Count > 0)
            {
                unit.SetPath(path);
                unit.FinalDestination = mapData.TileToWorldFixed(farmTile.x, farmTile.y);
                unit.State = UnitState.MovingToGather;
            }
            else
            {
                unit.State = UnitState.Idle;
                unit.TargetResourceNodeId = -1;
                return false;
            }
            return true;
        }

        private bool IsFarmNodeOccupied(int farmNodeId, UnitData excludeUnit)
        {
            if (!occupiedFarmNodeIds.Contains(farmNodeId))
                return false;
            // The excludeUnit itself may be the only occupant — check if anyone else occupies it
            // This is a conservative check: if the farm is in the set, it's occupied by someone
            // The only false positive is if excludeUnit is the sole occupant
            if (excludeUnit.TargetResourceNodeId == farmNodeId)
            {
                // Count occupants: if only self, not occupied
                int otherCount = 0;
                if (occupiedTilesByNode.TryGetValue(farmNodeId, out var set))
                    otherCount += set.Count;
                // Self contributes 1 if gathering/moving-to-gather
                bool selfInGatherSet = (excludeUnit.State == UnitState.MovingToGather || excludeUnit.State == UnitState.Gathering);
                if (selfInGatherSet && otherCount > 0)
                    otherCount--;
                return otherCount > 0;
            }
            return true;
        }

        private bool IsFarmInfluencedByMill(ResourceNodeData farm, int playerId,
            BuildingRegistry buildingRegistry, int influenceRadius)
        {
            var buildings = buildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (b.Type != BuildingType.Mill) continue;
                if (b.PlayerId != playerId) continue;
                if (b.IsDestroyed || b.IsUnderConstruction) continue;

                int minX = b.OriginTileX - influenceRadius;
                int maxX = b.OriginTileX + b.TileFootprintWidth + influenceRadius;
                int minZ = b.OriginTileZ - influenceRadius;
                int maxZ = b.OriginTileZ + b.TileFootprintHeight + influenceRadius;

                if (farm.TileX >= minX && farm.TileX < maxX &&
                    farm.TileZ >= minZ && farm.TileZ < maxZ)
                    return true;
            }
            return false;
        }

        private bool TryAutoSlaughterNearbySheep(UnitData unit, FixedVector3 searchPos, UnitRegistry unitRegistry, MapData mapData, BuildingRegistry buildingRegistry, Fixed32 overrideRange = default)
        {
            Fixed32 searchRange = overrideRange.Raw > 0 ? overrideRange : unit.DetectionRange;
            Fixed32 searchRangeSq = searchRange * searchRange;
            UnitData bestSheep = null;
            Fixed32 bestDistSq = searchRangeSq;

            var allUnits = unitRegistry.GetAllUnits();
            for (int i = 0; i < allUnits.Count; i++)
            {
                var candidate = allUnits[i];
                if (!candidate.IsSheep) continue;
                if (candidate.State == UnitState.Dead) continue;
                if (candidate.PlayerId != unit.PlayerId) continue;

                FixedVector3 diff = candidate.SimPosition - searchPos;
                if (Fixed32.Abs(diff.x) > searchRange || Fixed32.Abs(diff.z) > searchRange) continue;

                Fixed32 distSq = diff.x * diff.x + diff.z * diff.z;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestSheep = candidate;
                }
            }

            if (bestSheep == null) return false;

            // Initiate slaughter — mirrors ProcessSlaughterSheepCommand logic
            unit.CombatTargetId = bestSheep.Id;
            unit.CombatTargetBuildingId = -1;
            unit.TargetResourceNodeId = -1;
            unit.GatherTimer = Fixed32.Zero;
            unit.PlayerCommanded = false;

            Vector2Int startTile = mapData.WorldToTile(unit.SimPosition);
            Vector2Int goalTile = mapData.WorldToTile(bestSheep.SimPosition);
            var path = GridPathfinder.FindPath(mapData, startTile, goalTile, unit.PlayerId, buildingRegistry);
            if (path.Count > 0)
            {
                unit.SetPath(path);
                unit.FinalDestination = bestSheep.SimPosition;
                unit.State = UnitState.MovingToSlaughter;
                return true;
            }
            return false;
        }

        private Vector2Int FindNearestWalkableAdjacentTileForResource(Vector2Int nodeOrigin, int footprintW, int footprintH, FixedVector3 unitPos, MapData mapData, HashSet<Vector2Int> occupiedTiles = null)
        {
            Vector2Int unitTile = mapData.WorldToTile(unitPos);
            Vector2Int best = nodeOrigin; // inside footprint = always unwalkable = valid failure sentinel
            int bestDistSq = int.MaxValue;

            // Scan the ring of tiles surrounding the full NxN footprint
            for (int x = nodeOrigin.x - 1; x <= nodeOrigin.x + footprintW; x++)
            {
                for (int z = nodeOrigin.y - 1; z <= nodeOrigin.y + footprintH; z++)
                {
                    // Skip tiles inside the footprint
                    if (x >= nodeOrigin.x && x < nodeOrigin.x + footprintW &&
                        z >= nodeOrigin.y && z < nodeOrigin.y + footprintH)
                        continue;

                    if (!mapData.IsWalkable(x, z)) continue;
                    if (occupiedTiles != null && occupiedTiles.Contains(new Vector2Int(x, z))) continue;

                    int dx = x - unitTile.x;
                    int dz = z - unitTile.y;
                    int distSq = dx * dx + dz * dz;
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        best = new Vector2Int(x, z);
                    }
                }
            }

            // Fallback: if all walkable tiles were occupied, retry ignoring occupied filter
            // Sharing a tile is better than idling
            if (bestDistSq == int.MaxValue && occupiedTiles != null && occupiedTiles.Count > 0)
            {
                for (int x = nodeOrigin.x - 1; x <= nodeOrigin.x + footprintW; x++)
                {
                    for (int z = nodeOrigin.y - 1; z <= nodeOrigin.y + footprintH; z++)
                    {
                        if (x >= nodeOrigin.x && x < nodeOrigin.x + footprintW &&
                            z >= nodeOrigin.y && z < nodeOrigin.y + footprintH)
                            continue;
                        if (!mapData.IsWalkable(x, z)) continue;
                        int dx = x - unitTile.x;
                        int dz = z - unitTile.y;
                        int distSq = dx * dx + dz * dz;
                        if (distSq < bestDistSq)
                        {
                            bestDistSq = distSq;
                            best = new Vector2Int(x, z);
                        }
                    }
                }
            }

            return best;
        }
    }
}
