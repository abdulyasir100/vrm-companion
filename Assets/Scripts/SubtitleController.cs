using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Displays subtitle text at the bottom of the screen.
/// EN mode: single line. JA mode: JP text + EN translation below.
/// Attach to a Canvas with TextMeshPro components.
/// </summary>
public class SubtitleController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI mainText;
    [SerializeField] private TextMeshProUGUI translationText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Timing")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private float displayDuration = 8f;

    private Coroutine _currentRoutine;

    void Awake()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (translationText != null)
            translationText.gameObject.SetActive(false);
    }

    /// <summary>
    /// Show subtitle text. If translation is provided, show dual lines (JP + EN).
    /// </summary>
    public void Show(string text, string translation)
    {
        if (_currentRoutine != null)
            StopCoroutine(_currentRoutine);

        _currentRoutine = StartCoroutine(SubtitleSequence(text, translation));
    }

    /// <summary>
    /// Hide subtitle immediately.
    /// </summary>
    public void Hide()
    {
        if (_currentRoutine != null)
        {
            StopCoroutine(_currentRoutine);
            _currentRoutine = null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private IEnumerator SubtitleSequence(string text, string translation)
    {
        // Set text content
        if (mainText != null)
            mainText.text = text ?? "";

        bool hasDualSubtitle = !string.IsNullOrEmpty(translation);

        if (translationText != null)
        {
            translationText.gameObject.SetActive(hasDualSubtitle);
            if (hasDualSubtitle)
                translationText.text = translation;
        }

        // Fade in
        if (canvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        // Display
        yield return new WaitForSeconds(displayDuration);

        // Fade out
        if (canvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
                yield return null;
            }
            canvasGroup.alpha = 0f;
        }

        _currentRoutine = null;
    }
}
