using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace OpenEmpires
{
    /// <summary>Bounded native capture. Only explicit Start opens a microphone.
    /// The actual clip supplies rate/channels; progress is not a gameplay authority.</summary>
    public sealed class UnityMicrophoneAudioCapture : ICommanderAudioCapture,
        ICommanderCaptureReadiness, ICommanderCaptureProgress
    {
        private const int PreferredSampleRate = 16000;
        private const double OpeningTimeoutSeconds = 5;
        private static UnityMicrophoneAudioCapture leaseOwner;
        private readonly ICommanderMicrophoneBackend backend;
        private AudioClip activeClip;
        private string recordingDevice;
        private int sampleRate, channels, frameLimit, lastFrame;
        private double openedAt, audioStartedAt;
        private bool hasSamples, isRecording, ended, endRequested, disposed;
        public string CaptureError { get; private set; } = "";
        public float MaxDurationSeconds { get; private set; } = 15;
        public float PeakLevel { get; private set; }
        public float RootMeanSquareLevel { get; private set; }
        public float RecordedDurationSeconds { get { Poll(); return sampleRate > 0 ? (float)lastFrame / sampleRate : 0; } }
        public bool IsRecording => isRecording && !disposed;
        public string CurrentDevice => recordingDevice;
        public bool IsListening { get { Poll(); return isRecording && hasSamples && !ended && !disposed; } }
        public bool CaptureEnded { get { Poll(); return ended && !disposed; } }
        public bool PermissionReady
        {
            get { try { return !disposed && (backend.Devices?.Length ?? 0) > 0; } catch { return false; } }
        }
        public static string[] AvailableDevices => Microphone.devices ?? Array.Empty<string>();
        public static bool HasMicrophone => AvailableDevices.Length > 0;
        public UnityMicrophoneAudioCapture() : this(new UnityBackend()) { }
        public UnityMicrophoneAudioCapture(ICommanderMicrophoneBackend backend)
        { this.backend = backend ?? throw new ArgumentNullException(nameof(backend)); }
        public void SetHeldKey(string code) { }
        public Task<bool> RequestPermissionAsync(CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled<bool>(token) : Task.FromResult(PermissionReady);

        public bool StartRecording(string deviceName = null, float maxDurationSeconds = 15)
        {
            if (disposed || isRecording) return false;
            leaseOwner?.ReclaimStopped();
            if (leaseOwner != null) { CaptureError = "MIC_BUSY"; return false; }
            CaptureError = "";
            if (float.IsNaN(maxDurationSeconds) || float.IsInfinity(maxDurationSeconds) || maxDurationSeconds <= 0)
            { CaptureError = "MIC_INVALID_DURATION"; return false; }
            try
            {
                string[] devices = backend.Devices ?? Array.Empty<string>();
                if (devices.Length == 0) { CaptureError = "MIC_DEVICE_UNAVAILABLE"; return false; }
                string device = string.IsNullOrWhiteSpace(deviceName) ? devices[0]
                    : Array.Find(devices, name => string.Equals(name, deviceName, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrEmpty(device)) { CaptureError = "MIC_DEVICE_UNAVAILABLE"; return false; }
                backend.GetDeviceCaps(device, out int minimum, out int maximum);
                int requestedRate = PreferredSampleRate;
                if (minimum > 0 && requestedRate < minimum) requestedRate = minimum;
                if (maximum > 0 && requestedRate > maximum) requestedRate = maximum;
                if (requestedRate < 8000 || requestedRate > 96000 || minimum < 0 || maximum < 0
                    || minimum > 0 && maximum > 0 && minimum > maximum)
                { CaptureError = "MIC_UNSUPPORTED"; return false; }

                MaxDurationSeconds = Mathf.Clamp(maxDurationSeconds, 1, CommanderVoiceSettings.MaximumDurationSeconds);
                openedAt = backend.RealtimeSeconds;
                if (double.IsNaN(openedAt) || double.IsInfinity(openedAt)) { CaptureError = "MIC_INVALID_DATA"; return false; }
                recordingDevice = device; ended = endRequested = hasSamples = false;
                lastFrame = 0; PeakLevel = RootMeanSquareLevel = 0;
                leaseOwner = this; // One actual native capture, including pending/failed stop.
                activeClip = backend.Start(device, false, Mathf.CeilToInt(MaxDurationSeconds), requestedRate);
                if (activeClip == null) { CaptureError = "MIC_START_FAILED"; CancelRecording(); return false; }
                sampleRate = activeClip.frequency; channels = activeClip.channels;
                if (sampleRate < 8000 || sampleRate > 96000 || channels < 1 || channels > 2
                    || activeClip.samples <= 0 || activeClip.samples > (long)sampleRate * 60
                    || activeClip.samples < Math.Round(sampleRate * (double)MaxDurationSeconds))
                { CaptureError = "MIC_INVALID_DATA"; CancelRecording(); return false; }
                frameLimit = (int)Math.Round(sampleRate * (double)MaxDurationSeconds);
                isRecording = true;
                return true;
            }
            catch { CaptureError = "MIC_START_FAILED"; CancelRecording(); return false; }
        }

        private void Poll()
        {
            if (!isRecording || ended || disposed || activeClip == null) return;
            try
            {
                if (Array.IndexOf(backend.Devices ?? Array.Empty<string>(), recordingDevice) < 0)
                { Fail("MIC_DISCONNECTED"); return; }
                double now = backend.RealtimeSeconds;
                if (double.IsNaN(now) || double.IsInfinity(now) || now < openedAt)
                { Fail("MIC_INVALID_DATA"); return; }
                int position = backend.GetPosition(recordingDevice);
                bool nativeRecording = backend.IsRecording(recordingDevice);
                if (position < 0 || position > activeClip.samples) { Fail("MIC_INVALID_DATA"); return; }
                if (position == 0)
                {
                    if (!hasSamples)
                    {
                        if (now - openedAt >= OpeningTimeoutSeconds) Fail("MIC_NOT_READY");
                        return;
                    }
                    // A clock alone cannot invent a full recording. Recover only a
                    // closed non-loop buffer with observed near-capacity progress.
                    bool full = !nativeRecording && hasSamples
                        && lastFrame >= activeClip.samples - Math.Max(1, sampleRate / 4)
                        && now >= audioStartedAt + (double)activeClip.samples / sampleRate;
                    if (full) { lastFrame = frameLimit; ended = true; return; }
                    if (!nativeRecording || hasSamples) { Fail(hasSamples ? "MIC_DISCONNECTED" : "MIC_NOT_READY"); return; }
                    if (now - openedAt >= OpeningTimeoutSeconds) Fail("MIC_NOT_READY");
                    return;
                }
                if (position < lastFrame) { Fail("MIC_INVALID_DATA"); return; }
                if (!hasSamples)
                {
                    hasSamples = true;
                    audioStartedAt = Math.Max(openedAt, now - (double)position / sampleRate);
                }
                lastFrame = Math.Min(position, frameLimit);
                if (position >= frameLimit)
                {
                    ended = true;
                    if (!EndOwned()) CaptureError = "MIC_STOP_FAILED";
                }
                else if (!nativeRecording) Fail("MIC_DISCONNECTED");
                else if (now - audioStartedAt > MaxDurationSeconds + .25) Fail("MIC_STALLED");
            }
            catch { Fail("MIC_CAPTURE_FAILED"); }
        }

        private void Fail(string code)
        {
            CaptureError = code; ended = true;
            EndOwned(); // Best effort stop, never read/free work that is still active.
        }

        public CommanderAudioData StopRecording()
        {
            if (!isRecording || disposed) return CommanderAudioData.Empty;
            Poll();
            isRecording = false;
            try
            {
                if (!EndOwned()) { CaptureError = "MIC_STOP_FAILED"; return CommanderAudioData.Empty; }
                if (!string.IsNullOrEmpty(CaptureError) || activeClip == null) return CommanderAudioData.Empty;
                if (lastFrame == 0) { CaptureError = "MIC_NOT_READY"; return CommanderAudioData.Empty; }
                var samples = new float[checked(lastFrame * channels)];
                if (!backend.ReadSamples(activeClip, samples)) { CaptureError = "MIC_READ_FAILED"; return CommanderAudioData.Empty; }
                double squares = 0; float peak = 0;
                for (int i = 0; i < samples.Length; i++)
                {
                    float value = samples[i];
                    if (float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > 1.001f)
                    { CaptureError = "MIC_INVALID_DATA"; return CommanderAudioData.Empty; }
                    peak = Math.Max(peak, Math.Abs(value)); squares += (double)value * value;
                }
                PeakLevel = peak; RootMeanSquareLevel = (float)Math.Sqrt(squares / samples.Length);
                if (peak == 0) { CaptureError = "MIC_NO_SIGNAL"; return CommanderAudioData.Empty; }
                return new CommanderAudioData(samples, sampleRate, channels);
            }
            catch { CaptureError = "MIC_CAPTURE_FAILED"; return CommanderAudioData.Empty; }
            finally { ReclaimStopped(); }
        }

        private bool EndOwned()
        {
            if (leaseOwner != this) return true;
            try
            {
                if (!endRequested) { backend.End(recordingDevice); endRequested = true; }
                return !backend.IsRecording(recordingDevice);
            }
            catch { return false; }
        }

        private void ReclaimStopped()
        {
            if (leaseOwner != this || isRecording) return;
            try
            {
                if (backend.IsRecording(recordingDevice)) return;
                if (activeClip != null) backend.ReleaseClip(activeClip);
                activeClip = null; leaseOwner = null;
            }
            catch { CaptureError = "MIC_RELEASE_FAILED"; } // Keep the single ownership slot.
        }

        public void CancelRecording()
        {
            isRecording = false; ended = false;
            if (leaseOwner != this) return;
            if (!EndOwned() && string.IsNullOrEmpty(CaptureError)) CaptureError = "MIC_STOP_FAILED";
            ReclaimStopped();
        }

        public void Dispose()
        {
            // A repeated call may reclaim a previously quarantined stopped device.
            disposed = true; CancelRecording();
        }

        private sealed class UnityBackend : ICommanderMicrophoneBackend
        {
            public string[] Devices => AvailableDevices;
            public double RealtimeSeconds => Time.realtimeSinceStartupAsDouble;
            public void GetDeviceCaps(string device,out int minimum,out int maximum)=>Microphone.GetDeviceCaps(device,out minimum,out maximum);
            public AudioClip Start(string device,bool loop,int seconds,int requestedRate)=>Microphone.Start(device,loop,seconds,requestedRate);
            public int GetPosition(string device)=>Microphone.GetPosition(device);
            public bool IsRecording(string device)=>Microphone.IsRecording(device);
            public void End(string device)=>Microphone.End(device);
            public bool ReadSamples(AudioClip clip,float[] samples)=>clip.GetData(samples,0);
            public void ReleaseClip(AudioClip clip)=>UnityEngine.Object.Destroy(clip);
        }
    }
}
