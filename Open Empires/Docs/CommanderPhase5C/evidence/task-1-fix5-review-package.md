# Task1 final fix5 scoped review package

Baseline: fix4-final snapshots in task-1-fix5-baseline. No commits.
Only two production files + HintGate tests changed. Requirement: task-1-fix5-brief.md.
Prior finding: task-1-fix4-review.md. Report: task-1-fix-report.md FIX5.
Parent inspected full saved dc4e...12/12 results; no tests rerun for review.
Frozen hashes: GraphDraft836C3E16591B17969A100D6CB0E9B2DC93FEF2C6C09B472527BDC7F323BD8283;
SemanticProviderFEB4F7B372AB23749C14213DCE48C6B750BA9DD64D19602880031D521F47D791;
HintGateEFB126CF5D421FDB59A91AA87659B3F6326A900B51E28CCC4545E8D1FB82BFE7.
Review partial graph/legacy local continuation requirement lifetime and new breakage
in this diff only. No broad English/native/UI/actual-token/full-goal certification.

## Incremental production/test diffs
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-1-fix5-baseline\\Assets__Scripts__AI__Commander__Economy__CommanderRequestGraphDraft.cs.snapshot" b/Assets/Scripts/AI/Commander/Economy/CommanderRequestGraphDraft.cs
index ed310b3..c7e6cb0 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-1-fix5-baseline\\Assets__Scripts__AI__Commander__Economy__CommanderRequestGraphDraft.cs.snapshot"
+++ b/Assets/Scripts/AI/Commander/Economy/CommanderRequestGraphDraft.cs
@@ -26,32 +26,36 @@ namespace OpenEmpires
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
         private readonly List<KeyValuePair<string, string>> nameConfirmations;
+        private readonly string unresolvedNameRequirements;
         internal bool IsNameConfirmation => nameConfirmations != null;
         internal string NameConfirmationQuestion => nameConfirmations.Count == 0
             ? "Blocked: the proposed content does not uniquely match the unresolved similar name. Original request retained; no action was submitted."
             : "Did you mean " + nameConfirmations[0].Value + " for that similar name? Confirm the name with yes or its name, or no to dismiss. Gameplay approval is separate.";
         public IReadOnlyList<Slot> MissingSlots => slots.AsReadOnly();
-        public int NodeCount => ((JArray)graph["nodes"]).Count;
-        private CommanderRequestGraphDraft(JObject graph, List<Slot> slots)
-        { this.graph = (JObject)graph.DeepClone(); this.slots = slots; }
+        public int NodeCount => (graph["nodes"] as JArray)?.Count ?? 0;
+        private CommanderRequestGraphDraft(JObject graph, List<Slot> slots, string unresolvedNames = null)
+        { this.graph = (JObject)graph.DeepClone(); this.slots = slots; unresolvedNameRequirements = unresolvedNames; }
+
+        internal CommanderRequestGraphDraft WithNameRequirements(JArray hints)
+            => new CommanderRequestGraphDraft(graph, slots, hints.ToString(Formatting.None));
 
         private CommanderRequestGraphDraft(JObject proposal, List<KeyValuePair<string, string>> confirmations)
             : this(proposal, new List<Slot>()) { nameConfirmations = confirmations; }
 
         // Only called after strict provider parsing. Retain the entire proposal, including
         // the DynamicPlan envelope, and re-enter the same parser/admission path locally.
         internal static CommanderRequestGraphDraft ConfirmNames(string strictProposal, JArray hints)
         {
             var root = JObject.Parse(strictProposal);
             var catalog = GameKnowledgeCatalog.BuildCanonicalIdentityCatalog();
             var confirmations = new List<KeyValuePair<string, string>>();
             foreach (JObject hint in hints)
@@ -115,34 +119,62 @@ namespace OpenEmpires
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
+            => CommanderSemanticJson.ParseProviderResponse(CompletedProposal(changed, value).ToString(Formatting.None));
+
+        private JObject CompletedProposal(Slot changed, JToken value)
         {
             var completed = (JObject)graph.DeepClone();
             foreach (var slot in slots)
             {
                 var parent = Parent((JObject)((JArray)completed["nodes"])[slot.Node], slot.Path);
                 if (slot.Type != "Confirmation") parent[Leaf(slot.Path)] = (slot == changed ? value : slot.Alternatives[0]).DeepClone();
             }
-            return CommanderSemanticJson.ParseProviderResponse(new JObject { ["outcome"] = "Request",
+            return new JObject { ["outcome"] = "Request",
                 ["executionOrder"] = completed["executionOrder"].DeepClone(),
-                ["nodes"] = completed["nodes"].DeepClone() }.ToString(Formatting.None));
+                ["nodes"] = completed["nodes"].DeepClone() };
+        }
+
+        // Game-owned metadata is never serialized into provider fields. Each local
+        // transition retains it; only an explicit typed name choice resolves a hint.
+        private CommanderSemanticResult ParseLocal(JObject proposal, string selectedPath, JToken selectedValue)
+        {
+            string json = proposal.ToString(Formatting.None);
+            var parsed = CommanderSemanticJson.ParseProviderResponse(json);
+            if (!parsed.IsValid || string.IsNullOrEmpty(unresolvedNameRequirements)) return parsed;
+            var hints = JArray.Parse(unresolvedNameRequirements);
+            string selectedId = null;
+            string candidates = null;
+            var catalog = GameKnowledgeCatalog.BuildCanonicalIdentityCatalog();
+            if (selectedValue?.Type == JTokenType.String)
+            {
+                if (selectedPath == "unit" && CommanderIntentCatalog.TryResolveUnit((string)selectedValue, out int unit))
+                { selectedId = catalog.FindUnitByType(unit)?.StableId; candidates = "unitCandidates"; }
+                else if ((selectedPath == "structure" || selectedPath == "producer")
+                    && CommanderIntentCatalog.TryResolveStructure((string)selectedValue, out BuildingType building))
+                { selectedId = catalog.Buildings.FirstOrDefault(b => b.BuildingType == building)?.StableId; candidates = "buildingCandidates"; }
+            }
+            if (selectedId != null)
+                foreach (var hint in hints.OfType<JObject>().Where(h => ((JArray)h[candidates]).Values<string>().Contains(selectedId)).ToList())
+                    hint.Remove();
+            return parsed.RequireNameConfirmation(json, hints);
         }
 
         internal CommanderSemanticResult Resolve(string reply)
         {
             if (IsNameConfirmation)
             {
                 if (nameConfirmations.Count == 0) return null;
                 var proposed = nameConfirmations[0];
                 string whole = (reply ?? string.Empty).Trim();
                 if (!string.Equals(whole, "yes", StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(whole, "y", StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(whole, proposed.Value, StringComparison.OrdinalIgnoreCase)
@@ -155,38 +187,38 @@ namespace OpenEmpires
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
-                var parsed = Probe(slot, chosen);
+                var parsed = ParseLocal(CompletedProposal(slot, chosen), slot.Path, chosen);
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
-            return CommanderSemanticJson.Parse(new JObject { ["outcome"] = "Clarify",
+            return ParseLocal(new JObject { ["outcome"] = "Clarify",
                 ["message"] = "Please specify the remaining detail.", ["pending"] = next,
-                ["missingSlots"] = remaining }.ToString(Formatting.None));
+                ["missingSlots"] = remaining }, slot.Path, chosen);
         }
 
         internal static bool IsCorrectionUtterance(string reply)
         {
             foreach (var word in Words(reply))
                 if (word == "instead" || word == "actually" || word == "change" || word == "rather") return true;
             return false;
         }
 
         private sealed class Change
         {
             internal int Node;
@@ -275,30 +307,30 @@ namespace OpenEmpires
             if (prior != null && !NamesCurrentValue(prior, change)) return null;
             if (change.Path == "resourceAmount" && !AmountQualifierMatchesGraph(replacement, change.Node)) return null;
             var next = (JObject)graph.DeepClone();
             Parent((JObject)((JArray)next["nodes"])[change.Node], change.Path)[Leaf(change.Path)] = change.Value.DeepClone();
             var remaining = new JArray();
             foreach (var slot in slots)
             {
                 if (slot.Node == change.Node && slot.Path == change.Path) continue;
                 remaining.Add(new JObject { ["node"] = slot.Node, ["path"] = slot.Path,
                     ["type"] = slot.Type, ["alternatives"] = new JArray(slot.Alternatives) });
             }
             if (remaining.Count == 0)
-                return CommanderSemanticJson.ParseProviderResponse(new JObject { ["outcome"] = "Request",
+                return ParseLocal(new JObject { ["outcome"] = "Request",
                     ["executionOrder"] = next["executionOrder"].DeepClone(),
-                    ["nodes"] = next["nodes"].DeepClone() }.ToString(Formatting.None));
-            return CommanderSemanticJson.Parse(new JObject { ["outcome"] = "Clarify",
+                    ["nodes"] = next["nodes"].DeepClone() }, change.Path, change.Value);
+            return ParseLocal(new JObject { ["outcome"] = "Clarify",
                 ["message"] = "Please specify the remaining detail.", ["pending"] = next,
-                ["missingSlots"] = remaining }.ToString(Formatting.None));
+                ["missingSlots"] = remaining }, change.Path, change.Value);
         }
 
         // Correction-like wording keeps a pending draft local, but only these complete,
         // affirmative shapes are allowed to change one typed leaf. Unknown language fails closed.
         private static bool TryAffirmativeCorrection(string reply, out string replacement, out string prior)
         {
             replacement = null; prior = null;
             string whole = Regex.Replace((reply ?? string.Empty).Trim().ToLowerInvariant(), @"\s+", " ");
             if (!Regex.IsMatch(whole, @"^[a-z0-9 ]+$")) return false;
             for (int i = 0; i < 2; i++)
             {
                 if (whole.StartsWith("please ", StringComparison.Ordinal)) whole = whole.Substring(7);
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-1-fix5-baseline\\Assets__Scripts__AI__Commander__Phase4E__CommanderSemanticProvider.cs.snapshot" b/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticProvider.cs
index b12f6dc..53db232 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-1-fix5-baseline\\Assets__Scripts__AI__Commander__Phase4E__CommanderSemanticProvider.cs.snapshot"
+++ b/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticProvider.cs
@@ -153,27 +153,40 @@ namespace OpenEmpires
             try { return JToken.Parse(json); }
             catch (JsonException) { return new JObject(); }
         }
 
         private static string UnitName(int type)
             => GameKnowledgeCatalog.BuildCanonicalIdentityCatalog().FindUnitByType(type)?.DisplayName;
     }
 
     public sealed partial class CommanderSemanticResult
     {
         internal CommanderSemanticResult RequireNameConfirmation(string strictProposal, CommanderSemanticProviderRequest request)
         {
-            if (!IsValid || (Outcome != CommanderSemanticOutcome.Request && Outcome != CommanderSemanticOutcome.DynamicPlan)) return this;
+            if (!IsValid) return this;
             var hints = JArray.Parse(request.UnresolvedNameInterpretations);
+            return RequireNameConfirmation(strictProposal, hints);
+        }
+
+        internal CommanderSemanticResult RequireNameConfirmation(string strictProposal, JArray hints)
+        {
+            if (!IsValid) return this;
             if (hints.Count == 0) return this;
+            if (Outcome == CommanderSemanticOutcome.Clarify && PendingGraphDraft != null)
+                return new CommanderSemanticResult(true, Outcome, Nodes, SafeExplanation,
+                    pendingGraphDraft: PendingGraphDraft.WithNameRequirements(hints));
+            // The legacy worker draft cannot represent a unit/building hint. Preserve
+            // its entire strict proposal in the existing non-approvable local blocker.
+            if (Outcome != CommanderSemanticOutcome.Request && Outcome != CommanderSemanticOutcome.DynamicPlan
+                && !(Outcome == CommanderSemanticOutcome.Clarify && PendingDraft != null)) return this;
             var draft = CommanderRequestGraphDraft.ConfirmNames(strictProposal, hints);
             return NameConfirmation(draft);
         }
 
         internal static CommanderSemanticResult NameConfirmation(CommanderRequestGraphDraft draft)
             => new CommanderSemanticResult(true, CommanderSemanticOutcome.Clarify,
                 Array.Empty<CommanderSemanticNode>(), draft.NameConfirmationQuestion, pendingGraphDraft: draft);
 
         internal static CommanderSemanticResult ProviderRejected(string safeExplanation)
         {
             return new CommanderSemanticResult(false, CommanderSemanticOutcome.Unsupported,
                 Array.Empty<CommanderSemanticNode>(), safeExplanation);
diff --git "a/Docs\\CommanderPhase5C\\evidence\\task-1-fix5-baseline\\Assets__Tests__EditMode__CommanderEconomyClarificationTests.HintGate.cs.snapshot" b/Assets/Tests/EditMode/CommanderEconomyClarificationTests.HintGate.cs
index ee23568..239c3f1 100644
--- "a/Docs\\CommanderPhase5C\\evidence\\task-1-fix5-baseline\\Assets__Tests__EditMode__CommanderEconomyClarificationTests.HintGate.cs.snapshot"
+++ b/Assets/Tests/EditMode/CommanderEconomyClarificationTests.HintGate.cs
@@ -5,24 +5,153 @@ using System.Threading;
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
 
+        [TestCase("longbowmna", true)]
+        [TestCase("Longbowman", false)]
+        public async Task NearName_PartialCountRetainsOriginalNameRequirement(string name, bool needsName)
+        {
+            var node = (JObject)JObject.Parse(LongbowRequest)["nodes"][0];
+            node.Remove("count");
+            string proposal = GraphDraft(new JArray(node), "Independent", 0, "count", "Count", new JValue(3));
+            using (var f = new HintFixture(proposal))
+            {
+                string original = "Train new " + name;
+                await f.Chat.SubmitMessageAsync(original);
+                var pending = f.Chat.PendingClarification;
+                Assert.That(pending, Is.Not.Null, f.Chat.DisplayedTranscript);
+                Assert.That(JToken.DeepEquals(pending.GraphDraft.ToPendingJson(), JObject.Parse(proposal)["pending"]), Is.True);
+                await f.Chat.SubmitMessageAsync("3");
+                if (needsName)
+                {
+                    Assert.That(f.Economy.Manager.Goals, Is.Empty, "Count must not confirm the name.");
+                    Assert.That(f.Economy.Commands(), Is.Empty);
+                    Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(pending.RequestTicket));
+                    Assert.That(f.Chat.PendingClarification.OriginalText, Is.EqualTo(original));
+                    Assert.That(f.Chat.PendingClarification.RuntimeGeneration, Is.EqualTo(pending.RuntimeGeneration));
+                    Assert.That(JToken.DeepEquals(f.Chat.PendingClarification.GraphDraft.ToPendingJson(), JObject.Parse(LongbowRequest)), Is.True);
+                    await f.Chat.SubmitMessageAsync("yes");
+                }
+                Assert.That(f.Chat.PendingClarification, Is.Null, f.Chat.DisplayedTranscript);
+                var goal = f.Economy.Manager.Goals.Single();
+                Assert.That(goal.RequestAuthority.RequestId, Is.EqualTo(pending.RequestTicket.Id));
+                Assert.That(goal.RequestAuthority.OriginalInput, Is.EqualTo(original));
+                Assert.That(f.Transport.Calls, Is.EqualTo(1));
+            }
+        }
+
+        [TestCase(false)]
+        [TestCase(true)]
+        public async Task NearName_PartialCompoundCountAndSourceStillNeedNameAndGameplayApproval(bool correction)
+        {
+            var complete = JObject.Parse(LongbowRequest);
+            complete["executionOrder"] = "Sequential";
+            ((JObject)complete["nodes"][0])["constraints"] = new JArray(JObject.Parse("{\"type\":\"MaximumQueue\",\"amount\":2}"));
+            ((JArray)complete["nodes"]).Add(JObject.Parse("{\"type\":\"WatchFutureUnits\",\"unit\":\"Villager\",\"count\":5,\"producer\":\"TownCenter\",\"producerOrdinal\":1,\"action\":\"Gather\",\"resource\":\"Wood\",\"sourceKind\":\"Tree\",\"dependsOn\":[0]}"));
+            var partial = (JArray)complete["nodes"].DeepClone();
+            ((JObject)partial[0]).Remove("count");
+            if (!correction) ((JObject)partial[1]).Remove("sourceKind");
+            else partial[1]["sourceKind"] = "Any";
+            var proposal = JObject.Parse(GraphDraft(partial, "Sequential", 0, "count", "Count", new JValue(3)));
+            if (!correction) ((JArray)proposal["missingSlots"]).Add(JObject.Parse("{\"node\":1,\"path\":\"sourceKind\",\"type\":\"SourceKind\",\"alternatives\":[\"Tree\"]}"));
+            using (var f = new HintFixture(proposal.ToString(Formatting.None)))
+            {
+                f.Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter, f.Economy.Base.x, f.Economy.Base.y, false, true).AutoProduceVillagers = false;
+                const string original = "Train new longbowmna with maximum queue two, then send next five Villagers from my first Town Center to Wood";
+                await f.Chat.SubmitMessageAsync(original);
+                var pending = f.Chat.PendingClarification;
+                Assert.That(pending, Is.Not.Null, f.Chat.DisplayedTranscript);
+                await f.Chat.SubmitMessageAsync(correction ? "change Any to Tree" : "from trees");
+                Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(pending.RequestTicket));
+                await f.Chat.SubmitMessageAsync("3");
+                Assert.That(f.Chat.PendingActionPlan, Is.Null, "Slot completion is not name confirmation.");
+                Assert.That(f.Economy.Manager.Goals, Is.Empty);
+                Assert.That(f.Economy.Commands(), Is.Empty);
+                Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(pending.RequestTicket));
+                Assert.That(JToken.DeepEquals(f.Chat.PendingClarification.GraphDraft.ToPendingJson(), complete), Is.True);
+                await f.Chat.SubmitMessageAsync("Longbowman");
+                Assert.That(f.Chat.PendingClarification, Is.Null);
+                Assert.That(f.Economy.Manager.Goals, Is.Empty);
+                Assert.That(f.Chat.PendingActionPlan, Is.Not.Null, f.Chat.DisplayedTranscript);
+                var nodes = f.Chat.PendingActionPlan.Interpretation.Nodes;
+                Assert.That(nodes[0].Count, Is.EqualTo(3));
+                Assert.That(((MaximumQueueConstraint)nodes[0].Constraints.Single()).MaximumQueue, Is.EqualTo(2));
+                Assert.That(nodes[1].Count, Is.EqualTo(5));
+                Assert.That(nodes[1].ProducerOrdinal, Is.EqualTo(1));
+                Assert.That(nodes[1].SourceKind, Is.EqualTo(ResourceSourceKind.Tree));
+                Assert.That(nodes[1].DependsOn, Is.EqualTo(new[] { 0 }));
+                await f.Chat.SubmitMessageAsync("yes");
+                Assert.That(f.Economy.Manager.Goals.Count, Is.EqualTo(2), f.Chat.DisplayedTranscript);
+                Assert.That(f.Economy.Manager.Goals.All(g => g.RequestAuthority.RequestId == pending.RequestTicket.Id), Is.True);
+                Assert.That(f.Transport.Calls, Is.EqualTo(1));
+            }
+        }
+
+        [TestCase("CanonicalName", "Longbowman")]
+        [TestCase("Confirmation", "yes")]
+        public async Task NearName_ExplicitNameSlotIsNotAskedAgainAfterCount(string type, string reply)
+        {
+            var node = (JObject)JObject.Parse(LongbowRequest)["nodes"][0];
+            node.Remove("count");
+            if (type == "CanonicalName") node.Remove("unit");
+            var proposal = JObject.Parse(GraphDraft(new JArray(node), "Independent", 0, "unit", type, new JValue("Longbowman")));
+            ((JArray)proposal["missingSlots"]).Add(JObject.Parse("{\"node\":0,\"path\":\"count\",\"type\":\"Count\",\"alternatives\":[3]}"));
+            using (var f = new HintFixture(proposal.ToString(Formatting.None)))
+            {
+                await f.Chat.SubmitMessageAsync("Train new longbowmna");
+                var ticket = f.Chat.PendingClarification.RequestTicket;
+                await f.Chat.SubmitMessageAsync(reply);
+                Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(ticket));
+                Assert.That(f.Economy.Manager.Goals, Is.Empty);
+                await f.Chat.SubmitMessageAsync("3");
+                Assert.That(f.Chat.PendingClarification, Is.Null, f.Chat.DisplayedTranscript);
+                Assert.That(f.Economy.Manager.Goals.Single().RequestAuthority.RequestId, Is.EqualTo(ticket.Id));
+                Assert.That(f.Transport.Calls, Is.EqualTo(1));
+            }
+        }
+
+        [Test]
+        public async Task NearName_LegacyWorkerDraftCannotDropUnrepresentableName()
+        {
+            const string proposal = "{\"outcome\":\"Clarify\",\"message\":\"How many?\",\"pending\":{\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\",\"countMode\":\"Exact\",\"workers\":{\"state\":\"Idle\"},\"destination\":{\"resource\":\"Wood\",\"sourceKind\":\"Tree\"}},\"missingFields\":[\"Count\"]}";
+            using (var f = new HintFixture(proposal))
+            {
+                for (int i = 0; i < 3; i++) f.Economy.Worker(i);
+                f.Economy.Node(ResourceType.Wood, 12);
+                const string original = "Send idle villagers to Wood then train longbowmna";
+                await f.Chat.SubmitMessageAsync(original);
+                var ticket = f.Chat.PendingClarification.RequestTicket;
+                await f.Chat.SubmitMessageAsync("3");
+                Assert.That(f.Economy.Manager.Goals, Is.Empty, "Legacy count draft cannot omit the hinted action and execute.");
+                Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(ticket));
+                Assert.That(f.Chat.PendingClarification.OriginalText, Is.EqualTo(original));
+                Assert.That(f.Chat.DisplayedTranscript, Does.Contain("Blocked"));
+                Assert.That(JToken.DeepEquals(f.Chat.PendingClarification.GraphDraft.ToPendingJson(), JObject.Parse(proposal)), Is.True);
+                await f.Chat.SubmitMessageAsync("yes");
+                Assert.That(f.Economy.Manager.Goals, Is.Empty);
+                Assert.That(f.Economy.Commands(), Is.Empty);
+                Assert.That(f.Transport.Calls, Is.EqualTo(1));
+                await f.Chat.SubmitMessageAsync("no");
+                Assert.That(f.Chat.PendingClarification, Is.Null);
+            }
+        }
+
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
