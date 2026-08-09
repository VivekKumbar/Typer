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
// upgrade) under the message — that's what ShowWithPreview is for. Show()
// (used by New Game) always clears the row, so the two flows sharing one
// popup instance never bleed icons into each other.
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
        ShowInternal(title, message, confirmCallback, cancelCallback, null);
    }

    // Same as Show(), but also populates the icon+level preview row beneath
    // the message. Pass null (or an empty list) for a normal popup with no row.
    public void ShowWithPreview(string title, string message, List<(UpgradeDefinition def, int level)> preview, Action confirmCallback, Action cancelCallback = null)
    {
        ShowInternal(title, message, confirmCallback, cancelCallback, preview);
    }

    void ShowInternal(string title, string message, Action confirmCallback, Action cancelCallback, List<(UpgradeDefinition def, int level)> preview)
    {
        onConfirm = confirmCallback;
        onCancel = cancelCallback;
        if (titleText != null) titleText.text = title;
        if (messageText != null) messageText.text = message;

        RebuildPreview(preview);

        if (panel != null) panel.SetActive(true);
    }

    void RebuildPreview(List<(UpgradeDefinition def, int level)> preview)
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
