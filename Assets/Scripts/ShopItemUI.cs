using UnityEngine;
using UnityEngine.UI;
using TMPro;

// One item tile. Spawned by ShopUI for each item in the selected category.
public class ShopItemUI : MonoBehaviour
{
    [Header("Refs")]
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text priceText;
    public Button actionButton;
    public TMP_Text actionLabel;
    public GameObject equippedBadge;

    private ShopItem item;
    private ShopUI shop;

    public void Setup(ShopItem data, ShopUI owner)
    {
        item = data;
        shop = owner;

        if (iconImage) iconImage.sprite = item.icon;
        if (nameText)  nameText.text = item.displayName;

        Refresh();
        if (actionButton)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(OnAction);
        }
    }

    public void Refresh()
    {
        bool owned = ShopInventory.IsOwned(item);
        bool equipped = ShopInventory.IsEquipped(item);

        if (priceText) priceText.text = owned ? "" : item.price.ToString();
        if (equippedBadge) equippedBadge.SetActive(equipped);

        if (actionLabel)
        {
            if (!owned) actionLabel.text = "BUY";
            else if (equipped) actionLabel.text = "EQUIPPED";
            else actionLabel.text = "EQUIP";
        }

        // Can't re-click the equipped item
        if (actionButton) actionButton.interactable = !(owned && equipped);
    }

    void OnAction()
    {
        bool owned = ShopInventory.IsOwned(item);

        if (!owned)
        {
            if (ShopInventory.Buy(item))
            {
                ShopInventory.Equip(item);   // auto-equip on purchase
                shop.OnPurchaseMade();
            }
            else
            {
                shop.OnPurchaseFailed(item); // not enough coins
            }
        }
        else
        {
            ShopInventory.Equip(item);
            shop.OnPurchaseMade();
        }
    }
}
