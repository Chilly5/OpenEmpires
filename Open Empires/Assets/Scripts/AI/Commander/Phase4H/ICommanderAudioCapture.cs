using System;

namespace OpenEmpires
{
    /// <summary>
    /// Audio capture abstraction decoupling physical microphone recording
    /// from speech recognition and Commander text interpretation.
    /// </summary>
    public interface ICommanderAudioCapture : IDisposable
    {
        bool IsRecording { get; }
        string CurrentDevice { get; }
        bool StartRecording(string deviceName = null, float maxDurationSeconds = 15f);
        CommanderAudioData StopRecording();
        void CancelRecording();
    }
}
