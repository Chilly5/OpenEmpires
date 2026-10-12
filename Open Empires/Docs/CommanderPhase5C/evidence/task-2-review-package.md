# Frozen Task2 review package
Baseline HEAD 4c9de96adbf6342fbe9e1490fa9b898a0f99caf6; uncommitted working changes. No commits requested.
Task1 shared pack included full for Task2 grammar review; Task1 broader findings separate.
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Economy/CommanderPendingClarification.cs b/Open Empires/Assets/Scripts/AI/Commander/Economy/CommanderPendingClarification.cs
index 7161857..1000fc7 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Economy/CommanderPendingClarification.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Economy/CommanderPendingClarification.cs	
@@ -41,36 +41,47 @@ namespace OpenEmpires
             return result;
         }
         internal CommanderWorkerAllocationDraft WithCount(int count) => new CommanderWorkerAllocationDraft(Mode, CountMode, count, Workers, Destination, ResourceAmount, ResourceAmountMode);
         internal CommanderSemanticResult CompleteResult() => CommanderSemanticJson.Parse(
             new JObject { ["outcome"] = "Request", ["nodes"] = new JArray(ToJson()) }.ToString(Formatting.None));
     }
 
     public sealed class CommanderPendingClarification
     {
         public CommanderWorkerAllocationDraft Draft { get; }
+        public CommanderRequestGraphDraft GraphDraft { get; }
         public string OriginalText { get; }
         public string Question { get; }
         public int Turns { get; }
         internal int RuntimeGeneration { get; }
         internal long Sequence { get; }
         internal CommanderRequestTicket RequestTicket { get; }
         public long RequestId => RequestTicket?.Id ?? Sequence;
         internal CommanderPendingClarification(CommanderWorkerAllocationDraft draft, string original, string question,
             int generation, long sequence, int turns = 0, CommanderRequestTicket requestTicket = null)
         {
             Draft = draft; OriginalText = Bound(original, 1024); Question = Bound(question, 180);
             RuntimeGeneration = generation; Sequence = sequence; Turns = turns;
             RequestTicket = requestTicket;
         }
+        internal CommanderPendingClarification(CommanderRequestGraphDraft graphDraft, string original, string question,
+            int generation, long sequence, int turns = 0, CommanderRequestTicket requestTicket = null)
+        {
+            GraphDraft = graphDraft; OriginalText = Bound(original, 1024); Question = Bound(question, 180);
+            RuntimeGeneration = generation; Sequence = sequence; Turns = turns;
+            RequestTicket = requestTicket;
+        }
         internal string Serialize()
         {
+            if (GraphDraft != null) return new JObject { ["draftGraph"] = GraphDraft.ToPendingJson(),
+                ["missingSlots"] = GraphDraft.ToSlotsJson(), ["originalText"] = OriginalText,
+                ["question"] = Question }.ToString(Formatting.None);
             var missing = new JArray(); foreach (var field in Draft.MissingFields) missing.Add(field.ToString());
             return new JObject { ["draft"] = Draft.ToJson(), ["missingFields"] = missing, ["originalText"] = OriginalText, ["question"] = Question }.ToString(Formatting.None);
         }
         private static string Bound(string value, int maximum) => string.IsNullOrEmpty(value) ? string.Empty : value.Length <= maximum ? value : value.Substring(0, maximum);
     }
 
     public static class CommanderClarificationReplies
     {
         public static bool TryParseCount(string reply, out int count)
         {
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Clarification.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Clarification.cs
index 4d09f5d..0a4c41e 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Clarification.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Clarification.cs	
@@ -12,58 +12,92 @@ namespace OpenEmpires
         private bool destroyCleanupComplete;
         public CommanderPendingClarification PendingClarification => pendingClarification;
 
         private bool HandlePendingControl(string message)
         {
             if (pendingClarification == null) return false;
             if (pendingClarification.RuntimeGeneration != runtimeGeneration) { pendingClarification = null; return false; }
             string whole = message.Trim().ToLowerInvariant();
             if (whole == "cancel" || whole == "never mind" || whole == "nevermind" || whole == "forget it")
             {
-                pendingClarification = null; AppendLine("Player", message); AppendLine("Commander", "Pending worker request cancelled; no goal was created."); return true;
+                pendingClarification = null; AppendLine("Player", message); AppendLine("Commander", "Pending request cancelled; no goal was created."); return true;
+            }
+            if (pendingClarification.GraphDraft != null && whole == "no")
+            {
+                pendingClarification = null; AppendLine("Player", message);
+                AppendLine("Commander", "Proposal dismissed; existing tasks are unchanged."); return true;
             }
             // This only chooses whether a new turn supersedes a pending question.
             // It neither parses nor executes any gameplay action.
-            string[] standalone = { "build ", "train ", "make ", "produce ", "research ", "attack ", "scout ", "patrol ", "retreat ", "repair ", "defend ", "advance ", "put ", "gather ", "send ", "reach ", "assign ", "have ", "keep " };
+            string[] standalone = { "build ", "train ", "make ", "produce ", "research ", "attack ", "scout ", "patrol ", "retreat ", "repair ", "defend ", "advance ", "put ", "gather ", "send ", "reach ", "assign ", "have ", "keep ", "move ", "garrison ", "ungarrison ", "explore ", "find " };
             if (standalone.Any(prefix => whole.StartsWith(prefix, StringComparison.Ordinal)))
             { pendingClarification = null; return false; }
             var previous = pendingClarification;
-            pendingClarification = new CommanderPendingClarification(previous.Draft, previous.OriginalText, previous.Question,
-                previous.RuntimeGeneration, previous.Sequence, previous.Turns + 1, previous.RequestTicket);
-            if (whole == "yes" || whole == "no" || whole == "those" || whole == "there")
+            pendingClarification = previous.GraphDraft != null
+                ? new CommanderPendingClarification(previous.GraphDraft, previous.OriginalText, previous.Question,
+                    previous.RuntimeGeneration, previous.Sequence, previous.Turns + 1, previous.RequestTicket)
+                : new CommanderPendingClarification(previous.Draft, previous.OriginalText, previous.Question,
+                    previous.RuntimeGeneration, previous.Sequence, previous.Turns + 1, previous.RequestTicket);
+            if (previous.GraphDraft == null && (whole == "yes" || whole == "no" || whole == "those" || whole == "there"))
             { AppendLine("Player", message); ReaskPending("Please specify the missing worker criteria."); return true; }
             return false;
         }
 
         private void ReaskPending(string reason)
         {
             if (pendingClarification == null) return;
             if (pendingClarification.Turns >= 3)
-            { pendingClarification = null; AppendLine("Commander", "Worker clarification expired after three replies; please give a new complete request."); }
+            { pendingClarification = null; AppendLine("Commander", "Clarification expired after three replies; please give a new complete request."); }
             else AppendLine("Commander", reason + " " + pendingClarification.Question);
         }
 
         private CommanderSemanticResult LocalCountContinuation(CommanderPendingClarification pending, string reply)
         {
-            if (pending == null || pending.Draft.CountMode != CommanderWorkerCountMode.Exact || pending.Draft.Count.HasValue
+            if (pending?.Draft == null || pending.Draft.CountMode != CommanderWorkerCountMode.Exact || pending.Draft.Count.HasValue
                 || !CommanderClarificationReplies.TryParseCount(reply, out int count)) return null;
             var updated = pending.Draft.WithCount(count);
             if (updated.MissingFields.Count == 0) return updated.CompleteResult();
             var missing = new JArray(); foreach (var field in updated.MissingFields) missing.Add(field.ToString());
             return CommanderSemanticJson.Parse(new JObject { ["outcome"] = "Clarify", ["message"] = "Which resource should those villagers gather?",
                 ["pending"] = updated.ToJson(), ["missingFields"] = missing }.ToString(Formatting.None));
         }
 
+        private static CommanderSemanticResult LocalGraphContinuation(CommanderPendingClarification pending, string reply)
+            => pending?.GraphDraft?.Resolve(reply);
+
         private bool HandleClarificationResult(CommanderSemanticResult result, CommanderPendingClarification previous,
-            string reply, int generation, CommanderRequestTicket requestTicket)
+            string reply, int generation, CommanderRequestTicket requestTicket, bool localGraphResult = false)
         {
             if (previous != null && !ReferenceEquals(previous, pendingClarification)) return true;
+            if (previous?.GraphDraft != null)
+            {
+                if (localGraphResult && result.Outcome == CommanderSemanticOutcome.Request)
+                { pendingClarification = null; return false; }
+                if (localGraphResult && result.Outcome == CommanderSemanticOutcome.Clarify
+                    && result.PendingGraphDraft != null)
+                {
+                    pendingClarification = new CommanderPendingClarification(result.PendingGraphDraft,
+                        previous.OriginalText, result.SafeExplanation, generation, previous.Sequence,
+                        previous.Turns, previous.RequestTicket);
+                    return false;
+                }
+                ReaskPending("That reply did not safely resolve the retained request.");
+                return true;
+            }
+            if (result.PendingGraphDraft != null)
+            {
+                if (previous != null) { ReaskPending("That reply did not preserve the worker request."); return true; }
+                pendingClarification = new CommanderPendingClarification(result.PendingGraphDraft, reply,
+                    string.IsNullOrWhiteSpace(result.SafeExplanation) ? "Please clarify the proposed request." : result.SafeExplanation,
+                    generation, ++clarificationSequence, 0, requestTicket);
+                return false;
+            }
             if (result.PendingDraft != null)
             {
                 if (previous != null && !PreservesResolvedCriteria(previous.Draft, result.PendingDraft, reply))
                 { ReaskPending("The reply did not safely preserve the existing worker request."); return true; }
                 if (previous != null && previous.Turns >= 3)
                 { ReaskPending(string.Empty); return true; }
                 pendingClarification = new CommanderPendingClarification(result.PendingDraft,
                     previous?.OriginalText ?? reply, string.IsNullOrWhiteSpace(result.SafeExplanation) ? "Please specify the missing worker criteria." : result.SafeExplanation,
                     generation, previous?.Sequence ?? ++clarificationSequence, previous?.Turns ?? 0, previous?.RequestTicket ?? requestTicket);
                 return false;
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs
index ae93489..cb111bd 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs	
@@ -236,20 +236,27 @@ namespace OpenEmpires
             {
                 // Explicit strategic controls may invalidate an in-flight translation;
                 // the busy guard must not prevent the human from pausing/cancelling it.
                 // Pending worker slot-filling stays serialized with its active reply.
                 if (pendingClarification == null) TryHandleStrategicLifecycle(trimmed);
                 return null;
             }
             currentRequestGoalId = 0;
             if (HandlePendingControl(trimmed)) return null;
             if (HandleActionPlanControl(trimmed)) return null;
+            if (pendingClarification == null
+                && (NormalizeWholeForm(trimmed) == "yes" || NormalizeWholeForm(trimmed) == "no"))
+            {
+                AppendLine("Player", trimmed);
+                AppendLine("Commander", "No Commander question or action plan is awaiting that answer.");
+                return null;
+            }
             ClearActionPlanPreview(); // An independent player turn supersedes an unapproved candidate.
             // Any new player turn dismisses an adaptation, while no-plan recommendations
             // retain the established preference/explanation query behavior.
             if (pendingAdaptationProposal != null)
             {
                 strategicBridge?.ClearPending();
                 ClearStrategicPreview();
             }
             if (TryHandleStrategicLifecycle(trimmed)) return null;
             if (TryHandleExplanationQuery(trimmed)) return null;
@@ -451,23 +458,24 @@ namespace OpenEmpires
                         return null;
                     }
                     IReadOnlyList<CommanderSemanticMemoryEntry> semanticMemoryAtTurnStart =
                         Conversation.SemanticMemory.Snapshot();
                     TryObserveQuestionAnswer(NormalizeExplanationWholeForm(message), out string questionFacts);
                     var providerRequest = new CommanderSemanticProviderRequest(message, context,
                         semanticMemoryAtTurnStart, questionFacts, capturedPending);
                     semanticStage = "provider-request";
                     Debug.Log("[Commander] context=projected;provider-request=started");
                     CommanderSemanticResult localCount = LocalCountContinuation(capturedPending, message);
-                    Task<CommanderSemanticResult> translation = localCount == null
+                    CommanderSemanticResult localGraph = LocalGraphContinuation(capturedPending, message);
+                    Task<CommanderSemanticResult> translation = localCount == null && localGraph == null
                         ? RequestSemanticTranslation(provider, providerRequest, request.Token)
-                        : Task.FromResult(localCount);
+                        : Task.FromResult(localCount ?? localGraph);
                     // A provider may ignore cancellation. Keep the host usable and observe
                     // any late fault without ever admitting its late response.
                     _ = translation.ContinueWith(task => { var ignored = task.Exception; },
                         CancellationToken.None,
                         TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                         TaskScheduler.Default);
                     Task finished = await Task.WhenAny(translation,
                         Task.Delay(semanticProviderTimeout, request.Token));
                     if (finished != translation)
                     {
@@ -499,54 +507,56 @@ namespace OpenEmpires
                     if (!string.IsNullOrEmpty(questionFacts)
                         && (result.Outcome == CommanderSemanticOutcome.Request || result.Outcome == CommanderSemanticOutcome.DynamicPlan))
                     {
                         AppendLine("Commander", "You asked for information; no gameplay order was submitted.");
                         Debug.Log("[Commander] question=read-only;executable-response=rejected");
                         return null;
                     }
                     if (result.Outcome != CommanderSemanticOutcome.Request
                         && result.Outcome != CommanderSemanticOutcome.DynamicPlan)
                     {
-                        if (HandleClarificationResult(result, capturedPending, message, generation, requestTicket)) return null;
+                        if (HandleClarificationResult(result, capturedPending, message, generation, requestTicket,
+                            ReferenceEquals(result, localGraph))) return null;
                         if (result.Outcome == CommanderSemanticOutcome.Clarify)
                             Conversation.SemanticMemory.RecordClarification(result.SafeExplanation);
                         AppendLine("Commander", string.IsNullOrWhiteSpace(result.SafeExplanation)
                             ? "Please clarify or try a supported Commander request."
                             : result.SafeExplanation);
                         return null;
                     }
-                    if (HandleClarificationResult(result, capturedPending, message, generation, requestTicket)) return null;
+                    if (HandleClarificationResult(result, capturedPending, message, generation, requestTicket,
+                        ReferenceEquals(result, localGraph))) return null;
                     if (result.Outcome == CommanderSemanticOutcome.DynamicPlan)
                     {
                         semanticStage = "dynamic-admission";
-                        pendingActionPlan = manager.PrepareActionPlan(result, message, generation,
+                        pendingActionPlan = manager.PrepareActionPlan(result, requestTicket.OriginalInput, generation,
                             strategicPipeline, IsCurrentScope, requestTicket);
                         AppendLine("Commander", pendingActionPlan.Preview);
                         UpdateStrategicControls();
                         return null;
                     }
                     if (result.Nodes == null || result.Nodes.Count < 1
                         || result.Nodes.Count > CommanderSemanticGraphAdmission.MaximumNodes)
                     {
                         AppendLine("Commander", "That compound Commander request is outside the bounded request limit.");
                         return null;
                     }
                     if (result.Nodes.Count > 1 || result.Nodes[0].Type == CommanderSemanticNodeType.WatchFutureUnits)
                     {
                         semanticStage = "graph-admission";
                         if (!CommanderSemanticGraphAdmission.TryAdmit(result, context,
                             out CommanderSemanticGraphPlan graph, out string graphReason))
                         {
                             AppendLine("Commander", graphReason);
                             return null;
                         }
-                        pendingActionPlan = manager.PrepareActionPlan(result, message, generation,
+                        pendingActionPlan = manager.PrepareActionPlan(result, requestTicket.OriginalInput, generation,
                             strategicPipeline, IsCurrentScope, requestTicket);
                         AppendLine("Commander", pendingActionPlan.Preview);
                         UpdateStrategicControls();
                         return null;
                     }
                     CommanderSemanticNode node = result.Nodes[0];
                     if (node.Type == CommanderSemanticNodeType.StrategicObjective)
                     {
                         if (!node.StrategicObjectiveType.HasValue || bridge == null
                             || sourcePipeline == null || !sourceCaptured)
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticJson.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticJson.cs
index 856fc5f..c05ca4e 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticJson.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticJson.cs	
@@ -71,20 +71,26 @@ namespace OpenEmpires
                     case "Clarify": outcome = CommanderSemanticOutcome.Clarify; break;
                     case "Unsupported": outcome = CommanderSemanticOutcome.Unsupported; break;
                     case "Answer": outcome = CommanderSemanticOutcome.Answer; break;
                     default: throw new JsonException();
                 }
 
                 if (outcome != CommanderSemanticOutcome.Request)
                 {
                     if (outcome == CommanderSemanticOutcome.Clarify && root.Property("pending") != null)
                     {
+                        if (root["pending"] is JObject graph && graph.Property("nodes") != null)
+                        {
+                            CheckFields(root, "outcome", "message", "pending", "missingSlots");
+                            return new CommanderSemanticResult(true, outcome, Array.Empty<CommanderSemanticNode>(),
+                                OptionalExplanation(root), pendingGraphDraft: CommanderRequestGraphDraft.Parse(root));
+                        }
                         CheckFields(root, "outcome", "message", "pending", "missingFields");
                         return new CommanderSemanticResult(true, outcome, Array.Empty<CommanderSemanticNode>(),
                             OptionalExplanation(root), ParsePendingDraft(root));
                     }
                     CheckFields(root, "outcome", "message");
                     string message = OptionalExplanation(root);
                     return new CommanderSemanticResult(true, outcome,
                         Array.Empty<CommanderSemanticNode>(), message);
                 }
 
@@ -236,20 +242,22 @@ namespace OpenEmpires
                     if (watchedAction == CommanderFutureUnitAction.Patrol
                         && (RequiredString(node, "location") != "WorkedResource"
                             || node.Property("sourceKind") != null)) throw new JsonException();
                     if (watchedAction == CommanderFutureUnitAction.Gather && node.Property("location") != null)
                         throw new JsonException();
                     ResourceType watchedResource = ParseResource(RequiredString(node, "resource"));
                     if (watchedAction == CommanderFutureUnitAction.Gather && watchedUnit != 0)
                         throw new JsonException();
                     ResourceSourceKind? watchedSource = node.Property("sourceKind") == null
                         ? (ResourceSourceKind?)null : RequiredSemanticEnum<ResourceSourceKind>(node, "sourceKind");
+                    if (watchedSource.HasValue && !ResourceSourceRules.IsCompatible(watchedResource, watchedSource.Value))
+                        throw FailField(node, "sourceKind", SchemaFailureCode.IncompatibleDeclaration);
                     int? watchedOrdinal = node.Property("producerOrdinal") == null ? (int?)null
                         : RequiredBoundedInteger(node, "producerOrdinal", 1, 8);
                     if (!CanProduce(watchedProducer, watchedUnit)) throw new JsonException();
                     return new CommanderSemanticNode(CommanderSemanticNodeType.WatchFutureUnits,
                         unitType: watchedUnit, buildingType: watchedProducer, count: RequiredCount(node, 1, 50),
                         resourceType: watchedResource, dependsOn: dependsOn,
                         producerOrdinal: watchedOrdinal, futureAction: watchedAction, sourceKind: watchedSource);
 
                 case "ReachAge":
                     CheckFields(node, "type", "targetAge", "dependsOn", "constraints");
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticRequest.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticRequest.cs
index 423adf1..4de1650 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticRequest.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticRequest.cs	
@@ -161,25 +161,27 @@ namespace OpenEmpires
         }
     }
 
     public sealed partial class CommanderSemanticResult
     {
         public bool IsValid { get; }
         public CommanderSemanticOutcome Outcome { get; }
         public IReadOnlyList<CommanderSemanticNode> Nodes { get; }
         public string SafeExplanation { get; }
         public CommanderWorkerAllocationDraft PendingDraft { get; }
+        public CommanderRequestGraphDraft PendingGraphDraft { get; }
         public CommanderDynamicPlan DynamicPlan { get; }
 
         private CommanderSemanticResult(bool isValid, CommanderSemanticOutcome outcome,
             IReadOnlyList<CommanderSemanticNode> nodes, string safeExplanation, CommanderWorkerAllocationDraft pendingDraft = null,
-            CommanderDynamicPlan dynamicPlan = null)
+            CommanderDynamicPlan dynamicPlan = null, CommanderRequestGraphDraft pendingGraphDraft = null)
         {
             IsValid = isValid;
             Outcome = outcome;
             Nodes = nodes ?? Array.Empty<CommanderSemanticNode>();
             SafeExplanation = safeExplanation ?? string.Empty;
             PendingDraft = pendingDraft;
+            PendingGraphDraft = pendingGraphDraft;
             DynamicPlan = dynamicPlan;
         }
     }
 }
# Full source: Assets/Scripts/AI/Commander/Economy/CommanderRequestGraphDraft.cs
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    // Detached provider proposal. It carries typed, bounded data, never authority or a runtime selector.
    public sealed class CommanderRequestGraphDraft
    {
        public sealed class Slot
        {
            public int Node { get; }
            public string Path { get; }
            public string Type { get; }
            public IReadOnlyList<JToken> Alternatives { get; }
            internal Slot(int node, string path, string type, IList<JToken> alternatives)
            { Node = node; Path = path; Type = type; Alternatives = new List<JToken>(alternatives).AsReadOnly(); }
        }

        private readonly JObject graph;
        private readonly List<Slot> slots;
        public IReadOnlyList<Slot> MissingSlots => slots.AsReadOnly();
        public int NodeCount => ((JArray)graph["nodes"]).Count;
        private CommanderRequestGraphDraft(JObject graph, List<Slot> slots)
        { this.graph = (JObject)graph.DeepClone(); this.slots = slots; }

        internal static CommanderRequestGraphDraft Parse(JObject root)
        {
            if (!(root["pending"] is JObject graph) || !(graph["nodes"] is JArray nodes)
                || nodes.Count < 1 || nodes.Count > CommanderSemanticJson.MaximumNodes
                || !(root["missingSlots"] is JArray sourceSlots) || sourceSlots.Count < 1 || sourceSlots.Count > 4)
                throw new JsonException();
            ExactFields(graph, "executionOrder", "nodes");
            string order = (string)graph["executionOrder"];
            if (order != "Independent" && order != "Sequential" && order != "DependencyGraph") throw new JsonException();
            var slots = new List<Slot>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in sourceSlots)
            {
                if (!(entry is JObject spec)) throw new JsonException();
                ExactFields(spec, "node", "path", "type", "alternatives");
                if (spec["node"]?.Type != JTokenType.Integer || spec["path"]?.Type != JTokenType.String
                    || spec["type"]?.Type != JTokenType.String || !(spec["alternatives"] is JArray alternatives)
                    || alternatives.Count < 1 || alternatives.Count > 8) throw new JsonException();
                int index = (int)spec["node"];
                string path = (string)spec["path"], type = (string)spec["type"];
                if (index < 0 || index >= nodes.Count || !(nodes[index] is JObject node)
                    || !Allowed(type, path) || !seen.Add(index + ":" + path)) throw new JsonException();
                JObject parent = Parent(node, path);
                string leaf = Leaf(path);
                if (parent == null || type == "Confirmation" && parent[leaf]?.Type != JTokenType.String
                    || type != "Confirmation" && parent.Property(leaf) != null) throw new JsonException();
                var values = new List<JToken>();
                foreach (var choice in alternatives)
                {
                    if (choice == null || (choice.Type != JTokenType.String && choice.Type != JTokenType.Integer)
                        || choice.ToString().Length > 64 || type == "Count" && choice.Type != JTokenType.Integer
                        || type != "Count" && choice.Type != JTokenType.String
                        || type == "Confirmation" && (string)choice != (string)parent[leaf]) throw new JsonException();
                    values.Add(choice.DeepClone());
                }
                slots.Add(new Slot(index, path, type, values));
            }
            var draft = new CommanderRequestGraphDraft(graph, slots);
            // Every offered completion, not just the first, must survive the existing strict
            // parser and graph checks. Temporary values never enter the stored graph.
            foreach (var slot in slots)
                foreach (var alternative in slot.Alternatives)
                    if (!draft.Probe(slot, alternative).IsValid) throw new JsonException();
            return draft;
        }

        private CommanderSemanticResult Probe(Slot changed, JToken value)
        {
            var completed = (JObject)graph.DeepClone();
            foreach (var slot in slots)
            {
                var parent = Parent((JObject)((JArray)completed["nodes"])[slot.Node], slot.Path);
                if (slot.Type != "Confirmation") parent[Leaf(slot.Path)] = (slot == changed ? value : slot.Alternatives[0]).DeepClone();
            }
            return CommanderSemanticJson.ParseProviderResponse(new JObject { ["outcome"] = "Request",
                ["executionOrder"] = completed["executionOrder"].DeepClone(),
                ["nodes"] = completed["nodes"].DeepClone() }.ToString(Formatting.None));
        }

        internal CommanderSemanticResult Resolve(string reply)
        {
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
            if (slots.Count == 1)
            {
                var parsed = Probe(slot, chosen);
                return parsed.IsValid ? parsed : null;
            }
            var next = (JObject)graph.DeepClone();
            if (slot.Type != "Confirmation")
                Parent((JObject)((JArray)next["nodes"])[slot.Node], slot.Path)[Leaf(slot.Path)] = chosen.DeepClone();
            var remaining = new JArray();
            foreach (var other in slots)
                if (!ReferenceEquals(other, slot))
                    remaining.Add(new JObject { ["node"] = other.Node, ["path"] = other.Path,
                        ["type"] = other.Type, ["alternatives"] = new JArray(other.Alternatives) });
            return CommanderSemanticJson.Parse(new JObject { ["outcome"] = "Clarify",
                ["message"] = "Please specify the remaining detail.", ["pending"] = next,
                ["missingSlots"] = remaining }.ToString(Formatting.None));
        }

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
                    if (string.Equals((string)offered, canonical, StringComparison.OrdinalIgnoreCase)) { chosen = offered; break; }
            }
            return chosen;
        }

        private static string SourceAlias(string reply)
        {
            switch (reply)
            {
                case "from trees": case "trees": case "tree": case "from tree": return "Tree";
                case "from berries": case "berries": case "berry": return "Berries";
                case "from sheep": case "sheep": return "Sheep";
                case "from farms": case "farms": case "farm": return "Farm";
                case "from gold mines": case "gold mines": case "gold mine": return "GoldMine";
                case "from stone mines": case "stone mines": case "stone mine": return "StoneMine";
                default: return reply;
            }
        }

        internal JObject ToPendingJson() => (JObject)graph.DeepClone();
        internal JArray ToSlotsJson()
        {
            var result = new JArray();
            foreach (var slot in slots)
                result.Add(new JObject { ["node"] = slot.Node, ["path"] = slot.Path,
                    ["type"] = slot.Type, ["alternatives"] = new JArray(slot.Alternatives) });
            return result;
        }

        private static bool Allowed(string type, string path)
        {
            if (type == "Confirmation") return path == "structure" || path == "unit" || path == "producer";
            if (type == "Count") return path == "count";
            if (type == "SourceKind") return path == "sourceKind" || path == "destination.sourceKind" || path == "placement.sourceKind";
            if (type == "CanonicalName") return path == "structure" || path == "unit" || path == "producer"
                || path == "technology" || path == "resource" || path == "destination.resource" || path == "placement.resource";
            return false;
        }
        private static JObject Parent(JObject node, string path)
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
# Full source: Assets/Tests/EditMode/CommanderEconomyClarificationTests.Graph.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace OpenEmpires.Tests
{
    public sealed partial class CommanderEconomyClarificationTests
    {
        private static string GraphDraft(JArray nodes, string order, int index, string path, string type, params JToken[] alternatives) =>
            new JObject { ["outcome"] = "Clarify", ["message"] = "Please confirm the missing detail.",
                ["pending"] = new JObject { ["executionOrder"] = order, ["nodes"] = nodes },
                ["missingSlots"] = new JArray(new JObject { ["node"] = index, ["path"] = path,
                    ["type"] = type, ["alternatives"] = new JArray(alternatives) }) }.ToString();

        [Test]
        public async Task MineProposal_YesUsesOriginalRequestAndNoSecondTranslation()
        {
            var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 },
                ["placement"] = new JObject { ["anchor"] = "VisibleResource", ["relation"] = "Near",
                    ["resource"] = "Gold", ["sourceKind"] = "GoldMine" } };
            string proposal = GraphDraft(new JArray(mine), "Independent", 0, "structure", "Confirmation", new JValue("Mine"));
            using (var f = new ChatFixture(new SequenceSemanticProvider(proposal)))
            {
                f.Economy.Worker(0); f.Economy.Node(ResourceType.Gold, 8);
                await f.Chat.SubmitMessageAsync("build a gold mine near gold");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null);
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                var result = await f.Chat.SubmitMessageAsync("yes");
                Assert.That(result?.Success, Is.True);
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                Assert.That(f.Chat.PendingClarification, Is.Null);
                var goal = f.Economy.Manager.Goals.OfType<BuildStructureGoal>().Single();
                Assert.That(goal.StructureType, Is.EqualTo(BuildingType.Mine));
                Assert.That(goal.PlacementAnchorSelector, Is.EqualTo(CommanderSemanticAnchorSelector.VisibleResource));
                Assert.That(goal.PlacementResourceType, Is.EqualTo(ResourceType.Gold));
                Assert.That(goal.PlacementSourceKind, Is.EqualTo(ResourceSourceKind.GoldMine));
                Assert.That(goal.RequestAuthority.OriginalInput, Is.EqualTo("build a gold mine near gold"));
            }
        }

        [Test]
        public async Task HouseThenNextFive_FromTreesKeepsGraphAndNeedsSeparateApproval()
        {
            var house = new JObject { ["type"] = "BuildStructure", ["structure"] = "House", ["count"] = 1,
                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
            var watch = new JObject { ["type"] = "WatchFutureUnits", ["unit"] = "Villager", ["count"] = 5,
                ["producer"] = "TownCenter", ["producerOrdinal"] = 1, ["action"] = "Gather", ["resource"] = "Wood",
                ["dependsOn"] = new JArray(0) };
            string proposal = GraphDraft(new JArray(house, watch), "Sequential", 1, "sourceKind", "SourceKind", new JValue("Tree"));
            using (var f = new ChatFixture(new SequenceSemanticProvider(proposal)))
            {
                f.Economy.Worker(0);
                f.Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter, f.Economy.Base.x, f.Economy.Base.y, false, true).AutoProduceVillagers = false;
                await f.Chat.SubmitMessageAsync("Build a House, then send the next five Villagers from my first Town Center to Wood");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null);
                await f.Chat.SubmitMessageAsync("From trees");
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                Assert.That(f.Chat.PendingClarification, Is.Null);
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Chat.PendingActionPlan, Is.Not.Null);
                var nodes = f.Chat.PendingActionPlan.Interpretation.Nodes;
                Assert.That(nodes.Count, Is.EqualTo(2));
                Assert.That(nodes[1].DependsOn, Is.EquivalentTo(new[] { 0 }));
                Assert.That(nodes[1].Count, Is.EqualTo(5));
                Assert.That(nodes[1].ProducerOrdinal, Is.EqualTo(1));
                Assert.That(nodes[1].ResourceType, Is.EqualTo(ResourceType.Wood));
                Assert.That(nodes[1].SourceKind, Is.EqualTo(ResourceSourceKind.Tree));
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(2));
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(2));
            }
        }

        [Test]
        public async Task NoRejectsOnlyProposal_AndIndependentCommandSupersedes()
        {
            var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
            string proposal = GraphDraft(new JArray(mine), "Independent", 0, "structure", "Confirmation", new JValue("Mine"));
            using (var f = new ChatFixture(new SequenceSemanticProvider(
                Request("{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4}"), proposal, proposal,
                Request("{\"type\":\"EnsureUnitCount\",\"unit\":\"Archer\",\"count\":3}"))))
            {
                await f.Chat.SubmitMessageAsync("train four spearmen");
                int accepted = f.Economy.Manager.Goals.Count;
                await f.Chat.SubmitMessageAsync("build a gold mine");
                await f.Chat.SubmitMessageAsync("no");
                Assert.That(f.Chat.PendingClarification, Is.Null);
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(accepted));
                await f.Chat.SubmitMessageAsync("build a gold mine");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null);
                await f.Chat.SubmitMessageAsync("train three archers");
                Assert.That(f.Chat.PendingClarification, Is.Null);
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(accepted + 1));
            }
        }

        [Test]
        public void GraphDraftRejectsInvalidAlternativesAndAuthority()
        {
            var build = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
            var valid = JObject.Parse(GraphDraft(new JArray(build), "Independent", 0, "structure", "Confirmation", new JValue("Mine")));
            Assert.That(CommanderSemanticJson.Parse(valid.ToString()).IsValid, Is.True);
            foreach (Action<JObject> corrupt in new Action<JObject>[] {
                x => ((JObject)x["pending"])["playerId"] = 0,
                x => ((JObject)((JArray)((JObject)x["pending"])["nodes"])[0])["entityId"] = 10,
                x => ((JObject)((JArray)x["missingSlots"])[0])["type"] = "RuntimeId",
                x => ((JObject)((JArray)x["missingSlots"])[0])["path"] = "placement.tile",
                x => ((JObject)((JArray)x["missingSlots"])[0])["alternatives"] = new JArray("Barracks"),
                x => ((JObject)((JArray)x["missingSlots"])[0])["node"] = 2 })
            {
                var copy = (JObject)valid.DeepClone(); corrupt(copy);
                Assert.That(CommanderSemanticJson.Parse(copy.ToString()).IsValid, Is.False, copy.ToString());
            }
            var watch = new JObject { ["type"] = "WatchFutureUnits", ["unit"] = "Villager", ["count"] = 5,
                ["producer"] = "TownCenter", ["action"] = "Gather", ["resource"] = "Wood" };
            Assert.That(CommanderSemanticJson.Parse(GraphDraft(new JArray(watch), "Independent", 0,
                "sourceKind", "SourceKind", new JValue("GoldMine"))).IsValid, Is.False,
                "Every offered source must be compatible with the retained resource.");
        }

        [Test]
        public async Task SourceReply_PreservesAmountModeAndWoodWorkerCriterion()
        {
            var allocation = new JObject { ["type"] = "AllocateWorkers", ["mode"] = "SelectedCount",
                ["countMode"] = "Exact", ["count"] = 6,
                ["workers"] = new JObject { ["state"] = "Gathering", ["currentResource"] = "Wood" },
                ["destination"] = new JObject { ["resource"] = "Food" },
                ["resourceAmount"] = 400, ["resourceAmountMode"] = "AdditionalGathered" };
            string proposal = GraphDraft(new JArray(allocation), "Independent", 0, "destination.sourceKind",
                "SourceKind", new JValue("Berries"));
            using (var f = new ChatFixture(new SequenceSemanticProvider(proposal)))
            {
                for (int i = 0; i < 6; i++) f.Economy.Assigned(f.Economy.Worker(i), f.Economy.Node(ResourceType.Wood, 15 + i));
                f.Economy.Node(ResourceType.Food, 30);
                await f.Chat.SubmitMessageAsync("send six wood villagers to gather 400 additional food");
                var submitted = await f.Chat.SubmitMessageAsync("from berries");
                Assert.That(submitted?.Success, Is.True);
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                var retained = f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation;
                Assert.That(retained.Count, Is.EqualTo(6));
                Assert.That(retained.Workers.CurrentResource, Is.EqualTo(ResourceType.Wood));
                Assert.That(retained.ResourceAmount, Is.EqualTo(400));
                Assert.That(retained.ResourceAmountMode, Is.EqualTo(CommanderResourceAmountMode.AdditionalGathered));
                Assert.That(retained.Destination.SourceKind, Is.EqualTo(ResourceSourceKind.Berries));
            }
        }

        [Test]
        public async Task ResetDropsGraphDraft_AndBareYesCannotBecomeANewGoal()
        {
            var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
            string proposal = GraphDraft(new JArray(mine), "Independent", 0, "structure", "Confirmation", new JValue("Mine"));
            using (var f = new ChatFixture(new SequenceSemanticProvider(proposal,
                Request("{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":4}"))))
            {
                await f.Chat.SubmitMessageAsync("build a gold mine");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null);
                f.Chat.ResetConversation();
                Assert.That(f.Chat.PendingClarification, Is.Null);
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Provider.Calls, Is.EqualTo(1), "An orphan confirmation is not a new model order.");
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
            }
        }

        [Test]
        public async Task MovementCommandSupersedesGraphDraftWithoutCancellingAcceptedWork()
        {
            var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
            string proposal = GraphDraft(new JArray(mine), "Independent", 0, "structure", "Confirmation", new JValue("Mine"));
            using (var f = new ChatFixture(new SequenceSemanticProvider(proposal,
                "{\"outcome\":\"Unsupported\",\"message\":\"No safe destination.\"}")))
            {
                await f.Chat.SubmitMessageAsync("build a gold mine");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null);
                await f.Chat.SubmitMessageAsync("move villagers to my base");
                Assert.That(f.Provider.Calls, Is.EqualTo(2));
                Assert.That(f.Chat.PendingClarification, Is.Null);
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
            }
        }

        [Test]
        public async Task TwoMissingSlots_CountThenSourceResumeSameDraftLocally()
        {
            var allocation = new JObject { ["type"] = "AllocateWorkers", ["mode"] = "SelectedCount",
                ["countMode"] = "Exact", ["workers"] = new JObject { ["state"] = "Idle" },
                ["destination"] = new JObject { ["resource"] = "Food" } };
            var proposal = JObject.Parse(GraphDraft(new JArray(allocation), "Independent", 0,
                "count", "Count", new JValue(2)));
            ((JArray)proposal["missingSlots"]).Add(new JObject { ["node"] = 0,
                ["path"] = "destination.sourceKind", ["type"] = "SourceKind",
                ["alternatives"] = new JArray("Berries") });
            using (var f = new ChatFixture(new SequenceSemanticProvider(proposal.ToString())))
            {
                for (int i = 0; i < 3; i++) f.Economy.Worker(i);
                f.Economy.Node(ResourceType.Food, 12);
                await f.Chat.SubmitMessageAsync("send idle villagers to food");
                Assert.That(f.Chat.PendingClarification?.GraphDraft?.MissingSlots.Count, Is.EqualTo(2));
                await f.Chat.SubmitMessageAsync("three");
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
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
    }
}
# Full source: Assets/Scripts/AI/Commander/Phase5C/CommanderSemanticRequestPack.cs
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
            "JSON only;strict keys/enums,no authority/IDs/coordinates/commands/hidden or extra goals. Keep counts,negations,actors,sources,constraints,order,exact results;unknown stays unknown. "
            + "Request{outcome:Request,nodes:typed1..4,executionOrder:Independent|Sequential|DependencyGraph}. then/after=completion dependsOn:[prior0-based];resultFromNode=exact new results;producerFromNode=explicit new producer. a/an=1;number words;left=MapWest. "
            + "Clarify{outcome:Clarify,message<=180,pending:{executionOrder,nodes:partial1..4},missingSlots:[{node,path,type:Confirmation|Count|SourceKind|CanonicalName,alternatives:[canonical]}]};keep known fields/edges/refs,omit missing leaves;Confirmation keeps offered value. Worker pending+missingFields valid. Clarify!=approval. Answer/Unsupported{outcome,message<=180},no nodes.";

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
