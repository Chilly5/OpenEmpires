using System.Reflection;
using UnityEngine;

namespace OpenEmpires.Village
{
    /// <summary>
    /// Click a villager: the camera zooms in and keeps them dead-centre, and a reticle floats
    /// above them (drawn on top of everything). The follow survives the villager entering a
    /// building (the camera holds on that building and the villager is re-selected when they
    /// come back out). Panning with the keyboard / middle mouse, or selecting something else,
    /// releases it.
    /// </summary>
    public class VillageCameraFollow : MonoBehaviour
    {
        [SerializeField] private float followZoom = 6f;
        [Tooltip("Closest the camera may get in this mode (the RTS default is 5).")]
        [SerializeField] private float minZoomDistance = 3f;

        [Header("Reticle")]
        [SerializeField] private Color reticleColor = new Color(1f, 0.9f, 0.35f, 1f);
        [SerializeField] private float reticleSize = 0.7f;
        [SerializeField] private float reticleHeightAboveUnit = 3.0f; // above the thought bubble
        [SerializeField] private float reticleHeightAboveBuilding = 6.5f;

        /// <summary>Unit currently followed / inspected (-1 = none). Stays valid while the villager is inside a building.</summary>
        public int FollowedUnitId { get; private set; } = -1;

        private UnitSelectionManager selection;
        private RTSCameraController cam;
        private GameSetup setup;
        private MethodInfo selectUnitById;
        private int lastSelectedId = -1;
        private bool insideBuilding;

        private Transform reticle;
        private Material reticleMaterial;
        private Camera mainCam;

        private void Start()
        {
            selection = FindFirstObjectByType<UnitSelectionManager>();
            cam = FindFirstObjectByType<RTSCameraController>();
            setup = FindFirstObjectByType<GameSetup>();
            if (cam != null) cam.MinZoomDistance = minZoomDistance;
            selectUnitById = typeof(UnitSelectionManager).GetMethod("SelectUnitById",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            BuildReticle();
        }

        private void LateUpdate()
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (selection == null || cam == null || sim == null) return;

            // --- selection changes ---
            int selectedId = selection.SelectedUnits.Count == 1 ? selection.SelectedUnits[0].UnitId : -1;
            if (selectedId != lastSelectedId)
            {
                lastSelectedId = selectedId;
                if (selectedId >= 0)
                {
                    // New explicit pick.
                    if (selectedId != FollowedUnitId) { FollowedUnitId = selectedId; cam.SetTargetZoom(followZoom); }
                }
                else
                {
                    // The selection manager clears the selection the moment a unit garrisons. Only treat
                    // this as a player deselect if the followed villager is NOT inside a building.
                    bool followedIsInside = FollowedUnitId >= 0
                        && sim.UnitRegistry.GetUnit(FollowedUnitId) == null
                        && sim.UnitRegistry.GetGarrisonedUnit(FollowedUnitId) != null;
                    if (!followedIsInside || selection.SelectedUnits.Count > 1 || selection.SelectedBuildings.Count > 0)
                        FollowedUnitId = -1;
                }
            }

            if (FollowedUnitId < 0) { SetReticleVisible(false); return; }

            // The player took the wheel.
            if (cam.HasManualPanInput) { FollowedUnitId = -1; SetReticleVisible(false); return; }

            // --- where is the villager? ---
            var unit = sim.UnitRegistry.GetUnit(FollowedUnitId);
            Vector3 focus;
            if (unit != null && unit.State != UnitState.Dead)
            {
                if (insideBuilding)
                {
                    // Just came back out: restore the selection ring.
                    insideBuilding = false;
                    if (selection.SelectedUnits.Count == 0 && selectUnitById != null)
                    {
                        selectUnitById.Invoke(selection, new object[] { FollowedUnitId });
                        lastSelectedId = FollowedUnitId;
                    }
                }
                focus = unit.SimPosition.ToVector3();
                // Interpolated view position is smoother than the raw sim position.
                var view = FindView(FollowedUnitId);
                if (view != null && view.gameObject.activeInHierarchy) focus = view.transform.position;
                else focus.y = GroundHeight(sim, focus);
                PlaceReticle(focus + Vector3.up * reticleHeightAboveUnit);
            }
            else if (sim.UnitRegistry.GetGarrisonedUnit(FollowedUnitId) != null)
            {
                insideBuilding = true;
                var building = FindBuildingContaining(sim, FollowedUnitId);
                if (building == null) { SetReticleVisible(false); return; }
                focus = building.SimPosition.ToVector3();
                focus.y = GroundHeight(sim, focus);
                PlaceReticle(focus + Vector3.up * (reticleHeightAboveBuilding + (building.TileFootprintWidth >= 3 ? 1f : 0f)));
            }
            else
            {
                FollowedUnitId = -1; // dead / removed
                SetReticleVisible(false);
                return;
            }

            cam.SnapPivot(focus);
        }

        // ------------------------------------------------------------------ helpers

        private static float GroundHeight(GameSimulation sim, Vector3 p) =>
            sim.MapData.SampleHeight(p.x, p.z) * sim.Config.TerrainHeightScale;

        private UnitView FindView(int unitId)
        {
            foreach (var v in selection.SelectedUnits)
                if (v.UnitId == unitId) return v;
            foreach (var v in FindObjectsByType<UnitView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (v.UnitId == unitId) return v;
            return null;
        }

        private static BuildingData FindBuildingContaining(GameSimulation sim, int unitId)
        {
            var buildings = sim.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
                if (buildings[i].GarrisonedUnitIds.Contains(unitId)) return buildings[i];
            return null;
        }

        // ------------------------------------------------------------------ reticle

        private void BuildReticle()
        {
            var shader = Shader.Find("AIVillage/OverlayMarker");
            if (shader == null) return;

            // Downward chevron with a dark outline.
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S, v = (y + 0.5f) / S;           // v=0 bottom (tip), v=1 top
                    float halfW = Mathf.Lerp(0.04f, 0.42f, v);               // triangle widens upward
                    float dx = Mathf.Abs(u - 0.5f);
                    float inside = halfW - dx;                                // >0 inside triangle
                    float notch = (v - 0.62f) * 1.4f - dx * 0.9f;             // carve a notch in the top → chevron
                    float d = Mathf.Min(inside, -notch) * S;                  // px distance to edge (approx)
                    float a = Mathf.Clamp01(d + 1f);
                    float edge = Mathf.Clamp01(3f - d);                       // outline band
                    Color c = Color.Lerp(Color.white, new Color(0.1f, 0.08f, 0.02f), edge * 0.9f);
                    c.a = a;
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();

            reticleMaterial = new Material(shader) { mainTexture = tex };
            reticleMaterial.SetColor("_Color", reticleColor);
            reticleMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay + 10; // above the building cards too

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "FollowReticle";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(reticleSize, reticleSize, 1f);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = reticleMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            reticle = go.transform;
            go.SetActive(false);
        }

        private void PlaceReticle(Vector3 pos)
        {
            if (reticle == null) return;
            if (mainCam == null) mainCam = Camera.main;
            float bob = Mathf.Sin(Time.time * 4f) * 0.12f;
            reticle.position = pos + Vector3.up * bob;
            if (mainCam != null) reticle.rotation = Quaternion.LookRotation(mainCam.transform.forward, mainCam.transform.up);
            SetReticleVisible(true);
        }

        private void SetReticleVisible(bool visible)
        {
            if (reticle != null && reticle.gameObject.activeSelf != visible) reticle.gameObject.SetActive(visible);
        }
    }
}
