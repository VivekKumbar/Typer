using System;
using UnityEngine;

// Central game state: fortress health, coins, game-over.
// Singleton so any script can reach it via GameManager.Instance.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Fortress")]
    public int maxHealth = 100;
    public int currentHealth;

    [Header("Economy")]
    public int coins = 0;

    public bool IsGameOver { get; private set; }

    // The UI subscribes to these so it auto-updates. No polling needed.
    public event Action<int, int> OnHealthChanged; // (current, max)
    public event Action<int> OnCoinsChanged;        // (total)
    public event Action OnGameOver;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        currentHealth = maxHealth;
    }

    void Start()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnCoinsChanged?.Invoke(coins);
    }

    public void AddCoins(int amount)
    {
        coins += amount;
        OnCoinsChanged?.Invoke(coins);
    }

    public bool SpendCoins(int amount)
    {
        if (coins < amount) return false;
        coins -= amount;
        OnCoinsChanged?.Invoke(coins);
        return true;
    }

    public void DamageFortress(int amount)
    {
        if (IsGameOver) return;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        if (currentHealth <= 0)
        {
            IsGameOver = true;
            OnGameOver?.Invoke();
            Time.timeScale = 0f; // freeze the game
        }
    }

    public void HealFortress(int amount)
    {
        if (IsGameOver) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
