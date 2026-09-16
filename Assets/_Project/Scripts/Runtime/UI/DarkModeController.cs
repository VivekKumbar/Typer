using UnityEngine;
using UnityEngine.Rendering;

public class DarkModeController : MonoBehaviour
{
    [Header("Scene refs")]
    public Light directionalLight;
    public Camera targetCamera;

    [Header("Volumes")]
    public GameObject normalVolume;
    public GameObject darkVolume;

    [Header("Tower light (dark mode only)")]
    [Tooltip("A light placed on/above the tower. Only switched on in dark mode.")]
    public Light towerLight;
    public float towerLightIntensity = 15f;
    public Color towerLightColor = new Color(1f, 0.9f, 0.7f);
    public float towerLightRange = 12f;

    [Header("Normal mode (Dark Mode OFF)")]
    public float normalDirectionalIntensity = 1f;
    public Color normalAmbient = new Color(0.55f, 0.55f, 0.55f);
    public Color normalBackground = new Color(0.85f, 0.85f, 0.85f);

    [Header("Dark mode (ON)")]
    [Range(0f, 1f)] public float darkDirectionalIntensity = 0.05f;
    public Color darkAmbient = new Color(0.03f, 0.03f, 0.06f);
    public Color darkBackground = Color.black;

    [Header("Debug")]
    public bool debugLog = true;

    void Start() { Apply(DarkMode.Enabled); }

    public void SetDarkMode(bool dark)
    {
        DarkMode.Enabled = dark;
        Apply(dark);
    }

    void Apply(bool dark)
    {
        if (debugLog) Debug.Log("[DarkMode] Applying dark = " + dark, this);

        // Volumes — exactly one active
        if (normalVolume != null) normalVolume.SetActive(!dark);
        else if (debugLog) Debug.LogWarning("[DarkMode] normalVolume NOT assigned!", this);

        if (darkVolume != null) darkVolume.SetActive(dark);
        else if (debugLog) Debug.LogWarning("[DarkMode] darkVolume NOT assigned!", this);

        // Ambient
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = dark ? darkAmbient : normalAmbient;

        // Sun
        if (directionalLight)
        {
            directionalLight.gameObject.SetActive(true);
            directionalLight.enabled = true;
            directionalLight.intensity = dark ? darkDirectionalIntensity : normalDirectionalIntensity;
        }

        // Background
        if (targetCamera)
        {
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = dark ? darkBackground : normalBackground;
        }
        else if (debugLog) Debug.LogWarning("[DarkMode] targetCamera NOT assigned!", this);

        // TOWER LIGHT — only glows in dark mode
        if (towerLight != null)
        {
            towerLight.enabled = dark;
            towerLight.intensity = towerLightIntensity;
            towerLight.color = towerLightColor;
            towerLight.range = towerLightRange;
        }
        else if (debugLog) Debug.LogWarning("[DarkMode] towerLight NOT assigned (optional)", this);
    }
}