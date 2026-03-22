using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public class GameSetup : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MapRenderer mapRenderer;
        [SerializeField] private UnitSelectionManager selectionManager;
        [SerializeField] private UnitInfoUI unitInfoUI;

        [Header("Prefabs")]
        [SerializeField] private GameObject villagerPrefab;
        [SerializeField] private GameObject spearmanPrefab;
        [SerializeField] private GameObject archerPrefab;
        [SerializeField] private GameObject horsemanPrefab;
        [SerializeField] private GameObject scoutPrefab;
        [SerializeField] private GameObject manAtArmsPrefab;
        [SerializeField] private GameObject knightPrefab;
        [SerializeField] private GameObject crossbowmanPrefab;
        [SerializeField] private GameObject monkPrefab;
        [SerializeField] private GameObject plagueMaskPrefab;
        [SerializeField] private GameObject longbowmanPrefab;
        [SerializeField] private GameObject gendarmePrefab;
        [SerializeField] private GameObject landsknechtPrefab;

        [Header("Markers")]
        [SerializeField] private Material markerMaterial;

        private static readonly Color[] ColdPalette = new Color[]
        {
            new Color(0.2f, 0.4f, 0.9f),   // Blue
            new Color(0.0f, 0.7f, 0.7f),   // Teal
            new Color(0.15f, 0.15f, 0.5f), // Navy
            new Color(0.4f, 0.2f, 0.85f),  // Purple
        };

        private static readonly Color[] WarmPalette = new Color[]
        {
            new Color(0.8f, 0.2f, 0.2f),  // Red
            new Color(0.9f, 0.5f, 0.1f),  // Orange
            new Color(0.9f, 0.8f, 0.1f),  // Yellow
            new Color(0.9f, 0.3f, 0.6f),  // Pink
        };

        public static Color[] PlayerColors = new Color[]
        {
            new Color(0.2f, 0.4f, 0.9f),  // Blue (default)
            new Color(0.8f, 0.2f, 0.2f),  // Red (default)
        };

        public static void ComputePlayerColors(int playerCount, int[] teamAssignments)
        {
            PlayerColors = new Color[Mathf.Max(playerCount, 2)];

            // Check if there are real teams (more than 2 distinct team IDs means team game,
            // but also 2v2 has exactly 2 teams — detect team game by checking if any two
            // players share the same team)
            bool isTeamGame = false;
            if (teamAssignments != null && teamAssignments.Length >= playerCount)
            {
                var teamCounts = new Dictionary<int, int>();
                for (int i = 0; i < playerCount; i++)
                {
                    int t = teamAssignments[i];
                    teamCounts.TryGetValue(t, out int c);
                    teamCounts[t] = c + 1;
                }
                foreach (var kv in teamCounts)
                {
                    if (kv.Value > 1) { isTeamGame = true; break; }
                }
            }

            if (isTeamGame)
            {
                // Team game: cold palette for team 0, warm for team 1
                var teamSlotIndex = new Dictionary<int, int>(); // team -> next slot
                for (int i = 0; i < playerCount; i++)
                {
                    int team = teamAssignments[i];
                    teamSlotIndex.TryGetValue(team, out int slot);
                    // First team encountered = cold (team 0), second = warm
                    // Determine which palette based on team ID: even = cold, odd = warm
                    Color[] palette = (team % 2 == 0) ? ColdPalette : WarmPalette;
                    PlayerColors[i] = palette[slot % palette.Length];
                    teamSlotIndex[team] = slot + 1;
                }
            }
            else
            {
                // 1v1 / singleplayer: alternate cold/warm
                int coldSlot = 0, warmSlot = 0;
                for (int i = 0; i < playerCount; i++)
                {
                    if (i % 2 == 0)
                        PlayerColors[i] = ColdPalette[coldSlot++ % ColdPalette.Length];
                    else
                        PlayerColors[i] = WarmPalette[warmSlot++ % WarmPalette.Length];
                }
            }

            // Fill any remaining slots (for materialCount padding)
            for (int i = playerCount; i < PlayerColors.Length; i++)
                PlayerColors[i] = (i % 2 == 0) ? ColdPalette[0] : WarmPalette[0];
        }

        private static Vector2Int[] GetFallbackBasePositions(int w, int h)
        {
            return new Vector2Int[]
            {
                new Vector2Int(w / 4, h / 4),
                new Vector2Int(3 * w / 4, 3 * h / 4),
                new Vector2Int(3 * w / 4, h / 4),
                new Vector2Int(w / 4, 3 * h / 4),
                new Vector2Int(w / 2, h / 6),
                new Vector2Int(w / 2, 5 * h / 6),
                new Vector2Int(w / 6, h / 2),
                new Vector2Int(5 * w / 6, h / 2),
            };
        }

        private Material[] playerMaterials;
        private Material[] playerSilhouetteMaterials;
        private Material buildingBodyMaterial;
        private Material unitStencilMat;

        // Cached shaders and shared materials to avoid per-building Shader.Find / new Material
        private static Shader cachedSelectionRingShader;
        private static Shader cachedUnlitShader;
        private Material sharedSelectionRingMat;
        private Dictionary<int, UnitView> unitViews = new Dictionary<int, UnitView>();
        private Dictionary<int, BuildingView> buildingViews = new Dictionary<int, BuildingView>();
        private Dictionary<int, ProjectileView> projectileViews = new Dictionary<int, ProjectileView>();
        private MeteorVisualManager meteorVisualManager;
        private GameObject[] markerPool;
        private MarkerFader[] markerFaders;
        private int activePreviewCount;
        private const int MarkerPoolSize = 100;

        // Building billboard sprites
        private Dictionary<string, Material> buildingSpriteMaterials = new Dictionary<string, Material>();

        // Facing arrow
        private GameObject facingArrow;
        private LineRenderer arrowShaft;
        private LineRenderer arrowHead;

        // Fog of war
        private FogOfWarRenderer fogRenderer;

        // Ghost building tracking
        private HashSet<int> knownEnemyBuildings = new HashSet<int>();
        private Dictionary<int, (int tileX, int tileZ)> ghostBuildings = new Dictionary<int, (int, int)>();
        private List<int> ghostCleanupList = new List<int>();

        // Resource node fog of war tracking
        private HashSet<int> knownResourceNodes = new HashSet<int>();

        private bool initialized = false;

        private void Start()
        {
            StartCoroutine(WaitAndInitialize());
        }

        private System.Collections.IEnumerator WaitAndInitialize()
        {
            while (GameBootstrapper.Instance == null || GameBootstrapper.Instance.Simulation == null)
                yield return null;

            InitializeGame(); // Will skip if already called by GameBootstrapper
        }

        private static void SetMaterialColor(Material mat, Color color)
        {
            if (mat.HasProperty("_Color1")) mat.SetColor("_Color1", color);
            else if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        }

        public void InitializeGame()
        {
            if (initialized) return;
            if (GameBootstrapper.Instance?.Simulation == null) return;
            initialized = true;

            var sim = GameBootstrapper.Instance.Simulation;

            // Compute warm/cold team colors before creating materials
            int playerCount = GameBootstrapper.Instance.PlayerCount;
            ComputePlayerColors(playerCount, sim.PlayerTeamIds);

            // Create player-colored materials
            // Use at least 2 slots so dummy units (playerId 1) have a valid material in single-player
            int materialCount = Mathf.Max(playerCount, 2);
            var baseMat = villagerPrefab.GetComponentInChildren<Renderer>().sharedMaterial;
            playerMaterials = new Material[materialCount];
            for (int i = 0; i < materialCount; i++)
            {
                playerMaterials[i] = new Material(baseMat);
                SetMaterialColor(playerMaterials[i], PlayerColors[i]);
            }

            // Neutral stone material for building bodies (roofs get player color)
            buildingBodyMaterial = new Material(baseMat);
            SetMaterialColor(buildingBodyMaterial, new Color(0.75f, 0.68f, 0.55f));

            // Create per-player silhouette materials
            var silhouetteShader = Shader.Find("Custom/Silhouette");
            playerSilhouetteMaterials = new Material[materialCount];
            for (int i = 0; i < materialCount; i++)
            {
                playerSilhouetteMaterials[i] = new Material(silhouetteShader);
                playerSilhouetteMaterials[i].SetColor("_SilhouetteColor",
                    new Color(PlayerColors[i].r, PlayerColors[i].g, PlayerColors[i].b, 0.5f));
                playerSilhouetteMaterials[i].renderQueue = 2451;
            }

            // Create shared stencil material for unit self-occlusion prevention
            var stencilShader = Shader.Find("Custom/UnitStencilWrite");
            unitStencilMat = new Material(stencilShader);

            // Cache shaders and create shared selection ring material
            cachedSelectionRingShader = Shader.Find("Custom/SelectionRing");
            cachedUnlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (cachedUnlitShader == null) cachedUnlitShader = Shader.Find("Unlit/Color");
            sharedSelectionRingMat = new Material(cachedSelectionRingShader);
            sharedSelectionRingMat.SetColor("_Color", new Color(0f, 1f, 0f, 0.5f));

            // Use computed base positions from map generation (falls back to hardcoded if unavailable)
            var basePositions = sim.MapData.BasePositions ?? GetFallbackBasePositions(sim.MapData.Width, sim.MapData.Height);
            if (basePositions.Length < playerCount)
            {
                Debug.LogError($"[GameSetup] basePositions has {basePositions.Length} entries but playerCount is {playerCount}. Using fallback positions.");
                basePositions = GetFallbackBasePositions(sim.MapData.Width, sim.MapData.Height);
            }

            // Initialize map (creates terrain mesh, water, resources)
            var basesUsed = new Vector2Int[playerCount];
            for (int p = 0; p < playerCount; p++)
                basesUsed[p] = basePositions[p];
            if (mapRenderer != null)
                mapRenderer.Initialize(sim.MapData, basesUsed, sim.Config.MapSeed);

            // Spawn each player's base (TC + villagers)
            for (int p = 0; p < playerCount; p++)
                SpawnPlayerBase(sim, p, basePositions[p].x, basePositions[p].y);

            // Recompute holeMap now that trees and buildings have been placed —
            // the initial ComputeHoleMap() in GameSimulation ran before any tiles
            // were marked as Building, leaving stale walkability data.
            sim.MapData.ComputeHoleMap();

            // Spawn neutral sheep
            sim.SpawnNeutralSheep();
            SpawnSheepViews(sim);

            // Pre-allocate formation marker pool
            InitMarkerPool();
            InitFacingArrow();

            // Fog of war overlay
            var fogGO = new GameObject("FogOfWar");
            fogRenderer = fogGO.AddComponent<FogOfWarRenderer>();
            int localPlayerId = selectionManager != null ? selectionManager.LocalPlayerId : 0;
            fogRenderer.Initialize(sim.MapData.Width, sim.MapData.Height, localPlayerId, sim.Config.TerrainHeightScale);

            // Set fog of war texture on billboard tree materials
            if (mapRenderer != null && mapRenderer.TreeMaterials != null)
            {
                foreach (var mat in mapRenderer.TreeMaterials)
                    if (mat != null) fogRenderer.SetFogTexture(mat);
            }

            // Initialize resource UI with local player ID
            var resourceUI = Object.FindFirstObjectByType<ResourceUI>();
            if (resourceUI != null)
                resourceUI.SetLocalPlayerId(localPlayerId);

            // Initialize chat UI with local player ID and name
            var chatUI = Object.FindFirstObjectByType<ChatUI>();
            if (chatUI != null)
            {
                var mm = MatchmakingManager.Instance;
                string playerName = (mm != null && !string.IsNullOrEmpty(mm.Username)) ? mm.Username : "Player";
                chatUI.SetLocalPlayer(localPlayerId, playerName);
            }

            // Create player list UI
            if (Object.FindFirstObjectByType<PlayerListUI>() == null)
            {
                var playerListGO = new GameObject("PlayerListUI");
                playerListGO.AddComponent<PlayerListUI>();
            }

            // God Power cooldown bar UI
            var godPowerUIGO = new GameObject("GodPowerBarUI");
            godPowerUIGO.AddComponent<GodPowerBarUI>();

            // Subscribe to events
            sim.OnUnitDied += HandleUnitDied;
            sim.OnBuildingDestroyed += HandleBuildingDestroyed;
            sim.OnUnitTrained += HandleUnitTrained;
            sim.OnProjectileCreated += HandleProjectileCreated;
            sim.OnProjectileHit += HandleProjectileHit;
            sim.OnBuildingCreated += HandleBuildingCreated;
            sim.OnUnitGarrisoned += HandleUnitGarrisoned;
            sim.OnUnitUngarrisoned += HandleUnitUngarrisoned;
            sim.OnSheepConverted += HandleSheepConverted;
            sim.OnSheepSlaughtered += HandleSheepSlaughtered;

            // Meteor visual manager
            var meteorGO = new GameObject("MeteorVisualManager");
            meteorVisualManager = meteorGO.AddComponent<MeteorVisualManager>();
            meteorVisualManager.Initialize(unitViews);
            sim.OnMeteorWarning += meteorVisualManager.HandleMeteorWarning;
            sim.OnMeteorImpact += meteorVisualManager.HandleMeteorImpact;
            sim.OnHealingRainWarning += meteorVisualManager.HandleHealingRainWarning;
            sim.OnHealingRainEnd += meteorVisualManager.HandleHealingRainEnd;
            sim.OnLightningStormWarning += meteorVisualManager.HandleLightningStormWarning;
            sim.OnLightningBolt += meteorVisualManager.HandleLightningBolt;
            sim.OnLightningStormEnd += meteorVisualManager.HandleLightningStormEnd;
            sim.OnTsunamiWarning += meteorVisualManager.HandleTsunamiWarning;
            sim.OnTsunamiImpact += meteorVisualManager.HandleTsunamiImpact;

            // Set camera bounds and center on local player's town center
            var cam = Object.FindFirstObjectByType<RTSCameraController>();
            if (cam != null)
            {
                cam.SetBounds(sim.MapData.Width, sim.MapData.Height);
                Vector2Int basePos = basePositions[localPlayerId];
                cam.PivotPosition = new Vector3(basePos.x, 0f, basePos.y);
            }

            // Pre-populate known sets so resources and enemy TCs show as ghosts
            // from the start (all tiles begin as Explored).
            int localPid = selectionManager != null ? selectionManager.LocalPlayerId : 0;
            foreach (var node in sim.MapData.GetAllResourceNodes())
                knownResourceNodes.Add(node.Id);
            foreach (var b in sim.BuildingRegistry.GetAllBuildings())
            {
                if (!sim.AreAllies(b.PlayerId, localPid))
                    knownEnemyBuildings.Add(b.Id);
            }

            Debug.Log($"GameSetup complete: {sim.UnitRegistry.Count} units, map {sim.MapData.Width}x{sim.MapData.Height}");
        }

        private void SpawnUnit(GameObject prefab, UnitData unitData, Vector3 spawnPos, int unitType = 0)
        {
            if (prefab == null) return;

            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim != null)
                spawnPos.y = sim.MapData.SampleHeight(spawnPos.x, spawnPos.z) * sim.Config.TerrainHeightScale;

            var go = Instantiate(prefab, spawnPos, Quaternion.identity);
            go.SetActive(true);

            // Apply player color + silhouette
            var mat = playerMaterials[unitData.PlayerId];
            var silMat = playerSilhouetteMaterials[unitData.PlayerId];
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var partName = r.gameObject.name;
                if (partName == "SelectionRing") continue;

                bool isTeamColored = partName.StartsWith("Body") || partName.StartsWith("Sphere");
                var primaryMat = isTeamColored ? mat : r.sharedMaterial;
                r.sharedMaterials = new Material[] { primaryMat, unitStencilMat, silMat };
            }

            var unitView = go.GetComponent<UnitView>();
            if (unitView != null)
            {
                var mapData = sim?.MapData;
                float hs = sim?.Config.TerrainHeightScale ?? 0f;
                unitView.Initialize(unitData.Id, spawnPos, unitData, unitType, mapData, hs,
                    unitStencilMat, playerSilhouetteMaterials[unitData.PlayerId]);
                unitViews[unitData.Id] = unitView;

                if (selectionManager != null)
                    selectionManager.RegisterUnitView(unitView);
            }
        }

        private void SpawnPlayerBase(GameSimulation sim, int playerId, int tileX, int tileZ)
        {
            // Validate TC footprint is walkable (safety net against water spawns)
            int tcW = sim.Config.TownCenterFootprintWidth;
            int tcH = sim.Config.TownCenterFootprintHeight;
            for (int x = tileX; x < tileX + tcW; x++)
                for (int z = tileZ; z < tileZ + tcH; z++)
                    if (!sim.MapData.IsWalkable(x, z))
                        sim.MapData.Tiles[x, z] = TileType.Grass;

            // Town Center (mark as main/initial town center)
            var tc = sim.CreateBuilding(playerId, BuildingType.TownCenter, tileX, tileZ, underConstruction: false, isMainTownCenter: true);
            SpawnBuilding(tc);

            // Starting resources
            sim.ResourceManager.AddResource(playerId, ResourceType.Food, sim.Config.StartingFood);
            sim.ResourceManager.AddResource(playerId, ResourceType.Wood, sim.Config.StartingWood);
            sim.ResourceManager.AddResource(playerId, ResourceType.Gold, sim.Config.StartingGold);
            sim.ResourceManager.AddResource(playerId, ResourceType.Stone, sim.Config.StartingStone);

            // 6 villagers in a 3x2 grid, offset ~3 tiles from TC center
            float spacing = 1.5f;
            float baseX = tileX + 2f; // TC center is at tileX + footprint/2
            float baseZ = tileZ - 3f; // spawn below TC
            for (int i = 0; i < 6; i++)
            {
                int col = i % 3;
                int row = i / 3;
                Vector3 spawnPos = new Vector3(baseX + col * spacing, 0f, baseZ + row * spacing);

                // Validate villager spawn tile is walkable (safety net)
                Vector2Int spawnTile = sim.MapData.WorldToTile(spawnPos);
                if (!sim.MapData.IsWalkable(spawnTile.x, spawnTile.y))
                    sim.MapData.Tiles[spawnTile.x, spawnTile.y] = TileType.Grass;

                FixedVector3 fixedPos = FixedVector3.FromVector3(spawnPos);
                var unitData = sim.UnitRegistry.CreateUnit(playerId, fixedPos,
                    sim.ConfigToFixed32(sim.Config.UnitMoveSpeed),
                    sim.ConfigToFixed32(sim.Config.UnitRadius),
                    sim.ConfigToFixed32(sim.Config.VillagerMass));
                unitData.MaxHealth = sim.Config.VillagerMaxHealth;
                unitData.CurrentHealth = unitData.MaxHealth;
                unitData.AttackDamage = sim.Config.VillagerAttackDamage;
                unitData.AttackRange = sim.ConfigToFixed32(sim.Config.VillagerAttackRange);
                unitData.AttackCooldownTicks = sim.Config.VillagerAttackCooldownTicks;
                unitData.UnitType = 0;
                unitData.MeleeArmor = sim.Config.VillagerMeleeArmor;
                unitData.RangedArmor = sim.Config.VillagerRangedArmor;
                unitData.DetectionRange = sim.ConfigToFixed32(sim.Config.VillagerDetectionRange);
                unitData.CarryCapacity = sim.Config.VillagerCarryCapacity;
                unitData.IsVillager = true;
                SpawnUnit(villagerPrefab, unitData, spawnPos);
            }

            // Starting scout — spawned east of the villager grid
            {
                Vector3 scoutSpawnPos = new Vector3(baseX + 3 * spacing + 1f, 0f, baseZ);
                Vector2Int scoutTile = sim.MapData.WorldToTile(scoutSpawnPos);
                if (!sim.MapData.IsWalkable(scoutTile.x, scoutTile.y))
                    sim.MapData.Tiles[scoutTile.x, scoutTile.y] = TileType.Grass;

                FixedVector3 fixedPos = FixedVector3.FromVector3(scoutSpawnPos);
                var scoutData = sim.UnitRegistry.CreateUnit(playerId, fixedPos,
                    sim.ConfigToFixed32(sim.Config.ScoutMoveSpeed),
                    sim.ConfigToFixed32(sim.Config.CavalryRadius),
                    sim.ConfigToFixed32(sim.Config.ScoutMass));
                scoutData.MaxHealth = sim.Config.ScoutMaxHealth;
                scoutData.CurrentHealth = scoutData.MaxHealth;
                scoutData.AttackDamage = sim.Config.ScoutAttackDamage;
                scoutData.AttackRange = sim.ConfigToFixed32(sim.Config.ScoutAttackRange);
                scoutData.AttackCooldownTicks = sim.Config.ScoutAttackCooldownTicks;
                scoutData.UnitType = 4;
                scoutData.MeleeArmor = sim.Config.ScoutMeleeArmor;
                scoutData.RangedArmor = sim.Config.ScoutRangedArmor;
                scoutData.DetectionRange = sim.ConfigToFixed32(sim.Config.ScoutDetectionRange);
                SpawnUnit(scoutPrefab, scoutData, scoutSpawnPos, 4);
            }

            // 3 starting sheep — spawned west of the villager grid
            for (int i = 0; i < 3; i++)
            {
                Vector3 sheepSpawnPos = new Vector3(baseX - 2f - i * 1f, 0f, baseZ + i * 0.5f);
                Vector2Int sheepTile = sim.MapData.WorldToTile(sheepSpawnPos);
                if (!sim.MapData.IsWalkable(sheepTile.x, sheepTile.y))
                    sim.MapData.Tiles[sheepTile.x, sheepTile.y] = TileType.Grass;

                FixedVector3 fixedPos = FixedVector3.FromVector3(sheepSpawnPos);
                var sheepData = sim.UnitRegistry.CreateUnit(playerId, fixedPos,
                    Fixed32.FromFloat(sim.Config.SheepMoveSpeed),
                    Fixed32.FromFloat(sim.Config.SheepRadius),
                    Fixed32.FromFloat(sim.Config.SheepMass));
                sheepData.UnitType = 5;
                sheepData.IsSheep = true;
                sheepData.MaxHealth = sim.Config.SheepMaxHealth;
                sheepData.CurrentHealth = sheepData.MaxHealth;
                sheepData.AttackDamage = 0;
                sheepData.AttackRange = Fixed32.Zero;
                sheepData.AttackCooldownTicks = 999;
                sheepData.DetectionRange = Fixed32.FromFloat(2f);

                SpawnSheepView(sheepData, sheepSpawnPos);
            }
        }

        private void InitMarkerPool()
        {
            markerPool = new GameObject[MarkerPoolSize];
            markerFaders = new MarkerFader[MarkerPoolSize];
            var holder = new GameObject("FormationMarkers");
            for (int i = 0; i < MarkerPoolSize; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "FormationMarker";
                go.transform.SetParent(holder.transform);
                go.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                // Remove collider — markers are visual only
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);

                if (markerMaterial != null)
                    go.GetComponent<MeshRenderer>().sharedMaterial = markerMaterial;

                markerFaders[i] = go.AddComponent<MarkerFader>();
                go.SetActive(false);
                markerPool[i] = go;
            }
        }

        private void InitFacingArrow()
        {
            facingArrow = new GameObject("FacingArrow");

            var shaftGO = new GameObject("Shaft");
            shaftGO.transform.SetParent(facingArrow.transform);
            arrowShaft = shaftGO.AddComponent<LineRenderer>();
            arrowShaft.useWorldSpace = true;
            arrowShaft.positionCount = 2;
            arrowShaft.startWidth = 0.15f;
            arrowShaft.endWidth = 0.15f;

            var headGO = new GameObject("Head");
            headGO.transform.SetParent(facingArrow.transform);
            arrowHead = headGO.AddComponent<LineRenderer>();
            arrowHead.useWorldSpace = true;
            arrowHead.positionCount = 3;
            arrowHead.startWidth = 0.15f;
            arrowHead.endWidth = 0.15f;

            if (markerMaterial != null)
            {
                var arrowMat = new Material(markerMaterial);
                arrowMat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always); // Always — render on top of terrain
                arrowMat.renderQueue = 3000;
                arrowShaft.sharedMaterial = arrowMat;
                arrowHead.sharedMaterial = arrowMat;
            }

            facingArrow.SetActive(false);
        }

        public void ShowFacingArrow(Vector3 center, Vector3 facingDir)
        {
            float shaftLength = 3f;
            float headLength = 0.8f;
            float headSpread = 0.5f;
            float yOffset = 0.1f;

            Vector3 start = center + Vector3.up * yOffset;
            Vector3 tip = start + facingDir * shaftLength;
            Vector3 headBack = tip - facingDir * headLength;
            Vector3 perp = Vector3.Cross(Vector3.up, facingDir).normalized;

            arrowShaft.SetPosition(0, start);
            arrowShaft.SetPosition(1, tip);

            arrowHead.SetPosition(0, headBack + perp * headSpread);
            arrowHead.SetPosition(1, tip);
            arrowHead.SetPosition(2, headBack - perp * headSpread);

            facingArrow.SetActive(true);
        }

        public void HideFacingArrow()
        {
            if (facingArrow != null)
                facingArrow.SetActive(false);
        }

        public void ShowMarkers(List<Vector3> positions)
        {
            int count = Mathf.Min(positions.Count, MarkerPoolSize);
            for (int i = 0; i < count; i++)
            {
                var marker = markerPool[i];
                marker.transform.position = positions[i] + new Vector3(0f, 0.15f, 0f);
                markerFaders[i].Preview = false;
                marker.SetActive(false);
                marker.SetActive(true);
            }
        }

        public void PreviewMarkers(List<Vector3> positions)
        {
            if (markerPool == null) return;

            // Deactivate previously active preview markers
            for (int i = 0; i < activePreviewCount; i++)
                markerPool[i].SetActive(false);

            int count = Mathf.Min(positions.Count, MarkerPoolSize);
            for (int i = 0; i < count; i++)
            {
                var marker = markerPool[i];
                marker.transform.position = positions[i] + new Vector3(0f, 0.15f, 0f);
                markerFaders[i].Preview = true;
                marker.SetActive(false);
                marker.SetActive(true);
            }
            activePreviewCount = count;
        }

        public void HidePreview()
        {
            if (markerPool == null) return;
            for (int i = 0; i < activePreviewCount; i++)
            {
                markerFaders[i].Preview = false;
                markerPool[i].SetActive(false);
            }
            activePreviewCount = 0;
        }

        public void CommitMarkers()
        {
            for (int i = 0; i < activePreviewCount; i++)
                markerFaders[i].Preview = false;
            activePreviewCount = 0;
        }

        public static Vector3 SnapClickToNearestWalkable(MapData map, Vector3 clickPoint)
        {
            Vector2Int clickTile = new Vector2Int(
                Mathf.FloorToInt(clickPoint.x), Mathf.FloorToInt(clickPoint.z));
            if (map.IsWalkable(clickTile.x, clickTile.y)) return clickPoint;

            Vector2Int snapped = GridPathfinder.FindNearestWalkableTile(map, clickTile, 20);
            if (snapped.x < 0) return clickPoint;
            return new Vector3(snapped.x + 0.5f, clickPoint.y, snapped.y + 0.5f);
        }

        public static void SnapToWalkable(MapData map, List<Vector3> positions)
        {
            if (positions.Count == 0) return;

            // Compute centroid as connectivity anchor
            float sumX = 0f, sumZ = 0f;
            for (int i = 0; i < positions.Count; i++)
            {
                sumX += positions[i].x;
                sumZ += positions[i].z;
            }
            Vector2Int anchorTile = new Vector2Int(
                Mathf.FloorToInt(sumX / positions.Count),
                Mathf.FloorToInt(sumZ / positions.Count));

            // Snap anchor to walkable if needed
            if (!map.IsWalkable(anchorTile.x, anchorTile.y))
                anchorTile = GridPathfinder.FindNearestWalkableTile(map, anchorTile, 20);

            if (anchorTile.x < 0) return;

            // Compute flood radius from max Chebyshev distance
            int maxChebyshev = 0;
            for (int i = 0; i < positions.Count; i++)
            {
                int tx = Mathf.FloorToInt(positions[i].x);
                int tz = Mathf.FloorToInt(positions[i].z);
                int chebyshev = Mathf.Max(Mathf.Abs(tx - anchorTile.x), Mathf.Abs(tz - anchorTile.y));
                if (chebyshev > maxChebyshev) maxChebyshev = chebyshev;
            }
            int floodRadius = maxChebyshev * 2;

            var reachable = GridPathfinder.FloodFillWalkable(map, anchorTile, floodRadius);
            if (reachable.Count == 0) return;

            int mapWidth = map.Width;
            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 pos = positions[i];
                int tileX = Mathf.FloorToInt(pos.x);
                int tileZ = Mathf.FloorToInt(pos.z);
                int key = tileZ * mapWidth + tileX;

                if (reachable.Contains(key)) continue;

                // Position is disconnected or unwalkable — walk toward anchor
                Vector2Int snapped = GridPathfinder.FindConnectedTileToward(
                    new Vector2Int(tileX, tileZ), anchorTile, reachable, mapWidth, floodRadius);
                if (snapped.x >= 0)
                    positions[i] = new Vector3(snapped.x + 0.5f, pos.y, snapped.y + 0.5f);
            }
        }

        public static List<Vector3> ComputeGridFormation(Vector3 center, int unitCount, float spacing = 1.0f)
        {
            var positions = new List<Vector3>(unitCount);
            if (unitCount <= 0) return positions;

            if (unitCount == 1)
            {
                positions.Add(center);
                return positions;
            }

            int cols = Mathf.CeilToInt(Mathf.Sqrt(unitCount));
            int rows = Mathf.CeilToInt((float)unitCount / cols);

            float offsetX = (cols - 1) * spacing * 0.5f;
            float offsetZ = (rows - 1) * spacing * 0.5f;

            for (int i = 0; i < unitCount; i++)
            {
                int col = i % cols;
                int row = i / cols;
                Vector3 pos = center + new Vector3(col * spacing - offsetX, 0f, row * spacing - offsetZ);
                positions.Add(pos);
            }

            return positions;
        }

        public static List<Vector3> ComputeLineFormation(Vector3 lineStart, Vector3 lineEnd, int unitCount, float rowSpacing = 1.2f)
        {
            var positions = new List<Vector3>(unitCount);
            if (unitCount <= 0) return positions;

            Vector3 center = (lineStart + lineEnd) * 0.5f;

            if (unitCount == 1)
            {
                positions.Add(center);
                return positions;
            }

            Vector3 lineVec = lineEnd - lineStart;
            float lineLength = lineVec.magnitude;

            if (lineLength < 0.5f)
            {
                return ComputeGridFormation(center, unitCount);
            }

            Vector3 lineDir = lineVec / lineLength;
            Vector3 depthDir = Vector3.Cross(Vector3.up, lineDir);

            float minSpacing = 1.0f;
            int numCols = Mathf.Max(1, Mathf.Min(unitCount, Mathf.FloorToInt(lineLength / minSpacing) + 1));
            float colSpacing = lineLength / numCols;
            int numRows = Mathf.CeilToInt((float)unitCount / numCols);

            for (int i = 0; i < unitCount; i++)
            {
                int col = i % numCols;
                int row = i / numCols;
                Vector3 pos = lineStart + lineDir * ((col + 0.5f) * colSpacing) + depthDir * (row * rowSpacing);
                positions.Add(pos);
            }

            return positions;
        }

        /// <summary>
        /// Line formation where each group occupies its own row(s), never mixing types within a row.
        /// Groups are ordered front-to-back. Positions are returned in group order.
        /// </summary>
        public static List<Vector3> ComputeGroupedLineFormation(Vector3 lineStart, Vector3 lineEnd, int[] groupSizes, float rowSpacing = 1.2f)
        {
            int totalUnits = 0;
            for (int g = 0; g < groupSizes.Length; g++) totalUnits += groupSizes[g];

            var positions = new List<Vector3>(totalUnits);
            if (totalUnits <= 0) return positions;

            Vector3 center = (lineStart + lineEnd) * 0.5f;

            if (totalUnits == 1)
            {
                positions.Add(center);
                return positions;
            }

            Vector3 lineVec = lineEnd - lineStart;
            float lineLength = lineVec.magnitude;

            if (lineLength < 0.5f)
                return ComputeGridFormation(center, totalUnits);

            Vector3 lineDir = lineVec / lineLength;
            Vector3 depthDir = Vector3.Cross(Vector3.up, lineDir);

            float minSpacing = 1.0f;
            int maxGroupSize = 0;
            for (int g = 0; g < groupSizes.Length; g++)
                if (groupSizes[g] > maxGroupSize) maxGroupSize = groupSizes[g];
            int numCols = Mathf.Max(1, Mathf.Min(maxGroupSize, Mathf.FloorToInt(lineLength / minSpacing) + 1));
            float colSpacing = lineLength / numCols;

            int currentRow = 0;
            for (int g = 0; g < groupSizes.Length; g++)
            {
                int groupCount = groupSizes[g];
                if (groupCount == 0) continue;

                int groupRows = Mathf.CeilToInt((float)groupCount / numCols);
                for (int i = 0; i < groupCount; i++)
                {
                    int col = i % numCols;
                    int localRow = i / numCols;
                    bool isLastRow = (localRow == groupRows - 1) && (groupCount % numCols != 0);
                    int rowUnits = isLastRow ? (groupCount % numCols) : numCols;
                    float centerOffset = (numCols - rowUnits) * colSpacing * 0.5f;
                    Vector3 pos = lineStart + lineDir * ((col + 0.5f) * colSpacing + centerOffset)
                                + depthDir * ((currentRow + localRow) * rowSpacing);
                    positions.Add(pos);
                }

                currentRow += groupRows;
            }

            return positions;
        }

        /// <summary>
        /// Scales each group's formation offsets from center by groupRadii[g] / standardRadius,
        /// matching the simulation's per-unit radius scaling.
        /// </summary>
        public static void ScaleFormationByRadius(List<Vector3> positions, int[] groupSizes, float[] groupRadii, float standardRadius)
        {
            if (positions.Count == 0) return;

            // Compute center of all positions
            Vector3 center = Vector3.zero;
            for (int i = 0; i < positions.Count; i++)
                center += positions[i];
            center /= positions.Count;

            int posIdx = 0;
            for (int g = 0; g < groupSizes.Length; g++)
            {
                float scale = groupRadii[g] / standardRadius;
                if (Mathf.Approximately(scale, 1f))
                {
                    posIdx += groupSizes[g];
                    continue;
                }
                for (int i = 0; i < groupSizes[g]; i++)
                {
                    Vector3 offset = positions[posIdx] - center;
                    positions[posIdx] = center + new Vector3(offset.x * scale, offset.y, offset.z * scale);
                    posIdx++;
                }
            }
        }

        private Material GetOrCreateBuildingSpriteMaterial(string spriteName)
        {
            if (buildingSpriteMaterials.TryGetValue(spriteName, out var existing))
                return existing;

            var tex = Resources.Load<Texture2D>($"BuildingSprites/{spriteName}");
            if (tex == null) return null;

            var shader = Shader.Find("OpenEmpires/Billboard");
            if (shader == null) return null;

            var mat = new Material(shader);
            mat.SetTexture("_MainTex", tex);
            mat.SetColor("_Color", Color.white);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry + 1;
            mat.enableInstancing = true;
            buildingSpriteMaterials[spriteName] = mat;

            // Set fog of war texture if available
            if (fogRenderer != null)
                fogRenderer.SetFogTexture(mat);

            return mat;
        }

        private GameObject CreateBuildingSpritePrefab(string name, string spriteName, int footprintW, int footprintH, float spriteScale = 5f)
        {
            var mat = GetOrCreateBuildingSpriteMaterial(spriteName);
            if (mat == null)
                return null; // Fallback to procedural

            var building = new GameObject(name);
            building.layer = 11;

            // Billboard sprite quad
            var spriteGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            spriteGo.name = "Sprite";
            spriteGo.layer = 11;
            var mc = spriteGo.GetComponent<MeshCollider>();
            if (mc != null) Object.Destroy(mc);

            spriteGo.transform.SetParent(building.transform);
            spriteGo.transform.localPosition = new Vector3(0f, spriteScale * 0.2f, 0f);
            spriteGo.transform.localScale = new Vector3(spriteScale, spriteScale, 1f);
            spriteGo.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // Box collider on root for selection
            var col = building.AddComponent<BoxCollider>();
            float colW = footprintW * 0.85f;
            float colH = footprintH * 0.85f;
            col.center = new Vector3(0f, 0.8f, 0f);
            col.size = new Vector3(colW, 1.6f, colH);

            // Selection ring
            float ringSize = Mathf.Max(footprintW, footprintH) + 0.6f;
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ring.name = "SelectionRing";
            ring.transform.SetParent(building.transform);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(ringSize, 0.01f, ringSize);
            ring.layer = 11;
            var ringCollider = ring.GetComponent<Collider>();
            if (ringCollider != null) Object.Destroy(ringCollider);
            ring.GetComponent<Renderer>().sharedMaterial = sharedSelectionRingMat;

            var view = building.AddComponent<BuildingView>();
            view.SetSelectionRing(ring);

            return building;
        }

        private GameObject CreateHousePrefab(int playerId)
        {
            var house = new GameObject("House");
            house.layer = 11; // Building layer

            // Body (main cube)
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(house.transform);
            body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            body.transform.localScale = new Vector3(1.6f, 1.0f, 1.6f);
            body.layer = 11;

            // Roof (scaled cube, rotated 45 deg)
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(house.transform);
            roof.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            roof.transform.localScale = new Vector3(1.2f, 0.6f, 1.2f);
            roof.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            roof.layer = 11;

            // Remove individual colliders
            Object.Destroy(body.GetComponent<Collider>());
            Object.Destroy(roof.GetComponent<Collider>());

            // Single box collider on root
            var col = house.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.8f, 0f);
            col.size = new Vector3(1.8f, 1.6f, 1.8f);

            // Selection reticle (square for buildings)
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ring.name = "SelectionRing";
            ring.transform.SetParent(house.transform);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(2.4f, 0.01f, 2.4f);
            ring.layer = 11;

            var ringCollider = ring.GetComponent<Collider>();
            if (ringCollider != null) Object.Destroy(ringCollider);

            ring.GetComponent<Renderer>().sharedMaterial = sharedSelectionRingMat;

            // Add BuildingView component
            var view = house.AddComponent<BuildingView>();
            view.SetSelectionRing(ring);

            return house;
        }

        private GameObject CreateBarracksPrefab(int playerId)
        {
            var barracks = new GameObject("Barracks");
            barracks.layer = 11;

            // Body (wider, taller cube)
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(barracks.transform);
            body.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            body.transform.localScale = new Vector3(2.6f, 1.4f, 2.6f);
            body.layer = 11;

            // Roof (flat wide slab)
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(barracks.transform);
            roof.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            roof.transform.localScale = new Vector3(2.8f, 0.2f, 2.8f);
            roof.layer = 11;

            // Tower (tall pillar on one corner)
            var tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.name = "Tower";
            tower.transform.SetParent(barracks.transform);
            tower.transform.localPosition = new Vector3(1.0f, 1.2f, 1.0f);
            tower.transform.localScale = new Vector3(0.6f, 2.4f, 0.6f);
            tower.layer = 11;

            // Remove individual colliders
            Object.Destroy(body.GetComponent<Collider>());
            Object.Destroy(roof.GetComponent<Collider>());
            Object.Destroy(tower.GetComponent<Collider>());

            // Single box collider on root
            var col = barracks.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.0f, 0f);
            col.size = new Vector3(2.8f, 2.0f, 2.8f);

            // Selection reticle (square for buildings)
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ring.name = "SelectionRing";
            ring.transform.SetParent(barracks.transform);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(3.6f, 0.01f, 3.6f);
            ring.layer = 11;

            var ringCollider = ring.GetComponent<Collider>();
            if (ringCollider != null) Object.Destroy(ringCollider);

            ring.GetComponent<Renderer>().sharedMaterial = sharedSelectionRingMat;

            var view = barracks.AddComponent<BuildingView>();
            view.SetSelectionRing(ring);

            return barracks;
        }

        private GameObject CreateTownCenterPrefab(int playerId)
        {
            var tc = new GameObject("TownCenter");
            tc.layer = 11;

            // Body (wide, tall cube)
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(tc.transform);
            body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            body.transform.localScale = new Vector3(3.6f, 1.6f, 3.6f);
            body.layer = 11;

            // Roof (flat slab)
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(tc.transform);
            roof.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            roof.transform.localScale = new Vector3(3.8f, 0.2f, 3.8f);
            roof.layer = 11;

            // Central tower pillar
            var tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.name = "Tower";
            tower.transform.SetParent(tc.transform);
            tower.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            tower.transform.localScale = new Vector3(0.8f, 3.0f, 0.8f);
            tower.layer = 11;

            // Remove individual colliders
            Object.Destroy(body.GetComponent<Collider>());
            Object.Destroy(roof.GetComponent<Collider>());
            Object.Destroy(tower.GetComponent<Collider>());

            // Single box collider on root
            var col = tc.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.2f, 0f);
            col.size = new Vector3(3.8f, 2.4f, 3.8f);

            // Selection reticle (square for buildings)
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ring.name = "SelectionRing";
            ring.transform.SetParent(tc.transform);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(4.8f, 0.01f, 4.8f);
            ring.layer = 11;

            var ringCollider = ring.GetComponent<Collider>();
            if (ringCollider != null) Object.Destroy(ringCollider);

            ring.GetComponent<Renderer>().sharedMaterial = sharedSelectionRingMat;

            var view = tc.AddComponent<BuildingView>();
            view.SetSelectionRing(ring);

            // Attack range indicator ring (shown when selected)
            // Use subsequent TC range for ghost mode (player is building a new one)
            float attackRange = GameBootstrapper.Instance?.Simulation?.Config.SubsequentTownCenterAttackRange ?? 16f;
            float rangeDiameter = attackRange * 2f;
            var rangeRingGO = CreateRangeRing(tc.transform, rangeDiameter);
            view.SetRangeRing(rangeRingGO);

            // For English players, TC is the influence source — add influence zone
            var sim2 = GameBootstrapper.Instance?.Simulation;
            if (sim2 != null && sim2.GetInfluenceBuildingType(playerId) == BuildingType.TownCenter)
            {
                int influenceRadius = sim2.Config.MillInfluenceRadius;
                int tcFootW = sim2.Config.TownCenterFootprintWidth;
                int tcFootH = sim2.Config.TownCenterFootprintHeight;
                float halfX = (tcFootW + 2 * influenceRadius) * 0.5f;
                float halfZ = (tcFootH + 2 * influenceRadius) * 0.5f;

                var zone = new GameObject("InfluenceZone");
                zone.transform.SetParent(tc.transform);
                zone.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                zone.layer = 11;
                var zoneLR = zone.AddComponent<LineRenderer>();
                zoneLR.useWorldSpace = false;
                zoneLR.loop = true;
                zoneLR.positionCount = 4;
                zoneLR.SetPosition(0, new Vector3(-halfX, 0f, -halfZ));
                zoneLR.SetPosition(1, new Vector3(halfX, 0f, -halfZ));
                zoneLR.SetPosition(2, new Vector3(halfX, 0f, halfZ));
                zoneLR.SetPosition(3, new Vector3(-halfX, 0f, halfZ));
                zoneLR.startWidth = 0.08f;
                zoneLR.endWidth = 0.08f;
                var zoneMat = new Material(cachedUnlitShader);
                zoneMat.color = new Color(1f, 0.6f, 0f, 0.8f);
                zoneMat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                zoneMat.renderQueue = 3000;
                zoneLR.material = zoneMat;
                zoneLR.startColor = new Color(1f, 0.6f, 0f, 0.8f);
                zoneLR.endColor = new Color(1f, 0.6f, 0f, 0.8f);
                view.SetInfluenceZone(zone);
            }

            return tc;
        }

        private GameObject CreateRangeRing(Transform parent, float diameter)
        {
            // Create a hollow ring using a thin cylinder
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "RangeRing";
            ring.transform.SetParent(parent);
            ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            ring.transform.localScale = new Vector3(diameter, 0.005f, diameter);
            ring.layer = 11;

            var col = ring.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            var mat = new Material(cachedUnlitShader);
            mat.color = new Color(1f, 0.3f, 0.3f, 0.15f);
            mat.SetFloat("_Surface", 1);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
            ring.GetComponent<Renderer>().sharedMaterial = mat;

            return ring;
        }

        private GameObject CreateWallPrefab(int playerId)
        {
            var wall = new GameObject("Wall");
            wall.layer = 11; // Building layer

            // Wall geometry container (toggled off when converted to gate)
            var wallGeo = new GameObject("WallGeometry");
            wallGeo.transform.SetParent(wall.transform);
            wallGeo.transform.localPosition = Vector3.zero;

            // Body (fills full tile so adjacent walls form a continuous surface)
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(wallGeo.transform);
            body.transform.localPosition = new Vector3(0f, 0.375f, 0f);
            body.transform.localScale = new Vector3(1.0f, 0.75f, 1.0f);
            body.layer = 11;

            // Walkway ledge (thin horizontal band creating a shadow line at the top)
            var ledge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ledge.name = "Ledge";
            ledge.transform.SetParent(wallGeo.transform);
            ledge.transform.localPosition = new Vector3(0f, 0.775f, 0f);
            ledge.transform.localScale = new Vector3(1.0f, 0.05f, 1.0f);
            ledge.layer = 11;

            // Merlons (4 corner battlements)
            float mx = 0.35f, mz = 0.35f, my = 0.89f;
            Vector3 merlonScale = new Vector3(0.28f, 0.18f, 0.28f);
            var m0 = CreateWallMerlon(wallGeo.transform, new Vector3(-mx, my, -mz), merlonScale);
            m0.name = "Merlon_NxNz";
            var m1 = CreateWallMerlon(wallGeo.transform, new Vector3( mx, my, -mz), merlonScale);
            m1.name = "Merlon_PxNz";
            var m2 = CreateWallMerlon(wallGeo.transform, new Vector3(-mx, my,  mz), merlonScale);
            m2.name = "Merlon_NxPz";
            var m3 = CreateWallMerlon(wallGeo.transform, new Vector3( mx, my,  mz), merlonScale);
            m3.name = "Merlon_PxPz";

            // Remove individual colliders
            Object.Destroy(body.GetComponent<Collider>());
            Object.Destroy(ledge.GetComponent<Collider>());
            Object.Destroy(m0.GetComponent<Collider>());
            Object.Destroy(m1.GetComponent<Collider>());
            Object.Destroy(m2.GetComponent<Collider>());
            Object.Destroy(m3.GetComponent<Collider>());

            // Single box collider on root
            var col = wall.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.45f, 0f);
            col.size = new Vector3(1.0f, 0.9f, 1.0f);

            // Selection reticle (square for buildings)
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ring.name = "SelectionRing";
            ring.transform.SetParent(wall.transform);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(1.1f, 0.01f, 1.1f);
            ring.layer = 11;

            var ringCollider = ring.GetComponent<Collider>();
            if (ringCollider != null) Object.Destroy(ringCollider);

            ring.GetComponent<Renderer>().sharedMaterial = sharedSelectionRingMat;

            var view = wall.AddComponent<BuildingView>();
            view.SetSelectionRing(ring);

            return wall;
        }

        private GameObject CreateMillPrefab(int playerId)
        {
            var mill = new GameObject("Mill");
            mill.layer = 11;

            // Base (wide, low cube)
            var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseObj.name = "Base";
            baseObj.transform.SetParent(mill.transform);
            baseObj.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            baseObj.transform.localScale = new Vector3(1.6f, 0.6f, 1.6f);
            baseObj.layer = 11;

            // Roof (pyramid-like, rotated cube)
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(mill.transform);
            roof.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            roof.transform.localScale = new Vector3(1.3f, 0.5f, 1.3f);
            roof.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            roof.layer = 11;

            // Grain sack (small sphere on one side)
            var sack = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sack.name = "GrainSack";
            sack.transform.SetParent(mill.transform);
            sack.transform.localPosition = new Vector3(0.5f, 0.25f, 0.5f);
            sack.transform.localScale = new Vector3(0.4f, 0.35f, 0.4f);
            sack.layer = 11;

            Object.Destroy(baseObj.GetComponent<Collider>());
            Object.Destroy(roof.GetComponent<Collider>());
            Object.Destroy(sack.GetComponent<Collider>());

            var col = mill.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.6f, 0f);
            col.size = new Vector3(1.8f, 1.2f, 1.8f);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ring.name = "SelectionRing";
            ring.transform.SetParent(mill.transform);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(2.4f, 0.01f, 2.4f);
            ring.layer = 11;
            var ringCollider = ring.GetComponent<Collider>();
            if (ringCollider != null) Object.Destroy(ringCollider);
            ring.GetComponent<Renderer>().sharedMaterial = sharedSelectionRingMat;

            var view = mill.AddComponent<BuildingView>();
            view.SetSelectionRing(ring);

            // Influence zone outline (visible during Mill construction) — only for civs that use Mill as influence source
            var simRef = GameBootstrapper.Instance?.Simulation;
            if (simRef == null || simRef.GetInfluenceBuildingType(playerId) == BuildingType.Mill)
            {
                int influenceRadius = simRef?.Config?.MillInfluenceRadius ?? 6;
                int footprintW = simRef?.Config?.MillFootprintWidth ?? 2;
                int footprintH = simRef?.Config?.MillFootprintHeight ?? 2;
                float halfX = (footprintW + 2 * influenceRadius) * 0.5f;
                float halfZ = (footprintH + 2 * influenceRadius) * 0.5f;

                var zone = new GameObject("InfluenceZone");
                zone.transform.SetParent(mill.transform);
                zone.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                zone.layer = 11;
                var zoneLR = zone.AddComponent<LineRenderer>();
                zoneLR.useWorldSpace = false;
                zoneLR.loop = true;
                zoneLR.positionCount = 4;
                zoneLR.SetPosition(0, new Vector3(-halfX, 0f, -halfZ));
                zoneLR.SetPosition(1, new Vector3(halfX, 0f, -halfZ));
                zoneLR.SetPosition(2, new Vector3(halfX, 0f, halfZ));
                zoneLR.SetPosition(3, new Vector3(-halfX, 0f, halfZ));
                zoneLR.startWidth = 0.08f;
                zoneLR.endWidth = 0.08f;
                var zoneMat = new Material(cachedUnlitShader);
                zoneMat.color = new Color(1f, 0.6f, 0f, 0.8f);
                zoneMat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                zoneMat.renderQueue = 3000;
                zoneLR.material = zoneMat;
                zoneLR.startColor = new Color(1f, 0.6f, 0f, 0.8f);
                zoneLR.endColor = new Color(1f, 0.6f, 0f, 0.8f);
                view.SetInfluenceZone(zone);
            }
            return mill;
        }

        private GameObject CreateLumberYardPrefab(int playerId)
        {
            var lumberYard = new GameObject("LumberYard");
            lumberYard.layer = 11;

            // Open shed (wide, low structure)
            var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseObj.name = "Base";
            baseObj.transform.SetParent(lumberYard.transform);
            baseObj.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            baseObj.transform.localScale = new Vector3(1.6f, 0.7f, 1.6f);
            baseObj.layer = 11;

            // Flat sloped roof
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(lumberYard.transform);
            roof.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            roof.transform.localScale = new Vector3(1.8f, 0.15f, 1.8f);
            roof.layer = 11;

            // Log pile (horizontal cylinder)
            var logs = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            logs.name = "Logs";
            logs.transform.SetParent(lumberYard.transform);
            logs.transform.localPosition = new Vector3(0.4f, 0.15f, 0f);
            logs.transform.localScale = new Vector3(0.25f, 0.5f, 0.25f);
            logs.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            logs.layer = 11;

            Object.Destroy(baseObj.GetComponent<Collider>());
            Object.Destroy(roof.GetComponent<Collider>());
            Object.Destroy(logs.GetComponent<Collider>());

            var col = lumberYard.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.5f, 0f);
            col.size = new Vector3(1.8f, 1.0f, 1.8f);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ring.name = "SelectionRing";
            ring.transform.SetParent(lumberYard.transform);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(2.4f, 0.01f, 2.4f);
            ring.layer = 11;
            var ringCollider = ring.GetComponent<Collider>();
            if (ringCollider != null) Object.Destroy(ringCollider);
            ring.GetComponent<Renderer>().sharedMaterial = sharedSelectionRingMat;

            var view = lumberYard.AddComponent<BuildingView>();
            view.SetSelectionRing(ring);
            return lumberYard;
        }

        private GameObject CreateMinePrefab(int playerId)
        {
            var mine = new GameObject("Mine");
            mine.layer = 11;

            // Stone base (sturdy, squat cube)
            var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseObj.name = "Base";
            baseObj.transform.SetParent(mine.transform);
            baseObj.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            baseObj.transform.localScale = new Vector3(1.6f, 0.8f, 1.6f);
            baseObj.layer = 11;

            // Chimney / smelter tower
            var chimney = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            chimney.name = "Chimney";
            chimney.transform.SetParent(mine.transform);
            chimney.transform.localPosition = new Vector3(0.4f, 0.9f, 0.4f);
            chimney.transform.localScale = new Vector3(0.35f, 0.7f, 0.35f);
            chimney.layer = 11;

            // Roof
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(mine.transform);
            roof.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            roof.transform.localScale = new Vector3(1.4f, 0.15f, 1.4f);
            roof.layer = 11;

            Object.Destroy(baseObj.GetComponent<Collider>());
            Object.Destroy(chimney.GetComponent<Collider>());
            Object.Destroy(roof.GetComponent<Collider>());

            var col = mine.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.6f, 0f);
            col.size = new Vector3(1.8f, 1.2f, 1.8f);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ring.name = "SelectionRing";
            ring.transform.SetParent(mine.transform);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(2.4f, 0.01f, 2.4f);
            ring.layer = 11;
            var ringCollider = ring.GetComponent<Collider>();
            if (ringCollider != null) Object.Destroy(ringCollider);
            ring.GetComponent<Renderer>().sharedMaterial = sharedSelectionRingMat;

            var view = mine.AddComponent<BuildingView>();
            view.SetSelectionRing(ring);
            return mine;
        }

        private GameObject CreateArcheryRangePrefab(int playerId)
        {
            var archeryRange = new GameObject("ArcheryRange");
            archeryRange.layer = 11;

            // Body (slightly shorter than Barracks)
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(archeryRange.transform);
            body.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            body.transform.localScale = new Vector3(2.6f, 1.2f, 2.6f);
            body.layer = 11;

            // Roof (flat slab)
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(archeryRange.transform);
            roof.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            roof.transform.localScale = new Vector3(2.8f, 0.15f, 2.8f);
            roof.layer = 11;

            // Target stand (tall narrow element on one side)
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "TargetStand";
            target.transform.SetParent(archeryRange.transform);
            target.transform.localPosition = new Vector3(1.1f, 0.9f, 0f);
            target.transform.localScale = new Vector3(0.15f, 1.8f, 0.6f);
            target.layer = 11;

            // Target board (on top of stand)
            var board = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            board.name = "TargetBoard";
            board.transform.SetParent(archeryRange.transform);
            board.transform.localPosition = new Vector3(1.1f, 1.4f, 0f);
            board.transform.localScale = new Vector3(0.5f, 0.05f, 0.5f);
            board.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            board.layer = 11;

            Object.Destroy(body.GetComponent<Collider>());
            Object.Destroy(roof.GetComponent<Collider>());
            Object.Destroy(target.GetComponent<Collider>());
            Object.Destroy(board.GetComponent<Collider>());

            var col = archeryRange.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.8f, 0f);
            col.size = new Vector3(2.8f, 1.6f, 2.8f);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ring.name = "SelectionRing";
            ring.transform.SetParent(archeryRange.transform);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(3.6f, 0.01f, 3.6f);
            ring.layer = 11;
            var ringCollider = ring.GetComponent<Collider>();
            if (ringCollider != null) Object.Destroy(ringCollider);
            ring.GetComponent<Renderer>().sharedMaterial = sharedSelectionRingMat;

            var view = archeryRange.AddComponent<BuildingView>();
            view.SetSelectionRing(ring);
            return archeryRange;
        }

        private GameObject CreateStablesPrefab(int playerId)
        {
            var stables = new GameObject("Stables");
            stables.layer = 11;

            // Body (wider, lower — stable/barn feel)
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(stables.transform);
            body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            body.transform.localScale = new Vector3(2.8f, 1.0f, 2.2f);
            body.layer = 11;

            // Roof (flat slab, wider)
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(stables.transform);
            roof.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            roof.transform.localScale = new Vector3(3.0f, 0.15f, 2.4f);
            roof.layer = 11;

            // Fence post left
            var postL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            postL.name = "FencePostLeft";
            postL.transform.SetParent(stables.transform);
            postL.transform.localPosition = new Vector3(-1.2f, 0.4f, 1.2f);
            postL.transform.localScale = new Vector3(0.12f, 0.8f, 0.12f);
            postL.layer = 11;

            // Fence post right
            var postR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            postR.name = "FencePostRight";
            postR.transform.SetParent(stables.transform);
            postR.transform.localPosition = new Vector3(1.2f, 0.4f, 1.2f);
            postR.transform.localScale = new Vector3(0.12f, 0.8f, 0.12f);
            postR.layer = 11;

            // Fence rail
            var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = "FenceRail";
            rail.transform.SetParent(stables.transform);
            rail.transform.localPosition = new Vector3(0f, 0.55f, 1.2f);
            rail.transform.localScale = new Vector3(2.4f, 0.08f, 0.08f);
            rail.layer = 11;

            Object.Destroy(body.GetComponent<Collider>());
            Object.Destroy(roof.GetComponent<Collider>());
            Object.Destroy(postL.GetComponent<Collider>());
            Object.Destroy(postR.GetComponent<Collider>());
            Object.Destroy(rail.GetComponent<Collider>());

            var col = stables.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.7f, 0f);
            col.size = new Vector3(3.0f, 1.4f, 2.6f);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ring.name = "SelectionRing";
            ring.transform.SetParent(stables.transform);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(3.6f, 0.01f, 3.6f);
            ring.layer = 11;
            var ringCollider = ring.GetComponent<Collider>();
            if (ringCollider != null) Object.Destroy(ringCollider);
            ring.GetComponent<Renderer>().sharedMaterial = sharedSelectionRingMat;

            var view = stables.AddComponent<BuildingView>();
            view.SetSelectionRing(ring);
            return stables;
        }

        private GameObject CreateFarmPrefab(int playerId)
        {
            var farm = new GameObject("Farm");
            farm.layer = 11; // Building layer

            // Flat 2x2 ground quad (crop field)
            var field = GameObject.CreatePrimitive(PrimitiveType.Quad);
            field.name = "Field";
            field.transform.SetParent(farm.transform);
            field.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            field.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            field.transform.localScale = new Vector3(1.9f, 1.9f, 1f);
            field.layer = 11;

            // Crop rows (thin cubes to give texture)
            for (int i = 0; i < 4; i++)
            {
                var row = GameObject.CreatePrimitive(PrimitiveType.Cube);
                row.name = $"CropRow{i}";
                row.transform.SetParent(farm.transform);
                float zOff = -0.6f + i * 0.4f;
                row.transform.localPosition = new Vector3(0f, 0.08f, zOff);
                row.transform.localScale = new Vector3(1.6f, 0.06f, 0.15f);
                row.layer = 11;
                Object.Destroy(row.GetComponent<Collider>());
            }

            Object.Destroy(field.GetComponent<Collider>());

            // Box collider on root (thin, covers footprint)
            var col = farm.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.08f, 0f);
            col.size = new Vector3(1.9f, 0.16f, 1.9f);

            // Selection reticle
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ring.name = "SelectionRing";
            ring.transform.SetParent(farm.transform);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(2.4f, 0.01f, 2.4f);
            ring.layer = 11;

            var ringCollider = ring.GetComponent<Collider>();
            if (ringCollider != null) Object.Destroy(ringCollider);

            ring.GetComponent<Renderer>().sharedMaterial = sharedSelectionRingMat;

            var view = farm.AddComponent<BuildingView>();
            view.SetSelectionRing(ring);

            return farm;
        }

        private GameObject CreateTowerPrefab(int playerId)
        {
            var tower = new GameObject("Tower");
            tower.layer = 11; // Building layer

            // Main cylindrical tower body (tall cylinder)
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "TowerBody";
            body.transform.SetParent(tower.transform);
            body.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            body.transform.localScale = new Vector3(1.2f, 1.5f, 1.2f);
            body.layer = 11;

            // Wooden cap on top (flattened cylinder)
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = "TowerCap";
            cap.transform.SetParent(tower.transform);
            cap.transform.localPosition = new Vector3(0f, 3.2f, 0f);
            cap.transform.localScale = new Vector3(1.5f, 0.25f, 1.5f);
            cap.layer = 11;

            // Support beams around the body (4 vertical wooden beams)
            for (int i = 0; i < 4; i++)
            {
                var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                beam.name = $"SupportBeam_{i}";
                beam.transform.SetParent(tower.transform);

                float angle = i * 90f * Mathf.Deg2Rad;
                float beamRadius = 0.7f;
                Vector3 beamPos = new Vector3(
                    Mathf.Cos(angle) * beamRadius,
                    1.5f,
                    Mathf.Sin(angle) * beamRadius
                );
                beam.transform.localPosition = beamPos;
                beam.transform.localScale = new Vector3(0.12f, 2.8f, 0.12f);
                beam.layer = 11;
            }

            // Remove individual colliders
            Object.Destroy(body.GetComponent<Collider>());
            Object.Destroy(cap.GetComponent<Collider>());

            // Remove beam colliders
            for (int i = 0; i < 4; i++)
            {
                var beamCollider = tower.transform.Find($"SupportBeam_{i}")?.GetComponent<Collider>();
                if (beamCollider != null) Object.Destroy(beamCollider);
            }

            // Single cylinder collider on root (matches tower footprint)
            var col = tower.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1.6f, 0f);
            col.radius = 0.7f;
            col.height = 3.4f;

            // Selection reticle (circular for tower)
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "SelectionRing";
            ring.transform.SetParent(tower.transform);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(1.8f, 0.01f, 1.8f);
            ring.layer = 11;

            var ringCollider = ring.GetComponent<Collider>();
            if (ringCollider != null) Object.Destroy(ringCollider);

            // Create transparent green selection ring material
            ring.GetComponent<Renderer>().sharedMaterial = sharedSelectionRingMat;

            var view = tower.AddComponent<BuildingView>();
            view.SetSelectionRing(ring);

            return tower;
        }

        private GameObject CreateMonasteryPrefab(int playerId)
        {
            var monastery = new GameObject("Monastery");
            monastery.layer = 11;

            // Main hall (wide, tall body)
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(monastery.transform);
            body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            body.transform.localScale = new Vector3(2.4f, 1.6f, 2.8f);
            body.layer = 11;

            // Peaked roof
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(monastery.transform);
            roof.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            roof.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            roof.transform.localScale = new Vector3(1.8f, 1.8f, 3.0f);
            roof.layer = 11;

            // Bell tower (tall spire)
            var tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.name = "Tower";
            tower.transform.SetParent(monastery.transform);
            tower.transform.localPosition = new Vector3(0f, 2.8f, 0f);
            tower.transform.localScale = new Vector3(0.5f, 2.0f, 0.5f);
            tower.layer = 11;

            // Cross on top of bell tower
            var crossV = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crossV.name = "CrossVertical";
            crossV.transform.SetParent(monastery.transform);
            crossV.transform.localPosition = new Vector3(0f, 4.2f, 0f);
            crossV.transform.localScale = new Vector3(0.1f, 0.8f, 0.1f);
            crossV.layer = 11;

            var crossH = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crossH.name = "CrossHorizontal";
            crossH.transform.SetParent(monastery.transform);
            crossH.transform.localPosition = new Vector3(0f, 4.1f, 0f);
            crossH.transform.localScale = new Vector3(0.5f, 0.1f, 0.1f);
            crossH.layer = 11;

            // Remove individual colliders
            Object.Destroy(body.GetComponent<Collider>());
            Object.Destroy(roof.GetComponent<Collider>());
            Object.Destroy(tower.GetComponent<Collider>());
            Object.Destroy(crossV.GetComponent<Collider>());
            Object.Destroy(crossH.GetComponent<Collider>());

            // Single box collider on root
            var col = monastery.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.2f, 0f);
            col.size = new Vector3(2.6f, 2.4f, 3.0f);

            // Selection reticle
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ring.name = "SelectionRing";
            ring.transform.SetParent(monastery.transform);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(3.6f, 0.01f, 3.6f);
            ring.layer = 11;

            var ringCollider = ring.GetComponent<Collider>();
            if (ringCollider != null) Object.Destroy(ringCollider);

            ring.GetComponent<Renderer>().sharedMaterial = sharedSelectionRingMat;

            var view = monastery.AddComponent<BuildingView>();
            view.SetSelectionRing(ring);

            return monastery;
        }

        private GameObject CreateGenericBuildingPrefab(int playerId, int footprintW, int footprintH, string name)
        {
            var building = new GameObject(name);
            building.layer = 11;

            float sizeX = footprintW * 0.85f;
            float sizeZ = footprintH * 0.85f;
            float height = footprintW >= 4 ? 2.0f : 1.4f;

            // Body
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(building.transform);
            body.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            body.transform.localScale = new Vector3(sizeX, height, sizeZ);
            body.layer = 11;

            // Roof
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(building.transform);
            roof.transform.localPosition = new Vector3(0f, height + 0.1f, 0f);
            roof.transform.localScale = new Vector3(sizeX + 0.2f, 0.2f, sizeZ + 0.2f);
            roof.layer = 11;

            Object.Destroy(body.GetComponent<Collider>());
            Object.Destroy(roof.GetComponent<Collider>());

            var col = building.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, height * 0.5f + 0.1f, 0f);
            col.size = new Vector3(sizeX + 0.2f, height + 0.2f, sizeZ + 0.2f);

            float ringSize = Mathf.Max(footprintW, footprintH) + 0.6f;
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ring.name = "SelectionRing";
            ring.transform.SetParent(building.transform);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(ringSize, 0.01f, ringSize);
            ring.layer = 11;

            var ringCollider = ring.GetComponent<Collider>();
            if (ringCollider != null) Object.Destroy(ringCollider);

            ring.GetComponent<Renderer>().sharedMaterial = sharedSelectionRingMat;

            var view = building.AddComponent<BuildingView>();
            view.SetSelectionRing(ring);

            return building;
        }

        private GameObject CreateLandmarkPrefab(int playerId, LandmarkId landmarkId)
        {
            var def = LandmarkDefinitions.Get(landmarkId);
            var landmark = new GameObject($"Landmark_{def.Name}");
            landmark.layer = 11;

            // Main body (tall stone structure)
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(landmark.transform);
            body.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            body.transform.localScale = new Vector3(3.2f, 2.4f, 3.2f);
            body.layer = 11;

            // Roof/crown
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(landmark.transform);
            roof.transform.localPosition = new Vector3(0f, 2.8f, 0f);
            roof.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            roof.transform.localScale = new Vector3(2.5f, 0.8f, 2.5f);
            roof.layer = 11;

            // Central spire/tower
            var spire = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spire.name = "Spire";
            spire.transform.SetParent(landmark.transform);
            spire.transform.localPosition = new Vector3(0f, 4.0f, 0f);
            spire.transform.localScale = new Vector3(0.6f, 2.4f, 0.6f);
            spire.layer = 11;

            // Banner/flag element
            var banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            banner.name = "Roof"; // named "Roof" so it gets player color material
            banner.transform.SetParent(landmark.transform);
            banner.transform.localPosition = new Vector3(0.5f, 4.8f, 0f);
            banner.transform.localScale = new Vector3(0.8f, 0.5f, 0.1f);
            banner.layer = 11;

            // Remove individual colliders
            Object.Destroy(body.GetComponent<Collider>());
            Object.Destroy(roof.GetComponent<Collider>());
            Object.Destroy(spire.GetComponent<Collider>());
            Object.Destroy(banner.GetComponent<Collider>());

            // Single box collider on root
            var col = landmark.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.8f, 0f);
            col.size = new Vector3(3.4f, 3.6f, 3.4f);

            // Selection reticle
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ring.name = "SelectionRing";
            ring.transform.SetParent(landmark.transform);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(4.5f, 0.01f, 4.5f);
            ring.layer = 11;

            var ringCollider = ring.GetComponent<Collider>();
            if (ringCollider != null) Object.Destroy(ringCollider);

            ring.GetComponent<Renderer>().sharedMaterial = sharedSelectionRingMat;

            var view = landmark.AddComponent<BuildingView>();
            view.SetSelectionRing(ring);

            // Influence zone for French landmarks (training cost discount)
            if (LandmarkDefinitions.Get(landmarkId).Civ == Civilization.French)
            {
                var simRef = GameBootstrapper.Instance?.Simulation;
                int influenceRadius = simRef?.Config?.LandmarkInfluenceRadius ?? 5;
                int footprintW = def.FootprintWidth;
                int footprintH = def.FootprintHeight;
                float halfX = (footprintW + 2 * influenceRadius) * 0.5f;
                float halfZ = (footprintH + 2 * influenceRadius) * 0.5f;

                var zone = new GameObject("InfluenceZone");
                zone.transform.SetParent(landmark.transform);
                zone.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                zone.layer = 11;
                var zoneLR = zone.AddComponent<LineRenderer>();
                zoneLR.useWorldSpace = false;
                zoneLR.loop = true;
                zoneLR.positionCount = 4;
                zoneLR.SetPosition(0, new Vector3(-halfX, 0f, -halfZ));
                zoneLR.SetPosition(1, new Vector3(halfX, 0f, -halfZ));
                zoneLR.SetPosition(2, new Vector3(halfX, 0f, halfZ));
                zoneLR.SetPosition(3, new Vector3(-halfX, 0f, halfZ));
                zoneLR.startWidth = 0.08f;
                zoneLR.endWidth = 0.08f;
                var zoneMat = new Material(cachedUnlitShader);
                zoneMat.color = new Color(1f, 0.6f, 0f, 0.8f);
                zoneMat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                zoneMat.renderQueue = 3000;
                zoneLR.material = zoneMat;
                zoneLR.startColor = new Color(1f, 0.6f, 0f, 0.8f);
                zoneLR.endColor = new Color(1f, 0.6f, 0f, 0.8f);
                view.SetInfluenceZone(zone);
            }

            return landmark;
        }

        private GameObject CreateWallMerlon(Transform parent, Vector3 localPos, Vector3 localScale)
        {
            var merlon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            merlon.name = "Merlon";
            merlon.transform.SetParent(parent);
            merlon.transform.localPosition = localPos;
            merlon.transform.localScale = localScale;
            merlon.layer = 11;
            return merlon;
        }

        private void HandleUnitTrained(int unitId, int unitType, int playerId)
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            var unitData = sim.UnitRegistry.GetUnit(unitId);
            if (unitData == null) return;

            Vector3 spawnPos = unitData.SimPosition.ToVector3();
            if (unitType == 5)
            {
                // Sheep trained — create procedural view
                SpawnSheepView(unitData, spawnPos);
                return;
            }
            if (unitType == 9)
            {
                // Monk — create procedural view if no prefab assigned
                if (monkPrefab == null)
                {
                    SpawnProceduralMonk(unitData, spawnPos);
                    return;
                }
            }
            if (unitType >= 13 && unitType <= 15)
            {
                SpawnProceduralSiege(unitData, spawnPos, unitType);
                return;
            }
            GameObject prefab;
            switch (unitType)
            {
                case 9: prefab = monkPrefab; break;
                case 8: prefab = crossbowmanPrefab; break;
                case 7: prefab = knightPrefab; break;
                case 6: prefab = manAtArmsPrefab; break;
                case 12: prefab = landsknechtPrefab; break;
                case 11: prefab = gendarmePrefab; break;
                case 10: prefab = longbowmanPrefab; break;
                case 4: prefab = scoutPrefab; break;
                case 3: prefab = horsemanPrefab; break;
                case 2: prefab = archerPrefab; break;
                case 1: prefab = spearmanPrefab; break;
                default: prefab = villagerPrefab; break;
            }
            SpawnUnit(prefab, unitData, spawnPos, unitType);
            SFXManager.Instance?.Play(SFXType.UnitTrained, spawnPos, 0.6f);
        }

        private void SpawnBuilding(BuildingData buildingData)
        {
            GameObject prefab;
            switch (buildingData.Type)
            {
                case BuildingType.House:
                {
                    // Random house variant
                    int variant = UnityEngine.Random.Range(1, 4); // 1, 2, or 3
                    prefab = CreateBuildingSpritePrefab("House", $"House{variant}", 2, 2, 4f);
                    if (prefab == null) prefab = CreateHousePrefab(buildingData.PlayerId);
                    break;
                }
                case BuildingType.Barracks:
                    prefab = CreateBuildingSpritePrefab("Barracks", "Barracks", 3, 3, 5f)
                          ?? CreateBarracksPrefab(buildingData.PlayerId);
                    break;
                case BuildingType.TownCenter:
                    prefab = CreateTownCenterPrefab(buildingData.PlayerId);
                    break;
                case BuildingType.Wall:
                    prefab = CreateWallPrefab(buildingData.PlayerId);
                    break;
                case BuildingType.Mill:
                    prefab = CreateMillPrefab(buildingData.PlayerId);
                    break;
                case BuildingType.LumberYard:
                    prefab = CreateLumberYardPrefab(buildingData.PlayerId);
                    break;
                case BuildingType.Mine:
                    prefab = CreateMinePrefab(buildingData.PlayerId);
                    break;
                case BuildingType.ArcheryRange:
                    prefab = CreateBuildingSpritePrefab("ArcheryRange", "ArcheryRange", 3, 3, 5f)
                          ?? CreateArcheryRangePrefab(buildingData.PlayerId);
                    break;
                case BuildingType.Stables:
                    prefab = CreateBuildingSpritePrefab("Stables", "Stables", 3, 3, 5f)
                          ?? CreateStablesPrefab(buildingData.PlayerId);
                    break;
                case BuildingType.Farm:
                    prefab = CreateFarmPrefab(buildingData.PlayerId);
                    break;
                case BuildingType.Tower:
                    prefab = CreateBuildingSpritePrefab("Tower", "Tower", 1, 1, 5f)
                          ?? CreateTowerPrefab(buildingData.PlayerId);
                    break;
                case BuildingType.Monastery:
                    prefab = CreateMonasteryPrefab(buildingData.PlayerId);
                    break;
                case BuildingType.Blacksmith:
                    prefab = CreateBuildingSpritePrefab("Blacksmith", "Blacksmith", 3, 3, 5f)
                          ?? CreateGenericBuildingPrefab(buildingData.PlayerId, 3, 3, "Blacksmith");
                    break;
                case BuildingType.Market:
                    prefab = CreateGenericBuildingPrefab(buildingData.PlayerId, 3, 3, "Market");
                    break;
                case BuildingType.University:
                    prefab = CreateGenericBuildingPrefab(buildingData.PlayerId, 3, 3, "University");
                    break;
                case BuildingType.SiegeWorkshop:
                    prefab = CreateGenericBuildingPrefab(buildingData.PlayerId, 3, 3, "SiegeWorkshop");
                    break;
                case BuildingType.Keep:
                    prefab = CreateBuildingSpritePrefab("Keep", "Keep", 3, 3, 8f)
                          ?? CreateGenericBuildingPrefab(buildingData.PlayerId, 3, 3, "Keep");
                    break;
                case BuildingType.StoneWall:
                case BuildingType.StoneGate:
                case BuildingType.WoodGate:
                    prefab = CreateWallPrefab(buildingData.PlayerId);
                    break;
                case BuildingType.Wonder:
                    prefab = CreateGenericBuildingPrefab(buildingData.PlayerId, 5, 5, "Wonder");
                    break;
                case BuildingType.Landmark:
                    prefab = CreateLandmarkPrefab(buildingData.PlayerId, buildingData.LandmarkId);
                    break;
                default:
                    prefab = CreateHousePrefab(buildingData.PlayerId);
                    break;
            }
            Vector3 worldPos = buildingData.SimPosition.ToVector3();

            var sim = GameBootstrapper.Instance?.Simulation;
            MapData mapData = sim?.MapData;
            float hs = sim?.Config.TerrainHeightScale ?? 0f;

            if (mapData != null)
                worldPos.y = mapData.SampleHeight(worldPos.x, worldPos.z) * hs;

            prefab.transform.position = worldPos;

            // Apply player color to roofs, neutral stone to body parts (skip billboard sprites)
            var roofMat = playerMaterials[buildingData.PlayerId];
            foreach (var r in prefab.GetComponentsInChildren<Renderer>())
            {
                if (r.gameObject.name == "SelectionRing") continue;
                if (r.gameObject.name == "RangeRing") continue;
                if (r.gameObject.name == "Sprite") continue; // Billboard sprite — keep its material
                var mat = r.gameObject.name == "Roof" ? roofMat : buildingBodyMaterial;
                r.sharedMaterial = mat;
            }

            var view = prefab.GetComponent<BuildingView>();
            if (view != null)
            {
                view.Initialize(buildingData.Id, worldPos, buildingData, mapData, hs);

                if (buildingData.IsGate)
                    view.SetGateVisual(true, buildingBodyMaterial);

                buildingViews[buildingData.Id] = view;

                if (selectionManager != null)
                    selectionManager.RegisterBuildingView(view);
            }
        }

        private void HandleBuildingCreated(BuildingData buildingData)
        {
            SFXManager.Instance?.Play(SFXType.BuildingPlace, buildingData.SimPosition.ToVector3(), 0.8f);
            SpawnBuilding(buildingData);
        }

        private void HandleBuildingDestroyed(int buildingId)
        {
            if (!buildingViews.TryGetValue(buildingId, out var view)) return;

            SFXManager.Instance?.Play(SFXType.UnitDeath, view.transform.position, 0.9f);

            int localPid = selectionManager != null ? selectionManager.LocalPlayerId : 0;

            // Check if this is a known enemy building destroyed in fog
            var sim = GameBootstrapper.Instance?.Simulation;
            bool isEnemy = sim != null ? !sim.AreAllies(view.PlayerId, localPid) : view.PlayerId != localPid;
            if (isEnemy && knownEnemyBuildings.Contains(buildingId))
            {
                var fogData = GameBootstrapper.Instance?.Simulation?.FogOfWar;
                var tileVis = fogData?.GetVisibility(localPid, view.OriginTileX, view.OriginTileZ)
                              ?? TileVisibility.Visible;

                if (tileVis != TileVisibility.Visible)
                {
                    // Destroyed in fog — convert to ghost, keep view alive
                    ghostBuildings[buildingId] = (view.OriginTileX, view.OriginTileZ);
                    view.ClearBuildingData();
                    if (selectionManager != null)
                        selectionManager.OnBuildingDestroyed(buildingId);
                    selectionManager?.UnregisterBuildingView(buildingId);
                    return; // Don't remove from buildingViews — LateUpdate manages ghost visibility
                }
            }

            // Normal destruction: player is watching, or own building, or never-seen enemy
            view.OnDestroyed();
            if (selectionManager != null)
                selectionManager.OnBuildingDestroyed(buildingId);
            selectionManager?.UnregisterBuildingView(buildingId);
            buildingViews.Remove(buildingId);
            knownEnemyBuildings.Remove(buildingId);
        }

        private void HandleUnitDied(int unitId)
        {
            if (unitViews.TryGetValue(unitId, out var view))
            {
                view.OnDeath();
                SFXManager.Instance?.Play(SFXType.UnitDeath, view.transform.position, 0.7f);
                if (selectionManager != null)
                    selectionManager.OnUnitDied(unitId);
                selectionManager?.UnregisterUnitView(unitId);
                unitViews.Remove(unitId);
            }
        }

        private void HandleUnitGarrisoned(int unitId, int buildingId)
        {
            if (unitViews.TryGetValue(unitId, out var view))
            {
                view.HideHealthBar();
                view.gameObject.SetActive(false);
                if (selectionManager != null)
                    selectionManager.OnUnitDied(unitId); // deselect garrisoned unit
            }
        }

        private void HandleUnitUngarrisoned(int unitId)
        {
            if (unitViews.TryGetValue(unitId, out var view))
            {
                view.gameObject.SetActive(true);
                // Sync position from sim data
                var sim = GameBootstrapper.Instance?.Simulation;
                var unitData = sim?.UnitRegistry.GetUnit(unitId);
                if (unitData != null)
                {
                    view.transform.position = unitData.SimPosition.ToVector3();
                }
            }
        }

        private Material neutralSheepMaterial;

        private Material GetOrCreateNeutralSheepMaterial()
        {
            if (neutralSheepMaterial == null)
            {
                var baseMat = villagerPrefab.GetComponentInChildren<Renderer>().sharedMaterial;
                neutralSheepMaterial = new Material(baseMat);
                SetMaterialColor(neutralSheepMaterial, Color.white);
            }
            return neutralSheepMaterial;
        }

        private GameObject CreateProceduralSheep()
        {
            var root = new GameObject("Sheep");
            root.layer = LayerMask.NameToLayer("Unit");

            // Body
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            body.transform.SetParent(root.transform);
            body.transform.localScale = new Vector3(0.5f, 0.35f, 0.6f);
            body.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            body.layer = root.layer;
            var bodyCol = body.GetComponent<Collider>();
            if (bodyCol != null) Destroy(bodyCol);

            // Head
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Sphere_Head";
            head.transform.SetParent(root.transform);
            head.transform.localScale = new Vector3(0.22f, 0.22f, 0.25f);
            head.transform.localPosition = new Vector3(0f, 0.32f, 0.35f);
            head.layer = root.layer;
            var headCol = head.GetComponent<Collider>();
            if (headCol != null) Destroy(headCol);

            // Legs
            var legShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var legMat = new Material(legShader);
            legMat.color = new Color(0.3f, 0.2f, 0.1f);

            float legY = 0.08f;
            float legH = 0.12f;
            Vector3[] legPositions = {
                new Vector3(-0.12f, legY, 0.15f),
                new Vector3(0.12f, legY, 0.15f),
                new Vector3(-0.12f, legY, -0.15f),
                new Vector3(0.12f, legY, -0.15f)
            };
            for (int i = 0; i < 4; i++)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                leg.name = $"Leg{i}";
                leg.transform.SetParent(root.transform);
                leg.transform.localScale = new Vector3(0.06f, legH, 0.06f);
                leg.transform.localPosition = legPositions[i];
                leg.layer = root.layer;
                leg.GetComponent<Renderer>().sharedMaterial = legMat;
                var legCol = leg.GetComponent<Collider>();
                if (legCol != null) Destroy(legCol);
            }

            // Capsule collider for selection
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.25f, 0f);
            capsule.radius = 0.3f;
            capsule.height = 0.5f;

            // Selection ring
            var ringGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringGO.name = "SelectionRing";
            ringGO.transform.SetParent(root.transform);
            ringGO.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ringGO.transform.localScale = new Vector3(0.8f, 0.01f, 0.8f);
            ringGO.layer = root.layer;
            var ringCol = ringGO.GetComponent<Collider>();
            if (ringCol != null) Destroy(ringCol);
            ringGO.GetComponent<Renderer>().sharedMaterial = sharedSelectionRingMat;
            ringGO.SetActive(false);

            // UnitView component
            root.AddComponent<UnitView>();

            return root;
        }

        private void SpawnSheepView(UnitData unitData, Vector3 spawnPos)
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim != null)
                spawnPos.y = sim.MapData.SampleHeight(spawnPos.x, spawnPos.z) * sim.Config.TerrainHeightScale;

            var go = CreateProceduralSheep();
            go.transform.position = spawnPos;
            go.SetActive(false);

            // Apply neutral white or player material
            Material mat;
            Material silMat;
            if (unitData.PlayerId >= 0 && unitData.PlayerId < playerMaterials.Length)
            {
                mat = playerMaterials[unitData.PlayerId];
                silMat = playerSilhouetteMaterials[unitData.PlayerId];
            }
            else
            {
                mat = GetOrCreateNeutralSheepMaterial();
                silMat = null;
            }

            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var partName = r.gameObject.name;
                if (partName == "SelectionRing") continue;

                bool isBodyPart = partName.StartsWith("Body") || partName.StartsWith("Sphere");
                if (isBodyPart)
                {
                    if (silMat != null)
                        r.sharedMaterials = new Material[] { mat, unitStencilMat, silMat };
                    else
                        r.sharedMaterial = mat;
                }
            }

            var unitView = go.GetComponent<UnitView>();
            if (unitView != null)
            {
                var ring = go.transform.Find("SelectionRing")?.gameObject;
                if (ring != null)
                    unitView.SetSelectionRing(ring);

                var mapData = sim?.MapData;
                float hs = sim?.Config.TerrainHeightScale ?? 0f;
                var sheepSilMat = unitData.PlayerId >= 0 && unitData.PlayerId < playerSilhouetteMaterials.Length
                    ? playerSilhouetteMaterials[unitData.PlayerId] : null;
                unitView.Initialize(unitData.Id, spawnPos, unitData, 5, mapData, hs,
                    unitStencilMat, sheepSilMat);
                unitViews[unitData.Id] = unitView;

                if (selectionManager != null)
                    selectionManager.RegisterUnitView(unitView);
            }
        }

        private void SpawnProceduralMonk(UnitData unitData, Vector3 spawnPos)
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim != null)
                spawnPos.y = sim.MapData.SampleHeight(spawnPos.x, spawnPos.z) * sim.Config.TerrainHeightScale;

            var go = new GameObject("Monk");
            go.layer = 8; // Unit layer (matches other unit prefabs)

            // Body (robed figure)
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(go.transform);
            body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            body.transform.localScale = new Vector3(0.35f, 0.5f, 0.35f);
            body.layer = 8;

            // Hood (sphere on top)
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Sphere";
            head.transform.SetParent(go.transform);
            head.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            head.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            head.layer = 8;

            // Left arm
            var leftArm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            leftArm.name = "LeftArm";
            leftArm.transform.SetParent(go.transform);
            leftArm.transform.localPosition = new Vector3(-0.22f, 0.7f, 0f);
            leftArm.transform.localRotation = Quaternion.Euler(0f, 0f, 15f);
            leftArm.transform.localScale = new Vector3(0.1f, 0.25f, 0.1f);
            leftArm.layer = 8;

            // Right arm
            var rightArm = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            rightArm.name = "RightArm";
            rightArm.transform.SetParent(go.transform);
            rightArm.transform.localPosition = new Vector3(0.22f, 0.7f, 0f);
            rightArm.transform.localRotation = Quaternion.Euler(0f, 0f, -15f);
            rightArm.transform.localScale = new Vector3(0.1f, 0.25f, 0.1f);
            rightArm.layer = 8;

            // Plague mask
            if (plagueMaskPrefab != null)
            {
                var mask = Object.Instantiate(plagueMaskPrefab, go.transform);
                mask.name = "PlagueMask";
                mask.transform.localPosition = new Vector3(0f, -1.15f, 0.05f);
                mask.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
                mask.layer = 8;
                foreach (var c in mask.GetComponentsInChildren<Collider>())
                    Object.Destroy(c);
                foreach (var t in mask.GetComponentsInChildren<Transform>())
                    t.gameObject.layer = 8;
            }

            // Remove individual colliders, add one on root
            Object.Destroy(body.GetComponent<Collider>());
            Object.Destroy(head.GetComponent<Collider>());
            Object.Destroy(leftArm.GetComponent<Collider>());
            Object.Destroy(rightArm.GetComponent<Collider>());
            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.65f, 0f);
            col.radius = 0.4f;
            col.height = 1.5f;

            // Selection ring
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "SelectionRing";
            ring.transform.SetParent(go.transform);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(0.8f, 0.01f, 0.8f);
            ring.layer = 8;
            var ringCol = ring.GetComponent<Collider>();
            if (ringCol != null) Object.Destroy(ringCol);
            ring.GetComponent<Renderer>().sharedMaterial = sharedSelectionRingMat;

            go.transform.position = spawnPos;
            go.SetActive(true);

            // Apply player color
            var mat = playerMaterials[unitData.PlayerId];
            var silMat = playerSilhouetteMaterials[unitData.PlayerId];
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var partName = r.gameObject.name;
                if (partName == "SelectionRing") continue;
                bool isTeamColored = partName.StartsWith("Body") || partName.StartsWith("Sphere");
                var primaryMat = isTeamColored ? mat : r.sharedMaterial;
                r.sharedMaterials = new Material[] { primaryMat, unitStencilMat, silMat };
            }

            var unitView = go.AddComponent<UnitView>();
            unitView.SetSelectionRing(ring);

            var mapData = sim?.MapData;
            float hs = sim?.Config.TerrainHeightScale ?? 0f;
            unitView.Initialize(unitData.Id, spawnPos, unitData, 9, mapData, hs,
                unitStencilMat, playerSilhouetteMaterials[unitData.PlayerId]);
            unitViews[unitData.Id] = unitView;

            if (selectionManager != null)
                selectionManager.RegisterUnitView(unitView);

            SFXManager.Instance?.Play(SFXType.UnitTrained, spawnPos, 0.6f);
        }

        private void SpawnProceduralSiege(UnitData unitData, Vector3 spawnPos, int unitType)
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim != null)
                spawnPos.y = sim.MapData.SampleHeight(spawnPos.x, spawnPos.z) * sim.Config.TerrainHeightScale;

            string[] names = { "", "", "", "", "", "", "", "", "", "", "", "", "", "BatteringRam", "Mangonel", "Trebuchet" };
            var go = new GameObject(unitType < names.Length ? names[unitType] : "SiegeUnit");
            go.layer = 8;

            if (unitType == 13) // Battering Ram — long horizontal box with wheels
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                body.transform.SetParent(go.transform);
                body.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                body.transform.localScale = new Vector3(0.5f, 0.4f, 1.2f);
                body.layer = 8;
                Object.Destroy(body.GetComponent<Collider>());

                var ram = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ram.name = "Ram";
                ram.transform.SetParent(go.transform);
                ram.transform.localPosition = new Vector3(0f, 0.3f, 0.6f);
                ram.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ram.transform.localScale = new Vector3(0.15f, 0.4f, 0.15f);
                ram.layer = 8;
                Object.Destroy(ram.GetComponent<Collider>());
            }
            else if (unitType == 14) // Mangonel — box with arm
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                body.transform.SetParent(go.transform);
                body.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                body.transform.localScale = new Vector3(0.6f, 0.3f, 0.8f);
                body.layer = 8;
                Object.Destroy(body.GetComponent<Collider>());

                var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
                arm.name = "Arm";
                arm.transform.SetParent(go.transform);
                arm.transform.localPosition = new Vector3(0f, 0.6f, 0.1f);
                arm.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
                arm.transform.localScale = new Vector3(0.08f, 0.08f, 0.7f);
                arm.layer = 8;
                Object.Destroy(arm.GetComponent<Collider>());
            }
            else // Trebuchet — tall frame with counterweight arm
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                body.transform.SetParent(go.transform);
                body.transform.localPosition = new Vector3(0f, 0.2f, 0f);
                body.transform.localScale = new Vector3(0.7f, 0.25f, 0.9f);
                body.layer = 8;
                Object.Destroy(body.GetComponent<Collider>());

                var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frame.name = "Frame";
                frame.transform.SetParent(go.transform);
                frame.transform.localPosition = new Vector3(0f, 0.7f, 0f);
                frame.transform.localScale = new Vector3(0.08f, 0.8f, 0.08f);
                frame.layer = 8;
                Object.Destroy(frame.GetComponent<Collider>());

                var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
                arm.name = "Arm";
                arm.transform.SetParent(go.transform);
                arm.transform.localPosition = new Vector3(0f, 1.0f, 0.2f);
                arm.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
                arm.transform.localScale = new Vector3(0.06f, 0.06f, 1.0f);
                arm.layer = 8;
                Object.Destroy(arm.GetComponent<Collider>());
            }

            // Root collider
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.35f, 0f);
            col.size = new Vector3(0.7f, 0.7f, 1.0f);

            // Selection ring
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "SelectionRing";
            ring.transform.SetParent(go.transform);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(1.0f, 0.01f, 1.0f);
            ring.layer = 8;
            var ringCol = ring.GetComponent<Collider>();
            if (ringCol != null) Object.Destroy(ringCol);
            ring.GetComponent<Renderer>().sharedMaterial = sharedSelectionRingMat;

            go.transform.position = spawnPos;
            go.SetActive(true);

            // Apply player color
            var mat = playerMaterials[unitData.PlayerId];
            var silMat = playerSilhouetteMaterials[unitData.PlayerId];
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var partName = r.gameObject.name;
                if (partName == "SelectionRing") continue;
                r.sharedMaterials = new Material[] { mat, unitStencilMat, silMat };
            }

            var unitView = go.AddComponent<UnitView>();
            unitView.SetSelectionRing(ring);

            var mapData = sim?.MapData;
            float hs = sim?.Config.TerrainHeightScale ?? 0f;
            unitView.Initialize(unitData.Id, spawnPos, unitData, unitType, mapData, hs,
                unitStencilMat, playerSilhouetteMaterials[unitData.PlayerId]);
            unitViews[unitData.Id] = unitView;

            if (selectionManager != null)
                selectionManager.RegisterUnitView(unitView);

            SFXManager.Instance?.Play(SFXType.UnitTrained, spawnPos, 0.6f);
        }

        private void SpawnSheepViews(GameSimulation sim)
        {
            var allUnits = sim.UnitRegistry.GetAllUnits();
            for (int i = 0; i < allUnits.Count; i++)
            {
                var unit = allUnits[i];
                if (!unit.IsSheep) continue;
                Vector3 pos = unit.SimPosition.ToVector3();
                SpawnSheepView(unit, pos);
            }
        }

        private void HandleSheepConverted(int sheepId, int newPlayerId)
        {
            if (!unitViews.TryGetValue(sheepId, out var view)) return;
            if (newPlayerId < 0 || newPlayerId >= playerMaterials.Length) return;

            var mat = playerMaterials[newPlayerId];
            var silMat = playerSilhouetteMaterials[newPlayerId];

            foreach (var r in view.GetComponentsInChildren<Renderer>())
            {
                var partName = r.gameObject.name;
                if (partName == "SelectionRing") continue;

                bool isBodyPart = partName.StartsWith("Body") || partName.StartsWith("Sphere");
                if (isBodyPart)
                    r.sharedMaterials = new Material[] { mat, unitStencilMat, silMat };
            }

            SFXManager.Instance?.Play(SFXType.SheepConvert, view.transform.position);
        }

        private void HandleSheepSlaughtered(int sheepId, int carcassNodeId)
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            var node = sim.MapData.GetResourceNode(carcassNodeId);
            if (node == null) return;

            // Create carcass visual: brown flat sphere
            var carcassGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            carcassGO.name = "Carcass";
            carcassGO.transform.localScale = new Vector3(1.4f, 0.3f, 1.4f);

            Vector3 pos = node.Position.ToVector3();
            pos.y = sim.MapData.SampleHeight(pos.x, pos.z) * sim.Config.TerrainHeightScale + 0.05f;
            carcassGO.transform.position = pos;

            // Brown material
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            mat.color = new Color(0.55f, 0.35f, 0.15f);
            carcassGO.GetComponent<Renderer>().sharedMaterial = mat;

            // Replace collider with box on resource layer
            var existingCol = carcassGO.GetComponent<Collider>();
            if (existingCol != null) Destroy(existingCol);
            var boxCol = carcassGO.AddComponent<BoxCollider>();
            int resourceLayer = LayerMask.NameToLayer("Resource");
            if (resourceLayer >= 0) carcassGO.layer = resourceLayer;

            // ResourceNode component
            var nodeView = carcassGO.AddComponent<ResourceNode>();
            nodeView.Initialize(node.Id, node);

            // Register with MapRenderer
            if (mapRenderer != null)
                mapRenderer.RegisterResourceNodeView(node.Id, nodeView);
        }

        private void HandleProjectileCreated(ProjectileData proj)
        {
            var sim = GameBootstrapper.Instance?.Simulation;

            SFXManager.Instance?.Play(SFXType.ArrowFire, proj.Position.ToVector3(), 0.6f);

            // Create a thin white prism for the projectile
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = proj.IsBolt ? "Bolt" : "Projectile";
            // Bolts are shorter and thicker than arrows
            go.transform.localScale = proj.IsBolt
                ? new Vector3(0.05f, 0.05f, 0.3f)
                : new Vector3(0.04f, 0.04f, 0.5f);

            // Remove collider
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // White material
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader != null)
                {
                    var mat = new Material(shader);
                    mat.color = Color.white;
                    renderer.sharedMaterial = mat;
                }
            }

            var mapData = sim?.MapData;
            float hs = sim?.Config.TerrainHeightScale ?? 0f;

            // Look up target position for arc calculation
            FixedVector3 targetPos = proj.Position; // fallback
            if (proj.TargetUnitId >= 0)
            {
                var target = sim.UnitRegistry.GetUnit(proj.TargetUnitId);
                if (target != null) targetPos = target.SimPosition;
            }
            else if (proj.TargetBuildingId >= 0)
            {
                var target = sim.BuildingRegistry.GetBuilding(proj.TargetBuildingId);
                if (target != null) targetPos = target.SimPosition;
            }

            var view = go.AddComponent<ProjectileView>();
            view.Initialize(proj.Position, targetPos, mapData, hs, proj.IsBolt);
            projectileViews[proj.Id] = view;
        }

        private void HandleProjectileHit(int projectileId)
        {
            if (projectileViews.TryGetValue(projectileId, out var view))
            {
                SFXManager.Instance?.Play(SFXType.ArrowImpact, view.transform.position, 0.5f);
                view.OnHit();
                projectileViews.Remove(projectileId);
            }
        }

        private void OnDestroy()
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim != null)
            {
                sim.OnUnitDied -= HandleUnitDied;
                sim.OnBuildingDestroyed -= HandleBuildingDestroyed;
                sim.OnUnitTrained -= HandleUnitTrained;
                sim.OnProjectileCreated -= HandleProjectileCreated;
                sim.OnProjectileHit -= HandleProjectileHit;
                sim.OnBuildingCreated -= HandleBuildingCreated;
                sim.OnUnitGarrisoned -= HandleUnitGarrisoned;
                sim.OnUnitUngarrisoned -= HandleUnitUngarrisoned;
                sim.OnSheepConverted -= HandleSheepConverted;
                sim.OnSheepSlaughtered -= HandleSheepSlaughtered;
                if (meteorVisualManager != null)
                {
                    sim.OnMeteorWarning -= meteorVisualManager.HandleMeteorWarning;
                    sim.OnMeteorImpact -= meteorVisualManager.HandleMeteorImpact;
                    sim.OnHealingRainWarning -= meteorVisualManager.HandleHealingRainWarning;
                    sim.OnHealingRainEnd -= meteorVisualManager.HandleHealingRainEnd;
                    sim.OnLightningStormWarning -= meteorVisualManager.HandleLightningStormWarning;
                    sim.OnLightningBolt -= meteorVisualManager.HandleLightningBolt;
                    sim.OnLightningStormEnd -= meteorVisualManager.HandleLightningStormEnd;
                    sim.OnTsunamiWarning -= meteorVisualManager.HandleTsunamiWarning;
                    sim.OnTsunamiImpact -= meteorVisualManager.HandleTsunamiImpact;
                }

            }
        }

        private void LateUpdate()
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            // Sync resource nodes
            if (mapRenderer != null)
                mapRenderer.SyncFromSim(sim.MapData);

            // Fog of war visibility
            int localPid = selectionManager != null ? selectionManager.LocalPlayerId : 0;
            var fogData = sim.FogOfWar;

            // Toggle enemy unit visibility
            foreach (var kvp in unitViews)
            {
                var view = kvp.Value;
                var unitData = sim.UnitRegistry.GetUnit(kvp.Key);
                if (unitData == null) continue;

                if (sim.AreAllies(unitData.PlayerId, localPid) && !unitData.IsSheep)
                {
                    view.gameObject.SetActive(true);
                }
                else
                {
                    if (view.IsDead) continue; // Don't deactivate while corpse fade coroutine is running
                    var tile = sim.MapData.WorldToTile(unitData.SimPosition);
                    bool visible = fogData.GetVisibility(localPid, tile.x, tile.y) == TileVisibility.Visible;
                    view.gameObject.SetActive(visible);
                }
            }

            // Toggle enemy building visibility (fog of war ghost support)
            ghostCleanupList.Clear();
            foreach (var kvp in buildingViews)
            {
                var view = kvp.Value;
                var buildingData = sim.BuildingRegistry.GetBuilding(kvp.Key);

                // Own/allied buildings: always fully visible
                if (buildingData != null && sim.AreAllies(buildingData.PlayerId, localPid))
                {
                    view.gameObject.SetActive(true);
                    view.SetGhostMode(false);
                    continue;
                }

                // Determine tile visibility
                TileVisibility tileVis;
                if (buildingData != null)
                {
                    tileVis = fogData.GetVisibility(localPid, buildingData.OriginTileX, buildingData.OriginTileZ);
                }
                else if (ghostBuildings.TryGetValue(kvp.Key, out var ghostTile))
                {
                    tileVis = fogData.GetVisibility(localPid, ghostTile.tileX, ghostTile.tileZ);
                }
                else
                {
                    view.gameObject.SetActive(false);
                    continue;
                }

                if (tileVis == TileVisibility.Visible)
                {
                    if (buildingData != null)
                    {
                        // Player has line of sight — show fully, mark as known
                        knownEnemyBuildings.Add(kvp.Key);
                        view.gameObject.SetActive(true);
                        view.SetGhostMode(false);
                    }
                    else
                    {
                        // Re-scouted area, building is gone — remove ghost
                        view.gameObject.SetActive(false);
                        ghostCleanupList.Add(kvp.Key);
                    }
                }
                else if (tileVis == TileVisibility.Explored && knownEnemyBuildings.Contains(kvp.Key))
                {
                    // Previously seen — show as ghost
                    view.gameObject.SetActive(true);
                    view.SetGhostMode(true);
                }
                else
                {
                    // Never seen or unexplored — hide
                    view.gameObject.SetActive(false);
                }
            }

            // Clean up ghost views that were re-scouted
            for (int i = 0; i < ghostCleanupList.Count; i++)
            {
                int id = ghostCleanupList[i];
                if (buildingViews.TryGetValue(id, out var ghostView))
                {
                    Destroy(ghostView.gameObject);
                    buildingViews.Remove(id);
                }
                ghostBuildings.Remove(id);
                knownEnemyBuildings.Remove(id);
            }

            // Resource node fog of war visibility
            if (mapRenderer != null)
            {
                foreach (var kvp in mapRenderer.ResourceNodeViews)
                {
                    var nodeView = kvp.Value;
                    var nodeData = sim.MapData.GetResourceNode(kvp.Key);

                    // Get tile coords from the node data
                    int nodeTileX, nodeTileZ;
                    if (nodeData != null)
                    {
                        nodeTileX = nodeData.TileX;
                        nodeTileZ = nodeData.TileZ;
                    }
                    else
                    {
                        // Node removed from sim — hide
                        nodeView.gameObject.SetActive(false);
                        continue;
                    }

                    var tileVis = fogData.GetVisibility(localPid, nodeTileX, nodeTileZ);

                    if (tileVis == TileVisibility.Visible)
                    {
                        if (!nodeData.IsDepleted)
                        {
                            // Visible and alive — show fully, mark as known
                            knownResourceNodes.Add(kvp.Key);
                            nodeView.gameObject.SetActive(true);
                            nodeView.SetGhostMode(false);
                        }
                        else
                        {
                            // Visible but depleted — re-scouted, confirmed gone
                            nodeView.gameObject.SetActive(false);
                            knownResourceNodes.Remove(kvp.Key);
                        }
                    }
                    else if (tileVis == TileVisibility.Explored && knownResourceNodes.Contains(kvp.Key))
                    {
                        // Previously seen, now in fog — show as ghost
                        if (!nodeData.IsDepleted)
                        {
                            nodeView.gameObject.SetActive(true);
                            nodeView.SetGhostMode(true);
                        }
                        else
                        {
                            // Depleted in fog — keep ghost until re-scouted
                            nodeView.gameObject.SetActive(true);
                            nodeView.SetGhostMode(true);
                        }
                    }
                    else
                    {
                        // Unexplored or never seen — hide
                        nodeView.gameObject.SetActive(false);
                    }
                }
            }

            // Sync projectile view positions with sim data
            var allProjectiles = sim.ProjectileRegistry.GetAllProjectiles();
            for (int i = 0; i < allProjectiles.Count; i++)
            {
                var proj = allProjectiles[i];
                if (projectileViews.TryGetValue(proj.Id, out var pView))
                    pView.UpdatePositions(proj.PreviousPosition, proj.Position);
            }

            // Update fog texture
            if (fogRenderer != null)
                fogRenderer.UpdateTexture(fogData, localPid);
        }
    }
}
