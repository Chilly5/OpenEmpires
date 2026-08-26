using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires.Village
{
    /// <summary>
    /// Village-scale systems: births, the dead and their burial, construction projects
    /// (villagers haul timber and build), and wolf attacks.
    /// </summary>
    public partial class VillageRoutineSystem
    {
        // ================================================================== errand bookkeeping

        /// <summary>Clear an errand and undo anything it was holding (a body, a load of timber…).</summary>
        private void ReleaseErrand(GameSimulation sim, VillagerProfile p)
        {
            if (p.Errand == Errand.Bury && p.BuryCorpseId >= 0)
            {
                var c = FindCorpse(p.BuryCorpseId);
                if (c != null) c.CarrierId = -1;
            }
            p.BuryCorpseId = -1;
            p.CarryingLoad = false;
            p.WaitingForTable = false;
            p.Errand = Errand.None;
        }

        // ================================================================== seasons

        private VillageClock.Season lastSeason = (VillageClock.Season)(-1);

        /// <summary>Harvest yield per season, percent (applied to every food delivery).</summary>
        public static int HarvestPercent(VillageClock.Season s) => s == VillageClock.Season.Spring ? 130 : s == VillageClock.Season.Summer ? 200 : s == VillageClock.Season.Autumn ? 170 : 40;

        /// <summary>Meal price rises when the store runs low and falls when it overflows.</summary>
        public int CurrentMealPrice(GameSimulation sim)
        {
            int food = sim.ResourceManager.GetPlayerResources(PlayerId).Food;
            if (food < 40) return MealPrice * 2;
            if (food < 100) return MealPrice * 3 / 2;
            if (food > 400) return Mathf.Max(1, MealPrice * 3 / 4);
            return MealPrice;
        }

        private void SeasonPass(GameSimulation sim)
        {
            var s = VillageClock.SeasonOf(sim.CurrentTick);
            if (s == lastSeason) return;
            bool first = (int)lastSeason < 0;
            lastSeason = s;
            if (first) return;
            switch (s)
            {
                case VillageClock.Season.Spring: LogEvent(sim, "✿ Spring has come — the fields are green again"); break;
                case VillageClock.Season.Summer: LogEvent(sim, "☀ Summer — the harvest is at its best"); break;
                case VillageClock.Season.Autumn: LogEvent(sim, "♨ Autumn — time to lay in stores before the cold"); break;
                case VillageClock.Season.Winter: LogEvent(sim, "❄ Winter has come — the fields lie bare and the wolves grow bold"); break;
            }
        }

        /// <summary>Scale a fresh food delivery by the season (called when a gatherer's deposit is detected).</summary>
        private void ApplySeasonToDeposit(GameSimulation sim, UnitData unit)
        {
            if (unit.LastDepositResourceType != ResourceType.Food || unit.LastDepositAmount <= 0) return;
            int pct = HarvestPercent(VillageClock.SeasonOf(sim.CurrentTick));
            int adjusted = unit.LastDepositAmount * pct / 100;
            int delta = adjusted - unit.LastDepositAmount;
            if (delta == 0) return;
            var store = sim.ResourceManager.GetPlayerResources(PlayerId);
            store.Food = Mathf.Max(0, store.Food + delta);
        }

        // ================================================================== births

        /// <summary>
        /// Once a night (23:00–00:00) every couple asleep in the same house rolls for a baby —
        /// independent of how they got to bed, so a late dinner never skips the roll.
        /// </summary>
        private void ConceptionPass(GameSimulation sim, int minute, int day)
        {
            if (minute < 23 * 60) return;
            for (int i = 0; i < Profiles.Count; i++)
            {
                var p = Profiles[i];
                if (p.IsDead || p.PartnerId < 0 || p.Stage != LifeStage.Adult || p.LastFertilityDay == day) continue;
                if (sim.UnitRegistry.GetGarrisonedUnit(p.UnitId) == null || FindGarrisonBuilding(sim, p.UnitId) != p.HomeBuildingId) continue;
                var partner = GetProfile(p.PartnerId);
                if (partner == null || partner.IsDead || partner.HomeBuildingId != p.HomeBuildingId) continue;
                if (sim.UnitRegistry.GetGarrisonedUnit(partner.UnitId) == null) continue;
                if (p.UnitId > partner.UnitId) continue; // one roll per couple
                p.LastFertilityDay = partner.LastFertilityDay = day;
                if (p.Children >= 4) continue; // each birth counts once per parent
                if (sim.CurrentTick - p.PairedTick < VillageClock.DayLengthTicks) continue;

                float age = p.AgeDays(sim.CurrentTick);
                int chance = age < ChildDays + AdultDays * 0.6f ? 60 : 30;
                if (partner.Stage == LifeStage.Elder) chance /= 2;
                if (!Chance(chance)) continue;

                var house = sim.BuildingRegistry.GetBuilding(p.HomeBuildingId);
                if (house == null) continue;
                var door = DoorTile(sim, house);
                sim.AiCommandBuffer.EnqueueCommand(new CheatSpawnUnitCommand(PlayerId, 0, sim.MapData.TileToWorldFixed(door.x, door.y), 1, PlayerId));
                pendingBirths.Enqueue(new PendingBirth { HouseId = house.Id, ParentA = p.UnitId, ParentB = partner.UnitId, Family = p.FamilyName, HouseholdIndex = p.HouseholdIndex });
            }
        }

        // ================================================================== the dead

        public class Corpse
        {
            public int Id;
            public int UnitId;
            public string Name;
            public Gender Gender;
            public FixedVector3 Position;
            public int DeathTick;
            public int CarrierId = -1;
        }

        public readonly List<Corpse> Corpses = new List<Corpse>();
        public readonly List<string> Burials = new List<string>();
        public int GraveyardBuildingId = -1;
        private int nextCorpseId = 1;

        public Corpse FindCorpse(int id)
        {
            for (int i = 0; i < Corpses.Count; i++) if (Corpses[i].Id == id) return Corpses[i];
            return null;
        }

        private void CreateCorpse(GameSimulation sim, VillagerProfile p)
        {
            FixedVector3 pos;
            var unit = sim.UnitRegistry.GetUnit(p.UnitId);
            if (unit != null) pos = unit.SimPosition;
            else
            {
                int where = FindGarrisonBuilding(sim, p.UnitId);
                var b = sim.BuildingRegistry.GetBuilding(where >= 0 ? where : p.HomeBuildingId);
                if (b == null) return;
                var door = DoorTile(sim, b);
                pos = sim.MapData.TileToWorldFixed(door.x, door.y);
            }
            Corpses.Add(new Corpse { Id = nextCorpseId++, UnitId = p.UnitId, Name = p.FullName, Gender = p.Gender, Position = pos, DeathTick = sim.CurrentTick });
        }

        private BuildingData Graveyard(GameSimulation sim)
        {
            var g = sim.BuildingRegistry.GetBuilding(GraveyardBuildingId);
            return g != null && !g.IsDestroyed && !g.IsUnderConstruction ? g : null;
        }

        /// <summary>Send someone to fetch each unattended body once the village has a graveyard.</summary>
        private void BurialPass(GameSimulation sim)
        {
            if (Corpses.Count == 0) return;
            if (Graveyard(sim) == null) return;
            for (int c = 0; c < Corpses.Count; c++)
            {
                var corpse = Corpses[c];
                if (corpse.CarrierId >= 0) continue;
                var at = sim.MapData.WorldToTile(corpse.Position);
                VillagerProfile best = null; int bestD = int.MaxValue;
                for (int i = 0; i < Profiles.Count; i++)
                {
                    var p = Profiles[i];
                    if (p.IsDead || p.Stage == LifeStage.Child || p.Errand != Errand.None || p.PendingMeal != Meal.None || p.Phase == RoutinePhase.Sleeping) continue;
                    var u = sim.UnitRegistry.GetUnit(p.UnitId);
                    if (u == null) continue;
                    var t = sim.MapData.WorldToTile(u.SimPosition);
                    int dx = t.x - at.x, dz = t.y - at.y, d = dx * dx + dz * dz;
                    if (d < bestD) { bestD = d; best = p; }
                }
                if (best == null) return;
                corpse.CarrierId = best.UnitId;
                best.Errand = Errand.Bury;
                best.ErrandStartTick = sim.CurrentTick;
                best.BuryCorpseId = corpse.Id;
                best.CarryingLoad = false;
                best.Activity = $"Fetching {corpse.Name}'s body";
                Log(sim, best, $"went to fetch {corpse.Name}'s body");
                var tile = GridPathfinder.FindNearestWalkableTile(sim.MapData, at, 4);
                Enqueue(sim, best, new MoveCommand(PlayerId, Ids(best), sim.MapData.TileToWorldFixed(tile.x, tile.y)));
            }
        }

        private bool HandleBury(GameSimulation sim, VillagerProfile p, UnitData unit)
        {
            var corpse = FindCorpse(p.BuryCorpseId);
            var graveyard = Graveyard(sim);
            if (corpse == null || graveyard == null || unit == null) return true;
            if (sim.CurrentTick - p.ErrandStartTick > 3600) return true; // give up; someone else will try

            var at = sim.MapData.WorldToTile(unit.SimPosition);
            if (!p.CarryingLoad)
            {
                var ct = sim.MapData.WorldToTile(corpse.Position);
                int dx = at.x - ct.x, dz = at.y - ct.y;
                if (dx * dx + dz * dz <= 2)
                {
                    p.CarryingLoad = true;
                    p.Activity = $"Carrying {corpse.Name} to the graveyard";
                    Log(sim, p, $"lifted {corpse.Name}'s body and set off for the graveyard");
                    var door = DoorTile(sim, graveyard);
                    Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(door.x, door.y)));
                }
                else if (unit.State == UnitState.Idle && sim.CurrentTick - p.LastCommandTick > CommandCooldown)
                {
                    var tile = GridPathfinder.FindNearestWalkableTile(sim.MapData, ct, 4);
                    Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(tile.x, tile.y)));
                }
                return false;
            }

            corpse.Position = unit.SimPosition; // the body travels with the carrier
            var gd = DoorTile(sim, graveyard);
            int gx = at.x - gd.x, gz = at.y - gd.y;
            if (gx * gx + gz * gz <= 2 * 2)
            {
                Corpses.Remove(corpse);
                Burials.Add(corpse.Name);
                p.CarryingLoad = false;
                p.Money += 5;
                Log(sim, p, $"laid {corpse.Name} to rest in the graveyard", true);
                return true;
            }
            if (unit.State == UnitState.Idle && sim.CurrentTick - p.LastCommandTick > CommandCooldown)
                Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(gd.x, gd.y)));
            return false;
        }

        // ================================================================== construction projects

        public class Project
        {
            public BuildingType Type;
            public string Label;
            public Vector2Int Tile;
            public int Width, Height;
            public int LoadsNeeded, LoadsDelivered;
            public int BuildingId = -1;
            public bool Placed;
            public int StartTick;
            public readonly List<int> Builders = new List<int>();
        }

        public Project ActiveProject;
        public int ProjectsCompleted;
        private int lastProjectDay = -1;

        private void ProjectsPass(GameSimulation sim, int minute, int day)
        {
            if (ActiveProject == null)
            {
                if (day != lastProjectDay && minute >= 9 * 60 && minute < 10 * 60)
                {
                    lastProjectDay = day;
                    ChooseProject(sim);
                }
                return;
            }

            var proj = ActiveProject;

            // Hauling done → place the foundation and assign the builders.
            if (!proj.Placed && proj.LoadsDelivered >= proj.LoadsNeeded)
            {
                proj.Placed = true;
                var ids = new List<int>();
                foreach (var id in proj.Builders) if (sim.UnitRegistry.GetUnit(id) != null) ids.Add(id);
                sim.AiCommandBuffer.EnqueueCommand(new PlaceBuildingCommand(PlayerId, proj.Type, proj.Tile.x, proj.Tile.y, ids.ToArray()));
                LogEvent(sim, $"⚒ Timber delivered — work began on the new {proj.Label}");
                return;
            }

            if (proj.Placed && proj.BuildingId < 0)
            {
                foreach (var b in sim.BuildingRegistry.GetAllBuildings())
                    if (b.OriginTileX == proj.Tile.x && b.OriginTileZ == proj.Tile.y && b.Type == proj.Type) { proj.BuildingId = b.Id; break; }
                if (proj.BuildingId < 0 && sim.CurrentTick - proj.StartTick > 200 && (sim.CurrentTick % 300) == 0)
                {
                    // Placement was refused (site blocked?). Abandon and try again another day.
                    LogEvent(sim, $"The {proj.Label} site turned out to be unusable — the project was abandoned");
                    EndProject(sim, proj, completed: false);
                }
                return;
            }

            if (proj.BuildingId >= 0)
            {
                var b = sim.BuildingRegistry.GetBuilding(proj.BuildingId);
                if (b == null || b.IsDestroyed) { EndProject(sim, proj, completed: false); return; }
                if (!b.IsUnderConstruction)
                {
                    if (proj.Type == BuildingType.Graveyard) GraveyardBuildingId = b.Id;
                    if (proj.Type == BuildingType.Stables) StablesBuildingId = b.Id;
                    if (proj.Type == BuildingType.Barracks) BarracksBuildingId = b.Id;
                    if (proj.Type == BuildingType.ArcheryRange) ArcheryRangeBuildingId = b.Id;
                    if (proj.Type == BuildingType.House) b.GarrisonCapacity = 8;
                    if (proj.Type == BuildingType.Tavern || proj.Type == BuildingType.Market) b.GarrisonCapacity = 20;
                    if (proj.Type == BuildingType.Tavern && TavernBuildingId < 0) TavernBuildingId = b.Id;
                    ProjectsCompleted++;
                    LogEvent(sim, $"⚒ The new {proj.Label} is finished!");
                    EndProject(sim, proj, completed: true);
                }
            }
        }

        private void EndProject(GameSimulation sim, Project proj, bool completed)
        {
            foreach (var id in proj.Builders)
            {
                var p = GetProfile(id);
                if (p == null) continue;
                p.IsBuilder = false;
                if (p.Errand == Errand.Haul || p.Errand == Errand.Build)
                {
                    ReleaseErrand(sim, p);
                    var u = sim.UnitRegistry.GetUnit(id);
                    bool g = u == null && sim.UnitRegistry.GetGarrisonedUnit(id) != null;
                    BeginPhase(sim, p, u, g, resume: true);
                }
                if (completed) p.Money += 6;
            }
            ActiveProject = null;
        }

        private void ChooseProject(GameSimulation sim)
        {
            var res = sim.ResourceManager.GetPlayerResources(PlayerId);
            int houses = 0, farms = 0, towers = 0, alive = 0;
            foreach (var b in sim.BuildingRegistry.GetAllBuildings())
            {
                if (b.IsDestroyed) continue;
                if (b.Type == BuildingType.House) houses++;
                else if (b.Type == BuildingType.Farm) farms++;
                else if (b.Type == BuildingType.Tower) towers++;
            }
            foreach (var p in Profiles) if (!p.IsDead) alive++;

            // Score what the village needs; pick the top candidate with a bit of luck so it isn't always the same.
            var options = new List<(BuildingType type, string label, int wood, int score)>();
            void Option(BuildingType type, string label, int wood, int score) { if (score > 0) options.Add((type, label, wood, score)); }

            int tableWaits = 0; foreach (var e in Activity) if (e.Text.Contains("waiting for a table")) tableWaits++;
            int foodPerHead = alive > 0 ? res.Food / alive : 0;
            bool hasStables = Usable(sim, StablesBuildingId) != null || (StablesBuildingId >= 0 && sim.BuildingRegistry.GetBuilding(StablesBuildingId) != null);
            bool hasBarracks = BarracksBuildingId >= 0 && sim.BuildingRegistry.GetBuilding(BarracksBuildingId) != null;
            bool hasRange = ArcheryRangeBuildingId >= 0 && sim.BuildingRegistry.GetBuilding(ArcheryRangeBuildingId) != null;
            int taverns = 0; foreach (var b in sim.BuildingRegistry.GetAllBuildings()) if (b.Type == BuildingType.Tavern && !b.IsDestroyed) taverns++;

            Option(BuildingType.Graveyard, "graveyard", 30, GraveyardBuildingId < 0 ? 100 : 0);
            Option(BuildingType.House, "house", Mathf.Max(30, sim.GetBuildingWoodCost(BuildingType.House)), alive > houses * 3 ? 60 + (alive - houses * 3) * 10 : alive > houses * 2 ? 20 : 0);
            Option(BuildingType.Farm, "farm", Mathf.Max(30, sim.GetBuildingWoodCost(BuildingType.Farm)), farms < 12 ? (foodPerHead < 3 ? 70 : foodPerHead < 6 ? 40 : 15) : 0);
            Option(BuildingType.Stables, "stables", Mathf.Max(60, sim.GetBuildingWoodCost(BuildingType.Stables)), hasStables ? 0 : (HorseIds.Count > 0 ? 45 : 20));
            Option(BuildingType.Barracks, "barracks", Mathf.Max(80, sim.GetBuildingWoodCost(BuildingType.Barracks)), hasBarracks ? 0 : 15 + RaidCount * 30 + WolfAttackCount * 8);
            Option(BuildingType.ArcheryRange, "archery range", Mathf.Max(80, sim.GetBuildingWoodCost(BuildingType.ArcheryRange)), hasRange ? 0 : 10 + RaidCount * 20 + WolfAttackCount * 10);
            Option(BuildingType.Tower, "watchtower", Mathf.Max(40, sim.GetBuildingWoodCost(BuildingType.Tower)), towers < 5 ? 10 + RaidCount * 12 + WolfAttackCount * 6 : 0);
            Option(BuildingType.Tavern, "second tavern", Mathf.Max(60, sim.GetBuildingWoodCost(BuildingType.Market)), taverns < 2 && tableWaits > 40 ? 35 + tableWaits / 4 : 0);
            if (options.Count == 0) return;

            // Weighted random among the options (score + luck), then require the timber.
            int bestIdx = 0, bestRoll = int.MinValue;
            for (int i = 0; i < options.Count; i++)
            {
                int roll = options[i].score + (int)(Next() % 40);
                if (roll > bestRoll) { bestRoll = roll; bestIdx = i; }
            }
            var pick = options[bestIdx];
            BuildingType type = pick.type; string label = pick.label; int needWood = pick.wood;

            if (res.Wood < needWood) { LogEvent(sim, $"The council wanted a new {label} but the timber store is short ({res.Wood}/{needWood} wood)"); return; }

            int w = Footprint(sim, type, out int h);
            if (!FindSite(sim, w, h, out var tile)) return;

            // Each load is a big bundle: 2–6 trips regardless of cost, so big buildings don't take a week.
            var proj = new Project { Type = type, Label = label, Tile = tile, Width = w, Height = h, LoadsNeeded = Mathf.Clamp(needWood / 25, 2, 6), StartTick = sim.CurrentTick };
            // Three able adults, foresters first; farmers, foragers, cooks and guards keep the village fed and safe.
            var candidates = new List<VillagerProfile>();
            foreach (var p in Profiles)
                if (!p.IsDead && p.Stage != LifeStage.Child && p.Job != VillageJob.Cook && p.Job != VillageJob.Guard && p.Job != VillageJob.Farmer && p.Job != VillageJob.Forager
                    && p.Errand == Errand.None && !p.Has(Trait.BrokenLeg))
                    candidates.Add(p);
            candidates.Sort((a, b) => (b.Job == VillageJob.Forester ? 1 : 0).CompareTo(a.Job == VillageJob.Forester ? 1 : 0));
            for (int i = 0; i < candidates.Count && proj.Builders.Count < 3; i++)
            {
                var p = candidates[i];
                p.IsBuilder = true;
                proj.Builders.Add(p.UnitId);
            }
            if (proj.Builders.Count == 0) return;
            ActiveProject = proj;
            var names = new List<string>(); foreach (var id in proj.Builders) names.Add(GetProfile(id).FirstName);
            LogEvent(sim, $"⚒ The council decided to build a new {label} — {string.Join(", ", names)} will haul {proj.LoadsNeeded} loads of timber and build it");

            foreach (var id in proj.Builders)
            {
                var p = GetProfile(id);
                var u = sim.UnitRegistry.GetUnit(id);
                if (u != null && p.Phase == RoutinePhase.Working && p.PendingMeal == Meal.None) StartProjectErrand(sim, p, u);
            }
        }

        private static int Footprint(GameSimulation sim, BuildingType type, out int h)
        {
            var c = sim.Config;
            switch (type)
            {
                case BuildingType.Farm: h = c.FarmFootprintHeight; return c.FarmFootprintWidth;
                case BuildingType.Tower: h = c.TowerFootprintHeight; return c.TowerFootprintWidth;
                case BuildingType.Tavern: case BuildingType.Market: h = c.MarketFootprintHeight; return c.MarketFootprintWidth;
                case BuildingType.Stables: h = c.StablesFootprintHeight; return c.StablesFootprintWidth;
                case BuildingType.Barracks: h = c.BarracksFootprintHeight; return c.BarracksFootprintWidth;
                case BuildingType.ArcheryRange: h = c.ArcheryRangeFootprintHeight; return c.ArcheryRangeFootprintWidth;
                default: h = c.HouseFootprintHeight; return c.HouseFootprintWidth; // House, Graveyard
            }
        }

        /// <summary>A buildable spot with a 1-tile margin: start from a random direction and pick one of the first few fits.</summary>
        private bool FindSite(GameSimulation sim, int w, int h, out Vector2Int site)
        {
            var map = sim.MapData;
            var center = map.WorldToTile(PlazaPosition);
            var found = new List<Vector2Int>();
            float startAngle = (Next() % 360) * Mathf.Deg2Rad;
            for (int radius = 8; radius <= 28 && found.Count < 6; radius += 2)
            {
                int steps = Mathf.Max(8, radius * 2);
                for (int s = 0; s < steps && found.Count < 6; s++)
                {
                    float a = startAngle + s * Mathf.PI * 2f / steps;
                    int x = center.x + Mathf.RoundToInt(Mathf.Cos(a) * radius) - w / 2;
                    int z = center.y + Mathf.RoundToInt(Mathf.Sin(a) * radius) - h / 2;
                    bool ok = true;
                    for (int tx = x - 1; ok && tx < x + w + 1; tx++)
                        for (int tz = z - 1; ok && tz < z + h + 1; tz++)
                            if (!map.IsBuildable(tx, tz) || map.ForestDensity[tx, tz] >= MapData.ForestWalkableThreshold) ok = false;
                    if (ok) found.Add(new Vector2Int(x, z));
                }
            }
            if (found.Count == 0) { site = default; return false; }
            site = found[(int)(Next() % (uint)found.Count)];
            return true;
        }

        private BuildingData TimberSource(GameSimulation sim)
        {
            BuildingData tc = null;
            foreach (var b in sim.BuildingRegistry.GetAllBuildings())
            {
                if (b.IsDestroyed || b.IsUnderConstruction) continue;
                if (b.Type == BuildingType.LumberYard) return b;
                if (b.Type == BuildingType.TownCenter) tc = b;
            }
            return tc;
        }

        private void StartProjectErrand(GameSimulation sim, VillagerProfile p, UnitData unit)
        {
            var proj = ActiveProject;
            if (proj == null || unit == null) return;
            p.ErrandStartTick = sim.CurrentTick;
            if (!proj.Placed)
            {
                p.Errand = Errand.Haul;
                p.CarryingLoad = false;
                p.Activity = "Fetching timber for the " + proj.Label;
                var yard = TimberSource(sim);
                if (yard != null) { var d = DoorTile(sim, yard); Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(d.x, d.y))); }
            }
            else
            {
                p.Errand = Errand.Build;
                p.Activity = "Building the " + proj.Label;
                if (proj.BuildingId >= 0) Enqueue(sim, p, new ConstructBuildingCommand(PlayerId, Ids(p), proj.BuildingId));
            }
        }

        private bool HandleHaul(GameSimulation sim, VillagerProfile p, UnitData unit)
        {
            var proj = ActiveProject;
            if (proj == null || unit == null) return true;
            if (proj.Placed) { StartProjectErrand(sim, p, unit); return false; }

            var at = sim.MapData.WorldToTile(unit.SimPosition);
            if (!p.CarryingLoad)
            {
                var yard = TimberSource(sim);
                if (yard == null) return true;
                var yd = DoorTile(sim, yard);
                int dx = at.x - yd.x, dz = at.y - yd.y;
                if (dx * dx + dz * dz <= 2 * 2)
                {
                    p.CarryingLoad = true;
                    p.Money += 3;
                    p.Activity = "Hauling timber to the " + proj.Label + " site";
                    var st = GridPathfinder.FindNearestWalkableTile(sim.MapData, new Vector2Int(proj.Tile.x + proj.Width / 2, proj.Tile.y - 1), 5);
                    Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(st.x, st.y)));
                }
                else if (unit.State == UnitState.Idle && sim.CurrentTick - p.LastCommandTick > CommandCooldown)
                    Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(yd.x, yd.y)));
                return false;
            }

            var site = new Vector2Int(proj.Tile.x + proj.Width / 2, proj.Tile.y - 1);
            int sx = at.x - site.x, sz = at.y - site.y;
            if (sx * sx + sz * sz <= 2 * 2)
            {
                p.CarryingLoad = false;
                proj.LoadsDelivered++;
                Log(sim, p, $"delivered a load of timber to the {proj.Label} site ({proj.LoadsDelivered}/{proj.LoadsNeeded})");
                if (proj.LoadsDelivered >= proj.LoadsNeeded) return false; // ProjectsPass places the foundation
                p.Activity = "Fetching timber for the " + proj.Label;
                var yard = TimberSource(sim);
                if (yard != null) { var d = DoorTile(sim, yard); Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(d.x, d.y))); }
            }
            else if (unit.State == UnitState.Idle && sim.CurrentTick - p.LastCommandTick > CommandCooldown)
            {
                var st = GridPathfinder.FindNearestWalkableTile(sim.MapData, site, 5);
                Enqueue(sim, p, new MoveCommand(PlayerId, Ids(p), sim.MapData.TileToWorldFixed(st.x, st.y)));
            }
            return false;
        }

        private bool HandleBuild(GameSimulation sim, VillagerProfile p, UnitData unit)
        {
            var proj = ActiveProject;
            if (proj == null || unit == null) return true;
            if (proj.BuildingId < 0) return false; // waiting for the foundation to appear
            var b = sim.BuildingRegistry.GetBuilding(proj.BuildingId);
            if (b == null || !b.IsUnderConstruction) return true;
            if (unit.State == UnitState.Idle && sim.CurrentTick - p.LastCommandTick > CommandCooldown)
                Enqueue(sim, p, new ConstructBuildingCommand(PlayerId, Ids(p), b.Id));
            return false;
        }

        // ================================================================== wolves

        public const int WolfPlayerId = 1;
        public readonly List<int> WolfIds = new List<int>();
        public int WolfAttackStartTick = -1;
        private int wolfAttackEndTick = -1;
        private int pendingWolfSpawns;
        private int lastWolfDay = -1;
        private readonly Dictionary<int, int> wolfLastBite = new Dictionary<int, int>();
        private readonly Dictionary<int, int> wolfLastMove = new Dictionary<int, int>();

        private void WolfPass(GameSimulation sim, int minute, int day)
        {
            int tick = sim.CurrentTick;

            // Adopt newly spawned wolves (units owned by the hostile "player") and give them wolf stats.
            if (pendingWolfSpawns > 0)
            {
                var units = sim.UnitRegistry.GetAllUnits();
                for (int i = 0; i < units.Count && pendingWolfSpawns > 0; i++)
                {
                    var u = units[i];
                    if (u.PlayerId != WolfPlayerId || u.UnitType != 4 || WolfIds.Contains(u.Id) || SoldierIds.Contains(u.Id)) continue;
                    WolfIds.Add(u.Id);
                    pendingWolfSpawns--;
                    u.MaxHealth = 60; u.CurrentHealth = 60;
                    u.AttackDamage = 7;
                    u.AttackCooldownTicks = 30;
                    u.MoveSpeed = Fixed32.FromFloat(2.4f);
                }
            }

            if (WolfIds.Count == 0 && pendingWolfSpawns == 0)
            {
                // Dusk: small chance a pack comes down from the forest.
                if (day != lastWolfDay && minute >= 19 * 60 && minute < 21 * 60)
                {
                    lastWolfDay = day;
                    // Hungry winters bring the pack down more often.
                    int chance = VillageClock.SeasonOf(sim.CurrentTick) == VillageClock.Season.Winter ? 60 : 25;
                    if (!Chance(chance)) return;
                    int count = 2 + (int)(Next() % 2);
                    float angle = (Next() % 360) * Mathf.Deg2Rad;
                    var center = sim.MapData.WorldToTile(PlazaPosition);
                    var edge = new Vector2Int(center.x + Mathf.RoundToInt(Mathf.Cos(angle) * 26f), center.y + Mathf.RoundToInt(Mathf.Sin(angle) * 26f));
                    edge = GridPathfinder.FindNearestWalkableTile(sim.MapData, edge, 8);
                    sim.AiCommandBuffer.EnqueueCommand(new CheatSpawnUnitCommand(PlayerId, 4, sim.MapData.TileToWorldFixed(edge.x, edge.y), count, WolfPlayerId));
                    pendingWolfSpawns = count;
                    WolfAttackStartTick = tick;
                    wolfAttackEndTick = tick + 1500;
                    WolfAttackCount++;
                    foreach (var p in Profiles) { p.WolfDecisionTick = -1; }
                    LogEvent(sim, $"🐺 A pack of {count} wolves is prowling at the edge of the village!");
                }
                return;
            }

            // Pack behaviour.
            for (int i = WolfIds.Count - 1; i >= 0; i--)
            {
                int id = WolfIds[i];
                var wolf = sim.UnitRegistry.GetUnit(id);
                if (wolf == null || wolf.State == UnitState.Dead || wolf.CurrentHealth <= 0)
                {
                    if (wolf != null && wolf.CurrentHealth <= 0 && wolf.State != UnitState.Dead)
                        sim.AiCommandBuffer.EnqueueCommand(new DeleteUnitsCommand(WolfPlayerId, new[] { id }));
                    WolfIds.RemoveAt(i);
                    LogEvent(sim, "🐺 A wolf was slain");
                    if (wolf != null) CreditDefenders(sim, wolf.SimPosition, "a wolf");
                    continue;
                }

                if (tick >= wolfAttackEndTick)
                {
                    sim.AiCommandBuffer.EnqueueCommand(new DeleteUnitsCommand(WolfPlayerId, new[] { id }));
                    WolfIds.RemoveAt(i);
                    continue;
                }

                // Hunt the nearest villager through the real combat system (chasing, biting, health bars).
                var victim = NearestVillagerUnit(sim, wolf.SimPosition, 45, out _);
                if (victim != null) HostileOrder(sim, wolf, victim, 45);
            }

            if (WolfIds.Count == 0 && pendingWolfSpawns == 0 && tick >= wolfAttackEndTick)
                LogEvent(sim, "🐺 The wolves slunk back into the forest");
            else if (WolfIds.Count == 0 && pendingWolfSpawns == 0)
                LogEvent(sim, "🐺 The pack was driven off!");
        }

        private UnitData NearestVillagerUnit(GameSimulation sim, FixedVector3 from, int tiles, out VillagerProfile profile)
        {
            var a = sim.MapData.WorldToTile(from);
            UnitData best = null; profile = null; int bestD = tiles * tiles + 1;
            for (int i = 0; i < Profiles.Count; i++)
            {
                var p = Profiles[i];
                if (p.IsDead) continue;
                var u = sim.UnitRegistry.GetUnit(p.UnitId);
                if (u == null) continue;
                var b = sim.MapData.WorldToTile(u.SimPosition);
                int dx = a.x - b.x, dz = a.y - b.y, d = dx * dx + dz * dz;
                if (d < bestD) { bestD = d; best = u; profile = p; }
            }
            return best;
        }

        private UnitData NearestWolf(GameSimulation sim, FixedVector3 from, int tiles)
        {
            var a = sim.MapData.WorldToTile(from);
            UnitData best = null; int bestD = tiles * tiles + 1;
            for (int i = 0; i < WolfIds.Count; i++)
            {
                var w = sim.UnitRegistry.GetUnit(WolfIds[i]);
                if (w == null || w.CurrentHealth <= 0) continue;
                var b = sim.MapData.WorldToTile(w.SimPosition);
                int dx = a.x - b.x, dz = a.y - b.y, d = dx * dx + dz * dz;
                if (d < bestD) { bestD = d; best = w; }
            }
            return best;
        }

        /// <summary>Credit whoever was fighting near a slain threat: neighbours remember their protector.</summary>
        private void CreditDefenders(GameSimulation sim, FixedVector3 where, string what)
        {
            foreach (var p in Profiles)
            {
                if (p.IsDead || p.Errand != Errand.Defend) continue;
                var pu = sim.UnitRegistry.GetUnit(p.UnitId);
                if (pu == null) continue;
                var a = sim.MapData.WorldToTile(pu.SimPosition); var b = sim.MapData.WorldToTile(where);
                int dx = a.x - b.x, dz = a.y - b.y;
                if (dx * dx + dz * dz > 6 * 6) continue;
                Log(sim, p, $"struck {what} down", true);
                foreach (var o in Profiles)
                {
                    if (o == p || o.IsDead) continue;
                    var ou = sim.UnitRegistry.GetUnit(o.UnitId);
                    if (ou == null || TileDistanceSq(sim, pu, ou) > 12 * 12) continue;
                    Remember(sim, o, p, 8, $"{p.FirstName} drove off {what}", $"kept {what} off {o.FirstName}");
                }
                return; // one hero per kill
            }
        }
    }
}
