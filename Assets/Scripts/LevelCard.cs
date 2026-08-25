using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Attach to each carousel card. Holds what this card launches.
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

    [Header("Ground Skin background (optional)")]
    [Tooltip("This card's own background Image -- reused as ShopItemUI does for icon/previewImage: swapped to the currently EQUIPPED Ground Skin's preview (live shop state; this is the Main Menu carousel, not a run in progress). Leave empty to skip this card.")]
    public Image background;
    [Tooltip("If off, this card keeps whatever background is assigned in the Inspector and ignores the equipped Ground Skin entirely.")]
    public bool reflectGroundSkin = true;

    // Captured once so there's always something to fall back to -- whatever
    // sprite is assigned on Background in the Inspector IS the default.
    private Sprite defaultBackgroundSprite;

    void Awake()
    {
        if (background != null) defaultBackgroundSprite = background.sprite;
    }

    // Called by LevelCarousel (which owns the catalog reference) at Start and
    // whenever the equipped Ground Skin might have changed (see
    // LevelCarousel.OnEnable / ShopUI.OnDisable). Resolves the same way
    // GroundSkinApplier does for New Game (live ShopInventory.EquippedId --
    // this is the menu, not a run in progress, so there's nothing to lock),
    // but reads previewImage/icon like ShopItemUI instead of the Material payload.
    public void RefreshGroundSkinBackground(ShopCatalog catalog)
    {
        if (background == null || !reflectGroundSkin) return;

        Sprite sprite = defaultBackgroundSprite;
        string equippedId = ShopInventory.EquippedId("GroundSkin");
        if (catalog != null && !string.IsNullOrEmpty(equippedId))
        {
            ShopItem item = FindItem(catalog, equippedId);
            if (item != null)
                sprite = item.previewImage != null ? item.previewImage : (item.icon != null ? item.icon : defaultBackgroundSprite);
        }
        background.sprite = sprite;
    }

    static ShopItem FindItem(ShopCatalog catalog, string id)
    {
        if (catalog.categories == null) return null;
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
        // Loading handled by your MainMenu loader if you route through it;
        // otherwise load directly:
        SceneManager.LoadScene(sceneToLoad);
    }
}