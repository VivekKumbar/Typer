using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UImage = UnityEngine.UI.Image;

// Restyles GameScene's existing GameOverPanel to the crest-card reference
// (Docs reference: gameover.png + Layer 2-6; Layer 7 deliberately unused --
// there is no third "highest wave" row in this design). Reuses every existing
// GameObject/component (RestartButton, MainMenu buttons + their onClick
// wiring, GameOverAdOffer, StatCoins/StatWpm TMP objects HUD.cs already
// fills) exactly as wired -- this only adds art and repositions/resizes
// RectTransforms. No restart/menu logic changes here. Idempotent: safe to re-run.
public static class GameOverPopupBuilder
{
    const string ArtDir = "Assets/_Project/Art/Textures/HUD/GameOver/";

    const float CardWidth = 680f;
    const float CardHeight = 1020f; // matches gameover_panel.png's own 1024:1536 aspect

    const float RowWidth = 480f;
    const float RowHeight = 120f;

    [MenuItem("TypeKeep/Build Game Over Popup")]
    public static void Build()
    {
        GameObject canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null) { Debug.LogError("[GameOverPopupBuilder] No 'Canvas' found. Open GameScene.unity first."); return; }
        Transform panel = canvasGO.transform.Find("GameOverPanel");
        if (panel == null) { Debug.LogError("[GameOverPopupBuilder] No 'GameOverPanel' under Canvas."); return; }

        Transform card = BuildCard(panel);
        BuildTitle(panel, card);
        BuildStatRow(panel, "StatCoins", "CoinsRow", "stat_row_coins", "COINS",
            new Color(1f, 0.85f, 0.35f), 120f);
        BuildStatRow(panel, "StatWpm", "WpmRow", "stat_row_wpm", "WPM",
            new Color(0.45f, 0.85f, 1f), -20f);
        HideUnusedWaveStat(panel);
        BuildButton(panel, "RestartButton", "button_gold", "icon_restart", "PLAY AGAIN", -160f);
        BuildButton(panel, "MainMenu", "button_blue", "icon_house", "MAIN MENU", -290f);

        EditorUtility.SetDirty(canvasGO);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[GameOverPopupBuilder] Game Over popup restyled.");
    }

    // ================= card frame =================

    static Transform BuildCard(Transform panel)
    {
        Transform card = panel.Find("Card");
        if (card == null)
        {
            var go = new GameObject("Card", typeof(RectTransform));
            card = go.transform;
            card.SetParent(panel, false);
            card.SetAsFirstSibling(); // behind GAMEOVER/rows/buttons, which are all direct panel children
            var img = go.AddComponent<UImage>();
            img.raycastTarget = false;
        }
        var img2 = card.GetComponent<UImage>();
        img2.sprite = Load<Sprite>("gameover_panel");
        img2.type = UImage.Type.Simple; // native-res art at a size close to native -- no 9-slice border needed
        img2.color = Color.white;
        var rt = card.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(CardWidth, CardHeight);
        return card;
    }

    // ================= title =================

    static void BuildTitle(Transform panel, Transform card)
    {
        Transform banner = panel.Find("TitleBanner");
        if (banner == null)
        {
            var go = new GameObject("TitleBanner", typeof(RectTransform));
            banner = go.transform;
            banner.SetParent(panel, false);
            var img = go.AddComponent<UImage>();
            img.raycastTarget = false;
        }
        banner.SetSiblingIndex(card.GetSiblingIndex() + 1); // just above the card, below everything else
        var bannerImg = banner.GetComponent<UImage>();
        bannerImg.sprite = Load<Sprite>("gameover_title_banner");
        bannerImg.preserveAspect = true;
        var bannerRT = banner.GetComponent<RectTransform>();
        bannerRT.anchorMin = bannerRT.anchorMax = new Vector2(0.5f, 0.5f);
        bannerRT.anchoredPosition = new Vector2(0f, 320f);
        bannerRT.sizeDelta = new Vector2(520f, 336f);

        // GAMEOVER already exists as a bare TMP_Text at the panel's old layout
        // position -- move it onto the banner instead of creating a new object,
        // so anything else referencing it (none currently, but keeps the same
        // pattern as the ability HUD passes) stays valid.
        Transform title = panel.Find("GAMEOVER");
        title.SetParent(panel, false);
        title.SetSiblingIndex(banner.GetSiblingIndex() + 1);
        var titleRT = title.GetComponent<RectTransform>();
        titleRT.anchorMin = titleRT.anchorMax = new Vector2(0.5f, 0.5f);
        titleRT.anchoredPosition = new Vector2(0f, 320f);
        titleRT.sizeDelta = new Vector2(480f, 140f);

        var titleTmp = title.GetComponent<TMP_Text>();
        titleTmp.text = "GAME OVER";
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = Color.white;
        titleTmp.enableAutoSizing = true;
        titleTmp.fontSizeMin = 30f;
        titleTmp.fontSizeMax = 56f;
        titleTmp.enableWordWrapping = false;
        titleTmp.overflowMode = TextOverflowModes.Overflow;
    }

    // ================= stat rows (Coins / WPM) =================

    // valueLabel is the EXISTING bare TMP_Text (StatCoins/StatWpm) HUD.cs already
    // fills with just the number (see HUD.FillGameOverStats) -- reused as-is,
    // just repositioned onto the new row art, with a new static "COINS"/"WPM"
    // label added above it. The row's icon (crown/clock) is already baked into
    // stat_row_coins.png/stat_row_wpm.png, so no separate icon image is needed.
    static void BuildStatRow(Transform panel, string valueObjectName, string rowName, string rowSprite,
        string labelText, Color valueColor, float y)
    {
        Transform row = panel.Find(rowName);
        if (row == null)
        {
            var go = new GameObject(rowName, typeof(RectTransform));
            row = go.transform;
            row.SetParent(panel, false);
            var img = go.AddComponent<UImage>();
            img.raycastTarget = false;
        }
        var rowImg = row.GetComponent<UImage>();
        rowImg.sprite = Load<Sprite>(rowSprite);
        rowImg.preserveAspect = false; // rows share one height for a clean stacked column; the art tolerates a slight stretch fine at this size
        var rowRT = row.GetComponent<RectTransform>();
        rowRT.anchorMin = rowRT.anchorMax = new Vector2(0.5f, 0.5f);
        rowRT.anchoredPosition = new Vector2(0f, y);
        rowRT.sizeDelta = new Vector2(RowWidth, RowHeight);

        // Value: the existing StatCoins/StatWpm object, reparented onto the row.
        // Idempotent: a re-run finds it already reparented under the row.
        Transform value = row.Find(valueObjectName) ?? panel.Find(valueObjectName);
        if (value.parent != row) value.SetParent(row, false);
        var valueRT = value.GetComponent<RectTransform>();
        valueRT.anchorMin = new Vector2(0.30f, 0.02f);
        valueRT.anchorMax = new Vector2(0.95f, 0.48f); // lower half of the row's text area, right of the baked icon
        valueRT.offsetMin = Vector2.zero; valueRT.offsetMax = Vector2.zero;
        var valueTmp = value.GetComponent<TMP_Text>();
        valueTmp.alignment = TextAlignmentOptions.Left;
        valueTmp.fontStyle = FontStyles.Bold;
        valueTmp.color = valueColor;
        valueTmp.enableAutoSizing = true;
        valueTmp.fontSizeMin = 20f;
        valueTmp.fontSizeMax = 40f;
        valueTmp.enableWordWrapping = false;
        valueTmp.overflowMode = TextOverflowModes.Overflow;

        // Label: static "COINS"/"WPM", new object, upper half of the same text area.
        Transform label = row.Find(rowName + "Label");
        if (label == null)
        {
            var go = new GameObject(rowName + "Label", typeof(RectTransform));
            label = go.transform;
            label.SetParent(row, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.raycastTarget = false;
            tmp.font = valueTmp.font; // project's existing font, same as everything else
        }
        var labelRT = label.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0.30f, 0.52f);
        labelRT.anchorMax = new Vector2(0.95f, 0.95f);
        labelRT.offsetMin = Vector2.zero; labelRT.offsetMax = Vector2.zero;
        var labelTmp = label.GetComponent<TMP_Text>();
        labelTmp.text = labelText;
        labelTmp.alignment = TextAlignmentOptions.Left;
        labelTmp.fontStyle = FontStyles.Bold;
        labelTmp.color = new Color(0.85f, 0.82f, 0.78f); // light warm gray, matches the reference's label tone (distinct from the bold value color below it)
        labelTmp.enableAutoSizing = true;
        labelTmp.fontSizeMin = 14f;
        labelTmp.fontSizeMax = 24f;
        labelTmp.enableWordWrapping = false;
        labelTmp.overflowMode = TextOverflowModes.Overflow;
    }

    // Layer 7 (crest plate) is intentionally unused -- no third stat row on
    // this screen. StatWave stays exactly as HUD.cs already fills it (harmless,
    // just never rendered); only its GameObject is hidden.
    static void HideUnusedWaveStat(Transform panel)
    {
        Transform wave = panel.Find("StatWave");
        if (wave != null) wave.gameObject.SetActive(false);
    }

    // ================= buttons (Play Again / Main Menu) =================

    static void BuildButton(Transform panel, string buttonName, string buttonSprite, string iconSprite, string label, float y)
    {
        Transform button = panel.Find(buttonName);
        var btnImg = button.GetComponent<UImage>();
        btnImg.sprite = Load<Sprite>(buttonSprite);
        btnImg.type = UImage.Type.Simple;
        btnImg.color = Color.white;
        var btnRT = button.GetComponent<RectTransform>();
        btnRT.anchorMin = btnRT.anchorMax = new Vector2(0.5f, 0.5f);
        btnRT.anchoredPosition = new Vector2(0f, y);
        btnRT.sizeDelta = new Vector2(RowWidth, RowHeight);

        // Icon: left side, vertically centered, non-raycast so clicks still reach the Button.
        Transform icon = button.Find("Icon");
        if (icon == null)
        {
            var go = new GameObject("Icon", typeof(RectTransform));
            icon = go.transform;
            icon.SetParent(button, false);
            var img = go.AddComponent<UImage>();
            img.raycastTarget = false;
        }
        var iconImg = icon.GetComponent<UImage>();
        iconImg.sprite = Load<Sprite>(iconSprite);
        iconImg.preserveAspect = true;
        var iconRT = icon.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.10f, 0.5f);
        iconRT.anchorMax = new Vector2(0.10f, 0.5f);
        iconRT.pivot = new Vector2(0.5f, 0.5f);
        iconRT.anchoredPosition = Vector2.zero;
        iconRT.sizeDelta = new Vector2(76f, 76f);

        // Label: existing TMP_Text child (button doubled as a text pill before
        // this pass), repositioned to the icon's right, still vertically centered.
        Transform labelT = button.Find("PLAY-AGAIN") ?? button.Find("Text (TMP)") ?? FindAnyTmpChild(button);
        var labelRT = labelT.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0.20f, 0f);
        labelRT.anchorMax = new Vector2(0.95f, 1f);
        labelRT.offsetMin = Vector2.zero; labelRT.offsetMax = Vector2.zero;
        var labelTmp = labelT.GetComponent<TMP_Text>();
        labelTmp.text = label;
        labelTmp.alignment = TextAlignmentOptions.Center;
        labelTmp.fontStyle = FontStyles.Bold;
        labelTmp.color = new Color(0.18f, 0.12f, 0.02f); // dark warm brown -- reads clearly on both the gold and blue plates, matching the reference's dark button text
        labelTmp.enableAutoSizing = true;
        labelTmp.fontSizeMin = 20f;
        labelTmp.fontSizeMax = 34f;
        labelTmp.enableWordWrapping = false;
        labelTmp.overflowMode = TextOverflowModes.Overflow;
    }

    static Transform FindAnyTmpChild(Transform parent)
    {
        foreach (Transform c in parent)
            if (c.GetComponent<TMP_Text>() != null) return c;
        return null;
    }

    static T Load<T>(string spriteFileNameNoExt) where T : Object
    {
        string path = ArtDir + spriteFileNameNoExt + ".png";
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) Debug.LogError("[GameOverPopupBuilder] Could not load " + typeof(T).Name + " at " + path);
        return asset;
    }
}
