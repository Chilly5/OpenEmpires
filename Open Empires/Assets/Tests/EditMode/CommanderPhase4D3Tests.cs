using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4D3")]
    public sealed class CommanderPhase4D3Tests
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
            var stock = simulation.ResourceManager.GetPlayerResources(0);
            stock.Food = stock.Wood = stock.Gold = stock.Stone = 5000;
        }

        [TearDown]
        public void TearDown()
        {
            planner?.Dispose();
            goals?.Dispose();
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void MaterialObjectiveChange_RequiresPlayerApproval()
        {
            var proposal = MakeProposal(out _, out _);
            Assert.That(Json(proposal), Does.Contain("\"RequiresPlayerApproval\":true"));
            Assert.That(Json(proposal), Does.Contain("\"CurrentObjective\":\"RangedReinforcement\""));
            Assert.That(Json(proposal), Does.Contain("\"ProposedObjective\":\"DefensiveTurtle\""));
        }

        [Test]
        public void AdaptationProposal_DoesNotModifyPlan()
        {
            StrategicPlan plan = Start();
            int revision = plan.Revision;
            int[] children = plan.ChildGoalIds.ToArray();
            int[] reservations = plan.ResourceReservationIds.ToArray();
            var proposal = Build(Capture(plan), NewIntent());
            Assert.That(proposal, Is.Not.Null);
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(plan.Revision, Is.EqualTo(revision));
            Assert.That(plan.ChildGoalIds, Is.EqualTo(children));
            Assert.That(plan.ResourceReservationIds, Is.EqualTo(reservations));
            Assert.That(planner.ActivePlans, Has.Count.EqualTo(1));
        }

        [Test]
        public void AdaptationProposal_DoesNotCallPlanner()
        {
            StrategicPlan plan = Start();
            object source = Capture(plan);
            int intents = planner.Intents.Count, plans = planner.Plans.Count;
            int goalsBefore = plan.ChildGoalIds.Count, reservations = planner.Reservations.Count;
            var pending = new StrategicIntent(901, 0, StrategicObjectiveType.DefensiveTurtle, 0);
            Assert.That(Build(source, pending), Is.Not.Null);
            Assert.That(planner.Intents.Count, Is.EqualTo(intents));
            Assert.That(planner.Plans.Count, Is.EqualTo(plans));
            Assert.That(plan.ChildGoalIds.Count, Is.EqualTo(goalsBefore));
            Assert.That(planner.Reservations.Count, Is.EqualTo(reservations));
        }

        [Test]
        public void AdaptationProposal_IsDetached()
        {
            var proposal = MakeProposal(out StrategicPlan plan, out _);
            string before = Json(proposal);
            int original = ((StrategicResourceAllocationGoalRequest)plan.Milestones[0]
                .TacticalGoals[0]).WorkerTarget;
            SetWorkerTarget(plan, original + 1);
            plan.AddBudgetRequirement(ResourceType.Stone, 77);
            Assert.That(Json(proposal), Is.EqualTo(before));
            Assert.That(Json(proposal).Split(new[] { "\"ProposedTargets\":" },
                StringSplitOptions.None)[0], Does.Contain("\"WorkerTarget\":" + original));
            foreach (Type dto in typeof(StrategicPlan).Assembly.GetTypes()
                .Where(t => t.Namespace == "OpenEmpires"
                    && t.Name.StartsWith("StrategicAdaptation", StringComparison.Ordinal)))
                foreach (FieldInfo field in dto.GetFields(BindingFlags.Instance |
                    BindingFlags.Public | BindingFlags.NonPublic))
                    Assert.That(ContainsLiveReference(field.FieldType), Is.False,
                        dto.Name + "." + field.Name + " retains a live authority object.");
        }

        [Test]
        public void AdaptationProposal_IsDeterministic()
        {
            StrategicPlan plan = Start(); object source = Capture(plan);
            var intent = NewIntent();
            string first = InvokeJson(Build(source, intent));
            string second = InvokeJson(Build(source, intent));
            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Does.Contain("OldCanonicalBudget"));
            Assert.That(first, Does.Contain("ProposedFeasibilityQuote"));
        }

        [Test]
        public void StaleAdaptationProposal_IsRejected()
        {
            var proposal = MakeProposal(out StrategicPlan plan, out _);
            SetRevision(plan, plan.Revision + 1);
            Assert.That(Fresh(proposal, Capture(plan)), Is.False);
        }

        [Test]
        public void ChangedCanonicalBudgetWithoutRevision_RejectsOldProposal()
        {
            var proposal = MakeProposal(out StrategicPlan plan, out _);
            int revision = plan.Revision;
            plan.AddBudgetRequirement(ResourceType.Stone, 77);
            Assert.That(plan.Revision, Is.EqualTo(revision));
            Assert.That(Capture(plan), Is.Not.Null);
            Assert.That(Fresh(proposal, Capture(plan)), Is.False,
                "A proposal showing the old approved budget must not survive a budget change.");
        }

        [Test]
        public void ChangedCurrentTargetWithoutRevision_RejectsOldProposal()
        {
            var proposal = MakeProposal(out StrategicPlan plan, out _);
            int revision = plan.Revision;
            var request = (StrategicResourceAllocationGoalRequest)plan.Milestones[0].TacticalGoals[0];
            SetWorkerTarget(plan, request.WorkerTarget + 1);
            Assert.That(plan.Revision, Is.EqualTo(revision));
            Assert.That(Capture(plan), Is.Not.Null);
            Assert.That(Fresh(proposal, Capture(plan)), Is.False,
                "A proposal showing an old worker target must not survive an unversioned target change.");
        }

        [Test]
        public void CompletedPlanRejectsOldAdaptationProposal()
        {
            var proposal = MakeProposal(out StrategicPlan plan, out _);
            SetStatus(plan, StrategicPlanStatus.Completed);
            Assert.That(Fresh(proposal, Capture(plan)), Is.False);
        }

        [Test]
        public void CancelledPlanRejectsOldAdaptationProposal()
        {
            var proposal = MakeProposal(out StrategicPlan plan, out _);
            Assert.That(planner.CancelPlan(plan.StrategicPlanId), Is.True);
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Cancelled));
            Assert.That(Fresh(proposal, Capture(plan)), Is.False);
        }

        [Test]
        public void ReplacementPlanRejectsOldProposal()
        {
            var proposal = MakeProposal(out _, out _);
            StrategicPlan replacement = planner.SubmitIntent(StrategicObjectiveType.RangedReinforcement).Plan;
            Assert.That(replacement, Is.Not.Null);
            Assert.That(Fresh(proposal, Capture(replacement)), Is.False);
        }

        [Test]
        public void BudgetIncreaseRequiresApproval()
        {
            StrategicPlan plan = Start();
            var proposal = Build(Capture(plan), NewIntent(), Quote(12345));
            Assert.That(Json(proposal), Does.Contain("\"RequiresPlayerApproval\":true"));
            Assert.That(Json(proposal), Does.Contain("\"Amount\":12345"));
            Assert.That(Json(proposal).Split(new[] { "\"ProposedFeasibilityQuote\":" },
                StringSplitOptions.None)[0], Does.Not.Contain("12345"));
            Assert.That(plan.BudgetRequirements.All(b => b.Amount < 12345), Is.True);
        }

        [Test]
        public void TargetIncreaseRequiresApproval()
        {
            var proposal = MakeProposal(out _, out _);
            Assert.That(Json(proposal), Does.Contain("\"RequiresPlayerApproval\":true"));
            string[] halves = Json(proposal).Split(new[] { "\"ProposedTargets\":" },
                StringSplitOptions.None);
            Assert.That(halves[0], Does.Not.Contain("\"StructureType\":\"Tower\""));
            Assert.That(halves[1], Does.Contain("\"StructureType\":\"Tower\",\"Count\":2"));
        }

        [Test]
        public void BlockerDisappeared_RejectsOldProposal()
        {
            StrategicPlan plan = Start();
            typeof(StrategicMilestone).GetMethod("SetStatus", BindingFlags.Instance |
                BindingFlags.NonPublic).Invoke(plan.CurrentMilestone,
                    new object[] { StrategicMilestoneStatus.WaitingForResources });
            var blocked = planner.CapturePlanHealth(0, plan.StrategicPlanId);
            Assert.That(blocked.PrimaryHealthCategory,
                Is.EqualTo(StrategicPlanHealthCategory.WaitingForResources));
            var source = Capture(plan, blocked);
            var proposal = Build(source, NewIntent());
            Assert.That(proposal, Is.Not.Null);
            typeof(StrategicMilestone).GetMethod("SetStatus", BindingFlags.Instance |
                BindingFlags.NonPublic).Invoke(plan.CurrentMilestone,
                    new object[] { StrategicMilestoneStatus.Active });
            Assert.That(Fresh(proposal, Capture(plan)), Is.False);
        }

        [Test]
        public void BlockerIdentityChangedWithSameCategory_RejectsOldProposal()
        {
            StrategicPlan plan = Start();
            CommanderGoal first = goals.GetGoal(plan.ChildGoalIds[0]);
            CommanderGoal second = goals.GetGoal(plan.ChildGoalIds[1]);
            SetGoalStatus(first, CommanderGoalStatus.Blocked);
            var source = Capture(plan);
            Assert.That(source, Is.Not.Null);
            var proposal = Build(source, NewIntent());
            SetGoalStatus(first, CommanderGoalStatus.Planning);
            SetGoalStatus(second, CommanderGoalStatus.Blocked);
            Assert.That(planner.CapturePlanHealth(0, plan.StrategicPlanId).PrimaryHealthCategory,
                Is.EqualTo(StrategicPlanHealthCategory.TemporarilyBlocked));
            Assert.That(Fresh(proposal, Capture(plan)), Is.False);
        }

        [Test]
        public void PauseResumeAndRevisionOnly_RejectOldProposal()
        {
            var proposal = MakeProposal(out StrategicPlan plan, out _);
            Assert.That(planner.CaptureControlRequest(0, plan.StrategicPlanId,
                StrategicPlanControlType.Pause, out StrategicPlanControlRequest pause), Is.True);
            Assert.That(planner.ApplyControl(pause).Status, Is.EqualTo(StrategicPlanControlStatus.Applied));
            Assert.That(Fresh(proposal, Capture(plan)), Is.False);
            Assert.That(planner.CaptureControlRequest(0, plan.StrategicPlanId,
                StrategicPlanControlType.Resume, out StrategicPlanControlRequest resume), Is.True);
            Assert.That(planner.ApplyControl(resume).Status, Is.EqualTo(StrategicPlanControlStatus.Applied));
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(Fresh(proposal, Capture(plan)), Is.False);
        }

        [Test]
        public void OwnerMismatchAndMalformedInput_FailClosed()
        {
            StrategicPlan plan = Start();
            Assert.That(Capture(plan, planner.CapturePlanHealth(1, plan.StrategicPlanId)), Is.Null);
            Assert.That(Capture(plan, null), Is.Null);
            Assert.That(Build(null, NewIntent()), Is.Null);
            Assert.That(Fresh(null, null), Is.False);
            Assert.That(Build(Capture(plan), new StrategicIntent(902, 1,
                StrategicObjectiveType.DefensiveTurtle, 0)), Is.Null);
            Assert.That(Build(Capture(plan), new StrategicIntent(903, 0,
                StrategicObjectiveType.DefensiveTurtle, 0,
                new Dictionary<string, string> { ["targetCount"] = "999" })), Is.Null);
        }

        [Test]
        public void SupportedCavalryFocus_BuildsProposalButArbitraryParametersStayRejected()
        {
            StrategicPlan old = Start();
            object source = Capture(old);
            int plansBefore = planner.Plans.Count, goalsBefore = goals.Goals.Count;
            var focus = new StrategicIntent(904, 0, StrategicObjectiveType.AttackPreparation,
                simulation.CurrentTick, new Dictionary<string, string> { ["focus"] = "cavalry" });
            object proposal = Build(source, focus);
            Assert.That(proposal, Is.Not.Null);
            Assert.That(Json(proposal), Does.Contain("\"ProposedObjective\":\"AttackPreparation\""));
            Assert.That(Json(proposal), Does.Contain("\"PendingIntentId\":904"));
            Assert.That(Build(source, new StrategicIntent(905, 0,
                StrategicObjectiveType.AttackPreparation, simulation.CurrentTick,
                new Dictionary<string, string> { ["focus"] = "infantry" })), Is.Null);
            Assert.That(Build(source, new StrategicIntent(906, 0,
                StrategicObjectiveType.AttackPreparation, simulation.CurrentTick,
                new Dictionary<string, string> { ["focus"] = "cavalry", ["budget"] = "9999" })), Is.Null);
            Assert.That(planner.Plans.Count, Is.EqualTo(plansBefore));
            Assert.That(goals.Goals.Count, Is.EqualTo(goalsBefore));
        }

        [Test]
        public void CopiedSourceRetainsOldFittedWorkerTargetAndExactRequests()
        {
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            for (int i = 0; i < 4; i++)
            {
                UnitData worker = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(x + i, z),
                    Fixed32.One, Fixed32.One, Fixed32.One);
                worker.UnitType = 0;
                worker.IsVillager = true;
                worker.CurrentHealth = worker.MaxHealth = 100;
                worker.State = UnitState.Idle;
            }
            StrategicPlan plan = Start();
            int fitted = ((StrategicResourceAllocationGoalRequest)plan.Milestones[0].TacticalGoals[0]).WorkerTarget;
            Assert.That(fitted, Is.Not.EqualTo(RangedReinforcementPlan.FoodWorkers),
                "This fixture demonstrates the fitted target differs from template constants.");
            object source = Capture(plan);
            SetWorkerTarget(plan, 0);
            var proposal = Build(source, NewIntent());
            string old = Json(proposal).Split(new[] { "\"ProposedTargets\":" },
                StringSplitOptions.None)[0];
            Assert.That(old, Does.Contain("\"WorkerTarget\":" + fitted));
            Assert.That(old, Does.Contain("\"StructureType\":\"ArcheryRange\""));
            Assert.That(old, Does.Contain("\"TargetTotal\":" + RangedReinforcementPlan.ArcherTarget));
        }

        [Test]
        public void ProposedQuoteIsNotCanonicalBudget()
        {
            StrategicPlan plan = Start();
            var quote = Quote(12345);
            var proposal = Build(Capture(plan), NewIntent(), quote);
            string json = Json(proposal);
            Assert.That(json, Does.Contain("\"Amount\":12345"));
            Assert.That(json.Split(new[] { "\"ProposedFeasibilityQuote\":" },
                StringSplitOptions.None)[0], Does.Not.Contain("12345"));
            Assert.That(plan.BudgetRequirements.Any(x => x.Amount == 12345), Is.False);
        }

        [Test]
        public void EquivalentQuoteCostOrder_ProducesIdenticalProposalJson()
        {
            StrategicPlan plan = Start();
            object source = Capture(plan);
            string first = Json(Build(source, NewIntent(), QuoteTwo(false)));
            string reversed = Json(Build(source, NewIntent(), QuoteTwo(true)));
            Assert.That(reversed, Is.EqualTo(first),
                "The detached quote must use canonical resource ordering, not caller list order.");
        }

        [Test]
        public void DuplicateQuoteResource_FailsClosed()
        {
            StrategicPlan plan = Start();
            var costs = new List<StrategicRequirementState>
            {
                Requirement(ResourceType.Food, 23), Requirement(ResourceType.Food, 17)
            };
            Assert.That(Build(Capture(plan), NewIntent(), Quote(costs)), Is.Null);
        }

        private static StrategicFeasibility Quote(int amount)
        {
            return Quote(new List<StrategicRequirementState> { Requirement(ResourceType.Food, amount) });
        }

        private static StrategicFeasibility QuoteTwo(bool reverse)
        {
            var food = Requirement(ResourceType.Food, 23);
            var wood = Requirement(ResourceType.Wood, 17);
            return Quote(reverse
                ? new List<StrategicRequirementState> { wood, food }
                : new List<StrategicRequirementState> { food, wood });
        }

        private static StrategicRequirementState Requirement(ResourceType type, int amount) =>
            (StrategicRequirementState)Activator.CreateInstance(
                typeof(StrategicRequirementState), BindingFlags.Instance |
                BindingFlags.NonPublic, null, new object[] { type, amount }, null);

        private static StrategicFeasibility Quote(List<StrategicRequirementState> costs) =>
            (StrategicFeasibility)Activator.CreateInstance(
                typeof(StrategicFeasibility), BindingFlags.Instance |
                BindingFlags.NonPublic, null, new object[] { StrategicObjectiveType.DefensiveTurtle,
                    string.Empty, costs }, null);

        private object MakeProposal(out StrategicPlan plan, out object source)
        {
            plan = Start(); source = Capture(plan);
            object proposal = Build(source, NewIntent());
            Assert.That(proposal, Is.Not.Null);
            return proposal;
        }

        private StrategicPlan Start()
        {
            StrategicPlan plan = planner.SubmitIntent(StrategicObjectiveType.RangedReinforcement).Plan;
            Assert.That(plan, Is.Not.Null);
            return plan;
        }

        private StrategicIntent NewIntent() => new StrategicIntent(901, 0,
            StrategicObjectiveType.DefensiveTurtle, simulation.CurrentTick);

        private object Capture(StrategicPlan plan) => Capture(plan,
            planner.CapturePlanHealth(0, plan.StrategicPlanId));

        private object Capture(StrategicPlan plan, StrategicPlanHealthSnapshot health) => Call(
            "Capture", new[] { typeof(int), typeof(StrategicPlan), typeof(StrategicPlanHealthSnapshot) },
            0, plan, health);

        private object Build(object source, StrategicIntent intent, StrategicFeasibility quote = null) => Call(
            "Build", new[] { SourceType, typeof(StrategicIntent), typeof(StrategicFeasibility), typeof(string) },
            source, intent, quote, "A material objective change requires explicit player approval.");

        private bool Fresh(object proposal, object fresh) => (bool)Call("IsFresh",
            new[] { ProposalType, SourceType }, proposal, fresh);

        private string Json(object proposal) => InvokeJson(proposal);
        private string InvokeJson(object proposal) => (string)ProposalType.GetMethod("ToJson").Invoke(proposal, null);

        private Type BuilderType => typeof(StrategicPlan).Assembly.GetType(
            "OpenEmpires.StrategicAdaptationProposalBuilder");
        private Type SourceType => typeof(StrategicPlan).Assembly.GetType(
            "OpenEmpires.StrategicAdaptationSource");
        private Type ProposalType => typeof(StrategicPlan).Assembly.GetType(
            "OpenEmpires.StrategicAdaptationProposal");
        private object Call(string method, Type[] signature, params object[] args)
        {
            Assert.That(BuilderType, Is.Not.Null, "The detached adaptation builder must exist.");
            Assert.That(SourceType, Is.Not.Null, "The detached source value must exist.");
            Assert.That(ProposalType, Is.Not.Null, "The proposal value must exist.");
            MethodInfo member = BuilderType.GetMethod(method, BindingFlags.Public | BindingFlags.Static,
                null, signature, null);
            Assert.That(member, Is.Not.Null, method + " must be a pure static operation.");
            return member.Invoke(null, args);
        }

        private int Resource(ResourceType type)
        {
            var r = simulation.ResourceManager.GetPlayerResources(0);
            return type switch
            {
                ResourceType.Food => r.Food, ResourceType.Wood => r.Wood,
                ResourceType.Gold => r.Gold, ResourceType.Stone => r.Stone, _ => 0
            };
        }

        private static void SetWorkerTarget(StrategicPlan plan, int target) =>
            typeof(StrategicResourceAllocationGoalRequest).GetProperty("WorkerTarget")
                .SetValue(plan.Milestones[0].TacticalGoals[0], target);

        private static void SetRevision(StrategicPlan plan, int revision) =>
            typeof(StrategicPlan).GetField("<Revision>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(plan, revision);

        private static void SetStatus(StrategicPlan plan, StrategicPlanStatus status) =>
            typeof(StrategicPlan).GetProperty("Status").SetValue(plan, status);

        private static void SetGoalStatus(CommanderGoal goal, CommanderGoalStatus status) =>
            typeof(CommanderGoal).GetMethod("SetStatus", BindingFlags.Instance |
                BindingFlags.NonPublic).Invoke(goal, new object[] { status, "Controlled health fixture." });

        private static bool ContainsLiveReference(Type type)
        {
            if (typeof(StrategicPlan).IsAssignableFrom(type)
                || typeof(StrategicPlanHealthSnapshot).IsAssignableFrom(type)
                || typeof(StrategicIntent).IsAssignableFrom(type)
                || typeof(StrategicPlanner).IsAssignableFrom(type)
                || typeof(GameSimulation).IsAssignableFrom(type)
                || typeof(UnityEngine.Object).IsAssignableFrom(type)
                || typeof(Delegate).IsAssignableFrom(type)) return true;
            if (type.IsArray) return ContainsLiveReference(type.GetElementType());
            return type.IsGenericType && type.GetGenericArguments().Any(ContainsLiveReference);
        }
    }
}
