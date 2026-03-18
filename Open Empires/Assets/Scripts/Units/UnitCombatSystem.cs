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
        private static readonly Fixed32 LeashRangeSq = LeashRange * LeashRange;

        // Charge
        private static readonly Fixed32 ChargeSpeedMultiplier = Fixed32.FromFloat(1.5f);
        private static readonly Fixed32 ChargeMinDistance = Fixed32.FromFloat(4f);
        private static readonly Fixed32 ChargeMinDistanceSq = ChargeMinDistance * ChargeMinDistance;
        private static readonly Fixed32 ChargeFacingThreshold = Fixed32.FromFloat(0.7f);
        private static readonly Fixed32 NegChargeFacingThreshold = new Fixed32(-45875); // -0.7f * 65536
        private static readonly Fixed32 BuildingReach = Fixed32.FromInt(2);
        private const int ChargeDamageMultiplier = 2;
        private const int ChargeCooldownTicks = 200; // 10 sec at 20 TPS

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
                if (unit.IsSheep) continue;
                if (unit.IsHealer) continue;

                // Always tick cooldowns so units reload while moving
                if (unit.AttackCooldownRemaining > 0 && unit.CombatTargetBuildingId < 0)
                    unit.AttackCooldownRemaining--;
                if (unit.ChargeCooldownRemaining > 0)
                    unit.ChargeCooldownRemaining--;

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
                        unit.CombatTargetId = -1;
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
                        if (other.IsSheep) continue;

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
                    if (Fixed32.Abs(lx) > LeashRange || Fixed32.Abs(lz) > LeashRange || lx * lx + lz * lz > LeashRangeSq)
                        closestEnemy = null;
                }

                if (closestEnemy == null)
                {
                    unit.CombatTargetId = -1;
                    unit.IsCharging = false; // no cooldown — charge didn't land
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
                    // Check charge eligibility
                    if (unit.ChargeCooldownRemaining == 0 && closestDistSq > ChargeMinDistanceSq)
                    {
                        Fixed32 eDx = closestEnemy.SimPosition.x - unit.SimPosition.x;
                        Fixed32 eDz = closestEnemy.SimPosition.z - unit.SimPosition.z;
                        Fixed32 eDist = Fixed32.Sqrt(closestDistSq);
                        if (eDist.Raw > 0)
                        {
                            Fixed32 chargeDot = (unit.SimFacing.x * eDx + unit.SimFacing.z * eDz) / eDist;
                            if (chargeDot > ChargeFacingThreshold)
                                unit.IsCharging = true;
                        }
                    }
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
                    // Check charge eligibility
                    if (unit.ChargeCooldownRemaining == 0 && closestDistSq > ChargeMinDistanceSq)
                    {
                        Fixed32 eDx = closestEnemy.SimPosition.x - unit.SimPosition.x;
                        Fixed32 eDz = closestEnemy.SimPosition.z - unit.SimPosition.z;
                        Fixed32 eDist = Fixed32.Sqrt(closestDistSq);
                        if (eDist.Raw > 0)
                        {
                            Fixed32 chargeDot = (unit.SimFacing.x * eDx + unit.SimFacing.z * eDz) / eDist;
                            if (chargeDot > ChargeFacingThreshold)
                                unit.IsCharging = true;
                        }
                    }
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
                    if (unit.IsCharging)
                    {
                        damage = unit.AttackDamage * ChargeDamageMultiplier;
                        unit.IsCharging = false;
                        unit.ChargeCooldownRemaining = ChargeCooldownTicks;
                    }

                    // Bonus damage (rock-paper-scissors)
                    if (unit.BonusDamageVsType >= 0 && closestEnemy.UnitType == unit.BonusDamageVsType)
                        damage += unit.BonusDamageAmount;
                    if (unit.BonusDamageVsType2 >= 0 && closestEnemy.UnitType == unit.BonusDamageVsType2)
                        damage += unit.BonusDamageAmount2;

                    unit.AttackCooldownRemaining = unit.AttackCooldownTicks;

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

                        if (closestEnemy.CurrentHealth <= 0)
                        {
                            closestEnemy.State = UnitState.Dead;
                            deadList.Add(closestEnemy.Id);
                        }
                    }
                }
                else if (unit.AttackCooldownRemaining <= 0)
                {
                    // Chase immediately — turns while running
                    Fixed32 step = unit.MoveSpeed * tickDuration;
                    if (unit.IsCharging)
                        step = step * ChargeSpeedMultiplier;
                    FixedVector3 newPos = unit.SimPosition + targetDir * step;
                    Vector2Int newTile = mapData.WorldToTile(newPos);
                    if (mapData.IsWalkable(newTile.x, newTile.y))
                        unit.SimPosition = newPos;
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
                            unit.SimPosition = newPos;
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

                    int damage = unit.AttackDamage - building.Armor;
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
                            unit.SimPosition = newPos;
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
