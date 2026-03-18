using System.Collections.Generic;

namespace OpenEmpires
{
    public class ProjectileSystem
    {
        private static readonly Fixed32 HitThresholdSq = Fixed32.FromFloat(0.5f) * Fixed32.FromFloat(0.5f);
        private static readonly Fixed32 BuildingHitThresholdSq = Fixed32.FromFloat(1.5f) * Fixed32.FromFloat(1.5f);

        private List<int> hitList = new List<int>();
        private List<int> deadUnitList = new List<int>();
        private List<int> deadBuildingList = new List<int>();

        public (List<int> hitProjectileIds, List<int> deadUnitIds, List<int> deadBuildingIds) Tick(ProjectileRegistry registry, UnitRegistry unitRegistry, BuildingRegistry buildingRegistry, Fixed32 tickDuration, int currentTick, SimulationConfig config = null)
        {
            hitList.Clear();
            deadUnitList.Clear();
            deadBuildingList.Clear();

            var projectiles = registry.GetAllProjectiles();
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                var proj = projectiles[i];
                if (!proj.IsActive) continue;

                proj.PreviousPosition = proj.Position;

                // Building-target projectile
                if (proj.TargetBuildingId >= 0)
                {
                    var building = buildingRegistry.GetBuilding(proj.TargetBuildingId);
                    if (building == null || building.IsDestroyed)
                    {
                        proj.IsActive = false;
                        hitList.Add(proj.Id);
                        continue;
                    }

                    FixedVector3 toTarget = building.SimPosition - proj.Position;
                    toTarget.y = Fixed32.Zero;
                    Fixed32 distSq = toTarget.x * toTarget.x + toTarget.z * toTarget.z;

                    if (distSq <= BuildingHitThresholdSq)
                    {
                        building.CurrentHealth -= proj.Damage;
                        building.LastDamageTick = currentTick;
                        var source = unitRegistry.GetUnit(proj.SourceUnitId);
                        building.LastDamageFromPos = source != null ? source.SimPosition : proj.Position;

                        if (building.IsDestroyed)
                            deadBuildingList.Add(building.Id);

                        proj.IsActive = false;
                        hitList.Add(proj.Id);
                        continue;
                    }

                    Fixed32 dist = Fixed32.Sqrt(distSq);
                    Fixed32 step = proj.Speed * tickDuration;
                    if (step >= dist)
                        proj.Position = building.SimPosition;
                    else
                    {
                        FixedVector3 dir = toTarget / dist;
                        proj.Position = proj.Position + dir * step;
                    }
                    continue;
                }

                // Unit-target projectile
                var target = unitRegistry.GetUnit(proj.TargetUnitId);
                if (target == null || target.State == UnitState.Dead)
                {
                    proj.IsActive = false;
                    hitList.Add(proj.Id);
                    continue;
                }

                // Move toward target (homing)
                FixedVector3 toUnitTarget = target.SimPosition - proj.Position;
                toUnitTarget.y = Fixed32.Zero;

                // Overflow guard: if axis distance > 180 tiles, skip distSq and just move
                Fixed32 overflowThreshold = Fixed32.FromInt(180);
                Fixed32 absDx = Fixed32.Abs(toUnitTarget.x);
                Fixed32 absDz = Fixed32.Abs(toUnitTarget.z);
                if (absDx > overflowThreshold || absDz > overflowThreshold)
                {
                    Fixed32 approxDist = absDx > absDz ? absDx : absDz;
                    FixedVector3 farDir = toUnitTarget / approxDist;
                    Fixed32 farStep = proj.Speed * tickDuration;
                    proj.Position = proj.Position + farDir * farStep;
                    continue;
                }

                Fixed32 unitDistSq = toUnitTarget.x * toUnitTarget.x + toUnitTarget.z * toUnitTarget.z;

                // Check hit
                if (unitDistSq <= HitThresholdSq)
                {
                    int damage = proj.Damage - target.RangedArmor;
                    if (damage < 1) damage = 1;
                    target.CurrentHealth -= damage;

                    // Combat feedback on target
                    target.LastDamageTick = currentTick;
                    var source = unitRegistry.GetUnit(proj.SourceUnitId);
                    if (source != null)
                        target.LastDamageFromPos = source.SimPosition;
                    else if (proj.SourceBuildingId >= 0)
                    {
                        var srcBuilding = buildingRegistry.GetBuilding(proj.SourceBuildingId);
                        target.LastDamageFromPos = srcBuilding != null ? srcBuilding.SimPosition : proj.Position;
                    }
                    else
                        target.LastDamageFromPos = proj.Position;

                    if (target.CurrentHealth <= 0)
                    {
                        target.State = UnitState.Dead;
                        deadUnitList.Add(target.Id);
                    }

                    proj.IsActive = false;
                    hitList.Add(proj.Id);
                    continue;
                }

                // Move
                Fixed32 unitDist = Fixed32.Sqrt(unitDistSq);
                Fixed32 unitStep = proj.Speed * tickDuration;
                if (unitStep >= unitDist)
                {
                    proj.Position = target.SimPosition;
                }
                else
                {
                    FixedVector3 dir = toUnitTarget / unitDist;
                    proj.Position = proj.Position + dir * unitStep;
                }
            }

            // Clean up inactive projectiles
            for (int i = hitList.Count - 1; i >= 0; i--)
                registry.RemoveProjectile(hitList[i]);

            return (hitList, deadUnitList, deadBuildingList);
        }
    }
}
