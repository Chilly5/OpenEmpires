using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public sealed partial class CommanderEconomyClarificationTests
    {
        [Test]
        public async Task CountReplies_FillPendingSlotsWithoutProduction()
        {
            foreach (string reply in new[] { "4", "four", "four villagers" })
            using (var f = new ChatFixture(new SequenceSemanticProvider(Draft())))
            {
                await f.Chat.SubmitMessageAsync("gather food from sheep with idle villagers");
                Assert.That(Pending(f.Chat), Is.Not.Null, "Clarification must retain a detached executable-intent draft, not just text.");
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                var result = await f.Chat.SubmitMessageAsync(reply);
                Assert.That(result?.Success, Is.True);
                var allocation = f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation;
                Assert.That(allocation.Count, Is.EqualTo(4));
                Assert.That(allocation.Destination.Resource, Is.EqualTo(ResourceType.Food));
                Assert.That(allocation.Destination.SourceKind, Is.EqualTo(ResourceSourceKind.Sheep));
                Assert.That(allocation.Workers.State, Is.EqualTo(CommanderWorkerState.Idle));
                Assert.That(allocation.Mode, Is.EqualTo(CommanderWorkerAllocationMode.SelectedCount));
                Assert.That(f.Provider.Calls, Is.EqualTo(1), "Whole count fills locally, without another paid translation.");
                Assert.That(f.Economy.Manager.Goals.OfType<EnsureUnitCountGoal>(), Is.Empty);
                Assert.That(Pending(f.Chat), Is.Null);
            }
        }

        [Test]
        public async Task MultiFieldReplies_CancelOrBoundAmbiguousContinuation()
        {
            using (var f = new ChatFixture(new SequenceSemanticProvider(
                Draft(destination: false).Replace("\"state\":\"Idle\"", "\"state\":\"Any\""),
                Draft(source: "Any", resource: "Gold"))))
            {
                await f.Chat.SubmitMessageAsync("send villagers");
                await f.Chat.SubmitMessageAsync("idle villagers to gold");
                Assert.That(f.Chat.PendingClarification.Draft.Workers.State, Is.EqualTo(CommanderWorkerState.Idle),
                    "An explicit worker criterion correction must be retained while filling destination.");
                await f.Chat.SubmitMessageAsync("three");
                Assert.That(f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation.Workers.State, Is.EqualTo(CommanderWorkerState.Idle));
            }
            foreach (string invalid in new[] {
                Draft().Replace("[\"Count\"]", "[\"Destination\"]"),
                Draft().Replace("\"countMode\":\"Exact\"", "\"countMode\":\"Exact\",\"count\":4"),
                Draft().Replace("\"state\":\"Idle\"", "\"state\":\"Idle\",\"entityId\":5"),
                Draft().Replace("\"state\":\"Idle\"", "\"state\":\"Idle\",\"currentResource\":\"Wood\""),
                Draft().Replace("\"sourceKind\":\"Sheep\"", "\"sourceKind\":\"GoldMine\""),
                Draft().Replace("\"Clarify\"", "\"Answer\"") })
                Assert.That(CommanderSemanticJson.Parse(invalid).IsValid, Is.False, "Malformed or contradictory drafts reject atomically.");
            using (var f = new ChatFixture(new SequenceSemanticProvider(Draft(destination: false), Draft(source: "Any", resource: "Gold"))))
            {
                await f.Chat.SubmitMessageAsync("send villagers");
                await f.Chat.SubmitMessageAsync("to gold");
                Assert.That(f.Provider.LastRequest.GetType().GetProperty("SerializedPendingClarification")?.GetValue(f.Provider.LastRequest)?.ToString(), Does.Contain("AllocateWorkers"));
                await f.Chat.SubmitMessageAsync("three");
                var allocation = f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation;
                Assert.That(allocation.Count, Is.EqualTo(3)); Assert.That(allocation.Destination.Resource, Is.EqualTo(ResourceType.Gold));
            }
            foreach (string cancel in new[] { "cancel", "never mind", "forget it" })
            using (var f = new ChatFixture(new SequenceSemanticProvider(Draft())))
            {
                await f.Chat.SubmitMessageAsync("gather food"); await f.Chat.SubmitMessageAsync(cancel);
                Assert.That(Pending(f.Chat), Is.Null); Assert.That(f.Economy.Manager.Goals, Is.Empty); Assert.That(f.Provider.Calls, Is.EqualTo(1));
            }
            using (var f = new ChatFixture(new SequenceSemanticProvider(Draft(), Draft(), Draft(), Draft())))
            {
                await f.Chat.SubmitMessageAsync("gather food");
                foreach (string ambiguous in new[] { "yes", "those", "there" }) await f.Chat.SubmitMessageAsync(ambiguous);
                Assert.That(Pending(f.Chat), Is.Null); Assert.That(f.Economy.Manager.Goals, Is.Empty);
            }
            foreach (var replacement in new[] { new[] { "to those sources", Draft(source: "Any") }, new[] { "to wood", Draft(source: "Tree", resource: "Wood") } })
            using (var f = new ChatFixture(new SequenceSemanticProvider(Draft(), replacement[1])))
            {
                await f.Chat.SubmitMessageAsync("gather food from sheep"); await f.Chat.SubmitMessageAsync(replacement[0]);
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Chat.PendingClarification.Draft.Destination.SourceKind, Is.EqualTo(ResourceSourceKind.Sheep), "Unrequested source replacement must not be retained.");
                await f.Chat.SubmitMessageAsync("four");
                Assert.That(f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation.Destination.SourceKind, Is.EqualTo(ResourceSourceKind.Sheep));
            }
        }

        [Test]
        public async Task IndependentProduction_SupersedesWorkerClarification()
        {
            using (var f = new ChatFixture(new SequenceSemanticProvider(Draft(), Request("{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4}"))))
            {
                await f.Chat.SubmitMessageAsync("gather food");
                Assert.That(Pending(f.Chat), Is.Not.Null);
                await f.Chat.SubmitMessageAsync("build 4 spearmen");
                Assert.That(Pending(f.Chat), Is.Null); Assert.That(f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>(), Is.Empty);
                Assert.That(f.Economy.Manager.Goals.OfType<EnsureUnitCountGoal>().Single().TargetTotal, Is.EqualTo(4));
            }
        }

        [Test]
        public async Task ResetRuntimeAndLateReply_CannotResumeOldPending()
        {
            foreach (string reset in new[] { "conversation", "initialize", "runtime", "dispose" })
            using (var f = new ChatFixture(new SequenceSemanticProvider(Draft(), Draft())))
            using (var replacement = new EconomyFixture())
            using (var replacementDispatcher = new CommanderIntentDispatcher(replacement.Simulation, replacement.Manager))
            {
                await f.Chat.SubmitMessageAsync("gather food"); Assert.That(Pending(f.Chat), Is.Not.Null);
                f.Provider.Hold = true;
                Task<CommanderAIChatSubmission> late = f.Chat.SubmitMessageAsync("to berries");
                if (reset == "conversation") f.Chat.ResetConversation();
                else if (reset == "initialize") f.Chat.Initialize(f.Provider, f.Economy.Simulation, f.Economy.Manager, f.Dispatcher);
                else if (reset == "runtime") f.Chat.Initialize(f.Provider, replacement.Simulation, replacement.Manager, replacementDispatcher);
                else f.DestroyHost();
                Assert.That(Pending(f.Chat), Is.Null);
                f.Provider.Release(); await late;
                Assert.That(Pending(f.Chat), Is.Null); Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(replacement.Manager.Goals, Is.Empty);
            }
        }

        private static object Pending(CommanderChatUI chat) => typeof(CommanderChatUI).GetProperty("PendingClarification")?.GetValue(chat);
        private static string Draft(bool destination = true, string source = "Sheep", string resource = "Food") =>
            "{\"outcome\":\"Clarify\",\"message\":\"How many villagers?\",\"pending\":{\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\",\"countMode\":\"Exact\",\"workers\":{\"state\":\"Idle\"}"
            + (destination ? ",\"destination\":{\"resource\":\"" + resource + "\",\"sourceKind\":\"" + source + "\"}" : "")
            + "},\"missingFields\":[\"Count\"" + (destination ? "" : ",\"Destination\"") + "]}";

        private sealed class ChatFixture : IDisposable
        {
            internal readonly EconomyFixture Economy = new EconomyFixture();
            internal readonly CommanderIntentDispatcher Dispatcher;
            internal readonly SequenceSemanticProvider Provider;
            internal readonly CommanderChatUI Chat;
            private readonly GameObject host;
            internal ChatFixture(SequenceSemanticProvider provider)
            {
                Provider = provider; Dispatcher = new CommanderIntentDispatcher(Economy.Simulation, Economy.Manager);
                host = new GameObject("EconomyClarificationChat"); Chat = host.AddComponent<CommanderChatUI>();
                Chat.Initialize(provider, Economy.Simulation, Economy.Manager, Dispatcher);
            }
            internal void DestroyHost()
            {
                if (host == null) return;
                // EditMode AddComponent does not run Awake/OnDestroy automatically;
                // explicitly exercise the same disposal callback before destroying this fixture.
                if (!Application.isPlaying) typeof(CommanderChatUI).GetMethod("OnDestroy",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Chat, null);
                UnityEngine.Object.DestroyImmediate(host);
            }
            public void Dispose() { DestroyHost(); Dispatcher.Dispose(); Economy.Dispose(); }
        }

        private sealed class SequenceSemanticProvider : ICommanderAIProvider, ICommanderSemanticProvider
        {
            private readonly string[] results; private TaskCompletionSource<bool> release;
            internal int Calls; internal bool Hold; internal CommanderSemanticProviderRequest LastRequest;
            internal SequenceSemanticProvider(params string[] results) => this.results = results;
            public async Task<CommanderSemanticResult> TranslateSemanticAsync(CommanderSemanticProviderRequest request, CancellationToken token)
            {
                LastRequest = request; int index = Calls++;
                if (Hold) { release = new TaskCompletionSource<bool>(); await release.Task; }
                return CommanderSemanticJson.Parse(results[Math.Min(index, results.Length - 1)]);
            }
            internal void Release() => release?.TrySetResult(true);
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request, CancellationToken token) =>
                Task.FromResult(CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure, "Legacy route is not expected."));
        }
    }
}
