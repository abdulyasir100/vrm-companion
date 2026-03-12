using UnityEngine;
using UniVRM10;

/// <summary>
/// Subtle idle animations: blinking, breathing, gentle body sway.
/// Makes the avatar feel alive even when nothing is happening.
/// </summary>
public class IdleBehavior : MonoBehaviour
{
    private Vrm10Instance _vrm;
    private bool _initialized;

    // Sleep state
    private bool _isSleeping;
    private float _sleepBlinkWeight; // lerps to 100 when sleeping

    // Blink state
    private float _blinkTimer;
    private float _nextBlinkTime;
    private float _blinkProgress; // 0 = open, 1 = closed
    private bool _isBlinking;

    // Direct blendshape control (bypasses VRM expression system)
    private SkinnedMeshRenderer _faceMesh;
    private int _blinkLeftIndex = -1;
    private int _blinkRightIndex = -1;

    // Bone references
    private Transform _spine;
    private Transform _hips;

    // Initial poses (to offset from, not override)
    private Vector3 _spineStartPos;
    private Quaternion _hipsStartRot;

    public void Init(Vrm10Instance vrm)
    {
        _initialized = false;
        _faceMesh = null;
        _blinkLeftIndex = -1;
        _blinkRightIndex = -1;
        _spine = null;
        _hips = null;

        _vrm = vrm;
        if (_vrm == null) return;

        // Get bone references
        if (_vrm.TryGetBoneTransform(HumanBodyBones.Spine, out Transform spine))
        {
            _spine = spine;
            _spineStartPos = spine.localPosition;
        }

        if (_vrm.TryGetBoneTransform(HumanBodyBones.Hips, out Transform hips))
        {
            _hips = hips;
            _hipsStartRot = hips.localRotation;
        }

        // Find the face mesh and blink blendshape indices directly
        FindBlinkBlendshapes();

        // Randomize first blink
        _nextBlinkTime = Random.Range(Config.BlinkIntervalMin, Config.BlinkIntervalMax);

        _initialized = true;
        Debug.Log("[IdleBehavior] Initialized");
    }

    void FindBlinkBlendshapes()
    {
        // Search all SkinnedMeshRenderers on the VRM for blink blendshapes
        var renderers = _vrm.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (var smr in renderers)
        {
            if (smr.sharedMesh == null) continue;
            for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
            {
                string name = smr.sharedMesh.GetBlendShapeName(i).ToLower();
                // Log all blendshape names on first mesh that has them
                Debug.Log($"[IdleBehavior] BlendShape[{i}]: {smr.sharedMesh.GetBlendShapeName(i)} (on {smr.name})");
            }
        }

        // Now find the blink ones — supports English and Japanese names
        foreach (var smr in renderers)
        {
            if (smr.sharedMesh == null) continue;
            for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
            {
                string name = smr.sharedMesh.GetBlendShapeName(i);
                string lower = name.ToLower();

                // Japanese: まばたき = blink (both eyes)
                if (name == "\u307e\u3070\u305f\u304d" || lower == "blink" || lower.Contains("face.m_f00_000_00_fcl_eye_close"))
                {
                    _blinkLeftIndex = i;
                    _blinkRightIndex = i;
                    _faceMesh = smr;
                }
                // English left/right blink
                else if (lower.Contains("blink_l") || lower.Contains("blinkleft") || lower.Contains("blink.l"))
                {
                    _blinkLeftIndex = i;
                    _faceMesh = smr;
                }
                else if (lower.Contains("blink_r") || lower.Contains("blinkright") || lower.Contains("blink.r"))
                {
                    _blinkRightIndex = i;
                    _faceMesh = smr;
                }
            }
        }

        if (_faceMesh != null)
            Debug.Log($"[IdleBehavior] Found blink blendshapes: L={_blinkLeftIndex}, R={_blinkRightIndex} on {_faceMesh.name}");
        else
            Debug.LogWarning("[IdleBehavior] Could not find blink blendshapes on any mesh!");
    }

    public void SetSleeping(bool sleeping)
    {
        _isSleeping = sleeping;
        if (!sleeping)
        {
            // Reset blink state when waking — force eyes open immediately
            _isBlinking = false;
            _blinkTimer = 0f;
            _sleepBlinkWeight = 0f;
            _nextBlinkTime = Random.Range(Config.BlinkIntervalMin, Config.BlinkIntervalMax);

            // Force blendshapes to 0 (eyes open) right now
            if (_faceMesh != null)
            {
                if (_blinkLeftIndex >= 0)
                    _faceMesh.SetBlendShapeWeight(_blinkLeftIndex, 0f);
                if (_blinkRightIndex >= 0 && _blinkRightIndex != _blinkLeftIndex)
                    _faceMesh.SetBlendShapeWeight(_blinkRightIndex, 0f);
            }
        }
        Debug.Log($"[IdleBehavior] Sleep mode: {sleeping}, sleepBlinkWeight reset={!sleeping}");
    }

    public bool IsSleeping => _isSleeping;

    void LateUpdate()
    {
        if (!_initialized) return;

        if (_isSleeping)
        {
            UpdateSleepEyes();
            UpdateSleepBreathing();
            return;
        }

        UpdateBlink();
        UpdateBreathing();
        UpdateSway();
    }

    void UpdateSleepEyes()
    {
        if (_faceMesh == null) return;
        // Smoothly close eyes
        _sleepBlinkWeight = Mathf.Lerp(_sleepBlinkWeight, 100f, Time.deltaTime * 3f);
        if (_blinkLeftIndex >= 0)
            _faceMesh.SetBlendShapeWeight(_blinkLeftIndex, _sleepBlinkWeight);
        if (_blinkRightIndex >= 0 && _blinkRightIndex != _blinkLeftIndex)
            _faceMesh.SetBlendShapeWeight(_blinkRightIndex, _sleepBlinkWeight);
    }

    void UpdateSleepBreathing()
    {
        if (_spine == null) return;
        // Slower, deeper breathing while sleeping
        float breathOffset = Mathf.Sin(Time.time * Config.BreathingSpeed * 0.5f) * Config.BreathingAmount * 1.5f;
        _spine.localPosition = _spineStartPos + new Vector3(0f, breathOffset, 0f);
    }

    void UpdateBlink()
    {
        if (_faceMesh == null) return;

        // Smoothly open eyes when not sleeping
        if (_sleepBlinkWeight > 0.1f)
        {
            _sleepBlinkWeight = Mathf.Lerp(_sleepBlinkWeight, 0f, Time.deltaTime * 5f);
            if (_blinkLeftIndex >= 0)
                _faceMesh.SetBlendShapeWeight(_blinkLeftIndex, _sleepBlinkWeight);
            if (_blinkRightIndex >= 0 && _blinkRightIndex != _blinkLeftIndex)
                _faceMesh.SetBlendShapeWeight(_blinkRightIndex, _sleepBlinkWeight);
            return;
        }

        _blinkTimer += Time.deltaTime;

        if (!_isBlinking && _blinkTimer >= _nextBlinkTime)
        {
            _isBlinking = true;
            _blinkProgress = 0f;
        }

        if (_isBlinking)
        {
            _blinkProgress += Time.deltaTime / Config.BlinkDuration;

            float blinkWeight;
            if (_blinkProgress < 0.5f)
            {
                // Closing
                blinkWeight = _blinkProgress * 2f;
            }
            else if (_blinkProgress < 1f)
            {
                // Opening
                blinkWeight = (1f - _blinkProgress) * 2f;
            }
            else
            {
                // Done
                blinkWeight = 0f;
                _isBlinking = false;
                _blinkTimer = 0f;
                _nextBlinkTime = Random.Range(Config.BlinkIntervalMin, Config.BlinkIntervalMax);
            }

            // Drive blendshapes directly on the mesh (0-100 range)
            float meshWeight = blinkWeight * 100f;
            if (_blinkLeftIndex >= 0)
                _faceMesh.SetBlendShapeWeight(_blinkLeftIndex, meshWeight);
            if (_blinkRightIndex >= 0 && _blinkRightIndex != _blinkLeftIndex)
                _faceMesh.SetBlendShapeWeight(_blinkRightIndex, meshWeight);
        }
    }

    void UpdateBreathing()
    {
        if (_spine == null) return;

        // Subtle up-down on the spine for breathing
        float breathOffset = Mathf.Sin(Time.time * Config.BreathingSpeed) * Config.BreathingAmount;
        _spine.localPosition = _spineStartPos + new Vector3(0f, breathOffset, 0f);
    }

    void UpdateSway()
    {
        if (_hips == null) return;

        // Gentle side-to-side sway on the hips
        float swayAngle = Mathf.Sin(Time.time * Config.SwaySpeed) * Config.SwayAmount;
        _hips.localRotation = _hipsStartRot * Quaternion.Euler(0f, 0f, swayAngle);
    }
}
