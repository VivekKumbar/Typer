using UnityEngine;
using UnityEngine.UI;

// Assign a UI Toggle, or hook a Button's OnClick to ToggleDarkMode().
// Applies the change live if a DarkModeController exists in this scene.
public class DarkModeToggle : MonoBehaviour
{
    public Toggle toggle;

    void Start()
    {
        if (toggle != null)
        {
            toggle.isOn = DarkMode.Enabled;
            toggle.onValueChanged.AddListener(SetDarkMode);
        }
    }

    public void SetDarkMode(bool on)
    {
        DarkMode.Enabled = on;

        // Apply immediately if we're in a scene that has the controller
        DarkModeController c = FindObjectOfType<DarkModeController>();
        if (c != null) c.SetDarkMode(on);
    }

    public void ToggleDarkMode() { SetDarkMode(!DarkMode.Enabled); }
}
