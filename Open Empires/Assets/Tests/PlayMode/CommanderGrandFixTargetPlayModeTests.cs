using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixTargetPlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation sim;
        private CommanderGoalManager manager;
        private CommanderIntentDispatcher dispatcher;
        private int x, z;

        [SetUp] public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            sim = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            sim.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            x = sim.MapData.Width / 2; z = sim.MapData.Height / 2;
            // Legitimate initial terrain fixture, before submission. No forced
            // completion/health/target writes after an order has been accepted.
            typeof(MapData).GetField("holeMap", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sim.MapData, null);
            for (int tx = x - 20; tx <= x + 20; tx++)
                for (int tz = z - 20; tz <= z + 20; tz++)
                {
                    sim.MapData.Tiles[tx, tz] = TileType.Grass;
                    sim.MapData.ForestDensity[tx, tz] = 0; sim.MapData.FoundationCount[tx, tz] = 0;
                    sim.FogOfWar.SetVisible(0, tx, tz);
                }
            var resources = sim.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 10000;
            manager = new CommanderGoalManager(sim, 0);
            dispatcher = new CommanderIntentDispatcher(sim, manager);
        }

        [TearDown] public void TearDown()
        { dispatcher.Dispose(); manager.Dispose(); UnityEngine.Object.DestroyImmediate(config); }

        private UnitData InitialUnit(int player, int type, int dx, int dz)
            => (UnitData)typeof(GameSimulation).GetMethod("CreateTrainedUnit", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(sim, new object[] { player, type, sim.MapData.TileToWorldFixed(x + dx, z + dz) });

        private void Submit(string action, string actor, string target, string location)
        {
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"" + action
                + "\",\"unitSelector\":\"" + actor + "\",\"count\":1,\"location\":\"" + location + "\",\"target\":" + target + "}]}");
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            Assert.That(CommanderSemanticAdmission.TryCreateTacticalIntent(parsed.Nodes[0],
                new CommanderContextBuilder().Build(sim, manager), out var intent, out var reason), Is.True, reason);
            Assert.That(dispatcher.SubmitIntent(intent).CreatedGoal, Is.True);
        }

        [UnityTest] public IEnumerator NamedTownCenterRepair_UsesOrdinaryNativeRepairNotDamagedHouse()
        {
            var house = sim.CreateBuilding(0, BuildingType.House, x - 8, z, false, true); house.CurrentHealth -= 20;
            var tc = sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true); tc.CurrentHealth -= 20;
            tc.AutoProduceVillagers = false;
            InitialUnit(0, 0, -1, 1);
            int beforeTC = tc.CurrentHealth, beforeHouse = house.CurrentHealth;
            RepairBuildingCommand? issued = null;
            sim.CommandBuffer.CommandEnqueued += (command, source) =>
            { if (source == CommandEnqueueSource.Commander && command is RepairBuildingCommand repair) issued = repair; };
            Submit("RepairTarget", "Villagers", "{\"kind\":\"BuildingType\",\"structure\":\"TownCenter\"}", "PlayerBase");
            for (int i = 0; i < 800 && tc.CurrentHealth == beforeTC; i++)
            { manager.Tick(sim.CurrentTick); sim.Tick(); if (i % 20 == 0) yield return null; }
            Assert.That(issued.HasValue, Is.True);
            Assert.That(issued.Value.TargetBuildingId, Is.EqualTo(tc.Id));
            Assert.That(tc.CurrentHealth, Is.GreaterThan(beforeTC), "Actual native repair must increase requested TC health.");
            Assert.That(house.CurrentHealth, Is.EqualTo(beforeHouse));
        }

        [UnityTest] public IEnumerator NamedArcherAttack_UsesOrdinaryNativeCombatNotNearerVillager()
        {
            var tc = sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
            tc.AutoProduceVillagers = false; tc.AttackDamage = 0; // isolate the requested unit's native attack
            var attacker = InitialUnit(0, 1, 5, 6);
            var wrong = InitialUnit(1, 0, 6, 6);
            var archer = InitialUnit(1, 2, 10, 6);
            int beforeArcher = archer.CurrentHealth, beforeWrong = wrong.CurrentHealth;
            AttackUnitCommand? issued = null;
            sim.CommandBuffer.CommandEnqueued += (command, source) =>
            { if (source == CommandEnqueueSource.Commander && command is AttackUnitCommand attack) issued = attack; };
            Submit("AttackTarget", "Spearman", "{\"kind\":\"UnitType\",\"unit\":\"Archer\"}", "VisibleEnemy");
            for (int i = 0; i < 500 && archer.CurrentHealth == beforeArcher; i++)
            { manager.Tick(sim.CurrentTick); sim.Tick(); if (i % 20 == 0) yield return null; }
            Assert.That(issued.HasValue, Is.True);
            Assert.That(issued.Value.TargetUnitId, Is.EqualTo(archer.Id));
            Assert.That(issued.Value.UnitIds, Is.EqualTo(new[] { attacker.Id }));
            Assert.That(archer.CurrentHealth, Is.LessThan(beforeArcher), "Actual native combat must damage the requested Archer.");
            Assert.That(wrong.CurrentHealth, Is.EqualTo(beforeWrong));
        }
    }
}
