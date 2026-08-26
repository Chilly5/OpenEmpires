using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires.Village
{
    /// <summary>
    /// Procedurally lays out a village around the player's base position on top of the
    /// normal Open Empires map generation, then populates it with households and jobs.
    /// Buildings are created fully constructed through <see cref="GameSimulation.CreateBuilding"/>,
    /// so pathfinding, fog of war, drop-off and garrison all behave exactly as in a match.
    /// </summary>
    public class VillageGenerator
    {
        public int HouseCount = 15;
        public int VillagerTarget = 40;
        public int FarmCount = 6;
        public float WalkSpeedMultiplier = 1f;
        public int CarryCapacity = 10;
        public int StartingMoney = 8;

        private uint rng;
        private GameSimulation sim;
        private GameSetup setup;
        private int playerId;
        private int cx, cz; // village centre (TC centre tile)

        private const int VillageClearRadius = 30;
        private const int ForestClearRadius = 25;
        private static readonly Vector2Int ForestOffset = new Vector2Int(31, 13);
        private const int ForestRadius = 7;

        private static string[] FamilyNames => VillageNames.Family;

        public VillageGenerator(int seed)
        {
            rng = (uint)seed * 2654435761u + 0x9E3779B9u;
            if (rng == 0) rng = 1;
        }

        // ------------------------------------------------------------------ terrain

        /// <summary>Flatten and clear the village site; plant a working forest to the east.</summary>
        public void PrepareTerrain(GameSimulation sim, Vector2Int basePos)
        {
            var map = sim.MapData;
            int centerX = basePos.x + 2, centerZ = basePos.y + 2;
            map.ClearAreaForBase(centerX, centerZ, VillageClearRadius);

            for (int x = centerX - ForestClearRadius; x <= centerX + ForestClearRadius; x++)
                for (int z = centerZ - ForestClearRadius; z <= centerZ + ForestClearRadius; z++)
                {
                    if (!map.IsInBounds(x, z)) continue;
                    int dx = x - centerX, dz = z - centerZ;
                    if (dx * dx + dz * dz <= ForestClearRadius * ForestClearRadius)
                        map.ForestDensity[x, z] = 0f;
                }

            int fx = centerX + ForestOffset.x, fz = centerZ + ForestOffset.y;
            for (int x = fx - ForestRadius; x <= fx + ForestRadius; x++)
                for (int z = fz - ForestRadius; z <= fz + ForestRadius; z++)
                {
                    if (!map.IsInBounds(x, z) || map.IsOutsideCircle(x, z)) continue;
                    if (map.Tiles[x, z] != TileType.Grass) continue;
                    int dx = x - fx, dz = z - fz;
                    float d = Mathf.Sqrt(dx * dx + dz * dz) / ForestRadius;
                    if (d > 1f) continue;
                    map.ForestDensity[x, z] = Mathf.Max(map.ForestDensity[x, z], Mathf.Lerp(0.95f, 0.7f, d));
                }
        }

        // ------------------------------------------------------------------ village

        public void Build(GameSetup setup, GameSimulation sim, int playerId, int tileX, int tileZ, VillageRoutineSystem routine)
        {
            this.sim = sim;
            this.setup = setup;
            this.playerId = playerId;
            var map = sim.MapData;
            var cfg = sim.Config;
            cx = tileX + cfg.TownCenterFootprintWidth / 2;
            cz = tileZ + cfg.TownCenterFootprintHeight / 2;

            // --- Town Center ---
            for (int x = tileX; x < tileX + cfg.TownCenterFootprintWidth; x++)
                for (int z = tileZ; z < tileZ + cfg.TownCenterFootprintHeight; z++)
                    if (!map.IsWalkable(x, z)) map.Tiles[x, z] = TileType.Grass;
            var tc = sim.CreateBuilding(playerId, BuildingType.TownCenter, tileX, tileZ, underConstruction: false, isMainTownCenter: true);
            tc.AutoProduceVillagers = false;
            setup.SpawnBuildingView(tc);

            sim.ResourceManager.AddResource(playerId, ResourceType.Food, 200);
            sim.ResourceManager.AddResource(playerId, ResourceType.Wood, 200);
            sim.ResourceManager.AddResource(playerId, ResourceType.Gold, 100);
            sim.ResourceManager.AddResource(playerId, ResourceType.Stone, 100);

            var plazaTile = VillageRoutineSystem.DoorTile(sim, tc);
            routine.PlazaPosition = map.TileToWorldFixed(plazaTile.x, plazaTile.y + -2);

            // --- Civic buildings around the square ---
            var market = Place(BuildingType.Market, cx + 4, cz - 2, 5);
            var blacksmith = Place(BuildingType.Blacksmith, cx + 4, cz + 4, 5);
            var university = Place(BuildingType.University, cx - 9, cz - 5, 5);
            var monastery = Place(BuildingType.Monastery, cx - 9, cz + 3, 5);
            // Tavern on the square: every villager buys breakfast, lunch and dinner here.
            var tavern = Place(BuildingType.Tavern, cx - 3, cz - 8, 6);
            routine.TavernBuildingId = tavern != null ? tavern.Id : -1;

            // --- Mill next to the berry patch (if the map put one nearby) ---
            var berry = NearestNode(ResourceType.Food, cx, cz, 32, requireNotFarm: true);
            var mill = berry != null
                ? Place(BuildingType.Mill, berry.TileX - 4, berry.TileZ - 1, 6)
                : Place(BuildingType.Mill, cx + 12, cz + 9, 6);

            // --- Farm block to the south ---
            var farms = new List<BuildingData>();
            for (int i = 0; i < FarmCount; i++)
            {
                int col = i % 3, row = i / 3;
                var farm = Place(BuildingType.Farm, cx - 5 + col * 3, cz - 13 - row * 3, 4);
                if (farm != null) farms.Add(farm);
            }

            // --- Mine camp beside the nearest gold vein, with extra veins so it never runs dry ---
            var gold = NearestNode(ResourceType.Gold, cx, cz, 40);
            var mine = gold != null
                ? Place(BuildingType.Mine, gold.TileX - 4, gold.TileZ, 6)
                : Place(BuildingType.Mine, cx - 18, cz - 8, 6);
            if (mine != null)
            {
                var mr = setup.MapRenderer;
                if (mr != null)
                {
                    TrySpawnNodeNear(mr, ResourceType.Stone, mine.OriginTileX + 4, mine.OriginTileZ - 3, 3000);
                    TrySpawnNodeNear(mr, ResourceType.Stone, mine.OriginTileX - 3, mine.OriginTileZ + 3, 3000);
                    TrySpawnNodeNear(mr, ResourceType.Gold, mine.OriginTileX + 4, mine.OriginTileZ + 4, 3000);
                }
            }

            // --- Lumber yard at the forest edge ---
            var lumber = Place(BuildingType.LumberYard, cx + ForestOffset.x - 8, cz + ForestOffset.y - 2, 6);

            // --- Watchtowers on the edge for the guards ---
            var towerA = Place(BuildingType.Tower, cx - 15, cz + 13, 5);
            var towerB = Place(BuildingType.Tower, cx + 15, cz - 13, 5);

            // --- Stables for tamed horses, out towards the meadow ---
            var stables = Place(BuildingType.Stables, cx - 16, cz + 4, 6);
            routine.StablesBuildingId = stables != null ? stables.Id : -1;
            routine.BlacksmithBuildingId = blacksmith != null ? blacksmith.Id : -1;

            // --- Houses in a ring (leaving the southern sector for the farms) ---
            var houses = new List<BuildingData>();
            for (int i = 0; i < HouseCount; i++)
            {
                float t = (float)i / HouseCount;
                float angle = -Mathf.PI / 3f + t * (5f * Mathf.PI / 3f);
                float radius = 11f + (i % 2) * 3.5f;
                int px = Mathf.RoundToInt(cx + Mathf.Cos(angle) * radius) - 1;
                int pz = Mathf.RoundToInt(cz + Mathf.Sin(angle) * radius) - 1;
                var house = Place(BuildingType.House, px, pz, 5);
                if (house != null) { house.GarrisonCapacity = 8; houses.Add(house); }
            }

            // Indoor workplaces hold a whole shift.
            foreach (var b in new[] { market, blacksmith, university, monastery, tavern })
                if (b != null) b.GarrisonCapacity = 20;

            map.ComputeHoleMap();

            // --- Households, jobs, schedules ---
            if (houses.Count == 0)
            {
                Debug.LogError("[AI Village] No houses could be placed — village is empty.");
                return;
            }

            var jobs = BuildJobPool(farms.Count, lumber != null, mine != null, mill != null,
                blacksmith != null, university != null, market != null, monastery != null,
                towerA != null && towerB != null, tavern != null, VillagerTarget);

            int[] householdSize = new int[houses.Count];
            for (int i = 0; i < houses.Count; i++) householdSize[i] = 2;
            int remaining = Mathf.Max(0, VillagerTarget - 2 * houses.Count);
            int guard = 0;
            while (remaining > 0 && guard++ < 1000)
            {
                int h = (int)(Next() % (uint)houses.Count);
                if (householdSize[h] < 4) { householdSize[h]++; remaining--; }
            }

            int farmCursor = 0, jobCursor = 0;
            var slotCounter = new Dictionary<VillageJob, int>();
            var usedFirst = new HashSet<string>();
            for (int h = 0; h < houses.Count; h++)
            {
                var house = houses[h];
                string family = FamilyNames[h % FamilyNames.Length];
                var door = VillageRoutineSystem.DoorTile(sim, house);

                for (int m = 0; m < householdSize[h]; m++)
                {
                    if (jobCursor >= jobs.Count) break;
                    var job = jobs[jobCursor++];

                    Vector3 pos = new Vector3(door.x + 0.5f + (m % 2) * 0.8f, 0f, door.y + 0.5f - (m / 2) * 0.8f);
                    var unit = setup.SpawnVillager(sim, playerId, pos);
                    // Village pace: slower walking, smaller loads (gather cooldown comes from the config asset).
                    unit.MoveSpeed = sim.ConfigToFixed32(sim.Config.UnitMoveSpeed * WalkSpeedMultiplier);
                    unit.CarryCapacity = Mathf.Max(1, CarryCapacity);

                    var gender = Rand(0, 1) == 0 ? Gender.Male : Gender.Female;
                    var p = new VillagerProfile
                    {
                        UnitId = unit.Id,
                        Gender = gender,
                        FirstName = PickFirstName(usedFirst, gender),
                        FamilyName = family,
                        BaseMoveSpeed = unit.MoveSpeed,
                        HouseholdIndex = h,
                        Job = job,
                        HomeBuildingId = house.Id,
                        Money = StartingMoney,
                    };
                    slotCounter.TryGetValue(job, out int slot);
                    slotCounter[job] = slot + 1;
                    p.GatherSlot = slot;

                    switch (job)
                    {
                        case VillageJob.Farmer:
                            var farm = farms[farmCursor++ % farms.Count];
                            p.WorkplaceBuildingId = farm.Id;
                            p.WorkNodeId = farm.LinkedResourceNodeId;
                            break;
                        case VillageJob.Guard:
                            bool swap = slot % 2 == 1;
                            p.WorkplaceBuildingId = (swap ? towerB : towerA).Id;
                            p.PatrolBuildingId = (swap ? towerA : towerB).Id;
                            break;
                        default:
                            var wp = FindWorkplace(VillageJobInfo.Workplace(job), lumber, mine, mill, blacksmith, university, market, monastery, tavern);
                            p.WorkplaceBuildingId = wp != null ? wp.Id : tc.Id;
                            break;
                    }

                    routine.RollInnateTraits(p, Rand(1, 3));  // traits first: they shift the schedule
                    AssignSchedule(p);
                    AssignAgeAndNeeds(p, VillageClock.DayLengthTicks, routine.ChildDays, routine.AdultDays, routine.ElderDays);
                    routine.AddProfile(p);
                    routine.ApplyPace(sim, p);
                }
            }

            // Starting relationships: households know each other well; everyone has a few acquaintances.
            for (int i = 0; i < routine.Profiles.Count; i++)
                for (int j = i + 1; j < routine.Profiles.Count; j++)
                {
                    var a = routine.Profiles[i]; var b = routine.Profiles[j];
                    if (a.HouseholdIndex == b.HouseholdIndex) routine.ChangeRelation(a.UnitId, b.UnitId, Rand(20, 45));
                    else if (Rand(0, 99) < 22) routine.ChangeRelation(a.UnitId, b.UnitId, Rand(-8, 25));
                }

            // Careers available to children who come of age (farms are one-per-farmer, so excluded).
            routine.UniversityBuildingId = university != null ? university.Id : -1;
            void Slot(VillageJob j, BuildingData wp, BuildingData patrol = null)
            {
                if (wp != null) routine.AdultJobSlots.Add(new VillageRoutineSystem.JobSlot { Job = j, WorkplaceId = wp.Id, PatrolId = patrol != null ? patrol.Id : -1 });
            }
            Slot(VillageJob.Forester, lumber); Slot(VillageJob.Forester, lumber);
            Slot(VillageJob.Miner, mine); Slot(VillageJob.Quarryman, mine);
            Slot(VillageJob.Forager, mill);
            Slot(VillageJob.Blacksmith, blacksmith);
            Slot(VillageJob.Merchant, market);
            Slot(VillageJob.Monk, monastery);
            Slot(VillageJob.Cook, tavern);
            Slot(VillageJob.Guard, towerA, towerB); Slot(VillageJob.Guard, towerB, towerA);

            // Sharper sprite edges: the RTS cutoff of 0.05 keeps the semi-transparent fringe (a pale halo,
            // very visible against shadows). Raise it on every building sprite material instance.
            foreach (var view in Object.FindObjectsByType<BuildingView>(FindObjectsSortMode.None))
                foreach (var r in view.GetComponentsInChildren<Renderer>(true))
                {
                    var m = r.sharedMaterial;
                    if (m != null && m.HasProperty("_Cutoff") && m.shader != null && m.shader.name.Contains("Billboard")) m.SetFloat("_Cutoff", 0.4f);
                }

            Debug.Log($"[AI Village] Built village: {houses.Count} houses, {farms.Count} farms, {routine.Profiles.Count} villagers, " +
                      $"{sim.BuildingRegistry.Count} buildings total.");
        }

        // ------------------------------------------------------------------ placement

        private BuildingData Place(BuildingType type, int prefX, int prefZ, int maxRadius)
        {
            var (w, h) = Footprint(type);
            int margin = type == BuildingType.Farm ? 1 : 1;
            for (int r = 0; r <= maxRadius; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                        int x = prefX + dx, z = prefZ + dz;
                        if (!CanPlace(x, z, w, h, margin)) continue;
                        return Create(type, x, z);
                    }
            }
            Debug.LogWarning($"[AI Village] Could not place {type} near ({prefX},{prefZ})");
            return null;
        }

        private bool CanPlace(int x, int z, int w, int h, int margin)
        {
            var map = sim.MapData;
            for (int tx = x - margin; tx < x + w + margin; tx++)
                for (int tz = z - margin; tz < z + h + margin; tz++)
                {
                    if (!map.IsBuildable(tx, tz)) return false;
                    if (map.ForestDensity[tx, tz] >= MapData.ForestWalkableThreshold) return false;
                }
            // Keep a small yard around the town center's square.
            int ddx = (x + w / 2) - cx, ddz = (z + h / 2) - cz;
            if (ddx * ddx + ddz * ddz < 5 * 5) return false;
            return true;
        }

        private BuildingData Create(BuildingType type, int x, int z)
        {
            var b = sim.CreateBuilding(playerId, type, x, z, underConstruction: false);
            if (type == BuildingType.Farm)
            {
                var node = sim.MapData.AddFarmResourceNode(ResourceType.Food, b.SimPosition, int.MaxValue);
                node.LinkedBuildingId = b.Id;
                b.LinkedResourceNodeId = node.Id;
                sim.MapData.MarkFarmTiles(b.OriginTileX, b.OriginTileZ, b.TileFootprintWidth, b.TileFootprintHeight);
            }
            setup.SpawnBuildingView(b);
            if (type == BuildingType.Farm) FlattenFarmSprite(b);
            return b;
        }

        private static Material farmMaterial;

        /// <summary>
        /// The stock farm sprite uses the shared building billboard shader, which lets buildings
        /// occlude units standing behind the sprite plane — wrong for a field villagers walk on.
        /// Swap in the AI Village "BillboardFarm" variant (same look; no depth write, and it skips
        /// pixels a unit already drew) so villagers always render on top of the farm.
        /// </summary>
        private void FlattenFarmSprite(BuildingData farm)
        {
            BuildingView view = null;
            foreach (var v in Object.FindObjectsByType<BuildingView>(FindObjectsSortMode.None))
                if (v.BuildingId == farm.Id) { view = v; break; }
            var sprite = view != null ? view.transform.Find("Sprite") : null;
            if (sprite == null) return;

            var renderer = sprite.GetComponent<MeshRenderer>();
            var src = renderer.sharedMaterial;
            if (src == null) return;

            if (farmMaterial == null)
            {
                var shader = Shader.Find("OpenEmpires/BillboardFarm");
                if (shader == null) { Debug.LogWarning("[AI Village] BillboardFarm shader not found; farms keep the default material."); return; }
                farmMaterial = new Material(src) { name = "FarmSprite (walk-on)", shader = shader };
                farmMaterial.renderQueue = src.renderQueue;
                DayNightController.RegisterRuntimeMaterial(farmMaterial);
            }
            renderer.sharedMaterial = farmMaterial;
        }

        private (int, int) Footprint(BuildingType type)
        {
            var c = sim.Config;
            switch (type)
            {
                case BuildingType.TownCenter: return (c.TownCenterFootprintWidth, c.TownCenterFootprintHeight);
                case BuildingType.Mill: return (c.MillFootprintWidth, c.MillFootprintHeight);
                case BuildingType.LumberYard: return (c.LumberYardFootprintWidth, c.LumberYardFootprintHeight);
                case BuildingType.Mine: return (c.MineFootprintWidth, c.MineFootprintHeight);
                case BuildingType.Farm: return (c.FarmFootprintWidth, c.FarmFootprintHeight);
                case BuildingType.Tower: return (c.TowerFootprintWidth, c.TowerFootprintHeight);
                case BuildingType.Monastery: return (c.MonasteryFootprintWidth, c.MonasteryFootprintHeight);
                case BuildingType.Blacksmith: return (c.BlacksmithFootprintWidth, c.BlacksmithFootprintHeight);
                case BuildingType.Market: return (c.MarketFootprintWidth, c.MarketFootprintHeight);
                case BuildingType.University: return (c.UniversityFootprintWidth, c.UniversityFootprintHeight);
                default: return (c.HouseFootprintWidth, c.HouseFootprintHeight);
            }
        }

        private ResourceNodeData NearestNode(ResourceType type, int x, int z, int maxDist, bool requireNotFarm = false)
        {
            ResourceNodeData best = null;
            int bestD = maxDist * maxDist;
            foreach (var n in sim.MapData.GetAllResourceNodes())
            {
                if (n.Type != type || n.IsDepleted || n.IsCarcass) continue;
                if (requireNotFarm && n.IsFarmNode) continue;
                int dx = n.TileX - x, dz = n.TileZ - z;
                int d = dx * dx + dz * dz;
                if (d < bestD) { bestD = d; best = n; }
            }
            return best;
        }

        private void TrySpawnNodeNear(MapRenderer mr, ResourceType type, int x, int z, int amount)
        {
            for (int r = 0; r <= 4; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                        if (mr.TrySpawnResourceNode(sim.MapData, type, new Vector3(x + dx + 1f, 0f, z + dz + 1f), amount))
                            return;
                    }
        }

        private static BuildingData FindWorkplace(BuildingType type, params BuildingData[] candidates)
        {
            foreach (var c in candidates)
                if (c != null && c.Type == type) return c;
            return null;
        }

        // ------------------------------------------------------------------ people

        private List<VillageJob> BuildJobPool(int farmCount, bool lumber, bool mine, bool mill,
            bool blacksmith, bool university, bool market, bool monastery, bool towers, bool tavern, int target)
        {
            var pool = new List<VillageJob>();
            void Add(VillageJob j, int n, bool ok) { if (ok) for (int i = 0; i < n; i++) pool.Add(j); }

            Add(VillageJob.Cook, 2, tavern);
            Add(VillageJob.Farmer, farmCount, farmCount > 0);
            Add(VillageJob.Forester, 5, lumber);
            Add(VillageJob.Miner, 3, mine);
            Add(VillageJob.Quarryman, 2, mine);
            Add(VillageJob.Forager, 2, mill);
            Add(VillageJob.Blacksmith, 3, blacksmith);
            Add(VillageJob.Student, 6, university);
            Add(VillageJob.Merchant, 3, market);
            Add(VillageJob.Monk, 2, monastery);
            Add(VillageJob.Guard, 2, towers);

            var fillers = new List<VillageJob>();
            if (university) fillers.Add(VillageJob.Student);
            if (lumber) fillers.Add(VillageJob.Forester);
            if (market) fillers.Add(VillageJob.Merchant);
            if (mine) fillers.Add(VillageJob.Miner);
            if (fillers.Count == 0) fillers.Add(VillageJob.Student);
            for (int i = 0; pool.Count < target + 8; i++)
                pool.Add(fillers[i % fillers.Count]);

            // Fisher–Yates with the deterministic RNG
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = (int)(Next() % (uint)(i + 1));
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            return pool;
        }

        private void AssignSchedule(VillagerProfile p) => VillageSchedules.Assign(p, ref rng);

        /// <summary>Starting age & needs: students are children, everyone else an adult (a few elders).</summary>
        private void AssignAgeAndNeeds(VillagerProfile p, int dayTicks, int childDays, int adultDays, int elderDays)
        {
            float ageDays;
            if (p.Job == VillageJob.Student)
            {
                ageDays = Rand(3, (childDays - 1) * 10) / 10f;                    // 0.3 .. childDays-0.1
                p.Stage = LifeStage.Child;
            }
            else if (Rand(0, 99) < 5)
            {
                ageDays = childDays + adultDays + Rand(2, 10) / 10f;              // freshly elderly
                p.Stage = LifeStage.Elder;
            }
            else
            {
                // Adults spread across the first two thirds of adulthood, so they don't all age out together.
                ageDays = childDays + Rand(2, Mathf.Max(3, adultDays * 10 * 2 / 3)) / 10f;
                p.Stage = LifeStage.Adult;
            }
            p.BirthTick = -Mathf.RoundToInt(ageDays * dayTicks);

            p.Quirky = Rand(0, 99) < 7; // a handful of eccentrics
            p.NextQuirkTick = Rand(600, 3000);

            p.Hunger = VillageRoutineSystem.NeedMax * Rand(60, 100) / 100;
            p.Energy = VillageRoutineSystem.NeedMax * Rand(88, 100) / 100; // everyone just woke up
            p.Social = VillageRoutineSystem.NeedMax * Rand(40, 100) / 100;
            p.Fun = VillageRoutineSystem.NeedMax * Rand(40, 100) / 100;
        }

        private string PickFirstName(HashSet<string> used, Gender gender)
        {
            var names = gender == Gender.Female ? VillageNames.Female : VillageNames.Male;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                string n = names[Next() % (uint)names.Length];
                if (used.Add(n)) return n;
            }
            return names[Next() % (uint)names.Length] + " Jr.";
        }

        private int Rand(int minInclusive, int maxInclusive)
        {
            return minInclusive + (int)(Next() % (uint)(maxInclusive - minInclusive + 1));
        }

        private uint Next()
        {
            // xorshift32
            rng ^= rng << 13;
            rng ^= rng >> 17;
            rng ^= rng << 5;
            return rng;
        }
    }
}
