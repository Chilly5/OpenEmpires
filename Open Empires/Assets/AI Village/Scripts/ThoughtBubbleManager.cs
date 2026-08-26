using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires.Village
{
    /// <summary>
    /// View-only speech/thought bubbles above villagers showing what they're doing (from
    /// <see cref="VillagerProfile.Thought"/>). Bubbles follow the villager in the world and,
    /// when they're inside a building, hover over their mini avatar on the building card.
    /// Rendered with the overlay shaders so they're always on top.
    /// </summary>
    [DefaultExecutionOrder(600)]
    public class ThoughtBubbleManager : MonoBehaviour
    {
        [SerializeField] private float heightAboveUnit = 2.0f;
        [Tooltip("Gap between a building card's top edge and its grouped activity bubbles.")]
        [SerializeField] private float heightAboveCard = 0.15f;
        [SerializeField] private float textSize = 0.045f;
        [SerializeField] private Color bubbleColor = Color.white;
        [SerializeField] private Color textColor = new Color(0.12f, 0.10f, 0.08f, 1f);

        private class Bubble
        {
            public GameObject Root;
            public Transform Back;
            public TextMesh Text;
            public MeshRenderer TextRenderer;
            public string Shown;
        }

        // Keys: unitId for villagers in the world; -(buildingId * 16 + slot + 1) for grouped building bubbles.
        private readonly Dictionary<int, Bubble> bubbles = new Dictionary<int, Bubble>();
        private readonly List<int> toRemove = new List<int>();
        private readonly HashSet<int> usedKeys = new HashSet<int>();
        private readonly Dictionary<int, List<(string thought, int count)>> groups = new Dictionary<int, List<(string, int)>>();
        private readonly Dictionary<int, UnitView> viewCache = new Dictionary<int, UnitView>();

        private Material bubbleMaterial, textMaterial;
        private Font font;
        private Camera cam;
        private GarrisonIndicatorManager indicators;
        private int viewCacheFrame = -1;

        private void Start()
        {
            indicators = FindFirstObjectByType<GarrisonIndicatorManager>();
            BuildMaterials();
        }

        private void OnDestroy() { Font.textureRebuilt -= OnFontRebuilt; }

        private void BuildMaterials()
        {
            var markerShader = Shader.Find("AIVillage/OverlayMarker");
            var textShader = Shader.Find("AIVillage/OverlayText");
            if (markerShader == null || textShader == null) return;

            // Rounded bubble with a little tail at the bottom-centre and a dark outline.
            const int S = 96;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S, v = (y + 0.5f) / S;
                    // body: rounded rect occupying v in [0.18, 1]
                    float bx = Mathf.Abs(u - 0.5f) - (0.5f - 0.02f - 0.16f);
                    float by = Mathf.Abs(v - 0.59f) - (0.41f - 0.02f - 0.16f);
                    float body = new Vector2(Mathf.Max(bx, 0f), Mathf.Max(by, 0f)).magnitude + Mathf.Min(Mathf.Max(bx, by), 0f) - 0.16f;
                    // tail: a small, narrow triangle just under the body (tip at v≈0.09)
                    float tail = Mathf.Max(Mathf.Abs(u - 0.5f) * 5f - (v - 0.09f), 0.09f - v);
                    float d = Mathf.Min(body, tail) * S;
                    float a = Mathf.Clamp01(-d + 1f);
                    float edge = Mathf.Clamp01(2.5f - Mathf.Abs(d)) * a;
                    Color c = Color.Lerp(bubbleColor, new Color(0.15f, 0.12f, 0.08f), edge * 0.85f);
                    c.a = a;
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();

            // Bubble and text share ONE render queue so Unity sorts all bubble parts back-to-front by
            // distance: a nearer bubble (background + text) is drawn after a farther one and covers it.
            // (With separate queues every background drew first and every text last, so text bled through.)
            int bubbleQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay + 20;
            bubbleMaterial = new Material(markerShader) { mainTexture = tex };
            bubbleMaterial.renderQueue = bubbleQueue;

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            textMaterial = new Material(textShader) { mainTexture = font != null ? font.material.mainTexture : null };
            textMaterial.SetColor("_Color", textColor);
            textMaterial.renderQueue = bubbleQueue;
            Font.textureRebuilt += OnFontRebuilt;
        }

        private void OnFontRebuilt(Font f)
        {
            if (f == font && textMaterial != null) textMaterial.mainTexture = f.material.mainTexture;
        }

        private void LateUpdate()
        {
            var village = VillageBootstrapper.Instance;
            var sim = GameBootstrapper.Instance?.Simulation;
            if (village == null || village.Routine == null || sim == null || bubbleMaterial == null) return;
            if (cam == null) cam = Camera.main;
            if (indicators == null) indicators = FindFirstObjectByType<GarrisonIndicatorManager>();

            if (Time.frameCount - viewCacheFrame > 30)
            {
                viewCacheFrame = Time.frameCount;
                viewCache.Clear();
                foreach (var v in FindObjectsByType<UnitView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    viewCache[v.UnitId] = v;
            }

            var rot = cam != null ? Quaternion.LookRotation(cam.transform.forward, cam.transform.up) : Quaternion.identity;
            float bob = Mathf.Sin(Time.time * 2.5f) * 0.05f;

            usedKeys.Clear();
            foreach (var g in groups.Values) g.Clear();

            var profiles = village.Routine.Profiles;
            for (int i = 0; i < profiles.Count; i++)
            {
                var p = profiles[i];
                string thought = p.IsDead ? "" : p.Thought;
                if (string.IsNullOrEmpty(thought)) continue;

                var unit = sim.UnitRegistry.GetUnit(p.UnitId);
                if (unit == null)
                {
                    // Inside a building: pool into that building's group instead of a bubble per villager.
                    int buildingId = FindBuildingContaining(sim, p.UnitId);
                    if (buildingId < 0) continue;
                    if (!groups.TryGetValue(buildingId, out var list)) { list = new List<(string, int)>(); groups[buildingId] = list; }
                    int idx = list.FindIndex(e => e.thought == thought);
                    if (idx >= 0) list[idx] = (thought, list[idx].count + 1); else list.Add((thought, 1));
                    continue;
                }

                viewCache.TryGetValue(p.UnitId, out var view);
                Vector3 pos;
                if (view != null && view.gameObject.activeInHierarchy) pos = view.transform.position;
                else { pos = unit.SimPosition.ToVector3(); pos.y = sim.MapData.SampleHeight(pos.x, pos.z) * sim.Config.TerrainHeightScale; }
                pos += Vector3.up * (heightAboveUnit * view_scale(view));

                Show(p.UnitId, thought, pos + Vector3.up * bob, rot);
            }

            // Stables: show how many horses are inside (they aren't garrisoned units).
            if (village.Routine.StablesBuildingId >= 0 && village.Routine.StablesHorses > 0)
            {
                var stables = sim.BuildingRegistry.GetBuilding(village.Routine.StablesBuildingId);
                if (stables != null)
                {
                    Vector3 top;
                    if (indicators == null || !indicators.TryGetCardTop(stables.Id, out top))
                    {
                        top = stables.SimPosition.ToVector3();
                        top.y = sim.MapData.SampleHeight(top.x, top.z) * sim.Config.TerrainHeightScale + 4.2f;
                    }
                    Show(-(stables.Id * 16 + 15), $"Horses ×{village.Routine.StablesHorses}", top + Vector3.up * (heightAboveCard + bob * 0.5f), rot);
                }
            }

            // Grouped bubbles above each occupied building's card: one per distinct activity, stacked.
            if (indicators != null)
            {
                foreach (var kv in groups)
                {
                    var list = kv.Value;
                    if (list.Count == 0 || !indicators.TryGetCardTop(kv.Key, out var top)) continue;
                    list.Sort((a, b) => b.count.CompareTo(a.count));
                    float y = heightAboveCard;
                    if (kv.Key == village.Routine.StablesBuildingId && village.Routine.StablesHorses > 0) y += 0.7f; // leave room for the horse count
                    for (int s = 0; s < list.Count && s < 4; s++)
                    {
                        string label = list[s].count > 1 ? $"{list[s].thought} ×{list[s].count}" : list[s].thought;
                        int key = -(kv.Key * 16 + s + 1);
                        float h = Show(key, label, top + Vector3.up * (y + bob * 0.5f), rot);
                        y += h + 0.06f;
                    }
                }
            }

            // Hide anything not refreshed this frame; drop bubbles whose owner vanished.
            toRemove.Clear();
            foreach (var kv in bubbles)
            {
                if (usedKeys.Contains(kv.Key)) continue;
                if (kv.Key >= 0 && village.Routine.GetProfile(kv.Key) == null) { toRemove.Add(kv.Key); continue; }
                if (kv.Value.Root.activeSelf) kv.Value.Root.SetActive(false);
            }
            foreach (var id in toRemove) { Destroy(bubbles[id].Root); bubbles.Remove(id); }
        }

        private static float view_scale(UnitView view) => view != null ? Mathf.Max(0.4f, view.transform.localScale.y) : 1f;

        private static int FindBuildingContaining(GameSimulation sim, int unitId)
        {
            var buildings = sim.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
                if (buildings[i].GarrisonedUnitIds.Contains(unitId)) return buildings[i].Id;
            return -1;
        }

        /// <summary>Show/update one bubble; returns its height so callers can stack them.</summary>
        private float Show(int key, string text, Vector3 pos, Quaternion rot)
        {
            usedKeys.Add(key);
            var b = GetOrCreate(key);
            if (b.Shown != text) { b.Shown = text; b.Text.text = text; }
            // TextMesh re-applies the font's own material whenever its text changes; force ours.
            if (b.TextRenderer.sharedMaterial != textMaterial) b.TextRenderer.sharedMaterial = textMaterial;
            b.Root.transform.position = pos;
            b.Root.transform.rotation = rot;
            // Size the bubble from the text length (renderer bounds lag a frame behind text changes).
            float glyphH = b.Text.fontSize * b.Text.characterSize * 0.1f;
            float textW = text.Length * glyphH * 0.58f;
            float w = Mathf.Max(glyphH * 2.2f, textW + glyphH * 1.2f);
            float h = glyphH * 2.1f;
            b.Back.localScale = new Vector3(w, h, 1f);
            // Text sits clearly in front of its own background so the distance sort keeps them in order.
            b.Back.localPosition = new Vector3(0f, h * 0.5f - glyphH * 0.05f, 0.06f);
            b.Text.transform.localPosition = new Vector3(0f, h * 0.58f, -0.06f);
            if (!b.Root.activeSelf) b.Root.SetActive(true);
            return h;
        }

        private Bubble GetOrCreate(int key)
        {
            if (bubbles.TryGetValue(key, out var b)) return b;

            var root = new GameObject(key >= 0 ? $"Thought_{key}" : $"ThoughtGroup_{-key}");
            root.transform.SetParent(transform, false);

            var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            back.name = "Bubble";
            Destroy(back.GetComponent<Collider>());
            back.transform.SetParent(root.transform, false);
            back.transform.localPosition = new Vector3(0f, 0.22f, 0.01f); // tail tip sits at the root
            var br = back.GetComponent<MeshRenderer>();
            br.sharedMaterial = bubbleMaterial;
            br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(root.transform, false);
            textGO.transform.localPosition = new Vector3(0f, 0.29f, 0f);
            var tm = textGO.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = textSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white; // actual colour comes from the overlay text material
            tm.fontStyle = FontStyle.Bold;
            var tr = textGO.GetComponent<MeshRenderer>();
            tr.sharedMaterial = textMaterial;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            b = new Bubble { Root = root, Back = back.transform, Text = tm, TextRenderer = tr };
            bubbles[key] = b;
            return b;
        }
    }
}
