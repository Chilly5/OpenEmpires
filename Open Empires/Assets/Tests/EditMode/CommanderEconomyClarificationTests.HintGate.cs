using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public sealed partial class CommanderEconomyClarificationTests
    {
        private const string LongbowRequest = "{\"outcome\":\"Request\",\"executionOrder\":\"Independent\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Longbowman\",\"count\":3,\"quantityMode\":\"New\"}]}";

        [TestCase("longbowmna", true)]
        [TestCase("Longbowman", false)]
        public async Task NearName_PartialCountRetainsOriginalNameRequirement(string name, bool needsName)
        {
            var node = (JObject)JObject.Parse(LongbowRequest)["nodes"][0];
            node.Remove("count");
            string proposal = GraphDraft(new JArray(node), "Independent", 0, "count", "Count", new JValue(3));
            using (var f = new HintFixture(proposal))
            {
                string original = "Train new " + name;
                await f.Chat.SubmitMessageAsync(original);
                var pending = f.Chat.PendingClarification;
                Assert.That(pending, Is.Not.Null, f.Chat.DisplayedTranscript);
                Assert.That(JToken.DeepEquals(pending.GraphDraft.ToPendingJson(), JObject.Parse(proposal)["pending"]), Is.True);
                await f.Chat.SubmitMessageAsync("3");
                if (needsName)
                {
                    Assert.That(f.Economy.Manager.Goals, Is.Empty, "Count must not confirm the name.");
                    Assert.That(f.Economy.Commands(), Is.Empty);
                    Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(pending.RequestTicket));
                    Assert.That(f.Chat.PendingClarification.OriginalText, Is.EqualTo(original));
                    Assert.That(f.Chat.PendingClarification.RuntimeGeneration, Is.EqualTo(pending.RuntimeGeneration));
                    Assert.That(JToken.DeepEquals(f.Chat.PendingClarification.GraphDraft.ToPendingJson(), JObject.Parse(LongbowRequest)), Is.True);
                    await f.Chat.SubmitMessageAsync("yes");
                }
                Assert.That(f.Chat.PendingClarification, Is.Null, f.Chat.DisplayedTranscript);
                var goal = f.Economy.Manager.Goals.Single();
                Assert.That(goal.RequestAuthority.RequestId, Is.EqualTo(pending.RequestTicket.Id));
                Assert.That(goal.RequestAuthority.OriginalInput, Is.EqualTo(original));
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task NearName_PartialCompoundCountAndSourceStillNeedNameAndGameplayApproval(bool correction)
        {
            var complete = JObject.Parse(LongbowRequest);
            complete["executionOrder"] = "Sequential";
            ((JObject)complete["nodes"][0])["constraints"] = new JArray(JObject.Parse("{\"type\":\"MaximumQueue\",\"amount\":2}"));
            ((JArray)complete["nodes"]).Add(JObject.Parse("{\"type\":\"WatchFutureUnits\",\"unit\":\"Villager\",\"count\":5,\"producer\":\"TownCenter\",\"producerOrdinal\":1,\"action\":\"Gather\",\"resource\":\"Wood\",\"sourceKind\":\"Tree\",\"dependsOn\":[0]}"));
            var partial = (JArray)complete["nodes"].DeepClone();
            ((JObject)partial[0]).Remove("count");
            if (!correction) ((JObject)partial[1]).Remove("sourceKind");
            else partial[1]["sourceKind"] = "Any";
            var proposal = JObject.Parse(GraphDraft(partial, "Sequential", 0, "count", "Count", new JValue(3)));
            if (!correction) ((JArray)proposal["missingSlots"]).Add(JObject.Parse("{\"node\":1,\"path\":\"sourceKind\",\"type\":\"SourceKind\",\"alternatives\":[\"Tree\"]}"));
            using (var f = new HintFixture(proposal.ToString(Formatting.None)))
            {
                f.Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter, f.Economy.Base.x, f.Economy.Base.y, false, true).AutoProduceVillagers = false;
                const string original = "Train new longbowmna with maximum queue two, then send next five Villagers from my first Town Center to Wood";
                await f.Chat.SubmitMessageAsync(original);
                var pending = f.Chat.PendingClarification;
                Assert.That(pending, Is.Not.Null, f.Chat.DisplayedTranscript);
                await f.Chat.SubmitMessageAsync(correction ? "change Any to Tree" : "from trees");
                Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(pending.RequestTicket));
                await f.Chat.SubmitMessageAsync("3");
                Assert.That(f.Chat.PendingActionPlan, Is.Null, "Slot completion is not name confirmation.");
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Economy.Commands(), Is.Empty);
                Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(pending.RequestTicket));
                Assert.That(JToken.DeepEquals(f.Chat.PendingClarification.GraphDraft.ToPendingJson(), complete), Is.True);
                await f.Chat.SubmitMessageAsync("Longbowman");
                Assert.That(f.Chat.PendingClarification, Is.Null);
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Chat.PendingActionPlan, Is.Not.Null, f.Chat.DisplayedTranscript);
                var nodes = f.Chat.PendingActionPlan.Interpretation.Nodes;
                Assert.That(nodes[0].Count, Is.EqualTo(3));
                Assert.That(((MaximumQueueConstraint)nodes[0].Constraints.Single()).MaximumQueue, Is.EqualTo(2));
                Assert.That(nodes[1].Count, Is.EqualTo(5));
                Assert.That(nodes[1].ProducerOrdinal, Is.EqualTo(1));
                Assert.That(nodes[1].SourceKind, Is.EqualTo(ResourceSourceKind.Tree));
                Assert.That(nodes[1].DependsOn, Is.EqualTo(new[] { 0 }));
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(2), f.Chat.DisplayedTranscript);
                Assert.That(f.Economy.Manager.Goals.All(g => g.RequestAuthority.RequestId == pending.RequestTicket.Id), Is.True);
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
            }
        }

        [TestCase("CanonicalName", "Longbowman")]
        [TestCase("Confirmation", "yes")]
        public async Task NearName_ExplicitNameSlotIsNotAskedAgainAfterCount(string type, string reply)
        {
            var node = (JObject)JObject.Parse(LongbowRequest)["nodes"][0];
            node.Remove("count");
            if (type == "CanonicalName") node.Remove("unit");
            var proposal = JObject.Parse(GraphDraft(new JArray(node), "Independent", 0, "unit", type, new JValue("Longbowman")));
            ((JArray)proposal["missingSlots"]).Add(JObject.Parse("{\"node\":0,\"path\":\"count\",\"type\":\"Count\",\"alternatives\":[3]}"));
            using (var f = new HintFixture(proposal.ToString(Formatting.None)))
            {
                await f.Chat.SubmitMessageAsync("Train new longbowmna");
                var ticket = f.Chat.PendingClarification.RequestTicket;
                await f.Chat.SubmitMessageAsync(reply);
                Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(ticket));
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                await f.Chat.SubmitMessageAsync("3");
                Assert.That(f.Chat.PendingClarification, Is.Null, f.Chat.DisplayedTranscript);
                Assert.That(f.Economy.Manager.Goals.Single().RequestAuthority.RequestId, Is.EqualTo(ticket.Id));
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task NearName_LegacyWorkerDraftCannotDropUnrepresentableName()
        {
            const string proposal = "{\"outcome\":\"Clarify\",\"message\":\"How many?\",\"pending\":{\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\",\"countMode\":\"Exact\",\"workers\":{\"state\":\"Idle\"},\"destination\":{\"resource\":\"Wood\",\"sourceKind\":\"Tree\"}},\"missingFields\":[\"Count\"]}";
            using (var f = new HintFixture(proposal))
            {
                for (int i = 0; i < 3; i++) f.Economy.Worker(i);
                f.Economy.Node(ResourceType.Wood, 12);
                const string original = "Send idle villagers to Wood then train longbowmna";
                await f.Chat.SubmitMessageAsync(original);
                var ticket = f.Chat.PendingClarification.RequestTicket;
                await f.Chat.SubmitMessageAsync("3");
                Assert.That(f.Economy.Manager.Goals, Is.Empty, "Legacy count draft cannot omit the hinted action and execute.");
                Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(ticket));
                Assert.That(f.Chat.PendingClarification.OriginalText, Is.EqualTo(original));
                Assert.That(f.Chat.DisplayedTranscript, Does.Contain("Blocked"));
                Assert.That(JToken.DeepEquals(f.Chat.PendingClarification.GraphDraft.ToPendingJson(), JObject.Parse(proposal)), Is.True);
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Economy.Commands(), Is.Empty);
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
                await f.Chat.SubmitMessageAsync("no");
                Assert.That(f.Chat.PendingClarification, Is.Null);
            }
        }

        [TestCase("yes")]
        [TestCase("Longbowman")]
        public async Task NearName_RequestCannotExecuteUntilLocalNameConfirmation(string reply)
        {
            using (var f = new HintFixture(LongbowRequest))
            {
                await f.Chat.SubmitMessageAsync("Train three new longbowmna");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null, f.Chat.DisplayedTranscript);
                var ticket = f.Chat.PendingClarification.RequestTicket;
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Economy.Commands(), Is.Empty);
                await f.Chat.SubmitMessageAsync(reply);
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
                Assert.That(f.Chat.PendingClarification, Is.Null);
                var goal = f.Economy.Manager.Goals.Single();
                Assert.That(goal.RequestAuthority.RequestId, Is.EqualTo(ticket.Id));
                Assert.That(goal.RequestAuthority.OriginalInput, Is.EqualTo("Train three new longbowmna"));
            }
        }

        [Test]
        public async Task NearName_CompoundRetainsEveryFieldAndNeedsSeparateGameplayApproval()
        {
            var root = JObject.Parse(LongbowRequest);
            root["executionOrder"] = "Sequential";
            ((JArray)root["nodes"]).Add(JObject.Parse("{\"type\":\"WatchFutureUnits\",\"unit\":\"Villager\",\"count\":5,\"producer\":\"TownCenter\",\"producerOrdinal\":1,\"action\":\"Gather\",\"resource\":\"Wood\",\"sourceKind\":\"Tree\",\"dependsOn\":[0]}"));
            using (var f = new HintFixture(root.ToString(Formatting.None)))
            {
                f.Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter, f.Economy.Base.x, f.Economy.Base.y, false, true).AutoProduceVillagers = false;
                string original = "Train three new longbowmna, then send the next five Villagers from my first Town Center to Wood from trees";
                await f.Chat.SubmitMessageAsync(original);
                Assert.That(f.Chat.PendingClarification, Is.Not.Null, f.Chat.DisplayedTranscript);
                var ticket = f.Chat.PendingClarification.RequestTicket;
                Assert.That(f.Chat.PendingActionPlan, Is.Null);
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Economy.Commands(), Is.Empty);
                Assert.That(f.Chat.PendingActionPlan, Is.Not.Null, f.Chat.DisplayedTranscript);
                var nodes = f.Chat.PendingActionPlan.Interpretation.Nodes;
                Assert.That(nodes[0].Count, Is.EqualTo(3));
                Assert.That(nodes[0].QuantityMode, Is.EqualTo(CommanderProductionQuantityMode.New));
                Assert.That(nodes[1].DependsOn, Is.EqualTo(new[] { 0 }));
                Assert.That(nodes[1].Count, Is.EqualTo(5));
                Assert.That(nodes[1].ProducerOrdinal, Is.EqualTo(1));
                Assert.That(nodes[1].SourceKind, Is.EqualTo(ResourceSourceKind.Tree));
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(2), f.Chat.DisplayedTranscript);
                Assert.That(f.Economy.Manager.Goals.All(g => g.RequestAuthority.RequestId == ticket.Id), Is.True);
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task NearName_DynamicProposalCannotBypassNameThenGameplayConfirmation()
        {
            Assert.That(CommanderIntentCatalog.TryResolveUnit("Longbowman", out int unit), Is.True);
            string proposal = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[{\"id\":\"train\",\"mechanic\":\"produce\",\"parameters\":{\"unit\":\"unit:" + unit + "\",\"count\":3,\"quantityMode\":\"New\"},\"inputs\":{},\"dependsOn\":[]}]}";
            using (var f = new HintFixture(proposal))
            {
                await f.Chat.SubmitMessageAsync("Train three new longbowmna");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null, f.Chat.DisplayedTranscript);
                Assert.That(f.Chat.PendingActionPlan, Is.Null);
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Chat.PendingActionPlan, Is.Not.Null, f.Chat.DisplayedTranscript);
                Assert.That(f.Chat.PendingActionPlan.Interpretation.Outcome, Is.EqualTo(CommanderSemanticOutcome.DynamicPlan));
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task NearName_NoDismissesProposalOnly()
        {
            using (var f = new HintFixture(LongbowRequest))
            {
                await f.Chat.SubmitMessageAsync("Train three new Longbowmen");
                int existing = f.Economy.Manager.Goals.Count;
                Assert.That(existing, Is.EqualTo(1), f.Chat.DisplayedTranscript);
                await f.Chat.SubmitMessageAsync("Train three new longbowmna");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null);
                await f.Chat.SubmitMessageAsync("no");
                Assert.That(f.Chat.PendingClarification, Is.Null);
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(existing));
                Assert.That(f.Transport.Calls, Is.EqualTo(2));
            }
        }

        [Test]
        public async Task NearName_UnrelatedProposalKeepsOriginalDraftBlockedLocally()
        {
            using (var f = new HintFixture(LongbowRequest.Replace("Longbowman", "Spearman")))
            {
                const string original = "Train three new longbowmna";
                await f.Chat.SubmitMessageAsync(original);
                Assert.That(f.Chat.PendingClarification?.OriginalText, Is.EqualTo(original));
                Assert.That(f.Chat.DisplayedTranscript, Does.Contain("Blocked"));
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Chat.PendingClarification?.OriginalText, Is.EqualTo(original));
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Economy.Commands(), Is.Empty);
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
            }
        }

        [TestCase("Longbowman")]
        [TestCase("Longbowmen")]
        public async Task NearName_ExactNamesAndEstablishedAliasesDoNotRequireConfirmation(string name)
        {
            using (var f = new HintFixture(LongbowRequest))
            {
                await f.Chat.SubmitMessageAsync("Train three new " + name);
                Assert.That(f.Chat.PendingClarification, Is.Null);
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(1), f.Chat.DisplayedTranscript);
            }
        }

        [Test]
        public async Task NearName_ReadOnlyAnswerDoesNotCreatePendingAction()
        {
            using (var f = new HintFixture("{\"outcome\":\"Answer\",\"message\":\"Longbowman is an English unit.\"}"))
            {
                await f.Chat.SubmitMessageAsync("What is longbowmna?");
                Assert.That(f.Chat.PendingClarification, Is.Null);
                Assert.That(f.Chat.PendingActionPlan, Is.Null);
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Chat.DisplayedTranscript, Does.Contain("English unit"));
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task NearName_PrunedModelHintsStillRequireGameOwnedConfirmation()
        {
            using (var f = new HintFixture(LongbowRequest))
            {
                CommanderSemanticProviderRequest selected = null;
                for (int length = 0; length < 990; length += 10)
                {
                    string original = "Train three new longbowmna " + new string('\u754c', length);
                    var request = new CommanderSemanticProviderRequest(original,
                        new CommanderContextBuilder().Build(f.Economy.Simulation, f.Economy.Manager, original));
                    if (!CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096, out var pack, out _)) continue;
                    string user = (string)JObject.Parse(pack.Json)["messages"][1]["content"];
                    if (!user.Contains("nameInterpretations")) { selected = request; break; }
                }
                Assert.That(selected, Is.Not.Null, "Fixture must actually exercise optional hint pruning, not preflight rejection.");
                var result = await f.Provider.TranslateSemanticAsync(selected, CancellationToken.None);
                Assert.That(result.Outcome, Is.EqualTo(CommanderSemanticOutcome.Clarify));
                Assert.That(result.PendingGraphDraft, Is.Not.Null);
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
                Assert.That(f.Transport.Body, Does.Not.Contain("nameInterpretations"));
                Assert.That(f.Provider.LastInputEstimate, Is.LessThan(CommanderSemanticRequestPack.InputByteLimit));
            }
        }

        [Test]
        public async Task NearName_ConfirmedCrossCivilizationUnitStillFailsNormalAdmission()
        {
            using (var f = new HintFixture(LongbowRequest))
            {
                f.Economy.Simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.English });
                await f.Chat.SubmitMessageAsync("Train three new longbowmna");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null);
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals, Is.Empty, f.Chat.DisplayedTranscript);
                Assert.That(f.Economy.Commands(), Is.Empty);
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task NearName_OriginalTicketRejectsForeignResetAndReplay()
        {
            var pendingField = typeof(CommanderChatUI).GetField("pendingClarification",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            using (var f = new HintFixture(LongbowRequest))
            using (var foreign = new HintFixture(LongbowRequest))
            {
                await f.Chat.SubmitMessageAsync("Train three new longbowmna");
                var pending = f.Chat.PendingClarification;
                pendingField.SetValue(foreign.Chat, pending);
                await foreign.Chat.SubmitMessageAsync("yes");
                Assert.That(foreign.Economy.Manager.Goals, Is.Empty);
                Assert.That(foreign.Transport.Calls, Is.Zero);
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(1));
                pendingField.SetValue(f.Chat, pending);
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(1));
                f.Chat.ResetConversation();
                pendingField.SetValue(f.Chat, pending);
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(1));
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task NearName_LocalConfirmationRetainsFullStrictPayloadIncludingIndependentResourceAmount()
        {
            var root = JObject.Parse(LongbowRequest);
            root["executionOrder"] = "Sequential";
            ((JObject)root["nodes"][0])["constraints"] = new JArray(JObject.Parse("{\"type\":\"MaximumQueue\",\"amount\":2}"));
            ((JArray)root["nodes"]).Add(JObject.Parse("{\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\",\"countMode\":\"Exact\",\"count\":3,\"workers\":{\"state\":\"Gathering\",\"currentResource\":\"Wood\"},\"destination\":{\"resource\":\"Food\",\"sourceKind\":\"Sheep\"},\"resourceAmount\":400,\"resourceAmountMode\":\"AdditionalGathered\",\"dependsOn\":[0]}"));
            using (var f = new HintFixture(root.ToString(Formatting.None)))
            {
                const string original = "Train three new longbowmna with maximum queue two, then move three Wood gatherers to gather 400 additional Food from Sheep";
                await f.Chat.SubmitMessageAsync(original);
                var pending = f.Chat.PendingClarification;
                Assert.That(pending, Is.Not.Null, f.Chat.DisplayedTranscript);
                Assert.That(JToken.DeepEquals(pending.GraphDraft.ToPendingJson(), root), Is.True,
                    "The confirmation adapter must retain the entire strict payload, not rebuild selected fields.");
                var resolved = pending.GraphDraft.Resolve("yes");
                Assert.That(resolved.IsValid, Is.True);
                Assert.That(((MaximumQueueConstraint)resolved.Nodes[0].Constraints.Single()).MaximumQueue, Is.EqualTo(2));
                var allocation = resolved.Nodes[1].WorkerAllocation;
                Assert.That(allocation.Count, Is.EqualTo(3));
                Assert.That(allocation.Mode, Is.EqualTo(CommanderWorkerAllocationMode.SelectedCount));
                Assert.That(allocation.CountMode, Is.EqualTo(CommanderWorkerCountMode.Exact));
                Assert.That(allocation.Workers.CurrentResource, Is.EqualTo(ResourceType.Wood));
                Assert.That(allocation.Destination.SourceKind, Is.EqualTo(ResourceSourceKind.Sheep));
                Assert.That(allocation.ResourceAmount, Is.EqualTo(400));
                Assert.That(allocation.ResourceAmountMode, Is.EqualTo(CommanderResourceAmountMode.AdditionalGathered));
                Assert.That(resolved.Nodes[1].DependsOn, Is.EqualTo(new[] { 0 }));
                Assert.That(pending.OriginalText, Is.EqualTo(original));
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
                Assert.That(f.Economy.Manager.Goals, Is.Empty, "Detached local parsing carries no authority.");
            }
        }

        private sealed class HintTransport : ICommanderHttpTransport
        {
            internal int Calls;
            internal string Body;
            private readonly string response;
            internal HintTransport(string response) { this.response = response; }
            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string body,
                IReadOnlyDictionary<string, string> headers, CancellationToken token)
            {
                Calls++; Body = body;
                return Task.FromResult(new CommanderHttpResponse(200, new JObject { ["choices"] = new JArray(
                    new JObject { ["finish_reason"] = "stop", ["message"] = new JObject { ["content"] = response } }) }.ToString(Formatting.None)));
            }
        }

        private sealed class HintFixture : IDisposable
        {
            internal readonly EconomyFixture Economy = new EconomyFixture();
            internal readonly HintTransport Transport;
            internal readonly OpenRouterCommanderProvider Provider;
            internal readonly CommanderChatUI Chat;
            internal readonly CommanderIntentDispatcher Dispatcher;
            private readonly GameObject host;
            internal HintFixture(string response)
            {
                Economy.Simulation.SetPlayerCivilizations(new[] { Civilization.English, Civilization.English });
                Transport = new HintTransport(response);
                Provider = new OpenRouterCommanderProvider("fixture-not-a-credential", Transport);
                Dispatcher = new CommanderIntentDispatcher(Economy.Simulation, Economy.Manager);
                host = new GameObject("HintConfirmationFixture");
                Chat = host.AddComponent<CommanderChatUI>();
                Chat.Initialize(Provider, Economy.Simulation, Economy.Manager, Dispatcher);
            }
            public void Dispose()
            {
                typeof(CommanderChatUI).GetMethod("OnDestroy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Chat, null);
                UnityEngine.Object.DestroyImmediate(host); Dispatcher.Dispose(); Economy.Dispose();
            }
        }
    }
}
