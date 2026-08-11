using UnityEngine;

namespace OpenEmpires
{
    /// <summary>
    /// Golden dust at the boundary of a healing aura. It is dormant by default and only
    /// emits a short pulse when the aura actually heals.
    ///
    /// Purely cosmetic. Lives entirely in the view layer and never feeds back into the
    /// simulation, so it cannot affect determinism.
    /// </summary>
    [DisallowMultipleComponent]
    public class HealAuraVisual : MonoBehaviour
    {
        private const string ChildName = "HealAuraDust";

        // Motes per world-unit of circumference. Keeps density even whether the ring is
        // the King's 9 or the Abbey's 12.
        private const float MotesPerUnit = 3.2f;
        private const float PulseMoteScale = 0.55f;

        private static Texture2D dustTexture;
        private static Material dustMaterial;

        private ParticleSystem dust;
        private float radius;

        /// <summary>
        /// Attaches (or re-targets) a dormant dust ring on <paramref name="parent"/>.
        /// Radius is in world units and stays correct even if the parent is scaled.
        /// </summary>
        public static HealAuraVisual Attach(Transform parent, float radius, Color tint)
        {
            if (parent == null || radius <= 0f) return null;

            var existing = parent.GetComponentInChildren<HealAuraVisual>(true);
            if (existing != null)
            {
                existing.SetRadius(radius);
                return existing;
            }

            var go = new GameObject(ChildName);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            go.transform.localRotation = Quaternion.identity;

            // Buildings are scaled to their footprint, so cancel the parent's scale out or
            // the ring would inherit it and misreport the real heal radius.
            Vector3 ls = parent.lossyScale;
            go.transform.localScale = new Vector3(
                Mathf.Approximately(ls.x, 0f) ? 1f : 1f / ls.x,
                Mathf.Approximately(ls.y, 0f) ? 1f : 1f / ls.y,
                Mathf.Approximately(ls.z, 0f) ? 1f : 1f / ls.z);

            var visual = go.AddComponent<HealAuraVisual>();
            visual.Build(radius, tint);
            return visual;
        }

        /// <summary>Removes the dust ring from a parent, if one is present.</summary>
        public static void Detach(Transform parent)
        {
            if (parent == null) return;
            var existing = parent.GetComponentInChildren<HealAuraVisual>(true);
            if (existing != null) Destroy(existing.gameObject);
        }

        public void SetRadius(float newRadius)
        {
            if (newRadius <= 0f || dust == null) return;
            radius = newRadius;

            var shape = dust.shape;
            shape.radius = radius;

            var main = dust.main;
            main.maxParticles = MaxParticlesFor(radius);
        }

        public void Pulse()
        {
            if (dust == null) return;

            if (!dust.isPlaying)
                dust.Play();

            dust.Emit(PulseCountFor(radius));
        }

        private static int PulseCountFor(float r)
        {
            return Mathf.RoundToInt(Mathf.Clamp(2f * Mathf.PI * r * MotesPerUnit * PulseMoteScale, 24f, 180f));
        }

        private static int MaxParticlesFor(float r)
        {
            return Mathf.RoundToInt(Mathf.Clamp(2f * Mathf.PI * r * MotesPerUnit * 2.5f, 120f, 900f));
        }

        private void Build(float r, Color tint)
        {
            radius = r;
            dust = gameObject.AddComponent<ParticleSystem>();
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = dust.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1.8f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.14f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.30f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = tint;
            main.gravityModifier = -0.015f; // motes rise instead of fall
            main.maxParticles = MaxParticlesFor(radius);

            var emission = dust.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            // A thin annulus, not a filled disc — this is what makes it read as an edge.
            var shape = dust.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = 0.10f;
            shape.arc = 360f;
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;
            shape.rotation = new Vector3(90f, 0f, 0f); // lay the circle flat on the ground

            var vol = dust.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.Local;
            vol.orbitalY = new ParticleSystem.MinMaxCurve(0.18f, 0.42f); // slow drift around the rim
            vol.radial = new ParticleSystem.MinMaxCurve(-0.05f, 0.06f);  // subtle breathing
            vol.y = new ParticleSystem.MinMaxCurve(0.10f, 0.30f);        // lift

            var col = dust.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1.00f, 0.93f, 0.68f), 0.00f),
                    new GradientColorKey(new Color(1.00f, 0.80f, 0.30f), 0.45f),
                    new GradientColorKey(new Color(0.85f, 0.55f, 0.12f), 1.00f)
                },
                new[]
                {
                    new GradientAlphaKey(0.00f, 0.00f),
                    new GradientAlphaKey(1.00f, 0.22f),
                    new GradientAlphaKey(0.75f, 0.65f),
                    new GradientAlphaKey(0.00f, 1.00f)
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var sol = dust.sizeOverLifetime;
            sol.enabled = true;
            var curve = new AnimationCurve();
            curve.AddKey(0.00f, 0.35f);
            curve.AddKey(0.35f, 1.00f);
            curve.AddKey(1.00f, 0.15f);
            sol.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var rot = dust.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);

            var psr = GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.alignment = ParticleSystemRenderSpace.View;
            psr.sharedMaterial = GetDustMaterial();
            psr.sortingFudge = -2f;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;
            psr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static Material GetDustMaterial()
        {
            if (dustMaterial != null) return dustMaterial;

            // Sprites/Default multiplies the texture by each particle's own colour, which is what
            // carries the gold. The URP particle shader was tried first and ignored colour entirely
            // in this project — every mote rendered flat white, and tinting the material made no
            // difference on screen.
            var shader = Shader.Find("Sprites/Default")
                      ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                      ?? Shader.Find("Universal Render Pipeline/Unlit");

            dustMaterial = new Material(shader) { name = "M_HealAuraDust" };

            var tex = GetDustTexture();
            if (dustMaterial.HasProperty("_BaseMap")) dustMaterial.SetTexture("_BaseMap", tex);
            if (dustMaterial.HasProperty("_MainTex")) dustMaterial.SetTexture("_MainTex", tex);
            if (dustMaterial.HasProperty("_BaseColor")) dustMaterial.SetColor("_BaseColor", Color.white);
            if (dustMaterial.HasProperty("_Color")) dustMaterial.SetColor("_Color", Color.white);

            // Additive and depth-write off, so motes glow and never punch holes in the scene.
            if (dustMaterial.HasProperty("_Surface")) dustMaterial.SetFloat("_Surface", 1f);
            if (dustMaterial.HasProperty("_Blend")) dustMaterial.SetFloat("_Blend", 1f);
            if (dustMaterial.HasProperty("_SrcBlend"))
                dustMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (dustMaterial.HasProperty("_DstBlend"))
                dustMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (dustMaterial.HasProperty("_ZWrite")) dustMaterial.SetFloat("_ZWrite", 0f);
            if (dustMaterial.HasProperty("_Cull"))
                dustMaterial.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

            dustMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            dustMaterial.DisableKeyword("_ALPHATEST_ON");
            dustMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            dustMaterial.renderQueue = 3100;
            return dustMaterial;
        }

        /// <summary>Soft round mote. A hard-edged quad would read as confetti, not dust.</summary>
        private static Texture2D GetDustTexture()
        {
            if (dustTexture != null) return dustTexture;

            const int size = 64;
            dustTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "T_HealAuraDust",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / c;
                    float dy = (y - c) / c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a); // smoothstep, then squared for a softer core
                    a *= a;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            dustTexture.SetPixels32(pixels);
            dustTexture.Apply(false, false);
            return dustTexture;
        }
    }
}
