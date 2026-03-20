using UnityEngine;
using NativeWebSocket;
using System.Collections;
using System;
using UnityEngine.SceneManagement; // Potrzebne do restartu gry

[Serializable]
public class GameCommand
{
    public string type;   // np. "command"
    public string action; // np. "open_menu", "restart_game"
}

public class UnityNetworkClient : MonoBehaviour
{
    [Header("Network Settings")]
    public string serverUrl = "ws://127.0.0.1:8000/ws";
    private WebSocket _websocket;

    [Header("Capture Settings")]
    public RenderTexture dashboardRT;
    [Range(1, 30)] public int targetFPS = 20;
    [Range(10, 100)] public int jpgQuality = 80;

    private Texture2D _tex;
    private bool _isBusy = false;
    private float _nextFrameTime = 0f;

    async void Start()
    {
        _tex = new Texture2D(dashboardRT.width, dashboardRT.height, TextureFormat.RGB24, false);
        _websocket = new WebSocket(serverUrl);

        _websocket.OnOpen += () => Debug.Log("✅ Połączono! Czekam na komendy z Dashboardu...");
        _websocket.OnError += (e) => Debug.LogError($"❌ Błąd WS: {e}");
        
        _websocket.OnMessage += (bytes) =>
        {
            string msg = System.Text.Encoding.UTF8.GetString(bytes);
            HandleIncomingCommand(msg);
        };

        await _websocket.Connect();
    }

    void Update()
    {
        #if !UNITY_WEBGL || UNITY_EDITOR
            _websocket.DispatchMessageQueue();
        #endif

        if (_websocket.State == WebSocketState.Open && !_isBusy && Time.time >= _nextFrameTime)
        {
            StartCoroutine(CaptureAndSend());
            _nextFrameTime = Time.time + (1f / targetFPS);
        }
    }

    private static UnityNetworkClient _instance;

    void Awake()
    {
        // Jeśli już istnieje instancja, zniszcz ten duplikat
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        _instance = this;
        // To sprawia, że obiekt przetrwa ładowanie nowych scen!
        DontDestroyOnLoad(this.gameObject);
    }

        public void SendTextMessage(string json)
    {
        if (_websocket != null && _websocket.State == WebSocketState.Open)
        {
            _ = _websocket.SendText(json);
            Debug.Log($"📤 Wysłano stan do Dashboardu: {json}");
        }
    }

    IEnumerator CaptureAndSend()
    {
        _isBusy = true;
        RenderTexture.active = dashboardRT;
        _tex.ReadPixels(new Rect(0, 0, dashboardRT.width, dashboardRT.height), 0, 0);
        _tex.Apply();

        byte[] jpgData = _tex.EncodeToJPG(jpgQuality);

        if (_websocket.State == WebSocketState.Open)
        {
            _ = _websocket.Send(jpgData); 
        }

        _isBusy = false;
        yield return null;
    }

    private void HandleIncomingCommand(string json)
{
    try {
        GameCommand cmd = JsonUtility.FromJson<GameCommand>(json);
        if (cmd.type != "command") return;

        Debug.Log($"🕹️ Akcja z Dashboardu: {cmd.action}");

        switch (cmd.action)
        {
            case "clear_palette":
                // Naprawione: Usunięto niejednoznaczne "Object."
                FindFirstObjectByType<VRPaintBrush>()?.ClearCanvas();
                break;
            case "save_painting":
                StartCoroutine(CaptureAndSaveResult());
                break;
            case "next_image":
                // Naprawione: Usunięto niejednoznaczne "Object."
                FindFirstObjectByType<VRPaintBrush>()?.LoadNextReference();
                break;
            case "back_to_menu":
                SceneManager.LoadScene("MainSelection");
                break;

            case "request_state":
                // Szukamy kontrolera sceny i prosimy go o ponowne wysłanie danych
                FindFirstObjectByType<SceneStateSync>()?.SendStateToDashboard();
                break;
        }
    } catch (Exception e) {
        Debug.LogError("Błąd parsowania komendy: " + e.Message);
    }
}

IEnumerator CaptureAndSaveResult()
{
    _isBusy = true;
    yield return new WaitForEndOfFrame();

    // Używamy Twojego RT, na którym gracz maluje
    RenderTexture.active = dashboardRT;
    _tex.ReadPixels(new Rect(0, 0, dashboardRT.width, dashboardRT.height), 0, 0);
    _tex.Apply();

    byte[] jpgData = _tex.EncodeToJPG(80);
    string base64Image = System.Convert.ToBase64String(jpgData);

    // Wysyłamy JSON, który Twój Flutter już potrafi obsłużyć i pobrać!
    string jsonResponse = "{\"type\": \"canvas_image\", \"image_base64\": \"" + base64Image + "\", \"format\": \"jpg\"}";
    _websocket.SendText(jsonResponse);

    Debug.Log("🖼️ Wysłano obraz malunku do Dashboardu!");
    _isBusy = false;
}

    // --- LOGIKA TWOJEJ GRY ---
    private void OpenMenu() {
        Debug.Log("Wywołuję menu w grze...");
        // Tutaj Twój kod: MenuManager.Instance.Show();
    }

    private void RestartGame() {
        Debug.Log("Restartuję scenę...");
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private async void OnApplicationQuit()
    {
        if (_websocket != null) await _websocket.Close();
    }
}