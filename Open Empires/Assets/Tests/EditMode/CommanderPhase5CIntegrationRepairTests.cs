using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public sealed class CommanderPhase5CIntegrationRepairTests
    {
        private sealed class ObserverFixtureTransport:ICommanderHttpTransport
        {
            internal int Calls;
            internal readonly CommanderHttpResponse Response=new CommanderHttpResponse(200,
                new JObject{["choices"]=new JArray(new JObject{["message"]=new JObject{
                    ["content"]="{\"outcome\":\"Clarify\",\"message\":\"PRIVATE_REPLY_SENTINEL\",\"pending\":{\"nodes\":[]},\"missingSlots\":[]}"}})}.ToString());
            public System.Threading.Tasks.Task<CommanderHttpResponse> PostJsonAsync(Uri uri,string body,
                System.Collections.Generic.IReadOnlyDictionary<string,string> headers,System.Threading.CancellationToken token)
            {Calls++;return System.Threading.Tasks.Task.FromResult(Response);}
        }

        [Test]
        public async System.Threading.Tasks.Task DiagnosticObserver_FreshReceiptNeverOverwritesOrRepeatsDispatch()
        {
            var probe=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("OpenEmpires.EditorTools.CommanderPhase5CIntegrationProbe")).FirstOrDefault(t=>t!=null);
            var factory=probe?.GetMethod("ObserveNextResponse",new[]{typeof(ICommanderHttpTransport),typeof(string)});
            Assert.That(factory,Is.Not.Null,"Each separately authorized attempt needs a fresh receipt without overwriting the previous shape.");
            string directory=System.IO.Path.GetFullPath("Docs/CommanderPhase5C/evidence/observer-regression-temp/"+Guid.NewGuid().ToString("N"));
            string path=System.IO.Path.Combine(directory,"response-shape.json");
            var inner=new ObserverFixtureTransport();
            Func<ICommanderHttpTransport> create=()=> (ICommanderHttpTransport)factory.Invoke(null,new object[]{inner,path});
            Func<ICommanderHttpTransport,System.Threading.Tasks.Task<CommanderHttpResponse>> send=transport=>transport.PostJsonAsync(
                new Uri("https://example.invalid/fixture"),"{}",new System.Collections.Generic.Dictionary<string,string>(),System.Threading.CancellationToken.None);
            System.IO.Directory.CreateDirectory(directory);
            try
            {
                System.IO.File.WriteAllText(path,"preserved receipt");
                Assert.ThrowsAsync<InvalidOperationException>(async()=>await send(create()));
                Assert.That(inner.Calls,Is.Zero);
                Assert.That(System.IO.File.ReadAllText(path),Is.EqualTo("preserved receipt"));
                System.IO.File.Delete(path); // This test's own fresh sentinel only.
                var observer=create();
                Assert.That(await send(observer),Is.SameAs(inner.Response));
                string receipt=System.IO.File.ReadAllText(path);
                Assert.That(receipt,Does.Not.Contain("PRIVATE_REPLY_SENTINEL"));
                Assert.That(receipt,Does.Contain("$.pending.nodes"));
                Assert.ThrowsAsync<InvalidOperationException>(async()=>await send(observer));
                Assert.ThrowsAsync<InvalidOperationException>(async()=>await send(create()));
                Assert.That(inner.Calls,Is.EqualTo(1));
                Assert.That(System.IO.File.ReadAllText(path),Is.EqualTo(receipt));
            }
            finally
            {
                if(System.IO.File.Exists(path))System.IO.File.Delete(path);
                System.IO.Directory.Delete(directory);
            }
        }

        [Test]
        public void NextDiagnosticShape_IdentifiesContainersWithoutPersistingReplyValuesOrUnknownKeys()
        {
            var probe=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("OpenEmpires.EditorTools.CommanderPhase5CIntegrationProbe")).FirstOrDefault(t=>t!=null);
            var describe=probe?.GetMethod("DescribeSemanticEnvelope");
            Assert.That(describe,Is.Not.Null,"The next approved attempt needs bounded shape evidence before parser rejection loses the raw response.");
            var raw=new JObject{["outcome"]="Request",["nodes"]=new JObject{["type"]="BuildStructure",["count"]=3},
                ["PRIVATE_KEY_SENTINEL"]="PRIVATE_REPLY_SENTINEL"};
            var envelope=new JObject{["choices"]=new JArray(new JObject{["message"]=new JObject{["content"]=raw.ToString()}})};
            var safe=(JObject)describe.Invoke(null,new object[]{envelope.ToString()});
            Assert.That((string)safe["mode"],Is.EqualTo("Request"));
            var nodes=((JArray)safe["shape"]).OfType<JObject>().Single(row=>(string)row["path"]=="$.nodes");
            Assert.That((string)nodes["actualType"],Is.EqualTo("Object"));
            Assert.That(safe.ToString(),Does.Not.Contain("PRIVATE_KEY_SENTINEL"));
            Assert.That(safe.ToString(),Does.Not.Contain("PRIVATE_REPLY_SENTINEL"));
            Assert.That(((JArray)safe["shape"]).Count,Is.LessThanOrEqualTo(160));
        }

        [Test]
        public void DiagnosticShape_RetainsKnownClarificationSchemaValuesButMasksPrivateText()
        {
            var probe=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("OpenEmpires.EditorTools.CommanderPhase5CIntegrationProbe")).First(t=>t!=null);
            var raw=JObject.Parse("{\"outcome\":\"Clarify\",\"message\":\"New\",\"pending\":{\"executionOrder\":\"Sequential\",\"nodes\":[]},\"missingSlots\":[{\"node\":0,\"path\":\"quantityMode\",\"type\":\"Confirmation\",\"alternatives\":[\"TargetTotal\",\"New\"]},{\"node\":0,\"path\":\"PRIVATE_PATH_SENTINEL\",\"type\":\"PRIVATE_TYPE_SENTINEL\",\"alternatives\":[\"PRIVATE_REPLY_SENTINEL\"]}]}");
            var envelope=new JObject{["choices"]=new JArray(new JObject{["message"]=new JObject{["content"]=raw.ToString()}})};
            var safe=(JObject)probe.GetMethod("DescribeSemanticEnvelope").Invoke(null,new object[]{envelope.ToString()});
            var rows=((JArray)safe["shape"]).OfType<JObject>().ToArray();
            Func<string,string> value=path=>(string)rows.Single(row=>(string)row["path"]==path)["schemaValue"];
            Assert.That(value("$.pending.executionOrder"),Is.EqualTo("Sequential"));
            Assert.That(value("$.missingSlots[0].path"),Is.EqualTo("quantityMode"));
            Assert.That(value("$.missingSlots[0].type"),Is.EqualTo("Confirmation"));
            Assert.That(value("$.missingSlots[0].alternatives[0]"),Is.EqualTo("TargetTotal"));
            Assert.That(value("$.missingSlots[0].alternatives[1]"),Is.EqualTo("New"));
            Assert.That(value("$.missingSlots[1].path"),Is.EqualTo("<unknown>"));
            Assert.That(value("$.missingSlots[1].type"),Is.EqualTo("<unknown>"));
            Assert.That(value("$.message"),Is.Null,"Public enum text in free prose is still masked.");
            Assert.That(safe.ToString(),Does.Not.Contain("PRIVATE_"));
            Assert.That(rows.Length,Is.LessThanOrEqualTo(160));
        }

        [TestCase("{\"outcome\":\"Request\",\"nodes\":{}}","nodes","Array","Object")]
        [TestCase("{\"outcome\":\"Request\",\"nodes\":[\"unexpected\"]}","nodes[0]","Object","String")]
        [TestCase("{\"outcome\":\"DynamicPlan\",\"message\":\"not a graph\"}","message","AllowedField","String")]
        [TestCase("{\"outcome\":\"Clarify\",\"pending\":[],\"missingFields\":[\"Count\"]}","pending","Object","Array")]
        [TestCase("{\"outcome\":\"Clarify\",\"message\":{}}","message","String","Object")]
        [TestCase("{\"outcome\":\"Clarify\",\"pending\":{\"nodes\":{}},\"missingSlots\":[]}","pending.nodes","Array","Object")]
        [TestCase("{\"outcome\":\"Clarify\",\"pending\":{\"nodes\":[],\"executionOrder\":\"Independent\"},\"missingSlots\":{}}","missingSlots","Array","Object")]
        [TestCase("{\"outcome\":\"Clarify\",\"pending\":{\"nodes\":[\"PRIVATE_REPLY_SENTINEL\"],\"executionOrder\":\"Independent\"},\"missingSlots\":[{}]}","pending.nodes[0]","Object","String")]
        [TestCase("{\"outcome\":\"Clarify\",\"pending\":{\"nodes\":[{}],\"executionOrder\":\"Independent\"},\"missingSlots\":[\"PRIVATE_REPLY_SENTINEL\"]}","missingSlots[0]","Object","String")]
        [TestCase("{\"outcome\":\"Clarify\",\"pending\":{\"nodes\":[{}],\"executionOrder\":\"Independent\"},\"missingSlots\":[{\"node\":0,\"path\":\"location\",\"type\":\"Location\",\"alternatives\":{}}]}","missingSlots[0].alternatives","Array","Object")]
        [TestCase("{\"outcome\":\"Clarify\",\"pending\":{\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\",\"countMode\":\"Exact\",\"workers\":{\"state\":\"Any\"},\"destination\":{\"resource\":\"Food\"}},\"missingFields\":{}}","missingFields","Array","Object")]
        public void MalformedEnvelope_RejectsWithKnownFieldTypesInsteadOfLosingTheFirstFailure(string json,string field,string expected,string actual)
        {
            var result=CommanderSemanticJson.ParseProviderResponse(json);
            Assert.That(result.IsValid,Is.False);
            Assert.That(result.SchemaDiagnostic,Does.Contain("field="+field));
            Assert.That(result.SchemaDiagnostic,Does.Contain("expected="+expected));
            Assert.That(result.SchemaDiagnostic,Does.Contain("actual="+actual));
            Assert.That(result.SchemaDiagnostic,Does.Contain("mode="));
            Assert.That(result.SchemaDiagnostic,Does.Not.Contain("not a graph"));
            Assert.That(result.SchemaDiagnostic,Does.Not.Contain("PRIVATE_REPLY_SENTINEL"));
        }

        [TestCase("Clarify")]
        [TestCase("Answer")]
        [TestCase("Unsupported")]
        public void OverlongExplanation_ReportsKnownMessageFieldWithoutPersistingItsContent(string mode)
        {
            string value="PRIVATE_REPLY_SENTINEL"+new string('x',181);
            var result=CommanderSemanticJson.ParseProviderResponse(new JObject{["outcome"]=mode,["message"]=value}.ToString());
            Assert.That(result.IsValid,Is.False);
            Assert.That(result.SchemaDiagnostic,Does.Contain("field=message;code=out-of-range;expected=String;actual=String"));
            Assert.That(result.SchemaDiagnostic,Does.Contain("mode="+mode));
            Assert.That(result.SchemaDiagnostic,Does.Not.Contain(value));
            Assert.That(result.SchemaDiagnostic,Does.Not.Contain("PRIVATE_REPLY_SENTINEL"));
        }

        [Test]
        public void QuantityModeConfirmation_RejectsAtTheKnownSlotWithoutAdmittingItsPrefix()
        {
            const string json="{\"outcome\":\"Clarify\",\"message\":\"Choose total or new.\",\"pending\":{\"executionOrder\":\"Sequential\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1,\"builders\":{\"state\":\"Eligible\",\"count\":1}},{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4,\"producerFromNode\":0,\"dependsOn\":[0]}]},\"missingSlots\":[{\"node\":1,\"path\":\"quantityMode\",\"type\":\"Confirmation\",\"alternatives\":[\"TargetTotal\",\"New\"]}]}";
            var result=CommanderSemanticJson.ParseProviderResponse(json);
            Assert.That(result.IsValid,Is.False);
            Assert.That(result.Nodes,Is.Empty);
            Assert.That(result.PendingGraphDraft,Is.Null);
            Assert.That(result.SchemaDiagnostic,Does.Contain("field=missingSlots[0].path"));
            Assert.That(result.SchemaDiagnostic,Does.Contain("stage=clarification"));
            Assert.That(result.SchemaDiagnostic,Does.Contain("mode=Clarify"));
            Assert.That(result.SchemaDiagnostic,Does.Not.Contain("Choose total or new"));
        }

        [TestCase("Count","count","{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\"}","[4]")]
        [TestCase("Confirmation","unit","{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4}","[\"Spearman\"]")]
        [TestCase("CanonicalName","unit","{\"type\":\"EnsureUnitCount\",\"count\":4}","[\"Spearman\"]")]
        [TestCase("SourceKind","sourceKind","{\"type\":\"WatchFutureUnits\",\"unit\":\"Villager\",\"count\":4,\"producer\":\"TownCenter\",\"producerOrdinal\":1,\"action\":\"Gather\",\"resource\":\"Wood\"}","[\"Tree\"]")]
        [TestCase("Location","location","{\"type\":\"MoveUnits\",\"unitSelector\":\"Spearman\",\"count\":4}","[\"PlayerMarker\"]")]
        public void PackedClarificationSignatures_AreExecutableStrictDrafts(string type,string path,string node,string alternatives)
        {
            var config=ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var sim=new GameSimulation(config,1,new[]{0},Array.Empty<int>());
                using var manager=new CommanderGoalManager(sim,0);
                const string input="build barracks and 4 spearman";
                var request=new CommanderSemanticProviderRequest(input,new CommanderContextBuilder().Build(sim,manager,input));
                Assert.That(CommanderSemanticRequestPack.TryBuild(request,"fixture",4096,out var pack,out var reason),Is.True,reason);
                string contract=(string)JObject.Parse(pack.Json)["messages"][0]["content"];
                const string marker="Supported clarification slot paths: ";
                int offset=contract.IndexOf(marker,StringComparison.Ordinal);
                Assert.That(offset,Is.GreaterThanOrEqualTo(0),"Production contract must advertise the actual slot signatures.");
                using var reader=new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(contract.Substring(offset+marker.Length)));
                var signatures=(JObject)JToken.ReadFrom(reader);
                Assert.That(signatures[type].Values<string>(),Does.Contain(path));
                Assert.That(signatures["Confirmation"].Values<string>(),Is.EquivalentTo(new[]{"structure","unit","producer"}));
                Assert.That(signatures.Properties().SelectMany(p=>p.Value.Values<string>()),Does.Not.Contain("quantityMode"));
                var raw=new JObject{["outcome"]="Clarify",["message"]="Specify the missing detail.",
                    ["pending"]=new JObject{["executionOrder"]="Independent",["nodes"]=new JArray(JObject.Parse(node))},
                    ["missingSlots"]=new JArray(new JObject{["node"]=0,["path"]=path,["type"]=type,["alternatives"]=JArray.Parse(alternatives)})};
                var parsed=CommanderSemanticJson.ParseProviderResponse(raw.ToString());
                Assert.That(parsed.IsValid,Is.True,parsed.SchemaDiagnostic);
                Assert.That(parsed.PendingGraphDraft,Is.Not.Null);
                if(type=="Count")
                {
                    var completed=parsed.PendingGraphDraft.Resolve("4");
                    Assert.That(completed.IsValid,Is.True);
                    Assert.That(completed.Outcome,Is.EqualTo(CommanderSemanticOutcome.Request));
                    Assert.That(completed.Nodes.Single().Count,Is.EqualTo(4));
                    Assert.That(completed.Nodes.Single().QuantityMode,Is.EqualTo(CommanderProductionQuantityMode.TargetTotal));
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(config);}
        }

        [TestCase("build 3 stables", false)]
        [TestCase("build 4 houses", false)]
        [TestCase("place 3 houses near my town center", true)]
        public void ExecutableOutcomeDeclaration_OnlyAdvertisesRetrievedRepresentations(string message,bool dynamicAvailable)
        {
            var config=ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var sim=new GameSimulation(config,1,new[]{0},Array.Empty<int>());
                using var manager=new CommanderGoalManager(sim,0);
                var request=new CommanderSemanticProviderRequest(message,new CommanderContextBuilder().Build(sim,manager,message));
                Assert.That(CommanderSemanticRequestPack.TryBuild(request,"fixture",4096,out var pack,out var reason),Is.True,reason);
                string contract=(string)JObject.Parse(pack.Json)["messages"][0]["content"];
                Assert.That(contract.Contains("Outcomes are Request, DynamicPlan, Clarify, Unsupported, Answer."),Is.EqualTo(dynamicAvailable));
                Assert.That(contract.Contains("DynamicPlan is never a message-only outcome"),Is.True,"Each retrieved contract must define the DynamicPlan envelope boundary.");
            }
            finally{UnityEngine.Object.DestroyImmediate(config);}
        }

        [Test]
        public void PackedRequestedFacts_KeepNamedAvailabilityAndSupportAttachedToIdentity()
        {
            var config=ScriptableObject.CreateInstance<SimulationConfig>();
            try
            {
                var sim=new GameSimulation(config,1,new[]{0},Array.Empty<int>());
                sim.SetPlayerCivilizations(new[]{Civilization.English});
                using var manager=new CommanderGoalManager(sim,0);
                var message="build a barracks, then train 4 new spearmen from it";
                var request=new CommanderSemanticProviderRequest(message,new CommanderContextBuilder().Build(sim,manager,message));
                Assert.That(CommanderSemanticRequestPack.TryBuild(request,"fixture",4096,out var pack,out var reason),Is.True,reason);
                string user=(string)JObject.Parse(pack.Json)["messages"][1]["content"];
                var context=JObject.Parse(user.Substring(8,user.IndexOf("\nMemory:",StringComparison.Ordinal)-8));
                var unit=context["knowledge"]["units"].OfType<JObject>().SingleOrDefault(u=>(string)u["id"]=="unit:1");
                Assert.That(unit,Is.Not.Null,"Public availability must remain named data instead of a positional boolean.");
                Assert.That((bool)unit["availableForCivilization"],Is.True);
                Assert.That((bool)unit["commanderExecutable"],Is.True);
                Assert.That((string)unit["producer"],Is.EqualTo("Barracks"));
                string contract=(string)JObject.Parse(pack.Json)["messages"][0]["content"];
                Assert.That(contract,Does.Contain("\"producerFromNode\":0"),"The advertised new-producer binding needs a complete executable example.");
                Assert.That(contract,Does.Contain("\"resultFromNode\":0"),"New-unit movement needs a complete example with completion dependency.");
            }
            finally{UnityEngine.Object.DestroyImmediate(config);}
        }

        [TestCase("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":\"4\"}]}","nodes[0].count")]
        [TestCase("{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[{\"id\":\"build\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Barracks\",\"count\":\"1\"},\"inputs\":{},\"dependsOn\":[]}]}","nodes[0].parameters.count")]
        public void WrongNumericShape_ReportsKnownPathAndExpectedActualTypesWithoutValues(string json,string path)
        {
            var result=CommanderSemanticJson.ParseProviderResponse(json);
            Assert.That(result.IsValid,Is.False);
            Assert.That(result.SchemaDiagnostic,Does.Contain("field="+path));
            Assert.That(result.SchemaDiagnostic,Does.Contain("expected=Integer"));
            Assert.That(result.SchemaDiagnostic,Does.Contain("actual=String"));
            Assert.That(result.SchemaDiagnostic,Does.Not.Contain("Barracks"));
        }
    }
}
