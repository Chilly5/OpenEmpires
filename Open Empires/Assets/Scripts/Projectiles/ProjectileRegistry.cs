using System.Collections.Generic;

namespace OpenEmpires
{
    public class ProjectileRegistry
    {
        private List<ProjectileData> projectiles = new List<ProjectileData>();
        private List<ProjectileData> newlyCreated = new List<ProjectileData>();
        private List<ProjectileData> flushBuffer = new List<ProjectileData>();
        private int nextId;

        public ProjectileData CreateProjectile(int sourceUnitId, int targetUnitId, FixedVector3 position, int damage, Fixed32 speed, bool isBolt = false)
        {
            var proj = new ProjectileData(nextId++, sourceUnitId, targetUnitId, position, damage, speed);
            proj.IsBolt = isBolt;
            projectiles.Add(proj);
            newlyCreated.Add(proj);
            return proj;
        }

        public ProjectileData CreateBuildingProjectile(int sourceUnitId, int targetBuildingId, FixedVector3 position, int damage, Fixed32 speed, bool isBolt = false)
        {
            var proj = new ProjectileData(nextId++, sourceUnitId, -1, position, damage, speed);
            proj.TargetBuildingId = targetBuildingId;
            proj.IsBolt = isBolt;
            projectiles.Add(proj);
            newlyCreated.Add(proj);
            return proj;
        }

        public ProjectileData CreateBuildingSourceProjectile(int sourceBuildingId, int targetUnitId, FixedVector3 position, int damage, Fixed32 speed)
        {
            var proj = new ProjectileData(nextId++, -1, targetUnitId, position, damage, speed);
            proj.SourceBuildingId = sourceBuildingId;
            projectiles.Add(proj);
            newlyCreated.Add(proj);
            return proj;
        }

        public List<ProjectileData> GetAllProjectiles() => projectiles;

        public List<ProjectileData> FlushNewlyCreated()
        {
            var result = newlyCreated;
            newlyCreated = flushBuffer;
            newlyCreated.Clear();
            flushBuffer = result;
            return result; // caller must consume before next Flush
        }

        public void RemoveProjectile(int id)
        {
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                if (projectiles[i].Id == id)
                {
                    projectiles.RemoveAt(i);
                    return;
                }
            }
        }
    }
}
