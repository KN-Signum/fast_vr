using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.XR.CoreUtils;

/// <summary>
/// Footstep audio for the on-rails ForestWalk. Fires a step sound at a steady stride cadence
/// while the rig is actually travelling along the spline, using the same movement-based
/// "am I walking?" signal as <see cref="ForestHeadBob"/> (never <see cref="SplineAnimate.IsPlaying"/>,
/// which flips false at a non-looping spline's end). Steps stop the instant the walk pauses/ends.
///
/// Clips are taken from the serialized list if set; otherwise loaded from a <c>Resources/Footsteps</c>
/// folder at runtime, so the auto-added component (see ForestWalkSceneSetup) needs zero scene wiring —
/// just drop footstep .wav files into <c>Assets/Resources/Footsteps/</c>. A random clip is chosen per
/// step (no immediate repeat) with slight pitch variation so it never sounds looped.
/// Playback is 2D (non-spatialised) so it reads as the listener's own feet.
/// </summary>
[DefaultExecutionOrder(1101)] // just after ForestHeadBob (1100) so steps align with the bob dip
public class ForestFootsteps : MonoBehaviour
{
    [Header("Clips (empty = load from Resources/Footsteps)")]
    [SerializeField] private AudioClip[] footstepClips;

    [Header("Cadence")]
    [Tooltip("Steps per second while walking. Match ForestHeadBob.bobsPerSecond so steps land on the dip.")]
    [Range(0.5f, 4f)] [SerializeField] private float stepsPerSecond = 1.8f;

    [Header("Sound")]
    [Range(0f, 1f)] [SerializeField] private float volume = 0.6f;
    [Tooltip("Random +/- pitch spread per step for natural variation.")]
    [Range(0f, 0.3f)] [SerializeField] private float pitchJitter = 0.08f;

    [Header("Walking Detection")]
    [Tooltip("Rig speed (m/s) above which we count as walking.")]
    [SerializeField] private float moveThreshold = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool logToConsole = true;

    private const string ResourcesFolder = "Footsteps";

    private AudioSource _source;
    private Transform _motionReference;
    private SplineAnimate _splineAnimate;

    private Vector3 _lastMotionPos;
    private bool _hasMotionSample;
    private float _stepTimer;
    private int _lastClipIndex = -1;
    private bool _loggedMissing;

    void Awake()
    {
        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;   // 2D — the listener's own footsteps
        _source.loop = false;
        _source.volume = volume;

        if (footstepClips == null || footstepClips.Length == 0)
            footstepClips = Resources.LoadAll<AudioClip>(ResourcesFolder);
    }

    void Update()
    {
        if (_motionReference == null)
            ResolveReferences();

        if (_motionReference == null)
            return;

        float dt = Time.deltaTime;
        if (!IsWalking(dt))
        {
            // Reset the cadence so the first step after resuming isn't instant.
            _stepTimer = 0.5f / Mathf.Max(stepsPerSecond, 0.001f);
            return;
        }

        _stepTimer -= dt;
        if (_stepTimer <= 0f)
        {
            PlayStep();
            _stepTimer += 1f / Mathf.Max(stepsPerSecond, 0.001f);
        }
    }

    private void PlayStep()
    {
        if (footstepClips == null || footstepClips.Length == 0)
        {
            if (logToConsole && !_loggedMissing)
            {
                _loggedMissing = true;
                Debug.LogWarning("[ForestFootsteps] No footstep clips assigned or found in " +
                                 "Resources/Footsteps — footsteps silent.");
            }
            return;
        }

        int index = footstepClips.Length == 1
            ? 0
            : PickDifferentIndex();
        _lastClipIndex = index;

        _source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        _source.PlayOneShot(footstepClips[index], volume);
    }

    private int PickDifferentIndex()
    {
        int index = Random.Range(0, footstepClips.Length);
        if (index == _lastClipIndex)
            index = (index + 1) % footstepClips.Length; // avoid immediate repeat
        return index;
    }

    private bool IsWalking(float dt)
    {
        Vector3 pos = _motionReference.position;

        if (!_hasMotionSample || dt <= 0f)
        {
            _lastMotionPos = pos;
            _hasMotionSample = true;
            return _splineAnimate != null && _splineAnimate.IsPlaying;
        }

        float speed = (pos - _lastMotionPos).magnitude / dt;
        _lastMotionPos = pos;
        return speed > moveThreshold;
    }

    private void ResolveReferences()
    {
        if (_splineAnimate == null)
            _splineAnimate = FindFirstObjectByType<SplineAnimate>();

        if (_motionReference == null)
        {
            if (_splineAnimate != null)
                _motionReference = _splineAnimate.transform;
            else
            {
                var xrOrigin = FindFirstObjectByType<XROrigin>();
                if (xrOrigin != null)
                    _motionReference = xrOrigin.transform;
            }
        }
    }
}
