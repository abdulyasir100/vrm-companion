using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Game-like circular mood bar — top-left corner HUD element.
/// 2-color gradient ring: dark navy (low) to Suisei blue (high).
/// Star icon in center tinted to match mood level.
///
/// SETUP: Attach to MoodCanvas. Child objects: BgRing, FillRing, MoodIcon, MoodValue.
/// Ring sprites generated at runtime. Icon sprite assigned in Inspector.
/// </summary>
public class MoodDisplay : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Image bgRing;
    [SerializeField] private Image fillRing;
    [SerializeField] private Image moodIcon;
    [SerializeField] private TextMeshProUGUI valueText;

    private const int TEX_SIZE = 128;
    private const int RING_THICKNESS = 14;

    private float _targetMood = 100f;
    private float _displayMood = 100f;
    private float _pulseTimer;
    private bool _initialized;

    // 2-color gradient: dark navy → Suisei blue
    private static readonly Color ColorLow  = new Color(0.08f, 0.10f, 0.22f);  // dark navy
    private static readonly Color ColorHigh = new Color(0.26f, 0.52f, 0.96f);  // Suisei blue (#4285F5)

    void Start()
    {
        if (bgRing == null || fillRing == null || moodIcon == null || valueText == null)
            FindReferences();

        // Generate ring sprites at runtime
        if (bgRing != null)
            bgRing.sprite = MakeRingSprite(false);

        if (fillRing != null)
        {
            fillRing.sprite = MakeRingSprite(true);
            fillRing.type = Image.Type.Filled;
            fillRing.fillMethod = Image.FillMethod.Radial360;
            fillRing.fillOrigin = 2; // Top
            fillRing.fillClockwise = true;
            fillRing.fillAmount = 1f;
        }

        _initialized = true;
        Debug.Log("[MoodDisplay] Initialized");
    }

    void FindReferences()
    {
        // Search in direct children or one level deeper
        Transform container = transform.Find("MoodContainer");
        if (container == null)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                container = transform.GetChild(i).Find("MoodContainer");
                if (container != null) break;
            }
        }
        if (container == null)
        {
            Debug.LogWarning("[MoodDisplay] MoodContainer not found");
            return;
        }

        if (bgRing == null)
        {
            var t = container.Find("BgRing");
            if (t != null) bgRing = t.GetComponent<Image>();
        }
        if (fillRing == null)
        {
            var t = container.Find("FillRing");
            if (t != null) fillRing = t.GetComponent<Image>();
        }
        if (moodIcon == null)
        {
            var t = container.Find("MoodIcon");
            if (t != null) moodIcon = t.GetComponent<Image>();
        }
        if (valueText == null)
        {
            var t = container.Find("MoodValue");
            if (t != null) valueText = t.GetComponent<TextMeshProUGUI>();
        }
    }

    public void UpdateMood(float value)
    {
        _targetMood = Mathf.Clamp(value, 0f, 100f);
    }

    void Update()
    {
        if (!_initialized) return;

        _displayMood = Mathf.Lerp(_displayMood, _targetMood, Time.deltaTime * 2.5f);
        float t = _displayMood / 100f;

        if (fillRing != null)
            fillRing.fillAmount = t;

        // Tint icon to match mood level
        Color moodCol = Color.Lerp(ColorLow, ColorHigh, t);
        if (moodIcon != null)
        {
            moodIcon.color = moodCol;

            // Subtle pulse
            _pulseTimer += Time.deltaTime;
            float s = 1f + Mathf.Sin(_pulseTimer * 1.5f) * 0.03f;
            moodIcon.transform.localScale = Vector3.one * s;
        }

        if (valueText != null)
        {
            valueText.text = Mathf.RoundToInt(_displayMood).ToString();
            valueText.color = new Color(1f, 1f, 1f, 0.7f);
        }
    }

    // ────────────────── Ring Texture Generation ──────────────────

    Sprite MakeRingSprite(bool gradient)
    {
        var tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        float center = TEX_SIZE / 2f;
        float outerR = center - 2f;
        float innerR = outerR - RING_THICKNESS;

        var px = new Color[TEX_SIZE * TEX_SIZE];

        for (int y = 0; y < TEX_SIZE; y++)
        {
            for (int x = 0; x < TEX_SIZE; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                int idx = y * TEX_SIZE + x;

                if (dist < innerR - 1f || dist > outerR + 1f)
                {
                    px[idx] = Color.clear;
                    continue;
                }

                float aa = Mathf.Min(
                    Mathf.Clamp01(outerR - dist + 1f),
                    Mathf.Clamp01(dist - innerR + 1f)
                );

                Color col;
                if (gradient)
                {
                    // Clockwise from top — 2-color lerp
                    float angle = Mathf.Atan2(dx, dy);
                    float at = angle / (2f * Mathf.PI);
                    if (at < 0f) at += 1f;
                    col = Color.Lerp(ColorLow, ColorHigh, at);
                }
                else
                {
                    col = new Color(0.12f, 0.12f, 0.18f);
                }

                col.a *= aa;
                px[idx] = col;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, TEX_SIZE, TEX_SIZE), Vector2.one * 0.5f, 100f);
    }
}
