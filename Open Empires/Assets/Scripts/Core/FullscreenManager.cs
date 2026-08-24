using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using System;
using UnityEditor;
#endif
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
#elif UNITY_EDITOR
            SetEditorGameViewMaximized(true);
            Cursor.lockState = CursorLockMode.Confined;
            IsFullscreen = IsEditorGameViewMaximized();
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
#elif UNITY_EDITOR
            SetEditorGameViewMaximized(false);
            Cursor.lockState = CursorLockMode.None;
            IsFullscreen = IsEditorGameViewMaximized();
#else
            Screen.fullScreen = false;
            Cursor.lockState = CursorLockMode.None;
            IsFullscreen = false;
#endif
        }

        public void ToggleFullscreen()
        {
            if (IsFullscreen)
                ExitFullscreen();
            else
                EnterFullscreen();
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // Called from jslib via SendMessage
        private void OnFullscreenEntered(string unused)
        {
            IsFullscreen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
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
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                // Exited fullscreen — also unlock pointer
                IsFullscreen = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

#else
        private void Update()
        {
            if (WasFullscreenShortcutPressed())
                ToggleFullscreen();

#if UNITY_EDITOR
            bool editorFullscreen = IsEditorGameViewMaximized();
            if (IsFullscreen != editorFullscreen)
            {
                IsFullscreen = editorFullscreen;
                if (!IsFullscreen)
                    Cursor.lockState = CursorLockMode.None;
            }
#else
            if (IsFullscreen && !Screen.fullScreen)
            {
                IsFullscreen = false;
                Cursor.lockState = CursorLockMode.None;
            }
#endif
        }

        private static bool WasFullscreenShortcutPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            bool enterPressed = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
            bool altPressed = keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;
            return keyboard.f11Key.wasPressedThisFrame || (altPressed && enterPressed);
        }

#if UNITY_EDITOR
        private static void SetEditorGameViewMaximized(bool maximized)
        {
            var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null)
            {
                Debug.LogWarning("[Fullscreen] Could not find the Unity Game view.");
                return;
            }

            var gameView = EditorWindow.GetWindow(gameViewType);
            if (gameView == null)
            {
                Debug.LogWarning("[Fullscreen] Could not open the Unity Game view.");
                return;
            }

            gameView.Focus();
            gameView.maximized = maximized;
        }

        private static bool IsEditorGameViewMaximized()
        {
            var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null)
                return false;

            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                var window = windows[i];
                if (window != null && window.GetType() == gameViewType)
                    return window.maximized;
            }

            return false;
        }
#endif
#endif
    }
}
