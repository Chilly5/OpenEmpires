using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4E2")]
    public sealed class CommanderPhase4E2SpatialIntegrationTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private int anchorX;
        private int anchorZ;
        private UnitData worker;

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
            simulation.CreateBuilding(0, BuildingType.TownCenter, anchorX, anchorZ, false, true)
                .AutoProduceVillagers = false;
            worker = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(anchorX + 8, anchorZ + 8),
                Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
            worker.IsVillager = true;
            worker.UnitType = 0;
            worker.MaxHealth = worker.CurrentHealth = 100;
            worker.State = UnitState.Idle;
            simulation.ResourceManager.GetPlayerResources(0).Wood = 1000;
            goals = new CommanderGoalManager(simulation, 0);
        }

        [TearDown]
        public void TearDown()
        {
            goals?.Dispose();
            UnityEngine.Object.DestroyImmediate(config);
        }

        // Mutation caught: generic perimeter search or completed-count accounting ignores the semantic target.
        [Test]
        public void PlacedBuild_UsesOrdinaryCommandAtExactWestOriginDespiteUnrelatedCompletedBarracks()
        {
            simulation.CreateBuilding(0, BuildingType.Barracks, anchorX + 15, anchorZ);
            BuildStructureGoal goal = SubmitPlacedBarracks();

            goals.Tick(0);

            PlaceBuildingCommand place = SinglePlace();
            Assert.That(place.PlayerId, Is.EqualTo(0));
            Assert.That(place.BuildingType, Is.EqualTo(BuildingType.Barracks));
            Assert.That(place.TileX, Is.EqualTo(anchorX - 8));
            Assert.That(place.TileZ, Is.EqualTo(anchorZ));
            Assert.That(place.VillagerUnitIds, Is.EqualTo(new[] { worker.Id }));
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Executing));
        }

        // Mutation caught: an unrelated in-progress building diverts construction recovery from the requested tile.
        [Test]
        public void PlacedBuild_DoesNotRecoverAnUnrelatedSameTypeFoundation()
        {
            BuildingData unrelated = simulation.CreateBuilding(0, BuildingType.Barracks,
                anchorX + 15, anchorZ, underConstruction: true);
            SubmitPlacedBarracks();

            goals.Tick(0);

            PlaceBuildingCommand place = SinglePlace();
            Assert.That(place.TileX, Is.EqualTo(anchorX - 8));
            Assert.That(worker.ConstructionTargetBuildingId, Is.Not.EqualTo(unrelated.Id));
        }

        // Mutation caught: a blocked bounded set silently falls through to an unrelated generic location.
        [Test]
        public void PlacedBuild_ExhaustedCandidatesHasTypedBlockerAndNoCommand()
        {
            for (int x = anchorX - 11; x <= anchorX - 4; x++)
                for (int z = anchorZ - 4; z <= anchorZ + 5; z++)
                    simulation.MapData.Tiles[x, z] = TileType.Water;
            BuildStructureGoal goal = SubmitPlacedBarracks();

            goals.Tick(0);

            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
            Assert.That(GetGoalProperty(goal, "PlacementBlocker")?.ToString(),
                Is.EqualTo("NoLegalCandidate"));
        }

        // Mutation caught: visibility of the footprint but not its border is treated as sufficient.
        [Test]
        public void PlacedBuild_RequiresCurrentVisibilityOfFootprintAndBorder()
        {
            simulation.FogOfWar.DemoteAllVisible(0);
            for (int x = anchorX - 8; x <= anchorX - 6; x++)
                for (int z = anchorZ; z <= anchorZ + 2; z++)
                    simulation.FogOfWar.SetVisible(0, x, z);
            Assert.That(simulation.FogOfWar.GetVisibility(0, anchorX - 9, anchorZ),
                Is.EqualTo(TileVisibility.Explored));
            BuildStructureGoal goal = SubmitPlacedBarracks();

            goals.Tick(0);

            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        // Mutation caught: treating the requested exact tile as the only legal site despite bounded alternatives.
        [Test]
        public void PlacedBuild_InvalidExactTerrainUsesOnlyBoundedFallbackAndExplainsIt()
        {
            simulation.MapData.Tiles[anchorX - 9, anchorZ + 3] = TileType.Water;
            BuildStructureGoal goal = SubmitPlacedBarracks();

            goals.Tick(0);

            PlaceBuildingCommand place = SinglePlace();
            Assert.That(place.TileX, Is.InRange(anchorX - 9, anchorX - 7));
            Assert.That(place.TileZ, Is.InRange(anchorZ - 2, anchorZ + 2));
            Assert.That(place.TileX == anchorX - 8 && place.TileZ == anchorZ, Is.False);
            Assert.That(anchorX - (place.TileX + 3), Is.InRange(4, 6));
            Assert.That(goal.StatusReason, Does.Contain("fallback").IgnoreCase);
            Assert.That(goal.StatusReason, Does.Contain("gap").IgnoreCase);
        }

        // Mutation caught: explored tiles on a complete route are incorrectly treated as hidden.
        [Test]
        public void PlacedBuild_CompleteExploredWorkerPathIsLegitimate()
        {
            simulation.FogOfWar.DemoteAllVisible(0);
            RevealCandidateArea();
            BuildStructureGoal goal = SubmitPlacedBarracks();

            goals.Tick(0);

            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Executing), goal.StatusReason);
            Assert.That(SinglePlace().VillagerUnitIds, Is.EqualTo(new[] { worker.Id }));
        }

        // Mutation caught: a pathfinder route across unexplored cells is accepted without full known-path audit.
        [Test]
        public void PlacedBuild_UnexploredBarrierRejectsPartialKnownWorkerPath()
        {
            byte[] visibility = (byte[])typeof(FogOfWarData).GetField("visibility",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(simulation.FogOfWar);
            int barrierX = anchorX + 6;
            for (int z = 0; z < simulation.MapData.Height; z++)
                visibility[z * simulation.MapData.Width + barrierX] = (byte)TileVisibility.Unexplored;
            Assert.That(simulation.FogOfWar.GetVisibility(0, barrierX, anchorZ + 8),
                Is.EqualTo(TileVisibility.Unexplored));
            BuildStructureGoal goal = SubmitPlacedBarracks();

            goals.Tick(0);

            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        // Mutation caught: a command-created foundation is not bound and an unrelated ID is reported instead.
        [Test]
        public void PlacedBuild_BindsNewFoundationIdAtRequestedTargetOnly()
        {
            BuildingData unrelated = simulation.CreateBuilding(0, BuildingType.Barracks,
                anchorX + 15, anchorZ, underConstruction: true);
            BuildStructureGoal goal = SubmitPlacedBarracks();
            goals.Tick(0);
            simulation.Tick();

            BuildingData created = simulation.BuildingRegistry.GetAllBuildings().Single(building =>
                building.PlayerId == 0 && building.Type == BuildingType.Barracks
                && building.OriginTileX == anchorX - 8 && building.OriginTileZ == anchorZ);
            goals.Tick(15);

            Assert.That(GetGoalProperty(goal, "PlacedBuildingId"), Is.EqualTo(created.Id));
            Assert.That(created.Id, Is.Not.EqualTo(unrelated.Id));
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.WaitingForConstruction));
        }

        // Mutation caught: a semantic build bypasses the existing age and resource gates.
        [Test]
        public void PlacedBuild_PreservesAgeAndCostGateBeforePlacement()
        {
            int[] ages = (int[])typeof(GameSimulation).GetField("playerAges",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(simulation);
            ages[0] = 0;
            BuildStructureGoal goal = SubmitPlacedBarracks();
            goals.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.WaitingForPrerequisite));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);

            ages[0] = 1;
            simulation.ResourceManager.GetPlayerResources(0).Wood = 0;
            goals.Tick(15);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked)
                .Or.EqualTo(CommanderGoalStatus.WaitingForResources));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        // Mutation caught: a foreign villager is borrowed when no eligible owned builder exists.
        [Test]
        public void PlacedBuild_RejectsForeignOnlyBuilder()
        {
            worker.PlayerId = 1;
            BuildStructureGoal goal = SubmitPlacedBarracks();

            goals.Tick(0);

            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        // Mutation caught: planning twice before simulation consumes the queue emits a duplicate foundation.
        [Test]
        public void PlacedBuild_DoesNotRepeatPlacementWhileOrdinaryCommandRemainsQueued()
        {
            BuildStructureGoal goal = SubmitPlacedBarracks();
            goals.Tick(0);

            goals.Tick(15);

            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.WaitingForConstruction));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Has.Count.EqualTo(1));
        }

        // Mutation caught: an occupied footprint or border tile is accepted as a semantic build site.
        [Test]
        public void PlacedBuild_OccupiedExactBorderCannotReceivePlacement()
        {
            simulation.CreateBuilding(0, BuildingType.House, anchorX - 9, anchorZ + 3);
            BuildStructureGoal goal = SubmitPlacedBarracks();

            goals.Tick(0);

            ICommand[] commands = simulation.CommandBuffer.FlushCommands().ToArray();
            Assert.That(commands.All(command => !(command is PlaceBuildingCommand place)
                || place.TileX != anchorX - 8 || place.TileZ != anchorZ), Is.True);
            Assert.That(goal.Status == CommanderGoalStatus.Blocked
                || goal.Status == CommanderGoalStatus.Executing, Is.True);
        }

        // Mutation caught: footprint-only bounds allow an off-map border on the west edge.
        [Test]
        public void PlacedBuild_MapEdgeHasNoOutOfBoundsFallback()
        {
            simulation.CreateBuilding(0, BuildingType.TownCenter, 2, anchorZ);
            BuildStructureGoal goal = SubmitPlacedBarracks(ordinal: 2);

            goals.Tick(0);

            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            Assert.That(GetGoalProperty(goal, "PlacementBlocker")?.ToString(),
                Is.EqualTo("NoLegalCandidate"));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        // Mutation caught: the first eligible but unreachable villager masks a second reachable owned villager.
        [Test]
        public void PlacedBuild_SelectsReachableOwnedWorkerWhenFirstEligibleCannotReach()
        {
            byte[] visibility = (byte[])typeof(FogOfWarData).GetField("visibility",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(simulation.FogOfWar);
            int barrierX = anchorX + 6;
            for (int z = 0; z < simulation.MapData.Height; z++)
                visibility[z * simulation.MapData.Width + barrierX] = (byte)TileVisibility.Unexplored;
            UnitData reachable = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(anchorX - 12, anchorZ),
                Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
            reachable.IsVillager = true;
            reachable.UnitType = 0;
            reachable.MaxHealth = reachable.CurrentHealth = 100;
            reachable.State = UnitState.Idle;
            SubmitPlacedBarracks();

            goals.Tick(0);

            Assert.That(SinglePlace().VillagerUnitIds, Is.EqualTo(new[] { reachable.Id }));
        }

        // Mutation caught: after its bound foundation dies, a separate Barracks completes the placed goal.
        [Test]
        public void PlacedBuild_DestroyedBoundIdCannotBeHijackedByOtherCompletedBarracks()
        {
            BuildStructureGoal goal = SubmitPlacedBarracks();
            goals.Tick(0);
            simulation.Tick();
            goals.Tick(15);
            int boundId = (int)GetGoalProperty(goal, "PlacedBuildingId");
            BuildingData bound = simulation.BuildingRegistry.GetBuilding(boundId);
            Assert.That(bound, Is.Not.Null);
            bound.CurrentHealth = 0;
            simulation.CreateBuilding(0, BuildingType.Barracks, anchorX + 15, anchorZ);

            goals.Tick(30);

            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            Assert.That(GetGoalProperty(goal, "PlacementBlocker")?.ToString(),
                Is.EqualTo("BoundBuildingUnavailable"));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        // Mutation caught: a new same-owner human building at the exact tile is mistaken for this goal's result.
        [Test]
        public void PlacedBuild_HumanCommandProcessedFirstAtSameTileCannotHijackResultId()
        {
            UnitData humanWorker = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(anchorX + 10, anchorZ + 8),
                Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
            humanWorker.IsVillager = true;
            humanWorker.UnitType = 0;
            humanWorker.MaxHealth = humanWorker.CurrentHealth = 100;
            humanWorker.State = UnitState.Idle;
            simulation.CommandBuffer.EnqueueCommand(new PlaceBuildingCommand(0, BuildingType.Barracks,
                anchorX - 8, anchorZ, new[] { humanWorker.Id }), CommandEnqueueSource.Human);
            BuildStructureGoal goal = SubmitPlacedBarracks();
            goals.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Executing));

            simulation.Tick();
            BuildingData humanResult = simulation.BuildingRegistry.GetAllBuildings().Single(building =>
                building.PlayerId == 0 && building.Type == BuildingType.Barracks
                && building.OriginTileX == anchorX - 8 && building.OriginTileZ == anchorZ);
            goals.Tick(15);

            Assert.That(GetGoalProperty(goal, "PlacedBuildingId"), Is.EqualTo(-1));
            Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed));
            Assert.That(goal.StatusReason, Does.Not.Contain($"building #{humanResult.Id} is advancing"));
        }

        // Mutation caught: an invalid Near gap-one site is described as exact despite the legal gap-two fallback.
        [Test]
        public void PlacedBuild_NearFoundationBorderFallbackExplainsActualGap()
        {
            // TC foundation border occupies the nominal one-clear-tile gap.
            simulation.MapData.Tiles[anchorX - 5, anchorZ + 3] = TileType.Water;
            BuildStructureGoal goal = SubmitPlacedBarracks(relation: "Near", gap: 1);

            goals.Tick(0);

            PlaceBuildingCommand place = SinglePlace();
            Assert.That(place.TileX == anchorX - 4 && place.TileZ == anchorZ, Is.False);
            Assert.That(goal.StatusReason, Does.Contain("fallback").IgnoreCase);
            Assert.That(goal.StatusReason, Does.Contain("2 clear-tile gap"));
        }

        // Mutation caught: direct manager callers can supply incomplete semantic placement and crash planning.
        [Test]
        public void PlacedBuild_DirectManagerSubmissionRejectsIncompletePlacement()
        {
            Assert.Throws<ArgumentException>(() => goals.SubmitBuildStructure(BuildingType.Barracks,
                placementAnchorSelector: CommanderSemanticAnchorSelector.MyTownCenter));
            Assert.That(goals.Goals, Is.Empty);
        }

        private void RevealCandidateArea()
        {
            for (int x = anchorX - 11; x <= anchorX - 4; x++)
                for (int z = anchorZ - 4; z <= anchorZ + 5; z++)
                    simulation.FogOfWar.SetVisible(0, x, z);
        }

        private BuildStructureGoal SubmitPlacedBarracks(int? ordinal = null,
            string relation = "MapWest", int gap = 5)
        {
            string json = "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\","
                + "\"structure\":\"Barracks\",\"count\":1,\"placement\":{\"anchor\":\"MyTownCenter\","
                + (ordinal.HasValue ? "\"ordinal\":" + ordinal.Value + "," : string.Empty)
                + "\"relation\":\"" + relation + "\",\"clearGapTiles\":" + gap + "}}]}";
            CommanderSemanticResult parsed = CommanderSemanticJson.Parse(json);
            Assert.That(parsed.IsValid, Is.True);
            CommanderContext context = new CommanderContextBuilder().Build(simulation, goals);
            bool admitted = CommanderSemanticAdmission.TryCreateTacticalIntent(parsed.Nodes[0], context,
                out CommanderIntent intent, out string reason);
            Assert.That(admitted, Is.True, reason);
            CommanderIntentResolution resolution = new CommanderIntentResolver().Resolve(intent, simulation, goals);
            Assert.That(resolution.CreatedGoal, Is.True, resolution.Reason);
            Assert.That(resolution.Goal, Is.TypeOf<BuildStructureGoal>());
            return (BuildStructureGoal)resolution.Goal;
        }

        private PlaceBuildingCommand SinglePlace()
        {
            ICommand[] commands = simulation.CommandBuffer.FlushCommands().ToArray();
            Assert.That(commands, Has.Length.EqualTo(1));
            Assert.That(commands[0], Is.TypeOf<PlaceBuildingCommand>());
            return (PlaceBuildingCommand)commands[0];
        }

        private static object GetGoalProperty(BuildStructureGoal goal, string name)
        {
            PropertyInfo property = typeof(BuildStructureGoal).GetProperty(name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "Placed-build goals must expose a typed blocker.");
            return property.GetValue(goal);
        }
    }
}
