using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenEmpires
{
    public class NetworkDiagnosticsUI : MonoBehaviour
    {
        public static NetworkDiagnosticsUI Instance { get; private set; }

        [SerializeField] private Key toggleKey = Key.F3;
        [SerializeField] private bool showOnStart = false;

        private const int LineCount = 17;
        private const float LineHeight = 18f;
        private const float PanelWidth = 260f;

        private Canvas diagCanvas;
        private RectTransform panelRT;
        private RectTransform tcTrackerPanelRT;
        private TextMeshProUGUI headerLabel;
        private TextMeshProUGUI[] lineLabels;
        private Button resetButton;
        private bool isVisible;

        public bool IsVisible
        {
            get => isVisible;
            set
            {
                isVisible = value;
                if (diagCanvas != null)
                    diagCanvas.gameObject.SetActive(isVisible);
            }
        }

        private void Awake()
        {
            Instance = this;
            CreateUI();
        }

        private void Start()
        {
            IsVisible = showOnStart;
        }

        private void CreateUI()
        {
            // Canvas
            var canvasGO = new GameObject("NetworkDiagCanvas");
            canvasGO.transform.SetParent(transform, false);
            diagCanvas = canvasGO.AddComponent<Canvas>();
            diagCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            diagCanvas.sortingOrder = 200;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            // Panel background
            float panelHeight = LineHeight * LineCount + 60f;
            var panelGO = new GameObject("Panel");
            panelGO.transform.SetParent(canvasGO.transform, false);
            panelRT = panelGO.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0f, 1f);
            panelRT.anchorMax = new Vector2(0f, 1f);
            panelRT.pivot = new Vector2(0f, 1f);
            panelRT.anchoredPosition = new Vector2(10f, -10f);
            panelRT.sizeDelta = new Vector2(PanelWidth, panelHeight);
            var panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.8f);

            float currentY = -10f;

            // Header
            headerLabel = CreateLabel(panelGO.transform, "Header", ref currentY);
            headerLabel.fontSize = 14f;
            headerLabel.fontStyle = FontStyles.Bold;
            headerLabel.color = new Color(0.9f, 0.9f, 0.2f);
            headerLabel.text = "NETWORK DIAGNOSTICS (F3)";
            currentY -= 5f; // extra gap after header

            // Line labels
            lineLabels = new TextMeshProUGUI[LineCount];
            for (int i = 0; i < LineCount; i++)
            {
                lineLabels[i] = CreateLabel(panelGO.transform, $"Line_{i}", ref currentY);
            }

            // Reset button
            currentY -= 5f;
            var btnGO = new GameObject("ResetBtn");
            btnGO.transform.SetParent(panelGO.transform, false);
            var btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0f, 1f);
            btnRT.anchorMax = new Vector2(0f, 1f);
            btnRT.pivot = new Vector2(0f, 1f);
            btnRT.anchoredPosition = new Vector2(10f, currentY);
            btnRT.sizeDelta = new Vector2(80f, 20f);
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(0.3f, 0.3f, 0.3f);
            resetButton = btnGO.AddComponent<Button>();
            resetButton.targetGraphic = btnImg;
            resetButton.onClick.AddListener(OnResetClicked);

            var btnTextGO = new GameObject("Text");
            btnTextGO.transform.SetParent(btnGO.transform, false);
            var btnTextRT = btnTextGO.AddComponent<RectTransform>();
            btnTextRT.anchorMin = Vector2.zero;
            btnTextRT.anchorMax = Vector2.one;
            btnTextRT.offsetMin = Vector2.zero;
            btnTextRT.offsetMax = Vector2.zero;
            var btnText = btnTextGO.AddComponent<TextMeshProUGUI>();
            btnText.text = "Reset";
            btnText.fontSize = 12f;
            btnText.color = Color.white;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.raycastTarget = false;
        }

        private TextMeshProUGUI CreateLabel(Transform parent, string name, ref float currentY)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(10f, currentY);
            rt.sizeDelta = new Vector2(-20f, LineHeight);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 12f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.raycastTarget = false;
            currentY -= LineHeight;
            return tmp;
        }

        private void OnResetClicked()
        {
            var diag = NetworkDiagnostics.Instance;
            if (diag != null)
                diag.ResetStats();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                IsVisible = !IsVisible;
            }

            if (!isVisible) return;

            // Find TC tracker panel once
            if (tcTrackerPanelRT == null)
            {
                var go = GameObject.Find("TCTrackerPanel");
                if (go != null)
                    tcTrackerPanelRT = go.GetComponent<RectTransform>();
            }

            // Position below TC tracker
            if (tcTrackerPanelRT != null)
            {
                float tcBottom = tcTrackerPanelRT.anchoredPosition.y - tcTrackerPanelRT.sizeDelta.y;
                panelRT.anchoredPosition = new Vector2(10f, tcBottom - 5f);
            }

            var diag = NetworkDiagnostics.Instance;
            if (diag == null) return;

            int idx = 0;

            // FPS & Frame Time
            float fps = diag.FPS;
            Color fpsColor = fps >= 55 ? Color.green : fps >= 30 ? Color.yellow : Color.red;
            SetLine(idx++, $"FPS: {fps:F0}  Frame: {diag.FrameTime * 1000f:F1}ms", fpsColor);

            // Tick Rate
            float tickRate = diag.ActualTickRate;
            Color tickColor = tickRate >= diag.ExpectedTickRate - 1 ? Color.green : tickRate >= diag.ExpectedTickRate - 5 ? Color.yellow : Color.red;
            SetLine(idx++, $"Tick: {tickRate:F0}/{diag.ExpectedTickRate} Hz", tickColor);

            // Accumulator
            float accum = diag.Accumulator;
            float maxAccum = diag.MaxAccumulator;
            Color accumColor = accum < 0.1f ? Color.green : accum < 0.2f ? Color.yellow : Color.red;
            SetLine(idx++, $"Accum: {accum * 1000f:F0}ms (max: {maxAccum * 1000f:F0}ms)", accumColor);

            // Accumulator Overflows
            int overflows = diag.AccumulatorOverflowsThisSecond;
            Color overflowColor = overflows == 0 ? Color.green : Color.red;
            SetLine(idx++, $"Overflows: {overflows}/sec (total: {diag.AccumulatorOverflows})", overflowColor);

            // Stalls
            int stalls = diag.StallsThisSecond;
            Color stallColor = stalls == 0 ? Color.green : stalls < 5 ? Color.yellow : Color.red;
            SetLine(idx++, $"Stalls: {stalls}/sec", stallColor);

            // Wait Time
            float waitTime = diag.WaitTimeThisSecond;
            Color waitColor = waitTime < 0.05f ? Color.green : waitTime < 0.2f ? Color.yellow : Color.red;
            SetLine(idx++, $"Wait: {waitTime * 1000f:F0}ms/sec", waitColor);

            // Commands
            SetLine(idx++, $"Cmds Sent: {diag.CommandsSentThisSecond}/sec", Color.white);
            SetLine(idx++, $"Cmds Recv: {diag.CommandsReceivedThisSecond}/sec", Color.white);
            SetLine(idx++, $"Noop: {diag.NoopPacketsThisSecond}/sec", Color.white);

            // Late Packets
            int late = diag.LatePacketsDropped;
            Color lateColor = late == 0 ? Color.green : Color.red;
            SetLine(idx++, $"Late pkts: {late}", lateColor);

            // Desync Warnings (conditional)
            int desync = diag.DesyncWarnings;
            if (desync > 0)
                SetLine(idx++, $"DESYNC WARNINGS: {desync}", Color.red);

            // Timeouts (conditional)
            int timeouts = diag.Timeouts;
            if (timeouts > 0)
                SetLine(idx++, $"Timeouts: {timeouts}", Color.red);

            // Ping (conditional)
            if (diag.AveragePing > 0)
            {
                float ping = diag.CurrentPing;
                Color pingColor = ping < 50 ? Color.green : ping < 100 ? Color.yellow : Color.red;
                SetLine(idx++, $"Ping: {ping:F0}ms (avg: {diag.AveragePing:F0}ms)", pingColor);
            }

            // Hide remaining lines
            for (int i = idx; i < LineCount; i++)
            {
                if (lineLabels[i].gameObject.activeSelf)
                    lineLabels[i].gameObject.SetActive(false);
            }
        }

        private void SetLine(int index, string text, Color color)
        {
            if (index >= LineCount) return;
            var label = lineLabels[index];
            if (!label.gameObject.activeSelf)
                label.gameObject.SetActive(true);
            label.text = text;
            label.color = color;
        }
    }
}
