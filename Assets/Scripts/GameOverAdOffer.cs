using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Put on (or as a child within) HUD's gameOverPanel. Offers a rewarded-ad
// coin bonus once the run has ended. Kept as its own file/component rather
// than folded into HUD.cs so the ad-reward concern stays isolated, same
// spirit as AdsManager itself hiding all LevelPlay specifics.
//
// Coin bonus was chosen over a "continue this run" revive as the default --
// simpler and lower risk to game economy (a revive would need to re-arm
// GameManager.IsGameOver, un-freeze Time.timeScale, and decide what state the
// board/wave resumes in -- a bigger design call). To wire a revive
// instead/in addition later: swap OnWatchAdClicked's onRewardGranted branch
// to reset GameManager.IsGameOver, restore some health via
// GameManager.HealFortress, and resume Time.timeScale, instead of/alongside
// the coin grant below.
public class GameOverAdOffer : MonoBehaviour
{
    [Header("Reward")]
    [Tooltip("The coin bonus is this fraction of what was earned THIS RUN (GameManager.coinsEarnedThisRun), granted via Wallet.Add on top -- the run's own earnings are already banked by the time Game Over shows (GameManager.BankEarnings runs before OnGameOver fires), so this is purely additive, never a replacement.")]
    [Range(0f, 2f)] public float bonusFraction = 0.5f;

    [Header("UI")]
    [Tooltip("The whole 'Watch Ad for +50% Coins' button root -- hidden if no rewarded ad is ready when Game Over shows, so the player never taps a dead button.")]
    public GameObject watchAdButtonRoot;
    public Button watchAdButton;
    [Tooltip("Optional label -- set to the actual bonus fraction/amount once GameManager's earnings are known.")]
    public TMP_Text watchAdLabel;

    private bool rewardClaimed;

    void Start()
    {
        if (watchAdButton != null) watchAdButton.onClick.AddListener(OnWatchAdClicked);
    }

    // OnEnable, not Start: this panel starts inactive and Start() only ever
    // runs once (the first time it's activated) -- the "is a rewarded ad
    // ready right now" check has to re-run EVERY time Game Over is shown,
    // which only OnEnable does correctly here.
    void OnEnable()
    {
        rewardClaimed = false;
        RefreshButton();
    }

    void RefreshButton()
    {
        int bonus = ComputeBonus();
        if (watchAdLabel != null)
            watchAdLabel.text = "Watch Ad for +" + Mathf.RoundToInt(bonusFraction * 100f) + "% Coins (" + bonus + ")";

        bool canOffer = !rewardClaimed && bonus > 0 && AdsManager.Instance != null && AdsManager.Instance.IsRewardedReady();
        if (watchAdButtonRoot != null) watchAdButtonRoot.SetActive(canOffer);
        else if (watchAdButton != null) watchAdButton.gameObject.SetActive(canOffer);
    }

    int ComputeBonus()
    {
        GameManager gm = GameManager.Instance;
        return gm != null ? Mathf.RoundToInt(gm.coinsEarnedThisRun * bonusFraction) : 0;
    }

    void OnWatchAdClicked()
    {
        if (rewardClaimed || AdsManager.Instance == null) return;

        AdsManager.Instance.ShowRewardedAd(
            onRewardGranted: () =>
            {
                if (rewardClaimed) return; // guard a double-fire
                rewardClaimed = true;
                int bonus = ComputeBonus();
                if (bonus > 0) Wallet.Add(bonus);
                RefreshButton(); // hides the button now that the reward's claimed
            },
            onFailedOrSkipped: () =>
            {
                // No partial reward -- just re-check readiness in case the SDK already started loading the next ad.
                RefreshButton();
            });
    }
}
