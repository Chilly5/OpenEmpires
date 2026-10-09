using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public sealed class CommanderPhase5BProviderContextTests
    {
        [Test]
        public void WholeRequestRetrievesRelevantBuildingRatherThanFirstTwelveUnits()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            using (var manager = new CommanderGoalManager(new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>()), 0))
            {
                try
                {
                    var method = typeof(CommanderContextBuilder).GetMethod("Build", new[]
                        { typeof(GameSimulation), typeof(CommanderGoalManager), typeof(string) });
                    Assert.That(method, Is.Not.Null, "Context retrieval must receive the actual player request.");
                    var context = (CommanderContext)method.Invoke(new CommanderContextBuilder(), new object[]
                        { manager.Simulation, manager, "build a lumber camp near the woodline" });
                    var json = JObject.Parse(new CommanderSemanticProviderRequest("build a lumber camp near the woodline", context).SerializedContext);
                    Assert.That(json["knowledge"]["buildings"].Any(x => (string)x["id"] == "building:LumberYard"), Is.True);
                    Assert.That(json["knowledge"]["units"].Count() + json["knowledge"]["buildings"].Count(), Is.LessThanOrEqualTo(8));
                    Assert.That(json["structureCapabilities"].Values<string>(), Has.Member("LumberYard"));
                    Assert.That(json["unitCapabilities"].Values<string>(), Has.Member("Man-at-Arms"));
                }
                finally { UnityEngine.Object.DestroyImmediate(config); }
            }
        }
    }
}
