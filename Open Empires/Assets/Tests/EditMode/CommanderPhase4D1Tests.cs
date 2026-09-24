using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4D1")]
    public sealed class CommanderPhase4D1Tests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private StrategicPlanner planner;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            goals = new CommanderGoalManager(simulation, 0);
            planner = new StrategicPlanner(goals, Resource);
        }

        [TearDown]
        public void TearDown()
        {
            planner?.Dispose();
            goals?.Dispose();
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void PauseStrategy_StopsFutureStrategicProgress()
        {
            RichWorld();
            StrategicPlan plan = planner.StartCavalryPressurePlan();
            int milestone = plan.CurrentMilestone.MilestoneId;
            int children = plan.ChildGoalIds.Count;
            var reservations = planner.GetReservationsForPlan(plan.StrategicPlanId)
                .Where(value => value.Status == StrategicResourceReservationStatus.Active)
                .Select(value => (value.ReservationId, value.ResourceType, value.Amount)).ToArray();
            Assert.That(reservations, Is.Not.Empty);
            int commanderCommands = 0;
            simulation.CommandBuffer.CommandEnqueued += (_, source) =>
            {
                if (source == CommandEnqueueSource.Commander) commanderCommands++;
            };
            Assert.That(Apply(Capture(0, plan, "Pause")), Is.EqualTo("Applied"));
            for (int tick = 30; tick <= 300; tick += 30)
            {
                planner.Tick(tick);
                goals.Tick(tick);
            }
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Paused));
            Assert.That(plan.CurrentMilestone.MilestoneId, Is.EqualTo(milestone));
            Assert.That(plan.ChildGoalIds.Count, Is.EqualTo(children));
            Assert.That(commanderCommands, Is.Zero);
            Assert.That(planner.GetReservationsForPlan(plan.StrategicPlanId)
                .Where(value => value.Status == StrategicResourceReservationStatus.Active)
                .Select(value => (value.ReservationId, value.ResourceType, value.Amount)).ToArray(),
                Is.EqualTo(reservations));
        }

        [Test]
        public void ResumeStrategy_ContinuesSamePlan()
        {
            StrategicPlan plan = StartPlan();
            int id = plan.StrategicPlanId;
            int children = plan.ChildGoalIds.Count;
            Apply(Capture(0, plan, "Pause"));
            Assert.That(Apply(Capture(0, plan, "Resume")), Is.EqualTo("Applied"));
            Assert.That(plan.StrategicPlanId, Is.EqualTo(id));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
            AddGatherers(ResourceType.Food, 8, -20);
            AddGatherers(ResourceType.Wood, 8, 2);
            goals.Tick(15);
            Assert.That(plan.Milestones[0].Status, Is.EqualTo(StrategicMilestoneStatus.Completed));
            Assert.That(plan.CurrentMilestone.MilestoneId, Is.EqualTo(2));
            Assert.That(plan.ChildGoalIds.Count, Is.GreaterThan(children));
        }

        [Test]
        public void PausedChildCancellation_FailsPlanOnlyAfterResume()
        {
            StrategicPlan plan = StartPlan();
            int childId = plan.ChildGoalIds[0];
            Assert.That(Apply(Capture(0, plan, "Pause")), Is.EqualTo("Applied"));
            Assert.That(goals.CancelGoal(childId), Is.True);
            Assert.That(goals.GetGoal(childId), Is.Not.Null,
                "A newly archived child remains available for deterministic resume reconciliation.");
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Paused));
            Assert.That(plan.CurrentMilestone.Status, Is.EqualTo(StrategicMilestoneStatus.Active));
            Assert.That(Apply(Capture(0, plan, "Resume")), Is.EqualTo("Applied"));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Failed));
            Assert.That(planner.GetReservationsForPlan(plan.StrategicPlanId)
                .All(value => value.Status != StrategicResourceReservationStatus.Active), Is.True);
        }

        [TestCase(CommanderGoalStatus.Completed, CommanderGoalEventType.GoalCompleted)]
        [TestCase(CommanderGoalStatus.Failed, CommanderGoalEventType.GoalFailed)]
        public void PausedChildTerminalEvent_IsReconciledOnResume(
            CommanderGoalStatus terminalStatus, CommanderGoalEventType eventType)
        {
            StrategicPlan plan = StartPlan();
            CommanderGoal child = goals.GetGoal(plan.ChildGoalIds[0]);
            Assert.That(Apply(Capture(0, plan, "Pause")), Is.EqualTo("Applied"));
            child.SetStatus(terminalStatus, "Finished by an already-dispatched atomic action.");
            typeof(StrategicPlanner).GetMethod("HandleGoalEvent",
                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(planner,
                    new object[] { new CommanderGoalEvent(eventType, simulation.CurrentTick, child) });
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Paused));
            Assert.That(plan.CurrentMilestone.CompletedChildGoals, Is.Empty);
            Assert.That(Apply(Capture(0, plan, "Resume")), Is.EqualTo("Applied"));
            if (terminalStatus == CommanderGoalStatus.Completed)
                Assert.That(plan.Milestones[0].CompletedChildGoals, Does.Contain(child.GoalId));
            else
                Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Failed));
        }

        [Test]
        public void CancellationRevisionOverflow_FailsBeforeAnyMutation()
        {
            RichWorld();
            StrategicPlan plan = planner.StartCavalryPressurePlan();
            var reservations = planner.GetReservationsForPlan(plan.StrategicPlanId)
                .Where(value => value.Status == StrategicResourceReservationStatus.Active).ToArray();
            Assert.That(reservations.Length, Is.GreaterThanOrEqualTo(2));
            SetRevision(plan, int.MaxValue - 1);
            Assert.That(Apply(Capture(0, plan, "Cancel")), Is.EqualTo("Rejected"));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(reservations.All(value => value.Status == StrategicResourceReservationStatus.Active), Is.True);
            Assert.That(plan.ChildGoalIds.Select(goals.GetGoal).All(goal => !goal.IsTerminal), Is.True);
        }

        [Test]
        public void ReplacementCannotProceedWhenConflictingCancellationCannotComplete()
        {
            RichWorld();
            StrategicPlan old = planner.StartCavalryPressurePlan();
            Assert.That(planner.GetReservationsForPlan(old.StrategicPlanId)
                .Count(value => value.Status == StrategicResourceReservationStatus.Active),
                Is.GreaterThanOrEqualTo(2));
            SetRevision(old, int.MaxValue - 1);
            StrategicIntent replacement = planner.CreateIntent(StrategicObjectiveType.DefensivePreparation);
            var submission = planner.SubmitIntent(replacement, isEmergency: false, isPlayerOverride: true);
            Assert.That(submission.CreatedPlan, Is.False);
            Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(planner.ActivePlans, Has.Count.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReservationUpdateRevisionOverflow_FailsBeforeMutation(bool release)
        {
            RichWorld();
            StrategicPlan plan = planner.StartCavalryPressurePlan();
            StrategicResourceReservation reservation = planner.GetReservationsForPlan(plan.StrategicPlanId)
                .First(value => value.Status == StrategicResourceReservationStatus.Active);
            int amount = reservation.Amount;
            SetRevision(plan, int.MaxValue);
            Assert.That(planner.UpdateReservationAmount(reservation.ReservationId,
                release ? 0 : amount + 1), Is.False);
            Assert.That(reservation.Amount, Is.EqualTo(amount));
            Assert.That(reservation.Status, Is.EqualTo(StrategicResourceReservationStatus.Active));
        }

        [Test]
        public void ReservationReleaseRevisionOverflow_FailsBeforeMutation()
        {
            RichWorld();
            StrategicPlan plan = planner.StartCavalryPressurePlan();
            StrategicResourceReservation reservation = planner.GetReservationsForPlan(plan.StrategicPlanId)
                .First(value => value.Status == StrategicResourceReservationStatus.Active);
            SetRevision(plan, int.MaxValue);
            Assert.That(planner.ReleaseReservation(reservation.ReservationId), Is.False);
            Assert.That(reservation.Status, Is.EqualTo(StrategicResourceReservationStatus.Active));
        }

        [Test]
        public void ResumeWithTerminalChild_RevisionOverflowFailsBeforeAnchorMutation()
        {
            StrategicPlan plan = StartPlan();
            int terminalChildId = plan.ChildGoalIds[0];
            CommanderGoal survivingChild = goals.GetGoal(plan.ChildGoalIds[1]);
            Assert.That(Apply(Capture(0, plan, "Pause")), Is.EqualTo("Applied"));
            Assert.That(goals.CancelGoal(terminalChildId), Is.True);
            int originalCreatedTick = survivingChild.CreatedTick;
            SetRevision(plan, int.MaxValue - 1);
            Assert.That(Apply(Capture(0, plan, "Resume")), Is.EqualTo("Rejected"));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Paused));
            Assert.That(survivingChild.CreatedTick, Is.EqualTo(originalCreatedTick));
        }

        [Test]
        public void ChildCompletionRevisionExhaustion_LeavesMilestoneAndReservationsUntouched()
        {
            StrategicPlan plan = StartPlan();
            StrategicMilestone milestone = plan.CurrentMilestone;
            foreach (int id in milestone.RequiredChildGoals.Take(milestone.RequiredChildGoals.Count - 1))
            {
                CommanderGoal earlier = goals.GetGoal(id);
                earlier.SetStatus(CommanderGoalStatus.Completed, "Completed before revision boundary.");
                PublishChildEvent(earlier, CommanderGoalEventType.GoalCompleted);
            }
            int beforeCompleted = milestone.CompletedChildGoals.Count;
            int lastId = milestone.RequiredChildGoals.Last();
            CommanderGoal last = goals.GetGoal(lastId);
            var reservations = planner.GetReservationsForPlan(plan.StrategicPlanId)
                .Where(value => value.Status == StrategicResourceReservationStatus.Active).ToArray();
            SetRevision(plan, int.MaxValue - 1);
            last.SetStatus(CommanderGoalStatus.Completed, "Completed at revision boundary.");

            Assert.DoesNotThrow(() => PublishChildEvent(last, CommanderGoalEventType.GoalCompleted));
            Assert.That(plan.Revision, Is.EqualTo(int.MaxValue - 1));
            Assert.That(milestone.CompletedChildGoals.Count, Is.EqualTo(beforeCompleted));
            Assert.That(milestone.Status, Is.EqualTo(StrategicMilestoneStatus.Active));
            Assert.That(reservations.All(value => value.Status == StrategicResourceReservationStatus.Active), Is.True);
        }

        [Test]
        public void ChildFailureRevisionExhaustion_DoesNotPartiallyFailPlan()
        {
            RichWorld();
            StrategicPlan plan = planner.StartCavalryPressurePlan();
            CommanderGoal child = goals.GetGoal(plan.ChildGoalIds[0]);
            var reservations = planner.GetReservationsForPlan(plan.StrategicPlanId)
                .Where(value => value.Status == StrategicResourceReservationStatus.Active).ToArray();
            Assert.That(reservations, Is.Not.Empty);
            SetRevision(plan, int.MaxValue);
            child.SetStatus(CommanderGoalStatus.Failed, "Atomic command failed.");

            Assert.DoesNotThrow(() => PublishChildEvent(child, CommanderGoalEventType.GoalFailed));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(plan.CurrentMilestone.Status, Is.EqualTo(StrategicMilestoneStatus.Active));
            Assert.That(reservations.All(value => value.Status == StrategicResourceReservationStatus.Active), Is.True);
        }

        [Test]
        public void PublicMilestoneAdvanceRevisionExhaustion_LeavesPlanAtSameMilestone()
        {
            StrategicPlan plan = StartPlan();
            StrategicMilestone milestone = plan.CurrentMilestone;
            SetRevision(plan, int.MaxValue);

            Assert.That(planner.CompleteMilestoneAndAdvance(plan.StrategicPlanId), Is.False);
            Assert.That(plan.CurrentMilestone, Is.SameAs(milestone));
            Assert.That(milestone.Status, Is.EqualTo(StrategicMilestoneStatus.Active));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
        }

        [Test]
        public void ChildProgressRevisionExhaustion_DoesNotThrowOrMutatePlan()
        {
            StrategicPlan plan = StartPlan();
            CommanderGoal child = goals.GetGoal(plan.ChildGoalIds[0]);
            SetRevision(plan, int.MaxValue);

            Assert.DoesNotThrow(() => PublishChildEvent(child, CommanderGoalEventType.GoalProgressChanged));
            Assert.That(plan.Revision, Is.EqualTo(int.MaxValue));
            Assert.That(plan.CurrentMilestone.Status, Is.EqualTo(StrategicMilestoneStatus.Active));
        }

        [Test]
        public void PlanCompletionRevisionExhaustion_DoesNotPartiallyCompletePlan()
        {
            RichWorld();
            StrategicPlan plan = planner.StartCavalryPressurePlan();
            var reservations = planner.GetReservationsForPlan(plan.StrategicPlanId)
                .Where(value => value.Status == StrategicResourceReservationStatus.Active).ToArray();
            Assert.That(reservations, Is.Not.Empty);
            SetRevision(plan, int.MaxValue);

            Assert.DoesNotThrow(() => InvokePlanner("CompletePlan", plan));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(reservations.All(value => value.Status == StrategicResourceReservationStatus.Active), Is.True);
        }

        [Test]
        public void ChildTrackingRevisionExhaustion_DoesNotPartiallyLinkGoal()
        {
            StrategicPlan plan = StartPlan();
            StrategicMilestone milestone = plan.CurrentMilestone;
            int beforeChildren = plan.ChildGoalIds.Count;
            int beforeRequired = milestone.RequiredChildGoals.Count;
            SetRevision(plan, int.MaxValue);

            Assert.DoesNotThrow(() => InvokePlanner("TrackChildGoal", plan, milestone, 99999));
            Assert.That(plan.ChildGoalIds.Count, Is.EqualTo(beforeChildren));
            Assert.That(milestone.RequiredChildGoals.Count, Is.EqualTo(beforeRequired));
        }

        [Test]
        public void ReservationCreatedCallbackRevisionExhaustion_DoesNotRecordReservation()
        {
            StrategicPlan plan = StartPlan();
            int beforeReservations = plan.ResourceReservationIds.Count;
            var reservation = new StrategicResourceReservation(99999, plan, ResourceType.Stone, 1);
            SetRevision(plan, int.MaxValue);

            Assert.DoesNotThrow(() => InvokePlanner("HandleReservationCreated", reservation));
            Assert.That(plan.ResourceReservationIds.Count, Is.EqualTo(beforeReservations));
        }

        [Test]
        public void ReservationReleasedCallbackRevisionExhaustion_DoesNotThrowOrPublish()
        {
            StrategicPlan plan = StartPlan();
            var reservation = new StrategicResourceReservation(99999, plan, ResourceType.Stone, 1);
            int published = 0;
            planner.ReservationReleased += _ => published++;
            SetRevision(plan, int.MaxValue);

            Assert.DoesNotThrow(() => InvokePlanner("HandleReservationReleased", reservation));
            Assert.That(plan.Revision, Is.EqualTo(int.MaxValue));
            Assert.That(published, Is.Zero);
        }

        [Test]
        public void CancelStrategy_ReleasesReservations()
        {
            RichWorld();
            StrategicPlan plan = planner.StartCavalryPressurePlan();
            Assert.That(planner.GetReservationsForPlan(plan.StrategicPlanId), Is.Not.Empty);
            Assert.That(Apply(Capture(0, plan, "Cancel")), Is.EqualTo("Applied"));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Cancelled));
            Assert.That(planner.GetReservationsForPlan(plan.StrategicPlanId)
                .All(value => value.Status == StrategicResourceReservationStatus.Cancelled), Is.True);
            Assert.That(planner.GetReservedAmountForPlan(plan.StrategicPlanId, ResourceType.Food), Is.Zero);
            Assert.That(planner.GetReservedAmountForPlan(plan.StrategicPlanId, ResourceType.Gold), Is.Zero);
        }

        [Test]
        public void CancelStrategy_DoesNotDeleteCompletedAssets()
        {
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            var building = simulation.CreateBuilding(0, BuildingType.House, x + 15, z, false);
            StrategicPlan plan = StartPlan();
            Apply(Capture(0, plan, "Cancel"));
            Assert.That(simulation.BuildingRegistry.GetBuilding(building.Id), Is.SameAs(building));
        }

        [Test]
        public void CancelStrategy_DoesNotAffectUnrelatedPlayerCommands()
        {
            StrategicPlan plan = StartPlan();
            var manual = goals.SubmitResourceAllocation(ResourceType.Gold, 1);
            Apply(Capture(0, plan, "Cancel"));
            Assert.That(manual.IsTerminal, Is.False);
            goals.Tick(30);
            Assert.That(manual.Status, Is.Not.EqualTo(CommanderGoalStatus.Cancelled));
        }

        [Test]
        public void StalePauseRequest_CannotPauseReplacementPlan()
        {
            StrategicPlan old = StartPlan();
            StrategicPlanControlRequest token = Capture(0, old, "Pause");
            planner.CancelPlan(old.StrategicPlanId);
            StrategicPlan replacement = StartPlan();
            Assert.That(Apply(token), Is.EqualTo("StalePlan"));
            Assert.That(replacement.Status, Is.EqualTo(StrategicPlanStatus.Active));
        }

        [Test]
        public void StaleCancelRequest_CannotCancelReplacementPlan()
        {
            StrategicPlan old = StartPlan();
            StrategicPlanControlRequest token = Capture(0, old, "Cancel");
            planner.CancelPlan(old.StrategicPlanId);
            StrategicPlan replacement = StartPlan();
            Assert.That(Apply(token), Is.EqualTo("StalePlan"));
            Assert.That(replacement.Status, Is.EqualTo(StrategicPlanStatus.Active));
        }

        [Test]
        public void PlayerCannotPauseOtherPlayersPlan()
        {
            AssertCrossPlayerControlRejected("Pause");
        }

        [Test]
        public void PlayerCannotCancelOtherPlayersPlan()
        {
            AssertCrossPlayerControlRejected("Cancel");
        }

        [Test]
        public void PlayerCannotResumeOtherPlayersPlan()
        {
            AssertCrossPlayerControlRejected("Resume");
        }

        private void AssertCrossPlayerControlRejected(string action)
        {
            StrategicPlan plan = StartPlan();
            StrategicPlanControlRequest token = Capture(1, plan, action);
            Assert.That(Apply(token), Is.EqualTo("Unauthorized"));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
        }

        [Test]
        public void LifecycleCommands_CannotBypassOwnership()
        {
            StrategicPlan plan = StartPlan();
            Assert.That(Apply(Capture(1, plan, "Cancel")), Is.EqualTo("Unauthorized"));
            Assert.That(plan.IsTerminal, Is.False);
        }

        [Test]
        public void SameLifecycleStateProducesSameResult()
        {
            StrategicPlan plan = StartPlan();
            string first = Apply(Capture(0, plan, "Status"));
            string second = Apply(Capture(0, plan, "Status"));
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.EqualTo("Applied"));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
        }

        [Test]
        public void UnknownAction_IsRejectedWithoutMutation()
        {
            StrategicPlan plan = StartPlan();
            Assert.That(Apply(Capture(0, plan, "99")), Is.EqualTo("Rejected"));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
        }

        [Test]
        public void MultipleActivePlans_CurrentCaptureIsAmbiguous()
        {
            StrategicPlan first = StartPlan();
            StrategicPlan second = StartPlan();
            Assert.That(first.IsTerminal || second.IsTerminal, Is.False);
            Assert.That(CaptureCurrent(0, "Pause"), Is.EqualTo("AmbiguousPlan"));
            Assert.That(first.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(second.Status, Is.EqualTo(StrategicPlanStatus.Active));
        }

        [Test]
        public void Reset_ClearsLifecycleState()
        {
            StrategicPlan plan = StartPlan();
            StrategicPlanControlRequest old = Capture(0, plan, "Pause");
            StrategicPlanner oldPlanner = planner;
            planner.Dispose();
            goals.Dispose();
            goals = new CommanderGoalManager(simulation, 0);
            planner = new StrategicPlanner(goals, Resource);
            StrategicPlan newPlan = StartPlan();
            Assert.That(ApplyOn(oldPlanner, old), Is.EqualTo("StalePlan"));
            Assert.That(newPlan.Status, Is.EqualTo(StrategicPlanStatus.Active));
        }

        [Test]
        public void LongPause_PreservesGoalDurationAndRetryBudget()
        {
            StrategicPlan plan = StartPlan();
            int[] ids = plan.ChildGoalIds.ToArray();
            Apply(Capture(0, plan, "Pause"));
            goals.Tick(36030);
            planner.Tick(36030);
            Assert.That(ids.Select(goals.GetGoal).All(goal => goal == null || !goal.IsTerminal), Is.True);
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Paused));
        }

        [Test]
        public void LongPause_ResumeShiftsGoalDurationAnchor()
        {
            StrategicPlan plan = StartPlan();
            CommanderGoal child = goals.GetGoal(plan.ChildGoalIds[0]);
            int initialAnchor = child.CreatedTick - child.MaxDurationTicks + 60;
            typeof(CommanderGoal).GetProperty("CreatedTick").SetValue(child, initialAnchor);
            Apply(Capture(0, plan, "Pause"));
            for (int i = 0; i < 120; i++) simulation.Tick();
            goals.Tick(simulation.CurrentTick);
            Assert.That(child.IsTerminal, Is.False);
            Assert.That(Apply(Capture(0, plan, "Resume")), Is.EqualTo("Applied"));
            Assert.That(child.CreatedTick, Is.EqualTo(initialAnchor + 120));
            goals.Tick(simulation.CurrentTick + 15);
            Assert.That(child.Status, Is.Not.EqualTo(CommanderGoalStatus.Failed));
        }

        [Test]
        public void PauseStrategy_ManualGoalStillTicks()
        {
            StrategicPlan plan = StartPlan();
            CommanderGoal manual = goals.SubmitResourceAllocation(ResourceType.Stone, 1);
            Assert.That(Apply(Capture(0, plan, "Pause")), Is.EqualTo("Applied"));
            goals.Tick(15);
            Assert.That(manual.Status, Is.Not.EqualTo(CommanderGoalStatus.Pending));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Paused));
        }

        [Test]
        public void PauseStrategy_RetainsAndProtectsExistingReservations()
        {
            RichWorld();
            StrategicPlan plan = planner.StartCavalryPressurePlan();
            var reservation = planner.GetReservationsForPlan(plan.StrategicPlanId).First();
            int amount = reservation.Amount;
            Assert.That(Apply(Capture(0, plan, "Pause")), Is.EqualTo("Applied"));
            Assert.That(planner.UpdateReservationAmount(reservation.ReservationId, amount + 1), Is.False);
            Assert.That(planner.ReleaseReservation(reservation.ReservationId), Is.False);
            Assert.That(reservation.Amount, Is.EqualTo(amount));
            Assert.That(reservation.Status, Is.EqualTo(StrategicResourceReservationStatus.Active));
        }

        [Test]
        public void CancelPausedPlan_ReleasesItsReservations()
        {
            RichWorld();
            StrategicPlan plan = planner.StartCavalryPressurePlan();
            Assert.That(planner.GetReservationsForPlan(plan.StrategicPlanId), Is.Not.Empty);
            Assert.That(Apply(Capture(0, plan, "Pause")), Is.EqualTo("Applied"));
            Assert.That(Apply(Capture(0, plan, "Cancel")), Is.EqualTo("Applied"));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Cancelled));
            Assert.That(planner.GetReservationsForPlan(plan.StrategicPlanId)
                .All(value => value.Status == StrategicResourceReservationStatus.Cancelled), Is.True);
        }

        [Test]
        public void StaleRevisionRequest_CannotPauseChangedPlan()
        {
            StrategicPlan plan = StartPlan();
            StrategicPlanControlRequest stale = Capture(0, plan, "Pause");
            Assert.That(Apply(Capture(0, plan, "Status")), Is.EqualTo("Applied"));
            Assert.That(Apply(Capture(0, plan, "Pause")), Is.EqualTo("Applied"));
            Assert.That(Apply(stale), Is.EqualTo("StalePlan"));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Paused));
        }

        [Test]
        public void ResumeStrategy_ShiftsBlockedAndConstructionAnchors()
        {
            StrategicPlan plan = StartPlan();
            CommanderGoal child = goals.GetGoal(plan.ChildGoalIds[0]);
            child.BlockedSinceTick = 10;
            child.NextBlockedRetryTick = 160;
            child.LastEconomyCommandTick = 5;
            child.ObservedConstructionBuildingId = 1;
            child.LastConstructionProgressTick = 12;
            child.LastConstructionRecoveryTick = 14;
            Assert.That(Apply(Capture(0, plan, "Pause")), Is.EqualTo("Applied"));
            for (int i = 0; i < 120; i++) simulation.Tick();
            Assert.That(Apply(Capture(0, plan, "Resume")), Is.EqualTo("Applied"));
            Assert.That(child.BlockedSinceTick, Is.EqualTo(130));
            Assert.That(child.NextBlockedRetryTick, Is.EqualTo(280));
            Assert.That(child.LastEconomyCommandTick, Is.EqualTo(125));
            Assert.That(child.LastConstructionProgressTick, Is.EqualTo(132));
            Assert.That(child.LastConstructionRecoveryTick, Is.EqualTo(134));
        }

        private StrategicPlan StartPlan()
        {
            RichWorld();
            StrategicPlan plan = planner.SubmitIntent(StrategicObjectiveType.RangedReinforcement).Plan;
            Assert.That(plan, Is.Not.Null);
            return plan;
        }

        private void AddGatherers(ResourceType resource, int count, int xOffset)
        {
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            ResourceNodeData node = simulation.MapData.AddResourceNode(resource,
                simulation.MapData.TileToWorldFixed(x + xOffset + 4, z + 8), 10000);
            for (int i = 0; i < count; i++)
            {
                UnitData worker = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(x + xOffset + i, z),
                    Fixed32.One, Fixed32.One, Fixed32.One);
                worker.UnitType = 0;
                worker.IsVillager = true;
                worker.CurrentHealth = worker.MaxHealth = 100;
                worker.State = UnitState.Gathering;
                worker.TargetResourceNodeId = node.Id;
            }
        }

        private static void SetRevision(StrategicPlan plan, int revision)
        {
            typeof(StrategicPlan).GetField("<Revision>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Instance).SetValue(plan, revision);
        }

        private void PublishChildEvent(CommanderGoal child, CommanderGoalEventType eventType)
        {
            typeof(StrategicPlanner).GetMethod("HandleGoalEvent",
                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(planner,
                    new object[] { new CommanderGoalEvent(eventType, simulation.CurrentTick, child) });
        }

        private void InvokePlanner(string method, params object[] args)
        {
            typeof(StrategicPlanner).GetMethod(method,
                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(planner, args);
        }

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

        private void RichWorld()
        {
            var ages = (int[])typeof(GameSimulation).GetField("playerAges",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(simulation);
            ages[0] = 3;
            var resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 5000;
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            simulation.CreateBuilding(0, BuildingType.TownCenter, x + 12, z, false, true)
                .AutoProduceVillagers = false;
            for (int i = 0; i < 8; i++)
            {
                var worker = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(x - 10 + i, z), Fixed32.One,
                    Fixed32.One, Fixed32.One);
                worker.UnitType = 0;
                worker.IsVillager = true;
                worker.CurrentHealth = worker.MaxHealth = 100;
                worker.State = UnitState.Idle;
            }
        }

        private StrategicPlanControlRequest Capture(int playerId, StrategicPlan plan, string action)
        {
            StrategicPlanControlType control = action == "99" ? (StrategicPlanControlType)99
                : (StrategicPlanControlType)Enum.Parse(typeof(StrategicPlanControlType), action);
            if (playerId == planner.PlayerId && Enum.IsDefined(typeof(StrategicPlanControlType), control))
            {
                Assert.That(planner.CaptureControlRequest(playerId, plan.StrategicPlanId,
                    control, out StrategicPlanControlRequest captured), Is.True);
                return captured;
            }
            // Forge only malformed/foreign detached values to exercise ApplyControl's second check.
            ConstructorInfo constructor = typeof(StrategicPlanControlRequest).GetConstructors(
                BindingFlags.Instance | BindingFlags.NonPublic).Single();
            return (StrategicPlanControlRequest)constructor.Invoke(new object[] { playerId,
                plan.StrategicPlanId, plan.CreatedTick, plan.Revision, control });
        }

        private string CaptureCurrent(int playerId, string action)
        {
            bool result = planner.CaptureCurrentControlRequest(playerId,
                (StrategicPlanControlType)Enum.Parse(typeof(StrategicPlanControlType), action),
                out _);
            return result ? "Applied" : "AmbiguousPlan";
        }

        private string Apply(StrategicPlanControlRequest token) => ApplyOn(planner, token);

        private string ApplyOn(StrategicPlanner target, StrategicPlanControlRequest token)
        {
            return target.ApplyControl(token).Status.ToString();
        }
    }
}
