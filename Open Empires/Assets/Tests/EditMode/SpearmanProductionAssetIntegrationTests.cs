using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public class SpearmanProductionAssetIntegrationTests
    {
        private const string BaseSpearmanPrefabPath = "Assets/Prefabs/Units/Spearman.prefab";
        private const string ProductionModelFbxPath = "Assets/Models/Units/SM_Spearman.fbx";

        [Test]
        public void ProductionFBX_ImportsWithZeroRigsAndZeroAnimators()
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ProductionModelFbxPath);
            Assert.That(modelAsset, Is.Not.Null, "SM_Spearman.fbx must import successfully into Unity Assets.");

            GameObject instance = Object.Instantiate(modelAsset);
            try
            {
                // Strict zero-rig / zero-animator verification
                var animators = instance.GetComponentsInChildren<Animator>(true);
                var animations = instance.GetComponentsInChildren<Animation>(true);
                var skinnedRenderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);

                Assert.That(animators.Length, Is.EqualTo(0), "Production asset must contain zero Animator components.");
                Assert.That(animations.Length, Is.EqualTo(0), "Production asset must contain zero Animation components.");
                Assert.That(skinnedRenderers.Length, Is.EqualTo(0), "Production asset must contain zero SkinnedMeshRenderers (rigid mesh only).");

                // Verify all 6 modular parts exist
                string[] requiredParts = new string[] { "Body_Team", "Helmet", "Limbs_Boots", "Speartip", "Cylinder", "RoundShield" };
                foreach (string part in requiredParts)
                {
                    Transform t = FindDescendant(instance.transform, part);
                    Assert.That(t, Is.Not.Null, $"Modular component '{part}' must exist in FBX hierarchy.");
                    var mf = t.GetComponent<MeshFilter>();
                    Assert.That(mf != null && mf.sharedMesh != null, $"Modular component '{part}' must have a valid MeshFilter and Mesh.");
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void SpearmanVisualHierarchy_PreservesLocalObjectSpacePivots()
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ProductionModelFbxPath);
            Assert.That(modelAsset, Is.Not.Null);

            GameObject instance = Object.Instantiate(modelAsset);
            try
            {
                Transform body = FindDescendant(instance.transform, "Body_Team");
                Transform helm = FindDescendant(instance.transform, "Helmet");
                Transform limbs = FindDescendant(instance.transform, "Limbs_Boots");
                Transform tip = FindDescendant(instance.transform, "Speartip");
                Transform shaft = FindDescendant(instance.transform, "Cylinder");
                Transform shield = FindDescendant(instance.transform, "RoundShield");

                Assert.That(body, Is.Not.Null);
                Assert.That(helm, Is.Not.Null);
                Assert.That(limbs, Is.Not.Null);
                Assert.That(tip, Is.Not.Null);
                Assert.That(shaft, Is.Not.Null);
                Assert.That(shield, Is.Not.Null);

                Debug.Log($"[PIVOT AUDIT] Helmet local: {helm.localPosition}, world: {helm.position}");
                Debug.Log($"[PIVOT AUDIT] Speartip local: {tip.localPosition}, world: {tip.position}");
                Debug.Log($"[PIVOT AUDIT] Cylinder local: {shaft.localPosition}, world: {shaft.position}");
                Debug.Log($"[PIVOT AUDIT] RoundShield local: {shield.localPosition}, world: {shield.position}");

                // In Unity coordinate space (Y-up), height is Y or Z depending on FBX coordinate import
                float helmHeight = Mathf.Max(Mathf.Abs(helm.localPosition.y), Mathf.Abs(helm.localPosition.z));
                Assert.That(helmHeight, Is.EqualTo(0.94f).Within(0.04f), "Helmet local vertical pivot must be ~0.94m.");

                float tipHeight = Mathf.Max(Mathf.Abs(tip.localPosition.y), Mathf.Abs(tip.localPosition.z));
                Assert.That(tipHeight, Is.EqualTo(1.82f).Within(0.04f), "Speartip local vertical pivot must be ~1.82m.");

                float shaftHeight = Mathf.Max(Mathf.Abs(shaft.localPosition.y), Mathf.Abs(shaft.localPosition.z));
                Assert.That(shaftHeight, Is.EqualTo(0.55f).Within(0.04f), "Cylinder local vertical pivot must be ~0.55m.");

                float shieldHeight = Mathf.Max(Mathf.Abs(shield.localPosition.y), Mathf.Abs(shield.localPosition.z));
                Assert.That(shieldHeight, Is.EqualTo(0.57f).Within(0.04f), "RoundShield local vertical pivot must be ~0.57m.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void SpearmanVisualHierarchy_UnitAttackVisualAnimator_DetectsWeaponAndCreatesAttackPivot()
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ProductionModelFbxPath);
            Assert.That(modelAsset, Is.Not.Null);

            var root = new GameObject("Spearman_TestRoot");
            try
            {
                GameObject modelInstance = Object.Instantiate(modelAsset);
                try
                {
                    // Move modular children to unit root (matching runtime setup)
                    for (int i = modelInstance.transform.childCount - 1; i >= 0; i--)
                    {
                        Transform child = modelInstance.transform.GetChild(i);
                        child.SetParent(root.transform, false);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(modelInstance);
                }

                var ring = new GameObject("SelectionRing");
                ring.transform.SetParent(root.transform, false);

                UnitData data = CreateUnitData(1);
                var animator = new UnitAttackVisualAnimator(root.transform, ring, 1, data);

                Assert.That(animator.HasAttackMotion, Is.True, "Spearman (unitType 1) must have attack motion.");

                Transform visualRoot = root.transform.Find("VisualRoot");
                Assert.That(visualRoot, Is.Not.Null, "VisualRoot must be created.");

                Transform weaponPivot = visualRoot.Find("AttackWeaponPivot");
                Assert.That(weaponPivot, Is.Not.Null, "AttackWeaponPivot must wrap Speartip and Cylinder.");

                Transform tipInPivot = weaponPivot.Find("Speartip");
                Transform shaftInPivot = weaponPivot.Find("Cylinder");
                Assert.That(tipInPivot, Is.Not.Null, "Speartip must be grouped under AttackWeaponPivot.");
                Assert.That(shaftInPivot, Is.Not.Null, "Cylinder must be grouped under AttackWeaponPivot.");

                // Test Attack Thrust compatibility simulation (+0.30m forward thrust along weapon local axis)
                Vector3 restWeaponPos = weaponPivot.localPosition;
                Vector3 restVisualPos = visualRoot.localPosition;

                animator.PlayAttack(false);
                animator.UpdateAnimation(data, 0.01f);

                Assert.That(weaponPivot.localPosition.z, Is.GreaterThan(restWeaponPos.z + 0.20f), "Weapon must thrust forward by ~0.30m along attack axis.");
                Assert.That(visualRoot.localPosition.z, Is.GreaterThan(restVisualPos.z + 0.04f), "Body must step forward slightly during spear thrust.");

                // Reset pose
                animator.ResetPose();
                Assert.That(weaponPivot.localPosition, Is.EqualTo(restWeaponPos).Using(Vector3ComparerWithEpsilon(0.001f)));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SpearmanVisualHierarchy_SelectionSilhouetteAndTeamColors_Compatible()
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ProductionModelFbxPath);
            Assert.That(modelAsset, Is.Not.Null);

            GameObject instance = Object.Instantiate(modelAsset);
            try
            {
                var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(renderers.Length, Is.GreaterThanOrEqualTo(6), "All 6 modular components must have MeshRenderers.");

                // Verify shader compatibility for team colors and dynamic tinting
                int BaseColorId = Shader.PropertyToID("_BaseColor");
                int ColorId = Shader.PropertyToID("_Color");

                foreach (var renderer in renderers)
                {
                    Assert.That(renderer.sharedMaterial, Is.Not.Null, $"Renderer on '{renderer.gameObject.name}' must have a valid Material.");
                    Material mat = renderer.sharedMaterial;
                    bool hasColorProperty = mat.HasProperty(BaseColorId) || mat.HasProperty(ColorId);
                    Assert.That(hasColorProperty, Is.True, $"Material '{mat.name}' on '{renderer.gameObject.name}' must support _BaseColor or _Color for team coloring.");
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static UnitData CreateUnitData(int unitType)
        {
            var position = new FixedVector3(Fixed32.Zero, Fixed32.Zero, Fixed32.Zero);
            return new UnitData(101, 0, position, Fixed32.One, Fixed32.One, Fixed32.One)
            {
                UnitType = unitType
            };
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindDescendant(root.GetChild(i), name);
                if (result != null) return result;
            }
            return null;
        }

        private static System.Collections.Generic.IEqualityComparer<Vector3> Vector3ComparerWithEpsilon(float eps)
        {
            return new Vector3EqualityComparer(eps);
        }

        private class Vector3EqualityComparer : System.Collections.Generic.IEqualityComparer<Vector3>
        {
            private readonly float epsilon;
            public Vector3EqualityComparer(float eps) { epsilon = eps; }
            public bool Equals(Vector3 a, Vector3 b) => Vector3.Distance(a, b) <= epsilon;
            public int GetHashCode(Vector3 obj) => obj.GetHashCode();
        }
    }
}
