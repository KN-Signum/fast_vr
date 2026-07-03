using UnityEngine;

[DefaultExecutionOrder(-100)]
public class ForestWalkSceneSetup : MonoBehaviour
{
    [SerializeField] private RenderTexture dashboardRT;
    [SerializeField] private string sceneViewName = "forest";
    [SerializeField] private Transform spectatorFollowTarget;

    void Awake()
    {
        if (dashboardRT == null)
        {
            var networkClient = FindFirstObjectByType<UnityNetworkClient>();
            if (networkClient != null)
                dashboardRT = networkClient.dashboardRT;
        }

        EnsureSceneController();
        EnsureSpectatorCamera();
        EnsureForestWalkController();
    }

    private void EnsureSceneController()
    {
        if (FindFirstObjectByType<SceneStateSync>() != null)
            return;

        var controllerObject = new GameObject("_SceneController");
        var stateSync = controllerObject.AddComponent<SceneStateSync>();
        stateSync.sceneName = sceneViewName;
        stateSync.actions = new[]
        {
            new ActionEntry { action = "back_to_menu", label = "Wyjdź do menu" },
            new ActionEntry { action = "pause_walk", label = "Pauza" },
            new ActionEntry { action = "resume_walk", label = "Wznów spacer" }
        };
    }

    private void EnsureSpectatorCamera()
    {
        if (FindFirstObjectByType<SpectatorCameraSetup>() != null)
            return;

        var target = spectatorFollowTarget != null ? spectatorFollowTarget : FindHeadTransform();
        if (target == null)
        {
            Debug.LogWarning("ForestWalk: no HMD camera found for spectator stream.");
            return;
        }

        var setupObject = new GameObject("SpectatorCameraRig");
        var setup = setupObject.AddComponent<SpectatorCameraSetup>();
        setup.Configure(dashboardRT, target, mirrorRotation: true);
    }

    private void EnsureForestWalkController()
    {
        if (FindFirstObjectByType<ForestWalkController>() != null)
            return;

        var walkRoot = GameObject.Find("XRRig");
        if (walkRoot == null)
            walkRoot = new GameObject("ForestWalkLogic");

        walkRoot.AddComponent<ForestWalkController>();
    }

    private static Transform FindHeadTransform()
    {
        var mainCamera = Camera.main;
        if (mainCamera != null)
            return mainCamera.transform;

        var cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (var camera in cameras)
        {
            if (camera.CompareTag("MainCamera") || camera.gameObject.name.Contains("Main Camera"))
                return camera.transform;
        }

        return cameras.Length > 0 ? cameras[0].transform : null;
    }
}
