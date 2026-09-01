#if LEVELPLAY_ENABLED
using Unity.Services.LevelPlay;
#endif
using System;
using System.Collections;
using UnityEngine;

// Singleton wrapper around Unity LevelPlay (Ads Mediation). Every LevelPlay-
// specific type/call lives in THIS file only -- nothing else in the project
// references LevelPlay directly, so if the SDK/API changes again later only
// this file needs updating.
//
// ============================================================================
// RESTORED -- read this if you're wondering why this file looks different
// from a recent version you might have seen.
// ============================================================================
// This file was, for a period, replaced with a thin bridge to a separate
// AdManager.cs, whose Editor path unconditionally waits 1.5s and then reports
// "ad completed" -- it never calls the real LevelPlay SDK at all, in the
// Editor OR in a device build (the non-Editor branch hardcodes COMPLETED with
// no SDK call either). That made it impossible to genuinely distinguish a
// fully-watched ad from a skipped one, and would have granted rewards
// unconditionally on a real device.
//
// This version restores the real, verified LevelPlay integration (checked
// line-by-line against the actual installed package sources at
// Library/PackageCache/com.unity.services.levelplay@.../Runtime/Api/) and
// keeps the exact same public API (ShowRewardedAd/IsRewardedReady/
// ShowInterstitial/IsInterstitialReady/InitializeWithConsent/SetConsent/
// LastRewardedClosedRealtime/adsEnabled), so every existing caller --
// GameOverAdOffer, AdRewardManager, MainMenu -- keeps compiling and now
// transparently gets real ad behavior instead of the simulated stub.
//
// AdManager.cs itself is left untouched (nothing else in the project calls it
// directly -- verified via a project-wide search), so it's now orphaned/
// unused dead code. Not deleted here since removing files wasn't asked for;
// flagged so it can be cleaned up in a dedicated pass if wanted.
//
// Still gated behind LEVELPLAY_ENABLED (defined in Project Settings > Player
// > Other Settings > Scripting Define Symbols > Android) so the project stays
// safe to compile even if the package is ever removed -- the stub in the
// #else branch below takes over automatically with zero code changes needed
// elsewhere.
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

    [Header("Reward-event grace window")]
    [Tooltip("LevelPlay's ad-closed and ad-rewarded events are asynchronous and can arrive in EITHER order. After the ad closes without a reward having landed yet, wait this many seconds before concluding the player skipped/closed early -- covers the case where the reward event is still in flight and arrives moments after close. Only tune this down for testing; too short risks false 'no reward' reports on a real completed ad.")]
    public float rewardGraceWindowSeconds = 0.5f;

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
    private Coroutine graceWindowCo;
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
        // fire AFTER OnAdClosed. The two handlers below are independent --
        // whichever fires, in whichever order, the reward only ever actually
        // grants once (rewardConsumedForCurrentAd guards it), and the "no
        // reward" callback is only ever fired after a short grace window past
        // close, in case the reward event is still in flight (see
        // HandleRewardedClosed below).
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
        // If a grace-window wait from an earlier close is still running,
        // this flag flip is enough to make it back off without firing the
        // "no reward" callback -- see WaitForLateRewardThenReportSkipped.
        Action cb = pendingRewardGranted;
        pendingRewardGranted = null;
        pendingRewardFailedOrSkipped = null;
        cb?.Invoke();
    }

    // Called on OnAdClosed AND OnAdDisplayFailed. Does NOT immediately decide
    // "no reward" -- LevelPlay's ad-closed and ad-rewarded events can arrive
    // in either order, so a reward that's about to land would otherwise get
    // misreported as a skip. Instead, wait rewardGraceWindowSeconds for
    // HandleRewarded() to possibly still fire before concluding the player
    // closed/skipped without watching.
    void HandleRewardedClosed()
    {
        LastRewardedClosedRealtime = Time.realtimeSinceStartup;
        rewardedAd.LoadAd(); // preload the next one immediately, independent of the grace wait below

        if (rewardConsumedForCurrentAd) return; // already handled by HandleRewarded

        if (graceWindowCo != null) StopCoroutine(graceWindowCo);
        graceWindowCo = StartCoroutine(WaitForLateRewardThenReportSkipped());
    }

    IEnumerator WaitForLateRewardThenReportSkipped()
    {
        float t = 0f;
        while (t < rewardGraceWindowSeconds)
        {
            if (rewardConsumedForCurrentAd) yield break; // reward arrived late -- HandleRewarded already invoked the granted callback
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!rewardConsumedForCurrentAd)
        {
            Action cb = pendingRewardFailedOrSkipped;
            pendingRewardGranted = null;
            pendingRewardFailedOrSkipped = null;
            cb?.Invoke();
        }
        graceWindowCo = null;
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

        if (graceWindowCo != null) { StopCoroutine(graceWindowCo); graceWindowCo = null; }
        pendingRewardGranted = onRewardGranted;
        pendingRewardFailedOrSkipped = onFailedOrSkipped;
        rewardConsumedForCurrentAd = false;

#if UNITY_EDITOR
        // CONFIRMED PACKAGE BUG (Library/PackageCache/com.unity.services.
        // levelplay/.../Editor/EditorAds/Scripts/RewardedPrefab.cs, part of
        // the installed LevelPlay package, not project code): its HideAd()
        // fires OnAdRewarded UNCONDITIONALLY, whether triggered by tapping
        // the mock ad's own "Close" button OR by its 5s countdown expiring
        // naturally -- both paths call the same HideAd(). So LevelPlay's own
        // Editor mock cannot distinguish "closed early" from "watched fully"
        // at all: every close reports as rewarded. That's the actual root
        // cause of rewards being granted on early close during Editor
        // testing. We can't patch a package under Library/PackageCache (it's
        // regenerated, not project code), so in the Editor only, this real
        // chooser (see OnGUI below) replaces the call to rewardedAd.ShowAd()
        // -- it drives the exact same HandleRewarded()/HandleRewardedClosed()
        // methods production code uses, just triggered by an honest choice
        // instead of LevelPlay's broken mock. On a real device, real ad
        // networks correctly distinguish the two and this workaround isn't
        // used at all -- rewardedAd.ShowAd() runs as normal below.
        showEditorRewardedChooser = true;
#else
        rewardedAd.ShowAd();
#endif
    }

#if UNITY_EDITOR
    private bool showEditorRewardedChooser;

    void OnGUI()
    {
        if (!showEditorRewardedChooser) return;
        GUI.depth = 0;
        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);

        float w = Mathf.Min(560f, Screen.width - 40f);
        float h = 240f;
        float x = (Screen.width - w) * 0.5f;
        float y = (Screen.height - h) * 0.5f;
        GUI.Box(new Rect(x, y, w, h), GUIContent.none);

        GUILayout.BeginArea(new Rect(x + 20, y + 20, w - 40, h - 40));
        GUILayout.Label("EDITOR REWARDED AD SIMULATOR", new GUIStyle(GUI.skin.label)
        { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
        GUILayout.Space(6);
        GUILayout.Label("LevelPlay's own Editor mock ad can't tell 'closed early' from 'watched fully' (its Close button and countdown both grant the mock reward) -- this simulator replaces it so both paths test correctly.",
            new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, alignment = TextAnchor.MiddleCenter });
        GUILayout.Space(14);

        GUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
        if (GUILayout.Button("WATCH FULLY\n(grants reward)", GUILayout.Height(56)))
        {
            showEditorRewardedChooser = false;
            HandleRewarded();
            HandleRewardedClosed();
        }
        GUILayout.Space(10);
        GUI.backgroundColor = new Color(0.85f, 0.25f, 0.25f);
        if (GUILayout.Button("CLOSE EARLY\n(no reward)", GUILayout.Height(56)))
        {
            showEditorRewardedChooser = false;
            HandleRewardedClosed();
        }
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }
#endif

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

    // LevelPlay.SetConsent(bool) is obsolete as of the installed package
    // version -- LevelPlayPrivacySettings.SetGDPRConsent() is the current API.
    public void SetConsent(bool granted) => LevelPlayPrivacySettings.SetGDPRConsent(granted);

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
