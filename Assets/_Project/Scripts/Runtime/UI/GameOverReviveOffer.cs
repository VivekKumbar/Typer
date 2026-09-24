using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attached to GameOverPanel or the ContinueButton.
/// Allows the player to watch a rewarded ad to continue the run from the current wave
/// with full fortress health and all abilities/upgrades preserved, indefinitely.
/// </summary>
[DisallowMultipleComponent]
public class GameOverReviveOffer : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The button player clicks to watch an ad and continue the run.")]
    public Button continueButton;

    [Tooltip("Optional text label on the button.")]
    public TMP_Text continueLabel;

    private bool isWatchingAd = false;

    void Awake()
    {
        if (continueButton == null)
            continueButton = GetComponent<Button>();

        if (continueLabel == null && continueButton != null)
            continueLabel = continueButton.GetComponentInChildren<TMP_Text>();
    }

    void Start()
    {
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueClicked);
        }
    }

    void OnEnable()
    {
        isWatchingAd = false;
        RefreshButton();
    }

    /// <summary>
    /// Refreshes button interactability. Available indefinitely on every Game Over.
    /// </summary>
    public void RefreshButton()
    {
        if (continueButton != null)
        {
            bool adReady = PlayGamaAds.Instance != null && PlayGamaAds.Instance.IsRewardedSupported();
            continueButton.interactable = !isWatchingAd && adReady;
        }

        if (continueLabel != null)
        {
            continueLabel.text = "CONTINUE (Watch Ad)";
        }
    }

    public void OnContinueClicked()
    {
        if (isWatchingAd) return;

        isWatchingAd = true;
        if (continueButton != null) continueButton.interactable = false;
        if (continueLabel != null) continueLabel.text = "Loading Ad...";

        Debug.Log("[GameOverReviveOffer] Requesting Rewarded Ad to revive and continue from current wave...");
        if (PlayGamaAds.Instance != null)
        {
            PlayGamaAds.Instance.ShowRewarded(success =>
            {
                isWatchingAd = false;
                if (success)
                {
                    HandleReviveSuccess();
                }
                else
                {
                    HandleReviveFailed();
                }
            }, "Watch Ad to Continue & Revive");
        }
        else
        {
            Debug.LogWarning("[GameOverReviveOffer] PlayGamaAds.Instance is null! Failing revive ad.");
            isWatchingAd = false;
            HandleReviveFailed();
        }
    }

    private void HandleReviveSuccess()
    {
        Debug.Log("[GameOverReviveOffer] Rewarded ad watched! Continuing game from current wave with full health and all abilities...");
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.ReviveCurrentWave();
        }
        else if (GameManager.Instance != null)
        {
            GameManager.Instance.ReviveFortress();
            Time.timeScale = 1f;
        }
    }

    private void HandleReviveFailed()
    {
        Debug.LogWarning("[GameOverReviveOffer] Ad was closed early, skipped, or failed. Revive not granted.");
        RefreshButton();
    }
}
