using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Universal Unity Ads & Ad Network Manager.
/// Manages three distinct rewarded ad flows with strict completion checks and scene safety:
/// 1. Main Menu Ad (+50 Coins, strict 4-hour cooldown with HH:MM:SS countdown text).
/// 2. Game Scene Ad (Double Coins, +100% match of collected coins, no cooldown).
/// 3. Shop Ad (+500 Coins, no cooldown, with skip retry popup penalty).
/// </summary>
[DisallowMultipleComponent]
public class AdManager : MonoBehaviour
{
    // =========================================================================
    // SINGLETON INSTANCE
    // =========================================================================
    public static AdManager Instance { get; private set; }

    // =========================================================================
    // 1. MAIN MENU AD CONFIGURATION (+50 COINS, 4-HOUR COOLDOWN)
    // =========================================================================
    [Header("1. Main Menu Ad (+50 Coins)")]
    [Tooltip("Coin reward granted upon full completion of the Main Menu ad.")]
    [SerializeField] private int mainMenu50CoinReward = 50;

    [Tooltip("Real-world cooldown in hours for the Main Menu 50 Coin Ad.")]
    [SerializeField] private float mainMenuCooldownHours = 4f;

    [Tooltip("The Main Menu 'Watch Ad' button reference (optional/scene-specific).")]
    [SerializeField] private Button mainMenuAdButton;

    [Tooltip("TextMeshProUGUI element showing the live HH:MM:SS countdown while on cooldown.")]
    [SerializeField] private TextMeshProUGUI mainMenuCountdownText;

    public const string Last50CoinAdTimeKey = "Last50CoinAdTime";
    public const string LastMainMenuAdTimeKey = "Last50CoinAdTime"; // Backward-compatibility alias

    // =========================================================================
    // 2. GAME SCENE AD CONFIGURATION (DOUBLE COINS, NO COOLDOWN)
    // =========================================================================
    [Header("2. Game Scene Ad (Double Coins)")]
    [Tooltip("Optional Watch Ad button in the Game Over / End of Level screen.")]
    [SerializeField] private Button doubleCoinsAdButton;

    [Tooltip("Optional label on the Double Coins button.")]
    [SerializeField] private TextMeshProUGUI doubleCoinsButtonText;

    // =========================================================================
    // 3. SHOP AD CONFIGURATION (+500 COINS, NO COOLDOWN)
    // =========================================================================
    [Header("3. Shop Ad (+500 Coins)")]
    [Tooltip("Coin reward granted upon full completion of the Shop ad.")]
    [SerializeField] private int shop500CoinReward = 500;

    [Tooltip("Optional Watch Ad button in the Shop screen.")]
    [SerializeField] private Button shopAdButton;

    [Tooltip("Panel activated if the player skips or closes the 500 Coin Shop Ad early.")]
    [SerializeField] private GameObject skippedRetryPanel;

    [Tooltip("Button inside skippedRetryPanel to confirm retry and watch ad again.")]
    [SerializeField] private Button retryConfirmButton;

    [Tooltip("Button inside skippedRetryPanel to dismiss and cancel.")]
    [SerializeField] private Button retryCancelButton;

    // =========================================================================
    // 4. GENERAL POPUP REFERENCES & WARNINGS (SCENE-SAFE)
    // =========================================================================
    [Header("General Popup References (Optional / Fallback)")]
    [SerializeField] private GameObject warningPopupPanel;
    [SerializeField] private TextMeshProUGUI warningPopupMessageText;
    [SerializeField] private Button warningPopupOkButton;
    [SerializeField] private AdWarningPopup warningPopup;
    [SerializeField] private RewardPopup rewardPopup;

    // =========================================================================
    // 5. MASTER CONTROLS & PLATFORM IDS
    // =========================================================================
    [Header("Master Controls")]
    [SerializeField] public bool adsEnabled = true;
    [SerializeField] private bool testMode = true;
    [SerializeField] private string androidGameId = "1234567";
    [SerializeField] private string iosGameId = "7654321";
    [SerializeField] private string rewardedAdUnitIdAndroid = "Rewarded_Android";
    [SerializeField] private string rewardedAdUnitIdIOS = "Rewarded_iOS";

    // =========================================================================
    // RUNTIME STATE
    // =========================================================================
    private AdRewardType currentRewardType = AdRewardType.None;
    private int pendingCoinsToDouble = 0;
    private string currentAdUnitId = "Rewarded_Android";
    private bool isAdShowing = false;
    private float nextTimerUpdate = 0f;

    // Optional custom callback delegates
    private Action onCustomRewardGranted;
    private Action onCustomRewardSkippedOrFailed;

    public float LastRewardedClosedRealtime { get; private set; } = -1f;

#if UNITY_EDITOR
    private bool showEditorAdModal = false;
    private string editorAdModalTitle = "";
#endif

    // =========================================================================
    // LIFECYCLE & SINGLETON INITIALIZATION
    // =========================================================================
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ResolvePlatformAdUnit();
        HookUIListeners();
    }

    private void Start()
    {
        HookUIListeners();
        UpdateMainMenuUI();
    }

    private void OnEnable()
    {
        HookUIListeners();
        UpdateMainMenuUI();
    }

    private void Update()
    {
        // Periodic update for countdown timer (every 0.5 seconds)
        if (Time.unscaledTime >= nextTimerUpdate)
        {
            nextTimerUpdate = Time.unscaledTime + 0.5f;
            UpdateMainMenuUI();
        }
    }

    private void ResolvePlatformAdUnit()
    {
#if UNITY_IOS
        currentAdUnitId = rewardedAdUnitIdIOS;
#else
        currentAdUnitId = rewardedAdUnitIdAndroid;
#endif
    }

    public void HookUIListeners()
    {
        if (mainMenuAdButton != null)
        {
            mainMenuAdButton.onClick.RemoveListener(ShowMainMenu50CoinAd);
            mainMenuAdButton.onClick.AddListener(ShowMainMenu50CoinAd);
        }

        if (shopAdButton != null)
        {
            shopAdButton.onClick.RemoveListener(ShowShop500CoinAd);
            shopAdButton.onClick.AddListener(ShowShop500CoinAd);
        }

        if (retryConfirmButton != null)
        {
            retryConfirmButton.onClick.RemoveListener(ConfirmRetryAd);
            retryConfirmButton.onClick.AddListener(ConfirmRetryAd);
        }

        if (retryCancelButton != null)
        {
            retryCancelButton.onClick.RemoveListener(CancelRetryAd);
            retryCancelButton.onClick.AddListener(CancelRetryAd);
        }

        if (warningPopupOkButton != null)
        {
            warningPopupOkButton.onClick.RemoveListener(DismissWarningPanel);
            warningPopupOkButton.onClick.AddListener(DismissWarningPanel);
        }
    }

    // =========================================================================
    // 1. MAIN MENU AD (+50 COINS, 4-HOUR COOLDOWN)
    // =========================================================================

    /// <summary>
    /// Shows the Main Menu 50 Coin rewarded ad.
    /// Strictly verifies cooldown. Does NOT give coins or trigger cooldown on button click.
    /// </summary>
    public void ShowMainMenu50CoinAd()
    {
        if (!adsEnabled)
        {
            Debug.LogWarning("[AdManager] Ads are disabled.");
            return;
        }

        if (IsMainMenuAdOnCooldown(out TimeSpan remaining))
        {
            Debug.LogWarning($"[AdManager] Main Menu 50 Coin Ad is on cooldown. Remaining: {remaining}");
            UpdateMainMenuUI();
            return;
        }

        if (isAdShowing) return;

        currentRewardType = AdRewardType.MainMenu50Coins;
        pendingCoinsToDouble = 0;
        onCustomRewardGranted = null;
        onCustomRewardSkippedOrFailed = null;

        ExecuteShowAd();
    }

    /// <summary>
    /// Checks if the Main Menu 50 Coin Ad is on its 4-hour cooldown and returns the remaining TimeSpan.
    /// </summary>
    public bool IsMainMenuAdOnCooldown(out TimeSpan remainingTime)
    {
        remainingTime = TimeSpan.Zero;

        if (!PlayerPrefs.HasKey(Last50CoinAdTimeKey))
        {
            return false;
        }

        string savedTimeStr = PlayerPrefs.GetString(Last50CoinAdTimeKey, string.Empty);
        if (string.IsNullOrEmpty(savedTimeStr))
        {
            return false;
        }

        if (DateTime.TryParse(savedTimeStr, out DateTime lastClaimTime))
        {
            DateTime now = DateTime.Now;
            TimeSpan difference = now - lastClaimTime;
            TimeSpan cooldownDuration = TimeSpan.FromHours(mainMenuCooldownHours);

            if (difference < cooldownDuration)
            {
                remainingTime = cooldownDuration - difference;
                return true; // Active cooldown
            }
        }

        return false; // Cooldown expired
    }

    public bool IsMainMenuAdOnCooldown() => IsMainMenuAdOnCooldown(out _);

    /// <summary>
    /// Updates the Main Menu button interactability and formats countdown text strictly as HH:MM:SS.
    /// </summary>
    public void UpdateMainMenuUI()
    {
        if (mainMenuAdButton == null && mainMenuCountdownText == null) return;

        bool onCooldown = IsMainMenuAdOnCooldown(out TimeSpan remaining);

        if (onCooldown)
        {
            if (mainMenuAdButton != null)
            {
                mainMenuAdButton.interactable = false;
            }

            if (mainMenuCountdownText != null)
            {
                int totalHours = (int)remaining.TotalHours;
                mainMenuCountdownText.text = $"{totalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
            }
        }
        else
        {
            if (mainMenuAdButton != null)
            {
                mainMenuAdButton.interactable = adsEnabled && !isAdShowing;
            }

            if (mainMenuCountdownText != null)
            {
                mainMenuCountdownText.text = $"Watch Ad (+{mainMenu50CoinReward})";
            }
        }
    }

    // =========================================================================
    // 2. GAME SCENE AD (DOUBLE COINS, NO COOLDOWN)
    // =========================================================================

    /// <summary>
    /// Shows the Game Scene double coins rewarded ad (+100% match of round earnings, NO cooldown).
    /// </summary>
    /// <param name="coinsCollected">Coins earned in the round/wave to be matched 100%.</param>
    public void ShowDoubleCoinsAd(int coinsCollected)
    {
        if (!adsEnabled)
        {
            Debug.LogWarning("[AdManager] Ads are disabled.");
            return;
        }

        if (isAdShowing) return;

        currentRewardType = AdRewardType.DoubleCoins;
        pendingCoinsToDouble = Mathf.Max(0, coinsCollected);
        onCustomRewardGranted = null;
        onCustomRewardSkippedOrFailed = null;

        ExecuteShowAd();
    }

    // =========================================================================
    // 3. SHOP AD (+500 COINS, NO COOLDOWN, SKIP RETRY PANEL)
    // =========================================================================

    /// <summary>
    /// Shows the Shop 500 Coin rewarded ad (NO cooldown).
    /// </summary>
    public void ShowShop500CoinAd()
    {
        if (!adsEnabled)
        {
            Debug.LogWarning("[AdManager] Ads are disabled.");
            return;
        }

        if (isAdShowing) return;

        // Ensure retry panel is hidden when starting ad
        if (skippedRetryPanel != null)
        {
            skippedRetryPanel.SetActive(false);
        }

        currentRewardType = AdRewardType.Shop500Coins;
        pendingCoinsToDouble = 0;
        onCustomRewardGranted = null;
        onCustomRewardSkippedOrFailed = null;

        ExecuteShowAd();
    }

    /// <summary>
    /// Confirms retry from the skipped penalty panel: hides panel and triggers 500 Coin Shop ad again.
    /// </summary>
    public void ConfirmRetryAd()
    {
        if (skippedRetryPanel != null)
        {
            skippedRetryPanel.SetActive(false);
        }

        ShowShop500CoinAd();
    }

    /// <summary>
    /// Cancels retry from the skipped penalty panel: simply hides the panel.
    /// </summary>
    public void CancelRetryAd()
    {
        if (skippedRetryPanel != null)
        {
            skippedRetryPanel.SetActive(false);
        }
    }

    // =========================================================================
    // 4. CORE AD PRESENTATION & DISPATCH
    // =========================================================================

    private void ExecuteShowAd()
    {
        isAdShowing = true;
        if (mainMenuAdButton != null) mainMenuAdButton.interactable = false;
        if (shopAdButton != null) shopAdButton.interactable = false;
        if (doubleCoinsAdButton != null) doubleCoinsAdButton.interactable = false;

        Debug.Log($"[AdManager] Launching Rewarded Ad for type: {currentRewardType} (AdUnit: {currentAdUnitId})");

#if UNITY_EDITOR
        showEditorAdModal = true;
        editorAdModalTitle = currentRewardType switch
        {
            AdRewardType.MainMenu50Coins => "Main Menu Ad (+50 Coins)",
            AdRewardType.DoubleCoins => $"Game Scene Double Coins Ad (+{pendingCoinsToDouble} Coins)",
            AdRewardType.Shop500Coins => "Shop Ad (+500 Coins)",
            _ => "Rewarded Ad"
        };
#elif UNITY_WEBGL
        if (com.playgama.Bridge.advertisement != null && com.playgama.Bridge.advertisement.isRewardedSupported)
        {
            Action<Playgama.Modules.Advertisement.RewardedState> handler = null;
            bool wasRewarded = false;
            handler = state =>
            {
                if (state == Playgama.Modules.Advertisement.RewardedState.Rewarded)
                {
                    wasRewarded = true;
                }
                else if (state == Playgama.Modules.Advertisement.RewardedState.Closed)
                {
                    com.playgama.Bridge.advertisement.rewardedStateChanged -= handler;
                    OnUnityAdsShowComplete(currentAdUnitId, wasRewarded ? UnityAdsShowCompletionState.COMPLETED : UnityAdsShowCompletionState.SKIPPED);
                }
                else if (state == Playgama.Modules.Advertisement.RewardedState.Failed)
                {
                    com.playgama.Bridge.advertisement.rewardedStateChanged -= handler;
                    OnUnityAdsShowComplete(currentAdUnitId, UnityAdsShowCompletionState.SKIPPED);
                }
            };
            com.playgama.Bridge.advertisement.rewardedStateChanged += handler;
            com.playgama.Bridge.advertisement.ShowRewarded();
        }
        else
        {
            OnUnityAdsShowComplete(currentAdUnitId, UnityAdsShowCompletionState.SKIPPED);
        }
#else
        if (AdsManager.Instance != null && AdsManager.Instance.IsRewardedReady())
        {
            AdsManager.Instance.ShowRewardedAd(
                onRewardGranted: () => OnUnityAdsShowComplete(currentAdUnitId, UnityAdsShowCompletionState.COMPLETED),
                onFailedOrSkipped: () => OnUnityAdsShowComplete(currentAdUnitId, UnityAdsShowCompletionState.SKIPPED)
            );
        }
        else
        {
            OnUnityAdsShowComplete(currentAdUnitId, UnityAdsShowCompletionState.SKIPPED);
        }
#endif
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!showEditorAdModal) return;

        // Dark background overlay
        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);

        float dialogWidth = Mathf.Min(480f, Screen.width - 40f);
        float dialogHeight = 220f;
        float x = (Screen.width - dialogWidth) * 0.5f;
        float y = (Screen.height - dialogHeight) * 0.5f;

        GUI.Box(new Rect(x, y, dialogWidth, dialogHeight), GUIContent.none);

        GUILayout.BeginArea(new Rect(x + 20, y + 20, dialogWidth - 40, dialogHeight - 40));

        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        GUILayout.Label("AD SIMULATOR (UNITY EDITOR)", titleStyle);
        GUILayout.Space(8);

        var msgStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        GUILayout.Label($"Simulating Ad Playback:\n<b>{editorAdModalTitle}</b>", msgStyle);
        GUILayout.Space(16);

        GUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
        if (GUILayout.Button("COMPLETE AD\n(Watch Full)", GUILayout.Height(50)))
        {
            showEditorAdModal = false;
            OnUnityAdsShowComplete(currentAdUnitId, UnityAdsShowCompletionState.COMPLETED);
        }

        GUILayout.Space(10);

        GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
        if (GUILayout.Button("SKIP / CLOSE EARLY\n(No Reward)", GUILayout.Height(50)))
        {
            showEditorAdModal = false;
            OnUnityAdsShowComplete(currentAdUnitId, UnityAdsShowCompletionState.SKIPPED);
        }

        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }
#endif

    // =========================================================================
    // 5. STRICT COMPLETION CALLBACK & REWARD PROCESSING
    // =========================================================================

    /// <summary>
    /// Exact Unity Ads Show Completion Callback (IUnityAdsShowListener).
    /// Strictly verifies that the ad state is COMPLETED before granting any rewards.
    /// </summary>
    public void OnUnityAdsShowComplete(string adUnitId, UnityAdsShowCompletionState showCompletionState)
    {
        Debug.Log("Ad state: " + showCompletionState);
        isAdShowing = false;
        LastRewardedClosedRealtime = Time.realtimeSinceStartup;

        // ---------------------------------------------------------------------
        // STRICT STATE CHECKING:
        // ONLY COMPLETED is allowed to call ProcessReward().
        // ---------------------------------------------------------------------
        if (showCompletionState == UnityAdsShowCompletionState.COMPLETED)
        {
            Debug.Log("Reward Granted!");
            ProcessReward();
        }
        else // SKIPPED, UNKNOWN, NOT_DEFINED, or failed
        {
            Debug.LogWarning($"[AdManager] Ad was SKIPPED or closed early ({showCompletionState}). NO REWARD GIVEN.");
            ProcessSkippedOrFailedAd();
        }
    }

    /// <summary>
    /// Processes rewards and cooldowns for the completed ad type.
    /// MUST only be called inside the COMPLETED block of the callback.
    /// </summary>
    public void ProcessReward()
    {
        AdRewardType grantedType = currentRewardType;
        currentRewardType = AdRewardType.None;

        if (grantedType == AdRewardType.MainMenu50Coins || grantedType == AdRewardType.FlatAmount)
        {
            // 1. Grant 50 coins
            Wallet.Add(mainMenu50CoinReward);

            // 2. Save 4-hour cooldown timestamp
            PlayerPrefs.SetString(Last50CoinAdTimeKey, DateTime.Now.ToString());
            PlayerPrefs.Save();

            Debug.Log($"[AdManager] Granted {mainMenu50CoinReward} coins (Main Menu Ad). Cooldown saved: {PlayerPrefs.GetString(Last50CoinAdTimeKey)}");

            // 3. Show celebration popup if available
            ShowCelebrationPopup("COIN REWARD!", $"You watched the ad and earned {mainMenu50CoinReward} bonus coins!", $"+{mainMenu50CoinReward} COINS");
        }
        else if (grantedType == AdRewardType.DoubleCoins)
        {
            // 1. Grant 100% matched round coins
            int bonus = pendingCoinsToDouble;
            pendingCoinsToDouble = 0;

            if (bonus > 0)
            {
                Wallet.Add(bonus);
                Debug.Log($"[AdManager] Double Coins Ad: Granted {bonus} bonus coins (100% match) to Wallet. Total: {Wallet.Total}");
            }

            // 2. Show celebration popup if available
            ShowCelebrationPopup("DOUBLE COINS!", "You watched the entire ad and doubled your coins for this round!", $"+{bonus} COINS");
        }
        else if (grantedType == AdRewardType.Shop500Coins)
        {
            // 1. Grant 500 coins (no cooldown)
            Wallet.Add(shop500CoinReward);
            Debug.Log($"[AdManager] Shop 500 Coin Ad: Granted {shop500CoinReward} coins to Wallet. Total: {Wallet.Total}");

            // 2. Show celebration popup if available
            ShowCelebrationPopup("SHOP COIN REWARD!", $"You watched the ad and earned {shop500CoinReward} bonus coins!", $"+{shop500CoinReward} COINS");
        }

        // Invoke optional custom callback
        Action successCb = onCustomRewardGranted;
        onCustomRewardGranted = null;
        onCustomRewardSkippedOrFailed = null;
        successCb?.Invoke();

        UpdateMainMenuUI();
    }

    private void ProcessSkippedOrFailedAd()
    {
        AdRewardType failedType = currentRewardType;
        currentRewardType = AdRewardType.None;
        pendingCoinsToDouble = 0;

        Action failCb = onCustomRewardSkippedOrFailed;
        onCustomRewardGranted = null;
        onCustomRewardSkippedOrFailed = null;
        failCb?.Invoke();

        // If the Shop 500 Coin Ad was skipped, activate the retry penalty panel
        if (failedType == AdRewardType.Shop500Coins)
        {
            if (skippedRetryPanel != null)
            {
                skippedRetryPanel.SetActive(true);
                skippedRetryPanel.transform.SetAsLastSibling();
            }
            else
            {
                TriggerWarningPopup("You skipped the 500 Coin Ad. You did not receive any coins.");
            }
        }
        else
        {
            TriggerWarningPopup("You closed the ad early. You won't get the reward.");
        }

        UpdateMainMenuUI();
    }

    // =========================================================================
    // 6. SCENE-SAFE UI & POPUP HELPERS (ALL NULL-CHECKED)
    // =========================================================================

    public void TriggerWarningPopup(string message)
    {
        if (warningPopupPanel != null)
        {
            warningPopupPanel.SetActive(true);
            warningPopupPanel.transform.SetAsLastSibling();

            if (warningPopupMessageText != null)
            {
                warningPopupMessageText.text = message;
            }
            return;
        }

        if (warningPopup == null)
        {
            warningPopup = FindAnyObjectByType<AdWarningPopup>(FindObjectsInactive.Include);
        }

        if (warningPopup != null)
        {
            warningPopup.gameObject.SetActive(true);
            warningPopup.Show(message, UpdateMainMenuUI);
            return;
        }

        Debug.LogWarning($"[AdManager] Warning Popup: '{message}' (No UI panel assigned).");
    }

    private void DismissWarningPanel()
    {
        if (warningPopupPanel != null)
        {
            warningPopupPanel.SetActive(false);
        }
        UpdateMainMenuUI();
    }

    private void ShowCelebrationPopup(string title, string message, string amountString)
    {
        if (rewardPopup == null)
        {
            rewardPopup = FindAnyObjectByType<RewardPopup>(FindObjectsInactive.Include);
        }

        if (rewardPopup != null)
        {
            rewardPopup.gameObject.SetActive(true);
            rewardPopup.Show(title, message, amountString, UpdateMainMenuUI);
        }
    }

    // Dynamic UI scene binding helpers (null-safe)
    public void SetMainMenuUI(Button adButton, TextMeshProUGUI countdownText)
    {
        mainMenuAdButton = adButton;
        mainMenuCountdownText = countdownText;
        HookUIListeners();
        UpdateMainMenuUI();
    }

    public void SetShopUI(GameObject retryPanel, Button retryConfirm = null, Button retryCancel = null)
    {
        skippedRetryPanel = retryPanel;
        if (retryConfirm != null) retryConfirmButton = retryConfirm;
        if (retryCancel != null) retryCancelButton = retryCancel;
        HookUIListeners();
    }

    // =========================================================================
    // 7. BACKWARD COMPATIBILITY ALIASES
    // =========================================================================
    public void ShowMainMenuAd() => ShowMainMenu50CoinAd();
    public void Show500CoinAd() => ShowShop500CoinAd();
    public bool CheckMainMenuAdCooldown(out TimeSpan remainingTime) => IsMainMenuAdOnCooldown(out remainingTime);
    public bool CheckMainMenuAdCooldown() => IsMainMenuAdOnCooldown(out _);
    public bool IsRewardedReady() => adsEnabled && !isAdShowing;
    public bool IsInterstitialReady() => adsEnabled;
    public void ShowInterstitial(Action onClosed) => onClosed?.Invoke();

    public void ShowRewardedAd(Action onRewardGranted, Action onFailedOrSkipped, string placement = null)
    {
        if (!adsEnabled)
        {
            onFailedOrSkipped?.Invoke();
            return;
        }

        if (isAdShowing) return;

        currentRewardType = AdRewardType.None;
        onCustomRewardGranted = onRewardGranted;
        onCustomRewardSkippedOrFailed = onFailedOrSkipped;

        ExecuteShowAd();
    }
}
