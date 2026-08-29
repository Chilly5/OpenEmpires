using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OpenEmpires.EditorTests
{
    public static class InspectPrefabStatus
    {
        public static void Run()
        {
            var prefabPath = "Assets/Models/Units/Spearman/Spearman_Animated.prefab";
            var go = PrefabUtility.LoadPrefabContents(prefabPath);
            var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var mrs = go.GetComponentsInChildren<MeshRenderer>(true);

            Debug.Log($"[PREFAB STATUS] SMR Count = {smrs.Length}, MR Count = {mrs.Length}");
            foreach (var smr in smrs)
            {
                Debug.Log($"  SMR: {smr.name} | Mesh: {(smr.sharedMesh != null ? smr.sharedMesh.name : "NULL")} | Bounds: {smr.bounds} | Enabled: {smr.enabled} | UpdateOffscreen: {smr.updateWhenOffscreen} | Mats: {smr.sharedMaterials.Length}");
            }

            foreach (var mr in mrs)
            {
                Debug.Log($"  MR: {mr.name} | Enabled: {mr.enabled} | Mats: {mr.sharedMaterials.Length}");
            }

            var lod = go.GetComponent<LODGroup>();
            if (lod != null)
            {
                var lods = lod.GetLODs();
                Debug.Log($"  LODGroup: Size={lod.size}, LODs={lods.Length}");
                for (int i = 0; i < lods.Length; i++)
                {
                    Debug.Log($"    LOD {i}: ScreenHeight={lods[i].screenRelativeTransitionHeight}, Renderers={lods[i].renderers.Length}");
                }
            }

            PrefabUtility.UnloadPrefabContents(go);
        }
    }
}
