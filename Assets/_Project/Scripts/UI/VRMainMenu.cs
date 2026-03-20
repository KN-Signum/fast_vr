using UnityEngine;
using UnityEngine.SceneManagement;

public class VRMainMenu : MonoBehaviour
{
    [Header("Panele UI")]
    public GameObject infoPanel;
    public GameObject menuPanel;

    private UnityNetworkClient _networkClient;

    void Start()
    {
        _networkClient = Object.FindFirstObjectByType<UnityNetworkClient>();
        ShowInfo(); // Zawsze zaczynamy od info
    }

    // Wywoływane przez przycisk "Dalej" w VR lub Dashboardzie
    public void ShowMenu()
    {
        infoPanel.SetActive(false);
        menuPanel.SetActive(true);
        SendStateToDashboard("menu");
    }

    public void ShowInfo()
    {
        infoPanel.SetActive(true);
        menuPanel.SetActive(false);
        SendStateToDashboard("info");
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

    private void SendStateToDashboard(string view)
    {
        if (_networkClient == null) return;

        string actionsJson = "";
        if (view == "info") {
            actionsJson = "[{\"action\": \"next_to_selection\", \"label\": \"Dalej\"}]";
        } else {
            actionsJson = "[" +
                "{\"action\": \"start_forest\", \"label\": \"Spacer w lesie\"}," +
                "{\"action\": \"start_painting\", \"label\": \"Malowanie\"}," +
                "{\"action\": \"exit_app\", \"label\": \"Zamknij aplikację\"}" +
                "]";
        }

        string msg = "{\"type\": \"state_update\", \"current_view\": \"" + view + "\", \"available_actions\": " + actionsJson + "}";
        _networkClient.SendTextMessage(msg);
    }
}