using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    /// <summary>
    /// View-only rigid-part animation for the procedural unit models. Combat timing remains
    /// authoritative in the simulation; this class only poses existing visual transforms.
    /// </summary>
    public sealed class UnitAttackVisualAnimator
    {
        private enum MotionStyle
        {
            None,
            VillagerChop,
            SpearThrust,
            BowShot,
            MountedLance,
            ScoutStrike,
            SwordSlash,
            CrossbowShot,
            PolearmSweep,
            RamStrike,
            SiegeThrow
        }

        private const string VisualRootName = "VisualRoot";
        private const string WeaponPivotName = "AttackWeaponPivot";
        private const string OffhandPivotName = "AttackOffhandPivot";

        private readonly MotionStyle style;
        private readonly Transform visualRoot;
        private readonly Transform weaponPivot;
        private readonly Transform offhandPivot;

        private readonly Vector3 visualRestPosition;
        private readonly Quaternion visualRestRotation;
        private readonly Vector3 visualRestScale;
        private readonly Vector3 weaponRestPosition;
        private readonly Quaternion weaponRestRotation;
        private readonly Vector3 weaponRestScale;
        private readonly Vector3 offhandRestPosition;
        private readonly Quaternion offhandRestRotation;
        private readonly Vector3 offhandRestScale;

        private float attackElapsed = -1f;
        private float attackDuration = 0.35f;
        private bool chargeImpact;
        private bool chargingLastFrame;
        private float chargeBlend;

        public bool HasAttackMotion => style != MotionStyle.None;
        public bool WasCharging => chargingLastFrame;
        public Transform AttachmentRoot => visualRoot;

        public UnitAttackVisualAnimator(Transform unitRoot, GameObject selectionRing, int unitType, UnitData data)
        {
            style = StyleForUnitType(unitType);
            visualRoot = CreateVisualRoot(unitRoot, selectionRing);
            weaponPivot = CreateWeaponPivot(visualRoot, unitType);
            offhandPivot = CreateOffhandPivot(visualRoot, unitType);

            visualRestPosition = visualRoot.localPosition;
            visualRestRotation = visualRoot.localRotation;
            visualRestScale = visualRoot.localScale;

            if (weaponPivot != null)
            {
                weaponRestPosition = weaponPivot.localPosition;
                weaponRestRotation = weaponPivot.localRotation;
                weaponRestScale = weaponPivot.localScale;
            }

            if (offhandPivot != null)
            {
                offhandRestPosition = offhandPivot.localPosition;
                offhandRestRotation = offhandPivot.localRotation;
                offhandRestScale = offhandPivot.localScale;
            }

            chargingLastFrame = data != null && data.IsCharging;
            chargeBlend = chargingLastFrame && style == MotionStyle.MountedLance ? 1f : 0f;
            ApplyPose(data);
        }

        public Transform FindVisual(string name)
        {
            return FindDescendant(visualRoot, name);
        }

        public void PlayAttack(bool wasCharging)
        {
            if (!HasAttackMotion) return;

            attackElapsed = 0f;
            chargeImpact = wasCharging && style == MotionStyle.MountedLance;
            attackDuration = DurationForStyle(style, chargeImpact);
        }

        public void UpdateAnimation(UnitData data, float deltaTime)
        {
            if (data == null) return;

            if (attackElapsed >= 0f)
            {
                attackElapsed += deltaTime;
                if (attackElapsed >= attackDuration)
                    attackElapsed = -1f;
            }

            float chargeTarget = data.IsCharging && style == MotionStyle.MountedLance ? 1f : 0f;
            chargeBlend = Mathf.MoveTowards(chargeBlend, chargeTarget, deltaTime * 7f);

            ApplyPose(data);
            chargingLastFrame = data.IsCharging;
        }

        public void ResetPose()
        {
            attackElapsed = -1f;
            chargeBlend = 0f;
            visualRoot.localPosition = visualRestPosition;
            visualRoot.localRotation = visualRestRotation;
            visualRoot.localScale = visualRestScale;

            if (weaponPivot != null)
            {
                weaponPivot.localPosition = weaponRestPosition;
                weaponPivot.localRotation = weaponRestRotation;
                weaponPivot.localScale = weaponRestScale;
            }

            if (offhandPivot != null)
            {
                offhandPivot.localPosition = offhandRestPosition;
                offhandPivot.localRotation = offhandRestRotation;
                offhandPivot.localScale = offhandRestScale;
            }
        }

        private void ApplyPose(UnitData data)
        {
            float attackWeight = AttackRecoveryWeight();
            float cooldown = data != null && data.AttackCooldownTicks > 0
                ? Mathf.Clamp01((float)data.AttackCooldownRemaining / data.AttackCooldownTicks)
                : 0f;

            Vector3 visualPositionOffset = Vector3.zero;
            Vector3 visualEulerOffset = Vector3.zero;
            Vector3 visualScaleMultiplier = Vector3.one;
            Vector3 weaponPositionOffset = Vector3.zero;
            Vector3 weaponEulerOffset = Vector3.zero;
            Vector3 weaponScaleMultiplier = Vector3.one;
            Vector3 offhandPositionOffset = Vector3.zero;
            Vector3 offhandEulerOffset = Vector3.zero;

            switch (style)
            {
                case MotionStyle.VillagerChop:
                    visualPositionOffset.z += 0.06f * attackWeight;
                    visualEulerOffset += new Vector3(18f, 0f, 12f) * attackWeight;
                    visualScaleMultiplier.y += 0.04f * attackWeight;
                    break;

                case MotionStyle.SpearThrust:
                    visualPositionOffset.z += 0.08f * attackWeight;
                    visualEulerOffset.x -= 7f * attackWeight;
                    weaponPositionOffset.z += 0.30f * attackWeight;
                    weaponEulerOffset.x -= 8f * attackWeight;
                    break;

                case MotionStyle.BowShot:
                {
                    float reload = SmoothRange(cooldown, 0.08f, 0.9f);
                    float strength = data != null && data.UnitType == 10 ? 1.25f : 1f;
                    visualPositionOffset.z -= 0.07f * attackWeight * strength;
                    visualEulerOffset += new Vector3(10f, 0f, -5f) * attackWeight * strength;
                    weaponPositionOffset.y -= 0.06f * reload;
                    weaponPositionOffset.z += 0.10f * attackWeight * strength;
                    weaponEulerOffset += new Vector3(32f * reload - 18f * attackWeight, 0f, 18f * attackWeight) * strength;
                    weaponScaleMultiplier.x -= 0.10f * attackWeight;
                    break;
                }

                case MotionStyle.MountedLance:
                {
                    float impactStrength = chargeImpact ? 1.45f : 1f;
                    visualPositionOffset.z += 0.08f * attackWeight * impactStrength;
                    visualPositionOffset.z -= 0.025f * chargeBlend;
                    weaponPositionOffset.y -= 0.12f * chargeBlend;
                    visualEulerOffset.x += 7f * chargeBlend;
                    visualEulerOffset.z -= 3f * attackWeight * impactStrength;
                    weaponPositionOffset.z += 0.22f * attackWeight * impactStrength;
                    weaponPositionOffset.z += 0.08f * chargeBlend;
                    weaponEulerOffset.z -= 70f * chargeBlend;
                    weaponEulerOffset.z += 10f * attackWeight * impactStrength;
                    offhandEulerOffset.z += 8f * attackWeight;
                    break;
                }

                case MotionStyle.ScoutStrike:
                    visualPositionOffset.z += 0.07f * attackWeight;
                    visualEulerOffset += new Vector3(2f, -18f, 8f) * attackWeight;
                    break;

                case MotionStyle.SwordSlash:
                    visualPositionOffset.z += 0.045f * attackWeight;
                    visualEulerOffset += new Vector3(5f, -18f, -8f) * attackWeight;
                    weaponEulerOffset += new Vector3(18f, -42f, -95f) * attackWeight;
                    weaponPositionOffset.z += 0.06f * attackWeight;
                    offhandEulerOffset += new Vector3(-8f, 0f, 14f) * attackWeight;
                    offhandPositionOffset.z += 0.025f * attackWeight;
                    break;

                case MotionStyle.CrossbowShot:
                {
                    float reload = SmoothRange(cooldown, 0.12f, 0.85f);
                    visualPositionOffset.z -= 0.06f * attackWeight;
                    visualEulerOffset.x += 8f * attackWeight;
                    weaponPositionOffset.y -= 0.08f * reload;
                    weaponEulerOffset.x += 42f * reload - 28f * attackWeight;
                    weaponPositionOffset.z += 0.10f * attackWeight;
                    break;
                }

                case MotionStyle.PolearmSweep:
                    visualPositionOffset.z += 0.055f * attackWeight;
                    visualEulerOffset += new Vector3(4f, 20f, -8f) * attackWeight;
                    weaponEulerOffset += new Vector3(-15f, -88f, -38f) * attackWeight;
                    weaponPositionOffset.z += 0.08f * attackWeight;
                    break;

                case MotionStyle.RamStrike:
                    visualPositionOffset.z -= 0.08f * attackWeight;
                    visualEulerOffset.x += 3f * attackWeight;
                    weaponPositionOffset.z += 0.48f * attackWeight;
                    break;

                case MotionStyle.SiegeThrow:
                {
                    float reset = SmoothRange(cooldown, 0.05f, 0.95f);
                    float armTravel = data != null && data.UnitType == 15 ? 105f : 78f;
                    visualPositionOffset.z -= 0.045f * attackWeight;
                    visualEulerOffset.x += 3f * attackWeight;
                    weaponEulerOffset.x -= armTravel * reset;
                    break;
                }
            }

            visualRoot.localPosition = visualRestPosition + visualPositionOffset;
            visualRoot.localRotation = visualRestRotation * Quaternion.Euler(visualEulerOffset);
            visualRoot.localScale = Vector3.Scale(visualRestScale, visualScaleMultiplier);

            if (weaponPivot != null)
            {
                weaponPivot.localPosition = weaponRestPosition + weaponPositionOffset;
                weaponPivot.localRotation = weaponRestRotation * Quaternion.Euler(weaponEulerOffset);
                weaponPivot.localScale = Vector3.Scale(weaponRestScale, weaponScaleMultiplier);
            }

            if (offhandPivot != null)
            {
                offhandPivot.localPosition = offhandRestPosition + offhandPositionOffset;
                offhandPivot.localRotation = offhandRestRotation * Quaternion.Euler(offhandEulerOffset);
                offhandPivot.localScale = offhandRestScale;
            }
        }

        private float AttackRecoveryWeight()
        {
            if (attackElapsed < 0f || attackDuration <= 0f) return 0f;

            float t = Mathf.Clamp01(attackElapsed / attackDuration);
            float eased = t * t * (3f - 2f * t);
            return 1f - eased;
        }

        private static float SmoothRange(float value, float min, float max)
        {
            float t = Mathf.InverseLerp(min, max, value);
            return t * t * (3f - 2f * t);
        }

        private static float DurationForStyle(MotionStyle motionStyle, bool isChargeImpact)
        {
            switch (motionStyle)
            {
                case MotionStyle.BowShot: return 0.28f;
                case MotionStyle.CrossbowShot: return 0.34f;
                case MotionStyle.SpearThrust: return 0.30f;
                case MotionStyle.MountedLance: return isChargeImpact ? 0.46f : 0.34f;
                case MotionStyle.PolearmSweep: return 0.48f;
                case MotionStyle.RamStrike: return 0.42f;
                case MotionStyle.SiegeThrow: return 0.52f;
                case MotionStyle.VillagerChop: return 0.38f;
                default: return 0.36f;
            }
        }

        private static MotionStyle StyleForUnitType(int unitType)
        {
            switch (unitType)
            {
                case 0: return MotionStyle.VillagerChop;
                case 1: return MotionStyle.SpearThrust;
                case 2:
                case 10: return MotionStyle.BowShot;
                case 3:
                case 7:
                case 11:
                case UnitData.KingUnitType: return MotionStyle.MountedLance;
                case 4: return MotionStyle.ScoutStrike;
                case 6: return MotionStyle.SwordSlash;
                case 8: return MotionStyle.CrossbowShot;
                case 12: return MotionStyle.PolearmSweep;
                case 13: return MotionStyle.RamStrike;
                case 14:
                case 15: return MotionStyle.SiegeThrow;
                default: return MotionStyle.None;
            }
        }

        private static Transform CreateVisualRoot(Transform unitRoot, GameObject selectionRing)
        {
            var originalChildren = new List<Transform>();
            for (int i = 0; i < unitRoot.childCount; i++)
                originalChildren.Add(unitRoot.GetChild(i));

            var visualObject = new GameObject(VisualRootName);
            visualObject.layer = unitRoot.gameObject.layer;
            Transform root = visualObject.transform;
            root.SetParent(unitRoot, false);

            Transform ringTransform = selectionRing != null ? selectionRing.transform : null;
            for (int i = 0; i < originalChildren.Count; i++)
            {
                Transform child = originalChildren[i];
                if (child == ringTransform || child.name == "SelectionRing") continue;
                child.SetParent(root, false);
            }

            return root;
        }

        private static Transform CreateWeaponPivot(Transform root, int unitType)
        {
            switch (unitType)
            {
                case 1:
                case 6:
                case 12:
                    return WrapTransforms(root, WeaponPivotName,
                        FindDirectChild(root, "Speartip"), FindDirectChild(root, "Cylinder"));

                case 2:
                case 10:
                    return WrapTransforms(root, WeaponPivotName, FindGroup(root, "Bow"));

                case 3:
                case 7:
                case 11:
                case UnitData.KingUnitType:
                    return WrapTransforms(root, WeaponPivotName, FindMountedWeapon(root));

                case 8:
                    return WrapTransforms(root, WeaponPivotName, FindGroup(root, "Crossbow"));

                case 13:
                    return WrapTransforms(root, WeaponPivotName, FindDirectChild(root, "Ram"));

                case 14:
                case 15:
                    return WrapTransforms(root, WeaponPivotName, FindDirectChild(root, "Arm"));

                default:
                    return null;
            }
        }

        private static Transform CreateOffhandPivot(Transform root, int unitType)
        {
            if (unitType == 6)
                return WrapTransforms(root, OffhandPivotName, FindDirectChild(root, "NewShield"));
            if (unitType == 7)
                return WrapTransforms(root, OffhandPivotName, FindDirectChild(root, "KiteShield"));
            return null;
        }

        private static Transform WrapTransforms(Transform parent, string name, params Transform[] parts)
        {
            var validParts = new List<Transform>();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != null && !validParts.Contains(parts[i]))
                    validParts.Add(parts[i]);
            }

            if (validParts.Count == 0) return null;

            Vector3 pivotWorld = CalculateBoundsCenter(validParts);
            var pivotObject = new GameObject(name);
            pivotObject.layer = parent.gameObject.layer;
            Transform pivot = pivotObject.transform;
            pivot.SetParent(parent, false);
            pivot.position = pivotWorld;

            for (int i = 0; i < validParts.Count; i++)
                validParts[i].SetParent(pivot, true);

            return pivot;
        }

        private static Vector3 CalculateBoundsCenter(List<Transform> parts)
        {
            bool hasBounds = false;
            Bounds bounds = default;
            Vector3 positionSum = Vector3.zero;

            for (int i = 0; i < parts.Count; i++)
            {
                positionSum += parts[i].position;
                var renderers = parts[i].GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    if (!hasBounds)
                    {
                        bounds = renderers[r].bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderers[r].bounds);
                    }
                }
            }

            return hasBounds ? bounds.center : positionSum / parts.Count;
        }

        /// <summary>
        /// Finds the lance a mounted unit carries. Matched on the tip by prefix rather than on an
        /// exact "Speartip (1)": the King's lance names its tip plainly "Speartip", and an exact
        /// match quietly found nothing, leaving him to attack by shoving his horse forward.
        /// </summary>
        private static Transform FindMountedWeapon(Transform root)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (FindDescendantWithPrefix(child, "Speartip") != null)
                    return child;
            }
            return null;
        }

        private static Transform FindDescendantWithPrefix(Transform root, string prefix)
        {
            if (root == null) return null;
            if (root.name.StartsWith(prefix, System.StringComparison.Ordinal)) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform match = FindDescendantWithPrefix(root.GetChild(i), prefix);
                if (match != null) return match;
            }
            return null;
        }

        private static Transform FindGroup(Transform root, string name)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == name && transforms[i].childCount > 0)
                    return transforms[i];
            }
            return null;
        }

        private static Transform FindDirectChild(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == name) return child;
            }
            return null;
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
