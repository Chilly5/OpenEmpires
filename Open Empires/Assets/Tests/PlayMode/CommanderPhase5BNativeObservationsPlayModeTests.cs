using System;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5BNativeObservations")]
    public sealed class CommanderPhase5BNativeObservationsPlayModeTests
    {
        private static readonly BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private SimulationConfig config;
        private GameSimulation simulation;
        private BuildingData producer;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            simulation.SetPlayerCivilizations(new[] { Civilization.French });
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            typeof(MapData).GetField("holeMap", InstancePrivate).SetValue(simulation.MapData, null);
            for (int tx = x - 8; tx <= x + 8; tx++)
                for (int tz = z - 8; tz <= z + 8; tz++)
                {
                    simulation.MapData.Tiles[tx, tz] = TileType.Grass;
                    simulation.MapData.ForestDensity[tx, tz] = 0;
                    simulation.MapData.FoundationCount[tx, tz] = 0;
                    simulation.FogOfWar.SetVisible(0, tx, tz);
                }
            producer = simulation.CreateBuilding(0, BuildingType.Barracks, x, z, false, true);
            simulation.ResourceManager.GetPlayerResources(0).Food = 10000;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void GatheredIncomeCountsOnlyResourceDeliveryNotArbitraryStockpileCredits()
        {
            MethodInfo getIncome = typeof(ResourceManager).GetMethod("GetGatheredIncome",
                BindingFlags.Instance | BindingFlags.Public, null,
                new[] { typeof(int), typeof(ResourceType) }, null);
            Assert.That(getIncome, Is.Not.Null, "ResourceManager must expose the read-only gathered-income query.");
            Assert.That(getIncome.ReturnType, Is.EqualTo(typeof(long)));

            var resources = simulation.ResourceManager;
            resources.AddResource(0, ResourceType.Food, 7);
            Assert.That((long)getIncome.Invoke(resources, new object[] { 0, ResourceType.Food }), Is.Zero,
                "Starting stock, refunds, and ordinary AddResource credits are not gathered income.");

            UnitData gatherer = (UnitData)typeof(GameSimulation).GetMethod("CreateTrainedUnit", InstancePrivate)
                .Invoke(simulation, new object[] { 0, 0, simulation.MapData.TileToWorldFixed(
                    simulation.MapData.Width / 2 + 3, simulation.MapData.Height / 2 + 3) });
            gatherer.CarriedResourceType = ResourceType.Food;
            gatherer.CarriedResourceAmount = 4;
            gatherer.DropOffBuildingId = producer.Id;
            MethodInfo processDropOff = typeof(ResourceGatheringSystem).GetMethod("ProcessDropOff", InstancePrivate);
            object gatheringSystem = typeof(GameSimulation).GetField("gatheringSystem", InstancePrivate)
                .GetValue(simulation);
            processDropOff.Invoke(gatheringSystem, new object[] { gatherer, simulation.MapData, resources,
                simulation.BuildingRegistry, simulation.UnitRegistry, simulation.CurrentTick });
            Assert.That((long)getIncome.Invoke(resources, new object[] { 0, ResourceType.Food }), Is.EqualTo(4));

            gatherer.CarriedResourceAmount = 3;
            typeof(GameSimulation).GetMethod("BankCarriedResources", InstancePrivate)
                .Invoke(simulation, new object[] { gatherer });
            Assert.That((long)getIncome.Invoke(resources, new object[] { 0, ResourceType.Food }), Is.EqualTo(7),
                "The alternate carried-resource deposit path must use the same gathered-income counter.");
        }

        [Test]
        public void ProducerBirthObservationIncludesNativeBirthEvenWithoutCommanderReceipt()
        {
            EventInfo birthEvent = typeof(GameSimulation).GetEvent("ProducerUnitProduced",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(birthEvent, Is.Not.Null, "Native producer births need a receipt-independent observation event.");

            int observed = 0;
            object observation = null;
            var parameter = Expression.Parameter(birthEvent.EventHandlerType.GetMethod("Invoke").GetParameters()[0].ParameterType);
            Action<object> capture = value => { observed++; observation = value; };
            Delegate listener = Expression.Lambda(birthEvent.EventHandlerType,
                Expression.Call(Expression.Constant(capture), typeof(Action<object>).GetMethod("Invoke"),
                    Expression.Convert(parameter, typeof(object))), parameter).Compile();
            birthEvent.GetAddMethod(true).Invoke(simulation, new object[] { listener });
            try
            {
                typeof(GameSimulation).GetMethod("SpawnTrainedUnit", InstancePrivate)
                    .Invoke(simulation, new object[] { producer, 1, 0, null });
            }
            finally
            {
                birthEvent.GetRemoveMethod(true).Invoke(simulation, new object[] { listener });
            }

            Assert.That(observed, Is.EqualTo(1), "A producer birth must be published once even with a null receipt.");
            Type payload = observation.GetType();
            const BindingFlags PayloadProperty = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Assert.That(payload.GetProperty("Runtime", PayloadProperty)?.GetValue(observation), Is.SameAs(simulation));
            Assert.That(payload.GetProperty("Producer", PayloadProperty)?.GetValue(observation), Is.SameAs(producer));
            Assert.That((int)payload.GetProperty("ProducerId", PayloadProperty)?.GetValue(observation), Is.EqualTo(producer.Id));
            Assert.That((int)payload.GetProperty("PlayerId", PayloadProperty)?.GetValue(observation), Is.EqualTo(0));
            Assert.That((int)payload.GetProperty("UnitType", PayloadProperty)?.GetValue(observation), Is.EqualTo(1));
            Assert.That((int)payload.GetProperty("Tick", PayloadProperty)?.GetValue(observation), Is.EqualTo(simulation.CurrentTick));
            int unitId = (int)payload.GetProperty("UnitId", PayloadProperty)?.GetValue(observation);
            Assert.That(simulation.UnitRegistry.GetUnit(unitId), Is.Not.Null);
        }
    }
}
