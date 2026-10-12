using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5A")]
    public sealed class CommanderPhase5ADynamicRuntimeTests
    {
        [Test]
        public void AllCurrentWoodWorkers_GatherOnlyVisibleTreesWestOfOwnedTownCenter()
        {
            using var f = new Fixture(4);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var oldWood = f.Sim.MapData.AddResourceNode(ResourceType.Wood,
                f.Sim.MapData.TileToWorldFixed(x + 12, z), 1000);
            var westernTree = f.Sim.MapData.AddResourceNode(ResourceType.Wood,
                f.Sim.MapData.TileToWorldFixed(x - 14, z + 4), 1000);
            f.Sim.MapData.AddResourceNode(ResourceType.Wood,
                f.Sim.MapData.TileToWorldFixed(x + 14, z + 4), 1000);
            for (int i = 0; i < 3; i++)
            {
                f.Workers[i].State = UnitState.Gathering;
                f.Workers[i].TargetResourceNodeId = oldWood.Id;
            }
            var parsed = CommanderSemanticJson.Parse(AllWoodWestPlan);
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var candidate = f.Manager.PrepareActionPlan(parsed, "gather with all current Wood workers at trees west of my TC", 1);
            var goal = (AllocateWorkersGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            Assert.That(goal.FrozenWorkerIds, Is.EquivalentTo(f.Workers.Take(3).Select(w => w.Id)));
            f.Manager.Tick(0);
            var commands = f.Sim.CommandBuffer.FlushCommands().OfType<GatherCommand>().ToArray();
            Assert.That(commands.SelectMany(c => c.UnitIds), Is.EquivalentTo(f.Workers.Take(3).Select(w => w.Id)));
            Assert.That(commands.All(c => c.ResourceNodeId == westernTree.Id && c.SourceKind == ResourceSourceKind.Tree), Is.True);
            f.Manager.Tick(15); // Issued commands have not yet entered native simulation.
            Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed),
                "Their pre-existing east-side Wood assignments cannot satisfy the west-side request.");
            f.Sim.Tick(commands.Cast<ICommand>().ToList());
            Assert.That(f.Workers.Take(3).All(w => w.TargetResourceNodeId == westernTree.Id), Is.True,
                "Normal native Gather processing must retain the selected western Tree.");
        }

        private const string AllWoodWestPlan = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
            + "{\"id\":\"all\",\"mechanic\":\"select-workers\",\"parameters\":{\"state\":\"Gathering\",\"currentResource\":\"Wood\",\"countMode\":\"AllMatching\"},\"inputs\":{},\"dependsOn\":[]},"
            + "{\"id\":\"woodline\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":1,\"resource\":\"Wood\",\"sourceKind\":\"Tree\"},\"inputs\":{},\"dependsOn\":[]},"
            + "{\"id\":\"gather\",\"mechanic\":\"allocate-workers\",\"parameters\":{\"resource\":\"Wood\",\"sourceKind\":\"Tree\"},\"inputs\":{\"workers\":\"all\",\"location\":\"woodline\"},\"dependsOn\":[\"all\",\"woodline\"]}]}";

        [Test]
        public void AllCurrentWoodWorkers_ExplicitMoveToWestTownCenterWoodlineUsesNativeMove()
        {
            using var f = new Fixture(4);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var oldWood = f.Sim.MapData.AddResourceNode(ResourceType.Wood,
                f.Sim.MapData.TileToWorldFixed(x + 12, z), 1000);
            f.Sim.MapData.AddResourceNode(ResourceType.Wood,
                f.Sim.MapData.TileToWorldFixed(x - 14, z + 4), 1000);
            for (int i = 0; i < 3; i++)
            {
                f.Workers[i].State = UnitState.Gathering;
                f.Workers[i].TargetResourceNodeId = oldWood.Id;
            }
            var parsed = CommanderSemanticJson.Parse(AllWoodWestMovePlan);
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var candidate = f.Manager.PrepareActionPlan(parsed, "move all current Wood villagers to the woodline west of my Town Center", 1);
            Assert.That(candidate.Preview, Does.Contain("Move"));
            Assert.That(candidate.Preview, Does.Contain("all").IgnoreCase);
            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            Assert.That(goal.FrozenWorkerIds, Is.EquivalentTo(f.Workers.Take(3).Select(w => w.Id)));
            f.Manager.Tick(0);
            var issued = f.Sim.CommandBuffer.FlushCommands();
            Assert.That(issued.OfType<GatherCommand>(), Is.Empty);
            var move = issued.OfType<MoveCommand>().Single();
            Assert.That(move.UnitIds,
                Is.EquivalentTo(f.Workers.Take(3).Select(w => w.Id)));
            Assert.That(move.FormationPositions.Length, Is.EqualTo(3));
            Assert.That(move.FormationPositions.All(p => f.Sim.MapData.WorldToTile(p).x < x), Is.True);
            f.Manager.Tick(15);
            Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed),
                "A dispatched Move is not native arrival proof.");
            for (int tick = 1; tick < 1500 && goal.Status != CommanderGoalStatus.Completed
                && goal.Status != CommanderGoalStatus.Failed; tick++)
            {
                f.Sim.Tick(tick == 1 ? issued.ToList() : new System.Collections.Generic.List<ICommand>());
                f.Manager.Tick((tick + 1) * 15);
            }
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
            Assert.That(f.Workers.Take(3).All(w => f.Sim.MapData.WorldToTile(w.SimPosition).x < x), Is.True);
            Assert.That(goal.NativeMovementProcessed.All(done => done), Is.True,
                "The existing native movement receipt must process all frozen subjects.");
        }

        private const string AllWoodWestMovePlan = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
            + "{\"id\":\"all\",\"mechanic\":\"select-workers\",\"parameters\":{\"state\":\"Gathering\",\"currentResource\":\"Wood\",\"countMode\":\"AllMatching\"},\"inputs\":{},\"dependsOn\":[]},"
            + "{\"id\":\"woodline\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":1,\"resource\":\"Wood\",\"sourceKind\":\"Tree\"},\"inputs\":{},\"dependsOn\":[]},"
            + "{\"id\":\"move\",\"mechanic\":\"move-workers\",\"parameters\":{},\"inputs\":{\"workers\":\"all\",\"location\":\"woodline\"},\"dependsOn\":[\"all\",\"woodline\"]}]}";

        [Test]
        public void CurrentWoodMove_AllOverBoundSnapshotRejectsBeforeAnyGoalOrCommand()
        {
            using var f = new Fixture(1);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var oldWood = f.Sim.MapData.AddResourceNode(ResourceType.Wood,
                f.Sim.MapData.TileToWorldFixed(x + 12, z), 1000);
            f.Sim.MapData.AddResourceNode(ResourceType.Wood,
                f.Sim.MapData.TileToWorldFixed(x - 14, z + 4), 1000);
            f.Workers[0].State = UnitState.Gathering;
            f.Workers[0].TargetResourceNodeId = oldWood.Id;
            for (int i = 0; i < 200; i++)
            {
                var worker = f.Sim.UnitRegistry.CreateUnit(0, f.Workers[0].SimPosition,
                    Fixed32.One, Fixed32.One, Fixed32.One);
                worker.IsVillager = true;
                worker.CurrentHealth = worker.MaxHealth = 100;
                worker.State = UnitState.Gathering;
                worker.TargetResourceNodeId = oldWood.Id;
            }
            var parsed = CommanderSemanticJson.Parse(AllWoodWestMovePlan);
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var candidate = f.Manager.PrepareActionPlan(parsed, "move all current Wood villagers west of my TC", 1);
            Assert.Throws<InvalidOperationException>(() => f.Manager.SubmitSemanticGraph(
                f.Manager.ApproveActionPlan(candidate, 1)));
            Assert.That(f.Manager.Goals, Is.Empty);
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void DiscoveryEnemyTownCenter_IssuesOnlyKnownFrontierScoutMove()
        {
            using var f = new Fixture(1, twoPlayers: true);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var scout = f.Sim.UnitRegistry.CreateUnit(0, f.Sim.MapData.TileToWorldFixed(x - 1, z),
                Fixed32.One, Fixed32.One, Fixed32.One);
            scout.UnitType = 4;
            scout.CurrentHealth = scout.MaxHealth = 100;
            scout.State = UnitState.Idle;
            var hidden = f.Sim.CreateBuilding(1, BuildingType.TownCenter, x + 40, z, false, true);
            Assert.That(f.Sim.FogOfWar.GetVisibility(0, hidden.OriginTileX, hidden.OriginTileZ),
                Is.EqualTo(TileVisibility.Unexplored));
            var parsed = CommanderSemanticJson.Parse(DiscoverEnemyTownCenter);
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var candidate = f.Manager.PrepareActionPlan(parsed, "find the enemy Town Center with my scout", 1);
            Assert.That(candidate.Preview, Does.Contain("finite").IgnoreCase);
            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            f.Manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Executing), goal.StatusReason);
            var move = f.Sim.CommandBuffer.FlushCommands().OfType<MoveCommand>().Single();
            Assert.That(move.UnitIds, Is.EqualTo(new[] { scout.Id }));
            var waypoint = f.Sim.MapData.WorldToTile(move.TargetPosition);
            Assert.That(f.Sim.FogOfWar.GetVisibility(0, waypoint.x, waypoint.y),
                Is.Not.EqualTo(TileVisibility.Unexplored));
            Assert.That(waypoint.x, Is.LessThan(hidden.OriginTileX));
        }

        private const string DiscoverEnemyTownCenter = "{\"outcome\":\"Request\",\"nodes\":["
            + "{\"type\":\"DiscoverEnemy\",\"unitSelector\":\"Scout\",\"count\":1,"
            + "\"location\":\"VisibleEnemy\",\"target\":{\"kind\":\"BuildingType\",\"structure\":\"TownCenter\"}}]}";

        [Test]
        public void Discovery_VisibleEnemyCompletesOnlyOnLegitimateVisibleTarget()
        {
            using var f = new Fixture(1, twoPlayers: true);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var scout = f.Sim.UnitRegistry.CreateUnit(0, f.Sim.MapData.TileToWorldFixed(x - 1, z),
                Fixed32.One, Fixed32.One, Fixed32.One);
            scout.UnitType = 4; scout.CurrentHealth = scout.MaxHealth = 100;
            var enemy = f.Sim.CreateBuilding(1, BuildingType.TownCenter, x + 40, z, false, true);
            var parsed = CommanderSemanticJson.Parse(DiscoverEnemyTownCenter);
            var candidate = f.Manager.PrepareActionPlan(parsed, "find the enemy Town Center", 1);
            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            f.Manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Executing));
            Assert.That(f.Sim.CommandBuffer.FlushCommands().OfType<MoveCommand>().Count(), Is.EqualTo(1));
            f.Sim.FogOfWar.SetVisible(0, enemy.OriginTileX, enemy.OriginTileZ);
            f.Manager.Tick(15);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void Discovery_NoFrontierFailsWithoutInventingEnemyOrCommand()
        {
            using var f = new Fixture(1, twoPlayers: true);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var scout = f.Sim.UnitRegistry.CreateUnit(0, f.Sim.MapData.TileToWorldFixed(x - 1, z),
                Fixed32.One, Fixed32.One, Fixed32.One);
            scout.UnitType = 4; scout.CurrentHealth = scout.MaxHealth = 100;
            for (int tx = 0; tx < f.Sim.MapData.Width; tx++)
                for (int tz = 0; tz < f.Sim.MapData.Height; tz++)
                    if (!f.Sim.MapData.IsOutsideCircle(tx, tz)) f.Sim.FogOfWar.SetVisible(0, tx, tz);
            var parsed = CommanderSemanticJson.Parse(DiscoverEnemyTownCenter);
            var candidate = f.Manager.PrepareActionPlan(parsed, "find the enemy Town Center", 1);
            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            f.Manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Executing),
                "One observation may scan only a bounded portion, not the whole frontier map.");
            Assert.That(goal.DiscoveryScanRadius, Is.LessThan(64));
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
            for (int tick = 1; tick < 8 && !goal.IsTerminal; tick++)
            {
                f.Manager.Tick(tick * 15);
                Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty,
                    "An exhausted known-map scan cannot invent a target or issue a point-scout order.");
            }
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Failed), goal.StatusReason);
            Assert.That(goal.DiscoveryCandidatesChecked, Is.EqualTo(0));
            Assert.That(goal.DiscoveryMovesIssued, Is.EqualTo(0));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Discovery_UnsupportedResultOrStaleRuntimeCannotSelectUnrelatedScout(bool staleRuntime)
        {
            using var f = new Fixture(1, twoPlayers: true);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var scout = f.Sim.UnitRegistry.CreateUnit(0, f.Sim.MapData.TileToWorldFixed(x - 1, z),
                Fixed32.One, Fixed32.One, Fixed32.One);
            scout.UnitType = 4; scout.CurrentHealth = scout.MaxHealth = 100;
            var parsed = CommanderSemanticJson.Parse(DiscoverEnemyTownCenter);
            var candidate = f.Manager.PrepareActionPlan(parsed, "find the enemy Town Center", 1);
            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            if (staleRuntime) goal.RuntimeOwner = null;
            else goal.RequiresResultBinding = true; // Internal stale state, not a provider-supported composition.
            f.Manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked), goal.StatusReason);
            Assert.That(goal.DiscoveryScoutId, Is.EqualTo(-1));
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [TestCase("Lost")]
        [TestCase("Human")]
        [TestCase("Cancel")]
        public void Discovery_ExactScoutLossTakeoverOrCancelStopsFurtherOrders(string outcome)
        {
            using var f = new Fixture(1, twoPlayers: true);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var scout = f.Sim.UnitRegistry.CreateUnit(0, f.Sim.MapData.TileToWorldFixed(x - 1, z),
                Fixed32.One, Fixed32.One, Fixed32.One);
            scout.UnitType = 4; scout.CurrentHealth = scout.MaxHealth = 100;
            var parsed = CommanderSemanticJson.Parse(DiscoverEnemyTownCenter);
            var candidate = f.Manager.PrepareActionPlan(parsed, "find the enemy Town Center", 1);
            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            f.Manager.Tick(0);
            Assert.That(f.Sim.CommandBuffer.FlushCommands().OfType<MoveCommand>().Count(), Is.EqualTo(1));
            if (outcome == "Lost") scout.CurrentHealth = 0;
            else if (outcome == "Human") f.Sim.CommandBuffer.EnqueueCommand(
                new MoveCommand(0, new[] { scout.Id }, scout.SimPosition), CommandEnqueueSource.Human);
            else Assert.That(f.Manager.CancelGoal(goal.GoalId), Is.True);
            f.Sim.CommandBuffer.FlushCommands();
            f.Manager.Tick(15);
            Assert.That(goal.Status, Is.EqualTo(outcome == "Cancel"
                ? CommanderGoalStatus.Cancelled : CommanderGoalStatus.Failed), goal.StatusReason);
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void ComposedWoodline_RejectsWrongSourceAndRelativeDirectionBeforeAdmission()
        {
            using var f = new Fixture(1);
            Assert.That(CommanderSemanticJson.Parse(AllWoodWestPlan.Replace(
                "\"sourceKind\":\"Tree\"},\"inputs\":{\"workers\"",
                "\"sourceKind\":\"GoldMine\"},\"inputs\":{\"workers\"")).IsValid, Is.False);
            Assert.That(CommanderSemanticJson.Parse(AllWoodWestPlan.Replace(
                "\"relation\":\"MapWest\"", "\"relation\":\"Near\"")).IsValid, Is.False);
            Assert.That(f.Manager.Goals, Is.Empty);
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ComposedWoodline_RejectsWrongOwnerOrFoggedSource(bool foreignAnchor)
        {
            using var f = new Fixture(1);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var wood = f.Sim.MapData.AddResourceNode(ResourceType.Wood,
                f.Sim.MapData.TileToWorldFixed(foreignAnchor ? x - 15 : x - 30, z + 3), 1000);
            f.Workers[0].State = UnitState.Gathering;
            f.Workers[0].TargetResourceNodeId = wood.Id;
            if (foreignAnchor)
                f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter).PlayerId = 1;
            var parsed = CommanderSemanticJson.Parse(AllWoodWestPlan);
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var candidate = f.Manager.PrepareActionPlan(parsed, "gather at my western woodline", 1);
            Assert.Throws<ArgumentException>(() => f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1)));
            Assert.That(f.Manager.Goals, Is.Empty);
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void TwoHousesNearOwnedTownCenter_CompleteAtDistinctNativeSites()
        {
            using var f = new Fixture(3);
            string json = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + "{\"id\":\"site\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"MyTownCenter\",\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{},\"dependsOn\":[]},"
                + "{\"id\":\"houses\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:House\",\"count\":2},\"inputs\":{\"location\":\"site\"},\"dependsOn\":[\"site\"]}]}";
            var parsed = CommanderSemanticJson.Parse(json);
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var candidate = f.Manager.PrepareActionPlan(parsed, "build two Houses near my Town Center", 1);
            var goal = (BuildStructureGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            int initialWood = f.Sim.ResourceManager.GetPlayerResources(0).Wood;
            for (int tick = 0; tick < 6000 && goal.Status != CommanderGoalStatus.Completed; tick++)
            {
                if (tick % 15 == 0) f.Manager.Tick(tick);
                f.Sim.Tick(); // Accelerated native fixture ticks; no construction-time shortcut.
            }
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
            Assert.That(goal.ResultBuildingIds.Count, Is.EqualTo(2));
            var houses = goal.ResultBuildingIds.Select(id => f.Sim.BuildingRegistry.GetBuilding(id)).ToArray();
            Assert.That(houses.All(h => h != null && h.PlayerId == 0 && h.Type == BuildingType.House
                && !h.IsUnderConstruction && !h.IsDestroyed), Is.True);
            Assert.That(houses.Select(h => new Vector2Int(h.OriginTileX, h.OriginTileZ)).Distinct().Count(), Is.EqualTo(2));
            Assert.That(f.Sim.ResourceManager.GetPlayerResources(0).Wood, Is.LessThan(initialWood));
        }

        [Test]
        public void TwentyAnchoredHouses_InsufficientDistinctSitesRejectsBeforeFirstPlacement()
        {
            using var f = new Fixture(3);
            string json = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + "{\"id\":\"site\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"MyTownCenter\",\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{},\"dependsOn\":[]},"
                + "{\"id\":\"houses\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:House\",\"count\":20},\"inputs\":{\"location\":\"site\"},\"dependsOn\":[\"site\"]}]}";
            var parsed = CommanderSemanticJson.Parse(json);
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var candidate = f.Manager.PrepareActionPlan(parsed, "build twenty Houses near my TC", 1);
            var goal = (BuildStructureGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            f.Manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            Assert.That(f.Sim.CommandBuffer.FlushCommands().OfType<PlaceBuildingCommand>(), Is.Empty,
                "A statically impossible full site count must not execute a legal prefix.");
        }

        [Test]
        public void TwoHousesNearTc_BacktracksPastCentralCandidateAndBuildsCompatiblePair()
        {
            using var f = new Fixture(3);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            // Only the west strip can build. Its ranked center overlaps both
            // separated sites, while the two separated sites are compatible.
            for (int tx = x - 8; tx <= x + 8; tx++)
                for (int tz = z - 8; tz <= z + 8; tz++)
                    if (tx < x || tx >= x + 4 || tz < z || tz >= z + 4)
                        f.Sim.MapData.Tiles[tx, tz] = TileType.Foundation;
            for (int tx = x - 5; tx <= x - 2; tx++)
                for (int tz = z - 2; tz <= z + 5; tz++)
                    f.Sim.MapData.Tiles[tx, tz] = TileType.Grass;
            string json = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + "{\"id\":\"site\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"MyTownCenter\",\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{},\"dependsOn\":[]},"
                + "{\"id\":\"houses\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:House\",\"count\":2},\"inputs\":{\"location\":\"site\"},\"dependsOn\":[\"site\"]}]}";
            var parsed = CommanderSemanticJson.Parse(json);
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var candidate = f.Manager.PrepareActionPlan(parsed, "build two Houses near my TC", 1);
            var goal = (BuildStructureGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            for (int tick = 0; tick < 6000 && goal.Status != CommanderGoalStatus.Completed; tick++)
            {
                if (tick % 15 == 0) f.Manager.Tick(tick);
                f.Sim.Tick(); // Accelerated fixture, unchanged native construction timers.
            }
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed),
                goal.StatusReason + " planned=" + string.Join(";", goal.DynamicPlannedSiteSet ?? Array.Empty<Vector2Int>())
                + " attributed=" + string.Join(",", goal.AttributedBuildingIds));
            var sites = goal.ResultBuildingIds.Select(id => f.Sim.BuildingRegistry.GetBuilding(id))
                .Select(b => new Vector2Int(b.OriginTileX, b.OriginTileZ)).ToArray();
            Assert.That(sites, Is.EquivalentTo(new[] {
                new Vector2Int(x - 4, z - 1), new Vector2Int(x - 4, z + 3) }));
        }

        [Test]
        public void ExactTargetGarrison_FullRequestedShelterMustNotDispatchToAnotherShelter()
        {
            using var f = new Fixture(2);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var requested = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
            var other = f.Sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true);
            requested.GarrisonCapacity = 1;
            f.Sim.UnitRegistry.GarrisonUnit(f.Workers[0].Id);
            requested.GarrisonedUnitIds.Add(f.Workers[0].Id);
            Assert.That(requested.GarrisonedUnitIds, Does.Contain(f.Workers[0].Id));
            ICommand exact = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
            Assert.That(f.Sim.RegisterExactGarrisonCommand(exact), Is.True);
            f.Sim.Tick(new System.Collections.Generic.List<ICommand> { exact });
            Assert.That(f.Workers[1].TargetGarrisonBuildingId, Is.Not.EqualTo(other.Id),
                "An exact-target request must not dispatch to an unrelated shelter when the requested one fills.");
            Assert.That(other.GarrisonedUnitIds.Contains(f.Workers[1].Id), Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void ExactGarrison_DelayedPastPendingExpiryCannotBecomeOrdinaryFallback(int delivery)
        {
            using var f = new Fixture(2);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var requested = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
            var other = f.Sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true);
            requested.GarrisonCapacity = 1;
            f.Sim.UnitRegistry.GarrisonUnit(f.Workers[0].Id);
            requested.GarrisonedUnitIds.Add(f.Workers[0].Id);
            ICommand original = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
            Assert.That(f.Sim.RegisterExactGarrisonCommand(original), Is.True);
            for (int tick = 0; tick < 258; tick++) f.Sim.Tick();
            ICommand delivered = delivery == 0 ? original
                : new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
            if (delivery == 2)
            {
                var ledger = new CommanderCommandOriginLedger();
                ICommand stamped = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
                ledger.Record(f.Sim.CurrentTick, 0, new[] { original }, new[] { stamped }, f.Sim);
                var commands = new System.Collections.Generic.List<ICommand> { delivered, new NoopCommand(0) };
                var origins = ledger.Consume(f.Sim.CurrentTick, 0, commands, f.Sim);
                Assert.That(origins[delivered], Is.SameAs(original));
                f.Sim.Tick(commands, origins);
            }
            else f.Sim.Tick(new System.Collections.Generic.List<ICommand> { delivered });
            Assert.That(f.Workers[1].TargetGarrisonBuildingId, Is.Not.EqualTo(other.Id));
            Assert.That(other.GarrisonedUnitIds, Is.Empty);
        }

        [Test]
        public void ExpiredExactMarker_PreservesProvenHumanReplacementAndBoundsNewAdmissions()
        {
            using var f = new Fixture(2);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var requested = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
            var other = f.Sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true);
            requested.GarrisonCapacity = 1;
            f.Sim.UnitRegistry.GarrisonUnit(f.Workers[0].Id);
            requested.GarrisonedUnitIds.Add(f.Workers[0].Id);
            ICommand exact = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
            Assert.That(f.Sim.RegisterExactGarrisonCommand(exact), Is.True);
            for (int tick = 0; tick < 258; tick++) f.Sim.Tick();
            ICommand human = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
            f.Sim.CommandBuffer.EnqueueCommand(human, CommandEnqueueSource.Human);
            var flushed = f.Sim.CommandBuffer.FlushCommands();
            var ledger = new CommanderCommandOriginLedger();
            ICommand stamped = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
            ledger.Record(259, 0, flushed, new[] { stamped }, f.Sim);
            ICommand replay = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
            var received = new System.Collections.Generic.List<ICommand> { replay, new NoopCommand(0) };
            var origins = ledger.Consume(259, 0, received, f.Sim);
            Assert.That(origins[replay], Is.SameAs(human));
            f.Sim.Tick(received, origins);
            Assert.That(f.Workers[1].TargetGarrisonBuildingId, Is.EqualTo(other.Id),
                "Only a proven, separate local human order keeps ordinary native fallback.");

            // One pending/tombstoned subject occupies one bounded slot; expiration
            // must not silently free that safety evidence for another exact order.
            using var bounded = new Fixture(1);
            var shelter = bounded.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
            var boundedWorkers = new System.Collections.Generic.List<UnitData> { bounded.Workers[0] };
            for (int i = 1; i < 201; i++)
            {
                var worker = bounded.Sim.UnitRegistry.CreateUnit(0, bounded.Workers[0].SimPosition,
                    Fixed32.One, Fixed32.One, Fixed32.One);
                worker.IsVillager = true;
                worker.CurrentHealth = worker.MaxHealth = 100;
                boundedWorkers.Add(worker);
            }
            foreach (var worker in boundedWorkers.Take(200))
                Assert.That(bounded.Sim.RegisterExactGarrisonCommand(
                    new GarrisonCommand(0, new[] { worker.Id }, shelter.Id)), Is.True);
            for (int tick = 0; tick < 258; tick++) bounded.Sim.Tick();
            Assert.That(bounded.Sim.RegisterExactGarrisonCommand(
                new GarrisonCommand(0, new[] { boundedWorkers[200].Id }, shelter.Id)), Is.False);
        }

        [Test]
        public void ResultBoundVillagerGarrison_UsesOnlyExactProducedResultIds()
        {
            using var f = new Fixture(2);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Villager\",\"count\":2,\"quantityMode\":\"New\"},"
                + "{\"type\":\"GarrisonUnits\",\"unitSelector\":\"Villagers\",\"count\":2,\"location\":\"PlayerBase\",\"structure\":\"TownCenter\",\"dependsOn\":[0],\"resultFromNode\":0}]}" );
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var candidate = f.Manager.PrepareActionPlan(parsed, "train two new villagers then garrison those exact two", 1);
            var goals = f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1));
            var producer = (EnsureUnitCountGoal)goals[0];
            var garrison = (CommanderCapabilityGoal)goals[1];
            var produced = new int[2];
            for (int i = 0; i < produced.Length; i++)
            {
                var unit = f.Sim.UnitRegistry.CreateUnit(0, f.Sim.MapData.TileToWorldFixed(x - 3 - i, z + 3),
                    Fixed32.One, Fixed32.One, Fixed32.One);
                unit.IsVillager = true; unit.UnitType = 0;
                unit.CurrentHealth = unit.MaxHealth = 100;
                produced[i] = unit.Id;
            }
            producer.CaptureUnitResult(produced, 0);
            producer.SetStatus(CommanderGoalStatus.Completed, "Focused exact producer-result fixture");
            f.Manager.Tick(0);
            var command = f.Sim.CommandBuffer.FlushCommands().OfType<GarrisonCommand>().Single();
            Assert.That(command.UnitIds, Is.EquivalentTo(produced));
            Assert.That(command.UnitIds.Intersect(f.Workers.Select(w => w.Id)), Is.Empty);
            Assert.That(garrison.GarrisonCandidateIds, Is.EquivalentTo(produced));
        }

        [Test]
        public void ExactTargetGarrison_RelayOriginRemainsExactAndAmbiguousReplayFailsClosed()
        {
            using var f = new Fixture(2);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var requested = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
            var other = f.Sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true);
            requested.GarrisonCapacity = 1;
            f.Sim.UnitRegistry.GarrisonUnit(f.Workers[0].Id);
            requested.GarrisonedUnitIds.Add(f.Workers[0].Id);
            ICommand original = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
            Assert.That(f.Sim.RegisterExactGarrisonCommand(original), Is.True);
            var ledger = new CommanderCommandOriginLedger();
            ICommand stamped = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
            ledger.Record(0, 0, new[] { original }, new[] { stamped }, f.Sim);
            ICommand replay = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
            var commands = new System.Collections.Generic.List<ICommand> { replay, new NoopCommand(0) };
            var origins = ledger.Consume(0, 0, commands, f.Sim);
            Assert.That(origins.Count, Is.EqualTo(1));
            Assert.That(origins[replay], Is.SameAs(original));
            f.Sim.Tick(commands, origins);
            Assert.That(f.Workers[1].TargetGarrisonBuildingId, Is.Not.EqualTo(other.Id));

            ICommand guarded = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
            ICommand duplicate = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
            Assert.That(f.Sim.RegisterExactGarrisonCommand(guarded), Is.True);
            var ambiguous = new CommanderCommandOriginLedger();
            ambiguous.Record(1, 0, new[] { guarded, duplicate }, new[] { guarded, duplicate }, f.Sim);
            var replays = new System.Collections.Generic.List<ICommand> {
                new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id),
                new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id), new NoopCommand(0) };
            var unknown = ambiguous.Consume(1, 0, replays, f.Sim);
            Assert.That(unknown, Is.Empty);
            f.Sim.Tick(replays, unknown);
            Assert.That(f.Workers[1].TargetGarrisonBuildingId, Is.Not.EqualTo(other.Id));
            Assert.That(other.GarrisonedUnitIds.Contains(f.Workers[1].Id), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExactTargetGarrison_LateFullOrDestroyedTargetCancelsWithoutFallback(bool destroyed)
        {
            using var f = new Fixture(2);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var requested = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
            var other = f.Sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true);
            requested.GarrisonCapacity = 1;
            ICommand exact = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
            Assert.That(f.Sim.RegisterExactGarrisonCommand(exact), Is.True);
            f.Sim.Tick(new System.Collections.Generic.List<ICommand> { exact });
            Assert.That(f.Workers[1].TargetGarrisonBuildingId, Is.EqualTo(requested.Id));
            if (destroyed) requested.CurrentHealth = 0;
            else
            {
                f.Sim.UnitRegistry.GarrisonUnit(f.Workers[0].Id);
                requested.GarrisonedUnitIds.Add(f.Workers[0].Id);
                // Model the path reaching the target; native arrival handles the full shelter.
                f.Workers[1].State = UnitState.Idle;
                f.Workers[1].SimPosition = f.Sim.MapData.TileToWorldFixed(x - 1, z);
            }
            f.Sim.Tick();
            Assert.That(f.Workers[1].TargetGarrisonBuildingId, Is.EqualTo(-1));
            Assert.That(other.GarrisonedUnitIds.Contains(f.Workers[1].Id), Is.False);
        }

        [Test]
        public void OrdinaryGarrisonAndHumanReplacementKeepNativeFallback()
        {
            using var f = new Fixture(2);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var requested = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
            var other = f.Sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true);
            requested.GarrisonCapacity = 1;
            ICommand exact = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
            Assert.That(f.Sim.RegisterExactGarrisonCommand(exact), Is.True);
            f.Sim.Tick(new System.Collections.Generic.List<ICommand> { exact });
            Assert.That(f.Workers[1].TargetGarrisonBuildingId, Is.EqualTo(requested.Id));
            f.Sim.UnitRegistry.GarrisonUnit(f.Workers[0].Id);
            requested.GarrisonedUnitIds.Add(f.Workers[0].Id);
            // A new human Garrison is a replacement order with ordinary fallback semantics.
            f.Sim.Tick(new System.Collections.Generic.List<ICommand> {
                new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id) });
            Assert.That(f.Workers[1].TargetGarrisonBuildingId, Is.EqualTo(other.Id));
        }

        [Test]
        public void ForeignInvalidStopCannotDisarmAnOwnedExactGarrisonOrder()
        {
            using var f = new Fixture(2);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var requested = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
            var other = f.Sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true);
            requested.GarrisonCapacity = 1;
            ICommand exact = new GarrisonCommand(0, new[] { f.Workers[1].Id }, requested.Id);
            Assert.That(f.Sim.RegisterExactGarrisonCommand(exact), Is.True);
            f.Sim.Tick(new System.Collections.Generic.List<ICommand> { exact });
            Assert.That(f.Workers[1].TargetGarrisonBuildingId, Is.EqualTo(requested.Id));
            f.Sim.Tick(new System.Collections.Generic.List<ICommand> {
                new StopCommand(1, new[] { f.Workers[1].Id }) });
            f.Sim.UnitRegistry.GarrisonUnit(f.Workers[0].Id);
            requested.GarrisonedUnitIds.Add(f.Workers[0].Id);
            f.Workers[1].State = UnitState.Idle;
            f.Workers[1].SimPosition = f.Sim.MapData.TileToWorldFixed(x - 1, z);
            f.Sim.Tick();
            Assert.That(f.Workers[1].TargetGarrisonBuildingId, Is.EqualTo(-1));
            Assert.That(other.GarrisonedUnitIds.Contains(f.Workers[1].Id), Is.False);
        }

        [Test]
        public void ChangedOwnerReleasesGuardWithoutMutatingForeignActor()
        {
            using var f = new Fixture(1);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var requested = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
            var newOwnersShelter = f.Sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true);
            newOwnersShelter.PlayerId = 1;
            ICommand exact = new GarrisonCommand(0, new[] { f.Workers[0].Id }, requested.Id);
            Assert.That(f.Sim.RegisterExactGarrisonCommand(exact), Is.True);
            f.Sim.Tick(new System.Collections.Generic.List<ICommand> { exact });
            Assert.That(f.Workers[0].TargetGarrisonBuildingId, Is.EqualTo(requested.Id));
            f.Workers[0].PlayerId = 1;
            f.Sim.Tick();
            Assert.That(f.Workers[0].TargetGarrisonBuildingId, Is.EqualTo(newOwnersShelter.Id),
                "The old owner's marker must not cancel the changed-owner actor's normal redirect.");
        }

        [Test]
        public void ExactGarrison_HealthZeroBeforeNativeProcessingCannotEnter()
        {
            using var f = new Fixture(1);
            var requested = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
            ICommand exact = new GarrisonCommand(0, new[] { f.Workers[0].Id }, requested.Id);
            Assert.That(f.Sim.RegisterExactGarrisonCommand(exact), Is.True);
            f.Workers[0].CurrentHealth = 0;
            f.Sim.Tick(new System.Collections.Generic.List<ICommand> { exact });
            Assert.That(f.Workers[0].TargetGarrisonBuildingId, Is.EqualTo(-1));
            Assert.That(requested.GarrisonedUnitIds.Contains(f.Workers[0].Id), Is.False);
        }

        [Test]
        public void SemanticGarrison_UsesOwnedShelterAndReportsActualHousedCount()
        {
            using var f = new Fixture(2);
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"GarrisonUnits\",\"unitSelector\":\"Villagers\",\"count\":2,"
                + "\"location\":\"PlayerBase\",\"structure\":\"TownCenter\"}]}");
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var candidate = f.Manager.PrepareActionPlan(parsed, "garrison two villagers in my Town Center", 1);
            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            var shelter = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
            shelter.GarrisonCapacity = 1;
            f.Manager.Tick(0);
            ICommand original = f.Sim.CommandBuffer.FlushCommands().Single();
            var command = (GarrisonCommand)original;
            Assert.That(command.TargetBuildingId, Is.EqualTo(shelter.Id));
            Assert.That(command.UnitIds.Length, Is.EqualTo(1));
            for (int tick = 1; tick < 500 && !goal.IsTerminal; tick++)
            {
                f.Sim.Tick(tick == 1 ? new System.Collections.Generic.List<ICommand> { original }
                    : new System.Collections.Generic.List<ICommand>());
                f.Manager.Tick(tick * 15);
            }
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked), goal.StatusReason);
            Assert.That(shelter.GarrisonCount, Is.EqualTo(1));
            Assert.That(goal.StatusReason, Does.Contain("1/2"));
            Assert.That(goal.StatusReason, Does.Contain("unaccommodated"));
            var card = CommanderTaskBoardProjection.Capture(f.Manager, null, 1).Cards.Single();
            Assert.That(card.Status, Is.EqualTo(CommanderTaskStatus.Blocked));
            Assert.That(card.Progress, Does.Contain("1 / 2"));
        }

        [Test]
        public void SemanticGarrison_AllVillagersFreezesActualSetAndReportsRemainder()
        {
            using var f = new Fixture(3);
            var shelter = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
            shelter.GarrisonCapacity = 1;
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"GarrisonUnits\",\"unitSelector\":\"Villagers\",\"countMode\":\"AllMatching\","
                + "\"location\":\"PlayerBase\",\"structure\":\"TownCenter\"}]}");
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var candidate = f.Manager.PrepareActionPlan(parsed, "garrison all villagers", 1);
            Assert.That(candidate.Preview, Does.Contain("all").IgnoreCase);
            Assert.That(candidate.Preview, Does.Not.Contain("1 owned"));
            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            f.Manager.Tick(0);
            ICommand original = f.Sim.CommandBuffer.FlushCommands().Single();
            Assert.That(((GarrisonCommand)original).UnitIds.Length, Is.EqualTo(1));
            for (int tick = 1; tick < 500 && !goal.IsTerminal; tick++)
            {
                f.Sim.Tick(tick == 1 ? new System.Collections.Generic.List<ICommand> { original }
                    : new System.Collections.Generic.List<ICommand>());
                f.Manager.Tick(tick * 15);
            }
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked), goal.StatusReason);
            Assert.That(goal.StatusReason, Does.Contain("1/3"));
            Assert.That(goal.StatusReason, Does.Contain("unaccommodated 2"));
            var card = CommanderTaskBoardProjection.Capture(f.Manager, null, 1).Cards.Single();
            Assert.That(card.Status, Is.EqualTo(CommanderTaskStatus.Blocked));
            Assert.That(card.Progress, Does.Contain("1 / 3"));
        }

        [Test]
        public void SemanticGarrison_FullNativeEntryStillCompletesRequestedObjective()
        {
            using var f = new Fixture(1);
            var parsed = CommanderSemanticJson.Parse(GarrisonTwoVillagers.Replace("\"count\":2", "\"count\":1"));
            var candidate = f.Manager.PrepareActionPlan(parsed, "garrison one villager in my Town Center", 1);
            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            f.Manager.Tick(0);
            ICommand original = f.Sim.CommandBuffer.FlushCommands().Single();
            for (int tick = 1; tick < 500 && !goal.IsTerminal; tick++)
            {
                f.Sim.Tick(tick == 1 ? new System.Collections.Generic.List<ICommand> { original }
                    : new System.Collections.Generic.List<ICommand>());
                f.Manager.Tick(tick * 15);
            }
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
            Assert.That(goal.StatusReason, Does.Contain("1/1"));
            Assert.That(CommanderTaskBoardProjection.Capture(f.Manager, null, 1).Cards.Single().Status,
                Is.EqualTo(CommanderTaskStatus.Completed));
        }

        [Test]
        public void SemanticGarrison_StalledNativeApproachTerminatesWithoutShelterFallback()
        {
            using var f = new Fixture(1);
            f.Workers[0].MoveSpeed = Fixed32.Zero;
            var parsed = CommanderSemanticJson.Parse(GarrisonTwoVillagers.Replace("\"count\":2", "\"count\":1"));
            var candidate = f.Manager.PrepareActionPlan(parsed, "garrison one villager", 1);
            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            f.Manager.Tick(0);
            ICommand original = f.Sim.CommandBuffer.FlushCommands().Single();
            for (int tick = 1; tick <= 165 && goal.Status == CommanderGoalStatus.Executing; tick++)
            {
                f.Sim.Tick(tick == 1 ? new System.Collections.Generic.List<ICommand> { original }
                    : new System.Collections.Generic.List<ICommand>());
                f.Manager.Tick(tick * 15);
            }
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked), goal.StatusReason);
            Assert.That(goal.StatusReason, Does.Contain("progress").IgnoreCase);
        }

        [Test]
        public void SemanticGarrison_AllMatchingOverBoundRefusesWholeSnapshotWithoutPrefix()
        {
            using var f = new Fixture(1);
            for (int i = 0; i < 200; i++)
            {
                var worker = f.Sim.UnitRegistry.CreateUnit(0, f.Workers[0].SimPosition,
                    Fixed32.One, Fixed32.One, Fixed32.One);
                worker.IsVillager = true;
                worker.CurrentHealth = worker.MaxHealth = 100;
                worker.State = UnitState.Idle;
            }
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"GarrisonUnits\",\"unitSelector\":\"Villagers\",\"countMode\":\"AllMatching\","
                + "\"location\":\"PlayerBase\",\"structure\":\"TownCenter\"}]}");
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(
                f.Manager.PrepareActionPlan(parsed, "garrison all villagers", 1), 1))[0];
            f.Manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked), goal.StatusReason);
            Assert.That(goal.StatusReason, Does.Contain("200-subject safety bound"));
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test]
        public void SemanticUngarrison_ExplicitBuildingWideCommandRestoresAllHousedUnits()
        {
            using var f = new Fixture(2);
            var shelter = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
            foreach (var worker in f.Workers)
            {
                f.Sim.UnitRegistry.GarrisonUnit(worker.Id);
                shelter.GarrisonedUnitIds.Add(worker.Id);
            }
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"UngarrisonBuilding\",\"location\":\"PlayerBase\",\"structure\":\"TownCenter\"}]}");
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var candidate = f.Manager.PrepareActionPlan(parsed, "ungarrison everyone from my Town Center", 1);
            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            f.Manager.Tick(0);
            var command = f.Sim.CommandBuffer.FlushCommands().OfType<UngarrisonCommand>().Single();
            Assert.That(command.BuildingId, Is.EqualTo(shelter.Id));
            f.Sim.Tick(new System.Collections.Generic.List<ICommand> { command });
            f.Manager.Tick(15);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
            Assert.That(shelter.GarrisonCount, Is.Zero);
            Assert.That(f.Workers.All(w => f.Sim.UnitRegistry.GetUnit(w.Id) != null), Is.True);
            Assert.That(goal.StatusReason, Does.Contain("Building-wide"));
        }

        [Test]
        public void SemanticGarrison_ProtectedWorkerIsNotReclaimedOrSilentlyReplaced()
        {
            using var f = new Fixture(2);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            f.Workers[1].SimPosition = f.Sim.MapData.TileToWorldFixed(x - 1, z);
            f.Sim.CommandBuffer.EnqueueCommand(new StopCommand(0, new[] { f.Workers[0].Id }));
            f.Sim.CommandBuffer.FlushCommands(); // Human enqueue creates a protection lease.
            var parsed = CommanderSemanticJson.Parse(GarrisonTwoVillagers);
            var candidate = f.Manager.PrepareActionPlan(parsed, "garrison two villagers in my TC", 1);
            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            f.Manager.Tick(0);
            ICommand original = f.Sim.CommandBuffer.FlushCommands().Single();
            var command = (GarrisonCommand)original;
            Assert.That(command.UnitIds, Is.EqualTo(new[] { f.Workers[1].Id }));
            for (int tick = 1; tick < 500 && goal.Status != CommanderGoalStatus.Completed; tick++)
            {
                f.Sim.Tick(tick == 1 ? new System.Collections.Generic.List<ICommand> { original }
                    : new System.Collections.Generic.List<ICommand>());
                f.Manager.Tick(tick * 15);
            }
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
            Assert.That(goal.StatusReason, Does.Contain("protected 1"));
            Assert.That(f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter)
                .GarrisonedUnitIds.Contains(f.Workers[0].Id), Is.False);
        }

        [Test]
        public void SemanticGarrison_FullShelterBlocksWithoutAnyCommand()
        {
            using var f = new Fixture(2);
            var shelter = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
            shelter.GarrisonCapacity = 1;
            f.Sim.UnitRegistry.GarrisonUnit(f.Workers[0].Id);
            shelter.GarrisonedUnitIds.Add(f.Workers[0].Id);
            var parsed = CommanderSemanticJson.Parse(GarrisonTwoVillagers);
            var candidate = f.Manager.PrepareActionPlan(parsed, "garrison two villagers in my TC", 1);
            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            f.Manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
            Assert.That(goal.StatusReason, Does.Contain("unaccommodated"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SemanticGarrison_CancelOrResetRetainsExactGuardUntilNativeOrderEnds(bool reset)
        {
            using var f = new Fixture(2);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var requested = f.Sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.TownCenter);
            var other = f.Sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true);
            var parsed = CommanderSemanticJson.Parse(GarrisonTwoVillagers);
            var candidate = f.Manager.PrepareActionPlan(parsed, "garrison two villagers in my TC", 1);
            var goal = (CommanderCapabilityGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            f.Manager.Tick(0);
            ICommand original = f.Sim.CommandBuffer.FlushCommands().Single();
            var command = (GarrisonCommand)original;
            Assert.That(command.TargetBuildingId, Is.EqualTo(requested.Id));
            f.Sim.Tick(new System.Collections.Generic.List<ICommand> { original });
            if (reset) f.Manager.Dispose();
            else
            {
                Assert.That(f.Manager.CancelActionPlan(candidate), Is.True);
                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Cancelled));
            }
            requested.GarrisonCapacity = 0;
            f.Sim.Tick();
            Assert.That(f.Workers.All(w => w.TargetGarrisonBuildingId != other.Id), Is.True);
            Assert.That(other.GarrisonedUnitIds, Is.Empty);
        }

        [Test]
        public void SelectiveUngarrisonDeclarationIsRejectedBeforeAdmission()
        {
            using var f = new Fixture(1);
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"UngarrisonBuilding\",\"location\":\"PlayerBase\","
                + "\"structure\":\"TownCenter\",\"count\":1}]}");
            Assert.That(parsed.IsValid, Is.False);
            Assert.That(f.Manager.Goals, Is.Empty);
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        private const string GarrisonTwoVillagers = "{\"outcome\":\"Request\",\"nodes\":["
            + "{\"type\":\"GarrisonUnits\",\"unitSelector\":\"Villagers\",\"count\":2,"
            + "\"location\":\"PlayerBase\",\"structure\":\"TownCenter\"}]}";

        [Test]
        public void OptInRequestTrace_CorrelatesApprovalAdmissionAndDispatchWithoutOriginalText()
        {
            using var f = new Fixture(1);
            var logs = new System.Collections.Generic.List<string>();
            void Capture(string message, string stack, LogType type)
            { if (message.StartsWith("[Commander] request=", StringComparison.Ordinal)) logs.Add(message); }
            Application.logMessageReceived += Capture;
            try
            {
                f.Manager.RequestTracingEnabled = true;
                var ticket = f.Manager.BeginSemanticRequest("private original text", 1, () => true);
                var parsed = CommanderSemanticJson.Parse(CommanderPhase5ADynamicPlanTests.Farms.Replace("\"count\":4", "\"count\":1"));
                var candidate = f.Manager.PrepareActionPlan(parsed, ticket.OriginalInput, 1, requestTicket: ticket);
                f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1));
                f.Manager.Tick(0);
                Assert.That(logs.Any(l => l.Contains("stage=approved")), Is.True);
                Assert.That(logs.Any(l => l.Contains("stage=goal-admitted")), Is.True);
                Assert.That(logs.Any(l => l.Contains("dispatch=PlaceBuildingCommand")), Is.True);
                Assert.That(logs.All(l => l.Contains("request=" + ticket.Id + ";")), Is.True);
                Assert.That(logs.Any(l => l.Contains(ticket.OriginalInput)), Is.False);
            }
            finally { Application.logMessageReceived -= Capture; }
        }

        [Test]
        public void CanonicalUnitSelection_UsesAvailableCivilizationIdsAndRejectsReplacedBaseId()
        {
            using var f = new Fixture(1);
            f.Sim.SetPlayerCivilizations(new[] { Civilization.HolyRomanEmpire });
            var replacement = f.Sim.UnitRegistry.CreateUnit(0, f.Workers[0].SimPosition,
                Fixed32.One, Fixed32.One, Fixed32.One);
            replacement.UnitType = 12;
            replacement.CurrentHealth = replacement.MaxHealth = 100;
            replacement.State = UnitState.Idle;
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + "{\"id\":\"owned\",\"mechanic\":\"select-units\",\"parameters\":{\"unit\":\"unit:1\",\"count\":1},\"inputs\":{},\"dependsOn\":[]},"
                + "{\"id\":\"build\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1},\"inputs\":{},\"dependsOn\":[\"owned\"]}]}");
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            Assert.Throws<ArgumentException>(() => f.Manager.PrepareActionPlan(parsed,
                "select my spear and make a farm", 1), "The replaced base ID is unavailable in canonical HRE data.");
            parsed = CommanderSemanticJson.Parse(parsedJsonForReplacement());
            var candidate = f.Manager.PrepareActionPlan(parsed, "select my Landsknecht and make a farm", 1);
            Assert.That(f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1)).Count, Is.EqualTo(1));
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty,
                "Selection is preflight-only and must not issue a new dynamic military action.");
            var context = new CommanderContextBuilder().Build(f.Sim, f.Manager);
            Assert.That(context.CanonicalUnitIds, Does.Contain("unit:12"));
            var production = CommanderSemanticJson.Parse(OneNewSpear.Replace("unit:1", "unit:12"));
            var productionCandidate = f.Manager.PrepareActionPlan(production, "one new Landsknecht", 2);
            Assert.That(productionCandidate.Preview, Does.Contain("Landsknecht"));
            Assert.That(productionCandidate.Preview, Does.Not.Contain("Spearman"),
                "The approved effect label must describe the canonical replacement, not its internal base request.");

            string parsedJsonForReplacement() => "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + "{\"id\":\"owned\",\"mechanic\":\"select-units\",\"parameters\":{\"unit\":\"unit:12\",\"count\":1},\"inputs\":{},\"dependsOn\":[]},"
                + "{\"id\":\"build\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":1},\"inputs\":{},\"dependsOn\":[\"owned\"]}]}";
        }

        [Test]
        public void RestrictedAgePreparation_PreservesGoldWorkersAddedAfterInitialBlocker()
        {
            using var f = new Fixture(1);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var sheep = f.Sim.UnitRegistry.GetAllUnits().Single(u => u.IsSheep);
            sheep.CurrentHealth = 0;
            sheep.State = UnitState.Dead;
            var resources = f.Sim.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Gold = 0;
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"ReachAge\",\"targetAge\":\"Next\",\"constraints\":["
                + "{\"type\":\"ResourceSource\",\"resource\":\"Food\",\"sourceKind\":\"Sheep\"}]}]}");
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(
                f.Manager.PrepareActionPlan(parsed, "next age using food only from sheep", 1), 1));
            f.Manager.Tick(0);
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
            var gold = f.Sim.MapData.AddResourceNode(ResourceType.Gold,
                f.Sim.MapData.TileToWorldFixed(x - 10, z + 2), 1000);
            f.Workers[0].TargetResourceNodeId = gold.Id;
            f.Workers[0].State = UnitState.Gathering;
            sheep.CurrentHealth = 100;
            sheep.State = UnitState.Idle;
            f.Manager.Tick(180);
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty,
                "Cached Food preparation must not reclaim the newly needed Gold worker.");
        }

        [Test]
        public void PreparationSourceDtoAndHumanTakeover_PreserveRestrictionWithoutReclaim()
        {
            using var f = new Fixture(1);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            f.Sim.CreateBuilding(0, BuildingType.Barracks, x + 9, z, false);
            f.Sim.ResourceManager.GetPlayerResources(0).Food = 0;
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":1,\"constraints\":["
                + "{\"type\":\"ResourceSource\",\"resource\":\"Food\",\"sourceKind\":\"Sheep\"}]}]}");
            Assert.That(parsed.IsValid, Is.True);
            var context = new CommanderContextBuilder().Build(f.Sim, f.Manager);
            Assert.That(CommanderSemanticAdmission.TryCreateTacticalIntent(parsed.Nodes[0], context, out var intent, out _), Is.True);
            var dto = CommanderIntentDtoCodec.FromIntent(intent);
            Assert.That(dto.constraints[0].sourceKind, Is.EqualTo("Sheep"));
            var roundTrip = CommanderIntentDtoCodec.InterpretJson(CommanderIntentDtoCodec.ToJson(dto), context);
            Assert.That(roundTrip.Success, Is.True);
            Assert.That(((ResourceSourceConstraint)roundTrip.Intent.Constraints[0]).SourceKind, Is.EqualTo(ResourceSourceKind.Sheep));
            var goal = f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(
                f.Manager.PrepareActionPlan(parsed, "one spear with food only from sheep", 1), 1))[0];
            f.Manager.Tick(0);
            Assert.That(f.Sim.CommandBuffer.FlushCommands().OfType<SlaughterSheepCommand>().Count(), Is.EqualTo(1));
            f.Sim.CommandBuffer.EnqueueCommand(new StopCommand(0, new[] { f.Workers[0].Id }));
            f.Sim.Tick();
            f.Manager.Tick(1005);
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
        }

        [TestCase("{\"type\":\"ResourceSource\",\"resource\":\"Wood\",\"sourceKind\":\"Sheep\"}")]
        [TestCase("{\"type\":\"ResourceSource\",\"resource\":\"Food\",\"sourceKind\":0}")]
        [TestCase("{\"type\":\"ResourceSource\",\"resource\":\"Food\",\"sourceKind\":\"Sheep\",\"approved\":true}")]
        public void MalformedPreparationSource_RejectsWholePlan(string constraint)
        {
            using var f = new Fixture(1);
            string json = OneNewSpear.Replace("\"version\":1,", "\"version\":1,\"constraints\":[" + constraint + "],");
            Assert.That(CommanderSemanticJson.Parse(json).IsValid, Is.False);
            Assert.That(f.Manager.Goals, Is.Empty);
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ProductionFoodSourceRestriction_UsesOnlySheepOrReportsBlocker(bool sheepAvailable)
        {
            using var f = new Fixture(1);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            f.Sim.CreateBuilding(0, BuildingType.Barracks, x + 9, z, false);
            f.Sim.ResourceManager.GetPlayerResources(0).Food = 0;
            f.Sim.MapData.AddResourceNode(ResourceType.Food, f.Sim.MapData.TileToWorldFixed(x - 11, z + 2), 1000);
            foreach (var sheep in f.Sim.UnitRegistry.GetAllUnits().Where(u => u.IsSheep))
                if (!sheepAvailable) { sheep.CurrentHealth = 0; sheep.State = UnitState.Dead; }
            string json = OneNewSpear.Replace("\"version\":1,", "\"version\":1,\"constraints\":["
                + "{\"type\":\"ResourceSource\",\"resource\":\"Food\",\"sourceKind\":\"Sheep\"}],");
            var parsed = CommanderSemanticJson.Parse(json);
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var candidate = f.Manager.PrepareActionPlan(parsed, "one new spear using food only from sheep", 1);
            Assert.That(candidate.Preview, Does.Contain("Sheep"));
            var goal = f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            f.Manager.Tick(0);
            var commands = f.Sim.CommandBuffer.FlushCommands();
            Assert.That(commands.OfType<GatherCommand>().All(c => c.SourceKind == ResourceSourceKind.Sheep), Is.True);
            Assert.That(commands.OfType<TrainUnitCommand>(), Is.Empty);
            if (sheepAvailable)
            {
                var command = commands.OfType<SlaughterSheepCommand>().Single();
                Assert.That(command.SourceKind, Is.EqualTo(ResourceSourceKind.Sheep));
                Assert.That(command.VillagerIds, Is.EqualTo(new[] { f.Workers[0].Id }));
            }
            else
            {
                Assert.That(commands, Is.Empty, "Visible berries cannot silently substitute for the requested food source.");
                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            }
        }

        private const string OneNewSpear = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
            + "{\"id\":\"spear\",\"mechanic\":\"produce\",\"parameters\":{\"unit\":\"unit:1\",\"count\":1,\"quantityMode\":\"New\"},\"inputs\":{},\"dependsOn\":[]}]}";

        [Test]
        public void AmbiguousRelayTrainingOrigin_BlocksReplacementOfPotentiallyExecutedExactOrder()
        {
            using var f = new Fixture(1);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var barracks = f.Sim.CreateBuilding(0, BuildingType.Barracks, x + 9, z, false);
            var candidate = f.Manager.PrepareActionPlan(CommanderSemanticJson.Parse(OneNewSpear), "one new spear", 1);
            var goal = f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            f.Manager.Tick(0);
            ICommand own = f.Sim.CommandBuffer.FlushCommands().Single(c => c is TrainUnitCommand);
            ICommand human = new TrainUnitCommand(0, barracks.Id, 1);
            var ledger = new CommanderCommandOriginLedger();
            var originals = new[] { own, human };
            var replayed = new ICommand[] { new TrainUnitCommand(0, barracks.Id, 1), new TrainUnitCommand(0, barracks.Id, 1), new NoopCommand(0) };
            ledger.Record(0, 0, originals, originals, f.Sim);
            var map = ledger.Consume(0, 0, replayed, f.Sim);
            Assert.That(map, Is.Empty);
            f.Sim.Tick(replayed.ToList(), map);
            Assert.That(barracks.TrainingQueue.Count, Is.EqualTo(2), "Attribution rejection cannot suppress ordinary gameplay.");
            f.Manager.Tick(15);
            Assert.That(f.Sim.CommandBuffer.FlushCommands().OfType<TrainUnitCommand>(), Is.Empty,
                "Unknown attribution is not proof of rejection and cannot authorize extra production.");
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
        }

        [Test]
        public void CancelRequest_ReleasesItsPendingTrainingOriginsOnly()
        {
            using var f = new Fixture(1);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var barracks = f.Sim.CreateBuilding(0, BuildingType.Barracks, x + 9, z, false);
            var candidate = f.Manager.PrepareActionPlan(CommanderSemanticJson.Parse(OneNewSpear), "one new spear", 1);
            f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1));
            f.Manager.Tick(0);
            ICommand own = f.Sim.CommandBuffer.FlushCommands().Single(c => c is TrainUnitCommand);
            ICommand unrelated = new TrainUnitCommand(0, barracks.Id, 1);
            f.Sim.RegisterTrainingOrigin(unrelated, new object(), () => true);
            Assert.That(f.Sim.HasPendingTrainingOrigin(own), Is.True);
            Assert.That(f.Manager.CancelActionPlan(candidate), Is.True);
            Assert.That(f.Sim.HasPendingTrainingOrigin(own), Is.False);
            Assert.That(f.Sim.HasPendingTrainingOrigin(unrelated), Is.True);
            f.Sim.DiscardTrainingOrigin(unrelated);
        }

        [Test]
        public void UnsupportedNearGap_RejectsBeforeOfferingApproval()
        {
            using var f = new Fixture(1);
            var parsed = CommanderSemanticJson.Parse(CommanderPhase5ADynamicPlanTests.Mill.Replace("\"clearGapTiles\":1", "\"clearGapTiles\":2"));
            Assert.That(parsed.IsValid, Is.True, "Structural data does not yet establish a supported placement mechanic.");
            Assert.Throws<ArgumentException>(() => f.Manager.PrepareActionPlan(parsed, "near with two clear tiles", 1));
            Assert.That(f.Manager.Goals, Is.Empty);
        }

        [Test]
        public void CanonicalCostMutation_FreshProjectionAndOrdinaryPlacementUseExistingVirtualGameData()
        {
            using var f = new Fixture(1, mutableCosts: true);
            var mutable = (MutableCostConfig)f.Config;
            mutable.BarracksCost = 333;
            var oldRecord = GameKnowledgeCatalog.Build(f.Sim).FindBuilding("building:Barracks");
            Assert.That(oldRecord.Cost.Wood, Is.EqualTo(333));
            mutable.BarracksCost = 444;
            var freshRecord = GameKnowledgeCatalog.Build(f.Sim).FindBuilding("building:Barracks");
            Assert.That(freshRecord.Cost.Wood, Is.EqualTo(444));
            Assert.That(oldRecord.Cost.Wood, Is.EqualTo(333), "Detached observations must not alias later gameplay mutation.");
            var json = CommanderPhase5ADynamicPlanTests.Farms.Replace("building:Farm", "building:Barracks")
                .Replace("\"count\":4", "\"count\":1");
            var candidate = f.Manager.PrepareActionPlan(CommanderSemanticJson.Parse(json), "one barracks", 1);
            f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1));
            int before = f.Sim.ResourceManager.GetPlayerResources(0).Wood;
            f.Manager.Tick(0);
            f.Sim.Tick();
            Assert.That(f.Sim.BuildingRegistry.GetAllBuildings().Count(b => b.Type == BuildingType.Barracks), Is.EqualTo(1));
            Assert.That(f.Sim.ResourceManager.GetPlayerResources(0).Wood, Is.EqualTo(before - 444));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DynamicGlobalNoConstruction_ControlsProductionPrerequisites(bool producerExists)
        {
            using var f = new Fixture(1);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            if (producerExists) f.Sim.CreateBuilding(0, BuildingType.Barracks, x + 9, z, false);
            string json = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"constraints\":["
                + "{\"type\":\"NoConstruction\"},{\"type\":\"MaximumQueue\",\"amount\":1}],\"nodes\":["
                + "{\"id\":\"spears\",\"mechanic\":\"produce\",\"parameters\":{\"unit\":\"unit:1\",\"count\":1,\"quantityMode\":\"New\"},"
                + "\"inputs\":{},\"dependsOn\":[]}]}";
            var parsed = CommanderSemanticJson.Parse(json);
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var candidate = f.Manager.PrepareActionPlan(parsed, "one new spear without any construction", 1);
            Assert.That(candidate.Preview, Does.Contain("construction forbidden"));
            var goal = (EnsureUnitCountGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            Assert.That(goal.ConstructionForbidden, Is.True);
            Assert.That(goal.MaxQueueDepth, Is.EqualTo(1));
            f.Manager.Tick(0);
            var commands = f.Sim.CommandBuffer.FlushCommands();
            Assert.That(commands.OfType<PlaceBuildingCommand>(), Is.Empty);
            Assert.That(commands.OfType<ConstructBuildingCommand>(), Is.Empty);
            Assert.That(commands.OfType<TrainUnitCommand>().Count(), Is.EqualTo(producerExists ? 1 : 0));
        }

        [Test]
        public void DynamicGlobalNoConstruction_RejectsContradictoryBuildBeforeAdmission()
        {
            using var f = new Fixture(1);
            string json = CommanderPhase5ADynamicPlanTests.Farms.Replace("\"version\":1,",
                "\"version\":1,\"constraints\":[{\"type\":\"NoConstruction\"}],");
            var parsed = CommanderSemanticJson.Parse(json);
            Assert.That(parsed.IsValid, Is.True);
            Assert.Throws<ArgumentException>(() => f.Manager.PrepareActionPlan(parsed, "no construction", 1));
            Assert.That(f.Manager.Goals, Is.Empty);
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [TestCase("{\"type\":\"PreferredWorkers\",\"mode\":\"IdleOnly\"}")]
        [TestCase("{\"type\":\"ProtectedResource\",\"resource\":\"Wood\",\"amount\":1}")]
        public void DynamicGlobalWorkerConstraints_RejectEntireUnsafeSharedSelection(string constraint)
        {
            using var f = new Fixture(3);
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var wood = f.Sim.MapData.AddResourceNode(ResourceType.Wood, f.Sim.MapData.TileToWorldFixed(x - 12, z), 1000);
            for (int i = 0; i < 2; i++)
            {
                f.Workers[i].State = UnitState.Gathering;
                f.Workers[i].TargetResourceNodeId = wood.Id;
            }
            string json = CommanderPhase5ADynamicCompilerTests.SharedWorkers
                .Replace("\"version\":1,", "\"version\":1,\"constraints\":[" + constraint + "],")
                .Replace("\"state\":\"Idle\"", "\"state\":\"Any\"");
            var parsed = CommanderSemanticJson.Parse(json);
            Assert.That(parsed.IsValid, Is.True);
            var plan = f.Manager.ApproveActionPlan(f.Manager.PrepareActionPlan(parsed, "shared workers with protection", 1), 1);
            Assert.Throws<InvalidOperationException>(() => f.Manager.SubmitSemanticGraph(plan));
            Assert.That(f.Manager.Goals, Is.Empty);
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
            Assert.That(f.Workers.All(w => !f.Manager.GetWorkerReservation(w.Id).HasValue), Is.True);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void ResourceLocation_BuildsDistinctExactMillsNearTheFrozenVisibleBerries(int count)
        {
            using var f = new Fixture(1);
            foreach (var resource in f.Sim.MapData.GetAllResourceNodes()) resource.RemainingAmount = 0;
            int x = f.Sim.MapData.Width / 2, z = f.Sim.MapData.Height / 2;
            var berries = f.Sim.MapData.AddResourceNode(ResourceType.Food,
                f.Sim.MapData.TileToWorldFixed(x + 15, z + 5), 1000);
            string json = CommanderPhase5ADynamicPlanTests.Mill.Replace(
                "\"building\":\"building:Mill\",\"count\":1", "\"building\":\"building:Mill\",\"count\":" + count);
            var candidate = f.Manager.PrepareActionPlan(CommanderSemanticJson.Parse(json),
                "build Mills near the visible berries", 1);
            var build = (BuildStructureGoal)f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1))[0];
            // Native Mill placement queues a gather waypoint. Preserve that behavior
            // and allow the existing 150-tick blocked retry to observe its completion.
            for (int step = 0; step < 20; step++)
            {
                f.Manager.Tick(step * 15);
                f.Sim.Tick();
                foreach (var mill in f.Sim.BuildingRegistry.GetAllBuildings().Where(b =>
                    b.PlayerId == 0 && b.Type == BuildingType.Mill && b.IsUnderConstruction))
                    mill.IsUnderConstruction = false;
                f.Workers[0].State = UnitState.Idle;
            }
            Assert.That(build.Status, Is.EqualTo(CommanderGoalStatus.Completed), build.StatusReason
                + "; worker state=" + f.Workers[0].State + "; queue=" + f.Workers[0].CommandQueue.Count
                + "; reservation=" + f.Manager.GetWorkerReservation(f.Workers[0].Id)?.GoalId
                + "; health=" + f.Workers[0].CurrentHealth + "; villager=" + f.Workers[0].IsVillager
                + "; registry=" + (f.Sim.UnitRegistry.GetUnit(f.Workers[0].Id) != null)
                + "; frozen=" + (build.FrozenWorkerIds?.Count.ToString() ?? "none"));
            Assert.That(build.ResultBuildingIds.Count, Is.EqualTo(count));
            var results = build.ResultBuildingIds.Select(id => f.Sim.BuildingRegistry.GetBuilding(id)).ToArray();
            Assert.That(results.Select(b => new Vector2Int(b.OriginTileX, b.OriginTileZ)).Distinct().Count(), Is.EqualTo(count));
            foreach (var mill in results)
            {
                int dx = Math.Max(berries.TileX - mill.OriginTileX - mill.TileFootprintWidth,
                    mill.OriginTileX - berries.TileX - berries.FootprintWidth);
                int dz = Math.Max(berries.TileZ - mill.OriginTileZ - mill.TileFootprintHeight,
                    mill.OriginTileZ - berries.TileZ - berries.FootprintHeight);
                Assert.That(Math.Max(dx, dz), Is.InRange(0, 3), "The semantic target cannot become an unrelated base location.");
            }
        }

        [Test]
        public void SharedWorkerHumanTakeover_IsStickyAndDoesNotReplaceTheBoundBuilder()
        {
            using var f = new Fixture(4);
            var candidate = f.Manager.PrepareActionPlan(
                CommanderSemanticJson.Parse(CommanderPhase5ADynamicCompilerTests.SharedWorkers), "shared worker request", 1);
            var goals = f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1));
            f.Sim.CommandBuffer.EnqueueCommand(new StopCommand(0, new[] { f.Workers[2].Id }));
            f.Sim.Tick();
            for (int step = 0; step < 3; step++)
            {
                f.Manager.Tick(1005 + step * 15); // Aligned planning ticks beyond temporary protection.
                f.Sim.Tick(); // Earlier runnable allocation yields before the builder is checked.
            }
            Assert.That(f.Sim.CommandBuffer.FlushCommands().OfType<PlaceBuildingCommand>(), Is.Empty);
            Assert.That(goals[1].Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            Assert.That(f.Manager.GetWorkerReservation(f.Workers[3].Id).HasValue, Is.False);
        }

        [Test]
        public void SharedThreeWorkerSelection_UnavailableFullSetRejectsWithoutAnyAdmission()
        {
            using var f = new Fixture(2);
            var candidate = f.Manager.PrepareActionPlan(
                CommanderSemanticJson.Parse(CommanderPhase5ADynamicCompilerTests.SharedWorkers),
                "take three idle workers, two on sheep and the third on a Mill", 1);
            var plan = f.Manager.ApproveActionPlan(candidate, 1);
            Assert.Throws<InvalidOperationException>(() => f.Manager.SubmitSemanticGraph(plan));
            Assert.That(f.Manager.Goals, Is.Empty);
            Assert.That(f.Sim.CommandBuffer.FlushCommands(), Is.Empty);
            Assert.That(f.Workers.All(w => !f.Manager.GetWorkerReservation(w.Id).HasValue), Is.True);
        }

        [Test]
        public void SharedThreeWorkerSelection_FreezesDisjointRolesAndIgnoresFourthWorker()
        {
            using var f = new Fixture(4);
            var candidate = f.Manager.PrepareActionPlan(
                CommanderSemanticJson.Parse(CommanderPhase5ADynamicCompilerTests.SharedWorkers),
                "take three idle workers, two on sheep and the third on a Mill", 1);
            Assert.That(f.Workers.All(w => !f.Manager.GetWorkerReservation(w.Id).HasValue), Is.True,
                "A pending preview cannot reserve workers.");
            var goals = f.Manager.SubmitSemanticGraph(f.Manager.ApproveActionPlan(candidate, 1));
            Assert.That(f.Manager.GetWorkerReservation(f.Workers[0].Id)?.GoalId, Is.EqualTo(goals[0].GoalId));
            Assert.That(f.Manager.GetWorkerReservation(f.Workers[1].Id)?.GoalId, Is.EqualTo(goals[0].GoalId));
            Assert.That(f.Manager.GetWorkerReservation(f.Workers[2].Id)?.GoalId, Is.EqualTo(goals[1].GoalId));
            Assert.That(f.Manager.GetWorkerReservation(f.Workers[3].Id).HasValue, Is.False);
            f.Manager.Tick(0);
            var first = f.Sim.CommandBuffer.FlushCommands();
            var gather = first.OfType<SlaughterSheepCommand>().Single();
            Assert.That(gather.VillagerIds, Is.EquivalentTo(new[] { f.Workers[0].Id, f.Workers[1].Id }));
            f.Sim.Tick(first);
            f.Manager.Tick(15);
            var build = f.Sim.CommandBuffer.FlushCommands().OfType<PlaceBuildingCommand>().Single();
            Assert.That(build.BuildingType, Is.EqualTo(BuildingType.Mill));
            Assert.That(build.VillagerUnitIds, Is.EqualTo(new[] { f.Workers[2].Id }));
        }

        private sealed class MutableCostConfig : SimulationConfig
        {
            public int BarracksCost;
            public override int BarracksWoodCost => BarracksCost;
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly SimulationConfig Config;
            internal readonly GameSimulation Sim;
            internal readonly CommanderGoalManager Manager;
            internal readonly UnitData[] Workers;
            internal Fixture(int count, bool mutableCosts = false, bool twoPlayers = false)
            {
                Config = mutableCosts ? ScriptableObject.CreateInstance<MutableCostConfig>()
                    : ScriptableObject.CreateInstance<SimulationConfig>();
                Sim = new GameSimulation(Config, twoPlayers ? 2 : 1,
                    twoPlayers ? new[] { 0, 1 } : new[] { 0 }, Array.Empty<int>());
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
                var resources = Sim.ResourceManager.GetPlayerResources(0);
                resources.Food = resources.Wood = resources.Gold = resources.Stone = 10000;
                Workers = new UnitData[count];
                for (int i = 0; i < count; i++)
                {
                    var worker = Sim.UnitRegistry.CreateUnit(0, Sim.MapData.TileToWorldFixed(x - 4 - i, z),
                        Fixed32.One, Fixed32.One, Fixed32.One);
                    worker.IsVillager = true;
                    worker.CurrentHealth = worker.MaxHealth = 100;
                    worker.State = UnitState.Idle;
                    Workers[i] = worker;
                }
                var sheep = Sim.UnitRegistry.CreateUnit(0, Sim.MapData.TileToWorldFixed(x - 8, z - 2),
                    Fixed32.One, Fixed32.One, Fixed32.One);
                sheep.IsSheep = true;
                sheep.CurrentHealth = sheep.MaxHealth = 100;
                sheep.State = UnitState.Idle;
                Manager = new CommanderGoalManager(Sim, 0);
            }
            public void Dispose() { Manager.Dispose(); UnityEngine.Object.DestroyImmediate(Config); }
        }
    }
}
