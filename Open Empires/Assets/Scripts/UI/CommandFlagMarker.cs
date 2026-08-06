using TMPro;
using UnityEngine;

namespace OpenEmpires
{
    public enum CommandFlagKind
    {
        Move,
        AttackMove,
        Rally
    }

    public class CommandFlagMarker : MonoBehaviour
    {
        private const float FallHeight = 8f;
        private const float FallDuration = 0.28f;
        private const float FadeStartTime = 0.9f;
        private const float Lifetime = 1.45f;
        private const int PulseSegments = 40;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private CommandFlagKind kind;
        private bool persistent;
        private bool initialized;
        private float elapsed;
        private Vector3 landingPosition;
        private Camera cam;

        private Material poleMaterial;
        private Material bannerMaterial;
        private Material pulseMaterial;
        private MeshRenderer poleRenderer;
        private MeshRenderer bannerRenderer;
        private LineRenderer pulseRing;
        private LineRenderer echoRing;
        private TextMeshPro glyph;

        private Color poleColor;
        private Color bannerColor;
        private Color pulseColor;

        public static CommandFlagMarker Spawn(Vector3 worldPosition, CommandFlagKind kind, Color? colorOverride = null)
        {
            var go = new GameObject($"{kind}CommandFlag");
            var marker = go.AddComponent<CommandFlagMarker>();
            marker.Initialize(worldPosition, kind, false, colorOverride ?? GetDefaultColor(kind), true);
            return marker;
        }

        public static CommandFlagMarker CreatePersistent(Transform parent, CommandFlagKind kind, Color? colorOverride = null)
        {
            var go = new GameObject($"{kind}CommandFlag");
            if (parent != null)
                go.transform.SetParent(parent, true);

            var marker = go.AddComponent<CommandFlagMarker>();
            marker.Initialize(Vector3.zero, kind, true, colorOverride ?? GetDefaultColor(kind), false);
            go.SetActive(false);
            return marker;
        }

        public void SetLandingPosition(Vector3 worldPosition, bool replayDrop)
        {
            landingPosition = worldPosition;
            if (replayDrop)
            {
                elapsed = 0f;
                transform.position = landingPosition + Vector3.up * FallHeight;
            }
            else if (persistent || elapsed >= FallDuration)
            {
                transform.position = landingPosition;
            }
        }

        public void SetMarkerColor(Color color)
        {
            bannerColor = color;
            pulseColor = new Color(color.r, color.g, color.b, Mathf.Max(color.a, 0.75f));

            Color poleTint = Color.Lerp(color, Color.white, 0.45f);
            poleColor = new Color(poleTint.r, poleTint.g, poleTint.b, 0.85f);

            ApplyAlpha(1f);
        }

        public void SetPersistentVisible(bool visible)
        {
            if (!persistent)
            {
                gameObject.SetActive(visible);
                return;
            }

            if (visible && !gameObject.activeSelf)
            {
                elapsed = FallDuration;
                transform.position = landingPosition;
                gameObject.SetActive(true);
            }
            else if (!visible && gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void Initialize(Vector3 worldPosition, CommandFlagKind markerKind, bool isPersistent, Color color, bool replayDrop)
        {
            kind = markerKind;
            persistent = isPersistent;
            cam = Camera.main;

            BuildVisuals();
            SetMarkerColor(color);
            SetLandingPosition(worldPosition, replayDrop);

            if (!replayDrop)
                elapsed = FallDuration;

            initialized = true;
        }

        private void BuildVisuals()
        {
            poleMaterial = CreateTransparentMaterial(Color.white);
            bannerMaterial = CreateTransparentMaterial(Color.white);
            pulseMaterial = CreateTransparentMaterial(Color.white, true);

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(transform, false);
            pole.transform.localPosition = new Vector3(0f, 0.52f, 0f);
            pole.transform.localScale = new Vector3(0.035f, 0.52f, 0.035f);
            DestroyCollider(pole);
            poleRenderer = pole.GetComponent<MeshRenderer>();
            poleRenderer.sharedMaterial = poleMaterial;
            poleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            poleRenderer.receiveShadows = false;

            var banner = new GameObject("Banner");
            banner.transform.SetParent(transform, false);
            banner.transform.localPosition = new Vector3(0.04f, 1.06f, 0f);
            banner.AddComponent<MeshFilter>().sharedMesh = CreateBannerMesh();
            bannerRenderer = banner.AddComponent<MeshRenderer>();
            bannerRenderer.sharedMaterial = bannerMaterial;
            bannerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bannerRenderer.receiveShadows = false;

            if (kind == CommandFlagKind.AttackMove)
                CreateGlyph("A", new Color(0.18f, 0.03f, 0.02f, 0.95f));

            pulseRing = CreatePulseRing("Pulse", 0.055f);
            echoRing = CreatePulseRing("EchoPulse", 0.035f);
        }

        private void CreateGlyph(string text, Color color)
        {
            var glyphGO = new GameObject("Glyph");
            glyphGO.transform.SetParent(transform, false);
            glyphGO.transform.localPosition = new Vector3(0.35f, 1.06f, 0.02f);
            glyphGO.transform.localScale = Vector3.one * 0.08f;

            glyph = glyphGO.AddComponent<TextMeshPro>();
            glyph.text = text;
            glyph.fontSize = 4f;
            glyph.fontStyle = FontStyles.Bold;
            glyph.alignment = TextAlignmentOptions.Center;
            glyph.color = color;
            glyph.enableWordWrapping = false;
            glyph.rectTransform.sizeDelta = new Vector2(4f, 3f);
        }

        private void Update()
        {
            if (!initialized) return;

            elapsed += Time.deltaTime;
            float alpha = 1f;

            if (!persistent && elapsed > FadeStartTime)
                alpha = Mathf.Clamp01(1f - (elapsed - FadeStartTime) / (Lifetime - FadeStartTime));

            UpdateDropMotion();
            UpdatePulse(alpha);
            ApplyAlpha(alpha);
            FaceCamera();

            if (!persistent && elapsed >= Lifetime)
                Destroy(gameObject);
        }

        private void UpdateDropMotion()
        {
            if (elapsed < FallDuration)
            {
                float t = Mathf.Clamp01(elapsed / FallDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                transform.position = landingPosition + Vector3.up * (FallHeight * (1f - eased));
                transform.localScale = Vector3.one;
                return;
            }

            float settledTime = elapsed - FallDuration;
            float decay = Mathf.Exp(-8f * settledTime);
            float bounce = Mathf.Max(0f, Mathf.Sin(settledTime * 22f) * 0.18f * decay);
            float scalePunch = 1f + Mathf.Sin(settledTime * 26f) * 0.08f * decay;
            transform.position = landingPosition + Vector3.up * bounce;
            transform.localScale = Vector3.one * scalePunch;
        }

        private void UpdatePulse(float alpha)
        {
            if (pulseRing == null) return;

            if (persistent)
            {
                float radius = 0.52f + Mathf.Sin(Time.time * 2.4f) * 0.04f;
                float pulseAlpha = 0.22f + Mathf.Sin(Time.time * 2.4f) * 0.04f;
                UpdatePulseRing(pulseRing, radius, pulseAlpha * alpha, 0.045f);

                if (echoRing != null)
                    echoRing.enabled = false;
                return;
            }

            float pulseAge = elapsed - FallDuration;
            float t = Mathf.Clamp01(pulseAge / 0.68f);
            float easedRadius = 1f - Mathf.Pow(1f - t, 3f);
            float ringAlpha = 0.68f * Mathf.Pow(1f - t, 2f) * alpha;
            float ringWidth = Mathf.Lerp(0.095f, 0.012f, t);
            UpdatePulseRing(pulseRing, Mathf.Lerp(0.18f, 1.18f, easedRadius), ringAlpha, ringWidth);

            if (echoRing == null) return;

            float echoAge = pulseAge - 0.11f;
            if (echoAge < 0f)
            {
                echoRing.enabled = false;
                return;
            }

            float echoT = Mathf.Clamp01(echoAge / 0.72f);
            float echoRadius = Mathf.SmoothStep(0.28f, 1.35f, echoT);
            float echoAlpha = 0.26f * Mathf.Pow(1f - echoT, 2.4f) * alpha;
            float echoWidth = Mathf.Lerp(0.05f, 0.008f, echoT);
            UpdatePulseRing(echoRing, echoRadius, echoAlpha, echoWidth);
        }

        private LineRenderer CreatePulseRing(string ringName, float width)
        {
            var pulse = new GameObject(ringName);
            pulse.transform.SetParent(transform, false);
            var ring = pulse.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = PulseSegments;
            ring.startWidth = width;
            ring.endWidth = width;
            ring.sharedMaterial = pulseMaterial;
            UpdatePulseRing(ring, 0.25f, 0f, width);
            return ring;
        }

        private void UpdatePulseRing(LineRenderer ring, float radius, float ringAlpha, float width)
        {
            if (ring == null) return;

            ring.enabled = ringAlpha > 0.01f;
            ring.startWidth = width;
            ring.endWidth = width;
            for (int i = 0; i < PulseSegments; i++)
            {
                float angle = (Mathf.PI * 2f * i) / PulseSegments;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0.035f, Mathf.Sin(angle) * radius));
            }

            Color c = pulseColor;
            c.a *= ringAlpha;
            ring.startColor = c;
            ring.endColor = c;
        }

        private void ApplyAlpha(float alpha)
        {
            SetMaterialColor(poleMaterial, WithAlpha(poleColor, poleColor.a * alpha));
            SetMaterialColor(bannerMaterial, WithAlpha(bannerColor, bannerColor.a * alpha));
            SetMaterialColor(pulseMaterial, WithAlpha(pulseColor, pulseColor.a * alpha));

            if (glyph != null)
            {
                Color c = glyph.color;
                c.a = alpha;
                glyph.color = c;
            }
        }

        private void FaceCamera()
        {
            if (cam == null)
                cam = Camera.main;
            if (cam == null) return;

            Vector3 toCamera = cam.transform.position - transform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        }

        private static Mesh CreateBannerMesh()
        {
            var mesh = new Mesh();
            mesh.name = "CommandFlagBanner";
            mesh.vertices = new[]
            {
                new Vector3(0f, 0.2f, 0f),
                new Vector3(0.68f, 0.2f, 0f),
                new Vector3(0.52f, 0f, 0f),
                new Vector3(0.68f, -0.2f, 0f),
                new Vector3(0f, -0.2f, 0f),
            };
            mesh.triangles = new[]
            {
                0, 1, 2,
                0, 2, 4,
                4, 2, 3,
                2, 1, 0,
                4, 2, 0,
                3, 2, 4,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Color GetDefaultColor(CommandFlagKind markerKind)
        {
            switch (markerKind)
            {
                case CommandFlagKind.AttackMove:
                    return new Color(1f, 0.22f, 0.08f, 0.78f);
                case CommandFlagKind.Rally:
                    return new Color(1f, 0.78f, 0.18f, 0.82f);
                default:
                    return new Color(1f, 0.74f, 0.16f, 0.76f);
            }
        }

        private static Material CreateTransparentMaterial(Color color, bool alwaysOnTop = false)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            mat.SetOverrideTag("RenderType", "Transparent");
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            if (mat.HasProperty("_ZTest"))
            {
                mat.SetFloat("_ZTest", alwaysOnTop
                    ? (float)UnityEngine.Rendering.CompareFunction.Always
                    : (float)UnityEngine.Rendering.CompareFunction.LessEqual);
            }
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = alwaysOnTop ? 3100 : 3000;
            SetMaterialColor(mat, color);
            return mat;
        }

        private static void SetMaterialColor(Material mat, Color color)
        {
            if (mat == null) return;
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, color);
            if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, color);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static void DestroyCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
        }

        private void OnDestroy()
        {
            DestroyRuntimeMaterial(poleMaterial);
            DestroyRuntimeMaterial(bannerMaterial);
            DestroyRuntimeMaterial(pulseMaterial);
        }

        private static void DestroyRuntimeMaterial(Material mat)
        {
            if (mat == null) return;
            if (Application.isPlaying) Destroy(mat);
            else DestroyImmediate(mat);
        }
    }
}
