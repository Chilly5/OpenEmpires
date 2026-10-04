using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4E2")]
    public sealed class CommanderPhase4E2SpatialPlacementPlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private BuildingData anchor;
        private UnitData worker;
        private int anchorX;
        private int anchorZ;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            anchorX = simulation.MapData.Width / 2;
            anchorZ = simulation.MapData.Height / 2;
            typeof(MapData).GetField("holeMap", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(simulation.MapData, null);

            for (int x = anchorX - 35; x <= anchorX + 35; x++)
                for (int z = anchorZ - 20; z <= anchorZ + 20; z++)
                {
                    simulation.MapData.Tiles[x, z] = TileType.Grass;
                    simulation.MapData.ForestDensity[x, z] = 0;
                    simulation.MapData.FoundationCount[x, z] = 0;
                    simulation.FogOfWar.SetVisible(0, x, z);
                }
            foreach (ResourceNodeData node in simulation.MapData.GetAllResourceNodes())
                node.RemainingAmount = 0;

            anchor = simulation.CreateBuilding(0, BuildingType.TownCenter,
                anchorX, anchorZ, false, true);
            anchor.AutoProduceVillagers = false;
            BuildingData laterTownCenter = simulation.CreateBuilding(0,
                BuildingType.TownCenter, anchorX + 18, anchorZ, false, true);
            laterTownCenter.AutoProduceVillagers = false;

            worker = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(anchorX + 8, anchorZ + 8),
                Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
            worker.IsVillager = true;
            worker.UnitType = 0;
            worker.MaxHealth = worker.CurrentHealth = 100;
            worker.State = UnitState.Idle;
            simulation.ResourceManager.GetPlayerResources(0).Wood = 1000;
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
        public void SemanticWestPlacement_CreatesOwnedBarracksAtFiveClearColumnsAndBindsFoundation()
        {
            CommanderSemanticReferenceResolver referenceResolver =
                new CommanderSemanticReferenceResolver(simulation);
            Assert.That(referenceResolver.TryResolveOwnedAnchor(0,
                CommanderSemanticAnchorSelector.MyTownCenter, null, out BuildingData resolvedAnchor), Is.True);
            Assert.That(resolvedAnchor.Id, Is.EqualTo(anchor.Id),
                "The resolver must choose the oldest surviving owned Town Center.");

            BuildStructureGoal goal = SubmitPlacedBarracks();
            goals.Tick(0);
            simulation.Tick();

            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty,
                "The ordinary simulation tick should consume the queued placement command.");
            BuildingData created = simulation.BuildingRegistry.GetAllBuildings().Single(building =>
                building.PlayerId == 0 && building.Type == BuildingType.Barracks
                && building.OriginTileX < anchor.OriginTileX && !building.IsDestroyed);
            goals.Tick(15);

            Assert.That(created.OriginTileX, Is.EqualTo(anchor.OriginTileX
                - created.TileFootprintWidth - 5));
            Assert.That(created.OriginTileZ, Is.EqualTo(anchor.OriginTileZ
                + (anchor.TileFootprintHeight - created.TileFootprintHeight) / 2));
            Assert.That(anchor.OriginTileX - (created.OriginTileX + created.TileFootprintWidth), Is.EqualTo(5),
                "The number of clear intervening columns is measured edge-to-edge.");
            Assert.That(created.PlayerId, Is.EqualTo(0));
            Assert.That(created.IsUnderConstruction, Is.True);
            Assert.That(worker.PlayerId, Is.EqualTo(0));
            Assert.That(worker.IsVillager, Is.True);
            Assert.That(worker.ConstructionTargetBuildingId, Is.EqualTo(created.Id));
            Assert.That(GetGoalProperty(goal, "PlacedBuildingId"), Is.EqualTo(created.Id));
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.WaitingForConstruction));
        }

        [Test]
        public void SemanticWestPlacement_WhenEveryBoundedCandidateIsInvalidCreatesNothing()
        {
            for (int x = anchorX - 11; x <= anchorX - 4; x++)
                for (int z = anchorZ - 4; z <= anchorZ + 5; z++)
                    simulation.MapData.Tiles[x, z] = TileType.Water;

            BuildStructureGoal goal = SubmitPlacedBarracks();
            goals.Tick(0);
            simulation.Tick();

            Assert.That(simulation.BuildingRegistry.GetAllBuildings().Any(building =>
                building.PlayerId == 0 && building.Type == BuildingType.Barracks), Is.False);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            Assert.That(GetGoalProperty(goal, "PlacementBlocker")?.ToString(),
                Is.EqualTo("NoLegalCandidate"));
        }

        [Test]
        [Category("CommanderPhase4E3")]
        public void CompoundWestBuildThenSpearmen_UsesOnlyTheBoundNewBarracks()
        {
            CommanderSemanticResult parsed = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[" +
                "{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1," +
                "\"placement\":{\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":5}}," +
                "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10,\"producerFromNode\":0}]}" );
            CommanderContext context = new CommanderContextBuilder().Build(simulation, goals);
            Assert.That(CommanderSemanticGraphAdmission.TryAdmit(parsed, context,
                out CommanderSemanticGraphPlan plan, out string reason), Is.True, reason);
            var submitted = goals.SubmitSemanticGraph(plan);
            var build = submitted[0] as BuildStructureGoal;
            var units = submitted[1] as EnsureUnitCountGoal;

            goals.Tick(0);
            simulation.Tick();
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
            BuildingData created = simulation.BuildingRegistry.GetAllBuildings().Single(building =>
                building.PlayerId == 0 && building.Type == BuildingType.Barracks
                && building.OriginTileX < anchor.OriginTileX && !building.IsDestroyed);
            goals.Tick(15);
            Assert.That(units.RequiredProducerGoal, Is.SameAs(build));
            Assert.That(build.PlacedBuildingId, Is.EqualTo(created.Id));
            Assert.That(units.Status, Is.EqualTo(CommanderGoalStatus.WaitingForConstruction));

            created.IsUnderConstruction = false;
            created.ConstructionTicksRemaining = 0;
            goals.Tick(30);
            var commands = simulation.CommandBuffer.FlushCommands();
            TrainUnitCommand train = commands.OfType<TrainUnitCommand>().Single();
            Assert.That(train.BuildingId, Is.EqualTo(created.Id));
            Assert.That(train.UnitType, Is.EqualTo(CommanderIntentCatalog.SpearmanUnitType));

            created.CurrentHealth = 0;
            simulation.CreateBuilding(0, BuildingType.Barracks, anchorX + 20, anchorZ + 10, false, true);
            goals.Tick(45);
            Assert.That(units.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty,
                "A linked goal must not switch to an unrelated Barracks after its bound result is destroyed.");
        }

        [UnityTest]
        [Category("CommanderPhase4E3")]
        public IEnumerator CompoundWestBuildThenSpearmen_CompletesThroughNormalSimulation()
        {
            simulation.CreateBuilding(0, BuildingType.House, anchorX + 12, anchorZ + 12, false);
            for (int i = 0; i < 3; i++)
            {
                UnitData extraWorker = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(anchorX + 7 + i, anchorZ + 9),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                extraWorker.IsVillager = true;
                extraWorker.UnitType = 0;
                extraWorker.MaxHealth = extraWorker.CurrentHealth = 100;
                extraWorker.State = UnitState.Idle;
            }

            CommanderSemanticResult parsed = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[" +
                "{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1," +
                "\"placement\":{\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":5}}," +
                "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10," +
                "\"producerFromNode\":0}]}" );
            CommanderContext context = new CommanderContextBuilder().Build(simulation, goals);
            Assert.That(CommanderSemanticGraphAdmission.TryAdmit(parsed, context,
                out CommanderSemanticGraphPlan plan, out string reason), Is.True, reason);
            var submitted = goals.SubmitSemanticGraph(plan);
            var build = submitted[0] as BuildStructureGoal;
            var units = submitted[1] as EnsureUnitCountGoal;
            Assert.That(build, Is.Not.Null);
            Assert.That(units, Is.Not.Null);

            for (int i = 0; i < 30000 && !units.IsTerminal; i++)
            {
                goals.Tick(simulation.CurrentTick);
                simulation.Tick();
                if (i % 300 == 0) yield return null;
            }

            int spearmen = simulation.UnitRegistry.GetAllUnits().Count(unit =>
                unit.PlayerId == 0 && unit.UnitType == CommanderIntentCatalog.SpearmanUnitType
                && unit.CurrentHealth > 0);
            BuildingData created = simulation.BuildingRegistry.GetAllBuildings().SingleOrDefault(building =>
                building.PlayerId == 0 && building.Type == BuildingType.Barracks
                && !building.IsDestroyed && building.Id == build.PlacedBuildingId);
            Assert.That(created, Is.Not.Null, build.StatusReason);
            Assert.That(created.OriginTileX, Is.EqualTo(anchor.OriginTileX
                - created.TileFootprintWidth - 5));
            Assert.That(units.RequiredProducerGoal, Is.SameAs(build));
            Assert.That(units.Status, Is.EqualTo(CommanderGoalStatus.Completed),
                units.StatusReason + $" owned={spearmen} tick={simulation.CurrentTick}");
            Assert.That(spearmen, Is.GreaterThanOrEqualTo(10));
            Debug.Log($"[Phase4E3 Compound Runtime] PASS bound barracks #{created.Id}, spearmen={spearmen}, tick={simulation.CurrentTick}.");
        }

        private BuildStructureGoal SubmitPlacedBarracks()
        {
            CommanderSemanticResult parsed = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\","
                + "\"structure\":\"Barracks\",\"count\":1,\"placement\":{\"anchor\":\"MyTownCenter\","
                + "\"relation\":\"MapWest\",\"clearGapTiles\":5}}]}");
            Assert.That(parsed.IsValid, Is.True);
            CommanderContext context = new CommanderContextBuilder().Build(simulation, goals);
            Assert.That(CommanderSemanticAdmission.TryCreateTacticalIntent(parsed.Nodes[0], context,
                out CommanderIntent intent, out string reason), Is.True, reason);

            CommanderIntentResolution resolution = new CommanderIntentResolver()
                .Resolve(intent, simulation, goals);
            Assert.That(resolution.CreatedGoal, Is.True, resolution.Reason);
            Assert.That(resolution.Goal, Is.TypeOf<BuildStructureGoal>());
            return (BuildStructureGoal)resolution.Goal;
        }

        private static object GetGoalProperty(BuildStructureGoal goal, string name)
        {
            PropertyInfo property = typeof(BuildStructureGoal).GetProperty(name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "Placed-build goals must expose a typed result or blocker.");
            return property.GetValue(goal);
        }
    }
}
