using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Listens to GameManager events AND initializes itself immediately on Start,
// so the health bar begins FULL (not empty) regardless of script order.
public class HUD : MonoBehaviour
{
    public Slider healthBar;
    public GameObject gameOverPanel;

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
        if (gameOverPanel) gameOverPanel.SetActive(true);
    }
}
