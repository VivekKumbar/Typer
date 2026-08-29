using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Shared progress-bar piece: a non-interactable UI Slider (Min Value 0 / Max
// Value 1) + optional percent label. Owns its OWN smoothing -- callers never
// set the Slider's value directly, they just push a TARGET (SetTargetProgress01)
// whenever they learn a new one, and this steps the displayed value toward it
// every frame via Mathf.MoveTowards, capped at Max Fill Rate Per Second.
//
// That guarantees the bar can NEVER visibly jump in one frame, no matter how
// jumpy the underlying source is -- a real AsyncOperation.progress famously
// jumps from ~0 straight to 0.9 in very few frames for a small scene, and even
// a single inflated Time.deltaTime after a load hitch could otherwise cause a
// visible snap. Used by SplashScreen and MainMenu's loading screen alike, so
// there's exactly one place that knows how to draw "progress from 0 to 1".
public class LoadingBarUI : MonoBehaviour
{
    [Tooltip("The progress Slider. Forced to Min Value 0 / Max Value 1 / Interactable OFF in Awake, and Raycast Target is turned off on all its child Graphics (Background/Fill/Handle) -- it's a display only, never draggable, regardless of how it was authored.")]
    public Slider bar;
    [Tooltip("Optional 'NN%' label. Leave empty to skip.")]
    public TMP_Text percentText;

    [Tooltip("How fast the DISPLAYED value can move toward its target, in bar-units (0-1) per second. This is what makes the fill smooth: it caps any single-frame jump, e.g. 2 means it takes at least 0.5s to cross the whole bar even if the target snaps straight from 0 to 1.")]
    [Range(0.2f, 10f)] public float maxFillRatePerSecond = 1.5f;

    private float target;
    private float displayed;

    void Awake()
    {
        if (bar != null)
        {
            bar.minValue = 0f;
            bar.maxValue = 1f;
            bar.interactable = false; // display only, never draggable
            foreach (Graphic g in bar.GetComponentsInChildren<Graphic>(true))
                g.raycastTarget = false;
        }
        target = 0f;
        displayed = 0f;
        Apply();
    }

    void Update()
    {
        if (Mathf.Approximately(displayed, target)) return;
        displayed = Mathf.MoveTowards(displayed, target, maxFillRatePerSecond * Time.unscaledDeltaTime);
        Apply();
    }

    // Callers set where the bar SHOULD be (real async progress, elapsed-time
    // pacing, whatever) -- Update() above eases the DISPLAYED value toward it
    // every frame. Never sets bar.value directly, so it can never jump.
    public void SetTargetProgress01(float t) => target = Mathf.Clamp01(t);

    // Skips the smoothing and jumps straight to a value -- only for resetting
    // to empty at the very start of a fresh load (so it doesn't visibly
    // animate FROM whatever value a previous load left behind).
    public void SnapTo01(float t)
    {
        target = Mathf.Clamp01(t);
        displayed = target;
        Apply();
    }

    void Apply()
    {
        if (bar != null) bar.value = displayed;
        if (percentText != null) percentText.text = Mathf.RoundToInt(displayed * 100f) + "%";
    }
}
