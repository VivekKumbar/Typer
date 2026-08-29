using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Tints a button gold when its ability is "ready" (fully charged) — the design
// system's Ready state (#854F0B bg / #FAEEDA text). Whatever color the button
// already has in the Inspector is captured as its "normal" look, so this drops
// onto any existing button without needing normal-state colors configured here.
// Call SetReady(true/false) from whichever HUD script owns the ability's state.
[RequireComponent(typeof(Image))]
public class ReadyStateHighlight : MonoBehaviour
{
    public TMP_Text label;

    static readonly Color ReadyBg = Hex("#854F0B");
    static readonly Color ReadyText = Hex("#FAEEDA");

    Image bg;
    Color normalBg;
    Color normalText;

    void Awake()
    {
        bg = GetComponent<Image>();
        normalBg = bg.color;
        normalText = label ? label.color : Color.white;
    }

    public void SetReady(bool ready)
    {
        if (bg) bg.color = ready ? ReadyBg : normalBg;
        if (label) label.color = ready ? ReadyText : normalText;
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
