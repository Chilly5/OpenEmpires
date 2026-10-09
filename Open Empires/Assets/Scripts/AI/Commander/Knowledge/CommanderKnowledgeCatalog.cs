using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenEmpires
{
    /// <summary>
    /// Detached, read-only Commander knowledge.  This is a projection of the
    /// live simulation/configuration; it is never used as gameplay authority.
    /// </summary>
    [Serializable]
    public sealed class KnowledgeCost
    {
        public int Food { get; private set; }
        public int Wood { get; private set; }
        public int Gold { get; private set; }
        public int Stone { get; private set; }

        public KnowledgeCost(int food, int wood, int gold, int stone = 0)
        {
            Food = food; Wood = wood; Gold = gold; Stone = stone;
        }

        public KnowledgeCost Copy() => new KnowledgeCost(Food, Wood, Gold, Stone);
    }

    [Serializable]
    public sealed class UnitKnowledge
    {
        public string StableId { get; private set; }
        public string DisplayName { get; private set; }
        public IReadOnlyList<string> Aliases { get; private set; }
        public int UnitType { get; private set; }
        public int RequiredAge { get; private set; }
        public KnowledgeCost Cost { get; private set; }
        public int TrainTimeTicks { get; private set; }
        public int ProductionBuildingType { get; private set; }
        public int MaxHealth { get; private set; }
        public int AttackDamage { get; private set; }
        public float AttackRange { get; private set; }
        public float MoveSpeed { get; private set; }
        public int MeleeArmor { get; private set; }
        public int RangedArmor { get; private set; }
        public bool IsRanged { get; private set; }

        public UnitKnowledge(int type, string name, string[] aliases, int age,
            KnowledgeCost cost, int trainTicks, int production, int hp, int attack,
            float range, float speed, int meleeArmor, int rangedArmor, bool ranged)
        {
            UnitType = type; StableId = "unit:" + type; DisplayName = name;
            Aliases = Array.AsReadOnly((aliases ?? new string[0]).ToArray()); RequiredAge = age; Cost = cost;
            TrainTimeTicks = trainTicks; ProductionBuildingType = production;
            MaxHealth = hp; AttackDamage = attack; AttackRange = range;
            MoveSpeed = speed; MeleeArmor = meleeArmor; RangedArmor = rangedArmor;
            IsRanged = ranged;
        }

        public UnitKnowledge Copy()
        {
            return new UnitKnowledge(UnitType, DisplayName, Aliases.ToArray(), RequiredAge,
                Cost.Copy(), TrainTimeTicks, ProductionBuildingType, MaxHealth, AttackDamage,
                AttackRange, MoveSpeed, MeleeArmor, RangedArmor, IsRanged);
        }
    }

    [Serializable]
    public sealed class BuildingKnowledge
    {
        public string StableId { get; private set; }
        public string DisplayName { get; private set; }
        public IReadOnlyList<string> Aliases { get; private set; }
        public BuildingType BuildingType { get; private set; }
        public int RequiredAge { get; private set; }
        public KnowledgeCost Cost { get; private set; }
        public int FootprintWidth { get; private set; }
        public int FootprintHeight { get; private set; }
        public int MaxHealth { get; private set; }
        public int ConstructionTicks { get; private set; }
        public LandmarkId? Landmark { get; private set; }

        public BuildingKnowledge(BuildingType type, string name, string[] aliases, int age,
            KnowledgeCost cost, int width, int height, int hp, int constructionTicks = 0, LandmarkId? landmarkId = null)
        {
            BuildingType = type; StableId = landmarkId.HasValue ? "landmark:" + landmarkId.Value : "building:" + type; DisplayName = name;
            Landmark = landmarkId;
            Aliases = Array.AsReadOnly((aliases ?? new string[0]).ToArray()); RequiredAge = age; Cost = cost;
            FootprintWidth = width; FootprintHeight = height; MaxHealth = hp; ConstructionTicks = constructionTicks;
        }

        public BuildingKnowledge Copy() => new BuildingKnowledge(BuildingType, DisplayName,
            Aliases.ToArray(), RequiredAge, Cost.Copy(), FootprintWidth, FootprintHeight, MaxHealth, ConstructionTicks, Landmark);
    }

    [Serializable]
    public sealed class TechnologyKnowledge
    {
        public string StableId { get; private set; }
        public string DisplayName { get; private set; }
        public TechnologyType TechnologyType { get; private set; }
        public int RequiredAge { get; private set; }
        public KnowledgeCost Cost { get; private set; }
        public BuildingType ResearchBuilding { get; private set; }
        public int ResearchTimeTicks { get; private set; }

        public TechnologyKnowledge(TechnologyType type, string name, int age,
            KnowledgeCost cost, BuildingType building, int researchTicks = 0)
        {
            TechnologyType = type; StableId = "technology:" + type; DisplayName = name;
            RequiredAge = age; Cost = cost; ResearchBuilding = building; ResearchTimeTicks = researchTicks;
        }

        public TechnologyKnowledge Copy() => new TechnologyKnowledge(TechnologyType, DisplayName,
            RequiredAge, Cost.Copy(), ResearchBuilding, ResearchTimeTicks);
    }

    [Serializable]
    public sealed class CivilizationKnowledge
    {
        public string StableId { get; private set; }
        public string DisplayName { get; private set; }
        public Civilization Civilization { get; private set; }
        public IReadOnlyList<string> AvailableUnitIds { get; private set; }
        public IReadOnlyList<string> AvailableBuildingIds { get; private set; }

        internal CivilizationKnowledge(Civilization civ, string[] units, string[] buildings)
        {
            Civilization = civ; StableId = "civilization:" + civ; DisplayName = civ.ToString();
            AvailableUnitIds = Array.AsReadOnly((units ?? new string[0]).ToArray()); AvailableBuildingIds = Array.AsReadOnly((buildings ?? new string[0]).ToArray());
        }

        public CivilizationKnowledge Copy() => new CivilizationKnowledge(Civilization,
            AvailableUnitIds.ToArray(), AvailableBuildingIds.ToArray());
    }

    [Serializable]
    public sealed class AgeKnowledge
    {
        public string StableId { get; private set; }
        public int Age { get; private set; }
        public string DisplayName { get; private set; }
        public KnowledgeCost Cost { get; private set; }

        internal AgeKnowledge(int age, KnowledgeCost cost)
        {
            Age = age; StableId = "age:" + age; DisplayName = "Age " + age;
            Cost = cost;
        }

        public AgeKnowledge Copy() => new AgeKnowledge(Age, Cost.Copy());
    }

    [Serializable]
    public sealed class ResourceKnowledge
    {
        public string StableId { get; private set; }
        public ResourceType ResourceType { get; private set; }
        public string DisplayName { get; private set; }
        public string GatheringClassification { get; private set; }

        internal ResourceKnowledge(ResourceType type, string classification)
        {
            ResourceType = type; StableId = "resource:" + type; DisplayName = type.ToString();
            GatheringClassification = classification;
        }

        public ResourceKnowledge Copy() => new ResourceKnowledge(ResourceType, GatheringClassification);
    }

    /// <summary>Bounded, deterministic read-only catalog built from canonical game data.</summary>
    public sealed class GameKnowledgeCatalog
    {
        public const int MaxContextRecords = 24;
        public const int MaxAliasLength = 64;
        private readonly IReadOnlyList<UnitKnowledge> units;
        private readonly IReadOnlyList<BuildingKnowledge> buildings;
        private readonly IReadOnlyList<TechnologyKnowledge> technologies;
        private readonly IReadOnlyList<CivilizationKnowledge> civilizations;
        private readonly IReadOnlyList<AgeKnowledge> ages;
        private readonly IReadOnlyList<ResourceKnowledge> resources;

        public IReadOnlyList<UnitKnowledge> Units => units;
        public IReadOnlyList<BuildingKnowledge> Buildings => buildings;
        public IReadOnlyList<TechnologyKnowledge> Technologies => technologies;
        public IReadOnlyList<CivilizationKnowledge> Civilizations => civilizations;
        public IReadOnlyList<AgeKnowledge> Ages => ages;
        public IReadOnlyList<ResourceKnowledge> Resources => resources;

        private static readonly List<UnitKnowledge> customUnits = new List<UnitKnowledge>();
        private static readonly List<BuildingKnowledge> customBuildings = new List<BuildingKnowledge>();
        private static readonly object canonicalIdentityLock = new object();
        private static GameKnowledgeCatalog canonicalIdentityCatalog;

        public static void RegisterCustomUnit(UnitKnowledge unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            customUnits.Add(unit);
        }

        public static void RegisterCustomBuilding(BuildingKnowledge building)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            customBuildings.Add(building);
        }

        public static void ResetCustomContent()
        {
            customUnits.Clear();
            customBuildings.Clear();
        }

        internal static GameKnowledgeCatalog BuildCanonicalIdentityCatalog()
        {
            if (canonicalIdentityCatalog != null) return canonicalIdentityCatalog;
            lock (canonicalIdentityLock)
            {
                if (canonicalIdentityCatalog != null) return canonicalIdentityCatalog;
                var units = new List<UnitKnowledge>();
                foreach (int type in KeybindManager.BindableUnitTypes)
                {
                    BuildingType producer = GameSimulation.TryGetCanonicalProductionBuildingType(type, out var nativeProducer)
                        ? nativeProducer : BuildingType.Landmark;
                    units.Add(new UnitKnowledge(type, UnitInfoUI.GetUnitTypeDisplayName(type), BuildUnitAliases(type),
                        LandmarkDefinitions.GetUnitRequiredAge(type), new KnowledgeCost(0, 0, 0), 0,
                        (int)producer, 0, 0, 0f, 0f, 0, 0, false));
                }
                units.Add(new UnitKnowledge(5, UnitInfoUI.GetUnitTypeDisplayName(5), BuildUnitAliases(5),
                    0, new KnowledgeCost(0, 0, 0), 0, -1, 0, 0, 0f, 0f, 0, 0, false));
                var buildings = new List<BuildingKnowledge>();
                foreach (BuildingType type in Enum.GetValues(typeof(BuildingType)))
                {
                    string name = UnitInfoUI.GetBuildingTypeDisplayName(type);
                    buildings.Add(new BuildingKnowledge(type, name, BuildBuildingAliases(type, name),
                        LandmarkDefinitions.GetBuildingRequiredAge(type), new KnowledgeCost(0, 0, 0), 0, 0, 0));
                }
                AppendNamedLandmarks(buildings);
                canonicalIdentityCatalog = new GameKnowledgeCatalog(units, buildings,
                    new List<TechnologyKnowledge>(), new List<CivilizationKnowledge>(), new List<AgeKnowledge>(),
                    new List<ResourceKnowledge>());
                return canonicalIdentityCatalog;
            }
        }

        private GameKnowledgeCatalog(List<UnitKnowledge> units, List<BuildingKnowledge> buildings,
            List<TechnologyKnowledge> technologies, List<CivilizationKnowledge> civilizations,
            List<AgeKnowledge> ages, List<ResourceKnowledge> resources)
        {
            ValidateUniqueIds(units, x => x.StableId);
            ValidateUniqueIds(buildings, x => x.StableId);
            ValidateUniqueIds(technologies, x => x.StableId);
            ValidateUniqueIds(civilizations, x => x.StableId);
            ValidateUniqueIds(ages, x => x.StableId);
            ValidateUniqueIds(resources, x => x.StableId);

            this.units = Array.AsReadOnly(units.ToArray());
            this.buildings = Array.AsReadOnly(buildings.ToArray());
            this.technologies = Array.AsReadOnly(technologies.ToArray());
            this.civilizations = Array.AsReadOnly(civilizations.ToArray());
            this.ages = Array.AsReadOnly(ages.ToArray());
            this.resources = Array.AsReadOnly(resources.ToArray());
        }

        private static void ValidateUniqueIds<T>(IEnumerable<T> items, Func<T, string> idSelector)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                string id = idSelector(item);
                if (string.IsNullOrEmpty(id) || !seen.Add(id))
                    throw new InvalidOperationException("Duplicate or invalid stable ID: " + id);
            }
        }

        public static GameKnowledgeCatalog Build(SimulationConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            var u = BuildUnits(config);
            var b = BuildBuildings(config);
            var t = BuildTechnologies(config);
            var civs = BuildCivilizations(u, b);
            var ages = BuildAges();
            var resources = Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>()
                .OrderBy(x => (int)x).Select(x => new ResourceKnowledge(x, x == ResourceType.Food ? "food-source" : x.ToString().ToLowerInvariant() + "-source"))
                .ToList();
            return new GameKnowledgeCatalog(u, b, t, civs, ages, resources);
        }

        /// <summary>Builds a detached catalog using canonical simulation query helpers.</summary>
        public static GameKnowledgeCatalog Build(GameSimulation simulation)
        {
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            var baseCatalog = Build(simulation.Config);
            var buildings = baseCatalog.Buildings.Select(x =>
            {
                if (x.Landmark.HasValue) return x.Copy();
                var cost = new KnowledgeCost(simulation.GetBuildingFoodCost(x.BuildingType),
                    simulation.GetBuildingWoodCost(x.BuildingType), simulation.GetBuildingGoldCost(x.BuildingType),
                    simulation.GetBuildingStoneCost(x.BuildingType));
                return new BuildingKnowledge(x.BuildingType, x.DisplayName, x.Aliases.ToArray(), x.RequiredAge,
                    cost, x.FootprintWidth, x.FootprintHeight, x.MaxHealth, simulation.GetConstructionTicks(x.BuildingType));
            }).ToList();
            var technologies = baseCatalog.Technologies.Select(x =>
            {
                simulation.GetTechnologySpec(x.TechnologyType, out int age, out int food, out int gold,
                    out BuildingType building, out int ticks);
                return new TechnologyKnowledge(x.TechnologyType, x.DisplayName, age,
                    new KnowledgeCost(food, 0, gold), building, ticks);
            }).ToList();
            var civs = BuildCivilizations(baseCatalog.Units.ToList(), buildings);
            return new GameKnowledgeCatalog(baseCatalog.Units.Select(x => x.Copy()).ToList(), buildings,
                technologies, civs, baseCatalog.Ages.Select(x => x.Copy()).ToList(),
                baseCatalog.Resources.Select(x => x.Copy()).ToList());
        }

        private static List<AgeKnowledge> BuildAges()
        {
            var ages = new List<AgeKnowledge> { new AgeKnowledge(1, new KnowledgeCost(0, 0, 0)) };
            for (int age = 2; age <= 4; age++)
            {
                var landmark = LandmarkDefinitions.Get(LandmarkDefinitions.GetChoices(Civilization.English, age).a);
                ages.Add(new AgeKnowledge(age, new KnowledgeCost(landmark.FoodCost, 0, landmark.GoldCost)));
            }
            return ages;
        }

        /// <summary>Builds the same base catalog and overlays only detached player-effective values.</summary>
        public static EffectivePlayerKnowledge BuildEffective(GameSimulation simulation, int playerId)
        {
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            var baseCatalog = Build(simulation);
            var effectiveCosts = new Dictionary<int, KnowledgeCost>();
            foreach (var unit in baseCatalog.Units)
            {
                simulation.GetUnitTrainingSpec(playerId, unit.UnitType, out _, out int food, out int wood, out int gold, out _);
                effectiveCosts[unit.UnitType] = new KnowledgeCost(food, wood, gold);
            }
            return new EffectivePlayerKnowledge(baseCatalog, simulation.GetPlayerCivilization(playerId),
                simulation.GetPlayerAge(playerId), effectiveCosts);
        }

        public UnitKnowledge FindUnit(string idOrAlias)
        {
            return Find(units, idOrAlias, x => x.StableId, x => x.DisplayName, x => x.Aliases);
        }

        internal UnitKnowledge FindUnitByType(int type) => units.FirstOrDefault(x => x.UnitType == type);

        public BuildingKnowledge FindBuilding(string idOrAlias)
        {
            return Find(buildings, idOrAlias, x => x.StableId, x => x.DisplayName, x => x.Aliases);
        }

        public TechnologyKnowledge FindTechnology(string idOrName)
        {
            return Find(technologies, idOrName, x => x.StableId, x => x.DisplayName, _ => Array.Empty<string>());
        }

        public KnowledgeSlice Slice(string query, int maxRecords = MaxContextRecords)
        {
            int bound = Math.Max(1, Math.Min(MaxContextRecords, maxRecords));
            string key = Normalize(query);
            var selectedUnits = units.Where(x => Contains(x.StableId, key) || Contains(x.DisplayName, key) || x.Aliases.Any(a => Contains(a, key))).Select(x => x.Copy()).ToList();
            var selectedBuildings = buildings.Where(x => Contains(x.StableId, key) || Contains(x.DisplayName, key) || x.Aliases.Any(a => Contains(a, key))).Select(x => x.Copy()).ToList();
            var selectedTechs = technologies.Where(x => Contains(x.StableId, key) || Contains(x.DisplayName, key)).Select(x => x.Copy()).ToList();
            if (selectedUnits.Count == 0 && selectedBuildings.Count == 0 && selectedTechs.Count == 0)
            {
                selectedUnits = units.Select(x => x.Copy()).ToList();
            }
            // The bound applies to the complete provider slice, not independently
            // to each category. Keep deterministic category priority/order.
            int remaining = bound;
            selectedUnits = selectedUnits.Take(remaining).ToList(); remaining -= selectedUnits.Count;
            selectedBuildings = selectedBuildings.Take(Math.Max(0, remaining)).ToList(); remaining -= selectedBuildings.Count;
            selectedTechs = selectedTechs.Take(Math.Max(0, remaining)).ToList();
            return new KnowledgeSlice(selectedUnits, selectedBuildings, selectedTechs);
        }

        private static T Find<T>(IEnumerable<T> source, string idOrAlias, Func<T, string> id,
            Func<T, string> name, Func<T, IEnumerable<string>> aliases) where T : class
        {
            string key = Normalize(idOrAlias);
            var matches = source.Where(x => Normalize(id(x)) == key || Normalize(name(x)) == key || aliases(x).Any(a => Normalize(a) == key)).ToList();
            if (matches.Count > 1) throw new InvalidOperationException("Ambiguous knowledge alias: " + idOrAlias);
            return matches.Count == 1 ? matches[0] : null;
        }

        private static bool Contains(string value, string key) => string.IsNullOrEmpty(key) || Normalize(value).Contains(key);
        private static string Normalize(string value) => (value ?? string.Empty).Trim().ToLowerInvariant().Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty);

        private static List<UnitKnowledge> BuildUnits(SimulationConfig c, bool includeCustom = true)
        {
            var list = new List<UnitKnowledge>();
            AddUnit(list, c, 0, "Villager", BuildUnitAliases(0), 1, new KnowledgeCost(c.VillagerFoodCost, 0, 0), c.VillagerTrainTimeTicks, (int)BuildingType.TownCenter, c.VillagerMaxHealth, c.VillagerAttackDamage, c.VillagerAttackRange, c.UnitMoveSpeed, c.VillagerMeleeArmor, c.VillagerRangedArmor, false);
            AddUnit(list, c, 1, "Spearman", BuildUnitAliases(1), 1, new KnowledgeCost(c.SpearmanFoodCost, c.SpearmanWoodCost, 0), c.SpearmanTrainTimeTicks, (int)BuildingType.Barracks, c.SpearmanMaxHealth, c.SpearmanAttackDamage, c.SpearmanAttackRange, c.UnitMoveSpeed, c.SpearmanMeleeArmor, c.SpearmanRangedArmor, false);
            AddUnit(list, c, 2, "Archer", BuildUnitAliases(2), 1, new KnowledgeCost(c.ArcherFoodCost, c.ArcherWoodCost, 0), c.ArcherTrainTimeTicks, (int)BuildingType.ArcheryRange, c.ArcherMaxHealth, c.ArcherAttackDamage, c.ArcherAttackRange, c.ArcherMoveSpeed, c.ArcherMeleeArmor, c.ArcherRangedArmor, true);
            AddUnit(list, c, 3, "Horseman", BuildUnitAliases(3), 1, new KnowledgeCost(c.HorsemanFoodCost, c.HorsemanWoodCost, 0), c.HorsemanTrainTimeTicks, (int)BuildingType.Stables, c.HorsemanMaxHealth, c.HorsemanAttackDamage, c.HorsemanAttackRange, c.HorsemanMoveSpeed, c.HorsemanMeleeArmor, c.HorsemanRangedArmor, false);
            AddUnit(list, c, 4, "Scout", BuildUnitAliases(4), 1, new KnowledgeCost(c.ScoutFoodCost, c.ScoutWoodCost, 0), c.ScoutTrainTimeTicks, (int)BuildingType.Stables, c.ScoutMaxHealth, c.ScoutAttackDamage, c.ScoutAttackRange, c.ScoutMoveSpeed, c.ScoutMeleeArmor, c.ScoutRangedArmor, false);
            AddUnit(list, c, 5, "Sheep", BuildUnitAliases(5), 0, new KnowledgeCost(0, 0, 0), 0, -1, c.SheepMaxHealth, 0, 0f, c.SheepMoveSpeed, 0, 0, false);
            AddUnit(list, c, 6, "Man-at-Arms", BuildUnitAliases(6), 3, new KnowledgeCost(c.ManAtArmsFoodCost, 0, c.ManAtArmsGoldCost), c.ManAtArmsTrainTimeTicks, (int)BuildingType.Barracks, c.ManAtArmsMaxHealth, c.ManAtArmsAttackDamage, c.ManAtArmsAttackRange, c.ManAtArmsMoveSpeed, c.ManAtArmsMeleeArmor, c.ManAtArmsRangedArmor, false);
            AddUnit(list, c, 7, "Knight", BuildUnitAliases(7), 3, new KnowledgeCost(c.KnightFoodCost, 0, c.KnightGoldCost), c.KnightTrainTimeTicks, (int)BuildingType.Stables, c.KnightMaxHealth, c.KnightAttackDamage, c.KnightAttackRange, c.KnightMoveSpeed, c.KnightMeleeArmor, c.KnightRangedArmor, false);
            AddUnit(list, c, 8, "Crossbowman", BuildUnitAliases(8), 3, new KnowledgeCost(c.CrossbowmanFoodCost, 0, c.CrossbowmanGoldCost), c.CrossbowmanTrainTimeTicks, (int)BuildingType.ArcheryRange, c.CrossbowmanMaxHealth, c.CrossbowmanAttackDamage, c.CrossbowmanAttackRange, c.CrossbowmanMoveSpeed, c.CrossbowmanMeleeArmor, c.CrossbowmanRangedArmor, true);
            AddUnit(list, c, 9, "Monk", BuildUnitAliases(9), 3, new KnowledgeCost(c.MonkFoodCost, 0, c.MonkGoldCost), c.MonkTrainTimeTicks, (int)BuildingType.Monastery, c.MonkMaxHealth, c.MonkAttackDamage, c.MonkAttackRange, c.MonkMoveSpeed, c.MonkMeleeArmor, c.MonkRangedArmor, false);
            AddUnit(list, c, 10, "Longbowman", BuildUnitAliases(10), 1, new KnowledgeCost(c.LongbowmanFoodCost, c.LongbowmanWoodCost, 0), c.LongbowmanTrainTimeTicks, (int)BuildingType.ArcheryRange, c.LongbowmanMaxHealth, c.LongbowmanAttackDamage, c.LongbowmanAttackRange, c.LongbowmanMoveSpeed, c.LongbowmanMeleeArmor, c.LongbowmanRangedArmor, true);
            AddUnit(list, c, 11, "Gendarme", BuildUnitAliases(11), 1, new KnowledgeCost(c.GendarmeFoodCost, c.GendarmeWoodCost, 0), c.GendarmeTrainTimeTicks, (int)BuildingType.Stables, c.GendarmeMaxHealth, c.GendarmeAttackDamage, c.GendarmeAttackRange, c.GendarmeMoveSpeed, c.GendarmeMeleeArmor, c.GendarmeRangedArmor, false);
            AddUnit(list, c, 12, "Landsknecht", BuildUnitAliases(12), 1, new KnowledgeCost(c.LandsknechtFoodCost, c.LandsknechtWoodCost, 0), c.LandsknechtTrainTimeTicks, (int)BuildingType.Barracks, c.LandsknechtMaxHealth, c.LandsknechtAttackDamage, c.LandsknechtAttackRange, c.LandsknechtMoveSpeed, c.LandsknechtMeleeArmor, c.LandsknechtRangedArmor, false);
            AddUnit(list, c, 13, "Battering Ram", BuildUnitAliases(13), 3, new KnowledgeCost(0, c.BatteringRamWoodCost, c.BatteringRamGoldCost), c.BatteringRamTrainTimeTicks, (int)BuildingType.SiegeWorkshop, c.BatteringRamMaxHealth, c.BatteringRamAttackDamage, c.BatteringRamAttackRange, c.BatteringRamMoveSpeed, c.BatteringRamMeleeArmor, c.BatteringRamRangedArmor, false);
            AddUnit(list, c, 14, "Mangonel", BuildUnitAliases(14), 3, new KnowledgeCost(0, c.MangonelWoodCost, c.MangonelGoldCost), c.MangonelTrainTimeTicks, (int)BuildingType.SiegeWorkshop, c.MangonelMaxHealth, c.MangonelAttackDamage, c.MangonelAttackRange, c.MangonelMoveSpeed, c.MangonelMeleeArmor, c.MangonelRangedArmor, true);
            AddUnit(list, c, 15, "Trebuchet", BuildUnitAliases(15), 3, new KnowledgeCost(0, c.TrebuchetWoodCost, c.TrebuchetGoldCost), c.TrebuchetTrainTimeTicks, (int)BuildingType.SiegeWorkshop, c.TrebuchetMaxHealth, c.TrebuchetAttackDamage, c.TrebuchetAttackRange, c.TrebuchetMoveSpeed, c.TrebuchetMeleeArmor, c.TrebuchetRangedArmor, true);
            AddUnit(list, c, UnitData.KingUnitType, "English King", BuildUnitAliases(UnitData.KingUnitType), 2, new KnowledgeCost(c.KingFoodCost, 0, c.KingGoldCost), c.KingTrainTimeTicks, (int)BuildingType.Landmark, c.KingMaxHealth, c.KingAttackDamage, c.KingAttackRange, c.KingMoveSpeed, c.KingMeleeArmor, c.KingRangedArmor, false);
            if (includeCustom)
                for (int i = 0; i < customUnits.Count; i++) list.Add(customUnits[i].Copy());
            return list.OrderBy(x => x.StableId, StringComparer.Ordinal).ToList();
        }

        private static void AddUnit(List<UnitKnowledge> list, SimulationConfig c, int type, string name, string[] aliases, int age, KnowledgeCost cost, int train, int producer, int hp, int attack, float range, float speed, int melee, int ranged, bool isRanged)
        {
            if (GameSimulation.TryGetCanonicalProductionBuildingType(type, out BuildingType nativeProducer))
                producer = (int)nativeProducer;
            if (type != 5) age = LandmarkDefinitions.GetUnitRequiredAge(type);
            list.Add(new UnitKnowledge(type, UnitInfoUI.GetUnitTypeDisplayName(type), aliases, age, cost, train, producer, hp, attack, range, speed, melee, ranged, isRanged));
        }

        private static string[] BuildUnitAliases(int type)
        {
            var aliases = new List<string> { UnitInfoUI.GetUnitTypeDisplayName(type) };
            switch (type)
            {
                case 0: aliases.AddRange(new[] { "villagers", "worker", "workers", "peasant", "peasants", "builder", "builders", "vil", "vils", "vill", "vills" }); break;
                case 1: aliases.AddRange(new[] { "spearmen", "spear" }); break;
                case 2: aliases.AddRange(new[] { "archers", "bowman" }); break;
                case 3: aliases.Add("horsemen"); break;
                case 4: aliases.Add("scouts"); break;
                case 5: aliases.Add("sheep"); break;
                case 6: aliases.AddRange(new[] { "manatarms", "maa" }); break;
                case 7: aliases.Add("knights"); break;
                case 8: aliases.AddRange(new[] { "crossbowmen", "crossbow" }); break;
                case 9: aliases.Add("monks"); break;
                case 10: aliases.AddRange(new[] { "longbowmen", "longbow" }); break;
                case 11: aliases.Add("gendarmes"); break;
                case 12: aliases.Add("landsknechts"); break;
                case 13: aliases.AddRange(new[] { "batteringrams", "ram" }); break;
                case 14: aliases.Add("mangonels"); break;
                case 15: aliases.Add("trebuchets"); break;
                case UnitData.KingUnitType: aliases.AddRange(new[] { "king", "kings" }); break;
            }
            return aliases.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static List<BuildingKnowledge> BuildBuildings(SimulationConfig c, bool includeCustom = true)
        {
            var list = new List<BuildingKnowledge>();
            foreach (BuildingType type in Enum.GetValues(typeof(BuildingType)))
            {
                int width = 1, height = 1, hp = 0, wood = 0, stone = 0;
                switch (type)
                {
                    case BuildingType.House: width = c.HouseFootprintWidth; height = c.HouseFootprintHeight; hp = c.HouseMaxHealth; wood = c.HouseWoodCost; break;
                    case BuildingType.Barracks: width = c.BarracksFootprintWidth; height = c.BarracksFootprintHeight; hp = c.BarracksMaxHealth; wood = c.BarracksWoodCost; break;
                    case BuildingType.TownCenter: width = c.TownCenterFootprintWidth; height = c.TownCenterFootprintHeight; hp = c.TownCenterMaxHealth; wood = c.TownCenterWoodCost; stone = c.TownCenterStoneCost; break;
                    case BuildingType.Mill: width = c.MillFootprintWidth; height = c.MillFootprintHeight; wood = c.MillWoodCost; break;
                    case BuildingType.LumberYard: width = c.LumberYardFootprintWidth; height = c.LumberYardFootprintHeight; wood = c.LumberYardWoodCost; break;
                    case BuildingType.Mine: width = c.MineFootprintWidth; height = c.MineFootprintHeight; wood = c.MineWoodCost; break;
                    case BuildingType.ArcheryRange: width = c.ArcheryRangeFootprintWidth; height = c.ArcheryRangeFootprintHeight; wood = c.ArcheryRangeWoodCost; break;
                    case BuildingType.Stables: width = c.StablesFootprintWidth; height = c.StablesFootprintHeight; wood = c.StablesWoodCost; break;
                    case BuildingType.Farm: width = c.FarmFootprintWidth; height = c.FarmFootprintHeight; wood = c.FarmWoodCost; break;
                    case BuildingType.Tower: width = c.TowerFootprintWidth; height = c.TowerFootprintHeight; wood = c.TowerWoodCost; break;
                    case BuildingType.Monastery: width = c.MonasteryFootprintWidth; height = c.MonasteryFootprintHeight; wood = c.MonasteryWoodCost; break;
                    case BuildingType.Blacksmith: width = c.BlacksmithFootprintWidth; height = c.BlacksmithFootprintHeight; wood = c.BlacksmithWoodCost; break;
                    case BuildingType.Market: width = c.MarketFootprintWidth; height = c.MarketFootprintHeight; wood = c.MarketWoodCost; break;
                    case BuildingType.University: width = c.UniversityFootprintWidth; height = c.UniversityFootprintHeight; wood = c.UniversityWoodCost; break;
                    case BuildingType.SiegeWorkshop: width = c.SiegeWorkshopFootprintWidth; height = c.SiegeWorkshopFootprintHeight; wood = c.SiegeWorkshopWoodCost; break;
                    case BuildingType.Keep: width = c.KeepFootprintWidth; height = c.KeepFootprintHeight; wood = c.KeepWoodCost; stone = c.KeepStoneCost; break;
                    case BuildingType.StoneWall: width = c.StoneWallFootprintWidth; height = c.StoneWallFootprintHeight; stone = c.StoneWallStoneCost; break;
                    case BuildingType.StoneGate: width = c.StoneGateFootprintWidth; height = c.StoneGateFootprintHeight; stone = c.StoneGateStoneCost; break;
                    case BuildingType.WoodGate: width = c.WoodGateFootprintWidth; height = c.WoodGateFootprintHeight; wood = c.WoodGateWoodCost; break;
                    case BuildingType.Wonder: width = c.WonderFootprintWidth; height = c.WonderFootprintHeight; wood = c.WonderWoodCost; break;
                    case BuildingType.Landmark: width = c.LandmarkFootprintWidth; height = c.LandmarkFootprintHeight; break;
                }
                string displayName = UnitInfoUI.GetBuildingTypeDisplayName(type);
                list.Add(new BuildingKnowledge(type, displayName, BuildBuildingAliases(type, displayName), LandmarkDefinitions.GetBuildingRequiredAge(type), new KnowledgeCost(0, wood, 0, stone), width, height, hp));
            }
            AppendNamedLandmarks(list);
            if (includeCustom)
                for (int i = 0; i < customBuildings.Count; i++) list.Add(customBuildings[i].Copy());
            return list.OrderBy(x => x.StableId, StringComparer.Ordinal).ToList();
        }

        private static string[] BuildBuildingAliases(BuildingType type, string displayName)
        {
            var aliases = new List<string> { type.ToString(), displayName };
            if (GameSimulation.IsDropOffBuilding(type))
            {
                aliases.AddRange(new[] { "dropoff", "drop-off", "dropoff building", "drop-off building" });
                foreach (ResourceType resource in Enum.GetValues(typeof(ResourceType)))
                {
                    bool acceptsResource = GameSimulation.AcceptsResourceType(type, resource);
                    if (!acceptsResource) continue;
                    bool acceptsOtherResource = Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>()
                        .Any(other => other != resource && GameSimulation.AcceptsResourceType(type, other));
                    if (!acceptsOtherResource)
                    {
                        string resourceName = resource.ToString().ToLowerInvariant();
                        aliases.Add(resourceName + " dropoff");
                        aliases.Add(resourceName + " drop-off");
                        aliases.Add(resourceName + " dropoff building");
                    }
                }
            }
            switch (type)
            {
                case BuildingType.House: aliases.AddRange(new[] { "houses", "home" }); break;
                case BuildingType.Barracks: aliases.Add("barrack"); break;
                case BuildingType.Stables: aliases.Add("stable"); break;
                case BuildingType.TownCenter: aliases.AddRange(new[] { "towncenter", "town centers", "tc", "tcs" }); break;
                case BuildingType.ArcheryRange: aliases.AddRange(new[] { "archeryrange", "archery ranges" }); break;
                case BuildingType.Tower: aliases.AddRange(new[] { "towers", "watchtower", "watch tower", "watchtowers", "watch towers", "guardtower", "guard tower", "guardtowers", "guard towers" }); break;
                case BuildingType.Mill: aliases.Add("mills"); break;
                case BuildingType.Farm: aliases.Add("farms"); break;
                case BuildingType.LumberYard: aliases.AddRange(new[] { "lumberyard", "lumber yards", "lumber camp", "lumbercamp", "wood camp", "woodcamp" }); break;
                case BuildingType.Mine: aliases.AddRange(new[] { "mining camp", "miningcamp", "mines" }); break;
                case BuildingType.Monastery: aliases.Add("monasteries"); break;
                case BuildingType.Blacksmith: aliases.Add("blacksmiths"); break;
                case BuildingType.Market: aliases.Add("markets"); break;
                case BuildingType.University: aliases.Add("universities"); break;
                case BuildingType.SiegeWorkshop: aliases.AddRange(new[] { "siege workshop", "siege workshops", "siegeworkshop" }); break;
                case BuildingType.Keep: aliases.Add("keeps"); break;
                case BuildingType.StoneWall: aliases.Add("stone walls"); break;
                case BuildingType.StoneGate: aliases.Add("stone gates"); break;
                case BuildingType.WoodGate: aliases.AddRange(new[] { "wood gate", "wood gates" }); break;
                case BuildingType.Wonder: aliases.Add("wonders"); break;
            }
            return aliases.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static void AppendNamedLandmarks(List<BuildingKnowledge> buildings)
        {
            foreach (LandmarkId id in Enum.GetValues(typeof(LandmarkId)))
            {
                var definition = LandmarkDefinitions.Get(id);
                buildings.Add(new BuildingKnowledge(BuildingType.Landmark, definition.Name,
                    new[] { definition.Name, id.ToString() }, definition.TargetAge,
                    new KnowledgeCost(definition.FoodCost, 0, definition.GoldCost),
                    definition.FootprintWidth, definition.FootprintHeight, definition.MaxHealth,
                    definition.ConstructionTicks, id));
            }
        }

        private static List<TechnologyKnowledge> BuildTechnologies(SimulationConfig c)
        {
            return new List<TechnologyKnowledge>
            {
                new TechnologyKnowledge(TechnologyType.BlacksmithDamage, "Blacksmith Damage", 2, new KnowledgeCost(0, 0, c.BlacksmithDamageCost), BuildingType.Blacksmith),
                new TechnologyKnowledge(TechnologyType.BlacksmithDefense, "Blacksmith Defense", 2, new KnowledgeCost(0, 0, c.BlacksmithDefenseCost), BuildingType.Blacksmith),
                new TechnologyKnowledge(TechnologyType.Ballistics, "Ballistics", 3, new KnowledgeCost(c.BallisticsFoodCost, 0, c.BallisticsGoldCost), BuildingType.University),
                new TechnologyKnowledge(TechnologyType.SiegeEngineering, "Siege Engineering", 3, new KnowledgeCost(c.SiegeEngineeringFoodCost, 0, c.SiegeEngineeringGoldCost), BuildingType.University),
                new TechnologyKnowledge(TechnologyType.Chemistry, "Chemistry", 3, new KnowledgeCost(c.ChemistryFoodCost, 0, c.ChemistryGoldCost), BuildingType.University),
                new TechnologyKnowledge(TechnologyType.MurderHoles, "Murder Holes", 3, new KnowledgeCost(c.MurderHolesFoodCost, 0, c.MurderHolesGoldCost), BuildingType.University)
            }.OrderBy(x => x.StableId, StringComparer.Ordinal).ToList();
        }

        private static List<CivilizationKnowledge> BuildCivilizations(List<UnitKnowledge> units, List<BuildingKnowledge> buildings)
        {
            var nativeUnits = units.Where(x => x.UnitType >= 0 && x.UnitType <= UnitData.KingUnitType
                    && x.UnitType != 5 && x.UnitType != UnitData.KingUnitType).ToArray();
            var civilizations = Enum.GetValues(typeof(Civilization)).Cast<Civilization>().ToArray();
            var variantTypes = new HashSet<int>();
            foreach (Civilization civ in civilizations)
                foreach (UnitKnowledge unit in nativeUnits)
                {
                    int resolved = GameSimulation.ResolveCanonicalCivilizationUnitType(civ, unit.UnitType);
                    if (resolved != unit.UnitType) variantTypes.Add(resolved);
                }
            var allBuildings = buildings.Where(x => Enum.IsDefined(typeof(BuildingType), x.BuildingType))
                .Select(x => x.StableId).ToArray();
            return civilizations.OrderBy(x => (int)x)
                .Select(x =>
                {
                    var available = new HashSet<string>(StringComparer.Ordinal);
                    foreach (UnitKnowledge unit in nativeUnits)
                    {
                        int resolved = GameSimulation.ResolveCanonicalCivilizationUnitType(x, unit.UnitType);
                        if (resolved == unit.UnitType && !variantTypes.Contains(unit.UnitType))
                            available.Add(unit.StableId);
                        else if (resolved != unit.UnitType)
                            available.Add("unit:" + resolved);
                    }
                    var availableBuildings = buildings.Where(b => Enum.IsDefined(typeof(BuildingType), b.BuildingType)
                        && (!b.Landmark.HasValue || LandmarkDefinitions.Get(b.Landmark.Value).Civ == x))
                        .Select(b => b.StableId).OrderBy(v => v, StringComparer.Ordinal).ToArray();
                    return new CivilizationKnowledge(x, available.OrderBy(v => v, StringComparer.Ordinal).ToArray(), availableBuildings);
                }).ToList();
        }
    }

    public sealed class EffectivePlayerKnowledge
    {
        public GameKnowledgeCatalog BaseCatalog { get; private set; }
        public Civilization Civilization { get; private set; }
        public int Age { get; private set; }
        private readonly IReadOnlyDictionary<int, KnowledgeCost> effectiveCosts;

        internal EffectivePlayerKnowledge(GameKnowledgeCatalog catalog, Civilization civ, int age,
            IReadOnlyDictionary<int, KnowledgeCost> effectiveCosts)
        {
            BaseCatalog = catalog; Civilization = civ; Age = age;
            this.effectiveCosts = new Dictionary<int, KnowledgeCost>(effectiveCosts ?? new Dictionary<int, KnowledgeCost>());
        }

        public UnitKnowledge GetUnit(string idOrAlias) => BaseCatalog.FindUnit(idOrAlias);
        public KnowledgeCost GetEffectiveUnitCost(string idOrAlias)
        {
            var unit = GetUnit(idOrAlias);
            if (unit == null) return null;
            KnowledgeCost cost;
            return effectiveCosts.TryGetValue(unit.UnitType, out cost) ? cost.Copy() : unit.Cost.Copy();
        }
    }

    /// <summary>Execution support is intentionally narrower than discoverable knowledge.</summary>
    public sealed class CommanderCapabilityCatalog
    {
        public bool CanExecuteUnit(int unitType) => CommanderIntentCatalog.IsSupportedUnit(unitType);
        public bool CanExecuteBuilding(BuildingType type) => CommanderIntentCatalog.IsSupportedStructure(type);
        public bool TryResolveUnit(GameKnowledgeCatalog catalog, string idOrAlias, out UnitKnowledge unit)
        {
            unit = catalog == null ? null : catalog.FindUnit(idOrAlias);
            return unit != null && CanExecuteUnit(unit.UnitType);
        }
    }

    public sealed class KnowledgeSlice
    {
        public IReadOnlyList<UnitKnowledge> Units { get; private set; }
        public IReadOnlyList<BuildingKnowledge> Buildings { get; private set; }
        public IReadOnlyList<TechnologyKnowledge> Technologies { get; private set; }

        internal KnowledgeSlice(List<UnitKnowledge> units, List<BuildingKnowledge> buildings, List<TechnologyKnowledge> technologies)
        {
            Units = Array.AsReadOnly((units ?? new List<UnitKnowledge>()).ToArray());
            Buildings = Array.AsReadOnly((buildings ?? new List<BuildingKnowledge>()).ToArray());
            Technologies = Array.AsReadOnly((technologies ?? new List<TechnologyKnowledge>()).ToArray());
        }

        public string ToDeterministicJson()
        {
            var sb = new StringBuilder("{\"units\":[");
            for (int i = 0; i < Units.Count; i++) { if (i > 0) sb.Append(','); AppendUnitJson(sb, Units[i]); }
            sb.Append("],\"buildings\":[");
            for (int i = 0; i < Buildings.Count; i++) { if (i > 0) sb.Append(','); AppendBuildingJson(sb, Buildings[i]); }
            sb.Append("],\"technologies\":[");
            for (int i = 0; i < Technologies.Count; i++) { if (i > 0) sb.Append(','); AppendTechnologyJson(sb, Technologies[i]); }
            return sb.Append("]}").ToString();
        }

        private static void AppendCost(StringBuilder sb, KnowledgeCost cost)
        {
            sb.Append("\",\"cost\":{\"food\":").Append(cost.Food)
              .Append(",\"wood\":").Append(cost.Wood).Append(",\"gold\":").Append(cost.Gold)
              .Append(",\"stone\":").Append(cost.Stone).Append("}");
        }
        private static void AppendUnitJson(StringBuilder sb, UnitKnowledge x)
        {
            sb.Append("{\"id\":\"").Append(x.StableId).Append("\",\"name\":\"").Append(x.DisplayName)
              .Append("\",\"age\":").Append(x.RequiredAge)
              .Append(",\"producer\":\"building:").Append((BuildingType)x.ProductionBuildingType);
            AppendCost(sb, x.Cost); sb.Append("}");
        }
        private static void AppendBuildingJson(StringBuilder sb, BuildingKnowledge x)
        {
            sb.Append("{\"id\":\"").Append(x.StableId).Append("\",\"name\":\"").Append(x.DisplayName)
              .Append("\",\"age\":").Append(x.RequiredAge);
            AppendCost(sb, x.Cost); sb.Append("}");
        }
        private static void AppendTechnologyJson(StringBuilder sb, TechnologyKnowledge x)
        {
            sb.Append("{\"id\":\"").Append(x.StableId).Append("\",\"name\":\"").Append(x.DisplayName)
              .Append("\",\"age\":").Append(x.RequiredAge)
              .Append(",\"researchBuilding\":\"building:").Append(x.ResearchBuilding);
            AppendCost(sb, x.Cost); sb.Append("}");
        }
    }
}
