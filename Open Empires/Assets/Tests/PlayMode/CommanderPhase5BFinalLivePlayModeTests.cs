using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenEmpires.TestSupport;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    [Category("CommanderLiveProvider")]
    public sealed class CommanderPhase5BFinalLivePlayModeTests
    {
        public const string OptIn = "OpenEmpires.Phase5BFinalFix.AllowLive";
        private const string RunId = "ca6f2668-e566-4734-8847-242fb3294c5f";
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager manager;
        private OpenRouterCommanderProvider provider;
        private BudgetedSemanticTransport transport;
        private CancellationTokenSource cancellation;
        private string directory;
        private string activeRunId;

        [SetUp] public void SetUp()
        {
#if UNITY_EDITOR
            if (!UnityEditor.SessionState.GetBool(OptIn, false))
                Assert.Ignore("Paid final-fix lane requires explicit one-run Editor opt-in.");
#else
            Assert.Ignore("This bounded paid diagnostic lane is Editor-only.");
#endif
            bool confirmation = TestContext.CurrentContext.Test.Name.StartsWith("CorrectedIdle", StringComparison.Ordinal);
            activeRunId = confirmation ? "f71031e4-4e88-4ef7-985b-98d0f13ea853" : RunId;
            directory = Path.Combine(Path.GetDirectoryName(Application.dataPath),
                "Docs", "CommanderPhase5B", "evidence", confirmation ? "final-resource-confirmation" : "final-intent-live");
            var journal = new EvidenceBudgetJournal(Path.Combine(directory, "paid-usage-ledger.json"), activeRunId, false);
            var innerType = typeof(OpenRouterCommanderProvider).Assembly.GetType("OpenEmpires.CommanderHttpClientTransport");
            transport = new BudgetedSemanticTransport((ICommanderHttpTransport)Activator.CreateInstance(innerType, true), journal);
            provider = new OpenRouterCommanderProvider(transport: transport);
            if (!(bool)typeof(OpenRouterCommanderProvider).GetProperty("HasConfiguration", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(provider)) Assert.Inconclusive("Configured live provider is unavailable; no credentials are exported.");

            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            simulation.SetPlayerCivilizations(new[] { Civilization.English });
            int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
            typeof(MapData).GetField("holeMap", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(simulation.MapData, null);
            foreach (var node in simulation.MapData.GetAllResourceNodes()) node.RemainingAmount = 0;
            for (int a = x - 25; a <= x + 25; a++) for (int b = z - 25; b <= z + 25; b++)
            {
                simulation.MapData.Tiles[a, b] = TileType.Grass;
                simulation.MapData.ForestDensity[a, b] = 0;
                simulation.MapData.FoundationCount[a, b] = 0;
                simulation.FogOfWar.SetVisible(0, a, b);
            }
            simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true).AutoProduceVillagers = false;
            var worker = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 6, z + 7),
                Fixed32.FromFloat(2), Fixed32.FromFloat(.4f), Fixed32.One);
            worker.UnitType = 0; worker.IsVillager = true; worker.MaxHealth = worker.CurrentHealth = 100;
            worker.State = UnitState.Idle;
            simulation.MapData.AddResourceNode(ResourceType.Food, simulation.MapData.TileToWorldFixed(x + 12, z + 9), 10000);
            simulation.MapData.AddResourceNode(ResourceType.Wood, simulation.MapData.TileToWorldFixed(x - 12, z + 9), 10000);
            var resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = 1000;
            manager = new CommanderGoalManager(simulation, 0);
            cancellation = new CancellationTokenSource();
        }

        [TearDown] public void TearDown()
        {
#if UNITY_EDITOR
            UnityEditor.SessionState.SetBool(OptIn, false);
#endif
            cancellation?.Cancel(); cancellation?.Dispose();
            manager?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        [UnityTest] public IEnumerator DiagnoseIdleMill_BeforeContractChange()
        {
            string path = Path.Combine(directory, "idle-mill-before-contract.json");
            Assert.That(File.Exists(path), Is.False, "Preserve prior capture; do not repeat this paid baseline automatically.");
            const string input = "Build a Mill near berry bushes with one idle villager";
            CommanderSemanticResult result = null;
            using (transport.BeginSubmission())
            {
                Task<CommanderSemanticResult> task = provider.TranslateSemanticAsync(
                    new CommanderSemanticProviderRequest(input, new CommanderContextBuilder().Build(simulation, manager, input)),
                    cancellation.Token);
                float start = Time.realtimeSinceStartup;
                while (!task.IsCompleted && Time.realtimeSinceStartup - start < 35) yield return null;
                if (!task.IsCompleted) cancellation.Cancel();
                Assert.That(task.IsCompleted, Is.True, "Owned HTTP operation did not terminate inside the existing caller bound.");
                Assert.That(task.IsFaulted, Is.False, "Live operation faulted; inspect consumed reservations, never raw exceptions.");
                result = task.Result;
            }
            var nodes = result.Nodes.Select(node => new SafeNode {
                type = node.Type.ToString(), building = node.BuildingType?.ToString(),
                count = node.Count ?? 0, anchor = node.PlacementAnchorSelector?.ToString(),
                resource = node.ResourceType?.ToString(), sourceKind = node.SourceKind?.ToString(),
                constraints = node.Constraints.Select(c => c.Type.ToString()).ToArray(),
                dependsOn = node.DependsOn.ToArray() }).ToArray();
            var evidence = new SafeEvidence { run_id = activeRunId, player_input = input,
                method = "Before provider contract change; configured production transport, no admission or native commands",
                source_identity = JsonUtility.FromJson<SourceStamp>(File.ReadAllText(Path.Combine(directory, "../../source-identity.json"))).aggregateSha256,
                http_initial = transport.InitialHttpAttempts, http_repair = transport.RepairHttpAttempts,
                http_status = transport.LastHttpStatus, valid = result.IsValid,
                outcome = result.Outcome.ToString(), trace = provider.LastRequestTrace,
                schema_diagnostic = result.SchemaDiagnostic, nodes = nodes, goal_count = manager.Goals.Count };
            File.WriteAllText(path, JsonUtility.ToJson(evidence, true));
            Assert.That(manager.Goals, Is.Empty, "This is an observation-only provider baseline.");
            if (result.IsValid) Assert.Inconclusive("The original malformed Mill result did not recur in this bounded baseline; preserve the capture and original failure.");
            if (transport.LastHttpStatus != 200) Assert.Inconclusive("Provider access/lifecycle gap, not a reproduced HTTP-200 schema failure.");
            Assert.That(provider.LastRequestTrace, Does.Contain("schema=field="));
        }

        [UnityTest] public IEnumerator IdleLumberYard_FinalProviderAndNativeCompletion()
            => VerifyFinal("Build a Lumber Yard near the woodline with one idle villager", "idle-lumber-final.json", BuildingType.LumberYard, false);

        [UnityTest] public IEnumerator IdleMill_FinalProviderAndNativeCompletion()
            => VerifyFinal("Build a Mill near berry bushes with one idle villager", "idle-mill-final.json", BuildingType.Mill, false);

        [UnityTest] public IEnumerator ThenCompound_FinalProviderAndNativeCompletion()
            => VerifyFinal("Build a House near my Town Center, then assign one idle villager to wood", "then-compound-final.json", BuildingType.House, true);

        [UnityTest] public IEnumerator CorrectedIdleLumber_FinalProviderAndNativeCompletion()
            => VerifyFinal("Build a Lumber Yard near the woodline with one idle villager", "idle-lumber-confirmed.json", BuildingType.LumberYard, false);

        [UnityTest] public IEnumerator CorrectedIdleMill_FinalProviderAndNativeCompletion()
            => VerifyFinal("Build a Mill near berry bushes with one idle villager", "idle-mill-confirmed.json", BuildingType.Mill, false);

        private IEnumerator VerifyFinal(string input, string filename, BuildingType type, bool compound)
        {
            string path = Path.Combine(directory, filename);
            Assert.That(File.Exists(path), Is.False, "Preserve the first attempt; do not repeat paid cases automatically.");
            int x = simulation.MapData.Width / 2, z = simulation.MapData.Height / 2;
            if (!compound) simulation.CreateBuilding(0, type, x - 18, z - 18, false, true);
            if (compound)
            {
                var spare = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 7, z + 8),
                    Fixed32.FromFloat(2), Fixed32.FromFloat(.4f), Fixed32.One);
                spare.UnitType = 0; spare.IsVillager = true; spare.MaxHealth = spare.CurrentHealth = 100;
                spare.State = UnitState.Idle;
            }
            var commands = new List<ICommand>();
            simulation.CommandBuffer.CommandEnqueued += (command, source) =>
            { if (source == CommandEnqueueSource.Commander) commands.Add(command); };
            CommanderSemanticResult result;
            using (transport.BeginSubmission())
            {
                var task = provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest(input,
                    new CommanderContextBuilder().Build(simulation, manager, input)), cancellation.Token);
                float start = Time.realtimeSinceStartup;
                while (!task.IsCompleted && Time.realtimeSinceStartup - start < 35) yield return null;
                if (!task.IsCompleted) cancellation.Cancel();
                Assert.That(task.IsCompleted, Is.True, "Owned operation did not terminate inside the existing caller bound.");
                Assert.That(task.IsFaulted, Is.False, "Provider operation faulted; no raw exception is exported.");
                result = task.Result;
            }
            var evidence = new SafeEvidence { run_id = activeRunId, player_input = input,
                method = "Final production provider response followed by explicit game-side approval and normal native commands/ticks",
                source_identity = JsonUtility.FromJson<SourceStamp>(File.ReadAllText(Path.Combine(directory, "../../source-identity.json"))).aggregateSha256,
                http_initial = transport.InitialHttpAttempts, http_repair = transport.RepairHttpAttempts,
                http_status = transport.LastHttpStatus, valid = result.IsValid, outcome = result.Outcome.ToString(),
                trace = provider.LastRequestTrace, schema_diagnostic = result.SchemaDiagnostic, stage = "semantic",
                nodes = result.Nodes.Select(node => new SafeNode { type = node.Type.ToString(), building = node.BuildingType?.ToString(),
                    count = node.Count ?? 0, anchor = node.PlacementAnchorSelector?.ToString(), resource = node.ResourceType?.ToString(),
                    sourceKind = node.SourceKind?.ToString(), constraints = node.Constraints.Select(c => c.Type.ToString()).ToArray(),
                    idleOnly = node.Constraints.OfType<PreferredWorkersConstraint>().Any(), dependsOn = node.DependsOn.ToArray(),
                    workerState = node.WorkerAllocation?.Workers.State.ToString(), workerCount = node.WorkerAllocation?.Count ?? 0,
                    workerDestination = node.WorkerAllocation?.Destination.Resource.ToString() }).ToArray() };
            File.WriteAllText(path, JsonUtility.ToJson(evidence, true));
            if (!result.IsValid && transport.LastHttpStatus == 0)
                Assert.Inconclusive("No terminal HTTP response; normalized intent and native execution remain unverified.");
            Assert.That(result.IsValid, Is.True, "Final provider result failed the strict declaration/schema boundary.");
            Assert.That(result.Outcome, Is.EqualTo(CommanderSemanticOutcome.Request));
            Assert.That(result.Nodes.Count, Is.EqualTo(compound ? 2 : 1));
            Assert.That(result.Nodes[0].BuildingType, Is.EqualTo(type));
            Assert.That(result.Nodes[0].Count, Is.EqualTo(1));
            if (!compound)
            {
                Assert.That(result.Nodes[0].Constraints.OfType<PreferredWorkersConstraint>().Single().WorkerSource,
                    Is.EqualTo(CommanderPreferredWorkerSource.IdleOnly));
                Assert.That(result.Nodes[0].PlacementAnchorSelector, Is.EqualTo(CommanderSemanticAnchorSelector.VisibleResource));
                Assert.That(result.Nodes[0].SourceKind, Is.EqualTo(type == BuildingType.Mill ? ResourceSourceKind.Berries : ResourceSourceKind.Tree));
            }
            else
            {
                Assert.That(result.Nodes[1].DependsOn, Does.Contain(0));
                Assert.That(result.Nodes[1].WorkerAllocation.Workers.State, Is.EqualTo(CommanderWorkerState.Idle));
                Assert.That(result.Nodes[1].WorkerAllocation.Mode, Is.EqualTo(CommanderWorkerAllocationMode.SelectedCount));
                Assert.That(result.Nodes[1].WorkerAllocation.Count, Is.EqualTo(1));
                Assert.That(result.Nodes[1].WorkerAllocation.Destination.Resource, Is.EqualTo(ResourceType.Wood));
            }
            var scope = manager.PrepareActionPlan(result, input, simulation.CurrentTick);
            Assert.That(manager.Goals, Is.Empty, "A candidate is not approval.");
            Assert.That(commands, Is.Empty, "No commands before explicit game-side approval.");
            var goals = manager.SubmitSemanticGraph(manager.ApproveActionPlan(scope, simulation.CurrentTick));
            evidence.stage = "native"; evidence.goal_count = goals.Count;
            File.WriteAllText(path, JsonUtility.ToJson(evidence, true));
            for (int i = 0; i < 12000 && !goals.All(g => g.IsTerminal); i++)
            {
                if (compound && goals[0].Status != CommanderGoalStatus.Completed)
                    Assert.That(commands.OfType<GatherCommand>(), Is.Empty, "Assignment cannot precede actual House completion.");
                manager.Tick(simulation.CurrentTick); simulation.Tick();
                if (i % 200 == 0) yield return null;
            }
            evidence.stage = "terminal";
            evidence.tick = simulation.CurrentTick;
            evidence.goalStatuses = goals.Select(g => g.Status.ToString()).ToArray();
            evidence.commandTypes = commands.Select(c => c.GetType().Name).Distinct().ToArray();
            evidence.commandCount = commands.Count;
            evidence.cardProgress = CommanderTaskBoardProjection.Capture(manager, null, 0).Cards.Select(c => c.Progress).ToArray();
            File.WriteAllText(path, JsonUtility.ToJson(evidence, true));
            Assert.That(goals.All(g => g.Status == CommanderGoalStatus.Completed), Is.True, string.Join("; ", goals.Select(g => g.StatusReason)));
            Assert.That(commands.OfType<PlaceBuildingCommand>().Count(), Is.EqualTo(1), "One requested native placement, no duplicate goal/placement.");
            if (!compound) Assert.That(evidence.cardProgress.Single(), Is.EqualTo("Completed buildings: 1 / 1"));
        }

        [Serializable] private sealed class SourceStamp { public string aggregateSha256; }
        [Serializable] private sealed class SafeNode
        {
            public string type, building, anchor, resource, sourceKind, workerState, workerDestination;
            public int count, workerCount;
            public bool idleOnly;
            public string[] constraints;
            public int[] dependsOn;
        }
        [Serializable] private sealed class SafeEvidence
        {
            public string run_id, player_input, method, source_identity, outcome, trace, schema_diagnostic, stage;
            public int http_initial, http_repair, http_status, goal_count, tick, commandCount;
            public bool valid;
            public SafeNode[] nodes;
            public string[] goalStatuses, commandTypes, cardProgress;
        }
    }
}
