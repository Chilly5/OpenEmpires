using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

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
    }
}
