using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenEmpires
{
    public class VirtualCursor : MonoBehaviour
    {
        private static VirtualCursor instance;
        private static bool settingsMenuOpen;

        public static Vector2 Position { get; private set; }

        public static void SetSettingsMenuOpen(bool value) { settingsMenuOpen = value; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (instance != null) return;

            var go = new GameObject("VirtualCursor");
            instance = go.AddComponent<VirtualCursor>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            Position = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Update()
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (!locked && Mouse.current != null)
                Position = Mouse.current.position.ReadValue();

            Cursor.visible = !locked && (settingsMenuOpen || !CustomCursor.IsContextualCursorActive);
        }

        public static void RestorePosition(Vector2 position)
        {
            Position = new Vector2(
                Mathf.Clamp(position.x, 0f, Screen.width),
                Mathf.Clamp(position.y, 0f, Screen.height));

            Mouse.current?.WarpCursorPosition(Position);
        }
    }
}
