using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Drives the two Time Sink bars + button. Mirrors ComboHUD's shape:
// a bottom charge meter (like the Overload bar) plus a top duration bar that
// only shows while the slow is active.
//
// Both sliders receive an already-normalized 0..1 fraction from TimeSinkManager
// (Charge/chargeMax, RemainingFraction) — NEVER the raw charge/duration values.
// minValue/maxValue are therefore always 0/1 here, enforced defensively in both
// Awake and Start, and never set to chargeMax or duration anywhere in this file.
public class TimeSinkHUD : MonoBehaviour
{
    [Header("Top: remaining duration (hidden while inactive)")]
    public GameObject durationBarRoot;
    public Slider durationBar;

    [Header("Bottom: charge meter + button")]
    public Slider chargeBar;
    public Button activateButton;
    public TMP_Text buttonLabel;
    public ReadyStateHighlight readyHighlight;
    public ReadyPulse readyPulse;

    void Awake()
    {
        ConfigureBars();
    }

    void Start()
    {
        var ts = TimeSinkManager.Instance;
        if (ts == null) return;

        ts.OnChargeChanged += UpdateCharge;
        ts.OnChargeReady += OnReady;
        ts.OnActivated += OnActivated;
        ts.OnDurationChanged += UpdateDuration;
        ts.OnEnded += OnEnded;

        ConfigureBars();
        // Gate purely on the manager's own IsReady flag — not on any slider
        // value comparison — so this can never desync from a different max.
        if (activateButton) activateButton.interactable = ts.IsReady;
        if (readyHighlight) readyHighlight.SetReady(ts.IsReady);
        if (readyPulse) readyPulse.SetActive(ts.IsReady);
        if (durationBarRoot) durationBarRoot.SetActive(ts.IsActive);
        RefreshLabel();
    }

    void OnDestroy()
    {
        var ts = TimeSinkManager.Instance;
        if (ts == null) return;
        ts.OnChargeChanged -= UpdateCharge;
        ts.OnChargeReady -= OnReady;
        ts.OnActivated -= OnActivated;
        ts.OnDurationChanged -= UpdateDuration;
        ts.OnEnded -= OnEnded;
    }

    // minValue=0 / maxValue=1 always — both sliders only ever receive
    // pre-normalized fractions, so this must never be chargeMax/duration.
    // Also re-asserts non-interactable + raycasting off, matching the
    // "can't be dragged" requirement even if a scene edit ever changes it.
    void ConfigureBars()
    {
        if (chargeBar)
        {
            chargeBar.minValue = 0f; chargeBar.maxValue = 1f;
            chargeBar.wholeNumbers = false;
            chargeBar.interactable = false;
            SetRaycastTarget(chargeBar, false);
        }
        if (durationBar)
        {
            durationBar.minValue = 0f; durationBar.maxValue = 1f;
            durationBar.wholeNumbers = false;
            durationBar.interactable = false;
            SetRaycastTarget(durationBar, false);
        }
    }

    static void SetRaycastTarget(Slider slider, bool value)
    {
        foreach (var g in slider.GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = value;
    }

    void UpdateCharge(float fill)
    {
        // Direct assignment, no rescaling — fill is already 0..1.
        if (chargeBar) chargeBar.value = fill;
    }

    // The ONLY place that enables the button — fired exactly once, exactly
    // when TimeSinkManager.Charge reaches chargeMax.
    void OnReady()
    {
        if (activateButton) activateButton.interactable = true;
        if (readyHighlight) readyHighlight.SetReady(true);
        if (readyPulse) readyPulse.SetActive(true);
    }

    void OnActivated()
    {
        if (activateButton) activateButton.interactable = false;
        if (readyHighlight) readyHighlight.SetReady(false);
        if (readyPulse) readyPulse.SetActive(false);
        if (durationBarRoot) durationBarRoot.SetActive(true);
        RefreshLabel();
    }

    void UpdateDuration(float remaining01)
    {
        // Direct assignment, no rescaling — remaining01 is already 0..1.
        if (durationBar) durationBar.value = remaining01;
    }

    void OnEnded()
    {
        if (durationBarRoot) durationBarRoot.SetActive(false);
        // Explicit, not just inherited from OnActivated: disabled after the
        // effect ends until the next full charge fires OnReady again.
        if (activateButton) activateButton.interactable = false;
        if (readyHighlight) readyHighlight.SetReady(false);
        if (readyPulse) readyPulse.SetActive(false);
        RefreshLabel();
    }

    void RefreshLabel()
    {
        if (!buttonLabel) return;
        var ts = TimeSinkManager.Instance;
        buttonLabel.text = (ts != null && ts.IsActive) ? "TIME SINK ACTIVE" : "TIME SINK";
    }
}
