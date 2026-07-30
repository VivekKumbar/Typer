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

    private readonly List<ShopItemUI> spawnedCards = new List<ShopItemUI>();
    private int currentCategory = 0;

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
    }

    void BuildCategories()
    {
        foreach (Transform c in categoryListRoot) Destroy(c.gameObject);
        if (catalog == null) return;

        for (int i = 0; i < catalog.categories.Count; i++)
        {
            ShopCategory cat = catalog.categories[i];
            Button btn = Instantiate(categoryButtonPrefab, categoryListRoot);

            TMP_Text label = btn.GetComponentInChildren<TMP_Text>();
            if (label) label.text = cat.categoryName;

            Image icon = btn.transform.Find("Icon") ? btn.transform.Find("Icon").GetComponent<Image>() : null;
            if (icon && cat.categoryIcon) icon.sprite = cat.categoryIcon;

            int index = i;
            btn.onClick.AddListener(() => ShowCategory(index));
        }
    }

    public void ShowCategory(int index)
    {
        currentCategory = index;

        foreach (ShopItemUI card in spawnedCards) if (card) Destroy(card.gameObject);
        spawnedCards.Clear();

        if (catalog == null || index < 0 || index >= catalog.categories.Count) return;

        foreach (ShopItem item in catalog.categories[index].items)
        {
            ShopItemUI card = Instantiate(itemCardPrefab, itemGridRoot);
            card.Setup(item, this);
            spawnedCards.Add(card);
        }
    }

    public void OnPurchaseMade()
    {
        foreach (ShopItemUI card in spawnedCards) if (card) card.Refresh();
        RefreshCoins();
    }

    public void OnPurchaseFailed(ShopItem item)
    {
        if (notEnoughCoinsPopup) notEnoughCoinsPopup.SetActive(true);
    }

    void RefreshCoins()
    {
        if (coinText) coinText.text = Wallet.Total.ToString();
    }
}
