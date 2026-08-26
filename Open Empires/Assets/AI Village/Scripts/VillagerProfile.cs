namespace OpenEmpires.Village
{
    public enum RoutinePhase
    {
        Sleeping,   // garrisoned at home for the night
        Morning,    // awake, at home before work
        Working,    // at the job (gathering / indoors / patrolling)
        Lunch,      // back home for a midday break
        Evening     // leisure at the plaza or resting at home before bed
    }

    public enum MilitaryKind { None, Militia, Soldier, Archer, Knight }

    public enum LifeStage
    {
        Child,
        Adult,
        Elder
    }

    public enum Meal
    {
        None = -1,
        Breakfast = 0,
        Lunch = 1,
        Dinner = 2,
        Snack = 3
    }

    /// <summary>Need-driven side trips that interrupt the routine until the need is met.</summary>
    public enum Errand
    {
        None,
        Nap,      // exhausted → sleep at home until rested
        Social,   // lonely → find company at the square
        Fun,      // bored → play at the square / drink at the tavern
        Eating,   // sitting inside the tavern with a bought meal
        Quirk,    // an eccentric villager wandering off doing something odd
        Gawk,     // stopped to stare at an eccentric
        Fight,    // brawling
        Watch,    // standing around watching a brawl
        Haul,     // carrying timber from the lumber yard to a construction site
        Build,    // working on a construction site
        Bury,     // carrying a body to the graveyard
        Flee,     // running home from a threat
        Defend,   // fighting wolves / raiders through the real combat system
        Arm,      // collecting a weapon at the blacksmith
        Mount,    // taking a horse from the stables
        Dismount, // returning the horse
        Tame,     // taming a wild horse
        Lead      // leading a tamed horse to the stables
    }

    /// <summary>Per-villager life-sim data. Owned by the routine system; sim-side only.</summary>
    public class VillagerProfile
    {
        public int UnitId;
        public string FirstName;
        public string FamilyName;
        public int HouseholdIndex;

        public VillageJob Job;
        public int HomeBuildingId = -1;
        public int WorkplaceBuildingId = -1;
        /// <summary>Fixed resource node for jobs that own one (farmers). -1 = pick nearest each time.</summary>
        public int WorkNodeId = -1;
        /// <summary>Second post for patrol jobs.</summary>
        public int PatrolBuildingId = -1;
        /// <summary>Used to spread gatherers across nearby nodes deterministically.</summary>
        public int GatherSlot;

        // Daily schedule, in minutes of day
        public int WakeMinute;
        public int WorkStartMinute;
        public bool TakesLunch;
        public int LunchStartMinute;
        public int LunchEndMinute;
        public int WorkEndMinute;
        public int SleepMinute;
        public bool EveningLeisure;

        // Runtime
        public RoutinePhase Phase = RoutinePhase.Morning;
        public int LastCommandTick = -1000;
        public string Activity = "";

        // Economy: work earns coins, meals cost coins, missing meals starves.
        public int Money;
        public int MissedMeals;           // consecutive meals missed
        public int MealsEatenMask;        // bit per Meal eaten today
        public int MealsHandledMask;      // bit per Meal already decided today (eaten or skipped)
        public int LastMealDay;
        public Meal PendingMeal = Meal.None;
        public int MealStartedTick;
        public bool PhaseBeginPending;    // a phase change happened while out eating; resume afterwards
        public int LastDepositTickSeen;
        public int WageTicks;             // ticks worked since the last hourly wage
        public bool IsStarving;
        public bool IsDead;

        public bool HasEaten(Meal m) => (MealsEatenMask & (1 << (int)m)) != 0;
        public bool HasHandled(Meal m) => (MealsHandledMask & (1 << (int)m)) != 0;

        // Needs, 0..VillageRoutineSystem.NeedMax (millionths). Full = satisfied.
        public int Hunger = 1_000_000;
        public int Energy = 1_000_000;
        public int Social = 1_000_000;
        public int Fun = 1_000_000;
        public Errand Errand = Errand.None;
        public int ErrandStartTick;

        // Identity & traits
        public Gender Gender;
        public readonly System.Collections.Generic.List<Trait> Traits = new System.Collections.Generic.List<Trait>();
        public readonly System.Collections.Generic.Dictionary<Trait, int> TraitExpiry = new System.Collections.Generic.Dictionary<Trait, int>();
        public Fixed32 BaseMoveSpeed;      // pace before trait multipliers
        public int NextGawkTick;
        public int GawkTargetId = -1;
        public int GawkCount;
        public int RejectedCount;
        public int LastMealTick = -100000;

        // Fights
        public int FightId = -1;
        public int FightsFought;
        public int LastArgumentTick = -100000;

        // Memories: notable things that happened between this villager and another.
        public struct Memory { public int Tick; public int OtherId; public int Delta; public string Text; }
        public readonly System.Collections.Generic.List<Memory> Memories = new System.Collections.Generic.List<Memory>();
        public int LastGossipTick = -100000;

        // Relationships & comfort
        public int ChatPartnerId = -1;    // who they're talking to right now (-1 = nobody)
        public bool WaitingForTable;      // tavern full
        public bool Crowded;              // too many people in the house

        // Combat & cavalry
        public MilitaryKind Military = MilitaryKind.None; // trained at barracks / archery range / stables
        public bool Armed;                // has a weapon (permanent)
        public bool Mounted;              // riding a horse from the stables
        public int DefendStage;           // 0 = arm, 1 = mount, 2 = fight
        public FixedVector3 LastKnownPos; // where they were last seen alive (for the body)
        public int HorseTargetId = -1;
        public int TameProgress;

        // Construction, burials, wolves
        public bool IsBuilder;            // assigned to the active construction project
        public bool CarryingLoad;         // hauling: currently holding timber
        public int BuryCorpseId = -1;
        public int WolfDecisionTick = -1; // when we last decided flee/fight
        public bool WillDefend;
        public int LastHitTick = -1000;

        public bool Has(Trait t) => Traits.Contains(t);

        // Stuck-movement watchdog
        public int StuckTileX = int.MinValue, StuckTileZ;
        public int StuckTicks;
        public int LastDetourTick = -1000;

        // Personality: a few villagers are eccentric — they wander off their routine and move erratically.
        public bool Quirky;
        public int NextQuirkTick;
        public int QuirkEndTick;
        public int QuirkStepTick;

        // Life cycle: child (small) → adult → elder (crooked) → death of old age.
        public int BirthTick;             // may be negative for villagers who start the sim already grown
        public LifeStage Stage = LifeStage.Adult;

        // Relationships
        public int PartnerId = -1;
        public int PairedTick = -1;
        public int Children;
        public int LastFertilityDay = -1;

        /// <summary>What the villager is doing right now, for the thought bubble ("" = none).</summary>
        public string Thought = "";

        public float AgeDays(int tick) => (float)(tick - BirthTick) / VillageClock.DayLengthTicks;

        public string FullName => $"{FirstName} {FamilyName}";

        public RoutinePhase DesiredPhase(int minute)
        {
            if (minute < WakeMinute) return RoutinePhase.Sleeping;
            if (minute < WorkStartMinute) return RoutinePhase.Morning;
            if (TakesLunch && minute >= LunchStartMinute && minute < LunchEndMinute) return RoutinePhase.Lunch;
            if (minute < WorkEndMinute) return RoutinePhase.Working;
            if (minute < SleepMinute) return RoutinePhase.Evening;
            return RoutinePhase.Sleeping;
        }
    }
}
