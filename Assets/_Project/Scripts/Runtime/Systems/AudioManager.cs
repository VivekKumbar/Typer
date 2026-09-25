using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public enum AudioCategory
{
    Master,
    Music,
    SFX
}

/// <summary>
/// Singleton Audio Manager controlling AudioMixer groups via logarithmic dB conversion,
/// persisting volume preferences via PlayerPrefs, and managing real-time audio settings across scenes.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    #region Constants & PlayerPrefs Keys
    public const string MasterVolumeKey = "MasterVolume";
    public const string MusicVolumeKey = "MusicVolume";
    public const string SFXVolumeKey = "SFXVolume";

    public const string MasterVolumeParam = "MasterVolume";
    public const string MusicVolumeParam = "MusicVolume";
    public const string SFXVolumeParam = "SFXVolume";

    public const float DefaultVolume = 0.75f;
    public const float MinVolumeThreshold = 0.0001f;
    public const float MinDecibels = -80f;
    public const float MaxDecibels = 0f;
    #endregion

    #region Serialized Fields
    [Header("Audio Mixer Configuration")]
    [Tooltip("Primary AudioMixer containing exposed parameters: MasterVolume, MusicVolume, SFXVolume")]
    [SerializeField] private AudioMixer _audioMixer;

    [Tooltip("AudioMixer group for Master")]
    [SerializeField] private AudioMixerGroup _masterGroup;

    [Tooltip("AudioMixer group for Background Music")]
    [SerializeField] private AudioMixerGroup _musicGroup;

    [Tooltip("AudioMixer group for Sound Effects")]
    [SerializeField] private AudioMixerGroup _sfxGroup;

    [Header("UI Feedback")]
    [Tooltip("AudioSource configured to ignore listener pause for unscaled UI feedback")]
    [SerializeField] private AudioSource _uiAudioSource;

    [Tooltip("Subtle tick / click audio clip played when dragging volume sliders")]
    [SerializeField] private AudioClip _sliderTickClip;
    #endregion

    #region Events
    public event Action<float> OnMasterVolumeChanged;
    public event Action<float> OnMusicVolumeChanged;
    public event Action<float> OnSFXVolumeChanged;
    #endregion

    #region Private State
    private float _masterVolume = DefaultVolume;
    private float _musicVolume = DefaultVolume;
    private float _sfxVolume = DefaultVolume;
    private float _lastTickTime;
    private const float MinTickInterval = 0.04f; // Limit to ~25 ticks/sec for crisp UX
    #endregion

    #region Properties
    public AudioMixer Mixer => _audioMixer;
    public AudioMixerGroup MasterGroup => _masterGroup;
    public AudioMixerGroup MusicGroup => _musicGroup;
    public AudioMixerGroup SFXGroup => _sfxGroup;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureAudioMixerAssigned();
        EnsureUIAudioSource();
        LoadVolumes();
    }

    private void Start()
    {
        ApplyAllVolumes();
        RouteExistingAudioSources();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }
    #endregion

    #region Volume Getters & Setters
    public float GetVolume(AudioCategory category)
    {
        switch (category)
        {
            case AudioCategory.Master: return _masterVolume;
            case AudioCategory.Music: return _musicVolume;
            case AudioCategory.SFX: return _sfxVolume;
            default: return DefaultVolume;
        }
    }

    public void SetVolume(AudioCategory category, float value)
    {
        value = Mathf.Clamp01(value);
        switch (category)
        {
            case AudioCategory.Master:
                SetMasterVolume(value);
                break;
            case AudioCategory.Music:
                SetMusicVolume(value);
                break;
            case AudioCategory.SFX:
                SetSFXVolume(value);
                break;
        }
    }

    public void SetMasterVolume(float value)
    {
        _masterVolume = Mathf.Clamp01(value);
        ApplyVolumeToMixer(MasterVolumeParam, _masterVolume);
        PlayerPrefs.SetFloat(MasterVolumeKey, _masterVolume);
        BridgeStorageSync.SetFloat(MasterVolumeKey, _masterVolume);
        PlayerPrefs.Save();
        OnMasterVolumeChanged?.Invoke(_masterVolume);
    }

    public float GetMasterVolume() => _masterVolume;

    public void SetMusicVolume(float value)
    {
        _musicVolume = Mathf.Clamp01(value);
        ApplyVolumeToMixer(MusicVolumeParam, _musicVolume);
        PlayerPrefs.SetFloat(MusicVolumeKey, _musicVolume);
        BridgeStorageSync.SetFloat(MusicVolumeKey, _musicVolume);
        PlayerPrefs.Save();
        OnMusicVolumeChanged?.Invoke(_musicVolume);
    }

    public float GetMusicVolume() => _musicVolume;

    public void SetSFXVolume(float value)
    {
        _sfxVolume = Mathf.Clamp01(value);
        ApplyVolumeToMixer(SFXVolumeParam, _sfxVolume);
        PlayerPrefs.SetFloat(SFXVolumeKey, _sfxVolume);
        BridgeStorageSync.SetFloat(SFXVolumeKey, _sfxVolume);
        PlayerPrefs.Save();
        OnSFXVolumeChanged?.Invoke(_sfxVolume);
    }

    public float GetSFXVolume() => _sfxVolume;
    #endregion

    #region Audio Mathematics (Linear to Logarithmic dB)
    /// <summary>
    /// Converts linear slider value (0..1) to decibels (-80dB..0dB).
    /// Clamps minimum slider value to 0.0001f to avoid Log10(0).
    /// </summary>
    public static float LinearToDecibels(float linear)
    {
        if (linear <= MinVolumeThreshold)
            return MinDecibels;

        float clamped = Mathf.Clamp(linear, MinVolumeThreshold, 1f);
        return Mathf.Log10(clamped) * 20f;
    }

    /// <summary>
    /// Converts decibels (-80dB..0dB) to linear slider value (0..1).
    /// </summary>
    public static float DecibelsToLinear(float decibels)
    {
        if (decibels <= MinDecibels)
            return 0f;

        return Mathf.Clamp01(Mathf.Pow(10f, decibels / 20f));
    }
    #endregion

    #region Audio Routing & Persistence
    public void LoadVolumes()
    {
        _masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, DefaultVolume);
        _musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, DefaultVolume);
        _sfxVolume = PlayerPrefs.GetFloat(SFXVolumeKey, DefaultVolume);
    }

    public void ApplyAllVolumes()
    {
        ApplyVolumeToMixer(MasterVolumeParam, _masterVolume);
        ApplyVolumeToMixer(MusicVolumeParam, _musicVolume);
        ApplyVolumeToMixer(SFXVolumeParam, _sfxVolume);
    }

    private void ApplyVolumeToMixer(string paramName, float linearValue)
    {
        if (_audioMixer == null) return;
        float db = LinearToDecibels(linearValue);
        _audioMixer.SetFloat(paramName, db);
    }

    private void RouteExistingAudioSources()
    {
        if (MusicManager.Instance != null && _musicGroup != null)
        {
            var musicSrc = MusicManager.Instance.GetComponent<AudioSource>();
            if (musicSrc != null && musicSrc.outputAudioMixerGroup != _musicGroup)
            {
                musicSrc.outputAudioMixerGroup = _musicGroup;
            }
        }

        if (SfxPlayer.Instance != null && _sfxGroup != null)
        {
            var sfxSrc = SfxPlayer.Instance.GetComponent<AudioSource>();
            if (sfxSrc != null && sfxSrc.outputAudioMixerGroup != _sfxGroup)
            {
                sfxSrc.outputAudioMixerGroup = _sfxGroup;
            }
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RouteExistingAudioSources();
        ApplyAllVolumes();
    }
    #endregion

    #region UI Sound Playback
    public void PlaySliderTick()
    {
        if (Time.unscaledTime - _lastTickTime < MinTickInterval) return;
        _lastTickTime = Time.unscaledTime;

        if (_uiAudioSource != null && _sliderTickClip != null && _sfxVolume > MinVolumeThreshold)
        {
            _uiAudioSource.PlayOneShot(_sliderTickClip, 0.4f);
        }
    }
    #endregion

    #region Auto Configuration
    private void EnsureAudioMixerAssigned()
    {
        if (_audioMixer == null)
        {
            _audioMixer = Resources.Load<AudioMixer>("MainAudioMixer");
#if UNITY_EDITOR
            if (_audioMixer == null)
            {
                _audioMixer = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/_Project/Audio/MainAudioMixer.mixer");
            }
#endif
        }

        if (_audioMixer != null)
        {
            if (_masterGroup == null)
            {
                var matches = _audioMixer.FindMatchingGroups("Master");
                if (matches != null && matches.Length > 0) _masterGroup = matches[0];
            }
            if (_musicGroup == null)
            {
                var matches = _audioMixer.FindMatchingGroups("Music");
                if (matches != null && matches.Length > 0) _musicGroup = matches[0];
            }
            if (_sfxGroup == null)
            {
                var matches = _audioMixer.FindMatchingGroups("SFX");
                if (matches != null && matches.Length > 0) _sfxGroup = matches[0];
            }
        }
    }

    private void EnsureUIAudioSource()
    {
        if (_uiAudioSource == null)
        {
            _uiAudioSource = GetComponent<AudioSource>();
            if (_uiAudioSource == null)
            {
                _uiAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        _uiAudioSource.playOnAwake = false;
        _uiAudioSource.loop = false;
        _uiAudioSource.spatialBlend = 0f;
        _uiAudioSource.ignoreListenerPause = true; // Essential: allows UI audio while paused

        if (_sfxGroup != null)
        {
            _uiAudioSource.outputAudioMixerGroup = _sfxGroup;
        }

        if (_sliderTickClip == null)
        {
#if UNITY_EDITOR
            _sliderTickClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Button Click.mp3");
#endif
            if (_sliderTickClip == null)
            {
                _sliderTickClip = CreateTickProcedural();
            }
        }
    }

    private static AudioClip CreateTickProcedural()
    {
        int sr = 44100;
        int n = Mathf.Max(1, (int)(sr * 0.03f));
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            float env = 1f - (float)i / n;
            data[i] = Mathf.Sin(2f * Mathf.PI * 1400f * t) * env * 0.35f;
        }
        AudioClip clip = AudioClip.Create("UIRateTick", n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }
    #endregion
}
