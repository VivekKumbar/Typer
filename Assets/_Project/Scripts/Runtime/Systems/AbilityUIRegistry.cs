using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

public class AbilityUIRegistry : MonoBehaviour
{
    public static AbilityUIRegistry Instance { get; private set; }

    [Header("Ability -> UI mapping")]
    [Tooltip("One entry per hidden-until-unlocked ability.")]
    public List<AbilityUISlot> slots = new List<AbilityUISlot>();

    void Awake()
    {
        Instance = this;

        // Configure and sanitize every slot while objects are accessible
        foreach (AbilityUISlot slot in slots)
        {
            if (slot != null && slot.uiParent != null)
            {
                SetupAbilitySlot(slot);
                slot.uiParent.SetActive(false);
            }
        }
    }

    void Start()
    {
        if (UpgradeManager.Instance == null) return;

        UpgradeManager.Instance.OnUpgradeChanged += HandleUpgradeChanged;

        // Sync-now pass: on a Continue, UpgradeManager already restored its
        // levels in its own Awake. Reveal anything already unlocked right now.
        foreach (AbilityUISlot slot in slots)
        {
            if (slot == null || slot.upgrade == null || slot.uiParent == null) continue;
            if (UpgradeManager.Instance.LevelOf(slot.upgrade) > 0)
            {
                slot.uiParent.SetActive(true);
                SetupAbilitySlot(slot);
            }
        }
    }

    void OnDestroy()
    {
        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnUpgradeChanged -= HandleUpgradeChanged;
    }

    // Fires on every level-up, not just the first — reveal is a no-op once
    // already visible.
    void HandleUpgradeChanged(UpgradeDefinition def, int newLevel)
    {
        if (def == null) return;
        foreach (AbilityUISlot slot in slots)
        {
            if (slot != null && slot.upgrade == def && slot.uiParent != null && !slot.uiParent.activeSelf)
            {
                slot.uiParent.SetActive(true);
                SetupAbilitySlot(slot);
                break;
            }
        }
    }

    public void SetupAbilitySlot(AbilityUISlot slot)
    {
        if (slot == null || slot.uiParent == null) return;

        AbilityClickTrigger.AbilityType abilityType = ResolveAbilityType(slot);

        // Ensure proper layer and raycastTarget on all graphics
        foreach (var t in slot.uiParent.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = 5; // UI Layer
        }

        foreach (var g in slot.uiParent.GetComponentsInChildren<Graphic>(true))
        {
            g.raycastTarget = true;
        }

        // Wire click triggers on all interactive parts (buttons, plates, labels, bars)
        foreach (Transform child in slot.uiParent.GetComponentsInChildren<Transform>(true))
        {
            string n = child.gameObject.name.ToLowerInvariant();
            bool isInteractive = n.Contains("button") || n.Contains("plate") || n.Contains("label") || n.Contains("bar");
            if (isInteractive)
            {
                var trigger = child.GetComponent<AbilityClickTrigger>();
                if (trigger == null) trigger = child.gameObject.AddComponent<AbilityClickTrigger>();
                trigger.abilityType = abilityType;
                trigger.bounceTarget = child;
            }
        }

        // Specific configuration per ability
        switch (abilityType)
        {
            case AbilityClickTrigger.AbilityType.Repair:
                SetupRepair(slot.uiParent);
                break;
            case AbilityClickTrigger.AbilityType.Shield:
                SetupShield(slot.uiParent);
                break;
            case AbilityClickTrigger.AbilityType.TimeSink:
                SetupTimeSink(slot.uiParent);
                break;
            case AbilityClickTrigger.AbilityType.Overload:
                SetupOverload(slot.uiParent);
                break;
        }
    }

    AbilityClickTrigger.AbilityType ResolveAbilityType(AbilityUISlot slot)
    {
        string pName = slot.uiParent != null ? slot.uiParent.name.ToLowerInvariant() : "";
        string uName = slot.upgrade != null ? (slot.upgrade.name + " " + slot.upgrade.displayName).ToLowerInvariant() : "";

        if (pName.Contains("repair") || uName.Contains("repair"))
            return AbilityClickTrigger.AbilityType.Repair;
        if (pName.Contains("shield") || uName.Contains("shield"))
            return AbilityClickTrigger.AbilityType.Shield;
        if (pName.Contains("timesink") || pName.Contains("time") || uName.Contains("slow") || uName.Contains("time"))
            return AbilityClickTrigger.AbilityType.TimeSink;
        return AbilityClickTrigger.AbilityType.Overload;
    }

    void SetupRepair(GameObject root)
    {
        var ub = root.GetComponentInChildren<UpgradeButton>(true);
        var label = FindInTree<TMP_Text>(root, "RepairLabel");

        if (ub != null)
        {
            if (label != null) ub.labelText = label;
            ub.RefreshLabel();

            var btn = ub.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => ub.Purchase());
            }
        }

        var plateBtn = FindInTree<Button>(root, "RepairPlate");
        if (plateBtn != null && ub != null)
        {
            plateBtn.onClick.RemoveAllListeners();
            plateBtn.onClick.AddListener(() => ub.Purchase());
        }
    }

    void SetupShield(GameObject root)
    {
        var sb = root.GetComponentInChildren<ShieldButton>(true);
        var label = FindInTree<TMP_Text>(root, "ShieldLabel");

        if (sb != null)
        {
            if (label != null) sb.labelText = label;
            sb.RefreshLabel();

            var btn = sb.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => sb.Buy());
            }
        }

        var plateBtn = FindInTree<Button>(root, "ShieldPlate");
        if (plateBtn != null && sb != null)
        {
            plateBtn.onClick.RemoveAllListeners();
            plateBtn.onClick.AddListener(() => sb.Buy());
        }
    }

    void SetupTimeSink(GameObject root)
    {
        var label = FindInTree<TMP_Text>(root, "TimeSinkLabel");
        var ts = TimeSinkManager.Instance;
        if (label != null)
        {
            label.text = (ts != null && ts.IsActive) ? "TIME SINK ACTIVE" : "TIME SINK";
        }

        var btn = root.GetComponentInChildren<Button>(true);
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => {
                var inst = TimeSinkManager.Instance;
                if (inst != null && !inst.IsActive)
                {
                    if (!inst.IsReady) inst.DebugFillCharge();
                    inst.Activate();
                    SfxPlayer.PlayGameStart();
                    UIToast.ShowAt(btn.transform, "Time Sink Activated!", Color.cyan);
                }
            });
        }
    }

    void SetupOverload(GameObject root)
    {
        var label = FindInTree<TMP_Text>(root, "OverloadLabel");
        if (label != null)
        {
            label.text = "OVERLOAD";
        }

        var btn = root.GetComponentInChildren<Button>(true);
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => {
                var inst = ComboManager.Instance;
                if (inst != null)
                {
                    if (!inst.overloadReady) inst.DebugFillOverload();
                    inst.TriggerOverload();
                    SfxPlayer.PlayGameStart();
                    UIToast.ShowAt(btn.transform, "Overload Blast!", Color.yellow);
                }
            });
        }
    }

    T FindInTree<T>(GameObject root, string targetName) where T : Component
    {
        foreach (var c in root.GetComponentsInChildren<T>(true))
        {
            if (c.gameObject.name.Equals(targetName, System.StringComparison.OrdinalIgnoreCase))
                return c;
        }
        return null;
    }
}

