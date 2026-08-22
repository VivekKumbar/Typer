using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Small icon+name element for the Continue popup's word-pack preview row —
// same shape as AbilityPreviewIcon, but no level badge (packs are selected,
// not leveled). One instantiated per pack locked in for the saved run (see
// RunSaveData.selectedWordPackIds / RunContext), plus one "Default Words"
// placeholder entry when no packs were selected at all.
public class WordPackPreviewIcon : MonoBehaviour
{
    public Image iconImage;
    public TMP_Text nameText;

    public void Setup(Sprite icon, string displayName)
    {
        if (iconImage) iconImage.sprite = icon;
        if (nameText) nameText.text = displayName;
    }
}
