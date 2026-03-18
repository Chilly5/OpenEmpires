using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenEmpires
{
    public class MatchResultUI : MonoBehaviour
    {
        private static MatchResultUI instance;

        private GameObject root;
        private TMP_Text resultText;
        private bool shown;

        private static readonly Color VictoryGold = new Color(1f, 0.84f, 0f);
        private static readonly Color DefeatRed = new Color(0.85f, 0.15f, 0.15f);
        private static readonly Color DrawGrey = new Color(0.6f, 0.6f, 0.6f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (instance != null) return;

            var go = new GameObject("MatchResultUI");
            instance = go.AddComponent<MatchResultUI>();
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
            SceneManager.sceneLoaded += OnSceneLoaded;
            BuildUI();
            root.SetActive(false);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            shown = false;
            if (root != null) root.SetActive(false);
            UnitSelectionManager.SetSettingsMenuOpen(false);
        }

        private void BuildUI()
        {
            // Canvas
            var canvasGO = new GameObject("MatchResultCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            root = canvasGO;

            // Full-screen dark overlay
            var overlayGO = new GameObject("Overlay");
            overlayGO.transform.SetParent(canvasGO.transform, false);
            var overlayRT = overlayGO.AddComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;
            var overlayImg = overlayGO.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.7f);

            // Centered panel
            float panelW = 400f;
            float panelH = 250f;
            var panelGO = new GameObject("Panel");
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelRT = panelGO.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(panelW, panelH);
            var panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

            // Result text
            var textGO = new GameObject("ResultText");
            textGO.transform.SetParent(panelGO.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0.5f, 0.5f);
            textRT.anchorMax = new Vector2(0.5f, 0.5f);
            textRT.pivot = new Vector2(0.5f, 0.5f);
            textRT.anchoredPosition = new Vector2(0, 30f);
            textRT.sizeDelta = new Vector2(380f, 60f);
            resultText = textGO.AddComponent<TextMeshProUGUI>();
            resultText.fontSize = 48;
            resultText.fontStyle = FontStyles.Bold;
            resultText.alignment = TextAlignmentOptions.Center;
            resultText.color = Color.white;
            resultText.raycastTarget = false;

            // Queue Again button
            float btnW = 180f;
            float btnH = 40f;
            var btnGO = new GameObject("QueueAgainButton");
            btnGO.transform.SetParent(panelGO.transform, false);
            var btnRT = btnGO.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 0.5f);
            btnRT.anchorMax = new Vector2(0.5f, 0.5f);
            btnRT.pivot = new Vector2(0.5f, 0.5f);
            btnRT.anchoredPosition = new Vector2(0, -50f);
            btnRT.sizeDelta = new Vector2(btnW, btnH);

            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(0.25f, 0.25f, 0.25f);

            var btn = btnGO.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.25f, 0.25f, 0.25f);
            colors.highlightedColor = new Color(0.35f, 0.35f, 0.35f);
            colors.pressedColor = new Color(0.15f, 0.15f, 0.15f);
            btn.colors = colors;
            btn.onClick.AddListener(OnQueueAgain);

            var btnTextGO = new GameObject("Text");
            btnTextGO.transform.SetParent(btnGO.transform, false);
            var btnTextRT = btnTextGO.AddComponent<RectTransform>();
            btnTextRT.anchorMin = Vector2.zero;
            btnTextRT.anchorMax = Vector2.one;
            btnTextRT.offsetMin = Vector2.zero;
            btnTextRT.offsetMax = Vector2.zero;
            var btnTmp = btnTextGO.AddComponent<TextMeshProUGUI>();
            btnTmp.text = "Play Again";
            btnTmp.fontSize = 16;
            btnTmp.alignment = TextAlignmentOptions.Center;
            btnTmp.color = Color.white;
        }

        private void Update()
        {
            if (shown) return;

            if (GameBootstrapper.Instance == null) return;
            var sim = GameBootstrapper.Instance.Simulation;
            if (sim == null || !sim.IsMatchOver) return;

            shown = true;

            // Determine local player's team
            int localPlayerId = 0;
            var network = GameBootstrapper.Instance.Network;
            if (network != null && network.IsMultiplayer)
                localPlayerId = network.LocalPlayerId;

            int localTeam = localPlayerId;
            if (sim.PlayerTeamIds != null && localPlayerId < sim.PlayerTeamIds.Length)
                localTeam = sim.PlayerTeamIds[localPlayerId];

            int winTeam = sim.WinningTeamId;

            if (winTeam == -1)
            {
                resultText.text = "Draw";
                resultText.color = DrawGrey;
            }
            else if (winTeam == localTeam)
            {
                resultText.text = "Victory";
                resultText.color = VictoryGold;
            }
            else
            {
                resultText.text = "Defeat";
                resultText.color = DefeatRed;
            }

            root.SetActive(true);
            UnitSelectionManager.SetSettingsMenuOpen(true);
        }

        private void OnQueueAgain()
        {
            shown = false;
            root.SetActive(false);
            UnitSelectionManager.SetSettingsMenuOpen(false);
            MusicManager.Instance?.Stop();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
