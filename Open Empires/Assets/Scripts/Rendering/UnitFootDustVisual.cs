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

        private const float StrideLength = 0.70f;   // metres between footfalls
        private const float StepWidth = 0.13f;      // sideways offset, so prints alternate feet
        private const float TrailOffset = 0.14f;    // puffs land just behind the unit, not under it

        // Weighted for legibility from the RTS camera. The first pass ran at 0.10..0.40 and was
        // technically emitting the whole time — it simply could not be seen from play distance.
        private const float MinStrength = 0.28f;
        private const float MaxStrength = 0.68f;

        /// <summary>Body height of a foot soldier — the yardstick every other unit is read
        /// against, so a smaller model kicks up proportionally less.</summary>
        public const float ReferenceBodyHeight = 1.1f;

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
        /// <param name="bodyHeight">
        /// World height of the unit's body. Scales the dust with the model, so a sheep scuffs
        /// less than a man-at-arms without either being special-cased.
        /// </param>
        public UnitFootDustVisual(int unitType, int unitId, float bodyHeight)
        {
            // Clamped rather than raw: an oddly-sized model should shade the dust, not run away
            // with it in either direction.
            float bodyScale = bodyHeight > 0.01f
                ? Mathf.Clamp(bodyHeight / ReferenceBodyHeight, 0.45f, 1.6f)
                : 1f;

            dustScale = TreadWeightFor(unitType) * bodyScale;

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

        /// <summary>
        /// How heavily a unit treads, independent of how big it is. Body size is handled
        /// separately, so this carries only what the model cannot say: a villager walking to work
        /// does not stamp like a soldier of the same height marching in boots.
        /// </summary>
        private static float TreadWeightFor(int unitType)
        {
            switch (unitType)
            {
                case 0: return 0.55f;  // Villager — working clothes, unhurried, light on the ground
                case 5: return 0.40f;  // Sheep — dainty on top of already being small
                case 9: return 0.50f;  // Monk — robes sweep rather than stamp
                default: return 0.90f; // Soldiery in boots
            }
        }
    }
}
