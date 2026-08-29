using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OpenEmpires.EditorTests
{
    public static class SnapSpearToHand
    {
        public static void Run()
        {
            var prefabPath = "Assets/Models/Units/Spearman/Spearman_Animated.prefab";
            var go = PrefabUtility.LoadPrefabContents(prefabPath);

            var rh = go.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "RightHand");
            var spearSocket = go.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "SpearSocket");
            var spearAttachment = go.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "SpearAttachment");

            spearSocket.localPosition = Vector3.zero;
            spearSocket.localRotation = Quaternion.identity;
            spearSocket.localScale = Vector3.one;

            // Rotate 90 deg so the spear points forward horizontally in front of the character
            spearAttachment.localPosition = Vector3.zero;
            spearAttachment.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            spearAttachment.localScale = Vector3.one;

            var spearParts = spearAttachment.GetComponentsInChildren<Transform>().Where(t => t != spearAttachment).ToArray();
            Vector3 offset = new Vector3(0.3715f, 0.0399f, 0.6839f);
            foreach (var part in spearParts)
            {
                part.localPosition = offset;
                part.localRotation = Quaternion.identity;
                part.localScale = Vector3.one;
            }

            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            PrefabUtility.UnloadPrefabContents(go);

            Debug.Log("[SNAP SPEAR TO HAND] Spear rotated 90 deg forward and saved!");
        }
    }
}
