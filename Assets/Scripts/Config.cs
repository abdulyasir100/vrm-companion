using UnityEngine;
using System.IO;

/// <summary>
/// VRMCompanion configuration — all tuneable values in one place.
/// Server IP is loaded from StreamingAssets/server.txt (gitignored).
/// Create that file with just your server IP, e.g.: 100.102.146.85
///
/// On Android, File.ReadAllText can't read StreamingAssets directly,
/// so we use UnityWebRequest via a lazy-load pattern.
/// </summary>
public static class Config
{
    // --- Avatar Server ---
    private const string DefaultServerHost = "127.0.0.1";
    private const int    ServerPort = 8800;

    private static string _serverHost;
    private static bool _serverHostLoaded;

    public static string AvatarServerHost
    {
        get
        {
            if (!_serverHostLoaded)
            {
                _serverHost = LoadServerHostSync();
                _serverHostLoaded = true;
                Debug.Log($"[Config] Server: {_serverHost}:{ServerPort}");
            }
            return _serverHost;
        }
    }

    /// <summary>
    /// Called by AvatarController on Start() to load server.txt via UnityWebRequest on Android.
    /// </summary>
    public static System.Collections.IEnumerator LoadServerHostAsync(System.Action onDone = null)
    {
        if (_serverHostLoaded)
        {
            onDone?.Invoke();
            yield break;
        }

        string path = Path.Combine(Application.streamingAssetsPath, "server.txt");

#if UNITY_ANDROID && !UNITY_EDITOR
        using (var request = UnityEngine.Networking.UnityWebRequest.Get(path))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                string ip = request.downloadHandler.text.Trim();
                if (!string.IsNullOrEmpty(ip))
                {
                    _serverHost = ip;
                    _serverHostLoaded = true;
                    Debug.Log($"[Config] Server (Android): {_serverHost}:{ServerPort}");
                    onDone?.Invoke();
                    yield break;
                }
            }
            Debug.LogWarning($"[Config] Android: server.txt read failed, using default");
        }
#else
        // Editor/PC: synchronous read works fine
        yield return null;
#endif

        if (!_serverHostLoaded)
        {
            _serverHost = LoadServerHostSync();
            _serverHostLoaded = true;
            Debug.Log($"[Config] Server: {_serverHost}:{ServerPort}");
        }
        onDone?.Invoke();
    }

    private static string LoadServerHostSync()
    {
        try
        {
            string path = Path.Combine(Application.streamingAssetsPath, "server.txt");
            if (File.Exists(path))
            {
                string ip = File.ReadAllText(path).Trim();
                if (!string.IsNullOrEmpty(ip))
                    return ip;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Config] Failed to read server.txt: {e.Message}");
        }
        Debug.LogWarning($"[Config] server.txt not found, using default: {DefaultServerHost}");
        return DefaultServerHost;
    }

    public static readonly int AvatarServerPort = ServerPort;
    public static string WebSocketUrl => $"ws://{AvatarServerHost}:{AvatarServerPort}/ws";
    public static string HttpBaseUrl  => $"http://{AvatarServerHost}:{AvatarServerPort}";

    // --- VRM Costumes ---
    public struct CostumeEntry
    {
        public string id;
        public string displayName;
        public string fileName;
    }

    public static readonly CostumeEntry[] Costumes = new CostumeEntry[]
    {
        new CostumeEntry { id = "maid",   displayName = "Maid",   fileName = "SuiseiMaid.vrm" },
        new CostumeEntry { id = "idol",   displayName = "Idol",   fileName = "SuiseiIdol.vrm" },
        new CostumeEntry { id = "casual", displayName = "Casual", fileName = "SuiseiCasual.vrm" },
    };

    public const string DefaultCostumeId = "maid";

    public static string CurrentCostumeId { get; set; } = DefaultCostumeId;

    public static string VrmFileName => GetCostume(CurrentCostumeId).fileName;

    public static CostumeEntry GetCostume(string id)
    {
        foreach (var c in Costumes)
            if (c.id == id) return c;
        return Costumes[0]; // fallback to first entry
    }

    // --- WebSocket ---
    public const float ReconnectBaseDelay  = 2f;    // seconds
    public const float ReconnectMaxDelay   = 30f;   // seconds — exponential backoff cap
    public const float HeartbeatInterval   = 30f;   // seconds between pings
    public const float ConnectionTimeout   = 10f;   // seconds before giving up on connect

    // --- Expressions ---
    public const float ExpressionBlendSpeed = 5f;   // lerp speed for blendshape transitions

    // --- Lip Sync ---
    public const float LipSyncSensitivity  = 30f;   // amplitude multiplier (Qwen3-TTS outputs low amplitude)
    public const float LipSyncSmoothing    = 20f;   // lerp speed for mouth movement

    // --- Idle Behavior ---
    public const float BlinkIntervalMin = 2.5f;     // seconds
    public const float BlinkIntervalMax = 6f;
    public const float BlinkDuration    = 0.15f;    // seconds for one blink
    public const float BreathingSpeed   = 1.2f;     // sine wave frequency
    public const float BreathingAmount  = 0.003f;   // spine Y offset amplitude
    public const float SwaySpeed        = 0.4f;     // body sway frequency
    public const float SwayAmount       = 0.3f;     // degrees of rotation

    // --- Costume Change Animation ---
    public const float WalkOffDuration    = 1.5f;   // seconds to lerp off-screen
    public const float OffScreenPause     = 1.0f;   // seconds to wait off-screen
    public const float WalkOnDuration     = 1.5f;   // seconds to lerp back
    public const float WalkOffScreenX     = 2.0f;   // X distance off-screen (portrait mode)
    public const float PostSwapSettleTime = 0.3f;    // pause after walk-on before speaking

    // --- Orientation ---
    public const bool LockPortrait = true;

    // --- Offline ---
    public const float OfflineThreshold = 15f;      // seconds disconnected before showing offline UI

    // --- Audio ---
    public const float AudioDownloadTimeout = 10f;  // seconds
}
