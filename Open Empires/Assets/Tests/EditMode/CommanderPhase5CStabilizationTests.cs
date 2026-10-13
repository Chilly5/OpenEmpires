using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public sealed class CommanderPhase5CStabilizationTests
    {
        [TestCase("Archery","building:ArcheryRange","Building","ArcheryRange")]
        [TestCase("Spearmen","unit:1","Unit","Spearman")]
        [TestCase("Longbowmen","unit:10","Unit","Longbowman")]
        public void ExactResolvedAliases_KeepCategoryAndCanonicalProviderToken(string alias,string id,string category,string token)
        {
            var config=ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var facts=JObject.Parse(CommanderKnowledgeRetrieval.Retrieve(GameKnowledgeCatalog.Build(config),"Build 3 "+alias,Civilization.English));
                var rows=facts["resolvedNames"] as JArray;
                Assert.That(rows,Is.Not.Null,"Exact alias resolution must be supplied, not an optional typo suggestion.");
                Assert.That(rows.OfType<JObject>().Any(r=>(string)r["id"]==id&&(string)r["category"]==category&&(string)r["canonicalToken"]==token),Is.True);
            }
            finally{UnityEngine.Object.DestroyImmediate(config);}
        }

        [Test]
        public void EveryNativeUiPlural_RetrievesItsPositivelyResolvedIdentity()
        {
            var config=ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var catalog=GameKnowledgeCatalog.Build(config);
                foreach(int type in KeybindManager.BindableUnitTypes)
                {
                    string plural=UnitInfoUI.GetUnitTypePluralName(type);
                    var facts=JObject.Parse(CommanderKnowledgeRetrieval.Retrieve(catalog,"Train 3 "+plural,Civilization.English));
                    Assert.That(facts["requiredCanonicalIds"].Values<string>(),Does.Contain("unit:"+type),plural);
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(config);}
        }

        [Test]
        public void LandmarkRewardIdentity_IsKnownForItsNativeCivilizationWithoutInventingAdapter()
        {
            var config=ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var catalog=GameKnowledgeCatalog.Build(config);
                Assert.That(catalog.Civilizations.Single(c=>c.Civilization==Civilization.English).AvailableUnitIds,Does.Contain("unit:"+UnitData.KingUnitType));
                Assert.That(CommanderContentNameResolver.ResolveUnit("king",catalog,Civilization.English).IsExecutable,Is.False);
            }
            finally{UnityEngine.Object.DestroyImmediate(config);}
        }

        [Test]
        public void EveryNativeIdentityAndCivilization_HasPackedAvailabilityAndProducerFacts()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
                using var manager = new CommanderGoalManager(simulation, 0);
                var catalog = GameKnowledgeCatalog.Build(simulation);
                Assert.That(catalog.Units.Select(u => u.UnitType), Is.SupersetOf(KeybindManager.BindableUnitTypes));
                Assert.That(catalog.Buildings.Select(b => b.BuildingType).Distinct(), Is.EquivalentTo(Enum.GetValues(typeof(BuildingType))));
                foreach (Civilization civ in Enum.GetValues(typeof(Civilization)))
                {
                    simulation.SetPlayerCivilizations(new[] { civ, Civilization.English });
                    foreach (int age in new[] { 1, 3 })
                    {
                        simulation.SetPlayerAge(0, age);
                        foreach (var unit in catalog.Units.Where(u => GameSimulation.TryGetCanonicalProductionBuildingType(u.UnitType, out _)))
                        {
                            string message = "Train " + unit.DisplayName;
                            var context = new CommanderContextBuilder().Build(simulation, manager, message);
                            Assert.That(context.PlayerId, Is.EqualTo(0));
                            Assert.That(context.Age, Is.EqualTo(age));
                            Assert.That(context.Civilization, Is.EqualTo(civ.ToString()));
                            var facts = JObject.Parse(context.KnowledgeContext);
                            var row = ((JArray)facts["units"]).OfType<JObject>().Single(u => (string)u["id"] == unit.StableId);
                            Assert.That((bool)row["availableForCivilization"], Is.EqualTo(catalog.Civilizations.Single(c => c.Civilization == civ).AvailableUnitIds.Contains(unit.StableId)), unit.StableId + "/" + civ);
                            Assert.That(((JArray)facts["buildings"]).OfType<JObject>().Any(b => (string)b["id"] == "building:" + (BuildingType)unit.ProductionBuildingType), Is.True, "Missing producer for " + unit.StableId);
                            Assert.That(CommanderSemanticRequestPack.TryBuild(new CommanderSemanticProviderRequest(message, context), "fixture", 4096, out var pack, out var reason), Is.True, reason);
                            string payload = (string)JObject.Parse(pack.Json)["messages"][1]["content"];
                            Assert.That(payload, Does.Contain("unitCapabilities"));
                            Assert.That(payload, Does.Contain("structureCapabilities"));
                            Assert.That(payload, Does.Contain(unit.StableId));
                        }
                        foreach (var building in catalog.Buildings)
                        {
                            var context=new CommanderContextBuilder().Build(simulation,manager,"Build "+building.DisplayName);
                            var row=((JArray)JObject.Parse(context.KnowledgeContext)["buildings"]).OfType<JObject>()
                                .Single(b=>(string)b["id"]==building.StableId);
                            Assert.That((bool)row["availableForCivilization"],Is.EqualTo(catalog.Civilizations.Single(c=>c.Civilization==civ).AvailableBuildingIds.Contains(building.StableId)),building.StableId+"/"+civ);
                            Assert.That((bool)row["commanderExecutable"],Is.EqualTo(CommanderIntentCatalog.IsSupportedStructure(building.BuildingType)));
                        }
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [TestCase("Build house")]
        [TestCase("Build 3 stables")]
        [TestCase("Build 3 archery")]
        [TestCase("Build a Mill and 8 Farms")]
        [TestCase("Build 50 spearman out of 5 barracks")]
        public void ProductionPrompt_KeepsCapabilityContractConsistent(string message)
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var simulation = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                using var manager = new CommanderGoalManager(simulation, 0);
                var context = new CommanderContextBuilder().Build(simulation, manager, message);
                Assert.That(CommanderSemanticRequestPack.TryBuild(new CommanderSemanticProviderRequest(message, context), "fixture", 4096, out var pack, out var reason), Is.True, reason);
                var body = JObject.Parse(pack.Json);
                string contract = (string)body["messages"][0]["content"];
                string payload = (string)body["messages"][1]["content"];
                Assert.That(payload, Does.Contain("structureCapabilities"));
                Assert.That(contract, Does.Not.Contain("ordinary placed BuildStructure count is exactly 1; any requested repeated placed construction"));
                if (message.Contains("archery"))
                    Assert.That(JObject.Parse(context.KnowledgeContext)["requiredCanonicalIds"].Values<string>(), Does.Contain("building:ArcheryRange"));
                if (message.Contains("out of"))
                {
                    Assert.That(contract, Does.Contain("select-structures"), "Existing producer count needs an offered exact source restriction.");
                    Assert.That(contract, Does.Not.Contain("produce.producers=exact new build result"), "An existing producer restriction cannot require new construction.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        internal const string MissingBuildLocation = "{\"outcome\":\"Clarify\",\"message\":\"Where should I build it?\",\"pending\":{\"executionOrder\":\"Independent\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"LumberYard\",\"count\":1,\"builders\":{\"state\":\"Eligible\",\"count\":1},\"placement\":{\"relation\":\"Near\"}}]},\"missingSlots\":[{\"node\":0,\"path\":\"placement.anchor\",\"type\":\"Location\",\"alternatives\":[\"PlayerMarker\",\"MyTownCenter\"]}]}";

        [Test]
        public void LocationClarification_RetainsCompleteTypedBuildAndResolvesOnlyLocation()
        {
            var parsed = CommanderSemanticJson.ParseProviderResponse(MissingBuildLocation);
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            Assert.That(parsed.PendingGraphDraft, Is.Not.Null);
            var result = parsed.PendingGraphDraft.Resolve("PlayerMarker");
            Assert.That(result.IsValid, Is.True, result.SafeExplanation);
            Assert.That(result.Nodes.Single().BuildingType, Is.EqualTo(BuildingType.LumberYard));
            Assert.That(result.Nodes.Single().Count, Is.EqualTo(1));
            Assert.That(result.Nodes.Single().PlacementAnchorSelector.ToString(), Is.EqualTo("PlayerMarker"));
            Assert.That(parsed.PendingGraphDraft.Resolve("yes"), Is.Null, "Location is not approval.");
        }

        [Test]
        public void NewMatchAndCivilizationChanges_RefreshDetachedContextWithoutEnemyOwnership()
        {
            var config=ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var first=new GameSimulation(config,2,new[]{0,1},Array.Empty<int>());
                first.SetPlayerCivilizations(new[]{Civilization.English,Civilization.French});
                using var manager=new CommanderGoalManager(first,0);
                var early=new CommanderContextBuilder().Build(first,manager,"Longbowman Archery Range");
                first.SetPlayerAge(1,4);first.SetPlayerAge(0,3);
                first.SetPlayerCivilizations(new[]{Civilization.French,Civilization.English});
                var middle=new CommanderContextBuilder().Build(first,manager,"Longbowman Archery Range");
                Assert.That(early.PlayerId,Is.EqualTo(0));Assert.That(middle.PlayerId,Is.EqualTo(0));
                Assert.That(early.Age,Is.EqualTo(1));Assert.That(middle.Age,Is.EqualTo(3));
                Assert.That(early.Civilization,Is.EqualTo("English"));Assert.That(middle.Civilization,Is.EqualTo("French"));
                var next=new GameSimulation(config,2,new[]{0,1},Array.Empty<int>());
                using var nextManager=new CommanderGoalManager(next,0);
                Assert.That(new CommanderContextBuilder().Build(next,nextManager,"Longbowman").Age,Is.EqualTo(1));
                Assert.That(early.KnowledgeContext,Is.Not.EqualTo(middle.KnowledgeContext));
            }
            finally{UnityEngine.Object.DestroyImmediate(config);}
        }
    }
}
