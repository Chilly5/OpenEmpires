# Task1 fix3 frozen incremental review package

Baseline: task-1-fix3-baseline snapshots (fix2 source, before this round).
No commits; only four listed source/test files changed in this round.
Requirements: task-1-fix3-brief.md and prior task-1-fix2-static-review.md.
Report: task-1-fix-report.md FIX3 section. Parent inspected final raw d268...46/46.
Prior baseline33/33 proves fix2; current46/46 covers39Phase5C plus7legacy controls.
First failures/interim failed budget guidance variant retained in report/artifacts.
No measured API token/native/UI/full-regression/build acceptance claimed.

Frozen SHA256:
- OpenRouterCommanderProvider.cs A319883A57525CA411615CBA84A032D846FF11A6AB2E9E4DC2356574102E2E75
- CommanderKnowledgeRetrieval.cs 6CF082D78F3421D04C9F4BBC290A10FF75F9831E7F94C9B9C4A4B7B7CD6D6DA9
- CommanderSemanticRequestPack.cs 2F57C17FA503AAEB7AF2ED019A146D187217FC15C96CCC55021787F24ACFD677
- CommanderPhase5CProviderBudgetTests.cs A5C683329D11CB66569BC77C37FBDC9D8EA54F879C85882E6DB358E026FFF307

## Incremental diffs
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-1-fix3-baseline\\OpenRouterCommanderProvider.cs.snapshot" b/Assets/Scripts/AI/Commander/Phase4A/OpenRouterCommanderProvider.cs
index 93ea016..216ab48 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-1-fix3-baseline\\OpenRouterCommanderProvider.cs.snapshot"
+++ b/Assets/Scripts/AI/Commander/Phase4A/OpenRouterCommanderProvider.cs
@@ -444,46 +444,62 @@ namespace OpenEmpires
                 if (response == null)
                 {
                     resultStage = "transport-null";
                     throw new OpenRouterFailure(Unavailable);
                 }
                 attemptHttpStatus = response.StatusCode;
                 if (run != null) { run.HttpStatusCode = attemptHttpStatus; Publish(run); }
                 else LastHttpStatusCode = attemptHttpStatus;
                 resultStage = response.StatusCode >= 200 && response.StatusCode <= 299 ? "http-success" : "http-failure";
                 Trace("http=" + response.StatusCode + ";durationMs=" + elapsed.ElapsedMilliseconds, run);
                 if (response.StatusCode < 200 || response.StatusCode > 299)
                     throw new OpenRouterFailure(HttpFailure(response.StatusCode));
+                // A 2xx status is transport success only. Every rejected envelope must
+                // have a failure stage even if a malformed token raises unexpectedly.
+                resultStage = "response-invalid-envelope";
                 try
                 {
+                    if (response.Body == null)
+                        throw new OpenRouterFailure("Commander AI returned an invalid response envelope.");
                     if (response.Body.Length > 65536)
                     {
                         resultStage = "response-oversize";
                         throw new OpenRouterFailure("Commander AI returned an oversized response.");
                     }
                     JObject result = JObject.Parse(response.Body);
                     attemptUsage = CommanderProviderUsage.Read(result);
                     if (run != null) { run.Usage = attemptUsage; Publish(run); }
                     else LastUsage = attemptUsage;
-                    if (result["error"] is JObject error)
+                    if (result["error"] != null && result["error"].Type != JTokenType.Null)
                     {
+                        if (!(result["error"] is JObject error))
+                            throw new OpenRouterFailure("Commander AI returned an invalid response envelope.");
                         resultStage = "provider-error";
-                        throw new OpenRouterFailure(HttpFailure((int?)error["code"] ?? 500));
+                        JToken codeToken = error["code"];
+                        int code = 500;
+                        if (codeToken?.Type == JTokenType.Integer)
+                            int.TryParse(codeToken.ToString(Formatting.None), out code);
+                        if (code <= 0) code = 500;
+                        throw new OpenRouterFailure(HttpFailure(code));
                     }
                     if (!(result["choices"] is JArray choices) || choices.Count != 1 || !(choices[0] is JObject))
                     {
                         resultStage = "response-invalid-envelope";
                         throw new OpenRouterFailure("Commander AI returned an invalid response envelope.");
                     }
-                    string finish = (string)choices[0]["finish_reason"];
+                    JToken finishToken = choices[0]["finish_reason"];
+                    if (finishToken != null && finishToken.Type != JTokenType.Null
+                        && finishToken.Type != JTokenType.String)
+                        throw new OpenRouterFailure("Commander AI returned an invalid response envelope.");
+                    string finish = finishToken?.Type == JTokenType.String ? (string)finishToken : null;
                     // Bounded diagnostic category BEFORE rejection; never echo
                     // arbitrary provider finish strings or the response body.
                     Trace("finish="+(finish=="stop"?"stop":finish=="length"?"length":finish==null?"unspecified":"other"), run);
                     if (finish == "length")
                     {
                         resultStage = "response-truncated";
                         throw new OpenRouterFailure("Commander AI response was truncated; no order was submitted.");
                     }
                     if (finish != null && finish != "stop")
                     {
                         resultStage = "response-incomplete";
                         throw new OpenRouterFailure("Commander AI did not return a completed semantic response.");
@@ -492,34 +508,43 @@ namespace OpenEmpires
                     {
                         resultStage = "response-invalid-envelope";
                         throw new OpenRouterFailure("Commander AI returned an invalid response envelope.");
                     }
                     JToken content = message["content"];
                     string text = null;
                     if (content?.Type == JTokenType.String) text = (string)content;
                     else if (content is JArray parts)
                     {
                         var buffer = new System.Text.StringBuilder();
                         foreach (JToken part in parts)
                         {
-                            if (!(part is JObject textPart) || (string)textPart["type"] != "text"
-                                || textPart["text"]?.Type != JTokenType.String)
+                            if (!(part is JObject textPart))
+                            {
+                                resultStage = "response-unsupported-parts";
+                                throw new OpenRouterFailure("Commander AI returned unsupported content parts.");
+                            }
+                            JToken partType = textPart["type"];
+                            if (partType != null && partType.Type != JTokenType.String)
+                                throw new OpenRouterFailure("Commander AI returned an invalid response envelope.");
+                            if ((string)partType != "text" || textPart["text"]?.Type != JTokenType.String)
                             {
                                 resultStage = "response-unsupported-parts";
                                 throw new OpenRouterFailure("Commander AI returned unsupported content parts.");
                             }
                             buffer.Append((string)textPart["text"]);
                         }
                         text = buffer.ToString();
                     }
+                    else if (content != null && content.Type != JTokenType.Null)
+                        throw new OpenRouterFailure("Commander AI returned an invalid response envelope.");
                     if (string.IsNullOrWhiteSpace(text))
                     {
                         resultStage = "response-empty";
                         throw new OpenRouterFailure("Commander AI returned an empty semantic response.");
                     }
                     if (text.Length > maximumContentCharacters + 16)
                     {
                         resultStage = "response-oversize";
                         throw new OpenRouterFailure("Commander AI returned an oversized response.");
                     }
                     Trace("assistantContent=extracted;characters=" + text.Length + ";finish=" + (finish ?? "unspecified"), run);
                     resultStage = "response-extracted";
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-1-fix3-baseline\\CommanderKnowledgeRetrieval.cs.snapshot" b/Assets/Scripts/AI/Commander/Phase5B/CommanderKnowledgeRetrieval.cs
index 76b18d6..b34bfd0 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-1-fix3-baseline\\CommanderKnowledgeRetrieval.cs.snapshot"
+++ b/Assets/Scripts/AI/Commander/Phase5B/CommanderKnowledgeRetrieval.cs
@@ -4,62 +4,94 @@ using System.Linq;
 using System.Text.RegularExpressions;
 using Newtonsoft.Json;
 using Newtonsoft.Json.Linq;
 
 namespace OpenEmpires
 {
     // Retrieval only: lexical similarity never chooses an entity or authorizes an effect.
     internal static class CommanderKnowledgeRetrieval
     {
         // Required references are independent of optional lexical suggestions. The finite
         // ceiling is a refusal boundary, never permission to omit the seventeenth reference.
         internal const int MaximumRequiredEntries = 16;
+        private const int MaximumOptionalInterpretations = 2;
+        private const int MaximumOptionalEntries = 2;
         internal static string Retrieve(GameKnowledgeCatalog catalog, string input, Civilization civilization,
             string boundedPending = null)
         {
             if (catalog == null) throw new ArgumentNullException(nameof(catalog));
             var required = new HashSet<string>(StringComparer.Ordinal);
             var matchedPhrases = new HashSet<string>(StringComparer.Ordinal);
             var resolutions = new List<JObject>();
+            var optionalInterpretations = new List<JObject>();
+            var optional = new HashSet<string>(StringComparer.Ordinal);
             foreach (string source in new[] { input ?? string.Empty, boundedPending ?? string.Empty })
             {
                 // Both sources already have independent caller bounds (1024 player, 4096 pending).
                 // Scan the complete bounded strings; prefix/word/phrase cuts lose tail facts.
                 string searchable = " " + string.Join(" ", Regex.Matches(source.ToLowerInvariant(), @"[\p{L}\p{N}]+")
                     .Cast<Match>().Select(x => x.Value)) + " ";
                 foreach (var unit in catalog.Units)
                     MatchAliases(required, matchedPhrases, unit.StableId, unit.DisplayName, unit.Aliases, searchable);
                 foreach (var building in catalog.Buildings)
                     MatchAliases(required, matchedPhrases, building.StableId, building.DisplayName, building.Aliases, searchable);
                 // Role words such as "cavalry" are resolver-defined ambiguous names, not
                 // a new local name table. Deduplication bounds resolver work for long replies.
                 foreach (string word in Regex.Matches(source.ToLowerInvariant(), @"[\p{L}\p{N}]+")
                     .Cast<Match>().Select(x => x.Value).Distinct(StringComparer.Ordinal))
                     if (word.Length >= 3) matchedPhrases.Add(word);
             }
             foreach (string phrase in matchedPhrases.OrderBy(x => x, StringComparer.Ordinal))
             {
                 var unit = CommanderContentNameResolver.ResolveUnit(phrase, catalog, civilization);
                 var building = CommanderContentNameResolver.ResolveBuilding(phrase, catalog, civilization);
-                if (unit.Status != CommanderContentResolutionStatus.Ambiguous
-                    && building.Status != CommanderContentResolutionStatus.Ambiguous) continue;
-                foreach (var candidate in unit.Suggestions) required.Add(candidate.StableId);
-                foreach (var candidate in building.Suggestions) required.Add(candidate.StableId);
-                resolutions.Add(new JObject { ["phrase"] = phrase, ["interpretationOnly"] = true,
-                    ["unitCandidates"] = new JArray(unit.Suggestions.Select(x => x.StableId)),
-                    ["buildingCandidates"] = new JArray(building.Suggestions.Select(x => x.StableId)),
-                    ["ambiguous"] = true });
+                if (unit.Status == CommanderContentResolutionStatus.Ambiguous
+                    || building.Status == CommanderContentResolutionStatus.Ambiguous)
+                {
+                    foreach (var candidate in unit.Suggestions) required.Add(candidate.StableId);
+                    foreach (var candidate in building.Suggestions) required.Add(candidate.StableId);
+                    resolutions.Add(new JObject { ["phrase"] = phrase, ["interpretationOnly"] = true,
+                        ["unitCandidates"] = new JArray(unit.Suggestions.Select(x => x.StableId)),
+                        ["buildingCandidates"] = new JArray(building.Suggestions.Select(x => x.StableId)),
+                        ["ambiguous"] = true });
+                    continue;
+                }
+                // Only a narrow, resolver-defined near-name can become an optional
+                // interpretation. Common short words never fill the catalog with fuzz.
+                if (phrase.Length < 7 || unit.Status != CommanderContentResolutionStatus.Unknown
+                    || building.Status != CommanderContentResolutionStatus.Unknown) continue;
+                var unitCandidates = unit.Suggestions.Take(MaximumOptionalEntries).Select(x => x.StableId).ToArray();
+                var buildingCandidates = building.Suggestions.Take(MaximumOptionalEntries).Select(x => x.StableId).ToArray();
+                if (unitCandidates.Length + buildingCandidates.Length == 0
+                    || unitCandidates.Length + buildingCandidates.Length > MaximumOptionalEntries) continue;
+                optionalInterpretations.Add(new JObject { ["phrase"] = phrase, ["interpretationOnly"] = true,
+                    ["unitCandidates"] = new JArray(unitCandidates),
+                    ["buildingCandidates"] = new JArray(buildingCandidates), ["ambiguous"] = false });
             }
             bool overflow = required.Count > MaximumRequiredEntries || resolutions.Count > 6;
-            var chosen = overflow ? Array.Empty<string>() : required.OrderBy(x => x, StringComparer.Ordinal).ToArray();
+            int addedHints = 0;
+            if (!overflow)
+                foreach (JObject hint in optionalInterpretations)
+                {
+                    if (resolutions.Count >= 6 || addedHints >= MaximumOptionalInterpretations) break;
+                    var candidates = ((JArray)hint["unitCandidates"]).Concat((JArray)hint["buildingCandidates"])
+                        .Select(x => (string)x).Where(id => !required.Contains(id)).Distinct(StringComparer.Ordinal).ToArray();
+                    if (optional.Count + candidates.Count(id => !optional.Contains(id)) > MaximumOptionalEntries) continue;
+                    foreach (string id in candidates) optional.Add(id);
+                    resolutions.Add(hint);
+                    addedHints++;
+                }
+            var requiredIds = overflow ? Array.Empty<string>() : required.OrderBy(x => x, StringComparer.Ordinal).ToArray();
+            var optionalIds = overflow ? Array.Empty<string>() : optional.OrderBy(x => x, StringComparer.Ordinal).ToArray();
+            var chosen = requiredIds.Concat(optionalIds).ToArray();
             var units = new JArray(); var buildings = new JArray();
             foreach (string id in chosen)
             {
                 if (id.StartsWith("unit:", StringComparison.Ordinal))
                 {
                     var data = catalog.FindUnit(id);
                     var resolution = CommanderContentNameResolver.ResolveUnit(data.StableId, catalog, civilization);
                     units.Add(new JObject { ["id"] = data.StableId, ["name"] = data.DisplayName,
                         ["age"] = data.RequiredAge, ["producer"] = data.ProductionBuildingType < 0
                             ? null : ((BuildingType)data.ProductionBuildingType).ToString(),
                         ["cost"] = Cost(data.Cost), ["availableForCivilization"] = resolution.IsAvailableForCivilization,
                         ["commanderExecutable"] = resolution.IsExecutable });
@@ -69,25 +101,26 @@ namespace OpenEmpires
                     var data = catalog.FindBuilding(id);
                     var resolution = CommanderContentNameResolver.ResolveBuilding(data.StableId, catalog, civilization);
                     buildings.Add(new JObject { ["id"] = data.StableId, ["name"] = data.DisplayName,
                         ["age"] = data.RequiredAge, ["cost"] = Cost(data.Cost),
                         ["availableForCivilization"] = resolution.IsAvailableForCivilization,
                         ["commanderExecutable"] = resolution.IsExecutable,
                         ["dropoffResources"] = new JArray(Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>()
                             .Where(r => GameSimulation.AcceptsResourceType(data.BuildingType, r)).Select(r => r.ToString())) });
                 }
             }
             return new JObject { ["units"] = units, ["buildings"] = buildings,
                 ["technologies"] = new JArray(), ["nameInterpretations"] = new JArray(overflow ? Enumerable.Empty<JObject>() : resolutions),
-                ["requiredCanonicalIds"] = new JArray(chosen), ["requiredReferenceOverflow"] = overflow,
+                ["requiredCanonicalIds"] = new JArray(requiredIds), ["optionalCanonicalIds"] = new JArray(optionalIds),
+                ["requiredReferenceOverflow"] = overflow,
                 ["policy"] = "Knowledge is not execution authority. Show canonical interpretations; clarify ambiguous names. Availability and support are separate." }
                 .ToString(Formatting.None);
         }
 
         private static JObject Cost(KnowledgeCost value) => new JObject
             { ["food"] = value.Food, ["wood"] = value.Wood, ["gold"] = value.Gold, ["stone"] = value.Stone };
         private static void MatchAliases(HashSet<string> required, HashSet<string> matchedPhrases, string id, string name,
             IEnumerable<string> aliases, string searchable)
         {
             foreach (string label in new[] { name }.Concat(aliases))
             {
                 string normalized = Regex.Replace(label.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-1-fix3-baseline\\CommanderSemanticRequestPack.cs.snapshot" b/Assets/Scripts/AI/Commander/Phase5C/CommanderSemanticRequestPack.cs
index a482992..7193bbc 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-1-fix3-baseline\\CommanderSemanticRequestPack.cs.snapshot"
+++ b/Assets/Scripts/AI/Commander/Phase5C/CommanderSemanticRequestPack.cs
@@ -84,52 +84,85 @@ namespace OpenEmpires
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
+            bool optionalStripped = false;
+            while (true)
+            {
+            string packedInstruction = instruction;
+            if (knowledge?["nameInterpretations"] is JArray possibleHints
+                && possibleHints.OfType<JObject>().Any(hint => (bool?)hint["ambiguous"] == false))
+                packedInstruction += " Similar-name hints are interpretation only; Clarify before Request, never substitute a suggested name as authority.";
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
-                ["messages"] = new JArray(new JObject { ["role"] = "system", ["content"] = instruction },
+                ["messages"] = new JArray(new JObject { ["role"] = "system", ["content"] = packedInstruction },
                     new JObject { ["role"] = "user", ["content"] = user }),
                 ["max_tokens"] = maxOutputTokens, ["reasoning"] = new JObject { ["effort"] = "none" } };
             var sections = new Dictionary<string, int> {
-                ["contract"] = Encoding.UTF8.GetByteCount(instruction),
+                ["contract"] = Encoding.UTF8.GetByteCount(packedInstruction),
                 ["context"] = Encoding.UTF8.GetByteCount(compactContext),
                 ["memory"] = Encoding.UTF8.GetByteCount(request.SerializedSemanticMemory),
                 ["questionFacts"] = Encoding.UTF8.GetByteCount(request.QuestionFacts),
                 ["pending"] = Encoding.UTF8.GetByteCount(request.SerializedPendingClarification),
                 ["utterance"] = Encoding.UTF8.GetByteCount(request.PlayerMessage), ["messageMargin"] = MessageMargin };
             var candidate = new CommanderSemanticRequestPack(body, sections);
             pack = candidate; // Observed packing metrics survive a rejection; this is not dispatch authority.
-            if (candidate.EstimatedInputTokens >= InputLimit)
-            { reason = "Commander input limit: the complete request and required canonical context cannot fit safely; nothing was submitted. Edit the request or resolve its pending question."; return false; }
-            return true;
+            if (candidate.EstimatedInputTokens < InputLimit) return true;
+            if (!optionalStripped && StripOptionalHints(knowledge))
+            {
+                optionalStripped = true;
+                continue;
+            }
+            reason = "Commander input limit: the complete request and required canonical context cannot fit safely; nothing was submitted. Edit the request or resolve its pending question.";
+            return false;
+            }
+        }
+
+        private static bool StripOptionalHints(JObject knowledge)
+        {
+            if (knowledge == null) return false;
+            var required = new HashSet<string>((knowledge["requiredCanonicalIds"] as JArray ?? new JArray())
+                .Where(x => x.Type == JTokenType.String).Select(x => (string)x), StringComparer.Ordinal);
+            var optional = new HashSet<string>((knowledge["optionalCanonicalIds"] as JArray ?? new JArray())
+                .Where(x => x.Type == JTokenType.String).Select(x => (string)x), StringComparer.Ordinal);
+            bool stripped = false;
+            foreach (string table in new[] { "units", "buildings" })
+                if (knowledge[table] is JArray rows)
+                    foreach (JObject row in rows.OfType<JObject>().Where(row =>
+                        optional.Contains((string)row["id"]) && !required.Contains((string)row["id"])).ToArray())
+                    { row.Remove(); stripped = true; }
+            if (knowledge["nameInterpretations"] is JArray interpretations)
+                foreach (JObject hint in interpretations.OfType<JObject>()
+                    .Where(hint => (bool?)hint["ambiguous"] == false).ToArray())
+                { hint.Remove(); stripped = true; }
+            return stripped;
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
@@ -230,24 +263,25 @@ namespace OpenEmpires
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
+                        && property.Name != "optionalCanonicalIds"
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
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-1-fix3-baseline\\CommanderPhase5CProviderBudgetTests.cs.snapshot" b/Assets/Tests/EditMode/CommanderPhase5CProviderBudgetTests.cs
index 6c43424..b759289 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-1-fix3-baseline\\CommanderPhase5CProviderBudgetTests.cs.snapshot"
+++ b/Assets/Tests/EditMode/CommanderPhase5CProviderBudgetTests.cs
@@ -287,24 +287,62 @@ namespace OpenEmpires.Tests
         [Test] public void NineExplicitCanonicalNames_SurviveRealContextAndPackedColumns()
         {
             const string utterance = "Train Villager, Spearman, Archer, Horseman, Scout, Knight, Crossbowman, Monk, and Longbowman.";
             var request = ProductionRequest(utterance);
             Assert.That(CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
                 out var pack, out string reason), Is.True, reason);
             var ids = PackedKnowledgeIds(pack);
             foreach (string id in new[] { "unit:0", "unit:1", "unit:2", "unit:3", "unit:4",
                 "unit:7", "unit:8", "unit:9", "unit:10" })
                 Assert.That(ids, Does.Contain(id), id);
         }
 
+        [Test] public void SimilarNameHint_SurvivesRealRetrievalAndPackWithoutBecomingRequired()
+        {
+            var request = ProductionRequest("Train longbowmna.");
+            var sourceKnowledge = (JObject)JObject.Parse(request.SerializedContext)["knowledge"];
+            Assert.That(((JArray)sourceKnowledge["requiredCanonicalIds"]), Is.Empty,
+                "A typo suggestion must not acquire required/executable authority.");
+            Assert.That(CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
+                out var pack, out string reason), Is.True, reason);
+            string user = (string)JObject.Parse(pack.Json)["messages"]?[1]?["content"];
+            string compact = user.Substring("Context:".Length,
+                user.IndexOf("\nMemory:", StringComparison.Ordinal) - "Context:".Length);
+            var packedKnowledge = (JObject)JObject.Parse(compact)["knowledge"];
+            var columns = ((JArray)packedKnowledge["nameInterpretationsColumns"]).Select(x => (string)x).ToArray();
+            var interpretations = (JArray)packedKnowledge["nameInterpretations"];
+            Assert.That(interpretations, Is.Not.Null);
+            var typo = interpretations.OfType<JArray>().Single(x => (string)x[Array.IndexOf(columns, "phrase")] == "longbowmna");
+            Assert.That((bool)typo[Array.IndexOf(columns, "interpretationOnly")], Is.True);
+            Assert.That((bool)typo[Array.IndexOf(columns, "ambiguous")], Is.False);
+            Assert.That(((JArray)typo[Array.IndexOf(columns, "unitCandidates")]).Select(x => (string)x), Does.Contain("unit:10"));
+            Assert.That(PackedKnowledgeIds(pack), Does.Contain("unit:10"));
+            Assert.That(pack.EstimatedInputTokens, Is.LessThan(4000));
+            string contract = (string)JObject.Parse(pack.Json)["messages"]?[0]?["content"];
+            Assert.That(contract, Does.Contain("Similar-name hints are interpretation only; Clarify before Request"));
+        }
+
+        [Test] public void SimilarNameHint_DoesNotEvictExactCanonicalReferences()
+        {
+            const string utterance = "Train Villager, Spearman, Archer, Horseman, Scout, Knight, Crossbowman, Monk, and longbowmna.";
+            var request = ProductionRequest(utterance);
+            Assert.That(CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
+                out var pack, out string reason), Is.True, reason);
+            var ids = PackedKnowledgeIds(pack);
+            foreach (string id in new[] { "unit:0", "unit:1", "unit:2", "unit:3", "unit:4",
+                "unit:7", "unit:8", "unit:9" })
+                Assert.That(ids, Does.Contain(id), id);
+            Assert.That(ids, Does.Contain("unit:10"));
+        }
+
         [Test] public void LateCanonicalName_AfterOldWordAndPhraseWindowsSurvivesRealPack()
         {
             string utterance = "Build " + string.Join(" ", Enumerable.Repeat("please", 100)) + " Market.";
             Assert.That(utterance.Length, Is.LessThanOrEqualTo(1024));
             var request = ProductionRequest(utterance);
             Assert.That(CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
                 out var pack, out string reason), Is.True, reason);
             Assert.That(PackedKnowledgeIds(pack), Does.Contain("building:Market"));
         }
 
         [Test] public void PendingGraphAndAmbiguousAlternatives_AreRetainedOrWholeRequestRefused()
         {
@@ -355,39 +393,54 @@ namespace OpenEmpires.Tests
             Assert.That(transport.Calls, Is.Zero);
             Assert.That(result.Nodes, Is.Empty);
             Assert.That(result.SafeExplanation, Does.Contain("required").IgnoreCase);
         }
 
         [TestCase("{\"choices\":[]}", "response-invalid-envelope")]
         [TestCase("{\"choices\":[{},{}]}", "response-invalid-envelope")]
         [TestCase("{\"choices\":[{\"finish_reason\":\"length\",\"message\":{\"content\":\"x\"}}]}", "response-truncated")]
         [TestCase("{\"choices\":[{\"finish_reason\":\"content_filter\",\"message\":{\"content\":\"x\"}}]}", "response-incomplete")]
         [TestCase("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"\"}}]}", "response-empty")]
         [TestCase("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":[{\"type\":\"image\"}]}}]}", "response-unsupported-parts")]
         [TestCase("{\"error\":{\"code\":500}}", "provider-error")]
+        [TestCase("{\"choices\":[{\"finish_reason\":{},\"message\":{\"content\":\"x\"}}]}", "response-invalid-envelope")]
+        [TestCase("{\"error\":{\"code\":{}}}", "provider-error")]
+        [TestCase("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":[{\"type\":{},\"text\":\"x\"}]}}]}", "response-invalid-envelope")]
         public async Task RejectedSuccessfulHttp_RecordsActualResultStage(string envelope, string stage)
         {
             var transport = new EnvelopeTransport(new CommanderHttpResponse(200, envelope));
             var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
             var result = await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                 CommanderEconomyClarificationTests.Context()), CancellationToken.None);
             Assert.That(result.IsValid, Is.False);
             Assert.That(result.Nodes, Is.Empty);
             Assert.That(transport.Calls, Is.EqualTo(1));
             Assert.That(provider.LastSemanticAttempts, Has.Count.EqualTo(1));
             Assert.That(provider.LastSemanticAttempts[0].HttpStatusCode, Is.EqualTo(200));
             Assert.That(provider.LastSemanticAttempts[0].ResultStage, Is.EqualTo(stage));
             Assert.That(result.SafeExplanation, Does.Not.Contain(envelope));
         }
 
+        [Test] public async Task MalformedProviderErrorCode_UsesSafeDefaultProviderFailure()
+        {
+            var transport = new EnvelopeTransport(new CommanderHttpResponse(200, "{\"error\":{\"code\":{}}}"));
+            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
+            var result = await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
+                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
+            Assert.That(result.IsValid, Is.False);
+            Assert.That(result.SafeExplanation, Is.EqualTo("Commander AI provider unavailable. Please try again later."));
+            Assert.That(provider.LastSemanticAttempts, Has.Count.EqualTo(1));
+            Assert.That(provider.LastSemanticAttempts[0].ResultStage, Is.EqualTo("provider-error"));
+        }
+
         [Test] public async Task NullTransportResponse_RecordsTransportNullNotSuccess()
         {
             var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", new EnvelopeTransport(null));
             await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                 CommanderEconomyClarificationTests.Context()), CancellationToken.None);
             Assert.That(provider.LastSemanticAttempts[0].ResultStage, Is.EqualTo("transport-null"));
             Assert.That(provider.LastSemanticAttempts[0].HttpStatusCode, Is.Null);
         }
 
         [Test] public async Task OversizedSuccessfulHttpBody_RecordsResponseOversize()
         {
             var provider = new OpenRouterCommanderProvider("fixture-not-a-credential",
