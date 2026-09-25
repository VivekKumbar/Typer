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

    [Header("WPM (per run) — shown ONLY on the Game Over panel")]
    [Tooltip("Accumulated seconds of genuine typing time this run. Only ticks while IsTypingWindowOpen AND Time.timeScale > 0 -- pause, upgrade draft, wave announce/countdown, boss warning, game over all stop it. Saved/restored with the run, same as coins.")]
    public float activeGameplaySeconds = 0f;
    [Tooltip("Correct keystrokes this run (fed by StatsManager's existing correct-letter path), counted only while IsTypingWindowOpen.")]
    public int correctCharactersThisRun = 0;
    [Tooltip("Below this much active time the WPM is meaningless (a run that ends almost instantly) -- Game Over shows N/A instead of a huge number.")]
    public float minSecondsForWpm = 5f;

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
            activeGameplaySeconds = Mathf.Max(0f, save.activeGameplaySeconds);      // Continue resumes the same WPM run
            correctCharactersThisRun = Mathf.Max(0, save.correctCharactersThisRun); // (old saves lack these -> 0)
            RunContext.RestoreFromSave(save); // word packs: exactly what was locked in when saved
        }
        else
        {
            currentHealth = maxHealth;
            coins = 0;
            coinsEarnedThisRun = 0;
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

        StatsManager.OnCorrectLetterRecorded += HandleCorrectLetter;

        StartCoroutine(AutosaveLoop());
    }

    private System.Collections.IEnumerator AutosaveLoop()
    {
        var wait = new WaitForSecondsRealtime(4f);
        while (true)
        {
            yield return wait;
            if (!IsGameOver && currentHealth > 0)
            {
                SaveProgressIfActive();
            }
        }
    }

    void OnDestroy()
    {
        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnUpgradeChanged -= HandleUpgradeChanged;

        StatsManager.OnCorrectLetterRecorded -= HandleCorrectLetter;
    }

    // ---- WPM ----------------------------------------------------------------

    // True only while the player can genuinely be typing: not game over, not
    // paused, not in the between-wave upgrade draft, and WaveManager is in its
    // live phase (not the wave banner / countdown / boss warning). Deliberately
    // does NOT look at Time.timeScale -- a kill's HitStop zeroes timeScale for
    // a few frames BEFORE the killing letter is recorded, so gating keystrokes
    // on it would drop the last letter of every word. (The clock below adds
    // its own timeScale check on top.)
    public bool IsTypingWindowOpen
    {
        get
        {
            if (IsGameOver) return false;
            if (PauseMenu.Instance != null && PauseMenu.Instance.IsPaused) return false;
            if (UpgradeManager.Instance != null && UpgradeManager.Instance.IsDraftOpen) return false;
            if (WaveManager.Instance != null && !WaveManager.Instance.IsWaveActive) return false;
            return true;
        }
    }

    // Accumulating clock, NOT a start/end timestamp: pausing just stops it
    // adding, resuming carries on from the same total. Unscaled delta so a
    // future slow-mo can't undercount real typing time; clamped so a tab
    // that was hidden (WebGL) can't dump one giant frame into the total.
    void Update()
    {
        if (Time.timeScale <= 0f || !IsTypingWindowOpen) return;
        activeGameplaySeconds += Mathf.Min(Time.unscaledDeltaTime, Time.maximumDeltaTime);
    }

    void HandleCorrectLetter()
    {
        if (IsTypingWindowOpen) correctCharactersThisRun++;
    }

    // Standard WPM: (chars / 5) / minutes. False (=> show "N/A") when there's
    // too little active time for the number to mean anything.
    public bool TryGetWpm(out float wpm)
    {
        wpm = 0f;
        if (activeGameplaySeconds < minSecondsForWpm) return false;
        wpm = (correctCharactersThisRun / 5f) / (activeGameplaySeconds / 60f);
        return true;
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

    private int coinsBankedThisRun = 0;

    // Deposits this run's earnings into the persistent Wallet exactly once (or unbanked delta upon continue).
    // Safe to call from game over, quit-to-menu, or app background.
    public void BankEarnings()
    {
        if (earningsBanked) return;
        earningsBanked = true;

        int toBank = Mathf.Max(0, coinsEarnedThisRun - coinsBankedThisRun);
        if (toBank > 0)
        {
            Wallet.Add(toBank);
            coinsBankedThisRun += toBank;
        }

        int wave = WaveManager.Instance != null ? WaveManager.Instance.CurrentWaveNumber : 0;
        int peakCombo = ComboManager.Instance != null ? ComboManager.Instance.HighestComboThisRun : 0;
        StatsManager.EndRun(wave, peakCombo, coinsEarnedThisRun, StatsManager.CurrentRunAccuracy);

        int played = PlayerPrefs.GetInt("TypeKeep_GamesPlayedSinceInterstitial", 0) + 1;
        PlayerPrefs.SetInt("TypeKeep_GamesPlayedSinceInterstitial", played);
        PlayerPrefs.Save();
        Debug.Log($"[GameManager] Run completed. Games played since last interstitial: {played}");
    }

    /// <summary>
    /// Restores tower health to full, unmarks game over, and allows banking further earnings.
    /// </summary>
    public void ReviveFortress()
    {
        IsGameOver = false;
        earningsBanked = false;
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
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
        PlayerPrefs.Save();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            BankEarnings();
            SaveProgressIfActive();
        }
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