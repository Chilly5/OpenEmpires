using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public class BuildingRepairSystem
    {
        private List<int> completedList = new List<int>();
        private List<(int unitId, int buildingId)> idledVillagerIds = new List<(int, int)>();
        private const int StrikeCooldownTicks = 20; // visual strike every 1s at 20 TPS

        private static readonly Fixed32 BuildingReach = Fixed32.FromFloat(1.0f);
        private static readonly Fixed32 TurnRate = Fixed32.FromFloat(0.3f);
        private static readonly Fixed32 FacingThreshold = Fixed32.FromFloat(0.9f); // cos(~26 deg)

        public void ProcessRepairCommand(RepairBuildingCommand cmd, GameSimulation sim)
        {
            Debug.Log($"[Repair] Processing repair command for building {cmd.TargetBuildingId} with {cmd.UnitIds.Length} units");
            var building = sim.BuildingRegistry.GetBuilding(cmd.TargetBuildingId);
            if (building == null || building.IsDestroyed) return;
            if (building.CurrentHealth >= building.MaxHealth) return; // Already at full health
            if (!sim.AreAllies(building.PlayerId, cmd.PlayerId)) return;

            // Mark building as needing repair first
            if (!building.IsBeingRepaired)
            {
                building.IsBeingRepaired = true;
                int healthToRepair = building.MaxHealth - building.CurrentHealth;
                building.RepairTicksRemaining = CalculateRepairTicks(healthToRepair, sim.Config);
                building.RepairTicksTotal = building.RepairTicksRemaining;
                building.RepairStartHealth = building.CurrentHealth;
                Debug.Log($"[Repair] Started repair on building {building.Id}, health {building.CurrentHealth}/{building.MaxHealth}, repair ticks: {building.RepairTicksRemaining}");
            }

            var occupiedTiles = new HashSet<Vector2Int>();
            for (int i = 0; i < cmd.UnitIds.Length; i++)
            {
                var unit = sim.UnitRegistry.GetUnit(cmd.UnitIds[i]);
                if (unit == null || unit.State == UnitState.Dead) continue;
                if (unit.PlayerId != cmd.PlayerId) continue;
                if (!unit.IsVillager) continue; // Only villagers can repair

                if (cmd.IsQueued)
                {
                    // For now, don't support queued repair commands - could be added later
                    continue;
                }
                else
                {
                    // Set unit to repair the building (similar to construction)
                    unit.ClearCommandQueue();
                    unit.ClearSavedPath();
                    unit.ClearFormation();
                    unit.CombatTargetId = -1;
                    unit.CombatTargetBuildingId = -1;
                    unit.TargetResourceNodeId = -1;
                    unit.ConstructionTargetBuildingId = cmd.TargetBuildingId; // Reuse construction field for repair
                    unit.GatherTimer = Fixed32.Zero;
                    unit.PlayerCommanded = true;
                    unit.DropOffBuildingId = -1;
                    unit.TargetGarrisonBuildingId = -1;

                    unit.ClearPatrol();

                    // Pathfind to building like construction does
                    var triedTiles = new HashSet<Vector2Int>(occupiedTiles);
                    Vector2Int startTile = sim.MapData.WorldToTile(unit.SimPosition);
                    bool assigned = false;
                    int repairAttempts = 0;

                    while (true)
                    {
                        if (++repairAttempts > 4) break; // Cap retry attempts
                        Vector2Int adjTile = FindNearestWalkableAdjacentTile(building, unit.SimPosition, triedTiles, sim.MapData);
                        if (triedTiles.Contains(adjTile)) break; // All tiles exhausted

                        var path = GridPathfinder.FindPath(sim.MapData, startTile, adjTile, unit.PlayerId, sim.BuildingRegistry);
                        if (path.Count > 0)
                        {
                            occupiedTiles.Add(adjTile);
                            unit.SetPath(path);
                            unit.FinalDestination = sim.MapData.TileToWorldFixed(adjTile.x, adjTile.y);
                            unit.State = UnitState.MovingToBuild; // Use MovingToBuild state for repair pathfinding
                            assigned = true;
                            Debug.Log($"[Repair] Unit {unit.Id} pathfinding to repair building {building.Id}");
                            break;
                        }
                        triedTiles.Add(adjTile);
                    }

                    if (!assigned)
                    {
                        unit.State = UnitState.Constructing; // Fallback to direct repair if no path found
                        Debug.Log($"[Repair] Unit {unit.Id} assigned directly to repair building {building.Id} (no path found)");
                    }
                }
            }
        }

        public List<int> Tick(UnitRegistry unitRegistry, BuildingRegistry buildingRegistry, MapData mapData, ResourceManager resourceManager, SimulationConfig config, int currentTick, Fixed32 tickDuration, out List<(int unitId, int buildingId)> idledVillagers)
        {
            completedList.Clear();
            idledVillagerIds.Clear();

            foreach (var unit in unitRegistry.GetAllUnits())
            {
                if (unit.State != UnitState.Constructing)
                    continue;
                    
                var building = buildingRegistry.GetBuilding(unit.ConstructionTargetBuildingId);
                
                // Check if this unit might be doing repair
                if (building != null && !building.IsUnderConstruction && building.IsBeingRepaired)
                {
                    Debug.Log($"[Repair] Found repair unit {unit.Id} working on building {building.Id}");
                }
                if (building == null || building.IsDestroyed)
                {
                    unit.State = UnitState.Idle;
                    unit.ConstructionTargetBuildingId = -1;
                    continue;
                }

                // Skip if this is construction (not repair)
                if (building.IsUnderConstruction)
                    continue;

                // This must be repair work
                if (!building.IsBeingRepaired || building.CurrentHealth >= building.MaxHealth)
                {
                    // Repair finished or building already healthy
                    int finishedBuildingId = unit.ConstructionTargetBuildingId;
                    unit.State = UnitState.Idle;
                    unit.ConstructionTargetBuildingId = -1;
                    if (!unit.HasQueuedCommands)
                        idledVillagerIds.Add((unit.Id, finishedBuildingId));
                    continue;
                }

                // Range check: distance to nearest point on building footprint edge
                FixedVector3 toBuilding = building.SimPosition - unit.SimPosition;
                toBuilding.y = Fixed32.Zero;

                Fixed32 nearX = Fixed32.Max(Fixed32.FromInt(building.OriginTileX),
                    Fixed32.Min(Fixed32.FromInt(building.OriginTileX + building.TileFootprintWidth), unit.SimPosition.x));
                Fixed32 nearZ = Fixed32.Max(Fixed32.FromInt(building.OriginTileZ),
                    Fixed32.Min(Fixed32.FromInt(building.OriginTileZ + building.TileFootprintHeight), unit.SimPosition.z));
                FixedVector3 toEdge = new FixedVector3(nearX - unit.SimPosition.x, Fixed32.Zero, nearZ - unit.SimPosition.z);

                // Too far — chase toward nearest footprint edge
                if (Fixed32.Abs(toEdge.x) > BuildingReach || Fixed32.Abs(toEdge.z) > BuildingReach)
                {
                    Fixed32 absDx = Fixed32.Abs(toEdge.x);
                    Fixed32 absDz = Fixed32.Abs(toEdge.z);
                    Fixed32 approxDist = absDx > absDz ? absDx : absDz;
                    if (approxDist.Raw > 0)
                    {
                        FixedVector3 dir = toEdge / approxDist;
                        Fixed32 step = unit.MoveSpeed * tickDuration;
                        FixedVector3 newPos = unit.SimPosition + dir * step;
                        Vector2Int newTile = mapData.WorldToTile(newPos);
                        if (mapData.IsWalkable(newTile.x, newTile.y))
                            unit.SimPosition = newPos;
                        unit.SimFacing = dir;
                    }
                    continue;
                }
                Fixed32 edgeDistSq = toEdge.x * toEdge.x + toEdge.z * toEdge.z;
                Fixed32 reachSq = BuildingReach * BuildingReach;

                if (edgeDistSq > reachSq)
                {
                    // Too far — chase toward nearest footprint edge
                    Fixed32 edgeDist = Fixed32.Sqrt(edgeDistSq);
                    if (edgeDist.Raw > 0)
                    {
                        FixedVector3 dir = toEdge / edgeDist;
                        Fixed32 step = unit.MoveSpeed * tickDuration;
                        FixedVector3 newPos = unit.SimPosition + dir * step;
                        Vector2Int newTile = mapData.WorldToTile(newPos);
                        if (mapData.IsWalkable(newTile.x, newTile.y))
                            unit.SimPosition = newPos;
                        unit.SimFacing = dir;
                    }
                    continue;
                }

                // Turn toward building center
                Fixed32 mag = toBuilding.Magnitude();
                if (mag.Raw > 0)
                {
                    FixedVector3 targetDir = toBuilding / mag;

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

                    // Check if facing the building
                    Fixed32 dot = unit.SimFacing.x * targetDir.x + unit.SimFacing.z * targetDir.z;
                    if (dot < FacingThreshold)
                        continue; // still turning, don't work yet
                }

                // In range and facing — do repair work
                Debug.Log($"[Repair] Villager {unit.Id} is repairing building {building.Id}, remaining ticks: {building.RepairTicksRemaining}");
                building.RepairTicksRemaining--;
                
                // Gradual health restoration based on repair progress
                int ticksElapsed = building.RepairTicksTotal - building.RepairTicksRemaining;
                int targetHealth = building.MaxHealth; // We want to reach max health
                int currentRepairProgress = (int)((long)(building.MaxHealth - building.RepairStartHealth) * ticksElapsed / building.RepairTicksTotal);
                int newHealth = building.RepairStartHealth + currentRepairProgress;
                
                if (newHealth > building.CurrentHealth)
                {
                    // Consume wood for each health point repaired
                    var resources = resourceManager.GetPlayerResources(unit.PlayerId);
                    int healthToAdd = newHealth - building.CurrentHealth;
                    int repairCost = Mathf.RoundToInt(healthToAdd * config.RepairCostPerHealthPoint);
                    
                    if (resources.Wood >= repairCost)
                    {
                        resources.Wood -= repairCost;
                        building.CurrentHealth = newHealth;
                    }
                    else
                    {
                        // Not enough resources - stop repair
                        building.IsBeingRepaired = false;
                        unit.State = UnitState.Idle;
                        unit.ConstructionTargetBuildingId = -1;
                        continue;
                    }
                }

                // Visual strike feedback (periodic, not every tick)
                if (unit.AttackCooldownRemaining > 0)
                {
                    unit.AttackCooldownRemaining--;
                }
                else
                {
                    Debug.Log($"[Repair] Villager {unit.Id} strike animation on building {building.Id} at tick {currentTick}");
                    unit.AttackCooldownRemaining = StrikeCooldownTicks;
                    unit.LastAttackTick = currentTick;
                    unit.LastAttackTargetPos = building.SimPosition;
                    building.LastDamageTick = currentTick;
                    building.LastDamageFromPos = unit.SimPosition;
                }

                if (building.RepairTicksRemaining <= 0 || building.CurrentHealth >= building.MaxHealth)
                {
                    building.IsBeingRepaired = false;
                    building.RepairTicksRemaining = 0;
                    building.CurrentHealth = building.MaxHealth;
                    int finishedBuildingId = unit.ConstructionTargetBuildingId;
                    unit.State = UnitState.Idle;
                    unit.ConstructionTargetBuildingId = -1;
                    completedList.Add(building.Id);
                    if (!unit.HasQueuedCommands)
                        idledVillagerIds.Add((unit.Id, finishedBuildingId));
                }
            }

            idledVillagers = idledVillagerIds;
            return completedList;
        }

        private int CalculateRepairTicks(int healthToRepair, SimulationConfig config)
        {
            // Calculate repair time based on health to repair
            return Mathf.Max(30, Mathf.RoundToInt(healthToRepair * config.RepairTicksPerHealthPoint));
        }

        private int CalculateRepairCost(int healthToRepair, SimulationConfig config)
        {
            // Base cost + per-health-point cost
            return Mathf.Max(1, Mathf.RoundToInt(config.BaseRepairCost + healthToRepair * config.RepairCostPerHealthPoint));
        }

        private Vector2Int FindNearestWalkableAdjacentTile(BuildingData building, FixedVector3 unitPos, HashSet<Vector2Int> occupiedTiles, MapData mapData)
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

                    var tile = new Vector2Int(x, z);
                    if (occupiedTiles.Contains(tile)) continue;

                    int dx = x - unitTile.x;
                    int dz = z - unitTile.y;
                    int distSq = dx * dx + dz * dz;
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        best = tile;
                    }
                }
            }

            return best;
        }
    }
}