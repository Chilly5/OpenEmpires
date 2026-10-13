using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5C")]
    public sealed class CommanderPhase5CTargetedConstructionTests
    {
        private const string MillAndFarms = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
            + "{\"id\":\"mill\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Mill\",\"count\":1},\"inputs\":{},\"dependsOn\":[]},"
            + "{\"id\":\"millSite\",\"mechanic\":\"resolve-location\",\"parameters\":{\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{\"structures\":\"mill\"},\"dependsOn\":[\"mill\"]},"
            + "{\"id\":\"farms\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":8},\"inputs\":{\"location\":\"millSite\"},\"dependsOn\":[\"millSite\"]}]}";

        [Test]
        public void MillAndEightFarmsAroundIt_OffersExactNewMillGraphAndNoOrdinaryPartialBuild()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            CommanderGoalManager manager = null;
            try
            {
                var simulation = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                simulation.SetPlayerCivilizations(new[] { Civilization.English });
                manager = new CommanderGoalManager(simulation, 0);
                const string request = "Build 1 mill and 8 farms around it.";
                var context = new CommanderContextBuilder().Build(simulation, manager, request);
                Assert.That(CommanderSemanticRequestPack.TryBuild(
                    new CommanderSemanticProviderRequest(request, context), "openai/gpt-6-luna", 4096,
                    out var pack, out string reason), Is.True, reason);
                string instruction = (string)JObject.Parse(pack.Json)["messages"]?[0]?["content"];
                Assert.That(instruction, Does.Contain("\"structures\":\"mill\""),
                    "The offered graph must connect Farm placement to the newly requested Mill result.");
                Assert.That(instruction, Does.Not.Contain("BuildStructure{structure:canonical"),
                    "The incompatible ordinary placed-count format must not compete for this request.");

                var semantic = CommanderSemanticJson.Parse(MillAndFarms);
                Assert.That(semantic.IsValid, Is.True, semantic.SafeExplanation);
                Assert.That(semantic.Outcome, Is.EqualTo(CommanderSemanticOutcome.DynamicPlan));
                Assert.That(CommanderDynamicCompiler.TryCompile(semantic.DynamicPlan, simulation, manager,
                    out var graph, out reason), Is.True, reason);
                Assert.That(graph.Nodes.Select(node => node.Intent).OfType<BuildStructureIntent>()
                    .Select(intent => (intent.StructureType, intent.Count)),
                    Is.EquivalentTo(new[] { (BuildingType.Mill, 1), (BuildingType.Farm, 8) }));
                Assert.That(graph.Nodes[1].DependsOn, Does.Contain(0),
                    "The Farms cannot start before the exact new Mill has completed.");
                Assert.That(manager.Goals, Is.Empty, "Compilation is not player approval.");
                var candidate = manager.PrepareActionPlan(semantic, request, 1);
                Assert.That(manager.Goals, Is.Empty, "Preview is not player approval.");
                var approved = manager.ApproveActionPlan(candidate, 1);
                Assert.That(approved, Is.Not.Null);
                manager.SubmitSemanticGraph(approved);
                var mill = manager.Goals.OfType<BuildStructureGoal>().Single(goal => goal.StructureType == BuildingType.Mill);
                var farms = manager.Goals.OfType<BuildStructureGoal>().Single(goal => goal.StructureType == BuildingType.Farm);
                Assert.That(farms.Count, Is.EqualTo(8));
                Assert.That(farms.Dependencies, Does.Contain(mill));
                Assert.That(farms.DynamicLocation, Is.Not.Null);
                Assert.That(mill.HasResultConsumer, Is.True,
                    "The exact new Mill result must remain available to its Farm placement consumer.");
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void MillAndEightFarmsWithoutAround_UsesTwoCompleteOrdinaryBuildNodes()
        {
            const string request = "Build a mill and 8 farms.";
            var context = CommanderEconomyClarificationTests.Context();
            Assert.That(CommanderSemanticRequestPack.TryBuild(
                new CommanderSemanticProviderRequest(request, context), "openai/gpt-6-luna", 4096,
                out var pack, out string reason), Is.True, reason);
            string instruction = (string)JObject.Parse(pack.Json)["messages"]?[0]?["content"];
            Assert.That(instruction, Does.Contain("BuildStructure"));
            Assert.That(instruction, Does.Not.Contain("\"structures\":\"mill\""));

            var parsed = CommanderSemanticResult.ParseTrusted("{\"outcome\":\"Request\",\"executionOrder\":\"Independent\",\"nodes\":["
                + "{\"type\":\"BuildStructure\",\"structure\":\"Mill\",\"count\":1,\"builders\":{\"state\":\"Eligible\",\"count\":1}},"
                + "{\"type\":\"BuildStructure\",\"structure\":\"Farm\",\"count\":8,\"builders\":{\"state\":\"Eligible\",\"count\":1}}]}", true);
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            Assert.That(parsed.Nodes.Select(node => (node.BuildingType, node.Count)),
                Is.EquivalentTo(new[] { ((BuildingType?)BuildingType.Mill, (int?)1),
                    ((BuildingType?)BuildingType.Farm, (int?)8) }));
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            CommanderGoalManager manager = null;
            try
            {
                var simulation = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                simulation.SetPlayerCivilizations(new[] { Civilization.English });
                manager = new CommanderGoalManager(simulation, 0);
                Assert.That(manager.TryCompileActionPlan(parsed, out var graph, out reason), Is.True, reason);
                Assert.That(graph.Nodes.Select(node => ((BuildStructureIntent)node.Intent).Count),
                    Is.EquivalentTo(new[] { 1, 8 }));
                Assert.That(manager.Goals, Is.Empty, "The ordinary two-build candidate still requires approval.");
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void RepeatedPlacedFarms_DoNotCompleteAfterOnlyOneNativeFarm()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            CommanderGoalManager manager = null;
            try
            {
                var simulation = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                simulation.SetPlayerCivilizations(new[] { Civilization.English });
                var center = simulation.MapData.BasePositions[0];
                var townCenter = simulation.CreateBuilding(0, BuildingType.TownCenter,
                    center.x, center.y, false, true);
                townCenter.AutoProduceVillagers = false;
                for (int x = 0; x < simulation.MapData.Width; x++)
                    for (int z = 0; z < simulation.MapData.Height; z++)
                        simulation.FogOfWar.SetVisible(0, x, z);
                var worker = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(center.x + 5, center.y + 2),
                    Fixed32.One, Fixed32.One, Fixed32.One);
                worker.IsVillager = true;
                worker.UnitType = 0;
                worker.MaxHealth = worker.CurrentHealth = 100;
                worker.State = UnitState.Idle;
                simulation.ResourceManager.AddResource(0, ResourceType.Wood, 2000);
                manager = new CommanderGoalManager(simulation, 0);
                var plan = CommanderSemanticJson.Parse("{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                    + "{\"id\":\"site\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"MyTownCenter\",\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{},\"dependsOn\":[]},"
                    + "{\"id\":\"farms\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":8},\"inputs\":{\"location\":\"site\"},\"dependsOn\":[\"site\"]}]}");
                var candidate = manager.PrepareActionPlan(plan, "Build eight Farms near my Town Center", 1);
                var approved = manager.ApproveActionPlan(candidate, 1);
                Assert.That(approved, Is.Not.Null);
                manager.SubmitSemanticGraph(approved);
                var farms = manager.Goals.OfType<BuildStructureGoal>().Single();
                int xTile = center.x + townCenter.TileFootprintWidth + 1;
                int zTile = center.y;
                var first = simulation.CreateBuilding(0, BuildingType.Farm, xTile, zTile, false);
                farms.AttributedBuildingIds.Add(first.Id);
                farms.PlacedBuildingId = first.Id;
                farms.PlacedTileX = xTile;
                farms.PlacedTileZ = zTile;

                manager.Tick(simulation.CurrentTick);

                Assert.That(farms.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed),
                    "One of eight newly requested Farms is not full construction completion.");
                Assert.That(farms.AttributedBuildingIds, Has.Count.EqualTo(1));
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void NewlyBuiltMill_ProducesEightDistinctCompletedNearbyFarms()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            CommanderGoalManager manager = null;
            try
            {
                var simulation = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                simulation.SetPlayerCivilizations(new[] { Civilization.English });
                var center = simulation.MapData.BasePositions[0];
                var townCenter = simulation.CreateBuilding(0, BuildingType.TownCenter,
                    center.x, center.y, false, true);
                townCenter.AutoProduceVillagers = false;
                for (int x = 0; x < simulation.MapData.Width; x++)
                    for (int z = 0; z < simulation.MapData.Height; z++)
                        simulation.FogOfWar.SetVisible(0, x, z);
                var worker = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(center.x + 5, center.y + 2),
                    Fixed32.One, Fixed32.One, Fixed32.One);
                worker.IsVillager = true;
                worker.UnitType = 0;
                worker.MaxHealth = worker.CurrentHealth = 100;
                worker.State = UnitState.Idle;
                simulation.ResourceManager.AddResource(0, ResourceType.Wood, 2000);
                manager = new CommanderGoalManager(simulation, 0);
                var semantic = CommanderSemanticJson.Parse("{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                    + "{\"id\":\"millSite\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"MyTownCenter\",\"relation\":\"MapEast\",\"clearGapTiles\":10},\"inputs\":{},\"dependsOn\":[]},"
                    + "{\"id\":\"mill\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Mill\",\"count\":1},\"inputs\":{\"location\":\"millSite\"},\"dependsOn\":[\"millSite\"]},"
                    + "{\"id\":\"farmSite\",\"mechanic\":\"resolve-location\",\"parameters\":{\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{\"structures\":\"mill\"},\"dependsOn\":[\"mill\"]},"
                    + "{\"id\":\"farms\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":8},\"inputs\":{\"location\":\"farmSite\"},\"dependsOn\":[\"farmSite\"]}]}");
                var candidate = manager.PrepareActionPlan(semantic, "Build a Mill and eight Farms around it", 1);
                Assert.That(manager.Goals, Is.Empty);
                var approved = manager.ApproveActionPlan(candidate, 1);
                Assert.That(approved, Is.Not.Null);
                manager.SubmitSemanticGraph(approved);
                var millGoal = manager.Goals.OfType<BuildStructureGoal>().Single(goal => goal.StructureType == BuildingType.Mill);
                var farmGoal = manager.Goals.OfType<BuildStructureGoal>().Single(goal => goal.StructureType == BuildingType.Farm);
                for (int tick = 0; tick < 6500 && !farmGoal.IsTerminal; tick++)
                {
                    manager.Tick(simulation.CurrentTick);
                    simulation.Tick();
                }
                Assert.That(millGoal.Status, Is.EqualTo(CommanderGoalStatus.Completed), millGoal.StatusReason);
                Assert.That(farmGoal.Status, Is.EqualTo(CommanderGoalStatus.Completed), farmGoal.StatusReason);
                var mill = simulation.BuildingRegistry.GetBuilding(millGoal.ResultBuildingIds.Single());
                var farms = farmGoal.AttributedBuildingIds.Select(simulation.BuildingRegistry.GetBuilding).ToArray();
                Assert.That(farms, Has.Length.EqualTo(8));
                Assert.That(farms.Select(farm => farm.Id).Distinct().Count(), Is.EqualTo(8));
                foreach (var farm in farms)
                {
                    Assert.That(farm.IsUnderConstruction, Is.False);
                    Assert.That(farm.PlayerId, Is.EqualTo(0));
                    Assert.That(Math.Abs(farm.OriginTileX - mill.OriginTileX), Is.LessThanOrEqualTo(7));
                    Assert.That(Math.Abs(farm.OriginTileZ - mill.OriginTileZ), Is.LessThanOrEqualTo(7));
                }
                for (int i = 0; i < farms.Length; i++)
                    for (int j = i + 1; j < farms.Length; j++)
                        Assert.That(farms[i].OriginTileX + farms[i].TileFootprintWidth <= farms[j].OriginTileX
                            || farms[j].OriginTileX + farms[j].TileFootprintWidth <= farms[i].OriginTileX
                            || farms[i].OriginTileZ + farms[i].TileFootprintHeight <= farms[j].OriginTileZ
                            || farms[j].OriginTileZ + farms[j].TileFootprintHeight <= farms[i].OriginTileZ,
                            Is.True, "Completed Farm footprints must not overlap.");
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }
    }
}
