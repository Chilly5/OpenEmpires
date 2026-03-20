using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenEmpires
{
    /// <summary>
    /// Chat panel positioned to the left of the minimap at the bottom-right of the screen.
    /// Press Enter to type, Tab to toggle All/Team channel, Escape to cancel.
    /// </summary>
    public class ChatUI : MonoBehaviour
    {
        private const float ReferenceWidth = 1280f;
        private const float ReferenceHeight = 720f;
        private const float PanelWidth = 280f;
        private const float PanelHeight = 160f;
        private const float InputBarHeight = 28f;
        private const float ChannelLabelWidth = 52f;
        private const float Margin = 10f;
        private const float MinimapSize = 200f; // matches MinimapUI

        private ChatChannel currentChannel = ChatChannel.All;

        private TMP_Text messagesText;
        private TMP_InputField inputField;
        private TMP_Text channelLabel;
        private TMP_Text placeholderText;
        private ScrollRect scrollRect;
        private Image borderImage;
        private Image panelImage;

        private static readonly Color BorderUnfocused    = new Color(0.20f, 0.20f, 0.20f, 0.00f);
        private static readonly Color BorderFocused      = new Color(0.40f, 0.40f, 0.40f, 0.85f);
        private static readonly Color PanelUnfocused     = new Color(0f,    0f,    0f,    0.00f);
        private static readonly Color PanelFocused       = new Color(0f,    0f,    0f,    0.80f);
        private static readonly Color InputBarFocused    = new Color(0f,    0f,    0f,    0.70f);
        private static readonly Color InputFieldFocused  = new Color(0.12f, 0.12f, 0.12f, 0.90f);
        private const float FocusTransitionSpeed = 8f;

        private bool justSubmitted;
        private bool pendingFocus;
        private GameObject canvasGO;
        private GameObject inputBarGO;
        private bool gameStarted;
        private Coroutine waitForGameStartCoroutine;

        public int LocalPlayerId { get; set; }
        public string LocalPlayerName { get; set; } = "Player";

        private void Awake()
        {
            BuildUI();
            canvasGO.SetActive(false);
        }

        private void Start()
        {
            ChatManager.OnMessageAdded += OnMessageReceived;
            HookNetworkEvents();
            waitForGameStartCoroutine = StartCoroutine(WaitForGameStart());
        }

        private System.Collections.IEnumerator WaitForGameStart()
        {
            while (GameBootstrapper.Instance?.Simulation == null)
                yield return null;

            var sim = GameBootstrapper.Instance.Simulation;
            sim.OnPlayerSurrendered += OnPlayerSurrendered;
            sim.OnSurrenderVoteUpdated += OnSurrenderVoteUpdated;
            sim.OnPlayerAgedUp += OnPlayerAgedUp;

            gameStarted = true;
            canvasGO.SetActive(true);
            ChatManager.AddSystemMessage("Welcome! Press Enter to chat.");
        }

        private void OnDestroy()
        {
            if (waitForGameStartCoroutine != null)
                StopCoroutine(waitForGameStartCoroutine);
            ChatManager.OnMessageAdded -= OnMessageReceived;
            UnhookNetworkEvents();
            UnitSelectionManager.SetChatFocused(false);

            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim != null)
            {
                sim.OnPlayerSurrendered -= OnPlayerSurrendered;
                sim.OnSurrenderVoteUpdated -= OnSurrenderVoteUpdated;
                sim.OnPlayerAgedUp -= OnPlayerAgedUp;
            }
        }

        private void HookNetworkEvents()
        {
            var mm = MatchmakingManager.Instance;
            if (mm == null) return;
            mm.OnPlayerDisconnected += OnPlayerDisconnected;
            mm.OnMatchStarting += OnMatchStarting;
            mm.OnChatReceived += OnNetworkChatReceived;
        }

        private void UnhookNetworkEvents()
        {
            var mm = MatchmakingManager.Instance;
            if (mm == null) return;
            mm.OnPlayerDisconnected -= OnPlayerDisconnected;
            mm.OnMatchStarting -= OnMatchStarting;
            mm.OnChatReceived -= OnNetworkChatReceived;
        }

        private void OnPlayerDisconnected(string playerId)
        {
            // Disconnect chat message is now posted by GameBootstrapper with resolved name
        }

        private void OnMatchStarting() =>
            ChatManager.AddSystemMessage("Match is starting!");

        private void OnNetworkChatReceived(ChatServerMessage msg)
        {
            Color color = msg.from_player_id < GameSetup.PlayerColors.Length
                ? GameSetup.PlayerColors[msg.from_player_id]
                : Color.white;
            ChatChannel channel = msg.channel == "Team" ? ChatChannel.Team : ChatChannel.All;
            ChatManager.AddMessage(new ChatMessage
            {
                SenderName = msg.from_username,
                SenderColor = color,
                Text = msg.text,
                Channel = channel,
                IsSystem = false,
                SenderPlayerId = msg.from_player_id
            });
        }

        private void OnPlayerAgedUp(int playerId, int newAge)
        {
            string name = ResolvePlayerName(playerId);
            string ageRoman = LandmarkDefinitions.AgeToRoman(newAge);
            ChatManager.AddSystemMessage($"{name} has advanced to Age {ageRoman}!");
        }

        private void OnPlayerSurrendered(int playerId)
        {
            string name = ResolvePlayerName(playerId);
            ChatManager.AddSystemMessage($"{name} has surrendered.");
        }

        private void OnSurrenderVoteUpdated(int teamId, GameSimulation.SurrenderVoteData vote)
        {
            if (vote == null)
            {
                ChatManager.AddSystemMessage("Surrender vote expired.");
                return;
            }

            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            int teammateCount = 0;
            for (int pid = 0; pid < sim.PlayerCount; pid++)
            {
                int pidTeam = (pid < sim.PlayerTeamIds.Length) ? sim.PlayerTeamIds[pid] : pid;
                if (pidTeam == teamId) teammateCount++;
            }
            int needed = (teammateCount / 2) + 1;

            if (vote.YesVotes.Count >= needed)
            {
                ChatManager.AddSystemMessage("Surrender vote passed!");
                return;
            }

            string initiatorName = ResolvePlayerName(vote.InitiatorPlayerId);
            if (vote.YesVotes.Count == 1 && vote.NoVotes.Count == 0)
            {
                ChatManager.AddSystemMessage($"{initiatorName} started a surrender vote ({vote.YesVotes.Count}/{needed} needed). Type /yes or /no to vote.");
            }
            else
            {
                ChatManager.AddSystemMessage($"Surrender vote: {vote.YesVotes.Count}/{needed} needed.");
            }
        }

        private static string ResolvePlayerName(int gamePlayerId)
        {
            var mm = MatchmakingManager.Instance;
            if (mm?.Teams != null)
            {
                foreach (var team in mm.Teams)
                {
                    if (team.players == null) continue;
                    foreach (var tp in team.players)
                    {
                        if (tp.game_player_id == gamePlayerId)
                            return tp.username ?? $"Player {gamePlayerId + 1}";
                    }
                }
            }
            return $"Player {gamePlayerId + 1}";
        }

        private void Update()
        {
            if (!gameStarted) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            // Activate input field the frame after the input bar becomes active
            if (pendingFocus)
            {
                pendingFocus = false;
                inputField.ActivateInputField();
            }
            else
            {
                // Auto-hide input bar if focus was lost externally (e.g. clicking elsewhere)
                if (inputBarGO.activeSelf && !inputField.isFocused && !justSubmitted)
                    inputBarGO.SetActive(false);
            }

            if (!inputField.isFocused && !justSubmitted)
            {
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                {
                    inputBarGO.SetActive(true);
                    pendingFocus = true;
                }
            }

            if (inputField.isFocused && kb.escapeKey.wasPressedThisFrame)
            {
                inputField.text = string.Empty;
                inputField.DeactivateInputField();
                inputBarGO.SetActive(false);
            }

            if (inputField.isFocused && kb.tabKey.wasPressedThisFrame)
                ToggleChannel();

            UnitSelectionManager.SetChatFocused(inputField.isFocused);
            justSubmitted = false;

            // Smooth fade panel background in/out with focus
            float t = Time.unscaledDeltaTime * FocusTransitionSpeed;
            bool focused = inputField.isFocused;
            borderImage.color = Color.Lerp(borderImage.color, focused ? BorderFocused : BorderUnfocused, t);
            panelImage.color  = Color.Lerp(panelImage.color,  focused ? PanelFocused  : PanelUnfocused,  t);

            // Fade placeholder out when focused so it doesn't crowd the cursor
            Color ph = placeholderText.color;
            ph.a = Mathf.Lerp(ph.a, focused ? 0f : 0.85f, t);
            placeholderText.color = ph;
        }

        private void OnInputValueChanged(string value) { }

        private void OnInputSubmit(string value)
        {
            justSubmitted = true;

            string trimmed = value.Trim();

            // Intercept /yes and /no surrender vote commands
            if (!string.IsNullOrEmpty(trimmed))
            {
                string lower = trimmed.ToLower();
                if (lower == "/yes" || lower == "/no")
                {
                    bool voteYes = lower == "/yes";
                    var sim = GameBootstrapper.Instance?.Simulation;
                    if (sim != null && !sim.IsMatchOver)
                    {
                        sim.CommandBuffer.EnqueueCommand(new SurrenderVoteCommand(LocalPlayerId, voteYes));
                    }
                    inputField.text = string.Empty;
                    inputField.DeactivateInputField();
                    inputBarGO.SetActive(false);
                    return;
                }
            }

            if (!string.IsNullOrEmpty(trimmed))
            {
                var mm = MatchmakingManager.Instance;
                if (mm != null && mm.IsInMatch)
                {
                    // Multiplayer: send to server; it echoes back to all recipients including sender
                    mm.SendChat(currentChannel.ToString(), trimmed);
                }
                else
                {
                    // Single player: add directly to local chat
                    string name = (mm != null && !string.IsNullOrEmpty(mm.Username))
                        ? mm.Username
                        : LocalPlayerName;
                    Color color = LocalPlayerId < GameSetup.PlayerColors.Length
                        ? GameSetup.PlayerColors[LocalPlayerId]
                        : Color.white;
                    ChatManager.AddMessage(new ChatMessage
                    {
                        SenderName = name,
                        SenderColor = color,
                        Text = trimmed,
                        Channel = currentChannel,
                        IsSystem = false,
                        SenderPlayerId = LocalPlayerId
                    });
                }
            }

            inputField.text = string.Empty;
            inputField.DeactivateInputField();
            inputBarGO.SetActive(false);
        }

        private void ToggleChannel()
        {
            currentChannel = currentChannel == ChatChannel.All ? ChatChannel.Team : ChatChannel.All;
            channelLabel.text = currentChannel == ChatChannel.All ? "[ALL]" : "[TEAM]";
        }

        public void SetLocalPlayer(int playerId, string playerName)
        {
            LocalPlayerId = playerId;
            LocalPlayerName = playerName;
        }

        private void OnMessageReceived(ChatMessage msg)
        {
            RebuildMessages();
            Canvas.ForceUpdateCanvases();
            scrollRect.normalizedPosition = new Vector2(0, 0);

            if (SFXManager.Instance != null)
            {
                bool isSurrenderMessage = msg.IsSystem && msg.Text.Contains("surrender", System.StringComparison.OrdinalIgnoreCase);
                SFXManager.Instance.PlayUI(
                    isSurrenderMessage ? SFXType.SurrenderVote : SFXType.ChatMessage,
                    0.6f);
            }
        }

        private void RebuildMessages()
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            var sb = new StringBuilder();
            var msgs = ChatManager.Messages;
            bool firstLine = true;
            for (int i = 0; i < msgs.Count; i++)
            {
                var msg = msgs[i];

                // Filter team messages: only show if sender is an ally (or self)
                if (!msg.IsSystem && msg.Channel == ChatChannel.Team &&
                    !TeamHelper.AreAllies(sim?.PlayerTeamIds, msg.SenderPlayerId, LocalPlayerId))
                    continue;

                if (!firstLine) sb.Append('\n');
                firstLine = false;

                string hex = ColorUtility.ToHtmlStringRGB(msg.SenderColor);
                if (msg.IsSystem)
                {
                    sb.Append($"<color=#{hex}><i>{Escape(msg.Text)}</i></color>");
                }
                else
                {
                    string channelTag = msg.Channel == ChatChannel.Team
                        ? "<color=#88aaff>[Team] </color>"
                        : "<color=#aaaaaa>[All] </color>";
                    sb.Append($"{channelTag}<color=#{hex}><b>{Escape(msg.SenderName)}:</b></color> {Escape(msg.Text)}");
                }
            }
            messagesText.text = sb.ToString();
        }

        private static string Escape(string s) =>
            s.Replace("<", "\u003C").Replace(">", "\u003E");

        // ─── UI Construction ────────────────────────────────────────────────

        private void BuildUI()
        {
            canvasGO = new GameObject("ChatCanvas");
            canvasGO.transform.SetParent(transform);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Main panel anchored to bottom-right, above the minimap
            float rightOffset = Margin; // align right edge with minimap
            float bottomOffset = Margin + MinimapSize + 4f; // above minimap with 4px gap

            // Border sits 2px behind and around the panel (rendered first = drawn below)
            var borderGO = new GameObject("ChatBorder");
            borderGO.transform.SetParent(canvasGO.transform, false);
            var borderRT = borderGO.AddComponent<RectTransform>();
            borderRT.anchorMin = new Vector2(1, 0);
            borderRT.anchorMax = new Vector2(1, 0);
            borderRT.pivot = new Vector2(1, 0);
            borderRT.anchoredPosition = new Vector2(-rightOffset + 2, bottomOffset - 2);
            borderRT.sizeDelta = new Vector2(PanelWidth + 4, PanelHeight + 4);
            borderImage = borderGO.AddComponent<Image>();
            borderImage.color = BorderUnfocused;
            borderImage.raycastTarget = false;

            var panelGO = new GameObject("ChatPanel");
            panelGO.transform.SetParent(canvasGO.transform, false);
            var panelRT = panelGO.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(1, 0);
            panelRT.anchorMax = new Vector2(1, 0);
            panelRT.pivot = new Vector2(1, 0);
            panelRT.anchoredPosition = new Vector2(-rightOffset, bottomOffset);
            panelRT.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panelImage = panelGO.AddComponent<Image>();
            panelImage.color = PanelUnfocused; // nearly invisible until focused
            panelImage.raycastTarget = false;

            BuildScrollArea(panelGO.transform);
            BuildInputBar(panelGO.transform);
        }

        private void BuildScrollArea(Transform parent)
        {
            var scrollGO = new GameObject("ScrollView");
            scrollGO.transform.SetParent(parent, false);
            var scrollRT = scrollGO.AddComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = new Vector2(0, InputBarHeight);
            scrollRT.offsetMax = Vector2.zero;
            var scrollBg = scrollGO.AddComponent<Image>();
            scrollBg.color = Color.clear;
            scrollBg.raycastTarget = false;

            scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 20f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

            // Viewport
            var viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var viewportRT = viewportGO.AddComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.sizeDelta = Vector2.zero;
            viewportGO.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportRT;

            // Content = single TMP Text with ContentSizeFitter
            // Anchors stretch horizontally so word-wrap knows its width
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRT = contentGO.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0, 1);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = Vector2.zero;
            scrollRect.content = contentRT;

            messagesText = contentGO.AddComponent<TextMeshProUGUI>();
            messagesText.fontSize = 11;
            messagesText.color = Color.white;
            messagesText.alignment = TextAlignmentOptions.TopLeft;
            messagesText.overflowMode = TextOverflowModes.Overflow;
            messagesText.textWrappingMode = TextWrappingModes.Normal;
            messagesText.richText = true;
            messagesText.raycastTarget = false;
            messagesText.margin = new Vector4(4, 4, 4, 4);
            messagesText.text = string.Empty;

            var csf = contentGO.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void BuildInputBar(Transform parent)
        {
            inputBarGO = new GameObject("InputBar");
            inputBarGO.transform.SetParent(parent, false);
            var inputBarRT = inputBarGO.AddComponent<RectTransform>();
            inputBarRT.anchorMin = new Vector2(0, 0);
            inputBarRT.anchorMax = new Vector2(1, 0);
            inputBarRT.pivot = new Vector2(0, 0);
            inputBarRT.anchoredPosition = Vector2.zero;
            inputBarRT.sizeDelta = new Vector2(0, InputBarHeight);
            var inputBarImage = inputBarGO.AddComponent<Image>();
            inputBarImage.color = InputBarFocused;
            inputBarImage.raycastTarget = false;

            // Channel label ("[ALL]" / "[TEAM]")
            var labelGO = new GameObject("ChannelLabel");
            labelGO.transform.SetParent(inputBarGO.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0, 0);
            labelRT.anchorMax = new Vector2(0, 1);
            labelRT.pivot = new Vector2(0, 0.5f);
            labelRT.anchoredPosition = new Vector2(4, 0);
            labelRT.sizeDelta = new Vector2(ChannelLabelWidth, 0);
            channelLabel = labelGO.AddComponent<TextMeshProUGUI>();
            channelLabel.fontSize = 11;
            channelLabel.color = new Color(0.9f, 0.85f, 0.4f);
            channelLabel.alignment = TextAlignmentOptions.Left;
            channelLabel.text = "[ALL]";
            channelLabel.raycastTarget = false;

            // Input field background
            var inputGO = new GameObject("InputField");
            inputGO.transform.SetParent(inputBarGO.transform, false);
            var inputRT = inputGO.AddComponent<RectTransform>();
            inputRT.anchorMin = new Vector2(0, 0);
            inputRT.anchorMax = new Vector2(1, 1);
            inputRT.offsetMin = new Vector2(ChannelLabelWidth + 6, 3);
            inputRT.offsetMax = new Vector2(-4, -3);
            var inputFieldImage = inputGO.AddComponent<Image>();
            inputFieldImage.color = InputFieldFocused;

            inputField = inputGO.AddComponent<TMP_InputField>();
            inputField.targetGraphic = inputFieldImage;
            inputField.lineType = TMP_InputField.LineType.SingleLine;
            inputField.characterLimit = 200;
            // Disable navigation so Tab reaches text input instead of cycling UI elements
            inputField.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };

            // Text viewport (clips text to input box bounds)
            var textAreaGO = new GameObject("TextArea");
            textAreaGO.transform.SetParent(inputGO.transform, false);
            var textAreaRT = textAreaGO.AddComponent<RectTransform>();
            textAreaRT.anchorMin = Vector2.zero;
            textAreaRT.anchorMax = Vector2.one;
            textAreaRT.offsetMin = new Vector2(5, 2);
            textAreaRT.offsetMax = new Vector2(-5, -2);
            textAreaGO.AddComponent<RectMask2D>();
            inputField.textViewport = textAreaRT;

            // Placeholder text
            var placeholderGO = new GameObject("Placeholder");
            placeholderGO.transform.SetParent(textAreaGO.transform, false);
            var placeholderRT = placeholderGO.AddComponent<RectTransform>();
            placeholderRT.anchorMin = Vector2.zero;
            placeholderRT.anchorMax = Vector2.one;
            placeholderRT.sizeDelta = Vector2.zero;
            placeholderText = placeholderGO.AddComponent<TextMeshProUGUI>();
            placeholderText.fontSize = 11;
            placeholderText.color = new Color(0.80f, 0.80f, 0.80f, 0.85f);
            placeholderText.fontStyle = FontStyles.Italic;
            placeholderText.text = "Enter to chat...";
            placeholderText.textWrappingMode = TextWrappingModes.NoWrap;
            placeholderText.raycastTarget = false;
            inputField.placeholder = placeholderText;

            // Input text
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(textAreaGO.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;
            var textTMP = textGO.AddComponent<TextMeshProUGUI>();
            textTMP.fontSize = 11;
            textTMP.color = Color.white;
            textTMP.textWrappingMode = TextWrappingModes.NoWrap;
            textTMP.raycastTarget = false;
            inputField.textComponent = textTMP;

            inputField.onValueChanged.AddListener(OnInputValueChanged);
            inputField.onSubmit.AddListener(OnInputSubmit);

            inputBarGO.SetActive(false);
        }
    }
}
