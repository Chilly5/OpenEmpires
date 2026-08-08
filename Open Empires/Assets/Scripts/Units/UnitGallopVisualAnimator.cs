using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    /// <summary>
    /// View-only locomotion for the four-legged mounted models. Swings each horse leg from an
    /// inserted hip pivot, folds it as it passes through the air, nods the head, lets the rider
    /// ride the motion a beat behind the horse, and kicks dust and hoofbeats on each footfall.
    ///
    /// The horse walks at low speed and gallops at high speed, cross-fading between the two.
    /// Each mounted unit carries its own <see cref="GaitProfile"/>, so a barded Knight moves like
    /// a heavy charger and a Scout like a light courier even though they share this code.
    ///
    /// Parts are located by shape and position rather than exact names, because the mounted models
    /// disagree on naming: the Scout's parts carry "(1)" suffixes, the King is built in code with
    /// four legs all called "HorseLeg", and the Knight adds barding the others do not have.
    ///
    /// Nothing here feeds back into the simulation, so it cannot affect determinism.
    /// Layers on top of <see cref="UnitAttackVisualAnimator"/>, so it must be updated after it.
    /// </summary>
    public sealed class UnitGallopVisualAnimator
    {
        /// <summary>
        /// How one mounted unit carries itself. Everything that separates a Knight from a Scout
        /// lives here — the shared motion code below reads these and nothing else.
        /// </summary>
        private readonly struct GaitProfile
        {
            public readonly float StrideLength;    // metres covered per stride; sets leg turnover
            public readonly float SwingScale;      // how far the legs reach
            public readonly float FoldScale;       // how tightly the leg tucks in flight
            public readonly float BobScale;        // how much the body heaves — reads as weight
            public readonly float NodScale;
            public readonly float HeadCarriage;    // degrees; positive is nose down, negative high
            public readonly float GallopFromSpeed; // fraction of top speed where the gallop starts
            public readonly float DustScale;
            public readonly float HoofbeatVolume;
            public readonly float HoofbeatPitch;
            public readonly float RiderLeanDegrees; // constant forward lean of the rider

            public GaitProfile(float strideLength, float swingScale, float foldScale, float bobScale,
                float nodScale, float headCarriage, float gallopFromSpeed, float dustScale,
                float hoofbeatVolume, float hoofbeatPitch, float riderLeanDegrees)
            {
                StrideLength = strideLength;
                SwingScale = swingScale;
                FoldScale = foldScale;
                BobScale = bobScale;
                NodScale = nodScale;
                HeadCarriage = headCarriage;
                GallopFromSpeed = gallopFromSpeed;
                DustScale = dustScale;
                HoofbeatVolume = hoofbeatVolume;
                HoofbeatPitch = hoofbeatPitch;
                RiderLeanDegrees = riderLeanDegrees;
            }
        }

        //                                          stride swing  fold   bob   nod  carry gallop  dust   vol  pitch  lean
        private static readonly GaitProfile Horseman =
            new GaitProfile(1.90f, 1.00f, 1.00f, 1.00f, 1.00f,  0f,  0.40f, 1.00f, 0.35f, 1.00f,  0f);

        // Unarmoured courier: long flat bounding stride, head low and forward, barely any heave.
        private static readonly GaitProfile Scout =
            new GaitProfile(2.15f, 1.15f, 1.20f, 0.60f, 0.80f,  6f,  0.28f, 0.65f, 0.26f, 1.22f,  7f);

        // Barded warhorse: slow powerful turnover, big heave, lands hard, stays in trot longer.
        private static readonly GaitProfile Knight =
            new GaitProfile(2.30f, 0.92f, 0.75f, 1.45f, 0.85f,  2f,  0.62f, 1.60f, 0.50f, 0.78f, -2f);

        // A Horseman with more mass behind it — the one tell between two near-identical models.
        private static readonly GaitProfile Gendarme =
            new GaitProfile(2.05f, 0.96f, 0.88f, 1.22f, 0.90f,  1f,  0.50f, 1.25f, 0.42f, 0.88f, -1f);

        // Parade horse, not a warhorse: collected steps, neck arched, kicks up little. The stride
        // stays fairly long on purpose — a shorter, tighter setting turned the King's short legs
        // into a fast shuffle that read as no leg movement at all.
        private static readonly GaitProfile King =
            new GaitProfile(1.80f, 1.05f, 0.95f, 0.85f, 1.15f, -9f,  0.70f, 0.30f, 0.22f, 1.10f, -4f);

        private const int LegCount = 4;
        private const int FrontLeft = 0;
        private const int FrontRight = 1;
        private const int BackLeft = 2;
        private const int BackRight = 3;

        // Transverse gallop: the hind pair drives first, the fore pair reaches out roughly half a
        // cycle later, with a small lead-leg offset inside each pair.
        private static readonly float[] GallopPhases = { 0.46f, 0.54f, 0f, 0.09f };

        // Four-beat walk, in the order a horse actually places its feet:
        // back-left, front-left, back-right, front-right.
        private static readonly float[] WalkPhases = { 0.25f, 0.75f, 0f, 0.5f };

        private const float GallopSwingAngle = 40f;        // peak fore/aft leg swing, degrees
        private const float WalkSwingAngle = 22f;
        private const float GallopFlightFraction = 0.35f;  // share of the cycle spent off the ground
        private const float WalkFlightFraction = 0.28f;
        private const float GallopFold = 0.30f;            // how far the leg folds up at mid-flight
        private const float WalkFold = 0.10f;
        private const float GallopBobHeight = 0.05f;
        private const float WalkBobHeight = 0.012f;

        private const float PitchAngle = 4f;
        private const float ChargeLean = 5f;
        private const float ChargeCadenceScale = 1.2f;
        private const float BlendSpeed = 6f;
        private const float MaxCadence = 4.5f;             // safety net against absurd stride rates

        private const float GallopBandWidth = 0.35f;       // speed span over which walk becomes gallop

        private const float WalkNodAngle = 7f;
        private const float GallopNodAngle = 10f;

        private const float RiderBobShare = 0.55f;    // the rider heaves less than the horse...
        private const float RiderLag = 0.09f;         // ...and slightly later
        private const float RiderCounterPitch = 0.45f;

        private const float DustMinSpeed = 0.5f;
        private const float HoofbeatMinSpeed = 0.25f;

        private readonly GaitProfile profile;
        private readonly Transform visualRoot;
        private readonly Transform[] legs;
        private readonly Transform[] hips;
        private readonly Quaternion[] hipRestRotations;
        private readonly float[] previousLegPhases = new float[LegCount];

        private readonly Transform neckPivot;
        private readonly Quaternion neckRestRotation;

        private readonly Transform riderPivot;
        private readonly Vector3 riderRestPosition;
        private readonly Quaternion riderRestRotation;

        private readonly float phaseOffset;

        private float stridePhase;
        private float gaitBlend;
        private float gallopBlend;
        private bool footfallTrackingValid;
        private bool trackedGallopGait;

        public bool HasGallop => hips != null;

        /// <summary>
        /// True for the mounted unit types this animator drives.
        ///
        /// The King is deliberately excluded for now. His model is built in code rather than from
        /// a prefab and is proportioned differently from the others — a very wide barrel with short
        /// legs tucked well underneath it — and his legs never read convincingly at any gait we
        /// tried. His <see cref="King"/> profile below is kept ready for when that is revisited;
        /// adding UnitData.KingUnitType back here is all it takes to turn him on again.
        /// </summary>
        public static bool IsMounted(int unitType)
        {
            return unitType == 3 || unitType == 4 || unitType == 7 || unitType == 11;
        }

        /// <param name="unitId">
        /// Used only to stagger the starting stride, so a group of horses does not move as one body.
        /// </param>
        public UnitGallopVisualAnimator(Transform visualRoot, int unitType, int unitId)
        {
            this.visualRoot = visualRoot;
            profile = ProfileFor(unitType);
            if (visualRoot == null) return;

            // Spread units around the stride cycle. A plain multiply-and-wrap is enough here —
            // this is cosmetic only, so it just has to look unrelated between neighbours.
            phaseOffset = Mathf.Repeat(unitId * 0.6180339f, 1f);
            stridePhase = phaseOffset;

            var horseParts = new List<Transform>();
            var riderParts = new List<Transform>();
            var legParts = new Transform[LegCount];
            Classify(visualRoot, horseParts, riderParts, legParts);

            // Build the rider group before inserting any pivots, so new pivots are never mistaken
            // for parts of the model.
            riderPivot = CreateRiderPivot(visualRoot, riderParts, horseParts);
            if (riderPivot != null)
            {
                riderRestPosition = riderPivot.localPosition;
                riderRestRotation = riderPivot.localRotation;
            }

            neckPivot = CreateNeckPivot(visualRoot, horseParts);
            if (neckPivot != null)
                neckRestRotation = neckPivot.localRotation;

            var foundHips = new Transform[LegCount];
            var foundRest = new Quaternion[LegCount];
            bool anyLeg = false;

            for (int i = 0; i < LegCount; i++)
            {
                if (legParts[i] == null) continue;

                foundHips[i] = CreateHipPivot(visualRoot, legParts[i]);
                foundRest[i] = foundHips[i].localRotation;
                anyLeg = true;
            }

            if (!anyLeg) return;

            legs = legParts;
            hips = foundHips;
            hipRestRotations = foundRest;
        }

        /// <param name="normalizedSpeed">Distance covered last tick as a fraction of top speed.</param>
        /// <param name="groundSpeed">Actual speed in world units per second.</param>
        /// <param name="groundPosition">Where the unit meets the ground, for dust and hoofbeats.</param>
        public void UpdateGallop(float normalizedSpeed, float groundSpeed, bool isCharging,
            Vector3 groundPosition, float deltaTime)
        {
            if (hips == null) return;

            normalizedSpeed = Mathf.Clamp01(normalizedSpeed);

            // Ramp in quickly once the unit is clearly moving, and settle out when it stops.
            float targetBlend = Mathf.Clamp01(normalizedSpeed * 2.5f);
            gaitBlend = Mathf.MoveTowards(gaitBlend, targetBlend, deltaTime * BlendSpeed);

            float gallopFrom = profile.GallopFromSpeed;
            float targetGallop = Mathf.Clamp01(
                Mathf.InverseLerp(gallopFrom, gallopFrom + GallopBandWidth, normalizedSpeed));
            if (isCharging) targetGallop = 1f;
            gallopBlend = Mathf.MoveTowards(gallopBlend, targetGallop, deltaTime * BlendSpeed * 0.5f);

            if (gaitBlend > 0.001f)
            {
                // Leg turnover follows real ground speed, not speed-as-a-fraction-of-this-unit's-top.
                // That is what makes a quick Scout visibly outpace a heavy Knight instead of the two
                // running identical cycles at their own full speeds.
                float cadence = Mathf.Min(groundSpeed / Mathf.Max(profile.StrideLength, 0.01f), MaxCadence);
                if (isCharging) cadence *= ChargeCadenceScale;
                stridePhase = Mathf.Repeat(stridePhase + cadence * deltaTime, 1f);
            }

            PoseLegs();
            PoseBody(isCharging);
            HandleFootfalls(normalizedSpeed, groundPosition);
        }

        public void ResetPose()
        {
            stridePhase = phaseOffset;
            gaitBlend = 0f;
            gallopBlend = 0f;
            footfallTrackingValid = false;

            if (neckPivot != null)
                neckPivot.localRotation = neckRestRotation;

            if (riderPivot != null)
            {
                riderPivot.localPosition = riderRestPosition;
                riderPivot.localRotation = riderRestRotation;
            }

            if (hips == null) return;

            for (int i = 0; i < LegCount; i++)
            {
                if (hips[i] == null) continue;
                hips[i].localRotation = hipRestRotations[i];
                hips[i].localScale = Vector3.one;
            }
        }

        private static GaitProfile ProfileFor(int unitType)
        {
            switch (unitType)
            {
                case 4: return Scout;
                case 7: return Knight;
                case 11: return Gendarme;
                case UnitData.KingUnitType: return King;
                default: return Horseman;
            }
        }

        private void PoseLegs()
        {
            for (int i = 0; i < LegCount; i++)
            {
                if (hips[i] == null) continue;

                float walkPhase = stridePhase + WalkPhases[i];
                float gallopPhase = stridePhase + GallopPhases[i];

                // The two gaits put a leg in different places at the same instant, so blend the
                // resulting angles rather than the phases — phases are cyclic and do not lerp.
                float swing = Mathf.Lerp(
                    StrideCurve(walkPhase, WalkFlightFraction) * WalkSwingAngle,
                    StrideCurve(gallopPhase, GallopFlightFraction) * GallopSwingAngle,
                    gallopBlend) * profile.SwingScale;

                float fold = Mathf.Lerp(
                    FoldCurve(walkPhase, WalkFlightFraction) * WalkFold,
                    FoldCurve(gallopPhase, GallopFlightFraction) * GallopFold,
                    gallopBlend) * profile.FoldScale;

                // StrideCurve is +1 reaching forward; a positive x-rotation swings a leg back.
                hips[i].localRotation = hipRestRotations[i]
                    * Quaternion.Euler(-swing * gaitBlend, 0f, 0f);

                // Shortening the pivot draws the hoof up toward the hip, which reads as a knee
                // bend without the model actually having one.
                var scale = Vector3.one;
                scale.y = 1f - Mathf.Clamp(fold * gaitBlend, 0f, 0.6f);
                hips[i].localScale = scale;
            }
        }

        private void PoseBody(bool isCharging)
        {
            if (visualRoot == null) return;

            float bobPhase = stridePhase * Mathf.PI * 2f;
            float bob = BodyBob(bobPhase);

            // Body bob and pitch are added to whatever the attack animator posed this frame.
            visualRoot.localPosition += new Vector3(0f, bob * gaitBlend, 0f);

            float pitch = Mathf.Cos(bobPhase) * PitchAngle * gallopBlend;
            if (isCharging) pitch += ChargeLean;
            visualRoot.localRotation *= Quaternion.Euler(pitch * gaitBlend, 0f, 0f);

            if (neckPivot != null)
            {
                // The head pumps opposite the heave — down as the forehand lands, up over the top.
                float nodAngle = Mathf.Lerp(WalkNodAngle, GallopNodAngle, gallopBlend) * profile.NodScale;
                float nod = Mathf.Sin(bobPhase + Mathf.PI * 0.5f) * nodAngle * gaitBlend;

                // Head carriage is a standing posture, so it holds whether moving or not.
                neckPivot.localRotation = neckRestRotation
                    * Quaternion.Euler(profile.HeadCarriage + nod, 0f, 0f);
            }

            if (riderPivot != null)
            {
                // The rider is a child of the root, so it has already taken the full body bob.
                // Apply only the difference to leave them heaving less, and a fraction late.
                float riderBob = BodyBob(bobPhase - RiderLag * Mathf.PI * 2f) * RiderBobShare;
                var offset = riderRestPosition;
                offset.y += (riderBob - bob) * gaitBlend;
                riderPivot.localPosition = offset;

                float lean = profile.RiderLeanDegrees * gaitBlend
                           - pitch * RiderCounterPitch * gaitBlend;
                riderPivot.localRotation = riderRestRotation * Quaternion.Euler(lean, 0f, 0f);
            }
        }

        /// <summary>
        /// Vertical heave of the body, in world units, for the current mix of walk and gallop.
        /// A gallop heaves once per stride over the suspension phase; a walk only rocks gently,
        /// twice per stride, as each diagonal pair takes the weight.
        /// </summary>
        private float BodyBob(float bobPhase)
        {
            float gallop = (Mathf.Sin(bobPhase) * 0.65f + Mathf.Sin(bobPhase * 2f) * 0.35f) * GallopBobHeight;
            float walk = Mathf.Sin(bobPhase * 2f) * WalkBobHeight;
            return Mathf.Lerp(walk, gallop, gallopBlend) * profile.BobScale;
        }

        private void HandleFootfalls(float normalizedSpeed, Vector3 groundPosition)
        {
            bool useGallop = gallopBlend >= 0.5f;
            float[] phases = useGallop ? GallopPhases : WalkPhases;
            float flight = useGallop ? GallopFlightFraction : WalkFlightFraction;

            bool moving = gaitBlend > 0.05f;
            // After a gait change or a standstill the stored phases mean nothing, so re-seed them
            // rather than firing a spurious volley of footfalls.
            bool reseed = !footfallTrackingValid || useGallop != trackedGallopGait || !moving;

            for (int i = 0; i < LegCount; i++)
            {
                float phase = Mathf.Repeat(stridePhase + phases[i], 1f);

                if (!reseed && CrossedForward(previousLegPhases[i], phase, flight))
                    OnFootfall(i, normalizedSpeed, groundPosition);

                previousLegPhases[i] = phase;
            }

            footfallTrackingValid = moving;
            trackedGallopGait = useGallop;
        }

        private void OnFootfall(int legIndex, float normalizedSpeed, Vector3 groundPosition)
        {
            if (legs[legIndex] != null && normalizedSpeed >= DustMinSpeed && profile.DustScale > 0f)
            {
                Vector3 hoof = legs[legIndex].position;
                hoof.y = groundPosition.y;

                float strength = Mathf.InverseLerp(DustMinSpeed, 1f, normalizedSpeed) * profile.DustScale;
                HoofDustVisual.Burst(hoof, Mathf.Clamp01(strength));
            }

            if (normalizedSpeed >= HoofbeatMinSpeed)
            {
                SFXManager.Instance?.Play(SFXType.Hoofbeat, groundPosition,
                    profile.HoofbeatVolume, profile.HoofbeatPitch);
            }
        }

        /// <summary>
        /// Asymmetric stride: a quick reach forward through the air, then a longer push back
        /// while the hoof is planted. Returns -1 (fully back) through +1 (fully forward).
        /// </summary>
        private static float StrideCurve(float phase, float flightFraction)
        {
            float t = Mathf.Repeat(phase, 1f);

            if (t < flightFraction)
            {
                float k = t / flightFraction;
                return Mathf.Lerp(-1f, 1f, k * k * (3f - 2f * k));
            }

            float stance = (t - flightFraction) / (1f - flightFraction);
            return Mathf.Lerp(1f, -1f, stance);
        }

        /// <summary>
        /// How far the leg is folded up, 0 while the hoof is planted rising to 1 mid-flight.
        /// </summary>
        private static float FoldCurve(float phase, float flightFraction)
        {
            float t = Mathf.Repeat(phase, 1f);
            if (t >= flightFraction) return 0f;

            return Mathf.Sin(t / flightFraction * Mathf.PI);
        }

        /// <summary>Did a forward-running, wrapping phase pass <paramref name="mark"/> this frame?</summary>
        private static bool CrossedForward(float previous, float current, float mark)
        {
            if (current >= previous)
                return previous < mark && current >= mark;

            // Wrapped past 1 back to 0.
            return previous < mark || current >= mark;
        }

        /// <summary>
        /// Sorts the model's parts into legs, the rest of the horse, and the rider. Legs are placed
        /// into their four slots by where they sit rather than by name, which is what lets one rule
        /// cover "LegFrontLeft", "LegFrontLeft (1)" and "HorseLeg0" alike.
        /// </summary>
        private static void Classify(Transform visualRoot, List<Transform> horseParts,
            List<Transform> riderParts, Transform[] legParts)
        {
            for (int i = 0; i < visualRoot.childCount; i++)
            {
                Transform child = visualRoot.GetChild(i);
                string name = BaseName(child.name);

                // The attack animator owns the weapon pivots; never move them.
                if (child.name.StartsWith("Attack", StringComparison.Ordinal)) continue;
                if (name == "SelectionRing") continue;

                if (name.IndexOf("Leg", StringComparison.Ordinal) >= 0)
                {
                    Vector3 p = child.localPosition;
                    int slot = (p.z >= 0f ? FrontLeft : BackLeft) + (p.x >= 0f ? 1 : 0);
                    if (legParts[slot] == null)
                        legParts[slot] = child;
                    else
                        horseParts.Add(child); // duplicate in a slot — leave it on the body
                    continue;
                }

                if (IsHorsePart(name))
                    horseParts.Add(child);
                else
                    riderParts.Add(child);
            }
        }

        /// <summary>
        /// Horse parts are named for the horse, plus the chanfron — the plate over a barded
        /// warhorse's face, which belongs to the head rather than the rider wearing the armour.
        /// </summary>
        private static bool IsHorsePart(string baseName)
        {
            return baseName.StartsWith("Horse", StringComparison.Ordinal) || baseName == "Chanfron";
        }

        /// <summary>
        /// Strips Unity's duplicate suffixes and trailing indices, so "HorseBody (3)" and
        /// "HorseLeg0" both reduce to the family they belong to.
        /// </summary>
        private static string BaseName(string name)
        {
            int paren = name.IndexOf(" (", StringComparison.Ordinal);
            if (paren > 0) name = name.Substring(0, paren);

            int end = name.Length;
            while (end > 0 && char.IsDigit(name[end - 1])) end--;

            return end > 0 ? name.Substring(0, end) : name;
        }

        /// <summary>
        /// The leg meshes are boxes pivoted at their own centre, so swinging them directly would
        /// rotate them around the knee. Insert an empty at the top of the leg to swing from instead.
        /// </summary>
        private static Transform CreateHipPivot(Transform space, Transform leg)
        {
            Vector3 hipLocal = leg.localPosition;

            if (TryGetLocalBounds(space, leg, out Bounds bounds))
                hipLocal = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);

            return WrapAt(space, leg.name + "Hip", hipLocal, new List<Transform> { leg });
        }

        /// <summary>
        /// Groups the head — and the neck segment and face armour where a model has them — so they
        /// nod together about the shoulder. The neck is found as the body segment sitting forward of
        /// the barrel, since only some of these models have one at all.
        /// </summary>
        private static Transform CreateNeckPivot(Transform visualRoot, List<Transform> horseParts)
        {
            Transform barrel = LargestPart(horseParts);
            var parts = new List<Transform>();

            for (int i = 0; i < horseParts.Count; i++)
            {
                Transform part = horseParts[i];
                string name = BaseName(part.name);

                bool isHead = name.IndexOf("Head", StringComparison.Ordinal) >= 0;
                bool isFaceArmour = name == "Chanfron";

                // A neck segment shares the barrel's family name but sits ahead of it. Testing the
                // family keeps barding (chest plate, crupper, flank plates) out of the group.
                bool isNeck = barrel != null && part != barrel
                    && name == BaseName(barrel.name)
                    && part.localPosition.z > barrel.localPosition.z;

                if (isHead || isFaceArmour || isNeck)
                    parts.Add(part);
            }

            if (parts.Count == 0) return null;
            if (!TryGetLocalBounds(visualRoot, parts, out Bounds bounds)) return null;

            // Hinge at the back of the group — roughly where the neck meets the shoulder. This is
            // measured along the horse's own axes; measuring along world axes would put the hinge
            // somewhere near the middle of the head for any horse not happening to face +Z, and a
            // head that turns about its own middle barely moves its nose.
            var hinge = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z);
            return WrapAt(visualRoot, "NeckPivot", hinge, parts);
        }

        /// <summary>
        /// Everything on the model that is not the horse is the rider and their kit.
        /// </summary>
        private static Transform CreateRiderPivot(Transform visualRoot, List<Transform> riderParts,
            List<Transform> horseParts)
        {
            if (riderParts.Count == 0) return null;
            if (!TryGetLocalBounds(visualRoot, riderParts, out Bounds riderBounds)) return null;

            // Hinge at the saddle, so leaning rotates the rider about their seat. Prefer the top of
            // the horse's barrel — imported rider meshes can sit at odd transforms, and their own
            // bounds are a less trustworthy guide to where the seat actually is.
            var seat = new Vector3(riderBounds.center.x, riderBounds.min.y, riderBounds.center.z);

            Transform barrel = LargestPart(horseParts);
            if (barrel != null && TryGetLocalBounds(visualRoot, barrel, out Bounds barrelBounds))
                seat = new Vector3(barrelBounds.center.x, barrelBounds.max.y, barrelBounds.center.z);

            return WrapAt(visualRoot, "RiderPivot", seat, riderParts);
        }

        /// <summary>The horse's barrel: the bulkiest single piece of it.</summary>
        private static Transform LargestPart(List<Transform> parts)
        {
            Transform best = null;
            float bestVolume = 0f;

            for (int i = 0; i < parts.Count; i++)
            {
                if (!TryGetLocalBounds(parts[i], parts[i], out Bounds bounds)) continue;

                Vector3 size = bounds.size;
                float volume = size.x * size.y * size.z;
                if (volume > bestVolume)
                {
                    bestVolume = volume;
                    best = parts[i];
                }
            }

            return best;
        }

        /// <summary>
        /// Parents <paramref name="parts"/> under a new empty sitting at <paramref name="localPivot"/>
        /// in <paramref name="parent"/>'s own space, aligned to that space so rotations read as
        /// fore/aft and side to side on the horse rather than along the world axes.
        /// </summary>
        private static Transform WrapAt(Transform parent, string name, Vector3 localPivot, List<Transform> parts)
        {
            var pivotObject = new GameObject(name);
            pivotObject.layer = parent.gameObject.layer;

            Transform pivot = pivotObject.transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = localPivot;
            pivot.localRotation = Quaternion.identity;
            pivot.localScale = Vector3.one;

            for (int i = 0; i < parts.Count; i++)
                parts[i].SetParent(pivot, true);

            return pivot;
        }

        private static bool TryGetLocalBounds(Transform space, Transform part, out Bounds bounds)
        {
            bounds = default;
            bool has = false;
            Accumulate(space, part, ref bounds, ref has);
            return has;
        }

        /// <summary>
        /// Bounds of the given parts expressed in <paramref name="space"/>'s own coordinates.
        /// Renderer.bounds is a world-axis box, which is the wrong ruler for questions like
        /// "where is the back of this horse's neck" once the horse is facing any direction but +Z.
        /// </summary>
        private static bool TryGetLocalBounds(Transform space, List<Transform> parts, out Bounds bounds)
        {
            bounds = default;
            bool has = false;

            for (int i = 0; i < parts.Count; i++)
                Accumulate(space, parts[i], ref bounds, ref has);

            return has;
        }

        private static void Accumulate(Transform space, Transform part, ref Bounds bounds, ref bool has)
        {
            Matrix4x4 toLocal = space.worldToLocalMatrix;
            var renderers = part.GetComponentsInChildren<Renderer>(true);

            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                Bounds source;
                Matrix4x4 matrix;

                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    // Exact: take the mesh's own box through the renderer's transform.
                    source = filter.sharedMesh.bounds;
                    matrix = toLocal * renderer.localToWorldMatrix;
                }
                else
                {
                    // No mesh to read (skinned, particles); fall back to the world box.
                    source = renderer.bounds;
                    matrix = toLocal;
                }

                Vector3 c = source.center;
                Vector3 e = source.extents;

                for (int corner = 0; corner < 8; corner++)
                {
                    var offset = new Vector3(
                        (corner & 1) == 0 ? -e.x : e.x,
                        (corner & 2) == 0 ? -e.y : e.y,
                        (corner & 4) == 0 ? -e.z : e.z);

                    Vector3 point = matrix.MultiplyPoint3x4(c + offset);

                    if (!has)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        has = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }
        }
    }
}
