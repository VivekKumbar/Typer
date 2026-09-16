using UnityEngine;

// Drop this on any GameObject in your scene. Shows FPS on-screen, including in
// mobile builds. Uses OnGUI so it needs no Canvas or UI setup.
public class FpsDisplay : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("How often the number updates, in seconds.")]
    public float refreshRate = 0.5f;
    [Tooltip("Screen corner.")]
    public Corner corner = Corner.TopLeft;
    public int fontSize = 40;

    public enum Corner { TopLeft, TopRight, BottomLeft, BottomRight }

    private float timer;
    private float fps;
    private float minFps = Mathf.Infinity;
    private GUIStyle style;

    void Update()
    {
        // Smooth-ish current FPS
        float current = 1f / Time.unscaledDeltaTime;
        if (current < minFps && Time.timeSinceLevelLoad > 2f) minFps = current;

        timer += Time.unscaledDeltaTime;
        if (timer >= refreshRate)
        {
            fps = current;
            timer = 0f;
        }
    }

    void OnGUI()
    {
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.fontSize = fontSize;
            style.fontStyle = FontStyle.Bold;
        }

        // Color-code: green good, yellow ok, red bad
        Color c = fps >= 50 ? Color.green : fps >= 30 ? Color.yellow : Color.red;
        style.normal.textColor = c;

        string text = "FPS: " + Mathf.RoundToInt(fps) + "   (min " + Mathf.RoundToInt(minFps) + ")";

        float w = 500f, h = fontSize + 20f;
        float pad = 20f;
        float x = (corner == Corner.TopLeft || corner == Corner.BottomLeft) ? pad : Screen.width - w - pad;
        float y = (corner == Corner.TopLeft || corner == Corner.TopRight) ? pad : Screen.height - h - pad;

        // account for text alignment on right-side corners
        style.alignment = (corner == Corner.TopRight || corner == Corner.BottomRight)
            ? TextAnchor.UpperRight : TextAnchor.UpperLeft;

        GUI.Label(new Rect(x, y, w, h), text, style);
    }
}