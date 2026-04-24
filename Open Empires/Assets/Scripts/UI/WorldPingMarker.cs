using TMPro;
using UnityEngine;

namespace OpenEmpires
{
    // "!" marker placed on the ground where the player pinged. Billboards to the camera,
    // bounces in place while the scale-pulse plays, and stays visible after the pulsing ends
    // before a short fade-out at the very end.
    public class WorldPingMarker : MonoBehaviour
    {
        private const float Lifetime = 6f;
        private const float SpawnHeightOffset = 2.5f; // lifts the text clear of the ground
        private const float InitialScale = 1.6f;
        private const int PulseCount = 3;
        private const float PulseScaleAmplitude = 0.35f;
        private const float PulseWindow = 3.9f;       // scale-pulses play during this window
        private const float FadeStartTime = 5f;       // full alpha until this time, then fade
        private const float FadeOutDuration = 1f;     // fade over the final second

        private TMP_Text label;
        private Camera cam;
        private float elapsed;
        private Vector3 startPos;

        public static void Spawn(Vector3 worldPos)
        {
            var go = new GameObject("WorldPingMarker");
            go.transform.position = worldPos + Vector3.up * SpawnHeightOffset;
            go.AddComponent<WorldPingMarker>();
        }

        private void Awake()
        {
            startPos = transform.position;
            cam = Camera.main;

            var textGO = new GameObject("Label");
            textGO.transform.SetParent(transform, false);
            label = textGO.AddComponent<TextMeshPro>();
            label.text = "!";
            label.fontSize = 18;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 0.35f, 0.15f, 1f);
            label.enableVertexGradient = false;
            label.transform.localScale = Vector3.one * InitialScale;

            var mr = textGO.GetComponent<MeshRenderer>();
            if (mr != null && mr.material != null)
            {
                // Per-instance material so we don't pollute the shared TMP asset.
                mr.material.SetFloat("_ZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
                mr.sortingOrder = 50;
            }
        }

        private void LateUpdate()
        {
            elapsed += Time.deltaTime;

            // Stay anchored in place — no movement.
            transform.position = startPos;

            if (cam != null)
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

            if (label != null)
            {
                // Scale pulse plays PulseCount times across PulseWindow, then locks to base scale.
                float pulseScale = 1f;
                if (elapsed < PulseWindow)
                {
                    float pulseT = elapsed / PulseWindow;
                    float pulseBeat = pulseT * PulseCount;
                    float pulsePhase = pulseBeat - Mathf.Floor(pulseBeat);
                    pulseScale = 1f + PulseScaleAmplitude * Mathf.Sin(Mathf.PI * pulsePhase);
                }
                label.transform.localScale = Vector3.one * (InitialScale * pulseScale);

                // Full opacity until FadeStartTime, then fade to 0 over FadeOutDuration.
                float alpha = 1f;
                if (elapsed > FadeStartTime)
                    alpha = Mathf.Clamp01(1f - (elapsed - FadeStartTime) / FadeOutDuration);

                var c = label.color;
                c.a = alpha;
                label.color = c;
            }

            if (elapsed >= Lifetime)
                Destroy(gameObject);
        }
    }
}
