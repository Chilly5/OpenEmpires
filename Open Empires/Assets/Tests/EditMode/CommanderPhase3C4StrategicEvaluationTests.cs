using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public class CommanderPhase3C4StrategicEvaluationTests
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

        [Test]
        public void Recommendation_CreatesCorrectly()
        {
            var recommendation = new StrategicRecommendation(2, 0,
                StrategicObjectiveType.DefensivePreparation, 85, "Measured reason.", 17, 85);

            Assert.That(recommendation.RecommendationId, Is.EqualTo(2));
            Assert.That(recommendation.PlayerId, Is.Zero);
            Assert.That(recommendation.Score, Is.EqualTo(85));
            Assert.That(recommendation.CreatedTick, Is.EqualTo(17));
            Assert.That(recommendation.Priority, Is.EqualTo(85));
            Assert.That(recommendation.Status,
                Is.EqualTo(StrategicRecommendationStatus.Proposed));
        }

        [Test]
        public void Recommendation_UsesExistingObjectiveTypes()
        {
            PropertyInfo objective = typeof(StrategicRecommendation)
                .GetProperty(nameof(StrategicRecommendation.ObjectiveType));

            Assert.That(objective.PropertyType, Is.EqualTo(typeof(StrategicObjectiveType)));
            Assert.That(Enum.GetValues(typeof(StrategicObjectiveType)), Is.EqualTo(new[]
            {
                StrategicObjectiveType.AttackPreparation,
                StrategicObjectiveType.DefensivePreparation,
                StrategicObjectiveType.EconomicExpansion,
                StrategicObjectiveType.MilitaryReinforcement
            }));
        }

        [Test]
        public void Recommendation_PreservesReason()
        {
            const string reason = "Visible threat detected with insufficient defense.";
            var recommendation = new StrategicRecommendation(1, 0,
                StrategicObjectiveType.DefensivePreparation, 80, reason, 0, 80);

            Assert.That(recommendation.Reason, Is.EqualTo(reason));
        }

        [Test]
        public void Evaluator_ReturnsDeterministicResults()
        {
            SetResources(1000, 1000, 1000, 0);
            StrategicContext context = Context();

            var evaluated = evaluator.Evaluate(context);
            string[] first = evaluated.Select(Describe).ToArray();
            string[] second = evaluator.Evaluate(context).Select(Describe).ToArray();

            Assert.That(second, Is.EqualTo(first));
            Assert.That(evaluated.Select(item => item.Score), Is.Ordered.Descending);
        }

        [Test]
        public void Evaluator_ReturnsDefenseRecommendation()
        {
            UnitData enemy = Unit(1, 1, x, z);
            Reveal(enemy);

            StrategicRecommendation recommendation = Find(
                evaluator.Evaluate(Context()), StrategicObjectiveType.DefensivePreparation);

            Assert.That(recommendation, Is.Not.Null);
            Assert.That(recommendation.Reason,
                Is.EqualTo(RuleBasedStrategicEvaluator.DefenseReason));
        }

        [Test]
        public void Evaluator_ReturnsMilitaryRecommendation()
        {
            SetResources(500, 500, 0, 0);

            StrategicRecommendation recommendation = Find(
                evaluator.Evaluate(Context()), StrategicObjectiveType.MilitaryReinforcement);

            Assert.That(recommendation, Is.Not.Null);
            Assert.That(recommendation.Reason,
                Is.EqualTo(RuleBasedStrategicEvaluator.MilitaryReason));
        }

        [Test]
        public void Evaluator_ReturnsEconomicRecommendation()
        {
            SetResources(0, 0, 0, 0);

            StrategicRecommendation recommendation = Find(
                evaluator.Evaluate(Context()), StrategicObjectiveType.EconomicExpansion);

            Assert.That(recommendation, Is.Not.Null);
            Assert.That(recommendation.Reason,
                Is.EqualTo(RuleBasedStrategicEvaluator.EconomyReason));
        }

        [Test]
        public void Evaluator_ReturnsAttackRecommendation()
        {
            SetResources(1000, 0, 700, 0);
            for (int i = 0; i < RuleBasedStrategicEvaluator.StrongMilitaryThreshold; i++)
                Unit(0, 1, x - 20 + i, z + 5);

            StrategicRecommendation recommendation = Find(
                evaluator.Evaluate(Context()), StrategicObjectiveType.AttackPreparation);

            Assert.That(recommendation, Is.Not.Null);
            Assert.That(recommendation.Score, Is.EqualTo(100));
            Assert.That(recommendation.Reason,
                Is.EqualTo(RuleBasedStrategicEvaluator.AttackReason));
        }

        [Test]
        public void Evaluator_ScoresAreDeterministic()
        {
            UnitData enemy = Unit(1, 1, x, z);
            Reveal(enemy);
            StrategicContext context = Context();

            int[] first = evaluator.Evaluate(context).Select(item => item.Score).ToArray();
            int[] second = evaluator.Evaluate(context).Select(item => item.Score).ToArray();

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Has.All.InRange(0, 100));
        }

        [Test]
        public void Evaluator_HigherThreatProducesHigherDefenseScore()
        {
            UnitData firstEnemy = Unit(1, 1, x, z);
            Reveal(firstEnemy);
            int lower = Find(evaluator.Evaluate(Context()),
                StrategicObjectiveType.DefensivePreparation).Score;
            for (int i = 1; i < 5; i++)
            {
                UnitData enemy = Unit(1, 1, x + i, z);
                Reveal(enemy);
            }

            int higher = Find(evaluator.Evaluate(Context()),
                StrategicObjectiveType.DefensivePreparation).Score;

            Assert.That(higher, Is.GreaterThan(lower));
        }

        [Test]
        public void Evaluator_DoesNotUseHiddenInformation()
        {
            UnitData hidden = Unit(1, 7, x, z);
            string[] before = evaluator.Evaluate(Context()).Select(Describe).ToArray();
            hidden.UnitType = 6;
            Unit(1, 1, x + 1, z);
            string[] after = evaluator.Evaluate(Context()).Select(Describe).ToArray();

            Assert.That(after, Is.EqualTo(before));
            Assert.That(Find(evaluator.Evaluate(Context()),
                StrategicObjectiveType.DefensivePreparation), Is.Null);
            Assert.That(Context().Threat.VisibleEnemyMilitaryUnits, Is.Zero);
        }

        [Test]
        public void Evaluator_UsesOnlyStrategicContext()
        {
            MethodInfo method = typeof(IStrategicEvaluator).GetMethod("Evaluate");
            FieldInfo[] instanceFields = typeof(RuleBasedStrategicEvaluator)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.That(method.GetParameters().Select(item => item.ParameterType),
                Is.EqualTo(new[] { typeof(StrategicContext) }));
            Assert.That(instanceFields, Is.Empty,
                "The rules evaluator must not retain simulation or service references.");
        }

        [Test]
        public void Recommendation_CanConvertToStrategicIntent()
        {
            var recommendation = new StrategicRecommendation(4, 0,
                StrategicObjectiveType.MilitaryReinforcement, 85,
                RuleBasedStrategicEvaluator.MilitaryReason, 25, 85);

            StrategicIntent intent = recommendation.ToStrategicIntent(44);

            Assert.That(intent.IntentId, Is.EqualTo(44));
            Assert.That(intent.PlayerId, Is.EqualTo(recommendation.PlayerId));
            Assert.That(intent.ObjectiveType, Is.EqualTo(recommendation.ObjectiveType));
            Assert.That(intent.CreatedTick, Is.EqualTo(recommendation.CreatedTick));
            Assert.That(intent.Priority, Is.EqualTo(recommendation.Priority));
            Assert.That(recommendation.Status,
                Is.EqualTo(StrategicRecommendationStatus.ConvertedToIntent));
            Assert.That(planner.Intents, Is.Empty, "Conversion must not submit the intent.");
        }

        [Test]
        public void Evaluator_DoesNotCreatePlansDirectly()
        {
            SetResources(1000, 1000, 1000, 0);

            var recommendations = evaluator.Evaluate(Context());

            Assert.That(recommendations, Is.Not.Empty);
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(planner.Intents, Is.Empty);
            Assert.That(goalManager.Goals, Is.Empty);
        }

        [Test]
        public void Evaluator_DoesNotCreateCommands()
        {
            SetResources(1000, 1000, 1000, 0);

            evaluator.Evaluate(Context());

            Assert.That(simulation.CommandBuffer.FlushCommands(), Is.Empty);
            Assert.That(goalManager.Goals, Is.Empty);
        }

        private StrategicContext Context()
        {
            return planner.BuildContext(
                new CommanderContextBuilder().Build(simulation, goalManager));
        }

        private static StrategicRecommendation Find(
            System.Collections.Generic.IReadOnlyList<StrategicRecommendation> recommendations,
            StrategicObjectiveType objectiveType)
        {
            return recommendations.FirstOrDefault(item => item.ObjectiveType == objectiveType);
        }

        private static string Describe(StrategicRecommendation recommendation)
        {
            return recommendation.ObjectiveType + ":" + recommendation.Score + ":"
                + recommendation.Priority + ":" + recommendation.Reason + ":"
                + recommendation.CreatedTick;
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
