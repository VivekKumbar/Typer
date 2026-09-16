using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Hook a UI Button's OnClick to Buy(). Greys out while the shield is already up
// or unaffordable.
[RequireComponent(typeof(Button))]
public class ShieldButton : MonoBehaviour
{
    public TMP_Text labelText;
    private Button button;

    void Awake() { button = GetComponent<Button>(); }

    public void Buy()
    {
        if (ShieldManager.Instance != null)
            ShieldManager.Instance.TryRaiseShield();
    }

    void Update()
    {
        var sm = ShieldManager.Instance;
        var gm = GameManager.Instance;
        if (sm == null || gm == null) return;

        bool afford = gm.coins >= sm.cost;
        bool usable = afford && !sm.IsActive && !gm.IsGameOver;
        if (button) button.interactable = usable;

        if (labelText)
            labelText.text = sm.IsActive ? "Shield UP" : "Shield (" + sm.cost + ")";
    }
}