using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    /// <summary>
    /// Wild deer, which move as a pack rather than as individuals.
    ///
    /// A pack grazes around a shared anchor and, when startled, bolts away from whatever spooked
    /// it — all of them, together, because one deer noticing is the whole herd noticing. The flee
    /// is short and leashed to the anchor on purpose: a herd that genuinely escaped would strand
    /// the drop-off a player just built beside it, which is the one thing this feature exists to
    /// support. Panic is texture, not relocation.
    ///
    /// Two things startle deer: a soldier or scout walking close, and being hit. Villagers do not
    /// spook them by proximity — they are the hunters, and a pack that fled every approaching
    /// villager could never be worked by villagers slower than it. After a bolt the pack settles
    /// and cannot bolt again for a few seconds, which is what guarantees hunters can close.
    ///
    /// Runs inside the simulation, so everything here is fixed-point and every random choice is a
    /// hash of unit id and tick. No floats, no Unity randomness, no wall-clock time.
    /// </summary>
    public class DeerSystem
    {
        public void Tick(UnitRegistry unitRegistry, SpatialGrid spatialGrid, MapData mapData,
            Fixed32 tickDuration, int currentTick, SimulationConfig config)
        {
            var allUnits = unitRegistry.GetAllUnits();
            int count = allUnits.Count;

            Fixed32 scareRange = Fixed32.FromFloat(config.DeerScareRange);
            Fixed32 scareRangeSq = scareRange * scareRange;
            Fixed32 leashRadius = Fixed32.FromFloat(config.DeerLeashRadius);
            Fixed32 grazeSpeed = Fixed32.FromFloat(config.DeerMoveSpeed);
            Fixed32 fleeSpeed = Fixed32.FromFloat(config.DeerFleeSpeed);

            // Pass one: work out which herds are panicking this tick, and what from. A deer that
            // was hit had its own panic set by the hunt code, so this also spreads that to its
            // pack-mates. Units are walked in registry order so the winning threat is the same on
            // every machine.
            var herdThreat = new Dictionary<int, FixedVector3>();

            for (int i = 0; i < count; i++)
            {
                var deer = allUnits[i];
                if (!deer.IsDeer || deer.State == UnitState.Dead) continue;
                if (deer.HerdId < 0) continue;

                if (deer.PanicTicksRemaining > 0)
                {
                    if (!herdThreat.ContainsKey(deer.HerdId))
                        herdThreat[deer.HerdId] = deer.PanicFrom;
                    continue;
                }

                if (deer.PanicCooldownRemaining > 0) continue;
                if (herdThreat.ContainsKey(deer.HerdId)) continue;

                var nearby = spatialGrid.GetNearby(deer.SimPosition, scareRange);
                for (int j = 0; j < nearby.Count; j++)
                {
                    var other = nearby[j];
                    if (!IsFrightening(other)) continue;

                    Fixed32 dx = other.SimPosition.x - deer.SimPosition.x;
                    Fixed32 dz = other.SimPosition.z - deer.SimPosition.z;
                    if (Fixed32.Abs(dx) > scareRange || Fixed32.Abs(dz) > scareRange) continue;
                    if (dx * dx + dz * dz > scareRangeSq) continue;

                    herdThreat[deer.HerdId] = other.SimPosition;
                    break;
                }
            }

            // Pass two: move every deer.
            for (int i = 0; i < count; i++)
            {
                var deer = allUnits[i];
                if (!deer.IsDeer || deer.State == UnitState.Dead) continue;

                if (deer.PanicCooldownRemaining > 0)
                    deer.PanicCooldownRemaining--;

                bool startingToFlee = deer.PanicTicksRemaining <= 0
                    && deer.PanicCooldownRemaining <= 0
                    && deer.HerdId >= 0
                    && herdThreat.ContainsKey(deer.HerdId);

                if (startingToFlee)
                {
                    deer.PanicFrom = herdThreat[deer.HerdId];
                    deer.PanicTicksRemaining = config.DeerPanicTicks;
                    BoltAwayFrom(deer, mapData, leashRadius, config);
                }

                if (deer.PanicTicksRemaining > 0)
                {
                    deer.PanicTicksRemaining--;
                    if (deer.PanicTicksRemaining <= 0)
                    {
                        // Settled. Stop where it stands and stand still for a while.
                        deer.ClearPath();
                        deer.State = UnitState.Idle;
                        deer.PanicCooldownRemaining = config.DeerPanicCooldownTicks;
                        deer.WanderCooldown = config.DeerPanicCooldownTicks / 2;
                        continue;
                    }

                    WalkPath(deer, mapData, fleeSpeed, tickDuration);
                    continue;
                }

                Graze(deer, mapData, currentTick, config, leashRadius);
                WalkPath(deer, mapData, grazeSpeed, tickDuration);
            }
        }

        /// <summary>
        /// Whether the sight of this unit sends a herd running. Soldiers and scouts do; villagers
        /// do not, so a hunting party can actually reach the pack it was tasked onto.
        /// </summary>
        private static bool IsFrightening(UnitData unit)
        {
            if (unit == null || unit.State == UnitState.Dead) return false;
            if (unit.IsDeer || unit.IsSheep) return false;
            if (unit.IsVillager) return false;
            return unit.PlayerId >= 0;
        }

        /// <summary>
        /// Points the deer away from whatever startled it and sends it running, but never further
        /// from the pack's anchor than the leash allows.
        /// </summary>
        private static void BoltAwayFrom(UnitData deer, MapData mapData, Fixed32 leashRadius,
            SimulationConfig config)
        {
            FixedVector3 away = deer.SimPosition - deer.PanicFrom;
            away.y = Fixed32.Zero;
            Fixed32 awayDist = away.Magnitude();

            FixedVector3 dir;
            if (awayDist.Raw > 0)
            {
                dir = away / awayDist;
            }
            else
            {
                // Startled by something standing exactly on top of it — any direction will do,
                // but it has to be the same one on every machine.
                dir = new FixedVector3(Fixed32.One, Fixed32.Zero, Fixed32.Zero);
            }

            Fixed32 boltDistance = Fixed32.FromFloat(config.DeerLeashRadius) * Fixed32.FromFloat(0.8f);
            FixedVector3 target = deer.SimPosition + dir * boltDistance;

            // Pull the destination back inside the leash circle, so a pack harried from one side
            // drifts around its anchor rather than being pushed off the map.
            FixedVector3 fromAnchor = target - deer.HerdAnchor;
            fromAnchor.y = Fixed32.Zero;
            Fixed32 anchorDist = fromAnchor.Magnitude();
            if (anchorDist > leashRadius && anchorDist.Raw > 0)
                target = deer.HerdAnchor + (fromAnchor / anchorDist) * leashRadius;

            PathTo(deer, mapData, target);
        }

        /// <summary>Idle drift around the pack's anchor, on the same cadence sheep wander on.</summary>
        private static void Graze(UnitData deer, MapData mapData, int currentTick,
            SimulationConfig config, Fixed32 leashRadius)
        {
            if (deer.State != UnitState.Idle) return;

            deer.WanderCooldown--;
            if (deer.WanderCooldown > 0) return;

            int hash = (int)((uint)(deer.Id * 31 + currentTick) * 2654435761u);
            float angle = (hash & 0xFFFF) / 65536f * 6.2832f;
            float dist = ((hash >> 16) & 0xFFFF) / 65536f * config.DeerWanderRadius;

            FixedVector3 target = new FixedVector3(
                deer.HerdAnchor.x + Fixed32.FromFloat((float)System.Math.Cos(angle) * dist),
                deer.HerdAnchor.y,
                deer.HerdAnchor.z + Fixed32.FromFloat((float)System.Math.Sin(angle) * dist));

            PathTo(deer, mapData, target);

            // Next graze in 3-6 seconds.
            deer.WanderCooldown = 90 + ((hash >> 8) & 0xFF) % 90;
        }

        private static void PathTo(UnitData deer, MapData mapData, FixedVector3 target)
        {
            Vector2Int startTile = mapData.WorldToTile(deer.SimPosition);
            Vector2Int goalTile = mapData.WorldToTile(target);

            if (startTile == goalTile) return;
            if (!mapData.IsWalkable(goalTile.x, goalTile.y))
            {
                goalTile = GridPathfinder.FindNearestWalkableTile(mapData, goalTile, 4);
                if (!mapData.IsWalkable(goalTile.x, goalTile.y)) return;
                target = mapData.TileToWorldFixed(goalTile.x, goalTile.y);
            }

            var path = GridPathfinder.FindPath(mapData, startTile, goalTile, -1, null);
            if (path.Count == 0) return;

            deer.SetPath(path);
            deer.FinalDestination = target;
            deer.State = UnitState.Moving;
        }

        /// <summary>
        /// Walks the current path at the given speed. Deer are skipped by the ordinary movement
        /// system, the same way sheep are, so their locomotion lives here.
        /// </summary>
        private static void WalkPath(UnitData deer, MapData mapData, Fixed32 speed, Fixed32 tickDuration)
        {
            if (deer.State != UnitState.Moving || !deer.HasPath) return;

            Fixed32 remainingStep = speed * tickDuration;
            FixedVector3 lastDir = FixedVector3.Zero;

            while (remainingStep > Fixed32.Zero && deer.HasPath)
            {
                bool isFinal = deer.CurrentPathIndex == deer.Path.Count - 1;
                FixedVector3 waypoint = isFinal
                    ? deer.FinalDestination
                    : mapData.TileToWorldFixed(
                        deer.Path[deer.CurrentPathIndex].x,
                        deer.Path[deer.CurrentPathIndex].y);

                FixedVector3 toWp = waypoint - deer.SimPosition;
                Fixed32 wpDist = toWp.Magnitude();

                if (wpDist <= remainingStep || wpDist < Fixed32.FromFloat(0.05f))
                {
                    if (wpDist.Raw > 0) lastDir = toWp / wpDist;
                    deer.SimPosition = waypoint;
                    remainingStep = remainingStep - wpDist;
                    deer.CurrentPathIndex++;
                }
                else
                {
                    FixedVector3 dir = toWp / wpDist;
                    deer.SimPosition = deer.SimPosition + dir * remainingStep;
                    lastDir = dir;
                    remainingStep = Fixed32.Zero;
                }
            }

            if (lastDir.x.Raw != 0 || lastDir.z.Raw != 0)
                deer.SimFacing = lastDir;

            if (!deer.HasPath)
                deer.State = UnitState.Idle;
        }

        /// <summary>
        /// Startles a pack because one of its members was struck. Called from the hunt code rather
        /// than detected here, since a hit is an event and this system only sees state.
        /// </summary>
        public static void StartleFromHit(UnitData deer, FixedVector3 attackerPosition, SimulationConfig config)
        {
            if (deer == null || !deer.IsDeer) return;
            if (deer.PanicCooldownRemaining > 0) return;
            if (deer.PanicTicksRemaining > 0) return;

            deer.PanicFrom = attackerPosition;
            deer.PanicTicksRemaining = config.DeerPanicTicks;
        }
    }
}
