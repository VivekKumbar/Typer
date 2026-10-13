using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// One item tile. Spawned by ShopUI for each item in the selected category.
// Kind.WordPack items use a select-up-to-N toggle (WordPackSelection) instead
// of the single-slot equip pattern every other kind uses (ShopInventory.Equip).
public class ShopItemUI : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("The item's own art (turret/enemy/etc), shown centered over the card's static backdrop.")]
    public Image iconImage;
    [Tooltip("The card's background/backdrop — a DIFFERENT Image component from iconImage. Only overridden when an item explicitly sets previewImage; otherwise the backdrop baked into the prefab is left alone (it's shared chrome, not per-item art).")]
    public Image previewImage;
    public TMP_Text nameText;
    public TMP_Text priceText;
    public Button actionButton;
    public TMP_Text actionLabel;
    public GameObject equippedBadge;
    [Tooltip("Word Packs only: shown when this pack is in the active selection (separate from equippedBadge, which single-equip kinds use).")]
    public GameObject selectedBadge;

    [Header("Visual-only extras (optional)")]
    [Tooltip("Container holding the coin icon + Price text. Hidden once the item is owned (there's no price to show).")]
    public GameObject priceGroup;
    [Tooltip("Word Packs only: padlock overlay shown while the pack is NOT owned (the 'Locked' look).")]
    public GameObject lockedOverlay;
    [Tooltip("Word Packs only: tint applied to the icon while the pack is NOT owned.")]
    public Color lockedIconTint = new Color(0.42f, 0.44f, 0.5f, 1f);

    private ShopItem item;
    private ShopUI shop;

    public void Setup(ShopItem data, ShopUI owner)
    {
        item = data;
        shop = owner;

        if (iconImage)
        {
            bool isGround = item != null && item.slot == "GroundSkin";
            iconImage.gameObject.SetActive(!isGround);
            if (!isGround)
            {
                iconImage.sprite = item.icon;
                iconImage.color = item.icon != null ? Color.white : new Color(1, 1, 1, 0);
            }
        }
        if (previewImage && item.previewImage != null)
            previewImage.sprite = item.slot == "GroundSkin"
                ? CroppedToCard(item.previewImage, previewImage.rectTransform.rect.size)
                : item.previewImage;
        if (nameText)  nameText.text = item.displayName;

        Refresh();
        if (actionButton)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(OnAction);
        }
    }

    // Ground art is tall (9:16) but the card is wider than it is tall, so show a
    // centre crop at the card's aspect instead of squashing the whole map in.
    static readonly Dictionary<Sprite, Sprite> s_cardCrops = new Dictionary<Sprite, Sprite>();

    static Sprite CroppedToCard(Sprite source, Vector2 cardSize)
    {
        if (cardSize.x <= 0f || cardSize.y <= 0f) return source;
        if (source.packed && source.packingMode == SpritePackingMode.Tight) return source; // no rect to crop from
        if (s_cardCrops.TryGetValue(source, out Sprite cached) && cached != null) return cached;

        Rect r = source.textureRect;
        float cardAspect = cardSize.x / cardSize.y;
        Rect crop;
        if (r.width / r.height < cardAspect)
        {
            float h = r.width / cardAspect;
            crop = new Rect(r.x, r.y + (r.height - h) * 0.5f, r.width, h);
        }
        else
        {
            float w = r.height * cardAspect;
            crop = new Rect(r.x + (r.width - w) * 0.5f, r.y, w, r.height);
        }

        Sprite cropped = Sprite.Create(source.texture, crop, new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
        cropped.name = source.name + "_Card";
        s_cardCrops[source] = cropped;
        return cropped;
    }

    public void Refresh()
    {
        bool owned = ShopInventory.IsOwned(item);
        bool waveLocked = !owned && ShopInventory.IsLockedByWave(item);

        if (iconImage && item != null && item.slot == "GroundSkin")
        {
            iconImage.gameObject.SetActive(false);
        }

        if (item.kind == ShopItemKind.WordPack)
        {
            bool selected = WordPackSelection.IsSelected(item);

        if (priceText) priceText.text = owned ? "" : item.price.ToString();
            if (priceGroup) priceGroup.SetActive(!owned);
            if (equippedBadge) equippedBadge.SetActive(false); // not used for word packs
            if (selectedBadge) selectedBadge.SetActive(selected);
            if (lockedOverlay) lockedOverlay.SetActive(waveLocked);
            if (iconImage && item.icon != null) iconImage.color = waveLocked ? lockedIconTint : Color.white;

            if (actionLabel)
            {
                if (waveLocked) actionLabel.text = $"WAVE {item.requiredWave}";
                else if (!owned) actionLabel.text = "BUY";
                else if (selected) actionLabel.text = "SELECTED";
                else actionLabel.text = "SELECT";
            }

            // Always clickable: buy, select, or deselect (clicking a locked item shows toast requirement)
            if (actionButton) actionButton.interactable = true;
            return;
        }

        bool equipped = ShopInventory.IsEquipped(item);

        if (priceText) priceText.text = owned ? "" : item.price.ToString();
        if (priceGroup) priceGroup.SetActive(!owned);
        if (lockedOverlay) lockedOverlay.SetActive(waveLocked);
        if (iconImage && item.icon != null) iconImage.color = waveLocked ? lockedIconTint : Color.white;
        if (equippedBadge) equippedBadge.SetActive(equipped);
        if (selectedBadge) selectedBadge.SetActive(false); // not used outside word packs

        if (actionLabel)
        {
            if (waveLocked) actionLabel.text = $"WAVE {item.requiredWave}";
            else if (!owned) actionLabel.text = "BUY";
            else if (equipped) actionLabel.text = "EQUIPPED";
            else actionLabel.text = "EQUIP";
        }

        // Can't re-click the equipped item; locked items remain clickable for feedback
        if (actionButton) actionButton.interactable = !(owned && equipped);
    }

    void OnAction()
    {
        bool owned = ShopInventory.IsOwned(item);

        if (!owned && ShopInventory.IsLockedByWave(item))
        {
            UIToast.ShowAt(transform, $"Reach Wave {item.requiredWave} to unlock!", Color.yellow);
            return;
        }

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
