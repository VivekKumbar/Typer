using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Listens to GameManager events AND initializes itself immediately on Start,
// so the health bar begins FULL (not empty) regardless of script order.
public class HUD : MonoBehaviour
{
    public Slider healthBar;
    [Tooltip("Optional LoadingBarUI for smooth health animation.")]
    public LoadingBarUI healthLoadingBar;
    [Tooltip("Optional secondary health bar on the abilities row (RepairBar).")]
    public Slider repairBar;
    private LoadingBarUI repairLoadingBar;
    [Tooltip("Optional 'cur/max' label over the health bar (e.g. '100/100'). Kept in sync wherever healthBar itself is.")]
    public TMP_Text healthText;
    public GameObject gameOverPanel;

    [Header("Game Over stats (filled once, when the panel opens)")]
    [Tooltip("e.g. 'WAVE 5'. Optional.")]
    public TMP_Text gameOverWaveText;
    [Tooltip("e.g. 'COINS 120'. Optional.")]
    public TMP_Text gameOverCoinsText;
    [Tooltip("e.g. 'WPM 42' (or 'WPM N/A' if the run ended almost instantly). This is the ONLY place WPM is shown anywhere in the game.")]
    public TMP_Text gameOverWpmText;
    [Tooltip("Persistent wave indicator text in the corner of HUD. Optional.")]
    public TMP_Text persistentWaveText;

    private WaveBanner cachedWaveBanner;

    void Start()
    {
        var gm = GameManager.Instance;
        gm.OnHealthChanged += UpdateHealth;
        gm.OnGameOver += ShowGameOver;
        if (gameOverPanel) gameOverPanel.SetActive(false);

        if (persistentWaveText != null && WaveManager.Instance != null)
        {
            persistentWaveText.text = $"Wave {WaveManager.Instance.currentWave}";
            WaveManager.Instance.OnWaveStarted += UpdateWaveDisplay;
        }

        cachedWaveBanner = FindAnyObjectByType<WaveBanner>();

        if (healthLoadingBar == null && healthBar != null)
            healthLoadingBar = healthBar.GetComponent<LoadingBarUI>();

        if (repairBar == null)
        {
            foreach (var s in Resources.FindObjectsOfTypeAll<Slider>())
            {
                if (s != null && s.gameObject.name == "RepairBar")
                {
                    repairBar = s;
                    break;
                }
            }
        }
        if (repairBar != null && repairLoadingBar == null)
            repairLoadingBar = repairBar.GetComponent<LoadingBarUI>();

        // Pull the current value right now, in case GameManager already fired
        // its startup event before this HUD subscribed. Snap to current health.
        float frac = gm.maxHealth > 0 ? (float)gm.currentHealth / gm.maxHealth : 1f;
        if (healthLoadingBar != null) healthLoadingBar.SnapTo01(frac);
        else if (healthBar != null) { healthBar.minValue = 0f; healthBar.maxValue = 1f; healthBar.value = frac; }

        if (repairLoadingBar != null) repairLoadingBar.SnapTo01(frac);
        else if (repairBar != null) { repairBar.minValue = 0f; repairBar.maxValue = 1f; repairBar.value = frac; }

        // Top health bar is for health display only (not the repair button)
        if (healthBar != null)
        {
            healthBar.interactable = false;
        }

        if (healthText) healthText.text = gm.currentHealth + "/" + gm.maxHealth;
    }

    void WireHealthClickTarget(GameObject root, Transform bounceTarget = null)
    {
        if (root == null) return;
        Transform bounce = bounceTarget != null ? bounceTarget : root.transform;

        var handler = root.GetComponent<HealthBarClickHandler>();
        if (handler == null) handler = root.AddComponent<HealthBarClickHandler>();
        handler.bounceTarget = bounce;

        foreach (var g in root.GetComponentsInChildren<Graphic>(true))
        {
            g.raycastTarget = true;
            var childHandler = g.GetComponent<HealthBarClickHandler>();
            if (childHandler == null) childHandler = g.gameObject.AddComponent<HealthBarClickHandler>();
            childHandler.bounceTarget = bounce;
        }
    }

    void OnDestroy()
    {
        if (WaveManager.Instance != null)
            WaveManager.Instance.OnWaveStarted -= UpdateWaveDisplay;

        if (GameManager.Instance == null) return;
        var gm = GameManager.Instance;
        gm.OnHealthChanged -= UpdateHealth;
        gm.OnGameOver -= ShowGameOver;
    }

    void UpdateWaveDisplay(int wave)
    {
        if (persistentWaveText != null)
            persistentWaveText.text = $"Wave {wave}";
    }

    void UpdateHealth(int cur, int max)
    {
        float frac = max > 0 ? (float)cur / max : 0f;
        if (healthLoadingBar != null)
        {
            healthLoadingBar.SetTargetProgress01(frac);
        }
        else if (healthBar != null)
        {
            healthBar.minValue = 0f;
            healthBar.maxValue = 1f;
            healthBar.value = frac;
        }

        if (repairLoadingBar != null)
        {
            repairLoadingBar.SetTargetProgress01(frac);
        }
        else if (repairBar != null)
        {
            repairBar.minValue = 0f;
            repairBar.maxValue = 1f;
            repairBar.value = frac;
        }

        if (healthText) healthText.text = cur + "/" + max;
    }

    void ShowGameOver()
    {
        SfxPlayer.PlayGameOver();
        FillGameOverStats();
        // If death happens mid wave-announce/countdown, WaveManager's coroutine
        // just bails (see its own "if (GameOver) yield break;") without hiding
        // the banner -- it stays on screen, showing through the Game Over
        // dimmer's 88% opacity as a faint ghost behind the crest card. Force it
        // closed here so the popup is never sharing the screen with stray text.
        if (cachedWaveBanner != null) cachedWaveBanner.Hide();
        if (gameOverPanel) gameOverPanel.SetActive(true);
    }

    public void HideGameOver()
    {
        if (gameOverPanel) gameOverPanel.SetActive(false);
    }

    public void SnapHealthToFull()
    {
        if (healthLoadingBar != null) healthLoadingBar.SnapTo01(1f);
        else if (healthBar != null) healthBar.value = 1f;

        if (repairLoadingBar != null) repairLoadingBar.SnapTo01(1f);
        else if (repairBar != null) repairBar.value = 1f;

        if (GameManager.Instance != null && healthText != null)
            healthText.text = GameManager.Instance.maxHealth + "/" + GameManager.Instance.maxHealth;
    }

    void FillGameOverStats()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        int wave = WaveManager.Instance != null ? WaveManager.Instance.CurrentWaveNumber : 1;
        if (gameOverWaveText) gameOverWaveText.text = "WAVE " + wave;
        if (gameOverCoinsText) gameOverCoinsText.text = gm.coinsEarnedThisRun.ToString();

        bool hasWpm = gm.TryGetWpm(out float wpm);
        if (gameOverWpmText) gameOverWpmText.text = hasWpm ? Mathf.RoundToInt(wpm).ToString() : "N/A";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[WPM] correctChars={gm.correctCharactersThisRun} activeSeconds={gm.activeGameplaySeconds:F1} " +
                  $"wpm={(hasWpm ? wpm.ToString("F1") : "N/A")} realtimeSinceStartup={Time.realtimeSinceStartup:F1}");
#endif
    }
}
