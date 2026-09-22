using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UImage = UnityEngine.UI.Image;

// Restyles FTUEScene's existing Canvas UI to match the reference art (Background.png /
// Type the word.png / skip2.png / nice wrokd.png / im ready.png / skip.png / frame 1.png /
// Layer 1-3.png / button_arrow_next.png / dot_active.png / dot_inactive.png).
// Idempotent: safe to re-run anytime.
public static class FTUERestyleBuilder
{
    const string ArtDir = "Assets/_Project/Art/Textures/FTUE/";

    [MenuItem("TypeKeep/Build FTUE Restyle")]
    public static void Build()
    {
        GameObject canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null) { Debug.LogError("[FTUERestyleBuilder] No 'Canvas' found. Open FTUEScene.unity first."); return; }
        Transform canvas = canvasGO.transform;

        BuildSkipButton(canvas);
        Transform panel = BuildBottomPanel(canvas);
        Transform banner = BuildNiceWorkBanner(canvas);
        BuildCompletionPopup(canvas);
        BuildSkipPopup(canvas);
        WireFTUEManager(canvas, panel, banner);

        EditorUtility.SetDirty(canvasGO);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[FTUERestyleBuilder] FTUE scene restyled successfully.");
    }

    // ================= Skip button =================

    static void BuildSkipButton(Transform canvas)
    {
        Transform skip = canvas.Find("SkipButton");
        var img = skip.GetComponent<UImage>();
        img.sprite = Load<Sprite>("button_skip");
        img.type = UImage.Type.Simple;
        img.color = Color.white;
        var rt = skip.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        // Matching Background.png margin: ~46px from right, ~40px from top
        rt.anchoredPosition = new Vector2(-46f, -40f);
        // Matches button_skip aspect (1880:837)
        rt.sizeDelta = new Vector2(258f, 115f);

        // Baked "Skip >>" art -- hide any dynamic label
        Transform label = skip.Find("Text");
        if (label != null) label.gameObject.SetActive(false);
    }

    // ================= Bottom instruction panel =================

    static Transform BuildBottomPanel(Transform canvas)
    {
        Transform panel = canvas.Find("TypeTheWordPanel");
        CanvasGroup group;
        if (panel == null)
        {
            var go = new GameObject("TypeTheWordPanel", typeof(RectTransform));
            panel = go.transform;
            panel.SetParent(canvas, false);
            var img = go.AddComponent<UImage>();
            img.raycastTarget = false;
            group = go.AddComponent<CanvasGroup>();
        }
        else
        {
            group = panel.GetComponent<CanvasGroup>();
            if (group == null) group = panel.gameObject.AddComponent<CanvasGroup>();
        }

        var tutorialGroup = canvas.Find("TutorialTextGroup");
        if (tutorialGroup != null)
        {
            panel.SetSiblingIndex(tutorialGroup.GetSiblingIndex());
            // The panel has baked instructions, hide the old dynamic TMP text
            var tutorialText = tutorialGroup.GetComponentInChildren<TMP_Text>(true);
            if (tutorialText != null)
            {
                Color c = tutorialText.color;
                tutorialText.color = new Color(c.r, c.g, c.b, 0f);
            }
        }

        var panelImg = panel.GetComponent<UImage>();
        panelImg.sprite = Load<Sprite>("panel_type_the_word");
        panelImg.preserveAspect = true;
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        // Anchored at ~85px from bottom matching Background.png
        rt.anchoredPosition = new Vector2(0f, 85f);
        // Size 1000 x 502 matches panel_type_the_word aspect (1769:889)
        rt.sizeDelta = new Vector2(1000f, 502f);

        // Clean up any old overlays if present
        Transform oldDots = panel.Find("DotsIndicator");
        if (oldDots != null) UnityEngine.Object.DestroyImmediate(oldDots.gameObject);
        Transform oldArrow = panel.Find("NextArrowButton");
        if (oldArrow != null) UnityEngine.Object.DestroyImmediate(oldArrow.gameObject);

        return panel;
    }

    // ================= "Nice work!" transition banner =================

    static Transform BuildNiceWorkBanner(Transform canvas)
    {
        Transform banner = canvas.Find("NiceWorkBanner");
        CanvasGroup group;
        if (banner == null)
        {
            var go = new GameObject("NiceWorkBanner", typeof(RectTransform));
            banner = go.transform;
            banner.SetParent(canvas, false);
            var img = go.AddComponent<UImage>();
            img.raycastTarget = false;
            group = go.AddComponent<CanvasGroup>();
        }
        else
        {
            group = banner.GetComponent<CanvasGroup>();
            if (group == null) group = banner.gameObject.AddComponent<CanvasGroup>();
        }
        banner.SetAsLastSibling();

        var bannerImg = banner.GetComponent<UImage>();
        bannerImg.sprite = Load<Sprite>("banner_nice_work");
        bannerImg.preserveAspect = true;
        var rt = banner.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 120f);
        // Matches banner_nice_work aspect (1685:934)
        rt.sizeDelta = new Vector2(800f, 443f);

        group.alpha = 0f;
        banner.gameObject.SetActive(false);
        return banner;
    }

    // ================= Completion / "Play For Real" popup =================

    static void BuildCompletionPopup(Transform canvas)
    {
        Transform popup = canvas.Find("CompletionPopup");
        var bgImg = popup.GetComponent<UImage>();
        if (bgImg != null)
        {
            bgImg.color = new Color(0f, 0f, 0f, 0.75f);
            bgImg.raycastTarget = true;
        }

        Transform dialog = popup.Find("Dialog");
        var dialogImg = dialog.GetComponent<UImage>();
        dialogImg.sprite = Load<Sprite>("popup_frame");
        dialogImg.type = UImage.Type.Simple;
        dialogImg.color = Color.white;
        var dialogRT = dialog.GetComponent<RectTransform>();
        dialogRT.anchorMin = dialogRT.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRT.pivot = new Vector2(0.5f, 0.5f);
        dialogRT.anchoredPosition = Vector2.zero;
        // Matches popup_frame aspect (1175:1338), spans ~90% screen width
        dialogRT.sizeDelta = new Vector2(960f, 1093f);

        Transform title = dialog.Find("Title");
        if (title != null) title.gameObject.SetActive(false);

        Transform button = dialog.Find("PlayForRealButton");
        var btnImg = button.GetComponent<UImage>();
        btnImg.sprite = Load<Sprite>("button_play_for_real");
        btnImg.type = UImage.Type.Simple;
        btnImg.color = Color.white;
        var btnRT = button.GetComponent<RectTransform>();
        btnRT.anchorMin = btnRT.anchorMax = new Vector2(0.5f, 0.5f);
        btnRT.pivot = new Vector2(0.5f, 0.5f);
        btnRT.anchoredPosition = new Vector2(0f, -140f);
        // Matches button_play_for_real aspect (2160:728)
        btnRT.sizeDelta = new Vector2(720f, 243f);

        Transform btnLabel = button.Find("Text");
        if (btnLabel != null) btnLabel.gameObject.SetActive(false);
    }

    // ================= Skip popup =================

    static void BuildSkipPopup(Transform canvas)
    {
        Transform popup = canvas.Find("SkipPopup");
        var bgImg = popup.GetComponent<UImage>();
        if (bgImg != null)
        {
            bgImg.color = new Color(0f, 0f, 0f, 0.75f);
            bgImg.raycastTarget = true;
        }

        Transform dialog = popup.Find("Dialog");
        var dialogImg = dialog.GetComponent<UImage>();
        dialogImg.sprite = Load<Sprite>("popup_frame");
        dialogImg.type = UImage.Type.Simple;
        dialogImg.color = Color.white;
        var dialogRT = dialog.GetComponent<RectTransform>();
        dialogRT.anchorMin = dialogRT.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRT.pivot = new Vector2(0.5f, 0.5f);
        dialogRT.anchoredPosition = Vector2.zero;
        // Spans ~90% screen width matching skip.png
        dialogRT.sizeDelta = new Vector2(960f, 1093f);

        Transform title = dialog.Find("Title");
        if (title != null) title.gameObject.SetActive(false);

        Transform titleArt = dialog.Find("TitleArt");
        if (titleArt == null)
        {
            var go = new GameObject("TitleArt", typeof(RectTransform));
            titleArt = go.transform;
            titleArt.SetParent(dialog, false);
            var img = go.AddComponent<UImage>();
            img.raycastTarget = false;
        }
        titleArt.SetSiblingIndex(title != null ? title.GetSiblingIndex() : 0);
        var titleImg = titleArt.GetComponent<UImage>();
        titleImg.sprite = Load<Sprite>("title_skip_tutorial");
        titleImg.preserveAspect = true;
        var titleRT = titleArt.GetComponent<RectTransform>();
        titleRT.anchorMin = titleRT.anchorMax = new Vector2(0.5f, 0.5f);
        titleRT.pivot = new Vector2(0.5f, 0.5f);
        // Positioned at +114px above frame center per skip.png measurement
        titleRT.anchoredPosition = new Vector2(0f, 114f);
        // Matches title_skip_tutorial aspect (2170:725)
        titleRT.sizeDelta = new Vector2(565f, 189f);

        Transform imReady = dialog.Find("ImReadyButton");
        var imReadyImg = imReady.GetComponent<UImage>();
        imReadyImg.sprite = Load<Sprite>("button_im_ready");
        imReadyImg.type = UImage.Type.Simple;
        imReadyImg.color = Color.white;
        var imReadyRT = imReady.GetComponent<RectTransform>();
        imReadyRT.anchorMin = imReadyRT.anchorMax = new Vector2(0.5f, 0.5f);
        imReadyRT.pivot = new Vector2(0.5f, 0.5f);
        // Positioned at -133px below frame center per skip.png measurement
        imReadyRT.anchoredPosition = new Vector2(0f, -133f);
        imReadyRT.sizeDelta = new Vector2(720f, 241f);
        Transform imReadyLabel = imReady.Find("Text");
        if (imReadyLabel != null) imReadyLabel.gameObject.SetActive(false);

        Transform playAgain = dialog.Find("PlayTutorialAgainButton");
        var playAgainImg = playAgain.GetComponent<UImage>();
        playAgainImg.sprite = Load<Sprite>("button_play_tutorial_again");
        playAgainImg.type = UImage.Type.Simple;
        playAgainImg.color = Color.white;
        var playAgainRT = playAgain.GetComponent<RectTransform>();
        playAgainRT.anchorMin = playAgainRT.anchorMax = new Vector2(0.5f, 0.5f);
        playAgainRT.pivot = new Vector2(0.5f, 0.5f);
        // Positioned at -320px below frame center per skip.png measurement
        playAgainRT.anchoredPosition = new Vector2(0f, -320f);
        playAgainRT.sizeDelta = new Vector2(720f, 242f);
        Transform playAgainLabel = playAgain.Find("Text");
        if (playAgainLabel != null) playAgainLabel.gameObject.SetActive(false);
    }

    // ================= Wire FTUEManager fields =================

    static void WireFTUEManager(Transform canvas, Transform panel, Transform banner)
    {
        var ftue = Object.FindFirstObjectByType<FTUEManager>(FindObjectsInactive.Include);
        if (ftue == null) { Debug.LogError("[FTUERestyleBuilder] No FTUEManager found in the scene."); return; }

        ftue.niceWorkBanner = banner.GetComponent<CanvasGroup>();
        ftue.typeTheWordPanelGroup = panel.GetComponent<CanvasGroup>();

        Transform skip = canvas.Find("SkipButton");
        if (skip != null) ftue.skipButton = skip.GetComponent<Button>();

        Transform skipPopup = canvas.Find("SkipPopup");
        if (skipPopup != null)
        {
            ftue.skipPopup = skipPopup.gameObject;
            Transform imReady = skipPopup.Find("Dialog/ImReadyButton");
            if (imReady != null) ftue.skipImReadyButton = imReady.GetComponent<Button>();
            Transform playAgain = skipPopup.Find("Dialog/PlayTutorialAgainButton");
            if (playAgain != null) ftue.skipPlayTutorialAgainButton = playAgain.GetComponent<Button>();
        }

        Transform compPopup = canvas.Find("CompletionPopup");
        if (compPopup != null)
        {
            ftue.completionPopup = compPopup.gameObject;
            Transform pfr = compPopup.Find("Dialog/PlayForRealButton");
            if (pfr != null) ftue.playForRealButton = pfr.GetComponent<Button>();
        }

        EditorUtility.SetDirty(ftue);
    }

    static T Load<T>(string spriteFileNameNoExt) where T : Object
    {
        string path = ArtDir + spriteFileNameNoExt + ".png";
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) Debug.LogError("[FTUERestyleBuilder] Could not load " + typeof(T).Name + " at " + path);
        return asset;
    }
}
