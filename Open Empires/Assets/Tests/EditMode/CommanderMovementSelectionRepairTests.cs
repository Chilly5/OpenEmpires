using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public sealed class CommanderMovementSelectionRepairTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public async Task OfflineHttpEnvelope_ArcherSavedOrSpearmanTraceShaped_ReachesExactUnits(bool syntheticSpearman)
        {
            var instance = typeof(CommanderChatUI).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            var previous = instance.GetValue(null);
            instance.SetValue(null, null);
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            CommanderChatUI chat = null;
            CommanderGoalManager manager = null;
            CommanderIntentDispatcher dispatcher = null;
            try
            {
                var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                sim.SetPlayerCivilizations(new[] { Civilization.English });
                manager = new CommanderGoalManager(sim, 0);
                dispatcher = new CommanderIntentDispatcher(sim, manager);
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs",
                    "CommanderPhase5C", "evidence", "small-movement-ui-voice", "live-archers-corrected", "response.json"));
                string body = File.ReadAllText(path);
                if (syntheticSpearman)
                {
                    const string semantic = "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"MoveUnits\",\"unitSelector\":\"Spearman\",\"count\":1,\"location\":\"PlayerMarker\"}]}";
                    Assert.That(semantic.Length, Is.EqualTo(114),
                        "This is a trace-length-matched synthetic fixture, not archived provider bytes.");
                    body = new Newtonsoft.Json.Linq.JObject
                    {
                        ["choices"] = new Newtonsoft.Json.Linq.JArray(new Newtonsoft.Json.Linq.JObject
                        {
                            ["finish_reason"] = "stop",
                            ["message"] = new Newtonsoft.Json.Linq.JObject { ["content"] = semantic }
                        })
                    }.ToString(Newtonsoft.Json.Formatting.None);
                }
                var transport = new SavedMovementEnvelopeTransport(body);
                var provider = new OpenRouterCommanderProvider("offline-fixture-only", transport);
                chat = new GameObject("SavedArcherEnvelopeFixture").AddComponent<CommanderChatUI>();
                chat.Initialize(provider, sim, manager, dispatcher);
                instance.SetValue(null, chat);
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                for (int ix = x - 20; ix <= x + 20; ix++)
                    for (int iz = z - 20; iz <= z + 20; iz++) sim.FogOfWar.SetVisible(0, ix, iz);
                var archers = new List<int>();
                for (int i = 0; i < 2; i++)
                {
                    var archer = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x + 8 + i, z),
                        Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                    archer.UnitType = sim.ResolveCivUnitType(0, 2);
                    archer.CurrentHealth = archer.MaxHealth = 100;
                    archers.Add(archer.Id);
                }
                var spearman = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x + 10, z),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                spearman.UnitType = sim.ResolveCivUnitType(0, 1);
                spearman.CurrentHealth = spearman.MaxHealth = 100;
                var issued = new List<MoveCommand>();
                sim.CommandBuffer.CommandEnqueued += (command, origin) =>
                { if (origin == CommandEnqueueSource.Commander && command is MoveCommand move) issued.Add(move); };
                Assert.That(chat.TryPlaceDestinationMarker(x, z), Is.True);
                typeof(CommanderChatUI).GetField("markerInputFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(chat, -1);
                await chat.SubmitMessageAsync(syntheticSpearman
                    ? "move spearman to marker" : "move my archers to marker");
                Assert.That(transport.Calls, Is.EqualTo(1));
                Assert.That(chat.PendingActionPlan, Is.Not.Null, chat.DisplayedTranscript);
                Assert.That(manager.Goals, Is.Empty);
                await chat.SubmitMessageAsync("approve plan");
                Assert.That(manager.Goals, Has.Count.EqualTo(1), chat.DisplayedTranscript);
                var goal = (CommanderCapabilityGoal)manager.Goals[0];
                var expected = syntheticSpearman ? new[] { spearman.Id } : archers.ToArray();
                Assert.That(goal.DynamicMoveUnitIds, Is.EquivalentTo(expected));
                if (!syntheticSpearman) Assert.That(goal.DynamicMoveUnitIds, Has.None.EqualTo(spearman.Id));
                for (int step = 0; step < 2000 && goal.Status != CommanderGoalStatus.Completed; step++)
                { manager.Tick(sim.CurrentTick); sim.Tick(); }
                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed), goal.StatusReason);
                Assert.That(issued, Has.Count.EqualTo(1));
                Assert.That(issued[0].UnitIds, Is.EquivalentTo(expected));
            }
            finally
            {
                if (chat != null) UnityEngine.Object.DestroyImmediate(chat.gameObject);
                dispatcher?.Dispose(); manager?.Dispose();
                UnityEngine.Object.DestroyImmediate(config); instance.SetValue(null, previous);
            }
        }

        private sealed class SavedMovementEnvelopeTransport : ICommanderHttpTransport
        {
            private readonly string body;
            internal int Calls;
            internal SavedMovementEnvelopeTransport(string body) { this.body = body; }
            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string json,
                IReadOnlyDictionary<string, string> headers, CancellationToken token)
            { Calls++; return Task.FromResult(new CommanderHttpResponse(200, body)); }
        }

        [Test]
        public async Task ResetPendingExistingMarkerMove_RejectsOldApprovalAndLateYes()
        {
            var instance = typeof(CommanderChatUI).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            var previous = instance.GetValue(null);
            instance.SetValue(null, null);
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            CommanderChatUI chat = null;
            CommanderGoalManager manager = null;
            CommanderIntentDispatcher dispatcher = null;
            try
            {
                var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                manager = new CommanderGoalManager(sim, 0);
                dispatcher = new CommanderIntentDispatcher(sim, manager);
                var provider = new NoMovementProvider { Json =
                    "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"MoveUnits\",\"unitSelector\":\"Spearman\",\"count\":1,\"location\":\"PlayerMarker\"}]}" };
                chat = new GameObject("ResetMarkerMovementFixture").AddComponent<CommanderChatUI>();
                chat.Initialize(provider, sim, manager, dispatcher);
                instance.SetValue(null, chat);
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                for (int ix = x - 10; ix <= x + 10; ix++)
                    for (int iz = z - 10; iz <= z + 10; iz++) sim.FogOfWar.SetVisible(0, ix, iz);
                var unit = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x + 5, z),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                unit.UnitType = 1; unit.CurrentHealth = unit.MaxHealth = 100;
                Assert.That(chat.TryPlaceDestinationMarker(x, z), Is.True);
                typeof(CommanderChatUI).GetField("markerInputFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(chat, -1);
                await chat.SubmitMessageAsync("move one spearman to marker");
                var pending = chat.PendingActionPlan;
                Assert.That(pending, Is.Not.Null, chat.DisplayedTranscript);
                Assert.That(manager.Goals, Is.Empty);
                chat.ResetConversation();
                Assert.That(chat.PendingActionPlan, Is.Null);
                Assert.That(manager.ApproveActionPlan(pending, pending.Generation), Is.Null);
                provider.Json = "{\"outcome\":\"Answer\",\"message\":\"No active plan.\"}";
                await chat.SubmitMessageAsync("yes");
                Assert.That(manager.Goals, Is.Empty);
                Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
            }
            finally
            {
                if (chat != null) UnityEngine.Object.DestroyImmediate(chat.gameObject);
                dispatcher?.Dispose(); manager?.Dispose();
                UnityEngine.Object.DestroyImmediate(config); instance.SetValue(null, previous);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ForeignOnlyActorsAndHiddenMarker_CannotAdmitExistingMove(bool dynamicPlan)
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var sim = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
                using var manager = new CommanderGoalManager(sim, 0);
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                for (int ix = x - 10; ix <= x + 10; ix++)
                    for (int iz = z - 10; iz <= z + 10; iz++) sim.FogOfWar.SetVisible(0, ix, iz);
                var foreign = sim.UnitRegistry.CreateUnit(1, sim.MapData.TileToWorldFixed(x + 5, z),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                foreign.UnitType = 1; foreign.CurrentHealth = foreign.MaxHealth = 100;
                string json = dynamicPlan ? Plan("\"unit\":\"unit:1\",\"count\":1")
                    : "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"MoveUnits\",\"unitSelector\":\"Spearman\",\"count\":1,\"location\":\"PlayerMarker\"}]}";
                var parsed = CommanderSemanticJson.Parse(json);
                Assert.That(manager.TrySetPlayerMarkerSnapshot(x, z), Is.True);
                var approved = manager.ApproveActionPlan(manager.PrepareActionPlan(parsed,
                    "move one spearman to marker", 0), 0);
                Assert.That(() => manager.SubmitSemanticGraph(approved),
                    Throws.InstanceOf<ArgumentException>(), "No owned matching actor may be frozen or admitted.");
                Assert.That(manager.Goals, Is.Empty);
                Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
                Assert.That(manager.TrySetPlayerMarkerSnapshot(x + 20, z), Is.False,
                    "A hidden destination cannot replace the valid marker.");
                Assert.That(manager.TryGetPlayerMarkerSnapshot(out int retainedX, out int retainedZ), Is.True);
                Assert.That((retainedX, retainedZ), Is.EqualTo((x, z)));
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task EquivalentMarkerMoves_WaitForApprovalThenAdmitExactActorOnce(bool dynamicPlan)
        {
            var instance = typeof(CommanderChatUI).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            var previous = instance.GetValue(null);
            instance.SetValue(null, null);
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            CommanderChatUI chat = null;
            CommanderGoalManager manager = null;
            CommanderIntentDispatcher dispatcher = null;
            try
            {
                var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                manager = new CommanderGoalManager(sim, 0);
                dispatcher = new CommanderIntentDispatcher(sim, manager);
                var provider = new NoMovementProvider { Json = dynamicPlan
                    ? Plan("\"unit\":\"unit:1\",\"count\":1")
                    : "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"MoveUnits\",\"unitSelector\":\"Spearman\",\"count\":1,\"location\":\"PlayerMarker\"}]}" };
                chat = new GameObject("MovementParityFixture").AddComponent<CommanderChatUI>();
                chat.Initialize(provider, sim, manager, dispatcher);
                instance.SetValue(null, chat);
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                for (int ix = x - 20; ix <= x + 20; ix++)
                    for (int iz = z - 20; iz <= z + 20; iz++) sim.FogOfWar.SetVisible(0, ix, iz);
                var intended = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x + 8, z),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                intended.UnitType = 1; intended.CurrentHealth = intended.MaxHealth = 100;
                var unrelated = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x + 9, z),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                unrelated.UnitType = 2; unrelated.CurrentHealth = unrelated.MaxHealth = 100;
                var issuedMoves = new List<MoveCommand>();
                sim.CommandBuffer.CommandEnqueued += (command, origin) =>
                { if (origin == CommandEnqueueSource.Commander && command is MoveCommand move) issuedMoves.Add(move); };
                Assert.That(chat.TryPlaceDestinationMarker(x, z), Is.True);
                typeof(CommanderChatUI).GetField("markerInputFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(chat, -1);
                await chat.SubmitMessageAsync("move one spearman to marker");
                Assert.That(chat.PendingActionPlan, Is.Not.Null, chat.DisplayedTranscript);
                Assert.That(manager.Goals, Is.Empty, "Translation is not approval.");
                Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
                Assert.That(chat.TryPlaceDestinationMarker(x + 1, z), Is.True);
                Assert.That(chat.PendingActionPlan, Is.Not.Null, chat.DisplayedTranscript);
                Assert.That(chat.PendingActionPlan.MarkerTileX, Is.EqualTo(x + 1));
                typeof(CommanderChatUI).GetField("markerInputFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(chat, -1);
                await chat.SubmitMessageAsync("yes");
                Assert.That(manager.Goals, Has.Count.EqualTo(1), chat.DisplayedTranscript);
                var move = (CommanderCapabilityGoal)manager.Goals[0];
                Assert.That(move.DynamicMoveUnitIds, Is.EqualTo(new[] { intended.Id }));
                Assert.That(move.DynamicMoveUnitIds, Has.None.EqualTo(unrelated.Id));
                await chat.SubmitMessageAsync("approve plan");
                Assert.That(manager.Goals, Has.Count.EqualTo(1), "An approval cannot admit twice.");
                Assert.That(provider.Calls, Is.EqualTo(1));
                for (int step = 0; step < 2000 && move.Status != CommanderGoalStatus.Completed; step++)
                { manager.Tick(sim.CurrentTick); sim.Tick(); }
                Assert.That(issuedMoves, Has.Count.EqualTo(1));
                Assert.That(issuedMoves[0].UnitIds, Is.EqualTo(new[] { intended.Id }));
                Assert.That(move.Status, Is.EqualTo(CommanderGoalStatus.Completed), move.StatusReason);
                Assert.That(sim.MapData.WorldToTile(intended.SimPosition),
                    Is.EqualTo(sim.MapData.WorldToTile(issuedMoves[0].TargetPosition)),
                    "The exact frozen actor must reach the native destination tile.");
            }
            finally
            {
                if (chat != null) UnityEngine.Object.DestroyImmediate(chat.gameObject);
                dispatcher?.Dispose(); manager?.Dispose();
                UnityEngine.Object.DestroyImmediate(config); instance.SetValue(null, previous);
            }
        }

        [Test]
        public void NormalizedMovement_CountsEffectsAndKeepsDistinctSelectorsAndDestinations()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                using var manager = new CommanderGoalManager(sim, 0);
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                for (int ix = x - 12; ix <= x + 12; ix++)
                    for (int iz = z - 12; iz <= z + 12; iz++) sim.FogOfWar.SetVisible(0, ix, iz);
                for (int i = 0; i < 3; i++)
                {
                    var unit = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x + i + 2, z),
                        Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                    unit.UnitType = i == 2 ? 2 : 1; unit.CurrentHealth = unit.MaxHealth = 100;
                }
                Assert.That(manager.TrySetPlayerMarkerSnapshot(x, z), Is.True);
                var request = manager.PrepareActionPlan(CommanderSemanticJson.Parse(
                    "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"MoveUnits\",\"unitSelector\":\"Spearman\",\"count\":1,\"location\":\"PlayerMarker\"}]}"),
                    "move one spearman to marker", 0);
                var dynamicCandidate = manager.PrepareActionPlan(CommanderSemanticJson.Parse(
                    Plan("\"unit\":\"unit:1\",\"count\":1")), "move one spearman to marker", 0);
                Assert.That(request.MovementOrder, Is.Not.Null);
                Assert.That(dynamicCandidate.MovementOrder, Is.Not.Null);
                Assert.That(request.MovementOrder.GameplayEffectCount, Is.EqualTo(1));
                Assert.That(dynamicCandidate.MovementOrder.GameplayEffectCount, Is.EqualTo(1),
                    "Selection and location are not gameplay effects.");
                Assert.That(request.MovementOrder.SameGameplayEffects(dynamicCandidate.MovementOrder), Is.True);
                var more = manager.PrepareActionPlan(CommanderSemanticJson.Parse(
                    Plan("\"unit\":\"unit:1\",\"count\":2")), "move two spearmen to marker", 0);
                Assert.That(request.MovementOrder.SameGameplayEffects(more.MovementOrder), Is.False);
                var allMatching = manager.PrepareActionPlan(CommanderSemanticJson.Parse(
                    Plan("\"unit\":\"unit:1\",\"countMode\":\"AllMatching\"")),
                    "move all spearmen to marker", 0);
                Assert.That(request.MovementOrder.SameGameplayEffects(allMatching.MovementOrder), Is.False,
                    "An exact count and AllMatching are different order revisions.");
                var excluded = manager.PrepareActionPlan(CommanderSemanticJson.Parse(
                    Plan("\"kind\":\"AllUnits\",\"excludeKind\":\"Villagers\",\"count\":1")),
                    "move one unit except villagers", 0);
                Assert.That(request.MovementOrder.SameGameplayEffects(excluded.MovementOrder), Is.False);
                string constrainedJson = Plan("\"unit\":\"unit:1\",\"count\":1")
                    .Replace("\"version\":1,", "\"version\":1,\"constraints\":[{\"type\":\"NoConstruction\"}],");
                CommanderActionPlanCandidate PrepareChecked(string json, string label)
                {
                    var parsed = CommanderSemanticJson.Parse(json);
                    Assert.That(parsed.IsValid, Is.True,
                        label + " must pass strict parsing: " + parsed.SchemaDiagnostic);
                    try { return manager.PrepareActionPlan(parsed, label, 0); }
                    catch (ArgumentException error)
                    { Assert.Fail(label + " rejected at compilation: " + error.Message); return null; }
                }
                var constrained = PrepareChecked(constrainedJson, "constrained movement");
                Assert.That(request.MovementOrder.SameGameplayEffects(constrained.MovementOrder), Is.False,
                    "Typed restrictions cannot be dropped when comparing order revisions.");
                const string firstMove = "{\"type\":\"MoveUnits\",\"unitSelector\":\"Spearman\",\"count\":1,\"location\":\"PlayerMarker\"}";
                const string dependentMove = "{\"type\":\"MoveUnits\",\"unitSelector\":\"Spearman\",\"count\":1,\"location\":\"PlayerMarker\",\"dependsOn\":[0]}";
                var independent = PrepareChecked(
                    "{\"outcome\":\"Request\",\"nodes\":[" + firstMove + "," + firstMove + "]}",
                    "independent moves");
                var dependent = PrepareChecked(
                    "{\"outcome\":\"Request\",\"nodes\":[" + firstMove + "," + dependentMove + "]}",
                    "dependent moves");
                Assert.That(independent.MovementOrder.GameplayEffectCount, Is.EqualTo(2));
                Assert.That(independent.MovementOrder.SameGameplayEffects(dependent.MovementOrder), Is.False,
                    "Dependency edges are part of the immutable order revision.");
                Assert.That(manager.TrySetPlayerMarkerSnapshot(x + 1, z), Is.True);
                var otherDestination = manager.PrepareActionPlan(CommanderSemanticJson.Parse(
                    Plan("\"unit\":\"unit:1\",\"count\":1")), "move one spearman there", 0);
                Assert.That(request.MovementOrder.SameGameplayEffects(otherDestination.MovementOrder), Is.False);
                Assert.That(manager.ApproveActionPlan(request, 0), Is.Null,
                    "Approval of a stale destination must fail.");
                Assert.That(manager.Goals, Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void ExistingMarkerMove_CannotEnterAutomaticKnownIntentAdmission()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                using var manager = new CommanderGoalManager(sim, 0);
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                sim.FogOfWar.SetVisible(0, x, z);
                var unit = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x, z),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                unit.UnitType = 1; unit.CurrentHealth = unit.MaxHealth = 100;
                Assert.That(manager.TrySetPlayerMarkerSnapshot(x, z), Is.True);
                var result = CommanderSemanticJson.Parse(
                    "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"MoveUnits\",\"unitSelector\":\"Spearman\",\"count\":1,\"location\":\"PlayerMarker\"}]}");
                var ticket = manager.BeginSemanticRequest("move one spearman to marker", 0, () => true);
                Assert.Throws<ArgumentException>(() => manager.PrepareKnownIntentScope(result, ticket, null, false));
                Assert.That(manager.Goals, Is.Empty);
                Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void MovementSelectorClarification_UsesStrictSharedSlotAndRetainsOrder()
        {
            var pending = new Newtonsoft.Json.Linq.JObject
            {
                ["outcome"] = "Clarify", ["message"] = "Which units should move?",
                ["pending"] = new Newtonsoft.Json.Linq.JObject
                {
                    ["executionOrder"] = "Independent",
                    ["nodes"] = new Newtonsoft.Json.Linq.JArray(new Newtonsoft.Json.Linq.JObject
                    {
                        ["type"] = "MoveUnits", ["count"] = 1, ["location"] = "PlayerMarker"
                    })
                },
                ["missingSlots"] = new Newtonsoft.Json.Linq.JArray(new Newtonsoft.Json.Linq.JObject
                {
                    ["node"] = 0, ["path"] = CommanderMovementContract.RequestSelectionSlotPath,
                    ["type"] = "CanonicalName", ["alternatives"] = new Newtonsoft.Json.Linq.JArray("Spearman")
                })
            };
            var clarify = CommanderSemanticJson.ParseProviderResponse(pending.ToString());
            Assert.That(clarify.IsValid, Is.True);
            Assert.That(clarify.PendingGraphDraft, Is.Not.Null);
            var completed = clarify.PendingGraphDraft.Resolve("Spearman");
            Assert.That(completed.IsValid, Is.True);
            Assert.That(completed.Outcome, Is.EqualTo(CommanderSemanticOutcome.Request));
            Assert.That(completed.Nodes[0].Count, Is.EqualTo(1));
            Assert.That(completed.Nodes[0].LocationSelector, Is.EqualTo(CommanderSemanticLocationSelector.PlayerMarker));
            Assert.That(completed.Nodes[0].UnitSelector, Is.EqualTo(CommanderSemanticUnitSelector.Spearman));
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public async Task MarkerApprovalRejection_ShowsExpectedReasonAndRetainsMarkerWithoutGoal(bool humanProtected, bool ordinaryRequest)
        {
            var instance = typeof(CommanderChatUI).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            var previous = instance.GetValue(null);
            instance.SetValue(null, null);
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            CommanderChatUI chat = null;
            CommanderGoalManager manager = null;
            CommanderIntentDispatcher dispatcher = null;
            try
            {
                var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                manager = new CommanderGoalManager(sim, 0);
                dispatcher = new CommanderIntentDispatcher(sim, manager);
                var provider = new NoMovementProvider();
                chat = new GameObject("MovementRejectionFixture").AddComponent<CommanderChatUI>();
                chat.Initialize(provider, sim, manager, dispatcher);
                instance.SetValue(null, chat);
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                sim.FogOfWar.SetVisible(0, x, z);
                sim.FogOfWar.SetVisible(0, x + 20, z);
                var unit = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x + 20, z),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                unit.UnitType = 1; unit.CurrentHealth = unit.MaxHealth = 100;
                bool delayedMarker = ordinaryRequest && humanProtected;
                if (!delayedMarker) Assert.That(chat.TryPlaceDestinationMarker(x, z), Is.True);
                typeof(CommanderChatUI).GetField("markerInputFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(chat, -1);
                if (!ordinaryRequest)
                {
                    await chat.SubmitMessageAsync("move to marker");
                    await chat.SubmitMessageAsync("1 spearman");
                    Assert.That(chat.PendingActionPlan, Is.Not.Null, chat.DisplayedTranscript);
                }
                if (humanProtected)
                {
                    sim.CommandBuffer.EnqueueCommand(new MoveCommand(0, new[] { unit.Id }, unit.SimPosition));
                    sim.CommandBuffer.FlushCommands(); // Existing human order, not a Commander command.
                }
                if (ordinaryRequest)
                {
                    provider.Json = "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"MoveUnits\",\"unitSelector\":\"Spearman\",\"count\":1,\"location\":\"PlayerMarker\"}]}";
                    await chat.SubmitMessageAsync("move one spearman to marker");
                    if (delayedMarker)
                    {
                        Assert.That(manager.Goals, Is.Empty);
                        Assert.That(chat.TryPlaceDestinationMarker(x, z), Is.True);
                        typeof(CommanderChatUI).GetField("markerInputFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(chat, -1);
                    }
                    Assert.That(chat.PendingActionPlan, Is.Not.Null, chat.DisplayedTranscript);
                }
                await chat.SubmitMessageAsync("yes");
                Assert.That(chat.DisplayedTranscript, Does.Contain(humanProtected
                    ? "protected by a recent player order" : "No complete known reachable"));
                Assert.That(chat.DisplayedTranscript, Does.Contain("No goal was created"));
                Assert.That(chat.DisplayedTranscript, Does.Not.Contain("Check its current goal status"));
                Assert.That(manager.Goals, Is.Empty);
                Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
                Assert.That(manager.TryGetPlayerMarkerSnapshot(out _, out _), Is.True);
                Assert.That(provider.Calls, Is.EqualTo(ordinaryRequest ? 1 : 0));
            }
            finally
            {
                if (chat != null) UnityEngine.Object.DestroyImmediate(chat.gameObject);
                dispatcher?.Dispose(); manager?.Dispose();
                UnityEngine.Object.DestroyImmediate(config); instance.SetValue(null, previous);
            }
        }

        [Test]
        public async Task MissingGroup_UIRetainsMovementAndMarkerUntilTypedReplyAndApproval()
        {
            var instance = typeof(CommanderChatUI).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            var previous = instance.GetValue(null);
            instance.SetValue(null, null);
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            CommanderChatUI chat = null;
            CommanderGoalManager manager = null;
            CommanderIntentDispatcher dispatcher = null;
            try
            {
                var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                manager = new CommanderGoalManager(sim, 0);
                dispatcher = new CommanderIntentDispatcher(sim, manager);
                var provider = new NoMovementProvider();
                chat = new GameObject("MovementGroupFixture").AddComponent<CommanderChatUI>();
                chat.Initialize(provider, sim, manager, dispatcher);
                instance.SetValue(null, chat);
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                for (int ix = x - 20; ix <= x + 20; ix++)
                    for (int iz = z - 20; iz <= z + 20; iz++) sim.FogOfWar.SetVisible(0, ix, iz);
                foreach (int type in new[] { 0, 1, 4 })
                {
                    var unit = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x + type + 2, z + 2),
                        Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                    unit.UnitType = type; unit.IsVillager = type == 0; unit.CurrentHealth = unit.MaxHealth = 100;
                }
                Assert.That(chat.TryPlaceDestinationMarker(x, z), Is.True);
                // EditMode does not advance a frame between these awaited calls.
                // Model the next input frame, preserving production click ownership.
                typeof(CommanderChatUI).GetField("markerInputFrame", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(chat, -1);
                await chat.SubmitMessageAsync("move to marker");
                Assert.That(chat.DisplayedTranscript, Does.Contain("Which units"));
                Assert.That(manager.Goals, Is.Empty);
                Assert.That(manager.TryGetPlayerMarkerSnapshot(out _, out _), Is.True);
                await chat.SubmitMessageAsync("cancel");
                Assert.That(manager.Goals, Is.Empty);
                Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
                Assert.That(manager.TryGetPlayerMarkerSnapshot(out _, out _), Is.True,
                    "Cancelling this pending group question must not consume the saved marker.");
                await chat.SubmitMessageAsync("move to marker");
                await chat.SubmitMessageAsync("all except villagers");
                Assert.That(chat.PendingActionPlan, Is.Not.Null, chat.DisplayedTranscript);
                Assert.That(chat.PendingActionPlan.Preview, Does.Contain("except villagers"));
                Assert.That(chat.PendingActionPlan.Preview, Does.Contain("your placed destination marker"));
                Assert.That(chat.PendingActionPlan.Preview, Does.Not.Contain("resource-side"));
                Assert.That(provider.Calls, Is.Zero);
                Assert.That(manager.Goals, Is.Empty, "A group reply is not approval.");
                await chat.SubmitMessageAsync("yes");
                Assert.That(manager.Goals, Has.Count.EqualTo(1), chat.DisplayedTranscript);
                var goal = (CommanderCapabilityGoal)manager.Goals[0];
                Assert.That(goal.Action.ActionType, Is.EqualTo(CommanderCapabilityActionType.MoveUnits));
                Assert.That(goal.DynamicMoveUnitIds, Has.Count.EqualTo(2));
                foreach (int id in goal.DynamicMoveUnitIds)
                    Assert.That(sim.UnitRegistry.GetUnit(id).IsVillager, Is.False);
            }
            finally
            {
                if (chat != null) UnityEngine.Object.DestroyImmediate(chat.gameObject);
                dispatcher?.Dispose(); manager?.Dispose();
                UnityEngine.Object.DestroyImmediate(config); instance.SetValue(null, previous);
            }
        }

        private sealed class NoMovementProvider : ICommanderAIProvider, ICommanderSemanticProvider
        {
            internal int Calls;
            internal string Json = "{\"outcome\":\"Answer\",\"message\":\"Fixture\"}";
            public Task<CommanderSemanticResult> TranslateSemanticAsync(CommanderSemanticProviderRequest request, CancellationToken token)
            { Calls++; return Task.FromResult(CommanderSemanticJson.Parse(Json)); }
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request, CancellationToken token)
            { Calls++; return Task.FromResult(CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure, "Fixture")); }
        }

        internal static string Plan(string selection) =>
            "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[" +
            "{\"id\":\"units\",\"mechanic\":\"select-units\",\"parameters\":{" + selection + "},\"inputs\":{},\"dependsOn\":[]}," +
            "{\"id\":\"place\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"PlayerMarker\",\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{},\"dependsOn\":[]}," +
            "{\"id\":\"move\",\"mechanic\":\"move-units\",\"parameters\":{},\"inputs\":{\"units\":\"units\",\"location\":\"place\"},\"dependsOn\":[\"units\",\"place\"]}]}";

        [TestCase("\"kind\":\"AllUnits\",\"excludeKind\":\"Villagers\",\"count\":1")]
        [TestCase("\"kind\":\"Military\",\"excludeUnit\":\"unit:2\",\"count\":1")]
        public void TypedExclusion_SurvivesCompilation(string selection)
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                using var manager = new CommanderGoalManager(sim, 0);
                var parsed = CommanderSemanticJson.Parse(Plan(selection));
                Assert.That(parsed.IsValid, Is.True);
                Assert.That(CommanderDynamicCompiler.TryCompile(parsed.DynamicPlan, sim, manager,
                    out var graph, out var reason), Is.True, reason);
                var selector = ((CapabilityActionIntent)graph.Nodes[0].Intent).UnitSelector;
                var executor = new CommanderCapabilityExecutor(sim);
                var villager = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(12, 12),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                villager.UnitType = selection.Contains("excludeKind") ? 0 : sim.ResolveCivUnitType(0, 2);
                villager.IsVillager = villager.UnitType == 0;
                Assert.That(executor.MatchesSelector(villager, selector), Is.False);
                villager.UnitType = selection.Contains("excludeKind") ? 4 : 1;
                villager.IsVillager = false;
                Assert.That(executor.MatchesSelector(villager, selector), Is.True,
                    "All except villagers includes scouts; Military must retain nonexcluded spearmen.");
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void ArcherMarkerPrompt_ProvidesExecutableAllMatchingExampleForCivReplacement()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                sim.SetPlayerCivilizations(new[] { Civilization.English });
                using var manager = new CommanderGoalManager(sim, 0);
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                sim.FogOfWar.SetVisible(0, x, z);
                for (int i = 0; i < 2; i++)
                {
                    var unit = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x, z),
                        Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                    unit.UnitType = sim.ResolveCivUnitType(0, 2); unit.CurrentHealth = unit.MaxHealth = 100;
                }
                const string utterance = "move my archers to marker";
                var request = new CommanderSemanticProviderRequest(utterance,
                    new CommanderContextBuilder().Build(sim, manager, utterance));
                Assert.That(CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
                    out var pack, out string reason), Is.True, reason);
                string instruction = (string)Newtonsoft.Json.Linq.JObject.Parse(pack.Json)["messages"][0]["content"];
                const string prefix = "Existing marker movement example: ";
                int start = instruction.IndexOf(prefix, StringComparison.Ordinal);
                Assert.That(start, Is.GreaterThanOrEqualTo(0));
                start += prefix.Length;
                int end = instruction.IndexOf(" END marker example.", start, StringComparison.Ordinal);
                var example = CommanderSemanticJson.ParseProviderResponse(instruction.Substring(start, end - start));
                Assert.That(example.IsValid, Is.True);
                Assert.That(CommanderDynamicCompiler.TryCompile(example.DynamicPlan, sim, manager,
                    out var graph, out reason), Is.True, reason);
                var action = (CapabilityActionIntent)graph.Nodes[0].Intent;
                Assert.That(action.UnitSelector.UnitType, Is.EqualTo(2));
                Assert.That(action.UnitSelector.Count, Is.EqualTo(2), "AllMatching must include actual civilization replacements.");
                Assert.That(instruction, Does.Contain("not a missing Count slot"));
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnreachableMarker_RejectedBeforeAdmissionAndRetainsMarker(bool dynamicPlan)
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                using var manager = new CommanderGoalManager(sim, 0);
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                // Only two isolated known tiles: no complete known route or legal
                // scatter envelope may be inferred through the intervening fog.
                sim.FogOfWar.SetVisible(0, x, z);
                sim.FogOfWar.SetVisible(0, x + 20, z);
                var unit = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x + 20, z),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                unit.UnitType = 1; unit.CurrentHealth = unit.MaxHealth = 100;
                Assert.That(manager.TrySetPlayerMarkerSnapshot(x, z), Is.True);
                var parsed = CommanderSemanticJson.Parse(dynamicPlan
                    ? Plan("\"unit\":\"unit:1\",\"count\":1")
                    : "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"MoveUnits\",\"unitSelector\":\"Spearman\",\"count\":1,\"location\":\"PlayerMarker\"}]}");
                var approved = manager.ApproveActionPlan(manager.PrepareActionPlan(parsed,
                    "move one spearman to marker", 0), 0);
                Assert.Throws<CommanderMovementAdmissionException>(() => manager.SubmitSemanticGraph(approved));
                Assert.That(manager.Goals, Is.Empty);
                Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty);
                Assert.That(manager.TryGetPlayerMarkerSnapshot(out int retainedX, out int retainedZ), Is.True);
                Assert.That(retainedX, Is.EqualTo(x)); Assert.That(retainedZ, Is.EqualTo(z));
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MarkerSelection_IsFrozenAndMissingActorCannotBeReplaced(bool dynamicPlan)
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                using var manager = new CommanderGoalManager(sim, 0);
                int x = sim.MapData.Width / 2, z = sim.MapData.Height / 2;
                for (int ix = x - 15; ix <= x + 15; ix++)
                    for (int iz = z - 15; iz <= z + 15; iz++) sim.FogOfWar.SetVisible(0, ix, iz);
                var first = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(x, z),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                first.UnitType = 1; first.CurrentHealth = first.MaxHealth = 100;
                var replacement = sim.UnitRegistry.CreateUnit(0, first.SimPosition,
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                replacement.UnitType = 1; replacement.CurrentHealth = replacement.MaxHealth = 100;
                Assert.That(manager.TrySetPlayerMarkerSnapshot(x, z), Is.True);
                var interpretation = CommanderSemanticJson.Parse(dynamicPlan
                    ? Plan("\"unit\":\"unit:1\",\"count\":1")
                    : "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"MoveUnits\",\"unitSelector\":\"Spearman\",\"count\":1,\"location\":\"PlayerMarker\"}]}");
                var approved = manager.ApproveActionPlan(manager.PrepareActionPlan(interpretation,
                    "move one spearman to marker", 0), 0);
                var goals = manager.SubmitSemanticGraph(approved);
                var move = (CommanderCapabilityGoal)goals[0];
                Assert.That(move.DynamicMoveUnitIds, Is.EqualTo(new[] { first.Id }));
                first.CurrentHealth = 0; first.State = UnitState.Dead;
                manager.Tick(0);
                Assert.That(sim.CommandBuffer.FlushCommands(), Is.Empty,
                    "The other living spearman cannot replace the frozen selected actor.");
                Assert.That(move.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void GroupReply_PreservesAllExceptVillagersAndCanonicalPlural()
        {
            var catalog = GameKnowledgeCatalog.BuildCanonicalIdentityCatalog();
            Assert.That(CommanderChatUI.TryMovementGroupParameters("all except villagers", catalog,
                Civilization.English, out var all), Is.True);
            Assert.That((string)all["kind"], Is.EqualTo("AllUnits"));
            Assert.That((string)all["excludeKind"], Is.EqualTo("Villagers"));
            Assert.That((string)all["countMode"], Is.EqualTo("AllMatching"));
            Assert.That(CommanderChatUI.TryMovementGroupParameters("3 spearmen", catalog,
                Civilization.English, out var three), Is.True);
            Assert.That((string)three["unit"], Is.EqualTo("unit:1"));
            Assert.That((int)three["count"], Is.EqualTo(3));
            Assert.That(three["countMode"], Is.Null);
            Assert.That(CommanderChatUI.TryMovementGroupParameters("999 spearmen", catalog,
                Civilization.English, out _), Is.False);
            Assert.That(CommanderChatUI.TryMovementGroupParameters("archers and train knights", catalog,
                Civilization.English, out _), Is.False);
        }

        [Test]
        public void Exclusion_DtoRoundTripDoesNotWidenScope()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                using var manager = new CommanderGoalManager(sim, 0);
                var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.MoveUnits,
                    new CommanderUnitSelector(CommanderUnitSelectorKind.AllUnits, 3,
                        excludedKind: CommanderUnitSelectorKind.Villagers),
                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerMarker));
                var roundtrip = CommanderIntentDtoCodec.ValidateAndConvert(CommanderIntentDtoCodec.FromIntent(intent),
                    new CommanderContextBuilder().Build(sim, manager));
                Assert.That(roundtrip.Success, Is.True, roundtrip.Reason);
                Assert.That(CommanderScopeEquivalence.SameIntent(intent, roundtrip.Intent), Is.True);
                var widened = new CapabilityActionIntent(0, CommanderCapabilityActionType.MoveUnits,
                    new CommanderUnitSelector(CommanderUnitSelectorKind.AllUnits, 3),
                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerMarker));
                Assert.That(CommanderScopeEquivalence.SameIntent(intent, widened), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void EveryRegisteredCanonicalUnit_CanBeSelectedWithoutProductionCapability()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                using var manager = new CommanderGoalManager(sim, 0);
                foreach (var unit in GameKnowledgeCatalog.BuildCanonicalIdentityCatalog().Units)
                {
                    var parsed = CommanderSemanticJson.Parse(Plan("\"unit\":\"" + unit.StableId + "\",\"count\":1"));
                    Assert.That(parsed.IsValid, Is.True, unit.StableId);
                    Assert.That(CommanderDynamicCompiler.TryCompile(parsed.DynamicPlan, sim, manager,
                        out var graph, out var reason), Is.True, unit.StableId + ": " + reason);
                    var selector = ((CapabilityActionIntent)graph.Nodes[0].Intent).UnitSelector;
                    Assert.That(selector.UnitType, Is.EqualTo(unit.UnitType));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void NativeScatterEnvelopesMayOverlap_WhileCentersRemainDistinct()
        {
            var config = ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var sim = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                var planner = new CommanderPlanner(sim, null);
                var method = typeof(CommanderPlanner).GetMethod("TryKnownNativeMoveEnvelope",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                int midX = sim.MapData.Width / 2, midZ = sim.MapData.Height / 2;
                for (int x = midX - 20; x <= midX + 20; x++)
                    for (int z = midZ - 20; z <= midZ + 20; z++) sim.FogOfWar.SetVisible(0, x, z);
                var unit = sim.UnitRegistry.CreateUnit(0, sim.MapData.TileToWorldFixed(midX, midZ),
                    Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                Assert.That(sim.MaximumNativeMoveScatter(unit), Is.GreaterThan(Fixed32.Zero));
                bool tested = false;
                for (int x = midX - 10; x <= midX + 10 && !tested; x++)
                    for (int z = midZ - 10; z <= midZ + 10 && !tested; z++)
                    {
                        var slot = sim.MapData.TileToWorldFixed(x, z);
                        unit.SimPosition = slot;
                        object[] args = { 0, new Vector2Int(x, z), unit, slot, slot,
                            new HashSet<Vector2Int>(), default(FixedVector3), default(Fixed32), null, null, -1 };
                        if (!(bool)method.Invoke(planner, args)) continue;
                        var center = sim.MapData.WorldToTile((FixedVector3)args[6]);
                        foreach (var possibleTile in (HashSet<Vector2Int>)args[8])
                        {
                            if (possibleTile == center) continue;
                            args[5] = new HashSet<Vector2Int> { possibleTile };
                            Assert.That((bool)method.Invoke(planner, args), Is.True,
                                "A different reserved center inside the scatter envelope must not reject a reachable slot.");
                            args[5] = new HashSet<Vector2Int> { center };
                            Assert.That((bool)method.Invoke(planner, args), Is.False,
                                "Identical reserved centers must still be rejected.");
                            tested = true;
                            break;
                        }
                    }
                Assert.That(tested, Is.True, "Fixture needs a legal multi-tile native scatter envelope.");
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void Exclusions_RejectUnknownOrConflictingFields()
        {
            Assert.That(CommanderSemanticJson.Parse(Plan("\"kind\":\"Military\",\"excludeUnit\":\"unit:2\",\"excludeKind\":\"Villagers\",\"count\":1")).IsValid, Is.False);
            Assert.That(CommanderSemanticJson.Parse(Plan("\"kind\":\"Military\",\"excludeKind\":\"Anything\",\"count\":1")).IsValid, Is.False);
        }
    }
}
