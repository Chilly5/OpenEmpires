using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public class BuildingConstructionSystem
    {
        private List<int> completedList = new List<int>();
        private List<int> startedList = new List<int>();
        private List<(int unitId, int buildingId)> idledVillagerIds = new List<(int, int)>();
        private const int StrikeCooldownTicks = 20; // visual strike every 1s at 20 TPS

        private static readonly Fixed32 BuildingReach = Fixed32.FromFloat(1.0f);
        private static readonly Fixed32 TurnRate = Fixed32.FromFloat(0.3f);
        private static readonly Fixed32 FacingThreshold = Fixed32.FromFloat(0.9f); // cos(~26 deg)

        public List<int> Tick(UnitRegistry unitRegistry, BuildingRegistry buildingRegistry, MapData mapData, int currentTick, Fixed32 tickDuration, out List<(int unitId, int buildingId)> idledVillagers, out List<int> startedBuildingIds)
        {
            completedList.Clear();
            startedList.Clear();
            idledVillagerIds.Clear();

            foreach (var unit in unitRegistry.GetAllUnits())
            {
                if (unit.State != UnitState.Constructing)
                    continue;

                var building = buildingRegistry.GetBuilding(unit.ConstructionTargetBuildingId);
                if (building == null || building.IsDestroyed)
                {
                    unit.State = UnitState.Idle;
                    unit.ConstructionTargetBuildingId = -1;
                    continue;
                }

                if (!building.IsUnderConstruction)
                {
                    int finishedBuildingId = unit.ConstructionTargetBuildingId;
                    unit.State = UnitState.Idle;
                    unit.ConstructionTargetBuildingId = -1;
                    if (!unit.HasQueuedCommands)
                        idledVillagerIds.Add((unit.Id, finishedBuildingId));
                    continue;
                }

                // Range check: distance to nearest point on building footprint edge
                // (prevents villagers from chasing into the building interior for large footprints)
                FixedVector3 toBuilding = building.SimPosition - unit.SimPosition;
                toBuilding.y = Fixed32.Zero;

                Fixed32 nearX = Fixed32.Max(Fixed32.FromInt(building.OriginTileX),
                    Fixed32.Min(Fixed32.FromInt(building.OriginTileX + building.TileFootprintWidth), unit.SimPosition.x));
                Fixed32 nearZ = Fixed32.Max(Fixed32.FromInt(building.OriginTileZ),
                    Fixed32.Min(Fixed32.FromInt(building.OriginTileZ + building.TileFootprintHeight), unit.SimPosition.z));
                FixedVector3 toEdge = new FixedVector3(nearX - unit.SimPosition.x, Fixed32.Zero, nearZ - unit.SimPosition.z);

                // Overflow guard: skip multiply if axis distance exceeds reach
                if (Fixed32.Abs(toEdge.x) > BuildingReach || Fixed32.Abs(toEdge.z) > BuildingReach)
                {
                    // Too far — chase toward nearest footprint edge
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

                // Mark tiles non-walkable the first time a villager actually works
                if (!building.ConstructionStarted)
                {
                    building.ConstructionStarted = true;
                    startedList.Add(building.Id);
                }

                // In range and facing — do construction work
                building.ConstructionTicksRemaining--;
                int ticksElapsed = building.ConstructionTicksTotal - building.ConstructionTicksRemaining;
                int targetHealth = (int)((long)building.MaxHealth * ticksElapsed / building.ConstructionTicksTotal);
                int prevTarget = (int)((long)building.MaxHealth * (ticksElapsed - 1) / building.ConstructionTicksTotal);
                int healthDelta = targetHealth - prevTarget;
                if (healthDelta > 0)
                    building.CurrentHealth += healthDelta;

                // Visual strike feedback (periodic, not every tick)
                if (unit.AttackCooldownRemaining > 0)
                {
                    unit.AttackCooldownRemaining--;
                }
                else
                {
                    unit.AttackCooldownRemaining = StrikeCooldownTicks;
                    unit.LastAttackTick = currentTick;
                    unit.LastAttackTargetPos = building.SimPosition;
                    building.LastDamageTick = currentTick;
                    building.LastDamageFromPos = unit.SimPosition;
                }

                if (building.ConstructionTicksRemaining <= 0)
                {
                    building.IsUnderConstruction = false;
                    building.ConstructionTicksRemaining = 0;
                    building.CurrentHealth = building.MaxHealth;
                    int finishedBuildingId = unit.ConstructionTargetBuildingId;
                    unit.State = UnitState.Idle;
                    unit.ConstructionTargetBuildingId = -1;
                    completedList.Add(building.Id);
                    if (!unit.HasQueuedCommands)
                        idledVillagerIds.Add((unit.Id, finishedBuildingId));
                }
                else if (building.CurrentHealth > building.MaxHealth)
                {
                    building.CurrentHealth = building.MaxHealth;
                }
            }

            idledVillagers = idledVillagerIds;
            startedBuildingIds = startedList;
            return completedList;
        }
    }
}
