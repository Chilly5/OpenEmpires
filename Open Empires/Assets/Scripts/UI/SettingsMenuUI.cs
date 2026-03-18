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
        private GameObject controlsPanel;

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
            mainPanel.SetActive(false);
            controlsPanel.SetActive(true);
        }

        private void ShowMainSettings()
        {
            if (controlsPanel != null) controlsPanel.SetActive(false);
            if (mainPanel != null) mainPanel.SetActive(true);
        }

        private void BuildUI()
        {
            // Canvas
            var canvasGO = new GameObject("SettingsCanvas");
            canvasGO.transform.SetParent(transform, false);
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
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
            BuildControlsPanel(canvasGO.transform);

            controlsPanel.SetActive(false);
        }

        private void BuildMainPanel(Transform canvasParent)
        {
            float panelW = 400f;
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

            float y = panelH / 2f;

            // Title
            y -= 10f;
            y -= 28f;
            MakeLabel(panelGO.transform, "Settings", -panelW / 2f, y, panelW, 28f, 22, FontStyles.Bold, TextAlignmentOptions.Center);

            // Music Volume row
            y -= 40f;
            float labelW = 120f;
            float sliderW = 180f;
            float valueW = 50f;
            float rowX = -panelW / 2f + 20f;

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
            diagToggle.onValueChanged.AddListener(OnDiagToggleChanged);

            // Target Dummy buttons
            y -= 45f;
            CreateButton(panelGO.transform, "Place Target Dummy", -90f, y, 170f, 36f, () =>
            {
                IsPlacingDummy = true;
                Hide();
            });
            CreateButton(panelGO.transform, "Clear Dummies", 90f, y, 170f, 36f, () =>
            {
                var sim = GameBootstrapper.Instance?.Simulation;
                sim?.ClearAllDummies();
            });

            // Cheat buttons
            y -= 45f;
            CreateButton(panelGO.transform, "Resource Cheat", -90f, y, 170f, 36f, () =>
            {
                var sim = GameBootstrapper.Instance?.Simulation;
                if (sim == null) return;
                int pid = FindFirstObjectByType<UnitSelectionManager>()?.LocalPlayerId ?? 0;
                sim.CommandBuffer.EnqueueCommand(new CheatResourceCommand(pid));
            });
            TMP_Text prodLabel = null;
            var prodBtnGO = CreateButtonWithLabel(panelGO.transform, "Prod 10x: OFF", 0f, y, 170f, 36f, out prodLabel);
            var prodRT = prodBtnGO.GetComponent<RectTransform>();
            prodRT.pivot = new Vector2(0.5f, 0.5f);
            prodRT.anchoredPosition = new Vector2(90f, y);
            productionCheatLabel = prodLabel;
            prodBtnGO.GetComponent<Button>().onClick.AddListener(() =>
            {
                var sim = GameBootstrapper.Instance?.Simulation;
                if (sim == null) return;
                int pid = FindFirstObjectByType<UnitSelectionManager>()?.LocalPlayerId ?? 0;
                sim.CommandBuffer.EnqueueCommand(new CheatProductionCommand(pid));
            });

            // Vision cheat button + God powers cheat button
            y -= 45f;
            TMP_Text visLabel = null;
            var visBtnGO = CreateButtonWithLabel(panelGO.transform, "Vision: OFF", 0f, y, 170f, 36f, out visLabel);
            var visRT = visBtnGO.GetComponent<RectTransform>();
            visRT.pivot = new Vector2(0.5f, 0.5f);
            visRT.anchoredPosition = new Vector2(-90f, y);
            visionCheatLabel = visLabel;
            visBtnGO.GetComponent<Button>().onClick.AddListener(() =>
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
            gpRT.anchoredPosition = new Vector2(90f, y);
            godPowersCheatLabel = gpLabel;
            gpBtnGO.GetComponent<Button>().onClick.AddListener(() =>
            {
                bool newState = !GodPowerBarUI.IsCheatsEnabled;
                GodPowerBarUI.SetCheatsEnabled(newState);
            });

            // Controls button
            y -= 50f;
            CreateButton(panelGO.transform, "Controls", 0f, y, 160f, 36f, ShowControls);

            // Surrender button (red-tinted)
            y -= 44f;
            CreateSurrenderButton(panelGO.transform, 0f, y, 160f, 36f);

            // Resume button
            y -= 44f;
            CreateButton(panelGO.transform, "Resume Game", 0f, y, 160f, 36f, () => Hide());
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
    }
}
