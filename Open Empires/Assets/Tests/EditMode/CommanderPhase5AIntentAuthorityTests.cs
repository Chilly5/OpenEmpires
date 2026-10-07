using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5A")]
    public sealed class CommanderPhase5AIntentAuthorityTests
    {
        private SimulationConfig config;
        private GameSimulation sim;
        private CommanderGoalManager manager;
        private CommanderIntentDispatcher dispatcher;
        private CommanderChatUI chat;
        private CompoundProvider provider;
        private CommanderChatUI previous;
        private System.Reflection.FieldInfo singleton;
        private const string Compound = "{\"outcome\":\"Request\",\"nodes\":["
            + "{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1},"
            + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10,\"producerFromNode\":0}]}";

        [SetUp]
        public void Setup()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            sim = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            manager = new CommanderGoalManager(sim, 0);
            dispatcher = new CommanderIntentDispatcher(sim, manager);
            singleton = typeof(CommanderChatUI).GetField("instance",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            previous = CommanderChatUI.Instance;
            singleton.SetValue(null, null);
            chat = new GameObject("Phase5ACompoundConfirmation").AddComponent<CommanderChatUI>();
            chat.enabled = false;
            provider = new CompoundProvider(Compound);
            chat.Initialize(provider, sim, manager, dispatcher);
        }

        [TearDown]
        public void Cleanup()
        {
            if (chat != null) UnityEngine.Object.DestroyImmediate(chat.gameObject);
            singleton?.SetValue(null, previous);
            dispatcher?.Dispose();
            manager?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        // Removing normalized plan confirmation would execute this provider's extra roots.
        [Test]
        public async Task DynamicFarmRequest_PreviewsThenCommitsOnlyAfterOneLocalConfirmation()
        {
            provider = new CompoundProvider(CommanderPhase5ADynamicPlanTests.Farms);
            chat.Initialize(provider, sim, manager, dispatcher);
            await chat.SubmitMessageAsync("make four farms");
            Assert.That(chat.PendingActionPlan, Is.Not.Null);
            Assert.That(chat.PendingActionPlan.Preview, Does.Contain("Farm"));
            Assert.That(manager.Goals, Is.Empty);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
            await chat.SubmitMessageAsync("approve plan");
            Assert.That(manager.Goals.Count, Is.EqualTo(1));
            Assert.That(((BuildStructureGoal)manager.Goals[0]).Count, Is.EqualTo(4));
            Assert.That(provider.Calls, Is.EqualTo(1));
            await chat.SubmitMessageAsync("approve plan");
            Assert.That(manager.Goals.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task ConfirmedCompound_CancelPlanCancelsEveryLiveRoot()
        {
            await chat.SubmitMessageAsync("Build the barracks and use it for ten spearmen.");
            await chat.SubmitMessageAsync("approve plan");
            Assert.That(manager.Goals.Count, Is.EqualTo(2));
            await chat.SubmitMessageAsync("cancel plan");
            Assert.That(manager.Goals.All(g => g.Status == CommanderGoalStatus.Cancelled), Is.True);
            Assert.That(manager.Goals.All(g => g.RequestAuthority.Cancelled), Is.True);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public async Task SingleKnownIntent_RecordsItsRequestAuthorityBeforePublicationWithoutPreview()
        {
            provider = new CompoundProvider("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4}]}");
            chat.Initialize(provider, sim, manager, dispatcher);
            CommanderActionPlanCandidate observed = null;
            manager.GoalEventPublished += e =>
            { if (e.EventType == CommanderGoalEventType.GoalStarted) observed = e.Goal.RequestAuthority; };
            await chat.SubmitMessageAsync("make four spearmen");
            Assert.That(chat.PendingActionPlan, Is.Null);
            Assert.That(manager.Goals.Count, Is.EqualTo(1));
            Assert.That(observed, Is.Not.Null, "The first admission publication already needs request provenance.");
            Assert.That(observed.AuthorizationEvidence, Is.EqualTo("KnownIntentAutomatic"));
            Assert.That(observed.OriginalInput, Is.EqualTo("make four spearmen"));
            Assert.That(((EnsureUnitCountGoal)manager.Goals[0]).TargetTotal, Is.EqualTo(4));
            Assert.That(provider.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task UngroundedCompound_PreviewsAllEffects_ThenCommitsOnce()
        {
            await chat.SubmitMessageAsync("Build the barracks and use it for ten spearmen.");
            Assert.That(manager.Goals, Is.Empty, "A parsed compound is not player authorization.");
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
            Assert.That(chat.DisplayedTranscript, Does.Contain("Barracks"));
            Assert.That(chat.DisplayedTranscript, Does.Contain("Spearman"));
            Assert.That(chat.DisplayedTranscript, Does.Contain("10"));
            Assert.That(chat.DisplayedTranscript, Does.Contain("not started"));
            await chat.SubmitMessageAsync("approve plan");
            Assert.That(manager.Goals, Has.Count.EqualTo(2));
            Assert.That(provider.Calls, Is.EqualTo(1), "Consent is local, not a second synthesis.");
            await chat.SubmitMessageAsync("approve plan");
            Assert.That(manager.Goals, Has.Count.EqualTo(2), "Consumed approval cannot replay.");
            Assert.That(provider.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task ResetOrCancelPendingCompound_LeavesNoExecutablePrefix()
        {
            await chat.SubmitMessageAsync("Build the barracks and use it for ten spearmen.");
            Assert.That(manager.Goals, Is.Empty);
            await chat.SubmitMessageAsync("cancel plan");
            await chat.SubmitMessageAsync("approve plan");
            Assert.That(manager.Goals, Is.Empty);
            Assert.That(provider.Calls, Is.EqualTo(1));
            await chat.SubmitMessageAsync("Build the barracks and use it for ten spearmen.");
            chat.ResetConversation();
            await chat.SubmitMessageAsync("approve plan");
            Assert.That(manager.Goals, Is.Empty);
            Assert.That(provider.Calls, Is.EqualTo(2));
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void ParsedGraph_CannotBypassConfirmationAtManagerBoundary()
        {
            var context = new CommanderContextBuilder().Build(sim, manager);
            Assert.That(CommanderSemanticGraphAdmission.TryAdmit(CommanderSemanticJson.Parse(Compound),
                context, out var graph, out _), Is.True);
            Assert.Throws<InvalidOperationException>(() => manager.SubmitSemanticGraph(graph));
            Assert.That(manager.Goals, Is.Empty);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void ExternalRuntimeCallers_HaveNoPublicApprovalMintingSurface()
        {
            // Public providers may return data; only trusted game-assembly callbacks
            // may mint its approval. Check API access, not source-file text.
            var publicInstance = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
            Assert.That(typeof(CommanderGoalManager).GetMethod("PrepareActionPlan", publicInstance), Is.Null);
            Assert.That(typeof(CommanderGoalManager).GetMethod("ApproveActionPlan", publicInstance), Is.Null);
        }

        [Test]
        public void CandidateApproval_RejectsForeignRuntimeGenerationAndChangedMeaning()
        {
            var parsed = CommanderSemanticJson.Parse(Compound);
            var candidate = manager.PrepareActionPlan(parsed, "Build and train.", 7);
            Assert.That(manager.ApproveActionPlan(candidate, 8), Is.Null);
            using var foreign = new CommanderGoalManager(sim, 0);
            Assert.That(foreign.ApproveActionPlan(candidate, 7), Is.Null);
            var approved = manager.ApproveActionPlan(candidate, 7);
            Assert.That(approved, Is.Not.Null);
            Assert.Throws<InvalidOperationException>(() => foreign.SubmitSemanticGraph(approved));
            Assert.That(foreign.Goals, Is.Empty);
            Assert.That(manager.SubmitSemanticGraph(approved), Has.Count.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => manager.SubmitSemanticGraph(approved));
            Assert.That(manager.Goals, Has.Count.EqualTo(2));

            var ageRequest = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"ReachAge\",\"targetAge\":\"Next\"}]}");
            var ageCandidate = manager.PrepareActionPlan(ageRequest, "Advance to the next age.", 9);
            sim.SetPlayerAge(0, 2);
            Assert.That(manager.ApproveActionPlan(ageCandidate, 9), Is.Null,
                "A repaired Next-age candidate has a different effect and needs a new preview.");
            Assert.That(manager.Goals, Has.Count.EqualTo(2));
        }

        [Test]
        public void ThrowingAdmissionObserver_DoesNotMisreportCommittedGraphAsFailure()
        {
            var candidate = manager.PrepareActionPlan(CommanderSemanticJson.Parse(Compound), "Build and train.", 3);
            var graph = manager.ApproveActionPlan(candidate, 3);
            int laterNotifications = 0;
            manager.GoalEventPublished += _ => throw new InvalidOperationException("test observer failure");
            manager.GoalEventPublished += _ => laterNotifications++;
            Assert.DoesNotThrow(() => manager.SubmitSemanticGraph(graph));
            Assert.That(manager.Goals, Has.Count.EqualTo(2));
            Assert.That(laterNotifications, Is.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => manager.SubmitSemanticGraph(graph));
        }

        [Test]
        public async Task RetainedApprovedUiGraph_CannotCommitAfterHostGenerationReset()
        {
            await chat.SubmitMessageAsync("Build and train.");
            var candidate = chat.PendingActionPlan;
            var graph = manager.ApproveActionPlan(candidate, candidate.Generation);
            Assert.That(graph, Is.Not.Null);
            // Model a callback that detached its displayed candidate before delayed
            // commitment. Reset must revoke the runtime lease, not just the UI pointer.
            typeof(CommanderChatUI).GetField("pendingActionPlan",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(chat, null);
            chat.ResetConversation();
            Assert.Throws<InvalidOperationException>(() => manager.SubmitSemanticGraph(graph));
            Assert.That(manager.Goals, Is.Empty);
        }

        [Test]
        public void NoConstruction_UsesCompletedCapacityOrBlocksWithoutBuildingPrerequisites()
        {
            const string order = "{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4,"
                + "\"constraints\":[{\"type\":\"NoConstruction\"}]}]}";
            var parsed = CommanderSemanticJson.Parse(order);
            Assert.That(parsed.IsValid, Is.True, "Explicit negative constraints need a typed representation.");
            sim.SetPlayerAge(0, 3);
            var resources = sim.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 5000;
            int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
            for (int tx = x - 20; tx <= x + 20; tx++)
                for (int tz = z - 10; tz <= z + 10; tz++)
                {
                    sim.MapData.Tiles[tx, tz] = TileType.Grass;
                    sim.MapData.ForestDensity[tx, tz] = 0;
                    sim.MapData.FoundationCount[tx, tz] = 0;
                    sim.FogOfWar.SetVisible(0, tx, tz);
                }
            sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true).AutoProduceVillagers = false;
            var worker = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x + 6, z),
                Fixed32.One, Fixed32.One, Fixed32.One);
            worker.UnitType = 0;
            worker.IsVillager = true;
            worker.CurrentHealth = worker.MaxHealth = 100;
            worker.State = UnitState.Idle;
            Assert.That(CommanderSemanticAdmission.TryCreateTacticalIntent(parsed.Nodes[0],
                new CommanderContextBuilder().Build(sim, manager), out var intent, out var reason), Is.True, reason);
            Assert.That(dispatcher.SubmitIntent(intent).CreatedGoal, Is.True);
            manager.Tick(0);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty,
                "Missing Barracks is a blocker, not permission for a build/gather prefix.");
            Assert.That(manager.Goals[0].StatusReason, Does.Contain("construction"));

            var foundation = sim.CreateBuilding(0, BuildingType.Barracks, x + 12, z, true);
            manager.Tick(150);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty, "Do not resume forbidden construction either.");
            foundation.IsUnderConstruction = false;
            manager.Tick(300);
            var commands = sim.CommandBuffer.FlushCommands();
            Assert.That(commands, Has.Count.EqualTo(1));
            Assert.That(commands.Single(), Is.TypeOf<TrainUnitCommand>());
        }

        [Test]
        public void NoConstruction_CannotBeHiddenByAnExtraBuildRoot()
        {
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1},"
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4,"
                + "\"constraints\":[{\"type\":\"NoConstruction\"}],\"producerFromNode\":0}]}" );
            Assert.That(parsed.IsValid, Is.True);
            Assert.That(CommanderSemanticGraphAdmission.TryAdmit(parsed,
                new CommanderContextBuilder().Build(sim, manager), out _, out _), Is.False,
                "DependsOn/producer links cannot override a request's construction prohibition.");
            Assert.That(manager.Goals, Is.Empty);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void FourTypedConstraints_RoundTrip_AndMalformedSemanticConstraintsReject()
        {
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4,\"constraints\":["
                + "{\"type\":\"NoConstruction\"},{\"type\":\"PreferredWorkers\",\"mode\":\"IdleOnly\"},"
                + "{\"type\":\"MaximumQueue\",\"amount\":2},{\"type\":\"ProtectedResource\",\"resource\":\"Wood\",\"amount\":3}]}]}" );
            var context = new CommanderContextBuilder().Build(sim, manager);
            Assert.That(CommanderSemanticAdmission.TryCreateTacticalIntent(parsed.Nodes[0], context,
                out var intent, out _), Is.True);
            var projected = CommanderIntentDtoCodec.FromIntent(intent);
            Assert.That(projected.constraints.Select(c => c.type), Is.EqualTo(new[]
                { "NoConstruction", "PreferredWorkers", "MaximumQueue", "ProtectedResource" }));
            const string dto = "{\"intentCategory\":\"Tactical\",\"intentType\":\"EnsureUnitCount\","
                + "\"unit\":\"Spearman\",\"amount\":4,\"constraints\":[{\"type\":\"NoConstruction\"},"
                + "{\"type\":\"PreferredWorkers\",\"mode\":\"IdleOnly\"},{\"type\":\"MaximumQueue\",\"amount\":2},"
                + "{\"type\":\"ProtectedResource\",\"resource\":\"Wood\",\"amount\":3}]}";
            var roundTrip = CommanderIntentDtoCodec.InterpretJson(dto, context);
            Assert.That(roundTrip.Success, Is.True,
                "The shared legacy codec must retain all four typed constraints.");
            Assert.That(CommanderIntentDtoCodec.FromIntent(roundTrip.Intent).constraints.Select(c => c.type),
                Is.EqualTo(projected.constraints.Select(c => c.type)));
            foreach (var constraint in new[] { "{\"type\":0}", "{\"type\":\"NoConstruction\",\"approved\":true}",
                "{\"type\":\"NoConstruction\",\"amount\":0}", "{\"type\":\"MaximumQueue\",\"amount\":\"2\"}",
                "{\"type\":\"PreferredWorkers\",\"mode\":0}", "{\"type\":\"Unknown\"}",
                "{\"type\":\"NoConstruction\"},{\"type\":\"NoConstruction\"}" })
                Assert.That(CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                    + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4,\"constraints\":["
                    + constraint + "]}]}").IsValid, Is.False, constraint);
        }

        private sealed class CompoundProvider : ICommanderAIProvider, ICommanderSemanticProvider
        {
            private readonly string json;
            public int Calls { get; private set; }
            public CompoundProvider(string json) { this.json = json; }
            public Task<CommanderSemanticResult> TranslateSemanticAsync(
                CommanderSemanticProviderRequest request, CancellationToken token)
            {
                Calls++;
                return Task.FromResult(CommanderSemanticJson.Parse(json));
            }
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request,
                CancellationToken token) => throw new InvalidOperationException("No legacy/provider fallback.");
        }
    }
}
