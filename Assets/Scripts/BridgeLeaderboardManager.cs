using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_WEBGL
using Playgama;
using Playgama.Modules.Leaderboards;
#endif

/// <summary>
/// Manages player score submissions and leaderboard popups via Playgama Bridge SDK.
/// </summary>
public static class BridgeLeaderboardManager
{
    public const string DefaultWaveLeaderboard = "highest_wave";
    public const string DefaultCoinsLeaderboard = "total_coins";
    public const string DefaultComboLeaderboard = "highest_combo";

    /// <summary>
    /// Submits player scores from the completed run to the relevant leaderboards.
    /// </summary>
    public static void SubmitRunStats(int wave, int totalCoins, int maxCombo)
    {
        if (wave > 0)
        {
            SubmitScore(DefaultWaveLeaderboard, wave);
        }
        if (totalCoins > 0)
        {
            SubmitScore(DefaultCoinsLeaderboard, totalCoins);
        }
        if (maxCombo > 0)
        {
            SubmitScore(DefaultComboLeaderboard, maxCombo);
        }
    }

    /// <summary>
    /// Submits a specific score to a leaderboard.
    /// </summary>
    public static void SubmitScore(string leaderboardId, int score, Action<bool> callback = null)
    {
#if UNITY_WEBGL
        try
        {
            if (Bridge.leaderboards != null)
            {
                Bridge.leaderboards.SetScore(leaderboardId, score, success =>
                {
                    Debug.Log($"[BridgeLeaderboard] SetScore for '{leaderboardId}' ({score}) -> success: {success}");
                    callback?.Invoke(success);
                });
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BridgeLeaderboard] Failed to submit score to {leaderboardId}: {ex.Message}");
        }
#endif
        callback?.Invoke(false);
    }

    /// <summary>
    /// Requests the host platform to display its native leaderboard UI.
    /// </summary>
    public static void ShowLeaderboardPopup(string leaderboardId = DefaultWaveLeaderboard, Action<bool> callback = null)
    {
#if UNITY_WEBGL
        try
        {
            if (Bridge.leaderboards != null)
            {
                Bridge.leaderboards.ShowNativePopup(leaderboardId, callback);
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BridgeLeaderboard] Failed to show native popup: {ex.Message}");
        }
#endif
        callback?.Invoke(false);
    }
}
