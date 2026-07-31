using System.Collections.Generic;
using System;
using UnityEngine;

/// <summary>
/// At ForestWalk scene load, randomly hides 0–40% of the birds in the collection so the
/// visible flock varies from run to run, and reports the remaining (visible) bird count to
/// the dashboard over the existing WebSocket channel as {"type":"bird_count","visible":N}.
/// Operates on the direct children of <see cref="birdsContainer"/> — one child = one bird.
/// Randomization happens once on Start; <see cref="SendCount"/> only re-sends the chosen count.
/// </summary>
public class ForestBirdRandomizer : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Container whose direct children are the birds. Defaults to this object.")]
    [SerializeField] private Transform birdsContainer;

    [Tooltip("Maximum fraction of birds that may be hidden (0.4 = up to 40%).")]
    [Range(0f, 1f)] [SerializeField] private float maxHiddenFraction = 0.4f;

    private UnityNetworkClient _client;
    private int _visibleCount;
    private bool _countReady;   // randomization done
    private bool _sent;         // count acknowledged as sent over an open socket

    void Start()
    {
        RandomizeAndReport();
    }

    void Update()
    {
        // The socket (DontDestroyOnLoad from MainMenu) may not be Open the instant the scene
        // loads, so keep trying until the count actually goes out over an open connection.
        if (_countReady && !_sent)
            SendCount();
    }

    private void RandomizeAndReport()
    {
        Transform container = birdsContainer != null ? birdsContainer : transform;

        var birds = new List<Transform>(container.childCount);
        for (int i = 0; i < container.childCount; i++)
            birds.Add(container.GetChild(i));

        int total = birds.Count;
        if (total == 0)
        {
            _visibleCount = 0;
            _countReady = true;
            Debug.LogWarning("[ForestBirdRandomizer] No birds found in container — sending visible=0.");
            SendCount();
            return;
        }

        // Start from a clean slate, then hide the chosen ones.
        foreach (var bird in birds)
            bird.gameObject.SetActive(true);

        int hidden = Mathf.FloorToInt(UnityEngine.Random.Range(0f, maxHiddenFraction) * total);

        // Partial Fisher–Yates: shuffle the first `hidden` slots to pick distinct birds to hide.
        for (int i = 0; i < hidden; i++)
        {
            int j = UnityEngine.Random.Range(i, total);
            (birds[i], birds[j]) = (birds[j], birds[i]);
            birds[i].gameObject.SetActive(false);
        }

        _visibleCount = total - hidden;
        _countReady = true;
        Debug.Log($"[ForestBirdRandomizer] total={total} hidden={hidden} visible={_visibleCount}");
        SendCount();
    }

    /// <summary>
    /// Sends the already-chosen visible count (does not re-randomize). No-ops silently until
    /// the WebSocket is actually open — Update() keeps calling this until the count goes out.
    /// </summary>
    public void SendCount()
    {
        if (!_countReady)
            return;

        if (_client == null)
            _client = FindFirstObjectByType<UnityNetworkClient>();

        if (_client == null || !_client.IsConnected)
            return; // not ready yet — Update() will retry

        var msg = new BirdCountMessage { visible = _visibleCount };
        _client.SendTextMessage(JsonUtility.ToJson(msg));
        _sent = true;
        Debug.Log($"[ForestBirdRandomizer] Sent bird_count visible={_visibleCount}");
    }

    [Serializable]
    private class BirdCountMessage
    {
        public string type = "bird_count";
        public int visible;
    }
}
