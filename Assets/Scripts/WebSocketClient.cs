using UnityEngine;
using NativeWebSocket;
using System;
using System.Text;

/// <summary>
/// Connects to the avatar server via WebSocket.
/// Auto-reconnects with exponential backoff.
/// Dispatches incoming messages to AvatarController.
/// </summary>
public class WebSocketClient : MonoBehaviour
{
    [Header("References")]
    public AvatarController avatarController;
    public OfflineMode offlineMode;
    public CostumePicker costumePicker;

    private WebSocket _ws;
    private float _reconnectDelay;
    private float _reconnectTimer;
    private bool _connected;
    private bool _connecting;
    private float _heartbeatTimer;
    private float _lastMessageTime;
    private MoodDisplay _moodDisplay;

    public bool IsConnected => _connected;

    void Start()
    {
        _reconnectDelay = Config.ReconnectBaseDelay;

        // Find mood display in scene
        _moodDisplay = FindObjectOfType<MoodDisplay>();

        Connect();
    }

    void Update()
    {
        // NativeWebSocket requires dispatching messages on the main thread
        if (_ws != null)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            _ws.DispatchMessageQueue();
#endif
        }

        // Heartbeat
        if (_connected)
        {
            _heartbeatTimer += Time.deltaTime;
            if (_heartbeatTimer >= Config.HeartbeatInterval)
            {
                _heartbeatTimer = 0f;
                SendPing();
            }
        }

        // Auto-reconnect
        if (!_connected && !_connecting)
        {
            _reconnectTimer += Time.deltaTime;
            if (_reconnectTimer >= _reconnectDelay)
            {
                _reconnectTimer = 0f;
                Connect();
            }
        }
    }

    async void Connect()
    {
        if (_connecting) return;
        _connecting = true;

        string url = Config.WebSocketUrl;
        Debug.Log($"[WebSocket] Connecting to {url}...");

        try
        {
            _ws = new WebSocket(url);

            _ws.OnOpen += OnOpen;
            _ws.OnMessage += OnMessage;
            _ws.OnClose += OnClose;
            _ws.OnError += OnError;

            await _ws.Connect();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WebSocket] Connect failed: {e.Message}");
            _connecting = false;
            IncreaseReconnectDelay();
        }
    }

    void OnOpen()
    {
        Debug.Log("[WebSocket] Connected");
        _connected = true;
        _connecting = false;
        _reconnectDelay = Config.ReconnectBaseDelay;
        _heartbeatTimer = 0f;
        _lastMessageTime = Time.time;

        if (offlineMode != null)
            offlineMode.OnConnected();

        // Report current costume to server so it stays in sync
        SendCostumeReport();
    }

    void OnMessage(byte[] data)
    {
        _lastMessageTime = Time.time;
        string json = Encoding.UTF8.GetString(data);
        Debug.Log($"[WebSocket] Received: {json}");

        try
        {
            var msg = JsonUtility.FromJson<ServerMessage>(json);

            if (msg == null) return;

            switch (msg.type)
            {
                case "chat":
                    if (avatarController != null)
                    {
                        if (!string.IsNullOrEmpty(msg.costume_id))
                            avatarController.CostumeChangeWithChat(msg.costume_id, msg.reply, msg.emotion, msg.audio_url);
                        else
                            avatarController.HandleChat(msg.reply, msg.emotion, msg.audio_url);

                        // Show subtitle
                        avatarController.ShowSubtitle(msg.reply);
                    }
                    break;

                case "event":
                    if (avatarController != null)
                        avatarController.HandleEvent(msg.source, msg.@event, msg.data);
                    break;

                case "costume":
                    if (!string.IsNullOrEmpty(msg.costume_id))
                    {
                        if (avatarController != null)
                            avatarController.SwitchCostume(msg.costume_id);
                        if (costumePicker != null)
                            costumePicker.OnCostumeChanged(msg.costume_id);
                    }
                    break;

                case "sleep":
                    if (avatarController != null)
                        avatarController.SetSleeping(msg.sleeping);
                    break;

                case "config":
                    if (avatarController != null && avatarController.touchInteraction != null)
                        avatarController.touchInteraction.SetTouchEnabled(msg.touch_enabled);
                    Debug.Log($"[WebSocket] Config update: touch_enabled={msg.touch_enabled}");
                    break;

                case "mood":
                    if (_moodDisplay != null)
                        _moodDisplay.UpdateMood(msg.value);
                    Debug.Log($"[WebSocket] Mood update: {msg.value}");
                    break;

                case "connected":
                    // Welcome message — no action needed
                    break;

                case "pong":
                    // Heartbeat response — do nothing
                    break;

                default:
                    Debug.Log($"[WebSocket] Unknown message type: {msg.type}");
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WebSocket] Failed to parse message: {e.Message}");
        }
    }

    void OnClose(WebSocketCloseCode code)
    {
        Debug.Log($"[WebSocket] Disconnected (code: {code})");
        _connected = false;
        _connecting = false;
        _reconnectTimer = 0f;
        IncreaseReconnectDelay();

        if (offlineMode != null)
            offlineMode.OnDisconnected();
    }

    void OnError(string error)
    {
        Debug.LogWarning($"[WebSocket] Error: {error}");
        _connected = false;
        _connecting = false;
    }

    async void SendPing()
    {
        if (_ws == null || _ws.State != WebSocketState.Open) return;

        try
        {
            await _ws.SendText("{\"type\":\"ping\"}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WebSocket] Ping failed: {e.Message}");
        }
    }

    async void SendCostumeReport()
    {
        if (_ws == null || _ws.State != WebSocketState.Open) return;

        try
        {
            string costumeId = Config.CurrentCostumeId;
            string json = $"{{\"type\":\"costume_report\",\"costume_id\":\"{costumeId}\"}}";
            await _ws.SendText(json);
            Debug.Log($"[WebSocket] Sent costume_report: {costumeId}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WebSocket] Costume report failed: {e.Message}");
        }
    }

    void IncreaseReconnectDelay()
    {
        _reconnectDelay = Mathf.Min(_reconnectDelay * 2f, Config.ReconnectMaxDelay);
        Debug.Log($"[WebSocket] Next reconnect in {_reconnectDelay:F1}s");
    }

    async void OnDestroy()
    {
        if (_ws != null && _ws.State == WebSocketState.Open)
        {
            await _ws.Close();
        }
    }

    private async void OnApplicationQuit()
    {
        if (_ws != null && _ws.State == WebSocketState.Open)
        {
            await _ws.Close();
        }
    }
}

/// <summary>
/// JSON structure matching avatar server WebSocket frames.
/// </summary>
[Serializable]
public class ServerMessage
{
    public string type;      // "chat", "event", "costume", "pong"

    // Chat fields
    public string reply;
    public string emotion;
    public string audio_url;

    // Event fields
    public string source;
    public string @event;    // "event" is a C# keyword, use @
    public string data;

    // Costume fields
    public string costume_id;

    // Sleep fields
    public bool sleeping;

    // Mood fields
    public float value;  // matches server's {"type": "mood", "value": 77.0}

    // Config fields
    public bool touch_enabled;
}
