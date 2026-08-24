using UnityEngine;

namespace OpenEmpires
{
    public class CustomCursor : MonoBehaviour
    {
        private static readonly Vector2 CursorHotspot = new Vector2(10f, 15f);

        private static CustomCursor instance;

        private static bool contextualCursorActive;
        public static bool IsContextualCursorActive => contextualCursorActive;
        public static void SetContextualCursorActive(bool value) { contextualCursorActive = value; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (instance != null) return;
            var go = new GameObject("CustomCursor");
            instance = go.AddComponent<CustomCursor>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;

            var cursorTexture = Resources.Load<Texture2D>("ResourceIcons/cursoricon");
            if (cursorTexture != null)
                Cursor.SetCursor(cursorTexture, CursorHotspot, CursorMode.Auto);
        }

        private void OnDestroy()
        {
            if (instance != this) return;

            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            Cursor.visible = true;
            instance = null;
        }
    }
}
