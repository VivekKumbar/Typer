using UnityEngine;
using UnityEngine.Rendering;

// Put on an empty object in the GAME scene. On start it reads the Dark Mode
// setting and either darkens the scene or leaves it normal. Tune everything here.
public class DarkModeController : MonoBehaviour
{
    [Header("Scene refs")]
    public Light directionalLight;
    public Camera targetCamera;

    [Header("Normal mode (Dark Mode OFF)")]
    public float normalDirectionalIntensity = 1f;
    public Color normalAmbient = new Color(0.55f, 0.55f, 0.55f);
    public Color normalBackground = new Color(0.85f, 0.85f, 0.85f);

    [Header("Dark mode (ON)")]
    [Range(0f, 1f)] public float darkDirectionalIntensity = 0.05f;
    public Color darkAmbient = new Color(0.03f, 0.03f, 0.06f);
    public Color darkBackground = Color.black;

    void Start() { Apply(DarkMode.Enabled); }

    void Apply(bool dark)
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = dark ? darkAmbient : normalAmbient;

        if (directionalLight)
            directionalLight.intensity = dark ? darkDirectionalIntensity : normalDirectionalIntensity;

        if (targetCamera)
        {
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = dark ? darkBackground : normalBackground;
        }
    }
}