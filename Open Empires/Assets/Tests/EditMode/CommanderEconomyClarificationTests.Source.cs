using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    public sealed partial class CommanderEconomyClarificationTests
    {
        [Test]
        public void SourceConstraints_RoundtripJsonBinaryQueuedAndLegacyDefaults()
        {
            ICommand gather = new GatherCommand(0, new[] { 1, 2 }, 7);
            var source = typeof(GatherCommand).GetField("SourceKind");
            Assert.That(source, Is.Not.Null, "Restricted ordinary commands must carry their typed source constraint.");
            source.SetValue(gather, ResourceSourceKind.Sheep);
            var json = CommandSerializer.ToJson(gather);
            Assert.That(source.GetValue(CommandSerializer.FromJson(json.commandType, json.payload, 0)), Is.EqualTo(ResourceSourceKind.Sheep));
            var mixed = new System.Collections.Generic.List<ICommand> { new StopCommand(0, new[] { 3 }), gather };
            var binary = CommandSerializer.Serialize(mixed, 12);
            Assert.That(BitConverter.ToInt32(binary, 0), Is.EqualTo(int.MinValue));
            var roundtrip = CommandSerializer.Deserialize(binary);
            Assert.That(roundtrip.tick, Is.EqualTo(12)); Assert.That(roundtrip.commands.Count, Is.EqualTo(2));
            Assert.That(source.GetValue(roundtrip.commands[1]), Is.EqualTo(ResourceSourceKind.Sheep));
            var slaughter = new SlaughterSheepCommand { PlayerId = 0, VillagerIds = new[] { 1 }, SheepUnitId = 8, SourceKind = ResourceSourceKind.Sheep };
            var slaughterRoundtrip = (SlaughterSheepCommand)CommandSerializer.Deserialize(
                CommandSerializer.Serialize(new System.Collections.Generic.List<ICommand> { slaughter }, 12)).commands.Single();
            Assert.That(slaughterRoundtrip.SourceKind, Is.EqualTo(ResourceSourceKind.Sheep));
            Assert.That(QueuedCommand.GatherWaypoint(FixedVector3.Zero, 7, ResourceSourceKind.Farm).SourceKind, Is.EqualTo(ResourceSourceKind.Farm));
            Assert.That(QueuedCommand.SlaughterWaypoint(FixedVector3.Zero, 8, ResourceSourceKind.Sheep).SourceKind, Is.EqualTo(ResourceSourceKind.Sheep));
            var legacy = CommandSerializer.Serialize(new System.Collections.Generic.List<ICommand> { new GatherCommand(0, new[] { 1 }, 7) }, 12);
            Assert.That(BitConverter.ToInt32(legacy, 0), Is.EqualTo(12));
            using (var bytes = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(bytes))
            {
                writer.Write(12); writer.Write(1); writer.Write((byte)CommandType.Gather); writer.Write(0);
                writer.Write(1); writer.Write(1); writer.Write(7); writer.Write(false); writer.Flush();
                Assert.That(legacy, Is.EqualTo(bytes.ToArray()), "Unrestricted binary must remain byte-for-byte legacy.");
            }
            Assert.That(source.GetValue(CommandSerializer.Deserialize(legacy).commands.Single()), Is.EqualTo(ResourceSourceKind.Any));
            Assert.That(source.GetValue(CommandSerializer.FromJson("Gather", "{\"unitIds\":[1],\"resourceNodeId\":7}", 0)), Is.EqualTo(ResourceSourceKind.Any));
            byte[] bad = (byte[])binary.Clone(); bad[4] = 99;
            Assert.That(() => CommandSerializer.Deserialize(bad), Throws.Exception, "Unknown restricted version must fail closed.");
            Assert.That(() => CommandSerializer.Deserialize(binary.Take(binary.Length - 1).ToArray()), Throws.Exception);
            byte[] invalidKind = (byte[])binary.Clone(); invalidKind[invalidKind.Length - 1] = 99;
            Assert.That(() => CommandSerializer.Deserialize(invalidKind), Throws.Exception);
            byte[] unknownCommand = (byte[])binary.Clone(); unknownCommand[13] = 255;
            Assert.That(() => CommandSerializer.Deserialize(unknownCommand), Throws.Exception);
            foreach (string invalid in new[] { "99", "-1", "null", "\"Sheep\"", "1.5" })
            {
                LogAssert.Expect(UnityEngine.LogType.Error, "[CommandSerializer] Failed to parse Gather: Invalid resource source kind.");
                Assert.That(CommandSerializer.FromJson("Gather", "{\"unitIds\":[1],\"resourceNodeId\":7,\"sourceKind\":" + invalid + "}", 0), Is.Null);
            }
        }

        [Test]
        public void SourceConstraints_SurviveFallbackDepletionAndClearOnHumanOrder()
        {
            using (var f = new EconomyFixture())
            {
                var worker = f.Worker(0); worker.DetectionRange = Fixed32.FromInt(32);
                var blocked = f.Node(ResourceType.Food, 8); blocked.IsCarcass = true;
                var alternate = f.Node(ResourceType.Food, 12); alternate.IsCarcass = true;
                f.Node(ResourceType.Food, 10); // Competing berries are closer, but forbidden.
                for (int dx = -1; dx <= blocked.FootprintWidth; dx++)
                    for (int dz = -1; dz <= blocked.FootprintHeight; dz++)
                        f.Simulation.MapData.Tiles[blocked.TileX + dx, blocked.TileZ + dz] = TileType.Water;
                f.Simulation.Tick(new System.Collections.Generic.List<ICommand> {
                    new GatherCommand(0, new[] { worker.Id }, blocked.Id, ResourceSourceKind.Sheep) });
                Assert.That(worker.TargetResourceNodeId, Is.EqualTo(alternate.Id), "Native execution redirects to another matching reachable carcass.");
                Assert.That(worker.GatherSourceKind, Is.EqualTo(ResourceSourceKind.Sheep), "Automatic redirect must not clear the active player constraint.");
                for (int tick = 0; tick < 900 && worker.State != UnitState.Gathering; tick++) f.Simulation.Tick();
                alternate.RemainingAmount = 0; blocked.RemainingAmount = 0; f.Simulation.Tick();
                Assert.That(worker.TargetResourceNodeId, Is.Not.EqualTo(2), "A second depletion may not escape into berries after redirect.");
            }
            using (var f = new EconomyFixture())
            {
                var worker = f.Worker(0);
                worker.DetectionRange = Fixed32.FromInt(32);
                var carcass = f.Node(ResourceType.Food, 6); carcass.IsCarcass = true;
                var berries = f.Node(ResourceType.Food, 8);
                var goal = f.Submit("SelectedCount", "Exact", "1", "Idle", "", "Food", "Sheep");
                f.Manager.Tick(0);
                var commands = f.Commands();
                Assert.That(commands.Single(), Is.TypeOf<GatherCommand>());
                Assert.That(((GatherCommand)commands[0]).ResourceNodeId, Is.EqualTo(carcass.Id));
                f.Simulation.Tick(commands);
                Assert.That(worker.GatherSourceKind, Is.EqualTo(ResourceSourceKind.Sheep));
                uint restrictedHash = f.Simulation.ComputeStateChecksum();
                worker.GatherSourceKind = ResourceSourceKind.Any;
                Assert.That(f.Simulation.ComputeStateChecksum(), Is.Not.EqualTo(restrictedHash));
                worker.GatherSourceKind = ResourceSourceKind.Sheep;
                for (int i = 0; i < 900 && worker.State != UnitState.Gathering; i++) f.Simulation.Tick();
                Assert.That(worker.State, Is.EqualTo(UnitState.Gathering), "Ordinary gather command must actually reach the carcass.");
                f.Manager.Tick(15);
                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed));
                carcass.RemainingAmount = 0;
                f.Simulation.Tick();
                Assert.That(worker.TargetResourceNodeId, Is.Not.EqualTo(berries.Id),
                    "Runtime gate: native coarse-Food automatic retarget must not violate explicit Sheep constraint.");
                f.Simulation.Tick(new System.Collections.Generic.List<ICommand> { new GatherCommand(0, new[] { worker.Id }, berries.Id) });
                Assert.That(worker.TargetResourceNodeId, Is.EqualTo(berries.Id), "A later human order is allowed to replace the old constraint.");
                Assert.That(worker.GatherSourceKind, Is.EqualTo(ResourceSourceKind.Any));
            }
            using (var f = new EconomyFixture())
            {
                var worker = f.Worker(0); var food = f.Node(ResourceType.Food, 6); food.IsCarcass = true;
                var move = new MoveCommand(0, new[] { worker.Id }, f.Simulation.MapData.TileToWorldFixed(f.Base.x + 7, f.Base.y + 5));
                var gather = new GatherCommand(0, new[] { worker.Id }, food.Id, ResourceSourceKind.Sheep) { IsQueued = true };
                f.Simulation.Tick(new System.Collections.Generic.List<ICommand> { move, gather });
                Assert.That(worker.CommandQueue.Single().SourceKind, Is.EqualTo(ResourceSourceKind.Sheep));
                for (int i = 0; i < 900 && worker.GatherSourceKind != ResourceSourceKind.Sheep; i++) f.Simulation.Tick();
                Assert.That(worker.GatherSourceKind, Is.EqualTo(ResourceSourceKind.Sheep), "Queued promotion retains its own typed restriction.");
                f.Simulation.Tick(new System.Collections.Generic.List<ICommand> { new StopCommand(0, new[] { worker.Id }) });
                Assert.That(worker.GatherSourceKind, Is.EqualTo(ResourceSourceKind.Any));
            }
            using (var f = new EconomyFixture())
            {
                var worker = f.Worker(0);
                var sheep = f.Simulation.UnitRegistry.CreateUnit(0, worker.SimPosition, Fixed32.One, Fixed32.FromFloat(0.4f), Fixed32.One);
                sheep.IsSheep = true; sheep.UnitType = 5; sheep.CurrentHealth = sheep.MaxHealth = 20;
                var slaughter = new SlaughterSheepCommand { PlayerId = 0, VillagerIds = new[] { worker.Id }, SheepUnitId = sheep.Id, SourceKind = ResourceSourceKind.Sheep };
                f.Simulation.Tick(new System.Collections.Generic.List<ICommand> { slaughter });
                for (int i = 0; i < 120 && sheep.State != UnitState.Dead; i++) f.Simulation.Tick();
                Assert.That(sheep.State, Is.EqualTo(UnitState.Dead));
                Assert.That(f.Simulation.MapData.GetResourceNode(worker.TargetResourceNodeId).IsCarcass, Is.True);
                Assert.That(worker.GatherSourceKind, Is.EqualTo(ResourceSourceKind.Sheep));
            }
            using (var first = new EconomyFixture())
            using (var second = new EconomyFixture())
            {
                var a = first.Worker(0); var b = second.Worker(0);
                var x = first.Node(ResourceType.Food, 6); var y = second.Node(ResourceType.Food, 6); x.IsCarcass = y.IsCarcass = true;
                var bytes = CommandSerializer.Serialize(new System.Collections.Generic.List<ICommand> { new GatherCommand(0, new[] { a.Id }, x.Id, ResourceSourceKind.Sheep) }, 0);
                first.Simulation.Tick(CommandSerializer.Deserialize(bytes).commands);
                second.Simulation.Tick(CommandSerializer.Deserialize(bytes).commands);
                Assert.That(first.Simulation.ComputeStateChecksum(), Is.EqualTo(second.Simulation.ComputeStateChecksum()));
                Assert.That(b.GatherSourceKind, Is.EqualTo(ResourceSourceKind.Sheep));
            }
        }
    }
}
