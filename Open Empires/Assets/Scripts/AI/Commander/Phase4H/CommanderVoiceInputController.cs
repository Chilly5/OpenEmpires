using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace OpenEmpires
{
    /// <summary>
    /// Coordinates voice capture, local speech-to-text transcription, and submission
    /// into the existing Commander text input pipeline.
    /// Strictly adheres to authority boundaries: outputs ONLY a plain string into the existing text path.
    /// Implements generation identity for fail-closed match reset and cancellation safety.
    /// </summary>
    public sealed class CommanderVoiceInputController : IDisposable
    {
        public const float DefaultTranscriptionTimeoutSeconds = 25f;

        private readonly ICommanderAudioCapture audioCapture;
        private readonly ICommanderSpeechToTextProvider sttProvider;
        private readonly Func<string, Task<CommanderAIChatSubmission>> submitAction;

        private int currentSessionId;
        private CancellationTokenSource activeCts;
        private bool disposed;

        public CommanderVoiceState State { get; private set; } = CommanderVoiceState.Idle;
        public string CurrentTranscript { get; private set; } = string.Empty;
        public string LastError { get; private set; } = string.Empty;
        public bool AutoSubmit { get; set; } = false;

        public event Action<CommanderVoiceState> StateChanged;
        public event Action<string> TranscriptPreviewReady;
        public event Action<string> ErrorOccurred;

        public CommanderVoiceInputController(
            ICommanderSpeechToTextProvider sttProvider,
            ICommanderAudioCapture audioCapture = null,
            Func<string, Task<CommanderAIChatSubmission>> submitAction = null)
        {
            this.sttProvider = sttProvider ?? throw new ArgumentNullException(nameof(sttProvider));
            this.audioCapture = audioCapture ?? new UnityMicrophoneAudioCapture();
            this.submitAction = submitAction;
            this.AutoSubmit = CommanderVoiceSettings.AutoSubmit;
        }

        public bool StartRecording(string deviceName = null, float maxDuration = -1f)
        {
            if (disposed) return false;

            // Only allow starting from Idle or Error. Prevent overlapping recordings.
            if (State != CommanderVoiceState.Idle && State != CommanderVoiceState.Error)
            {
                Debug.LogWarning($"[CommanderVoice] Cannot start recording while in state {State}.");
                return false;
            }

            if (!CommanderVoiceSettings.VoiceEnabled)
            {
                SetError("VOICE_DISABLED", "Voice input is currently disabled in settings.");
                return false;
            }

            float duration = maxDuration > 0f ? maxDuration : CommanderVoiceSettings.MaxDurationSeconds;
            string dev = !string.IsNullOrEmpty(deviceName) ? deviceName : CommanderVoiceSettings.MicrophoneDevice;

            bool started = audioCapture.StartRecording(dev, duration);
            if (!started)
            {
                SetError("MIC_START_FAILED", "Could not start recording. Check microphone connection.");
                return false;
            }

            CurrentTranscript = string.Empty;
            LastError = string.Empty;
            TransitionTo(CommanderVoiceState.Recording);
            return true;
        }

        public async Task<CommanderSpeechToTextResult> StopRecordingAndTranscribeAsync()
        {
            if (disposed) return CommanderSpeechToTextResult.Cancelled();

            if (State != CommanderVoiceState.Recording)
            {
                Debug.LogWarning($"[CommanderVoice] Cannot stop recording while in state {State}.");
                return CommanderSpeechToTextResult.Empty();
            }

            CommanderAudioData audio = audioCapture.StopRecording();
            if (audio == null || audio.Samples == null || audio.Samples.Length == 0)
            {
                TransitionTo(CommanderVoiceState.Idle);
                return CommanderSpeechToTextResult.Empty();
            }

            int sessionId = ++currentSessionId;
            activeCts?.Cancel();
            activeCts?.Dispose();
            activeCts = new CancellationTokenSource();
            activeCts.CancelAfter(TimeSpan.FromSeconds(DefaultTranscriptionTimeoutSeconds));

            TransitionTo(CommanderVoiceState.Transcribing);

            try
            {
                CommanderSpeechToTextResult result = await sttProvider.TranscribeAsync(audio, activeCts.Token);

                // Reset safety check: if session changed during transcription (e.g. match reset or user cancel), discard!
                if (sessionId != currentSessionId || disposed)
                {
                    Debug.Log("[CommanderVoice] Discarding stale transcription from previous session.");
                    return CommanderSpeechToTextResult.Cancelled();
                }

                if (!result.Success)
                {
                    if (result.ErrorCode == "CANCELLED")
                    {
                        TransitionTo(CommanderVoiceState.Idle);
                        return result;
                    }

                    if (result.ErrorCode == "EMPTY_TRANSCRIPTION")
                    {
                        TransitionTo(CommanderVoiceState.Idle);
                        return result;
                    }

                    SetError(result.ErrorCode, result.UserFacingError);
                    return result;
                }

                string cleanTranscript = SafeCleanTranscript(result.Transcript);
                if (string.IsNullOrWhiteSpace(cleanTranscript))
                {
                    TransitionTo(CommanderVoiceState.Idle);
                    return CommanderSpeechToTextResult.Empty();
                }

                CurrentTranscript = cleanTranscript;

                if (AutoSubmit)
                {
                    TransitionTo(CommanderVoiceState.Idle);
                    if (submitAction != null)
                    {
                        await submitAction(cleanTranscript);
                    }
                }
                else
                {
                    TransitionTo(CommanderVoiceState.Preview);
                    TranscriptPreviewReady?.Invoke(cleanTranscript);
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                if (sessionId == currentSessionId && !disposed)
                    TransitionTo(CommanderVoiceState.Idle);
                return CommanderSpeechToTextResult.Cancelled();
            }
            catch (Exception ex)
            {
                if (sessionId == currentSessionId && !disposed)
                    SetError("UNEXPECTED_ERROR", $"Voice error: {ex.Message}");
                return CommanderSpeechToTextResult.Failure("UNEXPECTED_ERROR", ex.Message);
            }
        }

        public async Task<CommanderAIChatSubmission> ConfirmPreviewAndSubmitAsync()
        {
            if (State != CommanderVoiceState.Preview || string.IsNullOrWhiteSpace(CurrentTranscript))
                return null;

            string textToSubmit = CurrentTranscript;
            CurrentTranscript = string.Empty;
            TransitionTo(CommanderVoiceState.Idle);

            if (submitAction != null)
            {
                return await submitAction(textToSubmit);
            }

            return null;
        }

        public void Cancel()
        {
            if (disposed) return;

            ++currentSessionId;
            activeCts?.Cancel();
            activeCts?.Dispose();
            activeCts = null;

            if (audioCapture.IsRecording)
                audioCapture.CancelRecording();

            CurrentTranscript = string.Empty;
            TransitionTo(CommanderVoiceState.Idle);
        }

        public void ResetSession()
        {
            Cancel();
            LastError = string.Empty;
        }

        private void SetError(string code, string message)
        {
            LastError = message ?? "Voice error.";
            TransitionTo(CommanderVoiceState.Error);
            ErrorOccurred?.Invoke(LastError);
        }

        private void TransitionTo(CommanderVoiceState newState)
        {
            if (State == newState) return;
            State = newState;
            StateChanged?.Invoke(newState);
        }

        public static string SafeCleanTranscript(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            string trimmed = input.Trim();
            if (string.Equals(trimmed, "[BLANK_AUDIO]", StringComparison.OrdinalIgnoreCase)) return string.Empty;
            // Normalize internal carriage returns or double linebreaks if present
            trimmed = trimmed.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
            return trimmed.Trim();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Cancel();
            audioCapture?.Dispose();
            sttProvider?.Dispose();
        }
    }
}
