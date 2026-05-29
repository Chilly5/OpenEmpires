using TMPro;
using UnityEngine;

namespace OpenEmpires
{
    // "!" marker placed where the player pinged. It flies down from the sky, lands at ground
    // level with a recoil + shake that decays over time, holds, then fades out. Billboards to
    // the camera throughout.
    public class WorldPingMarker : MonoBehaviour
    {
        private const float Lifetime = 6f;
        private const float SpawnHeightOffset = 2.5f; // resting height of the text above the ground

        private const float InitialScale = 1.6f;

        // Fall-in
        private const float FallHeight = 15f;     // starts this far above the landing point
        private const float FallDuration = 0.35f; // time to drop to the ground

        // Landing recoil / shake (damped). Amplitude decays as exp(-ShakeDecay * t).
        private const float ShakeAmplitude = 0.35f; // world-space shake displacement at impact
        private const float ShakeScaleAmp = 0.35f;  // scale "punch" recoil at impact
        private const float ShakeDecay = 4.0f;       // higher = shaking settles faster
        private const float ShakeFreqX = 38f;        // horizontal shake frequency
        private const float ShakeFreqY = 47f;        // vertical shake frequency (different = chaotic)

        // Fade-out
        private const float FadeStartTime = 5f;   // full alpha until this time, then fade
        private const float FadeOutDuration = 1f; // fade over the final second

        private TMP_Text label;
        private Camera cam;
        private float elapsed;
        private Vector3 landingPos;

        public static void Spawn(Vector3 worldPos)
        {
            var go = new GameObject("WorldPingMarker");
            go.transform.position = worldPos + Vector3.up * SpawnHeightOffset;
            go.AddComponent<WorldPingMarker>();
        }

        private void Awake()
        {
            landingPos = transform.position;
            cam = Camera.main;
            // Start up in the sky so the first frame doesn't flash at the landing spot.
            transform.position = landingPos + Vector3.up * FallHeight;

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

            Vector3 pos;
            float scaleMul = 1f;

            if (elapsed < FallDuration)
            {
                // Accelerating drop (ease-in, gravity-like) from FallHeight down to the ground.
                float t = elapsed / FallDuration;
                float fallen = t * t;
                pos = landingPos + Vector3.up * (FallHeight * (1f - fallen));
            }
            else
            {
                // Landed: damped shake on both axes plus a scale-punch recoil, decaying over time.
                float st = elapsed - FallDuration;
                float decay = Mathf.Exp(-ShakeDecay * st);
                Vector3 offset = Vector3.zero;
                if (cam != null)
                {
                    offset = (cam.transform.right * Mathf.Sin(st * ShakeFreqX)
                            + cam.transform.up    * Mathf.Sin(st * ShakeFreqY + 0.6f))
                           * (ShakeAmplitude * decay);
                }
                pos = landingPos + offset;
                scaleMul = 1f + ShakeScaleAmp * decay * Mathf.Sin(st * ShakeFreqX);
            }

            transform.position = pos;
            if (cam != null)
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

            if (label != null)
            {
                label.transform.localScale = Vector3.one * (InitialScale * scaleMul);

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
