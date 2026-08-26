using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires.Village
{
    /// <summary>
    /// View-only model tweaks per villager: children are small, elders shorter and stooped,
    /// eccentrics spin/wobble while on one of their episodes.
    ///
    /// Tilts and spins are applied to a "ModelPivot" child inserted under the UnitView root —
    /// never to the root itself, because UnitView smooths its facing from the root's current
    /// rotation and any per-frame root tweak compounds into a tumble.
    /// </summary>
    [DefaultExecutionOrder(500)]
    public class VillagerAgeVisuals : MonoBehaviour
    {
        [SerializeField] private float childScale = 0.6f;
        [SerializeField] private Vector3 elderScale = new Vector3(0.95f, 0.8f, 0.95f);
        [SerializeField] private float elderStoopDegrees = 18f;
        [SerializeField] private float quirkSpinDegreesPerSecond = 540f;

        [SerializeField] private Color femaleColor = new Color(1f, 0.45f, 0.72f);

        private readonly Dictionary<int, UnitView> viewCache = new Dictionary<int, UnitView>();
        private readonly Dictionary<int, Transform> pivots = new Dictionary<int, Transform>();
        private readonly HashSet<int> genderApplied = new HashSet<int>();
        private readonly Dictionary<Material, Material> femaleMats = new Dictionary<Material, Material>();
        private int viewCacheFrame = -1;

        /// <summary>Women get pink team parts (men keep the player's blue). Same part rule as GameSetup.SpawnUnit.</summary>
        private void ApplyGenderColor(VillagerProfile p, UnitView view)
        {
            if (genderApplied.Contains(p.UnitId)) return;
            genderApplied.Add(p.UnitId);
            if (p.Gender != Gender.Female) return;
            foreach (var r in view.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.gameObject.name;
                bool team = n.EndsWith("_Team", System.StringComparison.Ordinal) || n.StartsWith("Body", System.StringComparison.Ordinal) || n.StartsWith("Sphere", System.StringComparison.Ordinal);
                if (!team || r.sharedMaterials.Length == 0 || r.sharedMaterials[0] == null) continue;
                var src = r.sharedMaterials[0];
                if (!femaleMats.TryGetValue(src, out var pink))
                {
                    pink = new Material(src) { name = src.name + " (female)" };
                    if (pink.HasProperty("_Color1")) pink.SetColor("_Color1", femaleColor);
                    else if (pink.HasProperty("_BaseColor")) pink.SetColor("_BaseColor", femaleColor);
                    else if (pink.HasProperty("_Color")) pink.SetColor("_Color", femaleColor);
                    femaleMats[src] = pink;
                }
                var mats = r.sharedMaterials;
                mats[0] = pink;
                r.sharedMaterials = mats;
            }
        }

        private void LateUpdate()
        {
            var village = VillageBootstrapper.Instance;
            var sim = GameBootstrapper.Instance?.Simulation;
            if (village == null || village.Routine == null || sim == null) return;

            if (Time.frameCount - viewCacheFrame > 30)
            {
                viewCacheFrame = Time.frameCount;
                viewCache.Clear();
                foreach (var v in FindObjectsByType<UnitView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    viewCache[v.UnitId] = v;
            }

            var profiles = village.Routine.Profiles;
            for (int i = 0; i < profiles.Count; i++)
            {
                var p = profiles[i];
                if (p.IsDead || !viewCache.TryGetValue(p.UnitId, out var view) || view == null) continue;
                ApplyGenderColor(p, view);
                var t = view.transform;

                // Scale on the root is safe (UnitView never touches scale).
                float grow = Mathf.Clamp01(p.AgeDays(sim.CurrentTick) / Mathf.Max(1, village.Routine.ChildDays));
                Vector3 scale = p.Stage == LifeStage.Child ? Vector3.one * Mathf.Lerp(childScale, 1f, grow * grow)
                              : p.Stage == LifeStage.Elder ? elderScale : Vector3.one;
                if (t.localScale != scale) t.localScale = scale;

                // Tilt / spin on the pivot only.
                bool stoop = p.Stage == LifeStage.Elder;
                bool spinning = p.Quirky && p.Errand == Errand.Quirk;
                bool fighting = p.Errand == Errand.Fight;
                bool mounted = p.Mounted;
                if (!stoop && !spinning && !fighting && !mounted)
                {
                    if (pivots.TryGetValue(p.UnitId, out var idle) && idle != null)
                    {
                        if (idle.localRotation != Quaternion.identity) idle.localRotation = Quaternion.identity;
                        if (idle.localPosition != Vector3.zero) idle.localPosition = Vector3.zero;
                    }
                    continue;
                }

                var pivot = GetPivot(p.UnitId, view);
                if (pivot == null) continue;
                // Militia riders sit up on the primitive horse; knights use the RTS knight model (already mounted).
                bool primitiveMount = mounted && p.Military != MilitaryKind.Knight;
                var seat = primitiveMount ? new Vector3(0f, 1.05f, -0.05f) : Vector3.zero;
                if (pivot.localPosition != seat) pivot.localPosition = seat;
                if (mounted && !stoop && !spinning && !fighting) { if (pivot.localRotation != Quaternion.identity) pivot.localRotation = Quaternion.identity; continue; }
                float spin = spinning ? Mathf.Repeat(Time.time * quirkSpinDegreesPerSecond + p.UnitId * 37f, 360f) : 0f;
                float wobble = spinning ? Mathf.Sin(Time.time * 9f + p.UnitId) * 10f : 0f;
                if (fighting)
                {
                    // Scuffle: quick jerky lunges and sways.
                    float ft = Time.time * 14f + p.UnitId;
                    pivot.localRotation = Quaternion.Euler(Mathf.Sin(ft) * 16f + (stoop ? elderStoopDegrees : 0f), Mathf.Sin(ft * 0.7f) * 25f, Mathf.Cos(ft * 1.3f) * 12f);
                    continue;
                }
                pivot.localRotation = Quaternion.Euler(stoop ? elderStoopDegrees : wobble, spin, spinning ? wobble : 0f);
            }
        }

        /// <summary>Insert (once) a pivot under the view root and move the model parts under it.</summary>
        private Transform GetPivot(int unitId, UnitView view)
        {
            if (pivots.TryGetValue(unitId, out var pivot) && pivot != null) return pivot;
            var existing = view.transform.Find("ModelPivot");
            if (existing != null) { pivots[unitId] = existing; return existing; }

            var go = new GameObject("ModelPivot");
            pivot = go.transform;
            pivot.SetParent(view.transform, false);

            var children = new List<Transform>();
            foreach (Transform c in view.transform)
            {
                if (c == pivot) continue;
                // Leave UI-ish children (selection ring, idle marker, bars) and village add-ons attached to the root.
                string n = c.name;
                if (n == "SelectionRing" || n.Contains("Zzz") || n.Contains("Health") || n.Contains("Bar") || n == "Mount" || n == "HorseModel" || n == "WolfModel") continue;
                children.Add(c);
            }
            foreach (var c in children) c.SetParent(pivot, true);

            pivots[unitId] = pivot;
            return pivot;
        }
    }
}
