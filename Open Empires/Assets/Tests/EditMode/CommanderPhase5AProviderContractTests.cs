using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5A")]
    public sealed class CommanderPhase5AProviderContractTests
    {
        private const string Key = "test-only-provider-secret";
        private const string Farm = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[{\"id\":\"farms\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":4},\"inputs\":{},\"dependsOn\":[]}]}";
        private const string StringCountFarm = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[{\"id\":\"farms\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":\"4\"},\"inputs\":{},\"dependsOn\":[]}]}";
        private const string Spearmen = "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10}]}";

        [Test]
        public async Task DynamicFarm_UsesRegistryVocabularyAndReturnsTypedPlan()
        {
            var transport = new SequenceTransport(Completion(Farm));
            var result = await Translate(transport, "make four farms");

            Assert.That(result.IsValid, Is.True, result.SafeExplanation);
            Assert.That(result.Outcome, Is.EqualTo(CommanderSemanticOutcome.DynamicPlan));
            Assert.That(result.DynamicPlan, Is.Not.Null);
            Assert.That(result.DynamicPlan.Nodes[0].Primitive.Mechanic, Is.EqualTo(CommanderDynamicMechanic.Build));
            Assert.That(result.DynamicPlan.Nodes[0].Parameter<string>("building"), Is.EqualTo("building:Farm"));
            Assert.That(transport.Bodies, Has.Count.EqualTo(1));
            ChatBody body = JsonUtility.FromJson<ChatBody>(transport.Bodies[0]);
            Assert.That(body.model, Is.EqualTo("openai/gpt-6-luna"));
            Assert.That(body.max_tokens, Is.EqualTo(4096));
            Assert.That(transport.Bodies[0], Does.Not.Contain("response_format"));
            string instruction = body.messages[0].content;
            Assert.That(instruction, Does.Contain("\"outcome\":\"DynamicPlan\""));
            Assert.That(instruction, Does.Contain("building:Farm"));
            foreach (var primitive in CommanderDynamicPrimitiveRegistry.All)
            {
                Assert.That(instruction, Does.Contain(primitive.Id), primitive.Id);
                foreach (var field in primitive.Parameters)
                    Assert.That(instruction, Does.Contain(field.Name), primitive.Id + "." + field.Name);
                foreach (var input in primitive.Inputs)
                    Assert.That(instruction, Does.Contain(input.Role), primitive.Id + "." + input.Role);
            }
            Assert.That(instruction, Does.Contain("never authorize"));
            Assert.That(instruction, Does.Contain("question"));
            Assert.That(instruction, Does.Contain("Castle"));
            Assert.That(instruction, Does.Contain("StrategicObjective"));
            Assert.That(transport.Bodies[0], Does.Not.Contain(Key));
        }

        [Test]
        public async Task OrdinaryKnownUnitRequest_RemainsOneCallWithTypedCount()
        {
            var transport = new SequenceTransport(Completion(Spearmen));
            var result = await Translate(transport, "make 10 spearmen");

            Assert.That(result.IsValid, Is.True, result.SafeExplanation);
            Assert.That(result.Outcome, Is.EqualTo(CommanderSemanticOutcome.Request));
            Assert.That(result.Nodes, Has.Count.EqualTo(1));
            Assert.That(result.Nodes[0].Count, Is.EqualTo(10));
            Assert.That(transport.Bodies, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task SharedWorkerComposition_PromptIncludesOneSelectionAndStrictExample()
        {
            var transport = new SequenceTransport(Completion(Spearmen));
            await Translate(transport, "send two of three idle workers to sheep and the third to build a mill near berries");

            string instruction = JsonUtility.FromJson<ChatBody>(transport.Bodies[0]).messages[0].content;
            Assert.That(instruction.Contains("one shared worker selection"), Is.True,
                "System message must describe one shared worker set before partitioning.");
            Assert.That(instruction.Contains("partition-workers"), Is.True,
                "System message must expose the registry partition mechanic.");
            var example = CommanderSemanticJson.Parse(Example(instruction, "SharedWorkersExample:"));
            Assert.That(example.IsValid, Is.True, example.SafeExplanation);
            Assert.That(example.Outcome, Is.EqualTo(CommanderSemanticOutcome.DynamicPlan));
            Assert.That(example.DynamicPlan.Nodes, Has.Count.EqualTo(7));
            Assert.That(example.DynamicPlan.Nodes[0].Primitive.Mechanic, Is.EqualTo(CommanderDynamicMechanic.SelectWorkers));
            Assert.That(example.DynamicPlan.Nodes[1].Primitive.Mechanic, Is.EqualTo(CommanderDynamicMechanic.PartitionWorkers));
            Assert.That(example.DynamicPlan.Nodes[2].Primitive.Mechanic, Is.EqualTo(CommanderDynamicMechanic.PartitionWorkers));
            Assert.That(example.DynamicPlan.Nodes[5].Primitive.Mechanic, Is.EqualTo(CommanderDynamicMechanic.Build));
            Assert.That(example.DynamicPlan.Nodes[6].Primitive.Mechanic, Is.EqualTo(CommanderDynamicMechanic.AllocateWorkers));
        }

        [Test]
        public async Task RepeatedPlacementAndExactProducers_PromptIncludesStrictDynamicExample()
        {
            var transport = new SequenceTransport(Completion(Spearmen));
            await Translate(transport, "build two barracks near my town center and train ten new spearmen in them");

            string instruction = JsonUtility.FromJson<ChatBody>(transport.Bodies[0]).messages[0].content;
            Assert.That(instruction.Contains("repeated placed construction"), Is.True,
                "System message must route repeated placement through DynamicPlan.");
            Assert.That(instruction.Contains("exact producer set"), Is.True,
                "System message must bind production to the new building result.");
            Assert.That(instruction.Contains("simple known requests"), Is.True,
                "Known fast paths must remain available.");
            var example = CommanderSemanticJson.Parse(Example(instruction, "ExactProducersExample:"));
            Assert.That(example.IsValid, Is.True, example.SafeExplanation);
            Assert.That(example.Outcome, Is.EqualTo(CommanderSemanticOutcome.DynamicPlan));
            Assert.That(example.DynamicPlan.Nodes, Has.Count.EqualTo(3));
            Assert.That(example.DynamicPlan.Nodes[1].Parameter<int>("count"), Is.EqualTo(2));
            Assert.That(example.DynamicPlan.Nodes[2].Parameter<string>("quantityMode"), Is.EqualTo("New"));
        }

        [Test]
        public async Task NumericStringCount_RequestsOneSchemaOnlyRepairAndAcceptsExactTemplate()
        {
            var transport = new SequenceTransport(Completion(StringCountFarm), Completion(Farm));
            var result = await Translate(transport, "make four farms");

            Assert.That(result.IsValid, Is.True, result.SafeExplanation);
            Assert.That(result.Outcome, Is.EqualTo(CommanderSemanticOutcome.DynamicPlan));
            Assert.That(transport.Bodies, Has.Count.EqualTo(2));
            ChatBody repair = JsonUtility.FromJson<ChatBody>(transport.Bodies[1]);
            string repairText = string.Join(" ", Array.ConvertAll(repair.messages, x => x.content));
            Assert.That(repairText, Does.Contain("schema").IgnoreCase);
            Assert.That(repairText, Does.Contain("building:Farm"));
            Assert.That(repairText, Does.Contain("\"count\":4"));
            Assert.That(repairText, Does.Not.Contain(Key));
            Assert.That(repair.max_tokens, Is.EqualTo(4096));
        }

        [Test]
        public async Task SchemaRepairChangingEffect_IsRejectedWithoutThirdCall()
        {
            string changed = Farm.Replace("building:Farm", "building:Mill");
            var transport = new SequenceTransport(Completion(StringCountFarm), Completion(changed), Completion(Farm));
            var result = await Translate(transport, "make four farms");

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.DynamicPlan, Is.Null);
            Assert.That(result.Nodes, Is.Empty);
            Assert.That(transport.Bodies, Has.Count.EqualTo(2));
        }

        [TestCase("{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[{\"id\":\"x\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":\"4\",\"tileX\":9},\"inputs\":{},\"dependsOn\":[]}]}")]
        [TestCase("{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[{\"id\":\"x\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":\"4\"},\"inputs\":{},\"dependsOn\":[],\"approved\":true}]}")]
        [TestCase("{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[{\"id\":\"x\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":\"4\",\"count\":5},\"inputs\":{},\"dependsOn\":[]}]}")]
        [TestCase("{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[{\"id\":\"x\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Unknown\",\"count\":\"4\"},\"inputs\":{},\"dependsOn\":[]}]}")]
        [TestCase("{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[{\"id\":\"x\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":\"4\"},\"inputs\":{},\"dependsOn\":[\"missing\"]}]}")]
        [TestCase("{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[{\"id\":\"x\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":\"4\"},\"inputs\":{},\"dependsOn\":[],}]} ")]
        public async Task NonNumericOrStructurallyInvalidResponse_NeverAttemptsRepair(string initial)
        {
            var transport = new SequenceTransport(Completion(initial), Completion(Farm));
            var result = await Translate(transport, "make four farms");

            Assert.That(result.IsValid, Is.False);
            Assert.That(transport.Bodies, Has.Count.EqualTo(1));
            Assert.That(result.SafeExplanation, Does.Not.Contain(initial));
        }

        [Test]
        public async Task TruncatedEligibleResponse_DoesNotRequestRepair()
        {
            var transport = new SequenceTransport(Completion(StringCountFarm, "length"), Completion(Farm));
            var result = await Translate(transport, "make four farms");

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.SafeExplanation, Does.Contain("truncated"));
            Assert.That(transport.Bodies, Has.Count.EqualTo(1));
        }

        [Test]
        public void SemanticTokenBudget_CanBeConfiguredOnlyInsideSafeRange()
        {
            var transport = new SequenceTransport(Completion(Farm));
            foreach (int invalid in new[] { 1023, 4097 })
            {
                var exception = Assert.Throws<TargetInvocationException>(() =>
                    Activator.CreateInstance(typeof(OpenRouterCommanderProvider), Key, transport, null, invalid));
                Assert.That(exception.InnerException, Is.TypeOf<ArgumentOutOfRangeException>());
            }
        }

        private static Task<CommanderSemanticResult> Translate(SequenceTransport transport, string message)
        {
            return new OpenRouterCommanderProvider(Key, transport).TranslateSemanticAsync(
                new CommanderSemanticProviderRequest(message, Context()), CancellationToken.None);
        }

        private static string Example(string instruction, string marker)
        {
            int start = instruction.IndexOf(marker, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), marker + " must be present in the request body");
            start += marker.Length;
            int end = instruction.IndexOf('\n', start);
            Assert.That(end, Is.GreaterThan(start), marker + " must end at a newline");
            return instruction.Substring(start, end - start);
        }

        private static CommanderContext Context()
        {
            return new CommanderContext(987654321, 432109876,
                new CommanderResourceSnapshot(100, 200, 300, 400), 12, 20, 200, 2,
                "English", new List<CommanderBuildingSnapshot>(), new List<CommanderUnitSnapshot>(),
                new List<CommanderBuildingSnapshot>(), new List<string>(), new List<CommanderGoalSnapshot>(),
                new List<CommanderVisibleResourceSnapshot>(), new List<CommanderUnitOptionSnapshot>(),
                new List<CommanderWorkerAllocationSnapshot>(), new List<CommanderVisibleEnemyMilitarySnapshot>(),
                canonicalUnitIds: new[] { "unit:1" }, canonicalBuildingIds: new[]
                    { "building:Farm", "building:Mill", "building:Barracks" });
        }

        private static CommanderHttpResponse Completion(string content, string finish = "stop")
        {
            return new CommanderHttpResponse(200, JsonUtility.ToJson(new CompletionBody
            {
                choices = new[] { new CompletionChoice
                {
                    message = new ChatMessage { content = content }, finish_reason = finish
                } }
            }));
        }

        private sealed class SequenceTransport : ICommanderHttpTransport
        {
            private readonly Queue<CommanderHttpResponse> responses;
            public readonly List<string> Bodies = new List<string>();

            public SequenceTransport(params CommanderHttpResponse[] responses)
            {
                this.responses = new Queue<CommanderHttpResponse>(responses);
            }

            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string json,
                IReadOnlyDictionary<string, string> headers, CancellationToken token)
            {
                Assert.That(uri.AbsoluteUri, Is.EqualTo("https://openrouter.ai/api/v1/chat/completions"));
                Assert.That(headers["Authorization"], Is.EqualTo("Bearer " + Key));
                Bodies.Add(json);
                return Task.FromResult(responses.Dequeue());
            }
        }

        [Serializable] private sealed class ChatMessage { public string role; public string content; }
        [Serializable] private sealed class ChatBody { public string model; public ChatMessage[] messages; public int max_tokens; }
        [Serializable] private sealed class CompletionChoice { public ChatMessage message; public string finish_reason; }
        [Serializable] private sealed class CompletionBody { public CompletionChoice[] choices; }
    }
}
