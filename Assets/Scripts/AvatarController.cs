using UnityEngine;
using UnityEngine.Networking;
using UniVRM10;
using TMPro;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Root controller — loads the VRM avatar at runtime and holds references
/// to all sub-controllers. Attach to an empty GameObject in the scene.
/// </summary>
[DefaultExecutionOrder(100)]
public class AvatarController : MonoBehaviour
{
    // References (auto-wired at runtime)
    public Vrm10Instance VrmInstance { get; private set; }
    public Animator AvatarAnimator { get; private set; }

    // Drag the AvatarAnimator controller here in the Inspector
    [SerializeField] private RuntimeAnimatorController animatorController;

    // Sub-controllers (assigned in Inspector or found at runtime)
    [HideInInspector] public ExpressionController expressionController;
    [HideInInspector] public AnimationController animationController;
    [HideInInspector] public AudioPlayer audioPlayer;
    [HideInInspector] public LipSyncController lipSyncController;
    [HideInInspector] public IdleBehavior idleBehavior;
    [HideInInspector] public TouchInteraction touchInteraction;
    [HideInInspector] public SubtitleController subtitleController;

    private bool _loaded;
    private bool _isSwitching;
    private bool _isCostumeAnimating;
    private string _currentCostumeId;
    private SleepOverlay _sleepOverlay;
    private bool _isSleepingState; // persists across costume changes

    // Default transform — stored at Start(), used as walk-on target
    private Vector3 _defaultPosition;
    private Quaternion _defaultRotation;

    // Chat messages queued during costume animation
    private struct QueuedChat
    {
        public string reply;
        public string emotion;
        public string audioUrl;
    }
    private readonly Queue<QueuedChat> _chatQueue = new Queue<QueuedChat>();

    async void Start()
    {
        // Lock to portrait orientation
        if (Config.LockPortrait)
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
        }

        // Ensure camera framing is set up for any screen size
        SetupCameraFraming();

        // Store default position before loading avatar
        _defaultPosition = transform.position;
        _defaultRotation = transform.rotation;

        _currentCostumeId = Config.CurrentCostumeId;
        await LoadAvatar();

        if (_loaded)
        {
            InitSubControllers();
        }
    }

    void SetupCameraFraming()
    {
        Camera cam = Camera.main;
        if (cam != null && cam.GetComponent<CameraFraming>() == null)
        {
            cam.gameObject.AddComponent<CameraFraming>();
            Debug.Log("[AvatarController] Added CameraFraming to Main Camera");
        }
    }

    async Task LoadAvatar()
    {
        Debug.Log($"[AvatarController] Loading VRM: {Config.VrmFileName}...");

        byte[] vrmBytes = null;

        try
        {
            // On Android, StreamingAssets is inside the APK (jar:file://...)
            // and cannot be accessed via File.Exists/ReadAllBytes.
            // Use UnityWebRequest which handles all platforms.
            string uri = Path.Combine(Application.streamingAssetsPath, Config.VrmFileName);

#if UNITY_ANDROID && !UNITY_EDITOR
            // Android needs a proper URI
            uri = Application.streamingAssetsPath + "/" + Config.VrmFileName;
#endif

            Debug.Log($"[AvatarController] Loading VRM from: {uri}");

            using (var request = UnityWebRequest.Get(uri))
            {
                var op = request.SendWebRequest();
                while (!op.isDone)
                    await Task.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[AvatarController] Failed to load VRM: {request.error}");
                    return;
                }

                vrmBytes = request.downloadHandler.data;
                Debug.Log($"[AvatarController] VRM loaded: {vrmBytes.Length / 1024}KB");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AvatarController] Failed to read VRM file: {e}");
            return;
        }

        try
        {
            VrmInstance = await Vrm10.LoadBytesAsync(
                vrmBytes,
                canLoadVrm0X: true,
                controlRigGenerationOption: ControlRigGenerationOption.Generate,
                showMeshes: true
            );

            if (VrmInstance == null)
            {
                Debug.LogError("[AvatarController] Vrm10.LoadBytesAsync returned null");
                return;
            }

            // Position the avatar — VRM origin is at feet, offset up so she's centered in camera
            Transform avatarRoot = VrmInstance.transform;
            avatarRoot.SetParent(transform, false);
            avatarRoot.localPosition = Vector3.zero;
            avatarRoot.localRotation = Quaternion.identity;

            // Grab the Animator (control rig provides it)
            AvatarAnimator = VrmInstance.GetComponent<Animator>();

            // Assign the Animator Controller so she plays animations instead of T-posing
            if (AvatarAnimator != null && animatorController != null)
            {
                AvatarAnimator.runtimeAnimatorController = animatorController;
                Debug.Log("[AvatarController] Animator Controller assigned");
            }

            // Switch VRM processing to Update so our LateUpdate scripts
            // can set expression weights AFTER animations but BEFORE next render
            VrmInstance.UpdateType = Vrm10Instance.UpdateTypes.None;

            _loaded = true;
            Debug.Log("[AvatarController] VRM loaded successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AvatarController] Failed to load VRM: {e}");
        }
    }

    void InitSubControllers()
    {
        // Find sub-controllers on this GameObject or children
        expressionController = GetComponentInChildren<ExpressionController>();
        animationController  = GetComponentInChildren<AnimationController>();
        audioPlayer          = GetComponentInChildren<AudioPlayer>();
        lipSyncController    = GetComponentInChildren<LipSyncController>();
        idleBehavior         = GetComponentInChildren<IdleBehavior>();

        // Pass VRM references to sub-controllers
        if (expressionController != null)
            expressionController.Init(VrmInstance);

        if (animationController != null)
            animationController.Init(AvatarAnimator);

        if (lipSyncController != null)
        {
            lipSyncController.audioPlayer = audioPlayer;
            lipSyncController.Init(VrmInstance);
        }

        if (idleBehavior != null)
            idleBehavior.Init(VrmInstance);

        // Setup touch interaction (rotate + poke with voice)
        touchInteraction = GetComponentInChildren<TouchInteraction>();
        if (touchInteraction == null)
            touchInteraction = gameObject.AddComponent<TouchInteraction>();
        touchInteraction.Init(VrmInstance.transform, expressionController, animationController, audioPlayer);

        // Find or add subtitle controller
        subtitleController = FindObjectOfType<SubtitleController>();

        // Wire extra references for touch interaction
        var costumePicker = FindObjectOfType<CostumePicker>();
        if (costumePicker != null)
            touchInteraction.costumePicker = costumePicker;
        touchInteraction.subtitleController = subtitleController;
        touchInteraction.webSocketClient = FindObjectOfType<WebSocketClient>();

        // Debug: list all available expressions on this VRM
        if (VrmInstance?.Runtime?.Expression != null)
        {
            var keys = VrmInstance.Runtime.Expression.ExpressionKeys;
            Debug.Log($"[AvatarController] VRM has {keys.Count} expressions:");
            foreach (var key in keys)
            {
                Debug.Log($"  - {key}");
            }
        }

        // Create sleep overlay (ZZZ text above head)
        CreateSleepOverlay();

        Debug.Log("[AvatarController] Sub-controllers initialized");
    }

    /// <summary>
    /// Creates the floating ZZZ overlay at runtime — no manual Unity Editor setup needed.
    /// Spawns a world-space Canvas above the avatar's head with TextMeshPro text.
    /// </summary>
    void CreateSleepOverlay()
    {
        // Clean up previous overlay if switching costumes
        if (_sleepOverlay != null)
        {
            Destroy(_sleepOverlay.gameObject);
            _sleepOverlay = null;
        }

        if (VrmInstance == null) return;

        // Find the head bone for positioning
        Transform head = null;
        if (VrmInstance.TryGetBoneTransform(HumanBodyBones.Head, out Transform h))
            head = h;

        if (head == null)
        {
            Debug.LogWarning("[AvatarController] No head bone found, skipping sleep overlay");
            return;
        }

        // Create Canvas GameObject as child of head
        var canvasGO = new GameObject("SleepOverlayCanvas");
        canvasGO.transform.SetParent(head, false);
        canvasGO.transform.localPosition = new Vector3(0.15f, 0.25f, 0f); // Above and slightly right of head

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;

        // Size the canvas rect
        var canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(0.5f, 0.3f);
        canvasRect.localScale = Vector3.one * 0.005f; // Scale down for world-space

        var canvasGroup = canvasGO.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        // Create text child
        var textGO = new GameObject("ZzzText");
        textGO.transform.SetParent(canvasGO.transform, false);

        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "z";
        tmp.fontSize = 36;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.7f, 0.85f, 1f, 1f); // Soft blue

        // Add the SleepOverlay behavior and inject references before deactivating
        _sleepOverlay = textGO.AddComponent<SleepOverlay>();
        _sleepOverlay.InjectReferences(canvasGroup, canvasGO.transform);

        // Start hidden
        canvasGO.SetActive(false);

        Debug.Log("[AvatarController] Sleep overlay created programmatically");
    }

    /// <summary>
    /// Called by WebSocketClient when a chat message arrives.
    /// Orchestrates expression + animation + audio playback.
    /// </summary>
    public void HandleChat(string reply, string emotion, string audioUrl)
    {
        // Queue messages that arrive during costume animation
        if (_isCostumeAnimating || _isSwitching)
        {
            Debug.Log("[AvatarController] Queuing chat during costume animation");
            _chatQueue.Enqueue(new QueuedChat { reply = reply, emotion = emotion, audioUrl = audioUrl });
            return;
        }

        if (!_loaded)
        {
            Debug.LogWarning("[AvatarController] HandleChat called but VRM not loaded yet!");
            return;
        }

        Debug.Log($"[AvatarController] HandleChat: emotion={emotion}, audioUrl={audioUrl}");
        Debug.Log($"[AvatarController] expressionController={expressionController != null}, animationController={animationController != null}, audioPlayer={audioPlayer != null}");

        // Set expression
        if (expressionController != null)
            expressionController.SetEmotion(emotion);

        // Trigger animation
        if (animationController != null)
            animationController.PlayEmotion(emotion);

        // Play audio with lip sync
        if (string.IsNullOrEmpty(audioUrl))
        {
            Debug.LogWarning("[AvatarController] No audio URL provided");
        }
        else if (audioPlayer == null)
        {
            Debug.LogWarning("[AvatarController] audioPlayer is NULL — cannot play audio");
        }
        else
        {
            string fullUrl = audioUrl.StartsWith("http")
                ? audioUrl
                : Config.HttpBaseUrl + audioUrl;

            Debug.Log($"[AvatarController] Playing audio from: {fullUrl}");

            // Switch to talking animation only for NEUTRAL — keep emotion animation for others
            if (animationController != null && emotion.ToUpper() == "NEUTRAL")
                animationController.PlayEmotion("TALKING");

            audioPlayer.PlayFromUrl(fullUrl, () =>
            {
                // Audio finished — return to neutral
                if (expressionController != null)
                    expressionController.SetEmotion("NEUTRAL");
                if (animationController != null)
                    animationController.PlayEmotion("NEUTRAL");
            });
        }
    }

    /// <summary>
    /// Called by WebSocketClient when an event arrives (motion, camera, etc.)
    /// </summary>
    public void HandleEvent(string source, string eventType, string data)
    {
        if (!_loaded) return;

        Debug.Log($"[AvatarController] HandleEvent: source={source}, event={eventType}");

        // React to motion alerts
        if (eventType == "motion_alert")
        {
            if (expressionController != null)
                expressionController.SetEmotion("SURPRISED");
            if (animationController != null)
                animationController.PlayEmotion("SURPRISED");

            // Return to neutral after 3 seconds
            Invoke(nameof(ReturnToNeutral), 3f);
        }
        // React to camera emotions
        else if (eventType == "camera_emotion" && !string.IsNullOrEmpty(data))
        {
            if (expressionController != null)
                expressionController.SetEmotion(data.ToUpper());
            if (animationController != null)
                animationController.PlayEmotion(data.ToUpper());

            Invoke(nameof(ReturnToNeutral), 5f);
        }
    }

    /// <summary>
    /// We manually call VRM Runtime.Process() here so it runs AFTER
    /// all our scripts (ExpressionController, IdleBehavior, LipSyncController)
    /// have set their expression weights in LateUpdate.
    /// </summary>
    void LateUpdate()
    {
        if (_loaded && VrmInstance != null && VrmInstance.Runtime != null)
        {
            VrmInstance.Runtime.Process();
        }
    }

    void ReturnToNeutral()
    {
        if (expressionController != null)
            expressionController.SetEmotion("NEUTRAL");
        if (animationController != null)
            animationController.PlayEmotion("NEUTRAL");
    }

    /// <summary>
    /// Unload the current VRM avatar, clearing all references.
    /// </summary>
    void UnloadAvatar()
    {
        // Stop any playing audio
        if (audioPlayer != null)
            audioPlayer.Stop();

        // Clear sub-controller refs so they stop updating
        ResetSubControllers();

        // Destroy the VRM GameObject
        if (VrmInstance != null)
        {
            Destroy(VrmInstance.gameObject);
            VrmInstance = null;
        }

        AvatarAnimator = null;
        _loaded = false;

        Debug.Log("[AvatarController] Avatar unloaded");
    }

    /// <summary>
    /// Reset all sub-controllers by passing null, clearing stale VRM refs.
    /// </summary>
    void ResetSubControllers()
    {
        if (expressionController != null)
            expressionController.Init(null);
        if (animationController != null)
            animationController.Init(null);
        if (lipSyncController != null)
            lipSyncController.Init(null);
        if (idleBehavior != null)
            idleBehavior.Init(null);
        if (touchInteraction != null)
            touchInteraction.Init(null, null, null, null);
    }

    /// <summary>
    /// Switch to a different costume at runtime. Called by WebSocketClient.
    /// </summary>
    public async void SwitchCostume(string costumeId)
    {
        if (_isSwitching || _isCostumeAnimating)
        {
            Debug.LogWarning("[AvatarController] Already switching costume, ignoring request");
            return;
        }

        // Validate costume exists
        var costume = Config.GetCostume(costumeId);
        if (costume.id != costumeId)
        {
            Debug.LogWarning($"[AvatarController] Unknown costume '{costumeId}', ignoring");
            return;
        }

        if (costumeId == _currentCostumeId)
        {
            Debug.Log($"[AvatarController] Already wearing '{costumeId}', ignoring");
            return;
        }

        _isSwitching = true;
        string previousCostumeId = _currentCostumeId;
        Debug.Log($"[AvatarController] Switching costume: {_currentCostumeId} → {costumeId}");

        try
        {
            UnloadAvatar();

            // Give GC a chance to free the old VRM data
            System.GC.Collect();
            await Task.Yield();

            // Set the new costume and load it
            _currentCostumeId = costumeId;
            Config.CurrentCostumeId = costumeId;

            await LoadAvatar();

            if (_loaded)
            {
                InitSubControllers();
                if (_isSleepingState)
                    SetSleeping(true);
                Debug.Log($"[AvatarController] Costume switched to '{costumeId}' successfully");
            }
            else
            {
                // Load failed — try to fall back to previous costume
                Debug.LogError($"[AvatarController] Failed to load '{costumeId}', falling back to '{previousCostumeId}'");
                _currentCostumeId = previousCostumeId;
                Config.CurrentCostumeId = previousCostumeId;

                await LoadAvatar();
                if (_loaded)
                {
                    InitSubControllers();
                    if (_isSleepingState)
                        SetSleeping(true);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AvatarController] Costume switch failed: {e}");

            // Attempt fallback
            _currentCostumeId = previousCostumeId;
            Config.CurrentCostumeId = previousCostumeId;

            try
            {
                await LoadAvatar();
                if (_loaded)
                {
                    InitSubControllers();
                    if (_isSleepingState)
                        SetSleeping(true);
                }
            }
            catch (System.Exception e2)
            {
                Debug.LogError($"[AvatarController] Fallback also failed: {e2}");
            }
        }
        finally
        {
            _isSwitching = false;
            DrainChatQueue();
        }
    }

    /// <summary>
    /// Initiate a costume change with walk-off/walk-on animation, then play the chat reply.
    /// Called by WebSocketClient when a chat message includes a costume_id.
    /// </summary>
    public void CostumeChangeWithChat(string costumeId, string reply, string emotion, string audioUrl)
    {
        if (_isCostumeAnimating || _isSwitching)
        {
            Debug.LogWarning("[AvatarController] Costume animation already in progress, ignoring");
            return;
        }

        // Validate costume
        var costume = Config.GetCostume(costumeId);
        if (costume.id != costumeId)
        {
            Debug.LogWarning($"[AvatarController] Unknown costume '{costumeId}', playing chat directly");
            HandleChat(reply, emotion, audioUrl);
            return;
        }

        // Already wearing this costume — skip animation, play chat directly
        if (costumeId == _currentCostumeId)
        {
            Debug.Log($"[AvatarController] Already wearing '{costumeId}', skipping animation");
            HandleChat(reply, emotion, audioUrl);
            return;
        }

        // Queue the chat reply to play after walk-on
        _chatQueue.Enqueue(new QueuedChat { reply = reply, emotion = emotion, audioUrl = audioUrl });

        // Start the animation sequence
        StartCoroutine(CostumeChangeSequence(costumeId));
    }

    /// <summary>
    /// Coroutine: walk off-screen → swap VRM → walk back → play queued chat.
    /// Moves the PARENT transform (this GameObject), not the VRM child.
    /// </summary>
    private IEnumerator CostumeChangeSequence(string costumeId)
    {
        _isCostumeAnimating = true;
        string previousCostumeId = _currentCostumeId;

        Debug.Log($"[AvatarController] Costume animation: {_currentCostumeId} → {costumeId}, defaultPos={_defaultPosition}");

        // Stop any playing audio
        if (audioPlayer != null)
            audioPlayer.Stop();

        // Disable root motion and freeze any Rigidbody so nothing fights our position lerp
        if (AvatarAnimator != null)
            AvatarAnimator.applyRootMotion = false;
        Rigidbody rb = GetComponentInChildren<Rigidbody>();
        bool wasKinematic = rb != null && rb.isKinematic;
        if (rb != null) rb.isKinematic = true;

        // Screen-right = -X with this camera (180° Y-rotated, at +Z looking toward -Z)
        float offX = _defaultPosition.x - Config.WalkOffScreenX;

        // --- Phase 1: Slide off-screen (to the right on screen = -X world) ---
        float elapsed = 0f;
        while (elapsed < Config.WalkOffDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / Config.WalkOffDuration);
            float x = Mathf.Lerp(_defaultPosition.x, offX, t);
            transform.position = new Vector3(x, _defaultPosition.y, _defaultPosition.z);
            if (VrmInstance != null)
                VrmInstance.transform.localPosition = Vector3.zero;
            yield return null;
        }
        transform.position = new Vector3(offX, _defaultPosition.y, _defaultPosition.z);

        // --- Phase 2: Pause off-screen ---
        yield return new WaitForSeconds(Config.OffScreenPause);

        // --- Phase 3: Swap VRM model (hidden off-screen) ---
        _isSwitching = true;
        bool swapSuccess = false;

        UnloadAvatar();
        System.GC.Collect();
        yield return null;

        _currentCostumeId = costumeId;
        Config.CurrentCostumeId = costumeId;

        var loadTask = LoadAvatar();
        while (!loadTask.IsCompleted)
            yield return null;

        if (_loaded)
        {
            InitSubControllers();
            if (_isSleepingState)
                SetSleeping(true);
            swapSuccess = true;
            if (AvatarAnimator != null)
                AvatarAnimator.applyRootMotion = false;
            Debug.Log($"[AvatarController] Costume swapped to '{costumeId}' (off-screen)");
        }
        else
        {
            Debug.LogError($"[AvatarController] Failed to load '{costumeId}', falling back to '{previousCostumeId}'");
            _currentCostumeId = previousCostumeId;
            Config.CurrentCostumeId = previousCostumeId;

            var fallbackTask = LoadAvatar();
            while (!fallbackTask.IsCompleted)
                yield return null;

            if (_loaded)
            {
                InitSubControllers();
                if (_isSleepingState)
                    SetSleeping(true);
                if (AvatarAnimator != null)
                    AvatarAnimator.applyRootMotion = false;
            }
        }

        _isSwitching = false;

        // --- Phase 4: Slide back on-screen ---
        elapsed = 0f;
        while (elapsed < Config.WalkOnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / Config.WalkOnDuration);
            float x = Mathf.Lerp(offX, _defaultPosition.x, t);
            transform.position = new Vector3(x, _defaultPosition.y, _defaultPosition.z);
            if (VrmInstance != null)
                VrmInstance.transform.localPosition = Vector3.zero;
            yield return null;
        }

        // --- Phase 5: Snap to exact position, return to idle ---
        transform.position = _defaultPosition;
        transform.rotation = _defaultRotation;

        // Restore Rigidbody state
        rb = GetComponentInChildren<Rigidbody>();
        if (rb != null) rb.isKinematic = wasKinematic;

        if (animationController != null)
            animationController.PlayEmotion("NEUTRAL");
        if (expressionController != null)
            expressionController.SetEmotion("NEUTRAL");

        yield return new WaitForSeconds(Config.PostSwapSettleTime);

        // --- Done ---
        _isCostumeAnimating = false;

        var picker = FindObjectOfType<CostumePicker>();
        if (picker != null)
            picker.OnCostumeChanged(_currentCostumeId);

        DrainChatQueue();

        Debug.Log($"[AvatarController] Costume animation complete (success={swapSuccess})");
    }

    /// <summary>
    /// Show subtitle text. Called by WebSocketClient after HandleChat.
    /// </summary>
    public void ShowSubtitle(string reply)
    {
        if (subtitleController == null) return;
        subtitleController.Show(reply, null);
    }

    /// <summary>
    /// Play only the most recent queued chat message (skip older ones to avoid stacking audio).
    /// </summary>
    private void DrainChatQueue()
    {
        if (_chatQueue.Count == 0) return;

        // Keep only the last message
        QueuedChat last = default;
        while (_chatQueue.Count > 0)
            last = _chatQueue.Dequeue();

        Debug.Log($"[AvatarController] Draining chat queue, playing latest message");
        HandleChat(last.reply, last.emotion, last.audioUrl);
    }

    /// <summary>
    /// Sleep mode — closes eyes, shows ZZZ, stops idle animations.
    /// </summary>
    public void SetSleeping(bool sleeping)
    {
        _isSleepingState = sleeping;
        Debug.Log($"[AvatarController] SetSleeping({sleeping}) — overlay={_sleepOverlay != null}, idle={idleBehavior != null}");

        if (idleBehavior != null)
            idleBehavior.SetSleeping(sleeping);

        // Activate the ZZZ overlay
        if (_sleepOverlay != null)
        {
            _sleepOverlay.SetActive(sleeping);
            Debug.Log($"[AvatarController] ZZZ overlay set to {sleeping}");
        }
        else
        {
            Debug.LogWarning("[AvatarController] ZZZ overlay is null! Recreating...");
            CreateSleepOverlay();
            if (_sleepOverlay != null)
                _sleepOverlay.SetActive(sleeping);
        }

        if (sleeping)
        {
            // Set neutral expression and stop any playing audio
            if (expressionController != null)
                expressionController.SetEmotion("NEUTRAL");
            if (animationController != null)
                animationController.PlayEmotion("NEUTRAL");
            if (audioPlayer != null)
                audioPlayer.Stop();
        }
        else
        {
            // Wake up — return to neutral (eyes open handled by IdleBehavior)
            if (expressionController != null)
                expressionController.SetEmotion("NEUTRAL");
            if (animationController != null)
                animationController.PlayEmotion("NEUTRAL");
        }

        Debug.Log($"[AvatarController] Sleep mode: {sleeping}");
    }
}
