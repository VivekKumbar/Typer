using System;

// One achievement's static definition, parsed from AchievementBank's CSV.
// Plain data class (not a ScriptableObject) -- mirrors how WordBank parses
// its word list from an external TextAsset rather than authoring data as
// individual assets, so achievements can be added/edited in Excel.
[Serializable]
public class AchievementData
{
    public string id;
    public string displayName;
    public string description;
    public AchievementMetricType metricType;
    public int targetValue;
    public int coinReward;
    // Lookup key mapped to a real Sprite via an Inspector list on
    // AchievementsPanel (sprites can't live in a CSV).
    public string iconName;
}

// What live stat an achievement's progress is measured against. To support a
// new metric: add a case here, and a matching read in
// AchievementManager.GetProgress(). The CSV's metricType column must spell
// the name the same way (case-insensitive).
public enum AchievementMetricType
{
    EnemiesKilled,
    HighestWave,
    HighestCombo,
    CoinsEarnedLifetime,
    WordsTypedPerfectly,
    RunsPlayed,
}
