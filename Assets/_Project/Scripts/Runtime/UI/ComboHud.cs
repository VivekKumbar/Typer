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

        // Pull current values right now instead of assuming a fresh 0/1f
        // start — on a Continue, ComboManager may already hold a restored
        // combo/overload from before, and this makes the HUD reflect it
        // immediately regardless of script execution order (same idiom HUD.cs
        // uses for health).
        UpdateCombo(cm.combo, ComboManager.Multiplier);
        if (overloadLoadingBar != null) overloadLoadingBar.SnapTo01(cm.OverloadFill);
        UpdateOverload(cm.OverloadFill);
        if (cm.overloadReady) OnReady();
    }

    void OnDestroy()
    {
        var cm = ComboManager.Instance;
        if (cm == null) return;
        cm.OnComboChanged -= UpdateCombo;
        cm.OnOverloadChanged -= UpdateOverload;
        cm.OnOverloadReady -= OnReady;
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
        if (overloadLoadingBar != null) overloadLoadingBar.SetTargetProgress01(fill);
        else if (overloadBar) overloadBar.value = fill;
        if (fill < 1f)
        {
            if (overloadButton) overloadButton.interactable = true;
            if (overloadHighlight) overloadHighlight.SetReady(false);
            if (overloadPulse) overloadPulse.SetActive(false);
        }
    }

    void OnReady()
    {
        if (overloadButton) overloadButton.interactable = true;
        if (overloadHighlight) overloadHighlight.SetReady(true);
        if (overloadPulse) overloadPulse.SetActive(true);
    }
}
