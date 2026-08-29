using UnityEngine;

// Reads which skin is equipped for "EnemySkin" (via ShopInventory) and swaps
// this enemy's BODY renderer(s) to the matching material — a full material/
// base-colour swap, same pattern as SkinApplier (turret skins), not a tint.
// Put this on the same prefab root as Enemy.
//
// ORDERING (important): this applies in Awake(). Unity guarantees every
// object's Awake() finishes before any object's Start() runs the same frame,
// and EnemyHitFlash / EnemyDissolve now cache their "original" colour and
// grab their material instances in Start() specifically so they always see
// the SKINNED material, not the prefab default. Don't move those back to
// Awake(), and don't call Apply() later than Awake() either — it needs to
// win the race against Start().
//
// r.material (NOT sharedMaterial) is used deliberately: many enemies can be
// on screen wearing the same skin at once, and EnemyHitFlash/EnemyDissolve
// mutate colour/dissolve per-instance. Assigning via .material clones the
// skin material once per enemy, so flashing/dissolving one enemy never
// affects another enemy wearing the same skin. (The turret uses
// sharedMaterial instead — correct there, since there's only ever one turret.)
public class EnemySkinApplier : MonoBehaviour
{
    [Header("Master control")]
    [Tooltip("Uncheck to disable enemy skins entirely — enemies always use the prefab's default material.")]
    public bool enableSkins = true;

    [Header("Config")]
    [Tooltip("The shop catalog to resolve the equipped skin's ShopItem from.")]
    public ShopCatalog catalog;
    [Tooltip("Must match the ShopItem.slot used by your Enemy Skins category items.")]
    public string slot = "EnemySkin";

    [Header("Target")]
    [Tooltip("The BODY renderer(s) this skin replaces. Set this per enemy prefab — do NOT include the word-label or weapon renderers.")]
    public Renderer[] targetRenderers;

    [Header("Fallback")]
    [Tooltip("Applied when nothing is equipped, the equipped skin can't be resolved, or it doesn't apply to this enemy's type — and you still want to force a specific look instead of the prefab's own material. Leave empty to just keep whatever material is already on the prefab.")]
    public Material defaultMaterial;

    void Awake()
    {
        Apply();
    }

    public void Apply()
    {
        if (!enableSkins || targetRenderers == null || targetRenderers.Length == 0) return;

        Material mat = ResolveMaterial();
        if (mat == null) return; // nothing to apply -> keep prefab default

        foreach (Renderer r in targetRenderers)
            if (r != null) r.material = mat; // instanced per-enemy, see class comment
    }

    Material ResolveMaterial()
    {
        if (catalog == null) return defaultMaterial;

        string equippedId = ShopInventory.EquippedId(slot);
        if (string.IsNullOrEmpty(equippedId)) return defaultMaterial;

        ShopItem item = FindItem(equippedId);
        if (item == null) return defaultMaterial;

        // Optional per-enemy-type restriction: empty appliesToEnemyType = everyone.
        if (!string.IsNullOrEmpty(item.appliesToEnemyType))
        {
            Enemy enemy = GetComponent<Enemy>();
            string myType = enemy != null ? enemy.enemyTypeId : "";
            if (!string.Equals(item.appliesToEnemyType, myType, System.StringComparison.OrdinalIgnoreCase))
                return defaultMaterial;
        }

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
