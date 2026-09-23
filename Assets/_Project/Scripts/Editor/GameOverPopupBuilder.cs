using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UImage = UnityEngine.UI.Image;

// Assembles GameScene's GameOverPanel to match the exact crest-card reference
// (Textures: gameover.png, stat_row_coins.png, stat_row_wpm.png, button_gold.png,
// button_blue.png, icon_restart.png, icon_house.png) already in the project.
// Idempotent: safe to re-run.
public static class GameOverPopupBuilder
{
    const string ArtDir = "Assets/_Project/Art/Textures/HUD/GameOver/";

    const float CardWidth = 680f;
    const float CardHeight = 1020f; // matches gameover.png's 1024:1536 aspect

    const float RowWidth = 360f;
    const float RowHeight = 90f;

    [MenuItem("TypeKeep/Build Game Over Popup")]
    public static void Build()
    {
        GameObject canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null) { Debug.LogError("[GameOverPopupBuilder] No 'Canvas' found. Open GameScene.unity first."); return; }
        Transform panel = canvasGO.transform.Find("GameOverPanel");
        if (panel == null) { Debug.LogError("[GameOverPopupBuilder] No 'GameOverPanel' under Canvas."); return; }

        // Clean up redundant banners if any
        Transform oldBanner = panel.Find("TitleBanner");
        if (oldBanner != null) Object.DestroyImmediate(oldBanner.gameObject);

        // Hide bare GAMEOVER text (gameover.png already has the 3D stone text baked into the ribbon)
        Transform title = panel.Find("GAMEOVER");
        if (title != null) title.gameObject.SetActive(false);

        // Hide StatWave and WatchAdButton so the crest card remains clean and matches reference
        Transform wave = panel.Find("StatWave");
        if (wave != null) wave.gameObject.SetActive(false);

        Transform adBtn = panel.Find("WatchAdButton");
        if (adBtn != null) adBtn.gameObject.SetActive(false);

        Transform card = BuildCard(panel);
        BuildStatRow(panel, "StatCoins", "CoinsRow", "stat_row_coins", "COINS",
            new Color(1f, 0.88f, 0.22f), 105f);
        BuildStatRow(panel, "StatWpm", "WpmRow", "stat_row_wpm", "WPM",
            new Color(0.20f, 0.82f, 1f), -5f);
        BuildButton(panel, "RestartButton", "button_gold", "icon_restart", "PLAY AGAIN",
            new Color(0.20f, 0.13f, 0.03f), new Color(0.20f, 0.13f, 0.03f), -115f, 82f);
        BuildButton(panel, "MainMenu", "button_blue", "icon_house", "MAIN MENU",
            new Color(0.95f, 0.98f, 1f), Color.white, -220f, 80f);

        EditorUtility.SetDirty(canvasGO);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[GameOverPopupBuilder] Game Over popup built to match exact reference art.");
    }

    static Transform BuildCard(Transform panel)
    {
        Transform card = panel.Find("Card");
        if (card == null)
        {
            var go = new GameObject("Card", typeof(RectTransform));
            card = go.transform;
            card.SetParent(panel, false);
            card.SetAsFirstSibling();
            var img = go.AddComponent<UImage>();
            img.raycastTarget = false;
        }
        var img2 = card.GetComponent<UImage>();
        img2.sprite = Load<Sprite>("gameover");
        img2.type = UImage.Type.Simple;
        img2.color = Color.white;
        var rt = card.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(CardWidth, CardHeight);
        return card;
    }

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
        rowImg.preserveAspect = false;
        var rowRT = row.GetComponent<RectTransform>();
        rowRT.anchorMin = rowRT.anchorMax = new Vector2(0.5f, 0.5f);
        rowRT.anchoredPosition = new Vector2(0f, y);
        rowRT.sizeDelta = new Vector2(RowWidth, RowHeight);

        Transform value = row.Find(valueObjectName) ?? panel.Find(valueObjectName);
        if (value != null)
        {
            if (value.parent != row) value.SetParent(row, false);
            value.gameObject.SetActive(true);
            var valueRT = value.GetComponent<RectTransform>();
            valueRT.anchorMin = new Vector2(0.34f, 0.06f);
            valueRT.anchorMax = new Vector2(0.95f, 0.52f);
            valueRT.offsetMin = Vector2.zero;
            valueRT.offsetMax = Vector2.zero;
            var valueTmp = value.GetComponent<TMP_Text>();
            valueTmp.alignment = TextAlignmentOptions.Left;
            valueTmp.fontStyle = FontStyles.Bold;
            valueTmp.color = valueColor;
            valueTmp.enableAutoSizing = true;
            valueTmp.fontSizeMin = 22f;
            valueTmp.fontSizeMax = 38f;
            valueTmp.enableWordWrapping = false;
            valueTmp.overflowMode = TextOverflowModes.Overflow;
        }

        Transform label = row.Find(rowName + "Label");
        if (label == null)
        {
            var go = new GameObject(rowName + "Label", typeof(RectTransform));
            label = go.transform;
            label.SetParent(row, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.raycastTarget = false;
        }
        var labelRT = label.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0.34f, 0.52f);
        labelRT.anchorMax = new Vector2(0.95f, 0.92f);
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
        var labelTmp = label.GetComponent<TMP_Text>();
        labelTmp.text = labelText;
        labelTmp.alignment = TextAlignmentOptions.Left;
        labelTmp.fontStyle = FontStyles.Bold;
        labelTmp.color = new Color(0.92f, 0.89f, 0.83f);
        labelTmp.enableAutoSizing = true;
        labelTmp.fontSizeMin = 13f;
        labelTmp.fontSizeMax = 20f;
        labelTmp.enableWordWrapping = false;
        labelTmp.overflowMode = TextOverflowModes.Overflow;
    }

    static void BuildButton(Transform panel, string buttonName, string buttonSprite, string iconSprite,
        string label, Color textColor, Color iconColor, float y, float height)
    {
        Transform button = panel.Find(buttonName);
        if (button == null) return;
        button.gameObject.SetActive(true);
        var btnImg = button.GetComponent<UImage>();
        btnImg.sprite = Load<Sprite>(buttonSprite);
        btnImg.type = UImage.Type.Simple;
        btnImg.color = Color.white;
        var btnRT = button.GetComponent<RectTransform>();
        btnRT.anchorMin = btnRT.anchorMax = new Vector2(0.5f, 0.5f);
        btnRT.anchoredPosition = new Vector2(0f, y);
        btnRT.sizeDelta = new Vector2(RowWidth, height);

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
        iconImg.color = iconColor;
        var iconRT = icon.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0f, 0.5f);
        iconRT.anchorMax = new Vector2(0f, 0.5f);
        iconRT.pivot = new Vector2(0.5f, 0.5f);
        iconRT.anchoredPosition = new Vector2(44f, 0f);
        iconRT.sizeDelta = new Vector2(44f, 44f);

        Transform labelT = button.Find("PLAY-AGAIN") ?? button.Find("Text (TMP)") ?? FindAnyTmpChild(button);
        if (labelT != null)
        {
            var labelRT = labelT.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0.18f, 0f);
            labelRT.anchorMax = new Vector2(0.95f, 1f);
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
            var labelTmp = labelT.GetComponent<TMP_Text>();
            labelTmp.text = label;
            labelTmp.alignment = TextAlignmentOptions.Center;
            labelTmp.fontStyle = FontStyles.Bold;
            labelTmp.color = textColor;
            labelTmp.enableAutoSizing = true;
            labelTmp.fontSizeMin = 18f;
            labelTmp.fontSizeMax = 28f;
            labelTmp.enableWordWrapping = false;
            labelTmp.overflowMode = TextOverflowModes.Overflow;
        }
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
