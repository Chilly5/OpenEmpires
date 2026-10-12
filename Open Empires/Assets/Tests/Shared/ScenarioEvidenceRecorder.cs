using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires.TestSupport
{
    public sealed class ScenarioEvidenceRecorder
    {
        private static readonly string[] Phase5CTurnFields = {
            "schemaVersion", "caseId", "requestId", "turnCorrelationId", "utterance", "turnText", "turnIndex", "status", "evidenceMode", "uiState",
            "fixtureKind", "semanticValid", "semanticOutcome", "fidelity", "normalizedFields", "admittedFields", "turnHistory",
            "draftBefore", "draftAfter", "draftChanges", "approvalRequired", "approved", "approvalMode",
            "providerCalls", "httpAttempts", "packAccepted", "commandCount", "commandTypes", "nativeResults",
            "taskStates", "estimatedInputTokens", "actualPromptTokens", "actualCompletionTokens",
            "actualCachedTokens", "actualReasoningTokens", "reportedCost", "attemptCosts",
            "uiSubmitMs", "packingMs", "transportMs", "parseMs", "admissionMs",
            "firstNativeActionMs", "fogNewlyExploredCount", "garrisonHousedCount", "garrisonProtectedCount",
            "garrisonUnaccommodatedCount", "futureObservedCount", "futureAssignedCount", "humanTrainCommands",
            "resourceProgress", "gatheredFoodIncome", "foodCompletionTick", "foodIncomeAtCompletion",
            "commanderCommandsAtFoodCompletion", "ordinaryFoodGatherActive",
            "fixtureAssumption", "previewMentionsOwnedBase", "newResultCount", "oldUnitExcluded",
            "humanBirthExcluded", "exactPatrolBinding", "nativePatrolActorCount",
            "nativePatrolWaypointsMatch", "nativePatrolDestinationMatch",
            "builderAffinityVerified", "assignedBuilderHumanProtected",
            "ownedTaskFactCount", "readOnlyProviderRequest", "questionFactClass",
            "errorStage", "errorCategory", "gap", "tickMode", "ticks"
        };
        private readonly string path,run;
        private string lastStage="setup",firstFailure,firstCategory;
        private JObject diagnostics=new JObject{["initial_http_attempts"]="unavailable",["repair_http_attempts"]="unavailable",["http_status"]="unavailable",["finish_category"]="unavailable",["attribution"]="unavailable"};
        public void SetDiagnostics(int initial,int repair,int status,string finish,int[] goals,string[] commands,int[] buildings,int[] units)
        {
            if(initial<0||initial>6||repair<0||repair>6||initial+repair>6||status<0||status>599
                ||!new[]{"stop","length","other","unavailable"}.Contains(finish)
                ||goals==null||commands==null||buildings==null||units==null||goals.Any(i=>i<0)||buildings.Any(i=>i<0)||units.Any(i=>i<0)
                ||commands.Any(c=>c==null||!System.Text.RegularExpressions.Regex.IsMatch(c,@"^[A-Za-z]{1,60}Command$")))throw new InvalidOperationException("Invalid bounded diagnostics.");
            diagnostics=new JObject{["initial_http_attempts"]=initial,["repair_http_attempts"]=repair,["http_status"]=status==0?(JToken)"unavailable":status,
                ["finish_category"]=finish,["goal_ids"]=new JArray(goals.Take(12)),["command_types"]=new JArray(commands.Take(64)),
                ["native_building_ids"]=new JArray(buildings.Take(64)),["native_unit_ids"]=new JArray(units.Take(64)),
                ["attribution_truncated"]=goals.Length>12||commands.Length>64||buildings.Length>64||units.Length>64};
        }
        public ScenarioEvidenceRecorder(string path,string runId)
        {
            if(!Guid.TryParseExact(runId,"D",out _)||Path.GetExtension(path)!=".json")throw new InvalidOperationException("Invalid evidence artifact identity.");
            this.path=Path.GetFullPath(path);run=runId;
            CheckPaths();if(File.Exists(this.path))throw new InvalidOperationException("Evidence artifact already exists; preserve prior evidence.");
        }
        public void Record(string stage,CommanderSemanticResult result,string category,long requestId,bool approved,int goals,int commands,int nativeCount,int tick)
        {
            if(!new[]{"setup","provider","semantic","approval","native","terminal"}.Contains(stage)
                ||!new[]{"none","unexpected-effect","test-failed","budget-exhausted","budget-unavailable","scope-denied","provider-fault","provider-no-result","provider-cancelled","provider-unavailable","invalid-semantic","native-deadline","external-gate"}.Contains(category)
                ||requestId< -1||goals< -1||goals>10000||commands< -1||commands>10000||nativeCount< -1||nativeCount>10000||tick< -1)throw new InvalidOperationException("Invalid evidence metadata.");
            if(category!="none"&&firstFailure==null){firstFailure=stage=="terminal"?lastStage:stage;firstCategory=category;}
            if(stage!="terminal")lastStage=stage;
            var root=new JObject{["schema"]="openempires-live-scenario-evidence@1",["run_id"]=run,["last_stage"]=stage,
                ["first_failing_stage"]=firstFailure,["first_failure_category"]=firstCategory,["error_category"]=category,
                ["semantic_valid"]=result?.IsValid??false,["outcome"]=result?.Outcome.ToString()??"unavailable",
                ["request_id"]=requestId>=0?requestId.ToString(System.Globalization.CultureInfo.InvariantCulture):"unavailable",
                ["approved"]=approved,["goal_count"]=goals,["command_count"]=commands,["native_entity_count"]=nativeCount,
                ["native_entity_basis"]=nativeCount<0?"unavailable":"game-owned goal attribution",["native_tick"]=tick,
                ["normalized_nodes"]=Normalize(result),["diagnostics"]=diagnostics.DeepClone()};
            // Never serialize provider prose, input, raw response, headers, errors,
            // context/world objects, symbolic provider node names or credentials.
            WriteAtomic(root);
        }
        // A distinct bounded per-turn projection. The caller supplies only detached, allow-listed
        // observations; the recorder never serializes provider envelopes or simulation objects.
        public void RecordPhase5CTurn(JObject safeTurn)
        {
            if(safeTurn==null || safeTurn.Count>Phase5CTurnFields.Length)
                throw new InvalidOperationException("Invalid Phase 5C turn record.");
            foreach(var field in safeTurn.Properties())
                if(!Phase5CTurnFields.Contains(field.Name) || !SafePhase5CValue(field.Value,0))
                    throw new InvalidOperationException("Unsafe Phase 5C turn field.");
            if((int?)safeTurn["schemaVersion"]!=1 || !SafeSlug((string)safeTurn["caseId"])
                || (safeTurn["requestId"]?.Type!=JTokenType.Null && !SafeRequestId((string)safeTurn["requestId"]))
                || (safeTurn["turnCorrelationId"]?.Type!=JTokenType.Null
                    && safeTurn["turnCorrelationId"]!=null && !SafeRequestId((string)safeTurn["turnCorrelationId"]))
                || safeTurn["utterance"]?.Type!=JTokenType.String
                || ((string)safeTurn["utterance"]).Length>1500
                || !new[]{"started","observed","failed","open-gap"}.Contains((string)safeTurn["status"]))
                throw new InvalidOperationException("Invalid Phase 5C turn identity or state.");
            string stage=(string)safeTurn["errorStage"];
            if(!string.IsNullOrEmpty(stage) && firstFailure==null)
            {firstFailure=stage;firstCategory=(string)safeTurn["errorCategory"] ?? "unclassified";}
            var root=new JObject { ["schema"]="openempires-phase5c-scenario-turn@1",["run_id"]=run,
                ["first_failing_stage"]=firstFailure,["first_failure_category"]=firstCategory };
            foreach(var field in safeTurn.Properties())root[field.Name]=field.Value.DeepClone();
            WriteAtomic(root);
        }
        private static bool SafeSlug(string value)=>value!=null&&Regex.IsMatch(value,@"^[a-z0-9-]{1,80}$");
        private static bool SafeRequestId(string value)=>value!=null&&Regex.IsMatch(value,@"^[A-Za-z0-9._:-]{1,128}$");
        private static bool SafePhase5CValue(JToken value,int depth)
        {
            if(value==null||depth>7)return false;
            if(value.Type==JTokenType.Null||value.Type==JTokenType.Boolean||value.Type==JTokenType.Integer||value.Type==JTokenType.Float)return true;
            if(value.Type==JTokenType.String)
            {string text=(string)value;return text==null||text.Length<=1500&&!text.Any(ch=>char.IsControl(ch)&&ch!='\n'&&ch!='\r'&&ch!='\t');}
            if(value is JArray array)return array.Count<=32&&array.All(item=>SafePhase5CValue(item,depth+1));
            if(value is JObject objectValue)
            {
                if(objectValue.Count>32)return false;
                foreach(var field in objectValue.Properties())
                    if(!Regex.IsMatch(field.Name,@"^[A-Za-z][A-Za-z0-9_]{0,63}$")
                        ||Regex.IsMatch(field.Name,@"(?i)(entityid|unitid|buildingid|coordinate|position|tile|header|raw|secret|password|apikey|hiddenenemy)")
                        ||!SafePhase5CValue(field.Value,depth+1))return false;
                return true;
            }
            return false;
        }
        private void WriteAtomic(JObject root)
        {
            byte[] bytes=new UTF8Encoding(false,true).GetBytes(root.ToString(Formatting.Indented));
            if(bytes.Length>16384)throw new InvalidOperationException("Evidence artifact exceeds its bound.");
            CheckPaths();Directory.CreateDirectory(Path.GetDirectoryName(path));string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {file.Write(bytes,0,bytes.Length);file.Flush(true);}
                if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        private void CheckPaths()
        {
            for(string cursor=Path.GetDirectoryName(path);cursor!=null;cursor=Path.GetDirectoryName(cursor))
                if(Directory.Exists(cursor)&&(File.GetAttributes(cursor)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("Evidence paths cannot use links.");
            if(File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("Evidence paths cannot use links.");
        }
        private static JArray Normalize(CommanderSemanticResult result)
        {
            var nodes=new JArray();if(result==null||!result.IsValid)return nodes;
            foreach(var node in result.Nodes.Take(4))nodes.Add(new JObject{["type"]=node.Type.ToString(),["count"]=node.Count,
                ["building"]=node.BuildingType?.ToString(),["unit"]=node.UnitType,["resource"]=node.ResourceType?.ToString(),
                ["anchor"]=node.PlacementAnchorSelector?.ToString(),["relation"]=node.PlacementRelation?.ToString(),["gap"]=node.ClearGapTiles,
                ["quantity_mode"]=node.QuantityMode.ToString(),["dependencies"]=new JArray(node.DependsOn),["producer_from"]=node.ProducerFromNode,["result_from"]=node.ResultFromNode});
            if(result.DynamicPlan==null)return nodes;
            var program=result.DynamicPlan.Nodes.ToList();
            foreach(var node in program.Take(12))
            {
                var parameters=new JObject();
                foreach(var field in node.Primitive.Parameters)
                    if(node.Parameters.TryGetValue(field.Name,out var value))parameters[field.Name]=SafeParameter(field.Kind,value);
                var inputs=new JObject();foreach(var input in node.Inputs)inputs[input.Key]=program.FindIndex(n=>n.Id==input.Value);
                nodes.Add(new JObject{["mechanic"]=node.Primitive.Mechanic.ToString(),["parameters"]=parameters,["inputs"]=inputs,
                    ["dependencies"]=new JArray(node.DependsOn.Select(id=>program.FindIndex(n=>n.Id==id)))});
            }
            return nodes;
        }
        private static JToken SafeParameter(CommanderDynamicFieldKind kind,object value)
        {
            if(value is int number)return new JValue(number);
            string text=value as string;
            if(text==null||text.Length>64)return new JValue("unavailable");
            if(kind==CommanderDynamicFieldKind.UnitId)
                return new JValue(text.StartsWith("unit:")&&int.TryParse(text.Substring(5),out int unit)&&unit>=0&&unit<=255?"unit:"+unit:"unsupported");
            if(kind==CommanderDynamicFieldKind.BuildingId)
                return new JValue(text.StartsWith("building:")&&Enum.TryParse(text.Substring(9),out BuildingType building)&&Enum.IsDefined(typeof(BuildingType),building)?"building:"+building:"unsupported");
            // Every remaining string field is a strict named enum in the typed parser.
            return new JValue(text);
        }
    }
}
