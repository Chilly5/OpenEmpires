using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenEmpires
{
    public class RTSCameraController : MonoBehaviour
    {
        [Header("Pan Settings")]
        private float keyboardPanSpeed = 1.0f;
        private float edgePanSpeed = 1.0f;
        private float basePanSpeed = 80f;
        private bool panAcceleration = true;
        private bool enableEdgeScroll = true;
        private bool edgePanWhileBoxSelecting = false;
        private float edgeScrollThreshold = 10f;

        private float zoomSpeed = 1.5f;
        private float minZoomDistance = 5f;
        private float maxZoomDistance = 40f;
        private float zoomSmoothing = 8f;

        private float panSmoothing = 0.15f;
        private float rotateSpeed = 0.3f;
        private float rotationSmoothing = 10f;
        private float pitch = 30f;

        private float centerX;
        private float centerZ;
        private float maxRadius;

        private Transform pivot;
        private Transform arm;
        private float currentZoom = 15f;
        private float targetZoom = 15f;
        private float currentYaw = 45f;
        private float targetYaw = 45f;
        private Vector3 targetPivotPos;
        private Vector3 pivotVelocity;

        private Camera cam;
        private RTSInputActions inputActions;
        private Vector2 panInput;
        private Vector2 mousePanDelta;
        private Vector2 mousePosition;
        private Vector2 rotateDelta;
        private bool rotateEnabled;
        private bool mousePanEnabled;
        private bool ownsPointerLock;
        private CursorLockMode cursorLockBeforeMouseControl;
        private Vector2 cursorPositionBeforeMouseControl;

        public Vector3 PivotPosition
        {
            get => pivot.position;
            set
            {
                if (panAcceleration)
                {
                    // Snap 70% instantly, let SmoothDamp handle the rest.
                    pivot.position = Vector3.Lerp(pivot.position, value, 0.7f);
                }
                else
                {
                    // Acceleration off — match the per-frame branch in SmoothPivotPosition and snap instantly.
                    pivot.position = value;
                }
                targetPivotPos = value;
                pivotVelocity = Vector3.zero;
            }
        }
        public float CurrentYaw => currentYaw;
        public float CurrentZoom => currentZoom;
        public float Pitch => pitch;

        // Camera settings properties
        public float KeyboardPanSpeed { get => keyboardPanSpeed; set => keyboardPanSpeed = value; }
        public float EdgePanSpeed { get => edgePanSpeed; set => edgePanSpeed = value; }
        public bool PanAcceleration { get => panAcceleration; set => panAcceleration = value; }
        public bool EnableEdgeScroll { get => enableEdgeScroll; set => enableEdgeScroll = value; }
        public bool EdgePanWhileBoxSelecting { get => edgePanWhileBoxSelecting; set => edgePanWhileBoxSelecting = value; }

        // Save/Load camera settings
        public void SaveCameraSettings()
        {
            PlayerPrefs.SetFloat("CameraKeyboardPanSpeed", keyboardPanSpeed);
            PlayerPrefs.SetFloat("CameraEdgePanSpeed", edgePanSpeed);
            PlayerPrefs.SetInt("CameraPanAcceleration", panAcceleration ? 1 : 0);
            PlayerPrefs.SetInt("CameraEnableEdgeScroll", enableEdgeScroll ? 1 : 0);
            PlayerPrefs.SetInt("CameraEdgePanWhileBoxSelecting", edgePanWhileBoxSelecting ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void LoadCameraSettings()
        {
            keyboardPanSpeed = PlayerPrefs.GetFloat("CameraKeyboardPanSpeed", 1.0f);
            edgePanSpeed = PlayerPrefs.GetFloat("CameraEdgePanSpeed", 1.0f);
            panAcceleration = PlayerPrefs.GetInt("CameraPanAcceleration", 1) == 1;
            enableEdgeScroll = PlayerPrefs.GetInt("CameraEnableEdgeScroll", 1) == 1;
            edgePanWhileBoxSelecting = PlayerPrefs.GetInt("CameraEdgePanWhileBoxSelecting", 0) == 1;
        }

        public void SetBounds(int mapWidth, int mapHeight)
        {
            centerX = mapWidth / 2f;
            centerZ = mapHeight / 2f;
            maxRadius = Mathf.Min(mapWidth, mapHeight) / 2f - 10f + 5f;
        }

        private void Awake()
        {
            inputActions = new RTSInputActions();

            // Build pivot hierarchy: Pivot (on ground) -> Arm -> Camera
            pivot = new GameObject("CameraPivot").transform;
            arm = new GameObject("CameraArm").transform;
            arm.SetParent(pivot);
            transform.SetParent(arm);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            // Orthographic camera with black background
            cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = currentZoom;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
            }

            targetPivotPos = Vector3.zero;
            UpdateCameraTransform();
            
            // Load saved camera settings
            LoadCameraSettings();
        }

        private void OnEnable()
        {
            inputActions.RTS.Enable();
            inputActions.RTS.CameraPan.performed += ctx => panInput = ctx.ReadValue<Vector2>();
            inputActions.RTS.CameraPan.canceled += ctx => panInput = Vector2.zero;
            inputActions.RTS.CameraMousePan.performed += ctx => mousePanDelta = ctx.ReadValue<Vector2>();
            inputActions.RTS.CameraMousePan.canceled += ctx => mousePanDelta = Vector2.zero;
            inputActions.RTS.CameraZoom.performed += ctx => OnZoom(ctx.ReadValue<float>());
            inputActions.RTS.CameraRotateEnable.performed += BeginMouseControl;
            inputActions.RTS.CameraRotateEnable.canceled += EndMouseControl;
            inputActions.RTS.CameraRotateDelta.performed += ctx => rotateDelta = ctx.ReadValue<Vector2>();
            inputActions.RTS.CameraRotateDelta.canceled += ctx => rotateDelta = Vector2.zero;
            // mousePosition is now read from VirtualCursor in Update
        }

        private void OnDisable()
        {
            EndMouseControl(default);
            inputActions.RTS.Disable();
        }

        private void BeginMouseControl(InputAction.CallbackContext context)
        {
            rotateEnabled = true;
            mousePanEnabled = true;

#if !UNITY_WEBGL || UNITY_EDITOR
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                cursorLockBeforeMouseControl = Cursor.lockState;
                cursorPositionBeforeMouseControl = Mouse.current != null
                    ? Mouse.current.position.ReadValue()
                    : VirtualCursor.Position;
                ownsPointerLock = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
#endif
        }

        private void EndMouseControl(InputAction.CallbackContext context)
        {
            rotateEnabled = false;
            mousePanEnabled = false;

            if (!ownsPointerLock) return;

            Cursor.lockState = cursorLockBeforeMouseControl;
            VirtualCursor.RestorePosition(cursorPositionBeforeMouseControl);
            ownsPointerLock = false;
        }

        private void Update()
        {
            mousePosition = VirtualCursor.Position;
            HandlePan();
            HandleEdgeScroll();
            HandleZoomSmoothing();
            HandleRotation();
            ClampPosition();
            SmoothPivotPosition();
            SmoothRotation();
            UpdateCameraTransform();
        }

        private void HandlePan()
        {
            Vector3 totalMove = Vector3.zero;
            Vector3 forward = pivot.forward;
            Vector3 right = pivot.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
            
            float zoomScale = currentZoom / 25f;
            
            // Handle keyboard pan (WASD/Arrow keys)
            if (panInput.sqrMagnitude > 0.001f)
            {
                float effectivePanSpeed = basePanSpeed * keyboardPanSpeed;
                Vector3 keyboardMove = (forward * panInput.y + right * panInput.x) * effectivePanSpeed * zoomScale * Time.deltaTime;
                totalMove += keyboardMove;
            }
            
            // Handle mouse pan (Middle mouse button drag)
            if (mousePanEnabled && mousePanDelta.sqrMagnitude > 0.001f)
            {
                float effectiveMousePanSpeed = basePanSpeed * 1.0f * 0.01f; // Fixed speed for mouse pan
                
                // Invert Y for intuitive dragging (drag up = move up)
                Vector3 mouseMove = (forward * (-mousePanDelta.y) + right * (-mousePanDelta.x)) * effectiveMousePanSpeed * zoomScale * Time.deltaTime;
                totalMove += mouseMove;
            }
            
            if (totalMove.sqrMagnitude > 0.001f)
            {
                targetPivotPos += totalMove;
            }
        }

        private void HandleEdgeScroll()
        {
            if (!enableEdgeScroll) return;
            if (mousePanEnabled) return;
            if (UnitSelectionManager.UIInputSuppressed) return;
            
            // Check if box selecting and edge pan while box selecting is disabled
            var selectionManager = FindObjectOfType<UnitSelectionManager>();
            if (!edgePanWhileBoxSelecting && selectionManager != null && selectionManager.IsDragging) return;

            Vector2 edgePan = Vector2.zero;
            if (mousePosition.x <= edgeScrollThreshold) edgePan.x = -1f;
            else if (mousePosition.x >= Screen.width - edgeScrollThreshold) edgePan.x = 1f;
            if (mousePosition.y <= edgeScrollThreshold) edgePan.y = -1f;
            else if (mousePosition.y >= Screen.height - edgeScrollThreshold) edgePan.y = 1f;

            if (edgePan.sqrMagnitude < 0.01f) return;

            Vector3 forward = pivot.forward;
            Vector3 right = pivot.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            float zoomScale = currentZoom / 25f;
            float effectiveEdgePanSpeed = basePanSpeed * edgePanSpeed;
            Vector3 move = (forward * edgePan.y + right * edgePan.x) * effectiveEdgePanSpeed * zoomScale * Time.deltaTime;
            targetPivotPos += move;
        }

        private void OnZoom(float scrollValue)
        {
            if (UnitSelectionManager.UIInputSuppressed) return;
            targetZoom -= scrollValue * zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoomDistance, maxZoomDistance);
        }

        private void HandleZoomSmoothing()
        {
            currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * zoomSmoothing);
            if (cam != null)
                cam.orthographicSize = currentZoom;
        }

        private void HandleRotation()
        {
            if (!rotateEnabled) return;
            targetYaw += rotateDelta.x * rotateSpeed;
        }

        private void ClampPosition()
        {
            float dx = targetPivotPos.x - centerX;
            float dz = targetPivotPos.z - centerZ;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            if (dist > maxRadius)
            {
                float scale = maxRadius / dist;
                targetPivotPos.x = centerX + dx * scale;
                targetPivotPos.z = centerZ + dz * scale;
            }
        }

        private void SmoothPivotPosition()
        {
            if (panAcceleration)
            {
                // Smooth, accelerated movement
                pivot.position = Vector3.SmoothDamp(pivot.position, targetPivotPos, ref pivotVelocity, panSmoothing);
            }
            else
            {
                // Snappy, immediate movement
                pivot.position = targetPivotPos;
                pivotVelocity = Vector3.zero; // Reset velocity for immediate stop
            }
        }

        private void SmoothRotation()
        {
            currentYaw = Mathf.Lerp(currentYaw, targetYaw, Time.deltaTime * rotationSmoothing);
        }

        private void UpdateCameraTransform()
        {
            pivot.rotation = Quaternion.Euler(0f, currentYaw, 0f);
            arm.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            arm.localPosition = Vector3.zero;
            transform.localPosition = new Vector3(0f, 0f, -100f);
        }
    }
}
