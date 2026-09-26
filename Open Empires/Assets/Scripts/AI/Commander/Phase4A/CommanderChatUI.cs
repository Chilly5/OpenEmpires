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
        public StrategicAdaptationProposal PendingAdaptationProposal => pendingAdaptationProposal;
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
            submitting = false;
            adapter = new CommanderAIIntentAdapter(provider, simulation, goalManager, dispatcher,
                null, providerTimeout);
            advisorySimulation = simulation;
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
                    : provider is OpenRouterCommanderProvider
                        ? "OpenRouter Luna translator ready."
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
            if (!pipeline.UsesSimulation(advisorySimulation))
                throw new System.ArgumentException(
                    "The strategic runtime must use the active Commander simulation.",
                    nameof(pipeline));
            if (strategicPipeline != null && !ReferenceEquals(strategicPipeline, pipeline))
                ResetConversation();
            runtimeGeneration++;
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
                () => Conversation?.Snapshot() ?? System.Array.Empty<MemoryEntry>());
            strategicPipeline.EvaluationCompleted += OnStrategicEvaluationCompleted;
            strategicPipeline.StrategicPlanner.PlanStatusChanged += OnHostPlanStatusChanged;
            strategicPipeline.StrategicPlanner.MilestoneStatusChanged += OnHostMilestoneStatusChanged;
            strategicPipeline.StrategicPlanner.ChildGoalEventObserved += OnHostChildGoalEvent;
            strategicPipeline.StrategicPlanner.ReservationCreated += OnHostReservationCreated;
            strategicPipeline.StrategicPlanner.ReservationReleased += OnHostReservationReleased;
            submitting = false;
            if (inputField != null) inputField.interactable = adapter != null;
            if (sendButton != null) sendButton.interactable = adapter != null;
            UpdateStrategicControls();
            ScanOwnedAdvisories(); // seed current owned plans without announcing a fabricated transition
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
            // Any new player turn dismisses an adaptation, while no-plan recommendations
            // retain the established preference/explanation query behavior.
            if (pendingAdaptationProposal != null)
            {
                strategicBridge?.ClearPending();
                ClearStrategicPreview();
            }
            if (TryHandleStrategicLifecycle(trimmed)) return null;
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
            ClearStrategicPreview();
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
                    StrategicPipeline sourcePipeline = strategicPipeline;
                    int owner = Conversation.PlayerId;
                    if (!TryCaptureOwnedStrategicSource(sourcePipeline, owner,
                        out int sourceCount, out StrategicAdaptationSource source))
                    {
                        AppendLine("Commander", "Strategic source evidence is unavailable; ask again after the current strategy settles.");
                        return null;
                    }
                    if (sourceCount > 1)
                    {
                        AppendLine("Commander", "Multiple active strategies make this request ambiguous; select a single strategy before asking again.");
                        return null;
                    }
                    int sourceLastPlanId = LatestRetainedPlanId(sourcePipeline.StrategicPlanner);
                    var result = await strategicBridge.TranslateWithMemoryAsync(trimmed,
                        memoryAtTurnStart, lifetime.Token);
                    if (this == null || generation != runtimeGeneration || lifetime.IsCancellationRequested) return null;
                    if (!ReferenceEquals(strategicPipeline, sourcePipeline)
                        || Conversation == null || Conversation.PlayerId != owner)
                    {
                        RejectStaleStrategicPreview();
                        return null;
                    }
                    if (result.Success)
                    {
                        StrategicIntent pending = strategicBridge.PendingIntent;
                        if (pending == null || !ReferenceEquals(pending, result.Intent)
                            || pending.PlayerId != owner || pending.IntentId < 1
                            || !TryCaptureOwnedStrategicSource(sourcePipeline, owner,
                                out int freshCount, out StrategicAdaptationSource fresh)
                            || freshCount != sourceCount
                            || LatestRetainedPlanId(sourcePipeline.StrategicPlanner) != sourceLastPlanId)
                        {
                            RejectStaleStrategicPreview();
                            return null;
                        }
                        if (sourceCount == 1)
                        {
                            StrategicAdaptationProposal proposal = StrategicAdaptationProposalBuilder.Build(
                                source, pending, null, "Player-requested strategic change.");
                            if (proposal == null || !StrategicAdaptationProposalBuilder.IsFresh(proposal, fresh))
                            {
                                RejectStaleStrategicPreview();
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
                    }
                    LatestStrategicInterpretation = result;
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

        private static string StrategicPreview(StrategicObjectiveType objective)
        {
            switch (objective)
            {
                case StrategicObjectiveType.RangedReinforcement:
                    return "Ranged reinforcement recommendation: finite preparation of an ArcheryRange and 10 archers; no automatic attack or holding behavior.";
                case StrategicObjectiveType.DefensiveTurtle:
                    return "Fortified defense recommendation: finite preparation of a Barracks, ArcheryRange, 2 mandatory towers, 8 spearmen, and 8 archers; no walls, keeps, garrisons, perimeter placement, automatic holding, or attack micro.";
                default:
                    return objective + " recommendation.";
            }
        }

        private bool TryCaptureOwnedStrategicSource(StrategicPipeline pipeline, int owner,
            out int count, out StrategicAdaptationSource source)
        {
            count = 0;
            source = null;
            if (pipeline == null || !ReferenceEquals(strategicPipeline, pipeline)
                || Conversation == null || Conversation.PlayerId != owner
                || pipeline.StrategicPlanner.PlayerId != owner) return false;
            int generation = runtimeGeneration;
            StrategicPlanner planner = pipeline.StrategicPlanner;
            StrategicPlan sole = null;
            foreach (StrategicPlan candidate in planner.ActivePlans)
            {
                if (candidate.IsTerminal || candidate.OwnerPlayerId != owner) continue;
                count++;
                if (count == 1) sole = candidate;
                if (count > 1) return true;
            }
            if (count == 0) return true;
            StrategicPlanHealthSnapshot health = planner.CapturePlanHealth(owner, sole.StrategicPlanId);
            source = StrategicAdaptationProposalBuilder.Capture(owner, sole, health);
            return source != null && generation == runtimeGeneration
                && ReferenceEquals(strategicPipeline, pipeline)
                && Conversation != null && Conversation.PlayerId == owner
                && ReferenceEquals(planner.GetPlan(sole.StrategicPlanId), sole);
        }

        private static int LatestRetainedPlanId(StrategicPlanner planner)
        {
            int latest = 0;
            foreach (StrategicPlan plan in planner.Plans)
                if (plan.StrategicPlanId > latest) latest = plan.StrategicPlanId;
            return latest;
        }

        private void ClearStrategicPreview()
        {
            pendingAdaptationProposal = null;
            pendingStrategicPipeline = null;
            pendingStrategicGeneration = 0;
            pendingStrategicOwner = -1;
            pendingStrategicIntentId = 0;
            pendingStrategicLastPlanId = 0;
            pendingStrategicHadNoPlan = false;
        }

        private void RejectStaleStrategicPreview()
        {
            strategicBridge?.ClearPending();
            ClearStrategicPreview();
            LatestStrategicInterpretation = null;
            AppendLine("Commander", "Strategic recommendation is stale; no plan changed. Ask again for a fresh proposal.");
            UpdateStrategicControls();
        }

        private StrategicDecisionRecord SubmitPendingStrategy(bool confirmed)
        {
            if (submitting || strategicPipeline == null || strategicBridge?.PendingIntent == null
                || lifetime.IsCancellationRequested) return null;
            StrategicIntent pending = strategicBridge.PendingIntent;
            int id = pending.IntentId;
            if (!ReferenceEquals(strategicPipeline, pendingStrategicPipeline)
                || runtimeGeneration != pendingStrategicGeneration
                || Conversation == null || Conversation.PlayerId != pendingStrategicOwner
                || id != pendingStrategicIntentId
                || pending.PlayerId != pendingStrategicOwner
                || LatestRetainedPlanId(strategicPipeline.StrategicPlanner) != pendingStrategicLastPlanId
                || pending.Source != StrategicIntentSource.AIRecommendation
                || pending.Status != StrategicIntentStatus.Created
                || !TryCaptureOwnedStrategicSource(strategicPipeline, pendingStrategicOwner,
                    out int currentCount, out StrategicAdaptationSource currentSource)
                || pendingAdaptationProposal == null && (!pendingStrategicHadNoPlan || currentCount != 0)
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
            UpdateStrategicControls();
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
            runtimeGeneration++;
            ClearStrategicPreview();
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = new CancellationTokenSource();
            Conversation.Reset();
            adapter?.ResetHistory();
            transcript.Clear();
            transcriptEntries.Clear();
            if (transcriptText != null) transcriptText.text = string.Empty;
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
            }
            if (sendButton != null) sendButton.interactable = adapter != null;
            UpdateStrategicControls();
            ScanOwnedAdvisories(); // a real event on the next tick must not become a silent first sample
        }

        private void UpdateStrategicControls()
        {
            bool available = !submitting && strategicBridge?.PendingIntent != null;
            if (approveStrategyButton != null)
                approveStrategyButton.interactable = available && pendingAdaptationProposal == null;
            if (confirmStrategyButton != null) confirmStrategyButton.interactable = available;
            if (dismissStrategyButton != null) dismissStrategyButton.interactable = available;
            UpdateStrategicHostControls();
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
            {
                strategicPipeline.EvaluationCompleted -= OnStrategicEvaluationCompleted;
                strategicPipeline.StrategicPlanner.PlanStatusChanged -= OnHostPlanStatusChanged;
                strategicPipeline.StrategicPlanner.MilestoneStatusChanged -= OnHostMilestoneStatusChanged;
                strategicPipeline.StrategicPlanner.ChildGoalEventObserved -= OnHostChildGoalEvent;
                strategicPipeline.StrategicPlanner.ReservationCreated -= OnHostReservationCreated;
                strategicPipeline.StrategicPlanner.ReservationReleased -= OnHostReservationReleased;
            }
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
            ClearStrategicPreview();
            if (bootstrapWait != null) StopCoroutine(bootstrapWait);
            lifetime?.Cancel();
            strategicBridge?.Dispose();
            DetachStrategicPipeline();
            ResetAdvisories();
            advisorySimulation = null;
            ResetStrategicHostControls();
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
                new Vector2(10, 146), new Vector2(-10, -43));
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
            BuildStrategicHostControls(panel.transform);
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
