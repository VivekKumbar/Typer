using System;
using UnityEngine;

// Lifetime stats + personal bests, saved to disk (PlayerPrefs).
// Same pattern as Wallet.cs: static, no MonoBehaviour needed, accessible from anywhere.
public static class StatsManager
{
    const string KEY_ENEMIES_DESTROYED = "Stats_EnemiesDestroyed";
    const string KEY_LETTERS_TYPED = "Stats_LettersTyped";
    const string KEY_CORRECT_LETTERS = "Stats_CorrectLetters";
    const string KEY_TOTAL_COINS = "Stats_TotalCoinsCollected";
    const string KEY_RUNS_PLAYED = "Stats_RunsPlayed";

    const string KEY_HIGHEST_WAVE = "Stats_HighestWave";
    const string KEY_HIGHEST_COMBO = "Stats_HighestCombo";
    const string KEY_MOST_COINS_IN_RUN = "Stats_MostCoinsInRun";
    const string KEY_BEST_RUN_ACCURACY = "Stats_BestRunAccuracy";

    // Fired after every recorded stat change, so the Profile screen can refresh live.
    public static event Action OnStatsChanged;

    // ---- lifetime (cumulative) ----
    public static int EnemiesDestroyed
    {
        get { return PlayerPrefs.GetInt(KEY_ENEMIES_DESTROYED, 0); }
        private set { PlayerPrefs.SetInt(KEY_ENEMIES_DESTROYED, value); }
    }

    public static int LettersTyped
    {
        get { return PlayerPrefs.GetInt(KEY_LETTERS_TYPED, 0); }
        private set { PlayerPrefs.SetInt(KEY_LETTERS_TYPED, value); }
    }

    public static int CorrectLetters
    {
        get { return PlayerPrefs.GetInt(KEY_CORRECT_LETTERS, 0); }
        private set { PlayerPrefs.SetInt(KEY_CORRECT_LETTERS, value); }
    }

    public static float LifetimeAccuracy => LettersTyped > 0 ? (float)CorrectLetters / LettersTyped * 100f : 0f;

    public static int TotalCoinsCollected
    {
        get { return PlayerPrefs.GetInt(KEY_TOTAL_COINS, 0); }
        private set { PlayerPrefs.SetInt(KEY_TOTAL_COINS, value); }
    }

    public static int RunsPlayed
    {
        get { return PlayerPrefs.GetInt(KEY_RUNS_PLAYED, 0); }
        private set { PlayerPrefs.SetInt(KEY_RUNS_PLAYED, value); }
    }

    // ---- personal bests (only ever overwritten with a higher value) ----
    public static int HighestWave
    {
        get { return PlayerPrefs.GetInt(KEY_HIGHEST_WAVE, 0); }
        private set { PlayerPrefs.SetInt(KEY_HIGHEST_WAVE, value); }
    }

    public static int HighestCombo
    {
        get { return PlayerPrefs.GetInt(KEY_HIGHEST_COMBO, 0); }
        private set { PlayerPrefs.SetInt(KEY_HIGHEST_COMBO, value); }
    }

    public static int MostCoinsInRun
    {
        get { return PlayerPrefs.GetInt(KEY_MOST_COINS_IN_RUN, 0); }
        private set { PlayerPrefs.SetInt(KEY_MOST_COINS_IN_RUN, value); }
    }

    public static float BestRunAccuracy
    {
        get { return PlayerPrefs.GetFloat(KEY_BEST_RUN_ACCURACY, 0f); }
        private set { PlayerPrefs.SetFloat(KEY_BEST_RUN_ACCURACY, value); }
    }

    // ---- per-run tracking (not persisted — reset at the end of each run) ----
    static int runLettersTyped;
    static int runCorrectLetters;

    // This run's accuracy so far. Feed this into EndRun() when a run finishes.
    public static float CurrentRunAccuracy => runLettersTyped > 0 ? (float)runCorrectLetters / runLettersTyped * 100f : 0f;

    // ---- recording ----
    public static void RecordCorrectLetter()
    {
        LettersTyped++;
        CorrectLetters++;
        runLettersTyped++;
        runCorrectLetters++;
        Commit();
    }

    public static void RecordMissedLetter()
    {
        LettersTyped++;
        runLettersTyped++;
        Commit();
    }

    public static void RecordEnemyKilled()
    {
        EnemiesDestroyed++;
        Commit();
    }

    public static void RecordCoins(int amount)
    {
        if (amount <= 0) return;
        TotalCoinsCollected += amount;
        Commit();
    }

    // Call once per run (e.g. from GameManager.BankEarnings) with that run's results.
    // Bumps runsPlayed, updates every personal best that was beaten, then clears the
    // per-run counters so CurrentRunAccuracy starts fresh next run.
    public static void EndRun(int wave, int combo, int coinsThisRun, float runAccuracy)
    {
        RunsPlayed++;

        if (wave > HighestWave) HighestWave = wave;
        if (combo > HighestCombo) HighestCombo = combo;
        if (coinsThisRun > MostCoinsInRun) MostCoinsInRun = coinsThisRun;
        if (runAccuracy > BestRunAccuracy) BestRunAccuracy = runAccuracy;

        runLettersTyped = 0;
        runCorrectLetters = 0;

        Commit();
    }

    // Debug: wipes every stat back to zero.
    public static void ResetStats()
    {
        PlayerPrefs.DeleteKey(KEY_ENEMIES_DESTROYED);
        PlayerPrefs.DeleteKey(KEY_LETTERS_TYPED);
        PlayerPrefs.DeleteKey(KEY_CORRECT_LETTERS);
        PlayerPrefs.DeleteKey(KEY_TOTAL_COINS);
        PlayerPrefs.DeleteKey(KEY_RUNS_PLAYED);
        PlayerPrefs.DeleteKey(KEY_HIGHEST_WAVE);
        PlayerPrefs.DeleteKey(KEY_HIGHEST_COMBO);
        PlayerPrefs.DeleteKey(KEY_MOST_COINS_IN_RUN);
        PlayerPrefs.DeleteKey(KEY_BEST_RUN_ACCURACY);
        runLettersTyped = 0;
        runCorrectLetters = 0;
        Commit();
    }

    static void Commit()
    {
        PlayerPrefs.Save();
        OnStatsChanged?.Invoke();
    }
}
