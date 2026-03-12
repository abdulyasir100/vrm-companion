using UnityEngine;
using TMPro;

/// <summary>
/// Floating "Zzz" text above the avatar's head during sleep mode.
/// Created programmatically by AvatarController — no manual scene setup needed.
/// </summary>
public class SleepOverlay : MonoBehaviour
{
    private float bobSpeed = 1.5f;
    private float bobAmount = 0.05f;
    private float fadeSpeed = 1f;

    private TextMeshProUGUI _text;
    private CanvasGroup _canvasGroup;
    private Transform _canvasTransform;
    private Vector3 _baseLocalPos;
    private bool _active;
    private float _alpha;
    private bool _initialized;

    // ZZZ cycle: shows "z", "zZ", "zZz" then fades and restarts
    private static readonly string[] ZzzFrames = { "z", "zZ", "zZz" };
    private int _frameIndex;
    private float _frameTimer;

    void EnsureInitialized()
    {
        if (_initialized) return;

        _text = GetComponent<TextMeshProUGUI>();
        _canvasGroup = GetComponentInParent<CanvasGroup>();
        _canvasTransform = _canvasGroup != null ? _canvasGroup.transform : transform.parent;
        _baseLocalPos = _canvasTransform != null ? _canvasTransform.localPosition : transform.localPosition;

        _initialized = true;
    }

    public void SetActive(bool active)
    {
        EnsureInitialized();
        _active = active;

        if (active)
        {
            // Enable the parent canvas
            if (_canvasTransform != null)
                _canvasTransform.gameObject.SetActive(true);
            gameObject.SetActive(true);

            _alpha = 0f;
            _frameIndex = 0;
            _frameTimer = 0f;
            if (_text != null) _text.text = "z";
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        }
    }

    void Update()
    {
        if (!_initialized) return;

        if (!_active && _alpha <= 0.01f)
        {
            // Hide the parent canvas when fully faded out
            if (_canvasTransform != null)
                _canvasTransform.gameObject.SetActive(false);
            return;
        }

        // Fade in/out
        float targetAlpha = _active ? 1f : 0f;
        _alpha = Mathf.Lerp(_alpha, targetAlpha, Time.deltaTime * fadeSpeed * 3f);
        if (_canvasGroup != null) _canvasGroup.alpha = _alpha;

        // Bob the whole canvas up and down
        if (_canvasTransform != null)
        {
            float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
            _canvasTransform.localPosition = _baseLocalPos + new Vector3(0f, bob, 0f);

            // Billboard: always face the camera
            Camera cam = Camera.main;
            if (cam != null)
                _canvasTransform.rotation = cam.transform.rotation;
        }

        // ZZZ frame animation
        if (_active && _text != null)
        {
            _frameTimer += Time.deltaTime;
            if (_frameTimer >= 0.8f)
            {
                _frameTimer = 0f;
                _frameIndex = (_frameIndex + 1) % ZzzFrames.Length;
                _text.text = ZzzFrames[_frameIndex];
            }
        }
    }
}
