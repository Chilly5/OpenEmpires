using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Whisper;
using Whisper.Native;

namespace OpenEmpires
{
    /// <summary>
    /// Local Whisper speech-to-text provider using whisper.unity bindings.
    /// Manages model loading, background thread inference, and safe degradation
    /// if the model file or native library is unavailable.
    /// </summary>
    public sealed class WhisperCommanderSpeechToTextProvider : ICommanderSpeechToTextProvider
    {
        private readonly string modelName;
        private readonly string explicitModelPath;
        private readonly string language;
        private readonly bool useGpu;
        private readonly object syncLock = new object();

        private WhisperWrapper whisperWrapper;
        private WhisperParams whisperParams;
        private bool isInitialized;
        private bool isAvailable;
        private string loadError;
        private bool disposed;

        public bool IsAvailable => isAvailable && !disposed;
        public string ModelPath { get; private set; }
        public long LastInferenceTimeMs { get; private set; }

        public WhisperCommanderSpeechToTextProvider(
            string modelName = WhisperModelLocator.DefaultModelName,
            string language = "en",
            string explicitModelPath = null,
            bool useGpu = false)
        {
            this.modelName = modelName;
            this.language = string.IsNullOrWhiteSpace(language) ? "en" : language.Trim();
            this.explicitModelPath = explicitModelPath;
            this.useGpu = useGpu;
        }

        public bool Initialize()
        {
            lock (syncLock)
            {
                if (disposed) return false;
                if (isInitialized) return isAvailable;

                isInitialized = true;
                if (!WhisperModelLocator.TryResolveModelPath(modelName, out string path, explicitModelPath))
                {
                    loadError = $"Whisper model '{modelName}' not found on disk.";
                    UnityEngine.Debug.LogWarning($"[CommanderVoice] {loadError}");
                    isAvailable = false;
                    return false;
                }

                ModelPath = path;

                try
                {
                    var contextParams = WhisperContextParams.GetDefaultParams();
                    contextParams.UseGpu = useGpu;

                    whisperWrapper = WhisperWrapper.InitFromFile(path, contextParams);
                    if (whisperWrapper == null)
                    {
                        loadError = "WhisperWrapper.InitFromFile returned null.";
                        UnityEngine.Debug.LogWarning($"[CommanderVoice] {loadError}");
                        isAvailable = false;
                        return false;
                    }

                    whisperParams = WhisperParams.GetDefaultParams(WhisperSamplingStrategy.WHISPER_SAMPLING_GREEDY);
                    whisperParams.Language = language;
                    whisperParams.Translate = false;
                    whisperParams.NoContext = true;
                    whisperParams.SingleSegment = true;

                    isAvailable = true;
                    loadError = null;
                    return true;
                }
                catch (DllNotFoundException ex)
                {
                    loadError = $"Native Whisper library not found: {ex.Message}";
                    UnityEngine.Debug.LogWarning($"[CommanderVoice] {loadError}");
                    isAvailable = false;
                    return false;
                }
                catch (Exception ex)
                {
                    loadError = $"Failed to initialize Whisper: {ex.Message}";
                    UnityEngine.Debug.LogWarning($"[CommanderVoice] {loadError}");
                    isAvailable = false;
                    return false;
                }
            }
        }

        public async Task<CommanderSpeechToTextResult> TranscribeAsync(
            CommanderAudioData audio,
            CancellationToken cancellationToken)
        {
            if (disposed || cancellationToken.IsCancellationRequested)
                return CommanderSpeechToTextResult.Cancelled();

            if (!isInitialized)
                Initialize();

            if (!isAvailable || whisperWrapper == null)
            {
                return CommanderSpeechToTextResult.Failure(
                    "STT_UNAVAILABLE",
                    "Voice transcription unavailable.");
            }

            if (audio == null || audio.Samples == null || audio.Samples.Length == 0)
                return CommanderSpeechToTextResult.Empty();

            // Resample / downmix to 16 kHz mono if not already
            CommanderAudioData prepared = CommanderAudioConverter.ConvertToMono16k(audio);
            if (prepared.Samples == null || prepared.Samples.Length == 0)
                return CommanderSpeechToTextResult.Empty();

            if (cancellationToken.IsCancellationRequested)
                return CommanderSpeechToTextResult.Cancelled();

            try
            {
                var stopwatch = Stopwatch.StartNew();

                // Run inference on a background worker thread to keep Unity main thread smooth
                WhisperResult result = await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    lock (syncLock)
                    {
                        if (disposed || whisperWrapper == null) return null;
                        cancellationToken.ThrowIfCancellationRequested();
                        return whisperWrapper.GetText(
                            prepared.Samples,
                            prepared.SampleRate,
                            prepared.Channels,
                            whisperParams);
                    }
                }, cancellationToken);

                stopwatch.Stop();
                LastInferenceTimeMs = stopwatch.ElapsedMilliseconds;

                if (cancellationToken.IsCancellationRequested)
                    return CommanderSpeechToTextResult.Cancelled();

                if (result == null || string.IsNullOrWhiteSpace(result.Result))
                    return CommanderSpeechToTextResult.Empty();

                string cleanTranscript = result.Result.Trim();
                if (cleanTranscript.Length == 0 || string.Equals(cleanTranscript, "[BLANK_AUDIO]", StringComparison.OrdinalIgnoreCase))
                    return CommanderSpeechToTextResult.Empty();

                return CommanderSpeechToTextResult.Accepted(
                    cleanTranscript,
                    result.Language ?? language,
                    1.0f);
            }
            catch (OperationCanceledException)
            {
                return CommanderSpeechToTextResult.Cancelled();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[CommanderVoice] Transcription error: {ex.Message}");
                return CommanderSpeechToTextResult.Failure(
                    "TRANSCRIPTION_FAILED",
                    "Could not understand the recording.");
            }
        }

        public void Dispose()
        {
            lock (syncLock)
            {
                if (disposed) return;
                disposed = true;
                whisperWrapper = null;
                whisperParams = null;
                isAvailable = false;
            }
        }
    }
}
