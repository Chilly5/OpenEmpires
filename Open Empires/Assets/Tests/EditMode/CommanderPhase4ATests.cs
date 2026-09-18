using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4A")]
    public sealed class CommanderPhase4ATests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goalManager;
        private CommanderIntentDispatcher dispatcher;
        private readonly List<GameObject> objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            goalManager = new CommanderGoalManager(simulation, 0);
            dispatcher = new CommanderIntentDispatcher(simulation, goalManager);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
            objects.Clear();
            dispatcher?.Dispose();
            goalManager?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        private CommanderContext Context() =>
            new CommanderContextBuilder().Build(simulation, goalManager);

        private CommanderAIRequest Request(string message) =>
            new CommanderAIRequest(message, Context(),
                Array.Empty<CommanderConversationMessage>());

        [Test]
        public async Task MockProvider_ReturnsValidIntent()
        {
            var provider = new MockAIProvider();
            CommanderAIProviderResult result = await provider.TranslateAsync(
                Request("make 10 spearmen"), CancellationToken.None);

            Assert.That(result.Success, Is.True, result.FailureReason);
            Assert.That(result.IntentDto.intentCategory, Is.EqualTo("Tactical"));
            Assert.That(result.IntentDto.intentType, Is.EqualTo("EnsureUnitCount"));
            Assert.That(result.IntentDto.unit, Is.EqualTo("Spearman"));
            Assert.That(result.IntentDto.amount, Is.EqualTo(10));
            StringAssert.Contains("\"parameters\"", result.IntentJson);
        }

        [Test]
        public async Task GeminiProvider_ParsesJsonResponse()
        {
            string response = "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":"
                + "\"```json\\n{\\\"intentCategory\\\":\\\"Tactical\\\","
                + "\\\"intentType\\\":\\\"EnsureUnitCount\\\","
                + "\\\"parameters\\\":{\\\"unit\\\":\\\"Archer\\\","
                + "\\\"count\\\":5}}\\n```\"}]}}]}";
            var transport = new FakeTransport(
                new CommanderHttpResponse(404, "{}"),
                new CommanderHttpResponse(200, response));
            var provider = new GeminiAIProvider("test-only-key", transport);

            CommanderAIProviderResult result = await provider.TranslateAsync(
                Request("make 5 archers"), CancellationToken.None);

            Assert.That(result.Success, Is.True, result.FailureReason);
            Assert.That(result.IntentDto.unit, Is.EqualTo("Archer"));
            Assert.That(result.IntentDto.amount, Is.EqualTo(5));
            Assert.That(transport.RequestedUris.First().AbsoluteUri,
                Does.Contain(GeminiAIProvider.PrimaryModel));
            Assert.That(transport.RequestedUris.Last().AbsoluteUri,
                Does.Contain(GeminiAIProvider.FallbackModel));
            Assert.That(transport.Headers.All(headers => headers.ContainsKey("x-goog-api-key")),
                Is.True);
            Assert.That(transport.RequestedUris.All(uri => uri.Query.Length == 0), Is.True,
                "The key must never be placed in the URL.");
        }

        [Test]
        public void InvalidLLMJson_IsRejectedSafely()
        {
            CommanderAIProviderResult result = CommanderAIJson.ParseTacticalIntent(
                "The answer is definitely to train some units.", Context());
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(CommanderIntentErrorCode.InvalidJson));
            Assert.That(goalManager.Goals, Is.Empty);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public async Task ChatInput_CreatesCommanderIntent()
        {
            var adapter = new CommanderAIIntentAdapter(new MockAIProvider(), simulation,
                goalManager, dispatcher);
            CommanderAIChatSubmission result = await adapter.SubmitAsync(
                "build barracks");

            Assert.That(result.Success, Is.True, result.DisplayText);
            Assert.That(result.Interpretation.Intent, Is.TypeOf<BuildStructureIntent>());
            Assert.That(result.CommanderSubmission.CreatedGoal, Is.True);
        }

        [Test]
        public async Task CommanderIntent_ReachesExistingPipeline()
        {
            var adapter = new CommanderAIIntentAdapter(new MockAIProvider(), simulation,
                goalManager, dispatcher);
            CommanderAIChatSubmission result = await adapter.SubmitAsync(
                "put 8 villagers on wood");

            Assert.That(result.Success, Is.True, result.DisplayText);
            ResourceAllocationGoal goal = goalManager.Goals.OfType<ResourceAllocationGoal>().Single();
            Assert.That(goal.Resource, Is.EqualTo(ResourceType.Wood));
            Assert.That(goal.TargetWorkers, Is.EqualTo(8));
            Assert.That(goal.PlayerId, Is.Zero);
        }

        [Test]
        public async Task LLM_CannotCreateCommandsDirectly()
        {
            var provider = new MockAIProvider();
            CommanderAIProviderResult result = await provider.TranslateAsync(
                Request("make 3 knights"), CancellationToken.None);

            Assert.That(result.Success, Is.True);
            Assert.That(goalManager.Goals, Is.Empty,
                "A provider may only return data; only the adapter/dispatcher may create a goal.");
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty,
                "A provider must never enqueue gameplay commands.");
            Assert.That(typeof(ICommanderAIProvider).GetMethods().Single().ReturnType,
                Is.EqualTo(typeof(Task<CommanderAIProviderResult>)));
        }

        [Test]
        public void ConversationHistory_RemainsBounded()
        {
            var history = new CommanderConversationHistory(4);
            for (int i = 0; i < 10; i++)
                history.Append(i % 2 == 0 ? CommanderConversationRole.Player
                    : CommanderConversationRole.Commander, "turn-" + i);

            Assert.That(history.Count, Is.EqualTo(4));
            Assert.That(history.Snapshot().Select(turn => turn.Text),
                Is.EqualTo(new[] { "turn-6", "turn-7", "turn-8", "turn-9" }));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<CommanderConversationMessage>)history.Snapshot()).Add(
                    new CommanderConversationMessage(CommanderConversationRole.Player, "bad")));
        }

        [Test]
        public void GameContext_DoesNotExposeHiddenInformation()
        {
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            UnitData hidden = simulation.UnitRegistry.CreateUnit(1,
                simulation.MapData.TileToWorldFixed(x, z), Fixed32.One,
                Fixed32.FromFloat(.4f), Fixed32.One);
            hidden.UnitType = 7;
            hidden.CurrentHealth = hidden.MaxHealth = 100;
            Assert.That(simulation.FogOfWar.GetVisibility(0, x, z),
                Is.EqualTo(TileVisibility.Unexplored));

            string before = CommanderAIContextSerializer.Serialize(Context());
            hidden.UnitType = 99;
            simulation.CreateBuilding(1, BuildingType.Stables, x + 1, z, false);
            string after = CommanderAIContextSerializer.Serialize(Context());

            Assert.That(after, Is.EqualTo(before));
            StringAssert.DoesNotContain("VisibleEnemyMilitary", before);
            StringAssert.DoesNotContain("enemyBase", before);
        }

        [Test]
        public async Task CommanderChat_SubmitsMessage()
        {
            CommanderChatUI chat = CreateChat();
            chat.InputText = "make 10 spearmen";
            CommanderAIChatSubmission result = await chat.SubmitCurrentInputAsync();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Success, Is.True, result.DisplayText);
            Assert.That(chat.LatestSubmission, Is.SameAs(result));
            Assert.That(goalManager.Goals.OfType<EnsureUnitCountGoal>().Single().TargetTotal,
                Is.EqualTo(10));
        }

        [Test]
        public async Task CommanderChat_DisplaysResponse()
        {
            CommanderChatUI chat = CreateChat();
            CommanderAIChatSubmission result = await chat.SubmitMessageAsync(
                "put 5 villagers on food");

            Assert.That(result.Success, Is.True, result.DisplayText);
            StringAssert.Contains("Player:" + Environment.NewLine + "put 5 villagers on food",
                chat.DisplayedTranscript);
            StringAssert.Contains("Commander:" + Environment.NewLine
                + "Assigning 5 villagers to food.", chat.DisplayedTranscript);
        }

        private CommanderChatUI CreateChat()
        {
            var gameObject = new GameObject("CommanderPhase4ATestChat");
            objects.Add(gameObject);
            CommanderChatUI chat = gameObject.AddComponent<CommanderChatUI>();
            chat.Initialize(new MockAIProvider(), simulation, goalManager, dispatcher);
            return chat;
        }

        private sealed class FakeTransport : ICommanderHttpTransport
        {
            private readonly Queue<CommanderHttpResponse> responses =
                new Queue<CommanderHttpResponse>();
            public readonly List<Uri> RequestedUris = new List<Uri>();
            public readonly List<IReadOnlyDictionary<string, string>> Headers =
                new List<IReadOnlyDictionary<string, string>>();

            public FakeTransport(params CommanderHttpResponse[] values)
            {
                foreach (CommanderHttpResponse value in values) responses.Enqueue(value);
            }

            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string json,
                IReadOnlyDictionary<string, string> headers,
                CancellationToken cancellationToken)
            {
                RequestedUris.Add(uri);
                Headers.Add(new Dictionary<string, string>(headers));
                return Task.FromResult(responses.Dequeue());
            }
        }
    }
}
