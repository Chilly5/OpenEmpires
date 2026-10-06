using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenEmpires
{
    /// <summary>
    /// Mock speech-to-text provider for deterministic automated testing.
    /// Provides controllable transcripts, latency, simulated errors, and invocation tracking.
    /// </summary>
    public sealed class MockCommanderSpeechToTextProvider : ICommanderSpeechToTextProvider
    {
        private readonly Queue<string> cannedTranscripts = new Queue<string>();
        public bool IsAvailable { get; set; } = true;
        public TimeSpan SimulatedLatency { get; set; } = TimeSpan.Zero;
        public bool SimulateFailure { get; set; }
        public string FailureErrorCode { get; set; } = "MOCK_FAILURE";
        public string FailureErrorMessage { get; set; } = "Mock speech recognition failure.";
        public int InvocationCount { get; private set; }
        public CommanderAudioData LastTranscribedAudio { get; private set; }
        public bool Disposed { get; private set; }

        public MockCommanderSpeechToTextProvider(params string[] initialTranscripts)
        {
            if (initialTranscripts != null)
            {
                foreach (string t in initialTranscripts)
                    cannedTranscripts.Enqueue(t);
            }
        }

        public void EnqueueTranscript(string transcript)
        {
            cannedTranscripts.Enqueue(transcript);
        }

        public async Task<CommanderSpeechToTextResult> TranscribeAsync(
            CommanderAudioData audio,
            CancellationToken cancellationToken)
        {
            if (Disposed || cancellationToken.IsCancellationRequested)
                return CommanderSpeechToTextResult.Cancelled();

            InvocationCount++;
            LastTranscribedAudio = audio;

            if (SimulatedLatency > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(SimulatedLatency, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return CommanderSpeechToTextResult.Cancelled();
                }
            }

            if (cancellationToken.IsCancellationRequested)
                return CommanderSpeechToTextResult.Cancelled();

            if (!IsAvailable || SimulateFailure)
            {
                return CommanderSpeechToTextResult.Failure(FailureErrorCode, FailureErrorMessage);
            }

            if (cannedTranscripts.Count == 0)
                return CommanderSpeechToTextResult.Empty();

            string transcript = cannedTranscripts.Dequeue();
            if (string.IsNullOrWhiteSpace(transcript))
                return CommanderSpeechToTextResult.Empty();

            return CommanderSpeechToTextResult.Accepted(transcript.Trim());
        }

        public void Dispose()
        {
            Disposed = true;
            IsAvailable = false;
        }
    }
}
