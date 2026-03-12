using UnityEngine;

/// <summary>
/// Shows/hides an offline indicator when WebSocket is disconnected.
/// Uses Unity's OnGUI for a simple overlay — no Canvas dependency.
/// </summary>
public class OfflineMode : MonoBehaviour
{
    private bool _connected;
    private float _disconnectedTime;
    private bool _showingOffline;

    // Style
    private GUIStyle _labelStyle;

    public void OnConnected()
    {
        _connected = true;
        _disconnectedTime = 0f;
        _showingOffline = false;
        Debug.Log("[OfflineMode] Connected — hiding offline indicator");
    }

    public void OnDisconnected()
    {
        _connected = false;
        _disconnectedTime = 0f;
        Debug.Log("[OfflineMode] Disconnected — starting offline timer");
    }

    void Update()
    {
        if (!_connected)
        {
            _disconnectedTime += Time.deltaTime;

            if (!_showingOffline && _disconnectedTime >= Config.OfflineThreshold)
            {
                _showingOffline = true;
                Debug.Log("[OfflineMode] Showing offline indicator");
            }
        }
    }

    void OnGUI()
    {
        if (!_showingOffline) return;

        // Lazy-init style
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.3f, 0.3f, 0.9f) }
            };
        }

        // Draw a semi-transparent bar at the top
        float barHeight = 40f;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, barHeight), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Draw text
        string statusText = $"OFFLINE — Reconnecting... ({_disconnectedTime:F0}s)";
        GUI.Label(new Rect(0, 0, Screen.width, barHeight), statusText, _labelStyle);
    }
}
