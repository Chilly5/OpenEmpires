using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenEmpires
{
    public sealed partial class CommanderChatUI
    {
        private TMP_Text taskBadgeText;
        private ScrollRect taskBoardScroll;
        private RectTransform taskBoardContent;
        private string taskBoardFingerprint;

        private void BuildTaskBoardUI()
        {
            taskBadgeText = Text("TaskBadge", compactCommander.transform, string.Empty, 12, TextAlignmentOptions.Left);
            taskBadgeText.richText = false;
            PanelElement(taskBadgeText.gameObject, 20);
            var board = UIObject("TaskBoard", commanderPanel);
            board.AddComponent<Image>().color = new Color(.045f, .065f, .09f, .97f);
            PanelElement(board, 90);
            taskBoardScroll = board.AddComponent<ScrollRect>();
            taskBoardScroll.horizontal = false;
            taskBoardScroll.vertical = true;
            taskBoardScroll.movementType = ScrollRect.MovementType.Clamped;
            taskBoardScroll.scrollSensitivity = 22;
            var viewport = UIObject("Viewport", board.transform);
            SetRect(viewport.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
                new Vector2(4, 4), new Vector2(-4, -4));
            viewport.AddComponent<Image>().color = new Color(0, 0, 0, .08f);
            viewport.AddComponent<RectMask2D>();
            taskBoardScroll.viewport = viewport.GetComponent<RectTransform>();
            var content = UIObject("Content", viewport.transform);
            taskBoardContent = content.GetComponent<RectTransform>();
            taskBoardContent.anchorMin = new Vector2(0, 1);
            taskBoardContent.anchorMax = Vector2.one;
            taskBoardContent.pivot = new Vector2(.5f, 1);
            taskBoardContent.sizeDelta = Vector2.zero;
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = 6;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            taskBoardScroll.content = taskBoardContent;
            board.transform.SetSiblingIndex(3);
            var transcriptLayout = transcriptScroll.GetComponent<LayoutElement>();
            transcriptLayout.minHeight = 70;
            transcriptLayout.preferredHeight = 70;
            taskBoardFingerprint = null;
            RefreshTaskBoard();
        }

        private void RefreshTaskBoard()
        {
            if (taskBadgeText == null || taskBoardContent == null) return;
            var snapshot = CommanderTaskBoardProjection.Capture(semanticGoalManager,
                strategicPipeline?.StrategicPlanner, runtimeGeneration);
            taskBadgeText.text = snapshot.ActiveCount + " active task" + (snapshot.ActiveCount == 1 ? "" : "s");
            taskBadgeText.gameObject.SetActive(snapshot.ActiveCount > 0);
            string fingerprint = Fingerprint(snapshot);
            if (fingerprint == taskBoardFingerprint) return;
            taskBoardFingerprint = fingerprint;
            for (int i = taskBoardContent.childCount - 1; i >= 0; i--)
            {
                var child = taskBoardContent.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
            if (snapshot.Cards.Count == 0)
            {
                var empty = Text("NoTasks", taskBoardContent, "No tracked long-running tasks.", 12, TextAlignmentOptions.Left);
                empty.richText = false; PanelElement(empty.gameObject, 24);
                return;
            }
            foreach (var card in snapshot.Cards) RenderTaskCard(card);
        }

        private static string Fingerprint(CommanderTaskBoardSnapshot snapshot)
        {
            var value = new StringBuilder();
            foreach (var card in snapshot.Cards)
            {
                value.Append(card.RequestId).Append('|').Append(card.StrategicPlanId).Append('|')
                    .Append(card.Generation).Append('|').Append(card.RuntimeToken).Append('|')
                    .Append(card.Status).Append('|').Append(card.CanCancel).Append('|').Append(card.Objective).Append('|').Append(card.Progress)
                    .Append('|').Append(card.CurrentStep).Append('|').Append(card.Blocker);
                foreach (var step in card.Steps)
                    value.Append('|').Append(step.GoalId).Append(':').Append(step.Status).Append(':')
                        .Append(step.Label).Append(':').Append(step.Progress).Append(':').Append(step.Blocker);
            }
            return value.ToString();
        }

        private void RenderTaskCard(CommanderTaskCardSnapshot card)
        {
            var root = UIObject("TaskCard", taskBoardContent);
            root.AddComponent<Image>().color = new Color(.075f, .105f, .15f, .98f);
            var layout = root.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = 3;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var title = Text("Objective", root.transform, card.Objective, 13, TextAlignmentOptions.Left);
            title.richText = false; PanelElement(title.gameObject, 34);
            var status = Text("Status", root.transform, card.Status + " — " + card.Progress, 12, TextAlignmentOptions.Left);
            status.richText = false; PanelElement(status.gameObject, 32);
            var stepText = Text("CurrentStep", root.transform,
                "Current: " + card.CurrentStep + (string.IsNullOrEmpty(card.Blocker) ? "" : " — " + card.Blocker),
                11, TextAlignmentOptions.Left);
            stepText.richText = false; PanelElement(stepText.gameObject, 32);
            foreach (var step in card.Steps)
            {
                var detail = Text("Step", root.transform, "• " + step.Label + " — " + step.Status
                    + (string.IsNullOrEmpty(step.Progress) ? "" : " — " + step.Progress), 11, TextAlignmentOptions.Left);
                detail.richText = false; PanelElement(detail.gameObject, 34);
            }
            if (card.CanCancel)
            {
                var cancel = CompactButton(root.transform, "CancelTask", "Cancel task", () =>
                {
                    if (CommanderTaskBoardProjection.TryCancel(semanticGoalManager,
                        strategicPipeline?.StrategicPlanner, card, runtimeGeneration))
                    {
                        taskBoardFingerprint = null;
                        RefreshTaskBoard();
                    }
                });
                PanelElement(cancel.gameObject, 27);
            }
            root.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }
}
