#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace OpenEmpires.EditorTools
{
    public static class CommanderDebugMenu
    {
        [MenuItem("Open Empires/Commander/Start Local QA Match")]
        public static void StartLocalQaMatch()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Commander] Enter Play Mode before starting the local QA match.");
                return;
            }

            GameBootstrapper bootstrapper = GameBootstrapper.Instance;
            NetworkManager network = bootstrapper != null ? bootstrapper.Network : null;
            if (bootstrapper == null || network == null)
            {
                Debug.LogError("[Commander] GameBootstrapper or NetworkManager is unavailable.");
                return;
            }

            bootstrapper.SetPlayerCount(2);
            bootstrapper.SetAIPlayerIds(new[] { 1 });
            bootstrapper.SetTeamAssignments(new[] { 0, 1 });
            typeof(NetworkManager).GetProperty(nameof(NetworkManager.GameStarted),
                BindingFlags.Instance | BindingFlags.Public)?.SetValue(network, true);
            Time.timeScale = 10f;
            Debug.Log("[Commander] Local 1v1 QA match enabled at 10x time scale.");
        }

        [MenuItem("Open Empires/Commander/Reset Time Scale")]
        public static void ResetTimeScale()
        {
            Time.timeScale = 1f;
            Debug.Log("[Commander] Time scale reset to 1x.");
        }

        [MenuItem("Open Empires/Commander/Ensure 10 Spearmen")]
        public static void EnsureTenSpearmen()
        {
            GameBootstrapper bootstrapper = GameBootstrapper.Instance;
            if (bootstrapper == null || bootstrapper.Commander == null)
            {
                Debug.LogWarning("[Commander] Enter Play Mode and wait for GameBootstrapper initialization first.");
                return;
            }
            bootstrapper.DebugEnsureTenSpearmen();
        }

        [MenuItem("Open Empires/Commander/Cancel Debug Goal")]
        public static void CancelDebugGoal()
        {
            GameBootstrapper bootstrapper = GameBootstrapper.Instance;
            if (bootstrapper == null)
            {
                Debug.LogWarning("[Commander] GameBootstrapper is not active.");
                return;
            }
            bootstrapper.DebugCancelCommanderGoal();
        }

        [MenuItem("Open Empires/Commander/Log Status")]
        public static void LogStatus()
        {
            CommanderGoal goal = GameBootstrapper.Instance?.Commander?.ActiveGoal;
            if (goal == null)
            {
                Debug.Log("[Commander] No active goal.");
                return;
            }
            Debug.Log($"[Commander] Goal #{goal.GoalId}: status={goal.Status}, "
                + $"owned={goal.LastObservedOwnedCount}, queued={goal.LastObservedQueuedCount}, "
                + $"reason={goal.StatusReason}");
        }
    }
}
#endif
