using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Defines the reward behavior for the ad button.
/// </summary>
public enum RewardType
{
    DoubleCoins, // End-of-level run earnings (+100% match, no cooldown)
    FlatAmount   // Main Menu flat coin reward (4-hour real-world cooldown)
}

/// <summary>
/// Production-ready Rewarded Ad Reward Manager.
/// Strictly enforces that the reward is ONLY granted when the ad completes fully.
/// Supports both End-of-Level Double Coins (+100%) and Main Menu Flat Coin rewards with 4-hour cooldown.
/// </summary>
[DisallowMultipleComponent]
public class AdRewardManager : MonoBehaviour
{
    // =========================================================================
    // 1. REWARD TYPE & COOLDOWN CONFIGURATION
    // =========================================================================
    [Header("Reward Type & Settings")]
    [Tooltip("Select DoubleCoins for End-of-Level screen, or FlatAmount for Main Menu.")]
    [SerializeField]
    private RewardType rewardType = RewardType.DoubleCoins;

    [Tooltip("Flat coin reward given when RewardType is FlatAmount (Main Menu).")]
    [SerializeField]
    private int flatRewardCoins = 500;

    [Tooltip("Cooldown duration in hours for the Main Menu FlatAmount ad.")]
    [SerializeField]
    private float cooldownHours = 4f;

    public const string LastMainMenuAdTimeKey = "LastMainMenuAdTime";

    // =========================================================================
    // 2. DYNAMIC TEXT CONFIGURATION (INSPECTOR EDITABLE / TRANSLATABLE)
    // =========================================================================
    [Header("Dynamic Text Configuration (Inspector Editable)")]
    [Tooltip("The prompt text displayed on the reward screen.")]
    [SerializeField]
    private string adOfferText = "You will be rewarded with double coins if you watch an ad";

    [Tooltip("The warning text displayed in the popup when the ad is closed or skipped early.")]
    [SerializeField]
    private string adSkippedWarningText = "You closed the ad early. You won't get the reward.";

    // =========================================================================
    // 3. UI TEXT & BUTTON REFERENCES
    // =========================================================================
    [Header("UI Text References")]
    [Tooltip("TextMeshProUGUI element that displays the adOfferText or countdown.")]
    [SerializeField]
    private TextMeshProUGUI promptLabel;

    [Tooltip("Secondary TextMeshProUGUI element that displays the dynamic bonus amount (e.g. '+150 Coins!').")]
    [SerializeField]
    private TextMeshProUGUI bonusAmountLabel;

    [Tooltip("Optional Text label directly on the Watch Ad button for countdown/action.")]
    [SerializeField]
    private TMP_Text buttonLabel;

    [Header("UI Controls")]
    [Tooltip("Button clicked by the player to watch the rewarded ad.")]
    [SerializeField]
    private Button watchAdButton;

    [Tooltip("Root GameObject of the Watch Ad button / container (optional).")]
    [SerializeField]
    private GameObject watchAdButtonRoot;

    // =========================================================================
    // 4. POPUP SYSTEM REFERENCES
    // =========================================================================
    [Header("Popup System References")]
    [Tooltip("Reference to the celebration RewardPopup shown upon successful ad completion.")]
    [SerializeField]
    private RewardPopup rewardPopup;

    [Tooltip("Reference to the AdWarningPopup shown when the ad is skipped, canceled, or failed.")]
    [SerializeField]
    private AdWarningPopup warningPopup;

    // Optional direct GameObject fallback slots for the warning popup
    [Header("Optional Direct Warning Popup Slots (Fallback)")]
    [SerializeField] private GameObject warningPopupPanel;
    [SerializeField] private TextMeshProUGUI warningPopupText;
    [SerializeField] private Button warningPopupOkButton;

    // =========================================================================
    // 5. STATE FLAGS & ANTI-EXPLOIT
    // =========================================================================
    private bool hasClaimedReward = false;
    private bool isAdInProgress = false;

    private void Awake()
    {
        if (rewardPopup == null)
        {
            rewardPopup = FindAnyObjectByType<RewardPopup>(FindObjectsInactive.Include);
        }
        if (warningPopup == null)
        {
            warningPopup = FindAnyObjectByType<AdWarningPopup>(FindObjectsInactive.Include);
        }
    }

    private void Start()
    {
        if (watchAdButton != null)
        {
            watchAdButton.onClick.RemoveListener(OnWatchAdButtonClicked);
            watchAdButton.onClick.AddListener(OnWatchAdButtonClicked);
        }

        if (warningPopupOkButton != null)
        {
            warningPopupOkButton.onClick.RemoveListener(DismissDirectWarningPanel);
            warningPopupOkButton.onClick.AddListener(DismissDirectWarningPanel);
        }

        if (rewardType == RewardType.FlatAmount)
        {
            CheckMainMenuAdCooldown();
        }
    }

    private void OnEnable()
    {
        hasClaimedReward = false;
        isAdInProgress = false;
        if (warningPopupPanel != null) warningPopupPanel.SetActive(false);

        if (rewardType == RewardType.FlatAmount)
        {
            CheckMainMenuAdCooldown();
        }
        UpdateRewardUI();
    }

    private void Update()
    {
        // Continuously update countdown for Main Menu FlatAmount ad
        if (rewardType == RewardType.FlatAmount)
        {
            UpdateCooldownCountdown();
        }
    }

    /// <summary>
    /// Checks if the Main Menu ad is currently in a 4-hour cooldown.
    /// </summary>
    public bool CheckMainMenuAdCooldown()
    {
        return CheckMainMenuAdCooldown(out _);
    }

    /// <summary>
    /// Checks the 4-hour cooldown and outputs the remaining TimeSpan.
    /// </summary>
    public bool CheckMainMenuAdCooldown(out TimeSpan remainingTime)
    {
        remainingTime = TimeSpan.Zero;

        if (rewardType != RewardType.FlatAmount)
        {
            return false; // DoubleCoins has no cooldown
        }

        if (!PlayerPrefs.HasKey(LastMainMenuAdTimeKey))
        {
            return false;
        }

        string savedTimeString = PlayerPrefs.GetString(LastMainMenuAdTimeKey, string.Empty);
        if (string.IsNullOrEmpty(savedTimeString))
        {
            return false;
        }

        if (DateTime.TryParse(savedTimeString, out DateTime lastClaimTime))
        {
            DateTime now = DateTime.Now;
            TimeSpan difference = now - lastClaimTime;
            TimeSpan cooldownDuration = TimeSpan.FromHours(cooldownHours);

            if (difference < cooldownDuration)
            {
                remainingTime = cooldownDuration - difference;
                return true; // Still in cooldown
            }
        }

        return false; // Cooldown finished
    }

    private float nextAdTimerUpdate = 0f;

    private void UpdateCooldownCountdown()
    {
        if (isAdInProgress) return;
        if (Time.unscaledTime < nextAdTimerUpdate) return;
        nextAdTimerUpdate = Time.unscaledTime + 0.5f;

        bool onCooldown = CheckMainMenuAdCooldown(out TimeSpan remaining);

        if (onCooldown)
        {
            if (watchAdButton != null) watchAdButton.interactable = false;

            string countdownText;
            if (remaining.TotalHours >= 1)
            {
                countdownText = $"Available in {remaining.Hours}h {remaining.Minutes}m";
            }
            else if (remaining.TotalMinutes >= 1)
            {
                countdownText = $"Available in {remaining.Minutes}m {remaining.Seconds}s";
            }
            else
            {
                countdownText = $"Available in {remaining.Seconds}s";
            }

            if (buttonLabel != null) buttonLabel.text = countdownText;
            if (bonusAmountLabel != null) bonusAmountLabel.text = countdownText;
            if (promptLabel != null) promptLabel.text = $"Watch ad reward is on cooldown.\n{countdownText}";
        }
        else
        {
            // No ad SDK integrated yet on this branch (WebGL-first pass, ads
            // come back before publishing) -- forced false keeps the button
            // non-interactable, matching GameOverAdOffer's same pattern.
            bool adReady = false;
            if (watchAdButton != null) watchAdButton.interactable = adReady && !hasClaimedReward;
            if (buttonLabel != null) buttonLabel.text = $"Watch Ad (+{flatRewardCoins})";
        }
    }

    /// <summary>
    /// Returns the coins earned by the player during this specific round.
    /// </summary>
    public int GetCoinsCollectedThisRound()
    {
        if (GameManager.Instance != null)
        {
            return GameManager.Instance.coinsEarnedThisRun;
        }
        return 0;
    }

    /// <summary>
    /// Updates the UI text and button state based on round earnings and ad availability.
    /// </summary>
    public void UpdateRewardUI()
    {
        if (rewardType == RewardType.FlatAmount)
        {
            UpdateCooldownCountdown();
            return;
        }

        int coinsCollectedThisRound = GetCoinsCollectedThisRound();
        int bonusCoins = coinsCollectedThisRound; // +100% proportional double coins

        // 1. Bind customizable offer text
        if (promptLabel != null)
        {
            promptLabel.text = adOfferText;
        }

        // 2. Bind dynamic secondary bonus amount label
        if (bonusAmountLabel != null)
        {
            bonusAmountLabel.text = $"+{bonusCoins} Coins!";
        }

        // 3. Determine if the watch ad button should be displayed
        // No ad SDK integrated yet on this branch (WebGL-first pass, ads
        // come back before publishing) -- forced false keeps the button
        // hidden, matching GameOverAdOffer's same pattern.
        bool adReady = false;
        // Can only offer if not already claimed, round coins > 0, and ad is ready
        bool canOffer = !hasClaimedReward && bonusCoins > 0 && adReady;

        if (watchAdButtonRoot != null)
        {
            watchAdButtonRoot.SetActive(canOffer);
        }
        else if (watchAdButton != null)
        {
            watchAdButton.gameObject.SetActive(canOffer);
        }

        if (watchAdButton != null)
        {
            watchAdButton.interactable = canOffer && !isAdInProgress;
        }
    }

    /// <summary>
    /// Explicit entry point to show the 500 Coin Ad.
    /// Strictly triggers ad presentation without granting coins or starting cooldown on click.
    /// </summary>
    public void Show500CoinAd()
    {
        OnWatchAdButtonClicked();
    }

    /// <summary>
    /// Triggered when the player clicks the Watch Ad button.
    /// </summary>
    public void OnWatchAdButtonClicked()
    {
        if (rewardType == RewardType.FlatAmount && CheckMainMenuAdCooldown())
        {
            UpdateRewardUI();
            return;
        }

        if (hasClaimedReward || isAdInProgress) return;

        isAdInProgress = true;
        if (watchAdButton != null) watchAdButton.interactable = false;

        // No ad SDK integrated yet on this branch (WebGL-first pass, ads
        // come back before publishing) -- matches GameOverAdOffer's same
        // pattern. Not currently reachable (UpdateRewardUI/UpdateCooldown-
        // Countdown keep the button hidden via adReady = false above), but
        // kept intact so re-wiring is small: swap the log line below for a
        // real ad SDK call into OnAdShowComplete(Completed/Skipped).
        Debug.Log("[AdRewardManager] Watch Ad tapped, but no ad SDK is integrated yet for this build.");
        OnAdShowComplete(AdCompletionState.Failed);
    }

    /// <summary>
    /// Grants the coin reward and sets cooldown. ONLY called upon full ad completion.
    /// </summary>
    public void ProcessReward()
    {
        int roundCoins = GetCoinsCollectedThisRound();
        GrantAdReward(roundCoins);
    }

    // =========================================================================
    // 5. >>> AD SDK COMPLETION CALLBACKS (STRICT ENFORCEMENT) <<<
    // =========================================================================

    /// <summary>
    /// Unity Ads Show Completion Callback (IUnityAdsShowListener).
    /// Strictly verifies that the ad was watched to completion before granting rewards.
    /// </summary>
    /// <param name="adUnitId">The Ad Unit ID (Placement ID) that was shown.</param>
    /// <param name="showCompletionState">Completion state returned by the Unity Ads SDK.</param>
    public void OnUnityAdsShowComplete(string adUnitId, UnityAdsShowCompletionState showCompletionState)
    {
        Debug.Log("Ad state: " + showCompletionState);
        isAdInProgress = false;

        // Strict IF check: ONLY give reward if showCompletionState is COMPLETED
        if (showCompletionState == UnityAdsShowCompletionState.COMPLETED)
        {
            Debug.Log("Reward Granted!");
            ProcessReward();
        }
        else if (showCompletionState == UnityAdsShowCompletionState.SKIPPED)
        {
            Debug.LogWarning($"[AdRewardManager] Unity Ads ({adUnitId}) SKIPPED. Player closed ad early. NO REWARD GIVEN.");
            TriggerWarningPopup(adSkippedWarningText);
            UpdateRewardUI();
        }
        else // UNKNOWN, NOT_DEFINED, or failed
        {
            Debug.LogError($"[AdRewardManager] Unity Ads ({adUnitId}) ended with state: {showCompletionState}. NO REWARD GIVEN.");
            TriggerWarningPopup("Ad did not complete. You won't get the reward.");
            UpdateRewardUI();
        }
    }

    /// <summary>
    /// Universal Ad SDK completion evaluation callback (LevelPlay, AdMob, AppLovin, Bridge, etc.).
    /// </summary>
    public void OnAdShowComplete(AdCompletionState state)
    {
        Debug.Log("Ad state: " + state);
        isAdInProgress = false;

        // ---------------------------------------------------------------------
        // STRICT SWITCH/IF VALIDATION:
        // ONLY the 'Completed' state is allowed to trigger ProcessReward().
        // All other states MUST trigger the warning popup and grant NO coins.
        // ---------------------------------------------------------------------
        if (state == AdCompletionState.Completed)
        {
            Debug.Log("Reward Granted!");
            ProcessReward();
        }
        else if (state == AdCompletionState.Skipped || state == AdCompletionState.Canceled)
        {
            Debug.LogWarning("[AdRewardManager] SDK Callback: Ad was closed or skipped early. Triggering warning popup. NO REWARD GIVEN.");
            TriggerWarningPopup(adSkippedWarningText);
            UpdateRewardUI();
        }
        else if (state == AdCompletionState.Failed)
        {
            Debug.LogError("[AdRewardManager] SDK Callback: Ad failed to stream. Triggering warning popup. NO REWARD GIVEN.");
            TriggerWarningPopup("Ad failed to load. You won't get the reward.");
            UpdateRewardUI();
        }
        else
        {
            Debug.LogWarning($"[AdRewardManager] SDK Callback: Unrecognized state ({state}). NO REWARD GIVEN.");
            TriggerWarningPopup(adSkippedWarningText);
            UpdateRewardUI();
        }
    }

    // =========================================================================
    // 6. STRICT REWARD LOGIC & COOLDOWN PERSISTENCE
    // =========================================================================
    /// <summary>
    /// Calculates the reward, persists cooldown (if FlatAmount), and deposits coins into Wallet.
    /// </summary>
    /// <param name="coinsCollected">The coins collected this round (used when DoubleCoins).</param>
    public void GrantAdReward(int coinsCollected)
    {
        // 1. Anti-spam guard: Never grant twice per screen
        if (hasClaimedReward)
        {
            Debug.LogWarning("[AdRewardManager] Reward already claimed for this round. Blocking duplicate grant.");
            return;
        }

        // 2. Lock the reward latch
        hasClaimedReward = true;
        isAdInProgress = false;

        int coinsToGrant = 0;
        string popupTitle = "DOUBLE COINS!";
        string popupMessage = "You watched the entire ad and doubled your coins for this round!";

        if (rewardType == RewardType.FlatAmount)
        {
            coinsToGrant = flatRewardCoins;
            popupTitle = "COIN REWARD!";
            popupMessage = $"You watched the ad and earned {flatRewardCoins} bonus coins!";

            // Save cooldown timestamp for Main Menu flat reward
            PlayerPrefs.SetString(LastMainMenuAdTimeKey, System.DateTime.Now.ToString());
            PlayerPrefs.Save();
            Debug.Log($"[AdRewardManager] Main Menu Flat Reward granted. Cooldown saved: {PlayerPrefs.GetString(LastMainMenuAdTimeKey)}");
        }
        else
        {
            // End-of-level Double Coins: proportional match (+100%) of round earnings
            coinsToGrant = coinsCollected;
        }

        if (coinsToGrant > 0)
        {
            Wallet.Add(coinsToGrant);
            Debug.Log($"[AdRewardManager] SUCCESS: Granted {coinsToGrant} coins to Wallet. Total: {Wallet.Total}");
        }

        // 4. Update button interactability
        if (rewardType == RewardType.FlatAmount)
        {
            if (watchAdButton != null) watchAdButton.interactable = false;
        }
        else
        {
            if (watchAdButtonRoot != null) watchAdButtonRoot.SetActive(false);
            else if (watchAdButton != null) watchAdButton.gameObject.SetActive(false);
            if (watchAdButton != null) watchAdButton.interactable = false;
        }

        // 5. Display celebration popup
        if (rewardPopup == null)
        {
            rewardPopup = FindAnyObjectByType<RewardPopup>(FindObjectsInactive.Include);
        }

        if (rewardPopup != null)
        {
            rewardPopup.gameObject.SetActive(true);
            rewardPopup.Show(
                title: popupTitle,
                message: popupMessage,
                amountString: $"+{coinsToGrant} COINS",
                onClosed: () => UpdateRewardUI()
            );
        }
        else
        {
            UpdateRewardUI();
        }
    }

    // =========================================================================
    // 7. WARNING POPUP TRIGGER
    // =========================================================================
    /// <summary>
    /// Triggers the warning popup when the player closes or skips the ad early.
    /// </summary>
    public void TriggerWarningPopup(string message)
    {
        // 1. Check dedicated AdWarningPopup component
        if (warningPopup == null)
        {
            warningPopup = FindAnyObjectByType<AdWarningPopup>(FindObjectsInactive.Include);
        }

        if (warningPopup != null)
        {
            warningPopup.gameObject.SetActive(true);
            warningPopup.Show(message, () => UpdateRewardUI());
            return;
        }

        // 2. Check direct GameObject / UI panel slots
        if (warningPopupPanel != null)
        {
            if (warningPopupText != null) warningPopupText.text = message;
            warningPopupPanel.SetActive(true);
            warningPopupPanel.transform.SetAsLastSibling();
            return;
        }

        Debug.LogWarning($"[AdRewardManager] Warning Popup Triggered: '{message}' (No AdWarningPopup UI found in scene).");
    }

    private void DismissDirectWarningPanel()
    {
        if (warningPopupPanel != null)
        {
            warningPopupPanel.SetActive(false);
        }
        UpdateRewardUI();
    }
}

