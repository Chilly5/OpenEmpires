using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5C")]
    public sealed class CommanderPhase5CProviderBudgetTests
    {
        [Test]
        public async Task PlaceTwoHousesNearTownCenter_RetrievesAdmittableRepeatedBuildRoute()
        {
            const string utterance = "Place two Houses near my Town Center.";
            const string response = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + "{\"id\":\"site\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"MyTownCenter\",\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{},\"dependsOn\":[]},"
                + "{\"id\":\"houses\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:House\",\"count\":2},\"inputs\":{\"location\":\"site\"},\"dependsOn\":[\"site\"]}]}";
            var config = UnityEngine.ScriptableObject.CreateInstance<SimulationConfig>();
            CommanderGoalManager manager = null;
            try
            {
                var simulation = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                simulation.SetPlayerCivilizations(new[] { Civilization.English });
                manager = new CommanderGoalManager(simulation, 0);
                var transport = new CaptureTransport { Replies = new[] { response } };
                var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
                var context = new CommanderContextBuilder().Build(simulation, manager, utterance);
                var result = await provider.TranslateSemanticAsync(
                    new CommanderSemanticProviderRequest(utterance, context), CancellationToken.None);
                Assert.That(transport.Calls, Is.EqualTo(1), result.SafeExplanation
                    + "; estimate=" + provider.LastInputEstimate
                    + "; sections=" + JsonConvert.SerializeObject(provider.LastInputSectionEstimates));
                string contract = (string)JObject.Parse(transport.Body)["messages"]?[0]?["content"];
                Assert.That(contract, Does.Contain("DynamicPlan for repeated placed construction"));
                Assert.That(contract, Does.Contain("BuildStructure{structure:canonical,count:1,"));
                Assert.That(contract, Does.Not.Contain("BuildStructure{structure:canonical,count:1..20,"));
                Assert.That(PackedKnowledgeIds(CommanderSemanticRequestPack.TryBuild(
                    new CommanderSemanticProviderRequest(utterance, context), "openai/gpt-6-luna", 4096,
                    out var pack, out _) ? pack : null), Does.Contain("building:House"));
                Assert.That(result.IsValid && result.Outcome == CommanderSemanticOutcome.DynamicPlan, Is.True, result.SafeExplanation);
                Assert.That(manager.PrepareActionPlan(result, utterance, 1).Preview, Does.Contain("House"));
                Assert.That(manager.Goals, Is.Empty, "Semantic admission previews only; no prefix executes before approval.");
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }
        [TestCase("Garrison all villagers in my Town Center.", "GarrisonUnits")]
        [TestCase("Ungarrison my Town Center.", "UngarrisonBuilding")]
        [TestCase("Find the enemy Town Center with my Scout.", "DiscoverEnemy")]
        public void NewNativeAdapters_ReceiveAccurateCompactContract(string utterance, string action)
        {
            var request = new CommanderSemanticProviderRequest(utterance,
                CommanderEconomyClarificationTests.Context());
            Assert.That(CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
                out var pack, out string reason), Is.True, reason);
            string instruction = (string)JObject.Parse(pack.Json)["messages"]?[0]?["content"];
            Assert.That(instruction, Does.Contain(action));
            if (action == "GarrisonUnits") Assert.That(instruction, Does.Contain("AllMatching"));
            if (action == "UngarrisonBuilding") Assert.That(instruction, Does.Contain("building-wide"));
            if (action == "DiscoverEnemy") Assert.That(instruction, Does.Contain("finite"));
            Assert.That(pack.EstimatedInputTokens, Is.LessThan(4000));
        }

        [Test]
        public void ExplicitCurrentWoodMove_ReceivesTypedComposedDynamicContractWithoutCount50Downgrade()
        {
            var request = new CommanderSemanticProviderRequest(
                "Move all of my Wood villagers to the woodline west of my Town Center.",
                CommanderEconomyClarificationTests.Context());
            Assert.That(CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
                out var pack, out string reason), Is.True, reason);
            string instruction = (string)JObject.Parse(pack.Json)["messages"]?[0]?["content"];
            Assert.That(instruction, Does.Contain("move-workers"));
            Assert.That(instruction, Does.Contain("countMode:AllMatching"));
            Assert.That(instruction, Does.Contain("sourceKind:Tree"));
            Assert.That(instruction, Does.Not.Contain("ALL/current-resource workers/composed side+resource+TC location unsupported"));
            Assert.That(pack.EstimatedInputTokens, Is.LessThan(4000));
        }
        [TestCase("Gather 200 Gold with three villagers from Wood.")]
        [TestCase("Build a House, then send the next five villagers from my first Town Center to gather Wood from trees.")]
        public async Task OrdinaryRequests_KeepCompleteModelPayloadBelowConservativeInputBudget(string utterance)
        {
            var transport = new CaptureTransport();
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest(utterance,
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(transport.Calls, Is.EqualTo(1));
            var body = JObject.Parse(transport.Body);
            var messages = (JArray)body["messages"];
            // Count model-visible content/roles, not HTTP JSON escaping that the model never sees.
            int byteUnits = 0;
            foreach (JObject message in messages) byteUnits += Encoding.UTF8.GetByteCount((string)message["role"])
                + Encoding.UTF8.GetByteCount((string)message["content"]);
            TestContext.WriteLine("Offline pack audit: " + new JObject {
                ["utterance"] = utterance, ["systemUtf8Units"] = Encoding.UTF8.GetByteCount((string)messages[0]["content"]),
                ["userUtf8Units"] = Encoding.UTF8.GetByteCount((string)messages[1]["content"]),
                ["estimateWithMargin"] = byteUnits + 256, ["estimateMethod"] = "UTF8 byte units plus256 message margin; not verified model tokenizer",
                ["actualPromptTokens"] = JValue.CreateNull() }.ToString(Formatting.None));
            Assert.That(byteUnits + 256, Is.LessThan(4000),
                "The whole outgoing model-facing request needs a conservative bound, not an output max_tokens or characters/4 claim.");
            Assert.That((string)messages[1]["content"], Does.Contain(utterance), "Never truncate the player goal to fit.");
        }

        [Test] public async Task TooLargeRequiredMeaning_IsRejectedWithoutDispatchingOrExecutingPrefix()
        {
            var transport = new CaptureTransport();
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            string message = new string('\u754c', 1000); // Valid character length, too large for the full input-byte margin.
            var result = await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest(message,
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(transport.Calls, Is.Zero);
            Assert.That(result.Nodes, Is.Empty);
            Assert.That(result.SafeExplanation, Does.Contain("limit"));
        }

        [TestCase("Build a House, then send the next five villagers from my first Town Center to gather Wood from trees.")]
        [TestCase("Move all of my Wood villagers to the woodline left of my Town Center.")]
        public async Task RealProductionCatalogProjection_StillFitsCompoundAndComposedRequests(string utterance)
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<SimulationConfig>();
            CommanderGoalManager manager = null;
            try
            {
                var simulation = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                simulation.SetPlayerCivilizations(new[] { Civilization.English });
                manager = new CommanderGoalManager(simulation, 0);
                var context = new CommanderContextBuilder().Build(simulation, manager, utterance);
                var transport = new CaptureTransport();
                var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
                var result = await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest(utterance, context), CancellationToken.None);
                TestContext.WriteLine("pack sections=" + JsonConvert.SerializeObject(provider.LastInputSectionEstimates)
                    + "; estimate=" + provider.LastInputEstimate);
                if (transport.Body != null)
                {
                    string packedUser = (string)JObject.Parse(transport.Body)["messages"]?[1]?["content"];
                    string packedContext = packedUser.Substring("Context:".Length,
                        packedUser.IndexOf("\nMemory:", StringComparison.Ordinal) - "Context:".Length);
                    var packedFacts = JObject.Parse(packedContext);
                    TestContext.WriteLine("context-unit-lengths=" + JsonConvert.SerializeObject(packedFacts.Properties()
                        .Select(p => new { key = p.Name, bytes = Encoding.UTF8.GetByteCount(p.Value.ToString(Formatting.None)) })));
                    if (packedFacts["knowledge"] is JObject observedKnowledge)
                        TestContext.WriteLine("knowledge-unit-lengths=" + JsonConvert.SerializeObject(observedKnowledge.Properties()
                            .Select(p => new { key = p.Name, bytes = Encoding.UTF8.GetByteCount(p.Value.ToString(Formatting.None)) })));
                }
                Assert.That(transport.Calls, Is.EqualTo(1), result.SafeExplanation + "; conservative estimate=" + provider.LastInputEstimate
                    + "; sections=" + JsonConvert.SerializeObject(provider.LastInputSectionEstimates));
                Assert.That(provider.LastInputEstimate, Is.LessThan(4000));
                TestContext.WriteLine("Production catalog estimate=" + provider.LastInputEstimate + "; actual tokens unknown; " + utterance);
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test] public async Task MalformedUsageMetadata_DoesNotInvalidateOtherwiseValidSemanticReply()
        {
            var transport = new CaptureTransport { Usage = new JObject { ["prompt_tokens"] = -1,
                ["prompt_tokens_details"] = "not an object", ["cost"] = "unverified" } };
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            var result = await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(result.IsValid, Is.True, "Invalid observation metadata cannot poison valid semantic content.");
            Assert.That(Read(provider.LastUsage, "PromptTokens"), Is.Null);
            Assert.That(Read(provider.LastUsage, "CachedTokens"), Is.Null);
            Assert.That(Read(provider.LastUsage, "Cost"), Is.Null);
        }

        [Test] public async Task ResponseUsage_IsRetainedAsTypedObservedMetricsIncludingCachedInput()
        {
            var transport = new CaptureTransport {
                Usage = new JObject { ["prompt_tokens"] = 900, ["completion_tokens"] = 60,
                    ["total_tokens"] = 960, ["cost"] = .002m,
                    ["prompt_tokens_details"] = new JObject { ["cached_tokens"] = 250 },
                    ["completion_tokens_details"] = new JObject { ["reasoning_tokens"] = 20 } } };
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            var usageProperty = typeof(OpenRouterCommanderProvider).GetProperty("LastUsage");
            Assert.That(usageProperty, Is.Not.Null, "Production currently drops the usage envelope.");
            var usage = usageProperty.GetValue(provider);
            Assert.That(Read(usage, "PromptTokens"), Is.EqualTo(900));
            Assert.That(Read(usage, "CompletionTokens"), Is.EqualTo(60));
            Assert.That(Read(usage, "CachedTokens"), Is.EqualTo(250));
            Assert.That(Read(usage, "ReasoningTokens"), Is.EqualTo(20));
            Assert.That(Read(usage, "Cost"), Is.EqualTo(.002m));
        }

        [Test] public async Task MissingResponseUsage_RemainsUnknownNotZero()
        {
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", new CaptureTransport());
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            var property = typeof(OpenRouterCommanderProvider).GetProperty("LastUsage");
            Assert.That(property, Is.Not.Null);
            Assert.That(Read(property.GetValue(provider), "PromptTokens"), Is.Null);
        }

        [TestCase("Set the Wood allocation to zero.", "SetResourceAllocation")]
        [TestCase("Send spearmen to guard my Gold mine.", "DefendArea")]
        [TestCase("Split all idle villagers between Food and Wood.", "partition-workers")]
        [TestCase("Recruit archers and send them to guard my Gold mine.", "EnsureUnitCount")]
        public async Task MixedAndAliasRequests_KeepRegisteredFormsVisible(string utterance, string required)
        {
            var transport = new CaptureTransport();
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest(utterance,
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(transport.Calls, Is.EqualTo(1));
            string contract = (string)JObject.Parse(transport.Body)["messages"]?[0]?["content"];
            Assert.That(contract, Does.Contain(required));
        }

        [Test] public async Task GenericRequest_AdvertisesEveryStrictlyParsedSemanticFamily()
        {
            var transport = new CaptureTransport();
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            var request = new CommanderSemanticProviderRequest("Help me with my army and economy.",
                CommanderEconomyClarificationTests.Context());
            CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096, out var pack, out _);
            string contract = (string)JObject.Parse(pack.Json)["messages"]?[0]?["content"];
            foreach (CommanderSemanticNodeType type in Enum.GetValues(typeof(CommanderSemanticNodeType)))
                Assert.That(contract, Does.Contain(type.ToString()), type.ToString());
            foreach (string required in new[] { "count:1..20", "count:0..200", "count:1..200",
                "count:1..50", "targetAge:Feudal|Castle|Imperial|Next", "resourceAmount?:1..1000000",
                "MaximumQueue amount1..8", "constraints?<=4", "nodes:1..4", "message<=180",
                "sourceKind:Any", "currentResource?", "executionOrder:Independent|Sequential|DependencyGraph" })
                Assert.That(contract, Does.Contain(required), required);
            await provider.TranslateSemanticAsync(request, CancellationToken.None);
            Assert.That(transport.Calls == 1 || provider.LastInputEstimate >= 4000, Is.True,
                "An overfull all-family fallback must reject before HTTP, never send an oversized prompt.");
        }

        [Test] public async Task EmptyUsageObject_IsUnknownAndDoesNotInventPromptTokens()
        {
            var transport = new CaptureTransport { Usage = new JObject() };
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(provider.LastUsage.HasReportedUsage, Is.False);
            Assert.That(provider.LastUsage.PromptTokens, Is.Null);
        }

        [Test] public async Task Repair_RetainsBothAttemptEstimatesAndUsage()
        {
            const string invalid = "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":\"3\"}]}";
            const string corrected = "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3}]}";
            var transport = new CaptureTransport { Replies = new[] { invalid, corrected },
                Usages = new[] { new JObject { ["prompt_tokens"] = 850, ["cost"] = .001m },
                    new JObject { ["prompt_tokens"] = 180, ["cost"] = .0002m } } };
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            var result = await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Train 3 spearmen.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(result.IsValid, Is.True, result.SafeExplanation);
            Assert.That(transport.Calls, Is.EqualTo(2));
            var attemptsProperty = typeof(OpenRouterCommanderProvider).GetProperty("LastSemanticAttempts");
            Assert.That(attemptsProperty, Is.Not.Null);
            var attempts = (System.Collections.IList)attemptsProperty.GetValue(provider);
            Assert.That(attempts, Has.Count.EqualTo(2));
            Assert.That(Read(attempts[0], "InputEstimate"), Is.GreaterThan(0));
            Assert.That(Read(attempts[1], "InputEstimate"), Is.GreaterThan(0));
            Assert.That(Read(Read(attempts[0], "Usage"), "PromptTokens"), Is.EqualTo(850));
            Assert.That(Read(Read(attempts[1], "Usage"), "PromptTokens"), Is.EqualTo(180));
            Assert.That(Read(Read(attempts[0], "Usage"), "Cost"), Is.EqualTo(.001m));
            Assert.That(Read(Read(attempts[1], "Usage"), "Cost"), Is.EqualTo(.0002m));
            Assert.That(Read(attempts[0], "ResultStage"), Does.Contain("schema"));
            Assert.That(Read(attempts[1], "ResultStage"), Does.Contain("valid"));
        }

        [Test] public void ReferencedAndAmbiguousCanonicalNames_SurviveKnowledgeRetrieval()
        {
            var catalog = GameKnowledgeCatalog.BuildCanonicalIdentityCatalog();
            var relevant = JObject.Parse(CommanderKnowledgeRetrieval.Retrieve(catalog,
                "Build a House near my Town Center then train Spearman at the Barracks.", Civilization.English));
            var ids = new HashSet<string>();
            foreach (JObject row in (JArray)relevant["units"]) ids.Add((string)row["id"]);
            foreach (JObject row in (JArray)relevant["buildings"]) ids.Add((string)row["id"]);
            foreach (string id in new[] { "building:House", "building:TownCenter", "building:Barracks",
                catalog.FindUnit("Spearman").StableId })
                Assert.That(ids, Does.Contain(id), id);
            var ambiguous = JObject.Parse(CommanderKnowledgeRetrieval.Retrieve(catalog,
                "Build a dropoff.", Civilization.English));
            Assert.That(((JArray)ambiguous["nameInterpretations"]).ToString(Formatting.None), Does.Contain("dropoff"));
        }

        [Test] public void ReferencedFacts_PreserveColumnsNullsCapabilityAndBoundedPending()
        {
            var knowledge = new JObject { ["units"] = new JArray(
                new JObject { ["id"] = "unit:Spearman", ["name"] = "Spearman", ["age"] = 2,
                    ["producer"] = "Barracks", ["cost"] = new JObject { ["food"] = 50, ["wood"] = 20, ["gold"] = 0, ["stone"] = 0 },
                    ["availableForCivilization"] = true, ["commanderExecutable"] = true },
                new JObject { ["id"] = "unit:Horseman", ["name"] = "Horseman", ["age"] = 2,
                    ["producer"] = JValue.CreateNull(), ["cost"] = new JObject { ["food"] = 0, ["wood"] = 0, ["gold"] = 30, ["stone"] = 0 },
                    ["availableForCivilization"] = true, ["commanderExecutable"] = true }) };
            var context = new CommanderContext(0, 0, new CommanderResourceSnapshot(0, 0, 0, 0),
                0, 0, 200, 1, "English", new List<CommanderBuildingSnapshot>(), new List<CommanderUnitSnapshot>(),
                new List<CommanderBuildingSnapshot>(), new List<string>(), new List<CommanderGoalSnapshot>(),
                new List<CommanderVisibleResourceSnapshot>(), new List<CommanderUnitOptionSnapshot>(),
                new List<CommanderWorkerAllocationSnapshot>(), new List<CommanderVisibleEnemyMilitarySnapshot>(),
                knowledge.ToString(Formatting.None), canonicalUnitIds: new[] { "unit:Spearman" });
            var pending = new CommanderPendingClarification(new CommanderWorkerAllocationDraft(
                CommanderWorkerAllocationMode.SelectedCount, CommanderWorkerCountMode.Exact, null,
                new CommanderWorkerSelector(CommanderWorkerState.Gathering, ResourceType.Wood),
                new CommanderResourceDestination(ResourceType.Food, ResourceSourceKind.Berries),
                200, CommanderResourceAmountMode.AdditionalGathered),
                "Gather 200 additional Food from Berries with Wood villagers", "How many villagers?", 1, 12);
            var request = new CommanderSemanticProviderRequest("three", context, pendingClarification: pending);
            CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096, out var pack, out _);
            string user = (string)JObject.Parse(pack.Json)["messages"]?[1]?["content"];
            Assert.That(user, Does.Contain(pending.Serialize()));
            string compact = user.Substring("Context:".Length, user.IndexOf("\nMemory:", StringComparison.Ordinal) - "Context:".Length);
            var facts = (JObject)JObject.Parse(compact)["knowledge"];
            var columns = ((JArray)facts["unitsColumns"]).Select(x => (string)x).ToArray();
            var rows = (JArray)facts["units"];
            int id = Array.IndexOf(columns, "id"), producer = Array.IndexOf(columns, "producer");
            int cost = Array.IndexOf(columns, "cost"), capability = Array.IndexOf(columns, "capabilitySupported");
            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That((string)rows[0][id], Is.EqualTo("unit:Spearman"));
            Assert.That((string)rows[1][id], Is.EqualTo("unit:Horseman"));
            Assert.That(rows[1][producer].Type, Is.EqualTo(JTokenType.Null));
            Assert.That((int)rows[0][cost][0], Is.EqualTo(50));
            Assert.That((bool)rows[1][capability], Is.False, "Knowledge must not grant unavailable execution capability.");
        }

        [Test] public async Task PreflightRejectionAfterASentRequest_ResetsAttemptCount()
        {
            var transport = new CaptureTransport();
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(provider.LastSemanticAttempts, Has.Count.EqualTo(1));
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest(new string('\u754c', 1000),
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(transport.Calls, Is.EqualTo(1));
            Assert.That(provider.LastSemanticAttempts, Is.Empty);
            Assert.That(provider.HasAttemptedRequest, Is.False);
        }

        [Test] public async Task CompletedAttemptSnapshot_RemainsImmutableAcrossLaterRequests()
        {
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", new CaptureTransport());
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            var completed = provider.LastSemanticAttempts;
            Assert.That(completed, Has.Count.EqualTo(1));
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest(new string('\u754c', 1000),
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(completed, Has.Count.EqualTo(1), "A later request may not erase an earlier HTTP attempt record.");
            Assert.That(provider.LastSemanticAttempts, Is.Empty);
        }

        [Test] public void NineExplicitCanonicalNames_SurviveRealContextAndPackedColumns()
        {
            const string utterance = "Train Villager, Spearman, Archer, Horseman, Scout, Knight, Crossbowman, Monk, and Longbowman.";
            var request = ProductionRequest(utterance);
            Assert.That(CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
                out var pack, out string reason), Is.True, reason);
            var ids = PackedKnowledgeIds(pack);
            foreach (string id in new[] { "unit:0", "unit:1", "unit:2", "unit:3", "unit:4",
                "unit:7", "unit:8", "unit:9", "unit:10" })
                Assert.That(ids, Does.Contain(id), id);
        }

        [Test] public void SimilarNameHint_SurvivesRealRetrievalAndPackWithoutBecomingRequired()
        {
            var request = ProductionRequest("Train longbowmna.");
            var sourceKnowledge = (JObject)JObject.Parse(request.SerializedContext)["knowledge"];
            Assert.That(((JArray)sourceKnowledge["requiredCanonicalIds"]), Is.Empty,
                "A typo suggestion must not acquire required/executable authority.");
            Assert.That(CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
                out var pack, out string reason), Is.True, reason);
            string user = (string)JObject.Parse(pack.Json)["messages"]?[1]?["content"];
            string compact = user.Substring("Context:".Length,
                user.IndexOf("\nMemory:", StringComparison.Ordinal) - "Context:".Length);
            var packedKnowledge = (JObject)JObject.Parse(compact)["knowledge"];
            var columns = ((JArray)packedKnowledge["nameInterpretationsColumns"]).Select(x => (string)x).ToArray();
            var interpretations = (JArray)packedKnowledge["nameInterpretations"];
            Assert.That(interpretations, Is.Not.Null);
            var typo = interpretations.OfType<JArray>().Single(x => (string)x[Array.IndexOf(columns, "phrase")] == "longbowmna");
            Assert.That((bool)typo[Array.IndexOf(columns, "interpretationOnly")], Is.True);
            Assert.That((bool)typo[Array.IndexOf(columns, "ambiguous")], Is.False);
            Assert.That(((JArray)typo[Array.IndexOf(columns, "unitCandidates")]).Select(x => (string)x), Does.Contain("unit:10"));
            Assert.That(PackedKnowledgeIds(pack), Does.Contain("unit:10"));
            Assert.That(pack.EstimatedInputTokens, Is.LessThan(4000));
            string contract = (string)JObject.Parse(pack.Json)["messages"]?[0]?["content"];
            Assert.That(contract, Does.Contain("Similar-name hints are interpretation only; Clarify before Request"));
        }

        [Test] public void SimilarNameHint_DoesNotEvictExactCanonicalReferences()
        {
            const string utterance = "Train Villager, Spearman, Archer, Horseman, Scout, Knight, Crossbowman, Monk, and longbowmna.";
            var request = ProductionRequest(utterance);
            Assert.That(CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
                out var pack, out string reason), Is.True, reason);
            var ids = PackedKnowledgeIds(pack);
            foreach (string id in new[] { "unit:0", "unit:1", "unit:2", "unit:3", "unit:4",
                "unit:7", "unit:8", "unit:9" })
                Assert.That(ids, Does.Contain(id), id);
            Assert.That(ids, Does.Contain("unit:10"));
        }

        [Test] public void LateCanonicalName_AfterOldWordAndPhraseWindowsSurvivesRealPack()
        {
            string utterance = "Build " + string.Join(" ", Enumerable.Repeat("please", 100)) + " Market.";
            Assert.That(utterance.Length, Is.LessThanOrEqualTo(1024));
            var request = ProductionRequest(utterance);
            Assert.That(CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
                out var pack, out string reason), Is.True, reason);
            Assert.That(PackedKnowledgeIds(pack), Does.Contain("building:Market"));
        }

        [Test] public void PendingGraphAndAmbiguousAlternatives_AreRetainedOrWholeRequestRefused()
        {
            var proposal = new JObject { ["outcome"] = "Clarify", ["message"] = "Which count?",
                ["pending"] = new JObject { ["executionOrder"] = "Independent", ["nodes"] = new JArray(
                    new JObject { ["type"] = "BuildStructure", ["structure"] = "Market",
                        ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } }) },
                ["missingSlots"] = new JArray(new JObject { ["node"] = 0, ["path"] = "count",
                    ["type"] = "Count", ["alternatives"] = new JArray(1) }) };
            var parsed = CommanderSemanticJson.Parse(proposal.ToString(Formatting.None));
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var pending = new CommanderPendingClarification(parsed.PendingGraphDraft,
                "Build a dropoff", "Which count?", 1, 9);
            var config = UnityEngine.ScriptableObject.CreateInstance<SimulationConfig>();
            CommanderGoalManager manager = null;
            try
            {
                var simulation = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                simulation.SetPlayerCivilizations(new[] { Civilization.English });
                manager = new CommanderGoalManager(simulation, 0);
                var builder = new CommanderContextBuilder();
                var overload = typeof(CommanderContextBuilder).GetMethod("Build", new[] {
                    typeof(GameSimulation), typeof(CommanderGoalManager), typeof(string), typeof(string) });
                Assert.That(overload, Is.Not.Null, "Current reply and bounded pending graph must both reach retrieval.");
                var context = (CommanderContext)overload.Invoke(builder, new object[] {
                    simulation, manager, "one", pending.Serialize() });
                var request = new CommanderSemanticProviderRequest("one", context, pendingClarification: pending);
                bool fit = CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
                    out var pack, out string reason);
                if (!fit) Assert.That(reason, Does.Contain("required"));
                else
                {
                    var ids = PackedKnowledgeIds(pack);
                    Assert.That(ids, Does.Contain("building:Market"));
                    Assert.That((string)JObject.Parse(pack.Json)["messages"]?[1]?["content"], Does.Contain("dropoff"));
                }
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test] public async Task TooManyRequiredNames_RefuseWholeRequestBeforeHttp()
        {
            const string utterance = "Train Villager Spearman Archer Horseman Scout Knight Crossbowman Monk Longbowman "
                + "and build House Barracks TownCenter Mill LumberYard Mine ArcheryRange Stables Farm Market.";
            var transport = new CaptureTransport();
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            var result = await provider.TranslateSemanticAsync(ProductionRequest(utterance), CancellationToken.None);
            Assert.That(transport.Calls, Is.Zero);
            Assert.That(result.Nodes, Is.Empty);
            Assert.That(result.SafeExplanation, Does.Contain("required").IgnoreCase);
        }

        [TestCase("{\"choices\":[]}", "response-invalid-envelope")]
        [TestCase("{\"choices\":[{},{}]}", "response-invalid-envelope")]
        [TestCase("{\"choices\":[{\"finish_reason\":\"length\",\"message\":{\"content\":\"x\"}}]}", "response-truncated")]
        [TestCase("{\"choices\":[{\"finish_reason\":\"content_filter\",\"message\":{\"content\":\"x\"}}]}", "response-incomplete")]
        [TestCase("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"\"}}]}", "response-empty")]
        [TestCase("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":[{\"type\":\"image\"}]}}]}", "response-unsupported-parts")]
        [TestCase("{\"error\":{\"code\":500}}", "provider-error")]
        [TestCase("{\"choices\":[{\"finish_reason\":{},\"message\":{\"content\":\"x\"}}]}", "response-invalid-envelope")]
        [TestCase("{\"error\":{\"code\":{}}}", "provider-error")]
        [TestCase("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":[{\"type\":{},\"text\":\"x\"}]}}]}", "response-invalid-envelope")]
        public async Task RejectedSuccessfulHttp_RecordsActualResultStage(string envelope, string stage)
        {
            var transport = new EnvelopeTransport(new CommanderHttpResponse(200, envelope));
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            var result = await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Nodes, Is.Empty);
            Assert.That(transport.Calls, Is.EqualTo(1));
            Assert.That(provider.LastSemanticAttempts, Has.Count.EqualTo(1));
            Assert.That(provider.LastSemanticAttempts[0].HttpStatusCode, Is.EqualTo(200));
            Assert.That(provider.LastSemanticAttempts[0].ResultStage, Is.EqualTo(stage));
            Assert.That(result.SafeExplanation, Does.Not.Contain(envelope));
        }

        [Test] public async Task MalformedProviderErrorCode_UsesSafeDefaultProviderFailure()
        {
            var transport = new EnvelopeTransport(new CommanderHttpResponse(200, "{\"error\":{\"code\":{}}}"));
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            var result = await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.SafeExplanation, Is.EqualTo("Commander AI provider unavailable. Please try again later."));
            Assert.That(provider.LastSemanticAttempts, Has.Count.EqualTo(1));
            Assert.That(provider.LastSemanticAttempts[0].ResultStage, Is.EqualTo("provider-error"));
        }

        [Test] public async Task NullTransportResponse_RecordsTransportNullNotSuccess()
        {
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", new EnvelopeTransport(null));
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(provider.LastSemanticAttempts[0].ResultStage, Is.EqualTo("transport-null"));
            Assert.That(provider.LastSemanticAttempts[0].HttpStatusCode, Is.Null);
        }

        [Test] public async Task OversizedSuccessfulHttpBody_RecordsResponseOversize()
        {
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential",
                new EnvelopeTransport(new CommanderHttpResponse(200, new string('x', 65537))));
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(provider.LastSemanticAttempts, Has.Count.EqualTo(1));
            Assert.That(provider.LastSemanticAttempts[0].HttpStatusCode, Is.EqualTo(200));
            Assert.That(provider.LastSemanticAttempts[0].ResultStage, Is.EqualTo("response-oversize"));
        }

        [Test] public async Task OlderCancelledLateAttempt_DoesNotOverwriteNewerRequestMetrics()
        {
            var transport = new OverlapTransport();
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            using (var cancelled = new CancellationTokenSource())
            {
                var old = provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                    CommanderEconomyClarificationTests.Context()), cancelled.Token);
                cancelled.Cancel();
                await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a Mill.",
                    CommanderEconomyClarificationTests.Context()), CancellationToken.None);
                Assert.That(provider.LastSemanticAttempts, Has.Count.EqualTo(1));
                Assert.That(provider.LastUsage.PromptTokens, Is.EqualTo(222));
                Assert.That(provider.LastHttpStatusCode, Is.EqualTo(200));
                transport.ReleaseOld();
                Assert.ThrowsAsync<TaskCanceledException>(async () => await old);
                Assert.That(provider.LastSemanticAttempts, Has.Count.EqualTo(1));
                Assert.That(provider.LastSemanticAttempts[0].Usage.PromptTokens, Is.EqualTo(222));
                Assert.That(provider.LastUsage.PromptTokens, Is.EqualTo(222));
                Assert.That(provider.LastHttpStatusCode, Is.EqualTo(200));
                Assert.That(Read(provider, "SemanticHttpAttemptCount"), Is.EqualTo(2L));
                var history = (System.Collections.IEnumerable)Read(provider, "RecentSemanticAttempts");
                var observed = history.Cast<CommanderProviderAttempt>().ToArray();
                Assert.That(observed, Has.Length.EqualTo(2));
                Assert.That(observed.Single(x => x.ResultStage == "caller-canceled").Usage.PromptTokens, Is.Null);
            }
        }

        private static CommanderSemanticProviderRequest ProductionRequest(string utterance)
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<SimulationConfig>();
            CommanderGoalManager manager = null;
            try
            {
                var simulation = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                simulation.SetPlayerCivilizations(new[] { Civilization.English });
                manager = new CommanderGoalManager(simulation, 0);
                return new CommanderSemanticProviderRequest(utterance,
                    new CommanderContextBuilder().Build(simulation, manager, utterance));
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        private static HashSet<string> PackedKnowledgeIds(CommanderSemanticRequestPack pack)
        {
            string user = (string)JObject.Parse(pack.Json)["messages"]?[1]?["content"];
            string compact = user.Substring("Context:".Length,
                user.IndexOf("\nMemory:", StringComparison.Ordinal) - "Context:".Length);
            var knowledge = (JObject)JObject.Parse(compact)["knowledge"];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string table in new[] { "units", "buildings" })
            {
                if (!(knowledge[table] is JArray rows)) continue;
                var columns = ((JArray)knowledge[table + "Columns"]).Select(x => (string)x).ToArray();
                int idColumn = Array.IndexOf(columns, "id");
                foreach (JArray row in rows) ids.Add((string)row[idColumn]);
            }
            return ids;
        }

        private static object Read(object value, string name)
        { Assert.That(value, Is.Not.Null); var member = value.GetType().GetProperty(name); Assert.That(member, Is.Not.Null, name); return member.GetValue(value); }

        private sealed class CaptureTransport : ICommanderHttpTransport
        {
            internal string Body;
            internal int Calls;
            internal JObject Usage;
            internal string[] Replies;
            internal JObject[] Usages;
            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string body,
                IReadOnlyDictionary<string, string> headers, CancellationToken token)
            {
                Calls++; Body = body;
                var envelope = new JObject { ["choices"] = new JArray(new JObject {
                    ["finish_reason"] = "stop", ["message"] = new JObject {
                        ["content"] = Replies != null ? Replies[Calls - 1] : "{\"outcome\":\"Answer\",\"message\":\"Offline fixture.\"}" } }) };
                if (Usages != null) envelope["usage"] = Usages[Calls - 1];
                else if (Usage != null) envelope["usage"] = Usage;
                return Task.FromResult(new CommanderHttpResponse(200, envelope.ToString(Formatting.None)));
            }
        }

        private sealed class EnvelopeTransport : ICommanderHttpTransport
        {
            private readonly CommanderHttpResponse response;
            internal int Calls;
            internal EnvelopeTransport(CommanderHttpResponse response) => this.response = response;
            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string body,
                IReadOnlyDictionary<string, string> headers, CancellationToken token)
            { Calls++; return Task.FromResult(response); }
        }

        private sealed class OverlapTransport : ICommanderHttpTransport
        {
            private readonly TaskCompletionSource<CommanderHttpResponse> old =
                new TaskCompletionSource<CommanderHttpResponse>();
            private int calls;
            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string body,
                IReadOnlyDictionary<string, string> headers, CancellationToken token)
            {
                if (Interlocked.Increment(ref calls) == 1) return old.Task;
                return Task.FromResult(Reply(222));
            }
            internal void ReleaseOld() => old.TrySetResult(Reply(111));
            private static CommanderHttpResponse Reply(int prompt) => new CommanderHttpResponse(200,
                new JObject { ["usage"] = new JObject { ["prompt_tokens"] = prompt },
                    ["choices"] = new JArray(new JObject { ["finish_reason"] = "stop",
                        ["message"] = new JObject { ["content"] = "{\"outcome\":\"Answer\",\"message\":\"fixture\"}" } }) }
                .ToString(Formatting.None));
        }
    }
}
