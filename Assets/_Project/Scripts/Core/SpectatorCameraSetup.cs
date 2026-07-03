using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class SpectatorCameraSetup : MonoBehaviour
{
    [SerializeField] private RenderTexture dashboardRT;
    [SerializeField] private Transform followTarget;
    [SerializeField] private bool mirrorHeadRotation = true;
    [SerializeField] private Vector3 localOffset = Vector3.zero;

    private Camera _spectatorCamera;

    public void Configure(RenderTexture renderTexture, Transform target, bool mirrorRotation = true, Vector3 offset = default)
    {
        dashboardRT = renderTexture;
        followTarget = target;
        mirrorHeadRotation = mirrorRotation;
        localOffset = offset;
        EnsureSpectatorCamera();
    }

    void Awake()
    {
        EnsureSpectatorCamera();
    }

    void LateUpdate()
    {
        if (_spectatorCamera == null || followTarget == null)
            return;

        _spectatorCamera.transform.position = followTarget.TransformPoint(localOffset);

        if (mirrorHeadRotation)
            _spectatorCamera.transform.rotation = followTarget.rotation;
    }

    private void EnsureSpectatorCamera()
    {
        if (dashboardRT == null)
        {
            var networkClient = FindFirstObjectByType<UnityNetworkClient>();
            if (networkClient != null)
                dashboardRT = networkClient.dashboardRT;
        }

        if (followTarget == null)
        {
            var xrCamera = Camera.main;
            if (xrCamera != null)
                followTarget = xrCamera.transform;
        }

        if (followTarget == null)
            return;

        if (_spectatorCamera == null)
            _spectatorCamera = GetComponentInChildren<Camera>(true);

        if (_spectatorCamera == null)
            _spectatorCamera = CreateSpectatorCamera();

        _spectatorCamera.targetTexture = dashboardRT;
        _spectatorCamera.stereoTargetEye = StereoTargetEyeMask.None;
        _spectatorCamera.depth = -10;

        var listener = _spectatorCamera.GetComponent<AudioListener>();
        if (listener != null)
            listener.enabled = false;
    }

    private Camera CreateSpectatorCamera()
    {
        var cameraObject = new GameObject("SpectatorCamera");
        cameraObject.transform.SetParent(transform, false);

        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.nearClipPlane = 0.08f;
        camera.farClipPlane = 1000f;
        camera.fieldOfView = 60f;

        if (!cameraObject.TryGetComponent(out UniversalAdditionalCameraData urpData))
            urpData = cameraObject.AddComponent<UniversalAdditionalCameraData>();

        urpData.renderShadows = true;

        return camera;
    }
}
