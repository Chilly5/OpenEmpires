# Task5 frozen incremental review package
HEAD unchanged4c9de96adbf6342fbe9e1490fa9b898a0f99caf6; no commits. Deltas against per-file pre-edit snapshots, not entire dirty branch. Source/render/platform boundaries in task-5-report.md.

## Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs
CurrentSHA256:DA0CB4005D6853D05E25F8AA03DE7977072B74EFE59CE46A62A9EC34187ECC0D
BaselineSHA256:7C794BCC68F9676AA45E07AEE098EB32B8C68B9CCC239AE6F79247190367365B
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-5-baseline\\Assets__Scripts__AI__Commander__Phase4A__CommanderChatUI.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.cs"
index f4d1212..73e355b 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-5-baseline\\Assets__Scripts__AI__Commander__Phase4A__CommanderChatUI.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.cs"	
@@ -36,24 +36,27 @@ namespace OpenEmpires
         private CommanderGoalManager semanticGoalManager;
         private CommanderIntentDispatcher semanticDispatcher;
         private System.TimeSpan semanticProviderTimeout = CommanderAIIntentAdapter.DefaultProviderTimeout;
         private CancellationTokenSource lifetime;
         private Coroutine bootstrapWait;
         private readonly CommanderIntentRouter intentRouter = new CommanderIntentRouter();
         private StrategicAIApprovalBridge strategicBridge;
         private StrategicPipeline strategicPipeline;
         private Button approveStrategyButton;
         private Button confirmStrategyButton;
         private Button dismissStrategyButton;
         private bool submitting;
+        private CancellationTokenSource activeSemanticRequest;
+        private int semanticRequestRevision;
+        private bool retainSubmittedDraft;
         private int runtimeGeneration;
         private StrategicAdaptationProposal pendingAdaptationProposal;
         private StrategicPipeline pendingStrategicPipeline;
         private int pendingStrategicGeneration;
         private int pendingStrategicOwner;
         private int pendingStrategicIntentId;
         private int pendingStrategicLastPlanId;
         private bool pendingStrategicHadNoPlan;
 
         public StrategicAIProviderResult LatestStrategicInterpretation { get; private set; }
         public StrategicDecisionRecord LatestStrategicDecision { get; private set; }
         public StrategicIntent PendingStrategicIntent => strategicBridge?.PendingIntent;
@@ -111,42 +114,45 @@ namespace OpenEmpires
 
         public void Initialize(ICommanderAIProvider provider, GameSimulation simulation,
             CommanderGoalManager goalManager, CommanderIntentDispatcher dispatcher,
             System.TimeSpan? providerTimeout = null)
         {
             // Awake is not invoked for a MonoBehaviour added by an EditMode test.
             // Keeping initialization idempotent also makes programmatic local hosts safe.
             EnsureLocalSurface();
             ResetVoiceControls(); // Invalidate old voice work before replacing ANY runtime affinity.
             if (semanticGoalManager != null && !semanticGoalManager.IsDisposed)
                 semanticGoalManager.CancelFutureSubscriptions();
             runtimeGeneration++;
+            CancelActiveSemanticRequest();
+            retainSubmittedDraft = false;
             currentRequestGoalId = 0;
             pendingClarification = null;
             ClearActionPlanPreview();
             ClearStrategicPreview();
             lifetime.Cancel();
             lifetime.Dispose();
             lifetime = new CancellationTokenSource();
             strategicBridge?.Dispose();
             strategicBridge = null;
             DetachStrategicPipeline();
             ResetAdvisories();
             ResetStrategicHostControls();
             ResetExplanationState();
             adapter?.ResetHistory();
             Conversation?.Reset();
             transcriptEntries.Clear();
             transcript.Clear();
             if (transcriptText != null) transcriptText.text = string.Empty;
+            RefreshChatBubbles();
             submitting = false;
             adapter = new CommanderAIIntentAdapter(provider, simulation, goalManager, dispatcher,
                 null, providerTimeout);
             semanticProvider = provider as ICommanderSemanticProvider;
             ObserveSemanticProviderSetup(provider);
             semanticSimulation = simulation;
             semanticGoalManager = goalManager;
             semanticDispatcher = dispatcher;
             semanticProviderTimeout = providerTimeout ?? CommanderAIIntentAdapter.DefaultProviderTimeout;
             advisorySimulation = simulation;
             Conversation = new ConversationState(goalManager.PlayerId);
             LatestStrategicInterpretation = null;
@@ -171,24 +177,25 @@ namespace OpenEmpires
             if (interpreter == null) throw new System.ArgumentNullException(nameof(interpreter));
             if (pipeline == null) throw new System.ArgumentNullException(nameof(pipeline));
             if (Conversation == null || Conversation.PlayerId != pipeline.StrategicPlanner.PlayerId)
                 throw new System.ArgumentException(
                     "The strategic runtime must belong to the active Commander player.", nameof(pipeline));
             if (!pipeline.UsesSimulation(advisorySimulation))
                 throw new System.ArgumentException(
                     "The strategic runtime must use the active Commander simulation.",
                     nameof(pipeline));
             if (strategicPipeline != null && !ReferenceEquals(strategicPipeline, pipeline))
                 ResetConversation();
             runtimeGeneration++;
+            CancelActiveSemanticRequest();
             ClearStrategicPreview();
             lifetime?.Cancel();
             lifetime?.Dispose();
             lifetime = new CancellationTokenSource();
             strategicBridge?.Dispose();
             DetachStrategicPipeline();
             ResetAdvisories();
             ResetStrategicHostControls();
             ResetExplanationState();
             strategicPipeline = pipeline;
             strategicBridge = new StrategicAIApprovalBridge(interpreter, pipeline.StrategicPlanner.IntentIds,
                 () => pipeline.CaptureContext(), providerTimeout,
@@ -210,36 +217,43 @@ namespace OpenEmpires
         {
             if (lifetime == null) lifetime = new CancellationTokenSource();
             if (canvasRoot == null) BuildUI();
         }
 
         public async Task<CommanderAIChatSubmission> SubmitMessageAsync(string message)
         {
             string trimmed = (message ?? string.Empty).Trim();
             if (trimmed.Length == 0) return null;
             if(CommanderTacticalStatusProjection.IsInformationQuestion(trimmed))
             {
                 currentRequestGoalId = 0;
+                int informationGeneration = runtimeGeneration;
+                SetLatestRequestState("Interpreting", trimmed);
                 if(trimmed.Length>CommanderSemanticProviderRequest.MaximumPlayerMessageCharacters)
-                {AppendLine("Commander","That question is too long; please shorten it.",false);return null;}
+                {SetLatestRequestState("Failed", "Question is too long.");AppendLine("Commander","That question is too long; please shorten it.",false);return null;}
                 string informationForm=NormalizeExplanationWholeForm(trimmed);
                 if(informationForm=="strategy status"||informationForm=="current strategy status")
                 {
                     AppendLine("Player",trimmed,false);
                     AppendLine("Commander",strategicPipeline!=null&&Conversation!=null&&strategicPipeline.StrategicPlanner.PlayerId==Conversation.PlayerId
                         ?CurrentPlansStatus(strategicPipeline,Conversation.PlayerId):"Strategic Commander is not ready.",false);
+                    SetLatestRequestState("Completed", "Information answered; no gameplay order submitted.");
                     return null;
                 }
-                if(TryHandleExplanationQuery(trimmed))return null;
-                return await SubmitReadOnlyQuestionAsync(trimmed);
+                if(TryHandleExplanationQuery(trimmed))
+                {SetLatestRequestState("Completed", "Information answered; no gameplay order submitted.");return null;}
+                var informationResult = await SubmitReadOnlyQuestionAsync(trimmed);
+                if(this!=null&&informationGeneration==runtimeGeneration)
+                    SetLatestRequestState("Completed", "Information answered; no gameplay order submitted.");
+                return informationResult;
             }
             if (submitting)
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
@@ -281,33 +295,36 @@ namespace OpenEmpires
                 AppendLine("Commander", Conversation.Memory.ToJson(), false);
                 return null;
             }
             if (wholeForm == "focus cavalry")
             {
                 AppendLine("Player", trimmed);
                 Conversation.Memory.RecordCavalryPreference();
                 AppendLine("Commander", "Cavalry attack focus remembered for this match.");
                 return null;
             }
             if (submitting) return null;
             submitting = true;
+            retainSubmittedDraft = false;
+            SetLatestRequestState("Interpreting", trimmed);
             SetCommanderStatus("Commander is interpreting...", false);
             int generation = runtimeGeneration;
             IReadOnlyList<MemoryEntry> memoryAtTurnStart = Conversation.Snapshot();
             strategicBridge?.ClearPending();
             ClearStrategicPreview();
             LatestStrategicInterpretation = null;
             LatestSubmission = null;
             UpdateStrategicControls();
             AppendLine("Player", trimmed);
+            int submittedDraftRevision = inputRevision;
             inputField.interactable = false;
             sendButton.interactable = false;
             try
             {
                 if (semanticProvider != null)
                     return await SubmitSemanticTacticalAsync(trimmed, generation);
 
                 CommanderTextRoute route = intentRouter.Classify(trimmed);
                 if (route == CommanderTextRoute.Rejected)
                 {
                     AppendLine("Commander", "Unsupported or mixed Commander request.");
                     return null;
@@ -393,25 +410,26 @@ namespace OpenEmpires
                     TrackCurrentRequest(tacticalResult?.CommanderSubmission);
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
                     sendButton.interactable = true;
-                    inputField.text = string.Empty;
+                    if (!retainSubmittedDraft && inputRevision == submittedDraftRevision
+                        && inputField.text == trimmed) inputField.text = string.Empty;
                     UpdateStrategicControls();
                     UpdateVoiceUI();
                     if (commanderStatusText != null && commanderStatusText.text == "Commander is interpreting...")
                         SetCommanderStatus("Request finished. Ready for your next message.", false);
                 }
             }
         }
 
         private async Task<CommanderAIChatSubmission> SubmitSemanticTacticalAsync(
             string message, int generation)
         {
             ICommanderSemanticProvider provider = semanticProvider;
@@ -428,30 +446,33 @@ namespace OpenEmpires
             bool sourceCaptured = sourcePipeline != null
                 && TryCaptureOwnedStrategicSource(sourcePipeline, owner,
                     out capturedSourceCount, out capturedSource);
             int sourceCount = sourceCaptured ? capturedSourceCount : 0;
             StrategicAdaptationSource source = sourceCaptured ? capturedSource : null;
             int sourceLastPlanId = sourcePipeline == null ? 0 : LatestRetainedPlanId(sourcePipeline.StrategicPlanner);
             if (message.Length > CommanderSemanticProviderRequest.MaximumPlayerMessageCharacters)
             {
                 AppendLine("Commander", "That request is too long; please shorten it.");
                 return null;
             }
 
+            int requestRevision = ++semanticRequestRevision;
             using (var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
             {
+                activeSemanticRequest = request;
                 request.CancelAfter(semanticProviderTimeout);
                 try
                 {
                     bool IsCurrentScope() => this != null && runtimeGeneration == generation
+                        && semanticRequestRevision == requestRevision
                         && !lifetime.IsCancellationRequested
                         && ReferenceEquals(semanticGoalManager, manager)
                         && ReferenceEquals(semanticSimulation, simulation)
                         && ReferenceEquals(semanticDispatcher, dispatcher)
                         && ReferenceEquals(semanticProvider, provider);
                     var requestTicket = capturedPending?.RequestTicket ?? manager.BeginSemanticRequest(message, generation, IsCurrentScope);
                     semanticStage = "context-projection";
                     CommanderContext context = new CommanderContextBuilder().Build(simulation, manager,
                         message, capturedPending?.GraphDraft?.IsNameConfirmation == true ? null : capturedPending?.Serialize());
                     if (context.PlayerId != owner)
                     {
                         AppendLine("Commander", "Commander ownership changed; ask again.");
@@ -480,94 +501,104 @@ namespace OpenEmpires
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
                         if (this != null && generation == runtimeGeneration
-                            && !lifetime.IsCancellationRequested)
-                            AppendLine("Commander", "Commander AI request timed out. Please try again or use offline commands.");
+                            && semanticRequestRevision == requestRevision && !lifetime.IsCancellationRequested)
+                        {
+                            retainSubmittedDraft = true;
+                            OfferRequestRecovery(message, "Timed out — no order was submitted.");
+                            AppendLine("Commander", "That request timed out. No order was submitted. Choose Retry, Edit, or Cancel.");
+                        }
                         return null;
                     }
                     CommanderSemanticResult result = await translation;
                     semanticStage = "semantic-response";
                     if (this == null || generation != runtimeGeneration
+                        || semanticRequestRevision != requestRevision
                         || !ReferenceEquals(semanticProvider, provider)
                         || !ReferenceEquals(semanticSimulation, simulation)
                         || !ReferenceEquals(semanticGoalManager, manager)
                         || !ReferenceEquals(semanticDispatcher, dispatcher)
                         || Conversation == null || Conversation.PlayerId != owner
                         || request.IsCancellationRequested || lifetime.IsCancellationRequested)
                         return null;
 
                     if (result == null || !result.IsValid)
                     {
+                        SetLatestRequestState("Failed", "The response could not be safely interpreted.");
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
+                        SetLatestRequestState(result.Outcome == CommanderSemanticOutcome.Clarify
+                            ? "Needs clarification" : "Completed", result.SafeExplanation);
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
+                        SetLatestRequestState("Awaiting approval", requestTicket.OriginalInput);
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
                         pendingActionPlan = manager.PrepareActionPlan(result, requestTicket.OriginalInput, generation,
                             strategicPipeline, IsCurrentScope, requestTicket);
+                        SetLatestRequestState("Awaiting approval", requestTicket.OriginalInput);
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
                             AppendLine("Commander", "Strategic Commander is not ready.");
                             return null;
@@ -635,46 +666,58 @@ namespace OpenEmpires
                         strategicPipeline, capturedPending != null);
                     CommanderIntentSubmission submission = manager.SubmitKnownScopedIntent(knownScope, intent,
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
+                    SetLatestRequestState(submission?.CreatedGoal == true ? "Accepted" : "Failed", display);
                     Debug.Log("[Commander] intent=admitted;goal-submitted=" + (submission?.CreatedGoal == true));
                     if (submission?.CreatedGoal == true)
                         RecordAcceptedSemanticNodes(result.Nodes);
                     TrackCurrentRequest(submission);
                     AppendLine("Commander", display);
                     return chatSubmission;
                 }
                 catch (System.OperationCanceledException)
                 {
                     if (this != null && generation == runtimeGeneration
-                        && !lifetime.IsCancellationRequested)
-                        AppendLine("Commander", "Commander AI request timed out. Please try again or use offline commands.");
+                        && semanticRequestRevision == requestRevision && !lifetime.IsCancellationRequested)
+                    {
+                        retainSubmittedDraft = true;
+                        OfferRequestRecovery(message, "Timed out — no order was submitted.");
+                        AppendLine("Commander", "That request timed out. No order was submitted. Choose Retry, Edit, or Cancel.");
+                    }
                     return null;
                 }
                 catch (System.Exception error)
                 {
                     Debug.Log("[Commander] failed-stage=" + semanticStage + ";exception=" + error.GetType().Name);
                     if (this != null && generation == runtimeGeneration
                         && !lifetime.IsCancellationRequested)
+                    {
+                        SetLatestRequestState("Failed", "Translation failed safely; no order was submitted.");
                         AppendLine("Commander", "Commander translation failed safely; no order was submitted.");
+                    }
                     return null;
                 }
+                finally
+                {
+                    if (ReferenceEquals(activeSemanticRequest, request)) activeSemanticRequest = null;
+                }
             }
         }
 
         private void RecordAcceptedSemanticNodes(IReadOnlyList<CommanderSemanticNode> nodes)
         {
             if (Conversation == null || nodes == null) return;
             for (int i = 0; i < nodes.Count; i++)
             {
                 CommanderSemanticNode node = nodes[i];
                 if (node == null) continue;
                 if (node.Type == CommanderSemanticNodeType.EnsureUnitCount
                     && node.UnitType.HasValue && node.Count.HasValue)
@@ -805,39 +848,41 @@ namespace OpenEmpires
             return LatestStrategicDecision;
         }
 
         public void DismissStrategicRecommendation()
         {
             strategicBridge?.ClearPending();
             ClearStrategicPreview();
             UpdateStrategicControls();
         }
 
         public void ResetConversation()
         {
+            CancelActiveSemanticRequest();
             if (semanticGoalManager != null && !semanticGoalManager.IsDisposed)
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
             adapter?.ResetHistory();
             transcript.Clear();
             transcriptEntries.Clear();
             if (transcriptText != null) transcriptText.text = string.Empty;
+            RefreshChatBubbles();
             strategicBridge?.Reset();
             ResetAdvisories();
             LatestStrategicInterpretation = null;
             LatestStrategicDecision = null;
             LatestSubmission = null;
             ResetExplanationState();
             ResetStrategicHostControls();
             submitting = false;
             if (inputField != null)
             {
                 inputField.text = string.Empty;
                 inputField.interactable = adapter != null;
@@ -852,41 +897,43 @@ namespace OpenEmpires
         {
             bool actionAvailable = !submitting && pendingActionPlan != null;
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
-            if (strategicApprovalRow != null) strategicApprovalRow.SetActive(actionAvailable || strategicBridge?.PendingIntent != null);
+            // Action plans use the expanded dock's explicit preview decisions;
+            // retain the existing row for strategic recommendations only.
+            if (strategicApprovalRow != null) strategicApprovalRow.SetActive(!actionAvailable && strategicBridge?.PendingIntent != null);
             UpdateStrategicHostControls();
         }
 
         public async Task<CommanderAIChatSubmission> SubmitCurrentInputAsync()
         {
             if (submitting) return null;
             string text = InputText;
             if (text.Length > CommanderSemanticProviderRequest.MaximumPlayerMessageCharacters)
             {
                 SetCommanderStatus("This request is too long. Shorten it to 1024 characters before sending.",true);
                 UpdatePresentation();
                 return null;
             }
             if (voiceController?.State == CommanderVoiceState.Preview) voiceController.Cancel();
             CommanderAIChatSubmission result = await SubmitMessageAsync(text);
-            if (!submitting && InputText == text) InputText = string.Empty;
+            if (!submitting && !retainSubmittedDraft && InputText == text) InputText = string.Empty;
             return result;
         }
 
         private async void SubmitFromUI()
         {
             if (!ClaimPresentationAction()) return;
             await SubmitCurrentInputAsync();
         }
 
         private void AppendLine(string speaker, string message, bool remember = true)
         {
             string bounded = message ?? string.Empty;
@@ -894,24 +941,25 @@ namespace OpenEmpires
                 bounded = bounded.Substring(0, TranscriptMessageMaximum);
             if (speaker == "Commander") SetCommanderStatus(bounded,
                 bounded.Contains("failed") || bounded.Contains("timed out") || bounded.Contains("invalid")
                 || bounded.Contains("unavailable") || bounded.Contains("credits") || bounded.Contains("rate-limited"));
             transcriptEntries.Add((speaker ?? string.Empty) + ":" + System.Environment.NewLine + bounded);
             while (transcriptEntries.Count > TranscriptCapacity) transcriptEntries.RemoveAt(0);
             transcript.Clear();
             for (int i = 0; i < transcriptEntries.Count; i++)
             {
                 if (i > 0) transcript.AppendLine().AppendLine();
                 transcript.Append(transcriptEntries[i]);
             }
+            RefreshChatBubbles();
             if (transcriptText != null)
             {
                 transcriptText.text = transcript.ToString();
                 if (transcriptScroll != null)
                 {
                     Canvas.ForceUpdateCanvases();
                     transcriptScroll.StopMovement();
                     transcriptScroll.verticalNormalizedPosition = 0f;
                 }
             }
             if (remember && Conversation != null)
             {
@@ -959,24 +1007,25 @@ namespace OpenEmpires
         }
 
         private static string NormalizeWholeForm(string message)
         {
             string text = Regex.Replace((message ?? string.Empty).Trim().ToLowerInvariant(), @"\s+", " ");
             if (text.EndsWith(".", System.StringComparison.Ordinal))
                 text = text.TrimEnd('.').TrimEnd();
             return text;
         }
 
         private void OnDestroy()
         {
+            CancelActiveSemanticRequest();
             if (destroyCleanupComplete) return;
             destroyCleanupComplete = true;
             pendingClarification = null;
             ClearActionPlanPreview();
             ClearStrategicPreview();
             if (bootstrapWait != null) StopCoroutine(bootstrapWait);
             lifetime?.Cancel();
             strategicBridge?.Dispose();
             DetachStrategicPipeline();
             ResetAdvisories();
             advisorySimulation = null;
             ResetStrategicHostControls();
@@ -1191,30 +1240,39 @@ namespace OpenEmpires
             strategicHostRow.transform.SetSiblingIndex(4);
             strategicApprovalRow.transform.SetSiblingIndex(5);
             voiceRootObject.transform.SetSiblingIndex(6);
             inputRow.transform.SetSiblingIndex(7);
             strategicHostRow.SetActive(false);
             strategicApprovalRow.SetActive(false);
         }
 
         private void RefreshPanelSize()
         {
             if (commanderPanel == null || canvasRoot == null) return;
             var canvasRect = canvasRoot.GetComponent<RectTransform>();
-            commanderPanel.sizeDelta = new Vector2(Mathf.Min(460, Mathf.Max(320, canvasRect.rect.width - 32)),
-                Mathf.Min(540, Mathf.Max(400, canvasRect.rect.height - 96)));
-            // The existing player-list HUD ends at267 reference pixels in local
-            // play. Keep it visible, without pushing Commander off the right edge.
-            commanderPanel.anchoredPosition = new Vector2(
-                Mathf.Clamp(canvasRect.rect.width-commanderPanel.sizeDelta.x-16,16,280),-72);
+            float scale = Mathf.Max(.01f, canvasRoot.GetComponent<Canvas>().scaleFactor);
+            Rect safe = Screen.safeArea;
+            float left = safe.width > 0 ? safe.xMin / scale : 0;
+            float right = safe.width > 0 ? safe.xMax / scale : canvasRect.rect.width;
+            float top = safe.height > 0 ? safe.yMax / scale : canvasRect.rect.height;
+            float bottom = safe.height > 0 ? safe.yMin / scale : 0;
+            // Preserve the real player-list HUD at the left; a zoomed Editor
+            // preview is not a reason to translate every HUD element.
+            float x = Mathf.Max(left + 16, 280);
+            float width = Mathf.Clamp(Mathf.Min(420, right - x - 16), 320, 460);
+            if (x + width + 16 > right) x = Mathf.Max(left + 8, right - width - 16);
+            float yInset = Mathf.Min(72, Mathf.Max(16, top - bottom - 360));
+            float height = Mathf.Clamp(top - bottom - yInset - 16, 320, 620);
+            commanderPanel.sizeDelta = new Vector2(width, height);
+            commanderPanel.anchoredPosition = new Vector2(x, -(canvasRect.rect.height - top + yInset));
         }
 
         private Button StrategyButton(Transform parent, string label, float left, float width,
             UnityEngine.Events.UnityAction action)
         {
             GameObject obj = UIObject(label, parent);
             SetRect(obj.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero,
                 new Vector2(left, 48), new Vector2(left + width, 78));
             var image = obj.AddComponent<Image>();
             image.color = new Color(0.18f, 0.29f, 0.42f, 1f);
             var button = obj.AddComponent<Button>();
             button.targetGraphic = image;

## Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Presentation.cs
CurrentSHA256:5544A0F72D93CF89223877C45D85D2F9CE45A58CC6A2AB4D33A5559278CE8A0D
BaselineSHA256:139FA7FE578773EB68CB07D06DF7AE8ED5B88C8165363E069692FF01EAF975CC
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-5-baseline\\Assets__Scripts__AI__Commander__Phase4A__CommanderChatUI.Presentation.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.Presentation.cs"
index a8d0c37..6e95701 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-5-baseline\\Assets__Scripts__AI__Commander__Phase4A__CommanderChatUI.Presentation.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.Presentation.cs"	
@@ -3,24 +3,33 @@ using UnityEngine;
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
+        private TMP_Text latestRequestText, pendingQuestionText, expandedApprovalText, launcherText;
+        private TMP_Text expandedReviewText;
+        private GameObject requestRecovery, pendingQuestion, expandedApproval, expandedReview;
+        private Button retryRequestButton, editRequestButton, cancelRequestButton;
+        private Button chatTabButton, tasksTabButton;
+        private RectTransform bubbleContent;
+        private string latestRequestState = "Ready", latestRequestDetail = "";
+        private string recoveryDraft;
+        private bool recoveryAvailable, tasksVisible;
         private bool expanded;
         private int inputRevision, voiceDraftRevision;
         private string voiceOwnedDraft;
         private int lastPresentationActionFrame = -1;
         private int lastEscapeActionFrame = -1;
 
         public bool IsExpanded => expanded;
 
         private string VoiceCaptureLimitLabel
         {
             get
             {
@@ -96,27 +105,79 @@ namespace OpenEmpires
         {
             // Only physical UI callbacks use this gate. Programmatic text APIs retain
             // their own request lease. One click/Enter cannot submit and approve in a frame.
             if (lastPresentationActionFrame == Time.frameCount) return false;
             lastPresentationActionFrame = Time.frameCount;
             return true;
         }
 
         private void InitializePresentation()
         {
             voiceOwnedDraft = null;
             lastPresentationActionFrame = -1;
+            recoveryAvailable = false;
+            recoveryDraft = null;
+            tasksVisible = false;
+            SetLatestRequestState("Ready", "Commander is ready.");
             MinimizeCommander();
         }
 
+        private void SetLatestRequestState(string state, string detail)
+        {
+            latestRequestState = state ?? "Ready";
+            latestRequestDetail = detail ?? string.Empty;
+            UpdatePresentation();
+        }
+
+        private void CancelActiveSemanticRequest()
+        {
+            semanticRequestRevision++;
+            activeSemanticRequest?.Cancel();
+            activeSemanticRequest = null;
+            recoveryAvailable = false;
+        }
+
+        private void OfferRequestRecovery(string original, string reason)
+        {
+            recoveryDraft = original;
+            recoveryAvailable = true;
+            SetLatestRequestState("Failed", reason);
+        }
+
+        private void RetryLatestRequest()
+        {
+            if (!recoveryAvailable || submitting || recoveryDraft == null || InputText != recoveryDraft) return;
+            recoveryAvailable = false; // one physical action consumes this recovery offer
+            _ = SubmitCurrentInputAsync();
+        }
+
+        private void EditLatestRequest()
+        {
+            if (!recoveryAvailable) return;
+            recoveryAvailable = false;
+            inputField?.ActivateInputField();
+            SetLatestRequestState("Failed", "Edit the retained draft, then Send when ready.");
+        }
+
+        private void CancelLatestRequest()
+        {
+            if (submitting)
+            {
+                retainSubmittedDraft = true;
+                CancelActiveSemanticRequest();
+            }
+            recoveryAvailable = false;
+            SetLatestRequestState("Cancelled", "No new order will be submitted from that request.");
+        }
+
         private void OnDraftChanged(string value)
         {
             inputRevision++;
             if (inputLengthText != null)
                 inputLengthText.text = value.Length + " / " + CommanderSemanticProviderRequest.MaximumPlayerMessageCharacters
                     + (value.Length > CommanderSemanticProviderRequest.MaximumPlayerMessageCharacters ? " — shorten before sending" : " characters");
             if (sendButton != null) sendButton.interactable = !submitting && adapter != null
                 && value.Length <= CommanderSemanticProviderRequest.MaximumPlayerMessageCharacters;
         }
 
         private void ReleaseVoiceDraft()
         {
@@ -162,79 +223,298 @@ namespace OpenEmpires
             body.rectTransform.anchorMin = new Vector2(0,1); body.rectTransform.anchorMax = Vector2.one;
             body.rectTransform.pivot = new Vector2(.5f,1); body.rectTransform.sizeDelta = Vector2.zero;
             body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
             scroll.content = body.rectTransform;
             var buttons = new GameObject[actions.Length];
             for (int i=0;i<actions.Length;i++) buttons[i] = CompactButton(card.transform,"Action"+i,actions[i],callbacks[i]).gameObject;
             PanelRow("Actions",card.transform,buttons);
             return card;
         }
 
         private void BuildPresentationUI()
         {
+            var transcriptViewport = transcriptScroll.viewport;
+            var bubbles = UIObject("BubbleContent", transcriptViewport.transform);
+            bubbleContent = bubbles.GetComponent<RectTransform>();
+            bubbleContent.anchorMin = new Vector2(0, 1);
+            bubbleContent.anchorMax = Vector2.one;
+            bubbleContent.pivot = new Vector2(.5f, 1);
+            bubbleContent.sizeDelta = Vector2.zero;
+            var bubbleLayout = bubbles.AddComponent<VerticalLayoutGroup>();
+            bubbleLayout.padding = new RectOffset(8, 8, 8, 8);
+            bubbleLayout.spacing = 8;
+            bubbleLayout.childControlWidth = bubbleLayout.childControlHeight = true;
+            bubbleLayout.childForceExpandWidth = true; bubbleLayout.childForceExpandHeight = false;
+            bubbles.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
+            transcriptScroll.content = bubbleContent;
+            transcriptText.gameObject.SetActive(false); // Retain exact transcript for API and legacy evidence.
+            RefreshChatBubbles();
             compactCommander = UIObject("CompactCommander",canvasRoot.transform);
             var rect = compactCommander.GetComponent<RectTransform>();
             rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
             // Leave the existing right-edge debug/HUD counters accessible.
-            rect.anchoredPosition = new Vector2(-110,-72); rect.sizeDelta = new Vector2(310,0);
-            compactCommander.AddComponent<Image>().color = new Color(.035f,.045f,.06f,.94f);
+            rect.anchoredPosition = new Vector2(-18,-72); rect.sizeDelta = new Vector2(178,0);
+            compactCommander.AddComponent<Image>().color = new Color(.055f,.065f,.075f,1f);
             var layout = compactCommander.AddComponent<VerticalLayoutGroup>();
             layout.padding = new RectOffset(8,8,8,8); layout.spacing = 6;
             layout.childControlWidth = layout.childControlHeight = true;
             layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
             compactCommander.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
             var open = CompactButton(compactCommander.transform,"Open","Commander",OpenCommander);
+            launcherText = open.GetComponentInChildren<TMP_Text>();
             var settings = CompactButton(compactCommander.transform,"VoiceOptions","Voice",()=>{if(voiceSetupVisible)HideVoiceSetup();else ShowVoiceSetup();});
             var textSettings = CompactButton(compactCommander.transform,"TextAIOptions","Text AI",()=>{if(semanticSetupVisible)HideSemanticSetup();else ShowSemanticSetup();});
             compactRecord = CompactButton(compactCommander.transform,"Record","● Record",OnPttButtonClicked);
             compactRecordLabel = compactRecord.GetComponentInChildren<TMP_Text>();
             compactCancel = CompactButton(compactCommander.transform,"CancelVoice","Cancel",OnVoiceCancelClicked);
-            PanelRow("Launcher",compactCommander.transform,open.gameObject,compactRecord.gameObject,compactCancel.gameObject,settings.gameObject);
+            PanelRow("Launcher",compactCommander.transform,open.gameObject);
             compactCommander.transform.Find("Launcher").SetAsFirstSibling();
-            PanelElement(textSettings.gameObject,28);
+            settings.gameObject.SetActive(false);
+            textSettings.gameObject.SetActive(false);
             compactStatus = Text("Status",compactCommander.transform,"Preparing Commander…",12,TextAlignmentOptions.Left);
-            compactStatus.richText = false; PanelElement(compactStatus.gameObject,38);
+            compactStatus.richText = false; PanelElement(compactStatus.gameObject,28);
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
+            var title = commanderPanel.Find("Title")?.GetComponent<TMP_Text>();
+            if (title != null) title.text = "COMMANDER";
+            commanderPanel.GetComponent<Image>().color = new Color(.065f,.075f,.085f,1f);
+            latestRequestText = Text("LatestRequest",commanderPanel,"Ready — Commander is ready.",14,TextAlignmentOptions.Left);
+            latestRequestText.richText = false; PanelElement(latestRequestText.gameObject,36);
+            latestRequestText.transform.SetSiblingIndex(2);
+            var chat = CompactButton(commanderPanel,"ChatTab","Chat",()=>{tasksVisible=false;UpdatePresentation();});
+            var tasks = CompactButton(commanderPanel,"TasksTab","Tasks",()=>{tasksVisible=true;UpdatePresentation();});
+            chatTabButton = chat; tasksTabButton = tasks;
+            var views = PanelRow("Views",commanderPanel,chat.gameObject,tasks.gameObject);
+            views.transform.SetSiblingIndex(3);
+            var settingsButton = CompactButton(commanderPanel,"Settings","Settings",()=>{if(semanticSetupVisible)HideSemanticSetup();else ShowSemanticSetup();});
+            PanelElement(settingsButton.gameObject,28);
+            settingsButton.GetComponent<LayoutElement>().ignoreLayout = true;
+            SetRect(settingsButton.GetComponent<RectTransform>(),Vector2.one,Vector2.one,new Vector2(-122,-37),new Vector2(-52,-9));
             var minimize = CompactButton(commanderPanel,"Minimize","Minimize Commander",MinimizeCommander);
             PanelElement(minimize.gameObject,32);
             minimize.GetComponent<LayoutElement>().ignoreLayout = true;
-            SetRect(minimize.GetComponent<RectTransform>(),Vector2.one,Vector2.one,new Vector2(-150,-38),new Vector2(-12,-10));
+            minimize.GetComponentInChildren<TMP_Text>().text="−";
+            SetRect(minimize.GetComponent<RectTransform>(),Vector2.one,Vector2.one,new Vector2(-44,-37),new Vector2(-12,-9));
+            pendingQuestion = UIObject("PendingClarification",commanderPanel);
+            pendingQuestion.AddComponent<Image>().color = new Color(.14f,.19f,.23f,1f);
+            pendingQuestionText = Text("Question",pendingQuestion.transform,"",14,TextAlignmentOptions.Left);
+            pendingQuestionText.richText=false;
+            SetRect(pendingQuestionText.rectTransform,Vector2.zero,Vector2.one,new Vector2(8,4),new Vector2(-8,-4));
+            PanelElement(pendingQuestion,64);pendingQuestion.transform.SetSiblingIndex(4);
+            expandedApproval = UIObject("Approval",commanderPanel);
+            expandedApproval.AddComponent<Image>().color = new Color(.14f,.19f,.23f,1f);
+            PanelElement(expandedApproval,168,true);expandedApproval.transform.SetSiblingIndex(5);
+            var approvalScroll = UIObject("PreviewScroll",expandedApproval.transform);
+            SetRect(approvalScroll.GetComponent<RectTransform>(),Vector2.zero,Vector2.one,
+                new Vector2(8,44),new Vector2(-8,-8));
+            var approvalScrollRect = approvalScroll.AddComponent<ScrollRect>();
+            approvalScrollRect.horizontal=false;
+            approvalScrollRect.vertical=true;
+            approvalScrollRect.movementType=ScrollRect.MovementType.Clamped;
+            var approvalViewport=UIObject("Viewport",approvalScroll.transform);
+            SetRect(approvalViewport.GetComponent<RectTransform>(),Vector2.zero,Vector2.one,
+                Vector2.zero,Vector2.zero);
+            approvalViewport.AddComponent<Image>().color=new Color(0,0,0,.1f);
+            approvalViewport.AddComponent<RectMask2D>();
+            approvalScrollRect.viewport=approvalViewport.GetComponent<RectTransform>();
+            expandedApprovalText=Text("Details",approvalViewport.transform,"",14,TextAlignmentOptions.TopLeft);
+            expandedApprovalText.richText=false;
+            expandedApprovalText.textWrappingMode=TextWrappingModes.Normal;
+            expandedApprovalText.rectTransform.anchorMin=new Vector2(0,1);
+            expandedApprovalText.rectTransform.anchorMax=Vector2.one;
+            expandedApprovalText.rectTransform.pivot=new Vector2(.5f,1);
+            expandedApprovalText.rectTransform.sizeDelta=Vector2.zero;
+            expandedApprovalText.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
+            approvalScrollRect.content=expandedApprovalText.rectTransform;
+            var approvePlan=CompactButton(expandedApproval.transform,"ApprovePlan","Approve plan",()=>HandleActionPlanControl("approve plan"));
+            SetRect(approvePlan.GetComponent<RectTransform>(),new Vector2(0,0),new Vector2(.5f,0),
+                new Vector2(8,7),new Vector2(-4,37));
+            var rejectPlan=CompactButton(expandedApproval.transform,"RejectPlan","Reject plan",()=>HandleActionPlanControl("cancel plan"));
+            SetRect(rejectPlan.GetComponent<RectTransform>(),new Vector2(.5f,0),new Vector2(1,0),
+                new Vector2(4,7),new Vector2(-8,37));
+            requestRecovery = UIObject("RequestRecovery",commanderPanel);
+            retryRequestButton=CompactButton(requestRecovery.transform,"Retry","Retry",RetryLatestRequest);
+            editRequestButton=CompactButton(requestRecovery.transform,"Edit","Edit",EditLatestRequest);
+            cancelRequestButton=CompactButton(requestRecovery.transform,"Cancel","Cancel",CancelLatestRequest);
+            var recoveryLayout=requestRecovery.AddComponent<HorizontalLayoutGroup>();
+            recoveryLayout.spacing=8; recoveryLayout.childControlWidth=recoveryLayout.childControlHeight=true;
+            recoveryLayout.childForceExpandWidth=true;recoveryLayout.childForceExpandHeight=true;
+            PanelElement(requestRecovery,36);
+            expandedReview=UIObject("VoiceReview",commanderPanel);
+            expandedReview.AddComponent<Image>().color=new Color(.13f,.19f,.21f,1f);
+            PanelElement(expandedReview,68);
+            expandedReviewText=Text("ReviewText",expandedReview.transform,
+                "Reviewed transcript — not sent",13,TextAlignmentOptions.Left);
+            expandedReviewText.richText=false;
+            SetRect(expandedReviewText.rectTransform,new Vector2(0,1),Vector2.one,
+                new Vector2(8,-25),new Vector2(-8,-4));
+            var sendReviewed=CompactButton(expandedReview.transform,"SendReviewed","Send reviewed",()=>{_ = SubmitCurrentInputAsync();});
+            var editReviewed=CompactButton(expandedReview.transform,"EditReviewed","Edit",()=>inputField?.ActivateInputField());
+            var discardReviewed=CompactButton(expandedReview.transform,"DiscardReviewed","Discard",OnVoiceCancelClicked);
+            SetRect(sendReviewed.GetComponent<RectTransform>(),new Vector2(0,0),new Vector2(.5f,0),new Vector2(8,5),new Vector2(-4,35));
+            SetRect(editReviewed.GetComponent<RectTransform>(),new Vector2(.5f,0),new Vector2(.75f,0),new Vector2(4,5),new Vector2(-4,35));
+            SetRect(discardReviewed.GetComponent<RectTransform>(),new Vector2(.75f,0),new Vector2(1,0),new Vector2(4,5),new Vector2(-8,35));
             inputLengthText = Text("RequestLength",commanderPanel,"0 / 1024 characters",11,TextAlignmentOptions.Left);
             inputLengthText.richText = false; PanelElement(inputLengthText.gameObject,18);
+            requestRecovery.transform.SetSiblingIndex(commanderPanel.childCount-4);
+            expandedReview.transform.SetSiblingIndex(commanderPanel.childCount-3);
+            inputLengthText.transform.SetSiblingIndex(commanderPanel.childCount-2);
+            var inputRow=commanderPanel.Find("TextSubmission");
+            if(inputRow!=null)
+            {
+                inputRow.SetAsLastSibling();
+                var composerLayout=inputRow.GetComponent<HorizontalLayoutGroup>();
+                composerLayout.childForceExpandWidth=false;
+                composerLayout.childForceExpandHeight=true;
+                var inputLayout=inputRow.Find("Input")?.GetComponent<LayoutElement>();
+                if(inputLayout!=null)
+                {
+                    inputLayout.minHeight=inputLayout.preferredHeight=64;
+                    inputLayout.flexibleWidth=1;
+                }
+                var sendLayout=inputRow.Find("Send")?.GetComponent<LayoutElement>();
+                if(sendLayout!=null)
+                {
+                    sendLayout.minHeight=sendLayout.preferredHeight=64;
+                    sendLayout.preferredWidth=76;
+                    sendLayout.flexibleWidth=0;
+                }
+            }
+            var inputRect=commanderPanel.Find("TextSubmission/Input")?.GetComponent<RectTransform>();
+            if(inputRect!=null)
+            {
+                inputRect.parent.GetComponent<LayoutElement>().minHeight=68;
+                inputRect.parent.GetComponent<LayoutElement>().preferredHeight=68;
+                inputRect.GetComponent<Image>().color=new Color(.12f,.14f,.16f,1f);
+            }
+            inputField.lineType=TMP_InputField.LineType.MultiLineNewline;
+            inputField.textComponent.fontSize=14;
+            transcriptText.fontSize=15;
+            var transcriptImage=transcriptScroll.GetComponent<Image>();
+            if(transcriptImage!=null)transcriptImage.color=new Color(.09f,.105f,.12f,1f);
             inputField.onValueChanged.AddListener(OnDraftChanged);
             UpdatePresentation();
         }
 
+        private void RefreshChatBubbles()
+        {
+            if (bubbleContent == null) return;
+            for (int i = bubbleContent.childCount - 1; i >= 0; i--)
+            {
+                var old = bubbleContent.GetChild(i);
+                old.SetParent(null, false);
+                if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
+            }
+            foreach (string entry in transcriptEntries)
+            {
+                int marker = entry.IndexOf(':');
+                string speaker = marker < 0 ? "Commander" : entry.Substring(0, marker);
+                string body = marker < 0 ? entry : entry.Substring(marker + 1).Trim();
+                bool player = speaker == "Player";
+                var bubble = UIObject(player ? "PlayerBubble" : "CommanderBubble", bubbleContent);
+                bubble.AddComponent<Image>().color = player
+                    ? new Color(.17f, .28f, .33f, 1f) : new Color(.13f, .15f, .17f, 1f);
+                var layout = bubble.AddComponent<VerticalLayoutGroup>();
+                layout.padding = new RectOffset(10, 10, 8, 8);
+                layout.spacing = 4;
+                layout.childControlWidth = layout.childControlHeight = true;
+                layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
+                var name = Text("Speaker", bubble.transform, player ? "YOU" : "COMMANDER", 12,
+                    TextAlignmentOptions.Left);
+                name.richText = false; name.color = new Color(.6f, .83f, .87f, 1f);
+                PanelElement(name.gameObject, 16);
+                var message = Text("Message", bubble.transform, body, 15, TextAlignmentOptions.Left);
+                message.richText = false;
+                message.textWrappingMode = TextWrappingModes.Normal;
+                message.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
+                bubble.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
+            }
+            Canvas.ForceUpdateCanvases();
+            transcriptScroll.verticalNormalizedPosition = 0;
+        }
+
         private void UpdatePresentation()
         {
             if (compactCommander == null) return;
             RefreshTaskBoard();
             RefreshSemanticSetupStatus();
+            bool reviewing = VoiceState == CommanderVoiceState.Preview;
             if (commanderPanel != null) commanderPanel.gameObject.SetActive(expanded);
             compactCommander.SetActive(!expanded);
+            // LatestRequest is the visible lifecycle source; keep the legacy text only
+            // as internal state so stale provider/debug status cannot crowd the dock.
+            if (commanderStatusText != null) commanderStatusText.gameObject.SetActive(false);
+            if (currentPlanStatusText != null)
+                currentPlanStatusText.gameObject.SetActive(selectedPlanId != 0);
+            if (voiceRootObject != null)
+            {
+                bool voiceNeedsStatus = VoiceState == CommanderVoiceState.Recording
+                    || VoiceState == CommanderVoiceState.Transcribing
+                    || VoiceState == CommanderVoiceState.Error;
+                voiceRootObject.GetComponent<LayoutElement>().minHeight = voiceNeedsStatus ? 56 : 36;
+                voiceRootObject.GetComponent<LayoutElement>().preferredHeight = voiceNeedsStatus ? 56 : 36;
+                if (voiceStatusText != null) voiceStatusText.gameObject.SetActive(voiceNeedsStatus);
+                voiceRootObject.SetActive(!reviewing);
+            }
+            if (latestRequestText != null)
+            {
+                string detail=latestRequestDetail.Length>90?latestRequestDetail.Substring(0,87)+"…":latestRequestDetail;
+                latestRequestText.text=latestRequestState+(detail.Length==0?"":" — "+detail);
+            }
+            if (launcherText != null) launcherText.text="Commander"+(taskBadgeText!=null&&taskBadgeText.gameObject.activeSelf?" • "+taskBadgeText.text:"")
+                +(pendingClarification!=null||pendingActionPlan!=null||recoveryAvailable?" !":"");
+            if(chatTabButton!=null)chatTabButton.GetComponent<Image>().color=tasksVisible?new Color(.13f,.15f,.17f,1f):new Color(.19f,.34f,.42f,1f);
+            if(tasksTabButton!=null)tasksTabButton.GetComponent<Image>().color=tasksVisible?new Color(.19f,.34f,.42f,1f):new Color(.13f,.15f,.17f,1f);
+            bool hasApproval = pendingActionPlan!=null||strategicBridge?.PendingIntent!=null;
+            // A pending preview replaces Chat's transcript scroll, rather than
+            // nesting an approval scroller alongside the conversation scroller.
+            if(transcriptScroll!=null)transcriptScroll.gameObject.SetActive(!tasksVisible&&!hasApproval);
+            if(taskBoardScroll!=null)taskBoardScroll.gameObject.SetActive(tasksVisible);
+            if(pendingQuestion!=null)
+            {
+                pendingQuestion.SetActive(pendingClarification!=null);
+                if(pendingClarification!=null)pendingQuestionText.text="Needs clarification — "+pendingClarification.Question;
+            }
+            if(expandedApproval!=null)
+            {
+                expandedApproval.SetActive(hasApproval&&!tasksVisible);
+                if(hasApproval)expandedApprovalText.text=pendingActionPlan?.Preview??"Strategic change proposed. Review the effects before approving.";
+                var approvePlan=expandedApproval.transform.Find("ApprovePlan");
+                var rejectPlan=expandedApproval.transform.Find("RejectPlan");
+                if(approvePlan!=null)approvePlan.gameObject.SetActive(pendingActionPlan!=null);
+                if(rejectPlan!=null)rejectPlan.gameObject.SetActive(pendingActionPlan!=null);
+            }
+            if(requestRecovery!=null)requestRecovery.SetActive(recoveryAvailable||submitting&&semanticProvider!=null);
+            if(expandedReview!=null)
+            {
+                expandedReview.SetActive(reviewing);
+                if(reviewing)expandedReviewText.text="Reviewed transcript — not sent. Edit or Send: "
+                    +(InputText.Length>72?InputText.Substring(0,69)+"…":InputText);
+            }
+            if(retryRequestButton!=null)retryRequestButton.gameObject.SetActive(recoveryAvailable);
+            if(editRequestButton!=null)editRequestButton.gameObject.SetActive(recoveryAvailable);
+            if(cancelRequestButton!=null)cancelRequestButton.gameObject.SetActive(recoveryAvailable||submitting&&semanticProvider!=null);
             BoundSetupCard(voiceSetupCard,532);
             BoundSetupCard(semanticSetupCard,336);
-            bool reviewing = VoiceState == CommanderVoiceState.Preview;
             compactReview.SetActive(reviewing);
             bool approving = pendingActionPlan != null || strategicBridge?.PendingIntent != null;
-            compactApproval.SetActive(approving);
+            compactApproval.SetActive(false);
             compactApproveLabel.text = pendingActionPlan != null ? "Approve plan" : "Review strategy";
             if (reviewing) compactReviewText.text = InputText;
             if (approving) compactApprovalText.text = pendingActionPlan?.Preview
                 ?? "A strategy is awaiting consent. Open Details to review it and use the existing strategy confirmation controls.";
             bool busy = VoiceState == CommanderVoiceState.Recording || VoiceState == CommanderVoiceState.Transcribing;
             compactCancel.gameObject.SetActive(busy);
             compactRecord.interactable = voiceController != null && !submitting && CommanderVoiceSettings.VoiceEnabled
                 && (VoiceState == CommanderVoiceState.Idle || VoiceState == CommanderVoiceState.Error || VoiceState == CommanderVoiceState.Recording);
             compactRecordLabel.text = VoiceState == CommanderVoiceState.Recording ? "■ Stop" : "● Record";
             compactStatus.text = VoiceState == CommanderVoiceState.Recording ? VoiceCaptureActivityLabel
                 : VoiceState == CommanderVoiceState.Transcribing ? "Transcribing — Cancel available"
                 : VoiceState == CommanderVoiceState.Error ? "Voice unavailable — open Commander for details"

## Assets/Scripts/AI/Commander/Phase5A/CommanderChatUI.ActionPlans.cs
CurrentSHA256:3CEDF85432DC1A66B2DA36033E95629BBDD21605F2B3DE9D2E78B5902CF2685E
BaselineSHA256:5517D93E3A743EB70CB0EA3587304FAB9215FCE27FF53723AC2C4726FA48C2E1
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-5-baseline\\CommanderChatUI.ActionPlans.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderChatUI.ActionPlans.cs"
index 67b6879..8f6e266 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-5-baseline\\CommanderChatUI.ActionPlans.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5A\\CommanderChatUI.ActionPlans.cs"	
@@ -28,47 +28,52 @@ namespace OpenEmpires
                     if (active != null && semanticGoalManager.CancelActionPlan(active))
                     {
                         AppendLine("Commander", "Action plan cancelled and reservations released. Already completed or issued gameplay is not rolled back.");
                         return true;
                     }
                 }
                 AppendLine("Commander", "No action plan is awaiting approval.");
                 return true;
             }
             if (cancel)
             {
                 ClearActionPlanPreview();
+                SetLatestRequestState("Cancelled", "Action plan dismissed; nothing started.");
                 AppendLine("Commander", "Action plan dismissed; nothing started.");
                 return true;
             }
             var candidate = pendingActionPlan;
             pendingActionPlan = null;
             UpdateStrategicControls();
             try
             {
                 var graph = semanticGoalManager?.ApproveActionPlan(candidate, runtimeGeneration, strategicPipeline);
                 if (graph == null)
                 {
                     candidate.Cancel();
+                    SetLatestRequestState("Failed", "Action plan changed or expired; nothing started.");
                     AppendLine("Commander", "Action plan is stale or changed; nothing started. Request a fresh preview.");
                     return true;
                 }
                 var submission = semanticDispatcher.SubmitSemanticGraph(graph);
                 if (submission?.CreatedGoal == true) RecordAcceptedSemanticNodes(candidate.Interpretation.Nodes);
                 TrackCurrentRequest(submission);
+                SetLatestRequestState(submission?.CreatedGoal == true ? "Accepted" : "Failed",
+                    submission?.Response ?? "Action plan could not be admitted safely.");
                 AppendLine("Commander", submission?.Response ?? "Action plan could not be admitted safely.");
             }
             catch (Exception error) when (error is InvalidOperationException || error is ArgumentException)
             {
                 candidate.Cancel();
+                SetLatestRequestState("Failed", "Action plan admission did not finish safely.");
                 AppendLine("Commander", "Action plan admission did not finish safely. Check its current goal status before retrying.");
             }
             return true;
         }
 
         private void ClearActionPlanPreview()
         {
             pendingActionPlan?.Cancel();
             pendingActionPlan = null;
             UpdateStrategicControls();
         }
     }

## Assets/Scripts/AI/Commander/Phase5B/CommanderChatUI.TaskBoard.cs
CurrentSHA256:89DB86100D7425AB5E1D294D50685035B3317341E7C1CEF9232851E9BA0188ED
BaselineSHA256:DC3CFB1256D38FA8F084D63CBDEFDB3D6CC1D3FEA938B7663B3B59FF1A611861
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-5-baseline\\Assets__Scripts__AI__Commander__Phase5B__CommanderChatUI.TaskBoard.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5B\\CommanderChatUI.TaskBoard.cs"
index 56e3c2e..f5f0b9a 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-5-baseline\\Assets__Scripts__AI__Commander__Phase5B__CommanderChatUI.TaskBoard.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5B\\CommanderChatUI.TaskBoard.cs"	
@@ -3,39 +3,40 @@ using TMPro;
 using UnityEngine;
 using UnityEngine.UI;
 
 namespace OpenEmpires
 {
     public sealed partial class CommanderChatUI
     {
         private TMP_Text taskBadgeText;
         private ScrollRect taskBoardScroll;
         private RectTransform taskBoardContent;
         private string taskBoardFingerprint;
         private int currentRequestGoalId;
+        private bool taskHistoryExpanded;
 
         private void TrackCurrentRequest(CommanderIntentSubmission submission)
         {
             currentRequestGoalId = submission?.CreatedGoal == true
                 ? submission.Resolution?.Goal?.GoalId ?? 0 : 0;
         }
 
         private void BuildTaskBoardUI()
         {
             taskBadgeText = Text("TaskBadge", compactCommander.transform, string.Empty, 12, TextAlignmentOptions.Left);
             taskBadgeText.richText = false;
             PanelElement(taskBadgeText.gameObject, 20);
             var board = UIObject("TaskBoard", commanderPanel);
-            board.AddComponent<Image>().color = new Color(.045f, .065f, .09f, .97f);
-            PanelElement(board, 90);
+            board.AddComponent<Image>().color = new Color(.09f, .105f, .12f, 1f);
+            PanelElement(board, 140, true);
             taskBoardScroll = board.AddComponent<ScrollRect>();
             taskBoardScroll.horizontal = false;
             taskBoardScroll.vertical = true;
             taskBoardScroll.movementType = ScrollRect.MovementType.Clamped;
             taskBoardScroll.scrollSensitivity = 22;
             var viewport = UIObject("Viewport", board.transform);
             SetRect(viewport.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
                 new Vector2(4, 4), new Vector2(-4, -4));
             viewport.AddComponent<Image>().color = new Color(0, 0, 0, .08f);
             viewport.AddComponent<RectMask2D>();
             taskBoardScroll.viewport = viewport.GetComponent<RectTransform>();
             var content = UIObject("Content", viewport.transform);
@@ -48,72 +49,86 @@ namespace OpenEmpires
             layout.padding = new RectOffset(4, 4, 4, 4);
             layout.spacing = 6;
             layout.childControlWidth = layout.childControlHeight = true;
             layout.childForceExpandWidth = true;
             layout.childForceExpandHeight = false;
             content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
             taskBoardScroll.content = taskBoardContent;
             board.transform.SetSiblingIndex(3);
             var transcriptLayout = transcriptScroll.GetComponent<LayoutElement>();
             transcriptLayout.minHeight = 70;
             transcriptLayout.preferredHeight = 70;
             taskBoardFingerprint = null;
+            taskHistoryExpanded = false;
             RefreshTaskBoard();
         }
 
         private void RefreshTaskBoard()
         {
             if (taskBadgeText == null || taskBoardContent == null) return;
             var snapshot = CommanderTaskBoardProjection.Capture(semanticGoalManager,
                 strategicPipeline?.StrategicPlanner, runtimeGeneration);
             RefreshCurrentRequestStatus(snapshot);
             taskBadgeText.text = snapshot.ActiveCount + " active task" + (snapshot.ActiveCount == 1 ? "" : "s");
             taskBadgeText.gameObject.SetActive(snapshot.ActiveCount > 0);
-            string fingerprint = Fingerprint(snapshot);
+            string fingerprint = Fingerprint(snapshot) + "|history=" + taskHistoryExpanded;
             if (fingerprint == taskBoardFingerprint) return;
             taskBoardFingerprint = fingerprint;
             for (int i = taskBoardContent.childCount - 1; i >= 0; i--)
             {
                 var child = taskBoardContent.GetChild(i).gameObject;
                 if (Application.isPlaying) Destroy(child);
                 else DestroyImmediate(child);
             }
             if (snapshot.Cards.Count == 0)
             {
                 var empty = Text("NoTasks", taskBoardContent, "No tracked long-running tasks.", 12, TextAlignmentOptions.Left);
                 empty.richText = false; PanelElement(empty.gameObject, 24);
                 return;
             }
-            foreach (var card in snapshot.Cards) RenderTaskCard(card);
+            for (int i = 0; i < snapshot.ActiveCount; i++) RenderTaskCard(snapshot.Cards[i]);
+            if (snapshot.Cards.Count > snapshot.ActiveCount)
+            {
+                var history = CompactButton(taskBoardContent, "HistoryToggle",
+                    (taskHistoryExpanded ? "▾ Hide" : "▸ Show") + " completed history ("
+                        + (snapshot.Cards.Count - snapshot.ActiveCount) + ")", () =>
+                    { taskHistoryExpanded = !taskHistoryExpanded; taskBoardFingerprint = null; RefreshTaskBoard(); });
+                PanelElement(history.gameObject, 32);
+                if (taskHistoryExpanded)
+                    for (int i = snapshot.ActiveCount; i < snapshot.Cards.Count; i++) RenderTaskCard(snapshot.Cards[i]);
+            }
         }
 
         private void RefreshCurrentRequestStatus(CommanderTaskBoardSnapshot snapshot)
         {
             if (commanderStatusText == null) return;
             if (pendingActionPlan != null || strategicBridge?.PendingIntent != null)
             {
                 commanderStatusText.text = "Awaiting approval — no gameplay started.";
                 commanderStatusText.color = new Color(.7f, .83f, 1f);
                 return;
             }
             if (currentRequestGoalId <= 0) return;
             foreach (var card in snapshot.Cards)
             {
                 bool belongsToCurrentRequest = false;
                 foreach (var step in card.Steps)
                     if (step.GoalId == currentRequestGoalId) { belongsToCurrentRequest = true; break; }
                 if (!belongsToCurrentRequest) continue;
                 string detail = card.Status == CommanderTaskStatus.Blocked || card.Status == CommanderTaskStatus.Failed
                     ? card.Blocker : card.Status == CommanderTaskStatus.Completed
                         ? card.Progress : card.CurrentStep;
+                latestRequestState = card.Status == CommanderTaskStatus.Blocked
+                    || card.Status == CommanderTaskStatus.Waiting ? "Waiting/Blocked" : card.Status.ToString();
+                latestRequestDetail = detail ?? string.Empty;
                 string message = card.Status + (string.IsNullOrWhiteSpace(detail) ? string.Empty : " — " + detail);
                 commanderStatusText.text = message.Length > 120 ? message.Substring(0, 117) + "..." : message;
                 commanderStatusText.color = card.Status == CommanderTaskStatus.Blocked
                     || card.Status == CommanderTaskStatus.Failed
                     ? new Color(1f, .6f, .5f) : new Color(.7f, .83f, 1f);
                 return;
             }
         }
 
         private static string Fingerprint(CommanderTaskBoardSnapshot snapshot)
         {
             var value = new StringBuilder();
@@ -124,53 +139,66 @@ namespace OpenEmpires
                     .Append(card.Status).Append('|').Append(card.CanCancel).Append('|').Append(card.Objective).Append('|').Append(card.Progress)
                     .Append('|').Append(card.CurrentStep).Append('|').Append(card.Blocker);
                 foreach (var step in card.Steps)
                     value.Append('|').Append(step.GoalId).Append(':').Append(step.Status).Append(':')
                         .Append(step.Label).Append(':').Append(step.Progress).Append(':').Append(step.Blocker);
             }
             return value.ToString();
         }
 
         private void RenderTaskCard(CommanderTaskCardSnapshot card)
         {
             var root = UIObject("TaskCard", taskBoardContent);
-            root.AddComponent<Image>().color = new Color(.075f, .105f, .15f, .98f);
+            root.AddComponent<Image>().color = new Color(.13f, .15f, .17f, 1f);
             var layout = root.AddComponent<VerticalLayoutGroup>();
-            layout.padding = new RectOffset(6, 6, 6, 6);
-            layout.spacing = 3;
+            layout.padding = new RectOffset(8, 8, 8, 8);
+            layout.spacing = 8;
             layout.childControlWidth = layout.childControlHeight = true;
             layout.childForceExpandWidth = true;
             layout.childForceExpandHeight = false;
             string attention = card.Status == CommanderTaskStatus.Blocked || card.Status == CommanderTaskStatus.Failed
                 ? card.Blocker : string.Empty;
             var status = Text("Status", root.transform, card.Status + " — "
-                + (string.IsNullOrWhiteSpace(attention) ? card.Progress : attention), 12, TextAlignmentOptions.Left);
-            status.richText = false; PanelElement(status.gameObject, 32);
-            var title = Text("Objective", root.transform, card.Objective, 13, TextAlignmentOptions.Left);
-            title.richText = false; PanelElement(title.gameObject, 34);
-            var stepText = Text("CurrentStep", root.transform,
-                "Current: " + card.CurrentStep + (string.IsNullOrEmpty(card.Blocker) ? "" : " — " + card.Blocker),
-                11, TextAlignmentOptions.Left);
-            stepText.richText = false; PanelElement(stepText.gameObject, 32);
-            foreach (var step in card.Steps)
-            {
-                var detail = Text("Step", root.transform, "• " + step.Label + " — " + step.Status
-                    + (string.IsNullOrEmpty(step.Progress) ? "" : " — " + step.Progress), 11, TextAlignmentOptions.Left);
-                detail.richText = false; PanelElement(detail.gameObject, 34);
-            }
+                + (string.IsNullOrWhiteSpace(attention) ? card.Progress : attention), 14, TextAlignmentOptions.Left);
+            status.richText = false; PanelElement(status.gameObject, 40);
             if (card.CanCancel)
             {
                 var cancel = CompactButton(root.transform, "CancelTask", "Cancel task", () =>
                 {
                     if (CommanderTaskBoardProjection.TryCancel(semanticGoalManager,
                         strategicPipeline?.StrategicPlanner, card, runtimeGeneration))
                     {
                         taskBoardFingerprint = null;
                         RefreshTaskBoard();
                     }
                 });
-                PanelElement(cancel.gameObject, 27);
+                PanelElement(cancel.gameObject, 28);
             }
+            var title = Text("Objective", root.transform, card.Objective, 15, TextAlignmentOptions.Left);
+            title.richText = false; PanelElement(title.gameObject, 44);
+            var stepText = Text("CurrentStep", root.transform,
+                "Current: " + card.CurrentStep + (string.IsNullOrEmpty(card.Blocker) ? "" : " — " + card.Blocker),
+                14, TextAlignmentOptions.Left);
+            stepText.richText = false; PanelElement(stepText.gameObject, 40);
+            var details = UIObject("Details", root.transform);
+            var detailsLayout = details.AddComponent<VerticalLayoutGroup>();
+            detailsLayout.spacing = 4;
+            detailsLayout.childControlWidth = detailsLayout.childControlHeight = true;
+            detailsLayout.childForceExpandWidth = true; detailsLayout.childForceExpandHeight = false;
+            foreach (var step in card.Steps)
+            {
+                var detail = Text("Step", details.transform, "• " + step.Label + " — " + step.Status
+                    + (string.IsNullOrEmpty(step.Progress) ? "" : " — " + step.Progress), 13, TextAlignmentOptions.Left);
+                detail.richText = false; PanelElement(detail.gameObject, 42);
+            }
+            details.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
+            var toggle = CompactButton(root.transform,"DetailsToggle","Show details",()=>
+            {
+                details.SetActive(!details.activeSelf);
+                root.GetComponent<RectTransform>().ForceUpdateRectTransforms();
+            });
+            PanelElement(toggle.gameObject, 28);
+            details.SetActive(false);
             root.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
         }
     }
 }

## Assets/Scripts/AI/Commander/Phase5B/CommanderTaskBoardProjection.cs
CurrentSHA256:70B8FE65696D3DB2624AC36E0732E0BF93A4F54F28C17C71F262579992E6D69F
BaselineSHA256:98F54950AFC966A1A88731B0262F064106EED56AB41994571BBD1E7C7C81EF83
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-5-baseline\\Assets__Scripts__AI__Commander__Phase5B__CommanderTaskBoardProjection.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5B\\CommanderTaskBoardProjection.cs"
index 39dbc24..23f3fee 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-5-baseline\\Assets__Scripts__AI__Commander__Phase5B__CommanderTaskBoardProjection.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase5B\\CommanderTaskBoardProjection.cs"	
@@ -185,24 +185,25 @@ namespace OpenEmpires
             CommanderGoalStatus.Failed => CommanderTaskStatus.Failed,
             CommanderGoalStatus.Cancelled => CommanderTaskStatus.Cancelled,
             CommanderGoalStatus.Blocked => CommanderTaskStatus.Blocked,
             CommanderGoalStatus.Pending => CommanderTaskStatus.Accepted,
             CommanderGoalStatus.Planning => CommanderTaskStatus.Working,
             CommanderGoalStatus.Executing => CommanderTaskStatus.Working,
             CommanderGoalStatus.WaitingForConstruction => CommanderTaskStatus.Working,
             _ => CommanderTaskStatus.Waiting
         };
 
         private static string GoalLabel(GameSimulation simulation, CommanderGoal goal)
         {
+            if (goal is CommanderCapabilityGoal capability) return CapabilityLabel(capability);
             if (goal is ReachAgeGoal age) return "Reach Age " + age.TargetAge;
             if (goal is AllocateWorkersGoal workers)
             {
                 if (workers.Allocation.ResourceAmount.HasValue)
                     return (workers.Allocation.ResourceAmountMode == CommanderResourceAmountMode.AdditionalGathered
                         ? "Gather " : "Have ") + workers.Allocation.ResourceAmount.Value + " "
                         + workers.Allocation.Destination.Resource;
                 return "Assign workers to " + workers.Allocation.Destination.Resource;
             }
             if (goal is BuildStructureGoal build) return "Build " + build.Count + " "
                 + CommanderIntentCatalog.GetStructureDisplayName(build.StructureType);
             if (goal is EnsureUnitCountGoal units) return "Produce "
@@ -210,24 +211,25 @@ namespace OpenEmpires
                 + (units.IsExplicitNewProduction ? " new " : " total ")
                 + CommanderIntentCatalog.GetUnitDisplayName(simulation.ResolveCivUnitType(goal.PlayerId, units.RequestedUnitType));
             if (goal is WatchFutureUnitsGoal future) return "Assign next " + future.Order.Count + " "
                 + CommanderIntentCatalog.GetUnitDisplayName(simulation.ResolveCivUnitType(goal.PlayerId, future.Order.UnitType), future.Order.Count != 1)
                 + " from " + CommanderIntentCatalog.GetStructureDisplayName(future.Order.ProducerType);
             if (goal is ResourceAllocationGoal allocation) return "Assign " + allocation.TargetWorkers
                 + " workers to " + allocation.Resource;
             return goal.GoalType.ToString();
         }
 
         private static string GoalProgress(GameSimulation simulation, CommanderGoal goal)
         {
+            if (goal is CommanderCapabilityGoal capability) return CapabilityProgress(simulation, capability);
             if (goal is ReachAgeGoal age) return "Age " + simulation.GetPlayerAge(goal.PlayerId) + " / " + age.TargetAge;
             if (goal is AllocateWorkersGoal workers)
             {
                 if (workers.Allocation.ResourceAmount.HasValue)
                 {
                     long amount = workers.Allocation.ResourceAmountMode == CommanderResourceAmountMode.AdditionalGathered
                         ? Math.Max(0L, simulation.ResourceManager.GetGatheredIncome(goal.PlayerId,
                             workers.Allocation.Destination.Resource) - workers.GatheredIncomeAtActivation)
                         : Stockpile(simulation.ResourceManager.GetPlayerResources(goal.PlayerId),
                             workers.Allocation.Destination.Resource);
                     return (workers.Allocation.ResourceAmountMode == CommanderResourceAmountMode.AdditionalGathered
                         ? "Additional gathered " : "Stockpile ") + workers.Allocation.Destination.Resource
@@ -246,24 +248,102 @@ namespace OpenEmpires
             }
             if (goal is EnsureUnitCountGoal units) return units.IsExplicitNewProduction
                 ? "Attributed new units: " + units.AttributedUnitIds.Count + " / " + units.RequiredNewProductionCount
                 : "Owned units: " + goal.LastObservedOwnedCount + " / " + units.TargetTotal;
             if (goal is WatchFutureUnitsGoal future) return "Observed: " + future.ObservedCount
                 + " / " + future.Order.Count + "; assigned: " + future.AssignedCount
                 + "; interrupted: " + future.InterruptedCount;
             if (goal is ResourceAllocationGoal allocation) return "Workers: " + goal.LastObservedOwnedCount
                 + " / " + allocation.TargetWorkers;
             return goal.StatusReason ?? string.Empty;
         }
 
+        private static string CapabilityLabel(CommanderCapabilityGoal goal)
+        {
+            var action = goal.Action;
+            string subjects = action.UnitSelector.AllMatching ? "all matching villagers"
+                : action.UnitSelector.Count + " " + action.UnitSelector.Kind;
+            switch (action.ActionType)
+            {
+                case CommanderCapabilityActionType.MoveUnits:
+                    return "Move " + subjects + " to the validated destination";
+                case CommanderCapabilityActionType.GarrisonUnits:
+                    return "Garrison " + subjects + " in the exact owned "
+                        + (action.StructureType?.ToString() ?? "shelter");
+                case CommanderCapabilityActionType.UngarrisonBuilding:
+                    return "Ungarrison the exact owned " + (action.StructureType?.ToString() ?? "shelter");
+                case CommanderCapabilityActionType.DiscoverEnemy:
+                    return "Discover visible enemy " + (action.TargetSelector?.Kind == CommanderTargetSelectorKind.BuildingType
+                        ? action.TargetSelector?.StructureType?.ToString() : "unit");
+                default:
+                    return action.ActionType.ToString();
+            }
+        }
+
+        private static string CapabilityProgress(GameSimulation simulation, CommanderCapabilityGoal goal)
+        {
+            var action = goal.Action.ActionType;
+            if (action == CommanderCapabilityActionType.MoveUnits)
+            {
+                int count = goal.IssuedCommand is MoveCommand move && move.UnitIds != null
+                    ? move.UnitIds.Length : goal.FrozenWorkerIds?.Count ?? 0;
+                if (count == 0) return "Awaiting exact bound subjects; no placeholder count.";
+                if (!(goal.IssuedCommand is MoveCommand issued))
+                    return "Frozen subjects: " + count + "; native order pending.";
+                int receipts = 0, arrived = 0;
+                for (int i = 0; i < count; i++)
+                {
+                    if (goal.NativeMovementProcessed == null || i >= goal.NativeMovementProcessed.Length
+                        || !goal.NativeMovementProcessed[i]) continue;
+                    receipts++;
+                    if (goal.NativeMovementResults == null || i >= goal.NativeMovementResults.Length
+                        || goal.NativeMovementResults[i] == NativeMovePathResult.Partial
+                        || goal.NativeMovementResults[i] == NativeMovePathResult.NoPath
+                        || goal.NativeMovementEndpoints == null || i >= goal.NativeMovementEndpoints.Length) continue;
+                    var unit = simulation.UnitRegistry.GetUnit(issued.UnitIds[i]);
+                    if (unit == null || unit.State != UnitState.Idle || unit.HasPath) continue;
+                    long dx = (long)unit.SimPosition.x.Raw - goal.NativeMovementEndpoints[i].x.Raw;
+                    long dz = (long)unit.SimPosition.z.Raw - goal.NativeMovementEndpoints[i].z.Raw;
+                    long limit = Fixed32.FromInt(1).Raw;
+                    if (dx * dx + dz * dz <= limit * limit) arrived++;
+                }
+                return "Native arrivals: " + arrived + " / " + count + "; route receipts: " + receipts + " / " + count;
+            }
+            if (action == CommanderCapabilityActionType.GarrisonUnits)
+            {
+                if (goal.GarrisonCandidateIds == null) return "Awaiting exact eligible shelter snapshot.";
+                int housed = 0;
+                if (goal.GarrisonShelter != null)
+                    foreach (int id in goal.GarrisonIssuedIds ?? Array.Empty<int>())
+                        if (goal.GarrisonShelter.GarrisonedUnitIds.Contains(id)
+                            && simulation.UnitRegistry.GetGarrisonedUnit(id) != null) housed++;
+                return "Native shelter entries: " + housed + " / " + goal.GarrisonCandidateIds.Length
+                    + "; protected: " + goal.GarrisonProtectedCount
+                    + "; unaccommodated: " + goal.GarrisonUnaccommodatedCount;
+            }
+            if (action == CommanderCapabilityActionType.UngarrisonBuilding)
+            {
+                if (goal.UngarrisonInitialIds == null) return "Awaiting exact shelter occupant snapshot.";
+                int released = 0;
+                foreach (int id in goal.UngarrisonInitialIds)
+                    if (simulation.UnitRegistry.GetUnit(id) != null
+                        && (goal.GarrisonShelter == null || !goal.GarrisonShelter.GarrisonedUnitIds.Contains(id))) released++;
+                return "Native released: " + released + " / " + goal.UngarrisonInitialIds.Length;
+            }
+            if (action == CommanderCapabilityActionType.DiscoverEnemy)
+                return "Known-frontier moves: " + goal.DiscoveryMovesIssued + " / 8; candidates checked: "
+                    + goal.DiscoveryCandidatesChecked + ". " + (goal.StatusReason ?? string.Empty);
+            return goal.StatusReason ?? string.Empty;
+        }
+
         private static long Stockpile(PlayerResources resources, ResourceType type) => type switch
         {
             ResourceType.Food => resources.Food, ResourceType.Wood => resources.Wood,
             ResourceType.Gold => resources.Gold, ResourceType.Stone => resources.Stone, _ => 0
         };
 
         private static CommanderTaskCardSnapshot StrategicCard(StrategicPlan plan, long runtimeToken, int generation)
         {
             var steps = new CommanderTaskStepSnapshot[plan.Milestones.Count];
             int completed = 0;
             for (int i = 0; i < steps.Length; i++)
             {

## Assets/Tests/PlayMode/CommanderGrandFixInputDispatchPlayModeTests.cs
CurrentSHA256:69D3E595703F6E38CB43CDF84C2F50D5A35C590C78DF710EA47F03B5304148FA
BaselineSHA256:8155AFA83FCAEB72272B9D5966940E04AF64C73B44628E457CC1115510A334FB
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-5-baseline\\CommanderGrandFixInputDispatchPlayModeTests.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\PlayMode\\CommanderGrandFixInputDispatchPlayModeTests.cs"
index 54bdc76..60ec914 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-5-baseline\\CommanderGrandFixInputDispatchPlayModeTests.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\PlayMode\\CommanderGrandFixInputDispatchPlayModeTests.cs"	
@@ -106,35 +106,42 @@ namespace OpenEmpires.Tests
                 var panelCorners=new Vector3[4];var hudCorners=new Vector3[4];
                 panel.GetWorldCorners(panelCorners);playerPanel.GetWorldCorners(hudCorners);
                 Assert.That(panelCorners[0].x,Is.GreaterThan(hudCorners[2].x+4),
                     "Expanded Commander must not obscure the actual player-list HUD.");
                 Assert.That(panelCorners[2].x,Is.LessThanOrEqualTo(Screen.width-8));
                 Assert.That(panelCorners[0].y,Is.GreaterThanOrEqualTo(8));
                 Assert.That(goals.Goals,Is.Empty);
             }
             finally {UnityEngine.Object.DestroyImmediate(hudObject);}
         }
 
         [UnityTest]
-        public IEnumerator CompactLauncher_PrecedesSettingsWithoutOverlappingControls()
+        public IEnumerator CompactLauncher_UsesSeparateSettingsWithoutOverlappingControls()
         {
             chat.enabled=true;yield return null;Canvas.ForceUpdateCanvases();
             var compact=chat.transform.Find("CommanderCanvas/CompactCommander");
-            var launcher=compact.Find("Launcher").GetComponent<RectTransform>();
-            var settings=compact.Find("TextAIOptions").GetComponent<RectTransform>();
-            var launcherCorners=new Vector3[4];var settingsCorners=new Vector3[4];
-            launcher.GetWorldCorners(launcherCorners);settings.GetWorldCorners(settingsCorners);
-            Assert.That(launcherCorners[0].y,Is.GreaterThan(settingsCorners[1].y),
-                "Primary Commander/Record/Voice controls belong above Text AI settings, with a clear gap.");
+            Assert.That(compact.Find("Launcher").gameObject.activeInHierarchy,Is.True);
+            Assert.That(compact.Find("TextAIOptions").gameObject.activeInHierarchy,Is.False);
+            Assert.That(compact.Find("VoiceOptions").gameObject.activeInHierarchy,Is.False);
             Assert.That(goals.Goals,Is.Empty);Assert.That(chat.IsExpanded,Is.False);
+            chat.OpenCommander();yield return null;Canvas.ForceUpdateCanvases();
+            var panel=chat.transform.Find("CommanderCanvas/Panel");
+            var settings=panel.Find("Settings").GetComponent<RectTransform>();
+            var minimize=panel.Find("Minimize").GetComponent<RectTransform>();
+            var settingsCorners=new Vector3[4];var minimizeCorners=new Vector3[4];
+            settings.GetWorldCorners(settingsCorners);minimize.GetWorldCorners(minimizeCorners);
+            Assert.That(settings.gameObject.activeInHierarchy,Is.True);
+            Assert.That(settingsCorners[2].x,Is.LessThanOrEqualTo(minimizeCorners[0].x-2),
+                "Settings and Minimize must be distinct hit targets in the expanded dock.");
+            Assert.That(goals.Goals,Is.Empty);
         }
 
         [UnityTest]
         public IEnumerator SetupCards_StayOnScreenAndExposeScrollableControlsWithoutGameplay()
         {
             chat.enabled=true;chat.ShowVoiceSetup();yield return null;Canvas.ForceUpdateCanvases();
             var voice=(GameObject)typeof(CommanderChatUI).GetField("voiceSetupCard",Private).GetValue(chat);
             var corners=new Vector3[4];voice.GetComponent<RectTransform>().GetWorldCorners(corners);
             Assert.That(corners[0].y,Is.GreaterThanOrEqualTo(8),"Voice/model setup must not hide its bottom below the screen.");
             Assert.That(voice.transform.Find("SetupViewport/SetupContent/LocalModels/ModelActions"),Is.Not.Null,
                 "Actual model actions must belong to the scrollable content, not be omitted to make the panel fit.");
             chat.ShowSemanticSetup();yield return null;Canvas.ForceUpdateCanvases();

## Assets/Tests/PlayMode/CommanderPhase5CUIPlayModeTests.cs
CurrentSHA256:C04C9637944A4EC949EC8C414D9C8B0D794B35EEECF12F02E0256EC9864F3FD7
NEW SOURCE
using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace OpenEmpires.Tests
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
        private GameObject renderCameraObject;
        private int previousWidth, previousHeight;

        [SetUp]
        public void SetUp()
        {
            previousWidth = Screen.width; previousHeight = Screen.height;
            foreach (var existing in UnityEngine.Object.FindObjectsByType<CommanderChatUI>())
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            goals = new CommanderGoalManager(simulation, 0);
            dispatcher = new CommanderIntentDispatcher(simulation, goals);
            provider = new SlowProvider();
            chat = new GameObject("Phase5CCommanderUI").AddComponent<CommanderChatUI>();
            chat.enabled = false;
            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromMilliseconds(60));
        }

        [TearDown]
        public void TearDown()
        {
            if (previousWidth > 0 && previousHeight > 0) Screen.SetResolution(previousWidth, previousHeight, false);
            if (chat != null) UnityEngine.Object.DestroyImmediate(chat.gameObject);
            if (renderCameraObject != null) UnityEngine.Object.DestroyImmediate(renderCameraObject);
            dispatcher?.Dispose();
            goals?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        [UnityTest]
        public IEnumerator MinimizedStartup_OpensTabbedDockWithFixedMultilineComposer()
        {
            chat.enabled = true;
            yield return null;
            Assert.That(chat.IsExpanded, Is.False);
            Assert.That(chat.transform.Find("CommanderCanvas/CompactCommander/Launcher"), Is.Not.Null);
            chat.OpenCommander();
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/Views/ChatTab"), Is.Not.Null);
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/Views/TasksTab"), Is.Not.Null);
            var input = chat.transform.Find("CommanderCanvas/Panel/TextSubmission/Input")?.GetComponent("TMP_InputField");
            Assert.That(input, Is.Not.Null);
            Assert.That(input.GetType().GetProperty("lineType")?.GetValue(input)?.ToString(), Is.EqualTo("MultiLineNewline"));
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/Transcript")?.GetComponent<ScrollRect>(), Is.Not.Null);
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/TaskBoard")?.GetComponent<ScrollRect>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Timeout_RetainsDraftAndOffersExplicitRetryEditCancel()
        {
            chat.enabled = true;
            chat.OpenCommander();
            chat.InputText = "gather 200 gold with 3 villagers";
            Task<CommanderAIChatSubmission> request = chat.SubmitCurrentInputAsync();
            while (!request.IsCompleted) yield return null;
            Assert.That(provider.Attempts, Is.EqualTo(1));
            Assert.That(chat.InputText, Is.EqualTo("gather 200 gold with 3 villagers"));
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/RequestRecovery/Retry")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/RequestRecovery/Edit")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/RequestRecovery/Cancel")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")), Does.StartWith("Failed"));
            Canvas.ForceUpdateCanvases();
            var recoveryCorners = new Vector3[4];
            var composerCorners = new Vector3[4];
            chat.transform.Find("CommanderCanvas/Panel/RequestRecovery").GetComponent<RectTransform>().GetWorldCorners(recoveryCorners);
            chat.transform.Find("CommanderCanvas/Panel/TextSubmission").GetComponent<RectTransform>().GetWorldCorners(composerCorners);
            Assert.That(recoveryCorners[0].y, Is.GreaterThanOrEqualTo(composerCorners[2].y));
            yield return new WaitForEndOfFrame();
            SaveCurrentFrame("timeout-recovery");
            Assert.That(goals.Goals, Is.Empty);
        }

        [UnityTest]
        public IEnumerator DockInputOwnership_TracksActualExpandedAndCompactRegions()
        {
            chat.enabled = true;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var compact = chat.transform.Find("CommanderCanvas/CompactCommander").GetComponent<RectTransform>();
            Assert.That(chat.ContainsScreenPoint(RectTransformUtility.WorldToScreenPoint(null, compact.TransformPoint(compact.rect.center))), Is.True);
            Assert.That(chat.ContainsScreenPoint(new Vector2(10,10)), Is.False);
            chat.OpenCommander();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var panel = chat.transform.Find("CommanderCanvas/Panel").GetComponent<RectTransform>();
            var panelCenter = RectTransformUtility.WorldToScreenPoint(null, panel.TransformPoint(panel.rect.center));
            Assert.That(chat.ContainsScreenPoint(panelCenter), Is.True);
            Assert.That(chat.ContainsScreenPoint(new Vector2(10,10)), Is.False);
            Assert.That(CommanderChatUI.TryHandleEscapeInput(), Is.True);
            Assert.That(chat.IsExpanded, Is.False);
            Assert.That(chat.ContainsScreenPoint(panelCenter), Is.False);
        }

        [Test]
        public void CapabilityCards_NameRealActionAndDoNotInventAllMatchingCount()
        {
            var selector = new CommanderUnitSelector(CommanderUnitSelectorKind.Villagers, 1, allMatching: true);
            var moveSelector = new CommanderUnitSelector(CommanderUnitSelectorKind.Villagers, 3);
            var location = new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase);
            goals.SubmitCapabilityAction(new CapabilityActionIntent(0,
                CommanderCapabilityActionType.MoveUnits, moveSelector, location));
            goals.SubmitCapabilityAction(new CapabilityActionIntent(0,
                CommanderCapabilityActionType.GarrisonUnits, selector, location,
                structureType: BuildingType.TownCenter));
            goals.SubmitCapabilityAction(new CapabilityActionIntent(0,
                CommanderCapabilityActionType.UngarrisonBuilding,
                new CommanderUnitSelector(CommanderUnitSelectorKind.Villagers), location,
                structureType: BuildingType.TownCenter));
            goals.SubmitCapabilityAction(new CapabilityActionIntent(0,
                CommanderCapabilityActionType.DiscoverEnemy,
                new CommanderUnitSelector(CommanderUnitSelectorKind.Scout),
                new CommanderLocationSelector(CommanderLocationSelectorKind.VisibleEnemy),
                targetSelector: new CommanderTargetSelector(BuildingType.TownCenter)));
            var cards = CommanderTaskBoardProjection.Capture(goals, null, 1).Cards;
            Assert.That(cards, Has.Count.EqualTo(4));
            Assert.That(cards[0].CurrentStep, Does.Contain("Discover"));
            Assert.That(cards[1].CurrentStep, Does.Contain("Ungarrison"));
            Assert.That(cards[2].CurrentStep, Does.Contain("Garrison"));
            Assert.That(cards[3].CurrentStep, Does.Contain("Move"));
            Assert.That(cards[1].Progress, Does.Contain("Awaiting exact shelter occupant snapshot"));
            Assert.That(cards[2].Progress, Does.Contain("Awaiting exact eligible shelter snapshot"));
            Assert.That(cards[3].Progress, Does.Contain("Awaiting exact bound subjects"));
        }

        [UnityTest]
        public IEnumerator TasksView_KeepsActiveCancelVisibleAndHistoryCollapsed()
        {
            chat.enabled = true;
            var completed = goals.SubmitBuildStructure(BuildingType.House);
            completed.SetStatus(CommanderGoalStatus.Completed, "One requested House completed.");
            var goal = goals.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
            chat.OpenCommander();
            yield return null;
            chat.transform.Find("CommanderCanvas/Panel/Views/TasksTab").GetComponent<Button>().onClick.Invoke();
            yield return null;
            var board = chat.transform.Find("CommanderCanvas/Panel/TaskBoard");
            Assert.That(board.gameObject.activeInHierarchy, Is.True);
            Assert.That(board.Find("Viewport/Content/TaskCard/CancelTask")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(board.Find("Viewport/Content/TaskCard/Details")?.gameObject.activeInHierarchy, Is.False);
            Assert.That(board.Find("Viewport/Content/HistoryToggle"), Is.Not.Null);
            yield return null;
            board.Find("Viewport/Content/TaskCard/CancelTask").GetComponent<Button>().onClick.Invoke();
            Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Cancelled));
        }

        [UnityTest]
        public IEnumerator TasksView_ActiveCancelIsInsideViewportWithoutScrolling()
        {
            chat.enabled = true;
            goals.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
            chat.OpenCommander();
            yield return null;
            chat.transform.Find("CommanderCanvas/Panel/Views/TasksTab").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var board = chat.transform.Find("CommanderCanvas/Panel/TaskBoard");
            var viewport = board.Find("Viewport").GetComponent<RectTransform>();
            var cancel = board.Find("Viewport/Content/TaskCard/CancelTask").GetComponent<RectTransform>();
            var viewCorners = new Vector3[4];
            var cancelCorners = new Vector3[4];
            viewport.GetWorldCorners(viewCorners);
            cancel.GetWorldCorners(cancelCorners);
            Assert.That(cancelCorners[0].y, Is.GreaterThanOrEqualTo(viewCorners[0].y + 2),
                "The active task's Cancel control must be visible without scrolling.");
            Assert.That(cancelCorners[2].y, Is.LessThanOrEqualTo(viewCorners[2].y - 2));
        }

        [UnityTest]
        public IEnumerator ExpandedDock_HidesItsLauncherAndHasOneActiveCommanderCanvas()
        {
            chat.enabled = true;
            chat.OpenCommander();
            yield return null;
            Assert.That(chat.transform.Find("CommanderCanvas/CompactCommander").gameObject.activeInHierarchy, Is.False);
            int activeCanvases = 0;
            foreach (var ui in UnityEngine.Object.FindObjectsByType<CommanderChatUI>())
                if (ui.gameObject.activeInHierarchy && ui.transform.Find("CommanderCanvas")?.gameObject.activeInHierarchy == true)
                    activeCanvases++;
            Assert.That(activeCanvases, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator IdleDock_ShowsReadableSettingsWithoutLegacyStatusAndTallVoiceStack()
        {
            chat.enabled = true;
            chat.OpenCommander();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var panel = chat.transform.Find("CommanderCanvas/Panel");
            Assert.That(TextOf(panel.Find("Settings/Label")), Is.EqualTo("Settings"));
            Assert.That(panel.Find("CommanderStatus").gameObject.activeInHierarchy, Is.False);
            Assert.That(panel.Find("Current plan status").gameObject.activeInHierarchy, Is.False);
            Assert.That(panel.Find("VoiceBar").GetComponent<LayoutElement>().preferredHeight,
                Is.LessThanOrEqualTo(42));
        }

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
            Assert.That(input.rect.width, Is.GreaterThanOrEqualTo(send.rect.width * 2));
        }

        [UnityTest]
        public IEnumerator CompoundApproval_ShowsFullValidatedPreviewAndExplicitDecisionsWithoutIssuing()
        {
            InstallRenderFixtureCamera();
            // The approval fixture performs real graph admission on the main thread;
            // its timeout is test-local, not a production timeout change.
            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
            chat.enabled = true;
            chat.OpenCommander();
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Barracks\",\"count\":1},"
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3,\"producerFromNode\":0,\"dependsOn\":[0]}]}");
            var turn = chat.SubmitMessageAsync("build a Barracks, then train three Spearmen there");
            provider.CompleteLatest(parsed);
            while (!turn.IsCompleted) yield return null;
            yield return null;
            Assert.That(chat.PendingActionPlan, Is.Not.Null,
                $"attempts={provider.Attempts}; transcript={chat.DisplayedTranscript}");
            var approval = chat.transform.Find("CommanderCanvas/Panel/Approval");
            Assert.That(approval.gameObject.activeInHierarchy, Is.True);
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/Transcript").gameObject.activeInHierarchy, Is.False,
                "Approval preview replaces Chat scroll while pending.");
            var scroll = approval.Find("PreviewScroll").GetComponent<ScrollRect>();
            Assert.That(scroll, Is.Not.Null);
            Assert.That(TextOf(scroll.content), Is.EqualTo(chat.PendingActionPlan.Preview));
            Assert.That(TextOf(scroll.content), Does.Contain("after step 1"));
            Assert.That(approval.Find("ApprovePlan")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(approval.Find("RejectPlan")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(goals.Goals, Is.Empty, "Displaying or scrolling details cannot authorize gameplay.");
            Canvas.ForceUpdateCanvases();
            var panelCorners = new Vector3[4];
            var approveCorners = new Vector3[4];
            var rejectCorners = new Vector3[4];
            var composerCorners = new Vector3[4];
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/TextSubmission").gameObject.activeInHierarchy, Is.True);
            Assert.That(chat.transform.Find("CommanderCanvas/Panel/VoiceBar").gameObject.activeInHierarchy, Is.True);
            chat.transform.Find("CommanderCanvas/Panel").GetComponent<RectTransform>().GetWorldCorners(panelCorners);
            approval.Find("ApprovePlan").GetComponent<RectTransform>().GetWorldCorners(approveCorners);
            approval.Find("RejectPlan").GetComponent<RectTransform>().GetWorldCorners(rejectCorners);
            chat.transform.Find("CommanderCanvas/Panel/TextSubmission").GetComponent<RectTransform>().GetWorldCorners(composerCorners);
            Assert.That(approveCorners[0].y, Is.GreaterThanOrEqualTo(panelCorners[0].y));
            Assert.That(rejectCorners[0].y, Is.GreaterThanOrEqualTo(panelCorners[0].y));
            Assert.That(composerCorners[0].y, Is.GreaterThanOrEqualTo(panelCorners[0].y),
                "Approval must not push the fixed composer below the dock.");
            Assert.That(composerCorners[2].y, Is.LessThanOrEqualTo(approveCorners[0].y - 4),
                "Composer must remain below the approval decision controls.");
            yield return null;
            ForceFixtureRepaint();
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            var approvalImage = ScreenCapture.CaptureScreenshotAsTexture();
            var directory = NewRenderDirectory("compound-approval");
            File.WriteAllBytes(Path.Combine(directory,
                $"editor-approval-actual-{approvalImage.width}x{approvalImage.height}.png"),
                approvalImage.EncodeToPNG());
            UnityEngine.Object.Destroy(approvalImage);
            yield return null;
            approval.Find("RejectPlan").GetComponent<Button>().onClick.Invoke();
            Assert.That(chat.PendingActionPlan, Is.Null);
            Assert.That(goals.Goals, Is.Empty);
            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
                Does.StartWith("Cancelled"));
        }

        [UnityTest]
        public IEnumerator ReviewedVoiceDraft_IsVisibleInExpandedDockAndDiscardDoesNotSubmit()
        {
            chat.SetVoiceProvider(new MockCommanderSpeechToTextProvider("train three spearmen"),
                new FixtureAudioCapture());
            chat.VoiceController.AutoSubmit = false;
            chat.enabled = true;
            chat.OpenCommander();
            chat.VoiceController.StartRecording();
            var voice = chat.VoiceController.StopRecordingAndTranscribeAsync();
            while (!voice.IsCompleted) yield return null;
            yield return null;
            Assert.That(chat.VoiceState, Is.EqualTo(CommanderVoiceState.Preview));
            var review = chat.transform.Find("CommanderCanvas/Panel/VoiceReview");
            Assert.That(review?.gameObject.activeInHierarchy, Is.True);
            Assert.That(TextOf(review.Find("ReviewText")), Does.Contain("not sent"));
            Assert.That(review.Find("SendReviewed")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(review.Find("EditReviewed")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(review.Find("DiscardReviewed")?.gameObject.activeInHierarchy, Is.True);
            Assert.That(goals.Goals, Is.Empty);
            review.Find("DiscardReviewed").GetComponent<Button>().onClick.Invoke();
            Assert.That(goals.Goals, Is.Empty);
        }

        [UnityTest]
        public IEnumerator ApprovePlan_UsesExistingSingleUseAuthorityAndUpdatesLatestStatus()
        {
            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
            chat.enabled = true;
            chat.OpenCommander();
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"House\",\"count\":1},"
                + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3}]}");
            var turn = chat.SubmitMessageAsync("build a House and train three Spearmen");
            provider.CompleteLatest(parsed);
            while (!turn.IsCompleted) yield return null;
            yield return null;
            Assert.That(chat.PendingActionPlan, Is.Not.Null);
            Assert.That(goals.Goals, Is.Empty);
            var approve = chat.transform.Find("CommanderCanvas/Panel/Approval/ApprovePlan").GetComponent<Button>();
            approve.onClick.Invoke();
            approve.onClick.Invoke();
            Assert.That(chat.PendingActionPlan, Is.Null);
            int admittedEffects = goals.Goals.Count;
            Assert.That(admittedEffects, Is.EqualTo(2), "The approved graph has two effects.");
            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
                Does.Not.StartWith("Awaiting approval"));
            yield return null;
            approve.onClick.Invoke();
            Assert.That(goals.Goals, Has.Count.EqualTo(admittedEffects));
            Assert.That(provider.Attempts, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MissingWorkerCount_ShowsProminentClarificationWithoutGameplay()
        {
            chat.Initialize(provider, simulation, goals, dispatcher, TimeSpan.FromSeconds(2));
            chat.enabled = true;
            chat.OpenCommander();
            var parsed = CommanderSemanticJson.Parse("{\"outcome\":\"Clarify\",\"message\":\"How many villagers?\","
                + "\"pending\":{\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\",\"countMode\":\"Exact\","
                + "\"workers\":{\"state\":\"Idle\"},\"destination\":{\"resource\":\"Wood\",\"sourceKind\":\"Tree\"}},"
                + "\"missingFields\":[\"Count\"]}");
            var turn = chat.SubmitMessageAsync("send idle villagers to gather wood");
            provider.CompleteLatest(parsed);
            while (!turn.IsCompleted) yield return null;
            yield return null;
            var question = chat.transform.Find("CommanderCanvas/Panel/PendingClarification");
            Assert.That(chat.PendingClarification, Is.Not.Null);
            Assert.That(question.gameObject.activeInHierarchy, Is.True);
            Assert.That(TextOf(question.Find("Question")), Does.Contain("How many villagers"));
            Assert.That(TextOf(chat.transform.Find("CommanderCanvas/Panel/LatestRequest")),
                Does.StartWith("Needs clarification"));
            Assert.That(goals.Goals, Is.Empty);
            yield return new WaitForEndOfFrame();
            SaveCurrentFrame("pending-clarification");
        }

        [UnityTest]
        public IEnumerator ChatView_RendersPlayerAndCommanderAsSeparateBubbles()
        {
            chat.enabled = true;
            chat.OpenCommander();
            var turn = chat.SubmitMessageAsync("strategy status");
            while (!turn.IsCompleted) yield return null;
            yield return null;
            var bubbles = chat.transform.Find("CommanderCanvas/Panel/Transcript/Viewport/BubbleContent");
            Assert.That(bubbles, Is.Not.Null);
            Assert.That(bubbles.childCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(bubbles.GetChild(bubbles.childCount - 2).name, Is.EqualTo("PlayerBubble"));
            Assert.That(bubbles.GetChild(bubbles.childCount - 1).name, Is.EqualTo("CommanderBubble"));
        }

        [UnityTest]
        public IEnumerator LatestQuestionStatus_DoesNotBorrowOlderWorkingGoal()
        {
            chat.enabled = true;
            goals.SubmitReachAge(CommanderSemanticAgeTarget.Castle);
            chat.OpenCommander();
            var turn = chat.SubmitMessageAsync("strategy status");
            while (!turn.IsCompleted) yield return null;
            yield return null;
            var status = chat.transform.Find("CommanderCanvas/Panel/LatestRequest");
            Assert.That(TextOf(status), Does.StartWith("Completed"));
            Assert.That(TextOf(status), Does.Not.Contain("Reach Age"));
        }

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
            var panel = chat.transform.Find("CommanderCanvas/Panel").GetComponent<RectTransform>();
            var viewport = chat.transform.Find("CommanderCanvas/Panel/Transcript/Viewport").GetComponent<RectTransform>();
            var composer = chat.transform.Find("CommanderCanvas/Panel/TextSubmission").GetComponent<RectTransform>();
            var panelCorners = new Vector3[4];
            var viewportCorners = new Vector3[4];
            var composerCorners = new Vector3[4];
            panel.GetWorldCorners(panelCorners);
            viewport.GetWorldCorners(viewportCorners);
            composer.GetWorldCorners(composerCorners);
            Assert.That(panelCorners[0].x, Is.GreaterThanOrEqualTo(0));
            Assert.That(panelCorners[2].x, Is.LessThanOrEqualTo(Screen.width + 1));
            Assert.That(panelCorners[0].y, Is.GreaterThanOrEqualTo(0));
            Assert.That(viewportCorners[0].y, Is.GreaterThanOrEqualTo(composerCorners[1].y + 4),
                "Chat scroll must end above the fixed composer.");
        }

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
            Directory.CreateDirectory(directory);
            int activeCommanderCanvases = 0;
            foreach (var ui in UnityEngine.Object.FindObjectsByType<CommanderChatUI>())
                if (ui.gameObject.activeInHierarchy && ui.transform.Find("CommanderCanvas")?.gameObject.activeInHierarchy == true)
                    activeCommanderCanvases++;
            Debug.Log($"[Commander Phase5C render] active Commander canvases={activeCommanderCanvases}; own compact active={chat.transform.Find("CommanderCanvas/CompactCommander").gameObject.activeInHierarchy}");
            Assert.That(activeCommanderCanvases, Is.EqualTo(1));
            Assert.That(chat.transform.Find("CommanderCanvas/CompactCommander").gameObject.activeInHierarchy, Is.False);
            foreach (int width in new[] { 1280, 1920 })
            {
                int height = width == 1280 ? 720 : 1080;
                Screen.SetResolution(width, height, false);
                yield return null;
                yield return null;
                ForceFixtureRepaint();
                Canvas.ForceUpdateCanvases();
                var launcher = GameObject.Find("CompactCommander");
                Debug.Log($"[Commander Phase5C render] active launcher object={(launcher == null ? "none" : launcher.transform.parent.name + "/" + launcher.name)}; own launcher={chat.transform.Find("CommanderCanvas/CompactCommander").gameObject.activeInHierarchy}");
                yield return new WaitForEndOfFrame();
                var chatImage = ScreenCapture.CaptureScreenshotAsTexture();
                Assert.That(chatImage, Is.Not.Null);
                File.WriteAllBytes(Path.Combine(directory,
                    $"editor-chat-requested-{width}x{height}-actual-{chatImage.width}x{chatImage.height}.png"),
                    chatImage.EncodeToPNG());
                Debug.Log($"[Commander Phase5C render] requested={width}x{height}; captured={chatImage.width}x{chatImage.height}; Chat active goal and local reply");
                UnityEngine.Object.Destroy(chatImage);
                chat.transform.Find("CommanderCanvas/Panel/Views/TasksTab").GetComponent<Button>().onClick.Invoke();
                yield return null;
                ForceFixtureRepaint();
                Canvas.ForceUpdateCanvases();
                yield return new WaitForEndOfFrame();
                var tasksImage = ScreenCapture.CaptureScreenshotAsTexture();
                Assert.That(tasksImage, Is.Not.Null);
                File.WriteAllBytes(Path.Combine(directory,
                    $"editor-tasks-requested-{width}x{height}-actual-{tasksImage.width}x{tasksImage.height}.png"),
                    tasksImage.EncodeToPNG());
                Debug.Log($"[Commander Phase5C render] requested={width}x{height}; captured={tasksImage.width}x{tasksImage.height}; Tasks active goal and visible Cancel");
                UnityEngine.Object.Destroy(tasksImage);
                chat.transform.Find("CommanderCanvas/Panel/Views/ChatTab").GetComponent<Button>().onClick.Invoke();
                yield return null;
            }
        }

        private void InstallRenderFixtureCamera()
        {
            renderCameraObject = new GameObject("Phase5CRenderFixtureCamera");
            var camera = renderCameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.depth = 100;
        }

        private void ForceFixtureRepaint()
        {
            foreach (var graphic in chat.GetComponentsInChildren<Graphic>(true))
                graphic.SetAllDirty();
        }

        private static string NewRenderDirectory(string state)
        {
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../Docs/CommanderPhase5C/evidence/task-5-render",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + state));
            Directory.CreateDirectory(directory);
            Debug.Log("[Commander Phase5C render] evidence directory=" + directory);
            return directory;
        }

        private static void SaveCurrentFrame(string state)
        {
            var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            var directory = NewRenderDirectory(state);
            File.WriteAllBytes(Path.Combine(directory,
                $"editor-{state}-actual-{screenshot.width}x{screenshot.height}.png"),
                screenshot.EncodeToPNG());
            UnityEngine.Object.Destroy(screenshot);
        }

        [UnityTest]
        public IEnumerator TimedOutLateResponse_CannotAdmitAndDoubleRetryStartsOnce()
        {
            chat.enabled = true;
            chat.OpenCommander();
            chat.InputText = "train 10 spearmen";
            var first = chat.SubmitCurrentInputAsync();
            while (!first.IsCompleted) yield return null;
            Assert.That(provider.Attempts, Is.EqualTo(1));
            Assert.That(chat.InputText, Is.EqualTo("train 10 spearmen"), "after initial timeout");
            provider.CompleteLatest("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10}]}");
            yield return null;
            Assert.That(goals.Goals, Is.Empty);
            var retry = chat.transform.Find("CommanderCanvas/Panel/RequestRecovery/Retry").GetComponent<Button>();
            retry.onClick.Invoke();
            retry.onClick.Invoke();
            yield return null;
            Assert.That(provider.Attempts, Is.EqualTo(2));
            Assert.That(chat.InputText, Is.EqualTo("train 10 spearmen"), "after retry begins");
            var cancel = chat.transform.Find("CommanderCanvas/Panel/RequestRecovery/Cancel").GetComponent<Button>();
            cancel.onClick.Invoke();
            Assert.That(chat.InputText, Is.EqualTo("train 10 spearmen"), "immediately after Cancel");
            provider.CompleteLatest("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":10}]}");
            yield return null;
            Assert.That(goals.Goals, Is.Empty);
            Assert.That(chat.InputText, Is.EqualTo("train 10 spearmen"));
        }

        private static string TextOf(Transform target)
        {
            if (target == null) return string.Empty;
            foreach (var component in target.GetComponents<Component>())
                if (component.GetType().Name == "TextMeshProUGUI")
                    return (string)component.GetType().GetProperty("text").GetValue(component);
            return string.Empty;
        }

        private sealed class SlowProvider : ICommanderAIProvider, ICommanderSemanticProvider
        {
            public int Attempts { get; private set; }
            private TaskCompletionSource<CommanderSemanticResult> latest;
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
        }

        private sealed class FixtureAudioCapture : ICommanderAudioCapture
        {
            public bool IsRecording { get; private set; }
            public string CurrentDevice => "FixtureMic";
            public bool StartRecording(string deviceName = null, float maxDurationSeconds = 15f)
            { IsRecording = true; return true; }
            public CommanderAudioData StopRecording()
            { IsRecording = false; return new CommanderAudioData(new float[16000], 16000, 1); }
            public void CancelRecording() => IsRecording = false;
            public void Dispose() => IsRecording = false;
        }
    }
}

fileFormatVersion: 2
guid: 976a282d0609a7148b2c6ba73dbcb07b

