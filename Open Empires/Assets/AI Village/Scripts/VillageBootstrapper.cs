using UnityEngine;

namespace OpenEmpires.Village
{
    /// <summary>
    /// Scene-level entry point for the AI Village mode. Assign this component to
    /// GameSetup's "Setup Extension" field. It configures a 1-player, no-network game,
    /// plugs the routine system into the simulation, and hands village generation to
    /// <see cref="VillageGenerator"/>.
    /// </summary>
    public class VillageBootstrapper : MonoBehaviour, IGameSetupExtension
    {
        [Header("Time")]
        [Tooltip("Real seconds for one in-game day.")]
        [SerializeField] private float dayLengthSeconds = 300f;
        [Tooltip("Hour of day the simulation starts at.")]
        [SerializeField, Range(0, 23)] private int startHour = 6;

        [Header("Village")]
        [SerializeField] private int houseCount = 15;
        [SerializeField] private int villagerTarget = 40;
        [SerializeField] private int farmCount = 6;
        [Tooltip("0 = keep the seed stored in SimulationConfig.")]
        [SerializeField] private int mapSeed = 0;

        [Header("Pace (village life is slower than an RTS match)")]
        [Tooltip("Villager walk speed as a fraction of the RTS speed.")]
        [SerializeField, Range(0.25f, 1f)] private float walkSpeedMultiplier = 0.7f;
        [Tooltip("Resources a villager carries per trip (RTS default is 10).")]
        [SerializeField] private int carryCapacity = 5;
        // Gathering strike cooldown is set by the scene's SimulationConfig asset (gatherCooldownPercent).

        [Header("Economy")]
        [Tooltip("Coins each villager starts with.")]
        [SerializeField] private int startingMoney = 8;
        [Tooltip("Price of one meal at the tavern.")]
        [SerializeField] private int mealPrice = 4;
        [Tooltip("Consecutive missed meals before a villager starts losing health.")]
        [SerializeField] private int starvationThreshold = 3;

        [Header("Life cycle (days)")]
        [SerializeField] private int childDays = 5;
        [SerializeField] private int adultDays = 10;
        [SerializeField] private int elderDays = 5;

        [Header("Mode")]
        [Tooltip("Player cannot issue commands; villagers are fully autonomous.")]
        [SerializeField] private bool observeOnly = true;
        [Tooltip("Reveal the whole map instead of using fog of war.")]
        [SerializeField] private bool revealMap = false;

        public static VillageBootstrapper Instance { get; private set; }
        public VillageRoutineSystem Routine { get; private set; }
        public bool ObserveOnly => observeOnly;

        private VillageGenerator generator;

        private void Awake()
        {
            Instance = this;
            UnitView.DisableIdleEffect = true; // the RTS "zzz" idle marker is meaningless here (thought bubbles replace it)
        }

        private void Start()
        {
            var gb = GameBootstrapper.Instance;
            if (gb != null)
            {
                gb.SetPlayerCount(1);
                gb.SetAIPlayerIds(null);
                gb.SetTeamAssignments(null);
                if (mapSeed != 0 && gb.Config != null)
                    gb.Config.MapSeed = mapSeed;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void OnBeforeMapRender(GameSetup setup, GameSimulation sim, Vector2Int[] basePositions)
        {
            int tickRate = sim.Config.TickRate;
            VillageClock.Configure(Mathf.RoundToInt(dayLengthSeconds * tickRate), startHour * 60);

            Routine = new VillageRoutineSystem
            {
                MealPrice = Mathf.Max(0, mealPrice),
                StarvationThreshold = Mathf.Max(1, starvationThreshold),
                ChildDays = Mathf.Max(1, childDays),
                AdultDays = Mathf.Max(1, adultDays),
                ElderDays = Mathf.Max(1, elderDays),
            };
            Routine.Seed(sim.Config.MapSeed);
            sim.Extension = Routine;

            // The RTS gates buildings by age (towers, stables, archery ranges need age 2+). The village
            // has no age-up mechanic, so put player 0 at the final age so the council can build anything.
            var agesField = typeof(GameSimulation).GetField("playerAges", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (agesField != null && agesField.GetValue(sim) is int[] ages && ages.Length > 0) ages[0] = 3;
            sim.CommandBuffer.BlockEnqueue = observeOnly;

            if (revealMap)
            {
                sim.FogOfWar.SetVisionCheat(0, true);
                FogOfWarRenderer.DisableFogOfWar = true;
            }

            generator = new VillageGenerator(sim.Config.MapSeed)
            {
                HouseCount = houseCount,
                VillagerTarget = villagerTarget,
                FarmCount = farmCount,
                WalkSpeedMultiplier = walkSpeedMultiplier,
                CarryCapacity = carryCapacity,
                StartingMoney = Mathf.Max(0, startingMoney),
            };
            if (basePositions != null && basePositions.Length > 0)
                generator.PrepareTerrain(sim, basePositions[0]);
        }

        public bool SpawnPlayerBase(GameSetup setup, GameSimulation sim, int playerId, int tileX, int tileZ)
        {
            if (playerId != 0 || generator == null) return false;
            generator.Build(setup, sim, playerId, tileX, tileZ, Routine);
            return true;
        }
    }
}
