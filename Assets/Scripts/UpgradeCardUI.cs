using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// One draft card. Spawned dynamically by UpgradeDraftUI, same pattern as
// ShopItemUI is spawned by ShopUI.
public class UpgradeCardUI : MonoBehaviour
{
    [Header("Refs")]
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text descriptionText;
    public TMP_Text levelText;
    [Tooltip("Optional. Shown only when picking this card would promote it to the boss (level 6) upgrade.")]
    public GameObject bossBadge;
    public Button pickButton;

    public void Setup(UpgradeDefinition def, int currentLevel, bool willBeBoss, Action onPick)
    {
        if (iconImage) iconImage.sprite = willBeBoss ? def.bossIcon : def.icon;
        if (nameText) nameText.text = willBeBoss ? def.bossName : def.displayName;
        if (descriptionText) descriptionText.text = willBeBoss ? def.bossDescription : def.description;

        if (levelText)
        {
            if (currentLevel <= 0) levelText.text = "NEW";
            else if (willBeBoss) levelText.text = "LEVEL " + currentLevel + " -> BOSS";
            else levelText.text = "LEVEL " + currentLevel + " -> " + (currentLevel + 1);
        }

        if (bossBadge) bossBadge.SetActive(willBeBoss);

        if (pickButton)
        {
            pickButton.onClick.RemoveAllListeners();
            pickButton.onClick.AddListener(() => onPick?.Invoke());
        }
    }
}
