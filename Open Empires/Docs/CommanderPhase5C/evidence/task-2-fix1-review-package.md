# Frozen Task2 fix1 incremental review package
Base: captured task-2-fix1-baseline.json before fix; uncommitted dirty checkout.
# Source delta: Assets/Scripts/AI/Commander/Economy/CommanderRequestGraphDraft.cs
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-2-fix1-review-baseline\\Assets__Scripts__AI__Commander__Economy__CommanderRequestGraphDraft.cs.snapshot" b/Assets/Scripts/AI/Commander/Economy/CommanderRequestGraphDraft.cs
index e521606..4600647 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-2-fix1-review-baseline\\Assets__Scripts__AI__Commander__Economy__CommanderRequestGraphDraft.cs.snapshot"
+++ b/Assets/Scripts/AI/Commander/Economy/CommanderRequestGraphDraft.cs
@@ -3,26 +3,38 @@ using System.Collections.Generic;
 using Newtonsoft.Json;
 using Newtonsoft.Json.Linq;
 
 namespace OpenEmpires
 {
     // Detached provider proposal. It carries typed, bounded data, never authority or a runtime selector.
     public sealed class CommanderRequestGraphDraft
     {
         public sealed class Slot
         {
+            private readonly List<JToken> alternatives;
             public int Node { get; }
             public string Path { get; }
             public string Type { get; }
-            public IReadOnlyList<JToken> Alternatives { get; }
+            public IReadOnlyList<JToken> Alternatives
+            {
+                get
+                {
+                    var detached = new List<JToken>();
+                    foreach (var value in alternatives) detached.Add(value.DeepClone());
+                    return detached.AsReadOnly();
+                }
+            }
             internal Slot(int node, string path, string type, IList<JToken> alternatives)
-            { Node = node; Path = path; Type = type; Alternatives = new List<JToken>(alternatives).AsReadOnly(); }
+            {
+                Node = node; Path = path; Type = type; this.alternatives = new List<JToken>();
+                foreach (var value in alternatives) this.alternatives.Add(value.DeepClone());
+            }
         }
 
         private readonly JObject graph;
         private readonly List<Slot> slots;
         public IReadOnlyList<Slot> MissingSlots => slots.AsReadOnly();
         public int NodeCount => ((JArray)graph["nodes"]).Count;
         private CommanderRequestGraphDraft(JObject graph, List<Slot> slots)
         { this.graph = (JObject)graph.DeepClone(); this.slots = slots; }
 
         internal static CommanderRequestGraphDraft Parse(JObject root)
@@ -79,20 +91,21 @@ namespace OpenEmpires
                 var parent = Parent((JObject)((JArray)completed["nodes"])[slot.Node], slot.Path);
                 if (slot.Type != "Confirmation") parent[Leaf(slot.Path)] = (slot == changed ? value : slot.Alternatives[0]).DeepClone();
             }
             return CommanderSemanticJson.ParseProviderResponse(new JObject { ["outcome"] = "Request",
                 ["executionOrder"] = completed["executionOrder"].DeepClone(),
                 ["nodes"] = completed["nodes"].DeepClone() }.ToString(Formatting.None));
         }
 
         internal CommanderSemanticResult Resolve(string reply)
         {
+            if (IsCorrectionUtterance(reply)) return Correct(reply);
             Slot slot = null;
             JToken chosen = null;
             foreach (var candidate in slots)
             {
                 JToken match = Match(candidate, reply);
                 if (match == null) continue;
                 if (slot != null) return null; // A reply matching two distinct slots is ambiguous.
                 slot = candidate; chosen = match;
             }
             if (slot == null) return null;
@@ -107,20 +120,146 @@ namespace OpenEmpires
             var remaining = new JArray();
             foreach (var other in slots)
                 if (!ReferenceEquals(other, slot))
                     remaining.Add(new JObject { ["node"] = other.Node, ["path"] = other.Path,
                         ["type"] = other.Type, ["alternatives"] = new JArray(other.Alternatives) });
             return CommanderSemanticJson.Parse(new JObject { ["outcome"] = "Clarify",
                 ["message"] = "Please specify the remaining detail.", ["pending"] = next,
                 ["missingSlots"] = remaining }.ToString(Formatting.None));
         }
 
+        internal static bool IsCorrectionUtterance(string reply)
+        {
+            foreach (var word in Words(reply))
+                if (word == "instead" || word == "actually" || word == "change" || word == "rather") return true;
+            return false;
+        }
+
+        private sealed class Change
+        {
+            internal int Node;
+            internal string Path;
+            internal JToken Value;
+        }
+
+        private CommanderSemanticResult Correct(string reply)
+        {
+            string[] words = Words(reply);
+            if (words.Length == 0 || words.Length > 12) return null;
+            var changes = new List<Change>();
+            var nodes = (JArray)graph["nodes"];
+            for (int start = 0; start < words.Length; start++)
+                for (int length = 1; length <= 3 && start + length <= words.Length; length++)
+                {
+                    string phrase = string.Join(" ", words, start, length);
+                    if (CommanderIntentCatalog.TryResolveStructure(phrase, out BuildingType building))
+                    {
+                        for (int i = 0; i < nodes.Count; i++)
+                        {
+                            AddChange(changes, i, (JObject)nodes[i], "structure", new JValue(building.ToString()));
+                            AddChange(changes, i, (JObject)nodes[i], "producer", new JValue(building.ToString()));
+                        }
+                    }
+                    if (CommanderIntentCatalog.TryResolveUnit(phrase, out int unitType))
+                    {
+                        string canonical = CommanderIntentCatalog.GetUnitDisplayName(unitType);
+                        for (int i = 0; i < nodes.Count; i++)
+                            AddChange(changes, i, (JObject)nodes[i], "unit", new JValue(canonical));
+                    }
+                    if (CommanderIntentCatalog.TryResolveResource(phrase, out ResourceType resource))
+                    {
+                        for (int i = 0; i < nodes.Count; i++)
+                        {
+                            AddChange(changes, i, (JObject)nodes[i], "resource", new JValue(resource.ToString()));
+                            AddChange(changes, i, (JObject)nodes[i], "destination.resource", new JValue(resource.ToString()));
+                            AddChange(changes, i, (JObject)nodes[i], "placement.resource", new JValue(resource.ToString()));
+                        }
+                    }
+                    string sourceName = SourceAlias(phrase);
+                    if (Enum.TryParse(sourceName, true, out ResourceSourceKind sourceKind)
+                        && Enum.IsDefined(typeof(ResourceSourceKind), sourceKind))
+                    {
+                        for (int i = 0; i < nodes.Count; i++)
+                        {
+                            AddChange(changes, i, (JObject)nodes[i], "sourceKind", new JValue(sourceKind.ToString()));
+                            AddChange(changes, i, (JObject)nodes[i], "destination.sourceKind", new JValue(sourceKind.ToString()));
+                            AddChange(changes, i, (JObject)nodes[i], "placement.sourceKind", new JValue(sourceKind.ToString()));
+                        }
+                    }
+                }
+            int? number = null;
+            foreach (var word in words)
+                if (CommanderClarificationReplies.TryParseCount(word, out int parsed))
+                {
+                    if (number.HasValue && number.Value != parsed) return null;
+                    number = parsed;
+                }
+            if (number.HasValue)
+            {
+                bool workers = Contains(words, "villager") || Contains(words, "villagers") || Contains(words, "worker") || Contains(words, "workers");
+                bool amount = Contains(words, "additional") || Contains(words, "stockpile") || Contains(words, "food")
+                    || Contains(words, "wood") || Contains(words, "gold") || Contains(words, "stone");
+                if (workers && amount) return null;
+                int fields = 0;
+                for (int i = 0; i < nodes.Count; i++)
+                {
+                    var node = (JObject)nodes[i];
+                    if (node["count"]?.Type == JTokenType.Integer && !amount) fields++;
+                    if (node["resourceAmount"]?.Type == JTokenType.Integer && !workers) fields++;
+                }
+                if (fields != 1) return null;
+                for (int i = 0; i < nodes.Count; i++)
+                {
+                    var node = (JObject)nodes[i];
+                    if (node["count"]?.Type == JTokenType.Integer && !amount)
+                        AddChange(changes, i, node, "count", new JValue(number.Value));
+                    if (node["resourceAmount"]?.Type == JTokenType.Integer && !workers)
+                        AddChange(changes, i, node, "resourceAmount", new JValue(number.Value));
+                }
+            }
+            if (changes.Count != 1) return null;
+            Change change = changes[0];
+            var next = (JObject)graph.DeepClone();
+            Parent((JObject)((JArray)next["nodes"])[change.Node], change.Path)[Leaf(change.Path)] = change.Value.DeepClone();
+            var remaining = new JArray();
+            foreach (var slot in slots)
+            {
+                if (slot.Node == change.Node && slot.Path == change.Path) continue;
+                remaining.Add(new JObject { ["node"] = slot.Node, ["path"] = slot.Path,
+                    ["type"] = slot.Type, ["alternatives"] = new JArray(slot.Alternatives) });
+            }
+            if (remaining.Count == 0)
+                return CommanderSemanticJson.ParseProviderResponse(new JObject { ["outcome"] = "Request",
+                    ["executionOrder"] = next["executionOrder"].DeepClone(),
+                    ["nodes"] = next["nodes"].DeepClone() }.ToString(Formatting.None));
+            return CommanderSemanticJson.Parse(new JObject { ["outcome"] = "Clarify",
+                ["message"] = "Please specify the remaining detail.", ["pending"] = next,
+                ["missingSlots"] = remaining }.ToString(Formatting.None));
+        }
+
+        private static void AddChange(List<Change> changes, int index, JObject node, string path, JToken replacement)
+        {
+            JObject parent = Parent(node, path);
+            JToken current = parent?[Leaf(path)];
+            if (current == null || current.Type != replacement.Type || JToken.DeepEquals(current, replacement)) return;
+            foreach (var existing in changes)
+                if (existing.Node == index && existing.Path == path && JToken.DeepEquals(existing.Value, replacement)) return;
+            changes.Add(new Change { Node = index, Path = path, Value = replacement });
+        }
+
+        private static bool Contains(string[] words, string word)
+        { foreach (string entry in words) if (entry == word) return true; return false; }
+
+        private static string[] Words(string text)
+        { return (text ?? string.Empty).Trim().ToLowerInvariant().Split(
+            new[] { ' ', '\t', '\r', '\n', ',', '.', '!', '?', ';', ':' }, StringSplitOptions.RemoveEmptyEntries); }
+
         private static JToken Match(Slot slot, string reply)
         {
             string whole = (reply ?? string.Empty).Trim().ToLowerInvariant();
             JToken chosen = null;
             if (slot.Type == "Confirmation" && (whole == "yes" || whole == "y")) chosen = slot.Alternatives[0];
             else if (slot.Type == "Count" && CommanderClarificationReplies.TryParseCount(reply, out int count)) chosen = new JValue(count);
             else
             {
                 string canonical = slot.Type == "SourceKind" ? SourceAlias(whole) : whole;
                 foreach (var offered in slot.Alternatives)
@@ -166,11 +305,10 @@ namespace OpenEmpires
         {
             int dot = path.IndexOf('.');
             return dot < 0 ? node : node[path.Substring(0, dot)] as JObject;
         }
         private static string Leaf(string path)
         { int dot = path.IndexOf('.'); return dot < 0 ? path : path.Substring(dot + 1); }
         private static void ExactFields(JObject obj, params string[] fields)
         { foreach (var property in obj.Properties()) if (Array.IndexOf(fields, property.Name) < 0) throw new JsonException(); }
     }
 }
-
# Source delta: Assets/Scripts/AI/Commander/Economy/CommanderPendingClarification.cs
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-2-fix1-review-baseline\\Assets__Scripts__AI__Commander__Economy__CommanderPendingClarification.cs.snapshot" b/Assets/Scripts/AI/Commander/Economy/CommanderPendingClarification.cs
index 75066bc..7d2d1ef 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-2-fix1-review-baseline\\Assets__Scripts__AI__Commander__Economy__CommanderPendingClarification.cs.snapshot"
+++ b/Assets/Scripts/AI/Commander/Economy/CommanderPendingClarification.cs
@@ -59,21 +59,23 @@ namespace OpenEmpires
         internal CommanderPendingClarification(CommanderWorkerAllocationDraft draft, string original, string question,
             int generation, long sequence, int turns = 0, CommanderRequestTicket requestTicket = null)
         {
             Draft = draft; OriginalText = Bound(original, 1024); Question = Bound(question, 180);
             RuntimeGeneration = generation; Sequence = sequence; Turns = turns;
             RequestTicket = requestTicket;
         }
         internal CommanderPendingClarification(CommanderRequestGraphDraft graphDraft, string original, string question,
             int generation, long sequence, int turns = 0, CommanderRequestTicket requestTicket = null)
         {
-            GraphDraft = graphDraft; OriginalText = Bound(original, 1024); Question = Bound(question, 180);
+            if (original != null && original.Length > CommanderSemanticProviderRequest.MaximumPlayerMessageCharacters)
+                throw new ArgumentOutOfRangeException(nameof(original));
+            GraphDraft = graphDraft; OriginalText = original ?? string.Empty; Question = Bound(question, 180);
             RuntimeGeneration = generation; Sequence = sequence; Turns = turns;
             RequestTicket = requestTicket;
         }
         internal string Serialize()
         {
             if (GraphDraft != null) return new JObject { ["draftGraph"] = GraphDraft.ToPendingJson(),
                 ["missingSlots"] = GraphDraft.ToSlotsJson(), ["originalText"] = OriginalText,
                 ["question"] = Question }.ToString(Formatting.None);
             var missing = new JArray(); foreach (var field in Draft.MissingFields) missing.Add(field.ToString());
             return new JObject { ["draft"] = Draft.ToJson(), ["missingFields"] = missing, ["originalText"] = OriginalText, ["question"] = Question }.ToString(Formatting.None);
@@ -90,11 +92,10 @@ namespace OpenEmpires
             if (value.EndsWith(" villagers", StringComparison.Ordinal)) value = value.Substring(0, value.Length - 10).TrimEnd();
             else if (value.EndsWith(" villager", StringComparison.Ordinal)) value = value.Substring(0, value.Length - 9).TrimEnd();
             string[] words = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen", "twenty" };
             int word = Array.IndexOf(words, value);
             if (word >= 0) { count = word; return true; }
             for (int i = 0; i < value.Length; i++) if (value[i] < '0' || value[i] > '9') return false;
             return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out count);
         }
     }
 }
-
# Source delta: Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Clarification.cs
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-2-fix1-review-baseline\\Assets__Scripts__AI__Commander__Phase4A__CommanderChatUI.Clarification.cs.snapshot" b/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Clarification.cs
index 4e94417..b5fd017 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-2-fix1-review-baseline\\Assets__Scripts__AI__Commander__Phase4A__CommanderChatUI.Clarification.cs.snapshot"
+++ b/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Clarification.cs
@@ -22,38 +22,52 @@ namespace OpenEmpires
                 pendingClarification = null; AppendLine("Player", message); AppendLine("Commander", "Pending request cancelled; no goal was created."); return true;
             }
             if (pendingClarification.GraphDraft != null && whole == "no")
             {
                 pendingClarification = null; AppendLine("Player", message);
                 AppendLine("Commander", "Proposal dismissed; existing tasks are unchanged."); return true;
             }
             // This only chooses whether a new turn supersedes a pending question.
             // It neither parses nor executes any gameplay action.
             string[] standalone = { "build ", "train ", "make ", "produce ", "research ", "attack ", "scout ", "patrol ", "retreat ", "repair ", "defend ", "advance ", "put ", "gather ", "send ", "reach ", "assign ", "have ", "keep ", "move ", "garrison ", "ungarrison ", "explore ", "find " };
-            if (standalone.Any(prefix => whole.StartsWith(prefix, StringComparison.Ordinal)))
+            if (!(pendingClarification.GraphDraft != null && CommanderRequestGraphDraft.IsCorrectionUtterance(message))
+                && standalone.Any(prefix => whole.StartsWith(prefix, StringComparison.Ordinal)))
             { pendingClarification = null; return false; }
             var previous = pendingClarification;
+            if (previous.GraphDraft != null && previous.Turns >= 6)
+            {
+                pendingClarification = null;
+                AppendLine("Player", message);
+                AppendLine("Commander", "Clarification expired after six replies; please give a new complete request.");
+                return true;
+            }
             pendingClarification = previous.GraphDraft != null
                 ? new CommanderPendingClarification(previous.GraphDraft, previous.OriginalText, previous.Question,
                     previous.RuntimeGeneration, previous.Sequence, previous.Turns + 1, previous.RequestTicket)
                 : new CommanderPendingClarification(previous.Draft, previous.OriginalText, previous.Question,
                     previous.RuntimeGeneration, previous.Sequence, previous.Turns + 1, previous.RequestTicket);
             if (previous.GraphDraft == null && (whole == "yes" || whole == "no" || whole == "those" || whole == "there"))
             { AppendLine("Player", message); ReaskPending("Please specify the missing worker criteria."); return true; }
             return false;
         }
 
         private void ReaskPending(string reason)
         {
             if (pendingClarification == null) return;
-            if (pendingClarification.Turns >= 3)
-            { pendingClarification = null; AppendLine("Commander", "Clarification expired after three replies; please give a new complete request."); }
+            if (pendingClarification.Turns >= (pendingClarification.GraphDraft != null ? 6 : 3))
+            {
+                bool graph = pendingClarification.GraphDraft != null;
+                pendingClarification = null;
+                AppendLine("Commander", graph
+                    ? "Clarification expired after six replies; please give a new complete request."
+                    : "Clarification expired after three replies; please give a new complete request.");
+            }
             else AppendLine("Commander", reason + " " + pendingClarification.Question);
         }
 
         private CommanderSemanticResult LocalCountContinuation(CommanderPendingClarification pending, string reply)
         {
             if (pending?.Draft == null || pending.Draft.CountMode != CommanderWorkerCountMode.Exact || pending.Draft.Count.HasValue
                 || !CommanderClarificationReplies.TryParseCount(reply, out int count)) return null;
             var updated = pending.Draft.WithCount(count);
             if (updated.MissingFields.Count == 0) return updated.CompleteResult();
             var missing = new JArray(); foreach (var field in updated.MissingFields) missing.Add(field.ToString());
@@ -161,11 +175,10 @@ namespace OpenEmpires
             // An explicitly named replacement destination may change that slot; the
             // provider is not allowed to silently drop an already-resolved source.
             string resource = next.Destination.Resource.ToString().ToLowerInvariant();
             string source = next.Destination.SourceKind.ToString().ToLowerInvariant();
             if (next.Destination.Resource != previous.Destination.Resource)
                 return tokens.Contains(resource) && (next.Destination.SourceKind == ResourceSourceKind.Any || tokens.Contains(source));
             return next.Destination.SourceKind != ResourceSourceKind.Any && tokens.Contains(source);
         }
     }
 }
-
# Source delta: Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-2-fix1-review-baseline\\Assets__Scripts__AI__Commander__Phase4A__CommanderChatUI.cs.snapshot" b/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs
index 27a06d8..21eaf17 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-2-fix1-review-baseline\\Assets__Scripts__AI__Commander__Phase4A__CommanderChatUI.cs.snapshot"
+++ b/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs
@@ -455,24 +455,33 @@ namespace OpenEmpires
                     if (context.PlayerId != owner)
                     {
                         AppendLine("Commander", "Commander ownership changed; ask again.");
                         return null;
                     }
                     IReadOnlyList<CommanderSemanticMemoryEntry> semanticMemoryAtTurnStart =
                         Conversation.SemanticMemory.Snapshot();
                     TryObserveQuestionAnswer(NormalizeExplanationWholeForm(message), out string questionFacts);
                     var providerRequest = new CommanderSemanticProviderRequest(message, context,
                         semanticMemoryAtTurnStart, questionFacts, capturedPending);
-                    semanticStage = "provider-request";
-                    Debug.Log("[Commander] context=projected;provider-request=started");
                     CommanderSemanticResult localCount = LocalCountContinuation(capturedPending, message);
                     CommanderSemanticResult localGraph = LocalGraphContinuation(capturedPending, message);
+                    if (capturedPending?.GraphDraft != null && localGraph == null)
+                    {
+                        semanticStage = "graph-local-reask";
+                        ReaskPending(CommanderRequestGraphDraft.IsCorrectionUtterance(message)
+                            ? "Please identify one graph node and field to correct with a supported value."
+                            : "That reply did not identify one safe graph detail.");
+                        return null;
+                    }
+                    semanticStage = localCount != null || localGraph != null ? "local-continuation" : "provider-request";
+                    if (localCount == null && localGraph == null)
+                        Debug.Log("[Commander] context=projected;provider-request=started");
                     Task<CommanderSemanticResult> translation = localCount == null && localGraph == null
                         ? RequestSemanticTranslation(provider, providerRequest, request.Token)
                         : Task.FromResult(localCount ?? localGraph);
                     // A provider may ignore cancellation. Keep the host usable and observe
                     // any late fault without ever admitting its late response.
                     _ = translation.ContinueWith(task => { var ignored = task.Exception; },
                         CancellationToken.None,
                         TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                         TaskScheduler.Default);
                     Task finished = await Task.WhenAny(translation,
@@ -1240,11 +1249,10 @@ namespace OpenEmpires
         private static void SetRect(RectTransform rect, Vector2 anchorMin,
             Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
         {
             rect.anchorMin = anchorMin;
             rect.anchorMax = anchorMax;
             rect.offsetMin = offsetMin;
             rect.offsetMax = offsetMax;
         }
     }
 }
-
# Source delta: Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticJson.cs
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-2-fix1-review-baseline\\Assets__Scripts__AI__Commander__Phase4E__CommanderSemanticJson.cs.snapshot" b/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticJson.cs
index 2510408..c05ca4e 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-2-fix1-review-baseline\\Assets__Scripts__AI__Commander__Phase4E__CommanderSemanticJson.cs.snapshot"
+++ b/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticJson.cs
@@ -688,11 +688,10 @@ namespace OpenEmpires
                     if (json[i] == '0' && i + 1 < json.Length && json[i + 1] >= '0' && json[i + 1] <= '9')
                         throw new JsonException();
                     while (i + 1 < json.Length && json[i + 1] >= '0' && json[i + 1] <= '9') i++;
                     previous = '0';
                 }
                 else throw new JsonException();
             }
         }
     }
 }
-
# Source delta: Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticRequest.cs
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-2-fix1-review-baseline\\Assets__Scripts__AI__Commander__Phase4E__CommanderSemanticRequest.cs.snapshot" b/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticRequest.cs
index 882dadd..4de1650 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-2-fix1-review-baseline\\Assets__Scripts__AI__Commander__Phase4E__CommanderSemanticRequest.cs.snapshot"
+++ b/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticRequest.cs
@@ -178,11 +178,10 @@ namespace OpenEmpires
             IsValid = isValid;
             Outcome = outcome;
             Nodes = nodes ?? Array.Empty<CommanderSemanticNode>();
             SafeExplanation = safeExplanation ?? string.Empty;
             PendingDraft = pendingDraft;
             PendingGraphDraft = pendingGraphDraft;
             DynamicPlan = dynamicPlan;
         }
     }
 }
-
# Source delta: Assets/Tests/EditMode/CommanderEconomyClarificationTests.Graph.cs
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-2-fix1-review-baseline\\Assets__Tests__EditMode__CommanderEconomyClarificationTests.Graph.cs.snapshot" b/Assets/Tests/EditMode/CommanderEconomyClarificationTests.Graph.cs
index f0d6a79..9c10bad 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-2-fix1-review-baseline\\Assets__Tests__EditMode__CommanderEconomyClarificationTests.Graph.cs.snapshot"
+++ b/Assets/Tests/EditMode/CommanderEconomyClarificationTests.Graph.cs
@@ -211,13 +211,298 @@ namespace OpenEmpires.Tests
                 Assert.That(f.Economy.Manager.Goals, Is.Empty);
                 Assert.That(f.Chat.PendingClarification?.GraphDraft?.MissingSlots.Count, Is.EqualTo(1));
                 await f.Chat.SubmitMessageAsync("from berries");
                 Assert.That(f.Provider.Calls, Is.EqualTo(1));
                 var retained = f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation;
                 Assert.That(retained.Count, Is.EqualTo(3));
                 Assert.That(retained.Workers.State, Is.EqualTo(CommanderWorkerState.Idle));
                 Assert.That(retained.Destination.SourceKind, Is.EqualTo(ResourceSourceKind.Berries));
             }
         }
+
+        [Test]
+        public async Task KnownStructureCorrection_BuildOrMakeMillInsteadPreservesMineProposalFields()
+        {
+            foreach (string correction in new[] { "build a Mill instead", "make a Mill instead" })
+            {
+                var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
+                    ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 },
+                    ["placement"] = new JObject { ["anchor"] = "MyTownCenter", ["relation"] = "Near" } };
+                using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(mine),
+                    "Independent", 0, "structure", "Confirmation", new JValue("Mine")))))
+                {
+                    f.Economy.Worker(0);
+                    f.Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter, f.Economy.Base.x, f.Economy.Base.y, false, true);
+                    await f.Chat.SubmitMessageAsync("build a Mine near my Town Center");
+                    var result = await f.Chat.SubmitMessageAsync(correction);
+                    Assert.That(result?.Success, Is.True, correction);
+                    Assert.That(f.Provider.Calls, Is.EqualTo(1), correction);
+                    var goal = f.Economy.Manager.Goals.OfType<BuildStructureGoal>().Single();
+                    Assert.That(goal.StructureType, Is.EqualTo(BuildingType.Mill));
+                    Assert.That(goal.PlacementAnchorSelector, Is.EqualTo(CommanderSemanticAnchorSelector.MyTownCenter));
+                    Assert.That(goal.RequestAuthority.OriginalInput, Is.EqualTo("build a Mine near my Town Center"));
+                }
+            }
+        }
+
+        [Test]
+        public async Task KnownCountCorrection_DoesNotChangeResourceAmount()
+        {
+            var allocation = new JObject { ["type"] = "AllocateWorkers", ["mode"] = "SelectedCount",
+                ["countMode"] = "Exact", ["count"] = 6,
+                ["workers"] = new JObject { ["state"] = "Gathering", ["currentResource"] = "Wood" },
+                ["destination"] = new JObject { ["resource"] = "Food" },
+                ["resourceAmount"] = 400, ["resourceAmountMode"] = "AdditionalGathered" };
+            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(allocation),
+                "Independent", 0, "destination.sourceKind", "SourceKind", new JValue("Berries")))))
+            {
+                for (int i = 0; i < 3; i++) f.Economy.Assigned(f.Economy.Worker(i), f.Economy.Node(ResourceType.Wood, 15 + i));
+                f.Economy.Node(ResourceType.Food, 30);
+                await f.Chat.SubmitMessageAsync("send six wood villagers to gather 400 additional food");
+                await f.Chat.SubmitMessageAsync("three villagers instead");
+                Assert.That(f.Provider.Calls, Is.EqualTo(1));
+                Assert.That(f.Economy.Manager.Goals, Is.Empty);
+                var pending = (JObject)((JArray)f.Chat.PendingClarification.GraphDraft.ToPendingJson()["nodes"])[0];
+                Assert.That((int)pending["count"], Is.EqualTo(3));
+                Assert.That((int)pending["resourceAmount"], Is.EqualTo(400));
+                await f.Chat.SubmitMessageAsync("from berries");
+                var retained = f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation;
+                Assert.That(retained.Count, Is.EqualTo(3));
+                Assert.That(retained.ResourceAmount, Is.EqualTo(400));
+                Assert.That(retained.ResourceAmountMode, Is.EqualTo(CommanderResourceAmountMode.AdditionalGathered));
+                Assert.That(retained.Workers.CurrentResource, Is.EqualTo(ResourceType.Wood));
+            }
+        }
+
+        [Test]
+        public async Task UnrecognizedGraphReply_AndSourceYes_ReaskWithoutHttp()
+        {
+            var watch = new JObject { ["type"] = "WatchFutureUnits", ["unit"] = "Villager", ["count"] = 5,
+                ["producer"] = "TownCenter", ["action"] = "Gather", ["resource"] = "Wood" };
+            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(watch),
+                "Independent", 0, "sourceKind", "SourceKind", new JValue("Tree")))))
+            {
+                f.Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter, f.Economy.Base.x, f.Economy.Base.y, false, true);
+                await f.Chat.SubmitMessageAsync("send the next five villagers to wood from trees");
+                await f.Chat.SubmitMessageAsync("yes");
+                await f.Chat.SubmitMessageAsync("maybe elsewhere");
+                Assert.That(f.Provider.Calls, Is.EqualTo(1));
+                Assert.That(f.Chat.PendingClarification, Is.Not.Null);
+                Assert.That(f.Economy.Manager.Goals, Is.Empty);
+                await f.Chat.SubmitMessageAsync("from trees");
+                Assert.That(f.Provider.Calls, Is.EqualTo(1));
+                Assert.That(f.Chat.PendingActionPlan, Is.Not.Null);
+                Assert.That(f.Economy.Manager.Goals, Is.Empty);
+            }
+        }
+
+        [Test]
+        public void GraphSlotAlternatives_ExternalMutationCannotAlterDraft()
+        {
+            var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
+                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
+            var parsed = CommanderSemanticJson.Parse(GraphDraft(new JArray(mine), "Independent", 0,
+                "structure", "Confirmation", new JValue("Mine")));
+            var draft = parsed.PendingGraphDraft;
+            ((JValue)draft.MissingSlots[0].Alternatives[0]).Value = "Mill";
+            Assert.That((string)draft.MissingSlots[0].Alternatives[0], Is.EqualTo("Mine"));
+            Assert.That((string)((JArray)draft.ToSlotsJson()[0]["alternatives"])[0], Is.EqualTo("Mine"));
+            Assert.That(draft.Resolve("yes").Nodes[0].BuildingType, Is.EqualTo(BuildingType.Mine));
+        }
+
+        [Test]
+        public async Task KnownResourceAmountCorrection_DoesNotChangeWorkerCount()
+        {
+            var allocation = new JObject { ["type"] = "AllocateWorkers", ["mode"] = "SelectedCount",
+                ["countMode"] = "Exact", ["count"] = 3,
+                ["workers"] = new JObject { ["state"] = "Idle" },
+                ["destination"] = new JObject { ["resource"] = "Food" },
+                ["resourceAmount"] = 400, ["resourceAmountMode"] = "AdditionalGathered" };
+            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(allocation),
+                "Independent", 0, "destination.sourceKind", "SourceKind", new JValue("Berries")))))
+            {
+                for (int i = 0; i < 3; i++) f.Economy.Worker(i);
+                f.Economy.Node(ResourceType.Food, 12);
+                await f.Chat.SubmitMessageAsync("send three idle villagers to gather 400 additional food");
+                await f.Chat.SubmitMessageAsync("500 additional food instead");
+                Assert.That(f.Provider.Calls, Is.EqualTo(1));
+                var node = (JObject)((JArray)f.Chat.PendingClarification.GraphDraft.ToPendingJson()["nodes"])[0];
+                Assert.That((int)node["count"], Is.EqualTo(3));
+                Assert.That((int)node["resourceAmount"], Is.EqualTo(500));
+                Assert.That((string)node["resourceAmountMode"], Is.EqualTo("AdditionalGathered"));
+                await f.Chat.SubmitMessageAsync("from berries");
+                var retained = f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation;
+                Assert.That(retained.Count, Is.EqualTo(3));
+                Assert.That(retained.ResourceAmount, Is.EqualTo(500));
+            }
+        }
+
+        [Test]
+        public async Task RepeatedSuccessfulGraphCorrections_ExpireBeforeSeventhReply()
+        {
+            var allocation = new JObject { ["type"] = "AllocateWorkers", ["mode"] = "SelectedCount",
+                ["countMode"] = "Exact", ["count"] = 2,
+                ["workers"] = new JObject { ["state"] = "Idle" },
+                ["destination"] = new JObject { ["resource"] = "Food" } };
+            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(allocation),
+                "Independent", 0, "destination.sourceKind", "SourceKind", new JValue("Berries")))))
+            {
+                for (int i = 0; i < 8; i++) f.Economy.Worker(i);
+                f.Economy.Node(ResourceType.Food, 12);
+                await f.Chat.SubmitMessageAsync("send two idle villagers to berries");
+                foreach (string word in new[] { "three", "four", "five", "six", "seven", "eight" })
+                    await f.Chat.SubmitMessageAsync(word + " villagers instead");
+                Assert.That(f.Chat.PendingClarification, Is.Not.Null, "Six successful replies may remain pending.");
+                Assert.That(f.Chat.PendingClarification.Turns, Is.EqualTo(6));
+                await f.Chat.SubmitMessageAsync("from berries");
+                Assert.That(f.Chat.PendingClarification, Is.Null);
+                Assert.That(f.Provider.Calls, Is.EqualTo(1));
+                Assert.That(f.Economy.Manager.Goals, Is.Empty);
+            }
+        }
+
+        [Test]
+        public async Task PendingDraft_ForeignRuntimeAndReplayCannotCreateGoals()
+        {
+            var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
+                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
+            string proposal = GraphDraft(new JArray(mine), "Independent", 0, "structure", "Confirmation", new JValue("Mine"));
+            var pendingField = typeof(CommanderChatUI).GetField("pendingClarification",
+                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
+            using (var first = new ChatFixture(new SequenceSemanticProvider(proposal)))
+            using (var foreign = new ChatFixture(new SequenceSemanticProvider(proposal)))
+            using (var replacement = new EconomyFixture())
+            using (var replacementDispatcher = new CommanderIntentDispatcher(replacement.Simulation, replacement.Manager))
+            {
+                first.Economy.Worker(0); foreign.Economy.Worker(0);
+                await first.Chat.SubmitMessageAsync("build a Mine");
+                var old = first.Chat.PendingClarification;
+                pendingField.SetValue(foreign.Chat, old);
+                await foreign.Chat.SubmitMessageAsync("yes");
+                Assert.That(foreign.Economy.Manager.Goals, Is.Empty);
+                Assert.That(foreign.Provider.Calls, Is.EqualTo(0));
+
+                first.Chat.Initialize(first.Provider, replacement.Simulation, replacement.Manager, replacementDispatcher);
+                pendingField.SetValue(first.Chat, old);
+                await first.Chat.SubmitMessageAsync("yes");
+                Assert.That(replacement.Manager.Goals, Is.Empty);
+                Assert.That(first.Provider.Calls, Is.EqualTo(1));
+            }
+            using (var f = new ChatFixture(new SequenceSemanticProvider(proposal)))
+            {
+                f.Economy.Worker(0);
+                await f.Chat.SubmitMessageAsync("build a Mine");
+                var old = f.Chat.PendingClarification;
+                await f.Chat.SubmitMessageAsync("yes");
+                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(1));
+                pendingField.SetValue(f.Chat, old);
+                await f.Chat.SubmitMessageAsync("yes");
+                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(1));
+                Assert.That(f.Provider.Calls, Is.EqualTo(1));
+            }
+        }
+
+        [Test]
+        public void OverlongGraphOriginal_IsRejectedWithoutTruncation()
+        {
+            var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
+                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
+            var draft = CommanderSemanticJson.Parse(GraphDraft(new JArray(mine), "Independent", 0,
+                "structure", "Confirmation", new JValue("Mine"))).PendingGraphDraft;
+            string exact = new string('x', CommanderSemanticProviderRequest.MaximumPlayerMessageCharacters);
+            Assert.That(new CommanderPendingClarification(draft, exact, "confirm", 1, 1).OriginalText, Is.EqualTo(exact));
+            Assert.Throws<ArgumentOutOfRangeException>(() => new CommanderPendingClarification(draft,
+                exact + "y", "confirm", 1, 1));
+        }
+
+        [Test]
+        public async Task FourSlotGraph_AllLocalRepliesCompleteWithinBudget()
+        {
+            var allocation = new JObject { ["type"] = "AllocateWorkers", ["mode"] = "SelectedCount",
+                ["countMode"] = "Exact", ["workers"] = new JObject { ["state"] = "Idle" },
+                ["destination"] = new JObject() };
+            var house = new JObject { ["type"] = "BuildStructure", ["structure"] = "House", ["count"] = 1,
+                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
+            var proposal = JObject.Parse(GraphDraft(new JArray(allocation, house), "Independent", 0, "count", "Count", new JValue(3)));
+            var slots = (JArray)proposal["missingSlots"];
+            slots.Add(new JObject { ["node"] = 0, ["path"] = "destination.resource", ["type"] = "CanonicalName",
+                ["alternatives"] = new JArray("Food") });
+            slots.Add(new JObject { ["node"] = 0, ["path"] = "destination.sourceKind", ["type"] = "SourceKind",
+                ["alternatives"] = new JArray("Berries") });
+            slots.Add(new JObject { ["node"] = 1, ["path"] = "structure", ["type"] = "Confirmation",
+                ["alternatives"] = new JArray("House") });
+            using (var f = new ChatFixture(new SequenceSemanticProvider(proposal.ToString())))
+            {
+                for (int i = 0; i < 3; i++) f.Economy.Worker(i);
+                f.Economy.Node(ResourceType.Food, 12);
+                await f.Chat.SubmitMessageAsync("send three idle villagers to food and build a House");
+                Assert.That(f.Chat.PendingClarification?.GraphDraft?.MissingSlots.Count, Is.EqualTo(4));
+                foreach (string reply in new[] { "three", "Food", "from berries", "yes" })
+                    await f.Chat.SubmitMessageAsync(reply);
+                Assert.That(f.Provider.Calls, Is.EqualTo(1));
+                Assert.That(f.Chat.PendingClarification, Is.Null);
+                Assert.That(f.Chat.PendingActionPlan, Is.Not.Null);
+                Assert.That(f.Economy.Manager.Goals, Is.Empty);
+            }
+        }
+
+        [Test]
+        public async Task KnownSourceCorrection_ChangesOnlyNamedSource()
+        {
+            var allocation = new JObject { ["type"] = "AllocateWorkers", ["mode"] = "SelectedCount",
+                ["countMode"] = "Exact", ["workers"] = new JObject { ["state"] = "Idle" },
+                ["destination"] = new JObject { ["resource"] = "Food", ["sourceKind"] = "Sheep" } };
+            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(allocation),
+                "Independent", 0, "count", "Count", new JValue(3)))))
+            {
+                for (int i = 0; i < 3; i++) f.Economy.Worker(i);
+                f.Economy.Node(ResourceType.Food, 12);
+                await f.Chat.SubmitMessageAsync("send idle villagers from sheep to food");
+                await f.Chat.SubmitMessageAsync("from berries instead");
+                Assert.That(f.Provider.Calls, Is.EqualTo(1));
+                var node = (JObject)((JArray)f.Chat.PendingClarification.GraphDraft.ToPendingJson()["nodes"])[0];
+                Assert.That((string)node["destination"]["resource"], Is.EqualTo("Food"));
+                Assert.That((string)node["destination"]["sourceKind"], Is.EqualTo("Berries"));
+                await f.Chat.SubmitMessageAsync("three");
+                Assert.That(f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation.Destination.SourceKind,
+                    Is.EqualTo(ResourceSourceKind.Berries));
+            }
+        }
+
+        [Test]
+        public async Task IndependentBuildWithoutCorrectionMarker_SupersedesGraphDraft()
+        {
+            var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
+                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
+            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(mine),
+                "Independent", 0, "structure", "Confirmation", new JValue("Mine")),
+                Request("{\"type\":\"BuildStructure\",\"structure\":\"Mill\",\"count\":1}"))))
+            {
+                f.Economy.Worker(0);
+                await f.Chat.SubmitMessageAsync("build a Mine");
+                await f.Chat.SubmitMessageAsync("build a Mill");
+                Assert.That(f.Provider.Calls, Is.EqualTo(2));
+                Assert.That(f.Chat.PendingClarification, Is.Null);
+                Assert.That(f.Economy.Manager.Goals.OfType<BuildStructureGoal>().Single().StructureType,
+                    Is.EqualTo(BuildingType.Mill));
+            }
+        }
+
+        [Test]
+        public async Task AmbiguousKnownFieldCorrection_ReasksForOneNodeAndFieldLocally()
+        {
+            var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
+                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
+            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(mine.DeepClone(), mine.DeepClone()),
+                "Independent", 0, "structure", "Confirmation", new JValue("Mine")))))
+            {
+                await f.Chat.SubmitMessageAsync("build two Mines");
+                await f.Chat.SubmitMessageAsync("Mill instead");
+                Assert.That(f.Provider.Calls, Is.EqualTo(1));
+                Assert.That(f.Chat.PendingClarification, Is.Not.Null);
+                Assert.That(f.Economy.Manager.Goals, Is.Empty);
+                Assert.That(f.Chat.DisplayedTranscript, Does.Contain("node"));
+                Assert.That(f.Chat.DisplayedTranscript, Does.Contain("field"));
+            }
+        }
     }
 }
-
