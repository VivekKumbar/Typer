using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Populates the Profile panel from StatsManager. Put this on the ProfilePanel root.
public class ProfilePanelUI : MonoBehaviour
{
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
        if (rankText) rankText.text = RankFor(StatsManager.HighestWave);
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

    // Skill title derived from highest wave reached. Tune the thresholds freely.
    static string RankFor(int highestWave)
    {
        if (highestWave >= 30) return "KEYMASTER";
        if (highestWave >= 15) return "WORDSMITH";
        if (highestWave >= 5) return "TYPIST";
        return "ROOKIE";
    }
}
