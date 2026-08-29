using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OpenEmpires.EditorTests
{
    public static class TestClipBindings
    {
        public static void Run()
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath("Assets/Models/Units/Spearman/SM_Spearman.fbx").OfType<AnimationClip>().ToArray();
            var sb = new StringBuilder();
            sb.AppendLine("=== CLIP BINDINGS ===");

            foreach (var c in clips)
            {
                var bindings = AnimationUtility.GetCurveBindings(c);
                sb.AppendLine($"\nClip: '{c.name}', length={c.length:F3}s, isHumanMotion={c.isHumanMotion}, bindingsCount={bindings.Length}");
                for (int i = 0; i < Mathf.Min(10, bindings.Length); i++)
                {
                    sb.AppendLine($"  Binding[{i}]: path='{bindings[i].path}', type={bindings[i].type.Name}, prop='{bindings[i].propertyName}'");
                }
            }

            File.WriteAllText(@"D:\unity_projects\OpenEmpiresTemp\Spearman_Production_Rebuild_V3_2\Clip_Bindings.txt", sb.ToString());
            Debug.Log(sb.ToString());
        }
    }
}
