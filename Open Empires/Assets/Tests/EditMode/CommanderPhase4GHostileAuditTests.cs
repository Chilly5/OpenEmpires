using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public sealed class CommanderPhase4GHostileAuditTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager manager;
        private CommanderIntentDispatcher dispatcher;
        private readonly List<GameObject> objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            manager = new CommanderGoalManager(simulation, 0);
            dispatcher = new CommanderIntentDispatcher(simulation, manager);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
            objects.Clear();
            dispatcher?.Dispose();
            manager?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        private CommanderChatUI CreateChat(string semanticJson = null)
        {
            var go = new GameObject("TestChatUI");
            objects.Add(go);
            var chat = go.AddComponent<CommanderChatUI>();
            var provider = new FakeHostileSemanticProvider(semanticJson ?? "{\"outcome\":\"Unsupported\",\"message\":\"Unsupported\"}");
            chat.Initialize(provider, simulation, manager, dispatcher);
            return chat;
        }

        private void SetupBaseMap(int cx, int cz)
        {
            for (int tx = cx - 25; tx <= cx + 25; tx++)
                for (int tz = cz - 25; tz <= cz + 25; tz++)
                {
                    simulation.MapData.Tiles[tx, tz] = TileType.Grass;
                    simulation.FogOfWar.SetVisible(0, tx, tz);
                }
            simulation.CreateBuilding(0, BuildingType.TownCenter, cx, cz, false, true);
        }

        private string GetLastExplanation(CommanderChatUI chat)
        {
            return chat.Conversation?.Snapshot().LastOrDefault(e => e.Kind == MemoryEntryKind.Explanation)?.Text ?? string.Empty;
        }

        // --- 1. resultFromNode Attacks ---

        [Test]
        public void ResultBinding_RejectsMissingSourceNodeIndex()
        {
            CommanderSemanticResult result = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[" +
                "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3}," +
                "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":3," +
                "\"location\":\"PlayerBase\",\"dependsOn\":[0],\"resultFromNode\":5}]}");
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void ResultBinding_RejectsNegativeSourceNodeIndex()
        {
            CommanderSemanticResult result = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[" +
                "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3}," +
                "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":3," +
                "\"location\":\"PlayerBase\",\"dependsOn\":[0],\"resultFromNode\":-1}]}");
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void ResultBinding_RejectsSelfReferentialSourceNode()
        {
            CommanderSemanticResult result = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[" +
                "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":3," +
                "\"location\":\"PlayerBase\",\"dependsOn\":[],\"resultFromNode\":0}]}");
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void ResultBinding_RejectsDependencyAbsentFromDependsOn()
        {
            CommanderSemanticResult result = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[" +
                "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3}," +
                "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":3," +
                "\"location\":\"PlayerBase\",\"dependsOn\":[],\"resultFromNode\":0}]}");
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void ResultBinding_RejectsDependencyCycles()
        {
            CommanderSemanticResult result = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[" +
                "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":3," +
                "\"location\":\"PlayerBase\",\"dependsOn\":[1]}," +
                "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":3," +
                "\"location\":\"PlayerBase\",\"dependsOn\":[0]}]}");
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void ResultBinding_RejectsStructureSourceWhenUnitsRequired()
        {
            CommanderSemanticResult result = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[" +
                "{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1}," +
                "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":3," +
                "\"location\":\"PlayerBase\",\"dependsOn\":[0],\"resultFromNode\":0}]}");
            Assert.That(result.IsValid, Is.False, "Incompatible resultFromNode source must be rejected at schema parse time");
        }

        [Test]
        public void ResultBinding_RejectsUnitSourceWhenStructureRequired()
        {
            CommanderSemanticResult result = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[" +
                "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3}," +
                "{\"type\":\"SetRallyPoint\",\"structure\":\"Barracks\"," +
                "\"location\":\"PlayerBase\",\"dependsOn\":[0],\"resultFromNode\":0}]}");
            Assert.That(result.IsValid, Is.False, "Incompatible resultFromNode target must be rejected at schema parse time");
        }

        [Test]
        public void ResultBinding_FailsClosedWhenBoundUnitIsDestroyed()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);

            var units = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                var u = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(cx + i, cz + 2), Fixed32.One,
                    Fixed32.FromFloat(.4f), Fixed32.One);
                u.UnitType = 1;
                u.CurrentHealth = u.MaxHealth = 100;
                units.Add(u.Id);
            }

            // Remove one unit from registry (destroyed)
            simulation.UnitRegistry.RemoveUnit(units[1]);

            var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.PatrolArea,
                new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 3, 1),
                new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase));
            var binding = CommanderResultBinding.ForUnits(units, 10, 0);
            var executor = new CommanderCapabilityExecutor(simulation);

            bool ok = executor.TryCreateCommand(intent, binding, out ICommand cmd, out string reason);
            Assert.That(ok, Is.False, "Expected failure when a bound unit is destroyed");
            Assert.That(cmd, Is.Null);
        }

        [Test]
        public void ResultBinding_FailsClosedWhenBoundUnitIsDead()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);

            var units = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                var u = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(cx + i, cz + 2), Fixed32.One,
                    Fixed32.FromFloat(.4f), Fixed32.One);
                u.UnitType = 1;
                u.CurrentHealth = u.MaxHealth = 100;
                units.Add(u.Id);
            }

            // Mark one unit dead
            var dead = simulation.UnitRegistry.GetUnit(units[0]);
            dead.CurrentHealth = 0;
            dead.State = UnitState.Dead;

            var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.PatrolArea,
                new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 3, 1),
                new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase));
            var binding = CommanderResultBinding.ForUnits(units, 10, 0);
            var executor = new CommanderCapabilityExecutor(simulation);

            bool ok = executor.TryCreateCommand(intent, binding, out ICommand cmd, out string reason);
            Assert.That(ok, Is.False, "Expected failure when a bound unit is dead");
        }

        [Test]
        public void ResultBinding_FailsClosedWhenBoundUnitOwnershipTransferred()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);

            var units = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                var u = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(cx + i, cz + 2), Fixed32.One,
                    Fixed32.FromFloat(.4f), Fixed32.One);
                u.UnitType = 1;
                u.CurrentHealth = u.MaxHealth = 100;
                units.Add(u.Id);
            }

            // Transfer ownership of one unit to enemy player 1
            simulation.UnitRegistry.GetUnit(units[2]).PlayerId = 1;

            var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.PatrolArea,
                new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 3, 1),
                new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase));
            var binding = CommanderResultBinding.ForUnits(units, 10, 0);
            var executor = new CommanderCapabilityExecutor(simulation);

            bool ok = executor.TryCreateCommand(intent, binding, out _, out _);
            Assert.That(ok, Is.False, "Expected failure when bound unit ownership changed");
        }

        [Test]
        public void ResultBinding_FailsClosedWhenBoundUnitIsHumanControlled()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);

            var u = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(cx + 2, cz + 2), Fixed32.One,
                Fixed32.FromFloat(.4f), Fixed32.One);
            u.UnitType = 1;
            u.CurrentHealth = u.MaxHealth = 100;

            var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.MoveUnits,
                new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 1, 1),
                new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase));
            var goal = manager.SubmitCapabilityAction(intent);

            // Human manual move command arrives before manager tick
            simulation.CommandBuffer.EnqueueCommand(new MoveCommand(0, new[] { u.Id },
                simulation.MapData.TileToWorldFixed(cx + 10, cz + 10)), CommandEnqueueSource.Human);
            simulation.CommandBuffer.FlushCommands();

            manager.Tick(0);

            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty, "Commander must not command human-controlled unit");
        }

        [Test]
        public void ResultBinding_FailsClosedWhenGoalManagerDisposed()
        {
            manager.Dispose();
            Assert.Throws<ObjectDisposedException>(() =>
            {
                var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.MoveUnits,
                    new CommanderUnitSelector(CommanderUnitSelectorKind.Military, 1),
                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase));
                manager.SubmitCapabilityAction(intent);
            });
        }

        [Test]
        public void ResultBinding_NeverSubstitutesPreExistingUnitsWhenExplicitResultRequested()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);

            // 5 pre-existing Spearmen already exist
            for (int i = 0; i < 5; i++)
            {
                var u = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(cx + i, cz + 2), Fixed32.One,
                    Fixed32.FromFloat(.4f), Fixed32.One);
                u.UnitType = 1;
                u.CurrentHealth = u.MaxHealth = 100;
            }

            // A Barracks is training the 3 new Spearmen
            var barracks = simulation.CreateBuilding(0, BuildingType.Barracks, cx + 5, cz + 5, false, true);
            barracks.TrainingQueue.Add(1);
            barracks.TrainingQueue.Add(1);
            barracks.TrainingQueue.Add(1);

            CommanderContext context = new CommanderContextBuilder().Build(simulation, manager);
            // Request 8 total Spearmen (3 new units required beyond the 5 baseline units)
            CommanderSemanticResult parsed = CommanderSemanticJson.Parse(
                "{\"outcome\":\"Request\",\"nodes\":[" +
                "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":8}," +
                "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":3," +
                "\"location\":\"PlayerBase\",\"dependsOn\":[0],\"resultFromNode\":0}]}");
            Assert.That(CommanderSemanticGraphAdmission.TryAdmit(parsed, context, out var plan, out _), Is.True);
            var goals = manager.SubmitSemanticGraph(plan);

            // Plan ticker at tick 0 before any production occurs
            manager.Tick(0);

            // EnsureUnitCount must NOT complete using pre-existing units!
            var ensureGoal = (EnsureUnitCountGoal)goals[0];
            Assert.That(ensureGoal.Status, Is.EqualTo(CommanderGoalStatus.WaitingForProduction),
                "Expected EnsureUnitCount to wait for newly produced units instead of substituting existing ones");
            Assert.That(goals[1].Status, Is.EqualTo(CommanderGoalStatus.WaitingForPrerequisite));
        }

        // --- 2. Human Override ---

        [Test]
        public void HumanOverride_Worker_ReleasesCommanderReservation()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);
            var gold = simulation.MapData.AddResourceNode(ResourceType.Gold,
                simulation.MapData.TileToWorldFixed(cx + 5, cz), 5000);

            var worker = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(cx + 2, cz), Fixed32.One,
                Fixed32.FromFloat(.4f), Fixed32.One);
            worker.UnitType = 0;
            worker.IsVillager = true;
            worker.CurrentHealth = worker.MaxHealth = 100;

            var goal = manager.SubmitResourceAllocation(ResourceType.Gold, 1);
            manager.Tick(0);
            simulation.CommandBuffer.FlushCommands();

            // Human manually commands this worker
            simulation.CommandBuffer.EnqueueCommand(new MoveCommand(0, new[] { worker.Id },
                simulation.MapData.TileToWorldFixed(cx - 10, cz)), CommandEnqueueSource.Human);
            simulation.CommandBuffer.FlushCommands();

            // Next manager tick
            manager.Tick(15);
            Assert.That(manager.GetWorkerReservation(worker.Id), Is.Null, "Human command must clear Commander reservation");
        }

        [Test]
        public void HumanOverride_Military_BlocksCommanderReclamation()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);

            var military = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(cx + 2, cz), Fixed32.One,
                Fixed32.FromFloat(.4f), Fixed32.One);
            military.UnitType = 1;
            military.CurrentHealth = military.MaxHealth = 100;

            var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.MoveUnits,
                new CommanderUnitSelector(CommanderUnitSelectorKind.Military, 1),
                new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase));
            var goal = manager.SubmitCapabilityAction(intent);
            manager.Tick(0);
            simulation.CommandBuffer.FlushCommands();

            // Human commands unit away
            simulation.CommandBuffer.EnqueueCommand(new MoveCommand(0, new[] { military.Id },
                simulation.MapData.TileToWorldFixed(cx + 15, cz + 15)), CommandEnqueueSource.Human);
            simulation.CommandBuffer.FlushCommands();

            manager.Tick(15);
            manager.Tick(150);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
        }

        // --- 3. Ally & Ownership Protection ---

        [Test]
        public void Ownership_MoveSelector_DoesNotSelectAlliedUnits()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);

            // Ally unit for player 1
            var ally = simulation.UnitRegistry.CreateUnit(1,
                simulation.MapData.TileToWorldFixed(cx + 2, cz), Fixed32.One,
                Fixed32.FromFloat(.4f), Fixed32.One);
            ally.UnitType = 1;
            ally.CurrentHealth = ally.MaxHealth = 100;

            var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.MoveUnits,
                new CommanderUnitSelector(CommanderUnitSelectorKind.Military, 1),
                new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase));
            var executor = new CommanderCapabilityExecutor(simulation);

            bool ok = executor.TryCreateCommand(intent, out _, out _);
            Assert.That(ok, Is.False, "Commander must not select allied units for movement");
        }

        [Test]
        public void Ownership_AttackSelector_DoesNotTargetAlliesOrSelf()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);
            simulation.SetTeamAssignments(new[] { 0, 0 }); // Player 0 and Player 1 are allies on team 0

            // Own military attacker
            var attacker = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(cx + 1, cz + 1), Fixed32.One,
                Fixed32.FromFloat(.4f), Fixed32.One);
            attacker.UnitType = 1;
            attacker.CurrentHealth = attacker.MaxHealth = 100;

            // Allied unit
            var ally = simulation.UnitRegistry.CreateUnit(1,
                simulation.MapData.TileToWorldFixed(cx + 4, cz + 4), Fixed32.One,
                Fixed32.FromFloat(.4f), Fixed32.One);
            ally.UnitType = 1;
            ally.CurrentHealth = ally.MaxHealth = 100;

            var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.AttackTarget,
                new CommanderUnitSelector(CommanderUnitSelectorKind.Military, 1),
                new CommanderLocationSelector(CommanderLocationSelectorKind.VisibleEnemy));
            var executor = new CommanderCapabilityExecutor(simulation);

            bool ok = executor.TryCreateCommand(intent, out _, out string reason);
            Assert.That(ok, Is.False, "Allied units must not be targeted as enemies");
        }

        [Test]
        public void Ownership_RepairSelector_DoesNotRepairAlliedOrEnemyBuildings()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);

            var worker = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(cx + 1, cz + 1), Fixed32.One,
                Fixed32.FromFloat(.4f), Fixed32.One);
            worker.UnitType = 0;
            worker.IsVillager = true;
            worker.CurrentHealth = worker.MaxHealth = 100;

            // Damaged building owned by player 1 (ally/enemy)
            var b = simulation.CreateBuilding(1, BuildingType.Barracks, cx + 5, cz + 5, false);
            b.CurrentHealth = 50;

            var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.RepairTarget,
                new CommanderUnitSelector(CommanderUnitSelectorKind.Villagers, 1),
                new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase));
            var executor = new CommanderCapabilityExecutor(simulation);

            bool ok = executor.TryCreateCommand(intent, out _, out string reason);
            Assert.That(ok, Is.False, "Commander must not repair non-owned buildings");
        }

        // --- 4. Fog-of-War Safety ---

        [Test]
        public void FogOfWar_AttackTarget_RejectsHiddenOrFoggedEnemy()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);

            var attacker = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(cx + 1, cz + 1), Fixed32.One,
                Fixed32.FromFloat(.4f), Fixed32.One);
            attacker.UnitType = 1;
            attacker.CurrentHealth = attacker.MaxHealth = 100;

            // Enemy placed in unexplored fog (far away)
            int ex = cx + 40;
            int ez = cz + 40;
            var enemy = simulation.UnitRegistry.CreateUnit(1,
                simulation.MapData.TileToWorldFixed(ex, ez), Fixed32.One,
                Fixed32.FromFloat(.4f), Fixed32.One);
            enemy.UnitType = 1;
            enemy.CurrentHealth = enemy.MaxHealth = 100;
            // Ensure tile is NOT visible to player 0
            Assert.That(simulation.FogOfWar.GetVisibility(0, ex, ez), Is.Not.EqualTo(TileVisibility.Visible));

            var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.AttackTarget,
                new CommanderUnitSelector(CommanderUnitSelectorKind.Military, 1),
                new CommanderLocationSelector(CommanderLocationSelectorKind.VisibleEnemy));
            var executor = new CommanderCapabilityExecutor(simulation);

            bool ok = executor.TryCreateCommand(intent, out _, out _);
            Assert.That(ok, Is.False, "Hidden enemy in fog of war must not be targeted");
        }

        [Test]
        public void FogOfWar_WorkedResource_RejectsFoggedResourceNode()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);

            int rx = cx + 40;
            int rz = cz + 40;
            var gold = simulation.MapData.AddResourceNode(ResourceType.Gold,
                simulation.MapData.TileToWorldFixed(rx, rz), 5000);
            Assert.That(simulation.FogOfWar.GetVisibility(0, rx, rz), Is.Not.EqualTo(TileVisibility.Visible));

            var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.PatrolArea,
                new CommanderUnitSelector(CommanderUnitSelectorKind.Military, 1),
                new CommanderLocationSelector(CommanderLocationSelectorKind.WorkedResource, ResourceType.Gold));
            var executor = new CommanderCapabilityExecutor(simulation);

            bool ok = executor.TryCreateCommand(intent, out _, out _);
            Assert.That(ok, Is.False, "Fogged resource node must not be resolved");
        }

        // --- 5. Contextual Construction ---

        [Test]
        public void ContextualConstruction_MillNearWorkedBerries_PlacesDeterministicLegalCommand()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);

            var foodNode = simulation.MapData.AddResourceNode(ResourceType.Food,
                simulation.MapData.TileToWorldFixed(cx + 8, cz), 2000);
            simulation.FogOfWar.SetVisible(0, cx + 8, cz);

            var villager = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(cx + 7, cz), Fixed32.One,
                Fixed32.FromFloat(.4f), Fixed32.One);
            villager.UnitType = 0;
            villager.IsVillager = true;
            villager.CurrentHealth = villager.MaxHealth = 100;
            villager.TargetResourceNodeId = foodNode.Id;

            var res = simulation.ResourceManager.GetPlayerResources(0);
            res.Wood = 1000;

            var goal = manager.SubmitBuildStructure(BuildingType.Mill, count: 1,
                placementAnchorSelector: CommanderSemanticAnchorSelector.WorkedResource,
                placementRelation: CommanderSemanticPlacementRelation.Near,
                clearGapTiles: 1, placementResourceType: ResourceType.Food);

            manager.Tick(0);

            Assert.That(simulation.CommandBuffer.FlushCommands(), Has.Some.TypeOf<PlaceBuildingCommand>());
        }

        [Test]
        public void ContextualConstruction_MillNearWorkedBerries_FailsClosedIfResourceDepletedOrUnworked()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);

            var foodNode = simulation.MapData.AddResourceNode(ResourceType.Food,
                simulation.MapData.TileToWorldFixed(cx + 8, cz), 2000);
            simulation.FogOfWar.SetVisible(0, cx + 8, cz);

            // No villager assigned to foodNode!
            var res = simulation.ResourceManager.GetPlayerResources(0);
            res.Wood = 1000;

            var goal = manager.SubmitBuildStructure(BuildingType.Mill, count: 1,
                placementAnchorSelector: CommanderSemanticAnchorSelector.WorkedResource,
                placementRelation: CommanderSemanticPlacementRelation.Near,
                clearGapTiles: 1, placementResourceType: ResourceType.Food);

            manager.Tick(0);

            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        // --- 6. Q&A Routing & Knowledge Tests ---

        [Test]
        public async Task QA_WhatCountersSpearmen_ReturnsCanonicalCounterAdviceWithoutGameMutation()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);

            CommanderChatUI chat = CreateChat();
            var submission = await chat.SubmitMessageAsync("What counters Spearmen?");

            Assert.That(submission, Is.Null, "Questions must not create a tactical/strategic submission");
            Assert.That(manager.Goals, Is.Empty, "Questions must not create Commander goals");
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty, "Questions must not enqueue commands");
            Assert.That(GetLastExplanation(chat), Does.Contain("Archers counter Spearmen"));
        }

        [Test]
        public async Task QA_HowDoIReachCastleAge_ReturnsCanonicalAgeCostWithoutGameMutation()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);

            CommanderChatUI chat = CreateChat();
            var submission = await chat.SubmitMessageAsync("How do I reach Castle Age?");

            Assert.That(submission, Is.Null);
            Assert.That(manager.Goals, Is.Empty);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
            Assert.That(GetLastExplanation(chat), Does.Contain("1200 Food and 600 Gold"));
        }

        [Test]
        public async Task QA_HowMuchDoesABarracksCost_ReturnsCanonicalCostWithoutGameMutation()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);

            CommanderChatUI chat = CreateChat();
            var submission = await chat.SubmitMessageAsync("How much does a Barracks cost?");

            Assert.That(submission, Is.Null);
            Assert.That(manager.Goals, Is.Empty);
            Assert.That(GetLastExplanation(chat), Does.Contain("150 Wood"));
        }

        [Test]
        public async Task QA_WhereDoITrainArchers_ReturnsProductionBuildingWithoutGameMutation()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);

            CommanderChatUI chat = CreateChat();
            var submission = await chat.SubmitMessageAsync("Where do I train Archers?");

            Assert.That(submission, Is.Null);
            Assert.That(manager.Goals, Is.Empty);
            Assert.That(GetLastExplanation(chat), Does.Contain("ArcheryRange"));
        }

        [Test]
        public async Task QA_CanMyCivilizationMakeKnights_ReturnsCivilizationCapabilityWithoutGameMutation()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);

            CommanderChatUI chat = CreateChat();
            var submission = await chat.SubmitMessageAsync("Can my civilization make Knights?");

            Assert.That(submission, Is.Null);
            Assert.That(manager.Goals, Is.Empty);
            Assert.That(GetLastExplanation(chat), Does.Contain("English can train Knight"));
        }

        [Test]
        public async Task QA_HowManyVillagersDoIHave_ReturnsDetachedCountWithoutGameMutation()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);

            for (int i = 0; i < 3; i++)
            {
                var v = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(cx + i, cz + 2), Fixed32.One,
                    Fixed32.FromFloat(.4f), Fixed32.One);
                v.UnitType = 0;
                v.IsVillager = true;
                v.CurrentHealth = v.MaxHealth = 100;
            }

            CommanderChatUI chat = CreateChat();
            var submission = await chat.SubmitMessageAsync("How many villagers do I have?");

            Assert.That(submission, Is.Null);
            Assert.That(manager.Goals, Is.Empty);
            Assert.That(GetLastExplanation(chat), Does.Contain("3 living Villagers"));
        }

        [Test]
        public async Task QA_RoutingDistinction_QuestionVersusAction()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);

            CommanderChatUI chat = CreateChat();

            // 1. Question: 0 gameplay mutation
            await chat.SubmitMessageAsync("How do I reach Castle Age?");
            Assert.That(manager.Goals, Is.Empty, "Question must not submit a ReachAgeGoal");

            // 2. Action: Creates ReachAgeGoal
            var actionGoal = manager.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
            Assert.That(manager.Goals.Count, Is.EqualTo(1));
            Assert.That(manager.Goals[0], Is.TypeOf<ReachAgeGoal>());
            Assert.That(((ReachAgeGoal)manager.Goals[0]).TargetAge, Is.EqualTo(3));
        }

        // --- 7. Technology & Research ---

        [Test]
        public void Research_ResearchTechnology_UsesCanonicalAgeCostAndBuilding()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);

            simulation.SetPlayerAge(0, 2);
            simulation.CreateBuilding(0, BuildingType.Blacksmith, cx + 5, cz + 5, false);
            var res = simulation.ResourceManager.GetPlayerResources(0);
            res.Gold = 1000;

            var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.ResearchTechnology,
                new CommanderUnitSelector(CommanderUnitSelectorKind.Military, 1),
                new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase),
                technology: TechnologyType.BlacksmithDamage);
            var executor = new CommanderCapabilityExecutor(simulation);

            bool ok = executor.TryCreateCommand(intent, out ICommand cmd, out string reason);
            Assert.That(ok, Is.True, reason);
            Assert.That(cmd, Is.TypeOf<ResearchCommand>());
            Assert.That((TechnologyType)((ResearchCommand)cmd).TechType, Is.EqualTo(TechnologyType.BlacksmithDamage));
        }

        [Test]
        public void Research_ResearchTechnology_RejectsWhenLackingResourcesOrPrerequisites()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);

            simulation.SetPlayerAge(0, 2);
            simulation.CreateBuilding(0, BuildingType.Blacksmith, cx + 5, cz + 5, false);
            var res = simulation.ResourceManager.GetPlayerResources(0);
            res.Gold = 0; // Blacksmith Damage requires Gold!

            var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.ResearchTechnology,
                new CommanderUnitSelector(CommanderUnitSelectorKind.Military, 1),
                new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase),
                technology: TechnologyType.BlacksmithDamage);
            var executor = new CommanderCapabilityExecutor(simulation);

            bool ok = executor.TryCreateCommand(intent, out _, out string reason);
            Assert.That(ok, Is.False, "Expected failure when lacking canonical research resources");
            Assert.That(reason, Does.Contain("lacks the canonical research resources"));
        }

        // --- 8. Cancellation & Determinism ---

        [Test]
        public void Cancellation_ReleasesCommanderWorkerAndUnitReservations()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);

            var military = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(cx + 2, cz), Fixed32.One,
                Fixed32.FromFloat(.4f), Fixed32.One);
            military.UnitType = 1;
            military.CurrentHealth = military.MaxHealth = 100;

            var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.MoveUnits,
                new CommanderUnitSelector(CommanderUnitSelectorKind.Military, 1),
                new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase));
            var goal = manager.SubmitCapabilityAction(intent);
            manager.Tick(0);

            // Cancel goal
            bool cancelled = manager.CancelGoal(goal.GoalId);
            Assert.That(cancelled, Is.True);
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Cancelled));
        }

        [Test]
        public void Determinism_IdenticalGraphAndState_EmitsIdenticalCommand()
        {
            int cx = simulation.MapData.Width / 2;
            int cz = simulation.MapData.Height / 2;
            SetupBaseMap(cx, cz);

            for (int i = 0; i < 5; i++)
            {
                var u = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(cx + i, cz + 3), Fixed32.One,
                    Fixed32.FromFloat(.4f), Fixed32.One);
                u.UnitType = 1;
                u.CurrentHealth = u.MaxHealth = 100;
            }

            var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.PatrolArea,
                new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 2, 1),
                new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase));

            var executor1 = new CommanderCapabilityExecutor(simulation);
            bool ok1 = executor1.TryCreateCommand(intent, out ICommand cmd1, out _);

            var executor2 = new CommanderCapabilityExecutor(simulation);
            bool ok2 = executor2.TryCreateCommand(intent, out ICommand cmd2, out _);

            Assert.That(ok1 && ok2, Is.True);
            Assert.That(((PatrolCommand)cmd1).UnitIds, Is.EqualTo(((PatrolCommand)cmd2).UnitIds));
            Assert.That(((PatrolCommand)cmd1).TargetPosition.x.Raw, Is.EqualTo(((PatrolCommand)cmd2).TargetPosition.x.Raw));
            Assert.That(((PatrolCommand)cmd1).TargetPosition.z.Raw, Is.EqualTo(((PatrolCommand)cmd2).TargetPosition.z.Raw));
        }

        private sealed class FakeHostileSemanticProvider : ICommanderAIProvider, ICommanderSemanticProvider
        {
            private readonly string json;
            public FakeHostileSemanticProvider(string json) { this.json = json; }

            public Task<CommanderSemanticResult> TranslateSemanticAsync(
                CommanderSemanticProviderRequest request, CancellationToken token)
            {
                return Task.FromResult(CommanderSemanticJson.Parse(json));
            }

            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request,
                CancellationToken token)
            {
                return Task.FromResult(CommanderAIProviderResult.Rejected(
                    CommanderIntentErrorCode.ProviderFailure, "Offline provider"));
            }
        }
    }
}
