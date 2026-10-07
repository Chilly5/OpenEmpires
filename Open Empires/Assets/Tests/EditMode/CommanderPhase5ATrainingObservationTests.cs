using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public sealed class CommanderPhase5ATrainingObservationTests
    {
        private const BindingFlags RuntimeMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private SimulationConfig config;
        private GameSimulation simulation;
        private BuildingData barracks;
        private readonly List<object> accepted = new List<object>();
        private readonly List<(object receipt, int unitId)> produced = new List<(object, int)>();
        private readonly List<int> trained = new List<int>();

        [SetUp]
        public void SetUp()
        {
            accepted.Clear();
            produced.Clear();
            trained.Clear();
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true)
                .AutoProduceVillagers = false;
            barracks = simulation.CreateBuilding(0, BuildingType.Barracks, x + 7, z, false);
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 10000;
            simulation.OnUnitTrained += (id, _, _) => trained.Add(id);
            SubscribeObservationEvents();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(config);

        [Test]
        public void PreExistingAndValueEqualOtherOrders_CreditOnlyTrackedActualSpawn()
        {
            // A type/building match cannot distinguish any of these three queue slots.
            barracks.EnqueueTraining(1, 3);
            ICommand other = new TrainUnitCommand(0, barracks.Id, 1);
            ICommand tracked = new TrainUnitCommand(0, barracks.Id, 1);
            object issuer = new object();
            Register(tracked, issuer);

            simulation.Tick(new List<ICommand> { other, tracked });

            Assert.That(accepted, Has.Count.EqualTo(1));
            Assert.That(Read(accepted[0], "Issuer"), Is.SameAs(issuer));
            Assert.That(Read(accepted[0], "Runtime"), Is.SameAs(simulation));
            Assert.That(Read(accepted[0], "ProducerId"), Is.EqualTo(barracks.Id));
            Assert.That(Read(accepted[0], "ResolvedUnitType"), Is.EqualTo(1));
            Assert.That(barracks.TrainingQueue.ToArray(), Is.EqualTo(new[] { 1, 1, 1 }));

            CompleteFrontTraining();
            CompleteFrontTraining();
            Assert.That(produced, Is.Empty, "pre-existing and value-equal other orders are not attributable");
            CompleteFrontTraining();

            Assert.That(trained, Has.Count.EqualTo(3));
            Assert.That(produced, Has.Count.EqualTo(1));
            Assert.That(produced[0].receipt, Is.SameAs(accepted[0]));
            Assert.That(produced[0].unitId, Is.EqualTo(trained[2]));
            Assert.That(Read(accepted[0], "IsCompleted"), Is.True);
        }

        [Test]
        public void CancelFrontQueueSlot_DiscardsOnlyThatReceipt()
        {
            ICommand first = new TrainUnitCommand(0, barracks.Id, 1);
            ICommand second = new TrainUnitCommand(0, barracks.Id, 1);
            Register(first, new object());
            Register(second, new object());
            simulation.Tick(new List<ICommand> { first, second });
            Assert.That(accepted, Has.Count.EqualTo(2));

            simulation.Tick(new List<ICommand> { new CancelTrainCommand(0, barracks.Id, 0) });
            Assert.That(barracks.TrainingQueue.ToArray(), Is.EqualTo(new[] { 1 }));
            Assert.That(Read(accepted[0], "IsCancelled"), Is.True);
            CompleteFrontTraining();

            Assert.That(produced, Has.Count.EqualTo(1));
            Assert.That(produced[0].receipt, Is.SameAs(accepted[1]));
            Assert.That(produced[0].unitId, Is.EqualTo(trained[0]));
        }

        [Test]
        public void RejectedTrainCommand_EmitsNoAcceptanceOrProduction()
        {
            simulation.ResourceManager.GetPlayerResources(0).Food = 0;
            ICommand rejected = new TrainUnitCommand(0, barracks.Id, 1);
            Register(rejected, new object());

            simulation.Tick(new List<ICommand> { rejected });

            Assert.That(accepted, Is.Empty);
            Assert.That(produced, Is.Empty);
            Assert.That(barracks.TrainingQueue, Is.Empty);
        }

        [Test]
        public void DirectSameValueQueueMutation_InvalidatesOutstandingReceipt()
        {
            ICommand command = new TrainUnitCommand(0, barracks.Id, 1);
            Register(command, new object());
            simulation.Tick(new List<ICommand> { command });
            Assert.That(accepted, Has.Count.EqualTo(1));

            barracks.TrainingQueue.RemoveAt(0);
            barracks.TrainingQueue.AddRange(new[] { 1 });
            Assert.That(barracks.TrainingQueue.ToArray(), Is.EqualTo(new[] { 1 }));
            CompleteFrontTraining();

            Assert.That(trained, Has.Count.EqualTo(1), "legacy queue edits do not cancel gameplay production");
            Assert.That(produced, Is.Empty, "same-value edits cannot preserve a stale receipt");
            Assert.That(Read(accepted[0], "IsCancelled"), Is.True);
        }

        [Test]
        public void ReplayedValueEqualCommand_RequiresVerifiedOneTickOriginMap()
        {
            ICommand original = new TrainUnitCommand(0, barracks.Id, 1);
            ICommand unverifiedReplay = new TrainUnitCommand(0, barracks.Id, 1);
            Register(original, new object());
            simulation.Tick(new List<ICommand> { unverifiedReplay });
            Assert.That(accepted, Is.Empty);
            Discard(original);

            ICommand secondOriginal = new TrainUnitCommand(0, barracks.Id, 1);
            ICommand verifiedReplay = new TrainUnitCommand(0, barracks.Id, 1);
            Register(secondOriginal, new object());
            TickWithOriginMap(new List<ICommand> { verifiedReplay },
                new Dictionary<ICommand, ICommand> { [verifiedReplay] = secondOriginal });

            Assert.That(accepted, Has.Count.EqualTo(1));
            Assert.That(Read(accepted[0], "IsQueued"), Is.True);
            Assert.That(barracks.TrainingQueue.ToArray(), Is.EqualTo(new[] { 1, 1 }));
        }

        private void CompleteFrontTraining()
        {
            Assert.That(barracks.TrainingQueue, Is.Not.Empty);
            barracks.TrainingTicksRemaining = 1;
            simulation.Tick(new List<ICommand>());
        }

        private void Register(ICommand command, object issuer)
        {
            MethodInfo register = typeof(GameSimulation).GetMethod("RegisterTrainingOrigin", RuntimeMembers);
            Assert.That(register, Is.Not.Null, "native training observation registration is missing");
            register.Invoke(simulation, new object[] { command, issuer, (Func<bool>)(() => true) });
        }

        private void Discard(ICommand command)
        {
            MethodInfo discard = typeof(GameSimulation).GetMethod("DiscardTrainingOrigin", RuntimeMembers);
            Assert.That(discard, Is.Not.Null, "undispatched origins need an explicit discard path");
            discard.Invoke(simulation, new object[] { command });
        }

        private void TickWithOriginMap(List<ICommand> commands, IReadOnlyDictionary<ICommand, ICommand> origins)
        {
            MethodInfo overload = typeof(GameSimulation).GetMethods(RuntimeMembers)
                .FirstOrDefault(method => method.Name == "Tick" && method.GetParameters().Length == 2
                    && method.GetParameters()[0].ParameterType == typeof(List<ICommand>));
            Assert.That(overload, Is.Not.Null, "multiplayer replay needs a one-tick verified origin map");
            overload.Invoke(simulation, new object[] { commands, origins });
        }

        private void SubscribeObservationEvents()
        {
            EventInfo acceptedEvent = typeof(GameSimulation).GetEvent("TrainingOrderAccepted", RuntimeMembers);
            EventInfo producedEvent = typeof(GameSimulation).GetEvent("TrackedUnitProduced", RuntimeMembers);
            Assert.That(acceptedEvent, Is.Not.Null, "accepted training orders need an observation event");
            Assert.That(producedEvent, Is.Not.Null, "actual tracked spawns need an observation event");
            Type receiptType = acceptedEvent.EventHandlerType.GetGenericArguments()[0];
            MethodInfo acceptedHandler = GetType().GetMethod(nameof(RecordAccepted), RuntimeMembers)
                .MakeGenericMethod(receiptType);
            MethodInfo producedHandler = GetType().GetMethod(nameof(RecordProduced), RuntimeMembers)
                .MakeGenericMethod(receiptType);
            acceptedEvent.GetAddMethod(true).Invoke(simulation, new object[]
            {
                Delegate.CreateDelegate(acceptedEvent.EventHandlerType, this, acceptedHandler)
            });
            producedEvent.GetAddMethod(true).Invoke(simulation, new object[]
            {
                Delegate.CreateDelegate(producedEvent.EventHandlerType, this, producedHandler)
            });
        }

        private void RecordAccepted<T>(T receipt) => accepted.Add(receipt);
        private void RecordProduced<T>(T receipt, int unitId) => produced.Add((receipt, unitId));

        private static object Read(object receipt, string name)
        {
            PropertyInfo property = receipt.GetType().GetProperty(name, RuntimeMembers);
            Assert.That(property, Is.Not.Null, $"receipt must expose {name}");
            return property.GetValue(receipt);
        }
    }
}
