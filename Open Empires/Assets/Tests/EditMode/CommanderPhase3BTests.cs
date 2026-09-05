using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public class CommanderPhase3BTests
    {
        private SimulationConfig config;
        private GameSimulation sim;
        private CommanderGoalManager manager;
        private CommanderContext Context() => new CommanderContextBuilder().Build(sim, manager);

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            sim = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            manager = new CommanderGoalManager(sim, 0);
        }
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(config);

        [Test]
        public void Context_ContainsOwnedResources()
        {
            var resources = sim.ResourceManager.GetPlayerResources(0);
            resources.Food = 101; resources.Wood = 202; resources.Gold = 303; resources.Stone = 404;
            var context = Context();
            resources.Food = 999;
            Assert.That(new[] { context.Resources.Food, context.Resources.Wood, context.Resources.Gold, context.Resources.Stone }, Is.EqualTo(new[] { 101, 202, 303, 404 }));
            Assert.That(context.Population, Is.EqualTo(sim.GetPopulation(0)));
            Assert.That(context.PopulationCap, Is.EqualTo(sim.GetPopulationCap(0)));
            Assert.That(context.AvailableCapacity, Is.EqualTo(Math.Max(0, context.PopulationCap - context.Population)));
        }

        [Test]
        public void Context_DoesNotLeakHiddenEnemyInformation()
        {
            int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
            var enemy = Unit(1, 7, x, z);
            sim.CreateBuilding(1, BuildingType.Stables, x, z, false);
            Assert.That(sim.FogOfWar.GetVisibility(0, x, z), Is.EqualTo(TileVisibility.Unexplored));
            var before = Context().ToJson();
            enemy.UnitType = 99;
            sim.CreateBuilding(1, BuildingType.Barracks, 45, 45, false);
            Assert.That(Context().ToJson(), Is.EqualTo(before), "Hidden enemy changes must not affect serialized context.");
            sim.FogOfWar.SetVisible(0, x, z);
            Assert.That(sim.FogOfWar.GetVisibility(0, x, z), Is.EqualTo(TileVisibility.Visible));
            Assert.That(Context().Units, Is.Empty, "Commander has no enemy roster awareness, even when visible.");
            Assert.That(Context().Buildings, Is.Empty);
        }

        [Test]
        public void Context_ExcludesHiddenAndExploredResources()
        {
            foreach (var node in sim.MapData.GetAllResourceNodes()) node.RemainingAmount = 0;
            int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
            var hidden = sim.MapData.AddResourceNode(ResourceType.Gold, sim.MapData.TileToWorldFixed(x, z), 123);
            Assert.That(Context().VisibleResources, Is.Empty);
            sim.FogOfWar.SetVisible(0, hidden.TileX, hidden.TileZ);
            var snapshot = Context();
            Assert.That(snapshot.VisibleResources.Single().RemainingAmount, Is.EqualTo(123));
            sim.FogOfWar.DemoteAllVisible(0);
            hidden.RemainingAmount = 7;
            Assert.That(Context().VisibleResources, Is.Empty);
            Assert.That(snapshot.VisibleResources.Single().RemainingAmount, Is.EqualTo(123));
        }

        [Test]
        public void Context_ContainsActiveGoals()
        {
            var first = manager.SubmitEnsureUnitCount(1, 10);
            var second = manager.SubmitBuildStructure(BuildingType.House);
            var cancelled = manager.SubmitEnsureUnitCount(2, 5);
            manager.CancelGoal(cancelled.GoalId);
            manager.Tick(0);
            var context = Context();
            Assert.That(context.ActiveGoals.Select(goal => goal.GoalId), Is.EquivalentTo(new[] { first.GoalId, second.GoalId }));
            Assert.That(context.ActiveGoals[0].Status, Is.EqualTo(first.Status.ToString()));
            manager.CancelGoal(first.GoalId);
            Assert.That(context.ActiveGoals, Has.Count.EqualTo(2));
        }

        [Test]
        public void Context_CopiesOwnedProductionGarrisonAndCivilization()
        {
            var building = sim.CreateBuilding(0, BuildingType.TownCenter, 50, 50, false);
            var unit = Unit(0, 1, 51, 51);
            sim.UnitRegistry.GarrisonUnit(unit.Id); building.GarrisonedUnitIds.Add(unit.Id);
            building.TrainingQueue.Add(0); building.TrainingTicksRemaining = 17;
            sim.CreateBuilding(0, BuildingType.Barracks, 55, 55, true);
            var context = Context();
            Assert.That(context.Units.Single(item => item.UnitType == 1).Count, Is.EqualTo(1));
            Assert.That(context.Units.Single(item => item.UnitType == 0).QueuedCount, Is.EqualTo(1));
            Assert.That(context.Production.Single().CurrentlyTrainingUnit, Is.EqualTo(0));
            Assert.That(context.Buildings.Single(item => item.Type == "Barracks").IsUnderConstruction, Is.True);
            Assert.That(context.Civilization, Is.EqualTo(sim.GetPlayerCivilization(0).ToString()));
            Assert.That(context.Age, Is.EqualTo(sim.GetPlayerAge(0)));
            Assert.That(context.UnitOptions.Single(item => item.IntentUnit == "Knight").RequiredAge, Is.EqualTo(3));
            building.TrainingQueue.Clear();
            Assert.That(context.Production.Single().TrainingQueue, Has.Count.EqualTo(1));
            Assert.Throws<NotSupportedException>(() => ((IList<int>)context.Production[0].TrainingQueue).Add(7));
        }

        [Test]
        public void IntentDTO_SerializesCorrectly()
        {
            var dto = new CommanderIntentDTO { intentType = "EnsureUnitCount", unit = "Spearman", amount = 10 };
            string json = CommanderIntentDtoCodec.Serialize(dto);
            Assert.That(json, Is.EqualTo("{\"intentType\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"amount\":10,\"constraints\":[]}"));
            var result = CommanderIntentDtoCodec.InterpretJson(json, Context());
            Assert.That(result.Success, Is.True, result.Reason);
            Assert.That(((EnsureUnitCountIntent)result.Intent).TargetTotal, Is.EqualTo(10));
            Assert.That(result.Intent.PlayerId, Is.Zero);
        }

        [TestCase("{\"intentType\":\"UnknownCommand\"}", CommanderIntentErrorCode.UnknownCommand)]
        [TestCase("{\"amount\":\"ten\"}", CommanderIntentErrorCode.InvalidJson)]
        [TestCase("{\"intentType\":\"EnsureUnitCount\",\"unit\":\"FakeUnit\",\"amount\":10}", CommanderIntentErrorCode.UnknownUnit)]
        [TestCase("{\"intentType\":0}", CommanderIntentErrorCode.InvalidJson)]
        [TestCase("{\"intentType\":\"0\"}", CommanderIntentErrorCode.UnknownCommand)]
        [TestCase("{\"intentType\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"amount\":10,\"playerId\":1}", CommanderIntentErrorCode.InvalidJson)]
        [TestCase("{\"intentType\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"amount\":1.5}", CommanderIntentErrorCode.InvalidJson)]
        [TestCase("{\"intentType\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"amount\":999999999999999999999}", CommanderIntentErrorCode.InvalidJson)]
        [TestCase("{\"intentType\":\"BuildStructure\",\"structure\":\"House\",\"amount\":0}", CommanderIntentErrorCode.AmountOutOfRange)]
        [TestCase("{\"intentType\":\"EnsureUnitCount\",\"intentType\":\"BuildStructure\"}", CommanderIntentErrorCode.InvalidJson)]
        [TestCase("{} {}", CommanderIntentErrorCode.InvalidJson)]
        [TestCase("{\"$type\":\"OpenEmpires.EnsureUnitCountIntent\"}", CommanderIntentErrorCode.InvalidJson)]
        [TestCase("{broken", CommanderIntentErrorCode.InvalidJson)]
        [TestCase("{\"intentType\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"amount\":0x10}", CommanderIntentErrorCode.InvalidJson)]
        [TestCase("{\"intentType\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"amount\":10,}", CommanderIntentErrorCode.InvalidJson)]
        [TestCase("{'intentType':'EnsureUnitCount','unit':'Spearman','amount':10}", CommanderIntentErrorCode.InvalidJson)]
        [TestCase("{\"intentType\":\"EnsureUnitCount\",/*comment*/\"unit\":\"Spearman\",\"amount\":10}", CommanderIntentErrorCode.InvalidJson)]
        public void InvalidDTO_IsRejected(string json, CommanderIntentErrorCode expected)
        {
            var result = CommanderIntentDtoCodec.InterpretJson(json, Context());
            Assert.That(result.Success, Is.False);
            Assert.That(result.Intent, Is.Null);
            Assert.That(result.ErrorCode, Is.EqualTo(expected), result.Reason);
            Assert.That(result.ErrorField, Is.Not.Empty);
            Assert.That(manager.Goals, Is.Empty);
        }

        [Test]
        public void ConstraintDTO_RoundTripWorks()
        {
            var intent = new EnsureUnitCountIntent(0, 1, 10, new CommanderConstraint[] {
                new ProtectedResourceConstraint(ResourceType.Gold, 4), new MaximumQueueConstraint(2),
                new PreferredWorkersConstraint(CommanderPreferredWorkerSource.IdleOnly) });
            var dto = CommanderIntentDtoCodec.FromIntent(intent);
            var result = CommanderIntentDtoCodec.InterpretJson(CommanderIntentDtoCodec.Serialize(dto), Context());
            Assert.That(result.Success, Is.True, result.Reason);
            Assert.That(((ProtectedResourceConstraint)result.Intent.Constraints[0]).MinimumWorkers, Is.EqualTo(4));
            Assert.That(((MaximumQueueConstraint)result.Intent.Constraints[1]).MaximumQueue, Is.EqualTo(2));
            Assert.That(((PreferredWorkersConstraint)result.Intent.Constraints[2]).WorkerSource, Is.EqualTo(CommanderPreferredWorkerSource.IdleOnly));
            dto.constraints.Add(dto.constraints[0]);
            Assert.That(CommanderIntentDtoCodec.ValidateAndConvert(dto, Context()).Success, Is.False);
        }

        [TestCase("{\"intentType\":\"SetResourceAllocation\",\"resource\":\"Wood\",\"mode\":\"Increase\"}")]
        [TestCase("{\"intentType\":\"BuildStructure\",\"structure\":\"Barracks\",\"amount\":2}")]
        public void IntentDTO_AllIntentKindsRoundTrip(string json)
        {
            var context = Context();
            var result = CommanderIntentDtoCodec.InterpretJson(json, context);
            Assert.That(result.Success, Is.True, result.Reason);
            var roundTrip = CommanderIntentDtoCodec.InterpretJson(CommanderIntentDtoCodec.Serialize(CommanderIntentDtoCodec.FromIntent(result.Intent)), context);
            Assert.That(roundTrip.Success, Is.True, roundTrip.Reason);
            Assert.That(roundTrip.Intent.Type, Is.EqualTo(result.Intent.Type));
        }

        [TestCase("{\"type\":\"PreferredWorkers\",\"mode\":\"0\"}")]
        [TestCase("{\"type\":\"MaximumQueue\",\"amount\":9}")]
        [TestCase("{\"type\":\"ProtectedResource\",\"resource\":\"Gold\",\"amount\":-1}")]
        [TestCase("{\"type\":\"FakeConstraint\"}")]
        public void InvalidConstraintDTO_IsRejected(string constraint)
        {
            var result = CommanderIntentDtoCodec.InterpretJson("{\"intentType\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"amount\":10,\"constraints\":[" + constraint + "]}", Context());
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(CommanderIntentErrorCode.UnsupportedConstraint));
        }

        [Test]
        public void ResponseLimits_AndDuplicateConstraints_AreRejected()
        {
            var context = Context();
            Assert.That(CommanderIntentDtoCodec.InterpretJson(new string(' ', CommanderIntentDtoCodec.MaximumResponseCharacters + 1), context).Success, Is.False);
            Assert.That(CommanderIntentDtoCodec.InterpretJson("{\"constraints\":[[[[[[[[[[]]]]]]]]]]}", context).ErrorCode, Is.EqualTo(CommanderIntentErrorCode.InvalidJson));
            var dto = new CommanderIntentDTO { intentType = "EnsureUnitCount", unit = "Spearman", amount = 10 };
            dto.constraints.Add(new CommanderConstraintDTO { type = "PreferredWorkers", mode = "IdleOnly" });
            dto.constraints.Add(new CommanderConstraintDTO { type = "PreferredWorkers", mode = "IdleOnly" });
            Assert.That(CommanderIntentDtoCodec.ValidateAndConvert(dto, context).ErrorCode, Is.EqualTo(CommanderIntentErrorCode.UnsupportedConstraint));
        }

        private UnitData Unit(int player, int type, int x, int z)
        {
            var unit = sim.UnitRegistry.CreateUnit(player, sim.MapData.TileToWorldFixed(x, z), Fixed32.One, Fixed32.One, Fixed32.One);
            unit.UnitType = type; unit.CurrentHealth = unit.MaxHealth = 100; unit.State = UnitState.Idle;
            return unit;
        }
    }
}
