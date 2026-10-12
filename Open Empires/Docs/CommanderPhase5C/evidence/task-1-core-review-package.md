# Task1 core frozen review package

Baseline HEAD4c9de96adbf6342fbe9e1490fa9b898a0f99caf6; dirty working tree, no commit.
Scope: first compact pack/usage core only, whole Task1 NOT complete. Task2 source writer may proceed; use this frozen snapshot.

warning: in the working copy of 'Open Empires/Assets/Scripts/AI/Commander/Phase4A/OpenRouterCommanderProvider.cs', LF will be replaced by CRLF the next time Git touches it
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase4A/OpenRouterCommanderProvider.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase4A/OpenRouterCommanderProvider.cs
index 2cf6816..a4f706d 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase4A/OpenRouterCommanderProvider.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase4A/OpenRouterCommanderProvider.cs	
@@ -80,20 +80,23 @@ namespace OpenEmpires
             + "Do not add fields.";
 
         private const string SemanticFidelityCheck =
             " Final interpretation check for the existing typed Request forms: a/an worker or villager is EXACTLY ONE eligible existing worker, not an unspecified count. Number words also fill their respective integer slots. A supplied quantity must not become a Count question. Resource amount and worker count stay separate; preserve the requested source and absolute age. A completion phrase connects effects with dependsOn, not newly produced actors. Example 'Build a Mill, and after completion send a worker to gold' is {\"outcome\":\"Request\",\"executionOrder\":\"Sequential\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Mill\",\"count\":1,\"builders\":{\"state\":\"Eligible\",\"count\":1}},{\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\",\"countMode\":\"Exact\",\"count\":1,\"workers\":{\"state\":\"Any\"},\"destination\":{\"resource\":\"Gold\"},\"dependsOn\":[0]}]}. Clarify only genuinely missing meaning; no runtime IDs, coordinates, commands, authority, dropped constraints or invented production.";
 
         private readonly string apiKey;
         private readonly ICommanderHttpTransport transport;
         private readonly TimeSpan timeout;
         private readonly int semanticMaxTokens;
         public string LastRequestTrace { get; private set; } = string.Empty;
+        public CommanderProviderUsage LastUsage { get; private set; } = CommanderProviderUsage.Unknown;
+        public int? LastInputEstimate { get; private set; }
+        public IReadOnlyDictionary<string, int> LastInputSectionEstimates { get; private set; }
         internal bool HasConfiguration => !string.IsNullOrWhiteSpace(apiKey);
         internal bool UsesGateway => transport is CommanderGatewaySemanticTransport;
         internal bool UsesTransport(ICommanderHttpTransport candidate) => ReferenceEquals(transport,candidate);
         internal bool HasGatewayConfiguration => transport is CommanderGatewaySemanticTransport gateway && gateway.IsConfigured;
         internal int? LastHttpStatusCode { get; private set; }
         internal bool LastNetworkFailure { get; private set; }
         internal bool LastRequestTimedOut { get; private set; }
         internal bool HasAttemptedRequest { get; private set; }
         internal bool HasValidatedSemanticResponse { get; private set; }
 
@@ -183,57 +186,52 @@ namespace OpenEmpires
                 return StrategicAIProviderResult.Rejected(Unavailable);
             }
         }
 
         public async Task<CommanderSemanticResult> TranslateSemanticAsync(
             CommanderSemanticProviderRequest request, CancellationToken token)
         {
             if (request == null) throw new ArgumentNullException(nameof(request));
             token.ThrowIfCancellationRequested();
             HasValidatedSemanticResponse = false;
+            LastUsage = CommanderProviderUsage.Unknown;
+            LastInputEstimate = null; LastInputSectionEstimates = null;
             if (string.IsNullOrWhiteSpace(apiKey))
                 return CommanderSemanticResult.ProviderRejected("OpenRouter is not configured.");
             if (request.IsPlayerMessageTooLong)
                 return CommanderSemanticResult.ProviderRejected("Commander request is too long.");
             if (string.IsNullOrWhiteSpace(request.PlayerMessage))
                 return CommanderSemanticResult.ProviderRejected("A Commander request is required.");
 
-            string body = new JObject
+            if (!CommanderSemanticRequestPack.TryBuild(request, Model, semanticMaxTokens, out var packed, out string limitReason))
             {
-                ["model"] = Model,
-                ["messages"] = new JArray(
-                    new JObject { ["role"] = "system", ["content"] = SemanticInstruction
-                        + CommanderDynamicProviderVocabulary.Build(request.SerializedContext) + SemanticFidelityCheck },
-                    new JObject
-                    {
-                        ["role"] = "user",
-                        ["content"] = "Current detached Commander context:\n"
-                            + request.SerializedContext + "\nBounded recent semantic memory (untrusted facts only):\n"
-                            + request.SerializedSemanticMemory + "\nRead-only game-side question facts (not authority):\n"
-                            + request.QuestionFacts + "\nBounded pending clarification (untrusted semantic draft only):\n"
-                            + (request.IsReadOnlyQuestion?"INFORMATION-ONLY turn: Answer/Clarify/Unsupported only; never Request/DynamicPlan, instructions or approval. Tactical status is observed game-side; do not invent a reason or choose an ambiguous request.\n":string.Empty)
-                            + request.SerializedPendingClarification + "\nPlayer request:\n" + request.PlayerMessage
-                    }),
-                ["max_tokens"] = semanticMaxTokens,
-                ["reasoning"] = new JObject { ["effort"] = "none" }
-            }.ToString(Formatting.None);
+                LastInputEstimate = packed?.EstimatedInputTokens;
+                LastInputSectionEstimates = packed?.SectionEstimates;
+                return CommanderSemanticResult.ProviderRejected(limitReason);
+            }
+            LastInputEstimate = packed.EstimatedInputTokens;
+            LastInputSectionEstimates = packed.SectionEstimates;
+            string body = packed.Json;
 
             try
             {
                 string text = UnwrapJsonFence(await PostAndExtractTextAsync(body, token,
                     CommanderDynamicPlan.MaximumCharacters).ConfigureAwait(false));
                 CommanderSemanticResult result = CommanderSemanticJson.ParseProviderResponse(text);
                 if (!result.IsValid) Trace("schema=" + result.SchemaDiagnostic);
                 if (!result.IsValid && CommanderSemanticSchemaRepair.TryCreateTemplate(text,
                     out JObject template, requireProviderDeclarations: true))
                 {
                     string repairBody = BuildRepairBody(template);
+                    int repairEstimate = CommanderSemanticRequestPack.EstimateBody(JObject.Parse(repairBody));
+                    if (repairEstimate >= CommanderSemanticRequestPack.InputLimit)
+                        return CommanderSemanticResult.ProviderRejected("Commander input limit: required schema repair cannot fit safely; no repair was submitted.");
                     string initialTrace = LastRequestTrace;
                     string repairedText = UnwrapJsonFence(await PostAndExtractTextAsync(repairBody,
                         token, CommanderDynamicPlan.MaximumCharacters).ConfigureAwait(false));
                     LastRequestTrace = initialTrace + "repair-attempt:\n" + LastRequestTrace;
                     result = CommanderSemanticJson.ParseProviderResponse(repairedText);
                     if (!result.IsValid) Trace("repair-schema=" + result.SchemaDiagnostic);
                     if (!result.IsValid || !CommanderSemanticSchemaRepair.MatchesTemplate(
                         repairedText, template))
                         result = CommanderSemanticResult.ProviderRejected(
                             "Commander AI returned an invalid semantic response; no order was submitted.");
@@ -302,20 +300,21 @@ namespace OpenEmpires
                         + template.ToString(Formatting.None) }),
                 ["max_tokens"] = semanticMaxTokens,
                 ["reasoning"] = new JObject { ["effort"] = "none" }
             }.ToString(Formatting.None);
         }
 
         private async Task<string> PostAndExtractTextAsync(string body,
             CancellationToken cancellationToken, int maximumContentCharacters)
         {
             LastRequestTrace = string.Empty;
+            LastUsage = CommanderProviderUsage.Unknown;
             HasAttemptedRequest = true; LastHttpStatusCode = null;
             LastNetworkFailure = false; LastRequestTimedOut = false;
             var elapsed = System.Diagnostics.Stopwatch.StartNew();
             Trace("utc=" + DateTime.UtcNow.ToString("O") + ";model=" + Model + ";request=built");
             using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
             {
                 deadline.CancelAfter(timeout);
                 Trace("http=sent");
                 CommanderHttpResponse response;
                 try { response = await transport.PostJsonAsync(new Uri(Endpoint),
@@ -340,20 +339,21 @@ namespace OpenEmpires
                 if (response == null) throw new OpenRouterFailure(Unavailable);
                 LastHttpStatusCode = response.StatusCode;
                 Trace("http=" + response.StatusCode + ";durationMs=" + elapsed.ElapsedMilliseconds);
                 if (response.StatusCode < 200 || response.StatusCode > 299)
                     throw new OpenRouterFailure(HttpFailure(response.StatusCode));
                 try
                 {
                     if (response.Body.Length > 65536)
                         throw new OpenRouterFailure("Commander AI returned an oversized response.");
                     JObject result = JObject.Parse(response.Body);
+                    LastUsage = CommanderProviderUsage.Read(result);
                     if (result["error"] is JObject error)
                         throw new OpenRouterFailure(HttpFailure((int?)error["code"] ?? 500));
                     if (!(result["choices"] is JArray choices) || choices.Count != 1)
                         throw new OpenRouterFailure("Commander AI returned an invalid response envelope.");
                     string finish = (string)choices[0]["finish_reason"];
                     // Bounded diagnostic category BEFORE rejection; never echo
                     // arbitrary provider finish strings or the response body.
                     Trace("finish="+(finish=="stop"?"stop":finish=="length"?"length":finish==null?"unspecified":"other"));
                     if (finish == "length")
                         throw new OpenRouterFailure("Commander AI response was truncated; no order was submitted.");


## New file Assets/Scripts/AI/Commander/Phase5C/CommanderSemanticRequestPack.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    // Retrieval/packing only. Nothing here parses an order, selects runtime actors or grants approval.
    internal sealed class CommanderSemanticRequestPack
    {
        internal const int InputLimit = 4000;
        internal const int MessageMargin = 256;
        internal string Json { get; }
        internal int EstimatedInputTokens { get; }
        internal IReadOnlyDictionary<string, int> SectionEstimates { get; }

        private CommanderSemanticRequestPack(JObject body, Dictionary<string, int> sections)
        {
            Json = body.ToString(Formatting.None);
            EstimatedInputTokens = EstimateBody(body);
            SectionEstimates = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(sections);
        }

        // Deliberately conservative UTF8 byte units, not characters/4 or an unverified tokenizer.
        // Actual endpoint-reported prompt usage is recorded separately; this is NOT certification
        // of a rolling alias's tokenizer/hidden endpoint overhead before live calibration.
        internal static int EstimateBody(JObject body)
        {
            JToken modelFacing = body["messages"];
            if (modelFacing == null) return int.MaxValue;
            long estimate = MessageMargin;
            if (!(modelFacing is JArray messages)) return int.MaxValue;
            foreach (var message in messages.OfType<JObject>())
                foreach (string field in new[] { "role", "content", "name", "tool_calls", "tool_call_id" })
                    if (message[field] != null) estimate += Encoding.UTF8.GetByteCount(message[field].Type == JTokenType.String
                        ? (string)message[field] : message[field].ToString(Formatting.None));
            foreach (string field in new[] { "tools", "response_format", "functions", "tool_choice", "function_call" })
                if (body[field] != null) estimate += Encoding.UTF8.GetByteCount(body[field].ToString(Formatting.None));
            return estimate > int.MaxValue ? int.MaxValue : (int)estimate;
        }

        internal static bool TryBuild(CommanderSemanticProviderRequest request, string model, int maxOutputTokens,
            out CommanderSemanticRequestPack pack, out string reason)
        {
            pack = null; reason = string.Empty;
            var context = JObject.Parse(request.SerializedContext);
            string search = request.PlayerMessage + " " + request.SerializedPendingClarification;
            var words = new HashSet<string>(Regex.Matches(search.ToLowerInvariant(), @"[\p{L}\p{N}]+")
                .Cast<Match>().Select(m => m.Value), StringComparer.Ordinal);
            bool Has(params string[] terms) => terms.Any(words.Contains);
            bool construction = Has("build", "construct", "house", "houses", "mill", "farm", "farms", "barracks", "mine", "yard");
            bool allocation = Has("gather", "gathering", "food", "wood", "gold", "stone", "berries", "sheep", "farms", "split", "assign", "put");
            bool future = Has("next", "future", "births");
            bool production = Has("train", "produce", "recruit", "new", "more", "spearmen", "archers", "knights");
            bool age = Has("age", "ages", "feudal", "castle", "imperial", "advance");
            bool action = Has("move", "defend", "defense", "patrol", "scout", "enemy", "attack", "retreat", "repair", "rally", "garrison", "ungarrison");
            bool research = Has("research", "technology", "upgrade");
            bool strategy = Has("strategy", "strategic");
            if (!construction && !allocation && !future && !production && !age && !action && !research && !strategy)
                construction = allocation = future = production = age = action = research = true;
            var selected = new HashSet<CommanderSemanticNodeType>();
            if (construction) selected.Add(CommanderSemanticNodeType.BuildStructure);
            if (allocation) selected.Add(CommanderSemanticNodeType.AllocateWorkers);
            if (future) selected.Add(CommanderSemanticNodeType.WatchFutureUnits);
            if (production) selected.Add(CommanderSemanticNodeType.EnsureUnitCount);
            if (age) selected.Add(CommanderSemanticNodeType.ReachAge);
            if (research) selected.Add(CommanderSemanticNodeType.ResearchTechnology);
            if (strategy) selected.Add(CommanderSemanticNodeType.StrategicObjective);
            if (action)
                foreach (CommanderCapabilityActionType capability in Enum.GetValues(typeof(CommanderCapabilityActionType)))
                    if (capability != CommanderCapabilityActionType.ResearchTechnology
                        && Enum.TryParse(capability.ToString(), out CommanderSemanticNodeType type)) selected.Add(type);

            string instruction = request.IsReadOnlyQuestion ? ReadOnlyRules : CommonRules + Vocabulary(selected);
            // Contract retrieval only: quantities on future units must not imply repeated buildings.
            bool repeated = construction && (Has("split", "third") || Regex.IsMatch(search,
                @"\b(?:build|construct)\s+(?:two|three|four|five|six|seven|eight|nine|ten|[2-9]|1[0-9]|20)\b",
                RegexOptions.IgnoreCase));
            if (repeated && !request.IsReadOnlyQuestion) instruction += DynamicVocabulary();
            string compactContext = CompactContext(context, strategy).ToString(Formatting.None);
            // Essential turn/pending data is never truncated to squeeze through the budget.
            string user = "Context:" + compactContext + "\nMemory:" + request.SerializedSemanticMemory
                + "\nFacts:" + request.QuestionFacts + "\nBounded pending:" + request.SerializedPendingClarification
                + "\nPlayer:" + request.PlayerMessage;
            var body = new JObject { ["model"] = model,
                ["messages"] = new JArray(new JObject { ["role"] = "system", ["content"] = instruction },
                    new JObject { ["role"] = "user", ["content"] = user }),
                ["max_tokens"] = maxOutputTokens, ["reasoning"] = new JObject { ["effort"] = "none" } };
            var sections = new Dictionary<string, int> {
                ["contract"] = Encoding.UTF8.GetByteCount(instruction),
                ["context"] = Encoding.UTF8.GetByteCount(compactContext),
                ["memory"] = Encoding.UTF8.GetByteCount(request.SerializedSemanticMemory),
                ["questionFacts"] = Encoding.UTF8.GetByteCount(request.QuestionFacts),
                ["pending"] = Encoding.UTF8.GetByteCount(request.SerializedPendingClarification),
                ["utterance"] = Encoding.UTF8.GetByteCount(request.PlayerMessage), ["messageMargin"] = MessageMargin };
            var candidate = new CommanderSemanticRequestPack(body, sections);
            pack = candidate; // Observed packing metrics survive a rejection; this is not dispatch authority.
            if (candidate.EstimatedInputTokens >= InputLimit)
            { reason = "Commander input limit: the complete request and required context cannot fit safely; nothing was submitted. Edit the request or resolve its pending question."; return false; }
            return true;
        }

        private const string ReadOnlyRules = "Return one JSON object, no markdown: {\"outcome\":\"Answer\",\"message\":\"brief grounded answer\"}, "
            + "{\"outcome\":\"Clarify\",\"message\":\"specific missing fact\"} or {\"outcome\":\"Unsupported\",\"message\":\"reason\"}. "
            + "Message<=180 chars. INFORMATION ONLY: no nodes/plans/approval. Use owned task facts; unknown facts remain unknown. No hidden state, IDs or commands.";
        private const string CommonRules =
            "JSON only,quoted keys/enums. No extras/authority/runtime IDs/coordinates/commands/hidden state/unrequested goals. Preserve quantities,negations,actors,sources,prerequisite constraints,order,exact results. Relevant Columns;costs food/wood/gold/stone;omitted content unknown. "
            + "Request{outcome:Request,nodes:[typed1..4],executionOrder:Independent|Sequential|DependencyGraph}. then/only-after=completion dependsOn:[prior0-based index];resultFromNode=exact new results;producerFromNode=explicit new producer. a/an=1 existing;number words count;left=MapWest. "
            + "Answer/Clarify/Unsupported{outcome,message<=180},no nodes. Missing Count/Destination:Clarify adds pending AllocateWorkers,missingFields. Retain draft;only missing/explicit corrections,not approval.";

        private static string Vocabulary(HashSet<CommanderSemanticNodeType> selected)
        {
            var text = new StringBuilder();
            foreach (CommanderSemanticNodeType type in Enum.GetValues(typeof(CommanderSemanticNodeType)))
            {
                if (!selected.Contains(type)) continue;
                switch (type)
                {
                    case CommanderSemanticNodeType.BuildStructure:
                        text.Append("BuildStructure{structure:canonical,count1..20,builders:{state:Eligible(default preferidle)|IdleOnly(strict),count:1},placement?}. placement{anchor:MyTownCenter|MyBarracks,relation:Near|MapWest|MapEast,clearGapTiles?:1..20,ordinal?:1..8 TC only} OR {anchor:VisibleResource(default)|WorkedResource(explicit),relation:Near,resource,sourceKind?}. Near gap1,no resource ordinal. "); break;
                    case CommanderSemanticNodeType.AllocateWorkers:
                        text.Append("AllocateWorkers{mode:SelectedCount(ordinary)|Additional(more workers)|TargetTotal(keep total),countMode:Exact|AllMatching(snapshot),count1..200 omitAllMatching,workers:{state:Any|Idle|Gathering,currentResource?},destination:{resource,sourceKind?:Any},resourceAmount?:1..1000000 paired resourceAmountMode:Stockpile|AdditionalGathered(player-wide income)}. Amount!=workers; assignment!=income. "); break;
                    case CommanderSemanticNodeType.EnsureUnitCount:
                        text.Append("EnsureUnitCount:unit canonical,count0..200,quantityMode:TargetTotal default|New. New/more=exact new count, no existing substitutes. Game handles prerequisites; extra buildings only explicit. "); break;
                    case CommanderSemanticNodeType.WatchFutureUnits:
                        text.Append("WatchFutureUnits{unit:canonical,count1..50,producer:canonical,producerOrdinal?:1..8,action:Gather(Villager only),resource,sourceKind?} OR Patrol,location:WorkedResource,resource. Next actual births incl human queues,neverTrain; first=1; ambiguous producer clarify. "); break;
                    case CommanderSemanticNodeType.ReachAge:
                        text.Append("ReachAge:targetAge:Feudal|Castle|Imperial|Next. Explicit2/3/4=Feudal/Castle/Imperial, not Next. "); break;
                    case CommanderSemanticNodeType.ResearchTechnology:
                        text.Append("ResearchTechnology:technology:BlacksmithDamage|BlacksmithDefense|Ballistics|SiegeEngineering|Chemistry|MurderHoles. "); break;
                    case CommanderSemanticNodeType.StrategicObjective:
                        text.Append("StrategicObjective only explicit player strategy; objective from context strategyCapabilities, constraints retained. "); break;
                    default:
                        break; // Capability actions share one field contract below, not repeated per action.
                }
            }
            var actions = selected.Where(t => t != CommanderSemanticNodeType.ResearchTechnology
                && Enum.TryParse(t.ToString(), out CommanderCapabilityActionType _)).OrderBy(t => t).ToArray();
            if (actions.Length > 0)
                text.Append("Action type:").Append(string.Join("|", actions))
                    .Append("; unitSelector,count1..50,location,resource/structure optional,resultFromNode/dependsOn optional. ")
                    .Append("unitSelector:Military|Scout|Villagers|Spearman|Archer|Knight|DamagedMilitary; location:PlayerBase|WorkedResource|VisibleResource|VisibleEnemy|RelativeToSelectedUnits. VisibleEnemy never discovers an unseen base. Point routes, no invented radius. Attack/Repair optional target:{kind:UnitType,unit} OR {kind:BuildingType,structure}; rally resolves owned producer. ");
            text.Append("resource:Food|Wood|Gold|Stone; sourceKind:Any or Food=Sheep|Berries|Farm,Wood=Tree,Gold=GoldMine,Stone=StoneMine. "
                + "constraints?<=4 objects type:NoConstruction;PreferredWorkers mode:IdleOnly;ProtectedResource resource,amount?:0..200;MaximumQueue amount1..8;ResourceSource resource,sourceKind. ");
            return text.ToString();
        }

        private static string DynamicVocabulary()
        {
            var text = new StringBuilder("DynamicPlan for repeated placed construction/shared workers: {outcome:DynamicPlan,version:1,nodes:[{id:symbol,mechanic,parameters:{},inputs:{},dependsOn:[]}]}. One shared worker selection, partition disjointly; exact producer set for explicitly new buildings. Registry fields below use name:type; ? optional; input refs also in dependsOn. ");
            foreach (var primitive in CommanderDynamicPrimitiveRegistry.All)
            {
                text.Append(primitive.Id).Append(" p[");
                foreach (var field in primitive.Parameters) text.Append(field.Name).Append(':').Append(field.Kind).Append(field.Required ? ";" : "?;");
                text.Append("] in[");
                foreach (var input in primitive.Inputs) text.Append(input.Role).Append(':').Append(input.Accepts).Append(input.Required ? ";" : "?;");
                text.Append("] ->").Append(primitive.Result).Append('.');
            }
            return text.Append(" Max12 nodes,4 refs/node,depth5,total counts200. Canonical IDs from context only; relation Near|MapWest|MapEast, Near gap1; resource mode Visible|Worked. Simple known requests keep Request fast path. ").ToString();
        }

        private static JObject CompactContext(JObject source, bool includeStrategy)
        {
            var result = new JObject();
            foreach (string key in new[] { "age", "civilization", "resources", "population", "populationCap", "workerCounts", "tacticalStatus" })
                if (source[key] != null) result[key] = source[key].DeepClone();
            if (source["knowledge"] is JObject knowledge)
            {
                var facts = new JObject();
                var supportedIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (string field in new[] { "canonicalUnitIds", "canonicalBuildingIds" })
                    if (source[field] is JArray ids)
                        foreach (var id in ids) if (id.Type == JTokenType.String) supportedIds.Add((string)id);
                foreach (var property in knowledge.Properties())
                    if (property.Name != "policy" && (!(property.Value is JArray array) || array.Count > 0))
                    {
                        if (property.Value is JArray rows && rows.Count > 0 && rows.All(v => v is JObject))
                        {
                            var detachedRows = rows.OfType<JObject>().Select(v => (JObject)v.DeepClone()).ToArray();
                            if (property.Name == "units" || property.Name == "buildings")
                            {
                                // Existing availability/executable facts normally express this already.
                                // Retain an explicit override only when the current adapter differs.
                                bool differs = detachedRows.Any(row => supportedIds.Contains((string)row["id"])
                                    != ((bool?)row["availableForCivilization"] == true && (bool?)row["commanderExecutable"] == true));
                                if (differs) foreach (var row in detachedRows)
                                    row["capabilitySupported"] = supportedIds.Contains((string)row["id"]);
                            }
                            var columns = detachedRows.SelectMany(v => v.Properties().Select(p => p.Name))
                                .Distinct(StringComparer.Ordinal).ToArray();
                            facts[property.Name + "Columns"] = new JArray(columns);
                            facts[property.Name] = new JArray(detachedRows.Select(row => new JArray(columns.Select(column =>
                                column == "cost" && row[column] is JObject cost
                                    ? (JToken)new JArray(new[] { "food", "wood", "gold", "stone" }.Select(resource => cost[resource]?.DeepClone() ?? JValue.CreateNull()))
                                    : row[column]?.DeepClone() ?? JValue.CreateNull()))));
                        }
                        else facts[property.Name] = property.Value.DeepClone();
                    }
                if (facts.Count > 0) result["knowledge"] = facts;
            }
            foreach (string key in new[] { "ownedUnitCounts", "ownedBuildingCounts" })
            {
                var counts = new JObject();
                if (source[key] is JObject all)
                    foreach (var value in all.Properties())
                        if (value.Value.Type == JTokenType.Integer && (value.Value.Value<int>() != 0 || value.Name == "Villager" || value.Name == "Scout")) counts[value.Name] = value.Value.DeepClone();
                result[key] = counts;
            }
            // Public IDs/names/availability/capability flags occur together in detached fact rows.
            // Avoid a duplicate map; knowledge still grants no execution support or authority.
            if (includeStrategy && source["strategyCapabilities"] != null) result["strategyCapabilities"] = source["strategyCapabilities"].DeepClone();
            return result;
        }
    }
}



## New file Assets/Scripts/AI/Commander/Phase5C/CommanderProviderUsage.cs

using System;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    // Reported observations only: no headers, response text, credentials or game authority.
    public sealed class CommanderProviderUsage
    {
        public long? PromptTokens { get; }
        public long? CompletionTokens { get; }
        public long? TotalTokens { get; }
        public long? CachedTokens { get; }
        public long? ReasoningTokens { get; }
        public decimal? Cost { get; }
        public bool HasReportedUsage { get; }
        public static CommanderProviderUsage Unknown { get; } = new CommanderProviderUsage(null);

        private CommanderProviderUsage(JObject value)
        {
            HasReportedUsage = value != null;
            PromptTokens = NonnegativeInteger(value?["prompt_tokens"]);
            CompletionTokens = NonnegativeInteger(value?["completion_tokens"]);
            TotalTokens = NonnegativeInteger(value?["total_tokens"]);
            CachedTokens = NonnegativeInteger((value?["prompt_tokens_details"] as JObject)?["cached_tokens"]);
            ReasoningTokens = NonnegativeInteger((value?["completion_tokens_details"] as JObject)?["reasoning_tokens"]);
            try
            {
                JToken cost = value?["cost"];
                if (cost != null && (cost.Type == JTokenType.Float || cost.Type == JTokenType.Integer))
                { decimal amount = cost.Value<decimal>(); if (amount >= 0) Cost = amount; }
            }
            catch (Exception error) when (error is OverflowException || error is FormatException || error is InvalidCastException) { }
        }

        public static CommanderProviderUsage Read(JObject envelope)
            => envelope?["usage"] is JObject usage ? new CommanderProviderUsage(usage) : Unknown;

        private static long? NonnegativeInteger(JToken value)
        {
            if (value == null || value.Type != JTokenType.Integer) return null;
            try { long count = value.Value<long>(); return count >= 0 ? count : (long?)null; }
            catch (Exception error) when (error is OverflowException || error is FormatException || error is InvalidCastException)
            { return null; }
        }
    }
}



## New file Assets/Tests/EditMode/CommanderPhase5CProviderBudgetTests.cs

using System;
using System.Collections.Generic;
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
                Assert.That(transport.Calls, Is.EqualTo(1), result.SafeExplanation + "; conservative estimate=" + provider.LastInputEstimate);
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

        private static object Read(object value, string name)
        { Assert.That(value, Is.Not.Null); var member = value.GetType().GetProperty(name); Assert.That(member, Is.Not.Null, name); return member.GetValue(value); }

        private sealed class CaptureTransport : ICommanderHttpTransport
        {
            internal string Body;
            internal int Calls;
            internal JObject Usage;
            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string body,
                IReadOnlyDictionary<string, string> headers, CancellationToken token)
            {
                Calls++; Body = body;
                var envelope = new JObject { ["choices"] = new JArray(new JObject {
                    ["finish_reason"] = "stop", ["message"] = new JObject {
                        ["content"] = "{\"outcome\":\"Answer\",\"message\":\"Offline fixture.\"}" } }) };
                if (Usage != null) envelope["usage"] = Usage;
                return Task.FromResult(new CommanderHttpResponse(200, envelope.ToString(Formatting.None)));
            }
        }
    }
}


