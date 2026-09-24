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
    [Category("CommanderPhase4D2Runtime")]
    public sealed class CommanderPhase4D2RuntimePlayModeTests
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
            for (int i = 0; i < 6; i++)
                simulation.CreateBuilding(0, BuildingType.House, x + 19 + i * 3, z + 12, false);
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
            chat = new GameObject("Phase4D2RuntimeChat").AddComponent<CommanderChatUI>();
            chat.enabled = false;
            chat.Initialize(tacticalProvider, simulation, goals, dispatcher);
            chat.InitializeStrategic(strategicProvider, pipeline);
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
        public IEnumerator ApprovedRangedResourceWait_RecoversSamePlanAndCompletesWithoutReapproval()
        {
            var commanderCommands = new List<CommandObservation>();
            simulation.CommandBuffer.CommandEnqueued += (command, source) =>
            {
                if (source == CommandEnqueueSource.Commander)
                    commanderCommands.Add(new CommandObservation(command,
                        simulation.GetPopulation(0), simulation.GetPopulationCap(0),
                        OwnedQueuedUnits()));
            };
            Task<CommanderAIChatSubmission> translation =
                chat.SubmitMessageAsync("prepare ranged reinforcements");
            while (!translation.IsCompleted) yield return null;
            Assert.That(translation.IsFaulted, Is.False,
                translation.Exception?.ToString() ?? string.Empty);
            Assert.That(chat.PendingStrategicIntent, Is.Not.Null);
            Assert.That(chat.DisplayedTranscript, Does.Contain("10 archers"));
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(goals.Goals, Is.Empty);
            Assert.That(planner.Reservations, Is.Empty);
            Assert.That(commanderCommands, Is.Empty);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
            Assert.That(strategicProvider.Calls, Is.EqualTo(1));
            Assert.That(tacticalProvider.Calls, Is.Zero);

            chat.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Approve strategy").onClick.Invoke();
            StrategicIntentSubmission submission = chat.LatestStrategicDecision?.Submission;
            Assert.That(submission?.CreatedPlan, Is.True, chat.LatestStrategicDecision?.Outcome);
            StrategicPlan plan = submission.Plan;
            Assert.That(plan.PlanType, Is.EqualTo(StrategicPlanType.RangedReinforcement));
            Assert.That(plan.Authority, Is.EqualTo(StrategicPlanAuthority.Normal));
            Assert.That(submission.Intent.Source, Is.EqualTo(StrategicIntentSource.AIRecommendation));
            int planId = plan.StrategicPlanId;
            int createdTick = plan.CreatedTick;
            int approvalRevision = plan.Revision;
            int decisionCount = pipeline.DecisionHistory.History.Count;
            int providerCalls = strategicProvider.Calls;
            Assert.That(planner.Plans.Count, Is.EqualTo(1));
            Assert.That(decisionCount, Is.EqualTo(1));
            Assert.That(planId, Is.GreaterThan(0));
            Assert.That(approvalRevision, Is.GreaterThan(0));

            // The approved quote was affordable. Spending its stockpile afterward is a
            // legitimate changing-world wait; the original plan and targets are untouched.
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = 0;
            resources.Wood = 0;
            StrategicPlanHealthSnapshot waiting = null;
            int waitTicks = 0;
            for (; waitTicks < 3000 && !plan.IsTerminal; waitTicks++)
            {
                Advance();
                StrategicPlanHealthSnapshot observed = planner.CapturePlanHealth(0, planId);
                AssertIdentity(plan, observed, planId, createdTick);
                if (observed.PrimaryHealthCategory == StrategicPlanHealthCategory.WaitingForResources
                    || observed.SecondaryHealthCategories.Contains(
                        StrategicPlanHealthCategory.WaitingForResources))
                {
                    waiting = observed;
                    break;
                }
                if (waitTicks % 300 == 0) yield return null;
            }
            Assert.That(waiting, Is.Not.Null,
                "Approved stockpile depletion must produce a sampled typed resource wait.");
            Assert.That(waiting.PlanStatus, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(waiting.MilestoneId, Is.EqualTo(plan.CurrentMilestone.MilestoneId));
            Assert.That(waiting.MilestoneStatus, Is.EqualTo(plan.CurrentMilestone.Status));
            Assert.That(waiting.Children.Select(child => child.GoalId),
                Is.EquivalentTo(plan.CurrentMilestone.RequiredChildGoals));
            StrategicPlanHealthResource waitingWood = waiting.Resources.Single(item =>
                item.ResourceType == ResourceType.Wood);
            Assert.That(waitingWood.MilestoneRequirementKnown, Is.True);
            Assert.That(waitingWood.MilestoneRequirement, Is.GreaterThan(0));
            Assert.That(waitingWood.MilestoneDeficit, Is.GreaterThan(0));
            Assert.That(waitingWood.Owned, Is.EqualTo(Resource(ResourceType.Wood)));
            Assert.That(waiting.Population, Is.EqualTo(simulation.GetPopulation(0)));
            Assert.That(waiting.PopulationCap, Is.EqualTo(simulation.GetPopulationCap(0)));
            Assert.That(waiting.AllQueuedUnits, Is.EqualTo(OwnedQueuedUnits()));
            Assert.That(waiting.PrimaryHealthCategory, Is.Not.EqualTo(StrategicPlanHealthCategory.Failed));
            Debug.Log($"[Phase4D2 Runtime WAIT] plan={planId}/{createdTick} rev={waiting.Revision} tick={waiting.ObservedTick} milestone={waiting.MilestoneId}/{waiting.MilestoneStatus} children={waiting.CompletedChildGoals}/{waiting.RequiredChildGoals} health={waiting.PrimaryHealthCategory} wood={waitingWood.Owned}/{waitingWood.MilestoneRequirement} deficit={waitingWood.MilestoneDeficit} planReserved={waitingWood.PlanActiveReservation} queue={waiting.AllQueuedUnits} population={waiting.Population}/{waiting.PopulationCap}");

            yield return null; // Let the enabled host render the unchanged simulation state.
            Assert.That(CurrentStatus(), Does.Contain("Health:"));
            Assert.That(CurrentStatus(), Does.Contain("Waiting for resources"));
            Task<CommanderAIChatSubmission> question = chat.SubmitMessageAsync("why is the plan waiting?");
            while (!question.IsCompleted) yield return null;
            Assert.That(question.IsFaulted, Is.False, question.Exception?.ToString() ?? string.Empty);
            Assert.That(chat.DisplayedTranscript, Does.Contain("Waiting for resources"));
            Assert.That(strategicProvider.Calls, Is.EqualTo(providerCalls));
            Assert.That(tacticalProvider.Calls, Is.Zero);
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(decisionCount));

            int completedAtWait = waiting.CompletedMilestones;
            int progressTick = -1;
            int progressRevision = -1;
            bool observedLinkedChild = false;
            int ticks = 0;
            for (; ticks < 40000 && !plan.IsTerminal; ticks++)
            {
                Advance();
                StrategicPlanHealthSnapshot observed = planner.CapturePlanHealth(0, planId);
                AssertIdentity(plan, observed, planId, createdTick);
                if (observed.Children.Count > 0)
                {
                    Assert.That(observed.Children.Select(child => child.GoalId),
                        Is.EquivalentTo(plan.CurrentMilestone.RequiredChildGoals));
                    Assert.That(observed.Children.All(child => child.Retained), Is.True);
                    if (!observedLinkedChild)
                        Debug.Log($"[Phase4D2 Runtime CHILD] plan={planId}/{createdTick} tick={observed.ObservedTick} milestone={observed.MilestoneId} child={observed.Children[0].GoalId}/{observed.Children[0].FineStatus} linked={observed.Children.Count}");
                    observedLinkedChild = true;
                }
                if (progressTick < 0 && observed.CompletedMilestones > completedAtWait)
                {
                    progressTick = observed.ObservedTick;
                    progressRevision = observed.Revision;
                    Debug.Log($"[Phase4D2 Runtime PROGRESS] plan={planId}/{createdTick} rev={progressRevision} tick={progressTick} milestones={completedAtWait}->{observed.CompletedMilestones} health={observed.PrimaryHealthCategory}");
                }
                if (ticks % 300 == 0) yield return null;
            }
            Assert.That(progressTick, Is.GreaterThan(waiting.ObservedTick),
                "Recovery must yield observed same-plan milestone progress after the sampled wait.");
            Assert.That(observedLinkedChild, Is.True,
                "Recovery must expose a live plan-linked tactical child, not only a terminal count.");
            Assert.That(plan.Status, Is.EqualTo(StrategicPlanStatus.Completed),
                $"After {ticks}/40000 ticks: {plan.OutcomeMessage} | "
                + string.Join("; ", goals.ActiveGoals.Select(goal => goal.StatusReason)));
            StrategicPlanHealthSnapshot terminal = planner.CapturePlanHealth(0, planId);
            AssertIdentity(plan, terminal, planId, createdTick);
            Assert.That(terminal.PrimaryHealthCategory, Is.EqualTo(StrategicPlanHealthCategory.Completed));
            Assert.That(terminal.CompletedMilestones, Is.EqualTo(terminal.TotalMilestones));
            Assert.That(plan.Revision, Is.GreaterThan(approvalRevision));
            Assert.That(Completed(BuildingType.ArcheryRange), Is.GreaterThanOrEqualTo(1));
            Assert.That(Count(CommanderIntentCatalog.ArcherUnitType),
                Is.GreaterThanOrEqualTo(RangedReinforcementPlan.ArcherTarget));
            Assert.That(planner.Plans.Count, Is.EqualTo(1), "Recovery cannot submit a replacement plan.");
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(decisionCount),
                "Recovery cannot make an unapproved strategic decision.");
            Assert.That(strategicProvider.Calls, Is.EqualTo(providerCalls),
                "Recovery cannot call the provider again.");
            Assert.That(tacticalProvider.Calls, Is.Zero);
            Assert.That(commanderCommands, Is.Not.Empty,
                "The approved plan must execute real Commander commands.");
            foreach (CommandObservation observation in commanderCommands)
                AssertApprovedRangedCommand(observation);
            AssertApprovedRangedGoals(plan);
            Assert.That(goals.ActiveGoals, Is.Empty);
            Assert.That(planner.GetReservationsForPlan(planId)
                .Where(item => item.Status == StrategicResourceReservationStatus.Active), Is.Empty,
                "Completion must release every plan-owned strategic reservation.");
            Assert.That(planner.Reservations
                .Where(item => item.Status == StrategicResourceReservationStatus.Active), Is.Empty);
            Debug.Log($"[Phase4D2 Runtime COMPLETE] plan={planId}/{createdTick} rev={terminal.Revision} tick={terminal.ObservedTick} waitTicks={waitTicks} recoveryTicks={ticks} progressTick={progressTick} archers={Count(CommanderIntentCatalog.ArcherUnitType)} commanderCommands={commanderCommands.Count} goals={goals.Goals.Count} reservations=0 providerCalls={providerCalls}");
        }

        private void AssertApprovedRangedGoals(StrategicPlan plan)
        {
            // Exact typed template contract, independent of the mutable plan child-ID list.
            Assert.That(goals.Goals.Count, Is.EqualTo(4));
            Assert.That(goals.Goals.All(goal => goal.PlayerId == 0 && goal.GoalId > 0), Is.True);
            Assert.That(goals.Goals.Select(goal => goal.GoalId).Distinct().Count(), Is.EqualTo(4));
            ResourceAllocationGoal[] allocations = goals.Goals.OfType<ResourceAllocationGoal>().ToArray();
            Assert.That(allocations.Length, Is.EqualTo(2));
            Assert.That(allocations.Single(goal => goal.Resource == ResourceType.Food).TargetWorkers,
                Is.EqualTo(RangedReinforcementPlan.FoodWorkers));
            Assert.That(allocations.Single(goal => goal.Resource == ResourceType.Wood).TargetWorkers,
                Is.EqualTo(RangedReinforcementPlan.WoodWorkers));
            BuildStructureGoal range = goals.Goals.OfType<BuildStructureGoal>().Single();
            Assert.That(range.StructureType, Is.EqualTo(BuildingType.ArcheryRange));
            Assert.That(range.TargetTotal, Is.EqualTo(1));
            EnsureUnitCountGoal archers = goals.Goals.OfType<EnsureUnitCountGoal>().Single();
            Assert.That(archers.RequestedUnitType, Is.EqualTo(CommanderIntentCatalog.ArcherUnitType));
            Assert.That(archers.TargetTotal, Is.EqualTo(RangedReinforcementPlan.ArcherTarget));
            Assert.That(goals.Goals.Select(goal => goal.GoalId), Is.EquivalentTo(plan.ChildGoalIds));
        }

        private void AssertApprovedRangedCommand(CommandObservation observation)
        {
            ICommand command = observation.Command;
            Assert.That(command.PlayerId, Is.EqualTo(0), "Commander command must be owner-scoped.");
            switch (command)
            {
                case GatherCommand gather:
                    Assert.That(command.Type, Is.EqualTo(CommandType.Gather));
                    AssertOwnedVillagers(gather.UnitIds);
                    ResourceNodeData node = simulation.MapData.GetResourceNode(gather.ResourceNodeId);
                    Assert.That(node, Is.Not.Null);
                    Assert.That(node.Type == ResourceType.Food || node.Type == ResourceType.Wood,
                        Is.True);
                    Assert.That(gather.IsQueued, Is.False);
                    break;
                case PlaceBuildingCommand place:
                    Assert.That(command.Type, Is.EqualTo(CommandType.PlaceBuilding));
                    Assert.That(place.BuildingType == BuildingType.ArcheryRange
                        || place.BuildingType == BuildingType.House, Is.True);
                    if (place.BuildingType == BuildingType.House)
                        Assert.That(observation.Population + observation.Queued,
                            Is.GreaterThanOrEqualTo(observation.PopulationCap),
                            "A dynamic House is authorized only for a live capacity prerequisite.");
                    AssertOwnedVillagers(place.VillagerUnitIds);
                    Assert.That(place.TileX, Is.InRange(0, simulation.MapData.Width - 1));
                    Assert.That(place.TileZ, Is.InRange(0, simulation.MapData.Height - 1));
                    Assert.That(place.LandmarkIdValue, Is.EqualTo(-1));
                    Assert.That(place.IsQueued, Is.False);
                    break;
                case ConstructBuildingCommand construct:
                    Assert.That(command.Type, Is.EqualTo(CommandType.ConstructBuilding));
                    AssertOwnedVillagers(construct.UnitIds);
                    BuildingData recovery = simulation.BuildingRegistry.GetBuilding(construct.TargetBuildingId);
                    Assert.That(recovery, Is.Not.Null);
                    Assert.That(recovery.PlayerId, Is.EqualTo(0));
                    Assert.That(recovery.Type == BuildingType.ArcheryRange
                        || recovery.Type == BuildingType.House, Is.True);
                    Assert.That(construct.IsQueued, Is.False);
                    break;
                case TrainUnitCommand train:
                    Assert.That(command.Type, Is.EqualTo(CommandType.TrainUnit));
                    Assert.That(train.UnitType, Is.EqualTo(CommanderIntentCatalog.ArcherUnitType));
                    BuildingData producer = simulation.BuildingRegistry.GetBuilding(train.BuildingId);
                    Assert.That(producer, Is.Not.Null);
                    Assert.That(producer.PlayerId, Is.EqualTo(0));
                    Assert.That(producer.Type, Is.EqualTo(BuildingType.ArcheryRange));
                    break;
                default:
                    Assert.Fail("Unapproved Commander command type: " + command.GetType().Name);
                    break;
            }
        }

        private void AssertOwnedVillagers(int[] unitIds)
        {
            Assert.That(unitIds, Is.Not.Null.And.Not.Empty);
            Assert.That(unitIds.Distinct().Count(), Is.EqualTo(unitIds.Length));
            foreach (int id in unitIds)
            {
                UnitData unit = simulation.UnitRegistry.GetUnit(id);
                Assert.That(unit, Is.Not.Null);
                Assert.That(unit.PlayerId, Is.EqualTo(0));
                Assert.That(unit.IsVillager, Is.True);
            }
        }

        private readonly struct CommandObservation
        {
            public readonly ICommand Command;
            public readonly int Population;
            public readonly int PopulationCap;
            public readonly int Queued;

            public CommandObservation(ICommand command, int population, int populationCap, int queued)
            {
                Command = command;
                Population = population;
                PopulationCap = populationCap;
                Queued = queued;
            }
        }

        private void AssertIdentity(StrategicPlan plan, StrategicPlanHealthSnapshot health,
            int planId, int createdTick)
        {
            Assert.That(health, Is.Not.Null);
            Assert.That(health.PlayerId, Is.EqualTo(0));
            Assert.That(health.PlanId, Is.EqualTo(planId));
            Assert.That(health.CreatedTick, Is.EqualTo(createdTick));
            Assert.That(health.Revision, Is.EqualTo(plan.Revision));
            Assert.That(health.ObservedTick, Is.EqualTo(simulation.CurrentTick));
            Assert.That(plan.StrategicPlanId, Is.EqualTo(planId));
            Assert.That(plan.CreatedTick, Is.EqualTo(createdTick));
            Assert.That(planner.GetPlan(planId), Is.SameAs(plan));
        }

        private void Advance()
        {
            planner.Tick(simulation.CurrentTick);
            goals.Tick(simulation.CurrentTick);
            simulation.Tick();
        }

        private string CurrentStatus()
        {
            Transform label = chat.GetComponentsInChildren<Transform>(true)
                .Single(value => value.gameObject.name == "Current plan status");
            Component text = label.GetComponent("TextMeshProUGUI");
            return (string)text.GetType().GetProperty("text").GetValue(text);
        }

        private int OwnedQueuedUnits() => simulation.BuildingRegistry.GetAllBuildings()
            .Where(building => building.PlayerId == 0 && !building.IsDestroyed)
            .Sum(building => building.TrainingQueue.Count);

        private int Count(int type) => simulation.UnitRegistry.GetAllUnits()
            .Count(unit => unit.PlayerId == 0 && unit.CurrentHealth > 0 && unit.UnitType == type);

        private int Completed(BuildingType type) => simulation.BuildingRegistry.GetAllBuildings()
            .Count(building => building.PlayerId == 0 && !building.IsDestroyed
                && !building.IsUnderConstruction && building.Type == type);

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
                return new MockStrategicAIProvider().InterpretStrategicIntentAsync(request, token);
            }
        }
    }
}
