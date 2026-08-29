using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Put this on an object in your MAIN MENU scene.
// - Hook the "NEW GAME" / "PLAY" button's OnClick to PlayGame() (unchanged
//   binding — if a save exists it now shows a confirm popup before erasing it)
// - Hook the "CONTINUE" button's OnClick to ContinueGame() (shows a "Continuing
//   from Wave X" confirm popup, then proceeds)
// - Hook a Quit button (optional) to Quit()
// Both confirm flows share ONE ConfirmPopup instance (confirmPopup below) —
// it's just Show()'n with different text/callbacks each time, not duplicated.
// It loads the game scene asynchronously and shows a loading bar.
public class MainMenu : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("Exact name of your game scene (must be added to Build Settings).")]
    public string gameSceneName = "GameScene";

    [Header("Loading UI (optional, but you asked for it)")]
    public GameObject loadingPanel;   // full-screen panel, disabled by default
    [Tooltip("Shared progress-bar piece (see LoadingBarUI) -- driven from the REAL scene-load progress, blended with Min Show Time so a fast load still visibly fills instead of flashing to 100%.")]
    public LoadingBarUI loadingBar;
    [Tooltip("Keep the loading screen visible at least this long so it doesn't flash by, even if the scene loads faster than that.")]
    public float minShowTime = 1.2f;

    public enum LoadingBackgroundVariant { A, B }

    [Header("Loading screen background — two selectable variants")]
    [Tooltip("LOADING_A_PLACEHOLDER - replace me. Shown when Loading Variant = A. Default rule: New Game uses A.")]
    public Image loadingBackgroundA;
    [Tooltip("LOADING_B_PLACEHOLDER - replace me. Shown when Loading Variant = B. Default rule: Continue uses B.")]
    public Image loadingBackgroundB;
    [Tooltip("Which background the loading screen shows THE NEXT TIME it appears. Set automatically right before LoadGame() starts (New Game -> A, Continue -> B) -- change PlayGame/ContinueGame's assignments below to wire a different rule later.")]
    public LoadingBackgroundVariant loadingVariant = LoadingBackgroundVariant.A;

    [Header("Continue / New Game")]
    [Tooltip("The whole Continue button — shown only when a save exists.")]
    public GameObject continueButtonRoot;
    [Tooltip("Label on the Continue button, set to 'CONTINUE - WAVE X'.")]
    public TMP_Text continueLabel;
    [Tooltip("Single shared popup used to confirm BOTH New Game (erase warning) and Continue (resume confirmation). Leave empty to skip confirmation entirely (not recommended) and act immediately.")]
    public ConfirmPopup confirmPopup;
    [Tooltip("The full upgrade pool — used to resolve the Continue popup's saved upgrade ids to their icon/name for the build-preview row. Assign the same UpgradePool asset UpgradeManager uses in GameScene.")]
    public UpgradePool upgradePool;
    [Tooltip("The shop catalog — used to resolve the Continue popup's saved word-pack ids AND ground-skin id to their ShopItems (icon/name/previewImage). Assign the same ShopCatalog WordBank/GroundSkinApplier use in GameScene.")]
    public ShopCatalog shopCatalog;

    [Header("Interstitial on return to Main Menu")]
    [Tooltip("Show an interstitial ad every Nth time the player returns to this scene (persisted across app sessions via PlayerPrefs, not just this session).")]
    public int showInterstitialEveryNReturns = 3;
    [Tooltip("Never show an interstitial within this many seconds of a rewarded ad closing, to avoid back-to-back ad fatigue.")]
    public float interstitialCooldownAfterRewardedSeconds = 60f;
    const string ReturnCountKey = "TypeKeep_MainMenuReturnCount";

    [Header("Watch Ad for Coins (Main Menu)")]
    [Tooltip("Flat coin reward for watching a rewarded ad from the Main Menu (separate from Game Over's run-based +50% bonus).")]
    public int watchAdCoinReward = 500;
    [Tooltip("The whole Watch Ad button root -- hidden whenever no rewarded ad is ready, so the player never taps a dead button.")]
    public GameObject watchAdButtonRoot;
    public Button watchAdButton;

    void Start()
    {
        RefreshContinueButton();
        SfxPlayer.PlayMainMenu();
        if (watchAdButton != null) watchAdButton.onClick.AddListener(OnWatchAdClicked);
        RefreshWatchAdButton();
        MaybeShowInterstitial();
    }

    void RefreshWatchAdButton()
    {
        bool ready = AdsManager.Instance != null && AdsManager.Instance.IsRewardedReady();
        if (watchAdButtonRoot != null) watchAdButtonRoot.SetActive(ready);
        else if (watchAdButton != null) watchAdButton.gameObject.SetActive(ready);
    }

    void OnWatchAdClicked()
    {
        if (AdsManager.Instance == null) return;
        AdsManager.Instance.ShowRewardedAd(
            onRewardGranted: () => Wallet.Add(watchAdCoinReward),
            onFailedOrSkipped: () => { }); // no partial reward
        RefreshWatchAdButton(); // re-check readiness now that this ad's been consumed
    }

    // Counts every Main Menu load (New Game/Continue -> GameScene -> back to
    // Main Menu all funnel back through this scene's Start) and shows an
    // interstitial every Nth return -- persisted so the count survives app
    // restarts, not just this play session.
    void MaybeShowInterstitial()
    {
        int count = PlayerPrefs.GetInt(ReturnCountKey, 0) + 1;
        PlayerPrefs.SetInt(ReturnCountKey, count);
        PlayerPrefs.Save();

        if (AdsManager.Instance == null || showInterstitialEveryNReturns <= 0) return;
        if (count % showInterstitialEveryNReturns != 0) return;
        if (!AdsManager.Instance.IsInterstitialReady()) return;

        float sinceRewarded = Time.realtimeSinceStartup - AdsManager.Instance.LastRewardedClosedRealtime;
        if (AdsManager.Instance.LastRewardedClosedRealtime >= 0f && sinceRewarded < interstitialCooldownAfterRewardedSeconds)
            return; // just watched a rewarded ad -- skip this one to avoid fatigue

        AdsManager.Instance.ShowInterstitial(onClosed: null);
    }

    void RefreshContinueButton()
    {
        bool hasSave = SaveManager.HasSave();
        if (continueButtonRoot != null) continueButtonRoot.SetActive(hasSave);
        if (hasSave && continueLabel != null)
            continueLabel.text = "CONTINUE - WAVE " + SaveManager.GetSavedWave();
    }

    // Hook the "CONTINUE" button here. Button should already be hidden/
    // disabled when no save exists (see RefreshContinueButton), but the
    // HasSave() check below is the real guard in case it's clicked anyway.
    public void ContinueGame()
    {
        if (!SaveManager.HasSave()) return;

        if (confirmPopup != null)
        {
            RunSaveData save = SaveManager.LoadRun();
            int wave = save != null ? save.waveNumber : SaveManager.GetSavedWave();
            confirmPopup.ShowWithPreview(
                "Continue Run",
                "Continuing from Wave " + wave + ".",
                BuildAbilityPreview(save),
                BuildWordPackPreview(save),
                ResolveGroundSkinBackground(save),
                ProceedWithContinue);
        }
        else
        {
            ProceedWithContinue();
        }
    }

    // Resolves RunSaveData's saved (id, level) pairs to their UpgradeDefinition
    // via upgradePool — UpgradeManager.Instance doesn't exist in this scene, so
    // the pool has to be looked up directly rather than through the runtime
    // singleton. Reads ANY unlocked upgrade generically (no hardcoded ability
    // list), so it stays correct as more upgrades are added later.
    List<(UpgradeDefinition def, int level)> BuildAbilityPreview(RunSaveData save)
    {
        var result = new List<(UpgradeDefinition, int)>();
        if (save == null || save.upgradeIds == null || upgradePool == null || upgradePool.upgrades == null)
            return result;

        for (int i = 0; i < save.upgradeIds.Length; i++)
        {
            int level = i < save.upgradeLevels.Length ? save.upgradeLevels[i] : 0;
            if (level <= 0) continue;

            UpgradeDefinition def = upgradePool.upgrades.Find(u => u != null && u.id == save.upgradeIds[i]);
            if (def != null) result.Add((def, level));
        }
        return result;
    }

    // Resolves RunSaveData's LOCKED word-pack ids (see RunContext) to their
    // ShopItem via shopCatalog — same pattern as BuildAbilityPreview
    // above (a live WordBank doesn't exist in the Main Menu scene either, so
    // the catalog has to be searched directly). Empty/no packs locked in
    // returns an empty list; ConfirmPopup shows its own "Default Words"
    // placeholder for that case.
    List<ShopItem> BuildWordPackPreview(RunSaveData save)
    {
        var result = new List<ShopItem>();
        if (save == null || save.selectedWordPackIds == null || shopCatalog == null || shopCatalog.categories == null)
            return result;

        foreach (string id in save.selectedWordPackIds)
        {
            ShopItem found = null;
            foreach (ShopCategory cat in shopCatalog.categories)
            {
                if (cat == null || cat.items == null) continue;
                found = cat.items.Find(i => i != null && i.kind == ShopItemKind.WordPack && i.id == id);
                if (found != null) break;
            }
            if (found != null) result.Add(found);
        }
        return result;
    }

    // Resolves RunSaveData's LOCKED ground-skin id (see RunContext) to its
    // ShopItem's previewImage/icon via shopCatalog -- the SAVED run's
    // ground, not whatever's currently equipped in the shop. Returns null if
    // nothing was locked in or it can't be resolved; ConfirmPopup then just
    // resets the Dialog to its default background sprite.
    Sprite ResolveGroundSkinBackground(RunSaveData save)
    {
        if (save == null || string.IsNullOrEmpty(save.groundSkinId) || shopCatalog == null || shopCatalog.categories == null)
            return null;

        foreach (ShopCategory cat in shopCatalog.categories)
        {
            if (cat == null || cat.items == null) continue;
            ShopItem item = cat.items.Find(i => i != null && i.id == save.groundSkinId);
            if (item != null) return item.previewImage != null ? item.previewImage : item.icon;
        }
        return null;
    }

    void ProceedWithContinue()
    {
        SaveManager.IsContinuing = true;
        loadingVariant = LoadingBackgroundVariant.B; // default rule: Continue -> B
        StartCoroutine(LoadGame());
    }

    // Hook the "NEW GAME" / "PLAY" button here. If a save exists, confirms
    // first (erasing it is a one-way action); if not, starts immediately —
    // no point confirming "start a new game" when there's nothing to lose.
    public void PlayGame()
    {
        if (SaveManager.HasSave() && confirmPopup != null)
        {
            int wave = SaveManager.GetSavedWave();
            confirmPopup.Show(
                "Start New Game?",
                "Your current progress at Wave " + wave + " will be lost. Are you sure you want to start a new game?",
                StartFreshGame);
        }
        else
        {
            StartFreshGame();
        }
    }

    void StartFreshGame()
    {
        SaveManager.ClearSave();
        SaveManager.IsContinuing = false;
        loadingVariant = LoadingBackgroundVariant.A; // default rule: New Game -> A
        StartCoroutine(LoadGame());
    }

    public void Quit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // stops Play mode in the editor
#endif
    }

    // Shows exactly one of the two placeholder backgrounds according to
    // loadingVariant, leaving everything else about the loading panel (the
    // progress bar, its trigger points) untouched.
    void ApplyLoadingVariant()
    {
        if (loadingBackgroundA != null) loadingBackgroundA.gameObject.SetActive(loadingVariant == LoadingBackgroundVariant.A);
        if (loadingBackgroundB != null) loadingBackgroundB.gameObject.SetActive(loadingVariant == LoadingBackgroundVariant.B);
    }

    IEnumerator LoadGame()
    {
        ApplyLoadingVariant();
        if (loadingPanel) loadingPanel.SetActive(true);
        if (loadingBar != null) loadingBar.SnapTo01(0f); // start visibly empty, no carry-over from a previous load
        float start = Time.unscaledTime;

        AsyncOperation op = SceneManager.LoadSceneAsync(gameSceneName);
        op.allowSceneActivation = false; // wait until we say go

        while (!op.isDone)
        {
            // Unity reports 0 -> 0.9 while loading, then holds at 0.9 until
            // activated -- famously in big, jumpy steps for a small scene, not
            // a smooth ramp. realProgress alone would make the bar snap.
            float realProgress = Mathf.Clamp01(op.progress / 0.9f);
            // pacedProgress ramps linearly over Min Show Time regardless of how
            // the real load is going, so a load that finishes instantly still
            // has something to visibly fill.
            float pacedProgress = Mathf.Clamp01((Time.unscaledTime - start) / minShowTime);
            // Whichever is FURTHER BEHIND wins: never claim more progress than
            // has genuinely happened (realProgress caps it), and never let a
            // fast load flash to 100% before Min Show Time's pacing catches up.
            float target = Mathf.Min(realProgress, pacedProgress);
            if (loadingBar != null) loadingBar.SetTargetProgress01(target);

            // Loaded AND minimum display time elapsed -> enter the game
            if (op.progress >= 0.9f && Time.unscaledTime - start >= minShowTime)
            {
                if (loadingBar != null) loadingBar.SetTargetProgress01(1f);
                op.allowSceneActivation = true;
            }
            yield return null;
        }
    }
}