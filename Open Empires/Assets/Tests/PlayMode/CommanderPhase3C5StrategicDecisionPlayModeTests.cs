using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    public class CommanderPhase3C5StrategicDecisionPlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goalManager;
        private StrategicPlanner planner;
        private RuleBasedStrategicEvaluator evaluator;
        private RuleBasedStrategicDecisionPolicy policy;
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
            policy = new RuleBasedStrategicDecisionPolicy();
        }

        [TearDown]
        public void TearDown()
        {
            planner.Dispose();
            UnityEngine.Object.DestroyImmediate(config);
        }

        [UnityTest]
        public IEnumerator Runtime_VisibleThreat_SelectsDefenseWithoutPlan()
        {
            SetResources(1000, 1000, 1000, 0);
            UnitData enemy = Unit(1, 1, x, z);
            Reveal(enemy);
            StrategicContext context = Context();

            StrategicDecisionResult decision = policy.Decide(context,
                evaluator.Evaluate(context));

            Assert.That(decision.SelectedIntent.ObjectiveType,
                Is.EqualTo(StrategicObjectiveType.DefensivePreparation));
            Assert.That(decision.PriorityLevel, Is.EqualTo(StrategicPriorityLevel.Emergency));
            AssertNoExecution();
            Debug.Log("[Phase3C-5 Runtime] PASS Scenario 1: visible enemy threat with weak defense selected DefensivePreparation and did not start a plan.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Runtime_StrongArmy_SelectsAttackPreparation()
        {
            PrepareAttackConditions();
            StrategicContext context = Context();

            StrategicDecisionResult decision = policy.Decide(context,
                evaluator.Evaluate(context));

            Assert.That(decision.SelectedIntent.ObjectiveType,
                Is.EqualTo(StrategicObjectiveType.AttackPreparation));
            Assert.That(decision.Status, Is.EqualTo(StrategicDecisionStatus.Selected));
            AssertNoExecution();
            Debug.Log("[Phase3C-5 Runtime] PASS Scenario 2: strong army and large resources without an emergency selected AttackPreparation.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Runtime_PlayerIntent_OverridesRecommendations()
        {
            PrepareAttackConditions();
            StrategicContext context = Context();
            var playerIntent = new StrategicIntent(5000, 0,
                StrategicObjectiveType.EconomicExpansion, context.SnapshotTick);

            StrategicDecisionResult decision = policy.Decide(context,
                evaluator.Evaluate(context), playerIntent);

            Assert.That(decision.SelectedIntent, Is.SameAs(playerIntent));
            Assert.That(decision.SelectedIntent.ObjectiveType,
                Is.EqualTo(StrategicObjectiveType.EconomicExpansion));
            Assert.That(decision.Reason,
                Is.EqualTo(RuleBasedStrategicDecisionPolicy.PlayerOverrideReason));
            AssertNoExecution();
            Debug.Log("[Phase3C-5 Runtime] PASS Scenario 3: an explicit EconomicExpansion StrategicIntent overrode AI recommendations.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Runtime_NoRecommendations_ReturnsNoDecision()
        {
            StrategicDecisionResult decision = policy.Decide(Context(),
                Array.Empty<StrategicRecommendation>());

            Assert.That(decision.Status, Is.EqualTo(StrategicDecisionStatus.NoDecision));
            Assert.That(decision.SelectedIntent, Is.Null);
            Assert.That(decision.SourceRecommendation, Is.Null);
            AssertNoExecution();
            Debug.Log("[Phase3C-5 Runtime] PASS Scenario 4: no recommendations returned NoDecision with no plans or commands.");
            yield return null;
        }

        private StrategicContext Context()
        {
            return planner.BuildContext(
                new CommanderContextBuilder().Build(simulation, goalManager));
        }

        private void PrepareAttackConditions()
        {
            SetResources(1000, 0, 700, 0);
            for (int i = 0; i < RuleBasedStrategicEvaluator.StrongMilitaryThreshold; i++)
                Unit(0, 1, x - 20 + i, z + 5);
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

        private void SetResources(int food, int wood, int gold, int stone)
        {
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = food;
            resources.Wood = wood;
            resources.Gold = gold;
            resources.Stone = stone;
        }

        private void AssertNoExecution()
        {
            Assert.That(planner.Intents, Is.Empty);
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(goalManager.Goals, Is.Empty);
            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
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
