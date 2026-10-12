using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace OpenEmpires
{
    public sealed partial class CommanderChatUI
    {
        private const int MaximumEvidenceScreenshotsPerSession = 3;
        private int evidenceScreenshotCount;
        private bool evidenceScreenshotInProgress;
        private string nextEvidenceKind = "manual";
        private readonly List<string> offlineEvidenceCapturePaths = new List<string>();

        // An explicit local UI action only. It does not invoke the provider, issue orders,
        // start audio, or serialize Commander context. Browser evidence uses the browser's
        // own screenshot route; WebGL's virtual persistent store is not a user download.
        public string LastEvidenceScreenshotPath { get; private set; }
        public string LastEvidenceScreenshotKind { get; private set; }
        public int LastEvidenceScreenshotWidth { get; private set; }
        public int LastEvidenceScreenshotHeight { get; private set; }
        public IReadOnlyList<string> OfflineEvidenceCapturePaths => offlineEvidenceCapturePaths.AsReadOnly();

        private sealed class OfflineEvidenceProvider : ICommanderAIProvider, ICommanderSemanticProvider
        {
            private const string SyntheticTwoHouses = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                + "{\"id\":\"site\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"MyTownCenter\",\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{},\"dependsOn\":[]},"
                + "{\"id\":\"houses\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:House\",\"count\":2},\"inputs\":{\"location\":\"site\"},\"dependsOn\":[\"site\"]}]}";
            internal int Calls { get; private set; }
            public Task<CommanderSemanticResult> TranslateSemanticAsync(CommanderSemanticProviderRequest request,
                CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                Calls++;
                return Task.FromResult(Calls == 1 && request?.PlayerMessage == "Build two Houses near my Town Center."
                    ? CommanderSemanticJson.ParseProviderResponse(SyntheticTwoHouses)
                    : CommanderSemanticResult.ProviderRejected("Offline fixture accepts exactly one declared request."));
            }
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request, CancellationToken token)
                => Task.FromResult(CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure,
                    "Offline fixture does not use the legacy provider route."));
        }

        private static bool ShouldStartCliOfflineFixture()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            foreach (string argument in Environment.GetCommandLineArgs())
                if (argument == "--commander-evidence-offline-ui-fixture") return true;
#endif
            return false;
        }

        private IEnumerator CaptureOfflineFixtureOnce()
        {
            offlineEvidenceCapturePaths.Clear();
            AppendLine("Commander", "OFFLINE FIXTURE UI EVIDENCE — synthetic response; not live Luna or English-accuracy proof.", false);
            OpenCommander();
            var request = SubmitMessageAsync("Build two Houses near my Town Center.");
            for (int frame = 0; frame < 120 && !request.IsCompleted; frame++) yield return null;
            if (!request.IsCompleted || request.IsFaulted || request.IsCanceled || pendingActionPlan == null)
                yield break;
            yield return CaptureOfflineFrame("offline-fixture-pending-approval");
            if (offlineEvidenceCapturePaths.Count != 1) yield break;

            // Distinct UI turn; only the explicit fixture flag authorizes this normal admission.
            var approval = SubmitMessageAsync("Approve plan");
            for (int frame = 0; frame < 120 && !approval.IsCompleted; frame++) yield return null;
            if (!approval.IsCompleted || approval.IsFaulted || approval.IsCanceled
                || semanticGoalManager == null || semanticGoalManager.Goals.Count == 0) yield break;
            tasksTabButton?.onClick.Invoke();
            yield return null;
            yield return CaptureOfflineFrame("offline-fixture-tasks");
            if (offlineEvidenceCapturePaths.Count != 2) yield break;

            chatTabButton?.onClick.Invoke();
            var status = SubmitMessageAsync("strategy status"); // local read-only, zero provider calls
            for (int frame = 0; frame < 120 && !status.IsCompleted; frame++) yield return null;
            if (!status.IsCompleted || status.IsFaulted || status.IsCanceled) yield break;
            yield return null;
            yield return CaptureOfflineFrame("offline-fixture-chat");
        }

        private IEnumerator CaptureOfflineFrame(string kind)
        {
            string previous = LastEvidenceScreenshotPath;
            nextEvidenceKind = kind;
            if (!RequestEvidenceScreenshot()) yield break;
            for (int frame = 0; frame < 120 && evidenceScreenshotInProgress; frame++) yield return null;
            if (!evidenceScreenshotInProgress && LastEvidenceScreenshotPath != previous
                && LastEvidenceScreenshotKind == kind)
                offlineEvidenceCapturePaths.Add(LastEvidenceScreenshotPath);
        }

        private static bool ShouldStartCliEvidenceCapture()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            foreach (string argument in Environment.GetCommandLineArgs())
                if (argument == "--commander-evidence-status-capture") return true;
#endif
            return false;
        }

        private IEnumerator CaptureStatusEvidenceOnce()
        {
            // Exact local information route in SubmitMessageAsync: no semantic provider,
            // no gameplay order, no microphone. This runs only with an explicit CLI flag.
            var status = SubmitMessageAsync("strategy status");
            while (!status.IsCompleted) yield return null;
            if (status.IsFaulted || status.IsCanceled) yield break;
            yield return null;
            nextEvidenceKind = "status-only-local";
            RequestEvidenceScreenshot();
        }

        public bool RequestEvidenceScreenshot()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            if (!Application.isPlaying || evidenceScreenshotInProgress
                || evidenceScreenshotCount >= MaximumEvidenceScreenshotsPerSession
                || canvasRoot == null || !canvasRoot.activeInHierarchy)
                return false;
            CloseSettingsMenu();
            OpenCommander();
            evidenceScreenshotInProgress = true;
            StartCoroutine(CaptureEvidenceScreenshotAtEndOfFrame());
            return true;
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private IEnumerator CaptureEvidenceScreenshotAtEndOfFrame()
        {
            yield return new WaitForEndOfFrame();
            Texture2D texture = null;
            string temporary = null;
            try
            {
                Canvas.ForceUpdateCanvases();
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (texture == null) throw new InvalidOperationException("Screenshot unavailable.");
                byte[] png = texture.EncodeToPNG();
                if (png == null || png.Length < 8 || png.Length > 16 * 1024 * 1024)
                    throw new InvalidOperationException("Screenshot encoding unavailable or out of bounds.");
                string directory = Path.Combine(Application.persistentDataPath, "CommanderEvidence");
                Directory.CreateDirectory(directory);
                string kind = nextEvidenceKind;
                string destination = Path.Combine(directory, "commander-ui-" + kind + "-"
                    + texture.width + "x" + texture.height + "-"
                    + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".png");
                temporary = destination + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(png, 0, png.Length); stream.Flush(true); }
                File.Move(temporary, destination); // unique destination, no overwrite
                temporary = null;
                LastEvidenceScreenshotPath = destination;
                LastEvidenceScreenshotKind = kind;
                LastEvidenceScreenshotWidth = texture.width;
                LastEvidenceScreenshotHeight = texture.height;
                evidenceScreenshotCount++;
                Debug.Log("[Commander] Explicit local UI evidence screenshot saved.");
            }
            catch (Exception)
            {
                Debug.LogWarning("[Commander] Local UI evidence screenshot could not be saved.");
            }
            finally
            {
                if (temporary != null && File.Exists(temporary)) File.Delete(temporary);
                if (texture != null) Destroy(texture);
                nextEvidenceKind = "manual";
                evidenceScreenshotInProgress = false;
            }
        }
#endif
    }
}
