using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OpenEmpires.EditorTests
{
    public static class AlignSpearSocket
    {
        public static void Run()
        {
            var prefabPath = "Assets/Models/Units/Spearman/Spearman_Animated.prefab";
            var go = PrefabUtility.LoadPrefabContents(prefabPath);

            var rh = go.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "RightHand");
            var spearSocket = go.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "SpearSocket");
            var spearAttachment = go.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "SpearAttachment");
            var spearShaft = go.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "OE_Spear_Spear_LOD0_Shaft");

            Debug.Log($"Before: rh={rh?.position}, spearSocket={spearSocket?.position}, spearShaft={spearShaft?.position}");

            // The spear shaft mesh bounds center in local space:
            var mf = spearShaft.GetComponent<MeshFilter>();
            var boundsCenter = mf.sharedMesh.bounds.center;
            Debug.Log($"Shaft mesh bounds center in mesh local space: {boundsCenter:F4}");

            // To place the grip point of the spear directly at RightHand:
            // The rear grip point on the shaft is approximately at boundsCenter - new Vector3(0f, 0f, 0.35f);
            // In SpearAttachment local space:
            spearAttachment.localPosition = -boundsCenter + new Vector3(0f, 0f, 0.25f);
            spearAttachment.localRotation = Quaternion.identity;

            Debug.Log($"Set SpearAttachment localPosition to: {spearAttachment.localPosition:F4}");

            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            PrefabUtility.UnloadPrefabContents(go);

            Debug.Log("[ALIGNED SPEAR SOCKET] Prefab updated successfully!");
        }
    }
}
