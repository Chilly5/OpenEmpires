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
        public IEnumerator ScenarioD_ConfirmedDefensiveReplacement_NeverResurrectsCancelledRangedWork()
        {
            StrategicPlan ranged = null;
            yield return Approve("prepare ranged reinforcements", StrategicPlanType.RangedReinforcement,
                plan => ranged = plan);
            int oldCommands = 0;
            simulation.CommandBuffer.CommandEnqueued += (_, source) =>
            {
                if (source == CommandEnqueueSource.Commander) oldCommands++;
            };
            int partial = 0;
            while (partial++ < 12000 && ranged.CurrentMilestone.Name == "Economy")
            {
                Advance();
                if (partial % 300 == 0) yield return null;
            }
            Assert.That(ranged.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(ranged.CurrentMilestone.Name, Is.Not.EqualTo("Economy"),
                "Ranged must be in real in-progress work at the replacement boundary.");
            int rangedRevision = ranged.Revision;
            int rangedMilestone = ranged.CurrentMilestone.MilestoneId;
            int[] rangedChildren = ranged.ChildGoalIds.ToArray();
            int goalCount = goals.Goals.Count;
            int reservations = ActiveReservations(ranged).Length;
            Assert.That(reservations, Is.GreaterThan(0));
            int commandsBeforeRecommendation = oldCommands;
            Task<CommanderAIChatSubmission> request = chat.SubmitMessageAsync("prepare fortified defenses");
            while (!request.IsCompleted) yield return null;
            Assert.That(request.IsFaulted, Is.False, request.Exception?.ToString());
            Assert.That(chat.PendingStrategicIntent, Is.Not.Null);
            Assert.That(chat.PendingAdaptationProposal, Is.Not.Null);
            Assert.That(ranged.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(ranged.Revision, Is.EqualTo(rangedRevision));
            Assert.That(goals.Goals.Count, Is.EqualTo(goalCount));
            Assert.That(ActiveReservations(ranged).Length, Is.EqualTo(reservations));
            Assert.That(oldCommands, Is.EqualTo(commandsBeforeRecommendation));
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
            for (int tick = 0; tick < 5; tick++) Advance();
            Assert.That(ranged.Status, Is.EqualTo(StrategicPlanStatus.Active),
                "A pending recommendation must not stop the already-approved strategy.");
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(chat.PendingStrategicIntent, Is.Not.Null);
            Assert.That(chat.PendingAdaptationProposal, Is.Not.Null);
            Assert.That(ActiveReservations(ranged).Length, Is.EqualTo(reservations));

            int[] oldGoalIds = goals.Goals.Select(goal => goal.GoalId).ToArray();
            StrategicDecisionRecord decision = chat.ConfirmStrategicCommand();
            Assert.That(decision?.Submission?.CreatedPlan, Is.True, decision?.Outcome);
            StrategicPlan defensive = decision.Submission.Plan;
            Assert.That(defensive.PlanType, Is.EqualTo(StrategicPlanType.DefensiveTurtle));
            Assert.That(defensive.StrategicPlanId, Is.Not.EqualTo(ranged.StrategicPlanId));
            Assert.That(ranged.Status, Is.EqualTo(StrategicPlanStatus.Cancelled));
            Assert.That(ActiveReservations(ranged), Is.Empty);
            Assert.That(defensive.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(planner.Reservations.Where(r => r.Status == StrategicResourceReservationStatus.Active)
                .All(r => r.PlanId == defensive.StrategicPlanId), Is.True,
                "Any active reservation after replacement must belong to the new plan.");
            int cancelledRevision = ranged.Revision;
            int cancelledMilestone = ranged.CurrentMilestone.MilestoneId;
            int[] cancelledChildren = ranged.ChildGoalIds.ToArray();
            var cancelledGoalStates = goals.Goals.Select(goal =>
                (goal.GoalId, goal.Status, goal.StatusReason, goal.ParentGoalId)).ToArray();
            int[] goalIdsAtReplacement = goals.Goals.Select(goal => goal.GoalId).ToArray();
            int newMilestone = defensive.CurrentMilestone.MilestoneId;
            Assert.That(planner.CaptureControlRequest(0, defensive.StrategicPlanId,
                StrategicPlanControlType.Pause, out StrategicPlanControlRequest pauseDefensive), Is.True);
            Assert.That(planner.ApplyControl(pauseDefensive).Status,
                Is.EqualTo(StrategicPlanControlStatus.Applied));
            int commandsAtIsolation = oldCommands;
            for (int tick = 0; tick < 1200; tick++)
            {
                Advance();
                if (tick % 300 == 0) yield return null;
            }
            Assert.That(defensive.Status, Is.EqualTo(StrategicPlanStatus.Paused));
            Assert.That(defensive.CurrentMilestone.MilestoneId, Is.EqualTo(newMilestone));
            Assert.That(goals.Goals.Select(goal => goal.GoalId), Is.EqualTo(goalIdsAtReplacement),
                "No old orphan goal or paused replacement goal may be admitted during isolation.");
            Assert.That(goals.Goals.Select(goal =>
                (goal.GoalId, goal.Status, goal.StatusReason, goal.ParentGoalId)).ToArray(),
                Is.EqualTo(cancelledGoalStates),
                "All pre-replacement goals, including orphaned work, must remain frozen.");
            Assert.That(oldCommands, Is.EqualTo(commandsAtIsolation),
                "With Plan B paused, no cancelled Plan A work may enqueue Commander commands.");
            Assert.That(ActiveReservations(ranged), Is.Empty);
            Assert.That(planner.CaptureControlRequest(0, defensive.StrategicPlanId,
                StrategicPlanControlType.Resume, out StrategicPlanControlRequest resumeDefensive), Is.True);
            Assert.That(planner.ApplyControl(resumeDefensive).Status,
                Is.EqualTo(StrategicPlanControlStatus.Applied));
            for (int tick = 0; tick < 1200; tick++)
            {
                Advance();
                if (tick % 300 == 0) yield return null;
            }
            Assert.That(ranged.Status, Is.EqualTo(StrategicPlanStatus.Cancelled));
            Assert.That(ranged.Revision, Is.EqualTo(cancelledRevision));
            Assert.That(ranged.CurrentMilestone.MilestoneId, Is.EqualTo(cancelledMilestone));
            Assert.That(ranged.ChildGoalIds, Is.EqualTo(cancelledChildren));
            Assert.That(goals.Goals.Where(goal => oldGoalIds.Contains(goal.GoalId))
                .Select(goal => (goal.GoalId, goal.Status, goal.StatusReason, goal.ParentGoalId)).ToArray(),
                Is.EqualTo(cancelledGoalStates.Where(goal => oldGoalIds.Contains(goal.GoalId)).ToArray()),
                "Cancelled Plan A goals, including any orphaned work, must remain frozen after B resumes.");
            Assert.That(ActiveReservations(ranged), Is.Empty);
            Assert.That(defensive.CurrentMilestone.MilestoneId, Is.GreaterThan(newMilestone),
                "The replacement plan itself must advance through real milestone work.");
            int[] subsequentGoalIds = goals.Goals.Select(goal => goal.GoalId)
                .Except(goalIdsAtReplacement).ToArray();
            Assert.That(subsequentGoalIds, Is.Not.Empty);
            Assert.That(subsequentGoalIds.All(id => defensive.ChildGoalIds.Contains(id)), Is.True,
                "Every newly admitted strategic goal must be linked to replacement Plan B.");
            Assert.That(subsequentGoalIds.Any(id => goals.GetGoal(id)?.Status
                != CommanderGoalStatus.Pending), Is.True,
                "Replacement-owned child goals must perform real work.");
            Assert.That(subsequentGoalIds.Intersect(ranged.ChildGoalIds), Is.Empty);
            Assert.That(planner.ActivePlans.All(plan => plan.StrategicPlanId != ranged.StrategicPlanId),
                Is.True);
            Debug.Log($"[Phase4D4 D] ranged={ranged.StrategicPlanId} defensive={defensive.StrategicPlanId} partial={partial} isolatedTicks=1200 resumedTicks=1200 oldMilestone={rangedMilestone}/{cancelledMilestone} newMilestone={newMilestone}/{defensive.CurrentMilestone.MilestoneId} replacementGoals={string.Join(",", subsequentGoalIds)} commands={commandsAtIsolation}->{oldCommands}");
        }

        [UnityTest]
        public IEnumerator ScenarioE_CapturedPlanAControlRejectedAfterCancelAndPlanBAdmission()
        {
            StrategicPlan planA = null;
            yield return Approve("prepare ranged reinforcements", StrategicPlanType.RangedReinforcement,
                plan => planA = plan);
            Assert.That(planner.CaptureControlRequest(0, planA.StrategicPlanId,
                StrategicPlanControlType.Pause, out StrategicPlanControlRequest stalePause), Is.True);
            Assert.That(planner.CaptureControlRequest(0, planA.StrategicPlanId,
                StrategicPlanControlType.Cancel, out StrategicPlanControlRequest cancel), Is.True);
            Assert.That(planner.ApplyControl(cancel).Status, Is.EqualTo(StrategicPlanControlStatus.Applied));
            Task<CommanderAIChatSubmission> translation = chat.SubmitMessageAsync("prepare fortified defenses");
            while (!translation.IsCompleted) yield return null;
            Assert.That(translation.IsFaulted, Is.False, translation.Exception?.ToString());
            Assert.That(chat.PendingStrategicIntent, Is.Not.Null);
            ButtonNamed("Approve strategy").onClick.Invoke();
            StrategicIntentSubmission submission = chat.LatestStrategicDecision?.Submission;
            Assert.That(submission?.CreatedPlan, Is.True, chat.LatestStrategicDecision?.Outcome);
            StrategicPlan planB = submission.Plan;
            Assert.That(planB.PlanType, Is.EqualTo(StrategicPlanType.DefensiveTurtle));
            for (int tick = 0; tick < 1200 && ActiveReservations(planB).Length == 0; tick++)
            {
                Advance();
                if (tick % 300 == 0) yield return null;
            }
            Assert.That(planB.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(ActiveReservations(planB), Is.Not.Empty,
                "Plan B must have real resources at risk when the stale control is applied.");
            int revision = planB.Revision;
            int milestone = planB.CurrentMilestone.MilestoneId;
            int[] children = planB.ChildGoalIds.ToArray();
            int goalsBefore = goals.Goals.Count;
            var goalStates = goals.Goals.Select(goal =>
                (goal.GoalId, goal.Status, goal.StatusReason, goal.ParentGoalId)).ToArray();
            var reservations = ActiveReservations(planB);
            int commands = 0;
            simulation.CommandBuffer.CommandEnqueued += (_, source) =>
            {
                if (source == CommandEnqueueSource.Commander) commands++;
            };
            StrategicPlanControlResult result = planner.ApplyControl(stalePause);
            Assert.That(result.Status, Is.EqualTo(StrategicPlanControlStatus.StalePlan));
            Assert.That(result.PlanId, Is.EqualTo(planA.StrategicPlanId));
            Assert.That(planB.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(planB.Revision, Is.EqualTo(revision));
            Assert.That(planB.CurrentMilestone.MilestoneId, Is.EqualTo(milestone));
            Assert.That(planB.ChildGoalIds, Is.EqualTo(children));
            Assert.That(goals.Goals.Count, Is.EqualTo(goalsBefore));
            Assert.That(goals.Goals.Select(goal =>
                (goal.GoalId, goal.Status, goal.StatusReason, goal.ParentGoalId)).ToArray(),
                Is.EqualTo(goalStates));
            Assert.That(ActiveReservations(planB), Is.EqualTo(reservations));
            Assert.That(commands, Is.Zero);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
            Advance();
            Assert.That(planB.Status, Is.EqualTo(StrategicPlanStatus.Active),
                "The rejected stale request must not stop Plan B on its next runtime tick.");
            Debug.Log($"[Phase4D4 E] stale={result.Status} A={planA.StrategicPlanId} B={planB.StrategicPlanId} goals={goalsBefore} reservations={reservations.Length}");
        }

        [UnityTest]
        public IEnumerator MatchResetDuringHeldProviderReply_CannotLeakIntoNewRuntime()
        {
            StrategicPlan oldPlan = null;
            yield return Approve("prepare ranged reinforcements", StrategicPlanType.RangedReinforcement,
                plan => oldPlan = plan);
            Assert.That(oldPlan.Status, Is.EqualTo(StrategicPlanStatus.Active));
            var held = new HeldStrategicProvider();
            chat.InitializeStrategic(held, pipeline);
            yield return null;
            Assert.That((int)typeof(CommanderChatUI).GetField("selectedPlanId",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(chat),
                Is.EqualTo(oldPlan.StrategicPlanId));
            object oldFeed = typeof(CommanderChatUI).GetField("advisoryFeed",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(chat);
            Assert.That(((System.Collections.ICollection)oldFeed.GetType().GetField("plans",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(oldFeed)).Count,
                Is.GreaterThan(0), "The old match must have real advisory observation to clear.");
            Task<CommanderAIChatSubmission> oldTranslation = chat.SubmitMessageAsync("prepare fortified defenses");
            Assert.That(held.Calls, Is.EqualTo(1));
            Assert.That(planner.CaptureControlRequest(0, oldPlan.StrategicPlanId,
                StrategicPlanControlType.Pause, out StrategicPlanControlRequest endedMatchControl), Is.True);
            GameSimulation laterSimulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            laterSimulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            var laterGoals = new CommanderGoalManager(laterSimulation, 0);
            var laterPlanner = new StrategicPlanner(laterGoals, _ => 5000);
            var laterPipeline = new StrategicPipeline(laterSimulation, laterGoals, laterPlanner);
            var laterDispatcher = new CommanderIntentDispatcher(laterSimulation, laterGoals,
                strategicPlanner: laterPlanner);
            int lateCommanderCommands = 0;
            laterSimulation.CommandBuffer.CommandEnqueued += (_, source) =>
            {
                if (source == CommandEnqueueSource.Commander) lateCommanderCommands++;
            };
            try
            {
                chat.ResetConversation(); // end the old match while provider work is still held
                Assert.That(planner.CaptureControlRequest(0, oldPlan.StrategicPlanId,
                    StrategicPlanControlType.Cancel, out StrategicPlanControlRequest endOldPlan), Is.True);
                Assert.That(planner.ApplyControl(endOldPlan).Status,
                    Is.EqualTo(StrategicPlanControlStatus.Applied));
                Assert.That(oldPlan.Status, Is.EqualTo(StrategicPlanStatus.Cancelled));
                Assert.That(planner.GetReservationsForPlan(oldPlan.StrategicPlanId)
                    .Any(reservation => reservation.Status == StrategicResourceReservationStatus.Active),
                    Is.False);
                dispatcher.Dispose();
                pipeline.Dispose();
                planner.Dispose();
                goals.Dispose();
                Assert.That(planner.ApplyControl(endedMatchControl).Status,
                    Is.EqualTo(StrategicPlanControlStatus.StalePlan),
                    "The old Commander authority must be disposed before the late reply arrives.");
                chat.Initialize(new MockAIProvider(), laterSimulation, laterGoals, laterDispatcher);
                chat.InitializeStrategic(new MockStrategicAIProvider(), laterPipeline);
                held.Release();
                while (!oldTranslation.IsCompleted) yield return null;
                yield return null;
                Assert.That(oldTranslation.IsFaulted, Is.False,
                    oldTranslation.Exception?.ToString() ?? string.Empty);
                Assert.That(chat.PendingStrategicIntent, Is.Null);
                Assert.That(chat.PendingAdaptationProposal, Is.Null);
                Assert.That(chat.LatestStrategicInterpretation, Is.Null);
                Assert.That(chat.LatestStrategicDecision, Is.Null);
                Assert.That(chat.LatestSubmission, Is.Null);
                Assert.That(chat.Conversation.Memory.Count, Is.Zero);
                Assert.That(chat.DisplayedTranscript, Does.Not.Contain("prepare fortified defenses"));
                Assert.That((int)typeof(CommanderChatUI).GetField("selectedPlanId",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(chat), Is.Zero);
                Component statusText = (Component)typeof(CommanderChatUI).GetField(
                    "currentPlanStatusText", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(chat);
                Assert.That((string)statusText.GetType().GetProperty("text").GetValue(statusText),
                    Is.EqualTo("No active strategy."));
                object feed = typeof(CommanderChatUI).GetField("advisoryFeed",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(chat);
                Assert.That(((System.Collections.ICollection)feed.GetType().GetField("plans",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(feed)).Count, Is.Zero);
                Assert.That(laterPlanner.Plans, Is.Empty);
                Assert.That(laterPlanner.Intents, Is.Empty);
                Assert.That(laterPlanner.Reservations, Is.Empty);
                Assert.That(laterGoals.Goals, Is.Empty);
                Assert.That(laterPlanner.CapturePlanHealth(0, 1), Is.Null);
                Assert.That(laterPlanner.CaptureCurrentControlRequest(0,
                    StrategicPlanControlType.Pause, out _), Is.False);
                Assert.That(chat.ConfirmStrategicCommand(), Is.Null);
                Assert.That(laterSimulation.CommandBuffer.FlushCommands(), Is.Empty);
                Assert.That(planner.ApplyControl(endedMatchControl).Status,
                    Is.EqualTo(StrategicPlanControlStatus.StalePlan));
                for (int tick = 0; tick < 120; tick++)
                {
                    laterPlanner.Tick(laterSimulation.CurrentTick);
                    laterGoals.Tick(laterSimulation.CurrentTick);
                    laterSimulation.Tick();
                }
                Assert.That(laterPlanner.Plans, Is.Empty);
                Assert.That(laterGoals.Goals, Is.Empty);
                Assert.That(laterPlanner.Reservations, Is.Empty);
                Assert.That(laterSimulation.CommandBuffer.FlushCommands(), Is.Empty);
                Assert.That(lateCommanderCommands, Is.Zero);
                Assert.That(chat.Conversation.Memory.Count, Is.Zero);
                Assert.That(chat.PendingAdaptationProposal, Is.Null);
                Debug.Log("[Phase4D4 F] old match reset and commander authority disposed before held provider reply; new pending=0 memory=0 plans=0 commands=0");
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
