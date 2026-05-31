using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenEmpires
{
    public class BuildingView : MonoBehaviour
    {
        // Wall-family view registry — used to invalidate a neighbor wall's classification
        // when this wall is placed, destroyed, or converted to/from a gate.
        private static readonly System.Collections.Generic.Dictionary<int, BuildingView> wallViewsById
            = new System.Collections.Generic.Dictionary<int, BuildingView>();

        // Shared rally material — created once, reused by all BuildingViews
        private static Material sharedRallyMaterial;

        private static Material GetSharedRallyMaterial()
        {
            if (sharedRallyMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                sharedRallyMaterial = new Material(shader);
                sharedRallyMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always); // Always — render on top of terrain
                sharedRallyMaterial.renderQueue = 3000;
                SetMaterialColor(sharedRallyMaterial, Color.white);
            }
            return sharedRallyMaterial;
        }

        public int BuildingId { get; private set; }
        public int PlayerId { get; private set; }
        public BuildingType BuildingType { get; private set; }
        public bool IsDestroyed { get; private set; }
        public bool IsSelected => isSelected;

        public Rect GetScreenBounds(Camera cam)
        {
            if (bodyRenderers == null || bodyRenderers.Length == 0)
                return default;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            bool anyValid = false;

            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] == null) continue;
                Bounds b = bodyRenderers[i].bounds;
                Vector3 center = b.center;
                Vector3 ext = b.extents;

                bool behindCamera = false;
                for (int cx = -1; cx <= 1 && !behindCamera; cx += 2)
                for (int cy = -1; cy <= 1 && !behindCamera; cy += 2)
                for (int cz = -1; cz <= 1 && !behindCamera; cz += 2)
                {
                    Vector3 corner = new Vector3(
                        center.x + ext.x * cx,
                        center.y + ext.y * cy,
                        center.z + ext.z * cz);
                    Vector3 sp = cam.WorldToScreenPoint(corner);
                    if (sp.z < 0) { behindCamera = true; break; }
                    if (sp.x < minX) minX = sp.x;
                    if (sp.x > maxX) maxX = sp.x;
                    if (sp.y < minY) minY = sp.y;
                    if (sp.y > maxY) maxY = sp.y;
                    anyValid = true;
                }
            }

            if (!anyValid) return default;
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }
        public int OriginTileX { get; private set; }
        public int OriginTileZ { get; private set; }

        private BuildingData buildingData;
        private MapData cachedMapData;
        private float cachedHeightScale;
        private bool isSelected;
        private bool isPreselected;
        private bool isGhostMode;
        private int controlGroupLabel = -1;
        private TextMeshProUGUI controlGroupLabelTMP;
        private GameObject selectionRing;
        private GameObject rangeRing;
        private GameObject influenceZone;

        // Health bar
        private static readonly Color HealthColorFull = new Color(0.2f, 0.8f, 0.2f);
        private static readonly Color HealthColorEmpty = new Color(0.8f, 0.1f, 0.1f);
        private const float HealthBarHeight = 7f;
        private const float HealthBarYOffset = 2.5f;

        // Training queue icons
        private static readonly Color TrainingProgressColor = new Color(0.3f, 0.6f, 1f);
        private const float QueueIconSize = 28f;
        private const float QueueIconGap = 2f;
        private const float QueueYGap = 3f;
        private const int MaxVisibleQueueIcons = 4;
        private static float HealthBarWidth => MaxVisibleQueueIcons * QueueIconSize + (MaxVisibleQueueIcons - 1) * QueueIconGap;

        // Upgrade bar (for towers)
        private static readonly Color UpgradeBarColor = new Color(0.9f, 0.6f, 0.1f);
        private const float UpgradeBarYGap = 2f;

        // Influence buff icon
        private static readonly Color InfluenceIconColor = new Color(1f, 0.85f, 0f);
        private const float InfluenceIconSize = 22f;
        private GameObject influenceIconGO;
        private bool cachedInInfluence;
        private int influenceCheckCooldown;
        private bool externalInfluenceMark;

        // Canvas overlay widgets
        private RectTransform overlayRoot;
        private Image healthBarFill;
        private RectTransform healthBarFillRT;
        private RectTransform queueContainer;
        private RectTransform[] queueSlotRTs;
        private Image[] queueSlotFills;
        private Image[] queueSlotIcons;
        private RectTransform[] queueSlotFillRTs;
        private TextMeshProUGUI overflowLabel;
        private GameObject upgradeBarGO;
        private Image upgradeFill;
        private RectTransform upgradeFillRT;
        private TextMeshProUGUI upgradeQueueText;

        // Shader-safe color access (RGBRecolor shader uses _BaseColor instead of _Color)
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private static Color GetMaterialColor(Material mat)
        {
            if (mat == null) return Color.white;
            if (mat.HasProperty(BaseColorId)) return mat.GetColor(BaseColorId);
            if (mat.HasProperty(ColorId)) return mat.color;
            return Color.white;
        }

        private static void SetMaterialColor(Material mat, Color color)
        {
            if (mat == null) return;
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, color);
            else if (mat.HasProperty(ColorId)) mat.color = color;
        }

        // Influence tint
        private bool influenceTinted;
        private int influenceCheckCounter;
        private bool cachedInfluenceResult;

        // Construction
        private Vector3 fullScale;
        private bool wasUnderConstruction;
        private GameObject constructionScaffold;
        private Renderer constructionScaffoldRenderer;
        private int constructionStage = -1;
        private static readonly System.Collections.Generic.Dictionary<BuildingType, Texture2D[]> constructionStagesByType
            = new System.Collections.Generic.Dictionary<BuildingType, Texture2D[]>();
        private static Texture2D[] defaultConstructionStages;

        private static Texture2D[] GetDefaultConstructionStages()
        {
            if (defaultConstructionStages == null)
            {
                defaultConstructionStages = new[]
                {
                    Resources.Load<Texture2D>("BuildingSprites/Construction/4x4lvl1"),
                    Resources.Load<Texture2D>("BuildingSprites/Construction/4x4lvl2"),
                    Resources.Load<Texture2D>("BuildingSprites/Construction/4x4lvl3"),
                };
            }
            return defaultConstructionStages;
        }

        // Per-type construction stage sprites. Returns null when the type uses the generic scaffold.
        private static Texture2D[] LoadCustomConstructionStages(BuildingType type)
        {
            string[] names = type switch
            {
                BuildingType.Farm => new[] { "BuildingSprites/farm_00", "BuildingSprites/farm_01", "BuildingSprites/farm_02" },
                _ => null,
            };
            if (names == null) return null;
            var arr = new Texture2D[3];
            for (int i = 0; i < 3; i++)
            {
                arr[i] = Resources.Load<Texture2D>(names[i]);
                if (arr[i] == null) return null;
            }
            return arr;
        }

        private static Texture2D GetConstructionStageTexture(int stage, BuildingType type)
        {
            if (!constructionStagesByType.TryGetValue(type, out var arr))
            {
                arr = LoadCustomConstructionStages(type) ?? GetDefaultConstructionStages();
                constructionStagesByType[type] = arr;
            }
            return arr[Mathf.Clamp(stage, 0, 2)];
        }

        // Damage flash
        private int lastSeenDamageTick;
        private float damageFlashTimer;
        private const float DamageFlashDuration = 0.18f;
        private Renderer[] bodyRenderers;
        private Color[] originalColors;
        private bool flashActive;

        // Silhouette
        private Material silhouetteMaterial;

        // Wall connections (diagonal smoothing + merlon hiding)
        private bool wallConnectionsUpdated;

        // Gate visual
        private bool wasGate;
        private Transform wallGeometry;
        private GameObject gateContainer;
        private GameObject gateLeftPillar;
        private GameObject gateRightPillar;
        private GameObject gateArch;
        private GameObject gateLeftCap;
        private GameObject gateRightCap;
        private GameObject gateSpriteQuad;
        private Renderer gateSpriteRenderer;
        private bool gateIsOpen;
        // Gate opens while a unit is on its tile and stays open for this long after the last
        // unit leaves; the timer is reset every frame a unit is still passing through.
        private const float GateOpenHoldDuration = 0.75f;
        private float gateOpenTimer;
        // True while this wall is absorbed into an adjacent gate's 3-tile span — its own body
        // sprite is hidden so the gate sprite covers it. Collider stays (still attackable).
        private bool isCovered;

        // Palisade (wood wall) sprite billboard — replaces procedural body cubes for BuildingType.Wall.
        // YOffsetRatio < 0.5 lowers the quad below the half-height position so the wall art
        // (which sits in the lower portion of the canvas with transparent padding below) rests
        // on the ground instead of floating.
        private const float PalisadeSpriteScale = 6.61f;
        private const float PalisadeSpriteYOffsetRatio = 0.27f;

        // Gate billboard sizes (quad scale). Tunable. Gates now span THREE tiles — the gate tile
        // plus the two collinear wall neighbors it absorbs — so the towers in the art should land
        // over those neighbor tiles. Roughly wall-scale (6.61) is the starting point; tune per
        // orientation via the WallGateSpriteRegistry knobs if towers don't sit on the neighbors.
        private const float WoodGateSpriteScale = 5.82f;
        private const float StoneGateSpriteScale = 7.0f;
        private GameObject palisadeSpriteQuad;
        private Renderer palisadeSpriteRenderer;
        private string lastPalisadeSpriteName;

        // Command confirmation flash
        private float commandFlashTimer;
        private const float CommandFlashDuration = 0.18f;
        private static readonly Color CommandFlashColor = new Color(0.2f, 1f, 0.2f);
        private static readonly Color AttackFlashColor = new Color(1f, 0.2f, 0.2f);
        private bool attackFlashActive;

        // Rally point visualization
        private LineRenderer rallyLine;
        private GameObject rallyDot;

        // Age-based sprite swap
        private string ageSpritePrefix;
        private int maxSpriteAge;
        private int lastKnownAge;
        private Renderer spriteRenderer;
        private string[] ageSpriteNamesByAge; // optional explicit names indexed by age (1-based); overrides prefix

        // Tower upgrade visuals
        private GameObject arrowSlitsVisual;
        private GameObject cannonVisual;
        private GameObject stoneVisual;
        private GameObject visionVisual;
        private bool lastArrowSlits;
        private bool lastCannon;
        private bool lastStone;
        private bool lastVision;

        public void Initialize(int buildingId, Vector3 position, BuildingData data, MapData mapData = null, float heightScale = 0f)
        {
            BuildingId = buildingId;
            PlayerId = data.PlayerId;
            BuildingType = data.Type;
            buildingData = data;
            cachedMapData = mapData;
            cachedHeightScale = heightScale;
            OriginTileX = data.OriginTileX;
            OriginTileZ = data.OriginTileZ;

            if (mapData != null)
                position.y = mapData.SampleHeight(position.x, position.z) * heightScale;
            transform.position = position;

            fullScale = transform.localScale;
            if (data.IsUnderConstruction)
            {
                wasUnderConstruction = true;
            }

            // Build/destroy wall-specific geometry BEFORE CacheRenderers so the palisade
            // sprite is included in bodyRenderers and gets hidden/shown by the construction
            // visibility path (SetBodyRenderersVisible).
            if (data.Type == BuildingType.Wall || data.Type == BuildingType.StoneWall || data.Type == BuildingType.StoneGate || data.Type == BuildingType.WoodGate)
            {
                wallGeometry = transform.Find("WallGeometry");
            }

            if (UsesSpriteWallBody(data.Type))
            {
                EnsurePalisadeSprite();
                HideProceduralWallBody();
            }

            CacheRenderers();

            if (wasUnderConstruction)
            {
                CreateConstructionScaffold();
                SetBodyRenderersVisible(false);
                UpdateConstructionStage(data.ConstructionProgress);
            }

            // Cache reference to the billboard sprite renderer (if any)
            var spriteTransform = transform.Find("Sprite");
            if (spriteTransform != null)
                spriteRenderer = spriteTransform.GetComponent<Renderer>();

            if (IsWallFamily(data.Type))
                wallViewsById[buildingId] = this;
            // Any new building may turn an adjacent wall into a tower (obstacle-cap rule).
            InvalidateNeighborWallViews(OriginTileX, OriginTileZ);

            CreateRallyPointVisuals();
            CreateOverlayWidgets();
        }

        public void SetAgeSpriteInfo(string prefix, int maxAge)
        {
            ageSpritePrefix = prefix;
            ageSpriteNamesByAge = null;
            maxSpriteAge = maxAge;
            var sim = GameBootstrapper.Instance?.Simulation;
            lastKnownAge = sim != null ? Mathf.Clamp(sim.GetPlayerAge(PlayerId), 1, maxAge) : 1;
        }

        // namesByAge is indexed by age (1-based). Index 0 unused. Null/empty entries are skipped.
        public void SetAgeSpriteNames(string[] namesByAge)
        {
            ageSpriteNamesByAge = namesByAge;
            ageSpritePrefix = null;
            maxSpriteAge = namesByAge != null ? namesByAge.Length - 1 : 0;
            var sim = GameBootstrapper.Instance?.Simulation;
            lastKnownAge = sim != null ? Mathf.Clamp(sim.GetPlayerAge(PlayerId), 1, maxSpriteAge) : 1;
        }

        private void CreateConstructionScaffold()
        {
            if (constructionScaffold != null) return;

            var tex = GetConstructionStageTexture(0, buildingData.Type);
            if (tex == null) return;

            var shader = Shader.Find("OpenEmpires/Billboard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) return;

            var mat = new Material(shader);
            mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.05f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry + 1;

            constructionScaffold = GameObject.CreatePrimitive(PrimitiveType.Quad);
            constructionScaffold.name = "ConstructionScaffold";
            constructionScaffold.layer = gameObject.layer;
            var mc = constructionScaffold.GetComponent<MeshCollider>();
            if (mc != null) Object.Destroy(mc);

            constructionScaffold.transform.SetParent(transform);
            float size = Mathf.Max(buildingData.TileFootprintWidth, buildingData.TileFootprintHeight);
            if (size <= 0f) size = 2f;
            float scale = size * 1.5f;
            float yPos = 0f;
            // Farms render their stages at the same scale/Y as the finished Farm.png
            // (see GameSetup CreateBuildingSpritePrefab for Farm: scale 2.75, yRatio 0.25/2.75).
            if (buildingData.Type == BuildingType.Farm)
            {
                scale = 2.75f;
                yPos = 0.25f;
            }
            constructionScaffold.transform.localPosition = new Vector3(0f, yPos, 0f);
            constructionScaffold.transform.localScale = new Vector3(scale, scale, 1f);

            constructionScaffoldRenderer = constructionScaffold.GetComponent<MeshRenderer>();
            constructionScaffoldRenderer.sharedMaterial = mat;
        }

        private void DestroyConstructionScaffold()
        {
            if (constructionScaffold != null)
            {
                Object.Destroy(constructionScaffold);
                constructionScaffold = null;
                constructionScaffoldRenderer = null;
            }
            constructionStage = -1;
        }

        private void UpdateConstructionStage(float progress)
        {
            if (constructionScaffoldRenderer == null) return;
            int stage = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(progress) * 3f), 0, 2);
            if (stage == constructionStage) return;
            var tex = GetConstructionStageTexture(stage, buildingData.Type);
            if (tex != null)
            {
                constructionScaffoldRenderer.material.SetTexture("_MainTex", tex);
                constructionStage = stage;
            }
        }

        private void SetBodyRenderersVisible(bool visible)
        {
            if (bodyRenderers == null) return;
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] != null)
                    bodyRenderers[i].enabled = visible;
            }
        }

        private void CreateRallyPointVisuals()
        {
            // Line from building to rally point
            var lineGO = new GameObject("RallyLine");
            lineGO.transform.SetParent(transform);
            rallyLine = lineGO.AddComponent<LineRenderer>();
            rallyLine.useWorldSpace = true;
            rallyLine.positionCount = 2;
            rallyLine.startWidth = 0.08f;
            rallyLine.endWidth = 0.08f;
            rallyLine.sharedMaterial = GetSharedRallyMaterial();
            rallyLine.startColor = Color.white;
            rallyLine.endColor = Color.white;
            lineGO.SetActive(false);

            // White dot at rally point
            rallyDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rallyDot.name = "RallyDot";
            rallyDot.transform.SetParent(transform);
            rallyDot.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            var dotCollider = rallyDot.GetComponent<Collider>();
            if (dotCollider != null) Object.Destroy(dotCollider);
            var dotRenderer = rallyDot.GetComponent<Renderer>();
            dotRenderer.sharedMaterial = GetSharedRallyMaterial();
            rallyDot.SetActive(false);
        }

        public void SetSelectionRing(GameObject ring)
        {
            selectionRing = ring;
            if (selectionRing != null)
                selectionRing.SetActive(false);
        }

        public void SetRangeRing(GameObject ring)
        {
            rangeRing = ring;
            if (rangeRing != null)
                rangeRing.SetActive(false);
        }

        public void SetInfluenceZone(GameObject zone)
        {
            influenceZone = zone;
            if (influenceZone != null)
            {
                bool show = buildingData != null && buildingData.IsUnderConstruction && !buildingData.IsDestroyed
                    && PlayerId == GetLocalPlayerId();
                influenceZone.SetActive(show);
            }
        }

        private void CacheRenderers()
        {
            var allRenderers = GetComponentsInChildren<Renderer>(true);
            int count = 0;
            for (int i = 0; i < allRenderers.Length; i++)
            {
                if (selectionRing != null && allRenderers[i].transform.IsChildOf(selectionRing.transform))
                    continue;
                if (rangeRing != null && allRenderers[i].transform.IsChildOf(rangeRing.transform))
                    continue;
                if (influenceZone != null && allRenderers[i].transform.IsChildOf(influenceZone.transform))
                    continue;
                count++;
            }
            bodyRenderers = new Renderer[count];
            originalColors = new Color[count];
            int idx = 0;
            for (int i = 0; i < allRenderers.Length; i++)
            {
                if (selectionRing != null && allRenderers[i].transform.IsChildOf(selectionRing.transform))
                    continue;
                if (rangeRing != null && allRenderers[i].transform.IsChildOf(rangeRing.transform))
                    continue;
                if (influenceZone != null && allRenderers[i].transform.IsChildOf(influenceZone.transform))
                    continue;
                bodyRenderers[idx] = allRenderers[i];
                originalColors[idx] = allRenderers[i].sharedMaterial != null ? GetMaterialColor(allRenderers[i].sharedMaterial) : Color.white;
                // Render after tree billboard sprites (Geometry+1)
                bodyRenderers[idx].material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry + 2;
                idx++;
            }

            // Cache silhouette material from second material slot (if present)
            if (bodyRenderers.Length > 0 && bodyRenderers[0] != null)
            {
                var mats = bodyRenderers[0].sharedMaterials;
                if (mats.Length > 1) silhouetteMaterial = mats[1];
            }
        }

        private void Update()
        {
            if (IsDestroyed || buildingData == null) return;

            // Deferred wall connection setup (runs once, after all walls in the batch exist)
            if (!wallConnectionsUpdated && (BuildingType == BuildingType.Wall || BuildingType == BuildingType.StoneWall || BuildingType == BuildingType.StoneGate || BuildingType == BuildingType.WoodGate))
                UpdateWallConnections();

            // Construction scaffold progression (swaps through 4x4lvl1/2/3 stages)
            if (wasUnderConstruction)
            {
                if (buildingData.IsUnderConstruction)
                {
                    UpdateConstructionStage(buildingData.ConstructionProgress);
                }
                else
                {
                    wasUnderConstruction = false;
                    DestroyConstructionScaffold();
                    SetBodyRenderersVisible(true);
                    SFXManager.Instance?.Play(SFXType.ConstructionComplete, transform.position, 0.7f);

                    if (influenceZone != null)
                        influenceZone.SetActive(isSelected && PlayerId == GetLocalPlayerId());
                }
            }

            // Age-based sprite swap
            if ((ageSpritePrefix != null || ageSpriteNamesByAge != null) && spriteRenderer != null)
            {
                var sim = GameBootstrapper.Instance?.Simulation;
                if (sim != null)
                {
                    int currentAge = Mathf.Clamp(sim.GetPlayerAge(PlayerId), 1, maxSpriteAge);
                    if (currentAge != lastKnownAge)
                    {
                        lastKnownAge = currentAge;
                        string spriteName = ageSpriteNamesByAge != null
                            && currentAge >= 0 && currentAge < ageSpriteNamesByAge.Length
                            ? ageSpriteNamesByAge[currentAge]
                            : $"{ageSpritePrefix}{currentAge}";
                        if (!string.IsNullOrEmpty(spriteName))
                        {
                            var tex = Resources.Load<Texture2D>($"BuildingSprites/{spriteName}");
                            if (tex != null)
                                spriteRenderer.material.SetTexture("_MainTex", tex);
                        }
                    }
                }
            }

            // Detect gate toggle
            if (buildingData.IsGate != wasGate)
            {
                var mat = bodyRenderers.Length > 0 && bodyRenderers[0] != null ? bodyRenderers[0].sharedMaterial : null;
                SetGateVisual(buildingData.IsGate, mat);
            }

            // Open the gate while a unit is passing through (cosmetic, view-only).
            if (buildingData.IsGate)
                UpdateGateOpenState();

            // Detect new damage or work strike — apply flash. (LastStrikeTick is the cosmetic
            // hammer-strike pulse from repair/construction; LastDamageTick is real combat damage.)
            int latestTick = buildingData.LastDamageTick > buildingData.LastStrikeTick
                ? buildingData.LastDamageTick : buildingData.LastStrikeTick;
            if (latestTick > lastSeenDamageTick && latestTick > 0)
            {
                lastSeenDamageTick = latestTick;
                damageFlashTimer = DamageFlashDuration;
                SFXManager.Instance?.Play(SFXType.UnitHurt, transform.position, 0.4f);
            }

            UpdateFlash();
            UpdateRallyPointVisuals();

            // Tower upgrade visuals
            if (BuildingType == BuildingType.Tower)
                UpdateTowerUpgradeVisuals();
                
            // Update attack range ring if building's range changes
            UpdateAttackRangeRing();
        }

        private float lastAttackRange = -1f;

        private void UpdateAttackRangeDisplay()
        {
            float range = GetCurrentAttackRange();
            bool canAttack = (buildingData != null && buildingData.AttackDamage > 0) || 
                           (isGhostMode && ghostModeAttackRange > 0);
            
            // Only show attack range if selected by the local player or during a placement preview.
            // isGhostMode is also set for fog-of-war "last-known" enemy buildings — those must NOT show a range ring,
            // so distinguish placement ghosts (which set ghostModeAttackRange > 0) from fog ghosts (which don't).
            bool isLocalPlayerSelection = isSelected && PlayerId == GetLocalPlayerId();
            bool isPlacementGhost = isGhostMode && ghostModeAttackRange > 0;
            bool showRange = canAttack && range > 0 && (isLocalPlayerSelection || isPlacementGhost);
            
            if (rangeRing != null)
                rangeRing.SetActive(showRange);
            else if (showRange)
                CreateAttackRangeRing();
        }

        private void UpdateAttackRangeRing()
        {
            float currentRange = GetCurrentAttackRange();
            
            // Check if range has changed or if we need to create/destroy the ring
            if (currentRange != lastAttackRange)
            {
                lastAttackRange = currentRange;
                
                // Destroy existing range ring if it exists
                if (rangeRing != null)
                {
                    Object.Destroy(rangeRing);
                    rangeRing = null;
                }
                
                // Update display based on current state
                UpdateAttackRangeDisplay();
            }
        }

        private void UpdateRallyPointVisuals()
        {
            bool showRally = isSelected && buildingData.HasRallyPoint;

            // Resolve rally target position and color once
            Vector3 rallyPos = buildingData.RallyPoint.ToVector3();
            bool isGreen = buildingData.RallyPointOnResource || buildingData.RallyPointOnConstruction;
            if (showRally && buildingData.RallyPointUnitId >= 0)
            {
                var sim = GameBootstrapper.Instance?.Simulation;
                if (sim != null)
                {
                    var targetUnit = sim.UnitRegistry.GetUnit(buildingData.RallyPointUnitId);
                    if (targetUnit != null && targetUnit.State != UnitState.Dead)
                    {
                        rallyPos = targetUnit.SimPosition.ToVector3();
                        if (targetUnit.IsSheep) isGreen = true;
                    }
                }
            }
            Color rallyColor = isGreen ? Color.green : Color.white;

            if (rallyLine != null)
            {
                rallyLine.gameObject.SetActive(showRally);
                if (showRally)
                {
                    rallyLine.startColor = rallyColor;
                    rallyLine.endColor = rallyColor;
                    SetMaterialColor(rallyLine.material, rallyColor);

                    Vector3 buildingPos = transform.position + Vector3.up * 0.5f;
                    Vector3 lineEnd = rallyPos;
                    if (cachedMapData != null)
                        lineEnd.y = cachedMapData.SampleHeight(lineEnd.x, lineEnd.z) * cachedHeightScale + 0.1f;
                    else
                        lineEnd.y = 0.1f;
                    rallyLine.SetPosition(0, buildingPos);
                    rallyLine.SetPosition(1, lineEnd);
                }
            }

            if (rallyDot != null)
            {
                rallyDot.SetActive(showRally);
                if (showRally)
                {
                    SetMaterialColor(rallyDot.GetComponent<Renderer>().material, rallyColor);
                    Vector3 dotPos = rallyPos;
                    if (cachedMapData != null)
                        dotPos.y = cachedMapData.SampleHeight(dotPos.x, dotPos.z) * cachedHeightScale + 0.2f;
                    else
                        dotPos.y = 0.2f;
                    rallyDot.transform.position = dotPos;
                }
            }
        }

        public void FlashCommandConfirm()
        {
            commandFlashTimer = CommandFlashDuration;
            attackFlashActive = false;
        }

        public void FlashAttackConfirm()
        {
            commandFlashTimer = CommandFlashDuration;
            attackFlashActive = true;
        }

        public void ClearBuildingData() { buildingData = null; }

        public void SetGhostMode(bool ghost)
        {
            if (isGhostMode == ghost) return;
            isGhostMode = ghost;

            UpdateAttackRangeDisplay();

            if (ghost)
            {
                // Strip silhouette material in ghost mode (semi-transparent ghosts shouldn't show occlusion silhouettes)
                for (int i = 0; i < bodyRenderers.Length; i++)
                {
                    if (bodyRenderers[i] == null) continue;
                    var mats = bodyRenderers[i].sharedMaterials;
                    if (mats.Length > 1)
                        bodyRenderers[i].sharedMaterials = new Material[] { mats[0] };
                    var mat = bodyRenderers[i].material;
                    mat.SetFloat("_Surface", 1);
                    mat.SetFloat("_Blend", 0);
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.renderQueue = 3000;
                    Color c = originalColors[i];
                    c.a = 0.75f;
                    SetMaterialColor(mat, c);
                }
                var col = GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }
            else
            {
                for (int i = 0; i < bodyRenderers.Length; i++)
                {
                    if (bodyRenderers[i] == null) continue;
                    var mat = bodyRenderers[i].material;
                    mat.SetFloat("_Surface", 0);
                    mat.SetOverrideTag("RenderType", "Opaque");
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.DisableKeyword("_ALPHABLEND_ON");
                    mat.renderQueue = -1;
                    SetMaterialColor(mat, originalColors[i]);
                    // Restore silhouette material
                    if (silhouetteMaterial != null)
                        bodyRenderers[i].sharedMaterials = new Material[] { bodyRenderers[i].sharedMaterial, silhouetteMaterial };
                }
                var col = GetComponent<Collider>();
                if (col != null) col.enabled = true;
            }
        }

        private void UpdateFlash()
        {
            // Command flash takes priority over damage flash
            if (commandFlashTimer > 0f)
            {
                Color flashColor = attackFlashActive ? AttackFlashColor : CommandFlashColor;
                if (!flashActive)
                {
                    flashActive = true;
                    for (int i = 0; i < bodyRenderers.Length; i++)
                    {
                        if (bodyRenderers[i] != null)
                            SetMaterialColor(bodyRenderers[i].material, flashColor);
                    }
                }
                else
                {
                    for (int i = 0; i < bodyRenderers.Length; i++)
                    {
                        if (bodyRenderers[i] != null)
                            SetMaterialColor(bodyRenderers[i].material, flashColor);
                    }
                }
                commandFlashTimer -= Time.deltaTime;
                if (commandFlashTimer <= 0f)
                    attackFlashActive = false;
            }
            else if (damageFlashTimer > 0f)
            {
                if (!flashActive)
                {
                    flashActive = true;
                    for (int i = 0; i < bodyRenderers.Length; i++)
                    {
                        if (bodyRenderers[i] != null)
                            SetMaterialColor(bodyRenderers[i].material, Color.white);
                    }
                }
                damageFlashTimer -= Time.deltaTime;
            }
            else if (flashActive)
            {
                flashActive = false;
                influenceTinted = false;
                for (int i = 0; i < bodyRenderers.Length; i++)
                {
                    if (bodyRenderers[i] != null)
                        SetMaterialColor(bodyRenderers[i].material, originalColors[i]);
                }
            }
        }

        private float ghostModeAttackRange = -1f;

        public void SetGhostModeAttackRange(float range)
        {
            ghostModeAttackRange = range;
            if (isGhostMode)
                UpdateAttackRangeDisplay();
        }

        private void CreateAttackRangeRing()
        {
            float range = GetCurrentAttackRange();
            if (range <= 0) return;
            
            // Create range ring GameObject
            var rangeRingGO = new GameObject("AttackRangeRing");
            rangeRingGO.transform.SetParent(transform);
            rangeRingGO.transform.localPosition = Vector3.zero;
            
            // Add spinning range ring component
            var spinningRing = rangeRingGO.AddComponent<SpinningAttackRangeRing>();
            spinningRing.Initialize(range);
            
            rangeRing = rangeRingGO;
            rangeRing.SetActive(true);
        }

        private float GetCurrentAttackRange()
        {
            // In ghost mode, use the explicitly set range
            if (isGhostMode && ghostModeAttackRange > 0)
                return ghostModeAttackRange;
                
            // Otherwise use building data
            if (buildingData != null)
                return buildingData.AttackRange.ToFloat();
                
            return 0f;
        }

        private int GetLocalPlayerId()
        {
            var net = GameBootstrapper.Instance?.Network;
            return net != null && net.IsMultiplayer ? net.LocalPlayerId : 0;
        }

        private bool IsFarmInfluencedByInfluenceBuilding()
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return false;
            int radius = sim.Config.MillInfluenceRadius;
            var buildings = sim.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (!sim.IsInfluenceBuildingType(PlayerId, b.Type)) continue;
                if (b.PlayerId != PlayerId) continue;
                if (b.IsDestroyed) continue;
                int minX = b.OriginTileX - radius;
                int maxX = b.OriginTileX + b.TileFootprintWidth + radius;
                int minZ = b.OriginTileZ - radius;
                int maxZ = b.OriginTileZ + b.TileFootprintHeight + radius;
                if (OriginTileX >= minX && OriginTileX < maxX &&
                    OriginTileZ >= minZ && OriginTileZ < maxZ)
                    return true;
            }
            return false;
        }

        public void SetExternalInfluenceMark(bool show)
        {
            externalInfluenceMark = show;
        }

        public void SetGateVisual(bool isGate, Material mat)
        {
            if (isGate)
            {
                // Hide wall geometry container
                if (wallGeometry != null) wallGeometry.gameObject.SetActive(false);
                if (palisadeSpriteQuad != null) palisadeSpriteQuad.SetActive(false);

                // Create gate container on first call
                if (gateContainer == null)
                {
                    gateContainer = new GameObject("GateContainer");
                    gateContainer.transform.SetParent(transform, false);
                    gateContainer.transform.localPosition = Vector3.zero;
                }

                // All wall-family gates (wood + stone) render an orientation-aware billboard
                // sprite from WallGateSpriteRegistry. Procedural cubes remain only as a
                // fallback for any non-wall gate type that has no sprite art.
                if (IsWoodGateFamily(BuildingType) || IsStoneGateFamily(BuildingType))
                    EnsureGateSpriteQuad();
                else
                    EnsureGateProceduralParts(mat);

                // Orient opening toward the side without adjacent walls (procedural parts only;
                // the billboard sprite picks orientation per-sprite from the registry).
                float rotation = ComputeGateRotation();
                gateContainer.transform.localRotation = Quaternion.Euler(0f, rotation, 0f);

                if (gateSpriteRenderer != null)
                    UpdateGateTexture();

                gateContainer.SetActive(true);
            }
            else
            {
                // Show wall geometry container
                if (wallGeometry != null) wallGeometry.gameObject.SetActive(true);
                if (palisadeSpriteQuad != null && UsesSpriteWallBody(BuildingType))
                    palisadeSpriteQuad.SetActive(true);

                // Hide gate
                if (gateContainer != null) gateContainer.SetActive(false);
            }

            wasGate = isGate;
            CacheRenderers();

            // Gate state change doesn't affect IsWallTile (gates count as walls regardless),
            // but neighbors may still need to re-evaluate visuals (e.g. junction merlons).
            // Also re-classify self in case any presentation depends on it.
            InvalidateWallConnections();
            InvalidateNeighborWallViews(OriginTileX, OriginTileZ);
        }

        private void EnsureGateProceduralParts(Material mat)
        {
            if (gateLeftPillar != null) return;

            // Default orientation: opening along Z-axis, pillars on X-axis
            gateLeftPillar = CreateGatePart("GatePillarLeft",
                new Vector3(-0.375f, 0.55f, 0f), new Vector3(0.25f, 1.1f, 1.0f), mat);
            gateRightPillar = CreateGatePart("GatePillarRight",
                new Vector3(0.375f, 0.55f, 0f), new Vector3(0.25f, 1.1f, 1.0f), mat);
            gateLeftCap = CreateGatePart("GateCapLeft",
                new Vector3(-0.375f, 1.15f, 0f), new Vector3(0.25f, 0.1f, 1.0f), mat);
            gateRightCap = CreateGatePart("GateCapRight",
                new Vector3(0.375f, 1.15f, 0f), new Vector3(0.25f, 0.1f, 1.0f), mat);
            gateArch = CreateGatePart("GateLintel",
                new Vector3(0f, 1.05f, 0f), new Vector3(1.0f, 0.12f, 1.0f), mat);
        }

        private void EnsureGateSpriteQuad()
        {
            if (gateSpriteQuad != null) return;

            var shader = Shader.Find("OpenEmpires/Billboard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return;

            gateSpriteQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            gateSpriteQuad.name = "Sprite";
            gateSpriteQuad.transform.SetParent(gateContainer.transform, false);
            // Initial scale/Y from the gate's family base size; ApplyGateSprite refines it
            // per-sprite (flip / scale multiplier / vertical nudge) once the texture loads.
            float gateScale = GateBaseScale;
            float gateY     = gateScale * PalisadeSpriteYOffsetRatio;
            gateSpriteQuad.transform.localPosition = new Vector3(0f, gateY, 0f);
            gateSpriteQuad.transform.localScale = new Vector3(gateScale, gateScale, 1f);
            gateSpriteQuad.layer = 11;
            var mc = gateSpriteQuad.GetComponent<MeshCollider>();
            if (mc != null) Object.Destroy(mc);

            gateSpriteRenderer = gateSpriteQuad.GetComponent<Renderer>();
            var spriteMat = new Material(shader);
            spriteMat.SetColor("_Color", Color.white);
            // Low alpha cutoff so the sprites' baked semi-transparent drop shadows aren't clipped
            // (a 0.5 cutoff erased soft shadows such as PalisadegateFrontB's). Matches the
            // shadowed-building convention used in GameSetup.
            if (spriteMat.HasProperty("_Cutoff")) spriteMat.SetFloat("_Cutoff", 0.05f);
            spriteMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry + 1;
            gateSpriteRenderer.sharedMaterial = spriteMat;
        }

        private void UpdateGateTexture()
        {
            if (gateSpriteRenderer == null) return;

            // Every wall-family gate (wood + stone) picks its sprite from
            // WallGateSpriteRegistry using the current neighbor-derived WallSegmentKind.
            // Handles 4 orientations (2 cardinal axes + 2 diagonal axes) × open/closed.
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;
            var mask = SampleWallNeighbors(sim.MapData, sim.BuildingRegistry, OriginTileX, OriginTileZ);
            var kind = WallSegmentClassifier.Classify(mask);
            if (WallGateSpriteRegistry.TryLookup(BuildingType, kind, gateIsOpen, out var sel))
                ApplyGateSprite(sel);
        }

        // Applies a registry selection to the gate billboard quad — same transform handling
        // as ApplyPalisadeSprite (UV crop, flip, rotation, per-sprite scale + Y nudge) but
        // sized from the gate's family base scale instead of the wall-body scale.
        private void ApplyGateSprite(WallSpriteSelection sel)
        {
            if (gateSpriteRenderer == null) return;

            var tex = WallSpriteRegistry.LoadTexture(sel.ResourceName);
            if (tex == null) return;
            gateSpriteRenderer.material.SetTexture("_MainTex", tex);
            gateSpriteRenderer.material.mainTextureScale = sel.UvScale;
            gateSpriteRenderer.material.mainTextureOffset = sel.UvOffset;

            if (gateSpriteQuad == null) return;
            float baseScale = GateBaseScale;
            float effectiveScale = baseScale * sel.ScaleMultiplier;
            float sx = sel.FlipX ? -effectiveScale : effectiveScale;
            gateSpriteQuad.transform.localScale = new Vector3(sx, effectiveScale, 1f);
            gateSpriteQuad.transform.localEulerAngles = new Vector3(0f, 0f, sel.RotationDegrees);
            // Per-sprite local position (towers tuned to land over the two absorbed neighbor tiles).
            float targetY = baseScale * PalisadeSpriteYOffsetRatio + sel.WorldYOffset;
            gateSpriteQuad.transform.localPosition = new Vector3(sel.LocalPosX, targetY, sel.LocalPosZ);
        }

        public void SetGateOpen(bool open)
        {
            if (gateIsOpen == open) return;
            gateIsOpen = open;
            UpdateGateTexture();
        }

        // Hide/show this wall's own body sprite when it's absorbed into an adjacent gate's
        // 3-tile span. Driven from UpdateWallConnections (pull model) so it self-heals whenever
        // a neighbor gate is placed, toggled, or destroyed.
        public void SetCovered(bool covered)
        {
            if (isCovered == covered) return;
            isCovered = covered;
            if (palisadeSpriteQuad != null)
                palisadeSpriteQuad.SetActive(!covered);
        }

        // Shows the open sprite while any unit stands on the gate's tile, then holds it open
        // for GateOpenHoldDuration after the last unit leaves. The hold timer is refreshed
        // every frame a unit is present, so it only counts down once the doorway is clear.
        private void UpdateGateOpenState()
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            // The passage spans the gate tile plus the two collinear neighbors it absorbs, so a
            // unit on any of the three opens the gate. Query a 2-tile radius to cover them.
            bool hasPair = WallSegmentClassifier.TryGetCollinearWallPair(
                sim.MapData, sim.BuildingRegistry, OriginTileX, OriginTileZ, PlayerId,
                out var pairA, out var pairB);

            bool unitOnTile = false;
            var nearby = sim.GetUnitsNear(buildingData.SimPosition, Fixed32.FromInt(2));
            for (int i = 0; i < nearby.Count; i++)
            {
                var u = nearby[i];
                if (u.State == UnitState.Dead) continue;
                var tile = sim.MapData.WorldToTile(u.SimPosition);
                int rx = tile.x - OriginTileX;
                int rz = tile.y - OriginTileZ;
                bool onCenter = rx == 0 && rz == 0;
                bool onPair = hasPair && ((rx == pairA.x && rz == pairA.y) || (rx == pairB.x && rz == pairB.y));
                if (onCenter || onPair)
                {
                    unitOnTile = true;
                    break;
                }
            }

            if (unitOnTile)
                gateOpenTimer = GateOpenHoldDuration; // reset on every pass-through
            else if (gateOpenTimer > 0f)
                gateOpenTimer -= Time.deltaTime;

            SetGateOpen(gateOpenTimer > 0f);
        }

        private GameObject CreateGatePart(string partName, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;
            part.transform.SetParent(gateContainer.transform);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
            part.layer = 11;
            var col = part.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var r = part.GetComponent<Renderer>();
            if (mat != null)
            {
                if (silhouetteMaterial != null)
                    r.sharedMaterials = new Material[] { mat, silhouetteMaterial };
                else
                    r.sharedMaterial = mat;
            }
            return part;
        }

        private float ComputeGateRotation()
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null || buildingData == null) return 0f;

            int tx = buildingData.OriginTileX;
            int tz = buildingData.OriginTileZ;
            var map = sim.MapData;

            // Count non-walkable neighbors on each axis
            int xNeighbors = (!map.IsWalkable(tx - 1, tz) ? 1 : 0) + (!map.IsWalkable(tx + 1, tz) ? 1 : 0);
            int zNeighbors = (!map.IsWalkable(tx, tz - 1) ? 1 : 0) + (!map.IsWalkable(tx, tz + 1) ? 1 : 0);

            // Wall runs along the axis with more neighbors; opening is perpendicular
            // Default (0°): pillars on X, opening along Z
            // 90°: pillars on Z, opening along X
            if (zNeighbors > xNeighbors)
                return 90f;
            return 0f;
        }

        private static bool IsWallFamily(BuildingType t)
        {
            return t == BuildingType.Wall
                || t == BuildingType.StoneWall
                || t == BuildingType.StoneGate
                || t == BuildingType.WoodGate;
        }

        // Wall types whose visual is a billboard sprite (rather than the procedural cubes).
        // These types skip the procedural diagonal connectors and use ApplyPalisadeSprite
        // (despite the legacy name) to render their wall art via WallSpriteRegistry.
        private static bool UsesSpriteWallBody(BuildingType t)
        {
            return t == BuildingType.Wall || t == BuildingType.StoneWall;
        }

        // Gate-art families: wood (palisade) gates vs stone (English) gates. The Wall/StoneWall
        // types reach these when converted to a gate; WoodGate/StoneGate are the standalone
        // gate building types. Both families render via WallGateSpriteRegistry.
        private static bool IsWoodGateFamily(BuildingType t)
            => t == BuildingType.Wall || t == BuildingType.WoodGate;
        private static bool IsStoneGateFamily(BuildingType t)
            => t == BuildingType.StoneWall || t == BuildingType.StoneGate;

        // Base quad scale for this building's gate sprite, by family.
        private float GateBaseScale
            => IsWoodGateFamily(BuildingType) ? WoodGateSpriteScale
             : IsStoneGateFamily(BuildingType) ? StoneGateSpriteScale
             : 2.5f;

        private static WallNeighborMask SampleWallNeighbors(MapData map, BuildingRegistry reg, int tx, int tz)
        {
            WallNeighborMask m = WallNeighborMask.None;
            if (map.IsWallTile(tx,     tz + 1, reg)) m |= WallNeighborMask.N;
            if (map.IsWallTile(tx,     tz - 1, reg)) m |= WallNeighborMask.S;
            if (map.IsWallTile(tx + 1, tz,     reg)) m |= WallNeighborMask.E;
            if (map.IsWallTile(tx - 1, tz,     reg)) m |= WallNeighborMask.W;
            if (map.IsWallTile(tx + 1, tz + 1, reg)) m |= WallNeighborMask.NE;
            if (map.IsWallTile(tx - 1, tz + 1, reg)) m |= WallNeighborMask.NW;
            if (map.IsWallTile(tx + 1, tz - 1, reg)) m |= WallNeighborMask.SE;
            if (map.IsWallTile(tx - 1, tz - 1, reg)) m |= WallNeighborMask.SW;
            return m;
        }

        // True when (tx, tz) is impassable terrain, contains a non-wall building/resource,
        // or sits in the Foundation border around a non-wall building (1-tile clearance ring
        // around TCs, Houses, Mills, etc. — walls placed adjacent to those buildings have
        // this Foundation tile between them and the building's actual Building tile).
        // Walls don't count — they're handled by the wall-neighbor mask.
        // Out-of-bounds counts as obstacle so walls placed at the map edge get capped.
        private static bool IsObstacleNonWall(MapData map, BuildingRegistry reg, int tx, int tz)
        {
            if (tx < 0 || tx >= map.Width || tz < 0 || tz >= map.Height) return true;
            if (map.IsWallTile(tx, tz, reg)) return false;
            // Direct building occupancy — catches Farms (walkable but a building) and
            // any non-wall building tile.
            var b = map.GetBuildingAt(tx, tz, reg);
            if (b != null && !b.IsDestroyed && !IsWallFamily(b.Type)) return true;
            // Foundation tiles only ever exist as a 1-tile border around non-wall buildings.
            if (map.Tiles[tx, tz] == TileType.Foundation) return true;
            // Impassable terrain (Rock, Cliff, Water, River, dense forest, holeMap, etc.).
            return !map.IsWalkable(tx, tz);
        }

        // Resets the dirty flag so the next LateUpdate re-runs classification.
        // Safe to call multiple times per frame — flag flip is idempotent.
        public void InvalidateWallConnections()
        {
            wallConnectionsUpdated = false;
        }

        // Invalidate nearby wall tiles so they re-classify on the next frame. Call after a wall is
        // placed, destroyed, or toggled to/from a gate. Radius is 2: crossing-aware classification
        // makes a tile's sprite depend on whether a neighbor is a crossing center, which in turn
        // depends on tiles two steps away — so a 1-ring refresh would leave hugging tiles stale.
        public static void InvalidateNeighborWallViews(int tx, int tz)
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;
            var map = sim.MapData;
            var reg = sim.BuildingRegistry;
            if (map == null || reg == null) return;

            for (int dz = -2; dz <= 2; dz++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    var nb = map.GetBuildingAt(tx + dx, tz + dz, reg);
                    if (nb == null) continue;
                    if (!IsWallFamily(nb.Type)) continue;
                    if (wallViewsById.TryGetValue(nb.Id, out var view) && view != null)
                        view.InvalidateWallConnections();
                }
            }
        }

        private void UpdateWallConnections()
        {
            if (!IsWallFamily(BuildingType) || wallGeometry == null) return;
            wallConnectionsUpdated = true;

            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            int tx = OriginTileX;
            int tz = OriginTileZ;
            var map = sim.MapData;
            var reg = sim.BuildingRegistry;

            WallNeighborMask mask = SampleWallNeighbors(map, reg, tx, tz);
            // Crossing-aware: a straight-through X collapses to one central post, with the four
            // hugging tiles rendering as their straight run. Other shapes use the per-tile rules.
            WallSegmentKind kind = WallSegmentClassifier.ClassifyAt(map, reg, tx, tz);

            bool hasN  = (mask & WallNeighborMask.N)  != 0;
            bool hasS  = (mask & WallNeighborMask.S)  != 0;
            bool hasE  = (mask & WallNeighborMask.E)  != 0;
            bool hasW  = (mask & WallNeighborMask.W)  != 0;
            bool hasNE = (mask & WallNeighborMask.NE) != 0;
            bool hasNW = (mask & WallNeighborMask.NW) != 0;
            bool hasSE = (mask & WallNeighborMask.SE) != 0;
            bool hasSW = (mask & WallNeighborMask.SW) != 0;

            // Any wall whose cardinal neighbor is impassable terrain or a non-wall building
            // becomes a tower (Junction sprite). This is the wall tile that "makes contact"
            // with the obstacle — caps tree lines, mountains, water, buildings, etc.
            if (kind != WallSegmentKind.Junction && kind != WallSegmentKind.Isolated)
            {
                bool nObs = !hasN && IsObstacleNonWall(map, reg, tx,     tz + 1);
                bool sObs = !hasS && IsObstacleNonWall(map, reg, tx,     tz - 1);
                bool eObs = !hasE && IsObstacleNonWall(map, reg, tx + 1, tz);
                bool wObs = !hasW && IsObstacleNonWall(map, reg, tx - 1, tz);
                if (nObs || sObs || eObs || wObs)
                    kind = WallSegmentKind.Junction;
            }

            // Hide merlons at corners that connect to any neighbor (stone walls only — palisade has merlons hidden up-front)
            Transform mNxNz = wallGeometry.Find("Merlon_NxNz");
            Transform mPxNz = wallGeometry.Find("Merlon_PxNz");
            Transform mNxPz = wallGeometry.Find("Merlon_NxPz");
            Transform mPxPz = wallGeometry.Find("Merlon_PxPz");
            if (mNxNz != null) mNxNz.gameObject.SetActive(!(hasW || hasS || hasSW));
            if (mPxNz != null) mPxNz.gameObject.SetActive(!(hasE || hasS || hasSE));
            if (mNxPz != null) mNxPz.gameObject.SetActive(!(hasW || hasN || hasNW));
            if (mPxPz != null) mPxPz.gameObject.SetActive(!(hasE || hasN || hasNE));

            // Procedural diagonal connectors — only for wall types that still use the
            // procedural cube body. Sprite-bodied walls (palisade, stonewall) use
            // dedicated diagonal sprites from the registry instead.
            if (!UsesSpriteWallBody(BuildingType))
            {
                Material wallMat = bodyRenderers.Length > 0 && bodyRenderers[0] != null
                    ? bodyRenderers[0].sharedMaterial : null;

                bool[] diagWalls = { hasNE, hasSE, hasNW, hasSW };
                bool[] cardXArr  = { hasE,  hasE,  hasW,  hasW  };
                bool[] cardZArr  = { hasN,  hasS,  hasN,  hasS  };
                int[] dxArr = { 1, 1, -1, -1 };
                int[] dzArr = { 1, -1, 1, -1 };

                // Clear any stale diagonal connectors from prior runs (re-invalidation can fire repeatedly)
                for (int i = wallGeometry.childCount - 1; i >= 0; i--)
                {
                    var c = wallGeometry.GetChild(i);
                    if (c.name == "DiagBody" || c.name == "DiagLedge")
                        Object.Destroy(c.gameObject);
                }

                for (int d = 0; d < 4; d++)
                {
                    if (!diagWalls[d]) continue;
                    if (cardXArr[d] || cardZArr[d]) continue;

                    int dx = dxArr[d];
                    int dz = dzArr[d];

                    var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    body.name = "DiagBody";
                    body.transform.SetParent(wallGeometry);
                    body.transform.localPosition = new Vector3(dx * 0.5f, 0.375f, dz * 0.5f);
                    body.transform.localScale = new Vector3(0.5f, 0.75f, 0.5f);
                    body.layer = 11;
                    Object.Destroy(body.GetComponent<Collider>());
                    if (wallMat != null) body.GetComponent<Renderer>().sharedMaterial = wallMat;

                    var ledge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    ledge.name = "DiagLedge";
                    ledge.transform.SetParent(wallGeometry);
                    ledge.transform.localPosition = new Vector3(dx * 0.5f, 0.775f, dz * 0.5f);
                    ledge.transform.localScale = new Vector3(0.5f, 0.05f, 0.5f);
                    ledge.layer = 11;
                    Object.Destroy(ledge.GetComponent<Collider>());
                    if (wallMat != null) ledge.GetComponent<Renderer>().sharedMaterial = wallMat;
                }
            }

            // If this wall is currently a gate, the wall-body sprite is hidden; refresh the
            // gate sprite instead so it picks up the orientation change. Otherwise: if this wall
            // is absorbed into an adjacent owner gate's span, hide its body (the gate sprite
            // covers it); else show + refresh the wall-body sprite (sprite-bodied wall types).
            if (buildingData != null && buildingData.IsGate)
            {
                if (gateSpriteRenderer != null)
                    UpdateGateTexture();
            }
            else
            {
                bool covered = UsesSpriteWallBody(BuildingType)
                    && WallSegmentClassifier.TryGetAbsorbingGate(map, reg, tx, tz, out _);
                SetCovered(covered);
                if (!covered && UsesSpriteWallBody(BuildingType)
                    && WallSpriteRegistry.TryLookup(BuildingType, kind, out var sel))
                {
                    ApplyPalisadeSprite(sel);
                }
            }

            CacheRenderers();
        }

        private void EnsurePalisadeSprite()
        {
            if (palisadeSpriteQuad != null) return;

            var shader = Shader.Find("OpenEmpires/Billboard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return;

            palisadeSpriteQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            palisadeSpriteQuad.name = "PalisadeSprite";
            palisadeSpriteQuad.transform.SetParent(transform, false);
            palisadeSpriteQuad.transform.localPosition = new Vector3(0f, PalisadeSpriteScale * PalisadeSpriteYOffsetRatio, 0f);
            palisadeSpriteQuad.transform.localScale = new Vector3(PalisadeSpriteScale, PalisadeSpriteScale, 1f);
            palisadeSpriteQuad.layer = 11;
            var mc = palisadeSpriteQuad.GetComponent<MeshCollider>();
            if (mc != null) Object.Destroy(mc);

            palisadeSpriteRenderer = palisadeSpriteQuad.GetComponent<Renderer>();
            var spriteMat = new Material(shader);
            spriteMat.SetColor("_Color", Color.white);
            // Low alpha cutoff so the wall sprites' baked semi-transparent drop shadows aren't
            // clipped (matches the gate sprites and the shadowed-building convention in GameSetup).
            if (spriteMat.HasProperty("_Cutoff")) spriteMat.SetFloat("_Cutoff", 0.05f);
            spriteMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry + 1;
            palisadeSpriteRenderer.sharedMaterial = spriteMat;

            // Default texture (used until UpdateWallConnections runs and picks the right one)
            var defaultTex = Resources.Load<Texture2D>("BuildingSprites/PalisadeTower");
            if (defaultTex != null)
            {
                palisadeSpriteRenderer.material.SetTexture("_MainTex", defaultTex);
                lastPalisadeSpriteName = "PalisadeTower";
            }
        }

        private void HideProceduralWallBody()
        {
            if (wallGeometry == null) return;

            // Destroy (not just disable) the procedural cubes for palisade walls —
            // the billboard sprite replaces them entirely, and we don't want stray
            // collider/render geometry lingering in the scene.
            var body = wallGeometry.Find("Body");
            var ledge = wallGeometry.Find("Ledge");
            if (body != null) Destroy(body.gameObject);
            if (ledge != null) Destroy(ledge.gameObject);
            for (int i = wallGeometry.childCount - 1; i >= 0; i--)
            {
                var c = wallGeometry.GetChild(i);
                if (c.name.StartsWith("Merlon_"))
                    Destroy(c.gameObject);
            }
        }

        private void ApplyPalisadeSprite(WallSpriteSelection sel)
        {
            if (palisadeSpriteRenderer == null) return;

            if (sel.ResourceName != lastPalisadeSpriteName)
            {
                var tex = WallSpriteRegistry.LoadTexture(sel.ResourceName);
                if (tex == null) return;
                palisadeSpriteRenderer.material.SetTexture("_MainTex", tex);
                lastPalisadeSpriteName = sel.ResourceName;
            }

            // UV crop — zoom in on the wall art so it fills more of the quad.
            // Material instance is unique per BuildingView (created in EnsurePalisadeSprite),
            // so we can safely write to .material without leaking changes across walls.
            palisadeSpriteRenderer.material.mainTextureScale = sel.UvScale;
            palisadeSpriteRenderer.material.mainTextureOffset = sel.UvOffset;

            // Apply optional flip / rotation / per-sprite scale. The registry can reuse
            // the same art with transforms (mirroring, rotation, size variants).
            if (palisadeSpriteQuad != null)
            {
                float effectiveScale = PalisadeSpriteScale * sel.ScaleMultiplier;
                float sx = sel.FlipX ? -effectiveScale : effectiveScale;
                var s = palisadeSpriteQuad.transform.localScale;
                if (!Mathf.Approximately(s.x, sx) || !Mathf.Approximately(s.y, effectiveScale))
                    palisadeSpriteQuad.transform.localScale = new Vector3(sx, effectiveScale, 1f);

                var r = palisadeSpriteQuad.transform.localEulerAngles;
                if (!Mathf.Approximately(r.z, sel.RotationDegrees))
                    palisadeSpriteQuad.transform.localEulerAngles = new Vector3(r.x, r.y, sel.RotationDegrees);

                // Per-sprite vertical nudge on top of the shared Y offset.
                float targetY = PalisadeSpriteScale * PalisadeSpriteYOffsetRatio + sel.WorldYOffset;
                var p = palisadeSpriteQuad.transform.localPosition;
                if (!Mathf.Approximately(p.y, targetY))
                    palisadeSpriteQuad.transform.localPosition = new Vector3(p.x, targetY, p.z);
            }
        }

        private void CreateOverlayWidgets()
        {
            WorldOverlayCanvas.EnsureCreated();
            UnitIcons.EnsureLoaded();

            var rootGO = new GameObject($"BuildingBar_{BuildingId}");
            rootGO.transform.SetParent(WorldOverlayCanvas.Instance.transform, false);
            overlayRoot = rootGO.AddComponent<RectTransform>();
            overlayRoot.sizeDelta = new Vector2(HealthBarWidth, HealthBarHeight);

            // Health bar background
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(rootGO.transform, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            bgGO.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f);
            var bgOutline = bgGO.AddComponent<Outline>();
            bgOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            bgOutline.effectDistance = new Vector2(1, -1);

            // Health bar fill
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(rootGO.transform, false);
            healthBarFillRT = fillGO.AddComponent<RectTransform>();
            healthBarFillRT.anchorMin = Vector2.zero;
            healthBarFillRT.anchorMax = Vector2.one;
            healthBarFillRT.offsetMin = Vector2.zero;
            healthBarFillRT.offsetMax = Vector2.zero;
            healthBarFill = fillGO.AddComponent<Image>();
            healthBarFill.color = HealthColorFull;

            // Control group label badge
            var labelGO = new GameObject("ControlGroupLabel");
            labelGO.transform.SetParent(rootGO.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.sizeDelta = new Vector2(18f, 13f);
            labelRT.anchoredPosition = new Vector2(0f, HealthBarHeight * 0.5f + 3f + 13f * 0.5f);
            controlGroupLabelTMP = labelGO.AddComponent<TextMeshProUGUI>();
            controlGroupLabelTMP.fontSize = 11f;
            controlGroupLabelTMP.fontStyle = FontStyles.Bold;
            controlGroupLabelTMP.color = Color.white;
            controlGroupLabelTMP.alignment = TextAlignmentOptions.Center;
            controlGroupLabelTMP.raycastTarget = false;
            labelGO.SetActive(false);

            // Queue container (above health bar)
            var qcGO = new GameObject("QueueContainer");
            qcGO.transform.SetParent(rootGO.transform, false);
            queueContainer = qcGO.AddComponent<RectTransform>();
            queueContainer.anchoredPosition = new Vector2(0f, HealthBarHeight * 0.5f + QueueYGap + QueueIconSize * 0.5f);
            queueContainer.sizeDelta = new Vector2(HealthBarWidth, QueueIconSize);
            qcGO.SetActive(false);

            // Pre-create queue slots
            queueSlotRTs = new RectTransform[MaxVisibleQueueIcons];
            queueSlotFills = new Image[MaxVisibleQueueIcons];
            queueSlotIcons = new Image[MaxVisibleQueueIcons];
            queueSlotFillRTs = new RectTransform[MaxVisibleQueueIcons];

            for (int i = 0; i < MaxVisibleQueueIcons; i++)
            {
                var slotGO = new GameObject($"Slot_{i}");
                slotGO.transform.SetParent(qcGO.transform, false);
                var slotRT = slotGO.AddComponent<RectTransform>();
                slotRT.sizeDelta = new Vector2(QueueIconSize, QueueIconSize);
                queueSlotRTs[i] = slotRT;

                // Slot background
                var slotBgGO = new GameObject("SlotBg");
                slotBgGO.transform.SetParent(slotGO.transform, false);
                var slotBgRT = slotBgGO.AddComponent<RectTransform>();
                slotBgRT.anchorMin = Vector2.zero;
                slotBgRT.anchorMax = Vector2.one;
                slotBgRT.offsetMin = Vector2.zero;
                slotBgRT.offsetMax = Vector2.zero;
                slotBgGO.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f);

                // Slot fill (training progress — only used for slot 0)
                var slotFillGO = new GameObject("SlotFill");
                slotFillGO.transform.SetParent(slotGO.transform, false);
                var slotFillRT = slotFillGO.AddComponent<RectTransform>();
                slotFillRT.anchorMin = Vector2.zero;
                slotFillRT.anchorMax = Vector2.one;
                slotFillRT.offsetMin = Vector2.zero;
                slotFillRT.offsetMax = Vector2.zero;
                var slotFillImg = slotFillGO.AddComponent<Image>();
                slotFillImg.color = TrainingProgressColor;
                queueSlotFills[i] = slotFillImg;
                queueSlotFillRTs[i] = slotFillRT;
                slotFillGO.SetActive(i == 0); // only slot 0 has progress fill

                // Slot icon
                var slotIconGO = new GameObject("SlotIcon");
                slotIconGO.transform.SetParent(slotGO.transform, false);
                var slotIconRT = slotIconGO.AddComponent<RectTransform>();
                slotIconRT.anchorMin = Vector2.zero;
                slotIconRT.anchorMax = Vector2.one;
                slotIconRT.offsetMin = Vector2.zero;
                slotIconRT.offsetMax = Vector2.zero;
                var slotIconImg = slotIconGO.AddComponent<Image>();
                slotIconImg.preserveAspect = true;
                slotIconImg.color = Color.white;
                queueSlotIcons[i] = slotIconImg;

                slotGO.SetActive(false);
            }

            // Overflow label (last slot position)
            var overflowGO = new GameObject("OverflowLabel");
            overflowGO.transform.SetParent(qcGO.transform, false);
            var overflowRT = overflowGO.AddComponent<RectTransform>();
            overflowRT.sizeDelta = new Vector2(QueueIconSize, QueueIconSize);
            overflowGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f);
            var overflowTextGO = new GameObject("OverflowText");
            overflowTextGO.transform.SetParent(overflowGO.transform, false);
            var overflowTextRT = overflowTextGO.AddComponent<RectTransform>();
            overflowTextRT.anchorMin = Vector2.zero;
            overflowTextRT.anchorMax = Vector2.one;
            overflowTextRT.sizeDelta = Vector2.zero;
            overflowLabel = overflowTextGO.AddComponent<TextMeshProUGUI>();
            overflowLabel.fontSize = 11f;
            overflowLabel.fontStyle = FontStyles.Bold;
            overflowLabel.color = Color.white;
            overflowLabel.alignment = TextAlignmentOptions.Center;
            overflowLabel.raycastTarget = false;
            overflowGO.SetActive(false);

            // Upgrade bar (below health bar)
            upgradeBarGO = new GameObject("UpgradeBar");
            upgradeBarGO.transform.SetParent(rootGO.transform, false);
            var upgradeRT = upgradeBarGO.AddComponent<RectTransform>();
            upgradeRT.sizeDelta = new Vector2(HealthBarWidth, HealthBarHeight);
            upgradeRT.anchoredPosition = new Vector2(0f, -(HealthBarHeight * 0.5f + UpgradeBarYGap + HealthBarHeight * 0.5f));

            var upgBgGO = new GameObject("UpgradeBg");
            upgBgGO.transform.SetParent(upgradeBarGO.transform, false);
            var upgBgRT = upgBgGO.AddComponent<RectTransform>();
            upgBgRT.anchorMin = Vector2.zero;
            upgBgRT.anchorMax = Vector2.one;
            upgBgRT.offsetMin = Vector2.zero;
            upgBgRT.offsetMax = Vector2.zero;
            upgBgGO.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f);

            var upgFillGO = new GameObject("UpgradeFill");
            upgFillGO.transform.SetParent(upgradeBarGO.transform, false);
            upgradeFillRT = upgFillGO.AddComponent<RectTransform>();
            upgradeFillRT.anchorMin = Vector2.zero;
            upgradeFillRT.anchorMax = Vector2.one;
            upgradeFillRT.offsetMin = Vector2.zero;
            upgradeFillRT.offsetMax = Vector2.zero;
            upgradeFill = upgFillGO.AddComponent<Image>();
            upgradeFill.color = UpgradeBarColor;

            var upgTextGO = new GameObject("UpgradeQueueText");
            upgTextGO.transform.SetParent(upgradeBarGO.transform, false);
            var upgTextRT = upgTextGO.AddComponent<RectTransform>();
            upgTextRT.sizeDelta = new Vector2(100f, 16f);
            upgTextRT.anchoredPosition = new Vector2(HealthBarWidth * 0.5f - 50f, HealthBarHeight * 0.5f + 10f);
            upgTextRT.pivot = new Vector2(1f, 0f);
            upgradeQueueText = upgTextGO.AddComponent<TextMeshProUGUI>();
            upgradeQueueText.fontSize = 11f;
            upgradeQueueText.color = Color.white;
            upgradeQueueText.alignment = TextAlignmentOptions.Right;
            upgradeQueueText.raycastTarget = false;
            upgTextGO.SetActive(false);

            upgradeBarGO.SetActive(false);

            // Influence buff icon (yellow circle with + above queue area)
            influenceIconGO = new GameObject("InfluenceIcon");
            influenceIconGO.transform.SetParent(rootGO.transform, false);
            var influenceRT = influenceIconGO.AddComponent<RectTransform>();
            influenceRT.sizeDelta = new Vector2(InfluenceIconSize, InfluenceIconSize);
            // Position above queue container area
            float queueTop = HealthBarHeight * 0.5f + QueueYGap + QueueIconSize;
            influenceRT.anchoredPosition = new Vector2(0f, queueTop + 4f + InfluenceIconSize * 0.5f);

            // Yellow circle background
            var circleImg = influenceIconGO.AddComponent<Image>();
            circleImg.color = InfluenceIconColor;

            // Plus text
            var plusGO = new GameObject("PlusText");
            plusGO.transform.SetParent(influenceIconGO.transform, false);
            var plusRT = plusGO.AddComponent<RectTransform>();
            plusRT.anchorMin = Vector2.zero;
            plusRT.anchorMax = Vector2.one;
            plusRT.sizeDelta = Vector2.zero;
            var plusTMP = plusGO.AddComponent<TextMeshProUGUI>();
            plusTMP.text = "+";
            plusTMP.fontSize = 16f;
            plusTMP.fontStyle = FontStyles.Bold;
            plusTMP.color = new Color(0.15f, 0.15f, 0.15f);
            plusTMP.alignment = TextAlignmentOptions.Center;
            plusTMP.raycastTarget = false;

            influenceIconGO.SetActive(false);

            rootGO.SetActive(false);
        }

        private void UpdateBuildingOverlayUI()
        {
            if (isGhostMode || IsDestroyed || buildingData == null || buildingData.MaxHealth <= 0)
            {
                if (overlayRoot != null && overlayRoot.gameObject.activeSelf)
                    overlayRoot.gameObject.SetActive(false);
                return;
            }

            bool damaged = buildingData.CurrentHealth < buildingData.MaxHealth;
            bool training = buildingData.IsTraining;
            // Reuse the upgrade-bar widget for both Tower upgrades AND research progress
            // (Blacksmith / University). Single visual, two underlying systems.
            bool towerUpgrading = buildingData.Type == BuildingType.Tower && buildingData.IsUpgrading;
            bool upgrading = towerUpgrading || buildingData.IsResearching;

            // Check influence periodically (not every frame)
            if (--influenceCheckCooldown <= 0)
            {
                influenceCheckCooldown = 30;
                var sim = GameBootstrapper.Instance?.Simulation;
                cachedInInfluence = false;
                if (sim != null)
                {
                    bool isUnitProducer = buildingData.Type == BuildingType.Barracks
                        || buildingData.Type == BuildingType.TownCenter
                        || buildingData.Type == BuildingType.ArcheryRange
                        || buildingData.Type == BuildingType.Stables
                        || buildingData.Type == BuildingType.Monastery;
                    if (isUnitProducer)
                        cachedInInfluence = sim.IsBuildingInFrenchLandmarkInfluence(buildingData);
                    else if (buildingData.Type == BuildingType.Farm)
                        cachedInInfluence = IsFarmInfluencedByInfluenceBuilding();
                }
            }

            bool showInfluence = cachedInInfluence || externalInfluenceMark;
            // Farms don't auto-show the HP bar from influence — the "+" buff icon already conveys it.
            bool barShowInfluence = showInfluence && buildingData.Type != BuildingType.Farm;
            if (!isSelected && !damaged && !buildingData.IsUnderConstruction && !training && !upgrading && !barShowInfluence)
            {
                if (overlayRoot != null && overlayRoot.gameObject.activeSelf)
                    overlayRoot.gameObject.SetActive(false);
                return;
            }

            Camera cam = UnitView.CachedMainCamera;
            if (cam == null) return;

            Vector3 worldPos = transform.position + Vector3.up * HealthBarYOffset;
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
            if (screenPos.z < 0f)
            {
                if (overlayRoot.gameObject.activeSelf)
                    overlayRoot.gameObject.SetActive(false);
                return;
            }

            if (!overlayRoot.gameObject.activeSelf)
                overlayRoot.gameObject.SetActive(true);

            overlayRoot.position = new Vector3(screenPos.x, screenPos.y, 0f);

            // Health fill
            float fraction = buildingData.IsUnderConstruction
                ? Mathf.Clamp01(buildingData.ConstructionProgress)
                : Mathf.Clamp01((float)buildingData.CurrentHealth / buildingData.MaxHealth);
            healthBarFillRT.anchorMax = new Vector2(fraction, 1f);
            healthBarFill.color = Color.Lerp(HealthColorEmpty, HealthColorFull, fraction);

            // Training queue
            if (training)
            {
                int localPlayerId = GetLocalPlayerId();
                if (buildingData.PlayerId != localPlayerId)
                {
                    var fog = GameBootstrapper.Instance?.Simulation?.FogOfWar;
                    if (fog == null || fog.GetVisibility(localPlayerId, buildingData.OriginTileX, buildingData.OriginTileZ) != TileVisibility.Visible)
                        training = false;
                }
            }

            if (training)
            {
                if (!queueContainer.gameObject.activeSelf)
                    queueContainer.gameObject.SetActive(true);

                int total = buildingData.TrainingQueue.Count;
                bool hasOverflow = total > MaxVisibleQueueIcons;
                int iconCount = hasOverflow ? MaxVisibleQueueIcons - 1 : Mathf.Min(total, MaxVisibleQueueIcons);
                int slotCount = hasOverflow ? MaxVisibleQueueIcons : iconCount;
                float rowWidth = slotCount * QueueIconSize + (slotCount - 1) * QueueIconGap;
                float startX = -rowWidth * 0.5f + QueueIconSize * 0.5f;

                for (int i = 0; i < MaxVisibleQueueIcons; i++)
                {
                    if (i < iconCount)
                    {
                        if (!queueSlotRTs[i].gameObject.activeSelf)
                            queueSlotRTs[i].gameObject.SetActive(true);
                        queueSlotRTs[i].anchoredPosition = new Vector2(startX + i * (QueueIconSize + QueueIconGap), 0f);

                        int unitType = buildingData.TrainingQueue[i];
                        Sprite icon = UnitIcons.Get(unitType);
                        queueSlotIcons[i].sprite = icon;
                        queueSlotIcons[i].enabled = icon != null;

                        // Training progress fill on slot 0
                        if (i == 0)
                        {
                            queueSlotFills[0].gameObject.SetActive(true);
                            float trainFraction = Mathf.Clamp01(buildingData.TrainingProgress);
                            queueSlotFillRTs[0].anchorMax = new Vector2(trainFraction, 1f);
                        }
                    }
                    else
                    {
                        if (queueSlotRTs[i].gameObject.activeSelf)
                            queueSlotRTs[i].gameObject.SetActive(false);
                    }
                }

                if (hasOverflow)
                {
                    int overflow = total - iconCount;
                    overflowLabel.text = $"+{overflow}";
                    overflowLabel.transform.parent.gameObject.SetActive(true);
                    var overflowRT = overflowLabel.transform.parent.GetComponent<RectTransform>();
                    overflowRT.anchoredPosition = new Vector2(startX + iconCount * (QueueIconSize + QueueIconGap), 0f);
                }
                else
                {
                    if (overflowLabel.transform.parent.gameObject.activeSelf)
                        overflowLabel.transform.parent.gameObject.SetActive(false);
                }
            }
            else
            {
                if (queueContainer.gameObject.activeSelf)
                    queueContainer.gameObject.SetActive(false);
            }

            // Upgrade bar — drives off whichever progress source is active.
            if (upgrading)
            {
                if (!upgradeBarGO.activeSelf)
                    upgradeBarGO.SetActive(true);

                float upgradeFraction = towerUpgrading
                    ? Mathf.Clamp01(buildingData.UpgradeProgress)
                    : Mathf.Clamp01(buildingData.ResearchProgress);
                upgradeFillRT.anchorMax = new Vector2(upgradeFraction, 1f);

                int queueCount = towerUpgrading
                    ? buildingData.UpgradeQueue.Count
                    : buildingData.ResearchQueue.Count;
                bool showQueueCount = queueCount > 1;
                if (upgradeQueueText.gameObject.activeSelf != showQueueCount)
                    upgradeQueueText.gameObject.SetActive(showQueueCount);
                if (showQueueCount)
                    upgradeQueueText.text = $"Queue: {queueCount}";
            }
            else
            {
                if (upgradeBarGO.activeSelf)
                    upgradeBarGO.SetActive(false);
            }

            // Control group badge — show only when selected
            bool showLabel = isSelected && controlGroupLabel >= 0;
            if (controlGroupLabelTMP.gameObject.activeSelf != showLabel)
                controlGroupLabelTMP.gameObject.SetActive(showLabel);
            if (showLabel)
                controlGroupLabelTMP.text = controlGroupLabel.ToString();

            // Influence buff icon
            if (influenceIconGO != null && influenceIconGO.activeSelf != showInfluence)
                influenceIconGO.SetActive(showInfluence);

        }

        private void LateUpdate()
        {
            UpdateBuildingOverlayUI();
            UpdateInfluenceTint();
        }

        private void UpdateInfluenceTint()
        {
            if (BuildingType != BuildingType.Farm) return;
            if (isGhostMode || IsDestroyed || flashActive) return;

            if (++influenceCheckCounter >= 60)
            {
                influenceCheckCounter = 0;
                cachedInfluenceResult = IsFarmInfluencedByInfluenceBuilding();
            }
            bool influenced = cachedInfluenceResult;
            if (influenced && !influenceTinted)
            {
                influenceTinted = true;
                for (int i = 0; i < bodyRenderers.Length; i++)
                {
                    if (bodyRenderers[i] != null)
                        SetMaterialColor(bodyRenderers[i].material, new Color(1f, 0.85f, 0f));
                }
            }
            else if (!influenced && influenceTinted)
            {
                influenceTinted = false;
                for (int i = 0; i < bodyRenderers.Length; i++)
                {
                    if (bodyRenderers[i] != null)
                        SetMaterialColor(bodyRenderers[i].material, originalColors[i]);
                }
            }
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            isPreselected = false;
            if (selectionRing != null)
                selectionRing.SetActive(selected);

            if (influenceZone != null && !IsDestroyed && buildingData != null && !buildingData.IsUnderConstruction)
                influenceZone.SetActive(selected && PlayerId == GetLocalPlayerId());

            UpdateAttackRangeDisplay();
        }

        public void SetControlGroup(int index) { controlGroupLabel = index; }

        public void SetPreselected(bool preselected)
        {
            isPreselected = preselected;
            if (selectionRing != null && !isSelected)
                selectionRing.SetActive(preselected);
        }

        public void OnDestroyed()
        {
            if (IsDestroyed) return;
            IsDestroyed = true;

            if (IsWallFamily(BuildingType))
                wallViewsById.Remove(BuildingId);
            // Any building destruction may un-cap adjacent walls (lose obstacle neighbor).
            InvalidateNeighborWallViews(OriginTileX, OriginTileZ);

            if (overlayRoot != null)
                overlayRoot.gameObject.SetActive(false);

            SetSelected(false);

            // Restore colors in case flash or influence tint was active
            if (flashActive || influenceTinted)
            {
                flashActive = false;
                influenceTinted = false;
                for (int i = 0; i < bodyRenderers.Length; i++)
                {
                    if (bodyRenderers[i] != null)
                        SetMaterialColor(bodyRenderers[i].material, originalColors[i]);
                }
            }

            // Strip silhouette material so it doesn't persist as a solid shape during fade
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] == null) continue;
                var mats = bodyRenderers[i].sharedMaterials;
                if (mats.Length > 1)
                    bodyRenderers[i].sharedMaterials = new Material[] { mats[0] };
            }

            // Disable collider
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            if (gameObject.activeInHierarchy)
                StartCoroutine(DestructionCoroutine());
            else
                Destroy(gameObject);
        }

        private IEnumerator DestructionCoroutine()
        {
            // Shrink Y over 0.5s
            float shrinkDuration = 0.5f;
            float elapsed = 0f;
            Vector3 startScale = transform.localScale;
            Vector3 endScale = new Vector3(startScale.x, 0.05f, startScale.z);
            while (elapsed < shrinkDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / shrinkDuration);
                transform.localScale = Vector3.Lerp(startScale, endScale, t * t);
                yield return null;
            }

            yield return new WaitForSeconds(3f);

            // Fade out
            var renderers = GetComponentsInChildren<Renderer>();
            var materials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                materials[i] = renderers[i].material;
                materials[i].SetFloat("_Surface", 1);
                materials[i].SetFloat("_Blend", 0);
                materials[i].SetOverrideTag("RenderType", "Transparent");
                materials[i].SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                materials[i].SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                materials[i].SetInt("_ZWrite", 0);
                materials[i].EnableKeyword("_ALPHABLEND_ON");
                materials[i].renderQueue = 3000;
            }

            float fadeDuration = 2f;
            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float a = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                for (int i = 0; i < renderers.Length; i++)
                {
                    Color c = GetMaterialColor(materials[i]);
                    c.a = a;
                    SetMaterialColor(materials[i], c);
                }
                yield return null;
            }

            Destroy(gameObject);
        }

        private void OnDisable()
        {
            if (overlayRoot != null && overlayRoot.gameObject.activeSelf)
                overlayRoot.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (overlayRoot != null)
                Destroy(overlayRoot.gameObject);

            // Prevent static registry from holding stale references across scene reloads.
            if (wallViewsById.TryGetValue(BuildingId, out var registered) && registered == this)
                wallViewsById.Remove(BuildingId);
        }

        private void UpdateTowerUpgradeVisuals()
        {
            // Check for Arrow Slits upgrade
            if (buildingData.HasArrowSlits != lastArrowSlits)
            {
                lastArrowSlits = buildingData.HasArrowSlits;
                if (lastArrowSlits && arrowSlitsVisual == null)
                {
                    CreateArrowSlitsVisual();
                }
            }

            // Check for Cannon upgrade
            if (buildingData.HasCannonEmplacement != lastCannon)
            {
                lastCannon = buildingData.HasCannonEmplacement;
                if (lastCannon && cannonVisual == null)
                {
                    CreateCannonVisual();
                }
            }

            // Check for Stone upgrade
            if (buildingData.HasStoneUpgrade != lastStone)
            {
                lastStone = buildingData.HasStoneUpgrade;
                if (lastStone && stoneVisual == null)
                {
                    CreateStoneVisual();
                }
            }

            // Check for Vision upgrade
            if (buildingData.HasVisionUpgrade != lastVision)
            {
                lastVision = buildingData.HasVisionUpgrade;
                if (lastVision && visionVisual == null)
                {
                    CreateVisionVisual();
                }
            }
        }

        private void CreateArrowSlitsVisual()
        {
            // Create small dark arrow slit windows on the tower
            arrowSlitsVisual = new GameObject("ArrowSlits");
            arrowSlitsVisual.transform.SetParent(transform, false);
            arrowSlitsVisual.transform.localPosition = new Vector3(0, 1.5f, 0);

            // Create multiple small dark recesses to represent arrow slits
            for (int i = 0; i < 8; i++) // More slits for better look
            {
                var slit = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slit.transform.SetParent(arrowSlitsVisual.transform, false);
                slit.transform.localScale = new Vector3(0.08f, 0.25f, 0.02f);
                
                float angle = i * 45f; // 8 slits around the tower
                float x = Mathf.Sin(angle * Mathf.Deg2Rad) * 0.85f;
                float z = Mathf.Cos(angle * Mathf.Deg2Rad) * 0.85f;
                slit.transform.localPosition = new Vector3(x, 0, z);
                slit.transform.localRotation = Quaternion.Euler(0, angle, 0);
                
                // Get the existing tower material to match colors
                var renderer = slit.GetComponent<Renderer>();
                var towerRenderer = GetComponent<Renderer>();
                if (towerRenderer != null && towerRenderer.material != null)
                {
                    renderer.material = new Material(towerRenderer.material);
                    // Make it darker to represent the slit opening
                    Color baseColor = GetMaterialColor(towerRenderer.material);
                    SetMaterialColor(renderer.material, new Color(baseColor.r * 0.2f, baseColor.g * 0.2f, baseColor.b * 0.2f));
                }
                else
                {
                    // Fallback to default material
                    SetMaterialColor(renderer.material, new Color(0.15f, 0.12f, 0.1f));
                }
            }
        }

        private void CreateCannonVisual()
        {
            // Create 4 cannon barrels on the sides of the tower
            cannonVisual = new GameObject("Cannons");
            cannonVisual.transform.SetParent(transform, false);
            cannonVisual.transform.localPosition = new Vector3(0, 1.5f, 0);

            for (int i = 0; i < 4; i++)
            {
                var cannon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cannon.transform.SetParent(cannonVisual.transform, false);
                cannon.transform.localScale = new Vector3(0.2f, 0.8f, 0.2f);
                
                float angle = i * 90f;
                float x = Mathf.Sin(angle * Mathf.Deg2Rad) * 0.9f;
                float z = Mathf.Cos(angle * Mathf.Deg2Rad) * 0.9f;
                cannon.transform.localPosition = new Vector3(x, 0, z);
                cannon.transform.localRotation = Quaternion.Euler(0, 0, 90) * Quaternion.Euler(0, angle, 0); // Point outward
                
                // Use tower material instead of creating new Standard material
                var renderer = cannon.GetComponent<Renderer>();
                var towerRenderer = GetComponent<Renderer>();
                if (towerRenderer != null && towerRenderer.material != null)
                {
                    renderer.material = new Material(towerRenderer.material);
                    SetMaterialColor(renderer.material, new Color(0.25f, 0.22f, 0.18f)); // Dark metallic cannon
                }
                else
                {
                    SetMaterialColor(renderer.material, new Color(0.25f, 0.22f, 0.18f));
                }

                // Add cannon support/mount  
                var mount = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mount.transform.SetParent(cannonVisual.transform, false);
                mount.transform.localScale = new Vector3(0.3f, 0.2f, 0.3f);
                mount.transform.localPosition = new Vector3(x * 0.7f, -0.1f, z * 0.7f);
                
                var mountRenderer = mount.GetComponent<Renderer>();
                if (towerRenderer != null && towerRenderer.material != null)
                {
                    mountRenderer.material = new Material(towerRenderer.material);
                    // Keep original tower color for mount
                }
                else
                {
                    SetMaterialColor(mountRenderer.material, new Color(0.65f, 0.6f, 0.5f));
                }
            }
        }

        private void CreateStoneVisual()
        {
            // Sprite-based tower: the billboard is already the stone watchtower from
            // the moment the tower is built, so the stone upgrade is a stats-only
            // event with no visual change. Drop a marker so we don't re-enter.
            if (spriteRenderer != null)
            {
                stoneVisual = new GameObject("StoneReinforcement");
                stoneVisual.transform.SetParent(transform, false);
                return;
            }

            // Procedural tower fallback: add stone reinforcement geometry.
            stoneVisual = new GameObject("StoneReinforcement");
            stoneVisual.transform.SetParent(transform, false);
            stoneVisual.transform.localPosition = Vector3.zero;

            // Create stone bands around the tower instead of a full layer
            var towerRenderer = GetComponent<Renderer>();
            for (int band = 0; band < 3; band++)
            {
                var stoneRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stoneRing.transform.SetParent(stoneVisual.transform, false);
                stoneRing.transform.localScale = new Vector3(2.05f, 0.15f, 2.05f);
                stoneRing.transform.localPosition = new Vector3(0, 0.5f + band * 0.8f, 0);
                
                var renderer = stoneRing.GetComponent<Renderer>();
                if (towerRenderer != null && towerRenderer.material != null)
                {
                    renderer.material = new Material(towerRenderer.material);
                    Color baseColor = GetMaterialColor(towerRenderer.material);
                    // Slightly lighter for reinforcement bands
                    SetMaterialColor(renderer.material, new Color(baseColor.r * 1.1f, baseColor.g * 1.1f, baseColor.b * 1.1f));
                }
                else
                {
                    SetMaterialColor(renderer.material, new Color(0.8f, 0.75f, 0.65f));
                }
            }

            // Add corner stone reinforcements
            for (int i = 0; i < 4; i++)
            {
                var cornerStone = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cornerStone.transform.SetParent(stoneVisual.transform, false);
                cornerStone.transform.localScale = new Vector3(0.2f, 3.0f, 0.2f);
                
                float angle = i * 90f;
                float x = Mathf.Sin(angle * Mathf.Deg2Rad) * 1.03f;
                float z = Mathf.Cos(angle * Mathf.Deg2Rad) * 1.03f;
                cornerStone.transform.localPosition = new Vector3(x, 1.5f, z);
                
                var cornerRenderer = cornerStone.GetComponent<Renderer>();
                if (towerRenderer != null && towerRenderer.material != null)
                {
                    cornerRenderer.material = new Material(towerRenderer.material);
                    Color baseColor = GetMaterialColor(towerRenderer.material);
                    // Darker corners for structural appearance
                    SetMaterialColor(cornerRenderer.material, new Color(baseColor.r * 0.8f, baseColor.g * 0.8f, baseColor.b * 0.8f));
                }
                else
                {
                    SetMaterialColor(cornerRenderer.material, new Color(0.6f, 0.55f, 0.45f));
                }
            }
        }

        private void CreateVisionVisual()
        {
            // Create a telescope for enhanced vision
            visionVisual = new GameObject("Telescope");
            visionVisual.transform.SetParent(transform, false);
            visionVisual.transform.localPosition = new Vector3(0, 2.8f, 0);

            // Telescope base/mount
            var telescopeBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            telescopeBase.transform.SetParent(visionVisual.transform, false);
            telescopeBase.transform.localScale = new Vector3(0.3f, 0.15f, 0.3f);
            telescopeBase.transform.localPosition = new Vector3(0, 0, 0);
            
            // Get tower material for consistency
            var towerRenderer = GetComponent<Renderer>();
            var baseRenderer = telescopeBase.GetComponent<Renderer>();
            if (towerRenderer != null && towerRenderer.material != null)
            {
                baseRenderer.material = new Material(towerRenderer.material);
                // Keep tower color for base
            }
            else
            {
                SetMaterialColor(baseRenderer.material, new Color(0.65f, 0.6f, 0.5f));
            }

            // Telescope tube
            var tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tube.transform.SetParent(visionVisual.transform, false);
            tube.transform.localScale = new Vector3(0.15f, 0.6f, 0.15f);
            tube.transform.localPosition = new Vector3(0.2f, 0.3f, 0.2f);
            tube.transform.localRotation = Quaternion.Euler(30, 45, 0); // Angled upward
            
            var tubeRenderer = tube.GetComponent<Renderer>();
            if (towerRenderer != null && towerRenderer.material != null)
            {
                tubeRenderer.material = new Material(towerRenderer.material);
                SetMaterialColor(tubeRenderer.material, new Color(0.3f, 0.25f, 0.2f)); // Darker for metal tube
            }
            else
            {
                SetMaterialColor(tubeRenderer.material, new Color(0.3f, 0.25f, 0.2f));
            }

            // Telescope lens 
            var lens = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lens.transform.SetParent(visionVisual.transform, false);
            lens.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
            lens.transform.localPosition = new Vector3(0.35f, 0.65f, 0.35f);
            
            var lensRenderer = lens.GetComponent<Renderer>();
            if (towerRenderer != null && towerRenderer.material != null)
            {
                lensRenderer.material = new Material(towerRenderer.material);
                SetMaterialColor(lensRenderer.material, new Color(0.8f, 0.85f, 0.9f)); // Light bluish for glass
            }
            else
            {
                SetMaterialColor(lensRenderer.material, new Color(0.8f, 0.85f, 0.9f));
            }
        }
    }
}
