using TMPro;
using System.Text;
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
        private TMP_Text latestRequestText, providerConnectionText, pendingQuestionText, expandedApprovalText, launcherText;
        private TMP_Text expandedReviewText;
        private GameObject requestRecovery, pendingQuestion, expandedApproval, expandedReview, settingsMenu;
        private Button retryRequestButton, editRequestButton, cancelRequestButton;
        private Button chatTabButton, tasksTabButton;
        private RectTransform bubbleContent;
        private string latestRequestState = "Ready", latestRequestDetail = "";
        private long presentationTurnRevision;
        private long pendingStrategicPresentationTurn;
        private string recoveryDraft;
        private bool recoveryAvailable, tasksVisible, settingsMenuVisible;
        private bool expanded;
        private int inputRevision, voiceDraftRevision;
        private string voiceOwnedDraft;
        private int lastPresentationActionFrame = -1;
        private int lastEscapeActionFrame = -1;

        private TMP_InputField compactTranscriptInput;
        private TMP_Text compactPttHint;
        private Button dockRecord;
        private bool IsVoiceApprovalCandidateDisplayed =>
            !settingsMenuVisible && !voiceSetupVisible && !semanticSetupVisible
            && ((expandedApproval != null && expandedApproval.activeInHierarchy)
                || (compactApproval != null && compactApproval.activeInHierarchy));

        public bool IsExpanded => expanded;

        private string VoiceCaptureLimitLabel
        {
            get
            {
                float limit = VoiceState == CommanderVoiceState.Recording || VoiceState == CommanderVoiceState.Transcribing
                    ? voiceController?.RecordingDurationLimitSeconds ?? CommanderVoiceSettings.MaxDurationSeconds
                    : CommanderVoiceSettings.MaxDurationSeconds;
                // Native/controller limits are float values in [1,60]. Seven
                // decimal places retain their precision without Mono's float
                // formatter exposing extra binary round-trip digits.
                return ((double)limit).ToString("0.#######",System.Globalization.CultureInfo.InvariantCulture) + " s limit";
            }
        }
        private string VoiceCaptureActivityLabel
        {
            get
            {
                if (voiceAudioCapture is ICommanderCaptureReadiness ready && !ready.IsListening)
                    return "Opening microphone — wait before speaking (" + VoiceCaptureLimitLabel + ")";
                string duration = voiceAudioCapture is ICommanderCaptureProgress progress
                    ? progress.RecordedDurationSeconds.ToString("0.#",System.Globalization.CultureInfo.InvariantCulture) + " / " + VoiceCaptureLimitLabel
                    : VoiceCaptureLimitLabel;
                return "● Listening — " + duration + "; release " + ResolvePttKey() + " (or Stop)";
            }
        }

        // Evaluate the real current region before gameplay Input System callbacks;
        // EventSystem's selected/hovered object can still describe the prior frame.
        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            if (!isActiveAndEnabled || canvasRoot == null || !canvasRoot.activeInHierarchy) return false;
            var region = expanded ? commanderPanel : compactCommander?.GetComponent<RectTransform>();
            return region != null && region.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(region, screenPoint, null);
        }

        // Input System gameplay callbacks run before MonoBehaviour.Update. Route
        // Escape here synchronously instead of hoping the UI polls it first.
        public static bool TryHandleEscapeInput()
        {
            if (instance == null || !instance.isActiveAndEnabled) return false;
            if (instance.lastEscapeActionFrame == Time.frameCount) return true;
            bool voiceOperation = instance.VoiceState == CommanderVoiceState.Recording
                || instance.VoiceState == CommanderVoiceState.Transcribing
                || instance.VoiceState == CommanderVoiceState.Preview;
            if (!voiceOperation && !instance.voiceSetupVisible && !instance.semanticSetupVisible
                && !instance.settingsMenuVisible && !instance.expanded) return false;
            instance.lastEscapeActionFrame = Time.frameCount;
            if (voiceOperation) instance.voiceController?.Cancel();
            else if (instance.voiceSetupVisible) instance.HideVoiceSetup();
            else if (instance.semanticSetupVisible) instance.HideSemanticSetup();
            else if (instance.settingsMenuVisible) instance.CloseSettingsMenu();
            else instance.MinimizeCommander();
            return true;
        }

        public void OpenCommander()
        {
            CloseSettingsMenu();
            expanded = true;
            UpdatePresentation();
            inputField?.ActivateInputField();
        }

        public void MinimizeCommander()
        {
            expanded = false;
            inputField?.DeactivateInputField();
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != null
                && events.currentSelectedGameObject.transform.IsChildOf(transform)) events.SetSelectedGameObject(null);
            UnitSelectionManager.SetChatFocused(false);
            UpdatePresentation();
        }

        private bool ClaimPresentationAction()
        {
            if (OwnsDestinationInput) return false;
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
            recoveryAvailable = false;
            recoveryDraft = null;
            tasksVisible = false;
            settingsMenuVisible = false;
            SetLatestRequestState("Ready", string.Empty);
            MinimizeCommander();
        }

        private void SetLatestRequestState(string state, string detail)
        {
            latestRequestState = state ?? "Ready";
            latestRequestDetail = detail ?? string.Empty;
            UpdatePresentation();
        }

        private long BeginPresentationTurn()
        {
            currentRequestGoalId = 0;
            recoveryAvailable = false;
            LastSubmittedSemanticRequestId = null;
            return ++presentationTurnRevision;
        }

        internal long LastSubmittedUiTurnId => presentationTurnRevision;

        private bool IsLatestPresentationTurn(long turn) => turn == presentationTurnRevision;

        private void PublishLatestRequestState(long turn, string state, string detail)
        {
            if (IsLatestPresentationTurn(turn)) SetLatestRequestState(state, detail);
        }

        private void OpenSettingsMenu()
        {
            HideVoiceSetup();
            HideSemanticSetup();
            OpenCommander();
            settingsMenuVisible = true;
            settingsMenu?.transform.SetAsLastSibling();
            UpdatePresentation();
        }

        private void CloseSettingsMenu()
        {
            settingsMenuVisible = false;
            if (settingsMenu != null) settingsMenu.SetActive(false);
        }

        private string ProviderConnectionLabel
        {
            get
            {
                string status = SemanticSetupStatus;
                string route = semanticProvider is OpenRouterCommanderProvider router
                    ? router.UsesGateway ? "Luna gateway" : "OpenRouter Luna"
                    : "Text AI";
                if (semanticSetupMode == SemanticSetupMode.Disabled) return "Text AI: disabled";
                if (status.Contains("setup required")) return route + ": setup required";
                if (status.Contains("timed out")) return route + ": timed out";
                if (status.Contains("connection failed") || status.Contains("service unavailable"))
                    return route + ": connection issue";
                if (status.Contains("authentication failed") || status.Contains("access forbidden"))
                    return route + ": access issue";
                if (status.Contains("validated semantic data")) return route + ": validated response";
                if (semanticProvider is OpenRouterCommanderProvider) return route + ": configured, unverified";
                return "Text AI: local translator";
            }
        }

        private string StrategicApprovalPreview()
        {
            StrategicIntent pending = strategicBridge?.PendingIntent;
            if (pending == null) return string.Empty;
            StrategicPlan template = StrategicPlanRegistry.CreateDefault().CreatePlan(pending);
            StrategicAdaptationProposal adaptation = pendingAdaptationProposal;
            var preview = new StringBuilder();
            preview.AppendLine("Strategic proposal — no new plan started.");
            preview.AppendLine("Source: validated AI recommendation for your Commander.");
            if (adaptation != null)
            {
                preview.Append("Strategy #").Append(adaptation.SourcePlanId).Append(" (")
                    .Append(adaptation.CurrentObjective).AppendLine(") remains active until you decide.");
                preview.AppendLine("Proposed change:");
            }
            else preview.AppendLine("Proposed preparation:");
            int count = adaptation?.ProposedTargets.Count ?? template.Milestones.Count;
            for (int i = 0; i < count; i++)
            {
                string name = i < template.Milestones.Count ? template.Milestones[i].Name : "Next stage";
                preview.Append("Step ").Append(i + 1).Append(" — ").Append(name).Append(": ");
                bool any = false;
                if (adaptation != null)
                {
                    foreach (StrategicAdaptationRequest request in adaptation.ProposedTargets[i].Requests)
                    {
                        if (any) preview.Append("; ");
                        preview.Append(StrategicEffect(request, i, template));
                        any = true;
                    }
                }
                else foreach (StrategicTacticalGoalRequest request in template.Milestones[i].TacticalGoals)
                {
                    if (any) preview.Append("; ");
                    preview.Append(StrategicEffect(request, i, template));
                    any = true;
                }
                if (!any) preview.Append("ready when earlier steps finish");
                preview.AppendLine(".");
            }
            if (adaptation != null)
                preview.AppendLine("Ordinary Approve cannot replace the active strategy. Confirm as command explicitly; existing policy may reject, coexist, or replace only an eligible AI plan. Direct player plans are never replaced.");
            else
                preview.AppendLine("Approve strategy uses ordinary recommendation approval. Confirm as command is a separate stronger player decision. Neither happens by opening Details.");
            return preview.ToString();
        }

        private string StrategicEffect(StrategicAdaptationRequest request, int step, StrategicPlan template)
        {
            switch (request.Kind)
            {
                case StrategicAdaptationRequestKind.WorkerAllocation:
                    return request.WorkerTarget + " " + request.ResourceType.ToString().ToLowerInvariant() + " workers";
                case StrategicAdaptationRequestKind.BuildStructure:
                    return (request.EnsureExisting == true ? "ensure " : "add ") + request.Count + " " + StrategicBuildingName(request.StructureType.Value,request.Count.Value)
                        + (request.EnsureExisting == true ? " exists" : "");
                case StrategicAdaptationRequestKind.EnsureUnitCount:
                    return StrategicUnitEffect(request.UnitType.Value, request.TargetTotal.Value, step, template);
                default: return "validated step";
            }
        }

        private string StrategicEffect(StrategicTacticalGoalRequest request, int step, StrategicPlan template)
        {
            if (request is StrategicResourceAllocationGoalRequest workers)
                return workers.WorkerTarget + " " + workers.ResourceType.ToString().ToLowerInvariant() + " workers";
            if (request is StrategicBuildStructureGoalRequest building)
                return (building.EnsureExisting ? "ensure " : "add ") + building.Count + " " + StrategicBuildingName(building.StructureType,building.Count)
                    + (building.EnsureExisting ? " exists" : "");
            if (request is StrategicEnsureUnitCountGoalRequest units)
                return StrategicUnitEffect(units.UnitType, units.TargetTotal, step, template);
            return "validated step";
        }

        private string StrategicUnitEffect(int unitType, int total, int step, StrategicPlan template)
        {
            string units = unitType == CommanderIntentCatalog.ArcherUnitType ? "archers"
                : unitType == CommanderIntentCatalog.SpearmanUnitType ? "spearmen"
                : CommanderIntentCatalog.GetUnitDisplayName(unitType).ToLowerInvariant() + " units";
            string producer = "its producer";
            if (semanticSimulation != null && semanticSimulation.TryGetProductionBuildingType(
                pendingStrategicOwner, unitType, out BuildingType building)) producer = building.ToString();
            int producerStep = 0;
            for (int i = 0; i < step && i < template.Milestones.Count; i++)
                foreach (StrategicTacticalGoalRequest request in template.Milestones[i].TacticalGoals)
                    if (request is StrategicBuildStructureGoalRequest build && build.StructureType.ToString() == producer)
                        producerStep = i + 1;
            return "reach " + total + " total " + units + " after "
                + (producerStep > 0 ? "step " + producerStep + " secures " : "securing ") + producer
                + " (existing units count; train only the shortfall)";
        }

        private static string StrategicBuildingName(BuildingType type, int count)
            => type == BuildingType.Tower && count > 1 ? "towers" : type.ToString();

        private void CancelActiveSemanticRequest()
        {
            semanticRequestRevision++;
            activeSemanticRequest?.Cancel();
            activeSemanticRequest = null;
            recoveryAvailable = false;
        }

        private void OfferRequestRecovery(long turn, string original, string reason)
        {
            if (!IsLatestPresentationTurn(turn)) return;
            recoveryDraft = original;
            recoveryAvailable = true;
            SetLatestRequestState("Failed", reason);
        }

        private void RetryLatestRequest()
        {
            if (!recoveryAvailable || submitting || recoveryDraft == null || InputText != recoveryDraft) return;
            recoveryAvailable = false; // one physical action consumes this recovery offer
            _ = SubmitCurrentInputAsync();
        }

        private void EditLatestRequest()
        {
            if (!recoveryAvailable) return;
            recoveryAvailable = false;
            inputField?.ActivateInputField();
            SetLatestRequestState("Failed", "Edit the retained draft, then Send when ready.");
        }

        private void CancelLatestRequest()
        {
            if (submitting)
            {
                retainSubmittedDraft = true;
                CancelActiveSemanticRequest();
            }
            recoveryAvailable = false;
            SetLatestRequestState("Cancelled", "No new order will be submitted from that request.");
        }

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
            if (voiceOwnedDraft == null) return;
            if (InputText == voiceOwnedDraft && inputRevision == voiceDraftRevision) InputText = string.Empty;
            voiceOwnedDraft = null;
        }

        private Button CompactButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction action)
        {
            var obj = UIObject(name, parent);
            var image = obj.AddComponent<Image>(); image.color = new Color(.18f,.29f,.42f,1f);
            var button = obj.AddComponent<Button>(); button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => { if (ClaimPresentationAction()) action(); });
            var text = Text("Label", obj.transform, label, 13, TextAlignmentOptions.Center);
            text.richText = false;
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return button;
        }

        private GameObject ReviewCard(string name, string title, out TMP_Text body,
            string[] actions, UnityEngine.Events.UnityAction[] callbacks)
        {
            var card = UIObject(name, compactCommander.transform);
            var image = card.AddComponent<Image>(); image.color = new Color(.055f,.075f,.11f,.98f);
            PanelElement(card, 190);
            var layout = card.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8,8,8,8); layout.spacing = 4;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var heading = Text("Heading", card.transform, title, 14, TextAlignmentOptions.Left);
            heading.richText = false; PanelElement(heading.gameObject,20);
            var scrollObj = UIObject("Scroll", card.transform); PanelElement(scrollObj,110,true);
            var scroll = scrollObj.AddComponent<ScrollRect>(); scroll.horizontal = false;
            var viewport = UIObject("Viewport", scrollObj.transform);
            SetRect(viewport.GetComponent<RectTransform>(), Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            // Bounded local hit target so review scrolling never becomes a full-screen blocker.
            viewport.AddComponent<Image>().color = new Color(0,0,0,.1f);
            viewport.AddComponent<RectMask2D>(); scroll.viewport = viewport.GetComponent<RectTransform>();
            body = Text("Text", viewport.transform, "",13,TextAlignmentOptions.TopLeft);
            body.richText = false; body.textWrappingMode = TextWrappingModes.Normal;
            body.rectTransform.anchorMin = new Vector2(0,1); body.rectTransform.anchorMax = Vector2.one;
            body.rectTransform.pivot = new Vector2(.5f,1); body.rectTransform.sizeDelta = Vector2.zero;
            body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = body.rectTransform;
            var buttons = new GameObject[actions.Length];
            for (int i=0;i<actions.Length;i++) buttons[i] = CompactButton(card.transform,"Action"+i,actions[i],callbacks[i]).gameObject;
            PanelRow("Actions",card.transform,buttons);
            return card;
        }

        private void BuildCompactTranscriptEditor()
        {
            compactReview.GetComponent<LayoutElement>().preferredHeight = 240;
            compactReview.GetComponent<LayoutElement>().minHeight = 240;
            var scroll = compactReview.transform.Find("Scroll").GetComponent<LayoutElement>();
            scroll.minHeight = scroll.preferredHeight = 46;
            var editor = UIObject("Editor",compactReview.transform);
            editor.transform.SetSiblingIndex(1);
            PanelElement(editor,80);
            editor.AddComponent<Image>().color = new Color(.06f,.07f,.09f,1);
            compactTranscriptInput = editor.AddComponent<TMP_InputField>();
            compactTranscriptInput.targetGraphic = editor.GetComponent<Image>();
            compactTranscriptInput.lineType = TMP_InputField.LineType.MultiLineNewline;
            var viewport = UIObject("Viewport",editor.transform);
            SetRect(viewport.GetComponent<RectTransform>(),Vector2.zero,Vector2.one,new Vector2(6,4),new Vector2(-6,-4));
            viewport.AddComponent<RectMask2D>();
            var body = Text("Text",viewport.transform,"",14,TextAlignmentOptions.TopLeft);
            body.richText = false;
            SetRect(body.rectTransform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            compactTranscriptInput.textViewport = viewport.GetComponent<RectTransform>();
            compactTranscriptInput.textComponent = body;
            compactTranscriptInput.onValueChanged.AddListener(value=>InputText=value);
            compactTranscriptInput.onSelect.AddListener(_=>UnitSelectionManager.SetChatFocused(true));
            compactTranscriptInput.onDeselect.AddListener(_=>UnitSelectionManager.SetChatFocused(false));
        }

        private void BuildPresentationUI()
        {
            var transcriptViewport = transcriptScroll.viewport;
            var bubbles = UIObject("BubbleContent", transcriptViewport.transform);
            bubbleContent = bubbles.GetComponent<RectTransform>();
            bubbleContent.anchorMin = new Vector2(0, 1);
            bubbleContent.anchorMax = Vector2.one;
            bubbleContent.pivot = new Vector2(.5f, 1);
            bubbleContent.sizeDelta = Vector2.zero;
            var bubbleLayout = bubbles.AddComponent<VerticalLayoutGroup>();
            bubbleLayout.padding = new RectOffset(8, 8, 8, 8);
            bubbleLayout.spacing = 8;
            bubbleLayout.childControlWidth = bubbleLayout.childControlHeight = true;
            bubbleLayout.childForceExpandWidth = true; bubbleLayout.childForceExpandHeight = false;
            bubbles.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            transcriptScroll.content = bubbleContent;
            transcriptText.gameObject.SetActive(false); // Retain exact transcript for API and legacy evidence.
            RefreshChatBubbles();
            compactCommander = UIObject("CompactCommander",canvasRoot.transform);
            var rect = compactCommander.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            // Sit beside the player-list HUD rather than covering the right-side controls.
            rect.anchoredPosition = new Vector2(-12,-56); rect.sizeDelta = new Vector2(340,0);
            compactCommander.AddComponent<Image>().color = new Color(.055f,.065f,.075f,1f);
            var layout = compactCommander.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8,8,8,8); layout.spacing = 4;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            compactCommander.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var open = CompactButton(compactCommander.transform,"Open","Commander",OpenCommander);
            launcherText = open.GetComponentInChildren<TMP_Text>();
            var settings = CompactButton(compactCommander.transform,"VoiceOptions","Voice",()=>{if(voiceSetupVisible)HideVoiceSetup();else ShowVoiceSetup();});
            var textSettings = CompactButton(compactCommander.transform,"TextAIOptions","Text AI",()=>{if(semanticSetupVisible)HideSemanticSetup();else ShowSemanticSetup();});
            compactRecord = CompactButton(compactCommander.transform,"Record","● Record",OnPttButtonClicked);
            compactRecordLabel = compactRecord.GetComponentInChildren<TMP_Text>();
            compactPttHint = Text("PttHint",compactCommander.transform,"Hold V to talk",12,TextAlignmentOptions.Left);
            PanelElement(compactPttHint.gameObject,20);
            compactCancel = CompactButton(compactCommander.transform,"CancelVoice","Cancel",()=>{if(submitting)CancelActiveSemanticRequest();else OnVoiceCancelClicked();});
            PanelRow("VoiceControls",compactCommander.transform,compactRecord.gameObject,compactCancel.gameObject);
            PanelRow("Launcher",compactCommander.transform,open.gameObject);
            compactCommander.transform.Find("Launcher").SetAsFirstSibling();
            settings.gameObject.SetActive(false);
            textSettings.gameObject.SetActive(false);
            settingsMenu = UIObject("SettingsMenu", commanderPanel);
            settingsMenu.AddComponent<Image>().color = new Color(.09f,.11f,.13f,1f);
            PanelElement(settingsMenu, 274);
            settingsMenu.GetComponent<LayoutElement>().ignoreLayout = true;
            SetRect(settingsMenu.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
                new Vector2(8,8), new Vector2(-8,-46));
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
            PanelElement(CompactButton(settingsMenu.transform,"CaptureEvidence","Capture UI evidence",()=>
            {CloseSettingsMenu();RequestEvidenceScreenshot();}).gameObject,32);
            localObservationToggle = CompactButton(settingsMenu.transform, "LocalMetricsToggle",
                "Record local metrics: Off", ToggleLocalObservationRecording);
            PanelElement(localObservationToggle.gameObject, 32);
            PanelElement(CompactButton(settingsMenu.transform, "ExportLocalMetrics",
                "Export local metrics", ExportLocalObservations).gameObject, 32);
            var observationNote = Text("LocalMetricsNote", settingsMenu.transform,
                "Opt-in, local only. No upload or automatic orders.", 10, TextAlignmentOptions.Left);
            observationNote.richText = false; PanelElement(observationNote.gameObject, 28);
            PanelElement(CompactButton(settingsMenu.transform,"CloseSettings","Close",CloseSettingsMenu).gameObject,32);
            settingsMenu.SetActive(false);
            compactStatus = Text("Status",compactCommander.transform,"Preparing Commander…",12,TextAlignmentOptions.Left);
            var setDestination = CompactButton(compactCommander.transform,"SetDestination","Set destination",BeginDestinationPlacement);
            var clearDestination = CompactButton(compactCommander.transform,"ClearDestination","Clear marker",ClearDestinationMarker);
            PanelRow("Destination",compactCommander.transform,setDestination.gameObject,clearDestination.gameObject);
            var expandedSet = CompactButton(commanderPanel,"SetDestination","Set destination",BeginDestinationPlacement);
            var expandedClear = CompactButton(commanderPanel,"ClearDestination","Clear marker",ClearDestinationMarker);
            PanelRow("Destination",commanderPanel,expandedSet.gameObject,expandedClear.gameObject);
            compactStatus.richText = false; PanelElement(compactStatus.gameObject,28);
            BuildTaskBoardUI();
            BuildVoiceSetupUI();
            BuildSemanticSetupUI();
            DockSetupCard(voiceSetupCard);
            DockSetupCard(semanticSetupCard);
            settingsMenu.transform.SetAsLastSibling();
            compactReview = ReviewCard("TranscriptReview","Review transcript — not submitted",out compactReviewText,
                new[]{"Send","Discard"},new UnityEngine.Events.UnityAction[]{()=>{_ = SubmitCurrentInputAsync();},OnVoiceCancelClicked});
            BuildCompactTranscriptEditor();
            compactApproval = ReviewCard("PlanApproval","Plan approval — not started",out compactApprovalText,
                new[]{"Details","Approve","Reject"},new UnityEngine.Events.UnityAction[]{OpenCommander,
                    ()=>{if(pendingActionPlan!=null)HandleActionPlanControl("approve plan");else OpenCommander();},
                    ()=>{if(pendingActionPlan!=null)HandleActionPlanControl("cancel plan");else DismissStrategicRecommendation();}});
            compactApproveLabel = compactApproval.transform.Find("Actions/Action1").GetComponentInChildren<TMP_Text>();
            var title = commanderPanel.Find("Title")?.GetComponent<TMP_Text>();
            if (title != null) title.text = "COMMANDER";
            commanderPanel.GetComponent<Image>().color = new Color(.065f,.075f,.085f,1f);
            latestRequestText = Text("LatestRequest",commanderPanel,"Ready",14,TextAlignmentOptions.Left);
            latestRequestText.richText = false; PanelElement(latestRequestText.gameObject,36);
            providerConnectionText = Text("ProviderConnection",commanderPanel,"Text AI: local translator",12,TextAlignmentOptions.Left);
            providerConnectionText.richText = false; PanelElement(providerConnectionText.gameObject,18);
            providerConnectionText.transform.SetSiblingIndex(2);
            latestRequestText.transform.SetSiblingIndex(3);
            var chat = CompactButton(commanderPanel,"ChatTab","Chat",()=>{tasksVisible=false;UpdatePresentation();});
            var tasks = CompactButton(commanderPanel,"TasksTab","Tasks",()=>
            {tasksVisible=true;UpdateStrategicHostControls();UpdatePresentation();});
            chatTabButton = chat; tasksTabButton = tasks;
            dockRecord = CompactButton(commanderPanel,"DockRecord","Record",OnPttButtonClicked);
            var views = PanelRow("Views",commanderPanel,chat.gameObject,tasks.gameObject,dockRecord.gameObject);
            views.transform.SetSiblingIndex(4);
            var settingsButton = CompactButton(commanderPanel,"Settings","Settings",OpenSettingsMenu);
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
            PanelElement(pendingQuestion,64);pendingQuestion.transform.SetSiblingIndex(5);
            expandedApproval = UIObject("Approval",commanderPanel);
            expandedApproval.AddComponent<Image>().color = new Color(.14f,.19f,.23f,1f);
            PanelElement(expandedApproval,120,true);
            expandedApproval.GetComponent<LayoutElement>().preferredHeight=168;expandedApproval.transform.SetSiblingIndex(6);
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
            approvalViewport.AddComponent<Image>().color=new Color(0,0,0,.1f);
            approvalViewport.AddComponent<RectMask2D>();
            approvalScrollRect.viewport=approvalViewport.GetComponent<RectTransform>();
            expandedApprovalText=Text("Details",approvalViewport.transform,"",14,TextAlignmentOptions.TopLeft);
            expandedApprovalText.richText=false;
            expandedApprovalText.textWrappingMode=TextWrappingModes.Normal;
            expandedApprovalText.rectTransform.anchorMin=new Vector2(0,1);
            expandedApprovalText.rectTransform.anchorMax=Vector2.one;
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
            var approveStrategy=CompactButton(expandedApproval.transform,"ApproveStrategy","Approve strategy",()=>
            {ApproveStrategicRecommendation();UpdatePresentation();});
            SetRect(approveStrategy.GetComponent<RectTransform>(),new Vector2(0,0),new Vector2(1f/3f,0),
                new Vector2(8,7),new Vector2(-4,37));
            var confirmStrategy=CompactButton(expandedApproval.transform,"ConfirmStrategy","Confirm as command",()=>
            {ConfirmStrategicCommand();UpdatePresentation();});
            SetRect(confirmStrategy.GetComponent<RectTransform>(),new Vector2(1f/3f,0),new Vector2(2f/3f,0),
                new Vector2(4,7),new Vector2(-4,37));
            var dismissStrategy=CompactButton(expandedApproval.transform,"DismissStrategy","Dismiss",()=>
            {DismissStrategicRecommendation();UpdatePresentation();});
            SetRect(dismissStrategy.GetComponent<RectTransform>(),new Vector2(2f/3f,0),new Vector2(1,0),
                new Vector2(4,7),new Vector2(-8,37));
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
            PanelElement(expandedReview,120,true);
            // The dock can be only 350 canvas units above the minimap. Let the
            // review shrink while preserving its scroll viewport and composer.
            expandedReview.GetComponent<LayoutElement>().minHeight=68;
            var reviewScrollObject=UIObject("Scroll",expandedReview.transform);
            SetRect(reviewScrollObject.GetComponent<RectTransform>(),Vector2.zero,Vector2.one,
                new Vector2(8,42),new Vector2(-8,-6));
            var reviewScroll=reviewScrollObject.AddComponent<ScrollRect>();
            reviewScroll.horizontal=false;reviewScroll.vertical=true;
            reviewScroll.movementType=ScrollRect.MovementType.Clamped;
            var reviewViewport=UIObject("Viewport",reviewScrollObject.transform);
            SetRect(reviewViewport.GetComponent<RectTransform>(),Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            reviewViewport.AddComponent<Image>().color=new Color(0,0,0,.1f);
            reviewViewport.AddComponent<RectMask2D>();
            reviewScroll.viewport=reviewViewport.GetComponent<RectTransform>();
            expandedReviewText=Text("ReviewText",reviewViewport.transform,
                "Reviewed transcript — not sent",13,TextAlignmentOptions.TopLeft);
            expandedReviewText.richText=false;
            expandedReviewText.textWrappingMode=TextWrappingModes.Normal;
            expandedReviewText.rectTransform.anchorMin=new Vector2(0,1);
            expandedReviewText.rectTransform.anchorMax=Vector2.one;
            expandedReviewText.rectTransform.pivot=new Vector2(.5f,1);
            expandedReviewText.rectTransform.sizeDelta=Vector2.zero;
            expandedReviewText.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            reviewScroll.content=expandedReviewText.rectTransform;
            var sendReviewed=CompactButton(expandedReview.transform,"SendReviewed","Send reviewed",()=>{_ = SubmitCurrentInputAsync();});
            var editReviewed=CompactButton(expandedReview.transform,"EditReviewed","Edit",()=>inputField?.ActivateInputField());
            var discardReviewed=CompactButton(expandedReview.transform,"DiscardReviewed","Discard",OnVoiceCancelClicked);
            SetRect(sendReviewed.GetComponent<RectTransform>(),new Vector2(0,0),new Vector2(.5f,0),new Vector2(8,5),new Vector2(-4,35));
            SetRect(editReviewed.GetComponent<RectTransform>(),new Vector2(.5f,0),new Vector2(.75f,0),new Vector2(4,5),new Vector2(-4,35));
            SetRect(discardReviewed.GetComponent<RectTransform>(),new Vector2(.75f,0),new Vector2(1,0),new Vector2(4,5),new Vector2(-8,35));
            inputLengthText = Text("RequestLength",commanderPanel,"0 / 1024 characters",11,TextAlignmentOptions.Left);
            inputLengthText.richText = false; PanelElement(inputLengthText.gameObject,18);
            requestRecovery.transform.SetSiblingIndex(commanderPanel.childCount-4);
            expandedReview.transform.SetSiblingIndex(commanderPanel.childCount-3);
            inputLengthText.transform.SetSiblingIndex(commanderPanel.childCount-2);
            var inputRow=commanderPanel.Find("TextSubmission");
            if(inputRow!=null)
            {
                inputRow.SetAsLastSibling();
                var composerLayout=inputRow.GetComponent<HorizontalLayoutGroup>();
                composerLayout.childForceExpandWidth=false;
                composerLayout.childForceExpandHeight=true;
                var inputLayout=inputRow.Find("Input")?.GetComponent<LayoutElement>();
                if(inputLayout!=null)
                {
                    inputLayout.minHeight=inputLayout.preferredHeight=64;
                    inputLayout.flexibleWidth=1;
                }
                var sendLayout=inputRow.Find("Send")?.GetComponent<LayoutElement>();
                if(sendLayout!=null)
                {
                    sendLayout.minHeight=sendLayout.preferredHeight=64;
                    sendLayout.preferredWidth=76;
                    sendLayout.flexibleWidth=0;
                }
            }
            var inputRect=commanderPanel.Find("TextSubmission/Input")?.GetComponent<RectTransform>();
            if(inputRect!=null)
            {
                inputRect.parent.GetComponent<LayoutElement>().minHeight=68;
                inputRect.parent.GetComponent<LayoutElement>().preferredHeight=68;
                inputRect.GetComponent<Image>().color=new Color(.12f,.14f,.16f,1f);
            }
            inputField.lineType=TMP_InputField.LineType.MultiLineNewline;
            inputField.textComponent.fontSize=14;
            transcriptText.fontSize=15;
            transcriptScroll.GetComponent<LayoutElement>().minHeight=32;
            transcriptScroll.GetComponent<LayoutElement>().preferredHeight=100;
            var transcriptImage=transcriptScroll.GetComponent<Image>();
            if(transcriptImage!=null)transcriptImage.color=new Color(.09f,.105f,.12f,1f);
            inputField.onValueChanged.AddListener(OnDraftChanged);
            UpdatePresentation();
        }

        private void RefreshChatBubbles()
        {
            if (bubbleContent == null) return;
            for (int i = bubbleContent.childCount - 1; i >= 0; i--)
            {
                var old = bubbleContent.GetChild(i);
                old.SetParent(null, false);
                if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
            }
            foreach (string entry in transcriptEntries)
            {
                int marker = entry.IndexOf(':');
                string speaker = marker < 0 ? "Commander" : entry.Substring(0, marker);
                string body = marker < 0 ? entry : entry.Substring(marker + 1).Trim();
                bool player = speaker == "Player";
                var bubble = UIObject(player ? "PlayerBubble" : "CommanderBubble", bubbleContent);
                bubble.AddComponent<Image>().color = player
                    ? new Color(.17f, .28f, .33f, 1f) : new Color(.13f, .15f, .17f, 1f);
                var layout = bubble.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(10, 10, 8, 8);
                layout.spacing = 4;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
                var name = Text("Speaker", bubble.transform, player ? "YOU" : "COMMANDER", 12,
                    TextAlignmentOptions.Left);
                name.richText = false; name.color = new Color(.6f, .83f, .87f, 1f);
                PanelElement(name.gameObject, 16);
                var message = Text("Message", bubble.transform, body, 15, TextAlignmentOptions.Left);
                message.richText = false;
                message.textWrappingMode = TextWrappingModes.Normal;
                message.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                bubble.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            Canvas.ForceUpdateCanvases();
            transcriptScroll.verticalNormalizedPosition = 0;
        }

        private void UpdatePresentation()
        {
            RefreshPanelSize();
            if (compactCommander == null) return;
            RefreshTaskBoard();
            RefreshSemanticSetupStatus();
            bool reviewing = VoiceState == CommanderVoiceState.Preview;
            bool strategicChatApproval = !tasksVisible && strategicBridge?.PendingIntent != null;
            if (commanderPanel != null) commanderPanel.gameObject.SetActive(expanded);
            compactCommander.SetActive(!expanded);
            // LatestRequest is the visible lifecycle source; keep the legacy text only
            // as internal state so stale provider/debug status cannot crowd the dock.
            if (commanderStatusText != null) commanderStatusText.gameObject.SetActive(false);
            if (currentPlanStatusText != null)
                currentPlanStatusText.gameObject.SetActive(selectedPlanId != 0 && !strategicChatApproval);
            if (strategicChatApproval && strategicHostRow != null) strategicHostRow.SetActive(false);
            if (voiceRootObject != null)
            {
                bool voiceNeedsStatus = VoiceState == CommanderVoiceState.Recording
                    || VoiceState == CommanderVoiceState.Transcribing
                    || VoiceState == CommanderVoiceState.Error;
                voiceRootObject.GetComponent<LayoutElement>().minHeight = voiceNeedsStatus ? 56 : 36;
                voiceRootObject.GetComponent<LayoutElement>().preferredHeight = voiceNeedsStatus ? 56 : 36;
                if (voiceStatusText != null) voiceStatusText.gameObject.SetActive(voiceNeedsStatus);
                voiceRootObject.SetActive(!reviewing && voiceNeedsStatus);
            }
            if (latestRequestText != null)
            {
                string detail=latestRequestDetail.Length>90?latestRequestDetail.Substring(0,87)+"…":latestRequestDetail;
                latestRequestText.text=latestRequestState+(detail.Length==0?"":" — "+detail);
            }
            if (providerConnectionText != null) providerConnectionText.text = ProviderConnectionLabel;
            if (settingsMenu != null) settingsMenu.SetActive(settingsMenuVisible && !voiceSetupVisible && !semanticSetupVisible && expanded);
            if (launcherText != null) launcherText.text="Commander"+(taskBadgeText!=null&&taskBadgeText.gameObject.activeSelf?" • "+taskBadgeText.text:"")
                +(pendingClarification!=null||pendingActionPlan!=null||recoveryAvailable?" !":"");
            if(chatTabButton!=null)chatTabButton.GetComponent<Image>().color=tasksVisible?new Color(.13f,.15f,.17f,1f):new Color(.19f,.34f,.42f,1f);
            if(tasksTabButton!=null)tasksTabButton.GetComponent<Image>().color=tasksVisible?new Color(.19f,.34f,.42f,1f):new Color(.13f,.15f,.17f,1f);
            bool hasApproval = pendingActionPlan!=null||strategicBridge?.PendingIntent!=null;
            // A pending preview replaces Chat's transcript scroll, rather than
            // nesting an approval scroller alongside the conversation scroller.
            if(transcriptScroll!=null)transcriptScroll.gameObject.SetActive(!tasksVisible&&!hasApproval&&!reviewing);
            if(taskBoardScroll!=null)taskBoardScroll.gameObject.SetActive(tasksVisible&&!reviewing);
            if(pendingQuestion!=null)
            {
                pendingQuestion.SetActive(pendingClarification!=null&&!hasApproval&&!reviewing);
                if(pendingClarification!=null)pendingQuestionText.text="Needs clarification — "+pendingClarification.Question;
            }
            if(expandedApproval!=null)
            {
                expandedApproval.SetActive(hasApproval&&!tasksVisible&&!reviewing);
                if(hasApproval)expandedApprovalText.text=pendingActionPlan?.Preview??StrategicApprovalPreview();
                var approvePlan=expandedApproval.transform.Find("ApprovePlan");
                var rejectPlan=expandedApproval.transform.Find("RejectPlan");
                if(approvePlan!=null)approvePlan.gameObject.SetActive(pendingActionPlan!=null);
                if(rejectPlan!=null)rejectPlan.gameObject.SetActive(pendingActionPlan!=null);
                bool strategicOnly=pendingActionPlan==null&&strategicBridge?.PendingIntent!=null;
                var approveStrategy=expandedApproval.transform.Find("ApproveStrategy");
                var confirmStrategy=expandedApproval.transform.Find("ConfirmStrategy");
                var dismissStrategy=expandedApproval.transform.Find("DismissStrategy");
                if(approveStrategy!=null)
                {
                    approveStrategy.gameObject.SetActive(strategicOnly);
                    approveStrategy.GetComponent<Button>().interactable=strategicOnly&&pendingAdaptationProposal==null&&!submitting;
                    approveStrategy.GetComponent<Image>().color=pendingAdaptationProposal==null
                        ?new Color(.18f,.29f,.42f,1f):new Color(.12f,.14f,.16f,1f);
                    approveStrategy.GetComponentInChildren<TMP_Text>().color=pendingAdaptationProposal==null
                        ?Color.white:new Color(.55f,.59f,.62f,1f);
                }
                if(confirmStrategy!=null)
                {
                    confirmStrategy.gameObject.SetActive(strategicOnly);
                    confirmStrategy.GetComponent<Button>().interactable=strategicOnly&&!submitting;
                }
                if(dismissStrategy!=null)dismissStrategy.gameObject.SetActive(strategicOnly);
            }
            if(requestRecovery!=null)requestRecovery.SetActive(recoveryAvailable||submitting&&semanticProvider!=null);
            if(expandedReview!=null)
            {
                expandedReview.SetActive(reviewing);
                if(reviewing)expandedReviewText.text="Voice review — not sent. Edit the text below, then Send or Discard.\n"+VoiceReviewMessage;
            }
            if(retryRequestButton!=null)retryRequestButton.gameObject.SetActive(recoveryAvailable);
            if(editRequestButton!=null)editRequestButton.gameObject.SetActive(recoveryAvailable);
            if(cancelRequestButton!=null)cancelRequestButton.gameObject.SetActive(recoveryAvailable||submitting&&semanticProvider!=null);
            BoundSetupCard(voiceSetupCard,532);
            BoundSetupCard(semanticSetupCard,336);
            compactReview.SetActive(reviewing);
            if(reviewing && taskBadgeText != null) taskBadgeText.gameObject.SetActive(false);
            if(dockRecord != null)
            {
                dockRecord.interactable=voiceController!=null && CommanderVoiceSettings.VoiceEnabled && !submitting
                    && VoiceState!=CommanderVoiceState.Transcribing && !reviewing;
                dockRecord.GetComponentInChildren<TMP_Text>().text=VoiceState==CommanderVoiceState.Recording?"Stop":"Record";
            }
            bool approving = pendingActionPlan != null || strategicBridge?.PendingIntent != null;
            compactApproval.SetActive(approving && !reviewing);
            compactApproveLabel.text = pendingActionPlan != null ? "Approve plan" : "Review strategy";
            if (reviewing)
            {
                compactReviewText.text = VoiceReviewMessage;
                compactTranscriptInput.SetTextWithoutNotify(InputText);
            }
            compactPttHint.text = "Hold " + ResolvePttKey().ToString().ToUpperInvariant() + " to talk";
            if (approving) compactApprovalText.text = pendingActionPlan?.Preview ?? StrategicApprovalPreview();
            bool busy = VoiceState == CommanderVoiceState.Recording || VoiceState == CommanderVoiceState.Transcribing;
            compactCancel.gameObject.SetActive(true);
            compactCancel.interactable = busy || reviewing || submitting;
            compactRecord.interactable = voiceController != null && !submitting && CommanderVoiceSettings.VoiceEnabled
                && (VoiceState == CommanderVoiceState.Idle || VoiceState == CommanderVoiceState.Error || VoiceState == CommanderVoiceState.Recording);
            compactRecord.gameObject.SetActive(true);
            compactRecordLabel.text = VoiceState == CommanderVoiceState.Recording ? "Stop recording" : "● Record";
            compactStatus.text = VoiceState == CommanderVoiceState.Recording
                ? ((voiceAudioCapture is ICommanderCaptureReadiness ready && !ready.IsListening)
                    ? "Mic opening • " : "Listening • ") + VoiceCaptureLimitLabel.Replace(" limit", "")
                : VoiceState == CommanderVoiceState.Transcribing ? "Transcribing"
                : VoiceState == CommanderVoiceState.Error ? "Voice unavailable"
                : submitting ? "Interpreting"
                : approving && !placingDestination ? "Approval needed"
                : reviewing ? "Review transcript"
                : latestRequestState == "Ready" ? "Ready • " + VoiceCaptureLimitLabel
                : latestRequestState.Length > 30 ? latestRequestState.Substring(0,27) + "…" : latestRequestState;
            ObserveVoiceApprovalCandidate();
        }
        private void DockSetupCard(GameObject card)
        {
            if (card == null) return;
            card.transform.SetParent(commanderPanel, false);
            card.GetComponent<LayoutElement>().ignoreLayout = true;
            SetRect(card.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
                new Vector2(8,8), new Vector2(-8,-46));
        }
        private void MakeSetupScrollable(GameObject card)
        {
            var original=card.GetComponent<VerticalLayoutGroup>();original.enabled=false;
            var close=card.transform.Find("Close");
            if(close!=null){close.GetComponent<LayoutElement>().ignoreLayout=true;
                SetRect(close.GetComponent<RectTransform>(),Vector2.one,Vector2.one,new Vector2(-32,-22),new Vector2(-6,-3));
                close.GetComponentInChildren<TMP_Text>().text="×";}
            var children=new System.Collections.Generic.List<Transform>();
            foreach(Transform child in card.transform)if(child.name!="Close")children.Add(child);
            var viewport=UIObject("SetupViewport",card.transform);
            SetRect(viewport.GetComponent<RectTransform>(),Vector2.zero,Vector2.one,new Vector2(6,6),new Vector2(-6,-22));
            viewport.AddComponent<Image>().color=new Color(0,0,0,.04f);viewport.AddComponent<RectMask2D>();
            var content=UIObject("SetupContent",viewport.transform);var contentRect=content.GetComponent<RectTransform>();
            contentRect.anchorMin=new Vector2(0,1);contentRect.anchorMax=Vector2.one;contentRect.pivot=new Vector2(.5f,1);contentRect.sizeDelta=Vector2.zero;
            var layout=content.AddComponent<VerticalLayoutGroup>();layout.spacing=6;layout.padding=new RectOffset(2,2,2,6);
            layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            content.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            foreach(var child in children)child.SetParent(content.transform,false);
            var scroll=card.AddComponent<ScrollRect>();scroll.viewport=viewport.GetComponent<RectTransform>();scroll.content=contentRect;
            scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=24;
            var hint=Text("SetupScrollHint",card.transform,"Scroll for more settings",10,TextAlignmentOptions.Left);hint.richText=false;
            SetRect(hint.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(8,-20),new Vector2(-36,-3));
        }
        private void BoundSetupCard(GameObject card,float maximumHeight)
        {
            if(card==null||!card.activeInHierarchy)return;
            var rect=card.GetComponent<RectTransform>();var corners=new Vector3[4];rect.GetWorldCorners(corners);
            if(corners[0].y>=12)return;
            Canvas.ForceUpdateCanvases();rect.GetWorldCorners(corners);
            float scale=card.GetComponentInParent<Canvas>().scaleFactor;
            var layout=card.GetComponent<LayoutElement>();
            layout.minHeight=layout.preferredHeight=Mathf.Min(maximumHeight,Mathf.Max(80,(corners[1].y-12)/Mathf.Max(.01f,scale)));
        }
    }
}
