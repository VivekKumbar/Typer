using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attached to the GameOverPanel. Handles the post-game rewarded ad offer,
/// strictly enforcing that +100% double coin rewards are ONLY granted if the ad is fully watched.
/// </summary>
[DisallowMultipleComponent]
public class GameOverAdOffer : MonoBehaviour
{
    // =========================================================================
    // 1. DYNAMIC AD PROMPT & FEEDBACK TEXT
    // =========================================================================
    [Header("Dynamic Text Configuration (Inspector Editable)")]
    [Tooltip("The prompt text displayed on the reward screen.")]
    [SerializeField]
    private string adOfferText = "You will be rewarded with double coins if you watch an ad";

    [Tooltip("The warning text displayed in the popup when the ad is closed or skipped early.")]
    [SerializeField]
    private string adSkippedWarningText = "You closed the ad early. You won't get the reward.";

    // =========================================================================
    // 2. UI REFERENCES
    // =========================================================================
    [Header("UI Text References")]
    [Tooltip("TextMeshProUGUI element that displays the adOfferText.")]
    [SerializeField]
    private TextMeshProUGUI promptLabel;

    [Tooltip("Secondary TextMeshProUGUI element that displays the dynamic bonus amount (e.g. '+150 Coins!').")]
    [SerializeField]
    private TextMeshProUGUI bonusAmountLabel;

    [Header("UI Controls")]
    [Tooltip("Root GameObject of the Watch Ad button (optional).")]
    public GameObject watchAdButtonRoot;
    [Tooltip("Button component the player clicks to watch the ad.")]
    public Button watchAdButton;
    [Tooltip("Text label on the button.")]
    public TMP_Text watchAdLabel;

    [Header("Popup System References")]
    [Tooltip("Reference to the RewardPopup UI component that displays the reward celebration upon watching the ad.")]
    public RewardPopup rewardPopup;

    [Tooltip("Reference to the AdWarningPopup shown when the ad is skipped, canceled, or failed.")]
    public AdWarningPopup warningPopup;

    // Optional direct GameObject fallback slots for the warning popup
    [Header("Optional Direct Warning Popup Slots (Fallback)")]
    [SerializeField] private GameObject warningPopupPanel;
    [SerializeField] private TextMeshProUGUI warningPopupText;
    [SerializeField] private Button warningPopupOkButton;

    // =========================================================================
    // 3. ANTI-EXPLOIT & STATE SAFETY FLAGS
    // =========================================================================
    private bool hasClaimedReward = false;
    private bool isAdInProgress = false;

    void Awake()
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

    void Start()
    {
        if (watchAdButton != null)
        {
            watchAdButton.onClick.RemoveListener(OnWatchAdClicked);
            watchAdButton.onClick.AddListener(OnWatchAdClicked);
        }

        if (warningPopupOkButton != null)
        {
            warningPopupOkButton.onClick.RemoveListener(DismissDirectWarningPanel);
            warningPopupOkButton.onClick.AddListener(DismissDirectWarningPanel);
        }
    }

    void OnEnable()
    {
        if (rewardPopup == null)
        {
            rewardPopup = FindAnyObjectByType<RewardPopup>(FindObjectsInactive.Include);
        }
        if (warningPopup == null)
        {
            warningPopup = FindAnyObjectByType<AdWarningPopup>(FindObjectsInactive.Include);
        }

        hasClaimedReward = false;
        isAdInProgress = false;
        if (warningPopupPanel != null) warningPopupPanel.SetActive(false);
        RefreshButton();
    }

    /// <summary>
    /// Returns the coins collected in the current round.
    /// </summary>
    public int GetCoinsCollectedThisRound()
    {
        GameManager gm = GameManager.Instance;
        return gm != null ? gm.coinsEarnedThisRun : 0;
    }

    /// <summary>
    /// Computes the exact double coins bonus amount (+100% of round earnings).
    /// </summary>
    public int ComputeBonus()
    {
        return GetCoinsCollectedThisRound();
    }

    /// <summary>
    /// Refreshes prompt labels, bonus text, and button visibility based on readiness and claim status.
    /// </summary>
    public void RefreshButton()
    {
        if (isAdInProgress) return;

        int bonus = ComputeBonus();

        // 1. Bind the customizable prompt text
        if (promptLabel != null)
        {
            promptLabel.text = adOfferText;
        }

        // 2. Bind the secondary bonus amount label
        if (bonusAmountLabel != null)
        {
            bonusAmountLabel.text = $"+{bonus} Coins!";
        }

        // 3. Update button label
        if (watchAdLabel != null)
        {
            watchAdLabel.text = $"Watch Ad for Double Coins (+{bonus})";
        }

        bool adReady = AdsManager.Instance != null && AdsManager.Instance.IsRewardedReady();
#if UNITY_EDITOR
        adReady = true;
#endif
        bool canOffer = !hasClaimedReward && bonus > 0 && adReady;

        if (watchAdButtonRoot != null) watchAdButtonRoot.SetActive(canOffer);
        else if (watchAdButton != null) watchAdButton.gameObject.SetActive(canOffer);

        if (watchAdButton != null) watchAdButton.interactable = canOffer && !isAdInProgress;
    }

    public void OnWatchAdClicked()
    {
        if (hasClaimedReward || isAdInProgress) return;
        TriggerAd();
    }

    private void TriggerAd()
    {
        isAdInProgress = true;
        if (watchAdButton != null) watchAdButton.interactable = false;

        if (AdsManager.Instance == null)
        {
            Debug.LogWarning("[GameOverAdOffer] AdsManager.Instance is missing.");
            OnAdShowComplete(AdCompletionState.Failed);
            return;
        }

        AdsManager.Instance.ShowRewardedAd(
            onRewardGranted: () => OnAdShowComplete(AdCompletionState.Completed),
            onFailedOrSkipped: () => OnAdShowComplete(AdCompletionState.Skipped)
        );
    }

    /// <summary>
    /// Grants the double coin reward. ONLY called upon full ad completion.
    /// </summary>
    public void ProcessReward()
    {
        int roundCoins = GetCoinsCollectedThisRound();
        GrantAdReward(roundCoins);
    }

    // =========================================================================
    // 4. >>> EXACT SDK COMPLETION CALLBACKS (STRICT ENFORCEMENT) <<<
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
            Debug.LogWarning($"[GameOverAdOffer] Unity Ads ({adUnitId}) SKIPPED. Player closed ad early. NO REWARD GIVEN.");
            TriggerWarningPopup(adSkippedWarningText);
            RefreshButton();
        }
        else // UNKNOWN, NOT_DEFINED, or failed
        {
            Debug.LogError($"[GameOverAdOffer] Unity Ads ({adUnitId}) ended with state: {showCompletionState}. NO REWARD GIVEN.");
            TriggerWarningPopup("Ad did not complete. You won't get the reward.");
            RefreshButton();
        }
    }

    /// <summary>
    /// Universal Ad SDK completion evaluation callback (LevelPlay, AdMob, AppLovin, Bridge, etc.).
    /// </summary>
    public void OnAdShowComplete(AdCompletionState state)
    {
        Debug.Log("Ad state: " + state);
        isAdInProgress = false;

        if (state == AdCompletionState.Completed)
        {
            Debug.Log("Reward Granted!");
            ProcessReward();
        }
        else if (state == AdCompletionState.Skipped || state == AdCompletionState.Canceled)
        {
            Debug.LogWarning("[GameOverAdOffer] SDK Callback: Ad was closed or skipped early. Triggering warning popup. NO REWARD GIVEN.");
            TriggerWarningPopup(adSkippedWarningText);
            RefreshButton();
        }
        else if (state == AdCompletionState.Failed)
        {
            Debug.LogError("[GameOverAdOffer] SDK Callback: Ad failed to stream. Triggering warning popup. NO REWARD GIVEN.");
            TriggerWarningPopup("Ad failed to load. You won't get the reward.");
            RefreshButton();
        }
        else
        {
            Debug.LogWarning($"[GameOverAdOffer] SDK Callback: Unknown state ({state}). NO REWARD GIVEN.");
            TriggerWarningPopup(adSkippedWarningText);
            RefreshButton();
        }
    }

    /// <summary>
    /// Legacy wrapper for backward compatibility.
    /// </summary>
    public void OnAdWatchedSuccessfully()
    {
        GrantAdReward(GetCoinsCollectedThisRound());
    }

    /// <summary>
    /// Strict reward calculation (+100% round match) and anti-duplicate enforcement.
    /// </summary>
    public void GrantAdReward(int coinsCollected)
    {
        if (hasClaimedReward)
        {
            Debug.LogWarning("[GameOverAdOffer] Reward already claimed for this round. Blocking duplicate grant.");
            return;
        }

        hasClaimedReward = true;
        isAdInProgress = false;

        int bonusCoins = coinsCollected;

        // 1. Grant the bonus coins (+100%) to persistent wallet
        if (bonusCoins > 0)
        {
            Wallet.Add(bonusCoins);
            Debug.Log($"[GameOverAdOffer] SUCCESS: Granted {bonusCoins} bonus coins (Double Coins) to Wallet. Total: {Wallet.Total}");
        }

        // 2. Hide and disable watch ad button
        if (watchAdButtonRoot != null) watchAdButtonRoot.SetActive(false);
        else if (watchAdButton != null) watchAdButton.gameObject.SetActive(false);

        if (watchAdButton != null) watchAdButton.interactable = false;

        // 3. Display the celebration Reward Popup
        if (rewardPopup == null)
        {
            rewardPopup = FindAnyObjectByType<RewardPopup>(FindObjectsInactive.Include);
        }

        if (rewardPopup != null)
        {
            rewardPopup.gameObject.SetActive(true);
            rewardPopup.Show(
                title: "DOUBLE COINS!",
                message: "You watched the entire ad and doubled your coins for this round!",
                amountString: $"+{bonusCoins} COINS",
                onClosed: () => RefreshButton()
            );
        }
        else
        {
            RefreshButton();
        }
    }

    /// <summary>
    /// Triggers the warning popup when the player closes or skips the ad early.
    /// </summary>
    public void TriggerWarningPopup(string message)
    {
        if (warningPopup == null)
        {
            warningPopup = FindAnyObjectByType<AdWarningPopup>(FindObjectsInactive.Include);
        }

        if (warningPopup != null)
        {
            warningPopup.gameObject.SetActive(true);
            warningPopup.Show(message, () => RefreshButton());
            return;
        }

        if (warningPopupPanel != null)
        {
            if (warningPopupText != null) warningPopupText.text = message;
            warningPopupPanel.SetActive(true);
            warningPopupPanel.transform.SetAsLastSibling();
            return;
        }

        Debug.LogWarning($"[GameOverAdOffer] Warning Popup: '{message}' (No AdWarningPopup assigned).");
    }

    private void DismissDirectWarningPanel()
    {
        if (warningPopupPanel != null)
        {
            warningPopupPanel.SetActive(false);
        }
        RefreshButton();
    }
}

