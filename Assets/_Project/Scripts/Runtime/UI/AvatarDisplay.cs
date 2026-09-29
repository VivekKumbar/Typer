using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the active player avatar on an Image component and updates automatically
/// when the player chooses a different avatar.
/// </summary>
public class AvatarDisplay : MonoBehaviour
{
    [Tooltip("The Image component displaying the avatar. If unset, looks on this GameObject.")]
    [SerializeField] private Image _avatarImage;

    [Tooltip("If true, clicking this object opens the ProfileAvatarPickerUI dialog.")]
    [SerializeField] private bool _openPickerOnClick = true;

    private Button _button;

    private void Awake()
    {
        if (_avatarImage == null)
        {
            _avatarImage = GetComponent<Image>();
        }

        if (_openPickerOnClick)
        {
            _button = GetComponent<Button>();
            if (_button != null)
            {
                _button.onClick.AddListener(OnAvatarClicked);
            }
        }
    }

    private void OnEnable()
    {
        StatsManager.OnAvatarChanged += HandleAvatarChanged;
        Refresh();
    }

    private void OnDisable()
    {
        StatsManager.OnAvatarChanged -= HandleAvatarChanged;
    }

    /// <summary>
    /// Refreshes the displayed sprite based on the current persisted avatar.
    /// </summary>
    public void Refresh()
    {
        if (_avatarImage == null) return;

        string currentAvatarId = StatsManager.SelectedAvatar;
        if (ProfileAvatarPickerUI.Instance != null)
        {
            Sprite sprite = ProfileAvatarPickerUI.Instance.GetAvatarSprite(currentAvatarId);
            if (sprite != null)
            {
                _avatarImage.sprite = sprite;
                _avatarImage.enabled = true;
            }
        }
    }

    private void HandleAvatarChanged(string newAvatarId)
    {
        Refresh();
    }

    private void OnAvatarClicked()
    {
        if (ProfileAvatarPickerUI.Instance != null)
        {
            ProfileAvatarPickerUI.Instance.Show();
        }
    }
}
