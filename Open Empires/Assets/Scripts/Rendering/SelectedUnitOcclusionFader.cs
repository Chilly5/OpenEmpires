using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenEmpires
{
    /// <summary>
    /// View-only camera occlusion aid for selected units. It fades world renderers that sit
    /// between the camera and selected units, then restores them when they stop blocking.
    /// </summary>
    public sealed class SelectedUnitOcclusionFader
    {
        private const float FadedAlpha = 0.38f;
        private const float FadeInSpeed = 10f;
        private const float FadeOutSpeed = 7f;
        private const float SampleInterval = 0.06f;
        private const float TargetHeight = 0.65f;
        private const float HitClearance = 0.25f;
        private const int MaxSampledUnits = 28;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");

        private readonly Dictionary<Renderer, FadeState> states = new Dictionary<Renderer, FadeState>();
        private readonly HashSet<Renderer> currentOccluders = new HashSet<Renderer>();
        private readonly HashSet<Renderer> sampledOccluders = new HashSet<Renderer>();
        private readonly List<Renderer> rendererBuffer = new List<Renderer>(16);
        private readonly List<Renderer> removalBuffer = new List<Renderer>(64);
        private readonly RaycastHit[] hitBuffer = new RaycastHit[128];

        private float nextSampleTime;

        public void Tick(Camera camera, IReadOnlyList<UnitView> selectedUnits, int occluderMask)
        {
            if (camera == null || selectedUnits == null || selectedUnits.Count == 0)
            {
                Clear();
                return;
            }

            if (Time.unscaledTime >= nextSampleTime)
            {
                nextSampleTime = Time.unscaledTime + SampleInterval;
                ResampleOccluders(camera, selectedUnits, occluderMask);
            }

            UpdateFadeStates();
        }

        public void Clear()
        {
            foreach (var kvp in states)
                kvp.Value.Restore();

            states.Clear();
            currentOccluders.Clear();
            sampledOccluders.Clear();
            removalBuffer.Clear();
        }

        private void ResampleOccluders(Camera camera, IReadOnlyList<UnitView> selectedUnits, int occluderMask)
        {
            sampledOccluders.Clear();

            int sampled = 0;
            for (int i = 0; i < selectedUnits.Count && sampled < MaxSampledUnits; i++)
            {
                UnitView unit = selectedUnits[i];
                if (unit == null || unit.IsDead || !unit.IsSelected) continue;

                Vector3 target = unit.transform.position + Vector3.up * TargetHeight;
                Vector3 screenPoint = camera.WorldToScreenPoint(target);
                if (screenPoint.z <= 0f ||
                    screenPoint.x < 0f || screenPoint.x > Screen.width ||
                    screenPoint.y < 0f || screenPoint.y > Screen.height)
                {
                    continue;
                }

                Ray ray = camera.ScreenPointToRay(screenPoint);
                float distanceToTarget = Vector3.Dot(target - ray.origin, ray.direction);
                if (distanceToTarget <= HitClearance) continue;

                int hitCount = Physics.RaycastNonAlloc(
                    ray,
                    hitBuffer,
                    distanceToTarget - HitClearance,
                    occluderMask,
                    QueryTriggerInteraction.Ignore);

                for (int h = 0; h < hitCount; h++)
                    AddOcclusionRenderers(hitBuffer[h].collider, hitBuffer[h].point);

                sampled++;
            }

            currentOccluders.Clear();
            foreach (Renderer renderer in sampledOccluders)
                currentOccluders.Add(renderer);
        }

        private void AddOcclusionRenderers(Collider collider, Vector3 hitPoint)
        {
            if (collider == null) return;

            rendererBuffer.Clear();

            var building = collider.GetComponentInParent<BuildingView>();
            if (building != null && !building.IsDestroyed)
            {
                building.GetOcclusionFadeRenderers(hitPoint, rendererBuffer);
            }
            else
            {
                var renderer = collider.GetComponentInParent<Renderer>();
                if (renderer != null)
                    rendererBuffer.Add(renderer);
                else
                    collider.GetComponentsInChildren<Renderer>(false, rendererBuffer);
            }

            for (int i = 0; i < rendererBuffer.Count; i++)
            {
                Renderer renderer = rendererBuffer[i];
                if (renderer == null || !renderer.enabled || renderer is ParticleSystemRenderer)
                    continue;

                sampledOccluders.Add(renderer);
            }
        }

        private void UpdateFadeStates()
        {
            foreach (Renderer renderer in currentOccluders)
            {
                if (renderer == null) continue;
                if (!states.ContainsKey(renderer))
                    states[renderer] = new FadeState(renderer);
            }

            removalBuffer.Clear();

            foreach (var kvp in states)
            {
                Renderer renderer = kvp.Key;
                FadeState state = kvp.Value;
                if (renderer == null)
                {
                    removalBuffer.Add(renderer);
                    continue;
                }

                float targetAlpha = currentOccluders.Contains(renderer) ? FadedAlpha : 1f;
                float speed = targetAlpha < state.Alpha ? FadeInSpeed : FadeOutSpeed;
                state.Alpha = Mathf.MoveTowards(state.Alpha, targetAlpha, Time.deltaTime * speed);
                state.Apply();

                if (Mathf.Approximately(state.Alpha, 1f) && !currentOccluders.Contains(renderer))
                {
                    state.Restore();
                    removalBuffer.Add(renderer);
                }
            }

            for (int i = 0; i < removalBuffer.Count; i++)
                states.Remove(removalBuffer[i]);
        }

        private sealed class FadeState
        {
            private readonly Renderer renderer;
            private readonly Material[] materials;
            private readonly MaterialSnapshot[] snapshots;

            public float Alpha { get; set; } = 1f;

            public FadeState(Renderer renderer)
            {
                this.renderer = renderer;
                materials = renderer.materials;
                snapshots = new MaterialSnapshot[materials.Length];

                for (int i = 0; i < materials.Length; i++)
                    snapshots[i] = new MaterialSnapshot(materials[i]);
            }

            public void Apply()
            {
                if (renderer == null) return;

                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null) continue;

                    snapshots[i].ConfigureTransparent(material);
                    Color color = snapshots[i].Color;
                    color.a *= Alpha;
                    SetMaterialColor(material, color);
                }
            }

            public void Restore()
            {
                if (renderer == null) return;

                for (int i = 0; i < materials.Length; i++)
                    snapshots[i].Restore(materials[i]);
            }
        }

        private readonly struct MaterialSnapshot
        {
            public readonly Color Color;
            private readonly int renderQueue;
            private readonly string renderType;
            private readonly bool alphaBlendKeyword;
            private readonly bool surfaceTransparentKeyword;
            private readonly bool hasSurface;
            private readonly bool hasSrcBlend;
            private readonly bool hasDstBlend;
            private readonly bool hasZWrite;
            private readonly bool hasAlphaClip;
            private readonly float surface;
            private readonly float srcBlend;
            private readonly float dstBlend;
            private readonly float zWrite;
            private readonly float alphaClip;

            public MaterialSnapshot(Material material)
            {
                Color = GetMaterialColor(material);
                renderQueue = material != null ? material.renderQueue : -1;
                renderType = material != null ? material.GetTag("RenderType", false, "") : "";
                alphaBlendKeyword = material != null && material.IsKeywordEnabled("_ALPHABLEND_ON");
                surfaceTransparentKeyword = material != null && material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT");

                hasSurface = material != null && material.HasProperty(SurfaceId);
                hasSrcBlend = material != null && material.HasProperty(SrcBlendId);
                hasDstBlend = material != null && material.HasProperty(DstBlendId);
                hasZWrite = material != null && material.HasProperty(ZWriteId);
                hasAlphaClip = material != null && material.HasProperty(AlphaClipId);

                surface = hasSurface ? material.GetFloat(SurfaceId) : 0f;
                srcBlend = hasSrcBlend ? material.GetFloat(SrcBlendId) : 0f;
                dstBlend = hasDstBlend ? material.GetFloat(DstBlendId) : 0f;
                zWrite = hasZWrite ? material.GetFloat(ZWriteId) : 1f;
                alphaClip = hasAlphaClip ? material.GetFloat(AlphaClipId) : 0f;
            }

            public void ConfigureTransparent(Material material)
            {
                if (material == null) return;

                if (hasSurface) material.SetFloat(SurfaceId, 1f);
                if (hasSrcBlend) material.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
                if (hasDstBlend) material.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
                if (hasZWrite) material.SetFloat(ZWriteId, 0f);
                if (hasAlphaClip) material.SetFloat(AlphaClipId, 0f);

                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            public void Restore(Material material)
            {
                if (material == null) return;

                SetMaterialColor(material, Color);
                if (hasSurface) material.SetFloat(SurfaceId, surface);
                if (hasSrcBlend) material.SetFloat(SrcBlendId, srcBlend);
                if (hasDstBlend) material.SetFloat(DstBlendId, dstBlend);
                if (hasZWrite) material.SetFloat(ZWriteId, zWrite);
                if (hasAlphaClip) material.SetFloat(AlphaClipId, alphaClip);

                material.SetOverrideTag("RenderType", renderType);
                SetKeyword(material, "_ALPHABLEND_ON", alphaBlendKeyword);
                SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", surfaceTransparentKeyword);
                material.renderQueue = renderQueue;
            }
        }

        private static Color GetMaterialColor(Material material)
        {
            if (material == null) return Color.white;
            if (material.HasProperty(BaseColorId)) return material.GetColor(BaseColorId);
            if (material.HasProperty(ColorId)) return material.GetColor(ColorId);
            return Color.white;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null) return;
            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, color);
            if (material.HasProperty(ColorId)) material.SetColor(ColorId, color);
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled) material.EnableKeyword(keyword);
            else material.DisableKeyword(keyword);
        }
    }
}
