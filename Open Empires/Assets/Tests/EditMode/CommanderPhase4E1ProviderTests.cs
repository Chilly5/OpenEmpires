using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4E1")]
    public sealed class CommanderPhase4E1ProviderTests
    {
        private const string DummyKey = "dummy-semantic-key";
        private const string UnitJson = "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10}]}";

        [TestCase("Could you get our spearmen up to ten?")]
        [TestCase("I want a total of ten spears in my army.")]
        public async Task NaturalParaphrase_ReturnsSameTypedUnitRequest(string message)
        {
            var transport = new FakeTransport(Completion(200, UnitJson));
            var provider = new OpenRouterCommanderProvider(DummyKey, transport);

            CommanderSemanticResult result = await ((ICommanderSemanticProvider)provider)
                .TranslateSemanticAsync(new CommanderSemanticProviderRequest(message, Context()),
                    CancellationToken.None);

            Assert.That(result.IsValid, Is.True, result.SafeExplanation);
            Assert.That(result.Outcome, Is.EqualTo(CommanderSemanticOutcome.Request));
            Assert.That(result.Nodes, Has.Count.EqualTo(1));
            Assert.That(result.Nodes[0].Type, Is.EqualTo(CommanderSemanticNodeType.EnsureUnitCount));
            Assert.That(result.Nodes[0].UnitType, Is.EqualTo(1));
            Assert.That(result.Nodes[0].Count, Is.EqualTo(10));
            Assert.That(transport.Calls, Is.EqualTo(1));
            Assert.That(transport.Uri.AbsoluteUri,
                Is.EqualTo("https://openrouter.ai/api/v1/chat/completions"));
            ChatBody body = JsonUtility.FromJson<ChatBody>(transport.Body);
            Assert.That(body.model, Is.EqualTo("openai/gpt-6-luna"));
            Assert.That(body.messages[0].role, Is.EqualTo("system"));
            Assert.That(body.messages[1].role, Is.EqualTo("user"));
            Assert.That(transport.Headers["Authorization"], Is.EqualTo("Bearer " + DummyKey));
            Assert.That(transport.Body, Does.Not.Contain(DummyKey));
            Assert.That(result.SafeExplanation, Does.Not.Contain(DummyKey));
        }

        [Test]
        public async Task DetachedContext_ExcludesIdsCoordinatesAndEnemyData()
        {
            var transport = new FakeTransport(Completion(200, UnitJson));
            await new OpenRouterCommanderProvider(DummyKey, transport).TranslateSemanticAsync(
                new CommanderSemanticProviderRequest("ten spearmen", Context()),
                CancellationToken.None);

            ChatBody body = JsonUtility.FromJson<ChatBody>(transport.Body);
            string sent = body.messages[1].content;
            Assert.That(sent, Does.Contain("Spearman"));
            Assert.That(sent, Does.Contain("Barracks"));
            Assert.That(sent, Does.Contain("RangedReinforcement"));
            Assert.That(sent, Does.Contain("population"));
            Assert.That(sent, Does.Not.Contain("PlayerId"));
            Assert.That(sent, Does.Not.Contain("BuildingId"));
            Assert.That(sent, Does.Not.Contain("TileX"));
            Assert.That(sent, Does.Not.Contain("VisibleEnemy"));
            Assert.That(sent, Does.Not.Contain("enemy-sentinel"));
            Assert.That(sent, Does.Not.Contain("987654321"));
            Assert.That(sent, Does.Not.Contain("432109876"));
        }

        [Test]
        public async Task LongPlayerMessage_IsRejectedBeforeTransport()
        {
            var transport = new FakeTransport(Completion(200, UnitJson));
            string message = new string('a', 1024) + "TAIL_SENTINEL";
            CommanderSemanticResult result = await new OpenRouterCommanderProvider(DummyKey, transport)
                .TranslateSemanticAsync(
                new CommanderSemanticProviderRequest(message, Context()), CancellationToken.None);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Nodes, Is.Empty);
            Assert.That(result.SafeExplanation.Length, Is.LessThan(200));
            Assert.That(result.SafeExplanation, Does.Not.Contain("TAIL_SENTINEL"));
            Assert.That(transport.Calls, Is.Zero);
        }

        [Test]
        public async Task OmittedKey_DoesNotCallTransport()
        {
            var transport = new FakeTransport(Completion(200, UnitJson));
            CommanderSemanticResult result = await new OpenRouterCommanderProvider("", transport)
                .TranslateSemanticAsync(new CommanderSemanticProviderRequest("ten spearmen", Context()),
                    CancellationToken.None);

            Assert.That(result.IsValid, Is.False);
            Assert.That(transport.Calls, Is.Zero);
            Assert.That(result.SafeExplanation, Does.Not.Contain(DummyKey));
        }

        [Test]
        public async Task QuotaFailure_DoesNotRetryOrEchoResponse()
        {
            var transport = new FakeTransport(new CommanderHttpResponse(429,
                "private upstream quota detail " + DummyKey));
            CommanderSemanticResult result = await new OpenRouterCommanderProvider(DummyKey, transport)
                .TranslateSemanticAsync(new CommanderSemanticProviderRequest("ten spearmen", Context()),
                    CancellationToken.None);

            Assert.That(result.IsValid, Is.False);
            Assert.That(transport.Calls, Is.EqualTo(1));
            Assert.That(result.SafeExplanation, Does.Contain("quota"));
            Assert.That(result.SafeExplanation, Does.Not.Contain("private upstream"));
            Assert.That(result.SafeExplanation, Does.Not.Contain(DummyKey));
        }

        [Test]
        public async Task Timeout_ReturnsSafeRejectionWithoutRetry()
        {
            var transport = new HangingTransport();
            CommanderSemanticResult result = await new OpenRouterCommanderProvider(DummyKey,
                transport, TimeSpan.FromMilliseconds(25)).TranslateSemanticAsync(
                    new CommanderSemanticProviderRequest("ten spearmen", Context()),
                    CancellationToken.None);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.SafeExplanation, Does.Contain("timed out"));
            Assert.That(result.SafeExplanation, Does.Not.Contain(DummyKey));
            Assert.That(transport.Calls, Is.EqualTo(1));
        }

        [TestCase("not json")]
        [TestCase("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10,\"playerId\":99}]}")]
        public async Task MalformedSemanticJson_IsRejectedWithoutAuthority(string modelText)
        {
            var transport = new FakeTransport(Completion(200, modelText));
            CommanderSemanticResult result = await new OpenRouterCommanderProvider(DummyKey, transport)
                .TranslateSemanticAsync(new CommanderSemanticProviderRequest("ten spearmen", Context()),
                    CancellationToken.None);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Nodes, Is.Empty);
            Assert.That(result.SafeExplanation, Does.Not.Contain(modelText));
        }

        [Test]
        public async Task OversizedModelText_IsRejectedWithoutEcho()
        {
            var transport = new FakeTransport(Completion(200,
                new string('x', CommanderSemanticJson.MaximumResponseCharacters + 1)));
            CommanderSemanticResult result = await new OpenRouterCommanderProvider(DummyKey, transport)
                .TranslateSemanticAsync(new CommanderSemanticProviderRequest("ten spearmen", Context()),
                    CancellationToken.None);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Nodes, Is.Empty);
            Assert.That(result.SafeExplanation.Length, Is.LessThan(200));
        }

        private static CommanderHttpResponse Completion(int code, string text)
        {
            return new CommanderHttpResponse(code, JsonUtility.ToJson(new CompletionBody
            {
                choices = new[] { new CompletionChoice
                {
                    message = new ChatMessage { content = text }
                } }
            }));
        }

        private static CommanderContext Context()
        {
            return new CommanderContext(987654321, 432109876,
                new CommanderResourceSnapshot(100, 200, 300, 400), 12, 20, 200, 2,
                "English", new List<CommanderBuildingSnapshot>
                {
                    new CommanderBuildingSnapshot(987654321, "Barracks", "Barracks", false,
                        new List<int> { 1 }, new List<int>(), 0)
                }, new List<CommanderUnitSnapshot>
                {
                    new CommanderUnitSnapshot(1, 4, 2)
                }, new List<CommanderBuildingSnapshot>(), new List<string>(),
                new List<CommanderGoalSnapshot>(),
                new List<CommanderVisibleResourceSnapshot>
                {
                    new CommanderVisibleResourceSnapshot("Wood", 987654321, 432109876, 50)
                }, new List<CommanderUnitOptionSnapshot>
                {
                    new CommanderUnitOptionSnapshot("Spearman", 1, 1, 2)
                }, new List<CommanderWorkerAllocationSnapshot>
                {
                    new CommanderWorkerAllocationSnapshot(ResourceType.Wood, 3)
                }, new List<CommanderVisibleEnemyMilitarySnapshot>
                {
                    new CommanderVisibleEnemyMilitarySnapshot(7, 9)
                });
        }

        private sealed class FakeTransport : ICommanderHttpTransport
        {
            private readonly CommanderHttpResponse response;
            public int Calls;
            public Uri Uri;
            public string Body;
            public IReadOnlyDictionary<string, string> Headers;
            public FakeTransport(CommanderHttpResponse response) => this.response = response;

            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string json,
                IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
            {
                Calls++;
                Uri = uri;
                Body = json;
                Headers = new Dictionary<string, string>(headers);
                return Task.FromResult(response);
            }
        }

        private sealed class HangingTransport : ICommanderHttpTransport
        {
            public int Calls;
            public async Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string json,
                IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
            {
                Calls++;
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return null;
            }
        }

        [Serializable] private sealed class ChatMessage { public string role; public string content; }
        [Serializable] private sealed class ChatBody { public string model; public ChatMessage[] messages; }
        [Serializable] private sealed class CompletionChoice { public ChatMessage message; }
        [Serializable] private sealed class CompletionBody { public CompletionChoice[] choices; }
    }
}
