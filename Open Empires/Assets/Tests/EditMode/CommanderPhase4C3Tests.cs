using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    // Focused detached-insights contract and provider-boundary proof.
    [Category("CommanderPhase4C3")]
    public sealed class CommanderPhase4C3Tests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private StrategicPlanner planner;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            goals = new CommanderGoalManager(simulation, 0);
            planner = new StrategicPlanner(goals, CurrentResource);
        }

        [TearDown]
        public void TearDown()
        {
            planner?.Dispose();
            goals?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void ContextRemainsFogSafe()
        {
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            int exploredX = x + 18;
            UnitData hidden = AddUnit(1, 7, x, z);
            UnitData explored = AddUnit(1, 2, exploredX, z);
            ResourceNodeData hiddenNode = simulation.MapData.AddResourceNode(ResourceType.Gold,
                simulation.MapData.TileToWorldFixed(x, z + 3), 777);
            ResourceNodeData exploredNode = simulation.MapData.AddResourceNode(ResourceType.Wood,
                simulation.MapData.TileToWorldFixed(exploredX, z + 3), 888);
            simulation.CreateBuilding(1, BuildingType.Stables, x + 3, z, false);
            simulation.CreateBuilding(1, BuildingType.Barracks, exploredX + 3, z, false);
            simulation.FogOfWar.SetVisible(0, exploredX, z);
            simulation.FogOfWar.SetVisible(0, exploredNode.TileX, exploredNode.TileZ);
            simulation.FogOfWar.DemoteAllVisible(0);
            Assert.That(simulation.FogOfWar.GetVisibility(0, x, z), Is.EqualTo(TileVisibility.Unexplored));
            Assert.That(simulation.FogOfWar.GetVisibility(0, exploredX, z), Is.EqualTo(TileVisibility.Explored));

            string before = ProviderJson(Context(), 41);
            hidden.UnitType = 91;
            explored.UnitType = 92;
            hiddenNode.RemainingAmount = 333;
            exploredNode.RemainingAmount = 444;
            AddUnit(1, 93, x + 1, z);
            AddUnit(1, 94, exploredX + 1, z);
            simulation.CreateBuilding(1, BuildingType.Keep, x + 6, z, false);
            simulation.CreateBuilding(1, BuildingType.Tower, exploredX + 6, z, false);
            simulation.ResourceManager.GetPlayerResources(1).Gold = 987654;
            string after = ProviderJson(Context(), 41);

            Assert.That(after, Is.EqualTo(before),
                "Hidden and explored-only enemy state must not alter the emitted provider request.");
            string safeContextJson = ExtractSafeContext(before);
            SafeContextProjection safeContext = JsonUtility.FromJson<SafeContextProjection>(safeContextJson);
            Assert.That(safeContext.insights, Is.Not.Null,
                "The fog comparison must cover the new serialized insight payload.");
            Assert.That(before, Does.Not.Contain("987654").And.Not.Contain("\"UnitType\":91")
                .And.Not.Contain("\"UnitType\":92"));

            simulation.FogOfWar.SetVisible(0, hiddenNode.TileX, hiddenNode.TileZ);
            simulation.FogOfWar.SetVisible(0, x, z);
            string visible = ProviderJson(Context(), 41);
            Assert.That(visible, Is.Not.EqualTo(after),
                "Currently visible threats remain part of the pre-existing allowed payload.");
            string visibleSafeContextJson = ExtractSafeContext(visible);
            SafeContextProjection visibleProjection =
                JsonUtility.FromJson<SafeContextProjection>(visibleSafeContextJson);
            Assert.That(safeContext.visibleThreats.VisibleEnemyMilitaryUnits, Is.Zero);
            Assert.That(visibleProjection.visibleThreats.VisibleEnemyMilitaryUnits,
                Is.GreaterThan(0));
            Assert.That(NormalizeVisibleThreats(visibleSafeContextJson),
                Is.EqualTo(NormalizeVisibleThreats(safeContextJson)),
                "No field outside the existing visible-threat section may change.");
            Assert.That(ExtractInsightsJson(visibleSafeContextJson),
                Is.EqualTo(ExtractInsightsJson(safeContextJson)),
                "Owned insights must remain independent of enemy visibility.");
        }

        [Test]
        public void ContextSerializationDeterministic()
        {
            StrategicContext baseline = Context();
            var firstMilitary = new List<StrategicMilitaryState>
            {
                new StrategicMilitaryState(7, 1, 2),
                new StrategicMilitaryState(2, 1, 0),
                new StrategicMilitaryState(5, 99, 99),
                new StrategicMilitaryState(2, 2, 0),
                new StrategicMilitaryState(0, 99, 99)
            };
            var secondMilitary = firstMilitary.AsEnumerable().Reverse().ToList();
            var firstProduction = ProductionFixture();
            var secondProduction = firstProduction.AsEnumerable().Reverse().ToList();
            var firstWorkers = new List<StrategicWorkerAllocationState>
            {
                new StrategicWorkerAllocationState(ResourceType.Wood, 2),
                new StrategicWorkerAllocationState(ResourceType.Food, 1)
            };
            var secondWorkers = firstWorkers.AsEnumerable().Reverse().ToList();
            var firstPlans = new List<StrategicPlanState> { PlanState(12), PlanState(4) };
            var secondPlans = firstPlans.AsEnumerable().Reverse().ToList();
            var builder = new StrategicContextInsightsBuilder();

            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("tr-TR");
                string first = ProviderJson(WithCollections(baseline, 3, baseline.Economy.ToList(),
                    firstWorkers, firstMilitary, firstProduction, firstPlans,
                    builder.Build(3, firstWorkers, firstMilitary, firstProduction, firstPlans)), 55);
                CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("fr-FR");
                string second = ProviderJson(WithCollections(baseline, 3,
                    baseline.Economy.Reverse().ToList(), secondWorkers, secondMilitary,
                    secondProduction, secondPlans,
                    builder.Build(3, secondWorkers, secondMilitary, secondProduction, secondPlans)), 55);

                Assert.That(second, Is.EqualTo(first));
                InsightProjection insights = JsonUtility.FromJson<SafeContextProjection>(
                    ExtractSafeContext(first)).insights;
                Assert.That(insights, Is.Not.Null);
                Assert.That(insights.incomeTrendAvailable, Is.False);
                ArmyProjection[] army = insights.armyComposition;
                Assert.That(army.Length, Is.EqualTo(2));
                Assert.That(army[0].unitType, Is.EqualTo(2));
                Assert.That(army[0].ownedShareBasisPoints, Is.EqualTo(7500));
                Assert.That(army[1].unitType, Is.EqualTo(7));
                Assert.That(army[1].ownedShareBasisPoints, Is.EqualTo(2500));
                Assert.That(insights.planProgress.Select(value => value.strategicPlanId),
                    Is.EqualTo(new[] { 4, 12 }));
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUiCulture;
            }
        }

        [Test]
        public void ProviderRequest_SortsProductionIdleCapacityTies()
        {
            StrategicContext baseline = Context();
            var firstProduction = new List<StrategicProductionState>
            {
                new StrategicProductionState("Barracks", 1, 0, 0, 0, 1),
                new StrategicProductionState("Barracks", 1, 0, 0, 0, 0)
            };
            var reversedProduction = firstProduction.AsEnumerable().Reverse().ToList();
            var builder = new StrategicContextInsightsBuilder();
            string first = ProviderJson(WithCollections(baseline, baseline.TotalWorkers,
                baseline.Economy.ToList(),
                baseline.WorkerAllocation.ToList(), baseline.Military.ToList(), firstProduction,
                baseline.ActivePlans.ToList(), builder.Build(baseline.TotalWorkers,
                    baseline.WorkerAllocation, baseline.Military, firstProduction,
                    baseline.ActivePlans)), 55);
            string reversed = ProviderJson(WithCollections(baseline, baseline.TotalWorkers,
                baseline.Economy.ToList(),
                baseline.WorkerAllocation.ToList(), baseline.Military.ToList(), reversedProduction,
                baseline.ActivePlans.ToList(), builder.Build(baseline.TotalWorkers,
                    baseline.WorkerAllocation, baseline.Military, reversedProduction,
                    baseline.ActivePlans)), 55);

            Assert.That(ExtractSafeContext(first), Does.Contain("\"AvailableCapacity\":1")
                .And.Contain("\"AvailableCapacity\":0"));
            Assert.That(reversed, Is.EqualTo(first),
                "The complete Gemini request must not depend on insertion order for capacity-only ties.");
        }

        [Test]
        public void Aggregates_UseHandCheckedIntegerSemantics()
        {
            var builder = new StrategicContextInsightsBuilder();
            StrategicContextInsights zero = builder.Build(0,
                Array.Empty<StrategicWorkerAllocationState>(),
                Array.Empty<StrategicMilitaryState>(),
                Array.Empty<StrategicProductionState>(),
                Array.Empty<StrategicPlanState>());
            Assert.That(zero.IncomeTrendAvailable, Is.False);
            Assert.That(zero.WorkerActivityAvailable, Is.False);
            Assert.That(zero.WorkerActivityBasisPoints, Is.Zero);

            StrategicContextInsights oneOfThree = builder.Build(3,
                new[] { new StrategicWorkerAllocationState(ResourceType.Food, 1) },
                new[]
                {
                    new StrategicMilitaryState(2, 2, 0),
                    new StrategicMilitaryState(7, 1, 2),
                    new StrategicMilitaryState(2, 1, 0),
                    new StrategicMilitaryState(0, 80, 80),
                    new StrategicMilitaryState(5, 80, 80)
                }, ProductionFixture(), Array.Empty<StrategicPlanState>());
            Assert.That(oneOfThree.WorkerActivityAvailable, Is.True);
            Assert.That(oneOfThree.TotalWorkers, Is.EqualTo(3));
            Assert.That(oneOfThree.GatheringWorkers, Is.EqualTo(1));
            Assert.That(oneOfThree.WorkerActivityBasisPoints, Is.EqualTo(3333));
            Assert.That(oneOfThree.ArmyComposition.Select(value => new[]
            {
                value.UnitType, value.OwnedCount, value.QueuedCount, value.OwnedShareBasisPoints
            }), Is.EqualTo(new[] { new[] { 2, 3, 0, 7500 }, new[] { 7, 1, 2, 2500 } }));
            Assert.That(oneOfThree.ProductionPressure.Select(value => new object[]
            {
                value.BuildingType, value.CompletedCount, value.UnderConstructionCount,
                value.ActiveQueueCount, value.QueuedUnitCount, value.IdleCompletedCount
            }), Is.EqualTo(new[]
            {
                new object[] { "Barracks", 2, 1, 1, 4, 1 },
                new object[] { "Stables", 0, 1, 0, 0, 0 }
            }));

            StrategicContextInsights allThree = builder.Build(3,
                new[]
                {
                    new StrategicWorkerAllocationState(ResourceType.Food, 1),
                    new StrategicWorkerAllocationState(ResourceType.Wood, 2)
                }, Array.Empty<StrategicMilitaryState>(),
                Array.Empty<StrategicProductionState>(), Array.Empty<StrategicPlanState>());
            Assert.That(allThree.GatheringWorkers, Is.EqualTo(3));
            Assert.That(allThree.WorkerActivityBasisPoints, Is.EqualTo(10000));

            StrategicContextInsights queuedOnly = builder.Build(0,
                Array.Empty<StrategicWorkerAllocationState>(),
                new[] { new StrategicMilitaryState(7, 0, 2) },
                Array.Empty<StrategicProductionState>(), Array.Empty<StrategicPlanState>());
            Assert.That(queuedOnly.ArmyComposition.Single().OwnedShareBasisPoints, Is.Zero);
            Assert.That(queuedOnly.ArmyComposition.Single().QueuedCount, Is.EqualTo(2));
        }

        [Test]
        public void MilestoneCounts_AreDetachedAndCountCompletedOnly()
        {
            StrategicPlan plan = planner.SubmitIntent(StrategicObjectiveType.DefensivePreparation).Plan;
            StrategicContext before = Context();
            StrategicPlanState detached = before.ActivePlans.Single();
            Assert.That(detached.TotalMilestoneCount, Is.EqualTo(plan.Milestones.Count));
            Assert.That(detached.CompletedMilestoneCount, Is.Zero);

            Assert.That(planner.CompleteMilestoneAndAdvance(plan.StrategicPlanId), Is.True);
            plan.Milestones[1].SetStatus(StrategicMilestoneStatus.Failed);
            plan.Milestones[2].SetStatus(StrategicMilestoneStatus.Skipped);
            StrategicPlanState current = Context().ActivePlans.Single();

            Assert.That(detached.CompletedMilestoneCount, Is.Zero,
                "The earlier snapshot must remain detached from later plan changes.");
            Assert.That(current.CompletedMilestoneCount, Is.EqualTo(1));
            Assert.That(current.TotalMilestoneCount, Is.EqualTo(plan.Milestones.Count));
            Assert.That(current.CurrentMilestone, Is.EqualTo(plan.CurrentMilestone.Name));
            Assert.That(current.MilestoneStatus, Is.EqualTo(plan.CurrentMilestone.Status.ToString()));
        }

        [Test]
        public void Insights_ValidateInputsAndRemainAuthorityFree()
        {
            var builder = new StrategicContextInsightsBuilder();
            Assert.Throws<ArgumentOutOfRangeException>(() => builder.Build(-1,
                Array.Empty<StrategicWorkerAllocationState>(), Array.Empty<StrategicMilitaryState>(),
                Array.Empty<StrategicProductionState>(), Array.Empty<StrategicPlanState>()));
            Assert.Throws<ArgumentNullException>(() => builder.Build(0, null,
                Array.Empty<StrategicMilitaryState>(), Array.Empty<StrategicProductionState>(),
                Array.Empty<StrategicPlanState>()));
            Assert.Throws<ArgumentException>(() => builder.Build(0,
                Array.Empty<StrategicWorkerAllocationState>(),
                new[] { new StrategicMilitaryState(2, -1, 0) },
                Array.Empty<StrategicProductionState>(), Array.Empty<StrategicPlanState>()));
            Assert.Throws<OverflowException>(() => builder.Build(0,
                Array.Empty<StrategicWorkerAllocationState>(),
                new[]
                {
                    new StrategicMilitaryState(2, int.MaxValue, 0),
                    new StrategicMilitaryState(2, 1, 0)
                }, Array.Empty<StrategicProductionState>(), Array.Empty<StrategicPlanState>()));
            Assert.Throws<OverflowException>(() => builder.Build(0,
                Array.Empty<StrategicWorkerAllocationState>(),
                new[]
                {
                    new StrategicMilitaryState(2, int.MaxValue, 0),
                    new StrategicMilitaryState(7, 1, 0)
                }, Array.Empty<StrategicProductionState>(), Array.Empty<StrategicPlanState>()),
                "The army-wide owned denominator must also fit Int32.");

            Type[] insightTypes =
            {
                typeof(StrategicContextInsights), typeof(StrategicArmyCompositionInsight),
                typeof(StrategicProductionPressureInsight), typeof(StrategicPlanProgressInsight)
            };
            Type[] forbidden =
            {
                typeof(GameSimulation), typeof(StrategicPlan), typeof(StrategicPlanner),
                typeof(ICommand), typeof(Delegate), typeof(UnityEngine.Object)
            };
            foreach (Type type in insightTypes)
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance
                    | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    Assert.That(forbidden.Any(value => value.IsAssignableFrom(field.FieldType)),
                        Is.False, type.Name + "." + field.Name);
                    Assert.That(typeof(Delegate).IsAssignableFrom(field.FieldType), Is.False,
                        type.Name + "." + field.Name);
                }
        }

        [Test]
        public void Insights_DoNotRetainCallerCollections()
        {
            var workers = new List<StrategicWorkerAllocationState>
            {
                new StrategicWorkerAllocationState(ResourceType.Food, 1)
            };
            var military = new List<StrategicMilitaryState>
            {
                new StrategicMilitaryState(2, 3, 1)
            };
            var production = ProductionFixture();
            var plans = new List<StrategicPlanState> { PlanState(4) };
            StrategicContextInsights snapshot = new StrategicContextInsightsBuilder().Build(
                3, workers, military, production, plans);

            workers.Clear();
            military.Clear();
            production.Clear();
            plans.Clear();
            Assert.That(snapshot.GatheringWorkers, Is.EqualTo(1));
            Assert.That(snapshot.ArmyComposition.Single().OwnedCount, Is.EqualTo(3));
            Assert.That(snapshot.ProductionPressure.Select(value => value.BuildingType),
                Is.EqualTo(new[] { "Barracks", "Stables" }));
            Assert.That(snapshot.PlanProgress.Single().StrategicPlanId, Is.EqualTo(4));
            Assert.That(((IList<StrategicArmyCompositionInsight>)snapshot.ArmyComposition).IsReadOnly,
                Is.True);
        }

        [Test]
        public void ContextBuilder_RejectsWorkerCountOverflow()
        {
            var detached = new CommanderContext(0, 99,
                new CommanderResourceSnapshot(0, 0, 0, 0), 0, 0, 200, 1, "French",
                new List<CommanderBuildingSnapshot>(),
                new List<CommanderUnitSnapshot>
                {
                    new CommanderUnitSnapshot(0, int.MaxValue, 0),
                    new CommanderUnitSnapshot(0, 1, 0)
                },
                new List<CommanderBuildingSnapshot>(), new List<string>(),
                new List<CommanderGoalSnapshot>(), new List<CommanderVisibleResourceSnapshot>(),
                new List<CommanderUnitOptionSnapshot>(),
                new List<CommanderWorkerAllocationSnapshot>(),
                new List<CommanderVisibleEnemyMilitarySnapshot>());
            Assert.Throws<OverflowException>(() =>
                new StrategicContextBuilder().Build(detached, planner));
        }

        [Test]
        public void LegacyConstructor_OmitsAbsentInsightsAndPreservesArity()
        {
            StrategicContext baseline = Context();
            StrategicContext legacy = new StrategicContext(baseline.PlayerId, baseline.SnapshotTick,
                baseline.Economy.ToList(), baseline.Population, baseline.Military.ToList(),
                baseline.Production.ToList(), baseline.ActivePlans.ToList(),
                baseline.VisibleResources.ToList(), baseline.WorkerAllocation.ToList(),
                baseline.TotalWorkers, baseline.Defense, baseline.Threat, baseline.Feasibility);
            Assert.That(legacy.Insights, Is.Null);
            Assert.That(StrategicAIContextSerializer.Serialize(legacy),
                Does.Not.Contain("\"insights\""));
            Assert.That(typeof(StrategicContext).GetConstructors(BindingFlags.Instance
                | BindingFlags.NonPublic).Any(value => value.GetParameters().Length == 13), Is.True);
            Assert.That(typeof(StrategicPlanState).GetConstructors(BindingFlags.Instance
                | BindingFlags.NonPublic).Single().GetParameters().Length, Is.EqualTo(3));
        }

        [Test]
        public void ProviderRequest_ContainsExplicitPrimitiveInsightContract()
        {
            string providerJson = ProviderJson(Context(), 73);
            string safe = ExtractSafeContext(providerJson);
            InsightProjection insights = JsonUtility.FromJson<SafeContextProjection>(safe).insights;
            Assert.That(insights, Is.Not.Null);
            Assert.That(safe, Does.Contain("\"incomeTrendAvailable\":false")
                .And.Contain("\"workerActivity\":{")
                .And.Contain("\"available\":")
                .And.Contain("\"totalWorkers\":")
                .And.Contain("\"gatheringWorkers\":")
                .And.Contain("\"basisPoints\":")
                .And.Contain("\"armyComposition\":[")
                .And.Contain("\"productionPressure\":[")
                .And.Contain("\"planProgress\":["));
            Assert.That(providerJson, Does.Not.Contain("GameSimulation")
                .And.Not.Contain("StrategicPlanner").And.Not.Contain("StrategicPlan\"")
                .And.Not.Contain("delegate").And.Not.Contain("incomePer"));
        }

        private StrategicContext Context() => new StrategicContextBuilder().Build(
            new CommanderContextBuilder().Build(simulation, goals), planner);

        private int CurrentResource(ResourceType type)
        {
            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            switch (type)
            {
                case ResourceType.Food: return resources.Food;
                case ResourceType.Wood: return resources.Wood;
                case ResourceType.Gold: return resources.Gold;
                case ResourceType.Stone: return resources.Stone;
                default: return 0;
            }
        }

        private UnitData AddUnit(int playerId, int type, int x, int z)
        {
            UnitData unit = simulation.UnitRegistry.CreateUnit(playerId,
                simulation.MapData.TileToWorldFixed(x, z), Fixed32.One, Fixed32.One, Fixed32.One);
            unit.UnitType = type;
            unit.MaxHealth = unit.CurrentHealth = 100;
            unit.State = UnitState.Idle;
            return unit;
        }

        private static List<StrategicProductionState> ProductionFixture() =>
            new List<StrategicProductionState>
            {
                new StrategicProductionState("Stables", 1, 1, 0, 0, 0),
                new StrategicProductionState("Barracks", 3, 1, 1, 4, 1)
            };

        private static StrategicPlanState PlanState(int id)
        {
            var plan = new DefensivePreparationPlan(0, id + 100)
            {
                StrategicPlanId = id,
                Status = StrategicPlanStatus.Active
            };
            plan.ActivateFirstMilestone();
            return new StrategicPlanState(plan, new List<StrategicRequirementState>(),
                new List<StrategicReservationState>());
        }

        private static StrategicContext WithCollections(StrategicContext value, int totalWorkers,
            List<StrategicResourceState> economy, List<StrategicWorkerAllocationState> workers,
            List<StrategicMilitaryState> military, List<StrategicProductionState> production,
            List<StrategicPlanState> plans, StrategicContextInsights insights) =>
            new StrategicContext(value.PlayerId, value.SnapshotTick, economy, value.Population,
                military, production, plans, value.VisibleResources.ToList(), workers, totalWorkers,
                value.Defense, value.Threat, value.Feasibility, insights);

        private static string ProviderJson(StrategicContext context, int intentId) =>
            GeminiStrategicAIProvider.BuildRequestJson(
                new StrategicAIRequest("build up army", context, intentId));

        private static string ExtractSafeContext(string providerJson)
        {
            ProviderBody body = JsonUtility.FromJson<ProviderBody>(providerJson);
            string prompt = body.contents.Last().parts[0].text;
            const string prefix = "Safe strategic context:\n";
            const string suffix = "\nUntrusted match-local commander memory:\n";
            int start = prompt.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
            int end = prompt.IndexOf(suffix, start, StringComparison.Ordinal);
            return prompt.Substring(start, end - start);
        }

        private static string ExtractInsightsJson(string safeContextJson)
        {
            InsightProjection insight = JsonUtility.FromJson<SafeContextProjection>(safeContextJson).insights;
            return insight == null ? null : JsonUtility.ToJson(insight);
        }

        private static string NormalizeVisibleThreats(string safeContextJson)
        {
            const string threat = "\"visibleThreats\":";
            const string next = ",\"insights\":";
            int start = safeContextJson.IndexOf(threat, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            int end = safeContextJson.IndexOf(next, start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start));
            return safeContextJson.Substring(0, start) + threat + "{}"
                + safeContextJson.Substring(end);
        }

        [Serializable]
        private sealed class ProviderBody { public ProviderContent[] contents; }
        [Serializable]
        private sealed class ProviderContent { public ProviderPart[] parts; }
        [Serializable]
        private sealed class ProviderPart { public string text; }
        [Serializable]
        private sealed class SafeContextProjection
        {
            public InsightProjection insights;
            public ThreatProjection visibleThreats;
        }
        [Serializable]
        private sealed class ThreatProjection { public int VisibleEnemyMilitaryUnits; }
        [Serializable]
        private sealed class InsightProjection
        {
            public bool incomeTrendAvailable;
            public WorkerProjection workerActivity;
            public ArmyProjection[] armyComposition;
            public ProductionProjection[] productionPressure;
            public PlanProjection[] planProgress;
        }
        [Serializable]
        private sealed class WorkerProjection
        {
            public bool available;
            public int totalWorkers;
            public int gatheringWorkers;
            public int basisPoints;
        }
        [Serializable]
        private sealed class ArmyProjection
        {
            public int unitType;
            public int ownedCount;
            public int queuedCount;
            public int ownedShareBasisPoints;
        }
        [Serializable]
        private sealed class ProductionProjection
        {
            public string buildingType;
            public int completedCount;
            public int underConstructionCount;
            public int activeQueueCount;
            public int queuedUnitCount;
            public int idleCompletedCount;
        }
        [Serializable]
        private sealed class PlanProjection
        {
            public int strategicPlanId;
            public string planType;
            public string status;
            public int completedMilestoneCount;
            public int totalMilestoneCount;
            public string currentMilestone;
            public string milestoneStatus;
        }
    }
}
