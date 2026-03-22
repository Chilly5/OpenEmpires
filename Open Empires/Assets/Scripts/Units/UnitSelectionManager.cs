using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenEmpires
{
    public class UnitSelectionManager : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void RegisterKeyboardOverrides();
#endif

        private static UnitSelectionManager instance;

        private static bool minimapSuppressed;
        private static bool infoPanelSuppressed;
        private static bool settingsMenuOpen;
        private static bool chatFocused;
        public static bool UIInputSuppressed => minimapSuppressed || infoPanelSuppressed || settingsMenuOpen || chatFocused;
        public static void SetMinimapSuppressed(bool value) { minimapSuppressed = value; }
        public static void SetInfoPanelSuppressed(bool value) { infoPanelSuppressed = value; }
        public static void SetSettingsMenuOpen(bool value) { settingsMenuOpen = value; }
        public static void SetChatFocused(bool value) { chatFocused = value; }
        public static bool IsSettingsMenuOpen => settingsMenuOpen;

        [SerializeField] private LayerMask unitLayer;
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private LayerMask resourceLayer;
        [SerializeField] private LayerMask buildingLayer;
        [SerializeField] private GameSetup gameSetup;
        [SerializeField] private float dragThreshold = 100f;

        private RTSInputActions inputActions;
        private Camera mainCamera;

        private Dictionary<string, InputAction> remappableActions;
        public static IReadOnlyDictionary<string, InputAction> RemappableActions => instance?.remappableActions;
        public static bool IsRebinding => SettingsMenuUI.IsRebinding;

        private List<UnitView> selectedUnits = new List<UnitView>();
        private Dictionary<int, UnitView> unitViews = new Dictionary<int, UnitView>();

        private List<BuildingView> selectedBuildings = new List<BuildingView>();
        private Dictionary<int, BuildingView> buildingViews = new Dictionary<int, BuildingView>();

        public IReadOnlyList<BuildingView> SelectedBuildings => selectedBuildings;

        private ResourceNode selectedResourceNode;
        public ResourceNode SelectedResourceNode => selectedResourceNode;

        private int lastClickedBuildingId = -1;

        public int LocalPlayerId
        {
            get
            {
                var net = GameBootstrapper.Instance?.Network;
                return net != null && net.IsMultiplayer ? net.LocalPlayerId : 0;
            }
        }

        private bool isDragging;
        private Vector2 dragStartScreen;
        private Vector2 currentMousePos;
        private bool selectHeld;
        private bool multiSelectHeld;

        // Double-click detection
        private float lastClickTime;
        private int lastClickedUnitId = -1;
        private const float DoubleClickTime = 0.3f;

        // Right-click drag formation
        private bool commandHeld;
        private bool isRightDragging;
        private Vector2 rightDragStartScreen;
        private Vector3 rightDragStartWorld;
        private float rightClickDownTime;

        // Attack-move mode
        private bool attackMoveMode;
        public bool IsAttackMoveMode => attackMoveMode;
        public void ClearAttackMoveMode() { attackMoveMode = false; }

        // Garrison targeting mode
        private bool garrisonMode;

        // Patrol targeting mode
        private bool patrolMode;

        // Meteor strike targeting mode
        private bool isMeteorTargeting;
        private GameObject meteorTargetingPreview;

        // Healing Rain targeting mode
        private bool isHealingRainTargeting;
        private GameObject healingRainTargetingPreview;

        // Lightning Storm targeting mode
        private bool isLightningStormTargeting;
        private GameObject lightningStormTargetingPreview;

        // Tsunami targeting mode
        private bool isTsunamiTargeting;
        private bool tsunamiDragging;
        private Vector3 tsunamiOriginWorld;
        private GameObject tsunamiTargetingPreview;
        private GameObject tsunamiArrowPreview;

        // Suppress OnSelectCanceled after a placement click
        private bool placementConsumedClick;

        // Building placement mode
        private bool isPlacingBuilding;
        private bool placementIsShiftQueued; // true when placement was kept alive via shift
        private BuildingType placementBuildingType;
        private int[] placementVillagerIds;
        private LandmarkId placementLandmarkId; // only meaningful when placementBuildingType == Landmark
        private GameObject ghostBuilding;
        private GameObject ghostAttackRangeRing;
        private GameObject ghostInfluenceZone;       // follows ghost cursor (Mill placement)
        private List<GameObject> ghostInfluenceZones; // static at existing Mills (Farm placement)
        private Material ghostValidMaterial;
        private Material ghostInvalidMaterial;
        private Material ghostInfluenceMaterial;
        private bool ghostIsValid;
        private bool ghostInInfluenceZone;
        private GameObject ghostInfluenceIcon; // the "+" overlay icon
        private List<BuildingView> markedInfluenceBuildingViews = new List<BuildingView>();
        private int snappedTileX, snappedTileZ;
        private bool snappedPositionValid;
        private GameObject gridOverlay;
        private Material gridMaterial;
        private Texture2D gridTexture;

        // Wall placement mode
        private bool isPlacingWall;
        private bool wallDragging;
        private int wallStartTileX, wallStartTileZ;
        private int[] wallVillagerIds;
        private BuildingType wallPlacementType = BuildingType.Wall;
        private bool wallPlacementIsGate;
        private List<GameObject> wallGhostPool = new List<GameObject>();
        private int wallGhostActiveCount;

        // Control groups (CTRL+0-9 to assign, 0-9 to recall)
        private Dictionary<int, List<int>> controlGroups = new Dictionary<int, List<int>>();

        // Building control groups — parallel to unit controlGroups, mutually exclusive per index
        private Dictionary<int, List<int>> buildingControlGroups = new Dictionary<int, List<int>>();

        // Tab cycling state: which building type is currently "shown" for an active building group
        private BuildingType? activeTabBuildingType = null;

        // Public accessor for UnitInfoUI
        public BuildingType? ActiveTabBuildingType => activeTabBuildingType;
        public void SetActiveTabBuildingType(BuildingType? t) => activeTabBuildingType = t;

        // Last recalled control group index (for select-then-pan behaviour)
        private int lastRecalledGroupIndex = -1;

        // Cached camera controller reference
        private RTSCameraController cameraController;

        // Dummy placement ghost
        private GameObject dummyGhost;

        // Hover tracking
        private UnitView hoveredUnit;
        private ResourceNode hoveredResource;
        private GameObject resourceCursorIcon;
        private Image resourceCursorImage;
        private GameObject attackCursorIcon;
        private Image attackCursorImage;
        private Sprite attackIconSprite;
        private GameObject garrisonCursorIcon;
        private Image garrisonCursorImage;
        private Sprite garrisonIconSprite;
        private GameObject patrolCursorIcon;
        private Image patrolCursorImage;
        private Sprite patrolIconSprite;
        private GameObject actionCursorIcon;
        private Image actionCursorImage;
        private GameObject healCursorIcon;
        private Image healCursorImage;

        // Hold-to-delete
        private float deleteHoldTimer;
        private bool deleteHolding;
        private const float DeleteHoldDuration = 2f;

        /// <summary>0-1 progress of the hold-to-delete timer. 0 when not holding.</summary>
        public float DeleteHoldProgress => deleteHolding ? Mathf.Clamp01(deleteHoldTimer / DeleteHoldDuration) : 0f;

        // Exposed for SelectionBoxUI
        public bool IsDragging => isDragging;
        public Vector2 DragStart => dragStartScreen;
        public Vector2 DragEnd => currentMousePos;

        public IReadOnlyList<UnitView> SelectedUnits => selectedUnits;
        public UnitView HoveredUnit => hoveredUnit;

        private void Awake()
        {
            instance = this;
            inputActions = new RTSInputActions();
            mainCamera = UnityEngine.Camera.main;

#if UNITY_WEBGL && !UNITY_EDITOR
            RegisterKeyboardOverrides();
#endif

            remappableActions = new Dictionary<string, InputAction>
            {
                { "AttackMove", inputActions.RTS.AttackMove },
            };
        }

        private void OnDestroy()
        {
            if (resourceCursorIcon != null)
            {
                var canvas = resourceCursorIcon.transform.parent;
                if (canvas != null) Object.Destroy(canvas.gameObject);
            }
            if (attackCursorIcon != null)
            {
                var canvas = attackCursorIcon.transform.parent;
                if (canvas != null) Object.Destroy(canvas.gameObject);
            }
            if (garrisonCursorIcon != null)
            {
                var canvas = garrisonCursorIcon.transform.parent;
                if (canvas != null) Object.Destroy(canvas.gameObject);
            }
            if (patrolCursorIcon != null)
            {
                var canvas = patrolCursorIcon.transform.parent;
                if (canvas != null) Object.Destroy(canvas.gameObject);
            }
            if (actionCursorIcon != null)
            {
                var canvas = actionCursorIcon.transform.parent;
                if (canvas != null) Object.Destroy(canvas.gameObject);
            }
            if (healCursorIcon != null)
            {
                var canvas = healCursorIcon.transform.parent;
                if (canvas != null) Object.Destroy(canvas.gameObject);
            }
            if (instance == this) instance = null;
        }

        private void OnEnable()
        {
            inputActions.RTS.Enable();
            inputActions.RTS.Select.performed += OnSelectPerformed;
            inputActions.RTS.Select.canceled += OnSelectCanceled;
            inputActions.RTS.Command.performed += OnCommandStarted;
            inputActions.RTS.Command.canceled += OnCommandReleased;
            inputActions.RTS.MultiSelect.performed += ctx => multiSelectHeld = true;
            inputActions.RTS.MultiSelect.canceled += OnMultiSelectReleased;
            inputActions.RTS.DeselectAll.performed += OnEscapePressed;
            // currentMousePos is now read from VirtualCursor in Update
            inputActions.RTS.AttackMove.performed += OnAttackMovePerformed;

            for (int i = 0; i < 10; i++)
            {
                int groupIndex = i;
                inputActions.RTS.ControlGroups[i].performed += ctx => OnControlGroupKey(groupIndex);
            }

        }

        private void OnDisable()
        {
            inputActions.RTS.Disable();
        }

        public void RegisterUnitView(UnitView view)
        {
            unitViews[view.UnitId] = view;
        }

        public void UnregisterUnitView(int unitId)
        {
            unitViews.Remove(unitId);
        }

        public void RegisterBuildingView(BuildingView view)
        {
            buildingViews[view.BuildingId] = view;
        }

        public void UnregisterBuildingView(int buildingId)
        {
            buildingViews.Remove(buildingId);
        }

        private void OnSelectPerformed(InputAction.CallbackContext ctx)
        {
            if (UIInputSuppressed) return;

            // Wall placement: click-to-start, click-to-end (AoE4 style)
            if (isPlacingWall)
            {
                placementConsumedClick = true;

                if (!wallDragging)
                {
                    // First click: set start point
                    Ray wallRay = mainCamera.ScreenPointToRay(currentMousePos);
                    if (Physics.Raycast(wallRay, out RaycastHit wallHit, 1000f, groundLayer))
                    {
                        wallStartTileX = Mathf.FloorToInt(wallHit.point.x);
                        wallStartTileZ = Mathf.FloorToInt(wallHit.point.z);
                        wallDragging = true;
                    }
                }
                else
                {
                    // Second click: confirm end point and place wall
                    ConfirmWallPlacement();
                }
                return;
            }

            // Dummy placement: left-click to spawn target dummy
            if (SettingsMenuUI.IsPlacingDummy)
            {
                Ray dummyRay = mainCamera.ScreenPointToRay(currentMousePos);
                if (Physics.Raycast(dummyRay, out RaycastHit dummyHit, 1000f, groundLayer))
                {
                    var sim = GameBootstrapper.Instance?.Simulation;
                    if (sim != null)
                    {
                        FixedVector3 fixedPos = FixedVector3.FromVector3(dummyHit.point);
                        sim.CreateDummy(fixedPos);
                    }
                }
                placementConsumedClick = true;
                return;
            }

            // Meteor strike targeting: left-click to cast
            if (isMeteorTargeting)
            {
                Ray meteorRay = mainCamera.ScreenPointToRay(currentMousePos);
                if (Physics.Raycast(meteorRay, out RaycastHit meteorHit, 1000f, groundLayer))
                {
                    var sim = GameBootstrapper.Instance?.Simulation;
                    if (sim != null)
                    {
                        Vector2Int tile = sim.MapData.WorldToTile(FixedVector3.FromVector3(meteorHit.point));
                        if (sim.FogOfWar.GetVisibility(LocalPlayerId, tile.x, tile.y) == TileVisibility.Visible)
                        {
                            var cmd = new MeteorStrikeCommand(LocalPlayerId, tile.x, tile.y);
                            sim.CommandBuffer.EnqueueCommand(cmd);
                        }
                    }
                }
                isMeteorTargeting = false;
                DestroyMeteorTargetingPreview();
                placementConsumedClick = true;
                return;
            }

            // Healing Rain targeting: left-click to cast
            if (isHealingRainTargeting)
            {
                Ray hrRay = mainCamera.ScreenPointToRay(currentMousePos);
                if (Physics.Raycast(hrRay, out RaycastHit hrHit, 1000f, groundLayer))
                {
                    var sim = GameBootstrapper.Instance?.Simulation;
                    if (sim != null)
                    {
                        Vector2Int tile = sim.MapData.WorldToTile(FixedVector3.FromVector3(hrHit.point));
                        if (sim.FogOfWar.GetVisibility(LocalPlayerId, tile.x, tile.y) == TileVisibility.Visible)
                        {
                            var cmd = new HealingRainCommand(LocalPlayerId, tile.x, tile.y);
                            sim.CommandBuffer.EnqueueCommand(cmd);
                        }
                    }
                }
                isHealingRainTargeting = false;
                DestroyGodPowerPreview(ref healingRainTargetingPreview);
                placementConsumedClick = true;
                return;
            }

            // Lightning Storm targeting: left-click to cast
            if (isLightningStormTargeting)
            {
                Ray lsRay = mainCamera.ScreenPointToRay(currentMousePos);
                if (Physics.Raycast(lsRay, out RaycastHit lsHit, 1000f, groundLayer))
                {
                    var sim = GameBootstrapper.Instance?.Simulation;
                    if (sim != null)
                    {
                        Vector2Int tile = sim.MapData.WorldToTile(FixedVector3.FromVector3(lsHit.point));
                        if (sim.FogOfWar.GetVisibility(LocalPlayerId, tile.x, tile.y) == TileVisibility.Visible)
                        {
                            var cmd = new LightningStormCommand(LocalPlayerId, tile.x, tile.y);
                            sim.CommandBuffer.EnqueueCommand(cmd);
                        }
                    }
                }
                isLightningStormTargeting = false;
                DestroyGodPowerPreview(ref lightningStormTargetingPreview);
                placementConsumedClick = true;
                return;
            }

            // Tsunami targeting: click-drag to set origin + direction
            if (isTsunamiTargeting && !tsunamiDragging)
            {
                Ray tsRay = mainCamera.ScreenPointToRay(currentMousePos);
                if (Physics.Raycast(tsRay, out RaycastHit tsHit, 1000f, groundLayer))
                {
                    tsunamiOriginWorld = tsHit.point;
                    tsunamiDragging = true;
                }
                placementConsumedClick = true;
                return;
            }

            // Building placement: left-click to place at snapped position
            if (isPlacingBuilding)
            {
                var sim = GameBootstrapper.Instance?.Simulation;
                if (sim != null && snappedPositionValid && CanAffordBuilding(sim))
                {
                    var placeCmd = new PlaceBuildingCommand(
                        LocalPlayerId, placementBuildingType, snappedTileX, snappedTileZ, placementVillagerIds);
                    placeCmd.IsQueued = multiSelectHeld;
                    if (placementBuildingType == BuildingType.Landmark)
                        placeCmd.LandmarkIdValue = (int)placementLandmarkId;
                    sim.CommandBuffer.EnqueueCommand(placeCmd);

                    placementConsumedClick = true;

                    if (multiSelectHeld)
                        placementIsShiftQueued = true;
                    else
                        CancelBuildPlacement();
                }
                return;
            }

            selectHeld = true;
            isDragging = false;
            dragStartScreen = currentMousePos;

            if (attackMoveMode)
            {
                // Record world position for potential formation drag
                Ray ray = mainCamera.ScreenPointToRay(currentMousePos);
                if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
                    rightDragStartWorld = hit.point;
            }
        }

        private void Update()
        {
            currentMousePos = VirtualCursor.Position;

            // God power hotkeys removed — activated via clickable UI buttons (cheat menu)

            // Update meteor targeting preview position
            if (isMeteorTargeting && meteorTargetingPreview != null && mainCamera != null)
            {
                Ray ray = mainCamera.ScreenPointToRay(currentMousePos);
                if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
                {
                    meteorTargetingPreview.transform.position = hit.point + Vector3.up * 0.05f;
                }
            }

            // Update healing rain targeting preview position
            if (isHealingRainTargeting && healingRainTargetingPreview != null && mainCamera != null)
            {
                Ray ray = mainCamera.ScreenPointToRay(currentMousePos);
                if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
                {
                    healingRainTargetingPreview.transform.position = hit.point + Vector3.up * 0.05f;
                }
            }

            // Update lightning storm targeting preview position
            if (isLightningStormTargeting && lightningStormTargetingPreview != null && mainCamera != null)
            {
                Ray ray = mainCamera.ScreenPointToRay(currentMousePos);
                if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
                {
                    lightningStormTargetingPreview.transform.position = hit.point + Vector3.up * 0.05f;
                }
            }

            // Update tsunami targeting preview
            if (isTsunamiTargeting && mainCamera != null)
            {
                Ray ray = mainCamera.ScreenPointToRay(currentMousePos);
                if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
                {
                    if (!tsunamiDragging)
                    {
                        // Follow cursor before click
                        if (tsunamiTargetingPreview != null)
                            tsunamiTargetingPreview.transform.position = hit.point + Vector3.up * 0.05f;
                    }
                    else if (selectHeld)
                    {
                        // Dragging: update direction from origin to cursor
                        Vector3 dir = hit.point - tsunamiOriginWorld;
                        dir.y = 0f;
                        if (dir.sqrMagnitude > 0.1f)
                        {
                            var sim = GameBootstrapper.Instance?.Simulation;
                            float width = sim != null ? sim.Config.TsunamiWidth : 10f;
                            float length = sim != null ? sim.Config.TsunamiLength : 15f;
                            dir.Normalize();

                            if (tsunamiTargetingPreview != null)
                            {
                                Vector3 center = tsunamiOriginWorld + dir * (length / 2f);
                                center.y = GetTerrainHeightForPreview(center) + 0.05f;
                                tsunamiTargetingPreview.transform.position = center;
                                tsunamiTargetingPreview.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                            }
                            if (tsunamiArrowPreview != null)
                            {
                                if (!tsunamiArrowPreview.activeSelf)
                                    tsunamiArrowPreview.SetActive(true);
                                Vector3 arrowPos = tsunamiOriginWorld + dir * (length + 2f);
                                arrowPos.y = GetTerrainHeightForPreview(arrowPos) + 0.5f;
                                tsunamiArrowPreview.transform.position = arrowPos;
                                tsunamiArrowPreview.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                            }
                        }
                    }
                }
            }

            UpdateDeleteHold();

            if (selectHeld && !isDragging)
            {
                if (Vector2.Distance(dragStartScreen, currentMousePos) > dragThreshold)
                {
                    isDragging = true;
                }
            }

            if (isDragging)
                UpdateDragPreview();

            // Right-click drag: detect threshold and update formation preview
            if (commandHeld && selectedUnits.Count > 0)
            {
                if (!isRightDragging)
                {
                    float holdTime = Time.unscaledTime - rightClickDownTime;
                    if (holdTime > 0.18f && Vector2.Distance(rightDragStartScreen, currentMousePos) > dragThreshold)
                        isRightDragging = true;
                }

                if (isRightDragging && gameSetup != null)
                {
                    Ray ray = mainCamera.ScreenPointToRay(currentMousePos);
                    if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
                    {
                        var groupSizes = GetFormationGroupSizes();
                        var sim = GameBootstrapper.Instance?.Simulation;
                        Vector3 snappedHit = sim != null ? GameSetup.SnapClickToNearestWalkable(sim.MapData, hit.point) : hit.point;
                        var positions = GameSetup.ComputeGroupedLineFormation(
                            rightDragStartWorld, snappedHit, groupSizes);
                        if (sim != null) GameSetup.SnapToWalkable(sim.MapData, positions);
                        GameSetup.ScaleFormationByRadius(positions, groupSizes, GetFormationGroupRadii(), sim != null ? sim.Config.UnitRadius : 0.4f);
                        gameSetup.PreviewMarkers(positions);

                        // Show facing direction arrow
                        Vector3 dragDir = hit.point - rightDragStartWorld;
                        dragDir.y = 0f;
                        if (dragDir.sqrMagnitude > 0.001f)
                        {
                            dragDir.Normalize();
                            Vector3 facingDir = new Vector3(-dragDir.z, 0f, dragDir.x);
                            Vector3 center = (rightDragStartWorld + hit.point) * 0.5f;
                            gameSetup.ShowFacingArrow(center, facingDir);
                        }
                    }
                }
            }

            // Attack-move left-drag: show formation preview
            if (attackMoveMode && selectHeld && isDragging && selectedUnits.Count > 0 && gameSetup != null)
            {
                Ray ray = mainCamera.ScreenPointToRay(currentMousePos);
                if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
                {
                    var atkGroupSizes = GetFormationGroupSizes();
                    var atkSim = GameBootstrapper.Instance?.Simulation;
                    Vector3 snappedHit = atkSim != null ? GameSetup.SnapClickToNearestWalkable(atkSim.MapData, hit.point) : hit.point;
                    var positions = GameSetup.ComputeGroupedLineFormation(
                        rightDragStartWorld, snappedHit, atkGroupSizes);
                    if (atkSim != null) GameSetup.SnapToWalkable(atkSim.MapData, positions);
                    GameSetup.ScaleFormationByRadius(positions, atkGroupSizes, GetFormationGroupRadii(), atkSim != null ? atkSim.Config.UnitRadius : 0.4f);
                    gameSetup.PreviewMarkers(positions);

                    Vector3 dragDir = hit.point - rightDragStartWorld;
                    dragDir.y = 0f;
                    if (dragDir.sqrMagnitude > 0.001f)
                    {
                        dragDir.Normalize();
                        Vector3 facingDir = new Vector3(-dragDir.z, 0f, dragDir.x);
                        Vector3 center = (rightDragStartWorld + hit.point) * 0.5f;
                        gameSetup.ShowFacingArrow(center, facingDir);
                    }
                }
            }

            // Building placement ghost update
            if (isPlacingBuilding && ghostBuilding != null)
            {
                var sim = GameBootstrapper.Instance?.Simulation;
                if (sim != null)
                {
                    RefreshGridTexture(sim.MapData, sim.FogOfWar, LocalPlayerId, placementBuildingType);
                    Ray placementRay = mainCamera.ScreenPointToRay(currentMousePos);
                    if (Physics.Raycast(placementRay, out RaycastHit placementHit, 1000f, groundLayer))
                    {
                        GetBuildingFootprint(sim.Config, placementBuildingType, out int fw, out int fh);

                        snappedPositionValid = FindNearestValidPlacement(
                            sim.MapData, placementHit.point.x, placementHit.point.z,
                            fw, fh, placementBuildingType, 8,
                            out snappedTileX, out snappedTileZ);

                        float centerX = snappedTileX + fw * 0.5f;
                        float centerZ = snappedTileZ + fh * 0.5f;
                        float ghostY = sim.MapData.SampleHeight(centerX, centerZ) * sim.Config.TerrainHeightScale + 0.05f;
                        Vector3 ghostPosition = new Vector3(centerX, ghostY, centerZ);
                        ghostBuilding.transform.position = ghostPosition;

                        // Update attack range ring position if it exists
                        if (ghostAttackRangeRing != null)
                        {
                            ghostAttackRangeRing.transform.position = ghostPosition;
                        }

                        // Update influence zone position if it exists (follows ghost for Mill placement)
                        if (ghostInfluenceZone != null)
                        {
                            ghostInfluenceZone.transform.position = new Vector3(ghostPosition.x, ghostPosition.y, ghostPosition.z);
                        }

                        // Show buff marks on existing buildings within ghost influence zone
                        UpdateGhostInfluenceBuildingMarks(snappedTileX, snappedTileZ, sim);

                        bool valid = snappedPositionValid && CanAffordBuilding(sim);
                        bool inInfluence = false;
                        if (valid)
                        {
                            if (placementBuildingType == BuildingType.Farm)
                                inInfluence = IsFarmInAnyInfluenceZone(sim, snappedTileX, snappedTileZ, fw, fh);
                            else if (IsUnitProducerType(placementBuildingType)
                                && sim.GetPlayerCivilization(LocalPlayerId) == Civilization.French)
                                inInfluence = IsGhostInFrenchLandmarkInfluence(sim, snappedTileX, snappedTileZ, fw, fh);
                        }
                        if (valid != ghostIsValid || inInfluence != ghostInInfluenceZone)
                        {
                            ghostIsValid = valid;
                            ghostInInfluenceZone = inInfluence;
                            Material mat = !valid ? ghostInvalidMaterial
                                : inInfluence ? ghostInfluenceMaterial
                                : ghostValidMaterial;
                            ghostBuilding.GetComponent<Renderer>().sharedMaterial = mat;
                        }

                        // Ghost influence "+" icon
                        if (inInfluence)
                        {
                            if (ghostInfluenceIcon == null) CreateGhostInfluenceIcon();
                            ghostInfluenceIcon.SetActive(true);
                            var cam = UnitView.CachedMainCamera ?? mainCamera;
                            Vector3 sp = cam.WorldToScreenPoint(ghostPosition + Vector3.up * 2.5f);
                            if (sp.z > 0) ghostInfluenceIcon.GetComponent<RectTransform>().position = new Vector3(sp.x, sp.y, 0f);
                        }
                        else if (ghostInfluenceIcon != null)
                        {
                            ghostInfluenceIcon.SetActive(false);
                        }
                    }
                }
            }

            // Wall placement ghost update (drag preview)
            if (isPlacingWall && wallDragging)
            {
                var sim = GameBootstrapper.Instance?.Simulation;
                if (sim != null)
                {
                    Ray wallRay = mainCamera.ScreenPointToRay(currentMousePos);
                    if (Physics.Raycast(wallRay, out RaycastHit wallHit, 1000f, groundLayer))
                    {
                        int endTileX = Mathf.FloorToInt(wallHit.point.x);
                        int endTileZ = Mathf.FloorToInt(wallHit.point.z);

                        var tiles = WallLineHelper.ComputeWallLine(wallStartTileX, wallStartTileZ, endTileX, endTileZ);

                        // Show ghost quads for each tile
                        for (int i = 0; i < tiles.Count; i++)
                        {
                            var ghost = GetOrCreateWallGhost(i);
                            float wy = sim.MapData.SampleHeight(tiles[i].x + 0.5f, tiles[i].y + 0.5f) * sim.Config.TerrainHeightScale + 0.05f;
                            ghost.transform.position = new Vector3(tiles[i].x + 0.5f, wy, tiles[i].y + 0.5f);
                            bool valid = sim.MapData.IsBuildable(tiles[i].x, tiles[i].y);
                            ghost.GetComponent<Renderer>().sharedMaterial = valid ? ghostValidMaterial : ghostInvalidMaterial;
                            ghost.SetActive(true);
                        }

                        // Hide excess ghosts
                        for (int i = tiles.Count; i < wallGhostActiveCount; i++)
                            wallGhostPool[i].SetActive(false);
                        wallGhostActiveCount = tiles.Count;
                    }
                }
            }
            else if (isPlacingWall && !wallDragging)
            {
                // Show single ghost at cursor before drag starts
                var sim = GameBootstrapper.Instance?.Simulation;
                if (sim != null)
                {
                    Ray wallRay = mainCamera.ScreenPointToRay(currentMousePos);
                    if (Physics.Raycast(wallRay, out RaycastHit wallHit, 1000f, groundLayer))
                    {
                        int tileX = Mathf.FloorToInt(wallHit.point.x);
                        int tileZ = Mathf.FloorToInt(wallHit.point.z);
                        var ghost = GetOrCreateWallGhost(0);
                        float wy2 = sim.MapData.SampleHeight(tileX + 0.5f, tileZ + 0.5f) * sim.Config.TerrainHeightScale + 0.05f;
                        ghost.transform.position = new Vector3(tileX + 0.5f, wy2, tileZ + 0.5f);
                        bool valid = sim.MapData.IsBuildable(tileX, tileZ);
                        ghost.GetComponent<Renderer>().sharedMaterial = valid ? ghostValidMaterial : ghostInvalidMaterial;
                        ghost.SetActive(true);
                        for (int i = 1; i < wallGhostActiveCount; i++)
                            wallGhostPool[i].SetActive(false);
                        wallGhostActiveCount = 1;
                    }
                }
            }

            // Dummy placement ghost
            if (SettingsMenuUI.IsPlacingDummy)
            {
                if (dummyGhost == null)
                {
                    dummyGhost = new GameObject("DummyGhost");

                    // Body capsule
                    var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    body.name = "Body";
                    body.transform.SetParent(dummyGhost.transform);
                    body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                    body.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                    Object.Destroy(body.GetComponent<Collider>());

                    // Transparent green material
                    if (ghostValidMaterial == null)
                    {
                        ghostValidMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                        ghostValidMaterial.color = new Color(0f, 1f, 0f, 0.35f);
                        ghostValidMaterial.SetFloat("_Surface", 1);
                        ghostValidMaterial.SetOverrideTag("RenderType", "Transparent");
                        ghostValidMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        ghostValidMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        ghostValidMaterial.SetInt("_ZWrite", 0);
                        ghostValidMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                        ghostValidMaterial.EnableKeyword("_ALPHABLEND_ON");
                        ghostValidMaterial.renderQueue = 3000;
                    }
                    body.GetComponent<Renderer>().sharedMaterial = ghostValidMaterial;
                }

                Ray dummyRay = mainCamera.ScreenPointToRay(currentMousePos);
                if (Physics.Raycast(dummyRay, out RaycastHit dummyHit, 1000f, groundLayer))
                {
                    var sim = GameBootstrapper.Instance?.Simulation;
                    float ghostY = dummyHit.point.y;
                    if (sim != null)
                        ghostY = sim.MapData.SampleHeight(dummyHit.point.x, dummyHit.point.z) * sim.Config.TerrainHeightScale;
                    dummyGhost.transform.position = new Vector3(dummyHit.point.x, ghostY, dummyHit.point.z);
                }
                dummyGhost.SetActive(true);
            }
            else if (dummyGhost != null)
            {
                Object.Destroy(dummyGhost);
                dummyGhost = null;
            }

            // Hover detection
            UpdateHover();
            UpdateResourceHover();
            UpdateAttackHover();
            UpdateGarrisonCursor();
            UpdatePatrolCursor();
            UpdateActionCursor();
            UpdateHealCursor();

            // Signal the global custom cursor to hide when a contextual cursor is active
            bool anyContextual = (attackCursorIcon != null && attackCursorIcon.activeSelf)
                              || (garrisonCursorIcon != null && garrisonCursorIcon.activeSelf)
                              || (patrolCursorIcon != null && patrolCursorIcon.activeSelf)
                              || (actionCursorIcon != null && actionCursorIcon.activeSelf)
                              || (healCursorIcon != null && healCursorIcon.activeSelf);
            CustomCursor.SetContextualCursorActive(anyContextual);
        }

        private void UpdateHover()
        {
            UnitView newHover = null;

            if (!UIInputSuppressed && mainCamera != null)
            {
                Ray ray = mainCamera.ScreenPointToRay(currentMousePos);
                if (Physics.Raycast(ray, out RaycastHit hit, 1000f, unitLayer))
                {
                    var view = hit.collider.GetComponent<UnitView>();
                    if (view != null && !view.IsDead)
                        newHover = view;
                }
            }

            if (newHover != hoveredUnit)
            {
                if (hoveredUnit != null)
                    hoveredUnit.SetHovered(false);
                hoveredUnit = newHover;
                if (hoveredUnit != null)
                    hoveredUnit.SetHovered(true);
            }
        }

        private void UpdateResourceHover()
        {
            ResourceNode newHover = null;

            if (!UIInputSuppressed && !isPlacingBuilding && !isPlacingWall && mainCamera != null
                && HasSelectedVillager())
            {
                Ray ray = mainCamera.ScreenPointToRay(currentMousePos);
                if (Physics.Raycast(ray, out RaycastHit hit, 1000f, resourceLayer))
                {
                    var node = hit.collider.GetComponent<ResourceNode>();
                    if (node != null)
                    {
                        var data = node.GetNodeData();
                        if (data != null && !data.IsDepleted)
                            newHover = node;
                    }
                }
            }

            if (newHover != hoveredResource)
            {
                hoveredResource = newHover;

                if (hoveredResource != null)
                {
                    EnsureResourceCursorIcon();
                    var data = hoveredResource.GetNodeData();
                    resourceCursorImage.sprite = ResourceIcons.Get(data.Type);
                    resourceCursorIcon.SetActive(true);
                }
                else if (resourceCursorIcon != null)
                {
                    resourceCursorIcon.SetActive(false);
                }
            }

            if (hoveredResource != null && resourceCursorIcon != null)
            {
                resourceCursorIcon.transform.position = new Vector3(currentMousePos.x + 20f, currentMousePos.y - 20f, 0f);
            }
        }

        private bool HasSelectedVillager()
        {
            for (int i = 0; i < selectedUnits.Count; i++)
            {
                if (selectedUnits[i].UnitType == 0 && selectedUnits[i].PlayerId == LocalPlayerId)
                    return true;
            }
            return false;
        }

        private bool HasSelectedOwnUnit()
        {
            for (int i = 0; i < selectedUnits.Count; i++)
            {
                if (selectedUnits[i].PlayerId == LocalPlayerId)
                    return true;
            }
            return false;
        }

        private void UpdateDeleteHold()
        {
            if (UIInputSuppressed || isPlacingBuilding || isPlacingWall)
            {
                ResetDeleteHold();
                return;
            }

            bool hasSelection = selectedUnits.Count > 0 || selectedBuildings.Count > 0;
            bool xHeld = inputActions.RTS.DeleteEntity.IsPressed();

            // Instant delete for buildings under construction (single press)
            if (inputActions.RTS.DeleteEntity.WasPressedThisFrame() && hasSelection && !deleteHolding)
            {
                if (selectedBuildings.Count > 0 && selectedBuildings[0].PlayerId == LocalPlayerId)
                {
                    var sim = GameBootstrapper.Instance?.Simulation;
                    var bData = sim?.BuildingRegistry.GetBuilding(selectedBuildings[0].BuildingId);
                    if (bData != null && bData.IsUnderConstruction)
                    {
                        sim.CommandBuffer.EnqueueCommand(
                            new DeleteBuildingCommand(LocalPlayerId, selectedBuildings[0].BuildingId));
                        return;
                    }
                }
            }

            if (xHeld && hasSelection && !deleteHolding)
            {
                deleteHolding = true;
                deleteHoldTimer = 0f;
            }

            if (deleteHolding)
            {
                if (!xHeld || !hasSelection)
                {
                    ResetDeleteHold();
                }
                else
                {
                    deleteHoldTimer += Time.deltaTime;

                    if (deleteHoldTimer >= DeleteHoldDuration)
                    {
                        var sim = GameBootstrapper.Instance?.Simulation;
                        if (sim != null)
                        {
                            if (selectedUnits.Count > 0)
                            {
                                var ownIds = new List<int>();
                                for (int i = 0; i < selectedUnits.Count; i++)
                                {
                                    if (selectedUnits[i].PlayerId == LocalPlayerId)
                                        ownIds.Add(selectedUnits[i].UnitId);
                                }
                                if (ownIds.Count > 0)
                                    sim.CommandBuffer.EnqueueCommand(new DeleteUnitsCommand(LocalPlayerId, ownIds.ToArray()));
                            }
                            else if (selectedBuildings.Count > 0 && selectedBuildings[0].PlayerId == LocalPlayerId)
                            {
                                sim.CommandBuffer.EnqueueCommand(new DeleteBuildingCommand(LocalPlayerId, selectedBuildings[0].BuildingId));
                            }
                        }
                        ResetDeleteHold();
                    }
                }
            }
        }

        private void ResetDeleteHold()
        {
            deleteHolding = false;
            deleteHoldTimer = 0f;
        }

        private void UpdateAttackHover()
        {
            bool showAttack = false;

            if (!UIInputSuppressed && !isPlacingBuilding && !isPlacingWall && mainCamera != null
                && HasSelectedOwnUnit())
            {
                if (attackMoveMode)
                {
                    showAttack = true;
                }
                else if (hoveredUnit != null && !hoveredUnit.IsDead)
                {
                    var teamSim = GameBootstrapper.Instance?.Simulation;
                    if (teamSim != null)
                    {
                        if (!teamSim.AreAllies(hoveredUnit.PlayerId, LocalPlayerId))
                            showAttack = true;
                        else
                        {
                            // Show sword when hovering own sheep with a villager selected
                            var hoveredUnitData = teamSim.UnitRegistry.GetUnit(hoveredUnit.UnitId);
                            if (hoveredUnitData != null && hoveredUnitData.IsSheep && HasSelectedVillager())
                                showAttack = true;
                        }
                    }
                }

                if (!showAttack && hoveredUnit == null)
                {
                    Ray ray = mainCamera.ScreenPointToRay(currentMousePos);
                    if (Physics.Raycast(ray, out RaycastHit buildingHit, 1000f, buildingLayer))
                    {
                        var bv = buildingHit.collider.GetComponent<BuildingView>();
                        if (bv != null && !bv.IsDestroyed)
                        {
                            var teamSim = GameBootstrapper.Instance?.Simulation;
                            if (teamSim != null && !teamSim.AreAllies(bv.PlayerId, LocalPlayerId))
                                showAttack = true;
                        }
                    }
                }
            }

            if (showAttack)
            {
                EnsureAttackCursorIcon();
                attackCursorIcon.SetActive(true);
                attackCursorIcon.transform.position = new Vector3(currentMousePos.x + 20f, currentMousePos.y - 20f, 0f);
            }
            else if (attackCursorIcon != null)
            {
                attackCursorIcon.SetActive(false);
            }
        }

        private void UpdateGarrisonCursor()
        {
            if (garrisonMode)
            {
                EnsureGarrisonCursorIcon();
                garrisonCursorIcon.SetActive(true);
                garrisonCursorIcon.transform.position = new Vector3(
                    currentMousePos.x + 20f, currentMousePos.y - 20f, 0f);
            }
            else if (garrisonCursorIcon != null)
            {
                garrisonCursorIcon.SetActive(false);
            }
        }

        private void EnsureGarrisonCursorIcon()
        {
            if (garrisonCursorIcon != null) return;

            if (garrisonIconSprite == null)
            {
                garrisonIconSprite = Resources.Load<Sprite>("ResourceIcons/garrisonicon");
                if (garrisonIconSprite == null)
                {
                    var tex = Resources.Load<Texture2D>("ResourceIcons/garrisonicon");
                    if (tex != null)
                        garrisonIconSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }

            var canvasGO = new GameObject("GarrisonCursorCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGO.AddComponent<CanvasScaler>();

            garrisonCursorIcon = new GameObject("GarrisonCursorIcon");
            garrisonCursorIcon.transform.SetParent(canvasGO.transform, false);

            garrisonCursorImage = garrisonCursorIcon.AddComponent<Image>();
            garrisonCursorImage.raycastTarget = false;
            garrisonCursorImage.sprite = garrisonIconSprite;

            var rt = garrisonCursorIcon.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(40f, 40f);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 1f);

            garrisonCursorIcon.SetActive(false);
        }

        private void UpdatePatrolCursor()
        {
            if (patrolMode)
            {
                EnsurePatrolCursorIcon();
                patrolCursorIcon.SetActive(true);
                patrolCursorIcon.transform.position = new Vector3(
                    currentMousePos.x + 20f, currentMousePos.y - 20f, 0f);
            }
            else if (patrolCursorIcon != null)
            {
                patrolCursorIcon.SetActive(false);
            }
        }

        private void EnsurePatrolCursorIcon()
        {
            if (patrolCursorIcon != null) return;

            if (patrolIconSprite == null)
            {
                patrolIconSprite = Resources.Load<Sprite>("ResourceIcons/patrolicon");
                if (patrolIconSprite == null)
                {
                    var tex = Resources.Load<Texture2D>("ResourceIcons/patrolicon");
                    if (tex != null)
                        patrolIconSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }

            var canvasGO = new GameObject("PatrolCursorCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGO.AddComponent<CanvasScaler>();

            patrolCursorIcon = new GameObject("PatrolCursorIcon");
            patrolCursorIcon.transform.SetParent(canvasGO.transform, false);

            patrolCursorImage = patrolCursorIcon.AddComponent<Image>();
            patrolCursorImage.raycastTarget = false;
            patrolCursorImage.sprite = patrolIconSprite;

            var rt = patrolCursorIcon.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(40f, 40f);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 1f);

            patrolCursorIcon.SetActive(false);
        }

        private void EnsureAttackCursorIcon()
        {
            if (attackCursorIcon != null) return;

            if (attackIconSprite == null)
            {
                attackIconSprite = Resources.Load<Sprite>("ResourceIcons/attackicon");
                if (attackIconSprite == null)
                {
                    var tex = Resources.Load<Texture2D>("ResourceIcons/attackicon");
                    if (tex != null)
                        attackIconSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }

            var canvasGO = new GameObject("AttackCursorCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGO.AddComponent<CanvasScaler>();

            attackCursorIcon = new GameObject("AttackCursorIcon");
            attackCursorIcon.transform.SetParent(canvasGO.transform, false);

            attackCursorImage = attackCursorIcon.AddComponent<Image>();
            attackCursorImage.raycastTarget = false;
            attackCursorImage.sprite = attackIconSprite;

            var rt = attackCursorIcon.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(40f, 40f);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 1f);

            attackCursorIcon.SetActive(false);
        }

        private void EnsureResourceCursorIcon()
        {
            if (resourceCursorIcon != null) return;

            ResourceIcons.EnsureLoaded();

            var canvasGO = new GameObject("ResourceCursorCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGO.AddComponent<CanvasScaler>();

            resourceCursorIcon = new GameObject("ResourceCursorIcon");
            resourceCursorIcon.transform.SetParent(canvasGO.transform, false);

            resourceCursorImage = resourceCursorIcon.AddComponent<Image>();
            resourceCursorImage.raycastTarget = false;

            var rt = resourceCursorIcon.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(40f, 40f);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 1f);

            resourceCursorIcon.SetActive(false);
        }

        private void EnsureActionCursorIcon()
        {
            if (actionCursorIcon != null) return;

            CommandIcons.EnsureLoaded();

            var canvasGO = new GameObject("ActionCursorCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 101;
            canvasGO.AddComponent<CanvasScaler>();

            actionCursorIcon = new GameObject("ActionCursorIcon");
            actionCursorIcon.transform.SetParent(canvasGO.transform, false);

            actionCursorImage = actionCursorIcon.AddComponent<Image>();
            actionCursorImage.raycastTarget = false;

            var rt = actionCursorIcon.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(60f, 60f);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            actionCursorIcon.SetActive(false);
        }

        private void UpdateActionCursor()
        {
            Sprite actionSprite = null;

            if (!UIInputSuppressed && !isPlacingBuilding && !isPlacingWall && mainCamera != null
                && HasSelectedVillager())
            {
                // Check resource hover: wood → chop, gold/stone → mine
                if (hoveredResource != null)
                {
                    var data = hoveredResource.GetNodeData();
                    if (data != null && !data.IsDepleted)
                    {
                        CommandIcons.EnsureLoaded();
                        if (data.Type == ResourceType.Wood)
                            actionSprite = CommandIcons.Chop;
                        else if (data.Type == ResourceType.Gold || data.Type == ResourceType.Stone)
                            actionSprite = CommandIcons.Mine;
                    }
                }

                // Check building hover: under construction → build
                if (actionSprite == null)
                {
                    Ray ray = mainCamera.ScreenPointToRay(currentMousePos);
                    if (Physics.Raycast(ray, out RaycastHit hit, 1000f, buildingLayer))
                    {
                        var buildingView = hit.collider.GetComponent<BuildingView>();
                        if (buildingView != null)
                        {
                            var sim = GameBootstrapper.Instance?.Simulation;
                            if (sim != null)
                            {
                                var buildingData = sim.BuildingRegistry.GetBuilding(buildingView.BuildingId);
                                if (buildingData != null && buildingData.IsUnderConstruction
                                    && sim.AreAllies(buildingData.PlayerId, LocalPlayerId))
                                {
                                    CommandIcons.EnsureLoaded();
                                    actionSprite = CommandIcons.Build;
                                }
                            }
                        }
                    }
                }
            }

            if (actionSprite != null)
            {
                EnsureActionCursorIcon();
                actionCursorImage.sprite = actionSprite;
                actionCursorIcon.SetActive(true);
                actionCursorIcon.transform.position = new Vector3(currentMousePos.x + 12f, currentMousePos.y - 12f, 0f);
            }
            else if (actionCursorIcon != null)
            {
                actionCursorIcon.SetActive(false);
            }
        }

        private bool HasSelectedOwnMonk()
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return false;
            for (int i = 0; i < selectedUnits.Count; i++)
            {
                if (selectedUnits[i].PlayerId == LocalPlayerId && selectedUnits[i].UnitType == 9)
                    return true;
            }
            return false;
        }

        private void EnsureHealCursorIcon()
        {
            if (healCursorIcon != null) return;

            var canvasGO = new GameObject("HealCursorCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 101;
            canvasGO.AddComponent<CanvasScaler>();

            healCursorIcon = new GameObject("HealCursorIcon");
            healCursorIcon.transform.SetParent(canvasGO.transform, false);

            healCursorImage = healCursorIcon.AddComponent<Image>();
            healCursorImage.raycastTarget = false;

            // Create a green + texture procedurally
            var tex = new Texture2D(32, 32);
            var pixels = new Color[32 * 32];
            var green = new Color(0.2f, 0.9f, 0.2f, 1f);
            var transparent = new Color(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = transparent;
            // Horizontal bar of the +
            for (int x = 8; x < 24; x++)
                for (int y = 13; y < 19; y++)
                    pixels[y * 32 + x] = green;
            // Vertical bar of the +
            for (int x = 13; x < 19; x++)
                for (int y = 8; y < 24; y++)
                    pixels[y * 32 + x] = green;
            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            healCursorImage.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));

            var rt = healCursorIcon.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(40f, 40f);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 1f);

            healCursorIcon.SetActive(false);
        }

        private void UpdateHealCursor()
        {
            bool showHeal = false;

            if (!UIInputSuppressed && !isPlacingBuilding && !isPlacingWall && mainCamera != null
                && HasSelectedOwnMonk())
            {
                if (hoveredUnit != null && !hoveredUnit.IsDead && hoveredUnit.PlayerId == LocalPlayerId)
                {
                    var sim = GameBootstrapper.Instance?.Simulation;
                    if (sim != null)
                    {
                        var ud = sim.UnitRegistry.GetUnit(hoveredUnit.UnitId);
                        if (ud != null && ud.CurrentHealth < ud.MaxHealth)
                            showHeal = true;
                    }
                }
            }

            if (showHeal)
            {
                EnsureHealCursorIcon();
                healCursorIcon.SetActive(true);
                healCursorIcon.transform.position = new Vector3(currentMousePos.x + 20f, currentMousePos.y - 20f, 0f);
            }
            else if (healCursorIcon != null)
            {
                healCursorIcon.SetActive(false);
            }
        }

        private void OnSelectCanceled(InputAction.CallbackContext ctx)
        {
            bool wasHeld = selectHeld;
            selectHeld = false;

            if (placementConsumedClick)
            {
                placementConsumedClick = false;
                return;
            }

            // Press was suppressed by UI (minimap, info panel, settings), skip selection logic
            if (!wasHeld) return;

            if (attackMoveMode)
            {
                IssueAttackMoveCommand();
                attackMoveMode = false;
                isDragging = false;
                return;
            }

            if (garrisonMode)
            {
                TryIssueGarrisonAtClick();
                garrisonMode = false;
                isDragging = false;
                return;
            }

            if (patrolMode)
            {
                IssuePatrolCommand();
                patrolMode = false;
                isDragging = false;
                return;
            }

            // Tsunami drag release: compute direction and cast
            if (isTsunamiTargeting && tsunamiDragging)
            {
                Ray tsRay = mainCamera.ScreenPointToRay(currentMousePos);
                if (Physics.Raycast(tsRay, out RaycastHit tsHit, 1000f, groundLayer))
                {
                    Vector3 dir = tsHit.point - tsunamiOriginWorld;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.5f)
                    {
                        dir.Normalize();
                        var sim = GameBootstrapper.Instance?.Simulation;
                        if (sim != null)
                        {
                            Vector2Int tile = sim.MapData.WorldToTile(FixedVector3.FromVector3(tsunamiOriginWorld));
                            if (sim.FogOfWar.GetVisibility(LocalPlayerId, tile.x, tile.y) == TileVisibility.Visible)
                            {
                                var fixedDir = FixedVector3.FromVector3(dir);
                                var cmd = new TsunamiCommand(LocalPlayerId, tile.x, tile.y, fixedDir.x.Raw, fixedDir.z.Raw);
                                sim.CommandBuffer.EnqueueCommand(cmd);
                            }
                        }
                    }
                }
                isTsunamiTargeting = false;
                tsunamiDragging = false;
                DestroyTsunamiTargetingPreview();
                return;
            }

            ClearDragPreview();

            if (isDragging)
            {
                BoxSelect();
                isDragging = false;
            }
            else
            {
                ClickSelect();
            }
        }

        private void ClickSelect()
        {
            // Ignore clicks on the info panel
            if (UnitInfoUI.ContainsScreenPoint(currentMousePos))
                return;

            Ray ray = mainCamera.ScreenPointToRay(currentMousePos);

            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, unitLayer))
            {
                var unitView = hit.collider.GetComponent<UnitView>();
                if (unitView != null && !unitView.IsDead)
                {
                    DeselectBuilding();
                    DeselectResourceNode();
                    lastClickedBuildingId = -1;
                    bool isOwned = unitView.PlayerId == LocalPlayerId;

                    if (isOwned)
                    {
                        // Double-click detection (own units only)
                        float now = Time.unscaledTime;
                        if (unitView.UnitId == lastClickedUnitId && (now - lastClickTime) < DoubleClickTime)
                        {
                            DoubleClickSelect(unitView.UnitType);
                            lastClickedUnitId = -1;
                            return;
                        }
                        lastClickTime = now;
                        lastClickedUnitId = unitView.UnitId;

                        if (!multiSelectHeld)
                            DeselectAll();

                        if (unitView.IsSelected)
                        {
                            unitView.SetSelected(false);
                            selectedUnits.Remove(unitView);
                        }
                        else
                        {
                            unitView.SetSelected(true);
                            selectedUnits.Add(unitView);
                            SFXManager.Instance?.PlayUI(SFXType.UnitSelect, 0.5f);
                        }
                    }
                    else
                    {
                        // Enemy unit: view info only (mirrors enemy building pattern)
                        lastClickedUnitId = -1;
                        DeselectAll();
                        DeselectBuilding();
                        unitView.SetSelected(true);
                        selectedUnits.Add(unitView);
                    }
                    return;
                }
            }

            // Check for building click
            if (Physics.Raycast(ray, out RaycastHit buildingHit, 1000f, buildingLayer))
            {
                var buildingView = buildingHit.collider.GetComponent<BuildingView>();
                if (buildingView != null && !buildingView.IsDestroyed)
                {
                    lastClickedUnitId = -1;
                    DeselectResourceNode();
                    bool isOwned = buildingView.PlayerId == LocalPlayerId;

                    if (isOwned)
                    {
                        // Double-click detection (own buildings only)
                        float now = Time.unscaledTime;
                        if (buildingView.BuildingId == lastClickedBuildingId && (now - lastClickTime) < DoubleClickTime)
                        {
                            DoubleClickSelectBuilding(buildingView.BuildingType);
                            lastClickedBuildingId = -1;
                            return;
                        }
                        lastClickTime = now;
                        lastClickedBuildingId = buildingView.BuildingId;

                        if (!multiSelectHeld)
                        {
                            DeselectAll();
                            DeselectBuilding();
                        }

                        if (buildingView.IsSelected)
                        {
                            buildingView.SetSelected(false);
                            selectedBuildings.Remove(buildingView);
                        }
                        else
                        {
                            buildingView.SetSelected(true);
                            selectedBuildings.Add(buildingView);
                            SFXManager.Instance?.PlayUI(SFXType.BuildingSelect, 0.5f);
                        }
                    }
                    else
                    {
                        // Enemy building: single-select only, deselect all friendlies
                        lastClickedBuildingId = -1;
                        DeselectAll();
                        DeselectBuilding();
                        buildingView.SetSelected(true);
                        selectedBuildings.Add(buildingView);
                        SFXManager.Instance?.PlayUI(SFXType.BuildingSelect, 0.5f);
                    }
                    return;
                }
            }

            // Check for resource node click
            if (Physics.Raycast(ray, out RaycastHit resourceClickHit, 1000f, resourceLayer))
            {
                var resourceNode = resourceClickHit.collider.GetComponent<ResourceNode>();
                if (resourceNode != null)
                {
                    lastClickedUnitId = -1;
                    lastClickedBuildingId = -1;
                    DeselectAll();
                    DeselectBuilding();
                    DeselectResourceNode();
                    resourceNode.SetSelected(true);
                    selectedResourceNode = resourceNode;
                    return;
                }
            }

            // Clicked empty ground
            lastClickedUnitId = -1;
            lastClickedBuildingId = -1;
            DeselectBuilding();
            DeselectResourceNode();
            if (!multiSelectHeld)
                DeselectAll();
        }

        private void DoubleClickSelect(int unitType)
        {
            DeselectAll();
            DeselectResourceNode();

            foreach (var kvp in unitViews)
            {
                var view = kvp.Value;
                if (view.UnitType != unitType) continue;
                if (view.PlayerId != LocalPlayerId) continue;
                if (view.IsDead) continue;

                Vector3 screenPos = mainCamera.WorldToScreenPoint(view.transform.position);
                if (screenPos.z > 0 &&
                    screenPos.x >= 0 && screenPos.x <= Screen.width &&
                    screenPos.y >= 0 && screenPos.y <= Screen.height)
                {
                    view.SetSelected(true);
                    selectedUnits.Add(view);
                }
            }
        }

        private void DoubleClickSelectBuilding(BuildingType type)
        {
            DeselectAll();
            DeselectBuilding();
            DeselectResourceNode();

            foreach (var kvp in buildingViews)
            {
                var view = kvp.Value;
                if (view.BuildingType != type) continue;
                if (view.PlayerId != LocalPlayerId) continue;
                if (view.IsDestroyed) continue;

                Vector3 screenPos = mainCamera.WorldToScreenPoint(view.transform.position);
                if (screenPos.z > 0 &&
                    screenPos.x >= 0 && screenPos.x <= Screen.width &&
                    screenPos.y >= 0 && screenPos.y <= Screen.height)
                {
                    view.SetSelected(true);
                    selectedBuildings.Add(view);
                }
            }
        }

        private void BoxSelect()
        {
            if (!multiSelectHeld)
            {
                DeselectAll();
                DeselectBuilding();
                DeselectResourceNode();
            }

            Vector2 min = Vector2.Min(dragStartScreen, currentMousePos);
            Vector2 max = Vector2.Max(dragStartScreen, currentMousePos);
            Rect selectionRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);

            // First pass — select military units only (skip villagers and sheep)
            foreach (var kvp in unitViews)
            {
                var view = kvp.Value;

                if (view.PlayerId != LocalPlayerId) continue;
                if (view.IsDead) continue;
                if (view.UnitType == 0 || view.UnitType == 5) continue; // skip villagers and sheep

                Rect unitRect = view.GetScreenBounds(mainCamera);
                if (unitRect.width > 0 && selectionRect.Overlaps(unitRect))
                {
                    if (!view.IsSelected)
                    {
                        view.SetSelected(true);
                        selectedUnits.Add(view);
                    }
                }
            }

            // Second pass — select villagers only if no military units were selected
            if (selectedUnits.Count == 0)
            {
                foreach (var kvp in unitViews)
                {
                    var view = kvp.Value;

                    if (view.PlayerId != LocalPlayerId) continue;
                    if (view.IsDead) continue;
                    if (view.UnitType != 0) continue; // only villagers

                    Rect unitRect = view.GetScreenBounds(mainCamera);
                    if (unitRect.width > 0 && selectionRect.Overlaps(unitRect))
                    {
                        if (!view.IsSelected)
                        {
                            view.SetSelected(true);
                            selectedUnits.Add(view);
                        }
                    }
                }
            }

            // Third pass — select sheep only if no military or villagers were selected
            if (selectedUnits.Count == 0)
            {
                foreach (var kvp in unitViews)
                {
                    var view = kvp.Value;

                    if (view.PlayerId != LocalPlayerId) continue;
                    if (view.IsDead) continue;
                    if (view.UnitType != 5) continue; // only sheep

                    Rect unitRect = view.GetScreenBounds(mainCamera);
                    if (unitRect.width > 0 && selectionRect.Overlaps(unitRect))
                    {
                        if (!view.IsSelected)
                        {
                            view.SetSelected(true);
                            selectedUnits.Add(view);
                        }
                    }
                }
            }

            // Only select buildings if no units were selected in the box
            if (selectedUnits.Count == 0)
            {
                foreach (var kvp in buildingViews)
                {
                    var view = kvp.Value;

                    if (view.PlayerId != LocalPlayerId) continue;
                    if (view.IsDestroyed) continue;

                    Rect buildingRect = view.GetScreenBounds(mainCamera);
                    if (buildingRect.width > 0 && selectionRect.Overlaps(buildingRect))
                    {
                        if (!view.IsSelected)
                        {
                            view.SetSelected(true);
                            selectedBuildings.Add(view);
                        }
                    }
                }

                if (selectedBuildings.Count > 0)
                    SFXManager.Instance?.PlayUI(SFXType.BuildingSelect, 0.5f);
            }
            else
            {
                SFXManager.Instance?.PlayUI(SFXType.UnitSelect, 0.5f);
            }
        }

        private void UpdateDragPreview()
        {
            Vector2 min = Vector2.Min(dragStartScreen, currentMousePos);
            Vector2 max = Vector2.Max(dragStartScreen, currentMousePos);
            Rect selectionRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);

            // Check what's in the box
            bool hasMilitary = false;
            bool hasVillager = false;
            bool hasSheep = false;

            foreach (var kvp in unitViews)
            {
                var view = kvp.Value;
                if (view.PlayerId != LocalPlayerId || view.IsDead) continue;
                Rect unitRect = view.GetScreenBounds(mainCamera);
                if (unitRect.width > 0 && selectionRect.Overlaps(unitRect))
                {
                    if (view.UnitType == 5) hasSheep = true;
                    else if (view.UnitType != 0) hasMilitary = true;
                    else hasVillager = true;
                }
            }

            // Apply preselection with priority: military > villagers > sheep
            foreach (var kvp in unitViews)
            {
                var view = kvp.Value;
                if (view.PlayerId != LocalPlayerId || view.IsDead || view.IsSelected)
                {
                    view.SetPreselected(false);
                    continue;
                }

                Rect unitRect = view.GetScreenBounds(mainCamera);
                bool inBox = unitRect.width > 0 && selectionRect.Overlaps(unitRect);

                bool shouldHighlight = false;
                if (inBox)
                {
                    if (hasMilitary)
                        shouldHighlight = view.UnitType != 0 && view.UnitType != 5;
                    else if (hasVillager)
                        shouldHighlight = view.UnitType == 0;
                    else
                        shouldHighlight = view.UnitType == 5;
                }

                view.SetPreselected(shouldHighlight);
            }

            // Buildings: only if no units at all in box
            bool hasAnyUnit = hasMilitary || hasVillager || hasSheep;
            foreach (var kvp in buildingViews)
            {
                var view = kvp.Value;
                if (view.PlayerId != LocalPlayerId || view.IsDestroyed || view.IsSelected)
                {
                    view.SetPreselected(false);
                    continue;
                }

                Rect buildingRect = view.GetScreenBounds(mainCamera);
                bool inBox = buildingRect.width > 0 && selectionRect.Overlaps(buildingRect);

                view.SetPreselected(inBox && !hasAnyUnit);
            }
        }

        private void ClearDragPreview()
        {
            foreach (var kvp in unitViews)
                kvp.Value.SetPreselected(false);
            foreach (var kvp in buildingViews)
                kvp.Value.SetPreselected(false);
        }

        private void OnCommandStarted(InputAction.CallbackContext ctx)
        {
            if (UIInputSuppressed) return;
            if (isMeteorTargeting)
            {
                isMeteorTargeting = false;
                DestroyMeteorTargetingPreview();
                return;
            }
            if (isHealingRainTargeting)
            {
                isHealingRainTargeting = false;
                DestroyGodPowerPreview(ref healingRainTargetingPreview);
                return;
            }
            if (isLightningStormTargeting)
            {
                isLightningStormTargeting = false;
                DestroyGodPowerPreview(ref lightningStormTargetingPreview);
                return;
            }
            if (isTsunamiTargeting)
            {
                isTsunamiTargeting = false;
                tsunamiDragging = false;
                DestroyTsunamiTargetingPreview();
                return;
            }
            if (SettingsMenuUI.IsPlacingDummy)
            {
                SettingsMenuUI.IsPlacingDummy = false;
                return;
            }
            if (isPlacingWall)
            {
                CancelWallPlacement();
                return;
            }
            if (isPlacingBuilding)
            {
                CancelBuildPlacement();
                return;
            }
            if (attackMoveMode)
            {
                attackMoveMode = false;
                return;
            }
            if (garrisonMode)
            {
                garrisonMode = false;
                return;
            }
            if (patrolMode)
            {
                patrolMode = false;
                return;
            }

            commandHeld = true;
            isRightDragging = false;
            rightDragStartScreen = currentMousePos;
            rightClickDownTime = Time.unscaledTime;

            // Record the world position where right-click started
            Ray ray = mainCamera.ScreenPointToRay(currentMousePos);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
                rightDragStartWorld = hit.point;
        }

        private void OnCommandReleased(InputAction.CallbackContext ctx)
        {
            if (UIInputSuppressed) return;
            if (!commandHeld) return;
            commandHeld = false;

            // Rally point: right-click with own buildings selected
            if (selectedBuildings.Count > 0 && selectedBuildings[0].PlayerId == LocalPlayerId &&
                selectedUnits.Count == 0)
            {
                var rallySim = GameBootstrapper.Instance?.Simulation;
                if (rallySim != null)
                {
                    Ray rallyRay = mainCamera.ScreenPointToRay(currentMousePos);

                    // Check if clicking on a unit (e.g. sheep)
                    if (Physics.Raycast(rallyRay, out RaycastHit rallyUnitHit, 200f, unitLayer))
                    {
                        var rallyUnitView = rallyUnitHit.collider.GetComponent<UnitView>();
                        if (rallyUnitView != null)
                        {
                            FixedVector3 fixedPos = FixedVector3.FromVector3(rallyUnitHit.point);
                            for (int i = 0; i < selectedBuildings.Count; i++)
                            {
                                rallySim.CommandBuffer.EnqueueCommand(new SetRallyPointCommand(
                                    LocalPlayerId, selectedBuildings[i].BuildingId, fixedPos,
                                    -1, rallyUnitView.UnitId));
                            }
                            isRightDragging = false;
                            return;
                        }
                    }

                    // Check if clicking on a resource node
                    if (Physics.Raycast(rallyRay, out RaycastHit rallyResourceHit, 1000f, resourceLayer))
                    {
                        var resNode = rallyResourceHit.collider.GetComponent<ResourceNode>();
                        if (resNode != null)
                        {
                            FixedVector3 fixedPos = FixedVector3.FromVector3(rallyResourceHit.point);
                            for (int i = 0; i < selectedBuildings.Count; i++)
                            {
                                rallySim.CommandBuffer.EnqueueCommand(new SetRallyPointCommand(
                                    LocalPlayerId, selectedBuildings[i].BuildingId, fixedPos, resNode.ResourceNodeId));
                            }
                            isRightDragging = false;
                            return;
                        }
                    }

                    // Check if clicking on a building (construction site or farm)
                    if (Physics.Raycast(rallyRay, out RaycastHit rallyBuildingHit, 1000f, buildingLayer))
                    {
                        var rallyBuildingView = rallyBuildingHit.collider.GetComponent<BuildingView>();
                        if (rallyBuildingView != null)
                        {
                            var rallyBuildingData = rallySim.BuildingRegistry.GetBuilding(rallyBuildingView.BuildingId);
                            if (rallyBuildingData != null)
                            {
                                // Under-construction building owned by local player → construction rally
                                if (rallyBuildingData.IsUnderConstruction && rallyBuildingData.PlayerId == LocalPlayerId)
                                {
                                    FixedVector3 fixedPos = rallyBuildingData.SimPosition;
                                    for (int i = 0; i < selectedBuildings.Count; i++)
                                    {
                                        rallySim.CommandBuffer.EnqueueCommand(new SetRallyPointCommand(
                                            LocalPlayerId, selectedBuildings[i].BuildingId, fixedPos, -1, -1, rallyBuildingData.Id));
                                    }
                                    isRightDragging = false;
                                    return;
                                }

                                // Completed farm → resource rally
                                if (rallyBuildingData.Type == BuildingType.Farm
                                    && !rallyBuildingData.IsUnderConstruction && rallyBuildingData.LinkedResourceNodeId >= 0)
                                {
                                    FixedVector3 fixedPos = rallyBuildingData.SimPosition;
                                    for (int i = 0; i < selectedBuildings.Count; i++)
                                    {
                                        rallySim.CommandBuffer.EnqueueCommand(new SetRallyPointCommand(
                                            LocalPlayerId, selectedBuildings[i].BuildingId, fixedPos, rallyBuildingData.LinkedResourceNodeId));
                                    }
                                    isRightDragging = false;
                                    return;
                                }
                            }
                        }
                    }

                    if (Physics.Raycast(rallyRay, out RaycastHit rallyHit, 1000f, groundLayer))
                    {
                        FixedVector3 fixedPos = FixedVector3.FromVector3(rallyHit.point);
                        for (int i = 0; i < selectedBuildings.Count; i++)
                        {
                            rallySim.CommandBuffer.EnqueueCommand(new SetRallyPointCommand(
                                LocalPlayerId, selectedBuildings[i].BuildingId, fixedPos));
                        }
                    }
                }
                isRightDragging = false;
                return;
            }

            if (!HasSelectedOwnUnit())
            {
                isRightDragging = false;
                return;
            }

            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) { isRightDragging = false; return; }

            Ray ray = mainCamera.ScreenPointToRay(currentMousePos);

            // Check units (sheep slaughter or enemy attack)
            if (!isRightDragging && Physics.Raycast(ray, out RaycastHit unitCmdHit, 200f, unitLayer))
            {
                var unitView = unitCmdHit.collider.GetComponent<UnitView>();
                if (unitView != null && !unitView.IsDead)
                {
                    // Check for sheep slaughter: right-click allied sheep with villagers selected
                    var sheepData = sim.UnitRegistry.GetUnit(unitView.UnitId);
                    if (sheepData != null && sheepData.IsSheep && sim.AreAllies(sheepData.PlayerId, LocalPlayerId))
                    {
                        // Collect villager IDs from selection
                        var villagerIds = new System.Collections.Generic.List<int>();
                        int[] allIds = GetSelectedUnitIds();
                        for (int vi = 0; vi < allIds.Length; vi++)
                        {
                            var u = sim.UnitRegistry.GetUnit(allIds[vi]);
                            if (u != null && u.IsVillager && u.State != UnitState.Dead)
                                villagerIds.Add(allIds[vi]);
                        }
                        if (villagerIds.Count > 0)
                        {
                            var slaughterCmd = new SlaughterSheepCommand
                            {
                                PlayerId = LocalPlayerId,
                                VillagerIds = villagerIds.ToArray(),
                                SheepUnitId = unitView.UnitId,
                                IsQueued = multiSelectHeld
                            };
                            sim.CommandBuffer.EnqueueCommand(slaughterCmd);
                            SFXManager.Instance?.PlayUI(SFXType.CommandMove, 0.5f);
                            isRightDragging = false;
                            return;
                        }
                    }

                    // Check for sheep follow: right-click allied scout with sheep selected
                    var clickedUnit = sim.UnitRegistry.GetUnit(unitView.UnitId);
                    if (clickedUnit != null && clickedUnit.UnitType == 4 && sim.AreAllies(clickedUnit.PlayerId, LocalPlayerId))
                    {
                        var sheepIds = new System.Collections.Generic.List<int>();
                        int[] allSelIds = GetSelectedUnitIds();
                        for (int si = 0; si < allSelIds.Length; si++)
                        {
                            var u = sim.UnitRegistry.GetUnit(allSelIds[si]);
                            if (u != null && u.IsSheep && u.State != UnitState.Dead)
                                sheepIds.Add(allSelIds[si]);
                        }
                        if (sheepIds.Count > 0)
                        {
                            var followCmd = new FollowUnitCommand
                            {
                                PlayerId = LocalPlayerId,
                                UnitIds = sheepIds.ToArray(),
                                TargetUnitId = unitView.UnitId,
                                IsQueued = multiSelectHeld
                            };
                            sim.CommandBuffer.EnqueueCommand(followCmd);
                            SFXManager.Instance?.PlayUI(SFXType.CommandMove, 0.5f);
                            isRightDragging = false;
                            return;
                        }
                    }

                    // Check for monk heal: right-click damaged friendly unit with monks selected
                    if (clickedUnit != null && sim.AreAllies(clickedUnit.PlayerId, LocalPlayerId)
                        && clickedUnit.CurrentHealth < clickedUnit.MaxHealth)
                    {
                        var monkIds = new System.Collections.Generic.List<int>();
                        int[] allMonkSelIds = GetSelectedUnitIds();
                        for (int mi = 0; mi < allMonkSelIds.Length; mi++)
                        {
                            var mu = sim.UnitRegistry.GetUnit(allMonkSelIds[mi]);
                            if (mu != null && mu.IsHealer && mu.State != UnitState.Dead)
                                monkIds.Add(allMonkSelIds[mi]);
                        }
                        if (monkIds.Count > 0)
                        {
                            var healCmd = new HealUnitCommand
                            {
                                PlayerId = LocalPlayerId,
                                UnitIds = monkIds.ToArray(),
                                TargetUnitId = unitView.UnitId,
                                IsQueued = multiSelectHeld
                            };
                            sim.CommandBuffer.EnqueueCommand(healCmd);
                            SFXManager.Instance?.PlayUI(SFXType.CommandMove, 0.5f);
                            isRightDragging = false;
                            return;
                        }
                    }

                    var teamSim = GameBootstrapper.Instance?.Simulation;
                    if (teamSim != null && !teamSim.AreAllies(unitView.PlayerId, LocalPlayerId))
                    {
                        int[] unitIds = GetSelectedUnitIds();
                        var attackCmd = new AttackUnitCommand(LocalPlayerId, unitIds, unitView.UnitId);
                        attackCmd.IsQueued = multiSelectHeld;
                        sim.CommandBuffer.EnqueueCommand(attackCmd);
                        unitView.FlashAttackConfirm();
                        unitView.ShowAttackTargetRing();
                        isRightDragging = false;
                        return;
                    }
                }
            }

            // Check buildings (construct own under-construction, or attack enemy)
            if (!isRightDragging && Physics.Raycast(ray, out RaycastHit buildingCmdHit, 1000f, buildingLayer))
            {
                var buildingView = buildingCmdHit.collider.GetComponent<BuildingView>();
                if (buildingView != null && !buildingView.IsDestroyed)
                {
                    var teamSim = GameBootstrapper.Instance?.Simulation;
                    if (teamSim != null && teamSim.AreAllies(buildingView.PlayerId, LocalPlayerId))
                    {
                        // Own/allied building under construction: send villagers to help build
                        var buildingData = sim.BuildingRegistry.GetBuilding(buildingView.BuildingId);
                        if (buildingData != null && buildingData.IsUnderConstruction)
                        {
                            int[] unitIds = GetSelectedUnitIds();
                            var constructCmd = new ConstructBuildingCommand(
                                LocalPlayerId, unitIds, buildingView.BuildingId);
                            constructCmd.IsQueued = multiSelectHeld;
                            sim.CommandBuffer.EnqueueCommand(constructCmd);
                            buildingView.FlashCommandConfirm();
                            SFXManager.Instance?.PlayUI(SFXType.CommandMove, 0.5f);
                            isRightDragging = false;
                            return;
                        }
                        else if (buildingData != null && GameSimulation.IsDropOffBuilding(buildingData.Type) && !buildingData.IsUnderConstruction)
                        {
                            // Sheep right-clicked on food drop-off (TC/Mill): move at boosted speed
                            bool isFoodDropOff = buildingData.Type == BuildingType.TownCenter || buildingData.Type == BuildingType.Mill;
                            int[] allIds = GetSelectedUnitIds();

                            if (isFoodDropOff)
                            {
                                var sheepIds = new System.Collections.Generic.List<int>();
                                var nonSheepIds = new System.Collections.Generic.List<int>();
                                for (int i = 0; i < allIds.Length; i++)
                                {
                                    var u = sim.UnitRegistry.GetUnit(allIds[i]);
                                    if (u != null && u.IsSheep && u.State != UnitState.Dead)
                                        sheepIds.Add(allIds[i]);
                                    else
                                        nonSheepIds.Add(allIds[i]);
                                }

                                if (sheepIds.Count > 0)
                                {
                                    var sheepTarget = new FixedVector3(
                                        Fixed32.FromInt(buildingData.OriginTileX),
                                        Fixed32.Zero,
                                        Fixed32.FromInt(buildingData.OriginTileZ));

                                    var sheepMoveCmd = new MoveCommand(LocalPlayerId, sheepIds.ToArray(),
                                        sheepTarget);
                                    sheepMoveCmd.IsQueued = multiSelectHeld;
                                    sim.CommandBuffer.EnqueueCommand(sheepMoveCmd);

                                    if (gameSetup != null)
                                    {
                                        var markers = new System.Collections.Generic.List<Vector3>();
                                        markers.Add(sheepTarget.ToVector3());
                                        gameSetup.ShowMarkers(markers);
                                    }

                                    if (nonSheepIds.Count > 0)
                                    {
                                        var dropOffCmd = new DropOffCommand(LocalPlayerId, nonSheepIds.ToArray(), buildingView.BuildingId);
                                        dropOffCmd.IsQueued = multiSelectHeld;
                                        sim.CommandBuffer.EnqueueCommand(dropOffCmd);
                                    }

                                    buildingView.FlashCommandConfirm();
                                    SFXManager.Instance?.PlayUI(SFXType.CommandMove, 0.5f);
                                    isRightDragging = false;
                                    return;
                                }
                            }

                            var defaultDropOffCmd = new DropOffCommand(LocalPlayerId, allIds, buildingView.BuildingId);
                            defaultDropOffCmd.IsQueued = multiSelectHeld;
                            sim.CommandBuffer.EnqueueCommand(defaultDropOffCmd);
                            buildingView.FlashCommandConfirm();
                            SFXManager.Instance?.PlayUI(SFXType.CommandMove, 0.5f);
                            isRightDragging = false;
                            return;
                        }
                        else if (buildingData != null && buildingData.Type == BuildingType.Farm
                            && !buildingData.IsUnderConstruction && buildingData.LinkedResourceNodeId >= 0)
                        {
                            // Right-click own farm: gather from its linked resource node
                            int[] unitIds = GetSelectedUnitIds();
                            var gatherCmd = new GatherCommand(LocalPlayerId, unitIds, buildingData.LinkedResourceNodeId);
                            gatherCmd.IsQueued = multiSelectHeld;
                            sim.CommandBuffer.EnqueueCommand(gatherCmd);
                            buildingView.FlashCommandConfirm();
                            SFXManager.Instance?.PlayUI(SFXType.CommandMove, 0.5f);
                            isRightDragging = false;
                            return;
                        }
                        else if (buildingData != null && buildingData.GarrisonCapacity > 0
                            && !buildingData.IsUnderConstruction)
                        {
                            // Right-click own garrisonable building: garrison units
                            int[] unitIds = GetSelectedUnitIds();
                            var garrisonCmd = new GarrisonCommand(LocalPlayerId, unitIds, buildingView.BuildingId);
                            sim.CommandBuffer.EnqueueCommand(garrisonCmd);
                            buildingView.FlashCommandConfirm();
                            SFXManager.Instance?.PlayUI(SFXType.CommandMove, 0.5f);
                            isRightDragging = false;
                            return;
                        }
                    }
                    else
                    {
                        // Enemy building: attack
                        int[] unitIds = GetSelectedUnitIds();
                        var attackCmd = new AttackBuildingCommand(LocalPlayerId, unitIds, buildingView.BuildingId);
                        attackCmd.IsQueued = multiSelectHeld;
                        sim.CommandBuffer.EnqueueCommand(attackCmd);
                        buildingView.FlashAttackConfirm();
                        isRightDragging = false;
                        return;
                    }
                }
            }

            // Check resource nodes first (no formation drag for gather)
            if (!isRightDragging && Physics.Raycast(ray, out RaycastHit resourceHit, 1000f, resourceLayer))
            {
                var resourceNode = resourceHit.collider.GetComponent<ResourceNode>();
                if (resourceNode != null)
                {
                    var villagerIds = new List<int>();
                    var militaryIds = new List<int>();
                    for (int i = 0; i < selectedUnits.Count; i++)
                    {
                        if (selectedUnits[i].PlayerId != LocalPlayerId) continue;
                        if (selectedUnits[i].UnitType == 0)
                            villagerIds.Add(selectedUnits[i].UnitId);
                        else
                            militaryIds.Add(selectedUnits[i].UnitId);
                    }

                    if (villagerIds.Count > 0)
                    {
                        var gatherCmd = new GatherCommand(LocalPlayerId, villagerIds.ToArray(), resourceNode.ResourceNodeId);
                        gatherCmd.IsQueued = multiSelectHeld;
                        sim.CommandBuffer.EnqueueCommand(gatherCmd);
                        resourceNode.FlashCommandConfirm();
                    }

                    if (militaryIds.Count > 0)
                    {
                        int[] milIds = militaryIds.ToArray();
                        FixedVector3 fixedTarget = FixedVector3.FromVector3(resourceHit.point);
                        var positions = GameSetup.ComputeGridFormation(
                            GameSetup.SnapClickToNearestWalkable(sim.MapData, resourceHit.point), milIds.Length);
                        GameSetup.SnapToWalkable(sim.MapData, positions);
                        FixedVector3[] fixedPositions = ConvertToFixed(positions);
                        var moveCmd = new MoveCommand(LocalPlayerId, milIds, fixedTarget, fixedPositions);
                        moveCmd.IsQueued = multiSelectHeld;
                        sim.CommandBuffer.EnqueueCommand(moveCmd);
                    }

                    SFXManager.Instance?.PlayUI(SFXType.CommandMove, 0.5f);
                    isRightDragging = false;
                    return;
                }
            }

            if (isRightDragging)
            {
                // Formation drag — use grouped line formation (each type gets own rows)
                if (Physics.Raycast(ray, out RaycastHit groundHit, 1000f, groundLayer))
                {
                    int[] unitIds = GetFormationSortedUnitIds(out int[] groupSizes);
                    Vector3 snappedHit = GameSetup.SnapClickToNearestWalkable(sim.MapData, groundHit.point);
                    var positions = GameSetup.ComputeGroupedLineFormation(
                        rightDragStartWorld, snappedHit, groupSizes);
                    GameSetup.SnapToWalkable(sim.MapData, positions);
                    Vector3 center = (rightDragStartWorld + groundHit.point) * 0.5f;
                    // Convert float positions to FixedVector3 at the command boundary
                    FixedVector3 fixedCenter = FixedVector3.FromVector3(center);
                    FixedVector3[] fixedPositions = ConvertToFixed(positions);
                    // Compute facing direction perpendicular to drag vector
                    Vector3 dragDir = groundHit.point - rightDragStartWorld;
                    dragDir.y = 0f;
                    MoveCommand dragCmd;
                    if (dragDir.sqrMagnitude > 0.001f)
                    {
                        dragDir.Normalize();
                        Vector3 facingDir = new Vector3(-dragDir.z, 0f, dragDir.x);
                        FixedVector3 fixedFacing = FixedVector3.FromVector3(facingDir);
                        dragCmd = new MoveCommand(LocalPlayerId, unitIds, fixedCenter, fixedPositions, fixedFacing);
                    }
                    else
                    {
                        dragCmd = new MoveCommand(LocalPlayerId, unitIds, fixedCenter, fixedPositions);
                    }
                    dragCmd.IsQueued = multiSelectHeld;
                    sim.CommandBuffer.EnqueueCommand(dragCmd);
                    SFXManager.Instance?.PlayUI(SFXType.CommandMove, 0.5f);
                }

                // Transition preview markers to fading and hide arrow
                if (gameSetup != null)
                {
                    gameSetup.CommitMarkers();
                    gameSetup.HideFacingArrow();
                }

                isRightDragging = false;
            }
            else
            {
                // Simple right-click — preserve formation if all selected units are in formation
                if (Physics.Raycast(ray, out RaycastHit groundHit, 1000f, groundLayer))
                {
                    int[] unitIds = GetSelectedUnitIds();
                    FixedVector3 fixedTarget = FixedVector3.FromVector3(groundHit.point);

                    bool canPreserve = selectedUnits.Count > 1 && selectedUnits[0].InFormation;
                    if (canPreserve)
                    {
                        int gid = selectedUnits[0].FormationGroupId;
                        if (gid == 0 || selectedUnits[0].FormationGroupSize != selectedUnits.Count)
                            canPreserve = false;
                        else
                        {
                            for (int i = 1; i < selectedUnits.Count; i++)
                            {
                                if (!selectedUnits[i].InFormation || selectedUnits[i].FormationGroupId != gid)
                                { canPreserve = false; break; }
                            }
                        }
                    }

                    if (canPreserve)
                    {
                        var moveCmd = new MoveCommand(LocalPlayerId, unitIds, fixedTarget, true);
                        moveCmd.IsQueued = multiSelectHeld;
                        sim.CommandBuffer.EnqueueCommand(moveCmd);

                        if (gameSetup != null)
                        {
                            var positions = new List<Vector3>(selectedUnits.Count);
                            for (int i = 0; i < selectedUnits.Count; i++)
                                positions.Add((fixedTarget + selectedUnits[i].FormationOffset).ToVector3());
                            gameSetup.ShowMarkers(positions);
                        }
                    }
                    else
                    {
                        var positions = GameSetup.ComputeGridFormation(
                            GameSetup.SnapClickToNearestWalkable(sim.MapData, groundHit.point), unitIds.Length);
                        GameSetup.SnapToWalkable(sim.MapData, positions);
                        FixedVector3[] fixedPositions = ConvertToFixed(positions);
                        var moveCmd = new MoveCommand(LocalPlayerId, unitIds, fixedTarget, fixedPositions);
                        moveCmd.IsQueued = multiSelectHeld;
                        sim.CommandBuffer.EnqueueCommand(moveCmd);

                        if (gameSetup != null)
                            gameSetup.ShowMarkers(positions);
                    }
                    SFXManager.Instance?.PlayUI(SFXType.CommandMove, 0.5f);
                }
            }
        }

        private void OnMultiSelectReleased(InputAction.CallbackContext ctx)
        {
            multiSelectHeld = false;

            if (isPlacingBuilding && placementIsShiftQueued)
                CancelBuildPlacement();
        }

        private void OnAttackMovePerformed(InputAction.CallbackContext ctx)
        {
            if (UnitInfoUI.BuildHotkeysActive) return;
            if (selectedUnits.Count > 0)
            {
                garrisonMode = false;
                patrolMode = false;
                attackMoveMode = true;
            }
        }

        public void EnterAttackMoveMode()
        {
            if (selectedUnits.Count > 0)
            {
                garrisonMode = false;
                patrolMode = false;
                attackMoveMode = true;
            }
        }

        public void EnterGarrisonMode()
        {
            if (selectedUnits.Count > 0)
            {
                attackMoveMode = false;
                patrolMode = false;
                garrisonMode = true;
            }
        }

        public void EnterPatrolMode()
        {
            if (selectedUnits.Count > 0)
            {
                attackMoveMode = false;
                garrisonMode = false;
                patrolMode = true;
            }
        }

        private void OnEscapePressed(InputAction.CallbackContext ctx)
        {
            if (SettingsMenuUI.IsRebinding) return;
            if (UnitInfoUI.BuildHotkeysActive)
            {
                UnitInfoUI.DeactivateBuildHotkeys();
                return;
            }
            if (settingsMenuOpen)
            {
                SettingsMenuUI.Close();
                return;
            }
            if (SettingsMenuUI.IsPlacingDummy)
            {
                SettingsMenuUI.IsPlacingDummy = false;
                return;
            }
            if (isMeteorTargeting)
            {
                isMeteorTargeting = false;
                DestroyMeteorTargetingPreview();
                return;
            }
            if (isHealingRainTargeting)
            {
                isHealingRainTargeting = false;
                DestroyGodPowerPreview(ref healingRainTargetingPreview);
                return;
            }
            if (isLightningStormTargeting)
            {
                isLightningStormTargeting = false;
                DestroyGodPowerPreview(ref lightningStormTargetingPreview);
                return;
            }
            if (isTsunamiTargeting)
            {
                isTsunamiTargeting = false;
                tsunamiDragging = false;
                DestroyTsunamiTargetingPreview();
                return;
            }
            if (isPlacingWall)
            {
                CancelWallPlacement();
                return;
            }
            if (isPlacingBuilding)
            {
                CancelBuildPlacement();
                return;
            }
            if (attackMoveMode)
            {
                attackMoveMode = false;
                return;
            }
            if (garrisonMode)
            {
                garrisonMode = false;
                return;
            }
            if (patrolMode)
            {
                patrolMode = false;
                return;
            }
            SettingsMenuUI.Open();
        }

        // ── Control groups ─────────────────────────────────────────────────────

        private void OnControlGroupKey(int index)
        {
            if (UIInputSuppressed) return;

            if (Keyboard.current.ctrlKey.isPressed)
            {
                AssignControlGroup(index);
            }
            else if (Keyboard.current.shiftKey.isPressed)
            {
                AddToControlGroup(index);
            }
            else
                RecallControlGroup(index);
        }

        private void AssignControlGroup(int index)
        {
            if (selectedUnits.Count > 0)
            {
                // Clear any building group at this index (groups are exclusive)
                if (buildingControlGroups.TryGetValue(index, out var oldBldIds))
                {
                    foreach (var id in oldBldIds)
                        if (buildingViews.TryGetValue(id, out var bv)) bv.SetControlGroup(-1);
                    buildingControlGroups.Remove(index);
                }

                // Build set of newly selected unit IDs
                var newIdSet = new HashSet<int>();
                foreach (var v in selectedUnits) newIdSet.Add(v.UnitId);

                // Clear badge on units being removed from this group
                if (controlGroups.TryGetValue(index, out var oldIds))
                {
                    foreach (var oldId in oldIds)
                    {
                        if (!newIdSet.Contains(oldId) && unitViews.TryGetValue(oldId, out var oldView))
                            oldView.SetControlGroup(-1);
                    }
                }

                // Assign new members
                var ids = new List<int>(selectedUnits.Count);
                foreach (var v in selectedUnits)
                {
                    ids.Add(v.UnitId);
                    v.SetControlGroup(index);
                }
                controlGroups[index] = ids;
            }
            else if (selectedBuildings.Count > 0)
            {
                // Clear unit group at this index (groups are exclusive)
                if (controlGroups.TryGetValue(index, out var oldUnitIds))
                {
                    foreach (var id in oldUnitIds)
                        if (unitViews.TryGetValue(id, out var v)) v.SetControlGroup(-1);
                    controlGroups.Remove(index);
                }
                // Clear old building group badges at this index
                if (buildingControlGroups.TryGetValue(index, out var oldBldIds2))
                    foreach (var id in oldBldIds2)
                        if (buildingViews.TryGetValue(id, out var bv)) bv.SetControlGroup(-1);

                // Assign buildings with badge
                var ids = new List<int>(selectedBuildings.Count);
                foreach (var v in selectedBuildings) { ids.Add(v.BuildingId); v.SetControlGroup(index); }
                buildingControlGroups[index] = ids;
                activeTabBuildingType = null;
            }
        }

        private void AddToControlGroup(int index)
        {
            if (selectedUnits.Count > 0)
            {
                // Clear any building group at this index (groups are exclusive)
                if (buildingControlGroups.TryGetValue(index, out var oldBldIdsAdd))
                {
                    foreach (var id in oldBldIdsAdd)
                        if (buildingViews.TryGetValue(id, out var bv)) bv.SetControlGroup(-1);
                    buildingControlGroups.Remove(index);
                }

                // Get or create the existing member list
                if (!controlGroups.TryGetValue(index, out var ids))
                    ids = new List<int>();

                var existingIdSet = new HashSet<int>(ids);

                foreach (var v in selectedUnits)
                {
                    if (!existingIdSet.Contains(v.UnitId))
                    {
                        ids.Add(v.UnitId);
                        existingIdSet.Add(v.UnitId);
                    }
                    v.SetControlGroup(index);
                }
                controlGroups[index] = ids;
            }
            else if (selectedBuildings.Count > 0)
            {
                // Clear unit group at this index if present
                if (controlGroups.TryGetValue(index, out var oldUnitIds))
                {
                    foreach (var id in oldUnitIds)
                        if (unitViews.TryGetValue(id, out var v)) v.SetControlGroup(-1);
                    controlGroups.Remove(index);
                }
                if (!buildingControlGroups.TryGetValue(index, out var bldIds))
                    bldIds = new List<int>();
                var existingSet = new HashSet<int>(bldIds);
                foreach (var v in selectedBuildings)
                    if (!existingSet.Contains(v.BuildingId))
                    {
                        bldIds.Add(v.BuildingId);
                        existingSet.Add(v.BuildingId);
                        v.SetControlGroup(index);
                    }
                buildingControlGroups[index] = bldIds;
            }
        }

        private void RecallControlGroup(int index)
        {
            bool hasUnits = controlGroups.TryGetValue(index, out var unitIds) && unitIds.Count > 0;
            bool hasBuildings = buildingControlGroups.TryGetValue(index, out var bldIds) && bldIds.Count > 0;

            if (hasUnits)
            {
                if (index == lastRecalledGroupIndex && IsGroupCurrentlySelected(unitIds))
                {
                    // Second press while group is selected → pan camera only
                    PanToGroupCentroid(unitIds);
                    return;
                }

                // First press → select units, no camera pan
                DeselectAll();
                DeselectBuilding();
                DeselectResourceNode();

                foreach (var id in unitIds)
                {
                    if (!unitViews.TryGetValue(id, out var view) || view.IsDead) continue;
                    view.SetSelected(true);
                    selectedUnits.Add(view);
                }

                lastRecalledGroupIndex = index;
            }
            else if (hasBuildings)
            {
                // Double-press → pan camera
                if (index == lastRecalledGroupIndex && IsBuildingGroupCurrentlySelected(bldIds))
                {
                    PanToBuildingGroupCentroid(bldIds);
                    return;
                }

                DeselectAll();
                DeselectBuilding();
                DeselectResourceNode();
                activeTabBuildingType = null;   // reset tab on fresh recall

                foreach (var id in bldIds)
                {
                    if (!buildingViews.TryGetValue(id, out var view) || view.IsDestroyed) continue;
                    view.SetSelected(true);
                    selectedBuildings.Add(view);
                }
                lastRecalledGroupIndex = index;
            }
        }

        private bool IsGroupCurrentlySelected(List<int> ids)
        {
            var aliveIds = new HashSet<int>();
            foreach (var id in ids)
                if (unitViews.TryGetValue(id, out var v) && !v.IsDead)
                    aliveIds.Add(id);

            if (aliveIds.Count == 0 || selectedUnits.Count != aliveIds.Count) return false;
            foreach (var v in selectedUnits)
                if (!aliveIds.Contains(v.UnitId)) return false;
            return true;
        }

        private void PanToGroupCentroid(List<int> ids)
        {
            Vector3 centroid = Vector3.zero;
            int count = 0;
            foreach (var id in ids)
            {
                if (!unitViews.TryGetValue(id, out var view) || view.IsDead) continue;
                centroid += view.transform.position;
                count++;
            }
            if (count > 0) PanCameraTo(centroid / count);
        }

        private bool IsBuildingGroupCurrentlySelected(List<int> ids)
        {
            var aliveIds = new HashSet<int>();
            foreach (var id in ids)
                if (buildingViews.TryGetValue(id, out var v) && !v.IsDestroyed) aliveIds.Add(id);
            if (aliveIds.Count == 0 || selectedBuildings.Count != aliveIds.Count) return false;
            foreach (var v in selectedBuildings)
                if (!aliveIds.Contains(v.BuildingId)) return false;
            return true;
        }

        private void PanToBuildingGroupCentroid(List<int> ids)
        {
            Vector3 centroid = Vector3.zero;
            int count = 0;
            foreach (var id in ids)
            {
                if (!buildingViews.TryGetValue(id, out var view) || view.IsDestroyed) continue;
                centroid += view.transform.position;
                count++;
            }
            if (count > 0) PanCameraTo(centroid / count);
        }

        // ── Camera helpers ─────────────────────────────────────────────────────

        private void PanCameraTo(Vector3 worldPos)
        {
            if (cameraController == null)
                cameraController = FindFirstObjectByType<RTSCameraController>();
            if (cameraController != null)
                cameraController.PivotPosition = new Vector3(worldPos.x, 0f, worldPos.z);
        }

        private void IssueAttackMoveCommand()
        {
            if (selectedUnits.Count == 0) return;

            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            Ray ray = mainCamera.ScreenPointToRay(currentMousePos);

            if (isDragging)
            {
                // Formation drag — same as right-drag but with IsAttackMove
                if (Physics.Raycast(ray, out RaycastHit groundHit, 1000f, groundLayer))
                {
                    int[] unitIds = GetFormationSortedUnitIds(out int[] groupSizes);
                    Vector3 snappedHit = GameSetup.SnapClickToNearestWalkable(sim.MapData, groundHit.point);
                    var positions = GameSetup.ComputeGroupedLineFormation(
                        rightDragStartWorld, snappedHit, groupSizes);
                    GameSetup.SnapToWalkable(sim.MapData, positions);
                    Vector3 center = (rightDragStartWorld + groundHit.point) * 0.5f;
                    FixedVector3 fixedCenter = FixedVector3.FromVector3(center);
                    FixedVector3[] fixedPositions = ConvertToFixed(positions);

                    Vector3 dragDir = groundHit.point - rightDragStartWorld;
                    dragDir.y = 0f;
                    MoveCommand cmd;
                    if (dragDir.sqrMagnitude > 0.001f)
                    {
                        dragDir.Normalize();
                        Vector3 facingDir = new Vector3(-dragDir.z, 0f, dragDir.x);
                        FixedVector3 fixedFacing = FixedVector3.FromVector3(facingDir);
                        cmd = new MoveCommand(LocalPlayerId, unitIds, fixedCenter, fixedPositions, fixedFacing);
                    }
                    else
                    {
                        cmd = new MoveCommand(LocalPlayerId, unitIds, fixedCenter, fixedPositions);
                    }
                    cmd.IsAttackMove = true;
                    cmd.IsQueued = multiSelectHeld;
                    sim.CommandBuffer.EnqueueCommand(cmd);
                }

                if (gameSetup != null)
                {
                    gameSetup.CommitMarkers();
                    gameSetup.HideFacingArrow();
                }
            }
            else
            {
                // A-move onto a specific enemy unit → send AttackUnitCommand (same as right-click attack)
                if (Physics.Raycast(ray, out RaycastHit unitHit, 200f, unitLayer))
                {
                    var unitView = unitHit.collider.GetComponent<UnitView>();
                    if (unitView != null && !unitView.IsDead && !sim.AreAllies(unitView.PlayerId, LocalPlayerId))
                    {
                        int[] unitIds = GetSelectedUnitIds();
                        var attackCmd = new AttackUnitCommand(LocalPlayerId, unitIds, unitView.UnitId);
                        attackCmd.IsQueued = multiSelectHeld;
                        sim.CommandBuffer.EnqueueCommand(attackCmd);
                        unitView.FlashAttackConfirm();
                        unitView.ShowAttackTargetRing();
                        return;
                    }
                }

                // Simple click on ground — attack-move to position
                if (Physics.Raycast(ray, out RaycastHit groundHit, 1000f, groundLayer))
                {
                    int[] unitIds = GetSelectedUnitIds();
                    FixedVector3 fixedTarget = FixedVector3.FromVector3(groundHit.point);

                    bool canPreserve = selectedUnits.Count > 1 && selectedUnits[0].InFormation;
                    if (canPreserve)
                    {
                        int gid = selectedUnits[0].FormationGroupId;
                        if (gid == 0 || selectedUnits[0].FormationGroupSize != selectedUnits.Count)
                            canPreserve = false;
                        else
                        {
                            for (int i = 1; i < selectedUnits.Count; i++)
                            {
                                if (!selectedUnits[i].InFormation || selectedUnits[i].FormationGroupId != gid)
                                { canPreserve = false; break; }
                            }
                        }
                    }

                    if (canPreserve)
                    {
                        var cmd = new MoveCommand(LocalPlayerId, unitIds, fixedTarget, true);
                        cmd.IsAttackMove = true;
                        cmd.IsQueued = multiSelectHeld;
                        sim.CommandBuffer.EnqueueCommand(cmd);

                        if (gameSetup != null)
                        {
                            var positions = new List<Vector3>(selectedUnits.Count);
                            for (int i = 0; i < selectedUnits.Count; i++)
                                positions.Add((fixedTarget + selectedUnits[i].FormationOffset).ToVector3());
                            gameSetup.ShowMarkers(positions);
                        }
                    }
                    else
                    {
                        var positions = GameSetup.ComputeGridFormation(
                            GameSetup.SnapClickToNearestWalkable(sim.MapData, groundHit.point), unitIds.Length);
                        GameSetup.SnapToWalkable(sim.MapData, positions);
                        FixedVector3[] fixedPositions = ConvertToFixed(positions);
                        var cmd = new MoveCommand(LocalPlayerId, unitIds, fixedTarget, fixedPositions);
                        cmd.IsAttackMove = true;
                        cmd.IsQueued = multiSelectHeld;
                        sim.CommandBuffer.EnqueueCommand(cmd);

                        if (gameSetup != null)
                            gameSetup.ShowMarkers(positions);
                    }
                }
            }
        }

        private void TryIssueGarrisonAtClick()
        {
            if (selectedUnits.Count == 0) return;

            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            Ray ray = mainCamera.ScreenPointToRay(currentMousePos);
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, buildingLayer))
                return;

            var buildingView = hit.collider.GetComponent<BuildingView>();
            if (buildingView == null) return;

            var buildingData = sim.BuildingRegistry.GetBuilding(buildingView.BuildingId);
            if (buildingData == null || buildingData.IsDestroyed) return;
            if (!sim.AreAllies(buildingData.PlayerId, LocalPlayerId)) return;
            if (buildingData.GarrisonCapacity <= 0) return;
            if (buildingData.IsUnderConstruction) return;

            int[] unitIds = GetSelectedUnitIds();
            sim.CommandBuffer.EnqueueCommand(new GarrisonCommand(LocalPlayerId, unitIds, buildingView.BuildingId));
            buildingView.FlashCommandConfirm();
            SFXManager.Instance?.PlayUI(SFXType.CommandMove, 0.5f);
        }

        private void IssuePatrolCommand()
        {
            if (selectedUnits.Count == 0) return;

            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            Ray ray = mainCamera.ScreenPointToRay(currentMousePos);
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
                return;

            int[] unitIds = GetSelectedUnitIds();
            FixedVector3 fixedTarget = FixedVector3.FromVector3(hit.point);
            var cmd = new PatrolCommand(LocalPlayerId, unitIds, fixedTarget);
            cmd.IsQueued = multiSelectHeld;
            sim.CommandBuffer.EnqueueCommand(cmd);
            SFXManager.Instance?.PlayUI(SFXType.CommandMove, 0.5f);
        }

        private static FixedVector3[] ConvertToFixed(List<Vector3> positions)
        {
            var result = new FixedVector3[positions.Count];
            for (int i = 0; i < positions.Count; i++)
                result[i] = FixedVector3.FromVector3(positions[i]);
            return result;
        }

        public int[] GetSelectedUnitIds()
        {
            int count = 0;
            for (int i = 0; i < selectedUnits.Count; i++)
            {
                if (selectedUnits[i].PlayerId == LocalPlayerId)
                    count++;
            }
            int[] ids = new int[count];
            int idx = 0;
            for (int i = 0; i < selectedUnits.Count; i++)
            {
                if (selectedUnits[i].PlayerId == LocalPlayerId)
                    ids[idx++] = selectedUnits[i].UnitId;
            }
            return ids;
        }

        private static int GetFormationPriority(int unitType)
        {
            switch (unitType)
            {
                case 4: return 0; // Scout — front (fast)
                case 3: return 0; // Horseman — front
                case 1: return 1; // Spearman
                case 0: return 2; // Villager
                case 2: return 3; // Archer — back
                default: return 2;
            }
        }

        private Vector3 GetSelectedUnitCenter()
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < selectedUnits.Count; i++)
                sum += selectedUnits[i].transform.position;
            return selectedUnits.Count > 0 ? sum / selectedUnits.Count : Vector3.zero;
        }

        private int[] GetFormationSortedUnitIds(out int[] groupSizes)
        {
            // Group units by formation priority: each type gets its own row(s)
            var groups = new SortedDictionary<int, List<int>>();
            for (int i = 0; i < selectedUnits.Count; i++)
            {
                int priority = GetFormationPriority(selectedUnits[i].UnitType);
                if (!groups.ContainsKey(priority))
                    groups[priority] = new List<int>();
                groups[priority].Add(selectedUnits[i].UnitId);
            }

            var ids = new List<int>();
            var sizes = new List<int>();
            foreach (var kvp in groups)
            {
                sizes.Add(kvp.Value.Count);
                ids.AddRange(kvp.Value);
            }
            groupSizes = sizes.ToArray();
            return ids.ToArray();
        }

        private int[] GetFormationGroupSizes()
        {
            var groups = new SortedDictionary<int, int>();
            for (int i = 0; i < selectedUnits.Count; i++)
            {
                int priority = GetFormationPriority(selectedUnits[i].UnitType);
                if (!groups.ContainsKey(priority))
                    groups[priority] = 0;
                groups[priority]++;
            }
            var sizes = new int[groups.Count];
            int idx = 0;
            foreach (var kvp in groups)
                sizes[idx++] = kvp.Value;
            return sizes;
        }

        private float[] GetFormationGroupRadii()
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            float stdRadius = sim != null ? sim.Config.UnitRadius : 0.4f;
            float cavRadius = sim != null ? sim.Config.CavalryRadius : 0.55f;

            var groups = new SortedDictionary<int, float>();
            for (int i = 0; i < selectedUnits.Count; i++)
            {
                int priority = GetFormationPriority(selectedUnits[i].UnitType);
                if (!groups.ContainsKey(priority))
                {
                    int ut = selectedUnits[i].UnitType;
                    groups[priority] = (ut == 3 || ut == 4) ? cavRadius : stdRadius;
                }
            }
            var radii = new float[groups.Count];
            int idx = 0;
            foreach (var kvp in groups)
                radii[idx++] = kvp.Value;
            return radii;
        }

        public void EnterBuildPlacement(BuildingType type)
        {
            if (isPlacingBuilding) CancelBuildPlacement();

            isPlacingBuilding = true;
            placementBuildingType = type;

            // Capture all selected villager IDs
            var villagerIds = new System.Collections.Generic.List<int>();
            for (int i = 0; i < selectedUnits.Count; i++)
            {
                if (selectedUnits[i].UnitType == 0 && selectedUnits[i].PlayerId == LocalPlayerId)
                    villagerIds.Add(selectedUnits[i].UnitId);
            }
            placementVillagerIds = villagerIds.Count > 0 ? villagerIds.ToArray() : null;

            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            GetBuildingFootprint(sim.Config, type, out int w, out int h);

            // Create ghost quad
            ghostBuilding = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ghostBuilding.name = "BuildingGhost";
            ghostBuilding.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            int border = (type == BuildingType.Wall || type == BuildingType.Farm || type == BuildingType.StoneWall || type == BuildingType.StoneGate || type == BuildingType.WoodGate) ? 0 : 1;
            ghostBuilding.transform.localScale = new Vector3(w + border * 2, h + border * 2, 1f);

            // Remove collider so it doesn't interfere with raycasts
            var col = ghostBuilding.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            // Create materials
            if (ghostValidMaterial == null)
            {
                ghostValidMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                ghostValidMaterial.color = new Color(0f, 1f, 0f, 0.35f);
                ghostValidMaterial.SetFloat("_Surface", 1);
                ghostValidMaterial.SetOverrideTag("RenderType", "Transparent");
                ghostValidMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                ghostValidMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                ghostValidMaterial.SetInt("_ZWrite", 0);
                ghostValidMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always); // Always — render on top of terrain
                ghostValidMaterial.EnableKeyword("_ALPHABLEND_ON");
                ghostValidMaterial.renderQueue = 3000;
            }
            if (ghostInvalidMaterial == null)
            {
                ghostInvalidMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                ghostInvalidMaterial.color = new Color(1f, 0f, 0f, 0.35f);
                ghostInvalidMaterial.SetFloat("_Surface", 1);
                ghostInvalidMaterial.SetOverrideTag("RenderType", "Transparent");
                ghostInvalidMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                ghostInvalidMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                ghostInvalidMaterial.SetInt("_ZWrite", 0);
                ghostInvalidMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                ghostInvalidMaterial.EnableKeyword("_ALPHABLEND_ON");
                ghostInvalidMaterial.renderQueue = 3000;
            }
            if (ghostInfluenceMaterial == null)
            {
                ghostInfluenceMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                ghostInfluenceMaterial.color = new Color(1f, 0.85f, 0f, 0.35f);
                ghostInfluenceMaterial.SetFloat("_Surface", 1);
                ghostInfluenceMaterial.SetOverrideTag("RenderType", "Transparent");
                ghostInfluenceMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                ghostInfluenceMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                ghostInfluenceMaterial.SetInt("_ZWrite", 0);
                ghostInfluenceMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                ghostInfluenceMaterial.EnableKeyword("_ALPHABLEND_ON");
                ghostInfluenceMaterial.renderQueue = 3000;
            }

            ghostBuilding.GetComponent<Renderer>().sharedMaterial = ghostValidMaterial;
            ghostIsValid = true;
            ghostInInfluenceZone = false;

            // Create attack range visualization for buildings that can attack
            CreateGhostAttackRangeRing(type, sim.Config);
            CreateGhostInfluenceZone(type, sim.Config);
            CreateGhostInfluenceZonesForExistingBuildings(type, sim);

            // Create grid overlay
            CreateGridOverlay(sim.MapData, sim.Config.TerrainHeightScale);
        }

        public void EnterLandmarkPlacement(LandmarkId landmarkId)
        {
            placementLandmarkId = landmarkId;
            EnterBuildPlacement(BuildingType.Landmark);
        }

        private void CreateGhostAttackRangeRing(BuildingType buildingType, SimulationConfig config)
        {
            // Destroy existing range ring if any
            if (ghostAttackRangeRing != null)
            {
                Object.Destroy(ghostAttackRangeRing);
                ghostAttackRangeRing = null;
            }

            // Only create range ring for buildings that can attack
            float attackRange = 0f;
            if (buildingType == BuildingType.Tower)
            {
                attackRange = config.TowerAttackRange;
            }
            else if (buildingType == BuildingType.TownCenter)
            {
                // For placement ghost mode, use subsequent TC range (player is building a new one)
                attackRange = config.SubsequentTownCenterAttackRange;
            }

            if (attackRange <= 0) return;

            // Create range ring GameObject
            ghostAttackRangeRing = new GameObject("GhostAttackRangeRing");
            
            // Add spinning range ring component
            var spinningRing = ghostAttackRangeRing.AddComponent<SpinningAttackRangeRing>();
            spinningRing.Initialize(attackRange);
        }

        private void CreateGhostInfluenceZone(BuildingType type, SimulationConfig config)
        {
            if (ghostInfluenceZone != null)
            {
                Object.Destroy(ghostInfluenceZone);
                ghostInfluenceZone = null;
            }

            var sim = GameBootstrapper.Instance?.Simulation;

            // French landmark influence zone
            if (type == BuildingType.Landmark && sim != null && sim.GetPlayerCivilization(LocalPlayerId) == Civilization.French)
            {
                int lmInfluenceRadius = config.LandmarkInfluenceRadius;
                int lmFootW = config.LandmarkFootprintWidth;
                int lmFootH = config.LandmarkFootprintHeight;
                float lmHalfX = (lmFootW + 2 * lmInfluenceRadius) * 0.5f;
                float lmHalfZ = (lmFootH + 2 * lmInfluenceRadius) * 0.5f;

                ghostInfluenceZone = new GameObject("GhostInfluenceZone");
                var lmLr = ghostInfluenceZone.AddComponent<LineRenderer>();
                lmLr.useWorldSpace = false;
                lmLr.loop = true;
                lmLr.positionCount = 4;
                lmLr.SetPosition(0, new Vector3(-lmHalfX, 0f, -lmHalfZ));
                lmLr.SetPosition(1, new Vector3(lmHalfX, 0f, -lmHalfZ));
                lmLr.SetPosition(2, new Vector3(lmHalfX, 0f, lmHalfZ));
                lmLr.SetPosition(3, new Vector3(-lmHalfX, 0f, lmHalfZ));
                lmLr.startWidth = 0.08f;
                lmLr.endWidth = 0.08f;
                var lmZoneMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                lmZoneMat.color = new Color(1f, 0.6f, 0f, 0.8f);
                lmZoneMat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                lmZoneMat.renderQueue = 3000;
                lmLr.material = lmZoneMat;
                lmLr.startColor = new Color(1f, 0.6f, 0f, 0.8f);
                lmLr.endColor = new Color(1f, 0.6f, 0f, 0.8f);
                return;
            }

            BuildingType influenceBuildingType = sim != null ? sim.GetInfluenceBuildingType(LocalPlayerId) : BuildingType.Mill;
            if (type != influenceBuildingType) return;

            int influenceRadius = config.MillInfluenceRadius;
            int footprintW = influenceBuildingType == BuildingType.TownCenter ? config.TownCenterFootprintWidth : config.MillFootprintWidth;
            int footprintH = influenceBuildingType == BuildingType.TownCenter ? config.TownCenterFootprintHeight : config.MillFootprintHeight;
            float halfX = (footprintW + 2 * influenceRadius) * 0.5f;
            float halfZ = (footprintH + 2 * influenceRadius) * 0.5f;

            ghostInfluenceZone = new GameObject("GhostInfluenceZone");
            var lr = ghostInfluenceZone.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 4;
            lr.SetPosition(0, new Vector3(-halfX, 0f, -halfZ));
            lr.SetPosition(1, new Vector3(halfX, 0f, -halfZ));
            lr.SetPosition(2, new Vector3(halfX, 0f, halfZ));
            lr.SetPosition(3, new Vector3(-halfX, 0f, halfZ));
            lr.startWidth = 0.08f;
            lr.endWidth = 0.08f;
            var zoneMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            zoneMat.color = new Color(1f, 0.6f, 0f, 0.8f);
            zoneMat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            zoneMat.renderQueue = 3000;
            lr.material = zoneMat;
            lr.startColor = new Color(1f, 0.6f, 0f, 0.8f);
            lr.endColor = new Color(1f, 0.6f, 0f, 0.8f);
        }

        private void CreateGhostInfluenceZonesForExistingBuildings(BuildingType type, GameSimulation sim)
        {
            if (ghostInfluenceZones != null)
            {
                for (int i = 0; i < ghostInfluenceZones.Count; i++)
                    Object.Destroy(ghostInfluenceZones[i]);
                ghostInfluenceZones.Clear();
            }

            ghostInfluenceZones = new List<GameObject>();
            var config = sim.Config;
            var buildings = sim.BuildingRegistry.GetAllBuildings();
            bool isFrench = sim.GetPlayerCivilization(LocalPlayerId) == Civilization.French;

            // Show mill/TC influence zones when placing farms
            if (type == BuildingType.Farm)
            {
                BuildingType influenceBuildingType = sim.GetInfluenceBuildingType(LocalPlayerId);
                int influenceRadius = config.MillInfluenceRadius;
                int footprintW = influenceBuildingType == BuildingType.TownCenter ? config.TownCenterFootprintWidth : config.MillFootprintWidth;
                int footprintH = influenceBuildingType == BuildingType.TownCenter ? config.TownCenterFootprintHeight : config.MillFootprintHeight;
                float halfX = (footprintW + 2 * influenceRadius) * 0.5f;
                float halfZ = (footprintH + 2 * influenceRadius) * 0.5f;

                for (int i = 0; i < buildings.Count; i++)
                {
                    var b = buildings[i];
                    if (b.Type != influenceBuildingType) continue;
                    if (b.PlayerId != LocalPlayerId) continue;
                    if (b.IsDestroyed) continue;

                    float centerX = b.OriginTileX + footprintW * 0.5f;
                    float centerZ = b.OriginTileZ + footprintH * 0.5f;
                    float centerY = sim.MapData.SampleHeight(centerX, centerZ) * config.TerrainHeightScale + 0.05f;

                    var zone = new GameObject("GhostMillInfluenceZone");
                    zone.transform.position = new Vector3(centerX, centerY, centerZ);
                    var lr = zone.AddComponent<LineRenderer>();
                    lr.useWorldSpace = false;
                    lr.loop = true;
                    lr.positionCount = 4;
                    lr.SetPosition(0, new Vector3(-halfX, 0f, -halfZ));
                    lr.SetPosition(1, new Vector3(halfX, 0f, -halfZ));
                    lr.SetPosition(2, new Vector3(halfX, 0f, halfZ));
                    lr.SetPosition(3, new Vector3(-halfX, 0f, halfZ));
                    lr.startWidth = 0.08f;
                    lr.endWidth = 0.08f;
                    var zoneMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    zoneMat.color = new Color(1f, 0.6f, 0f, 0.8f);
                    zoneMat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                    zoneMat.renderQueue = 3000;
                    lr.material = zoneMat;
                    lr.startColor = new Color(1f, 0.6f, 0f, 0.8f);
                    lr.endColor = new Color(1f, 0.6f, 0f, 0.8f);

                    ghostInfluenceZones.Add(zone);
                }
            }

            // Show French landmark influence zones when placing any building
            if (isFrench && type != BuildingType.Landmark)
            {
                int lmRadius = config.LandmarkInfluenceRadius;
                for (int i = 0; i < buildings.Count; i++)
                {
                    var b = buildings[i];
                    if (b.Type != BuildingType.Landmark) continue;
                    if (b.PlayerId != LocalPlayerId) continue;
                    if (b.IsDestroyed) continue;
                    if (LandmarkDefinitions.Get(b.LandmarkId).Civ != Civilization.French) continue;

                    int lmFootW = b.TileFootprintWidth;
                    int lmFootH = b.TileFootprintHeight;
                    float lmHalfX = (lmFootW + 2 * lmRadius) * 0.5f;
                    float lmHalfZ = (lmFootH + 2 * lmRadius) * 0.5f;
                    float centerX = b.OriginTileX + lmFootW * 0.5f;
                    float centerZ = b.OriginTileZ + lmFootH * 0.5f;
                    float centerY = sim.MapData.SampleHeight(centerX, centerZ) * config.TerrainHeightScale + 0.05f;

                    var zone = new GameObject("GhostLandmarkInfluenceZone");
                    zone.transform.position = new Vector3(centerX, centerY, centerZ);
                    var lr = zone.AddComponent<LineRenderer>();
                    lr.useWorldSpace = false;
                    lr.loop = true;
                    lr.positionCount = 4;
                    lr.SetPosition(0, new Vector3(-lmHalfX, 0f, -lmHalfZ));
                    lr.SetPosition(1, new Vector3(lmHalfX, 0f, -lmHalfZ));
                    lr.SetPosition(2, new Vector3(lmHalfX, 0f, lmHalfZ));
                    lr.SetPosition(3, new Vector3(-lmHalfX, 0f, lmHalfZ));
                    lr.startWidth = 0.08f;
                    lr.endWidth = 0.08f;
                    var zoneMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    zoneMat.color = new Color(1f, 0.6f, 0f, 0.8f);
                    zoneMat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                    zoneMat.renderQueue = 3000;
                    lr.material = zoneMat;
                    lr.startColor = new Color(1f, 0.6f, 0f, 0.8f);
                    lr.endColor = new Color(1f, 0.6f, 0f, 0.8f);

                    ghostInfluenceZones.Add(zone);
                }
            }
        }

        private bool IsFarmInAnyInfluenceZone(GameSimulation sim, int farmTileX, int farmTileZ, int farmW, int farmH)
        {
            var config = sim.Config;
            int influenceRadius = config.MillInfluenceRadius;
            BuildingType influenceBuildingType = sim.GetInfluenceBuildingType(LocalPlayerId);
            int millW = influenceBuildingType == BuildingType.TownCenter ? config.TownCenterFootprintWidth : config.MillFootprintWidth;
            int millH = influenceBuildingType == BuildingType.TownCenter ? config.TownCenterFootprintHeight : config.MillFootprintHeight;
            var buildings = sim.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (b.Type != influenceBuildingType) continue;
                if (b.PlayerId != LocalPlayerId) continue;
                if (b.IsDestroyed) continue;

                int zoneMinX = b.OriginTileX - influenceRadius;
                int zoneMaxX = b.OriginTileX + millW + influenceRadius;
                int zoneMinZ = b.OriginTileZ - influenceRadius;
                int zoneMaxZ = b.OriginTileZ + millH + influenceRadius;

                if (farmTileX + farmW > zoneMinX && farmTileX < zoneMaxX
                    && farmTileZ + farmH > zoneMinZ && farmTileZ < zoneMaxZ)
                    return true;
            }
            return false;
        }

        private static bool IsUnitProducerType(BuildingType type)
        {
            return type == BuildingType.Barracks
                || type == BuildingType.TownCenter
                || type == BuildingType.ArcheryRange
                || type == BuildingType.Stables
                || type == BuildingType.Monastery;
        }

        private bool IsGhostInFrenchLandmarkInfluence(GameSimulation sim, int tileX, int tileZ, int footW, int footH)
        {
            int influenceRadius = sim.Config.LandmarkInfluenceRadius;
            var buildings = sim.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (b.Type != BuildingType.Landmark) continue;
                if (b.PlayerId != LocalPlayerId) continue;
                if (b.IsDestroyed) continue;
                if (LandmarkDefinitions.Get(b.LandmarkId).Civ != Civilization.French) continue;

                int minX = b.OriginTileX - influenceRadius;
                int maxX = b.OriginTileX + b.TileFootprintWidth + influenceRadius;
                int minZ = b.OriginTileZ - influenceRadius;
                int maxZ = b.OriginTileZ + b.TileFootprintHeight + influenceRadius;

                if (tileX + footW > minX && tileX < maxX &&
                    tileZ + footH > minZ && tileZ < maxZ)
                    return true;
            }
            return false;
        }

        private void CreateGhostInfluenceIcon()
        {
            WorldOverlayCanvas.EnsureCreated();
            ghostInfluenceIcon = new GameObject("GhostInfluenceIcon");
            ghostInfluenceIcon.transform.SetParent(WorldOverlayCanvas.Instance.transform, false);
            var rt = ghostInfluenceIcon.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(22f, 22f);
            var img = ghostInfluenceIcon.AddComponent<Image>();
            img.color = new Color(1f, 0.85f, 0f);
            var plusGO = new GameObject("PlusText");
            plusGO.transform.SetParent(ghostInfluenceIcon.transform, false);
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
        }

        private void ClearGhostInfluenceBuildingMarks()
        {
            for (int i = 0; i < markedInfluenceBuildingViews.Count; i++)
            {
                if (markedInfluenceBuildingViews[i] != null)
                    markedInfluenceBuildingViews[i].SetExternalInfluenceMark(false);
            }
            markedInfluenceBuildingViews.Clear();
        }

        private void UpdateGhostInfluenceBuildingMarks(int tileX, int tileZ, GameSimulation sim)
        {
            // Clear previous marks
            ClearGhostInfluenceBuildingMarks();

            var config = sim.Config;
            bool isFrench = sim.GetPlayerCivilization(LocalPlayerId) == Civilization.French;

            // Determine if the building being placed is an influence source and what it affects
            bool affectsUnitProducers = false;
            bool affectsFarms = false;
            int influenceRadius;
            int footW, footH;

            if (placementBuildingType == BuildingType.Landmark && isFrench)
            {
                affectsUnitProducers = true;
                influenceRadius = config.LandmarkInfluenceRadius;
                footW = config.LandmarkFootprintWidth;
                footH = config.LandmarkFootprintHeight;
            }
            else
            {
                BuildingType influenceBuildingType = sim.GetInfluenceBuildingType(LocalPlayerId);
                if (placementBuildingType != influenceBuildingType) return;
                affectsFarms = true;
                influenceRadius = config.MillInfluenceRadius;
                footW = influenceBuildingType == BuildingType.TownCenter ? config.TownCenterFootprintWidth : config.MillFootprintWidth;
                footH = influenceBuildingType == BuildingType.TownCenter ? config.TownCenterFootprintHeight : config.MillFootprintHeight;
            }

            // Compute influence zone AABB
            int zoneMinX = tileX - influenceRadius;
            int zoneMaxX = tileX + footW + influenceRadius;
            int zoneMinZ = tileZ - influenceRadius;
            int zoneMaxZ = tileZ + footH + influenceRadius;

            // Check all local player's buildings
            foreach (var kvp in buildingViews)
            {
                var bv = kvp.Value;
                if (bv == null || bv.IsDestroyed) continue;
                if (bv.PlayerId != LocalPlayerId) continue;

                bool eligible = false;
                if (affectsUnitProducers)
                {
                    eligible = bv.BuildingType == BuildingType.Barracks
                        || bv.BuildingType == BuildingType.TownCenter
                        || bv.BuildingType == BuildingType.ArcheryRange
                        || bv.BuildingType == BuildingType.Stables
                        || bv.BuildingType == BuildingType.Monastery;
                }
                else if (affectsFarms)
                {
                    eligible = bv.BuildingType == BuildingType.Farm;
                }

                if (!eligible) continue;

                // AABB overlap check
                var bData = sim.BuildingRegistry.GetBuilding(kvp.Key);
                if (bData == null || bData.IsDestroyed) continue;
                int bMaxX = bData.OriginTileX + bData.TileFootprintWidth;
                int bMaxZ = bData.OriginTileZ + bData.TileFootprintHeight;
                if (bMaxX > zoneMinX && bData.OriginTileX < zoneMaxX &&
                    bMaxZ > zoneMinZ && bData.OriginTileZ < zoneMaxZ)
                {
                    bv.SetExternalInfluenceMark(true);
                    markedInfluenceBuildingViews.Add(bv);
                }
            }
        }

        private void RefreshGridTexture(MapData mapData, FogOfWarData fogData, int playerId, BuildingType buildingType = BuildingType.House)
        {
            if (gridTexture == null) return;
            int w = mapData.Width, h = mapData.Height;
            var pixels = gridTexture.GetPixels32();
            Color32 buildable   = new Color32(0, 180, 0, 40);
            Color32 unbuildable = new Color32(200, 0, 0, 80);
            Color32 transparent = new Color32(0, 0, 0, 0);
            bool isFarm = buildingType == BuildingType.Farm;
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                    pixels[z * w + x] = fogData.GetVisibility(playerId, x, z) != TileVisibility.Unexplored
                        ? ((isFarm ? mapData.IsBuildableForFarm(x, z) : mapData.IsBuildable(x, z)) ? buildable : unbuildable)
                        : transparent;
            gridTexture.SetPixels32(pixels);
            gridTexture.Apply();
        }

        private void CreateGridOverlay(MapData mapData, float heightScale)
        {
            int w = mapData.Width, h = mapData.Height;

            // --- Texture: 1 pixel per tile ---
            gridTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            gridTexture.filterMode = FilterMode.Point;
            gridTexture.wrapMode = TextureWrapMode.Clamp;

            var pixels = new Color32[w * h];
            Color32 buildable   = new Color32(0, 180, 0, 40);
            Color32 unbuildable = new Color32(200, 0, 0, 80);
            Color32 transparent = new Color32(0, 0, 0, 0);
            var sim = GameBootstrapper.Instance?.Simulation;
            var fogData = sim?.FogOfWar;
            int playerId = LocalPlayerId;
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                    pixels[z * w + x] = (fogData != null && fogData.GetVisibility(playerId, x, z) != TileVisibility.Unexplored)
                        ? (mapData.IsBuildable(x, z) ? buildable : unbuildable)
                        : transparent;

            gridTexture.SetPixels32(pixels);
            gridTexture.Apply();

            // --- Mesh: inset quads per tile ---
            int tileCount = w * h;
            var verts   = new Vector3[tileCount * 4];
            var uvs     = new Vector2[tileCount * 4];
            var indices = new int[tileCount * 6];
            float inset = 0.06f;
            float yOff  = 0.08f;

            int vi = 0, ii = 0;
            for (int z = 0; z < h; z++)
            {
                for (int x = 0; x < w; x++)
                {
                    float x0 = x + inset, x1 = x + 1 - inset;
                    float z0 = z + inset, z1 = z + 1 - inset;

                    verts[vi + 0] = new Vector3(x0, mapData.SampleHeight(x0, z0) * heightScale + yOff, z0);
                    verts[vi + 1] = new Vector3(x1, mapData.SampleHeight(x1, z0) * heightScale + yOff, z0);
                    verts[vi + 2] = new Vector3(x0, mapData.SampleHeight(x0, z1) * heightScale + yOff, z1);
                    verts[vi + 3] = new Vector3(x1, mapData.SampleHeight(x1, z1) * heightScale + yOff, z1);

                    float u = (x + 0.5f) / w;
                    float v = (z + 0.5f) / h;
                    uvs[vi + 0] = uvs[vi + 1] = uvs[vi + 2] = uvs[vi + 3] = new Vector2(u, v);

                    indices[ii++] = vi;     indices[ii++] = vi + 2; indices[ii++] = vi + 1;
                    indices[ii++] = vi + 1; indices[ii++] = vi + 2; indices[ii++] = vi + 3;
                    vi += 4;
                }
            }

            var mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);

            // --- Material: URP/Unlit transparent ---
            gridMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            gridMaterial.mainTexture = gridTexture;
            gridMaterial.SetFloat("_Surface", 1);
            gridMaterial.SetOverrideTag("RenderType", "Transparent");
            gridMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            gridMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            gridMaterial.SetInt("_ZWrite", 0);
            gridMaterial.DisableKeyword("_ALPHATEST_ON");
            gridMaterial.EnableKeyword("_ALPHABLEND_ON");
            gridMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            gridMaterial.renderQueue = 3000;

            // --- GameObject ---
            gridOverlay = new GameObject("PlacementGrid");
            var mf = gridOverlay.AddComponent<MeshFilter>();
            mf.mesh = mesh;
            var mr = gridOverlay.AddComponent<MeshRenderer>();
            mr.sharedMaterial = gridMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        public void CancelBuildPlacement()
        {
            isPlacingBuilding = false;
            placementIsShiftQueued = false;
            placementVillagerIds = null;
            ClearGhostInfluenceBuildingMarks();
            if (ghostInfluenceIcon != null)
            {
                Object.Destroy(ghostInfluenceIcon);
                ghostInfluenceIcon = null;
            }
            if (ghostBuilding != null)
            {
                Object.Destroy(ghostBuilding);
                ghostBuilding = null;
            }
            if (ghostAttackRangeRing != null)
            {
                Object.Destroy(ghostAttackRangeRing);
                ghostAttackRangeRing = null;
            }
            if (ghostInfluenceZone != null)
            {
                Object.Destroy(ghostInfluenceZone);
                ghostInfluenceZone = null;
            }
            if (ghostInfluenceZones != null)
            {
                for (int i = 0; i < ghostInfluenceZones.Count; i++)
                    Object.Destroy(ghostInfluenceZones[i]);
                ghostInfluenceZones.Clear();
                ghostInfluenceZones = null;
            }
            if (gridOverlay != null) { Object.Destroy(gridOverlay); gridOverlay = null; }
            if (gridMaterial != null) { Object.Destroy(gridMaterial); gridMaterial = null; }
            if (gridTexture != null) { Object.Destroy(gridTexture); gridTexture = null; }
        }

        // Public methods for god power button clicks (from GodPowerBarUI)
        public void ActivateMeteorTargeting()
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null || sim.GetMeteorCooldownRemaining(LocalPlayerId) != 0) return;
            CancelAllGodPowerTargeting();
            isMeteorTargeting = true;
            attackMoveMode = false;
            garrisonMode = false;
            patrolMode = false;
            CreateMeteorTargetingPreview(sim.Config.MeteorRadius);
        }

        public void ActivateHealingRainTargeting()
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null || sim.GetHealingRainCooldownRemaining(LocalPlayerId) != 0) return;
            CancelAllGodPowerTargeting();
            isHealingRainTargeting = true;
            attackMoveMode = false;
            garrisonMode = false;
            patrolMode = false;
            CreateGodPowerTargetingPreview(ref healingRainTargetingPreview, "HealingRainPreview",
                sim.Config.HealingRainRadius, new Color(0.1f, 0.8f, 0.2f, 0.3f));
        }

        public void ActivateLightningStormTargeting()
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null || sim.GetLightningStormCooldownRemaining(LocalPlayerId) != 0) return;
            CancelAllGodPowerTargeting();
            isLightningStormTargeting = true;
            attackMoveMode = false;
            garrisonMode = false;
            patrolMode = false;
            CreateGodPowerTargetingPreview(ref lightningStormTargetingPreview, "LightningStormPreview",
                sim.Config.LightningStormRadius, new Color(0.6f, 0.4f, 1f, 0.3f));
        }

        public void ActivateTsunamiTargeting()
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null || sim.GetTsunamiCooldownRemaining(LocalPlayerId) != 0) return;
            CancelAllGodPowerTargeting();
            isTsunamiTargeting = true;
            tsunamiDragging = false;
            attackMoveMode = false;
            garrisonMode = false;
            patrolMode = false;
            CreateTsunamiTargetingPreview(sim.Config.TsunamiWidth, sim.Config.TsunamiLength);
        }

        private void CancelAllGodPowerTargeting()
        {
            isMeteorTargeting = false;
            isHealingRainTargeting = false;
            isLightningStormTargeting = false;
            isTsunamiTargeting = false;
            DestroyMeteorTargetingPreview();
            DestroyGodPowerPreview(ref healingRainTargetingPreview);
            DestroyGodPowerPreview(ref lightningStormTargetingPreview);
            DestroyGodPowerPreview(ref tsunamiTargetingPreview);
        }

        private void CreateMeteorTargetingPreview(float radius)
        {
            DestroyMeteorTargetingPreview();
            meteorTargetingPreview = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            meteorTargetingPreview.name = "MeteorTargetingPreview";
            meteorTargetingPreview.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);

            var collider = meteorTargetingPreview.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);

            var renderer = meteorTargetingPreview.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mat.color = new Color(1f, 0.3f, 0.1f, 0.3f);
                mat.SetFloat("_Surface", 1);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                renderer.material = mat;
            }
        }

        private void DestroyMeteorTargetingPreview()
        {
            if (meteorTargetingPreview != null)
            {
                Object.Destroy(meteorTargetingPreview);
                meteorTargetingPreview = null;
            }
        }

        private void CreateGodPowerTargetingPreview(ref GameObject preview, string name, float radius, Color color)
        {
            DestroyGodPowerPreview(ref preview);
            preview = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            preview.name = name;
            preview.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);

            var collider = preview.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);

            var renderer = preview.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mat.color = color;
                mat.SetFloat("_Surface", 1);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                renderer.material = mat;
            }
        }

        private void DestroyGodPowerPreview(ref GameObject preview)
        {
            if (preview != null)
            {
                Object.Destroy(preview);
                preview = null;
            }
        }

        private void CreateTsunamiTargetingPreview(float width, float length)
        {
            DestroyTsunamiTargetingPreview();

            // Rectangular preview — follows cursor before click, then orients during drag
            tsunamiTargetingPreview = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tsunamiTargetingPreview.name = "TsunamiPreview";
            tsunamiTargetingPreview.transform.localScale = new Vector3(width, 0.02f, length);

            var collider = tsunamiTargetingPreview.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);

            var renderer = tsunamiTargetingPreview.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mat.color = new Color(0.1f, 0.3f, 1f, 0.25f);
                mat.SetFloat("_Surface", 1);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                renderer.material = mat;
            }

            // Arrow indicator — hidden until drag starts
            tsunamiArrowPreview = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tsunamiArrowPreview.name = "TsunamiArrow";
            tsunamiArrowPreview.transform.localScale = new Vector3(2f, 1f, 3f);

            var arrowCol = tsunamiArrowPreview.GetComponent<Collider>();
            if (arrowCol != null) Object.Destroy(arrowCol);

            var arrowRend = tsunamiArrowPreview.GetComponent<Renderer>();
            if (arrowRend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mat.color = new Color(0.2f, 0.5f, 1f, 0.6f);
                mat.SetFloat("_Surface", 1);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                arrowRend.material = mat;
            }

            // Hide arrow until dragging begins
            tsunamiArrowPreview.SetActive(false);
        }

        private void DestroyTsunamiTargetingPreview()
        {
            if (tsunamiTargetingPreview != null)
            {
                Object.Destroy(tsunamiTargetingPreview);
                tsunamiTargetingPreview = null;
            }
            if (tsunamiArrowPreview != null)
            {
                Object.Destroy(tsunamiArrowPreview);
                tsunamiArrowPreview = null;
            }
        }

        private float GetTerrainHeightForPreview(Vector3 pos)
        {
            if (Physics.Raycast(new Vector3(pos.x, 100f, pos.z), Vector3.down, out RaycastHit hit, 200f, groundLayer))
                return hit.point.y;
            return 0f;
        }

        public void EnterWallPlacement(BuildingType wallType = BuildingType.Wall, bool isGate = false)
        {
            if (isPlacingBuilding) CancelBuildPlacement();
            if (isPlacingWall) CancelWallPlacement();

            isPlacingWall = true;
            wallDragging = false;
            wallPlacementType = wallType;
            wallPlacementIsGate = isGate;

            // Capture villager IDs
            var villagerIds = new List<int>();
            for (int i = 0; i < selectedUnits.Count; i++)
            {
                if (selectedUnits[i].UnitType == 0 && selectedUnits[i].PlayerId == LocalPlayerId)
                    villagerIds.Add(selectedUnits[i].UnitId);
            }
            wallVillagerIds = villagerIds.Count > 0 ? villagerIds.ToArray() : null;

            // Ensure ghost materials exist
            if (ghostValidMaterial == null)
            {
                ghostValidMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                ghostValidMaterial.color = new Color(0f, 1f, 0f, 0.35f);
                ghostValidMaterial.SetFloat("_Surface", 1);
                ghostValidMaterial.SetOverrideTag("RenderType", "Transparent");
                ghostValidMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                ghostValidMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                ghostValidMaterial.SetInt("_ZWrite", 0);
                ghostValidMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                ghostValidMaterial.EnableKeyword("_ALPHABLEND_ON");
                ghostValidMaterial.renderQueue = 3000;
            }
            if (ghostInvalidMaterial == null)
            {
                ghostInvalidMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                ghostInvalidMaterial.color = new Color(1f, 0f, 0f, 0.35f);
                ghostInvalidMaterial.SetFloat("_Surface", 1);
                ghostInvalidMaterial.SetOverrideTag("RenderType", "Transparent");
                ghostInvalidMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                ghostInvalidMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                ghostInvalidMaterial.SetInt("_ZWrite", 0);
                ghostInvalidMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                ghostInvalidMaterial.EnableKeyword("_ALPHABLEND_ON");
                ghostInvalidMaterial.renderQueue = 3000;
            }
        }

        private void ConfirmWallPlacement()
        {
            int endTileX = wallStartTileX;
            int endTileZ = wallStartTileZ;

            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim != null)
            {
                Ray wallRay = mainCamera.ScreenPointToRay(currentMousePos);
                if (Physics.Raycast(wallRay, out RaycastHit wallHit, 1000f, groundLayer))
                {
                    endTileX = Mathf.FloorToInt(wallHit.point.x);
                    endTileZ = Mathf.FloorToInt(wallHit.point.z);

                    var tiles = WallLineHelper.ComputeWallLine(wallStartTileX, wallStartTileZ, endTileX, endTileZ);
                    int validCount = 0;
                    for (int i = 0; i < tiles.Count; i++)
                        if (sim.MapData.IsBuildable(tiles[i].x, tiles[i].y))
                            validCount++;

                    if (validCount > 0)
                    {
                        int woodPerSeg = sim.GetBuildingWoodCost(wallPlacementType);
                        int stonePerSeg = sim.GetBuildingStoneCost(wallPlacementType);
                        int totalWood = validCount * woodPerSeg;
                        int totalStone = validCount * stonePerSeg;
                        var resources = sim.ResourceManager.GetPlayerResources(LocalPlayerId);
                        if (resources.Wood >= totalWood && resources.Stone >= totalStone)
                        {
                            var wallCmd = new PlaceWallCommand(LocalPlayerId,
                                wallStartTileX, wallStartTileZ, endTileX, endTileZ, wallVillagerIds,
                                wallPlacementType, wallPlacementIsGate);
                            wallCmd.IsQueued = multiSelectHeld;
                            sim.CommandBuffer.EnqueueCommand(wallCmd);
                        }
                    }
                }
            }

            if (multiSelectHeld)
            {
                // Chain: end point becomes next start point (AoE4-style)
                wallStartTileX = endTileX;
                wallStartTileZ = endTileZ;
            }
            else
            {
                CancelWallPlacement();
            }
        }

        public void CancelWallPlacement()
        {
            isPlacingWall = false;
            wallDragging = false;
            wallVillagerIds = null;
            HideWallGhosts();
        }

        private void HideWallGhosts()
        {
            for (int i = 0; i < wallGhostActiveCount; i++)
                wallGhostPool[i].SetActive(false);
            wallGhostActiveCount = 0;
        }

        private GameObject GetOrCreateWallGhost(int index)
        {
            while (wallGhostPool.Count <= index)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "WallGhost";
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                go.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
                go.SetActive(false);
                wallGhostPool.Add(go);
            }
            return wallGhostPool[index];
        }

        private bool CanAffordBuilding(GameSimulation sim)
        {
            if (placementBuildingType == BuildingType.Landmark)
            {
                var def = LandmarkDefinitions.Get(placementLandmarkId);
                var res = sim.ResourceManager.GetPlayerResources(LocalPlayerId);
                return res.Food >= def.FoodCost && res.Gold >= def.GoldCost;
            }
            int cost = sim.GetBuildingWoodCost(placementBuildingType);
            int stoneCost = sim.GetBuildingStoneCost(placementBuildingType);
            var resources = sim.ResourceManager.GetPlayerResources(LocalPlayerId);
            return resources.Wood >= cost && resources.Stone >= stoneCost;
        }

        private void GetBuildingFootprint(SimulationConfig config, BuildingType type, out int w, out int h)
        {
            switch (type)
            {
                case BuildingType.Barracks:
                    w = config.BarracksFootprintWidth;
                    h = config.BarracksFootprintHeight;
                    break;
                case BuildingType.TownCenter:
                    w = config.TownCenterFootprintWidth;
                    h = config.TownCenterFootprintHeight;
                    break;
                case BuildingType.Wall:
                    w = config.WallFootprintWidth;
                    h = config.WallFootprintHeight;
                    break;
                case BuildingType.Mill:
                    w = config.MillFootprintWidth;
                    h = config.MillFootprintHeight;
                    break;
                case BuildingType.LumberYard:
                    w = config.LumberYardFootprintWidth;
                    h = config.LumberYardFootprintHeight;
                    break;
                case BuildingType.Mine:
                    w = config.MineFootprintWidth;
                    h = config.MineFootprintHeight;
                    break;
                case BuildingType.ArcheryRange:
                    w = config.ArcheryRangeFootprintWidth;
                    h = config.ArcheryRangeFootprintHeight;
                    break;
                case BuildingType.Stables:
                    w = config.StablesFootprintWidth;
                    h = config.StablesFootprintHeight;
                    break;
                case BuildingType.Farm:
                    w = config.FarmFootprintWidth;
                    h = config.FarmFootprintHeight;
                    break;
                case BuildingType.Tower:
                    w = config.TowerFootprintWidth;
                    h = config.TowerFootprintHeight;
                    break;
                case BuildingType.Monastery:
                    w = config.MonasteryFootprintWidth;
                    h = config.MonasteryFootprintHeight;
                    break;
                case BuildingType.Blacksmith:
                    w = config.BlacksmithFootprintWidth;
                    h = config.BlacksmithFootprintHeight;
                    break;
                case BuildingType.Market:
                    w = config.MarketFootprintWidth;
                    h = config.MarketFootprintHeight;
                    break;
                case BuildingType.University:
                    w = config.UniversityFootprintWidth;
                    h = config.UniversityFootprintHeight;
                    break;
                case BuildingType.SiegeWorkshop:
                    w = config.SiegeWorkshopFootprintWidth;
                    h = config.SiegeWorkshopFootprintHeight;
                    break;
                case BuildingType.Keep:
                    w = config.KeepFootprintWidth;
                    h = config.KeepFootprintHeight;
                    break;
                case BuildingType.StoneWall:
                    w = config.StoneWallFootprintWidth;
                    h = config.StoneWallFootprintHeight;
                    break;
                case BuildingType.StoneGate:
                    w = config.StoneGateFootprintWidth;
                    h = config.StoneGateFootprintHeight;
                    break;
                case BuildingType.WoodGate:
                    w = config.WoodGateFootprintWidth;
                    h = config.WoodGateFootprintHeight;
                    break;
                case BuildingType.Wonder:
                    w = config.WonderFootprintWidth;
                    h = config.WonderFootprintHeight;
                    break;
                case BuildingType.Landmark:
                    var ldef = LandmarkDefinitions.Get(placementLandmarkId);
                    w = ldef.FootprintWidth;
                    h = ldef.FootprintHeight;
                    break;
                default:
                    w = config.HouseFootprintWidth;
                    h = config.HouseFootprintHeight;
                    break;
            }
        }

        private bool FindNearestValidPlacement(MapData mapData, float cursorX, float cursorZ, int w, int h, BuildingType type, int maxRadius, out int outTileX, out int outTileZ)
        {
            int centerTileX = Mathf.FloorToInt(cursorX);
            int centerTileZ = Mathf.FloorToInt(cursorZ);
            float bestDistSq = float.MaxValue;
            outTileX = centerTileX;
            outTileZ = centerTileZ;
            bool found = false;

            for (int r = 0; r <= maxRadius; r++)
            {
                // Early-terminate: minimum possible distance for this ring can't beat best
                if (found && r * r >= bestDistSq)
                    break;

                if (r == 0)
                {
                    TryCandidate(mapData, centerTileX, centerTileZ, w, h, type, cursorX, cursorZ, ref bestDistSq, ref outTileX, ref outTileZ, ref found);
                }
                else
                {
                    for (int i = -r; i <= r; i++)
                    {
                        // Top and bottom edges of the ring
                        TryCandidate(mapData, centerTileX + i, centerTileZ + r, w, h, type, cursorX, cursorZ, ref bestDistSq, ref outTileX, ref outTileZ, ref found);
                        TryCandidate(mapData, centerTileX + i, centerTileZ - r, w, h, type, cursorX, cursorZ, ref bestDistSq, ref outTileX, ref outTileZ, ref found);
                    }
                    for (int i = -r + 1; i <= r - 1; i++)
                    {
                        // Left and right edges of the ring (excluding corners already covered)
                        TryCandidate(mapData, centerTileX + r, centerTileZ + i, w, h, type, cursorX, cursorZ, ref bestDistSq, ref outTileX, ref outTileZ, ref found);
                        TryCandidate(mapData, centerTileX - r, centerTileZ + i, w, h, type, cursorX, cursorZ, ref bestDistSq, ref outTileX, ref outTileZ, ref found);
                    }
                }
            }

            return found;
        }

        private void TryCandidate(MapData mapData, int cx, int cz, int w, int h, BuildingType type, float cursorX, float cursorZ, ref float bestDistSq, ref int outTileX, ref int outTileZ, ref bool found)
        {
            if (!IsPlacementValid(mapData, cx, cz, w, h, type))
                return;
            float dx = (cx + w * 0.5f) - cursorX;
            float dz = (cz + h * 0.5f) - cursorZ;
            float distSq = dx * dx + dz * dz;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                outTileX = cx;
                outTileZ = cz;
                found = true;
            }
        }

        private bool IsPlacementValid(MapData mapData, int tileX, int tileZ, int w, int h, BuildingType type = BuildingType.House)
        {
            int border = (type == BuildingType.Wall || type == BuildingType.Farm || type == BuildingType.StoneWall || type == BuildingType.StoneGate || type == BuildingType.WoodGate) ? 0 : 1;
            bool isFarm = type == BuildingType.Farm;
            for (int x = tileX - border; x < tileX + w + border; x++)
                for (int z = tileZ - border; z < tileZ + h + border; z++)
                    if (isFarm ? !mapData.IsBuildableForFarm(x, z) : !mapData.IsBuildable(x, z)) return false;
            return true;
        }

        public void DeselectAll()
        {
            foreach (var unit in selectedUnits)
                unit.SetSelected(false);
            selectedUnits.Clear();
        }

        public void DeselectBuilding()
        {
            for (int i = 0; i < selectedBuildings.Count; i++)
                selectedBuildings[i].SetSelected(false);
            selectedBuildings.Clear();
            activeTabBuildingType = null;
        }

        public void DeselectResourceNode()
        {
            if (selectedResourceNode != null)
            {
                selectedResourceNode.SetSelected(false);
                selectedResourceNode = null;
            }
        }

        public void OnBuildingDestroyed(int buildingId)
        {
            for (int i = selectedBuildings.Count - 1; i >= 0; i--)
            {
                if (selectedBuildings[i].BuildingId == buildingId)
                {
                    selectedBuildings[i].SetSelected(false);
                    selectedBuildings.RemoveAt(i);
                    break;
                }
            }
        }

        public void OnUnitDied(int unitId)
        {
            if (hoveredUnit != null && hoveredUnit.UnitId == unitId)
            {
                hoveredUnit.SetHovered(false);
                hoveredUnit = null;
            }

            for (int i = selectedUnits.Count - 1; i >= 0; i--)
            {
                if (selectedUnits[i].UnitId == unitId)
                {
                    selectedUnits[i].SetSelected(false);
                    selectedUnits.RemoveAt(i);
                    break;
                }
            }
        }

        public void SelectUnitById(int unitId)
        {
            if (unitViews.TryGetValue(unitId, out UnitView unitView))
            {
                DeselectAll();
                DeselectBuilding();
                DeselectResourceNode();
                
                unitView.SetSelected(true);
                selectedUnits.Add(unitView);
            }
        }

        public void SyncFromSim()
        {
            // UnitView now reads directly from UnitData via interpolation in Update()
        }
    }
}
