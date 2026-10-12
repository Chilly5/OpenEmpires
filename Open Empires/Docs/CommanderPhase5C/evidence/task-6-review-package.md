# Task 6 incremental source review package
Base: exact pre-Task6 snapshot bytes (Clarification explicitly reconstructed and verified against recorded Task2 hash). HEAD unchanged4c9de96adbf6342fbe9e1490fa9b898a0f99caf6. No commits. Scope excludes prior Task1–5 deltas.
## Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs
Before SHA256:86BEF68EC2A8FFDADBAF1CE12E37F0E5B3FBD51C54DB4D7038DF5F2A0D2A4BDB
After SHA256:FFBE9B01FB297F87347E7DA793C5D69562C76C42B9D03DC5DFD2DEDA211B0CAF
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-baseline\\Assets__Scripts__AI__Commander__Phase4A__CommanderChatUI.cs.snapshot" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.cs"
index 6b7f4f1..2019d5d 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-baseline\\Assets__Scripts__AI__Commander__Phase4A__CommanderChatUI.cs.snapshot"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.cs"	
@@ -99,24 +99,28 @@ namespace OpenEmpires
 
         private IEnumerator WaitForCommanderRuntime()
         {
             while (GameBootstrapper.Instance == null
                 || GameBootstrapper.Instance.Simulation == null
                 || GameBootstrapper.Instance.Commander == null
                 || GameBootstrapper.Instance.CommanderDispatcher == null)
                 yield return null;
 
             GameBootstrapper bootstrap = GameBootstrapper.Instance;
-            Initialize(CommanderAIProviderFactory.CreateDefault(), bootstrap.Simulation,
+            bool offlineEvidenceFixture = ShouldStartCliOfflineFixture();
+            Initialize(offlineEvidenceFixture ? (ICommanderAIProvider)new OfflineEvidenceProvider()
+                : CommanderAIProviderFactory.CreateDefault(), bootstrap.Simulation,
                 bootstrap.Commander, bootstrap.CommanderDispatcher);
             if (bootstrap.CommanderStrategicPipeline != null)
                 InitializeStrategic(StrategicAIInterpreterFactory.Create(), bootstrap.CommanderStrategicPipeline);
+            if (offlineEvidenceFixture) StartCoroutine(CaptureOfflineFixtureOnce());
+            else if (ShouldStartCliEvidenceCapture()) StartCoroutine(CaptureStatusEvidenceOnce());
         }
 
         public void Initialize(ICommanderAIProvider provider, GameSimulation simulation,
             CommanderGoalManager goalManager, CommanderIntentDispatcher dispatcher,
             System.TimeSpan? providerTimeout = null)
         {
             // Awake is not invoked for a MonoBehaviour added by an EditMode test.
             // Keeping initialization idempotent also makes programmatic local hosts safe.
             EnsureLocalSurface();
             ResetVoiceControls(); // Invalidate old voice work before replacing ANY runtime affinity.
@@ -277,21 +281,21 @@ namespace OpenEmpires
             if (semanticSetupMode == SemanticSetupMode.Disabled)
             { AppendLine("Commander", SemanticSetupStatus, false); return null; }
             if (adapter == null)
             {
                 LatestSubmission = null;
                 AppendLine("Commander", "Commander runtime is not ready yet.");
                 return null;
             }
 
             string wholeForm = NormalizeWholeForm(trimmed);
-            if (wholeForm == "clear memory")
+            if (wholeForm == "clear memory" || wholeForm == "reset commander")
             {
                 ResetConversation();
                 return null;
             }
             if (wholeForm == "show memory")
             {
                 AppendLine("Player", trimmed, false);
                 AppendLine("Commander", Conversation.Memory.ToJson(), false);
                 return null;
             }
@@ -467,20 +471,29 @@ namespace OpenEmpires
                 try
                 {
                     bool IsCurrentScope() => this != null && runtimeGeneration == generation
                         && semanticRequestRevision == requestRevision
                         && !lifetime.IsCancellationRequested
                         && ReferenceEquals(semanticGoalManager, manager)
                         && ReferenceEquals(semanticSimulation, simulation)
                         && ReferenceEquals(semanticDispatcher, dispatcher)
                         && ReferenceEquals(semanticProvider, provider);
                     var requestTicket = capturedPending?.RequestTicket ?? manager.BeginSemanticRequest(message, generation, IsCurrentScope);
+                    if (capturedPending != null && (!ReferenceEquals(pendingClarification, capturedPending)
+                        || request.IsCancellationRequested || requestTicket == null
+                        || capturedPending.OriginalText != requestTicket.OriginalInput
+                        || !requestTicket.BindLocalContinuation(manager, generation, IsCurrentScope)))
+                    {
+                        pendingClarification = null;
+                        AppendLine("Commander", "The retained request is no longer current; please submit a new request.");
+                        return null;
+                    }
                     semanticStage = "context-projection";
                     CommanderContext context = new CommanderContextBuilder().Build(simulation, manager,
                         message, capturedPending?.GraphDraft?.IsNameConfirmation == true ? null : capturedPending?.Serialize());
                     if (context.PlayerId != owner)
                     {
                         AppendLine("Commander", "Commander ownership changed; ask again.");
                         return null;
                     }
                     IReadOnlyList<CommanderSemanticMemoryEntry> semanticMemoryAtTurnStart =
                         Conversation.SemanticMemory.Snapshot();
@@ -885,20 +898,21 @@ namespace OpenEmpires
                 semanticGoalManager.CancelFutureSubscriptions();
             ClearActionPlanPreview();
             pendingClarification = null;
             runtimeGeneration++;
             currentRequestGoalId = 0;
             ClearStrategicPreview();
             lifetime?.Cancel();
             lifetime?.Dispose();
             lifetime = new CancellationTokenSource();
             Conversation.Reset();
+            SetLatestRequestState("Ready", "Commander is ready.");
             adapter?.ResetHistory();
             transcript.Clear();
             transcriptEntries.Clear();
             if (transcriptText != null) transcriptText.text = string.Empty;
             RefreshChatBubbles();
             strategicBridge?.Reset();
             ResetAdvisories();
             LatestStrategicInterpretation = null;
             LatestStrategicDecision = null;
             LatestSubmission = null;
```
## Assets/Tests/EditMode/CommanderEconomyClarificationTests.Chat.cs
Before SHA256:7FCBA69A488B69D324AC5B6E2A0CE3D849E3A8C6579761115ADC15D8E6F7E406
After SHA256:7FCBA69A488B69D324AC5B6E2A0CE3D849E3A8C6579761115ADC15D8E6F7E406
No delta.
## Assets/Tests/EditMode/CommanderEconomyClarificationTests.Graph.cs
Before SHA256:71854326E2624ACE778FAD56222ECB1AA55A709148FC8EFF7DA1BA37F7AC03CA
After SHA256:71854326E2624ACE778FAD56222ECB1AA55A709148FC8EFF7DA1BA37F7AC03CA
No delta.
## Assets/Tests/PlayMode/CommanderPhase5CUIPlayModeTests.cs
Before SHA256:8EFBAD044A537814B62515FC03A82167F1E0CFD25DE03F85C36C72908BF5C2C2
After SHA256:896DA4229E66A75810AD835FD434CC906D97CB5D56576F35D8EB4FDFE695770F
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-baseline\\Assets__Tests__PlayMode__CommanderPhase5CUIPlayModeTests.cs.snapshot" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\PlayMode\\CommanderPhase5CUIPlayModeTests.cs"
index f51b350..673ca52 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-baseline\\Assets__Tests__PlayMode__CommanderPhase5CUIPlayModeTests.cs.snapshot"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\PlayMode\\CommanderPhase5CUIPlayModeTests.cs"	
@@ -1,13 +1,14 @@
 using System;
 using System.Collections;
 using System.IO;
+using System.Linq;
 using System.Threading;
 using System.Threading.Tasks;
 using NUnit.Framework;
 using UnityEngine;
 using UnityEngine.TestTools;
 using UnityEngine.UI;
 
 namespace OpenEmpires.Tests
 {
     [Category("CommanderPhase5COffline")]
@@ -719,20 +720,97 @@ namespace OpenEmpires.Tests
             panel.GetWorldCorners(panelCorners);
             viewport.GetWorldCorners(viewportCorners);
             composer.GetWorldCorners(composerCorners);
             Assert.That(panelCorners[0].x, Is.GreaterThanOrEqualTo(0));
             Assert.That(panelCorners[2].x, Is.LessThanOrEqualTo(Screen.width + 1));
             Assert.That(panelCorners[0].y, Is.GreaterThanOrEqualTo(0));
             Assert.That(viewportCorners[0].y, Is.GreaterThanOrEqualTo(composerCorners[1].y + 4),
                 "Chat scroll must end above the fixed composer.");
         }
 
+        [UnityTest]
+        public IEnumerator ExplicitRuntimeCapture_UsesActualPopulatedCommanderCanvas()
+        {
+            InstallRenderFixtureCamera();
+            chat.enabled = true;
+            goals.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
+            chat.OpenCommander();
+            var turn = chat.SubmitMessageAsync("strategy status");
+            while (!turn.IsCompleted) yield return null;
+            yield return null;
+            var capture = chat.GetType().GetMethod("RequestEvidenceScreenshot");
+            var lastPath = chat.GetType().GetProperty("LastEvidenceScreenshotPath");
+            Assert.That(capture, Is.Not.Null);
+            Assert.That(lastPath, Is.Not.Null);
+            Assert.That(chat.transform.Find("CommanderCanvas/CompactCommander/SettingsMenu/CaptureEvidence"), Is.Not.Null);
+            Assert.That(capture.Invoke(chat, null), Is.EqualTo(true));
+            yield return new WaitForEndOfFrame();
+            yield return null;
+            string path = (string)lastPath.GetValue(chat);
+            Assert.That(path, Is.Not.Null.And.Not.Empty);
+            Assert.That(chat.LastEvidenceScreenshotKind, Is.EqualTo("manual"));
+            Assert.That(chat.LastEvidenceScreenshotWidth, Is.GreaterThan(0));
+            Assert.That(chat.LastEvidenceScreenshotHeight, Is.GreaterThan(0));
+            Assert.That(File.Exists(path), Is.True);
+            var signature = File.ReadAllBytes(path);
+            Assert.That(signature.Length, Is.GreaterThan(8));
+            Assert.That(signature.Take(8), Is.EqualTo(new byte[] { 137,80,78,71,13,10,26,10 }));
+            Assert.That(provider.Attempts, Is.Zero, "A screenshot must never dispatch a semantic request.");
+            File.Delete(path); // only this test's generated screenshot
+        }
+
+        [UnityTest]
+        public IEnumerator ExplicitOfflineFixture_CapturesPendingApprovalTasksAndChat()
+        {
+            InstallRenderFixtureCamera();
+            chat.enabled = true;
+            foreach (var node in simulation.MapData.GetAllResourceNodes().ToArray())
+            { simulation.MapData.ClearResourceTile(node.Id); simulation.MapData.RemoveResourceNode(node.Id); }
+            for (int tx = 0; tx < simulation.MapData.Width; tx++)
+                for (int tz = 0; tz < simulation.MapData.Height; tz++)
+                { simulation.MapData.Tiles[tx, tz] = TileType.Grass;
+                  simulation.MapData.ForestDensity[tx, tz] = 0;
+                  simulation.FogOfWar.SetVisible(0, tx, tz); }
+            simulation.MapData.ComputeHoleMap();
+            simulation.FogOfWar.SetVisionCheat(0, true);
+            var basePosition = simulation.MapData.BasePositions[0];
+            int x = basePosition.x, z = basePosition.y;
+            simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true).AutoProduceVillagers = false;
+            var worker = simulation.UnitRegistry.CreateUnit(0, simulation.MapData.TileToWorldFixed(x + 6, z + 6),
+                Fixed32.One, Fixed32.One, Fixed32.One);
+            worker.IsVillager = true; worker.UnitType = 0; worker.CurrentHealth = worker.MaxHealth = 100;
+            var stock = simulation.ResourceManager.GetPlayerResources(0);
+            stock.Food = stock.Wood = stock.Gold = stock.Stone = 2000;
+            var nested = typeof(CommanderChatUI).GetNestedType("OfflineEvidenceProvider",
+                System.Reflection.BindingFlags.NonPublic);
+            Assert.That(nested, Is.Not.Null);
+            var offline = (ICommanderAIProvider)Activator.CreateInstance(nested, true);
+            chat.Initialize(offline, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
+            var method = chat.GetType().GetMethod("CaptureOfflineFixtureOnce",
+                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
+            Assert.That(method, Is.Not.Null);
+            yield return (IEnumerator)method.Invoke(chat, null);
+            Assert.That(goals.Goals, Is.Not.Empty, chat.DisplayedTranscript);
+            Assert.That(chat.PendingActionPlan, Is.Null);
+            var captures = chat.GetType().GetProperty("OfflineEvidenceCapturePaths");
+            Assert.That(captures, Is.Not.Null);
+            var paths = (System.Collections.Generic.IReadOnlyList<string>)captures.GetValue(chat);
+            Assert.That(paths.Count, Is.EqualTo(3));
+            foreach (string path in paths)
+            {
+                Assert.That(File.Exists(path), Is.True, path);
+                byte[] png = File.ReadAllBytes(path);
+                Assert.That(png.Take(8), Is.EqualTo(new byte[] { 137,80,78,71,13,10,26,10 }));
+                File.Delete(path); // only this test's generated image
+            }
+        }
+
         [UnityTest]
         public IEnumerator CaptureActualPopulatedEditorViews_AtRequestedDesktopSizes()
         {
             InstallRenderFixtureCamera();
             chat.enabled = true;
             goals.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
             chat.OpenCommander();
             var turn = chat.SubmitMessageAsync("strategy status");
             while (!turn.IsCompleted) yield return null;
             var directory = NewRenderDirectory("populated-chat-tasks");
```
## Assets/Tests/Shared/ScenarioEvidenceRecorder.cs
Before SHA256:AEF952668724B0E186CE3905057992C844EC59E7DDF559278DAB1F66305FD2E3
After SHA256:69F5E6B4418D53AC618C8E2FE7BEE60FBC3EE60699C2188C0A8899D0F254C6FC
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-baseline\\Assets__Tests__Shared__ScenarioEvidenceRecorder.cs.snapshot" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\Shared\\ScenarioEvidenceRecorder.cs"
index 5fddf93..3065cd6 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-baseline\\Assets__Tests__Shared__ScenarioEvidenceRecorder.cs.snapshot"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\Shared\\ScenarioEvidenceRecorder.cs"	
@@ -1,21 +1,40 @@
 using System;
 using System.IO;
 using System.Linq;
 using System.Text;
+using System.Text.RegularExpressions;
 using Newtonsoft.Json;
 using Newtonsoft.Json.Linq;
 
 namespace OpenEmpires.TestSupport
 {
     public sealed class ScenarioEvidenceRecorder
     {
+        private static readonly string[] Phase5CTurnFields = {
+            "schemaVersion", "caseId", "requestId", "utterance", "turnText", "turnIndex", "status", "evidenceMode", "uiState",
+            "fixtureKind", "semanticValid", "semanticOutcome", "fidelity", "normalizedFields", "admittedFields", "turnHistory",
+            "draftBefore", "draftAfter", "draftChanges", "approvalRequired", "approved", "approvalMode",
+            "providerCalls", "httpAttempts", "packAccepted", "commandCount", "commandTypes", "nativeResults",
+            "taskStates", "estimatedInputTokens", "actualPromptTokens", "actualCompletionTokens",
+            "actualCachedTokens", "actualReasoningTokens", "reportedCost", "attemptCosts",
+            "uiSubmitMs", "packingMs", "transportMs", "parseMs", "admissionMs",
+            "firstNativeActionMs", "fogNewlyExploredCount", "garrisonHousedCount", "garrisonProtectedCount",
+            "garrisonUnaccommodatedCount", "futureObservedCount", "futureAssignedCount", "humanTrainCommands",
+            "resourceProgress", "gatheredFoodIncome", "foodCompletionTick", "foodIncomeAtCompletion",
+            "commanderCommandsAtFoodCompletion", "ordinaryFoodGatherActive",
+            "fixtureAssumption", "previewMentionsOwnedBase", "newResultCount", "oldUnitExcluded",
+            "humanBirthExcluded", "exactPatrolBinding",
+            "builderAffinityVerified", "assignedBuilderHumanProtected",
+            "ownedTaskFactCount", "readOnlyProviderRequest", "questionFactClass",
+            "errorStage", "errorCategory", "gap", "tickMode", "ticks"
+        };
         private readonly string path,run;
         private string lastStage="setup",firstFailure,firstCategory;
         private JObject diagnostics=new JObject{["initial_http_attempts"]="unavailable",["repair_http_attempts"]="unavailable",["http_status"]="unavailable",["finish_category"]="unavailable",["attribution"]="unavailable"};
         public void SetDiagnostics(int initial,int repair,int status,string finish,int[] goals,string[] commands,int[] buildings,int[] units)
         {
             if(initial<0||initial>6||repair<0||repair>6||initial+repair>6||status<0||status>599
                 ||!new[]{"stop","length","other","unavailable"}.Contains(finish)
                 ||goals==null||commands==null||buildings==null||units==null||goals.Any(i=>i<0)||buildings.Any(i=>i<0)||units.Any(i=>i<0)
                 ||commands.Any(c=>c==null||!System.Text.RegularExpressions.Regex.IsMatch(c,@"^[A-Za-z]{1,60}Command$")))throw new InvalidOperationException("Invalid bounded diagnostics.");
             diagnostics=new JObject{["initial_http_attempts"]=initial,["repair_http_attempts"]=repair,["http_status"]=status==0?(JToken)"unavailable":status,
@@ -38,20 +57,67 @@ namespace OpenEmpires.TestSupport
             if(stage!="terminal")lastStage=stage;
             var root=new JObject{["schema"]="openempires-live-scenario-evidence@1",["run_id"]=run,["last_stage"]=stage,
                 ["first_failing_stage"]=firstFailure,["first_failure_category"]=firstCategory,["error_category"]=category,
                 ["semantic_valid"]=result?.IsValid??false,["outcome"]=result?.Outcome.ToString()??"unavailable",
                 ["request_id"]=requestId>=0?requestId.ToString(System.Globalization.CultureInfo.InvariantCulture):"unavailable",
                 ["approved"]=approved,["goal_count"]=goals,["command_count"]=commands,["native_entity_count"]=nativeCount,
                 ["native_entity_basis"]=nativeCount<0?"unavailable":"game-owned goal attribution",["native_tick"]=tick,
                 ["normalized_nodes"]=Normalize(result),["diagnostics"]=diagnostics.DeepClone()};
             // Never serialize provider prose, input, raw response, headers, errors,
             // context/world objects, symbolic provider node names or credentials.
+            WriteAtomic(root);
+        }
+        // A distinct bounded per-turn projection. The caller supplies only detached, allow-listed
+        // observations; the recorder never serializes provider envelopes or simulation objects.
+        public void RecordPhase5CTurn(JObject safeTurn)
+        {
+            if(safeTurn==null || safeTurn.Count>Phase5CTurnFields.Length)
+                throw new InvalidOperationException("Invalid Phase 5C turn record.");
+            foreach(var field in safeTurn.Properties())
+                if(!Phase5CTurnFields.Contains(field.Name) || !SafePhase5CValue(field.Value,0))
+                    throw new InvalidOperationException("Unsafe Phase 5C turn field.");
+            if((int?)safeTurn["schemaVersion"]!=1 || !SafeSlug((string)safeTurn["caseId"])
+                || !SafeRequestId((string)safeTurn["requestId"])
+                || safeTurn["utterance"]?.Type!=JTokenType.String
+                || ((string)safeTurn["utterance"]).Length>1500
+                || !new[]{"started","observed","failed","open-gap"}.Contains((string)safeTurn["status"]))
+                throw new InvalidOperationException("Invalid Phase 5C turn identity or state.");
+            string stage=(string)safeTurn["errorStage"];
+            if(!string.IsNullOrEmpty(stage) && firstFailure==null)
+            {firstFailure=stage;firstCategory=(string)safeTurn["errorCategory"] ?? "unclassified";}
+            var root=new JObject { ["schema"]="openempires-phase5c-scenario-turn@1",["run_id"]=run,
+                ["first_failing_stage"]=firstFailure,["first_failure_category"]=firstCategory };
+            foreach(var field in safeTurn.Properties())root[field.Name]=field.Value.DeepClone();
+            WriteAtomic(root);
+        }
+        private static bool SafeSlug(string value)=>value!=null&&Regex.IsMatch(value,@"^[a-z0-9-]{1,80}$");
+        private static bool SafeRequestId(string value)=>value!=null&&Regex.IsMatch(value,@"^[A-Za-z0-9._:-]{1,128}$");
+        private static bool SafePhase5CValue(JToken value,int depth)
+        {
+            if(value==null||depth>7)return false;
+            if(value.Type==JTokenType.Null||value.Type==JTokenType.Boolean||value.Type==JTokenType.Integer||value.Type==JTokenType.Float)return true;
+            if(value.Type==JTokenType.String)
+            {string text=(string)value;return text==null||text.Length<=1500&&!text.Any(ch=>char.IsControl(ch)&&ch!='\n'&&ch!='\r'&&ch!='\t');}
+            if(value is JArray array)return array.Count<=32&&array.All(item=>SafePhase5CValue(item,depth+1));
+            if(value is JObject objectValue)
+            {
+                if(objectValue.Count>32)return false;
+                foreach(var field in objectValue.Properties())
+                    if(!Regex.IsMatch(field.Name,@"^[A-Za-z][A-Za-z0-9_]{0,63}$")
+                        ||Regex.IsMatch(field.Name,@"(?i)(entityid|unitid|buildingid|coordinate|position|tile|header|raw|secret|password|apikey|hiddenenemy)")
+                        ||!SafePhase5CValue(field.Value,depth+1))return false;
+                return true;
+            }
+            return false;
+        }
+        private void WriteAtomic(JObject root)
+        {
             byte[] bytes=new UTF8Encoding(false,true).GetBytes(root.ToString(Formatting.Indented));
             if(bytes.Length>16384)throw new InvalidOperationException("Evidence artifact exceeds its bound.");
             CheckPaths();Directory.CreateDirectory(Path.GetDirectoryName(path));string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
             try
             {
                 using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                 {file.Write(bytes,0,bytes.Length);file.Flush(true);}
                 if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
             }
             finally{if(File.Exists(temporary))File.Delete(temporary);}
```
## Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Clarification.cs
Before SHA256:D6582A384846F43ABADA7409CCC5EB8974CA93C6F9F8D00105A787A070A6602C
After SHA256:28F940BADAC21A33BF20B6C99DE4E9716C498B2EBDC9C5B2871F352E7A55430A
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-baseline\\CommanderChatUI.Clarification.cs.reconstructed.snapshot" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.Clarification.cs"
index b5fd017..44e1543 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-baseline\\CommanderChatUI.Clarification.cs.reconstructed.snapshot"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.Clarification.cs"	
@@ -1,36 +1,42 @@
 using System;
 using System.Linq;
+using System.Text.RegularExpressions;
 using Newtonsoft.Json;
 using Newtonsoft.Json.Linq;
 
 namespace OpenEmpires
 {
     public sealed partial class CommanderChatUI
     {
         private CommanderPendingClarification pendingClarification;
         private long clarificationSequence;
         private bool destroyCleanupComplete;
         public CommanderPendingClarification PendingClarification => pendingClarification;
 
         private bool HandlePendingControl(string message)
         {
             if (pendingClarification == null) return false;
             if (pendingClarification.RuntimeGeneration != runtimeGeneration) { pendingClarification = null; return false; }
-            string whole = message.Trim().ToLowerInvariant();
+            string whole = NormalizeWholeForm(message);
             if (whole == "cancel" || whole == "never mind" || whole == "nevermind" || whole == "forget it")
             {
-                pendingClarification = null; AppendLine("Player", message); AppendLine("Commander", "Pending request cancelled; no goal was created."); return true;
+                pendingClarification = null; AppendLine("Player", message);
+                SetLatestRequestState("Cancelled", "Pending request cancelled; no goal was created.");
+                AppendLine("Commander", "Pending request cancelled; no goal was created."); return true;
             }
-            if (pendingClarification.GraphDraft != null && whole == "no")
+            bool negativeCancellation = Regex.IsMatch(whole,
+                @"^no,\s*cancel(?:\s+(?:that|this)\s+(?:proposal|plan))?$");
+            if (pendingClarification.GraphDraft != null && (whole == "no" || negativeCancellation))
             {
                 pendingClarification = null; AppendLine("Player", message);
+                SetLatestRequestState("Cancelled", "Proposal dismissed; existing tasks are unchanged.");
                 AppendLine("Commander", "Proposal dismissed; existing tasks are unchanged."); return true;
             }
             // This only chooses whether a new turn supersedes a pending question.
             // It neither parses nor executes any gameplay action.
             string[] standalone = { "build ", "train ", "make ", "produce ", "research ", "attack ", "scout ", "patrol ", "retreat ", "repair ", "defend ", "advance ", "put ", "gather ", "send ", "reach ", "assign ", "have ", "keep ", "move ", "garrison ", "ungarrison ", "explore ", "find " };
             if (!(pendingClarification.GraphDraft != null && CommanderRequestGraphDraft.IsCorrectionUtterance(message))
                 && standalone.Any(prefix => whole.StartsWith(prefix, StringComparison.Ordinal)))
             { pendingClarification = null; return false; }
             var previous = pendingClarification;
             if (previous.GraphDraft != null && previous.Turns >= 6)
@@ -77,21 +83,22 @@ namespace OpenEmpires
 
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
```
## Assets\Scripts\AI\Commander\Phase4A\CommanderChatUI.Presentation.cs
Before SHA256:2D28477D4CAC1D8AC81C947E60429156533CE16EDB0A7B44B296A05B2CE4237A
After SHA256:B75BEACE203D55C20D4B795580082FE88E9BDCDB6CF7B1A83C75E794BC45B7AD
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-baseline\\CommanderChatUI.Presentation.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.Presentation.cs"
index f97644f..feab37f 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-baseline\\CommanderChatUI.Presentation.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.Presentation.cs"	
@@ -418,31 +418,33 @@ namespace OpenEmpires
             var textSettings = CompactButton(compactCommander.transform,"TextAIOptions","Text AI",()=>{if(semanticSetupVisible)HideSemanticSetup();else ShowSemanticSetup();});
             compactRecord = CompactButton(compactCommander.transform,"Record","● Record",OnPttButtonClicked);
             compactRecordLabel = compactRecord.GetComponentInChildren<TMP_Text>();
             compactCancel = CompactButton(compactCommander.transform,"CancelVoice","Cancel",OnVoiceCancelClicked);
             PanelRow("Launcher",compactCommander.transform,open.gameObject);
             compactCommander.transform.Find("Launcher").SetAsFirstSibling();
             settings.gameObject.SetActive(false);
             textSettings.gameObject.SetActive(false);
             settingsMenu = UIObject("SettingsMenu", compactCommander.transform);
             settingsMenu.AddComponent<Image>().color = new Color(.09f,.11f,.13f,1f);
-            PanelElement(settingsMenu, 154);
+            PanelElement(settingsMenu, 192);
             var settingsLayout = settingsMenu.AddComponent<VerticalLayoutGroup>();
             settingsLayout.padding = new RectOffset(6,6,6,6); settingsLayout.spacing = 5;
             settingsLayout.childControlWidth = settingsLayout.childControlHeight = true;
             settingsLayout.childForceExpandWidth = true; settingsLayout.childForceExpandHeight = false;
             var settingsHeading = Text("Heading",settingsMenu.transform,"SETTINGS",13,TextAlignmentOptions.Left);
             settingsHeading.richText = false; PanelElement(settingsHeading.gameObject,18);
             PanelElement(CompactButton(settingsMenu.transform,"VoiceSettings","Voice settings",()=>
             {CloseSettingsMenu();ShowVoiceSetup();}).gameObject,32);
             PanelElement(CompactButton(settingsMenu.transform,"TextAISettings","Text AI settings",()=>
             {CloseSettingsMenu();ShowSemanticSetup();}).gameObject,32);
+            PanelElement(CompactButton(settingsMenu.transform,"CaptureEvidence","Capture UI evidence",()=>
+            {CloseSettingsMenu();RequestEvidenceScreenshot();}).gameObject,32);
             PanelElement(CompactButton(settingsMenu.transform,"CloseSettings","Close",CloseSettingsMenu).gameObject,32);
             settingsMenu.SetActive(false);
             compactStatus = Text("Status",compactCommander.transform,"Preparing Commander…",12,TextAlignmentOptions.Left);
             compactStatus.richText = false; PanelElement(compactStatus.gameObject,28);
             BuildTaskBoardUI();
             BuildVoiceSetupUI();
             BuildSemanticSetupUI();
             compactReview = ReviewCard("TranscriptReview","Review transcript — not submitted",out compactReviewText,
                 new[]{"Send","Edit","Discard"},new UnityEngine.Events.UnityAction[]{()=>{_ = SubmitCurrentInputAsync();},OpenCommander,OnVoiceCancelClicked});
             compactApproval = ReviewCard("PlanApproval","Plan approval — not started",out compactApprovalText,
```
## Assets\Scripts\AI\Commander\CommanderGoal.cs
Before SHA256:7A01CF916889B72E23DB1EC336A6044B9B3E4F873C695D6606100093729AE57D
After SHA256:CF3BCBC96005DE0B9F4B42F7220F7D91AA97EEA8E0B506A86637601430B0D985
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-baseline\\CommanderGoal.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\CommanderGoal.cs"
index 76ab9ce..c27a245 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-baseline\\CommanderGoal.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\CommanderGoal.cs"	
@@ -187,20 +187,23 @@ namespace OpenEmpires
         public int? PlacedTileZ { get; internal set; }
         public int PlacedBuildingId { get; internal set; } = -1;
         public CommanderPlacementBlocker PlacementBlocker { get; internal set; }
         internal int PlacementIssuedTick { get; set; } = -1;
         internal int PlacementIssuedSimulationTick { get; set; } = -1;
         internal ICommand PendingPlacementCommand { get; set; }
         // Result-binding state is captured by the game-side manager, never by the provider.
         internal bool HasResultConsumer { get; set; }
         internal readonly HashSet<int> BaselineBuildingIds = new HashSet<int>();
         internal readonly List<int> AttributedBuildingIds = new List<int>();
+        // Captured only after the ordinary placement command creates this goal's foundation.
+        // These are observed game-side builders, not a provider-selected identity.
+        internal IReadOnlyList<int> LastAcceptedBuilderUnitIds { get; set; } = Array.Empty<int>();
         internal CommanderBoundLocation DynamicLocation { get; set; }
         internal IReadOnlyList<Vector2Int> DynamicPlannedSiteSet { get; set; }
         internal IReadOnlyList<int> ResultBuildingIds { get; private set; } = Array.Empty<int>();
         internal int ResultCaptureTick { get; private set; } = -1;
         public bool HasSemanticPlacement => PlacementAnchorSelector.HasValue || DynamicLocation != null;
 
         internal void CaptureBuildingResult(IReadOnlyList<int> buildingIds, int tick)
         {
             ResultBuildingIds = buildingIds ?? Array.Empty<int>();
             ResultCaptureTick = tick;
```
## Assets\Scripts\AI\Commander\CommanderGoalManager.cs
Before SHA256:3E4EA9E5D6FE86B34A57E519C52BC580E39552587CE268A099E6DFE5270CFE64
After SHA256:D09934D21DA93FFB5CC8F929DE65278A58C625F4F5E9EE8E1BEAEB4686FF8082
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-baseline\\CommanderGoalManager.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\CommanderGoalManager.cs"
index 10babb4..0245115 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-baseline\\CommanderGoalManager.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\CommanderGoalManager.cs"	
@@ -815,20 +815,22 @@ namespace OpenEmpires
                     || goal.IsTerminal || !ReferenceEquals(goal.PendingPlacementCommand, command))
                     continue;
                 goal.PendingPlacementCommand = null;
                 if (created.PlayerId == goal.PlayerId && !created.IsDestroyed
                     && simulation.GetEffectiveBuildingType(created) == goal.StructureType
                     && created.OriginTileX == goal.PlacedTileX
                     && created.OriginTileZ == goal.PlacedTileZ)
                 {
                     goal.PlacedBuildingId = created.Id;
                     if (!goal.AttributedBuildingIds.Contains(created.Id)) goal.AttributedBuildingIds.Add(created.Id);
+                    if (command is PlaceBuildingCommand placed && placed.VillagerUnitIds != null)
+                        goal.LastAcceptedBuilderUnitIds = Array.AsReadOnly(placed.VillagerUnitIds.ToArray());
                 }
                 return;
             }
         }
 
         private void PublishEvent(CommanderGoalEventType type, CommanderGoal goal, int tick)
         {
             GoalEventPublished?.Invoke(new CommanderGoalEvent(type, tick, goal));
         }
 
```
## Assets\Scripts\AI\Commander\Economy\CommanderRequestGraphDraft.cs
Before SHA256:836C3E16591B17969A100D6CB0E9B2DC93FEF2C6C09B472527BDC7F323BD8283
After SHA256:FF564A3B1876C94B7CCDD380BFB3553E8A7C1F9FC3F407211D49FF8A7C7591AF
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-baseline\\CommanderRequestGraphDraft.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Economy\\CommanderRequestGraphDraft.cs"
index c7e6cb0..d5397f7 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-baseline\\CommanderRequestGraphDraft.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Economy\\CommanderRequestGraphDraft.cs"	
@@ -207,21 +207,23 @@ namespace OpenEmpires
                 if (!ReferenceEquals(other, slot))
                     remaining.Add(new JObject { ["node"] = other.Node, ["path"] = other.Path,
                         ["type"] = other.Type, ["alternatives"] = new JArray(other.Alternatives) });
             return ParseLocal(new JObject { ["outcome"] = "Clarify",
                 ["message"] = "Please specify the remaining detail.", ["pending"] = next,
                 ["missingSlots"] = remaining }, slot.Path, chosen);
         }
 
         internal static bool IsCorrectionUtterance(string reply)
         {
-            foreach (var word in Words(reply))
+            string[] correctionWords = Words(reply);
+            if (correctionWords.Length >= 3 && correctionWords[0] == "make" && correctionWords[1] == "that") return true;
+            foreach (var word in correctionWords)
                 if (word == "instead" || word == "actually" || word == "change" || word == "rather") return true;
             return false;
         }
 
         private sealed class Change
         {
             internal int Node;
             internal string Path;
             internal JToken Value;
         }
@@ -275,21 +277,21 @@ namespace OpenEmpires
             int? number = null;
             foreach (var word in words)
                 if (CommanderClarificationReplies.TryParseCount(word, out int parsed))
                 {
                     if (number.HasValue && number.Value != parsed) return null;
                     number = parsed;
                 }
             if (number.HasValue)
             {
                 bool workers = Contains(words, "villager") || Contains(words, "villagers") || Contains(words, "worker") || Contains(words, "workers");
-                bool amount = Contains(words, "additional") || Contains(words, "stockpile") || Contains(words, "food")
+                bool amount = prior == "amount" || Contains(words, "additional") || Contains(words, "stockpile") || Contains(words, "food")
                     || Contains(words, "wood") || Contains(words, "gold") || Contains(words, "stone");
                 if (workers && amount) return null;
                 int fields = 0;
                 for (int i = 0; i < nodes.Count; i++)
                 {
                     var node = (JObject)nodes[i];
                     if (node["count"]?.Type == JTokenType.Integer && !amount) fields++;
                     if (node["resourceAmount"]?.Type == JTokenType.Integer && !workers) fields++;
                 }
                 if (fields != 1) return null;
@@ -297,22 +299,24 @@ namespace OpenEmpires
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
-            if (prior != null && !NamesCurrentValue(prior, change)) return null;
-            if (change.Path == "resourceAmount" && !AmountQualifierMatchesGraph(replacement, change.Node)) return null;
+            if (prior != null && !(prior == "amount" && change.Path == "resourceAmount")
+                && !NamesCurrentValue(prior, change)) return null;
+            if (change.Path == "resourceAmount" && prior != "amount"
+                && !AmountQualifierMatchesGraph(replacement, change.Node)) return null;
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
@@ -322,40 +326,46 @@ namespace OpenEmpires
             return ParseLocal(new JObject { ["outcome"] = "Clarify",
                 ["message"] = "Please specify the remaining detail.", ["pending"] = next,
                 ["missingSlots"] = remaining }, change.Path, change.Value);
         }
 
         // Correction-like wording keeps a pending draft local, but only these complete,
         // affirmative shapes are allowed to change one typed leaf. Unknown language fails closed.
         private static bool TryAffirmativeCorrection(string reply, out string replacement, out string prior)
         {
             replacement = null; prior = null;
-            string whole = Regex.Replace((reply ?? string.Empty).Trim().ToLowerInvariant(), @"\s+", " ");
+            string whole = Regex.Replace((reply ?? string.Empty).Trim().TrimEnd('.', '!', '?').Trim().ToLowerInvariant(), @"\s+", " ");
             if (!Regex.IsMatch(whole, @"^[a-z0-9 ]+$")) return false;
             for (int i = 0; i < 2; i++)
             {
                 if (whole.StartsWith("please ", StringComparison.Ordinal)) whole = whole.Substring(7);
                 else if (whole.StartsWith("actually ", StringComparison.Ordinal)) whole = whole.Substring(9);
                 else break;
             }
             if (whole.StartsWith("change ", StringComparison.Ordinal))
             {
                 string rest = whole.Substring(7);
                 int separator = rest.IndexOf(" to ", StringComparison.Ordinal);
                 if (separator <= 0 || rest.IndexOf(" to ", separator + 4, StringComparison.Ordinal) >= 0) return false;
                 prior = rest.Substring(0, separator);
                 replacement = rest.Substring(separator + 4);
                 if (prior.StartsWith("the ", StringComparison.Ordinal)) prior = prior.Substring(4);
                 if (prior.StartsWith("a ", StringComparison.Ordinal)) prior = prior.Substring(2);
+                if (prior == "amount" && CommanderClarificationReplies.TryParseCount(replacement, out int _)) return true;
                 if (!IsTypedCorrectionValue(prior) || !IsTypedCorrectionValue(replacement)) return false;
                 return true;
             }
+            if (whole.StartsWith("make that ", StringComparison.Ordinal))
+            {
+                replacement = whole.Substring(10);
+                return IsTypedCorrectionValue(replacement);
+            }
             if (!whole.EndsWith(" instead", StringComparison.Ordinal)) return false;
             replacement = whole.Substring(0, whole.Length - 8);
             if (replacement.StartsWith("build a ", StringComparison.Ordinal)) replacement = replacement.Substring(8);
             else if (replacement.StartsWith("make a ", StringComparison.Ordinal)) replacement = replacement.Substring(7);
             else if (replacement.StartsWith("a ", StringComparison.Ordinal)) replacement = replacement.Substring(2);
             else if (replacement.StartsWith("an ", StringComparison.Ordinal)) replacement = replacement.Substring(3);
             return IsTypedCorrectionValue(replacement);
         }
 
         private static bool IsTypedCorrectionValue(string phrase)
@@ -428,21 +438,23 @@ namespace OpenEmpires
 
         private static bool Contains(string[] words, string word)
         { foreach (string entry in words) if (entry == word) return true; return false; }
 
         private static string[] Words(string text)
         { return (text ?? string.Empty).Trim().ToLowerInvariant().Split(
             new[] { ' ', '\t', '\r', '\n', ',', '.', '!', '?', ';', ':' }, StringSplitOptions.RemoveEmptyEntries); }
 
         private static JToken Match(Slot slot, string reply)
         {
-            string whole = (reply ?? string.Empty).Trim().ToLowerInvariant();
+            // Only terminal sentence punctuation is presentation, not a slot value.
+            // Preserve the original reply in the retained request and evidence.
+            string whole = (reply ?? string.Empty).Trim().TrimEnd('.', '!', '?').Trim().ToLowerInvariant();
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
```
## Assets\Scripts\AI\Commander\Phase5A\CommanderRequestTicket.cs
Before SHA256:66B717BB4AF89DB4A6F55406A89F6A66EDE6D2945D8469A6A0B0975561B8A8CA
After SHA256:96CA372B5926A30F549A25D969A412054E63B2BDD3C41C1B302B3FA8DA473CEB
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-baseline\\CommanderRequestTicket.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderRequestTicket.cs"
index f815a0c..113b38d 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-baseline\\CommanderRequestTicket.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderRequestTicket.cs"	
@@ -4,36 +4,51 @@ namespace OpenEmpires
 {
     // Local correlation exists before interpretation, but grants no execution authority.
     // The ticket can bind one candidate only; no provider field can supply this object.
     internal sealed class CommanderRequestTicket
     {
         internal long Id { get; }
         internal CommanderGoalManager Owner { get; }
         internal GameSimulation Runtime { get; }
         internal string OriginalInput { get; }
         internal int Generation { get; }
-        private readonly Func<bool> current;
+        private Func<bool> current;
         private bool claimed;
 
         internal CommanderRequestTicket(long id, CommanderGoalManager owner, string input,
             int generation, Func<bool> current)
         {
             Id = id; Owner = owner; Runtime = owner.Simulation;
             OriginalInput = input; Generation = generation; this.current = current;
         }
 
         internal bool IsCurrent()
         {
             try { return !Owner.IsDisposed && ReferenceEquals(Owner.Simulation, Runtime)
                 && (current?.Invoke() ?? true); }
             catch (Exception) { return false; }
         }
 
+        // A retained local clarification is a new UI turn of the same player
+        // request. Its old turn-revision predicate must not authorize admission,
+        // but the ticket's identity and original utterance must remain stable.
+        internal bool BindLocalContinuation(CommanderGoalManager owner, int generation,
+            Func<bool> currentTurn)
+        {
+            if (claimed || !ReferenceEquals(owner, Owner) || generation != Generation
+                || Owner.IsDisposed || !ReferenceEquals(Runtime, owner.Simulation)
+                || currentTurn == null) return false;
+            try { if (!currentTurn()) return false; }
+            catch (Exception) { return false; }
+            current = currentTurn;
+            return true;
+        }
+
         internal bool Claim(CommanderGoalManager owner, string input, int generation)
         {
             if (claimed || !ReferenceEquals(owner, Owner) || generation != Generation
                 || input != OriginalInput || !IsCurrent()) return false;
             claimed = true;
             return true;
         }
     }
 }
```
## Assets\Scripts\AI\Commander\CommanderTacticalStatusSnapshot.cs
Before SHA256:75D58B28015248E106FD8F4958498CE69F1D11E241F491EB4C5CF645C7216AF1
After SHA256:682B3588D374D458661D0D16526CD09C5AF8BAAE4A87AC149DED400448259C90
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-baseline\\CommanderTacticalStatusSnapshot.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\CommanderTacticalStatusSnapshot.cs"
index 9a7ad15..008294c 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-baseline\\CommanderTacticalStatusSnapshot.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\CommanderTacticalStatusSnapshot.cs"	
@@ -109,21 +109,21 @@ namespace OpenEmpires
     }
 
     internal static class CommanderTacticalStatusProjection
     {
         internal static bool IsStatusQuestion(string input) => Regex.IsMatch(input??"",
             @"\b(status|progress|progressing|doing|waiting|finished|ready|blocked|stuck|request|army)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
         internal static bool IsCausalTaskQuestion(string input,GameSimulation sim,CommanderGoalManager manager)
         {
             string query=(input??"").Trim();
             if(!Regex.IsMatch(query,@"^(why|what happened|how come|what went wrong)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return false;
-            if(Regex.IsMatch(query,@"\b(task|order|goal|arriv(?:e|ed|ing)|stop(?:ped)?|staff(?:ed|ing)?)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return true;
+            if(Regex.IsMatch(query,@"\b(task|order|goal|arriv(?:e|ed|ing)|stop(?:ped)?|staff(?:ed|ing)?|builders?)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return true;
             if(Regex.IsMatch(query,@"^what happened\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)
                 &&ReferencedUnitCount(query).HasValue)return true;
             var catalog=GameKnowledgeCatalog.Build(sim);
             return catalog.Units.Any(u=>Mention(query,u.DisplayName)||u.Aliases.Any(a=>Mention(query,a)))
                 ||catalog.Buildings.Any(b=>Mention(query,b.DisplayName)||b.Aliases.Any(a=>Mention(query,a)));
         }
         private static int? ReferencedUnitCount(string query)
         {
             var match=Regex.Match(query??"",@"\b(?:my|the|our)\s+(?<count>\d{1,3}|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen|twenty)\s+(?:selected\s+)?units?\b",
                 RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
@@ -142,26 +142,27 @@ namespace OpenEmpires
             if(Regex.IsMatch(text,@"^can (i|we) (make|train|build) (?:a |an )?[a-z]+\s*[?.!]*$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return true;
             if(Regex.IsMatch(text,@"^(can|could|would|will) you (?:(tell|show|remind) me\b|(?:explain|report)\b)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return true;
             return text.EndsWith("?",StringComparison.Ordinal)
                 && !Regex.IsMatch(text,@"^(can|could|would|will) (you|we|i)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)
                 && !SimpleTextIntentParser.IsExplicitOrderForm(text);
         }
 
         internal static CommanderTacticalStatusSnapshot Observe(GameSimulation sim,CommanderGoalManager manager,string query)
         {
             var catalog=GameKnowledgeCatalog.Build(sim);
+            bool builderRole=Regex.IsMatch(query??"",@"\bbuilders?\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
             var all=manager.ActiveGoals.Concat(manager.ArchivedGoals.Reverse().Take(CommanderGoalManager.MaxArchivedGoals))
                 .Where(g=>g.PlayerId==manager.PlayerId&&ReferenceEquals(g.RuntimeOwner,manager)).ToList();
             bool HasMention(CommanderGoal goal)
             {
                 if(goal is EnsureUnitCountGoal unit)return UnitMention(unit.RequestedUnitType);
-                if(goal is BuildStructureGoal build)return BuildingMention(build.StructureType);
+                if(goal is BuildStructureGoal build)return builderRole||BuildingMention(build.StructureType);
                 if(goal is CommanderCapabilityGoal action)
                     return action.Action.UnitSelector.Kind==CommanderUnitSelectorKind.UnitType&&UnitMention(action.Action.UnitSelector.UnitType)
                         || action.Action.TargetSelector.HasValue&&(action.Action.TargetSelector.Value.Kind==CommanderTargetSelectorKind.UnitType
                             ?UnitMention(action.Action.TargetSelector.Value.UnitType):BuildingMention(action.Action.TargetSelector.Value.StructureType.Value));
                 return false;
             }
             bool UnitMention(int type)
             {
                 var record=catalog.FindUnitByType(type);var actual=catalog.FindUnitByType(sim.ResolveCivUnitType(manager.PlayerId,type));
                 return Mention(query,CommanderIntentCatalog.GetUnitDisplayName(type))||Mention(query,CommanderIntentCatalog.GetUnitDisplayName(type,true))
@@ -237,21 +238,40 @@ namespace OpenEmpires
                     &&(exactProducerIds==null||exactProducerIds.Contains(b.Id))
                     &&(units.BoundProducerBuildingIds==null||units.BoundProducerBuildingIds.Contains(b.Id))).OrderBy(b=>b.Id).ToList();
                 ready=producers.Count(b=>!b.IsUnderConstruction);unfinished=producers.Count-ready;
                 var producer=producers.FirstOrDefault(b=>!b.IsUnderConstruction);
                 if(producer!=null)
                 { sim.GetUnitTrainingCosts(producer,units.RequestedUnitType,out int food,out int wood,out int gold);var stock=sim.ResourceManager.GetPlayerResources(goal.PlayerId);
                     deficit=new KnowledgeCost(Math.Max(0,food-stock.Food),Math.Max(0,wood-stock.Wood),Math.Max(0,gold-stock.Gold)); }
             }
             else if(goal is BuildStructureGoal building)
             { requested=building.Count;label="Build "+building.Count+" "+CommanderIntentCatalog.GetStructureDisplayName(building.StructureType);
-                produced=building.AttributedBuildingIds.Count(id=>sim.BuildingRegistry.GetBuilding(id) is BuildingData b&&b.PlayerId==goal.PlayerId&&!b.IsDestroyed&&!b.IsUnderConstruction); }
+                produced=building.AttributedBuildingIds.Count(id=>sim.BuildingRegistry.GetBuilding(id) is BuildingData b&&b.PlayerId==goal.PlayerId&&!b.IsDestroyed&&!b.IsUnderConstruction);
+                BuildingData acceptedFoundation=sim.BuildingRegistry.GetBuilding(building.PlacedBuildingId);
+                if(!building.IsTerminal&&acceptedFoundation!=null&&acceptedFoundation.PlayerId==goal.PlayerId
+                    &&!acceptedFoundation.IsDestroyed&&acceptedFoundation.IsUnderConstruction
+                    &&building.LastAcceptedBuilderUnitIds.Count>0
+                    &&building.AttributedBuildingIds.Contains(building.PlacedBuildingId))
+                {
+                    bool dead=false,foreign=false,human=false;
+                    foreach(int builderId in building.LastAcceptedBuilderUnitIds)
+                    {
+                        UnitData assigned=sim.UnitRegistry.GetUnit(builderId);
+                        if(assigned==null||assigned.CurrentHealth<=0||assigned.State==UnitState.Dead)dead=true;
+                        else if(assigned.PlayerId!=goal.PlayerId)foreign=true;
+                        else if(manager.ObserveHumanProtection(builderId))human=true;
+                    }
+                    if(dead)reason="An accepted construction builder is dead or missing; no unrelated survivor proves that assignment complete.";
+                    else if(foreign)reason="An accepted construction builder is no longer owned; no foreign unit can satisfy the assignment.";
+                    else if(human){takeover=true;reason="The player took control of an accepted construction builder; that worker remains protected from Commander recovery.";}
+                }
+            }
             else if(goal is AllocateWorkersGoal workers){label="Workers to "+workers.Allocation.Destination.Resource;requested=workers.Allocation.Count??workers.SelectedWorkerIds.Count;takeover|=workers.HumanInterrupted;}
             else if(goal is ResourceAllocationGoal allocation){label="Workers to "+allocation.Resource;requested=allocation.TargetWorkers;}
             else if(goal is ReachAgeGoal age){label="Reach age "+age.TargetAge;requested=age.TargetAge;}
             else if(goal is CommanderCapabilityGoal capability)
             {
                 label=capability.Action.ActionType.ToString();requested=capability.Action.UnitSelector.Count;takeover|=capability.ResultHumanOverride;
                 if(capability.Action.ActionType==CommanderCapabilityActionType.AttackTarget)
                 {
                     UnitData enemy=capability.TargetBinding?.Unit;
                     BuildingData enemyBuilding=capability.TargetBinding?.Building;
```
## Docs/CommanderPhase5C/evidence/phase5c-replay-corpus.v1.json
Before SHA256:944F5D9FC4F4CF659738E0F7CBF7F4F56BD2211456C2EB5BDB26424BD13B3CFA
After SHA256:41FEAF3C22FF04384194B3D529781B215979A65CD66ED5FC32DE563F371E03F2
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-baseline\\phase5c-replay-corpus.v1.json" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\phase5c-replay-corpus.v1.json"
index 330d862..dc58861 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-baseline\\phase5c-replay-corpus.v1.json"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\phase5c-replay-corpus.v1.json"	
@@ -17,27 +17,27 @@
       "fixtures": [{"path": "Assets/Tests/EditMode/CommanderEconomyClarificationTests.Graph.cs", "tests": ["HouseThenNextFive_FromTreesKeepsGraphAndNeedsSeparateApproval", "TwoMissingSlots_CountThenSourceResumeSameDraftLocally"]}, {"path": "Assets/Tests/EditMode/CommanderPhase5BFutureOrdersTests.cs", "tests": ["NextBirthsFromBoundProducer_IncludeOldQueueAndNeverTrainOrSwitchProducers", "NewVillagersCanBeBoundToGatherWithoutSelectingOldVillagers"]}]
     },
     {
       "id": "amount-versus-worker-count",
       "turns": ["Gather 200 gold with 3 villagers."],
       "expected": {"semantic": {"resourceAmount": 200, "amountMode": "Stockpile", "workerCount": 3, "countMode": "Exact", "resource": "Gold"}, "constraints": ["resource amount mode Stockpile is distinct from worker quantity mode TargetTotal"], "order": [], "exactResult": "objective remains incomplete until amount condition is satisfied", "approval": "ordinary allocation admission", "localZeroCall": []},
       "fixtures": [{"path": "Assets/Tests/EditMode/CommanderPhase5BClarificationTests.cs", "tests": ["ResourceAmountClarification_RetainsObjectiveThroughLocalCountReply", "ResourceObjective_DoesNotCompleteWhenWorkersAreAssigned"]}, {"path": "Assets/Tests/EditMode/CommanderPhase5BReliabilitySemanticTests.cs", "tests": ["BerriesAmountCountContract_PreservesIndependentSlots"]}]
     },
     {
       "id": "count-correction-only",
-      "turns": ["Gather 200 gold with 3 villagers.", "Make that 4 villagers."],
+      "turns": ["Gather 200 gold with 3 villagers.", "Make that 4 villagers.", "From gold mines."],
       "expected": {"semantic": {"resourceAmount": 200, "amountMode": "Stockpile", "workerCount": 4, "workerQuantityMode": "TargetTotal", "countMode": "Exact"}, "constraints": ["correction changes worker count only", "preserve resource amount and Stockpile mode", "worker TargetTotal is not resource amount mode"], "order": ["initial request", "local correction"], "exactResult": "amount stays 200; count becomes 4", "approval": "ordinary allocation admission", "localZeroCall": ["correction"]},
       "fixtures": [{"path": "Assets/Tests/EditMode/CommanderEconomyClarificationTests.Graph.cs", "tests": ["KnownCountCorrection_DoesNotChangeResourceAmount"]}, {"path": "Assets/Tests/EditMode/CommanderPhase5BClarificationTests.cs", "tests": ["ExplicitAmountCorrection_ChangesOnlyAmountAndKeepsWorkerAndSourceCriteria"]}]
     },
     {
       "id": "amount-correction-only",
-      "turns": ["Gather 200 gold with 3 villagers.", "Change the amount to 250."],
+      "turns": ["Gather 200 gold with 3 villagers.", "Change the amount to 250.", "From gold mines."],
       "expected": {"semantic": {"resourceAmount": 250, "amountMode": "Stockpile", "workerCount": 3, "workerQuantityMode": "TargetTotal", "countMode": "Exact"}, "constraints": ["correction changes amount only", "preserve worker and source criteria", "resource amount mode is Stockpile; worker TargetTotal is separate"], "order": ["initial request", "local correction"], "exactResult": "amount becomes 250; count stays 3", "approval": "ordinary allocation admission", "localZeroCall": ["correction"]},
       "fixtures": [{"path": "Assets/Tests/EditMode/CommanderEconomyClarificationTests.Graph.cs", "tests": ["KnownResourceAmountCorrection_DoesNotChangeWorkerCount"]}, {"path": "Assets/Tests/EditMode/CommanderPhase5BClarificationTests.cs", "tests": ["ExplicitAmountCorrection_ChangesOnlyAmountAndKeepsWorkerAndSourceCriteria"]}]
     },
     {
       "id": "all-current-wood-to-berries",
       "turns": ["Send all my wood gatherers to berries."],
       "expected": {"semantic": {"workerSelector": {"countMode": "AllMatching", "currentResource": "Wood"}, "destination": {"resource": "Food", "sourceKind": "Berries"}}, "constraints": ["bounded eligible snapshot", "no invented exact count", "source selection respects visible/known facts"], "order": [], "exactResult": "all eligible current-Wood workers frozen for the request", "approval": "ordinary allocation admission", "localZeroCall": []},
       "fixtures": [{"path": "Assets/Tests/EditMode/CommanderPhase5BReliabilitySemanticTests.cs", "tests": ["BerriesAmountCountContract_PreservesIndependentSlots"]}, {"path": "Assets/Tests/EditMode/CommanderEconomyClarificationTests.Execution.cs", "tests": ["QuantityModes_KeepBaselineTransferAndAllMatchingDistinct"]}]
     },
     {
```
## NEW Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.EvidenceCapture.cs
SHA256:90FF148818A2717135B1FF9D5377EE7B73D79D6107A705E42EA8A0B7EE0304CC
```
using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace OpenEmpires
{
    public sealed partial class CommanderChatUI
    {
        private const int MaximumEvidenceScreenshotsPerSession = 3;
        private int evidenceScreenshotCount;
        private bool evidenceScreenshotInProgress;
        private string nextEvidenceKind = "manual";
        private readonly List<string> offlineEvidenceCapturePaths = new List<string>();

        // An explicit local UI action only. It does not invoke the provider, issue orders,
        // start audio, or serialize Commander context. Browser evidence uses the browser's
        // own screenshot route; WebGL's virtual persistent store is not a user download.
        public string LastEvidenceScreenshotPath { get; private set; }
        public string LastEvidenceScreenshotKind { get; private set; }
        public int LastEvidenceScreenshotWidth { get; private set; }
        public int LastEvidenceScreenshotHeight { get; private set; }
        public IReadOnlyList<string> OfflineEvidenceCapturePaths => offlineEvidenceCapturePaths.AsReadOnly();

        private sealed class OfflineEvidenceProvider : ICommanderAIProvider, ICommanderSemanticProvider
        {
            private const string SyntheticTwoHouses = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + "{\"id\":\"site\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"MyTownCenter\",\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{},\"dependsOn\":[]},"
                + "{\"id\":\"houses\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:House\",\"count\":2},\"inputs\":{\"location\":\"site\"},\"dependsOn\":[\"site\"]}]}";
            internal int Calls { get; private set; }
            public Task<CommanderSemanticResult> TranslateSemanticAsync(CommanderSemanticProviderRequest request,
                CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                Calls++;
                return Task.FromResult(Calls == 1 && request?.PlayerMessage == "Build two Houses near my Town Center."
                    ? CommanderSemanticJson.ParseProviderResponse(SyntheticTwoHouses)
                    : CommanderSemanticResult.ProviderRejected("Offline fixture accepts exactly one declared request."));
            }
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request, CancellationToken token)
                => Task.FromResult(CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure,
                    "Offline fixture does not use the legacy provider route."));
        }

        private static bool ShouldStartCliOfflineFixture()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            foreach (string argument in Environment.GetCommandLineArgs())
                if (argument == "--commander-evidence-offline-ui-fixture") return true;
#endif
            return false;
        }

        private IEnumerator CaptureOfflineFixtureOnce()
        {
            offlineEvidenceCapturePaths.Clear();
            AppendLine("Commander", "OFFLINE FIXTURE UI EVIDENCE — synthetic response; not live Luna or English-accuracy proof.", false);
            OpenCommander();
            var request = SubmitMessageAsync("Build two Houses near my Town Center.");
            for (int frame = 0; frame < 120 && !request.IsCompleted; frame++) yield return null;
            if (!request.IsCompleted || request.IsFaulted || request.IsCanceled || pendingActionPlan == null)
                yield break;
            yield return CaptureOfflineFrame("offline-fixture-pending-approval");
            if (offlineEvidenceCapturePaths.Count != 1) yield break;

            // Distinct UI turn; only the explicit fixture flag authorizes this normal admission.
            var approval = SubmitMessageAsync("Approve plan");
            for (int frame = 0; frame < 120 && !approval.IsCompleted; frame++) yield return null;
            if (!approval.IsCompleted || approval.IsFaulted || approval.IsCanceled
                || semanticGoalManager == null || semanticGoalManager.Goals.Count == 0) yield break;
            tasksTabButton?.onClick.Invoke();
            yield return null;
            yield return CaptureOfflineFrame("offline-fixture-tasks");
            if (offlineEvidenceCapturePaths.Count != 2) yield break;

            chatTabButton?.onClick.Invoke();
            var status = SubmitMessageAsync("strategy status"); // local read-only, zero provider calls
            for (int frame = 0; frame < 120 && !status.IsCompleted; frame++) yield return null;
            if (!status.IsCompleted || status.IsFaulted || status.IsCanceled) yield break;
            yield return null;
            yield return CaptureOfflineFrame("offline-fixture-chat");
        }

        private IEnumerator CaptureOfflineFrame(string kind)
        {
            string previous = LastEvidenceScreenshotPath;
            nextEvidenceKind = kind;
            if (!RequestEvidenceScreenshot()) yield break;
            for (int frame = 0; frame < 120 && evidenceScreenshotInProgress; frame++) yield return null;
            if (!evidenceScreenshotInProgress && LastEvidenceScreenshotPath != previous
                && LastEvidenceScreenshotKind == kind)
                offlineEvidenceCapturePaths.Add(LastEvidenceScreenshotPath);
        }

        private static bool ShouldStartCliEvidenceCapture()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            foreach (string argument in Environment.GetCommandLineArgs())
                if (argument == "--commander-evidence-status-capture") return true;
#endif
            return false;
        }

        private IEnumerator CaptureStatusEvidenceOnce()
        {
            // Exact local information route in SubmitMessageAsync: no semantic provider,
            // no gameplay order, no microphone. This runs only with an explicit CLI flag.
            var status = SubmitMessageAsync("strategy status");
            while (!status.IsCompleted) yield return null;
            if (status.IsFaulted || status.IsCanceled) yield break;
            yield return null;
            nextEvidenceKind = "status-only-local";
            RequestEvidenceScreenshot();
        }

        public bool RequestEvidenceScreenshot()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            if (!Application.isPlaying || evidenceScreenshotInProgress
                || evidenceScreenshotCount >= MaximumEvidenceScreenshotsPerSession
                || canvasRoot == null || !canvasRoot.activeInHierarchy)
                return false;
            CloseSettingsMenu();
            OpenCommander();
            evidenceScreenshotInProgress = true;
            StartCoroutine(CaptureEvidenceScreenshotAtEndOfFrame());
            return true;
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private IEnumerator CaptureEvidenceScreenshotAtEndOfFrame()
        {
            yield return new WaitForEndOfFrame();
            Texture2D texture = null;
            string temporary = null;
            try
            {
                Canvas.ForceUpdateCanvases();
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (texture == null) throw new InvalidOperationException("Screenshot unavailable.");
                byte[] png = texture.EncodeToPNG();
                if (png == null || png.Length < 8 || png.Length > 16 * 1024 * 1024)
                    throw new InvalidOperationException("Screenshot encoding unavailable or out of bounds.");
                string directory = Path.Combine(Application.persistentDataPath, "CommanderEvidence");
                Directory.CreateDirectory(directory);
                string kind = nextEvidenceKind;
                string destination = Path.Combine(directory, "commander-ui-" + kind + "-"
                    + texture.width + "x" + texture.height + "-"
                    + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".png");
                temporary = destination + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(png, 0, png.Length); stream.Flush(true); }
                File.Move(temporary, destination); // unique destination, no overwrite
                temporary = null;
                LastEvidenceScreenshotPath = destination;
                LastEvidenceScreenshotKind = kind;
                LastEvidenceScreenshotWidth = texture.width;
                LastEvidenceScreenshotHeight = texture.height;
                evidenceScreenshotCount++;
                Debug.Log("[Commander] Explicit local UI evidence screenshot saved.");
            }
            catch (Exception)
            {
                Debug.LogWarning("[Commander] Local UI evidence screenshot could not be saved.");
            }
            finally
            {
                if (temporary != null && File.Exists(temporary)) File.Delete(temporary);
                if (texture != null) Destroy(texture);
                nextEvidenceKind = "manual";
                evidenceScreenshotInProgress = false;
            }
        }
#endif
    }
}

```
## NEW Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.EvidenceCapture.cs.meta
SHA256:63012AFA25653C3E5EDFCF52B20D70B56E4E3C048895B25A66DEFC0CB121B264
```
fileFormatVersion: 2
guid: 84f9821d9cee9624d8484bb890c70ef7
```
## NEW Assets/Tests/EditMode/CommanderEconomyClarificationTests.Phase5CCorpus.cs
SHA256:681D9A9CDFA43F0A5E09EA54C32077EB517105DF9B70BE55E7B6313095AED31D
```
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using OpenEmpires.TestSupport;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public sealed partial class CommanderEconomyClarificationTests
    {
        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusCorrection_TypedCountsPreserveOtherSlotAndUseNoSecondCall()
        {
            foreach (string id in new[] { "count-correction-only", "amount-correction-only" })
            {
                Assert.That(TryResponse(id, out string response), Is.True);
                using (var fixture = new CorpusFixture(response))
                {
                    fixture.Seed(id);
                    await fixture.Chat.SubmitMessageAsync("Gather 200 gold with 3 villagers.");
                    Assert.That(fixture.Chat.PendingClarification?.GraphDraft, Is.Not.Null);
                    string correction = id == "count-correction-only"
                        ? "Make that 4 villagers." : "Change the amount to 250.";
                    var local = fixture.Chat.PendingClarification.GraphDraft.Resolve(correction);
                    Assert.That(local?.PendingGraphDraft, Is.Not.Null, id + " local correction resolution");
                    await fixture.Chat.SubmitMessageAsync(correction);
                    Assert.That(fixture.Provider.Calls, Is.EqualTo(1), id);
                    var pending = fixture.Chat.PendingClarification?.GraphDraft;
                    Assert.That(pending, Is.Not.Null, id);
                    var node = (JObject)((JArray)pending.ToPendingJson()["nodes"])[0];
                    Assert.That((int)node["count"], Is.EqualTo(id == "count-correction-only" ? 4 : 3), id);
                    Assert.That((int)node["resourceAmount"], Is.EqualTo(id == "amount-correction-only" ? 250 : 200), id);
                    Assert.That((string)node["resourceAmountMode"], Is.EqualTo("Stockpile"), id);
                }
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusCancelReset_NegativeControlAndResetKeepLateYesInert()
        {
            Assert.That(TryResponse("cancel-reset-late-retry", out string response), Is.True);
            using (var fixture = new CorpusFixture(response))
            {
                fixture.Seed("cancel-reset-late-retry");
                await fixture.Chat.SubmitMessageAsync("Build a House by the visible gold.");
                Assert.That(fixture.Chat.PendingClarification?.GraphDraft, Is.Not.Null);
                await fixture.Chat.SubmitMessageAsync("No, cancel that proposal.");
                Assert.That(fixture.Chat.PendingClarification, Is.Null);
                Assert.That(LatestUiState(fixture.Chat), Is.EqualTo("Cancelled"));
                await fixture.Chat.SubmitMessageAsync("Reset Commander.");
                Assert.That(LatestUiState(fixture.Chat), Is.EqualTo("Ready"));
                await fixture.Chat.SubmitMessageAsync("Yes.");
                Assert.That(fixture.Provider.Calls, Is.EqualTo(1));
                Assert.That(fixture.Economy.Manager.Goals, Is.Empty);
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusPunctuation_NoPeriodRejectsOnlyRetainedProposal()
        {
            Assert.That(TryResponse("mine-yes-original-request", out string response), Is.True);
            using (var fixture = new CorpusFixture(response))
            {
                fixture.Seed("mine-yes-original-request");
                await fixture.Chat.SubmitMessageAsync("Build a gold mine near gold.");
                Assert.That(fixture.Chat.PendingClarification?.GraphDraft, Is.Not.Null);
                await fixture.Chat.SubmitMessageAsync("No.");
                Assert.That(fixture.Provider.Calls, Is.EqualTo(1));
                Assert.That(fixture.Chat.PendingClarification, Is.Null);
                Assert.That(fixture.Economy.Manager.Goals, Is.Empty);
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusPunctuation_ResolvesRetainedGraphWithoutAnotherProviderCall()
        {
            Assert.That(TryResponse("mine-yes-original-request", out string mineResponse), Is.True);
            using (var fixture = new CorpusFixture(mineResponse))
            {
                fixture.Seed("mine-yes-original-request");
                await fixture.Chat.SubmitMessageAsync("Build a Mine near a visible gold deposit.");
                Assert.That(fixture.Chat.PendingClarification?.GraphDraft, Is.Not.Null);
                var locallyResolved = fixture.Chat.PendingClarification.GraphDraft.Resolve("Yes.");
                Assert.That(locallyResolved?.IsValid, Is.True);
                Assert.That(fixture.Economy.Manager.TryCompileActionPlan(locallyResolved, out _, out string reason),
                    Is.True, reason);
                await fixture.Chat.SubmitMessageAsync("Yes.");
                Assert.That(fixture.Provider.Calls, Is.EqualTo(1));
                Assert.That(fixture.Chat.PendingClarification, Is.Null);
                Assert.That(fixture.Economy.Manager.Goals.OfType<BuildStructureGoal>()
                    .Any(goal => goal.StructureType == BuildingType.Mine), Is.True);
            }
            Assert.That(TryResponse("house-next-five-from-trees", out string houseResponse), Is.True);
            using (var fixture = new CorpusFixture(houseResponse))
            {
                fixture.Seed("house-next-five-from-trees");
                await fixture.Chat.SubmitMessageAsync("Build a House and have the next five villagers gather wood from trees.");
                Assert.That(fixture.Chat.PendingClarification?.GraphDraft, Is.Not.Null);
                await fixture.Chat.SubmitMessageAsync("From trees.");
                Assert.That(fixture.Provider.Calls, Is.EqualTo(1));
                Assert.That(fixture.Chat.PendingClarification, Is.Null);
                Assert.That(fixture.Chat.PendingActionPlan, Is.Not.Null);
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusDiscovery_TravelRevealsRequestedHostileAndStops()
        {
            Assert.That(TryResponse("finite-discovery-distinct-from-point-scout", out string response), Is.True);
            using (var fixture = new CorpusFixture(response, "finite-discovery-distinct-from-point-scout"))
            {
                Assert.That(fixture.Discovery.TargetInitiallyHidden, Is.True);
                Assert.That(fixture.Discovery.NoOrderControlStable, Is.True,
                    "Native fog bootstrap plus idle/no-order ticks must not reveal the hostile target.");
                await fixture.Chat.SubmitMessageAsync("Find the enemy Town Center with my Scout.");
                Assert.That(fixture.Provider.Calls, Is.EqualTo(1));
                fixture.TickBounded(1500);
                Assert.That(fixture.Commands, Does.Contain("MoveCommand"));
                Assert.That(fixture.Native.Any(entry => entry == "Move:Complete"), Is.True);
                Assert.That(fixture.Discovery.TravelRevealedCount(), Is.GreaterThan(0));
                Assert.That(fixture.Discovery.TargetEverVisibleAfterTravel, Is.True);
                Assert.That(fixture.Manager.Goals.Single().Status, Is.EqualTo(CommanderGoalStatus.Completed));
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public void CorpusIdleOnlyFixture_ParsesStrictBuilderSelection()
        {
            Assert.That(TryResponse("idle-only-protection", out string response), Is.True);
            var parsed = CommanderSemanticJson.ParseProviderResponse(response);
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            Assert.That(parsed.Nodes.Single().Constraints.OfType<PreferredWorkersConstraint>().Count(), Is.EqualTo(1));
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public void CorpusOrderedHistoricalSynthetic_RejectsAtStrictParseBoundary()
        {
            var oldResponse = Request("{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":2,\"quantityMode\":\"New\"},"
                + "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":2,\"location\":\"WorkedResource\",\"resource\":\"Gold\",\"dependsOn\":[0],\"resultFromNode\":0}");
            var parsed = CommanderSemanticJson.ParseProviderResponse(oldResponse);
            Assert.That(parsed.IsValid, Is.False);
            Assert.That(parsed.SafeExplanation, Is.Not.Empty);
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusAssignedBuilder_NativeDeathIsNotConfusedWithHumanTakeover()
        {
            Assert.That(TryResponse("death-versus-takeover", out string response), Is.True);
            using (var fixture = new CorpusFixture(response, "death-versus-takeover"))
            {
                fixture.Seed("death-versus-takeover", deleteAssignedBuilder: true);
                Assert.That(fixture.BuilderAffinityVerified, Is.True);
                Assert.That(fixture.AssignedBuilderMissing, Is.True,
                    "The native delete must remove the already-attributed builder before the question.");
                int before = fixture.Commands.Count;
                await fixture.Chat.SubmitMessageAsync("What happened to the assigned builders?");
                Assert.That(QuestionFactClass(fixture.Provider.LastRequest), Is.EqualTo("dead-or-missing"));
                Assert.That(fixture.Commands.Count, Is.EqualTo(before));
                Assert.That(fixture.Provider.LastRequest.IsReadOnlyQuestion, Is.True);
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusUnattributedWorker_HumanStopDoesNotBecomeAnAssignedBuilderFact()
        {
            Assert.That(TryResponse("death-versus-takeover", out string response), Is.True);
            using (var fixture = new CorpusFixture(response, "unattributed-builder-control"))
            {
                fixture.Seed("unattributed-builder-control");
                Assert.That(fixture.BuilderAffinityVerified, Is.False);
                Assert.That(fixture.Native, Does.Not.Contain("PlaceBuildingCommand:processed"));
                await fixture.Chat.SubmitMessageAsync("What happened to the assigned builders?");
                Assert.That(QuestionFactClass(fixture.Provider.LastRequest), Is.Not.EqualTo("human-override"));
                Assert.That(fixture.Provider.LastRequest.IsReadOnlyQuestion, Is.True);
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusGarrison_CapacityAndProtectedRemainderAreNativeFacts()
        {
            Assert.That(TryResponse("garrison-capacity-protected-remainder", out string response), Is.True);
            using (var fixture = new CorpusFixture(response))
            {
                fixture.Seed("garrison-capacity-protected-remainder");
                await fixture.Chat.SubmitMessageAsync("Garrison all my villagers in my Town Center.");
                fixture.TickBounded(500);
                var goal = fixture.Manager.Goals.OfType<CommanderCapabilityGoal>().Single();
                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Completed));
                Assert.That(goal.GarrisonShelter.GarrisonCount, Is.EqualTo(1));
                Assert.That(goal.GarrisonProtectedCount, Is.EqualTo(1));
                Assert.That(goal.GarrisonUnaccommodatedCount, Is.EqualTo(1));
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusReplay_UsesProductionChatBoundaryAndPersistsAllDeclaredCases()
        {
            var run = typeof(CommanderEconomyClarificationTests).GetMethod("RunPhase5CCorpusAsync",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(run, Is.Not.Null, "The declared 25-case corpus needs an executable production-chat replay.");
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string corpus = Path.Combine(projectRoot, "Docs", "CommanderPhase5C", "evidence", "phase5c-replay-corpus.v1.json");
            string directory = Path.Combine(projectRoot, "Docs", "CommanderPhase5C", "evidence", "task-6-runs",
                Guid.NewGuid().ToString("N"));
            string result = await (Task<string>)run.Invoke(null, new object[] { corpus, directory });
            Assert.That(result, Is.EqualTo(directory));
            Assert.That(Directory.GetFiles(directory, "*.json").Length, Is.EqualTo(25));
            JObject mine = JObject.Parse(File.ReadAllText(Path.Combine(directory, "mine-yes-original-request.json")));
            JObject house = JObject.Parse(File.ReadAllText(Path.Combine(directory, "house-next-five-from-trees.json")));
            JObject malformed = JObject.Parse(File.ReadAllText(Path.Combine(directory, "malformed-provider-json.json")));
            JObject wrong = JObject.Parse(File.ReadAllText(Path.Combine(directory, "schema-valid-wrong-provider-result.json")));
            JObject ordered = JObject.Parse(File.ReadAllText(Path.Combine(directory, "ordered-production-exact-new-units.json")));
            JObject countCorrection = JObject.Parse(File.ReadAllText(Path.Combine(directory, "count-correction-only.json")));
            JObject amountCorrection = JObject.Parse(File.ReadAllText(Path.Combine(directory, "amount-correction-only.json")));
            JObject additionalFood = JObject.Parse(File.ReadAllText(Path.Combine(directory, "additional-gathered-food-bounded-objective.json")));
            JObject cancelled = JObject.Parse(File.ReadAllText(Path.Combine(directory, "cancel-reset-late-retry.json")));
            JObject garrison = JObject.Parse(File.ReadAllText(Path.Combine(directory, "garrison-capacity-protected-remainder.json")));
            Assert.That((string)mine["status"], Is.EqualTo("observed"));
            Assert.That((string)mine["fidelity"], Is.EqualTo("matched"));
            Assert.That((int)mine["providerCalls"], Is.EqualTo(1));
            Assert.That((string)house["status"], Is.EqualTo("observed"));
            Assert.That((string)house["fidelity"], Is.EqualTo("matched"));
            Assert.That((int?)house["futureObservedCount"], Is.EqualTo(5));
            Assert.That((int?)house["futureAssignedCount"], Is.EqualTo(5));
            Assert.That((int)house["providerCalls"], Is.EqualTo(1));
            Assert.That((string)malformed["errorStage"], Is.EqualTo("parse"));
            Assert.That((string)wrong["fidelity"], Is.EqualTo("wrong"));
            Assert.That((bool?)ordered["semanticValid"], Is.True);
            Assert.That((string)ordered["status"], Is.EqualTo("observed"));
            Assert.That((string)ordered["fidelity"], Is.EqualTo("matched"));
            Assert.That((int?)ordered["newResultCount"], Is.EqualTo(2));
            Assert.That((bool?)ordered["oldUnitExcluded"], Is.True);
            Assert.That((bool?)ordered["humanBirthExcluded"], Is.True);
            Assert.That((bool?)ordered["exactPatrolBinding"], Is.True);
            Assert.That((bool?)ordered["previewMentionsOwnedBase"], Is.True);
            Assert.That((string)countCorrection["status"], Is.EqualTo("observed"));
            Assert.That((string)amountCorrection["status"], Is.EqualTo("observed"));
            Assert.That((string)countCorrection["fidelity"], Is.EqualTo("matched"));
            Assert.That((string)amountCorrection["fidelity"], Is.EqualTo("matched"));
            Assert.That((string)((JArray)countCorrection["admittedFields"])[0]["mode"], Is.EqualTo("TargetTotal"));
            Assert.That((string)((JArray)amountCorrection["admittedFields"])[0]["mode"], Is.EqualTo("TargetTotal"));
            Assert.That((int)countCorrection["providerCalls"], Is.EqualTo(1));
            Assert.That((int)amountCorrection["providerCalls"], Is.EqualTo(1));
            Assert.That((string)additionalFood["status"], Is.EqualTo("observed"));
            Assert.That((string)additionalFood["fidelity"], Is.EqualTo("matched"));
            Assert.That((int?)additionalFood["resourceProgress"], Is.EqualTo(400));
            Assert.That((long?)additionalFood["gatheredFoodIncome"], Is.GreaterThan(400));
            Assert.That((bool?)additionalFood["ordinaryFoodGatherActive"], Is.True);
            Assert.That((string)countCorrection["requestId"], Does.StartWith("runtime:"));
            Assert.That((string)amountCorrection["requestId"], Does.StartWith("runtime:"));
            Assert.That((string)cancelled["uiState"], Is.EqualTo("Ready"));
            Assert.That(((JArray)cancelled["normalizedFields"]), Is.Empty);
            Assert.That((string)cancelled["requestId"], Does.StartWith("runtime:"));
            Assert.That((bool?)mine["packAccepted"], Is.True);
            Assert.That((int?)mine["estimatedInputTokens"], Is.GreaterThan(0));
            Assert.That((int?)garrison["garrisonHousedCount"], Is.EqualTo(1));
            Assert.That((int?)garrison["garrisonProtectedCount"], Is.EqualTo(1));
            Assert.That((int?)garrison["garrisonUnaccommodatedCount"], Is.EqualTo(1));
        }

        // The provider is substituted only at the external semantic boundary. Every turn still
        // goes through the production CommanderChatUI and its normal admission/goal path.
        private sealed class CorpusProvider : ICommanderAIProvider, ICommanderSemanticProvider
        {
            private readonly Queue<string> responses;
            internal int Calls;
            internal CommanderSemanticResult LastResult;
            internal CommanderSemanticProviderRequest LastRequest;
            internal long FirstCallTicks;
            internal long LastReturnTicks;
            internal double? ParseMs;
            internal double? PackMs;
            internal int? InputEstimate;
            internal bool PackSucceeded;
            internal CorpusProvider(string response) => responses = new Queue<string>(new[] { response });
            public Task<CommanderSemanticResult> TranslateSemanticAsync(CommanderSemanticProviderRequest request,
                CancellationToken token)
            {
                if (FirstCallTicks == 0) FirstCallTicks = Stopwatch.GetTimestamp();
                LastRequest = request;
                Calls++;
                if (responses.Count == 0) throw new InvalidOperationException("Unexpected second fixture translation.");
                long packStart = Stopwatch.GetTimestamp();
                PackSucceeded = CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
                    out var pack, out _);
                PackMs = Milliseconds(packStart, Stopwatch.GetTimestamp());
                InputEstimate = pack?.EstimatedInputTokens;
                long started = Stopwatch.GetTimestamp();
                LastResult = CommanderSemanticJson.ParseProviderResponse(responses.Dequeue());
                LastReturnTicks = Stopwatch.GetTimestamp();
                ParseMs = Milliseconds(started, LastReturnTicks);
                return Task.FromResult(LastResult);
            }
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request, CancellationToken token) =>
                Task.FromResult(CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure,
                    "Legacy provider path is not part of the corpus."));
        }

        private sealed class CorpusFixture : IDisposable
        {
            internal readonly EconomyFixture Economy = new EconomyFixture();
            internal readonly DiscoveryFixture Discovery;
            internal GameSimulation Simulation => Discovery?.Simulation ?? Economy.Simulation;
            internal CommanderGoalManager Manager => Discovery?.Manager ?? Economy.Manager;
            internal readonly CorpusProvider Provider;
            internal readonly CommanderIntentDispatcher Dispatcher;
            internal readonly CommanderChatUI Chat;
            internal readonly List<string> Commands = new List<string>();
            internal readonly List<string> Native = new List<string>();
            internal long FirstNativeTicks;
            internal int InitialGoalCount;
            internal int InitialCommanderCommandCount;
            internal int HumanTrainCommands;
            internal int? FoodCompletionTick;
            internal long? FoodIncomeAtCompletion;
            internal int? CommanderCommandsAtFoodCompletion;
            internal bool OrdinaryFoodGatherActive;
            internal int OldSpearmanId = -1;
            internal int HumanBirthId = -1;
            internal int[] PatrolSubjects;
            internal bool BuilderAffinityVerified;
            internal bool AssignedBuilderHumanProtected;
            internal bool AssignedBuilderMissing;
            private readonly string caseId;
            private bool houseHumanQueueSeeded;
            private readonly GameObject host;
            internal CorpusFixture(string response, string id = null)
            {
                caseId = id;
                if (id == "finite-discovery-distinct-from-point-scout") Discovery = new DiscoveryFixture();
                Provider = new CorpusProvider(response);
                Dispatcher = new CommanderIntentDispatcher(Simulation, Manager);
                host = new GameObject("Phase5CCorpusProductionChat");
                Chat = host.AddComponent<CommanderChatUI>();
                Chat.Initialize(Provider, Simulation, Manager, Dispatcher);
                Simulation.CommandBuffer.CommandEnqueued += OnCommandEnqueued;
                Simulation.NativeMoveProcessed += OnMoveProcessed;
                Simulation.NativeUnitCommandProcessed += OnUnitCommandProcessed;
            }
            private void OnCommandEnqueued(ICommand command, CommandEnqueueSource source)
            {
                if (source == CommandEnqueueSource.Commander && Commands.Count < 32)
                    Commands.Add(command.GetType().Name);
                if (source == CommandEnqueueSource.Commander && command is PatrolCommand patrol)
                    PatrolSubjects = patrol.UnitIds?.ToArray();
            }
            private void OnMoveProcessed(NativeMoveProcessingReceipt receipt)
            {
                if (FirstNativeTicks == 0) FirstNativeTicks = Stopwatch.GetTimestamp();
                if (Native.Count < 32) Native.Add("Move:" + receipt.Result);
            }
            private void OnUnitCommandProcessed(NativeUnitCommandObservation observation)
            {
                if (FirstNativeTicks == 0) FirstNativeTicks = Stopwatch.GetTimestamp();
                if (Native.Count < 32) Native.Add(observation.ExecutingCommand.GetType().Name + ":processed");
            }
            internal void Seed(string id, bool deleteAssignedBuilder = false)
            {
                if (Discovery != null) return;
                if (id == "mine-yes-original-request")
                {
                    Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                        Economy.Base.x, Economy.Base.y, false, true).AutoProduceVillagers = false;
                    Economy.Worker(0); Economy.Node(ResourceType.Gold, 8);
                }
                else if (id == "house-next-five-from-trees")
                {
                    Economy.Worker(0);
                    var producer = Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                        Economy.Base.x, Economy.Base.y, false, true);
                    producer.AutoProduceVillagers = false;
                    Economy.Node(ResourceType.Wood, 25);
                }
                else if (id.StartsWith("worker-mode-", StringComparison.Ordinal))
                {
                    for (int i = 0; i < 8; i++) Economy.Worker(i);
                    Economy.Node(ResourceType.Food, 8);
                }
                else if (id == "amount-versus-worker-count" || id == "count-correction-only"
                    || id == "amount-correction-only" || id == "additional-gathered-food-bounded-objective")
                {
                    for (int i = 0; i < 6; i++) Economy.Worker(i);
                    Economy.Node(ResourceType.Gold, 14);
                    if (id == "additional-gathered-food-bounded-objective")
                        Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                            Economy.Base.x, Economy.Base.y, false, true).AutoProduceVillagers = false;
                    Economy.Node(ResourceType.Food,
                        id == "additional-gathered-food-bounded-objective" ? 8 : 20);
                }
                else if (id == "all-current-wood-to-berries" || id == "exact-current-wood-to-berries")
                {
                    var wood = Economy.Node(ResourceType.Wood, 15);
                    Economy.Node(ResourceType.Food, 24);
                    for (int i = 0; i < 7; i++) Economy.Assigned(Economy.Worker(i), wood);
                }
                else if (id == "all-wood-west-tc-explicit-move" || id == "all-wood-west-tc-explicit-gather")
                {
                    Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                        Economy.Base.x, Economy.Base.y, false, true).AutoProduceVillagers = false;
                    var oldWood = Economy.Node(ResourceType.Wood, 18);
                    Economy.Simulation.MapData.AddResourceNode(ResourceType.Wood,
                        Economy.Simulation.MapData.TileToWorldFixed(Economy.Base.x - 14, Economy.Base.y + 4), 10000);
                    for (int i = 0; i < 3; i++) Economy.Assigned(Economy.Worker(i), oldWood);
                }
                else if (id == "idle-only-protection" || id == "two-houses-near-town-center"
                    || id == "cancel-reset-late-retry")
                {
                    Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                        Economy.Base.x, Economy.Base.y, false, true).AutoProduceVillagers = false;
                    for (int i = 0; i < 3; i++) Economy.Worker(i);
                    Economy.Node(ResourceType.Gold, 18);
                }
                else if (id == "occupied-farms-and-capacity")
                {
                    var farmA = Economy.Farm(16); var farmB = Economy.Farm(24);
                    Economy.Farm(32); Economy.Farm(40);
                    Economy.Assigned(Economy.Worker(0), farmA);
                    Economy.Assigned(Economy.Worker(1), farmB);
                    for (int i = 2; i < 6; i++) Economy.Worker(i);
                }
                else if (id == "garrison-capacity-protected-remainder")
                {
                    var center = Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                        Economy.Base.x, Economy.Base.y, false, true);
                    center.GarrisonCapacity = 1; center.AutoProduceVillagers = false;
                    var protectedWorker = Economy.Worker(0);
                    for (int i = 1; i < 3; i++) Economy.Worker(i);
                    Economy.Simulation.CommandBuffer.EnqueueCommand(
                        new StopCommand(0, new[] { protectedWorker.Id }), CommandEnqueueSource.Human);
                    Economy.Simulation.CommandBuffer.FlushCommands(); // retain the human protection lease
                }
                else if (id == "ordered-production-exact-new-units")
                {
                    Economy.Simulation.SetPlayerAge(0, 3);
                    Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                        Economy.Base.x, Economy.Base.y, false, true).AutoProduceVillagers = false;
                    Economy.Simulation.CreateBuilding(0, BuildingType.Barracks,
                        Economy.Base.x + 10, Economy.Base.y, false, true);
                    var old = Economy.Simulation.UnitRegistry.CreateUnit(0,
                        Economy.Simulation.MapData.TileToWorldFixed(Economy.Base.x + 9, Economy.Base.y + 7),
                        Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                    old.UnitType = 1; old.MaxHealth = old.CurrentHealth = 100;
                    OldSpearmanId = old.Id;
                }
                else if (id == "defense-arrival-native-receipt")
                {
                    Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                        Economy.Base.x, Economy.Base.y, false, true).AutoProduceVillagers = false;
                    for (int i = 0; i < 6; i++)
                    {
                        var unit = Economy.Simulation.UnitRegistry.CreateUnit(0,
                            Economy.Simulation.MapData.TileToWorldFixed(Economy.Base.x + 10 + i, Economy.Base.y + 8),
                            Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                        unit.UnitType = 1; unit.MaxHealth = unit.CurrentHealth = 100;
                    }
                }
                else if (id == "schema-valid-wrong-provider-result") Economy.Worker(0);
                else if (id == "causal-information-owned-task-facts"
                    || id == "death-versus-takeover" || id == "unattributed-builder-control")
                {
                    Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                        Economy.Base.x, Economy.Base.y, false, true).AutoProduceVillagers = false;
                    if (id == "causal-information-owned-task-facts")
                    {
                        Economy.Simulation.CreateBuilding(0, BuildingType.Barracks,
                            Economy.Base.x + 10, Economy.Base.y, false, true);
                        Economy.Manager.SubmitEnsureUnitCount(1, 4, newProductionCount: 4);
                    }
                    else
                    {
                        Economy.Worker(0);
                        Economy.Manager.SubmitBuildStructure(BuildingType.House, 1);
                    }
                }
                var stock = Economy.Simulation.ResourceManager.GetPlayerResources(0);
                stock.Food = stock.Wood = stock.Gold = stock.Stone = 2000;
                if (id == "amount-versus-worker-count" || id == "count-correction-only"
                    || id == "amount-correction-only") stock.Gold = 0;
                if (id == "causal-information-owned-task-facts") stock.Wood = 0;
                if (id == "unattributed-builder-control") stock.Wood = 0;
                if (id == "ordered-production-exact-new-units")
                {
                    var barracks = Economy.Simulation.BuildingRegistry.GetAllBuildings()
                        .Single(building => building.PlayerId == 0 && building.Type == BuildingType.Barracks);
                    Economy.Simulation.CommandBuffer.EnqueueCommand(
                        new TrainUnitCommand(0, barracks.Id, 1), CommandEnqueueSource.Human);
                    HumanTrainCommands++;
                    Economy.Simulation.Tick(); // native acceptance; the ordinary human birth remains queued first
                }
                if (id == "causal-information-owned-task-facts" || id == "death-versus-takeover"
                    || id == "unattributed-builder-control")
                {
                    Economy.Manager.Tick(0);
                    if (id == "death-versus-takeover")
                    {
                        Economy.Simulation.Tick(); // native placement must create the exact owned foundation first
                        var build = Manager.Goals.OfType<BuildStructureGoal>().Single();
                        var assigned = Economy.Simulation.UnitRegistry.GetAllUnits()
                            .Single(unit => unit.PlayerId == 0 && unit.IsVillager);
                        BuilderAffinityVerified = build.PlacedBuildingId >= 0
                            && build.AttributedBuildingIds.Contains(build.PlacedBuildingId)
                            && build.LastAcceptedBuilderUnitIds.SequenceEqual(new[] { assigned.Id })
                            && assigned.ConstructionTargetBuildingId == build.PlacedBuildingId
                            && Native.Contains("PlaceBuildingCommand:processed");
                        Economy.Simulation.CommandBuffer.EnqueueCommand(
                            deleteAssignedBuilder
                                ? (ICommand)new DeleteUnitsCommand(0, new[] { assigned.Id })
                                : new StopCommand(0, new[] { assigned.Id }),
                            CommandEnqueueSource.Human);
                        Economy.Simulation.Tick(); // native human control, not an injected status flag
                        AssignedBuilderHumanProtected = Manager.ObserveHumanProtection(assigned.Id);
                        AssignedBuilderMissing = Economy.Simulation.UnitRegistry.GetUnit(assigned.Id) == null;
                        Economy.Manager.Tick(15);
                    }
                    if (id == "unattributed-builder-control")
                    {
                        var idle = Economy.Simulation.UnitRegistry.GetAllUnits()
                            .Single(unit => unit.PlayerId == 0 && unit.IsVillager);
                        Economy.Simulation.CommandBuffer.EnqueueCommand(
                            new StopCommand(0, new[] { idle.Id }), CommandEnqueueSource.Human);
                        Economy.Simulation.Tick();
                        Economy.Manager.Tick(15);
                    }
                    InitialGoalCount = Manager.Goals.Count;
                    InitialCommanderCommandCount = Commands.Count;
                }
            }
            internal int TickBounded(int bound)
            {
                for (int i = 0; i < bound; i++)
                {
                    if (i % 15 == 0) Manager.Tick(i);
                    if (caseId == "additional-gathered-food-bounded-objective"
                        && FoodCompletionTick == null
                        && Manager.Goals.OfType<AllocateWorkersGoal>()
                            .Any(goal => goal.Status == CommanderGoalStatus.Completed))
                    {
                        FoodCompletionTick = i;
                        FoodIncomeAtCompletion = Simulation.ResourceManager.GetGatheredIncome(0, ResourceType.Food);
                        CommanderCommandsAtFoodCompletion = Commands.Count;
                    }
                    if (caseId == "house-next-five-from-trees")
                    {
                        var townCenter = Simulation.BuildingRegistry.GetAllBuildings()
                            .FirstOrDefault(building => building.PlayerId == 0
                                && building.Type == BuildingType.TownCenter);
                        if (!houseHumanQueueSeeded && Manager.Goals.OfType<BuildStructureGoal>()
                                .Any(goal => goal.Status == CommanderGoalStatus.Completed))
                        {
                            for (int birth = 0; birth < 5; birth++)
                            {
                                Simulation.CommandBuffer.EnqueueCommand(
                                    new TrainUnitCommand(0, townCenter.Id, 0), CommandEnqueueSource.Human);
                                HumanTrainCommands++;
                            }
                            houseHumanQueueSeeded = true;
                        }
                    }
                    Simulation.Tick(); Discovery?.ObserveAfterNativeTick();
                    if (caseId == "ordered-production-exact-new-units" && HumanBirthId < 0)
                    {
                        var newborn = Simulation.UnitRegistry.GetAllUnits()
                            .Where(unit => unit.PlayerId == 0 && unit.UnitType == 1
                                && unit.CurrentHealth > 0 && unit.Id != OldSpearmanId)
                            .OrderBy(unit => unit.Id).FirstOrDefault();
                        if (newborn != null) HumanBirthId = newborn.Id;
                    }
                }
                if (caseId == "additional-gathered-food-bounded-objective")
                    OrdinaryFoodGatherActive = Simulation.UnitRegistry.GetAllUnits().Any(unit =>
                        unit.PlayerId == 0 && unit.IsVillager && unit.CurrentHealth > 0
                        && unit.TargetResourceNodeId >= 0
                        && Simulation.MapData.GetResourceNode(unit.TargetResourceNodeId)?.Type == ResourceType.Food);
                return bound;
            }
            public void Dispose()
            {
                Simulation.CommandBuffer.CommandEnqueued -= OnCommandEnqueued;
                Simulation.NativeMoveProcessed -= OnMoveProcessed;
                Simulation.NativeUnitCommandProcessed -= OnUnitCommandProcessed;
                if (host != null)
                {
                    if (!Application.isPlaying) typeof(CommanderChatUI).GetMethod("OnDestroy",
                        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Chat, null);
                    UnityEngine.Object.DestroyImmediate(host);
                }
                Dispatcher.Dispose(); Discovery?.Dispose(); Economy.Dispose();
            }
        }

        private sealed class DiscoveryFixture : IDisposable
        {
            private readonly SimulationConfig config;
            internal readonly GameSimulation Simulation;
            internal readonly CommanderGoalManager Manager;
            private readonly bool[,] initiallyUnexplored;
            private readonly BuildingData target;
            private readonly UnitData scout;
            private readonly Vector2Int scoutBaseline;
            internal readonly bool TargetInitiallyHidden;
            internal readonly bool NoOrderControlStable;
            internal bool TargetEverVisibleAfterTravel { get; private set; }
            internal DiscoveryFixture()
            {
                config = ScriptableObject.CreateInstance<SimulationConfig>();
                Simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
                int x = Simulation.MapData.Width / 2, z = Simulation.MapData.Height / 2;
                for (int tx = x - 7; tx <= x + 7; tx++)
                    for (int tz = z - 7; tz <= z + 7; tz++)
                    {
                        Simulation.MapData.Tiles[tx, tz] = TileType.Grass;
                        Simulation.MapData.ForestDensity[tx, tz] = 0;
                        Simulation.MapData.FoundationCount[tx, tz] = 0;
                        Simulation.FogOfWar.SetVisible(0, tx, tz);
                    }
                // Discovery requires an owned Scout, not an owned completed TC. Omitting the TC
                // prevents its radius-20 building vision from substituting for Scout travel.
                scout = Simulation.UnitRegistry.CreateUnit(0, Simulation.MapData.TileToWorldFixed(x - 1, z),
                    Fixed32.One, Fixed32.One, Fixed32.One);
                scout.UnitType = 4; scout.CurrentHealth = scout.MaxHealth = 100; scout.State = UnitState.Idle;
                target = Simulation.CreateBuilding(1, BuildingType.TownCenter, x - 10, z - 9, false, true);
                Simulation.Tick(); // Native owner-vision bootstrap before the observed baseline.
                TargetInitiallyHidden = Simulation.FogOfWar.GetVisibility(0, target.OriginTileX, target.OriginTileZ)
                    == TileVisibility.Unexplored;
                var scoutStart = Simulation.MapData.WorldToTile(scout.SimPosition);
                for (int i = 0; i < 30; i++) Simulation.Tick(); // no Commander/human orders
                var scoutAfterControl = Simulation.MapData.WorldToTile(scout.SimPosition);
                NoOrderControlStable = scoutAfterControl == scoutStart
                    && Simulation.FogOfWar.GetVisibility(0, target.OriginTileX, target.OriginTileZ)
                        == TileVisibility.Unexplored;
                scoutBaseline = scoutAfterControl;
                var stock = Simulation.ResourceManager.GetPlayerResources(0);
                stock.Food = stock.Wood = stock.Gold = stock.Stone = 2000;
                initiallyUnexplored = new bool[Simulation.MapData.Width, Simulation.MapData.Height];
                for (int tx = 0; tx < Simulation.MapData.Width; tx++)
                    for (int tz = 0; tz < Simulation.MapData.Height; tz++)
                        initiallyUnexplored[tx, tz] = Simulation.FogOfWar.GetVisibility(0, tx, tz) == TileVisibility.Unexplored;
                Manager = new CommanderGoalManager(Simulation, 0);
            }
            internal int TravelRevealedCount()
            {
                int count = 0;
                for (int tx = 0; tx < Simulation.MapData.Width; tx++)
                    for (int tz = 0; tz < Simulation.MapData.Height; tz++)
                        if (initiallyUnexplored[tx, tz]
                            && Simulation.FogOfWar.GetVisibility(0, tx, tz) != TileVisibility.Unexplored) count++;
                return count;
            }
            internal bool TargetNowVisible => Simulation.FogOfWar.GetVisibility(0, target.OriginTileX,
                target.OriginTileZ) == TileVisibility.Visible;
            internal void ObserveAfterNativeTick()
            {
                if (Simulation.MapData.WorldToTile(scout.SimPosition) != scoutBaseline && TargetNowVisible)
                    TargetEverVisibleAfterTravel = true;
            }
            public void Dispose() { Manager.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        private static async Task<string> RunPhase5CCorpusAsync(string corpusPath, string directory)
        {
            JObject corpus = JObject.Parse(File.ReadAllText(corpusPath));
            if ((int?)corpus["schemaVersion"] != 1 || (string)corpus["executionState"] != "not-run")
                throw new InvalidOperationException("Unexpected corpus version or execution state.");
            JArray cases = corpus["cases"] as JArray;
            if (cases == null || cases.Count != 25) throw new InvalidOperationException("The bounded corpus must contain 25 cases.");
            string runId = Guid.NewGuid().ToString("D");
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject item in cases)
            {
                string id = (string)item["id"];
                if (!uniqueIds.Add(id)) throw new InvalidOperationException("Duplicate corpus case.");
                var turns = item["turns"] as JArray;
                if (turns == null || turns.Count < 1 || turns.Count > 4)
                    throw new InvalidOperationException("Unbounded corpus turns.");
                string original = (string)turns[0];
                string path = Path.Combine(directory, id + ".json");
                var evidence = new ScenarioEvidenceRecorder(path, runId);
                var row = NewTurn(id, original);
                var turnHistory = (JArray)row["turnHistory"];
                string correlationId = "case:" + id;
                evidence.RecordPhase5CTurn(row); // durable before any provider/UI work
                if (!TryResponse(id, out string response))
                {
                    row["status"] = "open-gap";
                    row["gap"] = "No independent synthetic provider fixture or seeded native setup is declared for this case.";
                    row["errorStage"] = "fixture-coverage";
                    row["errorCategory"] = "not-executed";
                    evidence.RecordPhase5CTurn(row);
                    continue;
                }
                long started = Stopwatch.GetTimestamp();
                try
                {
                    using (var fixture = new CorpusFixture(response, id))
                    {
                        fixture.Seed(id);
                        row["fixtureKind"] = id == "malformed-provider-json" ? "malformed"
                            : id == "schema-valid-wrong-provider-result" ? "schema-valid-wrong" : "correct-synthetic";
                        for (int i = 0; i < turns.Count; i++)
                        {
                            string turn = (string)turns[i];
                            var before = Missing(fixture.Chat.PendingClarification);
                            var beforeFields = fixture.Chat.PendingClarification?.GraphDraft == null
                                ? new JArray() : ProjectGraphDraft(fixture.Chat.PendingClarification.GraphDraft);
                            bool awaitingApproval = fixture.Chat.PendingActionPlan != null;
                            row["turnIndex"] = i;
                            row["turnText"] = turn;
                            row["draftBefore"] = before;
                            long turnStart = Stopwatch.GetTimestamp();
                            int callsBefore = fixture.Provider.Calls;
                            CommanderAIChatSubmission submission = await fixture.Chat.SubmitMessageAsync(turn);
                            row["uiSubmitMs"] = Milliseconds(turnStart, Stopwatch.GetTimestamp());
                            row["draftAfter"] = Missing(fixture.Chat.PendingClarification);
                            var afterFields = fixture.Chat.PendingClarification?.GraphDraft == null
                                ? new JArray() : ProjectGraphDraft(fixture.Chat.PendingClarification.GraphDraft);
                            row["draftChanges"] = DraftDelta(before, (JArray)row["draftAfter"], beforeFields, afterFields);
                            row["providerCalls"] = fixture.Provider.Calls;
                            row["packAccepted"] = fixture.Provider.Calls == 0 ? JValue.CreateNull()
                                : (JToken)fixture.Provider.PackSucceeded;
                            CommanderSemanticResult observedSemantic = fixture.Chat.PendingActionPlan?.Interpretation
                                ?? (fixture.Provider.Calls > callsBefore ? fixture.Provider.LastResult : null);
                            row["semanticValid"] = observedSemantic?.IsValid;
                            row["semanticOutcome"] = observedSemantic?.Outcome.ToString()
                                ?? (submission?.Success == true ? "Request" : null);
                            if (observedSemantic != null) row["normalizedFields"] = NormalizeCorpus(observedSemantic);
                            else if (fixture.Chat.PendingClarification?.GraphDraft != null)
                                row["normalizedFields"] = ProjectGraphDraft(fixture.Chat.PendingClarification.GraphDraft);
                            else row["normalizedFields"] = new JArray();
                            row["admittedFields"] = AdmittedFields(fixture);
                            row["ownedTaskFactCount"] = OwnedTaskFactCount(fixture.Provider.LastRequest);
                            row["readOnlyProviderRequest"] = fixture.Provider.LastRequest?.IsReadOnlyQuestion;
                            row["questionFactClass"] = QuestionFactClass(fixture.Provider.LastRequest);
                            if (id == "death-versus-takeover")
                            {
                                row["builderAffinityVerified"] = fixture.BuilderAffinityVerified;
                                row["assignedBuilderHumanProtected"] = fixture.AssignedBuilderHumanProtected;
                            }
                            row["approvalRequired"] = fixture.Chat.PendingActionPlan != null;
                            if (id == "ordered-production-exact-new-units")
                            {
                                row["fixtureAssumption"] = "The utterance omits patrol destination; this valid offline fixture supplies the bounded owned-base PlayerBase selector.";
                                row["previewMentionsOwnedBase"] = fixture.Chat.PendingActionPlan?.Preview?
                                    .IndexOf("base", StringComparison.OrdinalIgnoreCase) >= 0;
                            }
                            row["approved"] = awaitingApproval && fixture.Chat.PendingActionPlan == null
                                && fixture.Manager.Goals.Count > 0;
                            string observedCorrelation = RequestCorrelation(fixture, id);
                            if (observedCorrelation != "case:" + id) correlationId = observedCorrelation;
                            row["requestId"] = correlationId;
                            row["uiState"] = LatestUiState(fixture.Chat);
                            row["packingMs"] = fixture.Provider.Calls > callsBefore && fixture.Provider.PackMs.HasValue
                                ? (JToken)fixture.Provider.PackMs.Value : JValue.CreateNull();
                            row["estimatedInputTokens"] = fixture.Provider.InputEstimate.HasValue
                                ? (JToken)fixture.Provider.InputEstimate.Value : JValue.CreateNull();
                            row["parseMs"] = fixture.Provider.Calls > callsBefore && fixture.Provider.ParseMs.HasValue
                                ? (JToken)fixture.Provider.ParseMs.Value : JValue.CreateNull();
                            row["admissionMs"] = fixture.Provider.Calls == callsBefore || fixture.Provider.LastReturnTicks < turnStart ? JValue.CreateNull()
                                : (JToken)Milliseconds(fixture.Provider.LastReturnTicks, Stopwatch.GetTimestamp());
                            row["commandCount"] = fixture.Commands.Count;
                            row["commandTypes"] = new JArray(fixture.Commands);
                            row["nativeResults"] = new JArray(fixture.Native);
                            row["taskStates"] = GoalStates(fixture);
                            row["status"] = "observed";
                            turnHistory.Add(new JObject { ["index"] = i, ["text"] = turn,
                                ["requestId"] = row["requestId"].DeepClone(),
                                ["uiState"] = row["uiState"].DeepClone(),
                                ["providerCalls"] = fixture.Provider.Calls,
                                ["packAccepted"] = fixture.Provider.Calls > callsBefore
                                    ? (JToken)fixture.Provider.PackSucceeded : JValue.CreateNull(),
                                ["estimatedInputTokens"] = fixture.Provider.Calls > callsBefore && fixture.Provider.InputEstimate.HasValue
                                    ? (JToken)fixture.Provider.InputEstimate.Value : JValue.CreateNull(),
                                ["semanticValid"] = row["semanticValid"].DeepClone(),
                                ["semanticOutcome"] = row["semanticOutcome"].DeepClone(),
                                ["normalizedFields"] = row["normalizedFields"].DeepClone(),
                                ["admittedFields"] = row["admittedFields"].DeepClone(),
                                ["draftBefore"] = before.DeepClone(),
                                ["draftAfter"] = row["draftAfter"].DeepClone(),
                                ["draftChanges"] = row["draftChanges"].DeepClone(),
                                ["approvalRequired"] = row["approvalRequired"].DeepClone(),
                                ["approved"] = row["approved"].DeepClone(),
                                ["commandTypes"] = row["commandTypes"].DeepClone(),
                                ["taskStates"] = row["taskStates"].DeepClone(),
                                ["uiSubmitMs"] = row["uiSubmitMs"].DeepClone(),
                                ["packingMs"] = row["packingMs"].DeepClone(),
                                ["transportMs"] = JValue.CreateNull(),
                                ["parseMs"] = row["parseMs"].DeepClone(),
                                ["admissionMs"] = row["admissionMs"].DeepClone() });
                            evidence.RecordPhase5CTurn(row); // atomic checkpoint after every declared turn
                        }
                        if (fixture.Chat.PendingActionPlan != null && turns.Count == 1)
                        {
                            string correlation = correlationId;
                            long approvalStart = Stopwatch.GetTimestamp();
                            await fixture.Chat.SubmitMessageAsync("Approve plan");
                            row["requestId"] = correlation;
                            row["approvalMode"] = "offline-fixture-control-not-declared-player-turn";
                            row["approved"] = fixture.Chat.PendingActionPlan == null
                                && fixture.Manager.Goals.Count > 0;
                            row["admittedFields"] = AdmittedFields(fixture);
                            row["uiState"] = LatestUiState(fixture.Chat);
                            row["taskStates"] = GoalStates(fixture);
                            turnHistory.Add(new JObject { ["index"] = turns.Count, ["text"] = "Approve plan",
                                ["origin"] = "offline-fixture-control", ["requestId"] = correlation,
                                ["uiState"] = row["uiState"].DeepClone(),
                                ["providerCalls"] = fixture.Provider.Calls, ["approved"] = row["approved"].DeepClone(),
                                ["admittedFields"] = row["admittedFields"].DeepClone(),
                                ["taskStates"] = row["taskStates"].DeepClone(),
                                ["uiSubmitMs"] = Milliseconds(approvalStart, Stopwatch.GetTimestamp()) });
                            evidence.RecordPhase5CTurn(row);
                        }
                        int tickBound = id == "malformed-provider-json" ? 0
                            : id == "causal-information-owned-task-facts" || id == "death-versus-takeover" ? 0
                            : id == "mine-yes-original-request" ? 1500
                            : id == "two-houses-near-town-center" ? 6000
                            : id == "house-next-five-from-trees" ? Math.Min(8000,
                                fixture.Simulation.Config.HouseConstructionTicks
                                + 5 * fixture.Simulation.Config.VillagerTrainTimeTicks + 4000)
                            : id == "additional-gathered-food-bounded-objective" ? 7000
                            : id == "all-wood-west-tc-explicit-move" ? 1500
                            : id == "defense-arrival-native-receipt" ? 1500
                            : id == "garrison-capacity-protected-remainder" ? 500
                            : id == "finite-discovery-distinct-from-point-scout" ? 1500
                            : id == "ordered-production-exact-new-units" ? Math.Min(8000,
                                4 * fixture.Simulation.Config.SpearmanTrainTimeTicks + 4000) : 60;
                        int ticks = fixture.TickBounded(tickBound);
                        row["tickMode"] = ticks == 0 ? "not-run" : "accelerated-fixture";
                        row["ticks"] = ticks;
                        row["commandCount"] = fixture.Commands.Count;
                        row["commandTypes"] = new JArray(fixture.Commands);
                        row["nativeResults"] = new JArray(fixture.Native);
                        row["taskStates"] = GoalStates(fixture);
                        row["fogNewlyExploredCount"] = fixture.Discovery == null ? JValue.CreateNull()
                            : (JToken)fixture.Discovery.TravelRevealedCount();
                        var garrison = fixture.Manager.Goals.OfType<CommanderCapabilityGoal>()
                            .FirstOrDefault(goal => goal.Action.ActionType == CommanderCapabilityActionType.GarrisonUnits);
                        row["garrisonHousedCount"] = garrison?.GarrisonShelter == null ? JValue.CreateNull()
                            : (JToken)garrison.GarrisonShelter.GarrisonCount;
                        row["garrisonProtectedCount"] = garrison == null ? JValue.CreateNull()
                            : (JToken)garrison.GarrisonProtectedCount;
                        row["garrisonUnaccommodatedCount"] = garrison == null ? JValue.CreateNull()
                            : (JToken)garrison.GarrisonUnaccommodatedCount;
                        var future = fixture.Manager.Goals.OfType<WatchFutureUnitsGoal>().FirstOrDefault();
                        row["futureObservedCount"] = future == null ? JValue.CreateNull()
                            : (JToken)future.ObservedCount;
                        row["futureAssignedCount"] = future == null ? JValue.CreateNull()
                            : (JToken)future.AssignedCount;
                        row["humanTrainCommands"] = id == "house-next-five-from-trees"
                            || id == "ordered-production-exact-new-units"
                            ? (JToken)fixture.HumanTrainCommands : JValue.CreateNull();
                        if (id == "ordered-production-exact-new-units")
                        {
                            var resultIds = fixture.Manager.Goals.OfType<EnsureUnitCountGoal>()
                                .SingleOrDefault()?.ResultUnitIds ?? Array.Empty<int>();
                            row["newResultCount"] = resultIds.Count;
                            row["oldUnitExcluded"] = fixture.OldSpearmanId >= 0
                                && !resultIds.Contains(fixture.OldSpearmanId);
                            row["humanBirthExcluded"] = fixture.HumanBirthId >= 0
                                && !resultIds.Contains(fixture.HumanBirthId);
                            row["exactPatrolBinding"] = fixture.PatrolSubjects != null
                                && resultIds.Count == 2
                                && fixture.PatrolSubjects.OrderBy(value => value)
                                    .SequenceEqual(resultIds.OrderBy(value => value));
                        }
                        var resourceGoal = fixture.Manager.Goals.OfType<AllocateWorkersGoal>().FirstOrDefault();
                        row["resourceProgress"] = resourceGoal == null ? JValue.CreateNull()
                            : (JToken)resourceGoal.ResourceProgress;
                        row["gatheredFoodIncome"] = id == "additional-gathered-food-bounded-objective"
                            ? (JToken)fixture.Simulation.ResourceManager.GetGatheredIncome(0, ResourceType.Food)
                            : JValue.CreateNull();
                        row["foodCompletionTick"] = fixture.FoodCompletionTick.HasValue
                            ? (JToken)fixture.FoodCompletionTick.Value : JValue.CreateNull();
                        row["foodIncomeAtCompletion"] = fixture.FoodIncomeAtCompletion.HasValue
                            ? (JToken)fixture.FoodIncomeAtCompletion.Value : JValue.CreateNull();
                        row["commanderCommandsAtFoodCompletion"] = fixture.CommanderCommandsAtFoodCompletion.HasValue
                            ? (JToken)fixture.CommanderCommandsAtFoodCompletion.Value : JValue.CreateNull();
                        row["ordinaryFoodGatherActive"] = id == "additional-gathered-food-bounded-objective"
                            ? (JToken)fixture.OrdinaryFoodGatherActive : JValue.CreateNull();
                        row["firstNativeActionMs"] = fixture.FirstNativeTicks == 0 ? JValue.CreateNull()
                            : (JToken)Milliseconds(started, fixture.FirstNativeTicks);
                        EvaluateFixture(id, fixture, row);
                        evidence.RecordPhase5CTurn(row);
                    }
                }
                catch (Exception error)
                {
                    row["status"] = "failed";
                    row["errorStage"] = "fixture-runner";
                    row["errorCategory"] = error.GetType().Name;
                    row["gap"] = "First integrated fixture failure; inspect focused NUnit output and the preserved case artifact.";
                    try { evidence.RecordPhase5CTurn(row); }
                    catch (InvalidOperationException)
                    {
                        var boundedFailure = NewTurn(id, original);
                        boundedFailure["status"] = "failed";
                        boundedFailure["errorStage"] = "fixture-runner";
                        boundedFailure["errorCategory"] = error.GetType().Name;
                        boundedFailure["gap"] = "The richer record exceeded the evidence allow-list or size bound; inspect the earlier atomic checkpoint.";
                        evidence.RecordPhase5CTurn(boundedFailure);
                    }
                }
            }
            return directory;
        }

        private static JObject NewTurn(string id, string original) => new JObject
        {
            ["schemaVersion"] = 1, ["caseId"] = id, ["requestId"] = "case:" + id,
            ["utterance"] = original, ["turnIndex"] = 0, ["status"] = "started",
            ["evidenceMode"] = "offline-production-chat-fixture", ["providerCalls"] = 0,
            ["httpAttempts"] = 0, ["packAccepted"] = JValue.CreateNull(),
            ["fidelity"] = "not-evaluated", ["semanticValid"] = JValue.CreateNull(),
            ["estimatedInputTokens"] = JValue.CreateNull(), ["actualPromptTokens"] = JValue.CreateNull(),
            ["actualCompletionTokens"] = JValue.CreateNull(), ["actualCachedTokens"] = JValue.CreateNull(),
            ["actualReasoningTokens"] = JValue.CreateNull(), ["reportedCost"] = JValue.CreateNull(),
            ["attemptCosts"] = new JArray(), ["packingMs"] = JValue.CreateNull(),
            ["transportMs"] = JValue.CreateNull(), ["parseMs"] = JValue.CreateNull(),
            ["admissionMs"] = JValue.CreateNull(), ["firstNativeActionMs"] = JValue.CreateNull(),
            ["commandCount"] = 0, ["commandTypes"] = new JArray(), ["nativeResults"] = new JArray(),
            ["taskStates"] = new JArray(), ["draftBefore"] = new JArray(), ["draftAfter"] = new JArray(),
            ["draftChanges"] = new JArray(), ["normalizedFields"] = new JArray(),
            ["admittedFields"] = new JArray(), ["turnHistory"] = new JArray(),
            ["fogNewlyExploredCount"] = JValue.CreateNull(),
            ["garrisonHousedCount"] = JValue.CreateNull(), ["garrisonProtectedCount"] = JValue.CreateNull(),
            ["garrisonUnaccommodatedCount"] = JValue.CreateNull(),
            ["futureObservedCount"] = JValue.CreateNull(), ["futureAssignedCount"] = JValue.CreateNull(),
            ["humanTrainCommands"] = JValue.CreateNull(),
            ["resourceProgress"] = JValue.CreateNull(), ["gatheredFoodIncome"] = JValue.CreateNull(),
            ["foodCompletionTick"] = JValue.CreateNull(), ["foodIncomeAtCompletion"] = JValue.CreateNull(),
            ["commanderCommandsAtFoodCompletion"] = JValue.CreateNull(),
            ["ordinaryFoodGatherActive"] = JValue.CreateNull(),
            ["ownedTaskFactCount"] = JValue.CreateNull(), ["readOnlyProviderRequest"] = JValue.CreateNull(),
            ["questionFactClass"] = JValue.CreateNull()
        };

        private static bool TryResponse(string id, out string response)
        {
            response = null;
            if (id == "mine-yes-original-request")
            {
                var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
                    ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 },
                    ["placement"] = new JObject { ["anchor"] = "VisibleResource", ["relation"] = "Near",
                        ["resource"] = "Gold", ["sourceKind"] = "GoldMine" } };
                response = GraphDraft(new JArray(mine), "Independent", 0, "structure", "Confirmation", new JValue("Mine"));
                return true;
            }
            if (id == "house-next-five-from-trees")
            {
                var house = new JObject { ["type"] = "BuildStructure", ["structure"] = "House", ["count"] = 1,
                    ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
                var watch = new JObject { ["type"] = "WatchFutureUnits", ["unit"] = "Villager", ["count"] = 5,
                    ["producer"] = "TownCenter", ["producerOrdinal"] = 1, ["action"] = "Gather",
                    ["resource"] = "Wood", ["dependsOn"] = new JArray(0) };
                response = GraphDraft(new JArray(house, watch), "Sequential", 1, "sourceKind", "SourceKind", new JValue("Tree"));
                return true;
            }
            if (id == "malformed-provider-json") { response = "{not-json"; return true; }
            if (id == "schema-valid-wrong-provider-result")
            {
                response = Request(AllocationWithAmount("2", "Wood", "Tree", 200, "Stockpile"));
                return true;
            }
            if (id.StartsWith("worker-mode-", StringComparison.Ordinal))
            {
                string mode = id == "worker-mode-additional-four" ? "Additional"
                    : id == "worker-mode-target-total-four" ? "TargetTotal" : "SelectedCount";
                response = Request(AllocationNode(new[] { mode, "Exact", "4", "Any", "", "Food", "Any" }));
                return true;
            }
            if (id == "amount-versus-worker-count")
                response = Request(AllocationWithAmount("3", "Gold", "GoldMine", 200, "Stockpile"));
            else if (id == "count-correction-only" || id == "amount-correction-only")
            {
                var allocation = JObject.Parse(AllocationWithAmount("3", "Gold", "GoldMine", 200, "Stockpile"));
                allocation["mode"] = "TargetTotal";
                ((JObject)allocation["destination"]).Remove("sourceKind");
                response = GraphDraft(new JArray(allocation), "Independent", 0, "destination.sourceKind",
                    "SourceKind", new JValue("GoldMine"));
            }
            else if (id == "all-current-wood-to-berries" || id == "exact-current-wood-to-berries")
                response = Request(AllocationNode(new[] { "SelectedCount",
                    id == "all-current-wood-to-berries" ? "AllMatching" : "Exact",
                    id == "all-current-wood-to-berries" ? "" : "6",
                    "Gathering", "Wood", "Food", "Berries" }));
            else if (id == "additional-gathered-food-bounded-objective")
                response = Request(AllocationWithAmount("4", "Food", "Berries", 400, "AdditionalGathered"));
            else if (id == "idle-only-protection")
                response = Request("{\"type\":\"BuildStructure\",\"structure\":\"House\",\"count\":1,"
                    + "\"builders\":{\"state\":\"IdleOnly\",\"count\":1},"
                    + "\"constraints\":[{\"type\":\"PreferredWorkers\",\"mode\":\"IdleOnly\"}]}");
            else if (id == "all-wood-west-tc-explicit-move" || id == "all-wood-west-tc-explicit-gather")
                response = WestWoodline(id == "all-wood-west-tc-explicit-move");
            else if (id == "two-houses-near-town-center")
                response = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                    + "{\"id\":\"site\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"MyTownCenter\",\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{},\"dependsOn\":[]},"
                    + "{\"id\":\"houses\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:House\",\"count\":2},\"inputs\":{\"location\":\"site\"},\"dependsOn\":[\"site\"]}]}";
            else if (id == "ordered-production-exact-new-units")
                response = "{\"outcome\":\"Request\",\"executionOrder\":\"Sequential\",\"nodes\":["
                    + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":2,\"quantityMode\":\"New\"},"
                    + "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":2,\"location\":\"PlayerBase\",\"dependsOn\":[0],\"resultFromNode\":0}]}";
            else if (id == "occupied-farms-and-capacity")
                response = Request(AllocationNode(new[] { "SelectedCount", "Exact", "4", "Any", "", "Food", "Farm" }));
            else if (id == "defense-arrival-native-receipt")
                response = Request("{\"type\":\"DefendArea\",\"unitSelector\":\"Spearman\",\"count\":6,\"location\":\"PlayerBase\"}");
            else if (id == "garrison-capacity-protected-remainder")
                response = Request("{\"type\":\"GarrisonUnits\",\"unitSelector\":\"Villagers\",\"countMode\":\"AllMatching\",\"location\":\"PlayerBase\",\"structure\":\"TownCenter\"}");
            else if (id == "finite-discovery-distinct-from-point-scout")
                response = Request("{\"type\":\"DiscoverEnemy\",\"unitSelector\":\"Scout\",\"count\":1,\"location\":\"VisibleEnemy\",\"target\":{\"kind\":\"BuildingType\",\"structure\":\"TownCenter\"}}");
            else if (id == "cancel-reset-late-retry")
                response = GraphDraft(new JArray(new JObject { ["type"] = "BuildStructure", ["structure"] = "House", ["count"] = 1,
                    ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } }),
                    "Independent", 0, "structure", "Confirmation", new JValue("House"));
            else if (id == "death-versus-takeover" || id == "causal-information-owned-task-facts")
                response = "{\"outcome\":\"Answer\",\"message\":\"Owned task status is unavailable in this fixture.\"}";
            return response != null;
        }

        private static string AllocationWithAmount(string count, string resource, string source, int amount, string mode)
        {
            string node = AllocationNode(new[] { "SelectedCount", "Exact", count, "Any", "", resource, source });
            return node.Substring(0, node.Length - 1) + ",\"resourceAmount\":" + amount
                + ",\"resourceAmountMode\":\"" + mode + "\"}";
        }

        private static string WestWoodline(bool move) => "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
            + "{\"id\":\"all\",\"mechanic\":\"select-workers\",\"parameters\":{\"state\":\"Gathering\",\"currentResource\":\"Wood\",\"countMode\":\"AllMatching\"},\"inputs\":{},\"dependsOn\":[]},"
            + "{\"id\":\"woodline\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":1,\"resource\":\"Wood\",\"sourceKind\":\"Tree\"},\"inputs\":{},\"dependsOn\":[]},"
            + (move
                ? "{\"id\":\"effect\",\"mechanic\":\"move-workers\",\"parameters\":{},"
                : "{\"id\":\"effect\",\"mechanic\":\"allocate-workers\",\"parameters\":{\"resource\":\"Wood\",\"sourceKind\":\"Tree\"},")
            + "\"inputs\":{\"workers\":\"all\",\"location\":\"woodline\"},\"dependsOn\":[\"all\",\"woodline\"]}]}";

        private static JArray Missing(CommanderPendingClarification pending)
        {
            if (pending?.GraphDraft != null)
                return new JArray(pending.GraphDraft.MissingSlots.Select(slot => slot.Node + ":" + slot.Path + ":" + slot.Type));
            if (pending?.Draft != null)
                return new JArray(pending.Draft.MissingFields.Select(field => field.ToString()));
            return new JArray();
        }
        private static JArray DraftDelta(JArray before, JArray after, JArray beforeFields, JArray afterFields)
        {
            var changes = new JArray(before.Values<string>().Except(after.Values<string>(), StringComparer.Ordinal));
            for (int i = 1; i < Math.Min(beforeFields.Count, afterFields.Count); i++)
            {
                if (!(beforeFields[i] is JObject original) || !(afterFields[i] is JObject current)) continue;
                foreach (var field in original.Properties())
                    if (field.Name != "dependencies" && !JToken.DeepEquals(field.Value, current[field.Name]))
                        changes.Add("node" + (i - 1) + "." + field.Name + ":"
                            + field.Value.ToString(Formatting.None) + "->" + current[field.Name]?.ToString(Formatting.None));
            }
            return changes;
        }
        private static JArray NormalizeCorpus(CommanderSemanticResult result)
        {
            var fields = new JArray();
            if (result == null || !result.IsValid) return fields;
            foreach (var node in result.Nodes.Take(4))
                fields.Add(new JObject { ["type"] = node.Type.ToString(), ["building"] = node.BuildingType?.ToString(),
                    ["unit"] = node.UnitType, ["resource"] = node.ResourceType?.ToString(), ["count"] = node.Count,
                    ["quantityMode"] = node.QuantityMode.ToString(), ["anchor"] = node.PlacementAnchorSelector?.ToString(),
                    ["anchorOrdinal"] = node.PlacementAnchorOrdinal, ["relation"] = node.PlacementRelation?.ToString(),
                    ["placementSource"] = node.SourceKind?.ToString(), ["unitSelector"] = node.UnitSelector?.ToString(),
                    ["locationSelector"] = node.LocationSelector?.ToString(),
                    ["producerOrdinal"] = node.ProducerOrdinal, ["producerFrom"] = node.ProducerFromNode,
                    ["resultFrom"] = node.ResultFromNode, ["futureAction"] = node.FutureAction?.ToString(),
                    ["workerMode"] = node.WorkerAllocation?.Mode.ToString(),
                    ["workerCountMode"] = node.WorkerAllocation?.CountMode.ToString(),
                    ["workerCount"] = node.WorkerAllocation?.Count,
                    ["workerState"] = node.WorkerAllocation?.Workers.State.ToString(),
                    ["workerCurrentResource"] = node.WorkerAllocation?.Workers.CurrentResource?.ToString(),
                    ["destinationResource"] = node.WorkerAllocation?.Destination.Resource.ToString(),
                    ["destinationSource"] = node.WorkerAllocation?.Destination.SourceKind.ToString(),
                    ["resourceAmount"] = node.WorkerAllocation?.ResourceAmount,
                    ["resourceAmountMode"] = node.WorkerAllocation?.ResourceAmountMode?.ToString(),
                    ["dependencies"] = new JArray(node.DependsOn) });
            if (result.PendingGraphDraft != null)
            {
                foreach (var field in ProjectGraphDraft(result.PendingGraphDraft)) fields.Add(field);
            }
            if (result.DynamicPlan != null)
            {
                var program = result.DynamicPlan.Nodes.ToList();
                foreach (var step in program.Take(12))
                {
                    var parameters = new JObject();
                    foreach (var field in step.Primitive.Parameters)
                        if (step.Parameters.TryGetValue(field.Name, out object value))
                            parameters[field.Name == "clearGapTiles" ? "clearGap" : field.Name] = value is int count ? (JToken)count
                                : value is string name && name.Length <= 64 ? (JToken)name : "unavailable";
                    var inputs = new JObject();
                    foreach (var input in step.Inputs)
                        inputs[input.Key] = program.FindIndex(node => node.Id == input.Value);
                    fields.Add(new JObject { ["mechanic"] = step.Primitive.Mechanic.ToString(),
                        ["parameters"] = parameters, ["inputs"] = inputs,
                        ["dependencies"] = new JArray(step.DependsOn.Select(dep => program.FindIndex(node => node.Id == dep))) });
                }
            }
            return fields;
        }
        private static JArray ProjectGraphDraft(CommanderRequestGraphDraft draft)
        {
            var fields = new JArray { new JObject { ["draftNodeCount"] = draft.NodeCount,
                ["missingSlots"] = MissingSlots(draft) } };
            var pendingNodes = (JArray)draft.ToPendingJson()["nodes"];
            foreach (JObject node in pendingNodes.Take(4))
                fields.Add(new JObject { ["draftType"] = (string)node["type"],
                        ["structure"] = (string)node["structure"], ["count"] = (int?)node["count"],
                        ["resource"] = (string)node["resource"],
                        ["resourceAmount"] = (int?)node["resourceAmount"],
                        ["resourceAmountMode"] = (string)node["resourceAmountMode"],
                        ["workerMode"] = (string)node["mode"], ["countMode"] = (string)node["countMode"],
                        ["workerState"] = (string)node["workers"]?["state"],
                        ["workerCurrentResource"] = (string)node["workers"]?["currentResource"],
                        ["destinationResource"] = (string)node["destination"]?["resource"],
                        ["destinationSource"] = (string)node["destination"]?["sourceKind"],
                        ["producerOrdinal"] = (int?)node["producerOrdinal"],
                        ["dependencies"] = node["dependsOn"] is JArray dependencies
                            ? new JArray(dependencies.Values<int>()) : new JArray() });
            return fields;
        }
        private static JArray AdmittedFields(CorpusFixture fixture)
        {
            var admitted = new JArray();
            foreach (var goal in fixture.Manager.Goals.Take(12))
            {
                var projection = new JObject { ["type"] = goal.GetType().Name, ["status"] = goal.Status.ToString(),
                    ["originalUtterance"] = goal.RequestAuthority?.OriginalInput };
                if (goal is BuildStructureGoal build)
                {
                    projection["building"] = build.StructureType.ToString(); projection["count"] = build.Count;
                    projection["anchor"] = build.PlacementAnchorSelector?.ToString();
                    projection["anchorOrdinal"] = build.PlacementAnchorOrdinal;
                    projection["relation"] = build.PlacementRelation?.ToString();
                    projection["resource"] = build.PlacementResourceType?.ToString();
                    projection["source"] = build.PlacementSourceKind?.ToString();
                }
                if (goal is AllocateWorkersGoal workers)
                {
                    var allocation = workers.Allocation;
                    projection["mode"] = allocation.Mode.ToString(); projection["countMode"] = allocation.CountMode.ToString();
                    projection["count"] = allocation.Count; projection["state"] = allocation.Workers.State.ToString();
                    projection["currentResource"] = allocation.Workers.CurrentResource?.ToString();
                    projection["destinationResource"] = allocation.Destination.Resource.ToString();
                    projection["destinationSource"] = allocation.Destination.SourceKind.ToString();
                    projection["resourceAmount"] = allocation.ResourceAmount;
                    projection["resourceAmountMode"] = allocation.ResourceAmountMode?.ToString();
                }
                admitted.Add(projection);
            }
            return admitted;
        }
        private static JArray MissingSlots(CommanderRequestGraphDraft draft)
            => new JArray(draft.MissingSlots.Select(slot => slot.Node + ":" + slot.Path + ":" + slot.Type));
        private static JArray GoalStates(CorpusFixture fixture)
            => new JArray(fixture.Manager.Goals.Take(16).Select(goal =>
                goal.GetType().Name + ":" + goal.Status));
        private static string LatestUiState(CommanderChatUI chat) =>
            (string)typeof(CommanderChatUI).GetField("latestRequestState",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(chat);
        private static string RequestCorrelation(CorpusFixture fixture, string id)
        {
            long request = fixture.Chat.PendingClarification?.RequestId
                ?? fixture.Chat.PendingActionPlan?.RequestId
                ?? fixture.Manager.Goals.FirstOrDefault()?.RequestAuthority?.RequestId ?? 0;
            return request > 0 ? "runtime:" + request : "case:" + id;
        }
        private static int? OwnedTaskFactCount(CommanderSemanticProviderRequest request)
        {
            if (request == null) return null;
            try { return (JObject.Parse(request.SerializedContext)["tacticalStatus"]?["requests"] as JArray)?.Count; }
            catch (Newtonsoft.Json.JsonException) { return null; }
        }
        private static string QuestionFactClass(CommanderSemanticProviderRequest request)
        {
            if (request == null || !request.IsReadOnlyQuestion) return null;
            string facts = request.QuestionFacts ?? string.Empty;
            if (facts.IndexOf("wood", StringComparison.OrdinalIgnoreCase) >= 0) return "wood-shortage";
            if (facts.IndexOf("dead or missing", StringComparison.OrdinalIgnoreCase) >= 0) return "dead-or-missing";
            if (facts.IndexOf("human", StringComparison.OrdinalIgnoreCase) >= 0) return "human-override";
            if (facts.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0) return "unavailable";
            return string.IsNullOrEmpty(facts) ? "empty" : "other-owned-status";
        }
        private static void EvaluateFixture(string id, CorpusFixture fixture, JObject row)
        {
            if (fixture.Provider.Calls > 0 && !fixture.Provider.PackSucceeded)
            {
                row["status"] = "open-gap"; row["fidelity"] = "not-evaluable";
                row["errorStage"] = "packing"; row["errorCategory"] = "offline-request-pack-rejected";
                return;
            }
            bool oneCall = fixture.Provider.Calls == 1;
            bool noEffect = fixture.Manager.Goals.Count == 0 && fixture.Commands.Count == 0;
            string states = string.Join("|", fixture.Manager.Goals.Select(goal => goal.GetType().Name + ":" + goal.Status));
            bool hasNative(string name) => fixture.Native.Any(entry => entry.StartsWith(name, StringComparison.Ordinal));
            bool hasCommand(string name) => fixture.Commands.Contains(name);
            void Mark(bool success, string stage, string category, bool terminal = true)
            {
                row["fidelity"] = success ? "matched" : "mismatch";
                if (success) return;
                row["status"] = terminal ? "observed" : "open-gap";
                row["errorStage"] = stage;
                row["errorCategory"] = category;
            }
            if (id == "malformed-provider-json")
            {
                bool rejected = oneCall && fixture.Provider.LastResult?.IsValid == false && noEffect;
                row["fidelity"] = "not-evaluable";
                row["errorStage"] = "parse";
                row["errorCategory"] = rejected ? "malformed-rejected" : "malformed-unexpected-effect";
                return;
            }
            if (id == "schema-valid-wrong-provider-result")
            {
                row["semanticValid"] = fixture.Provider.LastResult?.IsValid;
                row["fidelity"] = "wrong";
                row["errorStage"] = "semantic-fidelity";
                row["errorCategory"] = "schema-valid-wrong-action";
                row["gap"] = "Synthetic Wood/count-2 result differs from declared Gold/count-3/amount-200 request; actual admission is recorded separately.";
                return;
            }
            if (id == "cancel-reset-late-retry")
            {
                Mark(oneCall && noEffect && fixture.Chat.PendingClarification == null
                    && fixture.Chat.PendingActionPlan == null && LatestUiState(fixture.Chat) == "Ready",
                    "local-continuation", "stale-proposal-effect");
                return;
            }
            if (id == "count-correction-only" || id == "amount-correction-only")
            {
                var goal = fixture.Manager.Goals.OfType<AllocateWorkersGoal>().SingleOrDefault();
                var admitted = goal?.Allocation;
                Mark(oneCall && fixture.Chat.PendingClarification == null && goal != null
                    && admitted.Mode.ToString() == "TargetTotal"
                    && admitted.CountMode.ToString() == "Exact"
                    && admitted.Count == (id == "count-correction-only" ? 4 : 3)
                    && admitted.ResourceAmount == (id == "amount-correction-only" ? 250 : 200)
                    && admitted.ResourceAmountMode.ToString() == "Stockpile"
                    && admitted.Destination.Resource == ResourceType.Gold
                    && admitted.Destination.SourceKind == ResourceSourceKind.GoldMine
                    && goal.RequestAuthority?.OriginalInput == (string)row["utterance"],
                    "local-continuation", "typed-correction-or-admission-not-retained");
                return;
            }
            if (id == "death-versus-takeover" || id == "causal-information-owned-task-facts")
            {
                bool grounded = (string)row["questionFactClass"] ==
                    (id == "causal-information-owned-task-facts" ? "wood-shortage" : "human-override");
                Mark(oneCall && fixture.Manager.Goals.Count == fixture.InitialGoalCount
                    && fixture.Commands.Count == fixture.InitialCommanderCommandCount
                    && fixture.Provider.LastResult?.Outcome == CommanderSemanticOutcome.Answer
                    && fixture.Provider.LastRequest?.IsReadOnlyQuestion == true
                    && (int?)row["ownedTaskFactCount"] > 0 && grounded
                    && (id != "death-versus-takeover" || fixture.BuilderAffinityVerified
                        && fixture.AssignedBuilderHumanProtected),
                    "read-only-answer", "owned-task-cause-not-observed", terminal: false);
                if (!grounded) row["gap"] = "Owned task facts were seeded, but this fixture did not prove the requested native cause.";
                return;
            }
            if (id == "mine-yes-original-request")
            {
                var goal = fixture.Manager.Goals.OfType<BuildStructureGoal>().SingleOrDefault();
                Mark(oneCall && goal?.StructureType == BuildingType.Mine
                    && goal.RequestAuthority?.OriginalInput == (string)row["utterance"]
                    && fixture.Chat.PendingClarification == null, "admission", "original-mine-ticket-lost");
                if ((string)row["fidelity"] == "matched" && goal.Status != CommanderGoalStatus.Completed)
                { row["status"] = "open-gap"; row["errorStage"] = "native-placement";
                  row["errorCategory"] = "mine-placement-not-complete"; row["gap"] = states; }
                return;
            }
            if (id == "house-next-five-from-trees")
            {
                var history = (JArray)row["turnHistory"];
                var fields = history.Count > 1
                    ? (JArray)history[1]["normalizedFields"] : (JArray)row["normalizedFields"];
                bool graph = fields.Count >= 2 && (string)fields[0]["type"] == "BuildStructure"
                    && (string)fields[0]["building"] == "House"
                    && (string)fields[1]["type"] == "WatchFutureUnits"
                    && (int?)fields[1]["count"] == 5 && (int?)fields[1]["producerOrdinal"] == 1
                    && (string)fields[1]["resource"] == "Wood"
                    && (string)fields[1]["placementSource"] == "Tree"
                    && ((JArray)fields[1]["dependencies"]).Values<int>().SequenceEqual(new[] { 0 });
                Mark(oneCall && graph && (bool?)row["approved"] == true
                    && fixture.Manager.Goals.Count == 2 && hasNative("PlaceBuildingCommand:processed")
                    && (int?)row["humanTrainCommands"] == 5
                    && (int?)row["futureObservedCount"] == 5 && (int?)row["futureAssignedCount"] == 5
                    && fixture.Native.Count(name => name == "GatherCommand:processed") >= 5,
                    "native-future-births", "five-attributed-births-not-complete");
                if ((string)row["fidelity"] == "matched" && !states.Contains("WatchFutureUnitsGoal:Completed"))
                { row["status"] = "open-gap"; row["errorStage"] = "native-future-births";
                  row["errorCategory"] = "five-attributed-births-not-complete"; row["gap"] = states; }
                return;
            }
            if (id == "finite-discovery-distinct-from-point-scout")
            {
                Mark(oneCall && fixture.Discovery != null && fixture.Discovery.TargetInitiallyHidden
                    && fixture.Discovery.NoOrderControlStable && hasCommand("MoveCommand")
                    && hasNative("Move:Complete") && fixture.Discovery.TravelRevealedCount() > 0
                    && fixture.Discovery.TargetEverVisibleAfterTravel,
                    "native-discovery", "travel-fog-not-revealed");
                if ((string)row["fidelity"] == "matched" && !states.Contains("CommanderCapabilityGoal:Completed")
                    && !states.Contains("CommanderCapabilityGoal:Blocked") && !states.Contains("CommanderCapabilityGoal:Failed"))
                { row["status"] = "open-gap"; row["errorStage"] = "native-discovery";
                  row["errorCategory"] = "finite-outcome-not-reached"; row["gap"] = states; }
                return;
            }
            if (id == "ordered-production-exact-new-units")
            {
                var producer = fixture.Manager.Goals.OfType<EnsureUnitCountGoal>().SingleOrDefault();
                var patrol = fixture.Manager.Goals.OfType<CommanderCapabilityGoal>()
                    .SingleOrDefault(goal => goal.Action.ActionType == CommanderCapabilityActionType.PatrolArea);
                Mark(oneCall && fixture.Provider.LastResult?.IsValid == true
                    && (bool?)row["approved"] == true && (bool?)row["previewMentionsOwnedBase"] == true
                    && fixture.HumanTrainCommands == 1 && fixture.HumanBirthId >= 0
                    && producer?.Status == CommanderGoalStatus.Completed && patrol != null
                    && (int?)row["newResultCount"] == 2
                    && (bool?)row["oldUnitExcluded"] == true
                    && (bool?)row["humanBirthExcluded"] == true
                    && (bool?)row["exactPatrolBinding"] == true
                    && hasCommand("TrainUnitCommand") && hasCommand("PatrolCommand")
                    && hasNative("PatrolCommand:processed"),
                    "native-result-binding", "exact-new-patrol-chain-not-observed", terminal: false);
                if ((string)row["fidelity"] != "matched")
                    row["gap"] = "Check ordinary human queue, exactly two Commander-attributed newborn results, owned-base preview, and native patrol subjects independently.";
                return;
            }
            if (id == "idle-only-protection")
            {
                Mark(oneCall && fixture.Provider.LastResult?.IsValid == true
                    && fixture.Manager.Goals.OfType<BuildStructureGoal>().Any(goal => goal.UseIdleWorkersOnly)
                    && hasNative("PlaceBuildingCommand:processed"),
                    "native-construction", "idle-only-selector-or-placement-missing");
                return;
            }
            if (id == "garrison-capacity-protected-remainder")
            {
                var goal = fixture.Manager.Goals.OfType<CommanderCapabilityGoal>().SingleOrDefault();
                Mark(oneCall && goal != null && goal.Status == CommanderGoalStatus.Completed
                    && hasNative("GarrisonCommand:processed") && goal.GarrisonShelter?.GarrisonCount == 1
                    && goal.GarrisonProtectedCount == 1 && goal.GarrisonUnaccommodatedCount == 1,
                    "native-garrison", "capacity-or-protected-remainder-missing");
                return;
            }
            if (id == "occupied-farms-and-capacity")
            {
                var goal = fixture.Manager.Goals.OfType<AllocateWorkersGoal>().SingleOrDefault();
                Mark(oneCall && goal != null && goal.Status == CommanderGoalStatus.Completed
                    && goal.Allocation.Count == 4 && goal.Allocation.Destination.SourceKind == ResourceSourceKind.Farm
                    && fixture.Commands.Count(name => name == "GatherCommand") == 2
                    && fixture.Native.Count(name => name == "GatherCommand:processed") == 2,
                    "native-farm-allocation", "incumbent-or-capacity-count-mismatch");
                return;
            }
            if (id == "amount-versus-worker-count")
            {
                var goal = fixture.Manager.Goals.OfType<AllocateWorkersGoal>().SingleOrDefault();
                Mark(oneCall && goal != null && goal.Allocation.Count == 3
                    && goal.Allocation.ResourceAmount == 200
                    && goal.Allocation.ResourceAmountMode?.ToString() == "Stockpile"
                    && goal.Allocation.Destination.Resource == ResourceType.Gold
                    && goal.Status == CommanderGoalStatus.WaitingForResources
                    && hasNative("GatherCommand:processed"),
                    "native-resource-objective", "amount-worker-slots-collapsed-or-premature-completion");
                return;
            }
            if (id.StartsWith("worker-mode-", StringComparison.Ordinal))
            {
                string expected = id == "worker-mode-additional-four" ? "Additional"
                    : id == "worker-mode-target-total-four" ? "TargetTotal" : "SelectedCount";
                var goal = fixture.Manager.Goals.OfType<AllocateWorkersGoal>().SingleOrDefault();
                Mark(oneCall && goal != null && goal.Allocation.Mode.ToString() == expected
                    && goal.Allocation.CountMode.ToString() == "Exact" && goal.Allocation.Count == 4
                    && goal.Status == CommanderGoalStatus.Completed && hasNative("GatherCommand:processed"),
                    "native-worker-allocation", "quantity-mode-or-four-worker-effect-missing");
                return;
            }
            if (id == "all-current-wood-to-berries" || id == "exact-current-wood-to-berries")
            {
                var goal = fixture.Manager.Goals.OfType<AllocateWorkersGoal>().SingleOrDefault();
                Mark(oneCall && goal != null && goal.Allocation.Mode.ToString() == "SelectedCount"
                    && goal.Allocation.CountMode.ToString() == (id == "all-current-wood-to-berries" ? "AllMatching" : "Exact")
                    && goal.Allocation.Workers.State.ToString() == "Gathering"
                    && goal.Allocation.Workers.CurrentResource == ResourceType.Wood
                    && goal.Allocation.Destination.Resource == ResourceType.Food
                    && goal.Allocation.Destination.SourceKind == ResourceSourceKind.Berries
                    && goal.Status == CommanderGoalStatus.Completed && hasNative("GatherCommand:processed"),
                    "native-worker-allocation", "all-exact-source-or-destination-mismatch");
                return;
            }
            if (id == "two-houses-near-town-center")
            {
                Mark(oneCall && (bool?)row["approved"] == true && fixture.Commands.Count(name => name == "PlaceBuildingCommand") == 2
                    && fixture.Native.Count(name => name == "PlaceBuildingCommand:processed") == 2
                    && states.Contains("BuildStructureGoal:Completed"), "native-construction", "two-distinct-build-results-missing");
                return;
            }
            if (id == "defense-arrival-native-receipt")
            {
                Mark(oneCall && hasCommand("MoveCommand") && fixture.Native.Count(name => name == "Move:Complete") == 6
                    && states.Contains("CommanderCapabilityGoal:Completed"), "native-arrival", "six-exact-arrivals-missing");
                return;
            }
            if (id == "all-wood-west-tc-explicit-move")
            {
                Mark(oneCall && hasCommand("MoveCommand") && !hasCommand("GatherCommand")
                    && hasNative("Move:Complete") && states.Contains("CommanderCapabilityGoal:Completed"),
                    "native-movement", "explicit-move-rewritten-or-incomplete");
                return;
            }
            if (id == "all-wood-west-tc-explicit-gather")
            {
                Mark(oneCall && hasCommand("GatherCommand") && !hasCommand("MoveCommand")
                    && hasNative("GatherCommand:processed") && states.Contains("AllocateWorkersGoal:Completed"),
                    "native-gather", "explicit-gather-rewritten-or-incomplete");
                return;
            }
            if (id == "additional-gathered-food-bounded-objective")
            {
                var goal = fixture.Manager.Goals.OfType<AllocateWorkersGoal>().SingleOrDefault();
                Mark(oneCall && hasCommand("GatherCommand") && hasNative("GatherCommand:processed")
                    && goal?.Status == CommanderGoalStatus.Completed
                    && goal.ResourceProgress == 400
                    && (long?)row["foodIncomeAtCompletion"] == 400
                    && (long?)row["gatheredFoodIncome"] > 400
                    && (int?)row["commanderCommandsAtFoodCompletion"] == fixture.Commands.Count
                    && (bool?)row["ordinaryFoodGatherActive"] == true,
                    "native-resource-objective", "completion-or-post-target-gather-not-observed");
                return;
            }
            Mark(oneCall && fixture.Provider.LastResult?.IsValid == true && fixture.Manager.Goals.Count > 0,
                "admission", "expected-goal-not-admitted");
        }
        private static double Milliseconds(long start, long end)
            => Math.Round((end - start) * 1000.0 / Stopwatch.Frequency, 3);
    }
}

```
## NEW Assets/Tests/EditMode/CommanderEconomyClarificationTests.Phase5CCorpus.cs.meta
SHA256:3251487A38630DA404951D2E260E2D7286D67CBF8B5213A83FBDE90227520660
```
fileFormatVersion: 2
guid: 3c5fa05a3261a67458cb958960628f8f
```
## NEW Assets/Tests/EditMode/CommanderPhase5CScenarioArtifactTests.cs
SHA256:3662572D16C453B2D54FC3189948264892ED431D7FE8DEA654F38AEC1D5FA524
```
using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace OpenEmpires.Tests
{
    public sealed class CommanderPhase5CScenarioArtifactTests
    {
        [Test]
        public void PerTurnArtifact_IsAtomicBoundedAndRejectsUnsafeKeys()
        {
            Type writer = typeof(OpenEmpires.TestSupport.ScenarioEvidenceRecorder);
            MethodInfo write = writer.GetMethod("RecordPhase5CTurn", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(write, Is.Not.Null, "Task 6 extends the existing atomic recorder with a safe turn record.");
            string directory = Path.Combine(Path.GetTempPath(), "phase5c-artifact-" + Guid.NewGuid().ToString("N"));
            try
            {
                var safe = new JObject
                {
                    ["schemaVersion"] = 1, ["caseId"] = "mine-yes-original-request",
                    ["requestId"] = "request-1", ["utterance"] = "Build a gold mine near gold.",
                    ["actualPromptTokens"] = JValue.CreateNull(), ["status"] = "started"
                };
                string path = Path.Combine(directory, "mine-yes-original-request-turn-0.json");
                var recorder = new OpenEmpires.TestSupport.ScenarioEvidenceRecorder(path, Guid.NewGuid().ToString("D"));
                write.Invoke(recorder, new object[] { safe });
                Assert.That(File.Exists(path), Is.True);
                Assert.That(JObject.Parse(File.ReadAllText(path))["actualPromptTokens"].Type, Is.EqualTo(JTokenType.Null));
                Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
                var unsafeRecord = (JObject)safe.DeepClone();
                unsafeRecord["entityId"] = 42;
                Assert.That(() => write.Invoke(recorder, new object[] { unsafeRecord }),
                    Throws.TypeOf<TargetInvocationException>());
                Assert.That(Directory.GetFiles(directory, "*.json").Length, Is.EqualTo(1));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}

```
## NEW Assets/Tests/EditMode/CommanderPhase5CScenarioArtifactTests.cs.meta
SHA256:A5A54BB2A06405947A5364B1E499957078BC2496ABCF393DDEB3487C4DB52CE4
```
fileFormatVersion: 2
guid: 26583bcda9a08a74c87cd7d78b56ce39
```
