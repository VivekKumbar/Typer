using UnityEngine;
using UnityEngine.UI;
using TMPro;

// One item tile. Spawned by ShopUI for each item in the selected category.
// Kind.WordPack items use a select-up-to-N toggle (WordPackSelection) instead
// of the single-slot equip pattern every other kind uses (ShopInventory.Equip).
public class ShopItemUI : MonoBehaviour
{
    [Header("Refs")]
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text priceText;
    public Button actionButton;
    public TMP_Text actionLabel;
    public GameObject equippedBadge;
    [Tooltip("Word Packs only: shown when this pack is in the active selection (separate from equippedBadge, which single-equip kinds use).")]
    public GameObject selectedBadge;

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

        if (item.kind == ShopItemKind.WordPack)
        {
            bool selected = WordPackSelection.IsSelected(item);

            if (priceText) priceText.text = owned ? "" : item.price.ToString();
            if (equippedBadge) equippedBadge.SetActive(false); // not used for word packs
            if (selectedBadge) selectedBadge.SetActive(selected);

            if (actionLabel)
            {
                if (!owned) actionLabel.text = "BUY";
                else if (selected) actionLabel.text = "SELECTED";
                else actionLabel.text = "SELECT";
            }

            // Always clickable: buy, select, or deselect (deselecting an
            // already-selected pack is a valid action, unlike single-equip).
            if (actionButton) actionButton.interactable = true;
            return;
        }

        bool equipped = ShopInventory.IsEquipped(item);

        if (priceText) priceText.text = owned ? "" : item.price.ToString();
        if (equippedBadge) equippedBadge.SetActive(equipped);
        if (selectedBadge) selectedBadge.SetActive(false); // not used outside word packs

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

        if (item.kind == ShopItemKind.WordPack)
        {
            if (!owned)
            {
                if (ShopInventory.Buy(item))
                {
                    // Auto-select on purchase if there's room, mirroring how
                    // skins auto-equip on purchase.
                    if (WordPackSelection.SelectedCount() < shop.WordPackMaxActive)
                        WordPackSelection.Toggle(item, shop.WordPackMaxActive);
                    shop.OnPurchaseMade();
                }
                else
                {
                    shop.OnPurchaseFailed(item);
                }
            }
            else
            {
                var result = WordPackSelection.Toggle(item, shop.WordPackMaxActive);
                if (result == WordPackSelection.ToggleResult.MaxReached)
                    shop.OnSelectionBlocked();
                shop.OnPurchaseMade();
            }
            return;
        }

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
