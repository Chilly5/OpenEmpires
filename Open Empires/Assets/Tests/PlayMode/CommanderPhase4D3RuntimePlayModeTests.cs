using System;
using System.Collections;
using System.Collections.Generic;
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
    [Category("CommanderPhase4D3Runtime")]
    public sealed class CommanderPhase4D3RuntimePlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private StrategicPlanner planner;
        private StrategicPipeline pipeline;
        private CommanderIntentDispatcher dispatcher;
        private CommanderChatUI chat;
        private CountingTacticalProvider tacticalProvider;
        private CountingStrategicProvider strategicProvider;
        private readonly List<ICommand> commands = new List<ICommand>();
        private int x, z;

        [SetUp]
        public void SetUp()
        {
            foreach (CommanderChatUI existing in UnityEngine.Object.FindObjectsByType<CommanderChatUI>())
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            ((int[])typeof(GameSimulation).GetField("playerAges",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(simulation))[0] = 3;
            typeof(MapData).GetField("holeMap", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(simulation.MapData, null);
            foreach (ResourceNodeData node in simulation.MapData.GetAllResourceNodes())
                node.RemainingAmount = 0;
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
            // Sixteen existing workers and one House leave four places for the ten-archer
            // approved target. The population test must build the next House itself.
            simulation.CreateBuilding(0, BuildingType.House, x + 19, z + 12, false);
            Gatherers(ResourceType.Food, 8, x - 22);
            Gatherers(ResourceType.Wood, 8, x - 10);
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 5000;
            goals = new CommanderGoalManager(simulation, 0);
            planner = new StrategicPlanner(goals, Resource);
            pipeline = new StrategicPipeline(simulation, goals, planner);
            dispatcher = new CommanderIntentDispatcher(simulation, goals, strategicPlanner: planner);
            tacticalProvider = new CountingTacticalProvider();
            strategicProvider = new CountingStrategicProvider();
            chat = new GameObject("Phase4D3RuntimeChat").AddComponent<CommanderChatUI>();
            chat.enabled = false;
            chat.Initialize(tacticalProvider, simulation, goals, dispatcher);
            chat.InitializeStrategic(strategicProvider, pipeline);
            chat.enabled = true;
            simulation.CommandBuffer.CommandEnqueued += (command, source) =>
            {
                Assert.That(source, Is.EqualTo(CommandEnqueueSource.Commander));
                Assert.That(command.PlayerId, Is.EqualTo(0));
                commands.Add(command);
            };
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
            commands.Clear();
        }

        [UnityTest]
        public IEnumerator TemporaryResourceBlocker_RecoversWithoutNewApproval()
        {
            for (int i = 1; i < 6; i++)
                simulation.CreateBuilding(0, BuildingType.House, x + 19 + i * 3, z + 12, false);
            StrategicPlan plan = null;
            yield return ApproveRanged(value => plan = value);
            int planId = plan.StrategicPlanId, createdTick = plan.CreatedTick;
            int approvedRevision = plan.Revision;
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = 0;

            StrategicPlanHealthSnapshot waiting = null;
            for (int tick = 0; tick < 3000 && !plan.IsTerminal; tick++)
            {
                Advance();
                StrategicPlanHealthSnapshot health = Health(plan, planId, createdTick);
                if (HasHealth(health, StrategicPlanHealthCategory.WaitingForResources))
                {
                    waiting = health;
                    break;
                }
                if (tick % 300 == 0) yield return null;
            }
            Assert.That(waiting, Is.Not.Null, "The approved plan must encounter a real typed resource wait.");
            StrategicPlanHealthResource deficit = waiting.Resources.Single(value =>
                value.ResourceType == ResourceType.Wood);
            Assert.That(deficit.MilestoneRequirementKnown, Is.True);
            Assert.That(deficit.MilestoneDeficit, Is.GreaterThan(0));
            Assert.That(waiting.MilestoneId, Is.EqualTo(plan.CurrentMilestone.MilestoneId));
            int completedAtWait = waiting.CompletedMilestones;

            int progressedTick = -1;
            for (int tick = 0; tick < 40000 && !plan.IsTerminal; tick++)
            {
                Advance();
                StrategicPlanHealthSnapshot health = Health(plan, planId, createdTick);
                if (progressedTick < 0 && health.CompletedMilestones > completedAtWait)
                    progressedTick = health.ObservedTick;
                if (tick % 300 == 0) yield return null;
            }
            Assert.That(progressedTick, Is.GreaterThan(waiting.ObservedTick),
                "Natural gathering/retry must advance a later milestone on the same plan.");
            AssertCompletedApprovedPlan(plan, planId, createdTick, approvedRevision);
            Assert.That(commands.Count(command => command is PlaceBuildingCommand place
                && place.BuildingType == BuildingType.ArcheryRange), Is.EqualTo(1));
            Assert.That(commands.Count(command => command is PlaceBuildingCommand place
                && place.BuildingType == BuildingType.House), Is.Zero);
            Assert.That(commands.Count, Is.EqualTo(11),
                "One Range and ten Archers are the only approved target commands.");
        }

        [UnityTest]
        public IEnumerator PopulationPrerequisite_RecoversWithoutNewApproval()
        {
            Assert.That(simulation.GetPopulation(0), Is.EqualTo(16));
            Assert.That(simulation.GetPopulationCap(0), Is.EqualTo(20));
            StrategicPlan plan = null;
            yield return ApproveRanged(value => plan = value);
            int planId = plan.StrategicPlanId, createdTick = plan.CreatedTick;
            int approvedRevision = plan.Revision;

            StrategicPlanHealthSnapshot waiting = null;
            int capBeforeHouse = simulation.GetPopulationCap(0);
            for (int tick = 0; tick < 15000 && !plan.IsTerminal; tick++)
            {
                Advance();
                StrategicPlanHealthSnapshot health = Health(plan, planId, createdTick);
                if (HasHealth(health, StrategicPlanHealthCategory.WaitingForPopulation))
                {
                    waiting = health;
                    break;
                }
                if (tick % 300 == 0) yield return null;
            }
            Assert.That(waiting, Is.Not.Null,
                "The force milestone must encounter actual population and queue pressure.");
            Assert.That(waiting.Population + waiting.AllQueuedUnits,
                Is.GreaterThanOrEqualTo(waiting.PopulationCap));
            Assert.That(waiting.PopulationCap, Is.EqualTo(capBeforeHouse));
            Assert.That(waiting.MaximumPopulation, Is.GreaterThan(waiting.PopulationCap),
                "This is a recoverable current cap, not the hard maximum.");
            Assert.That(waiting.Children.Single(child => child.TargetUnits.HasValue).RemainingOrders,
                Is.GreaterThan(0));
            int completedAtWait = waiting.CompletedMilestones;

            int houseCompleteTick = -1, progressedTick = -1;
            for (int tick = 0; tick < 40000 && !plan.IsTerminal; tick++)
            {
                Advance();
                StrategicPlanHealthSnapshot health = Health(plan, planId, createdTick);
                if (houseCompleteTick < 0 && simulation.GetPopulationCap(0) > capBeforeHouse)
                    houseCompleteTick = simulation.CurrentTick;
                if (progressedTick < 0 && health.CompletedMilestones > completedAtWait)
                    progressedTick = health.ObservedTick;
                if (tick % 300 == 0) yield return null;
            }
            Assert.That(houseCompleteTick, Is.GreaterThan(waiting.ObservedTick),
                "The ordinary House prerequisite must actually increase capacity.");
            Assert.That(progressedTick, Is.GreaterThan(houseCompleteTick),
                "The same force plan must advance after the House finishes.");
            AssertCompletedApprovedPlan(plan, planId, createdTick, approvedRevision);
            Assert.That(commands.Count(command => command is PlaceBuildingCommand place
                && place.BuildingType == BuildingType.House), Is.EqualTo(1));
            Assert.That(commands.Count, Is.EqualTo(12),
                "A single capacity House is the only extra prerequisite command.");
            Assert.That(simulation.BuildingRegistry.GetAllBuildings().Count(building =>
                building.PlayerId == 0 && building.Type == BuildingType.House
                && !building.IsDestroyed && !building.IsUnderConstruction), Is.EqualTo(2));
        }

        private IEnumerator ApproveRanged(Action<StrategicPlan> accept)
        {
            Task<CommanderAIChatSubmission> translation =
                chat.SubmitMessageAsync("prepare ranged reinforcements");
            while (!translation.IsCompleted) yield return null;
            Assert.That(translation.IsFaulted, Is.False,
                translation.Exception?.ToString() ?? string.Empty);
            Assert.That(chat.PendingStrategicIntent, Is.Not.Null);
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(goals.Goals, Is.Empty);
            Assert.That(planner.Reservations, Is.Empty);
            Assert.That(commands, Is.Empty);
            Assert.That(strategicProvider.Calls, Is.EqualTo(1));
            chat.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Approve strategy").onClick.Invoke();
            StrategicIntentSubmission submission = chat.LatestStrategicDecision?.Submission;
            Assert.That(submission?.CreatedPlan, Is.True, chat.LatestStrategicDecision?.Outcome);
            Assert.That(submission.Intent.Source, Is.EqualTo(StrategicIntentSource.AIRecommendation));
            StrategicPlan plan = submission.Plan;
            Assert.That(plan.PlanType, Is.EqualTo(StrategicPlanType.RangedReinforcement));
            Assert.That(plan.OwnerPlayerId, Is.Zero);
            Assert.That(plan.Milestones.Select(item => item.MilestoneId),
                Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(plan.Milestones[1].TacticalGoals.OfType<StrategicBuildStructureGoalRequest>()
                .Single().Count, Is.EqualTo(1));
            Assert.That(plan.Milestones[2].TacticalGoals.OfType<StrategicEnsureUnitCountGoalRequest>()
                .Single().TargetTotal, Is.EqualTo(10));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(1));
            accept(plan);
        }

        private void AssertCompletedApprovedPlan(StrategicPlan plan, int id, int tick, int revision)
        {
            StrategicPlanHealthSnapshot terminal = Health(plan, id, tick);
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Completed), plan.OutcomeMessage);
            Assert.That(terminal.PrimaryHealthCategory,
                Is.EqualTo(StrategicPlanHealthCategory.Completed));
            Assert.That(terminal.CompletedMilestones, Is.EqualTo(4));
            Assert.That(plan.Revision, Is.GreaterThan(revision));
            Assert.That(plan.Milestones[1].TacticalGoals.OfType<StrategicBuildStructureGoalRequest>()
                .Single().Count, Is.EqualTo(1));
            Assert.That(plan.Milestones[2].TacticalGoals.OfType<StrategicEnsureUnitCountGoalRequest>()
                .Single().TargetTotal, Is.EqualTo(10),
                "Recovery cannot silently lower the approved Archer target.");
            Assert.That(simulation.BuildingRegistry.GetAllBuildings().Count(building =>
                building.PlayerId == 0 && building.Type == BuildingType.ArcheryRange
                && !building.IsDestroyed && !building.IsUnderConstruction), Is.EqualTo(1));
            Assert.That(simulation.UnitRegistry.GetAllUnits().Count(unit => unit.PlayerId == 0
                && unit.CurrentHealth > 0
                && unit.UnitType == simulation.ResolveCivUnitType(0,
                    CommanderIntentCatalog.ArcherUnitType)), Is.GreaterThanOrEqualTo(10));
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(1));
            Assert.That(strategicProvider.Calls, Is.EqualTo(1));
            Assert.That(tacticalProvider.Calls, Is.Zero);
            Assert.That(goals.Goals.Count, Is.EqualTo(4),
                "Recovery must not create a replacement strategic goal tree.");
            Assert.That(goals.Goals.OfType<ResourceAllocationGoal>().Single(goal =>
                goal.Resource == ResourceType.Food).TargetWorkers,
                Is.EqualTo(RangedReinforcementPlan.FoodWorkers));
            Assert.That(goals.Goals.OfType<ResourceAllocationGoal>().Single(goal =>
                goal.Resource == ResourceType.Wood).TargetWorkers,
                Is.EqualTo(RangedReinforcementPlan.WoodWorkers));
            Assert.That(goals.Goals.OfType<EnsureUnitCountGoal>().Single().TargetTotal,
                Is.EqualTo(10));
            Assert.That(goals.Goals.Select(goal => goal.GoalId),
                Is.EquivalentTo(plan.ChildGoalIds));
            Assert.That(goals.ActiveGoals, Is.Empty);
            Assert.That(planner.GetReservationsForPlan(id).Where(item =>
                item.Status == StrategicResourceReservationStatus.Active), Is.Empty);
            Assert.That(planner.Reservations.Where(item =>
                item.Status == StrategicResourceReservationStatus.Active), Is.Empty);
            Assert.That(commands.Count(command => command is TrainUnitCommand train
                && train.UnitType == CommanderIntentCatalog.ArcherUnitType), Is.EqualTo(10));
            Assert.That(commands.All(command => command is GatherCommand
                || command is PlaceBuildingCommand || command is ConstructBuildingCommand
                || command is TrainUnitCommand), Is.True);
        }

        private StrategicPlanHealthSnapshot Health(StrategicPlan plan, int id, int tick)
        {
            StrategicPlanHealthSnapshot health = planner.CapturePlanHealth(0, id);
            Assert.That(health, Is.Not.Null);
            Assert.That(health.PlayerId, Is.Zero);
            Assert.That(health.PlanId, Is.EqualTo(id));
            Assert.That(health.CreatedTick, Is.EqualTo(tick));
            Assert.That(health.Revision, Is.EqualTo(plan.Revision));
            Assert.That(planner.GetPlan(id), Is.SameAs(plan));
            return health;
        }

        private static bool HasHealth(StrategicPlanHealthSnapshot health,
            StrategicPlanHealthCategory category) => health.PrimaryHealthCategory == category
            || health.SecondaryHealthCategories.Contains(category);

        private void Advance()
        {
            planner.Tick(simulation.CurrentTick);
            goals.Tick(simulation.CurrentTick);
            simulation.Tick();
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
                UnitData worker = simulation.UnitRegistry.CreateUnit(0,
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

        private sealed class CountingTacticalProvider : ICommanderAIProvider
        {
            public int Calls { get; private set; }
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request,
                CancellationToken token)
            {
                Calls++;
                return new MockAIProvider().TranslateAsync(request, token);
            }
        }

        private sealed class CountingStrategicProvider : IStrategicAIInterpreter
        {
            public int Calls { get; private set; }
            public Task<StrategicAIProviderResult> InterpretStrategicIntentAsync(
                StrategicAIRequest request, CancellationToken token)
            {
                Calls++;
                return new MockStrategicAIProvider()
                    .InterpretStrategicIntentAsync(request, token);
            }
        }
    }
}
