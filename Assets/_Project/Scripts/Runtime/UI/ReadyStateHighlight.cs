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
    bool captured;

    void Awake()
    {
        bg = GetComponent<Image>();
        // Deliberately NOT captured here: this ran before TMP_Text (on a
        // sibling/child GameObject with its own, order-unspecified Awake) had
        // necessarily finished initializing, so label.color could read back
        // Unity's default Color(0,0,0,0) instead of the authored value --
        // label text silently went fully transparent forever (SetReady(false)
        // "restores" that captured transparency) any time this ran before the
        // label's own init, which is neither guaranteed nor obvious from here.
        // Capturing lazily on first use in Start()/SetReady() instead, after
        // every Awake in the scene (including the label's) has already run.
    }

    void Start() { EnsureCaptured(); }

    void EnsureCaptured()
    {
        if (captured) return;
        if (bg == null) bg = GetComponent<Image>(); // defensive: SetReady() can be called by another script's own Start() before this component's Awake() has necessarily run
        if (bg == null) return; // still missing (component destroyed?) -- try again next call rather than throw
        captured = true;
        normalBg = bg.color;
        normalText = label ? label.color : Color.white;
    }

    public void SetReady(bool ready)
    {
        EnsureCaptured();
        if (bg) bg.color = ready ? ReadyBg : normalBg;
        if (label) label.color = ready ? ReadyText : normalText;
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
