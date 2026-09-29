using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the Profile Avatar Selection popup UI.
/// Displays the 8 selectable avatar portraits in a 2x4 grid, highlighting the chosen one
/// with the golden ring indicator, and persists the confirmed avatar across sessions.
/// </summary>
public class ProfileAvatarPickerUI : MonoBehaviour
{
    public static ProfileAvatarPickerUI Instance { get; private set; }

    [Serializable]
    public class AvatarEntry
    {
        public string avatarId;
        public Sprite sprite;
        public Button button;
    }

    [Header("Popup Containers")]
    [Tooltip("Root GameObject of the avatar selection popup panel.")]
    [SerializeField] private GameObject _popupRoot;

    [Tooltip("Header banner Image displaying the 'avatar' sprite ('CHOOSE AVATAR').")]
    [SerializeField] private Image _headerBanner;

    [Tooltip("Background dialog frame Image displaying the 'background frame' sprite.")]
    [SerializeField] private Image _backgroundFrame;

    [Tooltip("Confirm button with the 'confirm button' sprite.")]
    [SerializeField] private Button _confirmButton;

    [Tooltip("Close button overlaying the red X on the background frame.")]
    [SerializeField] private Button _closeButton;

    [Tooltip("Optional backdrop overlay button to close popup on outside tap.")]
    [SerializeField] private Button _backdropButton;

    [Header("Selection Indicator & Preview")]
    [Tooltip("Selection-indicator ring with checkmark overlayed on the selected portrait.")]
    [SerializeField] private RectTransform _goldenRing;

    [Tooltip("Preview Image displaying the currently-selected avatar.")]
    [SerializeField] private Image _previewImage;

    [Header("Selectable Avatars (8 portraits: char 1, 2, 4, 5, 6, 7, 8, 9)")]
    [SerializeField] private List<AvatarEntry> _avatars = new List<AvatarEntry>();

    private string _tentativeAvatarId = StatsManager.DEFAULT_AVATAR;

    public string CurrentTentativeAvatarId => _tentativeAvatarId;
    public IReadOnlyList<AvatarEntry> Avatars => _avatars;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        if (_confirmButton != null)
        {
            _confirmButton.onClick.AddListener(ConfirmSelection);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(CancelSelection);
        }

        if (_backdropButton != null)
        {
            _backdropButton.onClick.AddListener(CancelSelection);
        }

        for (int i = 0; i < _avatars.Count; i++)
        {
            var entry = _avatars[i];
            if (entry != null && entry.button != null)
            {
                string id = entry.avatarId;
                entry.button.onClick.AddListener(() => SelectAvatar(id));
            }
        }
    }

    private void Start()
    {
        RefreshFromSaved();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Opens the avatar selection popup dialog.
    /// </summary>
    public void Show()
    {
        _tentativeAvatarId = StatsManager.SelectedAvatar;
        if (_popupRoot != null)
        {
            _popupRoot.SetActive(true);
        }
        else
        {
            gameObject.SetActive(true);
        }

        UpdateSelectionUI(_tentativeAvatarId);
    }

    /// <summary>
    /// Closes the avatar selection popup dialog.
    /// </summary>
    public void Hide()
    {
        if (_popupRoot != null)
        {
            _popupRoot.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Selects an avatar portrait without saving immediately (pending confirmation).
    /// </summary>
    public void SelectAvatar(string avatarId)
    {
        if (string.IsNullOrEmpty(avatarId)) return;

        _tentativeAvatarId = avatarId;
        UpdateSelectionUI(avatarId);
    }

    /// <summary>
    /// Commits the tentative selection to the persistence system and closes the popup.
    /// </summary>
    public void ConfirmSelection()
    {
        StatsManager.SelectedAvatar = _tentativeAvatarId;
        Hide();
    }

    /// <summary>
    /// Cancels tentative changes and reverts to the persisted avatar selection.
    /// </summary>
    public void CancelSelection()
    {
        _tentativeAvatarId = StatsManager.SelectedAvatar;
        UpdateSelectionUI(_tentativeAvatarId);
        Hide();
    }

    /// <summary>
    /// Refreshes UI based on the current persisted avatar.
    /// </summary>
    public void RefreshFromSaved()
    {
        _tentativeAvatarId = StatsManager.SelectedAvatar;
        UpdateSelectionUI(_tentativeAvatarId);
    }

    /// <summary>
    /// Updates the golden ring position and preview image to reflect the specified avatar.
    /// </summary>
    public void UpdateSelectionUI(string avatarId)
    {
        Sprite sprite = GetAvatarSprite(avatarId);

        if (_previewImage != null && sprite != null)
        {
            _previewImage.sprite = sprite;
        }

        if (_goldenRing != null)
        {
            AvatarEntry matched = null;
            for (int i = 0; i < _avatars.Count; i++)
            {
                if (_avatars[i] != null && string.Equals(_avatars[i].avatarId, avatarId, StringComparison.OrdinalIgnoreCase))
                {
                    matched = _avatars[i];
                    break;
                }
            }

            if (matched != null && matched.button != null)
            {
                _goldenRing.gameObject.SetActive(true);
                _goldenRing.SetParent(matched.button.transform, false);
                _goldenRing.anchorMin = new Vector2(0.5f, 0.5f);
                _goldenRing.anchorMax = new Vector2(0.5f, 0.5f);
                _goldenRing.pivot = new Vector2(0.5f, 0.5f);
                _goldenRing.anchoredPosition = Vector2.zero;
                // Scale golden ring slightly to match thumbnail diameter
                RectTransform btnRT = matched.button.GetComponent<RectTransform>();
                if (btnRT != null)
                {
                    _goldenRing.sizeDelta = btnRT.sizeDelta;
                }
                _goldenRing.SetAsLastSibling();
            }
            else
            {
                _goldenRing.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Retrieves the Sprite associated with the given avatar ID.
    /// </summary>
    public Sprite GetAvatarSprite(string avatarId)
    {
        if (string.IsNullOrEmpty(avatarId)) avatarId = StatsManager.DEFAULT_AVATAR;

        for (int i = 0; i < _avatars.Count; i++)
        {
            if (_avatars[i] != null && string.Equals(_avatars[i].avatarId, avatarId, StringComparison.OrdinalIgnoreCase))
            {
                return _avatars[i].sprite;
            }
        }

        // Fallback to first available sprite
        if (_avatars.Count > 0 && _avatars[0] != null)
        {
            return _avatars[0].sprite;
        }

        return null;
    }

#if UNITY_EDITOR
    public void Configure(
        GameObject popupRoot,
        Image headerBanner,
        Image backgroundFrame,
        Button confirmButton,
        Button closeButton,
        Button backdropButton,
        RectTransform goldenRing,
        Image previewImage,
        List<AvatarEntry> avatars)
    {
        _popupRoot = popupRoot;
        _headerBanner = headerBanner;
        _backgroundFrame = backgroundFrame;
        _confirmButton = confirmButton;
        _closeButton = closeButton;
        _backdropButton = backdropButton;
        _goldenRing = goldenRing;
        _previewImage = previewImage;
        _avatars = avatars;
    }
#endif
}
