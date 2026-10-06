using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace OpenEmpires.Tests
{
    public sealed partial class CommanderEconomyClarificationTests
    {
        [Test]
        public async Task ProviderProjectionAndSchema_ExposeAllEconomyFields()
        {
            using (var f = new EconomyFixture())
            {
                f.Worker(0);
                var pending = new CommanderPendingClarification(CommanderSemanticJson.Parse(Draft()).PendingDraft,
                    "gather food from sheep", "How many villagers?", 12, 99);
                var request = new CommanderSemanticProviderRequest("four", new CommanderContextBuilder().Build(f.Simulation, f.Manager), pendingClarification: pending);
                Assert.That(request.SerializedContext, Does.Contain("AllocateWorkers").And.Contain("workerCounts"));
                Assert.That(request.SerializedPendingClarification, Does.Contain("Sheep").And.Not.Contain("runtimeGeneration").And.Not.Contain("Sequence").And.Not.Contain("entityId"));
                var transport = new EconomyTransport(Draft());
                await new OpenRouterCommanderProvider("test-only-key", transport).TranslateSemanticAsync(request, CancellationToken.None);
                Assert.That(transport.Body, Does.Contain("SelectedCount").And.Contain("Additional").And.Contain("TargetTotal").And.Contain("AllMatching"));
                Assert.That(transport.Body, Does.Contain("currentResource").And.Contain("sourceKind").And.Contain("missingFields").And.Contain("Bounded pending"));
            }
        }

        [Test]
        public async Task SharedTextAndRegressionRoutes_PreserveExistingBehavior()
        {
            string allocate = Request(AllocationNode(new[] { "SelectedCount", "Exact", "4", "Idle", "", "Food", "Any" }));
            using (var f = new ChatFixture(new SequenceSemanticProvider(allocate,
                Request("{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4}"),
                Request("{\"type\":\"EnsureUnitCount\",\"unit\":\"Archer\",\"count\":5}"),
                Request("{\"type\":\"BuildStructure\",\"structure\":\"Mill\",\"count\":1,\"placement\":{\"anchor\":\"WorkedResource\",\"relation\":\"Near\",\"resource\":\"Food\"}}"),
                "{\"outcome\":\"Answer\",\"message\":\"Archers counter spearmen.\"}")))
            {
                var food = f.Economy.Node(ResourceType.Food, 6); f.Economy.Assigned(f.Economy.Worker(0), food);
                await f.Chat.SubmitMessageAsync("put four idle villagers on food");
                Assert.That(f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation.Count, Is.EqualTo(4));
                Assert.That(Pending(f.Chat), Is.Null);
                await f.Chat.SubmitMessageAsync("build 4 spearmen");
                await f.Chat.SubmitMessageAsync("make 5 archers");
                await f.Chat.SubmitMessageAsync("build a mill near worked berries");
                Assert.That(f.Economy.Manager.Goals.OfType<EnsureUnitCountGoal>().Select(g => g.TargetTotal), Is.EquivalentTo(new[] { 4, 5 }));
                Assert.That(f.Economy.Manager.Goals.OfType<BuildStructureGoal>().Single().PlacementAnchorSelector, Is.EqualTo(CommanderSemanticAnchorSelector.WorkedResource));
                int count = f.Economy.Manager.Goals.Count;
                await f.Chat.SubmitMessageAsync("what counters spearmen?");
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(count));
            }
        }

        private sealed class EconomyTransport : ICommanderHttpTransport
        {
            private readonly string response; internal string Body;
            internal EconomyTransport(string response) => this.response = response;
            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string body, IReadOnlyDictionary<string, string> headers, CancellationToken token)
            {
                Body = body;
                return Task.FromResult(new CommanderHttpResponse(200, "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"" + response.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}}]}"));
            }
        }
    }
}
