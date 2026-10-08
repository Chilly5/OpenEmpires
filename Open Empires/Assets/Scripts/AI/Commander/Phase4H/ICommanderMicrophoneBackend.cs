using UnityEngine;

namespace OpenEmpires
{
    /// <summary>Native microphone boundary. Clip creation and final release belong
    /// to this backend; capture logic owns the bounded recording/session lifetime.</summary>
    public interface ICommanderMicrophoneBackend
    {
        string[] Devices { get; }
        double RealtimeSeconds { get; }
        void GetDeviceCaps(string device, out int minimum, out int maximum);
        AudioClip Start(string device, bool loop, int seconds, int requestedRate);
        int GetPosition(string device);
        bool IsRecording(string device);
        void End(string device);
        bool ReadSamples(AudioClip clip, float[] samples);
        void ReleaseClip(AudioClip clip);
    }

    /// <summary>Detached, bounded capture diagnostics. No audio export or authority.</summary>
    public interface ICommanderCaptureProgress
    {
        float MaxDurationSeconds { get; }
        float RecordedDurationSeconds { get; }
        float PeakLevel { get; }
        float RootMeanSquareLevel { get; }
    }
}
