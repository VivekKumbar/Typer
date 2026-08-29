using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;
using System.Linq;
using UImage = UnityEngine.UI.Image;

// Builds/rebuilds the ProfilePanel hierarchy in the currently open MainMenu scene
// and wires every ProfilePanelUI field. Re-run any time from the menu below —
// it removes a previous ProfilePanel/Button_Profile first, so it's safe to repeat
// while iterating on layout.
public static class ProfilePanelBuilder
{
    // Design tokens from Docs/TypeKeep_UI_DesignSystem.md
    static readonly Color Surface1 = Hex("#12131A");
    static readonly Color Surface2 = Hex("#1C1E26");
    static readonly Color TextPrimary = Hex("#FFFFFF");
    static readonly Color TextSecondary = Hex("#C9CCD6");
    static readonly Color TextMuted = Hex("#9AA0AE");
    static readonly Color Accent = Hex("#EF9F27");

    const float RowHeight = 64f;
    const float SectionTitleHeight = 40f;
    const float RankHeaderHeight = 144f;
    const float SectionSpacing = 24f;
    const float RowSpacing = 8f;
    const float ScreenPadding = 16f;
    const float TopBarHeight = 100f;

    [MenuItem("TypeKeep/Build Profile Panel")]
    public static void Build()
    {
        GameObject canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null) { Debug.LogError("[ProfilePanelBuilder] No 'Canvas' found in the active scene. Open MainMenu.unity first."); return; }
        Transform canvas = canvasGO.transform;

        Transform shopPanel = canvas.Find("ShopPanel");
        if (shopPanel == null) { Debug.LogError("[ProfilePanelBuilder] No 'ShopPanel' found — this builder reuses its TopBar style."); return; }

        // Idempotent: remove a previous build first.
        Transform oldPanel = canvas.Find("ProfilePanel");
        if (oldPanel != null) Object.DestroyImmediate(oldPanel.gameObject);
        Transform oldButton = canvas.Find("Button_Profile");
        if (oldButton != null) Object.DestroyImmediate(oldButton.gameObject);

        TextMeshProUGUI mainTitleRef = canvas.Find("TopBar/Text_Title").GetComponent<TextMeshProUGUI>();
        TMP_FontAsset font = mainTitleRef.font;

        // ---- Panel root ----
        RectTransform panel = MakeRT("ProfilePanel", canvas);
        Stretch(panel, Vector2.zero, Vector2.zero);
        UImage panelBg = panel.gameObject.AddComponent<UImage>();
        panelBg.color = Surface1;
        panel.gameObject.SetActive(false);
        ProfilePanelUI ui = panel.gameObject.AddComponent<ProfilePanelUI>();

        // ---- TopBar: clone ShopPanel's (Back + Title), drop its coin group ----
        Transform shopTopBar = shopPanel.Find("TopBar");
        GameObject topBarGO = Object.Instantiate(shopTopBar.gameObject, panel);
        topBarGO.name = "TopBar";
        RectTransform topBarRT = topBarGO.GetComponent<RectTransform>();
        topBarRT.anchorMin = new Vector2(0, 1); topBarRT.anchorMax = new Vector2(1, 1);
        topBarRT.pivot = new Vector2(0.5f, 1f);
        topBarRT.sizeDelta = new Vector2(0, TopBarHeight);
        topBarRT.anchoredPosition = Vector2.zero;

        Transform coinGroup = topBarGO.transform.Find("CoinGroup");
        if (coinGroup != null) Object.DestroyImmediate(coinGroup.gameObject);

        TextMeshProUGUI title = topBarGO.transform.Find("Text_Title").GetComponent<TextMeshProUGUI>();
        title.text = "PROFILE";

        Button backBtn = topBarGO.transform.Find("Button_Back").GetComponent<Button>();
        // The cloned button's persistent listener still points at ShopPanel — replace it.
        for (int i = backBtn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(backBtn.onClick, i);
        UnityAction<bool> closeCall = panel.gameObject.SetActive;
        UnityEventTools.AddBoolPersistentListener(backBtn.onClick, closeCall, false);

        // ---- Content column ----
        RectTransform content = MakeRT("Content", panel);
        content.anchorMin = new Vector2(0, 0); content.anchorMax = new Vector2(1, 1);
        content.offsetMin = new Vector2(ScreenPadding, ScreenPadding);
        content.offsetMax = new Vector2(-ScreenPadding, -(TopBarHeight + ScreenPadding));
        VerticalLayoutGroup contentVLG = content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentVLG.spacing = SectionSpacing;
        contentVLG.childAlignment = TextAnchor.UpperCenter;
        // childControlHeight MUST be true for VerticalLayoutGroup to actually read each
        // child's LayoutElement.preferredHeight — with it false, Unity just uses whatever
        // raw RectTransform.sizeDelta the child already has (the 100x100 default here).
        contentVLG.childControlWidth = true; contentVLG.childControlHeight = true;
        contentVLG.childForceExpandWidth = true; contentVLG.childForceExpandHeight = false;

        // ---- Rank header ----
        RectTransform rankHeader = MakeSection("RankHeader", content, RankHeaderHeight);
        UImage rankBg = rankHeader.gameObject.AddComponent<UImage>();
        rankBg.color = Surface2;

        RectTransform rankTextRT = MakeRT("RankText", rankHeader);
        rankTextRT.anchorMin = new Vector2(0, 1); rankTextRT.anchorMax = new Vector2(1, 1);
        rankTextRT.pivot = new Vector2(0.5f, 1f);
        rankTextRT.sizeDelta = new Vector2(0, 80);
        rankTextRT.anchoredPosition = new Vector2(0, -16);
        TextMeshProUGUI rankText = rankTextRT.gameObject.AddComponent<TextMeshProUGUI>();
        rankText.font = font; rankText.fontSize = 64; rankText.fontStyle = FontStyles.Bold;
        rankText.color = Accent; rankText.alignment = TextAlignmentOptions.Center;
        rankText.text = "ROOKIE";

        RectTransform rankSubRT = MakeRT("RankSubtitle", rankHeader);
        rankSubRT.anchorMin = new Vector2(0, 0); rankSubRT.anchorMax = new Vector2(1, 0);
        rankSubRT.pivot = new Vector2(0.5f, 0f);
        rankSubRT.sizeDelta = new Vector2(0, 32);
        rankSubRT.anchoredPosition = new Vector2(0, 16);
        TextMeshProUGUI rankSub = rankSubRT.gameObject.AddComponent<TextMeshProUGUI>();
        rankSub.font = font; rankSub.fontSize = 22; rankSub.color = TextMuted;
        rankSub.alignment = TextAlignmentOptions.Center;
        rankSub.text = "Best run: Wave 0";

        // ---- Records section ----
        RectTransform records = MakeSubsection("RecordsSection", content, "RECORDS", font, out _);
        var highestWaveText = MakeStatRow(records, "Highest Wave", font);
        var highestComboText = MakeStatRow(records, "Highest Combo", font);
        var mostCoinsText = MakeStatRow(records, "Most Coins in a Run", font);
        var bestAccuracyText = MakeStatRow(records, "Best Accuracy", font);

        // ---- Lifetime section ----
        RectTransform lifetime = MakeSubsection("LifetimeSection", content, "LIFETIME", font, out _);
        var enemiesText = MakeStatRow(lifetime, "Enemies Destroyed", font);
        var lettersText = MakeStatRow(lifetime, "Letters Typed", font);
        var accuracyText = MakeStatRow(lifetime, "Lifetime Accuracy", font);
        var coinsText = MakeStatRow(lifetime, "Total Coins Collected", font);
        var runsText = MakeStatRow(lifetime, "Runs Played", font);

        // ---- Wire ProfilePanelUI ----
        ui.content = content;
        ui.recordsSection = records;
        ui.lifetimeSection = lifetime;
        ui.rankText = rankText;
        ui.rankSubtitleText = rankSub;
        ui.highestWaveText = highestWaveText;
        ui.highestComboText = highestComboText;
        ui.mostCoinsInRunText = mostCoinsText;
        ui.bestAccuracyText = bestAccuracyText;
        ui.enemiesDestroyedText = enemiesText;
        ui.lettersTypedText = lettersText;
        ui.lifetimeAccuracyText = accuracyText;
        ui.totalCoinsText = coinsText;
        ui.runsPlayedText = runsText;

        // ---- Profile button (clone Button_Shop's style) ----
        Transform shopButton = canvas.Find("Button_Shop");
        if (shopButton != null)
        {
            GameObject profileBtnGO = Object.Instantiate(shopButton.gameObject, canvas);
            profileBtnGO.name = "Button_Profile";
            // Instantiate(..., parent) appends as the LAST sibling, which would render on
            // TOP of ShopPanel/ProfilePanel. Put it right after Button_Shop instead, so the
            // panels (later siblings) correctly draw over it once opened.
            profileBtnGO.transform.SetSiblingIndex(shopButton.GetSiblingIndex() + 1);
            RectTransform shopBtnRT = shopButton.GetComponent<RectTransform>();
            RectTransform profileBtnRT = profileBtnGO.GetComponent<RectTransform>();
            profileBtnRT.anchoredPosition = shopBtnRT.anchoredPosition + new Vector2(0, -(shopBtnRT.sizeDelta.y + SectionSpacing));

            foreach (var label in profileBtnGO.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (label.name == "Text_Shop") label.text = "PROFILE";

            Button profileBtn = profileBtnGO.GetComponent<Button>();
            for (int i = profileBtn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(profileBtn.onClick, i);
            UnityAction<bool> openCall = panel.gameObject.SetActive;
            UnityEventTools.AddBoolPersistentListener(profileBtn.onClick, openCall, true);
        }
        else
        {
            Debug.LogWarning("[ProfilePanelBuilder] No 'Button_Shop' to clone for the Profile button — add one manually and wire its OnClick to ProfilePanel.SetActive(true).");
        }

        EditorUtility.SetDirty(canvasGO);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[ProfilePanelBuilder] ProfilePanel built and wired.");
    }

    // ---- helpers ----

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

    static RectTransform MakeRT(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
    }

    // A fixed-height block inside the content VerticalLayoutGroup.
    static RectTransform MakeSection(string name, Transform parent, float height)
    {
        RectTransform rt = MakeRT(name, parent);
        LayoutElement le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
        return rt;
    }

    // A section with a title + its own tightly-spaced (RowSpacing) vertical stack of rows.
    static RectTransform MakeSubsection(string name, Transform parent, string titleText, TMP_FontAsset font, out TextMeshProUGUI title)
    {
        RectTransform rt = MakeRT(name, parent);
        VerticalLayoutGroup vlg = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = RowSpacing;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        ContentSizeFitter csf = rt.gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform titleRT = MakeRT("SectionTitle", rt);
        LayoutElement titleLE = titleRT.gameObject.AddComponent<LayoutElement>();
        titleLE.preferredHeight = SectionTitleHeight;
        title = titleRT.gameObject.AddComponent<TextMeshProUGUI>();
        title.font = font; title.fontSize = 28; title.fontStyle = FontStyles.Bold;
        title.color = TextSecondary; title.alignment = TextAlignmentOptions.MidlineLeft;
        title.text = titleText;

        return rt;
    }

    // One label/value row. Returns the value TMP_Text to wire up.
    static TextMeshProUGUI MakeStatRow(Transform parent, string label, TMP_FontAsset font)
    {
        RectTransform row = MakeRT("Row_" + label.Replace(" ", ""), parent);
        LayoutElement le = row.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = RowHeight;
        UImage bg = row.gameObject.AddComponent<UImage>();
        bg.color = Surface2;

        RectTransform labelRT = MakeRT("Label", row);
        labelRT.anchorMin = new Vector2(0, 0); labelRT.anchorMax = new Vector2(0.6f, 1);
        labelRT.offsetMin = new Vector2(16, 0); labelRT.offsetMax = new Vector2(0, 0);
        TextMeshProUGUI labelTMP = labelRT.gameObject.AddComponent<TextMeshProUGUI>();
        labelTMP.font = font; labelTMP.fontSize = 28; labelTMP.color = TextSecondary;
        labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
        labelTMP.text = label;

        RectTransform valueRT = MakeRT("Value", row);
        valueRT.anchorMin = new Vector2(0.6f, 0); valueRT.anchorMax = new Vector2(1, 1);
        valueRT.offsetMin = new Vector2(0, 0); valueRT.offsetMax = new Vector2(-16, 0);
        TextMeshProUGUI valueTMP = valueRT.gameObject.AddComponent<TextMeshProUGUI>();
        valueTMP.font = font; valueTMP.fontSize = 28; valueTMP.fontStyle = FontStyles.Bold;
        valueTMP.color = TextPrimary; valueTMP.alignment = TextAlignmentOptions.MidlineRight;
        valueTMP.text = "0";

        return valueTMP;
    }
}
