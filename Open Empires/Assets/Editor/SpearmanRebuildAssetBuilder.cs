using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenEmpires;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SpearmanRebuildAssetBuilder
{
    private const string Root = "Assets/Models/Units/Spearman";
    private const string ModelPath = Root + "/SM_Spearman.fbx";
    private const string AtlasPath = Root + "/Textures/T_Spearman_Atlas.png";
    private const string ControllerPath = Root + "/Animations/AC_Spearman.controller";
    private const string PrefabPath = Root + "/Spearman_Animated.prefab";
    private const string TestScenePath = "Assets/Scenes/Tests/SpearmanAnimationTest.unity";
    private const string LegacySpearmanPath = "Assets/Prefabs/Units/Spearman.prefab";

    [MenuItem("OpenEmpires/Spearman Rebuild/Build Candidate Assets")]
    public static void Build()
    {
        Directory.CreateDirectory(Root + "/Materials");
        Directory.CreateDirectory(Root + "/Animations");
        Directory.CreateDirectory("Assets/Scenes/Tests");
        AssetDatabase.Refresh();

        ConfigureModelImporter();
        var clips = LoadClips();
        var controller = BuildController(clips);
        var materials = BuildMaterials();
        var prefab = BuildPrefab(controller, materials);
        BuildTestScene(prefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[SpearmanRebuild] Candidate built without promoting SampleScene: {PrefabPath}");
    }

    private static void ConfigureModelImporter()
    {
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Spearman FBX importer was not found.");

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.preserveHierarchy = true;
        importer.optimizeGameObjects = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importVisibility = false;
        importer.bakeAxisConversion = true;
        importer.useFileScale = true;
        importer.globalScale = 1f;

        var clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
            throw new InvalidOperationException("FBX contains no animation takes.");

        for (int i = 0; i < clips.Length; i++)
        {
            string cleanName = CleanClipName(clips[i].name);
            clips[i].name = cleanName;
            clips[i].loopTime = cleanName == "Idle" || cleanName == "Walk" || cleanName == "RunCharge" || cleanName == "Block";
            clips[i].lockRootHeightY = true;
            clips[i].lockRootPositionXZ = true;
            clips[i].lockRootRotation = true;
            clips[i].keepOriginalOrientation = true;
            clips[i].keepOriginalPositionXZ = true;
            clips[i].keepOriginalPositionY = true;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();

        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
            throw new InvalidOperationException("Spearman Humanoid Avatar is missing, invalid, or non-Humanoid.");
    }

    private static string CleanClipName(string name)
    {
        int pipe = name.LastIndexOf('|');
        if (pipe >= 0) name = name.Substring(pipe + 1);
        int take = name.LastIndexOf("Take 001", StringComparison.OrdinalIgnoreCase);
        return take >= 0 ? name.Substring(0, take).Trim(' ', '-') : name;
    }

    private static Dictionary<string, AnimationClip> LoadClips()
    {
        var result = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
        foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>())
        {
            if (clip.name.StartsWith("__preview__", StringComparison.Ordinal)) continue;
            result[CleanClipName(clip.name)] = clip;
        }

        string[] required = { "Idle", "Walk", "RunCharge", "Attack", "Block", "Hit", "Death" };
        foreach (string clip in required)
            if (!result.ContainsKey(clip))
                throw new InvalidOperationException($"Required clip '{clip}' was not imported. Imported: {string.Join(", ", result.Keys)}");
        return result;
    }

    private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("InCombat", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsCharging", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        var states = new Dictionary<string, AnimatorState>();
        string[] names = { "Idle", "Walk", "RunCharge", "Attack", "Block", "Hit", "Death" };
        for (int i = 0; i < names.Length; i++)
        {
            AnimatorState state = sm.AddState(names[i], new Vector3(260 + (i % 3) * 230, 80 + (i / 3) * 120));
            state.motion = clips[names[i]];
            state.writeDefaultValues = true;
            states[names[i]] = state;
        }
        sm.defaultState = states["Idle"];

        AddTransition(states["Idle"], states["Walk"], "Speed", AnimatorConditionMode.Greater, 0.1f);
        AddTransition(states["Walk"], states["Idle"], "Speed", AnimatorConditionMode.Less, 0.1f);
        AddBoolTransition(states["Idle"], states["Block"], "InCombat", true);
        AddBoolTransition(states["Walk"], states["Block"], "InCombat", true);
        AddBoolTransition(states["Block"], states["Idle"], "InCombat", false);

        var charge = sm.AddAnyStateTransition(states["RunCharge"]);
        ConfigureImmediate(charge); charge.canTransitionToSelf = false;
        charge.AddCondition(AnimatorConditionMode.If, 0f, "IsCharging");
        var chargeExit = states["RunCharge"].AddTransition(states["Walk"]);
        ConfigureImmediate(chargeExit); chargeExit.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCharging");

        AddTriggerTransition(sm, states["Death"], "Death", 0.03f, false);
        AddTriggerTransition(sm, states["Hit"], "Hit", 0.04f, true);
        AddTriggerTransition(sm, states["Attack"], "Attack", 0.04f, true);

        AddExit(states["Attack"], states["Block"], "InCombat", true, 0.88f);
        AddExit(states["Attack"], states["Idle"], "InCombat", false, 0.88f);
        AddExit(states["Hit"], states["Block"], "InCombat", true, 0.78f);
        AddExit(states["Hit"], states["Idle"], "InCombat", false, 0.78f);
        return controller;
    }

    private static void ConfigureImmediate(AnimatorStateTransition transition)
    {
        transition.hasExitTime = false;
        transition.duration = 0.08f;
        transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
    }

    private static void AddTransition(AnimatorState from, AnimatorState to, string parameter, AnimatorConditionMode mode, float threshold)
    {
        var transition = from.AddTransition(to);
        ConfigureImmediate(transition);
        transition.AddCondition(mode, threshold, parameter);
    }

    private static void AddBoolTransition(AnimatorState from, AnimatorState to, string parameter, bool value)
    {
        var transition = from.AddTransition(to);
        ConfigureImmediate(transition);
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
    }

    private static void AddTriggerTransition(AnimatorStateMachine sm, AnimatorState to, string trigger, float duration, bool interruptible)
    {
        var transition = sm.AddAnyStateTransition(to);
        transition.hasExitTime = false;
        transition.duration = duration;
        transition.canTransitionToSelf = false;
        transition.interruptionSource = interruptible ? TransitionInterruptionSource.SourceThenDestination : TransitionInterruptionSource.None;
        transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    private static void AddExit(AnimatorState from, AnimatorState to, string parameter, bool value, float exitTime)
    {
        var transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.duration = 0.08f;
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
    }

    private static Dictionary<string, Material> BuildMaterials()
    {
        Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        Texture2D normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/T_Spearman_Normal.png");
        Texture2D mraMap = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/T_Spearman_MRA.png");
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("No compatible lit shader was found.");

        return new Dictionary<string, Material>
        {
            ["Team"] = CreateMaterial("M_Spearman_Team", shader, atlas, normalMap, mraMap, new Color(0.16f, 0.38f, 0.82f), 0f, 0.28f),
            ["Body"] = CreateMaterial("M_Spearman_Body", shader, atlas, normalMap, mraMap, new Color(0.33f, 0.16f, 0.065f), 0f, 0.27f),
            ["Skin"] = CreateMaterial("M_Spearman_Skin", shader, atlas, normalMap, mraMap, new Color(0.66f, 0.39f, 0.23f), 0f, 0.34f),
            ["Metal"] = CreateMaterial("M_Spearman_Metal", shader, atlas, normalMap, mraMap, new Color(0.48f, 0.53f, 0.58f), 0.78f, 0.72f)
        };
    }

    private static Material CreateMaterial(string name, Shader shader, Texture2D atlas, Texture2D normalMap, Texture2D mraMap, Color color, float metallic, float smoothness)
    {
        string path = Root + "/Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        if (material.HasProperty("_BaseMap") && atlas != null) material.SetTexture("_BaseMap", atlas);
        if (material.HasProperty("_MainTex") && atlas != null) material.SetTexture("_MainTex", atlas);
        if (material.HasProperty("_BumpMap") && normalMap != null)
        {
            material.SetTexture("_BumpMap", normalMap);
            material.EnableKeyword("_NORMALMAP");
        }
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject BuildPrefab(AnimatorController controller, Dictionary<string, Material> materials)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) throw new InvalidOperationException("Spearman model asset is missing.");

        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("Spearman_Animated");
            SceneManager.MoveGameObjectToScene(root, preview);
            root.layer = 8;

            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.55f, 0f);
            collider.size = new Vector3(0.5f, 1.1f, 0.5f);
            var unitView = root.AddComponent<UnitView>();
            var driver = root.AddComponent<UnitAnimatorVisualDriver>();

            var visual = PrefabUtility.InstantiatePrefab(model, preview) as GameObject;
            PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            SetLayerRecursively(visual, 8);

            var animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.gameObject.SetActive(true);
                renderer.enabled = true;
                renderer.sharedMaterial = SelectMaterial(renderer.name, materials);
            }

            Transform spearSocket = FindTransform(visual.transform, "SpearSocket");
            if (spearSocket == null)
                throw new InvalidOperationException("Required SpearSocket was not imported.");

            var spearAttachment = new GameObject("SpearAttachment").transform;
            spearAttachment.SetParent(spearSocket, false);
            var spearParts = visual.GetComponentsInChildren<Transform>(true)
                .Where(t => t != spearAttachment && t.name.IndexOf("Spear_LOD", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();
            foreach (Transform part in spearParts) part.SetParent(spearAttachment, true);

            var driverSO = new SerializedObject(driver);
            driverSO.FindProperty("animator").objectReferenceValue = animator;
            driverSO.FindProperty("spearAttachment").objectReferenceValue = spearAttachment;
            driverSO.ApplyModifiedPropertiesWithoutUndo();

            GameObject legacy = AssetDatabase.LoadAssetAtPath<GameObject>(LegacySpearmanPath);
            Transform ringSource = legacy != null ? legacy.transform.Find("SelectionRing") : null;
            GameObject ring = ringSource != null ? UnityEngine.Object.Instantiate(ringSource.gameObject, root.transform) : CreateFallbackRing(root.transform);
            ring.name = "SelectionRing";
            ring.SetActive(false);
            SetLayerRecursively(ring, 8);
            var viewSO = new SerializedObject(unitView);
            var ringProperty = viewSO.FindProperty("selectionRing");
            if (ringProperty != null) ringProperty.objectReferenceValue = ring;
            viewSO.ApplyModifiedPropertiesWithoutUndo();

            ScaleVisualToRosterHeight(visual);
            BuildLodGroup(root, visual);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (saved == null) throw new InvalidOperationException("Failed to save Spearman animated prefab.");
            UnityEngine.Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    private static Material SelectMaterial(string rendererName, Dictionary<string, Material> materials)
    {
        if (rendererName.EndsWith("_Team", StringComparison.Ordinal) || rendererName.IndexOf("Team", StringComparison.OrdinalIgnoreCase) >= 0)
            return materials["Team"];
        if (rendererName.IndexOf("Metal", StringComparison.OrdinalIgnoreCase) >= 0 ||
            rendererName.IndexOf("Rim", StringComparison.OrdinalIgnoreCase) >= 0 ||
            rendererName.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0 ||
            rendererName.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0 ||
            rendererName.IndexOf("Socket", StringComparison.OrdinalIgnoreCase) >= 0)
            return materials["Metal"];
        if (rendererName.IndexOf("Skin", StringComparison.OrdinalIgnoreCase) >= 0)
            return materials["Skin"];
        return materials["Body"];
    }

    private static void BuildLodGroup(GameObject root, GameObject visual)
    {
        // Unity may synthesize an LODGroup on an imported FBX from its LOD naming.
        // The candidate prefab owns the authoritative group at its unit root, so
        // remove imported instance groups before registering the renderers again.
        foreach (LODGroup importedGroup in visual.GetComponentsInChildren<LODGroup>(true))
            UnityEngine.Object.DestroyImmediate(importedGroup);

        var all = visual.GetComponentsInChildren<Renderer>(true);
        var lod0 = all.Where(r => r.name.IndexOf("LOD0", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
        var lod1 = all.Where(r => r.name.IndexOf("LOD1", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
        if (lod0.Length == 0 || lod1.Length == 0)
            throw new InvalidOperationException($"LOD renderer sets are incomplete (LOD0={lod0.Length}, LOD1={lod1.Length}).");

        var group = root.AddComponent<LODGroup>();
        group.fadeMode = LODFadeMode.CrossFade;
        group.animateCrossFading = true;
        // With the real 1.1-unit roster scale and orthographic RTS camera, a 0.10
        // final threshold culls the unit at normal zoom. Keep LOD0 for the close
        // size-5 view, LOD1 for formation/strategic views, and cull only beyond
        // the approved size-40 camera gate.
        group.SetLODs(new[] { new LOD(0.08f, lod0), new LOD(0.005f, lod1) });
        group.localReferencePoint = new Vector3(0f, 0.55f, 0f);
        group.size = 1.1f;
        group.RecalculateBounds();
    }

    private static void ScaleVisualToRosterHeight(GameObject visual)
    {
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.name.IndexOf("LOD0", StringComparison.OrdinalIgnoreCase) >= 0 && r.name.IndexOf("Spear", StringComparison.OrdinalIgnoreCase) < 0)
            .ToArray();
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        if (bounds.size.y > 0.001f)
            visual.transform.localScale = Vector3.one * (1.1f / bounds.size.y);
    }

    private static Transform FindTransform(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    private static GameObject CreateFallbackRing(Transform parent)
    {
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.transform.SetParent(parent, false);
        ring.transform.localPosition = new Vector3(0f, 0.015f, 0f);
        ring.transform.localScale = new Vector3(0.45f, 0.005f, 0.45f);
        UnityEngine.Object.DestroyImmediate(ring.GetComponent<Collider>());
        return ring;
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
    }

    private static void BuildTestScene(GameObject prefab)
    {
        string previousScene = SceneManager.GetActiveScene().path;
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var light = new GameObject("Directional Light");
        var lightComponent = light.AddComponent<Light>();
        lightComponent.type = LightType.Directional;
        lightComponent.intensity = 1.25f;
        light.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

        var cameraObject = new GameObject("RTS Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 15f;
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<RTSCameraController>();

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "RTS Readability Ground";
        ground.transform.position = new Vector3(0f, -0.01f, 6f);
        ground.transform.localScale = new Vector3(3.5f, 1f, 3.5f);

        CreateFormation(prefab, "Formation_1", new Vector3(-7f, 0f, -3f), 1, 1);
        CreateFormation(prefab, "Formation_5", new Vector3(-2f, 0f, -2f), 5, 5);
        CreateFormation(prefab, "Formation_20", new Vector3(2f, 0f, 3f), 20, 5);
        new GameObject("Animation State Controls (1-7)").AddComponent<SpearmanAnimationTestController>();

        EditorSceneManager.SaveScene(scene, TestScenePath);
        if (!string.IsNullOrEmpty(previousScene))
            EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
    }

    private static void CreateFormation(GameObject prefab, string name, Vector3 origin, int count, int columns)
    {
        var parent = new GameObject(name).transform;
        for (int i = 0; i < count; i++)
        {
            var unit = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            unit.name = $"Spearman_{i + 1:00}";
            unit.transform.SetParent(parent, false);
            unit.transform.position = origin + new Vector3((i % columns) * 0.85f, 0f, (i / columns) * 0.85f);
            unit.transform.rotation = Quaternion.identity;
        }
    }
}
