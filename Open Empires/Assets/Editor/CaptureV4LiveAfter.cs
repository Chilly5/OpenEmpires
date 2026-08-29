using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenEmpires.EditorTests
{
    public static class CaptureV4LiveAfter
    {
        private const string OutputBase = @"D:\unity_projects\OpenEmpiresTemp\Spearman_Animation_V4";
        private const string PrefabPath = "Assets/Models/Units/Spearman/Spearman_Animated.prefab";
        private const string FbxPath = "Assets/Models/Units/Spearman/SM_Spearman.fbx";

        public static void Run()
        {
            string screenshotsDir = Path.Combine(OutputBase, "Screenshots");
            string framesDir = Path.Combine(OutputBase, "raw_frames");
            Directory.CreateDirectory(screenshotsDir);
            Directory.CreateDirectory(framesDir);

            Debug.Log("[V4 LIVE CAPTURE] Opening SampleScene for live V4 visual certification...");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            var gameManager = GameObject.Find("GameManager");
            var gameSetup = gameManager.GetComponent<GameSetup>();
            var gameBootstrapper = gameManager.GetComponent<GameBootstrapper>();

            var so = new SerializedObject(gameSetup);
            var spearmanPrefabProp = so.FindProperty("spearmanPrefab");
            var assignedPrefab = spearmanPrefabProp?.objectReferenceValue as GameObject;

            var config = AssetDatabase.LoadAssetAtPath<SimulationConfig>("Assets/ScriptableObjects/SimulationConfig.asset");
            var sim = new GameSimulation(config, 1, new int[] { 0, 1 }, new int[0], AIDifficulty.Medium);

            if (gameBootstrapper != null)
            {
                typeof(GameBootstrapper).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetSetMethod(true)?.Invoke(null, new object[] { gameBootstrapper });
                typeof(GameBootstrapper).GetProperty("Simulation", BindingFlags.Instance | BindingFlags.Public)?.GetSetMethod(true)?.Invoke(gameBootstrapper, new object[] { sim });
            }

            int playerCount = 2;
            var computeColorsMethod = typeof(GameSetup).GetMethod("ComputePlayerColors", BindingFlags.Instance | BindingFlags.NonPublic);
            computeColorsMethod?.Invoke(gameSetup, new object[] { playerCount, new int[] { 0, 1 } });

            var playerColorsProp = typeof(GameSetup).GetProperty("PlayerColors", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Color[] playerColors = playerColorsProp?.GetValue(null) as Color[] ?? new Color[] { new Color(0.2f, 0.4f, 0.9f), new Color(0.9f, 0.2f, 0.2f) };

            var villagerPrefabProp = so.FindProperty("villagerPrefab");
            var villagerPrefab = villagerPrefabProp?.objectReferenceValue as GameObject;
            var baseMat = villagerPrefab != null ? villagerPrefab.GetComponentInChildren<Renderer>()?.sharedMaterial : null;
            if (baseMat == null) baseMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));

            var playerMats = new Material[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                playerMats[i] = new Material(baseMat) { name = $"PlayerMaterial_{i}" };
                playerMats[i].color = playerColors[i];
            }
            typeof(GameSetup).GetField("playerMaterials", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(gameSetup, playerMats);

            var silhouetteShader = Shader.Find("Custom/Silhouette") ?? Shader.Find("Universal Render Pipeline/Lit");
            var playerSilMats = new Material[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                playerSilMats[i] = new Material(silhouetteShader) { name = $"Silhouette_{i}" };
                playerSilMats[i].renderQueue = 2451;
            }
            typeof(GameSetup).GetField("playerSilhouetteMaterials", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(gameSetup, playerSilMats);

            var unitStencilMat = new Material(Shader.Find("Custom/UnitStencilWrite") ?? Shader.Find("Universal Render Pipeline/Lit")) { name = "UnitStencil" };
            typeof(GameSetup).GetField("unitStencilMat", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(gameSetup, unitStencilMat);

            var selRingShader = Shader.Find("Custom/SelectionRing") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var selRingMat = new Material(selRingShader) { name = "SelectionRing" };
            typeof(GameSetup).GetField("sharedSelectionRingMat", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(gameSetup, selRingMat);

            var unitViewsDict = new Dictionary<int, UnitView>();
            typeof(GameSetup).GetField("unitViews", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(gameSetup, unitViewsDict);

            var urpAsset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (urpAsset != null)
            {
                GraphicsSettings.defaultRenderPipeline = urpAsset;
                QualitySettings.renderPipeline = urpAsset;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.65f, 0.70f, 0.75f, 1.0f);

            var dirLightGo = GameObject.Find("Directional Light");
            if (dirLightGo != null)
            {
                var light = dirLightGo.GetComponent<Light>();
                if (light != null)
                {
                    light.intensity = 1.5f;
                    light.color = new Color(1.0f, 0.95f, 0.90f);
                }
            }

            var spawnUnitMethod = typeof(GameSetup).GetMethod("SpawnUnit", BindingFlags.Instance | BindingFlags.NonPublic);

            Vector3 spawnPos = new Vector3(30f, 0f, 30f);
            var fixedPos = FixedVector3.FromVector3(spawnPos);
            var unitData = sim.UnitRegistry.CreateUnit(0, fixedPos,
                sim.ConfigToFixed32(config.UnitMoveSpeed),
                sim.ConfigToFixed32(config.UnitRadius),
                sim.ConfigToFixed32(config.SpearmanMass));
            unitData.UnitType = 1;
            unitData.MaxHealth = config.SpearmanMaxHealth;
            unitData.CurrentHealth = config.SpearmanMaxHealth;
            unitData.AttackDamage = config.SpearmanAttackDamage;
            unitData.AttackRange = sim.ConfigToFixed32(config.SpearmanAttackRange);
            spawnUnitMethod.Invoke(gameSetup, new object[] { assignedPrefab, unitData, spawnPos, 1 });

            var liveUnitView = unitViewsDict[unitData.Id];
            var liveGO = liveUnitView.gameObject;
            liveUnitView.SetSelected(true);

            var animator = liveGO.GetComponentInChildren<Animator>(true);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var mainCam = GameObject.FindWithTag("MainCamera")?.GetComponent<Camera>() ?? Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            mainCam.transform.SetParent(null);
            mainCam.fieldOfView = 35f;
            mainCam.nearClipPlane = 0.1f;
            mainCam.farClipPlane = 500f;

            Vector3 unitPos = liveGO.transform.position;

            // 1. Idle Pose
            animator.Play("Idle", 0, 0.5f);
            animator.Update(0f);
            mainCam.transform.position = unitPos + new Vector3(0f, 3.2f, -4.5f);
            mainCam.transform.LookAt(unitPos + new Vector3(0f, 0.9f, 0f));
            CaptureScreenshot(mainCam, Path.Combine(screenshotsDir, "live_spearman_idle.png"));

            // 2. Close Shot
            mainCam.transform.position = unitPos + new Vector3(0f, 2.2f, -3.2f);
            mainCam.transform.LookAt(unitPos + new Vector3(0f, 0.95f, 0f));
            CaptureScreenshot(mainCam, Path.Combine(screenshotsDir, "live_spearman_close.png"));

            // 3. Tactical Shot
            mainCam.transform.position = unitPos + new Vector3(0f, 15f, -15f);
            mainCam.transform.LookAt(unitPos + new Vector3(0f, 0.5f, 0f));
            CaptureScreenshot(mainCam, Path.Combine(screenshotsDir, "live_spearman_tactical.png"));

            // 4. Far Shot
            mainCam.transform.position = unitPos + new Vector3(0f, 30f, -30f);
            mainCam.transform.LookAt(unitPos + new Vector3(0f, 0.5f, 0f));
            CaptureScreenshot(mainCam, Path.Combine(screenshotsDir, "live_spearman_far.png"));

            // 5. Walk Pose
            animator.Play("Walk", 0, 0.35f);
            animator.Update(0f);
            mainCam.transform.position = unitPos + new Vector3(1.5f, 3.0f, -4.2f);
            mainCam.transform.LookAt(unitPos + new Vector3(0f, 0.9f, 0f));
            CaptureScreenshot(mainCam, Path.Combine(screenshotsDir, "live_spearman_walk.png"));

            // 6. Charge Pose
            animator.Play("RunCharge", 0, 0.40f);
            animator.Update(0f);
            CaptureScreenshot(mainCam, Path.Combine(screenshotsDir, "live_spearman_charge.png"));

            // 7. Attack Pose
            animator.Play("Attack", 0, 0.45f);
            animator.Update(0f);
            CaptureScreenshot(mainCam, Path.Combine(screenshotsDir, "live_spearman_attack.png"));

            // 8. Block Pose
            animator.Play("Block", 0, 0.50f);
            animator.Update(0f);
            CaptureScreenshot(mainCam, Path.Combine(screenshotsDir, "live_spearman_block.png"));

            // Video Frame Rendering Matrix:
            // Actions: Idle (80 frames), Walk (25 frames), Attack (22 frames), RunCharge (17 frames), Block (35 frames)
            var cams = new (string Name, Vector3 Offset, Vector3 LookTarget)[]
            {
                ("cam_a_tactical", new Vector3(0f, 14f, -14f), new Vector3(0f, 0.5f, 0f)),
                ("cam_b_close",    new Vector3(1.2f, 2.0f, -2.8f), new Vector3(0f, 0.95f, 0f)),
                ("cam_c_side",     new Vector3(3.0f, 1.2f, 0.0f), new Vector3(0f, 0.90f, 0f))
            };

            var actions = new (string StateName, int TotalFrames)[]
            {
                ("Idle", 80),
                ("Walk", 25),
                ("Attack", 22),
                ("RunCharge", 17),
                ("Block", 35)
            };

            foreach (var act in actions)
            {
                foreach (var cam in cams)
                {
                    string actionDir = Path.Combine(framesDir, $"{act.StateName}_{cam.Name}");
                    Directory.CreateDirectory(actionDir);

                    mainCam.transform.position = unitPos + cam.Offset;
                    mainCam.transform.LookAt(unitPos + cam.LookTarget);

                    Debug.Log($"[V4 FRAME CAPTURE] Rendering {act.TotalFrames} frames for {act.StateName} ({cam.Name})...");

                    for (int f = 0; f < act.TotalFrames; f++)
                    {
                        float normTime = (float)f / (float)act.TotalFrames;
                        animator.Play(act.StateName, 0, normTime);
                        animator.Update(0f);

                        string framePath = Path.Combine(actionDir, $"frame_{f:D4}.png");
                        CaptureScreenshot(mainCam, framePath);
                    }
                }
            }

            Debug.Log("[V4 LIVE CAPTURE] Complete! All screenshots and raw frame sequences generated successfully.");
        }

        private static void CaptureScreenshot(Camera cam, string path)
        {
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            var currentRT = RenderTexture.active;
            RenderTexture.active = rt;

            cam.Render();

            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();

            cam.targetTexture = null;
            RenderTexture.active = currentRT;
            UnityEngine.Object.DestroyImmediate(rt);

            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
