using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenEmpires
{
    public class BuildingView : MonoBehaviour
    {
        // Shared rally material — created once, reused by all BuildingViews
        private static Material sharedRallyMaterial;

        private static Material GetSharedRallyMaterial()
        {
            if (sharedRallyMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                sharedRallyMaterial = new Material(shader);
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

        // Command confirmation flash
        private float commandFlashTimer;
        private const float CommandFlashDuration = 0.18f;
        private static readonly Color CommandFlashColor = new Color(0.2f, 1f, 0.2f);
        private static readonly Color AttackFlashColor = new Color(1f, 0.2f, 0.2f);
        private bool attackFlashActive;

        // Rally point visualization
        private LineRenderer rallyLine;
        private GameObject rallyDot;

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
                transform.localScale = new Vector3(fullScale.x, fullScale.y * 0.1f, fullScale.z);
            }

            CacheRenderers();

            if (data.Type == BuildingType.Wall)
            {
                wallGeometry = transform.Find("WallGeometry");
            }

            CreateRallyPointVisuals();
            CreateOverlayWidgets();
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
            if (!wallConnectionsUpdated && BuildingType == BuildingType.Wall)
                UpdateWallConnections();

            // Construction scale animation
            if (wasUnderConstruction)
            {
                if (buildingData.IsUnderConstruction)
                {
                    float progress = buildingData.ConstructionProgress;
                    float scaleY = Mathf.Lerp(0.1f, 1f, progress);
                    transform.localScale = new Vector3(fullScale.x, fullScale.y * scaleY, fullScale.z);
                }
                else
                {
                    transform.localScale = fullScale;
                    wasUnderConstruction = false;
                    SFXManager.Instance?.Play(SFXType.ConstructionComplete, transform.position, 0.7f);

                    if (influenceZone != null)
                        influenceZone.SetActive(isSelected && PlayerId == GetLocalPlayerId());
                }
            }

            // Detect gate toggle
            if (buildingData.IsGate != wasGate)
            {
                var mat = bodyRenderers.Length > 0 && bodyRenderers[0] != null ? bodyRenderers[0].sharedMaterial : null;
                SetGateVisual(buildingData.IsGate, mat);
            }

            // Detect new damage — apply flash
            if (buildingData.LastDamageTick > lastSeenDamageTick && buildingData.LastDamageTick > 0)
            {
                lastSeenDamageTick = buildingData.LastDamageTick;
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
            
            // Only show attack range if selected by the local player or in ghost mode
            bool isLocalPlayerSelection = isSelected && PlayerId == GetLocalPlayerId();
            bool showRange = canAttack && range > 0 && (isLocalPlayerSelection || isGhostMode);
            
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
            bool isGreen = buildingData.RallyPointOnResource;
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
                    c.a = 0.4f;
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
            BuildingType influenceType = sim.GetInfluenceBuildingType(PlayerId);
            var buildings = sim.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (b.Type != influenceType) continue;
                if (b.PlayerId != PlayerId) continue;
                if (b.IsDestroyed || b.IsUnderConstruction) continue;
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

        public void SetGateVisual(bool isGate, Material mat)
        {
            if (isGate)
            {
                // Hide wall geometry container
                if (wallGeometry != null) wallGeometry.gameObject.SetActive(false);

                // Create gate container and geometry on first call
                if (gateContainer == null)
                {
                    gateContainer = new GameObject("GateContainer");
                    gateContainer.transform.SetParent(transform, false);
                    gateContainer.transform.localPosition = Vector3.zero;

                    // Default orientation: opening along Z-axis, pillars on X-axis
                    // Pillars: connect seamlessly to adjacent walls, full depth in Z
                    gateLeftPillar = CreateGatePart("GatePillarLeft",
                        new Vector3(-0.375f, 0.55f, 0f), new Vector3(0.25f, 1.1f, 1.0f), mat);
                    gateRightPillar = CreateGatePart("GatePillarRight",
                        new Vector3(0.375f, 0.55f, 0f), new Vector3(0.25f, 1.1f, 1.0f), mat);

                    // Pillar caps: match pillar width for seamless connection
                    gateLeftCap = CreateGatePart("GateCapLeft",
                        new Vector3(-0.375f, 1.15f, 0f), new Vector3(0.25f, 0.1f, 1.0f), mat);
                    gateRightCap = CreateGatePart("GateCapRight",
                        new Vector3(0.375f, 1.15f, 0f), new Vector3(0.25f, 0.1f, 1.0f), mat);

                    // Lintel: spans the full width between pillars
                    gateArch = CreateGatePart("GateLintel",
                        new Vector3(0f, 1.05f, 0f), new Vector3(1.0f, 0.12f, 1.0f), mat);
                }

                // Orient opening toward the side without adjacent walls
                float rotation = ComputeGateRotation();
                gateContainer.transform.localRotation = Quaternion.Euler(0f, rotation, 0f);

                gateContainer.SetActive(true);
            }
            else
            {
                // Show wall geometry container
                if (wallGeometry != null) wallGeometry.gameObject.SetActive(true);

                // Hide gate
                if (gateContainer != null) gateContainer.SetActive(false);
            }

            wasGate = isGate;
            CacheRenderers();
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

        private void UpdateWallConnections()
        {
            if (BuildingType != BuildingType.Wall || wallGeometry == null) return;
            wallConnectionsUpdated = true;

            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            int tx = OriginTileX;
            int tz = OriginTileZ;
            var map = sim.MapData;

            // Check all 8 neighbors for non-walkable tiles (walls/buildings)
            bool nxWall = !map.IsWalkable(tx - 1, tz);
            bool pxWall = !map.IsWalkable(tx + 1, tz);
            bool nzWall = !map.IsWalkable(tx, tz - 1);
            bool pzWall = !map.IsWalkable(tx, tz + 1);
            bool nxnzWall = !map.IsWalkable(tx - 1, tz - 1);
            bool pxnzWall = !map.IsWalkable(tx + 1, tz - 1);
            bool nxpzWall = !map.IsWalkable(tx - 1, tz + 1);
            bool pxpzWall = !map.IsWalkable(tx + 1, tz + 1);

            // Hide merlons at corners that connect to any neighbor
            Transform mNxNz = wallGeometry.Find("Merlon_NxNz");
            Transform mPxNz = wallGeometry.Find("Merlon_PxNz");
            Transform mNxPz = wallGeometry.Find("Merlon_NxPz");
            Transform mPxPz = wallGeometry.Find("Merlon_PxPz");

            if (mNxNz != null) mNxNz.gameObject.SetActive(!(nxWall || nzWall || nxnzWall));
            if (mPxNz != null) mPxNz.gameObject.SetActive(!(pxWall || nzWall || pxnzWall));
            if (mNxPz != null) mNxPz.gameObject.SetActive(!(nxWall || pzWall || nxpzWall));
            if (mPxPz != null) mPxPz.gameObject.SetActive(!(pxWall || pzWall || pxpzWall));

            // Add diagonal body+ledge connectors where there's a pure diagonal connection
            // (diagonal neighbor exists but no cardinal wall in between to fill the gap)
            Material wallMat = bodyRenderers.Length > 0 && bodyRenderers[0] != null
                ? bodyRenderers[0].sharedMaterial : null;

            bool[] diagWalls = { pxpzWall, pxnzWall, nxpzWall, nxnzWall };
            bool[] cardXArr  = { pxWall,   pxWall,   nxWall,   nxWall   };
            bool[] cardZArr  = { pzWall,   nzWall,   pzWall,   nzWall   };
            int[] dxArr = { 1, 1, -1, -1 };
            int[] dzArr = { 1, -1, 1, -1 };

            for (int d = 0; d < 4; d++)
            {
                if (!diagWalls[d]) continue;
                if (cardXArr[d] || cardZArr[d]) continue;

                int dx = dxArr[d];
                int dz = dzArr[d];

                // Body connector at the corner, bridging the diagonal step
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "DiagBody";
                body.transform.SetParent(wallGeometry);
                body.transform.localPosition = new Vector3(dx * 0.5f, 0.375f, dz * 0.5f);
                body.transform.localScale = new Vector3(0.5f, 0.75f, 0.5f);
                body.layer = 11;
                Object.Destroy(body.GetComponent<Collider>());
                if (wallMat != null) body.GetComponent<Renderer>().sharedMaterial = wallMat;

                // Matching ledge connector
                var ledge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ledge.name = "DiagLedge";
                ledge.transform.SetParent(wallGeometry);
                ledge.transform.localPosition = new Vector3(dx * 0.5f, 0.775f, dz * 0.5f);
                ledge.transform.localScale = new Vector3(0.5f, 0.05f, 0.5f);
                ledge.layer = 11;
                Object.Destroy(ledge.GetComponent<Collider>());
                if (wallMat != null) ledge.GetComponent<Renderer>().sharedMaterial = wallMat;
            }

            CacheRenderers();
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
            bool upgrading = buildingData.Type == BuildingType.Tower && buildingData.IsUpgrading;
            if (!isSelected && !damaged && !buildingData.IsUnderConstruction && !training && !upgrading)
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

            // Upgrade bar
            if (upgrading)
            {
                if (!upgradeBarGO.activeSelf)
                    upgradeBarGO.SetActive(true);

                float upgradeFraction = Mathf.Clamp01(buildingData.UpgradeProgress);
                upgradeFillRT.anchorMax = new Vector2(upgradeFraction, 1f);

                bool showQueueCount = buildingData.UpgradeQueue.Count > 1;
                if (upgradeQueueText.gameObject.activeSelf != showQueueCount)
                    upgradeQueueText.gameObject.SetActive(showQueueCount);
                if (showQueueCount)
                    upgradeQueueText.text = $"Queue: {buildingData.UpgradeQueue.Count}";
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
            // Add stone reinforcement to the tower
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
