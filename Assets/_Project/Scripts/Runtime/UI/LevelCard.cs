using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Attach to each carousel card. Holds what this card launches and displays.
[ExecuteAlways]
public class LevelCard : MonoBehaviour
{
    [Header("Identity")]
    public string levelName = "Endless";
    [Tooltip("Scene to load when this card is played.")]
    public string sceneToLoad = "GameScene";

    [Header("Mode flags (optional)")]
    public bool forceDarkMode = false;
    public bool setDarkMode = false; // if true, applies forceDarkMode value

    [Tooltip("PlayerPrefs key to store which mode/level was chosen.")]
    public string modeKey = "SelectedMode";
    public int modeId = 0;

    [Header("Ground Skin preview (optional)")]
    [Tooltip("This card's own background Image -- swapped to the currently EQUIPPED Ground Skin's preview (live shop state; this is the Main Menu card, not a run in progress). Leave empty to skip this card.")]
    public Image background;
    [Tooltip("If off, this card keeps whatever background is assigned in the Inspector and ignores the equipped Ground Skin entirely.")]
    public bool reflectGroundSkin = true;

    [Header("Item Name (Bottom Red Plaque)")]
    [Tooltip("Text label in the bottom red plaque area showing the equipped item's name matching the shop exactly.")]
    public TMP_Text itemNameText;

    // Captured once so there's always something to fall back to -- whatever
    // sprite is assigned on Background in the Inspector IS the default.
    private Sprite defaultBackgroundSprite;

    void Awake()
    {
        if (background != null) defaultBackgroundSprite = background.sprite;
        EnsureItemNameText();
    }

    void OnEnable()
    {
        EnsureItemNameText();
        RefreshWithFallbackCatalog();
    }

    void Start()
    {
        EnsureItemNameText();
        RefreshWithFallbackCatalog();
    }

    public void EnsureItemNameText()
    {
        if (itemNameText != null) return;

        // Try to find existing text in children (under Frame or directly under Card)
        Transform t = transform.Find("Frame/ItemName") ?? transform.Find("ItemName");
        if (t != null)
        {
            itemNameText = t.GetComponent<TMP_Text>();
            if (itemNameText != null) return;
        }

        // If not found, dynamically create it inside Frame or Card
        Transform parent = transform.Find("Frame") ?? transform;
        GameObject go = new GameObject("ItemName", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = 5; // UI layer
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        // If parented to Frame, use y=-264; if parented to Card, use y=-257
        rt.anchoredPosition = parent == transform ? new Vector2(0f, -257f) : new Vector2(0f, -264f);
        rt.sizeDelta = new Vector2(350f, 60f);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.raycastTarget = false;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 20f;
        tmp.fontSizeMax = 36f;
        tmp.fontSize = 34f;
        tmp.fontStyle = FontStyles.Bold;

        // Copy font, material and color from Title
        Transform titleTr = transform.Find("Title");
        if (titleTr != null)
        {
            TMP_Text titleTmp = titleTr.GetComponent<TMP_Text>();
            if (titleTmp != null)
            {
                tmp.font = titleTmp.font;
                tmp.fontSharedMaterial = titleTmp.fontSharedMaterial;
                tmp.color = titleTmp.color;
            }
        }
        else
        {
            tmp.color = new Color(0.97f, 0.91f, 0.76f, 1f);
        }

        go.transform.SetAsLastSibling();
        itemNameText = tmp;
    }

    void RefreshWithFallbackCatalog()
    {
        LevelCarousel carousel = GetComponentInParent<LevelCarousel>();
        ShopCatalog cat = carousel != null ? carousel.groundSkinCatalog : null;
        if (cat == null)
        {
            MainMenu menu = FindObjectOfType<MainMenu>();
            if (menu != null) cat = menu.shopCatalog;
        }
        RefreshGroundSkinBackground(cat);
    }

    // Called by LevelCarousel (which owns the catalog reference) at Start and
    // whenever the equipped Ground Skin might have changed (see
    // LevelCarousel.OnEnable / ShopUI.OnDisable). Resolves the same way
    // GroundSkinApplier does for New Game (live ShopInventory.EquippedId --
    // this is the menu, not a run in progress, so there's nothing to lock),
    // but reads previewImage/icon like ShopItemUI instead of the Material payload.
    public void RefreshGroundSkinBackground(ShopCatalog catalog)
    {
        EnsureItemNameText();

        Sprite sprite = defaultBackgroundSprite;
        string nameToDisplay = "";

        ShopItem item = ResolveGroundItem(catalog);
        if (item != null)
        {
            sprite = item.previewImage != null ? item.previewImage : (item.icon != null ? item.icon : defaultBackgroundSprite);
            nameToDisplay = item.displayName;
        }

        if (background != null && reflectGroundSkin && sprite != null)
        {
            background.sprite = sprite;
        }

        if (itemNameText != null && !string.IsNullOrEmpty(nameToDisplay))
        {
            itemNameText.text = nameToDisplay;
        }
    }

    ShopItem ResolveGroundItem(ShopCatalog catalog)
    {
        if (catalog == null || catalog.categories == null) return null;

        // 1. Check currently equipped Ground Skin in PlayerPrefs / BridgeStorage
        string equippedId = ShopInventory.EquippedId("GroundSkin");
        if (!string.IsNullOrEmpty(equippedId))
        {
            ShopItem item = FindItem(catalog, equippedId);
            if (item != null) return item;
        }

        // 2. Fallback: match current background sprite against items in the catalog
        Sprite currentSprite = (background != null && background.sprite != null) ? background.sprite : defaultBackgroundSprite;
        if (currentSprite != null)
        {
            foreach (ShopCategory cat in catalog.categories)
            {
                if (cat == null || cat.items == null) continue;
                foreach (ShopItem si in cat.items)
                {
                    if (si == null) continue;
                    if (si.previewImage == currentSprite || si.icon == currentSprite)
                        return si;
                }
            }
        }

        return null;
    }

    static ShopItem FindItem(ShopCatalog catalog, string id)
    {
        if (catalog == null || catalog.categories == null) return null;
        foreach (ShopCategory cat in catalog.categories)
        {
            if (cat == null || cat.items == null) continue;
            foreach (ShopItem item in cat.items)
                if (item != null && item.id == id) return item;
        }
        return null;
    }

    public void Launch()
    {
        if (setDarkMode) DarkMode.Enabled = forceDarkMode;
        PlayerPrefs.SetInt(modeKey, modeId);
        PlayerPrefs.Save();
        MusicManager.PlayGameplayMusic();
        SceneManager.LoadScene(sceneToLoad);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        EnsureItemNameText();
        RefreshWithFallbackCatalog();
    }
#endif
}