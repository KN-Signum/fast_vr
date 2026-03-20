using UnityEngine;
using System;

[Serializable]
public class ActionEntry {
    public string action;
    public string label;
}

[Serializable]
public class StateUpdateMessage {
    public string type = "state_update";
    public string current_view;
    public ActionEntry[] available_actions;
}

public class SceneStateSync : MonoBehaviour
{
    [Header("Ustawienia Widoku")]
    public string sceneName; // np. "painting"
    public ActionEntry[] actions; // Lista akcji do wyświetlenia na Dashboardzie

    private UnityNetworkClient _networkClient;

    void Start()
    {
        // Szukamy głównego skryptu sieciowego na scenie
        _networkClient = FindObjectOfType<UnityNetworkClient>();

        if (_networkClient != null)
        {
            SendStateToDashboard();
        }
        else
        {
            Debug.LogWarning("⚠️ Nie znaleziono UnityNetworkClient! Czy jest na scenie?");
        }
    }

    public void SendStateToDashboard()
    {
        StateUpdateMessage msg = new StateUpdateMessage {
            current_view = sceneName,
            available_actions = actions
        };

        string json = JsonUtility.ToJson(msg);
        _networkClient.SendTextMessage(json); // Musimy dodać tę metodę do UnityNetworkClient
    }
}