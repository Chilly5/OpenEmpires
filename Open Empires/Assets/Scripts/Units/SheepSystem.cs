using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public class SheepSystem
    {
        public void Tick(UnitRegistry unitRegistry, BuildingRegistry buildingRegistry, SpatialGrid spatialGrid,
            MapData mapData, Fixed32 tickDuration, int currentTick, int[] playerTeamIds,
            SimulationConfig config, Action<int, int> onSheepConverted)
        {
            Fixed32 conversionRange = Fixed32.FromFloat(config.SheepConversionRange);
            Fixed32 conversionRangeSq = conversionRange * conversionRange;

            var allUnits = unitRegistry.GetAllUnits();
            int count = allUnits.Count;

            for (int i = 0; i < count; i++)
            {
                var sheep = allUnits[i];
                if (!sheep.IsSheep) continue;
                if (sheep.State == UnitState.Dead) continue;

                // Reset state when sheep goes idle
                if (sheep.State == UnitState.Idle)
                {
                    if (sheep.MoveSpeed > Fixed32.FromFloat(config.SheepMoveSpeed))
                        sheep.MoveSpeed = Fixed32.FromFloat(config.SheepMoveSpeed);
                    sheep.SheepTargetBuildingId = -1;
                }

                // A. Conversion — neutral sheep
                if (sheep.PlayerId == UnitData.NeutralPlayerId)
                {
                    var nearby = spatialGrid.GetNearby(sheep.SimPosition, conversionRange);
                    UnitData closest = null;
                    Fixed32 closestDistSq = conversionRangeSq + Fixed32.One;

                    for (int j = 0; j < nearby.Count; j++)
                    {
                        var other = nearby[j];
                        if (other.IsSheep) continue;
                        if (other.State == UnitState.Dead) continue;
                        if (other.PlayerId < 0) continue;

                        Fixed32 dx = other.SimPosition.x - sheep.SimPosition.x;
                        Fixed32 dz = other.SimPosition.z - sheep.SimPosition.z;
                        Fixed32 distSq = dx * dx + dz * dz;

                        if (distSq < closestDistSq)
                        {
                            closestDistSq = distSq;
                            closest = other;
                        }
                    }

                    if (closest != null && closestDistSq <= conversionRangeSq)
                    {
                        sheep.PlayerId = closest.PlayerId;
                        if (closest.UnitType == 4) // Scout
                        {
                            sheep.FollowTargetId = closest.Id;
                            sheep.State = UnitState.Following;
                        }
                        else
                        {
                            sheep.FollowTargetId = -1;
                            sheep.State = UnitState.Idle;
                        }
                        sheep.ClearPath();
                        onSheepConverted?.Invoke(sheep.Id, sheep.PlayerId);
                        continue;
                    }

                    // Idle wander for neutral sheep
                    if (sheep.State == UnitState.Idle)
                    {
                        sheep.WanderCooldown--;
                        if (sheep.WanderCooldown <= 0)
                        {
                            // Pick a random point within 5 tiles of spawn position
                            int hash = (int)((uint)(sheep.Id * 31 + currentTick) * 2654435761u);
                            float angle = (hash & 0xFFFF) / 65536f * 6.2832f;
                            float dist = ((hash >> 16) & 0xFFFF) / 65536f * 5f;
                            Fixed32 dx = Fixed32.FromFloat((float)System.Math.Cos(angle) * dist);
                            Fixed32 dz = Fixed32.FromFloat((float)System.Math.Sin(angle) * dist);
                            FixedVector3 wanderTarget = new FixedVector3(
                                sheep.SpawnPosition.x + dx,
                                sheep.SpawnPosition.y,
                                sheep.SpawnPosition.z + dz);

                            Vector2Int startTile = mapData.WorldToTile(sheep.SimPosition);
                            Vector2Int goalTile = mapData.WorldToTile(wanderTarget);
                            if (startTile != goalTile && mapData.IsWalkable(goalTile.x, goalTile.y))
                            {
                                var path = GridPathfinder.FindPath(mapData, startTile, goalTile, -1, null);
                                if (path.Count > 0)
                                {
                                    sheep.SetPath(path);
                                    sheep.FinalDestination = wanderTarget;
                                    sheep.State = UnitState.Moving;
                                }
                            }
                            // Next wander in 3-6 seconds (90-180 ticks at 30 tps)
                            sheep.WanderCooldown = 90 + ((hash >> 8) & 0xFF) % 90;
                        }
                    }

                    // Walk along wander path
                    if (sheep.State == UnitState.Moving && sheep.HasPath)
                    {
                        Fixed32 moveSpeed = sheep.MoveSpeed;
                        Fixed32 remainingStep = moveSpeed * tickDuration;
                        FixedVector3 lastDir = FixedVector3.Zero;

                        while (remainingStep > Fixed32.Zero && sheep.HasPath)
                        {
                            bool isFinal = sheep.CurrentPathIndex == sheep.Path.Count - 1;
                            FixedVector3 waypoint = isFinal
                                ? sheep.FinalDestination
                                : mapData.TileToWorldFixed(
                                    sheep.Path[sheep.CurrentPathIndex].x,
                                    sheep.Path[sheep.CurrentPathIndex].y);

                            FixedVector3 toWp = waypoint - sheep.SimPosition;
                            Fixed32 wpDist = toWp.Magnitude();

                            if (wpDist <= remainingStep || wpDist < Fixed32.FromFloat(0.05f))
                            {
                                if (wpDist.Raw > 0) lastDir = toWp / wpDist;
                                sheep.SimPosition = waypoint;
                                remainingStep = remainingStep - wpDist;
                                sheep.CurrentPathIndex++;
                            }
                            else
                            {
                                FixedVector3 dir = toWp / wpDist;
                                sheep.SimPosition = sheep.SimPosition + dir * remainingStep;
                                lastDir = dir;
                                remainingStep = Fixed32.Zero;
                            }
                        }

                        if (lastDir.x.Raw != 0 || lastDir.z.Raw != 0)
                            sheep.SimFacing = lastDir;

                        // Arrived at destination
                        if (!sheep.HasPath)
                            sheep.State = UnitState.Idle;
                    }

                    continue;
                }

                // B. Stealing — owned sheep
                {
                    bool isProtected = false;

                    // Protected if near any allied building
                    var buildings = buildingRegistry.GetAllBuildings();
                    for (int b = 0; b < buildings.Count; b++)
                    {
                        var bld = buildings[b];
                        if (bld.IsDestroyed) continue;
                        if (!TeamHelper.AreAllies(playerTeamIds, bld.PlayerId, sheep.PlayerId)) continue;

                        Fixed32 dx = bld.SimPosition.x - sheep.SimPosition.x;
                        Fixed32 dz = bld.SimPosition.z - sheep.SimPosition.z;
                        // Early-out: skip if axis distance exceeds range (avoids Fixed32 overflow on squaring)
                        if (Fixed32.Abs(dx) > conversionRange || Fixed32.Abs(dz) > conversionRange) continue;
                        Fixed32 distSq = dx * dx + dz * dz;
                        if (distSq <= conversionRangeSq)
                        {
                            isProtected = true;
                            break;
                        }
                    }

                    if (!isProtected)
                    {
                        var nearby = spatialGrid.GetNearby(sheep.SimPosition, conversionRange);
                        UnitData closestEnemy = null;
                        Fixed32 closestDistSq = conversionRangeSq + Fixed32.One;

                        for (int j = 0; j < nearby.Count; j++)
                        {
                            var other = nearby[j];
                            if (other.IsSheep) continue;
                            if (other.State == UnitState.Dead) continue;
                            if (other.PlayerId < 0) continue;

                            Fixed32 dx = other.SimPosition.x - sheep.SimPosition.x;
                            Fixed32 dz = other.SimPosition.z - sheep.SimPosition.z;
                            Fixed32 distSq = dx * dx + dz * dz;

                            if (TeamHelper.AreAllies(playerTeamIds, other.PlayerId, sheep.PlayerId))
                            {
                                if (distSq <= conversionRangeSq)
                                    isProtected = true;
                            }
                            else
                            {
                                if (distSq < closestDistSq)
                                {
                                    closestDistSq = distSq;
                                    closestEnemy = other;
                                }
                            }
                        }

                        if (!isProtected && closestEnemy != null && closestDistSq <= conversionRangeSq)
                        {
                            sheep.PlayerId = closestEnemy.PlayerId;
                            if (closestEnemy.UnitType == 4) // Scout
                            {
                                sheep.FollowTargetId = closestEnemy.Id;
                                sheep.State = UnitState.Following;
                            }
                            else
                            {
                                sheep.FollowTargetId = -1;
                                sheep.State = UnitState.Idle;
                            }
                            onSheepConverted?.Invoke(sheep.Id, sheep.PlayerId);
                        }
                    }
                }

                // Walk along path for owned sheep (player move commands only, no wandering)
                if (sheep.State == UnitState.Moving && sheep.HasPath)
                {
                    // Run at scout speed when heading toward a building and close enough
                    Fixed32 moveSpeed = Fixed32.FromFloat(config.SheepMoveSpeed);
                    if (sheep.SheepTargetBuildingId >= 0)
                    {
                        var targetBuilding = buildingRegistry.GetBuilding(sheep.SheepTargetBuildingId);
                        if (targetBuilding != null && !targetBuilding.IsDestroyed)
                        {
                            Fixed32 dxB = targetBuilding.SimPosition.x - sheep.SimPosition.x;
                            Fixed32 dzB = targetBuilding.SimPosition.z - sheep.SimPosition.z;
                            Fixed32 distSq = dxB * dxB + dzB * dzB;
                            Fixed32 runRangeSq = Fixed32.FromFloat(config.SheepConversionRange * config.SheepConversionRange);
                            if (distSq <= runRangeSq)
                                moveSpeed = Fixed32.FromFloat(config.ScoutMoveSpeed);
                        }
                        else
                        {
                            sheep.SheepTargetBuildingId = -1;
                        }
                    }
                    Fixed32 remainingStep = moveSpeed * tickDuration;
                    FixedVector3 lastDir = FixedVector3.Zero;

                    while (remainingStep > Fixed32.Zero && sheep.HasPath)
                    {
                        bool isFinal = sheep.CurrentPathIndex == sheep.Path.Count - 1;
                        FixedVector3 waypoint = isFinal
                            ? sheep.FinalDestination
                            : mapData.TileToWorldFixed(
                                sheep.Path[sheep.CurrentPathIndex].x,
                                sheep.Path[sheep.CurrentPathIndex].y);

                        FixedVector3 toWp = waypoint - sheep.SimPosition;
                        Fixed32 wpDist = toWp.Magnitude();

                        if (wpDist <= remainingStep || wpDist < Fixed32.FromFloat(0.05f))
                        {
                            if (wpDist.Raw > 0) lastDir = toWp / wpDist;
                            sheep.SimPosition = waypoint;
                            remainingStep = remainingStep - wpDist;
                            sheep.CurrentPathIndex++;
                        }
                        else
                        {
                            FixedVector3 dir = toWp / wpDist;
                            sheep.SimPosition = sheep.SimPosition + dir * remainingStep;
                            lastDir = dir;
                            remainingStep = Fixed32.Zero;
                        }
                    }

                    if (lastDir.x.Raw != 0 || lastDir.z.Raw != 0)
                        sheep.SimFacing = lastDir;

                    if (!sheep.HasPath)
                        sheep.State = UnitState.Idle;
                }

                // C. Following — movement
                if (sheep.State == UnitState.Following)
                {
                    if (sheep.FollowTargetId < 0)
                    {
                        sheep.State = UnitState.Idle;
                        continue;
                    }

                    var followed = unitRegistry.GetUnit(sheep.FollowTargetId);
                    if (followed == null || followed.State == UnitState.Dead || followed.UnitType != 4)
                    {
                        sheep.State = UnitState.Idle;
                        sheep.FollowTargetId = -1;
                        continue;
                    }

                    // Oval blob behind the scout — each sheep targets a unique point
                    Fixed32 clumpDist = Fixed32.FromFloat(2.5f);
                    FixedVector3 clumpCenter = followed.SimPosition - followed.SimFacing * clumpDist;

                    // Perpendicular to scout facing
                    FixedVector3 perp = new FixedVector3(
                        -followed.SimFacing.z, Fixed32.Zero, followed.SimFacing.x);

                    // Deterministic offset within the oval based on sheep ID
                    // Use a simple hash to spread sheep around
                    int hash = (int)((uint)sheep.Id * 2654435761u); // Knuth multiplicative hash
                    float angle = (hash & 0xFFFF) / 65536f * 6.2832f; // 0 to 2*PI
                    float radiusFrac = ((hash >> 16) & 0xFFFF) / 65536f; // 0 to 1
                    // sqrt for uniform area distribution
                    radiusFrac = (float)System.Math.Sqrt(radiusFrac);

                    // Oval: wider perpendicular (1.8), shorter along facing (1.0)
                    float offLateral = (float)System.Math.Cos(angle) * radiusFrac * 1.8f;
                    float offBehind = (float)System.Math.Sin(angle) * radiusFrac * 1.0f;

                    FixedVector3 sheepTarget = clumpCenter
                        + perp * Fixed32.FromFloat(offLateral)
                        - followed.SimFacing * Fixed32.FromFloat(offBehind);

                    FixedVector3 toCenter = sheepTarget - sheep.SimPosition;
                    Fixed32 distToCenter = toCenter.Magnitude();
                    Fixed32 arrivalThreshold = Fixed32.FromFloat(0.5f);

                    // Speed: scout speed when close enough to scout, normal sheep speed when too far
                    Fixed32 dxS = followed.SimPosition.x - sheep.SimPosition.x;
                    Fixed32 dzS = followed.SimPosition.z - sheep.SimPosition.z;
                    Fixed32 distToScoutSq = dxS * dxS + dzS * dzS;
                    Fixed32 runRangeSq = Fixed32.FromFloat(config.SheepConversionRange * config.SheepConversionRange);
                    Fixed32 moveSpeed = distToScoutSq <= runRangeSq
                        ? Fixed32.FromFloat(config.ScoutMoveSpeed) : Fixed32.FromFloat(config.SheepMoveSpeed);

                    if (distToCenter > arrivalThreshold)
                    {
                        // Re-path periodically (every 10 ticks, staggered by sheep ID) or when no path
                        Vector2Int goalTile = mapData.WorldToTile(sheepTarget);
                        bool needsRepath = !sheep.HasPath
                            || (currentTick % 10 == (sheep.Id % 10));

                        if (needsRepath)
                        {
                            Vector2Int startTile = mapData.WorldToTile(sheep.SimPosition);
                            if (startTile != goalTile)
                            {
                                var path = GridPathfinder.FindPath(mapData, startTile, goalTile,
                                    sheep.PlayerId, buildingRegistry);
                                if (path.Count > 0)
                                {
                                    sheep.SetPath(path);
                                    sheep.FinalDestination = sheepTarget;
                                }
                            }
                        }

                        // Walk along path
                        if (sheep.HasPath)
                        {
                            Fixed32 remainingStep = moveSpeed * tickDuration;
                            FixedVector3 lastDir = FixedVector3.Zero;

                            while (remainingStep > Fixed32.Zero && sheep.HasPath)
                            {
                                bool isFinal = sheep.CurrentPathIndex == sheep.Path.Count - 1;
                                FixedVector3 waypoint = isFinal
                                    ? sheep.FinalDestination
                                    : mapData.TileToWorldFixed(
                                        sheep.Path[sheep.CurrentPathIndex].x,
                                        sheep.Path[sheep.CurrentPathIndex].y);

                                FixedVector3 toWp = waypoint - sheep.SimPosition;
                                Fixed32 wpDist = toWp.Magnitude();

                                if (wpDist <= remainingStep || wpDist < Fixed32.FromFloat(0.05f))
                                {
                                    if (wpDist.Raw > 0) lastDir = toWp / wpDist;
                                    sheep.SimPosition = waypoint;
                                    remainingStep = remainingStep - wpDist;
                                    sheep.CurrentPathIndex++;
                                }
                                else
                                {
                                    FixedVector3 dir = toWp / wpDist;
                                    sheep.SimPosition = sheep.SimPosition + dir * remainingStep;
                                    lastDir = dir;
                                    remainingStep = Fixed32.Zero;
                                }
                            }

                            if (lastDir.x.Raw != 0 || lastDir.z.Raw != 0)
                                sheep.SimFacing = lastDir;
                        }
                    }
                    else
                    {
                        // Inside the clump — stop moving
                        if (sheep.HasPath)
                            sheep.ClearPath();
                    }
                }
            }
        }
    }
}
