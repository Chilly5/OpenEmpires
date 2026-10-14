using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public sealed class CommanderMovementSelectionRepairTests
    {
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
                    }
                }
                else await chat.SubmitMessageAsync("yes");
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
                var example = CommanderSemanticJson.Parse(instruction.Substring(start, end - start));
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

        [Test]
        public void UnreachableMarker_RejectedBeforeAdmissionAndRetainsMarker()
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
                var parsed = CommanderSemanticJson.Parse(Plan("\"unit\":\"unit:1\",\"count\":1"));
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

        [Test]
        public void MarkerSelection_IsFrozenAndMissingActorCannotBeReplaced()
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
                var interpretation = CommanderSemanticJson.Parse(Plan("\"unit\":\"unit:1\",\"count\":1"));
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
