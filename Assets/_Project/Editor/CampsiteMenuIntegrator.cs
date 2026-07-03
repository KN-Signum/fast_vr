#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CampsiteMenuIntegrator
{
    private const string CampsiteScenePath = "Assets/_Project/Scenes/MenuCampsite.unity";
    private const string MainMenuScenePath = "Assets/_Project/Scenes/MainMenu.unity";
    private const string PrefabPath = "Assets/_Project/Prefabs/CampsiteEnvironment.prefab";

    private static readonly Vector3 CampsiteSpawnWorld = new(108.54f, 18.001f, 119.05f);

    private static readonly string[] EnvironmentRootNames =
    {
        "Terrain",
        "PropsStatic",
        "CampfireGroup",
        "LightSources",
        "AudioSources",
        "Global Volume"
    };

    [MenuItem("FAST VR/Integrate Campsite Into Main Menu")]
    public static void IntegrateFromMenu()
    {
        Integrate();
    }

    public static void IntegrateBatch()
    {
        Integrate();
        EditorApplication.Exit(0);
    }

    private static void Integrate()
    {
        var campsiteScene = EditorSceneManager.OpenScene(CampsiteScenePath, OpenSceneMode.Single);

        var environmentRoot = new GameObject("CampsiteEnvironment");
        var parented = new List<GameObject>();

        foreach (var rootName in EnvironmentRootNames)
        {
            var root = GameObject.Find(rootName);
            if (root == null)
            {
                Debug.LogWarning($"Campsite root '{rootName}' not found.");
                continue;
            }

            root.transform.SetParent(environmentRoot.transform, true);
            parented.Add(root);
        }

        if (parented.Count == 0)
        {
            Object.DestroyImmediate(environmentRoot);
            Debug.LogError("No campsite environment roots were found.");
            return;
        }

        environmentRoot.transform.position = -CampsiteSpawnWorld;

        EnsureFolder("Assets/_Project/Prefabs");
        PrefabUtility.SaveAsPrefabAsset(environmentRoot, PrefabPath);

        foreach (var root in parented)
            root.transform.SetParent(null, true);
        Object.DestroyImmediate(environmentRoot);

        var mainMenuScene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

        RemoveExistingEnvironmentInstances();
        DisableTemplateGeometry();

        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));

        EditorSceneManager.MarkSceneDirty(mainMenuScene);
        EditorSceneManager.SaveScene(mainMenuScene);

        RemoveSceneFromBuildSettings(CampsiteScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Campsite environment integrated into MainMenu at origin-aligned prefab.");
    }

    private static void RemoveExistingEnvironmentInstances()
    {
        foreach (var environment in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (environment.name == "CampsiteEnvironment")
                Object.DestroyImmediate(environment.gameObject);
        }
    }

    private static void DisableTemplateGeometry()
    {
        var plane = GameObject.Find("Plane");
        if (plane != null)
            plane.SetActive(false);
    }

    private static void RemoveSceneFromBuildSettings(string scenePath)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(scene => scene.path == scenePath);
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        var parts = path.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
