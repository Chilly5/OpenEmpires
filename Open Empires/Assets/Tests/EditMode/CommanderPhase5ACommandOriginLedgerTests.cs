using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public sealed class CommanderPhase5ACommandOriginLedgerTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private SimulationConfig config;
        private GameSimulation simulation;
        private object ledger;
        private Type ledgerType;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            // Reflection keeps this test assembly compilable while the new helper is absent.
            ledgerType = Type.GetType("OpenEmpires.CommanderCommandOriginLedger, OpenEmpires.Runtime");
            Assert.That(ledgerType, Is.Not.Null, "the command-origin ledger is not implemented yet");
            ledger = Activator.CreateInstance(ledgerType, true);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(config);

        [Test]
        public void ReconstructedLocalCommands_MapToTheirOriginalBoxesOnly()
        {
            ICommand firstOriginal = Train(0, 11);
            ICommand secondOriginal = Train(0, 12);
            Register(firstOriginal);
            Register(secondOriginal);
            var originals = new List<ICommand> { firstOriginal, secondOriginal };
            var stamped = new List<ICommand> { Reconstruct(firstOriginal), Reconstruct(secondOriginal) };
            Record(8, 0, originals, stamped);

            ICommand firstReceived = Reconstruct(stamped[0]);
            ICommand secondReceived = Reconstruct(stamped[1]);
            ICommand remote = Train(1, 99);
            var received = new List<ICommand> { firstReceived, secondReceived, new NoopCommand(0), remote, new NoopCommand(1) };
            ICommand[] gameplaySnapshot = received.ToArray();

            IReadOnlyDictionary<ICommand, ICommand> map = Consume(8, 0, received);

            Assert.That(map, Has.Count.EqualTo(2));
            Assert.That(map.Keys.Any(key => ReferenceEquals(key, firstReceived)), Is.True);
            Assert.That(map.Keys.Any(key => ReferenceEquals(key, secondReceived)), Is.True);
            Assert.That(map.Single(pair => ReferenceEquals(pair.Key, firstReceived)).Value,
                Is.SameAs(firstOriginal));
            Assert.That(map.Single(pair => ReferenceEquals(pair.Key, secondReceived)).Value,
                Is.SameAs(secondOriginal));
            Assert.That(map.Keys.Any(key => ReferenceEquals(key, remote)), Is.False);
            Assert.That(received.Select((command, index) => ReferenceEquals(command, gameplaySnapshot[index])).All(equal => equal), Is.True,
                "correlation must not alter the command list used by gameplay");
        }

        [Test]
        public void EqualPayloadsFromDifferentOrigins_FailClosedDespiteOrdinalMatch()
        {
            ICommand first = Train(0, 11);
            ICommand second = Train(0, 11);
            Register(first);
            Register(second);
            Record(8, 0, new List<ICommand> { first, second },
                new List<ICommand> { Reconstruct(first), Reconstruct(second) });
            var received = new List<ICommand> { Reconstruct(first), Reconstruct(second), new NoopCommand(0) };

            Assert.That(Consume(8, 0, received), Is.Empty);
            Assert.That(simulation.HasPendingTrainingOrigin(first), Is.False);
            Assert.That(simulation.HasPendingTrainingOrigin(second), Is.False);
            Assert.That(received, Has.Count.EqualTo(3), "ambiguous origin must not suppress game commands");
        }

        [TestCase("missing")]
        [TestCase("extra")]
        [TestCase("reordered")]
        [TestCase("changed")]
        [TestCase("owner")]
        [TestCase("noop-first")]
        public void NonEquivalentCompleteLocalBatch_MapsNothingAndDiscardsOrigins(string mismatch)
        {
            ICommand first = Train(0, 11);
            ICommand second = Train(0, 12);
            Register(first);
            Register(second);
            Record(8, 0, new List<ICommand> { first, second },
                new List<ICommand> { Reconstruct(first), Reconstruct(second) });
            ICommand firstReplay = Reconstruct(first);
            ICommand secondReplay = Reconstruct(second);
            var received = new List<ICommand> { firstReplay, secondReplay, new NoopCommand(0) };
            switch (mismatch)
            {
                case "missing": received.RemoveAt(1); break;
                case "extra": received.Insert(2, Train(0, 13)); break;
                case "reordered": received[0] = secondReplay; received[1] = firstReplay; break;
                case "changed": received[1] = Train(0, 13); break;
                case "owner": received[1] = Train(1, 12); break;
                case "noop-first": received.Insert(0, received[2]); received.RemoveAt(3); break;
            }
            ICommand[] gameplaySnapshot = received.ToArray();

            Assert.That(Consume(8, 0, received), Is.Empty, mismatch);
            Assert.That(simulation.HasPendingTrainingOrigin(first), Is.False, mismatch);
            Assert.That(simulation.HasPendingTrainingOrigin(second), Is.False, mismatch);
            Assert.That(received.Select((command, index) => ReferenceEquals(command, gameplaySnapshot[index])).All(equal => equal), Is.True,
                "failed attribution cannot change gameplay commands");
        }

        [Test]
        public void DifferentTickCannotInheritOrigin_AndLaterCorrectTickStillCan()
        {
            ICommand original = Train(0, 11);
            Register(original);
            Record(9, 0, new List<ICommand> { original }, new List<ICommand> { Reconstruct(original) });
            ICommand wrongTickReplay = Reconstruct(original);
            Assert.That(Consume(8, 0, new List<ICommand> { wrongTickReplay, new NoopCommand(0) }), Is.Empty);
            Assert.That(simulation.HasPendingTrainingOrigin(original), Is.True);

            ICommand correctReplay = Reconstruct(original);
            IReadOnlyDictionary<ICommand, ICommand> map = Consume(9, 0,
                new List<ICommand> { correctReplay, new NoopCommand(0) });
            Assert.That(map, Has.Count.EqualTo(1));
            Assert.That(map.Single().Key, Is.SameAs(correctReplay));
            Assert.That(map.Single().Value, Is.SameAs(original));
        }

        [Test]
        public void LostTickAndShutdown_DiscardUnresolvedOrigins()
        {
            ICommand old = Train(0, 11);
            ICommand future = Train(0, 12);
            Register(old);
            Register(future);
            Record(8, 0, new List<ICommand> { old }, new List<ICommand> { Reconstruct(old) });
            Record(9, 0, new List<ICommand> { future }, new List<ICommand> { Reconstruct(future) });

            Invoke("PruneBefore", 9, simulation);
            Assert.That(simulation.HasPendingTrainingOrigin(old), Is.False);
            Assert.That(simulation.HasPendingTrainingOrigin(future), Is.True);

            Invoke("Clear", simulation);
            Assert.That(simulation.HasPendingTrainingOrigin(future), Is.False);
        }

        [Test]
        public void OversizedBatch_DropsAttributionWithoutDroppingCommands()
        {
            ICommand tracked = Train(0, 11);
            Register(tracked);
            var originals = new List<ICommand> { tracked };
            for (int i = 0; i < 256; i++) originals.Add(Train(0, i + 100));
            var stamped = originals.Select(Reconstruct).ToList();
            var received = stamped.Select(Reconstruct).ToList();
            received.Add(new NoopCommand(0));
            Record(8, 0, originals, stamped);

            Assert.That(Consume(8, 0, received), Is.Empty);
            Assert.That(simulation.HasPendingTrainingOrigin(tracked), Is.False);
            Assert.That(received, Has.Count.EqualTo(258));
        }

        [Test]
        public void ReconstructedPlaceCommand_PublishesVerifiedOriginalAtNativePlacement()
        {
            int tileX = PrepareBuildableBarracksSite(out int tileZ, out int workerId);
            ICommand original = Place(tileX, tileZ, workerId);
            ICommand stamped = Reconstruct(original);
            Record(8, 0, new List<ICommand> { original }, new List<ICommand> { stamped });
            ICommand replayed = Reconstruct(stamped);
            var received = new List<ICommand> { replayed, new NoopCommand(0) };
            ICommand observedSource = null;
            BuildingData placed = null;
            simulation.OnBuildingPlacedFromCommand += (source, building) =>
            {
                observedSource = source;
                placed = building;
            };

            IReadOnlyDictionary<ICommand, ICommand> map = Consume(8, 0, received);
            simulation.Tick(new List<ICommand> { replayed }, map);

            Assert.That(placed, Is.Not.Null, "the real placement path must accept the reconstructed command");
            Assert.That(placed.OriginTileX, Is.EqualTo(tileX));
            Assert.That(placed.OriginTileZ, Is.EqualTo(tileZ));
            Assert.That(observedSource, Is.SameAs(original),
                "native result observation must receive the original issued box, not the relay box");
            Assert.That(received[0], Is.SameAs(replayed), "correlation cannot rewrite gameplay commands");
        }

        [Test]
        public void EqualPlacePayloads_InvalidateWholeBatchWithoutBorrowingAnOrigin()
        {
            int tileX = PrepareBuildableBarracksSite(out int tileZ, out int workerId);
            ICommand trackedTrain = Train(0, 11);
            Register(trackedTrain);
            ICommand firstPlace = Place(tileX, tileZ, workerId);
            ICommand secondPlace = Place(tileX, tileZ, workerId);
            var originals = new List<ICommand> { trackedTrain, firstPlace, secondPlace };
            var stamped = originals.Select(Reconstruct).ToList();
            Record(8, 0, originals, stamped);
            var received = stamped.Select(Reconstruct).ToList();
            received.Add(new NoopCommand(0));
            ICommand firstReplay = received[1];
            ICommand observedSource = null;
            int placements = 0;
            simulation.OnBuildingPlacedFromCommand += (source, _) =>
            {
                observedSource = source;
                placements++;
            };

            IReadOnlyDictionary<ICommand, ICommand> map = Consume(8, 0, received);
            simulation.Tick(received.Take(3).ToList(), map);

            Assert.That(map, Is.Empty, "indistinguishable place origins invalidate the full local observation map");
            Assert.That(simulation.HasPendingTrainingOrigin(trackedTrain), Is.False);
            Assert.That(placements, Is.EqualTo(1), "gameplay still executes the first valid placement");
            Assert.That(observedSource, Is.SameAs(firstReplay));
            Assert.That(observedSource, Is.Not.SameAs(firstPlace));
            Assert.That(observedSource, Is.Not.SameAs(secondPlace));
            Assert.That(received, Has.Count.EqualTo(4), "correlation does not remove duplicate game commands");
        }

        private static ICommand Train(int owner, int producer) => new TrainUnitCommand(owner, producer, 1);

        private static ICommand Place(int tileX, int tileZ, int workerId) =>
            new PlaceBuildingCommand(0, BuildingType.Barracks, tileX, tileZ, new[] { workerId });

        private int PrepareBuildableBarracksSite(out int tileZ, out int workerId)
        {
            int centerX = simulation.MapData.Width / 2;
            int centerZ = simulation.MapData.Height / 2;
            typeof(MapData).GetField("holeMap", Members).SetValue(simulation.MapData, null);
            for (int x = centerX - 20; x <= centerX + 20; x++)
                for (int z = centerZ - 12; z <= centerZ + 12; z++)
                {
                    simulation.MapData.Tiles[x, z] = TileType.Grass;
                    simulation.MapData.ForestDensity[x, z] = 0;
                    simulation.MapData.FoundationCount[x, z] = 0;
                    simulation.FogOfWar.SetVisible(0, x, z);
                }
            foreach (ResourceNodeData node in simulation.MapData.GetAllResourceNodes())
                node.RemainingAmount = 0;
            simulation.CreateBuilding(0, BuildingType.TownCenter, centerX, centerZ, false, true)
                .AutoProduceVillagers = false;
            UnitData worker = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(centerX + 8, centerZ + 8),
                Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
            worker.IsVillager = true;
            worker.UnitType = 0;
            worker.MaxHealth = worker.CurrentHealth = 100;
            worker.State = UnitState.Idle;
            simulation.ResourceManager.GetPlayerResources(0).Wood = 1000;
            tileZ = centerZ;
            workerId = worker.Id;
            return centerX - 8;
        }

        private static ICommand Reconstruct(ICommand command)
        {
            var (type, payload) = CommandSerializer.ToJson(command);
            return CommandSerializer.FromJson(type, payload, command.PlayerId);
        }

        private void Register(ICommand command) =>
            simulation.RegisterTrainingOrigin(command, new object(), () => true);

        private void Record(int tick, int owner, IReadOnlyList<ICommand> originals,
            IReadOnlyList<ICommand> stamped) =>
            Invoke("Record", tick, owner, originals, stamped, simulation);

        private IReadOnlyDictionary<ICommand, ICommand> Consume(int tick, int owner,
            IReadOnlyList<ICommand> received) =>
            (IReadOnlyDictionary<ICommand, ICommand>)Invoke("Consume", tick, owner, received, simulation);

        private object Invoke(string name, params object[] arguments)
        {
            MethodInfo method = ledgerType.GetMethod(name, Members);
            Assert.That(method, Is.Not.Null, $"ledger must expose {name}");
            return method.Invoke(ledger, arguments);
        }
    }
}
