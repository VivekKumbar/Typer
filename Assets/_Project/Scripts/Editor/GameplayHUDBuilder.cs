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
    const float IconY = 300f;
    const float BarY = 205f;
    const float LabelY = 150f;
    const float IconSize = 150f;
    static readonly Vector2 BarSize = new Vector2(150f, 20f);
    static readonly Vector2 LabelSize = new Vector2(190f, 40f);
    const float ColTimeSink = -378f, ColRepair = -126f, ColShield = 126f, ColOverload = 378f;
    // Spacing between column centers is 252 -- keep each column's own hard-clipped
    // width safely under that so neighboring columns can never touch, whatever the
    // label text turns out to be at runtime (see BUG 5: "SHIELD UP" bled into REPAIR's
    // column because nothing actually clipped a label to its own slot before this).
    const float ColumnWidth = 220f;
    const float ColumnHeight = 420f;

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

    // All three top-row elements share one row baseline, cropped directly from
    // the final reference composite (2026-09-22 02_04_06 PM) at its own native
    // scale (943px wide) and converted to this canvas's 1080-wide reference
    // units by the same factor (1080/943 = 1.1453) for every measurement below.
    const float TopRowY = -80f;

    static void BuildTopRow(Transform canvas)
    {
        // ---- Pause: exact square button from the reference (NOT the circular
        // ability-style badge used at the bottom -- the reference uses a
        // distinct square/octagon-cut icon for Pause specifically). ----
        Transform pause = canvas.Find("PauseButton");
        var pauseImg = pause.GetComponent<UImage>();
        pauseImg.sprite = Load<Sprite>("pause_button_icon");
        pauseImg.type = UImage.Type.Simple;
        pauseImg.preserveAspect = true;
        pauseImg.color = Color.white;
        var pauseRT = pause.GetComponent<RectTransform>();
        pauseRT.anchorMin = pauseRT.anchorMax = new Vector2(0f, 1f);
        pauseRT.anchoredPosition = new Vector2(83f, TopRowY);
        pauseRT.sizeDelta = new Vector2(143f, 149f);
        Transform pauseLabel = pause.Find("Text (TMP)");
        if (pauseLabel != null) pauseLabel.gameObject.SetActive(false); // "II" baked into the icon art

        // ---- Health bar ----
        Transform health = canvas.Find("HealthBar");
        var healthRT = health.GetComponent<RectTransform>();
        healthRT.anchorMin = healthRT.anchorMax = new Vector2(0.5f, 1f);
        healthRT.anchoredPosition = new Vector2(2f, TopRowY);
        healthRT.sizeDelta = new Vector2(514f, 110f);

        // Type.Simple, not Sliced: these bars are rendered much shorter than the
        // source art's native height, and a Sliced 9-slice whose top+bottom
        // border (from the imported sprite's border metadata) adds up to MORE
        // than the target rect height degenerates -- Unity has no room left for
        // a middle slice and the bar renders as a sliver or nothing at all.
        var hBg = health.Find("Background").GetComponent<UImage>();
        hBg.sprite = Load<Sprite>("health_bar_bg"); hBg.type = UImage.Type.Simple; hBg.color = Color.white;
        var hFill = health.Find("Fill Area/Fill").GetComponent<UImage>();
        hFill.sprite = Load<Sprite>("health_bar_fill"); hFill.type = UImage.Type.Simple; hFill.color = Color.white;

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
        }
        healthText.alignment = TextAlignmentOptions.Center; // white, centered ON the bar -- matches reference exactly
        healthText.fontSize = 34f;
        healthText.color = Color.white;
        var hud = canvas.GetComponent<HUD>();
        if (hud != null) hud.healthText = healthText;

        // Gold-framed fortress/shield badge immediately to the health bar's
        // left, slightly overlapping its rounded end cap -- present in the
        // reference and cropped directly from it (no such badge existed among
        // the previously-imported assets; this is a new crop, not new art).
        Transform badge = canvas.Find("HealthBadge");
        if (badge == null)
        {
            var go = new GameObject("HealthBadge", typeof(RectTransform));
            badge = go.transform;
            badge.SetParent(canvas, false);
            badge.SetSiblingIndex(health.GetSiblingIndex()); // draw behind the bar so the overlap reads as bar-over-badge, matching the reference
            var img = go.AddComponent<UImage>();
            img.raycastTarget = false;
        }
        var badgeImg = badge.GetComponent<UImage>();
        badgeImg.sprite = Load<Sprite>("health_badge_icon");
        badgeImg.preserveAspect = true;
        var badgeRT = badge.GetComponent<RectTransform>();
        badgeRT.anchorMin = badgeRT.anchorMax = new Vector2(0.5f, 1f);
        badgeRT.anchoredPosition = new Vector2(-311f, TopRowY);
        badgeRT.sizeDelta = new Vector2(119f, 142f);

        // ---- Coins: ONE fused coin+pill sprite (cropped from the same
        // reference), CoinText laid over its dark text plate. Replaces the
        // earlier separate coin-icon + plain-text approach entirely. ----
        // Idempotent: a re-run finds CoinText already reparented under CoinPill,
        // and CoinPill already renamed from its original CoinIcon/none.
        Transform pill = canvas.Find("CoinPill") ?? canvas.Find("CoinIcon");
        Transform coinText = (pill != null ? pill.Find("CoinText") : null) ?? canvas.Find("CoinText");
        if (pill == null)
        {
            var go = new GameObject("CoinPill", typeof(RectTransform));
            pill = go.transform;
            pill.SetParent(canvas, false);
            pill.SetSiblingIndex(coinText.GetSiblingIndex());
            var img = go.AddComponent<UImage>();
            img.raycastTarget = false;
        }
        else
        {
            pill.name = "CoinPill"; // reuse the old standalone-icon object's slot, repurposed
        }
        var pillImg = pill.GetComponent<UImage>();
        pillImg.sprite = Load<Sprite>("coin_pill");
        pillImg.preserveAspect = true;
        var pillRT = pill.GetComponent<RectTransform>();
        pillRT.anchorMin = pillRT.anchorMax = new Vector2(1f, 1f);
        pillRT.anchoredPosition = new Vector2(-160f, TopRowY); // -(margin 20 + width/2 140.5): the box's EDGE stays inboard of the screen, not just its center -- see BUG 3's postmortem on ComboText for why the naive "-20" reads wrong here
        pillRT.sizeDelta = new Vector2(281f, 133f);

        var coinRT = coinText.GetComponent<RectTransform>();
        coinRT.SetParent(pill, false); // child of the pill, so it moves/scales with it as one unit -- reparent BEFORE setting anchors, since anchors are relative to whatever the current parent is at assignment time
        coinRT.anchorMin = new Vector2(0.42f, 0.12f); // the pill's own dark text-plate region, not its full width (which includes the round coin knob on the left)
        coinRT.anchorMax = new Vector2(0.94f, 0.88f);
        coinRT.offsetMin = Vector2.zero; coinRT.offsetMax = Vector2.zero;
        var coinTmp = coinText.GetComponent<TMP_Text>();
        if (coinTmp != null)
        {
            coinTmp.alignment = TextAlignmentOptions.Center;
            coinTmp.enableWordWrapping = false;
            coinTmp.overflowMode = TextOverflowModes.Overflow;
            coinTmp.enableAutoSizing = true;
            coinTmp.fontSizeMin = 24f;
            coinTmp.fontSizeMax = 56f;
            coinTmp.color = Color.white;
        }
    }

    // ================= COMBO TEXT + WAVE BANNER =================

    static void BuildComboAndBanner(Transform canvas)
    {
        // ---- Combo/multiplier text: the exact top-bar reference has room for
        // exactly 3 elements (Pause, Health, Coins) and nothing else -- Combo
        // text has no clean spot there without crowding Coins (see the prior
        // pass's postmortem, still in git history), and the reference's center
        // field and bottom row are equally spoken for. Per instruction, default
        // to hiding it entirely rather than inventing a new location the
        // reference doesn't show. ComboManager/ComboHUD keep running unchanged
        // underneath -- this only hides the label.
        Transform combo = canvas.Find("ComboText");
        if (combo != null) combo.gameObject.SetActive(false);

        // ---- Wave banner ("WAVE n" announce + the 5-4-3-2-1 countdown, same
        // TMP object -- see WaveManager.Countdown / WaveBanner.ShowRaw): was
        // dead-center anchor (0,0), which is almost exactly the fortress's own
        // screen position, so any countdown digit rendered directly on top of
        // the 3D shield-bubble FX with nothing behind it (BUG 4). Wrap it in a
        // backdrop panel and move the whole thing up to a clear band above the
        // fortress instead.
        // Idempotent: a re-run finds it already reparented under the panel --
        // canvas.Find("WaveBannerText") alone stops matching after the first
        // run, which silently no-op'd this ENTIRE block (including the panel
        // sizing fix below) on every subsequent Build() call.
        Transform panel = canvas.Find("WaveBannerPanel");
        Transform bannerText = (panel != null ? panel.Find("WaveBannerText") : null) ?? canvas.Find("WaveBannerText");
        if (bannerText == null) return;

        if (panel == null)
        {
            var go = new GameObject("WaveBannerPanel", typeof(RectTransform));
            panel = go.transform;
            panel.SetParent(canvas, false);
            panel.SetSiblingIndex(bannerText.GetSiblingIndex()); // draw in the same place in the order it used to
            var img = go.AddComponent<UImage>();
            img.color = new Color(0f, 0f, 0f, 0.55f); // simple dark backdrop plate -- no bespoke banner asset was provided
            img.raycastTarget = false;
        }
        if (bannerText.parent != panel)
        {
            bannerText.SetParent(panel, false);
            Stretch(bannerText.GetComponent<RectTransform>());
        }
        bannerText.gameObject.SetActive(true); // the PANEL now owns show/hide; the text object itself must stay active
        // The actual root cause of the text overflowing its backdrop: this
        // Transform carried a leftover authored localScale of ~2.73 (nothing
        // in code animates it -- baked into the scene from long before this
        // pass), invisible in every RectTransform/TMP_Text inspection because
        // rect.width/preferredWidth are in LOCAL units and don't reflect a
        // parent-independent scale on the object itself. Reset both to 1.
        bannerText.localScale = Vector3.one;
        panel.localScale = Vector3.one;

        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = new Vector2(0f, 420f); // clear of the fortress/shield bubble, well above the top HUD row's bottom edge and the bottom ability row's top edge
        panelRT.sizeDelta = new Vector2(460f, 140f); // fits "WAVE 10"/"WAVE 99" and a single big countdown digit without the text overflowing its own backdrop

        var bannerTmp = bannerText.GetComponent<TMP_Text>();
        if (bannerTmp != null)
        {
            bannerTmp.enableWordWrapping = false; // one line always -- wrapping would fight autosize and can still overflow the panel vertically
            bannerTmp.overflowMode = TextOverflowModes.Overflow;
            bannerTmp.enableAutoSizing = true;
            bannerTmp.fontSizeMin = 40f;
            bannerTmp.fontSizeMax = 96f;
        }

        var banner = canvas.GetComponent<WaveBanner>();
        if (banner != null) banner.panelRoot = panel.gameObject;

        // Author-time default: hidden until WaveManager calls Show/ShowRaw (WaveBanner.Awake
        // does this too at runtime, but keep the saved scene's authored state honest).
        panel.gameObject.SetActive(false);
    }

    // ================= TIME SINK (blue) =================

    static void BuildTimeSink(Transform canvas)
    {
        Transform wrapper = canvas.Find("TimeSinkUI");
        Transform column = GetOrMakeColumn(wrapper, "TimeSinkColumn", ColTimeSink);
        Transform button = FindEither(column, wrapper, "TimeSinkButton");
        Transform oldDuration = wrapper.Find("TimeSinkDurationBar");
        if (oldDuration != null) Object.DestroyImmediate(oldDuration.gameObject); // Part B (earlier pass): old two-bar layout, gone for good

        Slider bar = FindEither(column, wrapper, "TimeSinkBar").GetComponent<Slider>();
        PositionButton(button, column, 0f, "timesink_icon");
        Transform label = MoveLabelOut(button, column, wrapper, "TimeSinkLabel", 0f);
        RestyleBar(bar, column, 0f, "timesink_bar_bg", "timesink_bar_fill");

        var hud = canvas.GetComponent<TimeSinkHUD>();
        if (hud != null)
        {
            hud.bar = bar;
            hud.buttonLabel = label.GetComponent<TMP_Text>();
        }

        // Glow FX: was mis-wired to OverloadGlow (copy/paste leftover) -- point it at its own glow.
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
        Transform label = MoveLabelOut(button, column, wrapper, "RepairLabel", 0f);

        // Repair (UpgradeButton) is an instant coin-purchase heal -- it has no
        // charge/cooldown/ready state anywhere in the code, unlike the other
        // three abilities. There is nothing meaningful to drive a fill amount
        // from without inventing a new mechanic, which is out of scope for a
        // layout pass. So this bar is static (always full) -- purely visual,
        // to match the reference's icon+bar+label pattern for every ability.
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
        bar.value = 1f;
        RestyleBar(bar, column, 0f, "repair_bar_bg", "repair_bar_fill");
    }

    // ================= SHIELD (gold) =================

    static void BuildShield(Transform canvas)
    {
        Transform wrapper = canvas.Find("ShieldUI");
        Transform column = GetOrMakeColumn(wrapper, "ShieldColumn", ColShield);
        Transform button = FindEither(column, wrapper, "ShieldButton");

        PositionButton(button, column, 0f, "shield_icon");
        Transform label = MoveLabelOut(button, column, wrapper, "ShieldLabel", 0f);

        // ShieldBar used to sit under the Health bar at the TOP of the screen
        // (see ShieldBar.cs's own comment). Per the reference layout every
        // ability's bar lives in the bottom row with its icon -- move it down;
        // ShieldBar.cs itself is untouched, it just drives whichever Slider is assigned.
        Transform barT = FindEither(column, wrapper, "ShieldBar") ?? canvas.Find("ShieldBar"); // pre-restyle location, first-ever run only
        Slider bar = barT.GetComponent<Slider>();
        if (barT.parent != column) barT.SetParent(column, false);
        RestyleBar(bar, column, 0f, "shield_bar_bg", "shield_bar_fill");

        var shieldBarComp = barT.GetComponent<ShieldBar>();
        if (shieldBarComp != null) shieldBarComp.barRoot = barT.gameObject; // unchanged behavior, just confirms wiring survives the move
    }

    // ================= OVERLOAD (red) =================

    static void BuildOverload(Transform canvas)
    {
        Transform wrapper = canvas.Find("OverloadUI");
        Transform column = GetOrMakeColumn(wrapper, "OverloadColumn", ColOverload);
        Transform button = FindEither(column, wrapper, "OverLoadButton");

        PositionButton(button, column, 0f, "overload_icon");
        Transform label = MoveLabelOut(button, column, wrapper, "OverloadLabel", 0f);

        Slider bar = FindEither(column, wrapper, "OverLoadBar").GetComponent<Slider>();
        RestyleBar(bar, column, 0f, "overload_bar_bg", "overload_bar_fill");

        Transform glow = canvas.Find("OverloadGlow");
        PositionGlow(glow, ColOverload);
        var pulse = button.GetComponent<ReadyPulse>();
        if (pulse != null) pulse.glowImage = glow != null ? glow.GetComponent<UImage>() : null;
    }

    // ================= shared helpers =================

    // BUG 5 fix: each ability now gets its own hard-clipped column container
    // (RectMask2D) sized well under the 252px column spacing, and everything
    // that ability owns -- icon button, bar, label -- is parented inside it.
    // Whatever a label's text turns out to be at runtime, it is physically
    // impossible for it to render into a neighboring column: the mask cuts it
    // off at the column's own edge, 16px inside the gap to the next column.
    static Transform GetOrMakeColumn(Transform wrapper, string name, float x)
    {
        Transform column = wrapper.Find(name);
        if (column == null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            column = go.transform;
            column.SetParent(wrapper, false);
            go.AddComponent<UImage>().color = new Color(0, 0, 0, 0); // RectMask2D needs a Graphic to mask against
            go.AddComponent<RectMask2D>();
        }
        var rt = column.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(ColumnWidth, ColumnHeight);
        return column;
    }

    // Old scenes had these objects directly under the ability's wrapper; once
    // migrated to a column they live under the column instead. Idempotent
    // either way.
    static Transform FindEither(Transform column, Transform wrapper, string name)
    {
        return column.Find(name) ?? wrapper.Find(name);
    }

    // Resizes/repositions an ability button into its column and gives it an
    // icon CHILD image (not a sprite swap on the button's own Image) --
    // ReadyStateHighlight/ReadyPulse already target the button's own Image
    // component and must keep working unmodified, so that Image stays where
    // it is (just made a near-invisible backing) and the colorful icon art
    // sits on top, non-raycast, so clicks still reach the Button.
    static void PositionButton(Transform button, Transform column, float x, string iconSprite)
    {
        if (button.parent != column) button.SetParent(column, false);
        var rt = button.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(x, IconY);
        rt.sizeDelta = new Vector2(IconSize, IconSize);

        var bg = button.GetComponent<UImage>();
        if (bg != null) bg.color = new Color(bg.color.r, bg.color.g, bg.color.b, 0f); // invisible normal state; ReadyStateHighlight still tints it on ready

        Transform iconT = button.Find("Icon");
        UImage icon;
        if (iconT == null)
        {
            var go = new GameObject("Icon", typeof(RectTransform));
            go.transform.SetParent(button, false);
            var irt = go.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.03f, 0.03f);
            irt.anchorMax = new Vector2(0.97f, 0.97f);
            irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
            icon = go.AddComponent<UImage>();
            icon.raycastTarget = false;
        }
        else
        {
            icon = iconT.GetComponent<UImage>();
        }
        icon.sprite = Load<Sprite>(iconSprite);
        icon.preserveAspect = true;
    }

    // The ability buttons used to carry their own label as a child TMP_Text
    // stretched over the whole button (the button doubled as a text pill).
    // Reparents that SAME object (same component references, same script
    // bindings) to sit as its own row beneath the bar, instead of creating a
    // new text object -- so buttonLabel/labelText fields elsewhere keep
    // pointing at a live object with unchanged behavior. Also switched to
    // auto-sizing (BUG 5): a fixed fontSize=24 in a 150-wide box wrapped
    // "Shield (999)"-length runtime strings across 2 lines that spilled past
    // the box's fixed 40px height with nothing clipping it -- reading as
    // "eld UP"-style garbling where it visually met the next column. Now it
    // shrinks to fit ONE line, and the column's RectMask2D is the hard backstop.
    static Transform MoveLabelOut(Transform button, Transform column, Transform wrapper, string newName, float x)
    {
        // Idempotent: a re-run finds it already moved+renamed under the column.
        // wrapper is also checked as a fallback: a scene built by the previous
        // (pre-column) version of this tool already has it renamed and parented
        // directly under wrapper rather than under button or column.
        Transform label = column.Find(newName) ?? wrapper.Find(newName) ?? button.Find("Text (TMP)");
        if (label == null) return null;
        label.name = newName;
        label.SetParent(column, false);
        var rt = label.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(x, LabelY);
        rt.sizeDelta = LabelSize;

        var tmp = label.GetComponent<TMP_Text>();
        if (tmp != null)
        {
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow; // safe now: RectMask2D on the column clips it regardless
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 12f;
            tmp.fontSizeMax = 24f;
            tmp.alignment = TextAlignmentOptions.Center;
            // BUG 5 (readability): these 4 labels' authored colors were wildly
            // inconsistent -- TimeSink's was fully transparent (alpha 0, so it
            // never rendered at all; unnoticeable back when it sat invisibly on
            // top of a solid button), Repair/Shield's were near-black (0.2,0.2,0.2),
            // reading as a faint smudge against the brown ground now that the
            // label sits on its own below the bar. Only Overload's was already a
            // legible cream. Standardize all 4 to that same cream so none of
            // them depend on ReadyStateHighlight ever firing to become visible.
            tmp.color = new Color(0.980f, 0.933f, 0.855f, 1f);
        }
        return label;
    }

    static void RestyleBar(Slider bar, Transform column, float x, string bgSprite, string fillSprite)
    {
        if (bar.transform.parent != column) bar.transform.SetParent(column, false);
        var rt = bar.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(x, BarY);
        rt.sizeDelta = BarSize;

        // Type.Simple -- see the comment on the health bar restyle above for why
        // Sliced breaks at this bar's height with these sprites' border metadata.
        var bg = bar.transform.Find("Background")?.GetComponent<UImage>();
        if (bg != null) { bg.sprite = Load<Sprite>(bgSprite); bg.type = UImage.Type.Simple; bg.color = Color.white; }
        var fill = bar.transform.Find("Fill Area/Fill")?.GetComponent<UImage>();
        if (fill != null) { fill.sprite = Load<Sprite>(fillSprite); fill.type = UImage.Type.Simple; fill.color = Color.white; }

        bar.interactable = false;
        foreach (var g in bar.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
    }

    static Slider MakeStaticBar(string name, Transform parent)
    {
        var root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        var rt = root.GetComponent<RectTransform>();
        rt.pivot = new Vector2(0.5f, 0.5f);

        Slider slider = root.AddComponent<Slider>();
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;

        var bgGO = new GameObject("Background", typeof(RectTransform));
        bgGO.transform.SetParent(root.transform, false);
        Stretch(bgGO.GetComponent<RectTransform>());
        var bg = bgGO.AddComponent<UImage>();
        bg.raycastTarget = false;

        var fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGO.transform.SetParent(root.transform, false);
        Stretch(fillAreaGO.GetComponent<RectTransform>());

        var fillGO = new GameObject("Fill", typeof(RectTransform));
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = new Vector2(0f, 0f); fillRT.anchorMax = new Vector2(1f, 1f); // static/full: stretch, not fillRect-driven
        fillRT.offsetMin = Vector2.zero; fillRT.offsetMax = Vector2.zero;
        var fill = fillGO.AddComponent<UImage>();
        fill.raycastTarget = false;

        slider.fillRect = fillRT;
        slider.targetGraphic = null;
        return slider;
    }

    static void PositionGlow(Transform glow, float x)
    {
        if (glow == null) return;
        var rt = glow.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(x, IconY);
        rt.sizeDelta = new Vector2(220f, 220f);
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
