using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires.Village
{
    /// <summary>
    /// View-only: a small card floating just above any building that has villagers inside
    /// (asleep at home, working indoors). Each occupant is drawn as a miniature of the real
    /// villager model standing on the card. Driven by the sim's garrison events; never
    /// touches sim state.
    /// </summary>
    public class GarrisonIndicatorManager : MonoBehaviour
    {
        [Header("Placement")]
        [Tooltip("Height of the card's bottom edge above the ground at the building centre (2x2 buildings).")]
        [SerializeField] private float heightAboveGround = 2.1f;
        [Tooltip("Extra height for 3x3+ buildings, whose roofs are taller.")]
        [SerializeField] private float largeBuildingExtraHeight = 0.9f;
        [SerializeField] private float bobAmplitude = 0.06f;

        [Header("Avatars")]
        [Tooltip("Scale of the miniature villager relative to a real one.")]
        [SerializeField] private float avatarScale = 0.5f;
        [Tooltip("Horizontal gap between villagers on the card (world units at scale 1).")]
        [SerializeField] private float avatarSpacing = 1.0f;

        [Header("Card")]
        [SerializeField] private Color cardColor = new Color(0.96f, 0.92f, 0.80f, 1f);      // parchment, opaque
        [SerializeField] private Color cardBorderColor = new Color(0.45f, 0.33f, 0.18f, 1f); // wood-brown edge
        [SerializeField] private float cardPadding = 0.25f;

        private class Indicator
        {
            public GameObject Root;
            public Transform Card;
            public List<GameObject> Avatars = new List<GameObject>();
            public List<int> AvatarUnitIds = new List<int>();
            public Vector3 BasePos;
        }

        private readonly Dictionary<int, Indicator> indicators = new Dictionary<int, Indicator>();

        /// <summary>World position of the top-centre edge of a building's card (for grouped bubbles).</summary>
        public bool TryGetCardTop(int buildingId, out Vector3 pos)
        {
            if (indicators.TryGetValue(buildingId, out var ind) && ind.Card != null)
            {
                pos = ind.Root.transform.position + Vector3.up * ind.Card.localScale.y;
                return true;
            }
            pos = default;
            return false;
        }

        /// <summary>World position of the mini villager standing for this unit on its building's card (for bubbles/reticles).</summary>
        public bool TryGetAvatarWorldPosition(int unitId, out Vector3 pos)
        {
            foreach (var kv in indicators)
            {
                var ind = kv.Value;
                int i = ind.AvatarUnitIds.IndexOf(unitId);
                if (i >= 0 && i < ind.Avatars.Count) { pos = ind.Avatars[i].transform.position + Vector3.up * avatarHeight * avatarScale; return true; }
            }
            pos = default;
            return false;
        }
        private GameSimulation sim;
        private GameSetup setup;
        private Camera cam;
        [SerializeField] private Color femaleColor = new Color(1f, 0.45f, 0.72f);
        private Material teamMaterial;
        private Material femaleTeamMaterial;
        private Material cardMaterial;
        // UI-style rendering: unlit, unshadowed copies of the villager's materials keyed by source.
        private readonly Dictionary<Material, Material> unlitCache = new Dictionary<Material, Material>();
        private Shader unlitShader;

        /// <summary>Unlit clone of a (lit) material so the card and its villagers ignore scene lighting/shadows.</summary>
        private Material Unlit(Material src, Color? overrideColor)
        {
            if (src == null) return null;
            // UI overlay shader: unlit, no depth test — the card and its villagers always draw on top of
            // world art (building sprites punch depth and would otherwise cover them).
            if (unlitShader == null)
            {
                unlitShader = Shader.Find("AIVillage/OverlayMarker");
                if (unlitShader == null) unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            if (unlitCache.TryGetValue(src, out var m)) return m;
            m = new Material(unlitShader) { name = src.name + " (UI overlay)" };
            Color c = overrideColor ?? (src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor")
                                      : src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white);
            c.a = 1f;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            var tex = src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : src.mainTexture;
            if (tex != null) { if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay + 2; // after the card
            unlitCache[src] = m;
            return m;
        }
        private GameObject avatarPrefab;
        private float avatarHeight = 1.8f; // measured from the prefab

        private void Update()
        {
            if (sim == null)
            {
                sim = GameBootstrapper.Instance?.Simulation;
                if (sim == null) return;
                setup = FindFirstObjectByType<GameSetup>();
                cam = Camera.main;
                sim.OnUnitGarrisoned += OnGarrisoned;
                sim.OnUnitUngarrisoned += OnUngarrisoned;
                sim.OnBuildingDestroyed += OnBuildingDestroyed;
                BuildMaterials();
                RefreshAll();
            }
        }

        private void OnDestroy()
        {
            if (sim == null) return;
            sim.OnUnitGarrisoned -= OnGarrisoned;
            sim.OnUnitUngarrisoned -= OnUngarrisoned;
            sim.OnBuildingDestroyed -= OnBuildingDestroyed;
        }

        // ------------------------------------------------------------------ setup

        private void BuildMaterials()
        {
            avatarPrefab = setup != null ? setup.GetUnitPrefabForType(0) : null;
            if (avatarPrefab != null)
            {
                var r = avatarPrefab.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    Color c = GameSetup.PlayerColors != null && GameSetup.PlayerColors.Length > 0
                        ? GameSetup.PlayerColors[0] : new Color(0.2f, 0.4f, 1f);
                    teamMaterial = Unlit(r.sharedMaterial, c); // flat team blue, unaffected by lighting
                    femaleTeamMaterial = new Material(teamMaterial) { name = "Villager (female, UI overlay)" };
                    if (femaleTeamMaterial.HasProperty("_BaseColor")) femaleTeamMaterial.SetColor("_BaseColor", femaleColor);
                    if (femaleTeamMaterial.HasProperty("_Color")) femaleTeamMaterial.SetColor("_Color", femaleColor);
                }

                // Measure the model so the card can be sized to it.
                var bounds = new Bounds(avatarPrefab.transform.position, Vector3.zero);
                bool any = false;
                foreach (var mr in avatarPrefab.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr.gameObject.name == "SelectionRing") continue;
                    if (!any) { bounds = mr.bounds; any = true; } else bounds.Encapsulate(mr.bounds);
                }
                if (any) avatarHeight = Mathf.Max(0.5f, bounds.max.y - avatarPrefab.transform.position.y);
            }

            // Card: rounded rectangle with a thin light border, transparent unlit.
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float radius = 14f, border = 3f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    // Signed distance to a rounded rect inset by 1px.
                    float px = Mathf.Abs(x + 0.5f - S / 2f) - (S / 2f - 1f - radius);
                    float py = Mathf.Abs(y + 0.5f - S / 2f) - (S / 2f - 1f - radius);
                    float d = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
                    float fill = Mathf.Clamp01(-d);                       // 1 inside, 0 outside (1px AA)
                    float edge = Mathf.Clamp01(border - Mathf.Abs(d)) * fill;
                    Color c = Color.Lerp(cardColor, cardBorderColor, edge);
                    c.a = Mathf.Lerp(cardColor.a, cardBorderColor.a, edge) * fill;
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();

            // Always-on-top overlay (no depth test) so no building sprite can cover the card.
            var shader = Shader.Find("AIVillage/OverlayMarker");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            cardMaterial = new Material(shader) { mainTexture = tex };
            if (cardMaterial.HasProperty("_BaseMap")) cardMaterial.SetTexture("_BaseMap", tex);
            if (cardMaterial.HasProperty("_Color")) cardMaterial.SetColor("_Color", Color.white);
            cardMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay + 1; // card first, villagers after
        }

        // ------------------------------------------------------------------ events

        private void RefreshAll()
        {
            foreach (var b in sim.BuildingRegistry.GetAllBuildings())
                Refresh(b.Id);
        }

        private void OnGarrisoned(int unitId, int buildingId) => Refresh(buildingId);

        // We aren't told which building released the unit; refreshing all ~30 buildings is cheap.
        private void OnUngarrisoned(int unitId) => RefreshAll();

        private void OnBuildingDestroyed(int buildingId)
        {
            if (indicators.TryGetValue(buildingId, out var ind))
            {
                Destroy(ind.Root);
                indicators.Remove(buildingId);
            }
        }

        private void Refresh(int buildingId)
        {
            var b = sim.BuildingRegistry.GetBuilding(buildingId);
            int count = (b != null && !b.IsDestroyed) ? b.GarrisonCount : 0;
            if (count <= 0) { OnBuildingDestroyed(buildingId); return; }

            if (!indicators.TryGetValue(buildingId, out var ind))
            {
                ind = Create(b);
                indicators[buildingId] = ind;
            }

            // One miniature villager per occupant.
            while (ind.Avatars.Count < count) ind.Avatars.Add(CreateAvatar(ind.Root.transform));
            while (ind.Avatars.Count > count)
            {
                Destroy(ind.Avatars[ind.Avatars.Count - 1]);
                ind.Avatars.RemoveAt(ind.Avatars.Count - 1);
            }
            ind.AvatarUnitIds.Clear();
            ind.AvatarUnitIds.AddRange(b.GarrisonedUnitIds);

            // Colour each mini villager by gender (blue men, pink women).
            var routine = VillageBootstrapper.Instance != null ? VillageBootstrapper.Instance.Routine : null;
            for (int i = 0; i < ind.Avatars.Count && i < ind.AvatarUnitIds.Count; i++)
            {
                var profile = routine != null ? routine.GetProfile(ind.AvatarUnitIds[i]) : null;
                var mat = profile != null && profile.Gender == Gender.Female ? femaleTeamMaterial : teamMaterial;
                if (mat == null) continue;
                foreach (var mr in ind.Avatars[i].GetComponentsInChildren<MeshRenderer>())
                    if (IsTeamColoredPart(mr.gameObject.name)) mr.sharedMaterial = mat;
            }

            Layout(ind, count);
        }

        /// <summary>Size the card to its occupants and stand them in a row on its floor.</summary>
        private void Layout(Indicator ind, int count)
        {
            float spacing = avatarSpacing * avatarScale;
            float h = avatarHeight * avatarScale;
            float cardW = count * spacing + cardPadding * 2f;
            float cardH = h + cardPadding * 2f;

            ind.Card.localScale = new Vector3(cardW, cardH, 1f);
            ind.Card.localPosition = new Vector3(0f, cardH * 0.5f, 0.05f); // card centre; bottom edge at root

            for (int i = 0; i < ind.Avatars.Count; i++)
                ind.Avatars[i].transform.localPosition = new Vector3((i - (count - 1) * 0.5f) * spacing, cardPadding, 0f);
        }

        // ------------------------------------------------------------------ construction

        private Indicator Create(BuildingData b)
        {
            var root = new GameObject($"GarrisonIndicator_{b.Id}");
            root.transform.SetParent(transform, false);

            Vector3 pos = b.SimPosition.ToVector3();
            pos.y = sim.MapData.SampleHeight(pos.x, pos.z) * sim.Config.TerrainHeightScale + heightAboveGround
                    + (b.TileFootprintWidth >= 3 ? largeBuildingExtraHeight : 0f);
            root.transform.position = pos;

            var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
            card.name = "Card";
            Destroy(card.GetComponent<Collider>());
            card.transform.SetParent(root.transform, false);
            var cr = card.GetComponent<MeshRenderer>();
            cr.sharedMaterial = cardMaterial;
            cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cr.receiveShadows = false;

            return new Indicator { Root = root, Card = card.transform, BasePos = pos };
        }

        /// <summary>A miniature of the villager model: the prefab's meshes only (no UnitView/animator/colliders).</summary>
        private GameObject CreateAvatar(Transform parent)
        {
            var avatarRoot = new GameObject("Villager");
            avatarRoot.transform.SetParent(parent, false);
            avatarRoot.transform.localScale = Vector3.one * avatarScale;
            if (avatarPrefab == null) return avatarRoot;

            foreach (var src in avatarPrefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (src.gameObject.name == "SelectionRing") continue;
                var srcRenderer = src.GetComponent<MeshRenderer>();
                if (srcRenderer == null || src.sharedMesh == null) continue;

                var part = new GameObject(src.gameObject.name);
                part.transform.SetParent(avatarRoot.transform, false);
                part.transform.localPosition = avatarPrefab.transform.InverseTransformPoint(src.transform.position);
                part.transform.localRotation = Quaternion.Inverse(avatarPrefab.transform.rotation) * src.transform.rotation;
                part.transform.localScale = src.transform.lossyScale;

                part.AddComponent<MeshFilter>().sharedMesh = src.sharedMesh;
                var mr = part.AddComponent<MeshRenderer>();
                // Same rule as GameSetup.SpawnUnit: team-coloured parts take the player colour,
                // everything else keeps its own material — so it looks like a normal villager.
                bool team = IsTeamColoredPart(src.gameObject.name);
                mr.sharedMaterial = (team && teamMaterial != null) ? teamMaterial : (Unlit(srcRenderer.sharedMaterial, null) ?? srcRenderer.sharedMaterial);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            return avatarRoot;
        }

        private static bool IsTeamColoredPart(string partName)
        {
            return partName.EndsWith("_Team", System.StringComparison.Ordinal)
                || partName.StartsWith("Body", System.StringComparison.Ordinal)
                || partName.StartsWith("Sphere", System.StringComparison.Ordinal);
        }

        // ------------------------------------------------------------------ per-frame

        private void LateUpdate()
        {
            if (cam == null) { cam = Camera.main; if (cam == null) return; }
            float bob = Mathf.Sin(Time.time * 2f) * bobAmplitude;
            // Card and villagers stay upright and turn to face the camera (yaw only).
            Vector3 fwd = cam.transform.forward; fwd.y = 0f;
            var yaw = fwd.sqrMagnitude > 0.001f ? Quaternion.LookRotation(fwd.normalized, Vector3.up) : Quaternion.identity;
            foreach (var kv in indicators)
            {
                var ind = kv.Value;
                ind.Root.transform.position = ind.BasePos + Vector3.up * bob;
                ind.Root.transform.rotation = yaw;
            }
        }
    }
}
