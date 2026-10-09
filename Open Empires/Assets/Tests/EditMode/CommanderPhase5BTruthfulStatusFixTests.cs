using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5B")]
    public sealed class CommanderPhase5BTruthfulStatusFixTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager manager;
        private CommanderIntentDispatcher dispatcher;
        private CommanderChatUI chat;
        private SemanticProvider provider;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            manager = new CommanderGoalManager(simulation, 0);
            dispatcher = new CommanderIntentDispatcher(simulation, manager);
            provider = new SemanticProvider();
            chat = new GameObject("TruthfulStatusFixture").AddComponent<CommanderChatUI>();
            chat.Initialize(provider, simulation, manager, dispatcher);
        }

        [TearDown]
        public void TearDown()
        {
            if (chat != null) UnityEngine.Object.DestroyImmediate(chat.gameObject);
            dispatcher?.Dispose();
            manager?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void AcceptedConstructionResponse_DoesNotClaimConstructionHasStarted()
        {
            var intent = new BuildStructureIntent(0, BuildingType.House);
            var resolution = new CommanderIntentResolution(CommanderIntentResolutionStatus.GoalCreated,
                intent, new BuildStructureGoal(0, BuildingType.House), CommanderIntentErrorCode.None, string.Empty);

            string response = new CommanderResponseGenerator().GenerateResolutionResponse(resolution);

            Assert.That(response, Does.Contain("Accepted"));
            Assert.That(response, Does.Not.Contain("will construct").IgnoreCase);
            Assert.That(response, Does.Not.Contain("building now").IgnoreCase);
        }

        [Test]
        public void PendingGoal_IsAcceptedButConstructionWaitIsWorking()
        {
            var goal = manager.SubmitBuildStructure(BuildingType.House);
            Assert.That(CommanderTaskBoardProjection.Capture(manager, null, 0).Cards.Single().Status.ToString(),
                Is.EqualTo("Accepted"));

            goal.SetStatus(CommanderGoalStatus.WaitingForConstruction, "Construction is in progress.");
            Assert.That(CommanderTaskBoardProjection.Capture(manager, null, 0).Cards.Single().Status,
                Is.EqualTo(CommanderTaskStatus.Working));
        }

        [Test]
        public void CompoundBlockedSibling_ProvidesItsOwnActionableReasonAtCardLevel()
        {
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1},"
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10,\"producerFromNode\":0}]}");
            var candidate = manager.PrepareActionPlan(parsed, "Build a barracks, then train ten spearmen", 0);
            var goals = manager.SubmitSemanticGraph(manager.ApproveActionPlan(candidate, 0));
            goals[1].SetStatus(CommanderGoalStatus.Blocked, "No eligible producer is available.");

            var card = CommanderTaskBoardProjection.Capture(manager, null, 0).Cards.Single();

            Assert.That(card.Status, Is.EqualTo(CommanderTaskStatus.Blocked));
            Assert.That(card.Blocker, Is.EqualTo("No eligible producer is available."));
            Assert.That(card.CurrentStep, Does.Contain("Produce"));
        }

        [Test]
        public void CompoundCompletedAllocation_DoesNotCompletePendingBuilding()
        {
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\",\"countMode\":\"Exact\","
                + "\"count\":1,\"workers\":{\"state\":\"Any\"},"
                + "\"destination\":{\"resource\":\"Wood\",\"sourceKind\":\"Any\"}},"
                + "{\"type\":\"BuildStructure\",\"structure\":\"House\",\"count\":1}]}");
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var candidate = manager.PrepareActionPlan(parsed, "Assign one worker to wood, then build a house", 0);
            var goals = manager.SubmitSemanticGraph(manager.ApproveActionPlan(candidate, 0));
            goals[0].SetStatus(CommanderGoalStatus.Completed, "Worker assigned.");

            var card = CommanderTaskBoardProjection.Capture(manager, null, 0).Cards.Single();

            Assert.That(card.Status, Is.Not.EqualTo(CommanderTaskStatus.Completed));
            Assert.That(card.Steps.Single(step => step.Label.Contains("Build")).Progress,
                Does.Contain("Completed buildings: 0 / 1"));
        }

        [Test]
        public async Task CurrentRequestBlocker_UpdatesTopStatusWithoutAddingTranscriptLine()
        {
            provider.Json = Build("House");
            await chat.SubmitMessageAsync("Build a house");
            var older = manager.Goals.Last();
            provider.Json = Build("Barracks");
            await chat.SubmitMessageAsync("Build a barracks");
            var current = manager.Goals.Last();
            int transcriptLines = TranscriptLineCount();

            older.SetStatus(CommanderGoalStatus.Blocked, "Older house blocker.");
            Refresh();
            Assert.That(TopStatus(), Does.Not.Contain("Older house blocker"));

            current.SetStatus(CommanderGoalStatus.Blocked, "No owned living villager is available.");
            Refresh();
            Assert.That(TopStatus(), Does.Contain("Blocked"));
            Assert.That(TopStatus(), Does.Contain("No owned living villager"));
            Assert.That(TranscriptLineCount(), Is.EqualTo(transcriptLines));
        }

        [Test]
        public async Task BlockedCard_ShowsReasonInFirstVisibleCardArea()
        {
            provider.Json = Build("House");
            await chat.SubmitMessageAsync("Build a house");
            manager.Goals.Single().SetStatus(CommanderGoalStatus.Blocked, "No owned living villager is available.");
            Refresh();

            var card = chat.transform.Find("CommanderCanvas/Panel/TaskBoard/Viewport/Content/TaskCard");
            Assert.That(card, Is.Not.Null);
            Assert.That(TextOf(card.GetChild(0)),
                Does.Contain("No owned living villager"), "The reason must be the first visible card line.");
        }

        [Test]
        public async Task CompoundPreview_ShowsAwaitingApprovalAndKeepsMinimizedState()
        {
            provider.Json = "{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"BuildStructure\",\"structure\":\"House\",\"count\":1},"
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4}]}";
            await chat.SubmitMessageAsync("Build a house and train four spearmen");
            Refresh();

            Assert.That(chat.PendingActionPlan, Is.Not.Null);
            Assert.That(manager.Goals, Is.Empty);
            Assert.That(chat.IsExpanded, Is.False);
            Assert.That(CompactStatus(), Does.Contain("Awaiting approval"));
        }

        private static string Build(string structure) => "{\"outcome\":\"Request\",\"nodes\":["
            + "{\"type\":\"BuildStructure\",\"structure\":\"" + structure + "\",\"count\":1}]}";

        private void Refresh() => typeof(CommanderChatUI).GetMethod("UpdatePresentation",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(chat, null);

        private string TopStatus() => TextOf(chat.transform.Find("CommanderCanvas/Panel/CommanderStatus"));

        private string CompactStatus() => TextOf(chat.transform.Find("CommanderCanvas/CompactCommander/Status"));

        private static string TextOf(Transform transform)
        {
            var component = transform.GetComponents<Component>()
                .FirstOrDefault(value => value != null && value.GetType().Name == "TextMeshProUGUI");
            return component == null ? string.Empty
                : (string)component.GetType().GetProperty("text").GetValue(component);
        }

        private int TranscriptLineCount() => ((System.Collections.ICollection)typeof(CommanderChatUI)
            .GetField("transcriptEntries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(chat)).Count;

        private sealed class SemanticProvider : ICommanderAIProvider, ICommanderSemanticProvider
        {
            internal string Json;
            public Task<CommanderSemanticResult> TranslateSemanticAsync(CommanderSemanticProviderRequest request,
                CancellationToken token) => Task.FromResult(CommanderSemanticJson.Parse(Json));
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request,
                CancellationToken token) => Task.FromResult(CommanderAIProviderResult.Rejected(
                    CommanderIntentErrorCode.ProviderFailure, "fixture"));
        }
    }
}
