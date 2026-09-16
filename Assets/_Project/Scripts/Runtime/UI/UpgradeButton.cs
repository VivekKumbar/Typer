using UnityEngine;
using TMPro;

// A single in-run upgrade. Hook a UI Button's OnClick to Purchase().
// Cost, healed Amount, AND how fast the price climbs are all editable here.
public class UpgradeButton : MonoBehaviour
{
    public enum UpgradeType { RepairFortress, MaxHealth }

    [Header("Config")]
    public UpgradeType type = UpgradeType.RepairFortress;
    public int cost = 10;          // starting price
    public int amount = 20;        // how much it heals / adds
    [Tooltip("Price is multiplied by this after each purchase. 1 = flat cost, 1.5 = +50% each time.")]
    public float costMultiplier = 1.5f;

    [Header("Optional label")]
    public TMP_Text labelText;

    void Start() { RefreshLabel(); }

    public void Purchase()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.IsGameOver) return;
        if (!gm.SpendCoins(cost)) return; // not enough coins -> do nothing

        switch (type)
        {
            case UpgradeType.RepairFortress: gm.HealFortress(amount); break;
            case UpgradeType.MaxHealth: gm.maxHealth += amount; gm.HealFortress(amount); break;
        }

        cost = Mathf.RoundToInt(cost * costMultiplier); // climbs by the Inspector value
        RefreshLabel();
    }

    void RefreshLabel()
    {
        if (labelText == null) return;
        string name = type == UpgradeType.RepairFortress ? "Repair" : "Max HP";
        labelText.text = name + " (" + cost + ")";
    }
}