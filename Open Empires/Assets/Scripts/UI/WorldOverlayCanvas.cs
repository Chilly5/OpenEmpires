using UnityEngine;

namespace OpenEmpires
{
    public static class WorldOverlayCanvas
    {
        public static Canvas Instance { get; private set; }

        public static void EnsureCreated()
        {
            if (Instance != null) return;
            var go = new GameObject("WorldOverlayCanvas");
            Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<Canvas>();
            Instance.renderMode = RenderMode.ScreenSpaceOverlay;
            Instance.sortingOrder = 3;
        }
    }
}
