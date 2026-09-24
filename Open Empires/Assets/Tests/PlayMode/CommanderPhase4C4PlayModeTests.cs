using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4C4")]
    public sealed class CommanderPhase4C4PlayModeTests
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
            foreach (CommanderChatUI existing in UnityEngine.Object.FindObjectsByType<CommanderChatUI>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            SetAge(3);
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
            goals = new CommanderGoalManager(simulation, 0);
            planner = new StrategicPlanner(goals, Resource);
            pipeline = new StrategicPipeline(simulation, goals, planner);
            dispatcher = new CommanderIntentDispatcher(simulation, goals, strategicPlanner: planner);
            chat = new GameObject("Phase4C4RuntimeChat").AddComponent<CommanderChatUI>();
            chat.enabled = false;
            chat.Initialize(new MockAIProvider(), simulation, goals, dispatcher);
            chat.InitializeStrategic(new MockStrategicAIProvider(), pipeline);
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
        public IEnumerator RangedPreview_ApprovedPlanBuildsArcheryRangeAndTenArchers()
        {
            PrebuildSixHouses();
            Gatherers(ResourceType.Food, 8, x - 22);
            Gatherers(ResourceType.Wood, 8, x - 10);
            yield return ApproveAndRun("prepare ranged reinforcements", StrategicPlanType.RangedReinforcement,
                30000, plan =>
                {
                    Assert.That(Completed(BuildingType.ArcheryRange), Is.GreaterThanOrEqualTo(1));
                    Assert.That(Count(CommanderIntentCatalog.ArcherUnitType), Is.GreaterThanOrEqualTo(10));
                });
        }

        [UnityTest]
        public IEnumerator TurtlePreview_ApprovedPlanBuildsMandatoryTowersAndBothForces()
        {
            PrebuildSixHouses();
            Gatherers(ResourceType.Food, 8, x - 22);
            Gatherers(ResourceType.Wood, 8, x - 10);
            yield return ApproveAndRun("prepare fortified defenses", StrategicPlanType.DefensiveTurtle,
                40000, plan =>
                {
                    Assert.That(Completed(BuildingType.Barracks), Is.GreaterThanOrEqualTo(1));
                    Assert.That(Completed(BuildingType.ArcheryRange), Is.GreaterThanOrEqualTo(1));
                    Assert.That(Completed(BuildingType.Tower), Is.GreaterThanOrEqualTo(2));
                    Assert.That(Count(CommanderIntentCatalog.SpearmanUnitType), Is.GreaterThanOrEqualTo(8));
                    Assert.That(Count(CommanderIntentCatalog.ArcherUnitType), Is.GreaterThanOrEqualTo(8));
                });
        }

        [UnityTest]
        public IEnumerator FundedTowerFinishesBeforeFortifications_OnlyOneMoreTowerIsBuilt()
        {
            PrebuildSixHouses();
            Gatherers(ResourceType.Food, 8, x - 22);
            Gatherers(ResourceType.Wood, 8, x - 10);
            BuildingData funded = simulation.CreateBuilding(0, BuildingType.Tower,
                x - 20, z + 12, true);
            funded.ConstructionTicksRemaining = 1;
            var builder = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(x - 21, z + 12),
                Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
            builder.UnitType = 0;
            builder.IsVillager = true;
            builder.MaxHealth = builder.CurrentHealth = 100;
            builder.State = UnitState.Constructing;
            builder.ConstructionTargetBuildingId = funded.Id;

            yield return ApproveAndRun("prepare fortified defenses", StrategicPlanType.DefensiveTurtle,
                40000, plan =>
                {
                    Assert.That(Completed(BuildingType.Tower), Is.EqualTo(2),
                        "The paid foundation plus one quoted tower must be the complete fortification target.");
                    Assert.That(simulation.BuildingRegistry.GetAllBuildings()
                        .Count(value => value.PlayerId == 0 && !value.IsDestroyed
                            && value.Type == BuildingType.Tower), Is.EqualTo(2));
                }, plan =>
                {
                    Assert.That(funded.IsUnderConstruction, Is.False,
                        "The paid foundation must finish before the Fortifications goal is created.");
                    Assert.That(plan.RequiredResources.Single(value => value.ResourceType == ResourceType.Wood).Amount,
                        Is.EqualTo(1160), "The submitted plan quotes only one new Tower.");
                    Assert.That(plan.CurrentMilestone.RequiredResources
                        .Single(value => value.ResourceType == ResourceType.Wood).Amount,
                        Is.EqualTo(300), "Fortifications must reserve only the quoted new Tower.");
                    Assert.That(goals.Goals.OfType<BuildStructureGoal>()
                        .Single(value => value.StructureType == BuildingType.Tower).TargetTotal,
                        Is.EqualTo(2), "The goal target must include the completed paid foundation.");
                });
        }

        [UnityTest]
        public IEnumerator TwoFundedTowersFinishBeforeFortifications_NoThirdTowerGoalIsCreated()
        {
            PrebuildSixHouses();
            Gatherers(ResourceType.Food, 8, x - 22);
            Gatherers(ResourceType.Wood, 8, x - 10);
            for (int i = 0; i < 2; i++)
            {
                BuildingData funded = simulation.CreateBuilding(0, BuildingType.Tower,
                    x - 20 + i * 4, z + 12, true);
                funded.ConstructionTicksRemaining = 1;
                var builder = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(x - 21 + i * 4, z + 12),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                builder.UnitType = 0;
                builder.IsVillager = true;
                builder.MaxHealth = builder.CurrentHealth = 100;
                builder.State = UnitState.Constructing;
                builder.ConstructionTargetBuildingId = funded.Id;
            }

            yield return ApproveAndRun("prepare fortified defenses", StrategicPlanType.DefensiveTurtle,
                40000, plan =>
                {
                    Assert.That(plan.RequiredResources.Single(value => value.ResourceType == ResourceType.Wood).Amount,
                        Is.EqualTo(860), "Both Tower foundations were paid before plan approval.");
                    Assert.That(Completed(BuildingType.Tower), Is.EqualTo(2));
                    Assert.That(goals.Goals.OfType<BuildStructureGoal>()
                        .Count(value => value.StructureType == BuildingType.Tower), Is.Zero,
                        "A fulfilled absolute target must not submit a third Tower goal.");
                });
        }

        [UnityTest]
        public IEnumerator OneFundedWorker_RangedPreparationCanFinish()
        {
            PrebuildSixHouses();
            Gatherers(ResourceType.Wood, 1, x - 10);
            yield return ApproveAndRun("prepare ranged reinforcements", StrategicPlanType.RangedReinforcement,
                40000, plan =>
                {
                    var allocations = plan.Milestones[0].TacticalGoals
                        .OfType<StrategicResourceAllocationGoalRequest>().ToArray();
                    Assert.That(allocations.Sum(value => value.WorkerTarget), Is.EqualTo(1));
                    Assert.That(allocations.Count(value => value.WorkerTarget == 0), Is.EqualTo(1));
                    Assert.That(Completed(BuildingType.ArcheryRange), Is.GreaterThanOrEqualTo(1));
                    Assert.That(Count(CommanderIntentCatalog.ArcherUnitType), Is.GreaterThanOrEqualTo(10));
                });
        }

        [UnityTest]
        public IEnumerator RangedPreparation_RecoversAfterApprovedStockpilesAreSpent()
        {
            PrebuildSixHouses();
            Gatherers(ResourceType.Food, 8, x - 22);
            Gatherers(ResourceType.Wood, 8, x - 10);
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            yield return ApproveAndRun("prepare ranged reinforcements", StrategicPlanType.RangedReinforcement,
                40000, plan =>
                {
                    Assert.That(Completed(BuildingType.ArcheryRange), Is.GreaterThanOrEqualTo(1));
                    Assert.That(Count(CommanderIntentCatalog.ArcherUnitType), Is.GreaterThanOrEqualTo(10));
                }, afterApproval: plan =>
                {
                    resources.Food = 0;
                    resources.Wood = 0;
                });
        }

        [UnityTest]
        public IEnumerator AtCurrentCap_ApprovedRangedPlanBuildsHouseThenTrainsAndReleasesReservations()
        {
            Gatherers(ResourceType.Food, 5, x - 22);
            Gatherers(ResourceType.Wood, 5, x - 10);
            Assert.That(simulation.GetPopulation(0), Is.EqualTo(10));
            Assert.That(simulation.GetPopulationCap(0), Is.EqualTo(10));
            Assert.That(simulation.BuildingRegistry.GetAllBuildings()
                .Count(value => value.PlayerId == 0 && value.Type == BuildingType.House), Is.Zero);

            bool sawHouseFoundation = false;
            bool sawArcherTrainingAfterHouse = false;
            yield return ApproveAndRun("prepare ranged reinforcements",
                StrategicPlanType.RangedReinforcement, 40000, plan =>
                {
                    Assert.That(sawHouseFoundation, Is.True, "A House must be placed and constructed dynamically.");
                    Assert.That(sawArcherTrainingAfterHouse, Is.True, "Archer training must resume after housing.");
                    Assert.That(Completed(BuildingType.House), Is.GreaterThanOrEqualTo(1));
                    Assert.That(Completed(BuildingType.ArcheryRange), Is.GreaterThanOrEqualTo(1));
                    Assert.That(Count(CommanderIntentCatalog.ArcherUnitType), Is.GreaterThanOrEqualTo(10));
                }, observeTick: plan =>
                {
                    sawHouseFoundation |= simulation.BuildingRegistry.GetAllBuildings().Any(value =>
                        value.PlayerId == 0 && value.Type == BuildingType.House && value.IsUnderConstruction);
                    sawArcherTrainingAfterHouse |= Completed(BuildingType.House) > 0
                        && simulation.BuildingRegistry.GetAllBuildings().Any(value =>
                            value.PlayerId == 0 && value.TrainingQueue.Contains(CommanderIntentCatalog.ArcherUnitType));
                });
        }

        [UnityTest]
        public IEnumerator BelowAgeTurtle_IsRejectedBeforeTowersCanBeSkipped()
        {
            PrebuildSixHouses();
            Gatherers(ResourceType.Food, 8, x - 22);
            Gatherers(ResourceType.Wood, 8, x - 10);
            SetAge(1);
            var translation = chat.SubmitMessageAsync("prepare fortified defenses");
            while (!translation.IsCompleted) yield return null;
            Assert.That(chat.PendingStrategicIntent, Is.Not.Null);
            ApproveButton().onClick.Invoke();
            Assert.That(chat.LatestStrategicDecision.Submission, Is.Null);
            Assert.That(chat.LatestStrategicDecision.Outcome, Does.Contain("current age"));
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(Completed(BuildingType.Tower), Is.Zero);
        }

        [UnityTest]
        public IEnumerator RangedConfirmation_UsesPlayerCommandOnceAndCannotReplaceDirectPlan()
        {
            PrebuildSixHouses();
            Gatherers(ResourceType.Food, 8, x - 22);
            Gatherers(ResourceType.Wood, 8, x - 10);
            yield return ConfirmationAndDirectProtection("prepare ranged reinforcements",
                StrategicPlanType.RangedReinforcement);
        }

        [UnityTest]
        public IEnumerator TurtleConfirmation_UsesPlayerCommandOnceAndCannotReplaceDirectPlan()
        {
            PrebuildSixHouses();
            Gatherers(ResourceType.Food, 8, x - 22);
            Gatherers(ResourceType.Wood, 8, x - 10);
            yield return ConfirmationAndDirectProtection("prepare fortified defenses",
                StrategicPlanType.DefensiveTurtle);
        }

        private IEnumerator ConfirmationAndDirectProtection(string phrase, StrategicPlanType expectedType)
        {
            var translation = chat.SubmitMessageAsync(phrase);
            while (!translation.IsCompleted) yield return null;
            Assert.That(chat.PendingStrategicIntent, Is.Not.Null);
            chat.GetComponentsInChildren<Button>(true)
                .Single(value => value.name == "Confirm as command").onClick.Invoke();
            var confirmed = chat.LatestStrategicDecision.Submission;
            Assert.That(confirmed?.CreatedPlan, Is.True, chat.LatestStrategicDecision.Outcome);
            Assert.That(confirmed.Plan.PlanType, Is.EqualTo(expectedType));
            Assert.That(confirmed.Plan.Authority, Is.EqualTo(StrategicPlanAuthority.PlayerOverride));
            Assert.That(confirmed.Plan.Source, Is.EqualTo(StrategicIntentSource.AIConfirmedPlayerCommand));
            int count = planner.Plans.Count;
            Assert.That(chat.ConfirmStrategicCommand(), Is.Null, "Exact confirmation cannot be replayed.");
            Assert.That(planner.Plans.Count, Is.EqualTo(count));

            var direct = planner.CreateIntent(StrategicObjectiveType.DefensivePreparation);
            var directApproval = new StrategicApprovalLayer().Evaluate(pipeline.CaptureContext(),
                direct, direct.Source);
            var directDecision = pipeline.EvaluateApprovedIntentNow(directApproval);
            Assert.That(directDecision.Submission?.CreatedPlan, Is.True, directDecision.Outcome);
            Assert.That(directDecision.Submission.Plan.Source, Is.EqualTo(StrategicIntentSource.PlayerDirect));
            Assert.That(confirmed.Plan.Status, Is.EqualTo(StrategicPlanStatus.Cancelled));
            translation = chat.SubmitMessageAsync(phrase);
            while (!translation.IsCompleted) yield return null;
            var rejected = chat.ConfirmStrategicCommand();
            Assert.That(rejected.Submission, Is.Null);
            Assert.That(planner.ActivePlans.Single().Source, Is.EqualTo(StrategicIntentSource.PlayerDirect));
        }

        private IEnumerator ApproveAndRun(string phrase, StrategicPlanType expectedType,
            int tickBound, Action<StrategicPlan> verify,
            Action<StrategicPlan> verifyFortifications = null,
            Action<StrategicPlan> afterApproval = null,
            Action<StrategicPlan> observeTick = null)
        {
            int initialTowers = Completed(BuildingType.Tower);
            int initialArchers = Count(CommanderIntentCatalog.ArcherUnitType);
            var translation = chat.SubmitMessageAsync(phrase);
            while (!translation.IsCompleted) yield return null;
            Assert.That(translation.IsFaulted, Is.False,
                translation.Exception == null ? string.Empty : translation.Exception.ToString());
            Assert.That(chat.PendingStrategicIntent, Is.Not.Null);
            Assert.That(chat.DisplayedTranscript, Does.Contain(expectedType == StrategicPlanType.DefensiveTurtle
                ? "2 mandatory towers" : "10 archers"));
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(goals.Goals, Is.Empty);
            Assert.That(planner.Reservations, Is.Empty);
            Assert.That(pipeline.DecisionHistory.History, Is.Empty);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
            ApproveButton().onClick.Invoke();
            var submission = chat.LatestStrategicDecision.Submission;
            Assert.That(submission?.CreatedPlan, Is.True, chat.LatestStrategicDecision.Outcome);
            Assert.That(submission.Plan.PlanType, Is.EqualTo(expectedType));
            Assert.That(submission.Plan.Authority, Is.EqualTo(StrategicPlanAuthority.Normal));
            Assert.That(submission.Intent.Source, Is.EqualTo(StrategicIntentSource.AIRecommendation));
            Assert.That(initialArchers, Is.LessThan(expectedType == StrategicPlanType.DefensiveTurtle ? 8 : 10));
            if (expectedType == StrategicPlanType.DefensiveTurtle)
                Assert.That(initialTowers, Is.LessThan(2));
            afterApproval?.Invoke(submission.Plan);
            int ticks = 0;
            bool checkedFortifications = false;
            for (; ticks < tickBound && !submission.Plan.IsTerminal; ticks++)
            {
                planner.Tick(simulation.CurrentTick);
                goals.Tick(simulation.CurrentTick);
                simulation.Tick();
                observeTick?.Invoke(submission.Plan);
                if (!checkedFortifications && verifyFortifications != null
                    && submission.Plan.CurrentMilestone?.Name == "Fortifications")
                {
                    verifyFortifications(submission.Plan);
                    checkedFortifications = true;
                }
                if (ticks % 300 == 0) yield return null;
            }
            Assert.That(submission.Plan.Status, Is.EqualTo(StrategicPlanStatus.Completed),
                $"Timed out or failed at {ticks}/{tickBound} ticks: {submission.Plan.OutcomeMessage} | "
                + string.Join("; ", goals.ActiveGoals.Select(value => value.StatusReason)));
            verify(submission.Plan);
            if (verifyFortifications != null)
                Assert.That(checkedFortifications, Is.True, "Fortifications must be observed at runtime.");
            Assert.That(planner.Reservations, Is.Empty);
            Debug.Log($"[Phase4C4 Runtime] {expectedType} completed at {ticks}/{tickBound} ticks via UI Approve; reservations released.");
        }

        private Button ApproveButton() => chat.GetComponentsInChildren<Button>(true)
            .Single(value => value.name == "Approve strategy");
        private void PrebuildSixHouses()
        {
            for (int i = 0; i < 6; i++)
                simulation.CreateBuilding(0, BuildingType.House, x + 19 + i * 3, z + 12, false);
        }
        private int Count(int type) => simulation.UnitRegistry.GetAllUnits()
            .Count(value => value.PlayerId == 0 && value.CurrentHealth > 0 && value.UnitType == type);
        private int Completed(BuildingType type) => simulation.BuildingRegistry.GetAllBuildings()
            .Count(value => value.PlayerId == 0 && !value.IsDestroyed
                && !value.IsUnderConstruction && value.Type == type);
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
        private void SetAge(int age) => ((int[])typeof(GameSimulation)
            .GetField("playerAges", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(simulation))[0] = age;
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
    }
}
