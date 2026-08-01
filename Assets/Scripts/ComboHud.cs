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

        if (overloadBar) { overloadBar.minValue = 0; overloadBar.maxValue = 1; overloadBar.value = 0; }
        if (overloadButton) overloadButton.interactable = false;
        if (overloadHighlight) overloadHighlight.SetReady(false);
        if (overloadPulse) overloadPulse.SetActive(false);
        UpdateCombo(0, 1f);
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
