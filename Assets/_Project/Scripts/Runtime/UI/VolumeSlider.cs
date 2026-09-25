using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable Volume Slider UI component supporting Master, Music, and SFX categories.
/// Features touch-friendly handle, real-time AudioMixer updates, percentage display,
/// tick sounds on adjustment, mute toggle with volume restoration, and multi-menu synchronization.
/// </summary>
public class VolumeSlider : MonoBehaviour
{
    #region Serialized Fields
    [Header("Category Configuration")]
    [Tooltip("Target audio category controlled by this slider")]
    [SerializeField] private AudioCategory _category = AudioCategory.Master;

    [Tooltip("Custom display name override. If blank, uses category name.")]
    [SerializeField] private string _customLabel;

    [Header("UI Components")]
    [Tooltip("Unity UI Slider component (0..1 range)")]
    [SerializeField] private Slider _slider;

    [Tooltip("TextMeshPro label displaying the category title")]
    [SerializeField] private TMP_Text _categoryLabel;

    [Tooltip("TextMeshPro text displaying current percentage (e.g. 75%)")]
    [SerializeField] private TMP_Text _percentageText;

    [Tooltip("Button toggling mute for this category")]
    [SerializeField] private Button _muteButton;

    [Tooltip("Image component displaying the speaker icon")]
    [SerializeField] private Image _speakerIcon;

    [Tooltip("Sprite shown when volume > 0")]
    [SerializeField] private Sprite _speakerNormalSprite;

    [Tooltip("Sprite shown when volume is 0 or muted")]
    [SerializeField] private Sprite _speakerMutedSprite;

    [Tooltip("Optional badge or category icon")]
    [SerializeField] private Image _categoryBadge;

    [Tooltip("Fill Image component using Filled horizontal mode for smooth unclipped filling")]
    [SerializeField] private Image _fillImage;
    #endregion

    #region Private State
    private float _preMuteVolume = AudioManager.DefaultVolume;
    private bool _isMuted = false;
    private bool _isInternalUpdate = false;
    private int _lastPercentage = -1;
    private const float SnapToZeroThreshold = 0.015f;
    #endregion

    #region Properties
    public AudioCategory Category => _category;
    public Slider SliderComponent => _slider;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (_slider == null)
            _slider = GetComponentInChildren<Slider>();

        if (_categoryLabel == null)
        {
            var labels = GetComponentsInChildren<TMP_Text>();
            foreach (var l in labels)
            {
                if (l != _percentageText)
                {
                    _categoryLabel = l;
                    break;
                }
            }
        }

        UpdateCategoryLabel();
    }

    private void Start()
    {
        InitializeControls();
        SyncFromAudioManager();
    }

    private void OnEnable()
    {
        SubscribeEvents();
        SyncFromAudioManager();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }
    #endregion

    #region Setup & Initialization
    public void SetCategory(AudioCategory category)
    {
        _category = category;
        UpdateCategoryLabel();
        SyncFromAudioManager();
    }

    private void UpdateCategoryLabel()
    {
        if (_categoryLabel != null)
        {
            _categoryLabel.text = !string.IsNullOrEmpty(_customLabel) 
                ? _customLabel 
                : _category.ToString().ToUpper();
        }
    }

    private void InitializeControls()
    {
        if (_slider != null)
        {
            _slider.minValue = 0f;
            _slider.maxValue = 1f;
            _slider.wholeNumbers = false;
            _slider.onValueChanged.RemoveListener(OnSliderValueChanged);
            _slider.onValueChanged.AddListener(OnSliderValueChanged);
        }

        if (_muteButton != null)
        {
            _muteButton.onClick.RemoveListener(ToggleMute);
            _muteButton.onClick.AddListener(ToggleMute);
        }
    }

    private void SubscribeEvents()
    {
        if (AudioManager.Instance == null) return;

        switch (_category)
        {
            case AudioCategory.Master:
                AudioManager.Instance.OnMasterVolumeChanged += OnExternalVolumeChanged;
                break;
            case AudioCategory.Music:
                AudioManager.Instance.OnMusicVolumeChanged += OnExternalVolumeChanged;
                break;
            case AudioCategory.SFX:
                AudioManager.Instance.OnSFXVolumeChanged += OnExternalVolumeChanged;
                break;
        }
    }

    private void UnsubscribeEvents()
    {
        if (AudioManager.Instance == null) return;

        switch (_category)
        {
            case AudioCategory.Master:
                AudioManager.Instance.OnMasterVolumeChanged -= OnExternalVolumeChanged;
                break;
            case AudioCategory.Music:
                AudioManager.Instance.OnMusicVolumeChanged -= OnExternalVolumeChanged;
                break;
            case AudioCategory.SFX:
                AudioManager.Instance.OnSFXVolumeChanged -= OnExternalVolumeChanged;
                break;
        }
    }
    #endregion

    #region Value Synchronization
    public void SyncFromAudioManager()
    {
        float currentVol = AudioManager.Instance != null 
            ? AudioManager.Instance.GetVolume(_category) 
            : AudioManager.DefaultVolume;

        _isInternalUpdate = true;
        if (_slider != null)
        {
            _slider.value = currentVol;
        }
        _isInternalUpdate = false;

        UpdateDisplay(currentVol);

        if (currentVol > SnapToZeroThreshold)
        {
            _preMuteVolume = currentVol;
            _isMuted = false;
        }
        else
        {
            _isMuted = true;
        }
        UpdateSpeakerVisuals();
    }

    private void OnExternalVolumeChanged(float newVolume)
    {
        if (_isInternalUpdate) return;

        _isInternalUpdate = true;
        if (_slider != null)
        {
            _slider.value = newVolume;
        }
        _isInternalUpdate = false;

        UpdateDisplay(newVolume);

        if (newVolume > SnapToZeroThreshold)
        {
            _preMuteVolume = newVolume;
            _isMuted = false;
        }
        else
        {
            _isMuted = true;
        }
        UpdateSpeakerVisuals();
    }
    #endregion

    #region User Interactions
    private void OnSliderValueChanged(float value)
    {
        if (_isInternalUpdate) return;

        // Smooth dragging, snap cleanly to 0 only when dragged very close to bottom
        if (value < SnapToZeroThreshold)
        {
            value = 0f;
            if (_slider != null && _slider.value != 0f)
            {
                _isInternalUpdate = true;
                _slider.value = 0f;
                _isInternalUpdate = false;
            }
        }

        // Apply immediately to AudioManager for real-time response
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetVolume(_category, value);
        }

        int percent = Mathf.RoundToInt(value * 100f);
        if (percent != _lastPercentage)
        {
            _lastPercentage = percent;
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySliderTick();
            }
        }

        UpdateDisplay(value);

        if (value > SnapToZeroThreshold)
        {
            _preMuteVolume = value;
            _isMuted = false;
        }
        else
        {
            _isMuted = true;
        }
        UpdateSpeakerVisuals();
    }

    public void ToggleMute()
    {
        if (_isMuted || (_slider != null && _slider.value <= SnapToZeroThreshold))
        {
            // Unmute: restore previously remembered volume (default 0.75 if previous was 0)
            float restoreVol = _preMuteVolume > SnapToZeroThreshold ? _preMuteVolume : AudioManager.DefaultVolume;
            _isMuted = false;
            ApplyNewVolume(restoreVol);
        }
        else
        {
            // Mute: remember current volume and drop to 0
            if (_slider != null && _slider.value > SnapToZeroThreshold)
            {
                _preMuteVolume = _slider.value;
            }
            _isMuted = true;
            ApplyNewVolume(0f);
        }
    }

    private void ApplyNewVolume(float val)
    {
        _isInternalUpdate = true;
        if (_slider != null)
        {
            _slider.value = val;
        }
        _isInternalUpdate = false;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetVolume(_category, val);
            AudioManager.Instance.PlaySliderTick();
        }

        UpdateDisplay(val);
        UpdateSpeakerVisuals();
    }
    #endregion

    #region Visual Feedback
    private void UpdateDisplay(float value)
    {
        if (_fillImage != null && _fillImage.type == Image.Type.Filled)
        {
            _fillImage.fillAmount = value;
        }

        if (_percentageText != null)
        {
            int percent = Mathf.RoundToInt(value * 100f);
            _percentageText.text = percent + "%";
        }
    }

    private void UpdateSpeakerVisuals()
    {
        if (_speakerIcon == null) return;

        bool isSilent = _isMuted || (_slider != null && _slider.value <= SnapToZeroThreshold);
        if (_speakerMutedSprite != null && _speakerNormalSprite != null)
        {
            _speakerIcon.sprite = isSilent ? _speakerMutedSprite : _speakerNormalSprite;
        }
        else
        {
            // Fallback color tint if single sprite
            _speakerIcon.color = isSilent ? new Color(0.85f, 0.25f, 0.25f, 0.9f) : Color.white;
        }
    }
    #endregion
}
