using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public class UnitSeparationSystem
    {
        private static readonly Fixed32 DistEpsilon = Fixed32.FromFloat(0.001f);
        private static readonly Fixed32 SoftSeparationScale = Fixed32.FromFloat(0.25f);
        private static readonly Fixed32 CombatSeparationScale = Fixed32.FromFloat(0.75f);

        // Maximum possible combined radius for spatial grid queries
        private static readonly Fixed32 MaxCombinedRadius = Fixed32.FromFloat(2f);

        // Pre-allocated buffers for two-phase separation (avoids per-tick allocations)
        private int[] accumPushX;
        private int[] accumPushZ;
        private Dictionary<int, int> idToIndex = new Dictionary<int, int>();

        private void EnsureBuffers(int count)
        {
            if (accumPushX == null || accumPushX.Length < count)
            {
                accumPushX = new int[count];
                accumPushZ = new int[count];
            }
        }

        public void Tick(UnitRegistry unitRegistry, MapData mapData, Fixed32 separationStrength, SpatialGrid spatialGrid = null, int[] playerTeamIds = null)
        {
            var units = unitRegistry.GetAllUnits();
            int count = units.Count;

            EnsureBuffers(count);

            // Build ID → index map and zero the accumulation buffers
            idToIndex.Clear();
            for (int i = 0; i < count; i++)
            {
                accumPushX[i] = 0;
                accumPushZ[i] = 0;
                idToIndex[units[i].Id] = i;
            }

            // Phase 1: Compute all separation forces (read-only — no position modifications)
            for (int i = 0; i < count; i++)
            {
                var a = units[i];
                if (a.State == UnitState.Dead) continue;

                // Use spatial grid for neighbor lookup if available
                var candidates = spatialGrid != null
                    ? spatialGrid.GetNearby(a.SimPosition, MaxCombinedRadius)
                    : units;
                int candidateCount = candidates.Count;

                for (int j = 0; j < candidateCount; j++)
                {
                    var b = candidates[j];
                    // Only process each pair once: a.Id < b.Id
                    if (b.Id <= a.Id) continue;
                    if (b.State == UnitState.Dead) continue;

                    Fixed32 combinedRadius = a.Radius + b.Radius;

                    Fixed32 dx = b.SimPosition.x - a.SimPosition.x;
                    if (Fixed32.Abs(dx) > combinedRadius) continue;
                    Fixed32 dz = b.SimPosition.z - a.SimPosition.z;
                    if (Fixed32.Abs(dz) > combinedRadius) continue;

                    Fixed32 distSq = dx * dx + dz * dz;
                    if (distSq >= combinedRadius * combinedRadius)
                        continue;

                    Fixed32 dist = Fixed32.Sqrt(distSq);
                    Fixed32 overlap = combinedRadius - dist;

                    Fixed32 dirX, dirZ;
                    if (dist > DistEpsilon)
                    {
                        Fixed32 invDist = Fixed32.One / dist;
                        dirX = dx * invDist;
                        dirZ = dz * invDist;
                    }
                    else
                    {
                        dirX = (a.Id < b.Id) ? -Fixed32.One : Fixed32.One;
                        dirZ = Fixed32.Zero;
                    }

                    bool bothVillagers = a.IsVillager && b.IsVillager;

                    Fixed32 pushMag;
                    Fixed32 totalMass;
                    Fixed32 aRatio, bRatio;

                    // Gathering: non-villager pairs walk through freely,
                    // villager pairs get soft separation to avoid pile-ups
                    if (a.State == UnitState.Gathering || b.State == UnitState.Gathering)
                    {
                        if (!bothVillagers)
                            continue; // non-villager + gatherer: walk-through
                        pushMag = overlap * separationStrength * SoftSeparationScale;
                        totalMass = a.Mass + b.Mass;
                        aRatio = b.Mass / totalMass;
                        bRatio = a.Mass / totalMass;
                    }
                    // Moving units: allies get soft push (25%) so they slide past each other,
                    // enemies get full push so armies block each other
                    else if (IsMovingState(a.State) || IsMovingState(b.State))
                    {
                        Fixed32 scale = TeamHelper.AreAllies(playerTeamIds, a.PlayerId, b.PlayerId) ? SoftSeparationScale : Fixed32.One;
                        pushMag = overlap * separationStrength * scale;
                        totalMass = a.Mass + b.Mass;
                        aRatio = b.Mass / totalMass;
                        bRatio = a.Mass / totalMass;
                    }
                    else
                    {
                        // Default: mass-based push with ally scaling
                        totalMass = a.Mass + b.Mass;
                        aRatio = b.Mass / totalMass;
                        bRatio = a.Mass / totalMass;
                        bool allies = TeamHelper.AreAllies(playerTeamIds, a.PlayerId, b.PlayerId);
                        Fixed32 allyScale = allies
                            ? ((a.State == UnitState.InCombat || b.State == UnitState.InCombat) ? CombatSeparationScale : SoftSeparationScale)
                            : Fixed32.One;
                        pushMag = overlap * separationStrength * allyScale;
                    }

                    // Accumulate forces for both units (a gets pushed in -dir, b gets pushed in +dir)
                    Fixed32 aPushX = dirX * pushMag * aRatio;
                    Fixed32 aPushZ = dirZ * pushMag * aRatio;
                    Fixed32 bPushX = dirX * pushMag * bRatio;
                    Fixed32 bPushZ = dirZ * pushMag * bRatio;

                    accumPushX[i] -= aPushX.Raw;
                    accumPushZ[i] -= aPushZ.Raw;

                    if (idToIndex.TryGetValue(b.Id, out int bIdx))
                    {
                        accumPushX[bIdx] += bPushX.Raw;
                        accumPushZ[bIdx] += bPushZ.Raw;
                    }
                }
            }

            // Phase 2: Apply all accumulated forces at once
            for (int i = 0; i < count; i++)
            {
                if (accumPushX[i] == 0 && accumPushZ[i] == 0) continue;

                var u = units[i];
                u.SimPosition.x = u.SimPosition.x + new Fixed32(accumPushX[i]);
                u.SimPosition.z = u.SimPosition.z + new Fixed32(accumPushZ[i]);
                ClampToWalkable(u, mapData);
            }
        }

        private static bool IsMovingState(UnitState state)
        {
            return state == UnitState.Moving
                || state == UnitState.MovingToGather
                || state == UnitState.MovingToBuild
                || state == UnitState.MovingToDropoff
                || state == UnitState.MovingToGarrison;
        }

        private void ClampToWalkable(UnitData unit, MapData mapData)
        {
            Vector2Int tile = mapData.WorldToTile(unit.SimPosition);
            if (!mapData.IsWalkable(tile.x, tile.y))
            {
                // Wall-slide: try keeping just the X component of the push
                FixedVector3 slideX = new FixedVector3(
                    unit.SimPosition.x, unit.SimPosition.y, unit.PreviousSimPosition.z);
                Vector2Int tileX = mapData.WorldToTile(slideX);
                if (mapData.IsWalkable(tileX.x, tileX.y))
                {
                    unit.SimPosition = slideX;
                    return;
                }

                // Wall-slide: try keeping just the Z component of the push
                FixedVector3 slideZ = new FixedVector3(
                    unit.PreviousSimPosition.x, unit.SimPosition.y, unit.SimPosition.z);
                Vector2Int tileZ = mapData.WorldToTile(slideZ);
                if (mapData.IsWalkable(tileZ.x, tileZ.y))
                {
                    unit.SimPosition = slideZ;
                    return;
                }

                // Both axes blocked: full revert
                unit.SimPosition = unit.PreviousSimPosition;
            }
        }
    }
}
