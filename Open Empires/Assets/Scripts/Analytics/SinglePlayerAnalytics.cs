using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace OpenEmpires
{
    public class SinglePlayerAnalytics : MonoBehaviour
    {
        private const string ProductionServerUrl = "https://openempires.onrender.com";
        private const string LocalServerUrl = "http://localhost:8081";
        private const int MinSessionDurationMs = 10000; // 10 seconds minimum to report (matches backend validation)

        public static SinglePlayerAnalytics Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private bool enableAnalytics = true;

        private string ServerUrl
        {
            get
            {
#if UNITY_EDITOR
                // In editor, use local server for testing
                return LocalServerUrl;
#else
                return ProductionServerUrl;
#endif
            }
        }

        private string sessionId;
        private long sessionStartTicks;
        private bool sessionActive;
        private string clientId;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Get or create persistent client ID
            clientId = PlayerPrefs.GetString("AnalyticsClientId", "");
            if (string.IsNullOrEmpty(clientId))
            {
                clientId = Guid.NewGuid().ToString();
                PlayerPrefs.SetString("AnalyticsClientId", clientId);
                PlayerPrefs.Save();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            // Report abandoned session on quit
            if (sessionActive)
            {
                EndSession("abandoned");
            }
        }

        public void StartSession()
        {
            if (!enableAnalytics) return;
            if (sessionActive)
            {
                Debug.LogWarning("[SinglePlayerAnalytics] Session already active");
                return;
            }

            sessionId = Guid.NewGuid().ToString();
            sessionStartTicks = DateTime.UtcNow.Ticks;
            sessionActive = true;

            Debug.Log($"[SinglePlayerAnalytics] Session started: {sessionId}");
        }

        public void EndSession(string result)
        {
            if (!enableAnalytics) return;
            if (!sessionActive)
            {
                Debug.LogWarning("[SinglePlayerAnalytics] No active session to end");
                return;
            }

            long endTicks = DateTime.UtcNow.Ticks;
            long durationMs = (endTicks - sessionStartTicks) / TimeSpan.TicksPerMillisecond;

            sessionActive = false;

            // Skip reporting very short sessions (likely menu exits)
            if (durationMs < MinSessionDurationMs)
            {
                Debug.Log($"[SinglePlayerAnalytics] Session too short ({durationMs}ms), skipping report");
                return;
            }

            Debug.Log($"[SinglePlayerAnalytics] Session ended: {sessionId}, duration={durationMs}ms, result={result}");

            StartCoroutine(ReportSession(sessionId, durationMs, result));
        }

        private IEnumerator ReportSession(string id, long durationMs, string result)
        {
            var payload = new SessionReport
            {
                session_id = id,
                client_id = clientId,
                duration_ms = durationMs,
                result = result
            };

            string json = JsonUtility.ToJson(payload);
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

            string url = $"{ServerUrl}/api/analytics/single-player";

            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 10;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[SinglePlayerAnalytics] Session reported successfully: {id}");
                }
                else
                {
                    // Log and continue - don't interrupt gameplay for analytics failures
                    Debug.LogWarning($"[SinglePlayerAnalytics] Failed to report session: {request.error}");
                }
            }
        }

        public bool IsSessionActive => sessionActive;

        [Serializable]
        private class SessionReport
        {
            public string session_id;
            public string client_id;
            public long duration_ms;
            public string result;
        }
    }
}
