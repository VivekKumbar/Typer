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
    public float showTime = 1.6f;

    private Coroutine co;

    private bool layoutWarmed;

    void Awake()
    {
        if (text != null)
        {
            text.raycastTarget = false;
        }

        if (panelRoot != null)
        {
            var cg = panelRoot.GetComponent<CanvasGroup>();
            if (cg == null) cg = panelRoot.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

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
        yield return new WaitForSeconds(showTime);
        SetVisible(false);
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