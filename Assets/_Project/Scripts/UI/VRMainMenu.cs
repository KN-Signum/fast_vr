using UnityEngine;
using UnityEngine.SceneManagement;

public class VRMainMenu : MonoBehaviour
{
    [Header("Panele UI")]
    public GameObject infoPanel;
    public GameObject menuPanel;
    
    private SceneStateSync _stateSync;

    void Start()
    {
        _stateSync = GetComponent<SceneStateSync>();

        if (_stateSync == null)
    {
        _stateSync = Object.FindFirstObjectByType<SceneStateSync>();
    }

        if (_stateSync != null)
    {
        ShowInfo();
    }
        else
    {
        Debug.LogError("❌ KRYTYCZNY BŁĄD: Nie znaleziono skryptu SceneStateSync na scenie! " +
                       "Upewnij się, że obiekt _SceneController go posiada.");
    }
    }

    public void ShowMenu()
    {
        infoPanel.SetActive(false);
        menuPanel.SetActive(true);

        _stateSync.SendState("menu", new ActionEntry[] {
            new ActionEntry { action = "start_forest", label = "Spacer w lesie" },
            new ActionEntry { action = "start_painting", label = "Malowanie" },
            new ActionEntry { action = "exit_app", label = "Wyjdź" }
        });
    }

    public void ShowInfo()
    {
        infoPanel.SetActive(true);
        menuPanel.SetActive(false);

        _stateSync.SendState("info", new ActionEntry[] {
            new ActionEntry { action = "next_to_selection", label = "Zacznij badanie" }
        });
    }

    public void LoadGameScene(string sceneName)
    {
        Debug.Log("Ładowanie sceny: " + sceneName);
        SceneManager.LoadScene(sceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}