using System.Collections;
using UnityEngine;
using TMPro;

// Shows a big centered message (e.g. "WAVE 2") for a moment.
// Put this on an ALWAYS-ACTIVE object (like the Canvas) and assign a TMP text
// child that it will show/hide.
public class WaveBanner : MonoBehaviour
{
    public TMP_Text text;
    [Tooltip("Optional backdrop panel, shown/hidden together with the text instead of toggling the text's own GameObject. Without this, both the 'WAVE n' announce AND the 5-4-3-2-1 countdown render as bare text with nothing behind them -- fine over the ground, but they sit dead-center screen, right over the fortress/shield bubble, and floated with no backing read as a stray number. Falls back to toggling the text's own GameObject if left unassigned (old behavior).")]
    public GameObject panelRoot;
    public float showTime = 2.0f;

    private Coroutine co;
    private CanvasGroup cachedCanvasGroup;
    private Transform animTarget;

    private bool layoutWarmed;

    void Awake()
    {
        if (text != null)
        {
            text.raycastTarget = false;
        }

        GameObject root = panelRoot != null ? panelRoot : (text != null ? text.gameObject : gameObject);
        animTarget = root.transform;

        cachedCanvasGroup = root.GetComponent<CanvasGroup>();
        if (cachedCanvasGroup == null) cachedCanvasGroup = root.AddComponent<CanvasGroup>();
        cachedCanvasGroup.blocksRaycasts = false;
        cachedCanvasGroup.interactable = false;

        if (panelRoot != null)
        {
            var graphics = panelRoot.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            foreach (var g in graphics) g.raycastTarget = false;
        }

        SetVisible(false);

        if (text != null)
        {
            bool wasActive = (panelRoot != null ? panelRoot : text.gameObject).activeSelf;
            (panelRoot != null ? panelRoot : text.gameObject).SetActive(true);
            Canvas.ForceUpdateCanvases();
            text.ForceMeshUpdate(true, true);
            (panelRoot != null ? panelRoot : text.gameObject).SetActive(wasActive);
            layoutWarmed = true;
        }
    }

    public void Show(string message)
    {
        if (text == null) return;
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(ShowRoutine(message));
    }

    IEnumerator ShowRoutine(string message)
    {
        text.text = message;
        SetVisible(true);
        WarmLayout();

        if (cachedCanvasGroup == null && animTarget != null)
            cachedCanvasGroup = animTarget.GetComponent<CanvasGroup>();

        float fadeInTime = 0.25f;
        float holdTime = Mathf.Max(0.5f, showTime - 0.7f); // holds prominently
        float fadeOutTime = 0.45f;

        // Fade in + scale up
        float elapsed = 0f;
        while (elapsed < fadeInTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeInTime);
            if (cachedCanvasGroup != null) cachedCanvasGroup.alpha = t;
            if (animTarget != null) animTarget.localScale = Vector3.Lerp(Vector3.one * 0.85f, Vector3.one, t);
            yield return null;
        }

        if (cachedCanvasGroup != null) cachedCanvasGroup.alpha = 1f;
        if (animTarget != null) animTarget.localScale = Vector3.one;

        // Hold prominently
        elapsed = 0f;
        while (elapsed < holdTime)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Fade out
        elapsed = 0f;
        while (elapsed < fadeOutTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeOutTime);
            if (cachedCanvasGroup != null) cachedCanvasGroup.alpha = 1f - t;
            if (animTarget != null) animTarget.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.95f, t);
            yield return null;
        }

        SetVisible(false);
        if (cachedCanvasGroup != null) cachedCanvasGroup.alpha = 1f;
        if (animTarget != null) animTarget.localScale = Vector3.one;
        co = null;
    }

    // Sets the banner text and keeps it up with NO auto-hide timer -- used by
    // WaveManager's countdown, which drives the show/hide timing itself
    // (frame-by-frame, so it can bail early on GameOver). Cancels any pending
    // auto-hide from Show() so a countdown started right after a Show() call
    // doesn't get hidden mid-count.
    public void ShowRaw(string message)
    {
        if (text == null) return;
        if (co != null) { StopCoroutine(co); co = null; }
        text.text = message;
        if (cachedCanvasGroup != null) cachedCanvasGroup.alpha = 1f;
        if (animTarget != null) animTarget.localScale = Vector3.one;
        SetVisible(true);
        text.ForceMeshUpdate(false, false);
    }

    void WarmLayout()
    {
        if (!layoutWarmed)
        {
            Canvas.ForceUpdateCanvases();
            layoutWarmed = true;
        }
        text.ForceMeshUpdate(true, true);
    }

    public void Hide()
    {
        if (co != null) { StopCoroutine(co); co = null; }
        if (text != null) SetVisible(false);
    }

    void SetVisible(bool visible)
    {
        if (text == null) return;
        (panelRoot != null ? panelRoot : text.gameObject).SetActive(visible);
    }
}