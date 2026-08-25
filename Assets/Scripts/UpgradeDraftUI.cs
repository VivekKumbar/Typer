using System;
using System.Collections.Generic;
using UnityEngine;

// Put this on the draft panel. UpgradeManager.RunDraft() calls Show(...) with
// the 3 offered upgrades and a pick callback, then Hide() once a card is
// picked. No code changes needed to add more upgrades — this just renders
// whatever list it's given.
public class UpgradeDraftUI : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("The whole draft overlay — disabled by default, shown only during a draft.")]
    public GameObject panelRoot;
    [Tooltip("Parent with a Horizontal/Grid Layout Group the 3 cards spawn into.")]
    public Transform cardGridRoot;
    public UpgradeCardUI cardPrefab;

    private readonly List<UpgradeCardUI> spawned = new List<UpgradeCardUI>();
    private Action<UpgradeDefinition> onPicked;

    public void Show(List<UpgradeDefinition> offers, Action<UpgradeDefinition> onPick)
    {
        onPicked = onPick;

        foreach (UpgradeCardUI c in spawned)
            if (c != null) Destroy(c.gameObject);
        spawned.Clear();

        if (cardPrefab != null && cardGridRoot != null)
        {
            foreach (UpgradeDefinition def in offers)
            {
                UpgradeCardUI card = Instantiate(cardPrefab, cardGridRoot);
                int currentLevel = UpgradeManager.Instance != null ? UpgradeManager.Instance.LevelOf(def) : 0;
                bool willBeBoss = currentLevel == UpgradeDefinition.MaxNormalLevel;
                card.Setup(def, currentLevel, willBeBoss, () => Pick(def));
                spawned.Add(card);
            }
        }

        if (panelRoot) panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (panelRoot) panelRoot.SetActive(false);
    }

    void Pick(UpgradeDefinition def)
    {
        onPicked?.Invoke(def);
    }
}
