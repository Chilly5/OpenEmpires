using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4D2")]
    public sealed class CommanderPhase4D2Tests
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
        public void ResourceWaiting_IsNotMisclassifiedAsFailure()
        {
            StrategicPlan plan = StartPlan();
            Assert.That(plan.CurrentMilestone.Status, Is.EqualTo(StrategicMilestoneStatus.WaitingForResources));
            StrategicPlanHealthSnapshot health = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            Assert.That(health, Is.Not.Null);
            Assert.That(health.PrimaryHealthCategory, Is.EqualTo(StrategicPlanHealthCategory.WaitingForResources));
            Assert.That(health.PlanStatus, Is.EqualTo(StrategicPlanStatus.Active));
        }

        [Test]
        public void PlanHealth_IsDeterministic()
        {
            StrategicPlan plan = StartPlan();
            var first = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            var second = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            Assert.That(first, Is.Not.Null);
            Assert.That(second.ToJson(), Is.EqualTo(first.ToJson()));
        }

        [Test]
        public void PlanHealth_DoesNotMutateSimulation()
        {
            StrategicPlan plan = StartPlan();
            int tick = simulation.CurrentTick;
            int revision = plan.Revision;
            int reservations = planner.Reservations.Count;
            int goalsBefore = goals.Goals.Count;
            planner.CapturePlanHealth(0, plan.StrategicPlanId);
            Assert.That(simulation.CurrentTick, Is.EqualTo(tick));
            Assert.That(plan.Revision, Is.EqualTo(revision));
            Assert.That(planner.Reservations.Count, Is.EqualTo(reservations));
            Assert.That(goals.Goals.Count, Is.EqualTo(goalsBefore));
        }

        [Test]
        public void ForeignAndUnknownPlan_ReturnNoHealth()
        {
            StrategicPlan plan = StartPlan();
            Assert.That(planner.CapturePlanHealth(1, plan.StrategicPlanId), Is.Null);
            Assert.That(planner.CapturePlanHealth(0, plan.StrategicPlanId + 1000), Is.Null);
        }

        [Test]
        public void SameStateProducesSameHealthSnapshot()
        {
            StrategicPlan plan = StartPlan();
            var a = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            var b = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            Assert.That(b.PlayerId, Is.EqualTo(a.PlayerId));
            Assert.That(b.Revision, Is.EqualTo(a.Revision));
            Assert.That(b.Resources.Select(x => x.MilestoneDeficit),
                Is.EqualTo(a.Resources.Select(x => x.MilestoneDeficit)));
        }

        [Test]
        public void PlanHealth_DoesNotAdvancePlan()
        {
            StrategicPlan plan = StartPlan();
            int revision = plan.Revision, childCount = plan.ChildGoalIds.Count;
            int milestone = plan.CurrentMilestone.MilestoneId;
            planner.CapturePlanHealth(0, plan.StrategicPlanId);
            Assert.That(plan.Revision, Is.EqualTo(revision));
            Assert.That(plan.ChildGoalIds.Count, Is.EqualTo(childCount));
            Assert.That(plan.CurrentMilestone.MilestoneId, Is.EqualTo(milestone));
        }

        [Test]
        public void ReservationTotals_UnchangedByHealthCapture()
        {
            StrategicPlan plan = StartPlan();
            var before = planner.Reservations.Select(r =>
                (r.ReservationId, r.ResourceType, r.Amount, r.Status)).ToArray();
            planner.CapturePlanHealth(0, plan.StrategicPlanId);
            Assert.That(planner.Reservations.Select(r =>
                (r.ReservationId, r.ResourceType, r.Amount, r.Status)).ToArray(), Is.EqualTo(before));
        }

        [Test]
        public void PlanHealth_DoesNotCallProvider()
        {
            planner.Dispose();
            int calls = 0;
            planner = new StrategicPlanner(goals, type => { calls++; return Resource(type); });
            StrategicPlan plan = StartPlan();
            calls = 0;
            Assert.That(planner.CapturePlanHealth(0, plan.StrategicPlanId), Is.Not.Null);
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void PausedPlan_ReportsPaused()
        {
            StrategicPlan plan = StartPlan();
            Assert.That(planner.CaptureControlRequest(0, plan.StrategicPlanId,
                StrategicPlanControlType.Pause, out var request), Is.True);
            Assert.That(planner.ApplyControl(request).Status, Is.EqualTo(StrategicPlanControlStatus.Applied));
            var health = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            Assert.That(health.PrimaryHealthCategory, Is.EqualTo(StrategicPlanHealthCategory.Paused));
            Assert.That(health.PlanStatus, Is.EqualTo(StrategicPlanStatus.Paused));
        }

        [Test]
        public void CancelledPlan_ReportsCancelled()
        {
            StrategicPlan plan = StartPlan();
            Assert.That(planner.CancelPlan(plan.StrategicPlanId), Is.True);
            Assert.That(planner.CapturePlanHealth(0, plan.StrategicPlanId).PrimaryHealthCategory,
                Is.EqualTo(StrategicPlanHealthCategory.Cancelled));
        }

        [Test]
        public void Reset_ClearsPlanHealthProjection()
        {
            StrategicPlan plan = StartPlan();
            var captured = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            planner.Dispose();
            Assert.That(planner.CapturePlanHealth(0, plan.StrategicPlanId), Is.Null);
            Assert.That(captured.PlanId, Is.EqualTo(plan.StrategicPlanId),
                "An already copied historical value remains intact after disposal.");
        }

        [Test]
        public void OldPlanHealth_DoesNotDescribeReplacementPlan()
        {
            StrategicPlan old = StartPlan();
            var oldSnapshot = planner.CapturePlanHealth(0, old.StrategicPlanId);
            Assert.That(planner.CancelPlan(old.StrategicPlanId), Is.True);
            StrategicPlan replacement = planner.SubmitIntent(StrategicObjectiveType.AttackPreparation).Plan;
            Assert.That(replacement, Is.Not.Null);
            Assert.That(oldSnapshot.PlanId, Is.Not.EqualTo(replacement.StrategicPlanId));
            Assert.That(planner.CapturePlanHealth(0, old.StrategicPlanId).PlanStatus,
                Is.EqualTo(StrategicPlanStatus.Cancelled));
        }

        [Test]
        public void HealthSnapshot_DoesNotRetainSimulationObjects()
        {
            StrategicPlan plan = StartPlan();
            var health = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            foreach (Type type in new[] { typeof(StrategicPlanHealthSnapshot),
                typeof(StrategicPlanHealthResource), typeof(StrategicPlanHealthChild),
                typeof(StrategicPlanHealthStatusCount) })
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance
                    | BindingFlags.NonPublic | BindingFlags.Public))
                    Assert.That(field.FieldType == typeof(GameSimulation)
                        || field.FieldType == typeof(StrategicPlan)
                        || field.FieldType == typeof(CommanderGoal)
                        || field.FieldType == typeof(CommanderContext), Is.False,
                        type.Name + "." + field.Name);
            Assert.That(health.ToJson(), Does.Not.Contain("OutcomeMessage"));
            Assert.That(health.ToJson(), Does.Not.Contain("StatusReason"));
        }

        [Test]
        public void PlanHealth_RemainsFogSafe()
        {
            StrategicPlan plan = StartPlan();
            string before = planner.CapturePlanHealth(0, plan.StrategicPlanId).ToJson();
            int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
            var enemy = simulation.UnitRegistry.CreateUnit(1,
                simulation.MapData.TileToWorldFixed(x - 30, z - 30),
                Fixed32.One, Fixed32.One, Fixed32.One);
            enemy.UnitType = CommanderIntentCatalog.KnightUnitType;
            enemy.CurrentHealth = enemy.MaxHealth = 100;
            Assert.That(planner.CapturePlanHealth(0, plan.StrategicPlanId).ToJson(), Is.EqualTo(before));
        }

        [Test]
        public void PlanBudget_DiffersFromMilestoneDeficitAndReservations()
        {
            StrategicPlan plan = StartPlan();
            var health = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            var food = health.Resources.Single(x => x.ResourceType == ResourceType.Food);
            Assert.That(food.PlanBudget, Is.GreaterThan(0));
            Assert.That(food.MilestoneRequirementKnown, Is.True);
            Assert.That(food.Owned, Is.Zero);
            Assert.That(food.MilestoneDeficit, Is.EqualTo(food.MilestoneRequirement));
            Assert.That(food.PlanActiveReservation, Is.Zero);
        }

        [Test]
        public void ChangingWorldStateWithoutRevision_ChangesHealthEvidence()
        {
            StrategicPlan plan = StartPlan();
            var before = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            simulation.ResourceManager.GetPlayerResources(0).Food += 100;
            var after = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            Assert.That(after.Revision, Is.EqualTo(before.Revision));
            Assert.That(after.Resources.Single(x => x.ResourceType == ResourceType.Food).Owned,
                Is.EqualTo(100));
            Assert.That(after.ToJson(), Is.Not.EqualTo(before.ToJson()));
        }

        [Test]
        public void UnresolvedRequirement_IsUnknownNotZero()
        {
            StrategicPlan plan = StartPlan();
            var next = plan.Milestones[1];
            Assert.That(next.RequiredResources, Is.Empty);
            // Read-only projection of the current milestone's canonical-resolution flag.
            typeof(StrategicPlan).GetField("currentMilestoneIndex",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(plan, 1);
            var health = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            Assert.That(health.Resources.All(x => !x.MilestoneRequirementKnown), Is.True);
        }

        [Test]
        public void PopulationWaiting_IsStructured()
        {
            StrategicPlan plan = StartForcePlan();
            AddUnit(CommanderIntentCatalog.KnightUnitType);
            AddUnit(CommanderIntentCatalog.KnightUnitType);
            var health = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            Assert.That(health.Population, Is.EqualTo(health.PopulationCap));
            Assert.That(health.PrimaryHealthCategory, Is.EqualTo(
                StrategicPlanHealthCategory.WaitingForPopulation));
            Assert.That(health.Children.Single(c => c.TargetUnits.HasValue).RemainingOrders,
                Is.GreaterThan(0));
        }

        [Test]
        public void PopulationMirror_UsesPlanUnitsRegistryAndQueuePredicates()
        {
            StrategicPlan plan = StartForcePlan();
            var range = simulation.BuildingRegistry.GetAllBuildings()
                .Single(b => b.PlayerId == 0 && b.Type == BuildingType.ArcheryRange);
            range.TrainingQueue.Add(CommanderIntentCatalog.ArcherUnitType);
            range.TrainingQueue.Add(CommanderIntentCatalog.KnightUnitType);
            var health = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            var child = health.Children.Single(c => c.TargetUnits.HasValue);
            Assert.That(child.MatchingQueuedUnits, Is.EqualTo(1));
            Assert.That(health.AllQueuedUnits, Is.EqualTo(2));
            Assert.That(child.RemainingOrders, Is.EqualTo(child.TargetUnits - child.OwnedLivingUnits - 1));
            Assert.That(health.PrimaryHealthCategory, Is.EqualTo(
                StrategicPlanHealthCategory.WaitingForPopulation));
        }

        [Test]
        public void MaxPopulation_DoesNotInventHouseRecovery()
        {
            StrategicPlan plan = StartForcePlan();
            int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
            for (int i = 0; i < 19; i++)
                simulation.CreateBuilding(0, BuildingType.House, x + 20 + i, z + 12, false);
            for (int i = 0; i < 192; i++) AddUnit(CommanderIntentCatalog.KnightUnitType);
            var health = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            Assert.That(health.MaximumPopulation, Is.EqualTo(health.PopulationCap));
            Assert.That(health.PrimaryHealthCategory, Is.EqualTo(
                StrategicPlanHealthCategory.WaitingForPopulation));
            Assert.That(health.ToJson(), Does.Not.Contain("House"));
        }

        [Test]
        public void CompletedPlan_ReportsCompleted()
        {
            StrategicPlan plan = StartForcePlan();
            for (int i = 0; i < RangedReinforcementPlan.ArcherTarget; i++)
                AddUnit(CommanderIntentCatalog.ArcherUnitType);
            goals.Tick(30);
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Completed));
            Assert.That(planner.CapturePlanHealth(0, plan.StrategicPlanId).PrimaryHealthCategory,
                Is.EqualTo(StrategicPlanHealthCategory.Completed));
        }

        [Test]
        public void PrimaryAndSecondaryBlockers_AreRankedAndBounded()
        {
            StrategicPlan plan = StartForcePlan();
            var child = plan.ChildGoalIds.Select(goals.GetGoal).OfType<EnsureUnitCountGoal>().Single();
            child.SetStatus(CommanderGoalStatus.Blocked, "test blocked evidence");
            for (int i = 0; i < 2; i++) AddUnit(CommanderIntentCatalog.KnightUnitType);
            var health = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            Assert.That(health.PrimaryHealthCategory, Is.EqualTo(
                StrategicPlanHealthCategory.TemporarilyBlocked));
            Assert.That(health.SecondaryHealthCategories, Does.Contain(
                StrategicPlanHealthCategory.WaitingForPopulation));
            Assert.That(health.SecondaryHealthCategories.Count, Is.LessThanOrEqualTo(8));
        }

        [Test]
        public void ArchivedGoalEviction_ReportsMissingHistory()
        {
            StrategicPlan plan = StartForcePlan();
            int childId = plan.CurrentMilestone.RequiredChildGoals.Single();
            Assert.That(planner.CancelPlan(plan.StrategicPlanId), Is.True);
            for (int i = 0; i < CommanderGoalManager.MaxArchivedGoals; i++)
            {
                var extra = goals.SubmitEnsureUnitCount(CommanderIntentCatalog.ArcherUnitType, 1);
                Assert.That(goals.CancelGoal(extra.GoalId), Is.True);
            }
            Assert.That(goals.GetGoal(childId), Is.Null);
            var health = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            Assert.That(health.MissingHistoryCount, Is.GreaterThan(0));
            var child = health.Children.Single(c => c.GoalId == childId);
            Assert.That(child.Retained, Is.False);
            Assert.That(child.FineStatus, Is.Null);
        }

        [Test]
        public void PopulationMirror_ExcludesGarrisonedUnits()
        {
            StrategicPlan plan = StartForcePlan();
            int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
            var unit = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(x - 25, z - 10),
                Fixed32.One, Fixed32.One, Fixed32.One);
            unit.UnitType = CommanderIntentCatalog.ArcherUnitType;
            unit.CurrentHealth = unit.MaxHealth = 100;
            var tc = simulation.BuildingRegistry.GetAllBuildings()
                .Single(b => b.PlayerId == 0 && b.Type == BuildingType.TownCenter);
            simulation.UnitRegistry.GarrisonUnit(unit.Id);
            tc.GarrisonedUnitIds.Add(unit.Id);
            var health = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            var child = health.Children.Single(c => c.TargetUnits.HasValue);
            Assert.That(child.OwnedLivingUnits, Is.Zero);
        }

        [Test]
        public void BlockedDuration_StopsAtPauseTickAndRejectsInvalidOrdering()
        {
            StrategicPlan plan = StartForcePlan();
            var child = plan.ChildGoalIds.Select(goals.GetGoal).OfType<EnsureUnitCountGoal>().Single();
            child.SetStatus(CommanderGoalStatus.Blocked, "typed status");
            typeof(GameSimulation).GetField("currentTick", BindingFlags.Instance
                | BindingFlags.NonPublic).SetValue(simulation, 20);
            child.BlockedSinceTick = 5;
            plan.PausedAtTick = 15;
            plan.Status = StrategicPlanStatus.Paused;
            Assert.That(planner.CapturePlanHealth(0, plan.StrategicPlanId)
                .Children.Single(c => c.GoalId == child.GoalId).BlockedDurationTicks,
                Is.EqualTo(10));
            child.BlockedSinceTick = 30;
            Assert.That(planner.CapturePlanHealth(0, plan.StrategicPlanId)
                .Children.Single(c => c.GoalId == child.GoalId).BlockedDurationTicks, Is.Null);
        }

        [Test]
        public void MalformedPlanEnum_FailsClosed()
        {
            StrategicPlan plan = StartPlan();
            plan.Status = (StrategicPlanStatus)99;
            Assert.That(planner.CapturePlanHealth(0, plan.StrategicPlanId), Is.Null);
        }

        [Test]
        public void CapturedHealth_IsDetachedFromResourceMutation()
        {
            StrategicPlan plan = StartPlan();
            var before = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            string json = before.ToJson();
            simulation.ResourceManager.GetPlayerResources(0).Food = 200;
            Assert.That(before.ToJson(), Is.EqualTo(json));
            Assert.That(planner.CapturePlanHealth(0, plan.StrategicPlanId).ToJson(),
                Is.Not.EqualTo(json));
        }

        [Test]
        public void ActiveWithoutExecutionOrProgress_IsUnknown()
        {
            StrategicPlan plan = StartPlan();
            plan.CurrentMilestone.SetStatus(StrategicMilestoneStatus.Active);
            var health = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            Assert.That(health.PrimaryHealthCategory, Is.EqualTo(StrategicPlanHealthCategory.Unknown));
        }

        [Test]
        public void SatisfiedUnitTarget_OverCapQueueDoesNotInventPopulationWait()
        {
            StrategicPlan plan = StartForcePlan();
            for (int i = 0; i < RangedReinforcementPlan.ArcherTarget; i++)
                AddUnit(CommanderIntentCatalog.ArcherUnitType);
            var range = simulation.BuildingRegistry.GetAllBuildings()
                .Single(b => b.PlayerId == 0 && b.Type == BuildingType.ArcheryRange);
            range.TrainingQueue.Add(CommanderIntentCatalog.KnightUnitType);
            var health = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            Assert.That(health.Population + health.AllQueuedUnits,
                Is.GreaterThan(health.PopulationCap));
            Assert.That(health.PrimaryHealthCategory,
                Is.Not.EqualTo(StrategicPlanHealthCategory.WaitingForPopulation));
            Assert.That(health.SecondaryHealthCategories,
                Is.Not.Member(StrategicPlanHealthCategory.WaitingForPopulation));
        }

        [Test]
        public void MilestoneName_IsDeterministicallyBounded()
        {
            StrategicPlan plan = StartPlan();
            var field = typeof(StrategicMilestone).GetField("<Name>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(plan.CurrentMilestone, new string('N', 500));
            var health = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            Assert.That(health.MilestoneName.Length, Is.LessThanOrEqualTo(96));
            Assert.That(health.ToJson(), Does.Not.Contain(new string('N', 97)));
        }

        [Test]
        public void LiveStatusCounts_IncludeChildrenBeyondDetailLimitAndInvalidStatus()
        {
            StrategicPlan plan = StartForcePlan();
            StrategicMilestone milestone = plan.CurrentMilestone;
            for (int id = 1000; id < 1130; id++) milestone.AddRequiredChildGoal(id);
            var extra = goals.SubmitEnsureUnitCount(CommanderIntentCatalog.ArcherUnitType, 1);
            extra.GoalId = 1200;
            extra.SetStatus(CommanderGoalStatus.WaitingForProduction, "queued");
            milestone.AddRequiredChildGoal(extra.GoalId);
            var health = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            Assert.That(health.Children.Count, Is.EqualTo(128));
            Assert.That(health.RequiredChildGoals, Is.EqualTo(132));
            Assert.That(health.MissingHistoryCount, Is.EqualTo(130));
            Assert.That(health.RetainedChildStatusCounts.Single(x =>
                x.Status == CommanderGoalStatus.WaitingForProduction).Count, Is.EqualTo(1));
            extra.SetStatus((CommanderGoalStatus)99, "malformed");
            var malformed = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            Assert.That(malformed.RetainedChildStatusCounts.Single(x =>
                x.Status == null).Count, Is.EqualTo(1));
        }

        [Test]
        public void OwnReservation_DoesNotCreateMilestoneDeficit()
        {
            StrategicPlan plan = StartForcePlan();
            var initial = planner.CapturePlanHealth(0, plan.StrategicPlanId)
                .Resources.Single(x => x.ResourceType == ResourceType.Food);
            Assert.That(initial.MilestoneRequirement, Is.EqualTo(300));
            Assert.That(initial.PlanActiveReservation, Is.EqualTo(300));
            simulation.ResourceManager.GetPlayerResources(0).Food = 350;
            var otherPlan = new RangedReinforcementPlan(0, 999) { StrategicPlanId = 999 };
            var other = new StrategicActiveReservation(999, otherPlan, ResourceType.Food, 50);
            var manager = typeof(StrategicPlanner).GetField("reservationManager",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(planner);
            var field = manager.GetType().GetField("reservations",
                BindingFlags.Instance | BindingFlags.NonPublic);
            ((List<StrategicResourceReservation>)field.GetValue(manager)).Add(other);
            var health = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            var food = health.Resources.Single(x => x.ResourceType == ResourceType.Food);
            Assert.That(food.PlanActiveReservation, Is.EqualTo(300));
            Assert.That(food.GlobalActiveReservation, Is.EqualTo(350));
            Assert.That(food.Available, Is.Zero);
            Assert.That(food.MilestoneDeficit, Is.Zero,
                "Own reserved stock remains usable by this plan; another plan's 50 is excluded.");
            simulation.ResourceManager.GetPlayerResources(0).Food = 320;
            Assert.That(planner.CapturePlanHealth(0, plan.StrategicPlanId).Resources
                .Single(x => x.ResourceType == ResourceType.Food).MilestoneDeficit,
                Is.EqualTo(30));
        }

        [Test]
        public void QueuedProduction_IsExpectedWaiting()
        {
            StrategicPlan plan = StartForcePlan();
            int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
            simulation.CreateBuilding(0, BuildingType.House, x + 19, z + 10, false);
            var range = simulation.BuildingRegistry.GetAllBuildings()
                .Single(b => b.PlayerId == 0 && b.Type == BuildingType.ArcheryRange);
            for (int i = 0; i < RangedReinforcementPlan.ArcherTarget; i++)
                range.TrainingQueue.Add(CommanderIntentCatalog.ArcherUnitType);
            var child = plan.ChildGoalIds.Select(goals.GetGoal).OfType<EnsureUnitCountGoal>().Single();
            child.SetStatus(CommanderGoalStatus.WaitingForProduction, "queued");
            var health = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            Assert.That(health.Children.Single(c => c.GoalId == child.GoalId).RemainingOrders, Is.Zero);
            Assert.That(health.PrimaryHealthCategory,
                Is.EqualTo(StrategicPlanHealthCategory.WaitingForProduction));
        }

        [Test]
        public void PopulationAndMilestoneResourceWait_AreBothPreserved()
        {
            StrategicPlan plan = StartForcePlan();
            plan.CurrentMilestone.SetStatus(StrategicMilestoneStatus.WaitingForResources);
            AddUnit(CommanderIntentCatalog.KnightUnitType);
            AddUnit(CommanderIntentCatalog.KnightUnitType);
            var health = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            Assert.That(health.PrimaryHealthCategory,
                Is.EqualTo(StrategicPlanHealthCategory.WaitingForPopulation));
            Assert.That(health.SecondaryHealthCategories,
                Does.Contain(StrategicPlanHealthCategory.WaitingForResources));
        }

        [Test]
        public void BlockedChild_OutranksMilestoneResourceWait()
        {
            StrategicPlan plan = StartForcePlan();
            plan.CurrentMilestone.SetStatus(StrategicMilestoneStatus.WaitingForResources);
            var child = plan.ChildGoalIds.Select(goals.GetGoal).OfType<EnsureUnitCountGoal>().Single();
            child.SetStatus(CommanderGoalStatus.Blocked, "blocked");
            var health = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            Assert.That(health.PrimaryHealthCategory,
                Is.EqualTo(StrategicPlanHealthCategory.TemporarilyBlocked));
            Assert.That(health.SecondaryHealthCategories,
                Does.Contain(StrategicPlanHealthCategory.WaitingForResources));
        }

        [Test]
        public void BlockedDuration_IntMaxBoundaryAndMissingPauseAnchorFailClosed()
        {
            StrategicPlan plan = StartForcePlan();
            var child = plan.ChildGoalIds.Select(goals.GetGoal).OfType<EnsureUnitCountGoal>().Single();
            child.SetStatus(CommanderGoalStatus.Blocked, "typed blocked status");
            typeof(GameSimulation).GetField("currentTick", BindingFlags.Instance
                | BindingFlags.NonPublic).SetValue(simulation, int.MaxValue);
            child.BlockedSinceTick = 1;
            Assert.That(planner.CapturePlanHealth(0, plan.StrategicPlanId)
                .Children.Single(c => c.GoalId == child.GoalId).BlockedDurationTicks,
                Is.EqualTo(int.MaxValue - 1));
            plan.Status = StrategicPlanStatus.Paused;
            plan.PausedAtTick = -1;
            Assert.That(planner.CapturePlanHealth(0, plan.StrategicPlanId)
                .Children.Single(c => c.GoalId == child.GoalId).BlockedDurationTicks,
                Is.Null);
        }

        [Test]
        public void MalformedUnitGoalStatus_CannotAssertPopulationWait()
        {
            StrategicPlan plan = StartForcePlan();
            var child = plan.ChildGoalIds.Select(goals.GetGoal).OfType<EnsureUnitCountGoal>().Single();
            AddUnit(CommanderIntentCatalog.KnightUnitType);
            AddUnit(CommanderIntentCatalog.KnightUnitType);
            child.SetStatus((CommanderGoalStatus)99, "invalid enum");

            var health = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            Assert.That(health.Population, Is.EqualTo(health.PopulationCap));
            Assert.That(health.Children.Single(c => c.GoalId == child.GoalId).Category,
                Is.EqualTo(StrategicPlanHealthCategory.Unknown));
            Assert.That(health.RetainedChildStatusCounts.Single(x => x.Status == null).Count,
                Is.EqualTo(1));
            Assert.That(health.PrimaryHealthCategory,
                Is.EqualTo(StrategicPlanHealthCategory.Unknown));
            Assert.That(health.SecondaryHealthCategories,
                Is.Not.Member(StrategicPlanHealthCategory.WaitingForPopulation));
        }

        [Test]
        public void OverqueuedUnitGoal_PreservesSignedRemainingOrders()
        {
            StrategicPlan plan = StartForcePlan();
            var range = simulation.BuildingRegistry.GetAllBuildings()
                .Single(b => b.PlayerId == 0 && b.Type == BuildingType.ArcheryRange);
            for (int i = 0; i < RangedReinforcementPlan.ArcherTarget + 2; i++)
                range.TrainingQueue.Add(CommanderIntentCatalog.ArcherUnitType);

            var health = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            var child = health.Children.Single(c => c.TargetUnits.HasValue);
            Assert.That(child.OwnedLivingUnits, Is.Zero);
            Assert.That(child.MatchingQueuedUnits,
                Is.EqualTo(RangedReinforcementPlan.ArcherTarget + 2));
            Assert.That(child.RemainingOrders, Is.EqualTo(-2));
            Assert.That(health.PrimaryHealthCategory,
                Is.EqualTo(StrategicPlanHealthCategory.WaitingForPopulation),
                "PlanUnits' first capacity predicate remains true when all queues exceed cap.");
        }

        [Test]
        public void HealthAnswer_PreservesFourthTypedSecondaryWithinBound()
        {
            var health = RenderFixture(StrategicPlanHealthCategory.TemporarilyBlocked,
                StrategicPlanHealthCategory.WaitingForPopulation,
                StrategicPlanHealthCategory.WaitingForResources,
                StrategicPlanHealthCategory.WaitingForProduction,
                StrategicPlanHealthCategory.WaitingForConstruction);
            string answer = RenderHealth(health);
            Assert.That(answer, Does.Contain("Temporarily blocked"));
            Assert.That(answer, Does.Contain("Waiting for construction"));
            Assert.That(answer.IndexOf("Waiting for population capacity", StringComparison.Ordinal),
                Is.LessThan(answer.IndexOf("Waiting for resources", StringComparison.Ordinal)));
            Assert.That(answer.IndexOf("Waiting for resources", StringComparison.Ordinal),
                Is.LessThan(answer.IndexOf("Waiting for production", StringComparison.Ordinal)));
            Assert.That(answer.IndexOf("Waiting for production", StringComparison.Ordinal),
                Is.LessThan(answer.IndexOf("Waiting for construction", StringComparison.Ordinal)));
            Assert.That(answer.Length, Is.LessThanOrEqualTo(512));
            Assert.That(RenderHealth(health), Is.EqualTo(answer));
        }

        [Test]
        public void HealthAnswer_MixedFactsRetainPopulationAndResourceNumbers()
        {
            var health = RenderFixture(StrategicPlanHealthCategory.TemporarilyBlocked,
                StrategicPlanHealthCategory.WaitingForPopulation,
                StrategicPlanHealthCategory.WaitingForResources);
            string answer = RenderHealth(health);
            Assert.That(answer, Does.Contain("Temporarily blocked"));
            Assert.That(answer, Does.Contain("Waiting for population capacity"));
            Assert.That(answer, Does.Contain("Waiting for resources"));
            Assert.That(answer, Does.Contain("Population 0/"));
            Assert.That(answer, Does.Contain("Wood owned 0, milestone deficit 12"));
        }

        [Test]
        public void HealthAnswer_UnknownCategoryFailsClosed()
        {
            var health = RenderFixture((StrategicPlanHealthCategory)999,
                StrategicPlanHealthCategory.WaitingForResources);
            string answer = RenderHealth(health);
            Assert.That(answer, Does.Contain("Unknown"));
            Assert.That(answer, Does.Not.Contain("Waiting for resources"));
            Assert.That(answer, Does.Not.Contain("milestone deficit"));
        }

        [Test]
        public void HealthAnswer_NumbersAreCultureInvariant()
        {
            var health = RenderFixture(StrategicPlanHealthCategory.WaitingForResources);
            CultureInfo before = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("fa-IR");
                string answer = RenderHealth(health);
                Assert.That(answer, Does.Contain("Wood owned 0, milestone deficit 12"));
                Assert.That(answer, Is.EqualTo(RenderHealth(health)));
            }
            finally { Thread.CurrentThread.CurrentCulture = before; }
        }

        private StrategicPlanHealthSnapshot RenderFixture(StrategicPlanHealthCategory primary,
            params StrategicPlanHealthCategory[] secondary)
        {
            StrategicPlan plan = StartPlan();
            var context = new CommanderContextBuilder().Build(simulation, goals);
            return new StrategicPlanHealthSnapshot(plan, plan.CurrentMilestone,
                simulation.CurrentTick, 0, 0, 0, context, 2, primary,
                secondary.ToList(), new List<StrategicPlanHealthResource> {
                    new StrategicPlanHealthResource(ResourceType.Wood, 100, true, 12, 0, 12, 0, 0)
                }, new List<StrategicPlanHealthChild>(), new List<StrategicPlanHealthStatusCount>());
        }

        private static string RenderHealth(StrategicPlanHealthSnapshot health) =>
            (string)typeof(CommanderChatUI).GetMethod("FormatHealthAnswer",
                BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { health });

        private StrategicPlan StartForcePlan()
        {
            var resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 5000;
            int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
            var ages = (int[])typeof(GameSimulation).GetField("playerAges",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(simulation);
            ages[0] = 3;
            simulation.CreateBuilding(0, BuildingType.TownCenter, x + 12, z, false, true)
                .AutoProduceVillagers = false;
            simulation.CreateBuilding(0, BuildingType.ArcheryRange, x + 15, z + 7, false);
            for (int i = 0; i < 4; i++)
            {
                AddGatherer(ResourceType.Food, x - 20 + i, z);
                AddGatherer(ResourceType.Wood, x - 10 + i, z);
            }
            StrategicPlan plan = planner.SubmitIntent(StrategicObjectiveType.RangedReinforcement).Plan;
            Assert.That(plan, Is.Not.Null);
            goals.Tick(0);
            simulation.CommandBuffer.FlushCommands();
            goals.Tick(15);
            Assert.That(plan.CurrentMilestone.Name, Is.EqualTo("Force"),
                string.Join("; ", plan.ChildGoalIds.Select(id =>
                { var g = goals.GetGoal(id); return id + ":" + g.GoalType + ":" + g.Status + ":" + g.StatusReason; })));
            return plan;
        }

        private void AddGatherer(ResourceType type, int x, int z)
        {
            var node = simulation.MapData.AddResourceNode(type,
                simulation.MapData.TileToWorldFixed(x + 4, z + 8), 10000);
            var unit = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(x, z),
                Fixed32.One, Fixed32.One, Fixed32.One);
            unit.UnitType = 0; unit.IsVillager = true;
            unit.CurrentHealth = unit.MaxHealth = 100;
            unit.State = UnitState.Gathering; unit.TargetResourceNodeId = node.Id;
        }

        private void AddUnit(int type)
        {
            int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
            var unit = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(x - 30, z - 10),
                Fixed32.One, Fixed32.One, Fixed32.One);
            unit.UnitType = type; unit.IsVillager = false;
            unit.CurrentHealth = unit.MaxHealth = 100;
            unit.State = UnitState.Idle;
        }

        private StrategicPlan StartPlan()
        {
            var resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 0;
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            simulation.CreateBuilding(0, BuildingType.TownCenter, x + 12, z, false, true)
                .AutoProduceVillagers = false;
            var plan = planner.SubmitIntent(StrategicObjectiveType.AttackPreparation).Plan;
            Assert.That(plan, Is.Not.Null);
            return plan;
        }

        private int Resource(ResourceType type)
        {
            var resources = simulation.ResourceManager.GetPlayerResources(0);
            return type == ResourceType.Food ? resources.Food
                : type == ResourceType.Wood ? resources.Wood
                : type == ResourceType.Gold ? resources.Gold : resources.Stone;
        }
    }
}
