using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OpenEmpires
{
    // Edge-of-screen directional arrows ("peripheral pings") for off-screen attacks on the
    // local player's units/buildings. Driven by the same OnEntityDamaged event the minimap
    // uses, but presented at the screen periphery as clickable arrows that pan the camera
    // to the source. Self-bootstraps once and lives across scene loads.
    public class PeripheralAttackIndicators : MonoBehaviour
    {
        private const float Lifetime = 3f;          // matches MinimapUI.PingDuration
        private const float ProximitySq = 6f * 6f;  // matches MinimapUI dedup
        private const float PulseRate = 4f;         // matches MinimapUI.PingFlashRate
        private const float EdgeMargin = 60f;       // px from screen edge for the arrow ring
        private const float ArrowSize = 48f;        // px (canvas reference)
        private const float FlashDuration = 0.8f;   // seconds — vignette decay after a hit
        private const float FlashMaxAlpha = 0.55f;  // peak vignette opacity at FlashLevel = 1
        private const string FlashLevelPrefKey = "peripheral_flash_level";

        private static readonly Color32 ArrowColor = new Color32(255, 60, 30, 255);
        private static readonly Color FlashColor = new Color(1f, 0.15f, 0.1f, 1f);

        // 0..1 user-facing scalar on the vignette intensity. Persisted via PlayerPrefs so the
        // settings menu can write it directly without needing a reference to the live instance.
        private static float flashLevel = -1f; // sentinel — lazy-loaded
        public static float FlashLevel
        {
            get
            {
                if (flashLevel < 0f)
                    flashLevel = Mathf.Clamp01(PlayerPrefs.GetFloat(FlashLevelPrefKey, 0.18f));
                return flashLevel;
            }
            set
            {
                flashLevel = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(FlashLevelPrefKey, flashLevel);
                PlayerPrefs.Save();
            }
        }

        private struct Indicator
        {
            public float worldX, worldZ, timeRemaining;
            public RectTransform rt;
            public Image image;
        }

        private readonly List<Indicator> indicators = new List<Indicator>();
        private Canvas canvas;
        private Camera mainCamera;
        private RTSCameraController cameraController;
        private UnitSelectionManager selectionManager;
        private GameSimulation subscribedSim;
        private Sprite arrowSprite;
        private Image vignetteImage;
        private float flashIntensity; // 0..1, decays each frame

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject(nameof(PeripheralAttackIndicators));
            DontDestroyOnLoad(go);
            go.AddComponent<PeripheralAttackIndicators>();
        }

        private void Awake()
        {
            mainCamera = Camera.main;
            BuildIndicatorCanvas();
            arrowSprite = BuildTriangleSprite();
            StartCoroutine(WaitAndSubscribe());
        }

        private System.Collections.IEnumerator WaitAndSubscribe()
        {
            while (true)
            {
                var sim = GameBootstrapper.Instance?.Simulation;
                if (sim != null && sim != subscribedSim)
                {
                    if (subscribedSim != null) subscribedSim.OnEntityDamaged -= HandleEntityDamaged;
                    subscribedSim = sim;
                    subscribedSim.OnEntityDamaged += HandleEntityDamaged;
                }
                yield return null;
            }
        }

        private void OnDestroy()
        {
            if (subscribedSim != null) subscribedSim.OnEntityDamaged -= HandleEntityDamaged;
        }

        private void BuildIndicatorCanvas()
        {
            var canvasGO = new GameObject("PeripheralIndicatorsCanvas");
            canvasGO.transform.SetParent(transform, false);
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Below the minimap (sortingOrder 10) so the minimap stays clickable on top.
            canvas.sortingOrder = 9;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            // Vignette goes in first so it draws under the arrows. Stretches to fill the canvas.
            var vignetteGO = new GameObject("EdgeFlashVignette");
            vignetteGO.transform.SetParent(canvasGO.transform, false);
            var vrt = vignetteGO.AddComponent<RectTransform>();
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero;
            vrt.offsetMax = Vector2.zero;
            vignetteImage = vignetteGO.AddComponent<Image>();
            vignetteImage.sprite = BuildVignetteSprite();
            vignetteImage.raycastTarget = false;
            vignetteImage.color = new Color(FlashColor.r, FlashColor.g, FlashColor.b, 0f);
            vignetteImage.enabled = false;
        }

        private void HandleEntityDamaged(float wx, float wz, int ownerPlayerId)
        {
            if (ownerPlayerId != ResolveLocalPlayerId()) return;

            // Every hit re-triggers the flash, so sustained damage keeps the vignette visible.
            flashIntensity = 1f;

            // Proximity dedup: if an indicator for this battle already exists, refresh its timer.
            for (int i = 0; i < indicators.Count; i++)
            {
                var existing = indicators[i];
                float dx = existing.worldX - wx;
                float dz = existing.worldZ - wz;
                if (dx * dx + dz * dz < ProximitySq)
                {
                    existing.timeRemaining = Lifetime;
                    indicators[i] = existing;
                    return;
                }
            }
            indicators.Add(CreateIndicator(wx, wz));
        }

        private int ResolveLocalPlayerId()
        {
            if (selectionManager == null) selectionManager = FindFirstObjectByType<UnitSelectionManager>();
            return selectionManager != null ? selectionManager.LocalPlayerId : 0;
        }

        private Indicator CreateIndicator(float wx, float wz)
        {
            var go = new GameObject("PeripheralIndicator");
            go.transform.SetParent(canvas.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(ArrowSize, ArrowSize);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var img = go.AddComponent<Image>();
            img.sprite = arrowSprite;
            img.color = ArrowColor;
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            float capturedX = wx;
            float capturedZ = wz;
            btn.onClick.AddListener(() =>
            {
                if (cameraController == null) cameraController = FindFirstObjectByType<RTSCameraController>();
                if (cameraController != null)
                    cameraController.PivotPosition = new Vector3(capturedX, 0f, capturedZ);
                RemoveIndicatorAt(capturedX, capturedZ);
            });

            return new Indicator
            {
                worldX = wx,
                worldZ = wz,
                timeRemaining = Lifetime,
                rt = rt,
                image = img,
            };
        }

        private void RemoveIndicatorAt(float wx, float wz)
        {
            for (int i = indicators.Count - 1; i >= 0; i--)
            {
                if (indicators[i].worldX == wx && indicators[i].worldZ == wz)
                {
                    if (indicators[i].rt != null) Destroy(indicators[i].rt.gameObject);
                    indicators.RemoveAt(i);
                    return;
                }
            }
        }

        private void Update()
        {
            UpdateVignette();

            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null || indicators.Count == 0) return;

            float halfW = Screen.width * 0.5f;
            float halfH = Screen.height * 0.5f;
            float minX = EdgeMargin;
            float minY = EdgeMargin;
            float maxX = Screen.width - EdgeMargin;
            float maxY = Screen.height - EdgeMargin;

            for (int i = indicators.Count - 1; i >= 0; i--)
            {
                var ind = indicators[i];
                ind.timeRemaining -= Time.deltaTime;
                if (ind.timeRemaining <= 0f)
                {
                    if (ind.rt != null) Destroy(ind.rt.gameObject);
                    indicators.RemoveAt(i);
                    continue;
                }

                Vector3 sp = mainCamera.WorldToScreenPoint(new Vector3(ind.worldX, 0f, ind.worldZ));
                bool behind = sp.z < 0f;
                if (behind)
                {
                    sp.x = Screen.width - sp.x;
                    sp.y = Screen.height - sp.y;
                }

                bool onScreen = !behind && sp.x >= minX && sp.x <= maxX && sp.y >= minY && sp.y <= maxY;
                if (onScreen)
                {
                    // Battle is in view — hide the arrow but keep the timer running so the
                    // same event doesn't immediately re-spawn one if the camera pans away.
                    ind.image.enabled = false;
                    indicators[i] = ind;
                    continue;
                }
                ind.image.enabled = true;

                float dirX = sp.x - halfW;
                float dirY = sp.y - halfH;
                if (dirX == 0f && dirY == 0f) { indicators[i] = ind; continue; }

                float scaleX = dirX > 0f ? (maxX - halfW) / dirX
                              : dirX < 0f ? (minX - halfW) / dirX
                              : float.PositiveInfinity;
                float scaleY = dirY > 0f ? (maxY - halfH) / dirY
                              : dirY < 0f ? (minY - halfH) / dirY
                              : float.PositiveInfinity;
                float t = Mathf.Min(scaleX, scaleY);
                float clampedX = halfW + dirX * t;
                float clampedY = halfH + dirY * t;

                ind.rt.position = new Vector3(clampedX, clampedY, 0f);

                float angle = Mathf.Atan2(dirY, dirX) * Mathf.Rad2Deg;
                ind.rt.localRotation = Quaternion.Euler(0f, 0f, angle);

                float pulse = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * PulseRate * Mathf.PI));
                var c = ind.image.color;
                c.a = pulse;
                ind.image.color = c;

                indicators[i] = ind;
            }
        }

        private void UpdateVignette()
        {
            if (vignetteImage == null) return;
            float effectiveAlpha = flashIntensity * FlashMaxAlpha * FlashLevel;
            if (effectiveAlpha <= 0.001f)
            {
                if (vignetteImage.enabled) vignetteImage.enabled = false;
                if (flashIntensity > 0f)
                    flashIntensity = Mathf.Max(0f, flashIntensity - Time.deltaTime / FlashDuration);
                return;
            }
            flashIntensity = Mathf.Max(0f, flashIntensity - Time.deltaTime / FlashDuration);
            vignetteImage.enabled = true;
            vignetteImage.color = new Color(FlashColor.r, FlashColor.g, FlashColor.b, effectiveAlpha);
        }

        // Radial gradient: clear in the center, ramps to opaque red at the corners. Stretching
        // the square sprite across a 16:9 screen warps it into an oval, which still reads as a
        // believable "edge tint" without needing a screen-space shader.
        private static Sprite BuildVignetteSprite()
        {
            const int size = 256;
            const float innerNorm = 0.45f; // start of the ramp (clear inside)
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            float center = size * 0.5f;
            float maxDist = Mathf.Sqrt(2f) * center; // corner distance
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - center;
                    float dy = y + 0.5f - center;
                    float distNorm = Mathf.Sqrt(dx * dx + dy * dy) / maxDist;
                    float t = Mathf.Clamp01((distNorm - innerNorm) / (1f - innerNorm));
                    byte a = (byte)(t * t * 255f); // squared for a softer ramp
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        // Right-pointing isoceles triangle (tip at +X). RectTransform.localRotation around Z
        // then aligns the tip with atan2(dy, dx).
        private static Sprite BuildTriangleSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            Vector2 a = new Vector2(size - 2f, size * 0.5f);
            Vector2 b = new Vector2(2f, 4f);
            Vector2 c = new Vector2(2f, size - 4f);
            Color32 fill = new Color32(255, 255, 255, 255);
            Color32 clear = new Color32(255, 255, 255, 0);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float s1 = Sign(p, a, b);
                    float s2 = Sign(p, b, c);
                    float s3 = Sign(p, c, a);
                    bool inside = (s1 >= 0f && s2 >= 0f && s3 >= 0f) || (s1 <= 0f && s2 <= 0f && s3 <= 0f);
                    pixels[y * size + x] = inside ? fill : clear;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static float Sign(Vector2 p, Vector2 q, Vector2 r)
        {
            return (p.x - r.x) * (q.y - r.y) - (q.x - r.x) * (p.y - r.y);
        }
    }
}
