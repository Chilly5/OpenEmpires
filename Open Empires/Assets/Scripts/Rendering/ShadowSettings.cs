using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenEmpires
{
    /// <summary>
    /// Player-facing on/off switch for world shadows, remembered between sessions.
    ///
    /// Purely cosmetic. The simulation neither reads nor writes this, so a player turning shadows
    /// off cannot change gameplay, desync a multiplayer match, or alter a replay — two players in
    /// the same game may hold different settings safely.
    ///
    /// Implemented by switching the sun's shadows off rather than by editing the render pipeline
    /// asset, which would otherwise be written back to disk when changed in the editor.
    /// </summary>
    public static class ShadowSettings
    {
        private const string PrefKey = "shadows_enabled";

        private static bool loaded;
        private static bool enabled = true;
        private static Light cachedSun;

        public static bool Enabled
        {
            get
            {
                if (!loaded)
                {
                    enabled = PlayerPrefs.GetInt(PrefKey, 1) != 0;
                    loaded = true;
                }
                return enabled;
            }
            set
            {
                enabled = value;
                loaded = true;
                PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Apply();
            }
        }

        /// <summary>Pushes the current setting onto the scene's sun.</summary>
        public static void Apply()
        {
            Light sun = FindSun();
            if (sun == null) return;

            sun.shadows = Enabled ? LightShadows.Soft : LightShadows.None;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // A new scene brings a new light, so the cached one is no longer the right target.
            cachedSun = null;
            Apply();
        }

        private static Light FindSun()
        {
            if (cachedSun != null) return cachedSun;

            if (RenderSettings.sun != null)
            {
                cachedSun = RenderSettings.sun;
                return cachedSun;
            }

            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional)
                {
                    cachedSun = lights[i];
                    return cachedSun;
                }
            }

            return null;
        }
    }
}
