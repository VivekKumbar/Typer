using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
#if UNITY_WEBGL
using Playgama;
#endif

/// <summary>
/// Handles two-way synchronization between local PlayerPrefs storage and Playgama Bridge cloud storage.
/// Preloads cloud state on game launch and persists state changes seamlessly.
/// </summary>
public static class BridgeStorageSync
{
    private static readonly List<string> s_knownKeys = new List<string>
    {
        // Wallet & economy
        "TypeKeep_TotalCoins",

        // Lifetime stats & personal bests
        "Stats_EnemiesDestroyed",
        "Stats_LettersTyped",
        "Stats_CorrectLetters",
        "Stats_TotalCoinsCollected",
        "Stats_RunsPlayed",
        "Stats_HighestWave",
        "Stats_HighestCombo",
        "Stats_MostCoinsInRun",
        "Stats_BestRunAccuracy",

        // Settings & options
        "TypeKeep_SfxEnabled",
        "TypeKeep_MusicEnabled",
        "TypeKeep_VibrationEnabled",
        "DarkMode_Enabled",
        "TypeKeep_MainMenuReturnCount",

        // Shop & word packs
        "wordpacks_selected",

        // Mid-run checkpoint save
        "TypeKeep_RunSave",
        "TypeKeep_RunSave_Wave"
    };

    public static bool IsLoaded { get; private set; }
    public static event Action OnStorageLoaded;

    /// <summary>
    /// Preloads storage data from Bridge cloud storage into local PlayerPrefs cache.
    /// </summary>
    public static void Preload(Action onComplete = null)
    {
#if UNITY_WEBGL
        try
        {
            if (Bridge.storage != null)
            {
                Bridge.storage.Get(s_knownKeys, (success, dataList) =>
                {
                    if (success && dataList != null)
                    {
                        for (int i = 0; i < s_knownKeys.Count && i < dataList.Count; i++)
                        {
                            string key = s_knownKeys[i];
                            string val = dataList[i];
                            if (!string.IsNullOrEmpty(val))
                            {
                                PlayerPrefs.SetString(key, val);
                            }
                        }
                        PlayerPrefs.Save();
                    }

                    IsLoaded = true;
                    OnStorageLoaded?.Invoke();
                    onComplete?.Invoke();
                });
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BridgeStorageSync] Preload failed: {ex.Message}");
        }
#endif
        IsLoaded = true;
        OnStorageLoaded?.Invoke();
        onComplete?.Invoke();
    }

    /// <summary>
    /// Sets a string key in PlayerPrefs and syncs to Bridge storage.
    /// </summary>
    public static void SetString(string key, string value)
    {
        PlayerPrefs.SetString(key, value);
        PlayerPrefs.Save();

#if UNITY_WEBGL
        try
        {
            if (Bridge.storage != null)
            {
                Bridge.storage.Set(key, value);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BridgeStorageSync] SetString failed for {key}: {ex.Message}");
        }
#endif
    }

    /// <summary>
    /// Sets an integer key in PlayerPrefs and syncs to Bridge storage.
    /// </summary>
    public static void SetInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();

#if UNITY_WEBGL
        try
        {
            if (Bridge.storage != null)
            {
                Bridge.storage.Set(key, value);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BridgeStorageSync] SetInt failed for {key}: {ex.Message}");
        }
#endif
    }

    /// <summary>
    /// Sets a float key in PlayerPrefs and syncs to Bridge storage.
    /// </summary>
    public static void SetFloat(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();

#if UNITY_WEBGL
        try
        {
            if (Bridge.storage != null)
            {
                Bridge.storage.Set(key, value.ToString(CultureInfo.InvariantCulture));
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BridgeStorageSync] SetFloat failed for {key}: {ex.Message}");
        }
#endif
    }

    /// <summary>
    /// Deletes a key from PlayerPrefs and Bridge storage.
    /// </summary>
    public static void DeleteKey(string key)
    {
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();

#if UNITY_WEBGL
        try
        {
            if (Bridge.storage != null)
            {
                Bridge.storage.Delete(key);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BridgeStorageSync] DeleteKey failed for {key}: {ex.Message}");
        }
#endif
    }
}
