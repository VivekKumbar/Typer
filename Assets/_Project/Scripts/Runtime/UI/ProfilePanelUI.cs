using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Populates the Profile panel from StatsManager. Put this on the ProfilePanel root.
public class ProfilePanelUI : MonoBehaviour
{
    public enum RankMetric { HighestWave, LifetimeAccuracy }

    // One rung of the rank ladder: the player earns Rank Name once the chosen
    // metric (see Rank Metric) reaches Min Requirement. The highest rung reached wins.
    [Serializable]
    public class RankTier
    {
        [Tooltip("Title shown on the rank card, e.g. TYPIST.")]
        public string rankName = "ROOKIE";
        [Tooltip("Minimum value of the Rank Metric (a wave number, or an accuracy percentage) needed to earn this rank.")]
        public float minRequirement;
    }

    [Header("Rank")]
    [Tooltip("What the rank is derived from: the best wave reached, or lifetime accuracy (%).")]
    public RankMetric rankMetric = RankMetric.HighestWave;
    [Tooltip("Rank ladder, in any order. The tier with the highest Min Requirement that the player has reached is shown. Keep one tier at 0 so everyone has a rank.")]
    public List<RankTier> rankTiers = new List<RankTier>
    {
        new RankTier { rankName = "ROOKIE", minRequirement = 0f },
        new RankTier { rankName = "TYPIST", minRequirement = 5f },
        new RankTier { rankName = "WORDSMITH", minRequirement = 15f },
        new RankTier { rankName = "KEYMASTER", minRequirement = 30f },
    };

    [Header("Layout (for the forced rebuild on open — nested ContentSizeFitters\ndon't always resolve correctly the first frame a panel is activated)")]
    public RectTransform content;
    public RectTransform recordsSection;
    public RectTransform lifetimeSection;

    [Header("Header")]
    public TMP_Text rankText;
    public TMP_Text rankSubtitleText;

    [Header("Records (personal bests)")]
    public TMP_Text highestWaveText;
    public TMP_Text highestComboText;
    public TMP_Text mostCoinsInRunText;
    public TMP_Text bestAccuracyText;

    [Header("Lifetime")]
    public TMP_Text enemiesDestroyedText;
    public TMP_Text lettersTypedText;
    public TMP_Text lifetimeAccuracyText;
    public TMP_Text totalCoinsText;
    public TMP_Text runsPlayedText;

    void Start()
    {
        StatsManager.OnStatsChanged += Refresh;
    }

    void OnDestroy()
    {
        StatsManager.OnStatsChanged -= Refresh;
    }

    // Re-read stats every time the panel is shown, in case a run finished while it was hidden.
    void OnEnable()
    {
        Refresh();

        // Nested ContentSizeFitter groups (RecordsSection/LifetimeSection inside Content)
        // don't always resolve their height on the very first frame a panel is activated.
        // Force it explicitly, innermost first, so Content stacks everything correctly.
        Canvas.ForceUpdateCanvases();
        if (recordsSection) LayoutRebuilder.ForceRebuildLayoutImmediate(recordsSection);
        if (lifetimeSection) LayoutRebuilder.ForceRebuildLayoutImmediate(lifetimeSection);
        if (content) LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    public void Refresh()
    {
        if (rankText) rankText.text = CurrentRank();
        if (rankSubtitleText) rankSubtitleText.text = "Best run: Wave " + StatsManager.HighestWave;

        if (highestWaveText) highestWaveText.text = StatsManager.HighestWave.ToString();
        if (highestComboText) highestComboText.text = StatsManager.HighestCombo.ToString();
        if (mostCoinsInRunText) mostCoinsInRunText.text = StatsManager.MostCoinsInRun.ToString();
        if (bestAccuracyText) bestAccuracyText.text = StatsManager.BestRunAccuracy.ToString("F1") + "%";

        if (enemiesDestroyedText) enemiesDestroyedText.text = StatsManager.EnemiesDestroyed.ToString();
        if (lettersTypedText) lettersTypedText.text = StatsManager.LettersTyped.ToString();
        if (lifetimeAccuracyText) lifetimeAccuracyText.text = StatsManager.LifetimeAccuracy.ToString("F1") + "%";
        if (totalCoinsText) totalCoinsText.text = StatsManager.TotalCoinsCollected.ToString();
        if (runsPlayedText) runsPlayedText.text = StatsManager.RunsPlayed.ToString();
    }

    // Skill title derived from the live stats and the Inspector-editable rank ladder.
    public string CurrentRank()
    {
        float value = rankMetric == RankMetric.LifetimeAccuracy ? StatsManager.LifetimeAccuracy : StatsManager.HighestWave;
        return RankFor(value);
    }

    // Highest tier whose Min Requirement has been reached; if none has (or the list is empty),
    // falls back to the lowest tier so a rank is always shown.
    string RankFor(float value)
    {
        RankTier best = null, lowest = null;
        if (rankTiers != null)
        {
            foreach (RankTier tier in rankTiers)
            {
                if (tier == null) continue;
                if (lowest == null || tier.minRequirement < lowest.minRequirement) lowest = tier;
                if (value >= tier.minRequirement && (best == null || tier.minRequirement > best.minRequirement)) best = tier;
            }
        }
        if (best != null) return best.rankName;
        return lowest != null ? lowest.rankName : "ROOKIE";
    }
}
