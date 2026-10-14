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
        // Transport-side model-visible UTF-8 payload safety ceiling. This is NOT an
        // actual model-token cap; endpoint usage remains the only token measurement.
        internal const int InputByteLimit = 32768;
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
            bool referencedBuilding = (knowledge?["buildings"] as JArray)?.OfType<JObject>()
                .Any(row => row["name"]?.Type == JTokenType.String) == true;
            bool construction = Has("build", "construct", "house", "houses", "mill", "farm", "farms", "barracks", "mine", "yard")
                || referencedBuilding && Has("place", "erect");
            bool allocation = Has("gather", "gathering", "food", "wood", "gold", "stone", "berries", "sheep", "farms", "split", "assign", "allocation", "allocate", "put");
            bool future = Has("next", "future", "births");
            bool production = Has("train", "produce", "recruit", "new", "more",
                "spearman", "spearmen", "spear", "spears", "archer", "archers", "knight", "knights");
            bool age = Has("age", "ages", "feudal", "castle", "imperial", "advance");
            bool action = Has("move", "march", "go", "guard", "protect", "defend", "defense", "patrol", "scout", "explore", "find", "discover", "enemy", "attack", "strike", "retreat", "withdraw", "repair", "fix", "rally", "garrison", "ungarrison")
                || Has("send") && Has("spearmen", "archers", "knights", "army", "troops", "military");
            bool markerMovement = Has("move", "march", "send") && Has("marker", "there");
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

            // A counted building without placement stays on the ordinary BuildStructure path.
            // Do not infer repeated *placed* construction from the number alone.
            bool sharedWorkers = Has("worker", "workers", "villager", "villagers")
                && Has("third", "split", "partition");
            bool repeatedPlaced = construction
                && Has("place", "placed", "near", "beside", "by", "next", "adjacent",
                    "around", "west", "east", "left", "right", "at")
                && HasRepeatedReferencedBuilding(search, knowledge);
            bool restrictedProducers = production && Has("from", "out", "using", "in")
                && HasRepeatedReferencedBuilding(search, knowledge);
            bool repeated = sharedWorkers || repeatedPlaced || restrictedProducers || Has("split", "partition", "third");
            bool composedWoodline = Has("all") && Has("wood", "woodline", "trees", "tree")
                && Has("west", "western", "left") && Has("town", "center", "tc");
            bool resourceLocationComposition = !composedWoodline && Has("woodline", "trees", "tree")
                && (construction || action) && Has("near", "right", "east", "top", "north", "working", "worked");
            var regularSelected = new HashSet<CommanderSemanticNodeType>(selected);
            if (sharedWorkers)
            {
                // One shared selection/partition cannot be expressed by independent
                // Request nodes; the registered DynamicPlan primitives keep the same effects.
                regularSelected.Remove(CommanderSemanticNodeType.BuildStructure);
                regularSelected.Remove(CommanderSemanticNodeType.AllocateWorkers);
                regularSelected.Remove(CommanderSemanticNodeType.EnsureUnitCount);
                regularSelected.Remove(CommanderSemanticNodeType.MoveUnits);
            }
            else if (repeatedPlaced)
            {
                // A placed count and its exact new-building production are represented
                // by the registered location/build/produce graph, not legacy Request.
                regularSelected.Remove(CommanderSemanticNodeType.BuildStructure);
                regularSelected.Remove(CommanderSemanticNodeType.EnsureUnitCount);
                regularSelected.Remove(CommanderSemanticNodeType.AllocateWorkers);
            }
            // Restore the proven explicit JSON/discriminator and fidelity contract for
            // ordinary requests. Compact retrieval remains only for Phase 5C forms not
            // described by that contract, and for the registered DynamicPlan graph.
            string instruction = request.IsReadOnlyQuestion ? ReadOnlyRules
                : OpenRouterCommanderProvider.SemanticInstruction
                    + OpenRouterCommanderProvider.SemanticFidelityCheck
                    + " Phase 5C construction: unplaced BuildStructure count supports 1..20 under deterministic default placement; a placed Request count is exactly 1. Repeated explicitly placed construction uses DynamicPlan. Never reject an ordinary counted build because no placement was requested. Build followed by a unit name means production; build followed by a building name means construction. Archery in building context means Archery Range. "
                    + " Context capabilities and requested knowledge objects define known content and supported routes. Each named object keeps its id, contentExists, availableForCivilization, commanderExecutable and capabilitySupported together. These current game facts take precedence over earlier conversational claims about support. Current age, resources, producer counts and population are execution readiness, not unknown content. Omitted facts do not prove unavailability. Never ask players to confirm canonical IDs or game-data availability. "
                    + CompositionExamples
                    + " Phase 5C clarification extension: " + ReplyRules
                    + " Supported clarification slot paths: " + CommanderRequestGraphDraft.SupportedSlotPathsJson + ". ";
            if (!request.IsReadOnlyQuestion)
            {
                var extraActions = new HashSet<CommanderSemanticNodeType>(regularSelected.Where(type =>
                    type == CommanderSemanticNodeType.GarrisonUnits
                    || type == CommanderSemanticNodeType.UngarrisonBuilding
                    || type == CommanderSemanticNodeType.DiscoverEnemy));
                if (extraActions.Count > 0)
                    instruction += " Additional typed executable Phase 5C node forms (same strict JSON type discriminator): "
                        + Vocabulary(extraActions);
                if (sharedWorkers || repeatedPlaced)
                    instruction += " For this known registered shared-worker or repeated-placed composition, use DynamicPlan ONLY for those effects; do not emit a partial Request. ";
            }
            if (repeated && !request.IsReadOnlyQuestion)
            {
                instruction += DynamicVocabulary(sharedWorkers, repeatedPlaced, selected, knowledge);
                if (restrictedProducers)
                {
                    instruction += " For production out of/from/using a stated number of producers, use select-structures{building:canonical,count:requested producer count} -> StructureSet and produce inputs producers referencing that selection. This selects existing eligible owned producers, not new buildings. Only an explicit request to build producers adds a build node. If existing-versus-new meaning is genuinely ambiguous, ask once and retain the full draft. Preserve TargetTotal versus explicitly New. ";
                    instruction += ExistingProducerExample(knowledge);
                }
            }
            else if (composedWoodline && !request.IsReadOnlyQuestion) instruction += ComposedWoodlineVocabulary;
            else if (resourceLocationComposition && !request.IsReadOnlyQuestion)
                instruction += ResourceLocationVocabulary;
            if (markerMovement && !request.IsReadOnlyQuestion)
                instruction += MarkerMovementVocabulary + (!Has("train", "produce", "recruit", "build", "make", "new", "more", "additional")
                    ? ExistingMarkerMovementExample(knowledge) : string.Empty);
            if (!request.IsReadOnlyQuestion)
            {
                bool dynamicAvailable = repeated || composedWoodline || resourceLocationComposition || markerMovement;
                if (!dynamicAvailable)
                    instruction = instruction.Replace("Outcomes are Request, DynamicPlan, Clarify, Unsupported, Answer.",
                        "Outcomes are Request, Clarify, Unsupported, Answer.");
                instruction += " DynamicPlan is never a message-only outcome: it requires the complete version 1 typed nodes graph and only applies when its registered representation is retrieved above. Otherwise use the complete Request form, or Clarify/Unsupported with a message if the meaning cannot be represented. ";
                instruction += " FINAL route check: resolvedNames rows are exact shared catalog matches, not typo suggestions. Use their category; DynamicPlan unit/building fields require the row id (unit:N/building:Name), while legacy Request name fields use canonicalToken; do not ask the player to confirm a canonical ID or an exactly resolved alias. A Building row belongs in BuildStructure.structure or build.parameters.building; a Unit row belongs in select-units.parameters.unit for existing-unit movement, or EnsureUnitCount.unit/produce.parameters.unit only for requested production. For example Archery resolves to Building/ArcheryRange, never an Archer unit. ";
                if (repeatedPlaced && Has("around"))
                    instruction += " The requested around relation is mandatory: connect resolve-location.inputs.structures to the NEW anchor build result, then the repeated build.inputs.location to that resolver, with all matching dependsOn references. Two independent build nodes DROP the requested around relation and are not a complete answer. Never substitute an existing anchor. ";
            }
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
            if (candidate.EstimatedInputTokens < InputByteLimit) return true;
            if (!optionalStripped && StripOptionalHints(knowledge))
            {
                optionalStripped = true;
                continue;
            }
            reason = "Commander input limit: the complete request and required canonical context cannot fit safely; nothing was submitted. Edit the request or resolve its pending question.";
            return false;
            }
        }

        private static bool HasRepeatedReferencedBuilding(string search, JObject knowledge)
        {
            if (!(knowledge?["buildings"] is JArray buildings)) return false;
            const string quantity = @"\b(?:two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen|twenty|[2-9]|1[0-9]|20)\s+";
            foreach (JObject row in buildings.OfType<JObject>())
            {
                string name = (string)row["name"];
                if (string.IsNullOrWhiteSpace(name)) continue;
                string normalized = Regex.Replace(name.Trim(), @"[^\p{L}\p{N}]+", " ");
                string phrase = string.Join(@"\s+", normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(Regex.Escape));
                if (Regex.IsMatch(search, quantity + @"(?:[\p{L}]+\s+){0,2}" + phrase + @"(?:s|es)?\b",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return true;
            }
            return false;
        }

        private static string ExistingMarkerMovementExample(JObject knowledge)
        {
            var named = (knowledge?["resolvedNames"] as JArray ?? new JArray()).OfType<JObject>()
                .Where(row => (string)row["category"] == "Unit")
                .Select(row => (string)row["id"]).Distinct(StringComparer.Ordinal).ToArray();
            if (named.Length != 1) return string.Empty;
            var graph = new JObject { ["outcome"] = "DynamicPlan", ["version"] = 1,
                ["nodes"] = new JArray(
                    new JObject { ["id"] = "units", ["mechanic"] = "select-units", ["parameters"] = new JObject { ["unit"] = named[0], ["countMode"] = "AllMatching" }, ["inputs"] = new JObject(), ["dependsOn"] = new JArray() },
                    new JObject { ["id"] = "destination", ["mechanic"] = "resolve-location", ["parameters"] = new JObject { ["anchor"] = "PlayerMarker", ["relation"] = "Near", ["clearGapTiles"] = 1 }, ["inputs"] = new JObject(), ["dependsOn"] = new JArray() },
                    new JObject { ["id"] = "move", ["mechanic"] = "move-units", ["parameters"] = new JObject(), ["inputs"] = new JObject { ["units"] = "units", ["location"] = "destination" }, ["dependsOn"] = new JArray("units", "destination") }) };
            return " For an unnumbered plural existing group, use this complete graph with this request's resolved unit id. For singular one or an explicit number replace countMode with count. Existing marker movement example: "
                + graph.ToString(Formatting.None) + " END marker example. ";
        }

        private static string ExistingProducerExample(JObject knowledge)
        {
            var named = new HashSet<string>((knowledge?["resolvedNames"] as JArray ?? new JArray()).OfType<JObject>()
                .Where(row => (string)row["category"] == "Unit").Select(row => (string)row["id"]), StringComparer.Ordinal);
            var unit = (knowledge?["units"] as JArray)?.OfType<JObject>().FirstOrDefault(row => named.Contains((string)row["id"])
                && (bool?)row["availableForCivilization"] == true && (bool?)row["commanderExecutable"] == true
                && row["producer"]?.Type == JTokenType.String);
            if (unit == null) return string.Empty;
            var example = new JObject { ["outcome"] = "DynamicPlan", ["version"] = 1,
                ["nodes"] = new JArray(new JObject { ["id"] = "sources", ["mechanic"] = "select-structures",
                    ["parameters"] = new JObject { ["building"] = "building:" + (string)unit["producer"], ["count"] = 5 },
                    ["inputs"] = new JObject(), ["dependsOn"] = new JArray() },
                new JObject { ["id"] = "units", ["mechanic"] = "produce",
                    ["parameters"] = new JObject { ["unit"] = (string)unit["id"], ["count"] = 50, ["quantityMode"] = "TargetTotal" },
                    ["inputs"] = new JObject { ["producers"] = "sources" }, ["dependsOn"] = new JArray("sources") }) };
            return " Complete existing-producer JSON example from this request's canonical catalog (adapt both counts and quantityMode to the actual player meaning; never add construction): " + example.ToString(Formatting.None);
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
                if (compactKnowledge?[table] is JArray namedRows)
                    foreach (var row in namedRows.OfType<JObject>())
                        if (row["id"]?.Type == JTokenType.String) present.Add((string)row["id"]);
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
        private const string CompositionExamples =
            " Complete Request composition examples; adapt canonical names/counts/quantityMode to the actual request, never add effects. A self-contained new request does not inherit old actions or old unanswered questions from semantic memory. Resolve 'it' against a named antecedent in the current utterance first. Only the active bounded pending draft carries unresolved slots; historical clarification text cannot append movement or any omitted action. "
            + "Explicit new construction supplying requested production: {\"outcome\":\"Request\",\"executionOrder\":\"Sequential\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1,\"builders\":{\"state\":\"Eligible\",\"count\":1}},{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4,\"quantityMode\":\"TargetTotal\",\"producerFromNode\":0,\"dependsOn\":[0]}]}. If the player asks NEW units use New, preserving that count. 'from it' requires this exact newly built producer. Future building results are symbolic dependencies, never missing current buildings. "
            + "Explicit new production then movement: {\"outcome\":\"Request\",\"executionOrder\":\"Sequential\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4,\"quantityMode\":\"New\"},{\"type\":\"MoveUnits\",\"unitSelector\":\"Spearman\",\"count\":4,\"location\":\"PlayerMarker\",\"resultFromNode\":0,\"dependsOn\":[0]}]}. PlayerMarker stays symbolic; absent marker is resolved locally by the game without discarding the graph. The move uses only that production result. Counts in these examples are illustrative; preserve the player's quantities. ";
        private static bool MatchesAction(CommanderCapabilityActionType type, HashSet<string> words)
        {
            bool Has(params string[] terms) => terms.Any(words.Contains);
            switch (type)
            {
                case CommanderCapabilityActionType.MoveUnits: return Has("move", "send", "march", "go");
                case CommanderCapabilityActionType.ScoutArea: return Has("scout", "explore") && !Has("find", "discover");
                case CommanderCapabilityActionType.DiscoverEnemy: return Has("find", "discover") && Has("enemy", "base", "town", "center");
                case CommanderCapabilityActionType.GarrisonUnits: return Has("garrison");
                case CommanderCapabilityActionType.UngarrisonBuilding: return Has("ungarrison", "evacuate");
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
        private const string BaseRules =
            "JSON only,strict fields/enums; never authorize IDs,coords,commands,hidden/extra goals. Keep counts,negations,actors,sources,restrictions,order,exact refs;unknown unknown. "
            + "After/then depends on completion; a/an=1,number words,left=MapWest. ";
        private const string RequestRules =
            "Request{outcome:Request,nodes:1..4,executionOrder:Independent|Sequential|DependencyGraph}; dependsOn:[prior0-based]; resultFromNode=exact-new,producerFromNode=explicit-new. ";
        private const string ReplyRules =
            "Clarify{outcome:Clarify,message<=180,pending:{executionOrder,nodes:partial1..4},missingSlots:[{node,path,type:Confirmation|Count|SourceKind|CanonicalName|Location,alternatives:[typed values]}]};retain known leaves/edges/refs. Count alternatives are integers; other alternatives are canonical strings. Omit only the missing leaf. Confirmation is only canonical-name confirmation: retain that leaf and offer only its same value; never use it for quantityMode, resource shortages or plan approval. Omitted quantityMode defaults TargetTotal even with existing units; explicit new/more/additional means New. Readiness shortages are planner prerequisites, not missing semantic slots. For missing movement location use Location path location alternatives PlayerMarker|PlayerBase; for construction use Location path placement.anchor alternatives PlayerMarker|MyTownCenter and retain placement.relation Near. A world marker fills only that missing location locally. Prefer ordinary deterministic placement when unspecified and safe; do not ask merely because placement was omitted. Worker pending+missingFields valid;not approval. Answer/Unsupported{outcome,message<=180};no nodes. ";
        private const string CommonRules = BaseRules + RequestRules + ReplyRules;

        private static string Vocabulary(HashSet<CommanderSemanticNodeType> selected)
        {
            var text = new StringBuilder();
            foreach (CommanderSemanticNodeType type in Enum.GetValues(typeof(CommanderSemanticNodeType)))
            {
                if (!selected.Contains(type)) continue;
                switch (type)
                {
                    case CommanderSemanticNodeType.BuildStructure:
                        text.Append("BuildStructure{structure:canonical,count:1,builders:{state:Eligible(default)|IdleOnly(strict),count:1},placement?}. Placed count1; repeated placed=>DynamicPlan; unplaced count:1..20. placement TC/Barracks:{anchor:MyTownCenter|MyBarracks,relation:Near|MapWest|MapEast,clearGapTiles?:1..20,ordinal?:1..8 TC-only}; resource:{anchor:VisibleResource(default)|WorkedResource(explicit),relation:Near,resource,sourceKind?}. Near gap1,no resource ordinal. "); break;
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
                    .Append("unitSelector:Military|Scout|Villagers|Spearman|Archer|Knight|DamagedMilitary; location:PlayerBase|WorkedResource|VisibleResource|VisibleEnemy|RelativeToSelectedUnits. Ordinary ScoutArea is one visible point, not enemy discovery; no radius. Keep explicit source/side/current-resource criteria; use the typed DynamicPlan composition when advertised, otherwise Clarify/Unsupported, never drop criteria. ");
                if (actions.Contains(CommanderSemanticNodeType.AttackTarget) || actions.Contains(CommanderSemanticNodeType.RepairTarget))
                    text.Append("Attack/Repair target?{kind:UnitType,unit}|{kind:BuildingType,structure}. ");
                if (actions.Contains(CommanderSemanticNodeType.SetRallyPoint)) text.Append("Rally uses owned producer. ");
                if (actions.Contains(CommanderSemanticNodeType.GarrisonUnits))
                    text.Append("GarrisonUnits{unitSelector:Villagers,countMode:AllMatching omit count OR count:1..50,location:PlayerBase,structure:owned shelter type}; exact shelter, actual native entry/capacity and protected/unaccommodated remainder; no other shelter fallback. ");
                if (actions.Contains(CommanderSemanticNodeType.UngarrisonBuilding))
                    text.Append("UngarrisonBuilding{location:PlayerBase,structure:owned shelter type}; building-wide egress, not selective. ");
                if (actions.Contains(CommanderSemanticNodeType.DiscoverEnemy))
                    text.Append("DiscoverEnemy{unitSelector:Scout,count:1,location:VisibleEnemy,target:{kind:UnitType,unit}|{kind:BuildingType,structure}}; finite known-frontier Scout+Move route, complete only on legitimately visible requested enemy type; no hidden coordinates or resultFromNode. ");
            }
            text.Append("resource:Food|Wood|Gold|Stone; sourceKind:Any or Food=Sheep|Berries|Farm,Wood=Tree,Gold=GoldMine,Stone=StoneMine. "
                + "constraints?<=4 objects type:NoConstruction;PreferredWorkers mode:IdleOnly;ProtectedResource resource,amount?:0..200;MaximumQueue amount1..8;ResourceSource resource,sourceKind. ");
            return text.ToString();
        }

        private static string DynamicVocabulary(bool sharedWorkers, bool repeatedPlaced,
            HashSet<CommanderSemanticNodeType> selected, JObject knowledge)
        {
            var text = new StringBuilder("Shared workers/placed repeats: DynamicPlan{outcome:DynamicPlan,version:1,nodes:[{id,mechanic,parameters:{},inputs:{},dependsOn:[]}]}; ");
            if (sharedWorkers) text.Append("One worker selection, disjoint partitions; ");
            if ((sharedWorkers || repeatedPlaced) && selected.Contains(CommanderSemanticNodeType.EnsureUnitCount))
                text.Append("produce.producers=exact new build result; ");
            text.Append("Registry p=parameters,in=inputs,?optional; typed input refs also in dependsOn. ");
            // This is contract retrieval, not a capability grant: every advertised primitive
            // still comes from the registered schema and the game binds all concrete actors.
            var needed = new HashSet<CommanderDynamicMechanic>();
            if (sharedWorkers)
            {
                needed.UnionWith(new[] { CommanderDynamicMechanic.PartitionWorkers,
                    CommanderDynamicMechanic.AllocateWorkers,
                    CommanderDynamicMechanic.ResolveLocation, CommanderDynamicMechanic.Build });
                if (selected.Contains(CommanderSemanticNodeType.MoveUnits))
                    needed.Add(CommanderDynamicMechanic.MoveWorkers);
                if (selected.Contains(CommanderSemanticNodeType.EnsureUnitCount))
                    needed.Add(CommanderDynamicMechanic.Produce);
            }
            if (repeatedPlaced)
            {
                needed.UnionWith(new[] { CommanderDynamicMechanic.ResolveLocation,
                    CommanderDynamicMechanic.Build });
                if (selected.Contains(CommanderSemanticNodeType.EnsureUnitCount))
                    needed.Add(CommanderDynamicMechanic.Produce);
                if (selected.Contains(CommanderSemanticNodeType.AllocateWorkers))
                    needed.Add(CommanderDynamicMechanic.AllocateWorkers);
            }
            // Close required typed inputs using the authoritative registry descriptors.
            // This preserves source primitives when a new registered dependency is added;
            // unknown compositions retain the complete registry and may refuse on budget.
            if (needed.Count > 0)
            {
                bool changed;
                do
                {
                    changed = false;
                    foreach (var primitive in CommanderDynamicPrimitiveRegistry.All.Where(p => needed.Contains(p.Mechanic)))
                        foreach (var input in primitive.Inputs.Where(i => i.Required))
                        {
                            if (CommanderDynamicPrimitiveRegistry.All.Any(p => needed.Contains(p.Mechanic)
                                && p.Mechanic != primitive.Mechanic && p.Result == input.Accepts
                                && p.Inputs.All(i => !i.Required))) continue;
                            var source = CommanderDynamicPrimitiveRegistry.All.FirstOrDefault(p =>
                                p.Result == input.Accepts && p.Mechanic != primitive.Mechanic
                                && p.Inputs.All(i => !i.Required) && p.Effect == CommanderDynamicEffectClass.Selection);
                            if (source == null) source = CommanderDynamicPrimitiveRegistry.All.FirstOrDefault(p =>
                                p.Result == input.Accepts && p.Mechanic != primitive.Mechanic
                                && p.Inputs.All(i => !i.Required));
                            if (source == null) { needed.Clear(); break; }
                            changed |= needed.Add(source.Mechanic);
                        }
                } while (changed && needed.Count > 0);
            }
            foreach (var primitive in CommanderDynamicPrimitiveRegistry.All)
            {
                if (needed.Count > 0 && !needed.Contains(primitive.Mechanic)) continue;
                text.Append(primitive.Id).Append(" p[");
                foreach (var field in primitive.Parameters) text.Append(field.Name).Append(':').Append(field.Kind).Append(field.Required ? ";" : "?;");
                text.Append("] in[");
                foreach (var input in primitive.Inputs) text.Append(input.Role).Append(':').Append(input.Accepts).Append(input.Required ? ";" : "?;");
                text.Append("] ->").Append(primitive.Result).Append('.');
            }
            text.Append(" Types: Count=int1..200 (build<=20); Offset=int0..200; ClearGapTiles=int1..20 (Near=1); UnitId/BuildingId=context canonical IDs; Resource/SourceKind=compatible values above; WorkerState=Any|Idle|Gathering; WorkerCountMode=AllMatching; Anchor=MyTownCenter|MyBarracks; Relation=Near|MapWest|MapEast; QuantityMode=New|TargetTotal. select-workers: count XOR countMode; AllMatching needs non-Any state and cannot partition; currentResource requires Gathering. resolve-location: exactly one anchor/structures/resources; resource+sourceKind together only for MyTownCenter+MapWest. Max12 nodes,4 refs/node,depth5,total non-partition counts200. New=exact new; TargetTotal=existing+new. ");
            if (repeatedPlaced && HasCanonicalBuilding(knowledge, "building:Mill")
                && HasCanonicalBuilding(knowledge, "building:Farm"))
                text.Append(" For repeated buildings around an exact newly requested building, use the first build result as the resolve-location structures input, never select an existing structure. Example: ")
                    .Append("{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[")
                    .Append("{\"id\":\"mill\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Mill\",\"count\":1},\"inputs\":{},\"dependsOn\":[]},")
                    .Append("{\"id\":\"site\",\"mechanic\":\"resolve-location\",\"parameters\":{\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{\"structures\":\"mill\"},\"dependsOn\":[\"mill\"]},")
                    .Append("{\"id\":\"farms\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:Farm\",\"count\":8},\"inputs\":{\"location\":\"site\"},\"dependsOn\":[\"site\"]}]}. ")
                    .Append("The first count is one exact new anchor; the second count is the full repeated request. Adjust canonical buildings and counts only to match the player's request. ");
            return text.ToString();
        }

        private static bool HasCanonicalBuilding(JObject knowledge, string canonicalId)
        {
            return (knowledge?["buildings"] as JArray)?.OfType<JObject>()
                .Any(row => string.Equals((string)row["id"], canonicalId, StringComparison.Ordinal)) == true;
        }

        private const string ComposedWoodlineVocabulary =
            "For all current-Wood villagers and west-of-owned-TC Wood/Tree: DynamicPlan version1 with select-workers{state:Gathering,currentResource:Wood,countMode:AllMatching,omit count} -> WorkerSet; resolve-location{anchor:MyTownCenter,relation:MapWest,clearGapTiles:1,resource:Wood,sourceKind:Tree} -> LocationIntent. For explicit move, move-workers inputs workers:select-workers,location:resolve-location -> native Move (not Gather); for explicit gather, allocate-workers{resource:Wood,sourceKind:Tree} with same inputs -> native Gather. Include dependsOn refs; never replace ALL with count50 or silently change Move to Gather. Game binds owned workers/source/anchor and refuses overflow. No coordinates/IDs. ";

        private const string MarkerMovementVocabulary =
            "Existing-unit marker movement overrides the legacy Request count requirement: an unnumbered plural such as my archers is AllMatching, not a missing Count slot. Do not ask for count or manufacture a clarification. Base unit names select their current civilization replacement through the game resolver; a zero base-name count does not prove the replacement is absent. Never ask the player to choose between the base name and its civilization replacement. Canonical DynamicPlan unit values are row id values such as unit:2, never display-name canonicalToken strings. Existing-unit movement to a marker: use DynamicPlan version 1 with select-units -> UnitSet, resolve-location{anchor:PlayerMarker,relation:Near,clearGapTiles:1} -> LocationIntent, move-units inputs{units:selectionId,location:locationId} and dependsOn containing both references. select-units parameters use exactly one of unit:canonical unit:id or kind:AllUnits|Military|Scout|Villagers|Spearman|Archer|Knight|DamagedMilitary. Use canonical unit IDs from catalog for every named unit, plural, alias or civilization replacement. Use exactly one of count:1..50 for explicit numbers (or one singular unit) or countMode:AllMatching for unnumbered plurals/my archers/all. Optional typed exclusion is exactly one of excludeKind (same kind vocabulary) or excludeUnit:canonical unit:id; all except villagers means kind:AllUnits,excludeKind:Villagers,countMode:AllMatching, preserving scouts and other nonvillagers. Never change an explicit count to all, drop exclusions, substitute another unit type or add production to a move-existing request. Unknown group: clarify and retain movement plus destination. No coordinates or runtime IDs. For new produced result units continue using the existing Request dependency/resultFromNode route; never substitute a current-unit selection for a production result. ";

        private const string ResourceLocationVocabulary =
            "Composable woodline location override: when the player specifies a worked woodline, a side of their Town Center, or a woodline relative to action units, use only the registered DynamicPlan graph for that action, not a Request that drops the location criteria. DynamicPlan{outcome:DynamicPlan,version:1,nodes:[{id,mechanic,parameters:{},inputs:{},dependsOn:[]}]}. Each typed input reference must also occur in dependsOn. select-resources p{resource:Wood,sourceKind:Tree,mode:Visible|Worked,count:1,anchor?:MyTownCenter,relation?:MapEast|MapWest|MapNorth|MapSouth} in{units?:UnitSet,workers?:WorkerSet} -> ResourceSet. For 'my villagers are working', mode:Worked is mandatory; never replace with Visible. For 'right of my Town Center', anchor:MyTownCenter and relation:MapEast are mandatory; game filters the side before choosing. For 'top woodline' with selected Spearmen, omit anchor, set relation:MapNorth and input units from select-units; map north is the game's top. select-units p{unit?:canonical unit:id,kind?:AllUnits|Military|Scout|Villagers|Spearman|Archer|Knight|DamagedMilitary,excludeKind?:Villagers|Military|Scout,excludeUnit?:canonical unit:id,count?:1..50,countMode?:AllMatching} (unit/kind and count/countMode are exclusive pairs; excludeKind/excludeUnit are exclusive) -> UnitSet; use countMode:AllMatching without count for unnumbered 'my Spearmen', and count for an explicit number. Select units once and retain the selection for the move. resolve-location p{relation:Near,clearGapTiles:1} in{resources:ResourceSet} -> LocationIntent. For construction, build p{building:canonical,count:1} in{location:LocationIntent}; for movement, move-units in{units:UnitSet,location:LocationIntent} -> native Move with legal resource-side formation arrival, not Gather. All actor identities, resource IDs, visibility and paths are chosen by the game, never provider coordinates. If no suitable visible resource exists, preserve the explicit restriction and ask for a marker. ";

        private static JObject CompactContext(JObject source, bool includeStrategy)
        {
            var result = new JObject();
            foreach (string key in new[] { "age", "civilization", "resources", "population", "populationCap", "workerCounts", "tacticalStatus",
                "unitCapabilities", "structureCapabilities", "resourceCapabilities", "actionCapabilities", "selectorCapabilities" })
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
                            facts[property.Name] = new JArray(detachedRows);
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
