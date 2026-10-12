# Frozen Task2 fix3 scoped diff

Baseline: fix2-final snapshots in task-2-fix3-baseline. No commits.
Production SHA256 A5935790BF05503C4F1FD49219135799FD0B91E5E329FB3FC42B112BD0F2E157.
Graph-test SHA256 71854326E2624ACE778FAD56222ECB1AA55A709148FC8EFF7DA1BA37F7AC03CA.
Review only old/new amount qualifier fidelity and new breakage in this change.
Parent inspected raw b97eb8b879224c308504a4596f158e57 summary10/10 and fix3 report.
Earlier negation guard/source and all other Phase5C modules are unchanged this round.

## Incremental production and test diffs
diff --git a/Docs/CommanderPhase5C/evidence/task-2-fix3-baseline/CommanderRequestGraphDraft.cs.snapshot b/Assets/Scripts/AI/Commander/Economy/CommanderRequestGraphDraft.cs
index 2720bc7..be1c5a3 100644
--- a/Docs/CommanderPhase5C/evidence/task-2-fix3-baseline/CommanderRequestGraphDraft.cs.snapshot
+++ b/Assets/Scripts/AI/Commander/Economy/CommanderRequestGraphDraft.cs
@@ -209,30 +209,31 @@ namespace OpenEmpires
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
             if (prior != null && !NamesCurrentValue(prior, change)) return null;
+            if (change.Path == "resourceAmount" && !AmountQualifierMatchesGraph(replacement, change.Node)) return null;
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
@@ -300,33 +301,47 @@ namespace OpenEmpires
                 return CommanderIntentCatalog.TryResolveStructure(prior, out BuildingType building)
                     && string.Equals((string)current, building.ToString(), StringComparison.OrdinalIgnoreCase);
             if (leaf == "unit")
                 return CommanderIntentCatalog.TryResolveUnit(prior, out int unit)
                     && string.Equals((string)current, CommanderIntentCatalog.GetUnitDisplayName(unit), StringComparison.OrdinalIgnoreCase);
             if (leaf == "resource")
                 return CommanderIntentCatalog.TryResolveResource(prior, out ResourceType resource)
                     && string.Equals((string)current, resource.ToString(), StringComparison.OrdinalIgnoreCase);
             if (leaf == "sourceKind")
                 return string.Equals((string)current, SourceAlias(prior), StringComparison.OrdinalIgnoreCase);
             string[] words = Words(prior);
             if (words.Length < 2 || !CommanderClarificationReplies.TryParseCount(words[0], out int number)
                 || (int)current != number) return false;
             if (leaf == "count") return words.Length == 2 && (words[1] == "villager" || words[1] == "villagers"
                 || words[1] == "worker" || words[1] == "workers");
-            return leaf == "resourceAmount" && IsTypedCorrectionValue(prior)
-                && words[1] != "villager" && words[1] != "villagers"
-                && words[1] != "worker" && words[1] != "workers";
+            return leaf == "resourceAmount" && AmountQualifierMatchesGraph(prior, change.Node);
+        }
+
+        private bool AmountQualifierMatchesGraph(string phrase, int nodeIndex)
+        {
+            string[] words = Words(phrase);
+            if (words.Length < 2 || words.Length > 3
+                || !CommanderClarificationReplies.TryParseCount(words[0], out int _)
+                || !CommanderIntentCatalog.TryResolveResource(words[words.Length - 1], out ResourceType namedResource)) return false;
+            var node = (JObject)((JArray)graph["nodes"])[nodeIndex];
+            if (!string.Equals((string)node["destination"]?["resource"], namedResource.ToString(),
+                StringComparison.OrdinalIgnoreCase)) return false;
+            if (words.Length == 2) return true; // An omitted mode cannot rewrite the retained mode.
+            string namedMode = words[1] == "additional" ? "AdditionalGathered"
+                : words[1] == "stockpile" ? "Stockpile" : null;
+            return namedMode != null && string.Equals((string)node["resourceAmountMode"], namedMode,
+                StringComparison.OrdinalIgnoreCase);
         }
 
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
 
diff --git a/Docs/CommanderPhase5C/evidence/task-2-fix3-baseline/CommanderEconomyClarificationTests.Graph.cs.snapshot b/Assets/Tests/EditMode/CommanderEconomyClarificationTests.Graph.cs
index 26c8a45..aa067d5 100644
--- a/Docs/CommanderPhase5C/evidence/task-2-fix3-baseline/CommanderEconomyClarificationTests.Graph.cs.snapshot
+++ b/Assets/Tests/EditMode/CommanderEconomyClarificationTests.Graph.cs
@@ -607,14 +607,159 @@ namespace OpenEmpires.Tests
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
+
+        [Test]
+        public async Task OldAmountWrongResource_ReasksAndRetainsAdditionalFood()
+        { await AssertMismatchedAmountCorrectionRetainsDraft("change 400 wood to 500 food"); }
+
+        [Test]
+        public async Task OldAmountWrongMode_ReasksAndRetainsAdditionalFood()
+        { await AssertMismatchedAmountCorrectionRetainsDraft("change 400 stockpile food to 500 food"); }
+
+        [Test]
+        public async Task NewAmountWrongResource_ReasksAndRetainsAdditionalFood()
+        { await AssertMismatchedAmountCorrectionRetainsDraft("change 400 additional food to 500 wood"); }
+
+        [Test]
+        public async Task NewAmountWrongMode_ReasksAndRetainsAdditionalFood()
+        { await AssertMismatchedAmountCorrectionRetainsDraft("change 400 additional food to 500 stockpile food"); }
+
+        private static async Task AssertMismatchedAmountCorrectionRetainsDraft(string correction)
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
+                var old = f.Chat.PendingClarification;
+                var retained = old.GraphDraft.ToPendingJson();
+                await f.Chat.SubmitMessageAsync(correction);
+                Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(old.RequestTicket), correction);
+                Assert.That(JToken.DeepEquals(f.Chat.PendingClarification.GraphDraft.ToPendingJson(), retained), Is.True, correction);
+                Assert.That(f.Provider.Calls, Is.EqualTo(1), correction);
+                Assert.That(f.Economy.Manager.Goals, Is.Empty, correction);
+                Assert.That(f.Economy.Commands(), Is.Empty, correction);
+                await f.Chat.SubmitMessageAsync("from berries");
+                var admitted = f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation;
+                Assert.That(admitted.Count, Is.EqualTo(3), correction);
+                Assert.That(admitted.Destination.Resource, Is.EqualTo(ResourceType.Food), correction);
+                Assert.That(admitted.ResourceAmount, Is.EqualTo(400), correction);
+                Assert.That(admitted.ResourceAmountMode, Is.EqualTo(CommanderResourceAmountMode.AdditionalGathered), correction);
+                Assert.That(f.Provider.Calls, Is.EqualTo(1), correction);
+            }
+        }
+
+        [Test]
+        public async Task QualifiedOldAmount_ChangesOnlyAmount()
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
+                var old = f.Chat.PendingClarification;
+                await f.Chat.SubmitMessageAsync("change 400 additional food to 500 additional food");
+                Assert.That(f.Chat.PendingClarification?.RequestTicket, Is.SameAs(old.RequestTicket));
+                var node = (JObject)((JArray)f.Chat.PendingClarification.GraphDraft.ToPendingJson()["nodes"])[0];
+                Assert.That((int)node["count"], Is.EqualTo(3));
+                Assert.That((string)node["destination"]["resource"], Is.EqualTo("Food"));
+                Assert.That((int)node["resourceAmount"], Is.EqualTo(500));
+                Assert.That((string)node["resourceAmountMode"], Is.EqualTo("AdditionalGathered"));
+                Assert.That(f.Provider.Calls, Is.EqualTo(1));
+                Assert.That(f.Economy.Manager.Goals, Is.Empty);
+                await f.Chat.SubmitMessageAsync("from berries");
+                var admitted = f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation;
+                Assert.That(admitted.Count, Is.EqualTo(3));
+                Assert.That(admitted.ResourceAmount, Is.EqualTo(500));
+                Assert.That(admitted.ResourceAmountMode, Is.EqualTo(CommanderResourceAmountMode.AdditionalGathered));
+            }
+        }
+
+        [Test]
+        public async Task NamedOldToNewStructure_AdmitsOnlyMill()
+        {
+            var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
+                ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
+            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(mine),
+                "Independent", 0, "structure", "Confirmation", new JValue("Mine")))))
+            {
+                f.Economy.Worker(0);
+                await f.Chat.SubmitMessageAsync("build a Mine");
+                await f.Chat.SubmitMessageAsync("change Mine to Mill");
+                Assert.That(f.Provider.Calls, Is.EqualTo(1));
+                Assert.That(f.Economy.Manager.Goals.OfType<BuildStructureGoal>().Single().StructureType,
+                    Is.EqualTo(BuildingType.Mill));
+            }
+        }
+
+        [Test]
+        public async Task NamedOldToNewSource_ChangesOnlySource()
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
+                await f.Chat.SubmitMessageAsync("change Sheep to Berries");
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
+        public async Task NamedOldToNewWorkerCount_PreservesAmount()
+        {
+            var allocation = new JObject { ["type"] = "AllocateWorkers", ["mode"] = "SelectedCount",
+                ["countMode"] = "Exact", ["count"] = 6,
+                ["workers"] = new JObject { ["state"] = "Idle" },
+                ["destination"] = new JObject { ["resource"] = "Food" },
+                ["resourceAmount"] = 400, ["resourceAmountMode"] = "AdditionalGathered" };
+            using (var f = new ChatFixture(new SequenceSemanticProvider(GraphDraft(new JArray(allocation),
+                "Independent", 0, "destination.sourceKind", "SourceKind", new JValue("Berries")))))
+            {
+                for (int i = 0; i < 6; i++) f.Economy.Worker(i);
+                f.Economy.Node(ResourceType.Food, 12);
+                await f.Chat.SubmitMessageAsync("send six idle villagers to gather 400 additional food");
+                await f.Chat.SubmitMessageAsync("change six villagers to three villagers");
+                Assert.That(f.Provider.Calls, Is.EqualTo(1));
+                var node = (JObject)((JArray)f.Chat.PendingClarification.GraphDraft.ToPendingJson()["nodes"])[0];
+                Assert.That((int)node["count"], Is.EqualTo(3));
+                Assert.That((int)node["resourceAmount"], Is.EqualTo(400));
+                await f.Chat.SubmitMessageAsync("from berries");
+                var admitted = f.Economy.Manager.Goals.OfType<AllocateWorkersGoal>().Single().Allocation;
+                Assert.That(admitted.Count, Is.EqualTo(3));
+                Assert.That(admitted.ResourceAmount, Is.EqualTo(400));
+            }
+        }
     }
 }
