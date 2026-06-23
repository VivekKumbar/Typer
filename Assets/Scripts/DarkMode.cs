using UnityEngine;

// Simple persistent flag for the dark mode setting.
public static class DarkMode
{
    const string KEY = "TypeKeep_DarkMode";

    public static bool Enabled
    {
        get { return PlayerPrefs.GetInt(KEY, 0) == 1; }
        set { PlayerPrefs.SetInt(KEY, value ? 1 : 0); PlayerPrefs.Save(); }
    }
}