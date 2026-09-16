using UnityEngine;

public enum UpgradeCategory { Offense, Defense, Economy, Utility }

// One draftable upgrade. Create assets via:
// right-click -> Create -> TypeKeep -> Upgrades -> Upgrade.
// Add as many as you want to an UpgradePool; the draft reads them from there,
// no code changes. Levels 1-5 are the normal track; level 6 is the "boss"
// upgrade — a distinct, stronger version with its own name/description/icon.
[CreateAssetMenu(fileName = "UpgradeDefinition", menuName = "TypeKeep/Upgrades/Upgrade")]
public class UpgradeDefinition : ScriptableObject
{
    // Level 5 is the last normal level; picking it again promotes to BossLevel.
    public const int MaxNormalLevel = 5;
    public const int BossLevel = 6;

    [Header("Identity")]
    [Tooltip("UNIQUE id — used to track this run's level for this upgrade. Never reuse or rename.")]
    public string id = "upgrade_id";
    public string displayName = "New Upgrade";
    [TextArea] public string description = "";
    public Sprite icon;
    public UpgradeCategory category = UpgradeCategory.Utility;

    [Header("Per-level values (index 0 = level 1 ... index 4 = level 5)")]
    [Tooltip("Meaning depends on the upgrade — read by whichever system implements its effect (e.g. a percent, a flat amount, a duration in seconds). Make each level stronger than the last.")]
    public float[] levelValues = new float[MaxNormalLevel] { 1f, 2f, 3f, 4f, 5f };

    [Header("Boss upgrade (level 6 — the super version)")]
    public string bossName = "Boss Upgrade";
    [TextArea] public string bossDescription = "";
    public Sprite bossIcon;
    [Tooltip("Effect value for the boss (level 6) version. Same meaning as the per-level values above, just the strongest tier.")]
    public float bossValue = 10f;

    // Resolves the effect value for a given level (0 = not owned -> 0,
    // 1-5 -> levelValues, 6+ -> bossValue).
    public float ValueForLevel(int level)
    {
        if (level <= 0) return 0f;
        if (level >= BossLevel) return bossValue;
        if (levelValues == null || levelValues.Length == 0) return 0f;
        int idx = Mathf.Clamp(level - 1, 0, levelValues.Length - 1);
        return levelValues[idx];
    }
}
