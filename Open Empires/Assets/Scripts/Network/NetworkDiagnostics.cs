using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public class NetworkDiagnostics : MonoBehaviour
    {
        public static NetworkDiagnostics Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance == null)
            {
                var go = new GameObject("NetworkDiagnostics");
                go.AddComponent<NetworkDiagnostics>();
                go.AddComponent<NetworkDiagnosticsUI>();
                DontDestroyOnLoad(go);
            }
        }

        // Ping (placeholder for future ping/pong implementation)
        public float CurrentPing { get; private set; }
        public float AveragePing { get; private set; }
        public float MinPing { get; private set; } = float.MaxValue;
        public float MaxPing { get; private set; }
        private Queue<float> pingHistory = new Queue<float>();
        private const int PingHistorySize = 30;

        // Tick rate
        public float ActualTickRate { get; private set; }
        public int ExpectedTickRate => 30;
        private int ticksThisSecond;
        private float tickRateTimer;

        // Accumulator
        public float Accumulator { get; set; }
        public float MaxAccumulator { get; private set; }
        public int AccumulatorOverflows { get; private set; }
        public int AccumulatorOverflowsThisSecond { get; private set; }
        private int overflowsThisSecondCounter;

        // Network wait
        public float WaitTimeThisSecond { get; private set; }
        public int StallsThisSecond { get; private set; }
        private float waitStartTime;
        private bool isWaiting;
        private float waitTimeAccumulator;
        private int stallsAccumulator;

        // Commands
        public int CommandsSentThisTick { get; set; }
        public int CommandsReceivedThisTick { get; set; }
        public int CommandsSentThisSecond { get; private set; }
        public int CommandsReceivedThisSecond { get; private set; }
        public int LatePacketsDropped { get; set; }
        public int NoopPacketsThisSecond { get; private set; }
        public int Timeouts { get; private set; }
        public int DesyncWarnings { get; private set; }
        private int commandsSentAccumulator;
        private int commandsReceivedAccumulator;
        private int noopAccumulator;

        // Frame
        public float FrameTime => Time.unscaledDeltaTime;
        public float FPS => Time.unscaledDeltaTime > 0 ? 1f / Time.unscaledDeltaTime : 0f;

        // Per-second stats timer
        private float secondTimer;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            // Update max accumulator
            if (Accumulator > MaxAccumulator)
            {
                MaxAccumulator = Accumulator;
            }

            // Update wait time if currently waiting
            if (isWaiting)
            {
                waitTimeAccumulator += Time.unscaledDeltaTime;
            }

            // Per-second stats
            secondTimer += Time.unscaledDeltaTime;
            if (secondTimer >= 1f)
            {
                // Tick rate
                ActualTickRate = ticksThisSecond;
                ticksThisSecond = 0;

                // Wait stats
                WaitTimeThisSecond = waitTimeAccumulator;
                StallsThisSecond = stallsAccumulator;
                waitTimeAccumulator = 0f;
                stallsAccumulator = 0;

                // Command stats
                CommandsSentThisSecond = commandsSentAccumulator;
                CommandsReceivedThisSecond = commandsReceivedAccumulator;
                NoopPacketsThisSecond = noopAccumulator;
                commandsSentAccumulator = 0;
                commandsReceivedAccumulator = 0;
                noopAccumulator = 0;

                // Overflow stats
                AccumulatorOverflowsThisSecond = overflowsThisSecondCounter;
                overflowsThisSecondCounter = 0;

                secondTimer -= 1f;
            }
        }

        public void RecordTick()
        {
            ticksThisSecond++;
        }

        public void RecordOverflow()
        {
            AccumulatorOverflows++;
            overflowsThisSecondCounter++;
        }

        public void StartWait()
        {
            if (!isWaiting)
            {
                isWaiting = true;
                waitStartTime = Time.unscaledTime;
                stallsAccumulator++;
            }
        }

        public void EndWait()
        {
            if (isWaiting)
            {
                isWaiting = false;
            }
        }

        public void RecordCommandSent(bool isNoop = false)
        {
            commandsSentAccumulator++;
            if (isNoop)
            {
                noopAccumulator++;
            }
        }

        public void RecordCommandReceived()
        {
            commandsReceivedAccumulator++;
        }

        public void RecordLatePacket()
        {
            LatePacketsDropped++;
        }

        public void RecordTimeout()
        {
            Timeouts++;
        }

        public void RecordDesyncWarning()
        {
            DesyncWarnings++;
        }

        public void RecordPing(float pingMs)
        {
            CurrentPing = pingMs;

            pingHistory.Enqueue(pingMs);
            if (pingHistory.Count > PingHistorySize)
            {
                pingHistory.Dequeue();
            }

            // Calculate average
            float sum = 0f;
            foreach (var p in pingHistory)
            {
                sum += p;
            }
            AveragePing = sum / pingHistory.Count;

            // Track min/max
            if (pingMs < MinPing) MinPing = pingMs;
            if (pingMs > MaxPing) MaxPing = pingMs;
        }

        public void ResetStats()
        {
            MaxAccumulator = 0f;
            AccumulatorOverflows = 0;
            LatePacketsDropped = 0;
            Timeouts = 0;
            DesyncWarnings = 0;
            MinPing = float.MaxValue;
            MaxPing = 0f;
            pingHistory.Clear();
        }
    }
}
