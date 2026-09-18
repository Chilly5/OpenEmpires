using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4B2")]
    public sealed class CommanderPhase4B2PlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation sim;
        private CommanderGoalManager goals;
        private StrategicPlanner planner;
        private StrategicPipeline pipeline;
        private CommanderIntentDispatcher dispatcher;
        private CommanderChatUI chat;
        private int x, z;

        [SetUp]
        public void SetUp()
        {
            foreach (var existing in UnityEngine.Object.FindObjectsByType<CommanderChatUI>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            sim = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            sim.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            ((int[])typeof(GameSimulation).GetField("playerAges", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(sim))[0] = 3;
            typeof(MapData).GetField("holeMap", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(sim.MapData, null);
            foreach (var node in sim.MapData.GetAllResourceNodes()) node.RemainingAmount = 0;
            x = sim.MapData.Width / 2; z = sim.MapData.Height / 2;
            for (int tx = x - 35; tx <= x + 35; tx++)
                for (int tz = z - 22; tz <= z + 22; tz++)
                {
                    sim.MapData.Tiles[tx, tz] = TileType.Grass;
                    sim.MapData.ForestDensity[tx, tz] = 0;
                    sim.MapData.FoundationCount[tx, tz] = 0;
                    sim.FogOfWar.SetVisible(0, tx, tz);
                }
            sim.CreateBuilding(0, BuildingType.TownCenter, x + 15, z, false, true).AutoProduceVillagers = false;
            sim.CreateBuilding(0, BuildingType.Barracks, x + 8, z, false);
            for (int i = 0; i < 6; i++) sim.CreateBuilding(0, BuildingType.House, x + 19 + i * 3, z + 12, false);
            var enemy = sim.MapData.BasePositions[1];
            sim.CreateBuilding(1, BuildingType.TownCenter, enemy.x, enemy.y, false, true).AutoProduceVillagers = false;
            var resources = sim.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 5000;
            Gatherers(ResourceType.Food, 10, x - 22);
            Gatherers(ResourceType.Gold, 6, x);
            Gatherers(ResourceType.Wood, 4, x - 10);
            for (int i = 0; i < 12; i++) Unit(2, x - 28 + i % 4, z - 10 + i / 4);
            goals = new CommanderGoalManager(sim, 0);
            planner = new StrategicPlanner(goals, Resource);
            pipeline = new StrategicPipeline(sim, goals, planner);
            dispatcher = new CommanderIntentDispatcher(sim, goals, strategicPlanner: planner);
            chat = new GameObject("Phase4B2RuntimeChat").AddComponent<CommanderChatUI>();
            chat.enabled = false; // This test host supplies the runtime instead of the bootstrap coroutine.
            chat.Initialize(new MockAIProvider(), sim, goals, dispatcher);
            chat.InitializeStrategic(new MockStrategicAIProvider(), pipeline);
        }

        [TearDown]
        public void TearDown()
        {
            if (chat != null) UnityEngine.Object.DestroyImmediate(chat.gameObject);
            dispatcher?.Dispose();
            pipeline?.Dispose();
            planner?.Dispose();
            goals?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        [UnityTest]
        public IEnumerator Runtime_ApprovedAttackCompletesAndTacticalSpearmenStillExecute()
        {
            var translation = chat.SubmitMessageAsync("prepare cavalry attack");
            while (!translation.IsCompleted) yield return null;
            Assert.That(chat.PendingStrategicIntent, Is.Not.Null);
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(goals.Goals, Is.Empty);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
            chat.GetComponentsInChildren<Button>(true).Single(b => b.name == "Approve strategy").onClick.Invoke();
            var submission = chat.LatestStrategicDecision.Submission;
            Assert.That(submission.CreatedPlan, Is.True, chat.LatestStrategicDecision.Outcome);
            Assert.That(submission.Plan.Authority, Is.EqualTo(StrategicPlanAuthority.Normal));
            Assert.That(submission.Intent.Source, Is.EqualTo(StrategicIntentSource.AIRecommendation));
            for (int i = 0; i < 30000 && !submission.Plan.IsTerminal; i++)
            {
                planner.Tick(sim.CurrentTick);
                goals.Tick(sim.CurrentTick);
                sim.Tick();
                if (i % 300 == 0) yield return null;
            }
            Assert.That(submission.Plan.Status, Is.EqualTo(StrategicPlanStatus.Completed),
                submission.Plan.OutcomeMessage + " | " + string.Join("; ", goals.ActiveGoals.Select(g => g.StatusReason)));
            Assert.That(sim.BuildingRegistry.GetAllBuildings().Any(b => b.PlayerId == 0 && !b.IsDestroyed
                && !b.IsUnderConstruction && b.Type == BuildingType.Stables), Is.True);
            Assert.That(Count(CommanderIntentCatalog.KnightUnitType), Is.GreaterThanOrEqualTo(6));
            Assert.That(planner.Reservations, Is.Empty);
            var tactical = chat.SubmitMessageAsync("make 10 spearmen");
            while (!tactical.IsCompleted) yield return null;
            Assert.That(tactical.Result.Success, Is.True, tactical.Result.DisplayText);
            var goal = goals.Goals.OfType<EnsureUnitCountGoal>().Single(g => g.RequestedUnitType == CommanderIntentCatalog.SpearmanUnitType);
            for (int i = 0; i < 20000 && !goal.IsTerminal; i++)
            {
                goals.Tick(sim.CurrentTick); sim.Tick();
                if (i % 300 == 0) yield return null;
            }
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
            Assert.That(Count(CommanderIntentCatalog.SpearmanUnitType), Is.GreaterThanOrEqualTo(10));
            Debug.Log("[Phase4B2 Runtime] Preview plans/goals/commands=0; normal approval -> shared pipeline -> Stables built and 6 knights; tactical chat -> 10 spearmen; reservations released.");
        }

        [UnityTest]
        public IEnumerator Runtime_EmergencyRejectsRecommendationAndExplicitConfirmationReplacesAI()
        {
            var request = new StrategicAIRequest("prepare defenses", pipeline.CaptureContext(), planner.IntentIds);
            var defense = new MockStrategicAIProvider().InterpretStrategicIntentAsync(request, default).Result;
            var emergency = planner.SubmitIntent(defense.Intent, true, false).Plan;
            var translation = chat.SubmitMessageAsync("prepare cavalry attack");
            while (!translation.IsCompleted) yield return null;
            var rejected = chat.ApproveStrategicRecommendation();
            Assert.That(rejected.Submission, Is.Null);
            Assert.That(rejected.Outcome, Does.Contain("Emergency"));
            Assert.That(emergency.Status, Is.EqualTo(StrategicPlanStatus.Active));
            translation = chat.SubmitMessageAsync("prepare cavalry attack");
            while (!translation.IsCompleted) yield return null;
            chat.GetComponentsInChildren<Button>(true).Single(b => b.name == "Confirm as command").onClick.Invoke();
            var confirmed = chat.LatestStrategicDecision.Submission;
            Assert.That(confirmed?.CreatedPlan, Is.True, chat.LatestStrategicDecision.Outcome);
            Assert.That(confirmed.Plan.Authority, Is.EqualTo(StrategicPlanAuthority.PlayerOverride));
            Assert.That(confirmed.Plan.Source, Is.EqualTo(StrategicIntentSource.AIConfirmedPlayerCommand));
            Assert.That(emergency.Status, Is.EqualTo(StrategicPlanStatus.Cancelled));
            Assert.That(chat.ConfirmStrategicCommand(), Is.Null);
            var direct = planner.CreateIntent(StrategicObjectiveType.DefensivePreparation);
            var approval = new StrategicApprovalLayer().Evaluate(pipeline.CaptureContext(), direct, direct.Source);
            Assert.That(pipeline.EvaluateApprovedIntentNow(approval).Submission?.CreatedPlan, Is.True);
            translation = chat.SubmitMessageAsync("prepare cavalry attack");
            while (!translation.IsCompleted) yield return null;
            Assert.That(chat.ConfirmStrategicCommand().Submission, Is.Null);
            Assert.That(planner.ActivePlans.Single().Source, Is.EqualTo(StrategicIntentSource.PlayerDirect));
            Debug.Log("[Phase4B2 Runtime] Emergency rejected normal attack; explicit confirmation replaced AI once; subsequent direct-player plan resisted confirmed AI replacement.");
        }

        [UnityTest]
        public IEnumerator Runtime_DestroyedChatRejectsLateStrategicResponse()
        {
            var provider = new DeferredProvider();
            chat.InitializeStrategic(provider, pipeline);
            var translation = chat.SubmitMessageAsync("prepare cavalry attack");
            UnityEngine.Object.Destroy(chat.gameObject);
            yield return null;
            for (int i = 0; i < 120 && !translation.IsCompleted; i++) yield return null;
            Assert.That(translation.IsCompleted, Is.True);
            provider.Complete();
            yield return null;
            Assert.That(planner.Plans, Is.Empty);
            Assert.That(goals.Goals, Is.Empty);
            Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
            Debug.Log("[Phase4B2 Runtime] Destroyed chat cancelled pending translation; late provider response created no plan/goal/command.");
        }

        private sealed class DeferredProvider : IStrategicAIInterpreter
        {
            private StrategicAIRequest request;
            private readonly TaskCompletionSource<StrategicAIProviderResult> completion = new TaskCompletionSource<StrategicAIProviderResult>();
            public Task<StrategicAIProviderResult> InterpretStrategicIntentAsync(StrategicAIRequest value, CancellationToken token)
            { request = value; return completion.Task; }
            public void Complete() => completion.TrySetResult(StrategicAIJson.Parse(
                "{\"intentCategory\":\"Strategic\",\"objectiveType\":\"AttackPreparation\"}", request));
        }

        private int Count(int type) => sim.UnitRegistry.GetAllUnits().Count(u => u.PlayerId == 0 && u.CurrentHealth > 0 && u.UnitType == type);
        private UnitData Unit(int type, int tx, int tz)
        {
            var unit = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(tx, tz),
                Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
            unit.UnitType = type; unit.IsVillager = type == 0;
            unit.MaxHealth = unit.CurrentHealth = 100; unit.State = UnitState.Idle;
            return unit;
        }
        private void Gatherers(ResourceType resource, int count, int start)
        {
            var node = sim.MapData.AddResourceNode(resource, sim.MapData.TileToWorldFixed(start + 4, z + 8), 10000);
            for (int i = 0; i < count; i++)
            {
                var worker = Unit(0, start + 3 + i % 2, z + 7 + i / 2);
                worker.FinalDestination = worker.SimPosition;
                worker.State = UnitState.Gathering; worker.TargetResourceNodeId = node.Id;
            }
        }
        private int Resource(ResourceType type)
        {
            var resources = sim.ResourceManager.GetPlayerResources(0);
            switch (type)
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
