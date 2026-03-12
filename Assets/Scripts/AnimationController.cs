using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Plays emotion-based animations on the VRM avatar's Animator.
/// Uses Animator parameters to transition between states.
///
/// SETUP REQUIRED IN UNITY:
/// 1. Create an Animator Controller (Assets/Animations/AvatarAnimator.controller)
/// 2. Add states: Idle, Happy, Sad, Angry, Surprised, Thinking, Talking
/// 3. Add an integer parameter called "EmotionIndex"
/// 4. Add transitions from Any State to each emotion state based on EmotionIndex:
///    - 0 = Idle, 1 = Happy, 2 = Sad, 3 = Angry, 4 = Surprised, 5 = Thinking, 6 = Talking
/// 5. Each state should use the corresponding FBX animation clip
/// 6. Idle state should loop. Others can be one-shot with transition back to Idle.
/// 7. Assign the Animator Controller to the VRM's Animator component.
/// </summary>
public class AnimationController : MonoBehaviour
{
    private Animator _animator;
    private bool _initialized;

    // Parameter name in the Animator Controller
    private const string EMOTION_PARAM = "EmotionIndex";

    // Map emotion strings to animator indices
    private static readonly Dictionary<string, int> EmotionIndex = new Dictionary<string, int>
    {
        { "NEUTRAL",   0 },  // Idle
        { "HAPPY",     1 },
        { "SAD",       2 },
        { "ANGRY",     3 },
        { "SURPRISED", 4 },
        { "THINKING",  5 },
        { "TALKING",   6 },
        { "WALKING",   7 },
    };

    public void Init(Animator animator)
    {
        _initialized = false;
        _animator = animator;
        if (_animator == null)
        {
            Debug.LogWarning("[AnimationController] No Animator found on avatar");
            return;
        }

        _initialized = true;
        Debug.Log("[AnimationController] Initialized");
    }

    /// <summary>
    /// Play the animation for the given emotion.
    /// </summary>
    public void PlayEmotion(string emotion)
    {
        if (!_initialized || _animator == null) return;

        string upper = emotion?.ToUpper() ?? "NEUTRAL";

        if (EmotionIndex.TryGetValue(upper, out int index))
        {
            _animator.SetInteger(EMOTION_PARAM, index);
        }
        else
        {
            _animator.SetInteger(EMOTION_PARAM, 0); // fallback to Idle
            Debug.LogWarning($"[AnimationController] Unknown emotion '{emotion}', playing Idle");
        }
    }

    /// <summary>
    /// Force-play a specific state by name (e.g., "Talking").
    /// </summary>
    public void PlayState(string stateName)
    {
        if (!_initialized || _animator == null) return;
        _animator.Play(stateName, 0, 0f);
    }
}
