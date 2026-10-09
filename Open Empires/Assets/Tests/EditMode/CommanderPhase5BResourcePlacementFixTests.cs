using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5BFix")]
    public sealed class CommanderPhase5BResourcePlacementFixTests
    {
        private SimulationConfig config;
        private GameSimulation sim;
        private CommanderGoalManager manager;
        private UnitData worker;
        private ResourceNodeData resource;
        private int x, z;

        [SetUp] public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            sim = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            sim.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            x = sim.MapData.Width / 2; z = sim.MapData.Height / 2;
            typeof(MapData).GetField("holeMap", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(sim.MapData, null);
            foreach (var node in sim.MapData.GetAllResourceNodes()) node.RemainingAmount = 0;
            for (int a = x - 25; a < x + 25; a++)
                for (int b = z - 25; b < z + 25; b++)
                {
                    sim.MapData.Tiles[a, b] = TileType.Grass;
                    sim.MapData.ForestDensity[a, b] = 0;
                    sim.MapData.FoundationCount[a, b] = 0;
                    sim.FogOfWar.SetVisible(0, a, b);
                }
            var tc = sim.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
            tc.AutoProduceVillagers = false;
            worker = Worker(x + 6, z + 7);
            sim.ResourceManager.GetPlayerResources(0).Wood = 1000;
            sim.ResourceManager.GetPlayerResources(0).Food = 1000;
            sim.ResourceManager.GetPlayerResources(0).Gold = 1000;
            manager = new CommanderGoalManager(sim, 0);
        }
        [TearDown] public void TearDown() { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }

        private UnitData Worker(int a, int b)
        {
            var unit = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(a, b),
                Fixed32.FromFloat(2), Fixed32.FromFloat(.4f), Fixed32.One);
            unit.IsVillager = true; unit.UnitType = 0;
            unit.MaxHealth = unit.CurrentHealth = 100; unit.State = UnitState.Idle;
            return unit;
        }
        private void Resource(ResourceType type)
        {
            resource = sim.MapData.AddResourceNode(type, sim.MapData.TileToWorldFixed(x + 12, z + 9), 10000);
            sim.FogOfWar.SetVisible(0, resource.TileX, resource.TileZ);
        }
        private CommanderSemanticResult Parse(string structure, string anchor, string type, string source = null, string constraints = "")
            => CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\""
                + structure + "\",\"count\":1,\"placement\":{\"anchor\":\"" + anchor
                + "\",\"relation\":\"Near\",\"resource\":\"" + type + "\""
                + (source == null ? "" : ",\"sourceKind\":\"" + source + "\"") + "}" + constraints + "}]}");
        private BuildStructureGoal Submit(CommanderSemanticResult parsed, string input)
        {
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            Assert.That(CommanderSemanticAdmission.TryCreateTacticalIntent(parsed.Nodes[0],
                new CommanderContextBuilder().Build(sim, manager), out var intent, out var reason), Is.True, reason);
            var scope = manager.PrepareActionPlan(parsed, input, 0);
            return (BuildStructureGoal)manager.SubmitSemanticGraph(manager.ApproveActionPlan(scope, 0)).Single();
        }

        // Reproduces the audit's normalized payload before introducing a new selector.
        [TestCase(false)] [TestCase(true)]
        public void AuditReproduction_UnworkedWood_FailsAtAnchorBeforeBuilder(bool idleOnly)
        {
            Resource(ResourceType.Wood);
            var parsed = Parse("Lumber Yard", "WorkedResource", "Wood", constraints: idleOnly
                ? ",\"constraints\":[{\"type\":\"PreferredWorkers\",\"mode\":\"IdleOnly\"}]" : "");
            var goal = Submit(parsed, idleOnly ? "Build a Lumber Yard near the woodline with one idle villager."
                : "Build a Lumber Yard near the woodline.");
            manager.Tick(0);
            TestContext.WriteLine($"normalized anchor={goal.PlacementAnchorSelector}, relation={goal.PlacementRelation}, resource={goal.PlacementResourceType}, idleOnly={idleOnly}; stage={goal.PlacementBlocker}; reason={goal.StatusReason}");
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            Assert.That(goal.PlacementBlocker, Is.EqualTo(CommanderPlacementBlocker.AnchorUnavailable));
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [TestCase("Lumber Yard", "Wood", "Tree", false, 0)]
        [TestCase("Lumber Yard", "Wood", "Tree", true, 0)]
        [TestCase("Mill", "Food", "Berries", false, 0)]
        [TestCase("Mill", "Food", "Berries", true, 0)]
        [TestCase("Lumber Yard", "Wood", "Tree", true, 1)]
        [TestCase("Mill", "Food", "Berries", true, 1)]
        public void VisibleUnworkedResource_ConstructsNativelyToCompletion(string structure, string type, string source, bool idleOnly, int preExisting)
        {
            Assert.That(CommanderIntentCatalog.TryResolveStructure(structure, out var buildingType), Is.True);
            if (preExisting > 0)
                sim.CreateBuilding(0, buildingType, x - 15, z - 15, false, true);
            Resource((ResourceType)Enum.Parse(typeof(ResourceType), type));
            var goal = Submit(Parse(structure, "VisibleResource", type, source, idleOnly
                ? ",\"constraints\":[{\"type\":\"PreferredWorkers\",\"mode\":\"IdleOnly\"}]" : ""),
                "Build " + structure + " near visible " + source + (idleOnly ? " with one idle villager" : ""));
            manager.Tick(0);
            sim.Tick();
            var created = sim.BuildingRegistry.GetBuilding(goal.PlacedBuildingId);
            Assert.That(created, Is.Not.Null);
            Assert.That(created.IsUnderConstruction, Is.True);
            Assert.That(CommanderTaskBoardProjection.Capture(manager, null, 0).Cards.Single().Progress,
                Is.EqualTo("Completed buildings: 0 / 1"), "Only this request's result belongs in placed-building progress.");
            Assert.That(worker.ConstructionTargetBuildingId, Is.EqualTo(created.Id));
            Assert.That(worker.TargetResourceNodeId, Is.LessThan(0), "Must not issue a gather to fabricate a worked anchor.");
            for (int i = 0; i < 12000 && !goal.IsTerminal; i++) { manager.Tick(sim.CurrentTick); sim.Tick(); }
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
            Assert.That(created.IsUnderConstruction, Is.False);
            Assert.That(created.CurrentHealth, Is.GreaterThan(0));
            Assert.That(goal.PlacedBuildingId, Is.EqualTo(created.Id));
            Assert.That(CommanderTaskBoardProjection.Capture(manager, null, 0).Cards.Single().Status, Is.EqualTo(CommanderTaskStatus.Completed));
            Assert.That(CommanderTaskBoardProjection.Capture(manager, null, 0).Cards.Single().Progress,
                Is.EqualTo("Completed buildings: 1 / 1"));
            Assert.That(sim.BuildingRegistry.GetAllBuildings().Count(b => b.Type == buildingType && !b.IsUnderConstruction),
                Is.EqualTo(preExisting + 1));
            TestContext.WriteLine($"native {structure} #{created.Id} at {created.OriginTileX},{created.OriginTileZ}; worker={worker.Id}; completed tick={sim.CurrentTick}");
        }

        [Test] public void ConcurrentPlacedRequests_ReportTheirOwnNativeResults()
        {
            Resource(ResourceType.Wood);
            Worker(x + 7, z + 8);
            var first = Submit(Parse("Lumber Yard", "VisibleResource", "Wood", "Tree"), "Build first Lumber Yard near wood");
            var second = Submit(Parse("Lumber Yard", "VisibleResource", "Wood", "Tree"), "Build second Lumber Yard near wood");
            for (int i = 0; i < 12000 && (!first.IsTerminal || !second.IsTerminal); i++)
            { manager.Tick(sim.CurrentTick); sim.Tick(); }
            Assert.That(first.Status, Is.EqualTo(CommanderGoalStatus.Completed), first.StatusReason);
            Assert.That(second.Status, Is.EqualTo(CommanderGoalStatus.Completed), second.StatusReason);
            Assert.That(first.PlacedBuildingId, Is.Not.EqualTo(second.PlacedBuildingId));
            var cards = CommanderTaskBoardProjection.Capture(manager, null, 0).Cards;
            Assert.That(cards.Count, Is.EqualTo(2));
            Assert.That(cards.All(card => card.Progress == "Completed buildings: 1 / 1"), Is.True);
        }

        [Test] public void UnplacedGlobalObjective_RetainsOwnedTotalAcrossCancellation()
        {
            sim.CreateBuilding(0, BuildingType.House, x - 15, z - 15, false, true);
            var goal = manager.SubmitBuildStructure(BuildingType.House);
            manager.Tick(0);
            Assert.That(goal.TargetTotal, Is.EqualTo(2));
            Assert.That(CommanderTaskBoardProjection.Capture(manager, null, 0).Cards.Single().Progress,
                Is.EqualTo("Completed buildings: 1 / 2"));
            manager.CancelGoal(goal.GoalId);
            var card = CommanderTaskBoardProjection.Capture(manager, null, 0).Cards.Single();
            Assert.That(card.Status, Is.EqualTo(CommanderTaskStatus.Cancelled));
            Assert.That(card.Progress, Is.EqualTo("Completed buildings: 1 / 2"));
        }

        [Test] public void ExplicitlyWorkedResource_RemainsStrict()
        {
            Resource(ResourceType.Food);
            var goal = Submit(Parse("Mill", "WorkedResource", "Food", "Berries"), "Build near berries my villagers are working");
            manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            Worker(x + 7, z + 8);
            sim.CommandBuffer.EnqueueCommand(new GatherCommand(0, new[] { worker.Id }, resource.Id));
            for (int i = 0; i < 600 && !sim.BuildingRegistry.GetAllBuildings().Any(b => b.Type == BuildingType.Mill); i++)
            { manager.Tick(sim.CurrentTick); sim.Tick(); }
            Assert.That(sim.BuildingRegistry.GetAllBuildings().Any(b => b.Type == BuildingType.Mill), Is.True,
                goal.StatusReason + "; target=" + worker.TargetResourceNodeId + "; state=" + worker.State
                + "; visibility=" + sim.FogOfWar.GetVisibility(0, resource.TileX, resource.TileZ));
        }

        [Test] public void ProtectedGold_IsPreservedAndIdleBuilderChosen()
        {
            Resource(ResourceType.Wood);
            var gold = sim.MapData.AddResourceNode(ResourceType.Gold, sim.MapData.TileToWorldFixed(x + 8, z + 14), 10000);
            worker.TargetResourceNodeId = gold.Id; worker.State = UnitState.Gathering;
            var idle = Worker(x + 7, z + 8);
            var goal = Submit(Parse("Lumber Yard", "VisibleResource", "Wood", "Tree",
                ",\"constraints\":[{\"type\":\"ProtectedResource\",\"resource\":\"Gold\"}]"), "Do not take workers off gold");
            manager.Tick(0); sim.Tick();
            Assert.That(worker.TargetResourceNodeId, Is.EqualTo(gold.Id));
            Assert.That(idle.ConstructionTargetBuildingId, Is.GreaterThanOrEqualTo(0));
            Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Blocked));
        }

        [TestCase(true)] [TestCase(false)] public void MissingResourceOrBuilder_IsActionableAndCreatesNothing(bool missingResource)
        {
            if (!missingResource) { Resource(ResourceType.Wood); worker.CurrentHealth = 0; }
            var goal = Submit(Parse("Lumber Yard", "VisibleResource", "Wood", "Tree"), "Build near woodline");
            manager.Tick(0);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            Assert.That(goal.PlacementBlocker.ToString(), Is.EqualTo(missingResource ? "AnchorUnavailable" : "NoEligibleBuilder"));
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [TestCase("House")] [TestCase("Barracks")] public void LivingBuilderControls_CompleteNatively(string structure)
        {
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"" + structure + "\",\"count\":1}]}");
            var goal = Submit(parsed, "Build a " + structure);
            for (int i = 0; i < 12000 && !goal.IsTerminal; i++) { manager.Tick(sim.CurrentTick); sim.Tick(); }
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
            Assert.That(sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == goal.StructureType).IsUnderConstruction, Is.False);
        }

        [Test] public void PlacementDto_RoundTripPreservesSourceAndConstraints()
        {
            var parsed = Parse("Mill", "VisibleResource", "Food", "Berries",
                ",\"constraints\":[{\"type\":\"PreferredWorkers\",\"mode\":\"IdleOnly\"}]");
            var context = new CommanderContextBuilder().Build(sim, manager);
            Assert.That(CommanderSemanticAdmission.TryCreateTacticalIntent(parsed.Nodes[0], context, out var intent, out var reason), Is.True, reason);
            var roundtrip = CommanderIntentDtoCodec.InterpretJson(CommanderIntentDtoCodec.ToJson(CommanderIntentDtoCodec.FromIntent(intent)), context);
            Assert.That(roundtrip.Success, Is.True, roundtrip.Reason);
            var build = (BuildStructureIntent)roundtrip.Intent;
            Assert.That(build.PlacementAnchorSelector?.ToString(), Is.EqualTo("VisibleResource"));
            Assert.That(build.PlacementResourceType, Is.EqualTo(ResourceType.Food));
            Assert.That(build.Constraints.Single(), Is.TypeOf<PreferredWorkersConstraint>());
            Assert.That(build.GetType().GetProperty("PlacementSourceKind").GetValue(build), Is.EqualTo(ResourceSourceKind.Berries));
        }

        [TestCase("{\"anchor\":\"VisibleResource\",\"relation\":\"Near\"}")]
        [TestCase("{\"anchor\":\"VisibleResource\",\"relation\":\"Near\",\"resource\":\"Wood\",\"sourceKind\":\"Berries\"}")]
        [TestCase("{\"anchor\":\"VisibleResource\",\"relation\":\"Near\",\"resource\":\"Wood\",\"x\":100}")]
        [TestCase("{\"anchor\":\"VisibleResource\",\"relation\":\"Near\",\"resource\":\"Wood\",\"ordinal\":1}")]
        public void InvalidResourcePlacement_FailsClosed(string placement)
        {
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Lumber Yard\",\"count\":1,\"placement\":" + placement + "}]}");
            Assert.That(parsed.IsValid, Is.False);
            Assert.That(parsed.Nodes, Is.Empty);
        }

        [Test] public void BerrySourceCannotSubstituteVisibleFarmOrCarcass()
        {
            Resource(ResourceType.Food);
            resource.IsCarcass = true;
            var goal = Submit(Parse("Mill", "VisibleResource", "Food", "Berries"), "Build near berries");
            manager.Tick(0);
            Assert.That(goal.PlacementBlocker, Is.EqualTo(CommanderPlacementBlocker.AnchorUnavailable));
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test] public void HiddenResource_DoesNotResolveOrPlace()
        {
            resource = sim.MapData.AddResourceNode(ResourceType.Wood, sim.MapData.TileToWorldFixed(1, 1), 10000);
            Assert.That(sim.FogOfWar.GetVisibility(0, 1, 1), Is.Not.EqualTo(TileVisibility.Visible));
            var goal = Submit(Parse("Lumber Yard", "VisibleResource", "Wood", "Tree"), "Build near woodline");
            manager.Tick(0);
            Assert.That(goal.PlacementBlocker, Is.EqualTo(CommanderPlacementBlocker.AnchorUnavailable));
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test] public void IdleOnlyRestriction_PersistsThroughBlockedRetry()
        {
            Resource(ResourceType.Wood);
            worker.TargetResourceNodeId = resource.Id; worker.State = UnitState.Gathering;
            var goal = Submit(Parse("Lumber Yard", "VisibleResource", "Wood", "Tree",
                ",\"constraints\":[{\"type\":\"PreferredWorkers\",\"mode\":\"IdleOnly\"}]"), "with one idle villager");
            manager.Tick(0);
            Assert.That(goal.PlacementBlocker, Is.EqualTo(CommanderPlacementBlocker.NoEligibleBuilder));
            manager.Tick(300);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
            Assert.That(worker.TargetResourceNodeId, Is.EqualTo(resource.Id));
            var idle = Worker(x + 7, z + 8);
            manager.Tick(600); sim.Tick();
            Assert.That(idle.ConstructionTargetBuildingId, Is.GreaterThanOrEqualTo(0));
        }

        [Test] public void HumanProtectedIdleWorker_IsNotTakenForConstruction()
        {
            Resource(ResourceType.Wood);
            sim.CommandBuffer.EnqueueCommand(new StopCommand(0, new[] { worker.Id }));
            sim.Tick();
            var goal = Submit(Parse("Lumber Yard", "VisibleResource", "Wood", "Tree"), "Build near woodline");
            manager.Tick(sim.CurrentTick);
            Assert.That(goal.PlacementBlocker, Is.EqualTo(CommanderPlacementBlocker.NoEligibleBuilder));
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test] public void StructuralAuthority_RejectsChangingPlacementSource()
        {
            var parsed = Parse("Mill", "VisibleResource", "Food", "Berries");
            var changed = new BuildStructureIntent(0, BuildingType.Mill, 1,
                placementAnchorSelector: (CommanderSemanticAnchorSelector)Enum.Parse(typeof(CommanderSemanticAnchorSelector), "VisibleResource"),
                placementRelation: CommanderSemanticPlacementRelation.Near, placementResourceType: ResourceType.Food,
                placementSourceKind: ResourceSourceKind.Farm);
            Assert.Throws<ArgumentException>(() => manager.PrepareGroundedActionPlan(parsed, new CommanderIntent[] { changed }, "near berries", 0));
            Assert.That(manager.Goals, Is.Empty);
        }

        [Test] public void NoLegalNearbyFootprint_BlocksWithoutUnrelatedFallback()
        {
            Resource(ResourceType.Wood);
            for (int a = resource.TileX - 10; a <= resource.TileX + 10; a++)
                for (int b = resource.TileZ - 10; b <= resource.TileZ + 10; b++)
                    sim.MapData.Tiles[a, b] = TileType.Water;
            var goal = Submit(Parse("Lumber Yard", "VisibleResource", "Wood", "Tree"), "Build near woodline");
            manager.Tick(0);
            Assert.That(goal.PlacementBlocker, Is.EqualTo(CommanderPlacementBlocker.NoLegalCandidate));
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
        }

        [Test] public void CompoundBuildThenAllocate_CannotCompleteBeforeNativeBuilding()
        {
            Resource(ResourceType.Food);
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":["
                + "{\"type\":\"BuildStructure\",\"structure\":\"Mill\",\"count\":1,\"placement\":{\"anchor\":\"VisibleResource\",\"relation\":\"Near\",\"resource\":\"Food\",\"sourceKind\":\"Berries\"}},"
                + "{\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\",\"countMode\":\"Exact\",\"count\":1,\"workers\":{\"state\":\"Any\"},\"destination\":{\"resource\":\"Food\",\"sourceKind\":\"Berries\"},\"dependsOn\":[0]}]}");
            var scope = manager.PrepareActionPlan(parsed, "Build a mill near berries then assign one villager", 0);
            var goals = manager.SubmitSemanticGraph(manager.ApproveActionPlan(scope, 0));
            manager.Tick(0); sim.Tick(); manager.Tick(15);
            var foundation = sim.BuildingRegistry.GetAllBuildings().Single(b => b.Type == BuildingType.Mill);
            Assert.That(foundation.IsUnderConstruction, Is.True);
            var card = CommanderTaskBoardProjection.Capture(manager, null, 0).Cards.Single();
            Assert.That(card.Status, Is.Not.EqualTo(CommanderTaskStatus.Completed));
            Assert.That(goals[1].Status, Is.EqualTo(CommanderGoalStatus.WaitingForPrerequisite));
            for (int i = 0; i < 12000 && !goals.All(g => g.IsTerminal); i++) { manager.Tick(sim.CurrentTick); sim.Tick(); }
            Assert.That(foundation.IsUnderConstruction, Is.False);
            Assert.That(goals.All(g => g.Status == CommanderGoalStatus.Completed), Is.True, string.Join(";", goals.Select(g => g.StatusReason)));
            Assert.That(worker.TargetResourceNodeId, Is.EqualTo(resource.Id));
            Assert.That(CommanderTaskBoardProjection.Capture(manager, null, 0).Cards.Single().Status, Is.EqualTo(CommanderTaskStatus.Completed));
        }
    }
}
