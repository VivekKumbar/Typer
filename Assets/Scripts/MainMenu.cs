using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Put this on an object in your MAIN MENU scene.
// - Hook the Play button's OnClick to PlayGame()
// - Hook a Quit button (optional) to Quit()
// It loads the game scene asynchronously and shows a loading bar.
public class MainMenu : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("Exact name of your game scene (must be added to Build Settings).")]
    public string gameSceneName = "GameScene";

    [Header("Loading UI (optional, but you asked for it)")]
    public GameObject loadingPanel;   // full-screen panel, disabled by default
    public Slider progressBar;
    public TMP_Text progressText;
    [Tooltip("Keep the loading screen visible at least this long so it doesn't flash by.")]
    public float minShowTime = 1.2f;

    public void PlayGame() { StartCoroutine(LoadGame()); }

    public void Quit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // stops Play mode in the editor
#endif
    }

    IEnumerator LoadGame()
    {
        if (loadingPanel) loadingPanel.SetActive(true);
        float start = Time.unscaledTime;

        AsyncOperation op = SceneManager.LoadSceneAsync(gameSceneName);
        op.allowSceneActivation = false; // wait until we say go

        while (!op.isDone)
        {
            // Unity reports 0 -> 0.9 while loading, then holds at 0.9 until activated
            float progress = Mathf.Clamp01(op.progress / 0.9f);
            if (progressBar) progressBar.value = progress;
            if (progressText) progressText.text = Mathf.RoundToInt(progress * 100f) + "%";

            // Loaded AND minimum display time elapsed -> enter the game
            if (op.progress >= 0.9f && Time.unscaledTime - start >= minShowTime)
            {
                if (progressBar) progressBar.value = 1f;
                if (progressText) progressText.text = "100%";
                op.allowSceneActivation = true;
            }
            yield return null;
        }
    }
}