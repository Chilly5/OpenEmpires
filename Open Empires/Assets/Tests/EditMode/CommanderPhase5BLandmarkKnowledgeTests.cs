using System;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public sealed class CommanderPhase5BLandmarkKnowledgeTests
    {
        [Test]
        public void EveryNativeNamedLandmark_IsDiscoverableWithNativeFactsAndNotGenericExecutableSubstitution()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var catalog = GameKnowledgeCatalog.Build(new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>()));
                foreach (LandmarkId id in Enum.GetValues(typeof(LandmarkId)))
                {
                    var native = LandmarkDefinitions.Get(id);
                    var match = CommanderContentNameResolver.ResolveBuilding(native.Name, catalog, native.Civ);
                    Assert.That(match.Status, Is.EqualTo(CommanderContentResolutionStatus.Resolved), native.Name);
                    Assert.That(match.Match.StableId, Is.EqualTo("landmark:" + id));
                    Assert.That(match.Match.Copy().StableId, Is.EqualTo("landmark:" + id));
                    Assert.That(match.Match.Cost.Food, Is.EqualTo(native.FoodCost));
                    Assert.That(match.Match.Cost.Gold, Is.EqualTo(native.GoldCost));
                    Assert.That(match.Match.FootprintWidth, Is.EqualTo(native.FootprintWidth));
                    Assert.That(match.IsAvailableForCivilization, Is.True);
                    Assert.That(match.IsExecutable, Is.False, "Specific landmark mechanics are not generic building execution.");
                    var other = native.Civ == Civilization.English ? Civilization.French : Civilization.English;
                    Assert.That(CommanderContentNameResolver.ResolveBuilding(native.Name, catalog, other).IsAvailableForCivilization, Is.False);
                    var repair = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"RepairTarget\",\"unitSelector\":\"Villagers\",\"count\":1,\"location\":\"PlayerBase\",\"target\":{\"kind\":\"BuildingType\",\"structure\":\"" + native.Name + "\"}}]}");
                    Assert.That(repair.IsValid, Is.False, "A named landmark must not collapse to a generic Landmark selector.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }
    }
}
