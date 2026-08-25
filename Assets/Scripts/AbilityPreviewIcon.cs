using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Small icon+level element for the Continue popup's build-preview row. One
// instantiated per unlocked upgrade (level > 0) at save time, read from
// RunSaveData via MainMenu's own UpgradePool reference — UpgradeManager.Instance
// doesn't exist in the Main Menu scene, so the pool has to be resolved directly
// rather than through the runtime singleton.
public class AbilityPreviewIcon : MonoBehaviour
{
    public Image iconImage;
    public TMP_Text levelText;
    [Tooltip("Shown only for boss-level (6) upgrades — a distinct border/glow behind the icon.")]
    public GameObject bossBadge;

    public void Setup(UpgradeDefinition def, int level)
    {
        bool isBoss = level >= UpgradeDefinition.BossLevel;

        if (iconImage) iconImage.sprite = (isBoss && def.bossIcon != null) ? def.bossIcon : def.icon;
        if (levelText) levelText.text = isBoss ? "MAX" : ("Lv." + level);
        if (bossBadge) bossBadge.SetActive(isBoss);
    }
}
