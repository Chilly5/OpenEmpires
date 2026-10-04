using NUnit.Framework;
using System.Linq;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public sealed class CommanderPhase4FKnowledgeCatalogTests
    {
        private SimulationConfig config;
        private GameKnowledgeCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            catalog = GameKnowledgeCatalog.Build(config);
        }

        [TearDown]
        public void TearDown()
        {
            GameKnowledgeCatalog.ResetCustomContent();
            if (config != null) Object.DestroyImmediate(config);
        }

        [Test]
        public void CapabilityCatalog_MatchesAcceptedPhase4EExecutionPolicy()
        {
            var capability = new CommanderCapabilityCatalog();
            for (int type = 0; type <= 16; type++)
                Assert.AreEqual(CommanderIntentCatalog.IsSupportedUnit(type), capability.CanExecuteUnit(type), "unit " + type);
            foreach (BuildingType type in System.Enum.GetValues(typeof(BuildingType)))
                Assert.AreEqual(CommanderIntentCatalog.IsSupportedStructure(type), capability.CanExecuteBuilding(type), type.ToString());
        }

        [Test]
        public void Catalog_CollectionsCannotBeMutatedByConsumers()
        {
            var mutable = catalog.Units as System.Collections.Generic.IList<UnitKnowledge>;
            if (mutable != null) Assert.IsTrue(mutable.IsReadOnly, "Catalog exposes mutable backing list");
        }

        [Test]
        public void KnowledgeSlice_CollectionsCannotBeMutatedByConsumers()
        {
            var slice = catalog.Slice(string.Empty, 2);
            var mutable = slice.Units as System.Collections.Generic.IList<UnitKnowledge>;
            if (mutable != null) Assert.IsTrue(mutable.IsReadOnly, "Slice exposes mutable backing list");
        }

        [Test]
        public void ContextRecordLimit_AppliesAcrossAllCategories()
        {
            var slice = catalog.Slice(string.Empty, 2);
            Assert.LessOrEqual(slice.Units.Count + slice.Buildings.Count + slice.Technologies.Count, 2);
        }

        [Test]
        public void ContextSerialization_ContainsCanonicalFactsNotOnlyNames()
        {
            string json = catalog.Slice("Spearman").ToDeterministicJson();
            StringAssert.Contains("cost", json.ToLowerInvariant());
            StringAssert.Contains(config.SpearmanFoodCost.ToString(), json);
        }

        [Test]
        public void Catalog_UsesCanonicalSpearmanCostAndStableIdentity()
        {
            var spearman = catalog.FindUnit("spearmen");
            Assert.IsNotNull(spearman);
            Assert.AreEqual("unit:1", spearman.StableId);
            Assert.AreEqual(config.SpearmanFoodCost, spearman.Cost.Food);
            Assert.AreEqual(config.SpearmanWoodCost, spearman.Cost.Wood);
            Assert.AreEqual((int)BuildingType.Barracks, spearman.ProductionBuildingType);
        }

        [Test]
        public void Catalog_IsDeterministicallyOrderedAndBounded()
        {
            Assert.That(catalog.Units.Count, Is.LessThanOrEqualTo(GameKnowledgeCatalog.MaxContextRecords));
            for (int i = 1; i < catalog.Units.Count; i++)
                Assert.That(string.CompareOrdinal(catalog.Units[i - 1].StableId, catalog.Units[i].StableId), Is.LessThan(0));
            var slice = catalog.Slice("archer", 2);
            Assert.That(slice.Units.Count, Is.LessThanOrEqualTo(2));
            Assert.That(slice.ToDeterministicJson(), Is.EqualTo(slice.ToDeterministicJson()));
        }

        [Test]
        public void Catalog_ProjectionIsDetachedFromConfig()
        {
            int before = config.SpearmanFoodCost;
            var cost = catalog.FindUnit("Spearman").Cost;
            Assert.AreEqual(before, cost.Food);
            var copy = catalog.FindUnit("Spearman").Copy();
            Assert.AreNotSame(catalog.FindUnit("Spearman"), copy);
            Assert.AreNotSame(catalog.FindUnit("Spearman").Cost, copy.Cost);
            Assert.AreEqual(before, config.SpearmanFoodCost);
        }

        [Test]
        public void CapabilityCatalog_DoesNotGrantUnsupportedDiscoveredContent()
        {
            var capability = new CommanderCapabilityCatalog();
            Assert.IsNotNull(catalog.FindUnit("Spearman"));
            Assert.IsFalse(capability.CanExecuteUnit(13));
            UnitKnowledge ignored;
            Assert.IsFalse(capability.TryResolveUnit(catalog, "unit:13", out ignored));
        }

        [Test]
        public void Catalog_ExposesCanonicalAgeTechnologyAndResourceSources()
        {
            Assert.AreEqual(3, catalog.FindUnit("Knight").RequiredAge);
            Assert.AreEqual(3, catalog.FindTechnology("chemistry").RequiredAge);
            Assert.AreEqual(ResourceType.Food, catalog.Resources[0].ResourceType);
            Assert.AreEqual(3, catalog.Ages[2].Age);
        }

        [Test]
        public void CivilizationProjection_UsesCanonicalReplacementRules()
        {
            var english = catalog.Civilizations[0];
            Assert.IsFalse(english.AvailableUnitIds.Contains("unit:2"));
            Assert.IsTrue(english.AvailableUnitIds.Contains("unit:10"));
            var hre = catalog.Civilizations[2];
            Assert.IsFalse(hre.AvailableUnitIds.Contains("unit:1"));
            Assert.IsTrue(hre.AvailableUnitIds.Contains("unit:12"));
        }

        [Test]
        public void EffectiveProjection_UsesCanonicalCivilizationStateWithoutMutation()
        {
            var sim = new GameSimulation(config, 2, new[] { 0, 1 }, System.Array.Empty<int>());
            sim.SetPlayerCivilizations(new[] { Civilization.English, Civilization.French });
            var effective = GameKnowledgeCatalog.BuildEffective(sim, 0);
            Assert.AreEqual(Civilization.English, effective.Civilization);
            Assert.AreEqual(config.LongbowmanFoodCost, effective.GetEffectiveUnitCost("archer").Food);
            Assert.AreEqual(1, sim.GetPlayerAge(0));
        }

        [Test]
        public void EffectiveProjection_UsesSimulationCanonicalReplacementForHre()
        {
            var sim = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
            sim.SetPlayerCivilizations(new[] { Civilization.HolyRomanEmpire });
            var effective = GameKnowledgeCatalog.BuildEffective(sim, 0);
            Assert.AreEqual(config.LandsknechtFoodCost, effective.GetEffectiveUnitCost("spearman").Food);
            Assert.AreEqual(config.LandsknechtWoodCost, effective.GetEffectiveUnitCost("spearman").Wood);
        }

        [Test]
        public void SimulationCatalog_UsesCanonicalBuildingAndTechnologyQueries()
        {
            var sim = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
            var projected = GameKnowledgeCatalog.Build(sim);
            var barracks = projected.FindBuilding("barracks");
            Assert.AreEqual(sim.GetBuildingWoodCost(BuildingType.Barracks), barracks.Cost.Wood);
            Assert.AreEqual(sim.GetConstructionTicks(BuildingType.Barracks), barracks.ConstructionTicks);
            var chemistry = projected.FindTechnology("chemistry");
            sim.GetTechnologySpec(TechnologyType.Chemistry, out int age, out int food, out int gold, out BuildingType location, out int ticks);
            Assert.AreEqual(age, chemistry.RequiredAge);
            Assert.AreEqual(food, chemistry.Cost.Food);
            Assert.AreEqual(gold, chemistry.Cost.Gold);
            Assert.AreEqual(location, chemistry.ResearchBuilding);
            Assert.AreEqual(ticks, chemistry.ResearchTimeTicks);
        }

        [Test]
        public void SimulationCatalog_CivilizationProjectionUsesCanonicalResolver()
        {
            var sim = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
            var projected = GameKnowledgeCatalog.Build(sim);
            foreach (Civilization civ in System.Enum.GetValues(typeof(Civilization)))
            {
                var row = projected.Civilizations.Single(x => x.Civilization == civ);
                foreach (var unit in projected.Units)
                {
                    int resolved = sim.ResolveCivUnitType(civ, unit.UnitType);
                    Assert.IsTrue(row.AvailableUnitIds.Contains("unit:" + resolved), civ + "/unit:" + resolved);
                    if (resolved != unit.UnitType)
                        Assert.IsFalse(row.AvailableUnitIds.Contains(unit.StableId), civ + "/replaced/" + unit.StableId);
                }
            }
        }

        private sealed class SyntheticTestConfig : SimulationConfig
        {
            public int CustomSpearmanFoodCost = 60;
            public override int SpearmanFoodCost => CustomSpearmanFoodCost;

            public int CustomBarracksWoodCost = 150;
            public override int BarracksWoodCost => CustomBarracksWoodCost;

            public int CustomBallisticsFoodCost = 200;
            public override int BallisticsFoodCost => CustomBallisticsFoodCost;
        }

        [Test]
        public void CostMutation_PropagatesFromSyntheticCanonicalConfig()
        {
            var syntheticConfig = ScriptableObject.CreateInstance<SyntheticTestConfig>();
            try
            {
                syntheticConfig.CustomSpearmanFoodCost = 85;
                var cat1 = GameKnowledgeCatalog.Build(syntheticConfig);
                Assert.AreEqual(85, cat1.FindUnit("spearman").Cost.Food);

                syntheticConfig.CustomSpearmanFoodCost = 145;
                var cat2 = GameKnowledgeCatalog.Build(syntheticConfig);
                Assert.AreEqual(145, cat2.FindUnit("spearman").Cost.Food);
            }
            finally
            {
                Object.DestroyImmediate(syntheticConfig);
            }
        }

        [Test]
        public void BuildingDataMutation_PropagatesFromCanonicalSource()
        {
            var syntheticConfig = ScriptableObject.CreateInstance<SyntheticTestConfig>();
            try
            {
                syntheticConfig.CustomBarracksWoodCost = 310;
                var cat = GameKnowledgeCatalog.Build(syntheticConfig);
                Assert.AreEqual(310, cat.FindBuilding("barracks").Cost.Wood);
            }
            finally
            {
                Object.DestroyImmediate(syntheticConfig);
            }
        }

        [Test]
        public void TechnologyDataMutation_PropagatesFromCanonicalSource()
        {
            var syntheticConfig = ScriptableObject.CreateInstance<SyntheticTestConfig>();
            try
            {
                syntheticConfig.CustomBallisticsFoodCost = 480;
                var cat = GameKnowledgeCatalog.Build(syntheticConfig);
                Assert.AreEqual(480, cat.FindTechnology("ballistics").Cost.Food);
            }
            finally
            {
                Object.DestroyImmediate(syntheticConfig);
            }
        }

        [Test]
        public void Prerequisites_ExposesCanonicalAgeAndProducerRelationships()
        {
            var spearman = catalog.FindUnit("spearman");
            Assert.AreEqual(1, spearman.RequiredAge);
            Assert.AreEqual((int)BuildingType.Barracks, spearman.ProductionBuildingType);

            var knight = catalog.FindUnit("knight");
            Assert.AreEqual(3, knight.RequiredAge);
            Assert.AreEqual((int)BuildingType.Stables, knight.ProductionBuildingType);

            var barracks = catalog.FindBuilding("barracks");
            Assert.AreEqual(1, barracks.RequiredAge);

            var monastery = catalog.FindBuilding("monastery");
            Assert.AreEqual(3, monastery.RequiredAge);

            var chemistry = catalog.FindTechnology("chemistry");
            Assert.AreEqual(3, chemistry.RequiredAge);
            Assert.AreEqual(BuildingType.University, chemistry.ResearchBuilding);
        }

        [Test]
        public void NewContentDiscovery_DiscoversCustomUnitWithoutExecutionAuthority()
        {
            var customUnit = new UnitKnowledge(99, "War Elephant", new[] { "elephant" }, 3,
                new KnowledgeCost(200, 0, 100), 500, (int)BuildingType.Stables, 300, 20, 1f, 1.5f, 2, 2, false);
            GameKnowledgeCatalog.RegisterCustomUnit(customUnit);
            try
            {
                var customCat = GameKnowledgeCatalog.Build(config);
                var discovered = customCat.FindUnit("War Elephant");
                Assert.IsNotNull(discovered);
                Assert.AreEqual("unit:99", discovered.StableId);
                Assert.AreEqual(200, discovered.Cost.Food);
                Assert.AreEqual(100, discovered.Cost.Gold);

                var capability = new CommanderCapabilityCatalog();
                Assert.IsFalse(capability.CanExecuteUnit(99), "New content must not automatically be executable");
                Assert.IsFalse(capability.TryResolveUnit(customCat, "unit:99", out _), "New content must not resolve through capability");
            }
            finally
            {
                GameKnowledgeCatalog.ResetCustomContent();
            }
        }

        [Test]
        public void NewContentDiscovery_DiscoversCustomBuildingWithoutExecutionAuthority()
        {
            var customType = (BuildingType)999;
            var customBuilding = new BuildingKnowledge(customType, "Grand Monument", new[] { "grandmonument" }, 4,
                new KnowledgeCost(0, 1000, 1000, 1000), 5, 5, 2000, 1200);
            GameKnowledgeCatalog.RegisterCustomBuilding(customBuilding);
            try
            {
                var customCat = GameKnowledgeCatalog.Build(config);
                var discovered = customCat.FindBuilding("grandmonument");
                Assert.IsNotNull(discovered);
                Assert.AreEqual(customType, discovered.BuildingType);

                var capability = new CommanderCapabilityCatalog();
                Assert.IsFalse(capability.CanExecuteBuilding(customType), "New custom building is not supported executable structure");
            }
            finally
            {
                GameKnowledgeCatalog.ResetCustomContent();
            }
        }

        [Test]
        public void DuplicateStableIds_FailsClosed()
        {
            var duplicate = new UnitKnowledge(1, "Fake Spearman", new string[0], 1,
                new KnowledgeCost(10, 10, 0), 100, 1, 50, 5, 1f, 1f, 0, 0, false);
            GameKnowledgeCatalog.RegisterCustomUnit(duplicate);
            try
            {
                Assert.Throws<System.InvalidOperationException>(() => GameKnowledgeCatalog.Build(config));
            }
            finally
            {
                GameKnowledgeCatalog.ResetCustomContent();
            }
        }

        [Test]
        public void AliasCollision_FailsClosed()
        {
            var duplicateAlias = new UnitKnowledge(88, "Spear Duplicate", new[] { "spearmen" }, 1,
                new KnowledgeCost(10, 10, 0), 100, 1, 50, 5, 1f, 1f, 0, 0, false);
            GameKnowledgeCatalog.RegisterCustomUnit(duplicateAlias);
            try
            {
                var customCat = GameKnowledgeCatalog.Build(config);
                Assert.Throws<System.InvalidOperationException>(() => customCat.FindUnit("spearmen"));
            }
            finally
            {
                GameKnowledgeCatalog.ResetCustomContent();
            }
        }

        [Test]
        public void UnknownAndForgedIds_FailsClosed()
        {
            Assert.IsNull(catalog.FindUnit("unit:999999"));
            Assert.IsNull(catalog.FindBuilding("building:FakeBuilding"));
            Assert.IsNull(catalog.FindTechnology("technology:FakeTech"));
            Assert.IsNull(catalog.FindUnit(""));
            Assert.IsNull(catalog.FindUnit(new string('x', 2000)));

            var capability = new CommanderCapabilityCatalog();
            Assert.IsFalse(capability.TryResolveUnit(catalog, "unit:999999", out _));
        }

        [Test]
        public void EffectivePlayerKnowledge_AgeTransitionUpdatesWithoutStaleCache()
        {
            var sim = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
            sim.SetPlayerAge(0, 1);
            var eff1 = GameKnowledgeCatalog.BuildEffective(sim, 0);
            Assert.AreEqual(1, eff1.Age);

            sim.SetPlayerAge(0, 2);
            var eff2 = GameKnowledgeCatalog.BuildEffective(sim, 0);
            Assert.AreEqual(2, eff2.Age);
        }

        [Test]
        public void ResetAndCacheIsolation_MatchInstancesDoNotLeak()
        {
            var sim1 = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
            sim1.SetPlayerCivilizations(new[] { Civilization.English });
            sim1.SetPlayerAge(0, 1);
            var eff1 = GameKnowledgeCatalog.BuildEffective(sim1, 0);

            var sim2 = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
            sim2.SetPlayerCivilizations(new[] { Civilization.French });
            sim2.SetPlayerAge(0, 3);
            var eff2 = GameKnowledgeCatalog.BuildEffective(sim2, 0);

            Assert.AreEqual(Civilization.English, eff1.Civilization);
            Assert.AreEqual(1, eff1.Age);
            Assert.AreEqual(Civilization.French, eff2.Civilization);
            Assert.AreEqual(3, eff2.Age);
        }

        [Test]
        public void ContextSerialization_ContainsCanonicalPrerequisitesAndRemainsBounded()
        {
            var slice = catalog.Slice(string.Empty, 12);
            string json = slice.ToDeterministicJson();

            StringAssert.Contains("\"producer\":\"building:Barracks\"", json);
            StringAssert.Contains("\"age\":1", json);
            Assert.Less(json.Length, 8192);
        }
    }
}
