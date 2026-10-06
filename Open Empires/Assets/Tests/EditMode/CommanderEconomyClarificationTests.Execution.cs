using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public sealed partial class CommanderEconomyClarificationTests
    {
        [Test]
        public void QuantityModes_KeepBaselineTransferAndAllMatchingDistinct()
        {
            foreach (string mode in new[] { "SelectedCount", "Additional", "TargetTotal" })
            using (var f = new EconomyFixture())
            {
                var food = f.Node(ResourceType.Food, 20);
                var secondFood = f.Node(ResourceType.Food, 24); // Capacity for six existing plus four selected assignments.
                var existing = Enumerable.Range(0, 6).Select(i => f.Worker(i)).ToArray();
                for (int i = 0; i < existing.Length; i++) f.Assigned(existing[i], i < 3 ? food : secondFood);
                var idle = Enumerable.Range(6, 4).Select(i => f.Worker(i)).ToArray();
                var goal = f.Submit(mode, "Exact", "4", "Any", "", "Food", "Any");
                f.Manager.Tick(0);
                var commands = f.Commands();
                if (mode == "TargetTotal")
                { Assert.That(commands, Is.Empty); Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed)); }
                else
                {
                    int[] ids = Subjects(commands);
                    Assert.That(ids.Distinct().Count(), Is.EqualTo(4));
                    if (mode == "Additional") Assert.That(ids, Is.SubsetOf(idle.Select(w => w.Id)));
                    f.Manager.Tick(15); Assert.That(f.Commands(), Is.Empty, "A retry may not issue a duplicate batch before processing.");
                }
            }
            using (var f = new EconomyFixture())
            {
                f.Node(ResourceType.Food, 20);
                var workers = Enumerable.Range(0, 4).Select(i => f.Worker(i)).ToArray();
                f.Submit("SelectedCount", "AllMatching", "", "Idle", "", "Food", "Berries");
                f.Manager.Tick(0); Assert.That(Subjects(f.Commands()), Is.EquivalentTo(workers.Select(w => w.Id)));
                var later = f.Worker(8); f.Manager.Tick(15); Assert.That(Subjects(f.Commands()), Has.No.Member(later.Id));
            }
            foreach (int amount in new[] { 0, 201 })
            using (var f = new EconomyFixture())
            {
                f.Node(ResourceType.Food, 20);
                for (int i = 0; i < amount; i++) f.Worker(i % 12);
                var goal = f.Submit("SelectedCount", "AllMatching", "", "Idle", "", "Food", "Any");
                f.Manager.Tick(0); Assert.That(f.Commands(), Is.Empty); Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            }
            using (var f = new EconomyFixture())
            {
                var food = f.Node(ResourceType.Food, 6);
                var existing = f.Worker(0); f.Assigned(existing, food);
                var expected = new[] { f.Worker(1), f.Worker(2), f.Worker(3) };
                var goal = f.Submit("TargetTotal", "Exact", "4", "Any", "", "Food", "Any");
                f.Manager.Tick(0); var commands = f.Commands();
                Assert.That(Subjects(commands), Is.EquivalentTo(expected.Select(w => w.Id)));
                f.Simulation.Tick(commands); f.Manager.Tick(15);
                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed));
            }
        }

        [Test]
        public void WorkerSelection_RespectsOwnershipAuthorityAndCurrentResource()
        {
            using (var f = new EconomyFixture())
            {
                var food = f.Node(ResourceType.Food, 20);
                var expected = Enumerable.Range(0, 4).Select(i => f.Worker(i)).ToArray();
                f.Worker(5, 1); var dead = f.Worker(6); dead.CurrentHealth = 0;
                var builder = f.Worker(7); builder.State = UnitState.Constructing;
                var queued = f.Worker(8); queued.CommandQueue.Add(QueuedCommand.GatherWaypoint(food.Position, food.Id));
                var garrisoned = f.Worker(10); f.Simulation.UnitRegistry.GarrisonUnit(garrisoned.Id);
                var headingToGarrison = f.Worker(11); headingToGarrison.State = UnitState.MovingToGarrison;
                f.Simulation.SetTeamAssignments(new[] { 0, 0 }); // Allied workers are still not owned workers.
                var protectedWorker = f.Worker(9);
                f.Simulation.CommandBuffer.EnqueueCommand(new StopCommand(0, new[] { protectedWorker.Id })); f.Commands();
                f.Submit("SelectedCount", "Exact", "4", "Idle", "", "Food", "Any");
                f.Manager.Tick(0); Assert.That(Subjects(f.Commands()), Is.EquivalentTo(expected.Select(w => w.Id)));
            }
            using (var f = new EconomyFixture())
            {
                f.Node(ResourceType.Food, 20); var protectedWorker = f.Worker(0);
                var owner = f.Manager.SubmitBuildStructure(BuildingType.House);
                Assert.That(f.Manager.TryReserveWorker(owner.GoalId, protectedWorker.Id, CommanderWorkerReservationType.Builder), Is.True);
                f.Manager.SuspendGoal(owner.GoalId);
                var other = f.Submit("SelectedCount", "Exact", "1", "Idle", "", "Food", "Any");
                f.Manager.Tick(0);
                Assert.That(f.Commands(), Is.Empty); Assert.That(other.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            }
            using (var f = new EconomyFixture())
            {
                var wood = f.Node(ResourceType.Wood, 22); var food = f.Node(ResourceType.Food, 24); f.Node(ResourceType.Gold, 20);
                var expected = Enumerable.Range(0, 3).Select(i => f.Worker(i)).ToArray();
                foreach (var worker in expected) f.Assigned(worker, wood);
                f.Assigned(f.Worker(4), food); f.Worker(5);
                f.Submit("SelectedCount", "Exact", "3", "Gathering", "Wood", "Gold", "Any");
                f.Manager.Tick(0); Assert.That(Subjects(f.Commands()), Is.EquivalentTo(expected.Select(w => w.Id)));
            }
            using (var f = new EconomyFixture())
            {
                var wood = f.Node(ResourceType.Wood, 22); f.Node(ResourceType.Gold, 20);
                f.Assigned(f.Worker(0), wood); f.Assigned(f.Worker(1), wood); f.Worker(2);
                var shortage = f.Submit("SelectedCount", "Exact", "3", "Gathering", "Wood", "Gold", "Any");
                f.Manager.Tick(0); Assert.That(f.Commands(), Is.Empty); Assert.That(shortage.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            }
        }

        [Test]
        public void TargetResolution_RejectsUnknownExhaustedUnreachableAndWrongSources()
        {
            foreach (string reason in new[] { "missing", "depleted", "hidden", "unreachable", "wrongSource" })
            using (var f = new EconomyFixture())
            {
                var worker = f.Worker(0);
                if (reason != "missing")
                {
                    var node = f.Node(ResourceType.Food, 20);
                    if (reason != "wrongSource") node.IsCarcass = true;
                    if (reason == "depleted") node.RemainingAmount = 0;
                    if (reason == "hidden") { f.Simulation.FogOfWar.SetVisionCheat(0, false); f.Simulation.FogOfWar.DemoteAllVisible(0); }
                    if (reason == "unreachable")
                    {
                        var tile = f.Simulation.MapData.WorldToTile(worker.SimPosition);
                        for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++)
                            if (dx != 0 || dz != 0) f.Simulation.MapData.Tiles[tile.x + dx, tile.y + dz] = TileType.Water;
                    }
                }
                var goal = f.Submit("SelectedCount", "Exact", "1", "Idle", "", "Food", "Sheep");
                f.Manager.Tick(0); Assert.That(f.Commands(), Is.Empty, reason); Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked), reason);
                Assert.That(f.Manager.GetWorkerReservation(worker.Id), Is.Null);
            }
            foreach (int owner in new[] { UnitData.NeutralPlayerId, 1 })
            using (var f = new EconomyFixture())
            {
                var worker = f.Worker(0);
                var sheep = f.Simulation.UnitRegistry.CreateUnit(owner, worker.SimPosition, Fixed32.One, Fixed32.One, Fixed32.One);
                sheep.IsSheep = true; sheep.CurrentHealth = 20;
                var goal = f.Submit("SelectedCount", "Exact", "1", "Idle", "", "Food", "Sheep");
                f.Manager.Tick(0); Assert.That(f.Commands(), Is.Empty); Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
                f.Simulation.SetTeamAssignments(new[] { 0, 0 }); f.Manager.Tick(150);
                Assert.That(f.Commands(), Is.Empty, "Allied sheep are not owned sheep.");
            }
        }

        [Test]
        public void ReservationPreflight_DistributesCapacityOrIssuesNothing()
        {
            using (var f = new EconomyFixture())
            {
                f.Node(ResourceType.Food, 20); var workers = new[] { f.Worker(0), f.Worker(1) };
                var goal = f.Submit("SelectedCount", "Exact", "4", "Idle", "", "Food", "Any");
                f.Manager.Tick(0); Assert.That(f.Commands(), Is.Empty); Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
                foreach (var worker in workers) Assert.That(f.Manager.GetWorkerReservation(worker.Id), Is.Null);
            }
            using (var f = new EconomyFixture())
            {
                var workers = Enumerable.Range(0, 4).Select(i => f.Worker(i)).ToArray();
                var targets = new List<int>();
                for (int i = 0; i < 4; i++)
                {
                    var building = f.Simulation.CreateBuilding(0, BuildingType.Farm, f.Base.x + 16 + i * 4, f.Base.y + 14, false);
                    var node = f.Simulation.MapData.AddFarmResourceNode(ResourceType.Food, building.SimPosition, 10000);
                    node.LinkedBuildingId = building.Id; building.LinkedResourceNodeId = node.Id; targets.Add(node.Id);
                    f.Simulation.MapData.MarkFarmTiles(building.OriginTileX, building.OriginTileZ, building.TileFootprintWidth, building.TileFootprintHeight);
                }
                f.Submit("SelectedCount", "Exact", "4", "Idle", "", "Food", "Farm");
                f.Manager.Tick(0);
                var first = f.Commands(); Assert.That(first.Count, Is.EqualTo(1), "One ordinary command per planning tick.");
                foreach (var worker in workers) Assert.That(f.Manager.GetWorkerReservation(worker.Id), Is.Not.Null, "Reserve complete selection before first command.");
                f.Simulation.Tick(first);
                var issuedTargets = first.OfType<GatherCommand>().Select(c => c.ResourceNodeId).ToList();
                for (int tick = 15; tick <= 45; tick += 15)
                { f.Manager.Tick(tick); var commands = f.Commands(); issuedTargets.AddRange(commands.OfType<GatherCommand>().Select(c => c.ResourceNodeId)); f.Simulation.Tick(commands); }
                Assert.That(issuedTargets.Distinct().Count(), Is.EqualTo(4)); Assert.That(issuedTargets, Is.SubsetOf(targets));
                Assert.That(workers.Select(w => w.TargetResourceNodeId).Distinct().Count(), Is.EqualTo(4), "Actual native commands must bind four separate farms.");
            }
        }

        [Test]
        public void HumanTakeover_InterruptsWithoutReclaimAfterLease()
        {
            using (var f = new EconomyFixture())
            {
                f.Node(ResourceType.Food, 20); var worker = f.Worker(0); f.Worker(1);
                var goal = f.Submit("SelectedCount", "Exact", "1", "Idle", "", "Food", "Any");
                f.Manager.Tick(0); int selectedId = Subjects(f.Commands()).Single();
                f.Simulation.CommandBuffer.EnqueueCommand(new StopCommand(0, new[] { selectedId })); f.Commands();
                Assert.That(f.Manager.GetWorkerReservation(selectedId), Is.Null);
                f.Manager.Tick(15); Assert.That(f.Commands(), Is.Empty);
                f.Manager.Tick(915); Assert.That(f.Commands(), Is.Empty); Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            }
        }

        private static int[] Subjects(List<ICommand> commands) => commands.SelectMany(c => CommanderWorkerAuthority.GetSubjectUnitIds(c) ?? Array.Empty<int>()).ToArray();

        private sealed class EconomyFixture : IDisposable
        {
            private readonly SimulationConfig config;
            internal readonly GameSimulation Simulation;
            internal readonly CommanderGoalManager Manager;
            internal readonly Vector2Int Base;
            internal EconomyFixture()
            {
                config = ScriptableObject.CreateInstance<SimulationConfig>();
                Simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
                Base = Simulation.MapData.BasePositions[0];
                foreach (var node in Simulation.MapData.GetAllResourceNodes().ToArray())
                { Simulation.MapData.ClearResourceTile(node.Id); Simulation.MapData.RemoveResourceNode(node.Id); }
                for (int x = 0; x < Simulation.MapData.Width; x++) for (int z = 0; z < Simulation.MapData.Height; z++)
                { Simulation.MapData.Tiles[x, z] = TileType.Grass; Simulation.MapData.ForestDensity[x, z] = 0; Simulation.FogOfWar.SetVisible(0, x, z); }
                Simulation.MapData.ComputeHoleMap();
                Simulation.FogOfWar.SetVisionCheat(0, true);
                Manager = new CommanderGoalManager(Simulation, 0);
            }
            internal UnitData Worker(int offset, int owner = 0)
            {
                var worker = Simulation.UnitRegistry.CreateUnit(owner, Simulation.MapData.TileToWorldFixed(Base.x + 5 + offset, Base.y + 5),
                    Fixed32.One, Fixed32.FromFloat(0.4f), Fixed32.One);
                worker.IsVillager = true; worker.UnitType = 0; worker.CurrentHealth = worker.MaxHealth = 100;
                worker.CarryCapacity = config.VillagerCarryCapacity; worker.State = UnitState.Idle; return worker;
            }
            internal ResourceNodeData Node(ResourceType resource, int offset) => Simulation.MapData.AddResourceNode(resource,
                Simulation.MapData.TileToWorldFixed(Base.x + offset, Base.y + 10), 10000);
            internal void Assigned(UnitData worker, ResourceNodeData node)
            { worker.State = UnitState.Gathering; worker.TargetResourceNodeId = node.Id; }
            internal CommanderGoal Submit(params string[] values)
            {
                var parsed = CommanderSemanticJson.Parse(Request(AllocationNode(values)));
                Assert.That(parsed.IsValid, Is.True);
                Assert.That(CommanderSemanticAdmission.TryCreateTacticalIntent(parsed.Nodes[0], Context(), out var intent, out _), Is.True);
                var result = new CommanderIntentResolver().Resolve(intent, Simulation, Manager);
                Assert.That(result.CreatedGoal, Is.True, result.Reason); return result.Goal;
            }
            internal List<ICommand> Commands() => Simulation.CommandBuffer.FlushCommands();
            public void Dispose() { Manager.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }
    }
}
