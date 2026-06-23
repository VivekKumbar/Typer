using UnityEngine;
using UnityEngine.UI;

// Put on an object in the MAIN MENU. Assign a UI Toggle to it, OR hook a plain
// Button's OnClick to ToggleDarkMode(). Saves the choice for the game scene.
public class DarkModeToggle : MonoBehaviour
{
    public Toggle toggle;

    void Start()
    {
        if (toggle != null)
        {
            toggle.isOn = DarkMode.Enabled;                // reflect saved state
            toggle.onValueChanged.AddListener(SetDarkMode);
        }
    }

    public void SetDarkMode(bool on) { DarkMode.Enabled = on; }
    public void ToggleDarkMode() { DarkMode.Enabled = !DarkMode.Enabled; }
}
