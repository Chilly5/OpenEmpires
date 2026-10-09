using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5B")]
    public sealed class CommanderPhase5BTaskBoardTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager manager;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            manager = new CommanderGoalManager(simulation, 0);
        }

        [TearDown]
        public void TearDown()
        {
            manager?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void CompoundRequest_ProjectsOneCardWithTwoRealGoalSteps()
        {
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1,\"placement\":{\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":5}},"
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10,\"producerFromNode\":0}]}");
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var scope = manager.PrepareActionPlan(parsed, "Build a barracks, then train ten spearmen", 4);
            manager.SubmitSemanticGraph(manager.ApproveActionPlan(scope, 4));

            var snapshot = Capture(4);

            var cards = Cards(snapshot);
            Assert.That(cards.Length, Is.EqualTo(1));
            Assert.That(Value<long>(cards[0], "RequestId"), Is.EqualTo(scope.RequestId));
            Assert.That(Value<string>(cards[0], "Objective"), Does.Contain("barracks"));
            Assert.That(((System.Collections.ICollection)Property(cards[0], "Steps")).Count, Is.EqualTo(2));
            Assert.That(Property(cards[0], "Status").ToString(), Is.EqualTo("Accepted"));
            Assert.That(Value<int>(snapshot, "ActiveCount"), Is.EqualTo(1));
        }

        [Test]
        public void CancelRequest_StopsAllItsGoalsButLeavesUnrelatedGoalUntouched()
        {
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1,\"placement\":{\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":5}},"
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10,\"producerFromNode\":0}]}");
            var scope = manager.PrepareActionPlan(parsed, "Build a barracks, then train ten spearmen", 4);
            var owned = manager.SubmitSemanticGraph(manager.ApproveActionPlan(scope, 4));
            var unrelated = manager.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
            var card = Cards(Capture(4)).Single(x => Value<long>(x, "RequestId") == scope.RequestId);

            Assert.That(Cancel(card, 4), Is.True);
            Assert.That(owned.All(x => x.Status == CommanderGoalStatus.Cancelled), Is.True);
            Assert.That(unrelated.IsTerminal, Is.False);
            Assert.That(Cancel(card, 4), Is.False);
        }

        [Test]
        public void StaleGeneration_CannotCancelLiveRequest()
        {
            var goal = manager.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
            var card = Cards(Capture(4)).Single();

            Assert.That(Cancel(card, 5), Is.False);
            Assert.That(goal.IsTerminal, Is.False);
        }

        [Test]
        public void AgeCard_ReportsObservedAgeAndTerminalStatusWithoutInventedPercent()
        {
            var goal = manager.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
            var waiting = Cards(Capture(4)).Single();
            Assert.That(Value<string>(waiting, "Progress"), Does.Contain("Age"));
            Assert.That(Value<string>(waiting, "Progress"), Does.Contain("3"));
            Assert.That(Value<string>(waiting, "Progress"), Does.Not.Contain("%"));
            goal.SetStatus(CommanderGoalStatus.Completed, "Reached Castle Age.");

            var completed = Cards(Capture(4)).Single();
            Assert.That(Property(completed, "Status").ToString(), Is.EqualTo("Completed"));
            Assert.That(Value<bool>(completed, "CanCancel"), Is.False);
            Assert.That(Value<string>(completed, "Progress"), Does.Not.Contain("%"));
        }

        [Test]
        public void CardFromPreviousRuntime_CannotCancelSameIdsInReplacementRuntime()
        {
            manager.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
            var oldCard = Cards(Capture(4)).Single();
            var otherSimulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            using (var replacement = new CommanderGoalManager(otherSimulation, 0))
            {
                var newGoal = replacement.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
                Assert.That(Value<long>(oldCard, "RequestId"), Is.EqualTo(-newGoal.GoalId));
                Assert.That((bool)ProjectionType().GetMethod("TryCancel", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new object[] { replacement, null, oldCard, 4 }), Is.False);
                Assert.That(newGoal.IsTerminal, Is.False);
            }
        }

        [Test]
        public void PartiallyFailedRequest_RemainsCancellableWhileSiblingAutomationIsLive()
        {
            var parsed = Compound();
            var scope = manager.PrepareActionPlan(parsed, "Build a barracks, then train ten spearmen", 4);
            var goals = manager.SubmitSemanticGraph(manager.ApproveActionPlan(scope, 4));
            goals[0].SetStatus(CommanderGoalStatus.Failed, "Producer unavailable.");

            var card = Cards(Capture(4)).Single();
            Assert.That(Property(card, "Status").ToString(), Is.EqualTo("Failed"));
            Assert.That(Value<bool>(card, "CanCancel"), Is.True);
            Assert.That(Cancel(card, 4), Is.True);
            Assert.That(goals[1].Status, Is.EqualTo(CommanderGoalStatus.Cancelled));
        }

        [Test]
        public void CompletedHistory_CannotHideOlderActiveTaskOrBadgeCount()
        {
            var active = manager.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
            for (int i = 0; i < 32; i++)
                manager.SubmitReachAge(CommanderSemanticAgeTarget.Castle)
                    .SetStatus(CommanderGoalStatus.Completed, "Already reached.");

            var snapshot = Capture(4);
            Assert.That(Value<int>(snapshot, "ActiveCount"), Is.EqualTo(1));
            Assert.That(Cards(snapshot).Any(card => Value<long>(card, "RequestId") == -active.GoalId), Is.True);
        }

        [Test]
        public void FreshCardAfterConversationReset_CanCancelStillLiveOldRequest_ButOldCardCannot()
        {
            var scope = manager.PrepareActionPlan(Compound(), "Build a barracks, then train ten spearmen", 0);
            var goals = manager.SubmitSemanticGraph(manager.ApproveActionPlan(scope, 0));
            var oldCard = Cards(Capture(0)).Single();
            var freshCard = Cards(Capture(1)).Single();

            Assert.That(Cancel(oldCard, 1), Is.False, "A stale UI card cannot act after reset.");
            Assert.That(goals.All(x => !x.IsTerminal), Is.True);
            Assert.That(Cancel(freshCard, 1), Is.True, "The new UI observation must control the still-live request.");
            Assert.That(goals.All(x => x.Status == CommanderGoalStatus.Cancelled), Is.True);
        }

        private static CommanderSemanticResult Compound() => CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
            + "{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1,\"placement\":{\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":5}},"
            + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10,\"producerFromNode\":0}]}");

        private static Type ProjectionType()
        {
            var type = typeof(CommanderGoalManager).Assembly.GetType("OpenEmpires.CommanderTaskBoardProjection");
            Assert.That(type, Is.Not.Null, "Phase5B task-board projection is not yet implemented.");
            return type;
        }

        private object Capture(int generation) => ProjectionType().GetMethod("Capture", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { manager, null, generation });

        private bool Cancel(object card, int generation) => (bool)ProjectionType().GetMethod("TryCancel", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { manager, null, card, generation });

        private static object[] Cards(object snapshot) => ((System.Collections.IEnumerable)Property(snapshot, "Cards"))
            .Cast<object>().ToArray();

        private static object Property(object source, string name) => source.GetType().GetProperty(name).GetValue(source);
        private static T Value<T>(object source, string name) => (T)Property(source, name);
    }
}
