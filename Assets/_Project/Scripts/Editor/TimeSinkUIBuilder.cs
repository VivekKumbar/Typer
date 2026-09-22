using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;
using UImage = UnityEngine.UI.Image;

// Builds/rebuilds the Time Sink HUD (ONE bar + button) in the currently open
// GameScene, and the TimeSinkManager that drives them. Reuses the existing
// OverLoadBar/OverLoadButton for style reference, so it matches the game's
// HUD instead of introducing a new look. Re-run any time from the menu
// below — it's idempotent.
//
// NOTE: the gameplay HUD restyle (GameplayHUDBuilder) now owns the actual
// bottom-row position/size/art for TimeSinkButton + its bar — run this
// FIRST if you need to recreate TimeSinkManager/TimeSinkHUD from scratch,
// then GameplayHUDBuilder to lay it out. This builder only used to place
// TWO bars (a duration bar stacked below ShieldBar at the top of the screen,
// separate from the charge bar down at the button); that's what made Time
// Sink read as broken -- charging happened at one button, but the moment it
// activated a second, unrelated-looking bar popped up somewhere else
// entirely. TimeSinkHUD now drives a single bar that shows charge while
// charging and remaining duration while active, so this builder creates
// only one.
public static class TimeSinkUIBuilder
{
    static readonly Color TealFill = Hex("#5DCAA5");

    [MenuItem("TypeKeep/Build Time Sink UI")]
    public static void Build()
    {
        GameObject canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null) { Debug.LogError("[TimeSinkUIBuilder] No 'Canvas' found. Open GameScene.unity first."); return; }
        Transform canvas = canvasGO.transform;

        Transform overloadBar = canvas.Find("OverLoadBar") ?? canvas.Find("OverloadUI/OverLoadBar");
        Transform overloadButton = canvas.Find("OverLoadButton") ?? canvas.Find("OverloadUI/OverLoadButton");
        if (overloadBar == null || overloadButton == null)
        {
            Debug.LogError("[TimeSinkUIBuilder] Expected OverLoadBar/OverLoadButton to already exist in Canvas — this builder reuses their style.");
            return;
        }

        // ---- idempotent cleanup ----
        DestroyIfExists(canvas, "TimeSinkDurationBar"); // old two-bar layout, no longer used
        DestroyIfExists(canvas, "TimeSinkBar");
        DestroyIfExists(canvas, "TimeSinkButton");
        var existingHud = canvasGO.GetComponent<TimeSinkHUD>();
        if (existingHud != null) Object.DestroyImmediate(existingHud);
        GameObject oldManager = GameObject.Find("TimeSinkManager");
        if (oldManager != null) Object.DestroyImmediate(oldManager);

        // ---- TimeSinkManager (its own root object, matching Game manager/ComboManager/ShieldManager) ----
        GameObject managerGO = new GameObject("TimeSinkManager");
        TimeSinkManager manager = managerGO.AddComponent<TimeSinkManager>();
        var spotlight = Object.FindFirstObjectByType<TargetSpotlight>();
        if (spotlight != null)
        {
            // Same danger distances TargetSpotlight already uses, for consistency.
            manager.safeDistance = spotlight.safeDistance;
            manager.dangerDistance = spotlight.dangerDistance;
        }

        // ---- style/position references pulled live, not hardcoded ----
        RectTransform overloadBarRT = overloadBar.GetComponent<RectTransform>();
        RectTransform overloadButtonRT = overloadButton.GetComponent<RectTransform>();

        UImage overloadBg = overloadBar.Find("Background").GetComponent<UImage>();
        UImage overloadFillSrc = overloadBar.Find("Fill Area/Fill").GetComponent<UImage>();
        UImage overloadBtnImg = overloadButton.GetComponent<UImage>();
        TextMeshProUGUI overloadBtnLabel = overloadButton.Find("Text (TMP)").GetComponent<TextMeshProUGUI>();

        Transform parent = overloadBar.parent; // TimeSinkUI-equivalent wrapper, mirrors Overload's

        Slider bar = MakeBar("TimeSinkBar", parent,
            x: overloadBarRT.anchoredPosition.x, y: overloadBarRT.anchoredPosition.y,
            width: overloadBarRT.sizeDelta.x, height: overloadBarRT.sizeDelta.y,
            bgSprite: overloadBg.sprite, bgColor: overloadBg.color,
            fillSprite: overloadFillSrc.sprite, fillColor: TealFill);

        Button activateButton = MakeButton("TimeSinkButton", parent,
            x: overloadButtonRT.anchoredPosition.x, y: overloadButtonRT.anchoredPosition.y,
            width: overloadButtonRT.sizeDelta.x, height: overloadButtonRT.sizeDelta.y,
            sprite: overloadBtnImg.sprite, font: overloadBtnLabel.font, fontSize: overloadBtnLabel.fontSize,
            textColor: overloadBtnLabel.color, label: "TIME SINK", out TextMeshProUGUI buttonLabel);

        UnityAction activateCall = manager.Activate;
        UnityEventTools.AddPersistentListener(activateButton.onClick, activateCall);

        // Gold Ready-state highlight (design system: #854F0B bg / #FAEEDA text).
        ReadyStateHighlight readyHighlight = activateButton.gameObject.AddComponent<ReadyStateHighlight>();
        readyHighlight.label = buttonLabel;

        // ---- TimeSinkHUD on Canvas (alongside HUD/ComboHUD/WaveBanner) ----
        TimeSinkHUD hud = canvasGO.AddComponent<TimeSinkHUD>();
        hud.bar = bar;
        hud.activateButton = activateButton;
        hud.buttonLabel = buttonLabel;
        hud.readyHighlight = readyHighlight;

        EditorUtility.SetDirty(canvasGO);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[TimeSinkUIBuilder] Time Sink HUD built and wired (single bar). Run GameplayHUDBuilder to position it in the bottom ability row.");
    }

    // ---- helpers ----

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

    static void DestroyIfExists(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) Object.DestroyImmediate(t.gameObject);
    }

    static RectTransform MakeRT(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    // Builds a Slider with Background + Fill Area/Fill children, no handle
    // (matching the existing bars), non-interactable with raycasting off so it
    // can never be dragged.
    static Slider MakeBar(string name, Transform parent, float x, float y, float width, float height,
        Sprite bgSprite, Color bgColor, Sprite fillSprite, Color fillColor)
    {
        RectTransform root = MakeRT(name, parent);
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = root.anchorMin;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(width, height);
        root.anchoredPosition = new Vector2(x, y);

        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        slider.minValue = 0; slider.maxValue = 1; slider.value = 0;

        RectTransform bgRT = MakeRT("Background", root);
        Stretch(bgRT);
        UImage bg = bgRT.gameObject.AddComponent<UImage>();
        bg.sprite = bgSprite; bg.type = UImage.Type.Sliced; bg.color = bgColor;
        bg.raycastTarget = false;

        RectTransform fillAreaRT = MakeRT("Fill Area", root);
        Stretch(fillAreaRT);
        RectTransform fillRT = MakeRT("Fill", fillAreaRT);
        fillRT.anchorMin = new Vector2(0, 0); fillRT.anchorMax = new Vector2(0, 1);
        fillRT.offsetMin = Vector2.zero; fillRT.offsetMax = Vector2.zero;
        fillRT.sizeDelta = new Vector2(width, 0); // Slider drives the fill's width via fillRect
        UImage fill = fillRT.gameObject.AddComponent<UImage>();
        fill.sprite = fillSprite; fill.type = UImage.Type.Sliced; fill.color = fillColor;
        fill.raycastTarget = false;

        slider.fillRect = fillRT;
        slider.targetGraphic = null; // no handle to tint

        return slider;
    }

    static Button MakeButton(string name, Transform parent, float x, float y, float width, float height,
        Sprite sprite, TMP_FontAsset font, float fontSize, Color textColor, string label, out TextMeshProUGUI labelTMP)
    {
        RectTransform root = MakeRT(name, parent);
        root.anchorMin = new Vector2(0.5f, 0.5f); root.anchorMax = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(width, height);
        root.anchoredPosition = new Vector2(x, y);

        UImage img = root.gameObject.AddComponent<UImage>();
        img.sprite = sprite; img.type = UImage.Type.Sliced;
        Button button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = img;

        RectTransform labelRT = MakeRT("Text (TMP)", root);
        Stretch(labelRT);
        labelTMP = labelRT.gameObject.AddComponent<TextMeshProUGUI>();
        labelTMP.font = font; labelTMP.fontSize = fontSize; labelTMP.color = textColor;
        labelTMP.alignment = TextAlignmentOptions.Center;
        labelTMP.text = label;

        return button;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
