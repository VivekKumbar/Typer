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
    [Tooltip("Optional LoadingBarUI for smooth filling like the loading bar.")]
    public LoadingBarUI loadingBar;

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

        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnUpgradeChanged += HandleUpgradeChanged;

        ConfigureBar();
        if (buttonLabel == null && transform.parent != null)
            buttonLabel = transform.parent.Find("TimeSinkLabel")?.GetComponent<TMP_Text>();

        // Gate purely on the manager's own IsReady flag + IsUnlocked — not on any slider
        // value comparison — so this can never desync from a different max.
        bool unlocked = ts.IsUnlocked;
        if (activateButton) activateButton.interactable = unlocked && !ts.IsActive && ts.IsReady;
        if (readyHighlight) readyHighlight.SetReady(unlocked && ts.IsReady);
        if (readyPulse) readyPulse.SetActive(unlocked && ts.IsReady);
        // Sync-now: reflect whichever the manager is currently doing, instead
        // of assuming a fresh 0 (same idiom ComboHUD/HUD use for restored state).
        float initialFrac = unlocked ? (ts.IsActive ? ts.RemainingFraction : (ts.chargeMax > 0f ? ts.Charge / ts.chargeMax : 0f)) : 0f;
        if (loadingBar != null) loadingBar.SnapTo01(initialFrac);
        else if (bar) bar.value = initialFrac;
        RefreshLabel();
    }

    void OnDestroy()
    {
        var ts = TimeSinkManager.Instance;
        if (ts != null)
        {
            ts.OnChargeChanged -= UpdateCharge;
            ts.OnChargeReady -= OnReady;
            ts.OnActivated -= OnActivated;
            ts.OnDurationChanged -= UpdateDuration;
            ts.OnEnded -= OnEnded;
        }

        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnUpgradeChanged -= HandleUpgradeChanged;
    }

    void HandleUpgradeChanged(UpgradeDefinition def, int level)
    {
        var ts = TimeSinkManager.Instance;
        if (ts != null && def == ts.slowMoUpgrade)
        {
            RefreshLabel();
            float fill = ts.IsUnlocked ? (ts.IsActive ? ts.RemainingFraction : (ts.chargeMax > 0f ? ts.Charge / ts.chargeMax : 0f)) : 0f;
            if (loadingBar != null) loadingBar.SetTargetProgress01(fill);
            else if (bar) bar.value = fill;
            if (ts.IsUnlocked && ts.IsReady)
                OnReady();
        }
    }

    // minValue=0 / maxValue=1 always — the bar only ever receives a
    // pre-normalized fraction, so this must never be chargeMax/duration.
    // Also re-asserts non-interactable + raycasting off, matching the
    // "can't be dragged" requirement even if a scene edit ever changes it.
    void ConfigureBar()
    {
        if (loadingBar == null && bar != null)
            loadingBar = bar.GetComponent<LoadingBarUI>();
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
        var ts = TimeSinkManager.Instance;
        bool unlocked = ts != null && ts.IsUnlocked;
        if (!unlocked)
        {
            if (loadingBar != null) loadingBar.SetTargetProgress01(0f);
            else if (bar) bar.value = 0f;
            if (readyHighlight) readyHighlight.SetReady(false);
            if (readyPulse) readyPulse.SetActive(false);
            if (activateButton) activateButton.interactable = false;
            RefreshLabel();
            return;
        }

        // Direct assignment or smooth easing — fill is already 0..1. Only ever
        // fires while not active, so this can't stomp an in-progress duration drain.
        if (loadingBar != null) loadingBar.SetTargetProgress01(fill);
        else if (bar) bar.value = fill;
        if (fill < 1f)
        {
            if (readyHighlight) readyHighlight.SetReady(false);
            if (readyPulse) readyPulse.SetActive(false);
            RefreshLabel();
        }
    }

    // The ONLY place that enables the button — fired exactly once, exactly
    // when TimeSinkManager.Charge reaches chargeMax.
    void OnReady()
    {
        var ts = TimeSinkManager.Instance;
        if (ts == null || !ts.IsUnlocked) return;

        if (activateButton) activateButton.interactable = true;
        if (readyHighlight) readyHighlight.SetReady(true);
        if (readyPulse) readyPulse.SetActive(true);
        SfxPlayer.PlayButtonClick();
        string abilityName = (ts.slowMoUpgrade != null && !string.IsNullOrEmpty(ts.slowMoUpgrade.displayName))
            ? ts.slowMoUpgrade.displayName
            : "Chronos";
        UIToast.ShowAt(activateButton != null ? activateButton.transform : transform, $"{abilityName} Ready! Tap to use!", Color.cyan);
        RefreshLabel();
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
        // Direct assignment or smooth easing — remaining01 is already 0..1. Only
        // ever fires while active, so this can't stomp mid-charge progress.
        if (loadingBar != null) loadingBar.SetTargetProgress01(remaining01);
        else if (bar) bar.value = remaining01;
    }

    void OnEnded()
    {
        // Explicit, not just inherited from OnActivated: disabled after the
        // effect ends until the next full charge fires OnReady again.
        var ts = TimeSinkManager.Instance;
        bool unlocked = ts != null && ts.IsUnlocked;
        if (activateButton) activateButton.interactable = unlocked;
        if (readyHighlight) readyHighlight.SetReady(false);
        if (readyPulse) readyPulse.SetActive(false);
        RefreshLabel();
    }

    void RefreshLabel()
    {
        if (!buttonLabel) return;
        var ts = TimeSinkManager.Instance;
        string abilityUpper = (ts != null && ts.slowMoUpgrade != null && !string.IsNullOrEmpty(ts.slowMoUpgrade.displayName))
            ? ts.slowMoUpgrade.displayName.ToUpperInvariant()
            : "CHRONOS";

        if (ts != null && ts.IsActive)
            buttonLabel.text = $"{abilityUpper} ACTIVE";
        else if (ts != null && ts.IsUnlocked && ts.IsReady)
            buttonLabel.text = $"{abilityUpper} READY!";
        else
            buttonLabel.text = abilityUpper;
    }
}
