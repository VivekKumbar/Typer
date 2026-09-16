using System;
using UnityEngine;

// Tracks achievement progress and claim state, saved with PlayerPrefs.
// Same static pattern as Wallet.cs/ShopInventory.cs. Spends nothing itself --
// Claim() deposits into the persistent Wallet.
//
// Progress is never cached here: GetProgress()/GetState() read StatsManager
// live every time they're called, so an achievement is correctly
// ReadyToClaim the moment its underlying stat crosses the target -- even if
// that happened mid-run in GameScene while the Achievements panel (Main
// Menu only) wasn't open. Only the CLAIMED flag is actually persisted by
// this class; "ready" is a pure computation, not stored state.
//
// Bank must be assigned once before use (AchievementsPanel does this from
// its own Inspector-referenced AchievementBank in Awake()) -- kept as a
// plain static field rather than loaded via Resources so the data asset is
// still an ordinary Inspector reference, like WordBank/ShopCatalog elsewhere
// in this project.
public static class AchievementManager
{
    public enum AchievementState { NotReady, ReadyToClaim, Claimed }

    public static AchievementBank Bank;

    // Fired whenever an achievement is successfully claimed, so open UI can
    // refresh without polling.
    public static event Action<string> OnAchievementClaimed;

    const string ClaimedKeyPrefix = "Achievement_Claimed_";

    public static bool IsClaimed(string achievementId)
    {
        if (string.IsNullOrEmpty(achievementId)) return false;
        return PlayerPrefs.GetInt(ClaimedKeyPrefix + achievementId, 0) == 1;
    }

    // Current live progress value for a metric (not clamped to any single
    // achievement's target -- callers compare it against targetValue themselves).
    public static int GetProgress(AchievementMetricType metric)
    {
        switch (metric)
        {
            case AchievementMetricType.EnemiesKilled: return StatsManager.EnemiesDestroyed;
            case AchievementMetricType.HighestWave: return StatsManager.HighestWave;
            case AchievementMetricType.HighestCombo: return StatsManager.HighestCombo;
            case AchievementMetricType.CoinsEarnedLifetime: return StatsManager.TotalCoinsCollected;
            case AchievementMetricType.WordsTypedPerfectly: return StatsManager.WordsTypedPerfectly;
            case AchievementMetricType.RunsPlayed: return StatsManager.RunsPlayed;
            default: return 0;
        }
    }

    public static AchievementState GetState(string achievementId)
    {
        AchievementData data = Bank != null ? Bank.GetById(achievementId) : null;
        if (data == null) return AchievementState.NotReady;

        if (IsClaimed(achievementId)) return AchievementState.Claimed;
        return GetProgress(data.metricType) >= data.targetValue
            ? AchievementState.ReadyToClaim
            : AchievementState.NotReady;
    }

    // Only succeeds if the achievement is currently ReadyToClaim -- returns
    // false (no coins, no state change) if it's already Claimed or still
    // NotReady, so double-claiming or claiming early is never possible even
    // if called directly (e.g. a modified client, a stale button).
    public static bool Claim(string achievementId)
    {
        AchievementData data = Bank != null ? Bank.GetById(achievementId) : null;
        if (data == null) return false;
        if (GetState(achievementId) != AchievementState.ReadyToClaim) return false;

        PlayerPrefs.SetInt(ClaimedKeyPrefix + achievementId, 1);
        PlayerPrefs.Save();
        Wallet.Add(data.coinReward);
        OnAchievementClaimed?.Invoke(achievementId);
        return true;
    }
}
