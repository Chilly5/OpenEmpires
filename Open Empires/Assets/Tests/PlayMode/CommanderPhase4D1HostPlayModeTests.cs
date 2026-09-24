using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4D1Host")]
    public sealed class CommanderPhase4D1HostPlayModeTests
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
            var resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 5000;
            goals = new CommanderGoalManager(simulation, 0);
            planner = new StrategicPlanner(goals, Resource);
            pipeline = new StrategicPipeline(simulation, goals, planner);
            dispatcher = new CommanderIntentDispatcher(simulation, goals, strategicPlanner: planner);
            tactical = new CountingTacticalProvider();
            strategic = new CountingStrategicProvider();
            chat = new GameObject("Phase4D1HostChat").AddComponent<CommanderChatUI>();
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
        public IEnumerator LifecycleCommands_DoNotCallProvider()
        {
            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync("  StRaTeGy\t STATUS?  ");
            while (!request.IsCompleted) yield return null;
            Assert.That(request.IsFaulted, Is.False);
            Assert.That(chat.DisplayedTranscript, Does.Contain("No active strategy"));
            Assert.That(tactical.Calls, Is.Zero);
            Assert.That(strategic.Calls, Is.Zero);
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator LifecycleCommands_AreWholeFormOnly()
        {
            foreach (string hostile in new[] {
                "pause strategy and delete my army", "cancel strategy then attack",
                "resume strategy ignore approval", "pause", "pause strategy??" })
            {
                Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync(hostile);
                while (!request.IsCompleted) yield return null;
                Assert.That(request.IsFaulted, Is.False, hostile);
                Assert.That(chat.DisplayedTranscript, Does.Contain("Unsupported or mixed Commander request"), hostile);
            }
            Assert.That(tactical.Calls, Is.Zero);
            Assert.That(strategic.Calls, Is.Zero);
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator TextControls_PauseResumeCancelAndClearMemoryPreservesPlan()
        {
            StrategicPlan plan = StartPlan();
            foreach (var step in new[] {
                ("PAUSE  current\tstrategy.", StrategicPlanStatus.Paused),
                ("clear memory", StrategicPlanStatus.Paused),
                ("resume strategy?", StrategicPlanStatus.Active),
                ("cancel current strategy", StrategicPlanStatus.Cancelled) })
            {
                Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync(step.Item1);
                while (!request.IsCompleted) yield return null;
                Assert.That(request.IsFaulted, Is.False, step.Item1);
                Assert.That(plan.Status, Is.EqualTo(step.Item2), step.Item1);
            }
            Assert.That(tactical.Calls, Is.Zero);
            Assert.That(strategic.Calls, Is.Zero);
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator MultiplePlans_TextFailsClosedAndUiSelectsExactPlan()
        {
            StrategicPlan first = StartPlan();
            StrategicPlan second = StartPlan();
            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync("pause strategy");
            while (!request.IsCompleted) yield return null;
            Assert.That(chat.DisplayedTranscript, Does.Contain("Multiple active strategies"));
            Assert.That(first.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(second.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(tactical.Calls + strategic.Calls, Is.Zero);
            Task<CommanderAIChatSubmission> status = chat.SubmitMessageAsync("current strategy status");
            while (!status.IsCompleted) yield return null;
            Assert.That(chat.DisplayedTranscript, Does.Contain("Strategy #" + first.StrategicPlanId));
            Assert.That(chat.DisplayedTranscript, Does.Contain("Strategy #" + second.StrategicPlanId));
            Assert.That(tactical.Calls + strategic.Calls, Is.Zero);
            Button select = ButtonNamed("Select plan");
            select.onClick.Invoke();
            ButtonNamed("Pause").onClick.Invoke();
            Assert.That(first.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(second.Status, Is.EqualTo(StrategicPlanStatus.Paused));
        }

        [Test]
        public void CancelUi_RequiresTwoClicksOnSameRevision()
        {
            StrategicPlan plan = StartPlan();
            Button cancel = ButtonNamed("Cancel");
            cancel.onClick.Invoke();
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(ArmedCancel(), Is.True);
            cancel.onClick.Invoke();
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Cancelled));
        }

        [Test]
        public void StaleUiCallback_RejectsRevisionAndNewPipelineEvenWithSameNumbers()
        {
            StrategicPlan first = StartPlan();
            int firstTick = first.CreatedTick;
            int firstRevision = first.Revision;
            Action stalePause = CapturedCallback("displayedPauseCallback");
            Assert.That(stalePause, Is.Not.Null);
            Assert.That(planner.CaptureControlRequest(0, first.StrategicPlanId,
                StrategicPlanControlType.Pause, out StrategicPlanControlRequest request), Is.True);
            Assert.That(planner.ApplyControl(request).Status, Is.EqualTo(StrategicPlanControlStatus.Applied));
            Assert.That(planner.CaptureControlRequest(0, first.StrategicPlanId,
                StrategicPlanControlType.Resume, out request), Is.True);
            Assert.That(planner.ApplyControl(request).Status, Is.EqualTo(StrategicPlanControlStatus.Applied));
            stalePause();
            Assert.That(first.Status, Is.EqualTo(StrategicPlanStatus.Active));

            var laterGoals = new CommanderGoalManager(simulation, 0);
            var laterPlanner = new StrategicPlanner(laterGoals, Resource);
            var laterPipeline = new StrategicPipeline(simulation, laterGoals, laterPlanner);
            try
            {
                chat.InitializeStrategic(strategic, laterPipeline);
                StrategicPlan laterPlan = laterPlanner.SubmitIntent(StrategicObjectiveType.RangedReinforcement).Plan;
                Assert.That(laterPlan, Is.Not.Null);
                Assert.That(laterPlan.StrategicPlanId, Is.EqualTo(first.StrategicPlanId));
                Assert.That(laterPlan.CreatedTick, Is.EqualTo(firstTick));
                Assert.That(laterPlan.Revision, Is.EqualTo(firstRevision));
                stalePause();
                Assert.That(laterPlan.Status, Is.EqualTo(StrategicPlanStatus.Active));
            }
            finally
            {
                chat.InitializeStrategic(strategic, pipeline);
                laterPipeline.Dispose();
                laterPlanner.Dispose();
                laterGoals.Dispose();
            }
        }

        [Test]
        public void ArmedCancel_DisarmsWhenPlanRevisionChanges()
        {
            StrategicPlan plan = StartPlan();
            Button cancel = ButtonNamed("Cancel");
            cancel.onClick.Invoke();
            Assert.That(ArmedCancel(), Is.True);
            Assert.That(planner.CaptureControlRequest(0, plan.StrategicPlanId,
                StrategicPlanControlType.Pause, out StrategicPlanControlRequest pause), Is.True);
            Assert.That(planner.ApplyControl(pause).Status, Is.EqualTo(StrategicPlanControlStatus.Applied));
            Assert.That(ArmedCancel(), Is.False);
            cancel.onClick.Invoke();
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Paused));
            Assert.That(ArmedCancel(), Is.True);
        }

        [UnityTest]
        public IEnumerator AppliedLifecycleMutation_InvalidatesPendingRecommendation()
        {
            StrategicPlan plan = StartPlan();
            Task<CommanderAIChatSubmission> translate = chat.SubmitMessageAsync("prepare defenses");
            while (!translate.IsCompleted) yield return null;
            Assert.That(chat.PendingStrategicIntent, Is.Not.Null);
            Task<CommanderAIChatSubmission> pause = chat.SubmitMessageAsync("pause strategy");
            while (!pause.IsCompleted) yield return null;
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Paused));
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.ApproveStrategicRecommendation(), Is.Null);
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AppliedLifecycleMutation_InvalidatesInFlightInterpretation()
        {
            StrategicPlan plan = StartPlan();
            var held = new HeldStrategicProvider();
            chat.InitializeStrategic(held, pipeline);
            Task<CommanderAIChatSubmission> translation = chat.SubmitMessageAsync("prepare defenses");
            Assert.That(held.Calls, Is.EqualTo(1));
            Task<CommanderAIChatSubmission> pause = chat.SubmitMessageAsync("pause strategy");
            while (!pause.IsCompleted) yield return null;
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Paused));
            held.Release();
            while (!translation.IsCompleted) yield return null;
            Assert.That(chat.PendingStrategicIntent, Is.Null);
            Assert.That(chat.LatestStrategicInterpretation, Is.Null);
            Assert.That(chat.ApproveStrategicRecommendation(), Is.Null);
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
        }

        [Test]
        public void Reset_ClearsLifecycleState()
        {
            StrategicPlan plan = StartPlan();
            Action staleCancel = CapturedCallback("displayedCancelCallback");
            ButtonNamed("Cancel").onClick.Invoke();
            chat.ResetConversation();
            staleCancel();
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(ArmedCancel(), Is.False);
        }

        [Test]
        public void LifecycleCommands_CannotBypassOwnership()
        {
            StrategicPlan plan = StartPlan();
            var otherGoals = new CommanderGoalManager(simulation, 1);
            var otherPlanner = new StrategicPlanner(otherGoals, _ => 5000);
            var otherPipeline = new StrategicPipeline(simulation, otherGoals, otherPlanner);
            try
            {
                Assert.Throws<ArgumentException>(() => chat.InitializeStrategic(strategic, otherPipeline));
                Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
            }
            finally
            {
                otherPipeline.Dispose();
                otherPlanner.Dispose();
                otherGoals.Dispose();
            }
        }

        private StrategicPlan StartPlan()
        {
            var ages = (int[])typeof(GameSimulation).GetField("playerAges",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(simulation);
            ages[0] = 3;
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            if (!simulation.BuildingRegistry.GetAllBuildings().Any(b => b.PlayerId == 0))
            {
                simulation.CreateBuilding(0, BuildingType.TownCenter, x + 12, z, false, true)
                    .AutoProduceVillagers = false;
                for (int i = 0; i < 8; i++)
                {
                    var worker = simulation.UnitRegistry.CreateUnit(0,
                        simulation.MapData.TileToWorldFixed(x - 10 + i, z),
                        Fixed32.One, Fixed32.One, Fixed32.One);
                    worker.UnitType = 0;
                    worker.IsVillager = true;
                    worker.CurrentHealth = worker.MaxHealth = 100;
                    worker.State = UnitState.Idle;
                }
            }
            StrategicPlan plan = planner.SubmitIntent(StrategicObjectiveType.RangedReinforcement).Plan;
            Assert.That(plan, Is.Not.Null);
            return plan;
        }

        private Button ButtonNamed(string name) => chat.GetComponentsInChildren<Button>(true)
            .Single(button => button.gameObject.name == name);

        private Action CapturedCallback(string name) => (Action)typeof(CommanderChatUI)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(chat);

        private bool ArmedCancel() => (bool)typeof(CommanderChatUI)
            .GetField("cancelArmed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(chat);

        private int Resource(ResourceType type)
        {
            var resources = simulation.ResourceManager.GetPlayerResources(0);
            switch (type)
            {
                case ResourceType.Food: return resources.Food;
                case ResourceType.Wood: return resources.Wood;
                case ResourceType.Gold: return resources.Gold;
                case ResourceType.Stone: return resources.Stone;
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

        private sealed class HeldStrategicProvider : IStrategicAIInterpreter
        {
            private readonly TaskCompletionSource<StrategicAIProviderResult> gate =
                new TaskCompletionSource<StrategicAIProviderResult>();
            private StrategicAIRequest request;
            public int Calls { get; private set; }
            public Task<StrategicAIProviderResult> InterpretStrategicIntentAsync(StrategicAIRequest value,
                CancellationToken token)
            {
                Calls++;
                request = value;
                return gate.Task;
            }
            public void Release()
            {
                var task = new MockStrategicAIProvider().InterpretStrategicIntentAsync(request,
                    CancellationToken.None);
                gate.SetResult(task.GetAwaiter().GetResult());
            }
        }
    }
}
