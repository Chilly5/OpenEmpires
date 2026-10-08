using System;

namespace OpenEmpires
{
    /// <summary>
    /// Deterministic audio preprocessing and conversion utility.
    /// Resamples and downmixes raw audio into mono 16 kHz audio suitable for Whisper STT.
    /// Does not depend on GameSimulation or Unity GameObject lifecycles.
    /// </summary>
    public static class CommanderAudioConverter
    {
        public const int TargetSampleRate = 16000;
        public const float DefaultMaxDurationSeconds = CommanderVoiceSettings.MaximumDurationSeconds;

        public static CommanderAudioData ConvertToMono16k(CommanderAudioData source, float maxDurationSeconds = DefaultMaxDurationSeconds)
        {
            if (source == null || source.Samples == null || source.Samples.Length == 0)
                return CommanderAudioData.Empty;

            return ConvertToMono16k(source.Samples, source.SampleRate, source.Channels, maxDurationSeconds);
        }

        public static CommanderAudioData ConvertToMono16k(float[] srcSamples, int srcSampleRate, int srcChannels, float maxDurationSeconds = DefaultMaxDurationSeconds)
        {
            if (srcSamples == null || srcSamples.Length == 0 || srcSampleRate <= 0 || srcChannels <= 0)
                return CommanderAudioData.Empty;

            if (float.IsNaN(maxDurationSeconds) || float.IsInfinity(maxDurationSeconds)
                || maxDurationSeconds <= 0f) return CommanderAudioData.Empty;
            maxDurationSeconds = Math.Min(maxDurationSeconds, CommanderVoiceSettings.MaximumDurationSeconds);
            int sourceFrames = Math.Min(srcSamples.Length / srcChannels,
                (int)Math.Round(maxDurationSeconds * srcSampleRate));

            // 1. Downmix to mono if multi-channel
            float[] monoSamples;
            if (srcChannels > 1)
            {
                int monoLength = sourceFrames;
                monoSamples = new float[monoLength];
                for (int i = 0; i < monoLength; i++)
                {
                    float sum = 0f;
                    int offset = i * srcChannels;
                    for (int ch = 0; ch < srcChannels; ch++)
                        sum += srcSamples[offset + ch];
                    monoSamples[i] = sum / srcChannels;
                }
            }
            else
            {
                if (sourceFrames == srcSamples.Length) monoSamples = srcSamples;
                else
                {
                    monoSamples = new float[sourceFrames];
                    Array.Copy(srcSamples, monoSamples, sourceFrames);
                }
            }

            // 2. Clamp duration if exceeds maximum
            if (maxDurationSeconds > 0f)
            {
                int maxSamples = (int)Math.Round(maxDurationSeconds * srcSampleRate);
                if (monoSamples.Length > maxSamples)
                {
                    float[] clamped = new float[maxSamples];
                    Array.Copy(monoSamples, 0, clamped, 0, maxSamples);
                    monoSamples = clamped;
                }
            }

            // 3. Resample to 16 kHz if necessary
            float[] resampled;
            if (srcSampleRate == TargetSampleRate)
            {
                resampled = monoSamples;
            }
            else
            {
                int srcLen = monoSamples.Length;
                float duration = (float)srcLen / srcSampleRate;
                int dstLen = (int)Math.Round(duration * TargetSampleRate);
                if (dstLen <= 0)
                    return CommanderAudioData.Empty;

                resampled = new float[dstLen];
                for (int i = 0; i < dstLen; i++)
                {
                    float srcIndex = (float)i / dstLen * srcLen;
                    int low = (int)Math.Floor(srcIndex);
                    float fraction = srcIndex - low;

                    if (low + 1 >= srcLen)
                        resampled[i] = monoSamples[srcLen - 1];
                    else
                        resampled[i] = monoSamples[low] + fraction * (monoSamples[low + 1] - monoSamples[low]);
                }
            }

            return new CommanderAudioData(resampled, TargetSampleRate, 1);
        }
    }
}
