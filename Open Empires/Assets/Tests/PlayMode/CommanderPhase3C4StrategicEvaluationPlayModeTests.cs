using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    public class CommanderPhase3C4StrategicEvaluationPlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goalManager;
        private StrategicPlanner planner;
        private RuleBasedStrategicEvaluator evaluator;
        private int x;
        private int z;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            x = simulation.MapData.Width / 2;
            z = simulation.MapData.Height / 2;
            simulation.CreateBuilding(0, BuildingType.TownCenter, x + 12, z, false, true)
                .AutoProduceVillagers = false;
            simulation.CreateBuilding(0, BuildingType.House, x + 18, z, false);
            simulation.CreateBuilding(0, BuildingType.House, x + 22, z, false);
            goalManager = new CommanderGoalManager(simulation, 0);
            planner = new StrategicPlanner(goalManager, CurrentResourceAmount);
            evaluator = new RuleBasedStrategicEvaluator();
        }

        [TearDown]
        public void TearDown()
        {
            planner.Dispose();
            UnityEngine.Object.DestroyImmediate(config);
        }

        [UnityTest]
        public IEnumerator Runtime_VisibleThreat_RecommendsDefenseWithoutPlan()
        {
            UnitData enemy = Unit(1, 1, x, z);
            Reveal(enemy);

            var recommendations = evaluator.Evaluate(Context());

            Assert.That(recommendations.Any(item => item.ObjectiveType
                == StrategicObjectiveType.DefensivePreparation), Is.True);
            AssertNoExecution();
            Debug.Log("[Phase3C-4 Runtime] PASS Scenario 1: a visible enemy threat with weak defense produced a DefensivePreparation recommendation and no plan.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Runtime_LowEconomy_RecommendsExpansion()
        {
            SetResources(0, 0, 0, 0);

            var recommendations = evaluator.Evaluate(Context());

            Assert.That(recommendations.Any(item => item.ObjectiveType
                == StrategicObjectiveType.EconomicExpansion), Is.True);
            AssertNoExecution();
            Debug.Log("[Phase3C-4 Runtime] PASS Scenario 2: low worker allocation with available population produced an EconomicExpansion recommendation.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Runtime_StrongArmy_RecommendsAttackPreparation()
        {
            SetResources(1000, 0, 700, 0);
            for (int i = 0; i < RuleBasedStrategicEvaluator.StrongMilitaryThreshold; i++)
                Unit(0, 1, x - 20 + i, z + 5);

            var recommendations = evaluator.Evaluate(Context());

            Assert.That(recommendations.Any(item => item.ObjectiveType
                == StrategicObjectiveType.AttackPreparation), Is.True);
            AssertNoExecution();
            Debug.Log("[Phase3C-4 Runtime] PASS Scenario 3: a strong owned army with sufficient visible economy produced an AttackPreparation recommendation.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Runtime_HiddenArmy_DoesNotAffectRecommendations()
        {
            UnitData hidden = Unit(1, 7, x, z);
            string[] before = evaluator.Evaluate(Context()).Select(Describe).ToArray();
            hidden.UnitType = 6;
            Unit(1, 1, x + 1, z);
            string[] after = evaluator.Evaluate(Context()).Select(Describe).ToArray();

            Assert.That(after, Is.EqualTo(before));
            Assert.That(Context().Threat.VisibleEnemyMilitaryUnits, Is.Zero);
            Assert.That(evaluator.Evaluate(Context()).Any(item => item.ObjectiveType
                == StrategicObjectiveType.DefensivePreparation), Is.False);
            AssertNoExecution();
            Debug.Log("[Phase3C-4 Runtime] PASS Scenario 4: hidden enemy military changes did not alter recommendations or create a defensive recommendation.");
            yield return null;
        }

        private StrategicContext Context()
        {
            return planner.BuildContext(
                new CommanderContextBuilder().Build(simulation, goalManager));
        }

        private void AssertNoExecution()
        {
            Assert.That(planner.Intents, Is.Empty);
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(goalManager.Goals, Is.Empty);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
        }

        private static string Describe(StrategicRecommendation recommendation)
        {
            return recommendation.ObjectiveType + ":" + recommendation.Score + ":"
                + recommendation.Reason;
        }

        private void SetResources(int food, int wood, int gold, int stone)
        {
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = food;
            resources.Wood = wood;
            resources.Gold = gold;
            resources.Stone = stone;
        }

        private UnitData Unit(int playerId, int unitType, int tileX, int tileZ)
        {
            UnitData unit = simulation.UnitRegistry.CreateUnit(playerId,
                simulation.MapData.TileToWorldFixed(tileX, tileZ), Fixed32.One,
                Fixed32.One, Fixed32.One);
            unit.UnitType = unitType;
            unit.IsVillager = unitType == 0;
            unit.MaxHealth = unit.CurrentHealth = 100;
            unit.State = UnitState.Idle;
            return unit;
        }

        private void Reveal(UnitData unit)
        {
            Vector2Int tile = simulation.MapData.WorldToTile(unit.SimPosition);
            simulation.FogOfWar.SetVisible(0, tile.x, tile.y);
        }

        private int CurrentResourceAmount(ResourceType resourceType)
        {
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            switch (resourceType)
            {
                case ResourceType.Food: return resources.Food;
                case ResourceType.Wood: return resources.Wood;
                case ResourceType.Gold: return resources.Gold;
                case ResourceType.Stone: return resources.Stone;
                default: return 0;
            }
        }
    }
}
