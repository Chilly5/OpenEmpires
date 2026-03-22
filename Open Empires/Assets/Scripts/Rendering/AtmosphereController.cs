using UnityEngine;
using OpenEmpires;

public class AtmosphereController : MonoBehaviour
{
    [Header("Aerial Perspective")]
    [SerializeField] private Color aerialFogColor = new Color(0.45f, 0.55f, 0.78f, 1f);
    [SerializeField] private float fogStart = 10f;
    [SerializeField] private float fogEnd = 80f;
    [SerializeField, Range(0f, 1f)] private float maxFogAmount = 0.5f;
    [SerializeField, Range(0f, 1f)] private float fogBackAmount = 0.3f;

    [Header("Cloud Shadows")]
    [SerializeField] private float cloudScale = 0.06f;
    [SerializeField] private float cloudSpeed = 3.0f;
    [SerializeField] private Vector2 cloudDirection = new Vector2(1f, 0.4f);
    [SerializeField, Range(0f, 1f)] private float cloudShadowIntensity = 0.4f;
    [SerializeField, Range(0f, 1f)] private float cloudReflectionIntensity = 0.3f;
    [SerializeField, Range(0f, 1f)] private float cloudCoverage = 0.78f;
    [SerializeField, Range(0f, 1f)] private float cloudSoftness = 0.15f;

    private RTSCameraController cameraController;
    private Camera cam;

    private static readonly int AerialFogColorID = Shader.PropertyToID("_AerialFogColor");
    private static readonly int AerialFogParamsID = Shader.PropertyToID("_AerialFogParams");
    private static readonly int CameraFocusXZID = Shader.PropertyToID("_CameraFocusXZ");
    private static readonly int CameraFogDirID = Shader.PropertyToID("_CameraFogDir");
    private static readonly int CloudParamsID = Shader.PropertyToID("_CloudParams");
    private static readonly int CloudParams2ID = Shader.PropertyToID("_CloudParams2");
    private static readonly int CloudDirectionID = Shader.PropertyToID("_CloudDirection");

    private void Start()
    {
        cameraController = FindFirstObjectByType<RTSCameraController>();
        cam = Camera.main;
        UpdateFogParams();
    }

    private void Update()
    {
        if (cameraController == null || cam == null) return;

        Vector3 pivot = cameraController.PivotPosition;
        Shader.SetGlobalVector(CameraFocusXZID, new Vector4(pivot.x, pivot.z, 0f, 0f));

        // Camera forward projected onto XZ plane (direction "into" the scene)
        Vector3 fwd = cam.transform.forward;
        Vector2 fwdXZ = new Vector2(fwd.x, fwd.z).normalized;
        Shader.SetGlobalVector(CameraFogDirID, new Vector4(fwdXZ.x, fwdXZ.y, 0f, 0f));

        UpdateFogParams();
    }

    private void UpdateFogParams()
    {
        Shader.SetGlobalVector(AerialFogColorID, aerialFogColor);
        float range = Mathf.Max(fogEnd - fogStart, 0.001f);
        Shader.SetGlobalVector(AerialFogParamsID, new Vector4(fogStart, 1f / range, maxFogAmount, fogBackAmount));

        Vector2 dir = cloudDirection.normalized;
        Shader.SetGlobalVector(CloudDirectionID, new Vector4(dir.x, dir.y, 0f, 0f));
        Shader.SetGlobalVector(CloudParamsID, new Vector4(cloudScale, cloudSpeed, cloudShadowIntensity, cloudCoverage));
        Shader.SetGlobalVector(CloudParams2ID, new Vector4(cloudSoftness, cloudReflectionIntensity, 0f, 0f));
    }

    private void OnValidate()
    {
        UpdateFogParams();
    }
}
