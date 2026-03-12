using UnityEngine;

/// <summary>
/// Auto-frames the camera to keep the VRM avatar visible regardless of
/// screen aspect ratio (portrait tablet, landscape desktop, etc.).
/// Attach to the Main Camera or let AvatarController find it.
/// </summary>
public class CameraFraming : MonoBehaviour
{
    [Header("Framing Target")]
    [Tooltip("Height on the avatar to look at (Y). VRM origin is at feet.")]
    [SerializeField] private float lookAtHeight = 1.2f;

    [Tooltip("Distance from avatar at the reference aspect ratio (16:9 landscape).")]
    [SerializeField] private float baseDistance = 0.8f;

    [Tooltip("Camera height at the reference aspect ratio.")]
    [SerializeField] private float baseHeight = 1.2f;

    [Header("Portrait Adjustments")]
    [Tooltip("Extra distance multiplier when in portrait mode. Pulls camera back so full body fits.")]
    [SerializeField] private float portraitDistanceMultiplier = 1.8f;

    [Tooltip("Camera height offset in portrait mode. Moves camera down to center the avatar.")]
    [SerializeField] private float portraitHeightOffset = -0.1f;

    private Camera _cam;
    private float _lastAspect;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        if (_cam == null)
            _cam = Camera.main;
    }

    void Start()
    {
        AdjustCamera();
    }

    void Update()
    {
        // Re-adjust if aspect ratio changes (rotation, window resize)
        if (!Mathf.Approximately(_cam.aspect, _lastAspect))
        {
            AdjustCamera();
        }
    }

    void AdjustCamera()
    {
        if (_cam == null) return;

        _lastAspect = _cam.aspect;
        bool isPortrait = _cam.aspect < 1f;

        float distance = baseDistance;
        float height = baseHeight;

        if (isPortrait)
        {
            // In portrait, the horizontal FOV is much narrower.
            // Pull the camera back so the avatar fits width-wise,
            // and adjust height to keep her centered.
            float aspectScale = 1f / _cam.aspect; // e.g., 0.6 aspect -> 1.67x
            distance = baseDistance * Mathf.Lerp(1f, portraitDistanceMultiplier, 
                Mathf.InverseLerp(1f, 2f, aspectScale));
            height = baseHeight + portraitHeightOffset;
        }

        // Position the camera: Z offset from origin, facing back toward avatar
        transform.position = new Vector3(0f, height, distance);
        transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        Debug.Log($"[CameraFraming] aspect={_cam.aspect:F2}, portrait={isPortrait}, pos={transform.position}, dist={distance:F2}");
    }
}
