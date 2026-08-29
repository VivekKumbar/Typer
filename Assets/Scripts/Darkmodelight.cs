using UnityEngine;

// Put this on a Point Light that is a CHILD of an enemy. The light only switches
// on in Dark Mode (so enemies glow in the dark). Set the glow's intensity / range
// / color on the Light component itself — that's your editor control.
[RequireComponent(typeof(Light))]
public class DarkModeLight : MonoBehaviour
{
    void Awake()
    {
        GetComponent<Light>().enabled = DarkMode.Enabled;
    }
}