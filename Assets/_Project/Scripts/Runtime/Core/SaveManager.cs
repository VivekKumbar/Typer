using UnityEngine;

// Mid-run save/continue, static like Wallet.cs. Separate from Wallet
// (permanent currency) and ShopInventory/WordPackSelection (permanent
// ownership + shop selection) — this ONLY holds in-progress run state.
//
// Flow:
//   - MainMenu sets IsContinuing before loading GameScene (true for Continue,
//     false for New Game).
//   - Each relevant singleton (GameManager, WaveManager, ShieldManager,
//     ComboManager, UpgradeManager) checks "IsContinuing && HasSave()" in its
//     own Awake() and restores its own slice of RunSaveData if so. HasSave()
//     is the real guard — IsContinuing alone can go stale (e.g. RestartButton
//     reloads GameScene directly without going through MainMenu), but the
//     save is always cleared on game over, so a stale flag with no save is a
//     harmless no-op.
//   - WaveManager.RunWaves() calls CaptureAndSave() at the start of every
//     wave (the safe checkpoint). GameManager also calls it when pausing to
//     the Main Menu and from OnApplicationPause/OnApplicationQuit.
//   - GameManager clears the save the moment a run ends (game over) — there's
//     nothing left to continue.
public static class SaveManager
{
    const string DataKey = "TypeKeep_RunSave";
    const string WaveKey = "TypeKeep_RunSave_Wave"; // lightweight mirror for GetSavedWave()

    // Set by MainMenu right before loading GameScene. Not persisted — a plain
    // static flag is enough since it only needs to survive one scene load.
    public static bool IsContinuing { get; set; }

    public static bool HasSave() => PlayerPrefs.HasKey(DataKey);

    // Quick accessor for the Main Menu button label — reads the small
    // mirrored int key instead of deserializing the full JSON blob.
    public static int GetSavedWave() => PlayerPrefs.GetInt(WaveKey, 1);

    public static void SaveRun(RunSaveData data)
    {
        if (data == null) return;
        BridgeStorageSync.SetString(DataKey, JsonUtility.ToJson(data));
        BridgeStorageSync.SetInt(WaveKey, data.waveNumber);
    }

    public static RunSaveData LoadRun()
    {
        if (!HasSave()) return null;
        string json = PlayerPrefs.GetString(DataKey, "");
        if (string.IsNullOrEmpty(json)) return null;
        return JsonUtility.FromJson<RunSaveData>(json);
    }

    public static void ClearSave()
    {
        BridgeStorageSync.DeleteKey(DataKey);
        BridgeStorageSync.DeleteKey(WaveKey);
    }

    // Gathers current state from every relevant singleton and writes it.
    // Safe to call repeatedly — unlike Wallet.Add, writing a save is
    // idempotent (each call just overwrites the last one), so this
    // deliberately has no "only once" guard the way GameManager.BankEarnings
    // does; callers only need to make sure they don't call it after game over
    // (GameManager.SaveProgressIfActive already checks that).
    public static void CaptureAndSave(int waveNumber)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return; // nothing to capture

        RunSaveData data = new RunSaveData
        {
            waveNumber = Mathf.Max(1, waveNumber),
            isNight = DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight,
            bigEnemyAppearances = WaveManager.Instance != null ? WaveManager.Instance.BigEnemyAppearances : 0,

            health = gm.currentHealth,
            maxHealth = gm.maxHealth,
            gmShield = gm.shield,
            abilityShield = ShieldManager.Instance != null ? ShieldManager.Instance.Current : 0,

            coins = gm.coins,
            coinsEarnedThisRun = gm.coinsEarnedThisRun,

            combo = ComboManager.Instance != null ? ComboManager.Instance.combo : 0,
            highestComboThisRun = ComboManager.Instance != null ? ComboManager.Instance.HighestComboThisRun : 0,
            overload = ComboManager.Instance != null ? ComboManager.Instance.overload : 0f,
            overloadReady = ComboManager.Instance != null && ComboManager.Instance.overloadReady,

            selectedWordPackIds = RunContext.LockedWordPackIds.ToArray(), // re-save the SAME locked packs, not the live shop selection
            groundSkinId = RunContext.LockedGroundSkinId, // re-save the SAME locked ground skin, not the live equipped one
        };

        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.CaptureLevelsInto(data);

        SaveRun(data);
    }
}
