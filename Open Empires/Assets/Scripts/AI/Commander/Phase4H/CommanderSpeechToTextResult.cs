using System;

namespace OpenEmpires
{
    /// <summary>
    /// Pure, detached result of speech-to-text transcription.
    /// Strictly separated from GameSimulation, GameObjects, commands, goals, and simulation entities.
    /// </summary>
    public sealed class CommanderSpeechToTextResult
    {
        public bool Success { get; }
        public string Transcript { get; }
        public string ErrorCode { get; }
        public string UserFacingError { get; }
        public string DetectedLanguage { get; }
        public float Confidence { get; }

        private CommanderSpeechToTextResult(
            bool success,
            string transcript,
            string errorCode,
            string userFacingError,
            string detectedLanguage,
            float confidence)
        {
            Success = success;
            Transcript = transcript ?? string.Empty;
            ErrorCode = errorCode ?? string.Empty;
            UserFacingError = userFacingError ?? string.Empty;
            DetectedLanguage = detectedLanguage ?? string.Empty;
            Confidence = confidence;
        }

        public static CommanderSpeechToTextResult Accepted(string transcript, string detectedLanguage = "en", float confidence = 1f)
        {
            return new CommanderSpeechToTextResult(
                true,
                (transcript ?? string.Empty).Trim(),
                string.Empty,
                string.Empty,
                detectedLanguage,
                confidence);
        }

        public static CommanderSpeechToTextResult Failure(string errorCode, string userFacingError)
        {
            return new CommanderSpeechToTextResult(
                false,
                string.Empty,
                errorCode ?? "UNKNOWN_ERROR",
                userFacingError ?? "Voice transcription unavailable.",
                string.Empty,
                0f);
        }

        public static CommanderSpeechToTextResult Cancelled()
        {
            return new CommanderSpeechToTextResult(
                false,
                string.Empty,
                "CANCELLED",
                "Transcription was cancelled.",
                string.Empty,
                0f);
        }

        public static CommanderSpeechToTextResult Empty()
        {
            return new CommanderSpeechToTextResult(
                false,
                string.Empty,
                "EMPTY_TRANSCRIPTION",
                "No speech detected.",
                string.Empty,
                0f);
        }
    }
}
