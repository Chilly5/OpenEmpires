using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4E1")]
    public sealed class CommanderPhase4E1StrategicTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private StrategicPlanner planner;
        private StrategicPipeline pipeline;
        private StrategicAIApprovalBridge bridge;
        private readonly ConversationState preferenceMemory = new ConversationState(0);

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            goals = new CommanderGoalManager(simulation, 0);
            planner = new StrategicPlanner(goals, _ => 0);
            pipeline = new StrategicPipeline(simulation, goals, planner);
            bridge = new StrategicAIApprovalBridge(new MockStrategicAIProvider(), planner.IntentIds,
                () => pipeline.CaptureContext());
        }

        [TearDown]
        public void TearDown()
        {
            bridge?.Dispose();
            pipeline?.Dispose();
            planner?.Dispose();
            goals?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void SemanticObjective_StagesOwnedAiRecommendationWithoutCreatingPlan()
        {
            int generation = ReadGeneration();
            object result = Stage(StrategicObjectiveType.RangedReinforcement, 0, generation);

            Assert.That(ReadSuccess(result), Is.True);
            StrategicIntent pending = bridge.PendingIntent;
            Assert.That(pending, Is.Not.Null);
            Assert.That(pending.Source, Is.EqualTo(StrategicIntentSource.AIRecommendation));
            Assert.That(pending.PlayerId, Is.EqualTo(0));
            Assert.That(pending.CreatedTick, Is.EqualTo(simulation.CurrentTick));
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(planner.Intents, Is.Empty);
            Assert.That(goals.Goals, Is.Empty);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void SemanticObjective_RejectsWrongOwnerAndStaleGeneration()
        {
            int generation = ReadGeneration();

            Assert.That(ReadSuccess(Stage(StrategicObjectiveType.RangedReinforcement, 1, generation)),
                Is.False);
            Assert.That(bridge.PendingIntent, Is.Null);
            Assert.That(ReadSuccess(Stage(StrategicObjectiveType.RangedReinforcement, 0,
                generation - 1)), Is.False);
            Assert.That(bridge.PendingIntent, Is.Null);
        }

        [Test]
        public async Task SemanticObjective_RejectsWhileLegacyTranslationIsInFlight()
        {
            var held = new HeldInterpreter();
            bridge.Dispose();
            bridge = new StrategicAIApprovalBridge(held, planner.IntentIds,
                () => pipeline.CaptureContext());
            Task<StrategicAIProviderResult> translation = bridge.TranslateAsync(
                "prepare defenses", CancellationToken.None);
            int generation = ReadGeneration();

            Assert.That(ReadSuccess(Stage(StrategicObjectiveType.RangedReinforcement, 0, generation)),
                Is.False);
            Assert.That(bridge.PendingIntent, Is.Null);
            held.Release();
            await translation;
        }

        [Test]
        public void SemanticAttackObjective_RequiresExistingCavalryPreference()
        {
            int generation = ReadGeneration();

            Assert.That(ReadSuccess(Stage(StrategicObjectiveType.AttackPreparation, 0, generation)),
                Is.False);
            Assert.That(bridge.PendingIntent, Is.Null);
        }

        [Test]
        public void SemanticAttackObjective_AcceptsOnlyWithExistingCavalryPreference()
        {
            bridge.Dispose();
            preferenceMemory.Memory.RecordCavalryPreference();
            bridge = new StrategicAIApprovalBridge(new MockStrategicAIProvider(), planner.IntentIds,
                () => pipeline.CaptureContext(), null, () => preferenceMemory.Snapshot());

            object result = Stage(StrategicObjectiveType.AttackPreparation, 0, ReadGeneration());

            Assert.That(ReadSuccess(result), Is.True);
            Assert.That(bridge.PendingIntent?.Source, Is.EqualTo(StrategicIntentSource.AIRecommendation));
            Assert.That(bridge.PendingIntent?.Parameters["focus"], Is.EqualTo("cavalry"),
                "The bridge derives focus from retained game-side preference; the semantic node carries no parameters.");
            Assert.That(planner.Plans, Is.Empty);
        }

        private int ReadGeneration()
        {
            PropertyInfo property = typeof(StrategicAIApprovalBridge).GetProperty("Generation",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null,
                "The bridge must expose its synchronized generation for semantic async guards.");
            return (int)property.GetValue(bridge);
        }

        private object Stage(StrategicObjectiveType objective, int owner, int generation)
        {
            MethodInfo method = typeof(StrategicAIApprovalBridge).GetMethod(
                "StageValidatedSemanticObjective", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null,
                "Semantic objectives need a bridge-owned staging API, not PlayerDirect conversion.");
            return method.Invoke(bridge, new object[] { objective, owner, generation });
        }

        private static bool ReadSuccess(object result)
        {
            Assert.That(result, Is.Not.Null);
            return (bool)result.GetType().GetProperty("Success").GetValue(result);
        }

        private sealed class HeldInterpreter : IStrategicAIInterpreter
        {
            private readonly TaskCompletionSource<StrategicAIProviderResult> gate =
                new TaskCompletionSource<StrategicAIProviderResult>();

            public Task<StrategicAIProviderResult> InterpretStrategicIntentAsync(
                StrategicAIRequest request, CancellationToken cancellationToken) => gate.Task;

            public void Release() => gate.SetResult(StrategicAIProviderResult.Rejected("test"));
        }
    }
}
