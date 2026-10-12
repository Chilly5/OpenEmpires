using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public sealed class CommanderPhase4GCapabilityTests
    {
        [Test]
        public void ParsesWorkedResourcePatrolWithoutConcreteTargetData()
        {
            CommanderSemanticResult result = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":3,\"location\":\"WorkedResource\",\"resource\":\"Gold\",\"dependsOn\":[]}]}");

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Nodes.Count, Is.EqualTo(1));
            Assert.That(result.Nodes[0].Type, Is.EqualTo(CommanderSemanticNodeType.PatrolArea));
            Assert.That(result.Nodes[0].UnitSelector, Is.EqualTo(CommanderSemanticUnitSelector.Spearman));
            Assert.That(result.Nodes[0].LocationSelector, Is.EqualTo(CommanderSemanticLocationSelector.WorkedResource));
            Assert.That(result.Nodes[0].ResourceType, Is.EqualTo(ResourceType.Gold));
        }

        [Test]
        public void RejectsWorkedResourceActionWithoutResourceType()
        {
            CommanderSemanticResult result = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"MoveUnits\",\"unitSelector\":\"Military\",\"count\":1,\"location\":\"WorkedResource\",\"dependsOn\":[]}]}");

            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void ParsesResearchTechnologyAsBoundedSemanticAction()
        {
            CommanderSemanticResult result = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"ResearchTechnology\",\"technology\":\"BlacksmithDamage\",\"dependsOn\":[]}]}");

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Nodes[0].Type, Is.EqualTo(CommanderSemanticNodeType.ResearchTechnology));
            Assert.That(result.Nodes[0].Technology, Is.EqualTo(TechnologyType.BlacksmithDamage));
        }

        [Test]
        public void ParsesMillNearWorkedFoodWithNoCoordinateAuthority()
        {
            CommanderSemanticResult result = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Mill\",\"count\":1,\"placement\":{\"anchor\":\"WorkedResource\",\"relation\":\"Near\",\"resource\":\"Food\"},\"dependsOn\":[]}]}");

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Nodes[0].BuildingType, Is.EqualTo(BuildingType.Mill));
            Assert.That(result.Nodes[0].PlacementAnchorSelector, Is.EqualTo(CommanderSemanticAnchorSelector.WorkedResource));
            Assert.That(result.Nodes[0].ResourceType, Is.EqualTo(ResourceType.Food));
        }

        [Test]
        public void CapabilityNodeAdmitsThroughExistingGameSideGraphBoundary()
        {
            CommanderSemanticResult result = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":2,\"location\":\"WorkedResource\",\"resource\":\"Gold\",\"dependsOn\":[]}]}");
            var context = new CommanderContext(1, 1, new CommanderResourceSnapshot(0, 0, 0, 0),
                1, 10, 20, 1, "English", new List<CommanderBuildingSnapshot>(),
                new List<CommanderUnitSnapshot>(), new List<CommanderBuildingSnapshot>(),
                new List<string>(), new List<CommanderGoalSnapshot>(),
                new List<CommanderVisibleResourceSnapshot>(), new List<CommanderUnitOptionSnapshot>(),
                new List<CommanderWorkerAllocationSnapshot>(), new List<CommanderVisibleEnemyMilitarySnapshot>());

            Assert.That(CommanderSemanticGraphAdmission.TryAdmit(result, context,
                out CommanderSemanticGraphPlan plan, out _), Is.True);
            Assert.That(plan.Nodes[0].Intent, Is.TypeOf<CapabilityActionIntent>());
        }

        [Test]
        public void CapabilityActionIntent_DtoRoundTripsSemanticSelectors()
        {
            var intent = new CapabilityActionIntent(0,
                CommanderCapabilityActionType.PatrolArea,
                new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 3, 1),
                new CommanderLocationSelector(CommanderLocationSelectorKind.WorkedResource,
                    ResourceType.Gold));

            CommanderIntentDTO dto = CommanderIntentDtoCodec.FromIntent(intent);
            Assert.That(dto.intentType, Is.EqualTo(nameof(CommanderIntentType.CapabilityAction)));
            Assert.That(dto.action, Is.EqualTo(nameof(CommanderCapabilityActionType.PatrolArea)));
            Assert.That(dto.unit, Is.EqualTo(nameof(CommanderUnitSelectorKind.UnitType)));
            Assert.That(dto.location, Is.EqualTo(nameof(CommanderLocationSelectorKind.WorkedResource)));
            Assert.That(dto.resource, Is.EqualTo(nameof(ResourceType.Gold)));
            Assert.That(dto.amount, Is.EqualTo(3));

            CommanderIntentInterpretation roundTrip = CommanderIntentDtoCodec.ValidateAndConvert(dto,
                CreateContext(0));
            Assert.That(roundTrip.Success, Is.True, roundTrip.Reason);
            Assert.That(roundTrip.Intent, Is.TypeOf<CapabilityActionIntent>());
            var restored = (CapabilityActionIntent)roundTrip.Intent;
            Assert.That(restored.ActionType, Is.EqualTo(intent.ActionType));
            Assert.That(restored.UnitSelector.Kind, Is.EqualTo(intent.UnitSelector.Kind));
            Assert.That(restored.UnitSelector.Count, Is.EqualTo(intent.UnitSelector.Count));
            Assert.That(restored.LocationSelector.Kind, Is.EqualTo(intent.LocationSelector.Kind));
            Assert.That(restored.LocationSelector.ResourceType, Is.EqualTo(intent.LocationSelector.ResourceType));
        }

        [Test]
        public void CapabilityValidator_RejectsRepairWithMilitarySelector()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
            try
            {
                var intent = new CapabilityActionIntent(0,
                    CommanderCapabilityActionType.RepairTarget,
                    new CommanderUnitSelector(CommanderUnitSelectorKind.Military),
                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase));

                CommanderIntentValidationResult validation = new CommanderIntentValidator().Validate(
                    intent, simulation, 0);
                Assert.That(validation.IsValid, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void CapabilityIntentResolver_CreatesCapabilityGoalThroughNormalPath()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
            var manager = new CommanderGoalManager(simulation, 0);
            try
            {
                var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.MoveUnits,
                    new CommanderUnitSelector(CommanderUnitSelectorKind.Military),
                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase));

                CommanderIntentResolution resolution = new CommanderIntentResolver().Resolve(
                    intent, simulation, manager);

                Assert.That(resolution.CreatedGoal, Is.True, resolution.Reason);
                Assert.That(resolution.Goal, Is.TypeOf<CommanderCapabilityGoal>());
            }
            finally
            {
                manager.Dispose();
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void SemanticGraph_StoresEveryDependencyForExecutionGating()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
            var manager = new CommanderGoalManager(simulation, 0);
            try
            {
                CommanderContext context = new CommanderContextBuilder().Build(simulation, manager);
                CommanderSemanticResult result = CommanderSemanticJson.Parse(
                    "{\"outcome\":\"Request\",\"nodes\":[" +
                    "{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1}," +
                    "{\"type\":\"PatrolArea\",\"unitSelector\":\"Military\",\"count\":1," +
                    "\"location\":\"PlayerBase\",\"dependsOn\":[0]}]}" );
                Assert.That(CommanderSemanticGraphAdmission.TryAdmit(result, context,
                    out CommanderSemanticGraphPlan plan, out string reason), Is.True, reason);

                plan = manager.ApproveActionPlan(manager.PrepareActionPlan(result,
                    "Trusted fixture approves build and follow-up.", 0), 0);
                IReadOnlyList<CommanderGoal> submitted = manager.SubmitSemanticGraph(plan);
                Assert.That(submitted[1].Dependencies, Has.Count.EqualTo(1));
                Assert.That(submitted[1].Dependencies[0], Is.SameAs(submitted[0]));
            }
            finally
            {
                manager.Dispose();
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void CapabilityExecutor_DoesNotTreatAlliedVisibleUnitAsEnemy()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 2, new[] { 0, 0 }, System.Array.Empty<int>());
            try
            {
                int x = simulation.MapData.Width / 2;
                int z = simulation.MapData.Height / 2;
                simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                UnitData attacker = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(x + 2, z + 2), Fixed32.One,
                    Fixed32.FromFloat(.4f), Fixed32.One);
                attacker.UnitType = 1;
                attacker.MaxHealth = attacker.CurrentHealth = 100;
                UnitData ally = simulation.UnitRegistry.CreateUnit(1,
                    simulation.MapData.TileToWorldFixed(x + 5, z + 5), Fixed32.One,
                    Fixed32.FromFloat(.4f), Fixed32.One);
                ally.UnitType = 1;
                ally.MaxHealth = ally.CurrentHealth = 100;
                for (int ix = x - 10; ix <= x + 10; ix++)
                    for (int iz = z - 10; iz <= z + 10; iz++)
                        simulation.FogOfWar.SetVisible(0, ix, iz);

                var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.AttackTarget,
                    new CommanderUnitSelector(CommanderUnitSelectorKind.Military),
                    new CommanderLocationSelector(CommanderLocationSelectorKind.VisibleEnemy));
                var executor = new CommanderCapabilityExecutor(simulation);


                Assert.That(executor.TryCreateCommand(intent, out _, out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void CapabilityGoal_MilitaryCommandUsesNormalCommandBufferAndRemainsExecuting()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
            var manager = new CommanderGoalManager(simulation, 0);
            try
            {
                int x = simulation.MapData.Width / 2;
                int z = simulation.MapData.Height / 2;
                simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                UnitData military = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(x + 2, z + 2), Fixed32.One,
                    Fixed32.FromFloat(.4f), Fixed32.One);
                military.UnitType = 1;
                military.MaxHealth = military.CurrentHealth = 100;

                var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.MoveUnits,
                    new CommanderUnitSelector(CommanderUnitSelectorKind.Military),
                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase));
                CommanderCapabilityGoal goal = manager.SubmitCapabilityAction(intent);

                manager.Tick(0);

                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Executing));
                Assert.That(simulation.CommandBuffer.FlushCommands(), Has.Some.TypeOf<MoveCommand>());
            }
            finally
            {
                manager.Dispose();
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void DefenseAtOwnedBase_UsesDistinctReachableNativeSlots_AcceleratedFixture()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
            var manager = new CommanderGoalManager(simulation, 0);
            try
            {
                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
                var townCenter = simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                var ids = new List<int>();
                for (int i = 0; i < 6; i++)
                {
                    UnitData unit = simulation.UnitRegistry.CreateUnit(0,
                        simulation.MapData.TileToWorldFixed(x + 8 + i, z + 8), Fixed32.One,
                        Fixed32.FromFloat(.4f), Fixed32.One);
                    unit.UnitType = 1; unit.MaxHealth = unit.CurrentHealth = 100; ids.Add(unit.Id);
                }
                for (int ix = x - 20; ix <= x + 20; ix++)
                    for (int iz = z - 20; iz <= z + 20; iz++) simulation.FogOfWar.SetVisible(0, ix, iz);
                var goal = manager.SubmitCapabilityAction(new CapabilityActionIntent(0,
                    CommanderCapabilityActionType.DefendArea,
                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 6, 1),
                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase)));
                manager.Tick(0);
                var commands = simulation.CommandBuffer.FlushCommands();
                Assert.That(commands, Has.Count.EqualTo(1));
                Assert.That(commands[0], Is.TypeOf<MoveCommand>());
                var move = (MoveCommand)commands[0];
                Assert.That(move.UnitIds, Is.EquivalentTo(ids));
                Assert.That(move.FormationPositions, Has.Length.EqualTo(6));
                var slots = new HashSet<Vector2Int>();
                foreach (var position in move.FormationPositions)
                {
                    var tile = simulation.MapData.WorldToTile(position);
                    Assert.That(simulation.MapData.IsWalkable(tile.x, tile.y, 0, simulation.BuildingRegistry), Is.True);
                    Assert.That(slots.Add(tile), Is.True, "Each exact subject needs a distinct slot.");
                    Assert.That(tile.x >= townCenter.OriginTileX && tile.x < townCenter.OriginTileX + townCenter.TileFootprintWidth
                        && tile.y >= townCenter.OriginTileZ && tile.y < townCenter.OriginTileZ + townCenter.TileFootprintHeight, Is.False);
                }
                simulation.Tick(commands);
                for (int tick = 15; tick <= 15000 && !goal.IsTerminal; tick += 15)
                {
                    simulation.Tick(new List<ICommand>());
                    manager.Tick(tick);
                }
                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason + " / " +
                    string.Join("; ", ids.ConvertAll(id =>
                    {
                        var u = simulation.UnitRegistry.GetUnit(id);
                        return id + ":" + u.State + ":" + u.SimPosition + ":" + u.FinalDestination + ":" + u.HasPath;
                    })));
            }
            finally { manager.Dispose(); Object.DestroyImmediate(config); }
        }

        [Test]
        public void MissingExactMovementSubject_IsNotReportedAsHumanTakeover()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
            var manager = new CommanderGoalManager(simulation, 0);
            try
            {
                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
                simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                var unit = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 8, z + 8),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                unit.UnitType = 1; unit.MaxHealth = unit.CurrentHealth = 100;
                for (int ix = x - 15; ix <= x + 15; ix++)
                    for (int iz = z - 15; iz <= z + 15; iz++) simulation.FogOfWar.SetVisible(0, ix, iz);
                var goal = manager.SubmitCapabilityAction(new CapabilityActionIntent(0,
                    CommanderCapabilityActionType.MoveUnits,
                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 1, 1),
                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase)));
                manager.Tick(0);
                var commands = simulation.CommandBuffer.FlushCommands();
                Assert.That(commands, Has.Count.EqualTo(1));
                unit.CurrentHealth = 0; unit.State = UnitState.Dead;
                manager.Tick(15);
                Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed));
                Assert.That(goal.StatusReason.ToLowerInvariant(), Does.Contain("dead"));
                Assert.That(goal.StatusReason.ToLowerInvariant(), Does.Not.Contain("player took control"));
            }
            finally { manager.Dispose(); Object.DestroyImmediate(config); }
        }

        [TestCase("missing", "missing")]
        [TestCase("foreign", "foreign")]
        [TestCase("garrison", "garrison")]
        public void ExactMovementSubjectLoss_IsClassifiedWithoutSurvivorSubstitution(string loss, string expected)
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 2, new[] { 0, 1 }, System.Array.Empty<int>());
            var manager = new CommanderGoalManager(simulation, 0);
            try
            {
                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
                simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                var exact = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 8, z + 8),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                exact.UnitType = 1; exact.CurrentHealth = exact.MaxHealth = 100;
                var survivor = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 9, z + 8),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                survivor.UnitType = 1; survivor.CurrentHealth = survivor.MaxHealth = 100;
                for (int ix = x - 15; ix <= x + 15; ix++)
                    for (int iz = z - 15; iz <= z + 15; iz++) simulation.FogOfWar.SetVisible(0, ix, iz);
                var goal = manager.SubmitCapabilityAction(new CapabilityActionIntent(0,
                    CommanderCapabilityActionType.DefendArea,
                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 2, 1),
                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase)));
                manager.Tick(0);
                var commands = simulation.CommandBuffer.FlushCommands();
                Assert.That(((MoveCommand)commands[0]).UnitIds, Is.EquivalentTo(new[] { exact.Id, survivor.Id }));
                if (loss == "missing") simulation.UnitRegistry.RemoveUnit(exact.Id);
                else if (loss == "foreign") exact.PlayerId = 1;
                else simulation.UnitRegistry.GarrisonUnit(exact.Id);
                manager.Tick(15);
                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
                Assert.That(goal.StatusReason.ToLowerInvariant(), Does.Contain(expected));
                Assert.That(goal.StatusReason.ToLowerInvariant(), Does.Not.Contain("player took control"));
                Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
            }
            finally { manager.Dispose(); Object.DestroyImmediate(config); }
        }

        [Test]
        public void DistantIdleSubject_CannotCompleteUnprocessedNativeMovement()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
            var manager = new CommanderGoalManager(simulation, 0);
            try
            {
                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
                simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                var unit = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 12, z + 12),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                unit.UnitType = 1; unit.CurrentHealth = unit.MaxHealth = 100;
                for (int ix = x - 18; ix <= x + 18; ix++)
                    for (int iz = z - 18; iz <= z + 18; iz++) simulation.FogOfWar.SetVisible(0, ix, iz);
                var goal = manager.SubmitCapabilityAction(new CapabilityActionIntent(0,
                    CommanderCapabilityActionType.MoveUnits,
                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 1, 1),
                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase)));
                manager.Tick(0); simulation.CommandBuffer.FlushCommands();
                simulation.Tick(new List<ICommand>()); // Issued command intentionally never reached native processing.
                unit.State = UnitState.Idle;
                manager.Tick(15);
                Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed));
            }
            finally { manager.Dispose(); Object.DestroyImmediate(config); }
        }

        [Test]
        public void NearSlotIdleSubject_CannotCompleteBeforeItsNativeMoveIsProcessed()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
            var manager = new CommanderGoalManager(simulation, 0);
            try
            {
                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
                simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                var unit = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 12, z + 12),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                unit.UnitType = 1; unit.CurrentHealth = unit.MaxHealth = 100;
                for (int ix = x - 18; ix <= x + 18; ix++)
                    for (int iz = z - 18; iz <= z + 18; iz++) simulation.FogOfWar.SetVisible(0, ix, iz);
                var goal = manager.SubmitCapabilityAction(new CapabilityActionIntent(0,
                    CommanderCapabilityActionType.MoveUnits,
                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 1, 1),
                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase)));
                manager.Tick(0);
                Assert.That(simulation.CommandBuffer.FlushCommands(), Has.Count.EqualTo(1));
                unit.SimPosition = goal.NativeMovementSlots[0];
                unit.State = UnitState.Idle;
                simulation.Tick(new List<ICommand>()); // The exact boxed Move never executes.
                manager.Tick(15);
                Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed));
            }
            finally { manager.Dispose(); Object.DestroyImmediate(config); }
        }

        [Test]
        public void NativePartialPathAfterPreflight_CannotBecomeInventedArrival()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
            var manager = new CommanderGoalManager(simulation, 0);
            try
            {
                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
                simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                var unit = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 12, z + 12),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                unit.UnitType = 1; unit.CurrentHealth = unit.MaxHealth = 100;
                for (int ix = x - 18; ix <= x + 18; ix++)
                    for (int iz = z - 18; iz <= z + 18; iz++) simulation.FogOfWar.SetVisible(0, ix, iz);
                var goal = manager.SubmitCapabilityAction(new CapabilityActionIntent(0,
                    CommanderCapabilityActionType.MoveUnits,
                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 1, 1),
                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase)));
                manager.Tick(0);
                var commands = simulation.CommandBuffer.FlushCommands();
                Assert.That(commands, Has.Count.EqualTo(1));
                var start = simulation.MapData.WorldToTile(unit.SimPosition);
                simulation.MapData.Tiles[start.x - 1, start.y] = TileType.Water;
                simulation.MapData.Tiles[start.x + 1, start.y] = TileType.Water;
                simulation.MapData.Tiles[start.x, start.y - 1] = TileType.Water;
                simulation.MapData.Tiles[start.x, start.y + 1] = TileType.Water;
                simulation.Tick(commands);
                manager.Tick(15);
                Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed));
                Assert.That(goal.StatusReason.ToLowerInvariant(), Does.Contain("stopped short"));
            }
            finally { manager.Dispose(); Object.DestroyImmediate(config); }
        }

        [Test]
        public void ReplacementNativeMoveToSameEndpoint_CannotCompleteOriginalRequest()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
            var manager = new CommanderGoalManager(simulation, 0);
            try
            {
                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
                simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                var unit = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 10, z + 10),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                unit.UnitType = 1; unit.CurrentHealth = unit.MaxHealth = 100;
                for (int ix = x - 18; ix <= x + 18; ix++)
                    for (int iz = z - 18; iz <= z + 18; iz++) simulation.FogOfWar.SetVisible(0, ix, iz);
                var goal = manager.SubmitCapabilityAction(new CapabilityActionIntent(0,
                    CommanderCapabilityActionType.MoveUnits,
                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 1, 1),
                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase)));
                manager.Tick(0);
                var issued = simulation.CommandBuffer.FlushCommands();
                simulation.Tick(issued);
                Assert.That(goal.NativeMovementSlots, Has.Length.EqualTo(1));
                var unrelated = new MoveCommand(0, new[] { unit.Id }, goal.NativeMovementSlots[0]);
                simulation.Tick(new List<ICommand> { unrelated });
                for (int tick = 15; tick <= 15000 && !goal.IsTerminal; tick += 15)
                { simulation.Tick(new List<ICommand>()); manager.Tick(tick); }
                Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed));
            }
            finally { manager.Dispose(); Object.DestroyImmediate(config); }
        }

        [Test]
        public void UnrelatedNativeStopNearEndpoint_CannotCompleteOriginalRequest()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
            var manager = new CommanderGoalManager(simulation, 0);
            try
            {
                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
                simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                var unit = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 10, z + 10),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                unit.UnitType = 1; unit.CurrentHealth = unit.MaxHealth = 100;
                for (int ix = x - 18; ix <= x + 18; ix++)
                    for (int iz = z - 18; iz <= z + 18; iz++) simulation.FogOfWar.SetVisible(0, ix, iz);
                var goal = manager.SubmitCapabilityAction(new CapabilityActionIntent(0,
                    CommanderCapabilityActionType.MoveUnits,
                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 1, 1),
                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase)));
                manager.Tick(0);
                simulation.Tick(simulation.CommandBuffer.FlushCommands());
                Assert.That(goal.NativeMovementProcessed[0], Is.True);
                unit.SimPosition = goal.NativeMovementEndpoints[0];
                simulation.Tick(new List<ICommand> { new StopCommand(0, new[] { unit.Id }) });
                manager.Tick(15);
                Assert.That(goal.Status, Is.Not.EqualTo(CommanderGoalStatus.Completed));
            }
            finally { manager.Dispose(); Object.DestroyImmediate(config); }
        }

        [Test]
        public void MixedRadiusFormation_UsesLegalNativeEndpointsAndCompletes_AcceleratedFixture()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
            var manager = new CommanderGoalManager(simulation, 0);
            try
            {
                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
                var tc = simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                var ids = new List<int>();
                for (int i = 0; i < 2; i++)
                {
                    var unit = simulation.UnitRegistry.CreateUnit(0,
                        simulation.MapData.TileToWorldFixed(x + 10 + i, z + 10), Fixed32.One,
                        Fixed32.FromFloat(i == 0 ? .4f : 1.2f), Fixed32.One);
                    unit.UnitType = 1; unit.CurrentHealth = unit.MaxHealth = 100; ids.Add(unit.Id);
                }
                for (int ix = x - 20; ix <= x + 20; ix++)
                    for (int iz = z - 20; iz <= z + 20; iz++) simulation.FogOfWar.SetVisible(0, ix, iz);
                var goal = manager.SubmitCapabilityAction(new CapabilityActionIntent(0,
                    CommanderCapabilityActionType.DefendArea,
                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 2, 1),
                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase)));
                manager.Tick(0);
                var commands = simulation.CommandBuffer.FlushCommands();
                Assert.That(commands, Has.Count.EqualTo(1), goal.StatusReason);
                simulation.Tick(commands);
                var anchor = simulation.MapData.WorldToTile(tc.SimPosition);
                var nativeTiles = new HashSet<Vector2Int>();
                foreach (int id in ids)
                {
                    var unit = simulation.UnitRegistry.GetUnit(id);
                    var tile = simulation.MapData.WorldToTile(unit.FinalDestination);
                    Assert.That(Mathf.Max(Mathf.Abs(tile.x - anchor.x), Mathf.Abs(tile.y - anchor.y)), Is.LessThanOrEqualTo(10));
                    Assert.That(simulation.MapData.IsWalkable(tile.x, tile.y, 0, simulation.BuildingRegistry), Is.True);
                    Assert.That(nativeTiles.Add(tile), Is.True);
                }
                for (int tick = 15; tick <= 15000 && !goal.IsTerminal; tick += 15)
                { simulation.Tick(new List<ICommand>()); manager.Tick(tick); }
                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
            }
            finally { manager.Dispose(); Object.DestroyImmediate(config); }
        }

        [Test]
        public void NativeMovementWithNoProgress_BecomesBoundedBlocker()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
            var manager = new CommanderGoalManager(simulation, 0);
            try
            {
                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
                simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                var unit = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 12, z + 12),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                unit.UnitType = 1; unit.CurrentHealth = unit.MaxHealth = 100;
                for (int ix = x - 18; ix <= x + 18; ix++)
                    for (int iz = z - 18; iz <= z + 18; iz++) simulation.FogOfWar.SetVisible(0, ix, iz);
                var goal = manager.SubmitCapabilityAction(new CapabilityActionIntent(0,
                    CommanderCapabilityActionType.MoveUnits,
                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 1, 1),
                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase)));
                manager.Tick(0);
                simulation.Tick(simulation.CommandBuffer.FlushCommands());
                unit.MoveSpeed = Fixed32.Zero;
                for (int tick = 15; tick <= 2500 && goal.Status != CommanderGoalStatus.Blocked; tick += 15)
                { simulation.Tick(new List<ICommand>()); manager.Tick(tick); }
                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked), goal.StatusReason);
                Assert.That(goal.StatusReason, Does.Contain("no progress"));
            }
            finally { manager.Dispose(); Object.DestroyImmediate(config); }
        }

        [Test]
        public void TemporarilyUnavailableExactMover_IsNotDeathOrHumanTakeover()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
            var manager = new CommanderGoalManager(simulation, 0);
            try
            {
                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
                simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                var unit = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 12, z + 12),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                unit.UnitType = 1; unit.CurrentHealth = unit.MaxHealth = 100;
                for (int ix = x - 18; ix <= x + 18; ix++)
                    for (int iz = z - 18; iz <= z + 18; iz++) simulation.FogOfWar.SetVisible(0, ix, iz);
                var goal = manager.SubmitCapabilityAction(new CapabilityActionIntent(0,
                    CommanderCapabilityActionType.MoveUnits,
                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 1, 1),
                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase)));
                manager.Tick(0); simulation.CommandBuffer.FlushCommands();
                simulation.Tick(new List<ICommand>());
                unit.State = UnitState.MovingToGarrison;
                manager.Tick(15);
                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
                Assert.That(goal.StatusReason.ToLowerInvariant(), Does.Contain("temporarily unavailable"));
                Assert.That(goal.StatusReason.ToLowerInvariant(), Does.Not.Contain("player took control"));
            }
            finally { manager.Dispose(); Object.DestroyImmediate(config); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InvalidDefenseObjective_EnqueuesNoNativeMove(bool outsideBounds)
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
            var manager = new CommanderGoalManager(simulation, 0);
            try
            {
                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
                var tc = simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                var unit = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 20, z + 20),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                unit.UnitType = 1; unit.CurrentHealth = unit.MaxHealth = 100;
                for (int ix = x - 25; ix <= x + 25; ix++)
                    for (int iz = z - 25; iz <= z + 25; iz++) simulation.FogOfWar.SetVisible(0, ix, iz);
                if (outsideBounds) tc.SimPosition = simulation.MapData.TileToWorldFixed(-1, z);
                else
                {
                    var anchor = simulation.MapData.WorldToTile(tc.SimPosition);
                    for (int ix = anchor.x - 10; ix <= anchor.x + 10; ix++)
                        for (int iz = anchor.y - 10; iz <= anchor.y + 10; iz++) simulation.MapData.Tiles[ix, iz] = TileType.Water;
                }
                var goal = manager.SubmitCapabilityAction(new CapabilityActionIntent(0,
                    CommanderCapabilityActionType.DefendArea,
                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 1, 1),
                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase)));
                manager.Tick(0);
                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
                Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
            }
            finally { manager.Dispose(); Object.DestroyImmediate(config); }
        }

        [Test]
        public void CapabilityGoal_HumanOverrideBlocksWithoutFalseCompletion()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
            var manager = new CommanderGoalManager(simulation, 0);
            try
            {
                int x = simulation.MapData.Width / 2;
                int z = simulation.MapData.Height / 2;
                simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                UnitData military = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(x + 10, z + 10), Fixed32.One,
                    Fixed32.FromFloat(.4f), Fixed32.One);
                military.UnitType = 1;
                military.MaxHealth = military.CurrentHealth = 100;
                for (int ix = x - 18; ix <= x + 18; ix++)
                    for (int iz = z - 18; iz <= z + 18; iz++) simulation.FogOfWar.SetVisible(0, ix, iz);
                var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.MoveUnits,
                    new CommanderUnitSelector(CommanderUnitSelectorKind.Military),
                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase));
                CommanderCapabilityGoal goal = manager.SubmitCapabilityAction(intent);
                manager.Tick(0);
                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Executing));
                Assert.That(simulation.CommandBuffer.FlushCommands(), Has.Some.TypeOf<MoveCommand>());
                simulation.CommandBuffer.EnqueueCommand(new MoveCommand(0, new[] { military.Id },
                    simulation.MapData.TileToWorldFixed(x + 12, z + 12)));
                simulation.CommandBuffer.FlushCommands();
                manager.Tick(15);
                manager.Tick(150);

                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
                Assert.That(simulation.CommandBuffer.FlushCommands(), Has.None.TypeOf<MoveCommand>());
            }
            finally
            {
                manager.Dispose();
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void ResultBinding_UsesOnlyTheExactProducedUnitSet()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
            try
            {
                int x = simulation.MapData.Width / 2;
                int z = simulation.MapData.Height / 2;
                for (int tx = x - 12; tx <= x + 12; tx++)
                    for (int tz = z - 12; tz <= z + 12; tz++)
                    {
                        simulation.MapData.Tiles[tx, tz] = TileType.Grass;
                        simulation.MapData.ForestDensity[tx, tz] = 0f;
                        simulation.FogOfWar.SetVisible(0, tx, tz);
                    }
                simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                UnitData unrelated = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(x + 1, z + 1), Fixed32.One,
                    Fixed32.FromFloat(.4f), Fixed32.One);
                unrelated.UnitType = 2;
                var produced = new List<int>();
                for (int i = 0; i < 3; i++)
                {
                    UnitData spearman = simulation.UnitRegistry.CreateUnit(0,
                        simulation.MapData.TileToWorldFixed(x + 2 + i, z + 6), Fixed32.One,
                        Fixed32.FromFloat(.4f), Fixed32.One);
                    spearman.UnitType = 1;
                    spearman.MaxHealth = spearman.CurrentHealth = 100;
                    produced.Add(spearman.Id);
                }

                var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.PatrolArea,
                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 3, 1),
                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase));
                var binding = CommanderResultBinding.ForUnits(produced, 7, 0, simulation);
                var executor = new CommanderCapabilityExecutor(simulation);

                var foreignSimulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
                foreignSimulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                for (int i = 0; i < 4; i++)
                {
                    UnitData duplicateId = foreignSimulation.UnitRegistry.CreateUnit(0,
                        foreignSimulation.MapData.TileToWorldFixed(x + 2 + i, z + 2), Fixed32.One,
                        Fixed32.FromFloat(.4f), Fixed32.One);
                    duplicateId.UnitType = i == 0 ? 2 : 1;
                    duplicateId.MaxHealth = duplicateId.CurrentHealth = 100;
                }
                Assert.That(new CommanderCapabilityExecutor(foreignSimulation)
                    .TryCreateCommand(intent, binding, out _, out _), Is.False,
                    "A result from another simulation must not bind coincidentally identical IDs.");

                Assert.That(executor.TryCreateCommand(intent, binding, out ICommand command, out string reason),
                    Is.True, reason);
                Assert.That(command, Is.TypeOf<PatrolCommand>());
                Assert.That(((PatrolCommand)command).UnitIds, Is.EqualTo(produced.ToArray()));
                Assert.That(System.Array.IndexOf(((PatrolCommand)command).UnitIds, unrelated.Id),
                    Is.EqualTo(-1));

                simulation.SetPlayerCivilizations(new[] { Civilization.HolyRomanEmpire });
                foreach (int id in produced) simulation.UnitRegistry.GetUnit(id).UnitType = 12;
                bool civBound = executor.TryCreateCommand(intent, binding, out _, out string civReason);

                CommanderSemanticResult scout = CommanderSemanticJson.Parse(
                    "{\"outcome\":\"Request\",\"nodes\":[" +
                    "{\"type\":\"EnsureUnitCount\",\"unit\":\"Scout\",\"count\":1,\"quantityMode\":\"New\"}," +
                    "{\"type\":\"ScoutArea\",\"unitSelector\":\"Scout\",\"count\":1," +
                    "\"location\":\"PlayerBase\",\"dependsOn\":[0],\"resultFromNode\":0}]}");
                bool scoutAdmitted = CommanderSemanticGraphAdmission.TryAdmit(scout, CreateContext(0), out _, out _);

                foreach (UnitData unit in simulation.UnitRegistry.GetAllUnits()) unit.CurrentHealth = 0;
                BuildingData mill = simulation.CreateBuilding(0, BuildingType.Mill, x - 6, z, false);
                var rally = new CapabilityActionIntent(0, CommanderCapabilityActionType.SetRallyPoint,
                    new CommanderUnitSelector(CommanderUnitSelectorKind.Military),
                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase));
                bool structureBound = executor.TryCreateCommand(rally,
                    CommanderResultBinding.ForBuilding(mill.Id, 8, 0, simulation), out command, out reason);
                Assert.That(civBound && scout.IsValid && scoutAdmitted
                    && CommanderIntentCatalog.IsSupportedUnit(4) && structureBound, Is.True,
                    $"civ={civBound} ({civReason}); Scout parsed={scout.IsValid}, admitted={scoutAdmitted}; structure={structureBound} ({reason})");
                Assert.That(((SetRallyPointCommand)command).BuildingId, Is.EqualTo(mill.Id));

                using (var manager = new CommanderGoalManager(simulation, 0))
                {
                    CommanderSemanticResult graph = CommanderSemanticJson.Parse(
                        "{\"outcome\":\"Request\",\"nodes\":[" +
                        "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3}," +
                        "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":3," +
                        "\"location\":\"PlayerBase\",\"dependsOn\":[0],\"resultFromNode\":0}]}");
                    Assert.That(CommanderSemanticGraphAdmission.TryAdmit(graph, CreateContext(0), out var plan, out _), Is.True);
                    plan = manager.ApproveActionPlan(manager.PrepareActionPlan(graph,
                        "Trusted fixture approves new units and exact patrol.", 0), 0);
                    var goals = manager.SubmitSemanticGraph(plan);
                    var newIds = new List<int>();
                    for (int i = 0; i < 3; i++)
                    {
                        UnitData unit = simulation.UnitRegistry.CreateUnit(0,
                            simulation.MapData.TileToWorldFixed(x + 2 + i, z + 3), Fixed32.One,
                            Fixed32.FromFloat(.4f), Fixed32.One);
                        unit.UnitType = 12;
                        unit.MaxHealth = unit.CurrentHealth = 100;
                        newIds.Add(unit.Id);
                    }
                    ((EnsureUnitCountGoal)goals[0]).CaptureUnitResult(newIds, 0);
                    goals[0].SetStatus(CommanderGoalStatus.Completed, "Focused result fixture");
                    simulation.CommandBuffer.EnqueueCommand(new MoveCommand(0, new[] { newIds[0] },
                        simulation.MapData.TileToWorldFixed(x + 9, z + 9)));
                    simulation.CommandBuffer.FlushCommands();
                    manager.Tick(0);
                    manager.Tick(1050);
                    Assert.That(goals[1].Status, Is.EqualTo(CommanderGoalStatus.Blocked),
                        "A result-bound goal must not reclaim manually overridden results after the ordinary protection timeout.");
                    Assert.That(simulation.CommandBuffer.FlushCommands(), Has.None.TypeOf<PatrolCommand>());
                }
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void ResultBinding_RejectsIncompatibleDependency()
        {
            CommanderSemanticResult result = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[" +
                "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3}," +
                "{\"type\":\"PatrolArea\",\"unitSelector\":\"Archer\",\"count\":3," +
                "\"location\":\"PlayerBase\",\"dependsOn\":[0],\"resultFromNode\":0}]}" );

            Assert.That(result.IsValid, Is.False);
        }

        private static CommanderContext CreateContext(int playerId)
        {
            return new CommanderContext(playerId, 1,
                new CommanderResourceSnapshot(0, 0, 0, 0), 1, 10, 20, 1, "English",
                new List<CommanderBuildingSnapshot>(), new List<CommanderUnitSnapshot>(),
                new List<CommanderBuildingSnapshot>(), new List<string>(),
                new List<CommanderGoalSnapshot>(), new List<CommanderVisibleResourceSnapshot>(),
                new List<CommanderUnitOptionSnapshot>(), new List<CommanderWorkerAllocationSnapshot>(),
                new List<CommanderVisibleEnemyMilitarySnapshot>());
        }
    }
}
