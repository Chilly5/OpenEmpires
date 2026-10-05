using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    // One bounded runtime proof for the Phase 4G result handoff:
    // train three Spearmen from a Barracks, then patrol a worked goldmine using only those new units.
    public sealed class CommanderPhase4GResultBindingPlayModeTests
    {
        [UnityTest]
        public IEnumerator BuildProduceThenPatrol_UsesExactProducedSpearmen()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            CommanderGoalManager manager = null;
            try
            {
                int x = simulation.MapData.Width / 2;
                int z = simulation.MapData.Height / 2;
                typeof(MapData).GetField("holeMap", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(simulation.MapData, null);
                for (int tx = x - 30; tx <= x + 30; tx++)
                    for (int tz = z - 20; tz <= z + 20; tz++)
                    {
                        simulation.MapData.Tiles[tx, tz] = TileType.Grass;
                        simulation.MapData.ForestDensity[tx, tz] = 0;
                        simulation.MapData.FoundationCount[tx, tz] = 0;
                        simulation.FogOfWar.SetVisible(0, tx, tz);
                    }

                simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true)
                    .AutoProduceVillagers = false;
                simulation.CreateBuilding(0, BuildingType.Barracks, x - 6, z, false);
                ResourceNodeData gold = simulation.MapData.AddResourceNode(
                    ResourceType.Gold, simulation.MapData.TileToWorldFixed(x + 8, z), 10000);
                PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
                resources.Food = resources.Wood = resources.Gold = resources.Stone = 10000;
                for (int i = 0; i < 6; i++)
                {
                    UnitData villager = simulation.UnitRegistry.CreateUnit(0,
                        simulation.MapData.TileToWorldFixed(x - 5 + i, z + 4), Fixed32.One,
                        Fixed32.FromFloat(.4f), Fixed32.One);
                    villager.UnitType = 0;
                    villager.IsVillager = true;
                    villager.CurrentHealth = villager.MaxHealth = 100;
                    villager.State = UnitState.Idle;
                    if (i == 0) villager.TargetResourceNodeId = gold.Id;
                }

                manager = new CommanderGoalManager(simulation, 0);
                CommanderContext context = new CommanderContextBuilder().Build(simulation, manager);
                CommanderSemanticResult parsed = CommanderSemanticJson.Parse(
                    "{\"outcome\":\"Request\",\"nodes\":[" +
                    "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3}," +
                    "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":3," +
                    "\"location\":\"WorkedResource\",\"resource\":\"Gold\",\"dependsOn\":[0],\"resultFromNode\":0}]}" );
                Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
                Assert.That(CommanderSemanticGraphAdmission.TryAdmit(parsed, context,
                    out CommanderSemanticGraphPlan plan, out string admissionReason),
                    Is.True, admissionReason);
                IReadOnlyList<CommanderGoal> goals = manager.SubmitSemanticGraph(plan);
                var producedGoal = (EnsureUnitCountGoal)goals[0];
                var patrolGoal = (CommanderCapabilityGoal)goals[1];
                PatrolCommand? patrol = null;
                simulation.CommandBuffer.CommandEnqueued += (command, source) =>
                {
                    if (source == CommandEnqueueSource.Commander && command is PatrolCommand commandValue)
                        patrol = commandValue;
                };

                for (int i = 0; i < 3600 && patrol == null; i++)
                {
                    manager.Tick(simulation.CurrentTick);
                    simulation.Tick();
                    if (i % 30 == 0) yield return null;
                }

                Assert.That(producedGoal.Status, Is.EqualTo(CommanderGoalStatus.Completed), producedGoal.StatusReason);
                Assert.That(producedGoal.ResultUnitIds.Count, Is.EqualTo(3));
                Assert.That(patrol.HasValue, Is.True, patrolGoal.StatusReason);
                Assert.That(patrol.Value.UnitIds, Is.EqualTo(producedGoal.ResultUnitIds));
                Assert.That(patrolGoal.Status, Is.EqualTo(CommanderGoalStatus.Executing));
                Debug.Log($"[Phase4G Runtime] PASS exact result binding: tick={simulation.CurrentTick}, " +
                    $"spearmen={string.Join(",", producedGoal.ResultUnitIds)}, patrol={patrolGoal.Status}.");
            }
            finally
            {
                manager?.Dispose();
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
