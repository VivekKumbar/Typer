using UnityEngine;

// Put this on the battlefield ground object. Reads the Ground Skin LOCKED IN
// for this run (RunContext.LockedGroundSkinId — snapshotted once at run start
// by GameManager.Awake(), same pattern as Word Packs) and swaps the ground's
// material — same equip/ownership pattern as SkinApplier (turret) /
// EnemySkinApplier, just simplified for one object with one renderer.
// Deliberately does NOT read ShopInventory.EquippedId("GroundSkin") directly:
// that would let re-equipping a skin in the Shop swap the ground out from
// under a run already in progress.
public class GroundSkinApplier : MonoBehaviour
{
    [Header("Master control")]
    [Tooltip("Uncheck to disable ground skins entirely — the ground always keeps whatever material it currently has.")]
    public bool enableSkin = true;

    [Header("Config")]
    [Tooltip("The shop catalog to resolve the locked-in skin's ShopItem from.")]
    public ShopCatalog catalog;

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

    // Start, not OnEnable: matches the project convention. Safe to read
    // RunContext.LockedGroundSkinId here because GameManager.Awake() (which
    // locks it, either fresh for New Game or restored from RunSaveData for
    // Continue) always runs before ANY Start() in the scene.
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

        string lockedId = RunContext.LockedGroundSkinId;
        if (string.IsNullOrEmpty(lockedId)) return defaultMaterial;

        ShopItem item = FindItem(lockedId);
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
