using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires.Village
{
    /// <summary>
    /// View-only visuals for the village-scale systems: bodies lying where villagers died
    /// (carried when someone hauls them), wolves (dark, low, fast), and the graveyard's
    /// soil plot and headstones.
    /// </summary>
    [DefaultExecutionOrder(550)]
    public class VillageWorldVisuals : MonoBehaviour
    {
        [SerializeField] private Color corpseColor = new Color(0.45f, 0.45f, 0.5f);
        [SerializeField] private Color wolfColor = new Color(0.22f, 0.2f, 0.2f);
        [SerializeField] private Color soilColor = new Color(0.32f, 0.24f, 0.16f);
        [SerializeField] private Color stoneColor = new Color(0.6f, 0.6f, 0.62f);

        private readonly Dictionary<int, GameObject> corpseObjects = new Dictionary<int, GameObject>();
        private readonly HashSet<int> wolfStyled = new HashSet<int>();
        private readonly Dictionary<int, UnitView> viewCache = new Dictionary<int, UnitView>();
        private int viewCacheFrame = -1;
        private GameObject avatarPrefab;
        private Material corpseMaterial, wolfMaterial, soilMaterial, stoneMaterial;
        private int graveyardDecorated = -1;
        private int headstonesShown;
        private Transform headstoneRoot;
        private Material nameMaterial;
        private readonly List<MeshRenderer> nameLabels = new List<MeshRenderer>();
        private Camera cam;

        private void Start()
        {
            var setup = FindFirstObjectByType<GameSetup>();
            avatarPrefab = setup != null ? setup.GetUnitPrefabForType(0) : null;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Standard");
            corpseMaterial = Make(lit, corpseColor);
            wolfMaterial = Make(lit, wolfColor);
            soilMaterial = Make(lit, soilColor);
            stoneMaterial = Make(lit, stoneColor);
        }

        private static Material Make(Shader shader, Color c)
        {
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
            return m;
        }

        private void LateUpdate()
        {
            var village = VillageBootstrapper.Instance;
            var sim = GameBootstrapper.Instance?.Simulation;
            if (village == null || village.Routine == null || sim == null) return;
            var r = village.Routine;

            if (Time.frameCount - viewCacheFrame > 30)
            {
                viewCacheFrame = Time.frameCount;
                viewCache.Clear();
                foreach (var v in FindObjectsByType<UnitView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    viewCache[v.UnitId] = v;
            }

            UpdateCorpses(sim, r);
            UpdateWolves(sim, r);
            UpdateHorses(sim, r);
            UpdateGear(sim, r);
            UpdateLoads(r);
            UpdateGraveyard(sim, r);
        }

        // ------------------------------------------------------------------ corpses

        private void UpdateCorpses(GameSimulation sim, VillageRoutineSystem r)
        {
            // Create / move
            for (int i = 0; i < r.Corpses.Count; i++)
            {
                var c = r.Corpses[i];
                if (!corpseObjects.TryGetValue(c.Id, out var go))
                {
                    go = BuildLyingVillager($"Corpse_{c.Name}");
                    corpseObjects[c.Id] = go;
                }
                Vector3 pos;
                bool carried = c.CarrierId >= 0 && viewCache.TryGetValue(c.CarrierId, out var carrier) && carrier != null
                               && carrier.gameObject.activeInHierarchy && r.GetProfile(c.CarrierId) != null && r.GetProfile(c.CarrierId).CarryingLoad;
                if (carried)
                {
                    var cv = viewCache[c.CarrierId];
                    pos = cv.transform.position + Vector3.up * 1.15f + cv.transform.forward * 0.25f; // over the shoulder
                    go.transform.rotation = cv.transform.rotation * Quaternion.Euler(90f, 0f, 0f);
                }
                else
                {
                    pos = c.Position.ToVector3();
                    pos.y = sim.MapData.SampleHeight(pos.x, pos.z) * sim.Config.TerrainHeightScale + 0.12f;
                    go.transform.rotation = Quaternion.Euler(90f, c.Id * 47f % 360f, 0f);
                }
                go.transform.position = pos;
            }
            // Remove buried
            var stale = new List<int>();
            foreach (var kv in corpseObjects) if (r.FindCorpse(kv.Key) == null) stale.Add(kv.Key);
            foreach (var id in stale) { Destroy(corpseObjects[id]); corpseObjects.Remove(id); }
        }

        private GameObject BuildLyingVillager(string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(transform, false);
            if (avatarPrefab == null) return root;
            foreach (var src in avatarPrefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (src.gameObject.name == "SelectionRing") continue;
                var srcR = src.GetComponent<MeshRenderer>();
                if (srcR == null || src.sharedMesh == null) continue;
                var part = new GameObject(src.gameObject.name);
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = avatarPrefab.transform.InverseTransformPoint(src.transform.position);
                part.transform.localRotation = Quaternion.Inverse(avatarPrefab.transform.rotation) * src.transform.rotation;
                part.transform.localScale = src.transform.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = src.sharedMesh;
                var mr = part.AddComponent<MeshRenderer>();
                mr.sharedMaterial = corpseMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            return root;
        }

        // ------------------------------------------------------------------ wolves

        private void UpdateWolves(GameSimulation sim, VillageRoutineSystem r)
        {
            for (int i = 0; i < r.WolfIds.Count; i++)
            {
                int id = r.WolfIds[i];
                if (wolfStyled.Contains(id) || !viewCache.TryGetValue(id, out var view) || view == null) continue;
                wolfStyled.Add(id);
                // Hide the placeholder unit model and attach a built wolf.
                foreach (var mr in view.GetComponentsInChildren<Renderer>(true))
                    if (mr.gameObject.name != "SelectionRing") mr.enabled = false;
                BuildWolf(view.transform);
            }
        }

        /// <summary>A low, lean wolf from primitives: body, chest, head with snout and ears, four legs, tail.</summary>
        private void BuildWolf(Transform parent)
        {
            var root = new GameObject("WolfModel").transform;
            root.SetParent(parent, false);
            root.localPosition = Vector3.zero;

            GameObject Part(string name, PrimitiveType type, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
            {
                var g = GameObject.CreatePrimitive(type);
                g.name = name;
                Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(root, false);
                g.transform.localPosition = pos;
                g.transform.localScale = scale;
                g.transform.localRotation = Quaternion.Euler(euler);
                g.GetComponent<MeshRenderer>().sharedMaterial = mat;
                return g;
            }

            var fur = wolfMaterial;
            var dark = Make(wolfMaterial.shader, new Color(0.12f, 0.1f, 0.1f));
            var light = Make(wolfMaterial.shader, new Color(0.45f, 0.42f, 0.4f));

            Part("Body", PrimitiveType.Capsule, new Vector3(0f, 0.55f, -0.05f), new Vector3(0.42f, 0.55f, 0.42f), new Vector3(90f, 0f, 0f), fur);   // horizontal capsule
            Part("Chest", PrimitiveType.Sphere, new Vector3(0f, 0.6f, 0.35f), new Vector3(0.5f, 0.5f, 0.5f), Vector3.zero, fur);
            Part("Belly", PrimitiveType.Sphere, new Vector3(0f, 0.45f, 0.05f), new Vector3(0.36f, 0.3f, 0.6f), Vector3.zero, light);
            Part("Head", PrimitiveType.Sphere, new Vector3(0f, 0.8f, 0.7f), new Vector3(0.34f, 0.3f, 0.34f), Vector3.zero, fur);
            Part("Snout", PrimitiveType.Cube, new Vector3(0f, 0.74f, 0.92f), new Vector3(0.16f, 0.14f, 0.3f), Vector3.zero, fur);
            Part("Nose", PrimitiveType.Sphere, new Vector3(0f, 0.76f, 1.07f), new Vector3(0.08f, 0.07f, 0.08f), Vector3.zero, dark);
            Part("EarL", PrimitiveType.Cube, new Vector3(-0.1f, 0.98f, 0.62f), new Vector3(0.07f, 0.16f, 0.05f), new Vector3(0f, 0f, 15f), fur);
            Part("EarR", PrimitiveType.Cube, new Vector3(0.1f, 0.98f, 0.62f), new Vector3(0.07f, 0.16f, 0.05f), new Vector3(0f, 0f, -15f), fur);
            Part("EyeL", PrimitiveType.Sphere, new Vector3(-0.09f, 0.85f, 0.83f), new Vector3(0.05f, 0.05f, 0.05f), Vector3.zero, Make(wolfMaterial.shader, new Color(1f, 0.85f, 0.2f)));
            Part("EyeR", PrimitiveType.Sphere, new Vector3(0.09f, 0.85f, 0.83f), new Vector3(0.05f, 0.05f, 0.05f), Vector3.zero, Make(wolfMaterial.shader, new Color(1f, 0.85f, 0.2f)));
            Part("LegFL", PrimitiveType.Capsule, new Vector3(-0.14f, 0.25f, 0.32f), new Vector3(0.1f, 0.26f, 0.1f), Vector3.zero, fur);
            Part("LegFR", PrimitiveType.Capsule, new Vector3(0.14f, 0.25f, 0.32f), new Vector3(0.1f, 0.26f, 0.1f), Vector3.zero, fur);
            Part("LegBL", PrimitiveType.Capsule, new Vector3(-0.14f, 0.25f, -0.35f), new Vector3(0.1f, 0.26f, 0.1f), Vector3.zero, fur);
            Part("LegBR", PrimitiveType.Capsule, new Vector3(0.14f, 0.25f, -0.35f), new Vector3(0.1f, 0.26f, 0.1f), Vector3.zero, fur);
            Part("Tail", PrimitiveType.Capsule, new Vector3(0f, 0.62f, -0.75f), new Vector3(0.1f, 0.24f, 0.1f), new Vector3(-35f, 0f, 0f), fur);
        }

        // ------------------------------------------------------------------ horses

        private readonly HashSet<int> horseStyled = new HashSet<int>();
        private Material horseMaterial, maneMaterial, swordMaterial;

        private void UpdateHorses(GameSimulation sim, VillageRoutineSystem r)
        {
            if (horseMaterial == null)
            {
                var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                horseMaterial = Make(lit, new Color(0.45f, 0.3f, 0.18f));
                maneMaterial = Make(lit, new Color(0.15f, 0.1f, 0.07f));
                swordMaterial = Make(lit, new Color(0.8f, 0.82f, 0.85f));
            }
            for (int i = 0; i < r.HorseIds.Count; i++)
            {
                int id = r.HorseIds[i];
                if (horseStyled.Contains(id) || !viewCache.TryGetValue(id, out var view) || view == null) continue;
                horseStyled.Add(id);
                foreach (var mr in view.GetComponentsInChildren<Renderer>(true))
                    if (mr.gameObject.name != "SelectionRing") mr.enabled = false;
                BuildHorse(view.transform, 1f);
            }
        }

        /// <summary>A horse from primitives: barrel body, neck, head, mane, four legs, tail. Facing +Z.</summary>
        private Transform BuildHorse(Transform parent, float scale)
        {
            var root = new GameObject("HorseModel").transform;
            root.SetParent(parent, false);
            root.localScale = Vector3.one * scale;

            GameObject Part(string name, PrimitiveType type, Vector3 pos, Vector3 sc, Vector3 euler, Material mat)
            {
                var g = GameObject.CreatePrimitive(type);
                g.name = name;
                Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(root, false);
                g.transform.localPosition = pos;
                g.transform.localScale = sc;
                g.transform.localRotation = Quaternion.Euler(euler);
                g.GetComponent<MeshRenderer>().sharedMaterial = mat;
                return g;
            }

            Part("Body", PrimitiveType.Capsule, new Vector3(0f, 1.0f, 0f), new Vector3(0.62f, 0.75f, 0.62f), new Vector3(90f, 0f, 0f), horseMaterial);
            Part("Neck", PrimitiveType.Capsule, new Vector3(0f, 1.35f, 0.65f), new Vector3(0.3f, 0.45f, 0.3f), new Vector3(-40f, 0f, 0f), horseMaterial);
            Part("Head", PrimitiveType.Cube, new Vector3(0f, 1.68f, 1.0f), new Vector3(0.24f, 0.26f, 0.48f), new Vector3(20f, 0f, 0f), horseMaterial);
            Part("Mane", PrimitiveType.Cube, new Vector3(0f, 1.62f, 0.55f), new Vector3(0.1f, 0.42f, 0.5f), new Vector3(-40f, 0f, 0f), maneMaterial);
            Part("EarL", PrimitiveType.Cube, new Vector3(-0.08f, 1.88f, 0.85f), new Vector3(0.06f, 0.14f, 0.05f), Vector3.zero, horseMaterial);
            Part("EarR", PrimitiveType.Cube, new Vector3(0.08f, 1.88f, 0.85f), new Vector3(0.06f, 0.14f, 0.05f), Vector3.zero, horseMaterial);
            Part("LegFL", PrimitiveType.Capsule, new Vector3(-0.2f, 0.45f, 0.45f), new Vector3(0.13f, 0.5f, 0.13f), Vector3.zero, horseMaterial);
            Part("LegFR", PrimitiveType.Capsule, new Vector3(0.2f, 0.45f, 0.45f), new Vector3(0.13f, 0.5f, 0.13f), Vector3.zero, horseMaterial);
            Part("LegBL", PrimitiveType.Capsule, new Vector3(-0.2f, 0.45f, -0.5f), new Vector3(0.13f, 0.5f, 0.13f), Vector3.zero, horseMaterial);
            Part("LegBR", PrimitiveType.Capsule, new Vector3(0.2f, 0.45f, -0.5f), new Vector3(0.13f, 0.5f, 0.13f), Vector3.zero, horseMaterial);
            Part("Tail", PrimitiveType.Capsule, new Vector3(0f, 1.05f, -0.95f), new Vector3(0.1f, 0.35f, 0.1f), new Vector3(-60f, 0f, 0f), maneMaterial);
            return root;
        }

        // ------------------------------------------------------------------ swords & mounts

        private readonly Dictionary<int, GameObject> swords = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, Transform> mounts = new Dictionary<int, Transform>();
        private readonly Dictionary<int, (MilitaryKind kind, GameObject model)> militaryModels = new Dictionary<int, (MilitaryKind, GameObject)>();
        private GameSetup setupRef;

        /// <summary>Soldiers, archers and knights wear the real Open Empires unit models instead of the villager one.</summary>
        private void UpdateMilitaryModel(VillagerProfile p, UnitView view)
        {
            var wanted = p.Mounted ? MilitaryKind.Knight : (p.Military == MilitaryKind.Soldier || p.Military == MilitaryKind.Archer ? p.Military : MilitaryKind.None);
            militaryModels.TryGetValue(p.UnitId, out var current);
            if (current.kind == wanted) return;

            var pivot = view.transform.Find("ModelPivot");
            var parent = pivot != null ? pivot : view.transform;
            if (current.model != null) Destroy(current.model);
            // Show / hide the villager's own parts (everything under the pivot that isn't ours).
            foreach (var mr in parent.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.transform == parent || mr.gameObject.name == "Sword" || mr.transform.parent == null) continue;
                if (mr.transform.parent.name == "MilitaryModel" || mr.transform.IsChildOf(parent) && mr.transform.parent.name == "Mount") continue;
                mr.enabled = wanted == MilitaryKind.None;
            }
            GameObject model = null;
            if (wanted != MilitaryKind.None)
            {
                if (setupRef == null) setupRef = FindFirstObjectByType<GameSetup>();
                int unitType = wanted == MilitaryKind.Knight ? 7 : wanted == MilitaryKind.Archer ? 2 : 1;
                var prefab = setupRef != null ? setupRef.GetUnitPrefabForType(unitType) : null;
                if (prefab != null)
                {
                    model = new GameObject("MilitaryModel");
                    model.transform.SetParent(parent, false);
                    // Borrow the villager's team material so colours match (blue / pink).
                    Material team = null;
                    foreach (var mr in view.GetComponentsInChildren<MeshRenderer>(true))
                        if (mr.gameObject.name.StartsWith("Body") && mr.sharedMaterials.Length > 0) { team = mr.sharedMaterials[0]; break; }
                    foreach (var src in prefab.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (src.gameObject.name == "SelectionRing" || src.sharedMesh == null) continue;
                        var srcR = src.GetComponent<MeshRenderer>();
                        if (srcR == null) continue;
                        var part = new GameObject(src.gameObject.name);
                        part.transform.SetParent(model.transform, false);
                        part.transform.localPosition = prefab.transform.InverseTransformPoint(src.transform.position);
                        part.transform.localRotation = Quaternion.Inverse(prefab.transform.rotation) * src.transform.rotation;
                        part.transform.localScale = src.transform.lossyScale;
                        part.AddComponent<MeshFilter>().sharedMesh = src.sharedMesh;
                        var mr = part.AddComponent<MeshRenderer>();
                        string n = src.gameObject.name;
                        bool teamPart = n.EndsWith("_Team", System.StringComparison.Ordinal) || n.StartsWith("Body", System.StringComparison.Ordinal) || n.StartsWith("Sphere", System.StringComparison.Ordinal);
                        mr.sharedMaterial = teamPart && team != null ? team : srcR.sharedMaterial;
                    }
                }
            }
            militaryModels[p.UnitId] = (wanted, model);
        }

        private void UpdateGear(GameSimulation sim, VillageRoutineSystem r)
        {
            var profiles = r.Profiles;
            for (int i = 0; i < profiles.Count; i++)
            {
                var p = profiles[i];
                UnitView view = null;
                bool hasView = !p.IsDead && viewCache.TryGetValue(p.UnitId, out view) && view != null;
                if (hasView) UpdateMilitaryModel(p, view);

                // Sword: a thin blade at the hip for militia (the real unit models carry their own weapons).
                if (hasView && p.Armed && p.Military == MilitaryKind.Militia && !p.Mounted)
                {
                    if (!swords.TryGetValue(p.UnitId, out var sword) || sword == null)
                    {
                        sword = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        sword.name = "Sword";
                        Destroy(sword.GetComponent<Collider>());
                        var pivot = view.transform.Find("ModelPivot");
                        sword.transform.SetParent(pivot != null ? pivot : view.transform, false);
                        sword.transform.localPosition = new Vector3(0.32f, 0.9f, -0.05f);
                        sword.transform.localRotation = Quaternion.Euler(25f, 0f, 10f);
                        sword.transform.localScale = new Vector3(0.05f, 0.75f, 0.12f);
                        sword.GetComponent<MeshRenderer>().sharedMaterial = swordMaterial ?? horseMaterial;
                        swords[p.UnitId] = sword;
                    }
                }
                else if (swords.TryGetValue(p.UnitId, out var s2) && s2 != null) { Destroy(s2); swords.Remove(p.UnitId); }

                // Mount: the knight prefab already includes its horse, so only militia riders get the primitive horse.
                if (hasView && p.Mounted && !militaryModels.ContainsKey(p.UnitId))
                {
                    if (!mounts.TryGetValue(p.UnitId, out var m) || m == null)
                    {
                        m = BuildHorse(view.transform, 0.9f);
                        m.name = "Mount";
                        mounts[p.UnitId] = m;
                    }
                }
                else if (mounts.TryGetValue(p.UnitId, out var m2) && m2 != null) { Destroy(m2.gameObject); mounts.Remove(p.UnitId); }
            }
        }

        // ------------------------------------------------------------------ carried loads

        private readonly Dictionary<int, GameObject> loads = new Dictionary<int, GameObject>();

        private void UpdateLoads(VillageRoutineSystem r)
        {
            var profiles = r.Profiles;
            for (int i = 0; i < profiles.Count; i++)
            {
                var p = profiles[i];
                bool carrying = !p.IsDead && p.Errand == Errand.Haul && p.CarryingLoad && viewCache.TryGetValue(p.UnitId, out var view) && view != null && view.gameObject.activeInHierarchy;
                if (carrying)
                {
                    if (!loads.TryGetValue(p.UnitId, out var go) || go == null)
                    {
                        go = BuildTimberBundle();
                        loads[p.UnitId] = go;
                    }
                    var v = viewCache[p.UnitId].transform;
                    go.transform.position = v.position + Vector3.up * 1.25f * v.localScale.y + v.right * 0.25f;
                    go.transform.rotation = v.rotation * Quaternion.Euler(0f, 0f, 12f);
                    if (!go.activeSelf) go.SetActive(true);
                }
                else if (loads.TryGetValue(p.UnitId, out var go2) && go2 != null && go2.activeSelf) go2.SetActive(false);
            }
        }

        private Material timberMaterial;
        private GameObject BuildTimberBundle()
        {
            if (timberMaterial == null) timberMaterial = Make(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"), new Color(0.5f, 0.35f, 0.18f));
            var root = new GameObject("TimberLoad");
            root.transform.SetParent(transform, false);
            for (int i = 0; i < 3; i++)
            {
                var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                log.name = "Log";
                Destroy(log.GetComponent<Collider>());
                log.transform.SetParent(root.transform, false);
                log.transform.localPosition = new Vector3((i - 1) * 0.11f, (i == 1 ? 0.1f : 0f), 0f);
                log.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                log.transform.localScale = new Vector3(0.11f, 0.45f, 0.11f);
                log.GetComponent<MeshRenderer>().sharedMaterial = timberMaterial;
            }
            return root;
        }

        // ------------------------------------------------------------------ graveyard

        private void UpdateGraveyard(GameSimulation sim, VillageRoutineSystem r)
        {
            if (r.GraveyardBuildingId < 0) return;
            if (graveyardDecorated != r.GraveyardBuildingId)
            {
                BuildingView view = null;
                foreach (var v in FindObjectsByType<BuildingView>(FindObjectsSortMode.None))
                    if (v.BuildingId == r.GraveyardBuildingId) { view = v; break; }
                if (view == null) return;
                graveyardDecorated = r.GraveyardBuildingId;
                headstonesShown = 0;
                // Bare earth instead of crops.
                foreach (var mr in view.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr.gameObject.name.StartsWith("CropRow")) mr.enabled = false;
                    else if (mr.gameObject.name == "Field") mr.sharedMaterial = soilMaterial;
                }
                var rootGo = new GameObject("Headstones");
                rootGo.transform.SetParent(view.transform, false);
                headstoneRoot = rootGo.transform;
            }
            if (headstoneRoot == null) return;
            while (headstonesShown < r.Burials.Count)
            {
                int i = headstonesShown++;
                var stone = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stone.name = "Headstone_" + r.Burials[i];
                Destroy(stone.GetComponent<Collider>());
                stone.transform.SetParent(headstoneRoot, false);
                stone.transform.localPosition = new Vector3(-0.7f + (i % 4) * 0.47f, 0.22f, 0.7f - (i / 4) * 0.5f);
                stone.transform.localScale = new Vector3(0.22f, 0.36f, 0.08f);
                stone.GetComponent<MeshRenderer>().sharedMaterial = stoneMaterial;

                // Name plate: a tiny always-on-top label above the stone.
                var label = new GameObject("Name");
                label.transform.SetParent(stone.transform, false);
                label.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                label.transform.localScale = new Vector3(1f / stone.transform.localScale.x, 1f / stone.transform.localScale.y, 1f / stone.transform.localScale.z);
                var tm = label.AddComponent<TextMesh>();
                tm.text = r.Burials[i];
                tm.fontSize = 48;
                tm.characterSize = 0.012f;
                tm.anchor = TextAnchor.LowerCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = Color.white;
                var tr = label.GetComponent<MeshRenderer>();
                if (nameMaterial == null)
                {
                    var sh = Shader.Find("AIVillage/OverlayText");
                    var font = tm.font != null ? tm.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (sh != null && font != null) { nameMaterial = new Material(sh) { mainTexture = font.material.mainTexture }; nameMaterial.SetColor("_Color", new Color(0.9f, 0.9f, 0.85f)); nameMaterial.renderQueue = 4025; }
                }
                if (nameMaterial != null) tr.sharedMaterial = nameMaterial;
                nameLabels.Add(tr);
            }
            // TextMesh re-applies the font material whenever text changes; keep ours.
            foreach (var tr in nameLabels) if (tr != null && nameMaterial != null && tr.sharedMaterial != nameMaterial) tr.sharedMaterial = nameMaterial;
            if (cam == null) cam = Camera.main;
            if (cam != null) foreach (var tr in nameLabels) if (tr != null) tr.transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
        }
    }
}
