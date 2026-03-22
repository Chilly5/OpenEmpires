using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace OpenEmpires
{
    public class UnitInfoUI : MonoBehaviour
    {
        [SerializeField] private UnitSelectionManager selectionManager;

        // --- Layout constants ---
        private const int ActionGridCols = 4;
        private const int ActionGridRows = 3;
        private const float ActionButtonSize = 48f;
        private const float ActionButtonGap = 3f;
        private const float ActionPadding = 8f;
        private const float ActionPanelWidth = 217f;
        private const float ActionPanelHeight = 166f;
        private const float PanelGap = 6f;
        private const float StatsPanelWidth = 150f;
        private const float StatsPanelHeight = ActionPanelHeight;
        private const float ResourcePanelWidth = 130f;
        private const float Margin = 10f;
        private const float Padding = 10f;
        private const float BarHeight = 12f;

        // --- Queue panel constants ---
        private const float QueuePanelHeight = 40f;
        private const float QueueItemWidth = 28f;
        private const float QueueItemHeight = 28f;
        private const float QueueItemGap = 3f;
        private const int MaxVisibleQueueItems = 5;

        // --- Build age panel constants ---
        private const int BuildAgePanelCount = 3;
        private const int BuildButtonsPerAge = 9;
        private const int TotalBuildButtons = BuildAgePanelCount * BuildButtonsPerAge; // 27
        private const int BuildGridCols = 3;
        private const int BuildGridRows = 3;
        private const float BuildPanelHeaderHeight = 18f;
        private static readonly string[] AgeHeaders = { "Age I  [Q]", "Age II  [W]", "Age III  [E]" };

        // --- Panel references for hover detection ---
        private static RectTransform s_statsPanelRT;
        private static RectTransform s_actionPanelRT;
        private static bool s_actionPanelVisible;
        private static RectTransform[] s_buildPanelRTs;
        private static bool s_buildPanelsVisible;
        private static RectTransform s_queuePanelRT;
        private static RectTransform s_tooltipPanelRT;
        private static Camera s_uiCamera; // null for overlay canvas

        // --- Build hotkey state machine ---
        private static UnitInfoUI s_instance;
        private int buildMenuAge; // 0 = not in build mode, 1/2/3 = showing that age's buildings
        public static bool BuildHotkeysActive => s_instance != null && s_instance.buildMenuAge > 0;
        public static void DeactivateBuildHotkeys()
        {
            if (s_instance != null) s_instance.buildMenuAge = 0;
        }

        public static Rect StatsPanelRect
        {
            get { return s_statsPanelRT != null ? GetScreenRect(s_statsPanelRT) : Rect.zero; }
        }
        public static Rect ActionPanelRect
        {
            get { return s_actionPanelRT != null && s_actionPanelVisible ? GetScreenRect(s_actionPanelRT) : Rect.zero; }
        }
        public static bool ActionPanelVisible => s_actionPanelVisible;

        public static bool ContainsScreenPoint(Vector2 screenPos)
        {
            if (s_statsPanelRT != null && s_statsPanelRT.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(s_statsPanelRT, screenPos, s_uiCamera))
                return true;
            if (s_actionPanelVisible && s_actionPanelRT != null && s_actionPanelRT.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(s_actionPanelRT, screenPos, s_uiCamera))
                return true;
            if (s_buildPanelsVisible && s_buildPanelRTs != null)
            {
                for (int i = 0; i < s_buildPanelRTs.Length; i++)
                    if (s_buildPanelRTs[i] != null && s_buildPanelRTs[i].gameObject.activeInHierarchy &&
                        RectTransformUtility.RectangleContainsScreenPoint(s_buildPanelRTs[i], screenPos, s_uiCamera))
                        return true;
            }
            if (s_queuePanelRT != null && s_queuePanelRT.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(s_queuePanelRT, screenPos, s_uiCamera))
                return true;
            if (s_tooltipPanelRT != null && s_tooltipPanelRT.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(s_tooltipPanelRT, screenPos, s_uiCamera))
                return true;
            return false;
        }

        private static Rect GetScreenRect(RectTransform rt)
        {
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            // corners: 0=bottom-left, 1=top-left, 2=top-right, 3=bottom-right (in screen space for overlay)
            float x = corners[0].x;
            float y = corners[0].y;
            float w = corners[2].x - corners[0].x;
            float h = corners[2].y - corners[0].y;
            return new Rect(x, y, w, h);
        }

        private struct GridButton
        {
            public string Label;
            public string Hotkey;
            public string Tooltip;
            public bool Enabled;
            public System.Action OnClick;
            public Sprite Icon;
        }

        // --- Canvas UI references ---
        private Canvas canvas;
        private Transform canvasTransform;
        private RectTransform statsPanelRT;
        private GameObject statsPanelGO;
        private TMP_Text nameText;
        private RectTransform healthBarBgRT;
        private Image healthBarFill;
        private TMP_Text healthBarText;
        private TMP_Text[] statLines;
        private const int MaxStatLines = 6;
        private RectTransform progressBarBgRT;
        private Image progressBarFill;
        private TMP_Text progressBarText;
        private TMP_Text queueText;
        private GameObject statsIconGO;
        private Image statsIconImage;

        private GameObject actionPanelGO;
        private RectTransform actionPanelRT;
        private GameObject[] actionButtonGOs;
        private Button[] actionButtons;
        private TMP_Text[] actionButtonTexts;
        private TMP_Text[] actionButtonHotkeys;
        private Image[] actionButtonIcons;
        private Image[] actionButtonFills;

        // Landmark choice panel
        private GameObject landmarkPanelGO;
        private System.Action landmarkChoiceACallback;
        private System.Action landmarkChoiceBCallback;

        // Mouse hold-to-delete on action button
        private bool deleteMouseHolding;
        private float deleteMouseHoldTimer;
        private const float DeleteHoldDuration = 2f;
        private int deleteMouseHoldSlot = -1;

        // Button flash (hotkey press feedback)
        private float[] actionFlashTimers;
        private float[] buildFlashTimers;
        private const float ButtonFlashDuration = 0.12f;
        private static readonly Color ButtonPressedColor = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color ButtonNormalColor = new Color(0.25f, 0.25f, 0.25f);
        private static readonly Color IconPressedColor = new Color(0.6f, 0.6f, 0.6f);

        // Build panels (3 age panels, villagers only)
        private GameObject[] buildPanelGOs;
        private RectTransform[] buildPanelRTs;
        private Image[] buildPanelHeaderBgs;
        private GameObject[] buildButtonGOs;        // [TotalBuildButtons] = 24
        private Button[] buildButtons;
        private TMP_Text[] buildButtonTexts;
        private TMP_Text[] buildButtonHotkeys;
        private Image[] buildButtonIcons;
        private System.Action[] buildCallbacks;
        private string[] buildTooltips;

        // Build panel tooltip
        private GameObject buildTooltipPanelGO;
        private RectTransform buildTooltipPanelRT;
        private TMP_Text buildTooltipText;
        private int hoveredBuildIndex = -1;

        // Queue panel
        private GameObject queuePanelGO;
        private RectTransform queuePanelRT;
        private GameObject[] queueItemGOs;
        private Image[] queueItemFills;
        private Image[] queueItemIcons;
        private TMP_Text[] queueItemTexts;
        private GameObject[] queueItemXOverlays;
        private Image[] queueItemBgs;
        private int hoveredQueueIndex = -1;

        // Tooltip panel
        private GameObject tooltipPanelGO;
        private RectTransform tooltipPanelRT;
        private TMP_Text tooltipText;
        private string[] actionTooltips;
        private int hoveredActionIndex = -1;

        private struct QueueEntry
        {
            public int BuildingId;
            public int QueueIndex;
            public int UnitType;
            public float Progress;
        }

        private readonly List<QueueEntry> aggregatedQueue = new List<QueueEntry>();

        // Current action callbacks (rebuilt each frame)
        private System.Action[] actionCallbacks;

        private static readonly Color HealthColorFull = new Color(0.2f, 0.8f, 0.2f);
        private static readonly Color HealthColorEmpty = new Color(0.8f, 0.1f, 0.1f);
        private static readonly Color TrainingBarColor = new Color(0.2f, 0.4f, 0.9f);
        private static readonly Color ConstructionBarColor = new Color(0.9f, 0.6f, 0.1f);
        private static readonly Color PanelBgColor = new Color(0, 0, 0, 1f);
        private static readonly Color BarBgColor = new Color(0.1f, 0.1f, 0.1f);

        private static readonly string[] UnitTypeNames = { "Villager", "Spearman", "Archer", "Horseman", "Scout", "Sheep", "Man-at-Arms", "Knight", "Crossbowman", "Monk", "Longbowman", "Gendarme", "Landsknecht", "Battering Ram", "Mangonel", "Trebuchet" };
        private static readonly string[] UnitTypePlurals = { "Villagers", "Spearmen", "Archers", "Horsemen", "Scouts", "Sheep", "Men-at-Arms", "Knights", "Crossbowmen", "Monks", "Longbowmen", "Gendarmes", "Landsknechte", "Battering Rams", "Mangonels", "Trebuchets" };
        private static readonly string[] BuildingTypeNames = { "House", "Barracks", "Town Center", "Wood Wall", "Mill", "Lumber Yard", "Mine", "Archery Range", "Stables", "Farm", "Tower", "Monastery", "Landmark", "Blacksmith", "Market", "University", "Siege Workshop", "Keep", "Stone Wall", "Stone Gate", "Wood Gate", "Wonder" };
        private static readonly string[] BuildingTypePlurals = { "Houses", "Barracks", "Town Centers", "Wood Walls", "Mills", "Lumber Yards", "Mines", "Archery Ranges", "Stables", "Farms", "Towers", "Monasteries", "Landmarks", "Blacksmiths", "Markets", "Universities", "Siege Workshops", "Keeps", "Stone Walls", "Stone Gates", "Wood Gates", "Wonders" };
        private static readonly string[] ResourceNodeNames = { "Berry Bush", "Tree", "Gold Mine", "Stone Mine" };

        private void Awake()
        {
            s_instance = this;
            ResourceIcons.EnsureLoaded();
            BuildingIcons.EnsureLoaded();
            UnitIcons.EnsureLoaded();
            BuildUI();
            actionCallbacks = new System.Action[12];
            actionTooltips = new string[12];
            buildCallbacks = new System.Action[TotalBuildButtons];
            buildTooltips = new string[TotalBuildButtons];
            actionFlashTimers = new float[12];
            buildFlashTimers = new float[TotalBuildButtons];
        }

        // =============================================================
        //  UI Construction
        // =============================================================

        private const float ReferenceWidth = 1280f;
        private const float ReferenceHeight = 720f;

        private void BuildUI()
        {
            // EventSystem (required for button clicks)
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<InputSystemUIInputModule>();
            }

            var canvasGO = new GameObject("InfoPanelCanvas");
            canvasGO.transform.SetParent(transform);
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            canvasTransform = canvasGO.transform;
            BuildStatsPanel(canvasTransform);
            BuildActionPanel(canvasTransform);
            BuildBuildPanel(canvasTransform);
            BuildQueuePanel(canvasTransform);
        }

        private void BuildStatsPanel(Transform parent)
        {
            statsPanelGO = MakePanel(parent, "StatsPanel", StatsPanelWidth, StatsPanelHeight);
            statsPanelRT = statsPanelGO.GetComponent<RectTransform>();
            statsPanelRT.anchorMin = new Vector2(0, 0);
            statsPanelRT.anchorMax = new Vector2(0, 0);
            statsPanelRT.pivot = new Vector2(0, 0);
            var statsX = Margin + ResourcePanelWidth + PanelGap;
            statsPanelRT.anchoredPosition = new Vector2(statsX, Margin);

            float contentW = StatsPanelWidth - Padding * 2f;
            float y = StatsPanelHeight - Padding;

            // Icon (top-left of content area, 36x36)
            const float iconSize = 36f;
            const float iconGap = 6f;
            statsIconGO = new GameObject("StatsIcon");
            statsIconGO.transform.SetParent(statsPanelGO.transform, false);
            var iconRT = statsIconGO.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0, 0);
            iconRT.anchorMax = new Vector2(0, 0);
            iconRT.pivot = new Vector2(0, 0);
            iconRT.anchoredPosition = new Vector2(Padding, StatsPanelHeight - Padding - iconSize);
            iconRT.sizeDelta = new Vector2(iconSize, iconSize);
            statsIconImage = statsIconGO.AddComponent<Image>();
            statsIconImage.preserveAspect = true;
            statsIconImage.raycastTarget = false;
            statsIconGO.SetActive(false);

            // Name / header (shifted right when icon is visible)
            float nameX = Padding + iconSize + iconGap;
            float nameW = contentW - iconSize - iconGap;
            y -= 24f;
            nameText = MakeText(statsPanelGO.transform, "NameText", nameX, y, nameW, 24f, 18, FontStyles.Bold);
            y -= 4f;

            // Health bar
            y -= BarHeight;
            healthBarBgRT = MakeBar(statsPanelGO.transform, "HealthBarBg", Padding, y, contentW, BarHeight,
                BarBgColor, out healthBarFill, out healthBarText);
            y -= 10f;

            // Stat lines
            statLines = new TMP_Text[MaxStatLines];
            for (int i = 0; i < MaxStatLines; i++)
            {
                y -= 20f;
                statLines[i] = MakeText(statsPanelGO.transform, $"Stat{i}", Padding, y, contentW, 20f, 14, FontStyles.Normal);
            }

            // Progress bar (construction / training)
            y -= 8f;
            y -= BarHeight;
            progressBarBgRT = MakeBar(statsPanelGO.transform, "ProgressBarBg", Padding, y, contentW, BarHeight,
                BarBgColor, out progressBarFill, out progressBarText);
            y -= 6f;

            // Queue text
            y -= 20f;
            queueText = MakeText(statsPanelGO.transform, "QueueText", Padding, y, contentW, 20f, 14, FontStyles.Normal);

            statsPanelGO.SetActive(false);
        }

        private void BuildActionPanel(Transform parent)
        {
            actionPanelGO = MakePanel(parent, "ActionPanel", ActionPanelWidth, ActionPanelHeight);
            actionPanelRT = actionPanelGO.GetComponent<RectTransform>();
            actionPanelRT.anchorMin = new Vector2(0, 0);
            actionPanelRT.anchorMax = new Vector2(0, 0);
            actionPanelRT.pivot = new Vector2(0, 0);
            float actionX = Margin + ResourcePanelWidth + PanelGap + StatsPanelWidth + PanelGap;
            actionPanelRT.anchoredPosition = new Vector2(actionX, Margin);

            actionButtonGOs = new GameObject[12];
            actionButtons = new Button[12];
            actionButtonTexts = new TMP_Text[12];
            actionButtonIcons = new Image[12];
            actionButtonHotkeys = new TMP_Text[12];
            actionButtonFills = new Image[12];

            for (int i = 0; i < 12; i++)
            {
                int col = i % ActionGridCols;
                int row = i / ActionGridCols;
                // row 0 is top visually but in anchored coords (bottom-left), row 0 is top = highest y
                float bx = ActionPadding + col * (ActionButtonSize + ActionButtonGap);
                float by = ActionPanelHeight - ActionPadding - (row + 1) * ActionButtonSize - row * ActionButtonGap;

                var btnGO = new GameObject($"Btn{i}");
                btnGO.transform.SetParent(actionPanelGO.transform, false);
                var rt = btnGO.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(0, 0);
                rt.pivot = new Vector2(0, 0);
                rt.anchoredPosition = new Vector2(bx, by);
                rt.sizeDelta = new Vector2(ActionButtonSize, ActionButtonSize);

                var img = btnGO.AddComponent<Image>();
                img.color = Color.white;

                var btn = btnGO.AddComponent<Button>();
                var colors = btn.colors;
                colors.normalColor = new Color(0.38f, 0.38f, 0.38f);
                colors.highlightedColor = new Color(0.48f, 0.48f, 0.48f);
                colors.pressedColor = new Color(0.25f, 0.25f, 0.25f);
                colors.disabledColor = new Color(0.2f, 0.2f, 0.2f);
                btn.colors = colors;

                int idx = i;
                btn.onClick.AddListener(() => { FlashActionButton(idx); actionCallbacks[idx]?.Invoke(); });

                // Icon image (fills button with 2px padding)
                var iconGO = new GameObject("Icon");
                iconGO.transform.SetParent(btnGO.transform, false);
                var iconRT = iconGO.AddComponent<RectTransform>();
                iconRT.anchorMin = Vector2.zero;
                iconRT.anchorMax = Vector2.one;
                iconRT.sizeDelta = Vector2.zero;
                iconRT.offsetMin = new Vector2(2, 2);
                iconRT.offsetMax = new Vector2(-2, -2);
                var iconImg = iconGO.AddComponent<Image>();
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                iconGO.SetActive(false);
                actionButtonIcons[i] = iconImg;

                // Text label
                var textGO = new GameObject("Text");
                textGO.transform.SetParent(btnGO.transform, false);
                var trt = textGO.AddComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.sizeDelta = Vector2.zero;
                trt.offsetMin = new Vector2(2, 2);
                trt.offsetMax = new Vector2(-2, -2);
                var tmp = textGO.AddComponent<TextMeshProUGUI>();
                tmp.fontSize = 10;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.textWrappingMode = TextWrappingModes.Normal;
                tmp.raycastTarget = false;

                // Hotkey label (top-left corner)
                var hotkeyGO = new GameObject("Hotkey");
                hotkeyGO.transform.SetParent(btnGO.transform, false);
                var hrt = hotkeyGO.AddComponent<RectTransform>();
                hrt.anchorMin = new Vector2(0, 1);
                hrt.anchorMax = new Vector2(0, 1);
                hrt.pivot = new Vector2(0, 1);
                hrt.anchoredPosition = new Vector2(2, -1);
                hrt.sizeDelta = new Vector2(16, 14);
                var hTmp = hotkeyGO.AddComponent<TextMeshProUGUI>();
                hTmp.fontSize = 9;
                hTmp.fontStyle = FontStyles.Bold;
                hTmp.alignment = TextAlignmentOptions.TopLeft;
                hTmp.color = new Color(1f, 1f, 1f, 0.85f);
                hTmp.overflowMode = TextOverflowModes.Overflow;
                hTmp.raycastTarget = false;
                actionButtonHotkeys[i] = hTmp;

                // Delete-hold fill overlay (red, grows bottom-to-top via anchorMax.y)
                var fillGO = new GameObject("Fill");
                fillGO.transform.SetParent(btnGO.transform, false);
                var fillRT = fillGO.AddComponent<RectTransform>();
                fillRT.anchorMin = Vector2.zero;
                fillRT.anchorMax = new Vector2(1f, 0f);
                fillRT.offsetMin = Vector2.zero;
                fillRT.offsetMax = Vector2.zero;
                var fillImg = fillGO.AddComponent<Image>();
                fillImg.color = new Color(0.9f, 0.1f, 0.1f, 0.7f);
                fillImg.raycastTarget = false;
                fillGO.SetActive(false);
                actionButtonFills[i] = fillImg;

                // EventTrigger for tooltip hover
                var trigger = btnGO.AddComponent<EventTrigger>();
                var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                int hoverIdx = i;
                enterEntry.callback.AddListener((_) => ShowActionTooltip(hoverIdx));
                trigger.triggers.Add(enterEntry);
                var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exitEntry.callback.AddListener((_) => HideActionTooltip(hoverIdx));
                trigger.triggers.Add(exitEntry);
                var downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                int downIdx = i;
                downEntry.callback.AddListener((_) => OnActionButtonDown(downIdx));
                trigger.triggers.Add(downEntry);
                var upEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
                upEntry.callback.AddListener((_) => OnActionButtonUp(downIdx));
                trigger.triggers.Add(upEntry);

                actionButtonGOs[i] = btnGO;
                actionButtons[i] = btn;
                actionButtonTexts[i] = tmp;
                btnGO.SetActive(false);
            }

            // Tooltip panel (child of action panel, positioned dynamically)
            tooltipPanelGO = new GameObject("TooltipPanel");
            tooltipPanelGO.transform.SetParent(canvasTransform, false);
            tooltipPanelRT = tooltipPanelGO.AddComponent<RectTransform>();
            tooltipPanelRT.anchorMin = new Vector2(0, 0);
            tooltipPanelRT.anchorMax = new Vector2(0, 0);
            tooltipPanelRT.pivot = new Vector2(0, 0);
            tooltipPanelRT.sizeDelta = new Vector2(200f, 60f);
            var tooltipBg = tooltipPanelGO.AddComponent<Image>();
            tooltipBg.color = new Color(0, 0, 0, 1f);
            tooltipBg.raycastTarget = false;
            var tooltipOutline = tooltipPanelGO.AddComponent<Outline>();
            tooltipOutline.effectColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            tooltipOutline.effectDistance = new Vector2(2, -2);

            var tooltipTextGO = new GameObject("Text");
            tooltipTextGO.transform.SetParent(tooltipPanelGO.transform, false);
            var tooltipTextRT = tooltipTextGO.AddComponent<RectTransform>();
            tooltipTextRT.anchorMin = Vector2.zero;
            tooltipTextRT.anchorMax = Vector2.one;
            tooltipTextRT.sizeDelta = Vector2.zero;
            tooltipTextRT.offsetMin = new Vector2(8, 8);
            tooltipTextRT.offsetMax = new Vector2(-8, -8);
            tooltipText = tooltipTextGO.AddComponent<TextMeshProUGUI>();
            tooltipText.fontSize = 11;
            tooltipText.color = Color.white;
            tooltipText.alignment = TextAlignmentOptions.TopLeft;
            tooltipText.overflowMode = TextOverflowModes.Overflow;
            tooltipText.textWrappingMode = TextWrappingModes.Normal;
            tooltipText.raycastTarget = false;

            tooltipPanelGO.SetActive(false);

            actionPanelGO.SetActive(false);
        }

        private void BuildBuildPanel(Transform parent)
        {
            float buildPanelWidth = ActionPadding + BuildGridCols * (ActionButtonSize + ActionButtonGap) - ActionButtonGap + ActionPadding;
            float panelHeight = ActionPadding + BuildPanelHeaderHeight + BuildGridRows * (ActionButtonSize + ActionButtonGap) - ActionButtonGap + ActionPadding;
            float buildBaseX = Margin + ResourcePanelWidth + PanelGap + StatsPanelWidth + PanelGap + ActionPanelWidth + PanelGap;

            buildPanelGOs = new GameObject[BuildAgePanelCount];
            buildPanelRTs = new RectTransform[BuildAgePanelCount];
            buildPanelHeaderBgs = new Image[BuildAgePanelCount];
            buildButtonGOs = new GameObject[TotalBuildButtons];
            buildButtons = new Button[TotalBuildButtons];
            buildButtonTexts = new TMP_Text[TotalBuildButtons];
            buildButtonIcons = new Image[TotalBuildButtons];
            buildButtonHotkeys = new TMP_Text[TotalBuildButtons];
            s_buildPanelRTs = new RectTransform[BuildAgePanelCount];

            for (int age = 0; age < BuildAgePanelCount; age++)
            {
                float px = buildBaseX + age * (buildPanelWidth + PanelGap);
                var panelGO = MakePanel(parent, $"BuildPanel_Age{age + 1}", buildPanelWidth, panelHeight);
                var panelRT = panelGO.GetComponent<RectTransform>();
                panelRT.anchorMin = new Vector2(0, 0);
                panelRT.anchorMax = new Vector2(0, 0);
                panelRT.pivot = new Vector2(0, 0);
                panelRT.anchoredPosition = new Vector2(px, Margin);
                buildPanelGOs[age] = panelGO;
                buildPanelRTs[age] = panelRT;

                // Header background
                var headerGO = new GameObject("Header");
                headerGO.transform.SetParent(panelGO.transform, false);
                var headerRT = headerGO.AddComponent<RectTransform>();
                headerRT.anchorMin = new Vector2(0, 1);
                headerRT.anchorMax = new Vector2(1, 1);
                headerRT.pivot = new Vector2(0.5f, 1);
                headerRT.anchoredPosition = new Vector2(0, -2f);
                headerRT.sizeDelta = new Vector2(0, BuildPanelHeaderHeight);
                var headerBg = headerGO.AddComponent<Image>();
                headerBg.color = new Color(0.18f, 0.18f, 0.18f);
                headerBg.raycastTarget = false;
                buildPanelHeaderBgs[age] = headerBg;

                // Header text (child of header so Image and TMP don't conflict)
                var headerTextGO = new GameObject("HeaderText");
                headerTextGO.transform.SetParent(headerGO.transform, false);
                var headerTextRT = headerTextGO.AddComponent<RectTransform>();
                headerTextRT.anchorMin = Vector2.zero;
                headerTextRT.anchorMax = Vector2.one;
                headerTextRT.sizeDelta = Vector2.zero;
                headerTextRT.offsetMin = Vector2.zero;
                headerTextRT.offsetMax = Vector2.zero;
                var headerTmp = headerTextGO.AddComponent<TextMeshProUGUI>();
                headerTmp.text = AgeHeaders[age];
                headerTmp.fontSize = 10;
                headerTmp.fontStyle = FontStyles.Bold;
                headerTmp.alignment = TextAlignmentOptions.Center;
                headerTmp.color = Color.white;
                headerTmp.raycastTarget = false;

                // Buttons
                for (int slot = 0; slot < BuildButtonsPerAge; slot++)
                {
                    int globalIdx = age * BuildButtonsPerAge + slot;
                    int col = slot % BuildGridCols;
                    int row = slot / BuildGridCols;
                    float bx = ActionPadding + col * (ActionButtonSize + ActionButtonGap);
                    float by = panelHeight - ActionPadding - BuildPanelHeaderHeight - (row + 1) * ActionButtonSize - row * ActionButtonGap;

                    var btnGO = new GameObject($"BuildBtn{globalIdx}");
                    btnGO.transform.SetParent(panelGO.transform, false);
                    var rt = btnGO.AddComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0, 0);
                    rt.anchorMax = new Vector2(0, 0);
                    rt.pivot = new Vector2(0, 0);
                    rt.anchoredPosition = new Vector2(bx, by);
                    rt.sizeDelta = new Vector2(ActionButtonSize, ActionButtonSize);

                    var img = btnGO.AddComponent<Image>();
                    img.color = Color.white;

                    var btn = btnGO.AddComponent<Button>();
                    var colors = btn.colors;
                    colors.normalColor = new Color(0.38f, 0.38f, 0.38f);
                    colors.highlightedColor = new Color(0.48f, 0.48f, 0.48f);
                    colors.pressedColor = new Color(0.25f, 0.25f, 0.25f);
                    colors.disabledColor = new Color(0.2f, 0.2f, 0.2f);
                    btn.colors = colors;

                    int idx = globalIdx;
                    btn.onClick.AddListener(() => { FlashBuildButton(idx); buildCallbacks[idx]?.Invoke(); });

                    // Icon image
                    var iconGO = new GameObject("Icon");
                    iconGO.transform.SetParent(btnGO.transform, false);
                    var iconRT = iconGO.AddComponent<RectTransform>();
                    iconRT.anchorMin = Vector2.zero;
                    iconRT.anchorMax = Vector2.one;
                    iconRT.sizeDelta = Vector2.zero;
                    iconRT.offsetMin = new Vector2(2, 2);
                    iconRT.offsetMax = new Vector2(-2, -2);
                    var iconImg = iconGO.AddComponent<Image>();
                    iconImg.preserveAspect = true;
                    iconImg.raycastTarget = false;
                    iconGO.SetActive(false);
                    buildButtonIcons[globalIdx] = iconImg;

                    // Text label
                    var textGO = new GameObject("Text");
                    textGO.transform.SetParent(btnGO.transform, false);
                    var trt = textGO.AddComponent<RectTransform>();
                    trt.anchorMin = Vector2.zero;
                    trt.anchorMax = Vector2.one;
                    trt.sizeDelta = Vector2.zero;
                    trt.offsetMin = new Vector2(2, 2);
                    trt.offsetMax = new Vector2(-2, -2);
                    var tmp = textGO.AddComponent<TextMeshProUGUI>();
                    tmp.fontSize = 10;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.color = Color.white;
                    tmp.overflowMode = TextOverflowModes.Overflow;
                    tmp.textWrappingMode = TextWrappingModes.Normal;
                    tmp.raycastTarget = false;

                    // Hotkey label
                    var hotkeyGO = new GameObject("Hotkey");
                    hotkeyGO.transform.SetParent(btnGO.transform, false);
                    var hrt = hotkeyGO.AddComponent<RectTransform>();
                    hrt.anchorMin = new Vector2(0, 1);
                    hrt.anchorMax = new Vector2(0, 1);
                    hrt.pivot = new Vector2(0, 1);
                    hrt.anchoredPosition = new Vector2(2, -1);
                    hrt.sizeDelta = new Vector2(16, 14);
                    var hTmp = hotkeyGO.AddComponent<TextMeshProUGUI>();
                    hTmp.fontSize = 9;
                    hTmp.fontStyle = FontStyles.Bold;
                    hTmp.alignment = TextAlignmentOptions.TopLeft;
                    hTmp.color = new Color(1f, 1f, 1f, 0.85f);
                    hTmp.overflowMode = TextOverflowModes.Overflow;
                    hTmp.raycastTarget = false;
                    buildButtonHotkeys[globalIdx] = hTmp;

                    // EventTrigger for tooltip hover
                    var trigger = btnGO.AddComponent<EventTrigger>();
                    var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                    int hoverIdx = globalIdx;
                    enterEntry.callback.AddListener((_) => ShowBuildTooltip(hoverIdx));
                    trigger.triggers.Add(enterEntry);
                    var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                    exitEntry.callback.AddListener((_) => HideBuildTooltip(hoverIdx));
                    trigger.triggers.Add(exitEntry);

                    buildButtonGOs[globalIdx] = btnGO;
                    buildButtons[globalIdx] = btn;
                    buildButtonTexts[globalIdx] = tmp;
                    btnGO.SetActive(false);
                }

                panelGO.SetActive(false);
            }

            // Build panel tooltip (shared across all age panels)
            buildTooltipPanelGO = new GameObject("BuildTooltipPanel");
            buildTooltipPanelGO.transform.SetParent(canvasTransform, false);
            buildTooltipPanelRT = buildTooltipPanelGO.AddComponent<RectTransform>();
            buildTooltipPanelRT.anchorMin = new Vector2(0, 0);
            buildTooltipPanelRT.anchorMax = new Vector2(0, 0);
            buildTooltipPanelRT.pivot = new Vector2(0, 0);
            buildTooltipPanelRT.sizeDelta = new Vector2(200f, 60f);
            var buildTooltipBg = buildTooltipPanelGO.AddComponent<Image>();
            buildTooltipBg.color = new Color(0, 0, 0, 1f);
            buildTooltipBg.raycastTarget = false;
            var buildTooltipOutline = buildTooltipPanelGO.AddComponent<Outline>();
            buildTooltipOutline.effectColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            buildTooltipOutline.effectDistance = new Vector2(2, -2);

            var buildTooltipTextGO = new GameObject("Text");
            buildTooltipTextGO.transform.SetParent(buildTooltipPanelGO.transform, false);
            var buildTooltipTextRT = buildTooltipTextGO.AddComponent<RectTransform>();
            buildTooltipTextRT.anchorMin = Vector2.zero;
            buildTooltipTextRT.anchorMax = Vector2.one;
            buildTooltipTextRT.sizeDelta = Vector2.zero;
            buildTooltipTextRT.offsetMin = new Vector2(8, 8);
            buildTooltipTextRT.offsetMax = new Vector2(-8, -8);
            buildTooltipText = buildTooltipTextGO.AddComponent<TextMeshProUGUI>();
            buildTooltipText.fontSize = 11;
            buildTooltipText.color = Color.white;
            buildTooltipText.alignment = TextAlignmentOptions.TopLeft;
            buildTooltipText.overflowMode = TextOverflowModes.Overflow;
            buildTooltipText.textWrappingMode = TextWrappingModes.Normal;
            buildTooltipText.raycastTarget = false;

            buildTooltipPanelGO.SetActive(false);
        }

        private void ShowBuildTooltip(int index)
        {
            if (index < 0 || index >= TotalBuildButtons || buildTooltips[index] == null) return;
            hoveredBuildIndex = index;
            buildTooltipText.text = buildTooltips[index];

            int age = index / BuildButtonsPerAge;
            var btnRT = buildButtonGOs[index].GetComponent<RectTransform>();
            const float tooltipWidth = 200f;
            const float gap = 4f;
            const float padding = 8f;
            float bpw = ActionPadding + BuildGridCols * (ActionButtonSize + ActionButtonGap) - ActionButtonGap + ActionPadding;
            Vector2 buildPos = buildPanelRTs[age].anchoredPosition;
            float tx = buildPos.x + Mathf.Clamp(btnRT.anchoredPosition.x, 0, bpw - tooltipWidth);
            float ty = buildPos.y + btnRT.anchoredPosition.y + ActionButtonSize + gap;

            buildTooltipText.ForceMeshUpdate();
            float textHeight = buildTooltipText.preferredHeight;
            buildTooltipPanelRT.sizeDelta = new Vector2(tooltipWidth, textHeight + padding * 2f);
            buildTooltipPanelRT.anchoredPosition = new Vector2(tx, ty);

            buildTooltipPanelGO.SetActive(true);
            buildTooltipPanelGO.transform.SetAsLastSibling();
        }

        private void HideBuildTooltip(int index)
        {
            if (hoveredBuildIndex == index)
            {
                hoveredBuildIndex = -1;
                buildTooltipPanelGO.SetActive(false);
            }
        }

        private void BuildQueuePanel(Transform parent)
        {
            float queuePanelWidth = StatsPanelWidth + PanelGap + ActionPanelWidth;
            float queueX = Margin + ResourcePanelWidth + PanelGap;
            float queueY = Margin + StatsPanelHeight + PanelGap;

            queuePanelGO = MakePanel(parent, "QueuePanel", queuePanelWidth, QueuePanelHeight);
            queuePanelRT = queuePanelGO.GetComponent<RectTransform>();
            queuePanelRT.anchorMin = new Vector2(0, 0);
            queuePanelRT.anchorMax = new Vector2(0, 0);
            queuePanelRT.pivot = new Vector2(0, 0);
            queuePanelRT.anchoredPosition = new Vector2(queueX, queueY);

            queueItemGOs = new GameObject[MaxVisibleQueueItems];
            queueItemFills = new Image[MaxVisibleQueueItems];
            queueItemIcons = new Image[MaxVisibleQueueItems];
            queueItemTexts = new TMP_Text[MaxVisibleQueueItems];
            queueItemXOverlays = new GameObject[MaxVisibleQueueItems];
            queueItemBgs = new Image[MaxVisibleQueueItems];

            float itemY = (QueuePanelHeight - QueueItemHeight) / 2f;

            for (int i = 0; i < MaxVisibleQueueItems; i++)
            {
                float itemX = 6f + i * (QueueItemWidth + QueueItemGap);

                // Item container
                var itemGO = new GameObject($"QueueItem{i}");
                itemGO.transform.SetParent(queuePanelGO.transform, false);
                var itemRT = itemGO.AddComponent<RectTransform>();
                itemRT.anchorMin = new Vector2(0, 0);
                itemRT.anchorMax = new Vector2(0, 0);
                itemRT.pivot = new Vector2(0, 0);
                itemRT.anchoredPosition = new Vector2(itemX, itemY);
                itemRT.sizeDelta = new Vector2(QueueItemWidth, QueueItemHeight);

                // Background — dark to match action button appearance
                var bgImg = itemGO.AddComponent<Image>();
                bgImg.color = new Color(0.06f, 0.06f, 0.06f);
                queueItemBgs[i] = bgImg;

                // Deactivate BEFORE adding Button — prevents OnEnable from firing CrossFadeColor
                itemGO.SetActive(false);

                // Button (transition disabled — hover color handled manually via hoveredQueueIndex)
                var btn = itemGO.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = null;
                int idx = i;
                btn.onClick.AddListener(() => OnQueueItemClicked(idx));

                // Fill overlay (anchor-based)
                var fillGO = new GameObject("Fill");
                fillGO.transform.SetParent(itemGO.transform, false);
                var fillRT = fillGO.AddComponent<RectTransform>();
                fillRT.anchorMin = Vector2.zero;
                fillRT.anchorMax = new Vector2(0, 1);
                fillRT.pivot = new Vector2(0, 0.5f);
                fillRT.offsetMin = Vector2.zero;
                fillRT.offsetMax = Vector2.zero;
                var fillImg = fillGO.AddComponent<Image>();
                fillImg.color = TrainingBarColor;
                fillImg.raycastTarget = false;
                queueItemFills[i] = fillImg;

                // Unit icon (centered)
                var iconGO = new GameObject("Icon");
                iconGO.transform.SetParent(itemGO.transform, false);
                var irt = iconGO.AddComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.5f, 0.5f);
                irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.pivot = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = Vector2.zero;
                irt.sizeDelta = new Vector2(22f, 22f);
                var iconImg = iconGO.AddComponent<Image>();
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                queueItemIcons[i] = iconImg;

                // Text label (used for overflow indicator like "+3")
                var textGO = new GameObject("Text");
                textGO.transform.SetParent(itemGO.transform, false);
                var trt = textGO.AddComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.sizeDelta = Vector2.zero;
                trt.offsetMin = new Vector2(2, 0);
                trt.offsetMax = new Vector2(-2, 0);
                var tmp = textGO.AddComponent<TextMeshProUGUI>();
                tmp.fontSize = 11;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.raycastTarget = false;
                queueItemTexts[i] = tmp;

                // Red X overlay (hidden by default)
                var xOverlayGO = new GameObject("XOverlay");
                xOverlayGO.transform.SetParent(itemGO.transform, false);
                var xRT = xOverlayGO.AddComponent<RectTransform>();
                xRT.anchorMin = Vector2.zero;
                xRT.anchorMax = Vector2.one;
                xRT.sizeDelta = Vector2.zero;
                xRT.offsetMin = Vector2.zero;
                xRT.offsetMax = Vector2.zero;
                var xBg = xOverlayGO.AddComponent<Image>();
                xBg.color = new Color(0.7f, 0.1f, 0.1f, 0.6f);
                xBg.raycastTarget = false;

                var xTextGO = new GameObject("XText");
                xTextGO.transform.SetParent(xOverlayGO.transform, false);
                var xtrt = xTextGO.AddComponent<RectTransform>();
                xtrt.anchorMin = Vector2.zero;
                xtrt.anchorMax = Vector2.one;
                xtrt.sizeDelta = Vector2.zero;
                xtrt.offsetMin = Vector2.zero;
                xtrt.offsetMax = Vector2.zero;
                var xTmp = xTextGO.AddComponent<TextMeshProUGUI>();
                xTmp.text = "X";
                xTmp.fontSize = 16;
                xTmp.fontStyle = FontStyles.Bold;
                xTmp.alignment = TextAlignmentOptions.Center;
                xTmp.color = Color.white;
                xTmp.raycastTarget = false;

                xOverlayGO.SetActive(false);
                queueItemXOverlays[i] = xOverlayGO;

                // EventTrigger for hover — track index, UpdateQueuePanel syncs visuals + tooltip
                var trigger = itemGO.AddComponent<EventTrigger>();
                var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                int hoverIdx = i;
                enterEntry.callback.AddListener((_) => { hoveredQueueIndex = hoverIdx; ShowQueueTooltip(hoverIdx); });
                trigger.triggers.Add(enterEntry);
                var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exitEntry.callback.AddListener((_) => { if (hoveredQueueIndex == hoverIdx) { hoveredQueueIndex = -1; HideQueueTooltip(); } });
                trigger.triggers.Add(exitEntry);

                queueItemGOs[i] = itemGO;
            }

            queuePanelGO.SetActive(false);
        }

        private void OnQueueItemClicked(int slotIndex)
        {
            // Block clicks on the overflow indicator slot
            if (aggregatedQueue.Count > MaxVisibleQueueItems && slotIndex >= MaxVisibleQueueItems - 1)
                return;
            if (slotIndex < 0 || slotIndex >= aggregatedQueue.Count) return;
            var entry = aggregatedQueue[slotIndex];
            hoveredQueueIndex = -1;
            HideQueueTooltip();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;
            sim.CommandBuffer.EnqueueCommand(
                new CancelTrainCommand(selectionManager.LocalPlayerId, entry.BuildingId, entry.QueueIndex));
        }

        private void UpdateQueuePanel(List<QueueEntry> entries, GameSimulation sim)
        {
            bool hasOverflow = entries.Count > MaxVisibleQueueItems;
            int normalCount = hasOverflow ? MaxVisibleQueueItems - 1 : entries.Count;

            for (int i = 0; i < MaxVisibleQueueItems; i++)
            {
                if (i < normalCount)
                {
                    var entry = entries[i];
                    queueItemGOs[i].SetActive(true);
                    queueItemIcons[i].sprite = UnitIcons.Get(entry.UnitType);
                    queueItemIcons[i].gameObject.SetActive(true);
                    queueItemTexts[i].gameObject.SetActive(false);
                    SetBarFill(queueItemFills[i], entry.Progress > 0f ? entry.Progress : 0f, TrainingBarColor);

                    bool hovered = i == hoveredQueueIndex;
                    queueItemBgs[i].color = hovered
                        ? new Color(0.6f, 0.1f, 0.1f)
                        : new Color(0.06f, 0.06f, 0.06f);
                    queueItemXOverlays[i].SetActive(hovered);
                }
                else if (hasOverflow && i == normalCount)
                {
                    // Overflow indicator — display-only, no hover/cancel
                    queueItemGOs[i].SetActive(true);
                    queueItemIcons[i].gameObject.SetActive(false);
                    queueItemTexts[i].gameObject.SetActive(true);
                    queueItemTexts[i].text = $"+{entries.Count - normalCount}";
                    SetBarFill(queueItemFills[i], 0f, TrainingBarColor);
                    queueItemBgs[i].color = new Color(0.06f, 0.06f, 0.06f);
                    queueItemXOverlays[i].SetActive(false);
                }
                else
                {
                    queueItemGOs[i].SetActive(false);
                }
            }
        }

        // =============================================================
        //  UI Helpers
        // =============================================================

        private GameObject MakePanel(Transform parent, string name, float w, float h)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            var img = go.AddComponent<Image>();
            img.color = PanelBgColor;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            outline.effectDistance = new Vector2(2, -2);
            return go;
        }

        private TMP_Text MakeText(Transform parent, string name, float x, float y, float w, float h,
            float fontSize, FontStyles style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        private RectTransform MakeBar(Transform parent, string name, float x, float y, float w, float h,
            Color bgColor, out Image fill, out TMP_Text text)
        {
            var bgGO = new GameObject(name);
            bgGO.transform.SetParent(parent, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0, 0);
            bgRT.anchorMax = new Vector2(0, 0);
            bgRT.pivot = new Vector2(0, 0);
            bgRT.anchoredPosition = new Vector2(x, y);
            bgRT.sizeDelta = new Vector2(w, h);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = bgColor;

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(bgGO.transform, false);
            var fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = new Vector2(0, 1);
            fillRT.pivot = new Vector2(0, 0.5f);
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            fillRT.sizeDelta = new Vector2(0, 0);
            fill = fillGO.AddComponent<Image>();

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(bgGO.transform, false);
            var trt = textGO.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            text = textGO.AddComponent<TextMeshProUGUI>();
            text.fontSize = 11;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.overflowMode = TextOverflowModes.Overflow;

            return bgRT;
        }

        private void SetBarFill(Image fill, float fraction, Color color)
        {
            var rt = fill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1);
            rt.offsetMax = Vector2.zero;
            fill.color = color;
        }

        // =============================================================
        //  Button flash (hotkey press visual feedback)
        // =============================================================

        private void FlashActionButton(int index)
        {
            if (index < 0 || index >= 12) return;
            if (!actionButtonGOs[index].activeInHierarchy) return;
            actionFlashTimers[index] = ButtonFlashDuration;
            // Bypass Button's color tint by setting colors block directly
            var cb = actionButtons[index].colors;
            cb.normalColor = ButtonPressedColor;
            actionButtons[index].colors = cb;
            if (actionButtonIcons[index].gameObject.activeSelf)
                actionButtonIcons[index].color = IconPressedColor;
        }

        private void FlashBuildButton(int index)
        {
            if (index < 0 || index >= TotalBuildButtons) return;
            if (!buildButtonGOs[index].activeInHierarchy) return;
            buildFlashTimers[index] = ButtonFlashDuration;
            var cb = buildButtons[index].colors;
            cb.normalColor = ButtonPressedColor;
            buildButtons[index].colors = cb;
            if (buildButtonIcons[index].gameObject.activeSelf)
                buildButtonIcons[index].color = IconPressedColor;
        }

        private void UpdateButtonFlashTimers()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < 12; i++)
            {
                if (actionFlashTimers[i] > 0f)
                {
                    actionFlashTimers[i] -= dt;
                    if (actionFlashTimers[i] <= 0f)
                    {
                        actionFlashTimers[i] = 0f;
                        var cb = actionButtons[i].colors;
                        cb.normalColor = ButtonNormalColor;
                        actionButtons[i].colors = cb;
                        actionButtonIcons[i].color = Color.white;
                    }
                }
            }
            for (int i = 0; i < TotalBuildButtons; i++)
            {
                if (buildFlashTimers[i] > 0f)
                {
                    buildFlashTimers[i] -= dt;
                    if (buildFlashTimers[i] <= 0f)
                    {
                        buildFlashTimers[i] = 0f;
                        var cb = buildButtons[i].colors;
                        cb.normalColor = ButtonNormalColor;
                        buildButtons[i].colors = cb;
                        buildButtonIcons[i].color = Color.white;
                    }
                }
            }
        }

        // =============================================================
        //  Update — hotkeys + hover suppression
        // =============================================================

        private void Update()
        {
            UpdateButtonFlashTimers();

            if (selectionManager == null)
            {
                UnitSelectionManager.SetInfoPanelSuppressed(false);
                deleteMouseHolding = false;
                return;
            }

            bool panelVisible = selectionManager.SelectedBuildings.Count > 0 ||
                                selectionManager.SelectedUnits.Count > 0 ||
                                selectionManager.SelectedResourceNode != null;
            if (!panelVisible)
            {
                UnitSelectionManager.SetInfoPanelSuppressed(false);
                deleteMouseHolding = false;
                return;
            }

            Vector2 mousePos = VirtualCursor.Position;

            UnitSelectionManager.SetInfoPanelSuppressed(ContainsScreenPoint(mousePos));

            ProcessHotkeys();
            UpdateDeleteMouseHold();
        }

        // =============================================================
        //  LateUpdate — refresh UI content
        // =============================================================

        private void LateUpdate()
        {
            if (selectionManager == null) return;

            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            // Close landmark panel on Escape or right-click; Q/W to choose
            if (landmarkPanelGO != null)
            {
                var kb = Keyboard.current;
                var mouse = Mouse.current;
                if ((kb != null && kb.escapeKey.wasPressedThisFrame) ||
                    (mouse != null && mouse.rightButton.wasPressedThisFrame))
                {
                    CloseLandmarkChoicePanel();
                }
                else if (kb != null && kb.qKey.wasPressedThisFrame && landmarkChoiceACallback != null)
                    landmarkChoiceACallback();
                else if (kb != null && kb.wKey.wasPressedThisFrame && landmarkChoiceBCallback != null)
                    landmarkChoiceBCallback();
            }

            // Clear action callbacks
            for (int i = 0; i < 12; i++) actionCallbacks[i] = null;
            for (int i = 0; i < TotalBuildButtons; i++) buildCallbacks[i] = null;

            GridButton?[] actionSlots = null;
            GridButton?[] buildSlots = null;
            bool hasContent = false;

            var selectedBuildings = selectionManager.SelectedBuildings;
            var selectedNode = selectionManager.SelectedResourceNode;
            var selectedUnits = selectionManager.SelectedUnits;

            if (selectedBuildings.Count > 0)
            {
                hasContent = true;
                if (selectedBuildings.Count == 1)
                {
                    PopulateBuildingStats(selectedBuildings[0], sim);
                    actionSlots = GetBuildingActionSlots(selectedBuildings[0], sim);
                }
                else
                {
                    PopulateMultiBuildingStats(selectedBuildings, sim);
                    actionSlots = GetMultiBuildingActionSlots(selectedBuildings, sim);
                }
            }
            else if (selectedNode != null)
            {
                hasContent = true;
                PopulateResourceNodeStats(selectedNode);
            }
            else if (selectedUnits.Count > 0)
            {
                hasContent = true;
                if (selectedUnits.Count == 1)
                {
                    PopulateUnitStats(selectedUnits[0], sim);
                    actionSlots = GetUnitActionSlots(selectedUnits[0], sim);
                    if (selectedUnits[0].UnitType == 0 && selectedUnits[0].PlayerId == selectionManager.LocalPlayerId)
                        buildSlots = GetAllAgeBuildSlots(sim);
                }
                else
                {
                    PopulateMultiUnitStats(selectedUnits, sim);
                    actionSlots = GetMultiUnitActionSlots(selectedUnits, sim);
                    // Build menu only if all owned villagers
                    bool allOwnedVillagers = true;
                    for (int i = 0; i < selectedUnits.Count; i++)
                    {
                        if (selectedUnits[i].UnitType != 0 || selectedUnits[i].PlayerId != selectionManager.LocalPlayerId)
                        { allOwnedVillagers = false; break; }
                    }
                    if (allOwnedVillagers)
                        buildSlots = GetAllAgeBuildSlots(sim);
                }
            }

            // Show/hide stats panel
            statsPanelGO.SetActive(hasContent);
            s_statsPanelRT = hasContent ? statsPanelRT : null;

            // Show/hide action panel + populate buttons
            bool hasActions = actionSlots != null && HasAnySlot(actionSlots);
            actionPanelGO.SetActive(hasActions);
            s_actionPanelVisible = hasActions;
            s_actionPanelRT = hasActions ? actionPanelRT : null;
            if (hasActions)
            {
                PopulateActionButtons(actionSlots);
                UpdateDeleteHoldFill();
            }
            else
            {
                for (int i = 0; i < 12; i++) actionButtonGOs[i].SetActive(false);
                UpdateDeleteHoldFill();
                hoveredActionIndex = -1;
                tooltipPanelGO.SetActive(false);
                s_tooltipPanelRT = null;
            }

            // Show/hide build panels (3 age panels)
            bool hasBuildSlots = buildSlots != null && HasAnySlot(buildSlots);
            for (int a = 0; a < BuildAgePanelCount; a++)
            {
                buildPanelGOs[a].SetActive(hasBuildSlots);
                s_buildPanelRTs[a] = hasBuildSlots ? buildPanelRTs[a] : null;
            }
            s_buildPanelsVisible = hasBuildSlots;
            if (hasBuildSlots)
            {
                PopulateBuildButtons(buildSlots);
                UpdateBuildPanelHighlight();
            }
            else
            {
                for (int i = 0; i < TotalBuildButtons; i++) buildButtonGOs[i].SetActive(false);
                hoveredBuildIndex = -1;
                buildTooltipPanelGO.SetActive(false);
                // Reset build hotkeys when build panels disappear
                buildMenuAge = 0;
            }

            // Toggle hotkey label visibility based on build mode
            UpdateHotkeyVisibility();

            // Show/hide queue panel
            bool showQueue = false;
            aggregatedQueue.Clear();
            int maxDepth = 0;
            for (int i = 0; i < selectedBuildings.Count; i++)
            {
                var b = sim.BuildingRegistry.GetBuilding(selectedBuildings[i].BuildingId);
                if (b != null && b.IsTraining)
                {
                    if (b.TrainingQueue.Count > maxDepth) maxDepth = b.TrainingQueue.Count;
                }
            }
            for (int q = 0; q < maxDepth; q++)
            {
                for (int i = 0; i < selectedBuildings.Count; i++)
                {
                    var building = sim.BuildingRegistry.GetBuilding(selectedBuildings[i].BuildingId);
                    if (building == null || !building.IsTraining)
                        continue;
                    if (q >= building.TrainingQueue.Count) continue;
                    float progress = 0f;
                    if (q == 0)
                    {
                        int totalTicks = BuildingTrainingSystem.GetTrainTime(sim.Config, building.TrainingQueue[0]);
                        progress = 1f - (float)building.TrainingTicksRemaining / totalTicks;
                    }
                    aggregatedQueue.Add(new QueueEntry
                    {
                        BuildingId = building.Id,
                        QueueIndex = q,
                        UnitType = building.TrainingQueue[q],
                        Progress = progress
                    });
                }
            }
            if (aggregatedQueue.Count > 0)
            {
                showQueue = true;
                UpdateQueuePanel(aggregatedQueue, sim);
            }
            if (!showQueue)
            {
                queuePanelGO.SetActive(false);
                hoveredQueueIndex = -1;
            }
            else
            {
                queuePanelGO.SetActive(true);
            }
            s_queuePanelRT = showQueue ? queuePanelRT : null;
        }

        // =============================================================
        //  Stats population
        // =============================================================

        private void ClearStats()
        {
            healthBarBgRT.gameObject.SetActive(false);
            progressBarBgRT.gameObject.SetActive(false);
            queueText.gameObject.SetActive(false);
            statsIconGO.SetActive(false);
            for (int i = 0; i < MaxStatLines; i++)
                statLines[i].gameObject.SetActive(false);
        }

        private void PopulateUnitStats(UnitView view, GameSimulation sim)
        {
            var unitData = sim.UnitRegistry.GetUnit(view.UnitId);
            if (unitData == null) { ClearStats(); return; }

            nameText.text = GetUnitName(view.UnitType);
            nameText.gameObject.SetActive(true);

            // Icon
            statsIconImage.sprite = UnitIcons.Get(view.UnitType);
            statsIconGO.SetActive(statsIconImage.sprite != null);

            // Health bar
            float hpFrac = Mathf.Clamp01((float)unitData.CurrentHealth / unitData.MaxHealth);
            healthBarBgRT.gameObject.SetActive(true);
            SetBarFill(healthBarFill, hpFrac, Color.Lerp(HealthColorEmpty, HealthColorFull, hpFrac));
            healthBarText.text = $"{unitData.CurrentHealth} / {unitData.MaxHealth}";

            // Stats
            int line = 0;
            string attackStr = unitData.BonusDamageVsType >= 0
                ? $"Attack:  {unitData.AttackDamage} (+{unitData.BonusDamageAmount})"
                : $"Attack:  {unitData.AttackDamage}";
            SetStatLine(line++, attackStr);
            SetStatLine(line++, $"Armor:   {unitData.MeleeArmor}/{unitData.RangedArmor}");
            SetStatLine(line++, $"Range:   {unitData.AttackRange.ToFloat():F1}");
            SetStatLine(line++, $"Speed:   {unitData.MoveSpeed.ToFloat():F1}");

            if (view.UnitType == 0 && unitData.CarriedResourceAmount > 0)
                SetStatLine(line++, $"Carry: {unitData.CarriedResourceAmount}/{unitData.CarryCapacity} {unitData.CarriedResourceType}");

            if (view.PlayerId != selectionManager.LocalPlayerId)
            {
                var mm = MatchmakingManager.Instance;
                string playerName = $"Player {view.PlayerId + 1}";
                if (mm?.Teams != null)
                {
                    foreach (var team in mm.Teams)
                    {
                        if (team.players == null) continue;
                        foreach (var tp in team.players)
                        {
                            if (tp.game_player_id == view.PlayerId)
                            { playerName = tp.username ?? playerName; goto done; }
                        }
                    }
                    done:;
                }
                Color c = view.PlayerId < GameSetup.PlayerColors.Length
                    ? GameSetup.PlayerColors[view.PlayerId] : Color.white;
                string hex = ColorUtility.ToHtmlStringRGB(c);
                SetStatLine(line++, $"Owner:  <color=#{hex}>{playerName}</color>");
            }

            for (int i = line; i < MaxStatLines; i++)
                statLines[i].gameObject.SetActive(false);

            progressBarBgRT.gameObject.SetActive(false);
            queueText.gameObject.SetActive(false);
        }

        private void PopulateMultiUnitStats(IReadOnlyList<UnitView> selected, GameSimulation sim)
        {
            ClearStats();
            nameText.text = $"{selected.Count} Units";
            nameText.gameObject.SetActive(true);
            healthBarBgRT.gameObject.SetActive(false);

            var typeCounts = new Dictionary<int, int>();
            for (int i = 0; i < selected.Count; i++)
            {
                int t = selected[i].UnitType;
                if (typeCounts.ContainsKey(t)) typeCounts[t]++;
                else typeCounts[t] = 1;
            }

            int line = 0;
            foreach (var kvp in typeCounts)
            {
                if (line >= MaxStatLines) break;
                string label = kvp.Value == 1 ? GetUnitName(kvp.Key) : GetUnitPlural(kvp.Key);
                SetStatLine(line++, $"  {kvp.Value}  {label}");
            }
            for (int i = line; i < MaxStatLines; i++) statLines[i].gameObject.SetActive(false);
        }

        private void PopulateBuildingStats(BuildingView view, GameSimulation sim)
        {
            var building = sim.BuildingRegistry.GetBuilding(view.BuildingId);
            if (building == null) { ClearStats(); return; }

            nameText.text = building.IsGate ? "Gate" : GetBuildingName(view.BuildingType);
            nameText.gameObject.SetActive(true);

            // Icon
            statsIconImage.sprite = BuildingIcons.Get(view.BuildingType);
            statsIconGO.SetActive(statsIconImage.sprite != null);

            float hpFrac = Mathf.Clamp01((float)building.CurrentHealth / building.MaxHealth);
            healthBarBgRT.gameObject.SetActive(true);
            SetBarFill(healthBarFill, hpFrac, Color.Lerp(HealthColorEmpty, HealthColorFull, hpFrac));
            healthBarText.text = $"{building.CurrentHealth} / {building.MaxHealth}";

            int line = 0;
            SetStatLine(line++, $"Armor:   {building.Armor}");
            for (int i = line; i < MaxStatLines; i++) statLines[i].gameObject.SetActive(false);

            bool showProgress = false;
            queueText.gameObject.SetActive(false);

            if (building.IsUnderConstruction && building.PlayerId == selectionManager.LocalPlayerId)
            {
                showProgress = true;
                float secondsLeft = (float)building.ConstructionTicksRemaining / sim.Config.TickRate;
                float progress = building.ConstructionProgress;
                SetBarFill(progressBarFill, progress, ConstructionBarColor);
                progressBarText.text = $"Building... {secondsLeft:F1}s";
            }
            else if (building.IsTraining)
            {
                showProgress = true;
                int trainingUnitType = building.TrainingQueue[0];
                int totalTicks = BuildingTrainingSystem.GetTrainTime(sim.Config, trainingUnitType);
                int remaining = building.TrainingTicksRemaining;
                float progress = 1f - (float)remaining / totalTicks;
                float secondsLeft = (float)remaining / sim.Config.TickRate;
                SetBarFill(progressBarFill, progress, TrainingBarColor);
                progressBarText.text = $"{secondsLeft:F1}s";

                if (building.TrainingQueue.Count > 1)
                {
                    queueText.text = $"Queue: {building.TrainingQueue.Count}";
                    queueText.gameObject.SetActive(true);
                }
            }
            else if (building.IsUpgrading && building.PlayerId == selectionManager.LocalPlayerId)
            {
                showProgress = true;
                float progress = building.UpgradeProgress;
                float secondsLeft = (float)building.UpgradeTicksRemaining / sim.Config.TickRate;
                SetBarFill(progressBarFill, progress, TrainingBarColor);
                progressBarText.text = $"Upgrading... {secondsLeft:F1}s";

                if (building.UpgradeQueue.Count > 1)
                {
                    queueText.text = $"Queue: {building.UpgradeQueue.Count}";
                    queueText.gameObject.SetActive(true);
                }
            }
            progressBarBgRT.gameObject.SetActive(showProgress);
        }

        private void PopulateMultiBuildingStats(IReadOnlyList<BuildingView> buildings, GameSimulation sim)
        {
            ClearStats();
            nameText.text = $"{buildings.Count} Buildings";
            nameText.gameObject.SetActive(true);

            int localPid = selectionManager.LocalPlayerId;
            var effectiveType = GetEffectiveBuildingType(buildings, localPid);

            // Show icon for the effective type
            if (effectiveType.HasValue)
            {
                statsIconImage.sprite = BuildingIcons.Get(effectiveType.Value);
                statsIconGO.SetActive(statsIconImage.sprite != null);
            }

            var typeCounts = new Dictionary<BuildingType, int>();
            for (int i = 0; i < buildings.Count; i++)
            {
                var t = buildings[i].BuildingType;
                if (typeCounts.ContainsKey(t)) typeCounts[t]++;
                else typeCounts[t] = 1;
            }

            int line = 0;
            // Show effective type first with marker
            if (effectiveType.HasValue && typeCounts.TryGetValue(effectiveType.Value, out int eCount))
            {
                string label = eCount == 1 ? GetBuildingName(effectiveType.Value) : GetBuildingPlural(effectiveType.Value);
                SetStatLine(line++, $"> {eCount}  {label}");
                typeCounts.Remove(effectiveType.Value);
            }
            // Remaining types sorted by count desc, then name asc
            var remaining = new List<KeyValuePair<BuildingType, int>>(typeCounts);
            remaining.Sort((a, b) => {
                int cmp = b.Value.CompareTo(a.Value);
                if (cmp != 0) return cmp;
                return GetBuildingName(a.Key).CompareTo(GetBuildingName(b.Key));
            });
            foreach (var kvp in remaining)
            {
                if (line >= MaxStatLines) break;
                string label = kvp.Value == 1 ? GetBuildingName(kvp.Key) : GetBuildingPlural(kvp.Key);
                SetStatLine(line++, $"  {kvp.Value}  {label}");
            }
            for (int i = line; i < MaxStatLines; i++) statLines[i].gameObject.SetActive(false);
        }

        private void PopulateResourceNodeStats(ResourceNode nodeView)
        {
            ClearStats();
            var nodeData = nodeView.GetNodeData();
            if (nodeData == null) return;

            nameText.text = nodeData.IsCarcass ? "Sheep Carcass" : GetResourceNodeName(nodeData.Type);
            nameText.gameObject.SetActive(true);

            string sprName = GetResourceTypeName(nodeData.Type).ToLower();
            SetStatLine(0, $"<sprite name=\"{sprName}\">  {nodeData.RemainingAmount}");
            for (int i = 1; i < MaxStatLines; i++) statLines[i].gameObject.SetActive(false);
        }

        private void SetStatLine(int index, string text)
        {
            if (index >= MaxStatLines) return;
            statLines[index].text = text;
            statLines[index].gameObject.SetActive(true);
        }

        // =============================================================
        //  Action buttons population
        // =============================================================

        private void UpdateHotkeyVisibility()
        {
            bool buildMode = buildMenuAge > 0;
            for (int i = 0; i < 12; i++)
            {
                if (actionButtonGOs[i].activeSelf)
                    actionButtonHotkeys[i].gameObject.SetActive(!buildMode);
            }
            // Build button hotkeys always visible; they show which keys to press
            for (int i = 0; i < TotalBuildButtons; i++)
            {
                if (buildButtonGOs[i].activeSelf)
                {
                    // Show hotkey only for the active age panel, or all if none active
                    int age = i / BuildButtonsPerAge;
                    buildButtonHotkeys[i].gameObject.SetActive(buildMode ? (buildMenuAge == age + 1) : true);
                }
            }
        }

        private void UpdateBuildPanelHighlight()
        {
            for (int a = 0; a < BuildAgePanelCount; a++)
            {
                bool active = buildMenuAge == a + 1;
                if (buildPanelHeaderBgs[a] != null)
                    buildPanelHeaderBgs[a].color = active ? new Color(0.3f, 0.4f, 0.55f) : new Color(0.18f, 0.18f, 0.18f);
            }
        }

        private void PopulateActionButtons(GridButton?[] slots)
        {
            for (int i = 0; i < 12; i++)
            {
                if (i < slots.Length && slots[i].HasValue)
                {
                    var btn = slots[i].Value;
                    actionButtonGOs[i].SetActive(true);
                    actionButtons[i].interactable = btn.Enabled;
                    actionCallbacks[i] = btn.OnClick;
                    actionTooltips[i] = btn.Tooltip;

                    actionButtonHotkeys[i].text = btn.Hotkey ?? "";

                    if (btn.Icon != null)
                    {
                        actionButtonIcons[i].sprite = btn.Icon;
                        actionButtonIcons[i].gameObject.SetActive(true);
                        actionButtonTexts[i].text = "";
                    }
                    else
                    {
                        actionButtonIcons[i].gameObject.SetActive(false);
                        actionButtonTexts[i].text = btn.Label;
                        actionButtonTexts[i].fontSize = 10;
                        actionButtonTexts[i].alignment = TextAlignmentOptions.Center;
                    }
                }
                else
                {
                    actionButtonGOs[i].SetActive(false);
                    actionCallbacks[i] = null;
                    actionTooltips[i] = null;
                }
            }
        }

        private const int DeleteButtonSlot = 9;

        private void OnActionButtonDown(int slotIndex)
        {
            if (slotIndex == DeleteButtonSlot)
            {
                deleteMouseHolding = true;
                deleteMouseHoldTimer = 0f;
                deleteMouseHoldSlot = slotIndex;
            }
        }

        private void OnActionButtonUp(int slotIndex)
        {
            if (slotIndex == DeleteButtonSlot)
            {
                deleteMouseHolding = false;
                deleteMouseHoldTimer = 0f;
                deleteMouseHoldSlot = -1;
            }
        }

        private void UpdateDeleteMouseHold()
        {
            if (!deleteMouseHolding) return;

            deleteMouseHoldTimer += Time.deltaTime;
            if (deleteMouseHoldTimer >= DeleteHoldDuration)
            {
                ExecuteDelete();
                deleteMouseHolding = false;
                deleteMouseHoldTimer = 0f;
                deleteMouseHoldSlot = -1;
            }
        }

        private void ExecuteDelete()
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null || selectionManager == null) return;

            int localPid = selectionManager.LocalPlayerId;
            var units = selectionManager.SelectedUnits;
            var buildings = selectionManager.SelectedBuildings;

            if (units.Count > 0)
            {
                var ownIds = new List<int>();
                for (int i = 0; i < units.Count; i++)
                    if (units[i].PlayerId == localPid)
                        ownIds.Add(units[i].UnitId);
                if (ownIds.Count > 0)
                    sim.CommandBuffer.EnqueueCommand(new DeleteUnitsCommand(localPid, ownIds.ToArray()));
            }
            else if (buildings.Count > 0 && buildings[0].PlayerId == localPid)
            {
                sim.CommandBuffer.EnqueueCommand(new DeleteBuildingCommand(localPid, buildings[0].BuildingId));
            }
        }

        private void UpdateDeleteHoldFill()
        {
            float keyProgress = selectionManager != null ? selectionManager.DeleteHoldProgress : 0f;
            float mouseProgress = deleteMouseHolding ? Mathf.Clamp01(deleteMouseHoldTimer / DeleteHoldDuration) : 0f;
            float progress = Mathf.Max(keyProgress, mouseProgress);

            int fillCount = Mathf.Min(12, actionButtonFills.Length);
            for (int i = 0; i < fillCount; i++)
            {
                if (i == DeleteButtonSlot && progress > 0f)
                {
                    actionButtonFills[i].gameObject.SetActive(true);
                    var rt = actionButtonFills[i].rectTransform;
                    rt.anchorMax = new Vector2(1f, progress);
                }
                else
                {
                    actionButtonFills[i].gameObject.SetActive(false);
                }
            }
        }

        private static bool HasAnySlot(GridButton?[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].HasValue) return true;
            return false;
        }

        private void PopulateBuildButtons(GridButton?[] slots)
        {
            for (int i = 0; i < TotalBuildButtons; i++)
            {
                if (i < slots.Length && slots[i].HasValue)
                {
                    var btn = slots[i].Value;
                    buildButtonGOs[i].SetActive(true);
                    buildButtons[i].interactable = btn.Enabled;
                    buildCallbacks[i] = btn.OnClick;
                    buildTooltips[i] = btn.Tooltip;

                    buildButtonHotkeys[i].text = btn.Hotkey ?? "";

                    if (btn.Icon != null)
                    {
                        buildButtonIcons[i].sprite = btn.Icon;
                        buildButtonIcons[i].gameObject.SetActive(true);
                        buildButtonTexts[i].text = "";
                    }
                    else
                    {
                        buildButtonIcons[i].gameObject.SetActive(false);
                        buildButtonTexts[i].text = btn.Label;
                        buildButtonTexts[i].fontSize = 10;
                        buildButtonTexts[i].alignment = TextAlignmentOptions.Center;
                    }
                }
                else
                {
                    buildButtonGOs[i].SetActive(false);
                    buildCallbacks[i] = null;
                    buildTooltips[i] = null;
                }
            }
        }

        // =============================================================
        //  Action slot builders
        // =============================================================

        private GridButton?[] GetUnitActionSlots(UnitView view, GameSimulation sim)
        {
            var unitData = sim.UnitRegistry.GetUnit(view.UnitId);
            if (unitData == null) return null;
            if (unitData.PlayerId != selectionManager.LocalPlayerId)
                return null;

            CommandIcons.EnsureLoaded();
            var slots = new GridButton?[12];

            // Q/W/E = Build age tabs (villagers only)
            if (view.UnitType == 0)
            {
                slots[0] = new GridButton { Label = "Build I", Hotkey = "Q",
                    Tooltip = "<b>Build (Age I)</b>\nOpen Age I build menu.",
                    Enabled = true,
                    OnClick = () => { buildMenuAge = 1; } };
                slots[1] = new GridButton { Label = "Build II", Hotkey = "W",
                    Tooltip = "<b>Build (Age II)</b>\nOpen Age II build menu.",
                    Enabled = true,
                    OnClick = () => { buildMenuAge = 2; } };
                slots[2] = new GridButton { Label = "Build III", Hotkey = "E",
                    Tooltip = "<b>Build (Age III)</b>\nOpen Age III build menu.",
                    Enabled = true,
                    OnClick = () => { buildMenuAge = 3; } };

                // R = Age Up (villagers only)
                int currentAge = sim.GetPlayerAge(view.PlayerId);
                bool isAgingUp = sim.IsPlayerAgingUp(view.PlayerId);
                if (currentAge < 3 && !isAgingUp)
                {
                    int targetAge = currentAge + 1;
                    var civ = sim.GetPlayerCivilization(view.PlayerId);
                    var (choiceA, choiceB) = LandmarkDefinitions.GetChoices(civ, targetAge);
                    var defA = LandmarkDefinitions.Get(choiceA);
                    var defB = LandmarkDefinitions.Get(choiceB);
                    int minFood = Mathf.Min(defA.FoodCost, defB.FoodCost);
                    int minGold = Mathf.Min(defA.GoldCost, defB.GoldCost);
                    var resources = sim.ResourceManager.GetPlayerResources(view.PlayerId);
                    bool canAfford = resources.Food >= minFood && resources.Gold >= minGold;
                    string ageRoman = LandmarkDefinitions.AgeToRoman(targetAge);
                    slots[3] = new GridButton { Label = $"Age {ageRoman}", Hotkey = "R",
                        Enabled = canAfford,
                        Tooltip = $"<b>Advance to Age {ageRoman}</b>\nChoose a landmark to construct.\nCost: {minFood} <sprite name=\"food\"> {minGold} <sprite name=\"gold\">",
                        OnClick = () => ShowLandmarkChoicePanel(sim, view.PlayerId, targetAge) };
                }
                else if (isAgingUp)
                {
                    string ageRoman = LandmarkDefinitions.AgeToRoman(currentAge + 1);
                    slots[3] = new GridButton { Label = $"Age {ageRoman}", Hotkey = "R",
                        Enabled = false,
                        Tooltip = $"<b>Advancing to Age {ageRoman}</b>\nLandmark under construction..." };
                }
                else if (currentAge >= 3)
                {
                    var res = sim.ResourceManager.GetPlayerResources(view.PlayerId);
                    var cfg = sim.Config;
                    bool canAfford = res.Wood >= cfg.WonderWoodCost && res.Stone >= cfg.WonderStoneCost
                                  && res.Food >= cfg.WonderFoodCost && res.Gold >= cfg.WonderGoldCost;
                    string costStr = $"{cfg.WonderFoodCost} <sprite name=\"food\"> {cfg.WonderWoodCost} <sprite name=\"wood\"> {cfg.WonderGoldCost} <sprite name=\"gold\"> {cfg.WonderStoneCost} <sprite name=\"stone\">";
                    slots[3] = new GridButton { Label = "Wonder", Hotkey = "R",
                        Enabled = canAfford,
                        Icon = BuildingIcons.Get(BuildingType.Wonder),
                        Tooltip = $"<b>Wonder</b>\nBuild to achieve a wonder victory.\nCost: {costStr}",
                        OnClick = () => selectionManager.EnterBuildPlacement(BuildingType.Wonder) };
                }
            }

            // A = Attack Move
            slots[4] = new GridButton { Label = "Attack\nMove", Hotkey = "A",
                Icon = CommandIcons.Attack,
                Tooltip = "<b>Attack Move</b>\nMove to a location, attacking enemies along the way.",
                Enabled = true,
                OnClick = () => selectionManager.EnterAttackMoveMode() };

            // S = Halt
            slots[5] = new GridButton { Label = "Halt", Hotkey = "S",
                Icon = CommandIcons.Guard,
                Tooltip = "<b>Halt</b>\nStop all current actions.",
                Enabled = true,
                OnClick = () => {
                    var units = selectionManager.SelectedUnits;
                    int[] unitIds = new int[units.Count];
                    for (int i = 0; i < units.Count; i++) unitIds[i] = units[i].UnitId;
                    sim.CommandBuffer.EnqueueCommand(new StopCommand(selectionManager.LocalPlayerId, unitIds));
                } };

            // D = Patrol
            slots[6] = new GridButton { Label = "Patrol", Hotkey = "D",
                Icon = CommandIcons.Patrol,
                Tooltip = "<b>Patrol</b>\nClick to set patrol destination. Shift+click adds waypoints.",
                Enabled = true,
                OnClick = () => selectionManager.EnterPatrolMode() };

            // F = Garrison
            slots[7] = new GridButton { Label = "Garrison", Hotkey = "F",
                Icon = CommandIcons.Garrison,
                Tooltip = "<b>Garrison</b>\nClick on a building to garrison selected units.",
                Enabled = true,
                OnClick = () => selectionManager.EnterGarrisonMode() };

            // X = Delete (hold to confirm)
            slots[9] = new GridButton { Label = "Delete", Hotkey = "X",
                Tooltip = "<b>Delete</b>\nHold to destroy selected unit.",
                Enabled = true };

            return slots;
        }

        private GridButton?[] GetAllAgeBuildSlots(GameSimulation sim)
        {
            var resources = sim.ResourceManager.GetPlayerResources(selectionManager.LocalPlayerId);
            int playerAge = sim.GetPlayerAge(selectionManager.LocalPlayerId);
            var cfg = sim.Config;

            var slots = new GridButton?[TotalBuildButtons];

            // Age 1 (offset 0): Q=Mill, W=LumberYard, E=Mine, A=House, S=Farm, D=Barracks, Z=WoodWall, X=WoodGate
            int o = 0;
            slots[o + 0] = MakeBuildSlot("Mill", "Q", "Drop-off point for food.", BuildingType.Mill, cfg.MillWoodCost, 0, 0, 0, resources, playerAge, false);
            slots[o + 1] = MakeBuildSlot("Lumber\nYard", "W", "Drop-off point for wood.", BuildingType.LumberYard, cfg.LumberYardWoodCost, 0, 0, 0, resources, playerAge, false);
            slots[o + 2] = MakeBuildSlot("Mine", "E", "Drop-off point for gold and stone.", BuildingType.Mine, cfg.MineWoodCost, 0, 0, 0, resources, playerAge, false);
            slots[o + 3] = MakeBuildSlot("House", "A", "Increases population cap by 10.", BuildingType.House, cfg.HouseWoodCost, 0, 0, 0, resources, playerAge, false);
            slots[o + 4] = MakeBuildSlot("Farm", "S", "Produces food.", BuildingType.Farm, cfg.FarmWoodCost, 0, 0, 0, resources, playerAge, false);
            slots[o + 5] = MakeBuildSlot("Barracks", "D", "Trains spearmen.", BuildingType.Barracks, cfg.BarracksWoodCost, 0, 0, 0, resources, playerAge, false);
            slots[o + 6] = MakeBuildSlot("Wood\nWall", "Z", "Defensive barrier. Can convert to gate.", BuildingType.Wall, cfg.WallWoodCost, 0, 0, 0, resources, playerAge, true);
            slots[o + 7] = MakeBuildSlot("Wood\nGate", "X", "Gate that allows units to pass.", BuildingType.WoodGate, cfg.WoodGateWoodCost, 0, 0, 0, resources, playerAge, true, true);

            // Age 2 (offset 8): Q=Blacksmith, W=Market, E=TownCenter, A=ArcheryRange, S=Stables, D=Tower
            o = BuildButtonsPerAge;
            slots[o + 0] = MakeBuildSlot("Blacksmith", "Q", "Upgrades weapons and armor.", BuildingType.Blacksmith, cfg.BlacksmithWoodCost, 0, 0, 0, resources, playerAge, false);
            slots[o + 1] = MakeBuildSlot("Market", "W", "Trade and buy/sell resources.", BuildingType.Market, cfg.MarketWoodCost, 0, 0, 0, resources, playerAge, false);
            slots[o + 2] = MakeBuildSlot("Town\nCenter", "E", "Trains villagers, drop-off for all resources.", BuildingType.TownCenter, cfg.TownCenterWoodCost, cfg.TownCenterStoneCost, 0, 0, resources, playerAge, false);
            slots[o + 3] = MakeBuildSlot("Archery", "A", "Trains archers.", BuildingType.ArcheryRange, cfg.ArcheryRangeWoodCost, 0, 0, 0, resources, playerAge, false);
            slots[o + 4] = MakeBuildSlot("Stables", "S", "Trains horsemen and scouts.", BuildingType.Stables, cfg.StablesWoodCost, 0, 0, 0, resources, playerAge, false);
            slots[o + 5] = MakeBuildSlot("Tower", "D", "Defensive tower. Can be upgraded.", BuildingType.Tower, cfg.TowerWoodCost, 0, 0, 0, resources, playerAge, false);

            // Age 3 (offset 16): Q=Monastery, W=University, E=SiegeWorkshop, A=Keep, S=StoneWall, D=StoneGate, Z=Wonder
            o = 2 * BuildButtonsPerAge;
            slots[o + 0] = MakeBuildSlot("Monastery", "Q", "Trains monks (healers).", BuildingType.Monastery, cfg.MonasteryWoodCost, 0, 0, 0, resources, playerAge, false);
            slots[o + 1] = MakeBuildSlot("University", "W", "Researches advanced technologies.", BuildingType.University, cfg.UniversityWoodCost, 0, 0, 0, resources, playerAge, false);
            slots[o + 2] = MakeBuildSlot("Siege\nWorkshop", "E", "Builds siege engines.", BuildingType.SiegeWorkshop, cfg.SiegeWorkshopWoodCost, 0, 0, 0, resources, playerAge, false);
            slots[o + 3] = MakeBuildSlot("Keep", "A", "Fortified defensive structure.", BuildingType.Keep, cfg.KeepWoodCost, cfg.KeepStoneCost, 0, 0, resources, playerAge, false);
            slots[o + 4] = MakeBuildSlot("Stone\nWall", "S", "Strong defensive barrier.", BuildingType.StoneWall, 0, cfg.StoneWallStoneCost, 0, 0, resources, playerAge, true);
            slots[o + 5] = MakeBuildSlot("Stone\nGate", "D", "Stone gate that allows units to pass.", BuildingType.StoneGate, 0, cfg.StoneGateStoneCost, 0, 0, resources, playerAge, true, true);

            return slots;
        }

        private GridButton MakeResearchButton(GameSimulation sim, int playerId, int buildingId,
            TechnologyType tech, string label, string hotkey, string desc,
            int foodCost, int goldCost, PlayerResources resources)
        {
            bool done = sim.HasTechnology(playerId, tech);
            bool canAfford = resources.Food >= foodCost && resources.Gold >= goldCost;

            // Check prerequisites for tier 2
            bool prereqMet = true;
            switch (tech)
            {
                case TechnologyType.MeleeAttack2: prereqMet = sim.HasTechnology(playerId, TechnologyType.MeleeAttack1); break;
                case TechnologyType.MeleeArmor2: prereqMet = sim.HasTechnology(playerId, TechnologyType.MeleeArmor1); break;
                case TechnologyType.RangedAttack2: prereqMet = sim.HasTechnology(playerId, TechnologyType.RangedAttack1); break;
                case TechnologyType.RangedArmor2: prereqMet = sim.HasTechnology(playerId, TechnologyType.RangedArmor1); break;
            }

            // Age check
            int reqAge = tech <= TechnologyType.RangedArmor1 ? 2 : 3;
            bool ageOk = sim.GetPlayerAge(playerId) >= reqAge;

            string costStr = "";
            if (foodCost > 0) costStr += $"{foodCost} <sprite name=\"food\"> ";
            if (goldCost > 0) costStr += $"{goldCost} <sprite name=\"gold\">";

            string tooltip = $"<b>{label.Replace("\n", " ")}</b>\n{desc}\nCost: {costStr}";
            if (done) tooltip += "\n<i>(Already researched)</i>";
            else if (!ageOk) tooltip += $"\n<color=#FF6666>Requires Age {LandmarkDefinitions.AgeToRoman(reqAge)}</color>";
            else if (!prereqMet) tooltip += "\n<color=#FF6666>Requires previous tier</color>";

            return new GridButton
            {
                Label = label,
                Hotkey = hotkey,
                Tooltip = tooltip,
                Enabled = !done && canAfford && prereqMet && ageOk,
                OnClick = () => sim.CommandBuffer.EnqueueCommand(new ResearchCommand(playerId, buildingId, tech))
            };
        }

        private GridButton MakeBuildSlot(string label, string hotkey, string desc, BuildingType type,
            int woodCost, int stoneCost, int foodCost, int goldCost,
            PlayerResources resources, int playerAge, bool isWall, bool isGate = false)
        {
            int reqAge = LandmarkDefinitions.GetBuildingRequiredAge(type);
            bool ageOk = playerAge >= reqAge;
            bool canAfford = resources.Wood >= woodCost && resources.Stone >= stoneCost
                          && resources.Food >= foodCost && resources.Gold >= goldCost;

            // Build cost string
            string costStr = "";
            if (woodCost > 0) costStr += $"{woodCost} <sprite name=\"wood\"> ";
            if (stoneCost > 0) costStr += $"{stoneCost} <sprite name=\"stone\"> ";
            if (foodCost > 0) costStr += $"{foodCost} <sprite name=\"food\"> ";
            if (goldCost > 0) costStr += $"{goldCost} <sprite name=\"gold\"> ";
            costStr = costStr.TrimEnd();

            string tooltip = $"<b>{label.Replace("\n", " ")}</b>\n{desc}\nCost: {costStr}";
            if (!ageOk)
                tooltip += $"\n<color=#FF6666>Requires Age {LandmarkDefinitions.AgeToRoman(reqAge)}</color>";

            System.Action onClick;
            if (isWall)
                onClick = () => selectionManager.EnterWallPlacement(type, isGate);
            else
                onClick = () => selectionManager.EnterBuildPlacement(type);

            return new GridButton
            {
                Label = label,
                Hotkey = hotkey,
                Tooltip = tooltip,
                Enabled = ageOk && canAfford,
                Icon = BuildingIcons.Get(type),
                OnClick = onClick
            };
        }

        private GridButton?[] GetMultiUnitActionSlots(IReadOnlyList<UnitView> selected, GameSimulation sim)
        {
            bool allOwned = true;
            bool allOwnedVillagers = true;
            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i].PlayerId != selectionManager.LocalPlayerId)
                { allOwned = false; allOwnedVillagers = false; break; }
                if (selected[i].UnitType != 0)
                    allOwnedVillagers = false;
            }
            if (!allOwned) return null;

            CommandIcons.EnsureLoaded();
            var slots = new GridButton?[12];

            // Q/W/E = Build age tabs (only if all villagers)
            if (allOwnedVillagers)
            {
                slots[0] = new GridButton { Label = "Build I", Hotkey = "Q",
                    Tooltip = "<b>Build (Age I)</b>\nOpen Age I build menu.",
                    Enabled = true,
                    OnClick = () => { buildMenuAge = 1; } };
                slots[1] = new GridButton { Label = "Build II", Hotkey = "W",
                    Tooltip = "<b>Build (Age II)</b>\nOpen Age II build menu.",
                    Enabled = true,
                    OnClick = () => { buildMenuAge = 2; } };
                slots[2] = new GridButton { Label = "Build III", Hotkey = "E",
                    Tooltip = "<b>Build (Age III)</b>\nOpen Age III build menu.",
                    Enabled = true,
                    OnClick = () => { buildMenuAge = 3; } };

                // R = Age Up (villagers only)
                int localPid = selectionManager.LocalPlayerId;
                int currentAge = sim.GetPlayerAge(localPid);
                bool isAgingUp = sim.IsPlayerAgingUp(localPid);
                if (currentAge < 3 && !isAgingUp)
                {
                    int targetAge = currentAge + 1;
                    var civ = sim.GetPlayerCivilization(localPid);
                    var (choiceA, choiceB) = LandmarkDefinitions.GetChoices(civ, targetAge);
                    var defA = LandmarkDefinitions.Get(choiceA);
                    var defB = LandmarkDefinitions.Get(choiceB);
                    int minFood = Mathf.Min(defA.FoodCost, defB.FoodCost);
                    int minGold = Mathf.Min(defA.GoldCost, defB.GoldCost);
                    var resources = sim.ResourceManager.GetPlayerResources(localPid);
                    bool canAfford = resources.Food >= minFood && resources.Gold >= minGold;
                    string ageRoman = LandmarkDefinitions.AgeToRoman(targetAge);
                    slots[3] = new GridButton { Label = $"Age {ageRoman}", Hotkey = "R",
                        Enabled = canAfford,
                        Tooltip = $"<b>Advance to Age {ageRoman}</b>\nChoose a landmark to construct.\nCost: {minFood} <sprite name=\"food\"> {minGold} <sprite name=\"gold\">",
                        OnClick = () => ShowLandmarkChoicePanel(sim, localPid, targetAge) };
                }
                else if (isAgingUp)
                {
                    string ageRoman = LandmarkDefinitions.AgeToRoman(currentAge + 1);
                    slots[3] = new GridButton { Label = $"Age {ageRoman}", Hotkey = "R",
                        Enabled = false,
                        Tooltip = $"<b>Advancing to Age {ageRoman}</b>\nLandmark under construction..." };
                }
                else if (currentAge >= 3)
                {
                    var res = sim.ResourceManager.GetPlayerResources(localPid);
                    var cfg = sim.Config;
                    bool canAfford = res.Wood >= cfg.WonderWoodCost && res.Stone >= cfg.WonderStoneCost
                                  && res.Food >= cfg.WonderFoodCost && res.Gold >= cfg.WonderGoldCost;
                    string costStr = $"{cfg.WonderFoodCost} <sprite name=\"food\"> {cfg.WonderWoodCost} <sprite name=\"wood\"> {cfg.WonderGoldCost} <sprite name=\"gold\"> {cfg.WonderStoneCost} <sprite name=\"stone\">";
                    slots[3] = new GridButton { Label = "Wonder", Hotkey = "R",
                        Enabled = canAfford,
                        Icon = BuildingIcons.Get(BuildingType.Wonder),
                        Tooltip = $"<b>Wonder</b>\nBuild to achieve a wonder victory.\nCost: {costStr}",
                        OnClick = () => selectionManager.EnterBuildPlacement(BuildingType.Wonder) };
                }
            }

            // A = Attack Move
            slots[4] = new GridButton { Label = "Attack\nMove", Hotkey = "A",
                Icon = CommandIcons.Attack,
                Tooltip = "<b>Attack Move</b>\nMove to a location, attacking enemies along the way.",
                Enabled = true,
                OnClick = () => selectionManager.EnterAttackMoveMode() };

            // S = Halt
            slots[5] = new GridButton { Label = "Halt", Hotkey = "S",
                Icon = CommandIcons.Guard,
                Tooltip = "<b>Halt</b>\nStop all current actions.",
                Enabled = true,
                OnClick = () => {
                    var units = selectionManager.SelectedUnits;
                    int[] unitIds = new int[units.Count];
                    for (int i = 0; i < units.Count; i++) unitIds[i] = units[i].UnitId;
                    sim.CommandBuffer.EnqueueCommand(new StopCommand(selectionManager.LocalPlayerId, unitIds));
                } };

            // D = Patrol
            slots[6] = new GridButton { Label = "Patrol", Hotkey = "D",
                Icon = CommandIcons.Patrol,
                Tooltip = "<b>Patrol</b>\nClick to set patrol destination. Shift+click adds waypoints.",
                Enabled = true,
                OnClick = () => selectionManager.EnterPatrolMode() };

            // F = Garrison
            slots[7] = new GridButton { Label = "Garrison", Hotkey = "F",
                Icon = CommandIcons.Garrison,
                Tooltip = "<b>Garrison</b>\nClick on a building to garrison selected units.",
                Enabled = true,
                OnClick = () => selectionManager.EnterGarrisonMode() };

            // X = Delete (hold to confirm)
            slots[9] = new GridButton { Label = "Delete", Hotkey = "X",
                Tooltip = "<b>Delete</b>\nHold to destroy selected units.",
                Enabled = true };

            return slots;
        }

        private GridButton?[] GetBuildingActionSlots(BuildingView view, GameSimulation sim)
        {
            var building = sim.BuildingRegistry.GetBuilding(view.BuildingId);
            if (building == null) return null;
            if (building.PlayerId != selectionManager.LocalPlayerId) return null;

            var slots = new GridButton?[12];
            bool hasAny = false;

            if (building.Type == BuildingType.Wall || building.Type == BuildingType.StoneWall || building.Type == BuildingType.StoneGate || building.Type == BuildingType.WoodGate)
            {
                string gateLabel = building.IsGate ? "To Wall" : "To Gate";
                slots[0] = new GridButton { Label = gateLabel, Hotkey = "Q", Enabled = true,
                    Tooltip = "<b>Toggle Gate</b>\nConvert between wall and gate.",
                    OnClick = () => sim.CommandBuffer.EnqueueCommand(
                        new ConvertToGateCommand(building.PlayerId, building.Id)) };
                hasAny = true;
            }
            else if (!building.IsUnderConstruction)
            {
                var resources = sim.ResourceManager.GetPlayerResources(building.PlayerId);
                bool hasLandmarkDiscount = sim.IsBuildingInFrenchLandmarkInfluence(building);
                int discPct = hasLandmarkDiscount ? sim.Config.FrenchLandmarkTrainingDiscountPercent : 0;

                if (building.Type == BuildingType.Barracks)
                {
                    var civ = sim.GetPlayerCivilization(building.PlayerId);
                    bool isLandsknecht = civ == Civilization.HolyRomanEmpire;
                    string spLabel = isLandsknecht ? "Lands." : "Spear";
                    string spName = isLandsknecht ? "Landsknecht" : "Spearman";
                    int spIcon = isLandsknecht ? 8 : 1;
                    int spearmanFood = (isLandsknecht ? sim.Config.LandsknechtFoodCost : sim.Config.SpearmanFoodCost) * (100 - discPct) / 100;
                    int spearmanWood = (isLandsknecht ? sim.Config.LandsknechtWoodCost : sim.Config.SpearmanWoodCost) * (100 - discPct) / 100;
                    slots[0] = new GridButton { Label = spLabel, Hotkey = "Q",
                        Enabled = resources.Food >= spearmanFood && resources.Wood >= spearmanWood,
                        Icon = UnitIcons.Get(spIcon),
                        Tooltip = $"<b>{spName}</b>\nMelee infantry unit.\nCost: {spearmanFood} <sprite name=\"food\"> {spearmanWood} <sprite name=\"wood\">",
                        OnClick = () => sim.CommandBuffer.EnqueueCommand(
                            new TrainUnitCommand(building.PlayerId, building.Id, 1)) };
                    int maaFood = sim.Config.ManAtArmsFoodCost * (100 - discPct) / 100;
                    int maaGold = sim.Config.ManAtArmsGoldCost * (100 - discPct) / 100;
                    bool maaAgeOk = sim.GetPlayerAge(building.PlayerId) >= LandmarkDefinitions.GetUnitRequiredAge(6);
                    slots[1] = new GridButton { Label = "MAA", Hotkey = "W",
                        Enabled = maaAgeOk && resources.Food >= maaFood && resources.Gold >= maaGold,
                        Icon = UnitIcons.Get(6),
                        Tooltip = $"<b>Man-at-Arms</b>\nHeavy armored infantry.\nCost: {maaFood} <sprite name=\"food\"> {maaGold} <sprite name=\"gold\">"
                            + (maaAgeOk ? "" : $"\n<color=#FF6666>Requires Age {LandmarkDefinitions.AgeToRoman(3)}</color>"),
                        OnClick = () => sim.CommandBuffer.EnqueueCommand(
                            new TrainUnitCommand(building.PlayerId, building.Id, 6)) };
                    hasAny = true;
                }
                else if (building.Type == BuildingType.ArcheryRange)
                {
                    var civ = sim.GetPlayerCivilization(building.PlayerId);
                    bool isLongbow = civ == Civilization.English;
                    string arLabel = isLongbow ? "Longbow" : "Archer";
                    string arName = isLongbow ? "Longbowman" : "Archer";
                    int arIcon = isLongbow ? 6 : 2;
                    int archerFood = (isLongbow ? sim.Config.LongbowmanFoodCost : sim.Config.ArcherFoodCost) * (100 - discPct) / 100;
                    int archerWood = (isLongbow ? sim.Config.LongbowmanWoodCost : sim.Config.ArcherWoodCost) * (100 - discPct) / 100;
                    slots[0] = new GridButton { Label = arLabel, Hotkey = "Q",
                        Enabled = resources.Food >= archerFood && resources.Wood >= archerWood,
                        Icon = UnitIcons.Get(arIcon),
                        Tooltip = $"<b>{arName}</b>\nRanged infantry unit.\nCost: {archerFood} <sprite name=\"food\"> {archerWood} <sprite name=\"wood\">",
                        OnClick = () => sim.CommandBuffer.EnqueueCommand(
                            new TrainUnitCommand(building.PlayerId, building.Id, 2)) };
                    int xbowFood = sim.Config.CrossbowmanFoodCost * (100 - discPct) / 100;
                    int xbowGold = sim.Config.CrossbowmanGoldCost * (100 - discPct) / 100;
                    bool xbowAgeOk = sim.GetPlayerAge(building.PlayerId) >= LandmarkDefinitions.GetUnitRequiredAge(8);
                    slots[1] = new GridButton { Label = "Xbow", Hotkey = "W",
                        Enabled = xbowAgeOk && resources.Food >= xbowFood && resources.Gold >= xbowGold,
                        Icon = UnitIcons.Get(8),
                        Tooltip = $"<b>Crossbowman</b>\nRanged anti-armor unit.\nCost: {xbowFood} <sprite name=\"food\"> {xbowGold} <sprite name=\"gold\">"
                            + (xbowAgeOk ? "" : $"\n<color=#FF6666>Requires Age {LandmarkDefinitions.AgeToRoman(3)}</color>"),
                        OnClick = () => sim.CommandBuffer.EnqueueCommand(
                            new TrainUnitCommand(building.PlayerId, building.Id, 8)) };
                    hasAny = true;
                }
                else if (building.Type == BuildingType.Stables)
                {
                    var civ = sim.GetPlayerCivilization(building.PlayerId);
                    bool isGendarme = civ == Civilization.French;
                    string hrLabel = isGendarme ? "Gendrm" : "Horse";
                    string hrName = isGendarme ? "Gendarme" : "Horseman";
                    int hrIcon = isGendarme ? 7 : 3;
                    int horsemanFood = (isGendarme ? sim.Config.GendarmeFoodCost : sim.Config.HorsemanFoodCost) * (100 - discPct) / 100;
                    int horsemanWood = (isGendarme ? sim.Config.GendarmeWoodCost : sim.Config.HorsemanWoodCost) * (100 - discPct) / 100;
                    slots[0] = new GridButton { Label = hrLabel, Hotkey = "Q",
                        Enabled = resources.Food >= horsemanFood && resources.Wood >= horsemanWood,
                        Icon = UnitIcons.Get(hrIcon),
                        Tooltip = $"<b>{hrName}</b>\nMounted melee unit.\nCost: {horsemanFood} <sprite name=\"food\"> {horsemanWood} <sprite name=\"wood\">",
                        OnClick = () => sim.CommandBuffer.EnqueueCommand(
                            new TrainUnitCommand(building.PlayerId, building.Id, 3)) };
                    int knightFood = sim.Config.KnightFoodCost * (100 - discPct) / 100;
                    int knightGold = sim.Config.KnightGoldCost * (100 - discPct) / 100;
                    bool knightAgeOk = sim.GetPlayerAge(building.PlayerId) >= LandmarkDefinitions.GetUnitRequiredAge(7);
                    slots[1] = new GridButton { Label = "Knight", Hotkey = "W",
                        Enabled = knightAgeOk && resources.Food >= knightFood && resources.Gold >= knightGold,
                        Icon = UnitIcons.Get(7),
                        Tooltip = $"<b>Knight</b>\nHeavy armored cavalry.\nCost: {knightFood} <sprite name=\"food\"> {knightGold} <sprite name=\"gold\">"
                            + (knightAgeOk ? "" : $"\n<color=#FF6666>Requires Age {LandmarkDefinitions.AgeToRoman(3)}</color>"),
                        OnClick = () => sim.CommandBuffer.EnqueueCommand(
                            new TrainUnitCommand(building.PlayerId, building.Id, 7)) };
                    int scoutFood = sim.Config.ScoutFoodCost * (100 - discPct) / 100;
                    slots[2] = new GridButton { Label = "Scout", Hotkey = "E",
                        Enabled = resources.Food >= scoutFood,
                        Icon = UnitIcons.Get(4),
                        Tooltip = $"<b>Scout</b>\nFast mounted unit with high vision.\nCost: {scoutFood} <sprite name=\"food\">",
                        OnClick = () => sim.CommandBuffer.EnqueueCommand(
                            new TrainUnitCommand(building.PlayerId, building.Id, 4)) };
                    hasAny = true;
                }
                else if (building.Type == BuildingType.Monastery)
                {
                    int monkFood = sim.Config.MonkFoodCost * (100 - discPct) / 100;
                    int monkGold = sim.Config.MonkGoldCost * (100 - discPct) / 100;
                    bool monkAgeOk = sim.GetPlayerAge(building.PlayerId) >= LandmarkDefinitions.GetUnitRequiredAge(9);
                    slots[0] = new GridButton { Label = "Monk", Hotkey = "Q",
                        Enabled = monkAgeOk && resources.Food >= monkFood && resources.Gold >= monkGold,
                        Icon = UnitIcons.Get(9),
                        Tooltip = $"<b>Monk</b>\nHealer unit. Automatically heals nearby friendly units.\nCost: {monkFood} <sprite name=\"food\"> {monkGold} <sprite name=\"gold\">"
                            + (monkAgeOk ? "" : $"\n<color=#FF6666>Requires Age {LandmarkDefinitions.AgeToRoman(3)}</color>"),
                        OnClick = () => sim.CommandBuffer.EnqueueCommand(
                            new TrainUnitCommand(building.PlayerId, building.Id, 9)) };
                    hasAny = true;
                }
                else if (building.Type == BuildingType.SiegeWorkshop)
                {
                    int ramWood = sim.Config.BatteringRamWoodCost;
                    int ramGold = sim.Config.BatteringRamGoldCost;
                    int mangWood = sim.Config.MangonelWoodCost;
                    int mangGold = sim.Config.MangonelGoldCost;
                    int trebWood = sim.Config.TrebuchetWoodCost;
                    int trebGold = sim.Config.TrebuchetGoldCost;

                    slots[0] = new GridButton { Label = "Ram", Hotkey = "Q",
                        Enabled = resources.Wood >= ramWood && resources.Gold >= ramGold,
                        Tooltip = $"<b>Battering Ram</b>\nSlow melee siege unit. Devastating vs buildings.\nCost: {ramWood} <sprite name=\"wood\"> {ramGold} <sprite name=\"gold\">",
                        OnClick = () => sim.CommandBuffer.EnqueueCommand(
                            new TrainUnitCommand(building.PlayerId, building.Id, 13)) };
                    slots[1] = new GridButton { Label = "Mangonel", Hotkey = "W",
                        Enabled = resources.Wood >= mangWood && resources.Gold >= mangGold,
                        Tooltip = $"<b>Mangonel</b>\nRanged siege unit. Good vs groups and buildings.\nCost: {mangWood} <sprite name=\"wood\"> {mangGold} <sprite name=\"gold\">",
                        OnClick = () => sim.CommandBuffer.EnqueueCommand(
                            new TrainUnitCommand(building.PlayerId, building.Id, 14)) };
                    slots[2] = new GridButton { Label = "Trebuchet", Hotkey = "E",
                        Enabled = resources.Wood >= trebWood && resources.Gold >= trebGold,
                        Tooltip = $"<b>Trebuchet</b>\nLong-range siege unit. Extremely powerful vs buildings.\nCost: {trebWood} <sprite name=\"wood\"> {trebGold} <sprite name=\"gold\">",
                        OnClick = () => sim.CommandBuffer.EnqueueCommand(
                            new TrainUnitCommand(building.PlayerId, building.Id, 15)) };
                    hasAny = true;
                }
                else if (building.Type == BuildingType.TownCenter)
                {
                    int villagerCost = sim.Config.VillagerFoodCost * (100 - discPct) / 100;
                    slots[0] = new GridButton { Label = "Villager", Hotkey = "Q",
                        Enabled = resources.Food >= villagerCost,
                        Icon = UnitIcons.Get(0),
                        Tooltip = $"<b>Villager</b>\nGathers resources and constructs buildings.\nCost: {villagerCost} <sprite name=\"food\">",
                        OnClick = () => sim.CommandBuffer.EnqueueCommand(
                            new TrainUnitCommand(building.PlayerId, building.Id, 0)) };
                    int scoutFood = sim.Config.ScoutFoodCost * (100 - discPct) / 100;
                    slots[1] = new GridButton { Label = "Scout", Hotkey = "W",
                        Enabled = resources.Food >= scoutFood,
                        Icon = UnitIcons.Get(4),
                        Tooltip = $"<b>Scout</b>\nFast mounted unit with high vision.\nCost: {scoutFood} <sprite name=\"food\">",
                        OnClick = () => sim.CommandBuffer.EnqueueCommand(
                            new TrainUnitCommand(building.PlayerId, building.Id, 4)) };

                    // Age Up button
                    int currentAge = sim.GetPlayerAge(building.PlayerId);
                    bool isAgingUp = sim.IsPlayerAgingUp(building.PlayerId);
                    if (currentAge < 3 && !isAgingUp)
                    {
                        int targetAge = currentAge + 1;
                        var civ = sim.GetPlayerCivilization(building.PlayerId);
                        var (choiceA, choiceB) = LandmarkDefinitions.GetChoices(civ, targetAge);
                        var defA = LandmarkDefinitions.Get(choiceA);
                        var defB = LandmarkDefinitions.Get(choiceB);
                        int minFood = Mathf.Min(defA.FoodCost, defB.FoodCost);
                        int minGold = Mathf.Min(defA.GoldCost, defB.GoldCost);
                        bool canAfford = resources.Food >= minFood && resources.Gold >= minGold;
                        string ageRoman = LandmarkDefinitions.AgeToRoman(targetAge);
                        slots[4] = new GridButton { Label = $"Age {ageRoman}", Hotkey = "A",
                            Enabled = canAfford,
                            Tooltip = $"<b>Advance to Age {ageRoman}</b>\nChoose a landmark to construct.\nCost: {minFood} <sprite name=\"food\"> {minGold} <sprite name=\"gold\">",
                            OnClick = () => ShowLandmarkChoicePanel(sim, building.PlayerId, targetAge) };
                    }
                    else if (isAgingUp)
                    {
                        string ageRoman = LandmarkDefinitions.AgeToRoman(currentAge + 1);
                        slots[4] = new GridButton { Label = $"Age {ageRoman}", Hotkey = "A",
                            Enabled = false,
                            Tooltip = $"<b>Advancing to Age {ageRoman}</b>\nLandmark under construction..." };
                    }
                    else if (currentAge >= 3)
                    {
                        var cfg = sim.Config;
                        bool canAfford = resources.Wood >= cfg.WonderWoodCost && resources.Stone >= cfg.WonderStoneCost
                                      && resources.Food >= cfg.WonderFoodCost && resources.Gold >= cfg.WonderGoldCost;
                        string costStr = $"{cfg.WonderFoodCost} <sprite name=\"food\"> {cfg.WonderWoodCost} <sprite name=\"wood\"> {cfg.WonderGoldCost} <sprite name=\"gold\"> {cfg.WonderStoneCost} <sprite name=\"stone\">";
                        slots[4] = new GridButton { Label = "Wonder", Hotkey = "A",
                            Enabled = canAfford,
                            Icon = BuildingIcons.Get(BuildingType.Wonder),
                            Tooltip = $"<b>Wonder</b>\nBuild to achieve a wonder victory.\nCost: {costStr}",
                            OnClick = () => selectionManager.EnterBuildPlacement(BuildingType.Wonder) };
                    }
                    hasAny = true;
                }
                else if (building.Type == BuildingType.Tower)
                {
                    int arrowCost = sim.Config.ArrowSlitsWoodCost;
                    int cannonCost = sim.Config.CannonEmplacementWoodCost;
                    int stoneCost = sim.Config.StoneUpgradeWoodCost;
                    int visionCost = sim.Config.VisionUpgradeWoodCost;

                    bool arrowDone = building.HasArrowSlits || building.UpgradeQueue.Contains(TowerUpgradeType.ArrowSlits);
                    bool cannonDone = building.HasCannonEmplacement || building.UpgradeQueue.Contains(TowerUpgradeType.CannonEmplacement);
                    bool stoneDone = building.HasStoneUpgrade || building.UpgradeQueue.Contains(TowerUpgradeType.StoneUpgrade);
                    bool visionDone = building.HasVisionUpgrade || building.UpgradeQueue.Contains(TowerUpgradeType.VisionUpgrade);

                    slots[0] = new GridButton { Label = "Arrows", Hotkey = "Q",
                        Enabled = !arrowDone && resources.Wood >= arrowCost,
                        Tooltip = $"<b>Arrow Slits</b>\nIncreases arrows fired.\nCost: {arrowCost} <sprite name=\"wood\">" + (arrowDone ? "\n<i>(Already applied or queued)</i>" : ""),
                        OnClick = () => sim.CommandBuffer.EnqueueCommand(
                            new UpgradeTowerCommand(building.PlayerId, building.Id, TowerUpgradeType.ArrowSlits)) };
                    slots[1] = new GridButton { Label = "Cannon", Hotkey = "W",
                        Enabled = !cannonDone && resources.Wood >= cannonCost,
                        Tooltip = $"<b>Cannon Emplacement</b>\nReplaces arrows with cannons.\nCost: {cannonCost} <sprite name=\"wood\">" + (cannonDone ? "\n<i>(Already applied or queued)</i>" : ""),
                        OnClick = () => sim.CommandBuffer.EnqueueCommand(
                            new UpgradeTowerCommand(building.PlayerId, building.Id, TowerUpgradeType.CannonEmplacement)) };
                    slots[2] = new GridButton { Label = "Stone", Hotkey = "E",
                        Enabled = !stoneDone && resources.Wood >= stoneCost,
                        Tooltip = $"<b>Stone Upgrade</b>\nIncreases health and armor.\nCost: {stoneCost} <sprite name=\"wood\">" + (stoneDone ? "\n<i>(Already applied or queued)</i>" : ""),
                        OnClick = () => sim.CommandBuffer.EnqueueCommand(
                            new UpgradeTowerCommand(building.PlayerId, building.Id, TowerUpgradeType.StoneUpgrade)) };
                    slots[3] = new GridButton { Label = "Vision", Hotkey = "R",
                        Enabled = !visionDone && resources.Wood >= visionCost,
                        Tooltip = $"<b>Vision Upgrade</b>\nIncreases vision/detection range.\nCost: {visionCost} <sprite name=\"wood\">" + (visionDone ? "\n<i>(Already applied or queued)</i>" : ""),
                        OnClick = () => sim.CommandBuffer.EnqueueCommand(
                            new UpgradeTowerCommand(building.PlayerId, building.Id, TowerUpgradeType.VisionUpgrade)) };
                    hasAny = true;
                }
                else if (building.Type == BuildingType.Market)
                {
                    int pid = building.PlayerId;
                    int buyFood = sim.GetMarketBuyPrice(pid, ResourceType.Food);
                    int buyWood = sim.GetMarketBuyPrice(pid, ResourceType.Wood);
                    int buyStone = sim.GetMarketBuyPrice(pid, ResourceType.Stone);
                    int sellFood = sim.GetMarketSellPrice(pid, ResourceType.Food);
                    int sellWood = sim.GetMarketSellPrice(pid, ResourceType.Wood);
                    int sellStone = sim.GetMarketSellPrice(pid, ResourceType.Stone);
                    int tradeAmt = sim.Config.MarketTradeAmount;

                    slots[0] = new GridButton { Label = $"Buy\nFood", Hotkey = "Q",
                        Enabled = resources.Gold >= buyFood,
                        Tooltip = $"<b>Buy Food</b>\nBuy {tradeAmt} food.\nCost: {buyFood} <sprite name=\"gold\">",
                        OnClick = () => sim.CommandBuffer.EnqueueCommand(
                            new MarketTradeCommand(pid, (int)ResourceType.Food, true)) };
                    slots[1] = new GridButton { Label = $"Buy\nWood", Hotkey = "W",
                        Enabled = resources.Gold >= buyWood,
                        Tooltip = $"<b>Buy Wood</b>\nBuy {tradeAmt} wood.\nCost: {buyWood} <sprite name=\"gold\">",
                        OnClick = () => sim.CommandBuffer.EnqueueCommand(
                            new MarketTradeCommand(pid, (int)ResourceType.Wood, true)) };
                    slots[2] = new GridButton { Label = $"Buy\nStone", Hotkey = "E",
                        Enabled = resources.Gold >= buyStone,
                        Tooltip = $"<b>Buy Stone</b>\nBuy {tradeAmt} stone.\nCost: {buyStone} <sprite name=\"gold\">",
                        OnClick = () => sim.CommandBuffer.EnqueueCommand(
                            new MarketTradeCommand(pid, (int)ResourceType.Stone, true)) };
                    slots[4] = new GridButton { Label = $"Sell\nFood", Hotkey = "A",
                        Enabled = resources.Food >= tradeAmt,
                        Tooltip = $"<b>Sell Food</b>\nSell {tradeAmt} food.\nGain: {sellFood} <sprite name=\"gold\">",
                        OnClick = () => sim.CommandBuffer.EnqueueCommand(
                            new MarketTradeCommand(pid, (int)ResourceType.Food, false)) };
                    slots[5] = new GridButton { Label = $"Sell\nWood", Hotkey = "S",
                        Enabled = resources.Wood >= tradeAmt,
                        Tooltip = $"<b>Sell Wood</b>\nSell {tradeAmt} wood.\nGain: {sellWood} <sprite name=\"gold\">",
                        OnClick = () => sim.CommandBuffer.EnqueueCommand(
                            new MarketTradeCommand(pid, (int)ResourceType.Wood, false)) };
                    slots[6] = new GridButton { Label = $"Sell\nStone", Hotkey = "D",
                        Enabled = resources.Stone >= tradeAmt,
                        Tooltip = $"<b>Sell Stone</b>\nSell {tradeAmt} stone.\nGain: {sellStone} <sprite name=\"gold\">",
                        OnClick = () => sim.CommandBuffer.EnqueueCommand(
                            new MarketTradeCommand(pid, (int)ResourceType.Stone, false)) };
                    hasAny = true;
                }
                else if (building.Type == BuildingType.Blacksmith)
                {
                    int pid = building.PlayerId;
                    int bid = building.Id;
                    var cfg = sim.Config;
                    slots[0] = MakeResearchButton(sim, pid, bid, TechnologyType.MeleeAttack1, "Melee\nAttack I", "Q",
                        "Increases melee unit attack damage.", 0, cfg.MeleeAttack1Cost, resources);
                    slots[1] = MakeResearchButton(sim, pid, bid, TechnologyType.MeleeArmor1, "Melee\nArmor I", "W",
                        "Increases melee armor for all units.", 0, cfg.MeleeArmor1Cost, resources);
                    slots[2] = MakeResearchButton(sim, pid, bid, TechnologyType.RangedAttack1, "Ranged\nAttack I", "E",
                        "Increases ranged unit attack damage.", 0, cfg.RangedAttack1Cost, resources);
                    slots[3] = MakeResearchButton(sim, pid, bid, TechnologyType.RangedArmor1, "Ranged\nArmor I", "R",
                        "Increases ranged armor for all units.", 0, cfg.RangedArmor1Cost, resources);
                    slots[4] = MakeResearchButton(sim, pid, bid, TechnologyType.MeleeAttack2, "Melee\nAttack II", "A",
                        "Further increases melee attack damage.", 0, cfg.MeleeAttack2Cost, resources);
                    slots[5] = MakeResearchButton(sim, pid, bid, TechnologyType.MeleeArmor2, "Melee\nArmor II", "S",
                        "Further increases melee armor.", 0, cfg.MeleeArmor2Cost, resources);
                    slots[6] = MakeResearchButton(sim, pid, bid, TechnologyType.RangedAttack2, "Ranged\nAttack II", "D",
                        "Further increases ranged attack damage.", 0, cfg.RangedAttack2Cost, resources);
                    slots[7] = MakeResearchButton(sim, pid, bid, TechnologyType.RangedArmor2, "Ranged\nArmor II", "F",
                        "Further increases ranged armor.", 0, cfg.RangedArmor2Cost, resources);
                    hasAny = true;
                }
                else if (building.Type == BuildingType.University)
                {
                    int pid = building.PlayerId;
                    int bid = building.Id;
                    var cfg = sim.Config;
                    slots[0] = MakeResearchButton(sim, pid, bid, TechnologyType.Ballistics, "Ballistics", "Q",
                        "Projectiles lead moving targets.", cfg.BallisticsFoodCost, cfg.BallisticsGoldCost, resources);
                    slots[1] = MakeResearchButton(sim, pid, bid, TechnologyType.SiegeEngineering, "Siege\nEngr.", "W",
                        "Increases siege unit HP.", cfg.SiegeEngineeringFoodCost, cfg.SiegeEngineeringGoldCost, resources);
                    slots[2] = MakeResearchButton(sim, pid, bid, TechnologyType.Chemistry, "Chemistry", "E",
                        "Increases ranged attack damage.", cfg.ChemistryFoodCost, cfg.ChemistryGoldCost, resources);
                    slots[3] = MakeResearchButton(sim, pid, bid, TechnologyType.MurderHoles, "Murder\nHoles", "R",
                        "Towers have no minimum range.", cfg.MurderHolesFoodCost, cfg.MurderHolesGoldCost, resources);
                    hasAny = true;
                }
            }

            // Ungarrison at slot 7 (F) — any building with garrisoned units
            if (building.GarrisonCount > 0)
            {
                CommandIcons.EnsureLoaded();
                int bid = building.Id;
                slots[7] = new GridButton { Label = "Ungarrison", Hotkey = "F",
                    Icon = CommandIcons.Garrison,
                    Tooltip = $"<b>Ungarrison All</b>\nEject all {building.GarrisonCount} garrisoned units.",
                    Enabled = true,
                    OnClick = () => sim.CommandBuffer.EnqueueCommand(
                        new UngarrisonCommand(building.PlayerId, bid)) };
                hasAny = true;
            }

            // X = Delete (hold to confirm)
            slots[9] = new GridButton { Label = "Delete", Hotkey = "X",
                Tooltip = "<b>Delete</b>\nHold to destroy selected building.",
                Enabled = true };
            hasAny = true;

            return hasAny ? slots : null;
        }

        private void ShowLandmarkChoicePanel(GameSimulation sim, int playerId, int targetAge)
        {
            CloseLandmarkChoicePanel();

            var civ = sim.GetPlayerCivilization(playerId);
            var (choiceA, choiceB) = LandmarkDefinitions.GetChoices(civ, targetAge);
            var defA = LandmarkDefinitions.Get(choiceA);
            var defB = LandmarkDefinitions.Get(choiceB);
            var resources = sim.ResourceManager.GetPlayerResources(playerId);

            float panelW = 420f;
            float panelH = 180f;
            float btnW = 190f;
            float btnH = 140f;

            landmarkPanelGO = new GameObject("LandmarkChoicePanel");
            landmarkPanelGO.transform.SetParent(canvasTransform, false);
            var panelRT = landmarkPanelGO.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(panelW, panelH);
            var panelImg = landmarkPanelGO.AddComponent<Image>();
            panelImg.color = new Color(0.05f, 0.05f, 0.05f, 0.95f);
            var panelOutline = landmarkPanelGO.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            panelOutline.effectDistance = new Vector2(2, -2);

            // Title
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(landmarkPanelGO.transform, false);
            var titleRT = titleGO.AddComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 1);
            titleRT.anchorMax = new Vector2(1, 1);
            titleRT.pivot = new Vector2(0.5f, 1);
            titleRT.anchoredPosition = new Vector2(0, -4);
            titleRT.sizeDelta = new Vector2(0, 24);
            var titleText = titleGO.AddComponent<TextMeshProUGUI>();
            titleText.text = $"Choose Landmark - Age {LandmarkDefinitions.AgeToRoman(targetAge)}";
            titleText.fontSize = 16;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.Center;

            bool canAffordA = resources.Food >= defA.FoodCost && resources.Gold >= defA.GoldCost;
            bool canAffordB = resources.Food >= defB.FoodCost && resources.Gold >= defB.GoldCost;
            var capturedChoiceA = choiceA;
            var capturedChoiceB = choiceB;
            landmarkChoiceACallback = canAffordA ? () => { CloseLandmarkChoicePanel(); selectionManager.EnterLandmarkPlacement(capturedChoiceA); } : null;
            landmarkChoiceBCallback = canAffordB ? () => { CloseLandmarkChoicePanel(); selectionManager.EnterLandmarkPlacement(capturedChoiceB); } : null;

            // Choice A button (left)
            CreateLandmarkChoiceButton(landmarkPanelGO.transform, defA, choiceA, resources,
                new Vector2(-btnW / 2 - 5, -20), new Vector2(btnW, btnH), "Q");

            // Choice B button (right)
            CreateLandmarkChoiceButton(landmarkPanelGO.transform, defB, choiceB, resources,
                new Vector2(btnW / 2 + 5, -20), new Vector2(btnW, btnH), "W");
        }

        private void CreateLandmarkChoiceButton(Transform parent, LandmarkDefinition def, LandmarkId landmarkId,
            PlayerResources resources, Vector2 position, Vector2 size, string hotkeyLabel = null)
        {
            bool canAfford = resources.Food >= def.FoodCost && resources.Gold >= def.GoldCost;

            var btnGO = new GameObject($"Choice_{landmarkId}");
            btnGO.transform.SetParent(parent, false);
            var btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 1);
            btnRT.anchorMax = new Vector2(0.5f, 1);
            btnRT.pivot = new Vector2(0.5f, 1);
            btnRT.anchoredPosition = position;
            btnRT.sizeDelta = size;
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = canAfford ? new Color(0.2f, 0.2f, 0.2f, 1f) : new Color(0.15f, 0.1f, 0.1f, 1f);
            var btn = btnGO.AddComponent<Button>();
            btn.interactable = canAfford;

            var capturedId = landmarkId;
            btn.onClick.AddListener(() =>
            {
                CloseLandmarkChoicePanel();
                selectionManager.EnterLandmarkPlacement(capturedId);
            });

            // Hotkey label (top-left corner)
            if (!string.IsNullOrEmpty(hotkeyLabel))
            {
                var hkGO = new GameObject("Hotkey");
                hkGO.transform.SetParent(btnGO.transform, false);
                var hkRT = hkGO.AddComponent<RectTransform>();
                hkRT.anchorMin = new Vector2(0, 1);
                hkRT.anchorMax = new Vector2(0, 1);
                hkRT.pivot = new Vector2(0, 1);
                hkRT.anchoredPosition = new Vector2(4, -4);
                hkRT.sizeDelta = new Vector2(16, 14);
                var hkText = hkGO.AddComponent<TextMeshProUGUI>();
                hkText.text = hotkeyLabel;
                hkText.fontSize = 9;
                hkText.fontStyle = FontStyles.Bold;
                hkText.color = new Color(1f, 1f, 1f, 0.85f);
                hkText.alignment = TextAlignmentOptions.TopLeft;
                hkText.raycastTarget = false;
            }

            // Name text
            var nameGO = new GameObject("Name");
            nameGO.transform.SetParent(btnGO.transform, false);
            var nameRT = nameGO.AddComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 1);
            nameRT.anchorMax = new Vector2(1, 1);
            nameRT.pivot = new Vector2(0.5f, 1);
            nameRT.anchoredPosition = new Vector2(0, -6);
            nameRT.sizeDelta = new Vector2(-10, 24);
            var nameText = nameGO.AddComponent<TextMeshProUGUI>();
            nameText.text = def.Name;
            nameText.fontSize = 14;
            nameText.fontStyle = FontStyles.Bold;
            nameText.color = canAfford ? Color.white : Color.gray;
            nameText.alignment = TextAlignmentOptions.Center;

            // Description
            var descGO = new GameObject("Desc");
            descGO.transform.SetParent(btnGO.transform, false);
            var descRT = descGO.AddComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0, 1);
            descRT.anchorMax = new Vector2(1, 1);
            descRT.pivot = new Vector2(0.5f, 1);
            descRT.anchoredPosition = new Vector2(0, -32);
            descRT.sizeDelta = new Vector2(-10, 40);
            var descText = descGO.AddComponent<TextMeshProUGUI>();
            descText.text = def.Description;
            descText.fontSize = 11;
            descText.color = canAfford ? new Color(0.8f, 0.8f, 0.8f) : Color.gray;
            descText.alignment = TextAlignmentOptions.Center;
            descText.enableWordWrapping = true;

            // Cost text
            var costGO = new GameObject("Cost");
            costGO.transform.SetParent(btnGO.transform, false);
            var costRT = costGO.AddComponent<RectTransform>();
            costRT.anchorMin = new Vector2(0, 0);
            costRT.anchorMax = new Vector2(1, 0);
            costRT.pivot = new Vector2(0.5f, 0);
            costRT.anchoredPosition = new Vector2(0, 8);
            costRT.sizeDelta = new Vector2(-10, 24);
            var costText = costGO.AddComponent<TextMeshProUGUI>();
            string foodColor = resources.Food >= def.FoodCost ? "white" : "#FF6666";
            string goldColor = resources.Gold >= def.GoldCost ? "white" : "#FF6666";
            costText.text = $"<color={foodColor}>{def.FoodCost} <sprite name=\"food\"></color>  <color={goldColor}>{def.GoldCost} <sprite name=\"gold\"></color>";
            costText.fontSize = 13;
            costText.color = Color.white;
            costText.alignment = TextAlignmentOptions.Center;
            costText.richText = true;
        }

        private void CloseLandmarkChoicePanel()
        {
            if (landmarkPanelGO != null)
            {
                Destroy(landmarkPanelGO);
                landmarkPanelGO = null;
            }
            landmarkChoiceACallback = null;
            landmarkChoiceBCallback = null;
        }

        private BuildingType? GetEffectiveBuildingType(IReadOnlyList<BuildingView> buildings, int localPid)
        {
            var tabType = selectionManager.ActiveTabBuildingType;
            if (tabType != null)
            {
                // Verify that type still exists in the selection
                for (int i = 0; i < buildings.Count; i++)
                    if (buildings[i].PlayerId == localPid && buildings[i].BuildingType == tabType && !buildings[i].IsDestroyed)
                        return tabType;
                // Tab type gone; fall back to dominant and clear the stale override
                selectionManager.SetActiveTabBuildingType(null);
            }
            return FindDominantBuildingType(buildings, localPid);
        }

        private BuildingType? FindDominantBuildingType(IReadOnlyList<BuildingView> buildings, int localPid)
        {
            // Count owned buildings per type, pick the most common
            int bestCount = 0;
            BuildingType bestType = default;
            bool found = false;
            // Iterate BuildingType values 0..max by checking each building
            var counts = new Dictionary<BuildingType, int>();
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i].PlayerId != localPid) continue;
                var t = buildings[i].BuildingType;
                if (counts.ContainsKey(t)) counts[t]++;
                else counts[t] = 1;
            }
            foreach (var kvp in counts)
            {
                if (kvp.Value > bestCount) { bestType = kvp.Key; bestCount = kvp.Value; found = true; }
            }
            return found ? bestType : (BuildingType?)null;
        }

        private GridButton?[] GetMultiBuildingActionSlots(IReadOnlyList<BuildingView> buildings, GameSimulation sim)
        {
            int localPid = selectionManager.LocalPlayerId;
            var dominantType = GetEffectiveBuildingType(buildings, localPid);
            if (dominantType == null) return null;

            var slots = new GridButton?[12];
            bool hasAny = false;

            if (IsWallType(dominantType.Value))
            {
                // Walls: send gate toggle to all owned walls (including under construction)
                var walls = new List<BuildingView>();
                for (int i = 0; i < buildings.Count; i++)
                    if (buildings[i].PlayerId == localPid && IsWallType(buildings[i].BuildingType))
                        walls.Add(buildings[i]);
                slots[0] = new GridButton { Label = "Gate", Hotkey = "Q", Enabled = true,
                    Tooltip = "<b>Toggle Gate</b>\nConvert between wall and gate.",
                    OnClick = () => {
                        for (int i = 0; i < walls.Count; i++)
                        {
                            var bData = sim.BuildingRegistry.GetBuilding(walls[i].BuildingId);
                            if (bData != null)
                                sim.CommandBuffer.EnqueueCommand(
                                    new ConvertToGateCommand(bData.PlayerId, bData.Id));
                        }
                    } };
                hasAny = true;
            }
            else
            {
                // Collect only completed buildings of the dominant type
                var ready = new List<BuildingView>();
                for (int i = 0; i < buildings.Count; i++)
                {
                    if (buildings[i].PlayerId != localPid) continue;
                    if (buildings[i].BuildingType != dominantType) continue;
                    var bData = sim.BuildingRegistry.GetBuilding(buildings[i].BuildingId);
                    if (bData != null && !bData.IsUnderConstruction)
                        ready.Add(buildings[i]);
                }

                if (ready.Count > 0)
                {
                    var resources = sim.ResourceManager.GetPlayerResources(localPid);

                    if (dominantType == BuildingType.Barracks)
                    {
                        var civ = sim.GetPlayerCivilization(localPid);
                        bool isLandsknecht = civ == Civilization.HolyRomanEmpire;
                        string spLabel = isLandsknecht ? "Lands." : "Spear";
                        string spName = isLandsknecht ? "Landsknecht" : "Spearman";
                        int spIcon = isLandsknecht ? 8 : 1;
                        int spearmanFood = isLandsknecht ? sim.Config.LandsknechtFoodCost : sim.Config.SpearmanFoodCost;
                        int spearmanWood = isLandsknecht ? sim.Config.LandsknechtWoodCost : sim.Config.SpearmanWoodCost;
                        slots[0] = new GridButton { Label = spLabel, Hotkey = "Q",
                            Enabled = resources.Food >= spearmanFood && resources.Wood >= spearmanWood,
                            Icon = UnitIcons.Get(spIcon),
                            Tooltip = $"<b>{spName}</b>\nMelee infantry unit.\nCost: {spearmanFood} <sprite name=\"food\"> {spearmanWood} <sprite name=\"wood\">",
                            OnClick = () => { for (int i = 0; i < ready.Count; i++)
                                sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(
                                    localPid, ready[i].BuildingId, 1));
                                SFXManager.Instance?.PlayUI(SFXType.QueueUnit, 0.5f); } };
                        int maaFood = sim.Config.ManAtArmsFoodCost;
                        int maaGold = sim.Config.ManAtArmsGoldCost;
                        bool maaAgeOk2 = sim.GetPlayerAge(localPid) >= LandmarkDefinitions.GetUnitRequiredAge(6);
                        slots[1] = new GridButton { Label = "MAA", Hotkey = "W",
                            Enabled = maaAgeOk2 && resources.Food >= maaFood && resources.Gold >= maaGold,
                            Icon = UnitIcons.Get(6),
                            Tooltip = $"<b>Man-at-Arms</b>\nHeavy armored infantry.\nCost: {maaFood} <sprite name=\"food\"> {maaGold} <sprite name=\"gold\">"
                                + (maaAgeOk2 ? "" : $"\n<color=#FF6666>Requires Age {LandmarkDefinitions.AgeToRoman(3)}</color>"),
                            OnClick = () => { for (int i = 0; i < ready.Count; i++)
                                sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(
                                    localPid, ready[i].BuildingId, 6));
                                SFXManager.Instance?.PlayUI(SFXType.QueueUnit, 0.5f); } };
                        hasAny = true;
                    }
                    else if (dominantType == BuildingType.ArcheryRange)
                    {
                        var civ = sim.GetPlayerCivilization(localPid);
                        bool isLongbow = civ == Civilization.English;
                        string arLabel = isLongbow ? "Longbow" : "Archer";
                        string arName = isLongbow ? "Longbowman" : "Archer";
                        int arIcon = isLongbow ? 6 : 2;
                        int archerFood = isLongbow ? sim.Config.LongbowmanFoodCost : sim.Config.ArcherFoodCost;
                        int archerWood = isLongbow ? sim.Config.LongbowmanWoodCost : sim.Config.ArcherWoodCost;
                        slots[0] = new GridButton { Label = arLabel, Hotkey = "Q",
                            Enabled = resources.Food >= archerFood && resources.Wood >= archerWood,
                            Icon = UnitIcons.Get(arIcon),
                            Tooltip = $"<b>{arName}</b>\nRanged infantry unit.\nCost: {archerFood} <sprite name=\"food\"> {archerWood} <sprite name=\"wood\">",
                            OnClick = () => { for (int i = 0; i < ready.Count; i++)
                                sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(
                                    localPid, ready[i].BuildingId, 2));
                                SFXManager.Instance?.PlayUI(SFXType.QueueUnit, 0.5f); } };
                        int xbowFood = sim.Config.CrossbowmanFoodCost;
                        int xbowGold = sim.Config.CrossbowmanGoldCost;
                        bool xbowAgeOk2 = sim.GetPlayerAge(localPid) >= LandmarkDefinitions.GetUnitRequiredAge(8);
                        slots[1] = new GridButton { Label = "Xbow", Hotkey = "W",
                            Enabled = xbowAgeOk2 && resources.Food >= xbowFood && resources.Gold >= xbowGold,
                            Icon = UnitIcons.Get(8),
                            Tooltip = $"<b>Crossbowman</b>\nRanged anti-armor unit.\nCost: {xbowFood} <sprite name=\"food\"> {xbowGold} <sprite name=\"gold\">"
                                + (xbowAgeOk2 ? "" : $"\n<color=#FF6666>Requires Age {LandmarkDefinitions.AgeToRoman(3)}</color>"),
                            OnClick = () => { for (int i = 0; i < ready.Count; i++)
                                sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(
                                    localPid, ready[i].BuildingId, 8));
                                SFXManager.Instance?.PlayUI(SFXType.QueueUnit, 0.5f); } };
                        hasAny = true;
                    }
                    else if (dominantType == BuildingType.Stables)
                    {
                        var civ = sim.GetPlayerCivilization(localPid);
                        bool isGendarme = civ == Civilization.French;
                        string hrLabel = isGendarme ? "Gendrm" : "Horse";
                        string hrName = isGendarme ? "Gendarme" : "Horseman";
                        int hrIcon = isGendarme ? 7 : 3;
                        int horsemanFood = isGendarme ? sim.Config.GendarmeFoodCost : sim.Config.HorsemanFoodCost;
                        int horsemanWood = isGendarme ? sim.Config.GendarmeWoodCost : sim.Config.HorsemanWoodCost;
                        slots[0] = new GridButton { Label = hrLabel, Hotkey = "Q",
                            Enabled = resources.Food >= horsemanFood && resources.Wood >= horsemanWood,
                            Icon = UnitIcons.Get(hrIcon),
                            Tooltip = $"<b>{hrName}</b>\nMounted melee unit.\nCost: {horsemanFood} <sprite name=\"food\"> {horsemanWood} <sprite name=\"wood\">",
                            OnClick = () => { for (int i = 0; i < ready.Count; i++)
                                sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(
                                    localPid, ready[i].BuildingId, 3));
                                SFXManager.Instance?.PlayUI(SFXType.QueueUnit, 0.5f); } };
                        int knightFood = sim.Config.KnightFoodCost;
                        int knightGold = sim.Config.KnightGoldCost;
                        bool knightAgeOk2 = sim.GetPlayerAge(localPid) >= LandmarkDefinitions.GetUnitRequiredAge(7);
                        slots[1] = new GridButton { Label = "Knight", Hotkey = "W",
                            Enabled = knightAgeOk2 && resources.Food >= knightFood && resources.Gold >= knightGold,
                            Icon = UnitIcons.Get(7),
                            Tooltip = $"<b>Knight</b>\nHeavy armored cavalry.\nCost: {knightFood} <sprite name=\"food\"> {knightGold} <sprite name=\"gold\">"
                                + (knightAgeOk2 ? "" : $"\n<color=#FF6666>Requires Age {LandmarkDefinitions.AgeToRoman(3)}</color>"),
                            OnClick = () => { for (int i = 0; i < ready.Count; i++)
                                sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(
                                    localPid, ready[i].BuildingId, 7));
                                SFXManager.Instance?.PlayUI(SFXType.QueueUnit, 0.5f); } };
                        int scoutFood = sim.Config.ScoutFoodCost;
                        slots[2] = new GridButton { Label = "Scout", Hotkey = "E",
                            Enabled = resources.Food >= scoutFood,
                            Icon = UnitIcons.Get(4),
                            Tooltip = $"<b>Scout</b>\nFast mounted unit with high vision.\nCost: {scoutFood} <sprite name=\"food\">",
                            OnClick = () => { for (int i = 0; i < ready.Count; i++)
                                sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(
                                    localPid, ready[i].BuildingId, 4));
                                SFXManager.Instance?.PlayUI(SFXType.QueueUnit, 0.5f); } };
                        hasAny = true;
                    }
                    else if (dominantType == BuildingType.Monastery)
                    {
                        int monkFood = sim.Config.MonkFoodCost;
                        int monkGold = sim.Config.MonkGoldCost;
                        bool monkAgeOk2 = sim.GetPlayerAge(localPid) >= LandmarkDefinitions.GetUnitRequiredAge(9);
                        slots[0] = new GridButton { Label = "Monk", Hotkey = "Q",
                            Enabled = monkAgeOk2 && resources.Food >= monkFood && resources.Gold >= monkGold,
                            Icon = UnitIcons.Get(9),
                            Tooltip = $"<b>Monk</b>\nHealer unit. Automatically heals nearby friendly units.\nCost: {monkFood} <sprite name=\"food\"> {monkGold} <sprite name=\"gold\">"
                                + (monkAgeOk2 ? "" : $"\n<color=#FF6666>Requires Age {LandmarkDefinitions.AgeToRoman(3)}</color>"),
                            OnClick = () => { for (int i = 0; i < ready.Count; i++)
                                sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(
                                    localPid, ready[i].BuildingId, 9));
                                SFXManager.Instance?.PlayUI(SFXType.QueueUnit, 0.5f); } };
                        hasAny = true;
                    }
                    else if (dominantType == BuildingType.TownCenter)
                    {
                        int villagerCost = sim.Config.VillagerFoodCost;
                        slots[0] = new GridButton { Label = "Villager", Hotkey = "Q",
                            Enabled = resources.Food >= villagerCost,
                            Icon = UnitIcons.Get(0),
                            Tooltip = $"<b>Villager</b>\nGathers resources and constructs buildings.\nCost: {villagerCost} <sprite name=\"food\">",
                            OnClick = () => { for (int i = 0; i < ready.Count; i++)
                                sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(
                                    localPid, ready[i].BuildingId, 0));
                                SFXManager.Instance?.PlayUI(SFXType.QueueUnit, 0.5f); } };
                        int scoutFood = sim.Config.ScoutFoodCost;
                        slots[1] = new GridButton { Label = "Scout", Hotkey = "W",
                            Enabled = resources.Food >= scoutFood,
                            Icon = UnitIcons.Get(4),
                            Tooltip = $"<b>Scout</b>\nFast mounted unit with high vision.\nCost: {scoutFood} <sprite name=\"food\">",
                            OnClick = () => { for (int i = 0; i < ready.Count; i++)
                                sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(
                                    localPid, ready[i].BuildingId, 4));
                                SFXManager.Instance?.PlayUI(SFXType.QueueUnit, 0.5f); } };
                        hasAny = true;
                    }
                }
            }

            // Ungarrison at slot 4 (A) — any selected building with garrisoned units
            int totalGarrisoned = 0;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i].PlayerId != localPid) continue;
                var bData = sim.BuildingRegistry.GetBuilding(buildings[i].BuildingId);
                if (bData != null && bData.GarrisonCount > 0)
                    totalGarrisoned += bData.GarrisonCount;
            }
            if (totalGarrisoned > 0)
            {
                CommandIcons.EnsureLoaded();
                slots[7] = new GridButton { Label = "Ungarrison", Hotkey = "F",
                    Icon = CommandIcons.Garrison,
                    Tooltip = $"<b>Ungarrison All</b>\nEject all {totalGarrisoned} garrisoned units.",
                    Enabled = true,
                    OnClick = () => {
                        for (int i = 0; i < buildings.Count; i++)
                        {
                            if (buildings[i].PlayerId != localPid) continue;
                            var bData = sim.BuildingRegistry.GetBuilding(buildings[i].BuildingId);
                            if (bData != null && bData.GarrisonCount > 0)
                                sim.CommandBuffer.EnqueueCommand(
                                    new UngarrisonCommand(localPid, bData.Id));
                        }
                    } };
                hasAny = true;
            }

            // X = Delete (hold to confirm)
            slots[9] = new GridButton { Label = "Delete", Hotkey = "X",
                Tooltip = "<b>Delete</b>\nHold to destroy selected building.",
                Enabled = true };
            hasAny = true;

            return hasAny ? slots : null;
        }

        // =============================================================
        //  Hotkey processing (unchanged)
        // =============================================================

        private static bool WasKeyPressed(Key key)
        {
            return Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
        }

        private void ProcessHotkeys()
        {
            if (UnitSelectionManager.UIInputSuppressed) return;

            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            var selectedBuildings = selectionManager.SelectedBuildings;
            if (selectedBuildings.Count > 0)
            {
                if (WasKeyPressed(Key.Tab))
                    CycleBuildingTab(selectedBuildings);

                ProcessBuildingHotkeys(selectedBuildings, sim);
                return;
            }

            var selectedUnits = selectionManager.SelectedUnits;
            if (selectedUnits.Count > 0)
                ProcessUnitHotkeys(selectedUnits, sim);
        }

        private void ProcessBuildingHotkeys(IReadOnlyList<BuildingView> buildings, GameSimulation sim)
        {
            int localPid = selectionManager.LocalPlayerId;
            var dominantType = GetEffectiveBuildingType(buildings, localPid);
            if (dominantType == null) return;

            if (IsWallType(dominantType.Value))
            {
                if (WasKeyPressed(Key.Q))
                {
                    FlashActionButton(0);
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        if (buildings[i].PlayerId != localPid || !IsWallType(buildings[i].BuildingType)) continue;
                        var bData = sim.BuildingRegistry.GetBuilding(buildings[i].BuildingId);
                        if (bData != null)
                            sim.CommandBuffer.EnqueueCommand(new ConvertToGateCommand(bData.PlayerId, bData.Id));
                    }
                }
                return;
            }

            // Collect only completed buildings of the dominant type
            var resources = sim.ResourceManager.GetPlayerResources(localPid);

            if (dominantType == BuildingType.TownCenter)
            {
                if (WasKeyPressed(Key.Q) && resources.Food >= sim.Config.VillagerFoodCost)
                {
                    FlashActionButton(0);
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        if (!IsReadyBuilding(buildings[i], BuildingType.TownCenter, localPid, sim)) continue;
                        sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(localPid, buildings[i].BuildingId, 0));
                    }
                    SFXManager.Instance?.PlayUI(SFXType.QueueUnit, 0.5f);
                }
                else if (WasKeyPressed(Key.W) && resources.Food >= sim.Config.ScoutFoodCost)
                {
                    FlashActionButton(1);
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        if (!IsReadyBuilding(buildings[i], BuildingType.TownCenter, localPid, sim)) continue;
                        sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(localPid, buildings[i].BuildingId, 4));
                    }
                    SFXManager.Instance?.PlayUI(SFXType.QueueUnit, 0.5f);
                }
                else if (WasKeyPressed(Key.A))
                {
                    int currentAge = sim.GetPlayerAge(localPid);
                    bool isAgingUp = sim.IsPlayerAgingUp(localPid);
                    if (currentAge < 3 && !isAgingUp)
                    {
                        FlashActionButton(4);
                        ShowLandmarkChoicePanel(sim, localPid, currentAge + 1);
                    }
                    else if (currentAge >= 3)
                    {
                        FlashActionButton(4);
                        selectionManager.EnterBuildPlacement(BuildingType.Wonder);
                    }
                }
            }
            else if (dominantType == BuildingType.Barracks)
            {
                if (WasKeyPressed(Key.Q) && resources.Food >= sim.Config.SpearmanFoodCost && resources.Wood >= sim.Config.SpearmanWoodCost)
                {
                    FlashActionButton(0);
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        if (!IsReadyBuilding(buildings[i], BuildingType.Barracks, localPid, sim)) continue;
                        sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(localPid, buildings[i].BuildingId, 1));
                    }
                    SFXManager.Instance?.PlayUI(SFXType.QueueUnit, 0.5f);
                }
                else if (WasKeyPressed(Key.W) && resources.Food >= sim.Config.ManAtArmsFoodCost && resources.Gold >= sim.Config.ManAtArmsGoldCost)
                {
                    FlashActionButton(1);
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        if (!IsReadyBuilding(buildings[i], BuildingType.Barracks, localPid, sim)) continue;
                        sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(localPid, buildings[i].BuildingId, 6));
                    }
                    SFXManager.Instance?.PlayUI(SFXType.QueueUnit, 0.5f);
                }
            }
            else if (dominantType == BuildingType.ArcheryRange)
            {
                if (WasKeyPressed(Key.Q) && resources.Food >= sim.Config.ArcherFoodCost && resources.Wood >= sim.Config.ArcherWoodCost)
                {
                    FlashActionButton(0);
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        if (!IsReadyBuilding(buildings[i], BuildingType.ArcheryRange, localPid, sim)) continue;
                        sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(localPid, buildings[i].BuildingId, 2));
                    }
                    SFXManager.Instance?.PlayUI(SFXType.QueueUnit, 0.5f);
                }
                else if (WasKeyPressed(Key.W) && resources.Food >= sim.Config.CrossbowmanFoodCost && resources.Gold >= sim.Config.CrossbowmanGoldCost)
                {
                    FlashActionButton(1);
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        if (!IsReadyBuilding(buildings[i], BuildingType.ArcheryRange, localPid, sim)) continue;
                        sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(localPid, buildings[i].BuildingId, 8));
                    }
                    SFXManager.Instance?.PlayUI(SFXType.QueueUnit, 0.5f);
                }
            }
            else if (dominantType == BuildingType.Stables)
            {
                if (WasKeyPressed(Key.Q) && resources.Food >= sim.Config.HorsemanFoodCost && resources.Wood >= sim.Config.HorsemanWoodCost)
                {
                    FlashActionButton(0);
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        if (!IsReadyBuilding(buildings[i], BuildingType.Stables, localPid, sim)) continue;
                        sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(localPid, buildings[i].BuildingId, 3));
                    }
                    SFXManager.Instance?.PlayUI(SFXType.QueueUnit, 0.5f);
                }
                else if (WasKeyPressed(Key.W) && resources.Food >= sim.Config.KnightFoodCost && resources.Gold >= sim.Config.KnightGoldCost)
                {
                    FlashActionButton(1);
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        if (!IsReadyBuilding(buildings[i], BuildingType.Stables, localPid, sim)) continue;
                        sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(localPid, buildings[i].BuildingId, 7));
                    }
                    SFXManager.Instance?.PlayUI(SFXType.QueueUnit, 0.5f);
                }
                else if (WasKeyPressed(Key.E) && resources.Food >= sim.Config.ScoutFoodCost)
                {
                    FlashActionButton(2);
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        if (!IsReadyBuilding(buildings[i], BuildingType.Stables, localPid, sim)) continue;
                        sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(localPid, buildings[i].BuildingId, 4));
                    }
                    SFXManager.Instance?.PlayUI(SFXType.QueueUnit, 0.5f);
                }
            }
            else if (dominantType == BuildingType.Monastery)
            {
                if (WasKeyPressed(Key.Q) && resources.Food >= sim.Config.MonkFoodCost && resources.Gold >= sim.Config.MonkGoldCost)
                {
                    FlashActionButton(0);
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        if (!IsReadyBuilding(buildings[i], BuildingType.Monastery, localPid, sim)) continue;
                        sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(localPid, buildings[i].BuildingId, 9));
                    }
                    SFXManager.Instance?.PlayUI(SFXType.QueueUnit, 0.5f);
                }
            }
            else if (dominantType == BuildingType.Tower)
            {
                if (WasKeyPressed(Key.Q))
                {
                    FlashActionButton(0);
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        if (!IsReadyBuilding(buildings[i], BuildingType.Tower, localPid, sim)) continue;
                        sim.CommandBuffer.EnqueueCommand(new UpgradeTowerCommand(localPid, buildings[i].BuildingId, TowerUpgradeType.ArrowSlits));
                    }
                }
                if (WasKeyPressed(Key.W))
                {
                    FlashActionButton(1);
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        if (!IsReadyBuilding(buildings[i], BuildingType.Tower, localPid, sim)) continue;
                        sim.CommandBuffer.EnqueueCommand(new UpgradeTowerCommand(localPid, buildings[i].BuildingId, TowerUpgradeType.CannonEmplacement));
                    }
                }
                if (WasKeyPressed(Key.E))
                {
                    FlashActionButton(2);
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        if (!IsReadyBuilding(buildings[i], BuildingType.Tower, localPid, sim)) continue;
                        sim.CommandBuffer.EnqueueCommand(new UpgradeTowerCommand(localPid, buildings[i].BuildingId, TowerUpgradeType.StoneUpgrade));
                    }
                }
                if (WasKeyPressed(Key.R))
                {
                    FlashActionButton(3);
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        if (!IsReadyBuilding(buildings[i], BuildingType.Tower, localPid, sim)) continue;
                        sim.CommandBuffer.EnqueueCommand(new UpgradeTowerCommand(localPid, buildings[i].BuildingId, TowerUpgradeType.VisionUpgrade));
                    }
                }
            }
            else if (dominantType == BuildingType.Blacksmith)
            {
                TechnologyType[] blacksmithTechs = {
                    TechnologyType.MeleeAttack1, TechnologyType.MeleeArmor1,
                    TechnologyType.RangedAttack1, TechnologyType.RangedArmor1,
                    TechnologyType.MeleeAttack2, TechnologyType.MeleeArmor2,
                    TechnologyType.RangedAttack2, TechnologyType.RangedArmor2
                };
                Key[] blacksmithKeys = { Key.Q, Key.W, Key.E, Key.R, Key.A, Key.S, Key.D, Key.F };
                int[] blacksmithSlots = { 0, 1, 2, 3, 4, 5, 6, 7 };
                for (int t = 0; t < blacksmithTechs.Length; t++)
                {
                    if (WasKeyPressed(blacksmithKeys[t]))
                    {
                        FlashActionButton(blacksmithSlots[t]);
                        for (int i = 0; i < buildings.Count; i++)
                        {
                            if (!IsReadyBuilding(buildings[i], BuildingType.Blacksmith, localPid, sim)) continue;
                            sim.CommandBuffer.EnqueueCommand(new ResearchCommand(localPid, buildings[i].BuildingId, blacksmithTechs[t]));
                        }
                        break;
                    }
                }
            }
            else if (dominantType == BuildingType.University)
            {
                TechnologyType[] uniTechs = {
                    TechnologyType.Ballistics, TechnologyType.SiegeEngineering,
                    TechnologyType.Chemistry, TechnologyType.MurderHoles
                };
                Key[] uniKeys = { Key.Q, Key.W, Key.E, Key.R };
                for (int t = 0; t < uniTechs.Length; t++)
                {
                    if (WasKeyPressed(uniKeys[t]))
                    {
                        FlashActionButton(t);
                        for (int i = 0; i < buildings.Count; i++)
                        {
                            if (!IsReadyBuilding(buildings[i], BuildingType.University, localPid, sim)) continue;
                            sim.CommandBuffer.EnqueueCommand(new ResearchCommand(localPid, buildings[i].BuildingId, uniTechs[t]));
                        }
                        break;
                    }
                }
            }
            else if (dominantType == BuildingType.SiegeWorkshop)
            {
                if (WasKeyPressed(Key.Q) && resources.Wood >= sim.Config.BatteringRamWoodCost && resources.Gold >= sim.Config.BatteringRamGoldCost)
                {
                    FlashActionButton(0);
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        if (!IsReadyBuilding(buildings[i], BuildingType.SiegeWorkshop, localPid, sim)) continue;
                        sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(localPid, buildings[i].BuildingId, 13));
                    }
                    SFXManager.Instance?.PlayUI(SFXType.QueueUnit, 0.5f);
                }
                else if (WasKeyPressed(Key.W) && resources.Wood >= sim.Config.MangonelWoodCost && resources.Gold >= sim.Config.MangonelGoldCost)
                {
                    FlashActionButton(1);
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        if (!IsReadyBuilding(buildings[i], BuildingType.SiegeWorkshop, localPid, sim)) continue;
                        sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(localPid, buildings[i].BuildingId, 14));
                    }
                    SFXManager.Instance?.PlayUI(SFXType.QueueUnit, 0.5f);
                }
                else if (WasKeyPressed(Key.E) && resources.Wood >= sim.Config.TrebuchetWoodCost && resources.Gold >= sim.Config.TrebuchetGoldCost)
                {
                    FlashActionButton(2);
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        if (!IsReadyBuilding(buildings[i], BuildingType.SiegeWorkshop, localPid, sim)) continue;
                        sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(localPid, buildings[i].BuildingId, 15));
                    }
                    SFXManager.Instance?.PlayUI(SFXType.QueueUnit, 0.5f);
                }
            }
            else if (dominantType == BuildingType.Market)
            {
                if (WasKeyPressed(Key.Q))
                {
                    FlashActionButton(0);
                    sim.CommandBuffer.EnqueueCommand(new MarketTradeCommand(localPid, (int)ResourceType.Food, true));
                }
                else if (WasKeyPressed(Key.W))
                {
                    FlashActionButton(1);
                    sim.CommandBuffer.EnqueueCommand(new MarketTradeCommand(localPid, (int)ResourceType.Wood, true));
                }
                else if (WasKeyPressed(Key.E))
                {
                    FlashActionButton(2);
                    sim.CommandBuffer.EnqueueCommand(new MarketTradeCommand(localPid, (int)ResourceType.Stone, true));
                }
                else if (WasKeyPressed(Key.A))
                {
                    FlashActionButton(4);
                    sim.CommandBuffer.EnqueueCommand(new MarketTradeCommand(localPid, (int)ResourceType.Food, false));
                }
                else if (WasKeyPressed(Key.S))
                {
                    FlashActionButton(5);
                    sim.CommandBuffer.EnqueueCommand(new MarketTradeCommand(localPid, (int)ResourceType.Wood, false));
                }
                else if (WasKeyPressed(Key.D))
                {
                    FlashActionButton(6);
                    sim.CommandBuffer.EnqueueCommand(new MarketTradeCommand(localPid, (int)ResourceType.Stone, false));
                }
            }

            // F = Ungarrison — works for any building type with garrisoned units
            if (WasKeyPressed(Key.F))
            {
                for (int i = 0; i < buildings.Count; i++)
                {
                    if (buildings[i].PlayerId != localPid) continue;
                    var bData = sim.BuildingRegistry.GetBuilding(buildings[i].BuildingId);
                    if (bData != null && bData.GarrisonCount > 0)
                    {
                        FlashActionButton(7);
                        sim.CommandBuffer.EnqueueCommand(new UngarrisonCommand(localPid, bData.Id));
                    }
                }
            }
        }

        private void CycleBuildingTab(IReadOnlyList<BuildingView> buildings)
        {
            int localPid = selectionManager.LocalPlayerId;
            var counts = new Dictionary<BuildingType, int>();
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i].PlayerId != localPid || buildings[i].IsDestroyed) continue;
                var t = buildings[i].BuildingType;
                counts[t] = counts.TryGetValue(t, out int c) ? c + 1 : 1;
            }
            if (counts.Count <= 1) return;  // Nothing to cycle

            // Sort descending by count, then alphabetically by name for ties
            var sorted = new List<BuildingType>(counts.Keys);
            sorted.Sort((a, b) => {
                int cmp = counts[b].CompareTo(counts[a]);
                if (cmp != 0) return cmp;
                return GetBuildingName(a).CompareTo(GetBuildingName(b));
            });

            var current = selectionManager.ActiveTabBuildingType;
            int idx = current.HasValue ? sorted.IndexOf(current.Value) : -1;
            int next = (idx + 1) % sorted.Count;
            selectionManager.SetActiveTabBuildingType(sorted[next]);
        }

        private bool IsReadyBuilding(BuildingView view, BuildingType type, int playerId, GameSimulation sim)
        {
            if (view.PlayerId != playerId || view.BuildingType != type) return false;
            var bData = sim.BuildingRegistry.GetBuilding(view.BuildingId);
            return bData != null && !bData.IsUnderConstruction;
        }

        private void ProcessUnitHotkeys(IReadOnlyList<UnitView> units, GameSimulation sim)
        {
            int localPid = selectionManager.LocalPlayerId;

            if (buildMenuAge > 0)
            {
                ProcessBuildMenuHotkeys(sim, localPid);
                return;
            }

            ProcessCommandHotkeys(units, sim, localPid);
        }

        private bool TryBuildHotkey(GameSimulation sim, int localPid, BuildingType type, int btnIndex, bool isWall = false, bool isGate = false)
        {
            var resources = sim.ResourceManager.GetPlayerResources(localPid);
            int playerAge = sim.GetPlayerAge(localPid);
            int reqAge = LandmarkDefinitions.GetBuildingRequiredAge(type);
            if (playerAge < reqAge) return false;

            int wood = sim.GetBuildingWoodCost(type);
            int stone = sim.GetBuildingStoneCost(type);
            int food = sim.GetBuildingFoodCost(type);
            int gold = sim.GetBuildingGoldCost(type);
            if (resources.Wood < wood || resources.Stone < stone || resources.Food < food || resources.Gold < gold) return false;

            FlashBuildButton(btnIndex);
            if (isWall)
                selectionManager.EnterWallPlacement(type, isGate);
            else
                selectionManager.EnterBuildPlacement(type);
            buildMenuAge = 0;
            return true;
        }

        private void ProcessBuildMenuHotkeys(GameSimulation sim, int localPid)
        {
            int o = (buildMenuAge - 1) * BuildButtonsPerAge; // global button index offset
            switch (buildMenuAge)
            {
                case 1:
                    if (WasKeyPressed(Key.Q)) TryBuildHotkey(sim, localPid, BuildingType.Mill, o + 0);
                    else if (WasKeyPressed(Key.W)) TryBuildHotkey(sim, localPid, BuildingType.LumberYard, o + 1);
                    else if (WasKeyPressed(Key.E)) TryBuildHotkey(sim, localPid, BuildingType.Mine, o + 2);
                    else if (WasKeyPressed(Key.A)) TryBuildHotkey(sim, localPid, BuildingType.House, o + 3);
                    else if (WasKeyPressed(Key.S)) TryBuildHotkey(sim, localPid, BuildingType.Farm, o + 4);
                    else if (WasKeyPressed(Key.D)) TryBuildHotkey(sim, localPid, BuildingType.Barracks, o + 5);
                    else if (WasKeyPressed(Key.Z)) TryBuildHotkey(sim, localPid, BuildingType.Wall, o + 6, isWall: true);
                    else if (WasKeyPressed(Key.X)) TryBuildHotkey(sim, localPid, BuildingType.WoodGate, o + 7, isWall: true, isGate: true);
                    break;
                case 2:
                    if (WasKeyPressed(Key.Q)) TryBuildHotkey(sim, localPid, BuildingType.Blacksmith, o + 0);
                    else if (WasKeyPressed(Key.W)) TryBuildHotkey(sim, localPid, BuildingType.Market, o + 1);
                    else if (WasKeyPressed(Key.E)) TryBuildHotkey(sim, localPid, BuildingType.TownCenter, o + 2);
                    else if (WasKeyPressed(Key.A)) TryBuildHotkey(sim, localPid, BuildingType.ArcheryRange, o + 3);
                    else if (WasKeyPressed(Key.S)) TryBuildHotkey(sim, localPid, BuildingType.Stables, o + 4);
                    else if (WasKeyPressed(Key.D)) TryBuildHotkey(sim, localPid, BuildingType.Tower, o + 5);
                    break;
                case 3:
                    if (WasKeyPressed(Key.Q)) TryBuildHotkey(sim, localPid, BuildingType.Monastery, o + 0);
                    else if (WasKeyPressed(Key.W)) TryBuildHotkey(sim, localPid, BuildingType.University, o + 1);
                    else if (WasKeyPressed(Key.E)) TryBuildHotkey(sim, localPid, BuildingType.SiegeWorkshop, o + 2);
                    else if (WasKeyPressed(Key.A)) TryBuildHotkey(sim, localPid, BuildingType.Keep, o + 3);
                    else if (WasKeyPressed(Key.S)) TryBuildHotkey(sim, localPid, BuildingType.StoneWall, o + 4, isWall: true);
                    else if (WasKeyPressed(Key.D)) TryBuildHotkey(sim, localPid, BuildingType.StoneGate, o + 5, isWall: true, isGate: true);
                    else if (WasKeyPressed(Key.Z)) TryBuildHotkey(sim, localPid, BuildingType.Wonder, o + 6);
                    break;
            }
        }

        private void ProcessCommandHotkeys(IReadOnlyList<UnitView> units, GameSimulation sim, int localPid)
        {
            bool allOwnedVillagers = true;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].UnitType != 0 || units[i].PlayerId != localPid)
                { allOwnedVillagers = false; break; }
            }

            // Q/W/E = Build age tabs (villagers only)
            if (allOwnedVillagers)
            {
                if (WasKeyPressed(Key.Q))
                {
                    FlashActionButton(0);
                    buildMenuAge = 1;
                    return;
                }
                if (WasKeyPressed(Key.W))
                {
                    FlashActionButton(1);
                    buildMenuAge = 2;
                    return;
                }
                if (WasKeyPressed(Key.E))
                {
                    FlashActionButton(2);
                    buildMenuAge = 3;
                    return;
                }
            }

            // R = Age Up / Wonder (villagers only)
            if (allOwnedVillagers && WasKeyPressed(Key.R))
            {
                int currentAge = sim.GetPlayerAge(localPid);
                bool isAgingUp = sim.IsPlayerAgingUp(localPid);
                if (currentAge < 3 && !isAgingUp)
                {
                    FlashActionButton(3);
                    ShowLandmarkChoicePanel(sim, localPid, currentAge + 1);
                }
                else if (currentAge >= 3)
                {
                    FlashActionButton(3);
                    selectionManager.EnterBuildPlacement(BuildingType.Wonder);
                }
                return;
            }

            // A = Attack Move (slot 4)
            if (WasKeyPressed(Key.A))
            {
                FlashActionButton(4);
                selectionManager.EnterAttackMoveMode();
                return;
            }

            // S = Halt (slot 5)
            if (WasKeyPressed(Key.S))
            {
                FlashActionButton(5);
                int[] unitIds = new int[units.Count];
                for (int i = 0; i < units.Count; i++) unitIds[i] = units[i].UnitId;
                sim.CommandBuffer.EnqueueCommand(new StopCommand(localPid, unitIds));
                return;
            }

            // D = Patrol (slot 6)
            if (WasKeyPressed(Key.D))
            {
                FlashActionButton(6);
                selectionManager.EnterPatrolMode();
                return;
            }

            // F = Garrison (slot 7)
            if (WasKeyPressed(Key.F))
            {
                FlashActionButton(7);
                selectionManager.EnterGarrisonMode();
                return;
            }
        }

        // =============================================================
        //  Tooltip helpers
        // =============================================================

        private static bool IsWallType(BuildingType type)
        {
            return type == BuildingType.Wall || type == BuildingType.StoneWall || type == BuildingType.StoneGate || type == BuildingType.WoodGate;
        }

        private void ShowActionTooltip(int index)
        {
            if (actionTooltips[index] == null) return;
            hoveredActionIndex = index;
            tooltipText.text = actionTooltips[index];

            // Position above the hovered button
            var btnRT = actionButtonGOs[index].GetComponent<RectTransform>();
            const float tooltipWidth = 200f;
            const float gap = 4f;
            const float padding = 8f;
            Vector2 panelPos = actionPanelRT.anchoredPosition;
            float tx = panelPos.x + Mathf.Clamp(btnRT.anchoredPosition.x, 0, ActionPanelWidth - tooltipWidth);
            float ty = panelPos.y + btnRT.anchoredPosition.y + ActionButtonSize + gap;

            tooltipText.ForceMeshUpdate();
            float textHeight = tooltipText.preferredHeight;
            tooltipPanelRT.sizeDelta = new Vector2(tooltipWidth, textHeight + padding * 2f);
            tooltipPanelRT.anchoredPosition = new Vector2(tx, ty);

            tooltipPanelGO.transform.SetAsLastSibling();
            tooltipPanelGO.SetActive(true);
            s_tooltipPanelRT = tooltipPanelRT;
        }

        private void HideActionTooltip(int index)
        {
            if (hoveredActionIndex == index)
            {
                hoveredActionIndex = -1;
                tooltipPanelGO.SetActive(false);
                s_tooltipPanelRT = null;
            }
        }

        private string GetUnitTooltip(int unitType)
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return $"<b>{GetUnitName(unitType)}</b>";
            var cfg = sim.Config;
            switch (unitType)
            {
                case 0: return $"<b>Villager</b>\nGathers resources and constructs buildings.\nCost: {cfg.VillagerFoodCost} <sprite name=\"food\">";
                case 1: return $"<b>Spearman</b>\nMelee infantry unit.\nCost: {cfg.SpearmanFoodCost} <sprite name=\"food\"> {cfg.SpearmanWoodCost} <sprite name=\"wood\">";
                case 2: return $"<b>Archer</b>\nRanged infantry unit.\nCost: {cfg.ArcherFoodCost} <sprite name=\"food\"> {cfg.ArcherWoodCost} <sprite name=\"wood\">";
                case 3: return $"<b>Horseman</b>\nMounted melee unit.\nCost: {cfg.HorsemanFoodCost} <sprite name=\"food\"> {cfg.HorsemanWoodCost} <sprite name=\"wood\">";
                case 4: return $"<b>Scout</b>\nFast mounted unit with high vision.\nCost: {cfg.ScoutFoodCost} <sprite name=\"food\">";
                case 10: return $"<b>Longbowman</b>\nEnglish unique ranged unit with extended range.\nCost: {cfg.LongbowmanFoodCost} <sprite name=\"food\"> {cfg.LongbowmanWoodCost} <sprite name=\"wood\">";
                case 11: return $"<b>Gendarme</b>\nFrench unique mounted unit with greater health.\nCost: {cfg.GendarmeFoodCost} <sprite name=\"food\"> {cfg.GendarmeWoodCost} <sprite name=\"wood\">";
                case 12: return $"<b>Landsknecht</b>\nHRE unique infantry with greater speed.\nCost: {cfg.LandsknechtFoodCost} <sprite name=\"food\"> {cfg.LandsknechtWoodCost} <sprite name=\"wood\">";
                default: return $"<b>{GetUnitName(unitType)}</b>";
            }
        }

        private void ShowQueueTooltip(int index)
        {
            if (index < 0 || index >= aggregatedQueue.Count) return;
            var entry = aggregatedQueue[index];

            tooltipText.text = GetUnitTooltip(entry.UnitType);

            var itemRT = queueItemGOs[index].GetComponent<RectTransform>();
            const float tooltipWidth = 200f;
            const float gap = 4f;
            const float padding = 8f;
            Vector2 panelPos = queuePanelRT.anchoredPosition;
            float tx = panelPos.x + itemRT.anchoredPosition.x;
            float ty = panelPos.y + QueuePanelHeight + gap;

            tooltipText.ForceMeshUpdate();
            float textHeight = tooltipText.preferredHeight;
            tooltipPanelRT.sizeDelta = new Vector2(tooltipWidth, textHeight + padding * 2f);
            tooltipPanelRT.anchoredPosition = new Vector2(tx, ty);

            tooltipPanelGO.transform.SetAsLastSibling();
            tooltipPanelGO.SetActive(true);
            s_tooltipPanelRT = tooltipPanelRT;
        }

        private void HideQueueTooltip()
        {
            tooltipPanelGO.SetActive(false);
            s_tooltipPanelRT = null;
        }

        // =============================================================
        //  Name helpers
        // =============================================================

        private string GetUnitName(int unitType)
        {
            if (unitType >= 0 && unitType < UnitTypeNames.Length) return UnitTypeNames[unitType];
            return "Unit";
        }

        private string GetUnitPlural(int unitType)
        {
            if (unitType >= 0 && unitType < UnitTypePlurals.Length) return UnitTypePlurals[unitType];
            return "Units";
        }

        private string GetBuildingName(BuildingType type)
        {
            int idx = (int)type;
            if (idx >= 0 && idx < BuildingTypeNames.Length) return BuildingTypeNames[idx];
            return "Building";
        }

        private string GetBuildingPlural(BuildingType type)
        {
            int idx = (int)type;
            if (idx >= 0 && idx < BuildingTypePlurals.Length) return BuildingTypePlurals[idx];
            return "Buildings";
        }

        private string GetResourceNodeName(ResourceType type)
        {
            int idx = (int)type;
            if (idx >= 0 && idx < ResourceNodeNames.Length) return ResourceNodeNames[idx];
            return "Resource";
        }

        private string GetResourceTypeName(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Food: return "Food";
                case ResourceType.Wood: return "Wood";
                case ResourceType.Gold: return "Gold";
                case ResourceType.Stone: return "Stone";
                default: return "Resource";
            }
        }
    }
}
