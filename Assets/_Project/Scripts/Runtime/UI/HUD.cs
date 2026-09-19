using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Listens to GameManager events AND initializes itself immediately on Start,
// so the health bar begins FULL (not empty) regardless of script order.
public class HUD : MonoBehaviour
{
    public Slider healthBar;
    public GameObject gameOverPanel;

    [Header("Game Over stats (filled once, when the panel opens)")]
    [Tooltip("e.g. 'WAVE 5'. Optional.")]
    public TMP_Text gameOverWaveText;
    [Tooltip("e.g. 'COINS 120'. Optional.")]
    public TMP_Text gameOverCoinsText;
    [Tooltip("e.g. 'WPM 42' (or 'WPM N/A' if the run ended almost instantly). This is the ONLY place WPM is shown anywhere in the game.")]
    public TMP_Text gameOverWpmText;

    void Start()
    {
        var gm = GameManager.Instance;
        gm.OnHealthChanged += UpdateHealth;
        gm.OnGameOver += ShowGameOver;
        if (gameOverPanel) gameOverPanel.SetActive(false);

        // Pull the current value right now, in case GameManager already fired
        // its startup event before this HUD subscribed. This makes the bar
        // start full and only ever go DOWN.
        UpdateHealth(gm.currentHealth, gm.maxHealth);
    }

    void OnDestroy()
    {
        if (GameManager.Instance == null) return;
        var gm = GameManager.Instance;
        gm.OnHealthChanged -= UpdateHealth;
        gm.OnGameOver -= ShowGameOver;
    }

    void UpdateHealth(int cur, int max)
    {
        if (healthBar) { healthBar.maxValue = max; healthBar.value = cur; }
    }

    void ShowGameOver()
    {
        SfxPlayer.PlayGameOver();
        FillGameOverStats();
        if (gameOverPanel) gameOverPanel.SetActive(true);
    }

    void FillGameOverStats()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        int wave = WaveManager.Instance != null ? WaveManager.Instance.CurrentWaveNumber : 1;
        if (gameOverWaveText) gameOverWaveText.text = "WAVE " + wave;
        if (gameOverCoinsText) gameOverCoinsText.text = "COINS " + gm.coinsEarnedThisRun;

        bool hasWpm = gm.TryGetWpm(out float wpm);
        if (gameOverWpmText) gameOverWpmText.text = hasWpm ? "WPM " + Mathf.RoundToInt(wpm) : "WPM N/A";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[WPM] correctChars={gm.correctCharactersThisRun} activeSeconds={gm.activeGameplaySeconds:F1} " +
                  $"wpm={(hasWpm ? wpm.ToString("F1") : "N/A")} realtimeSinceStartup={Time.realtimeSinceStartup:F1}");
#endif
    }
}
