using TMPro;
using UnityEngine;

namespace OpenEmpires
{
    // Floating "!" marker placed on the ground where the player pinged. Billboards to the camera,
    // rises slightly, and fades out before self-destructing.
    public class WorldPingMarker : MonoBehaviour
    {
        private const float Lifetime = 3f;
        private const float RiseDistance = 1.0f;
        private const float InitialScale = 1.6f;

        private TMP_Text label;
        private Camera cam;
        private float elapsed;
        private Vector3 startPos;

        public static void Spawn(Vector3 worldPos)
        {
            var go = new GameObject("WorldPingMarker");
            go.transform.position = worldPos + Vector3.up * 0.5f;
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
            float t = Mathf.Clamp01(elapsed / Lifetime);

            transform.position = startPos + Vector3.up * (RiseDistance * t);

            if (cam != null)
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

            if (label != null)
            {
                var c = label.color;
                c.a = Mathf.SmoothStep(1f, 0f, t);
                label.color = c;
            }

            if (elapsed >= Lifetime)
                Destroy(gameObject);
        }
    }
}
