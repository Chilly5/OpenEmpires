using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenEmpires.TestSupport;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    [Category("CommanderLiveProvider")]
    public sealed class CommanderPhase5CLiveHouseDiagnosticTests
    {
        internal const string OptIn = "OpenEmpires.Phase5C.HouseDiagnostic.AllowOneRun";
        private const string RunId = "b814ee9b-bd36-46ba-8c6c-881d6a894136";

        [Serializable]
        private sealed class SafeResult
        {
            public string utterance, outcome, schemaDiagnostic, safeTrace;
            public bool valid;
            public int initialAttempts, repairAttempts, httpStatus, estimatedInput;
            public string[] nodeTypes, structures;
        }

        [Serializable]
        private sealed class SafeUiResult
        {
            public string utterance, stage, safeTrace, requestState;
            public string promptTokens, completionTokens, cost;
            public bool previewPresent;
            public int initialAttempts, repairAttempts, httpStatus, estimatedInput, goalCountBeforeApproval;
            public int goalCountAfterApproval, tick, houseCountBefore, houseCountAfter, woodBefore, woodAfter;
            public string[] goalStatuses, commandTypes, attemptKinds, attemptStages,
                attemptInputEstimates, attemptPromptTokens, attemptCompletionTokens, attemptCosts,
                attemptElapsedMs;
        }

        [UnityTest]
        [Ignore("Historical invalid duplicate-UI harness; retained native failure, never replay paid attempt.")]
        public IEnumerator BuildAHouse_RealUiPreviewApprovalAndNativeHouse()
        {
#if UNITY_EDITOR
            if (!UnityEditor.SessionState.GetBool(OptIn, false))
                Assert.Ignore("Paid UI/native lane requires explicit one-run Editor opt-in.");
            UnityEditor.SessionState.SetBool(OptIn, false);
#else
            Assert.Ignore("This opt-in paid lane is Editor-only.");
#endif
            const string input = "build a house";
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "Docs", "CommanderPhase5C", "evidence", "live-house-ui-1");
            string evidencePath = Path.Combine(directory, "safe-result.json");
            Assert.That(File.Exists(evidencePath), Is.False, "Do not repeat a paid run over first evidence.");
            Directory.CreateDirectory(directory);
            var journal = new EvidenceBudgetJournal(Path.Combine(directory, "paid-usage-ledger.json"),
                "4c7d9d53-6456-4620-ac51-3a86c6bca013", true);
            var transport = new BudgetedSemanticTransport(new CommanderHttpClientTransport(), journal);
            var provider = new OpenRouterCommanderProvider(transport: transport);
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            CommanderGoalManager manager = null;
            CommanderIntentDispatcher dispatcher = null;
            CommanderChatUI chat = null;
            CancellationTokenSource cancellation = null;
            var commands = new List<ICommand>();
            var safe = new SafeUiResult { utterance = input, stage = "setup", promptTokens = "unknown",
                completionTokens = "unknown", cost = "unknown" };
            void Save() => File.WriteAllText(evidencePath, JsonUtility.ToJson(safe, true));
            try
            {
                var simulation = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                simulation.SetPlayerCivilizations(new[] { Civilization.English });
                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
                typeof(MapData).GetField("holeMap", System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance).SetValue(simulation.MapData, null);
                foreach (var node in simulation.MapData.GetAllResourceNodes()) node.RemainingAmount = 0;
                for (int a = x - 25; a <= x + 25; a++) for (int b = z - 25; b <= z + 25; b++)
                {
                    simulation.MapData.Tiles[a, b] = TileType.Grass;
                    simulation.MapData.ForestDensity[a, b] = 0;
                    simulation.MapData.FoundationCount[a, b] = 0;
                    simulation.FogOfWar.SetVisible(0, a, b);
                }
                simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true).AutoProduceVillagers = false;
                for (int i = 0; i < 9; i++)
                {
                    var worker = simulation.UnitRegistry.CreateUnit(0,
                        simulation.MapData.TileToWorldFixed(x + 6 + i % 3, z + 7 + i / 3),
                        Fixed32.FromFloat(2), Fixed32.FromFloat(.4f), Fixed32.One);
                    worker.UnitType = 0; worker.IsVillager = true;
                    worker.MaxHealth = worker.CurrentHealth = 100; worker.State = UnitState.Idle;
                }
                var resources = simulation.ResourceManager.GetPlayerResources(0);
                resources.Food = 0; resources.Wood = 200;
                safe.woodBefore = resources.Wood;
                safe.houseCountBefore = simulation.BuildingRegistry.GetAllBuildings()
                    .Count(b => b.PlayerId == 0 && b.Type == BuildingType.House && !b.IsDestroyed);
                manager = new CommanderGoalManager(simulation, 0);
                dispatcher = new CommanderIntentDispatcher(simulation, manager);
                simulation.CommandBuffer.CommandEnqueued += (command, source) =>
                { if (source == CommandEnqueueSource.Commander) commands.Add(command); };
                chat = new GameObject("Phase5CLiveHouseUi").AddComponent<CommanderChatUI>();
                chat.enabled = false;
                chat.Initialize(provider, simulation, manager, dispatcher, TimeSpan.FromSeconds(15));
                chat.enabled = true;
                chat.OpenCommander();
                chat.InputText = input;
                cancellation = new CancellationTokenSource();
                using (transport.BeginSubmission())
                {
                    var task = chat.SubmitCurrentInputAsync();
                    float start = Time.realtimeSinceStartup;
                    while (!task.IsCompleted && Time.realtimeSinceStartup - start < 20) yield return null;
                    if (!task.IsCompleted) cancellation.Cancel();
                    Assert.That(task.IsCompleted, Is.True, "UI request did not terminate.");
                    Assert.That(task.IsFaulted, Is.False, "UI submission faulted; no raw exception exported.");
                }
                safe.stage = "translated";
                safe.safeTrace = provider.LastRequestTrace;
                safe.initialAttempts = transport.InitialHttpAttempts;
                safe.repairAttempts = transport.RepairHttpAttempts;
                safe.httpStatus = transport.LastHttpStatus;
                safe.estimatedInput = provider.LastInputEstimate ?? -1;
                var usage = provider.LastUsage;
                safe.promptTokens = usage.PromptTokens?.ToString() ?? "unknown";
                safe.completionTokens = usage.CompletionTokens?.ToString() ?? "unknown";
                safe.cost = usage.Cost?.ToString() ?? "unknown";
                safe.attemptKinds = provider.LastSemanticAttempts.Select(a => a.Kind).ToArray();
                safe.attemptStages = provider.LastSemanticAttempts.Select(a => a.ResultStage).ToArray();
                safe.attemptInputEstimates = provider.LastSemanticAttempts.Select(a => a.InputEstimate.ToString()).ToArray();
                safe.attemptPromptTokens = provider.LastSemanticAttempts.Select(a => a.Usage.PromptTokens?.ToString() ?? "unknown").ToArray();
                safe.attemptCompletionTokens = provider.LastSemanticAttempts.Select(a => a.Usage.CompletionTokens?.ToString() ?? "unknown").ToArray();
                safe.attemptCosts = provider.LastSemanticAttempts.Select(a => a.Usage.Cost?.ToString() ?? "unknown").ToArray();
                safe.attemptElapsedMs = provider.LastSemanticAttempts.Select(a => a.ElapsedMilliseconds.ToString()).ToArray();
                safe.previewPresent = chat.PendingActionPlan != null;
                safe.goalCountBeforeApproval = manager.Goals.Count;
                safe.requestState = chat.transform.Find("CommanderCanvas/Panel/LatestRequest")
                    ?.GetComponent("TextMeshProUGUI")?.GetType().GetProperty("text")
                    ?.GetValue(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")
                        .GetComponent("TextMeshProUGUI")) as string;
                Save();
                Assert.That(safe.previewPresent, Is.True, "Provider/UI did not stage a game-side preview.");
                Assert.That(manager.Goals, Is.Empty, "No goal before separate approval.");
                Assert.That(commands, Is.Empty, "No command before separate approval.");
                var approval = chat.SubmitMessageAsync("Approve plan");
                while (!approval.IsCompleted) yield return null;
                Assert.That(approval.IsFaulted, Is.False);
                safe.stage = "approved"; safe.goalCountAfterApproval = manager.Goals.Count; Save();
                Assert.That(manager.Goals, Is.Not.Empty, "Separate approval must admit a normal House goal.");
                for (int i = 0; i < 12000 && manager.Goals.Any(g => !g.IsTerminal); i++)
                {
                    manager.Tick(simulation.CurrentTick); simulation.Tick();
                    if (i % 200 == 0) yield return null;
                }
                safe.stage = "terminal"; safe.tick = simulation.CurrentTick;
                safe.goalStatuses = manager.Goals.Select(g => g.Status.ToString()).ToArray();
                safe.commandTypes = commands.Select(c => c.GetType().Name).Distinct().ToArray();
                safe.houseCountAfter = simulation.BuildingRegistry.GetAllBuildings()
                    .Count(b => b.PlayerId == 0 && b.Type == BuildingType.House && !b.IsDestroyed && !b.IsUnderConstruction);
                safe.woodAfter = resources.Wood; Save();
                Assert.That(manager.Goals.All(g => g.Status == CommanderGoalStatus.Completed), Is.True);
                Assert.That(safe.houseCountAfter, Is.EqualTo(safe.houseCountBefore + 1));
                Assert.That(safe.woodAfter, Is.LessThan(safe.woodBefore));
                Assert.That(commands.OfType<PlaceBuildingCommand>().Count(), Is.EqualTo(1));
            }
            finally
            {
                cancellation?.Cancel(); cancellation?.Dispose();
                if (chat != null) UnityEngine.Object.DestroyImmediate(chat.gameObject);
                dispatcher?.Dispose(); manager?.Dispose();
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [UnityTest]
        public IEnumerator BuildAHouse_ActualSampleSceneUiAndNativeCompletion()
        {
#if UNITY_EDITOR
            if (!UnityEditor.SessionState.GetBool(OptIn, false))
                Assert.Ignore("Paid SampleScene lane requires explicit one-run Editor opt-in.");
            UnityEditor.SessionState.SetBool(OptIn, false);
#else
            Assert.Ignore("Paid SampleScene lane is Editor-only.");
#endif
            const string input = "build a house";
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "Docs", "CommanderPhase5C", "evidence", "live-house-scene-2");
            string evidencePath = Path.Combine(directory, "safe-result.json");
            Assert.That(File.Exists(evidencePath), Is.False, "Preserve the first paid SampleScene attempt.");
            Directory.CreateDirectory(directory);
            var safe = new SafeUiResult { utterance = input, stage = "starting-scene",
                promptTokens = "unknown", completionTokens = "unknown", cost = "unknown" };
            void Save() => File.WriteAllText(evidencePath, JsonUtility.ToJson(safe, true));
            Save();
            var journal = new EvidenceBudgetJournal(Path.Combine(directory, "paid-usage-ledger.json"),
                "de8cbcf9-9091-4f3a-8a63-53fa4574609c", true);
            var transport = new BudgetedSemanticTransport(new CommanderHttpClientTransport(), journal);
            var provider = new OpenRouterCommanderProvider(transport: transport);
            var commands = new List<ICommand>();
            var bootstrap = GameBootstrapper.Instance;
            CommanderChatUI chat = null;
            ICommanderAIProvider originalProvider = null;
            try
            {
                var sampleScene = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
                if (!sampleScene.isLoaded)
                {
                    var loading = SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Additive);
                    Assert.That(loading, Is.Not.Null, "SampleScene is required for the real UI lane.");
                    while (!loading.isDone) yield return null;
                }
                float setupStart = Time.realtimeSinceStartup;
                while (bootstrap == null && Time.realtimeSinceStartup - setupStart < 5)
                { yield return null; bootstrap = GameBootstrapper.Instance; }
                Assert.That(bootstrap, Is.Not.Null, "The real SampleScene bootstrap is required.");
#if UNITY_EDITOR
                Assert.That(UnityEditor.EditorApplication.ExecuteMenuItem(
                    "Open Empires/Commander/Start Local QA Match"), Is.True);
#endif
                Time.timeScale = 1f; // Native game ticks at normal rate after QA setup.
                while ((bootstrap.Simulation == null || bootstrap.Commander == null
                    || bootstrap.CommanderDispatcher == null || CommanderChatUI.Instance == null
                    || !CommanderChatUI.Instance.IsReady)
                    && Time.realtimeSinceStartup - setupStart < 12) yield return null;
                chat = CommanderChatUI.Instance;
                Assert.That(bootstrap.Simulation, Is.Not.Null);
                Assert.That(chat, Is.Not.Null.And.Property("IsReady").True);
                Assert.That(bootstrap.Commander, Is.Not.Null);
                var simulation = bootstrap.Simulation;
                var manager = bootstrap.Commander;
                var resources = simulation.ResourceManager.GetPlayerResources(manager.PlayerId);
                safe.woodBefore = resources.Wood;
                safe.houseCountBefore = simulation.BuildingRegistry.GetAllBuildings()
                    .Count(b => b.PlayerId == manager.PlayerId && b.Type == BuildingType.House
                        && !b.IsDestroyed && !b.IsUnderConstruction);
                Assert.That(safe.woodBefore, Is.GreaterThanOrEqualTo(50),
                    "Actual QA opening lacks House resources; no paid request was dispatched.");
                int priorGoals = manager.Goals.Count;
                simulation.CommandBuffer.CommandEnqueued += (command, source) =>
                { if (source == CommandEnqueueSource.Commander) commands.Add(command); };
                // Same configured production provider/15 s limit on the actual singleton UI,
                // with only the transport wrapped for atomic paid-attempt accounting.
                originalProvider = typeof(CommanderChatUI).GetField("semanticProvider",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(chat) as ICommanderAIProvider;
                chat.Initialize(provider, simulation, manager, bootstrap.CommanderDispatcher,
                    TimeSpan.FromSeconds(15));
                chat.OpenCommander();
                safe.stage = "scene-ready"; Save();
                yield return new WaitForEndOfFrame();
                var before = ScreenCapture.CaptureScreenshotAsTexture();
                if (before != null)
                { File.WriteAllBytes(Path.Combine(directory, "scene-before.png"), before.EncodeToPNG());
                    UnityEngine.Object.Destroy(before); }
                chat.InputText = input;
                using (transport.BeginSubmission())
                {
                    var task = chat.SubmitCurrentInputAsync();
                    float requestStart = Time.realtimeSinceStartup;
                    while (!task.IsCompleted && Time.realtimeSinceStartup - requestStart < 20) yield return null;
                    if (!task.IsCompleted)
                        typeof(CommanderChatUI).GetMethod("CancelActiveSemanticRequest",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                            .Invoke(chat, null);
                    Assert.That(task.IsCompleted, Is.True, "Bounded UI request did not terminate.");
                    Assert.That(task.IsFaulted, Is.False, "UI request faulted; no raw exception exported.");
                }
                safe.stage = "translated";
                safe.safeTrace = provider.LastRequestTrace;
                safe.initialAttempts = transport.InitialHttpAttempts;
                safe.repairAttempts = transport.RepairHttpAttempts;
                safe.httpStatus = transport.LastHttpStatus;
                safe.estimatedInput = provider.LastInputEstimate ?? -1;
                safe.promptTokens = provider.LastUsage.PromptTokens?.ToString() ?? "unknown";
                safe.completionTokens = provider.LastUsage.CompletionTokens?.ToString() ?? "unknown";
                safe.cost = provider.LastUsage.Cost?.ToString() ?? "unknown";
                safe.attemptKinds = provider.LastSemanticAttempts.Select(a => a.Kind).ToArray();
                safe.attemptStages = provider.LastSemanticAttempts.Select(a => a.ResultStage).ToArray();
                safe.attemptInputEstimates = provider.LastSemanticAttempts.Select(a => a.InputEstimate.ToString()).ToArray();
                safe.attemptPromptTokens = provider.LastSemanticAttempts.Select(a => a.Usage.PromptTokens?.ToString() ?? "unknown").ToArray();
                safe.attemptCompletionTokens = provider.LastSemanticAttempts.Select(a => a.Usage.CompletionTokens?.ToString() ?? "unknown").ToArray();
                safe.attemptCosts = provider.LastSemanticAttempts.Select(a => a.Usage.Cost?.ToString() ?? "unknown").ToArray();
                safe.attemptElapsedMs = provider.LastSemanticAttempts.Select(a => a.ElapsedMilliseconds.ToString()).ToArray();
                safe.previewPresent = chat.PendingActionPlan != null;
                safe.goalCountBeforeApproval = manager.Goals.Count;
                Save();
                yield return new WaitForEndOfFrame();
                var preview = ScreenCapture.CaptureScreenshotAsTexture();
                if (preview != null)
                { File.WriteAllBytes(Path.Combine(directory, "scene-preview.png"), preview.EncodeToPNG());
                    UnityEngine.Object.Destroy(preview); }
                Assert.That(safe.previewPresent, Is.True, "Real UI did not stage an actionable House preview.");
                Assert.That(manager.Goals.Count, Is.EqualTo(priorGoals),
                    "No new gameplay goal before explicit approval.");
                Assert.That(commands, Is.Empty, "No native command before explicit approval.");
                var approval = chat.SubmitMessageAsync("Approve plan");
                float approvalStart = Time.realtimeSinceStartup;
                while (!approval.IsCompleted && Time.realtimeSinceStartup - approvalStart < 5) yield return null;
                Assert.That(approval.IsCompleted, Is.True, "Local approval did not settle.");
                Assert.That(approval.IsFaulted, Is.False);
                safe.stage = "approved"; safe.goalCountAfterApproval = manager.Goals.Count; Save();
                Assert.That(manager.Goals.Count, Is.GreaterThan(priorGoals), "Approval did not admit a House goal.");
                var admittedGoals = manager.Goals.Skip(priorGoals).ToArray();
                float buildStart = Time.realtimeSinceStartup;
                while (admittedGoals.Any(g => !g.IsTerminal)
                    && Time.realtimeSinceStartup - buildStart < 60) yield return null;
                safe.stage = "terminal"; safe.tick = simulation.CurrentTick;
                safe.goalStatuses = admittedGoals.Select(g => g.Status.ToString()).ToArray();
                safe.commandTypes = commands.Select(c => c.GetType().Name).Distinct().ToArray();
                safe.houseCountAfter = simulation.BuildingRegistry.GetAllBuildings()
                    .Count(b => b.PlayerId == manager.PlayerId && b.Type == BuildingType.House
                        && !b.IsDestroyed && !b.IsUnderConstruction);
                safe.woodAfter = resources.Wood; Save();
                yield return new WaitForEndOfFrame();
                var built = ScreenCapture.CaptureScreenshotAsTexture();
                if (built != null)
                { File.WriteAllBytes(Path.Combine(directory, "scene-after.png"), built.EncodeToPNG());
                    UnityEngine.Object.Destroy(built); }
                Assert.That(admittedGoals.All(g => g.Status == CommanderGoalStatus.Completed), Is.True);
                Assert.That(safe.houseCountAfter, Is.EqualTo(safe.houseCountBefore + 1));
                Assert.That(safe.woodAfter, Is.LessThan(safe.woodBefore));
                Assert.That(commands.OfType<PlaceBuildingCommand>().Count(), Is.EqualTo(1));
            }
            finally
            {
                Time.timeScale = 1f;
                if (chat != null && originalProvider != null && bootstrap != null
                    && bootstrap.Simulation != null && bootstrap.Commander != null
                    && bootstrap.CommanderDispatcher != null)
                {
                    try { chat.Initialize(originalProvider, bootstrap.Simulation,
                        bootstrap.Commander, bootstrap.CommanderDispatcher, TimeSpan.FromSeconds(15)); }
                    catch (Exception) { Debug.LogWarning("[Commander] Original scene provider could not be restored after the owned test run."); }
                }
            }
        }

        [UnityTest]
        public IEnumerator BuildAHouse_ProductionProviderSchemaDiagnostic()
        {
#if UNITY_EDITOR
            if (!UnityEditor.SessionState.GetBool(OptIn, false))
                Assert.Ignore("Paid diagnostic requires explicit one-run Editor opt-in.");
            UnityEditor.SessionState.SetBool(OptIn, false);
#else
            Assert.Ignore("This opt-in paid diagnostic is Editor-only.");
#endif
            const string input = "build a house";
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "Docs", "CommanderPhase5C", "evidence", "live-house-diagnostic-1");
            string evidencePath = Path.Combine(directory, "safe-result.json");
            Assert.That(File.Exists(evidencePath), Is.False, "Do not repeat a paid diagnostic over its first evidence.");
            Directory.CreateDirectory(directory);
            var journal = new EvidenceBudgetJournal(Path.Combine(directory, "paid-usage-ledger.json"), RunId, true);
            var transport = new BudgetedSemanticTransport(new CommanderHttpClientTransport(), journal);
            var provider = new OpenRouterCommanderProvider(transport: transport);
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            CommanderGoalManager manager = null;
            CancellationTokenSource cancellation = null;
            try
            {
                var simulation = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                simulation.SetPlayerCivilizations(new[] { Civilization.English });
                int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
                simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true).AutoProduceVillagers = false;
                var worker = simulation.UnitRegistry.CreateUnit(0,
                    simulation.MapData.TileToWorldFixed(x + 6, z + 7),
                    Fixed32.FromFloat(2), Fixed32.FromFloat(.4f), Fixed32.One);
                worker.UnitType = 0; worker.IsVillager = true;
                worker.MaxHealth = worker.CurrentHealth = 100; worker.State = UnitState.Idle;
                simulation.ResourceManager.GetPlayerResources(0).Wood = 200;
                manager = new CommanderGoalManager(simulation, 0);
                cancellation = new CancellationTokenSource();
                CommanderSemanticResult result = null;
                using (transport.BeginSubmission())
                {
                    Task<CommanderSemanticResult> task = provider.TranslateSemanticAsync(
                        new CommanderSemanticProviderRequest(input,
                            new CommanderContextBuilder().Build(simulation, manager, input)), cancellation.Token);
                    float start = Time.realtimeSinceStartup;
                    while (!task.IsCompleted && Time.realtimeSinceStartup - start < 20) yield return null;
                    if (!task.IsCompleted) cancellation.Cancel();
                    Assert.That(task.IsCompleted, Is.True, "Owned provider attempt did not terminate.");
                    Assert.That(task.IsFaulted, Is.False, "Provider faulted; no raw exception exported.");
                    result = task.Result;
                }
                var safe = new SafeResult {
                    utterance = input, valid = result.IsValid, outcome = result.Outcome.ToString(),
                    schemaDiagnostic = result.SchemaDiagnostic, safeTrace = provider.LastRequestTrace,
                    initialAttempts = transport.InitialHttpAttempts, repairAttempts = transport.RepairHttpAttempts,
                    httpStatus = transport.LastHttpStatus, estimatedInput = provider.LastInputEstimate ?? -1,
                    nodeTypes = result.Nodes.Select(n => n.Type.ToString()).ToArray(),
                    structures = result.Nodes.Select(n => n.BuildingType?.ToString() ?? "").ToArray()
                };
                File.WriteAllText(evidencePath, JsonUtility.ToJson(safe, true));
                Assert.That(manager.Goals, Is.Empty, "Diagnosis never approves or starts a gameplay goal.");
                Assert.That(result.IsValid, Is.True, "See safe schema trace for actual provider failure.");
                Assert.That(result.Outcome, Is.EqualTo(CommanderSemanticOutcome.Request));
                Assert.That(result.Nodes.Count, Is.EqualTo(1));
                Assert.That(result.Nodes[0].Type, Is.EqualTo(CommanderSemanticNodeType.BuildStructure));
                Assert.That(result.Nodes[0].BuildingType, Is.EqualTo(BuildingType.House));
            }
            finally
            {
                cancellation?.Cancel(); cancellation?.Dispose(); manager?.Dispose();
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
