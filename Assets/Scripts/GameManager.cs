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
    [Tooltip("When checked, DamageFortress() is a full no-op -- used by FTUEScene so the tutorial's tower can never take damage. Leave unchecked for the real GameScene.")]
    public bool invulnerable = false;

    [Header("Economy")]
    public int coins = 0;             // spendable this run
    public int coinsEarnedThisRun = 0; // total earned this run (for banking)
    private bool earningsBanked = false;

    [Header("Shield")]
    public int shield = 0;      // absorbs damage before health

    [Header("Upgrade: Repair (optional)")]
    [Tooltip("If assigned, each level heals this many HP automatically at the start of every wave. Boss (6) also heals Boss Value HP instantly the moment it's picked.")]
    public UpgradeDefinition repairUpgrade;

    public bool IsGameOver { get; private set; }

    // DEBUG CONSOLE HOOK: "godmode". Static because DebugConsole may toggle
    // it from a scene where GameManager itself doesn't exist yet (e.g. right
    // before loading into GameScene). When off (the default) this has zero
    // effect on normal play.
    public static bool DebugGodMode = false;

    // The UI subscribes to these so it auto-updates. No polling needed.
    public event Action<int, int> OnHealthChanged; // (current, max)
    public event Action<int> OnCoinsChanged;        // (total)
    public event Action<int> OnShieldChanged;      // (shield amount)
    public event Action OnGameOver;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (SaveManager.IsContinuing && SaveManager.HasSave())
        {
            RunSaveData save = SaveManager.LoadRun();
            currentHealth = Mathf.Clamp(save.health, 0, maxHealth);
            shield = Mathf.Max(0, save.gmShield);
            coins = Mathf.Max(0, save.coins);
            coinsEarnedThisRun = Mathf.Max(0, save.coinsEarnedThisRun);
            RunContext.RestoreFromSave(save); // word packs: exactly what was locked in when saved
        }
        else
        {
            currentHealth = maxHealth;
            RunContext.LockForNewRun(); // word packs: fresh snapshot of the shop's current selection
        }
    }

    void Start()
    {
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnCoinsChanged?.Invoke(coins);
        OnShieldChanged?.Invoke(shield);

        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnUpgradeChanged += HandleUpgradeChanged;
    }

    void OnDestroy()
    {
        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnUpgradeChanged -= HandleUpgradeChanged;
    }

    void HandleUpgradeChanged(UpgradeDefinition def, int newLevel)
    {
        if (def == null || def != repairUpgrade) return;
        if (newLevel >= UpgradeDefinition.BossLevel)
            HealFortress(Mathf.RoundToInt(repairUpgrade.bossValue)); // instant chunk heal on reaching boss
    }

    // Called by WaveManager at the start of each wave.
    public void ApplyRepairUpgrade()
    {
        if (repairUpgrade == null || UpgradeManager.Instance == null) return;
        int level = UpgradeManager.Instance.LevelOf(repairUpgrade);
        if (level <= 0) return;
        HealFortress(Mathf.RoundToInt(repairUpgrade.ValueForLevel(level)));
    }

    public void AddCoins(int amount)
    {
        coins += amount;
        coinsEarnedThisRun += amount;
        StatsManager.RecordCoins(amount);
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
        if (IsGameOver || invulnerable) return;
        if (DebugGodMode) return; // debug console "godmode" -- fortress takes no damage

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
            SaveManager.ClearSave(); // run is over — nothing left to continue
            BridgeManager.SendLevelFailed(WaveManager.Instance != null ? WaveManager.Instance.CurrentWaveNumber : 1);
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

        int wave = WaveManager.Instance != null ? WaveManager.Instance.CurrentWaveNumber : 0;
        int peakCombo = ComboManager.Instance != null ? ComboManager.Instance.HighestComboThisRun : 0;
        StatsManager.EndRun(wave, peakCombo, coinsEarnedThisRun, StatsManager.CurrentRunAccuracy);
    }

    // Writes a mid-run save if the run is still active — used when pausing to
    // the Main Menu (RestartButton.GoToMenu) and from the app-exit hooks
    // below. Unlike BankEarnings this has no "only once" guard: saving is
    // idempotent (each write just overwrites the last), so it's safe to call
    // on every background/foreground cycle, not just the first.
    public void SaveProgressIfActive()
    {
        if (IsGameOver) return;
        if (WaveManager.Instance == null) return;
        SaveManager.CaptureAndSave(WaveManager.Instance.CurrentWaveNumber);
    }

    // If the app is closed/backgrounded mid-run, bank what we have and save
    // the run so Continue picks it back up. Mirrors BankEarnings' own guard
    // (IsGameOver) rather than sharing its one-time latch, since a save write
    // should still happen on every background, not just the first.
    void OnApplicationPause(bool paused)
    {
        if (!paused) return;
        BankEarnings();
        SaveProgressIfActive();
    }

    void OnApplicationQuit()
    {
        BankEarnings();
        SaveProgressIfActive();
    }
}