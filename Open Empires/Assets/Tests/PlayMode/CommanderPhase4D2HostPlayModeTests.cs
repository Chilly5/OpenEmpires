using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4D2Host")]
    public sealed class CommanderPhase4D2HostPlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private StrategicPlanner planner;
        private StrategicPipeline pipeline;
        private CommanderIntentDispatcher dispatcher;
        private CommanderChatUI chat;
        private CountingTacticalProvider tactical;
        private CountingStrategicProvider strategic;

        [SetUp]
        public void SetUp()
        {
            foreach (CommanderChatUI existing in UnityEngine.Object.FindObjectsByType<CommanderChatUI>())
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            var stock = simulation.ResourceManager.GetPlayerResources(0);
            stock.Food = stock.Wood = stock.Gold = stock.Stone = 5000;
            goals = new CommanderGoalManager(simulation, 0);
            planner = new StrategicPlanner(goals, Resource);
            pipeline = new StrategicPipeline(simulation, goals, planner);
            dispatcher = new CommanderIntentDispatcher(simulation, goals, strategicPlanner: planner);
            tactical = new CountingTacticalProvider();
            strategic = new CountingStrategicProvider();
            chat = new GameObject("Phase4D2HostChat").AddComponent<CommanderChatUI>();
            chat.enabled = false;
            chat.Initialize(tactical, simulation, goals, dispatcher);
            chat.InitializeStrategic(strategic, pipeline);
        }

        [TearDown]
        public void TearDown()
        {
            if (chat != null) UnityEngine.Object.DestroyImmediate(chat.gameObject);
            dispatcher?.Dispose();
            pipeline?.Dispose();
            planner?.Dispose();
            goals?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        [UnityTest]
        public IEnumerator ExactHealthQuestions_AreOfflineAndUnavailableWithoutPlan()
        {
            foreach (string question in new[] { "why is the strategy paused?", "why is the plan waiting?",
                "what is blocking the current strategy?", "did the strategy recover?",
                "why did the plan stop?" })
            {
                Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync(question);
                while (!request.IsCompleted) yield return null;
                Assert.That(request.IsFaulted, Is.False, question);
                Assert.That(chat.DisplayedTranscript, Does.Contain("Health evidence unavailable"), question);
            }
            Assert.That(tactical.Calls + strategic.Calls, Is.Zero);
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator MixedHealthQuestion_DoesNotBypassRouter()
        {
            foreach (string question in new[] { "why is the plan waiting then attack",
                "why is the strategy paused??", "did the strategy recover; cancel strategy" })
            {
                Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync(question);
                while (!request.IsCompleted) yield return null;
                Assert.That(chat.DisplayedTranscript, Does.Contain("Unsupported or mixed Commander request"));
            }
            Assert.That(tactical.Calls + strategic.Calls, Is.Zero);
            Assert.That(planner.Plans, Is.Empty);
        }

        [UnityTest]
        public IEnumerator PausedQuestion_UsesCurrentTypedHealthWithoutMutation()
        {
            StrategicPlan plan = StartPlan();
            Assert.That(planner.CaptureControlRequest(0, plan.StrategicPlanId,
                StrategicPlanControlType.Pause, out StrategicPlanControlRequest pause), Is.True);
            Assert.That(planner.ApplyControl(pause).Status, Is.EqualTo(StrategicPlanControlStatus.Applied));
            int revision = plan.Revision;
            int reservationCount = planner.Reservations.Count;
            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync(" WHY  IS THE STRATEGY PAUSED? ");
            while (!request.IsCompleted) yield return null;
            Assert.That(chat.DisplayedTranscript, Does.Contain("Paused"));
            Assert.That(plan.Revision, Is.EqualTo(revision));
            Assert.That(planner.Reservations.Count, Is.EqualTo(reservationCount));
            Assert.That(tactical.Calls + strategic.Calls, Is.Zero);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void UnchangedRevision_WorldChangeRefreshesHealth()
        {
            StrategicPlan plan = StartPlan();
            int revision = plan.Revision;
            InvokeHostRefresh();
            string before = CurrentStatus();
            simulation.ResourceManager.GetPlayerResources(0).Wood = 17;
            InvokeHostRefresh();
            string after = CurrentStatus();
            Assert.That(plan.Revision, Is.EqualTo(revision));
            Assert.That(after, Is.Not.EqualTo(before));
            Assert.That(after, Does.Contain("17"));
            var townCenter = simulation.BuildingRegistry.GetAllBuildings()
                .Single(building => building.PlayerId == 0 && building.Type == BuildingType.TownCenter);
            townCenter.TrainingQueue.Add(CommanderIntentCatalog.ArcherUnitType);
            InvokeHostRefresh();
            Assert.That(CurrentStatus(), Does.Contain("queued 1"));
            var unit = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(simulation.MapData.Width / 2 - 20,
                    simulation.MapData.Height / 2), Fixed32.One, Fixed32.One, Fixed32.One);
            unit.UnitType = CommanderIntentCatalog.ArcherUnitType;
            unit.CurrentHealth = unit.MaxHealth = 100;
            InvokeHostRefresh();
            Assert.That(CurrentStatus(), Does.Contain("population 9/"));
            Assert.That(tactical.Calls + strategic.Calls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator EnabledHost_LateUpdateRefreshesSameRevisionWorldChange()
        {
            StrategicPlan plan = StartPlan();
            chat.enabled = true;
            yield return null;
            int revision = plan.Revision;
            int goalCount = goals.Goals.Count;
            int reservationCount = planner.Reservations.Count;
            simulation.ResourceManager.GetPlayerResources(0).Wood = 37;
            yield return null;
            Assert.That(CurrentStatus(), Does.Contain("wood 37"));
            Assert.That(plan.Revision, Is.EqualTo(revision));
            Assert.That(goals.Goals.Count, Is.EqualTo(goalCount));
            Assert.That(planner.Reservations.Count, Is.EqualTo(reservationCount));
            Assert.That(tactical.Calls + strategic.Calls, Is.Zero);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void MatchingNumericIdentity_NewPipelineRejectsOldHealth()
        {
            StrategicPlan first = StartPlan();
            InvokeHostRefresh();
            var laterGoals = new CommanderGoalManager(simulation, 0);
            var laterPlanner = new StrategicPlanner(laterGoals, Resource);
            var laterPipeline = new StrategicPipeline(simulation, laterGoals, laterPlanner);
            try
            {
                chat.InitializeStrategic(strategic, laterPipeline);
                Assert.That(CurrentStatus(), Does.Not.Contain("#" + first.StrategicPlanId));
                StrategicPlan later = laterPlanner.SubmitIntent(StrategicObjectiveType.RangedReinforcement).Plan;
                Assert.That(later, Is.Not.Null);
                Assert.That(later.StrategicPlanId, Is.EqualTo(first.StrategicPlanId));
                Assert.That(later.CreatedTick, Is.EqualTo(first.CreatedTick));
                InvokeHostRefresh();
                Assert.That(CurrentStatus(), Does.Contain("#" + later.StrategicPlanId));
                Assert.That(CurrentStatus(), Does.Contain("Health:"));
            }
            finally
            {
                chat.InitializeStrategic(strategic, pipeline);
                laterPipeline.Dispose(); laterPlanner.Dispose(); laterGoals.Dispose();
            }
        }

        [Test]
        public void Reset_ClearsPlanHealthProjection()
        {
            StartPlan();
            InvokeHostRefresh();
            Assert.That(CurrentStatus(), Does.Contain("Health:"));
            simulation.ResourceManager.GetPlayerResources(0).Wood = 19;
            chat.ResetConversation();
            Assert.That(CurrentStatus(), Does.Contain("wood 19"));
            Assert.That(CurrentStatus(), Does.Not.Contain("wood 5000"));
        }

        [UnityTest]
        public IEnumerator RevisionChange_RefreshesQuestionAndStatus()
        {
            StrategicPlan plan = StartPlan();
            Assert.That(planner.CaptureControlRequest(0, plan.StrategicPlanId,
                StrategicPlanControlType.Pause, out StrategicPlanControlRequest pause), Is.True);
            planner.ApplyControl(pause);
            Task<CommanderAIChatSubmission> first = chat.SubmitMessageAsync("why is the strategy paused?");
            while (!first.IsCompleted) yield return null;
            Assert.That(chat.DisplayedTranscript, Does.Contain("Strategy #" + plan.StrategicPlanId + ": Paused"));
            Assert.That(planner.CaptureControlRequest(0, plan.StrategicPlanId,
                StrategicPlanControlType.Resume, out StrategicPlanControlRequest resume), Is.True);
            planner.ApplyControl(resume);
            Task<CommanderAIChatSubmission> second = chat.SubmitMessageAsync("why is the strategy paused?");
            while (!second.IsCompleted) yield return null;
            Assert.That(chat.DisplayedTranscript, Does.Contain("Health evidence unavailable"));
            Assert.That(CurrentStatus(), Does.Not.Contain("Health: Paused"));
            Assert.That(tactical.Calls + strategic.Calls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator OwnerMismatch_HealthIsUnavailable()
        {
            StartPlan();
            typeof(CommanderChatUI).GetField("<Conversation>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(chat, new ConversationState(1));
            InvokeHostRefresh();
            Assert.That(CurrentStatus(), Does.Contain("No active strategy"));
            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync("what is blocking the current strategy?");
            while (!request.IsCompleted) yield return null;
            Assert.That(chat.DisplayedTranscript, Does.Contain("Health evidence unavailable"));
            Assert.That(tactical.Calls + strategic.Calls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator TerminalPlan_DoesNotBecomeCurrentHealthAlias()
        {
            StrategicPlan plan = StartPlan();
            Assert.That(planner.CaptureControlRequest(0, plan.StrategicPlanId,
                StrategicPlanControlType.Cancel, out StrategicPlanControlRequest cancel), Is.True);
            planner.ApplyControl(cancel);
            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync("why did the plan stop?");
            while (!request.IsCompleted) yield return null;
            Assert.That(chat.DisplayedTranscript, Does.Contain("Health evidence unavailable"));
            Assert.That(CurrentStatus(), Does.Not.Contain("Health: Cancelled"));
            Assert.That(tactical.Calls + strategic.Calls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ExplicitRetainedTerminalPlan_ReportsOnlyAuthoritativeStatus()
        {
            StrategicPlan plan = StartPlan();
            Assert.That(planner.CaptureControlRequest(0, plan.StrategicPlanId,
                StrategicPlanControlType.Cancel, out StrategicPlanControlRequest cancel), Is.True);
            planner.ApplyControl(cancel);
            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync(
                "why did plan #" + plan.StrategicPlanId + " stop?");
            while (!request.IsCompleted) yield return null;
            Assert.That(chat.DisplayedTranscript, Does.Contain("Strategy #" + plan.StrategicPlanId + ": Cancelled"));
            Assert.That(chat.DisplayedTranscript, Does.Not.Contain("Health evidence unavailable"));
            Assert.That(tactical.Calls + strategic.Calls, Is.Zero);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator WaitingAndBlockedQuestions_UseTypedCurrentChildEvidence()
        {
            StrategicPlan plan = StartPlan();
            int[] childIds = plan.CurrentMilestone.RequiredChildGoals.ToArray();
            Assert.That(childIds.Length, Is.GreaterThanOrEqualTo(2));
            CommanderGoal waiting = goals.GetGoal(childIds[0]);
            CommanderGoal blocked = goals.GetGoal(childIds[1]);
            waiting.SetStatus(CommanderGoalStatus.WaitingForResources, "untrusted reason");
            blocked.SetStatus(CommanderGoalStatus.Blocked, "untrusted secret");
            int revision = plan.Revision;
            int goalCount = goals.Goals.Count;
            int reservationCount = planner.Reservations.Count;
            Task<CommanderAIChatSubmission> wait = chat.SubmitMessageAsync("why is the plan waiting?");
            while (!wait.IsCompleted) yield return null;
            Assert.That(chat.DisplayedTranscript, Does.Contain("Temporarily blocked"));
            Assert.That(chat.DisplayedTranscript, Does.Contain("Waiting for resources"));
            Task<CommanderAIChatSubmission> block = chat.SubmitMessageAsync("what is blocking the current strategy?");
            while (!block.IsCompleted) yield return null;
            Assert.That(chat.DisplayedTranscript, Does.Contain("Temporarily blocked"));
            Assert.That(chat.DisplayedTranscript, Does.Not.Contain("untrusted reason"));
            Assert.That(chat.DisplayedTranscript, Does.Not.Contain("untrusted secret"));
            Assert.That(plan.Revision, Is.EqualTo(revision));
            Assert.That(goals.Goals.Count, Is.EqualTo(goalCount));
            Assert.That(planner.Reservations.Count, Is.EqualTo(reservationCount));
            Assert.That(tactical.Calls + strategic.Calls, Is.Zero);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void Destroy_ClearsSelectedHealthView()
        {
            StartPlan();
            InvokeHostRefresh();
            Assert.That(CurrentStatus(), Does.Contain("Health:"));
            CommanderChatUI destroyed = chat;
            UnityEngine.Object.DestroyImmediate(chat.gameObject);
            chat = null;
            Assert.That(typeof(CommanderChatUI).GetField("selectedPlanId",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(destroyed), Is.EqualTo(0));
            Assert.That(typeof(CommanderChatUI).GetField("selectedPlanStatusPrefix",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(destroyed), Is.Null);
        }

        private StrategicPlan StartPlan()
        {
            var ages = (int[])typeof(GameSimulation).GetField("playerAges",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(simulation);
            ages[0] = 3;
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            simulation.CreateBuilding(0, BuildingType.TownCenter, x + 12, z, false, true)
                .AutoProduceVillagers = false;
            for (int i = 0; i < 8; i++)
            {
                var worker = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(x - 10 + i, z),
                    Fixed32.One, Fixed32.One, Fixed32.One);
                worker.UnitType = 0; worker.IsVillager = true;
                worker.CurrentHealth = worker.MaxHealth = 100;
                worker.State = UnitState.Idle;
            }
            StrategicPlan plan = planner.SubmitIntent(StrategicObjectiveType.RangedReinforcement).Plan;
            Assert.That(plan, Is.Not.Null);
            return plan;
        }

        private void InvokeHostRefresh() => typeof(CommanderChatUI)
            .GetMethod("UpdateStrategicHostControls", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(chat, null);

        private string CurrentStatus()
        {
            Transform label = chat.GetComponentsInChildren<Transform>(true)
                .Single(value => value.gameObject.name == "Current plan status");
            Component text = label.GetComponent("TextMeshProUGUI");
            return (string)text.GetType().GetProperty("text").GetValue(text);
        }

        private int Resource(ResourceType type)
        {
            var stock = simulation.ResourceManager.GetPlayerResources(0);
            switch (type)
            {
                case ResourceType.Food: return stock.Food;
                case ResourceType.Wood: return stock.Wood;
                case ResourceType.Gold: return stock.Gold;
                case ResourceType.Stone: return stock.Stone;
                default: return 0;
            }
        }

        private sealed class CountingTacticalProvider : ICommanderAIProvider
        {
            public int Calls { get; private set; }
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request, CancellationToken token)
            {
                Calls++;
                return new MockAIProvider().TranslateAsync(request, token);
            }
        }

        private sealed class CountingStrategicProvider : IStrategicAIInterpreter
        {
            public int Calls { get; private set; }
            public Task<StrategicAIProviderResult> InterpretStrategicIntentAsync(StrategicAIRequest request,
                CancellationToken token)
            {
                Calls++;
                return new MockStrategicAIProvider().InterpretStrategicIntentAsync(request, token);
            }
        }
    }
}
