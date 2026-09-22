# Phase 4C.2 Task 2 review package

Generated from the frozen Task 1 source on 2026-09-21. This package is mechanical evidence only; production and focused test source was not modified.

## Exact CommanderChatUI diff against the Task 1 before snapshot

```diff
diff --git a/Docs/CommanderPhase4C/phase4c2-task1-before/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs b/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs
index 88fdf9b..ce94d6d 100644
--- a/Docs/CommanderPhase4C/phase4c2-task1-before/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs
+++ b/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs
@@ -12,7 +12,7 @@ namespace OpenEmpires
 {
     // Local-only Commander surface. It neither reads nor writes multiplayer chat and
     // never sends network messages. Accepted intents enter the existing dispatcher.
-    public sealed class CommanderChatUI : MonoBehaviour
+    public sealed partial class CommanderChatUI : MonoBehaviour
     {
         private const int TranscriptCapacity = 64;
         private const int TranscriptMessageMaximum = 32768;
@@ -103,6 +103,7 @@ namespace OpenEmpires
             strategicBridge?.Dispose();
             strategicBridge = null;
             DetachStrategicPipeline();
+            ResetExplanationState();
             adapter?.ResetHistory();
             Conversation?.Reset();
             transcriptEntries.Clear();
@@ -140,6 +141,7 @@ namespace OpenEmpires
             lifetime = new CancellationTokenSource();
             strategicBridge?.Dispose();
             DetachStrategicPipeline();
+            ResetExplanationState();
             strategicPipeline = pipeline;
             strategicBridge = new StrategicAIApprovalBridge(interpreter, pipeline.StrategicPlanner.IntentIds,
                 () => pipeline.CaptureContext(), providerTimeout,
@@ -159,6 +161,9 @@ namespace OpenEmpires
 
         public async Task<CommanderAIChatSubmission> SubmitMessageAsync(string message)
         {
+            string trimmed = (message ?? string.Empty).Trim();
+            if (trimmed.Length == 0) return null;
+            if (TryHandleExplanationQuery(trimmed)) return null;
             if (adapter == null)
             {
                 LatestSubmission = null;
@@ -166,8 +171,6 @@ namespace OpenEmpires
                 return null;
             }
 
-            string trimmed = (message ?? string.Empty).Trim();
-            if (trimmed.Length == 0) return null;
             string wholeForm = NormalizeWholeForm(trimmed);
             if (wholeForm == "clear memory")
             {
@@ -193,7 +196,6 @@ namespace OpenEmpires
             IReadOnlyList<MemoryEntry> memoryAtTurnStart = Conversation.Snapshot();
             strategicBridge?.ClearPending();
             LatestStrategicInterpretation = null;
-            LatestStrategicDecision = null;
             LatestSubmission = null;
             UpdateStrategicControls();
             AppendLine("Player", trimmed);
@@ -255,8 +257,19 @@ namespace OpenEmpires
             int id = strategicBridge.PendingIntent.IntentId;
             StrategicIntent intent = confirmed ? strategicBridge.Confirm(id) : strategicBridge.TakeRecommendation(id);
             if (intent == null) return null;
-            var approval = new StrategicApprovalLayer().Evaluate(strategicPipeline.CaptureContext(), intent, intent.Source);
-            LatestStrategicDecision = strategicPipeline.EvaluateApprovedIntentNow(approval);
+            pendingExplanationIntentId = intent.IntentId;
+            pendingExplanationObjective = intent.ObjectiveType.ToString();
+            try
+            {
+                var approval = new StrategicApprovalLayer().Evaluate(
+                    strategicPipeline.CaptureContext(), intent, intent.Source);
+                LatestStrategicDecision = strategicPipeline.EvaluateApprovedIntentNow(approval);
+            }
+            finally
+            {
+                pendingExplanationIntentId = null;
+                pendingExplanationObjective = null;
+            }
             AppendLine("Commander", LatestStrategicDecision.Outcome);
             UpdateStrategicControls();
             return LatestStrategicDecision;
@@ -283,6 +296,7 @@ namespace OpenEmpires
             LatestStrategicInterpretation = null;
             LatestStrategicDecision = null;
             LatestSubmission = null;
+            ResetExplanationState();
             submitting = false;
             if (inputField != null)
             {
@@ -338,6 +352,7 @@ namespace OpenEmpires
         private void OnStrategicEvaluationCompleted(StrategicDecisionRecord record)
         {
             if (record == null || Conversation == null) return;
+            ProjectStrategicExplanation(record);
             StrategicIntentSubmission submission = record.Submission;
             int? intentId = submission?.Intent?.IntentId;
             int? planId = submission?.CreatedPlan == true
@@ -377,6 +392,7 @@ namespace OpenEmpires
             lifetime?.Cancel();
             strategicBridge?.Dispose();
             DetachStrategicPipeline();
+            ResetExplanationState();
             adapter?.ResetHistory();
             Conversation?.Reset();
             transcriptEntries.Clear();
```

## Frozen file/hash table

| Path | Bytes | SHA-256 |
|---|---:|---|
| Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs | 27101 | 7F4C1D642DD2B373D3E7A49ED3F10E779EF36F415466BB4932147A05775DDBCA |
| Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Explanations.cs | 6061 | 68458D82489BB8669EF4D95329C4DA869AC55DFA1BFC00C4A87C7D8D54EC5973 |
| Assets/Scripts/AI/Commander/Phase4C/ExplanationContext.cs | 4419 | A902F96FB5B18BD3DCC00BDB924A7BD67C180412D54CE6852E754AE3B10305EE |
| Assets/Scripts/AI/Commander/Phase4C/ExplanationResult.cs | 577 | 2C1E02410A1D98D855C623AE9C819E0F84B83861A11CD44B04DE16FFC95D1D6D |
| Assets/Scripts/AI/Commander/Phase4C/CommanderExplanationService.cs | 6745 | 2A62448136C4B876942D97BB3DFFA28D8B35551AA6A9D15FA252DECD349C94C9 |
| Assets/Tests/EditMode/CommanderPhase4C2Tests.cs | 8917 | 5B57FA96494CAF3CB1ADF36913BA28BF832F80D3B0E8F1D0FF3FA6847B3C648B |
| Assets/Tests/PlayMode/CommanderPhase4C2PlayModeTests.cs | 17355 | 3A97D815B7F7A471C44ED7D6F9A2BFDCC161E7C049A60A7911BD543ACBA2F5DD |

## Full frozen source contents

### Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs

```csharp
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenEmpires
{
    // Local-only Commander surface. It neither reads nor writes multiplayer chat and
    // never sends network messages. Accepted intents enter the existing dispatcher.
    public sealed partial class CommanderChatUI : MonoBehaviour
    {
        private const int TranscriptCapacity = 64;
        private const int TranscriptMessageMaximum = 32768;
        private static CommanderChatUI instance;
        private readonly StringBuilder transcript = new StringBuilder();
        private readonly List<string> transcriptEntries = new List<string>();
        private GameObject canvasRoot;
        private TMP_Text transcriptText;
        private TMP_InputField inputField;
        private Button sendButton;
        private CommanderAIIntentAdapter adapter;
        private CancellationTokenSource lifetime;
        private Coroutine bootstrapWait;
        private readonly CommanderIntentRouter intentRouter = new CommanderIntentRouter();
        private StrategicAIApprovalBridge strategicBridge;
        private StrategicPipeline strategicPipeline;
        private Button approveStrategyButton;
        private Button confirmStrategyButton;
        private Button dismissStrategyButton;
        private bool submitting;
        private int runtimeGeneration;

        public StrategicAIProviderResult LatestStrategicInterpretation { get; private set; }
        public StrategicDecisionRecord LatestStrategicDecision { get; private set; }
        public StrategicIntent PendingStrategicIntent => strategicBridge?.PendingIntent;
        public ConversationState Conversation { get; private set; } = new ConversationState(0);

        public string DisplayedTranscript => transcriptText != null
            ? transcriptText.text : transcript.ToString();
        public string InputText
        {
            get => inputField != null ? inputField.text : string.Empty;
            set { if (inputField != null) inputField.text = value ?? string.Empty; }
        }
        public CommanderAIChatSubmission LatestSubmission { get; private set; }
        public bool IsReady => adapter != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateLocalWindow()
        {
            if (Object.FindFirstObjectByType<CommanderChatUI>() != null) return;
            new GameObject("Commander Chat UI").AddComponent<CommanderChatUI>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            EnsureLocalSurface();
            canvasRoot.SetActive(false);
        }

        private void Start()
        {
            bootstrapWait = StartCoroutine(WaitForCommanderRuntime());
        }

        private IEnumerator WaitForCommanderRuntime()
        {
            while (GameBootstrapper.Instance == null
                || GameBootstrapper.Instance.Simulation == null
                || GameBootstrapper.Instance.Commander == null
                || GameBootstrapper.Instance.CommanderDispatcher == null)
                yield return null;

            GameBootstrapper bootstrap = GameBootstrapper.Instance;
            Initialize(CommanderAIProviderFactory.CreateDefault(), bootstrap.Simulation,
                bootstrap.Commander, bootstrap.CommanderDispatcher);
            if (bootstrap.CommanderStrategicPipeline != null)
                InitializeStrategic(StrategicAIInterpreterFactory.Create(), bootstrap.CommanderStrategicPipeline);
        }

        public void Initialize(ICommanderAIProvider provider, GameSimulation simulation,
            CommanderGoalManager goalManager, CommanderIntentDispatcher dispatcher,
            System.TimeSpan? providerTimeout = null)
        {
            // Awake is not invoked for a MonoBehaviour added by an EditMode test.
            // Keeping initialization idempotent also makes programmatic local hosts safe.
            EnsureLocalSurface();
            runtimeGeneration++;
            lifetime.Cancel();
            lifetime.Dispose();
            lifetime = new CancellationTokenSource();
            strategicBridge?.Dispose();
            strategicBridge = null;
            DetachStrategicPipeline();
            ResetExplanationState();
            adapter?.ResetHistory();
            Conversation?.Reset();
            transcriptEntries.Clear();
            transcript.Clear();
            if (transcriptText != null) transcriptText.text = string.Empty;
            submitting = false;
            adapter = new CommanderAIIntentAdapter(provider, simulation, goalManager, dispatcher,
                null, providerTimeout);
            Conversation = new ConversationState(goalManager.PlayerId);
            LatestStrategicInterpretation = null;
            LatestStrategicDecision = null;
            LatestSubmission = null;
            inputField.interactable = true;
            sendButton.interactable = true;
            canvasRoot.SetActive(true);
            if (transcript.Length == 0)
                AppendLine("Commander", provider is GeminiAIProvider
                    ? "Gemini translator ready."
                    : "Local mock translator ready.", false);
        }

        public void InitializeStrategic(IStrategicAIInterpreter interpreter, StrategicPipeline pipeline,
            System.TimeSpan? providerTimeout = null)
        {
            if (interpreter == null) throw new System.ArgumentNullException(nameof(interpreter));
            if (pipeline == null) throw new System.ArgumentNullException(nameof(pipeline));
            if (Conversation == null || Conversation.PlayerId != pipeline.StrategicPlanner.PlayerId)
                throw new System.ArgumentException(
                    "The strategic runtime must belong to the active Commander player.", nameof(pipeline));
            if (strategicPipeline != null && !ReferenceEquals(strategicPipeline, pipeline))
                ResetConversation();
            runtimeGeneration++;
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = new CancellationTokenSource();
            strategicBridge?.Dispose();
            DetachStrategicPipeline();
            ResetExplanationState();
            strategicPipeline = pipeline;
            strategicBridge = new StrategicAIApprovalBridge(interpreter, pipeline.StrategicPlanner.IntentIds,
                () => pipeline.CaptureContext(), providerTimeout,
                () => Conversation?.Snapshot() ?? System.Array.Empty<MemoryEntry>());
            strategicPipeline.EvaluationCompleted += OnStrategicEvaluationCompleted;
            submitting = false;
            if (inputField != null) inputField.interactable = adapter != null;
            if (sendButton != null) sendButton.interactable = adapter != null;
            UpdateStrategicControls();
        }

        private void EnsureLocalSurface()
        {
            if (lifetime == null) lifetime = new CancellationTokenSource();
            if (canvasRoot == null) BuildUI();
        }

        public async Task<CommanderAIChatSubmission> SubmitMessageAsync(string message)
        {
            string trimmed = (message ?? string.Empty).Trim();
            if (trimmed.Length == 0) return null;
            if (TryHandleExplanationQuery(trimmed)) return null;
            if (adapter == null)
            {
                LatestSubmission = null;
                AppendLine("Commander", "Commander runtime is not ready yet.");
                return null;
            }

            string wholeForm = NormalizeWholeForm(trimmed);
            if (wholeForm == "clear memory")
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
            if (wholeForm == "focus cavalry")
            {
                AppendLine("Player", trimmed);
                Conversation.Memory.RecordCavalryPreference();
                AppendLine("Commander", "Cavalry attack focus remembered for this match.");
                return null;
            }
            if (submitting) return null;
            submitting = true;
            int generation = runtimeGeneration;
            IReadOnlyList<MemoryEntry> memoryAtTurnStart = Conversation.Snapshot();
            strategicBridge?.ClearPending();
            LatestStrategicInterpretation = null;
            LatestSubmission = null;
            UpdateStrategicControls();
            AppendLine("Player", trimmed);
            inputField.interactable = false;
            sendButton.interactable = false;
            try
            {
                CommanderTextRoute route = intentRouter.Classify(trimmed);
                if (route == CommanderTextRoute.Rejected)
                {
                    AppendLine("Commander", "Unsupported or mixed Commander request.");
                    return null;
                }
                if (route == CommanderTextRoute.Strategic)
                {
                    if (strategicBridge == null)
                    {
                        AppendLine("Commander", "Strategic Commander is not ready.");
                        return null;
                    }
                    var result = await strategicBridge.TranslateWithMemoryAsync(trimmed,
                        memoryAtTurnStart, lifetime.Token);
                    if (this == null || generation != runtimeGeneration || lifetime.IsCancellationRequested) return null;
                    LatestStrategicInterpretation = result;
                    AppendLine("Commander", result.Success
                        ? result.Intent.ObjectiveType + " recommendation. No plan started. Approve normally, or explicitly confirm as your command (may replace AI plans, never direct-player plans)."
                        : result.ExplanationText);
                    UpdateStrategicControls();
                    return null;
                }
                CommanderAIChatSubmission tacticalResult = await adapter.SubmitAsync(trimmed, lifetime.Token);
                if (this != null && generation == runtimeGeneration)
                {
                    LatestSubmission = tacticalResult;
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
                    inputField.text = string.Empty;
                    UpdateStrategicControls();
                }
            }
        }

        public StrategicDecisionRecord ApproveStrategicRecommendation() => SubmitPendingStrategy(false);
        public StrategicDecisionRecord ConfirmStrategicCommand() => SubmitPendingStrategy(true);

        private StrategicDecisionRecord SubmitPendingStrategy(bool confirmed)
        {
            if (submitting || strategicPipeline == null || strategicBridge?.PendingIntent == null
                || lifetime.IsCancellationRequested) return null;
            int id = strategicBridge.PendingIntent.IntentId;
            StrategicIntent intent = confirmed ? strategicBridge.Confirm(id) : strategicBridge.TakeRecommendation(id);
            if (intent == null) return null;
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
            UpdateStrategicControls();
            return LatestStrategicDecision;
        }

        public void DismissStrategicRecommendation()
        {
            strategicBridge?.ClearPending();
            UpdateStrategicControls();
        }

        public void ResetConversation()
        {
            runtimeGeneration++;
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = new CancellationTokenSource();
            Conversation.Reset();
            adapter?.ResetHistory();
            transcript.Clear();
            transcriptEntries.Clear();
            if (transcriptText != null) transcriptText.text = string.Empty;
            strategicBridge?.Reset();
            LatestStrategicInterpretation = null;
            LatestStrategicDecision = null;
            LatestSubmission = null;
            ResetExplanationState();
            submitting = false;
            if (inputField != null)
            {
                inputField.text = string.Empty;
                inputField.interactable = adapter != null;
            }
            if (sendButton != null) sendButton.interactable = adapter != null;
            UpdateStrategicControls();
        }

        private void UpdateStrategicControls()
        {
            bool available = !submitting && strategicBridge?.PendingIntent != null;
            if (approveStrategyButton != null) approveStrategyButton.interactable = available;
            if (confirmStrategyButton != null) confirmStrategyButton.interactable = available;
            if (dismissStrategyButton != null) dismissStrategyButton.interactable = available;
        }

        public Task<CommanderAIChatSubmission> SubmitCurrentInputAsync()
        {
            return SubmitMessageAsync(InputText);
        }

        private async void SubmitFromUI()
        {
            await SubmitCurrentInputAsync();
        }

        private void AppendLine(string speaker, string message, bool remember = true)
        {
            string bounded = message ?? string.Empty;
            if (bounded.Length > TranscriptMessageMaximum)
                bounded = bounded.Substring(0, TranscriptMessageMaximum);
            transcriptEntries.Add((speaker ?? string.Empty) + ":" + System.Environment.NewLine + bounded);
            while (transcriptEntries.Count > TranscriptCapacity) transcriptEntries.RemoveAt(0);
            transcript.Clear();
            for (int i = 0; i < transcriptEntries.Count; i++)
            {
                if (i > 0) transcript.AppendLine().AppendLine();
                transcript.Append(transcriptEntries[i]);
            }
            if (transcriptText != null)
                transcriptText.text = transcript.ToString();
            if (remember && Conversation != null)
            {
                if (speaker == "Player")
                    Conversation.Memory.RecordConversation(CommanderConversationRole.Player, bounded);
                else if (speaker == "Commander")
                    Conversation.Memory.RecordConversation(CommanderConversationRole.Commander, bounded);
            }
        }

        private void OnStrategicEvaluationCompleted(StrategicDecisionRecord record)
        {
            if (record == null || Conversation == null) return;
            ProjectStrategicExplanation(record);
            StrategicIntentSubmission submission = record.Submission;
            int? intentId = submission?.Intent?.IntentId;
            int? planId = submission?.CreatedPlan == true
                ? submission.Plan.StrategicPlanId : null;
            string objective = submission?.Intent == null
                ? string.Empty : submission.Intent.ObjectiveType.ToString();
            string status = submission?.CreatedPlan == true ? "Accepted"
                : submission != null ? "SubmissionRejected"
                : record.Decision?.Status == StrategicDecisionStatus.Rejected ? "Rejected"
                : record.Decision?.Status == StrategicDecisionStatus.NoDecision ? "NoDecision"
                : !record.TransitionAllowed ? "TransitionRefused"
                : record.Decision?.HasSelection == true ? "SelectionNotSubmitted" : "Rejected";
            Conversation.Memory.RecordDecision(status, record.Outcome, intentId, planId, objective);
            if (submission?.CreatedPlan == true)
                Conversation.Memory.RecordApprovedStrategy(objective,
                    submission.Intent.IntentId, submission.Plan.StrategicPlanId, record.Outcome);
        }

        private void DetachStrategicPipeline()
        {
            if (strategicPipeline != null)
                strategicPipeline.EvaluationCompleted -= OnStrategicEvaluationCompleted;
            strategicPipeline = null;
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
            if (bootstrapWait != null) StopCoroutine(bootstrapWait);
            lifetime?.Cancel();
            strategicBridge?.Dispose();
            DetachStrategicPipeline();
            ResetExplanationState();
            adapter?.ResetHistory();
            Conversation?.Reset();
            transcriptEntries.Clear();
            transcript.Clear();
            lifetime?.Dispose();
            UnitSelectionManager.SetChatFocused(false);
            if (instance == this) instance = null;
        }

        private void BuildUI()
        {
            canvasRoot = new GameObject("CommanderCanvas");
            canvasRoot.transform.SetParent(transform, false);
            Canvas canvas = canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;
            CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRoot.AddComponent<GraphicRaycaster>();

            GameObject panel = UIObject("Panel", canvasRoot.transform);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0, 1);
            panelRect.pivot = new Vector2(0, 1);
            panelRect.anchoredPosition = new Vector2(16, -72);
            panelRect.sizeDelta = new Vector2(430, 310);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.035f, 0.045f, 0.06f, 0.94f);

            TMP_Text title = Text("Title", panel.transform, "Commander", 19,
                TextAlignmentOptions.Left);
            SetRect(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(12, -38), new Vector2(-12, -8));
            title.color = new Color(0.85f, 0.9f, 1f);

            GameObject scrollObject = UIObject("Transcript", panel.transform);
            RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
            SetRect(scrollRectTransform, new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(10, 86), new Vector2(-10, -43));
            Image scrollImage = scrollObject.AddComponent<Image>();
            scrollImage.color = new Color(0, 0, 0, 0.32f);
            ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();

            GameObject viewport = UIObject("Viewport", scrollObject.transform);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            SetRect(viewportRect, Vector2.zero, Vector2.one,
                new Vector2(5, 5), new Vector2(-5, -5));
            viewport.AddComponent<RectMask2D>();
            scroll.viewport = viewportRect;

            GameObject content = UIObject("Content", viewport.transform);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = Vector2.zero;
            scroll.content = contentRect;
            transcriptText = content.AddComponent<TextMeshProUGUI>();
            transcriptText.fontSize = 13;
            transcriptText.color = Color.white;
            transcriptText.alignment = TextAlignmentOptions.TopLeft;
            transcriptText.textWrappingMode = TextWrappingModes.Normal;
            transcriptText.richText = false;
            transcriptText.raycastTarget = false;
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject inputObject = UIObject("Input", panel.transform);
            RectTransform inputRect = inputObject.GetComponent<RectTransform>();
            SetRect(inputRect, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(10, 10), new Vector2(-91, 40));
            Image inputImage = inputObject.AddComponent<Image>();
            inputImage.color = new Color(0.11f, 0.13f, 0.17f, 1f);
            inputField = inputObject.AddComponent<TMP_InputField>();
            inputField.targetGraphic = inputImage;
            inputField.lineType = TMP_InputField.LineType.SingleLine;
            inputField.characterLimit = 240;
            inputField.interactable = false;
            inputField.navigation = new Navigation { mode = Navigation.Mode.None };

            GameObject textArea = UIObject("Text Area", inputObject.transform);
            RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
            SetRect(textAreaRect, Vector2.zero, Vector2.one,
                new Vector2(7, 3), new Vector2(-7, -3));
            textArea.AddComponent<RectMask2D>();
            inputField.textViewport = textAreaRect;

            TMP_Text placeholder = Text("Placeholder", textArea.transform,
                "Give a tactical order or strategic request...", 12, TextAlignmentOptions.Left);
            placeholder.color = new Color(0.65f, 0.68f, 0.72f, 0.9f);
            placeholder.fontStyle = FontStyles.Italic;
            SetRect(placeholder.rectTransform, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            inputField.placeholder = placeholder;

            TMP_Text inputText = Text("Text", textArea.transform, string.Empty, 12,
                TextAlignmentOptions.Left);
            SetRect(inputText.rectTransform, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            inputField.textComponent = inputText;
            inputField.onSubmit.AddListener(_ => SubmitFromUI());
            inputField.onSelect.AddListener(_ => UnitSelectionManager.SetChatFocused(true));
            inputField.onDeselect.AddListener(_ => UnitSelectionManager.SetChatFocused(false));

            GameObject buttonObject = UIObject("Send", panel.transform);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            SetRect(buttonRect, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-82, 10), new Vector2(-10, 40));
            Image buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = new Color(0.18f, 0.36f, 0.62f, 1f);
            sendButton = buttonObject.AddComponent<Button>();
            sendButton.targetGraphic = buttonImage;
            sendButton.interactable = false;
            sendButton.onClick.AddListener(SubmitFromUI);
            TMP_Text buttonText = Text("Label", buttonObject.transform, "Send", 12,
                TextAlignmentOptions.Center);
            SetRect(buttonText.rectTransform, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            approveStrategyButton = StrategyButton(panel.transform, "Approve strategy", 10, 130,
                () => ApproveStrategicRecommendation());
            confirmStrategyButton = StrategyButton(panel.transform, "Confirm as command", 145, 170,
                () => ConfirmStrategicCommand());
            dismissStrategyButton = StrategyButton(panel.transform, "Dismiss", 320, 100,
                DismissStrategicRecommendation);
        }

        private static Button StrategyButton(Transform parent, string label, float left, float width,
            UnityEngine.Events.UnityAction action)
        {
            GameObject obj = UIObject(label, parent);
            SetRect(obj.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero,
                new Vector2(left, 48), new Vector2(left + width, 78));
            var image = obj.AddComponent<Image>();
            image.color = new Color(0.18f, 0.29f, 0.42f, 1f);
            var button = obj.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = false;
            button.onClick.AddListener(action);
            var text = Text("Label", obj.transform, label, 11, TextAlignmentOptions.Center);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return button;
        }

        private static GameObject UIObject(string name, Transform parent)
        {
            var result = new GameObject(name, typeof(RectTransform));
            result.transform.SetParent(parent, false);
            return result;
        }

        private static TMP_Text Text(string name, Transform parent, string value,
            float fontSize, TextAlignmentOptions alignment)
        {
            GameObject obj = UIObject(name, parent);
            var text = obj.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

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
```

### Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Explanations.cs

```csharp
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace OpenEmpires
{
    public sealed partial class CommanderChatUI
    {
        private readonly CommanderExplanationService explanationService =
            new CommanderExplanationService();
        private ExplanationContext latestExplanationContext;
        private int? pendingExplanationIntentId;
        private string pendingExplanationObjective;

        public ExplanationResult LatestExplanation { get; private set; }

        private bool TryHandleExplanationQuery(string message)
        {
            string normalized = NormalizeExplanationWholeForm(message);
            CommanderExplanationQuery query;
            switch (normalized)
            {
                case "why are we not attacking":
                    query = CommanderExplanationQuery.AttackReason;
                    break;
                case "why was that rejected":
                    query = CommanderExplanationQuery.LastRejection;
                    break;
                case "explain last decision":
                    query = CommanderExplanationQuery.LastDecision;
                    break;
                case "what is the plan doing":
                    query = CommanderExplanationQuery.CurrentPlan;
                    break;
                default:
                    return false;
            }

            int playerId = Conversation?.PlayerId ?? 0;
            ExplanationContext context = latestExplanationContext
                ?? new ExplanationContext(playerId);
            if (query == CommanderExplanationQuery.CurrentPlan)
                context = CopyCurrentPlanContext(context, playerId);
            LatestExplanation = explanationService.Explain(context, query);
            AppendLine("Player", (message ?? string.Empty).Trim(), false);
            AppendLine("Commander", LatestExplanation.DisplayText, false);
            Conversation?.Memory.RecordExplanation(LatestExplanation.DisplayText);
            return true;
        }

        private ExplanationContext CopyCurrentPlanContext(ExplanationContext source, int playerId)
        {
            int? snapshotTick = null;
            var plans = new List<ExplanationPlanState>();
            if (strategicPipeline != null)
            {
                StrategicContext snapshot = strategicPipeline.CaptureContext();
                snapshotTick = snapshot.SnapshotTick;
                for (int i = 0; i < snapshot.ActivePlans.Count
                    && plans.Count < ExplanationContext.MaximumPlans; i++)
                {
                    StrategicPlanState plan = snapshot.ActivePlans[i];
                    plans.Add(new ExplanationPlanState(plan.StrategicPlanId, plan.PlanType,
                        plan.Status, plan.CurrentMilestone, plan.MilestoneStatus, plan.Reason));
                }
            }
            return new ExplanationContext(playerId, source.DecisionId, source.DecisionTick,
                source.Outcome, source.Reason, source.RequestedObjective,
                source.AcceptedPlanId, source.AcceptedPlanType, snapshotTick, plans);
        }

        private void ProjectStrategicExplanation(StrategicDecisionRecord record)
        {
            if (record == null || Conversation == null) return;
            StrategicIntentSubmission submission = record.Submission;
            ExplanationOutcome outcome;
            if (submission?.CreatedPlan == true)
                outcome = ExplanationOutcome.PlanCreated;
            else if (submission != null)
                outcome = ExplanationOutcome.PlannerRejected;
            else if (record.Decision?.Status == StrategicDecisionStatus.Rejected)
                outcome = ExplanationOutcome.Rejected;
            else if (record.Decision?.Status == StrategicDecisionStatus.NoDecision)
                outcome = ExplanationOutcome.NoDecision;
            else if (record.Decision?.HasSelection == true && !record.TransitionAllowed)
                outcome = ExplanationOutcome.TransitionRefused;
            else if (record.Decision?.HasSelection == true)
                outcome = ExplanationOutcome.SelectionNotSubmitted;
            else
                outcome = ExplanationOutcome.Rejected;

            if (outcome == ExplanationOutcome.NoDecision
                && latestExplanationContext != null
                && latestExplanationContext.Outcome != ExplanationOutcome.NoDecision)
                return;

            StrategicIntent sourceIntent = submission?.Intent ?? record.Decision?.SelectedIntent;
            string objective = string.Empty;
            if (pendingExplanationIntentId.HasValue
                && (sourceIntent == null
                    || sourceIntent.IntentId == pendingExplanationIntentId.Value))
                objective = pendingExplanationObjective ?? string.Empty;
            else if (sourceIntent != null)
                objective = sourceIntent.ObjectiveType.ToString();

            latestExplanationContext = new ExplanationContext(Conversation.PlayerId,
                record.DecisionId, record.Tick, outcome, record.Outcome, objective,
                submission?.CreatedPlan == true ? submission.Plan.StrategicPlanId : (int?)null,
                submission?.CreatedPlan == true ? submission.Plan.PlanType.ToString() : string.Empty);
        }

        private void ResetExplanationState()
        {
            latestExplanationContext = null;
            LatestExplanation = null;
            pendingExplanationIntentId = null;
            pendingExplanationObjective = null;
        }

        private static string NormalizeExplanationWholeForm(string message)
        {
            string text = Regex.Replace((message ?? string.Empty).Trim().ToLowerInvariant(),
                @"\s+", " ");
            if (text.EndsWith("?", StringComparison.Ordinal)
                || text.EndsWith(".", StringComparison.Ordinal))
                text = text.Substring(0, text.Length - 1).TrimEnd();
            return text;
        }
    }
}
```

### Assets/Scripts/AI/Commander/Phase4C/ExplanationContext.cs

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OpenEmpires
{
    public enum ExplanationOutcome
    {
        NoDecision,
        Rejected,
        TransitionRefused,
        PlannerRejected,
        PlanCreated,
        SelectionNotSubmitted
    }

    public enum CommanderExplanationQuery
    {
        LastDecision,
        LastRejection,
        AttackReason,
        CurrentPlan
    }

    public sealed class ExplanationPlanState
    {
        public const int MaximumTextLength = 512;
        public int PlanId { get; }
        public string PlanType { get; }
        public string Status { get; }
        public string CurrentMilestone { get; }
        public string MilestoneStatus { get; }
        public string Reason { get; }

        public ExplanationPlanState(int planId, string planType, string status,
            string currentMilestone, string milestoneStatus, string reason)
        {
            if (planId < 0) throw new ArgumentOutOfRangeException(nameof(planId));
            PlanId = planId;
            PlanType = Bound(planType);
            Status = Bound(status);
            CurrentMilestone = Bound(currentMilestone);
            MilestoneStatus = Bound(milestoneStatus);
            Reason = Bound(reason);
        }

        private static string Bound(string value)
        {
            value = value ?? string.Empty;
            return value.Length <= MaximumTextLength
                ? value : value.Substring(0, MaximumTextLength);
        }
    }

    public sealed class ExplanationContext
    {
        public const int MaximumPlans = 32;
        public const int MaximumTextLength = 512;
        public int PlayerId { get; }
        public int? DecisionId { get; }
        public int? DecisionTick { get; }
        public ExplanationOutcome Outcome { get; }
        public string Reason { get; }
        public string RequestedObjective { get; }
        public int? AcceptedPlanId { get; }
        public string AcceptedPlanType { get; }
        public int? CurrentSnapshotTick { get; }
        public IReadOnlyList<ExplanationPlanState> CurrentPlans { get; }

        public ExplanationContext(int playerId, int? decisionId = null,
            int? decisionTick = null, ExplanationOutcome outcome = ExplanationOutcome.NoDecision,
            string reason = null, string requestedObjective = null,
            int? acceptedPlanId = null, string acceptedPlanType = null,
            int? currentSnapshotTick = null,
            IReadOnlyList<ExplanationPlanState> currentPlans = null)
        {
            if (playerId < 0) throw new ArgumentOutOfRangeException(nameof(playerId));
            if (decisionId.HasValue && decisionId.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(decisionId));
            if (decisionTick.HasValue && decisionTick.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(decisionTick));
            if (acceptedPlanId.HasValue && acceptedPlanId.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(acceptedPlanId));
            if (currentSnapshotTick.HasValue && currentSnapshotTick.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(currentSnapshotTick));
            if (!Enum.IsDefined(typeof(ExplanationOutcome), outcome))
                throw new ArgumentOutOfRangeException(nameof(outcome));
            PlayerId = playerId;
            DecisionId = decisionId;
            DecisionTick = decisionTick;
            Outcome = outcome;
            Reason = Bound(reason);
            RequestedObjective = Bound(requestedObjective);
            AcceptedPlanId = acceptedPlanId;
            AcceptedPlanType = Bound(acceptedPlanType);
            CurrentSnapshotTick = currentSnapshotTick;
            var plans = new List<ExplanationPlanState>();
            if (currentPlans != null)
                for (int i = 0; i < currentPlans.Count && plans.Count < MaximumPlans; i++)
                    if (currentPlans[i] != null)
                        plans.Add(currentPlans[i]);
            CurrentPlans = new ReadOnlyCollection<ExplanationPlanState>(plans);
        }

        private static string Bound(string value)
        {
            value = value ?? string.Empty;
            return value.Length <= MaximumTextLength
                ? value : value.Substring(0, MaximumTextLength);
        }
    }
}
```

### Assets/Scripts/AI/Commander/Phase4C/ExplanationResult.cs

```csharp
namespace OpenEmpires
{
    public sealed class ExplanationResult
    {
        public const int MaximumDisplayTextLength = 8192;
        public string DisplayText { get; }
        public ExplanationOutcome Outcome { get; }

        public ExplanationResult(string displayText, ExplanationOutcome outcome)
        {
            displayText = displayText ?? string.Empty;
            DisplayText = displayText.Length <= MaximumDisplayTextLength
                ? displayText : displayText.Substring(0, MaximumDisplayTextLength);
            Outcome = outcome;
        }
    }
}
```

### Assets/Scripts/AI/Commander/Phase4C/CommanderExplanationService.cs

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenEmpires
{
    public sealed class CommanderExplanationService
    {
        public ExplanationResult Explain(ExplanationContext context,
            CommanderExplanationQuery query = CommanderExplanationQuery.LastDecision)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!Enum.IsDefined(typeof(CommanderExplanationQuery), query))
                throw new ArgumentOutOfRangeException(nameof(query));
            string text;
            switch (query)
            {
                case CommanderExplanationQuery.LastDecision:
                    text = RenderDecision(context);
                    break;
                case CommanderExplanationQuery.LastRejection:
                    text = IsRejection(context.Outcome)
                        ? RenderDecision(context)
                        : "The latest recorded outcome was " + context.Outcome
                            + ", not a rejection.";
                    break;
                case CommanderExplanationQuery.AttackReason:
                    text = IsRejection(context.Outcome)
                        && string.Equals(context.RequestedObjective, "AttackPreparation",
                            StringComparison.Ordinal)
                        ? RenderDecision(context)
                        : "No recorded rejection is attributable to AttackPreparation. "
                            + "The latest recorded outcome was " + context.Outcome + ".";
                    break;
                case CommanderExplanationQuery.CurrentPlan:
                    text = RenderCurrentPlans(context);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(query));
            }
            return new ExplanationResult(text, context.Outcome);
        }

        private static bool IsRejection(ExplanationOutcome outcome)
        {
            return outcome == ExplanationOutcome.Rejected
                || outcome == ExplanationOutcome.TransitionRefused
                || outcome == ExplanationOutcome.PlannerRejected;
        }

        private static string RenderDecision(ExplanationContext context)
        {
            if (context.Outcome == ExplanationOutcome.NoDecision)
                return "No recorded decision is available.";

            var result = new StringBuilder();
            switch (context.Outcome)
            {
                case ExplanationOutcome.Rejected:
                    result.Append("The recorded decision was rejected.");
                    break;
                case ExplanationOutcome.TransitionRefused:
                    result.Append("The selected intent was blocked before submission.");
                    break;
                case ExplanationOutcome.PlannerRejected:
                    result.Append("The planner rejected the submitted intent. No accepted plan was created.");
                    break;
                case ExplanationOutcome.PlanCreated:
                    if (context.AcceptedPlanId.HasValue)
                    {
                        result.Append("The decision created plan #")
                            .Append(context.AcceptedPlanId.Value.ToString(CultureInfo.InvariantCulture));
                        if (context.AcceptedPlanType.Length > 0)
                            result.Append(" (").Append(context.AcceptedPlanType).Append(')');
                        result.Append('.');
                    }
                    else result.Append("The decision created an accepted plan.");
                    break;
                case ExplanationOutcome.SelectionNotSubmitted:
                    result.Append("An intent was selected but was not submitted.");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(context.Outcome));
            }

            if (context.DecisionId.HasValue)
                result.Append(" Decision ID: ")
                    .Append(context.DecisionId.Value.ToString(CultureInfo.InvariantCulture)).Append('.');
            if (context.DecisionTick.HasValue)
                result.Append(" Historical decision tick: ")
                    .Append(context.DecisionTick.Value.ToString(CultureInfo.InvariantCulture)).Append('.');
            if (context.RequestedObjective.Length > 0)
                result.Append(" Requested objective: ").Append(context.RequestedObjective).Append('.');
            result.Append(context.Reason.Length > 0
                ? " Recorded reason: " + context.Reason
                : " No recorded reason is available.");
            return result.ToString();
        }

        private static string RenderCurrentPlans(ExplanationContext context)
        {
            if (!context.CurrentSnapshotTick.HasValue)
                return "Current plan state is unavailable.";
            string tick = context.CurrentSnapshotTick.Value.ToString(CultureInfo.InvariantCulture);
            if (context.CurrentPlans.Count == 0)
                return "No active plan was observed at current snapshot tick " + tick
                    + ". This does not assert that any historical plan completed.";

            var plans = new List<ExplanationPlanState>(context.CurrentPlans);
            plans.Sort((left, right) => left.PlanId.CompareTo(right.PlanId));
            var result = new StringBuilder("Current snapshot tick ").Append(tick)
                .Append(" contains observed plan progress.");
            if (plans.Count == ExplanationContext.MaximumPlans)
                result.Append(" This view is showing up to 32 plans.");
            for (int i = 0; i < plans.Count; i++)
            {
                ExplanationPlanState plan = plans[i];
                result.Append(" Plan #")
                    .Append(plan.PlanId.ToString(CultureInfo.InvariantCulture)).Append(' ')
                    .Append(ValueOrUnavailable(plan.PlanType))
                    .Append("; observed status: ").Append(ValueOrUnavailable(plan.Status))
                    .Append("; observed current milestone: ")
                    .Append(ValueOrUnavailable(plan.CurrentMilestone))
                    .Append("; observed milestone status: ")
                    .Append(ValueOrUnavailable(plan.MilestoneStatus))
                    .Append("; observed reason: ").Append(ValueOrUnavailable(plan.Reason)).Append('.');
            }
            return result.ToString();
        }

        private static string ValueOrUnavailable(string value)
        {
            return string.IsNullOrEmpty(value) ? "unavailable" : value;
        }
    }
}
```

### Assets/Tests/EditMode/CommanderPhase4C2Tests.cs

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4C2")]
    public sealed class CommanderPhase4C2Tests
    {
        [Test]
        public void Explanation_MatchesDecisionReason()
        {
            var context = new ExplanationContext(playerId: 0, decisionId: 17,
                decisionTick: 450, outcome: ExplanationOutcome.Rejected,
                reason: "Insufficient available gold.", requestedObjective: "AttackPreparation");

            ExplanationResult result = new CommanderExplanationService().Explain(context);

            Assert.That(result.DisplayText, Does.Contain("Insufficient available gold."));
            Assert.That(result.DisplayText, Does.Contain("450"));
            Assert.That(result.DisplayText, Does.Contain("Historical decision tick"));
            Assert.That(result.DisplayText, Does.Not.Contain("gold income"));
            Assert.That(result.Outcome, Is.EqualTo(ExplanationOutcome.Rejected));
        }

        [TestCase(ExplanationOutcome.NoDecision, "No recorded decision is available")]
        [TestCase(ExplanationOutcome.Rejected, "recorded decision was rejected")]
        [TestCase(ExplanationOutcome.TransitionRefused, "blocked before submission")]
        [TestCase(ExplanationOutcome.PlannerRejected, "planner rejected the submitted intent")]
        [TestCase(ExplanationOutcome.PlanCreated, "created plan #8 (CavalryPressure)")]
        [TestCase(ExplanationOutcome.SelectionNotSubmitted, "selected but was not submitted")]
        public void Outcomes_HaveHandSpecifiedMeaning(ExplanationOutcome outcome, string meaning)
        {
            var context = new ExplanationContext(0, 4, 90, outcome, "recorded reason",
                "AttackPreparation", 8, "CavalryPressure");

            Assert.That(new CommanderExplanationService().Explain(context).DisplayText,
                Does.Contain(meaning));
        }

        [Test]
        public void RejectedPlanHasReason()
        {
            var context = new ExplanationContext(0, 2, 100,
                ExplanationOutcome.PlannerRejected, "No feasible placement was found.",
                "DefensivePreparation");

            string text = new CommanderExplanationService().Explain(context).DisplayText;

            Assert.That(text, Does.Contain("No feasible placement was found."));
            Assert.That(text, Does.Contain("DefensivePreparation"));
            Assert.That(text, Does.Contain("No accepted plan was created"));
        }

        [Test]
        public void MissingReason_StatesEvidenceIsUnavailable()
        {
            var context = new ExplanationContext(0, 2, 100, ExplanationOutcome.Rejected);

            Assert.That(new CommanderExplanationService().Explain(context).DisplayText,
                Does.Contain("No recorded reason is available"));
        }

        [Test]
        public void Context_CopiesAndBoundsPrimitiveInputs()
        {
            var source = new List<ExplanationPlanState>();
            for (int i = 40; i >= 1; i--)
                source.Add(new ExplanationPlanState(i, new string('p', 700), new string('s', 700),
                    new string('m', 700), new string('t', 700), new string('r', 700)));
            var context = new ExplanationContext(0, reason: new string('x', 700),
                requestedObjective: new string('o', 700), acceptedPlanType: new string('a', 700),
                currentSnapshotTick: 12, currentPlans: source);
            source.Clear();

            Assert.That(context.Reason.Length, Is.EqualTo(512));
            Assert.That(context.RequestedObjective.Length, Is.EqualTo(512));
            Assert.That(context.AcceptedPlanType.Length, Is.EqualTo(512));
            Assert.That(context.CurrentPlans.Count, Is.EqualTo(32));
            Assert.That(context.CurrentPlans[0].PlanType.Length, Is.EqualTo(512));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<ExplanationPlanState>)context.CurrentPlans).Add(
                    new ExplanationPlanState(99, null, null, null, null, null)));
        }

        [Test]
        public void Context_RejectsInvalidOwnerIdentityTicksAndEnums()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplanationContext(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplanationContext(0, decisionId: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplanationContext(0, decisionTick: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplanationContext(0, acceptedPlanId: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplanationContext(0, currentSnapshotTick: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExplanationContext(0,
                outcome: (ExplanationOutcome)99));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ExplanationPlanState(-1, null, null, null, null, null));
        }

        [Test]
        public void CurrentPlan_IsSortedAndDistinguishesUnavailableEmptyAndCapped()
        {
            var service = new CommanderExplanationService();
            var unavailable = new ExplanationContext(0);
            var empty = new ExplanationContext(0, currentSnapshotTick: 45,
                currentPlans: Array.Empty<ExplanationPlanState>());
            var plans = Enumerable.Range(1, 32).Reverse().Select(i =>
                new ExplanationPlanState(i, "Type" + i, "Active", "Step" + i,
                    "InProgress", "Observed" + i)).ToList();
            var capped = new ExplanationContext(0, currentSnapshotTick: 46, currentPlans: plans);

            Assert.That(service.Explain(unavailable, CommanderExplanationQuery.CurrentPlan).DisplayText,
                Does.Contain("Current plan state is unavailable"));
            Assert.That(service.Explain(empty, CommanderExplanationQuery.CurrentPlan).DisplayText,
                Does.Contain("No active plan was observed at current snapshot tick 45"));
            string rendered = service.Explain(capped, CommanderExplanationQuery.CurrentPlan).DisplayText;
            Assert.That(rendered.IndexOf("Plan #1 ", StringComparison.Ordinal),
                Is.LessThan(rendered.IndexOf("Plan #2 ", StringComparison.Ordinal)));
            Assert.That(rendered, Does.Contain("showing up to 32 plans"));
            Assert.That(rendered, Does.Contain("observed current milestone"));
            Assert.That(rendered, Does.Not.Contain("will"));
        }

        [Test]
        public void QuerySemantics_DoNotFabricateRejectionOrAttackAttribution()
        {
            var service = new CommanderExplanationService();
            var created = new ExplanationContext(0, 1, 2, ExplanationOutcome.PlanCreated,
                "Accepted.", "DefensivePreparation", 5, "DefensivePreparation");
            var unknown = new ExplanationContext(0, 2, 3, ExplanationOutcome.Rejected,
                "Insufficient available gold.");

            Assert.That(service.Explain(created, CommanderExplanationQuery.LastRejection).DisplayText,
                Does.Contain("not a rejection"));
            string attack = service.Explain(unknown, CommanderExplanationQuery.AttackReason).DisplayText;
            Assert.That(attack, Does.Contain("No recorded rejection is attributable to AttackPreparation"));
            Assert.That(attack, Does.Not.Contain("Insufficient available gold."));
            Assert.That(attack, Does.Not.Contain("income"));
        }

        [Test]
        public void Rendering_IsInvariantAndBounded()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fa-IR");
                var context = new ExplanationContext(0, 17, 450, ExplanationOutcome.Rejected,
                    new string('z', 512), "AttackPreparation");
                var result = new CommanderExplanationService().Explain(context);
                Assert.That(result.DisplayText, Does.Contain("450"));
                Assert.That(result.DisplayText.Length, Is.LessThanOrEqualTo(8192));
                Assert.That(new ExplanationResult(new string('q', 9000),
                    ExplanationOutcome.NoDecision).DisplayText.Length, Is.EqualTo(8192));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [Test]
        public void Service_RejectsNullContextAndInvalidQuery()
        {
            var service = new CommanderExplanationService();
            Assert.Throws<ArgumentNullException>(() => service.Explain(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => service.Explain(
                new ExplanationContext(0), (CommanderExplanationQuery)99));
        }
    }
}
```

### Assets/Tests/PlayMode/CommanderPhase4C2PlayModeTests.cs

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase4C2")]
    public sealed class CommanderPhase4C2PlayModeTests
    {
        private SimulationConfig config;
        private GameSimulation simulation;
        private CommanderGoalManager goals;
        private StrategicPlanner planner;
        private StrategicPipeline pipeline;
        private CommanderIntentDispatcher dispatcher;
        private CommanderChatUI chat;

        [SetUp]
        public void SetUp()
        {
            foreach (var existing in UnityEngine.Object.FindObjectsByType<CommanderChatUI>())
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            config = ScriptableObject.CreateInstance<SimulationConfig>();
            simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
            simulation.SetPlayerCivilizations(new[] { Civilization.French, Civilization.French });
            ((int[])typeof(GameSimulation).GetField("playerAges",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(simulation))[0] = 3;
            var resources = simulation.ResourceManager.GetPlayerResources(0);
            resources.Food = resources.Wood = resources.Gold = resources.Stone = 5000;
            int x = simulation.MapData.Width / 2;
            int z = simulation.MapData.Height / 2;
            typeof(MapData).GetField("holeMap", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(simulation.MapData, null);
            foreach (var node in simulation.MapData.GetAllResourceNodes()) node.RemainingAmount = 0;
            for (int tx = x - 35; tx <= x + 35; tx++)
                for (int tz = z - 22; tz <= z + 22; tz++)
                {
                    simulation.MapData.Tiles[tx, tz] = TileType.Grass;
                    simulation.MapData.ForestDensity[tx, tz] = 0;
                    simulation.MapData.FoundationCount[tx, tz] = 0;
                    simulation.FogOfWar.SetVisible(0, tx, tz);
                }
            simulation.CreateBuilding(0, BuildingType.TownCenter, x + 12, z, false, true)
                .AutoProduceVillagers = false;
            simulation.CreateBuilding(0, BuildingType.House, x + 18, z, false);
            simulation.CreateBuilding(0, BuildingType.House, x + 22, z, false);
            simulation.CreateBuilding(0, BuildingType.House, x + 18, z + 6, false);
            simulation.CreateBuilding(0, BuildingType.Barracks, x - 10, z, false);
            simulation.CreateBuilding(0, BuildingType.ArcheryRange, x - 16, z, false);
            simulation.CreateBuilding(0, BuildingType.Stables, x - 22, z, false);
            Gatherers(ResourceType.Food, 10, x - 22, z);
            Gatherers(ResourceType.Gold, 6, x, z);
            Gatherers(ResourceType.Wood, 4, x - 10, z);
            for (int i = 0; i < 12; i++) AddUnit(2);
            goals = new CommanderGoalManager(simulation, 0);
            planner = new StrategicPlanner(goals, CurrentResource);
            pipeline = new StrategicPipeline(simulation, goals, planner);
            dispatcher = new CommanderIntentDispatcher(simulation, goals, strategicPlanner: planner);
            chat = new GameObject("Phase4C2RuntimeChat").AddComponent<CommanderChatUI>();
            chat.enabled = false;
            chat.Initialize(new MockAIProvider(), simulation, goals, dispatcher);
        }

        [TearDown]
        public void TearDown()
        {
            if (chat != null) UnityEngine.Object.DestroyImmediate(chat.gameObject);
            dispatcher?.Dispose();
            pipeline?.Dispose();
            planner?.Dispose();
            goals?.Dispose();
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }

        [UnityTest]
        public IEnumerator RejectedAttackQuestion_MatchesRecordedReasonDespiteUnrelatedDefense()
        {
            var provider = new CountingStrategicProvider();
            chat.InitializeStrategic(provider, pipeline);
            StrategicPlan defense = CreateEmergencyDefense();
            Task<CommanderAIChatSubmission> prepare = chat.SubmitMessageAsync("prepare cavalry attack");
            while (!prepare.IsCompleted) yield return null;
            StrategicDecisionRecord rejected = chat.ApproveStrategicRecommendation();
            Assert.That(rejected.Decision.Status, Is.EqualTo(StrategicDecisionStatus.Rejected));
            Assert.That(rejected.Outcome, Does.Contain("Emergency defense has higher priority"));

            Task<CommanderAIChatSubmission> explain =
                chat.SubmitMessageAsync("  WHY   are we not attacking?  ");
            while (!explain.IsCompleted) yield return null;

            Assert.That(chat.LatestExplanation, Is.Not.Null);
            Assert.That(chat.LatestExplanation.Outcome, Is.EqualTo(ExplanationOutcome.Rejected));
            Assert.That(chat.LatestExplanation.DisplayText, Does.Contain(rejected.Outcome));
            Assert.That(chat.LatestExplanation.DisplayText, Does.Contain("AttackPreparation"));
            Assert.That(chat.LatestExplanation.DisplayText, Does.Not.Contain("income"));
            Assert.That(defense.Status, Is.EqualTo(StrategicPlanStatus.Active));
            Assert.That(provider.CallCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ExplanationCannotModifyIntent()
        {
            var provider = new CountingStrategicProvider();
            chat.InitializeStrategic(provider, pipeline);
            CreateEmergencyDefense();
            Task<CommanderAIChatSubmission> first = chat.SubmitMessageAsync("prepare cavalry attack");
            while (!first.IsCompleted) yield return null;
            StrategicDecisionRecord projected = chat.ApproveStrategicRecommendation();
            Task<CommanderAIChatSubmission> second = chat.SubmitMessageAsync("prepare cavalry attack");
            while (!second.IsCompleted) yield return null;
            StrategicIntent pending = chat.PendingStrategicIntent;
            Assert.That(pending, Is.Not.Null);

            int history = pipeline.DecisionHistory.History.Count;
            int plans = planner.Plans.Count;
            int reservations = planner.Reservations.Count;
            int archivedReservations = planner.ArchivedReservations.Count;
            int goalCount = goals.Goals.Count;
            int commandCount = PendingCommandCount();
            string planState = PlanState();
            string reservationState = ReservationState();
            string goalState = GoalState();
            int memoryBefore = chat.Conversation.Snapshot().Count;
            string[] questions = {
                "why are we not attacking?", "why was that rejected.",
                "explain   last decision", "what is the plan doing?"
            };
            foreach (string question in questions)
            {
                Task<CommanderAIChatSubmission> ask = chat.SubmitMessageAsync(question);
                while (!ask.IsCompleted) yield return null;
            }

            Assert.That(chat.PendingStrategicIntent, Is.SameAs(pending));
            Assert.That(pending.Status, Is.EqualTo(StrategicIntentStatus.Created));
            Assert.That(chat.LatestStrategicDecision, Is.SameAs(projected));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(history));
            Assert.That(planner.Plans.Count, Is.EqualTo(plans));
            Assert.That(planner.Reservations.Count, Is.EqualTo(reservations));
            Assert.That(planner.ArchivedReservations.Count, Is.EqualTo(archivedReservations));
            Assert.That(goals.Goals.Count, Is.EqualTo(goalCount));
            Assert.That(PendingCommandCount(), Is.EqualTo(commandCount));
            Assert.That(PlanState(), Is.EqualTo(planState));
            Assert.That(ReservationState(), Is.EqualTo(reservationState));
            Assert.That(GoalState(), Is.EqualTo(goalState));
            Assert.That(provider.CallCount, Is.EqualTo(2));
            Assert.That(chat.Conversation.Snapshot().Skip(memoryBefore).Count(), Is.EqualTo(4));
            Assert.That(chat.Conversation.Snapshot().Skip(memoryBefore)
                .All(entry => entry.Kind == MemoryEntryKind.Explanation), Is.True);
        }

        [UnityTest]
        public IEnumerator CurrentPlanQuery_MatchesFreshDetachedSnapshotWithoutAdvancing()
        {
            var provider = new CountingStrategicProvider();
            chat.InitializeStrategic(provider, pipeline);
            Task<CommanderAIChatSubmission> prepare = chat.SubmitMessageAsync("prepare defenses");
            while (!prepare.IsCompleted) yield return null;
            StrategicDecisionRecord accepted = chat.ApproveStrategicRecommendation();
            Assert.That(accepted.Submission?.CreatedPlan, Is.True, accepted.Outcome);
            StrategicPlan plan = accepted.Submission.Plan;
            Assert.That(planner.CompleteMilestoneAndAdvance(plan.StrategicPlanId), Is.True);
            StrategicContext before = pipeline.CaptureContext();
            StrategicPlanState expected = before.ActivePlans.Single(p =>
                p.StrategicPlanId == plan.StrategicPlanId);
            string planState = PlanState();
            int history = pipeline.DecisionHistory.History.Count;
            int goalCount = goals.Goals.Count;
            int commandCount = PendingCommandCount();

            Task<CommanderAIChatSubmission> query = chat.SubmitMessageAsync("what is the plan doing?");
            while (!query.IsCompleted) yield return null;

            Assert.That(chat.LatestExplanation.DisplayText,
                Does.Contain("Current snapshot tick " + before.SnapshotTick));
            Assert.That(chat.LatestExplanation.DisplayText,
                Does.Contain("Plan #" + expected.StrategicPlanId));
            Assert.That(chat.LatestExplanation.DisplayText, Does.Contain(expected.CurrentMilestone));
            Assert.That(chat.LatestExplanation.DisplayText, Does.Contain(expected.MilestoneStatus));
            Assert.That(chat.LatestExplanation.DisplayText, Does.Not.Contain("will"));
            Assert.That(PlanState(), Is.EqualTo(planState));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(history));
            Assert.That(goals.Goals.Count, Is.EqualTo(goalCount));
            Assert.That(PendingCommandCount(), Is.EqualTo(commandCount));
            Assert.That(provider.CallCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ClearMemory_DropsExplanationSourceWithoutClearingGameHistory()
        {
            chat.InitializeStrategic(new CountingStrategicProvider(), pipeline);
            Task<CommanderAIChatSubmission> prepare = chat.SubmitMessageAsync("prepare defenses");
            while (!prepare.IsCompleted) yield return null;
            StrategicDecisionRecord accepted = chat.ApproveStrategicRecommendation();
            Assert.That(accepted, Is.Not.Null);
            int history = pipeline.DecisionHistory.History.Count;

            Task<CommanderAIChatSubmission> clear = chat.SubmitMessageAsync("clear memory");
            while (!clear.IsCompleted) yield return null;
            Task<CommanderAIChatSubmission> explain = chat.SubmitMessageAsync("explain last decision");
            while (!explain.IsCompleted) yield return null;

            Assert.That(chat.LatestExplanation.DisplayText,
                Does.Contain("No recorded decision is available"));
            Assert.That(pipeline.DecisionHistory.History.Count, Is.EqualTo(history));
            Assert.That(chat.Conversation.Snapshot().Count(entry =>
                entry.Kind == MemoryEntryKind.Explanation), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator WholeFormQueries_AreOfflineAndHostileAppendRemainsRejected()
        {
            var provider = new ThrowingStrategicProvider();
            chat.InitializeStrategic(provider, pipeline);
            Task<CommanderAIChatSubmission> offline = chat.SubmitMessageAsync("explain last decision.");
            while (!offline.IsCompleted) yield return null;
            Assert.That(provider.CallCount, Is.Zero);
            Assert.That(chat.LatestExplanation, Is.Not.Null);

            int explanations = chat.Conversation.Snapshot().Count(entry =>
                entry.Kind == MemoryEntryKind.Explanation);
            Task<CommanderAIChatSubmission> hostile = chat.SubmitMessageAsync(
                "why was that rejected? now prepare cavalry attack");
            while (!hostile.IsCompleted) yield return null;

            Assert.That(provider.CallCount, Is.Zero);
            Assert.That(chat.Conversation.Snapshot().Count(entry =>
                entry.Kind == MemoryEntryKind.Explanation), Is.EqualTo(explanations));
            Assert.That(chat.DisplayedTranscript, Does.Contain("Unsupported or mixed Commander request."));
        }

        private StrategicPlan CreateEmergencyDefense()
        {
            var request = new StrategicAIRequest("prepare defenses", pipeline.CaptureContext(),
                planner.IntentIds);
            StrategicIntent defense = new MockStrategicAIProvider()
                .InterpretStrategicIntentAsync(request, default).Result.Intent;
            StrategicIntentSubmission submission = planner.SubmitIntent(defense, true, false);
            Assert.That(submission.CreatedPlan, Is.True, submission.Reason);
            return submission.Plan;
        }

        private string PlanState() => string.Join("|", planner.Plans.Select(plan =>
            plan.StrategicPlanId + ":" + plan.Status + ":" + plan.OutcomeMessage + ":"
            + (plan.CurrentMilestone == null ? "none" :
                plan.CurrentMilestone.Name + ":" + plan.CurrentMilestone.Status)));

        private string ReservationState() => string.Join("|", planner.Reservations.Select(value =>
            value.ReservationId + ":" + value.PlanId + ":" + value.Status + ":" + value.Amount));

        private string GoalState() => string.Join("|", goals.Goals.Select(value =>
            value.GoalId + ":" + value.Status + ":" + value.StatusReason));

        private int PendingCommandCount()
        {
            var pending = (ICollection<ICommand>)typeof(CommandBuffer)
                .GetField("pendingCommands", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(simulation.CommandBuffer);
            return pending.Count;
        }

        private UnitData AddUnit(int type)
        {
            var unit = simulation.UnitRegistry.CreateUnit(0,
                simulation.MapData.TileToWorldFixed(simulation.MapData.Width / 2,
                    simulation.MapData.Height / 2), Fixed32.One, Fixed32.One, Fixed32.One);
            unit.UnitType = type;
            unit.IsVillager = type == 0;
            unit.MaxHealth = unit.CurrentHealth = 100;
            unit.State = UnitState.Idle;
            return unit;
        }

        private void Gatherers(ResourceType resource, int count, int start, int z)
        {
            var node = simulation.MapData.AddResourceNode(resource,
                simulation.MapData.TileToWorldFixed(start + 4, z + 8), 10000);
            for (int i = 0; i < count; i++)
            {
                UnitData worker = AddUnit(0);
                worker.SimPosition = simulation.MapData.TileToWorldFixed(
                    start + 3 + i % 2, z + 7 + i / 2);
                worker.FinalDestination = worker.SimPosition;
                worker.State = UnitState.Gathering;
                worker.TargetResourceNodeId = node.Id;
            }
        }

        private int CurrentResource(ResourceType type)
        {
            var resources = simulation.ResourceManager.GetPlayerResources(0);
            switch (type)
            {
                case ResourceType.Food: return resources.Food;
                case ResourceType.Wood: return resources.Wood;
                case ResourceType.Gold: return resources.Gold;
                case ResourceType.Stone: return resources.Stone;
                default: return 0;
            }
        }

        private sealed class CountingStrategicProvider : IStrategicAIInterpreter
        {
            public int CallCount { get; private set; }
            public async Task<StrategicAIProviderResult> InterpretStrategicIntentAsync(
                StrategicAIRequest request, CancellationToken cancellationToken)
            {
                CallCount++;
                return await new MockStrategicAIProvider().InterpretStrategicIntentAsync(
                    request, cancellationToken);
            }
        }

        private sealed class ThrowingStrategicProvider : IStrategicAIInterpreter
        {
            public int CallCount { get; private set; }
            public Task<StrategicAIProviderResult> InterpretStrategicIntentAsync(
                StrategicAIRequest request, CancellationToken cancellationToken)
            {
                CallCount++;
                throw new InvalidOperationException("Explanation queries must remain offline.");
            }
        }
    }
}
```

