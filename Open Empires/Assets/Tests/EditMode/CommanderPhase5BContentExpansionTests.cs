using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public sealed class CommanderPhase5BContentExpansionTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private GameKnowledgeCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            catalog = GameKnowledgeCatalog.Build(simulation);
        }

        [TearDown]
        public void TearDown()
        {
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void EveryOrdinaryRegisteredTrainableUnitHasCanonicalProducerAndIsRequestable()
        {
            int[] ordinaryTrainables = { 0, 1, 2, 3, 4, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
            foreach (int unitType in ordinaryTrainables)
            {
                Assert.IsTrue(catalog.Units.Any(x => x.UnitType == unitType), "missing catalog unit " + unitType);
                Assert.IsTrue(CommanderIntentCatalog.IsSupportedUnit(unitType), "not requestable " + unitType);
                Assert.IsTrue(simulation.TryGetProductionBuildingType(0, unitType, out _), "no native producer " + unitType);
            }

            Assert.IsFalse(CommanderIntentCatalog.IsSupportedUnit(5), "Sheep is not trainable content");
            Assert.IsFalse(CommanderIntentCatalog.IsSupportedUnit(UnitData.KingUnitType),
                "King has landmark-reward mechanics and must not be represented as ordinary training");
        }

        [Test]
        public void StandardConstructibleBuildingsHaveReadableCanonicalNamesAndExecutionSupport()
        {
            BuildingType[] ordinaryBuildings =
            {
                BuildingType.House, BuildingType.Barracks, BuildingType.TownCenter,
                BuildingType.Mill, BuildingType.LumberYard, BuildingType.Mine,
                BuildingType.ArcheryRange, BuildingType.Stables, BuildingType.Farm,
                BuildingType.Tower, BuildingType.Monastery, BuildingType.Blacksmith,
                BuildingType.Market, BuildingType.University, BuildingType.SiegeWorkshop,
                BuildingType.Keep
            };

            foreach (BuildingType type in ordinaryBuildings)
            {
                var building = catalog.FindBuilding("building:" + type);
                Assert.IsNotNull(building, "missing catalog building " + type);
                Assert.IsTrue(CommanderIntentCatalog.IsSupportedStructure(type), "not constructible through Commander " + type);
                Assert.IsTrue(!string.IsNullOrWhiteSpace(CommanderIntentCatalog.GetStructureDisplayName(type)),
                    "missing canonical label " + type);
            }

            Assert.AreEqual("Town Center", CommanderIntentCatalog.GetStructureDisplayName(BuildingType.TownCenter));
            Assert.AreEqual("Lumber Yard", CommanderIntentCatalog.GetStructureDisplayName(BuildingType.LumberYard));
            Assert.AreEqual("Archery Range", CommanderIntentCatalog.GetStructureDisplayName(BuildingType.ArcheryRange));
            Assert.AreEqual("Siege Workshop", CommanderIntentCatalog.GetStructureDisplayName(BuildingType.SiegeWorkshop));
            Assert.IsFalse(CommanderIntentCatalog.IsSupportedStructure(BuildingType.Wall));
            Assert.IsFalse(CommanderIntentCatalog.IsSupportedStructure(BuildingType.Landmark));
            Assert.IsFalse(CommanderIntentCatalog.IsSupportedStructure(BuildingType.Wonder));
        }

        [TestCase("longbowman", Civilization.English, 10)]
        [TestCase("gendarme", Civilization.French, 11)]
        [TestCase("landsknecht", Civilization.HolyRomanEmpire, 12)]
        public void UniqueCivilizationUnitsResolveToCanonicalAvailableRecords(string name, Civilization civ, int unitType)
        {
            simulation.SetPlayerCivilizations(new[] { civ });
            var runtimeCatalog = GameKnowledgeCatalog.Build(simulation);
            var unit = runtimeCatalog.FindUnit(name);

            Assert.IsNotNull(unit);
            Assert.AreEqual(unitType, unit.UnitType);
            var civKnowledge = runtimeCatalog.Civilizations.Single(x => x.Civilization == civ);
            Assert.IsTrue(civKnowledge.AvailableUnitIds.Contains(unit.StableId));
            Assert.IsTrue(CommanderIntentCatalog.IsSupportedUnit(unitType));
        }

        [Test]
        public void UnitAndBuildingNamesResolveThroughOneBoundedCanonicalResolver()
        {
            Type resolver = typeof(GameKnowledgeCatalog).Assembly.GetType("OpenEmpires.CommanderContentNameResolver");
            Assert.IsNotNull(resolver, "shared content resolver is required for typed and voice semantic requests");
            Assert.LessOrEqual(GameKnowledgeCatalog.MaxAliasLength, 64);

            var unitMethod = resolver.GetMethod("ResolveUnit", new[] { typeof(string), typeof(GameKnowledgeCatalog), typeof(Civilization) });
            var buildingMethod = resolver.GetMethod("ResolveBuilding", new[] { typeof(string), typeof(GameKnowledgeCatalog), typeof(Civilization) });
            Assert.IsNotNull(unitMethod, "unit resolver API missing");
            Assert.IsNotNull(buildingMethod, "building resolver API missing");
        }

        [Test]
        public void ResolverReturnsCanonicalCopiesAndBoundedTypoSuggestions()
        {
            var exact = CommanderContentNameResolver.ResolveUnit("spearmen", catalog, Civilization.English);
            Assert.AreEqual(CommanderContentResolutionStatus.Resolved, exact.Status);
            Assert.AreEqual(1, exact.Match.UnitType);
            Assert.AreNotSame(catalog.FindUnit("spearman"), exact.Match);

            var typo = CommanderContentNameResolver.ResolveUnit("longbowmna", catalog, Civilization.English);
            Assert.AreEqual(CommanderContentResolutionStatus.Unknown, typo.Status);
            Assert.That(typo.Suggestions.Any(x => x.UnitType == 10));
            Assert.LessOrEqual(typo.Suggestions.Count, CommanderContentNameResolver.MaximumSuggestions);
        }

        [Test]
        public void ResolverTreatsRolesAsSuggestionsAndDoesNotSilentlyChooseOrReplace()
        {
            var role = CommanderContentNameResolver.ResolveUnit("cavalry", catalog, Civilization.French);
            Assert.AreEqual(CommanderContentResolutionStatus.Ambiguous, role.Status);
            Assert.IsNull(role.Match);
            Assert.That(role.Suggestions.Any(x => x.UnitType == 3 || x.UnitType == 7 || x.UnitType == 11));

            var baseUnit = CommanderContentNameResolver.ResolveUnit("archer", catalog, Civilization.English);
            Assert.AreEqual(CommanderContentResolutionStatus.Resolved, baseUnit.Status);
            Assert.AreEqual(2, baseUnit.Match.UnitType);
            Assert.IsFalse(baseUnit.IsAvailableForCivilization);
            Assert.IsTrue(baseUnit.IsExecutable, "support and civ availability are separate facts");

            var unique = CommanderContentNameResolver.ResolveUnit("longbowman", catalog, Civilization.English);
            Assert.AreEqual(10, unique.Match.UnitType);
            Assert.IsTrue(unique.IsAvailableForCivilization);
            Assert.IsTrue(unique.IsExecutable);
        }

        [Test]
        public void CustomKnowledgeDiscoveryDoesNotBecomeAvailableOrExecutable()
        {
            var custom = new UnitKnowledge(99, "War Elephant", new[] { "elephant" }, 3,
                new KnowledgeCost(200, 0, 100), 500, (int)BuildingType.Stables, 300, 20, 1f, 1.5f, 2, 2, false);
            GameKnowledgeCatalog.RegisterCustomUnit(custom);
            try
            {
                var customCatalog = GameKnowledgeCatalog.Build(simulation);
                var result = CommanderContentNameResolver.ResolveUnit("elephant", customCatalog, Civilization.English);
                Assert.AreEqual(CommanderContentResolutionStatus.Resolved, result.Status);
                Assert.IsFalse(result.IsAvailableForCivilization);
                Assert.IsFalse(result.IsExecutable);
            }
            finally
            {
                GameKnowledgeCatalog.ResetCustomContent();
            }
        }

        [TestCase("Man-at-Arms", 6)]
        [TestCase("Crossbowman", 8)]
        [TestCase("Monk", 9)]
        [TestCase("Battering Ram", 13)]
        [TestCase("Mangonel", 14)]
        [TestCase("Trebuchet", 15)]
        [TestCase("Longbowman", 10)]
        [TestCase("Gendarme", 11)]
        [TestCase("Landsknecht", 12)]
        public void SemanticRequestParsesFullRosterByCanonicalName(string name, int expectedType)
        {
            var result = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"" + name + "\",\"count\":1}]}");
            Assert.IsTrue(result.IsValid, result.SafeExplanation);
            Assert.AreEqual(expectedType, result.Nodes[0].UnitType.Value);
        }

        [Test]
        public void SemanticProducerBindingAcceptsNativeFullRosterProducerPair()
        {
            const string json = "{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"BuildStructure\",\"structure\":\"Siege Workshop\",\"count\":1},"
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Mangonel\",\"count\":1,\"producerFromNode\":0}]}";
            var result = CommanderSemanticJson.Parse(json);
            Assert.IsTrue(result.IsValid, result.SafeExplanation);
            Assert.AreEqual(BuildingType.SiegeWorkshop, result.Nodes[0].BuildingType.Value);
            Assert.AreEqual(14, result.Nodes[1].UnitType.Value);
        }

        [Test]
        public void SemanticParserAcceptsNormalizedNamesButStillRejectsUnresolvedTypos()
        {
            var unit = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"long bowman\",\"count\":1}]}");
            Assert.IsTrue(unit.IsValid, unit.SafeExplanation);
            Assert.AreEqual(10, unit.Nodes[0].UnitType.Value);

            var building = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Lumber Yard\",\"count\":1}]}");
            Assert.IsTrue(building.IsValid, building.SafeExplanation);
            Assert.AreEqual(BuildingType.LumberYard, building.Nodes[0].BuildingType.Value);

            var unknown = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"dragon\",\"count\":1}]}");
            Assert.IsFalse(unknown.IsValid);
        }

        [Test]
        public void DuplicateContentAliasesReturnAmbiguousSuggestionsWithoutExecution()
        {
            GameKnowledgeCatalog.RegisterCustomBuilding(new BuildingKnowledge((BuildingType)999,
                "Barracks Impostor", new[] { "Barracks" }, 1, new KnowledgeCost(0, 0, 0), 2, 2, 100));
            try
            {
                var customCatalog = GameKnowledgeCatalog.Build(simulation);
                var result = CommanderContentNameResolver.ResolveBuilding("barracks", customCatalog, Civilization.English);
                Assert.AreEqual(CommanderContentResolutionStatus.Ambiguous, result.Status);
                Assert.IsNull(result.Match);
                Assert.AreEqual(2, result.Suggestions.Count);
                Assert.LessOrEqual(result.Suggestions.Count, CommanderContentNameResolver.MaximumSuggestions);
            }
            finally
            {
                GameKnowledgeCatalog.ResetCustomContent();
            }
        }

        [Test]
        public void NativeUniqueAndRewardUnitsAreUnavailableOutsideTheirCivilizationMechanic()
        {
            var english = GameKnowledgeCatalog.Build(simulation);
            var gendarme = CommanderContentNameResolver.ResolveUnit("gendarme", english, Civilization.English);
            Assert.IsFalse(gendarme.IsAvailableForCivilization);
            Assert.IsTrue(gendarme.IsExecutable, "canonical execution support is distinct from civ availability");

            var king = CommanderContentNameResolver.ResolveUnit("English King", english, Civilization.English);
            Assert.IsFalse(king.IsAvailableForCivilization, "landmark reward is not ordinary trainable availability");
            Assert.IsFalse(king.IsExecutable);

            simulation.SetPlayerCivilizations(new[] { Civilization.French });
            var french = GameKnowledgeCatalog.Build(simulation);
            Assert.IsTrue(CommanderContentNameResolver.ResolveUnit("Gendarme", french, Civilization.French)
                .IsAvailableForCivilization);
        }

        [Test]
        public void IdentityParsingIsSafeOffTheUnityMainThread()
        {
            var task = Task.Run(() => CommanderIntentCatalog.TryResolveUnit("spearman", out int unitType)
                && unitType == 1);
            Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(2)), "identity parsing unexpectedly blocked a worker thread");
            Assert.IsTrue(task.Result, "pure identity parsing should not call Unity object APIs");
        }

        [Test]
        public void CatalogLabelsComeFromTheCanonicalUnitInfoUiTables()
        {
            var ui = typeof(GameKnowledgeCatalog).Assembly.GetType("OpenEmpires.UnitInfoUI");
            Assert.IsNotNull(ui);
            var unitName = ui.GetMethod("GetUnitTypeDisplayName", BindingFlags.Public | BindingFlags.Static);
            var unitPlural = ui.GetMethod("GetUnitTypePluralName", BindingFlags.Public | BindingFlags.Static);
            var buildingName = ui.GetMethod("GetBuildingTypeDisplayName", BindingFlags.Public | BindingFlags.Static);
            var buildingPlural = ui.GetMethod("GetBuildingTypePluralName", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(unitName);
            Assert.IsNotNull(unitPlural);
            Assert.IsNotNull(buildingName);
            Assert.IsNotNull(buildingPlural);

            Assert.AreEqual("English King", unitName.Invoke(null, new object[] { UnitData.KingUnitType }));
            Assert.AreEqual("Landsknechte", unitPlural.Invoke(null, new object[] { 12 }));
            Assert.AreEqual("Lumber Yard", buildingName.Invoke(null, new object[] { BuildingType.LumberYard }));
            Assert.AreEqual("Lumber Yards", buildingPlural.Invoke(null, new object[] { BuildingType.LumberYard }));
            Assert.AreEqual("English King", CommanderIntentCatalog.GetUnitDisplayName(UnitData.KingUnitType));
            Assert.AreEqual("Lumber Yard", catalog.FindBuilding("LumberYard").DisplayName);
        }

        [Test]
        public void LumberCampAliasResolvesToCanonicalLumberYard()
        {
            Assert.IsTrue(CommanderIntentCatalog.TryResolveStructure("lumber camp", out BuildingType type));
            Assert.AreEqual(BuildingType.LumberYard, type);

            var result = CommanderContentNameResolver.ResolveBuilding("lumbercamp", catalog, Civilization.English);
            Assert.AreEqual(CommanderContentResolutionStatus.Resolved, result.Status);
            Assert.AreEqual(BuildingType.LumberYard, result.Match.BuildingType);
        }

        [Test]
        public void SheepIsKnownAsNativeWorldContentButNotAnOrdinaryTrainableUnit()
        {
            var sheep = catalog.FindUnit("Sheep");
            Assert.IsNotNull(sheep, "registered native unit identity should be discoverable");
            Assert.AreEqual(5, sheep.UnitType);
            Assert.AreEqual("Sheep", sheep.DisplayName);
            Assert.IsFalse(CommanderIntentCatalog.IsSupportedUnit(sheep.UnitType));

            var resolved = CommanderContentNameResolver.ResolveUnit("sheep", catalog, Civilization.English);
            Assert.AreEqual(CommanderContentResolutionStatus.Resolved, resolved.Status);
            Assert.IsFalse(resolved.IsExecutable);
            Assert.IsFalse(CommanderIntentCatalog.TryResolveUnit("sheep", out _));
        }

        [Test]
        public void FunctionalDropoffNamesUseNativeResourceAcceptanceWithoutGuessingGenericDropoff()
        {
            var wood = CommanderContentNameResolver.ResolveBuilding("wood dropoff", catalog, Civilization.English);
            Assert.AreEqual(CommanderContentResolutionStatus.Resolved, wood.Status);
            Assert.AreEqual(BuildingType.LumberYard, wood.Match.BuildingType);
            Assert.IsTrue(wood.IsExecutable);

            var generic = CommanderContentNameResolver.ResolveBuilding("dropoff building", catalog, Civilization.English);
            Assert.AreEqual(CommanderContentResolutionStatus.Ambiguous, generic.Status);
            Assert.IsNull(generic.Match);
            Assert.That(generic.Suggestions.Select(x => x.BuildingType), Does.Contain(BuildingType.TownCenter));
            Assert.That(generic.Suggestions.Select(x => x.BuildingType), Does.Contain(BuildingType.Mill));
            Assert.That(generic.Suggestions.Select(x => x.BuildingType), Does.Contain(BuildingType.Mine));
        }
    }
}
