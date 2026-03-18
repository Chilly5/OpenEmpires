using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenEmpires
{
    public class ResourceUI : MonoBehaviour
    {
        private int localPlayerId = 0;

        private const float PanelWidth = 130f;
        private const float PanelHeight = 166f;
        private const float PopPanelWidth = 78f;
        private const float PopPanelHeight = 30f;
        private const float PopGap = 6f;
        private const float Padding = 8f;
        private const float Margin = 10f;
        private const float ReferenceWidth = 1280f;
        private const float ReferenceHeight = 720f;

        // Production summary constants
        private const int MaxProdRows = 9;
        private const float ProdRowHeight = 30f;
        private const float ProdGap = 6f;
        private const float ProdBoxWidth = 28f;
        private const float ProdBoxHeight = 28f;

        private static readonly Color TrainingBarColor = new Color(0.2f, 0.4f, 0.9f);
        private static readonly Color BarBgColor = new Color(0.1f, 0.1f, 0.1f);

        private const float IconSize = 24f;
        private const float IconTextGap = 4f;

        private GameObject resourceCanvas;
        private RectTransform panelRT;
        private RectTransform popPanelRT;
        private TMP_Text foodText;
        private TMP_Text woodText;
        private TMP_Text goldText;
        private TMP_Text stoneText;
        private TMP_Text popText;
        private Image foodIcon;
        private Image woodIcon;
        private Image goldIcon;
        private Image stoneIcon;
        private Image popIcon;

        // Pop-capped warning
        private TMP_Text warningText;
        private CanvasGroup warningCanvasGroup;
        private float warningTimer;
        private bool warningShownForCurrentCap;
        private int lastPopCap = -1;

        // Idle villager UI
        private GameObject idleVillagerPanel;
        private RectTransform idleVillagerPanelRT;
        private TMP_Text idleVillagerText;
        private TMP_Text idleVillagerLabel;

        private int lastSelectedIdleVillagerIndex = -1;

        // Production summary panel
        private GameObject prodPanelGO;
        private RectTransform prodPanelRT;
        private GameObject[] prodRowGOs;
        private TMP_Text[] prodRowTexts;
        private Image[] prodRowFills;
        private Image[] prodRowBoxIcons;
        private Image[] prodRowBoxBgs;

        // Game timer
        private TMP_Text timerText;

        // Fullscreen button
        private GameObject fullscreenButtonGO;

        // Reusable per-frame accumulators (avoid allocation)
        private readonly int[] prodCounts = new int[MaxProdRows];
        private readonly float[] prodProgressSum = new float[MaxProdRows];
        private readonly int[] prodProgressCount = new int[MaxProdRows];
        private readonly int[] gatherCounts = new int[4]; // Food, Wood, Gold, Stone
        private int resourceUIFrameCounter;
        private int cachedIdleCount;

        public void SetLocalPlayerId(int playerId)
        {
            localPlayerId = playerId;
        }

        private void Awake()
        {
            // Create canvas
            var canvasGO = new GameObject("ResourceCanvas");
            canvasGO.transform.SetParent(transform);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Panel background
            var panelGO = new GameObject("ResourcePanel");
            panelGO.transform.SetParent(canvasGO.transform, false);
            panelRT = panelGO.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0, 0);
            panelRT.anchorMax = new Vector2(0, 0);
            panelRT.pivot = new Vector2(0, 0);
            panelRT.anchoredPosition = new Vector2(Margin, Margin);
            panelRT.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            var panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0, 0, 0, 1f);
            var panelOutline = panelGO.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            panelOutline.effectDistance = new Vector2(2, -2);

            ResourceIcons.EnsureLoaded();
            UnitIcons.EnsureLoaded();

            float rowHeight = (PanelHeight - Padding * 2f) / 4f;
            foodText = CreateIconRow(panelGO.transform, "Food", Padding, PanelHeight - Padding - rowHeight, rowHeight, ResourceIcons.Get(ResourceType.Food), out foodIcon);
            woodText = CreateIconRow(panelGO.transform, "Wood", Padding, PanelHeight - Padding - rowHeight * 2, rowHeight, ResourceIcons.Get(ResourceType.Wood), out woodIcon);
            goldText = CreateIconRow(panelGO.transform, "Gold", Padding, PanelHeight - Padding - rowHeight * 3, rowHeight, ResourceIcons.Get(ResourceType.Gold), out goldIcon);
            stoneText = CreateIconRow(panelGO.transform, "Stone", Padding, PanelHeight - Padding - rowHeight * 4, rowHeight, ResourceIcons.Get(ResourceType.Stone), out stoneIcon);

            // Population panel (above resource panel)
            var popGO = new GameObject("PopulationPanel");
            popGO.transform.SetParent(canvasGO.transform, false);
            popPanelRT = popGO.AddComponent<RectTransform>();
            popPanelRT.anchorMin = new Vector2(0, 0);
            popPanelRT.anchorMax = new Vector2(0, 0);
            popPanelRT.pivot = new Vector2(0, 0);
            popPanelRT.anchoredPosition = new Vector2(Margin, Margin + PanelHeight + PopGap);
            popPanelRT.sizeDelta = new Vector2(PopPanelWidth, PopPanelHeight);
            var popImg = popGO.AddComponent<Image>();
            popImg.color = new Color(0, 0, 0, 1f);
            var popOutline = popGO.AddComponent<Outline>();
            popOutline.effectColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            popOutline.effectDistance = new Vector2(2, -2);

            // House icon in pop panel
            BuildingIcons.EnsureLoaded();
            var popIconGO = new GameObject("PopIcon");
            popIconGO.transform.SetParent(popGO.transform, false);
            var popIconRT = popIconGO.AddComponent<RectTransform>();
            popIconRT.anchorMin = new Vector2(0, 0.5f);
            popIconRT.anchorMax = new Vector2(0, 0.5f);
            popIconRT.pivot = new Vector2(0, 0.5f);
            popIconRT.anchoredPosition = new Vector2(4f, 0);
            popIconRT.sizeDelta = new Vector2(20f, 20f);
            popIcon = popIconGO.AddComponent<Image>();
            popIcon.sprite = BuildingIcons.Get(BuildingType.House);
            popIcon.preserveAspect = true;
            popIcon.raycastTarget = false;

            var popLabel = new GameObject("PopText");
            popLabel.transform.SetParent(popGO.transform, false);
            var popLabelRT = popLabel.AddComponent<RectTransform>();
            popLabelRT.anchorMin = new Vector2(0, 0);
            popLabelRT.anchorMax = new Vector2(1, 1);
            popLabelRT.offsetMin = new Vector2(26f, 0);
            popLabelRT.offsetMax = new Vector2(-2f, 0);
            popText = popLabel.AddComponent<TextMeshProUGUI>();
            popText.fontSize = 14;
            popText.enableAutoSizing = true;
            popText.fontSizeMin = 10;
            popText.fontSizeMax = 14;
            popText.fontStyle = FontStyles.Bold;
            popText.color = Color.white;
            popText.alignment = TextAlignmentOptions.Left;
            popText.overflowMode = TextOverflowModes.Ellipsis;

            // Center-screen warning text
            BuildWarningLabel(canvasGO.transform);

            // Production summary panel (above population panel)
            BuildProductionPanel(canvasGO.transform);

            // Idle villager panel (bottom right)
            BuildIdleVillagerPanel(canvasGO.transform);

            // Game timer (top center)
            BuildTimerPanel(canvasGO.transform);

            // Fullscreen button (below timer)
            BuildFullscreenButton(canvasGO.transform);

            resourceCanvas = canvasGO;
            resourceCanvas.SetActive(false);
        }

        private void BuildProductionPanel(Transform parent)
        {
            float maxPanelH = MaxProdRows * ProdRowHeight + Padding * 2f;
            float prodY = Margin + PanelHeight + PopGap + PopPanelHeight + ProdGap;

            prodPanelGO = new GameObject("ProductionPanel");
            prodPanelGO.transform.SetParent(parent, false);
            prodPanelRT = prodPanelGO.AddComponent<RectTransform>();
            prodPanelRT.anchorMin = new Vector2(0, 0);
            prodPanelRT.anchorMax = new Vector2(0, 0);
            prodPanelRT.pivot = new Vector2(0, 0);
            prodPanelRT.anchoredPosition = new Vector2(Margin, prodY);
            prodPanelRT.sizeDelta = new Vector2(PanelWidth, maxPanelH);
            var bgImg = prodPanelGO.AddComponent<Image>();
            bgImg.color = new Color(0, 0, 0, 1f);
            var prodOutline = prodPanelGO.AddComponent<Outline>();
            prodOutline.effectColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            prodOutline.effectDistance = new Vector2(2, -2);

            prodRowGOs = new GameObject[MaxProdRows];
            prodRowTexts = new TMP_Text[MaxProdRows];
            prodRowFills = new Image[MaxProdRows];
            prodRowBoxIcons = new Image[MaxProdRows];
            prodRowBoxBgs = new Image[MaxProdRows];

            float contentW = PanelWidth - Padding * 2f;

            for (int i = 0; i < MaxProdRows; i++)
            {
                // Row container
                var rowGO = new GameObject($"ProdRow{i}");
                rowGO.transform.SetParent(prodPanelGO.transform, false);
                var rowRT = rowGO.AddComponent<RectTransform>();
                rowRT.anchorMin = new Vector2(0, 1);
                rowRT.anchorMax = new Vector2(0, 1);
                rowRT.pivot = new Vector2(0, 1);
                float rowY = -Padding - i * ProdRowHeight;
                rowRT.anchoredPosition = new Vector2(Padding, rowY);
                rowRT.sizeDelta = new Vector2(contentW, ProdRowHeight);

                // Count text (left-aligned, e.g. "12")
                var textGO = new GameObject("CountText");
                textGO.transform.SetParent(rowGO.transform, false);
                var trt = textGO.AddComponent<RectTransform>();
                trt.anchorMin = new Vector2(0, 0);
                trt.anchorMax = new Vector2(0, 1);
                trt.pivot = new Vector2(0, 0.5f);
                trt.anchoredPosition = Vector2.zero;
                trt.sizeDelta = new Vector2(30f, 0);
                var tmp = textGO.AddComponent<TextMeshProUGUI>();
                tmp.fontSize = 14;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Left;
                tmp.overflowMode = TextOverflowModes.Overflow;
                prodRowTexts[i] = tmp;

                // Queue-style box (right-aligned within the row)
                var boxGO = new GameObject("Box");
                boxGO.transform.SetParent(rowGO.transform, false);
                var boxRT = boxGO.AddComponent<RectTransform>();
                boxRT.anchorMin = new Vector2(1, 0.5f);
                boxRT.anchorMax = new Vector2(1, 0.5f);
                boxRT.pivot = new Vector2(1, 0.5f);
                boxRT.anchoredPosition = Vector2.zero;
                boxRT.sizeDelta = new Vector2(ProdBoxWidth, ProdBoxHeight);
                var boxBgImg = boxGO.AddComponent<Image>();
                boxBgImg.color = new Color(0.06f, 0.06f, 0.06f);
                prodRowBoxBgs[i] = boxBgImg;

                // Fill overlay (anchor-based left-to-right)
                var fillGO = new GameObject("Fill");
                fillGO.transform.SetParent(boxGO.transform, false);
                var fillRT = fillGO.AddComponent<RectTransform>();
                fillRT.anchorMin = Vector2.zero;
                fillRT.anchorMax = new Vector2(0, 1);
                fillRT.pivot = new Vector2(0, 0.5f);
                fillRT.offsetMin = Vector2.zero;
                fillRT.offsetMax = Vector2.zero;
                var fillImg = fillGO.AddComponent<Image>();
                fillImg.color = TrainingBarColor;
                fillImg.raycastTarget = false;
                prodRowFills[i] = fillImg;

                // Centered unit icon inside box
                var boxIconGO = new GameObject("BoxIcon");
                boxIconGO.transform.SetParent(boxGO.transform, false);
                var biRT = boxIconGO.AddComponent<RectTransform>();
                biRT.anchorMin = new Vector2(0.5f, 0.5f);
                biRT.anchorMax = new Vector2(0.5f, 0.5f);
                biRT.pivot = new Vector2(0.5f, 0.5f);
                biRT.anchoredPosition = Vector2.zero;
                biRT.sizeDelta = new Vector2(22f, 22f);
                var boxIcon = boxIconGO.AddComponent<Image>();
                boxIcon.preserveAspect = true;
                boxIcon.raycastTarget = false;
                prodRowBoxIcons[i] = boxIcon;

                prodRowGOs[i] = rowGO;
                rowGO.SetActive(false);
            }

            prodPanelGO.SetActive(false);
        }

        private void BuildIdleVillagerPanel(Transform parent)
        {
            const float IdlePanelWidth = 50f;
            const float IdlePanelHeight = 30f;

            idleVillagerPanel = new GameObject("IdleVillagerPanel");
            idleVillagerPanel.transform.SetParent(parent, false);
            idleVillagerPanelRT = idleVillagerPanel.AddComponent<RectTransform>();
            idleVillagerPanelRT.anchorMin = new Vector2(0, 0);
            idleVillagerPanelRT.anchorMax = new Vector2(0, 0);
            idleVillagerPanelRT.pivot = new Vector2(0, 0);
            // Position next to population panel (to the right, fitting within resource panel width)
            idleVillagerPanelRT.anchoredPosition = new Vector2(Margin + PopPanelWidth + 2f, Margin + PanelHeight + PopGap);
            idleVillagerPanelRT.sizeDelta = new Vector2(IdlePanelWidth, IdlePanelHeight);
            var bgImg = idleVillagerPanel.AddComponent<Image>();
            bgImg.color = new Color(0, 0, 0, 1f);
            var idleOutline = idleVillagerPanel.AddComponent<Outline>();
            idleOutline.effectColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            idleOutline.effectDistance = new Vector2(2, -2);

            // Add button component for clicking
            var button = idleVillagerPanel.AddComponent<Button>();
            button.targetGraphic = bgImg;
            button.onClick.AddListener(OnIdleVillagerClicked);

            // "Zzz" label (left side)
            var labelGO = new GameObject("IdleLabel");
            labelGO.transform.SetParent(idleVillagerPanel.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0, 0);
            labelRT.anchorMax = new Vector2(0.5f, 1);
            labelRT.offsetMin = new Vector2(4f, 0);
            labelRT.offsetMax = Vector2.zero;
            idleVillagerLabel = labelGO.AddComponent<TextMeshProUGUI>();
            idleVillagerLabel.text = "Zz";
            idleVillagerLabel.fontSize = 14;
            idleVillagerLabel.fontStyle = FontStyles.Bold;
            idleVillagerLabel.color = Color.white;
            idleVillagerLabel.alignment = TextAlignmentOptions.MidlineLeft;
            idleVillagerLabel.raycastTarget = false;

            // Count text (right side)
            var textGO = new GameObject("IdleText");
            textGO.transform.SetParent(idleVillagerPanel.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0.5f, 0);
            textRT.anchorMax = new Vector2(1, 1);
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = new Vector2(-4f, 0);
            idleVillagerText = textGO.AddComponent<TextMeshProUGUI>();
            idleVillagerText.fontSize = 16;
            idleVillagerText.fontStyle = FontStyles.Bold;
            idleVillagerText.color = Color.white;
            idleVillagerText.alignment = TextAlignmentOptions.Center;
            idleVillagerText.overflowMode = TextOverflowModes.Overflow;

        }

        private TMP_Text CreateLabel(Transform parent, string name, float x, float y, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(PanelWidth - Padding * 2f, height);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 16;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        private TMP_Text CreateIconRow(Transform parent, string name, float x, float y, float height, Sprite icon, out Image iconImage)
        {
            // Icon
            var iconGO = new GameObject($"{name}Icon");
            iconGO.transform.SetParent(parent, false);
            var iconRT = iconGO.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0, 0);
            iconRT.anchorMax = new Vector2(0, 0);
            iconRT.pivot = new Vector2(0, 0);
            iconRT.anchoredPosition = new Vector2(x, y + (height - IconSize) * 0.5f);
            iconRT.sizeDelta = new Vector2(IconSize, IconSize);
            iconImage = iconGO.AddComponent<Image>();
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            // Text (to the right of icon)
            float textX = x + IconSize + IconTextGap;
            float textW = PanelWidth - Padding * 2f - IconSize - IconTextGap;
            var go = new GameObject($"{name}Text");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(textX, y);
            rt.sizeDelta = new Vector2(textW, height);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 16;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        private void LateUpdate()
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            if (!resourceCanvas.activeSelf)
                resourceCanvas.SetActive(true);

            // Throttle expensive registry scans to every 15 frames (~4/sec)
            if (++resourceUIFrameCounter >= 15)
            {
                resourceUIFrameCounter = 0;
                CountGatheringVillagers(sim);
                UpdateProductionSummary(sim);
                UpdateIdleVillagerCount(sim);
            }

            var resources = sim.ResourceManager.GetPlayerResources(localPlayerId);
            foodText.text = FormatResource(resources.Food, gatherCounts[0]);
            woodText.text = FormatResource(resources.Wood, gatherCounts[1]);
            goldText.text = FormatResource(resources.Gold, gatherCounts[2]);
            stoneText.text = FormatResource(resources.Stone, gatherCounts[3]);
            int pop = sim.GetPopulation(localPlayerId);
            int cap = sim.GetPopulationCap(localPlayerId);
            popText.text = $"{pop}/{cap}";
            int remaining = cap - pop;
            if (cap > 0 && remaining <= 0)
                popText.color = Color.red;
            else if (cap > 0 && remaining <= 1)
                popText.color = new Color(1f, 0.5f, 0f); // orange
            else if (cap > 0 && remaining <= 3)
                popText.color = Color.yellow;
            else
                popText.color = Color.white;

            int totalSeconds = sim.CurrentTick / 30;
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            timerText.text = $"{minutes}:{seconds:D2}";

            UpdateWarning(pop, cap);

            // Show fullscreen button only when not already fullscreen
            if (fullscreenButtonGO != null)
            {
                bool fs = FullscreenManager.Instance != null && FullscreenManager.Instance.IsFullscreen;
                fullscreenButtonGO.SetActive(!fs);
            }
        }

        private void CountGatheringVillagers(GameSimulation sim)
        {
            for (int i = 0; i < 4; i++)
                gatherCounts[i] = 0;

            var allUnits = sim.UnitRegistry.GetAllUnits();
            for (int i = 0; i < allUnits.Count; i++)
            {
                var unit = allUnits[i];
                if (unit.PlayerId != localPlayerId || !unit.IsVillager || unit.CurrentHealth <= 0)
                    continue;

                ResourceType resType;
                switch (unit.State)
                {
                    case UnitState.MovingToGather:
                    case UnitState.Gathering:
                        var node = sim.MapData.GetResourceNode(unit.TargetResourceNodeId);
                        if (node == null) continue;
                        resType = node.Type;
                        break;
                    case UnitState.MovingToDropoff:
                    case UnitState.DroppingOff:
                        resType = unit.CarriedResourceType;
                        break;
                    default:
                        continue;
                }

                int idx = (int)resType;
                if (idx >= 0 && idx < 4)
                    gatherCounts[idx]++;
            }
        }

        private string FormatResource(int amount, int gatherers)
        {
            return gatherers > 0
                ? $"{amount} <color=#BBBBBB>({gatherers})</color>"
                : $"{amount}";
        }

        private void UpdateProductionSummary(GameSimulation sim)
        {
            // Reset accumulators
            for (int i = 0; i < MaxProdRows; i++)
            {
                prodCounts[i] = 0;
                prodProgressSum[i] = 0f;
                prodProgressCount[i] = 0;
            }

            // Iterate all buildings owned by localPlayerId
            var allBuildings = sim.BuildingRegistry.GetAllBuildings();
            for (int b = 0; b < allBuildings.Count; b++)
            {
                var building = allBuildings[b];
                if (building.PlayerId != localPlayerId || !building.IsTraining)
                    continue;

                for (int q = 0; q < building.TrainingQueue.Count; q++)
                {
                    int unitType = building.TrainingQueue[q];
                    // Map unique units to their base type for production display
                    if (unitType == 10) unitType = 2;      // Longbowman → Archer
                    else if (unitType == 11) unitType = 3;  // Gendarme → Horseman
                    else if (unitType == 12) unitType = 1;  // Landsknecht → Spearman
                    if (unitType < 0 || unitType >= MaxProdRows) continue;

                    prodCounts[unitType]++;

                    // Only the first queue item in each building has progress > 0
                    if (q == 0)
                    {
                        int totalTicks = BuildingTrainingSystem.GetTrainTime(sim.Config, unitType);
                        float progress = totalTicks > 0
                            ? 1f - (float)building.TrainingTicksRemaining / totalTicks
                            : 0f;
                        prodProgressSum[unitType] += progress;
                        prodProgressCount[unitType]++;
                    }
                }
            }

            // Populate rows for active unit types
            int activeRows = 0;
            for (int t = 0; t < MaxProdRows; t++)
            {
                if (prodCounts[t] <= 0) continue;
                if (activeRows >= MaxProdRows) break;

                prodRowTexts[activeRows].text = prodCounts[t].ToString();
                prodRowBoxIcons[activeRows].sprite = UnitIcons.Get(t);

                float avgProgress = prodProgressCount[t] > 0
                    ? prodProgressSum[t] / prodProgressCount[t]
                    : 0f;
                SetBarFill(prodRowFills[activeRows], avgProgress);

                prodRowGOs[activeRows].SetActive(true);
                activeRows++;
            }

            // Hide unused rows
            for (int i = activeRows; i < MaxProdRows; i++)
                prodRowGOs[i].SetActive(false);

            if (activeRows > 0)
            {
                float panelH = activeRows * ProdRowHeight + Padding * 2f;
                prodPanelRT.sizeDelta = new Vector2(PanelWidth, panelH);
                float prodY = Margin + PanelHeight + PopGap + PopPanelHeight + ProdGap;
                prodPanelRT.anchoredPosition = new Vector2(Margin, prodY);
                prodPanelGO.SetActive(true);
            }
            else
            {
                prodPanelGO.SetActive(false);
            }
        }

        private void SetBarFill(Image fill, float fraction)
        {
            var rt = fill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1);
            rt.offsetMax = Vector2.zero;
            fill.color = TrainingBarColor;
        }

        private void UpdateIdleVillagerCount(GameSimulation sim)
        {
            int idleCount = 0;
            var allUnits = sim.UnitRegistry.GetAllUnits();
            
            for (int i = 0; i < allUnits.Count; i++)
            {
                var unit = allUnits[i];
                if (unit.PlayerId == localPlayerId && 
                    unit.IsVillager && 
                    unit.State == UnitState.Idle && 
                    unit.CurrentHealth > 0 &&
                    unit.IdleTimer >= Fixed32.One) // Only count as idle after 1 second
                {
                    idleCount++;
                }
            }

            cachedIdleCount = idleCount;
            idleVillagerText.text = idleCount.ToString();
            var idleColor = idleCount > 0 ? Color.red : Color.white;
            idleVillagerLabel.color = idleColor;
            idleVillagerText.color = idleColor;
            if (idleCount == 0)
            {
                lastSelectedIdleVillagerIndex = -1;
            }
        }

        private void BuildTimerPanel(Transform parent)
        {
            const float TimerPanelWidth = 70f;
            const float TimerPanelHeight = 28f;

            var timerGO = new GameObject("TimerPanel");
            timerGO.transform.SetParent(parent, false);
            var timerRT = timerGO.AddComponent<RectTransform>();
            timerRT.anchorMin = new Vector2(0.5f, 1f);
            timerRT.anchorMax = new Vector2(0.5f, 1f);
            timerRT.pivot = new Vector2(0.5f, 1f);
            timerRT.anchoredPosition = new Vector2(0, -10f);
            timerRT.sizeDelta = new Vector2(TimerPanelWidth, TimerPanelHeight);
            var bgImg = timerGO.AddComponent<Image>();
            bgImg.color = new Color(0, 0, 0, 1f);
            var outline = timerGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            outline.effectDistance = new Vector2(2, -2);

            var textGO = new GameObject("TimerText");
            textGO.transform.SetParent(timerGO.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            timerText = textGO.AddComponent<TextMeshProUGUI>();
            timerText.text = "0:00";
            timerText.fontSize = 16;
            timerText.fontStyle = FontStyles.Bold;
            timerText.color = Color.white;
            timerText.alignment = TextAlignmentOptions.Center;
            timerText.raycastTarget = false;
        }

        private void BuildFullscreenButton(Transform parent)
        {
            const float BtnWidth = 130f;
            const float BtnHeight = 20f;

            fullscreenButtonGO = new GameObject("FullscreenButton");
            fullscreenButtonGO.transform.SetParent(parent, false);
            var btnRT = fullscreenButtonGO.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 1f);
            btnRT.anchorMax = new Vector2(0.5f, 1f);
            btnRT.pivot = new Vector2(0.5f, 1f);
            // Timer is at y=-10, height 28, so place below it with a small gap
            btnRT.anchoredPosition = new Vector2(0, -10f - 28f - 4f);
            btnRT.sizeDelta = new Vector2(BtnWidth, BtnHeight);

            var bgImg = fullscreenButtonGO.AddComponent<Image>();
            bgImg.color = new Color(0, 0, 0, 1f);
            var outline = fullscreenButtonGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            outline.effectDistance = new Vector2(2, -2);

            var btn = fullscreenButtonGO.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0f, 0f, 0f);
            colors.highlightedColor = new Color(0.2f, 0.2f, 0.2f);
            colors.pressedColor = new Color(0.1f, 0.1f, 0.1f);
            btn.colors = colors;
            btn.targetGraphic = bgImg;
            btn.onClick.AddListener(() =>
            {
                var fm = FullscreenManager.Instance;
                if (fm != null) fm.EnterFullscreen();
            });

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(fullscreenButtonGO.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "Return to Full Screen";
            tmp.fontSize = 12;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
        }

        private void BuildWarningLabel(Transform parent)
        {
            var warningGO = new GameObject("PopWarning");
            warningGO.transform.SetParent(parent, false);
            var wrt = warningGO.AddComponent<RectTransform>();
            wrt.anchorMin = new Vector2(0.5f, 0.5f);
            wrt.anchorMax = new Vector2(0.5f, 0.5f);
            wrt.pivot = new Vector2(0.5f, 0.5f);
            wrt.anchoredPosition = new Vector2(0, 100f);
            wrt.sizeDelta = new Vector2(400f, 50f);
            warningText = warningGO.AddComponent<TextMeshProUGUI>();
            warningText.fontSize = 24;
            warningText.fontStyle = FontStyles.Bold;
            warningText.color = new Color(1f, 0.3f, 0.3f);
            warningText.alignment = TextAlignmentOptions.Center;
            warningText.overflowMode = TextOverflowModes.Overflow;
            warningText.raycastTarget = false;
            warningCanvasGroup = warningGO.AddComponent<CanvasGroup>();
            warningCanvasGroup.alpha = 0f;
        }

        private void UpdateWarning(int pop, int cap)
        {
            if (warningTimer > 0f)
            {
                warningTimer -= Time.deltaTime;
                warningCanvasGroup.alpha = Mathf.Clamp01(warningTimer / 1f);
                if (warningTimer <= 0f)
                    warningCanvasGroup.alpha = 0f;
            }

            if (cap > 0 && pop >= cap && !warningShownForCurrentCap)
            {
                warningText.text = "Need more houses!";
                warningTimer = 3f;
                warningCanvasGroup.alpha = 1f;
                warningShownForCurrentCap = true;
            }

            if (cap != lastPopCap || pop < cap)
            {
                warningShownForCurrentCap = false;
                lastPopCap = cap;
            }
        }

        private void OnIdleVillagerClicked()
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            var allUnits = sim.UnitRegistry.GetAllUnits();
            var idleVillagers = new List<UnitData>();

            // Collect all idle villagers
            for (int i = 0; i < allUnits.Count; i++)
            {
                var unit = allUnits[i];
                if (unit.PlayerId == localPlayerId && 
                    unit.IsVillager && 
                    unit.State == UnitState.Idle && 
                    unit.CurrentHealth > 0 &&
                    unit.IdleTimer >= Fixed32.One)
                {
                    idleVillagers.Add(unit);
                }
            }

            if (idleVillagers.Count == 0) return;

            // Sort idle villagers by ID for consistent ordering
            idleVillagers.Sort((a, b) => a.Id.CompareTo(b.Id));

            // Cycle to next idle villager
            lastSelectedIdleVillagerIndex = (lastSelectedIdleVillagerIndex + 1) % idleVillagers.Count;
            var selectedIdleVillager = idleVillagers[lastSelectedIdleVillagerIndex];

            // Select the villager using the new SelectUnitById method
            var selectionManager = FindFirstObjectByType<UnitSelectionManager>();
            if (selectionManager != null)
            {
                selectionManager.SelectUnitById(selectedIdleVillager.Id);
            }

            // Center camera on the villager
            var cam = FindFirstObjectByType<RTSCameraController>();
            if (cam != null)
            {
                Vector3 pos = selectedIdleVillager.SimPosition.ToVector3();
                cam.PivotPosition = new Vector3(pos.x, 0f, pos.z);
            }
        }
    }
}
