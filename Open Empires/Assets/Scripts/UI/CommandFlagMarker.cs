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
        private const float FallHeight = 12f;
        private const float FallDuration = 0.28f;
        private const int PulseSegments = 40;
        private const float BannerWaveSpeed = 9.5f;
        private const float BannerWaveSpacing = 7.5f;
        private const float BannerWaveAmplitude = 0.045f;
        private const float RippleRadiusScale = 1.5f;
        private const float RippleEchoDelay = 0.11f;
        private const float RippleDuration = 0.68f;
        private const float EchoRippleDuration = 0.72f;
        private const float Lifetime = FallDuration + RippleEchoDelay + EchoRippleDuration;
        private const float FadeStartTime = FallDuration + 0.35f;
        private const float ImpactBounceDecay = 14f;
        private const float ImpactRootDecay = 9f;
        private const float ImpactClothDecay = 8f;
        private const float PersistentRippleDuration = RippleEchoDelay + EchoRippleDuration;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private CommandFlagKind kind;
        private bool persistent;
        private bool persistentDropPulseActive;
        private bool initialized;
        private float elapsed;
        private Vector3 landingPosition;
        private Camera cam;

        private Material poleMaterial;
        private Material bannerMaterial;
        private Material pulseMaterial;
        private Transform flagRoot;
        private MeshRenderer poleRenderer;
        private MeshRenderer bannerRenderer;
        private Mesh bannerMesh;
        private Vector3[] bannerBaseVertices;
        private Vector3[] bannerAnimatedVertices;
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
                persistentDropPulseActive = persistent;
                transform.position = landingPosition + Vector3.up * FallHeight;
            }
            else if (!persistent || !persistentDropPulseActive || elapsed >= FallDuration)
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
                if (!persistentDropPulseActive || elapsed >= FallDuration)
                {
                    elapsed = FallDuration;
                    transform.position = landingPosition;
                }
                gameObject.SetActive(true);
            }
            else if (!visible && gameObject.activeSelf)
            {
                persistentDropPulseActive = false;
                HidePulseRings();
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

            flagRoot = new GameObject("FlagVisuals").transform;
            flagRoot.SetParent(transform, false);

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(flagRoot, false);
            pole.transform.localPosition = new Vector3(0f, 0.52f, 0f);
            pole.transform.localScale = new Vector3(0.035f, 0.52f, 0.035f);
            DestroyCollider(pole);
            poleRenderer = pole.GetComponent<MeshRenderer>();
            poleRenderer.sharedMaterial = poleMaterial;
            poleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            poleRenderer.receiveShadows = false;

            var banner = new GameObject("Banner");
            banner.transform.SetParent(flagRoot, false);
            banner.transform.localPosition = new Vector3(0.015f, 0.76f, 0f);
            bannerMesh = CreateBannerMesh();
            bannerBaseVertices = bannerMesh.vertices;
            bannerAnimatedVertices = new Vector3[bannerBaseVertices.Length];
            banner.AddComponent<MeshFilter>().sharedMesh = bannerMesh;
            bannerRenderer = banner.AddComponent<MeshRenderer>();
            bannerRenderer.sharedMaterial = bannerMaterial;
            bannerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bannerRenderer.receiveShadows = false;

            if (kind == CommandFlagKind.AttackMove)
                CreateGlyph("A", new Color(0.18f, 0.03f, 0.02f, 0.95f));

            if (!(persistent && kind == CommandFlagKind.Rally))
            {
                pulseRing = CreatePulseRing("Pulse", 0.055f);
                echoRing = CreatePulseRing("EchoPulse", 0.035f);
            }
        }

        private void CreateGlyph(string text, Color color)
        {
            var glyphGO = new GameObject("Glyph");
            glyphGO.transform.SetParent(flagRoot != null ? flagRoot : transform, false);
            glyphGO.transform.localPosition = new Vector3(0.325f, 0.76f, 0.02f);
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
            UpdateFlagRootMotion();
            UpdateBannerWave();
            UpdatePulse();
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
            float decay = Mathf.Exp(-ImpactBounceDecay * settledTime);
            float bounce = Mathf.Max(0f, Mathf.Sin(settledTime * 22f) * 0.18f * decay);
            float scalePunch = 1f + Mathf.Sin(settledTime * 26f) * 0.08f * decay;
            transform.position = landingPosition + Vector3.up * bounce;
            transform.localScale = Vector3.one * scalePunch;
        }

        private void UpdateFlagRootMotion()
        {
            if (flagRoot == null) return;

            if (persistent)
            {
                float idleLean = Mathf.Sin(Time.time * 1.7f) * 1.4f;
                flagRoot.localRotation = Quaternion.Euler(0f, 0f, idleLean);
                flagRoot.localScale = Vector3.one;
                return;
            }

            float fallT = Mathf.Clamp01(elapsed / FallDuration);
            float fallLean = elapsed < FallDuration ? Mathf.Lerp(-11f, -2f, fallT) : 0f;
            float impactAge = elapsed - FallDuration;
            float impactLean = 0f;
            float squash = 0f;

            if (impactAge >= 0f)
            {
                float decay = Mathf.Exp(-ImpactRootDecay * impactAge);
                impactLean = Mathf.Sin(impactAge * 26f) * 12f * decay;
                squash = Mathf.Sin(impactAge * 31f) * 0.045f * decay;
            }

            flagRoot.localRotation = Quaternion.Euler(0f, 0f, fallLean + impactLean);
            flagRoot.localScale = new Vector3(1f + squash, 1f - squash * 0.6f, 1f);
        }

        private void UpdateBannerWave()
        {
            if (bannerMesh == null || bannerBaseVertices == null || bannerAnimatedVertices == null) return;

            float fallT = Mathf.Clamp01(elapsed / FallDuration);
            float fallLag = !persistent && elapsed < FallDuration
                ? Mathf.Pow(1f - fallT, 0.7f)
                : 0f;
            float impactAge = elapsed - FallDuration;
            float impactDecay = !persistent && impactAge >= 0f ? Mathf.Exp(-ImpactClothDecay * impactAge) : 0f;
            float impactSnap = Mathf.Sin(impactAge * 28f) * impactDecay;
            float impactCurl = Mathf.Cos(impactAge * 21f) * impactDecay;

            float time = (Time.time + landingPosition.x * 0.13f + landingPosition.z * 0.07f) * BannerWaveSpeed;
            float windRise = Mathf.Sin(Time.time * 2.1f) * 0.01f;

            for (int i = 0; i < bannerBaseVertices.Length; i++)
            {
                Vector3 vertex = bannerBaseVertices[i];
                float tether = Mathf.Clamp01(vertex.x / 0.68f);
                float freeEdge = tether * tether;
                float waveAmplitude = BannerWaveAmplitude + fallLag * 0.08f + Mathf.Abs(impactSnap) * 0.07f;
                float wave = Mathf.Sin(time - tether * BannerWaveSpacing) * waveAmplitude * freeEdge;

                vertex.z += wave - fallLag * 0.12f * freeEdge + impactCurl * 0.08f * freeEdge;
                vertex.y += windRise * tether + fallLag * 0.12f * freeEdge - impactSnap * 0.14f * freeEdge;
                bannerAnimatedVertices[i] = vertex;
            }

            bannerMesh.vertices = bannerAnimatedVertices;
            bannerMesh.RecalculateNormals();
            bannerMesh.RecalculateBounds();
        }

        private void UpdatePulse()
        {
            if (pulseRing == null) return;

            if (persistent && !persistentDropPulseActive)
            {
                HidePulseRings();
                return;
            }

            float pulseAge = elapsed - FallDuration;
            if (persistent && pulseAge > PersistentRippleDuration)
            {
                persistentDropPulseActive = false;
                HidePulseRings();
                return;
            }

            float t = Mathf.Clamp01(pulseAge / RippleDuration);
            float easedRadius = 1f - Mathf.Pow(1f - t, 3f);
            float ringAlpha = 0.68f * (1f - t);
            float ringWidth = Mathf.Lerp(0.095f, 0.012f, t);
            UpdatePulseRing(pulseRing, Mathf.Lerp(0.18f, 1.18f, easedRadius) * RippleRadiusScale,
                ringAlpha, ringWidth);

            if (echoRing == null) return;

            float echoAge = pulseAge - RippleEchoDelay;
            if (echoAge < 0f)
            {
                echoRing.enabled = false;
                return;
            }

            float echoT = Mathf.Clamp01(echoAge / EchoRippleDuration);
            float echoRadius = Mathf.SmoothStep(0.28f, 1.35f, echoT) * RippleRadiusScale;
            float echoAlpha = 0.26f * (1f - echoT);
            float echoWidth = Mathf.Lerp(0.05f, 0.008f, echoT);
            UpdatePulseRing(echoRing, echoRadius, echoAlpha, echoWidth);
        }

        private void HidePulseRings()
        {
            if (pulseRing != null)
                pulseRing.enabled = false;
            if (echoRing != null)
                echoRing.enabled = false;
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
            const int columns = 6;
            var vertices = new Vector3[columns * 3];

            for (int c = 0; c < columns; c++)
            {
                float t = c / (float)(columns - 1);
                float x = 0.68f * t;
                float sag = Mathf.Sin(t * Mathf.PI);
                int index = c * 3;

                vertices[index] = new Vector3(x, Mathf.Lerp(0.2f, 0.15f, t) - sag * 0.015f, 0f);
                vertices[index + 1] = new Vector3(c == columns - 1 ? 0.52f : x, Mathf.Lerp(-0.01f, -0.05f, t), 0f);
                vertices[index + 2] = new Vector3(x, Mathf.Lerp(-0.22f, -0.26f, t) - sag * 0.025f, 0f);
            }

            var triangles = new int[(columns - 1) * 24];
            int tri = 0;
            for (int c = 0; c < columns - 1; c++)
            {
                int aTop = c * 3;
                int aMid = aTop + 1;
                int aBottom = aTop + 2;
                int bTop = (c + 1) * 3;
                int bMid = bTop + 1;
                int bBottom = bTop + 2;

                triangles[tri++] = aTop;
                triangles[tri++] = bTop;
                triangles[tri++] = bMid;
                triangles[tri++] = aTop;
                triangles[tri++] = bMid;
                triangles[tri++] = aMid;

                triangles[tri++] = aMid;
                triangles[tri++] = bMid;
                triangles[tri++] = bBottom;
                triangles[tri++] = aMid;
                triangles[tri++] = bBottom;
                triangles[tri++] = aBottom;
            }

            int frontIndexCount = tri;
            for (int i = 0; i < frontIndexCount; i += 3)
            {
                triangles[tri++] = triangles[i + 2];
                triangles[tri++] = triangles[i + 1];
                triangles[tri++] = triangles[i];
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
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
            DestroyRuntimeObject(bannerMesh);
        }

        private static void DestroyRuntimeMaterial(Material mat)
        {
            if (mat == null) return;
            if (Application.isPlaying) Destroy(mat);
            else DestroyImmediate(mat);
        }

        private static void DestroyRuntimeObject(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
