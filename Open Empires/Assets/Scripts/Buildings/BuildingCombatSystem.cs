using System.Collections.Generic;

namespace OpenEmpires
{
    public class BuildingCombatSystem
    {
        public void Tick(BuildingRegistry buildingRegistry, UnitRegistry unitRegistry,
            ProjectileRegistry projectileRegistry, SimulationConfig config,
            int currentTick, Fixed32 tickDuration, int[] playerTeamIds, Fixed32 projectileSpeed = default, SpatialGrid spatialGrid = null)
        {
            var buildings = buildingRegistry.GetAllBuildings();

            for (int b = 0; b < buildings.Count; b++)
            {
                var building = buildings[b];
                if (building.IsDestroyed || building.IsUnderConstruction) continue;
                if (building.AttackDamage <= 0) continue;

                // Cooldown
                if (building.AttackCooldownRemaining > 0)
                {
                    building.AttackCooldownRemaining--;
                    continue;
                }

                int arrowCount = building.ArrowCount;
                if (arrowCount <= 0) continue;

                Fixed32 detectionSq = building.DetectionRange * building.DetectionRange;

                // Use spatial grid for nearby units instead of scanning all
                var nearbyUnits = spatialGrid != null
                    ? spatialGrid.GetNearby(building.SimPosition, building.DetectionRange)
                    : unitRegistry.GetAllUnits();

                // Find enemies in range, sorted by distance
                // We need up to arrowCount targets (spread arrows across different targets if possible)
                var targets = FindEnemiesInRange(building, nearbyUnits, detectionSq, arrowCount, playerTeamIds);
                if (targets.Count == 0) continue;

                // Fire arrows at targets (distribute arrows round-robin)
                FixedVector3 launchPos = new FixedVector3(
                    building.SimPosition.x, Fixed32.FromInt(3), building.SimPosition.z);

                for (int a = 0; a < arrowCount; a++)
                {
                    var target = targets[a % targets.Count];
                    projectileRegistry.CreateBuildingSourceProjectile(
                        building.Id, target.Id, launchPos,
                        building.AttackDamage, projectileSpeed);
                }

                building.AttackCooldownRemaining = building.AttackCooldownTicks;
                building.CombatTargetUnitId = targets[0].Id;
            }
        }

        private List<UnitData> targetBuffer = new List<UnitData>();

        private List<UnitData> FindEnemiesInRange(BuildingData building, List<UnitData> allUnits,
            Fixed32 detectionSq, int maxTargets, int[] playerTeamIds)
        {
            targetBuffer.Clear();

            for (int i = 0; i < allUnits.Count; i++)
            {
                var unit = allUnits[i];
                if (unit.State == UnitState.Dead) continue;
                if (unit.IsSheep) continue;
                if (TeamHelper.AreAllies(playerTeamIds, unit.PlayerId, building.PlayerId)) continue;

                Fixed32 dx = unit.SimPosition.x - building.SimPosition.x;
                Fixed32 dz = unit.SimPosition.z - building.SimPosition.z;

                // Early out with axis check
                if (Fixed32.Abs(dx) > building.DetectionRange || Fixed32.Abs(dz) > building.DetectionRange)
                    continue;

                Fixed32 distSq = dx * dx + dz * dz;
                if (distSq > detectionSq) continue;

                // Insert sorted by distance (simple insertion for small N)
                bool inserted = false;
                for (int j = 0; j < targetBuffer.Count; j++)
                {
                    Fixed32 existDx = targetBuffer[j].SimPosition.x - building.SimPosition.x;
                    Fixed32 existDz = targetBuffer[j].SimPosition.z - building.SimPosition.z;
                    Fixed32 existDistSq = existDx * existDx + existDz * existDz;
                    if (distSq < existDistSq || (distSq == existDistSq && unit.Id < targetBuffer[j].Id))
                    {
                        targetBuffer.Insert(j, unit);
                        inserted = true;
                        break;
                    }
                }
                if (!inserted)
                    targetBuffer.Add(unit);

                // Keep only maxTargets
                if (targetBuffer.Count > maxTargets)
                    targetBuffer.RemoveAt(targetBuffer.Count - 1);
            }

            return targetBuffer;
        }
    }
}
