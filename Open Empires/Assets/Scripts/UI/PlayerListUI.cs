using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenEmpires
{
    public class PlayerListUI : MonoBehaviour
    {
        private const float PanelWidthBase = 155f;
        private const float PingColWidth = 44f;
        private const float PingColGap = 4f;
        private const float HealthBarWidth = 50f;
        private const float HealthBarHeight = 10f;
        private const float HealthBarGap = 4f;
        private const float AgeColWidth = 24f;
        private const float AgeColGap = 4f;
        private const float RowHeight = 22f;
        private const float RowGap = 3f;
        private const float Padding = 8f;
        private const float Margin = 10f;
        private const float SwatchSize = 13f;
        private const float FlagWidth = 20f;
        private const float FlagHeight = 13f;
        private const float FlagGap = 4f;
        private const float SwatchTextGap = 6f;
        private const float ReferenceWidth = 1280f;
        private const float ReferenceHeight = 720f;
        private const float HeaderRowHeight = 16f;
        private const float HeaderGap = 2f;
        private const float TeamSectionGap = 6f;
        private const float HintHeight = 18f;
        private const float HintGap = 4f;

        private static readonly Color HealthGreen = new Color(0.2f, 0.8f, 0.2f);
        private static readonly Color HealthRed = new Color(0.8f, 0.2f, 0.2f);
        private static readonly Color DestroyedColor = new Color(0.6f, 0.15f, 0.15f);

        private RectTransform panelRT;
        private bool initialized = false;
        private TextMeshProUGUI[] pingLabels;
        private TextMeshProUGUI[] nameLabels;
        private Image[] healthBarBgs;
        private Image[] healthBarFills;
        private TextMeshProUGUI[] destroyedMarkers;
        private TextMeshProUGUI[] ageLabels;
        private TextMeshProUGUI objectiveHint;
        private int localPlayerId;
        private bool showPing;
        private NetworkManager network;
        private int playerCount;

        private void Awake()
        {
            var canvasGO = new GameObject("PlayerListCanvas");
            canvasGO.transform.SetParent(transform);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            var panelGO = new GameObject("PlayerListPanel");
            panelGO.transform.SetParent(canvasGO.transform, false);
            panelRT = panelGO.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0, 1);
            panelRT.anchorMax = new Vector2(0, 1);
            panelRT.pivot = new Vector2(0, 1);
            panelRT.anchoredPosition = new Vector2(Margin, -Margin);
            panelRT.sizeDelta = new Vector2(PanelWidthBase, 0);
            var panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 1f);
            var outline = panelGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            outline.effectDistance = new Vector2(2, -2);
            panelGO.SetActive(false);

            // Objective hint label above the panel
            var hintGO = new GameObject("ObjectiveHint");
            hintGO.transform.SetParent(canvasGO.transform, false);
            var hintRT = hintGO.AddComponent<RectTransform>();
            hintRT.anchorMin = new Vector2(0, 1);
            hintRT.anchorMax = new Vector2(0, 1);
            hintRT.pivot = new Vector2(0, 1);
            hintRT.anchoredPosition = new Vector2(Margin, 0);
            hintRT.sizeDelta = new Vector2(300f, HintHeight);
            objectiveHint = hintGO.AddComponent<TextMeshProUGUI>();
            objectiveHint.text = "Destroy all enemy Headquarters to win.";
            objectiveHint.fontSize = 13;
            objectiveHint.color = new Color(1f, 1f, 1f, 0.9f);
            objectiveHint.alignment = TextAlignmentOptions.TopLeft;
            objectiveHint.raycastTarget = false;
            hintGO.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!initialized)
            {
                var bootstrapper = GameBootstrapper.Instance;
                if (bootstrapper == null || bootstrapper.Simulation == null) return;
                network = bootstrapper.Network;
                localPlayerId = network?.LocalPlayerId ?? 0;
                showPing = network?.IsMultiplayer ?? false;
                playerCount = bootstrapper.PlayerCount;
                BuildRows(playerCount, localPlayerId);
                initialized = true;
            }
            else
            {
                if (showPing && pingLabels != null)
                    UpdatePingLabels();
                UpdateSurrenderState();
                UpdateHealthBars();
                UpdateAgeLabels();
            }
        }

        private void UpdatePingLabels()
        {
            for (int i = 0; i < pingLabels.Length; i++)
            {
                if (pingLabels[i] == null) continue;
                float ping = network?.GetPlayerPing(i) ?? 0f;
                if (ping > 0f)
                {
                    pingLabels[i].text = $"{Mathf.RoundToInt(ping)}ms";
                    pingLabels[i].color = ping < 50f ? Color.green : ping < 100f ? Color.yellow : Color.red;
                }
                else
                {
                    pingLabels[i].text = "---";
                    pingLabels[i].color = new Color(1f, 1f, 1f, 0.4f);
                }
            }
        }

        private void UpdateSurrenderState()
        {
            if (nameLabels == null) return;
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            for (int pid = 0; pid < nameLabels.Length; pid++)
            {
                if (nameLabels[pid] == null) continue;
                if (sim.SurrenderedPlayers.Contains(pid))
                {
                    nameLabels[pid].fontStyle = FontStyles.Strikethrough;
                    var c = nameLabels[pid].color;
                    c.a = 0.5f;
                    nameLabels[pid].color = c;
                }
            }
        }

        private void UpdateAgeLabels()
        {
            if (ageLabels == null) return;
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            for (int pid = 0; pid < ageLabels.Length; pid++)
            {
                if (ageLabels[pid] == null) continue;
                int age = sim.GetPlayerAge(pid);
                ageLabels[pid].text = LandmarkDefinitions.AgeToRoman(age);
            }
        }

        private void UpdateHealthBars()
        {
            if (healthBarBgs == null) return;
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            var firstTCs = sim.FirstTownCenterIds;

            for (int pid = 0; pid < playerCount; pid++)
            {
                if (healthBarBgs[pid] == null) continue;

                if (!firstTCs.TryGetValue(pid, out int tcId))
                {
                    healthBarBgs[pid].gameObject.SetActive(false);
                    destroyedMarkers[pid].gameObject.SetActive(false);
                    continue;
                }

                var tc = sim.BuildingRegistry.GetBuilding(tcId);
                if (tc != null)
                {
                    healthBarBgs[pid].gameObject.SetActive(true);
                    destroyedMarkers[pid].gameObject.SetActive(false);

                    float fraction = tc.MaxHealth > 0 ? (float)tc.CurrentHealth / tc.MaxHealth : 0f;
                    var fillRT = healthBarFills[pid].rectTransform;
                    fillRT.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1);
                    fillRT.offsetMax = Vector2.zero;
                    healthBarFills[pid].color = Color.Lerp(HealthRed, HealthGreen, fraction);
                }
                else
                {
                    healthBarBgs[pid].gameObject.SetActive(false);
                    destroyedMarkers[pid].gameObject.SetActive(true);
                }
            }
        }

        private void BuildRows(int playerCount, int localPlayerId)
        {
            string[] names = new string[playerCount];
            for (int i = 0; i < playerCount; i++)
                names[i] = $"Player {i + 1}";

            // Build team groups: either from server data or a single synthetic team
            int[][] teamGroups;
            var mm = MatchmakingManager.Instance;
            if (mm?.Teams != null && mm.Teams.Length >= 2)
            {
                teamGroups = new int[mm.Teams.Length][];
                for (int t = 0; t < mm.Teams.Length; t++)
                {
                    var team = mm.Teams[t];
                    if (team.players == null) { teamGroups[t] = new int[0]; continue; }
                    teamGroups[t] = new int[team.players.Length];
                    for (int p = 0; p < team.players.Length; p++)
                    {
                        int id = team.players[p].game_player_id;
                        teamGroups[t][p] = id;
                        if (id >= 0 && id < playerCount && !string.IsNullOrEmpty(team.players[p].username))
                            names[id] = team.players[p].username;
                    }
                }
            }
            else
            {
                // Singleplayer or single-team: everyone in Team 1
                teamGroups = new int[1][];
                teamGroups[0] = new int[playerCount];
                for (int i = 0; i < playerCount; i++)
                    teamGroups[0][i] = i;
            }

            float panelWidth = PanelWidthBase + FlagGap + FlagWidth + AgeColGap + AgeColWidth + HealthBarGap + HealthBarWidth;
            if (showPing)
                panelWidth += PingColGap + PingColWidth;

            nameLabels = new TextMeshProUGUI[playerCount];
            healthBarBgs = new Image[playerCount];
            healthBarFills = new Image[playerCount];
            destroyedMarkers = new TextMeshProUGUI[playerCount];
            ageLabels = new TextMeshProUGUI[playerCount];
            if (showPing)
                pingLabels = new TextMeshProUGUI[playerCount];

            // Compute panel height
            float totalH = Padding * 2f;
            for (int t = 0; t < teamGroups.Length; t++)
            {
                int teamPlayerCount = teamGroups[t].Length;
                totalH += HeaderRowHeight + HeaderGap;
                totalH += teamPlayerCount * RowHeight + Mathf.Max(0, teamPlayerCount - 1) * RowGap;
                if (t < teamGroups.Length - 1) totalH += TeamSectionGap;
            }
            panelRT.sizeDelta = new Vector2(panelWidth, totalH);

            // Position objective hint above the panel, both within screen bounds
            if (objectiveHint != null)
            {
                var hintRT = objectiveHint.GetComponent<RectTransform>();
                hintRT.anchoredPosition = new Vector2(Margin, -Margin);
                panelRT.anchoredPosition = new Vector2(Margin, -Margin - HintHeight - HintGap);
                objectiveHint.gameObject.SetActive(true);
            }

            var panelTransform = panelRT.transform;
            float curY = -Padding;
            for (int t = 0; t < teamGroups.Length; t++)
            {
                AddTeamHeader(panelTransform, panelWidth, curY, t + 1);
                curY -= HeaderRowHeight + HeaderGap;

                int[] players = teamGroups[t];
                for (int p = 0; p < players.Length; p++)
                {
                    int pid = players[p];
                    Color color = pid < GameSetup.PlayerColors.Length ? GameSetup.PlayerColors[pid] : Color.white;
                    AddPlayerRow(panelTransform, panelWidth, curY, pid, names[pid], color, localPlayerId);
                    curY -= RowHeight;
                    if (p < players.Length - 1) curY -= RowGap;
                }

                if (t < teamGroups.Length - 1) curY -= TeamSectionGap;
            }

            panelTransform.gameObject.SetActive(true);
        }

        private void AddTeamHeader(Transform parent, float panelWidth, float curY, int teamNumber)
        {
            var headerGO = new GameObject($"TeamHeader{teamNumber}");
            headerGO.transform.SetParent(parent, false);
            var headerRT = headerGO.AddComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(0, 1);
            headerRT.pivot = new Vector2(0, 1);
            headerRT.anchoredPosition = new Vector2(0, curY);
            headerRT.sizeDelta = new Vector2(panelWidth, HeaderRowHeight);
            var headerImg = headerGO.AddComponent<Image>();
            headerImg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            headerImg.raycastTarget = false;

            var labelGO = new GameObject($"TeamLabel{teamNumber}");
            labelGO.transform.SetParent(headerGO.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = new Vector2(Padding, 0);
            labelRT.offsetMax = Vector2.zero;
            var labelTmp = labelGO.AddComponent<TextMeshProUGUI>();
            labelTmp.text = $"Team {teamNumber}";
            labelTmp.fontSize = 10;
            labelTmp.fontStyle = FontStyles.Bold;
            labelTmp.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            labelTmp.alignment = TextAlignmentOptions.Left;
            labelTmp.raycastTarget = false;
        }

        private void AddPlayerRow(Transform parent, float panelWidth, float rowTopY, int playerId, string name, Color color, int localPlayerId)
        {
            var swatchGO = new GameObject($"Swatch{playerId}");
            swatchGO.transform.SetParent(parent, false);
            var swatchRT = swatchGO.AddComponent<RectTransform>();
            swatchRT.anchorMin = new Vector2(0, 1);
            swatchRT.anchorMax = new Vector2(0, 1);
            swatchRT.pivot = new Vector2(0, 1);
            swatchRT.anchoredPosition = new Vector2(Padding, rowTopY - (RowHeight - SwatchSize) * 0.5f);
            swatchRT.sizeDelta = new Vector2(SwatchSize, SwatchSize);
            var swatchImg = swatchGO.AddComponent<Image>();
            swatchImg.color = color;
            swatchImg.raycastTarget = false;

            // Civ flag
            float flagX = Padding + SwatchSize + FlagGap;
            if (network != null)
            {
                var civ = network.GetPlayerCivilization(playerId);
                var flagTex = network.GetCivFlagTexture(civ);
                if (flagTex != null)
                {
                    var flagGO = new GameObject($"Flag{playerId}");
                    flagGO.transform.SetParent(parent, false);
                    var flagRT = flagGO.AddComponent<RectTransform>();
                    flagRT.anchorMin = new Vector2(0, 1);
                    flagRT.anchorMax = new Vector2(0, 1);
                    flagRT.pivot = new Vector2(0, 1);
                    flagRT.anchoredPosition = new Vector2(flagX, rowTopY - (RowHeight - FlagHeight) * 0.5f);
                    flagRT.sizeDelta = new Vector2(FlagWidth, FlagHeight);
                    var flagImg = flagGO.AddComponent<RawImage>();
                    flagImg.texture = flagTex;
                    flagImg.raycastTarget = false;
                }
            }

            string label = playerId == localPlayerId ? $"{name} (you)" : name;
            float nameX = Padding + SwatchSize + FlagGap + FlagWidth + SwatchTextGap;
            float rightColumnsWidth = HealthBarGap + HealthBarWidth + (showPing ? PingColGap + PingColWidth : 0f);
            float nameW = panelWidth - nameX - Padding - rightColumnsWidth;
            var nameGO = new GameObject($"Name{playerId}");
            nameGO.transform.SetParent(parent, false);
            var nameRT = nameGO.AddComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 1);
            nameRT.anchorMax = new Vector2(0, 1);
            nameRT.pivot = new Vector2(0, 1);
            nameRT.anchoredPosition = new Vector2(nameX, rowTopY);
            nameRT.sizeDelta = new Vector2(nameW, RowHeight);
            var tmp = nameGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 13;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            if (playerId >= 0 && playerId < nameLabels.Length)
                nameLabels[playerId] = tmp;

            // Age label (between name and health bar)
            float ageX = panelWidth - Padding - (showPing ? PingColGap + PingColWidth : 0f) - HealthBarWidth - HealthBarGap - AgeColWidth;
            var ageGO = new GameObject($"Age{playerId}");
            ageGO.transform.SetParent(parent, false);
            var ageRT = ageGO.AddComponent<RectTransform>();
            ageRT.anchorMin = new Vector2(0, 1);
            ageRT.anchorMax = new Vector2(0, 1);
            ageRT.pivot = new Vector2(0, 1);
            ageRT.anchoredPosition = new Vector2(ageX, rowTopY);
            ageRT.sizeDelta = new Vector2(AgeColWidth, RowHeight);
            var ageTmp = ageGO.AddComponent<TextMeshProUGUI>();
            ageTmp.text = "I";
            ageTmp.fontSize = 11;
            ageTmp.color = new Color(0.8f, 0.75f, 0.4f);
            ageTmp.alignment = TextAlignmentOptions.Center;
            ageTmp.raycastTarget = false;
            if (playerId >= 0 && playerId < ageLabels.Length)
                ageLabels[playerId] = ageTmp;

            // Health bar background
            float hbX = panelWidth - Padding - (showPing ? PingColGap + PingColWidth : 0f) - HealthBarWidth;
            var hbBgGO = new GameObject($"HBBg{playerId}");
            hbBgGO.transform.SetParent(parent, false);
            var hbBgRT = hbBgGO.AddComponent<RectTransform>();
            hbBgRT.anchorMin = new Vector2(0, 1);
            hbBgRT.anchorMax = new Vector2(0, 1);
            hbBgRT.pivot = new Vector2(0, 1);
            hbBgRT.anchoredPosition = new Vector2(hbX, rowTopY - (RowHeight - HealthBarHeight) * 0.5f);
            hbBgRT.sizeDelta = new Vector2(HealthBarWidth, HealthBarHeight);
            var hbBgImg = hbBgGO.AddComponent<Image>();
            hbBgImg.color = new Color(0.15f, 0.15f, 0.15f);
            hbBgImg.raycastTarget = false;
            if (playerId >= 0 && playerId < healthBarBgs.Length)
                healthBarBgs[playerId] = hbBgImg;

            // Health bar fill
            var hbFillGO = new GameObject($"HBFill{playerId}");
            hbFillGO.transform.SetParent(hbBgGO.transform, false);
            var hbFillRT = hbFillGO.AddComponent<RectTransform>();
            hbFillRT.anchorMin = Vector2.zero;
            hbFillRT.anchorMax = new Vector2(1, 1);
            hbFillRT.offsetMin = Vector2.zero;
            hbFillRT.offsetMax = Vector2.zero;
            var hbFillImg = hbFillGO.AddComponent<Image>();
            hbFillImg.color = HealthGreen;
            hbFillImg.raycastTarget = false;
            if (playerId >= 0 && playerId < healthBarFills.Length)
                healthBarFills[playerId] = hbFillImg;

            // Destroyed marker (hidden by default)
            var destroyedGO = new GameObject($"Destroyed{playerId}");
            destroyedGO.transform.SetParent(parent, false);
            var destroyedRT = destroyedGO.AddComponent<RectTransform>();
            destroyedRT.anchorMin = new Vector2(0, 1);
            destroyedRT.anchorMax = new Vector2(0, 1);
            destroyedRT.pivot = new Vector2(0, 1);
            destroyedRT.anchoredPosition = new Vector2(hbX, rowTopY);
            destroyedRT.sizeDelta = new Vector2(HealthBarWidth, RowHeight);
            var destroyedTmp = destroyedGO.AddComponent<TextMeshProUGUI>();
            destroyedTmp.text = "X";
            destroyedTmp.fontSize = 14;
            destroyedTmp.fontStyle = FontStyles.Bold;
            destroyedTmp.color = DestroyedColor;
            destroyedTmp.alignment = TextAlignmentOptions.Center;
            destroyedTmp.raycastTarget = false;
            if (playerId >= 0 && playerId < destroyedMarkers.Length)
                destroyedMarkers[playerId] = destroyedTmp;
            destroyedGO.SetActive(false);

            if (showPing)
            {
                float pingX = panelWidth - Padding - PingColWidth;
                var pingGO = new GameObject($"Ping{playerId}");
                pingGO.transform.SetParent(parent, false);
                var pingRT = pingGO.AddComponent<RectTransform>();
                pingRT.anchorMin = new Vector2(0, 1);
                pingRT.anchorMax = new Vector2(0, 1);
                pingRT.pivot = new Vector2(0, 1);
                pingRT.anchoredPosition = new Vector2(pingX, rowTopY);
                pingRT.sizeDelta = new Vector2(PingColWidth, RowHeight);
                var pingTmp = pingGO.AddComponent<TextMeshProUGUI>();
                pingTmp.text = "---";
                pingTmp.fontSize = 11;
                pingTmp.color = new Color(1f, 1f, 1f, 0.4f);
                pingTmp.alignment = TextAlignmentOptions.Right;
                pingTmp.overflowMode = TextOverflowModes.Ellipsis;
                pingTmp.raycastTarget = false;
                pingLabels[playerId] = pingTmp;
            }
        }
    }
}
