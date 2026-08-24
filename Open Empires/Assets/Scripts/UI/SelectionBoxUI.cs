using UnityEngine;
using UnityEngine.UI;

namespace OpenEmpires
{
    public class SelectionBoxUI : MonoBehaviour
    {
        [SerializeField] private UnitSelectionManager selectionManager;
        [SerializeField] private Color boxColor = Color.white;
        [SerializeField] private Color borderColor = new Color(1f, 1f, 1f, 0.65f);

        private GameObject boxRoot;
        private RectTransform fillRT;
        private RectTransform borderTop, borderBottom, borderLeft, borderRight;

        private const float BorderWidth = 2f;
        private const int EdgeSpriteSize = 32;
        private const int EdgeSpriteBorder = 6;
        private const float EdgeAlpha = 0.28f;
        private const float CenterAlpha = 0.03f;

        private static Sprite edgeGradientSprite;

        private void Awake()
        {
            // Canvas — pixel coords, no scaler
            var canvasGO = new GameObject("SelectionBoxCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 8;

            boxRoot = new GameObject("BoxRoot");
            boxRoot.transform.SetParent(canvasGO.transform, false);

            // Fill
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(boxRoot.transform, false);
            fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.pivot = new Vector2(0f, 0f);
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.sprite = GetEdgeGradientSprite();
            fillImg.type = Image.Type.Sliced;
            fillImg.color = boxColor;
            fillImg.raycastTarget = false;

            // Borders
            borderTop = CreateBorder("Top", boxRoot.transform);
            borderBottom = CreateBorder("Bottom", boxRoot.transform);
            borderLeft = CreateBorder("Left", boxRoot.transform);
            borderRight = CreateBorder("Right", boxRoot.transform);

            boxRoot.SetActive(false);
        }

        private RectTransform CreateBorder(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.pivot = new Vector2(0f, 0f);
            var img = go.AddComponent<Image>();
            img.color = borderColor;
            img.raycastTarget = false;
            return rt;
        }

        private static Sprite GetEdgeGradientSprite()
        {
            if (edgeGradientSprite != null) return edgeGradientSprite;

            var texture = new Texture2D(EdgeSpriteSize, EdgeSpriteSize, TextureFormat.RGBA32, false)
            {
                name = "SelectionBox_EdgeGradient",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color[EdgeSpriteSize * EdgeSpriteSize];
            for (int y = 0; y < EdgeSpriteSize; y++)
            {
                for (int x = 0; x < EdgeSpriteSize; x++)
                {
                    int edgeDistance = Mathf.Min(
                        Mathf.Min(x, y),
                        Mathf.Min(EdgeSpriteSize - 1 - x, EdgeSpriteSize - 1 - y));

                    float alpha = CenterAlpha;
                    if (edgeDistance < EdgeSpriteBorder)
                    {
                        float t = edgeDistance / (float)(EdgeSpriteBorder - 1);
                        alpha = Mathf.Lerp(EdgeAlpha, CenterAlpha, Mathf.SmoothStep(0f, 1f, t));
                    }

                    pixels[y * EdgeSpriteSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);

            edgeGradientSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, EdgeSpriteSize, EdgeSpriteSize),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(EdgeSpriteBorder, EdgeSpriteBorder, EdgeSpriteBorder, EdgeSpriteBorder));
            edgeGradientSprite.name = "SelectionBox_EdgeGradient";
            edgeGradientSprite.hideFlags = HideFlags.HideAndDontSave;

            return edgeGradientSprite;
        }

        private void Update()
        {
            bool unitDrag = selectionManager != null && selectionManager.IsDragging;
            bool wallDrag = selectionManager != null && selectionManager.IsWallBoxDragging;

            if (!unitDrag && !wallDrag)
            {
                if (boxRoot.activeSelf) boxRoot.SetActive(false);
                return;
            }

            if (!boxRoot.activeSelf) boxRoot.SetActive(true);

            // DragStart/DragEnd are in screen coords (Y-up), which matches ScreenSpaceOverlay
            Vector2 start = unitDrag ? selectionManager.DragStart : selectionManager.WallBoxDragStartScreen;
            Vector2 end = unitDrag ? selectionManager.DragEnd : selectionManager.WallBoxDragEndScreen;

            float x = Mathf.Min(start.x, end.x);
            float y = Mathf.Min(start.y, end.y);
            float w = Mathf.Abs(end.x - start.x);
            float h = Mathf.Abs(end.y - start.y);

            // Fill
            fillRT.position = new Vector3(x, y, 0f);
            fillRT.sizeDelta = new Vector2(w, h);

            // Top
            borderTop.position = new Vector3(x, y + h - BorderWidth, 0f);
            borderTop.sizeDelta = new Vector2(w, BorderWidth);

            // Bottom
            borderBottom.position = new Vector3(x, y, 0f);
            borderBottom.sizeDelta = new Vector2(w, BorderWidth);

            // Left
            borderLeft.position = new Vector3(x, y, 0f);
            borderLeft.sizeDelta = new Vector2(BorderWidth, h);

            // Right
            borderRight.position = new Vector3(x + w - BorderWidth, y, 0f);
            borderRight.sizeDelta = new Vector2(BorderWidth, h);
        }
    }
}
