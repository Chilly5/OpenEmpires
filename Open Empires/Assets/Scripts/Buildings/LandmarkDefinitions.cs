namespace OpenEmpires
{
    public enum LandmarkId
    {
        English_Age2_A,
        English_Age2_B,
        English_Age3_A,
        English_Age3_B,
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
                        Name = "King's Palace", Description = "A grand royal residence.",
                        FoodCost = 400, GoldCost = 200, ConstructionTicks = 3000,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 2500, Armor = 5
                    };
                case LandmarkId.English_Age2_B:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.English, TargetAge = 2,
                        Name = "Abbey of Kings", Description = "A sacred place of worship.",
                        FoodCost = 400, GoldCost = 200, ConstructionTicks = 3000,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 2500, Armor = 5
                    };
                // English Age 3
                case LandmarkId.English_Age3_A:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.English, TargetAge = 3,
                        Name = "King's College", Description = "A center of learning and strategy.",
                        FoodCost = 800, GoldCost = 400, ConstructionTicks = 4500,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 2500, Armor = 5
                    };
                case LandmarkId.English_Age3_B:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.English, TargetAge = 3,
                        Name = "White Tower", Description = "An imposing fortress of defense.",
                        FoodCost = 800, GoldCost = 400, ConstructionTicks = 4500,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 2500, Armor = 5
                    };

                // French Age 2
                case LandmarkId.French_Age2_A:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.French, TargetAge = 2,
                        Name = "Chamber of Commerce", Description = "A hub of trade and diplomacy.",
                        FoodCost = 400, GoldCost = 200, ConstructionTicks = 3000,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 2500, Armor = 5
                    };
                case LandmarkId.French_Age2_B:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.French, TargetAge = 2,
                        Name = "School of Cavalry", Description = "Trains elite mounted warriors.",
                        FoodCost = 400, GoldCost = 200, ConstructionTicks = 3000,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 2500, Armor = 5
                    };
                // French Age 3
                case LandmarkId.French_Age3_A:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.French, TargetAge = 3,
                        Name = "Royal Institute", Description = "A place of military innovation.",
                        FoodCost = 800, GoldCost = 400, ConstructionTicks = 4500,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 2500, Armor = 5
                    };
                case LandmarkId.French_Age3_B:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.French, TargetAge = 3,
                        Name = "Guild Hall", Description = "Provides economic advantages.",
                        FoodCost = 800, GoldCost = 400, ConstructionTicks = 4500,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 2500, Armor = 5
                    };

                // HRE Age 2
                case LandmarkId.HRE_Age2_A:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.HolyRomanEmpire, TargetAge = 2,
                        Name = "Aachen Chapel", Description = "A sacred imperial chapel.",
                        FoodCost = 400, GoldCost = 200, ConstructionTicks = 3000,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 2500, Armor = 5
                    };
                case LandmarkId.HRE_Age2_B:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.HolyRomanEmpire, TargetAge = 2,
                        Name = "Meinwerk Palace", Description = "An economic powerhouse.",
                        FoodCost = 400, GoldCost = 200, ConstructionTicks = 3000,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 2500, Armor = 5
                    };
                // HRE Age 3
                case LandmarkId.HRE_Age3_A:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.HolyRomanEmpire, TargetAge = 3,
                        Name = "Burgrave Palace", Description = "Trains units at great speed.",
                        FoodCost = 800, GoldCost = 400, ConstructionTicks = 4500,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 2500, Armor = 5
                    };
                case LandmarkId.HRE_Age3_B:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.HolyRomanEmpire, TargetAge = 3,
                        Name = "Regnitz Cathedral", Description = "Generates gold from relics.",
                        FoodCost = 800, GoldCost = 400, ConstructionTicks = 4500,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 2500, Armor = 5
                    };

                default:
                    return new LandmarkDefinition
                    {
                        Id = id, Civ = Civilization.English, TargetAge = 2,
                        Name = "Unknown", Description = "",
                        FoodCost = 400, GoldCost = 200, ConstructionTicks = 3000,
                        FootprintWidth = 4, FootprintHeight = 4, MaxHealth = 2500, Armor = 5
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
                case BuildingType.Tower:
                case BuildingType.WoodGate:
                    return 1;
                case BuildingType.Wall:
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
                case 6:  // Man-at-Arms
                case 7:  // Knight
                case 8:  // Crossbowman
                case 9:  // Monk
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
                default: return age.ToString();
            }
        }
    }
}
