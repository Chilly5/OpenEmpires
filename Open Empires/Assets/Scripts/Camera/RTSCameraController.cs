using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenEmpires
{
    public class RTSCameraController : MonoBehaviour
    {
        private float panSpeed = 40f;
        private float edgeScrollThreshold = 10f;
        private bool enableEdgeScroll = true;

        private float zoomSpeed = 5f;
        private float minZoomDistance = 8f;
        private float maxZoomDistance = 200f;
        private float zoomSmoothing = 8f;

        private float panSmoothing = 0.15f;
        private float rotateSpeed = 0.3f;
        private float rotationSmoothing = 10f;
        private float pitch = 35f;

        private float centerX;
        private float centerZ;
        private float maxRadius;

        private Transform pivot;
        private Transform arm;
        private float currentZoom = 25f;
        private float targetZoom = 25f;
        private float currentYaw = 45f;
        private float targetYaw = 45f;
        private Vector3 targetPivotPos;
        private Vector3 pivotVelocity;

        private RTSInputActions inputActions;
        private Vector2 panInput;
        private Vector2 mousePosition;
        private Vector2 rotateDelta;
        private bool rotateEnabled;

        public Vector3 PivotPosition
        {
            get => pivot.position;
            set
            {
                // Snap 70% instantly, let SmoothDamp handle the rest
                pivot.position = Vector3.Lerp(pivot.position, value, 0.7f);
                targetPivotPos = value;
                pivotVelocity = Vector3.zero;
            }
        }
        public float CurrentYaw => currentYaw;
        public float CurrentZoom => currentZoom;
        public float Pitch => pitch;

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

            // Black background so areas beyond the map edge appear black
            var cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
            }

            targetPivotPos = Vector3.zero;
            UpdateCameraTransform();
        }

        private void OnEnable()
        {
            inputActions.RTS.Enable();
            inputActions.RTS.CameraPan.performed += ctx => panInput = ctx.ReadValue<Vector2>();
            inputActions.RTS.CameraPan.canceled += ctx => panInput = Vector2.zero;
            inputActions.RTS.CameraZoom.performed += ctx => OnZoom(ctx.ReadValue<float>());
            inputActions.RTS.CameraRotateEnable.performed += ctx => rotateEnabled = true;
            inputActions.RTS.CameraRotateEnable.canceled += ctx => rotateEnabled = false;
            inputActions.RTS.CameraRotateDelta.performed += ctx => rotateDelta = ctx.ReadValue<Vector2>();
            inputActions.RTS.CameraRotateDelta.canceled += ctx => rotateDelta = Vector2.zero;
            // mousePosition is now read from VirtualCursor in Update
        }

        private void OnDisable()
        {
            inputActions.RTS.Disable();
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
            if (panInput.sqrMagnitude < 0.001f) return;

            Vector3 forward = pivot.forward;
            Vector3 right = pivot.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            float zoomScale = currentZoom / 25f;
            Vector3 move = (forward * panInput.y + right * panInput.x) * panSpeed * zoomScale * Time.deltaTime;
            targetPivotPos += move;
        }

        private void HandleEdgeScroll()
        {
            if (!enableEdgeScroll) return;
            if (UnitSelectionManager.UIInputSuppressed) return;

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
            Vector3 move = (forward * edgePan.y + right * edgePan.x) * panSpeed * zoomScale * Time.deltaTime;
            targetPivotPos += move;
        }

        private void OnZoom(float scrollValue)
        {
            targetZoom -= scrollValue * zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoomDistance, maxZoomDistance);
        }

        private void HandleZoomSmoothing()
        {
            currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * zoomSmoothing);
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
            pivot.position = Vector3.SmoothDamp(pivot.position, targetPivotPos, ref pivotVelocity, panSmoothing);
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
            transform.localPosition = new Vector3(0f, 0f, -currentZoom);
        }
    }
}
