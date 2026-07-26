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
    public int coins = 0;             // spendable this run
    public int coinsEarnedThisRun = 0; // total earned this run (for banking)
    private bool earningsBanked = false;

    [Header("Shield")]
    public int shield = 0;      // absorbs damage before health

    public bool IsGameOver { get; private set; }

    // The UI subscribes to these so it auto-updates. No polling needed.
    public event Action<int, int> OnHealthChanged; // (current, max)
    public event Action<int> OnCoinsChanged;        // (total)
    public event Action<int> OnShieldChanged;      // (shield amount)
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
        OnShieldChanged?.Invoke(shield);
    }

    public void AddCoins(int amount)
    {
        coins += amount;
        coinsEarnedThisRun += amount;
        OnCoinsChanged?.Invoke(coins);
    }

    public bool SpendCoins(int amount)
    {
        if (coins < amount) return false;
        coins -= amount;
        OnCoinsChanged?.Invoke(coins);
        return true;
    }

    public void AddShield(int amount, bool stack = true)
    {
        shield = stack ? shield + amount : Mathf.Max(shield, amount);
        OnShieldChanged?.Invoke(shield);
    }

    public void DamageFortress(int amount) { DamageFortress(amount, Vector3.zero); }

    public void DamageFortress(int amount, Vector3 hitPos)
    {
        if (IsGameOver) return;

        // Shield soaks damage first (if one is up)
        if (ShieldManager.Instance != null)
            amount = ShieldManager.Instance.AbsorbDamage(amount, hitPos);
        if (amount <= 0) return;

        // Shield soaks damage first
        if (shield > 0)
        {
            int absorbed = Mathf.Min(shield, amount);
            shield -= absorbed;
            amount -= absorbed;
            OnShieldChanged?.Invoke(shield);
            if (amount <= 0) return;
        }

        currentHealth = Mathf.Max(0, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        if (currentHealth <= 0)
        {
            IsGameOver = true;
            BankEarnings();
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

    // Deposits this run's earnings into the persistent Wallet exactly once.
    // Safe to call from game over, quit-to-menu, or app background.
    public void BankEarnings()
    {
        if (earningsBanked) return;
        earningsBanked = true;
        Wallet.Add(coinsEarnedThisRun);
    }

    // If the app is closed/backgrounded mid-run, bank what we have.
    void OnApplicationPause(bool paused)
    {
        if (paused) BankEarnings();
    }

    void OnApplicationQuit()
    {
        BankEarnings();
    }
}