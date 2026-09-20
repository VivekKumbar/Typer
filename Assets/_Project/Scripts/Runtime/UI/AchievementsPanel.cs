using System.Collections.Generic;
using UnityEngine;

// Maps one CSV row's iconName to a real Sprite -- sprites can't live in a
// CSV, so this is the Inspector-side lookup table. Add one entry per
// distinct iconName used in the AchievementBank CSV.
[System.Serializable]
public class AchievementIconEntry
{
    public string iconName;
    public Sprite icon;
}

// Renders every achievement from an AchievementBank as a scrollable list of
// cards. No code changes to add an achievement — edit the CSV. Mirrors
// ShopUI's pattern (spawn one card per data row into a laid-out container).
// Put this on the Achievements panel (same show/hide pattern as ShopPanel —
// disabled by default, shown on the Achievements button click).
public class AchievementsPanel : MonoBehaviour
{
    [Header("Config")]
    public AchievementBank bank;

    [Header("List")]
    [Tooltip("Content transform under the ScrollView -- needs a Vertical Layout Group (or Grid), same as the Shop's item grid.")]
    public Transform listRoot;
    public AchievementCardUI cardPrefab;

    [Header("Icons")]
    [Tooltip("Maps each CSV row's iconName to a real Sprite -- I'll drag my own art in here. Placeholder/empty entries are fine until then.")]
    public List<AchievementIconEntry> icons = new List<AchievementIconEntry>();
    [Tooltip("Shown in the icon socket for any achievement whose iconName has no entry (or no sprite) in the list above. Temporary stand-in until real per-achievement art is assigned. Leave empty to show an empty socket instead.")]
    public Sprite placeholderIcon;

    private readonly List<AchievementCardUI> spawnedCards = new List<AchievementCardUI>();

    void Awake()
    {
        AchievementManager.Bank = bank;
    }

    void Start()
    {
        StatsManager.OnStatsChanged += HandleStatsChanged;
        AchievementManager.OnAchievementClaimed += HandleAchievementClaimed;
    }

    void OnDestroy()
    {
        StatsManager.OnStatsChanged -= HandleStatsChanged;
        AchievementManager.OnAchievementClaimed -= HandleAchievementClaimed;
    }

    void OnEnable()
    {
        BuildList();
    }

    void BuildList()
    {
        foreach (AchievementCardUI card in spawnedCards) if (card) Destroy(card.gameObject);
        spawnedCards.Clear();

        if (bank == null || listRoot == null || cardPrefab == null) return;

        foreach (AchievementData data in bank.GetAll())
        {
            AchievementCardUI card = Instantiate(cardPrefab, listRoot);
            card.Setup(data, this);
            spawnedCards.Add(card);
        }
    }

    void HandleStatsChanged() => RefreshAllCards();
    void HandleAchievementClaimed(string achievementId) => RefreshAllCards();

    void RefreshAllCards()
    {
        foreach (AchievementCardUI card in spawnedCards) if (card) card.Refresh();
    }

    public Sprite GetIcon(string iconName)
    {
        if (string.IsNullOrEmpty(iconName)) return null;
        foreach (AchievementIconEntry entry in icons)
            if (entry != null && entry.iconName == iconName) return entry.icon;
        return null;
    }
}
