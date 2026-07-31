using UnityEngine;

// Reads which skin is equipped for a slot (via ShopInventory) and swaps this
// object's renderers to the matching material, so shop purchases actually
// show up in gameplay. Put this on the same object as TurretAim (the turret
// model root) with slot="TowerSkin".
public class SkinApplier : MonoBehaviour
{
    [Header("Config")]
    public ShopCatalog catalog;
    public string slot = "TowerSkin";

    [Header("Target")]
    [Tooltip("Renderers to re-skin. Leave empty to auto-grab all Renderers in children.")]
    public Renderer[] targetRenderers;

    void Start()
    {
        Apply();
    }

    public void Apply()
    {
        if (catalog == null) return;

        string equippedId = ShopInventory.EquippedId(slot);
        if (string.IsNullOrEmpty(equippedId)) return; // nothing equipped, keep the default look

        ShopItem item = FindItem(equippedId);
        Material mat = item != null ? item.payload as Material : null;
        if (mat == null) return;

        Renderer[] renderers = (targetRenderers != null && targetRenderers.Length > 0)
            ? targetRenderers
            : GetComponentsInChildren<Renderer>(true);

        foreach (Renderer r in renderers)
            r.sharedMaterial = mat;
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
