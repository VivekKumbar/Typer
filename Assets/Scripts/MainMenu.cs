using System;
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
    [Tooltip("Flat coin reward granted when the Main Menu rewarded ad is watched to completion. Editable here -- never hardcoded.")]
    public int rewardCoins = 500;
    [Tooltip("Real-world cooldown, in hours, before the Main Menu ad can be watched again after a reward is granted. Editable here -- never hardcoded. Separate from Game Over's ad offer, which is a one-shot-per-run bonus with no cooldown at all -- these are different mechanics (repeatable flat bonus vs. one-time proportional bonus), so they intentionally do NOT share a cooldown.")]
    public float cooldownHours = 4f;
    [Tooltip("The whole Watch Ad button root -- hidden while on cooldown or while no rewarded ad is loaded.")]
    public GameObject watchAdButtonRoot;
    public Button watchAdButton;
    [Tooltip("Label on the button: shows 'WATCH AD (+N coins)' when available, or a live 'Next ad in Xh Ym' countdown during cooldown.")]
    public TMP_Text watchAdButtonLabel;

    // Stored as UTC ticks (not DateTime.ToString(), which is locale/format
    // dependent) so the cooldown is unambiguous and survives app close/
    // reopen correctly regardless of device locale or timezone changes.
    const string LastMainMenuAdUtcTicksKey = "TypeKeep_LastMainMenuAdUtcTicks";

    private bool isMainMenuAdInProgress;

    // DEBUG CONSOLE HOOK: "resetadcooldown". Static (not tied to a live
    // MainMenu instance) since the cooldown is plain PlayerPrefs state that
    // can be cleared regardless of which scene the console is currently in.
    public static void DebugResetAdCooldown()
    {
        PlayerPrefs.DeleteKey(LastMainMenuAdUtcTicksKey);
        PlayerPrefs.Save();
    }

    // DEBUG CONSOLE HOOK: "forcereward". Routes through the exact same
    // OnMainMenuAdRewardGranted() a real completed ad calls.
    public void DebugForceReward() => OnMainMenuAdRewardGranted();

    void Start()
    {
        RefreshContinueButton();
        SfxPlayer.PlayMainMenu();
        if (watchAdButton != null)
        {
            watchAdButton.onClick.RemoveListener(OnWatchAdClicked);
            watchAdButton.onClick.AddListener(OnWatchAdClicked);
        }
        RefreshWatchAdButton();
        MaybeShowInterstitial();
    }

    void OnEnable()
    {
        // Re-check elapsed time whenever this scene/object is (re)enabled --
        // e.g. the app was closed mid-cooldown and reopened -- not just via
        // the live Update() tick below, so the button is correct immediately.
        RefreshWatchAdButton();
    }

    void Update()
    {
        if (isMainMenuAdInProgress) return;
        if (Time.unscaledTime < nextAdTimerUpdate) return;
        nextAdTimerUpdate = Time.unscaledTime + 1f; // live countdown, ticked once/sec (cheap, no need for per-frame)
        RefreshWatchAdButton();
    }

    private float nextAdTimerUpdate;

    // Remaining cooldown, or TimeSpan.Zero if none is active. Single place
    // that reads the persisted timestamp, so IsWatchAdAvailable() and the
    // countdown label can never disagree with each other.
    TimeSpan MainMenuAdCooldownRemaining()
    {
        string ticksString = PlayerPrefs.GetString(LastMainMenuAdUtcTicksKey, string.Empty);
        if (string.IsNullOrEmpty(ticksString) || !long.TryParse(ticksString, out long ticks))
            return TimeSpan.Zero;

        DateTime lastClaimUtc = new DateTime(ticks, DateTimeKind.Utc);
        TimeSpan elapsed = DateTime.UtcNow - lastClaimUtc;
        TimeSpan cooldown = TimeSpan.FromHours(cooldownHours);
        return elapsed < cooldown ? cooldown - elapsed : TimeSpan.Zero;
    }

    // Single source of truth for both the button's interactable state and
    // whether tapping it should actually do anything: cooldown elapsed AND a
    // rewarded ad is genuinely loaded and ready to show. Only ever asks the
    // real AdsManager -- no other ad system is consulted or can bypass this.
    public bool IsWatchAdAvailable()
    {
        if (isMainMenuAdInProgress) return false;
        if (MainMenuAdCooldownRemaining() > TimeSpan.Zero) return false;
        return AdsManager.Instance != null && AdsManager.Instance.IsRewardedReady();
    }

    // Three button states: available, cooldown countdown, ad-in-progress.
    public void RefreshWatchAdButton()
    {
        if (isMainMenuAdInProgress)
        {
            if (watchAdButton != null) watchAdButton.interactable = false;
            return;
        }

        TimeSpan remaining = MainMenuAdCooldownRemaining();
        bool onCooldown = remaining > TimeSpan.Zero;
        bool available = IsWatchAdAvailable();

        if (watchAdButtonRoot != null) watchAdButtonRoot.SetActive(true); // stays visible so the countdown is legible
        if (watchAdButton != null) watchAdButton.interactable = available;

        if (watchAdButtonLabel != null)
        {
            if (onCooldown)
                watchAdButtonLabel.text = FormatCountdown(remaining);
            else if (available)
                watchAdButtonLabel.text = $"WATCH AD (+{rewardCoins} coins)";
            else
                watchAdButtonLabel.text = "Ad Loading..."; // cooldown elapsed but no rewarded ad loaded yet
        }
    }

    static string FormatCountdown(TimeSpan remaining)
    {
        if (remaining.TotalHours >= 1) return $"Next ad in {(int)remaining.TotalHours}h {remaining.Minutes}m";
        if (remaining.TotalMinutes >= 1) return $"Next ad in {(int)remaining.TotalMinutes}m";
        return "Next ad in <1m";
    }

    // The ONLY entry point for the Main Menu Watch Ad button. Calls directly
    // into AdsManager.ShowRewardedAd -- the single shared implementation
    // GameOverAdOffer also uses -- with no other ad system able to intercept
    // or bypass this call. (A duplicate "AdManager" class previously sat in
    // front of this and unconditionally handled the click itself, which was
    // the actual root cause of rewards being granted on early close -- that
    // bypass has been removed. AdManager.cs is now fully unused; not deleted
    // here since removing files wasn't asked for, but it should not be
    // reintroduced into this call path again.)
    void OnWatchAdClicked()
    {
        if (!IsWatchAdAvailable()) return;

        isMainMenuAdInProgress = true;
        RefreshWatchAdButton();

        // AdsManager (see its own header comment) resolves the documented
        // LevelPlay reward/close ordering race internally via a short grace
        // window -- onFailedOrSkipped here is only ever invoked once it's
        // genuinely concluded no reward is coming, never just because the
        // close event happened to arrive first.
        AdsManager.Instance.ShowRewardedAd(
            onRewardGranted: OnMainMenuAdRewardGranted,
            onFailedOrSkipped: OnMainMenuAdClosedWithoutReward);
    }

    // Ad watched to completion -- per spec, the reward is simply granted with
    // no popup ("no popup needed in this case" is literal).
    void OnMainMenuAdRewardGranted()
    {
        isMainMenuAdInProgress = false;

        Wallet.Add(rewardCoins);
        PlayerPrefs.SetString(LastMainMenuAdUtcTicksKey, DateTime.UtcNow.Ticks.ToString());
        PlayerPrefs.Save();

        RefreshWatchAdButton();
    }

    // Called once AdsManager has genuinely concluded the ad closed without a
    // reward landing (see AdsManager's grace-window comment). By this point
    // LevelPlay's native ad activity is already closed and gone -- there is
    // no "resume a closed ad" API -- so this popup is an after-the-fact
    // acknowledgement, not a live intercept: CONFIRM and CANCEL both just
    // dismiss it and return to the button, unclaimed, no cooldown started,
    // available to try again immediately. Reuses the shared ConfirmPopup
    // (same instance New Game/Continue use).
    void OnMainMenuAdClosedWithoutReward()
    {
        isMainMenuAdInProgress = false;

        if (confirmPopup != null)
        {
            confirmPopup.Show(
                "No Reward",
                "You closed the ad before it finished, so no reward was given. The ad has already ended and can't be resumed -- tap Watch Ad to try again.",
                confirmCallback: RefreshWatchAdButton,
                cancelCallback: RefreshWatchAdButton);
        }
        else
        {
            RefreshWatchAdButton();
        }
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