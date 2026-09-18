using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    [Category("Phase3Fix11")]
    public class CommanderPhase3Fix11PlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation sim;
        private CommanderGoalManager goalManager;
        private StrategicPlanner strategicPlanner;
        private StrategicPipeline pipeline;
        private int x;
        private int z;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            sim = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            foreach (ResourceNodeData node in sim.MapData.GetAllResourceNodes())
                node.RemainingAmount = 0;
            sim.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            PlayerResources resources = sim.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 5000;
            x = sim.MapData.Width / 2;
            z = sim.MapData.Height / 2;
            for (int tileX = x - 20; tileX <= x + 20; tileX++)
                for (int tileZ = z - 10; tileZ <= z + 10; tileZ++)
                {
                    sim.MapData.Tiles[tileX, tileZ] = TileType.Grass;
                    sim.MapData.ForestDensity[tileX, tileZ] = 0;
                    sim.MapData.FoundationCount[tileX, tileZ] = 0;
                    sim.FogOfWar.SetVisible(0, tileX, tileZ);
                }
            sim.CreateBuilding(0, BuildingType.TownCenter, x + 12, z, false, true)
                .AutoProduceVillagers = false;
            sim.CreateBuilding(0, BuildingType.House, x + 18, z, false);
            sim.CreateBuilding(0, BuildingType.House, x + 22, z, false);
            goalManager = new CommanderGoalManager(sim, 0);
            strategicPlanner = new StrategicPlanner(goalManager, CurrentResourceAmount);
            pipeline = new StrategicPipeline(sim, goalManager, strategicPlanner);
        }

        [TearDown]
        public void TearDown()
        {
            pipeline.Dispose();
            strategicPlanner.Dispose();
            goalManager.Dispose();
            UnityEngine.Object.DestroyImmediate(config);
        }

        [UnityTest]
        public IEnumerator Runtime_StrategicEvaluationRunsEndToEnd()
        {
            pipeline.EvaluationTrigger.FireTrigger(
                StrategicEvaluationTriggerType.StrategicEvent, "Runtime evaluation proof.");
            pipeline.Tick(0);

            Assert.That(pipeline.LastContext, Is.Not.Null);
            Assert.That(pipeline.LastRecommendations, Is.Not.Empty);
            Assert.That(pipeline.LastDecision.HasSelection, Is.True);
            Assert.That(pipeline.LastSubmission.CreatedPlan, Is.True);
            Debug.Log("[Phase3 Fix 1.1 Runtime] PASS Scenario 1: context, evaluator, decision policy, commitment, and planner executed end-to-end.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Runtime_DispatcherStrategicIntentCreatesPlan()
        {
            using var dispatcher = new CommanderIntentDispatcher(sim, goalManager,
                strategicPlanner: strategicPlanner);

            CommanderIntentSubmission result = dispatcher.SubmitText("prepare cavalry attack");

            Assert.That(result.CreatedPlan, Is.True);
            Assert.That(result.StrategicSubmission.Plan.PlanType,
                Is.EqualTo(StrategicPlanType.CavalryPressure));
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
            Debug.Log("[Phase3 Fix 1.1 Runtime] PASS Scenario 2: the real dispatcher routed a strategic text intent into a strategic plan without emitting a command.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Runtime_CavalryPlanStartsWithoutFutureBudget()
        {
            PlayerResources resources = sim.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Gold = 0;

            StrategicIntentSubmission result = strategicPlanner.SubmitIntent(
                StrategicObjectiveType.AttackPreparation);

            Assert.That(result.CreatedPlan, Is.True);
            Assert.That(result.Plan.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(strategicPlanner.Reservations, Is.Empty);
            Debug.Log("[Phase3 Fix 1.1 Runtime] PASS Scenario 3: cavalry preparation started with zero stored Food and Gold because future budget is informational.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Runtime_CommitmentPreventsStrategyThrashing()
        {
            strategicPlanner.SubmitIntent(StrategicObjectiveType.AttackPreparation);

            StrategicIntentSubmission blocked = strategicPlanner.SubmitIntent(
                StrategicObjectiveType.MilitaryReinforcement);

            Assert.That(blocked.CreatedPlan, Is.False);
            Assert.That(blocked.Error,
                Is.EqualTo(StrategicIntentValidationError.CommitmentBlocked));
            Assert.That(strategicPlanner.ActivePlans, Has.Count.EqualTo(1));
            Debug.Log("[Phase3 Fix 1.1 Runtime] PASS Scenario 4: commitment rejected a rapid conflicting strategic transition.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Runtime_WorkerSelectionIsDeterministicAndBounded()
        {
            UnitData far = Worker(x - 12, z);
            UnitData near = Worker(x + 1, z);
            Resource(ResourceType.Wood, x + 3, z);
            goalManager.ResetDiagnosticPathCheckCount();
            goalManager.SubmitResourceAllocation(ResourceType.Wood, 1);
            goalManager.Tick(0);

            GatherCommand command = sim.CommandBuffer.FlushCommands()
                .OfType<GatherCommand>().Single();
            Assert.That(command.UnitIds.Single(), Is.EqualTo(near.Id));
            Assert.That(command.UnitIds.Single(), Is.Not.EqualTo(far.Id));
            Assert.That(goalManager.DiagnosticPathCheckCount,
                Is.LessThanOrEqualTo(CommanderPlanner.MaximumPathValidationCandidates));
            Debug.Log("[Phase3 Fix 1.1 Runtime] PASS Scenario 5: distance-first worker selection chose the nearest worker with bounded path validation.");
            yield return null;
        }

        private int CurrentResourceAmount(ResourceType resourceType)
        {
            PlayerResources resources = sim.ResourceManager.GetPlayerResources(0);
            switch (resourceType)
            {
                case ResourceType.Food: return resources.Food;
                case ResourceType.Wood: return resources.Wood;
                case ResourceType.Gold: return resources.Gold;
                case ResourceType.Stone: return resources.Stone;
                default: return 0;
            }
        }

        private UnitData Worker(int tileX, int tileZ)
        {
            UnitData unit = sim.UnitRegistry.CreateUnit(0,
                sim.MapData.TileToWorldFixed(tileX, tileZ), Fixed32.One,
                Fixed32.FromFloat(.4f), Fixed32.One);
            unit.IsVillager = true;
            unit.MaxHealth = unit.CurrentHealth = 100;
            unit.State = UnitState.Idle;
            return unit;
        }

        private ResourceNodeData Resource(ResourceType type, int tileX, int tileZ)
        {
            ResourceNodeData node = sim.MapData.AddResourceNode(type,
                sim.MapData.TileToWorldFixed(tileX, tileZ), 10000);
            sim.FogOfWar.SetVisible(0, node.TileX, node.TileZ);
            return node;
        }
    }
}
