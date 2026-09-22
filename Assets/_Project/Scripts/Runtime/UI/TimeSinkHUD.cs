using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Drives Time Sink's HUD: ONE bar + the activate button. Originally this
// mirrored ComboHUD with TWO bars (a bottom charge meter plus a top duration
// bar shown only while active) — that pair sat in completely different
// screen positions (see TimeSinkUIBuilder's old layout) and read as broken/
// confusing rather than as one ability. Consolidated to a single bar that
// SWITCHES what it represents with TimeSinkManager's own state: charge
// progress (0->1) while charging, remaining duration (1->0) while active.
//
// This works with no IsActive/IsReady branching in the update path: the two
// source events never overlap. TimeSinkManager only fires OnChargeChanged
// while NOT active (AddCharge/DebugFillCharge, plus the reset-to-0 at the
// start of Activate) and only fires OnDurationChanged while IsActive (every
// Update() tick, plus once at Activate() and once at EndEffect()). So both
// handlers can just assign bar.value directly — whichever one TimeSinkManager
// is currently sending is, by construction, the one that's meaningful.
//
// The bar receives an already-normalized 0..1 fraction from TimeSinkManager
// (Charge/chargeMax, RemainingFraction) — NEVER the raw charge/duration
// values. minValue/maxValue are therefore always 0/1 here, enforced
// defensively in both Awake and Start, and never set to chargeMax or
// duration anywhere in this file.
public class TimeSinkHUD : MonoBehaviour
{
    [Header("Single ability bar -- charge while charging, remaining duration while active")]
    public Slider bar;

    [Header("Button")]
    public Button activateButton;
    public TMP_Text buttonLabel;
    public ReadyStateHighlight readyHighlight;
    public ReadyPulse readyPulse;

    void Awake()
    {
        ConfigureBar();
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

        ConfigureBar();
        // Gate purely on the manager's own IsReady flag — not on any slider
        // value comparison — so this can never desync from a different max.
        if (activateButton) activateButton.interactable = ts.IsReady;
        if (readyHighlight) readyHighlight.SetReady(ts.IsReady);
        if (readyPulse) readyPulse.SetActive(ts.IsReady);
        // Sync-now: reflect whichever the manager is currently doing, instead
        // of assuming a fresh 0 (same idiom ComboHUD/HUD use for restored state).
        if (bar) bar.value = ts.IsActive ? ts.RemainingFraction : (ts.chargeMax > 0f ? ts.Charge / ts.chargeMax : 0f);
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

    // minValue=0 / maxValue=1 always — the bar only ever receives a
    // pre-normalized fraction, so this must never be chargeMax/duration.
    // Also re-asserts non-interactable + raycasting off, matching the
    // "can't be dragged" requirement even if a scene edit ever changes it.
    void ConfigureBar()
    {
        if (!bar) return;
        bar.minValue = 0f; bar.maxValue = 1f;
        bar.wholeNumbers = false;
        bar.interactable = false;
        SetRaycastTarget(bar, false);
    }

    static void SetRaycastTarget(Slider slider, bool value)
    {
        foreach (var g in slider.GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = value;
    }

    void UpdateCharge(float fill)
    {
        // Direct assignment, no rescaling — fill is already 0..1. Only ever
        // fires while not active, so this can't stomp an in-progress duration drain.
        if (bar) bar.value = fill;
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
        RefreshLabel();
    }

    void UpdateDuration(float remaining01)
    {
        // Direct assignment, no rescaling — remaining01 is already 0..1. Only
        // ever fires while active, so this can't stomp mid-charge progress.
        if (bar) bar.value = remaining01;
    }

    void OnEnded()
    {
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
