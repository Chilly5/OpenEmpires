using System;
using UnityEngine;

namespace OpenEmpires
{
    /// <summary>
    /// Unity Microphone audio capture implementation.
    /// Handles device enumeration, frequency capability detection, bounded recording duration,
    /// and exact position trimming without fixed silence padding.
    /// </summary>
    public sealed class UnityMicrophoneAudioCapture : ICommanderAudioCapture
    {
        private const int PreferredSampleRate = 16000;
        private const float DefaultMaxDurationSeconds = 15f;

        private AudioClip activeClip;
        private string recordingDevice;
        private int recordingSampleRate;
        private float recordingMaxDuration;
        private bool isRecording;
        private bool disposed;

        public bool IsRecording => isRecording && !disposed;
        public string CurrentDevice => recordingDevice;

        public static string[] AvailableDevices => Microphone.devices ?? Array.Empty<string>();

        public static bool HasMicrophone => AvailableDevices.Length > 0;

        public bool StartRecording(string deviceName = null, float maxDurationSeconds = DefaultMaxDurationSeconds)
        {
            if (disposed) return false;
            if (isRecording)
            {
                Debug.LogWarning("[CommanderVoice] Microphone is already recording.");
                return false;
            }

            string[] devices = AvailableDevices;
            if (devices.Length == 0)
            {
                Debug.LogWarning("[CommanderVoice] No microphone devices detected on system.");
                return false;
            }

            // Select requested device or fallback to first available
            string targetDevice = null;
            if (!string.IsNullOrWhiteSpace(deviceName))
            {
                for (int i = 0; i < devices.Length; i++)
                {
                    if (string.Equals(devices[i], deviceName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetDevice = devices[i];
                        break;
                    }
                }
            }

            if (targetDevice == null)
            {
                targetDevice = devices[0];
            }

            recordingDevice = targetDevice;
            recordingMaxDuration = Mathf.Clamp(maxDurationSeconds, 1f, 60f);

            // Determine sample rate based on device capabilities
            int sampleRate = SelectSupportedSampleRate(targetDevice);
            recordingSampleRate = sampleRate;

            try
            {
                int bufferLengthSec = Mathf.CeilToInt(recordingMaxDuration);
                activeClip = Microphone.Start(targetDevice, false, bufferLengthSec, sampleRate);
                if (activeClip == null)
                {
                    Debug.LogWarning($"[CommanderVoice] Microphone.Start returned null for device '{targetDevice}'.");
                    isRecording = false;
                    return false;
                }

                isRecording = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CommanderVoice] Failed to start microphone on '{targetDevice}': {ex.Message}");
                isRecording = false;
                activeClip = null;
                return false;
            }
        }

        public CommanderAudioData StopRecording()
        {
            if (!isRecording || disposed) return CommanderAudioData.Empty;

            try
            {
                int position = Microphone.GetPosition(recordingDevice);
                Microphone.End(recordingDevice);
                isRecording = false;

                if (activeClip == null) return CommanderAudioData.Empty;

                int channels = activeClip.channels;
                int recordedSampleCount = position * channels;

                if (recordedSampleCount <= 0)
                {
                    CleanupClip();
                    return CommanderAudioData.Empty;
                }

                // Clamp to clip capacity
                int totalClipSamples = activeClip.samples * channels;
                if (recordedSampleCount > totalClipSamples)
                    recordedSampleCount = totalClipSamples;

                float[] samples = new float[recordedSampleCount];
                activeClip.GetData(samples, 0);

                var audioData = new CommanderAudioData(samples, recordingSampleRate, channels);
                CleanupClip();
                return audioData;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CommanderVoice] Error stopping microphone: {ex.Message}");
                CancelRecording();
                return CommanderAudioData.Empty;
            }
        }

        public void CancelRecording()
        {
            if (!isRecording && activeClip == null) return;

            try
            {
                if (!string.IsNullOrEmpty(recordingDevice))
                    Microphone.End(recordingDevice);
            }
            catch
            {
                // Ignore cancel exceptions
            }

            isRecording = false;
            CleanupClip();
        }

        private void CleanupClip()
        {
            if (activeClip != null)
            {
                UnityEngine.Object.Destroy(activeClip);
                activeClip = null;
            }
        }

        private static int SelectSupportedSampleRate(string deviceName)
        {
            Microphone.GetDeviceCaps(deviceName, out int minFreq, out int maxFreq);

            // minFreq == 0 and maxFreq == 0 means device supports any sample rate
            if (minFreq == 0 && maxFreq == 0)
                return PreferredSampleRate;

            if (PreferredSampleRate >= minFreq && PreferredSampleRate <= maxFreq)
                return PreferredSampleRate;

            if (maxFreq > 0)
                return maxFreq;

            if (minFreq > 0)
                return minFreq;

            return PreferredSampleRate;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            CancelRecording();
        }
    }
}
