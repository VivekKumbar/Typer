using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Dynamic UI Popup shown when a rewarded ad completes successfully.
/// Displays a customizable reward celebration message with the exact coin/bonus amount.
/// </summary>
public class RewardPopup : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The root panel GameObject of the popup.")]
    public GameObject panel;
    [Tooltip("Popup title text component (e.g. 'Reward Claimed!').")]
    public TMP_Text titleText;
    [Tooltip("Main message text component (e.g. 'You have been rewarded with +50% Coins!').")]
    public TMP_Text messageText;
    [Tooltip("Optional highlighted reward amount text component (e.g. '+250 Coins').")]
    public TMP_Text amountText;
    [Tooltip("Optional claim / OK / close button.")]
    public Button closeButton;
    [Tooltip("Optional CanvasGroup for smooth unscaled fade-in.")]
    public CanvasGroup canvasGroup;

    [Header("Animation")]
    [Tooltip("Duration of the unscaled fade/scale-in popup animation in seconds.")]
    public float animationDuration = 0.25f;

    private Action onClosedCallback;
    private Coroutine animationCoroutine;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }
    }

    /// <summary>
    /// Displays the reward popup with dynamically formatted reward amount and percentage.
    /// </summary>
    /// <param name="rewardAmount">Calculated coin reward.</param>
    /// <param name="percentage">Percentage bonus (e.g. 50 for 50%).</param>
    /// <param name="customTitle">Optional title override.</param>
    /// <param name="onClosed">Optional callback when player dismisses the popup.</param>
    public void ShowReward(int rewardAmount, int percentage, string customTitle = "REWARD CLAIMED!", Action onClosed = null)
    {
        string message = percentage > 0
            ? $"You have been rewarded with +{percentage}% Coins!"
            : $"You have been rewarded with {rewardAmount} Coins!";

        string amountStr = $"+{rewardAmount} COINS";

        Show(customTitle, message, amountStr, onClosed);
    }

    /// <summary>
    /// Displays the popup with explicit title, message, and optional amount string.
    /// </summary>
    public void Show(string title, string message, string amountString = "", Action onClosed = null)
    {
        onClosedCallback = onClosed;

        if (titleText != null) titleText.text = title;
        if (messageText != null) messageText.text = message;
        if (amountText != null)
        {
            amountText.text = amountString;
            amountText.gameObject.SetActive(!string.IsNullOrEmpty(amountString));
        }

        // 1. Ensure the RewardPopup GameObject is active
        gameObject.SetActive(true);

        // 2. Ensure the panel is active if assigned separately
        if (panel != null && panel != gameObject)
        {
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
        }

        transform.SetAsLastSibling();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }

        try
        {
            SfxPlayer.PlayGameStart(); // Celebratory rising stinger sound on reward popup show
        }
        catch (Exception) { }

        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }

        // 3. Start animation if active in hierarchy; otherwise apply visual state immediately
        if (gameObject.activeInHierarchy)
        {
            animationCoroutine = StartCoroutine(AnimateShow());
        }
        else
        {
            Transform targetTransform = panel != null ? panel.transform : transform;
            targetTransform.localScale = Vector3.one;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }
    }

    private IEnumerator AnimateShow()
    {
        Transform targetTransform = panel != null ? panel.transform : transform;
        targetTransform.localScale = Vector3.one * 0.7f;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime; // unscaled so it animates while game is paused
            float t = Mathf.Clamp01(elapsed / animationDuration);
            // Smooth ease out back curve for a satisfying pop
            float ease = 1f + 0.3f * Mathf.Sin(t * Mathf.PI);
            targetTransform.localScale = Vector3.Lerp(Vector3.one * 0.7f, Vector3.one, t) * (t < 1f ? ease : 1f);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            }
            yield return null;
        }

        targetTransform.localScale = Vector3.one;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
        animationCoroutine = null;
    }

    /// <summary>
    /// Closes and hides the popup.
    /// </summary>
    public void Close()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        if (panel != null && panel != gameObject)
        {
            panel.SetActive(false);
        }

        gameObject.SetActive(false);

        Action cb = onClosedCallback;
        onClosedCallback = null;
        cb?.Invoke();
    }
}
