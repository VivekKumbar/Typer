#if UNITY_STANDALONE && !UNITY_EDITOR
using UnityEngine;

// Desktop (Windows/Mac/Linux) builds only. The game is designed for a 9:16
// portrait screen, so open it as a portrait window that fits the tester's
// monitor instead of a fullscreen landscape view.
public static class StandaloneWindowFit
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void FitPortraitWindow()
    {
        Resolution display = Screen.currentResolution;
        int height = Mathf.RoundToInt(display.height * 0.85f); // leave room for the taskbar and title bar
        int width = Mathf.RoundToInt(height * 9f / 16f);
        Screen.SetResolution(width, height, FullScreenMode.Windowed);
    }
}
#endif
