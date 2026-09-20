using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Renders the whole shop from a ShopCatalog. No code changes to add items:
// edit the catalog assets. Put this on your Shop panel.
public class ShopUI : MonoBehaviour
{
    [Header("Config")]
    public ShopCatalog catalog;

    [Header("Category list (left/side menu)")]
    public Transform categoryListRoot;   // has a Vertical/Horizontal Layout Group
    public Button categoryButtonPrefab;   // a button with a TMP label + optional icon

    [Header("Item grid")]
    public Transform itemGridRoot;        // has a Grid Layout Group
    public ShopItemUI itemCardPrefab;

    [Header("Coin display")]
    public TMP_Text coinText;

    [Header("Feedback")]
    public GameObject notEnoughCoinsPopup; // optional

    [Header("Word Packs category extras (optional)")]
    [Tooltip("The WordBank asset — used to read Max Active Packs and to resolve owned packs for the Random button. Assign the same WordBank your WaveManager uses.")]
    public WordBank wordBank;
    [Tooltip("Shown only while a Word Pack category (any item with Kind == WordPack) is active. Holds the Random button + the 'Selected: X / Y' counter.")]
    public GameObject wordPackHeader;
    [Tooltip("Shows 'Selected: X / Y' while a Word Packs category is active.")]
    public TMP_Text selectionCountText;
    [Tooltip("Randomly (re)selects up to Max Active Packs from the player's OWNED packs, overwriting the current selection.")]
    public Button randomButton;
    [Tooltip("Shown when the player tries to select a pack while already at Max Active Packs. Same simple pattern as Not Enough Coins Popup.")]
    public GameObject maxPacksSelectedPopup;

    [Header("Main Menu refresh (optional)")]
    [Tooltip("The Main Menu's level carousel -- refreshed on OnDisable so its card backgrounds pick up any Ground Skin change made while the shop was open, without needing to close/reopen the app. Leave empty if this ShopUI isn't the one shown over the Main Menu.")]
    public LevelCarousel levelCarousel;

    private readonly List<ShopItemUI> spawnedCards = new List<ShopItemUI>();
    private readonly List<Image> categoryButtonImages = new List<Image>();
    private int currentCategory = 0;

    public int WordPackMaxActive => wordBank != null ? wordBank.maxActivePacks : 3;

    void OnEnable()
    {
        BuildCategories();
        ShowCategory(0);
        RefreshCoins();
        Wallet.OnChanged += _ => RefreshCoins();
    }

    void OnDisable()
    {
        Wallet.OnChanged -= _ => RefreshCoins();
        if (levelCarousel != null) levelCarousel.RefreshGroundSkinBackgrounds();
    }

    void Start()
    {
        if (randomButton != null)
            randomButton.onClick.AddListener(OnRandomClicked);
    }

    void BuildCategories()
    {
        foreach (Transform c in categoryListRoot) Destroy(c.gameObject);
        categoryButtonImages.Clear();
        if (catalog == null) return;

        for (int i = 0; i < catalog.categories.Count; i++)
        {
            ShopCategory cat = catalog.categories[i];
            Button btn = Instantiate(categoryButtonPrefab, categoryListRoot);

            // Fully-baked per-category button art (icon + label already drawn
            // into the sprite) takes over the whole button and hides the
            // generic label/icon; falls back to the old text+icon look for
            // any category that doesn't have custom art assigned.
            bool hasCustomArt = cat.buttonNormalSprite != null && cat.buttonActiveSprite != null;
            Image btnImage = btn.GetComponent<Image>();
            TMP_Text label = btn.GetComponentInChildren<TMP_Text>();
            Transform iconT = btn.transform.Find("Icon");

            if (hasCustomArt)
            {
                if (btnImage) btnImage.sprite = cat.buttonNormalSprite;
                if (cat.nativeLabelOverArt)
                {
                    // Blank frame art: keep the real TMP label + icon on top of it.
                    if (label) label.text = cat.categoryName;
                    Image nativeIcon = iconT ? iconT.GetComponent<Image>() : null;
                    if (nativeIcon)
                    {
                        nativeIcon.sprite = cat.categoryIcon;
                        nativeIcon.color = cat.categoryIcon != null ? Color.white : new Color(1, 1, 1, 0);
                    }
                }
                else
                {
                    if (label) label.gameObject.SetActive(false);
                    if (iconT) iconT.gameObject.SetActive(false);
                }
            }
            else
            {
                if (label) label.text = cat.categoryName;
                Image icon = iconT ? iconT.GetComponent<Image>() : null;
                if (icon && cat.categoryIcon) icon.sprite = cat.categoryIcon;
            }

            categoryButtonImages.Add(btnImage);

            int index = i;
            btn.onClick.AddListener(() => ShowCategory(index));
        }
    }

    // Swaps each category button's baked art between its normal/active
    // sprite based on which category is currently shown. No-op for
    // categories without custom art (hasCustomArt was false at spawn time,
    // so their sprite was never touched here).
    void RefreshCategoryButtonArt()
    {
        for (int i = 0; i < categoryButtonImages.Count && i < catalog.categories.Count; i++)
        {
            ShopCategory cat = catalog.categories[i];
            if (cat.buttonNormalSprite == null || cat.buttonActiveSprite == null) continue;
            Image img = categoryButtonImages[i];
            if (img) img.sprite = (i == currentCategory) ? cat.buttonActiveSprite : cat.buttonNormalSprite;
        }
    }

    public void ShowCategory(int index)
    {
        currentCategory = index;
        RefreshCategoryButtonArt();

        foreach (ShopItemUI card in spawnedCards) if (card) Destroy(card.gameObject);
        spawnedCards.Clear();

        if (catalog == null || index < 0 || index >= catalog.categories.Count) return;

        ShopCategory cat = catalog.categories[index];
        bool isWordPackCategory = IsWordPackCategory(cat);
        if (wordPackHeader != null) wordPackHeader.SetActive(isWordPackCategory);
        if (isWordPackCategory) RefreshSelectionCount();

        foreach (ShopItem item in cat.items)
        {
            ShopItemUI card = Instantiate(itemCardPrefab, itemGridRoot);
            card.Setup(item, this);
            spawnedCards.Add(card);
        }
    }

    static bool IsWordPackCategory(ShopCategory cat)
    {
        if (cat == null || cat.items == null) return false;
        foreach (ShopItem item in cat.items)
            if (item != null && item.kind == ShopItemKind.WordPack) return true;
        return false;
    }

    public void OnPurchaseMade()
    {
        foreach (ShopItemUI card in spawnedCards) if (card) card.Refresh();
        RefreshCoins();
        if (wordPackHeader != null && wordPackHeader.activeSelf) RefreshSelectionCount();
    }

    public void OnPurchaseFailed(ShopItem item)
    {
        if (notEnoughCoinsPopup) notEnoughCoinsPopup.SetActive(true);
    }

    public void OnSelectionBlocked()
    {
        if (maxPacksSelectedPopup) maxPacksSelectedPopup.SetActive(true);
    }

    void OnRandomClicked()
    {
        if (wordBank == null || wordBank.catalog == null) return;

        var owned = new List<ShopItem>();
        foreach (ShopCategory cat in wordBank.catalog.categories)
        {
            if (cat == null || cat.items == null) continue;
            foreach (ShopItem item in cat.items)
                if (item != null && item.kind == ShopItemKind.WordPack && ShopInventory.IsOwned(item))
                    owned.Add(item);
        }

        WordPackSelection.SelectRandom(owned, WordPackMaxActive);
        OnPurchaseMade(); // refresh cards + selection count (no coins changed, harmless to also refresh those)
    }

    void RefreshSelectionCount()
    {
        if (selectionCountText == null) return;
        selectionCountText.text = "Selected: " + WordPackSelection.SelectedCount() + " / " + WordPackMaxActive;
    }

    void RefreshCoins()
    {
        if (coinText) coinText.text = Wallet.Total.ToString();
    }
}
