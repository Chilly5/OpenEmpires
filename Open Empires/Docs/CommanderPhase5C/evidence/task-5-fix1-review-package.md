# Task 5 fix1 incremental review package
Base: exact pre-fix1 saved working-tree bytes; HEAD unchanged 4c9de96adbf6342fbe9e1490fa9b898a0f99caf6. No commits. Includes every captured baseline, with explicit no-delta entries.
## Assets\Scripts\AI\Commander\Phase4A\CommanderChatUI.cs
Before SHA256: DA0CB4005D6853D05E25F8AA03DE7977072B74EFE59CE46A62A9EC34187ECC0D
After SHA256: 5B534E058A195EBB6343A3C6455273CA7C101A54E506B770CD18E3BC804CD525
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-5-fix1-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.cs"
index 73e355b..880f12e 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-5-fix1-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.cs"	
@@ -218,50 +218,50 @@ namespace OpenEmpires
             if (lifetime == null) lifetime = new CancellationTokenSource();
             if (canvasRoot == null) BuildUI();
         }
 
         public async Task<CommanderAIChatSubmission> SubmitMessageAsync(string message)
         {
             string trimmed = (message ?? string.Empty).Trim();
             if (trimmed.Length == 0) return null;
             if(CommanderTacticalStatusProjection.IsInformationQuestion(trimmed))
             {
-                currentRequestGoalId = 0;
+                long informationTurn = BeginPresentationTurn();
                 int informationGeneration = runtimeGeneration;
                 SetLatestRequestState("Interpreting", trimmed);
                 if(trimmed.Length>CommanderSemanticProviderRequest.MaximumPlayerMessageCharacters)
                 {SetLatestRequestState("Failed", "Question is too long.");AppendLine("Commander","That question is too long; please shorten it.",false);return null;}
                 string informationForm=NormalizeExplanationWholeForm(trimmed);
                 if(informationForm=="strategy status"||informationForm=="current strategy status")
                 {
                     AppendLine("Player",trimmed,false);
                     AppendLine("Commander",strategicPipeline!=null&&Conversation!=null&&strategicPipeline.StrategicPlanner.PlayerId==Conversation.PlayerId
                         ?CurrentPlansStatus(strategicPipeline,Conversation.PlayerId):"Strategic Commander is not ready.",false);
                     SetLatestRequestState("Completed", "Information answered; no gameplay order submitted.");
                     return null;
                 }
                 if(TryHandleExplanationQuery(trimmed))
                 {SetLatestRequestState("Completed", "Information answered; no gameplay order submitted.");return null;}
-                var informationResult = await SubmitReadOnlyQuestionAsync(trimmed);
-                if(this!=null&&informationGeneration==runtimeGeneration)
+                var informationResult = await SubmitReadOnlyQuestionAsync(trimmed, informationTurn);
+                if(this!=null&&informationGeneration==runtimeGeneration&&IsLatestPresentationTurn(informationTurn))
                     SetLatestRequestState("Completed", "Information answered; no gameplay order submitted.");
                 return informationResult;
             }
             if (submitting)
             {
                 // Explicit strategic controls may invalidate an in-flight translation;
                 // the busy guard must not prevent the human from pausing/cancelling it.
                 // Pending worker slot-filling stays serialized with its active reply.
                 if (pendingClarification == null) TryHandleStrategicLifecycle(trimmed);
                 return null;
             }
-            currentRequestGoalId = 0;
+            long presentationTurn = BeginPresentationTurn();
             if (HandlePendingControl(trimmed)) return null;
             if (HandleActionPlanControl(trimmed)) return null;
             if (pendingClarification == null
                 && (NormalizeWholeForm(trimmed) == "yes" || NormalizeWholeForm(trimmed) == "no"))
             {
                 AppendLine("Player", trimmed);
                 AppendLine("Commander", "No Commander question or action plan is awaiting that answer.");
                 return null;
             }
             ClearActionPlanPreview(); // An independent player turn supersedes an unapproved candidate.
@@ -314,21 +314,21 @@ namespace OpenEmpires
             LatestStrategicInterpretation = null;
             LatestSubmission = null;
             UpdateStrategicControls();
             AppendLine("Player", trimmed);
             int submittedDraftRevision = inputRevision;
             inputField.interactable = false;
             sendButton.interactable = false;
             try
             {
                 if (semanticProvider != null)
-                    return await SubmitSemanticTacticalAsync(trimmed, generation);
+                    return await SubmitSemanticTacticalAsync(trimmed, generation, presentationTurn);
 
                 CommanderTextRoute route = intentRouter.Classify(trimmed);
                 if (route == CommanderTextRoute.Rejected)
                 {
                     AppendLine("Commander", "Unsupported or mixed Commander request.");
                     return null;
                 }
                 if (route == CommanderTextRoute.Strategic)
                 {
                     if (strategicBridge == null)
@@ -382,39 +382,43 @@ namespace OpenEmpires
                                 return null;
                             }
                             pendingAdaptationProposal = proposal;
                         }
                         pendingStrategicPipeline = sourcePipeline;
                         pendingStrategicGeneration = generation;
                         pendingStrategicOwner = owner;
                         pendingStrategicIntentId = pending.IntentId;
                         pendingStrategicLastPlanId = sourceLastPlanId;
                         pendingStrategicHadNoPlan = sourceCount == 0;
+                        pendingStrategicPresentationTurn = presentationTurn;
                     }
                     LatestStrategicInterpretation = result;
+                    if (result.Success)
+                        PublishLatestRequestState(presentationTurn, "Awaiting approval", trimmed);
                     AppendLine("Commander", result.Success
                         ? StrategicPreview(result.Intent.ObjectiveType)
                             + (pendingAdaptationProposal == null
                                 ? " No plan started. Approve normally, or explicitly confirm as your command (may replace AI plans, never direct-player plans)."
                                 : " Strategy #" + pendingAdaptationProposal.SourcePlanId
                                     + " is still active. This is a pending adaptation proposal, not a new plan."
                                     + " Ordinary Approve cannot replace it. Choose Confirm as command explicitly;"
                                     + " existing policy may reject it or allow coexistence instead of replacement.")
                         : result.ExplanationText);
                     UpdateStrategicControls();
                     return null;
                 }
                 CommanderAIChatSubmission tacticalResult = await adapter.SubmitAsync(trimmed, lifetime.Token);
                 if (this != null && generation == runtimeGeneration)
                 {
                     LatestSubmission = tacticalResult;
-                    TrackCurrentRequest(tacticalResult?.CommanderSubmission);
+                    if (IsLatestPresentationTurn(presentationTurn))
+                        TrackCurrentRequest(tacticalResult?.CommanderSubmission);
                     AppendLine("Commander", tacticalResult?.DisplayText ?? "The order failed safely.");
                 }
                 return tacticalResult;
             }
             finally
             {
                 if (this != null && inputField != null && generation == runtimeGeneration)
                 {
                     submitting = false;
                     inputField.interactable = true;
@@ -423,21 +427,21 @@ namespace OpenEmpires
                         && inputField.text == trimmed) inputField.text = string.Empty;
                     UpdateStrategicControls();
                     UpdateVoiceUI();
                     if (commanderStatusText != null && commanderStatusText.text == "Commander is interpreting...")
                         SetCommanderStatus("Request finished. Ready for your next message.", false);
                 }
             }
         }
 
         private async Task<CommanderAIChatSubmission> SubmitSemanticTacticalAsync(
-            string message, int generation)
+            string message, int generation, long presentationTurn)
         {
             ICommanderSemanticProvider provider = semanticProvider;
             CommanderPendingClarification capturedPending = pendingClarification;
             GameSimulation simulation = semanticSimulation;
             CommanderGoalManager manager = semanticGoalManager;
             CommanderIntentDispatcher dispatcher = semanticDispatcher;
             int owner = Conversation.PlayerId;
             StrategicAIApprovalBridge bridge = strategicBridge;
             StrategicPipeline sourcePipeline = strategicPipeline;
             int bridgeGeneration = bridge?.Generation ?? -1;
@@ -506,75 +510,75 @@ namespace OpenEmpires
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
-                            OfferRequestRecovery(message, "Timed out — no order was submitted.");
+                            OfferRequestRecovery(presentationTurn, message, "Timed out — no order was submitted.");
                             AppendLine("Commander", "That request timed out. No order was submitted. Choose Retry, Edit, or Cancel.");
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
                         || !ReferenceEquals(semanticDispatcher, dispatcher)
                         || Conversation == null || Conversation.PlayerId != owner
                         || request.IsCancellationRequested || lifetime.IsCancellationRequested)
                         return null;
 
                     if (result == null || !result.IsValid)
                     {
-                        SetLatestRequestState("Failed", "The response could not be safely interpreted.");
+                        PublishLatestRequestState(presentationTurn, "Failed", "The response could not be safely interpreted.");
                         if (capturedPending != null) { ReaskPending("That reply could not safely fill the missing criteria."); return null; }
                         AppendLine("Commander", provider is OpenRouterCommanderProvider && result != null
                             ? result.SafeExplanation : "I couldn't understand that request safely.");
                         return null;
                     }
                     manager.TraceRequest(requestTicket, "interpreted-" + result.Outcome);
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
                         if (HandleClarificationResult(result, capturedPending, message, generation, requestTicket,
                             ReferenceEquals(result, localGraph))) return null;
                         if (result.Outcome == CommanderSemanticOutcome.Clarify)
                             Conversation.SemanticMemory.RecordClarification(result.SafeExplanation);
-                        SetLatestRequestState(result.Outcome == CommanderSemanticOutcome.Clarify
+                        PublishLatestRequestState(presentationTurn, result.Outcome == CommanderSemanticOutcome.Clarify
                             ? "Needs clarification" : "Completed", result.SafeExplanation);
                         AppendLine("Commander", string.IsNullOrWhiteSpace(result.SafeExplanation)
                             ? "Please clarify or try a supported Commander request."
                             : result.SafeExplanation);
                         return null;
                     }
                     if (HandleClarificationResult(result, capturedPending, message, generation, requestTicket,
                         ReferenceEquals(result, localGraph))) return null;
                     if (result.Outcome == CommanderSemanticOutcome.DynamicPlan)
                     {
                         semanticStage = "dynamic-admission";
                         pendingActionPlan = manager.PrepareActionPlan(result, requestTicket.OriginalInput, generation,
                             strategicPipeline, IsCurrentScope, requestTicket);
-                        SetLatestRequestState("Awaiting approval", requestTicket.OriginalInput);
+                        PublishLatestRequestState(presentationTurn, "Awaiting approval", requestTicket.OriginalInput);
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
@@ -582,21 +586,21 @@ namespace OpenEmpires
                     {
                         semanticStage = "graph-admission";
                         if (!CommanderSemanticGraphAdmission.TryAdmit(result, context,
                             out CommanderSemanticGraphPlan graph, out string graphReason))
                         {
                             AppendLine("Commander", graphReason);
                             return null;
                         }
                         pendingActionPlan = manager.PrepareActionPlan(result, requestTicket.OriginalInput, generation,
                             strategicPipeline, IsCurrentScope, requestTicket);
-                        SetLatestRequestState("Awaiting approval", requestTicket.OriginalInput);
+                        PublishLatestRequestState(presentationTurn, "Awaiting approval", requestTicket.OriginalInput);
                         AppendLine("Commander", pendingActionPlan.Preview);
                         UpdateStrategicControls();
                         return null;
                     }
                     CommanderSemanticNode node = result.Nodes[0];
                     if (node.Type == CommanderSemanticNodeType.StrategicObjective)
                     {
                         if (!node.StrategicObjectiveType.HasValue || bridge == null
                             || sourcePipeline == null || !sourceCaptured)
                         {
@@ -638,20 +642,22 @@ namespace OpenEmpires
                                 return null;
                             }
                             pendingAdaptationProposal = proposal;
                         }
                         pendingStrategicPipeline = sourcePipeline;
                         pendingStrategicGeneration = generation;
                         pendingStrategicOwner = owner;
                         pendingStrategicIntentId = pending.IntentId;
                         pendingStrategicLastPlanId = sourceLastPlanId;
                         pendingStrategicHadNoPlan = sourceCount == 0;
+                        pendingStrategicPresentationTurn = presentationTurn;
+                        PublishLatestRequestState(presentationTurn, "Awaiting approval", message);
                         AppendLine("Commander", StrategicPreview(pending.ObjectiveType)
                             + (pendingAdaptationProposal == null
                                 ? " No plan started. Approve normally, or explicitly confirm as your command (may replace AI plans, never direct-player plans)."
                                 : " Strategy #" + pendingAdaptationProposal.SourcePlanId
                                     + " is still active. This is a pending adaptation proposal, not a new plan."
                                     + " Ordinary Approve cannot replace it. Choose Confirm as command explicitly;"
                                     + " existing policy may reject it or allow coexistence instead of replacement."));
                         UpdateStrategicControls();
                         return null;
                     }
@@ -668,47 +674,50 @@ namespace OpenEmpires
                         () => dispatcher.SubmitIntent(intent));
                     string display = string.IsNullOrWhiteSpace(submission?.Response)
                         ? "Commander order submitted." : submission.Response;
                     CommanderIntentInterpretation interpretation = submission?.Interpretation
                         ?? CommanderIntentInterpretation.Accepted(intent);
                     CommanderAIProviderResult providerResult = CommanderAIProviderResult.Accepted(
                         CommanderIntentDtoCodec.FromIntent(intent), string.Empty, display);
                     var chatSubmission = new CommanderAIChatSubmission(providerResult,
                         interpretation, submission, display);
                     LatestSubmission = chatSubmission;
-                    SetLatestRequestState(submission?.CreatedGoal == true ? "Accepted" : "Failed", display);
+                    PublishLatestRequestState(presentationTurn, submission?.CreatedGoal == true ? "Accepted" : "Failed", display);
                     Debug.Log("[Commander] intent=admitted;goal-submitted=" + (submission?.CreatedGoal == true));
                     if (submission?.CreatedGoal == true)
                         RecordAcceptedSemanticNodes(result.Nodes);
-                    TrackCurrentRequest(submission);
+                    if (IsLatestPresentationTurn(presentationTurn)) TrackCurrentRequest(submission);
                     AppendLine("Commander", display);
                     return chatSubmission;
                 }
                 catch (System.OperationCanceledException)
                 {
                     if (this != null && generation == runtimeGeneration
                         && semanticRequestRevision == requestRevision && !lifetime.IsCancellationRequested)
                     {
                         retainSubmittedDraft = true;
-                        OfferRequestRecovery(message, "Timed out — no order was submitted.");
+                        OfferRequestRecovery(presentationTurn, message, "Timed out — no order was submitted.");
                         AppendLine("Commander", "That request timed out. No order was submitted. Choose Retry, Edit, or Cancel.");
                     }
                     return null;
                 }
                 catch (System.Exception error)
                 {
                     Debug.Log("[Commander] failed-stage=" + semanticStage + ";exception=" + error.GetType().Name);
                     if (this != null && generation == runtimeGeneration
-                        && !lifetime.IsCancellationRequested)
+                        && semanticRequestRevision == requestRevision
+                        && !request.IsCancellationRequested && !lifetime.IsCancellationRequested)
                     {
-                        SetLatestRequestState("Failed", "Translation failed safely; no order was submitted.");
-                        AppendLine("Commander", "Commander translation failed safely; no order was submitted.");
+                        PublishLatestRequestState(presentationTurn, "Failed", "Translation failed safely; no order was submitted.");
+                        AppendLine("Commander", IsLatestPresentationTurn(presentationTurn)
+                            ? "Commander translation failed safely; no order was submitted."
+                            : "Earlier request failed safely; no order was submitted.");
                     }
                     return null;
                 }
                 finally
                 {
                     if (ReferenceEquals(activeSemanticRequest, request)) activeSemanticRequest = null;
                 }
             }
         }
 
@@ -777,20 +786,21 @@ namespace OpenEmpires
         private static int LatestRetainedPlanId(StrategicPlanner planner)
         {
             int latest = 0;
             foreach (StrategicPlan plan in planner.Plans)
                 if (plan.StrategicPlanId > latest) latest = plan.StrategicPlanId;
             return latest;
         }
 
         private void ClearStrategicPreview()
         {
+            pendingStrategicPresentationTurn = 0;
             pendingAdaptationProposal = null;
             pendingStrategicPipeline = null;
             pendingStrategicGeneration = 0;
             pendingStrategicOwner = -1;
             pendingStrategicIntentId = 0;
             pendingStrategicLastPlanId = 0;
             pendingStrategicHadNoPlan = false;
         }
 
         private void RejectStaleStrategicPreview()
@@ -822,43 +832,49 @@ namespace OpenEmpires
                 || pendingAdaptationProposal != null && (currentCount != 1
                     || pendingAdaptationProposal.PendingIntentId != id
                     || !StrategicAdaptationProposalBuilder.IsFresh(pendingAdaptationProposal, currentSource)))
             {
                 RejectStaleStrategicPreview();
                 return null;
             }
             if (pendingAdaptationProposal != null && !confirmed) return null;
             StrategicIntent intent = confirmed ? strategicBridge.Confirm(id) : strategicBridge.TakeRecommendation(id);
             if (intent == null) return null;
+            long strategicPresentationTurn = pendingStrategicPresentationTurn;
             ClearStrategicPreview();
             pendingExplanationIntentId = intent.IntentId;
             pendingExplanationObjective = intent.ObjectiveType.ToString();
             try
             {
                 var approval = new StrategicApprovalLayer().Evaluate(
                     strategicPipeline.CaptureContext(), intent, intent.Source);
                 LatestStrategicDecision = strategicPipeline.EvaluateApprovedIntentNow(approval);
             }
             finally
             {
                 pendingExplanationIntentId = null;
                 pendingExplanationObjective = null;
             }
             AppendLine("Commander", LatestStrategicDecision.Outcome);
+            PublishLatestRequestState(strategicPresentationTurn,
+                LatestStrategicDecision.Submission?.CreatedPlan == true ? "Accepted" : "Failed",
+                LatestStrategicDecision.Outcome);
             UpdateStrategicControls();
             return LatestStrategicDecision;
         }
 
         public void DismissStrategicRecommendation()
         {
+            long strategicPresentationTurn = pendingStrategicPresentationTurn;
             strategicBridge?.ClearPending();
             ClearStrategicPreview();
+            PublishLatestRequestState(strategicPresentationTurn,"Cancelled","Strategic proposal dismissed; nothing started.");
             UpdateStrategicControls();
         }
 
         public void ResetConversation()
         {
             CancelActiveSemanticRequest();
             if (semanticGoalManager != null && !semanticGoalManager.IsDisposed)
                 semanticGoalManager.CancelFutureSubscriptions();
             ClearActionPlanPreview();
             pendingClarification = null;
@@ -899,24 +915,27 @@ namespace OpenEmpires
             bool available = !submitting && strategicBridge?.PendingIntent != null;
             if (approveStrategyButton != null)
                 approveStrategyButton.interactable = actionAvailable || available && pendingAdaptationProposal == null;
             if (confirmStrategyButton != null) confirmStrategyButton.interactable = available && !actionAvailable;
             if (dismissStrategyButton != null) dismissStrategyButton.interactable = available || actionAvailable;
             if (approveStrategyButton != null)
             {
                 var label = approveStrategyButton.GetComponentInChildren<TMP_Text>();
                 if (label != null) label.text = actionAvailable ? "Approve plan" : "Approve strategy";
             }
-            // Action plans use the expanded dock's explicit preview decisions;
-            // retain the existing row for strategic recommendations only.
-            if (strategicApprovalRow != null) strategicApprovalRow.SetActive(!actionAvailable && strategicBridge?.PendingIntent != null);
+            // The dock approval card is the sole visible decision row for both
+            // candidates and strategies; the buttons above retain their existing
+            // authority methods but must not form a second visible stage.
+            if (strategicApprovalRow != null) strategicApprovalRow.SetActive(false);
             UpdateStrategicHostControls();
+            if (strategicBridge?.PendingIntent != null && !tasksVisible && strategicHostRow != null)
+                strategicHostRow.SetActive(false);
         }
 
         public async Task<CommanderAIChatSubmission> SubmitCurrentInputAsync()
         {
             if (submitting) return null;
             string text = InputText;
             if (text.Length > CommanderSemanticProviderRequest.MaximumPlayerMessageCharacters)
             {
                 SetCommanderStatus("This request is too long. Shorten it to 1024 characters before sending.",true);
                 UpdatePresentation();
```
## Assets\Scripts\AI\Commander\Phase4A\CommanderChatUI.Presentation.cs
Before SHA256: 5544A0F72D93CF89223877C45D85D2F9CE45A58CC6A2AB4D33A5559278CE8A0D
After SHA256: 2D28477D4CAC1D8AC81C947E60429156533CE16EDB0A7B44B296A05B2CE4237A
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-5-fix1-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.Presentation.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.Presentation.cs"
index 6e95701..f97644f 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-5-fix1-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.Presentation.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.Presentation.cs"	
@@ -1,33 +1,36 @@
 using TMPro;
+using System.Text;
 using UnityEngine;
 using UnityEngine.EventSystems;
 using UnityEngine.UI;
 
 namespace OpenEmpires
 {
     public sealed partial class CommanderChatUI
     {
         private GameObject compactCommander, compactReview, compactApproval;
         private TMP_Text compactStatus, compactReviewText, compactApprovalText, inputLengthText;
         private Button compactRecord, compactCancel;
         private TMP_Text compactRecordLabel;
         private TMP_Text compactApproveLabel;
-        private TMP_Text latestRequestText, pendingQuestionText, expandedApprovalText, launcherText;
+        private TMP_Text latestRequestText, providerConnectionText, pendingQuestionText, expandedApprovalText, launcherText;
         private TMP_Text expandedReviewText;
-        private GameObject requestRecovery, pendingQuestion, expandedApproval, expandedReview;
+        private GameObject requestRecovery, pendingQuestion, expandedApproval, expandedReview, settingsMenu;
         private Button retryRequestButton, editRequestButton, cancelRequestButton;
         private Button chatTabButton, tasksTabButton;
         private RectTransform bubbleContent;
         private string latestRequestState = "Ready", latestRequestDetail = "";
+        private long presentationTurnRevision;
+        private long pendingStrategicPresentationTurn;
         private string recoveryDraft;
-        private bool recoveryAvailable, tasksVisible;
+        private bool recoveryAvailable, tasksVisible, settingsMenuVisible;
         private bool expanded;
         private int inputRevision, voiceDraftRevision;
         private string voiceOwnedDraft;
         private int lastPresentationActionFrame = -1;
         private int lastEscapeActionFrame = -1;
 
         public bool IsExpanded => expanded;
 
         private string VoiceCaptureLimitLabel
         {
@@ -67,31 +70,34 @@ namespace OpenEmpires
 
         // Input System gameplay callbacks run before MonoBehaviour.Update. Route
         // Escape here synchronously instead of hoping the UI polls it first.
         public static bool TryHandleEscapeInput()
         {
             if (instance == null || !instance.isActiveAndEnabled) return false;
             if (instance.lastEscapeActionFrame == Time.frameCount) return true;
             bool voiceOperation = instance.VoiceState == CommanderVoiceState.Recording
                 || instance.VoiceState == CommanderVoiceState.Transcribing
                 || instance.VoiceState == CommanderVoiceState.Preview;
-            if (!voiceOperation && !instance.voiceSetupVisible && !instance.semanticSetupVisible && !instance.expanded) return false;
+            if (!voiceOperation && !instance.voiceSetupVisible && !instance.semanticSetupVisible
+                && !instance.settingsMenuVisible && !instance.expanded) return false;
             instance.lastEscapeActionFrame = Time.frameCount;
             if (voiceOperation) instance.voiceController?.Cancel();
             else if (instance.voiceSetupVisible) instance.HideVoiceSetup();
             else if (instance.semanticSetupVisible) instance.HideSemanticSetup();
+            else if (instance.settingsMenuVisible) instance.CloseSettingsMenu();
             else instance.MinimizeCommander();
             return true;
         }
 
         public void OpenCommander()
         {
+            CloseSettingsMenu();
             expanded = true;
             UpdatePresentation();
             inputField?.ActivateInputField();
         }
 
         public void MinimizeCommander()
         {
             expanded = false;
             inputField?.DeactivateInputField();
             var events = EventSystem.current;
@@ -110,41 +116,188 @@ namespace OpenEmpires
             return true;
         }
 
         private void InitializePresentation()
         {
             voiceOwnedDraft = null;
             lastPresentationActionFrame = -1;
             recoveryAvailable = false;
             recoveryDraft = null;
             tasksVisible = false;
+            settingsMenuVisible = false;
             SetLatestRequestState("Ready", "Commander is ready.");
             MinimizeCommander();
         }
 
         private void SetLatestRequestState(string state, string detail)
         {
             latestRequestState = state ?? "Ready";
             latestRequestDetail = detail ?? string.Empty;
             UpdatePresentation();
         }
 
+        private long BeginPresentationTurn()
+        {
+            currentRequestGoalId = 0;
+            recoveryAvailable = false;
+            return ++presentationTurnRevision;
+        }
+
+        private bool IsLatestPresentationTurn(long turn) => turn == presentationTurnRevision;
+
+        private void PublishLatestRequestState(long turn, string state, string detail)
+        {
+            if (IsLatestPresentationTurn(turn)) SetLatestRequestState(state, detail);
+        }
+
+        private void OpenSettingsMenu()
+        {
+            HideVoiceSetup();
+            HideSemanticSetup();
+            MinimizeCommander();
+            settingsMenuVisible = true;
+            UpdatePresentation();
+        }
+
+        private void CloseSettingsMenu()
+        {
+            settingsMenuVisible = false;
+            if (settingsMenu != null) settingsMenu.SetActive(false);
+        }
+
+        private string ProviderConnectionLabel
+        {
+            get
+            {
+                string status = SemanticSetupStatus;
+                string route = semanticProvider is OpenRouterCommanderProvider router
+                    ? router.UsesGateway ? "Luna gateway" : "OpenRouter Luna"
+                    : "Text AI";
+                if (semanticSetupMode == SemanticSetupMode.Disabled) return "Text AI: disabled";
+                if (status.Contains("setup required")) return route + ": setup required";
+                if (status.Contains("timed out")) return route + ": timed out";
+                if (status.Contains("connection failed") || status.Contains("service unavailable"))
+                    return route + ": connection issue";
+                if (status.Contains("authentication failed") || status.Contains("access forbidden"))
+                    return route + ": access issue";
+                if (status.Contains("validated semantic data")) return route + ": validated response";
+                if (semanticProvider is OpenRouterCommanderProvider) return route + ": configured, unverified";
+                return "Text AI: local translator";
+            }
+        }
+
+        private string StrategicApprovalPreview()
+        {
+            StrategicIntent pending = strategicBridge?.PendingIntent;
+            if (pending == null) return string.Empty;
+            StrategicPlan template = StrategicPlanRegistry.CreateDefault().CreatePlan(pending);
+            StrategicAdaptationProposal adaptation = pendingAdaptationProposal;
+            var preview = new StringBuilder();
+            preview.AppendLine("Strategic proposal — no new plan started.");
+            preview.AppendLine("Source: validated AI recommendation for your Commander.");
+            if (adaptation != null)
+            {
+                preview.Append("Strategy #").Append(adaptation.SourcePlanId).Append(" (")
+                    .Append(adaptation.CurrentObjective).AppendLine(") remains active until you decide.");
+                preview.AppendLine("Proposed change:");
+            }
+            else preview.AppendLine("Proposed preparation:");
+            int count = adaptation?.ProposedTargets.Count ?? template.Milestones.Count;
+            for (int i = 0; i < count; i++)
+            {
+                string name = i < template.Milestones.Count ? template.Milestones[i].Name : "Next stage";
+                preview.Append("Step ").Append(i + 1).Append(" — ").Append(name).Append(": ");
+                bool any = false;
+                if (adaptation != null)
+                {
+                    foreach (StrategicAdaptationRequest request in adaptation.ProposedTargets[i].Requests)
+                    {
+                        if (any) preview.Append("; ");
+                        preview.Append(StrategicEffect(request, i, template));
+                        any = true;
+                    }
+                }
+                else foreach (StrategicTacticalGoalRequest request in template.Milestones[i].TacticalGoals)
+                {
+                    if (any) preview.Append("; ");
+                    preview.Append(StrategicEffect(request, i, template));
+                    any = true;
+                }
+                if (!any) preview.Append("ready when earlier steps finish");
+                preview.AppendLine(".");
+            }
+            if (adaptation != null)
+                preview.AppendLine("Ordinary Approve cannot replace the active strategy. Confirm as command explicitly; existing policy may reject, coexist, or replace only an eligible AI plan. Direct player plans are never replaced.");
+            else
+                preview.AppendLine("Approve strategy uses ordinary recommendation approval. Confirm as command is a separate stronger player decision. Neither happens by opening Details.");
+            return preview.ToString();
+        }
+
+        private string StrategicEffect(StrategicAdaptationRequest request, int step, StrategicPlan template)
+        {
+            switch (request.Kind)
+            {
+                case StrategicAdaptationRequestKind.WorkerAllocation:
+                    return request.WorkerTarget + " " + request.ResourceType.ToString().ToLowerInvariant() + " workers";
+                case StrategicAdaptationRequestKind.BuildStructure:
+                    return (request.EnsureExisting == true ? "ensure " : "add ") + request.Count + " " + StrategicBuildingName(request.StructureType.Value,request.Count.Value)
+                        + (request.EnsureExisting == true ? " exists" : "");
+                case StrategicAdaptationRequestKind.EnsureUnitCount:
+                    return StrategicUnitEffect(request.UnitType.Value, request.TargetTotal.Value, step, template);
+                default: return "validated step";
+            }
+        }
+
+        private string StrategicEffect(StrategicTacticalGoalRequest request, int step, StrategicPlan template)
+        {
+            if (request is StrategicResourceAllocationGoalRequest workers)
+                return workers.WorkerTarget + " " + workers.ResourceType.ToString().ToLowerInvariant() + " workers";
+            if (request is StrategicBuildStructureGoalRequest building)
+                return (building.EnsureExisting ? "ensure " : "add ") + building.Count + " " + StrategicBuildingName(building.StructureType,building.Count)
+                    + (building.EnsureExisting ? " exists" : "");
+            if (request is StrategicEnsureUnitCountGoalRequest units)
+                return StrategicUnitEffect(units.UnitType, units.TargetTotal, step, template);
+            return "validated step";
+        }
+
+        private string StrategicUnitEffect(int unitType, int total, int step, StrategicPlan template)
+        {
+            string units = unitType == CommanderIntentCatalog.ArcherUnitType ? "archers"
+                : unitType == CommanderIntentCatalog.SpearmanUnitType ? "spearmen"
+                : CommanderIntentCatalog.GetUnitDisplayName(unitType).ToLowerInvariant() + " units";
+            string producer = "its producer";
+            if (semanticSimulation != null && semanticSimulation.TryGetProductionBuildingType(
+                pendingStrategicOwner, unitType, out BuildingType building)) producer = building.ToString();
+            int producerStep = 0;
+            for (int i = 0; i < step && i < template.Milestones.Count; i++)
+                foreach (StrategicTacticalGoalRequest request in template.Milestones[i].TacticalGoals)
+                    if (request is StrategicBuildStructureGoalRequest build && build.StructureType.ToString() == producer)
+                        producerStep = i + 1;
+            return "reach " + total + " total " + units + " after "
+                + (producerStep > 0 ? "step " + producerStep + " secures " : "securing ") + producer
+                + " (existing units count; train only the shortfall)";
+        }
+
+        private static string StrategicBuildingName(BuildingType type, int count)
+            => type == BuildingType.Tower && count > 1 ? "towers" : type.ToString();
+
         private void CancelActiveSemanticRequest()
         {
             semanticRequestRevision++;
             activeSemanticRequest?.Cancel();
             activeSemanticRequest = null;
             recoveryAvailable = false;
         }
 
-        private void OfferRequestRecovery(string original, string reason)
+        private void OfferRequestRecovery(long turn, string original, string reason)
         {
+            if (!IsLatestPresentationTurn(turn)) return;
             recoveryDraft = original;
             recoveryAvailable = true;
             SetLatestRequestState("Failed", reason);
         }
 
         private void RetryLatestRequest()
         {
             if (!recoveryAvailable || submitting || recoveryDraft == null || InputText != recoveryDraft) return;
             recoveryAvailable = false; // one physical action consumes this recovery offer
             _ = SubmitCurrentInputAsync();
@@ -263,61 +416,80 @@ namespace OpenEmpires
             launcherText = open.GetComponentInChildren<TMP_Text>();
             var settings = CompactButton(compactCommander.transform,"VoiceOptions","Voice",()=>{if(voiceSetupVisible)HideVoiceSetup();else ShowVoiceSetup();});
             var textSettings = CompactButton(compactCommander.transform,"TextAIOptions","Text AI",()=>{if(semanticSetupVisible)HideSemanticSetup();else ShowSemanticSetup();});
             compactRecord = CompactButton(compactCommander.transform,"Record","● Record",OnPttButtonClicked);
             compactRecordLabel = compactRecord.GetComponentInChildren<TMP_Text>();
             compactCancel = CompactButton(compactCommander.transform,"CancelVoice","Cancel",OnVoiceCancelClicked);
             PanelRow("Launcher",compactCommander.transform,open.gameObject);
             compactCommander.transform.Find("Launcher").SetAsFirstSibling();
             settings.gameObject.SetActive(false);
             textSettings.gameObject.SetActive(false);
+            settingsMenu = UIObject("SettingsMenu", compactCommander.transform);
+            settingsMenu.AddComponent<Image>().color = new Color(.09f,.11f,.13f,1f);
+            PanelElement(settingsMenu, 154);
+            var settingsLayout = settingsMenu.AddComponent<VerticalLayoutGroup>();
+            settingsLayout.padding = new RectOffset(6,6,6,6); settingsLayout.spacing = 5;
+            settingsLayout.childControlWidth = settingsLayout.childControlHeight = true;
+            settingsLayout.childForceExpandWidth = true; settingsLayout.childForceExpandHeight = false;
+            var settingsHeading = Text("Heading",settingsMenu.transform,"SETTINGS",13,TextAlignmentOptions.Left);
+            settingsHeading.richText = false; PanelElement(settingsHeading.gameObject,18);
+            PanelElement(CompactButton(settingsMenu.transform,"VoiceSettings","Voice settings",()=>
+            {CloseSettingsMenu();ShowVoiceSetup();}).gameObject,32);
+            PanelElement(CompactButton(settingsMenu.transform,"TextAISettings","Text AI settings",()=>
+            {CloseSettingsMenu();ShowSemanticSetup();}).gameObject,32);
+            PanelElement(CompactButton(settingsMenu.transform,"CloseSettings","Close",CloseSettingsMenu).gameObject,32);
+            settingsMenu.SetActive(false);
             compactStatus = Text("Status",compactCommander.transform,"Preparing Commander…",12,TextAlignmentOptions.Left);
             compactStatus.richText = false; PanelElement(compactStatus.gameObject,28);
             BuildTaskBoardUI();
             BuildVoiceSetupUI();
             BuildSemanticSetupUI();
             compactReview = ReviewCard("TranscriptReview","Review transcript — not submitted",out compactReviewText,
                 new[]{"Send","Edit","Discard"},new UnityEngine.Events.UnityAction[]{()=>{_ = SubmitCurrentInputAsync();},OpenCommander,OnVoiceCancelClicked});
             compactApproval = ReviewCard("PlanApproval","Plan approval — not started",out compactApprovalText,
                 new[]{"Details","Approve","Reject"},new UnityEngine.Events.UnityAction[]{OpenCommander,
                     ()=>{if(pendingActionPlan!=null)HandleActionPlanControl("approve plan");else OpenCommander();},
                     ()=>{if(pendingActionPlan!=null)HandleActionPlanControl("cancel plan");else DismissStrategicRecommendation();}});
             compactApproveLabel = compactApproval.transform.Find("Actions/Action1").GetComponentInChildren<TMP_Text>();
             var title = commanderPanel.Find("Title")?.GetComponent<TMP_Text>();
             if (title != null) title.text = "COMMANDER";
             commanderPanel.GetComponent<Image>().color = new Color(.065f,.075f,.085f,1f);
             latestRequestText = Text("LatestRequest",commanderPanel,"Ready — Commander is ready.",14,TextAlignmentOptions.Left);
             latestRequestText.richText = false; PanelElement(latestRequestText.gameObject,36);
-            latestRequestText.transform.SetSiblingIndex(2);
+            providerConnectionText = Text("ProviderConnection",commanderPanel,"Text AI: local translator",12,TextAlignmentOptions.Left);
+            providerConnectionText.richText = false; PanelElement(providerConnectionText.gameObject,18);
+            providerConnectionText.transform.SetSiblingIndex(2);
+            latestRequestText.transform.SetSiblingIndex(3);
             var chat = CompactButton(commanderPanel,"ChatTab","Chat",()=>{tasksVisible=false;UpdatePresentation();});
-            var tasks = CompactButton(commanderPanel,"TasksTab","Tasks",()=>{tasksVisible=true;UpdatePresentation();});
+            var tasks = CompactButton(commanderPanel,"TasksTab","Tasks",()=>
+            {tasksVisible=true;UpdateStrategicHostControls();UpdatePresentation();});
             chatTabButton = chat; tasksTabButton = tasks;
             var views = PanelRow("Views",commanderPanel,chat.gameObject,tasks.gameObject);
-            views.transform.SetSiblingIndex(3);
-            var settingsButton = CompactButton(commanderPanel,"Settings","Settings",()=>{if(semanticSetupVisible)HideSemanticSetup();else ShowSemanticSetup();});
+            views.transform.SetSiblingIndex(4);
+            var settingsButton = CompactButton(commanderPanel,"Settings","Settings",OpenSettingsMenu);
             PanelElement(settingsButton.gameObject,28);
             settingsButton.GetComponent<LayoutElement>().ignoreLayout = true;
             SetRect(settingsButton.GetComponent<RectTransform>(),Vector2.one,Vector2.one,new Vector2(-122,-37),new Vector2(-52,-9));
             var minimize = CompactButton(commanderPanel,"Minimize","Minimize Commander",MinimizeCommander);
             PanelElement(minimize.gameObject,32);
             minimize.GetComponent<LayoutElement>().ignoreLayout = true;
             minimize.GetComponentInChildren<TMP_Text>().text="−";
             SetRect(minimize.GetComponent<RectTransform>(),Vector2.one,Vector2.one,new Vector2(-44,-37),new Vector2(-12,-9));
             pendingQuestion = UIObject("PendingClarification",commanderPanel);
             pendingQuestion.AddComponent<Image>().color = new Color(.14f,.19f,.23f,1f);
             pendingQuestionText = Text("Question",pendingQuestion.transform,"",14,TextAlignmentOptions.Left);
             pendingQuestionText.richText=false;
             SetRect(pendingQuestionText.rectTransform,Vector2.zero,Vector2.one,new Vector2(8,4),new Vector2(-8,-4));
-            PanelElement(pendingQuestion,64);pendingQuestion.transform.SetSiblingIndex(4);
+            PanelElement(pendingQuestion,64);pendingQuestion.transform.SetSiblingIndex(5);
             expandedApproval = UIObject("Approval",commanderPanel);
             expandedApproval.AddComponent<Image>().color = new Color(.14f,.19f,.23f,1f);
-            PanelElement(expandedApproval,168,true);expandedApproval.transform.SetSiblingIndex(5);
+            PanelElement(expandedApproval,168,true);expandedApproval.transform.SetSiblingIndex(6);
             var approvalScroll = UIObject("PreviewScroll",expandedApproval.transform);
             SetRect(approvalScroll.GetComponent<RectTransform>(),Vector2.zero,Vector2.one,
                 new Vector2(8,44),new Vector2(-8,-8));
             var approvalScrollRect = approvalScroll.AddComponent<ScrollRect>();
             approvalScrollRect.horizontal=false;
             approvalScrollRect.vertical=true;
             approvalScrollRect.movementType=ScrollRect.MovementType.Clamped;
             var approvalViewport=UIObject("Viewport",approvalScroll.transform);
             SetRect(approvalViewport.GetComponent<RectTransform>(),Vector2.zero,Vector2.one,
                 Vector2.zero,Vector2.zero);
@@ -332,20 +504,32 @@ namespace OpenEmpires
             expandedApprovalText.rectTransform.pivot=new Vector2(.5f,1);
             expandedApprovalText.rectTransform.sizeDelta=Vector2.zero;
             expandedApprovalText.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
             approvalScrollRect.content=expandedApprovalText.rectTransform;
             var approvePlan=CompactButton(expandedApproval.transform,"ApprovePlan","Approve plan",()=>HandleActionPlanControl("approve plan"));
             SetRect(approvePlan.GetComponent<RectTransform>(),new Vector2(0,0),new Vector2(.5f,0),
                 new Vector2(8,7),new Vector2(-4,37));
             var rejectPlan=CompactButton(expandedApproval.transform,"RejectPlan","Reject plan",()=>HandleActionPlanControl("cancel plan"));
             SetRect(rejectPlan.GetComponent<RectTransform>(),new Vector2(.5f,0),new Vector2(1,0),
                 new Vector2(4,7),new Vector2(-8,37));
+            var approveStrategy=CompactButton(expandedApproval.transform,"ApproveStrategy","Approve strategy",()=>
+            {ApproveStrategicRecommendation();UpdatePresentation();});
+            SetRect(approveStrategy.GetComponent<RectTransform>(),new Vector2(0,0),new Vector2(1f/3f,0),
+                new Vector2(8,7),new Vector2(-4,37));
+            var confirmStrategy=CompactButton(expandedApproval.transform,"ConfirmStrategy","Confirm as command",()=>
+            {ConfirmStrategicCommand();UpdatePresentation();});
+            SetRect(confirmStrategy.GetComponent<RectTransform>(),new Vector2(1f/3f,0),new Vector2(2f/3f,0),
+                new Vector2(4,7),new Vector2(-4,37));
+            var dismissStrategy=CompactButton(expandedApproval.transform,"DismissStrategy","Dismiss",()=>
+            {DismissStrategicRecommendation();UpdatePresentation();});
+            SetRect(dismissStrategy.GetComponent<RectTransform>(),new Vector2(2f/3f,0),new Vector2(1,0),
+                new Vector2(4,7),new Vector2(-8,37));
             requestRecovery = UIObject("RequestRecovery",commanderPanel);
             retryRequestButton=CompactButton(requestRecovery.transform,"Retry","Retry",RetryLatestRequest);
             editRequestButton=CompactButton(requestRecovery.transform,"Edit","Edit",EditLatestRequest);
             cancelRequestButton=CompactButton(requestRecovery.transform,"Cancel","Cancel",CancelLatestRequest);
             var recoveryLayout=requestRecovery.AddComponent<HorizontalLayoutGroup>();
             recoveryLayout.spacing=8; recoveryLayout.childControlWidth=recoveryLayout.childControlHeight=true;
             recoveryLayout.childForceExpandWidth=true;recoveryLayout.childForceExpandHeight=true;
             PanelElement(requestRecovery,36);
             expandedReview=UIObject("VoiceReview",commanderPanel);
             expandedReview.AddComponent<Image>().color=new Color(.13f,.19f,.21f,1f);
@@ -439,84 +623,106 @@ namespace OpenEmpires
             Canvas.ForceUpdateCanvases();
             transcriptScroll.verticalNormalizedPosition = 0;
         }
 
         private void UpdatePresentation()
         {
             if (compactCommander == null) return;
             RefreshTaskBoard();
             RefreshSemanticSetupStatus();
             bool reviewing = VoiceState == CommanderVoiceState.Preview;
+            bool strategicChatApproval = !tasksVisible && strategicBridge?.PendingIntent != null;
             if (commanderPanel != null) commanderPanel.gameObject.SetActive(expanded);
             compactCommander.SetActive(!expanded);
             // LatestRequest is the visible lifecycle source; keep the legacy text only
             // as internal state so stale provider/debug status cannot crowd the dock.
             if (commanderStatusText != null) commanderStatusText.gameObject.SetActive(false);
             if (currentPlanStatusText != null)
-                currentPlanStatusText.gameObject.SetActive(selectedPlanId != 0);
+                currentPlanStatusText.gameObject.SetActive(selectedPlanId != 0 && !strategicChatApproval);
+            if (strategicChatApproval && strategicHostRow != null) strategicHostRow.SetActive(false);
             if (voiceRootObject != null)
             {
                 bool voiceNeedsStatus = VoiceState == CommanderVoiceState.Recording
                     || VoiceState == CommanderVoiceState.Transcribing
                     || VoiceState == CommanderVoiceState.Error;
                 voiceRootObject.GetComponent<LayoutElement>().minHeight = voiceNeedsStatus ? 56 : 36;
                 voiceRootObject.GetComponent<LayoutElement>().preferredHeight = voiceNeedsStatus ? 56 : 36;
                 if (voiceStatusText != null) voiceStatusText.gameObject.SetActive(voiceNeedsStatus);
                 voiceRootObject.SetActive(!reviewing);
             }
             if (latestRequestText != null)
             {
                 string detail=latestRequestDetail.Length>90?latestRequestDetail.Substring(0,87)+"…":latestRequestDetail;
                 latestRequestText.text=latestRequestState+(detail.Length==0?"":" — "+detail);
             }
+            if (providerConnectionText != null) providerConnectionText.text = ProviderConnectionLabel;
+            if (settingsMenu != null) settingsMenu.SetActive(settingsMenuVisible && !voiceSetupVisible && !semanticSetupVisible && !expanded);
             if (launcherText != null) launcherText.text="Commander"+(taskBadgeText!=null&&taskBadgeText.gameObject.activeSelf?" • "+taskBadgeText.text:"")
                 +(pendingClarification!=null||pendingActionPlan!=null||recoveryAvailable?" !":"");
             if(chatTabButton!=null)chatTabButton.GetComponent<Image>().color=tasksVisible?new Color(.13f,.15f,.17f,1f):new Color(.19f,.34f,.42f,1f);
             if(tasksTabButton!=null)tasksTabButton.GetComponent<Image>().color=tasksVisible?new Color(.19f,.34f,.42f,1f):new Color(.13f,.15f,.17f,1f);
             bool hasApproval = pendingActionPlan!=null||strategicBridge?.PendingIntent!=null;
             // A pending preview replaces Chat's transcript scroll, rather than
             // nesting an approval scroller alongside the conversation scroller.
             if(transcriptScroll!=null)transcriptScroll.gameObject.SetActive(!tasksVisible&&!hasApproval);
             if(taskBoardScroll!=null)taskBoardScroll.gameObject.SetActive(tasksVisible);
             if(pendingQuestion!=null)
             {
                 pendingQuestion.SetActive(pendingClarification!=null);
                 if(pendingClarification!=null)pendingQuestionText.text="Needs clarification — "+pendingClarification.Question;
             }
             if(expandedApproval!=null)
             {
                 expandedApproval.SetActive(hasApproval&&!tasksVisible);
-                if(hasApproval)expandedApprovalText.text=pendingActionPlan?.Preview??"Strategic change proposed. Review the effects before approving.";
+                if(hasApproval)expandedApprovalText.text=pendingActionPlan?.Preview??StrategicApprovalPreview();
                 var approvePlan=expandedApproval.transform.Find("ApprovePlan");
                 var rejectPlan=expandedApproval.transform.Find("RejectPlan");
                 if(approvePlan!=null)approvePlan.gameObject.SetActive(pendingActionPlan!=null);
                 if(rejectPlan!=null)rejectPlan.gameObject.SetActive(pendingActionPlan!=null);
+                bool strategicOnly=pendingActionPlan==null&&strategicBridge?.PendingIntent!=null;
+                var approveStrategy=expandedApproval.transform.Find("ApproveStrategy");
+                var confirmStrategy=expandedApproval.transform.Find("ConfirmStrategy");
+                var dismissStrategy=expandedApproval.transform.Find("DismissStrategy");
+                if(approveStrategy!=null)
+                {
+                    approveStrategy.gameObject.SetActive(strategicOnly);
+                    approveStrategy.GetComponent<Button>().interactable=strategicOnly&&pendingAdaptationProposal==null&&!submitting;
+                    approveStrategy.GetComponent<Image>().color=pendingAdaptationProposal==null
+                        ?new Color(.18f,.29f,.42f,1f):new Color(.12f,.14f,.16f,1f);
+                    approveStrategy.GetComponentInChildren<TMP_Text>().color=pendingAdaptationProposal==null
+                        ?Color.white:new Color(.55f,.59f,.62f,1f);
+                }
+                if(confirmStrategy!=null)
+                {
+                    confirmStrategy.gameObject.SetActive(strategicOnly);
+                    confirmStrategy.GetComponent<Button>().interactable=strategicOnly&&!submitting;
+                }
+                if(dismissStrategy!=null)dismissStrategy.gameObject.SetActive(strategicOnly);
             }
             if(requestRecovery!=null)requestRecovery.SetActive(recoveryAvailable||submitting&&semanticProvider!=null);
             if(expandedReview!=null)
             {
                 expandedReview.SetActive(reviewing);
                 if(reviewing)expandedReviewText.text="Reviewed transcript — not sent. Edit or Send: "
                     +(InputText.Length>72?InputText.Substring(0,69)+"…":InputText);
             }
             if(retryRequestButton!=null)retryRequestButton.gameObject.SetActive(recoveryAvailable);
             if(editRequestButton!=null)editRequestButton.gameObject.SetActive(recoveryAvailable);
             if(cancelRequestButton!=null)cancelRequestButton.gameObject.SetActive(recoveryAvailable||submitting&&semanticProvider!=null);
             BoundSetupCard(voiceSetupCard,532);
             BoundSetupCard(semanticSetupCard,336);
             compactReview.SetActive(reviewing);
             bool approving = pendingActionPlan != null || strategicBridge?.PendingIntent != null;
             compactApproval.SetActive(false);
             compactApproveLabel.text = pendingActionPlan != null ? "Approve plan" : "Review strategy";
             if (reviewing) compactReviewText.text = InputText;
-            if (approving) compactApprovalText.text = pendingActionPlan?.Preview
-                ?? "A strategy is awaiting consent. Open Details to review it and use the existing strategy confirmation controls.";
+            if (approving) compactApprovalText.text = pendingActionPlan?.Preview ?? StrategicApprovalPreview();
             bool busy = VoiceState == CommanderVoiceState.Recording || VoiceState == CommanderVoiceState.Transcribing;
             compactCancel.gameObject.SetActive(busy);
             compactRecord.interactable = voiceController != null && !submitting && CommanderVoiceSettings.VoiceEnabled
                 && (VoiceState == CommanderVoiceState.Idle || VoiceState == CommanderVoiceState.Error || VoiceState == CommanderVoiceState.Recording);
             compactRecordLabel.text = VoiceState == CommanderVoiceState.Recording ? "■ Stop" : "● Record";
             compactStatus.text = VoiceState == CommanderVoiceState.Recording ? VoiceCaptureActivityLabel
                 : VoiceState == CommanderVoiceState.Transcribing ? "Transcribing — Cancel available"
                 : VoiceState == CommanderVoiceState.Error ? "Voice unavailable — open Commander for details"
                 : submitting ? "Interpreting your request…"
                 : approving ? "Awaiting approval — no new plan started"
```
## Assets\Scripts\AI\Commander\Phase4A\CommanderChatUI.ReadOnlyQuestions.cs
Before SHA256: 37F2FFA936E5EF014613C3FBC0DAF7E3554810CFE1C1BE0922C78B55572988FF
After SHA256: A8D5DD169EB9344FFC908B989EE6B4E796FB3FA7B9D42627262F4173D498D4EE
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-5-fix1-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.ReadOnlyQuestions.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.ReadOnlyQuestions.cs"
index 15e832c..424f281 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-5-fix1-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.ReadOnlyQuestions.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.ReadOnlyQuestions.cs"	
@@ -1,36 +1,38 @@
 using System;
 using System.Threading;
 using System.Threading.Tasks;
 
 namespace OpenEmpires
 {
     public sealed partial class CommanderChatUI
     {
-        private async Task<CommanderAIChatSubmission> SubmitReadOnlyQuestionAsync(string message)
+        private async Task<CommanderAIChatSubmission> SubmitReadOnlyQuestionAsync(string message, long presentationTurn)
         {
             var sim=semanticSimulation;var manager=semanticGoalManager;var provider=semanticProvider;
             if(sim==null||manager==null||manager.IsDisposed||Conversation==null||Conversation.PlayerId!=manager.PlayerId)
             {AppendLine("Commander","Commander observations are not available in this runtime.",false);return null;}
             int generation=runtimeGeneration;
             bool Current()=>this!=null&&runtimeGeneration==generation&&!lifetime.IsCancellationRequested
                 &&ReferenceEquals(semanticSimulation,sim)&&ReferenceEquals(semanticGoalManager,manager)
                 &&ReferenceEquals(semanticProvider,provider)&&!manager.IsDisposed;
             bool statusQuestion=CommanderTacticalStatusProjection.IsStatusQuestion(message)
                 ||CommanderTacticalStatusProjection.IsCausalTaskQuestion(message,sim,manager);
             var status=statusQuestion?CommanderTacticalStatusProjection.Observe(sim,manager,message):null;
             TryObserveQuestionAnswer(NormalizeExplanationWholeForm(message),out string facts);
             void LocalFallback(string unavailable)
             {
                 if(!Current())return;
                 string answer=(statusQuestion?CommanderTacticalStatusProjection.Observe(sim,manager,message).Answer():facts)??unavailable;
-                AppendLine("Commander",answer,false);Conversation.Memory.RecordExplanation(answer);
+                AppendLine("Commander",IsLatestPresentationTurn(presentationTurn)
+                    ?answer:"Earlier question ("+message+"): "+answer,false);
+                Conversation.Memory.RecordExplanation(answer);
             }
             AppendLine("Player",message,false);
             if(submitting||provider==null)
             {
                 string local=status?.Answer()??facts??"Another interpretation is pending; no information request started gameplay.";
                 AppendLine("Commander",local,false);Conversation.Memory.RecordExplanation(local);return null;
             }
             // No request ticket, admission, approval, strategy evaluation, planner or
             // cancellation of pending gameplay previews occurs on this branch.
             submitting=true;
@@ -48,20 +50,22 @@ namespace OpenEmpires
                     {cancellation.Cancel();LocalFallback("The information request timed out; gameplay requests were unchanged.");return null;}
                     var result=await task;
                     if(!Current()||cancellation.IsCancellationRequested)return null;
                     // Reobserve after latency. The model cannot fabricate a tactical
                     // blocker or use an executable answer to acquire authority.
                     if(statusQuestion)status=CommanderTacticalStatusProjection.Observe(sim,manager,message);
                     string answer=status?.Answer()??facts;
                     if(answer==null)
                         answer=result?.IsValid==true&&(result.Outcome==CommanderSemanticOutcome.Answer||result.Outcome==CommanderSemanticOutcome.Clarify||result.Outcome==CommanderSemanticOutcome.Unsupported)
                             ?result.SafeExplanation:"You asked for information; no gameplay order was submitted.";
-                    AppendLine("Commander",answer,false);Conversation.Memory.RecordExplanation(answer);
+                    AppendLine("Commander",IsLatestPresentationTurn(presentationTurn)
+                        ?answer:"Earlier question ("+message+"): "+answer,false);
+                    Conversation.Memory.RecordExplanation(answer);
                 }
                 catch(OperationCanceledException){LocalFallback("The information request was cancelled; gameplay requests were unchanged.");}
                 catch(Exception){LocalFallback("I could not observe that safely; no gameplay requests changed.");}
                 finally{if(this!=null&&generation==runtimeGeneration)submitting=false;}
             }
             return null;
         }
     }
 }
```
## Assets\Scripts\AI\Commander\Phase5A\CommanderChatUI.ActionPlans.cs
Before SHA256: 3CEDF85432DC1A66B2DA36033E95629BBDD21605F2B3DE9D2E78B5902CF2685E
After SHA256: 3CEDF85432DC1A66B2DA36033E95629BBDD21605F2B3DE9D2E78B5902CF2685E
No delta.
## Assets\Scripts\AI\Commander\Phase5B\CommanderChatUI.TaskBoard.cs
Before SHA256: 89DB86100D7425AB5E1D294D50685035B3317341E7C1CEF9232851E9BA0188ED
After SHA256: 89DB86100D7425AB5E1D294D50685035B3317341E7C1CEF9232851E9BA0188ED
No delta.
## Assets\Scripts\AI\Commander\Phase5B\CommanderTaskBoardProjection.cs
Before SHA256: 70B8FE65696D3DB2624AC36E0732E0BF93A4F54F28C17C71F262579992E6D69F
After SHA256: 70B8FE65696D3DB2624AC36E0732E0BF93A4F54F28C17C71F262579992E6D69F
No delta.
## Assets\Tests\PlayMode\CommanderGrandFixInputDispatchPlayModeTests.cs
Before SHA256: 69D3E595703F6E38CB43CDF84C2F50D5A35C590C78DF710EA47F03B5304148FA
After SHA256: 69D3E595703F6E38CB43CDF84C2F50D5A35C590C78DF710EA47F03B5304148FA
No delta.
## Assets\Tests\PlayMode\CommanderPhase5CUIPlayModeTests.cs
Before SHA256: C04C9637944A4EC949EC8C414D9C8B0D794B35EEECF12F02E0256EC9864F3FD7
After SHA256: E8F5E01CF9332663B025ACF9F68862DF97A440DFC9897643943CCA4BB870CD61
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-5-fix1-baseline\\Assets\\Tests\\PlayMode\\CommanderPhase5CUIPlayModeTests.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\PlayMode\\CommanderPhase5CUIPlayModeTests.cs"
index 1e416bf..9c641a0 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-5-fix1-baseline\\Assets\\Tests\\PlayMode\\CommanderPhase5CUIPlayModeTests.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\PlayMode\\CommanderPhase5CUIPlayModeTests.cs"	
@@ -12,20 +12,22 @@ namespace OpenEmpires.Tests
 {
     [Category("CommanderPhase5COffline")]
     public sealed class CommanderPhase5CUIPlayModeTests
     {
         private CommanderChatUI chat;
         private CommanderGoalManager goals;
         private CommanderIntentDispatcher dispatcher;
         private GameSimulation simulation;
         private SimulationConfig config;
         private SlowProvider provider;
+        private StrategicPlanner strategicPlanner;
+        private StrategicPipeline strategicPipeline;
         private GameObject renderCameraObject;
         private int previousWidth, previousHeight;
 
         [SetUp]
         public void SetUp()
         {
             previousWidth = Screen.width; previousHeight = Screen.height;
             foreach (var existing in UnityEngine.Object.FindObjectsByType<CommanderChatUI>())
                 UnityEngine.Object.DestroyImmediate(existing.gameObject);
             config = ScriptableObject.CreateInstance<SimulationConfig>();
@@ -38,20 +40,22 @@ namespace OpenEmpires.Tests
             chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromMilliseconds(60));
         }
 
         [TearDown]
         public void TearDown()
         {
             if (previousWidth > 0 && previousHeight > 0) Screen.SetResolution(previousWidth, previousHeight, false);
             if (chat != null) UnityEngine.Object.DestroyImmediate(chat.gameObject);
             if (renderCameraObject != null) UnityEngine.Object.DestroyImmediate(renderCameraObject);
             dispatcher?.Dispose();
+            strategicPipeline?.Dispose();
+            strategicPlanner?.Dispose();
             goals?.Dispose();
             if (config != null) UnityEngine.Object.DestroyImmediate(config);
         }
 
         [UnityTest]
         public IEnumerator MinimizedStartup_OpensTabbedDockWithFixedMultilineComposer()
         {
             chat.enabled = true;
             yield return null;
             Assert.That(chat.IsExpanded, Is.False);
@@ -210,20 +214,202 @@ namespace OpenEmpires.Tests
             yield return null;
             Canvas.ForceUpdateCanvases();
             var panel = chat.transform.Find("CommanderCanvas/Panel");
             Assert.That(TextOf(panel.Find("Settings/Label")), Is.EqualTo("Settings"));
             Assert.That(panel.Find("CommanderStatus").gameObject.activeInHierarchy, Is.False);
             Assert.That(panel.Find("Current plan status").gameObject.activeInHierarchy, Is.False);
             Assert.That(panel.Find("VoiceBar").GetComponent<LayoutElement>().preferredHeight,
                 Is.LessThanOrEqualTo(42));
         }
 
+        [UnityTest]
+        public IEnumerator SettingsSurface_OffersProactiveVoiceAndTextAIWithoutConsent()
+        {
+            InstallRenderFixtureCamera();
+            chat.enabled = true;
+            chat.OpenCommander();
+            yield return null;
+            chat.transform.Find("CommanderCanvas/Panel/Settings").GetComponent<Button>().onClick.Invoke();
+            var menu = chat.transform.Find("CommanderCanvas/CompactCommander/SettingsMenu");
+            Assert.That(menu?.gameObject.activeInHierarchy, Is.True);
+            Assert.That(menu.Find("VoiceSettings")?.gameObject.activeInHierarchy, Is.True);
+            Assert.That(menu.Find("TextAISettings")?.gameObject.activeInHierarchy, Is.True);
+            Assert.That(chat.transform.Find("CommanderCanvas/CompactCommander/VoiceSetup").gameObject.activeInHierarchy, Is.False);
+            ForceFixtureRepaint();
+            Canvas.ForceUpdateCanvases();
+            yield return new WaitForEndOfFrame();
+            SaveCurrentFrame("fix1-settings-menu");
+            yield return null; // Separate physical click; same-frame UI actions are intentionally single-use.
+            menu.Find("VoiceSettings").GetComponent<Button>().onClick.Invoke();
+            Assert.That(chat.transform.Find("CommanderCanvas/CompactCommander/VoiceSetup").gameObject.activeInHierarchy, Is.True);
+            Assert.That(chat.transform.Find("CommanderCanvas/CompactCommander/VoiceSetup/SetupViewport/SetupContent/Actions/Action1")
+                .GetComponent<Button>().interactable, Is.False, "Opening Voice settings does not grant audio consent.");
+            Assert.That(goals.Goals, Is.Empty);
+        }
+
+        [UnityTest]
+        public IEnumerator SettingsSurface_EscapeClosesBeforeGameplayReceivesIt()
+        {
+            chat.enabled = true;
+            chat.OpenCommander();
+            yield return null;
+            chat.transform.Find("CommanderCanvas/Panel/Settings").GetComponent<Button>().onClick.Invoke();
+            var menu = chat.transform.Find("CommanderCanvas/CompactCommander/SettingsMenu");
+            Assert.That(menu.gameObject.activeInHierarchy, Is.True);
+            Assert.That(CommanderChatUI.TryHandleEscapeInput(), Is.True);
+            Assert.That(menu.gameObject.activeInHierarchy, Is.False);
+            Assert.That(chat.IsExpanded, Is.False);
+        }
+
+        [UnityTest]
+        public IEnumerator Header_ShowsProviderStateSeparateFromLatestRequest()
+        {
+            chat.enabled = true;
+            chat.OpenCommander();
+            yield return null;
+            var header = chat.transform.Find("CommanderCanvas/Panel/ProviderConnection");
+            Assert.That(header, Is.Not.Null);
+            Assert.That(TextOf(header), Does.Contain("Text AI"));
+            var question = chat.SubmitMessageAsync("strategy status");
+            while (!question.IsCompleted) yield return null;
+            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")), Does.StartWith("Completed"));
+            Assert.That(TextOf(header), Does.Not.Contain("Completed"));
+        }
+
+        [UnityTest]
+        public IEnumerator StrategicPreview_ShowsValidatedMilestonesAndDirectSingleStageDecisions()
+        {
+            InstallRenderFixtureCamera();
+            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
+            strategicPlanner = new StrategicPlanner(goals, _ => 5000);
+            strategicPipeline = new StrategicPipeline(simulation, goals, strategicPlanner);
+            chat.InitializeStrategic(new MockStrategicAIProvider(), strategicPipeline);
+            chat.enabled = true;
+            chat.OpenCommander();
+            var result = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"StrategicObjective\",\"objective\":\"RangedReinforcement\"}]}");
+            var turn = chat.SubmitMessageAsync("prepare ranged reinforcements");
+            provider.CompleteLatest(result);
+            while (!turn.IsCompleted) yield return null;
+            yield return null;
+            Assert.That(chat.PendingStrategicIntent, Is.Not.Null);
+            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
+                Does.StartWith("Awaiting approval"));
+            var approval = chat.transform.Find("CommanderCanvas/Panel/Approval");
+            Assert.That(approval.gameObject.activeInHierarchy, Is.True);
+            string preview = TextOf(approval.Find("PreviewScroll/Viewport/Details"));
+            Assert.That(preview, Does.Contain("8 food workers"));
+            Assert.That(preview, Does.Contain("8 wood workers"));
+            Assert.That(preview, Does.Contain("ArcheryRange"));
+            Assert.That(preview, Does.Contain("10 total archers"));
+            Assert.That(preview, Does.Contain("after"));
+            Assert.That(approval.Find("ApproveStrategy")?.gameObject.activeInHierarchy, Is.True);
+            Assert.That(approval.Find("ConfirmStrategy")?.gameObject.activeInHierarchy, Is.True);
+            Assert.That(approval.Find("DismissStrategy")?.gameObject.activeInHierarchy, Is.True);
+            Assert.That(goals.Goals, Is.Empty, "Preview has no execution authority.");
+            Canvas.ForceUpdateCanvases();
+            var panelCorners = new Vector3[4];
+            var decisionCorners = new Vector3[4];
+            var composerCorners = new Vector3[4];
+            chat.transform.Find("CommanderCanvas/Panel").GetComponent<RectTransform>().GetWorldCorners(panelCorners);
+            approval.Find("DismissStrategy").GetComponent<RectTransform>().GetWorldCorners(decisionCorners);
+            chat.transform.Find("CommanderCanvas/Panel/TextSubmission").GetComponent<RectTransform>().GetWorldCorners(composerCorners);
+            Assert.That(decisionCorners[0].y, Is.GreaterThanOrEqualTo(panelCorners[0].y));
+            Assert.That(composerCorners[2].y, Is.LessThanOrEqualTo(decisionCorners[0].y - 4));
+            ForceFixtureRepaint();
+            yield return new WaitForEndOfFrame();
+            SaveCurrentFrame("fix1-strategic-proposal");
+            yield return null;
+            approval.Find("DismissStrategy").GetComponent<Button>().onClick.Invoke();
+            Assert.That(chat.PendingStrategicIntent, Is.Null);
+            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
+                Does.StartWith("Cancelled"));
+            Assert.That(goals.Goals, Is.Empty);
+        }
+
+        [UnityTest]
+        public IEnumerator StrategicApprovalButtons_SameFrameCannotApproveAndConfirm()
+        {
+            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
+            strategicPlanner = new StrategicPlanner(goals, _ => 5000);
+            strategicPipeline = new StrategicPipeline(simulation, goals, strategicPlanner);
+            chat.InitializeStrategic(new MockStrategicAIProvider(), strategicPipeline);
+            chat.enabled = true;
+            chat.OpenCommander();
+            var result = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"StrategicObjective\",\"objective\":\"RangedReinforcement\"}]}");
+            var turn = chat.SubmitMessageAsync("prepare ranged reinforcements");
+            provider.CompleteLatest(result);
+            while (!turn.IsCompleted) yield return null;
+            yield return null;
+            var approval = chat.transform.Find("CommanderCanvas/Panel/Approval");
+            Assert.That(chat.PendingStrategicIntent, Is.Not.Null);
+            approval.Find("ApproveStrategy").GetComponent<Button>().onClick.Invoke();
+            approval.Find("ConfirmStrategy").GetComponent<Button>().onClick.Invoke();
+            Assert.That(chat.LatestStrategicDecision, Is.Not.Null);
+            Assert.That(strategicPipeline.DecisionHistory.History, Has.Count.EqualTo(1));
+            Assert.That(chat.PendingStrategicIntent, Is.Null);
+            Assert.That(provider.Attempts, Is.EqualTo(1));
+        }
+
+        [UnityTest]
+        public IEnumerator StrategicAdaptationPreview_NamesExistingSourceAndRequiresExplicitConfirm()
+        {
+            InstallRenderFixtureCamera();
+            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
+            strategicPlanner = new StrategicPlanner(goals, _ => 5000);
+            strategicPipeline = new StrategicPipeline(simulation, goals, strategicPlanner);
+            chat.InitializeStrategic(new MockStrategicAIProvider(), strategicPipeline);
+            var old = strategicPlanner.SubmitIntent(StrategicObjectiveType.RangedReinforcement).Plan;
+            Assert.That(old, Is.Not.Null);
+            chat.enabled = true;
+            chat.OpenCommander();
+            var result = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"StrategicObjective\",\"objective\":\"DefensiveTurtle\"}]}");
+            var turn = chat.SubmitMessageAsync("prepare fortified defenses");
+            provider.CompleteLatest(result);
+            while (!turn.IsCompleted) yield return null;
+            yield return null;
+            Assert.That(chat.PendingAdaptationProposal, Is.Not.Null);
+            var approval = chat.transform.Find("CommanderCanvas/Panel/Approval");
+            string preview = TextOf(approval.Find("PreviewScroll/Viewport/Details"));
+            Assert.That(preview, Does.Contain("Strategy #" + old.StrategicPlanId));
+            Assert.That(preview, Does.Contain("2 towers"));
+            Assert.That(preview, Does.Contain("8 total spearmen"));
+            Assert.That(preview, Does.Contain("Confirm as command"));
+            Assert.That(approval.Find("ApproveStrategy").GetComponent<Button>().interactable, Is.False,
+                "Ordinary approval cannot replace an existing plan.");
+            Assert.That(approval.Find("ConfirmStrategy").GetComponent<Button>().interactable, Is.True);
+            Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Active));
+            Assert.That(chat.transform.Find("CommanderCanvas/Panel/Current plan status").gameObject.activeInHierarchy,
+                Is.False, "The old plan summary belongs in Tasks while its adaptation preview owns Chat.");
+            Assert.That(chat.transform.Find("CommanderCanvas/Panel/TaskControls").gameObject.activeInHierarchy,
+                Is.False);
+            Canvas.ForceUpdateCanvases();
+            var panelCorners = new Vector3[4];
+            var composerCorners = new Vector3[4];
+            chat.transform.Find("CommanderCanvas/Panel").GetComponent<RectTransform>().GetWorldCorners(panelCorners);
+            chat.transform.Find("CommanderCanvas/Panel/TextSubmission").GetComponent<RectTransform>().GetWorldCorners(composerCorners);
+            Assert.That(composerCorners[0].y, Is.GreaterThanOrEqualTo(panelCorners[0].y),
+                "The fixed composer must remain inside the actual dock in adaptation view.");
+            ForceFixtureRepaint();
+            Canvas.ForceUpdateCanvases();
+            yield return new WaitForEndOfFrame();
+            SaveCurrentFrame("fix1-strategic-adaptation");
+            yield return null;
+            chat.transform.Find("CommanderCanvas/Panel/Views/TasksTab").GetComponent<Button>().onClick.Invoke();
+            yield return null;
+            Assert.That(chat.transform.Find("CommanderCanvas/Panel/TaskControls").gameObject.activeInHierarchy,
+                Is.True, "Existing owned plan controls remain accessible in Tasks.");
+            chat.transform.Find("CommanderCanvas/Panel/Views/ChatTab").GetComponent<Button>().onClick.Invoke();
+            yield return null;
+            approval.Find("DismissStrategy").GetComponent<Button>().onClick.Invoke();
+            Assert.That(old.Status, Is.EqualTo(StrategicPlanStatus.Active));
+        }
+
         [UnityTest]
         public IEnumerator Composer_LeavesTwoLineDraftAreaBesideCompactSend()
         {
             chat.enabled = true;
             chat.OpenCommander();
             yield return null;
             Canvas.ForceUpdateCanvases();
             var input = chat.transform.Find("CommanderCanvas/Panel/TextSubmission/Input").GetComponent<RectTransform>();
             var send = chat.transform.Find("CommanderCanvas/Panel/TextSubmission/Send").GetComponent<RectTransform>();
             Assert.That(input.rect.height, Is.GreaterThanOrEqualTo(60));
@@ -392,20 +578,95 @@ namespace OpenEmpires.Tests
             goals.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
             chat.OpenCommander();
             var turn = chat.SubmitMessageAsync("strategy status");
             while (!turn.IsCompleted) yield return null;
             yield return null;
             var status = chat.transform.Find("CommanderCanvas/Panel/LatestRequest");
             Assert.That(TextOf(status), Does.StartWith("Completed"));
             Assert.That(TextOf(status), Does.Not.Contain("Reach Age"));
         }
 
+        [UnityTest]
+        public IEnumerator NewerInformationQuestion_KeepsLatestStatusWhileOlderOrderGetsOwnTask()
+        {
+            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
+            chat.enabled = true;
+            chat.OpenCommander();
+            var olderOrder = chat.SubmitMessageAsync("train three spearmen");
+            Assert.That(provider.Attempts, Is.EqualTo(1));
+            var question = chat.SubmitMessageAsync("strategy status");
+            while (!question.IsCompleted) yield return null;
+            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
+                Does.StartWith("Completed"));
+            provider.CompleteLatest("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3}]}");
+            while (!olderOrder.IsCompleted) yield return null;
+            yield return null;
+            Assert.That(goals.Goals, Has.Count.EqualTo(1), "The older valid order keeps its own task.");
+            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
+                Does.StartWith("Completed"), "The older order cannot impersonate the newer answer.");
+        }
+
+        [UnityTest]
+        public IEnumerator CancelledRequest_LateProviderFaultCannotReplaceCancelledStatus()
+        {
+            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
+            chat.enabled = true;
+            chat.OpenCommander();
+            var request = chat.SubmitMessageAsync("train three spearmen");
+            Assert.That(provider.Attempts, Is.EqualTo(1));
+            provider.FailLatest(new InvalidOperationException("late fixture fault"));
+            chat.transform.Find("CommanderCanvas/Panel/RequestRecovery/Cancel")
+                .GetComponent<Button>().onClick.Invoke();
+            while (!request.IsCompleted) yield return null;
+            yield return null;
+            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
+                Does.StartWith("Cancelled"));
+            Assert.That(goals.Goals, Is.Empty);
+        }
+
+        [UnityTest]
+        public IEnumerator OlderProviderFault_CannotReplaceNewerInformationAnswer()
+        {
+            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
+            chat.enabled = true;
+            chat.OpenCommander();
+            var olderOrder = chat.SubmitMessageAsync("train three spearmen");
+            var question = chat.SubmitMessageAsync("strategy status");
+            while (!question.IsCompleted) yield return null;
+            provider.FailLatest(new InvalidOperationException("old provider fixture fault"));
+            while (!olderOrder.IsCompleted) yield return null;
+            yield return null;
+            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
+                Does.StartWith("Completed"), "An old fault cannot become the newer question's status.");
+            Assert.That(goals.Goals, Is.Empty);
+        }
+
+        [UnityTest]
+        public IEnumerator EarlierAsyncInformationAnswer_IsIdentifiedAfterNewerQuestion()
+        {
+            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
+            chat.enabled = true;
+            chat.OpenCommander();
+            var earlier = chat.SubmitMessageAsync("what is happening nearby?");
+            Assert.That(provider.Attempts, Is.EqualTo(1));
+            var newer = chat.SubmitMessageAsync("strategy status");
+            while (!newer.IsCompleted) yield return null;
+            provider.CompleteLatest("{\"outcome\":\"Answer\",\"message\":\"No nearby observations.\"}");
+            while (!earlier.IsCompleted) yield return null;
+            yield return null;
+            var bubbles = chat.transform.Find("CommanderCanvas/Panel/Transcript/Viewport/BubbleContent");
+            Assert.That(TextOf(bubbles.GetChild(bubbles.childCount - 1).Find("Message")),
+                Does.StartWith("Earlier question"));
+            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
+                Does.StartWith("Completed"));
+        }
+
         [UnityTest]
         public IEnumerator PopulatedChatView_StaysInsideScreenAndAboveFixedComposer()
         {
             chat.enabled = true;
             goals.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
             chat.OpenCommander();
             var turn = chat.SubmitMessageAsync("strategy status");
             while (!turn.IsCompleted) yield return null;
             yield return null;
             Canvas.ForceUpdateCanvases();
@@ -557,20 +818,21 @@ namespace OpenEmpires.Tests
             public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request, CancellationToken token)
                 => Task.FromResult(CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure, "offline fixture"));
             public Task<CommanderSemanticResult> TranslateSemanticAsync(CommanderSemanticProviderRequest request, CancellationToken token)
             {
                 Attempts++;
                 latest = new TaskCompletionSource<CommanderSemanticResult>();
                 return latest.Task;
             }
             public void CompleteLatest(string json) => latest?.TrySetResult(CommanderSemanticJson.Parse(json));
             public void CompleteLatest(CommanderSemanticResult result) => latest?.TrySetResult(result);
+            public void FailLatest(Exception error) => latest?.TrySetException(error);
         }
 
         private sealed class FixtureAudioCapture : ICommanderAudioCapture
         {
             public bool IsRecording { get; private set; }
             public string CurrentDevice => "FixtureMic";
             public bool StartRecording(string deviceName = null, float maxDurationSeconds = 15f)
             { IsRecording = true; return true; }
             public CommanderAudioData StopRecording()
             { IsRecording = false; return new CommanderAudioData(new float[16000], 16000, 1); }
```
## Assets\Tests\PlayMode\CommanderPhase5CUIPlayModeTests.cs.meta
Before SHA256: DA58BCAE9982CF35AD4E10AA343C0DF8EECB76F521ABB504F588C535238BE033
After SHA256: DA58BCAE9982CF35AD4E10AA343C0DF8EECB76F521ABB504F588C535238BE033
No delta.
