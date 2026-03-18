using System.Collections.Generic;

namespace OpenEmpires
{
    public class TowerCombatSystem
    {
        private static readonly Fixed32 ArrowOffset = new Fixed32(19660); // 0.3f * 65536
        private List<int> deadUnitList = new List<int>();

        public List<int> Tick(BuildingRegistry buildingRegistry, UnitRegistry unitRegistry, ProjectileRegistry projectileRegistry,
            SimulationConfig config, int currentTick, int[] playerTeamIds = null, Fixed32 projectileSpeed = default, SpatialGrid spatialGrid = null)
        {
            deadUnitList.Clear();
            var allBuildings = buildingRegistry.GetAllBuildings();

            for (int i = 0; i < allBuildings.Count; i++)
            {
                var building = allBuildings[i];
                if (building.Type != BuildingType.Tower || building.IsDestroyed || building.IsUnderConstruction)
                    continue;

                // Handle tower attack cooldown
                if (building.AttackCooldownRemaining > 0)
                    building.AttackCooldownRemaining--;

                // Find closest enemy unit in range
                UnitData closestEnemy = null;
                Fixed32 closestDistSq = building.DetectionRange * building.DetectionRange;

                // Check current target first
                if (building.TowerTargetUnitId >= 0)
                {
                    var currentTarget = unitRegistry.GetUnit(building.TowerTargetUnitId);
                    if (currentTarget != null && currentTarget.State != UnitState.Dead && !currentTarget.IsSheep)
                    {
                        // Check if still in range and not allied
                        if (!TeamHelper.AreAllies(playerTeamIds, currentTarget.PlayerId, building.PlayerId))
                        {
                            Fixed32 dx = currentTarget.SimPosition.x - building.SimPosition.x;
                            Fixed32 dz = currentTarget.SimPosition.z - building.SimPosition.z;
                            Fixed32 distSq = dx * dx + dz * dz;
                            
                            if (distSq <= closestDistSq)
                            {
                                closestEnemy = currentTarget;
                                closestDistSq = distSq;
                            }
                        }
                    }
                    
                    if (closestEnemy == null)
                        building.TowerTargetUnitId = -1;
                }

                // If no current target, find new one using spatial grid
                if (closestEnemy == null)
                {
                    var nearbyUnits = spatialGrid != null
                        ? spatialGrid.GetNearby(building.SimPosition, building.DetectionRange)
                        : unitRegistry.GetAllUnits();

                    for (int j = 0; j < nearbyUnits.Count; j++)
                    {
                        var unit = nearbyUnits[j];
                        if (unit.State == UnitState.Dead) continue;
                        if (unit.IsSheep) continue;
                        if (TeamHelper.AreAllies(playerTeamIds, unit.PlayerId, building.PlayerId)) continue;

                        Fixed32 dx = unit.SimPosition.x - building.SimPosition.x;
                        if (Fixed32.Abs(dx) > building.DetectionRange) continue;
                        Fixed32 dz = unit.SimPosition.z - building.SimPosition.z;
                        if (Fixed32.Abs(dz) > building.DetectionRange) continue;

                        Fixed32 distSq = dx * dx + dz * dz;
                        if (distSq < closestDistSq || (distSq == closestDistSq && closestEnemy != null && unit.Id < closestEnemy.Id))
                        {
                            closestDistSq = distSq;
                            closestEnemy = unit;
                        }
                    }

                    if (closestEnemy != null)
                        building.TowerTargetUnitId = closestEnemy.Id;
                }

                // Attack if target in range and cooldown ready
                if (closestEnemy != null && building.AttackCooldownRemaining <= 0)
                {
                    Fixed32 attackRangeSq = building.AttackRange * building.AttackRange;
                    if (closestDistSq <= attackRangeSq)
                    {
                        // Reset cooldown based on upgrade
                        if (building.HasCannonEmplacement)
                            building.AttackCooldownRemaining = config.CannonCooldownTicks;
                        else
                            building.AttackCooldownRemaining = building.AttackCooldownTicks;

                        // Combat feedback
                        building.LastAttackTick = currentTick;
                        building.LastAttackTargetPos = closestEnemy.SimPosition;

                        // Determine damage based on upgrades
                        int damage = building.HasCannonEmplacement ? config.CannonDamage : building.AttackDamage;

                        // Fire multiple projectiles for arrow slits
                        int arrowCount = building.ArrowCount;
                        if (building.HasArrowSlits)
                            arrowCount += config.ArrowSlitsExtraArrows;

                        for (int arrow = 0; arrow < arrowCount; arrow++)
                        {
                            // Slightly offset arrow positions for visual variety
                            FixedVector3 firePosition = building.SimPosition;
                            if (arrow > 0)
                            {
                                Fixed32 offset = ArrowOffset * Fixed32.FromInt(arrow - 1);
                                firePosition.x += offset * (arrow % 2 == 0 ? Fixed32.One : -Fixed32.One);
                            }

                            projectileRegistry.CreateProjectile(building.Id, closestEnemy.Id,
                                firePosition, damage, projectileSpeed);
                        }
                    }
                }
                else if (closestEnemy == null)
                {
                    building.TowerTargetUnitId = -1;
                }
            }

            return deadUnitList;
        }
    }
}