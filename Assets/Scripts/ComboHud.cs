using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ComboHUD : MonoBehaviour
{
    public TMP_Text comboText;
    public Slider overloadBar;
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

        if (overloadBar) { overloadBar.minValue = 0; overloadBar.maxValue = 1; }

        // Pull current values right now instead of assuming a fresh 0/1f
        // start — on a Continue, ComboManager may already hold a restored
        // combo/overload from before, and this makes the HUD reflect it
        // immediately regardless of script execution order (same idiom HUD.cs
        // uses for health).
        UpdateCombo(cm.combo, ComboManager.Multiplier);
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
        if (comboText) comboText.text = combo > 1 ? ("Combo " + combo + "  \u2014  " + mult.ToString("0.#") + "\u00D7 coins") : "";
    }

    void UpdateOverload(float fill)
    {
        if (overloadBar) overloadBar.value = fill;
        if (fill < 1f)
        {
            if (overloadButton) overloadButton.interactable = false;
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
