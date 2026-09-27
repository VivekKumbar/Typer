using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ComboHUD : MonoBehaviour
{
    public TMP_Text comboText;
    public Slider overloadBar;
    [Tooltip("Optional LoadingBarUI for smooth filling like the loading bar.")]
    public LoadingBarUI overloadLoadingBar;
    public Button overloadButton;
    public TMP_Text overloadLabel;
    public ReadyStateHighlight overloadHighlight;
    public ReadyPulse overloadPulse;

    void Start()
    {
        var cm = ComboManager.Instance;
        if (cm == null) return;
        cm.OnComboChanged += UpdateCombo;
        cm.OnOverloadChanged += UpdateOverload;
        cm.OnOverloadReady += OnReady;

        if (overloadLoadingBar == null && overloadBar != null)
            overloadLoadingBar = overloadBar.GetComponent<LoadingBarUI>();

        if (overloadBar) { overloadBar.minValue = 0; overloadBar.maxValue = 1; }

        if (overloadLabel == null && transform.parent != null)
            overloadLabel = transform.parent.Find("OverloadLabel")?.GetComponent<TMP_Text>();

        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnUpgradeChanged += HandleUpgradeChanged;

        // Pull current values right now instead of assuming a fresh 0/1f
        // start — on a Continue, ComboManager may already hold a restored
        // combo/overload from before, and this makes the HUD reflect it
        // immediately regardless of script execution order (same idiom HUD.cs
        // uses for health).
        UpdateCombo(cm.combo, ComboManager.Multiplier);
        bool isUnlocked = cm.IsOverloadUnlocked;
        float fill = isUnlocked ? cm.OverloadFill : 0f;
        if (overloadLoadingBar != null) overloadLoadingBar.SnapTo01(fill);
        UpdateOverload(fill);
        if (isUnlocked && cm.overloadReady) OnReady();
        else
        {
            if (overloadButton) overloadButton.interactable = false;
            if (overloadHighlight) overloadHighlight.SetReady(false);
            if (overloadPulse) overloadPulse.SetActive(false);
            if (overloadLabel) overloadLabel.text = "OVERLOAD";
        }
    }

    void OnDestroy()
    {
        var cm = ComboManager.Instance;
        if (cm != null)
        {
            cm.OnComboChanged -= UpdateCombo;
            cm.OnOverloadChanged -= UpdateOverload;
            cm.OnOverloadReady -= OnReady;
        }

        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnUpgradeChanged -= HandleUpgradeChanged;
    }

    void HandleUpgradeChanged(UpgradeDefinition def, int level)
    {
        var cm = ComboManager.Instance;
        if (cm != null && def == cm.overloadUpgrade)
        {
            UpdateOverload(cm.OverloadFill);
            if (cm.IsOverloadUnlocked && cm.overloadReady)
                OnReady();
        }
    }

    void UpdateCombo(int combo, float mult)
    {
        // Plain ASCII "-"/"x", not "\u2014"/"\u00D7": the project's TMP font asset
        // (LiberationSans SDF) reports both characters present via HasCharacter()
        // and its characterLookupTable, but its baked atlas has no actual glyph
        // for either -- TMP lays the whole string out correctly (characterCount,
        // isVisible, etc. all report normal) but silently renders nothing from
        // the em-dash onward, so "Combo 35  \u2014  4\u00D7 coins" displayed as just
        // "Combo 35" with the rest invisible. ASCII is guaranteed present in any font.
        if (comboText) comboText.text = combo > 1 ? ("Combo " + combo + " - " + mult.ToString("0.#") + "x coins") : "";
    }

    void UpdateOverload(float fill)
    {
        var cm = ComboManager.Instance;
        bool isUnlocked = cm != null && cm.IsOverloadUnlocked;
        if (!isUnlocked)
        {
            if (overloadLoadingBar != null) overloadLoadingBar.SetTargetProgress01(0f);
            else if (overloadBar) overloadBar.value = 0f;
            if (overloadHighlight) overloadHighlight.SetReady(false);
            if (overloadPulse) overloadPulse.SetActive(false);
            if (overloadLabel) overloadLabel.text = "OVERLOAD";
            if (overloadButton) overloadButton.interactable = false;
            return;
        }

        if (overloadLoadingBar != null) overloadLoadingBar.SetTargetProgress01(fill);
        else if (overloadBar) overloadBar.value = fill;
        if (fill < 1f)
        {
            if (overloadHighlight) overloadHighlight.SetReady(false);
            if (overloadPulse) overloadPulse.SetActive(false);
            if (overloadLabel) overloadLabel.text = "OVERLOAD";
        }
    }

    void OnReady()
    {
        var cm = ComboManager.Instance;
        if (cm == null || !cm.IsOverloadUnlocked) return;

        if (overloadButton) overloadButton.interactable = true;
        if (overloadHighlight) overloadHighlight.SetReady(true);
        if (overloadPulse) overloadPulse.SetActive(true);
        if (overloadLabel) overloadLabel.text = "OVERLOAD READY!";
        SfxPlayer.PlayButtonClick();
        UIToast.ShowAt(overloadButton != null ? overloadButton.transform : transform, "Overload Ready! Tap to use!", Color.yellow);
    }
}
