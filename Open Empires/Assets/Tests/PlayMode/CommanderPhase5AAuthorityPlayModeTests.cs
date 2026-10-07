using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5A")]
    public sealed class CommanderPhase5AAuthorityPlayModeTests
    {
        // This invokes the real bootstrap Update initialization and subsequent ticking.
        // It does not manufacture a StrategicPipeline or fire a replacement startup trigger.
        [UnityTest]
        public IEnumerator QualifyingBootstrapStartup_AndLaterEmergencyRemainAdvisory()
        {
            var previous = GameBootstrapper.Instance;
            var singleton = typeof(GameBootstrapper).GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static);
            var previousAnalytics = SinglePlayerAnalytics.Instance;
            SimulationConfig config = ScriptableObject.CreateInstance<SimulationConfig>();
            GameObject host = null;
            try
            {
                Assert.That(UnityEngine.Object.FindFirstObjectByType<GameSetup>(), Is.Null,
                    "This controlled bootstrap fixture requires the test runner's empty scene.");
                singleton.SetValue(null, null);
                host = new GameObject("Phase5AActualBootstrap");
                var bootstrap = host.AddComponent<GameBootstrapper>();
                bootstrap.enabled = false;
                Set(bootstrap, "config", config);
                Set(bootstrap, "tickAccumulator", -1000f); // Build fixture before the first gameplay tick.
                bootstrap.SetPlayerCount(2);
                bootstrap.SetAIPlayerIds(Array.Empty<int>());
                bootstrap.SetTeamAssignments(new[] { 0, 1 });
                bootstrap.SetCivilizations(new[] { Civilization.French, Civilization.French });
                Update(bootstrap);
                Assert.That(bootstrap.Simulation, Is.Not.Null);
                Assert.That(bootstrap.CommanderStrategicPipeline.LastContext, Is.Null);
                var sim = bootstrap.Simulation;
                var resources = sim.ResourceManager.GetPlayerResources(0);
                resources.Food = resources.Wood = resources.Gold = resources.Stone = 5000;
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                for (int tx = x - 20; tx <= x + 20; tx++)
                    for (int tz = z - 10; tz <= z + 10; tz++)
                    {
                        sim.MapData.Tiles[tx, tz] = TileType.Grass;
                        sim.MapData.ForestDensity[tx, tz] = 0;
                        sim.MapData.FoundationCount[tx, tz] = 0;
                        sim.FogOfWar.SetVisible(0, tx, tz);
                    }
                sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true)
                    .AutoProduceVillagers = false;
                sim.CreateBuilding(0, BuildingType.House, x + 12, z, false);
                sim.CreateBuilding(0, BuildingType.House, x + 18, z, false);
                // Creation events may coalesce into the real pending startup trigger;
                // no explicit player request or manual strategic trigger is introduced.
                Set(bootstrap, "tickAccumulator", config.SecondsPerTick);
                Update(bootstrap);
                var pipeline = bootstrap.CommanderStrategicPipeline;
                Assert.That(pipeline.LastRecommendations, Is.Not.Empty);
                Assert.That(pipeline.LastDecision.HasSelection, Is.True);
                Assert.That(pipeline.LastSubmission?.CreatedPlan ?? false, Is.False);
                Assert.That(bootstrap.StrategicCommander.Plans, Is.Empty);
                Assert.That(bootstrap.StrategicCommander.Reservations, Is.Empty);
                Assert.That(bootstrap.Commander.Goals, Is.Empty);
                Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
                Debug.Log("[Phase5A] actual bootstrap qualifying human: advisory exists; plans/goals/reservations/commands=0.");

                var direct = bootstrap.CommanderDispatcher.SubmitText("prepare cavalry attack");
                Assert.That(direct.CreatedPlan, Is.True);
                int plans = bootstrap.StrategicCommander.Plans.Count;
                int goals = bootstrap.Commander.Goals.Count;
                pipeline.EvaluateNow(StrategicEvaluationTriggerType.Emergency,
                    "Later qualifying human emergency with active authorized root.");
                Assert.That(bootstrap.StrategicCommander.Plans.Count, Is.EqualTo(plans));
                Assert.That(bootstrap.Commander.Goals.Count, Is.EqualTo(goals));
                Assert.That(direct.StrategicSubmission.Plan.IsTerminal, Is.False);
                Debug.Log("[Phase5A] authorized direct root survives later emergency advisory with zero recommendation-attributable goal/plan deltas.");
                yield return null;
            }
            finally
            {
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                singleton.SetValue(null, previous);
                if (previousAnalytics == null && SinglePlayerAnalytics.Instance != null)
                    UnityEngine.Object.DestroyImmediate(SinglePlayerAnalytics.Instance.gameObject);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        private static void Set(object instance, string name, object value) => instance.GetType()
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, value);
        private static void Update(GameBootstrapper instance) => typeof(GameBootstrapper)
            .GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, null);
    }
}
