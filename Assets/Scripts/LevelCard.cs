using UnityEngine;
using UnityEngine.SceneManagement;

// Attach to each carousel card. Holds what this card launches.
public class LevelCard : MonoBehaviour
{
    [Header("Identity")]
    public string levelName = "Endless";
    [Tooltip("Scene to load when this card is played.")]
    public string sceneToLoad = "GameScene";

    [Header("Mode flags (optional)")]
    public bool forceDarkMode = false;
    public bool setDarkMode = false; // if true, applies forceDarkMode value

    [Tooltip("PlayerPrefs key to store which mode/level was chosen.")]
    public string modeKey = "SelectedMode";
    public int modeId = 0;

    public void Launch()
    {
        if (setDarkMode) DarkMode.Enabled = forceDarkMode;
        PlayerPrefs.SetInt(modeKey, modeId);
        PlayerPrefs.Save();
        // Loading handled by your MainMenu loader if you route through it;
        // otherwise load directly:
        SceneManager.LoadScene(sceneToLoad);
    }
}