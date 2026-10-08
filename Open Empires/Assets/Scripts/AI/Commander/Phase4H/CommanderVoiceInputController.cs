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
        private Task<CommanderSpeechToTextResult> inFlightInference;
        private Task<CommanderAudioData> inFlightCapture;
        private Task deferredProviderRelease;
        private bool disposed;
        private bool disposing;

        public CommanderVoiceState State { get; private set; } = CommanderVoiceState.Idle;
        public string CurrentTranscript { get; private set; } = string.Empty;
        public string LastError { get; private set; } = string.Empty;
        public bool AutoSubmit { get; set; } = false;
        public float RecordingDurationLimitSeconds { get; private set; } = 15f;

        public event Action<CommanderVoiceState> StateChanged;
        public event Action<string> TranscriptPreviewReady;
        public event Action<string> ErrorOccurred;

        public CommanderVoiceInputController(
            ICommanderSpeechToTextProvider sttProvider,
            ICommanderAudioCapture audioCapture = null,
            Func<string, Task<CommanderAIChatSubmission>> submitAction = null)
        {
            this.sttProvider = sttProvider ?? throw new ArgumentNullException(nameof(sttProvider));
            this.audioCapture = audioCapture ?? CommanderAudioCaptureFactory.Create();
            this.submitAction = submitAction;
            this.AutoSubmit = CommanderVoiceSettings.AutoSubmit;
            if (sttProvider is ICommanderVoiceSessionProvider sessionProvider)
                sessionProvider.SessionInvalidated += Cancel;
        }

        public bool StartRecording(string deviceName = null, float maxDuration = -1f)
        {
            if (disposed || disposing) return false;
            // Cancellation may invalidate the UI before a native/provider call actually
            // stops. Do not accumulate replacement recordings/jobs behind that call.
            if (inFlightInference != null && !inFlightInference.IsCompleted || inFlightCapture != null && !inFlightCapture.IsCompleted)
            {
                SetError("STT_BUSY", "The previous transcription is still finishing. Please wait before recording again.");
                return false;
            }

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

            if (float.IsNaN(maxDuration) || float.IsInfinity(maxDuration))
            {
                SetError("AUDIO_DURATION_INVALID", "Choose a finite recording duration before trying again. No microphone was opened.");
                return false;
            }
            float duration = maxDuration > 0f ? maxDuration : CommanderVoiceSettings.MaxDurationSeconds;
            duration = Math.Min(CommanderVoiceSettings.MaximumDurationSeconds, Math.Max(1f, duration));
            RecordingDurationLimitSeconds = duration;
            string dev = !string.IsNullOrEmpty(deviceName) ? deviceName : CommanderVoiceSettings.MicrophoneDevice;

            int recordingGeneration = currentSessionId;
            if (sttProvider is ICommanderVoiceSessionProvider sessionProvider
                && !sessionProvider.TryBeginVoiceSession(out CommanderSpeechToTextResult blocker))
            {
                SetError(blocker?.ErrorCode ?? "VOICE_SESSION_UNAVAILABLE",
                    blocker?.UserFacingError ?? "Voice setup or explicit online consent is required.");
                return false;
            }
            // Consent/session observers may synchronously revoke or reset the
            // host while the gate runs. A stale positive return cannot record.
            if (disposed || recordingGeneration != currentSessionId) return false;

            bool started = audioCapture.StartRecording(dev, duration);
            if (!started)
            {
                (sttProvider as ICommanderVoiceSessionProvider)?.CancelVoiceSession();
                if (audioCapture is ICommanderCaptureReadiness readiness && !string.IsNullOrEmpty(readiness.CaptureError))
                    ReportCaptureFailure(readiness.CaptureError);
                else SetError("MIC_START_FAILED", "Could not start recording. Check microphone connection.");
                return false;
            }

            CurrentTranscript = string.Empty;
            LastError = string.Empty;
            TransitionTo(CommanderVoiceState.Recording);
            return true;
        }

        public async Task<CommanderSpeechToTextResult> StopRecordingAndTranscribeAsync()
        {
            if (disposed || disposing) return CommanderSpeechToTextResult.Cancelled();

            if (State != CommanderVoiceState.Recording)
            {
                Debug.LogWarning($"[CommanderVoice] Cannot stop recording while in state {State}.");
                return CommanderSpeechToTextResult.Empty();
            }

            int sessionId = ++currentSessionId;
            activeCts?.Cancel();
            var requestCancellation = new CancellationTokenSource();
            activeCts = requestCancellation;
            requestCancellation.CancelAfter(TimeSpan.FromSeconds(DefaultTranscriptionTimeoutSeconds));

            TransitionTo(CommanderVoiceState.Transcribing);

            try
            {
                // State observers can synchronously reset/dispose the host. The
                // notification itself is not permission to start a stale provider call.
                if (disposed || sessionId != currentSessionId || requestCancellation.IsCancellationRequested)
                    return CommanderSpeechToTextResult.Cancelled();
                var cancelled = Task.Delay(Timeout.Infinite, requestCancellation.Token);
                var capture = audioCapture is ICommanderAsyncAudioCapture asyncCapture
                    ? asyncCapture.StopRecordingAsync(requestCancellation.Token)
                    : Task.FromResult(audioCapture.StopRecording());
                inFlightCapture = capture;
                if (await Task.WhenAny(capture, cancelled) != capture || requestCancellation.IsCancellationRequested)
                {
                    ObserveLateInference(capture);
                    if (sessionId == currentSessionId && !disposed)
                        SetError("AUDIO_TIMEOUT", "Audio preparation took too long. No order was submitted; record again when the microphone is ready.");
                    return CommanderSpeechToTextResult.Cancelled();
                }
                var audio = await capture;
                if (disposed || sessionId != currentSessionId || requestCancellation.IsCancellationRequested)
                    return CommanderSpeechToTextResult.Cancelled();
                if (audio == null || audio.Samples == null || audio.Samples.Length == 0)
                {
                    (sttProvider as ICommanderVoiceSessionProvider)?.CancelVoiceSession();
                    if (audioCapture is ICommanderCaptureReadiness readiness && !string.IsNullOrEmpty(readiness.CaptureError))
                    {
                        ReportCaptureFailure(readiness.CaptureError);
                        return CommanderSpeechToTextResult.Failure("MIC_CAPTURE_FAILED", LastError);
                    }
                    TransitionTo(CommanderVoiceState.Idle);
                    return CommanderSpeechToTextResult.Empty();
                }
                var inference = sttProvider.TranscribeAsync(audio, requestCancellation.Token);
                inFlightInference = inference;
                if (await Task.WhenAny(inference, cancelled) != inference || requestCancellation.IsCancellationRequested)
                {
                    ObserveLateInference(inference);
                    if (sessionId == currentSessionId && !disposed)
                        SetError("TRANSCRIPTION_TIMEOUT", "Transcription took too long. No order was submitted; please retry when the engine is ready.");
                    return CommanderSpeechToTextResult.Cancelled();
                }
                CommanderSpeechToTextResult result = await inference;

                // Reset safety check: if session changed during transcription (e.g. match reset or user cancel), discard!
                if (sessionId != currentSessionId || disposed || requestCancellation.IsCancellationRequested)
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
                    if (sessionId != currentSessionId || disposed || requestCancellation.IsCancellationRequested)
                        return CommanderSpeechToTextResult.Cancelled();
                    if (submitAction != null)
                    {
                        await submitAction(cleanTranscript);
                    }
                }
                else
                {
                    TransitionTo(CommanderVoiceState.Preview);
                    if (sessionId != currentSessionId || disposed || requestCancellation.IsCancellationRequested)
                        return CommanderSpeechToTextResult.Cancelled();
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
            catch (Exception)
            {
                if (sessionId == currentSessionId && !disposed)
                    SetError("UNEXPECTED_ERROR", "Voice transcription failed. No order was submitted; please retry.");
                return CommanderSpeechToTextResult.Failure("UNEXPECTED_ERROR", "Voice transcription failed.");
            }
            finally
            {
                if (ReferenceEquals(activeCts, requestCancellation)) activeCts = null;
                // Complete the cancellation waiter even after ordinary success. The
                // inference owns its own lifetime and token; late faults are observed.
                if (!requestCancellation.IsCancellationRequested) requestCancellation.Cancel();
                requestCancellation.Dispose();
            }
        }

        public async Task<CommanderAIChatSubmission> ConfirmPreviewAndSubmitAsync()
        {
            if (disposed || disposing || State != CommanderVoiceState.Preview || string.IsNullOrWhiteSpace(CurrentTranscript))
                return null;

            string textToSubmit = CurrentTranscript;
            int confirmationSession = currentSessionId;
            CurrentTranscript = string.Empty;
            TransitionTo(CommanderVoiceState.Idle);

            if (disposed || disposing || confirmationSession != currentSessionId || State != CommanderVoiceState.Idle)
                return null;

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
            var pending = activeCts;
            activeCts = null;
            pending?.Cancel(); // The operation finally owns CTS disposal, not Cancel.
            (sttProvider as ICommanderVoiceSessionProvider)?.CancelVoiceSession();

            audioCapture.CancelRecording(); // Also cancels pending async final-tail/load work.

            CurrentTranscript = string.Empty;
            TransitionTo(CommanderVoiceState.Idle);
        }

        public void ResetSession()
        {
            Cancel();
            LastError = string.Empty;
        }
        public void ReportCaptureFailure(string code)
        {
            if(disposed||disposing)return;int generation=currentSessionId;Cancel();
            if(disposed||disposing||currentSessionId!=unchecked(generation+1)||State!=CommanderVoiceState.Idle)return;
            string message=code=="MIC_FOCUS_LOST"?"Recording cancelled because the page/app lost focus. No order was submitted; use a fresh Record action."
                :code=="MIC_PERMISSION_REQUIRED"||code=="MIC_PERMISSION_DENIED"?"Microphone permission is required. No order was submitted; enable the microphone explicitly in Voice setup."
                :code=="MIC_DISCONNECTED"||code=="MIC_DEVICE_UNAVAILABLE"?"The selected microphone is unavailable. No order was submitted; reconnect/select the device and activate it again."
                :code=="MIC_BUSY"?"The previous microphone operation is still finishing. No order was submitted; wait for it to stop or finish/cancel any pending permission prompt before a fresh Record action."
                :code=="MIC_NOT_READY"?"Recording ended before the microphone was ready. No order was submitted; wait for Listening before speaking."
                :code=="MIC_NO_SIGNAL"?"No microphone signal was captured. No order was submitted; check/select the microphone and record a fresh clip."
                :code=="MIC_SUSPENDED"?"Browser audio was suspended. No order was submitted; activate the microphone again with a fresh click."
                :code=="MIC_UNSUPPORTED"||code=="MIC_SETUP_REQUIRED"?"Microphone setup is unsupported or blocked. No order was submitted; check the device/app permissions, secure browser hosting and embedding support where applicable, or type."
                :"The microphone could not safely open or finish this recording. No order was submitted; check the device/permission and try a fresh action.";
            SetError("MIC_CAPTURE_FAILED",message);
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
            if (disposed || disposing) return;
            // Mark shutdown BEFORE event-producing Cancel, without suppressing
            // cancellation itself. Reentrant Dispose/Start cannot own resources twice.
            disposing = true;
            Cancel();
            disposed = true;
            if (sttProvider is ICommanderVoiceSessionProvider sessionProvider)
                sessionProvider.SessionInvalidated -= Cancel;
            audioCapture?.Dispose();
            var pending = inFlightInference;
            if (pending != null && !pending.IsCompleted)
                deferredProviderRelease = ReleaseProviderAfterInference(pending);
            else sttProvider?.Dispose();
        }

        private static void ObserveLateInference(Task pending)
        {
            _ = pending.ContinueWith(t => { var ignored = t.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private async Task ReleaseProviderAfterInference(Task<CommanderSpeechToTextResult> pending)
        {
            // Keep the provider/context alive until the actual call returns; never wait
            // synchronously on the UI thread. Resume on the host context for adapters
            // whose cleanup needs Unity/JavaScript thread affinity.
            try { await pending; } catch (Exception) { /* Observed, stale result remains inert. */ }
            try { sttProvider?.Dispose(); }
            catch (Exception error) { Debug.LogWarning("[CommanderVoice] Deferred provider release failed; type=" + error.GetType().Name); }
        }
    }
}
