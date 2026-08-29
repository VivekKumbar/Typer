#if LEVELPLAY_ENABLED
using Unity.Services.LevelPlay;
#endif
using System;
using UnityEngine;

// Singleton wrapper around Unity LevelPlay (Ads Mediation). Every LevelPlay-
// specific type/call lives in THIS file only -- nothing else in the project
// references LevelPlay directly, so if the SDK/API changes again later only
// this file needs updating.
//
// ============================================================================
// READ THIS FIRST
// ============================================================================
// Written against the CURRENT (2025/2026) LevelPlay Unity SDK -- the ad-unit-
// ID-based `Unity.Services.LevelPlay` API (LevelPlayRewardedAd /
// LevelPlayInterstitialAd / LevelPlay.Init), NOT the deprecated static
// `IronSource.Agent...` waterfall API.
//
// At the time this was written, the "Ads Mediation" package was checked
// directly against this project's Package Manager and was NOT installed (52
// packages resolved, none ads-related) -- despite that being the stated
// prerequisite. That means this could not be compiled/verified against the
// real SDK types. To avoid breaking the rest of the project's compilation in
// the meantime, the real implementation below is gated behind a custom
// scripting define symbol, LEVELPLAY_ENABLED, with a safe no-op fallback
// beneath it (ads simply report "not ready" everywhere, same as
// adsEnabled = false).
//
// Once you've installed the Ads Mediation package via Package Manager:
//   1. Edit > Project Settings > Player > Other Settings > Scripting Define
//      Symbols (per build target you care about) -- add LEVELPLAY_ENABLED.
//   2. Let Unity recompile. If the real installed SDK's API surface differs
//      even slightly from what's written below (method/event names), you'll
//      get compile errors ONLY inside the `#if LEVELPLAY_ENABLED` block --
//      send me those errors and this file gets trued up in a few minutes.
// ============================================================================
[DisallowMultipleComponent]
public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance { get; private set; }

    [Header("LevelPlay credentials (paste from the Unity Dashboard)")]
    [Tooltip("Your app's LevelPlay App Key.")]
    public string appKey = "";
    [Tooltip("Ad Unit ID for Rewarded ads.")]
    public string rewardedAdUnitId = "";
    [Tooltip("Ad Unit ID for Interstitial ads.")]
    public string interstitialAdUnitId = "";

    [Header("Master control")]
    [Tooltip("Uncheck to disable ads entirely -- both Show methods immediately no-op and invoke their failure/closed callback, so calling code never needs to special-case \"ads disabled\".")]
    public bool adsEnabled = true;
    [Tooltip("Enable while developing so LevelPlay serves test/house ads instead of burning real inventory. MUST be off before publishing.")]
    public bool testMode = false;

    // How long ago (Time.realtimeSinceStartup) a rewarded ad last closed --
    // MainMenu's interstitial-on-return logic reads this for its cooldown.
    // realtime, not PlayerPrefs: this is a same-session-only "just watched an
    // ad" check, not something that should survive an app restart.
    public float LastRewardedClosedRealtime { get; private set; } = -1f;

    private bool initialized;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // Unlike most managers in this project (fresh per-scene instances),
        // ads genuinely need to survive MainMenu <-> GameScene loads: the SDK
        // inits once and preloaded ads carry over, rather than re-initializing
        // (and re-buying a fresh waterfall) every scene transition.
        DontDestroyOnLoad(gameObject);
    }

    // Start, not OnEnable, per project convention -- but the ACTUAL SDK init
    // only fires immediately here if consent was already resolved on a
    // previous launch. On a genuinely first launch, ConsentPopup calls
    // InitializeWithConsent() itself once the player answers Accept/Manage --
    // init must never happen before consent is known.
    void Start()
    {
        if (!adsEnabled) return;
        if (ConsentPopup.HasShownBefore)
            InitializeWithConsent(ConsentPopup.ConsentGranted);
        // else: ConsentPopup (in this same scene) will call
        // InitializeWithConsent() once the player responds to the popup.
    }

#if LEVELPLAY_ENABLED
    private LevelPlayRewardedAd rewardedAd;
    private LevelPlayInterstitialAd interstitialAd;

    private Action pendingRewardGranted;
    private Action pendingRewardFailedOrSkipped;
    private bool rewardConsumedForCurrentAd;
    private Action pendingInterstitialClosed;

    public void InitializeWithConsent(bool consentGranted)
    {
        if (!adsEnabled || initialized) return;
        initialized = true;

        // Consent before Init, per LevelPlay's own consent-setting guidance --
        // ensures the very first init already reflects the player's choice.
        SetConsent(consentGranted);

        if (testMode)
            LevelPlay.SetMetaData("is_test_suite", "enable");

        LevelPlay.OnInitSuccess += OnInitSuccess;
        LevelPlay.OnInitFailed += OnInitFailed;
        LevelPlay.Init(appKey);
    }

    void OnInitSuccess(LevelPlayConfiguration config)
    {
        Debug.Log("[AdsManager] LevelPlay init succeeded.");
        SetupRewarded();
        SetupInterstitial();
        rewardedAd.LoadAd();
        interstitialAd.LoadAd();
    }

    void OnInitFailed(LevelPlayInitError error)
    {
        Debug.LogWarning("[AdsManager] LevelPlay init failed: " + error);
    }

    void SetupRewarded()
    {
        rewardedAd = new LevelPlayRewardedAd(rewardedAdUnitId);
        // Reward-timing gotcha (per LevelPlay's own docs): OnAdRewarded can
        // fire AFTER OnAdClosed. The two handlers below are independent and
        // idempotent -- whichever fires, in whichever order, the reward only
        // ever actually grants once (rewardConsumedForCurrentAd guards it),
        // and "ad is done, reload the next one" bookkeeping happens on close
        // regardless of whether the reward already landed.
        rewardedAd.OnAdRewarded += (adInfo, reward) => HandleRewarded();
        rewardedAd.OnAdClosed += _ => HandleRewardedClosed();
        rewardedAd.OnAdDisplayFailed += (_, __) => HandleRewardedClosed();
    }

    void SetupInterstitial()
    {
        interstitialAd = new LevelPlayInterstitialAd(interstitialAdUnitId);
        interstitialAd.OnAdClosed += _ => HandleInterstitialClosed();
        interstitialAd.OnAdDisplayFailed += (_, __) => HandleInterstitialClosed();
    }

    void HandleRewarded()
    {
        if (rewardConsumedForCurrentAd) return;
        rewardConsumedForCurrentAd = true;
        Action cb = pendingRewardGranted;
        pendingRewardGranted = null;
        pendingRewardFailedOrSkipped = null;
        cb?.Invoke();
    }

    void HandleRewardedClosed()
    {
        LastRewardedClosedRealtime = Time.realtimeSinceStartup;

        // Reward never landed (skipped/failed before OnAdRewarded fired) --
        // the failure callback is still owed.
        if (!rewardConsumedForCurrentAd)
        {
            Action cb = pendingRewardFailedOrSkipped;
            pendingRewardGranted = null;
            pendingRewardFailedOrSkipped = null;
            cb?.Invoke();
        }
        rewardConsumedForCurrentAd = false;
        rewardedAd.LoadAd(); // preload the next one immediately
    }

    void HandleInterstitialClosed()
    {
        Action cb = pendingInterstitialClosed;
        pendingInterstitialClosed = null;
        cb?.Invoke();
        interstitialAd.LoadAd(); // preload the next one immediately
    }

    public bool IsRewardedReady() => adsEnabled && rewardedAd != null && rewardedAd.IsAdReady();

    public void ShowRewardedAd(Action onRewardGranted, Action onFailedOrSkipped)
    {
        if (!adsEnabled || rewardedAd == null || !rewardedAd.IsAdReady())
        {
            onFailedOrSkipped?.Invoke();
            return;
        }
        pendingRewardGranted = onRewardGranted;
        pendingRewardFailedOrSkipped = onFailedOrSkipped;
        rewardConsumedForCurrentAd = false;
        rewardedAd.ShowAd();
    }

    public bool IsInterstitialReady() => adsEnabled && interstitialAd != null && interstitialAd.IsAdReady();

    public void ShowInterstitial(Action onClosed)
    {
        if (!adsEnabled || interstitialAd == null || !interstitialAd.IsAdReady())
        {
            onClosed?.Invoke();
            return;
        }
        pendingInterstitialClosed = onClosed;
        interstitialAd.ShowAd();
    }

    public void SetConsent(bool granted) => LevelPlay.SetConsent(granted);

#else
    // Real implementation is gated behind LEVELPLAY_ENABLED -- see the header
    // comment. This safe no-op keeps the rest of the project compiling and
    // running, with ads simply never "ready", until the SDK is wired in.
    public void InitializeWithConsent(bool consentGranted)
    {
        if (adsEnabled)
            Debug.LogWarning("[AdsManager] LEVELPLAY_ENABLED is not defined (Ads Mediation package not installed / define symbol not set) -- ads are stubbed out. See AdsManager.cs's header comment for setup steps.");
    }

    public bool IsRewardedReady() => false;
    public void ShowRewardedAd(Action onRewardGranted, Action onFailedOrSkipped) => onFailedOrSkipped?.Invoke();
    public bool IsInterstitialReady() => false;
    public void ShowInterstitial(Action onClosed) => onClosed?.Invoke();
    public void SetConsent(bool granted) { }
#endif
}
