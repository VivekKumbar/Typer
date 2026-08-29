using System;
using UnityEngine;

// Persistent player settings: SFX, Music, Vibration. Same simple static
// PlayerPrefs-backed pattern as Wallet.cs / DarkMode.cs. Each setting fires
// its own OnChanged event so other systems (SfxPlayer, a future music
// player, haptics) can react live instead of polling. All default to ON.
public static class GameSettings
{
    const string SfxKey = "TypeKeep_SfxEnabled";
    const string MusicKey = "TypeKeep_MusicEnabled";
    const string VibrationKey = "TypeKeep_VibrationEnabled";

    public static event Action<bool> OnSfxChanged;
    public static event Action<bool> OnMusicChanged;
    public static event Action<bool> OnVibrationChanged;

    public static bool SfxEnabled
    {
        get { return PlayerPrefs.GetInt(SfxKey, 1) == 1; }
        set
        {
            PlayerPrefs.SetInt(SfxKey, value ? 1 : 0);
            PlayerPrefs.Save();
            OnSfxChanged?.Invoke(value);
        }
    }

    public static bool MusicEnabled
    {
        get { return PlayerPrefs.GetInt(MusicKey, 1) == 1; }
        set
        {
            PlayerPrefs.SetInt(MusicKey, value ? 1 : 0);
            PlayerPrefs.Save();
            OnMusicChanged?.Invoke(value);
        }
    }

    public static bool VibrationEnabled
    {
        get { return PlayerPrefs.GetInt(VibrationKey, 1) == 1; }
        set
        {
            PlayerPrefs.SetInt(VibrationKey, value ? 1 : 0);
            PlayerPrefs.Save();
            OnVibrationChanged?.Invoke(value);
        }
    }
}
