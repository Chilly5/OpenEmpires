using UnityEngine;

namespace OpenEmpires
{
    public class ResourceNode : MonoBehaviour
    {
        public int ResourceNodeId { get; private set; }
        public bool IsSelected => isSelected;
        public bool IsGhostMode => isGhostMode;

        private ResourceNodeData nodeData;
        private bool isSelected;
        private bool isGhostMode;
        private GameObject selectionRing;

        // Flash system (mirrors BuildingView)
        private Renderer[] bodyRenderers;
        private Color[] originalColors;
        private bool flashActive;
        private MaterialPropertyBlock propBlock;

        private int lastSeenDamageTick;
        private float damageFlashTimer;
        private const float DamageFlashDuration = 0.18f;

        private float commandFlashTimer;
        private const float CommandFlashDuration = 0.18f;
        private static readonly Color FlashColor = new Color(0.2f, 1f, 0.2f);

        public void Initialize(int nodeId, ResourceNodeData data)
        {
            ResourceNodeId = nodeId;
            nodeData = data;
            CacheRenderers();
            FitColliderToFootprint();
            enabled = false;
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
            bodyRenderers = GetComponentsInChildren<Renderer>(true);
            originalColors = new Color[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                originalColors[i] = bodyRenderers[i].sharedMaterial != null
                    ? bodyRenderers[i].sharedMaterial.color
                    : Color.white;

                // Disable shadows and probes for performance
                bodyRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                bodyRenderers[i].receiveShadows = false;
                bodyRenderers[i].lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                bodyRenderers[i].reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }

            propBlock = new MaterialPropertyBlock();
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            if (selected && selectionRing == null)
                CreateSelectionRing();
            if (selectionRing != null)
                selectionRing.SetActive(selected);
        }

        public ResourceNodeData GetNodeData()
        {
            return nodeData;
        }

        public void FlashCommandConfirm()
        {
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
                    mat.color = c;
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
                    mat.color = originalColors[i];
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
            // Command flash takes priority over gather flash
            if (commandFlashTimer > 0f)
            {
                if (!flashActive)
                {
                    flashActive = true;
                    propBlock.SetColor("_BaseColor", FlashColor);
                    for (int i = 0; i < bodyRenderers.Length; i++)
                    {
                        if (bodyRenderers[i] != null)
                            bodyRenderers[i].SetPropertyBlock(propBlock);
                    }
                }
                commandFlashTimer -= Time.deltaTime;
            }
            else if (damageFlashTimer > 0f)
            {
                if (!flashActive)
                {
                    flashActive = true;
                    propBlock.SetColor("_BaseColor", Color.white);
                    for (int i = 0; i < bodyRenderers.Length; i++)
                    {
                        if (bodyRenderers[i] != null)
                            bodyRenderers[i].SetPropertyBlock(propBlock);
                    }
                }
                damageFlashTimer -= Time.deltaTime;
            }
            else if (flashActive)
            {
                flashActive = false;
                for (int i = 0; i < bodyRenderers.Length; i++)
                {
                    if (bodyRenderers[i] != null)
                        bodyRenderers[i].SetPropertyBlock(null);
                }
                enabled = false;
            }
        }

        public void SyncFromSim(ResourceNodeData data)
        {
            if (data == null || data.IsDepleted)
            {
                gameObject.SetActive(false);
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
