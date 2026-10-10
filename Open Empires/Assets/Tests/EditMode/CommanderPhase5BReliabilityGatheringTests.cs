using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5BReliability")]
    public sealed class CommanderPhase5BReliabilityGatheringTests
    {
        private SimulationConfig config;
        private GameSimulation sim;
        private CommanderGoalManager manager;
        private int x, z;
        private readonly List<ICommand> issued = new List<ICommand>();
        private int[] workers;

        [SetUp] public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            sim.SetPlayerCivilizations(new[] { Civilization.English });
            x = sim.MapData.Width / 2; z = sim.MapData.Height / 2;
            typeof(MapData).GetField("holeMap", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(sim.MapData, null);
            foreach (var node in sim.MapData.GetAllResourceNodes()) node.RemainingAmount = 0;
            for (int a = x - 25; a <= x + 25; a++) for (int b = z - 25; b <= z + 25; b++)
            {
                sim.MapData.Tiles[a, b] = TileType.Grass; sim.MapData.ForestDensity[a, b] = 0;
                sim.MapData.FoundationCount[a, b] = 0; sim.FogOfWar.SetVisible(0, a, b);
            }
            sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true).AutoProduceVillagers = false;
            workers = Enumerable.Range(0, 4).Select(i => Worker(x + 5 + i, z + 5).Id).ToArray();
            sim.ResourceManager.GetPlayerResources(0).Food = 0;
            manager = new CommanderGoalManager(sim, 0);
            issued.Clear(); sim.CommandBuffer.CommandEnqueued += Capture;
        }
        [TearDown] public void TearDown()
        { sim.CommandBuffer.CommandEnqueued -= Capture; manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        private void Capture(ICommand command, CommandEnqueueSource source)
        { if (source == CommandEnqueueSource.Commander) issued.Add(command); }
        private UnitData Worker(int a, int b)
        {
            var unit = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(a, b), Fixed32.FromFloat(2), Fixed32.FromFloat(.4f), Fixed32.One);
            unit.IsVillager = true; unit.UnitType = 0; unit.State = UnitState.Idle; unit.CurrentHealth = unit.MaxHealth = 100;
            return unit;
        }
        private ResourceNodeData Berries(int a, int amount)
            => sim.MapData.AddResourceNode(ResourceType.Food, sim.MapData.TileToWorldFixed(a, z + 8), amount);
        private void IsolatedBerries(int a, int b)
        {
            var node = sim.MapData.AddResourceNode(ResourceType.Food, sim.MapData.TileToWorldFixed(a, b), 1000);
            for (int tx = node.TileX - 1; tx <= node.TileX + node.FootprintWidth; tx++)
                for (int tz = node.TileZ - 1; tz <= node.TileZ + node.FootprintHeight; tz++)
                    sim.MapData.Tiles[tx, tz] = TileType.Water;
        }
        private AllocateWorkersGoal Start(ResourceSourceKind source)
            => manager.SubmitWorkerAllocation(new CommanderWorkerAllocation(CommanderWorkerAllocationMode.SelectedCount,
                CommanderWorkerCountMode.Exact, 4, new CommanderWorkerSelector(CommanderWorkerState.Idle),
                new CommanderResourceDestination(ResourceType.Food, source), 400, CommanderResourceAmountMode.AdditionalGathered));
        private void Run(AllocateWorkersGoal goal)
        { for (int i = 0; i < 30000 && !goal.IsTerminal; i++) { manager.Tick(sim.CurrentTick); sim.Tick(); } }

        [Test] public void HouseCompletionDependency_AssignsExistingWorkerOnlyAfterNativeHouseFinishes()
        {
            sim.ResourceManager.GetPlayerResources(0).Wood = 1000;
            sim.MapData.AddResourceNode(ResourceType.Wood, sim.MapData.TileToWorldFixed(x + 15, z + 10), 10000);
            var parsed = CommanderSemanticJson.ParseProviderResponse("{\"outcome\":\"Request\",\"executionOrder\":\"Sequential\",\"nodes\":["
                + "{\"type\":\"BuildStructure\",\"structure\":\"House\",\"count\":1,\"builders\":{\"state\":\"Eligible\",\"count\":1}},"
                + "{\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\",\"countMode\":\"Exact\",\"count\":1,\"workers\":{\"state\":\"Any\"},\"destination\":{\"resource\":\"Wood\"},\"dependsOn\":[0]}]}");
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var scope = manager.PrepareActionPlan(parsed,
                "Build a House first, and only after it finishes send a villager to wood", 0);
            Assert.That(issued, Is.Empty, "A candidate cannot execute before game-side approval.");
            var goals = manager.SubmitSemanticGraph(manager.ApproveActionPlan(scope, 0));
            for (int i = 0; i < 12000 && !goals.All(g => g.IsTerminal); i++)
            {
                if (!sim.BuildingRegistry.GetAllBuildings().Any(b => b.Type == BuildingType.House && !b.IsUnderConstruction && !b.IsDestroyed))
                    Assert.That(issued.OfType<GatherCommand>(), Is.Empty, "Wood assignment cannot precede real House completion.");
                manager.Tick(sim.CurrentTick); sim.Tick();
            }
            Assert.That(goals.Select(g => g.Status), Is.All.EqualTo(CommanderGoalStatus.Completed));
            Assert.That(sim.BuildingRegistry.GetAllBuildings().Count(b => b.Type == BuildingType.House && !b.IsUnderConstruction && !b.IsDestroyed), Is.EqualTo(1));
            Assert.That(issued.OfType<PlaceBuildingCommand>().Count(), Is.EqualTo(1));
            Assert.That(workers, Does.Contain(issued.OfType<GatherCommand>().SelectMany(c => c.UnitIds).Distinct().Single()));
            Assert.That(issued.OfType<TrainUnitCommand>(), Is.Empty, "An existing villager request must not invent production.");
            TestContext.WriteLine($"Native ordered House then Wood; tick={sim.CurrentTick}; commands={issued.Count}");
        }

        [Test] public void AdditionalFood400_RecoversOriginalFourWorkersFromNativeSheepDepletionToVisibleBerries()
        {
            var sheep = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x + 6, z + 6), Fixed32.One, Fixed32.One, Fixed32.One);
            sheep.IsSheep = true; sheep.CurrentHealth = sheep.MaxHealth = 100; sheep.State = UnitState.Idle;
            var alternative = Berries(x + 17, 1000);
            var goal = Start(ResourceSourceKind.Any);
            var falseBusyBlockers = new List<string>();
            for (int i = 0; i < 30000 && !goal.IsTerminal; i++)
            {
                manager.Tick(sim.CurrentTick);
                if (goal.Status == CommanderGoalStatus.Blocked && goal.StatusReason.Contains("busy with another activity"))
                    falseBusyBlockers.Add("tick=" + sim.CurrentTick + "; " + string.Join(";", workers.Select(id =>
                    { var w = sim.UnitRegistry.GetUnit(id); return id + ":" + w.State + ":sheep=" + w.CombatTargetId; })));
                sim.Tick();
            }
            Assert.That(issued.OfType<SlaughterSheepCommand>(), Is.Not.Empty, "Native first source must be the nearby sheep.");
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
            Assert.That(sim.ResourceManager.GetGatheredIncome(0, ResourceType.Food), Is.GreaterThanOrEqualTo(400));
            Assert.That(issued.OfType<GatherCommand>().Any(c => c.ResourceNodeId == alternative.Id), Is.True,
                "After first source exhaustion, same-worker legal alternative must receive a normal gather command.");
            Assert.That(issued.OfType<GatherCommand>().SelectMany(c => c.UnitIds).Distinct().All(workers.Contains), Is.True);
            int count = issued.Count;
            for (int i = 0; i < 90; i++) { manager.Tick(sim.CurrentTick); sim.Tick(); }
            Assert.That(issued.Count, Is.EqualTo(count), "No further Commander recovery actions after the target.");
            Assert.That(falseBusyBlockers, Is.Empty, "A native transition on the request's own sheep order is not an unrelated busy activity: " + string.Join(" | ", falseBusyBlockers.Take(3)));
            TestContext.WriteLine($"Native additional Food={goal.ResourceProgress}; tick={sim.CurrentTick}; commandCount={count}; originalWorkers={string.Join(",",workers)}");
        }

        [Test] public void ExplicitBerries400_RecoversOnlyToBerriesAndKeepsOriginalFourWorkers()
        {
            var first = Berries(x + 6, 150); var second = Berries(x + 17, 1000);
            var goal = Start(ResourceSourceKind.Berries); Run(goal);
            Assert.That(first.IsDepleted, Is.True);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
            Assert.That(sim.ResourceManager.GetGatheredIncome(0, ResourceType.Food), Is.GreaterThanOrEqualTo(400));
            Assert.That(issued.OfType<GatherCommand>().All(c => c.SourceKind == ResourceSourceKind.Berries), Is.True);
            Assert.That(issued.OfType<GatherCommand>().SelectMany(c => c.UnitIds).Distinct().All(workers.Contains), Is.True);
            Assert.That(issued.OfType<SlaughterSheepCommand>(), Is.Empty);
        }

        [Test] public void BerriesRecovery_SearchesBeyondFourNearerUnreachableSourcesWithinPerTickBound()
        {
            var first = Berries(x + 6, 150);
            var far = sim.MapData.AddResourceNode(ResourceType.Food, sim.MapData.TileToWorldFixed(x + 23, z + 22), 1000);
            var goal = Start(ResourceSourceKind.Berries);
            // Establish ordinary native gathering before introducing the recovery obstacles.
            for (int i = 0; i < 8000 && goal.ResourceProgress < 20 && !goal.IsTerminal; i++)
            { manager.Tick(sim.CurrentTick); sim.Tick(); }
            Assert.That(goal.ResourceProgress, Is.GreaterThanOrEqualTo(20));
            foreach (var tile in new[] { new Vector2Int(x + 12, z + 8), new Vector2Int(x + 17, z + 8),
                new Vector2Int(x + 12, z + 15), new Vector2Int(x + 17, z + 15) })
                IsolatedBerries(tile.x, tile.y);
            sim.FogOfWar.SetVisionCheat(0, true); // Controlled known terrain; production still enforces visibility/known paths.
            int maxChecks = 0;
            for (int i = 0; i < 30000 && !goal.IsTerminal; i++)
            {
                manager.ResetDiagnosticPathCheckCount(); manager.Tick(sim.CurrentTick);
                maxChecks = Math.Max(maxChecks, manager.DiagnosticPathCheckCount); sim.Tick();
            }
            Assert.That(first.IsDepleted, Is.True);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
            Assert.That(sim.ResourceManager.GetGatheredIncome(0, ResourceType.Food), Is.GreaterThanOrEqualTo(400));
            Assert.That(issued.OfType<GatherCommand>().Any(c => c.ResourceNodeId == far.Id), Is.True);
            Assert.That(issued.OfType<GatherCommand>().SelectMany(c => c.UnitIds).Distinct(), Is.EquivalentTo(workers));
            Assert.That(issued.OfType<GatherCommand>().All(c => c.SourceKind == ResourceSourceKind.Berries), Is.True);
            Assert.That(maxChecks, Is.LessThanOrEqualTo(16), "At most four path candidates per original worker per planner tick.");
            TestContext.WriteLine($"Progressive native recovery: Food={goal.ResourceProgress}; tick={sim.CurrentTick}; maxPathChecks={maxChecks}");
        }

        [Test] public void BerriesRecovery_CompleteUnreachableSweepStillExpiresAsBlockedWithoutCommands()
        {
            Berries(x + 6, 150);
            var goal = Start(ResourceSourceKind.Berries);
            for (int i = 0; i < 8000 && goal.ResourceProgress < 20 && !goal.IsTerminal; i++)
            { manager.Tick(sim.CurrentTick); sim.Tick(); }
            Assert.That(goal.ResourceProgress, Is.GreaterThanOrEqualTo(20));
            foreach (var tile in new[] { new Vector2Int(x + 12, z + 8), new Vector2Int(x + 17, z + 8),
                new Vector2Int(x + 12, z + 15), new Vector2Int(x + 17, z + 15), new Vector2Int(x + 23, z + 22) })
                IsolatedBerries(tile.x, tile.y);
            sim.FogOfWar.SetVisionCheat(0, true);
            int before = issued.Count;
            Run(goal);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Failed));
            Assert.That(goal.StatusReason, Does.StartWith("Blocked for 1800 ticks."),
                "Repeated bounded sweeps must not disguise a proven blocker until the whole duration timeout.");
            Assert.That(issued.Count, Is.EqualTo(before));
            Assert.That(sim.ResourceManager.GetGatheredIncome(0, ResourceType.Food), Is.LessThan(400));
        }

        [Test] public void ExplicitBerries_WhenExhaustedDoesNotSwitchToAvailableSheep()
        {
            Berries(x + 6, 150);
            var goal = Start(ResourceSourceKind.Berries);
            manager.Tick(0); sim.Tick();
            var sheep = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x + 7, z + 7), Fixed32.One, Fixed32.One, Fixed32.One);
            sheep.IsSheep = true; sheep.CurrentHealth = sheep.MaxHealth = 100; sheep.State = UnitState.Idle;
            Run(goal);
            Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed));
            Assert.That(issued.OfType<SlaughterSheepCommand>(), Is.Empty);
            Assert.That(sim.ResourceManager.GetGatheredIncome(0, ResourceType.Food), Is.LessThan(400));
        }

        [Test] public void ExplicitNumericAge3_ReachesActualNativeAge3NotJustTheNextAge()
        {
            var resources = sim.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Gold = resources.Wood = 10000;
            var parsed = CommanderSemanticJson.ParseProviderResponse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"ReachAge\",\"targetAge\":3}]}");
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var scope = manager.PrepareActionPlan(parsed, "Reach Age 3", sim.CurrentTick);
            var goal = manager.SubmitSemanticGraph(manager.ApproveActionPlan(scope, sim.CurrentTick)).Single();
            for (int i = 0; i < 30000 && !goal.IsTerminal; i++) { manager.Tick(sim.CurrentTick); sim.Tick(); }
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
            Assert.That(sim.GetPlayerAge(0), Is.EqualTo(3));
            Assert.That(issued.OfType<PlaceBuildingCommand>().Count(), Is.EqualTo(2), "Native Feudal then Castle landmarks, not a completion flag shortcut.");
            TestContext.WriteLine($"Native Age={sim.GetPlayerAge(0)}; tick={sim.CurrentTick}");
        }

        [TestCase("human")] [TestCase("loss")] [TestCase("cancel")]
        public void GatheringRecovery_NeverReclaimsOrReplacesFrozenWorkers(string interruption)
        {
            Berries(x + 6, 150); Berries(x + 17, 1000);
            var goal = Start(ResourceSourceKind.Berries);
            for (int i = 0; i < 8000 && goal.ResourceProgress < 20 && !goal.IsTerminal; i++)
            { manager.Tick(sim.CurrentTick); sim.Tick(); }
            Assert.That(goal.ResourceProgress, Is.GreaterThanOrEqualTo(20));
            if (interruption == "human") sim.CommandBuffer.EnqueueCommand(new StopCommand(0, new[] { workers[0] }));
            else if (interruption == "loss")
            { var worker = sim.UnitRegistry.GetUnit(workers[0]); worker.CurrentHealth = 0; worker.State = UnitState.Dead; }
            else manager.CancelGoal(goal.GoalId);
            sim.Tick();
            int before = issued.Count;
            Worker(x + 10, z + 6); // Available replacement must never enter this frozen request.
            for (int i = 0; i < 3000 && !goal.IsTerminal; i++) { manager.Tick(sim.CurrentTick); sim.Tick(); }
            Assert.That(issued.Count, Is.EqualTo(before));
            Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed));
        }

        [Test] public void FirstTownCenterOrdinal_WatchesFiveRealBirthsWithoutTrainingOrSwitchingProducer()
        {
            var first = sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
            var second = sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z - 15, false, true);
            second.AutoProduceVillagers = false;
            sim.ResourceManager.GetPlayerResources(0).Food = 10000;
            sim.MapData.AddResourceNode(ResourceType.Wood, sim.MapData.TileToWorldFixed(x + 10, z + 10), 10000);
            var parsed = CommanderSemanticJson.ParseProviderResponse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"WatchFutureUnits\",\"unit\":\"Villager\",\"count\":5,\"producer\":\"TownCenter\",\"producerOrdinal\":1,\"action\":\"Gather\",\"resource\":\"Wood\"}]}");
            var goal = (WatchFutureUnitsGoal)manager.SubmitSemanticGraph(manager.ApproveActionPlan(
                manager.PrepareActionPlan(parsed, "Send the next five villagers from my first Town Center to Wood", 0), 0)).Single();
            Assert.That(goal.ProducerId, Is.EqualTo(first.Id));
            var births = new Dictionary<int, int>();
            var birthEvent = typeof(GameSimulation).GetEvent("ProducerUnitProduced", BindingFlags.Instance | BindingFlags.NonPublic);
            var parameter = Expression.Parameter(birthEvent.EventHandlerType.GetMethod("Invoke").GetParameters()[0].ParameterType);
            Action<object> observe = value =>
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                births.Add((int)value.GetType().GetProperty("UnitId", flags).GetValue(value),
                    (int)value.GetType().GetProperty("ProducerId", flags).GetValue(value));
            };
            var listener = Expression.Lambda(birthEvent.EventHandlerType,
                Expression.Call(Expression.Constant(observe), typeof(Action<object>).GetMethod("Invoke"), Expression.Convert(parameter, typeof(object))), parameter).Compile();
            birthEvent.GetAddMethod(true).Invoke(sim, new object[] { listener });
            try
            {
                for (int i = 0; i < 5; i++) sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(0, first.Id, 0));
                sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(0, second.Id, 0));
                for (int i = 0; i < 20000 && !goal.IsTerminal; i++) { manager.Tick(sim.CurrentTick); sim.Tick(); }
            }
            finally { birthEvent.GetRemoveMethod(true).Invoke(sim, new object[] { listener }); }
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
            Assert.That(goal.ObservedCount, Is.EqualTo(5));
            Assert.That(goal.AssignedCount, Is.EqualTo(5));
            Assert.That(issued.OfType<TrainUnitCommand>(), Is.Empty, "Watcher must not order production.");
            Assert.That(issued.OfType<GatherCommand>().SelectMany(c => c.UnitIds).Distinct().Count(), Is.EqualTo(5));
            Assert.That(births.Values, Does.Contain(second.Id), "The competing producer must actually produce a birth.");
            Assert.That(issued.OfType<GatherCommand>().SelectMany(c => c.UnitIds).Distinct(),
                Is.EquivalentTo(births.Where(b => b.Value == first.Id).Select(b => b.Key)), "Only the exact five native first-TC births may be assigned.");
            TestContext.WriteLine($"Native first-TC producer={first.Id}; observed={goal.ObservedCount}; assigned={goal.AssignedCount}; tick={sim.CurrentTick}");
        }
    }
}
