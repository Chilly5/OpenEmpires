using System;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenEmpires
{
    public sealed partial class CommanderChatUI
    {
        private CommanderVoiceInputController voiceController;
        private ICommanderSpeechToTextProvider voiceProvider;
        private ICommanderAudioCapture voiceAudioCapture;

        private GameObject voiceRootObject;
        private TMP_Text voiceStatusText;
        private Button voicePttButton;
        private TMP_Text voicePttButtonText;
        private Button voiceCancelButton;
        private bool pttMouseButtonHeld;
        private CommanderVoiceApprovalTicket voiceApprovalTicket;
        private string voiceLocalMessage = string.Empty;
        public string VoiceReviewMessage => voiceController?.TranscriptReview != null
            ? "Original: " + voiceController.OriginalTranscript + "\n" + voiceController.TranscriptReview.Message
            : voiceLocalMessage;

        public CommanderVoiceInputController VoiceController => voiceController;
        public CommanderVoiceState VoiceState => voiceController?.State ?? CommanderVoiceState.Idle;
        public bool IsVoiceRecording => VoiceState == CommanderVoiceState.Recording;
        public bool IsVoiceTranscribing => VoiceState == CommanderVoiceState.Transcribing;

        public void SetVoiceProvider(ICommanderSpeechToTextProvider provider, ICommanderAudioCapture capture = null)
        {
            CancelPendingVoiceSetup();
            if (voiceController != null)
            {
                voiceController.Cancel(); // Preserve edited input, remove only unchanged owned voice draft.
                voiceController.StateChanged -= OnVoiceStateChanged;
                voiceController.RecordingStarting -= CaptureVoiceApproval;
                voiceController.TranscriptPreviewReady -= OnVoiceTranscriptPreviewReady;
                voiceController.ErrorOccurred -= OnVoiceErrorOccurred;
                voiceController.Dispose();
                voiceController = null;
            }

            voiceProvider = provider;
            voiceAudioCapture = capture ?? CommanderAudioCaptureFactory.Create();

            if (voiceProvider != null)
            {
                voiceController = new CommanderVoiceInputController(
                    voiceProvider,
                    voiceAudioCapture,
                    SubmitVoiceTranscriptAsync);
                ConfigureVoiceReview();
                voiceController.StateChanged += OnVoiceStateChanged;
                voiceController.RecordingStarting += CaptureVoiceApproval;
                voiceController.TranscriptPreviewReady += OnVoiceTranscriptPreviewReady;
                voiceController.ErrorOccurred += OnVoiceErrorOccurred;
            }

            UpdateVoiceUI();
        }

        private void InitializeVoiceControls()
        {
            if (voiceController != null) return;

            if (voiceProvider == null)
            {
                voiceProvider = new WhisperCommanderSpeechToTextProvider(
                    CommanderVoiceSettings.ModelName,
                    CommanderVoiceSettings.Language);
            }

            if (voiceAudioCapture == null)
            {
                voiceAudioCapture = CommanderAudioCaptureFactory.Create();
            }

            voiceController = new CommanderVoiceInputController(
                voiceProvider,
                voiceAudioCapture,
                SubmitVoiceTranscriptAsync);
            ConfigureVoiceReview();

            voiceController.StateChanged += OnVoiceStateChanged;
            voiceController.RecordingStarting += CaptureVoiceApproval;
            voiceController.TranscriptPreviewReady += OnVoiceTranscriptPreviewReady;
            voiceController.ErrorOccurred += OnVoiceErrorOccurred;

            UpdateVoiceUI();
        }

        public async Task<CommanderAIChatSubmission> SubmitVoiceTranscriptAsync(string transcript)
        {
            if (string.IsNullOrWhiteSpace(transcript) || submitting) return null;
            if (!CanSubmitVoiceReview(transcript)) return null;
            if (voiceController?.State == CommanderVoiceState.Preview) voiceController.Cancel();

            // Display in input text field so player visually sees the identical text
            InputText = transcript;

            // Submit directly into the SAME existing Commander text submission entrypoint
            var result = await SubmitMessageAsync(transcript);
            if (!submitting && InputText == transcript) InputText = string.Empty;
            return result;
        }

        private void ResetVoiceControls()
        {
            HideVoiceSetup();
            voiceController?.ResetSession();
            InitializePresentation();
            UpdateVoiceUI();
        }

        private void DestroyVoiceControls()
        {
            gatewaySemanticTransport?.Dispose();gatewaySemanticTransport=null;
            CancelPendingVoiceSetup();
            if (voiceController != null)
            {
                voiceController.StateChanged -= OnVoiceStateChanged;
                voiceController.RecordingStarting -= CaptureVoiceApproval;
                voiceController.TranscriptPreviewReady -= OnVoiceTranscriptPreviewReady;
                voiceController.ErrorOccurred -= OnVoiceErrorOccurred;
                voiceController.Dispose();
                voiceController = null;
            }

            voiceProvider = null;
            voiceAudioCapture = null;
        }

        private void Update()
        {
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame
                && TryHandleEscapeInput()) return;
            (voiceProvider as CommanderGatewaySpeechToTextProvider)?.RefreshSessionBinding();
            UpdateLocalModelProgress();
            if (voiceController == null) return;
            if (!CommanderVoiceSettings.VoiceEnabled){if(VoiceState!=CommanderVoiceState.Idle)voiceController.Cancel();return;}
            if(VoiceState==CommanderVoiceState.Recording&&voiceAudioCapture is ICommanderCaptureReadiness readiness&&readiness.CaptureEnded){
                if(!string.IsNullOrEmpty(readiness.CaptureError))voiceController.ReportCaptureFailure(readiness.CaptureError);
                else _=voiceController.StopRecordingAndTranscribeAsync();return;}
            if (VoiceState == CommanderVoiceState.Recording) UpdateVoiceUI();

            // Poll keyboard input if not focused on typing in the text box
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool isChatInputFocused = inputField != null && inputField.isFocused || CommanderUIInputGuard.IsEditingText;

            // Push-To-Talk hotkey ('V' by default, or configured binding)
            Key pttKey = ResolvePttKey();
            var pttKeyControl = keyboard[pttKey];

            if (pttKeyControl != null)
            {
                if (!isChatInputFocused && !submitting && pttKeyControl.wasPressedThisFrame
                    && (voiceController.State == CommanderVoiceState.Idle || voiceController.State == CommanderVoiceState.Error))
                {
                    StartVoiceRecordingFromUI(true);
                }
                else if (pttKeyControl.wasReleasedThisFrame && voiceController.State == CommanderVoiceState.Recording)
                {
                    _ = voiceController.StopRecordingAndTranscribeAsync();
                }
            }

        }

        private Key ResolvePttKey()
        {
            string binding = CommanderVoiceSettings.PttBindingPath;
            if (string.IsNullOrEmpty(binding)) return Key.V;

            int slash = binding.LastIndexOf('/');
            string keyName = slash >= 0 ? binding.Substring(slash + 1) : binding;
            if (Enum.TryParse(keyName, true, out Key parsedKey))
                return parsedKey;

            return Key.V;
        }

        private void BuildVoiceUI(Transform parent)
        {
            voiceRootObject = UIObject("VoiceBar", parent);
            RectTransform rootRect = voiceRootObject.GetComponent<RectTransform>();
            SetRect(rootRect, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(10, 82), new Vector2(-10, 110));

            // Status indicator label
            voiceStatusText = Text("VoiceStatus", voiceRootObject.transform,
                "Voice: Hold V to talk", 12, TextAlignmentOptions.Left);
            SetRect(voiceStatusText.rectTransform, new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(4, 34), new Vector2(-4, 0));
            voiceStatusText.richText = false;
            voiceStatusText.overflowMode = TextOverflowModes.Ellipsis;
            voiceStatusText.color = new Color(0.7f, 0.8f, 0.95f);

            // Push-to-talk button (for mouse users)
            GameObject pttButtonObj = UIObject("PTTButton", voiceRootObject.transform);
            RectTransform pttRect = pttButtonObj.GetComponent<RectTransform>();
            SetRect(pttRect, new Vector2(0, 0), new Vector2(0.6f, 0),
                new Vector2(4, 2), new Vector2(-4, 30));
            var pttImg = pttButtonObj.AddComponent<Image>();
            pttImg.color = new Color(0.18f, 0.35f, 0.45f, 1f);
            voicePttButton = pttButtonObj.AddComponent<Button>();
            voicePttButton.targetGraphic = pttImg;
            voicePttButtonText = Text("Label", pttButtonObj.transform, "Record voice", 12, TextAlignmentOptions.Center);
            SetRect(voicePttButtonText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            voicePttButton.onClick.AddListener(() => { if (ClaimPresentationAction()) OnPttButtonClicked(); });

            // Cancel button
            GameObject cancelObj = UIObject("VoiceCancel", voiceRootObject.transform);
            RectTransform cancelRect = cancelObj.GetComponent<RectTransform>();
            SetRect(cancelRect, new Vector2(.6f, 0), new Vector2(1, 0),
                new Vector2(4, 2), new Vector2(-4, 30));
            var cancelImg = cancelObj.AddComponent<Image>();
            cancelImg.color = new Color(0.45f, 0.2f, 0.2f, 1f);
            voiceCancelButton = cancelObj.AddComponent<Button>();
            voiceCancelButton.targetGraphic = cancelImg;
            var cancelText = Text("Label", cancelObj.transform, "Cancel", 11, TextAlignmentOptions.Center);
            SetRect(cancelText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            voiceCancelButton.onClick.AddListener(() => { if (ClaimPresentationAction()) OnVoiceCancelClicked(); });
            voiceCancelButton.gameObject.SetActive(false);

            UpdateVoiceUI();
        }

        private void OnPttButtonClicked()
        {
            if (voiceController == null || submitting) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            if(voiceProvider is WhisperCommanderSpeechToTextProvider){ShowVoiceSetup();return;}
#endif

            if (voiceController.State == CommanderVoiceState.Idle || voiceController.State == CommanderVoiceState.Error)
            {
                StartVoiceRecordingFromUI();
            }
            else if (voiceController.State == CommanderVoiceState.Recording)
            {
                _ = voiceController.StopRecordingAndTranscribeAsync();
            }
            else if (voiceController.State == CommanderVoiceState.Preview)
            {
                _ = SubmitCurrentInputAsync(); // Uses the editable preview and consumes it once.
            }
        }

        private void StartVoiceRecordingFromUI(bool keyboardHeld=false)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if(voiceProvider is WhisperCommanderSpeechToTextProvider){ShowVoiceSetup();return;}
#endif
            if(voiceProvider is CommanderGatewaySpeechToTextProvider online&&!online.HasConsent){ShowVoiceSetup();return;}
            if(voiceAudioCapture is ICommanderCaptureReadiness readiness){
                if(!readiness.PermissionReady){ShowVoiceSetup();VoiceSetupMessage("Enable microphone explicitly, then use a fresh Record/hold-key action. Microphone permission is not online consent.");return;}
                string key=ResolvePttKey().ToString();readiness.SetHeldKey(keyboardHeld?(key.Length==1?"Key"+key.ToUpperInvariant():key):"");}
            voiceController?.StartRecording();
        }

        private string CurrentVoiceModeLabel
        {
            get{
                if(!CommanderVoiceSettings.VoiceEnabled)return "Text only";
                if(voiceProvider is CommanderGatewaySpeechToTextProvider online)return online.HasConsent?"Online — audio sent":"Online consent required";
#if UNITY_WEBGL && !UNITY_EDITOR
                return "Voice setup required";
#else
                return "On device";
#endif
            }
        }

        private void OnVoiceCancelClicked()
        {
            voiceController?.Cancel();
        }

        private void OnVoiceStateChanged(CommanderVoiceState state)
        {
            if (state == CommanderVoiceState.Idle || state == CommanderVoiceState.Error)
                voiceApprovalTicket = null;
            if (state != CommanderVoiceState.Preview) ReleaseVoiceDraft();
            UpdateVoiceUI();
        }

        private void CaptureVoiceApproval()
        {
            voiceLocalMessage = string.Empty;
            object candidate = SingleVoiceApprovalCandidate(out string preview, out bool displayed);
            voiceApprovalTicket = new CommanderVoiceApprovalTicket(candidate, preview, runtimeGeneration, displayed);
        }

        private void ObserveVoiceApprovalCandidate()
        {
            if (voiceApprovalTicket == null) return;
            object candidate = SingleVoiceApprovalCandidate(out string preview, out bool displayed);
            voiceApprovalTicket.Observe(candidate, preview, runtimeGeneration, displayed);
        }

        private void OnVoiceTranscriptPreviewReady(string transcript)
        {
            voiceOwnedDraft = transcript;
            InputText = transcript;
            voiceDraftRevision = inputRevision;
            UpdateVoiceUI();
        }

        private void OnVoiceErrorOccurred(string error)
        {
            UpdateVoiceUI();
        }

        private void UpdateVoiceUI()
        {
            if (voiceStatusText == null) return;

            CommanderVoiceState state = VoiceState;
            switch (state)
            {
                case CommanderVoiceState.Idle:
                    voiceStatusText.text = !string.IsNullOrEmpty(voiceLocalMessage) ? voiceLocalMessage : submitting ? "Commander is interpreting..." : CurrentVoiceModeLabel+". Hold " + ResolvePttKey() + " to talk (or click Record; " + VoiceCaptureLimitLabel + ")";
                    voiceStatusText.color = new Color(0.7f, 0.8f, 0.95f);
                    if (voicePttButtonText != null) voicePttButtonText.text = "Record voice";
                    if (voiceCancelButton != null) voiceCancelButton.gameObject.SetActive(false);
                    break;

                case CommanderVoiceState.Recording:
                    voiceStatusText.text = VoiceCaptureActivityLabel;
                    voiceStatusText.color = new Color(1f, 0.35f, 0.35f);
                    if (voicePttButtonText != null) voicePttButtonText.text = "Stop recording";
                    if (voiceCancelButton != null) voiceCancelButton.gameObject.SetActive(true);
                    break;

                case CommanderVoiceState.Transcribing:
                    voiceStatusText.text = "Transcribing audio...";
                    voiceStatusText.color = new Color(1f, 0.85f, 0.4f);
                    if (voicePttButtonText != null) voicePttButtonText.text = "Transcribing...";
                    if (voiceCancelButton != null) voiceCancelButton.gameObject.SetActive(true);
                    break;

                case CommanderVoiceState.Preview:
                    voiceStatusText.text = VoiceReviewMessage;
                    voiceStatusText.color = new Color(0.4f, 1f, 0.5f);
                    if (voicePttButtonText != null) voicePttButtonText.text = "Send edited text";
                    if (voiceCancelButton != null) voiceCancelButton.gameObject.SetActive(true);
                    break;

                case CommanderVoiceState.Error:
                    string err = voiceController?.LastError;
                    voiceStatusText.text = string.IsNullOrWhiteSpace(err) ? "Voice error." : err;
                    voiceStatusText.color = new Color(1f, 0.5f, 0.4f);
                    if (voicePttButtonText != null) voicePttButtonText.text = "Retry recording";
                    if (voiceCancelButton != null) voiceCancelButton.gameObject.SetActive(false);
                    break;
            }
            if (voicePttButton != null) voicePttButton.interactable = !submitting
                && state != CommanderVoiceState.Transcribing && CommanderVoiceSettings.VoiceEnabled;
            UpdatePresentation();
        }

        private void ConfigureVoiceReview()
        {
            // Every ordinary utterance remains editable, regardless of a stale autosubmit setting.
            voiceController.AutoSubmit = false;
            voiceController.RawTranscriptHandler = HandleRawVoiceApproval;
            voiceController.TranscriptProcessor = ReviewVoiceTranscript;
        }

        private static CommanderVoiceTranscriptReview ReviewVoiceTranscript(string raw)
        {
            var catalog = GameKnowledgeCatalog.BuildCanonicalIdentityCatalog();
            var names = catalog.Units.Select(x => new CommanderVoiceName(x.StableId, x.DisplayName, x.Aliases.ToArray()))
                .Concat(catalog.Buildings.Select(x => new CommanderVoiceName(x.StableId, x.DisplayName, x.Aliases.ToArray())));
            return CommanderVoiceTranscriptCorrection.Review(raw, names);
        }

        private object SingleVoiceApprovalCandidate(out string preview, out bool displayed)
        {
            preview = string.Empty;
            displayed = false;
            // Hidden alternatives, simultaneous pending requests, marker input and adaptation
            // replacement all require explicit review; voice never chooses among them.
            if (submitting || OwnsDestinationInput || pendingClarification != null || pendingAdaptationProposal != null
                || pendingActionPlan != null && strategicBridge?.PendingIntent != null) return null;
            object candidate = pendingActionPlan ?? (object)strategicBridge?.PendingIntent;
            if (candidate == null || pendingActionPlan?.Cancelled == true) return null;
            preview = pendingActionPlan?.Preview ?? StrategicApprovalPreview();
            displayed = IsVoiceApprovalCandidateDisplayed;
            return candidate;
        }

        private bool HandleRawVoiceApproval(string raw)
        {
            // Deliberately inspect UNCORRECTED output. Quotes, punctuation, negation and
            // longer utterances cannot accidentally enter the local approval route.
            if (raw == null || raw.IndexOf("approv", StringComparison.OrdinalIgnoreCase) < 0) return false;
            object candidate = SingleVoiceApprovalCandidate(out string preview, out bool displayed);
            bool approved = voiceApprovalTicket?.TryConsume(raw, candidate, preview, runtimeGeneration, displayed) == true;
            voiceApprovalTicket = null;
            if (!approved)
            {
                voiceLocalMessage = "Approval needs review. Display one pending candidate, then record only 'approve', or use its approval button.";
                AppendLine("Commander", voiceLocalMessage);
                SetLatestRequestState("Needs clarification", voiceLocalMessage);
                return true;
            }
            if (candidate is CommanderActionPlanCandidate) HandleActionPlanControl("approve plan");
            else ApproveStrategicRecommendation();
            voiceLocalMessage = "Voice approval sent through the existing approval checks.";
            UpdatePresentation();
            return true;
        }

        private bool CanSubmitVoiceReview(string editedText)
        {
            var review = voiceController?.TranscriptReview;
            if (voiceController?.State != CommanderVoiceState.Preview || review?.HasAmbiguity != true) return true;
            var editedReview = ReviewVoiceTranscript(editedText);
            if (!editedReview.HasAmbiguity) return true;
            SetCommanderStatus(editedReview.Message, true);
            return false;
        }
    }
}
