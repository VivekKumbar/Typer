using UnityEngine;

// Pause/resume the game. Freezes everything via Time.timeScale = 0 (enemy
// movement, bullets, spawning all stop) and shows a pause panel.
// - Pause button  -> Pause()
// - Resume button -> Resume()
// (Restart / Main Menu buttons reuse the RestartButton script.)
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }

    public GameObject pausePanel;   // the pause UI, disabled by default

    public bool IsPaused { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    public void Pause()
    {
        // Don't allow pausing once the game is over
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        IsPaused = true;
        Time.timeScale = 0f;
        if (pausePanel) pausePanel.SetActive(true);
        BridgeManager.SendLevelPaused();
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        if (pausePanel) pausePanel.SetActive(false);
        BridgeManager.SendLevelResumed();
    }
}