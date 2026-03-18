using UnityEngine;

namespace OpenEmpires
{
    public class MarkerFader : MonoBehaviour
    {
        private float elapsed;
        private const float Lifetime = 1.5f;
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock propertyBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public bool Preview { get; set; }

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            propertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            elapsed = 0f;
            if (meshRenderer != null)
            {
                propertyBlock.SetColor(BaseColorId, new Color(1f, 1f, 1f, 0.75f));
                meshRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void Update()
        {
            if (Preview) return;

            elapsed += Time.deltaTime;
            float t = elapsed / Lifetime;
            if (t >= 1f)
            {
                gameObject.SetActive(false);
                return;
            }
            float alpha = Mathf.Lerp(0.75f, 0f, t);
            propertyBlock.SetColor(BaseColorId, new Color(1f, 1f, 1f, alpha));
            meshRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
