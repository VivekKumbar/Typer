using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attached to the GameOverPanel. Handles the rewarded ad offer at the Game Over screen,
/// including countdown timing (unscaled time safe), ad triggering via AdsManager,
/// dynamic coin reward calculation, and launching the RewardPopup celebration UI.
/// </summary>
[DisallowMultipleComponent]
public class GameOverAdOffer : MonoBehaviour
{
    [Header("1. Dynamic Reward Configuration")]
    [Tooltip("The coin bonus fraction granted from this run's earnings (e.g. 0.5 for +50%, 1.0 for +100%). Editable in Unity Inspector.")]
    [Range(0f, 5f)] public float bonusFraction = 0.5f;

    [Tooltip("Minimum coin bonus granted if no coins were earned during the run (e.g. dying on wave 1).")]
    public int minimumBonusCoins = 50;

    [Header("2. Countdown / Ad Trigger Timer")]
    [Tooltip("Seconds to count down before triggering the ad (0 = show ad immediately on click). Uses unscaled time so it works while Game Over freezes Time.timeScale.")]
    public float countdownSeconds = 0f;

    [Header("3. UI References")]
    [Tooltip("Root GameObject of the Watch Ad button.")]
    public GameObject watchAdButtonRoot;
    [Tooltip("Button component the player clicks to watch the ad.")]
    public Button watchAdButton;
    [Tooltip("Text label on the button.")]
    public TMP_Text watchAdLabel;

    [Header("4. Reward Popup")]
    [Tooltip("Reference to the RewardPopup UI component that displays the reward message upon watching the ad.")]
    public RewardPopup rewardPopup;

    private bool rewardClaimed;
    private bool isAdLoadingOrCountingDown;
    private Coroutine countdownCoroutine;

    void Awake()
    {
        if (rewardPopup == null)
        {
            rewardPopup = FindAnyObjectByType<RewardPopup>(FindObjectsInactive.Include);
        }
    }

    void Start()
    {
        if (watchAdButton != null)
        {
            watchAdButton.onClick.AddListener(OnWatchAdClicked);
        }
    }

    void OnEnable()
    {
        if (rewardPopup == null)
        {
            rewardPopup = FindAnyObjectByType<RewardPopup>(FindObjectsInactive.Include);
        }
        rewardClaimed = false;
        isAdLoadingOrCountingDown = false;
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }
        RefreshButton();
    }

    /// <summary>
    /// Computes the exact coin bonus amount based on this run's earnings and bonusFraction.
    /// </summary>
    public int ComputeBonus()
    {
        GameManager gm = GameManager.Instance;
        int runCoins = gm != null ? gm.coinsEarnedThisRun : 0;
        int calculated = Mathf.RoundToInt(runCoins * bonusFraction);
        return Mathf.Max(calculated, minimumBonusCoins);
    }

    /// <summary>
    /// Refreshes button label and visibility based on readiness and claim status.
    /// </summary>
    public void RefreshButton()
    {
        if (isAdLoadingOrCountingDown) return;

        int bonus = ComputeBonus();
        int percent = Mathf.RoundToInt(bonusFraction * 100f);

        if (watchAdLabel != null)
        {
            watchAdLabel.text = $"Watch Ad for +{percent}% Coins (+{bonus})";
        }

        // No ad SDK integrated yet on this branch (WebGL-first pass, ads come
        // back before publishing) -- forced false keeps the button hidden.
        // Re-wire this to the eventual ad SDK's "rewarded ad ready" check.
        bool adReady = false;
        bool canOffer = !rewardClaimed && bonus > 0 && adReady;

        if (watchAdButtonRoot != null) watchAdButtonRoot.SetActive(canOffer);
        else if (watchAdButton != null) watchAdButton.gameObject.SetActive(canOffer);

        if (watchAdButton != null) watchAdButton.interactable = canOffer;
    }

    public void OnWatchAdClicked()
    {
        if (rewardClaimed || isAdLoadingOrCountingDown) return;

        if (countdownSeconds > 0f)
        {
            countdownCoroutine = StartCoroutine(CountdownAndTriggerAd());
        }
        else
        {
            TriggerAd();
        }
    }

    /// <summary>
    /// Unscaled countdown coroutine ensuring timers work while Time.timeScale == 0 at Game Over.
    /// </summary>
    private IEnumerator CountdownAndTriggerAd()
    {
        isAdLoadingOrCountingDown = true;
        if (watchAdButton != null) watchAdButton.interactable = false;

        float remaining = countdownSeconds;
        while (remaining > 0f)
        {
            if (watchAdLabel != null)
            {
                watchAdLabel.text = $"Loading ad in {Mathf.CeilToInt(remaining)}s...";
            }
            // CRITICAL: Must use unscaledDeltaTime because GameManager freezes Time.timeScale to 0 on Game Over
            yield return new WaitForSecondsRealtime(1f);
            remaining -= 1f;
        }

        if (watchAdLabel != null)
        {
            watchAdLabel.text = "Loading ad...";
        }

        TriggerAd();
        countdownCoroutine = null;
    }

    // Not currently reachable (RefreshButton() keeps the button hidden via
    // adReady = false above), but kept intact so re-wiring is small: swap the
    // log line below for a real ad SDK call into HandleAdRewarded/HandleAdFailed.
    private void TriggerAd()
    {
        isAdLoadingOrCountingDown = true;
        if (watchAdButton != null) watchAdButton.interactable = false;

        Debug.Log("[GameOverAdOffer] Watch Ad tapped, but no ad SDK is integrated yet for this build.");
        HandleAdFailed();
    }

    private void HandleAdRewarded()
    {
        if (rewardClaimed) return;
        rewardClaimed = true;
        isAdLoadingOrCountingDown = false;

        int bonus = ComputeBonus();
        int percent = Mathf.RoundToInt(bonusFraction * 100f);

        // 1. Grant the coins to persistent wallet
        if (bonus > 0)
        {
            Wallet.Add(bonus);
            Debug.Log($"[GameOverAdOffer] Granted {bonus} bonus coins to Wallet.");
        }

        // 2. Hide watch ad button
        if (watchAdButtonRoot != null) watchAdButtonRoot.SetActive(false);
        else if (watchAdButton != null) watchAdButton.gameObject.SetActive(false);

        // 3. Display the Dynamic Reward Popup
        if (rewardPopup == null)
        {
            rewardPopup = FindAnyObjectByType<RewardPopup>(FindObjectsInactive.Include);
        }

        if (rewardPopup != null)
        {
            rewardPopup.ShowReward(bonus, percent, "REWARD CLAIMED!", () =>
            {
                RefreshButton();
            });
        }
        else
        {
            Debug.LogWarning("[GameOverAdOffer] RewardPopup reference is not assigned in the Inspector.");
            RefreshButton();
        }
    }

    private void HandleAdFailed()
    {
        isAdLoadingOrCountingDown = false;
        Debug.Log("[GameOverAdOffer] Rewarded ad was skipped or failed.");
        RefreshButton();
    }
}
