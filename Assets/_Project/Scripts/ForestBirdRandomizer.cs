using System.Collections.Generic;
using System;
using UnityEngine;

/// <summary>
/// At ForestWalk scene load, randomly hides 0–40% of the birds on each side of the path so the
/// visible flock varies from run to run, and reports the remaining (visible) bird counts to the
/// dashboard over the existing WebSocket channel as
/// {"type":"bird_count","visible":N,"left":L,"right":R}.
///
/// Birds are grouped under two child containers of <see cref="birdsContainer"/> named "Left" and
/// "Right" (case-insensitive); each direct child of a group = one bird. The hide fraction is
/// applied independently per side, so both sides always keep some birds. If no Left/Right groups
/// are found, all direct children are treated as a single unsided flock (left/right reported 0).
/// Randomization happens once on Start; <see cref="SendCount"/> only re-sends the chosen counts.
/// </summary>
public class ForestBirdRandomizer : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Container holding the Left/Right bird groups. Defaults to this object.")]
    [SerializeField] private Transform birdsContainer;

    [Tooltip("Maximum fraction of birds that may be hidden per side (0.4 = up to 40%).")]
    [Range(0f, 1f)] [SerializeField] private float maxHiddenFraction = 0.4f;

    private UnityNetworkClient _client;
    private int _visibleCount;
    private int _leftVisible;
    private int _rightVisible;
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
        Transform root = birdsContainer != null ? birdsContainer : transform;

        Transform left = FindGroup(root, "Left");
        Transform right = FindGroup(root, "Right");

        if (left == null && right == null)
        {
            // No sided grouping — fall back to the whole flock as a single unsided group.
            Debug.LogWarning("[ForestBirdRandomizer] No 'Left'/'Right' groups found — treating " +
                             "all direct children as one unsided flock.");
            _leftVisible = 0;
            _rightVisible = 0;
            _visibleCount = RandomizeGroup(root);
        }
        else
        {
            _leftVisible = RandomizeGroup(left);
            _rightVisible = RandomizeGroup(right);
            _visibleCount = _leftVisible + _rightVisible;
        }

        _countReady = true;
        Debug.Log($"[ForestBirdRandomizer] visible={_visibleCount} left={_leftVisible} right={_rightVisible}");
        SendCount();
    }

    /// <summary>
    /// Hides a random 0–<see cref="maxHiddenFraction"/> of <paramref name="group"/>'s direct
    /// children and returns how many remain visible. Null/empty groups return 0.
    /// </summary>
    private int RandomizeGroup(Transform group)
    {
        if (group == null)
            return 0;

        var birds = new List<Transform>(group.childCount);
        for (int i = 0; i < group.childCount; i++)
            birds.Add(group.GetChild(i));

        int total = birds.Count;
        if (total == 0)
            return 0;

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

        return total - hidden;
    }

    private static Transform FindGroup(Transform root, string name)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase))
                return child;
        }
        return null;
    }

    /// <summary>
    /// Sends the already-chosen visible counts (does not re-randomize). No-ops silently until
    /// the WebSocket is actually open — Update() keeps calling this until the counts go out.
    /// </summary>
    public void SendCount()
    {
        if (!_countReady)
            return;

        if (_client == null)
            _client = FindFirstObjectByType<UnityNetworkClient>();

        if (_client == null || !_client.IsConnected)
            return; // not ready yet — Update() will retry

        var msg = new BirdCountMessage
        {
            visible = _visibleCount,
            left = _leftVisible,
            right = _rightVisible
        };
        _client.SendTextMessage(JsonUtility.ToJson(msg));
        _sent = true;
        Debug.Log($"[ForestBirdRandomizer] Sent bird_count visible={_visibleCount} left={_leftVisible} right={_rightVisible}");
    }

    [Serializable]
    private class BirdCountMessage
    {
        public string type = "bird_count";
        public int visible;
        public int left;
        public int right;
    }
}
