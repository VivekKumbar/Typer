using System.Collections.Generic;
using UnityEngine;

// One ability's UI group: the upgrade that unlocks it, and the parent
// GameObject holding its button + bars/meters.
[System.Serializable]
public class AbilityUISlot
{
    [Tooltip("The upgrade that unlocks this ability. The first time it reaches level 1, UI Parent below is revealed.")]
    public UpgradeDefinition upgrade;
    [Tooltip("Parent GameObject holding this ability's button + bars. Inactive at run start, activated on unlock, stays active the rest of the run.")]
    public GameObject uiParent;
}

// Hides every ability's UI (button + bars) at the start of a run, and reveals
// each one the moment its matching upgrade is picked in the draft for the
// first time. The underlying managers (TimeSinkManager, ShieldManager,
// ComboManager, GameManager) keep running the whole time — this only gates
// what's ON SCREEN, and an inactive GameObject can't be tapped, so that alone
// stops the player from using an ability before it's unlocked.
//
// To add a new hidden-until-unlocked ability later: group its button+bars
// under one parent GameObject, add an entry to Slots with that parent and the
// UpgradeDefinition that should reveal it. No code changes.
public class AbilityUIRegistry : MonoBehaviour
{
    public static AbilityUIRegistry Instance { get; private set; }

    [Header("Ability -> UI mapping")]
    [Tooltip("One entry per hidden-until-unlocked ability.")]
    public List<AbilityUISlot> slots = new List<AbilityUISlot>();

    void Awake()
    {
        Instance = this;

        // Per-run reset: a fresh scene load means a fresh instance, so this
        // naturally starts everything hidden every run with no manual reset needed.
        foreach (AbilityUISlot slot in slots)
            if (slot != null && slot.uiParent != null) slot.uiParent.SetActive(false);
    }

    void Start()
    {
        if (UpgradeManager.Instance == null) return;

        UpgradeManager.Instance.OnUpgradeChanged += HandleUpgradeChanged;

        // Sync-now pass: on a Continue, UpgradeManager already restored its
        // levels in its own Awake (guaranteed to run before this Start), but
        // that restore deliberately doesn't fire OnUpgradeChanged (nothing
        // would've been subscribed yet that early). Reveal anything already
        // unlocked right now instead of waiting for a future pick. On a fresh
        // run every level is 0, so this is a no-op — same hidden-at-start
        // behavior as before.
        foreach (AbilityUISlot slot in slots)
        {
            if (slot == null || slot.upgrade == null || slot.uiParent == null) continue;
            if (UpgradeManager.Instance.LevelOf(slot.upgrade) > 0)
                slot.uiParent.SetActive(true);
        }
    }

    void OnDestroy()
    {
        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnUpgradeChanged -= HandleUpgradeChanged;
    }

    // Fires on every level-up, not just the first — reveal is a no-op once
    // already visible, so this doesn't need to special-case level == 1.
    void HandleUpgradeChanged(UpgradeDefinition def, int newLevel)
    {
        if (def == null) return;
        foreach (AbilityUISlot slot in slots)
        {
            if (slot != null && slot.upgrade == def && slot.uiParent != null && !slot.uiParent.activeSelf)
            {
                slot.uiParent.SetActive(true);
                break;
            }
        }
    }
}
