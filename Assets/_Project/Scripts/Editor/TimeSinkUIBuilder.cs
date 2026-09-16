using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;
using UImage = UnityEngine.UI.Image;

// Builds/rebuilds the Time Sink HUD (duration bar + charge bar + button) in the
// currently open GameScene, and the TimeSinkManager that drives them. Reuses the
// existing ShieldBar/OverLoadBar/OverLoadButton/RepairButton for style and
// position reference, so it matches the game's HUD instead of introducing a new
// look. Re-run any time from the menu below — it's idempotent.
public static class TimeSinkUIBuilder
{
    static readonly Color TealFill = Hex("#5DCAA5");

    const float GapBelowShieldBar = 16f;
    const float DurationBarHeight = 24f;
    const float DurationBarWidth = 500f;

    [MenuItem("TypeKeep/Build Time Sink UI")]
    public static void Build()
    {
        GameObject canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null) { Debug.LogError("[TimeSinkUIBuilder] No 'Canvas' found. Open GameScene.unity first."); return; }
        Transform canvas = canvasGO.transform;

        Transform shieldBar = canvas.Find("ShieldBar");
        Transform shieldButton = canvas.Find("ShieldButton");
        Transform overloadBar = canvas.Find("OverLoadBar");
        Transform overloadButton = canvas.Find("OverLoadButton");
        Transform repairButton = canvas.Find("RepairButton");
        if (shieldBar == null || shieldButton == null || overloadBar == null || overloadButton == null || repairButton == null)
        {
            Debug.LogError("[TimeSinkUIBuilder] Expected ShieldBar/ShieldButton/OverLoadBar/OverLoadButton/RepairButton to already exist in Canvas — this builder reuses their style and position.");
            return;
        }

        // ---- idempotent cleanup ----
        DestroyIfExists(canvas, "TimeSinkDurationBar");
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
        RectTransform shieldBarRT = shieldBar.GetComponent<RectTransform>();
        RectTransform shieldButtonRT = shieldButton.GetComponent<RectTransform>();
        RectTransform overloadBarRT = overloadBar.GetComponent<RectTransform>();
        RectTransform overloadButtonRT = overloadButton.GetComponent<RectTransform>();
        RectTransform repairRT = repairButton.GetComponent<RectTransform>();

        UImage overloadBg = overloadBar.Find("Background").GetComponent<UImage>();
        UImage overloadFillSrc = overloadBar.Find("Fill Area/Fill").GetComponent<UImage>();
        UImage overloadBtnImg = overloadButton.GetComponent<UImage>();
        TextMeshProUGUI overloadBtnLabel = overloadButton.Find("Text (TMP)").GetComponent<TextMeshProUGUI>();

        // ---- TOP: duration bar — top-center anchor, stacked below ShieldBar ----
        float durationY = shieldBarRT.anchoredPosition.y - shieldBarRT.sizeDelta.y / 2f - GapBelowShieldBar - DurationBarHeight / 2f;
        Slider durationSlider = MakeBar("TimeSinkDurationBar", canvas,
            anchorTop: true, x: shieldBarRT.anchoredPosition.x, y: durationY,
            width: DurationBarWidth, height: DurationBarHeight,
            bgSprite: overloadBg.sprite, bgColor: overloadBg.color,
            fillSprite: overloadFillSrc.sprite, fillColor: TealFill);
        durationSlider.gameObject.SetActive(false); // TimeSinkHUD toggles this; starts hidden

        // ---- BOTTOM: charge bar + button — mirrors Overload's slot, on the left (Repair's) column ----
        float colX = repairRT.anchoredPosition.x;
        float buttonY = shieldButtonRT.anchoredPosition.y;   // mirrors ShieldButton's slot
        float barY = overloadButtonRT.anchoredPosition.y;    // mirrors OverLoadButton's slot

        Slider chargeSlider = MakeBar("TimeSinkBar", canvas,
            anchorTop: false, x: colX, y: barY,
            width: overloadBarRT.sizeDelta.x, height: overloadBarRT.sizeDelta.y,
            bgSprite: overloadBg.sprite, bgColor: overloadBg.color,
            fillSprite: overloadFillSrc.sprite, fillColor: TealFill);

        Button activateButton = MakeButton("TimeSinkButton", canvas,
            x: colX, y: buttonY, width: overloadButtonRT.sizeDelta.x, height: overloadButtonRT.sizeDelta.y,
            sprite: overloadBtnImg.sprite, font: overloadBtnLabel.font, fontSize: overloadBtnLabel.fontSize,
            textColor: overloadBtnLabel.color, label: "TIME SINK", out TextMeshProUGUI buttonLabel);

        UnityAction activateCall = manager.Activate;
        UnityEventTools.AddPersistentListener(activateButton.onClick, activateCall);

        // Gold Ready-state highlight (design system: #854F0B bg / #FAEEDA text).
        ReadyStateHighlight readyHighlight = activateButton.gameObject.AddComponent<ReadyStateHighlight>();
        readyHighlight.label = buttonLabel;

        // ---- TimeSinkHUD on Canvas (alongside HUD/ComboHUD/WaveBanner) ----
        TimeSinkHUD hud = canvasGO.AddComponent<TimeSinkHUD>();
        hud.durationBarRoot = durationSlider.gameObject;
        hud.durationBar = durationSlider;
        hud.chargeBar = chargeSlider;
        hud.activateButton = activateButton;
        hud.buttonLabel = buttonLabel;
        hud.readyHighlight = readyHighlight;

        EditorUtility.SetDirty(canvasGO);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[TimeSinkUIBuilder] Time Sink HUD built and wired.");
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
    static Slider MakeBar(string name, Transform parent, bool anchorTop, float x, float y, float width, float height,
        Sprite bgSprite, Color bgColor, Sprite fillSprite, Color fillColor)
    {
        RectTransform root = MakeRT(name, parent);
        root.anchorMin = anchorTop ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.5f);
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
