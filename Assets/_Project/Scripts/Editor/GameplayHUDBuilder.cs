using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UImage = UnityEngine.UI.Image;

// Restyles the GameScene gameplay HUD to the reference layout:
//   TOP: Pause (left) + Health bar w/ "cur/max" text (center) + Coins (right).
//   BOTTOM: all 4 abilities in one row -- icon button, one bar, one label each.
//
// Reuses every existing GameObject/component (Button, Slider, ReadyStateHighlight,
// ReadyPulse, TimeSinkHUD, ShieldButton, ComboHUD, UpgradeButton, HUD, WalletDisplay)
// exactly as wired -- this only moves/resizes RectTransforms and swaps in the new
// art (Assets/_Project/Art/Textures/HUD/Abilities). No ability logic changes here.
// Idempotent: safe to re-run.
public static class GameplayHUDBuilder
{
    const string ArtDir = "Assets/_Project/Art/Textures/HUD/Abilities/";

    // ---- bottom row layout (anchored to the BOTTOM edge, x relative to canvas center) ----
    const float IconY = 330f;
    const float BarY = 215f;
    const float PlateY = 170f;
    const float IconSize = 195f;
    static readonly Vector2 BarSize = new Vector2(213f, 36f);
    static readonly Vector2 PlateSize = new Vector2(213f, 42f);
    const float ColTimeSink = -384f, ColRepair = -128f, ColShield = 128f, ColOverload = 384f;
    const float ColumnWidth = 230f;
    const float ColumnHeight = 500f;

    [MenuItem("TypeKeep/Build Gameplay HUD (Restyle)")]
    public static void Build()
    {
        GameObject canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null) { Debug.LogError("[GameplayHUDBuilder] No 'Canvas' found. Open GameScene.unity first."); return; }
        Transform canvas = canvasGO.transform;

        BuildTopRow(canvas);
        BuildComboAndBanner(canvas);
        BuildTimeSink(canvas);
        BuildRepair(canvas);
        BuildShield(canvas);
        BuildOverload(canvas);

        EditorUtility.SetDirty(canvasGO);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[GameplayHUDBuilder] Gameplay HUD restyled.");
    }

    // ================= TOP ROW =================

    const float TopRowY = -80f;

    static void BuildTopRow(Transform canvas)
    {
        // ---- Pause: exact button from reference ----
        Transform pause = canvas.Find("PauseButton");
        var pauseImg = pause.GetComponent<UImage>();
        pauseImg.sprite = Load<Sprite>("pause_button_icon");
        pauseImg.type = UImage.Type.Simple;
        pauseImg.preserveAspect = true;
        pauseImg.color = Color.white;
        var pauseRT = pause.GetComponent<RectTransform>();
        pauseRT.anchorMin = pauseRT.anchorMax = new Vector2(0f, 1f);
        pauseRT.anchoredPosition = new Vector2(84f, TopRowY);
        pauseRT.sizeDelta = new Vector2(115f, 115f);
        Transform pauseLabel = pause.Find("Text (TMP)");
        if (pauseLabel != null) pauseLabel.gameObject.SetActive(false); // "II" baked into the icon art

        // ---- Health bar ----
        Transform health = canvas.Find("HealthBar");
        var healthRT = health.GetComponent<RectTransform>();
        healthRT.localScale = Vector3.one;
        healthRT.anchorMin = healthRT.anchorMax = new Vector2(0.5f, 1f);
        healthRT.anchoredPosition = new Vector2(-13f, TopRowY);
        healthRT.sizeDelta = new Vector2(485f, 95f);

        var hSlider = health.GetComponent<Slider>();
        if (hSlider != null)
        {
            hSlider.minValue = 0f;
            hSlider.maxValue = 1f;
            hSlider.interactable = false;
        }

        Transform hBgT = health.Find("Background");
        if (hBgT != null)
        {
            Stretch(hBgT.GetComponent<RectTransform>());
            var hBg = hBgT.GetComponent<UImage>();
            if (hBg != null) { hBg.sprite = Load<Sprite>("health_bar_bg"); hBg.type = UImage.Type.Simple; hBg.color = Color.white; hBg.raycastTarget = false; }
        }

        Transform hFaT = health.Find("Fill Area");
        if (hFaT != null)
        {
            var faRT = hFaT.GetComponent<RectTransform>();
            faRT.anchorMin = Vector2.zero;
            faRT.anchorMax = Vector2.one;
            faRT.offsetMin = new Vector2(10f, 11f);
            faRT.offsetMax = new Vector2(-10f, -11f);
        }

        Transform hFillT = health.Find("Fill Area/Fill");
        if (hFillT != null)
        {
            var hFillRT = hFillT.GetComponent<RectTransform>();
            Stretch(hFillRT);
            var hFill = hFillT.GetComponent<UImage>();
            if (hFill != null)
            {
                hFill.sprite = Load<Sprite>("health_bar_fill");
                hFill.type = UImage.Type.Filled;
                hFill.fillMethod = UImage.FillMethod.Horizontal;
                hFill.fillOrigin = 0;
                hFill.color = Color.white;
                hFill.raycastTarget = false;
            }
            if (hSlider != null) hSlider.fillRect = hFillRT;
        }

        var healthLoadingBar = health.GetComponent<LoadingBarUI>() ?? health.gameObject.AddComponent<LoadingBarUI>();
        healthLoadingBar.bar = hSlider;
        healthLoadingBar.maxFillRatePerSecond = 3f;

        Transform healthTextT = health.Find("HealthText");
        TMP_Text healthText;
        if (healthTextT == null)
        {
            var go = new GameObject("HealthText", typeof(RectTransform));
            go.transform.SetParent(health, false);
            var rt = go.GetComponent<RectTransform>();
            Stretch(rt);
            healthText = go.AddComponent<TextMeshProUGUI>();
            healthText.raycastTarget = false;
        }
        else
        {
            healthText = healthTextT.GetComponent<TextMeshProUGUI>();
            Stretch(healthTextT.GetComponent<RectTransform>());
        }
        healthText.alignment = TextAlignmentOptions.Center;
        healthText.fontSize = 38f;
        healthText.fontStyle = FontStyles.Bold;
        healthText.color = Color.white;
        healthText.text = "100/100";
        var hud = canvas.GetComponent<HUD>();
        if (hud != null)
        {
            hud.healthBar = hSlider;
            hud.healthLoadingBar = healthLoadingBar;
            hud.healthText = healthText;
        }

        // Gold-framed fortress badge immediately to the health bar's left
        Transform badge = canvas.Find("HealthBadge");
        if (badge == null)
        {
            var go = new GameObject("HealthBadge", typeof(RectTransform));
            badge = go.transform;
            badge.SetParent(canvas, false);
            var img = go.AddComponent<UImage>();
            img.raycastTarget = false;
        }
        badge.SetSiblingIndex(health.GetSiblingIndex()); // draw behind the bar so the overlap reads as bar-over-badge
        var badgeImg = badge.GetComponent<UImage>();
        badgeImg.sprite = Load<Sprite>("health_badge_icon");
        badgeImg.preserveAspect = true;
        var badgeRT = badge.GetComponent<RectTransform>();
        badgeRT.anchorMin = badgeRT.anchorMax = new Vector2(0.5f, 1f);
        badgeRT.anchoredPosition = new Vector2(-306f, TopRowY);
        badgeRT.sizeDelta = new Vector2(110f, 131f);

        // ---- Coins: exact coin+pill sprite from master reference ----
        List<Transform> oldPills = new List<Transform>();
        for (int i = 0; i < canvas.childCount; i++)
        {
            var child = canvas.GetChild(i);
            if (child.name == "CoinPill" || child.name == "CoinIcon")
                oldPills.Add(child);
        }
        Transform pill = oldPills.Count > 0 ? oldPills[0] : null;
        Transform coinText = (pill != null ? pill.Find("CoinText") : null) ?? canvas.Find("CoinText");
        for (int i = 1; i < oldPills.Count; i++)
        {
            Object.DestroyImmediate(oldPills[i].gameObject);
        }

        if (pill == null)
        {
            var go = new GameObject("CoinPill", typeof(RectTransform));
            pill = go.transform;
            pill.SetParent(canvas, false);
            var img = go.AddComponent<UImage>();
            img.raycastTarget = false;
        }
        else
        {
            pill.name = "CoinPill";
        }
        pill.SetSiblingIndex(health.GetSiblingIndex() + 1);
        var pillImg = pill.GetComponent<UImage>();
        // Use the same CoinPill.png as the Main Menu (crown coin + slate background)
        pillImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Textures/MainMenuExtras/CoinPill.png");
        pillImg.preserveAspect = true;
        var pillRT = pill.GetComponent<RectTransform>();
        pillRT.anchorMin = pillRT.anchorMax = new Vector2(1f, 1f);
        pillRT.anchoredPosition = new Vector2(-153f, TopRowY);
        pillRT.sizeDelta = new Vector2(260f, 115f);
        pillRT.localScale = Vector3.one;

        if (coinText == null)
        {
            var go = new GameObject("CoinText", typeof(RectTransform));
            coinText = go.transform;
            go.AddComponent<TextMeshProUGUI>();
        }
        var coinRT = coinText.GetComponent<RectTransform>();
        coinRT.SetParent(pill, false);
        // Crown coin takes ~35% of sprite width; text fills the slate area to its right
        coinRT.anchorMin = new Vector2(0.35f, 0.12f);
        coinRT.anchorMax = new Vector2(0.92f, 0.88f);
        coinRT.offsetMin = Vector2.zero; coinRT.offsetMax = Vector2.zero;
        var coinTmp = coinText.GetComponent<TMP_Text>();
        if (coinTmp != null)
        {
            coinTmp.alignment = TextAlignmentOptions.Center;
            coinTmp.enableWordWrapping = false;
            coinTmp.overflowMode = TextOverflowModes.Overflow;
            coinTmp.enableAutoSizing = true;
            coinTmp.fontSizeMin = 24f;
            coinTmp.fontSizeMax = 50f;
            coinTmp.fontStyle = FontStyles.Bold;
            coinTmp.color = new Color(0.98f, 0.95f, 0.88f, 1f); // Warm gold like the Main Menu
            coinTmp.text = "0";
        }
        var wd = canvas.GetComponentInChildren<WalletDisplay>();
        if (wd != null && coinTmp != null) wd.text = coinTmp;
    }

    // ================= COMBO TEXT + WAVE BANNER =================

    static void BuildComboAndBanner(Transform canvas)
    {
        Transform combo = canvas.Find("ComboText");
        if (combo != null) combo.gameObject.SetActive(false);

        Transform panel = canvas.Find("WaveBannerPanel");
        Transform bannerText = (panel != null ? panel.Find("WaveBannerText") : null) ?? canvas.Find("WaveBannerText");
        if (bannerText == null) return;

        if (panel == null)
        {
            var go = new GameObject("WaveBannerPanel", typeof(RectTransform));
            panel = go.transform;
            panel.SetParent(canvas, false);
            panel.SetSiblingIndex(bannerText.GetSiblingIndex());
            var img = go.AddComponent<UImage>();
            img.color = new Color(0f, 0f, 0f, 0.55f);
            img.raycastTarget = false;
        }
        if (bannerText.parent != panel)
        {
            bannerText.SetParent(panel, false);
            Stretch(bannerText.GetComponent<RectTransform>());
        }
        bannerText.gameObject.SetActive(true);
        bannerText.localScale = Vector3.one;
        panel.localScale = Vector3.one;

        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = new Vector2(0f, 420f);
        panelRT.sizeDelta = new Vector2(460f, 140f);

        var bannerTmp = bannerText.GetComponent<TMP_Text>();
        if (bannerTmp != null)
        {
            bannerTmp.enableWordWrapping = false;
            bannerTmp.overflowMode = TextOverflowModes.Overflow;
            bannerTmp.enableAutoSizing = true;
            bannerTmp.fontSizeMin = 40f;
            bannerTmp.fontSizeMax = 96f;
        }

        var banner = canvas.GetComponent<WaveBanner>();
        if (banner != null) banner.panelRoot = panel.gameObject;

        panel.gameObject.SetActive(false);
    }

    // ================= TIME SINK (blue) =================

    static void BuildTimeSink(Transform canvas)
    {
        Transform wrapper = canvas.Find("TimeSinkUI");
        Transform column = GetOrMakeColumn(wrapper, "TimeSinkColumn", ColTimeSink);
        Transform button = FindEither(column, wrapper, "TimeSinkButton");
        Transform oldDuration = wrapper.Find("TimeSinkDurationBar");
        if (oldDuration != null) Object.DestroyImmediate(oldDuration.gameObject);

        Slider bar = FindEither(column, wrapper, "TimeSinkBar").GetComponent<Slider>();
        bar.value = 0.35f;
        PositionButton(button, column, 0f, "timesink_icon");
        Transform label = MoveLabelOut(button, column, wrapper, "TimeSinkLabel", 0f, "timesink_plate", "TIME SINK");
        RestyleBar(bar, column, 0f, "timesink_bar_bg", "timesink_bar_fill");

        var hud = canvas.GetComponent<TimeSinkHUD>();
        if (hud != null)
        {
            hud.bar = bar;
            hud.loadingBar = bar.GetComponent<LoadingBarUI>();
            hud.buttonLabel = null;
        }

        Transform glow = canvas.Find("TimeSinkGlow");
        PositionGlow(glow, ColTimeSink);
        var pulse = button.GetComponent<ReadyPulse>();
        if (pulse != null) pulse.glowImage = glow != null ? glow.GetComponent<UImage>() : null;
    }

    // ================= REPAIR (green) =================

    static void BuildRepair(Transform canvas)
    {
        Transform wrapper = canvas.Find("RepairUI");
        Transform column = GetOrMakeColumn(wrapper, "RepairColumn", ColRepair);
        Transform button = FindEither(column, wrapper, "RepairButton");

        PositionButton(button, column, 0f, "repair_icon");
        Transform label = MoveLabelOut(button, column, wrapper, "RepairLabel", 0f, "repair_plate", "REPAIR");
        var ub = button.GetComponent<UpgradeButton>();
        if (ub != null) ub.labelText = null;

        Transform barT = FindEither(column, wrapper, "RepairBar");
        Slider bar;
        if (barT == null)
        {
            bar = MakeStaticBar("RepairBar", column);
        }
        else
        {
            bar = barT.GetComponent<Slider>();
        }
        bar.value = 0.40f;
        RestyleBar(bar, column, 0f, "repair_bar_bg", "repair_bar_fill");
    }

    // ================= SHIELD (gold) =================

    static void BuildShield(Transform canvas)
    {
        Transform wrapper = canvas.Find("ShieldUI");
        Transform column = GetOrMakeColumn(wrapper, "ShieldColumn", ColShield);
        Transform button = FindEither(column, wrapper, "ShieldButton");

        PositionButton(button, column, 0f, "shield_icon");
        Transform label = MoveLabelOut(button, column, wrapper, "ShieldLabel", 0f, "shield_plate", "SHIELD");
        var sb = button.GetComponent<ShieldButton>();
        if (sb != null) sb.labelText = null;

        Transform barT = FindEither(column, wrapper, "ShieldBar") ?? canvas.Find("ShieldBar");
        Slider bar = barT.GetComponent<Slider>();
        if (barT.parent != column) barT.SetParent(column, false);
        bar.value = 0.45f;
        RestyleBar(bar, column, 0f, "shield_bar_bg", "shield_bar_fill");

        var shieldBarComp = barT.GetComponent<ShieldBar>();
        if (shieldBarComp != null)
        {
            shieldBarComp.bar = bar;
            shieldBarComp.loadingBar = bar.GetComponent<LoadingBarUI>();
            shieldBarComp.barRoot = null;
        }
    }

    // ================= OVERLOAD (red) =================

    static void BuildOverload(Transform canvas)
    {
        Transform wrapper = canvas.Find("OverloadUI");
        Transform column = GetOrMakeColumn(wrapper, "OverloadColumn", ColOverload);
        Transform button = FindEither(column, wrapper, "OverLoadButton");

        PositionButton(button, column, 0f, "overload_icon");
        Transform label = MoveLabelOut(button, column, wrapper, "OverloadLabel", 0f, "overload_plate", "OVERLOAD");

        Slider bar = FindEither(column, wrapper, "OverLoadBar").GetComponent<Slider>();
        bar.value = 0.40f;
        RestyleBar(bar, column, 0f, "overload_bar_bg", "overload_bar_fill");

        var comboHud = canvas.GetComponent<ComboHUD>();
        if (comboHud != null)
        {
            comboHud.overloadBar = bar;
            comboHud.overloadLoadingBar = bar.GetComponent<LoadingBarUI>();
        }

        Transform glow = canvas.Find("OverloadGlow");
        PositionGlow(glow, ColOverload);
        var pulse = button.GetComponent<ReadyPulse>();
        if (pulse != null) pulse.glowImage = glow != null ? glow.GetComponent<UImage>() : null;
    }

    // ================= shared helpers =================

    static Transform GetOrMakeColumn(Transform wrapper, string name, float x)
    {
        Transform column = wrapper.Find(name);
        if (column == null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            column = go.transform;
            column.SetParent(wrapper, false);
        }
        var rt = column.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(ColumnWidth, ColumnHeight);

        var mask = column.GetComponent<UnityEngine.UI.RectMask2D>();
        if (mask != null) Object.DestroyImmediate(mask);
        var img = column.GetComponent<UImage>();
        if (img != null && img.color.a == 0f) Object.DestroyImmediate(img);

        return column;
    }

    static Transform FindEither(Transform column, Transform wrapper, string name)
    {
        return column.Find(name) ?? wrapper.Find(name);
    }

    static void PositionButton(Transform button, Transform column, float x, string iconSprite)
    {
        if (button.parent != column) button.SetParent(column, false);
        var rt = button.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, IconY);
        rt.sizeDelta = new Vector2(IconSize, IconSize);

        var bg = button.GetComponent<UImage>();
        if (bg != null) bg.color = new Color(bg.color.r, bg.color.g, bg.color.b, 0f);

        Transform iconT = button.Find("Icon");
        UImage icon;
        if (iconT == null)
        {
            var go = new GameObject("Icon", typeof(RectTransform));
            go.transform.SetParent(button, false);
            var irt = go.GetComponent<RectTransform>();
            Stretch(irt);
            icon = go.AddComponent<UImage>();
            icon.raycastTarget = false;
        }
        else
        {
            icon = iconT.GetComponent<UImage>();
            Stretch(iconT.GetComponent<RectTransform>());
        }
        icon.sprite = Load<Sprite>(iconSprite);
        icon.preserveAspect = true;
    }

    static Transform MoveLabelOut(Transform button, Transform column, Transform wrapper, string newName, float x, string plateSpriteName, string defaultText)
    {
        string plateName = newName.Replace("Label", "Plate");
        Transform plate = column.Find(plateName);
        if (plate == null)
        {
            var pgo = new GameObject(plateName, typeof(RectTransform));
            plate = pgo.transform;
            plate.SetParent(column, false);
            var pImg = pgo.AddComponent<UImage>();
            pImg.raycastTarget = false;
        }

        var prt = plate.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = new Vector2(x, PlateY);
        prt.sizeDelta = PlateSize;

        var img = plate.GetComponent<UImage>();
        img.sprite = Load<Sprite>(plateSpriteName);
        img.type = UImage.Type.Simple;
        img.preserveAspect = true;
        img.color = Color.white;
        img.enabled = true;
        img.raycastTarget = false;

        Transform label = column.Find(newName) ?? wrapper.Find(newName) ?? button.Find("Text (TMP)");
        if (label == null) return null;
        label.name = newName;
        label.SetParent(column, false);

        plate.SetSiblingIndex(0);
        label.SetSiblingIndex(plate.GetSiblingIndex() + 1);

        var rt = label.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, PlateY);
        rt.sizeDelta = new Vector2(PlateSize.x - 20f, PlateSize.y);

        var tmp = label.GetComponent<TMP_Text>();
        if (tmp != null)
        {
            tmp.text = ""; // Sprite contains embossed text
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 14f;
            tmp.fontSizeMax = 22f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.985f, 0.945f, 0.865f, 1f);
        }
        return label;
    }

    static void RestyleBar(Slider bar, Transform column, float x, string bgSprite, string fillSprite)
    {
        if (bar.transform.parent != column) bar.transform.SetParent(column, false);
        var rt = bar.GetComponent<RectTransform>();
        rt.localScale = Vector3.one;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, BarY);
        rt.sizeDelta = BarSize;

        bar.interactable = false;
        bar.minValue = 0f;
        bar.maxValue = 1f;

        var bgT = bar.transform.Find("Background");
        if (bgT != null)
        {
            var bgRT = bgT.GetComponent<RectTransform>();
            Stretch(bgRT);
            var bg = bgT.GetComponent<UImage>();
            if (bg != null) { bg.sprite = Load<Sprite>(bgSprite); bg.type = UImage.Type.Simple; bg.color = Color.white; bg.raycastTarget = false; }
        }

        var fillAreaT = bar.transform.Find("Fill Area");
        if (fillAreaT != null)
        {
            var faRT = fillAreaT.GetComponent<RectTransform>();
            faRT.anchorMin = Vector2.zero;
            faRT.anchorMax = Vector2.one;
            faRT.offsetMin = new Vector2(8f, 5f);
            faRT.offsetMax = new Vector2(-8f, -5f);
        }

        var fillT = bar.transform.Find("Fill Area/Fill");
        if (fillT != null)
        {
            var fillRT = fillT.GetComponent<RectTransform>();
            Stretch(fillRT);
            var fill = fillT.GetComponent<UImage>();
            if (fill != null)
            {
                fill.sprite = Load<Sprite>(fillSprite);
                fill.type = UImage.Type.Filled;
                fill.fillMethod = UImage.FillMethod.Horizontal;
                fill.fillOrigin = 0; // Left
                fill.color = Color.white;
                fill.raycastTarget = false;
            }
            bar.fillRect = fillRT;
        }

        var loadingBar = bar.GetComponent<LoadingBarUI>() ?? bar.gameObject.AddComponent<LoadingBarUI>();
        loadingBar.bar = bar;
        loadingBar.maxFillRatePerSecond = 4f;

        bar.interactable = false;
        foreach (var g in bar.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
    }

    static Slider MakeStaticBar(string name, Transform parent)
    {
        var root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        var rt = root.GetComponent<RectTransform>();
        rt.localScale = Vector3.one;
        rt.pivot = new Vector2(0.5f, 0.5f);

        Slider slider = root.AddComponent<Slider>();
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = 0.40f;

        var bgGO = new GameObject("Background", typeof(RectTransform));
        bgGO.transform.SetParent(root.transform, false);
        Stretch(bgGO.GetComponent<RectTransform>());
        var bg = bgGO.AddComponent<UImage>();
        bg.raycastTarget = false;

        var fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGO.transform.SetParent(root.transform, false);
        var faRT = fillAreaGO.GetComponent<RectTransform>();
        faRT.anchorMin = Vector2.zero; faRT.anchorMax = Vector2.one;
        faRT.offsetMin = new Vector2(8f, 5f); faRT.offsetMax = new Vector2(-8f, -5f);

        var fillGO = new GameObject("Fill", typeof(RectTransform));
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        var fillRT = fillGO.GetComponent<RectTransform>();
        Stretch(fillRT);
        var fill = fillGO.AddComponent<UImage>();
        fill.type = UImage.Type.Filled;
        fill.fillMethod = UImage.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 0.40f;
        fill.raycastTarget = false;

        slider.fillRect = fillRT;
        slider.targetGraphic = null;

        var loadingBar = root.AddComponent<LoadingBarUI>();
        loadingBar.bar = slider;
        loadingBar.maxFillRatePerSecond = 4f;

        return slider;
    }

    static void PositionGlow(Transform glow, float x)
    {
        if (glow == null) return;
        var rt = glow.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, IconY);
        rt.sizeDelta = new Vector2(240f, 240f);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static T Load<T>(string spriteFileNameNoExt) where T : Object
    {
        string path = ArtDir + spriteFileNameNoExt + ".png";
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) Debug.LogError("[GameplayHUDBuilder] Could not load " + typeof(T).Name + " at " + path);
        return asset;
    }
}
