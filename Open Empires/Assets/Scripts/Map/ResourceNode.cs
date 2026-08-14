using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public class ResourceNode : MonoBehaviour
    {
        public int ResourceNodeId { get; private set; }
        public bool IsSelected => isSelected;
        public bool IsGhostMode => isGhostMode;

        public static readonly List<ResourceNode> All = new List<ResourceNode>();

        private void Awake() { if (!All.Contains(this)) All.Add(this); }
        private void OnDestroy() { All.Remove(this); }

        private ResourceNodeData nodeData;
        private bool isSelected;
        private bool isGhostMode;
        private GameObject selectionRing;
        private GameObject billboardSprite;

        // Flash system (mirrors BuildingView)
        private Renderer[] bodyRenderers;
        private Color[] originalColors;
        private bool flashActive;
        private MaterialPropertyBlock propBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

        private int lastSeenDamageTick;
        private float damageFlashTimer;
        private const float DamageFlashDuration = 0.18f;

        private float commandFlashTimer;
        private const float CommandFlashDuration = 0.09f;
        private static readonly Color CommandFlashColor = Color.white;

        public void Initialize(int nodeId, ResourceNodeData data)
        {
            ResourceNodeId = nodeId;
            nodeData = data;
            CacheRenderers();
            FitColliderToFootprint();
            enabled = false;
        }

        public void SetBillboardSprite(GameObject sprite)
        {
            billboardSprite = sprite;
            CacheRenderers();
        }

        // Permanently destroys this view and its separately-parented billboard sprite (trees and
        // billboarded resources reparent their sprite onto a shared container, so destroying the
        // root alone leaves an orphan visible in the world).
        public void DestroyView()
        {
            if (billboardSprite != null)
            {
                Destroy(billboardSprite);
                billboardSprite = null;
            }
            Destroy(gameObject);
        }

        public Rect GetScreenBounds(Camera cam)
        {
            // Prefer the billboard sprite's renderer (for Gold/Stone/Food/Wood billboards),
            // fall back to body renderers, then to the collider for legacy 3D resource nodes.
            Bounds? worldBounds = null;
            if (billboardSprite != null)
            {
                var r = billboardSprite.GetComponent<Renderer>();
                if (r != null) worldBounds = r.bounds;
            }
            if (worldBounds == null && bodyRenderers != null && bodyRenderers.Length > 0)
            {
                Bounds b = bodyRenderers[0].bounds;
                for (int i = 1; i < bodyRenderers.Length; i++)
                    if (bodyRenderers[i] != null) b.Encapsulate(bodyRenderers[i].bounds);
                worldBounds = b;
            }
            if (worldBounds == null)
            {
                var col = GetComponent<Collider>();
                if (col != null) worldBounds = col.bounds;
            }
            if (worldBounds == null) return default;

            Vector3 center = worldBounds.Value.center;
            Vector3 ext = worldBounds.Value.extents;
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            for (int cx = -1; cx <= 1; cx += 2)
            for (int cy = -1; cy <= 1; cy += 2)
            for (int cz = -1; cz <= 1; cz += 2)
            {
                Vector3 corner = new Vector3(center.x + ext.x * cx, center.y + ext.y * cy, center.z + ext.z * cz);
                Vector3 sp = cam.WorldToScreenPoint(corner);
                if (sp.z < 0) return default;
                if (sp.x < minX) minX = sp.x;
                if (sp.x > maxX) maxX = sp.x;
                if (sp.y < minY) minY = sp.y;
                if (sp.y > maxY) maxY = sp.y;
            }
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private void FitColliderToFootprint()
        {
            if (nodeData == null || nodeData.FootprintWidth <= 1) return;

            var oldCol = GetComponent<Collider>();
            if (oldCol != null) Destroy(oldCol);

            var box = gameObject.AddComponent<BoxCollider>();
            float localCell = nodeData.FootprintWidth / transform.localScale.x;
            float boxHeight = nodeData.Type == ResourceType.Food ? localCell * 0.25f : localCell;
            box.size = new Vector3(localCell, boxHeight, localCell);
            box.center = nodeData.Type == ResourceType.Food
                ? new Vector3(0f, boxHeight * 0.5f, 0f)
                : new Vector3(0f, localCell * 0.5f, 0f);
        }

        private void CreateSelectionRing()
        {
            bool isLargeFootprint = nodeData != null && nodeData.FootprintWidth > 1;
            selectionRing = GameObject.CreatePrimitive(isLargeFootprint ? PrimitiveType.Cube : PrimitiveType.Cylinder);
            selectionRing.name = "SelectionRing";
            selectionRing.transform.SetParent(transform);
            selectionRing.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            float ringSize = isLargeFootprint
                ? nodeData.FootprintWidth / transform.localScale.x
                : 2.4f;
            selectionRing.transform.localScale = isLargeFootprint
                ? new Vector3(ringSize, 0.02f, ringSize)
                : new Vector3(2.4f, 0.01f, 2.4f);
            selectionRing.layer = 10; // Resource layer

            var ringCollider = selectionRing.GetComponent<Collider>();
            if (ringCollider != null) Object.Destroy(ringCollider);

            var ringMat = new Material(Shader.Find("Custom/SelectionRing"));
            ringMat.SetColor("_Color", new Color(0f, 1f, 0f, 0.5f));
            selectionRing.GetComponent<Renderer>().sharedMaterial = ringMat;

            selectionRing.SetActive(false);
        }

        private void CacheRenderers()
        {
            var renderers = new List<Renderer>();
            AddRenderers(GetComponentsInChildren<Renderer>(true), renderers);
            if (billboardSprite != null)
                AddRenderers(billboardSprite.GetComponentsInChildren<Renderer>(true), renderers);

            bodyRenderers = renderers.ToArray();
            originalColors = new Color[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                originalColors[i] = GetMaterialColor(bodyRenderers[i].sharedMaterial);

                // Disable shadows and probes for performance
                bodyRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                bodyRenderers[i].receiveShadows = false;
                bodyRenderers[i].lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                bodyRenderers[i].reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }

            propBlock = new MaterialPropertyBlock();
        }

        private void AddRenderers(Renderer[] source, List<Renderer> renderers)
        {
            if (source == null) return;

            for (int i = 0; i < source.Length; i++)
            {
                var renderer = source[i];
                if (renderer == null || renderers.Contains(renderer)) continue;
                if (selectionRing != null && renderer.transform.IsChildOf(selectionRing.transform)) continue;
                renderers.Add(renderer);
            }
        }

        private static Color GetMaterialColor(Material mat)
        {
            if (mat == null) return Color.white;
            if (mat.HasProperty(BaseColorId)) return mat.GetColor(BaseColorId);
            if (mat.HasProperty(ColorId)) return mat.GetColor(ColorId);
            return Color.white;
        }

        private static void SetMaterialColor(Material mat, Color color)
        {
            if (mat == null) return;
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, color);
            else if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, color);
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            if (selected && selectionRing == null)
                CreateSelectionRing();
            if (selectionRing != null)
                selectionRing.SetActive(selected);
            if (selected)
                FlashCommandConfirm();
        }

        public ResourceNodeData GetNodeData()
        {
            return nodeData;
        }

        public void FlashCommandConfirm()
        {
            if (bodyRenderers == null || bodyRenderers.Length == 0)
                CacheRenderers();
            commandFlashTimer = CommandFlashDuration;
            enabled = true;
        }

        public void SetGhostMode(bool ghost)
        {
            if (isGhostMode == ghost) return;
            isGhostMode = ghost;

            if (ghost)
            {
                for (int i = 0; i < bodyRenderers.Length; i++)
                {
                    if (bodyRenderers[i] == null) continue;
                    var mat = bodyRenderers[i].material;
                    mat.SetFloat("_Surface", 1);
                    mat.SetFloat("_Blend", 0);
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.renderQueue = 3000;
                    Color c = originalColors[i];
                    c.a = 0.4f;
                    SetMaterialColor(mat, c);
                }
                var col = GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }
            else
            {
                for (int i = 0; i < bodyRenderers.Length; i++)
                {
                    if (bodyRenderers[i] == null) continue;
                    var mat = bodyRenderers[i].material;
                    mat.SetFloat("_Surface", 0);
                    mat.SetOverrideTag("RenderType", "Opaque");
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    mat.SetInt("_ZWrite", 1);
                    mat.DisableKeyword("_ALPHABLEND_ON");
                    mat.renderQueue = -1;
                    SetMaterialColor(mat, originalColors[i]);
                }
                var col = GetComponent<Collider>();
                if (col != null) col.enabled = true;
            }
        }

        private void Update()
        {
            if (nodeData == null || isGhostMode) return;
            UpdateFlash();
        }

        private void UpdateFlash()
        {
            // Command flash takes priority over gather-hit flash.
            if (commandFlashTimer > 0f)
            {
                if (!flashActive)
                {
                    flashActive = true;
                    SetFlashColor(CommandFlashColor);
                }
                commandFlashTimer -= Time.deltaTime;
            }
            else if (damageFlashTimer > 0f)
            {
                if (!flashActive)
                {
                    flashActive = true;
                    SetFlashColor(Color.white);
                }
                damageFlashTimer -= Time.deltaTime;
            }
            else if (flashActive)
            {
                flashActive = false;
                ClearFlashColor();
                enabled = false;
            }
        }

        private void SetFlashColor(Color color)
        {
            if (propBlock == null)
                propBlock = new MaterialPropertyBlock();

            propBlock.Clear();
            propBlock.SetColor(BaseColorId, color);
            propBlock.SetColor(ColorId, color);
            propBlock.SetColor(FlashColorId, color);
            propBlock.SetFloat(FlashAmountId, 1f);
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] != null)
                    bodyRenderers[i].SetPropertyBlock(propBlock);
            }
        }

        private void ClearFlashColor()
        {
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] != null)
                    bodyRenderers[i].SetPropertyBlock(null);
            }
        }

        public void SyncFromSim(ResourceNodeData data)
        {
            if (data == null || data.IsDepleted)
            {
                gameObject.SetActive(false);
                if (billboardSprite != null)
                {
                    billboardSprite.SetActive(false);
                    billboardSprite = null;
                }
                return;
            }

            // Detect new gather strike — enable Update for flash
            if (data.LastDamageTick > lastSeenDamageTick && data.LastDamageTick > 0)
            {
                lastSeenDamageTick = data.LastDamageTick;
                damageFlashTimer = DamageFlashDuration;
                enabled = true;
            }
        }
    }
}
