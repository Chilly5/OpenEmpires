using UnityEngine;

namespace OpenEmpires
{
    /// <summary>
    /// View-only gallop locomotion for the four-legged mounted models. Swings each horse leg
    /// from a hip pivot and bobs the body in time with how fast the unit is actually travelling.
    /// Nothing here feeds back into the simulation, so it cannot affect determinism.
    /// Layers on top of <see cref="UnitAttackVisualAnimator"/>, so it must be updated after it.
    /// </summary>
    public sealed class UnitGallopVisualAnimator
    {
        private static readonly string[] LegNames =
        {
            "LegFrontLeft", "LegFrontRight", "LegBackLeft", "LegBackRight"
        };

        // Stride offsets for a transverse gallop: the hind pair drives first, the fore pair
        // reaches out roughly half a cycle later, with a small lead-leg offset inside each pair.
        private static readonly float[] LegPhases = { 0.46f, 0.54f, 0f, 0.09f };

        private const float StrideFrequency = 2.1f;   // strides per second at full move speed
        private const float SwingAngle = 40f;         // peak fore/aft leg swing, degrees
        private const float FlightFraction = 0.35f;   // share of the cycle spent reaching forward
        private const float BobHeight = 0.05f;
        private const float PitchAngle = 4f;
        private const float ChargeLean = 5f;
        private const float ChargeCadenceScale = 1.2f;
        private const float BlendSpeed = 6f;

        private readonly Transform visualRoot;
        private readonly Transform[] hips;
        private readonly Quaternion[] hipRestRotations;

        private float stridePhase;
        private float gaitBlend;

        public bool HasGallop => hips != null;

        public UnitGallopVisualAnimator(Transform visualRoot)
        {
            this.visualRoot = visualRoot;
            if (visualRoot == null) return;

            var foundHips = new Transform[LegNames.Length];
            var foundRest = new Quaternion[LegNames.Length];
            bool anyLeg = false;

            for (int i = 0; i < LegNames.Length; i++)
            {
                Transform leg = FindDescendant(visualRoot, LegNames[i]);
                if (leg == null) continue;

                foundHips[i] = CreateHipPivot(leg);
                foundRest[i] = foundHips[i].localRotation;
                anyLeg = true;
            }

            if (!anyLeg) return;

            hips = foundHips;
            hipRestRotations = foundRest;
        }

        /// <param name="normalizedSpeed">Distance covered last tick as a fraction of top speed.</param>
        public void UpdateGallop(float normalizedSpeed, bool isCharging, float deltaTime)
        {
            if (hips == null) return;

            normalizedSpeed = Mathf.Clamp01(normalizedSpeed);

            // Ramp in quickly once the unit is clearly moving, and settle out when it stops.
            float targetBlend = Mathf.Clamp01(normalizedSpeed * 2.5f);
            gaitBlend = Mathf.MoveTowards(gaitBlend, targetBlend, deltaTime * BlendSpeed);

            if (gaitBlend > 0.001f)
            {
                float cadence = StrideFrequency * Mathf.Lerp(0.6f, 1.25f, normalizedSpeed);
                if (isCharging) cadence *= ChargeCadenceScale;
                stridePhase = Mathf.Repeat(stridePhase + cadence * deltaTime, 1f);
            }

            for (int i = 0; i < hips.Length; i++)
            {
                if (hips[i] == null) continue;

                // StrideCurve is +1 reaching forward; a positive x-rotation swings a leg back.
                float swing = StrideCurve(stridePhase + LegPhases[i]);
                hips[i].localRotation = hipRestRotations[i]
                    * Quaternion.Euler(-swing * SwingAngle * gaitBlend, 0f, 0f);
            }

            if (visualRoot == null) return;

            // Body bob and pitch are added to whatever the attack animator posed this frame.
            float bobPhase = stridePhase * Mathf.PI * 2f;
            float bob = Mathf.Sin(bobPhase) * 0.65f + Mathf.Sin(bobPhase * 2f) * 0.35f;
            visualRoot.localPosition += new Vector3(0f, bob * BobHeight * gaitBlend, 0f);

            float pitch = Mathf.Cos(bobPhase) * PitchAngle;
            if (isCharging) pitch += ChargeLean;
            visualRoot.localRotation *= Quaternion.Euler(pitch * gaitBlend, 0f, 0f);
        }

        public void ResetPose()
        {
            stridePhase = 0f;
            gaitBlend = 0f;
            if (hips == null) return;

            for (int i = 0; i < hips.Length; i++)
            {
                if (hips[i] != null)
                    hips[i].localRotation = hipRestRotations[i];
            }
        }

        /// <summary>
        /// Asymmetric stride: a quick reach forward through the air, then a longer push back
        /// while the hoof is planted. Returns -1 (fully back) through +1 (fully forward).
        /// </summary>
        private static float StrideCurve(float phase)
        {
            float t = Mathf.Repeat(phase, 1f);

            if (t < FlightFraction)
            {
                float k = t / FlightFraction;
                return Mathf.Lerp(-1f, 1f, k * k * (3f - 2f * k));
            }

            float stance = (t - FlightFraction) / (1f - FlightFraction);
            return Mathf.Lerp(1f, -1f, stance);
        }

        /// <summary>
        /// The leg meshes are boxes pivoted at their own centre, so swinging them directly would
        /// rotate them around the knee. Insert an empty at the top of the leg to swing from instead.
        /// </summary>
        private static Transform CreateHipPivot(Transform leg)
        {
            Vector3 hipWorld = leg.position;

            var renderers = leg.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                hipWorld = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            }

            var pivotObject = new GameObject(leg.name + "Hip");
            pivotObject.layer = leg.gameObject.layer;

            Transform pivot = pivotObject.transform;
            pivot.SetParent(leg.parent, false);
            pivot.SetPositionAndRotation(hipWorld, leg.rotation);
            leg.SetParent(pivot, true);

            return pivot;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform match = FindDescendant(root.GetChild(i), name);
                if (match != null) return match;
            }
            return null;
        }
    }
}
