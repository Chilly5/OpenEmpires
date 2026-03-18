using UnityEngine;
using UnityEngine.UI;

namespace OpenEmpires
{
    public class SelectionBoxUI : MonoBehaviour
    {
        [SerializeField] private UnitSelectionManager selectionManager;
        [SerializeField] private Color boxColor = new Color(0.2f, 0.8f, 0.2f, 0.25f);
        [SerializeField] private Color borderColor = new Color(0.2f, 0.8f, 0.2f, 0.8f);

        private GameObject boxRoot;
        private RectTransform fillRT;
        private RectTransform borderTop, borderBottom, borderLeft, borderRight;

        private const float BorderWidth = 2f;

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

        private void Update()
        {
            if (selectionManager == null || !selectionManager.IsDragging)
            {
                if (boxRoot.activeSelf) boxRoot.SetActive(false);
                return;
            }

            if (!boxRoot.activeSelf) boxRoot.SetActive(true);

            // DragStart/DragEnd are in screen coords (Y-up), which matches ScreenSpaceOverlay
            Vector2 start = selectionManager.DragStart;
            Vector2 end = selectionManager.DragEnd;

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
