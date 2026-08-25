using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public class UnitCombatSystem
    {
        private static readonly Fixed32 TurnRate = Fixed32.FromFloat(0.3f);
        private static readonly Fixed32 FacingThreshold = Fixed32.FromFloat(0.9f); // cos(~26 deg)
        private const int RecentHitWindow = 40; // ticks (~2 sec at 20 TPS) — 360° awareness after being struck
        private static readonly Fixed32 LeashRange = Fixed32.FromFloat(12f);

        // A unit has to be able to reach anything it is willing to aggro onto. Leashing everyone at
        // a flat 12 tiles while a Scout detects at 20 pulled it back before it ever arrived; it then
        // re-acquired the same target and set off again, yo-yoing forever and never closing to
        // charge range. The leash therefore never sits tighter than the unit's own detection range.
        private static readonly Fixed32 LeashReachMargin = Fixed32.FromFloat(3f);

        private static Fixed32 LeashRangeFor(UnitData unit)
        {
            Fixed32 reach = unit.DetectionRange + LeashReachMargin;
            return reach > LeashRange ? reach : LeashRange;
        }

        // Charge
        private static readonly Fixed32 ChargeSpeedMultiplier = Fixed32.FromFloat(1.5f);
        // A charge fires when the enemy is spotted within this distance, rather than beyond it.
        // The unit does not need a run-up: closing from five tiles or from arm's length both
        // qualify, so most engagements open with a charge whenever the cooldown is clear.
        private static readonly Fixed32 ChargeMaxDistance = Fixed32.FromFloat(5f);
        private static readonly Fixed32 ChargeMaxDistanceSq = ChargeMaxDistance * ChargeMaxDistance;
        private static readonly Fixed32 ChargeFacingThreshold = Fixed32.FromFloat(0.7f);
        private static readonly Fixed32 NegChargeFacingThreshold = new Fixed32(-45875); // -0.7f * 65536
        private static readonly Fixed32 BuildingReach = Fixed32.FromInt(2);
        // A charge's payoff scales with the momentum behind it: every hit gets a baseline bonus,
        // and a unit that has been sprinting longer hits proportionally harder. All of it is
        // integer or fixed-point arithmetic so the simulation stays deterministic in lockstep.
        // Reached after a second of sprinting. The meter holds two seconds, but a charge that
        // begins inside five tiles rarely runs that long, so the ramp has to fit the window.
        private const int ChargeMomentumFullTicks = 30;

        // Damage: 1.25x from a standing start, rising to 2.5x at full momentum.
        private const int ChargeDamageBasePercent = 125;
        private const int ChargeDamageMomentumPercent = 125;

        // Charge stamina behaves like a sprint meter. It is counted in thirds of a tick so both
        // the drain and the regen stay whole numbers, which keeps the simulation deterministic.
        // Full meter = 2s of sprinting; empty to full = 6s.
        /// <summary>
        /// Full charge meter. Public so the view can draw it as a fraction; the simulation remains
        /// the only thing that changes it.
        /// </summary>
        public const int ChargeStaminaMax = 180;
        private const int ChargeStaminaDrainPerTick = 3;
        private const int ChargeStaminaRegenPerTick = 1;

        // Enough meter left to be worth committing to, so a charge cannot flicker on for a frame.
        private const int ChargeStaminaMinToStart = 45;
        private static readonly Fixed32 ChargeKnockbackBase = Fixed32.FromFloat(0.5f);
        private static readonly Fixed32 ChargeKnockbackMomentum = Fixed32.FromFloat(1.3f);
        private const int ChargeStunBaseTicks = 5;      // ~0.17s at 30 TPS
        private const int ChargeStunMomentumTicks = 13; // up to ~0.6s total


        /// <summary>
        /// Starts a charge if the unit is in a position to make one. Called every tick while
        /// closing on a target rather than only at the moment of spotting one: units detect enemies
        /// from 8 to 20 tiles away but can only charge inside <see cref="ChargeMaxDistance"/>, so a
        /// single test on contact almost always failed and the charge never fired at all.
        /// </summary>
        private static void TryBeginCharge(UnitData unit, UnitData enemy, Fixed32 distSq)
        {
            if (unit.IsCharging) return;
            if (unit.ChargeStamina < ChargeStaminaMinToStart) return;
            if (distSq > ChargeMaxDistanceSq) return;

            Fixed32 dist = Fixed32.Sqrt(distSq);
            if (dist.Raw <= 0) return;

            Fixed32 dx = enemy.SimPosition.x - unit.SimPosition.x;
            Fixed32 dz = enemy.SimPosition.z - unit.SimPosition.z;
            Fixed32 facingDot = (unit.SimFacing.x * dx + unit.SimFacing.z * dz) / dist;
            if (facingDot <= ChargeFacingThreshold) return;

            unit.IsCharging = true;
            unit.ChargeMomentum = 0; // a fresh run builds its own momentum
        }

        private List<int> deadList = new List<int>();
        private List<int> deadBuildingList = new List<int>();

        public (List<int> deadUnits, List<int> deadBuildings) Tick(UnitRegistry registry, BuildingRegistry buildingRegistry, int currentTick, Fixed32 tickDuration, MapData mapData, ProjectileRegistry projectileRegistry = null, SimulationConfig config = null, int[] playerTeamIds = null, SpatialGrid spatialGrid = null, Fixed32 projectileSpeed = default)
        {
            deadList.Clear();
            deadBuildingList.Clear();
            var allUnits = registry.GetAllUnits();
            int count = allUnits.Count;

            for (int i = 0; i < count; i++)
            {
                var unit = allUnits[i];
                if (unit.State == UnitState.Dead) continue;
                if (unit.IsHuntable) continue; // animals do not fight back
                if (unit.IsHealer) continue;

                // Always tick cooldowns so units reload while moving
                if (unit.AttackCooldownRemaining > 0 && unit.CombatTargetBuildingId < 0)
                    unit.AttackCooldownRemaining--;
                // Sprint meter: spend it while charging, recover it the rest of the time.
                if (unit.IsCharging)
                {
                    unit.ChargeStamina -= ChargeStaminaDrainPerTick;
                    unit.ChargeMomentum++;
                    if (unit.ChargeStamina <= 0)
                    {
                        unit.ChargeStamina = 0;
                        unit.IsCharging = false; // ran out of puff mid-run
                    }
                }
                else if (unit.ChargeStamina < ChargeStaminaMax)
                {
                    unit.ChargeStamina += ChargeStaminaRegenPerTick;
                    if (unit.ChargeStamina > ChargeStaminaMax)
                        unit.ChargeStamina = ChargeStaminaMax;
                }
                if (unit.ChargeStunRemaining > 0)
                {
                    unit.ChargeStunRemaining--;
                    continue;
                }

                bool recentlyDamaged = unit.LastDamageTick > 0 && (currentTick - unit.LastDamageTick) < RecentHitWindow;

                // Worker states: skip aggro unless personally attacked (retaliation)
                if (unit.State == UnitState.Gathering || unit.State == UnitState.MovingToGather ||
                    unit.State == UnitState.Constructing || unit.State == UnitState.MovingToBuild ||
                    unit.State == UnitState.DroppingOff || unit.State == UnitState.MovingToDropoff ||
                    unit.State == UnitState.MovingToGarrison || unit.State == UnitState.MovingToSlaughter)
                {
                    if (!recentlyDamaged) continue;
                }

                // Player-commanded units: always skip aggro (move commands override combat)
                if (unit.PlayerCommanded && unit.State != UnitState.InCombat) continue;
                if (unit.HasQueuedCommands) continue;

                // Try to keep locked target if still valid
                Fixed32 detectionSq = unit.DetectionRange * unit.DetectionRange;
                UnitData closestEnemy = null;
                Fixed32 closestDistSq = Fixed32.FromInt(9999);

                if (unit.CombatTargetId >= 0)
                {
                    var locked = registry.GetUnit(unit.CombatTargetId);
                    if (locked != null && locked.State != UnitState.Dead)
                    {
                        Fixed32 dx = locked.SimPosition.x - unit.SimPosition.x;
                        Fixed32 dz = locked.SimPosition.z - unit.SimPosition.z;
                        Fixed32 distSq;
                        if (Fixed32.Abs(dx) > unit.DetectionRange || Fixed32.Abs(dz) > unit.DetectionRange)
                            distSq = detectionSq + Fixed32.One; // guaranteed out of range
                        else
                            distSq = dx * dx + dz * dz;
                        if (distSq <= detectionSq)
                        {
                            closestEnemy = locked;
                            closestDistSq = distSq;
                        }
                    }

                    if (closestEnemy == null)
                    {
                        if (unit.PlayerCommanded)
                        {
                            // Player explicitly targeted this unit — re-pathfind
                            var target = registry.GetUnit(unit.CombatTargetId);
                            if (target != null && target.State != UnitState.Dead)
                            {
                                Vector2Int startTile = mapData.WorldToTile(unit.SimPosition);
                                Vector2Int goalTile = mapData.WorldToTile(target.SimPosition);
                                var path = GridPathfinder.FindPath(mapData, startTile, goalTile, unit.PlayerId, buildingRegistry);
                                if (path.Count > 0)
                                {
                                    unit.SetPath(path);
                                    unit.FinalDestination = target.SimPosition;
                                    unit.State = UnitState.Moving;
                                    unit.ChaseBlockedTicks = 0;
                                    continue;
                                }
                            }
                            // Target dead or unreachable — give up
                            unit.CombatTargetId = -1;
                            unit.PlayerCommanded = false;
                        }
                        else
                        {
                            unit.CombatTargetId = -1;
                        }
                        unit.ChaseBlockedTicks = 0;
                    }
                }

                // Scan for closest enemy if no locked target (spatial grid accelerated)
                if (closestEnemy == null)
                {
                    var nearby = spatialGrid != null
                        ? spatialGrid.GetNearby(unit.SimPosition, unit.DetectionRange)
                        : allUnits;
                    int nearbyCount = nearby.Count;

                    for (int j = 0; j < nearbyCount; j++)
                    {
                        var other = nearby[j];
                        if (TeamHelper.AreAllies(playerTeamIds, other.PlayerId, unit.PlayerId)) continue;
                        if (other.State == UnitState.Dead) continue;
                        if (other.IsHuntable) continue; // never auto-attack livestock or game

                        Fixed32 dx = other.SimPosition.x - unit.SimPosition.x;
                        if (Fixed32.Abs(dx) > unit.DetectionRange) continue;
                        Fixed32 dz = other.SimPosition.z - unit.SimPosition.z;
                        if (Fixed32.Abs(dz) > unit.DetectionRange) continue;

                        Fixed32 distSq = dx * dx + dz * dz;

                        if (distSq <= detectionSq && (distSq < closestDistSq || (distSq == closestDistSq && other.Id < closestEnemy.Id)))
                        {
                            closestDistSq = distSq;
                            closestEnemy = other;
                        }
                    }

                    if (closestEnemy != null)
                    {
                        unit.CombatTargetId = closestEnemy.Id;
                        unit.CombatTargetBuildingId = -1; // unit target takes priority
                        unit.ChaseBlockedTicks = 0;
                    }
                }

                // Building auto-aggro fallback (only when no enemy unit found)
                // Stagger across ticks: each unit only checks every 5th tick (buildings don't move)
                if (closestEnemy == null && unit.CombatTargetBuildingId < 0 && unit.Id % 5 == currentTick % 5)
                {
                    var allBuildings = buildingRegistry.GetAllBuildings();
                    Fixed32 closestBuildingDistSq = detectionSq;
                    int closestBuildingId = -1;

                    for (int b = 0; b < allBuildings.Count; b++)
                    {
                        var bld = allBuildings[b];
                        if (bld.IsDestroyed) continue;
                        if (TeamHelper.AreAllies(playerTeamIds, bld.PlayerId, unit.PlayerId)) continue;

                        Fixed32 bDx = bld.SimPosition.x - unit.SimPosition.x;
                        if (Fixed32.Abs(bDx) > unit.DetectionRange) continue;
                        Fixed32 bDz = bld.SimPosition.z - unit.SimPosition.z;
                        if (Fixed32.Abs(bDz) > unit.DetectionRange) continue;

                        Fixed32 bDistSq = bDx * bDx + bDz * bDz;
                        if (bDistSq < closestBuildingDistSq || (bDistSq == closestBuildingDistSq && bld.Id < closestBuildingId))
                        {
                            closestBuildingDistSq = bDistSq;
                            closestBuildingId = bld.Id;
                        }
                    }

                    if (closestBuildingId >= 0)
                    {
                        unit.CombatTargetBuildingId = closestBuildingId;
                        if (!unit.HasLeash)
                        {
                            unit.LeashOrigin = unit.SimPosition;
                            unit.LeashFacing = unit.SimFacing;
                            unit.HasLeash = true;
                        }
                        if (!unit.HasSavedPath)
                            unit.SavePathForCombat();
                        unit.ClearPath();
                        unit.State = UnitState.InCombat;
                        continue;
                    }
                }

                // Leash check: if AI-aggroed unit strayed too far, force disengage
                if (closestEnemy != null && unit.HasLeash && unit.State == UnitState.InCombat)
                {
                    Fixed32 lx = unit.SimPosition.x - unit.LeashOrigin.x;
                    Fixed32 lz = unit.SimPosition.z - unit.LeashOrigin.z;
                    Fixed32 leash = LeashRangeFor(unit);
                    Fixed32 leashSq = leash * leash;
                    if (Fixed32.Abs(lx) > leash || Fixed32.Abs(lz) > leash || lx * lx + lz * lz > leashSq)
                        closestEnemy = null;
                }

                if (closestEnemy == null)
                {
                    unit.CombatTargetId = -1;
                    unit.IsCharging = false; // no cooldown — charge didn't land
                    unit.ChaseBlockedTicks = 0;
                    if (unit.State == UnitState.InCombat && unit.CombatTargetBuildingId < 0)
                    {
                        if (unit.HasSavedPath)
                        {
                            unit.RestoreSavedPath();
                        }
                        else if (unit.HasLeash)
                        {
                            ReturnToLeash(unit, mapData, buildingRegistry);
                        }
                        else
                        {
                            unit.State = UnitState.Idle;
                        }
                    }
                    continue;
                }

                // Aggro: any unit that detects an enemy enters combat
                if (unit.State == UnitState.Moving || unit.State == UnitState.MovingToGather || unit.State == UnitState.MovingToBuild || unit.State == UnitState.MovingToDropoff || unit.State == UnitState.MovingToGarrison)
                {
                    if (!unit.HasLeash)
                    {
                        unit.LeashOrigin = unit.SimPosition;
                        unit.LeashFacing = unit.SimFacing;
                        unit.HasLeash = true;
                    }
                    if (!unit.HasSavedPath)
                        unit.SavePathForCombat();
                    unit.ClearPath();
                    unit.State = UnitState.InCombat;
                    TryBeginCharge(unit, closestEnemy, closestDistSq);
                }
                else if (unit.State == UnitState.Idle || unit.State == UnitState.Gathering || unit.State == UnitState.Constructing)
                {
                    if (!unit.HasLeash)
                    {
                        unit.LeashOrigin = unit.SimPosition;
                        unit.LeashFacing = unit.SimFacing;
                        unit.HasLeash = true;
                    }
                    unit.State = UnitState.InCombat;
                    TryBeginCharge(unit, closestEnemy, closestDistSq);
                }

                // Compute direction to enemy
                FixedVector3 toEnemy = closestEnemy.SimPosition - unit.SimPosition;
                toEnemy.y = Fixed32.Zero;
                Fixed32 toEnemyMag = toEnemy.Magnitude();
                if (toEnemyMag.Raw == 0) continue;

                FixedVector3 targetDir = toEnemy / toEnemyMag;

                // Set target facing so view shows rotation
                unit.HasTargetFacing = true;
                unit.TargetFacing = targetDir;

                // Rotate SimFacing toward target
                // Break 180° turn deadlock: when nearly opposite, turn toward perpendicular first
                FixedVector3 turnTarget = targetDir;
                Fixed32 preDot = unit.SimFacing.x * targetDir.x + unit.SimFacing.z * targetDir.z;
                if (preDot < NegChargeFacingThreshold)
                {
                    // Clockwise perpendicular in XZ plane
                    turnTarget = new FixedVector3(unit.SimFacing.z, Fixed32.Zero, -unit.SimFacing.x);
                }

                FixedVector3 newFacing = new FixedVector3(
                    unit.SimFacing.x + (turnTarget.x - unit.SimFacing.x) * TurnRate,
                    Fixed32.Zero,
                    unit.SimFacing.z + (turnTarget.z - unit.SimFacing.z) * TurnRate
                );
                Fixed32 newFacingMag = newFacing.Magnitude();
                if (newFacingMag.Raw > 0)
                    unit.SimFacing = newFacing / newFacingMag;

                // Check if facing the target
                Fixed32 dot = unit.SimFacing.x * targetDir.x + unit.SimFacing.z * targetDir.z;

                // Attack if within range (requires facing), otherwise chase immediately
                Fixed32 attackRangeSq = unit.AttackRange * unit.AttackRange;
                if (closestDistSq <= attackRangeSq)
                {
                    if (dot < FacingThreshold) continue;
                    if (unit.AttackCooldownRemaining > 0) continue;

                    // Attack
                    int damage = unit.AttackDamage;

                    // Hunting is work, not combat. A villager's fighting stats are deliberately
                    // useless — one damage every three seconds — which would make bringing down a
                    // deer take minutes. Against game they swing at a working pace instead.
                    bool isHunting = unit.IsVillager && closestEnemy.IsDeer;
                    if (isHunting && config != null)
                        damage = config.DeerVillagerDamage;

                    bool wasCharging = unit.IsCharging;
                    // How much of a full-length sprint this charge managed, 0 at a standing start.
                    int chargeMomentum = wasCharging
                        ? (unit.ChargeMomentum < ChargeMomentumFullTicks ? unit.ChargeMomentum : ChargeMomentumFullTicks)
                        : 0;
                    if (unit.IsCharging)
                    {
                        int percent = ChargeDamageBasePercent
                            + (ChargeDamageMomentumPercent * chargeMomentum) / ChargeMomentumFullTicks;
                        damage = (unit.AttackDamage * percent) / 100;
                        // The run is over, but nothing extra is confiscated: the meter has already
                        // paid for exactly the time spent sprinting.
                        unit.IsCharging = false;
                    }

                    // Bonus damage (rock-paper-scissors)
                    if (unit.BonusDamageVsType >= 0 && closestEnemy.UnitType == unit.BonusDamageVsType)
                        damage += unit.BonusDamageAmount;
                    if (unit.BonusDamageVsType2 >= 0 && closestEnemy.UnitType == unit.BonusDamageVsType2)
                        damage += unit.BonusDamageAmount2;

                    unit.AttackCooldownRemaining = isHunting && config != null
                        ? config.DeerHuntSwingTicks
                        : unit.AttackCooldownTicks;

                    // Combat feedback for view layer (attack-dash animation)
                    unit.LastAttackTick = currentTick;
                    unit.LastAttackTargetPos = closestEnemy.SimPosition;

                    if (unit.IsRanged && projectileRegistry != null && config != null)
                    {
                        // Ranged: spawn projectile — damage applied on impact
                        bool isBolt = unit.UnitType == 8; // Crossbowman fires bolts (flat trajectory)
                        projectileRegistry.CreateProjectile(unit.Id, closestEnemy.Id,
                            unit.SimPosition, damage, projectileSpeed, isBolt);
                    }
                    else
                    {
                        // Melee: instant damage
                        int finalDamage = damage - closestEnemy.MeleeArmor;
                        if (finalDamage < 1) finalDamage = 1;
                        closestEnemy.CurrentHealth -= finalDamage;

                        closestEnemy.LastDamageTick = currentTick;
                        closestEnemy.LastDamageFromPos = unit.SimPosition;

                        // Being struck sets the whole pack running, even from a villager the deer
                        // would otherwise have ignored.
                        if (closestEnemy.IsDeer && config != null)
                            DeerSystem.StartleFromHit(closestEnemy, unit.SimPosition, config);

                        // Only Knights (UnitType 7) produce a visible knock-up on charge hits;
                        // other charging units still apply bonus damage but don't launch the target.
                        if (wasCharging && unit.UnitType == 7)
                        {
                            closestEnemy.LastChargeHitTick = currentTick;
                            closestEnemy.LastChargeHitFromPos = unit.SimPosition;
                            closestEnemy.ChargeStunRemaining = ChargeStunBaseTicks
                                + (ChargeStunMomentumTicks * chargeMomentum) / ChargeMomentumFullTicks;

                            // Knockback displacement, also scaled by the momentum behind the hit.
                            Fixed32 knockDist = ChargeKnockbackBase + ChargeKnockbackMomentum
                                * Fixed32.FromInt(chargeMomentum) / Fixed32.FromInt(ChargeMomentumFullTicks);
                            var knockDir = targetDir;
                            var displacement = new FixedVector3(
                                knockDir.x * knockDist,
                                Fixed32.Zero,
                                knockDir.z * knockDist);
                            var newPos = closestEnemy.SimPosition + displacement;
                            Vector2Int newTile = mapData.WorldToTile(newPos);
                            if (mapData.IsWalkable(newTile.x, newTile.y))
                                closestEnemy.SimPosition = newPos;
                        }

                        if (closestEnemy.CurrentHealth <= 0)
                        {
                            if (closestEnemy.IsDummy)
                            {
                                closestEnemy.CurrentHealth = 1;
                            }
                            else
                            {
                                closestEnemy.State = UnitState.Dead;
                                deadList.Add(closestEnemy.Id);
                            }
                        }
                    }
                }
                else if (unit.AttackCooldownRemaining <= 0)
                {
                    // Re-checked on the way in, so a unit that spotted its target from beyond
                    // charge range still breaks into a run as it crosses the threshold.
                    TryBeginCharge(unit, closestEnemy, closestDistSq);

                    // Chase immediately — turns while running
                    Fixed32 step = unit.MoveSpeed * tickDuration;
                    if (unit.IsCharging)
                        step = step * ChargeSpeedMultiplier;
                    FixedVector3 newPos = unit.SimPosition + targetDir * step;
                    Vector2Int newTile = mapData.WorldToTile(newPos);
                    if (mapData.IsWalkable(newTile.x, newTile.y))
                    {
                        unit.SimPosition = newPos;
                        unit.ChaseBlockedTicks = 0;
                    }
                    else
                    {
                        unit.ChaseBlockedTicks++;
                        if (unit.ChaseBlockedTicks >= 3)
                        {
                            unit.ChaseBlockedTicks = 0;
                            Vector2Int startTile = mapData.WorldToTile(unit.SimPosition);
                            Vector2Int goalTile = mapData.WorldToTile(closestEnemy.SimPosition);
                            var path = GridPathfinder.FindPath(mapData, startTile, goalTile, unit.PlayerId, buildingRegistry);
                            if (path.Count > 0)
                            {
                                unit.SetPath(path);
                                unit.FinalDestination = closestEnemy.SimPosition;
                                unit.PlayerCommanded = true;
                                unit.State = UnitState.Moving;
                            }
                        }
                    }
                }
            }

            // --- Unit vs building combat (player-commanded only) ---
            for (int i = 0; i < count; i++)
            {
                var unit = allUnits[i];
                if (unit.State == UnitState.Dead) continue;
                if (unit.CombatTargetBuildingId < 0) continue;
                if (unit.State == UnitState.Moving) continue;

                var building = buildingRegistry.GetBuilding(unit.CombatTargetBuildingId);
                if (building == null || building.IsDestroyed)
                {
                    unit.CombatTargetBuildingId = -1;
                    if (unit.State == UnitState.InCombat)
                    {
                        if (unit.HasSavedPath)
                        {
                            unit.RestoreSavedPath();
                        }
                        else if (unit.HasLeash)
                        {
                            ReturnToLeash(unit, mapData, buildingRegistry);
                        }
                        else
                        {
                            unit.State = UnitState.Idle;
                        }
                    }
                    continue;
                }

                if (TeamHelper.AreAllies(playerTeamIds, building.PlayerId, unit.PlayerId))
                {
                    unit.CombatTargetBuildingId = -1;
                    if (unit.State == UnitState.InCombat) unit.State = UnitState.Idle;
                    continue;
                }

                FixedVector3 toBuilding = building.SimPosition - unit.SimPosition;
                toBuilding.y = Fixed32.Zero;

                // Use slightly extended range for buildings (footprint means center is farther)
                Fixed32 effectiveRange = unit.AttackRange + BuildingReach;

                // Overflow guard: skip distSq if axis distance already exceeds effective range
                if (Fixed32.Abs(toBuilding.x) > effectiveRange || Fixed32.Abs(toBuilding.z) > effectiveRange)
                {
                    // Too far — chase toward building
                    Fixed32 absDx = Fixed32.Abs(toBuilding.x);
                    Fixed32 absDz = Fixed32.Abs(toBuilding.z);
                    Fixed32 approxDist = absDx > absDz ? absDx : absDz;
                    if (approxDist.Raw > 0)
                    {
                        FixedVector3 dir = toBuilding / approxDist;
                        Fixed32 step = unit.MoveSpeed * tickDuration;
                        FixedVector3 newPos = unit.SimPosition + dir * step;
                        Vector2Int newTile = mapData.WorldToTile(newPos);
                        if (mapData.IsWalkable(newTile.x, newTile.y))
                        {
                            unit.SimPosition = newPos;
                            unit.ChaseBlockedTicks = 0;
                        }
                        else
                        {
                            unit.ChaseBlockedTicks++;
                            if (unit.ChaseBlockedTicks >= 3)
                            {
                                unit.ChaseBlockedTicks = 0;
                                Vector2Int startTile = mapData.WorldToTile(unit.SimPosition);
                                Vector2Int goalTile = mapData.WorldToTile(building.SimPosition);
                                var path = GridPathfinder.FindPath(mapData, startTile, goalTile, unit.PlayerId, buildingRegistry);
                                if (path.Count > 0)
                                {
                                    unit.SetPath(path);
                                    unit.FinalDestination = building.SimPosition;
                                    unit.PlayerCommanded = true;
                                    unit.State = UnitState.Moving;
                                    continue;
                                }
                            }
                        }
                        unit.SimFacing = dir;
                    }
                    unit.State = UnitState.InCombat;
                    continue;
                }

                Fixed32 distSq = toBuilding.x * toBuilding.x + toBuilding.z * toBuilding.z;
                Fixed32 attackRangeSq = unit.AttackRange * unit.AttackRange;
                Fixed32 effectiveRangeSq = effectiveRange * effectiveRange;

                if (distSq <= effectiveRangeSq)
                {
                    unit.State = UnitState.InCombat;
                    unit.ClearPath();

                    Fixed32 dist = Fixed32.Sqrt(distSq);
                    if (dist.Raw > 0)
                    {
                        unit.HasTargetFacing = true;
                        unit.TargetFacing = toBuilding / dist;
                    }

                    if (unit.AttackCooldownRemaining > 0)
                    {
                        unit.AttackCooldownRemaining--;
                        continue;
                    }

                    int damage = unit.AttackDamage + unit.BonusDamageVsBuildings - building.Armor;
                    if (damage < 1) damage = 1;
                    unit.AttackCooldownRemaining = unit.AttackCooldownTicks;

                    // Combat feedback
                    unit.LastAttackTick = currentTick;
                    unit.LastAttackTargetPos = building.SimPosition;

                    if (unit.IsRanged && projectileRegistry != null && config != null)
                    {
                        // Ranged: spawn projectile — damage applied on impact
                        bool isBolt = unit.UnitType == 8;
                        projectileRegistry.CreateBuildingProjectile(unit.Id, building.Id,
                            unit.SimPosition, damage, projectileSpeed, isBolt);
                    }
                    else
                    {
                        // Melee: instant damage
                        building.CurrentHealth -= damage;
                        building.LastDamageTick = currentTick;
                        building.LastDamageFromPos = unit.SimPosition;
                    }

                    if (building.IsDestroyed)
                    {
                        if (!deadBuildingList.Contains(building.Id))
                            deadBuildingList.Add(building.Id);
                        unit.CombatTargetBuildingId = -1;
                        if (unit.HasSavedPath)
                        {
                            unit.RestoreSavedPath();
                        }
                        else if (unit.HasLeash)
                        {
                            ReturnToLeash(unit, mapData, buildingRegistry);
                        }
                        else
                        {
                            unit.State = UnitState.Idle;
                        }
                    }
                }
                else
                {
                    // Chase toward building
                    if (unit.AttackCooldownRemaining > 0)
                        unit.AttackCooldownRemaining--;

                    Fixed32 dist = Fixed32.Sqrt(distSq);
                    if (dist.Raw > 0)
                    {
                        FixedVector3 dir = toBuilding / dist;
                        Fixed32 step = unit.MoveSpeed * tickDuration;
                        FixedVector3 newPos = unit.SimPosition + dir * step;
                        Vector2Int newTile = mapData.WorldToTile(newPos);
                        if (mapData.IsWalkable(newTile.x, newTile.y))
                        {
                            unit.SimPosition = newPos;
                            unit.ChaseBlockedTicks = 0;
                        }
                        else
                        {
                            unit.ChaseBlockedTicks++;
                            if (unit.ChaseBlockedTicks >= 3)
                            {
                                unit.ChaseBlockedTicks = 0;
                                Vector2Int startTile = mapData.WorldToTile(unit.SimPosition);
                                Vector2Int goalTile = mapData.WorldToTile(building.SimPosition);
                                var path = GridPathfinder.FindPath(mapData, startTile, goalTile, unit.PlayerId, buildingRegistry);
                                if (path.Count > 0)
                                {
                                    unit.SetPath(path);
                                    unit.FinalDestination = building.SimPosition;
                                    unit.PlayerCommanded = true;
                                    unit.State = UnitState.Moving;
                                    continue;
                                }
                            }
                        }
                        unit.SimFacing = dir;
                    }
                    unit.State = UnitState.InCombat;
                }
            }

            return (deadList, deadBuildingList);
        }

        private void ReturnToLeash(UnitData unit, MapData mapData, BuildingRegistry buildingRegistry)
        {
            Vector2Int startTile = mapData.WorldToTile(unit.SimPosition);
            Vector2Int goalTile = mapData.WorldToTile(unit.LeashOrigin);

            var path = GridPathfinder.FindPath(mapData, startTile, goalTile, unit.PlayerId, buildingRegistry);
            if (path.Count > 0)
            {
                unit.SetPath(path);
                unit.FinalDestination = unit.LeashOrigin;
                unit.State = UnitState.Moving;
                unit.HasTargetFacing = true;
                unit.TargetFacing = unit.LeashFacing;
            }
            else
            {
                // Can't path back — just idle in place
                unit.State = UnitState.Idle;
                unit.SimFacing = unit.LeashFacing;
            }
            unit.HasLeash = false;
        }
    }
}
