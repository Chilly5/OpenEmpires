using UnityEngine;

namespace OpenEmpires
{
    /// <summary>
    /// The trail of dust a unit on foot scuffs up as it walks. Puffs are spent per metre covered
    /// rather than per second, so one lands every stride whatever the unit's speed, and a unit
    /// being shoved sideways by separation never scuffs the ground at all.
    ///
    /// Mounted units are served by <see cref="UnitGallopVisualAnimator"/>, which kicks dust from
    /// the real hoof positions of its leg animation. This stands in for everyone else, who have
    /// no legs to read a footfall from — the stride is inferred from ground covered instead.
    ///
    /// Purely cosmetic. Lives entirely in the view layer and never feeds back into the
    /// simulation, so it cannot affect determinism.
    /// </summary>
    public sealed class UnitFootDustVisual
    {
        // Below this fraction of top speed a unit is being nudged, not walking. Deliberately
        // higher than the cavalry threshold: a jostling crowd standing still would otherwise
        // smoulder with dust.
        private const float MinNormalizedSpeed = 0.35f;

        private const float StrideLength = 0.85f;   // metres between footfalls
        private const float StepWidth = 0.13f;      // sideways offset, so prints alternate feet
        private const float TrailOffset = 0.14f;    // puffs land just behind the unit, not under it

        private const float MinStrength = 0.10f;
        private const float MaxStrength = 0.40f;

        // A frame that moves a unit further than this is a teleport — a respawn, a knockback
        // landing, a camera-cut re-anchor — not a stride, so the trail restarts instead of
        // spraying a line of puffs across everything it skipped over.
        private const float TeleportDistance = 1.5f;

        private readonly float dustScale;

        private Vector3 lastGroundPosition;
        private bool hasLastPosition;
        private float distanceSinceStep;
        private bool leftFoot;

        /// <param name="unitId">
        /// Used only to stagger the stride, so a marching group does not puff in unison.
        /// </param>
        public UnitFootDustVisual(int unitType, int unitId)
        {
            dustScale = DustScaleFor(unitType);

            // Same cheap spread the gallop animator uses to desynchronise neighbours.
            distanceSinceStep = Mathf.Repeat(unitId * 0.6180339f, 1f) * StrideLength;
            leftFoot = (unitId & 1) == 0;
        }

        /// <summary>
        /// Advances the stride by however far the unit has moved since the last frame.
        /// <paramref name="groundPosition"/> is the unit's feet, not its centre.
        /// </summary>
        public void UpdateFootDust(float normalizedSpeed, Vector3 groundPosition, Vector3 forward)
        {
            if (dustScale <= 0f) return;

            Vector3 previous = lastGroundPosition;
            bool hadPosition = hasLastPosition;
            lastGroundPosition = groundPosition;
            hasLastPosition = true;

            if (!hadPosition) return;

            // Horizontal only — walking up a slope should not count the climb as extra stride.
            Vector3 travel = groundPosition - previous;
            travel.y = 0f;
            float distance = travel.magnitude;

            if (distance > TeleportDistance)
            {
                distanceSinceStep = 0f;
                return;
            }

            if (normalizedSpeed < MinNormalizedSpeed) return;

            distanceSinceStep += distance;
            if (distanceSinceStep < StrideLength) return;

            float strength = Mathf.Lerp(MinStrength, MaxStrength,
                Mathf.InverseLerp(MinNormalizedSpeed, 1f, normalizedSpeed)) * dustScale;

            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);

            while (distanceSinceStep >= StrideLength)
            {
                distanceSinceStep -= StrideLength;

                Vector3 foot = groundPosition
                    - forward * TrailOffset
                    + right * (leftFoot ? -StepWidth : StepWidth);
                leftFoot = !leftFoot;

                GroundDustVisual.Burst(foot, Mathf.Clamp01(strength));
            }
        }

        /// <summary>How heavily a unit treads. Boots kick up more than bare feet or hooves the
        /// size of a sheep's, and a Monk in robes glides.</summary>
        private static float DustScaleFor(int unitType)
        {
            switch (unitType)
            {
                case 0: return 0.75f;  // Villager — working clothes, unhurried
                case 5: return 0.35f;  // Sheep — small and light
                case 9: return 0.55f;  // Monk — robes sweep rather than stamp
                default: return 1f;    // Soldiery in boots
            }
        }
    }
}
