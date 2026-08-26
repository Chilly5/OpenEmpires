using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires.Village
{
    /// <summary>
    /// Threats and fighting. Everything hostile goes through the real Open Empires combat
    /// system: wolves and raiders are units of a hostile player and are given
    /// <see cref="AttackUnitCommand"/>s; defending villagers attack back the same way, so
    /// damage, chasing, animations and the health bars are the RTS's own.
    ///
    /// Villagers train where the village lets them: a Barracks makes soldiers, an Archery Range
    /// makes archers, the Stables (with a tamed horse) makes knights; with none of those, the
    /// blacksmith hands out a sword (militia). Wild horses roam and can be tamed and led to the stables.
    /// </summary>
    public partial class VillageRoutineSystem
    {
        public const int HostilePlayerId = 1;

        public int BarracksBuildingId = -1;
        public int ArcheryRangeBuildingId = -1;
        public int RaidCount;
        public int WolfAttackCount;

        // ================================================================== threats (wolves + raiders)

        public readonly List<int> SoldierIds = new List<int>();
        public int RaidStartTick = -1;
        private int raidEndTick = -1;
        private int pendingSoldierSpawns;
        private int lastRaidDay = -1;
        private readonly Dictionary<int, int> hostileLastOrder = new Dictionary<int, int>();

        public bool ThreatActive => WolfIds.Count > 0 || SoldierIds.Count > 0 || pendingWolfSpawns > 0 || pendingSoldierSpawns > 0;
        public int ThreatStartTick => Mathf.Max(WolfAttackStartTick, RaidStartTick);

        private UnitData NearestThreat(GameSimulation sim, FixedVector3 from, int tiles)
        {
            var a = sim.MapData.WorldToTile(from);
            UnitData best = null; int bestD = tiles * tiles + 1;
            for (int pass = 0; pass < 2; pass++)
            {
                var list = pass == 0 ? WolfIds : SoldierIds;
                for (int i = 0; i < list.Count; i++)
                {
                    var u = sim.UnitRegistry.GetUnit(list[i]);
                    if (u == null || u.CurrentHealth <= 0 || u.State == UnitState.Dead) continue;
                    var b = sim.MapData.WorldToTile(u.SimPosition);
                    int dx = a.x - b.x, dz = a.y - b.y, d = dx * dx + dz * dz;
                    if (d < bestD) { bestD = d; best = u; }
                }
            }
            return best;
        }

        private void HostileOrder(GameSimulation sim, UnitData hostile, UnitData target, int everyTicks)
        {
            hostileLastOrder.TryGetValue(hostile.Id, out int last);
            if (sim.CurrentTick - last < everyTicks) return;
            hostileLastOrder[hostile.Id] = sim.CurrentTick;
            if (hostile.CombatTargetId == target.Id && hostile.State == UnitState.InCombat) return;
            sim.AiCommandBuffer.EnqueueCommand(new AttackUnitCommand(HostilePlayerId, new[] { hostile.Id }, target.Id));
        }

        // ------------------------------------------------------------------ raids

        private void RaidPass(GameSimulation sim, int minute, int day)
        {
            int tick = sim.CurrentTick;

            if (pendingSoldierSpawns > 0)
            {
                var units = sim.UnitRegistry.GetAllUnits();
                for (int i = 0; i < units.Count && pendingSoldierSpawns > 0; i++)
                {
                    var u = units[i];
                    if (u.PlayerId != HostilePlayerId || WolfIds.Contains(u.Id) || SoldierIds.Contains(u.Id) || u.UnitType != 1) continue;
                    SoldierIds.Add(u.Id);
                    pendingSoldierSpawns--;
                }
            }

            if (SoldierIds.Count == 0 && pendingSoldierSpawns == 0)
            {
                if (day != lastRaidDay && minute >= 14 * 60 && minute < 16 * 60)
                {
                    lastRaidDay = day;
                    int chance = VillageClock.SeasonOf(tick) == VillageClock.Season.Autumn ? 30 : 15;
                    if (day < 3 || !Chance(chance)) return; // the first days are peaceful
                    int count = 3 + (int)(Next() % 3);
                    float angle = (Next() % 360) * Mathf.Deg2Rad;
                    var center = sim.MapData.WorldToTile(PlazaPosition);
                    var edge = new Vector2Int(center.x + Mathf.RoundToInt(Mathf.Cos(angle) * 28f), center.y + Mathf.RoundToInt(Mathf.Sin(angle) * 28f));
                    edge = GridPathfinder.FindNearestWalkableTile(sim.MapData, edge, 8);
                    sim.AiCommandBuffer.EnqueueCommand(new CheatSpawnUnitCommand(PlayerId, 1, sim.MapData.TileToWorldFixed(edge.x, edge.y), count, HostilePlayerId));
                    pendingSoldierSpawns = count;
                    RaidStartTick = tick;
                    raidEndTick = tick + 2400;
                    RaidCount++;
                    foreach (var p in Profiles) p.WolfDecisionTick = -1;
                    LogEvent(sim, $"⚔ Raiders! A band of {count} soldiers is marching on the village!");
                }
                return;
            }

            for (int i = SoldierIds.Count - 1; i >= 0; i--)
            {
                int id = SoldierIds[i];
                var s = sim.UnitRegistry.GetUnit(id);
                if (s == null || s.State == UnitState.Dead || s.CurrentHealth <= 0)
                {
                    SoldierIds.RemoveAt(i);
                    LogEvent(sim, "⚔ A raider was cut down");
                    if (s != null) CreditDefenders(sim, s.SimPosition, "a raider");
                    continue;
                }
                if (tick >= raidEndTick)
                {
                    sim.AiCommandBuffer.EnqueueCommand(new DeleteUnitsCommand(HostilePlayerId, new[] { id }));
                    SoldierIds.RemoveAt(i);
                    continue;
                }
                var victim = NearestVillagerUnit(sim, s.SimPosition, 60, out _);
                if (victim != null) HostileOrder(sim, s, victim, 60);
            }

            if (SoldierIds.Count == 0 && pendingSoldierSpawns == 0)
                LogEvent(sim, tick >= raidEndTick ? "⚔ The raiders withdrew" : "⚔ The raiders were beaten back!");
        }

        // ------------------------------------------------------------------ reactions & training

        private bool ReactToThreat(GameSimulation sim, VillagerProfile p, UnitData unit)
        {
            if (p.Errand == Errand.Flee || p.Errand == Errand.Defend || p.Errand == Errand.Arm || p.Errand == Errand.Mount) return false;
            var threat = NearestThreat(sim, unit.SimPosition, 16);
            if (threat == null) return false;

            if (p.WolfDecisionTick < ThreatStartTick)
            {
                p.WolfDecisionTick = sim.CurrentTick;
                bool canTrain = TrainingBuilding(sim, p, out _) != null;
                p.WillDefend = p.Stage == LifeStage.Adult && !p.Has(Trait.BrokenLeg)
                               && (p.Job == VillageJob.Guard || p.Has(Trait.Brave) || p.Military != MilitaryKind.None || (canTrain && Chance(20)));
            }

            if (p.PendingMeal != Meal.None) { p.PendingMeal = Meal.None; p.PhaseBeginPending = false; }
            ReleaseErrand(sim, p);
            p.ErrandStartTick = sim.CurrentTick;
            if (p.WillDefend)
            {
                p.DefendStage = 0;
                Log(sim, p, "stood their ground against the attackers", true);
                AdvanceDefend(sim, p, unit);
            }
            else
            {
                p.Errand = Errand.Flee;
                p.Activity = "Running from the attackers";
                Log(sim, p, "fled screaming");
                GoHome(sim, p, unit, garrisoned: false);
            }
            return true;
        }

        /// <summary>Where this villager would go to get equipped, and what they'd become.</summary>
        private BuildingData TrainingBuilding(GameSimulation sim, VillagerProfile p, out MilitaryKind kind)
        {
            kind = MilitaryKind.None;
            var barracks = Usable(sim, BarracksBuildingId);
            var range = Usable(sim, ArcheryRangeBuildingId);
            var smith = Usable(sim, BlacksmithBuildingId);
            bool prefersBow = p.Has(Trait.Curious) || p.Has(Trait.Introvert) || p.Gender == Gender.Female && !p.Has(Trait.Brave);
            if (range != null && (prefersBow || barracks == null) && (p.Military == MilitaryKind.None || p.Military == MilitaryKind.Archer)) { kind = MilitaryKind.Archer; return range; }
            if (barracks != null) { kind = MilitaryKind.Soldier; return barracks; }
            if (range != null) { kind = MilitaryKind.Archer; return range; }
            if (smith != null) { kind = MilitaryKind.Militia; return smith; }
            return null;
        }

        private BuildingData Usable(GameSimulation sim, int id)
        {
            var b = sim.BuildingRegistry.GetBuilding(id);
            return b != null && !b.IsDestroyed && !b.IsUnderConstruction ? b : null;
        }

        /// <summary>Equip (barracks / archery range / blacksmith) → mount (stables) → fight; skipping what isn't possible.</summary>
        private void AdvanceDefend(GameSimulation sim, VillagerProfile p, UnitData unit)
        {
            if (unit == null) return;
            p.ErrandStartTick = sim.CurrentTick;
            if (p.DefendStage == 0)
            {
                p.DefendStage = 1;
                var where = TrainingBuilding(sim, p, out var kind);
                bool needsTraining = where != null && (p.Military == MilitaryKind.None || (kind != MilitaryKind.Militia && kind != p.Military && p.Military == MilitaryKind.Militia));
                if (needsTraining)
                {
                    p.Errand = Errand.Arm;
                    p.Activity = kind == MilitaryKind.Archer ? "Fetching a bow at the archery range" : kind == MilitaryKind.Soldier ? "Kitting up at the barracks" : "Grabbing a sword at the blacksmith";
                    var d = DoorTile(sim, where);
                    Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(d.x, d.y)));
                    return;
                }
            }
            if (p.DefendStage == 1)
            {
                p.DefendStage = 2;
                var stables = Usable(sim, StablesBuildingId);
                bool rider = !p.Mounted && StablesHorses > 0 && stables != null
                             && (p.Military == MilitaryKind.Soldier || p.Military == MilitaryKind.Militia || p.Military == MilitaryKind.Knight)
                             && (p.Job == VillageJob.Guard || p.Has(Trait.Brave) || p.Military == MilitaryKind.Knight);
                if (rider)
                {
                    p.Errand = Errand.Mount;
                    p.Activity = "Fetching a horse from the stables";
                    var d = DoorTile(sim, stables);
                    Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(d.x, d.y)));
                    return;
                }
            }
            p.Errand = Errand.Defend;
            p.Activity = p.Mounted ? "Riding down the attackers" : p.Military == MilitaryKind.Archer ? "Shooting at the attackers" : p.Armed ? "Fighting with weapon in hand" : "Fighting bare-handed";
        }

        private bool HandleArm(GameSimulation sim, VillagerProfile p, UnitData unit)
        {
            var where = TrainingBuilding(sim, p, out var kind);
            if (unit == null || where == null) { AdvanceDefend(sim, p, unit); return false; }
            var at = sim.MapData.WorldToTile(unit.SimPosition);
            var d = DoorTile(sim, where);
            int dx = at.x - d.x, dz = at.y - d.y;
            if (dx * dx + dz * dz <= 2 * 2)
            {
                if (sim.CurrentTick - p.ErrandStartTick < 60) return false; // a moment to kit up
                Train(sim, p, unit, kind);
                AdvanceDefend(sim, p, unit);
                return false;
            }
            if (unit.State == UnitState.Idle && sim.CurrentTick - p.LastCommandTick > CommandCooldown)
                Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(d.x, d.y)));
            if (sim.CurrentTick - p.ErrandStartTick > 900) AdvanceDefend(sim, p, unit);
            return false;
        }

        /// <summary>Become a soldier / archer / militia: permanent, and it changes the unit's real combat stats.</summary>
        public void Train(GameSimulation sim, VillagerProfile p, UnitData unit, MilitaryKind kind)
        {
            p.Military = kind;
            p.Armed = true;
            switch (kind)
            {
                case MilitaryKind.Soldier: Log(sim, p, "trained as a soldier at the barracks", true); break;
                case MilitaryKind.Archer: Log(sim, p, "trained as an archer at the archery range", true); break;
                default: Log(sim, p, "took up a sword at the blacksmith", true); break;
            }
            if (unit == null) unit = sim.UnitRegistry.GetUnit(p.UnitId) ?? sim.UnitRegistry.GetGarrisonedUnit(p.UnitId);
            if (unit != null) ApplyCombatStats(sim, p, unit);
        }

        private void ApplyCombatStats(GameSimulation sim, VillagerProfile p, UnitData unit)
        {
            var c = sim.Config;
            switch (p.Mounted ? MilitaryKind.Knight : p.Military)
            {
                case MilitaryKind.Knight:
                    unit.AttackDamage = 14; unit.MeleeArmor = 3; unit.RangedArmor = 1; unit.IsRanged = false; unit.AttackRange = c.ConfigToFixed32Safe(1.2f);
                    unit.MaxHealth = Mathf.Max(unit.MaxHealth, 90); break;
                case MilitaryKind.Soldier:
                    unit.AttackDamage = 9; unit.MeleeArmor = 2; unit.RangedArmor = 1; unit.IsRanged = false; unit.AttackRange = c.ConfigToFixed32Safe(1.2f);
                    unit.MaxHealth = Mathf.Max(unit.MaxHealth, 70); break;
                case MilitaryKind.Archer:
                    unit.AttackDamage = 6; unit.MeleeArmor = 0; unit.RangedArmor = 0; unit.IsRanged = true; unit.AttackRange = c.ConfigToFixed32Safe(5f); break;
                case MilitaryKind.Militia:
                    unit.AttackDamage = 6; unit.MeleeArmor = 1; unit.IsRanged = false; unit.AttackRange = c.ConfigToFixed32Safe(1.2f); break;
                default:
                    unit.AttackDamage = c.VillagerAttackDamage; unit.MeleeArmor = c.VillagerMeleeArmor; unit.RangedArmor = c.VillagerRangedArmor; unit.IsRanged = false;
                    unit.AttackRange = c.ConfigToFixed32Safe(c.VillagerAttackRange); break;
            }
            unit.CurrentHealth = Mathf.Min(unit.CurrentHealth, unit.MaxHealth);
            ApplyPace(sim, p);
        }

        private bool HandleMount(GameSimulation sim, VillagerProfile p, UnitData unit)
        {
            var stables = Usable(sim, StablesBuildingId);
            if (unit == null || stables == null || StablesHorses <= 0) { AdvanceDefend(sim, p, unit); return false; }
            var at = sim.MapData.WorldToTile(unit.SimPosition);
            var d = DoorTile(sim, stables);
            int dx = at.x - d.x, dz = at.y - d.y;
            if (dx * dx + dz * dz <= 2 * 2)
            {
                StablesHorses--;
                p.Mounted = true;
                p.Military = MilitaryKind.Knight;
                ApplyCombatStats(sim, p, unit);
                Log(sim, p, "rode out from the stables as a knight", true);
                AdvanceDefend(sim, p, unit);
                return false;
            }
            if (unit.State == UnitState.Idle && sim.CurrentTick - p.LastCommandTick > CommandCooldown)
                Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(d.x, d.y)));
            if (sim.CurrentTick - p.ErrandStartTick > 900) AdvanceDefend(sim, p, unit);
            return false;
        }

        private bool HandleDismount(GameSimulation sim, VillagerProfile p, UnitData unit)
        {
            var stables = sim.BuildingRegistry.GetBuilding(StablesBuildingId);
            if (unit == null || stables == null) { p.Mounted = false; if (unit != null) ApplyCombatStats(sim, p, unit); return true; }
            var at = sim.MapData.WorldToTile(unit.SimPosition);
            var d = DoorTile(sim, stables);
            int dx = at.x - d.x, dz = at.y - d.y;
            if (dx * dx + dz * dz <= 2 * 2)
            {
                StablesHorses = Mathf.Min(StableCapacity, StablesHorses + 1);
                p.Mounted = false;
                ApplyCombatStats(sim, p, unit);
                Log(sim, p, "stabled the horse");
                return true;
            }
            if (unit.State == UnitState.Idle && sim.CurrentTick - p.LastCommandTick > CommandCooldown)
                Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(d.x, d.y)));
            return sim.CurrentTick - p.ErrandStartTick > 1200;
        }

        private bool HandleDefend(GameSimulation sim, VillagerProfile p, UnitData unit)
        {
            if (unit == null) return true;
            if (!ThreatActive)
            {
                if (p.Mounted) { p.Errand = Errand.Dismount; p.ErrandStartTick = sim.CurrentTick; p.Activity = "Returning the horse"; return false; }
                return true;
            }
            var threat = NearestThreat(sim, unit.SimPosition, 45);
            if (threat == null) return false;
            bool engaged = unit.State == UnitState.InCombat && unit.CombatTargetId == threat.Id;
            if (!engaged && sim.CurrentTick - p.LastCommandTick > 40)
                Enqueue(sim, p, new AttackUnitCommand(PlayerId, Ids(p), threat.Id));
            if (unit.CurrentHealth < unit.MaxHealth / 4 && !p.Has(Trait.Brave))
            {
                p.Errand = Errand.Flee; p.ErrandStartTick = sim.CurrentTick; p.Activity = "Retreating, wounded";
                Log(sim, p, "fell back badly wounded");
                GoHome(sim, p, unit, garrisoned: false);
            }
            return false;
        }

        // ================================================================== wild horses & the stables

        public int StablesBuildingId = -1;
        public int BlacksmithBuildingId = -1;
        public int StablesHorses;
        public const int StableCapacity = 4;
        public readonly List<int> HorseIds = new List<int>();
        private int pendingHorseSpawns;
        private bool horsesSeeded;
        private int lastHorseHerdDay = -1;
        private readonly Dictionary<int, int> horseLastWander = new Dictionary<int, int>();
        private readonly HashSet<int> horsesBeingTamed = new HashSet<int>();

        private void HorsePass(GameSimulation sim)
        {
            int tick = sim.CurrentTick;
            int day = VillageClock.Day(tick);
            // Seed a herd at the start; a new herd wanders in every few days if the wild ones are gone.
            if ((!horsesSeeded && tick > 30) || (HorseIds.Count == 0 && pendingHorseSpawns == 0 && day != lastHorseHerdDay && day % 4 == 0))
            {
                horsesSeeded = true;
                lastHorseHerdDay = day;
                int count = 3 + (int)(Next() % 2);
                var center = sim.MapData.WorldToTile(PlazaPosition);
                var spot = GridPathfinder.FindNearestWalkableTile(sim.MapData, new Vector2Int(center.x - 22, center.y + 18), 8);
                sim.AiCommandBuffer.EnqueueCommand(new CheatSpawnUnitCommand(PlayerId, 4, sim.MapData.TileToWorldFixed(spot.x, spot.y), count, PlayerId));
                pendingHorseSpawns = count;
                LogEvent(sim, "🐎 Wild horses have been seen grazing beyond the village");
            }
            if (pendingHorseSpawns > 0)
            {
                var units = sim.UnitRegistry.GetAllUnits();
                for (int i = 0; i < units.Count && pendingHorseSpawns > 0; i++)
                {
                    var u = units[i];
                    if (u.PlayerId != PlayerId || u.UnitType != 4 || HorseIds.Contains(u.Id) || byUnitId.ContainsKey(u.Id)) continue;
                    HorseIds.Add(u.Id);
                    u.MoveSpeed = Fixed32.FromFloat(1.2f);
                    pendingHorseSpawns--;
                }
            }

            for (int i = HorseIds.Count - 1; i >= 0; i--)
            {
                int id = HorseIds[i];
                var h = sim.UnitRegistry.GetUnit(id);
                if (h == null || h.State == UnitState.Dead) { HorseIds.RemoveAt(i); horsesBeingTamed.Remove(id); continue; }
                if (horsesBeingTamed.Contains(id)) continue;
                horseLastWander.TryGetValue(id, out int last);
                if (tick - last < 300 + (id * 37) % 200 || h.State != UnitState.Idle) continue;
                horseLastWander[id] = tick;
                var at = sim.MapData.WorldToTile(h.SimPosition);
                var t = GridPathfinder.FindNearestWalkableTile(sim.MapData, new Vector2Int(at.x + (int)(Next() % 9) - 4, at.y + (int)(Next() % 9) - 4), 4);
                sim.AiCommandBuffer.EnqueueCommand(new MoveCommand(PlayerId, new[] { id }, sim.MapData.TileToWorldFixed(t.x, t.y)));
            }

            if (tick % 150 == 75) TamePass(sim);
        }

        private void TamePass(GameSimulation sim)
        {
            if (ThreatActive || StablesHorses >= StableCapacity) return;
            var stables = Usable(sim, StablesBuildingId);
            if (stables == null) return;
            int freeHorse = -1;
            foreach (var id in HorseIds) if (!horsesBeingTamed.Contains(id) && sim.UnitRegistry.GetUnit(id) != null) { freeHorse = id; break; }
            if (freeHorse < 0) return;
            foreach (var p in Profiles)
            {
                if (p.IsDead || p.Stage != LifeStage.Adult || p.Errand != Errand.None || p.PendingMeal != Meal.None || p.Phase != RoutinePhase.Working || p.IsBuilder) continue;
                if (p.Job != VillageJob.Guard && p.Job != VillageJob.Forester && p.Job != VillageJob.Farmer) continue;
                var u = sim.UnitRegistry.GetUnit(p.UnitId);
                if (u == null) continue;
                p.Errand = Errand.Tame;
                p.ErrandStartTick = sim.CurrentTick;
                p.HorseTargetId = freeHorse;
                p.TameProgress = 0;
                horsesBeingTamed.Add(freeHorse);
                p.Activity = "Out to tame a wild horse";
                Log(sim, p, "went out to tame a wild horse");
                var h = sim.UnitRegistry.GetUnit(freeHorse);
                Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), h.SimPosition));
                return;
            }
        }

        private bool HandleTame(GameSimulation sim, VillagerProfile p, UnitData unit)
        {
            var horse = sim.UnitRegistry.GetUnit(p.HorseTargetId);
            if (unit == null || horse == null || ThreatActive) { horsesBeingTamed.Remove(p.HorseTargetId); return true; }
            if (sim.CurrentTick - p.ErrandStartTick > 2400) { horsesBeingTamed.Remove(p.HorseTargetId); Log(sim, p, "gave up on the horse"); return true; }

            if (TileDistanceSq(sim, unit, horse) <= 2)
            {
                p.TameProgress++;
                if (p.TameProgress >= 150)
                {
                    p.TameProgress = 0;
                    if (Chance(70))
                    {
                        p.Errand = Errand.Lead;
                        p.ErrandStartTick = sim.CurrentTick;
                        p.Activity = "Leading the horse to the stables";
                        Log(sim, p, "tamed a wild horse and is leading it to the stables", true);
                        var stables = sim.BuildingRegistry.GetBuilding(StablesBuildingId);
                        if (stables != null) { var d = DoorTile(sim, stables); Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(d.x, d.y))); }
                    }
                    else
                    {
                        Log(sim, p, "was thrown — the horse bolted");
                        var at = sim.MapData.WorldToTile(horse.SimPosition);
                        var t = GridPathfinder.FindNearestWalkableTile(sim.MapData, new Vector2Int(at.x + (int)(Next() % 13) - 6, at.y + (int)(Next() % 13) - 6), 5);
                        sim.AiCommandBuffer.EnqueueCommand(new MoveCommand(PlayerId, new[] { horse.Id }, sim.MapData.TileToWorldFixed(t.x, t.y)));
                    }
                }
            }
            else if (unit.State == UnitState.Idle && sim.CurrentTick - p.LastCommandTick > 30)
                Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), horse.SimPosition));
            return false;
        }

        private bool HandleLead(GameSimulation sim, VillagerProfile p, UnitData unit)
        {
            var horse = sim.UnitRegistry.GetUnit(p.HorseTargetId);
            var stables = sim.BuildingRegistry.GetBuilding(StablesBuildingId);
            if (unit == null || horse == null || stables == null) { horsesBeingTamed.Remove(p.HorseTargetId); return true; }
            var d = DoorTile(sim, stables);
            var at = sim.MapData.WorldToTile(unit.SimPosition);
            int dx = at.x - d.x, dz = at.y - d.y;
            if ((sim.CurrentTick + p.UnitId) % 20 == 0 && TileDistanceSq(sim, unit, horse) > 1)
                sim.AiCommandBuffer.EnqueueCommand(new MoveCommand(PlayerId, new[] { horse.Id }, unit.SimPosition));
            if (dx * dx + dz * dz <= 2 * 2 && TileDistanceSq(sim, unit, horse) <= 3 * 3)
            {
                sim.AiCommandBuffer.EnqueueCommand(new DeleteUnitsCommand(PlayerId, new[] { horse.Id }));
                HorseIds.Remove(horse.Id);
                horsesBeingTamed.Remove(horse.Id);
                StablesHorses++;
                p.Money += 10;
                Log(sim, p, $"put a horse in the stables ({StablesHorses}/{StableCapacity})", true);
                return true;
            }
            if (unit.State == UnitState.Idle && sim.CurrentTick - p.LastCommandTick > CommandCooldown)
                Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(d.x, d.y)));
            if (sim.CurrentTick - p.ErrandStartTick > 2400) { horsesBeingTamed.Remove(p.HorseTargetId); return true; }
            return false;
        }
    }

    internal static class ConfigFixedExt
    {
        /// <summary>Float→Fixed32 for combat ranges (same conversion the sim uses for config values).</summary>
        public static Fixed32 ConfigToFixed32Safe(this SimulationConfig _, float v) => Fixed32.FromFloat(v);
    }
}
