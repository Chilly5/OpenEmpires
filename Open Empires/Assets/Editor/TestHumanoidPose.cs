using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OpenEmpires.EditorTests
{
    public static class TestHumanoidPose
    {
        public static void Run()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Units/Spearman/Spearman_Animated.prefab");
            var go = UnityEngine.Object.Instantiate(prefab);
            var anim = go.GetComponentInChildren<Animator>();

            Debug.Log($"Animator: isHuman={anim.isHuman}, avatar={anim.avatar?.name}, hasRootMotion={anim.hasRootMotion}");

            var clips = AssetDatabase.LoadAllAssetsAtPath("Assets/Models/Units/Spearman/SM_Spearman.fbx").OfType<AnimationClip>().ToArray();
            foreach (var c in clips)
            {
                Debug.Log($"Clip: '{c.name}', isHumanMotion={c.isHumanMotion}, length={c.length}");
            }

            var rh = go.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "RightHand");
            var spearSocket = go.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "SpearSocket");
            var spearShaft = go.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "OE_Spear_Spear_LOD0_Shaft");

            Debug.Log($"RightHand: pos={rh?.position:F3}, parent={rh?.parent?.name}");
            Debug.Log($"SpearSocket: pos={spearSocket?.position:F3}, localPos={spearSocket?.localPosition:F3}, parent={spearSocket?.parent?.name}");
            Debug.Log($"SpearShaft: pos={spearShaft?.position:F3}, localPos={spearShaft?.localPosition:F3}, parent={spearShaft?.parent?.name}");

            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
