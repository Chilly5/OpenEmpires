# Task 5 fix2 incremental review package
Exact pre-round2 snapshot bytes vs current working-tree. HEAD unchanged 4c9de96adbf6342fbe9e1490fa9b898a0f99caf6, no commits.
## Assets\Scripts\AI\Commander\Phase4A\CommanderChatUI.cs
Before SHA256: 5B534E058A195EBB6343A3C6455273CA7C101A54E506B770CD18E3BC804CD525
After SHA256: 86BEF68EC2A8FFDADBAF1CE12E37F0E5B3FBD51C54DB4D7038DF5F2A0D2A4BDB
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-5-fix2-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.cs"
index 880f12e..6b7f4f1 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-5-fix2-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.cs"	
@@ -510,22 +510,21 @@ namespace OpenEmpires
                         TaskScheduler.Default);
                     Task finished = await Task.WhenAny(translation,
                         Task.Delay(semanticProviderTimeout, request.Token));
                     if (finished != translation)
                     {
                         request.Cancel();
                         if (this != null && generation == runtimeGeneration
                             && semanticRequestRevision == requestRevision && !lifetime.IsCancellationRequested)
                         {
                             retainSubmittedDraft = true;
-                            OfferRequestRecovery(presentationTurn, message, "Timed out — no order was submitted.");
-                            AppendLine("Commander", "That request timed out. No order was submitted. Choose Retry, Edit, or Cancel.");
+                            ReportRequestTimeout(presentationTurn, message);
                         }
                         return null;
                     }
                     CommanderSemanticResult result = await translation;
                     semanticStage = "semantic-response";
                     if (this == null || generation != runtimeGeneration
                         || semanticRequestRevision != requestRevision
                         || !ReferenceEquals(semanticProvider, provider)
                         || !ReferenceEquals(semanticSimulation, simulation)
                         || !ReferenceEquals(semanticGoalManager, manager)
@@ -688,22 +687,21 @@ namespace OpenEmpires
                     if (IsLatestPresentationTurn(presentationTurn)) TrackCurrentRequest(submission);
                     AppendLine("Commander", display);
                     return chatSubmission;
                 }
                 catch (System.OperationCanceledException)
                 {
                     if (this != null && generation == runtimeGeneration
                         && semanticRequestRevision == requestRevision && !lifetime.IsCancellationRequested)
                     {
                         retainSubmittedDraft = true;
-                        OfferRequestRecovery(presentationTurn, message, "Timed out — no order was submitted.");
-                        AppendLine("Commander", "That request timed out. No order was submitted. Choose Retry, Edit, or Cancel.");
+                        ReportRequestTimeout(presentationTurn, message);
                     }
                     return null;
                 }
                 catch (System.Exception error)
                 {
                     Debug.Log("[Commander] failed-stage=" + semanticStage + ";exception=" + error.GetType().Name);
                     if (this != null && generation == runtimeGeneration
                         && semanticRequestRevision == requestRevision
                         && !request.IsCancellationRequested && !lifetime.IsCancellationRequested)
                     {
@@ -714,20 +712,29 @@ namespace OpenEmpires
                     }
                     return null;
                 }
                 finally
                 {
                     if (ReferenceEquals(activeSemanticRequest, request)) activeSemanticRequest = null;
                 }
             }
         }
 
+        private void ReportRequestTimeout(long presentationTurn, string message)
+        {
+            bool latest = IsLatestPresentationTurn(presentationTurn);
+            OfferRequestRecovery(presentationTurn, message, "Timed out — no order was submitted.");
+            AppendLine("Commander", latest
+                ? "That request timed out. No order was submitted. Choose Retry, Edit, or Cancel."
+                : "An earlier order timed out. No order was submitted. To try it again, enter that order in the composer and press Send.");
+        }
+
         private void RecordAcceptedSemanticNodes(IReadOnlyList<CommanderSemanticNode> nodes)
         {
             if (Conversation == null || nodes == null) return;
             for (int i = 0; i < nodes.Count; i++)
             {
                 CommanderSemanticNode node = nodes[i];
                 if (node == null) continue;
                 if (node.Type == CommanderSemanticNodeType.EnsureUnitCount
                     && node.UnitType.HasValue && node.Count.HasValue)
                     Conversation.SemanticMemory.RecordAcceptedUnitCount(node.UnitType.Value,
```
## Assets\Scripts\AI\Commander\Phase4A\CommanderChatUI.Presentation.cs
Before SHA256: 2D28477D4CAC1D8AC81C947E60429156533CE16EDB0A7B44B296A05B2CE4237A
After SHA256: 2D28477D4CAC1D8AC81C947E60429156533CE16EDB0A7B44B296A05B2CE4237A
No delta.
## Assets\Tests\PlayMode\CommanderPhase5CUIPlayModeTests.cs
Before SHA256: E8F5E01CF9332663B025ACF9F68862DF97A440DFC9897643943CCA4BB870CD61
After SHA256: 8EFBAD044A537814B62515FC03A82167F1E0CFD25DE03F85C36C72908BF5C2C2
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-5-fix2-baseline\\Assets\\Tests\\PlayMode\\CommanderPhase5CUIPlayModeTests.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\PlayMode\\CommanderPhase5CUIPlayModeTests.cs"
index 9c641a0..f51b350 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-5-fix2-baseline\\Assets\\Tests\\PlayMode\\CommanderPhase5CUIPlayModeTests.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\PlayMode\\CommanderPhase5CUIPlayModeTests.cs"	
@@ -598,20 +598,60 @@ namespace OpenEmpires.Tests
             Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
                 Does.StartWith("Completed"));
             provider.CompleteLatest("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3}]}");
             while (!olderOrder.IsCompleted) yield return null;
             yield return null;
             Assert.That(goals.Goals, Has.Count.EqualTo(1), "The older valid order keeps its own task.");
             Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
                 Does.StartWith("Completed"), "The older order cannot impersonate the newer answer.");
         }
 
+        [UnityTest]
+        public IEnumerator OlderOrderTimeout_AfterNewerQuestionGivesComposerRetryInsteadOfHiddenControls()
+        {
+            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
+            chat.enabled = true;
+            chat.OpenCommander();
+            var olderOrder = chat.SubmitMessageAsync("train three spearmen");
+            Assert.That(provider.Attempts, Is.EqualTo(1));
+            var question = chat.SubmitMessageAsync("strategy status");
+            while (!question.IsCompleted) yield return null;
+            Assert.That(olderOrder.IsCompleted, Is.False,
+                "This fixture requires the newer question to finish before the older order times out.");
+            while (!olderOrder.IsCompleted) yield return null;
+            yield return null;
+
+            Assert.That(goals.Goals, Is.Empty, "The timed-out order must not admit a goal.");
+            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
+                Does.StartWith("Completed"), "The newer information answer stays latest.");
+            var recovery = chat.transform.Find("CommanderCanvas/Panel/RequestRecovery");
+            Assert.That(recovery.gameObject.activeSelf, Is.False,
+                "Old-order recovery controls must not impersonate the newer question.");
+            Assert.That(chat.DisplayedTranscript, Does.Contain("timed out"),
+                "The old request must have actually reached its timeout branch. Transcript: " + chat.DisplayedTranscript);
+            var bubbles = chat.transform.Find("CommanderCanvas/Panel/Transcript/Viewport/BubbleContent");
+            string reply = TextOf(bubbles.GetChild(bubbles.childCount - 1).Find("Message"));
+            Assert.That(reply, Does.Contain("earlier order").IgnoreCase);
+            Assert.That(reply, Does.Contain("composer").And.Contain("Send"));
+            Assert.That(reply, Does.Not.Contain("Choose Retry, Edit, or Cancel"),
+                "Do not promise recovery buttons that are not available for the older order.");
+
+            chat.InputText = "train three spearmen";
+            var retry = chat.SubmitCurrentInputAsync();
+            yield return null;
+            Assert.That(provider.Attempts, Is.EqualTo(2),
+                "The instruction names a route the player can actually use.");
+            chat.transform.Find("CommanderCanvas/Panel/RequestRecovery/Cancel")
+                .GetComponent<Button>().onClick.Invoke();
+            while (!retry.IsCompleted) yield return null;
+        }
+
         [UnityTest]
         public IEnumerator CancelledRequest_LateProviderFaultCannotReplaceCancelledStatus()
         {
             chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
             chat.enabled = true;
             chat.OpenCommander();
             var request = chat.SubmitMessageAsync("train three spearmen");
             Assert.That(provider.Attempts, Is.EqualTo(1));
             provider.FailLatest(new InvalidOperationException("late fixture fault"));
             chat.transform.Find("CommanderCanvas/Panel/RequestRecovery/Cancel")
```
