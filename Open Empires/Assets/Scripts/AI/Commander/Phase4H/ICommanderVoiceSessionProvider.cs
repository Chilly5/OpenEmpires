using System;

namespace OpenEmpires
{
    /// <summary>Optional recording-time gate for providers with privacy/session requirements.
    /// A successful gate freezes one voice job, not gameplay authority.</summary>
    public interface ICommanderVoiceSessionProvider
    {
        bool TryBeginVoiceSession(out CommanderSpeechToTextResult blocker);
        void CancelVoiceSession();
        event Action SessionInvalidated;
    }
}
