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
    [Category("CommanderPhase4D1Runtime")]
    public sealed class CommanderPhase4D1PlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private StrategicPlanner planner;
        private StrategicPipeline pipeline;
        private CommanderIntentDispatcher dispatcher;
        private CommanderChatUI chat;
        private int x, z;

        [SetUp]
        public void SetUp()
        {
            foreach (CommanderChatUI existing in UnityEngine.Object.FindObjectsByType<CommanderChatUI>())
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            ((int[])typeof(GameSimulation).GetField("playerAges", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(simulation))[0] = 3;
            typeof(MapData).GetField("holeMap", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(simulation.MapData, null);
            foreach (ResourceNodeData node in simulation.MapData.GetAllResourceNodes()) node.RemainingAmount = 0;
            x = simulation.MapData.Width / 2;
            z = simulation.MapData.Height / 2;
            for (int tx = x - 35; tx <= x + 35; tx++)
                for (int tz = z - 22; tz <= z + 22; tz++)
                {
                    simulation.MapData.Tiles[tx, tz] = TileType.Grass;
                    simulation.MapData.ForestDensity[tx, tz] = 0;
                    simulation.MapData.FoundationCount[tx, tz] = 0;
                    simulation.FogOfWar.SetVisible(0, tx, tz);
                }
            simulation.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true)
                .AutoProduceVillagers = false;
            var enemy = simulation.MapData.BasePositions[1];
            simulation.CreateBuilding(1, BuildingType.TownCenter, enemy.x, enemy.y, false, true)
                .AutoProduceVillagers = false;
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 5000;
            for (int i = 0; i < 6; i++)
                simulation.CreateBuilding(0, BuildingType.House, x + 19 + i * 3, z + 12, false);
            Gatherers(ResourceType.Food, 8, x - 22);
            Gatherers(ResourceType.Wood, 8, x - 10);
            goals = new CommanderGoalManager(simulation, 0);
            planner = new StrategicPlanner(goals, Resource);
            pipeline = new StrategicPipeline(simulation, goals, planner);
            dispatcher = new CommanderIntentDispatcher(simulation, goals, strategicPlanner: planner);
            chat = new GameObject("Phase4D1RuntimeChat").AddComponent<CommanderChatUI>();
            chat.enabled = false;
            chat.Initialize(new MockAIProvider(), simulation, goals, dispatcher);
            chat.InitializeStrategic(new MockStrategicAIProvider(), pipeline);
            // Task 4 exercises a live panel: LateUpdate must refresh displayed plan tokens.
            chat.enabled = true;
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
        public IEnumerator RangedApproval_PausePreservesProgress_AtomicTrainingFinishes_ResumeCompletes()
        {
            int[] baselineBuildingIds = OwnedBuildings().Select(b => b.Id).ToArray();
            int commanderCommands = 0;
            simulation.CommandBuffer.CommandEnqueued += (_, source) =>
            {
                if (source == CommandEnqueueSource.Commander) commanderCommands++;
            };
            StrategicPlan approved = null;
            yield return Approve("prepare ranged reinforcements", StrategicPlanType.RangedReinforcement,
                plan => approved = plan);
            Assert.That(approved, Is.Not.Null);
            int planId = approved.StrategicPlanId;
            int createdTick = approved.CreatedTick;
            BuildingData range = null;
            int partialTicks = 0;
            while (partialTicks++ < 20000 && !approved.IsTerminal)
            {
                Advance();
                range = OwnedBuildings().FirstOrDefault(b => b.Type == BuildingType.ArcheryRange
                    && !baselineBuildingIds.Contains(b.Id) && !b.IsUnderConstruction
                    && b.TrainingQueue.Count > 0);
                if (range != null) break;
                if (partialTicks % 300 == 0) yield return null;
            }
            Assert.That(range, Is.Not.Null, "Plan-produced range must have accepted atomic training before pause.");
            Assert.That(approved.IsTerminal, Is.False);
            Assert.That(range.TrainingTicksRemaining, Is.GreaterThan(0));
            int milestone = approved.CurrentMilestone.MilestoneId;
            int[] childIds = approved.ChildGoalIds.ToArray();
            var reservations = ActiveReservations(approved);
            int commandsAtPause = commanderCommands;
            int archersBefore = Count(CommanderIntentCatalog.ArcherUnitType);
            Task<CommanderAIChatSubmission> pause = chat.SubmitMessageAsync("pause current strategy");
            while (!pause.IsCompleted) yield return null;
            Assert.That(approved.Status, Is.EqualTo(StrategicPlanStatus.Paused));
            // The queue entry was accepted by a prior simulation.Tick, not merely enqueued.
            // BuildingTrainingSystem.Tick completes at <= 0, so one paused tick proves
            // an already-funded atomic action can finish without plan progression.
            range.TrainingTicksRemaining = 1;
            var manual = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(x - 28, z - 12),
                Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
            manual.UnitType = 0;
            manual.IsVillager = true;
            manual.MaxHealth = manual.CurrentHealth = 100;
            manual.State = UnitState.Idle;
            simulation.CommandBuffer.EnqueueCommand(new MoveCommand(0, new[] { manual.Id },
                simulation.MapData.TileToWorldFixed(x - 20, z - 12)), CommandEnqueueSource.Human);
            simulation.Tick();
            Assert.That(manual.PlayerCommanded, Is.True,
                "Human movement must execute while a strategy is paused.");
            Assert.That(Count(CommanderIntentCatalog.ArcherUnitType), Is.GreaterThan(archersBefore),
                "Accepted training should finish on the first paused simulation tick.");
            for (int tick = 0; tick < 1200; tick++)
            {
                Advance();
                if (tick % 300 == 0) yield return null;
            }
            Assert.That(Count(CommanderIntentCatalog.ArcherUnitType), Is.GreaterThan(archersBefore),
                "Already-funded training should finish while strategic planning is paused.");
            Assert.That(approved.CurrentMilestone.MilestoneId, Is.EqualTo(milestone));
            Assert.That(approved.ChildGoalIds, Is.EqualTo(childIds));
            Assert.That(ActiveReservations(approved), Is.EqualTo(reservations));
            Assert.That(commanderCommands, Is.EqualTo(commandsAtPause));
            Assert.That(approved.StrategicPlanId, Is.EqualTo(planId));
            Assert.That(approved.CreatedTick, Is.EqualTo(createdTick));
            Task<CommanderAIChatSubmission> resume = chat.SubmitMessageAsync("resume strategy");
            while (!resume.IsCompleted) yield return null;
            Assert.That(approved.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(approved.CurrentMilestone.MilestoneId, Is.EqualTo(milestone));
            Assert.That(approved.ChildGoalIds, Is.EqualTo(childIds),
                "Resume must retain the in-progress force goal rather than create another one.");
            Assert.That(goals.Goals.OfType<EnsureUnitCountGoal>()
                .Count(g => g.RequestedUnitType == CommanderIntentCatalog.ArcherUnitType
                    && g.TargetTotal == RangedReinforcementPlan.ArcherTarget), Is.EqualTo(1));
            int resumedTicks = 0;
            while (resumedTicks++ < 30000 && !approved.IsTerminal)
            {
                Advance();
                if (approved.CurrentMilestone.MilestoneId == milestone)
                    Assert.That(approved.ChildGoalIds, Is.EqualTo(childIds),
                        "The active force milestone must not replace or duplicate its child goal.");
                if (resumedTicks % 300 == 0) yield return null;
            }
            Assert.That(approved.Status, Is.EqualTo(StrategicPlanStatus.Completed), approved.OutcomeMessage);
            Assert.That(Count(CommanderIntentCatalog.ArcherUnitType),
                Is.EqualTo(RangedReinforcementPlan.ArcherTarget),
                "Resume must not overproduce Archers beyond the plan's exact target.");
            Assert.That(goals.Goals.OfType<EnsureUnitCountGoal>()
                .Count(g => g.RequestedUnitType == CommanderIntentCatalog.ArcherUnitType
                    && g.TargetTotal == RangedReinforcementPlan.ArcherTarget), Is.EqualTo(1));
            Assert.That(OwnedBuildings().Count(b => b.Type == BuildingType.ArcheryRange), Is.EqualTo(1));
            Assert.That(ActiveReservations(approved), Is.Empty);
            Debug.Log($"[Phase4D1 A] plan={planId} created={createdTick} partial={partialTicks} pauseMilestone={milestone} children={childIds.Length} reservations={reservations.Length} range={range.Id} manualUnit={manual.Id} archers={archersBefore}->{Count(CommanderIntentCatalog.ArcherUnitType)} resumed={resumedTicks} commanderCommands={commanderCommands}");
        }

        [UnityTest]
        public IEnumerator TurtleApproval_TwoClickCancelPreservesBuiltAssetAndHumanMove()
        {
            int[] baselineBuildingIds = OwnedBuildings().Select(b => b.Id).ToArray();
            int commanderCommands = 0;
            simulation.CommandBuffer.CommandEnqueued += (_, source) =>
            {
                if (source == CommandEnqueueSource.Commander) commanderCommands++;
            };
            StrategicPlan approved = null;
            yield return Approve("prepare fortified defenses", StrategicPlanType.DefensiveTurtle,
                plan => approved = plan);
            BuildingData produced = null;
            int partialTicks = 0;
            while (partialTicks++ < 22000 && !approved.IsTerminal)
            {
                Advance();
                produced = OwnedBuildings().FirstOrDefault(b => !baselineBuildingIds.Contains(b.Id)
                    && !b.IsUnderConstruction && !b.IsDestroyed);
                if (produced != null) break;
                if (partialTicks % 300 == 0) yield return null;
            }
            Assert.That(produced, Is.Not.Null, "A completed asset must be demonstrably plan-produced.");
            Assert.That(approved.IsTerminal, Is.False);
            int milestone = approved.CurrentMilestone.MilestoneId;
            int[] childIds = approved.ChildGoalIds.ToArray();
            var reservations = ActiveReservations(approved);
            Assert.That(reservations, Is.Not.Empty);
            yield return null; // normal rendered-panel refresh after the last simulation tick
            var manual = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(x - 28, z - 12),
                Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
            manual.UnitType = 0;
            manual.IsVillager = true;
            manual.MaxHealth = manual.CurrentHealth = 100;
            manual.State = UnitState.Idle;
            var destination = simulation.MapData.TileToWorldFixed(x - 20, z - 12);
            simulation.CommandBuffer.EnqueueCommand(new MoveCommand(0, new[] { manual.Id }, destination),
                CommandEnqueueSource.Human);
            Assert.That(manual.PlayerCommanded, Is.False,
                "The raw Human command must still be pending when cancellation starts.");
            int[] resourcesBeforeCancel = { Resource(ResourceType.Food), Resource(ResourceType.Wood),
                Resource(ResourceType.Gold), Resource(ResourceType.Stone) };
            Button cancel = ButtonNamed("Cancel");
            cancel.onClick.Invoke();
            Assert.That(approved.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(CancelLabel(cancel), Does.Contain("Confirm cancel #" + approved.StrategicPlanId));
            Assert.That(ActiveReservations(approved), Is.EqualTo(reservations));
            cancel.onClick.Invoke();
            Assert.That(approved.Status, Is.EqualTo(StrategicPlanStatus.Cancelled));
            Assert.That(ActiveReservations(approved), Is.Empty);
            int commandsAtCancel = commanderCommands;
            Assert.That(manual.PlayerCommanded, Is.False,
                "Cancellation must not process or discard the pending Human command.");
            Assert.That(new[] { Resource(ResourceType.Food), Resource(ResourceType.Wood),
                Resource(ResourceType.Gold), Resource(ResourceType.Stone) }, Is.EqualTo(resourcesBeforeCancel),
                "Cancelling the strategy must not refund already-spent tactical resources.");
            simulation.Tick();
            Assert.That(manual.PlayerCommanded, Is.True,
                "The raw Human command must survive cancellation and be processed by simulation.Tick.");
            for (int tick = 0; tick < 1200; tick++)
            {
                Advance();
                if (tick % 300 == 0) yield return null;
            }
            Assert.That(approved.Status, Is.EqualTo(StrategicPlanStatus.Cancelled));
            Assert.That(approved.CurrentMilestone.MilestoneId, Is.EqualTo(milestone));
            Assert.That(approved.ChildGoalIds, Is.EqualTo(childIds));
            Assert.That(ActiveReservations(approved), Is.Empty);
            Assert.That(commanderCommands, Is.EqualTo(commandsAtCancel));
            Assert.That(simulation.BuildingRegistry.GetBuilding(produced.Id), Is.SameAs(produced));
            Assert.That(produced.IsDestroyed, Is.False);
            Assert.That(produced.IsUnderConstruction, Is.False);
            Debug.Log($"[Phase4D1 B] plan={approved.StrategicPlanId} partial={partialTicks} milestone={milestone} children={childIds.Length} reservations={reservations.Length} retainedAsset={produced.Id}/{produced.Type} manualUnit={manual.Id} commanderCommands={commanderCommands}");
        }

        [UnityTest]
        public IEnumerator OldDisplayedCallback_CannotPauseMatchingIdentityInNewPipeline()
        {
            StrategicPlan first = null;
            yield return Approve("prepare ranged reinforcements", StrategicPlanType.RangedReinforcement,
                plan => first = plan);
            Action oldPause = (Action)typeof(CommanderChatUI)
                .GetField("displayedPauseCallback", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(chat);
            Assert.That(oldPause, Is.Not.Null);
            var laterGoals = new CommanderGoalManager(simulation, 0);
            var laterPlanner = new StrategicPlanner(laterGoals, Resource);
            var laterPipeline = new StrategicPipeline(simulation, laterGoals, laterPlanner);
            try
            {
                chat.InitializeStrategic(new MockStrategicAIProvider(), laterPipeline);
                Task<CommanderAIChatSubmission> translation = chat.SubmitMessageAsync("prepare ranged reinforcements");
                while (!translation.IsCompleted) yield return null;
                ButtonNamed("Approve strategy").onClick.Invoke();
                StrategicPlan later = chat.LatestStrategicDecision?.Submission?.Plan;
                Assert.That(later, Is.Not.Null);
                Assert.That(later.StrategicPlanId, Is.EqualTo(first.StrategicPlanId));
                Assert.That(later.CreatedTick, Is.EqualTo(first.CreatedTick));
                Assert.That(later.Revision, Is.EqualTo(first.Revision));
                var reservations = laterPlanner.GetReservationsForPlan(later.StrategicPlanId)
                    .Where(r => r.Status == StrategicResourceReservationStatus.Active)
                    .Select(r => (r.ReservationId, r.Amount)).ToArray();
                int goalsBefore = laterGoals.Goals.Count;
                oldPause();
                Assert.That(later.Status, Is.EqualTo(StrategicPlanStatus.Active));
                Assert.That(laterPlanner.GetReservationsForPlan(later.StrategicPlanId)
                    .Where(r => r.Status == StrategicResourceReservationStatus.Active)
                    .Select(r => (r.ReservationId, r.Amount)).ToArray(), Is.EqualTo(reservations));
                Assert.That(laterGoals.Goals.Count, Is.EqualTo(goalsBefore));
                Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
                Debug.Log($"[Phase4D1 E] old/new plan={first.StrategicPlanId}/{later.StrategicPlanId} tick={first.CreatedTick}/{later.CreatedTick} revision={first.Revision}/{later.Revision} reservations={reservations.Length}");
            }
            finally
            {
                chat.InitializeStrategic(new MockStrategicAIProvider(), pipeline);
                laterPipeline.Dispose();
                laterPlanner.Dispose();
                laterGoals.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator MatchResetDuringHeldProviderReply_CannotLeakIntoNewRuntime()
        {
            var held = new HeldStrategicProvider();
            chat.InitializeStrategic(held, pipeline);
            Task<CommanderAIChatSubmission> oldTranslation = chat.SubmitMessageAsync("prepare fortified defenses");
            Assert.That(held.Calls, Is.EqualTo(1));
            GameSimulation laterSimulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            laterSimulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            var laterGoals = new CommanderGoalManager(laterSimulation, 0);
            var laterPlanner = new StrategicPlanner(laterGoals, _ => 5000);
            var laterPipeline = new StrategicPipeline(laterSimulation, laterGoals, laterPlanner);
            var laterDispatcher = new CommanderIntentDispatcher(laterSimulation, laterGoals,
                strategicPlanner: laterPlanner);
            try
            {
                chat.Initialize(new MockAIProvider(), laterSimulation, laterGoals, laterDispatcher);
                chat.InitializeStrategic(new MockStrategicAIProvider(), laterPipeline);
                held.Release();
                while (!oldTranslation.IsCompleted) yield return null;
                yield return null;
                Assert.That(oldTranslation.IsFaulted, Is.False,
                    oldTranslation.Exception?.ToString() ?? string.Empty);
                Assert.That(chat.PendingStrategicIntent, Is.Null);
                Assert.That(chat.LatestStrategicInterpretation, Is.Null);
                Assert.That(chat.Conversation.Memory.Count, Is.Zero);
                Assert.That(chat.DisplayedTranscript, Does.Not.Contain("prepare fortified defenses"));
                Assert.That(laterPlanner.Plans, Is.Empty);
                Assert.That(laterGoals.Goals, Is.Empty);
                Assert.That(laterSimulation.CommandBuffer.FlushCommands(), Is.Empty);
                Assert.That(planner.Plans, Is.Empty);
                Debug.Log("[Phase4D1 F] old held provider reply discarded after Initialize/InitializeStrategic new match; pending=0 memory=0 plans=0 commands=0");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(chat.gameObject);
                chat = null;
                laterDispatcher.Dispose();
                laterPipeline.Dispose();
                laterPlanner.Dispose();
                laterGoals.Dispose();
            }
        }

        private IEnumerator Approve(string phrase, StrategicPlanType type, Action<StrategicPlan> accepted)
        {
            Task<CommanderAIChatSubmission> translation = chat.SubmitMessageAsync(phrase);
            while (!translation.IsCompleted) yield return null;
            Assert.That(translation.IsFaulted, Is.False,
                translation.Exception?.ToString() ?? string.Empty);
            Assert.That(chat.PendingStrategicIntent, Is.Not.Null);
            Assert.That(planner.Plans, Is.Empty);
            ButtonNamed("Approve strategy").onClick.Invoke();
            StrategicIntentSubmission submission = chat.LatestStrategicDecision?.Submission;
            Assert.That(submission?.CreatedPlan, Is.True, chat.LatestStrategicDecision?.Outcome);
            Assert.That(submission.Plan.PlanType, Is.EqualTo(type));
            Assert.That(submission.Plan.Authority, Is.EqualTo(StrategicPlanAuthority.Normal));
            accepted(submission.Plan);
        }

        private void Advance()
        {
            planner.Tick(simulation.CurrentTick);
            goals.Tick(simulation.CurrentTick);
            simulation.Tick();
        }

        private (int, ResourceType, int)[] ActiveReservations(StrategicPlan plan) =>
            planner.GetReservationsForPlan(plan.StrategicPlanId)
                .Where(r => r.Status == StrategicResourceReservationStatus.Active)
                .Select(r => (r.ReservationId, r.ResourceType, r.Amount)).ToArray();

        private BuildingData[] OwnedBuildings() => simulation.BuildingRegistry.GetAllBuildings()
            .Where(b => b.PlayerId == 0).ToArray();
        private int Count(int type) => simulation.UnitRegistry.GetAllUnits()
            .Count(u => u.PlayerId == 0 && u.CurrentHealth > 0 && u.UnitType == type);
        private Button ButtonNamed(string name) => chat.GetComponentsInChildren<Button>(true)
            .Single(button => button.gameObject.name == name);
        private static string CancelLabel(Button button)
        {
            Component label = button.GetComponentsInChildren<Component>(true)
                .Single(c => c.GetType().Name == "TextMeshProUGUI");
            return (string)label.GetType().GetProperty("text").GetValue(label);
        }
        private int Resource(ResourceType type)
        {
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            switch (type)
            {
                case ResourceType.Food: return resources.Food;
                case ResourceType.Wood: return resources.Wood;
                case ResourceType.Gold: return resources.Gold;
                case ResourceType.Stone: return resources.Stone;
                default: return 0;
            }
        }
        private void Gatherers(ResourceType resource, int count, int start)
        {
            ResourceNodeData node = simulation.MapData.AddResourceNode(resource,
                simulation.MapData.TileToWorldFixed(start + 4, z + 8), 10000);
            for (int i = 0; i < count; i++)
            {
                var worker = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(start + 3 + i % 2, z + 7 + i / 2),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                worker.UnitType = 0;
                worker.IsVillager = true;
                worker.MaxHealth = worker.CurrentHealth = 100;
                worker.FinalDestination = worker.SimPosition;
                worker.State = UnitState.Gathering;
                worker.TargetResourceNodeId = node.Id;
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
                StrategicAIProviderResult result = new MockStrategicAIProvider()
                    .InterpretStrategicIntentAsync(request, CancellationToken.None)
                    .GetAwaiter().GetResult();
                gate.SetResult(result);
            }
        }
    }
}
