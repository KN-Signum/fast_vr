using System;
using System.Collections;
using UnityEngine;
using NativeWebSocket;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

[Serializable]
public class GameCommand
{
    public string type;   // np. "command"
    public string action; // np. "open_menu", "restart_game"
}

public class UnityNetworkClient : MonoBehaviour
{

    private static UnityNetworkClient _instance;
    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject); // Usuwamy duplikat przy powrocie do Menu
            return;
        }
        _instance = this;
        DontDestroyOnLoad(this.gameObject); // To sprawia, że połączenie trwa!
    }

    [Header("Beacon Settings")]
    public int BeaconPort = 15000;
    public string ExpectedService = "hrv-biofeedback";   // must match beacon_manager.py

    [Header("Network Settings")]
    private WebSocket _websocket;

    // Beacon discovery
    private UdpClient _udpClient;
    private Thread _listenThread;
    private bool _listening;
    private string _discoveredUrl;   // written on background thread, read on main thread

    [Header("Capture Settings")]
    public RenderTexture dashboardRT;
    [Range(1, 30)] public int targetFPS = 20;
    [Range(10, 100)] public int jpgQuality = 80;

    private Texture2D _tex;
    private bool _isBusy = false;
    private float _nextFrameTime = 0f;

    void Start()
    {
        _tex = new Texture2D(dashboardRT.width, dashboardRT.height, TextureFormat.RGB24, false);
        Debug.Log($"🔍 Listening for server beacon on UDP port {BeaconPort}...");
        StartBeaconListener();
    }

    void Update()
    {

    // Check if beacon thread discovered the server
    if (_discoveredUrl != null && _websocket == null)
    {
        string url = _discoveredUrl;
        _discoveredUrl = null;
        StopBeaconListener();
        ConnectWebSocket(url);
    }

        if (_websocket != null)
        {
            _websocket.DispatchMessageQueue();
        }

        if (_websocket != null && _websocket.State == WebSocketState.Open && !_isBusy && Time.time >= _nextFrameTime)
        {
            StartCoroutine(CaptureAndSend());
            _nextFrameTime = Time.time + (1f / targetFPS);
        }
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
                // MENU
            case "next_to_selection":
                // Znajdujemy menu i przełączamy panel
                FindFirstObjectByType<VRMainMenu>()?.ShowMenu();
                break;
            case "start_forest":
                SceneLoader.Load(GameSceneNames.ForestWalk);
                break;
            case "start_painting":
                SceneLoader.Load(GameSceneNames.PaintingGame);
                break;
            case "exit_app":
                Application.Quit();
                break;

                // MALOWANIE
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
                SceneLoader.Load(GameSceneNames.MainMenu);
                break;

                // FOREST WALK
            case "start_walk":
                FindFirstObjectByType<ForestWalkController>()?.StartWalk();
                break;
            case "pause_walk":
                FindFirstObjectByType<ForestWalkController>()?.PauseWalk();
                break;
            case "resume_walk":
                FindFirstObjectByType<ForestWalkController>()?.ResumeWalk();
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

// ── Beacon Discovery ───────────────────────────────────────

private void StartBeaconListener()
{
    Debug.Log("DEBUG: start beacon dupa");
    _udpClient = new UdpClient(BeaconPort);
    _udpClient.EnableBroadcast = true;
    _listening = true;

    _listenThread = new Thread(ListenLoop)
    {
        IsBackground = true,
        Name = "BeaconListener"
    };
    _listenThread.Start();
}

private void ListenLoop()
{
    var endPoint = new IPEndPoint(IPAddress.Any, BeaconPort);

    while (_listening)
    {
        try
        {
            byte[] data = _udpClient.Receive(ref endPoint);
            string json = Encoding.UTF8.GetString(data);
            var beacon = JsonUtility.FromJson<BeaconPayload>(json);

            if (beacon != null && beacon.service == ExpectedService)
            {
                Debug.Log($"[Beacon] Server found at {beacon.ws_url}");
                _discoveredUrl = beacon.ws_url;   // picked up by Update()
                return;
            }
            Debug.Log("DEBUG: beacon not found");
        }
        catch (SocketException ex)
        {
            if (_listening)
                Debug.LogWarning($"[Beacon] Socket error: {ex.Message}");
        }
    }
}

private void StopBeaconListener()
{
    _listening = false;
    _udpClient?.Close();
    _udpClient = null;
}

private async void ConnectWebSocket(string url)
{
    Debug.Log($"🔗 Connecting to {url}...");
    _websocket = new WebSocket(url);

    _websocket.OnOpen  += () => Debug.Log("✅ Połączono! Czekam na komendy z Dashboardu...");
    _websocket.OnError += (e) => Debug.LogError($"❌ Błąd WS: {e}");
    _websocket.OnMessage += (bytes) =>
    {
        string msg = System.Text.Encoding.UTF8.GetString(bytes);
        HandleIncomingCommand(msg);
    };

    try {
        await _websocket.Connect();
    } catch (Exception e) {
        Debug.LogError($"💥 Wyjątek przy łączeniu: {e.Message}");
    }
}

IEnumerator CaptureAndSaveResult()
{
    _isBusy = true;
    yield return new WaitForEndOfFrame();

    // 1. Find the brush script to get the actual painted texture
    var brush = FindFirstObjectByType<VRPaintBrush>();
    
    if (brush != null && brush.GetActiveTexture() != null)
    {
        Texture2D paintedTex = brush.GetActiveTexture();
        
        // 2. Encode the actual drawing texture directly to JPG
        // This ignores the 3D world, lighting, and cameras
        byte[] jpgData = paintedTex.EncodeToJPG(80);
        string base64Image = System.Convert.ToBase64String(jpgData);

        string jsonResponse = "{\"type\": \"canvas_image\", \"image_base64\": \"" + base64Image + "\", \"format\": \"jpg\"}";
        _ = _websocket.SendText(jsonResponse);

        Debug.Log("🖼️ Wysłano czysty obraz płótna do Dashboardu!");
    }
    else
    {
        Debug.LogError("❌ Nie znaleziono tekstury malunku!");
    }

    _isBusy = false;
}

    // --- LOGIKA TWOJEJ GRY ---
    private void OpenMenu() {
        Debug.Log("Wywołuję menu w grze...");
        // Tutaj Twój kod: MenuManager.Instance.Show();
    }

    private void RestartGame() {
        Debug.Log("Restartuję scenę...");
        SceneLoader.LoadActiveScene();
    }

    private async void OnApplicationQuit()
    {
        StopBeaconListener();
        if (_websocket != null) await _websocket.Close();
    }

    private void OnDestroy()
    {
        StopBeaconListener();
    }

    [Serializable]
    private class BeaconPayload
    {
        public string service;
        public string ws_url;
    }
}

