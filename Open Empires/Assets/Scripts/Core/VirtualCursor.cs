using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace OpenEmpires
{
    public class VirtualCursor : MonoBehaviour
    {
        private static VirtualCursor instance;
        private static bool settingsMenuOpen;

        public static Vector2 Position { get; private set; }

        public static void SetSettingsMenuOpen(bool value) { settingsMenuOpen = value; }

        private Canvas cursorCanvas;
        private RawImage cursorImage;
        private Texture2D cursorTexture;

        // Manual UI raycasting state (used when pointer is locked)
        private GameObject lastHover;
        private GameObject pressTarget;
        private PointerEventData cachedPointerData;
        private List<RaycastResult> raycastResults = new List<RaycastResult>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (instance != null) return;

            var go = new GameObject("VirtualCursor");
            instance = go.AddComponent<VirtualCursor>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            // Initialize position to center of screen
            Position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            // Load the same cursor texture used by CustomCursor
            cursorTexture = Resources.Load<Texture2D>("ResourceIcons/cursoricon");

            CreateSoftwareCursor();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void CreateSoftwareCursor()
        {
            // Create a Screen Space Overlay canvas for the software cursor
            var canvasGo = new GameObject("VirtualCursorCanvas");
            canvasGo.transform.SetParent(transform);
            cursorCanvas = canvasGo.AddComponent<Canvas>();
            cursorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            cursorCanvas.sortingOrder = 32767; // Always on top

            var imageGo = new GameObject("CursorImage");
            imageGo.transform.SetParent(canvasGo.transform, false);
            cursorImage = imageGo.AddComponent<RawImage>();
            cursorImage.raycastTarget = false;

            if (cursorTexture != null)
            {
                cursorImage.texture = cursorTexture;
                cursorImage.rectTransform.sizeDelta = new Vector2(64, 64);
            }
            else
            {
                // Fallback: small white arrow
                var fallback = new Texture2D(16, 16);
                var pixels = new Color[16 * 16];
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                        pixels[y * 16 + x] = (x < 16 - y) ? Color.white : Color.clear;
                fallback.SetPixels(pixels);
                fallback.Apply();
                cursorImage.texture = fallback;
                cursorImage.rectTransform.sizeDelta = new Vector2(16, 16);
            }

            // Pivot at top-left so the hotspot is the tip
            cursorImage.rectTransform.pivot = new Vector2(0f, 1f);

            // Cursor is visible immediately — VirtualCursor is the primary cursor
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            bool locked = Cursor.lockState == CursorLockMode.Locked;

            if (locked)
            {
                // Under pointer lock, accumulate deltas
                Vector2 delta = mouse.delta.ReadValue();
                Vector2 pos = Position + delta;
                pos.x = Mathf.Clamp(pos.x, 0f, Screen.width);
                pos.y = Mathf.Clamp(pos.y, 0f, Screen.height);
                Position = pos;

                // Warp mouse off-screen so InputSystemUIInputModule doesn't fire
                // conflicting pointer events — ProcessLockedUIInput handles all UI
                InputState.Change(mouse.position, new Vector2(-1f, -1f));
            }
            else
            {
                // Normal mode: read real mouse position
                Position = mouse.position.ReadValue();
            }

            // Show cursor when: not in settings menu AND no contextual cursor active
            bool showSoftwareCursor = !settingsMenuOpen && !CustomCursor.IsContextualCursorActive;
            Cursor.visible = settingsMenuOpen;

            if (cursorCanvas != null && cursorCanvas.gameObject.activeSelf != showSoftwareCursor)
                cursorCanvas.gameObject.SetActive(showSoftwareCursor);

            if (showSoftwareCursor && cursorImage != null)
            {
                // Position the cursor image at the virtual position
                // Canvas is overlay, so we use screen coordinates directly
                cursorImage.rectTransform.position = new Vector3(Position.x - 8f, Position.y + 8f, 0f);
            }

            ProcessLockedUIInput(mouse);
        }

        private void ProcessLockedUIInput(Mouse mouse)
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                // Transitioning out of locked mode — clear hover state
                if (lastHover != null)
                {
                    if (cachedPointerData != null)
                        ExecuteEvents.Execute(lastHover, cachedPointerData, ExecuteEvents.pointerExitHandler);
                    lastHover = null;
                }
                pressTarget = null;
                return;
            }

            if (EventSystem.current == null) return;

            // Create or reuse PointerEventData
            if (cachedPointerData == null)
                cachedPointerData = new PointerEventData(EventSystem.current);

            cachedPointerData.position = Position;

            // Raycast against all GraphicRaycasters
            raycastResults.Clear();
            EventSystem.current.RaycastAll(cachedPointerData, raycastResults);

            GameObject hitObject = raycastResults.Count > 0 ? raycastResults[0].gameObject : null;

            // Hover enter/exit
            if (hitObject != lastHover)
            {
                if (lastHover != null)
                    ExecuteEvents.ExecuteHierarchy(lastHover, cachedPointerData, ExecuteEvents.pointerExitHandler);
                if (hitObject != null)
                    ExecuteEvents.ExecuteHierarchy(hitObject, cachedPointerData, ExecuteEvents.pointerEnterHandler);
                lastHover = hitObject;
            }

            cachedPointerData.pointerCurrentRaycast = raycastResults.Count > 0 ? raycastResults[0] : default;

            // Press
            if (mouse.leftButton.wasPressedThisFrame && hitObject != null)
            {
                cachedPointerData.pointerPressRaycast = cachedPointerData.pointerCurrentRaycast;
                cachedPointerData.pressPosition = Position;
                pressTarget = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hitObject);
                cachedPointerData.pointerPress = pressTarget;
                ExecuteEvents.Execute(pressTarget, cachedPointerData, ExecuteEvents.pointerDownHandler);
            }

            // Release
            if (mouse.leftButton.wasReleasedThisFrame && pressTarget != null)
            {
                ExecuteEvents.Execute(pressTarget, cachedPointerData, ExecuteEvents.pointerUpHandler);

                // Click if still over the same element
                GameObject currentClickTarget = hitObject != null
                    ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hitObject)
                    : null;
                if (currentClickTarget == pressTarget)
                    ExecuteEvents.Execute(pressTarget, cachedPointerData, ExecuteEvents.pointerClickHandler);

                cachedPointerData.pointerPress = null;
                pressTarget = null;
            }
        }
    }
}
