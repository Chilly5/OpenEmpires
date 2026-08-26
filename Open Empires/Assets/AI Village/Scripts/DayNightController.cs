using UnityEngine;

namespace OpenEmpires.Village
{
    /// <summary>
    /// View-only day/night cycle driven by the village clock. Rotates the sun, fades its
    /// intensity/colour, and tints ambient light and aerial fog. Never touches the sim.
    /// </summary>
    public class DayNightController : MonoBehaviour
    {
        [SerializeField] private Light sun;
        [SerializeField] private AtmosphereController atmosphere;

        [Header("Night")]
        [SerializeField] private float nightIntensity = 0.45f;
        [SerializeField] private Color nightLightColor = new Color(0.55f, 0.65f, 1f);
        [SerializeField] private Color nightFogColor = new Color(0.10f, 0.13f, 0.26f);
        [SerializeField] private float nightAmbientIntensity = 0.5f;
        [SerializeField, Range(0f, 90f)] private float moonElevation = 45f;
        [Tooltip("Tint applied to unlit billboards (trees, bushes, mines) at night.")]
        [SerializeField] private Color nightBillboardTint = new Color(0.30f, 0.36f, 0.55f);

        private readonly System.Collections.Generic.List<Material> billboardMats = new System.Collections.Generic.List<Material>();
        private readonly System.Collections.Generic.List<Color> billboardBaseColors = new System.Collections.Generic.List<Color>();
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private bool billboardsCollected;

        // Resource nodes (gold/stone/berries) use PROJECT ASSET materials shared by their prefabs.
        // Never tint those in place (edits persist into the .mat files in the editor); use
        // per-renderer property blocks instead.
        private MapRenderer mapRenderer;
        private MaterialPropertyBlock resourceBlock; // created lazily (not allowed in a MonoBehaviour field initializer)
        private readonly System.Collections.Generic.List<Renderer> resourceRenderers = new System.Collections.Generic.List<Renderer>();
        private int resourceRendererRefreshFrame = -1;

        /// <summary>Runtime-created materials other systems want tinted with the day/night cycle.</summary>
        private static readonly System.Collections.Generic.List<Material> extraRuntimeMats = new System.Collections.Generic.List<Material>();
        public static void RegisterRuntimeMaterial(Material m) { if (m != null && !extraRuntimeMats.Contains(m)) extraRuntimeMats.Add(m); }

        private void OnDestroy()
        {
            // Restore every material we tinted (all runtime instances, but be safe regardless).
            for (int i = 0; i < billboardMats.Count; i++)
                if (billboardMats[i] != null) billboardMats[i].SetColor(ColorId, billboardBaseColors[i]);
            extraRuntimeMats.Clear();
            RestoreTerrain(); // the terrain material is a runtime instance too, but never leave it tinted
        }

        [Header("Dawn / Dusk")]
        [SerializeField] private Color horizonLightColor = new Color(1f, 0.72f, 0.45f);
        [SerializeField] private Color horizonFogColor = new Color(0.75f, 0.45f, 0.35f);

        private float baseIntensity;
        private Color baseColor;
        private float baseYaw;
        private Color baseFog;
        private Color baseAmbient;
        private float baseAmbientIntensity;
        private bool initialized;

        private void Start()
        {
            if (sun == null)
            {
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { sun = l; break; }
            }
            if (atmosphere == null) atmosphere = FindFirstObjectByType<AtmosphereController>();
            if (sun == null) { enabled = false; return; }

            baseIntensity = sun.intensity;
            baseColor = sun.color;
            baseYaw = sun.transform.eulerAngles.y;
            baseAmbient = RenderSettings.ambientLight;
            baseAmbientIntensity = RenderSettings.ambientIntensity;
            baseFog = atmosphere != null ? atmosphere.BaseAerialFogColor : Color.gray;
            initialized = true;
        }

        private void Update()
        {
            if (!initialized) return;
            var gb = GameBootstrapper.Instance;
            var sim = gb?.Simulation;
            if (sim == null) return;

            float f = VillageClock.DayFraction(sim.CurrentTick, gb.InterpolationAlpha);
            float elevation = Mathf.Sin((f - 0.25f) * Mathf.PI * 2f); // 1 at noon, -1 at midnight
            float daylight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.08f, 0.18f, elevation));
            float horizon = 1f - Mathf.Clamp01(Mathf.Abs(elevation) / 0.35f);

            // Sun path: 0° at 06:00 (east horizon), 90° at noon, 180° at 18:00. At night a fixed "moon".
            float sunPitch = Mathf.Clamp(f * 360f - 90f, 8f, 172f);
            float pitch = Mathf.Lerp(moonElevation, sunPitch, daylight);
            sun.transform.rotation = Quaternion.Euler(pitch, baseYaw, 0f);

            sun.intensity = Mathf.Lerp(nightIntensity, baseIntensity, daylight);
            Color dayCol = Color.Lerp(baseColor, horizonLightColor, horizon * 0.7f);
            sun.color = Color.Lerp(nightLightColor, dayCol, daylight);

            RenderSettings.ambientIntensity = Mathf.Lerp(baseAmbientIntensity * nightAmbientIntensity, baseAmbientIntensity, daylight);
            RenderSettings.ambientLight = Color.Lerp(baseAmbient * nightAmbientIntensity, baseAmbient, daylight);

            if (atmosphere != null)
            {
                Color dayFog = Color.Lerp(baseFog, horizonFogColor, horizon * 0.6f);
                atmosphere.SetAerialFogColor(Color.Lerp(nightFogColor, dayFog, daylight));
            }

            TintBillboards(daylight, horizon);
        }

        // ------------------------------------------------------------------ seasons

        [Header("Seasons")]
        [SerializeField] private Color springFoliage = new Color(0.95f, 1.05f, 0.9f);
        [SerializeField] private Color summerFoliage = new Color(1f, 1f, 0.85f);
        [SerializeField] private Color autumnFoliage = new Color(1.05f, 0.8f, 0.55f);
        [SerializeField] private Color winterFoliage = new Color(0.85f, 0.9f, 1.05f);
        [SerializeField] private Color springGrass = new Color(0.95f, 1.05f, 0.9f);
        [SerializeField] private Color summerGrass = new Color(1f, 0.98f, 0.8f);
        [SerializeField] private Color autumnGrass = new Color(0.95f, 0.85f, 0.6f);
        [SerializeField] private Color winterGrass = new Color(0.95f, 0.98f, 1.05f);

        private static readonly int TintGrassId = Shader.PropertyToID("_TintGrass");
        private Material terrainMaterial;
        private Color terrainBaseTint = Color.white;
        private bool terrainLooked;

        private Color FoliageFor(VillageClock.Season s) => s == VillageClock.Season.Spring ? springFoliage : s == VillageClock.Season.Summer ? summerFoliage : s == VillageClock.Season.Autumn ? autumnFoliage : winterFoliage;
        private Color GrassFor(VillageClock.Season s) => s == VillageClock.Season.Spring ? springGrass : s == VillageClock.Season.Summer ? summerGrass : s == VillageClock.Season.Autumn ? autumnGrass : winterGrass;

        /// <summary>Blend foliage tint toward the season (and the next one as it approaches); tint the terrain grass too.</summary>
        private void ApplySeason(int tick, ref Color tint)
        {
            var s = VillageClock.SeasonOf(tick);
            var next = (VillageClock.Season)(((int)s + 1) % 4);
            float f = VillageClock.SeasonFraction(tick);
            float blend = Mathf.Clamp01((f - 0.7f) / 0.3f); // last 30% of a season eases into the next
            Color foliage = Color.Lerp(FoliageFor(s), FoliageFor(next), blend);
            tint = new Color(tint.r * foliage.r, tint.g * foliage.g, tint.b * foliage.b, tint.a);

            if (!terrainLooked)
            {
                terrainLooked = true;
                foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    var m = r.sharedMaterial;
                    if (m != null && m.shader != null && m.shader.name.Contains("Terrain_Splatmap")) { terrainMaterial = m; break; }
                }
                if (terrainMaterial != null && terrainMaterial.HasProperty(TintGrassId)) terrainBaseTint = terrainMaterial.GetColor(TintGrassId);
            }
            if (terrainMaterial != null && terrainMaterial.HasProperty(TintGrassId))
            {
                Color g = Color.Lerp(GrassFor(s), GrassFor(next), blend);
                terrainMaterial.SetColor(TintGrassId, new Color(terrainBaseTint.r * g.r, terrainBaseTint.g * g.g, terrainBaseTint.b * g.b, terrainBaseTint.a));
            }
        }

        private void RestoreTerrain()
        {
            if (terrainMaterial != null && terrainMaterial.HasProperty(TintGrassId)) terrainMaterial.SetColor(TintGrassId, terrainBaseTint);
        }

        /// <summary>Trees/bushes/mines use an unlit billboard shader, so darken them by tint.</summary>
        private void TintBillboards(float daylight, float horizon)
        {
            if (!billboardsCollected)
            {
                mapRenderer = FindFirstObjectByType<MapRenderer>();
                if (mapRenderer == null || mapRenderer.TreeMaterials == null) return;
                foreach (var m in mapRenderer.TreeMaterials) AddBillboard(m);      // runtime instances
                var setup = FindFirstObjectByType<GameSetup>();
                if (setup != null)
                    foreach (var m in setup.BuildingSpriteMaterials) AddBillboard(m); // runtime instances
                billboardsCollected = true;
            }
            foreach (var m in extraRuntimeMats) AddBillboard(m);

            Color warm = Color.Lerp(Color.white, horizonLightColor, horizon * 0.35f);
            Color tint = Color.Lerp(nightBillboardTint, warm, daylight);
            var simNow = GameBootstrapper.Instance?.Simulation;
            if (simNow != null) ApplySeason(simNow.CurrentTick, ref tint);
            for (int i = 0; i < billboardMats.Count; i++)
            {
                if (billboardMats[i] == null) continue;
                var c = billboardBaseColors[i] * tint;
                billboardMats[i].SetColor(ColorId, c);
                if (billboardMats[i].HasProperty(BaseColorId)) billboardMats[i].SetColor(BaseColorId, c);
            }

            TintResourceNodes(tint);
            TintBuildings(tint);
        }

        // Building sprites are per-instance materials (BuildingView calls .material), so shared-material
        // tinting never reaches them. Tint their renderers with property blocks instead.
        private readonly System.Collections.Generic.List<Renderer> buildingRenderers = new System.Collections.Generic.List<Renderer>();
        private readonly System.Collections.Generic.List<Color> buildingBaseColors = new System.Collections.Generic.List<Color>();
        private int buildingRefreshFrame = -1;
        private MaterialPropertyBlock buildingBlock;

        private void TintBuildings(Color tint)
        {
            if (buildingBlock == null) buildingBlock = new MaterialPropertyBlock();
            if (Time.frameCount - buildingRefreshFrame > 60)
            {
                buildingRefreshFrame = Time.frameCount;
                buildingRenderers.Clear();
                buildingBaseColors.Clear();
                foreach (var view in FindObjectsByType<BuildingView>(FindObjectsSortMode.None))
                    foreach (var r in view.GetComponentsInChildren<Renderer>(true))
                    {
                        var m = r.sharedMaterial;
                        if (m == null || m.shader == null) continue;
                        bool billboard = m.shader.name.Contains("Billboard");
                        if (!billboard && !(r.gameObject.name.StartsWith("CropRow") || r.gameObject.name == "Field" || r.gameObject.name.StartsWith("Headstone"))) continue;
                        buildingRenderers.Add(r);
                        // A property block REPLACES the colour, so remember the material's own colour and multiply.
                        Color baseCol = billboard ? Color.white
                                      : m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId)
                                      : m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.white;
                        buildingBaseColors.Add(baseCol);
                    }
            }
            for (int i = 0; i < buildingRenderers.Count; i++)
            {
                if (buildingRenderers[i] == null) continue;
                buildingBlock.Clear();
                var c = buildingBaseColors[i] * tint;
                buildingBlock.SetColor(ColorId, c);
                buildingBlock.SetColor(BaseColorId, c);
                buildingRenderers[i].SetPropertyBlock(buildingBlock);
            }
        }

        /// <summary>Tint gold/stone/berry node renderers via property blocks (their materials are shared assets).</summary>
        private void TintResourceNodes(Color tint)
        {
            if (mapRenderer == null) return;
            if (resourceBlock == null) resourceBlock = new MaterialPropertyBlock();
            // Nodes come and go (depletion, carcasses); re-collect renderers a few times a second.
            if (Time.frameCount - resourceRendererRefreshFrame > 30)
            {
                resourceRendererRefreshFrame = Time.frameCount;
                resourceRenderers.Clear();
                // Any renderer using one of the resource prefab materials (gold / stone / berry).
                var assetMats = new System.Collections.Generic.HashSet<Material>(mapRenderer.ResourceNodeMaterials);
                foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    if (r.sharedMaterial != null && assetMats.Contains(r.sharedMaterial))
                        resourceRenderers.Add(r);
            }
            resourceBlock.Clear();
            resourceBlock.SetColor(ColorId, tint);
            for (int i = 0; i < resourceRenderers.Count; i++)
                if (resourceRenderers[i] != null) resourceRenderers[i].SetPropertyBlock(resourceBlock);
        }

        private void AddBillboard(Material m)
        {
            if (m == null || !m.HasProperty(ColorId) || billboardMats.Contains(m)) return;
            billboardMats.Add(m);
            billboardBaseColors.Add(m.GetColor(ColorId));
        }
    }
}
