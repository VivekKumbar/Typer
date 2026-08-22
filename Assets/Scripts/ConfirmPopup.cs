using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A single reusable Yes/No confirmation popup — title, message, Confirm/
// Cancel. Call Show(title, message, onConfirm, onCancel) to display it with
// whatever text/callbacks the calling flow needs; onCancel is optional since
// most callers just want the popup to close with no further action. Same
// simple show/hide pattern as the shop's Not Enough Coins popup, just with
// confirm/cancel actions attached instead of a single OK button. One instance
// of this in the scene is meant to be shared by every confirm flow (New Game,
// Continue, ...) — don't create a separate popup per flow, just call Show()
// again with different text/callbacks.
//
// Continue also wants a small build-preview row (icon + level per unlocked
// upgrade) under the message, a second row showing which word packs are
// locked in for that save, and the Dialog's own background swapped to that
// save's locked-in Ground Skin — that's what ShowWithPreview is for. Show()
// (used by New Game) always clears both rows and resets the background, so
// the flows sharing one popup instance never bleed state into each other.
public class ConfirmPopup : MonoBehaviour
{
    [Header("Refs")]
    public GameObject panel;       // the popup root, disabled by default
    public TMP_Text titleText;
    public TMP_Text messageText;
    public Button confirmButton;
    public Button cancelButton;

    [Header("Ability preview row (optional — Continue only)")]
    [Tooltip("Horizontal Layout Group container the preview icons spawn into. Leave empty to disable the preview row entirely.")]
    public Transform previewRoot;
    [Tooltip("Small icon+level prefab, one instantiated per unlocked upgrade (level > 0).")]
    public AbilityPreviewIcon previewIconPrefab;

    [Header("Word Pack preview row (optional — Continue only)")]
    [Tooltip("Horizontal Layout Group container the word-pack preview icons spawn into. Leave empty to disable this row entirely.")]
    public Transform wordPackPreviewRoot;
    [Tooltip("Small icon+name prefab, one instantiated per word pack locked in for the saved run.")]
    public WordPackPreviewIcon wordPackIconPrefab;
    [Tooltip("PLACEHOLDER sprite — used for any pack whose real icon isn't assigned yet in its ShopItem, AND for the 'Default Words' entry shown when no packs were selected. Replace per-pack art on each Word Pack ShopItem in the Shop catalog; this field just stays as the generic fallback.")]
    public Sprite placeholderPackIcon;

    [Header("Ground Skin background (optional — Continue only)")]
    [Tooltip("The Dialog's own background Image -- swapped to the SAVED run's locked-in Ground Skin for Continue.")]
    public Image dialogBackground;
    [Tooltip("The normal popup panel art -- what Dialog Background resets to for every popup OTHER than Continue (New Game, ...). Assign the same sprite Dialog Background normally shows (e.g. Carausel_1_normal). Same fallback-sprite pattern as Placeholder Pack Icon above.")]
    public Sprite defaultDialogBackgroundSprite;

    private Action onConfirm;
    private Action onCancel;

    void Start()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
        if (panel != null) panel.SetActive(false);
    }

    public void Show(string title, string message, Action confirmCallback, Action cancelCallback = null)
    {
        ShowInternal(title, message, confirmCallback, cancelCallback, null, null, null);
    }

    // Same as Show(), but also populates the ability icon+level row, the
    // word-pack icon+name row, and the Dialog background beneath/behind the
    // message. Pass null for any of them to skip/reset that part.
    public void ShowWithPreview(string title, string message,
        List<(UpgradeDefinition def, int level)> abilityPreview,
        List<ShopItem> wordPackPreview,
        Sprite groundSkinBackground,
        Action confirmCallback, Action cancelCallback = null)
    {
        ShowInternal(title, message, confirmCallback, cancelCallback, abilityPreview, wordPackPreview, groundSkinBackground);
    }

    void ShowInternal(string title, string message, Action confirmCallback, Action cancelCallback,
        List<(UpgradeDefinition def, int level)> abilityPreview, List<ShopItem> wordPackPreview, Sprite groundSkinBackground)
    {
        onConfirm = confirmCallback;
        onCancel = cancelCallback;
        if (titleText != null) titleText.text = title;
        if (messageText != null) messageText.text = message;

        RebuildAbilityPreview(abilityPreview);
        RebuildWordPackPreview(wordPackPreview);
        if (dialogBackground != null)
            dialogBackground.sprite = groundSkinBackground != null ? groundSkinBackground : defaultDialogBackgroundSprite;

        if (panel != null) panel.SetActive(true);
    }

    void RebuildAbilityPreview(List<(UpgradeDefinition def, int level)> preview)
    {
        if (previewRoot == null) return;

        foreach (Transform child in previewRoot) Destroy(child.gameObject);
        if (preview == null || previewIconPrefab == null) return;

        foreach (var entry in preview)
        {
            if (entry.def == null || entry.level <= 0) continue;
            AbilityPreviewIcon icon = Instantiate(previewIconPrefab, previewRoot);
            icon.Setup(entry.def, entry.level);
        }
    }

    // Packs aren't leveled, so this is just icon + name — no badge logic like
    // the ability row's boss glow. An empty/null list means no packs were
    // locked in for that run (default word list only), shown as a single
    // "Default Words" placeholder entry instead of leaving the row blank.
    void RebuildWordPackPreview(List<ShopItem> wordPacks)
    {
        if (wordPackPreviewRoot == null) return;

        foreach (Transform child in wordPackPreviewRoot) Destroy(child.gameObject);
        if (wordPackIconPrefab == null) return;

        if (wordPacks == null || wordPacks.Count == 0)
        {
            WordPackPreviewIcon defaultIcon = Instantiate(wordPackIconPrefab, wordPackPreviewRoot);
            defaultIcon.Setup(placeholderPackIcon, "Default Words");
            return;
        }

        foreach (ShopItem pack in wordPacks)
        {
            if (pack == null) continue;
            Sprite sprite = pack.icon != null ? pack.icon : placeholderPackIcon;
            WordPackPreviewIcon icon = Instantiate(wordPackIconPrefab, wordPackPreviewRoot);
            icon.Setup(sprite, pack.displayName);
        }
    }

    void OnConfirmClicked()
    {
        if (panel != null) panel.SetActive(false);
        Action cb = onConfirm;
        onConfirm = null;
        onCancel = null;
        cb?.Invoke();
    }

    void OnCancelClicked()
    {
        Action cb = onCancel;
        onConfirm = null;
        onCancel = null;
        if (panel != null) panel.SetActive(false);
        cb?.Invoke();
    }
}
