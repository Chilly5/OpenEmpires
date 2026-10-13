using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5C")]
    public sealed class CommanderPhase5CComposableLocationTests
    {
        private static string Plan(string selection) => "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
            + selection + ","
            + "{\"id\":\"site\",\"mechanic\":\"resolve-location\",\"parameters\":{\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{\"resources\":\"wood\"},\"dependsOn\":[\"wood\"]},"
            + "{\"id\":\"yard\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:LumberYard\",\"count\":1},\"inputs\":{\"location\":\"site\"},\"dependsOn\":[\"site\"]}]}";

        private const string WoodSelection = "{\"id\":\"wood\",\"mechanic\":\"select-resources\",\"parameters\":{\"resource\":\"Wood\",\"sourceKind\":\"Tree\",\"mode\":\"Visible\",\"count\":1},\"inputs\":{},\"dependsOn\":[]}";

        [Test]
        public void DirectionalResourceSelection_AcceptsTypedTownCenterFilter()
        {
            string selected = WoodSelection.Replace("\"count\":1", "\"count\":1,\"anchor\":\"MyTownCenter\",\"relation\":\"MapEast\"");
            Assert.That(CommanderSemanticJson.Parse(Plan(selected)).IsValid, Is.True);
        }

        [Test]
        public void PlayerMarker_IsSymbolicLocationAnchorWithoutProviderCoordinates()
        {
            const string json = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + "{\"id\":\"site\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"PlayerMarker\",\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{},\"dependsOn\":[]},"
                + "{\"id\":\"yard\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:LumberYard\",\"count\":1},\"inputs\":{\"location\":\"site\"},\"dependsOn\":[\"site\"]}]}";
            Assert.That(CommanderSemanticJson.Parse(json).IsValid, Is.True);
            Assert.That(CommanderSemanticJson.Parse(json.Replace("\"clearGapTiles\":1",
                "\"clearGapTiles\":1,\"tileX\":42")).IsValid, Is.False);
        }

        [Test]
        public void DirectionalResourceSelection_RejectsDirectionWithoutAnchor()
        {
            string selected = WoodSelection.Replace("\"count\":1", "\"count\":1,\"relation\":\"MapEast\"");
            Assert.That(CommanderSemanticJson.Parse(Plan(selected)).IsValid, Is.False);
        }

        [Test]
        public void RightOfTownCenter_FiltersLeftTreeBeforeChoosingNearest()
        {
            using var f = new Fixture();
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            f.Sim.MapData.AddResourceNode(ResourceType.Wood,
                f.Sim.MapData.TileToWorldFixed(x - 9, z), 1000);
            var right = f.Sim.MapData.AddResourceNode(ResourceType.Wood,
                f.Sim.MapData.TileToWorldFixed(x + 12, z), 1000);
            string selected = WoodSelection.Replace("\"count\":1", "\"count\":1,\"anchor\":\"MyTownCenter\",\"relation\":\"MapEast\"");
            var candidate = f.Manager.PrepareActionPlan(CommanderSemanticJson.Parse(Plan(selected)),
                "Lumber Yard near the woodline right of my Town Center", 1);
            var goal = (BuildStructureGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            Assert.That(goal.DynamicLocation.TryResolve(f.Sim, 0, out int selectedX, out int selectedZ,
                out _, out _), Is.True);
            Assert.That((selectedX, selectedZ), Is.EqualTo((right.TileX, right.TileZ)));
        }

        [Test]
        public void TopWoodline_UsesSelectedActionUnitsAsDirectionalAnchor()
        {
            const string units = "{\"id\":\"units\",\"mechanic\":\"select-units\",\"parameters\":{\"kind\":\"Spearman\",\"count\":1},\"inputs\":{},\"dependsOn\":[]}";
            string selected = WoodSelection.Replace("\"count\":1", "\"count\":1,\"relation\":\"MapNorth\"")
                .Replace("\"inputs\":{},\"dependsOn\":[]", "\"inputs\":{\"units\":\"units\"},\"dependsOn\":[\"units\"]");
            string json = Plan(selected).Replace("\"nodes\":[", "\"nodes\":[" + units + ",");
            Assert.That(CommanderSemanticJson.Parse(json).IsValid, Is.True);
        }

        [Test]
        public void TopWoodline_SpearmenMoveBindsVisibleNorthernResource()
        {
            using var f = new Fixture();
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var south = f.Sim.MapData.AddResourceNode(ResourceType.Wood,
                f.Sim.MapData.TileToWorldFixed(x + 4, z + 2), 1000);
            var north = f.Sim.MapData.AddResourceNode(ResourceType.Wood,
                f.Sim.MapData.TileToWorldFixed(x + 4, z + 12), 1000);
            var spear = f.Sim.UnitRegistry.CreateUnit(0,
                f.Sim.MapData.TileToWorldFixed(x + 2, z + 5), Fixed32.One, Fixed32.One, Fixed32.One);
            spear.UnitType = f.Sim.ResolveCivUnitType(0, 1);
            spear.CurrentHealth = spear.MaxHealth = 100;
            const string json = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + "{\"id\":\"units\",\"mechanic\":\"select-units\",\"parameters\":{\"kind\":\"Spearman\",\"count\":1},\"inputs\":{},\"dependsOn\":[]},"
                + "{\"id\":\"wood\",\"mechanic\":\"select-resources\",\"parameters\":{\"resource\":\"Wood\",\"sourceKind\":\"Tree\",\"mode\":\"Visible\",\"count\":1,\"relation\":\"MapNorth\"},\"inputs\":{\"units\":\"units\"},\"dependsOn\":[\"units\"]},"
                + "{\"id\":\"site\",\"mechanic\":\"resolve-location\",\"parameters\":{\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{\"resources\":\"wood\"},\"dependsOn\":[\"wood\"]},"
                + "{\"id\":\"move\",\"mechanic\":\"move-units\",\"parameters\":{},\"inputs\":{\"units\":\"units\",\"location\":\"site\"},\"dependsOn\":[\"units\",\"site\"]}]}";
            var parsed = CommanderSemanticJson.Parse(json);
            Assert.That(parsed.IsValid, Is.True);
            var candidate = f.Manager.PrepareActionPlan(parsed, "Move my Spearmen to the top woodline", 1);
            var move = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(
                f.Manager.ApproveActionPlan(candidate, 1))[0];
            Assert.That(move.DynamicMoveLocation, Is.Not.Null);
            Assert.That(move.DynamicMoveLocation.TryResolve(f.Sim, 0,
                out int targetX, out int targetZ, out _, out _), Is.True);
            Assert.That((targetX, targetZ), Is.EqualTo((north.TileX, north.TileZ)));
            Assert.That((targetX, targetZ), Is.Not.EqualTo((south.TileX, south.TileZ)));
            f.Manager.Tick(0);
            var issued = f.Sim.CommandBuffer.FlushCommands();
            Assert.That(issued.OfType<GatherCommand>(), Is.Empty);
            var command = issued.OfType<MoveCommand>().Single();
            Assert.That(command.UnitIds, Is.EquivalentTo(new[] { spear.Id }));
            for (int tick = 1; tick < 1500 && !move.IsTerminal; tick++)
            {
                f.Sim.Tick(tick == 1 ? issued.ToList() : new System.Collections.Generic.List<ICommand>());
                f.Manager.Tick((tick + 1) * 15);
            }
            Assert.That(move.Status, Is.EqualTo(CommanderGoalStatus.Completed), move.StatusReason);
            Assert.That(move.NativeMovementProcessed.All(done => done), Is.True);
        }

        [Test]
        public void AllSpearmen_SelectionRemainsCompleteWithoutProviderCount()
        {
            using var f = new Fixture();
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            f.Sim.MapData.AddResourceNode(ResourceType.Wood,
                f.Sim.MapData.TileToWorldFixed(x + 4, z + 12), 1000);
            for (int i = 0; i < 2; i++)
            {
                var spear = f.Sim.UnitRegistry.CreateUnit(0,
                    f.Sim.MapData.TileToWorldFixed(x + 2 + i, z + 5), Fixed32.One, Fixed32.One, Fixed32.One);
                spear.UnitType = f.Sim.ResolveCivUnitType(0, 1);
                spear.CurrentHealth = spear.MaxHealth = 100;
            }
            const string json = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + "{\"id\":\"units\",\"mechanic\":\"select-units\",\"parameters\":{\"kind\":\"Spearman\",\"countMode\":\"AllMatching\"},\"inputs\":{},\"dependsOn\":[]},"
                + "{\"id\":\"wood\",\"mechanic\":\"select-resources\",\"parameters\":{\"resource\":\"Wood\",\"sourceKind\":\"Tree\",\"mode\":\"Visible\",\"count\":1,\"relation\":\"MapNorth\"},\"inputs\":{\"units\":\"units\"},\"dependsOn\":[\"units\"]},"
                + "{\"id\":\"site\",\"mechanic\":\"resolve-location\",\"parameters\":{\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{\"resources\":\"wood\"},\"dependsOn\":[\"wood\"]},"
                + "{\"id\":\"move\",\"mechanic\":\"move-units\",\"parameters\":{},\"inputs\":{\"units\":\"units\",\"location\":\"site\"},\"dependsOn\":[\"units\",\"site\"]}]}";
            var parsed = CommanderSemanticJson.Parse(json);
            Assert.That(parsed.IsValid, Is.True);
            var candidate = f.Manager.PrepareActionPlan(parsed, "Move my Spearmen to the top woodline", 1);
            var move = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(
                f.Manager.ApproveActionPlan(candidate, 1))[0];
            Assert.That(move.DynamicMoveUnitIds, Has.Count.EqualTo(2));
        }

        [Test]
        public void MoveWorkers_AcceptsResourceSelectedRelativeToTheirCentroid()
        {
            using var f = new Fixture();
            string workers = "{\"id\":\"workers\",\"mechanic\":\"select-workers\",\"parameters\":{\"state\":\"Idle\",\"count\":2},\"inputs\":{},\"dependsOn\":[]}";
            string selected = WoodSelection.Replace("\"count\":1", "\"count\":1,\"relation\":\"MapNorth\"")
                .Replace("\"inputs\":{},\"dependsOn\":[]", "\"inputs\":{\"workers\":\"workers\"},\"dependsOn\":[\"workers\"]");
            string json = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[" + workers + "," + selected + ","
                + "{\"id\":\"site\",\"mechanic\":\"resolve-location\",\"parameters\":{\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{\"resources\":\"wood\"},\"dependsOn\":[\"wood\"]},"
                + "{\"id\":\"move\",\"mechanic\":\"move-workers\",\"parameters\":{},\"inputs\":{\"workers\":\"workers\",\"location\":\"site\"},\"dependsOn\":[\"workers\",\"site\"]}]}";
            var parsed = CommanderSemanticJson.Parse(json);
            Assert.That(parsed.IsValid, Is.True);
            Assert.DoesNotThrow(() => f.Manager.PrepareActionPlan(parsed, "Move my villagers to the top woodline", 1));
        }

        [Test]
        public void UnspecifiedWoodline_PrefersMostOwnedGatheringAssignmentsOverLowerId()
        {
            using var f = new Fixture();
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var unworked = f.Sim.MapData.AddResourceNode(ResourceType.Wood,
                f.Sim.MapData.TileToWorldFixed(x - 10, z), 1000);
            var worked = f.Sim.MapData.AddResourceNode(ResourceType.Wood,
                f.Sim.MapData.TileToWorldFixed(x + 10, z), 1000);
            Assert.That(unworked.Id, Is.LessThan(worked.Id));
            foreach (var worker in f.Workers)
            {
                worker.State = UnitState.Gathering;
                worker.TargetResourceNodeId = worked.Id;
                worker.GatherSourceKind = ResourceSourceKind.Tree;
            }
            var candidate = f.Manager.PrepareActionPlan(CommanderSemanticJson.Parse(Plan(WoodSelection)),
                "Build a Lumber Yard near the woodline", 1);
            var goal = (BuildStructureGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            Assert.That(goal.DynamicLocation.TryResolve(f.Sim, 0, out int selectedX, out int selectedZ,
                out _, out _), Is.True);
            Assert.That((selectedX, selectedZ), Is.EqualTo((worked.TileX, worked.TileZ)));
        }

        [Test]
        public void UnspecifiedWoodline_RanksClusterWorkerTotalBeforeNearerSingleTree()
        {
            using var f = new Fixture();
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var near = f.Sim.MapData.AddResourceNode(ResourceType.Wood,
                f.Sim.MapData.TileToWorldFixed(x - 7, z), 1000);
            var groupA = f.Sim.MapData.AddResourceNode(ResourceType.Wood,
                f.Sim.MapData.TileToWorldFixed(x + 12, z), 1000);
            var groupB = f.Sim.MapData.AddResourceNode(ResourceType.Wood,
                f.Sim.MapData.TileToWorldFixed(x + 13, z), 1000);
            int[] targetIds = { near.Id, groupA.Id, groupB.Id };
            var third = f.Sim.UnitRegistry.CreateUnit(0,
                f.Sim.MapData.TileToWorldFixed(x - 6, z), Fixed32.One, Fixed32.One, Fixed32.One);
            third.IsVillager = true;
            third.CurrentHealth = third.MaxHealth = 100;
            var actors = f.Workers.Concat(new[] { third }).ToArray();
            for (int i = 0; i < actors.Length; i++)
            {
                actors[i].State = UnitState.Gathering;
                actors[i].TargetResourceNodeId = targetIds[i];
                actors[i].GatherSourceKind = ResourceSourceKind.Tree;
            }
            var candidate = f.Manager.PrepareActionPlan(CommanderSemanticJson.Parse(Plan(WoodSelection)),
                "Build a Lumber Yard near the woodline", 1);
            var goal = (BuildStructureGoal)f.Manager.SubmitSemanticGraph(
                f.Manager.ApproveActionPlan(candidate, 1))[0];
            Assert.That(goal.DynamicLocation.TryResolve(f.Sim, 0, out int selectedX, out int selectedZ,
                out _, out _), Is.True);
            Assert.That((selectedX, selectedZ), Is.EqualTo((groupA.TileX, groupA.TileZ))
                .Or.EqualTo((groupB.TileX, groupB.TileZ)));
        }

        [Test]
        public void WorkedWoodline_DoesNotFallBackToUnworkedTree()
        {
            using var f = new Fixture();
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            f.Sim.MapData.AddResourceNode(ResourceType.Wood,
                f.Sim.MapData.TileToWorldFixed(x + 10, z), 1000);
            string selected = WoodSelection.Replace("\"Visible\"", "\"Worked\"");
            var candidate = f.Manager.PrepareActionPlan(CommanderSemanticJson.Parse(Plan(selected)),
                "Build near the woodline my villagers are working", 1);
            Assert.Throws<ArgumentException>(() =>
                f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1)));
            Assert.That(f.Manager.Goals, Is.Empty);
        }

        [Test]
        public void UnspecifiedUnworkedWoodline_AsksForLocationInsteadOfGuessing()
        {
            using var f = new Fixture();
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            f.Sim.MapData.AddResourceNode(ResourceType.Wood,
                f.Sim.MapData.TileToWorldFixed(x + 10, z), 1000);
            var candidate = f.Manager.PrepareActionPlan(CommanderSemanticJson.Parse(Plan(WoodSelection)),
                "Build a Lumber Yard near the woodline", 1);
            var error = Assert.Throws<ArgumentException>(() =>
                f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1)));
            Assert.That(error.Message, Does.Contain("Place a marker"));
            Assert.That(f.Manager.Goals, Is.Empty);
        }

        [Test]
        public void OrdinaryLumberYardDefault_UsesWorkedClusterInsteadOfTownCenter()
        {
            using var f = new Fixture();
            int x = f.Sim.MapData.Width/2, z = f.Sim.MapData.Height/2;
            var unworked = f.Sim.MapData.AddResourceNode(ResourceType.Wood,
                f.Sim.MapData.TileToWorldFixed(x-12,z),1000);
            var worked = f.Sim.MapData.AddResourceNode(ResourceType.Wood,
                f.Sim.MapData.TileToWorldFixed(x+20,z+10),1000);
            foreach (var worker in f.Workers)
            {
                worker.State=UnitState.Gathering;
                worker.TargetResourceNodeId=worked.Id;
                worker.GatherSourceKind=ResourceSourceKind.Tree;
            }
            f.Sim.ResourceManager.AddResource(0,ResourceType.Wood,1000);
            var goal=f.Manager.SubmitBuildStructure(BuildingType.LumberYard,1);
            var plan=new CommanderPlanner(f.Sim,new CommanderWorkerAuthority(f.Sim,0)).Plan(goal,0);
            Assert.That(plan.Command,Is.TypeOf<PlaceBuildingCommand>(),plan.Reason);
            var place=(PlaceBuildingCommand)plan.Command;
            Assert.That(Math.Abs(place.TileX-worked.TileX),Is.LessThanOrEqualTo(5));
            Assert.That(Math.Abs(place.TileZ-worked.TileZ),Is.LessThanOrEqualTo(5));
            Assert.That(Math.Abs(place.TileX-unworked.TileX),Is.GreaterThan(5));
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly SimulationConfig Config;
            internal readonly GameSimulation Sim;
            internal readonly CommanderGoalManager Manager;
            internal readonly UnitData[] Workers;

            internal Fixture()
            {
                Config = ScriptableObject.CreateInstance<SimulationConfig>();
                Sim = new GameSimulation(Config, 1, new[] { 0 }, Array.Empty<int>());
                int x = Sim.MapData.Width / 2, z = Sim.MapData.Height / 2;
                for (int tx = x - 25; tx <= x + 25; tx++)
                    for (int tz = z - 20; tz <= z + 20; tz++)
                    {
                        Sim.MapData.Tiles[tx, tz] = TileType.Grass;
                        Sim.MapData.ForestDensity[tx, tz] = 0;
                        Sim.MapData.FoundationCount[tx, tz] = 0;
                        Sim.FogOfWar.SetVisible(0, tx, tz);
                    }
                Sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true).AutoProduceVillagers = false;
                Workers = Enumerable.Range(0, 2).Select(i =>
                {
                    var worker = Sim.UnitRegistry.CreateUnit(0, Sim.MapData.TileToWorldFixed(x - 4 - i, z),
                        Fixed32.One, Fixed32.One, Fixed32.One);
                    worker.IsVillager = true;
                    worker.CurrentHealth = worker.MaxHealth = 100;
                    worker.State = UnitState.Idle;
                    return worker;
                }).ToArray();
                Manager = new CommanderGoalManager(Sim, 0);
            }

            public void Dispose() { Manager.Dispose(); UnityEngine.Object.DestroyImmediate(Config); }
        }
    }
}
