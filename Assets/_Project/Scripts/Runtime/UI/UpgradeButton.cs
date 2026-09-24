using UnityEngine;
using UnityEngine.UI;
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
    public float costMultiplier = 1.2f;

    [Header("Optional label")]
    public TMP_Text labelText;

    private Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        FindLabelIfNeeded();
    }

    void OnEnable()
    {
        RefreshLabel();
    }

    void Start()
    {
        RefreshLabel();
    }

    void FindLabelIfNeeded()
    {
        if (labelText != null) return;
        if (transform.parent != null)
        {
            var t = transform.parent.Find("RepairLabel");
            if (t != null) labelText = t.GetComponent<TMP_Text>();
        }
        if (labelText == null)
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (t != null && t.gameObject.name == "RepairLabel")
                {
                    labelText = t;
                    break;
                }
            }
        }
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (button != null && gm != null)
        {
            // Keep interactable while game is active so clicking always triggers Purchase() with clear feedback
            button.interactable = !gm.IsGameOver;
        }
    }

    public bool Purchase()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.IsGameOver) return false;

        // If fortress is already full health, give feedback and do not consume coins
        if (type == UpgradeType.RepairFortress && gm.currentHealth >= gm.maxHealth)
        {
            SfxPlayer.PlayButtonClick();
            UIToast.ShowAt(transform, "Fortress Health Full (100%)", Color.green);
            Debug.Log($"[UpgradeButton] Fortress already at full health ({gm.currentHealth}/{gm.maxHealth})");
            return false;
        }

        // If player cannot afford it, show feedback
        if (gm.coins < cost)
        {
            SfxPlayer.PlayWrongKey();
            UIToast.ShowAt(transform, $"Need {cost} Coins to Repair!", new Color(1f, 0.85f, 0.2f));
            Debug.Log($"[UpgradeButton] Need {cost} coins, but have {gm.coins}");
            return false;
        }

        if (!gm.SpendCoins(cost)) return false;

        switch (type)
        {
            case UpgradeType.RepairFortress: 
                gm.HealFortress(amount); 
                SfxPlayer.PlayGameStart();
                UIToast.ShowAt(transform, $"+{amount} HP Repaired!", Color.green);
                Debug.Log($"[UpgradeButton] Repaired +{amount} HP! Health now {gm.currentHealth}/{gm.maxHealth}, coins left: {gm.coins}");
                break;
            case UpgradeType.MaxHealth: 
                gm.maxHealth += amount; 
                gm.HealFortress(amount); 
                SfxPlayer.PlayGameStart();
                UIToast.ShowAt(transform, $"+{amount} Max HP!", Color.green);
                break;
        }

        cost = Mathf.RoundToInt(cost * costMultiplier); // climbs by the Inspector value
        RefreshLabel();
        return true;
    }

    public void RefreshLabel()
    {
        FindLabelIfNeeded();
        if (labelText == null) return;
        string name = type == UpgradeType.RepairFortress ? "REPAIR" : "MAX HP";
        labelText.text = $"{name} ({cost})";
    }
}