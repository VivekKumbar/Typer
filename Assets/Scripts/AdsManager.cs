using System;
using System.Collections;
using UnityEngine;
#if UNITY_WEBGL
using Playgama;
using Playgama.Modules.Advertisement;
#elif LEVELPLAY_ENABLED
using Unity.Services.LevelPlay;
#endif

/// <summary>
/// Universal AdsManager supporting Playgama Bridge SDK (WebGL) and Unity LevelPlay (Mobile).
/// All ad functionality across the game routes through this singleton.
/// </summary>
[DisallowMultipleComponent]
public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance
    {
        get
        {
            if (s_instance == null)
            {
                s_instance = FindAnyObjectByType<AdsManager>();
                if (s_instance == null)
                {
                    var go = new GameObject("[AdsManager]");
                    s_instance = go.AddComponent<AdsManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return s_instance;
        }
        private set { s_instance = value; }
    }
    private static AdsManager s_instance;

#if !UNITY_WEBGL && LEVELPLAY_ENABLED
    [Header("LevelPlay credentials (paste from the Unity Dashboard)")]
    [Tooltip("Your app's LevelPlay App Key.")]
    public string appKey = "";
    [Tooltip("Ad Unit ID for Rewarded ads.")]
    public string rewardedAdUnitId = "";
    [Tooltip("Ad Unit ID for Interstitial ads.")]
    public string interstitialAdUnitId = "";
#endif

    [Header("Master control")]
    [Tooltip("Uncheck to disable ads entirely -- both Show methods immediately no-op and invoke their failure/closed callback.")]
    public bool adsEnabled = true;
    [Tooltip("Enable while developing test mode ads.")]
    public bool testMode = false;

    public float LastRewardedClosedRealtime { get; private set; } = -1f;

    private bool initialized;
    private Action pendingRewardGranted;
    private Action pendingRewardFailedOrSkipped;
    private bool rewardConsumedForCurrentAd;
    private Action pendingInterstitialClosed;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

#if UNITY_WEBGL
        SetupBridgeAds();
#endif
    }

    void Start()
    {
        if (!adsEnabled) return;

#if UNITY_WEBGL
        // Bridge SDK initializes automatically on WebGL load
        initialized = true;
#else
        if (ConsentPopup.HasShownBefore)
            InitializeWithConsent(ConsentPopup.ConsentGranted);
#endif
    }

#if UNITY_WEBGL
    private void SetupBridgeAds()
    {
        try
        {
            if (Bridge.advertisement != null)
            {
                Bridge.advertisement.rewardedStateChanged += OnBridgeRewardedStateChanged;
                Bridge.advertisement.interstitialStateChanged += OnBridgeInterstitialStateChanged;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AdsManager] Failed to hook Bridge advertisement events: {ex.Message}");
        }
    }

    private void OnBridgeRewardedStateChanged(RewardedState state)
    {
        switch (state)
        {
            case RewardedState.Rewarded:
                HandleRewarded();
                break;
            case RewardedState.Closed:
                HandleRewardedClosed();
                break;
            case RewardedState.Failed:
                HandleRewardedClosed();
                break;
        }
    }

    private void OnBridgeInterstitialStateChanged(InterstitialState state)
    {
        switch (state)
        {
            case InterstitialState.Closed:
            case InterstitialState.Failed:
                HandleInterstitialClosed();
                break;
        }
    }
#endif

#if !UNITY_WEBGL && LEVELPLAY_ENABLED
    private LevelPlayRewardedAd rewardedAd;
    private LevelPlayInterstitialAd interstitialAd;

    public void InitializeWithConsent(bool consentGranted)
    {
        if (!adsEnabled || initialized) return;
        initialized = true;

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
#elif !UNITY_WEBGL
    public void InitializeWithConsent(bool consentGranted)
    {
        if (adsEnabled)
            Debug.LogWarning("[AdsManager] Running on non-WebGL platform without LEVELPLAY_ENABLED define.");
    }
#endif

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

#if UNITY_EDITOR
        if (editorCountdownCoroutine != null)
        {
            StopCoroutine(editorCountdownCoroutine);
            editorCountdownCoroutine = null;
        }
#endif

        if (rewardConsumedForCurrentAd)
        {
            rewardConsumedForCurrentAd = false;
            pendingRewardGranted = null;
            pendingRewardFailedOrSkipped = null;
        }
        else
        {
            StartCoroutine(DeferredRewardedClosedCheck());
        }

#if !UNITY_WEBGL && LEVELPLAY_ENABLED
        rewardedAd?.LoadAd();
#endif
    }

    private IEnumerator DeferredRewardedClosedCheck()
    {
        // Wait 1 frame in case OnAdRewarded fires immediately after OnAdClosed in LevelPlay mock
        yield return null;

        if (!rewardConsumedForCurrentAd && pendingRewardFailedOrSkipped != null)
        {
            Action cb = pendingRewardFailedOrSkipped;
            pendingRewardGranted = null;
            pendingRewardFailedOrSkipped = null;
            cb?.Invoke();
        }
        rewardConsumedForCurrentAd = false;
    }

#if UNITY_EDITOR && LEVELPLAY_ENABLED
    private Coroutine editorCountdownCoroutine;

    private IEnumerator DriveLevelPlayEditorCountdown()
    {
        // Unity LevelPlay's mock RewardedPrefab uses WaitForSeconds(1f) which freezes if Time.timeScale == 0 (e.g. at Game Over).
        // This coroutine counts down using unscaled realtime and triggers HideAd when reaching 0.
        float remaining = 5f;
        while (remaining > 0f)
        {
            yield return new WaitForSecondsRealtime(1f);
            remaining -= 1f;

            var monos = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            foreach (var m in monos)
            {
                if (m != null && m.GetType().Name == "RewardedPrefab")
                {
                    var field = m.GetType().GetField("m_CountdownTime", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (field != null) field.SetValue(m, remaining);
                }
            }
        }

        // Grant reward to player
        HandleRewarded();

        var prefabs = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
        foreach (var m in prefabs)
        {
            if (m != null && m.GetType().Name == "RewardedPrefab")
            {
                var method = m.GetType().GetMethod("HideAd", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null) method.Invoke(m, null);
            }
        }
        editorCountdownCoroutine = null;
    }
#endif

    void HandleInterstitialClosed()
    {
        Action cb = pendingInterstitialClosed;
        pendingInterstitialClosed = null;
        cb?.Invoke();

#if !UNITY_WEBGL && LEVELPLAY_ENABLED
        interstitialAd?.LoadAd();
#endif
    }

    public bool IsRewardedReady()
    {
        if (!adsEnabled) return false;
#if UNITY_EDITOR
        return true;
#elif UNITY_WEBGL
        return Bridge.advertisement != null && Bridge.advertisement.isRewardedSupported;
#elif LEVELPLAY_ENABLED
        return rewardedAd != null && rewardedAd.IsAdReady();
#else
        return false;
#endif
    }

    public void ShowRewardedAd(Action onRewardGranted, Action onFailedOrSkipped, string placement = null)
    {
        if (!adsEnabled)
        {
            onFailedOrSkipped?.Invoke();
            return;
        }

#if UNITY_WEBGL
        if (Bridge.advertisement == null || !Bridge.advertisement.isRewardedSupported)
        {
            onFailedOrSkipped?.Invoke();
            return;
        }
        pendingRewardGranted = onRewardGranted;
        pendingRewardFailedOrSkipped = onFailedOrSkipped;
        rewardConsumedForCurrentAd = false;
        Bridge.advertisement.ShowRewarded(placement);
#elif LEVELPLAY_ENABLED
        if (rewardedAd == null || !rewardedAd.IsAdReady())
        {
#if UNITY_EDITOR
            Debug.Log("[AdsManager] Simulating Rewarded Ad completion in Editor...");
            pendingRewardGranted = onRewardGranted;
            pendingRewardFailedOrSkipped = onFailedOrSkipped;
            rewardConsumedForCurrentAd = false;
            HandleRewarded();
            HandleRewardedClosed();
            return;
#else
            onFailedOrSkipped?.Invoke();
            return;
#endif
        }
        pendingRewardGranted = onRewardGranted;
        pendingRewardFailedOrSkipped = onFailedOrSkipped;
        rewardConsumedForCurrentAd = false;
        rewardedAd.ShowAd();
#if UNITY_EDITOR
        if (editorCountdownCoroutine != null) StopCoroutine(editorCountdownCoroutine);
        editorCountdownCoroutine = StartCoroutine(DriveLevelPlayEditorCountdown());
#endif
#else
#if UNITY_EDITOR
        Debug.Log("[AdsManager] Simulating Rewarded Ad completion in Editor (no SDK active)...");
        pendingRewardGranted = onRewardGranted;
        pendingRewardFailedOrSkipped = onFailedOrSkipped;
        rewardConsumedForCurrentAd = false;
        HandleRewarded();
        HandleRewardedClosed();
        return;
#else
        onFailedOrSkipped?.Invoke();
#endif
#endif
    }

    public bool IsInterstitialReady()
    {
        if (!adsEnabled) return false;
#if UNITY_WEBGL
        return Bridge.advertisement != null && Bridge.advertisement.isInterstitialSupported;
#elif LEVELPLAY_ENABLED
        return interstitialAd != null && interstitialAd.IsAdReady();
#else
        return false;
#endif
    }

    public void ShowInterstitial(Action onClosed, string placement = null)
    {
        if (!adsEnabled)
        {
            onClosed?.Invoke();
            return;
        }

#if UNITY_WEBGL
        if (Bridge.advertisement == null || !Bridge.advertisement.isInterstitialSupported)
        {
            onClosed?.Invoke();
            return;
        }
        pendingInterstitialClosed = onClosed;
        Bridge.advertisement.ShowInterstitial(placement);
#elif LEVELPLAY_ENABLED
        if (interstitialAd == null || !interstitialAd.IsAdReady())
        {
            onClosed?.Invoke();
            return;
        }
        pendingInterstitialClosed = onClosed;
        interstitialAd.ShowAd();
#else
        onClosed?.Invoke();
#endif
    }

    public bool IsBannerSupported()
    {
        if (!adsEnabled) return false;
#if UNITY_WEBGL
        return Bridge.advertisement != null && Bridge.advertisement.isBannerSupported;
#else
        return false;
#endif
    }

    public void ShowBanner(string placement = null)
    {
        if (!adsEnabled) return;
#if UNITY_WEBGL
        try
        {
            Bridge.advertisement?.ShowBanner(BannerPosition.Bottom, placement);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AdsManager] ShowBanner failed: {ex.Message}");
        }
#endif
    }

    public void HideBanner()
    {
#if UNITY_WEBGL
        try
        {
            Bridge.advertisement?.HideBanner();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AdsManager] HideBanner failed: {ex.Message}");
        }
#endif
    }

    public void CheckAdBlock(Action<bool> callback)
    {
#if UNITY_WEBGL
        try
        {
            if (Bridge.advertisement != null)
            {
                Bridge.advertisement.CheckAdBlock(callback);
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AdsManager] CheckAdBlock failed: {ex.Message}");
        }
#endif
        callback?.Invoke(false);
    }

    public void SetConsent(bool granted)
    {
#if !UNITY_WEBGL && LEVELPLAY_ENABLED
        LevelPlayPrivacySettings.SetGDPRConsent(granted);
#endif
    }
}
