using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OpenEmpires
{
    public class GodPowerBarUI : MonoBehaviour
    {
        private struct PowerSlot
        {
            public GameObject Root;
            public Image Background;
            public Image CooldownOverlay;
            public TextMeshProUGUI Label;
            public Button Button;
            public string Name;
            public Color ReadyColor;
            public Color CooldownColor;
        }

        private PowerSlot[] slots;
        private static GodPowerBarUI instance;
        public static GodPowerBarUI Instance => instance;
        private bool cheatsEnabled;

        private void Awake()
        {
            instance = this;
        }

        private void Start()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
                transform.SetParent(canvas.transform, false);

            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(10f, 220f);
            rt.sizeDelta = new Vector2(220f, 50f);

            slots = new PowerSlot[4];
            CreateSlot(0, "Meteor", new Color(0.6f, 0.15f, 0.1f, 0.8f), new Color(0.3f, 0.1f, 0.05f, 0.8f), OnMeteorClicked);
            CreateSlot(1, "Heal", new Color(0.15f, 0.5f, 0.15f, 0.8f), new Color(0.1f, 0.25f, 0.1f, 0.8f), OnHealingRainClicked);
            CreateSlot(2, "Bolt", new Color(0.4f, 0.2f, 0.6f, 0.8f), new Color(0.2f, 0.1f, 0.3f, 0.8f), OnLightningClicked);
            CreateSlot(3, "Wave", new Color(0.1f, 0.3f, 0.6f, 0.8f), new Color(0.05f, 0.15f, 0.3f, 0.8f), OnTsunamiClicked);

            // Hidden by default - only shown via cheat
            gameObject.SetActive(false);
        }

        public static void SetCheatsEnabled(bool enabled)
        {
            if (instance == null) return;
            instance.cheatsEnabled = enabled;
            instance.gameObject.SetActive(enabled);
        }

        public static bool IsCheatsEnabled => instance != null && instance.cheatsEnabled;

        private void OnMeteorClicked()
        {
            var usm = Object.FindFirstObjectByType<UnitSelectionManager>();
            usm?.ActivateMeteorTargeting();
        }

        private void OnHealingRainClicked()
        {
            var usm = Object.FindFirstObjectByType<UnitSelectionManager>();
            usm?.ActivateHealingRainTargeting();
        }

        private void OnLightningClicked()
        {
            var usm = Object.FindFirstObjectByType<UnitSelectionManager>();
            usm?.ActivateLightningStormTargeting();
        }

        private void OnTsunamiClicked()
        {
            var usm = Object.FindFirstObjectByType<UnitSelectionManager>();
            usm?.ActivateTsunamiTargeting();
        }

        private void CreateSlot(int index, string name, Color readyColor, Color cooldownColor, System.Action onClick)
        {
            float slotSize = 50f;
            float spacing = 5f;

            var slotGO = new GameObject($"PowerSlot_{name}");
            slotGO.transform.SetParent(transform, false);
            var slotRT = slotGO.AddComponent<RectTransform>();
            slotRT.anchorMin = new Vector2(0f, 0f);
            slotRT.anchorMax = new Vector2(0f, 1f);
            slotRT.pivot = new Vector2(0f, 0f);
            slotRT.anchoredPosition = new Vector2(index * (slotSize + spacing), 0f);
            slotRT.sizeDelta = new Vector2(slotSize, 0f);

            var bg = slotGO.AddComponent<Image>();
            bg.color = readyColor;

            // Make it a clickable button
            var btn = slotGO.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = readyColor;
            colors.highlightedColor = readyColor * 1.2f;
            colors.pressedColor = readyColor * 0.8f;
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());

            // Cooldown sweep overlay
            var overlayGO = new GameObject("CooldownOverlay");
            overlayGO.transform.SetParent(slotGO.transform, false);
            var overlayRT = overlayGO.AddComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.sizeDelta = Vector2.zero;
            var overlay = overlayGO.AddComponent<Image>();
            overlay.type = Image.Type.Filled;
            overlay.fillMethod = Image.FillMethod.Radial360;
            overlay.fillOrigin = (int)Image.Origin360.Top;
            overlay.fillClockwise = false;
            overlay.color = new Color(0f, 0f, 0f, 0.6f);
            overlay.raycastTarget = false;

            // Label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(slotGO.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.sizeDelta = Vector2.zero;
            var label = labelGO.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 12;
            label.color = Color.white;
            label.text = name;
            label.raycastTarget = false;

            slots[index] = new PowerSlot
            {
                Root = slotGO,
                Background = bg,
                CooldownOverlay = overlay,
                Label = label,
                Button = btn,
                Name = name,
                ReadyColor = readyColor,
                CooldownColor = cooldownColor
            };
        }

        private void Update()
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null || slots == null) return;

            var net = GameBootstrapper.Instance?.Network;
            int localPlayerId = net != null && net.IsMultiplayer ? net.LocalPlayerId : 0;
            int tickRate = sim.Config.TickRate;

            UpdateSlot(0, sim.GetMeteorCooldownRemaining(localPlayerId), sim.Config.MeteorCooldownTicks, tickRate);
            UpdateSlot(1, sim.GetHealingRainCooldownRemaining(localPlayerId), sim.Config.HealingRainCooldownTicks, tickRate);
            UpdateSlot(2, sim.GetLightningStormCooldownRemaining(localPlayerId), sim.Config.LightningStormCooldownTicks, tickRate);
            UpdateSlot(3, sim.GetTsunamiCooldownRemaining(localPlayerId), sim.Config.TsunamiCooldownTicks, tickRate);
        }

        private void UpdateSlot(int index, int remaining, int totalCooldown, int tickRate)
        {
            var slot = slots[index];

            if (remaining > 0)
            {
                slot.CooldownOverlay.fillAmount = remaining / (float)totalCooldown;
                int seconds = Mathf.CeilToInt(remaining / (float)tickRate);
                slot.Label.text = seconds.ToString();
                slot.Background.color = slot.CooldownColor;
                slot.Button.interactable = false;
            }
            else
            {
                slot.CooldownOverlay.fillAmount = 0f;
                slot.Label.text = slot.Name;
                slot.Background.color = slot.ReadyColor;
                slot.Button.interactable = true;
            }
        }
    }
}
