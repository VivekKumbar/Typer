using System;
using System.Collections.Generic;
using UnityEngine;

// Tracks the typing combo (multiplies coins), and an Overload meter that fills
// as you type. When full, TriggerOverload() clears every enemy on screen.
public class ComboManager : MonoBehaviour
{
    public static ComboManager Instance { get; private set; }

    [Header("Combo")]
    public int combo;
    [Tooltip("Every this-many combo adds +1 to the coin multiplier.")]
    public int comboPerMultiplierStep = 10;
    public float maxMultiplier = 5f;

    // Highest combo reached this run (a fresh instance each time GameScene loads,
    // so this naturally starts at 0 per run — no manual reset needed).
    public int HighestComboThisRun { get; private set; }

    // True if the word currently locked in TypingController hasn't had a wrong
    // keystroke yet. Reset via NotifyNewTarget() when a new target locks, cleared
    // on a miss. Enemy.Die reads this to show the PERFECT! popup.
    public bool CurrentWordPerfect { get; private set; } = true;

    [Header("Overload")]
    [Tooltip("Correct letters needed to fill the Overload meter.")]
    public float overloadMax = 30f;
    public float overload;
    public bool overloadReady;

    [Header("Upgrade: Overload (optional)")]
    [Tooltip("If assigned, each level multiplies Overload Max down (charges faster) by this fraction (e.g. 0.9 = 10% faster at level 1). Boss (6): also grants Boss Value bonus coins per enemy cleared, on top of their normal reward.")]
    public UpgradeDefinition overloadUpgrade;

    [Header("Upgrade: Greed (optional)")]
    [Tooltip("If assigned, each level adds this many PERCENT to every coin reward (e.g. 10 = +10%), stacking with the combo multiplier. Boss (6) uses Boss Value the same way for a big bonus — but see Greed Boss Speed Bump on WaveManager, a spicy small risk.")]
    public UpgradeDefinition greedUpgrade;

    [Header("Upgrade: Muscle Memory (optional)")]
    [Tooltip("If assigned, each level ignores this many typos per wave (rounded) before the combo actually breaks — resets every wave. Boss (6): typos never break combo for one full wave, but only on NIGHT waves (\"per night\").")]
    public UpgradeDefinition muscleMemoryUpgrade;

    private int forgivenessRemaining;
    private bool bossForgivenessActiveThisWave;

    // UI hooks
    public event Action<int, float> OnComboChanged;   // (combo, multiplier)
    public event Action<float> OnOverloadChanged;     // fill 0..1
    public event Action OnOverloadReady;

    public static float Multiplier => Instance ? Instance.CurrentMultiplier() * Instance.GreedMultiplier() : 1f;

    // Overload's fill fraction (0..1) against its EFFECTIVE max — used by
    // ComboHUD to sync its bar immediately on Start instead of assuming 0,
    // so a restored overload charge is visible right away.
    public float OverloadFill => EffectiveOverloadMax() > 0f ? overload / EffectiveOverloadMax() : 0f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Combo/overload restore is nice-to-have, not required — if this is
        // skipped or defaults to 0 the run is still perfectly playable. Raw
        // restore, same reasoning as ShieldManager: don't clamp against
        // EffectiveOverloadMax() here since UpgradeManager may not have
        // restored its levels yet (Awake order isn't guaranteed).
        if (SaveManager.IsContinuing && SaveManager.HasSave())
        {
            RunSaveData save = SaveManager.LoadRun();
            combo = Mathf.Max(0, save.combo);
            HighestComboThisRun = Mathf.Max(HighestComboThisRun, Mathf.Max(0, save.highestComboThisRun));
            overload = Mathf.Max(0f, save.overload);
            overloadReady = save.overloadReady;
        }
    }

    float CurrentMultiplier()
    {
        float m = 1f + Mathf.Floor((float)combo / Mathf.Max(1, comboPerMultiplierStep));
        return Mathf.Min(m, maxMultiplier);
    }

    float GreedMultiplier()
    {
        if (UpgradeManager.Instance == null || greedUpgrade == null) return 1f;
        int level = UpgradeManager.Instance.LevelOf(greedUpgrade);
        if (level <= 0) return 1f;
        return 1f + greedUpgrade.ValueForLevel(level) / 100f;
    }

    float EffectiveOverloadMax()
    {
        if (UpgradeManager.Instance == null || overloadUpgrade == null) return overloadMax;
        int level = UpgradeManager.Instance.LevelOf(overloadUpgrade);
        if (level <= 0) return overloadMax;
        float mult = overloadUpgrade.ValueForLevel(level);
        return Mathf.Max(1f, overloadMax * mult);
    }

    // Called by WaveManager at the start of each wave.
    public void NotifyWaveStart(bool isNightWave)
    {
        bool suppressed = DayNightCycle.Instance != null && DayNightCycle.Instance.CurrentProfile.stricterCombo;
        int level = (UpgradeManager.Instance != null && muscleMemoryUpgrade != null) ? UpgradeManager.Instance.LevelOf(muscleMemoryUpgrade) : 0;

        forgivenessRemaining = (!suppressed && level > 0 && level < UpgradeDefinition.BossLevel)
            ? Mathf.RoundToInt(muscleMemoryUpgrade.ValueForLevel(level)) : 0;
        bossForgivenessActiveThisWave = !suppressed && level >= UpgradeDefinition.BossLevel && isNightWave;
    }

    // Call when TypingController locks onto a fresh enemy — starts a clean
    // "no mistakes yet" slate for the word that's now being typed.
    public void NotifyNewTarget()
    {
        CurrentWordPerfect = true;
    }

    public void RegisterHit()
    {
        float multiplierBefore = CurrentMultiplier();
        combo++;
        if (combo > HighestComboThisRun) HighestComboThisRun = combo;
        OnComboChanged?.Invoke(combo, CurrentMultiplier());

        // Popup only on an actual multiplier step-up (every comboPerMultiplierStep
        // combo) — not on every hit, so it doesn't clutter the screen.
        if (CurrentMultiplier() > multiplierBefore && TypingController.Instance != null && TypingController.Instance.CurrentTarget != null)
            PopupManager.ShowCombo(TypingController.Instance.CurrentTarget.transform.position, combo);

        if (!overloadReady)
        {
            float effectiveMax = EffectiveOverloadMax();
            overload += 1f;
            if (overload >= effectiveMax)
            {
                overload = effectiveMax;
                overloadReady = true;
                OnOverloadReady?.Invoke();
            }
            OnOverloadChanged?.Invoke(overload / effectiveMax);
        }
    }

    public void RegisterMiss()
    {
        CurrentWordPerfect = false;

        if (bossForgivenessActiveThisWave) return; // typos never break combo this (night) wave

        if (forgivenessRemaining > 0)
        {
            forgivenessRemaining--;
            return; // typo forgiven — combo survives
        }

        if (combo == 0) return;
        combo = 0;
        OnComboChanged?.Invoke(combo, CurrentMultiplier());
        // Overload is NOT drained on a miss � only the combo resets.
    }

    public void TriggerOverload()
    {
        if (!overloadReady) return;

        // Clear every active enemy (full death juice + coins via Defeat)
        List<Enemy> snapshot = new List<Enemy>(Enemy.Active);
        int cleared = 0;
        foreach (Enemy e in snapshot)
            if (e != null && !e.IsDefeated) { e.Defeat(); cleared++; }

        // Boss (6): bonus coins for every enemy cleared, on top of their normal reward.
        if (overloadUpgrade != null && UpgradeManager.Instance != null
            && UpgradeManager.Instance.LevelOf(overloadUpgrade) >= UpgradeDefinition.BossLevel
            && cleared > 0 && GameManager.Instance != null)
        {
            GameManager.Instance.AddCoins(Mathf.RoundToInt(overloadUpgrade.bossValue) * cleared);
        }

        overload = 0f;
        overloadReady = false;
        OnOverloadChanged?.Invoke(0f);
        CameraShake.Shake(0.4f, 0.6f);
    }
}
