using UnityEngine;

namespace OpenEmpires
{
    [CreateAssetMenu(fileName = "SimulationConfig", menuName = "OpenEmpires/Simulation Config")]
    public class SimulationConfig : ScriptableObject
    {
        // General
        public int TickRate => 30;
        public static int GetMapSize(int playerCount)
        {
            return playerCount switch
            {
                <= 2 => 256,
                <= 4 => 384,
                <= 6 => 448,
                _    => 512,
            };
        }
        public int StartingVillagers => 3;
        public float UnitMoveSpeed => 2f;
        public float GatherRate => 0.5f;
        public int GatherAmountPerTick => 1;
        public float UnitRadius => 0.4f;
        public float CavalryRadius => 0.55f;
        public float VillagerMass => 1f;
        public float SpearmanMass => 3f;
        public float FormationScatter => 0.8f;
        public float SeparationStrength => 0.5f;
        public int VillagerCarryCapacity => 10;

        // Combat - Villager
        public int VillagerMaxHealth => 50;
        public int VillagerAttackDamage => 1;
        public float VillagerAttackRange => 1.0f;
        public float VillagerDetectionRange => 10f;
        public int VillagerAttackCooldownTicks => 90;
        public int VillagerMeleeArmor => 0;
        public int VillagerRangedArmor => 0;

        // Combat - Spearman
        public int SpearmanMaxHealth => 80;
        public int SpearmanAttackDamage => 8;
        public float SpearmanAttackRange => 1.0f;
        public float SpearmanDetectionRange => 8f;
        public int SpearmanAttackCooldownTicks => 30;
        public int SpearmanMeleeArmor => 0;
        public int SpearmanRangedArmor => 0;
        public int SpearmanBonusDamageVsType => 3; // vs Horseman
        public int SpearmanBonusDamageAmount => 12;

        // Population
        public int HousePopulation => 10;
        public int TownCenterPopulation => 10;
        public int MaxPopulation => 200;

        // Buildings - House
        public int HouseMaxHealth => 250;
        public int HouseArmor => 2;
        public int HouseFootprintWidth => 2;
        public int HouseFootprintHeight => 2;

        // Buildings - Barracks
        public int BarracksMaxHealth => 500;
        public int BarracksArmor => 3;
        public int BarracksFootprintWidth => 3;
        public int BarracksFootprintHeight => 3;

        // Buildings - Town Center
        public int TownCenterMaxHealth => 1000;
        public int TownCenterArmor => 4;
        public int TownCenterFootprintWidth => 4;
        public int TownCenterFootprintHeight => 4;

        // Town Center combat (arrow slits)
        public int TownCenterArrowDamage => 8;
        public float MainTownCenterAttackRange => 20f; // Main/starting town centers
        public float SubsequentTownCenterAttackRange => 16f; // Player-built town centers
        public float TownCenterDetectionRange => 10f;
        public int TownCenterAttackCooldownTicks => 60; // 2s at 30 TPS
        public int TownCenterGarrisonCapacity => 10;

        // Combat - Archer
        public int ArcherMaxHealth => 70;
        public int ArcherAttackDamage => 5;
        public float ArcherAttackRange => 12f;
        public float ArcherDetectionRange => 14f;
        public int ArcherAttackCooldownTicks => 45;
        public int ArcherMeleeArmor => 0;
        public int ArcherRangedArmor => 0;
        public float ArcherMass => 2f;
        public float ArcherMoveSpeed => 2f;
        public int ArcherBonusDamageVsType => 1; // vs Spearman
        public int ArcherBonusDamageAmount => 5;

        // Combat - Horseman
        public int HorsemanMaxHealth => 125;
        public int HorsemanAttackDamage => 9;
        public float HorsemanAttackRange => 1.0f;
        public float HorsemanDetectionRange => 8f;
        public int HorsemanAttackCooldownTicks => 25;
        public int HorsemanMeleeArmor => 0;
        public int HorsemanRangedArmor => 2;
        public float HorsemanMass => 4f;
        public float HorsemanMoveSpeed => 4f;
        public int HorsemanBonusDamageVsType => 2; // vs Archer
        public int HorsemanBonusDamageAmount => 9;

        // Combat - Scout
        public int ScoutMaxHealth => 50;
        public int ScoutAttackDamage => 1;
        public float ScoutAttackRange => 1.0f;
        public float ScoutDetectionRange => 20f;
        public int ScoutAttackCooldownTicks => 90;
        public int ScoutMeleeArmor => 0;
        public int ScoutRangedArmor => 0;
        public float ScoutMass => 3f;
        public float ScoutMoveSpeed => 4f;

        // Combat - Man-at-Arms
        public int ManAtArmsMaxHealth => 120;
        public int ManAtArmsAttackDamage => 8;
        public float ManAtArmsAttackRange => 1.0f;
        public float ManAtArmsDetectionRange => 8f;
        public int ManAtArmsAttackCooldownTicks => 30;
        public int ManAtArmsMeleeArmor => 3;
        public int ManAtArmsRangedArmor => 3;
        public float ManAtArmsMass => 4f;
        public float ManAtArmsMoveSpeed => 1.8f;

        // Combat - Knight
        public int KnightMaxHealth => 180;
        public int KnightAttackDamage => 24;
        public float KnightAttackRange => 1.0f;
        public float KnightDetectionRange => 8f;
        public int KnightAttackCooldownTicks => 30;
        public int KnightMeleeArmor => 3;
        public int KnightRangedArmor => 3;
        public float KnightMass => 5f;
        public float KnightMoveSpeed => 3.5f;
        public int KnightBonusDamageVsType => -1;
        public int KnightBonusDamageAmount => 0;

        // Combat - Crossbowman
        public int CrossbowmanMaxHealth => 75;
        public int CrossbowmanAttackDamage => 6;
        public float CrossbowmanAttackRange => 10f;
        public float CrossbowmanDetectionRange => 12f;
        public int CrossbowmanAttackCooldownTicks => 50;
        public int CrossbowmanMeleeArmor => 0;
        public int CrossbowmanRangedArmor => 0;
        public float CrossbowmanMass => 2f;
        public float CrossbowmanMoveSpeed => 1.8f;
        public int CrossbowmanBonusDamageVsType => 6; // vs Man-at-Arms
        public int CrossbowmanBonusDamageAmount => 9;
        public int CrossbowmanBonusDamageVsType2 => 7; // vs Knight
        public int CrossbowmanBonusDamageAmount2 => 9;

        // Combat - Monk
        public int MonkMaxHealth => 60;
        public int MonkAttackDamage => 0;
        public float MonkAttackRange => 1.0f;
        public float MonkDetectionRange => 8f;
        public int MonkAttackCooldownTicks => 30;
        public int MonkMeleeArmor => 1;
        public int MonkRangedArmor => 1;
        public float MonkMass => 2f;
        public float MonkMoveSpeed => 2f;
        public int MonkHealAmount => 2;
        public float MonkHealRange => 4f;
        public int MonkHealCooldownTicks => 30; // 1s at 30 TPS

        // Combat - Longbowman (English unique — Archer with greater range)
        public int LongbowmanMaxHealth => 70;
        public int LongbowmanAttackDamage => 5;
        public float LongbowmanAttackRange => 16f;
        public float LongbowmanDetectionRange => 18f;
        public int LongbowmanAttackCooldownTicks => 45;
        public int LongbowmanMeleeArmor => 0;
        public int LongbowmanRangedArmor => 0;
        public float LongbowmanMass => 2f;
        public float LongbowmanMoveSpeed => 2f;
        public int LongbowmanBonusDamageVsType => 1;
        public int LongbowmanBonusDamageAmount => 5;

        // Combat - Gendarme (French unique — Horseman with greater health)
        public int GendarmeMaxHealth => 175;
        public int GendarmeAttackDamage => 9;
        public float GendarmeAttackRange => 1.0f;
        public float GendarmeDetectionRange => 8f;
        public int GendarmeAttackCooldownTicks => 25;
        public int GendarmeMeleeArmor => 0;
        public int GendarmeRangedArmor => 2;
        public float GendarmeMass => 4f;
        public float GendarmeMoveSpeed => 4f;
        public int GendarmeBonusDamageVsType => 2;
        public int GendarmeBonusDamageAmount => 9;

        // Combat - Landsknecht (HRE unique — Spearman with greater speed)
        public int LandsknechtMaxHealth => 80;
        public int LandsknechtAttackDamage => 8;
        public float LandsknechtAttackRange => 1.0f;
        public float LandsknechtDetectionRange => 8f;
        public int LandsknechtAttackCooldownTicks => 30;
        public int LandsknechtMeleeArmor => 0;
        public int LandsknechtRangedArmor => 0;
        public float LandsknechtMass => 3f;
        public float LandsknechtMoveSpeed => 3f;
        public int LandsknechtBonusDamageVsType => 3;
        public int LandsknechtBonusDamageAmount => 20;

        // Sheep
        public int SheepMaxHealth => 25;
        public float SheepMoveSpeed => 0.5f;
        public float SheepMass => 1f;
        public float SheepRadius => 0.35f;
        public float SheepConversionRange => 11f;
        public int SheepPerPlayer => 10;
        public int SheepSlaughterFood => 150;

        // Training
        public int SpearmanTrainTimeTicks => 300; // 15s at 20 TPS
        public int SpearmanFoodCost => 60;
        public int SpearmanWoodCost => 20;
        public int ArcherTrainTimeTicks => 300; // 15s at 20 TPS
        public int ArcherFoodCost => 30;
        public int ArcherWoodCost => 50;
        public int HorsemanTrainTimeTicks => 450; // 22.5s at 20 TPS
        public int HorsemanFoodCost => 100;
        public int HorsemanWoodCost => 20;
        public int ScoutTrainTimeTicks => 200; // 10s at 20 TPS
        public int ScoutFoodCost => 80;
        public int ScoutWoodCost => 0;
        public int LongbowmanTrainTimeTicks => 300;
        public int LongbowmanFoodCost => 30;
        public int LongbowmanWoodCost => 50;
        public int GendarmeTrainTimeTicks => 450;
        public int GendarmeFoodCost => 100;
        public int GendarmeWoodCost => 20;
        public int LandsknechtTrainTimeTicks => 300;
        public int LandsknechtFoodCost => 60;
        public int LandsknechtWoodCost => 20;
        public int VillagerTrainTimeTicks => 400; // 20s at 20 TPS
        public int VillagerFoodCost => 50;

        public int ManAtArmsTrainTimeTicks => 400;
        public int ManAtArmsFoodCost => 100;
        public int ManAtArmsGoldCost => 20;
        public int KnightTrainTimeTicks => 600;
        public int KnightFoodCost => 140;
        public int KnightGoldCost => 100;
        public int CrossbowmanTrainTimeTicks => 400;
        public int CrossbowmanFoodCost => 80;
        public int CrossbowmanGoldCost => 40;
        public int MonkTrainTimeTicks => 500;
        public int MonkFoodCost => 0;
        public int MonkGoldCost => 100;

        // Combat - Battering Ram (siege, anti-building melee)
        public int BatteringRamMaxHealth => 200;
        public int BatteringRamAttackDamage => 5;
        public float BatteringRamAttackRange => 1.0f;
        public float BatteringRamDetectionRange => 6f;
        public int BatteringRamAttackCooldownTicks => 60;
        public int BatteringRamMeleeArmor => 3;
        public int BatteringRamRangedArmor => 0;
        public float BatteringRamMass => 8f;
        public float BatteringRamMoveSpeed => 0.6f;
        public int BatteringRamBonusDamageVsBuildings => 80;
        public int BatteringRamTrainTimeTicks => 600;
        public int BatteringRamWoodCost => 200;
        public int BatteringRamGoldCost => 100;

        // Combat - Mangonel (siege, ranged AoE)
        public int MangonelMaxHealth => 80;
        public int MangonelAttackDamage => 30;
        public float MangonelAttackRange => 14f;
        public float MangonelDetectionRange => 16f;
        public int MangonelAttackCooldownTicks => 90;
        public int MangonelMeleeArmor => 0;
        public int MangonelRangedArmor => 0;
        public float MangonelMass => 6f;
        public float MangonelMoveSpeed => 0.5f;
        public int MangonelBonusDamageVsBuildings => 40;
        public int MangonelTrainTimeTicks => 750;
        public int MangonelWoodCost => 250;
        public int MangonelGoldCost => 150;

        // Combat - Trebuchet (siege, long-range anti-building)
        public int TrebuchetMaxHealth => 60;
        public int TrebuchetAttackDamage => 15;
        public float TrebuchetAttackRange => 20f;
        public float TrebuchetDetectionRange => 22f;
        public int TrebuchetAttackCooldownTicks => 120;
        public int TrebuchetMeleeArmor => 0;
        public int TrebuchetRangedArmor => 0;
        public float TrebuchetMass => 10f;
        public float TrebuchetMoveSpeed => 0.4f;
        public int TrebuchetBonusDamageVsBuildings => 100;
        public int TrebuchetTrainTimeTicks => 900;
        public int TrebuchetWoodCost => 300;
        public int TrebuchetGoldCost => 200;

        // Projectiles
        public float ProjectileSpeed => 12f;

        // Buildings - Wall
        public int WallMaxHealth => 200;
        public int WallArmor => 5;
        public int WallFootprintWidth => 1;
        public int WallFootprintHeight => 1;

        // Buildings - Mill
        public int MillMaxHealth => 200;
        public int MillArmor => 2;
        public int MillFootprintWidth => 2;
        public int MillFootprintHeight => 2;
        public int MillInfluenceRadius => 3; // tiles from footprint edge
        public int MillInfluenceGatherBonusPercent => 20; // 20% faster farm gathering
        public int LandmarkInfluenceRadius => 5; // tiles from footprint edge (larger than mill since landmarks are rarer)
        public int FrenchLandmarkTrainingDiscountPercent => 15; // 15% cheaper unit training near French landmarks

        // Buildings - Lumber Yard
        public int LumberYardMaxHealth => 200;
        public int LumberYardArmor => 2;
        public int LumberYardFootprintWidth => 2;
        public int LumberYardFootprintHeight => 2;

        // Buildings - Mine
        public int MineMaxHealth => 300;
        public int MineArmor => 3;
        public int MineFootprintWidth => 2;
        public int MineFootprintHeight => 2;

        // Buildings - Archery Range
        public int ArcheryRangeMaxHealth => 400;
        public int ArcheryRangeArmor => 2;
        public int ArcheryRangeFootprintWidth => 3;
        public int ArcheryRangeFootprintHeight => 3;

        // Buildings - Stables
        public int StablesMaxHealth => 450;
        public int StablesArmor => 3;
        public int StablesFootprintWidth => 3;
        public int StablesFootprintHeight => 3;

        // Buildings - Farm
        public int FarmMaxHealth => 100;
        public int FarmArmor => 0;
        public int FarmFootprintWidth => 2;
        public int FarmFootprintHeight => 2;

        // Buildings - Monastery
        public int MonasteryMaxHealth => 400;
        public int MonasteryArmor => 2;
        public int MonasteryFootprintWidth => 3;
        public int MonasteryFootprintHeight => 3;

        // Buildings - Tower
        public int TowerMaxHealth => 800;
        public int TowerArmor => 4;
        public int TowerFootprintWidth => 2;
        public int TowerFootprintHeight => 2;
        public float TowerVisionRadius => 25f;

        // Tower Combat
        public int TowerAttackDamage => 15;
        public float TowerAttackRange => 12f;
        public float TowerDetectionRange => 15f;
        public int TowerAttackCooldownTicks => 60; // 2s at 30 TPS

        // Tower Upgrades
        public int ArrowSlitsExtraArrows => 2; // +2 arrows per attack
        public int StoneUpgradeHealthBonus => 400; // +400 health
        public int StoneUpgradeArmorBonus => 2; // +2 armor
        public float VisionUpgradeRangeBonus => 10f; // +10 vision range
        
        // Cannon Emplacement
        public int CannonDamage => 35;
        public float CannonRange => 15f;
        public int CannonCooldownTicks => 120; // 4s at 30 TPS - slower but more damage

        // Upgrade Costs
        public int ArrowSlitsWoodCost => 150;
        public int CannonEmplacementWoodCost => 200;
        public int StoneUpgradeWoodCost => 250;
        public int VisionUpgradeWoodCost => 100;

        // Upgrade Build Times (all 5 seconds at 30 TPS)
        public int TowerUpgradeTicks => 150;

        // Building costs (wood)
        public int FarmWoodCost => 75;
        public int HouseWoodCost => 50;
        public int BarracksWoodCost => 150;
        public int TownCenterWoodCost => 400;
        public int TownCenterStoneCost => 350;
        public int WallWoodCost => 5;
        public int MillWoodCost => 50;
        public int LumberYardWoodCost => 50;
        public int MineWoodCost => 50;
        public int ArcheryRangeWoodCost => 150;
        public int StablesWoodCost => 150;
        public int TowerWoodCost => 300;
        public int MonasteryWoodCost => 200;
        public int BlacksmithWoodCost => 150;
        public int MarketWoodCost => 150;
        public int UniversityWoodCost => 200;
        public int SiegeWorkshopWoodCost => 200;
        public int KeepWoodCost => 300;
        public int KeepStoneCost => 200;
        public int StoneWallStoneCost => 3;
        public int StoneGateStoneCost => 20;
        public int WoodGateWoodCost => 10;
        public int WonderFoodCost => 1000;
        public int WonderWoodCost => 1000;
        public int WonderGoldCost => 1000;
        public int WonderStoneCost => 1000;

        // Building construction time (ticks, 30 ticks = 1s)
        public int HouseConstructionTicks => 450;              // 15s
        public int BarracksConstructionTicks => 900;            // 30s
        public int TownCenterConstructionTicks => 4500;         // 150s
        public int WallConstructionTicks => 150;                // 5s
        public int MillConstructionTicks => 600;                // 20s
        public int LumberYardConstructionTicks => 600;          // 20s
        public int MineConstructionTicks => 600;                // 20s
        public int ArcheryRangeConstructionTicks => 900;        // 30s
        public int StablesConstructionTicks => 900;             // 30s
        public int FarmConstructionTicks => 180;                // 6s
        public int TowerConstructionTicks => 1800;              // 60s
        public int MonasteryConstructionTicks => 900;             // 30s
        public int BlacksmithConstructionTicks => 900;          // 30s
        public int MarketConstructionTicks => 900;              // 30s
        public int UniversityConstructionTicks => 900;          // 30s
        public int SiegeWorkshopConstructionTicks => 900;       // 30s
        public int KeepConstructionTicks => 1200;               // 40s
        public int StoneWallConstructionTicks => 180;           // 6s
        public int StoneGateConstructionTicks => 180;           // 6s
        public int WoodGateConstructionTicks => 150;            // 5s
        public int WonderConstructionTicks => 9000;             // 300s

        // Starting resources
        public int StartingFood => 200;
        public int StartingWood => 200;
        public int StartingGold => 100;
        public int StartingStone => 0;

        // Terrain generation
        [SerializeField] private int mapSeed = 42;
        public int MapSeed { get => mapSeed; set => mapSeed = value; }
        public float TerrainHeightScale => 8f;
        public float WaterThreshold => 0.30f;

        // Meteor Strike
        public int MeteorDamage => 150;
        public float MeteorAllyDamageMultiplier => 0.3f;
        public int MeteorBuildingDamage => 300;
        public float MeteorRadius => 5f;
        public float MeteorKnockbackDist => 8f;
        public int MeteorWarningTicks => 90;  // 3 seconds at 30 TPS
        public int MeteorCooldownTicks => 300; // 10 seconds at 30 TPS

        // Healing Rain
        public int HealingRainHealPerTick => 3;
        public float HealingRainRadius => 6f;
        public int HealingRainDurationTicks => 300;   // 10 seconds at 30 TPS
        public int HealingRainWarningTicks => 30;      // 1 second warning
        public int HealingRainCooldownTicks => 300;    // 10 seconds

        // Lightning Storm
        public float LightningStormRadius => 8f;
        public int LightningStormBoltCount => 8;
        public int LightningStormBoltDamage => 50;
        public float LightningStormBoltRadius => 2f;
        public float LightningStormBoltKnockbackDist => 4f;
        public int LightningStormDurationTicks => 90;  // 3 seconds
        public int LightningStormWarningTicks => 60;   // 2 seconds
        public int LightningStormCooldownTicks => 300;

        // Tsunami
        public float TsunamiWidth => 10f;
        public float TsunamiLength => 15f;
        public float TsunamiPushDist => 15f;
        public int TsunamiDamage => 30;
        public int TsunamiWarningTicks => 60;          // 2 seconds
        public int TsunamiCooldownTicks => 300;

        // Buildings - Blacksmith
        public int BlacksmithMaxHealth => 400;
        public int BlacksmithArmor => 2;
        public int BlacksmithFootprintWidth => 3;
        public int BlacksmithFootprintHeight => 3;

        // Buildings - Market
        public int MarketMaxHealth => 400;
        public int MarketArmor => 2;
        public int MarketFootprintWidth => 3;
        public int MarketFootprintHeight => 3;

        // Research
        public int ResearchTicks_Age2 => 600;       // 20s at 30 TPS
        public int ResearchTicks_Age3 => 900;       // 30s
        public int ResearchTicks_University => 750; // 25s
        // Blacksmith costs (gold)
        public int MeleeAttack1Cost => 100;
        public int MeleeArmor1Cost => 100;
        public int RangedAttack1Cost => 100;
        public int RangedArmor1Cost => 100;
        public int MeleeAttack2Cost => 200;
        public int MeleeArmor2Cost => 200;
        public int RangedAttack2Cost => 200;
        public int RangedArmor2Cost => 200;
        // University costs (food + gold)
        public int BallisticsFoodCost => 200;
        public int BallisticsGoldCost => 200;
        public int SiegeEngineeringFoodCost => 150;
        public int SiegeEngineeringGoldCost => 150;
        public int ChemistryFoodCost => 200;
        public int ChemistryGoldCost => 200;
        public int MurderHolesFoodCost => 100;
        public int MurderHolesGoldCost => 100;
        // Research bonuses
        public int MeleeAttackBonus1 => 1;
        public int MeleeAttackBonus2 => 2;
        public int MeleeArmorBonus1 => 1;
        public int MeleeArmorBonus2 => 2;
        public int RangedAttackBonus1 => 1;
        public int RangedAttackBonus2 => 2;
        public int RangedArmorBonus1 => 1;
        public int RangedArmorBonus2 => 2;
        public int ChemistryRangedBonus => 1;
        public int SiegeEngineeringHPBonus => 40;

        // Market trading
        public int MarketTradeAmount => 100;        // Amount of resource bought/sold per transaction
        public int MarketPriceStep => 5;            // Price change per transaction
        public int MarketStartPrice => 100;         // Starting buy/sell price in gold
        public int MarketMinPrice => 20;            // Minimum sell price
        public int MarketMaxPrice => 300;           // Maximum buy price

        // Buildings - University
        public int UniversityMaxHealth => 400;
        public int UniversityArmor => 2;
        public int UniversityFootprintWidth => 3;
        public int UniversityFootprintHeight => 3;

        // Buildings - Siege Workshop
        public int SiegeWorkshopMaxHealth => 450;
        public int SiegeWorkshopArmor => 3;
        public int SiegeWorkshopFootprintWidth => 3;
        public int SiegeWorkshopFootprintHeight => 3;

        // Buildings - Keep
        public int KeepMaxHealth => 1000;
        public int KeepArmor => 5;
        public int KeepFootprintWidth => 3;
        public int KeepFootprintHeight => 3;

        // Buildings - Stone Wall
        public int StoneWallMaxHealth => 400;
        public int StoneWallArmor => 8;
        public int StoneWallFootprintWidth => 1;
        public int StoneWallFootprintHeight => 1;

        // Buildings - Stone Gate
        public int StoneGateMaxHealth => 500;
        public int StoneGateArmor => 8;
        public int StoneGateFootprintWidth => 1;
        public int StoneGateFootprintHeight => 1;

        // Buildings - Wood Gate
        public int WoodGateMaxHealth => 200;
        public int WoodGateArmor => 5;
        public int WoodGateFootprintWidth => 1;
        public int WoodGateFootprintHeight => 1;

        // Buildings - Wonder
        public int WonderMaxHealth => 5000;
        public int WonderArmor => 10;
        public int WonderFootprintWidth => 5;
        public int WonderFootprintHeight => 5;

        // Buildings - Landmark (defaults; per-landmark values come from LandmarkDefinitions)
        public int LandmarkFootprintWidth => 4;
        public int LandmarkFootprintHeight => 4;

        public float SecondsPerTick => 1f / TickRate;
        public Fixed32 SecondsPerTickFixed => Fixed32.FromFloat(SecondsPerTick);
    }
}
