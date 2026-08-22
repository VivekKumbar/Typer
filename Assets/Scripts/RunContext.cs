using System.Collections.Generic;
using UnityEngine;

// Snapshot of the word packs LOCKED IN for the run currently in progress.
// Deliberately separate from WordPackSelection (the live/persistent shop
// choice, changeable any time from the Shop) — GameManager.Awake() locks
// this exactly once per run (a fresh snapshot for New Game, or restored
// from RunSaveData for Continue) and WordBank reads ONLY this for the
// entire run. That way changing packs in the Shop mid-run, or between
// pausing to the Main Menu and resuming, can never swap the active word
// pool out from under the player — it only ever affects the NEXT run.
public static class RunContext
{
    public static List<string> LockedWordPackIds { get; private set; } = new List<string>();

    // Same locking idea as LockedWordPackIds above, for the equipped Ground
    // Skin ("GroundSkin" slot) — GroundSkinApplier reads ONLY this during
    // gameplay, never ShopInventory.EquippedId(...) directly, so re-equipping
    // a skin in the Shop mid-run can't swap the ground out from under the
    // player either.
    public static string LockedGroundSkinId { get; private set; } = "";

    // Toggleable sanity-check logging (plain static class, no Inspector --
    // flip this line). Prints exactly what got locked in and from where, so
    // a New-Game-after-old-save can be verified against the live shop state
    // at a glance in the Console.
    public static bool logSnapshot = false;

    // New Game: snapshot whatever is currently selected/equipped in the shop
    // RIGHT NOW -- always a fresh read of WordPackSelection/ShopInventory,
    // never anything cached earlier in the session or inherited from a prior
    // save. This runs identically whether or not an old save existed: by the
    // time this fires (GameManager.Awake, after ClearSave already ran and
    // after the GameScene load), there IS no old save left to influence it.
    public static void LockForNewRun()
    {
        LockedWordPackIds = WordPackSelection.GetSelected();
        LockedGroundSkinId = ShopInventory.EquippedId("GroundSkin");

        if (logSnapshot)
            Debug.Log($"[RunContext] New Game snapshot (live shop state, taken fresh at run start): " +
                      $"groundSkinId='{LockedGroundSkinId}', wordPacks=[{string.Join(", ", LockedWordPackIds)}]. " +
                      $"Compare against ShopInventory.EquippedId(\"GroundSkin\")='{ShopInventory.EquippedId("GroundSkin")}' " +
                      $"and WordPackSelection.GetSelected()=[{string.Join(", ", WordPackSelection.GetSelected())}] -- must match exactly.");
    }

    // Continue: restore exactly what was locked in when the run was saved,
    // even if the live shop selection/equip has changed since then.
    public static void RestoreFromSave(RunSaveData save)
    {
        LockedWordPackIds = (save != null && save.selectedWordPackIds != null)
            ? new List<string>(save.selectedWordPackIds)
            : new List<string>();
        LockedGroundSkinId = save != null ? save.groundSkinId : "";

        if (logSnapshot)
            Debug.Log($"[RunContext] Continue snapshot (restored from the SAVED run, NOT live shop state): " +
                      $"groundSkinId='{LockedGroundSkinId}', wordPacks=[{string.Join(", ", LockedWordPackIds)}].");
    }
}
