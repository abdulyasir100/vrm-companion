using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

/// <summary>
/// Touch interaction for the VRM avatar on mobile:
/// - Single finger drag: rotate the model left/right
/// - Tap (short touch): poke her — she gets angry and says something!
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
    [SerializeField] private float angryDuration = 4f;
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
    private float _angryTimer;
    private bool _isAngry;
    private bool _isSpeaking;
    private bool _initialized;

    // Angry poke responses — Suisei's voice lines when you tap her
    private static readonly string[] PokeLines = new string[]
    {
        // Annoyed / stop it
        "Hey! Don't just poke me like that. I'm not a toy.",
        "Excuse me? Do I look like a touchscreen button to you?",
        "Stop it. I'm warning you. One more time and I'm not talking to you for an hour.",
        "You really have nothing better to do, huh?",
        "Oi. Hands off. Seriously.",

        // Threatening / competitive
        "Do that again and I'll make you regret it. I don't lose, remember?",
        "You're testing Sui-chan's patience right now. Bad idea.",
        "I will end you. Figuratively. Maybe.",
        "Keep poking me and see what happens. I dare you.",

        // Tsundere annoyed (secretly doesn't mind THAT much)
        "Why are you like this? Seriously, why?",
        "You're so annoying. The most annoying person I know. And I know a lot of people.",
        "I didn't sign up for this. I signed up for singing and being amazing.",
        "Are you done? Please tell me you're done.",
        "This is exactly why I can't have nice things.",
    };

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

    void Update()
    {
        if (!_initialized || avatarRoot == null) return;

        HandleTouch();
        UpdateRotation();
        UpdateAngryTimer();
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
        // Cooldown — don't allow spamming (TTS needs time)
        if (Time.time - _lastPokeTime < pokeCooldown) return;
        if (_isSpeaking) return;
        _lastPokeTime = Time.time;

        // Pick a random angry line
        string line = PokeLines[Random.Range(0, PokeLines.Length)];
        Debug.Log($"[TouchInteraction] Poked! Line: {line}");

        // Set angry expression + animation
        if (expressionController != null)
            expressionController.SetEmotion("ANGRY");
        if (animationController != null)
            animationController.PlayEmotion("ANGRY");

        _isAngry = true;
        _angryTimer = angryDuration;

        // Show subtitle
        if (subtitleController != null)
            subtitleController.Show(line, null);

        // Request TTS from the server and play it
        StartCoroutine(SpeakLine(line));

        // Notify Telegram (fire-and-forget)
        StartCoroutine(NotifyTelegram(line));
    }

    IEnumerator SpeakLine(string text)
    {
        _isSpeaking = true;

        // POST /tts to avatar server
        string url = Config.HttpBaseUrl + "/tts";
        string json = JsonUtility.ToJson(new TTSRequest { text = text });

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
                Debug.LogWarning($"[TouchInteraction] TTS request failed: {request.error}");
                _isSpeaking = false;
                yield break;
            }

            // Parse response to get audio_url
            var response = JsonUtility.FromJson<TTSResponseData>(request.downloadHandler.text);
            if (string.IsNullOrEmpty(response.audio_url))
            {
                Debug.LogWarning("[TouchInteraction] TTS returned no audio_url");
                _isSpeaking = false;
                yield break;
            }

            string audioUrl = response.audio_url.StartsWith("http")
                ? response.audio_url
                : Config.HttpBaseUrl + response.audio_url;

            Debug.Log($"[TouchInteraction] Playing angry voice: {audioUrl}");

            // Play through AudioPlayer (triggers lip sync automatically)
            if (audioPlayer != null)
            {
                // Extend angry duration to cover speech
                _angryTimer = Mathf.Max(_angryTimer, 10f);

                audioPlayer.PlayFromUrl(audioUrl, () =>
                {
                    Debug.Log("[TouchInteraction] Angry voice finished");
                    _isSpeaking = false;
                    // Let the angry timer handle returning to neutral
                    _angryTimer = 1f;
                });
            }
            else
            {
                _isSpeaking = false;
            }
        }
    }

    void UpdateRotation()
    {
        _currentYRotation = Mathf.Lerp(_currentYRotation, _targetYRotation, Time.deltaTime * rotateSmoothing);
        avatarRoot.localRotation = Quaternion.Euler(0f, _currentYRotation, 0f);
    }

    void UpdateAngryTimer()
    {
        if (!_isAngry) return;

        _angryTimer -= Time.deltaTime;
        if (_angryTimer <= 0f && !_isSpeaking)
        {
            _isAngry = false;
            if (expressionController != null)
                expressionController.SetEmotion("NEUTRAL");
            if (animationController != null)
                animationController.PlayEmotion("NEUTRAL");
        }
    }

    /// <summary>Send poke response to Telegram via /chat endpoint (fire-and-forget).</summary>
    IEnumerator NotifyTelegram(string line)
    {
        // POST /event for poke
        string eventUrl = Config.HttpBaseUrl + "/event";
        // Build JSON manually — data must be a dict (not string) for Pydantic
        string escapedLine = line.Replace("\\", "\\\\").Replace("\"", "\\\"");
        string eventJson = $"{{\"source\":\"tablet\",\"event\":\"poke\",\"data\":{{\"line\":\"{escapedLine}\"}}}}";

        using (var request = new UnityWebRequest(eventUrl, "POST"))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(eventJson);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                Debug.LogWarning($"[TouchInteraction] Telegram notify failed: {request.error}");
        }
    }

    // JSON serialization helpers
    [System.Serializable]
    private class TTSRequest
    {
        public string text;
    }

    [System.Serializable]
    private class TTSResponseData
    {
        public string audio_url;
        public string voice;
    }

    // PokeEvent JSON is built manually in NotifyTelegram() to ensure
    // the "data" field serializes as a dict (not string) for FastAPI.
}
