using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A single in-run upgrade. Hook a UI Button's OnClick to Purchase().
public class UpgradeButton : MonoBehaviour
{
    public enum UpgradeType { RepairFortress, MaxHealth }

    [Header("Config")]
    public UpgradeType type = UpgradeType.RepairFortress;
    public int cost = 10;
    public int amount = 20;

    [Header("Optional label")]
    public TMP_Text labelText; // shows "Repair (10)"

    void Start() { RefreshLabel(); }

    public void Purchase()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.IsGameOver) return;
        if (!gm.SpendCoins(cost)) return; // not enough coins -> do nothing

        switch (type)
        {
            case UpgradeType.RepairFortress: gm.HealFortress(amount); break;
            case UpgradeType.MaxHealth:      gm.maxHealth += amount; gm.HealFortress(amount); break;
        }
        cost = Mathf.RoundToInt(cost * 1.5f); // price climbs each buy
        RefreshLabel();
    }

    void RefreshLabel()
    {
        if (labelText == null) return;
        string name = type == UpgradeType.RepairFortress ? "Repair" : "Max HP";
        labelText.text = name + " (" + cost + ")";
    }
}
