using UnityEngine;

// Put this on the battlefield ground object. Reads which skin is equipped for
// "GroundSkin" (via ShopInventory) and swaps the ground's material — same
// equip/ownership pattern as SkinApplier (turret) / EnemySkinApplier, just
// simplified for one object with one renderer.
public class GroundSkinApplier : MonoBehaviour
{
    [Header("Master control")]
    [Tooltip("Uncheck to disable ground skins entirely — the ground always keeps whatever material it currently has.")]
    public bool enableSkin = true;

    [Header("Config")]
    [Tooltip("The shop catalog to resolve the equipped skin's ShopItem from.")]
    public ShopCatalog catalog;
    [Tooltip("Must match the ShopItem.slot used by your Ground Skins category items.")]
    public string slot = "GroundSkin";

    [Header("Target")]
    [Tooltip("The ground's renderer. Auto-found on this object if left empty.")]
    public Renderer groundRenderer;

    [Header("Fallback")]
    [Tooltip("Used when nothing is equipped or the equipped skin can't be resolved. Leave empty to just keep the ground's current material.")]
    public Material defaultMaterial;

    void Awake()
    {
        if (groundRenderer == null) groundRenderer = GetComponent<Renderer>();
    }

    // Start, not OnEnable: matches the project convention of reading singleton-
    // backed state (ShopInventory reads PlayerPrefs, no singleton race here, but
    // keeping it consistent with the other appliers).
    void Start()
    {
        Apply();
    }

    public void Apply()
    {
        if (!enableSkin || groundRenderer == null) return;

        Material mat = ResolveMaterial();
        if (mat != null) groundRenderer.sharedMaterial = mat; // one persistent ground object, no per-instance need
    }

    Material ResolveMaterial()
    {
        if (catalog == null) return defaultMaterial;

        string equippedId = ShopInventory.EquippedId(slot);
        if (string.IsNullOrEmpty(equippedId)) return defaultMaterial;

        ShopItem item = FindItem(equippedId);
        if (item == null) return defaultMaterial;

        return (item.payload as Material) ?? defaultMaterial;
    }

    ShopItem FindItem(string id)
    {
        foreach (ShopCategory cat in catalog.categories)
        {
            if (cat == null) continue;
            foreach (ShopItem item in cat.items)
                if (item != null && item.id == id) return item;
        }
        return null;
    }
}
