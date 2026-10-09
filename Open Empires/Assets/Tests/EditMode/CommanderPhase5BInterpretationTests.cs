using System;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public sealed class CommanderPhase5BInterpretationTests
    {
        [Test]
        public void CivilizationReplacement_IsNamedInResponseAndPreview_NotSilentlyReportedAsBaseUnit()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            sim.SetPlayerCivilizations(new[] { Civilization.English });
            using (var manager = new CommanderGoalManager(sim, 0))
            {
                try
                {
                    var intent = new EnsureUnitCountIntent(0, 2, 1);
                    var resolution = new CommanderIntentResolver().Resolve(intent, sim, manager);
                    Assert.That(new CommanderResponseGenerator().GenerateResolutionResponse(resolution), Does.Contain("Longbowman"));
                    var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Archer\",\"count\":1}]}");
                    Assert.That(manager.PrepareActionPlan(parsed, "train an archer", 0).Preview, Does.Contain("Longbowman"));
                }
                finally { UnityEngine.Object.DestroyImmediate(config); }
            }
        }
    }
}
