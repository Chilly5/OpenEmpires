namespace OpenEmpires
{
    public class ProjectileData
    {
        public int Id;
        public int SourceUnitId;
        public int SourceBuildingId = -1;
        public int TargetUnitId;
        public int TargetBuildingId = -1;
        public FixedVector3 Position;
        public FixedVector3 PreviousPosition;
        public int Damage;
        public Fixed32 Speed;
        public bool IsActive;
        public bool IsBolt; // flat trajectory (crossbow bolts)

        public ProjectileData(int id, int sourceUnitId, int targetUnitId, FixedVector3 position, int damage, Fixed32 speed)
        {
            Id = id;
            SourceUnitId = sourceUnitId;
            TargetUnitId = targetUnitId;
            TargetBuildingId = -1;
            Position = position;
            PreviousPosition = position;
            Damage = damage;
            Speed = speed;
            IsActive = true;
        }
    }
}
