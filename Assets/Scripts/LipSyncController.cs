using UnityEngine;
using UniVRM10;

/// <summary>
/// Simple amplitude-based lip sync.
/// Reads audio amplitude from AudioPlayer and sets the VRM Aa expression weight.
/// Uses direct blendshape writes to bypass VRM OverrideMouth blocking.
/// Execution order 99 ensures this runs BEFORE AvatarController (100) calls Process().
/// </summary>
[DefaultExecutionOrder(99)]
public class LipSyncController : MonoBehaviour
{
    [Header("References")]
    public AudioPlayer audioPlayer;

    private Vrm10Instance _vrm;
    private bool _initialized;
    private float _currentMouth;

    private SkinnedMeshRenderer _mouthMesh;
    private int _mouthBlendShapeIdx = -1;

    public void Init(Vrm10Instance vrm)
    {
        _initialized = false;
        _currentMouth = 0f;
        _mouthMesh = null;
        _mouthBlendShapeIdx = -1;

        _vrm = vrm;
        if (vrm == null) return;

        if (_vrm.Runtime?.Expression != null)
        {
            _initialized = true;
            Debug.Log("[LipSyncController] Initialized (VRM expression system)");
        }
        else
        {
            Debug.LogWarning("[LipSyncController] VRM Runtime.Expression is null — lip sync disabled");
            return;
        }

        string[] mouthNames = { "Fcl_MTH_A", "あ", "A", "vrc.v_aa", "MTH_A", "mouth_a", "Mouth_A" };
        var meshes = _vrm.GetComponentsInChildren<SkinnedMeshRenderer>();

        foreach (var mesh in meshes)
        {
            if (mesh.sharedMesh == null) continue;

            foreach (string name in mouthNames)
            {
                int idx = mesh.sharedMesh.GetBlendShapeIndex(name);
                if (idx >= 0)
                {
                    _mouthMesh = mesh;
                    _mouthBlendShapeIdx = idx;
                    Debug.Log($"[LipSyncController] Found mouth blendshape '{name}' at index {idx} on mesh '{mesh.name}'");
                    goto done;
                }
            }

            int count = mesh.sharedMesh.blendShapeCount;
            if (count > 0)
            {
                string names = "";
                for (int i = 0; i < count; i++)
                {
                    string n = mesh.sharedMesh.GetBlendShapeName(i);
                    if (n.ToLower().Contains("mth") || n.ToLower().Contains("mouth") || n.ToLower().Contains("a"))
                        names += $"  [{i}] {n}\n";
                }
                if (names.Length > 0)
                    Debug.Log($"[LipSyncController] Potential mouth shapes on '{mesh.name}':\n{names}");
            }
        }
        done:

        if (_mouthBlendShapeIdx < 0)
            Debug.LogWarning("[LipSyncController] No mouth blendshape found — direct fallback disabled, using VRM expression only");
    }

    void LateUpdate()
    {
        if (!_initialized) return;
        if (audioPlayer == null) return;
        if (_vrm?.Runtime?.Expression == null) return;

        // Get amplitude and scale it
        float targetMouth = Mathf.Clamp01(audioPlayer.CurrentAmplitude * Config.LipSyncSensitivity);

        // Smooth the mouth movement
        _currentMouth = Mathf.Lerp(_currentMouth, targetMouth, Time.deltaTime * Config.LipSyncSmoothing);

        // Snap to zero when very small
        if (_currentMouth < 0.01f)
            _currentMouth = 0f;

        // Set Aa expression weight via VRM system
        _vrm.Runtime.Expression.SetWeight(ExpressionKey.Aa, _currentMouth);

        // Direct blendshape fallback (bypasses VRM OverrideMouth)
        if (_mouthMesh != null && _mouthBlendShapeIdx >= 0)
            _mouthMesh.SetBlendShapeWeight(_mouthBlendShapeIdx, _currentMouth * 100f);
    }
}
