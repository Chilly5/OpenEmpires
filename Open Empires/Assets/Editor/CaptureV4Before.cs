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
    public static class CaptureV4Before
    {
        private const string BaseOutputDir = @"D:\unity_projects\OpenEmpiresTemp\Spearman_Animation_V4\V4_BEFORE";
        private const string FramesDir = @"D:\unity_projects\OpenEmpiresTemp\Spearman_Animation_V4\V4_BEFORE\raw_frames";
        private const string PrefabPath = "Assets/Models/Units/Spearman/Spearman_Animated.prefab";

        public static void Run()
        {
            Directory.CreateDirectory(BaseOutputDir);
            Directory.CreateDirectory(FramesDir);

            Debug.Log("[V4 BEFORE] Opening SampleScene to record baseline videos...");
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

            // Camera Setup Definitions:
            // Camera A: Tactical Gameplay (Normal RTS angle: 10m height, -12m distance, looking at chest)
            Action setCamA = () => {
                mainCam.transform.position = unitPos + new Vector3(0f, 10f, -12f);
                mainCam.transform.LookAt(unitPos + new Vector3(0f, 0.8f, 0f));
            };

            // Camera B: Close Inspection (2.2m height, -3.2m distance)
            Action setCamB = () => {
                mainCam.transform.position = unitPos + new Vector3(0.5f, 2.2f, -3.2f);
                mainCam.transform.LookAt(unitPos + new Vector3(0f, 0.95f, 0f));
            };

            // Camera C: Side Profile (Pure side view for locomotion & thrust path)
            Action setCamC = () => {
                mainCam.transform.position = unitPos + new Vector3(4.5f, 1.2f, 0f);
                mainCam.transform.LookAt(unitPos + new Vector3(0f, 0.9f, 0f));
            };

            var cams = new (string Name, Action Setup)[] {
                ("tactical", setCamA),
                ("close", setCamB),
                ("side", setCamC)
            };

            float fps = 60f;
            int frames5s = 300; // 5.0 seconds @ 60fps

            // Render all baseline states
            foreach (var cam in cams)
            {
                cam.Setup();

                // 1. Idle (5s)
                RenderClipSequence(animator, mainCam, "Idle", 2.467f, frames5s, fps, $"before_idle_{cam.Name}");

                // 2. Walk (5s)
                RenderClipSequence(animator, mainCam, "Walk", 0.767f, frames5s, fps, $"before_walk_{cam.Name}");

                // 3. Attack (5 repeated attacks: 5 * 0.933s = ~4.67s -> 280 frames)
                RenderClipSequence(animator, mainCam, "Attack", 0.933f, 280, fps, $"before_attack_{cam.Name}");

                // 4. Charge (5s)
                RenderClipSequence(animator, mainCam, "RunCharge", 0.567f, frames5s, fps, $"before_charge_{cam.Name}");

                // 5. Block / Guard (5s)
                RenderClipSequence(animator, mainCam, "Block", 1.167f, frames5s, fps, $"before_block_{cam.Name}");
            }

            Debug.Log("[V4 BEFORE] All baseline frames rendered successfully!");
        }

        private static void RenderClipSequence(Animator anim, Camera cam, string stateName, float clipLen, int frameCount, float fps, string prefix)
        {
            string seqDir = Path.Combine(FramesDir, prefix);
            Directory.CreateDirectory(seqDir);

            for (int f = 0; f < frameCount; f++)
            {
                float time = f / fps;
                float normTime = (time / clipLen) % 1.0f;
                anim.Play(stateName, 0, normTime);
                anim.Update(0f);

                string framePath = Path.Combine(seqDir, $"frame_{f:D5}.png");
                CaptureScreenshot(cam, framePath);
            }
        }

        private static void CaptureScreenshot(Camera cam, string outputPath)
        {
            int w = 1920;
            int h = 1080;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = prevTarget;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            File.WriteAllBytes(outputPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
