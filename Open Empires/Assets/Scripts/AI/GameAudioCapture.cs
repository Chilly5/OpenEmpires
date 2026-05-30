using UnityEngine;

namespace OpenEmpires
{
    // Continuously captures the game's final mixed audio into a rolling ring buffer and, on
    // demand, encodes the last N seconds as a mono 16-bit WAV for the LLM's audio input.
    //
    // Must live on the AudioListener's GameObject — OnAudioFilterRead only receives the full
    // post-mix signal there. Self-bootstrapping: access via Instance, which finds the active
    // AudioListener and attaches itself. Owner-client only; touches no sim state.
    public class GameAudioCapture : MonoBehaviour
    {
        // Ring capacity in seconds; the largest window EncodeRecentWav can return.
        private const int MaxBufferSeconds = 20;

        private static GameAudioCapture instance;
        public static GameAudioCapture Instance
        {
            get
            {
                if (instance == null)
                {
                    var listener = Object.FindFirstObjectByType<AudioListener>();
                    if (listener == null) return null; // no listener yet → caller skips audio
                    instance = listener.GetComponent<GameAudioCapture>();
                    if (instance == null) instance = listener.gameObject.AddComponent<GameAudioCapture>();
                }
                return instance;
            }
        }

        private float[] ring;
        private int writePos;
        private int filled;
        private int sampleRate;
        private readonly object sync = new object();

        private void Awake()
        {
            sampleRate = AudioSettings.outputSampleRate;
            if (sampleRate <= 0) sampleRate = 44100;
            ring = new float[sampleRate * MaxBufferSeconds];
        }

        // Runs on the AUDIO THREAD. We only read `data` (downmix to mono) and never modify
        // it, so playback is untouched. The ring is guarded because EncodeRecentWav reads it
        // from the main thread.
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (ring == null || channels <= 0) return;
            int frames = data.Length / channels;
            lock (sync)
            {
                for (int f = 0; f < frames; f++)
                {
                    int baseI = f * channels;
                    float sum = 0f;
                    for (int c = 0; c < channels; c++) sum += data[baseI + c];
                    ring[writePos] = sum / channels;
                    if (++writePos >= ring.Length) writePos = 0;
                    if (filled < ring.Length) filled++;
                }
            }
        }

        // Encodes the most recent `seconds` of captured audio as a mono 16-bit WAV.
        // Returns null if nothing has been captured yet.
        public byte[] EncodeRecentWav(float seconds)
        {
            if (ring == null) return null;
            int want = Mathf.Clamp(Mathf.RoundToInt(seconds * sampleRate), 0, ring.Length);
            float[] mono;
            int count;
            lock (sync)
            {
                count = Mathf.Min(want, filled);
                if (count == 0) return null;
                mono = new float[count];
                int start = writePos - count; // last `count` samples ending at the write head
                for (int i = 0; i < count; i++)
                {
                    int idx = start + i;
                    idx %= ring.Length;
                    if (idx < 0) idx += ring.Length;
                    mono[i] = ring[idx];
                }
            }
            return WavEncoder.EncodePcm16Mono(mono, sampleRate);
        }
    }

    // Minimal little-endian PCM16 mono WAV writer (44-byte header + samples).
    internal static class WavEncoder
    {
        public static byte[] EncodePcm16Mono(float[] samples, int sampleRate)
        {
            int n = samples.Length;
            int dataBytes = n * 2;
            var buf = new byte[44 + dataBytes];

            WriteAscii(buf, 0, "RIFF");
            WriteInt32(buf, 4, 36 + dataBytes);
            WriteAscii(buf, 8, "WAVE");
            WriteAscii(buf, 12, "fmt ");
            WriteInt32(buf, 16, 16);              // PCM fmt chunk size
            WriteInt16(buf, 20, 1);               // format = PCM
            WriteInt16(buf, 22, 1);               // channels = mono
            WriteInt32(buf, 24, sampleRate);
            WriteInt32(buf, 28, sampleRate * 2);  // byte rate = rate * channels * bytesPerSample
            WriteInt16(buf, 32, 2);               // block align = channels * bytesPerSample
            WriteInt16(buf, 34, 16);              // bits per sample
            WriteAscii(buf, 36, "data");
            WriteInt32(buf, 40, dataBytes);

            int p = 44;
            for (int i = 0; i < n; i++)
            {
                float v = Mathf.Clamp(samples[i], -1f, 1f);
                short s = (short)Mathf.RoundToInt(v * 32767f);
                buf[p++] = (byte)(s & 0xff);
                buf[p++] = (byte)((s >> 8) & 0xff);
            }
            return buf;
        }

        private static void WriteAscii(byte[] buf, int offset, string s)
        {
            for (int i = 0; i < s.Length; i++) buf[offset + i] = (byte)s[i];
        }

        private static void WriteInt32(byte[] buf, int offset, int value)
        {
            buf[offset] = (byte)(value & 0xff);
            buf[offset + 1] = (byte)((value >> 8) & 0xff);
            buf[offset + 2] = (byte)((value >> 16) & 0xff);
            buf[offset + 3] = (byte)((value >> 24) & 0xff);
        }

        private static void WriteInt16(byte[] buf, int offset, int value)
        {
            buf[offset] = (byte)(value & 0xff);
            buf[offset + 1] = (byte)((value >> 8) & 0xff);
        }
    }
}
