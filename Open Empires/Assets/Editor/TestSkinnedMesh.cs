using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenEmpires.EditorTests
{
    public static class TestSkinnedMesh
    {
        public static void Run()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Units/Spearman/Spearman_Animated.prefab");
            var go = UnityEngine.Object.Instantiate(prefab);
            var sb = new StringBuilder();
            sb.AppendLine("=== SKINNED MESH DIAGNOSTIC ===");

            var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                sb.AppendLine($"\nSMR: '{smr.name}' on '{smr.gameObject.name}'");
                sb.AppendLine($"  RootBone: '{smr.rootBone?.name}' (Pos={smr.rootBone?.position:F3}, Rot={smr.rootBone?.rotation.eulerAngles:F1}, Scale={smr.rootBone?.lossyScale:F3})");
                sb.AppendLine($"  SharedMesh: '{smr.sharedMesh?.name}', Verts={smr.sharedMesh?.vertexCount}, Submeshes={smr.sharedMesh?.subMeshCount}, BonesCount={smr.bones?.Length}");
                sb.AppendLine($"  Mesh Bounds: Center={smr.sharedMesh?.bounds.center:F3}, Extent={smr.sharedMesh?.bounds.extents:F3}");
                sb.AppendLine($"  SMR LocalBounds: Center={smr.localBounds.center:F3}, Extent={smr.localBounds.extents:F3}");
                sb.AppendLine($"  SMR WorldBounds: Center={smr.bounds.center:F3}, Extent={smr.bounds.extents:F3}");
                sb.AppendLine($"  UpdateWhenOffscreen: {smr.updateWhenOffscreen}");
                sb.AppendLine($"  Materials Count: {smr.sharedMaterials?.Length}");
                for (int m = 0; m < smr.sharedMaterials.Length; m++)
                {
                    var mat = smr.sharedMaterials[m];
                    sb.AppendLine($"    Mat[{m}]: '{mat?.name}' | Shader: '{mat?.shader?.name}'");
                    if (mat != null)
                    {
                        if (mat.HasProperty("_BaseMap"))
                            sb.AppendLine($"      _BaseMap: '{mat.GetTexture("_BaseMap")?.name}'");
                        if (mat.HasProperty("_BaseColor"))
                            sb.AppendLine($"      _BaseColor: {mat.GetColor("_BaseColor")}");
                        if (mat.HasProperty("_Color"))
                            sb.AppendLine($"      _Color: {mat.GetColor("_Color")}");
                    }
                }

                var mesh = smr.sharedMesh;
                if (mesh != null)
                {
                    var bindposes = mesh.bindposes;
                    sb.AppendLine($"  Bindposes count: {bindposes.Length}");
                    for (int b = 0; b < Mathf.Min(5, smr.bones.Length); b++)
                    {
                        var bone = smr.bones[b];
                        sb.AppendLine($"    Bone[{b}]: '{bone?.name}', LocalPos={bone?.localPosition:F3}, LocalScale={bone?.localScale:F3}, LossyScale={bone?.lossyScale:F3}");
                        if (b < bindposes.Length)
                        {
                            var bp = bindposes[b];
                            sb.AppendLine($"      Bindpose[{b}] translation: {bp.GetColumn(3):F3}, scale: ({bp.GetColumn(0).magnitude:F3}, {bp.GetColumn(1).magnitude:F3}, {bp.GetColumn(2).magnitude:F3})");
                        }
                    }

                    var verts = mesh.vertices;
                    sb.AppendLine($"  First 3 vertices in mesh:");
                    for (int v = 0; v < Mathf.Min(3, verts.Length); v++)
                    {
                        sb.AppendLine($"    v[{v}] = {verts[v]:F4}");
                    }
                }
            }

            string outPath = @"D:\unity_projects\OpenEmpiresTemp\Spearman_Production_Rebuild_V3_2\SkinnedMesh_Diag.txt";
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log(sb.ToString());
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
