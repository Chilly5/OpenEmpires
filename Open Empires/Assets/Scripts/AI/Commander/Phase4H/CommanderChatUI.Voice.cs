using System;
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

        public CommanderVoiceInputController VoiceController => voiceController;
        public CommanderVoiceState VoiceState => voiceController?.State ?? CommanderVoiceState.Idle;
        public bool IsVoiceRecording => VoiceState == CommanderVoiceState.Recording;
        public bool IsVoiceTranscribing => VoiceState == CommanderVoiceState.Transcribing;

        public void SetVoiceProvider(ICommanderSpeechToTextProvider provider, ICommanderAudioCapture capture = null)
        {
            if (voiceController != null)
            {
                voiceController.StateChanged -= OnVoiceStateChanged;
                voiceController.TranscriptPreviewReady -= OnVoiceTranscriptPreviewReady;
                voiceController.ErrorOccurred -= OnVoiceErrorOccurred;
                voiceController.Dispose();
                voiceController = null;
            }

            voiceProvider = provider;
            voiceAudioCapture = capture ?? new UnityMicrophoneAudioCapture();

            if (voiceProvider != null)
            {
                voiceController = new CommanderVoiceInputController(
                    voiceProvider,
                    voiceAudioCapture,
                    SubmitVoiceTranscriptAsync);
                voiceController.StateChanged += OnVoiceStateChanged;
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
                voiceAudioCapture = new UnityMicrophoneAudioCapture();
            }

            voiceController = new CommanderVoiceInputController(
                voiceProvider,
                voiceAudioCapture,
                SubmitVoiceTranscriptAsync);

            voiceController.StateChanged += OnVoiceStateChanged;
            voiceController.TranscriptPreviewReady += OnVoiceTranscriptPreviewReady;
            voiceController.ErrorOccurred += OnVoiceErrorOccurred;

            UpdateVoiceUI();
        }

        public async Task<CommanderAIChatSubmission> SubmitVoiceTranscriptAsync(string transcript)
        {
            if (string.IsNullOrWhiteSpace(transcript) || submitting) return null;
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
            voiceController?.ResetSession();
            UpdateVoiceUI();
        }

        private void DestroyVoiceControls()
        {
            if (voiceController != null)
            {
                voiceController.StateChanged -= OnVoiceStateChanged;
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
            if (voiceController == null || !CommanderVoiceSettings.VoiceEnabled) return;

            // Poll keyboard input if not focused on typing in the text box
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool isChatInputFocused = inputField != null && inputField.isFocused;

            // Push-To-Talk hotkey ('V' by default, or configured binding)
            Key pttKey = ResolvePttKey();
            var pttKeyControl = keyboard[pttKey];

            if (pttKeyControl != null)
            {
                if (!isChatInputFocused && !submitting && pttKeyControl.wasPressedThisFrame
                    && (voiceController.State == CommanderVoiceState.Idle || voiceController.State == CommanderVoiceState.Error))
                {
                    voiceController.StartRecording();
                }
                else if (pttKeyControl.wasReleasedThisFrame && voiceController.State == CommanderVoiceState.Recording)
                {
                    _ = voiceController.StopRecordingAndTranscribeAsync();
                }
            }

            // Cancel on Escape key
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (voiceController.State == CommanderVoiceState.Recording ||
                    voiceController.State == CommanderVoiceState.Transcribing ||
                    voiceController.State == CommanderVoiceState.Preview)
                {
                    voiceController.Cancel();
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

            voicePttButton.onClick.AddListener(OnPttButtonClicked);

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

            voiceCancelButton.onClick.AddListener(OnVoiceCancelClicked);
            voiceCancelButton.gameObject.SetActive(false);

            UpdateVoiceUI();
        }

        private void OnPttButtonClicked()
        {
            if (voiceController == null || submitting) return;

            if (voiceController.State == CommanderVoiceState.Idle || voiceController.State == CommanderVoiceState.Error)
            {
                voiceController.StartRecording();
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

        private void OnVoiceCancelClicked()
        {
            voiceController?.Cancel();
        }

        private void OnVoiceStateChanged(CommanderVoiceState state)
        {
            UpdateVoiceUI();
        }

        private void OnVoiceTranscriptPreviewReady(string transcript)
        {
            InputText = transcript;
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
                    voiceStatusText.text = submitting ? "Commander is interpreting..." : "Voice: Hold V to talk (or click Record)";
                    voiceStatusText.color = new Color(0.7f, 0.8f, 0.95f);
                    if (voicePttButtonText != null) voicePttButtonText.text = "Record voice";
                    if (voiceCancelButton != null) voiceCancelButton.gameObject.SetActive(false);
                    break;

                case CommanderVoiceState.Recording:
                    voiceStatusText.text = "Recording... release V (or click Stop)";
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
                    voiceStatusText.text = $"Heard: \"{voiceController?.CurrentTranscript}\"";
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
        }
    }
}
