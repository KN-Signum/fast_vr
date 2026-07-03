using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using Unity.XR.CoreUtils;

[DefaultExecutionOrder(1000)]
public class MainMenuXRRigLock : MonoBehaviour
{
    private const float DefaultCameraYOffset = 1.36144f;

    [Header("Targets")]
    [SerializeField] private XROrigin xrOrigin;
    [SerializeField] private Transform spawnAnchor;
    [SerializeField] private Transform menuLookTarget;

    [Header("Lock")]
    [SerializeField] private bool lockPosition = true;
    [SerializeField] private bool lockBodyRotationToMenu = true;
    [SerializeField] private bool disableLocomotion = true;
    [SerializeField] private bool disableCharacterController = true;

    [Header("Placement")]
    [SerializeField] private bool snapGroundYOnStart = true;
    [SerializeField] private float groundRaycastStartHeight = 10f;
    [SerializeField] private float groundSurfaceOffset = 0.02f;
    [SerializeField] private LayerMask groundLayers = Physics.DefaultRaycastLayers;
    [SerializeField] private float cameraYOffset = DefaultCameraYOffset;

    private CharacterController _characterController;
    private Vector3 _lockedWorldPosition;
    private Quaternion _lockedWorldRotation = Quaternion.identity;
    private bool _initialized;

    void Awake()
    {
        ResolveReferences();
        CaptureSpawnFromScene();
        ApplyLocomotionSettings();
        EnsureCameraHeight();
    }

    void Start()
    {
        if (snapGroundYOnStart)
            _lockedWorldPosition.y = SampleGroundHeight(_lockedWorldPosition.x, _lockedWorldPosition.z);

        ApplyLock();
        _initialized = true;
    }

    void LateUpdate()
    {
        if (!_initialized)
            return;

        ApplyLock();
    }

    private void ResolveReferences()
    {
        if (xrOrigin == null)
            xrOrigin = FindFirstObjectByType<XROrigin>();

        if (spawnAnchor == null)
        {
            var spawnObject = GameObject.Find("MenuSpawnPoint");
            if (spawnObject != null)
                spawnAnchor = spawnObject.transform;
        }

        if (menuLookTarget == null)
        {
            var canvas = GameObject.Find("Canvas");
            if (canvas != null)
                menuLookTarget = canvas.transform;
        }

        if (xrOrigin != null)
            _characterController = xrOrigin.GetComponent<CharacterController>();
    }

    private void CaptureSpawnFromScene()
    {
        if (spawnAnchor != null)
        {
            _lockedWorldPosition = spawnAnchor.position;
            _lockedWorldRotation = spawnAnchor.rotation;
            return;
        }

        if (xrOrigin != null)
        {
            _lockedWorldPosition = xrOrigin.transform.position;
            _lockedWorldRotation = xrOrigin.transform.rotation;
        }
    }

    private void EnsureCameraHeight()
    {
        if (xrOrigin == null)
            return;

        if (xrOrigin.CameraYOffset < 0.01f)
            xrOrigin.CameraYOffset = cameraYOffset;
    }

    private void ApplyLocomotionSettings()
    {
        if (xrOrigin == null)
            return;

        if (disableLocomotion)
        {
            foreach (var provider in xrOrigin.GetComponentsInChildren<LocomotionProvider>(true))
                provider.enabled = false;
        }

        if (disableCharacterController && _characterController != null)
            _characterController.enabled = false;

        foreach (var rigidbody in xrOrigin.GetComponentsInChildren<Rigidbody>(true))
            rigidbody.useGravity = false;
    }

    private float SampleGroundHeight(float worldX, float worldZ)
    {
        var samplePoint = new Vector3(worldX, 0f, worldZ);
        var bestHeight = _lockedWorldPosition.y;
        var found = false;

        foreach (var terrain in Terrain.activeTerrains)
        {
            var terrainHeight = terrain.SampleHeight(samplePoint) + terrain.transform.position.y;
            if (!found || terrainHeight > bestHeight)
            {
                bestHeight = terrainHeight;
                found = true;
            }
        }

        var rayOrigin = new Vector3(worldX, _lockedWorldPosition.y + groundRaycastStartHeight, worldZ);
        if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, groundRaycastStartHeight * 2f, groundLayers, QueryTriggerInteraction.Ignore))
        {
            if (!found || hit.point.y > bestHeight)
            {
                bestHeight = hit.point.y;
                found = true;
            }
        }

        return found ? bestHeight + groundSurfaceOffset : _lockedWorldPosition.y;
    }

    private void ApplyLock()
    {
        if (xrOrigin == null)
            return;

        var rigTransform = xrOrigin.transform;

        if (lockPosition)
            rigTransform.position = _lockedWorldPosition;

        if (lockBodyRotationToMenu)
        {
            if (menuLookTarget != null)
            {
                var toMenu = menuLookTarget.position - rigTransform.position;
                toMenu.y = 0f;

                if (toMenu.sqrMagnitude > 0.0001f)
                    rigTransform.rotation = Quaternion.LookRotation(toMenu.normalized, Vector3.up);
            }
            else
            {
                rigTransform.rotation = _lockedWorldRotation;
            }
        }
    }
}
