using TMPro;
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
            if (!voiceOperation && !instance.voiceSetupVisible && !instance.semanticSetupVisible && !instance.expanded) return false;
            instance.lastEscapeActionFrame = Time.frameCount;
            if (voiceOperation) instance.voiceController?.Cancel();
            else if (instance.voiceSetupVisible) instance.HideVoiceSetup();
            else if (instance.semanticSetupVisible) instance.HideSemanticSetup();
            else instance.MinimizeCommander();
            return true;
        }

        public void OpenCommander()
        {
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
            MinimizeCommander();
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
            layout.padding = new RectOffset(8,8,8,8); layout.spacing = 6;
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

        private void BuildPresentationUI()
        {
            compactCommander = UIObject("CompactCommander",canvasRoot.transform);
            var rect = compactCommander.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            // Leave the existing right-edge debug/HUD counters accessible.
            rect.anchoredPosition = new Vector2(-110,-72); rect.sizeDelta = new Vector2(310,0);
            compactCommander.AddComponent<Image>().color = new Color(.035f,.045f,.06f,.94f);
            var layout = compactCommander.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8,8,8,8); layout.spacing = 6;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            compactCommander.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var open = CompactButton(compactCommander.transform,"Open","Commander",OpenCommander);
            var settings = CompactButton(compactCommander.transform,"VoiceOptions","Voice",()=>{if(voiceSetupVisible)HideVoiceSetup();else ShowVoiceSetup();});
            var textSettings = CompactButton(compactCommander.transform,"TextAIOptions","Text AI",()=>{if(semanticSetupVisible)HideSemanticSetup();else ShowSemanticSetup();});
            compactRecord = CompactButton(compactCommander.transform,"Record","● Record",OnPttButtonClicked);
            compactRecordLabel = compactRecord.GetComponentInChildren<TMP_Text>();
            compactCancel = CompactButton(compactCommander.transform,"CancelVoice","Cancel",OnVoiceCancelClicked);
            PanelRow("Launcher",compactCommander.transform,open.gameObject,compactRecord.gameObject,compactCancel.gameObject,settings.gameObject);
            compactCommander.transform.Find("Launcher").SetAsFirstSibling();
            PanelElement(textSettings.gameObject,28);
            compactStatus = Text("Status",compactCommander.transform,"Preparing Commander…",12,TextAlignmentOptions.Left);
            compactStatus.richText = false; PanelElement(compactStatus.gameObject,38);
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
            var minimize = CompactButton(commanderPanel,"Minimize","Minimize Commander",MinimizeCommander);
            PanelElement(minimize.gameObject,32);
            minimize.GetComponent<LayoutElement>().ignoreLayout = true;
            SetRect(minimize.GetComponent<RectTransform>(),Vector2.one,Vector2.one,new Vector2(-150,-38),new Vector2(-12,-10));
            inputLengthText = Text("RequestLength",commanderPanel,"0 / 1024 characters",11,TextAlignmentOptions.Left);
            inputLengthText.richText = false; PanelElement(inputLengthText.gameObject,18);
            inputField.onValueChanged.AddListener(OnDraftChanged);
            UpdatePresentation();
        }

        private void UpdatePresentation()
        {
            if (compactCommander == null) return;
            RefreshTaskBoard();
            RefreshSemanticSetupStatus();
            if (commanderPanel != null) commanderPanel.gameObject.SetActive(expanded);
            compactCommander.SetActive(!expanded);
            BoundSetupCard(voiceSetupCard,532);
            BoundSetupCard(semanticSetupCard,336);
            bool reviewing = VoiceState == CommanderVoiceState.Preview;
            compactReview.SetActive(reviewing);
            bool approving = pendingActionPlan != null || strategicBridge?.PendingIntent != null;
            compactApproval.SetActive(approving);
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
                : submitting ? "Interpreting your request…"
                : approving ? "Approval required — no new plan started"
                : reviewing ? "Transcript ready — review before Send"
                : (commanderStatusText?.text ?? "Commander ready") + "\n"+CurrentVoiceModeLabel+" — hold " + ResolvePttKey()+" ("+VoiceCaptureLimitLabel+")";
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
