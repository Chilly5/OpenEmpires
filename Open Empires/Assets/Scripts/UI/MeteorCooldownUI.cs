using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OpenEmpires
{
    public class MeteorCooldownUI : MonoBehaviour
    {
        private Image cooldownOverlay;
        private TextMeshProUGUI label;
        private Image iconBackground;

        private void Start()
        {
            // Find existing canvas and reparent
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
                transform.SetParent(canvas.transform, false);

            // Position bottom-left, above minimap area
            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(10f, 220f);
            rt.sizeDelta = new Vector2(50f, 50f);

            // Background
            iconBackground = gameObject.AddComponent<Image>();
            iconBackground.color = new Color(0.2f, 0.4f, 0.15f, 0.8f);

            // Cooldown sweep overlay
            var overlayGO = new GameObject("CooldownOverlay");
            overlayGO.transform.SetParent(transform, false);
            var overlayRT = overlayGO.AddComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.sizeDelta = Vector2.zero;
            cooldownOverlay = overlayGO.AddComponent<Image>();
            cooldownOverlay.type = Image.Type.Filled;
            cooldownOverlay.fillMethod = Image.FillMethod.Radial360;
            cooldownOverlay.fillOrigin = (int)Image.Origin360.Top;
            cooldownOverlay.fillClockwise = false;
            cooldownOverlay.color = new Color(0f, 0f, 0f, 0.6f);

            // Label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.sizeDelta = Vector2.zero;
            label = labelGO.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 16;
            label.color = Color.white;
            label.text = "Q";
        }

        private void Update()
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            var net = GameBootstrapper.Instance?.Network;
            int localPlayerId = net != null && net.IsMultiplayer ? net.LocalPlayerId : 0;

            int remaining = sim.GetMeteorCooldownRemaining(localPlayerId);

            if (remaining > 0)
            {
                float total = sim.Config.MeteorCooldownTicks;
                cooldownOverlay.fillAmount = remaining / total;
                int seconds = Mathf.CeilToInt(remaining / (float)sim.Config.TickRate);
                label.text = seconds.ToString();
                iconBackground.color = new Color(0.3f, 0.15f, 0.1f, 0.8f);
            }
            else
            {
                cooldownOverlay.fillAmount = 0f;
                label.text = "Q";
                iconBackground.color = new Color(0.2f, 0.4f, 0.15f, 0.8f);
            }
        }
    }
}
