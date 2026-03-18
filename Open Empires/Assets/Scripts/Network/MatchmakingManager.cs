using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace OpenEmpires
{
    public enum MatchmakingState
    {
        Disconnected,
        Connecting,
        Connected,
        Authenticating,
        Authenticated,
        InQueue,
        MatchFound,
        WaitingForPlayers,
        MatchStarting,
        InGame
    }

    public class MatchmakingManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WebSocketClient webSocketClient;

        private string serverUrl;
        private string wsUrl;

        public static MatchmakingManager Instance { get; private set; }

        public MatchmakingState State { get; private set; } = MatchmakingState.Disconnected;
        public string PlayerId { get; private set; }
        public string Username { get; private set; }
        public string AuthToken { get; private set; }
        public int GamePlayerId { get; private set; }
        public string MatchId { get; private set; }
        public Team[] Teams { get; private set; }
        public void SetTeams(Team[] teams) { Teams = teams; }
        public int QueuePosition { get; private set; }
        public string[] QueuePlayers { get; private set; }
        public GameMode CurrentGameMode { get; private set; }

        public event Action<MatchmakingState> OnStateChanged;
        public event Action<string> OnError;
        public event Action<MatchFoundMessage> OnMatchFound;
        public event Action OnMatchStarting;
        public event Action<ServerGameCommandMessage> OnGameCommandReceived;
        public event Action<string> OnPlayerDisconnected;
        public event Action<string> OnPlayerReady;
        public event Action<float> OnPingReceived;
        public event Action<int, float> OnPlayerPingReceived;
        public event Action<ChatServerMessage> OnChatReceived;
        public event Action<PlayerJoinedMatchMessage> OnPlayerJoinedMatch;

        public float SmoothedRTT { get; private set; }

        private HashSet<string> readyPlayers = new HashSet<string>();
        private float pingTimer;
        private const float PingIntervalSeconds = 2.0f;
        private const float RTTSmoothingAlpha = 0.2f;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (webSocketClient == null)
            {
                webSocketClient = GetComponent<WebSocketClient>();
                if (webSocketClient == null)
                {
                    webSocketClient = gameObject.AddComponent<WebSocketClient>();
                }
            }

            SetupWebSocketCallbacks();
        }

        private void SetupWebSocketCallbacks()
        {
            webSocketClient.OnConnected += HandleWebSocketConnected;
            webSocketClient.OnDisconnected += HandleWebSocketDisconnected;
            webSocketClient.OnError += HandleWebSocketError;
            webSocketClient.OnMessageReceived += HandleServerMessageTimestamped;
        }

        private void Update()
        {
            if (State == MatchmakingState.WaitingForPlayers ||
                State == MatchmakingState.MatchStarting ||
                State == MatchmakingState.InGame)
            {
                pingTimer += Time.unscaledDeltaTime;
                if (pingTimer >= PingIntervalSeconds)
                {
                    pingTimer = 0f;
                    SendPing();
                }
            }
        }

        public void SendPing()
        {
            if (webSocketClient == null || !webSocketClient.IsConnected) return;
            webSocketClient.Send(new PingMessage(DateTime.UtcNow.Ticks));
        }

        public void SetServerUrls(string httpUrl, string websocketUrl)
        {
            serverUrl = httpUrl;
            wsUrl = websocketUrl;
            Debug.Log($"[Matchmaking] Server URLs configured: HTTP={serverUrl}, WS={wsUrl}");
        }

        // ========== LOGIN ==========

        public void Login(string username)
        {
            if (State != MatchmakingState.Disconnected)
            {
                Debug.LogWarning("[Matchmaking] Already logged in or connecting");
                return;
            }

            StartCoroutine(LoginCoroutine(username));
        }

        private IEnumerator LoginCoroutine(string username)
        {
            SetState(MatchmakingState.Connecting);

            string json = $"{{\"username\":\"{username}\"}}";
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

            using (var request = new UnityWebRequest($"{serverUrl}/api/auth/login", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[Matchmaking] Login failed: {request.error}");
                    SetState(MatchmakingState.Disconnected);
                    OnError?.Invoke($"Login failed: {request.error}");
                    yield break;
                }

                string responseJson = request.downloadHandler.text;
                Debug.Log($"[Matchmaking] Login response: {responseJson}");

                var response = JsonUtility.FromJson<LoginResponse>(responseJson);
                PlayerId = response.player_id;
                Username = response.username;
                AuthToken = response.token;

                Debug.Log($"[Matchmaking] Logged in as {Username} (ID: {PlayerId})");
            }

            // Connect WebSocket
            webSocketClient.ServerUrl = wsUrl;
            webSocketClient.Connect();
        }

        [Serializable]
        private class LoginResponse
        {
            public string player_id;
            public string token;
            public string username;
        }

        // ========== WEBSOCKET HANDLERS ==========

        private void HandleWebSocketConnected()
        {
            Debug.Log("[Matchmaking] WebSocket connected, authenticating...");
            SetState(MatchmakingState.Authenticating);
            webSocketClient.Send(new AuthenticateMessage(AuthToken));
        }

        private void HandleWebSocketDisconnected(string reason)
        {
            Debug.Log($"[Matchmaking] WebSocket disconnected: {reason}");
            SetState(MatchmakingState.Disconnected);
        }

        private void HandleWebSocketError(string error)
        {
            Debug.LogError($"[Matchmaking] WebSocket error: {error}");
            OnError?.Invoke(error);
        }

        private long lastPongReceivedAtTicks;

        private void HandleServerMessageTimestamped(ServerMessage message, long receivedAtTicks)
        {
            lastPongReceivedAtTicks = receivedAtTicks;
            HandleServerMessage(message);
        }

        private void HandleServerMessage(ServerMessage message)
        {
            switch (message)
            {
                case AuthenticatedMessage auth:
                    Debug.Log($"[Matchmaking] Authenticated as {auth.username}");
                    SetState(MatchmakingState.Authenticated);
                    break;

                case AuthErrorMessage authError:
                    Debug.LogError($"[Matchmaking] Auth error: {authError.message}");
                    OnError?.Invoke(authError.message);
                    SetState(MatchmakingState.Disconnected);
                    break;

                case QueueJoinedMessage queueJoined:
                    QueuePosition = queueJoined.position;
                    CurrentGameMode = queueJoined.game_mode;
                    Debug.Log($"[Matchmaking] Joined queue at position {QueuePosition}");
                    SetState(MatchmakingState.InQueue);
                    break;

                case QueueLeftMessage _:
                    Debug.Log("[Matchmaking] Left queue");
                    QueuePlayers = null;
                    SetState(MatchmakingState.Authenticated);
                    break;

                case QueueUpdateMessage queueUpdate:
                    QueuePosition = queueUpdate.position;
                    Debug.Log($"[Matchmaking] Queue position updated: {QueuePosition}");
                    break;

                case QueuePlayersUpdateMessage queuePlayers:
                    QueuePlayers = queuePlayers.players;
                    break;

                case MatchFoundMessage matchFound:
                    MatchId = matchFound.match_id;
                    Teams = matchFound.teams;
                    GamePlayerId = matchFound.your_game_player_id;
                    CurrentGameMode = matchFound.game_mode;
                    readyPlayers.Clear();
                    Debug.Log($"[Matchmaking] Match found! ID: {MatchId}, Your player ID: {GamePlayerId}");
                    SetState(MatchmakingState.MatchFound);
                    OnMatchFound?.Invoke(matchFound);
                    break;

                case PlayerReadyMessage playerReady:
                    readyPlayers.Add(playerReady.player_id);
                    Debug.Log($"[Matchmaking] Player ready: {playerReady.player_id}");
                    OnPlayerReady?.Invoke(playerReady.player_id);
                    break;

                case MatchStartingMessage matchStarting:
                    Debug.Log($"[Matchmaking] Match starting: {matchStarting.match_id}");
                    SetState(MatchmakingState.MatchStarting);
                    OnMatchStarting?.Invoke();
                    break;

                case ServerGameCommandMessage gameCommand:
                    OnGameCommandReceived?.Invoke(gameCommand);
                    break;

                case PlayerDisconnectedMessage playerDisconnected:
                    Debug.Log($"[Matchmaking] Player disconnected: {playerDisconnected.player_id}");
                    OnPlayerDisconnected?.Invoke(playerDisconnected.player_id);
                    break;

                case PongMessage pong:
                    float rttMs = (float)(lastPongReceivedAtTicks - pong.timestamp) / TimeSpan.TicksPerMillisecond;
                    if (rttMs >= 0f && rttMs <= 1000f)
                    {
                        SmoothedRTT = SmoothedRTT <= 0f
                            ? rttMs
                            : SmoothedRTT * (1f - RTTSmoothingAlpha) + rttMs * RTTSmoothingAlpha;
                        OnPingReceived?.Invoke(rttMs);
                        webSocketClient.Send(new ReportPingMessage((uint)UnityEngine.Mathf.RoundToInt(rttMs)));
                    }
                    break;

                case PlayerPingMessage playerPing:
                    OnPlayerPingReceived?.Invoke(playerPing.game_player_id, playerPing.ping_ms);
                    break;

                case ChatServerMessage chat:
                    OnChatReceived?.Invoke(chat);
                    break;

                case PlayerJoinedMatchMessage playerJoined:
                    Debug.Log($"[Matchmaking] Player joined match: {playerJoined.username}");
                    OnPlayerJoinedMatch?.Invoke(playerJoined);
                    break;

                case ErrorMessage error:
                    Debug.LogError($"[Matchmaking] Server error: {error.message}");
                    OnError?.Invoke(error.message);
                    break;
            }
        }

        // ========== QUEUE OPERATIONS ==========

        public void JoinQueue(GameMode gameMode, int civilization = 0)
        {
            if (State != MatchmakingState.Authenticated)
            {
                Debug.LogWarning("[Matchmaking] Cannot join queue - not authenticated");
                return;
            }

            Debug.Log($"[Matchmaking] Joining queue for {gameMode} with civilization {civilization}...");
            webSocketClient.Send(new JoinQueueMessage(gameMode, civilization));
        }

        public void LeaveQueue()
        {
            if (State != MatchmakingState.InQueue)
            {
                Debug.LogWarning("[Matchmaking] Not in queue");
                return;
            }

            Debug.Log("[Matchmaking] Leaving queue...");
            webSocketClient.Send(new LeaveQueueMessage());
        }

        // ========== MATCH OPERATIONS ==========

        public void SendReady()
        {
            if (State != MatchmakingState.MatchFound && State != MatchmakingState.WaitingForPlayers)
            {
                Debug.LogWarning("[Matchmaking] Cannot send ready - not in match");
                return;
            }

            Debug.Log("[Matchmaking] Sending ready...");
            SetState(MatchmakingState.WaitingForPlayers);
            webSocketClient.Send(new ReadyMessage());
        }

        public void SendGameCommand(int frame, string commandType, string payloadJson)
        {
            if (State != MatchmakingState.InGame && State != MatchmakingState.MatchStarting)
            {
                return;
            }

            webSocketClient.Send(new GameCommandMessage(frame, commandType, payloadJson));
        }

        public void LeaveMatch()
        {
            Debug.Log("[Matchmaking] Leaving match...");
            webSocketClient.Send(new LeaveMatchMessage());
            SetState(MatchmakingState.Authenticated);
        }

        public void StartGame()
        {
            SetState(MatchmakingState.InGame);
        }

        public void SendChat(string channel, string text)
        {
            if (webSocketClient == null || !webSocketClient.IsConnected) return;
            webSocketClient.Send(new ChatClientMessage(channel, text));
        }

        // ========== DISCONNECT ==========

        public void Disconnect()
        {
            webSocketClient.Disconnect();
            PlayerId = null;
            Username = null;
            AuthToken = null;
            GamePlayerId = 0;
            MatchId = null;
            Teams = null;
            QueuePlayers = null;
            SetState(MatchmakingState.Disconnected);
        }

        public void OverrideState(MatchmakingState newState)
        {
            SetState(newState);
        }

        private void SetState(MatchmakingState newState)
        {
            if (State != newState)
            {
                Debug.Log($"[Matchmaking] State: {State} -> {newState}");
                State = newState;
                OnStateChanged?.Invoke(newState);
            }
        }

        public void ResetSmoothedRTT()
        {
            SmoothedRTT = 0f;
        }

        public bool IsInMatch => State == MatchmakingState.MatchFound ||
                                  State == MatchmakingState.WaitingForPlayers ||
                                  State == MatchmakingState.MatchStarting ||
                                  State == MatchmakingState.InGame;

        public bool IsConnected => State != MatchmakingState.Disconnected &&
                                    State != MatchmakingState.Connecting;

        public int TotalPlayersInMatch
        {
            get
            {
                if (Teams == null) return 0;
                int count = 0;
                foreach (var team in Teams)
                {
                    count += team.players.Length;
                }
                return count;
            }
        }

        public bool IsPlayerReady(string playerId) => readyPlayers.Contains(playerId);

        public int ResolveGamePlayerId(string serverPlayerId)
        {
            if (Teams == null) return -1;
            foreach (var team in Teams)
            {
                if (team.players == null) continue;
                foreach (var tp in team.players)
                {
                    if (tp.player_id == serverPlayerId)
                        return tp.game_player_id;
                }
            }
            return -1;
        }

        public string ResolvePlayerName(int gamePlayerId)
        {
            if (Teams == null) return $"Player {gamePlayerId + 1}";
            foreach (var team in Teams)
            {
                if (team.players == null) continue;
                foreach (var tp in team.players)
                {
                    if (tp.game_player_id == gamePlayerId)
                        return tp.username ?? $"Player {gamePlayerId + 1}";
                }
            }
            return $"Player {gamePlayerId + 1}";
        }

        private void OnDestroy()
        {
            if (webSocketClient != null)
            {
                webSocketClient.OnConnected -= HandleWebSocketConnected;
                webSocketClient.OnDisconnected -= HandleWebSocketDisconnected;
                webSocketClient.OnError -= HandleWebSocketError;
                webSocketClient.OnMessageReceived -= HandleServerMessageTimestamped;
            }
        }
    }
}
