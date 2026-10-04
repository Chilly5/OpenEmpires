using System.Collections;
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4E4")]
    public sealed class CommanderPhase4E4ReachAgePlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private BuildingData townCenter;
        private UnitData worker;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            simulation.SetPlayerCivilizations(new[] { Civilization.English, Civilization.English });
            int centerX = simulation.MapData.Width / 2;
            int centerZ = simulation.MapData.Height / 2;
            for (int x = centerX - 35; x <= centerX + 35; x++)
                for (int z = centerZ - 20; z <= centerZ + 20; z++)
                {
                    simulation.MapData.Tiles[x, z] = TileType.Grass;
                    simulation.MapData.ForestDensity[x, z] = 0;
                    simulation.MapData.FoundationCount[x, z] = 0;
                    simulation.FogOfWar.SetVisible(0, x, z);
                }
            townCenter = simulation.CreateBuilding(0, BuildingType.TownCenter,
                centerX, centerZ, false, true);
            townCenter.AutoProduceVillagers = false;
            worker = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(centerX + 8, centerZ + 8),
                Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
            worker.IsVillager = true;
            worker.UnitType = 0;
            worker.MaxHealth = worker.CurrentHealth = 100;
            worker.State = UnitState.Idle;
            simulation.ResourceManager.GetPlayerResources(0).Food = 1000;
            simulation.ResourceManager.GetPlayerResources(0).Gold = 1000;
            goals = new CommanderGoalManager(simulation, 0);
        }

        [TearDown]
        public void TearDown()
        {
            goals?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void ReachAge_UsesNormalLandmarkCommandAndCompletesOnlyAfterRealAgeTransition()
        {
            ReachAgeGoal goal = goals.SubmitReachAge(CommanderSemanticAgeTarget.Next);
            goals.Tick(0);
            simulation.Tick();

            BuildingData landmark = simulation.BuildingRegistry.GetAllBuildings().Single(b =>
                b.PlayerId == 0 && b.Type == BuildingType.Landmark && !b.IsDestroyed);
            Assert.That(landmark.LandmarkId, Is.EqualTo(LandmarkId.English_Age2_A),
                "Commander chooses the lowest stable legal landmark ID.");
            Assert.That(landmark.IsUnderConstruction, Is.True);
            Assert.That(simulation.GetPlayerAge(0), Is.EqualTo(1));
            Assert.That(goal.AgeUpBuildingId, Is.EqualTo(landmark.Id));

            goals.Tick(15);
            Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed),
                "A queued or placed landmark is not age completion.");

            worker.SimPosition = simulation.MapData.TileToWorldFixed(landmark.OriginTileX - 1,
                landmark.OriginTileZ);
            FixedVector3 facing = landmark.SimPosition - worker.SimPosition;
            facing.y = Fixed32.Zero;
            worker.SimFacing = facing / facing.Magnitude();
            worker.State = UnitState.Constructing;
            worker.ConstructionTargetBuildingId = landmark.Id;
            landmark.ConstructionTicksRemaining = 1;
            simulation.Tick();

            Assert.That(simulation.GetPlayerAge(0), Is.EqualTo(2));
            goals.Tick(30);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed));
        }

        [Test]
        public void ReachAge_WhenAlreadyAtTargetSucceedsWithoutCommand()
        {
            // The desired-state condition is read from real simulation state; this test
            // uses the same private state seam as other simulation tests to model an
            // already-completed age transition without issuing a fake command.
            typeof(GameSimulation).GetField("playerAges",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(simulation, new[] { 3, 1 });
            ReachAgeGoal goal = goals.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
            goals.Tick(0);

            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void ReachAge_UnsupportedCivilizationTargetFailsWithTypedBlocker()
        {
            simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            ReachAgeGoal goal = goals.SubmitReachAge(CommanderSemanticAgeTarget.Imperial);
            goals.Tick(0);

            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Failed));
            Assert.That(goal.Blocker, Is.EqualTo(CommanderAgeBlocker.UnsupportedTarget));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator ReachAge_CastleTarget_CompletesTwoRealLandmarkTransitions()
        {
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 10000;
            ReachAgeGoal goal = goals.SubmitReachAge(CommanderSemanticAgeTarget.Castle);

            for (int expectedAge = 2; expectedAge <= 3; expectedAge++)
            {
                BuildingData landmark = null;
                for (int i = 0; i < 120 && landmark == null; i++)
                {
                    goals.Tick(simulation.CurrentTick);
                    simulation.Tick();
                    landmark = simulation.BuildingRegistry.GetAllBuildings().LastOrDefault(b =>
                        b.PlayerId == 0 && b.Type == BuildingType.Landmark && !b.IsDestroyed
                        && b.IsUnderConstruction);
                    if (i % 10 == 0) yield return null;
                }

                Assert.That(landmark, Is.Not.Null,
                    $"Castle target must place the age {expectedAge} landmark. {goal.StatusReason}");
                worker.SimPosition = simulation.MapData.TileToWorldFixed(
                    landmark.OriginTileX - 1, landmark.OriginTileZ);
                FixedVector3 facing = landmark.SimPosition - worker.SimPosition;
                facing.y = Fixed32.Zero;
                worker.SimFacing = facing / facing.Magnitude();
                worker.State = UnitState.Constructing;
                worker.ConstructionTargetBuildingId = landmark.Id;
                landmark.ConstructionTicksRemaining = 1;
                simulation.Tick();
                worker.State = UnitState.Idle;
                worker.ConstructionTargetBuildingId = -1;

                Assert.That(simulation.GetPlayerAge(0), Is.EqualTo(expectedAge),
                    $"Age {expectedAge} must be reached by the real simulation transition.");
            }

            // Goal planning is intentionally sampled on the manager's fixed planning
            // interval. Use the next aligned tick so this assertion observes the
            // post-transition desired-state check rather than a skipped evaluation.
            int completionTick = simulation.CurrentTick +
                (15 - simulation.CurrentTick % 15);
            goals.Tick(completionTick);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
            Debug.Log($"[Phase4E4 Castle Runtime] PASS Castle target reached at age {simulation.GetPlayerAge(0)}.");
        }

        [UnityTest]
        public IEnumerator ReachAge_WhenFoodAndGoldAreMissing_PreparesBothBeforeEitherIsReady()
        {
            typeof(MapData).GetField("holeMap",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(simulation.MapData, null);
            foreach (ResourceNodeData node in simulation.MapData.GetAllResourceNodes())
                node.RemainingAmount = 0;
            int centerX = simulation.MapData.Width / 2;
            int centerZ = simulation.MapData.Height / 2;
            ResourceNodeData food = simulation.MapData.AddResourceNode(ResourceType.Food,
                simulation.MapData.TileToWorldFixed(centerX + 10, centerZ + 8), 10000);
            ResourceNodeData gold = simulation.MapData.AddResourceNode(ResourceType.Gold,
                simulation.MapData.TileToWorldFixed(centerX + 13, centerZ + 8), 10000);
            UnitData secondWorker = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(centerX + 9, centerZ + 8),
                Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
            secondWorker.IsVillager = true;
            secondWorker.UnitType = 0;
            secondWorker.MaxHealth = secondWorker.CurrentHealth = 100;
            secondWorker.State = UnitState.Idle;
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Gold = 0;

            bool foodGather = false;
            bool goldGather = false;
            simulation.CommandBuffer.CommandEnqueued += (command, source) =>
            {
                if (source != CommandEnqueueSource.Commander || !(command is GatherCommand gather))
                    return;
                if (gather.ResourceNodeId == food.Id) foodGather = true;
                if (gather.ResourceNodeId == gold.Id) goldGather = true;
            };
            ReachAgeGoal goal = goals.SubmitReachAge(CommanderSemanticAgeTarget.Next);
            for (int i = 0; i < 180; i++)
            {
                goals.Tick(simulation.CurrentTick);
                simulation.Tick();
                if (i % 15 == 0) yield return null;
            }

            Assert.That(foodGather, Is.True, "A short age goal must begin food preparation.");
            Assert.That(goldGather, Is.True,
                "Gold preparation must start before the independent food deficit is fully paid. "
                + $"goal={goal.Status} reason={goal.StatusReason} food={resources.Food} gold={resources.Gold} "
                + $"worker=({worker.State},{worker.TargetResourceNodeId}) "
                + $"second=({secondWorker.State},{secondWorker.TargetResourceNodeId}).");
            Assert.That(new[] { worker, secondWorker }.Any(unit =>
                unit.TargetResourceNodeId == food.Id), Is.True,
                "Food preparation must remain assigned while gold preparation begins.");
            Assert.That(new[] { worker, secondWorker }.Any(unit =>
                unit.TargetResourceNodeId == gold.Id), Is.True,
                "A different worker must continue the independent gold preparation.");
            Assert.That(resources.Food, Is.LessThan(400),
                "This test must observe preparation before the age-up food cost is available.");
            Assert.That(simulation.GetPlayerAge(0), Is.EqualTo(1));
            Assert.That(goal.IsTerminal, Is.False);
        }

        [UnityTest]
        public IEnumerator ReachAge_CastleFromResourceShortState_GathersAndCompletesNaturally()
        {
            typeof(MapData).GetField("holeMap",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(simulation.MapData, null);
            foreach (ResourceNodeData node in simulation.MapData.GetAllResourceNodes())
                node.RemainingAmount = 0;
            int centerX = simulation.MapData.Width / 2;
            int centerZ = simulation.MapData.Height / 2;
            ResourceNodeData food = simulation.MapData.AddResourceNode(ResourceType.Food,
                simulation.MapData.TileToWorldFixed(centerX + 10, centerZ + 8), 10000);
            ResourceNodeData gold = simulation.MapData.AddResourceNode(ResourceType.Gold,
                simulation.MapData.TileToWorldFixed(centerX + 14, centerZ + 8), 10000);
            for (int i = 0; i < 5; i++)
            {
                UnitData villager = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(centerX + 8 + i, centerZ + 9),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                villager.IsVillager = true;
                villager.UnitType = 0;
                villager.MaxHealth = villager.CurrentHealth = 100;
                villager.State = UnitState.Idle;
            }
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Gold = 0;
            bool gatheredFood = false;
            bool gatheredGold = false;
            int landmarkCommands = 0;
            simulation.CommandBuffer.CommandEnqueued += (command, source) =>
            {
                if (source != CommandEnqueueSource.Commander) return;
                if (command is GatherCommand gather)
                {
                    if (gather.ResourceNodeId == food.Id) gatheredFood = true;
                    if (gather.ResourceNodeId == gold.Id) gatheredGold = true;
                }
                if (command is PlaceBuildingCommand place
                    && place.BuildingType == BuildingType.Landmark)
                    landmarkCommands++;
            };

            ReachAgeGoal goal = goals.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
            bool reachedFeudal = false;
            int maxConcurrentEconomyWorkers = 0;
            for (int i = 0; i < 36000 && !goal.IsTerminal; i++)
            {
                goals.Tick(simulation.CurrentTick);
                simulation.Tick();
                reachedFeudal |= simulation.GetPlayerAge(0) >= 2;
                if (i < 600 && resources.Food < 400 && resources.Gold < 200)
                    maxConcurrentEconomyWorkers = Math.Max(maxConcurrentEconomyWorkers,
                        simulation.UnitRegistry.GetAllUnits().Count(unit =>
                            unit.PlayerId == 0 && unit.IsVillager && unit.State != UnitState.Idle
                            && (unit.TargetResourceNodeId == food.Id
                                || unit.TargetResourceNodeId == gold.Id)));
                if (i % 300 == 0) yield return null;
            }

            Assert.That(gatheredFood, Is.True, "The short state must use a normal food-gather command.");
            Assert.That(gatheredGold, Is.True, "The short state must use a normal gold-gather command.");
            Assert.That(maxConcurrentEconomyWorkers, Is.GreaterThanOrEqualTo(3),
                "A multi-worker base must not leave almost all spare villagers idle while both age resources are short.");
            Assert.That(landmarkCommands, Is.GreaterThanOrEqualTo(2),
                "Both real landmark placements must use Commander commands.");
            Assert.That(reachedFeudal, Is.True);
            Assert.That(simulation.GetPlayerAge(0), Is.EqualTo(3), goal.StatusReason);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
            Debug.Log($"[Phase4E4 Castle Resource Runtime] PASS age={simulation.GetPlayerAge(0)} tick={simulation.CurrentTick} food={resources.Food} gold={resources.Gold}.");
        }
    }
}
