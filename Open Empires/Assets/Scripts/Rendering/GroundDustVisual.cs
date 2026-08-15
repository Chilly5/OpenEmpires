using UnityEngine;

namespace OpenEmpires
{
    /// <summary>
    /// Ground dust kicked up by hooves and boots. A single world-space particle system serves
    /// every moving unit on the map, so a bigger army costs emissions rather than components.
    ///
    /// Purely cosmetic. Lives entirely in the view layer and never feeds back into the
    /// simulation, so it cannot affect determinism.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GroundDustVisual : MonoBehaviour
    {
        // Sized for a whole army on the move, not just the cavalry: every unit on foot now
        // contributes puffs, and running dry would silently starve the hoof dust as well.
        private const int MaxParticles = 1536;
        private const int TextureSize = 32;

        private static GroundDustVisual instance;
        private static Texture2D dustTexture;
        private static Material dustMaterial;

        private ParticleSystem dust;
        private ParticleSystem.EmitParams emitParams;

        /// <summary>
        /// Puffs dust at a point on the ground. <paramref name="strength"/> is 0..1 and scales
        /// both how many motes appear and how hard they are thrown.
        /// </summary>
        public static void Burst(Vector3 groundPosition, float strength)
        {
            if (strength <= 0f) return;

            GroundDustVisual visual = Ensure();
            if (visual != null)
                visual.EmitBurst(groundPosition, Mathf.Clamp01(strength));
        }

        private static GroundDustVisual Ensure()
        {
            if (instance != null) return instance;

            var go = new GameObject("GroundDust");
            instance = go.AddComponent<GroundDustVisual>();
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            Build();
        }

        private void EmitBurst(Vector3 groundPosition, float strength)
        {
            if (dust == null) return;

            int count = Mathf.Max(2, Mathf.RoundToInt(Mathf.Lerp(2f, 7f, strength)));

            // Lifted clear of the ground: a billboard centred exactly on the terrain surface has
            // half of itself buried, which cost most of the puff before it was ever drawn.
            groundPosition.y += 0.06f;

            emitParams.position = groundPosition;
            emitParams.applyShapeToPosition = true;
            emitParams.startSize = Mathf.Lerp(0.28f, 0.60f, strength);
            emitParams.startLifetime = Mathf.Lerp(0.75f, 1.35f, strength);

            dust.Emit(emitParams, count);
        }

        private void Build()
        {
            dust = gameObject.AddComponent<ParticleSystem>();
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = dust.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 1f;
            // World space so a puff stays on the ground the horse has already left behind.
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            // Sized to read from an RTS camera some fifteen-plus units up, not from a close
            // third-person view: motes that looked right up close vanished entirely at play distance.
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.75f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.60f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.80f, 1.40f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.05f; // grit settles back down, but hangs long enough to be seen
            main.maxParticles = MaxParticles;

            var emission = dust.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f; // burst-only, driven by Emit

            // A shallow flat cone: dust sprays outward and low, not up in a column.
            var shape = dust.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 62f;
            shape.radius = 0.05f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var col = dust.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.78f, 0.70f, 0.56f), 0.00f),
                    new GradientColorKey(new Color(0.66f, 0.58f, 0.45f), 1.00f)
                },
                new[]
                {
                    new GradientAlphaKey(0.00f, 0.00f),
                    new GradientAlphaKey(0.80f, 0.18f),
                    new GradientAlphaKey(0.45f, 0.60f),
                    new GradientAlphaKey(0.00f, 1.00f)
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var sol = dust.sizeOverLifetime;
            sol.enabled = true;
            var curve = new AnimationCurve();
            curve.AddKey(0.00f, 0.50f);
            curve.AddKey(1.00f, 1.80f); // puffs bloom outward as they fade
            sol.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var psr = GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.alignment = ParticleSystemRenderSpace.View;
            psr.sharedMaterial = GetDustMaterial();
            psr.sortingFudge = -1f;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;
            psr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

            dust.Play();
        }

        private static Material GetDustMaterial()
        {
            if (dustMaterial != null) return dustMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                      ?? Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Sprites/Default");

            dustMaterial = new Material(shader) { name = "M_GroundDust" };

            Texture2D tex = GetDustTexture();
            if (dustMaterial.HasProperty("_BaseMap")) dustMaterial.SetTexture("_BaseMap", tex);
            if (dustMaterial.HasProperty("_MainTex")) dustMaterial.SetTexture("_MainTex", tex);
            if (dustMaterial.HasProperty("_BaseColor")) dustMaterial.SetColor("_BaseColor", Color.white);
            if (dustMaterial.HasProperty("_Color")) dustMaterial.SetColor("_Color", Color.white);

            // Alpha-blended (not additive — dirt occludes, it does not glow) with depth write off.
            if (dustMaterial.HasProperty("_Surface")) dustMaterial.SetFloat("_Surface", 1f);
            if (dustMaterial.HasProperty("_Blend")) dustMaterial.SetFloat("_Blend", 0f);
            if (dustMaterial.HasProperty("_SrcBlend"))
                dustMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (dustMaterial.HasProperty("_DstBlend"))
                dustMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (dustMaterial.HasProperty("_ZWrite")) dustMaterial.SetFloat("_ZWrite", 0f);
            if (dustMaterial.HasProperty("_Cull"))
                dustMaterial.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

            dustMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            dustMaterial.DisableKeyword("_ALPHATEST_ON");
            dustMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            dustMaterial.renderQueue = 3050;

            return dustMaterial;
        }

        /// <summary>A soft round blob, so each mote reads as a smudge rather than a square.</summary>
        private static Texture2D GetDustTexture()
        {
            if (dustTexture != null) return dustTexture;

            dustTexture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "T_GroundDust",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            const float center = (TextureSize - 1) * 0.5f;
            var pixels = new Color32[TextureSize * TextureSize];

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    falloff *= falloff;
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)(falloff * 255f));
                }
            }

            dustTexture.SetPixels32(pixels);
            dustTexture.Apply();
            return dustTexture;
        }
    }
}
