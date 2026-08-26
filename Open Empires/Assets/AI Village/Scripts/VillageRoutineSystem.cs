using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires.Village
{
    /// <summary>
    /// Drives every villager's life: daily routine, needs (hunger / sleep / social / fun),
    /// money and meals, ageing, pairing and births. Runs inside the deterministic sim tick via
    /// <see cref="ISimulationExtension"/> and acts on the world only through ordinary commands
    /// (Gather / Garrison / Move / Patrol / CheatSpawn / Delete) plus the per-unit ungarrison API.
    /// </summary>
    public partial class VillageRoutineSystem : ISimulationExtension
    {
        public const int PlayerId = 0;

        public readonly List<VillagerProfile> Profiles = new List<VillagerProfile>();
        private readonly Dictionary<int, VillagerProfile> byUnitId = new Dictionary<int, VillagerProfile>();

        /// <summary>World position of the village square (in front of the Town Center).</summary>
        public FixedVector3 PlazaPosition;

        // ------------------------------------------------------------------ settings
        public int TavernBuildingId = -1;
        public int UniversityBuildingId = -1;
        public int MealPrice = 4;
        public int StarvationThreshold = 3;

        /// <summary>Life-stage lengths in days.</summary>
        public int ChildDays = 3, AdultDays = 5, ElderDays = 3;

        /// <summary>Jobs a child can take when coming of age (workplace must exist).</summary>
        public struct JobSlot { public VillageJob Job; public int WorkplaceId; public int PatrolId; }
        public readonly List<JobSlot> AdultJobSlots = new List<JobSlot>();

        private const int MealTimeoutTicks = 2400;
        private const int MaintenanceInterval = 30;
        private const int CommandCooldown = 45;
        private const int PairingInterval = 90;

        // Needs are stored in millionths (0..1,000,000) so decay/restore rates are integers.
        public const int NeedMax = 1_000_000;
        private const int HungerDecay = 267;   // empty in ~10h
        private const int EnergyDecay = 125;   // empty in ~21h awake (a full day up is tiring, a normal day isn't)
        private const int EnergyRestore = 400; // full in ~6.5h asleep
        private const int SocialDecay = 56;    // empty in ~2 days
        private const int SocialRestore = 1667;
        private const int FunDecay = 74;       // empty in ~1.5 days
        private const int FunRestore = 1200;

        private uint rng;
        private readonly List<ResourceNodeData> nodeScratch = new List<ResourceNodeData>();
        private readonly List<VillagerProfile> plazaScratch = new List<VillagerProfile>();

        private struct PendingBirth { public int HouseId; public int ParentA; public int ParentB; public string Family; public int HouseholdIndex; }
        private readonly Queue<PendingBirth> pendingBirths = new Queue<PendingBirth>();

        public VillageRoutineSystem() { rng = 0x9E3779B9u; }
        public void Seed(int seed) { rng = (uint)seed * 2654435761u + 7u; if (rng == 0) rng = 1; }

        // ------------------------------------------------------------------ activity log

        public enum LogCategory { Events = 0, Social = 1, Economy = 2, Routine = 3 }

        public struct ActivityEntry { public int Tick; public int UnitId; public string Text; public bool Notable; public LogCategory Category; }
        public readonly List<ActivityEntry> Activity = new List<ActivityEntry>();
        private const int MaxActivity = 800;

        private void Log(GameSimulation sim, VillagerProfile p, string what, bool notable = false)
        {
            Activity.Add(new ActivityEntry
            {
                Tick = sim.CurrentTick,
                UnitId = p != null ? p.UnitId : -1,
                Text = p != null ? $"{p.FullName} {what}" : what,
                Notable = notable,
                Category = Classify(what, notable),
            });
            if (Activity.Count > MaxActivity) Activity.RemoveRange(0, Activity.Count - MaxActivity);
        }

        /// <summary>Bucket a log line so the HUD can filter: Events (big things), Social, Economy, Routine.</summary>
        private static LogCategory Classify(string what, bool notable)
        {
            string s = what.ToLowerInvariant();
            if (s.Contains("couple") || s.Contains("turned down") || s.Contains("argument") || s.Contains("gawk") || s.Contains("lonely") || s.Contains("company") || s.Contains("mourns"))
                return notable && (s.Contains("couple") || s.Contains("mourns")) ? LogCategory.Events : LogCategory.Social;
            if (notable) return LogCategory.Events;
            if (s.Contains("coins") || s.Contains("tavern") || s.Contains("breakfast") || s.Contains("lunch") || s.Contains("dinner") || s.Contains("snack") || s.Contains("afford") || s.Contains("timber") || s.Contains("purse"))
                return LogCategory.Economy;
            if (s.Contains("bored") || s.Contains("square") || s.Contains("dancing") || s.Contains("chasing") || s.Contains("tree") || s.Contains("chicken") || s.Contains("speech") || s.Contains("voices") || s.Contains("spinning") || s.Contains("treasure") || s.Contains("clouds") || s.Contains("keys"))
                return LogCategory.Social;
            return LogCategory.Routine;
        }

        /// <summary>Village-wide notable event (no single villager).</summary>
        private void LogEvent(GameSimulation sim, string what) => Log(sim, null, what, true);

        private static string BuildingName(GameSimulation sim, int buildingId)
        {
            var b = sim.BuildingRegistry.GetBuilding(buildingId);
            if (b == null) return "somewhere";
            switch (b.Type)
            {
                case BuildingType.TownCenter: return "the town center";
                case BuildingType.LumberYard: return "the lumber yard";
                default: return "the " + b.Type.ToString().ToLowerInvariant();
            }
        }

        public VillagerProfile GetProfile(int unitId)
        {
            byUnitId.TryGetValue(unitId, out var p);
            return p;
        }

        public void AddProfile(VillagerProfile profile)
        {
            Profiles.Add(profile);
            byUnitId[profile.UnitId] = profile;
        }

        // ------------------------------------------------------------------ tick

        public void Tick(GameSimulation sim)
        {
            int tick = sim.CurrentTick;
            int minute = VillageClock.MinuteOfDay(tick);
            int day = VillageClock.Day(tick);

            AdoptNewborns(sim);
            SeasonPass(sim);
            WolfPass(sim, minute, day);
            RaidPass(sim, minute, day);
            HorsePass(sim);

            for (int i = 0; i < Profiles.Count; i++)
            {
                var p = Profiles[i];
                if (p.IsDead) continue;
                var unit = sim.UnitRegistry.GetUnit(p.UnitId);
                bool garrisoned = unit == null && sim.UnitRegistry.GetGarrisonedUnit(p.UnitId) != null;
                if (unit == null && !garrisoned) { MarkDead(sim, p); continue; }
                if (unit != null && unit.State == UnitState.Dead) { MarkDead(sim, p); continue; }
                if (unit != null) p.LastKnownPos = unit.SimPosition;

                if (day != p.LastMealDay)
                {
                    p.LastMealDay = day;
                    p.MealsEatenMask = 0;
                    p.MealsHandledMask = 0;
                }

                Earn(sim, p, unit);
                TickTraitExpiry(sim, p);
                UpdateAge(sim, p, unit, garrisoned);
                if (p.IsDead) continue;
                UpdateNeeds(sim, p, unit, garrisoned);
                if (CheckStuck(sim, p, unit)) { UpdateThought(sim, p, unit, garrisoned); continue; }

                var desired = p.DesiredPhase(minute);
                if (desired != p.Phase)
                {
                    p.Phase = desired;
                    if ((p.PendingMeal != Meal.None || p.Errand == Errand.Eating) && desired != RoutinePhase.Sleeping)
                    {
                        p.PhaseBeginPending = true; // finish eating first
                    }
                    else
                    {
                        if (p.PendingMeal != Meal.None) CancelMeal(sim, p, "gave up on dinner and went to bed");
                        ReleaseErrand(sim, p);
                        BeginPhase(sim, p, unit, garrisoned);
                    }
                    UpdateThought(sim, p, unit, garrisoned);
                    continue;
                }

                // Threats (wolves, raiders) override everything except sleeping indoors.
                if (ThreatActive && unit != null && (tick + p.UnitId) % 30 == 0 && ReactToThreat(sim, p, unit)) { UpdateThought(sim, p, unit, garrisoned); continue; }

                if (p.PendingMeal != Meal.None) { HandlePendingMeal(sim, p, unit, garrisoned); UpdateThought(sim, p, unit, garrisoned); continue; }
                if (p.Errand != Errand.None) { HandleErrand(sim, p, unit, garrisoned); UpdateThought(sim, p, unit, garrisoned); continue; }

                if (p.Phase != RoutinePhase.Sleeping)
                {
                    var due = DueMeal(p, minute);
                    if (due != Meal.None && TryStartMeal(sim, p, unit, garrisoned, due)) { UpdateThought(sim, p, unit, garrisoned); continue; }
                    if (TryStartNeedErrand(sim, p, unit, garrisoned)) { UpdateThought(sim, p, unit, garrisoned); continue; }
                    if (p.Quirky && TryStartQuirk(sim, p, unit, garrisoned)) { UpdateThought(sim, p, unit, garrisoned); continue; }
                    if (unit != null && (tick + p.UnitId) % 45 == 0 && TryGawk(sim, p, unit)) { UpdateThought(sim, p, unit, garrisoned); continue; }
                }

                if ((tick + p.UnitId) % MaintenanceInterval == 0)
                    Maintain(sim, p, unit, garrisoned);

                UpdateThought(sim, p, unit, garrisoned);
            }

            if (tick % PairingInterval == 0) PairingPass(sim);
            if (tick % FightInterval == 0) FightPass(sim);
            FightsTick(sim);
            if (tick % 90 == 30) BurialPass(sim);
            ProjectsPass(sim, minute, day);
            if (tick % 60 == 0) ConceptionPass(sim, minute, day);
            int ticksPerHour = Mathf.Max(1, VillageClock.DayLengthTicks / 24);
            if (tick % ticksPerHour == ticksPerHour / 2) EventsPass(sim);
        }

        // ------------------------------------------------------------------ relationships

        /// <summary>Pairwise relationship scores, −100..100 (0 = strangers). Keyed by the two unit ids.</summary>
        public readonly Dictionary<long, int> Relations = new Dictionary<long, int>();
        private static long PairKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        public int Relation(int a, int b) => a == b ? 0 : Relations.TryGetValue(PairKey(a, b), out var v) ? v : 0;

        public void ChangeRelation(int a, int b, int delta)
        {
            if (a == b || a < 0 || b < 0) return;
            long k = PairKey(a, b);
            Relations.TryGetValue(k, out var v);
            Relations[k] = Mathf.Clamp(v + delta, -100, 100);
        }

        /// <summary>Relationship change that both villagers will remember (shown on the card and in thoughts).</summary>
        public void Remember(GameSimulation sim, VillagerProfile a, VillagerProfile b, int delta, string aText, string bText)
        {
            if (a == null || b == null) return;
            ChangeRelation(a.UnitId, b.UnitId, delta);
            AddMemory(sim, a, b.UnitId, delta, aText);
            AddMemory(sim, b, a.UnitId, delta, bText);
        }

        private const int MaxMemories = 10;
        private static void AddMemory(GameSimulation sim, VillagerProfile p, int otherId, int delta, string text)
        {
            p.Memories.Add(new VillagerProfile.Memory { Tick = sim.CurrentTick, OtherId = otherId, Delta = delta, Text = text });
            if (p.Memories.Count > MaxMemories) p.Memories.RemoveAt(0);
        }

        /// <summary>The memory that weighs on them most (strongest recent feeling).</summary>
        public bool StrongestMemory(VillagerProfile p, int tick, out VillagerProfile.Memory best)
        {
            best = default; int bestScore = 0;
            for (int i = 0; i < p.Memories.Count; i++)
            {
                var m = p.Memories[i];
                int ageDays = (tick - m.Tick) / Mathf.Max(1, VillageClock.DayLengthTicks);
                int score = Mathf.Abs(m.Delta) - ageDays * 4;
                if (score > bestScore) { bestScore = score; best = m; }
            }
            return bestScore > 0;
        }

        /// <summary>Talking spreads opinions: a strong feeling about a third person rubs off on the listener.</summary>
        private void Gossip(GameSimulation sim, VillagerProfile speaker, VillagerProfile listener)
        {
            if (sim.CurrentTick - speaker.LastGossipTick < 600) return;
            speaker.LastGossipTick = sim.CurrentTick;
            // Find the speaker's strongest opinion about someone the listener also knows of.
            VillagerProfile subject = null; int strongest = 0;
            foreach (var o in Profiles)
            {
                if (o == speaker || o == listener || o.IsDead) continue;
                int v = Relation(speaker.UnitId, o.UnitId);
                if (Mathf.Abs(v) >= 30 && Mathf.Abs(v) > Mathf.Abs(strongest)) { strongest = v; subject = o; }
            }
            if (subject == null) return;
            int nudge = strongest > 0 ? 2 : -3;
            ChangeRelation(listener.UnitId, subject.UnitId, nudge);
            if (Chance(20))
                Log(sim, speaker, strongest > 0 ? $"sang {subject.FullName}'s praises to {listener.FirstName}" : $"badmouthed {subject.FullName} to {listener.FirstName}");
        }

        /// <summary>Best friends / worst enemies of a villager, for the card.</summary>
        public void TopRelations(VillagerProfile p, int count, List<(VillagerProfile who, int score)> friends, List<(VillagerProfile who, int score)> rivals)
        {
            friends.Clear(); rivals.Clear();
            foreach (var o in Profiles)
            {
                if (o == p || o.IsDead) continue;
                int s = Relation(p.UnitId, o.UnitId);
                if (s > 5) friends.Add((o, s)); else if (s < -5) rivals.Add((o, s));
            }
            friends.Sort((x, y) => y.score.CompareTo(x.score));
            rivals.Sort((x, y) => x.score.CompareTo(y.score));
            if (friends.Count > count) friends.RemoveRange(count, friends.Count - count);
            if (rivals.Count > count) rivals.RemoveRange(count, rivals.Count - count);
        }

        public static string RelationWord(int s) =>
            s >= 60 ? "close friend" : s >= 30 ? "friend" : s >= 10 ? "acquaintance" : s > -10 ? "stranger" : s > -30 ? "dislikes" : s > -60 ? "enemy" : "hated";

        // ------------------------------------------------------------------ comfort limits

        public const int TavernSeats = 8;         // diners at a time (cooks don't count)
        public const int HouseComfortable = 5;    // more than this at home and everyone gets grumpy

        private int Diners(GameSimulation sim)
        {
            var t = sim.BuildingRegistry.GetBuilding(TavernBuildingId);
            if (t == null) return 0;
            int n = 0;
            foreach (var id in t.GarrisonedUnitIds) { var p = GetProfile(id); if (p != null && p.Job != VillageJob.Cook) n++; }
            return n;
        }

        // ------------------------------------------------------------------ mood

        /// <summary>Overall mood 0..NeedMax: the average of the four needs, nudged by traits and ailments.</summary>
        public int Mood(VillagerProfile p)
        {
            int m = (p.Hunger + p.Energy + p.Social + p.Fun) / 4;
            if (p.Has(Trait.Grumpy)) m -= NeedMax / 10;
            if (p.Has(Trait.Cheerful)) m += NeedMax / 10;
            if (p.Has(Trait.Sick)) m -= NeedMax * 15 / 100;
            if (p.Has(Trait.BrokenLeg)) m -= NeedMax / 10;
            if (p.IsStarving) m -= NeedMax / 4;
            return Mathf.Clamp(m, 0, NeedMax);
        }

        public int MoodPercent(VillagerProfile p) => Mood(p) * 100 / NeedMax;

        /// <summary>The unit vanished from the registry (killed by the combat system): record the death and leave a body.</summary>
        private void MarkDead(GameSimulation sim, VillagerProfile p)
        {
            if (p.IsDead) return;
            p.IsDead = true;
            string cause = ThreatActive ? (SoldierIds.Count > 0 && WolfIds.Count == 0 ? "was slain by raiders" : WolfIds.Count > 0 && SoldierIds.Count == 0 ? "was killed by wolves" : "was killed in the attack") : "died";
            p.Activity = cause;
            p.Thought = "";
            p.FightId = -1;
            if (p.Mounted) StablesHorses = Mathf.Min(StableCapacity, StablesHorses + 1); // the horse finds its way home
            Corpses.Add(new Corpse { Id = nextCorpseId++, UnitId = p.UnitId, Name = p.FullName, Gender = p.Gender, Position = p.LastKnownPos, DeathTick = sim.CurrentTick });
            var partner = p.PartnerId >= 0 ? GetProfile(p.PartnerId) : null;
            if (partner != null && partner.PartnerId == p.UnitId) { partner.PartnerId = -1; Log(sim, partner, $"mourns {p.FirstName}"); }
            ReleaseErrand(sim, p);
            Log(sim, p, "☠ " + cause, true);
        }

        // ------------------------------------------------------------------ phases

        private void BeginPhase(GameSimulation sim, VillagerProfile p, UnitData unit, bool garrisoned, bool resume = false)
        {
            switch (p.Phase)
            {
                case RoutinePhase.Sleeping:
                    p.Activity = "Sleeping";
                    if (!resume) Log(sim, p, "is heading home to sleep"); // babies: see ConceptionPass (nightly, once per couple)
                    GoHome(sim, p, unit, garrisoned);
                    break;

                case RoutinePhase.Morning:
                    p.Activity = "Waking up at home";
                    if (!resume) Log(sim, p, "woke up");
                    LeaveBuilding(sim, p, garrisoned);
                    break;

                case RoutinePhase.Working:
                    unit = LeaveBuilding(sim, p, garrisoned) ?? unit;
                    if (p.IsBuilder && ActiveProject != null && unit != null)
                    {
                        if (!resume) Log(sim, p, $"headed to the {ActiveProject.Label} construction site");
                        StartProjectErrand(sim, p, unit);
                        break;
                    }
                    if (!resume) Log(sim, p, WorkVerbPast(p.Job, BuildingName(sim, p.WorkplaceBuildingId)));
                    IssueWork(sim, p, unit);
                    break;

                case RoutinePhase.Lunch:
                    if (resume || !TryStartMeal(sim, p, unit, garrisoned, Meal.Lunch))
                    {
                        p.Activity = "Lunch break at home";
                        if (!resume) Log(sim, p, "went home for the lunch break");
                        GoHome(sim, p, unit, garrisoned);
                    }
                    break;

                case RoutinePhase.Evening:
                    unit = LeaveBuilding(sim, p, garrisoned) ?? unit;
                    // Unpaired adults and lonely villagers head for the square more often.
                    bool leisure = p.EveningLeisure
                        || (p.Stage == LifeStage.Adult && p.PartnerId < 0 && Chance(70))
                        || p.Social < NeedMax / 2 || p.Fun < NeedMax / 2;
                    if (leisure)
                    {
                        p.Activity = p.Stage == LifeStage.Child ? "Playing at the village square" : "Socialising at the village square";
                        if (!resume) Log(sim, p, "finished the day and went to the village square");
                        GoToPlaza(sim, p, unit);
                    }
                    else
                    {
                        p.Activity = "Resting at home";
                        if (!resume) Log(sim, p, "finished the day and went home to rest");
                        GoHome(sim, p, unit, garrisoned: false);
                    }
                    break;
            }
        }

        private void Maintain(GameSimulation sim, VillagerProfile p, UnitData unit, bool garrisoned)
        {
            if (sim.CurrentTick - p.LastCommandTick < CommandCooldown) return;

            if (garrisoned)
            {
                if (p.Phase == RoutinePhase.Sleeping || p.Phase == RoutinePhase.Lunch)
                {
                    int where = FindGarrisonBuilding(sim, p.UnitId);
                    if (where >= 0 && where != p.HomeBuildingId) GoHome(sim, p, null, garrisoned: true);
                }
                return;
            }

            if (unit == null || unit.State != UnitState.Idle) return;

            switch (p.Phase)
            {
                case RoutinePhase.Working: IssueWork(sim, p, unit); break;
                case RoutinePhase.Sleeping:
                case RoutinePhase.Lunch: GoHome(sim, p, unit, garrisoned: false); break;
                case RoutinePhase.Evening: if (p.Activity == "Resting at home") GoHome(sim, p, unit, garrisoned: false); break;
            }
        }

        // ------------------------------------------------------------------ needs

        private void UpdateNeeds(GameSimulation sim, VillagerProfile p, UnitData unit, bool garrisoned)
        {
            int hungerDecay = (p.Has(Trait.Glutton) || p.Has(Trait.WeakStomach)) ? HungerDecay * 3 / 2 : HungerDecay;
            p.Hunger = Mathf.Max(0, p.Hunger - hungerDecay);

            bool sleepingAtHome = garrisoned && FindGarrisonBuilding(sim, p.UnitId) == p.HomeBuildingId
                                  && (p.Phase == RoutinePhase.Sleeping || p.Errand == Errand.Nap);
            int energyDecay = (p.Has(Trait.Lazy) || p.Has(Trait.Sick)) ? EnergyDecay * 13 / 10 : EnergyDecay;
            int energyRestore = p.Has(Trait.LightSleeper) ? EnergyRestore * 6 / 10 : EnergyRestore;
            p.Energy = sleepingAtHome ? Mathf.Min(NeedMax, p.Energy + energyRestore) : Mathf.Max(0, p.Energy - energyDecay);

            bool atPlazaIdle = unit != null && unit.State == UnitState.Idle && IsNearPlaza(sim, unit, 6);
            // Conversations are with a specific person: pick (and keep) the nearest neighbour at the square.
            VillagerProfile partner = null;
            if (atPlazaIdle)
            {
                partner = p.ChatPartnerId >= 0 ? GetProfile(p.ChatPartnerId) : null;
                var pu = partner != null && !partner.IsDead ? sim.UnitRegistry.GetUnit(partner.UnitId) : null;
                bool partnerStillHere = pu != null && pu.State == UnitState.Idle && TileDistanceSq(sim, unit, pu) <= 5 * 5 && CountsAsCompany(p, partner);
                if (!partnerStillHere) partner = FindChatPartner(sim, p, unit, 5);
                p.ChatPartnerId = partner != null ? partner.UnitId : -1;
            }
            else p.ChatPartnerId = -1;
            bool company = partner != null;
            bool atHomeOffDuty = garrisoned && p.Phase != RoutinePhase.Sleeping && p.Phase != RoutinePhase.Working;
            int socialDelta;
            if (p.Has(Trait.Introvert))
            {
                // Company wears them out; quiet time (alone at the square, or at home) recharges them.
                if (company) socialDelta = -SocialRestore / 2;
                else if (atPlazaIdle || atHomeOffDuty) socialDelta = SocialRestore / 3;
                else socialDelta = -SocialDecay / 2;
            }
            else
            {
                int restore = SocialRestore;
                if (p.Has(Trait.Extrovert)) restore *= 2;
                if (p.Has(Trait.Grumpy)) restore /= 2;
                if (company)
                {
                    int rel = Relation(p.UnitId, partner.UnitId);
                    if (rel >= 30) restore = restore * 3 / 2;        // time with a friend is worth more
                    else if (rel <= -20) restore /= 3;              // stuck talking to someone they dislike
                    socialDelta = restore;
                }
                else if (atHomeOffDuty) socialDelta = SocialRestore / 8; // family at home
                else socialDelta = -(p.Has(Trait.Extrovert) ? SocialDecay * 2 : SocialDecay);
            }
            p.Social = Mathf.Clamp(p.Social + socialDelta, 0, NeedMax);

            // Talking builds the relationship a little (both directions, once per few seconds) and spreads gossip.
            if (company && (sim.CurrentTick + p.UnitId) % 60 == 0) ChangeRelation(p.UnitId, partner.UnitId, 1);
            if (company && (sim.CurrentTick + p.UnitId) % 300 == 0) Gossip(sim, p, partner);

            // Not every conversation goes well: now and then company turns into an argument.
            if (company && (sim.CurrentTick + p.UnitId) % 150 == 0 && sim.CurrentTick - p.LastArgumentTick > 900)
            {
                int pct = p.Has(Trait.Grumpy) ? 25 : p.Has(Trait.Cheerful) ? 4 : 10;
                if (MoodPercent(p) < 40) pct += 10;
                int rel = Relation(p.UnitId, partner.UnitId);
                if (rel <= -20) pct += 20; else if (rel >= 30) pct /= 2;
                if (Chance(pct)) Argue(sim, p, unit, partner);
            }

            // Crowded house: more than HouseComfortable people inside wears on everyone's nerves.
            if (garrisoned)
            {
                int where = FindGarrisonBuilding(sim, p.UnitId);
                var home = where == p.HomeBuildingId ? sim.BuildingRegistry.GetBuilding(where) : null;
                p.Crowded = home != null && home.GarrisonCount > HouseComfortable;
                if (p.Crowded)
                {
                    p.Social = Mathf.Max(0, p.Social - SocialDecay * 3);
                    p.Fun = Mathf.Max(0, p.Fun - FunDecay * 3);
                    if ((sim.CurrentTick + p.UnitId) % 1800 == 0) Log(sim, p, $"is fed up with how crowded the {p.FamilyName} house is ({home.GarrisonCount} people)");
                }
            }
            else p.Crowded = false;

            bool havingFun = atPlazaIdle || (garrisoned && FindGarrisonBuilding(sim, p.UnitId) == TavernBuildingId);
            int funDecay = p.Has(Trait.Cheerful) ? FunDecay / 2 : p.Has(Trait.Grumpy) ? FunDecay * 3 / 2 : FunDecay;
            p.Fun = havingFun ? Mathf.Min(NeedMax, p.Fun + FunRestore) : Mathf.Max(0, p.Fun - funDecay);
        }

        private bool IsNearPlaza(GameSimulation sim, UnitData unit, int tiles)
        {
            var a = sim.MapData.WorldToTile(unit.SimPosition);
            var b = sim.MapData.WorldToTile(PlazaPosition);
            int dx = a.x - b.x, dz = a.y - b.y;
            return dx * dx + dz * dz <= tiles * tiles;
        }

        private static int TileDistanceSq(GameSimulation sim, UnitData a, UnitData b)
        {
            var ta = sim.MapData.WorldToTile(a.SimPosition);
            var tb = sim.MapData.WorldToTile(b.SimPosition);
            int dx = ta.x - tb.x, dz = ta.y - tb.y;
            return dx * dx + dz * dz;
        }

        private static bool CountsAsCompany(VillagerProfile p, VillagerProfile o)
        {
            if (p.Has(Trait.Misogynist) && o.Gender == Gender.Female) return false;
            if (p.Has(Trait.Misandrist) && o.Gender == Gender.Male) return false;
            return true;
        }

        /// <summary>Nearest idle villager at the square to talk to (prefers friends, avoids enemies when possible).</summary>
        private VillagerProfile FindChatPartner(GameSimulation sim, VillagerProfile p, UnitData unit, int tiles)
        {
            VillagerProfile best = null; int bestScore = int.MinValue;
            for (int i = 0; i < Profiles.Count; i++)
            {
                var o = Profiles[i];
                if (o == p || o.IsDead || !CountsAsCompany(p, o)) continue;
                var ou = sim.UnitRegistry.GetUnit(o.UnitId);
                if (ou == null || ou.State != UnitState.Idle) continue;
                int d = TileDistanceSq(sim, unit, ou);
                if (d > tiles * tiles) continue;
                int score = Relation(p.UnitId, o.UnitId) - d * 2 + (o.ChatPartnerId == p.UnitId ? 20 : 0);
                if (score > bestScore) { bestScore = score; best = o; }
            }
            return best;
        }

        private bool HasCompanyNearby(GameSimulation sim, VillagerProfile p, UnitData unit, int tiles)
        {
            var a = sim.MapData.WorldToTile(unit.SimPosition);
            for (int i = 0; i < Profiles.Count; i++)
            {
                var o = Profiles[i];
                if (o == p || o.IsDead) continue;
                // Misogynists/misandrists don't count the other sex as company.
                if (p.Has(Trait.Misogynist) && o.Gender == Gender.Female) continue;
                if (p.Has(Trait.Misandrist) && o.Gender == Gender.Male) continue;
                var ou = sim.UnitRegistry.GetUnit(o.UnitId);
                if (ou == null) continue;
                var b = sim.MapData.WorldToTile(ou.SimPosition);
                int dx = a.x - b.x, dz = a.y - b.y;
                if (dx * dx + dz * dz <= tiles * tiles) return true;
            }
            return false;
        }

        /// <summary>Start a need-driven errand (nap / snack / socialise / fun) when a need gets low.</summary>
        private bool TryStartNeedErrand(GameSimulation sim, VillagerProfile p, UnitData unit, bool garrisoned)
        {
            bool offDuty = p.Phase == RoutinePhase.Morning || p.Phase == RoutinePhase.Evening || p.Phase == RoutinePhase.Lunch;

            // Exhausted: nap at home (even mid-shift).
            if (p.Energy < NeedMax / 10)
            {
                p.Errand = Errand.Nap;
                p.ErrandStartTick = sim.CurrentTick;
                p.Activity = "Going home for a nap";
                Log(sim, p, "is exhausted and went home for a nap");
                GoHome(sim, p, unit, garrisoned);
                return true;
            }

            // Very hungry between meals: buy a snack.
            if (p.Hunger < NeedMax / 5 && p.Money >= CurrentMealPrice(sim) && !p.HasHandled(Meal.Snack))
            {
                if (TryStartMeal(sim, p, unit, garrisoned, Meal.Snack)) return true;
            }

            if (!offDuty) return false;

            if (p.Social < NeedMax / 4)
            {
                unit = LeaveBuilding(sim, p, garrisoned) ?? unit;
                if (unit == null) return false;
                p.Errand = Errand.Social;
                p.ErrandStartTick = sim.CurrentTick;
                p.Activity = "Looking for company at the square";
                Log(sim, p, "felt lonely and went to the square for company");
                GoToPlaza(sim, p, unit);
                return true;
            }

            if (p.Fun < NeedMax / 4)
            {
                unit = LeaveBuilding(sim, p, garrisoned) ?? unit;
                if (unit == null) return false;
                p.Errand = Errand.Fun;
                p.ErrandStartTick = sim.CurrentTick;
                bool tavern = p.Stage != LifeStage.Child && TavernBuildingId >= 0 && Chance(50);
                p.Activity = tavern ? "Off to the tavern for a drink" : (p.Stage == LifeStage.Child ? "Off to play at the square" : "Off to the square for some fun");
                Log(sim, p, tavern ? "was bored and went to the tavern" : "was bored and went to the square");
                if (tavern)
                {
                    var t = sim.BuildingRegistry.GetBuilding(TavernBuildingId);
                    Enqueue(sim, p, new GarrisonCommand(PlayerId, Ids(p), t.Id));
                }
                else GoToPlaza(sim, p, unit);
                return true;
            }
            return false;
        }

        private void HandleErrand(GameSimulation sim, VillagerProfile p, UnitData unit, bool garrisoned)
        {
            bool done = false;
            switch (p.Errand)
            {
                case Errand.Nap:
                    done = p.Energy > NeedMax * 6 / 10 || p.Phase == RoutinePhase.Sleeping;
                    if (!done && unit != null && unit.State == UnitState.Idle && sim.CurrentTick - p.LastCommandTick > CommandCooldown)
                        GoHome(sim, p, unit, garrisoned: false);
                    break;
                case Errand.Social:
                    done = p.Social > NeedMax * 7 / 10 || p.Phase == RoutinePhase.Working || p.Phase == RoutinePhase.Sleeping;
                    if (!done && unit != null && unit.State == UnitState.Idle && !IsNearPlaza(sim, unit, 6) && sim.CurrentTick - p.LastCommandTick > CommandCooldown)
                        GoToPlaza(sim, p, unit);
                    break;
                case Errand.Fun:
                    done = p.Fun > NeedMax * 7 / 10 || p.Phase == RoutinePhase.Working || p.Phase == RoutinePhase.Sleeping;
                    break;
                case Errand.Gawk:
                {
                    var target = GetProfile(p.GawkTargetId);
                    done = sim.CurrentTick - p.ErrandStartTick > 240 || target == null || target.Errand != Errand.Quirk || p.Phase == RoutinePhase.Sleeping;
                    break;
                }
                case Errand.Fight:
                {
                    var fight = FindFight(p.FightId);
                    done = fight == null;
                    if (!done && unit != null)
                    {
                        // Stay in the scrum: shuffle around the fight tile every couple of seconds.
                        var at = sim.MapData.WorldToTile(unit.SimPosition);
                        int ddx = at.x - fight.Tile.x, ddz = at.y - fight.Tile.y;
                        bool far = ddx * ddx + ddz * ddz > 2 * 2;
                        if ((far && unit.State == UnitState.Idle) || (sim.CurrentTick + p.UnitId) % 45 == 0)
                        {
                            var t = GridPathfinder.FindNearestWalkableTile(sim.MapData,
                                new Vector2Int(fight.Tile.x + (int)(Next() % 3) - 1, fight.Tile.y + (int)(Next() % 3) - 1), 3);
                            Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(t.x, t.y)));
                        }
                    }
                    if (done) p.FightId = -1;
                    break;
                }
                case Errand.Watch:
                    done = FindFight(p.FightId) == null || sim.CurrentTick - p.ErrandStartTick > 600 || p.Phase == RoutinePhase.Sleeping;
                    if (done) p.FightId = -1;
                    break;
                case Errand.Haul: done = HandleHaul(sim, p, unit); break;
                case Errand.Build: done = HandleBuild(sim, p, unit); break;
                case Errand.Bury: done = HandleBury(sim, p, unit); break;
                case Errand.Flee:
                    done = !ThreatActive;
                    if (!done && unit != null && unit.State == UnitState.Idle && sim.CurrentTick - p.LastCommandTick > CommandCooldown) GoHome(sim, p, unit, garrisoned: false);
                    break;
                case Errand.Defend: done = HandleDefend(sim, p, unit); break;
                case Errand.Arm: done = HandleArm(sim, p, unit); break;
                case Errand.Mount: done = HandleMount(sim, p, unit); break;
                case Errand.Dismount: done = HandleDismount(sim, p, unit); break;
                case Errand.Tame: done = HandleTame(sim, p, unit); break;
                case Errand.Lead: done = HandleLead(sim, p, unit); break;
                case Errand.Quirk:
                    done = sim.CurrentTick >= p.QuirkEndTick || p.Phase == RoutinePhase.Sleeping;
                    if (!done && unit != null && sim.CurrentTick >= p.QuirkStepTick)
                    {
                        // Erratic: a new short dash in a random direction every few seconds.
                        p.QuirkStepTick = sim.CurrentTick + 30 + (int)(Next() % 60);
                        var at = sim.MapData.WorldToTile(unit.SimPosition);
                        var tile = new Vector2Int(at.x + (int)(Next() % 9) - 4, at.y + (int)(Next() % 9) - 4);
                        tile = GridPathfinder.FindNearestWalkableTile(sim.MapData, tile, 5);
                        Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(tile.x, tile.y)));
                    }
                    break;
                case Errand.Eating:
                {
                    // Waiting outside doesn't count as eating; the clock starts once seated.
                    if (unit != null)
                    {
                        if (p.WaitingForTable) p.ErrandStartTick = sim.CurrentTick;
                        if (unit.State == UnitState.Idle && sim.CurrentTick - p.LastCommandTick > CommandCooldown) TrySitDown(sim, p);
                        done = !p.WaitingForTable && sim.CurrentTick >= p.ErrandStartTick + EatDurationTicks * 2; // never got in: give up
                    }
                    else
                    {
                        p.WaitingForTable = false;
                        done = sim.CurrentTick >= p.ErrandStartTick + EatDurationTicks;
                        // Sharing a table builds bonds with the other diners.
                        if (!done && (sim.CurrentTick + p.UnitId) % 120 == 0)
                        {
                            var tav = sim.BuildingRegistry.GetBuilding(TavernBuildingId);
                            if (tav != null)
                                foreach (var id in tav.GarrisonedUnitIds)
                                    if (id != p.UnitId) { var o = GetProfile(id); if (o != null && o.Job != VillageJob.Cook) ChangeRelation(p.UnitId, id, 1); }
                        }
                    }
                    break;
                }
            }
            bool longErrand = p.Errand == Errand.Eating || p.Errand == Errand.Haul || p.Errand == Errand.Build || p.Errand == Errand.Flee || p.Errand == Errand.Defend
                           || p.Errand == Errand.Arm || p.Errand == Errand.Mount || p.Errand == Errand.Dismount || p.Errand == Errand.Tame || p.Errand == Errand.Lead;
            if (!longErrand && sim.CurrentTick - p.ErrandStartTick > 4500) done = true; // never get stuck

            if (done)
            {
                ReleaseErrand(sim, p);
                unit = sim.UnitRegistry.GetUnit(p.UnitId);
                garrisoned = unit == null && sim.UnitRegistry.GetGarrisonedUnit(p.UnitId) != null;
                BeginPhase(sim, p, unit, garrisoned, resume: true);
            }
        }

        // ------------------------------------------------------------------ economy: wages

        private void Earn(GameSimulation sim, VillagerProfile p, UnitData unit)
        {
            if (p.Phase != RoutinePhase.Working) return;

            if (VillageJobInfo.Kind(p.Job) == JobKind.Gather)
            {
                if (unit != null && unit.LastDepositTick > p.LastDepositTickSeen && unit.LastDepositTick > 0)
                {
                    p.LastDepositTickSeen = unit.LastDepositTick;
                    p.Money += WageWithTraits(p, Mathf.Max(1, unit.LastDepositAmount));
                    ApplySeasonToDeposit(sim, unit);
                }
                return;
            }

            int wage = VillageJobInfo.HourlyWage(p.Job);
            if (wage <= 0) return;
            int ticksPerHour = Mathf.Max(1, VillageClock.DayLengthTicks / 24);
            if (++p.WageTicks >= ticksPerHour) { p.WageTicks = 0; p.Money += WageWithTraits(p, wage); }
        }

        private static int WageWithTraits(VillagerProfile p, int wage)
        {
            if (p.Has(Trait.Hardworking)) wage = wage * 3 / 2;
            if (p.Has(Trait.Lazy)) wage = Mathf.Max(1, wage * 3 / 4);
            return wage;
        }

        // ------------------------------------------------------------------ economy: meals

        private static Meal DueMeal(VillagerProfile p, int minute)
        {
            if (!p.HasHandled(Meal.Breakfast) && minute >= p.WakeMinute + 5 && minute < p.WorkStartMinute + 90)
                return Meal.Breakfast;
            int lunchFrom = p.TakesLunch ? p.LunchStartMinute : 12 * 60 + 15;
            int lunchTo = p.TakesLunch ? p.LunchEndMinute + 30 : 13 * 60 + 45;
            if (!p.HasHandled(Meal.Lunch) && minute >= lunchFrom && minute < lunchTo)
                return Meal.Lunch;
            if (!p.HasHandled(Meal.Dinner) && minute >= p.WorkEndMinute && minute < p.SleepMinute - 10)
                return Meal.Dinner;
            return Meal.None;
        }

        private static string MealName(Meal m) => m == Meal.Snack ? "a snack" : m.ToString().ToLowerInvariant();

        private bool TryStartMeal(GameSimulation sim, VillagerProfile p, UnitData unit, bool garrisoned, Meal meal)
        {
            p.MealsHandledMask |= 1 << (int)meal;
            var tavern = sim.BuildingRegistry.GetBuilding(TavernBuildingId);
            if (tavern == null || tavern.IsDestroyed) { MissMeal(sim, p, unit, $"found nowhere to buy {MealName(meal)}"); return false; }
            int price = CurrentMealPrice(sim);
            if (p.Money < price)
            {
                if (meal != Meal.Snack) MissMeal(sim, p, unit, $"couldn't afford {MealName(meal)} ({p.Money} coins, meals cost {price})");
                return false;
            }
            // The tavern only serves what the farmers and foragers actually brought in.
            if (sim.ResourceManager.GetPlayerResources(PlayerId).Food <= 0)
            {
                if (meal != Meal.Snack) MissMeal(sim, p, unit, $"found the tavern out of food for {MealName(meal)}");
                return false;
            }

            if (garrisoned) unit = LeaveBuilding(sim, p, true) ?? unit;
            if (unit == null) return false;

            p.PendingMeal = meal;
            p.MealStartedTick = sim.CurrentTick;
            p.Activity = $"Going to the tavern for {MealName(meal)}";
            Log(sim, p, $"went to the tavern for {MealName(meal)}");
            var door = DoorTile(sim, tavern);
            Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(door.x, door.y)));
            return true;
        }

        private void HandlePendingMeal(GameSimulation sim, VillagerProfile p, UnitData unit, bool garrisoned)
        {
            var tavern = sim.BuildingRegistry.GetBuilding(TavernBuildingId);
            if (tavern == null || tavern.IsDestroyed) { CancelMeal(sim, p, "found the tavern gone"); return; }
            if (garrisoned) { unit = LeaveBuilding(sim, p, true); if (unit == null) return; }
            if (unit == null) return;

            if (sim.CurrentTick - p.MealStartedTick > MealTimeoutTicks)
            {
                CancelMeal(sim, p, $"never made it to the tavern for {MealName(p.PendingMeal)}");
                return;
            }

            var at = sim.MapData.WorldToTile(unit.SimPosition);
            int dx = at.x - (tavern.OriginTileX + tavern.TileFootprintWidth / 2);
            int dz = at.y - (tavern.OriginTileZ - 1);
            if (dx * dx + dz * dz <= 3 * 3) { EatMeal(sim, p, unit); return; }

            if (unit.State == UnitState.Idle && sim.CurrentTick - p.LastCommandTick > CommandCooldown)
            {
                var door = DoorTile(sim, tavern);
                Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(door.x, door.y)));
            }
        }

        private void EatMeal(GameSimulation sim, VillagerProfile p, UnitData unit)
        {
            var meal = p.PendingMeal;
            var store = sim.ResourceManager.GetPlayerResources(PlayerId);
            if (store.Food <= 0)
            {
                CancelMeal(sim, p, $"found the tavern out of food for {MealName(meal)}");
                return;
            }
            store.Food -= 1; // one harvested food per meal
            p.PendingMeal = Meal.None;
            int paid = Mathf.Min(p.Money, CurrentMealPrice(sim));
            p.Money -= paid;
            p.MealsEatenMask |= 1 << (int)meal;
            p.MissedMeals = 0;
            p.IsStarving = false;
            p.Hunger = NeedMax;
            p.LastMealTick = sim.CurrentTick;
            if (meal == Meal.Dinner) p.Fun = Mathf.Min(NeedMax, p.Fun + NeedMax / 5);
            if (p.Has(Trait.Glutton) && !p.Has(Trait.Sick)) p.Fun = Mathf.Min(NeedMax, p.Fun + NeedMax / 5);
            Log(sim, p, $"bought {MealName(meal)} at the tavern (−{paid} coins, {p.Money} left) and sat down to eat");

            // Eating is an activity: go inside and sit for a while, then resume the day.
            p.Errand = Errand.Eating;
            p.ErrandStartTick = sim.CurrentTick;
            p.Activity = $"Eating {MealName(meal)} at the tavern";
            TrySitDown(sim, p);
        }

        /// <summary>Take a seat if one of the tavern's <see cref="TavernSeats"/> is free; otherwise wait outside.</summary>
        private void TrySitDown(GameSimulation sim, VillagerProfile p)
        {
            var tavern = sim.BuildingRegistry.GetBuilding(TavernBuildingId);
            if (tavern == null) return;
            if (Diners(sim) < TavernSeats && tavern.CanGarrison)
            {
                p.WaitingForTable = false;
                Enqueue(sim, p, new GarrisonCommand(PlayerId, Ids(p), tavern.Id));
            }
            else
            {
                if (!p.WaitingForTable) Log(sim, p, "is waiting for a table at the tavern");
                p.WaitingForTable = true;
                p.Fun = Mathf.Max(0, p.Fun - NeedMax / 40);
            }
        }

        // ------------------------------------------------------------------ traits

        /// <summary>Give a villager <paramref name="count"/> random innate traits (no conflicts, no duplicates).</summary>
        public void RollInnateTraits(VillagerProfile p, int count)
        {
            for (int n = 0; n < count; n++)
            {
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    var t = VillageTraits.Innate[Next() % (uint)VillageTraits.Innate.Length];
                    if (p.Has(t)) continue;
                    bool conflict = false;
                    foreach (var have in p.Traits) if (VillageTraits.Conflicts(have, t)) { conflict = true; break; }
                    if (conflict) continue;
                    p.Traits.Add(t);
                    break;
                }
            }
        }

        public void AddTrait(GameSimulation sim, VillagerProfile p, Trait t, string why, int durationTicks = 0)
        {
            if (p.Has(t))
            {
                if (durationTicks > 0) p.TraitExpiry[t] = sim.CurrentTick + durationTicks;
                return;
            }
            for (int i = p.Traits.Count - 1; i >= 0; i--)
                if (VillageTraits.Conflicts(p.Traits[i], t)) p.Traits.RemoveAt(i);
            p.Traits.Add(t);
            if (durationTicks > 0) p.TraitExpiry[t] = sim.CurrentTick + durationTicks;
            ApplyPace(sim, p);
            Log(sim, p, $"{why} [{VillageTraits.Icon(t)} {VillageTraits.Name(t)}]", true);
        }

        public void RemoveTrait(GameSimulation sim, VillagerProfile p, Trait t, string why)
        {
            if (!p.Traits.Remove(t)) return;
            p.TraitExpiry.Remove(t);
            ApplyPace(sim, p);
            Log(sim, p, why);
        }

        private readonly List<Trait> expiryScratch = new List<Trait>();
        private void TickTraitExpiry(GameSimulation sim, VillagerProfile p)
        {
            if (p.TraitExpiry.Count == 0) return;
            expiryScratch.Clear();
            foreach (var kv in p.TraitExpiry) if (sim.CurrentTick >= kv.Value) expiryScratch.Add(kv.Key);
            foreach (var t in expiryScratch)
                RemoveTrait(sim, p, t, t == Trait.BrokenLeg ? "is walking normally again — the leg has healed" : t == Trait.Sick ? "is feeling better" : $"is no longer {VillageTraits.Name(t).ToLowerInvariant()}");
        }

        /// <summary>Walk speed = base pace × trait multipliers (applies to the live unit, garrisoned or not).</summary>
        public void ApplyPace(GameSimulation sim, VillagerProfile p)
        {
            var u = sim.UnitRegistry.GetUnit(p.UnitId) ?? sim.UnitRegistry.GetGarrisonedUnit(p.UnitId);
            if (u == null || p.BaseMoveSpeed.Raw == 0) return;
            float mult = 1f;
            if (p.Has(Trait.Fast)) mult *= 1.3f;
            if (p.Has(Trait.Slow)) mult *= 0.7f;
            if (p.Has(Trait.BrokenLeg)) mult *= 0.4f;
            if (p.Mounted) mult *= 2f;
            u.MoveSpeed = Fixed32.FromFloat(p.BaseMoveSpeed.ToFloat() * mult);
        }

        // ------------------------------------------------------------------ gawking

        /// <summary>Villagers near an eccentric mid-episode stop and stare.</summary>
        private bool TryGawk(GameSimulation sim, VillagerProfile p, UnitData unit)
        {
            if (p.Quirky || p.Errand != Errand.None || p.PendingMeal != Meal.None) return false;
            if (sim.CurrentTick < p.NextGawkTick) return false;
            if (unit.State != UnitState.Idle && unit.State != UnitState.Gathering && unit.State != UnitState.Moving) return false;

            var at = sim.MapData.WorldToTile(unit.SimPosition);
            for (int i = 0; i < Profiles.Count; i++)
            {
                var o = Profiles[i];
                if (o == p || !o.Quirky || o.Errand != Errand.Quirk || o.IsDead) continue;
                var ou = sim.UnitRegistry.GetUnit(o.UnitId);
                if (ou == null) continue;
                var b = sim.MapData.WorldToTile(ou.SimPosition);
                int dx = at.x - b.x, dz = at.y - b.y;
                if (dx * dx + dz * dz > 6 * 6) continue;

                int chance = p.Has(Trait.Curious) ? 70 : p.Has(Trait.Distractible) ? 55 : 25;
                p.NextGawkTick = sim.CurrentTick + 600;
                if (!Chance(chance)) return false;

                p.Errand = Errand.Gawk;
                p.ErrandStartTick = sim.CurrentTick;
                p.GawkTargetId = o.UnitId;
                p.GawkCount++;
                p.Activity = $"Staring at {o.FirstName}";
                Log(sim, p, $"stopped to gawk at {o.FullName} ({o.Activity.ToLowerInvariant()})");
                // Stop where they are (a move to their own tile cancels the current task).
                Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(at.x, at.y)));
                if (p.GawkCount >= 5 && !p.Has(Trait.Distractible) && !p.Has(Trait.Curious))
                    AddTrait(sim, p, Trait.Distractible, "can't stop watching the village eccentrics");
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ arguments & fights

        public class Fight
        {
            public int Id;
            public Vector2Int Tile;
            public int EndTick;
            public readonly List<int> Members = new List<int>();
            public readonly List<int> Watchers = new List<int>();
            public string Place;
        }

        public readonly List<Fight> Fights = new List<Fight>();
        private int nextFightId = 1;
        private const int FightInterval = 120;

        private Fight FindFight(int id)
        {
            if (id < 0) return null;
            for (int i = 0; i < Fights.Count; i++) if (Fights[i].Id == id) return Fights[i];
            return null;
        }

        private static bool CanBrawl(VillagerProfile p) =>
            !p.IsDead && p.Stage != LifeStage.Child && p.Errand == Errand.None && p.PendingMeal == Meal.None && p.FightId < 0;

        /// <summary>A conversation goes sour: both lose social; if both are miserable it turns into a fight.</summary>
        private void Argue(GameSimulation sim, VillagerProfile p, UnitData unit, VillagerProfile other = null)
        {
            if (other == null) other = NearestActiveVillager(sim, p, unit, 5, requireBrawlable: false);
            if (other == null) return;
            p.LastArgumentTick = other.LastArgumentTick = sim.CurrentTick;
            p.Social = Mathf.Max(0, p.Social - NeedMax / 8);
            other.Social = Mathf.Max(0, other.Social - NeedMax / 8);
            Remember(sim, p, other, -12, $"argued with {other.FirstName}", $"argued with {p.FirstName}");
            Log(sim, p, $"got into a heated argument with {other.FullName}");
            if (MoodPercent(p) < 45 && MoodPercent(other) < 45 && CanBrawl(p) && CanBrawl(other) && Chance(60))
                StartFight(sim, p, other, "an argument at the square");
        }

        private VillagerProfile NearestActiveVillager(GameSimulation sim, VillagerProfile p, UnitData unit, int tiles, bool requireBrawlable)
        {
            var a = sim.MapData.WorldToTile(unit.SimPosition);
            VillagerProfile best = null; int bestD = tiles * tiles + 1;
            for (int i = 0; i < Profiles.Count; i++)
            {
                var o = Profiles[i];
                if (o == p || o.IsDead) continue;
                if (requireBrawlable && !CanBrawl(o)) continue;
                var ou = sim.UnitRegistry.GetUnit(o.UnitId);
                if (ou == null) continue;
                var b = sim.MapData.WorldToTile(ou.SimPosition);
                int dx = a.x - b.x, dz = a.y - b.y, d = dx * dx + dz * dz;
                if (d < bestD) { bestD = d; best = o; }
            }
            return best;
        }

        /// <summary>Miserable villagers standing near other miserable villagers may come to blows.</summary>
        private void FightPass(GameSimulation sim)
        {
            for (int i = 0; i < Profiles.Count; i++)
            {
                var p = Profiles[i];
                if (!CanBrawl(p)) continue;
                int mood = MoodPercent(p);
                if (mood >= 40) continue;
                var unit = sim.UnitRegistry.GetUnit(p.UnitId);
                if (unit == null) continue;
                var other = NearestActiveVillager(sim, p, unit, 3, requireBrawlable: true);
                if (other == null || MoodPercent(other) >= 40) continue;
                int rel = Relation(p.UnitId, other.UnitId);
                if (rel >= 30) continue; // friends don't come to blows
                int chance = 6 + (40 - mood) / 2 + (p.Has(Trait.Grumpy) ? 8 : 0) + (rel <= -20 ? 15 : 0);
                if (!Chance(chance)) continue;
                StartFight(sim, p, other, DescribePlace(sim, unit));
            }
        }

        private string DescribePlace(GameSimulation sim, UnitData unit)
        {
            if (IsNearPlaza(sim, unit, 6)) return "the square";
            var at = sim.MapData.WorldToTile(unit.SimPosition);
            BuildingData nearest = null; int bestD = 8 * 8;
            foreach (var b in sim.BuildingRegistry.GetAllBuildings())
            {
                int dx = b.OriginTileX - at.x, dz = b.OriginTileZ - at.y, d = dx * dx + dz * dz;
                if (d < bestD) { bestD = d; nearest = b; }
            }
            return nearest != null ? BuildingName(sim, nearest.Id) : "the edge of the village";
        }

        private void StartFight(GameSimulation sim, VillagerProfile a, VillagerProfile b, string place)
        {
            var ua = sim.UnitRegistry.GetUnit(a.UnitId);
            if (ua == null) return;
            var fight = new Fight { Id = nextFightId++, Tile = sim.MapData.WorldToTile(ua.SimPosition), EndTick = sim.CurrentTick + 150 + (int)(Next() % 250), Place = place };
            Fights.Add(fight);
            JoinFight(sim, fight, a, null);
            JoinFight(sim, fight, b, null);
            LogEvent(sim, $"⚔ A fight broke out between {a.FullName} and {b.FullName} near {place}!");

            // Bystanders: some pile in, most gather round to watch.
            for (int i = 0; i < Profiles.Count; i++)
            {
                var o = Profiles[i];
                if (o == a || o == b || o.IsDead || o.FightId >= 0 || o.PendingMeal != Meal.None || o.Errand != Errand.None) continue;
                var ou = sim.UnitRegistry.GetUnit(o.UnitId);
                if (ou == null) continue;
                var t = sim.MapData.WorldToTile(ou.SimPosition);
                int dx = t.x - fight.Tile.x, dz = t.y - fight.Tile.y;
                if (dx * dx + dz * dz > 7 * 7) continue;

                bool hotHead = o.Stage != LifeStage.Child && (o.Has(Trait.Grumpy) || MoodPercent(o) < 50 || o.PartnerId == a.UnitId || o.PartnerId == b.UnitId);
                if (hotHead && Chance(35)) JoinFight(sim, fight, o, o.PartnerId == a.UnitId ? a : o.PartnerId == b.UnitId ? b : null);
                else if (Chance(65))
                {
                    o.Errand = Errand.Watch; o.ErrandStartTick = sim.CurrentTick; o.FightId = fight.Id;
                    o.Activity = "Watching the fight";
                    fight.Watchers.Add(o.UnitId);
                    var spot = GridPathfinder.FindNearestWalkableTile(sim.MapData, new Vector2Int(fight.Tile.x + (dx == 0 ? 2 : dx > 0 ? 2 : -2), fight.Tile.y + (dz == 0 ? 2 : dz > 0 ? 2 : -2)), 4);
                    Enqueue(sim, o, new MoveCommand(PlayerId, Ids(o), sim.MapData.TileToWorldFixed(spot.x, spot.y)));
                }
            }
        }

        private void JoinFight(GameSimulation sim, Fight fight, VillagerProfile p, VillagerProfile defending)
        {
            var unit = sim.UnitRegistry.GetUnit(p.UnitId);
            if (unit == null) return;
            // Everyone already in the scrum becomes an enemy; the person you defend becomes a friend.
            foreach (var id in fight.Members)
            {
                var m = GetProfile(id);
                if (m == null) continue;
                if (defending != null && id == defending.UnitId) Remember(sim, p, m, 15, $"stood up for {m.FirstName} in a brawl", $"{p.FirstName} stood up for me in a brawl");
                else Remember(sim, p, m, -20, $"brawled with {m.FirstName}", $"brawled with {p.FirstName}");
            }
            p.Errand = Errand.Fight;
            p.ErrandStartTick = sim.CurrentTick;
            p.FightId = fight.Id;
            p.FightsFought++;
            p.Activity = "Brawling";
            fight.Members.Add(p.UnitId);
            if (fight.Members.Count > 2)
                Log(sim, p, defending != null ? $"waded in to defend {defending.FirstName}" : "joined the brawl", true);
            Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(fight.Tile.x, fight.Tile.y)));
        }

        private void FightsTick(GameSimulation sim)
        {
            for (int i = Fights.Count - 1; i >= 0; i--)
            {
                var f = Fights[i];
                // Fists fly: every brawler lands a real hit on another brawler now and then, so health
                // bars drop and a brawl can actually kill a weakened villager.
                if ((sim.CurrentTick - f.Members.Count) % 45 == 0 && f.Members.Count >= 2)
                {
                    for (int m = 0; m < f.Members.Count; m++)
                    {
                        var attacker = sim.UnitRegistry.GetUnit(f.Members[m]);
                        var ap = GetProfile(f.Members[m]);
                        if (attacker == null || ap == null || ap.IsDead) continue;
                        int victimId = f.Members[(m + 1 + (int)(Next() % (uint)Mathf.Max(1, f.Members.Count - 1))) % f.Members.Count];
                        if (victimId == f.Members[m]) continue;
                        var victim = sim.UnitRegistry.GetUnit(victimId);
                        var vp = GetProfile(victimId);
                        if (victim == null || vp == null || vp.IsDead) continue;
                        int dmg = ap.Armed ? 6 : 3;
                        victim.CurrentHealth -= dmg;
                        victim.LastDamageTick = sim.CurrentTick;          // health bar / hit flash
                        victim.LastDamageFromPos = attacker.SimPosition;
                        attacker.LastAttackTick = sim.CurrentTick;         // swing animation
                        attacker.LastAttackTargetPos = victim.SimPosition;
                        if (victim.CurrentHealth <= 0)
                        {
                            victim.CurrentHealth = 0;
                            Kill(sim, vp, $"was beaten to death by {ap.FullName} in a brawl");
                        }
                    }
                }

                // Fights fizzle if fewer than two brawlers are still standing.
                int standing = 0;
                foreach (var id in f.Members) { var m = GetProfile(id); if (m != null && !m.IsDead && sim.UnitRegistry.GetUnit(id) != null) standing++; }
                if (sim.CurrentTick < f.EndTick && standing >= 2) continue;
                ResolveFight(sim, f);
                Fights.RemoveAt(i);
            }
        }

        private void ResolveFight(GameSimulation sim, Fight f)
        {
            bool someoneDied = false;
            var names = new List<string>();
            foreach (var id in f.Members)
            {
                var p = GetProfile(id);
                if (p == null || p.IsDead) continue;
                names.Add(p.FirstName);
                p.Social = Mathf.Max(0, p.Social - NeedMax / 4);
                p.Fun = Mathf.Max(0, p.Fun - NeedMax / 6);
                p.Energy = Mathf.Max(0, p.Energy - NeedMax / 6);
                p.FightId = -1;

                if (Chance(30)) AddTrait(sim, p, Trait.BrokenLeg, "limped away from the fight with a broken leg", 2 * VillageClock.DayLengthTicks);
                else if (Chance(20)) AddTrait(sim, p, Trait.Grumpy, "has been in a foul temper since the fight");
            }
            foreach (var id in f.Watchers) { var w = GetProfile(id); if (w != null && w.FightId == f.Id) { w.FightId = -1; } }
            foreach (var id in f.Members) { var m = GetProfile(id); if (m != null && m.IsDead && m.Activity.Contains("brawl")) someoneDied = true; }
            LogEvent(sim, someoneDied
                ? $"☠ The fight near {f.Place} ended in a death ({string.Join(", ", names)})"
                : $"The fight near {f.Place} broke up ({string.Join(", ", names)})");
        }

        // ------------------------------------------------------------------ random events

        private readonly List<VillagerProfile> eventScratch = new List<VillagerProfile>();

        /// <summary>Once an hour there's a small chance something notable happens in the village.</summary>
        private void EventsPass(GameSimulation sim)
        {
            if (!Chance(12)) return;
            int minute = VillageClock.MinuteOfDay(sim.CurrentTick);
            int ticksPerHour = Mathf.Max(1, VillageClock.DayLengthTicks / 24);
            int roll = (int)(Next() % 100);

            if (roll < 30)
            {
                // Bad food at the tavern: everyone who ate in the last hour gets sick.
                eventScratch.Clear();
                foreach (var p in Profiles)
                    if (!p.IsDead && sim.CurrentTick - p.LastMealTick <= ticksPerHour) eventScratch.Add(p);
                if (eventScratch.Count == 0) return;
                LogEvent(sim, $"☠ The tavern's stew was off — {eventScratch.Count} villager{(eventScratch.Count > 1 ? "s" : "")} fell ill");
                foreach (var p in eventScratch)
                {
                    AddTrait(sim, p, Trait.Sick, "got food poisoning", VillageClock.DayLengthTicks);
                    p.Fun = Mathf.Max(0, p.Fun - NeedMax / 4);
                    if (Chance(25) && !p.Has(Trait.WeakStomach)) AddTrait(sim, p, Trait.WeakStomach, "never quite recovered");
                }
            }
            else if (roll < 55)
            {
                // A fight at home between two household members who are both inside.
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    var a = Profiles[(int)(Next() % (uint)Profiles.Count)];
                    if (a.IsDead || a.Stage == LifeStage.Child || sim.UnitRegistry.GetGarrisonedUnit(a.UnitId) == null) continue;
                    if (FindGarrisonBuilding(sim, a.UnitId) != a.HomeBuildingId) continue;
                    VillagerProfile b = null;
                    foreach (var o in Profiles)
                        if (o != a && !o.IsDead && o.Stage != LifeStage.Child && o.HomeBuildingId == a.HomeBuildingId
                            && FindGarrisonBuilding(sim, o.UnitId) == a.HomeBuildingId) { b = o; break; }
                    if (b == null) continue;
                    var loser = Chance(50) ? a : b;
                    LogEvent(sim, $"⚔ A fight broke out in the {a.FamilyName} house — {a.FirstName} and {b.FirstName} came to blows and {loser.FirstName} broke a leg");
                    AddTrait(sim, loser, Trait.BrokenLeg, "is hobbling around", 2 * VillageClock.DayLengthTicks);
                    a.Social = Mathf.Max(0, a.Social - NeedMax / 3);
                    b.Social = Mathf.Max(0, b.Social - NeedMax / 3);
                    if (Chance(30)) AddTrait(sim, loser, Trait.Grumpy, "has been in a foul mood since the fight");
                    return;
                }
            }
            else if (roll < 75)
            {
                if (minute < 17 * 60 || minute > 21 * 60) return;
                LogEvent(sim, "♪ Travelling musicians played at the square tonight — spirits are high");
                foreach (var p in Profiles)
                {
                    if (p.IsDead) continue;
                    p.Fun = Mathf.Min(NeedMax, p.Fun + NeedMax * 3 / 10);
                    p.Social = Mathf.Min(NeedMax, p.Social + NeedMax / 5);
                    if (!p.EveningLeisure && Chance(50)) p.EveningLeisure = true;
                }
            }
            else if (roll < 88)
            {
                var p = Profiles[(int)(Next() % (uint)Profiles.Count)];
                if (p.IsDead) return;
                int found = 8 + (int)(Next() % 12);
                p.Money += found;
                Log(sim, p, $"found a purse with {found} coins in the road", true);
                p.Fun = Mathf.Min(NeedMax, p.Fun + NeedMax / 5);
            }
            else
            {
                var p = Profiles[(int)(Next() % (uint)Profiles.Count)];
                if (p.IsDead || p.Money < 6) return;
                int lost = Mathf.Min(p.Money, 5 + (int)(Next() % 10));
                p.Money -= lost;
                Log(sim, p, $"was pickpocketed — {lost} coins gone", true);
                p.Fun = Mathf.Max(0, p.Fun - NeedMax / 4);
            }
        }

        // ------------------------------------------------------------------ stuck watchdog

        private const int StuckTicksThreshold = 90;   // ~3 s without leaving the tile while "moving"
        private const int DetourCooldown = 240;

        /// <summary>
        /// A villager that is in a moving state but hasn't left its tile for a while is stuck on a
        /// bad path. Send them a few tiles away in a random direction; once they arrive (Idle) the
        /// normal handlers re-issue whatever they were trying to do, usually from a better start tile.
        /// </summary>
        private bool CheckStuck(GameSimulation sim, VillagerProfile p, UnitData unit)
        {
            if (unit == null) { p.StuckTicks = 0; return false; }
            bool moving = unit.State == UnitState.Moving || unit.State == UnitState.MovingToGather
                       || unit.State == UnitState.MovingToDropoff || unit.State == UnitState.MovingToGarrison
                       || unit.State == UnitState.MovingToBuild;
            if (!moving) { p.StuckTicks = 0; return false; }

            var tile = sim.MapData.WorldToTile(unit.SimPosition);
            if (tile.x == p.StuckTileX && tile.y == p.StuckTileZ) p.StuckTicks++;
            else { p.StuckTileX = tile.x; p.StuckTileZ = tile.y; p.StuckTicks = 0; }

            if (p.StuckTicks < StuckTicksThreshold || sim.CurrentTick - p.LastDetourTick < DetourCooldown) return false;

            p.StuckTicks = 0;
            p.LastDetourTick = sim.CurrentTick;
            // Random detour 3–5 tiles away.
            int dx = (int)(Next() % 11) - 5, dz = (int)(Next() % 11) - 5;
            if (dx == 0 && dz == 0) dx = 3;
            var target = GridPathfinder.FindNearestWalkableTile(sim.MapData, new Vector2Int(tile.x + dx, tile.y + dz), 6);
            Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(target.x, target.y)));
            Log(sim, p, "got stuck and is trying another way round");
            return true;
        }

        // ------------------------------------------------------------------ eccentrics

        private static readonly string[] QuirkThoughts =
        {
            "♪ Dancing", "Talking to a tree", "Chasing butterflies", "Counting clouds", "Looking for lost keys",
            "Arguing with a chicken", "Practising a speech", "Hearing voices", "Spinning!", "Hunting for treasure"
        };
        private static readonly string[] QuirkLogs =
        {
            "wandered off dancing", "stopped to have a long talk with a tree", "ran off chasing butterflies",
            "lay down to count clouds", "went looking for keys they never owned", "picked an argument with a chicken",
            "began rehearsing a speech to nobody", "heard voices and followed them", "started spinning in circles",
            "set off to dig for treasure"
        };

        /// <summary>Eccentric villagers occasionally abandon the plan and do something odd for a while.</summary>
        private bool TryStartQuirk(GameSimulation sim, VillagerProfile p, UnitData unit, bool garrisoned)
        {
            if (sim.CurrentTick < p.NextQuirkTick) return false;
            p.NextQuirkTick = sim.CurrentTick + 1500 + (int)(Next() % 4500); // next episode in 0.5–2 h of village time

            unit = LeaveBuilding(sim, p, garrisoned) ?? unit;
            if (unit == null) return false;

            int k = (int)(Next() % (uint)QuirkThoughts.Length);
            p.Errand = Errand.Quirk;
            p.ErrandStartTick = sim.CurrentTick;
            p.QuirkEndTick = sim.CurrentTick + 240 + (int)(Next() % 480);
            p.QuirkStepTick = sim.CurrentTick;
            p.Activity = QuirkThoughts[k];
            Log(sim, p, QuirkLogs[k]);
            return true;
        }

        /// <summary>How long a meal takes (~30 in-game minutes).</summary>
        private int EatDurationTicks => Mathf.Max(30, VillageClock.DayLengthTicks / 48);

        private void CancelMeal(GameSimulation sim, VillagerProfile p, string why)
        {
            p.PendingMeal = Meal.None;
            p.PhaseBeginPending = false;
            MissMeal(sim, p, sim.UnitRegistry.GetUnit(p.UnitId), why);
        }

        private void MissMeal(GameSimulation sim, VillagerProfile p, UnitData unit, string why)
        {
            p.MissedMeals++;
            Log(sim, p, why);
            if (p.MissedMeals < StarvationThreshold) return;

            var data = unit ?? sim.UnitRegistry.GetGarrisonedUnit(p.UnitId);
            if (data == null) return;
            if (!p.IsStarving) { p.IsStarving = true; Log(sim, p, "is starving!"); }
            data.CurrentHealth -= Mathf.Max(1, data.MaxHealth / 3);
            if (data.CurrentHealth <= 0) { data.CurrentHealth = 0; Kill(sim, p, "died of starvation"); }
        }

        private void Kill(GameSimulation sim, VillagerProfile p, string how)
        {
            p.IsDead = true;
            p.Activity = how;
            p.Thought = "";
            p.FightId = -1;
            CreateCorpse(sim, p);
            ReleaseErrand(sim, p);
            Log(sim, p, "☠ " + how, true);
            var partner = p.PartnerId >= 0 ? GetProfile(p.PartnerId) : null;
            if (partner != null && partner.PartnerId == p.UnitId) { partner.PartnerId = -1; Log(sim, partner, $"mourns {p.FirstName}"); }
            if (sim.UnitRegistry.GetUnit(p.UnitId) == null) LeaveBuilding(sim, p, true);
            sim.AiCommandBuffer.EnqueueCommand(new DeleteUnitsCommand(PlayerId, Ids(p)));
        }

        // ------------------------------------------------------------------ ageing

        private void UpdateAge(GameSimulation sim, VillagerProfile p, UnitData unit, bool garrisoned)
        {
            int ageTicks = sim.CurrentTick - p.BirthTick;
            int childEnd = ChildDays * VillageClock.DayLengthTicks;
            int adultEnd = childEnd + AdultDays * VillageClock.DayLengthTicks;
            int elderEnd = adultEnd + ElderDays * VillageClock.DayLengthTicks;

            LifeStage stage = ageTicks < childEnd ? LifeStage.Child : ageTicks < adultEnd ? LifeStage.Adult : LifeStage.Elder;
            if (stage != p.Stage)
            {
                var old = p.Stage;
                p.Stage = stage;
                if (old == LifeStage.Child && stage == LifeStage.Adult) ComeOfAge(sim, p, unit, garrisoned);
                else if (stage == LifeStage.Elder) Log(sim, p, "is getting old", true);
            }
            if (ageTicks >= elderEnd) Kill(sim, p, "passed away of old age");
        }

        private void ComeOfAge(GameSimulation sim, VillagerProfile p, UnitData unit, bool garrisoned)
        {
            if (AdultJobSlots.Count == 0) { Log(sim, p, "came of age"); return; }
            var slot = AdultJobSlots[(int)(Next() % (uint)AdultJobSlots.Count)];
            p.Job = slot.Job;
            p.WorkplaceBuildingId = slot.WorkplaceId;
            p.PatrolBuildingId = slot.PatrolId;
            p.WorkNodeId = -1;
            p.GatherSlot = (int)(Next() % 4);
            VillageSchedules.Assign(p, ref rng);
            Log(sim, p, $"came of age and became a {VillageJobInfo.DisplayName(p.Job).ToLowerInvariant()}", true);
            if (p.Phase == RoutinePhase.Working) { unit = LeaveBuilding(sim, p, garrisoned) ?? unit; IssueWork(sim, p, unit); }
        }

        // ------------------------------------------------------------------ pairing & births

        private void PairingPass(GameSimulation sim)
        {
            plazaScratch.Clear();
            for (int i = 0; i < Profiles.Count; i++)
            {
                var p = Profiles[i];
                if (p.IsDead || p.Stage != LifeStage.Adult || p.PartnerId >= 0) continue;
                var u = sim.UnitRegistry.GetUnit(p.UnitId);
                if (u == null || u.State != UnitState.Idle || !IsNearPlaza(sim, u, 6)) continue;
                plazaScratch.Add(p);
            }
            // Match men with women who are at the square right now.
            for (int i = 0; i < plazaScratch.Count; i++)
            {
                var a = plazaScratch[i];
                if (a.PartnerId >= 0 || a.Gender != Gender.Male) continue;
                if (a.Has(Trait.Misogynist)) continue;
                for (int j = 0; j < plazaScratch.Count; j++)
                {
                    var b = plazaScratch[j];
                    if (b.PartnerId >= 0 || b.Gender != Gender.Female) continue;
                    if (a.HouseholdIndex == b.HouseholdIndex) continue; // not within the same household
                    int relAB = Relation(a.UnitId, b.UnitId);
                    if (relAB < 10) continue;                            // they need to know each other first
                    if (!Chance(30)) continue;                          // does he even go over?

                    // Courtship: she may say no (misandrists always do; friends rarely).
                    bool refused = b.Has(Trait.Misandrist) || Chance(relAB >= 40 ? 15 : 40);
                    if (refused)
                    {
                        a.RejectedCount++;
                        Remember(sim, a, b, -10, $"was turned down by {b.FirstName}", $"turned {a.FirstName} down");
                        Log(sim, a, $"asked {b.FullName} out and was turned down", true);
                        a.Social = Mathf.Max(0, a.Social - NeedMax / 4);
                        if (a.RejectedCount >= 2 && Chance(50) && !a.Has(Trait.Misogynist))
                            AddTrait(sim, a, Trait.Misogynist, "has been spurned once too often and now wants nothing to do with women");
                        break;
                    }

                    a.PartnerId = b.UnitId; b.PartnerId = a.UnitId;
                    a.PairedTick = b.PairedTick = sim.CurrentTick;
                    a.Social = b.Social = NeedMax;
                    Remember(sim, a, b, 40, $"fell in love with {b.FirstName}", $"fell in love with {a.FirstName}");
                    // B moves in with A.
                    b.HomeBuildingId = a.HomeBuildingId;
                    b.HouseholdIndex = a.HouseholdIndex;
                    Log(sim, a, $"and {b.FullName} became a couple ♥ — {b.FirstName} moved into the {a.FamilyName} house", true);
                    break;
                }
            }

            // Women can court too — and be turned down.
            for (int i = 0; i < plazaScratch.Count; i++)
            {
                var a = plazaScratch[i];
                if (a.PartnerId >= 0 || a.Gender != Gender.Female || a.Has(Trait.Misandrist)) continue;
                for (int j = 0; j < plazaScratch.Count; j++)
                {
                    var b = plazaScratch[j];
                    if (b.PartnerId >= 0 || b.Gender != Gender.Male || a.HouseholdIndex == b.HouseholdIndex) continue;
                    int relAB = Relation(a.UnitId, b.UnitId);
                    if (relAB < 10) continue;
                    if (!Chance(15)) continue;
                    bool refused = b.Has(Trait.Misogynist) || Chance(relAB >= 40 ? 15 : 40);
                    if (refused)
                    {
                        a.RejectedCount++;
                        Remember(sim, a, b, -10, $"was turned down by {b.FirstName}", $"turned {a.FirstName} down");
                        Log(sim, a, $"asked {b.FullName} out and was turned down", true);
                        a.Social = Mathf.Max(0, a.Social - NeedMax / 4);
                        if (a.RejectedCount >= 2 && Chance(50) && !a.Has(Trait.Misandrist))
                            AddTrait(sim, a, Trait.Misandrist, "has been spurned once too often and now wants nothing to do with men");
                        break;
                    }
                    a.PartnerId = b.UnitId; b.PartnerId = a.UnitId;
                    a.PairedTick = b.PairedTick = sim.CurrentTick;
                    a.Social = b.Social = NeedMax;
                    Remember(sim, a, b, 40, $"fell in love with {b.FirstName}", $"fell in love with {a.FirstName}");
                    a.HomeBuildingId = b.HomeBuildingId;
                    a.HouseholdIndex = b.HouseholdIndex;
                    Log(sim, a, $"and {b.FullName} became a couple ♥ — {a.FirstName} moved into the {b.FamilyName} house", true);
                    break;
                }
            }
        }

        /// <summary>Villager units that appeared without a profile are newborns from the spawn command.</summary>
        private void AdoptNewborns(GameSimulation sim)
        {
            if (pendingBirths.Count == 0) return;
            var units = sim.UnitRegistry.GetAllUnits();
            for (int i = 0; i < units.Count && pendingBirths.Count > 0; i++)
            {
                var u = units[i];
                if (!u.IsVillager || u.PlayerId != PlayerId || byUnitId.ContainsKey(u.Id)) continue;
                var birth = pendingBirths.Dequeue();
                var a = GetProfile(birth.ParentA);
                var b = GetProfile(birth.ParentB);
                var mult = Profiles.Count > 0 ? Profiles[0] : null;

                var gender = Chance(50) ? Gender.Female : Gender.Male;
                var names = gender == Gender.Female ? VillageNames.Female : VillageNames.Male;
                var child = new VillagerProfile
                {
                    UnitId = u.Id,
                    Gender = gender,
                    FirstName = names[Next() % (uint)names.Length],
                    FamilyName = birth.Family,
                    HouseholdIndex = birth.HouseholdIndex,
                    Job = VillageJob.Student,
                    HomeBuildingId = birth.HouseId,
                    WorkplaceBuildingId = UniversityBuildingId >= 0 ? UniversityBuildingId : birth.HouseId,
                    Money = MealPrice,
                    BirthTick = sim.CurrentTick,
                    Stage = LifeStage.Child,
                    Hunger = NeedMax, Energy = NeedMax, Social = NeedMax, Fun = NeedMax,
                    Phase = RoutinePhase.Sleeping,
                };
                // One or two innate traits, then a schedule that respects them.
                RollInnateTraits(child, Chance(50) ? 1 : 2);
                VillageSchedules.Assign(child, ref rng);
                // Match the village pace set for the first villager.
                if (mult != null)
                {
                    child.BaseMoveSpeed = mult.BaseMoveSpeed;
                    var refUnit = sim.UnitRegistry.GetUnit(mult.UnitId) ?? sim.UnitRegistry.GetGarrisonedUnit(mult.UnitId);
                    if (refUnit != null) u.CarryCapacity = refUnit.CarryCapacity;
                }
                else child.BaseMoveSpeed = u.MoveSpeed;
                AddProfile(child);
                ApplyPace(sim, child);
                if (a != null) a.Children++;
                if (b != null) b.Children++;
                Log(sim, child, $"was born to {(a != null ? a.FirstName : "?")} and {(b != null ? b.FirstName : "?")} {birth.Family} ({(gender == Gender.Female ? "a girl" : "a boy")})", true);
                // Straight to bed with the family.
                var house = sim.BuildingRegistry.GetBuilding(birth.HouseId);
                if (house != null) Enqueue(sim, child, new GarrisonCommand(PlayerId, Ids(child), house.Id));
            }
        }

        // ------------------------------------------------------------------ thoughts

        private void UpdateThought(GameSimulation sim, VillagerProfile p, UnitData unit, bool garrisoned)
        {
            if (p.IsDead) { p.Thought = ""; return; }

            if (p.PendingMeal != Meal.None) { p.Thought = "Hungry…"; return; }
            if (p.Errand == Errand.Eating) { p.Thought = p.WaitingForTable ? "Waiting for a table" : "Eating"; return; }
            if (p.Errand == Errand.Quirk) { p.Thought = p.Activity; return; }
            if (p.Errand == Errand.Gawk) { var t = GetProfile(p.GawkTargetId); p.Thought = t != null ? $"Gawking at {t.FirstName}" : "Gawking"; return; }
            if (p.Errand == Errand.Fight) { p.Thought = "⚔ Fighting!"; return; }
            if (p.Errand == Errand.Watch) { p.Thought = "Watching the fight"; return; }
            if (p.Errand == Errand.Flee) { p.Thought = SoldierIds.Count > 0 ? "AAAH! Raiders!" : "AAAH! Wolves!"; return; }
            if (p.Errand == Errand.Defend) { p.Thought = p.Mounted ? "⚔ Charging!" : p.Armed ? "⚔ Fighting" : "⚔ Fighting bare-handed"; return; }
            if (p.Errand == Errand.Arm) { p.Thought = "Getting a sword"; return; }
            if (p.Errand == Errand.Mount) { p.Thought = "Getting a horse"; return; }
            if (p.Errand == Errand.Dismount) { p.Thought = "Returning the horse"; return; }
            if (p.Errand == Errand.Tame) { p.Thought = p.TameProgress > 0 ? "Taming…" : "Approaching a horse"; return; }
            if (p.Errand == Errand.Lead) { p.Thought = "Leading a horse"; return; }
            if (p.Errand == Errand.Haul) { p.Thought = p.CarryingLoad ? "Hauling timber" : "Fetching timber"; return; }
            if (p.Errand == Errand.Build) { p.Thought = unit != null && unit.State == UnitState.Constructing ? "Building" : ""; return; }
            if (p.Errand == Errand.Bury) { p.Thought = p.CarryingLoad ? "Carrying a body" : "Fetching a body"; return; }
            if (p.Has(Trait.Sick) && unit != null && unit.State == UnitState.Idle) { p.Thought = "Feeling sick"; return; }
            if (p.PairedTick >= 0 && sim.CurrentTick - p.PairedTick < 900) { p.Thought = "♥"; return; }

            if (garrisoned)
            {
                int where = FindGarrisonBuilding(sim, p.UnitId);
                bool home = where == p.HomeBuildingId;
                if (p.Errand == Errand.Nap || (home && p.Phase == RoutinePhase.Sleeping)) { p.Thought = "Zz"; return; }
                if (where == TavernBuildingId && p.Job != VillageJob.Cook) { p.Thought = "♪ Drinking"; return; }
                if (p.Phase == RoutinePhase.Working) { p.Thought = WorkThought(p.Job); return; }
                if (p.Phase == RoutinePhase.Morning) { p.Thought = "☀ Waking"; return; }
                p.Thought = home ? (p.Crowded ? "☹ Too crowded" : "⌂ Resting") : "";
                return;
            }

            if (unit == null) { p.Thought = ""; return; }
            switch (unit.State)
            {
                case UnitState.Gathering: p.Thought = WorkThought(p.Job); return;
                case UnitState.MovingToDropoff: p.Thought = "Hauling"; return;
                case UnitState.Idle:
                    if (p.Phase == RoutinePhase.Lunch && p.HasEaten(Meal.Lunch)) { p.Thought = "Eating"; return; }
                    if (IsNearPlaza(sim, unit, 6))
                    {
                        var partner = p.PartnerId >= 0 ? GetProfile(p.PartnerId) : null;
                        var pu = partner != null ? sim.UnitRegistry.GetUnit(partner.UnitId) : null;
                        if (pu != null && IsNearPlaza(sim, pu, 6)) { p.Thought = "♥"; return; }
                        var chat = p.ChatPartnerId >= 0 ? GetProfile(p.ChatPartnerId) : null;
                        if (p.Stage == LifeStage.Child) { p.Thought = chat != null ? $"♪ Playing with {chat.FirstName}" : "♪ Playing"; return; }
                        p.Thought = chat != null ? $"☺ Chatting with {chat.FirstName}" : "☺ Waiting for company";
                        return;
                    }
                    if (p.Job == VillageJob.Guard && p.Phase == RoutinePhase.Working) { p.Thought = "Guarding"; return; }
                    // Idle and alone: sometimes a memory surfaces.
                    if ((sim.CurrentTick / 240 + p.UnitId) % 5 == 0 && StrongestMemory(p, sim.CurrentTick, out var mem))
                    {
                        var who = GetProfile(mem.OtherId);
                        p.Thought = who == null ? "" : mem.Delta < 0 ? $"☹ Still angry at {who.FirstName}" : $"☺ Fond of {who.FirstName}";
                        return;
                    }
                    p.Thought = "";
                    return;
                default:
                    p.Thought = ""; // walking
                    return;
            }
        }

        private static string WorkThought(VillageJob job)
        {
            switch (job)
            {
                case VillageJob.Forester: return "Chopping";
                case VillageJob.Miner: return "Mining";
                case VillageJob.Quarryman: return "Quarrying";
                case VillageJob.Farmer: return "Farming";
                case VillageJob.Forager: return "Foraging";
                case VillageJob.Blacksmith: return "Smithing";
                case VillageJob.Student: return "Studying";
                case VillageJob.Merchant: return "Trading";
                case VillageJob.Monk: return "Praying";
                case VillageJob.Guard: return "Guarding";
                case VillageJob.Cook: return "Cooking";
                default: return "Working";
            }
        }

        // ------------------------------------------------------------------ actions

        private UnitData LeaveBuilding(GameSimulation sim, VillagerProfile p, bool garrisoned)
        {
            if (!garrisoned) return null;
            int buildingId = FindGarrisonBuilding(sim, p.UnitId);
            if (buildingId < 0) return null;
            if (!sim.UngarrisonUnit(buildingId, p.UnitId)) return null;
            return sim.UnitRegistry.GetUnit(p.UnitId);
        }

        private void GoHome(GameSimulation sim, VillagerProfile p, UnitData unit, bool garrisoned)
        {
            if (garrisoned)
            {
                int where = FindGarrisonBuilding(sim, p.UnitId);
                if (where == p.HomeBuildingId) return;
                unit = LeaveBuilding(sim, p, true);
                if (unit == null) return;
            }
            if (unit == null) return;
            var home = sim.BuildingRegistry.GetBuilding(p.HomeBuildingId);
            if (home == null || home.IsDestroyed) return;
            Enqueue(sim, p, new GarrisonCommand(PlayerId, Ids(p), home.Id));
        }

        private void GoToPlaza(GameSimulation sim, VillagerProfile p, UnitData unit)
        {
            if (unit == null) return;
            int k = p.UnitId;
            int dx = (k * 7) % 7 - 3;
            int dz = (k * 11) % 7 - 3;
            var target = new FixedVector3(PlazaPosition.x + Fixed32.FromInt(dx), Fixed32.Zero, PlazaPosition.z + Fixed32.FromInt(dz));
            var tile = sim.MapData.WorldToTile(target);
            tile = GridPathfinder.FindNearestWalkableTile(sim.MapData, tile, 6);
            Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(tile.x, tile.y)));
        }

        private void IssueWork(GameSimulation sim, VillagerProfile p, UnitData unit)
        {
            if (unit == null) return;
            var workplace = sim.BuildingRegistry.GetBuilding(p.WorkplaceBuildingId);

            switch (VillageJobInfo.Kind(p.Job))
            {
                case JobKind.Gather:
                {
                    int nodeId = p.WorkNodeId;
                    if (nodeId >= 0)
                    {
                        var owned = sim.MapData.GetResourceNode(nodeId);
                        if (owned == null || owned.IsDepleted) nodeId = -1;
                    }
                    if (nodeId < 0)
                    {
                        var anchor = workplace != null ? workplace.SimPosition : unit.SimPosition;
                        nodeId = FindGatherNode(sim, VillageJobInfo.Resource(p.Job), anchor, p.GatherSlot);
                    }
                    if (nodeId < 0) { p.Activity = "Looking for work"; return; }
                    p.Activity = WorkVerb(p.Job);
                    Enqueue(sim, p, new GatherCommand(PlayerId, Ids(p), nodeId));
                    break;
                }
                case JobKind.Indoor:
                {
                    if (workplace == null || workplace.IsDestroyed) return;
                    p.Activity = WorkVerb(p.Job);
                    Enqueue(sim, p, new GarrisonCommand(PlayerId, Ids(p), workplace.Id));
                    break;
                }
                case JobKind.Patrol:
                {
                    var post = sim.BuildingRegistry.GetBuilding(p.PatrolBuildingId);
                    if (post == null && workplace == null) return;
                    p.Activity = "On patrol";
                    if (workplace != null)
                    {
                        var startTile = DoorTile(sim, workplace);
                        Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(startTile.x, startTile.y)));
                    }
                    if (post != null)
                    {
                        var endTile = DoorTile(sim, post);
                        var patrol = new PatrolCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(endTile.x, endTile.y));
                        patrol.IsQueued = workplace != null;
                        Enqueue(sim, p, patrol);
                    }
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ helpers

        private void Enqueue(GameSimulation sim, VillagerProfile p, ICommand cmd)
        {
            p.LastCommandTick = sim.CurrentTick;
            sim.AiCommandBuffer.EnqueueCommand(cmd);
        }

        private static int[] Ids(VillagerProfile p) => new[] { p.UnitId };

        private uint Next() => VillageSchedules.Next(ref rng);
        private bool Chance(int percent) => VillageSchedules.Chance(ref rng, percent);

        private static int FindGarrisonBuilding(GameSimulation sim, int unitId)
        {
            var buildings = sim.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
                if (buildings[i].GarrisonedUnitIds.Contains(unitId))
                    return buildings[i].Id;
            return -1;
        }

        /// <summary>Walkable tile just outside a building's south face (its "door").</summary>
        public static Vector2Int DoorTile(GameSimulation sim, BuildingData b)
        {
            var tile = new Vector2Int(b.OriginTileX + b.TileFootprintWidth / 2, b.OriginTileZ - 1);
            return GridPathfinder.FindNearestWalkableTile(sim.MapData, tile, 6);
        }

        private int FindGatherNode(GameSimulation sim, ResourceType type, FixedVector3 anchor, int slot)
        {
            var at = sim.MapData.WorldToTile(anchor);
            nodeScratch.Clear();
            var nodes = sim.MapData.GetAllResourceNodes();
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n.Type != type || n.IsDepleted || n.IsFarmNode || n.IsCarcass) continue;
                int dx = n.TileX - at.x, dz = n.TileZ - at.y;
                if (dx * dx + dz * dz > 40 * 40) continue;
                nodeScratch.Add(n);
            }
            if (nodeScratch.Count == 0) return -1;
            nodeScratch.Sort((a, b) =>
            {
                int da = (a.TileX - at.x) * (a.TileX - at.x) + (a.TileZ - at.y) * (a.TileZ - at.y);
                int db = (b.TileX - at.x) * (b.TileX - at.x) + (b.TileZ - at.y) * (b.TileZ - at.y);
                return da != db ? da.CompareTo(db) : a.Id.CompareTo(b.Id);
            });
            int spread = Mathf.Min(nodeScratch.Count, 4);
            return nodeScratch[slot % spread].Id;
        }

        private static string WorkVerbPast(VillageJob job, string workplace)
        {
            switch (job)
            {
                case VillageJob.Forester: return "set off to cut timber near " + workplace;
                case VillageJob.Miner: return "set off to mine gold at " + workplace;
                case VillageJob.Quarryman: return "set off to quarry stone at " + workplace;
                case VillageJob.Farmer: return "went out to tend the farm";
                case VillageJob.Forager: return "went out picking berries for " + workplace;
                case VillageJob.Blacksmith: return "opened up " + workplace;
                case VillageJob.Student: return "went to class at " + workplace;
                case VillageJob.Merchant: return "opened a stall at " + workplace;
                case VillageJob.Monk: return "began prayers at " + workplace;
                case VillageJob.Guard: return "started patrol from " + workplace;
                case VillageJob.Cook: return "lit the stoves at " + workplace;
                default: return "went to work";
            }
        }

        private static string WorkVerb(VillageJob job)
        {
            switch (job)
            {
                case VillageJob.Forester: return "Cutting timber";
                case VillageJob.Miner: return "Mining gold";
                case VillageJob.Quarryman: return "Quarrying stone";
                case VillageJob.Farmer: return "Tending the farm";
                case VillageJob.Forager: return "Picking berries";
                case VillageJob.Blacksmith: return "Working the forge";
                case VillageJob.Student: return "Studying at the university";
                case VillageJob.Merchant: return "Trading at the market";
                case VillageJob.Monk: return "Praying at the monastery";
                case VillageJob.Guard: return "On patrol";
                case VillageJob.Cook: return "Cooking at the tavern";
                default: return "Working";
            }
        }
    }
}
