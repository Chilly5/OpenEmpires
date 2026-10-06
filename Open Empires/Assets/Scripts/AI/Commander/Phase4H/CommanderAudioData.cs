using System;

namespace OpenEmpires
{
    /// <summary>
    /// Pure, detached audio representation for speech-to-text processing.
    /// Contains no GameSimulation, GameObject, or gameplay state.
    /// </summary>
    public sealed class CommanderAudioData
    {
        public float[] Samples { get; }
        public int SampleRate { get; }
        public int Channels { get; }
        public float DurationSeconds { get; }

        public CommanderAudioData(float[] samples, int sampleRate, int channels)
        {
            Samples = samples ?? Array.Empty<float>();
            SampleRate = sampleRate > 0 ? sampleRate : 16000;
            Channels = channels > 0 ? channels : 1;
            DurationSeconds = (Samples.Length > 0 && SampleRate > 0 && Channels > 0)
                ? (float)Samples.Length / (SampleRate * Channels)
                : 0f;
        }

        public static CommanderAudioData Empty => new CommanderAudioData(Array.Empty<float>(), 16000, 1);
    }
}
