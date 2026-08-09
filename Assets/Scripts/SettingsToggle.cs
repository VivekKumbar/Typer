using UnityEngine;
using UnityEngine.UI;

// Wires one UI Toggle to one persisted GameSettings flag. Assign the Toggle
// and pick which setting this row controls. Same pattern as DarkModeToggle:
// syncs the toggle to the saved value and wires onValueChanged, both in
// Start() so it's correct the moment the Settings panel is first shown.
public class SettingsToggle : MonoBehaviour
{
    public enum Setting { Sfx, Music, Vibration }

    public Toggle toggle;
    public Setting setting;

    void Start()
    {
        if (toggle == null) return;

        toggle.isOn = CurrentValue();
        toggle.onValueChanged.AddListener(SetValue);
    }

    bool CurrentValue()
    {
        switch (setting)
        {
            case Setting.Sfx: return GameSettings.SfxEnabled;
            case Setting.Music: return GameSettings.MusicEnabled;
            case Setting.Vibration: return GameSettings.VibrationEnabled;
            default: return true;
        }
    }

    void SetValue(bool on)
    {
        switch (setting)
        {
            case Setting.Sfx: GameSettings.SfxEnabled = on; break;
            case Setting.Music: GameSettings.MusicEnabled = on; break;
            case Setting.Vibration: GameSettings.VibrationEnabled = on; break;
        }
    }
}
