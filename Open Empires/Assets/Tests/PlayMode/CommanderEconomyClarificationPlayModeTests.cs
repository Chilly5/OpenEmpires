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
    [Category("CommanderEconomyClarification")]
    public sealed class CommanderEconomyClarificationPlayModeTests
    {
        private static readonly FieldInfo ChatInstanceField = typeof(CommanderChatUI).GetField("instance",
            BindingFlags.NonPublic | BindingFlags.Static);

        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private CommanderIntentDispatcher dispatcher;
        private CommanderChatUI chat;
        private CommanderChatUI previousChatInstance;
        private SemanticProvider provider;
        private readonly List<ICommand> commanderCommands = new List<ICommand>();
        private int centerX;
        private int centerZ;

        [SetUp]
        public void SetUp()
        {
            commanderCommands.Clear();
            previousChatInstance = ChatInstanceField.GetValue(null) as CommanderChatUI;
            ChatInstanceField.SetValue(null, null);

            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            centerX = simulation.MapData.Width / 2;
            centerZ = simulation.MapData.Height / 2;

            foreach (ResourceNodeData node in simulation.MapData.GetAllResourceNodes())
                node.RemainingAmount = 0;
            for (int x = centerX - 30; x <= centerX + 30; x++)
                for (int z = centerZ - 20; z <= centerZ + 20; z++)
                {
                    simulation.MapData.Tiles[x, z] = TileType.Grass;
                    simulation.MapData.ForestDensity[x, z] = 0;
                    simulation.MapData.FoundationCount[x, z] = 0;
                    simulation.FogOfWar.SetVisible(0, x, z);
                }
            typeof(MapData).GetMethod("ComputeHoleMap", BindingFlags.Public | BindingFlags.Instance)
                .Invoke(simulation.MapData, null);
            simulation.FogOfWar.SetVisionCheat(0, true);

            PlayerResources resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 10000;
            goals = new CommanderGoalManager(simulation, 0);
            dispatcher = new CommanderIntentDispatcher(simulation, goals);
            simulation.CommandBuffer.CommandEnqueued += CaptureCommanderCommand;
            provider = new SemanticProvider();

            chat = new GameObject("CommanderEconomyClarificationTestChat").AddComponent<CommanderChatUI>();
            chat.enabled = false;
            chat.Initialize(provider, simulation, goals, dispatcher);
            // Keep Start's automatic bootstrap coroutine out of this controlled host.
            // Voice and typed submissions still use the real public methods.
        }

        [TearDown]
        public void TearDown()
        {
            if (simulation != null)
                simulation.CommandBuffer.CommandEnqueued -= CaptureCommanderCommand;
            if (chat != null) UnityEngine.Object.DestroyImmediate(chat.gameObject);
            dispatcher?.Dispose();
            goals?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
            ChatInstanceField.SetValue(null, previousChatInstance);
        }

        [UnityTest]
        public IEnumerator FourIdleWorkersToFood_UsesNormalCommands()
        {
            ResourceNodeData berries = AddNode(ResourceType.Food, centerX + 9, centerZ + 4, 20000);
            UnitData[] workers = AddIdleWorkers(4, centerX - 5, centerZ);
            chat.SetVoiceProvider(new MockCommanderSpeechToTextProvider("put four villagers on food"),
                new TestAudioCapture());
            chat.VoiceController.AutoSubmit = true;
            chat.VoiceController.StartRecording();
            Task<CommanderSpeechToTextResult> voice = chat.VoiceController.StopRecordingAndTranscribeAsync();
            while (!voice.IsCompleted) yield return null;

            Assert.That(voice.Result.Success, Is.True);
            Assert.That(voice.Result.Transcript, Is.EqualTo("put four villagers on food"));
            Assert.That(chat.LatestSubmission, Is.Not.Null);
            AllocateWorkersGoal goal = goals.Goals.OfType<AllocateWorkersGoal>().Single();
            Assert.That(goal.Allocation.Count, Is.EqualTo(4));
            Assert.That(goal.Allocation.Workers.State, Is.EqualTo(CommanderWorkerState.Any));

            for (int i = 0; i < 1800 && !goal.IsTerminal; i++)
            {
                goals.Tick(simulation.CurrentTick);
                simulation.Tick();
                if (i % 30 == 0) yield return null;
            }

            int[] selected = SelectedIds(goal);
            Assert.That(selected, Is.EquivalentTo(workers.Select(w => w.Id)));
            GatherCommand[] issued = commanderCommands.OfType<GatherCommand>().ToArray();
            Assert.That(issued, Has.Length.EqualTo(1));
            Assert.That(issued[0].ResourceNodeId, Is.EqualTo(berries.Id));
            Assert.That(issued[0].SourceKind, Is.EqualTo(ResourceSourceKind.Any));
            Assert.That(issued[0].UnitIds, Is.EquivalentTo(selected));
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
            Assert.That(selected.Select(id => simulation.UnitRegistry.GetUnit(id).TargetResourceNodeId),
                Is.All.EqualTo(berries.Id));
        }

        [UnityTest]
        public IEnumerator FourIdleWorkersToOwnedSheep_PreservesSourceThroughExecution()
        {
            ResourceNodeData closerBerries = AddNode(ResourceType.Food, centerX - 3, centerZ + 1, 20000);
            UnitData sheep = AddSheep(0, centerX - 1, centerZ + 1);
            UnitData[] workers = AddIdleWorkers(4, centerX + 5, centerZ - 2);
            provider.Json = AllocationJson(4, ResourceSourceKind.Sheep);
            Task<CommanderAIChatSubmission> submission = chat.SubmitMessageAsync("gather food from sheep with four idle villagers");
            while (!submission.IsCompleted) yield return null;

            AllocateWorkersGoal goal = goals.Goals.OfType<AllocateWorkersGoal>().Single();
            for (int i = 0; i < 1800 && (sheep.State != UnitState.Dead
                || workers.Any(w => w.TargetResourceNodeId < 0)); i++)
            {
                goals.Tick(simulation.CurrentTick);
                simulation.Tick();
                if (i % 30 == 0) yield return null;
            }

            int[] selected = SelectedIds(goal);
            Assert.That(selected, Is.EquivalentTo(workers.Select(w => w.Id)));
            SlaughterSheepCommand[] slaughter = commanderCommands.OfType<SlaughterSheepCommand>().ToArray();
            Assert.That(slaughter, Has.Length.EqualTo(1), "Sheep requests must use the ordinary slaughter command, not berries gathering.");
            Assert.That(slaughter[0].SheepUnitId, Is.EqualTo(sheep.Id));
            Assert.That(slaughter[0].SourceKind, Is.EqualTo(ResourceSourceKind.Sheep));
            Assert.That(slaughter[0].VillagerIds, Is.EquivalentTo(selected));
            Assert.That(closerBerries.IsDepleted, Is.False);

            ResourceNodeData carcass = simulation.MapData.GetAllResourceNodes()
                .Where(node => node.IsCarcass && !node.IsDepleted)
                .OrderBy(node => Vector2Int.Distance(new Vector2Int(node.TileX, node.TileZ),
                    new Vector2Int(centerX - 1, centerZ + 1))).First();
            Assert.That(sheep.State, Is.EqualTo(UnitState.Dead));
            Assert.That(selected.Select(id => simulation.UnitRegistry.GetUnit(id).TargetResourceNodeId),
                Is.All.EqualTo(carcass.Id), "Slaughter must hand each selected worker to the sheep carcass.");
            Assert.That(selected.Select(id => simulation.UnitRegistry.GetUnit(id).GatherSourceKind),
                Is.All.EqualTo(ResourceSourceKind.Sheep));
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);

            int previousGatherCount = commanderCommands.OfType<GatherCommand>().Count();
            carcass.RemainingAmount = 0;
            goals.Tick(simulation.CurrentTick + 15);
            simulation.CommandBuffer.EnqueueCommand(new StopCommand(0, new[] { selected[0] }), CommandEnqueueSource.Human);
            simulation.Tick();
            Assert.That(commanderCommands.OfType<GatherCommand>().Count(), Is.EqualTo(previousGatherCount),
                "A depleted Sheep source must not silently issue a berries fallback.");
            Assert.That(simulation.UnitRegistry.GetUnit(selected[0]).GatherSourceKind, Is.EqualTo(ResourceSourceKind.Any),
                "A human Stop must clear the prior source restriction.");
            Assert.That(workers.Skip(1).Select(w => w.TargetResourceNodeId), Has.No.Member(closerBerries.Id),
                "Native automatic retargeting, not just emitted commands, must preserve Sheep after depletion.");
        }

        [UnityTest]
        public IEnumerator GatherFoodThenCount_UsesRealChatContinuation()
        {
            ResourceNodeData berries = AddNode(ResourceType.Food, centerX + 8, centerZ + 3, 20000);
            UnitData[] workers = AddIdleWorkers(4, centerX - 5, centerZ + 2);
            provider.Json = "{\"outcome\":\"Clarify\",\"message\":\"How many idle villagers should gather food?\","
                + "\"pending\":{\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\",\"countMode\":\"Exact\","
                + "\"workers\":{\"state\":\"Any\"},\"destination\":{\"resource\":\"Food\",\"sourceKind\":\"Any\"}},"
                + "\"missingFields\":[\"Count\"]}";

            Task<CommanderAIChatSubmission> first = chat.SubmitMessageAsync("gather food");
            while (!first.IsCompleted) yield return null;
            Assert.That(first.IsFaulted, Is.False);
            Assert.That(chat.PendingClarification, Is.Not.Null);
            Assert.That(chat.PendingClarification.Draft.Destination.Resource, Is.EqualTo(ResourceType.Food));
            Assert.That(goals.Goals, Is.Empty);

            Task<CommanderAIChatSubmission> reply = chat.SubmitMessageAsync("4");
            while (!reply.IsCompleted) yield return null;
            Assert.That(reply.IsFaulted, Is.False);
            Assert.That(chat.PendingClarification, Is.Null);
            AllocateWorkersGoal goal = goals.Goals.OfType<AllocateWorkersGoal>().Single();
            Assert.That(goals.Goals.OfType<EnsureUnitCountGoal>(), Is.Empty);
            Assert.That(goal.Allocation.Count, Is.EqualTo(4));

            for (int i = 0; i < 1800 && !goal.IsTerminal; i++)
            {
                goals.Tick(simulation.CurrentTick);
                simulation.Tick();
                if (i % 30 == 0) yield return null;
            }
            int[] selected = SelectedIds(goal);
            Assert.That(selected, Is.EquivalentTo(workers.Select(w => w.Id)));
            GatherCommand gather = commanderCommands.OfType<GatherCommand>().Single();
            Assert.That(gather.ResourceNodeId, Is.EqualTo(berries.Id));
            Assert.That(gather.UnitIds, Is.EquivalentTo(selected));
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
        }

        private ResourceNodeData AddNode(ResourceType type, int x, int z, int amount) =>
            simulation.MapData.AddResourceNode(type, simulation.MapData.TileToWorldFixed(x, z), amount);

        private UnitData[] AddIdleWorkers(int count, int startX, int startZ)
        {
            var workers = new UnitData[count];
            for (int i = 0; i < count; i++)
            {
                UnitData worker = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(startX + i, startZ), Fixed32.One,
                    Fixed32.FromFloat(.4f), Fixed32.One);
                worker.IsVillager = true;
                worker.UnitType = 0;
                worker.MaxHealth = worker.CurrentHealth = 100;
                worker.State = UnitState.Idle;
                worker.CarryCapacity = config.VillagerCarryCapacity;
                worker.DetectionRange = Fixed32.FromInt(32);
                workers[i] = worker;
            }
            return workers;
        }

        private UnitData AddSheep(int owner, int x, int z)
        {
            UnitData sheep = simulation.UnitRegistry.CreateUnit(owner,
                simulation.MapData.TileToWorldFixed(x, z), Fixed32.One,
                Fixed32.FromFloat(.3f), Fixed32.One);
            sheep.IsSheep = true;
            sheep.MaxHealth = sheep.CurrentHealth = 1;
            sheep.State = UnitState.Idle;
            simulation.FogOfWar.SetVisible(0, x, z);
            return sheep;
        }

        private static string AllocationJson(int count, ResourceSourceKind source, string state = "Idle") =>
            "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\","
            + "\"countMode\":\"Exact\",\"count\":" + count + ",\"workers\":{\"state\":\"" + state + "\"},"
            + "\"destination\":{\"resource\":\"Food\",\"sourceKind\":\"" + source + "\"}}]}";

        private void CaptureCommanderCommand(ICommand command, CommandEnqueueSource source)
        {
            if (source == CommandEnqueueSource.Commander) commanderCommands.Add(command);
        }

        private static int[] SelectedIds(AllocateWorkersGoal goal) =>
            ((List<int>)typeof(AllocateWorkersGoal).GetField("SelectedWorkerIds",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(goal)).ToArray();

        private sealed class TestAudioCapture : ICommanderAudioCapture
        {
            public bool IsRecording { get; private set; }
            public string CurrentDevice => "Test microphone";
            public bool StartRecording(string deviceName = null, float maxDurationSeconds = 15f)
            { IsRecording = true; return true; }
            public CommanderAudioData StopRecording()
            { IsRecording = false; return new CommanderAudioData(new float[16000], 16000, 1); }
            public void CancelRecording() => IsRecording = false;
            public void Dispose() => IsRecording = false;
        }

        private sealed class SemanticProvider : ICommanderAIProvider, ICommanderSemanticProvider
        {
            public string Json { get; set; } = AllocationJson(4, ResourceSourceKind.Any, "Any");
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request, CancellationToken token) =>
                Task.FromResult(CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure,
                    "The test expects semantic routing."));
            public Task<CommanderSemanticResult> TranslateSemanticAsync(CommanderSemanticProviderRequest request,
                CancellationToken token) => Task.FromResult(CommanderSemanticJson.Parse(Json));
        }
    }
}
