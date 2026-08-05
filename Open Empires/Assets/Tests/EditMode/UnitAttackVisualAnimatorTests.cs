using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public class UnitAttackVisualAnimatorTests
    {
        [Test]
        public void Constructor_SeparatesVisualsFromSelectionRing()
        {
            var root = new GameObject("Unit");
            try
            {
                Transform body = CreateChild(root.transform, "Body");
                Transform ring = CreateChild(root.transform, "SelectionRing");

                _ = new UnitAttackVisualAnimator(root.transform, ring.gameObject, 4, CreateUnitData(4));

                Assert.That(root.transform.Find("SelectionRing"), Is.EqualTo(ring));
                Assert.That(root.transform.Find("VisualRoot/Body"), Is.EqualTo(body));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SpearAttack_AnimatesWeaponWithoutMovingUnitRoot()
        {
            var root = new GameObject("Spearman");
            try
            {
                CreateChild(root.transform, "Body");
                Transform spearTip = CreateChild(root.transform, "Speartip");
                CreateChild(spearTip, "Cube");
                CreateChild(root.transform, "Cylinder");
                Transform ring = CreateChild(root.transform, "SelectionRing");

                UnitData data = CreateUnitData(1);
                data.AttackCooldownTicks = 10;
                var animator = new UnitAttackVisualAnimator(root.transform, ring.gameObject, 1, data);
                Transform weaponPivot = root.transform.Find("VisualRoot/AttackWeaponPivot");
                Vector3 restPosition = weaponPivot.localPosition;
                Vector3 rootPosition = root.transform.position;

                animator.PlayAttack(false);
                animator.UpdateAnimation(data, 0.01f);

                Assert.That(weaponPivot.localPosition.z, Is.GreaterThan(restPosition.z + 0.2f));
                Assert.That(root.transform.position, Is.EqualTo(rootPosition));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MountedCharge_LowersLanceIntoChargePose()
        {
            var root = new GameObject("Knight");
            try
            {
                CreateChild(root.transform, "HorseBody");
                Transform lance = CreateChild(root.transform, "GameObject");
                CreateChild(lance, "Speartip (1)");
                CreateChild(lance, "Cylinder (1)");
                Transform ring = CreateChild(root.transform, "SelectionRing");

                UnitData data = CreateUnitData(7);
                data.AttackCooldownTicks = 10;
                var animator = new UnitAttackVisualAnimator(root.transform, ring.gameObject, 7, data);
                Transform weaponPivot = root.transform.Find("VisualRoot/AttackWeaponPivot");
                Quaternion restRotation = weaponPivot.localRotation;

                data.IsCharging = true;
                animator.UpdateAnimation(data, 0.2f);

                Assert.That(Quaternion.Angle(restRotation, weaponPivot.localRotation), Is.GreaterThan(20f));
                Assert.That(animator.WasCharging, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase("Assets/Prefabs/Units/Spearman.prefab", 1)]
        [TestCase("Assets/Prefabs/Units/Archer.prefab", 2)]
        [TestCase("Assets/Prefabs/Units/Horseman.prefab", 3)]
        [TestCase("Assets/Prefabs/Units/ManAtArms.prefab", 6)]
        [TestCase("Assets/Prefabs/Units/Knight.prefab", 7)]
        [TestCase("Assets/Prefabs/Units/Crossbowman.prefab", 8)]
        [TestCase("Assets/Prefabs/Units/Longbowman.prefab", 10)]
        [TestCase("Assets/Prefabs/Units/Gendarme.prefab", 11)]
        [TestCase("Assets/Prefabs/Units/Landsknecht.prefab", 12)]
        public void WeaponPrefab_CreatesAttackPivot(string prefabPath, int unitType)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                Transform ring = FindDescendant(instance.transform, "SelectionRing");
                UnitData data = CreateUnitData(unitType);

                var animator = new UnitAttackVisualAnimator(
                    instance.transform, ring != null ? ring.gameObject : null, unitType, data);

                Assert.That(animator.HasAttackMotion, Is.True);
                Assert.That(instance.transform.Find("VisualRoot/AttackWeaponPivot"), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static UnitData CreateUnitData(int unitType)
        {
            var position = new FixedVector3(Fixed32.Zero, Fixed32.Zero, Fixed32.Zero);
            return new UnitData(0, 0, position, Fixed32.One, Fixed32.One, Fixed32.One)
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
    }
}
