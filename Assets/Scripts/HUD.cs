using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Listens to GameManager events and updates the on-screen UI.
public class HUD : MonoBehaviour
{
    public Slider healthBar;
    public TMP_Text coinText;
    public GameObject gameOverPanel;

    void Start()
    {
        var gm = GameManager.Instance;
        gm.OnHealthChanged += UpdateHealth;
        gm.OnCoinsChanged  += UpdateCoins;
        gm.OnGameOver      += ShowGameOver;
        if (gameOverPanel) gameOverPanel.SetActive(false);
    }

    void OnDestroy()
    {
        if (GameManager.Instance == null) return;
        var gm = GameManager.Instance;
        gm.OnHealthChanged -= UpdateHealth;
        gm.OnCoinsChanged  -= UpdateCoins;
        gm.OnGameOver      -= ShowGameOver;
    }

    void UpdateHealth(int cur, int max) { if (healthBar) { healthBar.maxValue = max; healthBar.value = cur; } }
    void UpdateCoins(int total)         { if (coinText) coinText.text = total.ToString(); }
    void ShowGameOver()                 { if (gameOverPanel) gameOverPanel.SetActive(true); }
}
