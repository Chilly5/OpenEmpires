// Verification-only instrumentation. Compiled in memory, never imported into Assets.
// Observes the real Editor match and forwards unchanged production provider/commands.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenEmpires;
using UnityEngine;
using UnityEditor;

public static class Phase5BReplayProbe
{
    public static GameSimulation Sim;
    public static CommanderGoalManager Manager;
    public static CommanderChatUI Chat;
    public static OpenRouterCommanderProvider Provider;
    public static CountedTransport Transport;
    public static JObject Evidence = new JObject();
    public static JObject Current;
    static readonly object Gate = new object();
    static int baselineGoal, lastTick;
    static string path;
    static string previous;
    public static object Attach()
    {
        var b = GameBootstrapper.Instance;
        Sim = b.Simulation; Manager = b.Commander; Chat = CommanderChatUI.Instance;
        path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs/CommanderPhase5B/verification/replay-live-evidence.json");
        if (File.Exists(path)) throw new InvalidOperationException("Preserve existing replay evidence.");
        var innerType = typeof(OpenRouterCommanderProvider).Assembly.GetType("OpenEmpires.CommanderHttpClientTransport");
        Transport = new CountedTransport((ICommanderHttpTransport)Activator.CreateInstance(innerType, true));
        Provider = new OpenRouterCommanderProvider(transport: Transport);
        bool configured = (bool)typeof(OpenRouterCommanderProvider).GetProperty("HasConfiguration", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Provider, null);
        if (!configured) throw new InvalidOperationException("Provider configuration unavailable.");
        Evidence = new JObject { ["schema"]="phase5b-independent-replay@1", ["authorized_attempt_limit"]=100,
            ["source_sha256"]="beabf07553a67113b82833cffaa0cb2cc6b7e7690907d254ce326952a7bb06cb",
            ["method"]="Controlled Editor match; actual Commander UI; unchanged production HTTP transport and native commands",
            ["http_attempts"]=new JArray(), ["scenarios"]=new JArray() };
        Chat.Initialize(Provider, Sim, Manager, b.CommanderDispatcher);
        Sim.CommandBuffer.CommandEnqueued += ObserveCommand;
        EditorApplication.update += Sample;
        Save();
        return new { configured=true, tick=Sim.CurrentTick, expanded=Chat.IsExpanded };
    }
    public static object Begin(string name, string wording)
    {
        if (Current != null) Capture();
        baselineGoal = Manager.Goals.Count == 0 ? 0 : Manager.Goals.Max(g=>g.GoalId);
        Chat.ResetConversation();
        Current = new JObject { ["scenario"]=name, ["wording"]=wording, ["start_tick"]=Sim.CurrentTick,
            ["expanded_at_start"]=Chat.IsExpanded, ["worker_snapshot"]=Workers(), ["normalized_attempts"]=new JArray(),
            ["commands"]=new JArray(), ["samples"]=new JArray() };
        ((JArray)Evidence["scenarios"]).Add(Current); previous=null; Capture();
        return Current["worker_snapshot"];
    }
    public static JArray Workers()
    {
        return new JArray(Sim.UnitRegistry.GetAllUnits().Where(u=>u.PlayerId==Manager.PlayerId && u.IsVillager).Select(u=>new JObject {
            ["id"]=u.Id,["state"]=u.State.ToString(),["health"]=u.CurrentHealth,["resource_target"]=u.TargetResourceNodeId,
            ["construction_target"]=u.ConstructionTargetBuildingId,["queued"]=u.CommandQueue.Count }));
    }
    static void ObserveCommand(ICommand command, CommandEnqueueSource source)
    {
        if (Current == null) return;
        var record = new JObject { ["tick"]=Sim.CurrentTick,["type"]=command.GetType().Name,["source"]=source.ToString() };
        if (command is PlaceBuildingCommand)
        {
            var build=(PlaceBuildingCommand)command;
            record["building"]=build.BuildingType.ToString();record["tile_x"]=build.TileX;record["tile_z"]=build.TileZ;
            record["builders"]=new JArray(build.VillagerUnitIds ?? new int[0]);
        }
        lock(Gate) { ((JArray)Current["commands"]).Add(record); Save(); }
    }
    static void Sample()
    {
        if (Sim==null || !EditorApplication.isPlaying || Current==null || Sim.CurrentTick-lastTick<15) return;
        lastTick=Sim.CurrentTick; Capture();
    }
    static string Text(string relative)
    {
        var target=Chat.transform.Find(relative);
        if(target==null)return "unavailable";
        var component=target.GetComponents<Component>().FirstOrDefault(c=>c!=null && c.GetType().Name=="TextMeshProUGUI");
        return component==null?"unavailable":(string)component.GetType().GetProperty("text").GetValue(component,null);
    }
    public static object Capture()
    {
        var goals=new JArray(Manager.Goals.Where(g=>g.GoalId>baselineGoal).Select(g=>new JObject {
            ["id"]=g.GoalId,["type"]=g.GoalType.ToString(),["status"]=g.Status.ToString(),["reason"]=g.StatusReason,
            ["placed_id"]=g is BuildStructureGoal?((BuildStructureGoal)g).PlacedBuildingId:-1,
            ["anchor"]=g is BuildStructureGoal?((BuildStructureGoal)g).PlacementAnchorSelector.ToString():null,
            ["source_kind"]=g is BuildStructureGoal?((BuildStructureGoal)g).PlacementSourceKind.ToString():null,
            ["idle_only"]=g.UseIdleWorkersOnly }));
        var buildings=new JArray(Sim.BuildingRegistry.GetAllBuildings().Where(b=>b.PlayerId==Manager.PlayerId).Select(b=>new JObject {
            ["id"]=b.Id,["type"]=b.Type.ToString(),["under_construction"]=b.IsUnderConstruction,["destroyed"]=b.IsDestroyed,
            ["x"]=b.OriginTileX,["z"]=b.OriginTileZ }));
        var board=CommanderTaskBoardProjection.Capture(Manager,null,Chat.Conversation==null?0:0);
        var cards=new JArray(board.Cards.Select(c=>new JObject { ["request_id"]=c.RequestId,["status"]=c.Status.ToString(),["progress"]=c.Progress,["blocker"]=c.Blocker }));
        var data=new JObject { ["goals"]=goals,["buildings"]=buildings,["cards"]=cards,["approval_pending"]=Chat.PendingActionPlan!=null,
            ["expanded"]=Chat.IsExpanded,["top_status"]=Text("CommanderCanvas/Panel/CommanderStatus"),
            ["first_card_row"]=Text("CommanderCanvas/Panel/TaskBoard/Viewport/Content/TaskCard/Status") };
        lock(Gate)
        {
            if(Current!=null)
            {
                string fingerprint=data.ToString(Formatting.None);
                if(fingerprint!=previous) { data["tick"]=Sim.CurrentTick; ((JArray)Current["samples"]).Add(data);previous=fingerprint;Save(); }
            }
        }
        return data;
    }
    static JArray Normalize(CommanderSemanticResult result)
    {
        var nodes=new JArray();
        if(result==null || !result.IsValid)return nodes;
        foreach(var n in result.Nodes) nodes.Add(new JObject {
            ["type"]=n.Type.ToString(),["building"]=n.BuildingType.ToString(),["count"]=n.Count,
            ["anchor"]=n.PlacementAnchorSelector.ToString(),["resource"]=n.ResourceType.ToString(),
            ["source_kind"]=n.SourceKind.ToString(),["relation"]=n.PlacementRelation.ToString(),
            ["constraints"]=new JArray(n.Constraints.Select(c=>c is PreferredWorkersConstraint?"PreferredWorkers:IdleOnly":c is ProtectedResourceConstraint?"ProtectedResource:"+((ProtectedResourceConstraint)c).Resource:c.Type.ToString())),
            ["depends_on"]=new JArray(n.DependsOn) });
        return nodes;
    }
    static void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var data=Evidence.ToString(Formatting.Indented);
        File.WriteAllText(path,data,new System.Text.UTF8Encoding(false));
    }
    public static object Detach()
    {
        Capture();EditorApplication.update-=Sample;Sim.CommandBuffer.CommandEnqueued-=ObserveCommand;
        return new { attempts=Transport.Attempts,scenarios=((JArray)Evidence["scenarios"]).Count };
    }
    public sealed class CountedTransport:ICommanderHttpTransport
    {
        readonly ICommanderHttpTransport inner;
        public int Attempts;
        public CountedTransport(ICommanderHttpTransport inner){this.inner=inner;}
        public async Task<CommanderHttpResponse> PostJsonAsync(Uri uri,string json,IReadOnlyDictionary<string,string> headers,CancellationToken token)
        {
            JObject attempt;
            lock(Gate)
            {
                if(Attempts>=100)throw new InvalidOperationException("Authorized replay budget exhausted.");
                Attempts++;
                var model=(string)JObject.Parse(json)["model"];
                attempt=new JObject { ["ordinal"]=Attempts,["scenario"]=Current==null?"unavailable":Current["scenario"].DeepClone(),
                    ["requested_model"]=model,["endpoint"]=uri.Host+uri.AbsolutePath,["status"]="reserved" };
                ((JArray)Evidence["http_attempts"]).Add(attempt);Save();
            }
            try
            {
                var response=await inner.PostJsonAsync(uri,json,headers,token).ConfigureAwait(false);
                var envelope=JObject.Parse(response.Body);
                string content=(string)envelope["choices"]?[0]?["message"]?["content"];
                var parsed=CommanderSemanticJson.Parse(content??"");
                lock(Gate)
                {
                    attempt["http_status"]=response.StatusCode;attempt["status"]="received";
                    var returned=(string)envelope["model"];attempt["returned_model"]=returned!=null&&returned.Length<=100?returned:"unavailable";
                    if(Current!=null)((JArray)Current["normalized_attempts"]).Add(new JObject {
                        ["attempt"]=Attempts,["valid"]=parsed.IsValid,["outcome"]=parsed.Outcome.ToString(),["nodes"]=Normalize(parsed) });
                    Save();
                }
                return response;
            }
            catch(Exception e) { lock(Gate){attempt["status"]="error";attempt["error_type"]=e.GetType().Name;Save();}throw; }
        }
    }
}
