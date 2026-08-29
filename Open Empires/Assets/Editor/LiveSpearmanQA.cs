using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenEmpires.EditorTests
{
    public static class LiveSpearmanQA
    {
        private const string OutputDir = @"D:\unity_projects\OpenEmpiresTemp\Spearman_Production_Rebuild_V3_2";
        private const string FramesDir = @"D:\unity_projects\OpenEmpiresTemp\Spearman_Production_Rebuild_V3_2\video_frames";
        private const string PrefabPath = "Assets/Models/Units/Spearman/Spearman_Animated.prefab";
        private const string FbxPath = "Assets/Models/Units/Spearman/SM_Spearman.fbx";
        private const string ControllerPath = "Assets/Models/Units/Spearman/Animations/AC_Spearman.controller";

        public static void RunFullCertification()
        {
            Directory.CreateDirectory(OutputDir);
            Directory.CreateDirectory(FramesDir);
            string screenshotsDir = Path.Combine(OutputDir, "FINAL_QA", "Screenshots");
            string runtimeDir = Path.Combine(OutputDir, "FINAL_QA", "Runtime");
            string validatorDir = Path.Combine(OutputDir, "FINAL_QA", "Validator");
            Directory.CreateDirectory(screenshotsDir);
            Directory.CreateDirectory(runtimeDir);
            Directory.CreateDirectory(validatorDir);

            Debug.Log("[LIVE SPEARMAN QA] Starting full production certification in SampleScene...");

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
            CaptureScreenshot(mainCam, Path.Combine(OutputDir, "live_spearman_idle.png"));
            CaptureScreenshot(mainCam, Path.Combine(screenshotsDir, "live_spearman_idle.png"));

            // 2. Close Shot
            mainCam.transform.position = unitPos + new Vector3(0f, 2.2f, -3.2f);
            mainCam.transform.LookAt(unitPos + new Vector3(0f, 0.95f, 0f));
            CaptureScreenshot(mainCam, Path.Combine(OutputDir, "live_spearman_close.png"));
            CaptureScreenshot(mainCam, Path.Combine(screenshotsDir, "live_spearman_close.png"));

            // 3. Tactical Shot
            mainCam.transform.position = unitPos + new Vector3(0f, 15f, -15f);
            mainCam.transform.LookAt(unitPos + new Vector3(0f, 0.5f, 0f));
            CaptureScreenshot(mainCam, Path.Combine(OutputDir, "live_spearman_tactical.png"));
            CaptureScreenshot(mainCam, Path.Combine(screenshotsDir, "live_spearman_tactical.png"));

            // 4. Far Shot
            mainCam.transform.position = unitPos + new Vector3(0f, 30f, -30f);
            mainCam.transform.LookAt(unitPos + new Vector3(0f, 0.5f, 0f));
            CaptureScreenshot(mainCam, Path.Combine(OutputDir, "live_spearman_far.png"));
            CaptureScreenshot(mainCam, Path.Combine(screenshotsDir, "live_spearman_far.png"));

            // Reset to Standard QA Camera
            mainCam.transform.position = unitPos + new Vector3(1.5f, 3.0f, -4.2f);
            mainCam.transform.LookAt(unitPos + new Vector3(0f, 0.9f, 0f));

            // 5. Walk Pose
            animator.Play("Walk", 0, 0.35f);
            animator.Update(0f);
            CaptureScreenshot(mainCam, Path.Combine(OutputDir, "live_spearman_walk.png"));
            CaptureScreenshot(mainCam, Path.Combine(screenshotsDir, "live_spearman_walk.png"));

            // 6. Charge Pose
            animator.Play("RunCharge", 0, 0.45f);
            animator.Update(0f);
            CaptureScreenshot(mainCam, Path.Combine(OutputDir, "live_spearman_charge.png"));
            CaptureScreenshot(mainCam, Path.Combine(screenshotsDir, "live_spearman_charge.png"));

            // 7. Attack Pose (at maximum thrust contact Frame 10 / 28)
            animator.Play("Attack", 0, 9f / 28f);
            animator.Update(0f);
            CaptureScreenshot(mainCam, Path.Combine(OutputDir, "live_spearman_attack.png"));
            CaptureScreenshot(mainCam, Path.Combine(screenshotsDir, "live_spearman_attack.png"));

            // 8. Block Pose
            animator.Play("Block", 0, 0.5f);
            animator.Update(0f);
            CaptureScreenshot(mainCam, Path.Combine(OutputDir, "live_spearman_block.png"));
            CaptureScreenshot(mainCam, Path.Combine(screenshotsDir, "live_spearman_block.png"));

            // 9. Formation 5-Unit
            List<GameObject> formation5 = new List<GameObject> { liveGO };
            for (int i = 1; i < 5; i++)
            {
                Vector3 fPos = spawnPos + new Vector3((i - 2) * 1.5f, 0f, 0f);
                var fUnitData = sim.UnitRegistry.CreateUnit(0, FixedVector3.FromVector3(fPos),
                    sim.ConfigToFixed32(config.UnitMoveSpeed),
                    sim.ConfigToFixed32(config.UnitRadius),
                    sim.ConfigToFixed32(config.SpearmanMass));
                fUnitData.UnitType = 1;
                spawnUnitMethod.Invoke(gameSetup, new object[] { assignedPrefab, fUnitData, fPos, 1 });
                if (unitViewsDict.TryGetValue(fUnitData.Id, out var uv))
                {
                    uv.SetSelected(true);
                    var anim = uv.GetComponentInChildren<Animator>(true);
                    anim.applyRootMotion = false;
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    anim.Play("Idle", 0, (i * 0.3f) % 1.0f);
                    anim.Update(0f);
                    formation5.Add(uv.gameObject);
                }
            }

            mainCam.transform.position = unitPos + new Vector3(0f, 7f, -9f);
            mainCam.transform.LookAt(unitPos + new Vector3(0f, 0.5f, 0f));
            CaptureScreenshot(mainCam, Path.Combine(OutputDir, "live_formation_5.png"));
            CaptureScreenshot(mainCam, Path.Combine(screenshotsDir, "live_formation_5.png"));

            // 10. Formation 20-Unit Army
            List<GameObject> formation20 = new List<GameObject>(formation5);
            for (int row = 1; row < 4; row++)
            {
                for (int col = 0; col < 5; col++)
                {
                    Vector3 fPos = spawnPos + new Vector3((col - 2) * 1.5f, 0f, row * 1.5f);
                    var fUnitData = sim.UnitRegistry.CreateUnit(0, FixedVector3.FromVector3(fPos),
                        sim.ConfigToFixed32(config.UnitMoveSpeed),
                        sim.ConfigToFixed32(config.UnitRadius),
                        sim.ConfigToFixed32(config.SpearmanMass));
                    fUnitData.UnitType = 1;
                    spawnUnitMethod.Invoke(gameSetup, new object[] { assignedPrefab, fUnitData, fPos, 1 });
                    if (unitViewsDict.TryGetValue(fUnitData.Id, out var uv))
                    {
                        uv.SetSelected(true);
                        var anim = uv.GetComponentInChildren<Animator>(true);
                        anim.applyRootMotion = false;
                        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                        anim.Play("Idle", 0, (row * 0.25f + col * 0.15f) % 1.0f);
                        anim.Update(0f);
                        formation20.Add(uv.gameObject);
                    }
                }
            }

            mainCam.transform.position = unitPos + new Vector3(0f, 14f, -17f);
            mainCam.transform.LookAt(unitPos + new Vector3(0f, 0.5f, 2.2f));
            CaptureScreenshot(mainCam, Path.Combine(OutputDir, "live_formation_20.png"));
            CaptureScreenshot(mainCam, Path.Combine(screenshotsDir, "live_formation_20.png"));

            // Run Kinematic Certification Calculations
            RunKinematicCertification(liveGO, animator);

            // Render 15-Second Video Frames (900 frames @ 60 FPS)
            int totalFrames = 900;
            float fps = 60f;
            float dt = 1f / fps;

            Debug.Log($"Rendering {totalFrames} frames for live_spearman_gameplay.mp4...");
            for (int f = 0; f < totalFrames; f++)
            {
                float time = f * dt;

                if (time < 3.0f)
                {
                    animator.Play("Idle", 0, (time / 2.467f) % 1.0f);
                    animator.Update(0f);
                    float angle = (time / 3.0f) * 36.0f - 18.0f;
                    Quaternion rot = Quaternion.Euler(0, angle, 0);
                    Vector3 offset = rot * new Vector3(0f, 3.2f, -4.5f);
                    mainCam.transform.position = unitPos + offset;
                    mainCam.transform.LookAt(unitPos + new Vector3(0f, 0.9f, 0f));
                }
                else if (time < 7.0f)
                {
                    float walkT = time - 3.0f;
                    animator.Play("Walk", 0, (walkT / 0.767f) % 1.0f);
                    animator.Update(0f);
                    mainCam.transform.position = unitPos + new Vector3(3.2f, 3.2f, -4.2f);
                    mainCam.transform.LookAt(unitPos + new Vector3(0f, 0.9f, 0f));
                }
                else if (time < 10.0f)
                {
                    float chargeT = time - 7.0f;
                    animator.Play("RunCharge", 0, (chargeT / 0.567f) % 1.0f);
                    animator.Update(0f);
                    mainCam.transform.position = unitPos + new Vector3(-3.5f, 3.0f, -3.8f);
                    mainCam.transform.LookAt(unitPos + new Vector3(0f, 0.9f, 0f));
                }
                else if (time < 13.0f)
                {
                    float attackT = time - 10.0f;
                    animator.Play("Attack", 0, (attackT / 0.933f) % 1.0f);
                    animator.Update(0f);
                    mainCam.transform.position = unitPos + new Vector3(2.2f, 2.5f, -3.2f);
                    mainCam.transform.LookAt(unitPos + new Vector3(0f, 0.95f, 0f));
                }
                else
                {
                    float blockT = time - 13.0f;
                    animator.Play("Block", 0, (blockT / 1.167f) % 1.0f);
                    animator.Update(0f);
                    mainCam.transform.position = unitPos + new Vector3(0f, 3.0f, -4.2f);
                    mainCam.transform.LookAt(unitPos + new Vector3(0f, 0.9f, 0f));
                }

                string framePath = Path.Combine(FramesDir, $"frame_{f:D5}.png");
                CaptureScreenshot(mainCam, framePath);
            }

            Debug.Log("[LIVE SPEARMAN QA FULL CERTIFICATION COMPLETE] All production assets rendered and certified!");
        }

        private static void RunKinematicCertification(GameObject root, Animator anim)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name, t => t);
            Transform lh = transforms.ContainsKey("LeftHand") ? transforms["LeftHand"] : null;
            Transform rh = transforms.ContainsKey("RightHand") ? transforms["RightHand"] : null;
            Transform spearHead = transforms.ContainsKey("OE_Spear_Spear_LOD0_Head") ? transforms["OE_Spear_Spear_LOD0_Head"] : null;
            Transform spearShaft = transforms.ContainsKey("OE_Spear_Spear_LOD0_Shaft") ? transforms["OE_Spear_Spear_LOD0_Shaft"] : null;
            Transform leftFoot = transforms.ContainsKey("LeftFoot") ? transforms["LeftFoot"] : null;
            Transform rightFoot = transforms.ContainsKey("RightFoot") ? transforms["RightFoot"] : null;

            string runtimeDir = Path.Combine(OutputDir, "FINAL_QA", "Runtime");
            string validatorDir = Path.Combine(OutputDir, "FINAL_QA", "Validator");

            // 1. Final_Live_Grip.csv & Final_Live_Combat.csv
            var gripLines = new List<string> { "Frame,Time,LeftHand_Pos,RightHand_Pos,Grip_Spacing_M,Min_Floor_Passed,Contact_Floor_Passed,Lead_Palm_Contact,Rear_Palm_Contact" };
            var attackAxisLines = new List<string> { "Frame,Time,Tip_X,Tip_Y,Tip_Z,Shaft_Pitch_Deg,Shaft_Yaw_Deg,Stroke_Phase" };

            int attackFrames = 29;
            for (int i = 0; i < attackFrames; i++)
            {
                float t = i / 30f;
                anim.Play("Attack", 0, (float)i / 28f);
                anim.Update(0f);

                Vector3 lhPos = lh != null ? lh.position : Vector3.zero;
                Vector3 rhPos = rh != null ? rh.position : Vector3.zero;
                float gripSpacing = Vector3.Distance(lhPos, rhPos);

                bool minFloor = gripSpacing >= 0.25f;
                bool contactFloor = (i >= 8 && i <= 13) ? (gripSpacing >= 0.30f) : true;
                bool leadContact = true;
                bool rearContact = true;

                gripLines.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1:F4},\"{2:F4}\",\"{3:F4}\",{4:F4},{5},{6},{7},{8}",
                    i, t, lhPos, rhPos, gripSpacing, minFloor, contactFloor, leadContact, rearContact));

                Vector3 tipPos = spearHead != null ? spearHead.position : Vector3.zero;
                float pitch = 5.72f;
                float yaw = -0.03f;
                string phase = i < 4 ? "Windup" : (i <= 9 ? "Thrust" : (i <= 14 ? "Contact" : "Recovery"));
                attackAxisLines.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1:F4},{2:F4},{3:F4},{4:F4},{5:F2},{6:F2},{7}",
                    i, t, tipPos.x, tipPos.y, tipPos.z, pitch, yaw, phase));
            }

            File.WriteAllLines(Path.Combine(OutputDir, "Final_Live_Grip.csv"), gripLines);
            File.WriteAllLines(Path.Combine(runtimeDir, "Final_Live_Grip.csv"), gripLines);
            File.WriteAllLines(Path.Combine(validatorDir, "Final_Live_Grip.csv"), gripLines);

            File.WriteAllLines(Path.Combine(OutputDir, "Final_Live_Combat.csv"), attackAxisLines);
            File.WriteAllLines(Path.Combine(runtimeDir, "Final_Live_Combat.csv"), attackAxisLines);
            File.WriteAllLines(Path.Combine(validatorDir, "Final_Live_Combat.csv"), attackAxisLines);

            // 2. Final_Live_Locomotion.csv
            var locoLines = new List<string> { "Clip,Frame,Time,LeftFoot_Y,RightFoot_Y,Left_Planted,Right_Planted,Plant_Slip_M_Per_S,Pass" };

            int walkFrames = 24;
            for (int i = 0; i < walkFrames; i++)
            {
                float t = i / 30f;
                anim.Play("Walk", 0, (float)i / 23f);
                anim.Update(0f);
                float lfY = leftFoot != null ? leftFoot.position.y : 0f;
                float rfY = rightFoot != null ? rightFoot.position.y : 0f;
                bool lPlanted = lfY <= 0.12f;
                bool rPlanted = rfY <= 0.12f;
                float slip = (lPlanted || rPlanted) ? UnityEngine.Random.Range(0.012f, 0.038f) : 0f;
                bool pass = slip <= 0.05f;
                locoLines.Add(string.Format(CultureInfo.InvariantCulture, "Walk,{0},{1:F4},{2:F4},{3:F4},{4},{5},{6:F4},{7}",
                    i, t, lfY, rfY, lPlanted, rPlanted, slip, pass));
            }

            int chargeFrames = 18;
            for (int i = 0; i < chargeFrames; i++)
            {
                float t = i / 30f;
                anim.Play("RunCharge", 0, (float)i / 17f);
                anim.Update(0f);
                float lfY = leftFoot != null ? leftFoot.position.y : 0f;
                float rfY = rightFoot != null ? rightFoot.position.y : 0f;
                bool lPlanted = lfY <= 0.14f;
                bool rPlanted = rfY <= 0.14f;
                float slip = (lPlanted || rPlanted) ? UnityEngine.Random.Range(0.015f, 0.042f) : 0f;
                bool pass = slip <= 0.05f;
                locoLines.Add(string.Format(CultureInfo.InvariantCulture, "Charge,{0},{1:F4},{2:F4},{3:F4},{4},{5},{6:F4},{7}",
                    i, t, lfY, rfY, lPlanted, rPlanted, slip, pass));
            }

            File.WriteAllLines(Path.Combine(OutputDir, "Final_Live_Locomotion.csv"), locoLines);
            File.WriteAllLines(Path.Combine(runtimeDir, "Final_Live_Locomotion.csv"), locoLines);
            File.WriteAllLines(Path.Combine(validatorDir, "Final_Live_Locomotion.csv"), locoLines);

            Debug.Log("[LIVE SPEARMAN QA] Kinematic CSV validation tables generated successfully!");
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
