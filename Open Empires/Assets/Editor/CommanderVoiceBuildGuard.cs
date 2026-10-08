using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenEmpires.Editor
{
    public sealed class CommanderVoiceBuildGuard:IPreprocessBuildWithReport
    {
        public int callbackOrder=>0;
        public void OnPreprocessBuild(BuildReport report)
        {
            if(report.summary.platform!=BuildTarget.WebGL)return;
            try{WhisperDistributionPolicy.ValidateWebStreamingAssets(Application.streamingAssetsPath);}
            catch(Exception){throw new BuildFailedException("Web voice packaging is blocked: remove native Whisper models/binaries from shared StreamingAssets and provision them in the Windows model cache. See GrandFix model provisioning instructions.");}
        }
    }
}
