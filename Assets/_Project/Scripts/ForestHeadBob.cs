using UnityEngine;
using UnityEngine.Splines;
using Unity.XR.CoreUtils;

/// <summary>
/// Subtle walking head-bob for the on-rails ForestWalk.
/// Applies a small sine offset to the XR camera-offset transform (the parent of the
/// tracked HMD camera), so it layers on top of real head tracking instead of fighting it.
/// Kept intentionally gentle — vertical-dominant, no roll by default — because artificial
/// camera motion is a common VR comfort/motion-sickness trigger.
///
/// "Am I walking?" is derived from the rig actually moving through the world, not from
/// <see cref="SplineAnimate.IsPlaying"/> — that flag flips false when a non-looping spline
/// reaches its end, which made an earlier version bob only once. Movement is the ground truth:
/// it bobs while the rig travels and eases to neutral when it stops (finished or dashboard-paused).
/// </summary>
[DefaultExecutionOrder(1100)] // after MainMenuXRRigLock (1000); this only touches the camera offset
public class ForestHeadBob : MonoBehaviour
{
    [Header("References (auto-resolved if left empty)")]
    [SerializeField] private Transform bobTarget;         // XR Origin's Camera Offset (bobbed)
    [SerializeField] private Transform motionReference;   // moves along the spline (measured, not bobbed)
    [SerializeField] private SplineAnimate splineAnimate; // fallback motion hint

    [Header("Bob Shape")]
    [Tooltip("Vertical bob height in metres (peak). Keep small for comfort.")]
    [Range(0f, 0.12f)] public float verticalAmplitude = 0.03f;

    [Tooltip("Sideways sway in metres (peak). 0 = off. Comfort-sensitive.")]
    [Range(0f, 0.08f)] public float horizontalAmplitude = 0.012f;

    [Tooltip("Camera roll in degrees (peak). 0 = off. Most nausea-prone — leave low.")]
    [Range(0f, 3f)] public float rollAmplitude = 0f;

    [Tooltip("Vertical bobs per second while walking. Horizontal sway/roll run at half this (one per stride).")]
    [Range(0.1f, 4f)] public float bobsPerSecond = 1.8f;

    [Header("Walking Detection")]
    [Tooltip("Rig speed (m/s) above which we count as walking.")]
    [SerializeField] private float moveThreshold = 0.05f;

    [Header("Easing")]
    [Tooltip("How quickly the bob fades in when walking starts / out when it stops.")]
    [Range(0.5f, 12f)] public float blendSpeed = 4f;

    [Header("Debug")]
    [SerializeField] private bool logToConsole = true;

    private float _phase;                 // continuous, so start/stop never snaps
    private float _weight;                // 0 = neutral, 1 = full bob
    private Vector3 _lastPosOffset = Vector3.zero;
    private Quaternion _lastRotOffset = Quaternion.identity;
    private Vector3 _lastMotionPos;
    private bool _hasMotionSample;
    private bool _loggedWalking;
    private bool _loggedMissing;

    void OnDisable()
    {
        // Hand the transform back exactly as the rest of the rig expects it.
        if (bobTarget != null)
        {
            bobTarget.localPosition -= _lastPosOffset;
            bobTarget.localRotation *= Quaternion.Inverse(_lastRotOffset);
        }
        _lastPosOffset = Vector3.zero;
        _lastRotOffset = Quaternion.identity;
        _hasMotionSample = false;
    }

    void LateUpdate()
    {
        if (bobTarget == null || motionReference == null)
            ResolveReferences();

        if (bobTarget == null || motionReference == null)
        {
            if (logToConsole && !_loggedMissing)
            {
                _loggedMissing = true;
                Debug.LogWarning("[ForestHeadBob] Could not resolve XR camera offset / motion reference — bob disabled.");
            }
            return;
        }

        float dt = Time.deltaTime;
        bool walking = IsWalking(dt);

        float target = walking ? 1f : 0f;
        _weight = Mathf.MoveTowards(_weight, target, blendSpeed * dt);

        // Advance phase only while there's motion to show, so a stop freezes cleanly.
        _phase += dt * bobsPerSecond * Mathf.PI * 2f * Mathf.Max(_weight, target);

        float vertical = Mathf.Sin(_phase) * verticalAmplitude;
        float horizontal = Mathf.Sin(_phase * 0.5f) * horizontalAmplitude;
        float roll = Mathf.Sin(_phase * 0.5f) * rollAmplitude;

        Vector3 posOffset = new Vector3(horizontal, vertical, 0f) * _weight;
        Quaternion rotOffset = Quaternion.AngleAxis(roll * _weight, Vector3.forward);

        // Reclaim whatever else set the offset this frame, then re-apply our bob on top.
        Vector3 basePos = bobTarget.localPosition - _lastPosOffset;
        Quaternion baseRot = bobTarget.localRotation * Quaternion.Inverse(_lastRotOffset);

        bobTarget.localPosition = basePos + posOffset;
        bobTarget.localRotation = baseRot * rotOffset;

        _lastPosOffset = posOffset;
        _lastRotOffset = rotOffset;

        LogState(walking);
    }

    private bool IsWalking(float dt)
    {
        Vector3 pos = motionReference.position;

        if (!_hasMotionSample || dt <= 0f)
        {
            _lastMotionPos = pos;
            _hasMotionSample = true;
            // Fall back to the play flag on the very first frame before we have a delta.
            return splineAnimate != null && splineAnimate.IsPlaying;
        }

        float speed = (pos - _lastMotionPos).magnitude / dt;
        _lastMotionPos = pos;
        return speed > moveThreshold;
    }

    private void ResolveReferences()
    {
        var xrOrigin = FindFirstObjectByType<XROrigin>();

        if (bobTarget == null && xrOrigin != null)
        {
            var offset = xrOrigin.CameraFloorOffsetObject;
            bobTarget = offset != null ? offset.transform : xrOrigin.transform;
        }

        if (splineAnimate == null)
            splineAnimate = FindFirstObjectByType<SplineAnimate>();

        if (motionReference == null)
        {
            // Prefer whatever the spline actually drives; never the transform we bob.
            if (splineAnimate != null)
                motionReference = splineAnimate.transform;
            else if (xrOrigin != null)
                motionReference = xrOrigin.transform;
            else if (bobTarget != null && bobTarget.parent != null)
                motionReference = bobTarget.parent;
        }

        // Guard against measuring the transform we're bobbing (would feed back on itself).
        if (motionReference == bobTarget && bobTarget != null && bobTarget.parent != null)
            motionReference = bobTarget.parent;
    }

    private void LogState(bool walking)
    {
        if (!logToConsole || walking == _loggedWalking)
            return;

        _loggedWalking = walking;
        Debug.Log(walking ? "[ForestHeadBob] Walking — head-bob active." : "[ForestHeadBob] Stopped — head-bob easing out.");
    }
}
