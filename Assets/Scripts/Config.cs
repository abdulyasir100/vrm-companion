/// <summary>
/// VRMCompanion configuration — all tuneable values in one place.
/// Attach to an empty GameObject named "Config" in the scene,
/// or just reference the static defaults directly.
/// </summary>
public static class Config
{
    // --- Avatar Server ---
    public const string AvatarServerHost = "100.83.33.113";
    public const int    AvatarServerPort = 8800;
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
