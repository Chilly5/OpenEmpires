using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5BFinalFix")]
    public sealed class CommanderPhase5BFinalIntentContractTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager manager;

        [SetUp] public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
            manager = new CommanderGoalManager(simulation, 0);
        }
        [TearDown] public void TearDown()
        { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }

        private CommanderContext Context(string input)
            => new CommanderContextBuilder().Build(simulation, manager, input);

        private static string Build(string name, string builders = "")
            => "{\"type\":\"BuildStructure\",\"structure\":\"" + name + "\",\"count\":1" + builders + "}";
        private const string IdleBuilder = ",\"builders\":{\"state\":\"IdleOnly\",\"count\":1}";
        private const string Allocation = "{\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\",\"countMode\":\"Exact\",\"count\":1,\"workers\":{\"state\":\"Idle\"},\"destination\":{\"resource\":\"Wood\"}";

        private static CommanderSemanticResult ParseProviderContract(string text)
        {
            var method = typeof(CommanderSemanticJson).GetMethod("ParseProviderResponse", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Provider replies require their explicit declaration contract.");
            return (CommanderSemanticResult)method.Invoke(null, new object[] { text });
        }

        [TestCase("Lumber Yard")] [TestCase("Mill")] [TestCase("House")]
        public async Task MissingBuilderDeclaration_ProviderRejectsRatherThanDefaulting(string building)
        {
            var transport = new Transport("{\"outcome\":\"Request\",\"nodes\":[" + Build(building) + "]}");
            var provider = new OpenRouterCommanderProvider("test-only-key", transport);
            var result = await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest(
                "Build " + building + " with one idle villager", Context(building)), CancellationToken.None);
            Assert.That(result.IsValid, Is.False, "A provider construction reply must declare builder eligibility and count.");
            Assert.That(result.Nodes, Is.Empty);
            Assert.That(transport.Calls, Is.EqualTo(1), "Contract rejection must not broaden schema repairs.");
        }

        [Test] public void ExplicitIdleBuilder_AdmissionPreservesEligibility()
        {
            var result = ParseProviderContract("{\"outcome\":\"Request\",\"nodes\":[" + Build("Mill", IdleBuilder) + "]}");
            Assert.That(result.IsValid, Is.True, result.SafeExplanation);
            Assert.That(CommanderSemanticAdmission.TryCreateTacticalIntent(result.Nodes.Single(),
                Context("Build a Mill with one idle villager"), out var intent, out var reason), Is.True, reason);
            Assert.That(intent.Constraints.OfType<PreferredWorkersConstraint>().Single().WorkerSource,
                Is.EqualTo(CommanderPreferredWorkerSource.IdleOnly));
            Assert.That(manager.Goals, Is.Empty, "Semantic admission is not gameplay authorization.");
        }

        [Test] public void UnrepresentableBuilderCount_RejectsRatherThanChoosingOne()
        {
            var result = ParseProviderContract("{\"outcome\":\"Request\",\"nodes\":["
                + Build("House", ",\"builders\":{\"state\":\"IdleOnly\",\"count\":2}") + "]}");
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Nodes, Is.Empty);
        }

        [Test] public async Task MissingCompoundOrdering_ProviderRejectsRatherThanRunningBothImmediately()
        {
            var transport = new Transport("{\"outcome\":\"Request\",\"nodes\":["
                + Build("House") + "," + Allocation + "}]}");
            var provider = new OpenRouterCommanderProvider("test-only-key", transport);
            var result = await provider.TranslateSemanticAsync(
                new CommanderSemanticProviderRequest("Build a House, then assign one idle villager to wood", Context("House")),
                CancellationToken.None);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Nodes, Is.Empty);
            Assert.That(transport.Calls, Is.EqualTo(1));
            Assert.That(provider.LastRequestTrace, Does.Contain("field=executionOrder"));
        }

        [TestCase(false)] [TestCase(true)]
        public void DeclaredSequentialOrdering_RequiresCompletionDependency(bool dependency)
        {
            var result = ParseProviderContract("{\"outcome\":\"Request\",\"executionOrder\":\"Sequential\",\"nodes\":["
                + Build("House", IdleBuilder) + "," + Allocation
                + (dependency ? ",\"dependsOn\":[0]" : "") + "}]}");
            Assert.That(result.IsValid, Is.EqualTo(dependency), result.SafeExplanation);
            if (dependency)
            {
                Assert.That(result.Nodes[1].DependsOn, Is.EqualTo(new[] { 0 }));
                Assert.That(result.Nodes[0].Constraints.OfType<PreferredWorkersConstraint>().Count(), Is.EqualTo(1));
            }
        }

        [Test] public async Task InvalidConstraintMode_ReportsOnlySanitizedFieldDiagnostic()
        {
            const string privateMarker = "PRIVATE_VALUE_MUST_NEVER_LEAK";
            var transport = new Transport("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Mill\",\"count\":1,"
                + "\"constraints\":[{\"type\":\"PreferredWorkers\",\"mode\":\"" + privateMarker + "\"}]}]}");
            var provider = new OpenRouterCommanderProvider("test-only-key", transport);
            var result = await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest(
                "Build a Mill with one idle villager", Context("Mill")), CancellationToken.None);
            Assert.That(result.IsValid, Is.False);
            Assert.That(provider.LastRequestTrace, Does.Contain("field=nodes[0].constraints[0].mode"));
            Assert.That(provider.LastRequestTrace, Does.Contain("code=invalid-enum"));
            Assert.That(provider.LastRequestTrace, Does.Not.Contain(privateMarker));
            Assert.That(result.SafeExplanation, Does.Not.Contain(privateMarker));
            Assert.That(transport.Calls, Is.EqualTo(1));
        }

        private sealed class Transport : ICommanderHttpTransport
        {
            private readonly string[] texts;
            public int Calls;
            public Transport(params string[] texts) { this.texts = texts; }
            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string body,
                IReadOnlyDictionary<string, string> headers, CancellationToken token)
            {
                Calls++;
                return Task.FromResult(new CommanderHttpResponse(200, new JObject {
                    ["choices"] = new JArray(new JObject { ["message"] = new JObject { ["content"] = texts[Math.Min(Calls - 1, texts.Length - 1)] },
                        ["finish_reason"] = "stop" }) }.ToString()));
            }
        }

        [TestCase("anchor", "\"Woodline\"", "anchor")]
        [TestCase("relation", "\"Nearest\"", "relation")]
        [TestCase("clearGapTiles", "5", "clearGapTiles")]
        [TestCase("sourceKind", "\"Trees\"", "sourceKind")]
        [TestCase("resource", "\"Timber\"", "resource")]
        public async Task InvalidPlacement_IdentifiesFieldWithoutPrintingValue(string field, string value, string diagnosticField)
        {
            var placement = new JObject { ["anchor"] = "VisibleResource", ["relation"] = "Near", ["resource"] = "Wood", ["sourceKind"] = "Tree" };
            placement[field] = JToken.Parse(value);
            var node = JObject.Parse(Build("Lumber Yard", IdleBuilder)); node["placement"] = placement;
            var transport = new Transport(new JObject { ["outcome"] = "Request", ["nodes"] = new JArray(node) }.ToString());
            var provider = new OpenRouterCommanderProvider("test-only-key", transport);
            var result = await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build near wood", Context("Lumber Yard")), CancellationToken.None);
            Assert.That(result.IsValid, Is.False);
            Assert.That(provider.LastRequestTrace, Does.Contain("field=nodes[0].placement." + diagnosticField));
            Assert.That(transport.Calls, Is.EqualTo(1));
        }

        [Test] public async Task NumericOnlyRepair_WithDeclarations_PreservesIntentAndUsesOnlyOneRepair()
        {
            string valid = "{\"outcome\":\"Request\",\"nodes\":[" + Build("Mill", IdleBuilder) + "]}";
            var initial = JObject.Parse(valid); initial["nodes"][0]["count"] = "1";
            var transport = new Transport(initial.ToString(), valid);
            var provider = new OpenRouterCommanderProvider("test-only-key", transport);
            var result = await provider.TranslateSemanticAsync(
                new CommanderSemanticProviderRequest("Build a Mill with one idle villager", Context("Mill")), CancellationToken.None);
            Assert.That(result.IsValid, Is.True, result.SafeExplanation);
            Assert.That(transport.Calls, Is.EqualTo(2));
            Assert.That(provider.LastRequestTrace, Does.Contain("field=nodes[0].count"), "Retain the initial schema failure across the repair HTTP call.");
            Assert.That(result.Nodes.Single().Constraints.OfType<PreferredWorkersConstraint>().Single().WorkerSource,
                Is.EqualTo(CommanderPreferredWorkerSource.IdleOnly));
        }

        [Test] public async Task NumericStringWithoutDeclarations_DoesNotSpendARepairAttempt()
        {
            var initial = JObject.Parse("{\"outcome\":\"Request\",\"nodes\":[" + Build("Mill") + "]}");
            initial["nodes"][0]["count"] = "1";
            var transport = new Transport(initial.ToString());
            var result = await new OpenRouterCommanderProvider("test-only-key", transport).TranslateSemanticAsync(
                new CommanderSemanticProviderRequest("Build a Mill with one idle villager", Context("Mill")), CancellationToken.None);
            Assert.That(result.IsValid, Is.False);
            Assert.That(transport.Calls, Is.EqualTo(1), "Missing intent declarations cannot be invented by numeric repair.");
        }
    }
}
