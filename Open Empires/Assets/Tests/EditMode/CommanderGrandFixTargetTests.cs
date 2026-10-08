using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderGrandFixOffline")]
    public sealed class CommanderGrandFixTargetTests
    {
        private SimulationConfig config;
        private GameSimulation sim;
        private CommanderGoalManager manager;
        private int x, z;

        [SetUp] public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            sim = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            sim.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            manager = new CommanderGoalManager(sim, 0);
            x = sim.MapData.Width / 2; z = sim.MapData.Height / 2;
            for (int tx = x - 20; tx <= x + 20; tx++)
                for (int tz = z - 20; tz <= z + 20; tz++)
                { sim.MapData.Tiles[tx, tz] = TileType.Grass; sim.FogOfWar.SetVisible(0, tx, tz); }
        }

        [TearDown] public void TearDown()
        { manager.Dispose(); UnityEngine.Object.DestroyImmediate(config); }

        private BuildingData Building(BuildingType type, int dx, int player = 0)
            => sim.CreateBuilding(player, type, x + dx, z, false, true);

        private UnitData Unit(int type, int dx, int player = 0)
        {
            var unit = sim.UnitRegistry.CreateUnit(player, sim.MapData.TileToWorldFixed(x + dx, z + 1),
                Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
            unit.UnitType = type; unit.IsVillager = type == 0;
            unit.CurrentHealth = unit.MaxHealth = 100;
            return unit;
        }

        private static string Json(string action, string actor, string target, string location = null)
            => "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"" + action
                + "\",\"unitSelector\":\"" + actor + "\",\"count\":1,\"location\":\""
                + (location ?? (action == "RepairTarget" ? "PlayerBase" : "VisibleEnemy"))
                + "\",\"target\":" + target + "}]}";

        private CapabilityActionIntent Intent(string action, string actor, string target)
        {
            var parsed = CommanderSemanticJson.Parse(Json(action, actor, target));
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var context = new CommanderContextBuilder().Build(sim, manager);
            Assert.That(CommanderSemanticAdmission.TryCreateTacticalIntent(parsed.Nodes[0], context,
                out CommanderIntent intent, out string reason), Is.True, reason);
            return (CapabilityActionIntent)intent;
        }

        [Test] public void RepairNamedTownCenter_DoesNotChooseEarlierDamagedHouse()
        {
            var house = Building(BuildingType.House, -8); house.CurrentHealth = 10;
            var tc = Building(BuildingType.TownCenter, 0); tc.CurrentHealth = 20;
            var worker = Unit(0, 1);
            var intent = Intent("RepairTarget", "Villagers", "{\"kind\":\"BuildingType\",\"structure\":\"TownCenter\"}");
            Assert.That(new CommanderCapabilityExecutor(sim).TryCreateCommand(intent, out var command, out var reason), Is.True, reason);
            var repair = (RepairBuildingCommand)command;
            Assert.That(repair.TargetBuildingId, Is.EqualTo(tc.Id));
            Assert.That(repair.UnitIds, Is.EqualTo(new[] { worker.Id }));
        }

        [Test] public void AttackNamedArchers_DoesNotChooseNearerDifferentTypeOrChangeActor()
        {
            Building(BuildingType.TownCenter, 0);
            var attacker = Unit(1, 1);
            Unit(0, 3, 1);
            var archer = Unit(2, 8, 1);
            var intent = Intent("AttackTarget", "Spearman", "{\"kind\":\"UnitType\",\"unit\":\"Archer\"}");
            Assert.That(new CommanderCapabilityExecutor(sim).TryCreateCommand(intent, out var command, out var reason), Is.True, reason);
            var attack = (AttackUnitCommand)command;
            Assert.That(attack.TargetUnitId, Is.EqualTo(archer.Id));
            Assert.That(attack.UnitIds, Is.EqualTo(new[] { attacker.Id }));
        }

        [TestCase("missing")][TestCase("repaired")][TestCase("destroyed")][TestCase("foreign")]
        public void NamedRepairUnavailable_DoesNotSubstituteDamagedHouse(string loss)
        {
            var house = Building(BuildingType.House, -8); house.CurrentHealth = 10;
            if (loss != "missing")
            {
                var tc = Building(BuildingType.TownCenter, 0, loss == "foreign" ? 1 : 0);
                tc.CurrentHealth = loss == "repaired" ? tc.MaxHealth : loss == "destroyed" ? 0 : 20;
            }
            Unit(0, 1);
            var intent = Intent("RepairTarget", "Villagers", "{\"kind\":\"BuildingType\",\"structure\":\"TownCenter\"}");
            Assert.That(new CommanderCapabilityExecutor(sim).TryCreateCommand(intent, out var command, out _), Is.False);
            Assert.That(command, Is.Null);
        }

        [Test] public void MultipleTownCenters_SelectLowestOwnedIdBeforeDamageEligibility()
        {
            var first = Building(BuildingType.TownCenter, -8);
            var second = Building(BuildingType.TownCenter, 8); second.CurrentHealth = 10;
            Unit(0, 0);
            var intent = Intent("RepairTarget", "Villagers", "{\"kind\":\"BuildingType\",\"structure\":\"TownCenter\"}");
            var executor = new CommanderCapabilityExecutor(sim);
            Assert.That(executor.TryCreateCommand(intent, out _, out _), Is.False,
                "An already repaired first Town Center is not silently replaced by another.");
            first.CurrentHealth = 20;
            Assert.That(executor.TryCreateCommand(intent, out var command, out _), Is.True);
            Assert.That(((RepairBuildingCommand)command).TargetBuildingId, Is.EqualTo(first.Id));
        }

        [TestCase("missing")][TestCase("dead")][TestCase("hidden")][TestCase("ally")][TestCase("self")]
        public void NamedEnemyUnavailable_DoesNotSubstituteOtherEnemyTypeOrBuilding(string loss)
        {
            Building(BuildingType.TownCenter, 0); Unit(1, 1); Unit(0, 3, 1);
            Building(BuildingType.House, 12, 1);
            if (loss != "missing")
            {
                var archer = Unit(2, loss == "hidden" ? 30 : 8, loss == "self" ? 0 : 1);
                if (loss == "dead") { archer.CurrentHealth = 0; archer.State = UnitState.Dead; }
                if (loss == "ally") sim.SetTeamAssignments(new[] { 0, 0 });
            }
            var intent = Intent("AttackTarget", "Spearman", "{\"kind\":\"UnitType\",\"unit\":\"Archer\"}");
            Assert.That(new CommanderCapabilityExecutor(sim).TryCreateCommand(intent, out var command, out _), Is.False);
            Assert.That(command, Is.Null);
        }

        [Test] public void NamedEnemyBuilding_DoesNotSubstituteVisibleEnemyUnit()
        {
            Building(BuildingType.TownCenter, 0); Unit(1, 1); Unit(0, 3, 1);
            var barracks = Building(BuildingType.Barracks, 9, 1);
            var intent = Intent("AttackTarget", "Spearman", "{\"kind\":\"BuildingType\",\"structure\":\"Barracks\"}");
            Assert.That(new CommanderCapabilityExecutor(sim).TryCreateCommand(intent, out var command, out _), Is.True);
            Assert.That(((AttackBuildingCommand)command).TargetBuildingId, Is.EqualTo(barracks.Id));
        }

        [TestCase("RepairTarget", "Villagers", "{\"kind\":\"UnitType\",\"unit\":\"Archer\"}", "PlayerBase")]
        [TestCase("MoveUnits", "Military", "{\"kind\":\"BuildingType\",\"structure\":\"TownCenter\"}", "PlayerBase")]
        [TestCase("AttackTarget", "Spearman", "{\"kind\":\"UnitType\",\"unit\":\"Archer\"}", "PlayerBase")]
        [TestCase("AttackTarget", "Spearman", "{\"kind\":\"UnitType\",\"unit\":\"Archer\",\"id\":7}", "VisibleEnemy")]
        [TestCase("AttackTarget", "Spearman", "{\"kind\":\"UnitType\",\"unit\":\"Unknown\"}", "VisibleEnemy")]
        [TestCase("AttackTarget", "Spearman", "{\"kind\":\"SelectedGroup\"}", "VisibleEnemy")]
        public void IncompatibleOrUntrustedTarget_IsRejectedBeforeGoals(string action, string actor, string target, string location)
        {
            Assert.That(CommanderSemanticJson.Parse(Json(action, actor, target, location)).IsValid, Is.False);
            Assert.That(manager.Goals, Is.Empty); Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test] public void TargetSurvivesDtoRoundTripAndPreviewAndChangesAuthorizationScope()
        {
            var tc = Intent("RepairTarget", "Villagers", "{\"kind\":\"BuildingType\",\"structure\":\"TownCenter\"}");
            var house = Intent("RepairTarget", "Villagers", "{\"kind\":\"BuildingType\",\"structure\":\"House\"}");
            var context = new CommanderContextBuilder().Build(sim, manager);
            var roundTrip = CommanderIntentDtoCodec.InterpretJson(CommanderIntentDtoCodec.Serialize(CommanderIntentDtoCodec.FromIntent(tc)), context);
            Assert.That(roundTrip.Success, Is.True);
            var comparison = typeof(CommanderIntent).Assembly.GetType("OpenEmpires.CommanderScopeEquivalence")
                .GetMethod("SameIntent", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(comparison.Invoke(null, new object[] { tc, roundTrip.Intent }), Is.True);
            Assert.That(comparison.Invoke(null, new object[] { tc, house }), Is.False);
            var preview = typeof(CommanderIntent).Assembly.GetType("OpenEmpires.CommanderPlanPreview")
                .GetMethod("RenderIntent", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That((string)preview.Invoke(null, new object[] { tc }), Does.Contain("Town Center"));
        }

        [TestCase("owner")][TestCase("repaired")][TestCase("destroyed")][TestCase("type")]
        public void RepairBlockedForActors_TargetLossDoesNotSelectAnotherTownCenter(string loss)
        {
            var first = Building(BuildingType.TownCenter, -8); first.CurrentHealth = 10;
            var second = Building(BuildingType.TownCenter, 8); second.CurrentHealth = 10;
            var intent = Intent("RepairTarget", "Villagers", "{\"kind\":\"BuildingType\",\"structure\":\"TownCenter\"}");
            manager.SubmitCapabilityAction(intent); manager.Tick(0);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
            if (loss == "owner") first.PlayerId = 1;
            if (loss == "repaired") first.CurrentHealth = first.MaxHealth;
            if (loss == "destroyed") first.CurrentHealth = 0;
            if (loss == "type") first.Type = BuildingType.House;
            Unit(0, 0);
            manager.Tick(150);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty, "Loss of the resolved identity must not substitute another TC.");
        }

        [TestCase("owner")][TestCase("hidden")][TestCase("dead")][TestCase("type")]
        public void AttackBlockedForActors_TargetLossDoesNotSelectAnotherArcher(string loss)
        {
            Building(BuildingType.TownCenter, 0);
            var first = Unit(2, 3, 1); Unit(2, 8, 1);
            var intent = Intent("AttackTarget", "Spearman", "{\"kind\":\"UnitType\",\"unit\":\"Archer\"}");
            manager.SubmitCapabilityAction(intent); manager.Tick(0);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
            if (loss == "owner") first.PlayerId = 0;
            if (loss == "hidden") first.SimPosition = sim.MapData.TileToWorldFixed(x + 30, z);
            if (loss == "dead") { first.CurrentHealth = 0; first.State = UnitState.Dead; }
            if (loss == "type") first.UnitType = 1;
            Unit(1, 1);
            manager.Tick(150);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty, "Loss of the resolved identity must not substitute another Archer.");
        }

        [Test] public void ArcherTarget_UsesEnemyCivilizationReplacementNotActorsCivilization()
        {
            sim.SetPlayerCivilizations(new[] { Civilization.French, Civilization.English });
            Building(BuildingType.TownCenter, 0); Unit(1, 1);
            var longbow = Unit(10, 8, 1); Unit(0, 3, 1);
            var intent = Intent("AttackTarget", "Spearman", "{\"kind\":\"UnitType\",\"unit\":\"Archer\"}");
            Assert.That(new CommanderCapabilityExecutor(sim).TryCreateCommand(intent, out var command, out _), Is.True);
            Assert.That(((AttackUnitCommand)command).TargetUnitId, Is.EqualTo(longbow.Id));
        }
    }
}
