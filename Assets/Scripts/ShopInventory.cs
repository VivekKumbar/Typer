using UnityEngine;

// Tracks what the player owns and what's selected per slot. Saved with PlayerPrefs.
// Spends the persistent Wallet coins.
public static class ShopInventory
{
    // ---- ownership ----
    public static bool IsOwned(ShopItem item)
    {
        if (item == null) return false;
        if (item.ownedByDefault) return true;
        return PlayerPrefs.GetInt("owned_" + item.id, 0) == 1;
    }

    static void SetOwned(ShopItem item)
    {
        PlayerPrefs.SetInt("owned_" + item.id, 1);
        PlayerPrefs.Save();
    }

    // ---- buying ----
    // Returns true on success. Fails if already owned or not enough coins.
    public static bool Buy(ShopItem item)
    {
        if (item == null || IsOwned(item)) return false;
        if (!Wallet.Spend(item.price)) return false; // uses collected coins
        SetOwned(item);
        return true;
    }

    // ---- equipping (cosmetics) ----
    public static void Equip(ShopItem item)
    {
        if (item == null || !IsOwned(item)) return;
        PlayerPrefs.SetString("equipped_" + item.slot, item.id);
        PlayerPrefs.Save();
    }

    public static bool IsEquipped(ShopItem item)
    {
        if (item == null) return false;
        return PlayerPrefs.GetString("equipped_" + item.slot, "") == item.id;
    }

    // What id is equipped in a slot (e.g. "TowerSkin") — read this in-game to apply it.
    public static string EquippedId(string slot)
    {
        return PlayerPrefs.GetString("equipped_" + slot, "");
    }
}
