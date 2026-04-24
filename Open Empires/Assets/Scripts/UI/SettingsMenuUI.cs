using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenEmpires
{
    public class SettingsMenuUI : MonoBehaviour
    {
        private static SettingsMenuUI instance;
        public static bool IsPlacingDummy { get; set; }
        private TMP_Text productionCheatLabel;
        private TMP_Text visionCheatLabel;
        private TMP_Text godPowersCheatLabel;

        private Canvas canvas;
        private GameObject root;
        private Slider volumeSlider;
        private TMP_Text volumeValueText;
        private Toggle muteToggle;
        private Toggle diagToggle;

        private GameObject mainPanel;
        private GameObject contentArea;
        private GameObject controlsPanel;
        private GameObject cameraPanel;
        private GameObject soundPanel;

        private InputActionRebindingExtensions.RebindingOperation currentRebind;
        public static bool IsRebinding => instance?.currentRebind != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (instance != null) return;

            var go = new GameObject("SettingsMenuUI");
            instance = go.AddComponent<SettingsMenuUI>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            BuildUI();
            root.SetActive(false);
        }

        public static void Open()
        {
            if (instance != null) instance.Show();
        }

        public static void Close()
        {
            if (instance != null) instance.Hide();
        }

        private void Update()
        {
            if (productionCheatLabel == null) return;
            var sim = GameBootstrapper.Instance?.Simulation;
            bool active = sim != null && sim.ProductionCheatActive;
            productionCheatLabel.text = active ? "Prod 10x: ON" : "Prod 10x: OFF";

            if (visionCheatLabel != null && sim != null)
            {
                int pid = FindFirstObjectByType<UnitSelectionManager>()?.LocalPlayerId ?? 0;
                bool visionActive = sim.FogOfWar.HasVisionCheat(pid);
                visionCheatLabel.text = visionActive ? "Vision: ON" : "Vision: OFF";
            }

            if (godPowersCheatLabel != null)
            {
                bool gpActive = GodPowerBarUI.IsCheatsEnabled;
                godPowersCheatLabel.text = gpActive ? "God Powers: ON" : "God Powers: OFF";
            }
        }

        private void Show()
        {
            // Sync UI to current MusicManager state
            var mm = MusicManager.Instance;
            if (mm != null)
            {
                volumeSlider.SetValueWithoutNotify(mm.MusicVolume);
                volumeValueText.text = Mathf.RoundToInt(mm.MusicVolume * 100).ToString();
                muteToggle.SetIsOnWithoutNotify(mm.IsMuted);
            }

            // Sync diagnostics toggle
            var diag = NetworkDiagnosticsUI.Instance;
            if (diag != null)
                diagToggle.SetIsOnWithoutNotify(diag.IsVisible);

            root.SetActive(true);
            UnitSelectionManager.SetSettingsMenuOpen(true);
            VirtualCursor.SetSettingsMenuOpen(true);
        }

        private void Hide()
        {
            currentRebind?.Cancel();
            currentRebind?.Dispose();
            currentRebind = null;
            ShowMainSettings();

            root.SetActive(false);
            UnitSelectionManager.SetSettingsMenuOpen(false);
            VirtualCursor.SetSettingsMenuOpen(false);
        }

        private void ShowControls()
        {
            SwitchToContent("", BuildControlsContent);
        }

        private void ShowCamera()
        {
            SwitchToContent("", BuildCameraContent);
        }

        private void ShowSound()
        {
            SwitchToContent("", BuildSoundContent);
        }

        private void ShowMainSettings()
        {
            SwitchToContent("", BuildMainSettingsContent);
        }

        private void SwitchToContent(string title, System.Action<GameObject, float, float> buildContentAction)
        {
            // Clear existing content area
            if (contentArea != null)
            {
                Object.Destroy(contentArea);
            }

            // Update title
            var titleLabel = mainPanel.transform.Find("TitleLabel");
            if (titleLabel != null)
            {
                var titleText = titleLabel.GetComponent<TextMeshProUGUI>();
                if (titleText != null) titleText.text = title;
            }

            // Create new content area
            contentArea = new GameObject("ContentArea");
            contentArea.transform.SetParent(mainPanel.transform, false);
            var contentRT = contentArea.AddComponent<RectTransform>();
            contentRT.anchorMin = Vector2.zero;
            contentRT.anchorMax = Vector2.one;
            contentRT.offsetMin = Vector2.zero;
            contentRT.offsetMax = Vector2.zero;

            // Build the content
            float contentX = 60f; // 120px sidebar / 2
            float startY = 250f;  // Start below title
            buildContentAction(contentArea, contentX, startY);
        }


        private void BuildUI()
        {
            // Canvas
            var canvasGO = new GameObject("SettingsCanvas");
            canvasGO.transform.SetParent(transform, false);
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            root = canvasGO;

            // Fullscreen dark overlay
            var overlayGO = new GameObject("Overlay");
            overlayGO.transform.SetParent(canvasGO.transform, false);
            var overlayRT = overlayGO.AddComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;
            var overlayImg = overlayGO.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.6f);

            BuildMainPanel(canvasGO.transform);
        }

        private void BuildMainPanel(Transform canvasParent)
        {
            float panelW = 600f; // Increased width to accommodate sidebar
            float panelH = 569f;
            var panelGO = new GameObject("Panel");
            panelGO.transform.SetParent(canvasParent, false);
            var panelRT = panelGO.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(panelW, panelH);
            var panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.12f, 0.12f, 1f);

            mainPanel = panelGO;

            // Left sidebar for section buttons
            float sidebarW = 120f;
            float sidebarX = -panelW / 2f + sidebarW / 2f;
            
            // Sidebar background
            var sidebarGO = new GameObject("Sidebar");
            sidebarGO.transform.SetParent(panelGO.transform, false);
            var sidebarRT = sidebarGO.AddComponent<RectTransform>();
            sidebarRT.anchorMin = new Vector2(0f, 0f);
            sidebarRT.anchorMax = new Vector2(0f, 1f);
            sidebarRT.pivot = new Vector2(0f, 0.5f);
            sidebarRT.anchoredPosition = new Vector2(0f, 0f);
            sidebarRT.sizeDelta = new Vector2(sidebarW, 0f);
            var sidebarImg = sidebarGO.AddComponent<Image>();
            sidebarImg.color = new Color(0.08f, 0.08f, 0.08f, 1f);

            // Section buttons in sidebar  
            float sidebarY = panelH / 2f - 50f;
            CreateButton(panelGO.transform, "Settings", sidebarX, sidebarY, sidebarW - 10f, 36f, ShowMainSettings);
            sidebarY -= 45f;
            CreateButton(panelGO.transform, "Camera", sidebarX, sidebarY, sidebarW - 10f, 36f, ShowCamera);
            sidebarY -= 45f;
            CreateButton(panelGO.transform, "Controls", sidebarX, sidebarY, sidebarW - 10f, 36f, ShowControls);
            sidebarY -= 45f;
            CreateButton(panelGO.transform, "Sound", sidebarX, sidebarY, sidebarW - 10f, 36f, ShowSound);

            // Main content area (right side)
            float contentX = sidebarW / 2f; // Offset content to the right of sidebar
            float y = panelH / 2f;

            // Title  
            y -= 10f;
            y -= 28f;
            var titleLabel = MakeLabel(panelGO.transform, "Settings", contentX - (panelW - sidebarW) / 2f, y, panelW - sidebarW, 28f, 22, FontStyles.Bold, TextAlignmentOptions.Center);
            titleLabel.gameObject.name = "TitleLabel";

            // Close button (X) in top-right corner
            var closeButtonGO = new GameObject("CloseButton");
            closeButtonGO.transform.SetParent(panelGO.transform, false);
            var closeButtonRT = closeButtonGO.AddComponent<RectTransform>();
            closeButtonRT.anchorMin = new Vector2(1f, 1f);
            closeButtonRT.anchorMax = new Vector2(1f, 1f);
            closeButtonRT.pivot = new Vector2(1f, 1f);
            closeButtonRT.anchoredPosition = new Vector2(-10f, -10f);
            closeButtonRT.sizeDelta = new Vector2(28f, 28f);

            var closeImg = closeButtonGO.AddComponent<Image>();
            closeImg.color = new Color(0.25f, 0.25f, 0.25f);

            var closeBtn = closeButtonGO.AddComponent<Button>();
            var closeColors = closeBtn.colors;
            closeColors.normalColor = new Color(0.25f, 0.25f, 0.25f);
            closeColors.highlightedColor = new Color(0.35f, 0.35f, 0.35f);
            closeColors.pressedColor = new Color(0.15f, 0.15f, 0.15f);
            closeBtn.colors = closeColors;
            closeBtn.onClick.AddListener(() => Hide());

            // X text
            var closeTextGO = new GameObject("Text");
            closeTextGO.transform.SetParent(closeButtonGO.transform, false);
            var closeTextRT = closeTextGO.AddComponent<RectTransform>();
            closeTextRT.anchorMin = Vector2.zero;
            closeTextRT.anchorMax = Vector2.one;
            closeTextRT.offsetMin = Vector2.zero;
            closeTextRT.offsetMax = Vector2.zero;
            var closeText = closeTextGO.AddComponent<TextMeshProUGUI>();
            closeText.text = "×";
            closeText.fontSize = 20;
            closeText.alignment = TextAlignmentOptions.Center;
            closeText.color = Color.white;

            // Initialize with main settings content
            SwitchToContent("", BuildMainSettingsContent);

        }

        private void BuildMainPanelWithContent(Transform canvasParent, string title, System.Action<GameObject, float, float> buildContentAction)
        {
            float panelW = 600f; // Increased width to accommodate sidebar
            float panelH = 569f;
            var panelGO = new GameObject("Panel");
            panelGO.transform.SetParent(canvasParent, false);
            var panelRT = panelGO.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(panelW, panelH);
            var panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.12f, 0.12f, 1f);

            mainPanel = panelGO;

            // Left sidebar for section buttons
            float sidebarW = 120f;
            float sidebarX = -panelW / 2f + sidebarW / 2f;
            
            // Sidebar background
            var sidebarGO = new GameObject("Sidebar");
            sidebarGO.transform.SetParent(panelGO.transform, false);
            var sidebarRT = sidebarGO.AddComponent<RectTransform>();
            sidebarRT.anchorMin = new Vector2(0f, 0f);
            sidebarRT.anchorMax = new Vector2(0f, 1f);
            sidebarRT.pivot = new Vector2(0f, 0.5f);
            sidebarRT.anchoredPosition = new Vector2(0f, 0f);
            sidebarRT.sizeDelta = new Vector2(sidebarW, 0f);
            var sidebarImg = sidebarGO.AddComponent<Image>();
            sidebarImg.color = new Color(0.08f, 0.08f, 0.08f, 1f);

            // Section buttons in sidebar
            float sidebarY = panelH / 2f - 50f;
            CreateButton(panelGO.transform, "Settings", sidebarX, sidebarY, sidebarW - 10f, 36f, ShowMainSettings);
            sidebarY -= 45f;
            CreateButton(panelGO.transform, "Camera", sidebarX, sidebarY, sidebarW - 10f, 36f, ShowCamera);
            sidebarY -= 45f;
            CreateButton(panelGO.transform, "Controls", sidebarX, sidebarY, sidebarW - 10f, 36f, ShowControls);
            sidebarY -= 45f;
            CreateButton(panelGO.transform, "Sound", sidebarX, sidebarY, sidebarW - 10f, 36f, ShowSound);

            // Main content area (right side)
            float contentX = sidebarW / 2f; // Offset content to the right of sidebar
            float y = panelH / 2f;

            // Title
            y -= 10f;
            y -= 28f;
            MakeLabel(panelGO.transform, title, contentX - (panelW - sidebarW) / 2f, y, panelW - sidebarW, 28f, 22, FontStyles.Bold, TextAlignmentOptions.Center);

            // Close button (X) in top-right corner
            var closeButtonGO = new GameObject("CloseButton");
            closeButtonGO.transform.SetParent(panelGO.transform, false);
            var closeButtonRT = closeButtonGO.AddComponent<RectTransform>();
            closeButtonRT.anchorMin = new Vector2(1f, 1f);
            closeButtonRT.anchorMax = new Vector2(1f, 1f);
            closeButtonRT.pivot = new Vector2(1f, 1f);
            closeButtonRT.anchoredPosition = new Vector2(-10f, -10f);
            closeButtonRT.sizeDelta = new Vector2(28f, 28f);

            var closeImg = closeButtonGO.AddComponent<Image>();
            closeImg.color = new Color(0.25f, 0.25f, 0.25f);

            var closeBtn = closeButtonGO.AddComponent<Button>();
            var closeColors = closeBtn.colors;
            closeColors.normalColor = new Color(0.25f, 0.25f, 0.25f);
            closeColors.highlightedColor = new Color(0.35f, 0.35f, 0.35f);
            closeColors.pressedColor = new Color(0.15f, 0.15f, 0.15f);
            closeBtn.colors = closeColors;
            closeBtn.onClick.AddListener(() => Hide());

            // X text
            var closeTextGO = new GameObject("Text");
            closeTextGO.transform.SetParent(closeButtonGO.transform, false);
            var closeTextRT = closeTextGO.AddComponent<RectTransform>();
            closeTextRT.anchorMin = Vector2.zero;
            closeTextRT.anchorMax = Vector2.one;
            closeTextRT.offsetMin = Vector2.zero;
            closeTextRT.offsetMax = Vector2.zero;
            var closeText = closeTextGO.AddComponent<TextMeshProUGUI>();
            closeText.text = "×";
            closeText.fontSize = 20;
            closeText.alignment = TextAlignmentOptions.Center;
            closeText.color = Color.white;

            // Build the specific content for this section
            buildContentAction(panelGO, contentX, y - 40f);
        }

        private void BuildControlsContent(GameObject panelGO, float contentX, float startY)
        {
            // Scrollable region between the title and the sticky bottom buttons
            float scrollTop = 220f;
            float scrollBottom = -180f;
            float scrollWidth = 480f;
            var scrollContent = CreateScrollView(panelGO.transform, contentX, scrollTop, scrollBottom, scrollWidth);

            // Items use anchor (0.5, 0.5) -> referenced from scroll content center.
            // Build with y treated as "distance below content top" (negative going down);
            // we resize content + shift children by H/2 once we know the final height.
            float y = -10f;
            float rowStartX = -160f;
            float actionLabelW = 160f;
            float keybindBtnW = 80f;
            float resetBtnW = 30f;
            float colGap = 8f;

            string[] actionNames = KeybindManager.ActionNames;
            for (int i = 0; i < actionNames.Length; i++)
            {
                y -= 40f;
                string actionName = actionNames[i];
                string displayName = KeybindManager.GetDisplayName(actionName);
                string currentBinding = KeybindManager.GetBinding(actionName);
                string keyText = KeybindManager.GetKeyDisplayName(currentBinding);

                MakeLabel(scrollContent, displayName, rowStartX, y, actionLabelW, 24f, 16, FontStyles.Normal, TextAlignmentOptions.Left);

                string capturedAction = actionName;
                float keybindX = rowStartX + actionLabelW + colGap;

                TMP_Text keybindLabel;
                var keybindBtnGO = CreateButtonWithLabel(scrollContent, "[" + keyText + "]", keybindX, y, keybindBtnW, 28f, out keybindLabel);
                keybindBtnGO.GetComponent<Button>().onClick.AddListener(() => {
                    StartRebind(capturedAction, keybindLabel);
                });

                float resetX = keybindX + keybindBtnW + colGap;
                CreateButton(scrollContent, "R", resetX, y, resetBtnW, 28f, () => {
                    ResetRow(capturedAction, keybindLabel);
                });
            }

            // Building Keybinds Section
            y -= 50f;
            MakeLabel(scrollContent, "Building Keybinds", rowStartX, y, actionLabelW * 2, 28f, 18, FontStyles.Bold, TextAlignmentOptions.Left);

            var buildingTypes = System.Enum.GetValues(typeof(BuildingType));
            foreach (BuildingType buildingType in buildingTypes)
            {
                string buildingName = buildingType.ToString();

                y -= 40f;
                MakeLabel(scrollContent, $"Cycle {buildingName}", rowStartX, y, actionLabelW, 24f, 16, FontStyles.Normal, TextAlignmentOptions.Left);

                float cycleKeybindX = rowStartX + actionLabelW + colGap;
                CreateButton(scrollContent, "[Unbound]", cycleKeybindX, y, keybindBtnW, 28f, () => {
                    // TODO: Add keybind functionality
                });

                float cycleResetX = cycleKeybindX + keybindBtnW + colGap;
                CreateButton(scrollContent, "R", cycleResetX, y, resetBtnW, 28f, () => {
                    // TODO: Add reset functionality
                });

                y -= 30f;
                MakeLabel(scrollContent, $"Select All {buildingName}", rowStartX, y, actionLabelW, 24f, 16, FontStyles.Normal, TextAlignmentOptions.Left);

                float selectAllKeybindX = rowStartX + actionLabelW + colGap;
                CreateButton(scrollContent, "[Unbound]", selectAllKeybindX, y, keybindBtnW, 28f, () => {
                    // TODO: Add keybind functionality
                });

                float selectAllResetX = selectAllKeybindX + keybindBtnW + colGap;
                CreateButton(scrollContent, "R", selectAllResetX, y, resetBtnW, 28f, () => {
                    // TODO: Add reset functionality
                });

                y -= 10f;
            }

            FinalizeScrollContent(scrollContent, -y + 10f);

            // Sticky bottom buttons (outside the scroll view, parented to contentArea)
            float btnY = -210f;
            CreateButton(panelGO.transform, "Reset All", contentX, btnY, 160f, 36f, () =>
            {
                KeybindManager.ResetAll();
                ShowControls();
            });

            btnY -= 44f;
            CreateButton(panelGO.transform, "Back to Settings", contentX, btnY, 160f, 36f, () => ShowMainSettings());
        }

        // Builds a vertically-scrolling region inside `parent`, occupying the rect
        // bounded by [topY, bottomY] vertically (in parent local coords) and `width` wide,
        // centered horizontally at `x`. Returns the content Transform that callers
        // should parent items to. Caller must call FinalizeScrollContent when done.
        private Transform CreateScrollView(Transform parent, float x, float topY, float bottomY, float width)
        {
            float h = topY - bottomY;
            float centerY = (topY + bottomY) / 2f;
            const float scrollbarW = 12f;

            var scrollGO = new GameObject("ScrollView");
            scrollGO.transform.SetParent(parent, false);
            var scrollRT = scrollGO.AddComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0.5f, 0.5f);
            scrollRT.anchorMax = new Vector2(0.5f, 0.5f);
            scrollRT.pivot = new Vector2(0.5f, 0.5f);
            scrollRT.anchoredPosition = new Vector2(x, centerY);
            scrollRT.sizeDelta = new Vector2(width, h);

            // Transparent backdrop with raycastTarget so wheel events over empty
            // space (between buttons) still bubble up to the ScrollRect.
            var scrollBg = scrollGO.AddComponent<Image>();
            scrollBg.color = Color.clear;
            scrollBg.raycastTarget = true;

            var scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 40f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = false;

            // Viewport — leave room on the right for the scrollbar.
            var viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var viewportRT = viewportGO.AddComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = Vector2.zero;
            viewportRT.offsetMax = new Vector2(-scrollbarW, 0f);
            viewportGO.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportRT;

            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRT = contentGO.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = Vector2.zero;
            scrollRect.content = contentRT;

            // Vertical scrollbar pinned to the right of the scroll view.
            var scrollbarGO = new GameObject("VerticalScrollbar");
            scrollbarGO.transform.SetParent(scrollGO.transform, false);
            var scrollbarRT = scrollbarGO.AddComponent<RectTransform>();
            scrollbarRT.anchorMin = new Vector2(1f, 0f);
            scrollbarRT.anchorMax = new Vector2(1f, 1f);
            scrollbarRT.pivot = new Vector2(1f, 0.5f);
            scrollbarRT.anchoredPosition = Vector2.zero;
            scrollbarRT.sizeDelta = new Vector2(scrollbarW, 0f);

            var scrollbarBg = scrollbarGO.AddComponent<Image>();
            scrollbarBg.color = new Color(0.18f, 0.18f, 0.18f);

            var scrollbar = scrollbarGO.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            var slidingAreaGO = new GameObject("Sliding Area");
            slidingAreaGO.transform.SetParent(scrollbarGO.transform, false);
            var slidingAreaRT = slidingAreaGO.AddComponent<RectTransform>();
            slidingAreaRT.anchorMin = Vector2.zero;
            slidingAreaRT.anchorMax = Vector2.one;
            slidingAreaRT.offsetMin = new Vector2(2f, 2f);
            slidingAreaRT.offsetMax = new Vector2(-2f, -2f);

            var handleGO = new GameObject("Handle");
            handleGO.transform.SetParent(slidingAreaGO.transform, false);
            var handleRT = handleGO.AddComponent<RectTransform>();
            handleRT.anchorMin = Vector2.zero;
            handleRT.anchorMax = Vector2.one;
            handleRT.offsetMin = Vector2.zero;
            handleRT.offsetMax = Vector2.zero;
            var handleImg = handleGO.AddComponent<Image>();
            handleImg.color = new Color(0.55f, 0.55f, 0.55f);

            scrollbar.targetGraphic = handleImg;
            scrollbar.handleRect = handleRT;
            scrollbar.transition = Selectable.Transition.ColorTint;
            var hc = scrollbar.colors;
            hc.normalColor = new Color(0.55f, 0.55f, 0.55f);
            hc.highlightedColor = new Color(0.7f, 0.7f, 0.7f);
            hc.pressedColor = new Color(0.4f, 0.4f, 0.4f);
            hc.selectedColor = new Color(0.55f, 0.55f, 0.55f);
            scrollbar.colors = hc;

            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            return contentGO.transform;
        }

        // Sizes the scroll content to `height` and shifts every child's anchored-Y by
        // height/2 so items built with "y = distance below content top" (negative going
        // down) end up correctly placed once the parent has its real size.
        private void FinalizeScrollContent(Transform content, float height)
        {
            var rt = content.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);

            float shift = height / 2f;
            for (int i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i) as RectTransform;
                if (child == null) continue;
                child.anchoredPosition = new Vector2(child.anchoredPosition.x, child.anchoredPosition.y + shift);
            }
        }

        private void BuildCameraContent(GameObject panelGO, float contentX, float startY)
        {
            float y = startY;
            float labelW = 180f;
            float sliderW = 160f;
            float valueW = 50f;
            float rowX = contentX - 180f; // Adjusted for sidebar
            
            var cameraController = FindObjectOfType<RTSCameraController>();
            
            // Pan Acceleration toggle
            MakeLabel(panelGO.transform, "Pan Acceleration", rowX, y, labelW, 24f, 16, FontStyles.Normal, TextAlignmentOptions.Left);
            var panAccelToggle = CreateToggle(panelGO.transform, rowX + labelW + 5f, y, 24f);
            if (cameraController != null) panAccelToggle.isOn = cameraController.PanAcceleration;
            panAccelToggle.onValueChanged.AddListener(value => {
                if (cameraController != null) {
                    cameraController.PanAcceleration = value;
                    cameraController.SaveCameraSettings();
                }
            });
            
            
            // Screen Edge Panning toggle
            y -= 40f;
            MakeLabel(panelGO.transform, "Screen Edge Panning", rowX, y, labelW, 24f, 16, FontStyles.Normal, TextAlignmentOptions.Left);
            var edgePanToggle = CreateToggle(panelGO.transform, rowX + labelW + 5f, y, 24f);
            if (cameraController != null) edgePanToggle.isOn = cameraController.EnableEdgeScroll;
            edgePanToggle.onValueChanged.AddListener(value => {
                if (cameraController != null) {
                    cameraController.EnableEdgeScroll = value;
                    cameraController.SaveCameraSettings();
                }
            });
            
            // Edge Pan While Box Selecting toggle
            y -= 40f;
            MakeLabel(panelGO.transform, "Edge Pan While Selecting", rowX, y, labelW, 24f, 16, FontStyles.Normal, TextAlignmentOptions.Left);
            var edgePanBoxToggle = CreateToggle(panelGO.transform, rowX + labelW + 5f, y, 24f);
            if (cameraController != null) edgePanBoxToggle.isOn = cameraController.EdgePanWhileBoxSelecting;
            edgePanBoxToggle.onValueChanged.AddListener(value => {
                if (cameraController != null) {
                    cameraController.EdgePanWhileBoxSelecting = value;
                    cameraController.SaveCameraSettings();
                }
            });
            
            // Screen Edge Pan Speed slider
            y -= 40f;
            MakeLabel(panelGO.transform, "Edge Pan Speed", rowX, y, labelW, 24f, 16, FontStyles.Normal, TextAlignmentOptions.Left);
            var edgePanSlider = CreateSlider(panelGO.transform, rowX + labelW + 5f, y, sliderW, 20f);
            var edgePanSliderComp = edgePanSlider.GetComponent<Slider>();
            edgePanSliderComp.minValue = 0.5f;
            edgePanSliderComp.maxValue = 3.0f;
            if (cameraController != null) edgePanSliderComp.value = cameraController.EdgePanSpeed;
            var edgePanValueText = MakeLabel(panelGO.transform, edgePanSliderComp.value.ToString("F1"), rowX + labelW + 5f + sliderW + 10f, y, valueW, 24f, 16, FontStyles.Normal, TextAlignmentOptions.Left);
            edgePanSliderComp.onValueChanged.AddListener(value => {
                edgePanValueText.text = value.ToString("F1");
                if (cameraController != null) {
                    cameraController.EdgePanSpeed = value;
                    cameraController.SaveCameraSettings();
                }
            });
            
            // Keyboard Pan Speed slider
            y -= 40f;
            MakeLabel(panelGO.transform, "Keyboard Pan Speed", rowX, y, labelW, 24f, 16, FontStyles.Normal, TextAlignmentOptions.Left);
            var keyboardPanSlider = CreateSlider(panelGO.transform, rowX + labelW + 5f, y, sliderW, 20f);
            var keyboardPanSliderComp = keyboardPanSlider.GetComponent<Slider>();
            keyboardPanSliderComp.minValue = 0.5f;
            keyboardPanSliderComp.maxValue = 3.0f;
            if (cameraController != null) keyboardPanSliderComp.value = cameraController.KeyboardPanSpeed;
            var keyboardPanValueText = MakeLabel(panelGO.transform, keyboardPanSliderComp.value.ToString("F1"), rowX + labelW + 5f + sliderW + 10f, y, valueW, 24f, 16, FontStyles.Normal, TextAlignmentOptions.Left);
            keyboardPanSliderComp.onValueChanged.AddListener(value => {
                keyboardPanValueText.text = value.ToString("F1");
                if (cameraController != null) {
                    cameraController.KeyboardPanSpeed = value;
                    cameraController.SaveCameraSettings();
                }
            });
            
            // Back button
            y -= 80f;
            CreateButton(panelGO.transform, "Back to Settings", contentX, y, 160f, 36f, () => ShowMainSettings());
        }

        private void BuildSoundContent(GameObject panelGO, float contentX, float startY)
        {
            float y = startY;
            
            
            // Back button
            y -= 100f;
            CreateButton(panelGO.transform, "Back to Settings", contentX, y, 160f, 36f, () => ShowMainSettings());
        }

        private void BuildMainSettingsContent(GameObject panelGO, float contentX, float startY)
        {
            float y = startY;
            float labelW = 120f;
            float sliderW = 180f;
            float valueW = 50f;
            float rowX = contentX - 160f; // Adjusted for sidebar

            // Music Volume row
            MakeLabel(panelGO.transform, "Music Volume", rowX, y, labelW, 24f, 16, FontStyles.Normal, TextAlignmentOptions.Left);

            // Slider
            var sliderGO = CreateSlider(panelGO.transform, rowX + labelW + 5f, y, sliderW, 20f);
            volumeSlider = sliderGO.GetComponent<Slider>();
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.value = 0.25f;
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);

            // Value text
            volumeValueText = MakeLabel(panelGO.transform, "25", rowX + labelW + 5f + sliderW + 10f, y, valueW, 24f, 16, FontStyles.Normal, TextAlignmentOptions.Left);

            // Mute toggle row
            y -= 40f;
            MakeLabel(panelGO.transform, "Mute Music", rowX, y, labelW, 24f, 16, FontStyles.Normal, TextAlignmentOptions.Left);
            muteToggle = CreateToggle(panelGO.transform, rowX + labelW + 5f, y, 24f);
            muteToggle.onValueChanged.AddListener(OnMuteChanged);

            // Net Diagnostics toggle row
            y -= 40f;
            MakeLabel(panelGO.transform, "Net Diagnostics", rowX, y, labelW, 24f, 16, FontStyles.Normal, TextAlignmentOptions.Left);
            diagToggle = CreateToggle(panelGO.transform, rowX + labelW + 5f, y, 24f);
            // Set initial state based on NetworkDiagnosticsUI visibility
            var diag = NetworkDiagnosticsUI.Instance;
            if (diag != null)
                diagToggle.SetIsOnWithoutNotify(diag.IsVisible);
            diagToggle.onValueChanged.AddListener(OnDiagToggleChanged);

            // Cheat buttons
            y -= 50f;

            var resourcesBtnGO = CreateButtonWithLabel(panelGO.transform, "Resources", 0f, y, 160f, 36f, out _);
            var resourcesRT = resourcesBtnGO.GetComponent<RectTransform>();
            resourcesRT.pivot = new Vector2(0.5f, 0.5f);
            resourcesRT.anchoredPosition = new Vector2(contentX - 90f, y);
            resourcesBtnGO.GetComponent<Button>().onClick.AddListener(() =>
            {
                var sim = GameBootstrapper.Instance?.Simulation;
                if (sim == null) return;
                int pid = FindFirstObjectByType<UnitSelectionManager>()?.LocalPlayerId ?? 0;
                sim.CommandBuffer.EnqueueCommand(new CheatResourceCommand(pid));
            });

            var visionBtnGO = CreateButtonWithLabel(panelGO.transform, "Vision", 0f, y, 160f, 36f, out _);
            var visionRT = visionBtnGO.GetComponent<RectTransform>();
            visionRT.pivot = new Vector2(0.5f, 0.5f);
            visionRT.anchoredPosition = new Vector2(contentX + 90f, y);
            visionBtnGO.GetComponent<Button>().onClick.AddListener(() =>
            {
                var sim = GameBootstrapper.Instance?.Simulation;
                if (sim == null) return;
                int pid = FindFirstObjectByType<UnitSelectionManager>()?.LocalPlayerId ?? 0;
                sim.CommandBuffer.EnqueueCommand(new CheatVisionCommand(pid));
            });

            TMP_Text gpLabel = null;
            var gpBtnGO = CreateButtonWithLabel(panelGO.transform, "God Powers: OFF", 0f, y, 170f, 36f, out gpLabel);
            var gpRT = gpBtnGO.GetComponent<RectTransform>();
            gpRT.pivot = new Vector2(0.5f, 0.5f);
            gpRT.anchoredPosition = new Vector2(contentX + 90f, y);
            godPowersCheatLabel = gpLabel;
            gpBtnGO.GetComponent<Button>().onClick.AddListener(() =>
            {
                bool newState = !GodPowerBarUI.IsCheatsEnabled;
                GodPowerBarUI.SetCheatsEnabled(newState);
            });

            // Surrender button (red-tinted)
            y -= 44f;
            CreateSurrenderButton(panelGO.transform, contentX, y, 160f, 36f);

            // Resume button
            y -= 44f;
            CreateButton(panelGO.transform, "Resume Game", contentX, y, 160f, 36f, () => Hide());
        }

        private void BuildControlsPanel(Transform canvasParent)
        {
            float panelW = 400f;
            float panelH = 220f;
            var panelGO = new GameObject("ControlsPanel");
            panelGO.transform.SetParent(canvasParent, false);
            var panelRT = panelGO.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(panelW, panelH);
            var panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.12f, 0.12f, 1f);

            controlsPanel = panelGO;

            float y = panelH / 2f;

            // Header
            y -= 10f;
            y -= 24f;
            MakeLabel(panelGO.transform, "Controls", -panelW / 2f, y, panelW, 28f, 20, FontStyles.Bold, TextAlignmentOptions.Center);

            // Close button (X) in top-right corner
            var closeButtonGO = new GameObject("CloseButton");
            closeButtonGO.transform.SetParent(panelGO.transform, false);
            var closeButtonRT = closeButtonGO.AddComponent<RectTransform>();
            closeButtonRT.anchorMin = new Vector2(1f, 1f);
            closeButtonRT.anchorMax = new Vector2(1f, 1f);
            closeButtonRT.pivot = new Vector2(1f, 1f);
            closeButtonRT.anchoredPosition = new Vector2(-10f, -10f);
            closeButtonRT.sizeDelta = new Vector2(28f, 28f);

            var closeImg = closeButtonGO.AddComponent<Image>();
            closeImg.color = new Color(0.25f, 0.25f, 0.25f);

            var closeBtn = closeButtonGO.AddComponent<Button>();
            var closeColors = closeBtn.colors;
            closeColors.normalColor = new Color(0.25f, 0.25f, 0.25f);
            closeColors.highlightedColor = new Color(0.35f, 0.35f, 0.35f);
            closeColors.pressedColor = new Color(0.15f, 0.15f, 0.15f);
            closeBtn.colors = closeColors;
            closeBtn.onClick.AddListener(() => Hide());

            // X text
            var closeTextGO = new GameObject("Text");
            closeTextGO.transform.SetParent(closeButtonGO.transform, false);
            var closeTextRT = closeTextGO.AddComponent<RectTransform>();
            closeTextRT.anchorMin = Vector2.zero;
            closeTextRT.anchorMax = Vector2.one;
            closeTextRT.offsetMin = Vector2.zero;
            closeTextRT.offsetMax = Vector2.zero;
            var closeText = closeTextGO.AddComponent<TextMeshProUGUI>();
            closeText.text = "×";
            closeText.fontSize = 20;
            closeText.alignment = TextAlignmentOptions.Center;
            closeText.color = Color.white;

            // Rows for each remappable action
            float rowStartX = -panelW / 2f + 20f;
            float actionLabelW = 160f;
            float keybindBtnW = 80f;
            float resetBtnW = 30f;
            float colGap = 8f;

            string[] actionNames = KeybindManager.ActionNames;
            for (int i = 0; i < actionNames.Length; i++)
            {
                y -= 40f;
                string actionName = actionNames[i];
                string displayName = KeybindManager.GetDisplayName(actionName);
                string currentBinding = KeybindManager.GetBinding(actionName);
                string keyText = KeybindManager.GetKeyDisplayName(currentBinding);

                // Action label
                MakeLabel(panelGO.transform, displayName, rowStartX, y, actionLabelW, 24f, 16, FontStyles.Normal, TextAlignmentOptions.Left);

                // Keybind button — capture locals for closure
                string capturedAction = actionName;
                float keybindX = rowStartX + actionLabelW + colGap;
                TMP_Text keyLabel = null;
                var keybindBtnGO = CreateButtonWithLabel(panelGO.transform, "[" + keyText + "]", keybindX, y, keybindBtnW, 28f, out keyLabel);
                var keybindBtn = keybindBtnGO.GetComponent<Button>();
                TMP_Text capturedLabel = keyLabel;
                keybindBtn.onClick.AddListener(() => StartRebind(capturedAction, capturedLabel));

                // Reset button
                float resetX = keybindX + keybindBtnW + colGap;
                TMP_Text resetLabel = null;
                var resetBtnGO = CreateButtonWithLabel(panelGO.transform, "Rst", resetX, y, resetBtnW, 28f, out resetLabel);
                var resetBtn = resetBtnGO.GetComponent<Button>();
                TMP_Text capturedKeyLabel = keyLabel;
                resetBtn.onClick.AddListener(() => ResetRow(capturedAction, capturedKeyLabel));
            }

            // Reset All button
            y -= 44f;
            CreateButton(panelGO.transform, "Reset All", 0f, y, 160f, 36f, () =>
            {
                KeybindManager.ResetAll();
                // Rebuild controls panel to refresh all labels
                Object.Destroy(controlsPanel);
                BuildControlsPanel(canvasParent);
                controlsPanel.SetActive(true);
                mainPanel.SetActive(false);
            });

            // Back button
            y -= 44f;
            CreateButton(panelGO.transform, "Back", 0f, y, 160f, 36f, ShowMainSettings);
        }

        private void StartRebind(string actionName, TMP_Text keyLabel)
        {
            var actions = UnitSelectionManager.RemappableActions;
            if (actions == null || !actions.TryGetValue(actionName, out var action)) return;

            string originalLabel = keyLabel.text;
            keyLabel.text = "...";
            action.Disable();

            currentRebind = action.PerformInteractiveRebinding()
                .WithControlsExcluding("<Mouse>")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(op =>
                {
                    string path = action.bindings[0].effectivePath;
                    KeybindManager.SetBinding(actionName, path);
                    keyLabel.text = "[" + KeybindManager.GetKeyDisplayName(path) + "]";
                    action.Enable();
                    op.Dispose();
                    currentRebind = null;
                })
                .OnCancel(op =>
                {
                    keyLabel.text = originalLabel;
                    action.Enable();
                    op.Dispose();
                    currentRebind = null;
                })
                .Start();
        }

        private void ResetRow(string actionName, TMP_Text keyLabel)
        {
            var actions = UnitSelectionManager.RemappableActions;
            if (actions == null || !actions.TryGetValue(actionName, out var action)) return;

            KeybindManager.ResetToDefault(actionName);
            action.RemoveAllBindingOverrides();
            keyLabel.text = "[" + KeybindManager.GetKeyDisplayName(KeybindManager.GetBinding(actionName)) + "]";
        }

        private void OnVolumeChanged(float value)
        {
            var mm = MusicManager.Instance;
            if (mm != null) mm.MusicVolume = value;
            volumeValueText.text = Mathf.RoundToInt(value * 100).ToString();
        }

        private void OnMuteChanged(bool muted)
        {
            var mm = MusicManager.Instance;
            if (mm != null) mm.IsMuted = muted;
        }

        private void OnDiagToggleChanged(bool value)
        {
            var diag = NetworkDiagnosticsUI.Instance;
            if (diag != null) diag.IsVisible = value;
        }

        private void CreateSurrenderButton(Transform parent, float x, float y, float w, float h)
        {
            var btnGO = new GameObject("SurrenderButton");
            btnGO.transform.SetParent(parent, false);
            var btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0.5f);
            btnRT.anchorMax = new Vector2(0.5f, 0.5f);
            btnRT.pivot = new Vector2(0.5f, 0.5f);
            btnRT.anchoredPosition = new Vector2(x, y);
            btnRT.sizeDelta = new Vector2(w, h);

            var img = btnGO.AddComponent<Image>();
            img.color = new Color(0.5f, 0.15f, 0.15f);

            var btn = btnGO.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.5f, 0.15f, 0.15f);
            colors.highlightedColor = new Color(0.6f, 0.2f, 0.2f);
            colors.pressedColor = new Color(0.35f, 0.1f, 0.1f);
            btn.colors = colors;
            btn.onClick.AddListener(OnSurrenderClicked);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(btnGO.transform, false);
            var trt = textGO.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "Surrender";
            tmp.fontSize = 16;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
        }

        private void OnSurrenderClicked()
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null || sim.IsMatchOver) return;

            int localPlayerId = FindFirstObjectByType<UnitSelectionManager>()?.LocalPlayerId ?? 0;
            if (sim.SurrenderedPlayers.Contains(localPlayerId)) return;

            // In team game, check if there's already an active vote for this team
            int teamId = (localPlayerId < sim.PlayerTeamIds.Length) ? sim.PlayerTeamIds[localPlayerId] : localPlayerId;
            if (sim.ActiveSurrenderVotes.ContainsKey(teamId))
            {
                ChatManager.AddSystemMessage("Surrender vote already in progress.");
                Hide();
                return;
            }

            sim.CommandBuffer.EnqueueCommand(new SurrenderVoteCommand(localPlayerId, true));
            Hide();
        }

        // --- UI Helpers ---

        private TMP_Text MakeLabel(Transform parent, string text, float x, float y, float w, float h,
            float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            tmp.alignment = alignment;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            return tmp;
        }

        private GameObject CreateSlider(Transform parent, float x, float y, float w, float h)
        {
            var sliderGO = new GameObject("Slider");
            sliderGO.transform.SetParent(parent, false);
            var sliderRT = sliderGO.AddComponent<RectTransform>();
            sliderRT.anchorMin = new Vector2(0.5f, 0.5f);
            sliderRT.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRT.pivot = new Vector2(0f, 0.5f);
            sliderRT.anchoredPosition = new Vector2(x, y);
            sliderRT.sizeDelta = new Vector2(w, h);

            // Background
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(sliderGO.transform, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0f, 0.25f);
            bgRT.anchorMax = new Vector2(1f, 0.75f);
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f);

            // Fill area
            var fillAreaGO = new GameObject("Fill Area");
            fillAreaGO.transform.SetParent(sliderGO.transform, false);
            var fillAreaRT = fillAreaGO.AddComponent<RectTransform>();
            fillAreaRT.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRT.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRT.offsetMin = Vector2.zero;
            fillAreaRT.offsetMax = Vector2.zero;

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            var fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.color = new Color(0.3f, 0.6f, 0.9f);

            // Handle slide area
            var handleAreaGO = new GameObject("Handle Slide Area");
            handleAreaGO.transform.SetParent(sliderGO.transform, false);
            var handleAreaRT = handleAreaGO.AddComponent<RectTransform>();
            handleAreaRT.anchorMin = Vector2.zero;
            handleAreaRT.anchorMax = Vector2.one;
            handleAreaRT.offsetMin = new Vector2(10f, 0f);
            handleAreaRT.offsetMax = new Vector2(-10f, 0f);

            var handleGO = new GameObject("Handle");
            handleGO.transform.SetParent(handleAreaGO.transform, false);
            var handleRT = handleGO.AddComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(20f, 0f);
            handleRT.anchorMin = new Vector2(0f, 0f);
            handleRT.anchorMax = new Vector2(0f, 1f);
            var handleImg = handleGO.AddComponent<Image>();
            handleImg.color = Color.white;

            var slider = sliderGO.AddComponent<Slider>();
            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;

            return sliderGO;
        }

        private Toggle CreateToggle(Transform parent, float x, float y, float size)
        {
            var toggleGO = new GameObject("Toggle");
            toggleGO.transform.SetParent(parent, false);
            var toggleRT = toggleGO.AddComponent<RectTransform>();
            toggleRT.anchorMin = new Vector2(0.5f, 0.5f);
            toggleRT.anchorMax = new Vector2(0.5f, 0.5f);
            toggleRT.pivot = new Vector2(0f, 0.5f);
            toggleRT.anchoredPosition = new Vector2(x, y);
            toggleRT.sizeDelta = new Vector2(size, size);

            // Background box
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(toggleGO.transform, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f);

            // Checkmark
            var checkGO = new GameObject("Checkmark");
            checkGO.transform.SetParent(bgGO.transform, false);
            var checkRT = checkGO.AddComponent<RectTransform>();
            checkRT.anchorMin = new Vector2(0.1f, 0.1f);
            checkRT.anchorMax = new Vector2(0.9f, 0.9f);
            checkRT.offsetMin = Vector2.zero;
            checkRT.offsetMax = Vector2.zero;
            var checkImg = checkGO.AddComponent<Image>();
            checkImg.color = new Color(0.3f, 0.6f, 0.9f);

            var toggle = toggleGO.AddComponent<Toggle>();
            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;
            toggle.isOn = false;

            return toggle;
        }

        private void CreateButton(Transform parent, string label, float x, float y, float w, float h, System.Action onClick)
        {
            var btnGO = new GameObject("Button");
            btnGO.transform.SetParent(parent, false);
            var btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0.5f);
            btnRT.anchorMax = new Vector2(0.5f, 0.5f);
            btnRT.pivot = new Vector2(0.5f, 0.5f);
            btnRT.anchoredPosition = new Vector2(x, y);
            btnRT.sizeDelta = new Vector2(w, h);

            var img = btnGO.AddComponent<Image>();
            img.color = new Color(0.25f, 0.25f, 0.25f);

            var btn = btnGO.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.25f, 0.25f, 0.25f);
            colors.highlightedColor = new Color(0.35f, 0.35f, 0.35f);
            colors.pressedColor = new Color(0.15f, 0.15f, 0.15f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(btnGO.transform, false);
            var trt = textGO.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 16;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
        }

        // Left-anchored button used for keybind and reset cells in the Controls panel
        private GameObject CreateButtonWithLabel(Transform parent, string label, float x, float y, float w, float h, out TMP_Text labelText)
        {
            var btnGO = new GameObject("Button");
            btnGO.transform.SetParent(parent, false);
            var btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0.5f);
            btnRT.anchorMax = new Vector2(0.5f, 0.5f);
            btnRT.pivot = new Vector2(0f, 0.5f);
            btnRT.anchoredPosition = new Vector2(x, y);
            btnRT.sizeDelta = new Vector2(w, h);

            var img = btnGO.AddComponent<Image>();
            img.color = new Color(0.25f, 0.25f, 0.25f);

            var btn = btnGO.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.25f, 0.25f, 0.25f);
            colors.highlightedColor = new Color(0.35f, 0.35f, 0.35f);
            colors.pressedColor = new Color(0.15f, 0.15f, 0.15f);
            btn.colors = colors;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(btnGO.transform, false);
            var trt = textGO.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 16;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            labelText = tmp;
            return btnGO;
        }

        private void BuildCameraPanel(Transform canvasParent)
        {
            float panelW = 600f;
            float panelH = 569f;
            var panelGO = new GameObject("CameraPanel");
            panelGO.transform.SetParent(canvasParent, false);
            var panelRT = panelGO.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(panelW, panelH);
            var panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.12f, 0.12f, 1f);

            cameraPanel = panelGO;

            // Reuse the same sidebar structure
            BuildSidebar(panelGO, "Camera Settings");

            float contentX = 60f; // Offset content to the right of sidebar
            float y = panelH / 2f - 80f;

            // Placeholder text
            MakeLabel(panelGO.transform, "Camera settings will be added here", contentX - 200f, y, 400f, 24f, 14, FontStyles.Italic, TextAlignmentOptions.Center);
        }

        private void BuildSoundPanel(Transform canvasParent)
        {
            float panelW = 600f;
            float panelH = 569f;
            var panelGO = new GameObject("SoundPanel");
            panelGO.transform.SetParent(canvasParent, false);
            var panelRT = panelGO.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(panelW, panelH);
            var panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.12f, 0.12f, 1f);

            soundPanel = panelGO;

            // Reuse the same sidebar structure
            BuildSidebar(panelGO, "Sound Settings");

            float contentX = 60f; // Offset content to the right of sidebar
            float y = panelH / 2f - 80f;

            // Placeholder text for future sound settings
            MakeLabel(panelGO.transform, "Additional sound settings will be added here", contentX - 200f, y, 400f, 24f, 14, FontStyles.Italic, TextAlignmentOptions.Center);
        }

        private void BuildSidebar(GameObject panelGO, string title)
        {
            float panelW = 600f;
            float panelH = 569f;
            float sidebarW = 120f;
            float sidebarX = -panelW / 2f + sidebarW / 2f;
            
            // Sidebar background
            var sidebarGO = new GameObject("Sidebar");
            sidebarGO.transform.SetParent(panelGO.transform, false);
            var sidebarRT = sidebarGO.AddComponent<RectTransform>();
            sidebarRT.anchorMin = new Vector2(0f, 0f);
            sidebarRT.anchorMax = new Vector2(0f, 1f);
            sidebarRT.pivot = new Vector2(0f, 0.5f);
            sidebarRT.anchoredPosition = new Vector2(0f, 0f);
            sidebarRT.sizeDelta = new Vector2(sidebarW, 0f);
            var sidebarImg = sidebarGO.AddComponent<Image>();
            sidebarImg.color = new Color(0.08f, 0.08f, 0.08f, 1f);

            // Section buttons in sidebar
            float sidebarY = panelH / 2f - 50f;
            CreateButton(panelGO.transform, "Camera", sidebarX, sidebarY, sidebarW - 10f, 36f, ShowCamera);
            sidebarY -= 45f;
            CreateButton(panelGO.transform, "Controls", sidebarX, sidebarY, sidebarW - 10f, 36f, ShowControls);
            sidebarY -= 45f;
            CreateButton(panelGO.transform, "Sound", sidebarX, sidebarY, sidebarW - 10f, 36f, ShowSound);

            // Title
            float contentX = sidebarW / 2f;
            float y = panelH / 2f - 38f;
            MakeLabel(panelGO.transform, title, contentX - (panelW - sidebarW) / 2f, y, panelW - sidebarW, 28f, 22, FontStyles.Bold, TextAlignmentOptions.Center);

            // Close button (X) in top-right corner
            var closeButtonGO = new GameObject("CloseButton");
            closeButtonGO.transform.SetParent(panelGO.transform, false);
            var closeButtonRT = closeButtonGO.AddComponent<RectTransform>();
            closeButtonRT.anchorMin = new Vector2(1f, 1f);
            closeButtonRT.anchorMax = new Vector2(1f, 1f);
            closeButtonRT.pivot = new Vector2(1f, 1f);
            closeButtonRT.anchoredPosition = new Vector2(-10f, -10f);
            closeButtonRT.sizeDelta = new Vector2(28f, 28f);

            var closeImg = closeButtonGO.AddComponent<Image>();
            closeImg.color = new Color(0.25f, 0.25f, 0.25f);

            var closeBtn = closeButtonGO.AddComponent<Button>();
            var closeColors = closeBtn.colors;
            closeColors.normalColor = new Color(0.25f, 0.25f, 0.25f);
            closeColors.highlightedColor = new Color(0.35f, 0.35f, 0.35f);
            closeColors.pressedColor = new Color(0.15f, 0.15f, 0.15f);
            closeBtn.colors = closeColors;
            closeBtn.onClick.AddListener(() => Hide());

            // X text
            var closeTextGO = new GameObject("Text");
            closeTextGO.transform.SetParent(closeButtonGO.transform, false);
            var closeTextRT = closeTextGO.AddComponent<RectTransform>();
            closeTextRT.anchorMin = Vector2.zero;
            closeTextRT.anchorMax = Vector2.one;
            closeTextRT.offsetMin = Vector2.zero;
            closeTextRT.offsetMax = Vector2.zero;
            var closeText = closeTextGO.AddComponent<TextMeshProUGUI>();
            closeText.text = "×";
            closeText.fontSize = 20;
            closeText.alignment = TextAlignmentOptions.Center;
            closeText.color = Color.white;
        }
    }
}
