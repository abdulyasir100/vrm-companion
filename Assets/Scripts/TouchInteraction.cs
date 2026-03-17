using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

/// <summary>
/// Touch interaction for the VRM avatar on mobile:
/// - Single finger drag: rotate the model left/right
/// - Tap (short touch): poke her — sends *poke* to LLM for natural response
/// Attach to the AvatarController GameObject.
/// </summary>
public class TouchInteraction : MonoBehaviour
{
    [Header("Rotation")]
    [SerializeField] private float rotateSpeed = 0.3f;
    [SerializeField] private float rotateSmoothing = 8f;
    [SerializeField] private float returnSpeed = 2f;
    [SerializeField] private float maxRotation = 45f;

    [Header("Poke Detection")]
    [SerializeField] private float tapMaxDuration = 0.3f;
    [SerializeField] private float tapMaxDrag = 20f;
    [SerializeField] private float pokeCooldown = 5f;

    // References (set via Init)
    [HideInInspector] public Transform avatarRoot;
    [HideInInspector] public ExpressionController expressionController;
    [HideInInspector] public AnimationController animationController;
    [HideInInspector] public AudioPlayer audioPlayer;
    [HideInInspector] public CostumePicker costumePicker;
    [HideInInspector] public SubtitleController subtitleController;
    [HideInInspector] public WebSocketClient webSocketClient;

    private float _targetYRotation;
    private float _currentYRotation;
    private bool _isDragging;
    private float _touchStartTime;
    private Vector2 _touchStartPos;
    private float _lastPokeTime = -10f;
    private bool _initialized;
    private bool _touchEnabled = true;

    public void Init(Transform root, ExpressionController expr, AnimationController anim, AudioPlayer audio)
    {
        _initialized = false;
        avatarRoot = root;
        expressionController = expr;
        animationController = anim;
        audioPlayer = audio;
        _initialized = root != null;
        if (_initialized)
            Debug.Log("[TouchInteraction] Initialized");
    }

    /// <summary>Enable or disable touch/poke interaction (rotation always works).</summary>
    public void SetTouchEnabled(bool enabled)
    {
        _touchEnabled = enabled;
        Debug.Log($"[TouchInteraction] Touch {(enabled ? "enabled" : "disabled")}");
    }

    void Update()
    {
        if (!_initialized || avatarRoot == null) return;

        HandleTouch();
        UpdateRotation();
    }

    bool IsInUIRegion(Vector2 screenPos)
    {
        if (costumePicker != null && costumePicker.IsInButtonArea(screenPos))
            return true;
        return false;
    }

    void HandleTouch()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);

            // Ignore touches on UI elements (costume picker button)
            if (touch.phase == TouchPhase.Began && IsInUIRegion(touch.position))
                return;

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    OnTouchStart(touch.position);
                    break;
                case TouchPhase.Moved:
                    OnTouchDrag(touch.deltaPosition.x);
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    OnTouchEnd(touch.position);
                    break;
            }
        }
        else if (Input.touchCount == 0)
        {
            // Mouse fallback for editor testing
            if (Input.GetMouseButtonDown(0))
            {
                if (IsInUIRegion(Input.mousePosition)) return;
                OnTouchStart(Input.mousePosition);
            }
            else if (Input.GetMouseButton(0))
                OnTouchDrag(Input.GetAxis("Mouse X") * 10f);
            else if (Input.GetMouseButtonUp(0))
                OnTouchEnd(Input.mousePosition);
        }

        // When not touching, slowly return to forward-facing
        if (!_isDragging)
        {
            _targetYRotation = Mathf.Lerp(_targetYRotation, 0f, Time.deltaTime * returnSpeed);
        }
    }

    void OnTouchStart(Vector2 pos)
    {
        _isDragging = true;
        _touchStartTime = Time.time;
        _touchStartPos = pos;
    }

    void OnTouchDrag(float deltaX)
    {
        _targetYRotation -= deltaX * rotateSpeed;
        _targetYRotation = Mathf.Clamp(_targetYRotation, -maxRotation, maxRotation);
    }

    void OnTouchEnd(Vector2 pos)
    {
        _isDragging = false;

        float duration = Time.time - _touchStartTime;
        float drag = Vector2.Distance(_touchStartPos, pos);

        if (duration < tapMaxDuration && drag < tapMaxDrag)
        {
            OnPoke();
        }
    }

    void OnPoke()
    {
        // Touch disabled — only rotation works
        if (!_touchEnabled) return;

        // Cooldown — don't allow spamming while LLM processes
        if (Time.time - _lastPokeTime < pokeCooldown) return;
        _lastPokeTime = Time.time;

        Debug.Log("[TouchInteraction] Poked! Sending to LLM via /chat");

        // Send *poke* to LLM — server handles everything (LLM response, TTS, WebSocket broadcast)
        StartCoroutine(SendPokeToChat());
    }

    IEnumerator SendPokeToChat()
    {
        string url = Config.HttpBaseUrl + "/chat";
        string json = JsonUtility.ToJson(new ChatRequest
        {
            message = "[SYSTEM: User poked/touched the avatar screen. React naturally based on your mood.] *poke*",
            context = "tablet_touch",
            user_name = "User"
        });

        using (var request = new UnityWebRequest(url, "POST"))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 30;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[TouchInteraction] Chat request failed: {request.error}");
            }
            else
            {
                Debug.Log($"[TouchInteraction] Chat response: {request.downloadHandler.text}");
            }
            // Response arrives via WebSocket — no local handling needed
        }
    }

    void UpdateRotation()
    {
        _currentYRotation = Mathf.Lerp(_currentYRotation, _targetYRotation, Time.deltaTime * rotateSmoothing);
        avatarRoot.localRotation = Quaternion.Euler(0f, _currentYRotation, 0f);
    }

    // JSON serialization helper
    [System.Serializable]
    private class ChatRequest
    {
        public string message;
        public string context;
        public string user_name;
    }
}
