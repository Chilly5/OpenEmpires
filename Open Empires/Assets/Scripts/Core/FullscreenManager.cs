using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace OpenEmpires
{
    public class FullscreenManager : MonoBehaviour
    {
        public static FullscreenManager Instance { get; private set; }
        public bool IsFullscreen { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void RequestBrowserFullscreen(string gameObjectName);

        [DllImport("__Internal")]
        private static extern void RegisterFullscreenChangeListener(string gameObjectName);
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance != null) return;

            var go = new GameObject("FullscreenManager");
            Instance = go.AddComponent<FullscreenManager>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

#if UNITY_WEBGL && !UNITY_EDITOR
            RegisterFullscreenChangeListener(gameObject.name);
#endif
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void EnterFullscreen()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            RequestBrowserFullscreen(gameObject.name);
#else
            Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
            Screen.fullScreen = true;
            Cursor.lockState = CursorLockMode.Confined;
            IsFullscreen = true;
#endif
        }

        public void ExitFullscreen()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL: exit via browser API; OnFullscreenChanged callback will update state
            Screen.fullScreen = false;
#else
            Screen.fullScreen = false;
            Cursor.lockState = CursorLockMode.None;
            IsFullscreen = false;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // Called from jslib via SendMessage
        private void OnFullscreenEntered(string unused)
        {
            IsFullscreen = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Called from jslib via SendMessage
        private void OnFullscreenError(string error)
        {
            Debug.LogWarning($"[Fullscreen] Request denied: {error}");
        }

        // Called from jslib via SendMessage on fullscreenchange event
        private void OnFullscreenChanged(string state)
        {
            if (state == "1")
            {
                IsFullscreen = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                // Exited fullscreen — also unlock pointer
                IsFullscreen = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void Update()
        {
            // Detect pointer lock loss (user pressed Escape once).
            if (IsFullscreen && Cursor.lockState != CursorLockMode.Locked)
            {
                IsFullscreen = false;
                Cursor.visible = true;
            }
        }
#else
        private void Update()
        {
            // Detect Alt+Enter or other fullscreen exits on desktop
            if (IsFullscreen && !Screen.fullScreen)
            {
                IsFullscreen = false;
                Cursor.lockState = CursorLockMode.None;
            }
        }
#endif
    }
}
