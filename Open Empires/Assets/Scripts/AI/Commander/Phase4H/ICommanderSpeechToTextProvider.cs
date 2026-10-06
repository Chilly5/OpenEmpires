using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenEmpires
{
    /// <summary>
    /// Speech-to-text provider abstraction for Commander voice input.
    /// Allows swapping between Local Whisper, Mock provider for testing, or future providers
    /// without touching any Commander gameplay code.
    /// </summary>
    public interface ICommanderSpeechToTextProvider : IDisposable
    {
        /// <summary>
        /// Indicates whether the provider is initialized and ready for transcription.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Asynchronously transcribes audio data to text.
        /// </summary>
        Task<CommanderSpeechToTextResult> TranscribeAsync(
            CommanderAudioData audio,
            CancellationToken cancellationToken);
    }
}
