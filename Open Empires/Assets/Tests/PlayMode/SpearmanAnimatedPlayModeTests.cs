using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpenEmpires.Tests
{
    public sealed class SpearmanAnimatedPlayModeTests
    {
        private const string PrefabPath = "Assets/Models/Units/Spearman/Spearman_Animated.prefab";

        [UnityTest]
        public IEnumerator Candidate_SpawnsThroughGameSetupAndCompletesExistingCorpseLifecycle()
        {
            GameObject setupObject = new GameObject("Spearman PlayMode GameSetup");
            GameSetup setup = setupObject.AddComponent<GameSetup>();
            Material legacyPlayerMaterial = CreateMaterial("Legacy Player Material", Color.red);
            Material stencilMaterial = CreateMaterial("Test Stencil", Color.white);
            Material silhouetteMaterial = CreateMaterial("Test Silhouette", Color.white);
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);

            SetField(setup, "playerMaterials", new[] { legacyPlayerMaterial });
            SetField(setup, "playerSilhouetteMaterials", new[] { silhouetteMaterial });
            SetField(setup, "unitStencilMat", stencilMaterial);

            var position = new FixedVector3(Fixed32.Zero, Fixed32.Zero, Fixed32.Zero);
            var data = new UnitData(7001, 0, position, Fixed32.One, Fixed32.One, Fixed32.One)
            {
                UnitType = 1,
                MaxHealth = 80,
                CurrentHealth = 80,
                AttackDamage = 8,
                AttackCooldownTicks = 30
            };
            int originalHealth = data.CurrentHealth;
            FixedVector3 originalPosition = data.SimPosition;

            MethodInfo spawnUnit = typeof(GameSetup).GetMethod("SpawnUnit", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(spawnUnit, Is.Not.Null);
            spawnUnit.Invoke(setup, new object[] { prefab, data, Vector3.zero, 1 });
            yield return null;

            var unitViews = (Dictionary<int, UnitView>)GetField(setup, "unitViews");
            Assert.That(unitViews.TryGetValue(data.Id, out UnitView view), Is.True);
            GameObject spawned = view.gameObject;
            UnitAnimatorVisualDriver driver = spawned.GetComponent<UnitAnimatorVisualDriver>();
            Animator animator = spawned.GetComponentInChildren<Animator>(true);
            Assert.That(driver, Is.Not.Null);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.applyRootMotion, Is.False);

            view.SetSelected(true);
            Transform selectionRing = FindDescendant(spawned.transform, "SelectionRing");
            Assert.That(selectionRing, Is.Not.Null);
            Assert.That(selectionRing.gameObject.activeInHierarchy, Is.True);
            Assert.That(GetField(view, "healthBarRoot"), Is.Not.Null);

            Renderer teamRenderer = System.Array.Find(spawned.GetComponentsInChildren<Renderer>(true),
                renderer => renderer.name.EndsWith("_Team", System.StringComparison.Ordinal));
            Assert.That(teamRenderer, Is.Not.Null);
            Assert.That(teamRenderer.sharedMaterial.name, Does.Contain("Player0_TeamTint"));
            Assert.That(teamRenderer.sharedMaterial.mainTexture, Is.Not.Null,
                "Team tinting must preserve the textured atlas.");

            driver.UpdatePresentation(1f, true, true);
            Assert.That(animator.GetBool("IsCharging"), Is.True);
            Assert.That(animator.GetBool("InCombat"), Is.True);
            driver.PlayAttack();
            driver.PlayHit();

            spawned.SetActive(false);
            Assert.That(teamRenderer.gameObject.activeInHierarchy, Is.False,
                "Fog visibility can hide the complete skinned visual hierarchy.");
            spawned.SetActive(true);

            Assert.That(data.CurrentHealth, Is.EqualTo(originalHealth));
            Assert.That(data.SimPosition, Is.EqualTo(originalPosition));

            float oldTimeScale = Time.timeScale;
            Time.timeScale = 100f;
            view.OnDeath();
            Assert.That(spawned.GetComponent<Collider>().enabled, Is.False);
            Assert.That(driver.IsDeathPresentationActive, Is.True);

            float timeout = Time.realtimeSinceStartup + 2f;
            while (spawned != null && Time.realtimeSinceStartup < timeout)
                yield return null;

            Time.timeScale = oldTimeScale;
            Assert.That(spawned == null, Is.True,
                "Animated death must still finish the existing wait, fade, and removal lifecycle.");

            Object.Destroy(setupObject);
            Object.Destroy(legacyPlayerMaterial);
            Object.Destroy(stencilMaterial);
            Object.Destroy(silhouetteMaterial);
        }

        private static Material CreateMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            return material;
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field '{name}' was not found.");
            return field.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field '{name}' was not found.");
            field.SetValue(target, value);
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
