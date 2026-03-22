using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace OpenEmpires
{
    public class NetworkManager : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int IsSoftwareRenderer();
#endif

        private static readonly string[] DefaultNames = new string[]
        {
            "Charlemagne", "Eleanor", "Saladin", "Genghis", "Barbarossa",
            "Joan", "Richard", "Tamerlane", "William", "Harald",
            "Isabella", "Suleiman", "Alfred", "Matilda", "Baldwin",
            "Bohemond", "Mansa", "Pachacuti", "Montezuma", "Bayezid",
            "Vlad", "Stefan", "Olga", "Rurik", "Sviatoslav",
            "Aethelstan", "Canute", "Sigurd", "Rollo", "Godfrey",
            "Alexios", "Basil", "Irene", "Mehmed", "Timur",
            "Akbar", "Kublai", "Marco", "Dante", "Petrarch",
            "Sundiata", "Tamar", "Sejong", "Yoritomo", "Lalibela",
            "Baibars", "Casimir", "Skanderbeg", "Gediminas", "Lazar"
        };

        private static string GetRandomName()
        {
            return DefaultNames[UnityEngine.Random.Range(0, DefaultNames.Length)];
        }

        private bool isSoftwareRenderer;

        private struct ServerRegion
        {
            public string Name;
            public string HttpUrl;
            public string WsUrl;
            public int PingMs; // -1 = not yet probed, -2 = timeout/error
        }

        private ServerRegion[] regions = new ServerRegion[]
        {
            new ServerRegion { Name = "Oregon", HttpUrl = "https://openempires.onrender.com", WsUrl = "wss://openempires.onrender.com/ws", PingMs = -1 },
            new ServerRegion { Name = "Virginia", HttpUrl = "https://openempires-virginia.onrender.com", WsUrl = "wss://openempires-virginia.onrender.com/ws", PingMs = -1 },
            new ServerRegion { Name = "Frankfurt", HttpUrl = "https://openempires-6f10.onrender.com", WsUrl = "wss://openempires-6f10.onrender.com/ws", PingMs = -1 },
            new ServerRegion { Name = "Singapore", HttpUrl = "https://openempires-49g9.onrender.com", WsUrl = "wss://openempires-49g9.onrender.com/ws", PingMs = -1 },
        };

        private int selectedRegionIndex = 0;
        private bool userManuallySelectedRegion = false;
        private TMP_Text[] regionButtonLabels;
        private Image[] regionButtonImages;

        [Header("Server Configuration")]
        [SerializeField] private bool useLocalServer = false;
        [SerializeField] private string localServerUrl = "http://localhost:8081";
        [SerializeField] private string localWsUrl = "ws://localhost:8081/ws";

        [Header("References")]
        [SerializeField] private MatchmakingManager matchmakingManager;

        public int LocalPlayerId { get; private set; }
        public bool IsMultiplayer { get; private set; }
        public bool IsConnected => matchmakingManager != null && matchmakingManager.IsConnected;
        public bool GameStarted { get; private set; }

        // Dynamic input delay based on measured RTT
        public int CurrentInputDelay { get; private set; } = 3;
        private const int MinInputDelay = 3;
        private const int MaxInputDelay = 15;

        // Per-tick command exchange: tick -> (playerId -> commands)
        private Dictionary<int, Dictionary<int, List<ICommand>>> commandsByTickByPlayer = new Dictionary<int, Dictionary<int, List<ICommand>>>();
        private int expectedPlayerCount = 2;
        public int ExpectedPlayerCount => expectedPlayerCount;
        public int[] TeamAssignments { get; private set; }
        private float[] playerPings;

        public float GetPlayerPing(int gamePlayerId)
        {
            if (playerPings == null || gamePlayerId < 0 || gamePlayerId >= playerPings.Length)
                return 0f;
            return playerPings[gamePlayerId];
        }

        // Track which players have sent their Noop (tick-complete sentinel) per tick
        private Dictionary<int, HashSet<int>> noopReceivedByTickByPlayer = new Dictionary<int, HashSet<int>>();

        // Late packet tracking per player
        private Dictionary<int, int> latePacketsByPlayer = new Dictionary<int, int>();
        private const int LatePacketWarningThreshold = 5;

        // Reusable buffers to avoid per-tick heap allocations
        private readonly List<int> playerIdBuffer = new List<int>();
        private readonly List<ICommand> consumeBuffer = new List<ICommand>();
        private readonly List<int> readyTicksBuffer = new List<int>();

        // Checksum tracking for desync detection: simTick -> (playerId -> checksum)
        private Dictionary<int, Dictionary<int, uint>> checksumsByTickByPlayer = new Dictionary<int, Dictionary<int, uint>>();
        // Per-system hash tracking: simTick -> (playerId -> systemHashDetail string)
        private Dictionary<int, Dictionary<int, string>> systemHashesByTickByPlayer = new Dictionary<int, Dictionary<int, string>>();
        // Pre/post command hashes: simTick -> (playerId -> (preCmdHash, postCmdHash))
        private Dictionary<int, Dictionary<int, (uint pre, uint post)>> cmdHashesByTickByPlayer = new Dictionary<int, Dictionary<int, (uint pre, uint post)>>();

        // Desync detection state
        public bool DesyncDetected { get; private set; }
        public int DesyncTick { get; private set; }

        // Disconnected players — auto-fill Noop so game continues
        private HashSet<int> disconnectedGamePlayerIds = new HashSet<int>();
        public IReadOnlyCollection<int> DisconnectedGamePlayerIds => disconnectedGamePlayerIds;

        // UI state
        private string username = "Player";
        private string statusText = "";
        private GameMode selectedGameMode = GameMode.OneVsOne;

        // Civilization selection
        private Civilization selectedCivilization = Civilization.English;
        public Civilization SelectedCivilization => selectedCivilization;
        private Civilization[] playerCivilizations;
        private Image[] civButtonImages;
        private TMP_Text civDescriptionLabel;
        private Texture2D englishFlagTex;
        private Texture2D frenchFlagTex;
        private Texture2D hreFlagTex;
        private UnityEngine.Video.VideoPlayer englishFlagVideoPlayer;
        private RenderTexture englishFlagVidRT;
        private UnityEngine.Video.VideoPlayer frenchFlagVideoPlayer;
        private RenderTexture frenchFlagRT;

        public Civilization GetPlayerCivilization(int playerId)
        {
            if (playerCivilizations != null && playerId >= 0 && playerId < playerCivilizations.Length)
                return playerCivilizations[playerId];
            return Civilization.English;
        }

        // Canvas UI
        private GameObject menuCanvasGO;
        private Texture2D menuUnitTex;
        private Texture2D menuTCTex;

        // Panels
        private GameObject disconnectedPanel;
        private GameObject authenticatedPanel;
        private GameObject inQueuePanel;
        private GameObject matchFoundPanel;
        private GameObject matchStartingPanel;
        private GameObject hwAccelWarningPanel;
        private TMP_Text statusLabel;

        // Dynamic text references
        private TMP_InputField nameInputField;
        private TMP_InputField seedInputField;
        private TMP_Text queueSearchLabel;
        private TMP_Text queuePositionLabel;
        private TMP_Text queuePlayersLabel;
        private TMP_Text matchIdLabel;
        private TMP_Text matchRosterLabel;

        // Game mode toggle buttons
        private Image[] modeButtonImages;
        private MatchmakingState lastUIState = MatchmakingState.Disconnected;

        // Dashboard polling
        private TMP_Text playersOnlineLabel;
        private TMP_Text[] modeCountLabels;
        private TMP_Text queueCountsLabel;
        private Coroutine dashboardPollCoroutine;
        private int cachedOnlineCount;
        private int[] cachedQueueCounts = new int[4];

        // Fake player count padding
        private int fakeOnlineBase;
        private int[] fakeQueueBase = new int[4];
        private float fakeCountTimer;
        private float fakeCountNextUpdate;

        // AI queue fill
        private bool aiFillActive;
        private float aiFillTimer;
        private float aiFillNextDelay;
        private readonly List<string> aiFillNames = new List<string>();
        private int aiFillTargetCount;

        // Partial match: deferred ready until AI slots filled
        private MatchFoundMessage pendingMatch;

        private void Awake()
        {
            username = GetRandomName();

            if (matchmakingManager == null)
            {
                matchmakingManager = FindFirstObjectByType<MatchmakingManager>();
                if (matchmakingManager == null)
                {
                    var go = new GameObject("MatchmakingManager");
                    matchmakingManager = go.AddComponent<MatchmakingManager>();
                }
            }

            // Configure server URLs for local dev mode
            if (useLocalServer)
            {
                matchmakingManager.SetServerUrls(localServerUrl, localWsUrl);
            }

            menuUnitTex = Resources.Load<Texture2D>("UnitIcons/spearmanicon");
            menuTCTex   = Resources.Load<Texture2D>("BuildingIcons/towncentericon");

#if UNITY_WEBGL && !UNITY_EDITOR
            isSoftwareRenderer = IsSoftwareRenderer() == 1;
            if (isSoftwareRenderer)
                Debug.LogWarning("[Network] Software renderer detected — multiplayer will be blocked.");
#endif

            BuildMenuUI();

            // Load saved region preference
            selectedRegionIndex = Mathf.Clamp(PlayerPrefs.GetInt("SelectedRegion", 0), 0, regions.Length - 1);
            UpdateRegionButtonVisuals();

            // Probe all regions for latency
            if (!useLocalServer)
                StartCoroutine(ProbeAllRegions());
        }

        private void Start()
        {
            MusicManager.Instance?.PlayMenuMusic(selectedCivilization);
        }

        private void OnEnable()
        {
            if (matchmakingManager != null)
            {
                matchmakingManager.OnStateChanged += HandleStateChanged;
                matchmakingManager.OnMatchFound += HandleMatchFound;
                matchmakingManager.OnMatchStarting += HandleMatchStarting;
                matchmakingManager.OnGameCommandReceived += HandleGameCommand;
                matchmakingManager.OnError += HandleError;
                matchmakingManager.OnPingReceived += HandlePingReceived;
                matchmakingManager.OnPlayerPingReceived += HandlePlayerPingReceived;
                matchmakingManager.OnPlayerJoinedMatch += HandlePlayerJoinedMatch;
            }
        }

        private void OnDisable()
        {
            if (matchmakingManager != null)
            {
                matchmakingManager.OnStateChanged -= HandleStateChanged;
                matchmakingManager.OnMatchFound -= HandleMatchFound;
                matchmakingManager.OnMatchStarting -= HandleMatchStarting;
                matchmakingManager.OnGameCommandReceived -= HandleGameCommand;
                matchmakingManager.OnError -= HandleError;
                matchmakingManager.OnPingReceived -= HandlePingReceived;
                matchmakingManager.OnPlayerPingReceived -= HandlePlayerPingReceived;
                matchmakingManager.OnPlayerJoinedMatch -= HandlePlayerJoinedMatch;
            }
        }

        private void HandleStateChanged(MatchmakingState state)
        {
            switch (state)
            {
                case MatchmakingState.Disconnected:
                    statusText = "Disconnected";
                    IsMultiplayer = false;
                    aiFillActive = false;
                    aiFillNames.Clear();
                    pendingMatch = null;
                    break;
                case MatchmakingState.Connecting:
                    statusText = "Connecting...";
                    break;
                case MatchmakingState.Authenticating:
                    statusText = "Authenticating...";
                    break;
                case MatchmakingState.Authenticated:
                    statusText = $"Logged in as {matchmakingManager.Username}";
                    aiFillActive = false;
                    aiFillNames.Clear();
                    pendingMatch = null;
                    break;
                case MatchmakingState.InQueue:
                    statusText = $"In queue (position {matchmakingManager.QueuePosition})";
                    break;
                case MatchmakingState.MatchFound:
                    statusText = "Match found! Loading...";
                    break;
                case MatchmakingState.WaitingForPlayers:
                    statusText = "Waiting for players...";
                    break;
                case MatchmakingState.MatchStarting:
                    statusText = "Match starting!";
                    break;
                case MatchmakingState.InGame:
                    statusText = "In game";
                    break;
            }
        }

        private void HandleMatchFound(MatchFoundMessage match)
        {
            aiFillActive = false;
            aiFillNames.Clear();
            pendingMatch = null;

            LocalPlayerId = match.your_game_player_id;
            IsMultiplayer = true;

            int humanCount = 0;
            foreach (var team in match.teams)
                humanCount += team.players.Length;

            int fullPlayerCount = GetPlayerCountForMode(match.game_mode);

            Debug.Log($"[Network] Match found. Local ID: {LocalPlayerId}, Humans: {humanCount}, Total needed: {fullPlayerCount}");

            if (humanCount >= fullPlayerCount)
            {
                // Full match — proceed immediately
                FinishMatchSetup(match, fullPlayerCount, humanCount, new int[0]);
                matchmakingManager.SendReady();
            }
            else
            {
                // Partial match — stay in queue UI, AI fills remaining slots
                pendingMatch = match;
                aiFillActive = true;
                aiFillTimer = 0f;
                aiFillNextDelay = 30f;
                aiFillTargetCount = fullPlayerCount;

                // Populate aiFillNames with real humans from the match
                aiFillNames.Clear();
                foreach (var team in match.teams)
                    foreach (var p in team.players)
                        aiFillNames.Add(p.username);

                // Force back to InQueue so the queue panel stays visible
                matchmakingManager.OverrideState(MatchmakingState.InQueue);
                SFXManager.Instance?.PlayUI(SFXType.LobbyJoin, 0.7f);
            }
        }

        private void HandlePlayerJoinedMatch(PlayerJoinedMatchMessage msg)
        {
            if (pendingMatch == null) return;

            // Add player to pendingMatch teams
            for (int t = 0; t < pendingMatch.teams.Length; t++)
            {
                if (pendingMatch.teams[t].team_id == msg.team_id)
                {
                    var list = new List<TeamPlayer>(pendingMatch.teams[t].players);
                    list.Add(new TeamPlayer
                    {
                        player_id = msg.player_id,
                        username = msg.username,
                        game_player_id = msg.game_player_id,
                        is_ready = false,
                        civilization = msg.civilization
                    });
                    pendingMatch.teams[t].players = list.ToArray();
                    break;
                }
            }

            // Rebuild aiFillNames from humans only (discards any AI already added)
            aiFillNames.Clear();
            foreach (var team in pendingMatch.teams)
                foreach (var p in team.players)
                    aiFillNames.Add(p.username);

            // Reset 30s countdown
            aiFillTimer = 0f;
            aiFillNextDelay = 30f;
            aiFillActive = true;

            // Check if match is now full — no AI needed
            int humanCount = 0;
            foreach (var team in pendingMatch.teams)
                humanCount += team.players.Length;
            int fullPlayerCount = GetPlayerCountForMode(pendingMatch.game_mode);

            if (humanCount >= fullPlayerCount)
            {
                // Full match — proceed immediately
                aiFillActive = false;
                FinishMatchSetup(pendingMatch, fullPlayerCount, humanCount, new int[0]);
                pendingMatch = null;
                matchmakingManager.OverrideState(MatchmakingState.MatchFound);
                matchmakingManager.SendReady();
            }

            // Play join sound
            SFXManager.Instance?.PlayUI(SFXType.LobbyJoin, 0.7f);
        }

        private static int DeterministicStringHash(string s)
        {
            if (s == null) return 0;
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < s.Length; i++)
                {
                    hash ^= s[i];
                    hash *= 16777619u;
                }
                return (int)hash;
            }
        }

        private void FinishMatchSetup(MatchFoundMessage match, int fullPlayerCount, int humanCount, int[] aiPlayerIds)
        {
            // Derive deterministic map seed from match ID so both clients generate the same map
            var cfg = GameBootstrapper.Instance?.Config;
            if (cfg != null && matchmakingManager?.MatchId != null)
            {
                cfg.MapSeed = DeterministicStringHash(matchmakingManager.MatchId);
                Debug.Log($"[SyncCheck] MatchId=\"{matchmakingManager.MatchId}\" -> MapSeed={cfg.MapSeed}");
            }

            expectedPlayerCount = humanCount; // relay only syncs humans
            int playerCount = fullPlayerCount;

            // Build expanded teams with AI slots
            var expandedTeams = new Team[match.teams.Length];
            int nextId = humanCount;

            for (int t = 0; t < match.teams.Length; t++)
            {
                var allPlayers = new List<TeamPlayer>(match.teams[t].players);
                int teamSize = playerCount / match.teams.Length;

                while (allPlayers.Count < teamSize)
                {
                    allPlayers.Add(new TeamPlayer
                    {
                        player_id = $"ai_{nextId}",
                        username = aiFillNames[nextId],
                        game_player_id = nextId,
                        is_ready = true
                    });
                    nextId++;
                }
                expandedTeams[t] = new Team { team_id = match.teams[t].team_id, players = allPlayers.ToArray() };
            }

            TeamAssignments = new int[playerCount];
            playerPings = new float[playerCount];
            foreach (var team in expandedTeams)
                foreach (var player in team.players)
                    if (player.game_player_id >= 0 && player.game_player_id < playerCount)
                        TeamAssignments[player.game_player_id] = team.team_id;

            matchmakingManager.SetTeams(expandedTeams);

            // Build civilization array from TeamPlayer data
            playerCivilizations = new Civilization[playerCount];
            foreach (var team in expandedTeams)
                foreach (var player in team.players)
                    if (player.game_player_id >= 0 && player.game_player_id < playerCount)
                        playerCivilizations[player.game_player_id] = (Civilization)player.civilization;
            // AI-filled slots get a deterministic civilization based on map seed
            int mapSeed = cfg != null ? cfg.MapSeed : 0;
            foreach (int aiId in aiPlayerIds)
                if (aiId >= 0 && aiId < playerCount)
                {
                    uint civHash = (uint)(mapSeed * 31 + aiId * 7919);
                    playerCivilizations[aiId] = (Civilization)(civHash % 3);
                }

            var bootstrapper = GameBootstrapper.Instance;
            if (bootstrapper != null)
            {
                bootstrapper.SetPlayerCount(playerCount);
                bootstrapper.SetCivilizations(playerCivilizations);
                if (aiPlayerIds.Length > 0)
                    bootstrapper.SetAIPlayerIds(aiPlayerIds);
            }
        }

        private void HandleMatchStarting()
        {
            Debug.Log("[Network] Match starting!");

            // Pre-seed commands for initial ticks so the game can start immediately
            // Without this, the first InputDelayTicks would stall waiting for commands
            PreSeedCommands(CurrentInputDelay);

            if (dashboardPollCoroutine != null) { StopCoroutine(dashboardPollCoroutine); dashboardPollCoroutine = null; }
            GameStarted = true;
            matchmakingManager.StartGame();
        }

        /// <summary>
        /// Pre-sends empty commands (Noop) for the initial ticks.
        /// This allows the game to start immediately without waiting for network round-trips.
        /// With input delay, we send commands for tick N+delay, so ticks 0 to delay-1
        /// need to have commands pre-seeded.
        /// </summary>
        public void PreSeedCommands(int delayTicks)
        {
            Debug.Log($"[Network] Pre-seeding commands for ticks 0 to {delayTicks - 1}");

            for (int tick = 0; tick < delayTicks; tick++)
            {
                // Send empty command list (just Noop) for each initial tick
                SendCommands(new List<ICommand>(), tick);
            }
        }

        private void HandleGameCommand(ServerGameCommandMessage msg)
        {
            int tick = msg.command.frame;
            int playerId = msg.from_player_id;

            // Check for late packets (commands for ticks we've already processed)
            var simulation = GameBootstrapper.Instance?.Simulation;
            if (simulation != null && tick < simulation.CurrentTick)
            {
                // Silently drop late packets from disconnected players — expected after disconnect
                if (disconnectedGamePlayerIds.Contains(playerId))
                    return;

                int ticksBehind = simulation.CurrentTick - tick;

                // Track late packets per player
                if (!latePacketsByPlayer.ContainsKey(playerId))
                {
                    latePacketsByPlayer[playerId] = 0;
                }
                latePacketsByPlayer[playerId]++;

                Debug.LogWarning($"[Network] Late packet dropped: tick {tick} from player {playerId} (current: {simulation.CurrentTick}, {ticksBehind} ticks behind)");
                NetworkDiagnostics.Instance?.RecordLatePacket();

                // Warn about potential desync if too many late packets from a player
                if (latePacketsByPlayer[playerId] >= LatePacketWarningThreshold)
                {
                    Debug.LogError($"[Network] DESYNC WARNING: Player {playerId} has {latePacketsByPlayer[playerId]} late packets. Game state may be inconsistent!");
                    NetworkDiagnostics.Instance?.RecordDesyncWarning();
                }
                return;
            }

            // Drop all commands from disconnected players (we auto-fill Noop for them)
            if (disconnectedGamePlayerIds.Contains(playerId))
                return;

            var command = CommandSerializer.FromJson(msg.command.command_type, msg.command.payload, playerId);

            if (command != null)
            {
                NetworkDiagnostics.Instance?.RecordCommandReceived();

                // Record Noop arrival as tick-complete sentinel
                if (command is NoopCommand noopCmd)
                {
                    if (!noopReceivedByTickByPlayer.ContainsKey(tick))
                        noopReceivedByTickByPlayer[tick] = new HashSet<int>();
                    noopReceivedByTickByPlayer[tick].Add(playerId);

                    int simTick = noopCmd.SimTick;
                    // Only record non-zero checksums (zero = skipped tick for perf)
                    if (noopCmd.StateChecksum != 0)
                    {
                        if (!checksumsByTickByPlayer.ContainsKey(simTick))
                            checksumsByTickByPlayer[simTick] = new Dictionary<int, uint>();
                        checksumsByTickByPlayer[simTick][playerId] = noopCmd.StateChecksum;

                        // Track per-system hash detail
                        if (!systemHashesByTickByPlayer.ContainsKey(simTick))
                            systemHashesByTickByPlayer[simTick] = new Dictionary<int, string>();
                        systemHashesByTickByPlayer[simTick][playerId] = noopCmd.SystemHashDetail ?? "";

                        // Track pre/post command hashes
                        if (!cmdHashesByTickByPlayer.ContainsKey(simTick))
                            cmdHashesByTickByPlayer[simTick] = new Dictionary<int, (uint, uint)>();
                        cmdHashesByTickByPlayer[simTick][playerId] = (noopCmd.PreCmdHash, noopCmd.PostCmdHash);
                    }
                }

                if (!commandsByTickByPlayer.ContainsKey(tick))
                {
                    commandsByTickByPlayer[tick] = new Dictionary<int, List<ICommand>>();
                }

                if (!commandsByTickByPlayer[tick].ContainsKey(playerId))
                {
                    commandsByTickByPlayer[tick][playerId] = new List<ICommand>();
                }

                commandsByTickByPlayer[tick][playerId].Add(command);
            }
        }

        private void HandleError(string error)
        {
            statusText = $"Error: {error}";
            Debug.LogError($"[Network] Error: {error}");
        }

        private void HandlePingReceived(float rttMs)
        {
            NetworkDiagnostics.Instance?.RecordPing(rttMs);

            // Compute desired input delay: max(3, ceil(smoothedRTT / tickIntervalMs) + 1)
            float smoothedRTT = matchmakingManager.SmoothedRTT;
            float tickIntervalMs = (1f / 30f) * 1000f; // ~33.33ms
            int desired = Mathf.Clamp(Mathf.CeilToInt(smoothedRTT / tickIntervalMs) + 1, MinInputDelay, MaxInputDelay);

            if (desired > CurrentInputDelay)
            {
                Debug.Log($"[Network] Input delay increasing: {CurrentInputDelay} -> {desired} (SmoothedRTT: {smoothedRTT:F1}ms)");
                CurrentInputDelay = desired;
            }
            else if (desired < CurrentInputDelay)
            {
                // Decrease by 1 tick at a time for smooth recovery
                int newDelay = CurrentInputDelay - 1;
                Debug.Log($"[Network] Input delay decreasing: {CurrentInputDelay} -> {newDelay} (SmoothedRTT: {smoothedRTT:F1}ms)");
                CurrentInputDelay = newDelay;
            }
        }

        public void ResetAfterFocusReturn()
        {
            CurrentInputDelay = MinInputDelay;
            matchmakingManager?.ResetSmoothedRTT();
        }

        private void HandlePlayerPingReceived(int gamePlayerId, float pingMs)
        {
            if (playerPings != null && gamePlayerId >= 0 && gamePlayerId < playerPings.Length)
                playerPings[gamePlayerId] = pingMs;
        }

        public void SendCommands(List<ICommand> commands, int tick, bool isGapFill = false)
        {
            if (!IsMultiplayer || matchmakingManager == null) return;

            // Send all real commands first
            foreach (var cmd in commands)
            {
                var (commandType, payload) = CommandSerializer.ToJson(cmd);
                matchmakingManager.SendGameCommand(tick, commandType, payload);
                NetworkDiagnostics.Instance?.RecordCommandSent(isNoop: false);
            }

            // Always send Noop at the end to signal "tick complete"
            // Server only marks player as ready when it receives Noop
            var sim = GameBootstrapper.Instance?.Simulation;
            int simTick = sim?.CurrentTick ?? 0;
            // Compute checksum periodically for desync detection (every 300 ticks ~15s, or first 100 ticks)
            uint checksum = 0;
            uint systemHash = 0;
            uint preCmdHash = 0;
            uint postCmdHash = 0;
            string systemHashDetail = null;
            if (sim != null)
            {
                bool shouldHash = simTick <= 100 || simTick % 300 == 0;
                if (shouldHash)
                {
                    checksum = sim.ComputeStateChecksum();
                    var sysHashes = sim.LastSystemHashes;
                    for (int i = 0; i < sysHashes.Length; i++)
                        systemHash ^= sysHashes[i];
                    systemHashDetail = sim.GetSystemHashDetail();
                }
                preCmdHash = sim.LastPreCmdHash;
                postCmdHash = sim.LastPostCmdHash;
            }
            var noop = new NoopCommand(LocalPlayerId, checksum, simTick, systemHash, preCmdHash, postCmdHash, systemHashDetail);
            var (noopType, noopPayload) = CommandSerializer.ToJson(noop);
            matchmakingManager.SendGameCommand(tick, noopType, noopPayload);
            NetworkDiagnostics.Instance?.RecordCommandSent(isNoop: true);
        }

        public void SetExpectedPlayerCount(int count)
        {
            expectedPlayerCount = count;
            Debug.Log($"[Network] Expected player count set to {expectedPlayerCount}");
        }

        public bool HasCommandsForTick(int tick)
        {
            return commandsByTickByPlayer.ContainsKey(tick);
        }

        public void MarkPlayerDisconnected(int gamePlayerId)
        {
            disconnectedGamePlayerIds.Add(gamePlayerId);
        }

        public int BufferedTickCount(int currentTick)
        {
            int count = 0;
            foreach (var tick in noopReceivedByTickByPlayer.Keys)
                if (tick >= currentTick) count++;
            return count;
        }

        public bool HasCommandsFromAllPlayersForTick(int tick)
        {
            if (!noopReceivedByTickByPlayer.TryGetValue(tick, out var noopPlayers))
                noopPlayers = new HashSet<int>();

            // Count connected players who have sent + disconnected players (auto-filled)
            int readyCount = noopPlayers.Count;
            foreach (int dcId in disconnectedGamePlayerIds)
            {
                if (!noopPlayers.Contains(dcId))
                    readyCount++;
            }
            return readyCount >= expectedPlayerCount;
        }

        public List<ICommand> ConsumeCommandsForTick(int tick)
        {
            if (commandsByTickByPlayer.TryGetValue(tick, out var playerCommands))
            {
                commandsByTickByPlayer.Remove(tick);
                noopReceivedByTickByPlayer.Remove(tick);

                // Validate checksums for any sim ticks that have data from all players
                ValidateChecksums();

                // Flatten all player commands into a single list, sorted by PlayerId
                consumeBuffer.Clear();
                playerIdBuffer.Clear();
                foreach (var kvp in playerCommands)
                    playerIdBuffer.Add(kvp.Key);
                playerIdBuffer.Sort();
                for (int i = 0; i < playerIdBuffer.Count; i++)
                    consumeBuffer.AddRange(playerCommands[playerIdBuffer[i]]);

                // Periodic cleanup of late packet tracking
                if (tick % 900 == 0)
                    latePacketsByPlayer.Clear();

                return consumeBuffer;
            }
            return null;
        }

        /// <summary>
        /// Validates checksums keyed by sim tick. With asymmetric input delays, players
        /// compute checksums at different sim ticks for the same command tick. By keying
        /// on sim tick, we only compare checksums that represent the same game state.
        /// </summary>
        private void ValidateChecksums()
        {
            // Collect sim ticks that are ready for validation
            // With deadline-based broadcast, timed-out players send synthetic Noops with checksum=0
            // (which are skipped), so we may never get checksums from all players.
            // Process entries with all players, OR entries older than 60 ticks with at least 2 checksums.
            int currentSimTick = GameBootstrapper.Instance?.Simulation?.CurrentTick ?? 0;
            readyTicksBuffer.Clear();
            foreach (var kvp in checksumsByTickByPlayer)
            {
                if (kvp.Value.Count >= expectedPlayerCount ||
                    currentSimTick - kvp.Key > 60)
                {
                    readyTicksBuffer.Add(kvp.Key);
                }
            }

            foreach (int simTick in readyTicksBuffer)
            {
                var playerChecksums = checksumsByTickByPlayer[simTick];

                // Get the first checksum as reference
                uint? referenceChecksum = null;
                int referencePlayerId = -1;

                foreach (var kvp in playerChecksums)
                {
                    if (referenceChecksum == null)
                    {
                        referenceChecksum = kvp.Value;
                        referencePlayerId = kvp.Key;
                    }
                    else if (kvp.Value != referenceChecksum.Value)
                    {
                        DesyncDetected = true;
                        DesyncTick = simTick;
                        Debug.LogError($"[Network] DESYNC DETECTED at sim tick {simTick}! " +
                            $"Player {referencePlayerId} checksum: {referenceChecksum.Value:X8}, " +
                            $"Player {kvp.Key} checksum: {kvp.Value:X8}");

                        // Enhanced: compare pre/post command hashes to distinguish command vs system divergence
                        if (cmdHashesByTickByPlayer.TryGetValue(simTick, out var cmdHashes))
                        {
                            foreach (var ch in cmdHashes)
                                Debug.LogError($"[Network] Player {ch.Key} PreCmd={ch.Value.pre:X8} PostCmd={ch.Value.post:X8}");
                        }

                        // Enhanced: compare per-system hashes to narrow down divergent system
                        if (systemHashesByTickByPlayer.TryGetValue(simTick, out var sysHashes))
                        {
                            string[] systemNames = { "Baseline", "Movement", "Combat", "Separation",
                                "TowerCombat", "BuildingCombat", "Projectiles+Death", "Gathering", "Construction+Training+Fog" };
                            foreach (var sh in sysHashes)
                                Debug.LogError($"[Network] Player {sh.Key} SystemHashes={sh.Value}");

                            // Compare individual hashes across players to find first divergent system
                            var playerIds = new List<int>(sysHashes.Keys);
                            if (playerIds.Count >= 2)
                            {
                                string[] hashesA = sysHashes[playerIds[0]].Split(',');
                                string[] hashesB = sysHashes[playerIds[1]].Split(',');
                                int len = Mathf.Min(hashesA.Length, hashesB.Length);
                                for (int si = 0; si < len; si++)
                                {
                                    if (hashesA[si] != hashesB[si])
                                    {
                                        string sysName = si < systemNames.Length ? systemNames[si] : $"System[{si}]";
                                        Debug.LogError($"[Network] FIRST DIVERGENT SYSTEM: [{si}] {sysName} — " +
                                            $"Player {playerIds[0]}={hashesA[si]} vs Player {playerIds[1]}={hashesB[si]}");
                                        break;
                                    }
                                }
                            }
                        }

                        // State dump: log first 20 units for debugging
                        var dumpSim = GameBootstrapper.Instance?.Simulation;
                        if (dumpSim != null)
                        {
                            var units = dumpSim.UnitRegistry.GetAllUnits();
                            var dumpSb = new System.Text.StringBuilder();
                            dumpSb.Append($"[Network] Local unit state (first 20 of {units.Count}):\n");
                            int dumpCount = Mathf.Min(20, units.Count);
                            for (int di = 0; di < dumpCount; di++)
                            {
                                var u = units[di];
                                dumpSb.Append($"  #{u.Id} pos=({u.SimPosition.x.Raw},{u.SimPosition.z.Raw}) state={u.State}\n");
                            }
                            Debug.LogError(dumpSb.ToString());
                        }

                        // Find the earliest tick with data from both players, scanning backwards
                        int firstDivergentTick = simTick;
                        int lastMatchedTick = -1;
                        for (int t = simTick; t >= simTick - 100; t--)
                        {
                            if (t < 0) break;
                            if (checksumsByTickByPlayer.TryGetValue(t, out var cs) && cs.Count >= 2)
                            {
                                var pids = new List<int>(cs.Keys);
                                if (cs[pids[0]] == cs[pids[1]])
                                {
                                    lastMatchedTick = t;
                                    break; // Found where states were still identical
                                }
                                else
                                {
                                    firstDivergentTick = t;
                                }
                            }
                        }

                        Debug.LogError($"[Network] First divergent tick: {firstDivergentTick}, last matched tick: {lastMatchedTick}");

                        // If no match found in lookback, check earliest available tick (now preserved for ticks 0-20)
                        if (lastMatchedTick == -1)
                        {
                            for (int t = 0; t <= 20; t++)
                            {
                                if (checksumsByTickByPlayer.TryGetValue(t, out var earlyCs) && earlyCs.Count >= 2)
                                {
                                    var earlyPids = new List<int>(earlyCs.Keys);
                                    bool match = earlyCs[earlyPids[0]] == earlyCs[earlyPids[1]];
                                    Debug.LogError($"[Network] Earliest retained tick={t}: " +
                                        $"P{earlyPids[0]}={earlyCs[earlyPids[0]]:X8} P{earlyPids[1]}={earlyCs[earlyPids[1]]:X8} " +
                                        $"{(match ? "MATCHED" : "DIVERGED")}");
                                    if (!match)
                                    {
                                        Debug.LogError($"[Network] >>> STATES WERE NEVER IN SYNC — init divergence from tick {t}");
                                        break;
                                    }
                                    // If matched, keep scanning to find where it first diverged
                                }
                            }
                        }

                        // Log system hashes around the first divergent tick (the most useful window)
                        for (int t = firstDivergentTick - 1; t <= Mathf.Min(firstDivergentTick + 2, simTick); t++)
                        {
                            if (t < 0) continue;
                            if (systemHashesByTickByPlayer.TryGetValue(t, out var sh) && sh.Count >= 2)
                            {
                                var pids = new List<int>(sh.Keys);
                                Debug.LogError($"[Network]   tick={t}: P{pids[0]}={sh[pids[0]]} P{pids[1]}={sh[pids[1]]}");
                            }
                            else
                            {
                                Debug.LogError($"[Network]   tick={t}: (no data from both players)");
                            }
                        }

                        // Identify which system first diverged at the first divergent tick
                        if (systemHashesByTickByPlayer.TryGetValue(firstDivergentTick, out var fdSysHashes) && fdSysHashes.Count >= 2)
                        {
                            string[] systemNames = { "Baseline", "Movement", "Combat", "Separation",
                                "TowerCombat", "BuildingCombat", "Projectiles+Death", "Gathering", "Construction+Training+Fog" };
                            var fdPids = new List<int>(fdSysHashes.Keys);
                            string[] hashesA = fdSysHashes[fdPids[0]].Split(',');
                            string[] hashesB = fdSysHashes[fdPids[1]].Split(',');
                            int len = Mathf.Min(hashesA.Length, hashesB.Length);
                            for (int si = 0; si < len; si++)
                            {
                                string sysName = si < systemNames.Length ? systemNames[si] : $"System[{si}]";
                                if (hashesA[si] != hashesB[si])
                                {
                                    Debug.LogError($"[Network] ROOT CAUSE: At tick {firstDivergentTick}, " +
                                        $"system [{si}] {sysName} first diverged — " +
                                        $"P{fdPids[0]}={hashesA[si]} vs P{fdPids[1]}={hashesB[si]}");
                                    break;
                                }
                                else
                                {
                                    Debug.LogError($"[Network]   [{si}] {sysName}: MATCHED ({hashesA[si]})");
                                }
                            }
                        }

                        // Log recent command history to verify both clients received same commands
                        Debug.LogError($"[Network] Command history around desync tick {simTick}:");
                        for (int t = Mathf.Max(0, simTick - 3); t <= simTick; t++)
                        {
                            if (commandsByTickByPlayer.TryGetValue(t, out var tickCmds))
                            {
                                foreach (var pc in tickCmds)
                                    Debug.LogError($"[Network]   tick={t} player={pc.Key} cmds={pc.Value.Count}: " +
                                        string.Join(", ", pc.Value.ConvertAll(c => c.Type.ToString())));
                            }
                        }

                        NetworkDiagnostics.Instance?.RecordDesyncWarning();
                    }
                }

                // Keep entries for 50 ticks so we can inspect prior ticks on desync
                if (currentSimTick - simTick > 50 && simTick > 20)
                {
                    checksumsByTickByPlayer.Remove(simTick);
                    systemHashesByTickByPlayer.Remove(simTick);
                    cmdHashesByTickByPlayer.Remove(simTick);
                }
            }
        }

        public void ReadPendingData()
        {
            // No-op: WebSocket messages are processed via callbacks
        }

        private static int GetPlayerCountForMode(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.TwoVsTwo: return 4;
                case GameMode.ThreeVsThree: return 6;
                case GameMode.FourVsFour: return 8;
                default: return 2;
            }
        }

        private string GenerateUniqueAIName()
        {
            for (int i = 0; i < 50; i++)
            {
                string name = GetRandomName();
                if (!aiFillNames.Contains(name) && name != username)
                    return name;
            }
            return GetRandomName() + " II";
        }

        private void StartAIFilledGame()
        {
            // Leave the server queue
            matchmakingManager.LeaveQueue();

            // Configure as local game
            IsMultiplayer = false;
            LocalPlayerId = 0;

            // Set map seed
            if (int.TryParse(seedInputField.text, out int seed))
            {
                var cfg = GameBootstrapper.Instance?.Config;
                if (cfg != null) cfg.MapSeed = seed;
            }

            int playerCount = aiFillTargetCount;
            int halfCount = playerCount / 2;

            // Build team assignments: [0]=team0, [1..half-1]=team0, [half..end]=team1
            int[] teamAssignments = new int[playerCount];
            for (int i = 0; i < playerCount; i++)
                teamAssignments[i] = i < halfCount ? 0 : 1;

            // Build AI player IDs (everyone except player 0)
            int[] aiIds = new int[playerCount - 1];
            for (int i = 0; i < aiIds.Length; i++)
                aiIds[i] = i + 1;

            // Set civilizations for AI-filled single player game
            playerCivilizations = new Civilization[playerCount];
            playerCivilizations[0] = selectedCivilization;
            for (int i = 1; i < playerCount; i++)
                playerCivilizations[i] = (Civilization)UnityEngine.Random.Range(0, 3);
            GameBootstrapper.Instance?.SetCivilizations(playerCivilizations);

            // Synthesize Team[] for PlayerListUI name resolution
            var team0Players = new TeamPlayer[halfCount];
            for (int i = 0; i < halfCount; i++)
            {
                team0Players[i] = new TeamPlayer
                {
                    player_id = i == 0 ? "local" : $"ai_{i}",
                    username = aiFillNames[i],
                    game_player_id = i,
                    civilization = (int)playerCivilizations[i]
                };
            }

            var team1Players = new TeamPlayer[playerCount - halfCount];
            for (int i = 0; i < team1Players.Length; i++)
            {
                int pid = halfCount + i;
                team1Players[i] = new TeamPlayer
                {
                    player_id = $"ai_{pid}",
                    username = aiFillNames[pid],
                    game_player_id = pid,
                    civilization = (int)playerCivilizations[pid]
                };
            }

            var teams = new Team[]
            {
                new Team { team_id = 0, players = team0Players },
                new Team { team_id = 1, players = team1Players }
            };
            matchmakingManager.SetTeams(teams);

            // Configure GameBootstrapper
            var bootstrapper = GameBootstrapper.Instance;
            if (bootstrapper != null)
            {
                bootstrapper.SetPlayerCount(playerCount);
                bootstrapper.SetAIPlayerIds(aiIds);
                bootstrapper.SetTeamAssignments(teamAssignments);
            }

            aiFillActive = false;

            if (dashboardPollCoroutine != null) { StopCoroutine(dashboardPollCoroutine); dashboardPollCoroutine = null; }
            GameStarted = true;
            FullscreenManager.Instance?.EnterFullscreen();
        }

        /// <summary>
        /// Called when we timeout waiting for commands from another player.
        /// Marks missing players as disconnected so the game can continue.
        /// </summary>
        public void HandlePlayerTimeout(int tick)
        {
            // Determine which players are missing commands for this tick
            noopReceivedByTickByPlayer.TryGetValue(tick, out var noopPlayers);
            var received = noopPlayers ?? new HashSet<int>();

            bool anyNewDisconnect = false;
            for (int pid = 0; pid < expectedPlayerCount; pid++)
            {
                if (received.Contains(pid)) continue;
                if (disconnectedGamePlayerIds.Contains(pid)) continue;

                Debug.LogWarning($"[Network] Player {pid} timed out at tick {tick}. Marking as disconnected.");
                MarkPlayerDisconnected(pid);
                anyNewDisconnect = true;

                // Notify simulation via GameBootstrapper
                var sim = GameBootstrapper.Instance?.Simulation;
                sim?.MarkPlayerDisconnected(pid);

                string name = matchmakingManager != null ? ResolvePlayerName(pid) : $"Player {pid + 1}";
                ChatManager.AddSystemMessage($"{name} has disconnected and forfeited.");
            }

            if (!anyNewDisconnect)
            {
                // All remaining players are already marked disconnected — end game
                Debug.LogError($"[Network] All other players disconnected at tick {tick}. Ending game.");
                GameStarted = false;
                IsMultiplayer = false;
                if (matchmakingManager != null)
                    matchmakingManager.Disconnect();
                ClearMatchData();
            }
        }

        private string ResolvePlayerName(int gamePlayerId)
        {
            if (matchmakingManager?.Teams == null) return $"Player {gamePlayerId + 1}";
            foreach (var team in matchmakingManager.Teams)
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

        private void Update()
        {
            if (menuCanvasGO == null) return;

            if (GameStarted)
            {
                if (menuCanvasGO.activeSelf) menuCanvasGO.SetActive(false);
                return;
            }

            if (!menuCanvasGO.activeSelf) menuCanvasGO.SetActive(true);

            var state = matchmakingManager?.State ?? MatchmakingState.Disconnected;

            if (state != lastUIState)
            {
                lastUIState = state;
                disconnectedPanel.SetActive(state == MatchmakingState.Disconnected);
                authenticatedPanel.SetActive(state == MatchmakingState.Authenticated);
                inQueuePanel.SetActive(state == MatchmakingState.InQueue);
                matchFoundPanel.SetActive(state == MatchmakingState.MatchFound || state == MatchmakingState.WaitingForPlayers);
                matchStartingPanel.SetActive(state == MatchmakingState.MatchStarting);
            }

            // Fluctuate fake player counts
            fakeCountTimer += Time.deltaTime;
            if (fakeCountTimer >= fakeCountNextUpdate)
            {
                fakeCountTimer = 0f;
                fakeCountNextUpdate = UnityEngine.Random.Range(8f, 20f);
                fakeOnlineBase = Mathf.Clamp(fakeOnlineBase + UnityEngine.Random.Range(-3, 4), 1, 30);
                DistributeFakeQueueCounts();
            }

            // Update dynamic text
            if (state == MatchmakingState.Disconnected)
            {
                if (playersOnlineLabel != null)
                {
                    int displayOnline = Mathf.Max(cachedOnlineCount, fakeOnlineBase);
                    playersOnlineLabel.text = $"{displayOnline} Players Online";
                }
            }
            else if (state == MatchmakingState.Authenticated)
            {
                if (modeCountLabels != null)
                    for (int i = 0; i < modeCountLabels.Length; i++)
                    {
                        int display = Mathf.Max(cachedQueueCounts[i], fakeQueueBase[i]);
                        modeCountLabels[i].text = $"{display} searching";
                    }
            }
            else if (state == MatchmakingState.InQueue)
            {
                queueSearchLabel.text = $"Searching for {selectedGameMode} match...";
                queuePositionLabel.text = $"Queue position: {matchmakingManager?.QueuePosition}";

                if (queueCountsLabel != null)
                {
                    int total = 0;
                    for (int i = 0; i < 4; i++)
                        total += Mathf.Max(cachedQueueCounts[i], fakeQueueBase[i]);
                    queueCountsLabel.text = $"{total} players searching";
                }

                // AI fill logic
                if (!aiFillActive)
                {
                    aiFillActive = true;
                    aiFillTimer = 0f;
                    aiFillNextDelay = 30f;
                    aiFillNames.Clear();
                    aiFillNames.Add(username); // slot 0 is the human
                    aiFillTargetCount = GetPlayerCountForMode(selectedGameMode);
                }

                aiFillTimer += Time.deltaTime;
                if (aiFillTimer >= aiFillNextDelay && aiFillNames.Count < aiFillTargetCount)
                {
                    aiFillNames.Add(GenerateUniqueAIName());
                    aiFillTimer = 0f;
                    aiFillNextDelay = UnityEngine.Random.Range(2f, 20f);
                }

                // Display human + AI names joined so far
                queuePlayersLabel.text = "Players:\n" + string.Join("\n", aiFillNames);

                if (aiFillNames.Count >= aiFillTargetCount)
                {
                    if (pendingMatch != null)
                    {
                        // Partial server match — finalize with AI and send ready
                        aiFillActive = false;
                        int humanCount = 0;
                        foreach (var team in pendingMatch.teams)
                            humanCount += team.players.Length;

                        var aiIds = new List<int>();
                        for (int i = humanCount; i < aiFillTargetCount; i++)
                            aiIds.Add(i);

                        FinishMatchSetup(pendingMatch, aiFillTargetCount, humanCount, aiIds.ToArray());
                        pendingMatch = null;
                        matchmakingManager.OverrideState(MatchmakingState.MatchFound);
                        matchmakingManager.SendReady();
                    }
                    else
                    {
                        StartAIFilledGame();
                    }
                }
            }
            else if (state == MatchmakingState.MatchFound || state == MatchmakingState.WaitingForPlayers)
            {
                matchIdLabel.text = $"Your ID: {matchmakingManager?.GamePlayerId}";
                UpdateMatchRoster();
            }

            statusLabel.text = statusText ?? "";
        }

        private void UpdateMatchRoster()
        {
            if (matchmakingManager?.Teams == null) { matchRosterLabel.text = ""; return; }
            var sb = new System.Text.StringBuilder();
            foreach (var team in matchmakingManager.Teams)
            {
                sb.AppendLine($"Team {team.team_id}:");
                foreach (var player in team.players)
                {
                    string ready = matchmakingManager.IsPlayerReady(player.player_id) ? " [Ready]" : "";
                    sb.AppendLine($"  {player.username}{ready}");
                }
            }
            matchRosterLabel.text = sb.ToString();
        }

        /// <summary>
        /// Clears all per-match dictionaries to prevent stale data from lingering between matches.
        /// </summary>
        public void ClearMatchData()
        {
            commandsByTickByPlayer.Clear();
            noopReceivedByTickByPlayer.Clear();
            checksumsByTickByPlayer.Clear();
            systemHashesByTickByPlayer.Clear();
            cmdHashesByTickByPlayer.Clear();
            latePacketsByPlayer.Clear();
        }

        private void OnDestroy()
        {
            ClearMatchData();
            if (matchmakingManager != null)
            {
                matchmakingManager.Disconnect();
            }
        }

        // ---- Region Selection ----

        private IEnumerator ProbeAllRegions()
        {
            int n = regions.Length;

            // ---- Round 1: warmup (wake sleeping Render instances) ----
            var warmupOps = new UnityWebRequestAsyncOperation[n];
            var warmupReqs = new UnityWebRequest[n];
            for (int i = 0; i < n; i++)
            {
                warmupReqs[i] = UnityWebRequest.Get(regions[i].HttpUrl + "/health");
                warmupReqs[i].timeout = 10;
                warmupOps[i] = warmupReqs[i].SendWebRequest();
            }
            // Wait for all warmup requests to finish
            for (int i = 0; i < n; i++)
            {
                yield return warmupOps[i];
                warmupReqs[i].Dispose();
            }

            // ---- Round 2: measure actual latency ----
            var measureOps = new UnityWebRequestAsyncOperation[n];
            var measureReqs = new UnityWebRequest[n];
            var startTimes = new float[n];
            float measureStart = Time.realtimeSinceStartup;
            for (int i = 0; i < n; i++)
            {
                measureReqs[i] = UnityWebRequest.Get(regions[i].HttpUrl + "/health");
                measureReqs[i].timeout = 5;
                startTimes[i] = measureStart;
                measureOps[i] = measureReqs[i].SendWebRequest();
            }
            // Wait for all measurement requests and record results
            for (int i = 0; i < n; i++)
            {
                yield return measureOps[i];
                if (measureReqs[i].result == UnityWebRequest.Result.Success)
                {
                    float elapsed = Time.realtimeSinceStartup - startTimes[i];
                    regions[i].PingMs = Mathf.RoundToInt(elapsed * 1000f);
                }
                else
                {
                    regions[i].PingMs = -2;
                }
                measureReqs[i].Dispose();

                // Update button label
                if (regionButtonLabels != null && i < regionButtonLabels.Length && regionButtonLabels[i] != null)
                {
                    string pingText = regions[i].PingMs >= 0 ? $"{regions[i].PingMs}ms" : "err";
                    regionButtonLabels[i].text = $"{regions[i].Name} {pingText}";
                }
            }

            // Auto-select lowest ping region (unless user already picked one manually)
            if (!userManuallySelectedRegion)
            {
                int bestIdx = 0;
                int bestPing = int.MaxValue;
                for (int i = 0; i < regions.Length; i++)
                {
                    if (regions[i].PingMs > 0 && regions[i].PingMs < bestPing)
                    {
                        bestPing = regions[i].PingMs;
                        bestIdx = i;
                    }
                }
                selectedRegionIndex = bestIdx;
                PlayerPrefs.SetInt("SelectedRegion", selectedRegionIndex);
                UpdateRegionButtonVisuals();
            }
        }

        private void SelectRegion(int index)
        {
            selectedRegionIndex = index;
            userManuallySelectedRegion = true;
            PlayerPrefs.SetInt("SelectedRegion", index);
            UpdateRegionButtonVisuals();
        }

        private void UpdateRegionButtonVisuals()
        {
            if (regionButtonImages == null) return;
            for (int i = 0; i < regionButtonImages.Length; i++)
            {
                if (regionButtonImages[i] != null)
                    regionButtonImages[i].color = i == selectedRegionIndex
                        ? new Color(0.25f, 0.4f, 0.7f)
                        : new Color(0.22f, 0.22f, 0.25f);
            }
        }

        // ---- Civilization Selection ----

        /// <summary>Royal Arms of England: three gold lions passant on red.</summary>
        private static Texture2D GenerateEnglishFlag(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[w * h];
            Color red = new Color(0.7f, 0.05f, 0.05f);
            Color gold = new Color(0.9f, 0.75f, 0.1f);

            // Fill red background
            for (int i = 0; i < pixels.Length; i++) pixels[i] = red;

            // Draw three simplified lions (horizontal bars with a diamond head)
            int lionW = w / 3;
            int lionH = Mathf.Max(2, h / 7);
            int cx = w / 2;
            int[] lionYs = { h * 3 / 4, h / 2, h / 4 };
            for (int li = 0; li < 3; li++)
            {
                int ly = lionYs[li];
                // Body bar
                for (int dy = -lionH / 2; dy <= lionH / 2; dy++)
                    for (int dx = -lionW / 2; dx <= lionW / 2; dx++)
                    {
                        int px = cx + dx;
                        int py = ly + dy;
                        if (px >= 0 && px < w && py >= 0 && py < h)
                            pixels[py * w + px] = gold;
                    }
                // Head (small square to the right)
                int headSize = Mathf.Max(1, lionH);
                int headX = cx + lionW / 2 + 1;
                for (int dy = -headSize / 2; dy <= headSize / 2; dy++)
                    for (int dx = 0; dx < headSize; dx++)
                    {
                        int px = headX + dx;
                        int py = ly + dy;
                        if (px >= 0 && px < w && py >= 0 && py < h)
                            pixels[py * w + px] = gold;
                    }
                // Legs (two small ticks below body)
                int legY = ly - lionH / 2 - 1;
                int leg1X = cx - lionW / 4;
                int leg2X = cx + lionW / 4;
                if (legY >= 0)
                {
                    if (leg1X >= 0 && leg1X < w) pixels[legY * w + leg1X] = gold;
                    if (leg2X >= 0 && leg2X < w) pixels[legY * w + leg2X] = gold;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Royal Arms of France: gold fleur-de-lis pattern on blue (Azure seme-de-lis Or).</summary>
        private static Texture2D GenerateFrenchFlag(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[w * h];
            Color blue = new Color(0.05f, 0.1f, 0.55f);
            Color gold = new Color(0.9f, 0.75f, 0.1f);

            // Fill blue background
            for (int i = 0; i < pixels.Length; i++) pixels[i] = blue;

            // Draw a grid of simplified fleur-de-lis (cross shape: vertical line + horizontal bar + dot on top)
            int spacingX = w / 4;
            int spacingY = h / 3;
            for (int row = 0; row < 3; row++)
            {
                int offsetX = (row % 2 == 1) ? spacingX / 2 : 0;
                for (int col = 0; col < 4; col++)
                {
                    int cx = spacingX / 2 + col * spacingX + offsetX;
                    int cy = spacingY / 2 + row * spacingY;
                    if (cx < 0 || cx >= w || cy < 0 || cy >= h) continue;

                    // Vertical stem
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        int py = cy + dy;
                        if (py >= 0 && py < h) pixels[py * w + cx] = gold;
                    }
                    // Horizontal arms
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int px = cx + dx;
                        int py = cy + 1;
                        if (px >= 0 && px < w && py >= 0 && py < h) pixels[py * w + px] = gold;
                    }
                    // Top dot
                    int topY = cy + 2;
                    if (topY < h) pixels[topY * w + cx] = gold;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Holy Roman Empire: black eagle on gold/yellow field.</summary>
        private static Texture2D GenerateHREFlag(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[w * h];
            Color gold = new Color(0.95f, 0.8f, 0.1f);
            Color black = new Color(0.1f, 0.1f, 0.1f);
            Color red = new Color(0.7f, 0.1f, 0.1f);

            // Fill gold background
            for (int i = 0; i < pixels.Length; i++) pixels[i] = gold;

            int cx = w / 2;
            int cy = h / 2;

            // Eagle body (central oval/diamond)
            int bodyW = Mathf.Max(2, w / 5);
            int bodyH = Mathf.Max(3, h / 3);
            for (int dy = -bodyH; dy <= bodyH; dy++)
                for (int dx = -bodyW; dx <= bodyW; dx++)
                {
                    // Diamond shape
                    if (Mathf.Abs(dx) * bodyH + Mathf.Abs(dy) * bodyW <= bodyW * bodyH)
                    {
                        int px = cx + dx;
                        int py = cy + dy;
                        if (px >= 0 && px < w && py >= 0 && py < h)
                            pixels[py * w + px] = black;
                    }
                }

            // Wings (angled lines extending from body)
            for (int i = 1; i <= Mathf.Max(3, w / 4); i++)
            {
                int wingDy = i / 2;
                // Left wing
                int lx = cx - bodyW - i;
                int ly = cy + wingDy;
                if (lx >= 0 && ly < h) pixels[ly * w + lx] = black;
                if (lx >= 0 && ly - 1 >= 0) pixels[(ly - 1) * w + lx] = black;
                // Right wing
                int rx = cx + bodyW + i;
                int ry = cy + wingDy;
                if (rx < w && ry < h) pixels[ry * w + rx] = black;
                if (rx < w && ry - 1 >= 0) pixels[(ry - 1) * w + rx] = black;
            }

            // Head (small block above body)
            for (int dy = 0; dy < 2; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int px = cx + dx;
                    int py = cy + bodyH + 1 + dy;
                    if (px >= 0 && px < w && py >= 0 && py < h)
                        pixels[py * w + px] = black;
                }

            // Red beak
            int beakY = cy + bodyH + 2;
            int beakX = cx + 1;
            if (beakX < w && beakY < h) pixels[beakY * w + beakX] = red;

            // Tail (small lines below body)
            for (int dx = -1; dx <= 1; dx++)
            {
                int px = cx + dx;
                int py = cy - bodyH - 1;
                if (px >= 0 && px < w && py >= 0 && py < h)
                    pixels[py * w + px] = black;
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void SelectCivilization(int index)
        {
            selectedCivilization = (Civilization)index;
            UpdateCivButtonVisuals();
            UpdateCivDescription();
            MusicManager.Instance?.PlayMenuMusic(selectedCivilization);
        }

        private void UpdateCivDescription()
        {
            if (civDescriptionLabel == null) return;
            civDescriptionLabel.text = selectedCivilization switch
            {
                Civilization.English =>
                    "<b>English</b>\n\n" +
                    "<color=#AAAABB>Influence: Manors</color>\n" +
                    "<color=#CCCCCC>English Town Centers emit an influence that boosts food production on farms.</color>\n\n" +
                    "<color=#AAAABB>Unique Unit: Longbowman</color>\n" +
                    "<color=#CCCCCC>An Archer with greater range.</color>",
                Civilization.French =>
                    "<b>French</b>\n\n" +
                    "<color=#AAAABB>Influence: Royal Demesne</color>\n" +
                    "<color=#CCCCCC>French Landmarks emit an influence that reduces the cost of all units by 15%.</color>\n\n" +
                    "<color=#AAAABB>Unique Unit: Gendarme</color>\n" +
                    "<color=#CCCCCC>A Horseman with greater health.</color>",
                Civilization.HolyRomanEmpire =>
                    "<b>HRE (Holy Roman Empire)</b>\n\n" +
                    "<color=#AAAABB>Influence: Undefined</color>\n\n" +
                    "<color=#AAAABB>Unique Unit: Landsknecht</color>\n" +
                    "<color=#CCCCCC>A Spearman with greater movement speed.</color>",
                _ => ""
            };
        }

        private void UpdateCivButtonVisuals()
        {
            if (civButtonImages == null) return;
            for (int i = 0; i < civButtonImages.Length; i++)
            {
                if (civButtonImages[i] != null)
                    civButtonImages[i].color = i == (int)selectedCivilization
                        ? new Color(0.25f, 0.4f, 0.7f)
                        : new Color(0.22f, 0.22f, 0.25f);
            }
        }

        public Texture GetCivFlagTexture(Civilization civ)
        {
            return civ switch
            {
                Civilization.English => englishFlagVidRT != null ? (Texture)englishFlagVidRT : englishFlagTex,
                Civilization.French => frenchFlagRT != null ? (Texture)frenchFlagRT : frenchFlagTex,
                Civilization.HolyRomanEmpire => hreFlagTex,
                _ => englishFlagTex,
            };
        }

        // ---- Canvas UI Build ----

        private void BuildMenuUI()
        {
            var canvasGO = new GameObject("MenuCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            menuCanvasGO = canvasGO;

            BuildDisconnectedPanel(canvasGO.transform);
            BuildAuthenticatedPanel(canvasGO.transform);
            BuildInQueuePanel(canvasGO.transform);
            BuildMatchFoundPanel(canvasGO.transform);
            BuildMatchStartingPanel(canvasGO.transform);

            // Status label (bottom center, shared across all panels)
            statusLabel = MakeLabel(canvasGO.transform, "", 0f, -200f, 400f, 30f, 14, FontStyles.Normal, TextAlignmentOptions.Center);

            // Initial state
            authenticatedPanel.SetActive(false);
            inQueuePanel.SetActive(false);
            matchFoundPanel.SetActive(false);
            matchStartingPanel.SetActive(false);

            dashboardPollCoroutine = StartCoroutine(PollDashboard());

            fakeOnlineBase = UnityEngine.Random.Range(1, 31);
            DistributeFakeQueueCounts();
            fakeCountNextUpdate = UnityEngine.Random.Range(8f, 20f);
        }

        private void DistributeFakeQueueCounts()
        {
            int total = UnityEngine.Random.Range(1, 13);
            for (int i = 0; i < 4; i++) fakeQueueBase[i] = 0;
            for (int j = 0; j < total; j++)
                fakeQueueBase[UnityEngine.Random.Range(0, 4)]++;
        }

        private void BuildDisconnectedPanel(Transform parent)
        {
            // Full-screen dark overlay
            var overlayGO = new GameObject("Overlay");
            overlayGO.transform.SetParent(parent, false);
            var overlayRT = overlayGO.AddComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;
            var overlayImg = overlayGO.AddComponent<Image>();
            overlayImg.color = new Color(0.10f, 0.10f, 0.12f, 0.96f);
            overlayImg.raycastTarget = false;

            disconnectedPanel = overlayGO;

            // Center card — wide layout for left-right split
            float panelW = 880f, panelH = 480f;
            float leftX = -215f;  // center of left half
            float rightX = 220f;  // center of right half

            var cardGO = new GameObject("Card");
            cardGO.transform.SetParent(overlayGO.transform, false);
            var cardRT = cardGO.AddComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta = new Vector2(panelW, panelH);
            var cardImg = cardGO.AddComponent<Image>();
            cardImg.color = new Color(0.14f, 0.14f, 0.16f, 0.98f);
            var cardOutline = cardGO.AddComponent<Outline>();
            cardOutline.effectColor = new Color(0.3f, 0.3f, 0.35f, 0.6f);
            cardOutline.effectDistance = new Vector2(2, -2);

            // Vertical divider between left and right
            var vDivGO = new GameObject("VerticalDivider");
            vDivGO.transform.SetParent(cardGO.transform, false);
            var vDivRT = vDivGO.AddComponent<RectTransform>();
            vDivRT.anchorMin = new Vector2(0.5f, 0.5f);
            vDivRT.anchorMax = new Vector2(0.5f, 0.5f);
            vDivRT.pivot = new Vector2(0.5f, 0.5f);
            vDivRT.anchoredPosition = new Vector2(0f, 0f);
            vDivRT.sizeDelta = new Vector2(1f, panelH - 40f);
            var vDivImg = vDivGO.AddComponent<Image>();
            vDivImg.color = new Color(0.28f, 0.28f, 0.32f);
            vDivImg.raycastTarget = false;

            // =====================
            //  LEFT SIDE — Menu
            // =====================
            float y = panelH / 2f;

            // Title
            y -= 38f;
            MakeLabel(cardGO.transform, "Open Empires", leftX, y, 380f, 48f, 36, FontStyles.Bold, TextAlignmentOptions.Center, true);
            y -= 24f;
            MakeLabel(cardGO.transform, "v0.1", leftX, y, 380f, 20f, 12, FontStyles.Normal, TextAlignmentOptions.Center, true, new Color(0.55f, 0.55f, 0.55f));

            // Divider
            y -= 20f;
            var divGO = new GameObject("Divider");
            divGO.transform.SetParent(cardGO.transform, false);
            var divRT = divGO.AddComponent<RectTransform>();
            divRT.anchorMin = new Vector2(0.5f, 0.5f);
            divRT.anchorMax = new Vector2(0.5f, 0.5f);
            divRT.pivot = new Vector2(0.5f, 0.5f);
            divRT.anchoredPosition = new Vector2(leftX, y);
            divRT.sizeDelta = new Vector2(300f, 1f);
            var divImg = divGO.AddComponent<Image>();
            divImg.color = new Color(0.3f, 0.3f, 0.35f);
            divImg.raycastTarget = false;

            // Player Name
            y -= 20f;
            MakeLabel(cardGO.transform, "Player Name", leftX, y, 300f, 18f, 12, FontStyles.Normal, TextAlignmentOptions.Center, true, new Color(0.6f, 0.6f, 0.65f));
            y -= 24f;
            nameInputField = CreateInputField(cardGO.transform, username, leftX, y, 240f, 32f);

            // Map Seed — extra gap before new section
            y -= 30f;
            MakeLabel(cardGO.transform, "Map Seed", leftX, y, 300f, 18f, 12, FontStyles.Normal, TextAlignmentOptions.Center, true, new Color(0.6f, 0.6f, 0.65f));
            y -= 24f;
            int defaultSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            seedInputField = CreateInputField(cardGO.transform, defaultSeed.ToString(), leftX, y, 240f, 32f);
            seedInputField.contentType = TMP_InputField.ContentType.IntegerNumber;

            // Server Region — extra gap before new section
            y -= 30f;
            MakeLabel(cardGO.transform, "Server Region", leftX, y, 300f, 18f, 12, FontStyles.Normal, TextAlignmentOptions.Center, true, new Color(0.6f, 0.6f, 0.65f));
            y -= 22f;
            regionButtonLabels = new TMP_Text[regions.Length];
            regionButtonImages = new Image[regions.Length];
            // 2x2 grid for region buttons
            float regionBtnW = 115f;
            float regionBtnH = 24f;
            float regionGapX = 6f;
            float regionGapY = 4f;
            for (int i = 0; i < regions.Length; i++)
            {
                int regionIdx = i;
                int col = i % 2;
                int row = i / 2;
                float rx = leftX - (regionBtnW + regionGapX) / 2f + col * (regionBtnW + regionGapX);
                float rcy = y - row * (regionBtnH + regionGapY);

                var btnGO = new GameObject($"Region{regions[i].Name}");
                btnGO.transform.SetParent(cardGO.transform, false);
                var btnRT = btnGO.AddComponent<RectTransform>();
                btnRT.anchorMin = new Vector2(0.5f, 0.5f);
                btnRT.anchorMax = new Vector2(0.5f, 0.5f);
                btnRT.pivot = new Vector2(0.5f, 0.5f);
                btnRT.anchoredPosition = new Vector2(rx, rcy);
                btnRT.sizeDelta = new Vector2(regionBtnW, regionBtnH);

                var img = btnGO.AddComponent<Image>();
                img.color = new Color(0.22f, 0.22f, 0.25f);
                regionButtonImages[i] = img;

                var btn = btnGO.AddComponent<Button>();
                btn.onClick.AddListener(() => SelectRegion(regionIdx));

                var txtGO = new GameObject("Text");
                txtGO.transform.SetParent(btnGO.transform, false);
                var trt = txtGO.AddComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = Vector2.zero;
                trt.offsetMax = Vector2.zero;
                var tmp = txtGO.AddComponent<TextMeshProUGUI>();
                tmp.text = $"{regions[i].Name} --ms";
                tmp.fontSize = 11;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                regionButtonLabels[i] = tmp;
            }
            y -= (regionBtnH + regionGapY) * 2f; // skip past 2 rows
            UpdateRegionButtonVisuals();

            // Action buttons
            y -= 18f;
            CreateButton(cardGO.transform, "Single Player", leftX, y, 250f, 34f, () =>
            {
                username = nameInputField.text;
                LocalPlayerId = 0;
                IsMultiplayer = false;

                if (int.TryParse(seedInputField.text, out int seed))
                {
                    var cfg = GameBootstrapper.Instance?.Config;
                    if (cfg != null) cfg.MapSeed = seed;
                }

                GameBootstrapper.Instance?.SetPlayerCount(2);

                playerCivilizations = new Civilization[2];
                playerCivilizations[0] = selectedCivilization;
                playerCivilizations[1] = (Civilization)UnityEngine.Random.Range(0, 3);
                GameBootstrapper.Instance?.SetCivilizations(playerCivilizations);

                if (dashboardPollCoroutine != null) { StopCoroutine(dashboardPollCoroutine); dashboardPollCoroutine = null; }
                GameStarted = true;
                FullscreenManager.Instance?.EnterFullscreen();
            });

            // Multiplayer button — taller to include players online text inside
            y -= 48f;
            var mpBtnGO = new GameObject("MultiplayerButton");
            mpBtnGO.transform.SetParent(cardGO.transform, false);
            var mpBtnRT = mpBtnGO.AddComponent<RectTransform>();
            mpBtnRT.anchorMin = new Vector2(0.5f, 0.5f);
            mpBtnRT.anchorMax = new Vector2(0.5f, 0.5f);
            mpBtnRT.pivot = new Vector2(0.5f, 0.5f);
            mpBtnRT.anchoredPosition = new Vector2(leftX, y);
            mpBtnRT.sizeDelta = new Vector2(250f, 46f);
            var mpImg = mpBtnGO.AddComponent<Image>();
            mpImg.color = Color.white;
            var mpOutline = mpBtnGO.AddComponent<Outline>();
            mpOutline.effectColor = new Color(0.4f, 0.4f, 0.45f, 0.5f);
            mpOutline.effectDistance = new Vector2(1, -1);
            var mpBtn = mpBtnGO.AddComponent<Button>();
            var mpColors = mpBtn.colors;
            mpColors.normalColor = new Color(0.28f, 0.28f, 0.32f);
            mpColors.highlightedColor = new Color(0.38f, 0.38f, 0.42f);
            mpColors.pressedColor = new Color(0.20f, 0.20f, 0.23f);
            mpBtn.colors = mpColors;
            mpBtn.onClick.AddListener(() =>
            {
                if (isSoftwareRenderer)
                {
                    ShowHwAccelWarning();
                    return;
                }
                username = nameInputField.text;

                if (!useLocalServer)
                {
                    var region = regions[selectedRegionIndex];
                    matchmakingManager.SetServerUrls(region.HttpUrl, region.WsUrl);
                }

                FullscreenManager.Instance?.EnterFullscreen();
                matchmakingManager?.Login(username);
            });
            // "Multiplayer" label inside button (upper)
            var mpTxtGO = new GameObject("Text");
            mpTxtGO.transform.SetParent(mpBtnGO.transform, false);
            var mpTxtRT = mpTxtGO.AddComponent<RectTransform>();
            mpTxtRT.anchorMin = Vector2.zero;
            mpTxtRT.anchorMax = Vector2.one;
            mpTxtRT.offsetMin = new Vector2(0f, 12f);
            mpTxtRT.offsetMax = Vector2.zero;
            var mpTmp = mpTxtGO.AddComponent<TextMeshProUGUI>();
            mpTmp.text = "Multiplayer";
            mpTmp.fontSize = 16;
            mpTmp.fontStyle = FontStyles.Bold;
            mpTmp.alignment = TextAlignmentOptions.Center;
            mpTmp.color = Color.white;
            // "X Players Online" sub-label inside button (lower)
            var mpSubGO = new GameObject("OnlineLabel");
            mpSubGO.transform.SetParent(mpBtnGO.transform, false);
            var mpSubRT = mpSubGO.AddComponent<RectTransform>();
            mpSubRT.anchorMin = Vector2.zero;
            mpSubRT.anchorMax = Vector2.one;
            mpSubRT.offsetMin = Vector2.zero;
            mpSubRT.offsetMax = new Vector2(0f, -22f);
            var mpSubTmp = mpSubGO.AddComponent<TextMeshProUGUI>();
            mpSubTmp.text = "";
            mpSubTmp.fontSize = 10;
            mpSubTmp.alignment = TextAlignmentOptions.Center;
            mpSubTmp.color = new Color(0.7f, 0.7f, 0.75f);
            mpSubTmp.raycastTarget = false;
            playersOnlineLabel = mpSubTmp;

            y -= 48f;
            CreateButton(cardGO.transform, "Join Discord", leftX, y, 250f, 34f, () =>
            {
                Application.OpenURL("https://discord.gg/htUt9qv6Vk");
            });

            // =====================
            //  RIGHT SIDE — Civilization Picker + Description
            // =====================
            float ry = panelH / 2f;

            // Civilization label at top
            ry -= 38f;
            MakeLabel(cardGO.transform, "Civilization", rightX, ry, 380f, 22f, 14, FontStyles.Bold, TextAlignmentOptions.Center, true);

            // Civ flag buttons
            ry -= 30f;
            englishFlagTex = GenerateEnglishFlag(32, 20);
            frenchFlagTex = GenerateFrenchFlag(32, 20);
            hreFlagTex = GenerateHREFlag(32, 20);
            string[] civNames = { "English", "French", "HRE" };
            Texture2D[] civTextures = { englishFlagTex, frenchFlagTex, hreFlagTex };
            civButtonImages = new Image[civNames.Length];
            float civBtnW = 120f;
            float civTotalW = civNames.Length * civBtnW + (civNames.Length - 1) * 8f;
            float civStartX = rightX - civTotalW / 2f + civBtnW / 2f;
            for (int i = 0; i < civNames.Length; i++)
            {
                int civIdx = i;
                var civBtnGO = new GameObject($"Civ{civNames[i]}");
                civBtnGO.transform.SetParent(cardGO.transform, false);
                var civBtnRT = civBtnGO.AddComponent<RectTransform>();
                civBtnRT.anchorMin = new Vector2(0.5f, 0.5f);
                civBtnRT.anchorMax = new Vector2(0.5f, 0.5f);
                civBtnRT.pivot = new Vector2(0.5f, 0.5f);
                civBtnRT.anchoredPosition = new Vector2(civStartX + i * (civBtnW + 8f), ry);
                civBtnRT.sizeDelta = new Vector2(civBtnW, 30f);

                var civImg = civBtnGO.AddComponent<Image>();
                civImg.color = new Color(0.22f, 0.22f, 0.25f);

                var civBtn = civBtnGO.AddComponent<Button>();
                civBtn.onClick.AddListener(() => SelectCivilization(civIdx));

                // Flag icon
                var flagGO = new GameObject("Flag");
                flagGO.transform.SetParent(civBtnGO.transform, false);
                var flagRT = flagGO.AddComponent<RectTransform>();
                flagRT.anchorMin = new Vector2(0, 0.5f);
                flagRT.anchorMax = new Vector2(0, 0.5f);
                flagRT.pivot = new Vector2(0, 0.5f);
                flagRT.anchoredPosition = new Vector2(8f, 0f);
                flagRT.sizeDelta = new Vector2(26f, 17f);
                var flagImg = flagGO.AddComponent<RawImage>();
                flagImg.texture = civTextures[i];
                flagImg.raycastTarget = false;

                // Animated video flags
                if (civIdx == (int)Civilization.English)
                {
                    var flagClip = Resources.Load<UnityEngine.Video.VideoClip>("Flags/EnglishFlag");
                    if (flagClip != null)
                    {
                        englishFlagVidRT = new RenderTexture(128, 80, 0);
                        englishFlagVideoPlayer = flagGO.AddComponent<UnityEngine.Video.VideoPlayer>();
                        englishFlagVideoPlayer.clip = flagClip;
                        englishFlagVideoPlayer.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture;
                        englishFlagVideoPlayer.targetTexture = englishFlagVidRT;
                        englishFlagVideoPlayer.isLooping = true;
                        englishFlagVideoPlayer.playOnAwake = true;
                        englishFlagVideoPlayer.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.None;
                        englishFlagVideoPlayer.Play();
                        flagImg.texture = englishFlagVidRT;
                    }
                }
                else if (civIdx == (int)Civilization.French)
                {
                    var flagClip = Resources.Load<UnityEngine.Video.VideoClip>("Flags/FrenchFlag");
                    if (flagClip != null)
                    {
                        frenchFlagRT = new RenderTexture(128, 80, 0);
                        frenchFlagVideoPlayer = flagGO.AddComponent<UnityEngine.Video.VideoPlayer>();
                        frenchFlagVideoPlayer.clip = flagClip;
                        frenchFlagVideoPlayer.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture;
                        frenchFlagVideoPlayer.targetTexture = frenchFlagRT;
                        frenchFlagVideoPlayer.isLooping = true;
                        frenchFlagVideoPlayer.playOnAwake = true;
                        frenchFlagVideoPlayer.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.None;
                        frenchFlagVideoPlayer.Play();
                        flagImg.texture = frenchFlagRT;
                    }
                }
                civButtonImages[i] = civBtnGO.GetComponent<Image>();

                // Label
                var civTxtGO = new GameObject("Text");
                civTxtGO.transform.SetParent(civBtnGO.transform, false);
                var civTxtRT = civTxtGO.AddComponent<RectTransform>();
                civTxtRT.anchorMin = Vector2.zero;
                civTxtRT.anchorMax = Vector2.one;
                civTxtRT.offsetMin = new Vector2(36f, 0f);
                civTxtRT.offsetMax = new Vector2(-4f, 0f);
                var civTmp = civTxtGO.AddComponent<TextMeshProUGUI>();
                civTmp.text = civNames[i];
                civTmp.fontSize = 12;
                civTmp.alignment = TextAlignmentOptions.Center;
                civTmp.color = Color.white;
            }
            // Random starting civ
            selectedCivilization = (Civilization)UnityEngine.Random.Range(0, 3);
            UpdateCivButtonVisuals();
            UpdateCivDescription();

            // Divider below civ buttons
            ry -= 24f;
            var civDivGO = new GameObject("CivDivider");
            civDivGO.transform.SetParent(cardGO.transform, false);
            var civDivRT = civDivGO.AddComponent<RectTransform>();
            civDivRT.anchorMin = new Vector2(0.5f, 0.5f);
            civDivRT.anchorMax = new Vector2(0.5f, 0.5f);
            civDivRT.pivot = new Vector2(0.5f, 0.5f);
            civDivRT.anchoredPosition = new Vector2(rightX, ry);
            civDivRT.sizeDelta = new Vector2(380f, 1f);
            var civDivImg = civDivGO.AddComponent<Image>();
            civDivImg.color = new Color(0.28f, 0.28f, 0.32f);
            civDivImg.raycastTarget = false;

            // Civ description text area
            ry -= 16f;
            civDescriptionLabel = MakeLabel(cardGO.transform, "", rightX, ry, 370f, 280f, 13,
                FontStyles.Normal, TextAlignmentOptions.TopLeft, true, Color.white);
            civDescriptionLabel.enableWordWrapping = true;
            civDescriptionLabel.overflowMode = TextOverflowModes.Overflow;
            civDescriptionLabel.richText = true;
            // Anchor the description to top so text flows down
            var descRT = civDescriptionLabel.GetComponent<RectTransform>();
            descRT.pivot = new Vector2(0.5f, 1f);

            // Set initial description
            UpdateCivDescription();
        }

        private void ShowHwAccelWarning()
        {
            if (hwAccelWarningPanel != null)
            {
                hwAccelWarningPanel.SetActive(true);
                return;
            }

            float panelW = 420f, panelH = 220f;
            var panelGO = CreateCenterPanel(menuCanvasGO.transform, "HwAccelWarning", panelW, panelH);
            panelGO.GetComponent<RectTransform>().SetAsLastSibling();
            panelGO.GetComponent<Image>().color = new Color(0.14f, 0.14f, 0.16f, 0.98f);
            hwAccelWarningPanel = panelGO;

            float y = panelH / 2f;
            y -= 24f;
            MakeLabel(panelGO.transform, "Hardware Acceleration Required", 0f, y, panelW - 40f, 28f, 18, FontStyles.Bold, TextAlignmentOptions.Center, true);

            y -= 36f;
            MakeLabel(panelGO.transform,
                "Multiplayer requires hardware acceleration.\nEnable it in your browser settings\n(Chrome: Settings > System > Use graphics acceleration)\nthen restart your browser.",
                0f, y - 20f, panelW - 40f, 80f, 14, FontStyles.Normal, TextAlignmentOptions.Center, true, new Color(0.85f, 0.85f, 0.85f));

            y -= 100f;
            CreateButton(panelGO.transform, "OK", 0f, y, 120f, 36f, () =>
            {
                hwAccelWarningPanel.SetActive(false);
            });
        }

        private void BuildAuthenticatedPanel(Transform parent)
        {
            float panelW = 300f, panelH = 280f;
            var panelGO = CreateCenterPanel(parent, "AuthPanel", panelW, panelH);
            authenticatedPanel = panelGO;

            float y = panelH / 2f;

            // Title
            y -= 20f;
            MakeLabel(panelGO.transform, "Open Empires", 0f, y, panelW, 28f, 18, FontStyles.Bold, TextAlignmentOptions.Center, true);

            y -= 36f;
            MakeLabel(panelGO.transform, "Select Game Mode:", 0f, y, panelW, 24f, 14, FontStyles.Normal, TextAlignmentOptions.Center, true);

            // Mode buttons
            y -= 36f;
            string[] modes = { "1v1", "2v2", "3v3", "4v4" };
            modeButtonImages = new Image[modes.Length];
            float totalW = modes.Length * 60f + (modes.Length - 1) * 5f;
            float startX = -totalW / 2f + 30f;
            for (int i = 0; i < modes.Length; i++)
            {
                int modeIdx = i;
                var btnGO = new GameObject($"Mode{modes[i]}");
                btnGO.transform.SetParent(panelGO.transform, false);
                var btnRT = btnGO.AddComponent<RectTransform>();
                btnRT.anchorMin = new Vector2(0.5f, 0.5f);
                btnRT.anchorMax = new Vector2(0.5f, 0.5f);
                btnRT.pivot = new Vector2(0.5f, 0.5f);
                btnRT.anchoredPosition = new Vector2(startX + i * 65f, y);
                btnRT.sizeDelta = new Vector2(60f, 30f);

                var img = btnGO.AddComponent<Image>();
                img.color = i == 0 ? new Color(0.3f, 0.5f, 0.8f) : new Color(0.25f, 0.25f, 0.25f);
                modeButtonImages[i] = img;

                var btn = btnGO.AddComponent<Button>();
                btn.onClick.AddListener(() => SelectGameMode(modeIdx));

                var txtGO = new GameObject("Text");
                txtGO.transform.SetParent(btnGO.transform, false);
                var trt = txtGO.AddComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = Vector2.zero;
                trt.offsetMax = Vector2.zero;
                var tmp = txtGO.AddComponent<TextMeshProUGUI>();
                tmp.text = modes[i];
                tmp.fontSize = 14;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
            }

            modeCountLabels = new TMP_Text[modes.Length];
            for (int i = 0; i < modes.Length; i++)
            {
                modeCountLabels[i] = MakeLabel(panelGO.transform, "", startX + i * 65f, y - 22f, 60f, 18f, 10,
                    FontStyles.Normal, TextAlignmentOptions.Center, true, new Color(0.6f, 0.6f, 0.6f));
            }
            y -= 22f;

            y -= 40f;
            CreateButton(panelGO.transform, "Find Match", 0f, y, 200f, 30f, () =>
            {
                matchmakingManager?.JoinQueue(selectedGameMode, (int)selectedCivilization);
            });

            y -= 40f;
            CreateButton(panelGO.transform, "Disconnect", 0f, y, 200f, 30f, () =>
            {
                matchmakingManager?.Disconnect();
            });
        }

        private void SelectGameMode(int idx)
        {
            selectedGameMode = (GameMode)idx;
            for (int i = 0; i < modeButtonImages.Length; i++)
                modeButtonImages[i].color = i == idx ? new Color(0.3f, 0.5f, 0.8f) : new Color(0.25f, 0.25f, 0.25f);
        }

        private void BuildInQueuePanel(Transform parent)
        {
            float panelW = 300f, panelH = 300f;
            var panelGO = CreateCenterPanel(parent, "QueuePanel", panelW, panelH);
            inQueuePanel = panelGO;

            float y = panelH / 2f;

            y -= 20f;
            MakeLabel(panelGO.transform, "Open Empires", 0f, y, panelW, 28f, 18, FontStyles.Bold, TextAlignmentOptions.Center, true);

            y -= 36f;
            queueSearchLabel = MakeLabel(panelGO.transform, "Searching...", 0f, y, panelW, 24f, 14, FontStyles.Normal, TextAlignmentOptions.Center, true);

            y -= 28f;
            queuePositionLabel = MakeLabel(panelGO.transform, "Queue position: ?", 0f, y, panelW, 24f, 14, FontStyles.Normal, TextAlignmentOptions.Center, true);

            y -= 22f;
            queueCountsLabel = MakeLabel(panelGO.transform, "", 0f, y, panelW, 20f, 11,
                FontStyles.Normal, TextAlignmentOptions.Center, true, new Color(0.6f, 0.6f, 0.6f));

            y -= 28f;
            queuePlayersLabel = MakeLabel(panelGO.transform, "", 0f, y - 30f, panelW - 20f, 80f, 13, FontStyles.Normal, TextAlignmentOptions.Center, true, new Color(0.8f, 0.8f, 0.8f));

            y -= 100f;
            CreateButton(panelGO.transform, "Cancel", 0f, y, 200f, 30f, () =>
            {
                matchmakingManager?.LeaveQueue();
            });
        }

        private void BuildMatchFoundPanel(Transform parent)
        {
            float panelW = 300f, panelH = 300f;
            var panelGO = CreateCenterPanel(parent, "MatchFoundPanel", panelW, panelH);
            matchFoundPanel = panelGO;

            float y = panelH / 2f;

            y -= 20f;
            MakeLabel(panelGO.transform, "Match Found!", 0f, y, panelW, 28f, 18, FontStyles.Bold, TextAlignmentOptions.Center, true);

            y -= 32f;
            matchIdLabel = MakeLabel(panelGO.transform, "Your ID: ?", 0f, y, panelW, 24f, 14, FontStyles.Normal, TextAlignmentOptions.Center, true);

            y -= 28f;
            MakeLabel(panelGO.transform, "Waiting for all players to ready...", 0f, y, panelW, 24f, 12, FontStyles.Normal, TextAlignmentOptions.Center, true, new Color(0.7f, 0.7f, 0.7f));

            y -= 30f;
            matchRosterLabel = MakeLabel(panelGO.transform, "", 0f, y - 60f, panelW - 20f, 140f, 13, FontStyles.Normal, TextAlignmentOptions.TopLeft, true);
        }

        private void BuildMatchStartingPanel(Transform parent)
        {
            float panelW = 300f, panelH = 160f;
            var panelGO = CreateCenterPanel(parent, "MatchStartingPanel", panelW, panelH);
            matchStartingPanel = panelGO;

            float y = panelH / 2f;

            y -= 30f;
            MakeLabel(panelGO.transform, "Match Starting!", 0f, y, panelW, 28f, 20, FontStyles.Bold, TextAlignmentOptions.Center, true);

            y -= 36f;
            MakeLabel(panelGO.transform, "Loading game...", 0f, y, panelW, 24f, 14, FontStyles.Normal, TextAlignmentOptions.Center, true);
        }

        // ---- UI Helpers ----

        private GameObject CreateCenterPanel(Transform parent, string name, float w, float h)
        {
            var panelGO = new GameObject(name);
            panelGO.transform.SetParent(parent, false);
            var panelRT = panelGO.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(w, h);
            var panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.12f, 0.14f, 0.96f);
            return panelGO;
        }

        private TMP_Text MakeLabel(Transform parent, string text, float x, float y, float w, float h,
            float fontSize, FontStyles style, TextAlignmentOptions alignment, bool centerPivot = false, Color? color = null)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = centerPivot ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color ?? Color.white;
            tmp.alignment = alignment;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            return tmp;
        }

        private void CreateButton(Transform parent, string label, float x, float y, float w, float h, System.Action onClick)
        {
            var btnGO = new GameObject("Button");
            btnGO.transform.SetParent(parent, false);
            var btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0.5f);
            btnRT.anchorMax = new Vector2(0.5f, 0.5f);
            btnRT.pivot = new Vector2(0.5f, 0.5f);
            btnRT.anchoredPosition = new Vector2(x, y);
            btnRT.sizeDelta = new Vector2(w, h);

            var img = btnGO.AddComponent<Image>();
            img.color = Color.white;

            var outline = btnGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.4f, 0.4f, 0.45f, 0.5f);
            outline.effectDistance = new Vector2(1, -1);

            var btn = btnGO.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.28f, 0.28f, 0.32f);
            colors.highlightedColor = new Color(0.38f, 0.38f, 0.42f);
            colors.pressedColor = new Color(0.20f, 0.20f, 0.23f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(btnGO.transform, false);
            var trt = textGO.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 16;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
        }

        private TMP_InputField CreateInputField(Transform parent, string defaultText, float x, float y, float w, float h)
        {
            var fieldGO = new GameObject("InputField");
            fieldGO.transform.SetParent(parent, false);
            var fieldRT = fieldGO.AddComponent<RectTransform>();
            fieldRT.anchorMin = new Vector2(0.5f, 0.5f);
            fieldRT.anchorMax = new Vector2(0.5f, 0.5f);
            fieldRT.pivot = new Vector2(0.5f, 0.5f);
            fieldRT.anchoredPosition = new Vector2(x, y);
            fieldRT.sizeDelta = new Vector2(w, h);

            var bgImg = fieldGO.AddComponent<Image>();
            bgImg.color = new Color(0.13f, 0.13f, 0.16f);

            var outline = fieldGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.35f, 0.4f, 0.6f);
            outline.effectDistance = new Vector2(1, -1);

            // Text area
            var textAreaGO = new GameObject("TextArea");
            textAreaGO.transform.SetParent(fieldGO.transform, false);
            var textAreaRT = textAreaGO.AddComponent<RectTransform>();
            textAreaRT.anchorMin = Vector2.zero;
            textAreaRT.anchorMax = Vector2.one;
            textAreaRT.offsetMin = new Vector2(10f, 0f);
            textAreaRT.offsetMax = new Vector2(-10f, 0f);
            textAreaGO.AddComponent<RectMask2D>();

            // Placeholder
            var placeholderGO = new GameObject("Placeholder");
            placeholderGO.transform.SetParent(textAreaGO.transform, false);
            var phRT = placeholderGO.AddComponent<RectTransform>();
            phRT.anchorMin = Vector2.zero;
            phRT.anchorMax = Vector2.one;
            phRT.offsetMin = Vector2.zero;
            phRT.offsetMax = Vector2.zero;
            var phText = placeholderGO.AddComponent<TextMeshProUGUI>();
            phText.text = "Enter name...";
            phText.fontSize = 16;
            phText.alignment = TextAlignmentOptions.Center;
            phText.color = new Color(0.45f, 0.45f, 0.5f);
            phText.fontStyle = FontStyles.Italic;

            // Text
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(textAreaGO.transform, false);
            var trt = textGO.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 16;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            var inputField = fieldGO.AddComponent<TMP_InputField>();
            inputField.textViewport = textAreaRT;
            inputField.textComponent = tmp;
            inputField.placeholder = phText;
            inputField.text = defaultText;
            inputField.characterLimit = 20;

            return inputField;
        }

        // Dashboard API response classes
        [Serializable] private class DashboardResponse { public ServerLoadData server_load; public QueueStatusData[] queues; }
        [Serializable] private class ServerLoadData { public int active_connections; }
        [Serializable] private class QueueStatusData { public string game_mode; public int player_count; }

        private IEnumerator PollDashboard()
        {
            while (true)
            {
                string url;
                if (useLocalServer)
                    url = localServerUrl + "/api/dashboard/status";
                else
                    url = regions[selectedRegionIndex].HttpUrl + "/api/dashboard/status";

                var req = UnityWebRequest.Get(url);
                req.timeout = 10;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var resp = JsonUtility.FromJson<DashboardResponse>(req.downloadHandler.text);
                        if (resp.server_load != null)
                            cachedOnlineCount = resp.server_load.active_connections;
                        if (resp.queues != null)
                        {
                            for (int i = 0; i < cachedQueueCounts.Length; i++)
                                cachedQueueCounts[i] = 0;
                            foreach (var q in resp.queues)
                            {
                                int idx = q.game_mode switch
                                {
                                    "1v1" => 0, "2v2" => 1, "3v3" => 2, "4v4" => 3, _ => -1
                                };
                                if (idx >= 0) cachedQueueCounts[idx] = q.player_count;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[Network] Dashboard parse error: {e.Message}");
                    }
                }

                req.Dispose();
                yield return new WaitForSeconds(15f);
            }
        }
    }
}
