# Task1 fix4 frozen review package

Baseline: fix3-final snapshots; SemanticProvider baseline recovered from the earlier
nativeTask3 snapshot, SHA256 A1A0E5017C6D34FF7EE3F1B57E6B4524FA83C86F85BCF942651B929F3CCB4EBF.
No commits; new HintGate test file included in full after incremental production diffs.
Requirements: task-1-fix4-brief.md. Prior finding: task-1-fix3-review.md.
Report: task-1-fix-report.md FIX4. Parent inspected raw5/5 and16/16 saved results.
Expected foreign/replay/timeout fixture diagnostics reported, not compile errors.
Current source is frozen; no paid/native/full-regression/build claim.

## Incremental production diffs
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-1-fix4-baseline\\Assets__Scripts__AI__Commander__Economy__CommanderRequestGraphDraft.cs.snapshot" b/Assets/Scripts/AI/Commander/Economy/CommanderRequestGraphDraft.cs
index be1c5a3..ed310b3 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-1-fix4-baseline\\Assets__Scripts__AI__Commander__Economy__CommanderRequestGraphDraft.cs.snapshot"
+++ b/Assets/Scripts/AI/Commander/Economy/CommanderRequestGraphDraft.cs
@@ -1,14 +1,15 @@
 using System;
 using System.Collections.Generic;
+using System.Linq;
 using System.Text.RegularExpressions;
 using Newtonsoft.Json;
 using Newtonsoft.Json.Linq;
 
 namespace OpenEmpires
 {
     // Detached provider proposal. It carries typed, bounded data, never authority or a runtime selector.
     public sealed class CommanderRequestGraphDraft
     {
         public sealed class Slot
         {
             private readonly List<JToken> alternatives;
@@ -24,29 +25,67 @@ namespace OpenEmpires
                     return detached.AsReadOnly();
                 }
             }
             internal Slot(int node, string path, string type, IList<JToken> alternatives)
             {
                 Node = node; Path = path; Type = type; this.alternatives = new List<JToken>();
                 foreach (var value in alternatives) this.alternatives.Add(value.DeepClone());
             }
         }
 
         private readonly JObject graph;
         private readonly List<Slot> slots;
+        private readonly List<KeyValuePair<string, string>> nameConfirmations;
+        internal bool IsNameConfirmation => nameConfirmations != null;
+        internal string NameConfirmationQuestion => nameConfirmations.Count == 0
+            ? "Blocked: the proposed content does not uniquely match the unresolved similar name. Original request retained; no action was submitted."
+            : "Did you mean " + nameConfirmations[0].Value + " for that similar name? Confirm the name with yes or its name, or no to dismiss. Gameplay approval is separate.";
         public IReadOnlyList<Slot> MissingSlots => slots.AsReadOnly();
         public int NodeCount => ((JArray)graph["nodes"]).Count;
         private CommanderRequestGraphDraft(JObject graph, List<Slot> slots)
         { this.graph = (JObject)graph.DeepClone(); this.slots = slots; }
 
+        private CommanderRequestGraphDraft(JObject proposal, List<KeyValuePair<string, string>> confirmations)
+            : this(proposal, new List<Slot>()) { nameConfirmations = confirmations; }
+
+        // Only called after strict provider parsing. Retain the entire proposal, including
+        // the DynamicPlan envelope, and re-enter the same parser/admission path locally.
+        internal static CommanderRequestGraphDraft ConfirmNames(string strictProposal, JArray hints)
+        {
+            var root = JObject.Parse(strictProposal);
+            var catalog = GameKnowledgeCatalog.BuildCanonicalIdentityCatalog();
+            var confirmations = new List<KeyValuePair<string, string>>();
+            foreach (JObject hint in hints)
+            {
+                var matches = new List<KeyValuePair<string, string>>();
+                foreach (string id in ((JArray)hint["unitCandidates"]).Concat((JArray)hint["buildingCandidates"]).Values<string>())
+                {
+                    var unit = catalog.FindUnit(id);
+                    var building = catalog.FindBuilding(id);
+                    string display = unit?.DisplayName ?? building?.DisplayName;
+                    if (display == null) continue;
+                    bool named = root.Descendants().OfType<JProperty>().Any(p => p.Value.Type == JTokenType.String &&
+                        (unit != null ? p.Name == "unit" : p.Name == "structure" || p.Name == "producer" || p.Name == "building") &&
+                        ((string)p.Value == id || (unit != null
+                            ? CommanderIntentCatalog.TryResolveUnit((string)p.Value, out int u) && u == unit.UnitType
+                            : CommanderIntentCatalog.TryResolveStructure((string)p.Value, out BuildingType b) && b == building.BuildingType)));
+                    if (named) matches.Add(new KeyValuePair<string, string>(id, display));
+                }
+                // An omitted, unrelated, or multiply interpreted suggestion cannot be approved.
+                if (matches.Count != 1) return new CommanderRequestGraphDraft(root, new List<KeyValuePair<string, string>>());
+                if (!confirmations.Any(c => c.Key == matches[0].Key)) confirmations.Add(matches[0]);
+            }
+            return new CommanderRequestGraphDraft(root, confirmations);
+        }
+
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
@@ -90,24 +129,37 @@ namespace OpenEmpires
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
+            if (IsNameConfirmation)
+            {
+                if (nameConfirmations.Count == 0) return null;
+                var proposed = nameConfirmations[0];
+                string whole = (reply ?? string.Empty).Trim();
+                if (!string.Equals(whole, "yes", StringComparison.OrdinalIgnoreCase)
+                    && !string.Equals(whole, "y", StringComparison.OrdinalIgnoreCase)
+                    && !string.Equals(whole, proposed.Value, StringComparison.OrdinalIgnoreCase)
+                    && !string.Equals(whole, proposed.Key, StringComparison.Ordinal)) return null;
+                if (nameConfirmations.Count > 1)
+                    return CommanderSemanticResult.NameConfirmation(new CommanderRequestGraphDraft(graph, nameConfirmations.Skip(1).ToList()));
+                return CommanderSemanticJson.ParseProviderResponse(graph.ToString(Formatting.None));
+            }
             if (IsCorrectionUtterance(reply)) return Correct(reply);
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
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-1-fix4-baseline\\Assets__Scripts__AI__Commander__Phase4A__CommanderChatUI.Clarification.cs.snapshot" b/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Clarification.cs
index b5fd017..8365499 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-1-fix4-baseline\\Assets__Scripts__AI__Commander__Phase4A__CommanderChatUI.Clarification.cs.snapshot"
+++ b/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Clarification.cs
@@ -75,25 +75,26 @@ namespace OpenEmpires
                 ["pending"] = updated.ToJson(), ["missingFields"] = missing }.ToString(Formatting.None));
         }
 
         private static CommanderSemanticResult LocalGraphContinuation(CommanderPendingClarification pending, string reply)
             => pending?.GraphDraft?.Resolve(reply);
 
         private bool HandleClarificationResult(CommanderSemanticResult result, CommanderPendingClarification previous,
             string reply, int generation, CommanderRequestTicket requestTicket, bool localGraphResult = false)
         {
             if (previous != null && !ReferenceEquals(previous, pendingClarification)) return true;
             if (previous?.GraphDraft != null)
             {
-                if (localGraphResult && result.Outcome == CommanderSemanticOutcome.Request)
+                if (localGraphResult && (result.Outcome == CommanderSemanticOutcome.Request
+                    || result.Outcome == CommanderSemanticOutcome.DynamicPlan))
                 { pendingClarification = null; return false; }
                 if (localGraphResult && result.Outcome == CommanderSemanticOutcome.Clarify
                     && result.PendingGraphDraft != null)
                 {
                     pendingClarification = new CommanderPendingClarification(result.PendingGraphDraft,
                         previous.OriginalText, result.SafeExplanation, generation, previous.Sequence,
                         previous.Turns, previous.RequestTicket);
                     return false;
                 }
                 ReaskPending("That reply did not safely resolve the retained request.");
                 return true;
             }
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-1-fix4-baseline\\Assets__Scripts__AI__Commander__Phase4A__CommanderChatUI.cs.snapshot" b/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs
index a16dea5..f4d1212 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-1-fix4-baseline\\Assets__Scripts__AI__Commander__Phase4A__CommanderChatUI.cs.snapshot"
+++ b/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs
@@ -442,50 +442,49 @@ namespace OpenEmpires
                 request.CancelAfter(semanticProviderTimeout);
                 try
                 {
                     bool IsCurrentScope() => this != null && runtimeGeneration == generation
                         && !lifetime.IsCancellationRequested
                         && ReferenceEquals(semanticGoalManager, manager)
                         && ReferenceEquals(semanticSimulation, simulation)
                         && ReferenceEquals(semanticDispatcher, dispatcher)
                         && ReferenceEquals(semanticProvider, provider);
                     var requestTicket = capturedPending?.RequestTicket ?? manager.BeginSemanticRequest(message, generation, IsCurrentScope);
                     semanticStage = "context-projection";
                     CommanderContext context = new CommanderContextBuilder().Build(simulation, manager,
-                        message, capturedPending?.Serialize());
+                        message, capturedPending?.GraphDraft?.IsNameConfirmation == true ? null : capturedPending?.Serialize());
                     if (context.PlayerId != owner)
                     {
                         AppendLine("Commander", "Commander ownership changed; ask again.");
                         return null;
                     }
                     IReadOnlyList<CommanderSemanticMemoryEntry> semanticMemoryAtTurnStart =
                         Conversation.SemanticMemory.Snapshot();
                     TryObserveQuestionAnswer(NormalizeExplanationWholeForm(message), out string questionFacts);
-                    var providerRequest = new CommanderSemanticProviderRequest(message, context,
-                        semanticMemoryAtTurnStart, questionFacts, capturedPending);
                     CommanderSemanticResult localCount = LocalCountContinuation(capturedPending, message);
                     CommanderSemanticResult localGraph = LocalGraphContinuation(capturedPending, message);
                     if (capturedPending?.GraphDraft != null && localGraph == null)
                     {
                         semanticStage = "graph-local-reask";
                         ReaskPending(CommanderRequestGraphDraft.IsCorrectionUtterance(message)
                             ? "Please identify one graph node and field to correct with a supported value."
                             : "That reply did not identify one safe graph detail.");
                         return null;
                     }
                     semanticStage = localCount != null || localGraph != null ? "local-continuation" : "provider-request";
                     if (localCount == null && localGraph == null)
                         Debug.Log("[Commander] context=projected;provider-request=started");
                     Task<CommanderSemanticResult> translation = localCount == null && localGraph == null
-                        ? RequestSemanticTranslation(provider, providerRequest, request.Token)
+                        ? RequestSemanticTranslation(provider, new CommanderSemanticProviderRequest(message, context,
+                            semanticMemoryAtTurnStart, questionFacts, capturedPending), request.Token)
                         : Task.FromResult(localCount ?? localGraph);
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
                         request.Cancel();
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-1-fix4-baseline\\Assets__Scripts__AI__Commander__Phase4A__OpenRouterCommanderProvider.cs.snapshot" b/Assets/Scripts/AI/Commander/Phase4A/OpenRouterCommanderProvider.cs
index 216ab48..8772a9f 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-1-fix4-baseline\\Assets__Scripts__AI__Commander__Phase4A__OpenRouterCommanderProvider.cs.snapshot"
+++ b/Assets/Scripts/AI/Commander/Phase4A/OpenRouterCommanderProvider.cs
@@ -279,36 +279,37 @@ namespace OpenEmpires
                     JObject repairJson = JObject.Parse(repairBody);
                     var repairSections = new Dictionary<string, int> {
                         ["contract"] = Encoding.UTF8.GetByteCount((string)repairJson["messages"]?[0]?["content"] ?? string.Empty),
                         ["repairTemplate"] = Encoding.UTF8.GetByteCount((string)repairJson["messages"]?[1]?["content"] ?? string.Empty),
                         ["messageMargin"] = CommanderSemanticRequestPack.MessageMargin };
                     string initialTrace = run.Trace;
                     string repairedText = UnwrapJsonFence(await PostAndExtractTextAsync(repairBody,
                         token, CommanderDynamicPlan.MaximumCharacters, "repair", repairEstimate,
                         repairSections, run).ConfigureAwait(false));
                     run.Trace = initialTrace + "repair-attempt:\n" + run.Trace;
                     Publish(run);
                     result = CommanderSemanticJson.ParseProviderResponse(repairedText);
+                    text = repairedText;
                     if (!result.IsValid) Trace("repair-schema=" + result.SchemaDiagnostic, run);
                     if (!result.IsValid || !CommanderSemanticSchemaRepair.MatchesTemplate(
                         repairedText, template))
                         result = CommanderSemanticResult.ProviderRejected(
                             "Commander AI returned an invalid semantic response; no order was submitted.");
                     MarkLastSemanticAttempt(run, result.IsValid ? "semantic-valid" : "schema-invalid");
                 }
                 Trace("semantic=" + (result.IsValid ? result.Outcome.ToString() : "invalid")
                     + ";nodes=" + result.Nodes.Count, run);
                 run.Validated = result.IsValid;
                 Publish(run);
-                return result.IsValid ? result : CommanderSemanticResult.ProviderRejected(
+                return result.IsValid ? result.RequireNameConfirmation(text, request) : CommanderSemanticResult.ProviderRejected(
                     "Commander AI returned an invalid semantic response; no order was submitted.");
             }
             catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
             catch (OperationCanceledException)
             {
                 Trace("request=timeout", run);
                 run.TimedOut = true;
                 Publish(run);
                 return CommanderSemanticResult.ProviderRejected("Commander AI request timed out.");
             }
             catch (OpenRouterFailure failure)
             {
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-1-fix4-baseline\\Assets__Scripts__AI__Commander__Phase4E__CommanderSemanticProvider.cs.snapshot" b/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticProvider.cs
index 53a18e6..b12f6dc 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-1-fix4-baseline\\Assets__Scripts__AI__Commander__Phase4E__CommanderSemanticProvider.cs.snapshot"
+++ b/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticProvider.cs
@@ -19,34 +19,40 @@ namespace OpenEmpires
     {
         public const int MaximumPlayerMessageCharacters = 1024;
         public const int MaximumContextCharacters = 8192;
         public const int MaximumSemanticMemoryCharacters = 4096;
 
         public string PlayerMessage { get; }
         public string SerializedContext { get; }
         public string SerializedSemanticMemory { get; }
         public string QuestionFacts { get; }
         public string SerializedPendingClarification { get; }
         public bool IsPlayerMessageTooLong { get; }
         public bool IsReadOnlyQuestion { get; }
+        // Game-owned retrieval metadata, retained independently of optional model-pack rows.
+        internal string UnresolvedNameInterpretations { get; }
 
         public CommanderSemanticProviderRequest(string playerMessage, CommanderContext context,
             IReadOnlyList<CommanderSemanticMemoryEntry> semanticMemory = null, string questionFacts = null,
             CommanderPendingClarification pendingClarification = null,
             CommanderTacticalStatusSnapshot tacticalStatus = null, bool readOnlyQuestion = false)
         {
             if (context == null) throw new ArgumentNullException(nameof(context));
             string text = playerMessage ?? string.Empty;
             IsPlayerMessageTooLong = text.Length > MaximumPlayerMessageCharacters;
             PlayerMessage = IsPlayerMessageTooLong ? string.Empty : text;
+            var knowledge = ParseKnowledge(context.KnowledgeContext);
+            UnresolvedNameInterpretations = new JArray((knowledge["nameInterpretations"] as JArray ?? new JArray())
+                .OfType<JObject>().Where(h => (bool?)h["interpretationOnly"] == true && (bool?)h["ambiguous"] == false))
+                .ToString(Formatting.None);
             SerializedContext = SerializeContext(context,tacticalStatus);
             SerializedPendingClarification = pendingClarification?.Serialize() ?? string.Empty;
             if (SerializedPendingClarification.Length > 4096)
                 throw new InvalidOperationException("Pending clarification exceeds its bound.");
             string facts = questionFacts ?? tacticalStatus?.Answer() ?? string.Empty;
             QuestionFacts = facts.Length > 512 ? facts.Substring(0, 512) : facts;
             IsReadOnlyQuestion=readOnlyQuestion||tacticalStatus!=null||!string.IsNullOrEmpty(questionFacts);
             SerializedSemanticMemory = CommanderSemanticConversationMemory.Serialize(semanticMemory
                 ?? Array.Empty<CommanderSemanticMemoryEntry>());
             if (SerializedSemanticMemory.Length > MaximumSemanticMemoryCharacters)
                 throw new InvalidOperationException("Semantic conversation memory exceeded its limit.");
         }
@@ -145,19 +151,32 @@ namespace OpenEmpires
         {
             if (string.IsNullOrEmpty(json)) return new JObject();
             try { return JToken.Parse(json); }
             catch (JsonException) { return new JObject(); }
         }
 
         private static string UnitName(int type)
             => GameKnowledgeCatalog.BuildCanonicalIdentityCatalog().FindUnitByType(type)?.DisplayName;
     }
 
     public sealed partial class CommanderSemanticResult
     {
+        internal CommanderSemanticResult RequireNameConfirmation(string strictProposal, CommanderSemanticProviderRequest request)
+        {
+            if (!IsValid || (Outcome != CommanderSemanticOutcome.Request && Outcome != CommanderSemanticOutcome.DynamicPlan)) return this;
+            var hints = JArray.Parse(request.UnresolvedNameInterpretations);
+            if (hints.Count == 0) return this;
+            var draft = CommanderRequestGraphDraft.ConfirmNames(strictProposal, hints);
+            return NameConfirmation(draft);
+        }
+
+        internal static CommanderSemanticResult NameConfirmation(CommanderRequestGraphDraft draft)
+            => new CommanderSemanticResult(true, CommanderSemanticOutcome.Clarify,
+                Array.Empty<CommanderSemanticNode>(), draft.NameConfirmationQuestion, pendingGraphDraft: draft);
+
         internal static CommanderSemanticResult ProviderRejected(string safeExplanation)
         {
             return new CommanderSemanticResult(false, CommanderSemanticOutcome.Unsupported,
                 Array.Empty<CommanderSemanticNode>(), safeExplanation);
         }
     }
 }
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public sealed partial class CommanderEconomyClarificationTests
    {
        private const string LongbowRequest = "{\"outcome\":\"Request\",\"executionOrder\":\"Independent\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Longbowman\",\"count\":3,\"quantityMode\":\"New\"}]}";

        [TestCase("yes")]
        [TestCase("Longbowman")]
        public async Task NearName_RequestCannotExecuteUntilLocalNameConfirmation(string reply)
        {
            using (var f = new HintFixture(LongbowRequest))
            {
                await f.Chat.SubmitMessageAsync("Train three new longbowmna");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null, f.Chat.DisplayedTranscript);
                var ticket = f.Chat.PendingClarification.RequestTicket;
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Economy.Commands(), Is.Empty);
                await f.Chat.SubmitMessageAsync(reply);
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
                Assert.That(f.Chat.PendingClarification, Is.Null);
                var goal = f.Economy.Manager.Goals.Single();
                Assert.That(goal.RequestAuthority.RequestId, Is.EqualTo(ticket.Id));
                Assert.That(goal.RequestAuthority.OriginalInput, Is.EqualTo("Train three new longbowmna"));
            }
        }

        [Test]
        public async Task NearName_CompoundRetainsEveryFieldAndNeedsSeparateGameplayApproval()
        {
            var root = JObject.Parse(LongbowRequest);
            root["executionOrder"] = "Sequential";
            ((JArray)root["nodes"]).Add(JObject.Parse("{\"type\":\"WatchFutureUnits\",\"unit\":\"Villager\",\"count\":5,\"producer\":\"TownCenter\",\"producerOrdinal\":1,\"action\":\"Gather\",\"resource\":\"Wood\",\"sourceKind\":\"Tree\",\"dependsOn\":[0]}"));
            using (var f = new HintFixture(root.ToString(Formatting.None)))
            {
                f.Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter, f.Economy.Base.x, f.Economy.Base.y, false, true).AutoProduceVillagers = false;
                string original = "Train three new longbowmna, then send the next five Villagers from my first Town Center to Wood from trees";
                await f.Chat.SubmitMessageAsync(original);
                Assert.That(f.Chat.PendingClarification, Is.Not.Null, f.Chat.DisplayedTranscript);
                var ticket = f.Chat.PendingClarification.RequestTicket;
                Assert.That(f.Chat.PendingActionPlan, Is.Null);
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Economy.Commands(), Is.Empty);
                Assert.That(f.Chat.PendingActionPlan, Is.Not.Null, f.Chat.DisplayedTranscript);
                var nodes = f.Chat.PendingActionPlan.Interpretation.Nodes;
                Assert.That(nodes[0].Count, Is.EqualTo(3));
                Assert.That(nodes[0].QuantityMode, Is.EqualTo(CommanderProductionQuantityMode.New));
                Assert.That(nodes[1].DependsOn, Is.EqualTo(new[] { 0 }));
                Assert.That(nodes[1].Count, Is.EqualTo(5));
                Assert.That(nodes[1].ProducerOrdinal, Is.EqualTo(1));
                Assert.That(nodes[1].SourceKind, Is.EqualTo(ResourceSourceKind.Tree));
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(2), f.Chat.DisplayedTranscript);
                Assert.That(f.Economy.Manager.Goals.All(g => g.RequestAuthority.RequestId == ticket.Id), Is.True);
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task NearName_DynamicProposalCannotBypassNameThenGameplayConfirmation()
        {
            Assert.That(CommanderIntentCatalog.TryResolveUnit("Longbowman", out int unit), Is.True);
            string proposal = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":[{\"id\":\"train\",\"mechanic\":\"produce\",\"parameters\":{\"unit\":\"unit:" + unit + "\",\"count\":3,\"quantityMode\":\"New\"},\"inputs\":{},\"dependsOn\":[]}]}";
            using (var f = new HintFixture(proposal))
            {
                await f.Chat.SubmitMessageAsync("Train three new longbowmna");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null, f.Chat.DisplayedTranscript);
                Assert.That(f.Chat.PendingActionPlan, Is.Null);
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Chat.PendingActionPlan, Is.Not.Null, f.Chat.DisplayedTranscript);
                Assert.That(f.Chat.PendingActionPlan.Interpretation.Outcome, Is.EqualTo(CommanderSemanticOutcome.DynamicPlan));
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task NearName_NoDismissesProposalOnly()
        {
            using (var f = new HintFixture(LongbowRequest))
            {
                await f.Chat.SubmitMessageAsync("Train three new Longbowmen");
                int existing = f.Economy.Manager.Goals.Count;
                Assert.That(existing, Is.EqualTo(1), f.Chat.DisplayedTranscript);
                await f.Chat.SubmitMessageAsync("Train three new longbowmna");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null);
                await f.Chat.SubmitMessageAsync("no");
                Assert.That(f.Chat.PendingClarification, Is.Null);
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(existing));
                Assert.That(f.Transport.Calls, Is.EqualTo(2));
            }
        }

        [Test]
        public async Task NearName_UnrelatedProposalKeepsOriginalDraftBlockedLocally()
        {
            using (var f = new HintFixture(LongbowRequest.Replace("Longbowman", "Spearman")))
            {
                const string original = "Train three new longbowmna";
                await f.Chat.SubmitMessageAsync(original);
                Assert.That(f.Chat.PendingClarification?.OriginalText, Is.EqualTo(original));
                Assert.That(f.Chat.DisplayedTranscript, Does.Contain("Blocked"));
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Chat.PendingClarification?.OriginalText, Is.EqualTo(original));
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Economy.Commands(), Is.Empty);
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
            }
        }

        [TestCase("Longbowman")]
        [TestCase("Longbowmen")]
        public async Task NearName_ExactNamesAndEstablishedAliasesDoNotRequireConfirmation(string name)
        {
            using (var f = new HintFixture(LongbowRequest))
            {
                await f.Chat.SubmitMessageAsync("Train three new " + name);
                Assert.That(f.Chat.PendingClarification, Is.Null);
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(1), f.Chat.DisplayedTranscript);
            }
        }

        [Test]
        public async Task NearName_ReadOnlyAnswerDoesNotCreatePendingAction()
        {
            using (var f = new HintFixture("{\"outcome\":\"Answer\",\"message\":\"Longbowman is an English unit.\"}"))
            {
                await f.Chat.SubmitMessageAsync("What is longbowmna?");
                Assert.That(f.Chat.PendingClarification, Is.Null);
                Assert.That(f.Chat.PendingActionPlan, Is.Null);
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Chat.DisplayedTranscript, Does.Contain("English unit"));
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task NearName_PrunedModelHintsStillRequireGameOwnedConfirmation()
        {
            using (var f = new HintFixture(LongbowRequest))
            {
                CommanderSemanticProviderRequest selected = null;
                for (int length = 0; length < 990; length += 10)
                {
                    string original = "Train three new longbowmna " + new string('\u754c', length);
                    var request = new CommanderSemanticProviderRequest(original,
                        new CommanderContextBuilder().Build(f.Economy.Simulation, f.Economy.Manager, original));
                    if (!CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096, out var pack, out _)) continue;
                    string user = (string)JObject.Parse(pack.Json)["messages"][1]["content"];
                    if (!user.Contains("nameInterpretations")) { selected = request; break; }
                }
                Assert.That(selected, Is.Not.Null, "Fixture must actually exercise optional hint pruning, not preflight rejection.");
                var result = await f.Provider.TranslateSemanticAsync(selected, CancellationToken.None);
                Assert.That(result.Outcome, Is.EqualTo(CommanderSemanticOutcome.Clarify));
                Assert.That(result.PendingGraphDraft, Is.Not.Null);
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
                Assert.That(f.Transport.Body, Does.Not.Contain("nameInterpretations"));
                Assert.That(f.Provider.LastInputEstimate, Is.LessThan(4000));
            }
        }

        [Test]
        public async Task NearName_ConfirmedCrossCivilizationUnitStillFailsNormalAdmission()
        {
            using (var f = new HintFixture(LongbowRequest))
            {
                f.Economy.Simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.English });
                await f.Chat.SubmitMessageAsync("Train three new longbowmna");
                Assert.That(f.Chat.PendingClarification, Is.Not.Null);
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals, Is.Empty, f.Chat.DisplayedTranscript);
                Assert.That(f.Economy.Commands(), Is.Empty);
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task NearName_OriginalTicketRejectsForeignResetAndReplay()
        {
            var pendingField = typeof(CommanderChatUI).GetField("pendingClarification",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            using (var f = new HintFixture(LongbowRequest))
            using (var foreign = new HintFixture(LongbowRequest))
            {
                await f.Chat.SubmitMessageAsync("Train three new longbowmna");
                var pending = f.Chat.PendingClarification;
                pendingField.SetValue(foreign.Chat, pending);
                await foreign.Chat.SubmitMessageAsync("yes");
                Assert.That(foreign.Economy.Manager.Goals, Is.Empty);
                Assert.That(foreign.Transport.Calls, Is.Zero);
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(1));
                pendingField.SetValue(f.Chat, pending);
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(1));
                f.Chat.ResetConversation();
                pendingField.SetValue(f.Chat, pending);
                await f.Chat.SubmitMessageAsync("yes");
                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(1));
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task NearName_LocalConfirmationRetainsFullStrictPayloadIncludingIndependentResourceAmount()
        {
            var root = JObject.Parse(LongbowRequest);
            root["executionOrder"] = "Sequential";
            ((JObject)root["nodes"][0])["constraints"] = new JArray(JObject.Parse("{\"type\":\"MaximumQueue\",\"amount\":2}"));
            ((JArray)root["nodes"]).Add(JObject.Parse("{\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\",\"countMode\":\"Exact\",\"count\":3,\"workers\":{\"state\":\"Gathering\",\"currentResource\":\"Wood\"},\"destination\":{\"resource\":\"Food\",\"sourceKind\":\"Sheep\"},\"resourceAmount\":400,\"resourceAmountMode\":\"AdditionalGathered\",\"dependsOn\":[0]}"));
            using (var f = new HintFixture(root.ToString(Formatting.None)))
            {
                const string original = "Train three new longbowmna with maximum queue two, then move three Wood gatherers to gather 400 additional Food from Sheep";
                await f.Chat.SubmitMessageAsync(original);
                var pending = f.Chat.PendingClarification;
                Assert.That(pending, Is.Not.Null, f.Chat.DisplayedTranscript);
                Assert.That(JToken.DeepEquals(pending.GraphDraft.ToPendingJson(), root), Is.True,
                    "The confirmation adapter must retain the entire strict payload, not rebuild selected fields.");
                var resolved = pending.GraphDraft.Resolve("yes");
                Assert.That(resolved.IsValid, Is.True);
                Assert.That(((MaximumQueueConstraint)resolved.Nodes[0].Constraints.Single()).MaximumQueue, Is.EqualTo(2));
                var allocation = resolved.Nodes[1].WorkerAllocation;
                Assert.That(allocation.Count, Is.EqualTo(3));
                Assert.That(allocation.Mode, Is.EqualTo(CommanderWorkerAllocationMode.SelectedCount));
                Assert.That(allocation.CountMode, Is.EqualTo(CommanderWorkerCountMode.Exact));
                Assert.That(allocation.Workers.CurrentResource, Is.EqualTo(ResourceType.Wood));
                Assert.That(allocation.Destination.SourceKind, Is.EqualTo(ResourceSourceKind.Sheep));
                Assert.That(allocation.ResourceAmount, Is.EqualTo(400));
                Assert.That(allocation.ResourceAmountMode, Is.EqualTo(CommanderResourceAmountMode.AdditionalGathered));
                Assert.That(resolved.Nodes[1].DependsOn, Is.EqualTo(new[] { 0 }));
                Assert.That(pending.OriginalText, Is.EqualTo(original));
                Assert.That(f.Transport.Calls, Is.EqualTo(1));
                Assert.That(f.Economy.Manager.Goals, Is.Empty, "Detached local parsing carries no authority.");
            }
        }

        private sealed class HintTransport : ICommanderHttpTransport
        {
            internal int Calls;
            internal string Body;
            private readonly string response;
            internal HintTransport(string response) { this.response = response; }
            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string body,
                IReadOnlyDictionary<string, string> headers, CancellationToken token)
            {
                Calls++; Body = body;
                return Task.FromResult(new CommanderHttpResponse(200, new JObject { ["choices"] = new JArray(
                    new JObject { ["finish_reason"] = "stop", ["message"] = new JObject { ["content"] = response } }) }.ToString(Formatting.None)));
            }
        }

        private sealed class HintFixture : IDisposable
        {
            internal readonly EconomyFixture Economy = new EconomyFixture();
            internal readonly HintTransport Transport;
            internal readonly OpenRouterCommanderProvider Provider;
            internal readonly CommanderChatUI Chat;
            internal readonly CommanderIntentDispatcher Dispatcher;
            private readonly GameObject host;
            internal HintFixture(string response)
            {
                Economy.Simulation.SetPlayerCivilizations(new[] { Civilization.English, Civilization.English });
                Transport = new HintTransport(response);
                Provider = new OpenRouterCommanderProvider("fixture-not-a-credential", Transport);
                Dispatcher = new CommanderIntentDispatcher(Economy.Simulation, Economy.Manager);
                host = new GameObject("HintConfirmationFixture");
                Chat = host.AddComponent<CommanderChatUI>();
                Chat.Initialize(Provider, Economy.Simulation, Economy.Manager, Dispatcher);
            }
            public void Dispose()
            {
                typeof(CommanderChatUI).GetMethod("OnDestroy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Chat, null);
                UnityEngine.Object.DestroyImmediate(host); Dispatcher.Dispose(); Economy.Dispose();
            }
        }
    }
}
