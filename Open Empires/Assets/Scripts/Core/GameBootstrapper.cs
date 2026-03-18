using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public class GameBootstrapper : MonoBehaviour
    {
        [SerializeField] private SimulationConfig config;
        [SerializeField] private NetworkManager networkManager;
        private int playerCount = 1;
        private int[] aiPlayerIds;
        private AIDifficulty aiDifficulty = AIDifficulty.Medium;
        private Civilization[] civilizations;

        public static GameBootstrapper Instance { get; private set; }
        public SimulationConfig Config => config;
        public GameSimulation Simulation { get; private set; }
        public float InterpolationAlpha { get; private set; }
        public NetworkManager Network => networkManager;
        public int PlayerCount => playerCount;

        // Input delay: send commands for N ticks in the future
        // This gives commands time to arrive before they're needed
        // Dynamically adjusted based on measured RTT (minimum 3 ticks = ~100ms)
        public int InputDelayTicks { get; private set; } = 3;

        // Timeout for waiting on commands from other players
        // If we don't receive commands within this time, consider the player disconnected
        private const float CommandTimeoutSeconds = 2.0f;

        private float tickAccumulator;
        private List<ICommand> localCommandsThisTick = new List<ICommand>();
        private readonly List<ICommand> tickCommandsBuffer = new List<ICommand>();
        private static readonly List<ICommand> emptyCommandList = new List<ICommand>();
        private int sentCommandsForTick = -1;

        private bool teamsApplied = false;
        private bool desyncLogged = false;

        // Timeout tracking
        private int waitingForTick = -1;
        private float waitStartTime;

        // Relay mode: track which ticks we've sent our commands for
        // In relay mode, we receive all commands (including our own) back from the server
        private HashSet<int> pendingTicks = new HashSet<int>();

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // Simulation created lazily in Update() after playerCount is finalized
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            var mm = MatchmakingManager.Instance;
            if (mm != null)
                mm.OnPlayerDisconnected -= OnPlayerDisconnectedFromServer;

            // Clean up single player analytics subscription
            if (Simulation != null)
            {
                Simulation.OnMatchEnded -= HandleSinglePlayerMatchEnded;
            }
        }

        public void SetPlayerCount(int count)
        {
            playerCount = count;
        }

        public void SetAIPlayerIds(int[] ids)
        {
            aiPlayerIds = ids;
        }

        public void SetAIDifficulty(AIDifficulty difficulty)
        {
            aiDifficulty = difficulty;
        }

        public void SetCivilizations(Civilization[] civs)
        {
            civilizations = civs;
        }

        private int[] localTeamAssignments;

        public void SetTeamAssignments(int[] assignments)
        {
            localTeamAssignments = assignments;
        }

        private void Update()
        {
            if (config == null) return;

            // Wait for game to start (multiplayer menu or single player)
            if (networkManager != null && !networkManager.GameStarted) return;

            // Create simulation on first frame after GameStarted (playerCount is finalized)
            if (Simulation == null)
            {
                int[] teams = null;
                if (networkManager != null && networkManager.IsMultiplayer)
                    teams = networkManager.TeamAssignments;
                else if (localTeamAssignments != null)
                    teams = localTeamAssignments;
                Simulation = new GameSimulation(config, playerCount, teams, aiPlayerIds, aiDifficulty);
                if (civilizations != null)
                    Simulation.SetPlayerCivilizations(civilizations);

                // Force immediate game setup so all entities exist before first tick
                var gameSetup = Object.FindFirstObjectByType<GameSetup>();
                if (gameSetup != null)
                    gameSetup.InitializeGame();

                Debug.LogWarning($"[SyncCheck] MapSeed={config.MapSeed} Players={playerCount} " +
                    $"Teams=[{string.Join(",", teams ?? new int[0])}] " +
                    $"AI=[{string.Join(",", aiPlayerIds ?? new int[0])}]");
                uint initHash = Simulation.ComputeStateChecksum();
                Debug.LogWarning($"[SyncCheck] Tick0 Checksum={initHash:X8}");
                Simulation.LogStateBreakdown();

                // Start single player analytics session if not multiplayer
                if (networkManager == null || !networkManager.IsMultiplayer)
                {
                    EnsureSinglePlayerAnalytics();
                    SinglePlayerAnalytics.Instance?.StartSession();
                    Simulation.OnMatchEnded += HandleSinglePlayerMatchEnded;
                }

#if UNITY_WEBGL && !UNITY_EDITOR
                // If we're already fullscreen (e.g. multiplayer queue), engage pointer lock now
                if (FullscreenManager.Instance != null && FullscreenManager.Instance.IsFullscreen)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
#endif
            }

            if (!teamsApplied && networkManager != null && networkManager.IsMultiplayer
                && networkManager.TeamAssignments != null)
            {
                Simulation.SetTeamAssignments(networkManager.TeamAssignments);
                teamsApplied = true;

                // Hook player disconnect events for graceful surrender
                var mm = MatchmakingManager.Instance;
                if (mm != null)
                    mm.OnPlayerDisconnected += OnPlayerDisconnectedFromServer;
            }

            if (Simulation.IsMatchOver)
            {
                tickAccumulator = 0f;
                return;
            }

            tickAccumulator += Time.deltaTime;
            float tickInterval = config.SecondsPerTick;

            // Raise the cap when we have queued commands to process (catch-up after tab-back)
            float maxAccumulator;
            int bufferedTicks = networkManager != null && networkManager.IsMultiplayer
                ? networkManager.BufferedTickCount(Simulation?.CurrentTick ?? 0)
                : 0;
            if (bufferedTicks > 5)
                maxAccumulator = tickInterval * 30f;  // 30x speed catch-up
            else
                maxAccumulator = tickInterval * 3f;   // normal cap

            if (tickAccumulator > maxAccumulator)
            {
                NetworkDiagnostics.Instance?.RecordOverflow();
                tickAccumulator = maxAccumulator;
            }

            // Update diagnostics with current accumulator value
            if (NetworkDiagnostics.Instance != null)
            {
                NetworkDiagnostics.Instance.Accumulator = tickAccumulator;
            }

            while (tickAccumulator >= tickInterval)
            {
                if (networkManager != null && networkManager.IsMultiplayer)
                {
                    // In relay mode, commands go through the server and come back
                    // We use a lockstep approach: send our commands, wait for all commands from server
                    if (!ProcessMultiplayerTick())
                    {
                        NetworkDiagnostics.Instance?.StartWait();
                        break; // Don't consume time slot, exit loop and wait for commands
                    }
                    else
                    {
                        NetworkDiagnostics.Instance?.EndWait();
                        NetworkDiagnostics.Instance?.RecordTick();
                    }
                }
                else
                {
                    Simulation.Tick();
                    NetworkDiagnostics.Instance?.RecordTick();
                }
                tickAccumulator -= tickInterval;
            }

            // Hold interpolation steady during stalls to prevent visual snapping
            if (tickAccumulator >= tickInterval)
                InterpolationAlpha = 1f;
            else
                InterpolationAlpha = Mathf.Clamp01(tickAccumulator / tickInterval);

            // Check for desync
            if (networkManager != null && networkManager.DesyncDetected && !desyncLogged)
            {
                desyncLogged = true;
                Debug.LogError($"[Desync] DESYNC DETECTED at tick {networkManager.DesyncTick}! Game states have diverged.");
                Simulation.LogStateBreakdown();
            }

        }

        private bool ProcessMultiplayerTick()
        {
            int currentTick = Simulation.CurrentTick;

            // Check if network manager wants a higher input delay (RTT increased)
            int newDelay = networkManager.CurrentInputDelay;
            if (newDelay > InputDelayTicks)
            {
                int oldDelay = InputDelayTicks;
                InputDelayTicks = newDelay;

                // Fill gap: send Noop for any ticks between last sent tick and new horizon
                // so no tick is left without our commands
                int newHorizon = currentTick + newDelay;
                int gapStart = sentCommandsForTick + 1;
                for (int gapTick = gapStart; gapTick <= newHorizon; gapTick++)
                {
                    networkManager.SendCommands(emptyCommandList, gapTick, isGapFill: true);
                }

                // Update sentCommandsForTick to reflect the gap fill
                if (newHorizon > sentCommandsForTick)
                    sentCommandsForTick = newHorizon;

                Debug.Log($"[GameBootstrapper] Input delay increased: {oldDelay} -> {newDelay}, filled gap ticks {gapStart} to {newHorizon}");
            }
            else if (newDelay < InputDelayTicks)
            {
                InputDelayTicks = newDelay;
            }

            // With input delay, we send commands for a FUTURE tick
            // This gives commands time to travel through the network before they're needed
            int commandTick = currentTick + InputDelayTicks;

            // Flush and send local commands for the future tick
            if (sentCommandsForTick < commandTick)
            {
                localCommandsThisTick.Clear();
                localCommandsThisTick.AddRange(Simulation.CommandBuffer.FlushCommands());

                // Stamp player ID onto commands
                int localPlayerId = networkManager.LocalPlayerId;
                for (int i = 0; i < localCommandsThisTick.Count; i++)
                {
                    var cmd = localCommandsThisTick[i];
                    SetCommandPlayerId(ref cmd, localPlayerId);
                    localCommandsThisTick[i] = cmd;
                }

                networkManager.SendCommands(localCommandsThisTick, commandTick);
                sentCommandsForTick = commandTick;
                pendingTicks.Add(commandTick);
            }

            // Wait for commands for the CURRENT tick (these were sent InputDelayTicks earlier)
            // During startup, we need pre-seeded commands from NetworkManager.PreSeedCommands()
            if (!networkManager.HasCommandsFromAllPlayersForTick(currentTick))
            {
                // Track when we started waiting for this tick
                if (waitingForTick != currentTick)
                {
                    waitingForTick = currentTick;
                    waitStartTime = Time.unscaledTime;
                }
                else
                {
                    // Check for timeout
                    float waitDuration = Time.unscaledTime - waitStartTime;
                    if (waitDuration > CommandTimeoutSeconds)
                    {
                        Debug.LogError($"[Network] Timeout waiting for tick {currentTick} commands. Wait time: {waitDuration:F2}s");
                        NetworkDiagnostics.Instance?.RecordTimeout();

                        // Force disconnect - the match is broken
                        networkManager.HandlePlayerTimeout(currentTick);
                        return false;
                    }
                }
                return false; // Tick not ready, don't consume time slot
            }

            // Reset timeout tracking when we successfully get commands
            waitingForTick = -1;

            // Build deterministic command list from all players
            tickCommandsBuffer.Clear();

            // In relay mode, server sends us all commands including our own
            // ConsumeCommandsForTick returns commands already sorted by PlayerId
            var serverCommands = networkManager.ConsumeCommandsForTick(currentTick);
            if (serverCommands != null)
            {
                tickCommandsBuffer.AddRange(serverCommands);
            }

            pendingTicks.Remove(currentTick);
            Simulation.Tick(tickCommandsBuffer);
            return true; // Tick executed successfully
        }

        private void OnPlayerDisconnectedFromServer(string serverPlayerId)
        {
            var mm = MatchmakingManager.Instance;
            if (mm == null) return;
            int gamePlayerId = mm.ResolveGamePlayerId(serverPlayerId);
            if (gamePlayerId < 0) return;

            networkManager.MarkPlayerDisconnected(gamePlayerId);
            Simulation?.MarkPlayerDisconnected(gamePlayerId);

            string name = mm.ResolvePlayerName(gamePlayerId);
            ChatManager.AddSystemMessage($"{name} has disconnected and forfeited.");
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && networkManager != null && networkManager.IsMultiplayer)
            {
                tickAccumulator = 0f;
                networkManager.ResetAfterFocusReturn();
            }
        }

        private void SetCommandPlayerId(ref ICommand cmd, int playerId)
        {
            switch (cmd)
            {
                case MoveCommand move:
                    move.PlayerId = playerId;
                    cmd = move;
                    break;
                case GatherCommand gather:
                    gather.PlayerId = playerId;
                    cmd = gather;
                    break;
                case StopCommand stop:
                    stop.PlayerId = playerId;
                    cmd = stop;
                    break;
                case AttackBuildingCommand attack:
                    attack.PlayerId = playerId;
                    cmd = attack;
                    break;
                case TrainUnitCommand train:
                    train.PlayerId = playerId;
                    cmd = train;
                    break;
                case SetRallyPointCommand rally:
                    rally.PlayerId = playerId;
                    cmd = rally;
                    break;
                case PlaceBuildingCommand place:
                    place.PlayerId = playerId;
                    cmd = place;
                    break;
                case ConstructBuildingCommand construct:
                    construct.PlayerId = playerId;
                    cmd = construct;
                    break;
                case DropOffCommand dropOff:
                    dropOff.PlayerId = playerId;
                    cmd = dropOff;
                    break;
                case CheatResourceCommand cheatRes:
                    cheatRes.PlayerId = playerId;
                    cmd = cheatRes;
                    break;
                case CheatProductionCommand cheatProd:
                    cheatProd.PlayerId = playerId;
                    cmd = cheatProd;
                    break;
                case CheatVisionCommand cheatVis:
                    cheatVis.PlayerId = playerId;
                    cmd = cheatVis;
                    break;
                case AttackUnitCommand attackUnit:
                    attackUnit.PlayerId = playerId;
                    cmd = attackUnit;
                    break;
                case PlaceWallCommand placeWall:
                    placeWall.PlayerId = playerId;
                    cmd = placeWall;
                    break;
                case ConvertToGateCommand convertGate:
                    convertGate.PlayerId = playerId;
                    cmd = convertGate;
                    break;
                case CancelTrainCommand cancelTrain:
                    cancelTrain.PlayerId = playerId;
                    cmd = cancelTrain;
                    break;
                case UpgradeTowerCommand upgradeTower:
                    upgradeTower.PlayerId = playerId;
                    cmd = upgradeTower;
                    break;
                case CancelUpgradeCommand cancelUpgrade:
                    cancelUpgrade.PlayerId = playerId;
                    cmd = cancelUpgrade;
                    break;
                case GarrisonCommand garrison:
                    garrison.PlayerId = playerId;
                    cmd = garrison;
                    break;
                case UngarrisonCommand ungarrison:
                    ungarrison.PlayerId = playerId;
                    cmd = ungarrison;
                    break;
                case PatrolCommand patrol:
                    patrol.PlayerId = playerId;
                    cmd = patrol;
                    break;
                case SurrenderVoteCommand surrender:
                    surrender.PlayerId = playerId;
                    cmd = surrender;
                    break;
                default:
                    Debug.LogWarning($"[GameBootstrapper] SetCommandPlayerId: unhandled command type {cmd.GetType().Name}");
                    break;
            }
        }

        private void EnsureSinglePlayerAnalytics()
        {
            if (SinglePlayerAnalytics.Instance == null)
            {
                var go = new GameObject("SinglePlayerAnalytics");
                go.AddComponent<SinglePlayerAnalytics>();
            }
        }

        private void HandleSinglePlayerMatchEnded(int winningTeamId)
        {
            // In single player, player 0 is always the human player (team 0)
            // AI players are on team 1
            string result = winningTeamId == 0 ? "victory" : "defeat";
            SinglePlayerAnalytics.Instance?.EndSession(result);
        }

        private void OnApplicationQuit()
        {
            // Report abandoned session if single player match is in progress
            if (Simulation != null && !Simulation.IsMatchOver)
            {
                if (networkManager == null || !networkManager.IsMultiplayer)
                {
                    SinglePlayerAnalytics.Instance?.EndSession("abandoned");
                }
            }
        }

    }
}
