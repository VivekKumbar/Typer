using System.Collections.Generic;
using UnityEngine;

// Tracks which owned Word Pack ShopItems are ACTIVE for the run — separate
// from ownership (ShopInventory) because packs are select-up-to-N, not the
// single-slot equip pattern skins use. Persisted via PlayerPrefs, same as
// ShopInventory, so the selection is remembered between sessions.
public static class WordPackSelection
{
    private const string SelectedKey = "wordpacks_selected"; // comma-separated ids

    public enum ToggleResult { Selected, Deselected, MaxReached }

    public static List<string> GetSelected()
    {
        string raw = PlayerPrefs.GetString(SelectedKey, "");
        var result = new List<string>();
        if (string.IsNullOrEmpty(raw)) return result;
        foreach (string id in raw.Split(','))
            if (!string.IsNullOrEmpty(id)) result.Add(id);
        return result;
    }

    static void SaveSelected(List<string> ids)
    {
        PlayerPrefs.SetString(SelectedKey, string.Join(",", ids));
        PlayerPrefs.Save();
    }

    public static bool IsSelected(ShopItem item)
    {
        if (item == null) return false;
        return GetSelected().Contains(item.id);
    }

    public static int SelectedCount() => GetSelected().Count;

    // Toggles one pack on/off. Blocks (MaxReached) instead of selecting past
    // maxActivePacks — the caller decides how to surface that (e.g. a popup).
    public static ToggleResult Toggle(ShopItem item, int maxActivePacks)
    {
        if (item == null || !ShopInventory.IsOwned(item)) return ToggleResult.Deselected;

        List<string> ids = GetSelected();
        if (ids.Contains(item.id))
        {
            ids.Remove(item.id);
            SaveSelected(ids);
            return ToggleResult.Deselected;
        }

        if (ids.Count >= maxActivePacks) return ToggleResult.MaxReached;

        ids.Add(item.id);
        SaveSelected(ids);
        return ToggleResult.Selected;
    }

    // Overwrites the current selection with up to maxActivePacks random items
    // drawn from ownedPacks (no duplicates). Used by the shop's Random button.
    public static void SelectRandom(List<ShopItem> ownedPacks, int maxActivePacks)
    {
        var pool = new List<ShopItem>(ownedPacks);
        var chosen = new List<string>();
        int want = Mathf.Min(maxActivePacks, pool.Count);

        for (int i = 0; i < want; i++)
        {
            int idx = Random.Range(0, pool.Count);
            chosen.Add(pool[idx].id);
            pool.RemoveAt(idx);
        }

        SaveSelected(chosen);
    }
}
