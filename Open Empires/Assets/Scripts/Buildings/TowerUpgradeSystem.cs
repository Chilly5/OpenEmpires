using System.Collections.Generic;

namespace OpenEmpires
{
    public class TowerUpgradeSystem
    {
        public void Tick(BuildingRegistry buildingRegistry, SimulationConfig config, Fixed32 cachedCannonRange = default, Fixed32 cachedVisionUpgradeRangeBonus = default)
        {
            var allBuildings = buildingRegistry.GetAllBuildings();

            for (int i = 0; i < allBuildings.Count; i++)
            {
                var building = allBuildings[i];
                if (building.Type != BuildingType.Tower || !building.IsUpgrading)
                    continue;

                building.UpgradeTicksRemaining--;

                // Check if upgrade is complete
                if (building.UpgradeTicksRemaining <= 0)
                {
                    CompleteUpgrade(building, config, cachedCannonRange, cachedVisionUpgradeRangeBonus);

                    // Start next upgrade in queue
                    StartNextUpgrade(building, config);
                }
            }
        }

        private void CompleteUpgrade(BuildingData building, SimulationConfig config, Fixed32 cachedCannonRange, Fixed32 cachedVisionUpgradeRangeBonus)
        {
            // Apply upgrade effects based on upgrade type
            switch (building.CurrentUpgrade)
            {
                case TowerUpgradeType.ArrowSlits:
                    building.HasArrowSlits = true;
                    building.BaseArrowCount += config.ArrowSlitsExtraArrows;
                    break;
                case TowerUpgradeType.CannonEmplacement:
                    building.HasCannonEmplacement = true;
                    building.AttackDamage = config.CannonDamage;
                    building.AttackRange = cachedCannonRange;
                    break;
                case TowerUpgradeType.StoneUpgrade:
                    building.HasStoneUpgrade = true;
                    building.MaxHealth += config.StoneUpgradeHealthBonus;
                    building.CurrentHealth += config.StoneUpgradeHealthBonus;
                    building.Armor += config.StoneUpgradeArmorBonus;
                    break;
                case TowerUpgradeType.VisionUpgrade:
                    building.HasVisionUpgrade = true;
                    building.DetectionRange += cachedVisionUpgradeRangeBonus;
                    break;
            }

            // Remove completed upgrade from queue
            building.DequeueUpgrade();
        }

        private void StartNextUpgrade(BuildingData building, SimulationConfig config)
        {
            if (building.UpgradeQueue.Count > 0)
            {
                // Start next upgrade in queue
                building.IsUpgrading = true;
                building.CurrentUpgrade = building.UpgradeQueue[0];
                building.UpgradeTicksRemaining = config.TowerUpgradeTicks;
                building.UpgradeTicksTotal = config.TowerUpgradeTicks;
            }
            else
            {
                // No more upgrades in queue
                building.IsUpgrading = false;
                building.UpgradeTicksRemaining = 0;
                building.UpgradeTicksTotal = 0;
            }
        }
    }
}