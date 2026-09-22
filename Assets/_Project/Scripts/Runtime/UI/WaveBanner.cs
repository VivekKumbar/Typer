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

    void Awake()
    {
        SetVisible(false);
        // Pre-warm TMP/Canvas layout for this text object before the first
        // REAL Show/ShowRaw call (typically "Wave 1", milliseconds after
        // scene load) -- otherwise that first call's auto-sizing can compute
        // against a Canvas that has never done a layout pass yet and pick a
        // font size that overflows the backdrop for a frame or two.
        if (text != null)
        {
            bool wasActive = (panelRoot != null ? panelRoot : text.gameObject).activeSelf;
            (panelRoot != null ? panelRoot : text.gameObject).SetActive(true);
            Canvas.ForceUpdateCanvases();
            text.ForceMeshUpdate(true, true);
            (panelRoot != null ? panelRoot : text.gameObject).SetActive(wasActive);
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
        WarmLayout();
    }

    // The very first Show/ShowRaw call (e.g. "Wave 1" at game start) activates
    // panelRoot from inactive in the same frame it sets the text -- Canvas
    // hasn't run a layout pass on the freshly-activated hierarchy yet, so
    // auto-sizing can compute against a stale/zero RectTransform.rect and pick
    // a font size that overflows the backdrop. Forcing the layout pass before
    // TMP recalculates its mesh fixes it without touching the autosize config.
    void WarmLayout()
    {
        Canvas.ForceUpdateCanvases();
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