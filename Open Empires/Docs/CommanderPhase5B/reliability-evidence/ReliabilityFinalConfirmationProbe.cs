// Controlled verification only. Compiled in memory, never imported into Assets.
// Uses existing provider/transport, separate six-attempt journal, ordinary native ticks.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenEmpires;
using OpenEmpires.TestSupport;
using UnityEngine;

public static class ReliabilityFinalConfirmationProbe
{
    static string RunId = "bc761319-d608-4c37-bc90-f14be1f59fe4";
    static string DirectoryPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs/CommanderPhase5B/reliability-evidence/final-confirmation-six");
    static EvidenceBudgetJournal journal;
    static BudgetedSemanticTransport transport;
    static OpenRouterCommanderProvider provider;
    static SimulationConfig config;
    static GameSimulation sim;
    static CommanderGoalManager manager;
    static Task<CommanderSemanticResult> pending;
    static IReadOnlyList<CommanderGoal> goals;
    static CancellationTokenSource cancellation;
    static JObject current;
    static JArray scenarios = new JArray();
    static readonly List<ICommand> commands = new List<ICommand>();
    static readonly Dictionary<int,int> births = new Dictionary<int,int>();
    static EventInfo birthEvent;
    static Delegate listener;
    static int[] workers;
    static int firstProducer;
    static bool gatherBeforeHouse;

    public static object Initialize()
    {
        if (journal != null) throw new InvalidOperationException("Probe already initialized.");
        bool extraLane=RunId=="a702e83b-c7b8-4820-8e74-a73b9718d1d3";
        journal = new EvidenceBudgetJournal(Path.Combine(DirectoryPath, "paid-usage-ledger.json"), RunId, false);
        if(journal.Snapshot().semantic_http_limit!=(extraLane?4:6))throw new InvalidOperationException("The authorized lane cap does not match.");
        var evidencePath=Path.Combine(DirectoryPath,"scenarios.json");
        if(File.Exists(evidencePath)){
            var previous=JObject.Parse(File.ReadAllText(evidencePath));
            if((string)previous["run_id"]!=RunId)throw new InvalidOperationException("Foreign evidence must be preserved.");
            scenarios=(JArray)previous["scenarios"];
            if(scenarios.Any(s=>!((string)s["stage"]).StartsWith("terminal",StringComparison.Ordinal)))throw new InvalidOperationException("Unfinished scenario must be recovered before reload.");
        }
        var inner = typeof(OpenRouterCommanderProvider).Assembly.GetType("OpenEmpires.CommanderHttpClientTransport");
        transport = new BudgetedSemanticTransport((ICommanderHttpTransport)Activator.CreateInstance(inner, true), journal);
        provider = new OpenRouterCommanderProvider(transport:transport);
        if (!(bool)typeof(OpenRouterCommanderProvider).GetProperty("HasConfiguration", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(provider,null))
            throw new InvalidOperationException("Configured credential unavailable; no request sent.");
        cancellation = new CancellationTokenSource();
        Save(); return new { initialized=true, maximum_http_attempts=journal.Snapshot().semantic_http_limit, actual_http_attempts=journal.Snapshot().semantic_initial_http+journal.Snapshot().semantic_repair_http };
    }
    public static object UseAdditionalAllowance()
    {
        if(pending!=null||goals!=null&&!goals.All(g=>g.IsTerminal))throw new InvalidOperationException("Previous scenario must be terminal.");
        var before=journal.Snapshot();
        if(before.semantic_initial_http+before.semantic_repair_http!=6)throw new InvalidOperationException("Finish the original capped lane before switching.");
        Save();CleanupFixture();cancellation.Dispose();
        RunId="a702e83b-c7b8-4820-8e74-a73b9718d1d3";
        DirectoryPath=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Docs/CommanderPhase5B/reliability-evidence/final-confirmation-four");
        journal=null;scenarios=new JArray();
        var initialized=Initialize();
        if(journal.Snapshot().semantic_http_limit!=4)throw new InvalidOperationException("Additional allowance must be capped at four.");
        return new{initialized=true,additional_maximum_http_attempts=4};
    }
    public static object Begin(string name)
    {
        if (pending != null || goals != null && !goals.All(g=>g.IsTerminal)) throw new InvalidOperationException("Previous scenario is not terminal.");
        if(name!="berries400"&&name!="only-after")throw new ArgumentException("Only the two approved confirmation wordings are allowed.");
        CleanupFixture();
        string wording;
        switch(name)
        {
            case "berries400": wording="Gather 400 additional Food from berries with four villagers."; break;
            case "age3": wording="Reach Age 3."; break;
            case "only-after": wording="Build a House first, and only after it finishes send a villager to wood."; break;
            case "any400": wording="Gather 400 additional Food."; break;
            case "first-tc": wording="Assign the next five villagers produced by my first Town Center to gather Wood."; break;
            default: throw new ArgumentException("Unknown authorized scenario.");
        }
        config=ScriptableObject.CreateInstance<SimulationConfig>(); sim=new GameSimulation(config,1,new[]{0},new int[0]);
        sim.SetPlayerCivilizations(new[]{Civilization.English});
        int x=sim.MapData.Width/2,z=sim.MapData.Height/2;
        typeof(MapData).GetField("holeMap",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(sim.MapData,null);
        foreach(var node in sim.MapData.GetAllResourceNodes())node.RemainingAmount=0;
        for(int a=x-25;a<=x+25;a++)for(int b=z-25;b<=z+25;b++)
        {sim.MapData.Tiles[a,b]=TileType.Grass;sim.MapData.ForestDensity[a,b]=0;sim.MapData.FoundationCount[a,b]=0;sim.FogOfWar.SetVisible(0,a,b);}
        var tc=sim.CreateBuilding(0,BuildingType.TownCenter,x,z,false,true); tc.AutoProduceVillagers=false;firstProducer=tc.Id;
        workers=Enumerable.Range(0,4).Select(i=>{
            var w=sim.UnitRegistry.CreateUnit(0,sim.MapData.TileToWorldFixed(x+5+i,z+5),Fixed32.FromFloat(2),Fixed32.FromFloat(.4f),Fixed32.One);
            w.IsVillager=true;w.UnitType=0;w.State=UnitState.Idle;w.CurrentHealth=w.MaxHealth=100;return w.Id;
        }).ToArray();
        sim.MapData.AddResourceNode(ResourceType.Food,sim.MapData.TileToWorldFixed(x+6,z+8),150);
        sim.MapData.AddResourceNode(ResourceType.Food,sim.MapData.TileToWorldFixed(x+17,z+8),1000);
        sim.MapData.AddResourceNode(ResourceType.Wood,sim.MapData.TileToWorldFixed(x+15,z+10),10000);
        if(name=="any400"){
            var sheep=sim.UnitRegistry.CreateUnit(0,sim.MapData.TileToWorldFixed(x+6,z+6),Fixed32.One,Fixed32.One,Fixed32.One);
            sheep.IsSheep=true;sheep.CurrentHealth=sheep.MaxHealth=100;sheep.State=UnitState.Idle;
        }
        if(name=="first-tc")sim.CreateBuilding(0,BuildingType.TownCenter,x+15,z-15,false,true).AutoProduceVillagers=false;
        var resources=sim.ResourceManager.GetPlayerResources(0);resources.Wood=resources.Gold=10000;
        resources.Food=(name=="age3"||name=="first-tc")?10000:0;
        manager=new CommanderGoalManager(sim,0);commands.Clear();births.Clear();goals=null;gatherBeforeHouse=false;
        sim.CommandBuffer.CommandEnqueued+=Capture;
        birthEvent=typeof(GameSimulation).GetEvent("ProducerUnitProduced",BindingFlags.NonPublic|BindingFlags.Instance);
        var parameter=Expression.Parameter(birthEvent.EventHandlerType.GetMethod("Invoke").GetParameters()[0].ParameterType);
        Action<object> observe=value=>{
            const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
            births[(int)value.GetType().GetProperty("UnitId",flags).GetValue(value,null)]=(int)value.GetType().GetProperty("ProducerId",flags).GetValue(value,null);
        };
        listener=Expression.Lambda(birthEvent.EventHandlerType,Expression.Call(Expression.Constant(observe),typeof(Action<object>).GetMethod("Invoke"),Expression.Convert(parameter,typeof(object))),parameter).Compile();
        birthEvent.GetAddMethod(true).Invoke(sim,new object[]{listener});
        current=new JObject{["scenario"]=name,["wording"]=wording,["stage"]="http-pending",["raw_response_omitted"]=true};scenarios.Add(current);Save();
        var request=new CommanderSemanticProviderRequest(wording,new CommanderContextBuilder().Build(sim,manager,wording));
        current["context_projection"]= "Unchanged bounded production projection; public canonical IDs, no runtime entity IDs or coordinates";Save();
        pending=Translate(request);return new { started=true, scenario=name };
    }
    static async Task<CommanderSemanticResult> Translate(CommanderSemanticProviderRequest request)
    {using(transport.BeginSubmission())return await provider.TranslateSemanticAsync(request,cancellation.Token);}
    public static object Poll()
    {
        if(pending!=null){
            if(!pending.IsCompleted)return new {stage="http-pending",attempts=journal.Snapshot().semantic_initial_http+journal.Snapshot().semantic_repair_http};
            CommanderSemanticResult result;
            try{result=pending.GetAwaiter().GetResult();}catch(Exception error){current["error_type"]=error.GetType().Name;current["stage"]="terminal-error";pending=null;Save();return current;}
            pending=null;current["valid"]=result.IsValid;current["outcome"]=result.Outcome.ToString();current["trace"]=provider.LastRequestTrace;
            current["schema_diagnostic"]=result.SchemaDiagnostic;current["node_count"]=result.Nodes.Count;
            current["safe_explanation"]=result.SafeExplanation;
            if(result.PendingDraft!=null){var d=result.PendingDraft;current["pending_draft"]=new JObject{
                ["mode"]=d.Mode.ToString(),["count_mode"]=d.CountMode.ToString(),["count"]=d.Count,["worker_state"]=d.Workers.State.ToString(),
                ["amount"]=d.ResourceAmount,["amount_mode"]=d.ResourceAmountMode.ToString(),
                ["resource"]=d.Destination==null?null:d.Destination.Resource.ToString(),["source"]=d.Destination==null?null:d.Destination.SourceKind.ToString(),
                ["missing_fields"]=new JArray(d.MissingFields.Select(f=>f.ToString()))};}
            if((string)current["scenario"]=="any400"&&result.IsValid&&result.PendingDraft!=null&&result.PendingDraft.MissingFields.SequenceEqual(new[]{CommanderClarificationField.Count})){
                int count;if(!CommanderClarificationReplies.TryParseCount("4",out count))throw new InvalidOperationException("Local reply failed.");
                var updated=typeof(CommanderWorkerAllocationDraft).GetMethod("WithCount",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(result.PendingDraft,new object[]{count});
                result=(CommanderSemanticResult)typeof(CommanderWorkerAllocationDraft).GetMethod("CompleteResult",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(updated,null);
                current["local_count_reply"]="4";current["continuation_valid"]=result.IsValid;
            }
            var normalized=new JArray();foreach(var node in result.Nodes){
                var n=new JObject{["type"]=node.Type.ToString(),["count"]=node.Count,["age"]=node.AgeTarget.ToString(),["producer_ordinal"]=node.ProducerOrdinal,["depends_on"]=new JArray(node.DependsOn)};
                if(node.WorkerAllocation!=null){var a=node.WorkerAllocation;n["allocation_mode"]=a.Mode.ToString();n["worker_count"]=a.Count;n["worker_state"]=a.Workers.State.ToString();n["source"]=a.Destination.SourceKind.ToString();n["resource"]=a.Destination.Resource.ToString();n["amount"]=a.ResourceAmount;n["amount_mode"]=a.ResourceAmountMode.ToString();}
                normalized.Add(n);
            }current["nodes"]=normalized;
            if(!result.IsValid||result.Outcome!=CommanderSemanticOutcome.Request){current["stage"]="terminal-not-admitted";Save();return current;}
            current["semantics_match_request"]=ExpectedSemantics(result);
            if(!(bool)current["semantics_match_request"]){current["stage"]="terminal-semantic-mismatch";Save();return current;}
            const BindingFlags internalMethods=BindingFlags.Instance|BindingFlags.NonPublic;
            var scope=typeof(CommanderGoalManager).GetMethod("PrepareActionPlan",internalMethods).Invoke(manager,new object[]{result,(string)current["wording"],0,null,null,null});
            current["commands_before_approval"]=commands.Count;current["goals_before_approval"]=manager.Goals.Count;
            var approved=typeof(CommanderGoalManager).GetMethod("ApproveActionPlan",internalMethods).Invoke(manager,new object[]{scope,0,null});
            goals=(IReadOnlyList<CommanderGoal>)typeof(CommanderGoalManager).GetMethod("SubmitSemanticGraph",BindingFlags.Instance|BindingFlags.Public).Invoke(manager,new object[]{approved,36000});
            current["stage"]="native";
            if((string)current["scenario"]=="first-tc"){
                for(int i=0;i<5;i++)sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(0,firstProducer,0));
                var second=sim.BuildingRegistry.GetAllBuildings().Single(b=>b.Type==BuildingType.TownCenter&&b.Id!=firstProducer);
                sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(0,second.Id,0));
            }
        }
        if(goals!=null){
            for(int i=0;i<1000&&!goals.All(g=>g.IsTerminal)&&sim.CurrentTick<30000;i++){manager.Tick(sim.CurrentTick);sim.Tick();}
            current["tick"]=sim.CurrentTick;current["statuses"]=new JArray(goals.Select(g=>g.Status.ToString()));
            if(goals.All(g=>g.IsTerminal)||sim.CurrentTick>=30000){
                current["stage"]="terminal-native";current["native_age"]=sim.GetPlayerAge(0);current["credited_food"]=sim.ResourceManager.GetGatheredIncome(0,ResourceType.Food);
                current["commander_train_count"]=commands.OfType<TrainUnitCommand>().Count();current["placement_count"]=commands.OfType<PlaceBuildingCommand>().Count();
                current["gather_before_house_completion"]=gatherBeforeHouse;current["gather_unit_ids"]=new JArray(commands.OfType<GatherCommand>().SelectMany(c=>c.UnitIds).Distinct().OrderBy(id=>id));
                current["gather_source_kinds"]=new JArray(commands.OfType<GatherCommand>().Select(c=>c.SourceKind.ToString()).Distinct());
                current["original_worker_ids"]=new JArray(workers);current["first_tc_birth_ids"]=new JArray(births.Where(b=>b.Value==firstProducer).Select(b=>b.Key).OrderBy(id=>id));
                current["competing_birth_ids"]=new JArray(births.Where(b=>b.Value!=firstProducer).Select(b=>b.Key).OrderBy(id=>id));
                current["house_complete_count"]=sim.BuildingRegistry.GetAllBuildings().Count(b=>b.Type==BuildingType.House&&!b.IsUnderConstruction&&!b.IsDestroyed);
                bool matches=goals.Count>0&&goals.All(g=>g.Status==CommanderGoalStatus.Completed);
                var assigned=commands.OfType<GatherCommand>().SelectMany(c=>c.UnitIds).Distinct().OrderBy(id=>id).ToArray();
                string name=(string)current["scenario"];
                if(name=="age3")matches=matches&&sim.GetPlayerAge(0)==3&&commands.OfType<PlaceBuildingCommand>().Count()==2;
                else if(name=="only-after")matches=matches&&!gatherBeforeHouse&&(int)current["house_complete_count"]==1&&assigned.Length==1&&workers.Contains(assigned[0])&&commands.OfType<TrainUnitCommand>().Count()==0;
                else if(name=="first-tc")matches=matches&&assigned.Length==5&&assigned.SequenceEqual(births.Where(b=>b.Value==firstProducer).Select(b=>b.Key).OrderBy(id=>id))&&births.Any(b=>b.Value!=firstProducer)&&commands.OfType<TrainUnitCommand>().Count()==0;
                else matches=matches&&sim.ResourceManager.GetGatheredIncome(0,ResourceType.Food)>=400&&assigned.All(workers.Contains)&&commands.OfType<TrainUnitCommand>().Count()==0
                    &&(name!="berries400"||commands.OfType<GatherCommand>().All(c=>c.SourceKind==ResourceSourceKind.Berries)&&!commands.OfType<SlaughterSheepCommand>().Any());
                current["native_matches_request"]=matches;
            }Save();
        }return current;
    }
    static void Capture(ICommand command,CommandEnqueueSource source)
    {if(source!=CommandEnqueueSource.Commander)return;commands.Add(command);if((string)current["scenario"]=="only-after"&&command is GatherCommand&&!sim.BuildingRegistry.GetAllBuildings().Any(b=>b.Type==BuildingType.House&&!b.IsUnderConstruction&&!b.IsDestroyed))gatherBeforeHouse=true;}
    static void Save(){File.WriteAllText(Path.Combine(DirectoryPath,"scenarios.json"),new JObject{["schema"]="phase5b-reliability-controlled-live@1",["run_id"]=RunId,["method"]="Direct real semantic provider, explicit test game-side approval, native commands/ticks; not UGUI or standalone acceptance",["scenarios"]=scenarios}.ToString(Formatting.Indented));}
    static void RemoveIdentifierFields(JToken token){var obj=token as JObject;if(obj!=null)foreach(var p in obj.Properties().ToArray()){
        if(p.Name.Equals("id",StringComparison.OrdinalIgnoreCase)||p.Name.EndsWith("Id",StringComparison.Ordinal)||p.Name.EndsWith("Ids",StringComparison.Ordinal))p.Remove();else RemoveIdentifierFields(p.Value);
    }else if(token is JArray)foreach(var item in (JArray)token)RemoveIdentifierFields(item);}
    static bool ExpectedSemantics(CommanderSemanticResult result){
        var nodes=result.Nodes;string name=(string)current["scenario"];
        if(name=="age3")return nodes.Count==1&&nodes[0].Type==CommanderSemanticNodeType.ReachAge&&nodes[0].AgeTarget==CommanderSemanticAgeTarget.Castle;
        if(name=="first-tc")return nodes.Count==1&&nodes[0].Type==CommanderSemanticNodeType.WatchFutureUnits&&nodes[0].UnitType==0&&nodes[0].Count==5
            &&nodes[0].BuildingType==BuildingType.TownCenter&&nodes[0].ProducerOrdinal==1&&nodes[0].FutureAction==CommanderFutureUnitAction.Gather&&nodes[0].ResourceType==ResourceType.Wood;
        if(name=="only-after")return nodes.Count==2&&nodes[0].Type==CommanderSemanticNodeType.BuildStructure&&nodes[0].BuildingType==BuildingType.House&&nodes[0].Count==1
            &&nodes[1].Type==CommanderSemanticNodeType.AllocateWorkers&&nodes[1].WorkerAllocation.Count==1&&nodes[1].WorkerAllocation.Mode==CommanderWorkerAllocationMode.SelectedCount
            &&nodes[1].WorkerAllocation.Destination.Resource==ResourceType.Wood&&nodes[1].DependsOn.Contains(0)&&!nodes[1].ResultFromNode.HasValue;
        if(nodes.Count!=1||nodes[0].Type!=CommanderSemanticNodeType.AllocateWorkers)return false;
        var allocation=nodes[0].WorkerAllocation;
        return allocation.Mode==CommanderWorkerAllocationMode.SelectedCount&&allocation.CountMode==CommanderWorkerCountMode.Exact&&allocation.Count==4
            &&allocation.Destination.Resource==ResourceType.Food&&allocation.ResourceAmount==400&&allocation.ResourceAmountMode==CommanderResourceAmountMode.AdditionalGathered
            &&allocation.Destination.SourceKind==(name=="berries400"?ResourceSourceKind.Berries:ResourceSourceKind.Any);
    }
    static void CleanupFixture(){if(sim!=null){sim.CommandBuffer.CommandEnqueued-=Capture;if(listener!=null)birthEvent.GetRemoveMethod(true).Invoke(sim,new object[]{listener});}manager?.Dispose();if(config!=null)UnityEngine.Object.DestroyImmediate(config);manager=null;sim=null;config=null;goals=null;listener=null;}
    public static object Finish(){if(pending!=null)throw new InvalidOperationException("HTTP still pending.");Save();CleanupFixture();cancellation.Dispose();return journal.Snapshot();}
}
