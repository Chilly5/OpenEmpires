#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace OpenEmpires.EditorTools
{
    // Explicit local QA instrumentation. No raw replies, credentials or private context persist.
    public static class CommanderPhase5CIntegrationProbe
    {
        private static readonly string Folder = "Docs/CommanderPhase5C/evidence/marker-multistep-repair";
        private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly List<object> Commands = new List<object>();
        private static readonly List<object> Attempts = new List<object>();
        private static int limit;
        public static JObject DescribeSemanticEnvelope(string providerEnvelope)
        {
            var shape=new JArray();
            var safe=new JObject{["mode"]="Unknown",["shape"]=shape};
            try
            {
                var envelope=JObject.Parse(providerEnvelope);
                string content=(string)envelope["choices"]?[0]?["message"]?["content"];
                var root=JToken.Parse(content);
                string mode=root is JObject obj&&obj["outcome"]?.Type==JTokenType.String?(string)obj["outcome"]:null;
                if(new[]{"Request","DynamicPlan","Clarify","Answer","Unsupported"}.Contains(mode))safe["mode"]=mode;
                RecordingTransport.Walk(root,"$",shape);
            }
            catch(Exception){safe["shapeParsingFailed"]=true;}
            return safe;
        }

        // Observation only; the caller must separately obtain consent and wrap its
        // transport in the existing paid-attempt guard. Construction sends nothing.
        public static ICommanderHttpTransport ObserveNextResponse(ICommanderHttpTransport inner)
            => ObserveNextResponse(inner, Path.Combine(Folder,"next-response-shape.json"));

        public static ICommanderHttpTransport ObserveNextResponse(ICommanderHttpTransport inner, string receiptPath)
        {
            string root=Path.GetFullPath("Docs/CommanderPhase5C/evidence")+Path.DirectorySeparatorChar;
            string path=Path.GetFullPath(receiptPath);
            if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase)||Path.GetExtension(path)!=".json")
                throw new ArgumentException("Diagnostic receipts must stay inside the Phase5C evidence directory.");
            return new OneResponseShapeTransport(inner,path);
        }

        private sealed class OneResponseShapeTransport:ICommanderHttpTransport
        {
            private readonly ICommanderHttpTransport inner;
            private readonly string path;
            private int sent;
            internal OneResponseShapeTransport(ICommanderHttpTransport inner,string path)
            {this.inner=inner??throw new ArgumentNullException(nameof(inner));this.path=path;}
            public async Task<CommanderHttpResponse> PostJsonAsync(Uri uri,string body,IReadOnlyDictionary<string,string> headers,CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                if(Interlocked.Exchange(ref sent,1)!=0||File.Exists(path))
                    throw new InvalidOperationException("Preserve the single diagnostic response; no repeat dispatch.");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path,"{\"stage\":\"reserved\"}");
                var response=await inner.PostJsonAsync(uri,body,headers,token).ConfigureAwait(false);
                var safe=DescribeSemanticEnvelope(response?.Body);
                safe["http"]=response?.StatusCode??0;safe["stage"]="response";
                File.WriteAllText(path,safe.ToString(Formatting.Indented));
                return response;
            }
        }
        public static void Attach(int maximumAttempts = 12)
        {
            var bootstrap = GameBootstrapper.Instance;
            var chat = CommanderChatUI.Instance;
            var provider = (OpenRouterCommanderProvider)typeof(CommanderChatUI).GetField("semanticProvider", Private).GetValue(chat);
            var transportField = typeof(OpenRouterCommanderProvider).GetField("transport", Private);
            limit = maximumAttempts;
            if (Attempts.Count == 0 && File.Exists(Path.Combine(Folder,"attempts.json")))
                Attempts.AddRange(JArray.Parse(File.ReadAllText(Path.Combine(Folder,"attempts.json"))).Cast<object>());
            if (Commands.Count == 0 && File.Exists(Path.Combine(Folder,"commands.json")))
                Commands.AddRange(JArray.Parse(File.ReadAllText(Path.Combine(Folder,"commands.json"))).Cast<object>());
            if (transportField.GetValue(provider) is RecordingTransport) return;
            transportField.SetValue(provider, new RecordingTransport((ICommanderHttpTransport)transportField.GetValue(provider)));
            bootstrap.Commander.RequestTracingEnabled = true;
            bootstrap.Simulation.CommandBuffer.CommandEnqueued += (command, source) =>
            {
                if (source != CommandEnqueueSource.Commander) return;
                Commands.Add(new { tick = bootstrap.Simulation.CurrentTick, type = command.GetType().Name });
                Save("commands.json", Commands);
            };
        }
        public static object Snapshot(string name)
        {
            var b = GameBootstrapper.Instance;
            var ui = CommanderChatUI.Instance;
            var provider = (OpenRouterCommanderProvider)typeof(CommanderChatUI).GetField("semanticProvider", Private).GetValue(ui);
            object[] marker = { 0, 0 };
            bool hasMarker = (bool)b.Commander.GetType().GetMethod("TryGetPlayerMarkerSnapshot", Private).Invoke(b.Commander, marker);
            var result = new {
                requestId = typeof(CommanderChatUI).GetProperty("LastSubmittedSemanticRequestId", Private).GetValue(ui),
                trace = provider.LastRequestTrace,
                attempts = provider.LastSemanticAttempts.Select(a => new { kind = a.Kind, stage = a.ResultStage, http = a.HttpStatusCode }),
                preview = ui.PendingActionPlan?.Preview,
                transcript = ui.DisplayedTranscript,
                cursor = Cursor.lockState.ToString(), screen = new[] { Screen.width, Screen.height },
                marker = hasMarker ? marker : null, tick = b.Simulation.CurrentTick,
                goals = b.Commander.Goals.Select(g => new { id = g.GoalId, type = g.GetType().Name, status = g.Status.ToString(), reason = g.StatusReason }),
                buildings = b.Simulation.BuildingRegistry.GetAllBuildings().Where(v => v.PlayerId == b.Commander.PlayerId && !v.IsDestroyed)
                    .Select(v => new { id = v.Id, type = v.Type.ToString(), unfinished = v.IsUnderConstruction }),
                spearmen = b.Simulation.UnitRegistry.GetAllUnits().Where(u => u.PlayerId == b.Commander.PlayerId && u.UnitType == 1 && u.State != UnitState.Dead)
                    .Select(u => new { id = u.Id, position = u.SimPosition.ToString(), state = u.State.ToString() })
            };
            Save(name + ".json", result);
            ScreenCapture.CaptureScreenshot(Path.Combine(Folder, name + ".png"));
            return result;
        }
        private static void Save(string name, object value)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, name), JsonConvert.SerializeObject(value, Formatting.Indented));
        }
        private sealed class RecordingTransport : ICommanderHttpTransport
        {
            private readonly ICommanderHttpTransport inner;
            public RecordingTransport(ICommanderHttpTransport inner) { this.inner = inner; }
            public async Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string body, IReadOnlyDictionary<string,string> headers, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                lock (Attempts)
                {
                    if (Attempts.Count >= limit) throw new InvalidOperationException("Local integration attempt bound reached.");
                    string ledgerPath = "Docs/CommanderPhase5C/evidence/paid-attempt-ledger.json";
                    using (var lease = new FileStream(ledgerPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                    {
                        var ledger = JObject.Parse(File.ReadAllText(ledgerPath));
                        int used = (int)ledger["usedHttpAttempts"];
                        if (used >= (int)ledger["maximumHttpAttempts"]) throw new InvalidOperationException("Existing allowance exhausted.");
                        ledger["usedHttpAttempts"] = used + 1;
                        ((JArray)ledger["attempts"]).Add(new JObject { ["source"] = "evidence/marker-multistep-repair/attempts.json", ["kind"] = "integration-reserved-http", ["utc"] = DateTime.UtcNow.ToString("O") });
                        File.WriteAllText(ledgerPath, ledger.ToString(Formatting.Indented));
                    }
                    Attempts.Add(new { stage = "reserved" }); Save("attempts.json", Attempts);
                }
                var response = await inner.PostJsonAsync(uri, body, headers, token).ConfigureAwait(false);
                // Only schema paths/types and known discriminator values survive this boundary.
                var shape = new JArray();
                string mode = "unknown";
                try
                {
                    var envelope = JObject.Parse(response.Body);
                    string content = (string)envelope["choices"]?[0]?["message"]?["content"];
                    var root = JObject.Parse(content);
                    string outcome = (string)root["outcome"];
                    if (new[] { "Request", "DynamicPlan", "Clarify", "Answer", "Unsupported" }.Contains(outcome)) mode = outcome;
                    Walk(root, "$", shape);
                }
                catch (Exception) { }
                lock (Attempts) { Attempts[Attempts.Count - 1] = new { http = response.StatusCode, mode, shape }; Save("attempts.json", Attempts); }
                return response;
            }
            internal static void Walk(JToken value, string path, JArray rows)
            {
                if (rows.Count >= 160) return;
                if(path.Length>128)path="<deep>";
                rows.Add(new JObject { ["path"] = path, ["actualType"] = value.Type.ToString(),
                    ["discriminator"] = path.EndsWith(".type") || path == "$.executionOrder" ? SafeDiscriminator(value) : null,
                    ["schemaValue"] = SafeSchemaValue(path,value) });
                const string known = "|outcome|executionOrder|nodes|type|structure|unit|count|builders|state|placement|anchor|relation|ordinal|dependsOn|producerFromNode|resultFromNode|quantityMode|constraints|version|id|mechanic|parameters|inputs|location|unitSelector|clearGapTiles|workers|destination|mode|resource|sourceKind|countMode|message|pending|missingSlots|missingFields|node|path|alternatives|";
                if (value is JObject obj) foreach (var p in obj.Properties())
                    Walk(p.Value, path + "." + (known.Contains("|" + p.Name + "|") ? p.Name : "<unknown>"), rows);
                else if (value is JArray array) for (int i=0;i<Math.Min(array.Count,12);i++) Walk(array[i], path + "[" + i + "]", rows);
            }
            private static string SafeDiscriminator(JToken value)
            {
                string text = value.Type == JTokenType.String ? (string)value : "";
                return new[] { "BuildStructure", "EnsureUnitCount", "MoveUnits", "Independent", "Sequential", "DependencyGraph" }.Contains(text) ? text : null;
            }
            private static string SafeSchemaValue(string path,JToken value)
            {
                if(value.Type!=JTokenType.String)return null;
                string text=(string)value;
                // Public schema identifiers only, at their exact schema positions.
                // Never retain free prose, arbitrary names, numbers or coordinates.
                if(path=="$.executionOrder"||path=="$.pending.executionOrder")
                    return new[]{"Independent","Sequential","DependencyGraph"}.Contains(text)?text:"<unknown>";
                if(System.Text.RegularExpressions.Regex.IsMatch(path,@"^\$\.missingSlots\[[0-3]\]\.type$"))
                    return new[]{"Confirmation","Count","SourceKind","CanonicalName","Location"}.Contains(text)?text:"<unknown>";
                if(System.Text.RegularExpressions.Regex.IsMatch(path,@"^\$\.missingSlots\[[0-3]\]\.path$"))
                    return new[]{"structure","unit","producer","count","location","placement.anchor","sourceKind",
                        "destination.sourceKind","placement.sourceKind","technology","resource","destination.resource",
                        "placement.resource","quantityMode","targetAge","builders.state","builders.count"}.Contains(text)?text:"<unknown>";
                if(System.Text.RegularExpressions.Regex.IsMatch(path,@"^\$\.missingSlots\[[0-3]\]\.alternatives\[[0-7]\]$"))
                    return new[]{"TargetTotal","New","PlayerMarker","PlayerBase","MyTownCenter","Eligible","IdleOnly"}.Contains(text)?text:"<unknown>";
                return null;
            }
        }
    }
}
#endif
