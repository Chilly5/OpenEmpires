using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenEmpires
{
    public sealed class NetworkCompatibilityBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1100;
        public void OnPreprocessBuild(BuildReport report) => Generate();
        [MenuItem("Open Empires/Network/Generate source-matched compatibility identity")]
        public static void Generate()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Network")) AssetDatabase.CreateFolder("Assets/Resources", "Network");
            var profile = new NetworkCompatibilityProfile
            {
                protocol_revision = NetworkCompatibility.ProtocolRevision,
                source_sha256 = NetworkCompatibility.ComputeSourceIdentity(Path.GetDirectoryName(Application.dataPath)),
                command_encoding = NetworkCompatibility.CommandEncoding,
                recovery_policy = NetworkCompatibility.RecoveryPolicy
            };
            string path = Path.GetFullPath(NetworkCompatibility.AssetPath);
            string json = JsonUtility.ToJson(profile, true);
            if (!File.Exists(path) || File.ReadAllText(path) != json) File.WriteAllText(path, json, new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(NetworkCompatibility.AssetPath, ImportAssetOptions.ForceSynchronousImport);
            if (NetworkCompatibility.Current == null) throw new BuildFailedException("Compatibility identity did not match current source/data.");
            Debug.Log("[Network] Source-matched compatibility identity generated; no credentials inspected.");
        }
    }
}
