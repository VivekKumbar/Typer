using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Dedicated UI Popup displayed when a player skips, cancels, or closes a rewarded ad prematurely.
/// Displays the warning message and requires clicking the "Okay" button to dismiss.
/// </summary>
public class AdWarningPopup : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Root panel GameObject of the warning popup.")]
    [SerializeField] private GameObject panel;

    [Tooltip("TextMeshProUGUI element displaying the warning message.")]
    [SerializeField] private TextMeshProUGUI warningMessageText;

    [Tooltip("The 'Okay' / close button to dismiss the warning.")]
    [SerializeField] private Button okayButton;

    [Tooltip("Optional CanvasGroup for smooth unscaled fade-in animation.")]
    [SerializeField] private CanvasGroup canvasGroup;

    private Action onDismissCallback;
    private Coroutine animationCoroutine;

    private void Awake()
    {
        if (okayButton != null)
        {
            okayButton.onClick.RemoveListener(Close);
            okayButton.onClick.AddListener(Close);
        }
    }

    /// <summary>
    /// Displays the warning popup with custom warning text and an optional dismissal callback.
    /// </summary>
    /// <param name="message">The text to display (e.g. 'You closed the ad early. You won't get the reward.').</param>
    /// <param name="onDismiss">Callback triggered when the player clicks 'Okay'.</param>
    public void Show(string message, Action onDismiss = null)
    {
        onDismissCallback = onDismiss;

        if (warningMessageText != null)
        {
            warningMessageText.text = message;
        }

        // Activate root GameObject & panel
        gameObject.SetActive(true);
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

        if (okayButton != null)
        {
            okayButton.onClick.RemoveListener(Close);
            okayButton.onClick.AddListener(Close);
        }

        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }

        if (gameObject.activeInHierarchy)
        {
            animationCoroutine = StartCoroutine(AnimateShow());
        }
    }

    private IEnumerator AnimateShow()
    {
        Transform target = panel != null ? panel.transform : transform;
        target.localScale = Vector3.one * 0.8f;

        float elapsed = 0f;
        float duration = 0.2f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // Unscaled so it animates while Time.timeScale == 0
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = 1f + 0.2f * Mathf.Sin(t * Mathf.PI);
            target.localScale = Vector3.Lerp(Vector3.one * 0.8f, Vector3.one, t) * (t < 1f ? ease : 1f);
            yield return null;
        }

        target.localScale = Vector3.one;
        animationCoroutine = null;
    }

    /// <summary>
    /// Dismisses and hides the popup.
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

        Action cb = onDismissCallback;
        onDismissCallback = null;
        cb?.Invoke();
    }
}
