using UnityEngine;

/// <summary>
/// OnGUI circle button in the top-right corner to cycle costumes locally.
/// Consistent with OfflineMode.cs pattern (no Canvas dependency).
/// </summary>
public class CostumePicker : MonoBehaviour
{
    [Header("References")]
    public AvatarController avatarController;

    [Header("Settings")]
    [SerializeField] private float cooldownSeconds = 3f;

    private int _currentIndex;
    private float _lastSwitchTime = -10f;
    private bool _showTooltip;
    private float _tooltipTimer;
    private string _tooltipText = "";

    // Button geometry (computed once in OnGUI for DPI scaling)
    private Rect _buttonRect;
    private bool _rectComputed;

    // Styles
    private GUIStyle _buttonStyle;
    private GUIStyle _tooltipStyle;

    void Start()
    {
        // Find which costume index we're currently on
        string current = Config.CurrentCostumeId;
        for (int i = 0; i < Config.Costumes.Length; i++)
        {
            if (Config.Costumes[i].id == current)
            {
                _currentIndex = i;
                break;
            }
        }
    }

    void Update()
    {
        if (_showTooltip)
        {
            _tooltipTimer -= Time.deltaTime;
            if (_tooltipTimer <= 0f)
                _showTooltip = false;
        }
    }

    void OnGUI()
    {
        // Lazy-init styles
        if (_buttonStyle == null)
        {
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(18f * Screen.dpi / 160f),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            _buttonStyle.normal.textColor = Color.white;
        }

        if (_tooltipStyle == null)
        {
            _tooltipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(14f * Screen.dpi / 160f),
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            _tooltipStyle.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
        }

        // DPI-scaled button size
        float dpiScale = Mathf.Max(Screen.dpi / 160f, 1f);
        float size = 48f * dpiScale;
        float margin = 16f * dpiScale;

        _buttonRect = new Rect(
            Screen.width - size - margin,
            margin,
            size,
            size
        );
        _rectComputed = true;

        bool onCooldown = (Time.time - _lastSwitchTime) < cooldownSeconds;

        // Draw semi-transparent circle background
        Color bgColor = onCooldown
            ? new Color(0.3f, 0.3f, 0.3f, 0.6f)
            : new Color(0.2f, 0.5f, 0.9f, 0.7f);
        GUI.color = bgColor;
        GUI.DrawTexture(_buttonRect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Draw button icon (clothes / outfit)
        string label = onCooldown ? "..." : "\u2702"; // ✂ scissors (costume swap)
        if (GUI.Button(_buttonRect, label, _buttonStyle))
        {
            if (!onCooldown)
                CycleNext();
        }

        // Tooltip below button
        if (_showTooltip)
        {
            float tooltipW = 120f * dpiScale;
            float tooltipH = 28f * dpiScale;
            Rect tooltipRect = new Rect(
                _buttonRect.x + _buttonRect.width / 2f - tooltipW / 2f,
                _buttonRect.yMax + 4f * dpiScale,
                tooltipW,
                tooltipH
            );

            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(tooltipRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(tooltipRect, _tooltipText, _tooltipStyle);
        }
    }

    void CycleNext()
    {
        _currentIndex = (_currentIndex + 1) % Config.Costumes.Length;
        var costume = Config.Costumes[_currentIndex];

        _lastSwitchTime = Time.time;
        _tooltipText = costume.displayName;
        _showTooltip = true;
        _tooltipTimer = 2f;

        Debug.Log($"[CostumePicker] Cycling to: {costume.id}");

        if (avatarController != null)
            avatarController.SwitchCostume(costume.id);
    }

    /// <summary>
    /// Called externally when the server triggers a costume change (via WebSocket).
    /// Updates the picker index without triggering another switch.
    /// </summary>
    public void OnCostumeChanged(string costumeId)
    {
        for (int i = 0; i < Config.Costumes.Length; i++)
        {
            if (Config.Costumes[i].id == costumeId)
            {
                _currentIndex = i;
                _tooltipText = Config.Costumes[i].displayName;
                _showTooltip = true;
                _tooltipTimer = 2f;
                Debug.Log($"[CostumePicker] Synced to server costume: {costumeId}");
                return;
            }
        }
    }

    /// <summary>
    /// Returns true if the given screen position is inside the button area.
    /// Used by TouchInteraction to filter out UI taps.
    /// </summary>
    public bool IsInButtonArea(Vector2 screenPos)
    {
        if (!_rectComputed) return false;
        // OnGUI Y is top-down, Input screen Y is bottom-up
        float guiY = Screen.height - screenPos.y;
        return _buttonRect.Contains(new Vector2(screenPos.x, guiY));
    }
}
