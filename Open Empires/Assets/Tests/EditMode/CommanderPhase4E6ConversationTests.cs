using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4E6")]
    public sealed class CommanderPhase4E6ConversationTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private CommanderIntentDispatcher dispatcher;
        private readonly List<GameObject> objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            goals = new CommanderGoalManager(simulation, 0);
            dispatcher = new CommanderIntentDispatcher(simulation, goals);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
            dispatcher?.Dispose();
            goals?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void SemanticMemory_IsBoundedAndContainsOnlyDetachedFacts()
        {
            var memory = new CommanderSemanticConversationMemory(2);
            memory.RecordAcceptedUnitCount(CommanderIntentCatalog.SpearmanUnitType, 10);
            memory.RecordAcceptedStructure(BuildingType.Barracks, 1,
                CommanderSemanticAnchorSelector.MyTownCenter,
                CommanderSemanticPlacementRelation.MapWest, 5);
            memory.RecordClarification("Which Town Center should I use?");

            Assert.That(memory.Snapshot(), Has.Count.EqualTo(2));
            string json = memory.ToJson();
            StringAssert.Contains("Clarification", json);
            StringAssert.Contains("MapWest", json);
            StringAssert.DoesNotContain("entity", json.ToLowerInvariant());
            StringAssert.DoesNotContain("coordinate", json.ToLowerInvariant());
            StringAssert.DoesNotContain("command", json.ToLowerInvariant());

            memory.Clear();
            Assert.That(memory.Snapshot(), Is.Empty);
            Assert.That(memory.ToJson(), Is.EqualTo("[]"));
        }

        [Test]
        public async Task AcceptedSemanticFact_IsProvidedToFollowUp_AndResetInvalidatesIt()
        {
            var provider = new FakeSemanticProvider(
                Unit(10), Unit(15), Unit(5));
            CommanderChatUI chat = CreateChat(provider);

            CommanderAIChatSubmission first = await chat.SubmitMessageAsync("make 10 spearmen");
            Assert.That(first, Is.Not.Null);
            Assert.That(first.Success, Is.True, first.DisplayText);
            Assert.That(provider.Requests, Has.Count.EqualTo(1));

            CommanderAIChatSubmission followUp = await chat.SubmitMessageAsync("make five more");
            Assert.That(followUp, Is.Not.Null);
            Assert.That(followUp.Success, Is.True, followUp.DisplayText);
            Assert.That(provider.Requests, Has.Count.EqualTo(2));
            StringAssert.Contains("AcceptedUnitCount", provider.Requests[1].SerializedSemanticMemory);
            StringAssert.Contains("\"targetTotal\":10", provider.Requests[1].SerializedSemanticMemory);

            chat.ResetConversation();
            CommanderAIChatSubmission afterReset = await chat.SubmitMessageAsync("make five");
            Assert.That(afterReset, Is.Not.Null);
            Assert.That(provider.Requests, Has.Count.EqualTo(3));
            Assert.That(provider.Requests[2].SerializedSemanticMemory, Is.EqualTo("[]"));
        }

        [Test]
        public async Task Clarification_IsRememberedOnlyUntilConversationReset()
        {
            var provider = new FakeSemanticProvider(
                "{\"outcome\":\"Clarify\",\"message\":\"Which Town Center should I use?\"}",
                Unit(5));
            CommanderChatUI chat = CreateChat(provider);

            CommanderAIChatSubmission clarification = await chat.SubmitMessageAsync(
                "build a barracks near my base");
            Assert.That(clarification == null || !clarification.Success, Is.True);
            Assert.That(provider.Requests, Has.Count.EqualTo(1));

            await chat.SubmitMessageAsync("the first one");
            Assert.That(provider.Requests, Has.Count.EqualTo(2));
            StringAssert.Contains("Which Town Center should I use?",
                provider.Requests[1].SerializedSemanticMemory);

            chat.ResetConversation();
            await chat.SubmitMessageAsync("the first one");
            Assert.That(provider.Requests[2].SerializedSemanticMemory, Is.EqualTo("[]"));
        }

        private CommanderChatUI CreateChat(FakeSemanticProvider provider)
        {
            var gameObject = new GameObject("CommanderPhase4E6Chat");
            objects.Add(gameObject);
            CommanderChatUI chat = gameObject.AddComponent<CommanderChatUI>();
            chat.Initialize(provider, simulation, goals, dispatcher);
            return chat;
        }

        private static string Unit(int count) => "{\"outcome\":\"Request\",\"nodes\":["
            + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":"
            + count + "}]}";

        private sealed class FakeSemanticProvider : ICommanderAIProvider, ICommanderSemanticProvider
        {
            private readonly Queue<string> responses;
            public List<CommanderSemanticProviderRequest> Requests { get; } =
                new List<CommanderSemanticProviderRequest>();

            public FakeSemanticProvider(params string[] responses)
            { this.responses = new Queue<string>(responses); }

            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request,
                CancellationToken token) => Task.FromResult(CommanderAIProviderResult.Rejected(
                    CommanderIntentErrorCode.ProviderFailure, "Legacy route was called."));

            public Task<CommanderSemanticResult> TranslateSemanticAsync(
                CommanderSemanticProviderRequest request, CancellationToken token)
            {
                Requests.Add(request);
                string response = responses.Count > 0 ? responses.Dequeue() : Unit(1);
                return Task.FromResult(CommanderSemanticJson.Parse(response));
            }
        }
    }
}
