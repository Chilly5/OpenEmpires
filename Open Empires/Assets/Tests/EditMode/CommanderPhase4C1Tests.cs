using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4C1")]
    public sealed class CommanderPhase4C1Tests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private StrategicPlanner planner;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            goals = new CommanderGoalManager(simulation, 0);
            planner = new StrategicPlanner(goals, CurrentResource);
        }

        [TearDown]
        public void TearDown()
        {
            planner?.Dispose();
            goals?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        private StrategicContext Context() => new StrategicContextBuilder().Build(
            new CommanderContextBuilder().Build(simulation, goals), planner);

        private int CurrentResource(ResourceType type)
        {
            var resources = simulation.ResourceManager.GetPlayerResources(0);
            switch (type)
            {
                case ResourceType.Food: return resources.Food;
                case ResourceType.Wood: return resources.Wood;
                case ResourceType.Gold: return resources.Gold;
                case ResourceType.Stone: return resources.Stone;
                default: return 0;
            }
        }

        [Test]
        public void Memory_IsBounded()
        {
            var memory = new CommanderMemory(2);
            memory.RecordConversation(CommanderConversationRole.Player, "first");
            memory.RecordConversation(CommanderConversationRole.Player, "second");
            var before = memory.Snapshot();
            memory.RecordConversation(CommanderConversationRole.Player, "third");

            CollectionAssert.AreEqual(new[] { "second", "third" },
                memory.Snapshot().Select(entry => entry.Text));
            CollectionAssert.AreEqual(new[] { "first", "second" },
                before.Select(entry => entry.Text));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<MemoryEntry>)before).Add(memory.Snapshot()[0]));
        }

        [Test]
        public void Memory_ClearsBetweenMatches()
        {
            var first = new ConversationState(0, 4);
            first.Memory.RecordCavalryPreference();
            first.Memory.RecordConversation(CommanderConversationRole.Player, "old match");
            first.Reset();

            Assert.That(first.Snapshot(), Is.Empty);
            first.Memory.RecordConversation(CommanderConversationRole.Player, "new turn");
            Assert.That(first.Snapshot().Single().Sequence, Is.EqualTo(1));

            var nextPlayer = new ConversationState(1, 4);
            Assert.That(nextPlayer.PlayerId, Is.EqualTo(1));
            Assert.That(nextPlayer.TryGetCavalryPreference(out _), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => new ConversationState(-1));
        }

        [Test]
        public void SameHistoryProducesSameContext()
        {
            var first = new ConversationState(0);
            var second = new ConversationState(0);
            foreach (var state in new[] { first, second })
            {
                state.Memory.RecordConversation(CommanderConversationRole.Player, "focus cavalry");
                state.Memory.RecordCavalryPreference();
                state.Memory.RecordConversation(CommanderConversationRole.Commander, "Cavalry focus remembered.");
            }

            Assert.That(first.Memory.ToJson(), Is.EqualTo(second.Memory.ToJson()));
            Assert.That(first.Snapshot().Count, Is.EqualTo(3));
            Assert.That(first.Memory.ToJson(), Does.Contain("\"kind\":\"Preference\""));
            var one = new StrategicAIRequest("prepare attack", Context(), 41, null, first.Snapshot());
            var two = new StrategicAIRequest("prepare attack", Context(), 41, null, second.Snapshot());
            Assert.That(GeminiStrategicAIProvider.BuildRequestJson(one),
                Is.EqualTo(GeminiStrategicAIProvider.BuildRequestJson(two)));
        }

        [Test]
        public void Memory_DoesNotLeakGameState()
        {
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            var enemy = simulation.UnitRegistry.CreateUnit(1,
                simulation.MapData.TileToWorldFixed(x, z), Fixed32.One, Fixed32.One, Fixed32.One);
            enemy.UnitType = 987;
            enemy.MaxHealth = enemy.CurrentHealth = 100;
            Assert.That(simulation.FogOfWar.GetVisibility(0, x, z), Is.EqualTo(TileVisibility.Unexplored));

            var source = new CommanderMemory();
            source.RecordConversation(CommanderConversationRole.Player, "prepare attack");
            source.RecordCavalryPreference();
            var request = new StrategicAIRequest("prepare attack", Context(), 41, null, source.Snapshot());
            source.Clear();
            source.RecordConversation(CommanderConversationRole.Player, "HIDDEN_ENEMY_MARKER_987");

            string emitted = GeminiStrategicAIProvider.BuildRequestJson(request);
            Assert.That(emitted, Does.Not.Contain("HIDDEN_ENEMY_MARKER_987"));
            Assert.That(emitted, Does.Not.Contain("\"UnitType\":987"));
            Assert.That(request.MemorySnapshot.Count, Is.EqualTo(2));

            Type[] forbidden = { typeof(object), typeof(Delegate), typeof(UnityEngine.Object),
                typeof(GameSimulation), typeof(StrategicIntent), typeof(StrategicPlan),
                typeof(StrategicDecisionRecord) };
            foreach (PropertyInfo property in typeof(MemoryEntry).GetProperties())
                Assert.That(forbidden.Contains(property.PropertyType), Is.False,
                    property.Name + " cannot retain runtime authority/state.");
        }

        [Test]
        public void Memory_ValidatesAndBoundsValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CommanderMemory(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CommanderMemory(129));
            var memory = new CommanderMemory();
            memory.RecordConversation(CommanderConversationRole.Player, new string('x', 700));
            Assert.That(memory.Snapshot().Single().Text.Length, Is.EqualTo(512));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                memory.RecordConversation((CommanderConversationRole)99, "invalid"));
            Assert.That(memory.Snapshot().Count, Is.EqualTo(1));
        }

        [Test]
        public async Task RememberedPreference_ProducesContextualAttackWithoutExecution()
        {
            var memory = new CommanderMemory();
            memory.RecordCavalryPreference();
            var contextual = new StrategicAIRequest("prepare attack", Context(), 41, null, memory.Snapshot());
            var accepted = await new MockStrategicAIProvider().InterpretStrategicIntentAsync(contextual, default);
            Assert.That(accepted.Success, Is.True, accepted.ExplanationText);
            Assert.That(accepted.Intent.ObjectiveType, Is.EqualTo(StrategicObjectiveType.AttackPreparation));
            Assert.That(accepted.Intent.Parameters["focus"], Is.EqualTo("cavalry"));

            var empty = new StrategicAIRequest("prepare attack", Context(), 42);
            var rejected = await new MockStrategicAIProvider().InterpretStrategicIntentAsync(empty, default);
            Assert.That(rejected.Success, Is.False);
            Assert.That(rejected.ExplanationText, Does.Contain("cavalry"));
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(goals.Goals, Is.Empty);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void GeminiPayload_LabelsDetachedMemoryAsUntrusted()
        {
            var memory = new CommanderMemory();
            memory.RecordConversation(CommanderConversationRole.Player, "focus cavalry");
            memory.RecordCavalryPreference();
            var request = new StrategicAIRequest("prepare attack", Context(), 41, null, memory.Snapshot());
            string payload = GeminiStrategicAIProvider.BuildRequestJson(request);
            Assert.That(payload, Does.Contain("Untrusted match-local commander memory:"));
            Assert.That(payload, Does.Contain("\\\"cavalryPreference\\\":\\\"Cavalry\\\""));
            Assert.That(payload, Does.Contain(StrategicAIContextSerializer.Serialize(Context())
                .Replace("\"", "\\\"")));
        }

        [Test]
        public async Task BridgeReset_ClearsPrivateHistoryAndRejectsLateResponse()
        {
            var provider = new DeferredStrategicProvider();
            var state = new ConversationState(0);
            state.Memory.RecordCavalryPreference();
            using var bridge = new StrategicAIApprovalBridge(provider, planner.IntentIds, Context,
                TimeSpan.FromSeconds(2), state.Snapshot);
            Task<StrategicAIProviderResult> pending = bridge.TranslateAsync("prepare attack");
            bridge.Reset();
            Assert.That(await Task.WhenAny(pending, Task.Delay(2000)), Is.SameAs(pending));
            provider.Complete();
            await Task.Yield();
            Assert.That((await pending).Success, Is.False);
            Assert.That(bridge.PendingIntent, Is.Null);
            Assert.That(bridge.HistorySnapshot, Is.Empty);
        }

        [Test]
        public async Task EvictedPreference_CannotBeResurrectedByProviderOrLegacyHistory()
        {
            var state = new ConversationState(0, 2);
            state.Memory.RecordCavalryPreference();
            state.Memory.RecordConversation(CommanderConversationRole.Player, "later one");
            state.Memory.RecordConversation(CommanderConversationRole.Commander, "later two");
            Assert.That(state.TryGetCavalryPreference(out _), Is.False);

            var provider = new AlwaysCavalryProvider();
            using var bridge = new StrategicAIApprovalBridge(provider, planner.IntentIds, Context,
                TimeSpan.FromSeconds(2), state.Snapshot);
            StrategicAIProviderResult result = await bridge.TranslateAsync("prepare attack");
            Assert.That(result.Success, Is.False);
            Assert.That(result.ExplanationText, Does.Contain("focus cavalry"));
            Assert.That(provider.CallCount, Is.EqualTo(0));
            Assert.That(bridge.PendingIntent, Is.Null);
        }

        private sealed class DeferredStrategicProvider : IStrategicAIInterpreter
        {
            private StrategicAIRequest request;
            private readonly TaskCompletionSource<StrategicAIProviderResult> completion =
                new TaskCompletionSource<StrategicAIProviderResult>();

            public Task<StrategicAIProviderResult> InterpretStrategicIntentAsync(
                StrategicAIRequest value, CancellationToken cancellationToken)
            {
                request = value;
                return completion.Task;
            }

            public void Complete() => completion.TrySetResult(StrategicAIJson.Parse(
                "{\"intentCategory\":\"Strategic\",\"objectiveType\":\"AttackPreparation\",\"parameters\":{\"focus\":\"cavalry\"}}",
                request));
        }

        private sealed class AlwaysCavalryProvider : IStrategicAIInterpreter
        {
            public int CallCount { get; private set; }
            public Task<StrategicAIProviderResult> InterpretStrategicIntentAsync(
                StrategicAIRequest request, CancellationToken cancellationToken)
            {
                CallCount++;
                return Task.FromResult(StrategicAIJson.Parse(
                    "{\"intentCategory\":\"Strategic\",\"objectiveType\":\"AttackPreparation\",\"parameters\":{\"focus\":\"cavalry\"}}",
                    request));
            }
        }
    }
}
