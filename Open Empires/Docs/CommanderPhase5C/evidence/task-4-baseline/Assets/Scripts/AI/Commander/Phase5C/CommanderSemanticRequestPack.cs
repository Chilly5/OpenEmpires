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
            var knowledge = context["knowledge"] as JObject;
            if ((bool?)knowledge?["requiredReferenceOverflow"] == true)
            {
                reason = "Commander input limit: required canonical context cannot fit safely; the complete request was not submitted.";
                return false;
            }
            string search = request.PlayerMessage + " " + request.SerializedPendingClarification;
            var words = new HashSet<string>(Regex.Matches(search.ToLowerInvariant(), @"[\p{L}\p{N}]+")
                .Cast<Match>().Select(m => m.Value), StringComparer.Ordinal);
            bool Has(params string[] terms) => terms.Any(words.Contains);
            bool fallbackAll = false;
            bool construction = Has("build", "construct", "house", "houses", "mill", "farm", "farms", "barracks", "mine", "yard");
            bool allocation = Has("gather", "gathering", "food", "wood", "gold", "stone", "berries", "sheep", "farms", "split", "assign", "allocation", "allocate", "put");
            bool future = Has("next", "future", "births");
            bool production = Has("train", "produce", "recruit", "new", "more", "spearmen", "archers", "knights");
            bool age = Has("age", "ages", "feudal", "castle", "imperial", "advance");
            bool action = Has("move", "march", "go", "guard", "protect", "defend", "defense", "patrol", "scout", "explore", "find", "discover", "enemy", "attack", "strike", "retreat", "withdraw", "repair", "fix", "rally", "garrison", "ungarrison")
                || Has("send") && Has("spearmen", "archers", "knights", "army", "troops", "military");
            bool research = Has("research", "technology", "upgrade");
            bool strategy = Has("strategy", "strategic");
            if (!construction && !allocation && !future && !production && !age && !action && !research && !strategy)
            {
                construction = allocation = future = production = age = action = research = strategy = true;
                fallbackAll = true;
            }
            var selected = new HashSet<CommanderSemanticNodeType>();
            if (construction) selected.Add(CommanderSemanticNodeType.BuildStructure);
            if (allocation) selected.Add(CommanderSemanticNodeType.AllocateWorkers);
            if (fallbackAll || allocation && Has("allocation", "allocate", "set")) selected.Add(CommanderSemanticNodeType.SetResourceAllocation);
            if (future) selected.Add(CommanderSemanticNodeType.WatchFutureUnits);
            if (production) selected.Add(CommanderSemanticNodeType.EnsureUnitCount);
            if (age) selected.Add(CommanderSemanticNodeType.ReachAge);
            if (research) selected.Add(CommanderSemanticNodeType.ResearchTechnology);
            if (strategy) selected.Add(CommanderSemanticNodeType.StrategicObjective);
            if (action)
                foreach (CommanderCapabilityActionType capability in Enum.GetValues(typeof(CommanderCapabilityActionType)))
                    if (capability != CommanderCapabilityActionType.ResearchTechnology
                        && (fallbackAll || MatchesAction(capability, words))
                        && Enum.TryParse(capability.ToString(), out CommanderSemanticNodeType type)) selected.Add(type);

            string instruction = request.IsReadOnlyQuestion ? ReadOnlyRules : CommonRules + Vocabulary(selected);
            // Contract retrieval only: quantities on future units must not imply repeated buildings.
            bool repeated = Has("split", "partition") || construction && (Has("third") || Regex.IsMatch(search,
                @"\b(?:build|construct|make)\s+(?:two|three|four|five|six|seven|eight|nine|ten|[2-9]|1[0-9]|20)\b",
                RegexOptions.IgnoreCase));
            if (repeated && !request.IsReadOnlyQuestion) instruction += DynamicVocabulary();
            bool optionalStripped = false;
            while (true)
            {
            string packedInstruction = instruction;
            if (knowledge?["nameInterpretations"] is JArray possibleHints
                && possibleHints.OfType<JObject>().Any(hint => (bool?)hint["ambiguous"] == false))
                packedInstruction += " Similar-name hints are interpretation only; Clarify before Request, never substitute a suggested name as authority.";
            JObject compact = CompactContext(context, strategy);
            if (knowledge?["requiredCanonicalIds"] is JArray requiredIds
                && !RequiredRowsPresent(requiredIds, compact["knowledge"] as JObject))
            {
                reason = "Commander input limit: required canonical facts are missing from the complete context; nothing was submitted.";
                return false;
            }
            string compactContext = compact.ToString(Formatting.None);
            // Essential turn/pending data is never truncated to squeeze through the budget.
            string user = "Context:" + compactContext + "\nMemory:" + request.SerializedSemanticMemory
                + "\nFacts:" + request.QuestionFacts + "\nBounded pending:" + request.SerializedPendingClarification
                + "\nPlayer:" + request.PlayerMessage;
            var body = new JObject { ["model"] = model,
                ["messages"] = new JArray(new JObject { ["role"] = "system", ["content"] = packedInstruction },
                    new JObject { ["role"] = "user", ["content"] = user }),
                ["max_tokens"] = maxOutputTokens, ["reasoning"] = new JObject { ["effort"] = "none" } };
            var sections = new Dictionary<string, int> {
                ["contract"] = Encoding.UTF8.GetByteCount(packedInstruction),
                ["context"] = Encoding.UTF8.GetByteCount(compactContext),
                ["memory"] = Encoding.UTF8.GetByteCount(request.SerializedSemanticMemory),
                ["questionFacts"] = Encoding.UTF8.GetByteCount(request.QuestionFacts),
                ["pending"] = Encoding.UTF8.GetByteCount(request.SerializedPendingClarification),
                ["utterance"] = Encoding.UTF8.GetByteCount(request.PlayerMessage), ["messageMargin"] = MessageMargin };
            var candidate = new CommanderSemanticRequestPack(body, sections);
            pack = candidate; // Observed packing metrics survive a rejection; this is not dispatch authority.
            if (candidate.EstimatedInputTokens < InputLimit) return true;
            if (!optionalStripped && StripOptionalHints(knowledge))
            {
                optionalStripped = true;
                continue;
            }
            reason = "Commander input limit: the complete request and required canonical context cannot fit safely; nothing was submitted. Edit the request or resolve its pending question.";
            return false;
            }
        }

        private static bool StripOptionalHints(JObject knowledge)
        {
            if (knowledge == null) return false;
            var required = new HashSet<string>((knowledge["requiredCanonicalIds"] as JArray ?? new JArray())
                .Where(x => x.Type == JTokenType.String).Select(x => (string)x), StringComparer.Ordinal);
            var optional = new HashSet<string>((knowledge["optionalCanonicalIds"] as JArray ?? new JArray())
                .Where(x => x.Type == JTokenType.String).Select(x => (string)x), StringComparer.Ordinal);
            bool stripped = false;
            foreach (string table in new[] { "units", "buildings" })
                if (knowledge[table] is JArray rows)
                    foreach (JObject row in rows.OfType<JObject>().Where(row =>
                        optional.Contains((string)row["id"]) && !required.Contains((string)row["id"])).ToArray())
                    { row.Remove(); stripped = true; }
            if (knowledge["nameInterpretations"] is JArray interpretations)
                foreach (JObject hint in interpretations.OfType<JObject>()
                    .Where(hint => (bool?)hint["ambiguous"] == false).ToArray())
                { hint.Remove(); stripped = true; }
            return stripped;
        }

        private static bool RequiredRowsPresent(JArray required, JObject compactKnowledge)
        {
            var present = new HashSet<string>(StringComparer.Ordinal);
            foreach (string table in new[] { "units", "buildings" })
            {
                if (!(compactKnowledge?[table + "Columns"] is JArray columns)
                    || !(compactKnowledge[table] is JArray rows)) continue;
                int id = columns.Select((value, index) => (value: (string)value, index))
                    .Where(value => value.value == "id").Select(value => value.index).DefaultIfEmpty(-1).First();
                if (id < 0) continue;
                foreach (JArray row in rows.OfType<JArray>())
                    if (id < row.Count && row[id].Type == JTokenType.String) present.Add((string)row[id]);
            }
            return required.All(id => id.Type == JTokenType.String && present.Contains((string)id));
        }

        private const string ReadOnlyRules = "Return one JSON object, no markdown: {\"outcome\":\"Answer\",\"message\":\"brief grounded answer\"}, "
            + "{\"outcome\":\"Clarify\",\"message\":\"specific missing fact\"} or {\"outcome\":\"Unsupported\",\"message\":\"reason\"}. "
            + "Message<=180 chars. INFORMATION ONLY: no nodes/plans/approval. Use owned task facts; unknown facts remain unknown. No hidden state, IDs or commands.";
        private static bool MatchesAction(CommanderCapabilityActionType type, HashSet<string> words)
        {
            bool Has(params string[] terms) => terms.Any(words.Contains);
            switch (type)
            {
                case CommanderCapabilityActionType.MoveUnits: return Has("move", "send", "march", "go");
                case CommanderCapabilityActionType.ScoutArea: return Has("scout", "explore", "find", "discover");
                case CommanderCapabilityActionType.PatrolArea: return Has("patrol");
                case CommanderCapabilityActionType.SetRallyPoint: return Has("rally");
                case CommanderCapabilityActionType.AttackTarget: return Has("attack", "strike");
                case CommanderCapabilityActionType.DefendArea: return Has("defend", "defense", "guard", "protect");
                case CommanderCapabilityActionType.RetreatUnits: return Has("retreat", "withdraw");
                case CommanderCapabilityActionType.RepairTarget: return Has("repair", "fix");
                case CommanderCapabilityActionType.ResearchTechnology: return false;
                default: throw new InvalidOperationException("No capability retrieval mapping: " + type);
            }
        }
        private const string CommonRules =
            "JSON only,strict fields/enums; never authorize IDs,coords,commands,hidden/extra goals. Keep counts,negations,actors,sources,restrictions,order,exact refs;unknown unknown. "
            + "Request{outcome:Request,nodes:1..4,executionOrder:Independent|Sequential|DependencyGraph}; after/then=completion dependsOn:[prior0-based]; resultFromNode=exact-new,producerFromNode=explicit-new; a/an=1,number words,left=MapWest. "
            + "Clarify{outcome:Clarify,message<=180,pending:{executionOrder,nodes:partial1..4},missingSlots:[{node,path,type:Confirmation|Count|SourceKind|CanonicalName,alternatives:[canonical]}]};retain known leaves/edges/refs,omit only missing;Confirmation retains offer. Worker pending+missingFields valid;not approval. Answer/Unsupported{outcome,message<=180};no nodes.";

        private static string Vocabulary(HashSet<CommanderSemanticNodeType> selected)
        {
            var text = new StringBuilder();
            foreach (CommanderSemanticNodeType type in Enum.GetValues(typeof(CommanderSemanticNodeType)))
            {
                if (!selected.Contains(type)) continue;
                switch (type)
                {
                    case CommanderSemanticNodeType.BuildStructure:
                        text.Append("BuildStructure{structure:canonical,count:1..20,builders:{state:Eligible(default)|IdleOnly(strict),count:1},placement?}. placement TC/Barracks:{anchor:MyTownCenter|MyBarracks,relation:Near|MapWest|MapEast,clearGapTiles?:1..20,ordinal?:1..8 TC-only}; resource:{anchor:VisibleResource(default)|WorkedResource(explicit),relation:Near,resource,sourceKind?}. Near gap1,no resource ordinal. "); break;
                    case CommanderSemanticNodeType.SetResourceAllocation:
                        text.Append("SetResourceAllocation{resource,count:0..200} sets worker target; distinct from AllocateWorkers. "); break;
                    case CommanderSemanticNodeType.AllocateWorkers:
                        text.Append("AllocateWorkers{mode:SelectedCount(default)|Additional(more workers)|TargetTotal(keep total),countMode:Exact|AllMatching(snapshot),count:1..200 omit if AllMatching,workers:{state:Any|Idle|Gathering,currentResource?},destination:{resource,sourceKind?},resourceAmount?:1..1000000 paired resourceAmountMode:Stockpile|AdditionalGathered(player-wide)}. Amount!=worker count;assignment!=income. "); break;
                    case CommanderSemanticNodeType.EnsureUnitCount:
                        text.Append("EnsureUnitCount:unit canonical,count0..200,quantityMode:TargetTotal default|New. New/more=exact new count, no existing substitutes. Game handles prerequisites; extra buildings only explicit. "); break;
                    case CommanderSemanticNodeType.WatchFutureUnits:
                        text.Append("WatchFutureUnits{unit:canonical,count:1..50,producer:canonical,producerOrdinal?:1..8,action:Gather(Villager only),resource,sourceKind?} OR action:Patrol,location:WorkedResource,resource. Next actual births incl human queues,never train;ambiguous producer clarify. "); break;
                    case CommanderSemanticNodeType.ReachAge:
                        text.Append("ReachAge:targetAge:Feudal|Castle|Imperial|Next. Explicit2/3/4=Feudal/Castle/Imperial, not Next. "); break;
                    case CommanderSemanticNodeType.ResearchTechnology:
                        text.Append("ResearchTechnology:technology:BlacksmithDamage|BlacksmithDefense|Ballistics|SiegeEngineering|Chemistry|MurderHoles. "); break;
                    case CommanderSemanticNodeType.StrategicObjective:
                        text.Append("StrategicObjective only explicit player strategy; objective from context strategyCapabilities, constraints retained. "); break;
                    default:
                        if (!Enum.TryParse(type.ToString(), out CommanderCapabilityActionType _)
                            || type == CommanderSemanticNodeType.ResearchTechnology)
                            throw new InvalidOperationException("Semantic family has no compact contract: " + type);
                        break; // Registered capability actions share one field contract below.
                }
            }
            var actions = selected.Where(t => t != CommanderSemanticNodeType.ResearchTechnology
                && Enum.TryParse(t.ToString(), out CommanderCapabilityActionType _)).OrderBy(t => t).ToArray();
            if (actions.Length > 0)
            {
                text.Append("Action type:").Append(string.Join("|", actions))
                    .Append("; unitSelector,count:1..50,location,resource/structure?,resultFromNode/dependsOn?. ")
                    .Append("unitSelector:Military|Scout|Villagers|Spearman|Archer|Knight|DamagedMilitary; location:PlayerBase|WorkedResource|VisibleResource|VisibleEnemy|RelativeToSelectedUnits. ALL/current-resource workers/composed side+resource+TC location unsupported: Clarify/Unsupported,never drop criteria. VisibleEnemy cannot discover unseen base;point only,no radius. ");
                if (actions.Contains(CommanderSemanticNodeType.AttackTarget) || actions.Contains(CommanderSemanticNodeType.RepairTarget))
                    text.Append("Attack/Repair target?{kind:UnitType,unit}|{kind:BuildingType,structure}. ");
                if (actions.Contains(CommanderSemanticNodeType.SetRallyPoint)) text.Append("Rally uses owned producer. ");
            }
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
                    if (property.Name != "policy" && property.Name != "requiredCanonicalIds"
                        && property.Name != "optionalCanonicalIds"
                        && property.Name != "requiredReferenceOverflow"
                        && (!(property.Value is JArray array) || array.Count > 0))
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
