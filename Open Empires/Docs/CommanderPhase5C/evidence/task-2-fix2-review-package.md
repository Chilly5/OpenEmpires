# Frozen Task2 fix2 review package

Scope: correction-polarity fix only. No commits. Baseline is recovery snapshot
after tests were added, before production guard implementation. New tests were
already present at recovery, so included explicitly below rather than pretending
their addition belongs to this incremental diff.

Current production SHA256 C1B615525226E3303E60DE854D25E5EF03320ACCB62724D5A77723AD7FD47EAF.
Current graph-test SHA256 99681AFCA2252CD019DB479891513090E7DCB6BBC56DCFAD6F4DFC5067D6FB98.
Initial test import timed out; log independently confirmed import and later direct
reflection confirmed loaded methods. Production import succeeded. RED3 observed
failures/4discovered, count outcome unknown. GREEN11/11 + controls3/3 saved.
No live/native/full-regression certification. Report details in task-2-report.md.

## Production incremental diff
diff --git a/Docs/CommanderPhase5C/evidence/task-2-fix2-recovery-baseline/CommanderRequestGraphDraft.cs.snapshot b/Assets/Scripts/AI/Commander/Economy/CommanderRequestGraphDraft.cs
index 4600647..2720bc7 100644
--- a/Docs/CommanderPhase5C/evidence/task-2-fix2-recovery-baseline/CommanderRequestGraphDraft.cs.snapshot
+++ b/Assets/Scripts/AI/Commander/Economy/CommanderRequestGraphDraft.cs
@@ -1,17 +1,18 @@
 using System;
 using System.Collections.Generic;
+using System.Text.RegularExpressions;
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
             public int Node { get; }
             public string Path { get; }
             public string Type { get; }
             public IReadOnlyList<JToken> Alternatives
@@ -131,31 +132,32 @@ namespace OpenEmpires
         {
             foreach (var word in Words(reply))
                 if (word == "instead" || word == "actually" || word == "change" || word == "rather") return true;
             return false;
         }
 
         private sealed class Change
         {
             internal int Node;
             internal string Path;
             internal JToken Value;
         }
 
         private CommanderSemanticResult Correct(string reply)
         {
-            string[] words = Words(reply);
+            if (!TryAffirmativeCorrection(reply, out string replacement, out string prior)) return null;
+            string[] words = Words(replacement);
             if (words.Length == 0 || words.Length > 12) return null;
             var changes = new List<Change>();
             var nodes = (JArray)graph["nodes"];
             for (int start = 0; start < words.Length; start++)
                 for (int length = 1; length <= 3 && start + length <= words.Length; length++)
                 {
                     string phrase = string.Join(" ", words, start, length);
                     if (CommanderIntentCatalog.TryResolveStructure(phrase, out BuildingType building))
                     {
                         for (int i = 0; i < nodes.Count; i++)
                         {
                             AddChange(changes, i, (JObject)nodes[i], "structure", new JValue(building.ToString()));
                             AddChange(changes, i, (JObject)nodes[i], "producer", new JValue(building.ToString()));
                         }
                     }
@@ -206,48 +208,127 @@ namespace OpenEmpires
                     if (node["count"]?.Type == JTokenType.Integer && !amount) fields++;
                     if (node["resourceAmount"]?.Type == JTokenType.Integer && !workers) fields++;
                 }
                 if (fields != 1) return null;
                 for (int i = 0; i < nodes.Count; i++)
                 {
                     var node = (JObject)nodes[i];
                     if (node["count"]?.Type == JTokenType.Integer && !amount)
                         AddChange(changes, i, node, "count", new JValue(number.Value));
                     if (node["resourceAmount"]?.Type == JTokenType.Integer && !workers)
                         AddChange(changes, i, node, "resourceAmount", new JValue(number.Value));
                 }
             }
             if (changes.Count != 1) return null;
             Change change = changes[0];
+            if (prior != null && !NamesCurrentValue(prior, change)) return null;
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
                 return CommanderSemanticJson.ParseProviderResponse(new JObject { ["outcome"] = "Request",
                     ["executionOrder"] = next["executionOrder"].DeepClone(),
                     ["nodes"] = next["nodes"].DeepClone() }.ToString(Formatting.None));
             return CommanderSemanticJson.Parse(new JObject { ["outcome"] = "Clarify",
                 ["message"] = "Please specify the remaining detail.", ["pending"] = next,
                 ["missingSlots"] = remaining }.ToString(Formatting.None));
         }
 
+        // Correction-like wording keeps a pending draft local, but only these complete,
+        // affirmative shapes are allowed to change one typed leaf. Unknown language fails closed.
+        private static bool TryAffirmativeCorrection(string reply, out string replacement, out string prior)
+        {
+            replacement = null; prior = null;
+            string whole = Regex.Replace((reply ?? string.Empty).Trim().ToLowerInvariant(), @"\s+", " ");
+            if (!Regex.IsMatch(whole, @"^[a-z0-9 ]+$")) return false;
+            for (int i = 0; i < 2; i++)
+            {
+                if (whole.StartsWith("please ", StringComparison.Ordinal)) whole = whole.Substring(7);
+                else if (whole.StartsWith("actually ", StringComparison.Ordinal)) whole = whole.Substring(9);
+                else break;
+            }
+            if (whole.StartsWith("change ", StringComparison.Ordinal))
+            {
+                string rest = whole.Substring(7);
+                int separator = rest.IndexOf(" to ", StringComparison.Ordinal);
+                if (separator <= 0 || rest.IndexOf(" to ", separator + 4, StringComparison.Ordinal) >= 0) return false;
+                prior = rest.Substring(0, separator);
+                replacement = rest.Substring(separator + 4);
+                if (prior.StartsWith("the ", StringComparison.Ordinal)) prior = prior.Substring(4);
+                if (prior.StartsWith("a ", StringComparison.Ordinal)) prior = prior.Substring(2);
+                if (!IsTypedCorrectionValue(prior) || !IsTypedCorrectionValue(replacement)) return false;
+                return true;
+            }
+            if (!whole.EndsWith(" instead", StringComparison.Ordinal)) return false;
+            replacement = whole.Substring(0, whole.Length - 8);
+            if (replacement.StartsWith("build a ", StringComparison.Ordinal)) replacement = replacement.Substring(8);
+            else if (replacement.StartsWith("make a ", StringComparison.Ordinal)) replacement = replacement.Substring(7);
+            else if (replacement.StartsWith("a ", StringComparison.Ordinal)) replacement = replacement.Substring(2);
+            else if (replacement.StartsWith("an ", StringComparison.Ordinal)) replacement = replacement.Substring(3);
+            return IsTypedCorrectionValue(replacement);
+        }
+
+        private static bool IsTypedCorrectionValue(string phrase)
+        {
+            if (string.IsNullOrWhiteSpace(phrase) || Words(phrase).Length > 3) return false;
+            if (CommanderIntentCatalog.TryResolveStructure(phrase, out BuildingType _) ||
+                CommanderIntentCatalog.TryResolveUnit(phrase, out int _) ||
+                CommanderIntentCatalog.TryResolveResource(phrase, out ResourceType _)) return true;
+            if (Enum.TryParse(SourceAlias(phrase), true, out ResourceSourceKind source)
+                && Enum.IsDefined(typeof(ResourceSourceKind), source)) return true;
+            string[] words = Words(phrase);
+            if (words.Length < 2 || !CommanderClarificationReplies.TryParseCount(words[0], out int _)) return false;
+            if (words.Length == 2 && (words[1] == "villager" || words[1] == "villagers"
+                || words[1] == "worker" || words[1] == "workers")) return true;
+            int resourceIndex = words[1] == "additional" || words[1] == "stockpile" ? 2 : 1;
+            return words.Length == resourceIndex + 1 &&
+                (words[resourceIndex] == "food" || words[resourceIndex] == "wood"
+                    || words[resourceIndex] == "gold" || words[resourceIndex] == "stone");
+        }
+
+        private bool NamesCurrentValue(string prior, Change change)
+        {
+            JToken current = Parent((JObject)((JArray)graph["nodes"])[change.Node], change.Path)?[Leaf(change.Path)];
+            if (current == null) return false;
+            string leaf = Leaf(change.Path);
+            if (leaf == "structure" || leaf == "producer")
+                return CommanderIntentCatalog.TryResolveStructure(prior, out BuildingType building)
+                    && string.Equals((string)current, building.ToString(), StringComparison.OrdinalIgnoreCase);
+            if (leaf == "unit")
+                return CommanderIntentCatalog.TryResolveUnit(prior, out int unit)
+                    && string.Equals((string)current, CommanderIntentCatalog.GetUnitDisplayName(unit), StringComparison.OrdinalIgnoreCase);
+            if (leaf == "resource")
+                return CommanderIntentCatalog.TryResolveResource(prior, out ResourceType resource)
+                    && string.Equals((string)current, resource.ToString(), StringComparison.OrdinalIgnoreCase);
+            if (leaf == "sourceKind")
+                return string.Equals((string)current, SourceAlias(prior), StringComparison.OrdinalIgnoreCase);
+            string[] words = Words(prior);
+            if (words.Length < 2 || !CommanderClarificationReplies.TryParseCount(words[0], out int number)
+                || (int)current != number) return false;
+            if (leaf == "count") return words.Length == 2 && (words[1] == "villager" || words[1] == "villagers"
+                || words[1] == "worker" || words[1] == "workers");
+            return leaf == "resourceAmount" && IsTypedCorrectionValue(prior)
+                && words[1] != "villager" && words[1] != "villagers"
+                && words[1] != "worker" && words[1] != "workers";
+        }
+
         private static void AddChange(List<Change> changes, int index, JObject node, string path, JToken replacement)
         {
             JObject parent = Parent(node, path);
             JToken current = parent?[Leaf(path)];
             if (current == null || current.Type != replacement.Type || JToken.DeepEquals(current, replacement)) return;
             foreach (var existing in changes)
                 if (existing.Node == index && existing.Path == path && JToken.DeepEquals(existing.Value, replacement)) return;
             changes.Add(new Change { Node = index, Path = path, Value = replacement });
         }
 
         private static bool Contains(string[] words, string word)
         { foreach (string entry in words) if (entry == word) return true; return false; }
 
         private static string[] Words(string text)
         { return (text ?? string.Empty).Trim().ToLowerInvariant().Split(

        [Test]
        public async Task NegatedStructureCorrection_RetainsMineTicketUntilYes()
        {
            foreach (string correction in new[] { "Don't change the Mine to a Mill", "do not change the Mine to a Mill", "never change the Mine to a Mill" })
            {
                var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
                    ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
                using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(mine),
                    "Independent", 0, "structure", "Confirmation", new JValue("Mine")))))
                {
                    f.Economy.Worker(0);
                    await f.Chat.SubmitMessageAsync("build a Mine");
                    var old = f.Chat.PendingClarification;
                    var original = old.GraphDraft.ToPendingJson();
                    await f.Chat.SubmitMessageAsync(correction);
                    Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(old.RequestTicket), correction);
                    Assert.That(JToken.DeepEquals(f.Chat.PendingClarification.GraphDraft.ToPendingJson(), original), Is.True, correction);
                    Assert.That(f.Provider.Calls, Is.EqualTo(1), correction);
                    Assert.That(f.Economy.Manager.Goals, Is.Empty, correction);
                    Assert.That(f.Economy.Commands(), Is.Empty, correction);
                    await f.Chat.SubmitMessageAsync("yes");
                    Assert.That(f.Economy.Manager.Goals.OfType<BuildStructureGoal>().Single().StructureType,
                        Is.EqualTo(BuildingType.Mine), correction);
                }
            }
        }

        [Test]
        public async Task NegatedCountCorrection_PreservesCountAndIndependentAmount()
        {
            var allocation = new JObject { ["type"] = "AllocateWorkers", ["mode"] = "SelectedCount",
                ["countMode"] = "Exact", ["count"] = 6,
                ["workers"] = new JObject { ["state"] = "Gathering", ["currentResource"] = "Wood" },
                ["destination"] = new JObject { ["resource"] = "Food" },
                ["resourceAmount"] = 400, ["resourceAmountMode"] = "AdditionalGathered" };
            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(allocation),
                "Independent", 0, "destination.sourceKind", "SourceKind", new JValue("Berries")))))
            {
                for (int i = 0; i < 6; i++) f.Economy.Assigned(f.Economy.Worker(i), f.Economy.Node(ResourceType.Wood, 15 + i));
                f.Economy.Node(ResourceType.Food, 30);
                await f.Chat.SubmitMessageAsync("send six wood villagers to gather 400 additional food");
                var old = f.Chat.PendingClarification;
                var original = old.GraphDraft.ToPendingJson();
                await f.Chat.SubmitMessageAsync("don't change six villagers to three");
                Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(old.RequestTicket));
                Assert.That(JToken.DeepEquals(f.Chat.PendingClarification.GraphDraft.ToPendingJson(), original), Is.True);
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Economy.Commands(), Is.Empty);
                await f.Chat.SubmitMessageAsync("from berries");
                var retained = f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation;
                Assert.That(retained.Count, Is.EqualTo(6));
                Assert.That(retained.ResourceAmount, Is.EqualTo(400));
                Assert.That(retained.ResourceAmountMode, Is.EqualTo(CommanderResourceAmountMode.AdditionalGathered));
                Assert.That(retained.Workers.CurrentResource, Is.EqualTo(ResourceType.Wood));
            }
        }

        [Test]
        public async Task NegatedSourceCorrection_PreservesSheepUntilCountReply()
        {
            var allocation = new JObject { ["type"] = "AllocateWorkers", ["mode"] = "SelectedCount",
                ["countMode"] = "Exact", ["workers"] = new JObject { ["state"] = "Idle" },
                ["destination"] = new JObject { ["resource"] = "Food", ["sourceKind"] = "Sheep" } };
            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(allocation),
                "Independent", 0, "count", "Count", new JValue(3)))))
            {
                for (int i = 0; i < 3; i++) f.Economy.Worker(i);
                f.Economy.Node(ResourceType.Food, 12);
                await f.Chat.SubmitMessageAsync("send idle villagers from sheep to food");
                var old = f.Chat.PendingClarification;
                var original = old.GraphDraft.ToPendingJson();
                await f.Chat.SubmitMessageAsync("do not change Sheep to Berries");
                Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(old.RequestTicket));
                Assert.That(JToken.DeepEquals(f.Chat.PendingClarification.GraphDraft.ToPendingJson(), original), Is.True);
                Assert.That(f.Provider.Calls, Is.EqualTo(1));
                Assert.That(f.Economy.Manager.Goals, Is.Empty);
                Assert.That(f.Economy.Commands(), Is.Empty);
                await f.Chat.SubmitMessageAsync("three");
                Assert.That(f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation.Destination.SourceKind,
                    Is.EqualTo(ResourceSourceKind.Sheep));
            }
        }

        [Test]
        public async Task ConditionalOrExampleCorrection_ReasksWithoutSupersedingMine()
        {
            foreach (string correction in new[] { "if we change Mine to Mill", "for example change Mine to Mill",
                "build a Mill instead if we have wood", "could we change Mine to Mill?" })
            {
                var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
                    ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
                using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(mine),
                    "Independent", 0, "structure", "Confirmation", new JValue("Mine")))))
                {
                    f.Economy.Worker(0);
                    await f.Chat.SubmitMessageAsync("build a Mine");
                    var old = f.Chat.PendingClarification;
                    var original = old.GraphDraft.ToPendingJson();
                    await f.Chat.SubmitMessageAsync(correction);
                    Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(old.RequestTicket), correction);
                    Assert.That(JToken.DeepEquals(f.Chat.PendingClarification.GraphDraft.ToPendingJson(), original), Is.True, correction);
                    Assert.That(f.Provider.Calls, Is.EqualTo(1), correction);
                    Assert.That(f.Economy.Manager.Goals, Is.Empty, correction);
                    Assert.That(f.Economy.Commands(), Is.Empty, correction);
                    await f.Chat.SubmitMessageAsync("yes");
                    Assert.That(f.Economy.Manager.Goals.OfType<BuildStructureGoal>().Single().StructureType,
                        Is.EqualTo(BuildingType.Mine), correction);
                }
            }
        }
    }
}
