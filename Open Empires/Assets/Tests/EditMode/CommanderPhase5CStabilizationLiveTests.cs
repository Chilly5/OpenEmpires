using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using OpenEmpires.TestSupport;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    // Opt-in, one bounded six-HTTP matrix. Production context/packer/transport/parser;
    // semantic compilation only, never normal gameplay or package certification.
    [Category("CommanderLiveProvider")]
    public sealed class CommanderPhase5CStabilizationLiveTests
    {
        internal const string OptIn="OpenEmpires.Phase5C.Stabilization.AllowOneRun";
        internal const string FollowupOptIn="OpenEmpires.Phase5C.Stabilization.AllowFollowup";
        private const string RunId="b62221d2-9d39-4b78-90c2-3dfdb1f0622f";
        private sealed class DebitTransport:ICommanderHttpTransport
        {
            private readonly string path;
            private readonly string source;
            private readonly ICommanderHttpTransport inner=new CommanderHttpClientTransport();
            internal DebitTransport(string path,string source){this.path=path;this.source=source;}
            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri,string body,IReadOnlyDictionary<string,string> headers,CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                using(var lease=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
                {
                    var ledger=JObject.Parse(File.ReadAllText(path));
                    int used=(int)ledger["usedHttpAttempts"],limit=(int)ledger["maximumHttpAttempts"];
                    if(used>=limit)throw new InvalidOperationException("The existing paid allowance is exhausted.");
                    ledger["usedHttpAttempts"]=used+1;
                    ((JArray)ledger["attempts"]).Add(new JObject{["source"]=source,
                        ["kind"]="stabilization-reserved-http",["utc"]=DateTime.UtcNow.ToString("O"),
                        ["result"]="Consumed before dispatch; exact attempt kind and terminal status recorded in scoped journal."});
                    File.WriteAllText(path,ledger.ToString(Formatting.Indented)+"\n");
                }
                return inner.PostJsonAsync(uri,body,headers,token);
            }
        }

        [UnityTest]
        public IEnumerator SixReportedWordings_UseRealProviderAndProductionContext()
            => RunMatrix(OptIn,RunId,"stabilization-live",6,new[]{"Build house","Can my civilization train Longbowmen?","Build 3 archery",
                "Build 3 stables","Build 50 spearman out of 5 barracks","Build a Mill and 8 Farms around it"});

        [UnityTest]
        public IEnumerator ThreeFailedWordings_FollowupKeepsOriginalBudgetAndExactBindings()
            => RunMatrix(FollowupOptIn,"860310ca-07af-4f9c-a670-855d96ff8ab6","stabilization-live-followup",3,
                new[]{"Build 3 archery","Build 50 spearman out of 5 barracks","Build a Mill and 8 Farms around it"});

        private IEnumerator RunMatrix(string optIn,string runId,string folder,int limit,string[] messages)
        {
            if(!UnityEditor.SessionState.GetBool(optIn,false))Assert.Ignore("Explicit one-run paid opt-in required.");
            UnityEditor.SessionState.SetBool(optIn,false);
            string root=Path.GetFullPath("Docs/CommanderPhase5C/evidence");
            string output=Path.Combine(root,folder);Directory.CreateDirectory(output);
            string ledgerPath=Path.Combine(output,"paid-usage-ledger.json");
            bool existed=File.Exists(ledgerPath);
            var journal=new EvidenceBudgetJournal(ledgerPath,runId,true);
            if(!existed&&limit<6)
            {
                var fresh=JObject.Parse(File.ReadAllText(ledgerPath));fresh["semantic_http_limit"]=limit;fresh["live_submission_limit"]=limit;
                File.WriteAllText(ledgerPath,fresh.ToString(Formatting.Indented)+"\n");
            }
            var transport=new BudgetedSemanticTransport(new DebitTransport(Path.Combine(root,"paid-attempt-ledger.json"),"evidence/"+folder+"/paid-usage-ledger.json"),journal);
            var provider=new OpenRouterCommanderProvider(transport:transport);
            var config=ScriptableObject.CreateInstance<SimulationConfig>();
            var simulation=new GameSimulation(config,2,new[]{0,1},Array.Empty<int>());
            simulation.SetPlayerCivilizations(new[]{Civilization.English,Civilization.French});
            simulation.SetPlayerAge(0,3);
            using var manager=new CommanderGoalManager(simulation,0);
            var records=new JArray();var failures=new List<string>();
            try
            {
                foreach(string message in messages)
                {
                    bool question=message.StartsWith("Can ",StringComparison.Ordinal);
                    var context=new CommanderContextBuilder().Build(simulation,manager,message);
                    var request=new CommanderSemanticProviderRequest(message,context,readOnlyQuestion:question);
                    var record=new JObject{["utterance"]=message,["stage"]="provider-request",["owner"]=context.PlayerId,
                        ["age"]=context.Age,["civilization"]=context.Civilization};records.Add(record);
                    void Save()=>File.WriteAllText(Path.Combine(output,"results.json"),records.ToString(Formatting.Indented)+"\n");
                    Save();
                    CommanderSemanticResult result=null;
                    using(var cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                    using(transport.BeginSubmission())
                    {
                        var task=provider.TranslateSemanticAsync(request,cancellation.Token);
                        while(!task.IsCompleted)yield return null;
                        if(task.IsFaulted){record["stage"]="transport-fault";failures.Add(message);Save();continue;}
                        if(task.IsCanceled){record["stage"]="cancelled";failures.Add(message);Save();continue;}
                        result=task.Result;
                    }
                    record["valid"]=result.IsValid;record["outcome"]=result.Outcome.ToString();
                    record["schemaDiagnostic"]=result.SchemaDiagnostic;
                    record["explanation"]=result.SafeExplanation;
                    record["httpStatus"]=transport.LastHttpStatus;
                    record["actualPromptTokens"]=provider.LastUsage.PromptTokens.HasValue?(JToken)provider.LastUsage.PromptTokens.Value:JValue.CreateNull();
                    record["inputUtf8Estimate"]=provider.LastInputEstimate;
                    record["attempts"]=new JArray(provider.LastSemanticAttempts.Select(a=>new JObject{
                        ["kind"]=a.Kind,["stage"]=a.ResultStage,["httpStatus"]=a.HttpStatusCode,
                        ["actualPromptTokens"]=a.Usage.PromptTokens.HasValue?(JToken)a.Usage.PromptTokens.Value:JValue.CreateNull(),["elapsedMs"]=a.ElapsedMilliseconds}));
                    record["nodes"]=new JArray(result.Nodes.Select(n=>new JObject{["type"]=n.Type.ToString(),
                        ["unitType"]=n.UnitType,["buildingType"]=n.BuildingType?.ToString(),["count"]=n.Count,
                        ["quantityMode"]=n.QuantityMode.ToString(),["producerFromNode"]=n.ProducerFromNode,["dependsOn"]=new JArray(n.DependsOn)}));
                    record["dynamicNodes"]=new JArray(result.DynamicPlan?.Nodes.Select(n=>new JObject{
                        ["mechanic"]=n.Primitive.Id,["parameters"]=JObject.FromObject(n.Parameters),
                        ["inputs"]=JObject.FromObject(n.Inputs),["dependsOn"]=new JArray(n.DependsOn)})??Enumerable.Empty<JObject>());
                    bool executable=result.IsValid&&(result.Outcome==CommanderSemanticOutcome.Request||result.Outcome==CommanderSemanticOutcome.DynamicPlan);
                    if(executable)
                    {
                        bool compiled=manager.TryCompileActionPlan(result,out var graph,out var reason);
                        record["compiled"]=compiled;record["compileReason"]=reason;record["stage"]=compiled?"compiled-preview-only":"compilation-rejected";
                        if(!compiled)failures.Add(message);
                        if(message.Contains("out of")&&result.DynamicPlan?.Nodes.Any(n=>n.Primitive.Mechanic==CommanderDynamicMechanic.SelectStructures&&n.Parameter<int>("count")==5)!=true)
                        {failures.Add(message+" lost producer count");record["stage"]="producer-restriction-lost";}
                        if(message.Contains("around")&&!HasExactNewAnchor(result))
                        {failures.Add(message+" lost exact new anchor");record["stage"]="exact-anchor-restriction-lost";}
                    }
                    else
                    {
                        record["stage"]=question&&result.IsValid&&result.Outcome==CommanderSemanticOutcome.Answer?"read-only-answer":"meaning-not-executable";
                        if(!question||result.Outcome!=CommanderSemanticOutcome.Answer)failures.Add(message);
                    }
                    Save();
                }
                Assert.That(manager.Goals,Is.Empty,"Live semantics/compilation creates no gameplay authority.");
                Assert.That(failures,Is.Empty,string.Join("; ",failures));
            }
            finally{UnityEngine.Object.DestroyImmediate(config);}
        }

        private static bool HasExactNewAnchor(CommanderSemanticResult result)
        {
            if(result.DynamicPlan==null)return false;
            var nodes=result.DynamicPlan.Nodes;
            var mill=nodes.FirstOrDefault(n=>n.Primitive.Mechanic==CommanderDynamicMechanic.Build&&n.Parameter<string>("building")=="building:Mill");
            var farm=nodes.FirstOrDefault(n=>n.Primitive.Mechanic==CommanderDynamicMechanic.Build&&n.Parameter<string>("building")=="building:Farm");
            if(mill==null||farm==null||!farm.Inputs.TryGetValue("location",out string siteId))return false;
            var site=nodes.SingleOrDefault(n=>n.Id==siteId);
            return site?.Primitive.Mechanic==CommanderDynamicMechanic.ResolveLocation
                &&site.Inputs.TryGetValue("structures",out string source)&&source==mill.Id
                &&site.DependsOn.Contains(mill.Id)&&farm.DependsOn.Contains(site.Id);
        }
    }
}
