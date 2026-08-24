namespace OpenEmpires
{
    public enum LandmarkId
    {
        English_Age2_A,
        English_Age2_B,
        English_Age3_A,
        English_Age3_B,
        English_Age4_A,
        English_Age4_B,
        French_Age2_A,
        French_Age2_B,
        French_Age3_A,
        French_Age3_B,
        HRE_Age2_A,
        HRE_Age2_B,
        HRE_Age3_A,
        HRE_Age3_B
    }

    public struct LandmarkDefinition
    {
        public LandmarkId Id;
        public Civilization Civ;
        public int TargetAge;
        public string Name;
        public string Description;
        public int FoodCost;
        public int GoldCost;
        public int ConstructionTicks;
        public int FootprintWidth;
        public int FootprintHeight;
        public int MaxHealth;
        public int Armor;
        public BuildingType EffectiveBuildingType;
        public float ProductionSpeedMultiplier;
        public float VillagerProductionSpeedMultiplier;
        public bool SpawnsKingOnCompletion;
        public bool HasHealingAura;
        public int LongbowDiscountPercent;
        public int GarrisonCapacity;
        public int AttackDamage;
        public float AttackRange;
        public int AttackCooldownTicks;
        public int BaseArrowCount;
    }

    public static class LandmarkDefinitions
    {
        public static LandmarkDefinition Get(LandmarkId id)
        {
            switch (id)
            {
                // English Age 2
                case LandmarkId.English_Age2_A:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.English, TargetAge = 2,
                        Name = "Abbey of Kings", Description = "Heals nearby friendly units and crowns a King.",
                        FoodCost = 400, GoldCost = 200, ConstructionTicks = 3000,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 5000, Armor = 5,
                        EffectiveBuildingType = BuildingType.Landmark,
                        SpawnsKingOnCompletion = true,
                        HasHealingAura = true
                    };
                case LandmarkId.English_Age2_B:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.English, TargetAge = 2,
                        Name = "Council Hall", Description = "Acts as an Archery Range that works faster and discounts Longbowmen.",
                        FoodCost = 400, GoldCost = 200, ConstructionTicks = 3000,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 5000, Armor = 5,
                        EffectiveBuildingType = BuildingType.ArcheryRange,
                        ProductionSpeedMultiplier = 2f,
                        LongbowDiscountPercent = 5
                    };
                // English Age 3
                case LandmarkId.English_Age3_A:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.English, TargetAge = 3,
                        Name = "King's Palace", Description = "Acts as a Town Center and produces Villagers faster.",
                        FoodCost = 800, GoldCost = 400, ConstructionTicks = 4500,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 5000, Armor = 5,
                        EffectiveBuildingType = BuildingType.TownCenter,
                        VillagerProductionSpeedMultiplier = 1.1f
                    };
                case LandmarkId.English_Age3_B:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.English, TargetAge = 3,
                        Name = "The White Tower", Description = "Acts as a Keep and trains military units 75% faster.",
                        FoodCost = 800, GoldCost = 400, ConstructionTicks = 4500,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 5000, Armor = 5,
                        EffectiveBuildingType = BuildingType.Keep,
                        ProductionSpeedMultiplier = 1.75f,
                        GarrisonCapacity = 15,
                        AttackDamage = 12,
                        AttackRange = 8f,
                        AttackCooldownTicks = 60,
                        BaseArrowCount = 3
                    };
                // English Age 4
                case LandmarkId.English_Age4_A:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.English, TargetAge = 4,
                        Name = "Wynguard Palace", Description = "Military landmark for elite English reinforcements.",
                        FoodCost = 1600, GoldCost = 800, ConstructionTicks = 6000,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 5000, Armor = 5,
                        EffectiveBuildingType = BuildingType.Barracks,
                        ProductionSpeedMultiplier = 1f
                    };
                case LandmarkId.English_Age4_B:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.English, TargetAge = 4,
                        Name = "Berkshire Palace", Description = "Acts as a stronger Keep with long-range arrows.",
                        FoodCost = 1600, GoldCost = 800, ConstructionTicks = 6000,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 6500, Armor = 5,
                        EffectiveBuildingType = BuildingType.Keep,
                        GarrisonCapacity = 20,
                        AttackDamage = 14,
                        AttackRange = 15f,
                        AttackCooldownTicks = 60,
                        BaseArrowCount = 6
                    };

                // French Age 2
                case LandmarkId.French_Age2_A:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.French, TargetAge = 2,
                        Name = "Chamber of Commerce", Description = "A hub of trade and diplomacy.",
                        FoodCost = 400, GoldCost = 200, ConstructionTicks = 3000,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 2500, Armor = 5,
                        EffectiveBuildingType = BuildingType.Landmark
                    };
                case LandmarkId.French_Age2_B:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.French, TargetAge = 2,
                        Name = "School of Cavalry", Description = "Trains elite mounted warriors.",
                        FoodCost = 400, GoldCost = 200, ConstructionTicks = 3000,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 2500, Armor = 5,
                        EffectiveBuildingType = BuildingType.Landmark
                    };
                // French Age 3
                case LandmarkId.French_Age3_A:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.French, TargetAge = 3,
                        Name = "Royal Institute", Description = "A place of military innovation.",
                        FoodCost = 800, GoldCost = 400, ConstructionTicks = 4500,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 2500, Armor = 5,
                        EffectiveBuildingType = BuildingType.Landmark
                    };
                case LandmarkId.French_Age3_B:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.French, TargetAge = 3,
                        Name = "Guild Hall", Description = "Provides economic advantages.",
                        FoodCost = 800, GoldCost = 400, ConstructionTicks = 4500,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 2500, Armor = 5,
                        EffectiveBuildingType = BuildingType.Landmark
                    };

                // HRE Age 2
                case LandmarkId.HRE_Age2_A:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.HolyRomanEmpire, TargetAge = 2,
                        Name = "Aachen Chapel", Description = "A sacred imperial chapel.",
                        FoodCost = 400, GoldCost = 200, ConstructionTicks = 3000,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 2500, Armor = 5,
                        EffectiveBuildingType = BuildingType.Landmark
                    };
                case LandmarkId.HRE_Age2_B:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.HolyRomanEmpire, TargetAge = 2,
                        Name = "Meinwerk Palace", Description = "An economic powerhouse.",
                        FoodCost = 400, GoldCost = 200, ConstructionTicks = 3000,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 2500, Armor = 5,
                        EffectiveBuildingType = BuildingType.Landmark
                    };
                // HRE Age 3
                case LandmarkId.HRE_Age3_A:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.HolyRomanEmpire, TargetAge = 3,
                        Name = "Burgrave Palace", Description = "Trains units at great speed.",
                        FoodCost = 800, GoldCost = 400, ConstructionTicks = 4500,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 2500, Armor = 5,
                        EffectiveBuildingType = BuildingType.Landmark
                    };
                case LandmarkId.HRE_Age3_B:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.HolyRomanEmpire, TargetAge = 3,
                        Name = "Regnitz Cathedral", Description = "Generates gold from relics.",
                        FoodCost = 800, GoldCost = 400, ConstructionTicks = 4500,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 2500, Armor = 5,
                        EffectiveBuildingType = BuildingType.Landmark
                    };

                default:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.English, TargetAge = 2,
                        Name = "Unknown", Description = "",
                        FoodCost = 400, GoldCost = 200, ConstructionTicks = 3000,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 2500, Armor = 5,
                        EffectiveBuildingType = BuildingType.Landmark
                    };
            }
        }

        public static (LandmarkId a, LandmarkId b) GetChoices(Civilization civ, int targetAge)
        {
            switch (civ)
            {
                case Civilization.English:
                    switch (targetAge)
                    {
                        case 2: return (LandmarkId.English_Age2_A, LandmarkId.English_Age2_B);
                        case 3: return (LandmarkId.English_Age3_A, LandmarkId.English_Age3_B);
                        case 4: return (LandmarkId.English_Age4_A, LandmarkId.English_Age4_B);
                    }
                    break;
                case Civilization.French:
                    switch (targetAge)
                    {
                        case 2: return (LandmarkId.French_Age2_A, LandmarkId.French_Age2_B);
                        case 3: return (LandmarkId.French_Age3_A, LandmarkId.French_Age3_B);
                    }
                    break;
                case Civilization.HolyRomanEmpire:
                    switch (targetAge)
                    {
                        case 2: return (LandmarkId.HRE_Age2_A, LandmarkId.HRE_Age2_B);
                        case 3: return (LandmarkId.HRE_Age3_A, LandmarkId.HRE_Age3_B);
                    }
                    break;
            }
            return (LandmarkId.English_Age2_A, LandmarkId.English_Age2_B);
        }

        public static bool HasChoices(Civilization civ, int targetAge)
        {
            switch (civ)
            {
                case Civilization.English:
                    return targetAge >= 2 && targetAge <= 4;
                case Civilization.French:
                case Civilization.HolyRomanEmpire:
                    return targetAge >= 2 && targetAge <= 3;
                default:
                    return false;
            }
        }

        public static BuildingType GetEffectiveBuildingType(BuildingData building)
        {
            if (building == null) return BuildingType.House;
            if (building.Type != BuildingType.Landmark) return building.Type;
            var def = Get(building.LandmarkId);
            return def.EffectiveBuildingType == default ? BuildingType.Landmark : def.EffectiveBuildingType;
        }

        public static bool IsDropOffBuilding(BuildingData building)
        {
            return GameSimulation.IsDropOffBuilding(GetEffectiveBuildingType(building));
        }

        public static bool AcceptsResourceType(BuildingData building, ResourceType resourceType)
        {
            return GameSimulation.AcceptsResourceType(GetEffectiveBuildingType(building), resourceType);
        }

        public static int GetBuildingRequiredAge(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.House:
                case BuildingType.Mill:
                case BuildingType.LumberYard:
                case BuildingType.Farm:
                case BuildingType.Mine:
                case BuildingType.Barracks:
                case BuildingType.Wall:
                case BuildingType.WoodGate:
                    return 1;
                case BuildingType.Tower:
                case BuildingType.ArcheryRange:
                case BuildingType.Stables:
                case BuildingType.TownCenter:
                case BuildingType.Blacksmith:
                case BuildingType.Market:
                    return 2;
                case BuildingType.Monastery:
                case BuildingType.University:
                case BuildingType.SiegeWorkshop:
                case BuildingType.Keep:
                case BuildingType.StoneWall:
                case BuildingType.StoneGate:
                case BuildingType.Wonder:
                    return 3;
                default:
                    return 1;
            }
        }

        public static int GetUnitRequiredAge(int unitType)
        {
            switch (unitType)
            {
                case UnitData.KingUnitType:
                    return 2;
                case 6:  // Man-at-Arms
                case 7:  // Knight
                case 8:  // Crossbowman
                case 9:  // Monk
                case 13: // Battering Ram
                case 14: // Mangonel
                case 15: // Trebuchet
                    return 3;
                default:
                    return 1;
            }
        }

        public static string AgeToRoman(int age)
        {
            switch (age)
            {
                case 1: return "I";
                case 2: return "II";
                case 3: return "III";
                case 4: return "IV";
                default: return age.ToString();
            }
        }
    }
}
