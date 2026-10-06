using UnityEngine;

namespace OpenEmpires
{
    /// <summary>
    /// Configuration and user preferences for Commander voice input.
    /// Uses PlayerPrefs with safe defaults.
    /// </summary>
    public static class CommanderVoiceSettings
    {
        private const string PrefKeyEnabled = "voice_commander_enabled";
        private const string PrefKeyAutoSubmit = "voice_commander_autosubmit";
        private const string PrefKeyDevice = "voice_commander_device";
        private const string PrefKeyLanguage = "voice_commander_language";
        private const string PrefKeyModel = "voice_commander_model";
        private const string PrefKeyMaxDuration = "voice_commander_max_duration";

        public static bool VoiceEnabled
        {
            get => PlayerPrefs.GetInt(PrefKeyEnabled, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(PrefKeyEnabled, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool AutoSubmit
        {
            get => PlayerPrefs.GetInt(PrefKeyAutoSubmit, 0) != 0;
            set
            {
                PlayerPrefs.SetInt(PrefKeyAutoSubmit, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static string MicrophoneDevice
        {
            get => PlayerPrefs.GetString(PrefKeyDevice, string.Empty);
            set
            {
                PlayerPrefs.SetString(PrefKeyDevice, value ?? string.Empty);
                PlayerPrefs.Save();
            }
        }

        public static string Language
        {
            get => PlayerPrefs.GetString(PrefKeyLanguage, "en");
            set
            {
                PlayerPrefs.SetString(PrefKeyLanguage, string.IsNullOrWhiteSpace(value) ? "en" : value.Trim());
                PlayerPrefs.Save();
            }
        }

        public static string ModelName
        {
            get => PlayerPrefs.GetString(PrefKeyModel, WhisperModelLocator.DefaultModelName);
            set
            {
                PlayerPrefs.SetString(PrefKeyModel, string.IsNullOrWhiteSpace(value) ? WhisperModelLocator.DefaultModelName : value.Trim());
                PlayerPrefs.Save();
            }
        }

        public static float MaxDurationSeconds
        {
            get => PlayerPrefs.GetFloat(PrefKeyMaxDuration, 15f);
            set
            {
                PlayerPrefs.SetFloat(PrefKeyMaxDuration, Mathf.Clamp(value, 3f, 60f));
                PlayerPrefs.Save();
            }
        }

        public static string PttBindingPath => KeybindManager.GetBinding("CommanderPTT");

        public static void ResetToDefaults()
        {
            PlayerPrefs.DeleteKey(PrefKeyEnabled);
            PlayerPrefs.DeleteKey(PrefKeyAutoSubmit);
            PlayerPrefs.DeleteKey(PrefKeyDevice);
            PlayerPrefs.DeleteKey(PrefKeyLanguage);
            PlayerPrefs.DeleteKey(PrefKeyModel);
            PlayerPrefs.DeleteKey(PrefKeyMaxDuration);
            KeybindManager.ResetToDefault("CommanderPTT");
            PlayerPrefs.Save();
        }
    }
}
