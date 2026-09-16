using UnityEngine;

// One purchasable item. Create assets via: right-click -> Create -> TypeKeep -> Shop -> Item.
// Add as many as you want; the shop reads them from categories, no code changes.
[CreateAssetMenu(fileName = "ShopItem", menuName = "TypeKeep/Shop/Item")]
public class ShopItem : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("UNIQUE id — used to save ownership/selection. Never reuse or rename.")]
    public string id = "item_id";
    public string displayName = "New Item";
    [TextArea] public string description = "";

    [Header("Display")]
    public Sprite icon;
    public Sprite previewImage;

    [Header("Cost")]
    public int price = 100;

    [Header("Type")]
    public ShopItemKind kind = ShopItemKind.Cosmetic;
    [Tooltip("For cosmetics: which slot it belongs to (e.g. 'TowerSkin', 'EnemySkin', 'BulletSkin').")]
    public string slot = "TowerSkin";

    [Header("Enemy Skin targeting (optional, only used when slot == \"EnemySkin\")")]
    [Tooltip("Restricts this skin to enemies whose Enemy.enemyTypeId matches (case-insensitive), e.g. \"Brute\". Leave empty to apply to ALL enemy types.")]
    public string appliesToEnemyType = "";

    [Header("Payload (assign your assets here)")]
    [Tooltip("Material/prefab/TextAsset/etc this item applies. Read by whatever consumes the slot. For Kind == WordPack, this is a TextAsset word file — same CSV format WordBank already parses.")]
    public Object payload;   // drag a Material, GameObject, TextAsset, etc.

    [Tooltip("Owned from the start (free / default option).")]
    public bool ownedByDefault = false;
}

// WordPack items don't use Slot/Equip at all — they're multi-select (up to
// WordBank.maxActivePacks) via WordPackSelection instead of the single-slot
// ShopInventory.Equip pattern the other kinds use.
public enum ShopItemKind { Cosmetic, Consumable, Upgrade, WordPack }
