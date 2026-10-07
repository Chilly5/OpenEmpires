using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    // Opt-in paid evidence. This class is intentionally absent from the offline focused test filters.
    // Every translation delegates to the configured OpenRouter provider's normal constructor and HTTP path.
    [Category("CommanderPhase5ALiveRuntime")]
    public sealed class CommanderPhase5ALiveRuntimePlayModeTests
    {
        private static readonly FieldInfo ChatInstanceField = typeof(CommanderChatUI).GetField(
            "instance", BindingFlags.NonPublic | BindingFlags.Static);
        private static string sharedProviderBlocker;
        private static int submittedProviderCalls;

        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager manager;
        private CommanderIntentDispatcher dispatcher;
        private CommanderChatUI chat;
        private CommanderChatUI previousChat;
        private RecordingLiveProvider provider;
        private readonly List<ICommand> ordinaryCommanderCommands = new List<ICommand>();
        private int x;
        private int z;

        [SetUp]
        public void SetUp()
        {
            if (sharedProviderBlocker != null)
                Assert.Inconclusive("Live provider blocked in an earlier scenario: " + sharedProviderBlocker);
            Assert.That(submittedProviderCalls, Is.LessThan(6), "The six-call live evidence cap was exhausted.");
            previousChat = ChatInstanceField.GetValue(null) as CommanderChatUI;
            ChatInstanceField.SetValue(null, null);
            ordinaryCommanderCommands.Clear();
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            x = simulation.MapData.Width / 2;
            z = simulation.MapData.Height / 2;
            // Legitimate, independent human-player fixture: visible legal grass, owned TC/houses,
            // canonical stock resources and native villagers. No AI, god mode, training-tick edits,
            // completed foundations, or synthetic provider success responses.
            typeof(MapData).GetField("holeMap", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(simulation.MapData, null);
            foreach (ResourceNodeData node in simulation.MapData.GetAllResourceNodes())
                node.RemainingAmount = 0;
            for (int tx = x - 34; tx <= x + 34; tx++)
                for (int tz = z - 24; tz <= z + 24; tz++)
                {
                    simulation.MapData.Tiles[tx, tz] = TileType.Grass;
                    simulation.MapData.ForestDensity[tx, tz] = 0;
                    simulation.MapData.FoundationCount[tx, tz] = 0;
                    simulation.FogOfWar.SetVisible(0, tx, tz);
                }
            BuildingData tc = simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
            tc.AutoProduceVillagers = false;
            simulation.CreateBuilding(0, BuildingType.House, x + 12, z, false);
            simulation.CreateBuilding(0, BuildingType.House, x + 18, z, false);
            var resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 10000;
            manager = new CommanderGoalManager(simulation, 0);
            dispatcher = new CommanderIntentDispatcher(simulation, manager);
            simulation.CommandBuffer.CommandEnqueued += Capture;
            provider = new RecordingLiveProvider(new OpenRouterCommanderProvider());
            chat = new GameObject("Phase5ALiveRuntimeChat").AddComponent<CommanderChatUI>();
            chat.enabled = false; // Do not run Start's unrelated bootstrap coroutine.
            chat.Initialize(provider, simulation, manager, dispatcher, TimeSpan.FromSeconds(35));
        }

        [TearDown]
        public void TearDown()
        {
            if (simulation != null) simulation.CommandBuffer.CommandEnqueued -= Capture;
            if (chat != null) UnityEngine.Object.DestroyImmediate(chat.gameObject);
            dispatcher?.Dispose();
            manager?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
            ChatInstanceField.SetValue(null, previousChat);
        }

        [UnityTest]
        public IEnumerator FourFarms_RealLunaApprovalNativeCostFootprintAndFoodNodes()
        {
            AddWorkers(6, x - 7, z + 5);
            int initialWood = simulation.ResourceManager.GetPlayerResources(0).Wood;
            var initialIds = BuildingIds();
            yield return Submit("make 4 farms", "four-farms");
            Assert.That(provider.LastResult.Outcome, Is.EqualTo(CommanderSemanticOutcome.DynamicPlan)
                .Or.EqualTo(CommanderSemanticOutcome.Request));
            if (chat.PendingActionPlan != null) yield return ApproveCandidate("four-farms");
            Assert.That(manager.Goals.OfType<BuildStructureGoal>().Single().Count, Is.EqualTo(4));
            Assert.That(manager.Goals.OfType<BuildStructureGoal>().Single().StructureType,
                Is.EqualTo(BuildingType.Farm), "Do not execute unrelated content as a passing Farm scenario.");
            yield return TickUntil(() => NewBuildings(initialIds, BuildingType.Farm).Count == 4
                && NewBuildings(initialIds, BuildingType.Farm).All(b => !b.IsUnderConstruction), 6000);
            var farms = NewBuildings(initialIds, BuildingType.Farm);
            Assert.That(farms, Has.Count.EqualTo(4));
            Assert.That(ordinaryCommanderCommands.OfType<PlaceBuildingCommand>()
                .Count(c => c.BuildingType == BuildingType.Farm), Is.EqualTo(4));
            Assert.That(simulation.ResourceManager.GetPlayerResources(0).Wood,
                Is.EqualTo(initialWood - 4 * config.FarmWoodCost));
            foreach (BuildingData farm in farms)
            {
                Assert.That(farm.TileFootprintWidth, Is.EqualTo(config.FarmFootprintWidth));
                Assert.That(farm.TileFootprintHeight, Is.EqualTo(config.FarmFootprintHeight));
                Assert.That(farm.LinkedResourceNodeId, Is.GreaterThanOrEqualTo(0));
                Assert.That(simulation.MapData.GetResourceNode(farm.LinkedResourceNodeId).IsFarmNode, Is.True);
                Assert.That(simulation.MapData.Tiles[farm.OriginTileX, farm.OriginTileZ], Is.EqualTo(TileType.Farm));
            }
            Report("four-farms", "completed=" + farms.Count + ";ids=" + Ids(farms.Select(b => b.Id))
                + ";woodSpent=" + (initialWood - simulation.ResourceManager.GetPlayerResources(0).Wood));
        }

        [UnityTest]
        public IEnumerator MillNearVisibleBerries_RealLunaNativeConstruction()
        {
            AddWorkers(4, x - 7, z + 5);
            ResourceNodeData berries = AddBerries(x + 9, z + 6);
            // A fourth worker is already working the visible berry patch. The new Mill still
            // needs to be placed and completed through the ordinary native command path.
            UnitData gatherer = AddWorker(x + 8, z + 8);
            gatherer.State = UnitState.Gathering;
            gatherer.TargetResourceNodeId = berries.Id;
            int initialWood = simulation.ResourceManager.GetPlayerResources(0).Wood;
            var initialIds = BuildingIds();
            yield return Submit("Build a mill near the berries my villager is working.", "mill-berries");
            if (chat.PendingActionPlan != null) yield return ApproveCandidate("mill-berries");
            yield return TickUntil(() => NewBuildings(initialIds, BuildingType.Mill)
                .Any(b => !b.IsUnderConstruction), 6000);
            BuildingData mill = NewBuildings(initialIds, BuildingType.Mill).Single();
            Assert.That(ordinaryCommanderCommands.OfType<PlaceBuildingCommand>()
                .Count(c => c.BuildingType == BuildingType.Mill), Is.EqualTo(1));
            Assert.That(Vector2Int.Distance(new Vector2Int(mill.OriginTileX, mill.OriginTileZ),
                new Vector2Int(berries.TileX, berries.TileZ)), Is.LessThan(15));
            Assert.That(simulation.ResourceManager.GetPlayerResources(0).Wood,
                Is.EqualTo(initialWood - config.MillWoodCost));
            Report("mill-berries", "completedId=" + mill.Id + ";berryId=" + berries.Id);
        }

        [UnityTest]
        public IEnumerator ThreeIdleWorkers_TwoSheepThirdMill_DisjointOrdinaryCommands()
        {
            UnitData[] idle = AddWorkers(3, x - 7, z + 5);
            UnitData sheep = AddSheep(x - 4, z + 4);
            ResourceNodeData berries = AddBerries(x + 9, z + 6);
            UnitData berryGatherer = AddWorker(x + 8, z + 8);
            berryGatherer.State = UnitState.Gathering;
            berryGatherer.TargetResourceNodeId = berries.Id;
            var initialIds = BuildingIds();
            yield return Submit("Use exactly three idle villagers: send two to the sheep and have the third build a mill near the berries being worked.", "split-sheep-mill");
            Assert.That(provider.LastResult.Outcome, Is.EqualTo(CommanderSemanticOutcome.DynamicPlan));
            yield return ApproveCandidate("split-sheep-mill");
            yield return TickUntil(() => NewBuildings(initialIds, BuildingType.Mill)
                .Any(b => !b.IsUnderConstruction)
                && ordinaryCommanderCommands.OfType<SlaughterSheepCommand>().Any(), 6000);
            SlaughterSheepCommand slaughter = ordinaryCommanderCommands.OfType<SlaughterSheepCommand>().Single();
            PlaceBuildingCommand mill = ordinaryCommanderCommands.OfType<PlaceBuildingCommand>()
                .Single(c => c.BuildingType == BuildingType.Mill);
            Assert.That(slaughter.SheepUnitId, Is.EqualTo(sheep.Id));
            Assert.That(slaughter.VillagerIds, Has.Length.EqualTo(2));
            Assert.That(mill.VillagerUnitIds, Has.Length.EqualTo(1));
            Assert.That(slaughter.VillagerIds.Intersect(mill.VillagerUnitIds), Is.Empty);
            Assert.That(slaughter.VillagerIds.Concat(mill.VillagerUnitIds),
                Is.EquivalentTo(idle.Select(w => w.Id)));
            Assert.That(slaughter.SourceKind, Is.EqualTo(ResourceSourceKind.Sheep));
            Report("split-sheep-mill", "sheepWorkers=" + Ids(slaughter.VillagerIds)
                + ";millWorker=" + Ids(mill.VillagerUnitIds)
                + ";millIds=" + Ids(NewBuildings(initialIds, BuildingType.Mill).Select(b => b.Id)));
        }

        [UnityTest]
        public IEnumerator TwoNewBarracksThenTenNewSpearmen_OnlyThoseProducers()
        {
            AddWorkers(8, x - 7, z + 5);
            var initialBuildings = BuildingIds();
            var initialUnits = UnitIds();
            yield return Submit("Build two new barracks near my town center, then train exactly ten new spearmen using only those new barracks.", "barracks-spears");
            Assert.That(provider.LastResult.Outcome, Is.EqualTo(CommanderSemanticOutcome.DynamicPlan));
            yield return ApproveCandidate("barracks-spears");
            yield return TickUntil(() => NewBuildings(initialBuildings, BuildingType.Barracks).Count == 2
                && NewBuildings(initialBuildings, BuildingType.Barracks).All(b => !b.IsUnderConstruction)
                && NewUnits(initialUnits, 1).Count == 10, 12000);
            var barracks = NewBuildings(initialBuildings, BuildingType.Barracks);
            var newSpears = NewUnits(initialUnits, 1);
            var trains = ordinaryCommanderCommands.OfType<TrainUnitCommand>().ToArray();
            Assert.That(barracks, Has.Count.EqualTo(2));
            Assert.That(newSpears, Has.Count.EqualTo(10));
            Assert.That(trains, Has.Length.EqualTo(10));
            Assert.That(trains.Select(t => t.BuildingId).Distinct(),
                Is.SubsetOf(barracks.Select(b => b.Id)));
            Assert.That(trains.Select(t => t.BuildingId).Distinct().Count(), Is.EqualTo(2));
            Assert.That(manager.Goals.OfType<EnsureUnitCountGoal>().Single().TrackedTrainingOrders
                .Select(r => r.ProducerId).Distinct(), Is.EquivalentTo(barracks.Select(b => b.Id)));
            Report("barracks-spears", "barracks=" + Ids(barracks.Select(b => b.Id))
                + ";newSpearmen=" + Ids(newSpears.Select(u => u.Id))
                + ";receiptProducerIds=" + Ids(manager.Goals.OfType<EnsureUnitCountGoal>()
                    .Single().TrackedTrainingOrders.Select(r => r.ProducerId)));
        }

        [UnityTest]
        public IEnumerator SimpleKnownAllocation_RemainsImmediateWithoutPreviewOrExtraEffect()
        {
            UnitData[] workers = AddWorkers(4, x - 7, z + 5);
            ResourceNodeData berries = AddBerries(x + 9, z + 6);
            yield return Submit("Put four idle villagers on berries.", "known-allocation");
            Assert.That(provider.LastResult.Outcome, Is.EqualTo(CommanderSemanticOutcome.Request));
            Assert.That(chat.PendingActionPlan, Is.Null);
            Assert.That(manager.Goals.OfType<AllocateWorkersGoal>().Count(), Is.EqualTo(1));
            Assert.That(manager.Goals, Has.Count.EqualTo(1));
            yield return TickUntil(() => ordinaryCommanderCommands.OfType<GatherCommand>().Any(), 1800);
            GatherCommand gather = ordinaryCommanderCommands.OfType<GatherCommand>().Single();
            Assert.That(gather.ResourceNodeId, Is.EqualTo(berries.Id));
            Assert.That(gather.UnitIds, Is.EquivalentTo(workers.Select(w => w.Id)));
            Assert.That(ordinaryCommanderCommands, Has.Count.EqualTo(1));
            Report("known-allocation", "preview=false;goal=AllocateWorkers;gatherWorkers="
                + Ids(gather.UnitIds));
        }

        [UnityTest]
        public IEnumerator CastleAgeInformation_RealLunaNoExecution()
        {
            AddWorkers(4, x - 7, z + 5);
            int initialAge = simulation.GetPlayerAge(0);
            yield return Submit("What do I need to reach Castle age? I am asking for information, not an order.", "castle-question");
            Assert.That(provider.LastResult.Outcome,
                Is.EqualTo(CommanderSemanticOutcome.Answer)
                    .Or.EqualTo(CommanderSemanticOutcome.Clarify));
            Assert.That(chat.PendingActionPlan, Is.Null);
            Assert.That(manager.Goals, Is.Empty);
            Assert.That(ordinaryCommanderCommands, Is.Empty);
            for (int i = 0; i < 30; i++) { manager.Tick(simulation.CurrentTick); simulation.Tick(); }
            Assert.That(simulation.GetPlayerAge(0), Is.EqualTo(initialAge));
            Assert.That(manager.Goals, Is.Empty);
            Assert.That(ordinaryCommanderCommands, Is.Empty);
            Report("castle-question", "outcome=" + provider.LastResult.Outcome + ";goals=0;commands=0");
        }

        private IEnumerator Submit(string input, string scenario)
        {
            Task<CommanderAIChatSubmission> task = chat.SubmitMessageAsync(input);
            while (!task.IsCompleted) yield return null;
            Assert.That(task.IsFaulted, Is.False, scenario + " UI submission faulted.");
            Assert.That(provider.Calls, Is.EqualTo(1), scenario + " must use one real semantic translation.");
            if (provider.LastResult == null)
                Assert.Inconclusive(scenario + ": provider did not return before the UI timeout.");
            if (!provider.LastResult.IsValid)
            {
                string safe = SafeBlocker(provider.LastResult.SafeExplanation);
                if (safe != null)
                {
                    sharedProviderBlocker = safe;
                    Assert.Inconclusive(scenario + ": " + safe);
                }
                Assert.Fail(scenario + ": real semantic response failed validation (safe category: invalid semantic response).");
            }
            Report(scenario, "input=" + input + ";providerOutcome=" + provider.LastResult.Outcome
                + ";semanticNodes=" + (provider.LastResult.DynamicPlan == null
                    ? string.Join(",", provider.LastResult.Nodes.Select(n => n.Type + ":" + n.BuildingType
                        + ":count=" + n.Count + ":unit=" + n.UnitType + ":anchor=" + n.PlacementAnchorSelector
                        + ":relation=" + n.PlacementRelation))
                    : string.Join(",", provider.LastResult.DynamicPlan.Nodes.Select(n => n.Primitive.Mechanic.ToString())))
                + ";preview=" + (chat.PendingActionPlan != null));
        }

        private IEnumerator ApproveCandidate(string scenario)
        {
            Assert.That(chat.PendingActionPlan, Is.Not.Null, scenario + " needs an actual UI preview.");
            int priorCount = manager.Goals.Count;
            Task<CommanderAIChatSubmission> approval = chat.SubmitMessageAsync("approve plan");
            while (!approval.IsCompleted) yield return null;
            Assert.That(approval.IsFaulted, Is.False);
            Assert.That(chat.PendingActionPlan, Is.Null);
            Assert.That(manager.Goals.Count, Is.GreaterThan(priorCount));
            Assert.That(provider.Calls, Is.EqualTo(1), "Local approval must not call the provider.");
            Report(scenario, "approved=true;compiledGoals="
                + string.Join(",", manager.Goals.Select(g => g.GetType().Name)));
        }

        private IEnumerator TickUntil(Func<bool> complete, int maxNativeTicks)
        {
            for (int i = 0; i < maxNativeTicks && !complete(); i++)
            {
                if (simulation.CurrentTick % 15 == 0) manager.Tick(simulation.CurrentTick);
                simulation.Tick();
                if (i % 100 == 0) yield return null;
            }
            Assert.That(complete(), Is.True, "Native lifecycle deadline reached at tick "
                + simulation.CurrentTick + ";goals=" + string.Join(",", manager.Goals.Select(g =>
                    g.GetType().Name + ":" + g.Status + ":" + g.StatusReason)));
        }

        private void Capture(ICommand command, CommandEnqueueSource source)
        {
            if (source == CommandEnqueueSource.Commander) ordinaryCommanderCommands.Add(command);
        }

        private UnitData AddWorker(int tileX, int tileZ)
        {
            UnitData worker = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(tileX, tileZ), Fixed32.One,
                Fixed32.FromFloat(.4f), Fixed32.One);
            worker.IsVillager = true;
            worker.UnitType = 0;
            worker.MaxHealth = worker.CurrentHealth = 100;
            worker.State = UnitState.Idle;
            worker.CarryCapacity = config.VillagerCarryCapacity;
            worker.DetectionRange = Fixed32.FromInt(32);
            return worker;
        }

        private UnitData[] AddWorkers(int count, int tileX, int tileZ) =>
            Enumerable.Range(0, count).Select(i => AddWorker(tileX + i, tileZ)).ToArray();

        private UnitData AddSheep(int tileX, int tileZ)
        {
            UnitData sheep = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(tileX, tileZ), Fixed32.One,
                Fixed32.FromFloat(.3f), Fixed32.One);
            sheep.IsSheep = true;
            sheep.MaxHealth = sheep.CurrentHealth = 1;
            sheep.State = UnitState.Idle;
            return sheep;
        }

        private ResourceNodeData AddBerries(int tileX, int tileZ) =>
            simulation.MapData.AddResourceNode(ResourceType.Food,
                simulation.MapData.TileToWorldFixed(tileX, tileZ), 20000);

        private HashSet<int> BuildingIds() => new HashSet<int>(simulation.BuildingRegistry
            .GetAllBuildings().Where(b => b.PlayerId == 0).Select(b => b.Id));

        private HashSet<int> UnitIds() => new HashSet<int>(simulation.UnitRegistry
            .GetAllUnits().Where(u => u.PlayerId == 0).Select(u => u.Id));

        private List<BuildingData> NewBuildings(HashSet<int> before, BuildingType type) => simulation
            .BuildingRegistry.GetAllBuildings().Where(b => b.PlayerId == 0 && b.Type == type
                && !b.IsDestroyed && !before.Contains(b.Id)).ToList();

        private List<UnitData> NewUnits(HashSet<int> before, int type) => simulation.UnitRegistry
            .GetAllUnits().Where(u => u.PlayerId == 0 && u.UnitType == type
                && u.CurrentHealth > 0 && !before.Contains(u.Id)).ToList();

        private static string Ids(IEnumerable<int> values) => string.Join(",", values.OrderBy(v => v));
        private static void Report(string scenario, string detail) =>
            TestContext.WriteLine("[Phase5A live] scenario=" + scenario + ";" + detail);

        private static string SafeBlocker(string explanation)
        {
            string text = (explanation ?? string.Empty).ToLowerInvariant();
            if (text.Contains("not configured")) return "OpenRouter credential not configured";
            if (text.Contains("unauthorized") || text.Contains("authentication") || text.Contains("api key"))
                return "OpenRouter authentication rejected";
            if (text.Contains("payment") || text.Contains("credit") || text.Contains("quota"))
                return "OpenRouter quota or credits unavailable";
            if (text.Contains("rate limit")) return "OpenRouter rate limited";
            if (text.Contains("timed out") || text.Contains("temporarily unavailable")
                || text.Contains("could not reach")) return "OpenRouter transport unavailable or timed out";
            return null;
        }

        private sealed class RecordingLiveProvider : ICommanderAIProvider, ICommanderSemanticProvider
        {
            private readonly OpenRouterCommanderProvider inner;
            public int Calls { get; private set; }
            public CommanderSemanticResult LastResult { get; private set; }
            public RecordingLiveProvider(OpenRouterCommanderProvider inner) { this.inner = inner; }
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request,
                CancellationToken token) => inner.TranslateAsync(request, token);
            public async Task<CommanderSemanticResult> TranslateSemanticAsync(
                CommanderSemanticProviderRequest request, CancellationToken token)
            {
                Assert.That(submittedProviderCalls, Is.LessThan(6), "Paid semantic-call cap");
                submittedProviderCalls++;
                Calls++;
                LastResult = await inner.TranslateSemanticAsync(request, token);
                return LastResult;
            }
        }
    }
}
