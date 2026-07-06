using System;
using UnityEngine;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;

/// <summary>
/// Streams VIVE eye gaze to the dashboard over the existing WebSocket JSON channel.
/// Populates the agreed <c>eye_tracking</c> packet schema (see vr_fast_dashboard).
/// </summary>
[DisallowMultipleComponent]
public class EyeTrackingStreamer : MonoBehaviour
{
    [Header("Stream Settings")]
    [Range(1, 60)] public int targetHz = 20;
    [SerializeField] private float gazeMaxDistance = 50f;
    [SerializeField] private LayerMask gazeLayerMask = ~0;

    [Header("References")]
    [SerializeField] private UnityNetworkClient networkClient;
    [SerializeField] private Transform playerTransform;

    [Header("Debug")]
    [SerializeField] private bool logToConsole = true;
    [SerializeField] private float logIntervalSeconds = 2f;

    private float _nextSendTime;
    private float _nextLogTime;
    private bool _loggedUnavailable;
    private bool _loggedFirstSample;

    void Awake()
    {
        if (networkClient == null)
            networkClient = GetComponent<UnityNetworkClient>();
    }

    void Update()
    {
        if (networkClient == null || Time.time < _nextSendTime)
            return;

        _nextSendTime = Time.time + (1f / targetHz);

        if (!TrySampleCombinedGaze(out Vector3 gazeOrigin, out Quaternion gazeRotation))
            return;

        _loggedUnavailable = false;

        Transform player = ResolvePlayerTransform();
        Vector3 playerPosition = player != null ? player.position : gazeOrigin;
        Vector3 gazePoint = SampleGazePoint(gazeOrigin, gazeRotation * Vector3.forward);

        var message = BuildMessage(playerPosition, gazePoint, gazeOrigin, gazeRotation);
        TryAttachSpectatorScreenCoords(message, gazePoint);
        networkClient.SendTextMessage(JsonUtility.ToJson(message));
        LogSample(playerPosition, gazePoint, gazeOrigin, gazeRotation);
    }

    private Transform ResolvePlayerTransform()
    {
        if (playerTransform != null)
            return playerTransform;

        var mainCamera = Camera.main;
        return mainCamera != null ? mainCamera.transform : null;
    }

    private bool TrySampleCombinedGaze(out Vector3 origin, out Quaternion rotation)
    {
        origin = Vector3.zero;
        rotation = Quaternion.identity;

        if (!XR_HTC_eye_tracker.Interop.GetEyeGazeData(out XrSingleEyeGazeDataHTC[] gazes)
            || gazes == null
            || gazes.Length < 2)
        {
            LogUnavailableOnce("Eye gaze data unavailable.");
            return false;
        }

        var left = gazes[(int)XrEyePositionHTC.XR_EYE_POSITION_LEFT_HTC];
        var right = gazes[(int)XrEyePositionHTC.XR_EYE_POSITION_RIGHT_HTC];
        bool leftValid = left.isValid;
        bool rightValid = right.isValid;

        if (!leftValid && !rightValid)
        {
            LogUnavailableOnce("Eye gaze poses not valid.");
            return false;
        }

        if (leftValid && rightValid)
        {
            Vector3 leftPos = left.gazePose.position.ToUnityVector();
            Vector3 rightPos = right.gazePose.position.ToUnityVector();
            Quaternion leftRot = left.gazePose.orientation.ToUnityQuaternion();
            Quaternion rightRot = right.gazePose.orientation.ToUnityQuaternion();

            origin = (leftPos + rightPos) * 0.5f;
            rotation = Quaternion.Slerp(leftRot, rightRot, 0.5f);
            return true;
        }

        var active = leftValid ? left : right;
        origin = active.gazePose.position.ToUnityVector();
        rotation = active.gazePose.orientation.ToUnityQuaternion();
        return true;
    }

    private Vector3 SampleGazePoint(Vector3 origin, Vector3 direction)
    {
        if (direction.sqrMagnitude < 1e-6f)
            return origin;

        direction.Normalize();

        if (Physics.Raycast(origin, direction, out RaycastHit hit, gazeMaxDistance, gazeLayerMask, QueryTriggerInteraction.Ignore))
            return hit.point;

        return origin + direction * gazeMaxDistance;
    }

    private static void TryAttachSpectatorScreenCoords(EyeTrackingMessage message, Vector3 worldPoint)
    {
        var spectatorCamera = FindSpectatorCamera();
        if (spectatorCamera == null)
            return;

        Vector3 viewport = spectatorCamera.WorldToViewportPoint(worldPoint);
        if (viewport.z <= 0f)
            return;

        message.gaze_screen_x = Mathf.Clamp01(viewport.x);
        message.gaze_screen_y = Mathf.Clamp01(viewport.y);
    }

    private static Camera FindSpectatorCamera()
    {
        var setup = FindFirstObjectByType<SpectatorCameraSetup>();
        if (setup != null)
        {
            var camera = setup.GetComponentInChildren<Camera>(true);
            if (camera != null)
                return camera;
        }

        var named = GameObject.Find("SpectatorCamera");
        return named != null ? named.GetComponent<Camera>() : null;
    }

    private static EyeTrackingMessage BuildMessage(
        Vector3 playerPosition,
        Vector3 gazePoint,
        Vector3 gazeOrigin,
        Quaternion gazeRotation)
    {
        Vector3 right = gazeRotation * Vector3.right;
        Vector3 up = gazeRotation * Vector3.up;
        Vector3 forward = gazeRotation * Vector3.forward;

        return new EyeTrackingMessage
        {
            type = "eye_tracking",
            player_position = ToVector(playerPosition),
            eyes_position = ToVector(gazePoint),
            eyes_transform = new EyeTrackingTransform
            {
                x_axis = ToVector(right),
                y_axis = ToVector(up),
                z_axis = ToVector(forward),
                origin = ToVector(gazeOrigin),
            },
        };
    }

    private static EyeTrackingVector3 ToVector(Vector3 value)
    {
        return new EyeTrackingVector3
        {
            x = value.x,
            y = value.y,
            z = value.z,
        };
    }

    private void LogUnavailableOnce(string message)
    {
        if (_loggedUnavailable)
            return;

        _loggedUnavailable = true;
        Debug.LogWarning($"[EyeTrackingStreamer] {message}");
    }

    private void LogSample(Vector3 playerPosition, Vector3 gazePoint, Vector3 gazeOrigin, Quaternion gazeRotation)
    {
        if (!logToConsole)
            return;

        if (!_loggedFirstSample)
        {
            _loggedFirstSample = true;
            Debug.Log("[EyeTrackingStreamer] Eye tracking active — streaming gaze to dashboard.");
        }

        if (Time.time < _nextLogTime)
            return;

        _nextLogTime = Time.time + logIntervalSeconds;
        Vector3 forward = gazeRotation * Vector3.forward;
        Debug.Log(
            $"[EyeTrackingStreamer] player=({playerPosition.x:F2},{playerPosition.y:F2},{playerPosition.z:F2}) " +
            $"gaze=({gazePoint.x:F2},{gazePoint.y:F2},{gazePoint.z:F2}) " +
            $"origin=({gazeOrigin.x:F2},{gazeOrigin.y:F2},{gazeOrigin.z:F2}) " +
            $"dir=({forward.x:F2},{forward.y:F2},{forward.z:F2})");
    }

    [Serializable]
    public class EyeTrackingMessage
    {
        public string type;
        public EyeTrackingVector3 player_position;
        public EyeTrackingVector3 eyes_position;
        public EyeTrackingTransform eyes_transform;
        public float gaze_screen_x = -1f;
        public float gaze_screen_y = -1f;
    }

    [Serializable]
    public class EyeTrackingVector3
    {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public class EyeTrackingTransform
    {
        public EyeTrackingVector3 x_axis;
        public EyeTrackingVector3 y_axis;
        public EyeTrackingVector3 z_axis;
        public EyeTrackingVector3 origin;
    }
}
