using UnityEngine;

namespace OpenEmpires
{
    public class UnitMovementSystem
    {
        private static readonly Fixed32 ArrivalThreshold = new Fixed32(3276); // 0.05 * 65536 ≈ 3276

        public void Tick(UnitRegistry unitRegistry, MapData mapData, Fixed32 tickDuration)
        {
            foreach (var unit in unitRegistry.GetAllUnits())
            {
                if (unit.State == UnitState.Dead) continue;
                if (unit.IsSheep) continue; // SheepSystem handles all sheep movement
                if (unit.ChargeStunRemaining > 0) continue;
                if (unit.State != UnitState.Moving && unit.State != UnitState.MovingToGather && unit.State != UnitState.MovingToBuild && unit.State != UnitState.MovingToDropoff && unit.State != UnitState.MovingToGarrison && unit.State != UnitState.MovingToSlaughter)
                    continue;

                if (!unit.HasPath)
                {
                    if (unit.State == UnitState.Moving)
                    {
                        if (unit.CombatTargetId >= 0 || unit.CombatTargetBuildingId >= 0)
                        {
                            unit.State = UnitState.InCombat;
                        }
                        else
                        {
                            unit.State = UnitState.Idle;
                            if (!unit.IsVillager)
                                unit.PlayerCommanded = false;
                        }
                    }
                    else if (unit.State == UnitState.MovingToGather)
                    {
                        unit.State = UnitState.Gathering;
                        unit.PlayerCommanded = false;
                    }
                    else if (unit.State == UnitState.MovingToBuild)
                    {
                        unit.State = UnitState.Constructing;
                        unit.PlayerCommanded = false;
                    }
                    else if (unit.State == UnitState.MovingToDropoff)
                    {
                        unit.State = UnitState.DroppingOff;
                        // Keep PlayerCommanded — ProcessDropOff needs it to distinguish manual vs auto
                    }
                    else if (unit.State == UnitState.MovingToGarrison)
                    {
                        // Will be handled by garrison logic in GameSimulation
                        unit.State = UnitState.Idle;
                        unit.PlayerCommanded = false;
                    }
                    else if (unit.State == UnitState.MovingToSlaughter)
                    {
                        unit.State = UnitState.Idle;
                        unit.PlayerCommanded = false;
                    }
                    continue;
                }

                Fixed32 speed = (unit.State == UnitState.Moving && unit.InFormation)
                    ? unit.FormationMoveSpeed : unit.MoveSpeed;
                FixedVector3 lastMoveDir = FixedVector3.Zero;

                Fixed32 remainingStep = speed * tickDuration;

                while (remainingStep > Fixed32.Zero && unit.HasPath)
                {
                    bool isFinalWaypoint = (unit.CurrentPathIndex == unit.Path.Count - 1);

                    FixedVector3 waypoint;
                    if (isFinalWaypoint)
                        waypoint = unit.FinalDestination;
                    else
                    {
                        var tile = unit.Path[unit.CurrentPathIndex];
                        waypoint = mapData.TileToWorldFixed(tile.x, tile.y);

                        // Formation marching: offset waypoint perpendicular to path direction
                        if (unit.InFormation && unit.State == UnitState.Moving)
                        {
                            FixedVector3 segDir = FixedVector3.Zero;
                            int nextIdx = unit.CurrentPathIndex + 1;
                            if (nextIdx < unit.Path.Count)
                            {
                                FixedVector3 nextWp;
                                if (nextIdx == unit.Path.Count - 1)
                                    nextWp = unit.FinalDestination;
                                else
                                {
                                    var nextTile = unit.Path[nextIdx];
                                    nextWp = mapData.TileToWorldFixed(nextTile.x, nextTile.y);
                                }
                                FixedVector3 diff = nextWp - waypoint;
                                Fixed32 mag = diff.Magnitude();
                                if (mag.Raw > 0)
                                    segDir = diff / mag;
                            }

                            if (segDir.x.Raw != 0 || segDir.z.Raw != 0)
                            {
                                // Right perpendicular in XZ plane
                                FixedVector3 perpDir = new FixedVector3(segDir.z, Fixed32.Zero, -segDir.x);
                                FixedVector3 offset = perpDir * unit.FormationOffset.x + segDir * unit.FormationOffset.z;
                                FixedVector3 offsetWp = waypoint + offset;

                                // Walkability check with progressive fallback
                                Vector2Int offsetTile = mapData.WorldToTile(offsetWp);
                                if (mapData.IsWalkable(offsetTile.x, offsetTile.y))
                                    waypoint = offsetWp;
                                else
                                {
                                    // Try half offset (corridor compression)
                                    FixedVector3 halfWp = waypoint + offset * Fixed32.Half;
                                    offsetTile = mapData.WorldToTile(halfWp);
                                    if (mapData.IsWalkable(offsetTile.x, offsetTile.y))
                                        waypoint = halfWp;
                                    // else: keep base waypoint (full corridor compression)
                                }
                            }
                        }
                    }

                    FixedVector3 toWaypoint = waypoint - unit.SimPosition;
                    Fixed32 dist = toWaypoint.Magnitude();

                    if (dist <= remainingStep || dist < ArrivalThreshold)
                    {
                        if (dist.Raw > 0)
                            lastMoveDir = toWaypoint / dist;
                        unit.SimPosition = waypoint;
                        remainingStep = remainingStep - dist;
                        unit.CurrentPathIndex++;
                    }
                    else
                    {
                        FixedVector3 dir = toWaypoint / dist;
                        unit.SimPosition = unit.SimPosition + dir * remainingStep;
                        lastMoveDir = dir;
                        remainingStep = Fixed32.Zero;
                    }
                }

                // Update SimFacing from movement direction
                if (lastMoveDir.x.Raw != 0 || lastMoveDir.z.Raw != 0)
                    unit.SimFacing = new FixedVector3(lastMoveDir.x, Fixed32.Zero, lastMoveDir.z);

                // Combat range check: stop moving when target is within attack range
                if (unit.State == UnitState.Moving && unit.CombatTargetId >= 0)
                {
                    var target = unitRegistry.GetUnit(unit.CombatTargetId);
                    if (target != null && target.State != UnitState.Dead)
                    {
                        Fixed32 dx = target.SimPosition.x - unit.SimPosition.x;
                        Fixed32 dz = target.SimPosition.z - unit.SimPosition.z;
                        Fixed32 distSq = dx * dx + dz * dz;
                        Fixed32 attackRangeSq = unit.AttackRange * unit.AttackRange;
                        if (distSq <= attackRangeSq)
                        {
                            unit.ClearPath();
                            unit.State = UnitState.InCombat;
                            continue;
                        }
                    }
                }

                // Path exhausted — transition state
                if (!unit.HasPath)
                {
                    if (unit.State == UnitState.Moving)
                    {
                        if (unit.CombatTargetId >= 0 || unit.CombatTargetBuildingId >= 0)
                        {
                            unit.State = UnitState.InCombat;
                        }
                        else
                        {
                            unit.State = UnitState.Idle;
                            if (!unit.IsVillager)
                                unit.PlayerCommanded = false;
                        }
                    }
                    else if (unit.State == UnitState.MovingToGather)
                    {
                        unit.State = UnitState.Gathering;
                        unit.PlayerCommanded = false;
                    }
                    else if (unit.State == UnitState.MovingToBuild)
                    {
                        unit.State = UnitState.Constructing;
                        unit.PlayerCommanded = false;
                    }
                    else if (unit.State == UnitState.MovingToDropoff)
                    {
                        unit.State = UnitState.DroppingOff;
                        // Keep PlayerCommanded — ProcessDropOff needs it to distinguish manual vs auto
                    }
                    else if (unit.State == UnitState.MovingToGarrison)
                    {
                        // Will be handled by garrison logic in GameSimulation
                        unit.State = UnitState.Idle;
                        unit.PlayerCommanded = false;
                    }
                    else if (unit.State == UnitState.MovingToSlaughter)
                    {
                        unit.State = UnitState.Idle;
                        unit.PlayerCommanded = false;
                    }
                }
            }
        }
    }
}
