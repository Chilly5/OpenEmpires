namespace OpenEmpires
{
    public enum TowerUpgradeType
    {
        ArrowSlits,
        CannonEmplacement,
        StoneUpgrade,
        VisionUpgrade
    }

    public class UpgradeTowerCommand : ICommand
    {
        public CommandType Type => CommandType.UpgradeTower;
        public int PlayerId { get; set; }
        public int BuildingId;
        public TowerUpgradeType UpgradeType;

        public UpgradeTowerCommand(int playerId, int buildingId, TowerUpgradeType upgradeType)
        {
            PlayerId = playerId;
            BuildingId = buildingId;
            UpgradeType = upgradeType;
        }

        public bool Execute(GameSimulation simulation)
        {
            var building = simulation.BuildingRegistry.GetBuilding(BuildingId);
            if (building == null || building.Type != BuildingType.Tower || building.IsDestroyed)
                return false;

            // Can't upgrade if under construction
            if (building.IsUnderConstruction)
                return false;

            var config = simulation.Config;
            var resources = simulation.ResourceManager.GetPlayerResources(building.PlayerId);

            // Check if upgrade is already applied
            switch (UpgradeType)
            {
                case TowerUpgradeType.ArrowSlits:
                    if (building.HasArrowSlits) return false;
                    if (resources.Wood < config.ArrowSlitsWoodCost) return false;
                    break;
                case TowerUpgradeType.CannonEmplacement:
                    if (building.HasCannonEmplacement) return false;
                    if (resources.Wood < config.CannonEmplacementWoodCost) return false;
                    break;
                case TowerUpgradeType.StoneUpgrade:
                    if (building.HasStoneUpgrade) return false;
                    if (resources.Wood < config.StoneUpgradeWoodCost) return false;
                    break;
                case TowerUpgradeType.VisionUpgrade:
                    if (building.HasVisionUpgrade) return false;
                    if (resources.Wood < config.VisionUpgradeWoodCost) return false;
                    break;
            }

            // Enqueue upgrade and deduct cost
            building.EnqueueUpgrade(UpgradeType, config.TowerUpgradeTicks);

            // Deduct resources
            switch (UpgradeType)
            {
                case TowerUpgradeType.ArrowSlits:
                    resources.Wood -= config.ArrowSlitsWoodCost;
                    break;
                case TowerUpgradeType.CannonEmplacement:
                    resources.Wood -= config.CannonEmplacementWoodCost;
                    break;
                case TowerUpgradeType.StoneUpgrade:
                    resources.Wood -= config.StoneUpgradeWoodCost;
                    break;
                case TowerUpgradeType.VisionUpgrade:
                    resources.Wood -= config.VisionUpgradeWoodCost;
                    break;
            }

            return true;
        }
    }
}