using UnityEngine;

namespace OpenEmpires
{
    public class CustomCursor : MonoBehaviour
    {
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
        }

        private void OnDestroy()
        {
            Cursor.visible = true;
            if (instance == this) instance = null;
        }
    }
}
