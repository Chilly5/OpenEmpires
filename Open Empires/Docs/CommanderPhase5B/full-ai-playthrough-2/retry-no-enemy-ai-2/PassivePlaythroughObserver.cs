// Diagnostic observer only. Compiled in memory outside Assets.
// No Send callback, command enqueue, admission, provider replacement, simulation tick,
// unit creation, resource mutation or gameplay settings mutation.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenEmpires;
using UnityEditor;
using UnityEngine;

public static class PassivePlaythrough2NoEnemySecondObserver200
{
    static GameSimulation sim;
    static CommanderGoalManager manager;
    static CommanderChatUI chat;
    static OpenRouterCommanderProvider provider;
    static string path, fingerprint;
    static double nextSample;
    static int cap;
    static readonly HashSet<string> observedHttp = new HashSet<string>();
    static JObject journal;

    public static object Attach(int authorizedHttpCap)
    {
        if (authorizedHttpCap < 1 || authorizedHttpCap > 200) throw new ArgumentOutOfRangeException("authorizedHttpCap");
        var bootstrap = GameBootstrapper.Instance;
        if (bootstrap == null || bootstrap.Simulation == null) throw new InvalidOperationException("A real match must already exist.");
        path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Docs/CommanderPhase5B/full-ai-playthrough-2/retry-no-enemy-ai-2/passive-evidence.json");
        if (File.Exists(path)) throw new InvalidOperationException("Preserve existing evidence.");
        sim = bootstrap.Simulation; manager = bootstrap.Commander; chat = CommanderChatUI.Instance;
        provider = typeof(CommanderChatUI).GetField("semanticProvider", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(chat) as OpenRouterCommanderProvider;
        if (provider == null) throw new InvalidOperationException("Configured Luna provider is unavailable.");
        cap = authorizedHttpCap;
        journal = new JObject { ["method"]="Normal UI match; passive observer; no gameplay mutation", ["authorized_http_cap"]=cap,
            ["source_sha256"]="3059cfbf9ef7189e05c60b05fe4ee17cd00ffbb7b21e24dd4782517b45055676",
            ["start_utc"]=DateTime.UtcNow.ToString("O"), ["start_tick"]=sim.CurrentTick,
            ["observed_http_attempts"]=new JArray(), ["notes"]=new JArray(), ["commands"]=new JArray(), ["samples"]=new JArray() };
        sim.CommandBuffer.CommandEnqueued += Observe;
        EditorApplication.update += Sample;
        Capture();
        return new { attached=true, tick=sim.CurrentTick, cap=cap };
    }
    public static object Note(string kind, string text)
    {
        if (text == null || text.Length > 2048) throw new ArgumentException("Bounded player-visible note required.");
        ((JArray)journal["notes"]).Add(new JObject { ["utc"]=DateTime.UtcNow.ToString("O"), ["tick"]=sim.CurrentTick, ["kind"]=kind, ["text"]=text });
        Save(); return new { recorded=true, attempts=observedHttp.Count, remaining=cap-observedHttp.Count };
    }
    static void Observe(ICommand command, CommandEnqueueSource source)
    {
        if (command == null || command.PlayerId != manager.PlayerId) return;
        // Type/source only: never export target coordinates or hidden enemy state.
        ((JArray)journal["commands"]).Add(new JObject { ["utc"]=DateTime.UtcNow.ToString("O"), ["tick"]=sim.CurrentTick,
            ["type"]=command.GetType().Name, ["source"]=source.ToString() });
        Save();
    }
    static void Sample()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextSample) return;
        nextSample = EditorApplication.timeSinceStartup + 5;
        Capture();
    }
    public static object Capture()
    {
        string trace = provider.LastRequestTrace ?? "";
        foreach (Match match in Regex.Matches(trace, @"utc=([^;\r\n]+);model=([^;\r\n]+);request=built"))
        {
            string id = match.Groups[1].Value;
            if (observedHttp.Add(id)) ((JArray)journal["observed_http_attempts"]).Add(new JObject { ["ordinal"]=observedHttp.Count, ["request_utc"]=id, ["model"]=match.Groups[2].Value });
        }
        var board = CommanderTaskBoardProjection.Capture(manager, null, 0);
        var data = new JObject { ["tick"]=sim.CurrentTick, ["age"]=sim.GetPlayerAge(manager.PlayerId),
            ["resources"]=JObject.FromObject(sim.ResourceManager.GetPlayerResources(manager.PlayerId)),
            ["owned_unit_count"]=sim.UnitRegistry.GetAllUnits().Count(u=>u.PlayerId==manager.PlayerId),
            ["transcript"]=chat.DisplayedTranscript, ["owned_living_units"]=new JArray(sim.UnitRegistry.GetAllUnits().Where(u=>u.PlayerId==manager.PlayerId && !u.IsSheep).Select(u=>new JObject { ["id"]=u.Id,["type"]=u.UnitType,["health"]=u.CurrentHealth,["state"]=u.State.ToString(),["resource_target"]=u.TargetResourceNodeId,["construction_target"]=u.ConstructionTargetBuildingId })), ["population"]=sim.GetPopulation(manager.PlayerId),["population_cap"]=sim.GetPopulationCap(manager.PlayerId),["owned_villager_count"]=sim.UnitRegistry.GetAllUnits().Count(u=>u.PlayerId==manager.PlayerId && u.IsVillager),
            ["owned_buildings"]=new JArray(sim.BuildingRegistry.GetAllBuildings().Where(b=>b.PlayerId==manager.PlayerId).Select(b=>new JObject { ["id"]=b.Id, ["type"]=b.Type.ToString(), ["under_construction"]=b.IsUnderConstruction, ["destroyed"]=b.IsDestroyed })),
            ["goals"]=new JArray(manager.Goals.Select(g=>new JObject { ["id"]=g.GoalId, ["type"]=g.GoalType.ToString(), ["status"]=g.Status.ToString() })),
            ["cards"]=new JArray(board.Cards.Select(c=>new JObject { ["request_id"]=c.RequestId, ["status"]=c.Status.ToString(), ["progress"]=c.Progress })),
            ["approval_pending"]=chat.PendingActionPlan!=null, ["clarification_pending"]=chat.PendingClarification!=null,
            ["provider_trace"]=trace,
            ["provider_timed_out"]=(bool)typeof(OpenRouterCommanderProvider).GetProperty("LastRequestTimedOut",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(provider,null),
            ["provider_network_failure"]=(bool)typeof(OpenRouterCommanderProvider).GetProperty("LastNetworkFailure",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(provider,null),
            ["match_over"]=sim.IsMatchOver, ["match_end_tick"]=sim.MatchEndTick };
        if (sim.IsMatchOver) data["winning_team"]=sim.WinningTeamId;
        string next = data.ToString(Formatting.None);
        if (next != fingerprint) { ((JArray)journal["samples"]).Add(data); fingerprint=next; Save(); }
        return new { attempts=observedHttp.Count, remaining=cap-observedHttp.Count, tick=sim.CurrentTick, matchOver=sim.IsMatchOver };
    }
    static void Save() { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path,journal.ToString(Formatting.Indented),new System.Text.UTF8Encoding(false)); }
    public static object Detach()
    {
        Capture(); EditorApplication.update-=Sample; sim.CommandBuffer.CommandEnqueued-=Observe;
        journal["end_utc"]=DateTime.UtcNow.ToString("O"); Save();
        return new { attempts=observedHttp.Count, cap=cap, matchOver=sim.IsMatchOver };
    }
}



