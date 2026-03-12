using UnityEngine;
using UniVRM10;
using System.Collections.Generic;

public class ExpressionController : MonoBehaviour
{
    private Vrm10Instance _vrm;
    private bool _initialized;

    // Current target weights
    private Dictionary<ExpressionKey, float> _targetWeights = new Dictionary<ExpressionKey, float>();

    // Current actual weights (for lerping)
    private Dictionary<ExpressionKey, float> _currentWeights = new Dictionary<ExpressionKey, float>();

    // Map server emotion strings to VRM ExpressionKeys
    // NOTE: NEUTRAL is not mapped — neutral = all weights at 0 (default face).
    // Setting neutral to 1.0 triggers OverrideBlink which kills our blink code.
    private static readonly Dictionary<string, ExpressionKey> EmotionMap = new Dictionary<string, ExpressionKey>
    {
        { "HAPPY",     ExpressionKey.Happy },
        { "SAD",       ExpressionKey.Sad },
        { "ANGRY",     ExpressionKey.Angry },
        { "SURPRISED", ExpressionKey.Surprised },
        { "RELAXED",   ExpressionKey.Relaxed },
    };

    private static readonly ExpressionKey[] AllEmotionKeys = new ExpressionKey[]
    {
        ExpressionKey.Happy,
        ExpressionKey.Sad,
        ExpressionKey.Angry,
        ExpressionKey.Surprised,
        ExpressionKey.Relaxed,
    };

    public void Init(Vrm10Instance vrm)
    {
        _initialized = false;
        _vrm = vrm;
        if (_vrm == null) return;

        foreach (var key in AllEmotionKeys)
        {
            _targetWeights[key] = 0f;
            _currentWeights[key] = 0f;
        }

        _initialized = true;
        Debug.Log("[ExpressionController] Initialized");
    }

    void LateUpdate()
    {
        if (!_initialized) return;

        bool changed = false;
        foreach (var key in AllEmotionKeys)
        {
            float target = _targetWeights.ContainsKey(key) ? _targetWeights[key] : 0f;
            float current = _currentWeights.ContainsKey(key) ? _currentWeights[key] : 0f;

            float newVal = Mathf.Lerp(current, target, Time.deltaTime * Config.ExpressionBlendSpeed);

            if (Mathf.Abs(newVal - target) < 0.005f)
                newVal = target;

            if (Mathf.Abs(newVal - current) > 0.001f)
            {
                _currentWeights[key] = newVal;
                changed = true;
            }
        }

        if (changed)
        {
            ApplyWeights();
        }
    }

    void ApplyWeights()
    {
        if (_vrm?.Runtime?.Expression == null) return;

        foreach (var kvp in _currentWeights)
        {
            _vrm.Runtime.Expression.SetWeight(kvp.Key, kvp.Value);
        }
    }

    public void SetEmotion(string emotion)
    {
        if (!_initialized) return;

        foreach (var key in AllEmotionKeys)
        {
            _targetWeights[key] = 0f;
        }

        string upper = emotion?.ToUpper() ?? "NEUTRAL";
        if (upper == "NEUTRAL" || upper == "THINKING")
        {
            return;
        }

        if (EmotionMap.TryGetValue(upper, out ExpressionKey targetKey))
        {
            _targetWeights[targetKey] = 1f;
        }
        else
        {
            Debug.LogWarning($"[ExpressionController] Unknown emotion '{emotion}', defaulting to neutral");
        }
    }

    public void SetBlinkWeight(float weight)
    {
        if (!_initialized || _vrm?.Runtime?.Expression == null) return;
        _vrm.Runtime.Expression.SetWeight(ExpressionKey.Blink, weight);
    }

    public void SetMouthWeight(float weight)
    {
        if (!_initialized || _vrm?.Runtime?.Expression == null) return;
        _vrm.Runtime.Expression.SetWeight(ExpressionKey.Aa, weight);
    }
}
