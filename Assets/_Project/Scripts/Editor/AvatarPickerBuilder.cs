using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor utility to construct and wire the Profile Avatar Selection UI
/// in the currently open MainMenu scene according to design specifications and ui reference.png.
/// </summary>
public static class AvatarPickerBuilder
{
    private const string TexturePath = "Assets/_Project/Art/Textures/profile 1";

    private static readonly string[] AvatarNames = new string[]
    {
        "char 1",
        "char 2",
        "char 4",
        "char 5",
        "char 6",
        "char 7",
        "char 8",
        "char 9"
    };

    [MenuItem("TypeKeep/Build Avatar Picker UI")]
    public static void Build()
    {
        GameObject canvasGO = GameObject.Find("Canvas");
        if (canvasGO == null)
        {
            Debug.LogError("[AvatarPickerBuilder] No 'Canvas' found in active scene. Open MainMenu.unity first.");
            return;
        }

        Transform canvas = canvasGO.transform;
        Transform profilePanelTrans = canvas.Find("ProfilePanel");
        if (profilePanelTrans == null)
        {
            Debug.LogError("[AvatarPickerBuilder] No 'ProfilePanel' found under Canvas.");
            return;
        }

        // Load Sprites from Assets/_Project/Art/Textures/profile 1
        Sprite avatarBannerSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{TexturePath}/avatar.png");
        Sprite backgroundFrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{TexturePath}/background frame.png");
        Sprite confirmBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{TexturePath}/confirm button.png");
        Sprite goldenRingSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{TexturePath}/golden ring.png");

        if (avatarBannerSprite == null || backgroundFrameSprite == null || confirmBtnSprite == null || goldenRingSprite == null)
        {
            Debug.LogError("[AvatarPickerBuilder] Failed to load required UI sprites from " + TexturePath);
            return;
        }

        var avatarSprites = new Dictionary<string, Sprite>();
        foreach (string name in AvatarNames)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>($"{TexturePath}/{name}.png");
            if (s == null)
            {
                Debug.LogError($"[AvatarPickerBuilder] Missing avatar sprite: {name}.png");
                return;
            }
            avatarSprites[name] = s;
        }

        // 1. Remove previous AvatarPickerPopup if already built (idempotent)
        Transform oldPopup = canvas.Find("AvatarPickerPopup");
        if (oldPopup != null)
        {
            Undo.DestroyObjectImmediate(oldPopup.gameObject);
        }

        // 2. Build AvatarPickerPopup root
        GameObject popupRoot = new GameObject("AvatarPickerPopup", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(popupRoot, "Create AvatarPickerPopup");
        popupRoot.transform.SetParent(canvas, false);
        popupRoot.transform.SetSiblingIndex(profilePanelTrans.GetSiblingIndex() + 1);

        RectTransform popupRT = popupRoot.GetComponent<RectTransform>();
        popupRT.anchorMin = Vector2.zero;
        popupRT.anchorMax = Vector2.one;
        popupRT.offsetMin = Vector2.zero;
        popupRT.offsetMax = Vector2.zero;

        ProfileAvatarPickerUI pickerUI = popupRoot.AddComponent<ProfileAvatarPickerUI>();

        // 3. Backdrop (semi-transparent dark overlay)
        GameObject backdropGO = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        backdropGO.transform.SetParent(popupRoot.transform, false);
        RectTransform backdropRT = backdropGO.GetComponent<RectTransform>();
        backdropRT.anchorMin = Vector2.zero;
        backdropRT.anchorMax = Vector2.one;
        backdropRT.offsetMin = Vector2.zero;
        backdropRT.offsetMax = Vector2.zero;

        Image backdropImage = backdropGO.GetComponent<Image>();
        backdropImage.color = new Color(0f, 0f, 0f, 0.75f);
        backdropImage.raycastTarget = true;
        Button backdropButton = backdropGO.GetComponent<Button>();
        backdropButton.transition = Selectable.Transition.None;

        // 4. Window (Dialog containing background frame)
        GameObject windowGO = new GameObject("Window", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        windowGO.transform.SetParent(popupRoot.transform, false);
        RectTransform windowRT = windowGO.GetComponent<RectTransform>();
        windowRT.anchorMin = new Vector2(0.5f, 0.5f);
        windowRT.anchorMax = new Vector2(0.5f, 0.5f);
        windowRT.pivot = new Vector2(0.5f, 0.5f);
        windowRT.sizeDelta = new Vector2(1000f, 914f);
        windowRT.anchoredPosition = new Vector2(0f, 30f);

        Image windowImage = windowGO.GetComponent<Image>();
        windowImage.sprite = backgroundFrameSprite;
        windowImage.type = Image.Type.Simple;
        windowImage.preserveAspect = true;
        windowImage.raycastTarget = true;

        // 5. Header Banner ("CHOOSE AVATAR")
        GameObject headerGO = new GameObject("HeaderBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        headerGO.transform.SetParent(windowGO.transform, false);
        RectTransform headerRT = headerGO.GetComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0.5f, 1f);
        headerRT.anchorMax = new Vector2(0.5f, 1f);
        headerRT.pivot = new Vector2(0.5f, 0.5f);
        headerRT.sizeDelta = new Vector2(620f, 206f);
        headerRT.anchoredPosition = new Vector2(0f, -95f);

        Image headerImage = headerGO.GetComponent<Image>();
        headerImage.sprite = avatarBannerSprite;
        headerImage.preserveAspect = true;
        headerImage.raycastTarget = false;

        // 6. Close Button (Over top-right red X on background frame)
        GameObject closeGO = new GameObject("Button_Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        closeGO.transform.SetParent(windowGO.transform, false);
        RectTransform closeRT = closeGO.GetComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1f, 1f);
        closeRT.anchorMax = new Vector2(1f, 1f);
        closeRT.pivot = new Vector2(0.5f, 0.5f);
        closeRT.sizeDelta = new Vector2(90f, 90f);
        closeRT.anchoredPosition = new Vector2(-68f, -145f);

        Image closeImage = closeGO.GetComponent<Image>();
        closeImage.color = new Color(0f, 0f, 0f, 0f); // Transparent hit target
        closeImage.raycastTarget = true;
        Button closeButton = closeGO.GetComponent<Button>();

        // 7. Grid for the 8 avatar thumbnails
        GameObject gridGO = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
        gridGO.transform.SetParent(windowGO.transform, false);
        RectTransform gridRT = gridGO.GetComponent<RectTransform>();
        gridRT.anchorMin = new Vector2(0.5f, 0.5f);
        gridRT.anchorMax = new Vector2(0.5f, 0.5f);
        gridRT.pivot = new Vector2(0.5f, 0.5f);
        gridRT.sizeDelta = new Vector2(760f, 380f);
        gridRT.anchoredPosition = new Vector2(0f, -40f);

        GridLayoutGroup layout = gridGO.GetComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(170f, 170f);
        layout.spacing = new Vector2(25f, 30f);
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.startAxis = GridLayoutGroup.Axis.Horizontal;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 4;

        var entries = new List<ProfileAvatarPickerUI.AvatarEntry>();
        Button firstButton = null;

        foreach (string name in AvatarNames)
        {
            GameObject btnGO = new GameObject($"Button_{name}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(gridGO.transform, false);

            RectTransform btnRT = btnGO.GetComponent<RectTransform>();
            btnRT.sizeDelta = new Vector2(170f, 170f);

            Image img = btnGO.GetComponent<Image>();
            img.sprite = avatarSprites[name];
            img.preserveAspect = true;
            img.raycastTarget = true;

            Button btn = btnGO.GetComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;

            if (firstButton == null) firstButton = btn;

            entries.Add(new ProfileAvatarPickerUI.AvatarEntry
            {
                avatarId = name,
                sprite = avatarSprites[name],
                button = btn
            });
        }

        // 8. Golden Ring Selection Indicator
        GameObject goldenRingGO = new GameObject("GoldenRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        goldenRingGO.transform.SetParent(firstButton != null ? firstButton.transform : windowGO.transform, false);
        RectTransform ringRT = goldenRingGO.GetComponent<RectTransform>();
        ringRT.anchorMin = new Vector2(0.5f, 0.5f);
        ringRT.anchorMax = new Vector2(0.5f, 0.5f);
        ringRT.pivot = new Vector2(0.5f, 0.5f);
        ringRT.sizeDelta = new Vector2(176f, 172f);
        ringRT.anchoredPosition = Vector2.zero;

        Image ringImage = goldenRingGO.GetComponent<Image>();
        ringImage.sprite = goldenRingSprite;
        ringImage.preserveAspect = true;
        ringImage.raycastTarget = false;

        // 9. Confirm Button
        GameObject confirmGO = new GameObject("Button_Confirm", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        confirmGO.transform.SetParent(windowGO.transform, false);
        RectTransform confirmRT = confirmGO.GetComponent<RectTransform>();
        confirmRT.anchorMin = new Vector2(0.5f, 0f);
        confirmRT.anchorMax = new Vector2(0.5f, 0f);
        confirmRT.pivot = new Vector2(0.5f, 0.5f);
        confirmRT.sizeDelta = new Vector2(530f, 178f);
        confirmRT.anchoredPosition = new Vector2(0f, 175f);

        Image confirmImage = confirmGO.GetComponent<Image>();
        confirmImage.sprite = confirmBtnSprite;
        confirmImage.preserveAspect = true;
        confirmImage.raycastTarget = true;
        Button confirmButton = confirmGO.GetComponent<Button>();

        // Set UI Layer (5) recursively
        SetLayerRecursively(popupRoot, 5);

        // 10. Wire ProfileAvatarPickerUI
        pickerUI.Configure(
            popupRoot,
            headerImage,
            windowImage,
            confirmButton,
            closeButton,
            backdropButton,
            ringRT,
            null, // Preview image will be linked to RankCard avatar
            entries
        );

        // 11. Wire ProfilePanel (RankHeader circular avatar button and display)
        Transform rankHeaderTrans = profilePanelTrans.Find("Content/RankHeader");
        if (rankHeaderTrans != null)
        {
            Transform oldAvatarSlot = rankHeaderTrans.Find("AvatarButton");
            if (oldAvatarSlot != null)
            {
                Undo.DestroyObjectImmediate(oldAvatarSlot.gameObject);
            }

            GameObject avatarBtnGO = new GameObject("AvatarButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(AvatarDisplay));
            Undo.RegisterCreatedObjectUndo(avatarBtnGO, "Create AvatarButton in RankHeader");
            avatarBtnGO.transform.SetParent(rankHeaderTrans, false);

            RectTransform avatarBtnRT = avatarBtnGO.GetComponent<RectTransform>();
            avatarBtnRT.anchorMin = new Vector2(0f, 0.5f);
            avatarBtnRT.anchorMax = new Vector2(0f, 0.5f);
            avatarBtnRT.pivot = new Vector2(0.5f, 0.5f);
            avatarBtnRT.sizeDelta = new Vector2(150f, 150f);
            avatarBtnRT.anchoredPosition = new Vector2(152f, 0f);

            Image avatarImg = avatarBtnGO.GetComponent<Image>();
            avatarImg.sprite = avatarSprites["char 1"];
            avatarImg.preserveAspect = true;
            avatarImg.raycastTarget = true;

            Button avatarBtn = avatarBtnGO.GetComponent<Button>();
            avatarBtn.transition = Selectable.Transition.ColorTint;

            // Wire ProfilePanelUI
            ProfilePanelUI profilePanelUI = profilePanelTrans.GetComponent<ProfilePanelUI>();
            if (profilePanelUI != null)
            {
                profilePanelUI.avatarImage = avatarImg;
                profilePanelUI.avatarButton = avatarBtn;
                profilePanelUI.avatarPicker = pickerUI;
                EditorUtility.SetDirty(profilePanelUI);
            }
        }

        // 12. Main Menu Button_Profile reflection
        Transform profileBtn = canvas.Find("Button_Profile");
        if (profileBtn != null)
        {
            Transform normalIcon = profileBtn.Find("Nomal/Icon");
            if (normalIcon != null && normalIcon.GetComponent<AvatarDisplay>() == null)
            {
                normalIcon.gameObject.AddComponent<AvatarDisplay>();
                EditorUtility.SetDirty(normalIcon.gameObject);
            }
            Transform focusIcon = profileBtn.Find("Focus/Icon");
            if (focusIcon != null && focusIcon.GetComponent<AvatarDisplay>() == null)
            {
                focusIcon.gameObject.AddComponent<AvatarDisplay>();
                EditorUtility.SetDirty(focusIcon.gameObject);
            }
        }

        // Popup starts closed
        popupRoot.SetActive(false);

        EditorUtility.SetDirty(popupRoot);
        EditorUtility.SetDirty(canvasGO);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log("[AvatarPickerBuilder] Avatar picker UI successfully built and wired into MainMenu scene.");
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}
