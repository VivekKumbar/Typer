using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_WEBGL
using Playgama;
using Playgama.Modules.Platform;
#endif

/// <summary>
/// Central manager coordinating Playgama Bridge SDK lifecycle, host audio/pause state,
/// and platform progress messages across the game.
/// </summary>
[DisallowMultipleComponent]
public class BridgeManager : MonoBehaviour
{
    public static BridgeManager Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("If enabled, automatically sends PlatformMessage.GameReady when the first interactive screen is initialized.")]
    public bool autoSendGameReady = true;

    private static bool s_gameReadySent;
    private static bool s_isHostPaused;
    private static bool s_isHostAudioMuted;
#pragma warning disable CS0414
    private static float s_savedTimeScale = 1f;
#pragma warning restore CS0414

#pragma warning disable CS0067
    public static event Action<bool> OnHostPauseStateChanged;
    public static event Action<bool> OnHostAudioStateChanged;
#pragma warning restore CS0067

    public static bool IsHostPaused => s_isHostPaused;
    public static bool IsHostAudioMuted => s_isHostAudioMuted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance != null) return;
        var go = new GameObject("[BridgeManager]");
        go.AddComponent<BridgeManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

#if UNITY_WEBGL
        InitializeBridgeHooks();
#endif
    }

#if UNITY_WEBGL
    private void InitializeBridgeHooks()
    {
        try
        {
            if (Bridge.platform != null)
            {
                // Audio state hook
                Bridge.platform.audioStateChanged += HandleAudioStateChanged;
                if (!Bridge.platform.isAudioEnabled)
                {
                    HandleAudioStateChanged(false);
                }

                // Pause state hook
                Bridge.platform.pauseStateChanged += HandlePauseStateChanged;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BridgeManager] Could not bind Bridge hooks: {ex.Message}");
        }
    }

    private void HandleAudioStateChanged(bool isAudioEnabled)
    {
        s_isHostAudioMuted = !isAudioEnabled;
        AudioListener.pause = s_isHostAudioMuted;
        AudioListener.volume = isAudioEnabled ? 1f : 0f;
        OnHostAudioStateChanged?.Invoke(isAudioEnabled);
    }

    private void HandlePauseStateChanged(bool isPaused)
    {
        s_isHostPaused = isPaused;
        if (isPaused)
        {
            if (Time.timeScale > 0f)
            {
                s_savedTimeScale = Time.timeScale;
            }
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }
        else
        {
            Time.timeScale = s_savedTimeScale > 0f ? s_savedTimeScale : 1f;
            if (!s_isHostAudioMuted)
            {
                AudioListener.pause = false;
            }
        }
        OnHostPauseStateChanged?.Invoke(isPaused);
    }
#endif

    /// <summary>
    /// Sends game_ready message to the host platform once the game is loaded and ready for interaction.
    /// </summary>
    public static void SendGameReady()
    {
        if (s_gameReadySent) return;
        s_gameReadySent = true;

#if UNITY_WEBGL
        try
        {
            if (Bridge.platform != null)
            {
                Bridge.platform.SendMessage(PlatformMessage.GameReady);
                Debug.Log("[BridgeManager] Sent GameReady signal to platform.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BridgeManager] Failed to send GameReady: {ex.Message}");
        }
#endif
    }

    /// <summary>
    /// Sends level_started signal for the given wave.
    /// </summary>
    public static void SendLevelStarted(int wave)
    {
#if UNITY_WEBGL
        try
        {
            if (Bridge.platform != null)
            {
                var options = new Dictionary<string, object>
                {
                    { "level", wave.ToString() }
                };
                Bridge.platform.SendMessage(PlatformMessage.LevelStarted, options);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BridgeManager] Failed to send LevelStarted: {ex.Message}");
        }
#endif
    }

    /// <summary>
    /// Sends level_completed signal for the given wave.
    /// </summary>
    public static void SendLevelCompleted(int wave)
    {
#if UNITY_WEBGL
        try
        {
            if (Bridge.platform != null)
            {
                var options = new Dictionary<string, object>
                {
                    { "level", wave.ToString() }
                };
                Bridge.platform.SendMessage(PlatformMessage.LevelCompleted, options);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BridgeManager] Failed to send LevelCompleted: {ex.Message}");
        }
#endif
    }

    /// <summary>
    /// Sends level_failed signal for the given wave.
    /// </summary>
    public static void SendLevelFailed(int wave)
    {
#if UNITY_WEBGL
        try
        {
            if (Bridge.platform != null)
            {
                var options = new Dictionary<string, object>
                {
                    { "level", wave.ToString() }
                };
                Bridge.platform.SendMessage(PlatformMessage.LevelFailed, options);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BridgeManager] Failed to send LevelFailed: {ex.Message}");
        }
#endif
    }

    /// <summary>
    /// Sends level_paused signal.
    /// </summary>
    public static void SendLevelPaused()
    {
#if UNITY_WEBGL
        try
        {
            if (Bridge.platform != null)
            {
                Bridge.platform.SendMessage(PlatformMessage.LevelPaused);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BridgeManager] Failed to send LevelPaused: {ex.Message}");
        }
#endif
    }

    /// <summary>
    /// Sends level_resumed signal.
    /// </summary>
    public static void SendLevelResumed()
    {
#if UNITY_WEBGL
        try
        {
            if (Bridge.platform != null)
            {
                Bridge.platform.SendMessage(PlatformMessage.LevelResumed);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BridgeManager] Failed to send LevelResumed: {ex.Message}");
        }
#endif
    }

    /// <summary>
    /// Returns the host platform language (ISO 639-1, e.g. "en", "ru").
    /// </summary>
    public static string GetPlatformLanguage()
    {
#if UNITY_WEBGL
        try
        {
            if (Bridge.platform != null && !string.IsNullOrEmpty(Bridge.platform.language))
            {
                return Bridge.platform.language;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BridgeManager] Failed to read platform language: {ex.Message}");
        }
#endif
        return "en";
    }

    /// <summary>
    /// Returns the platform ID (e.g. "crazy_games", "yandex", "poki", "playgama", "mock").
    /// </summary>
    public static string GetPlatformId()
    {
#if UNITY_WEBGL
        try
        {
            if (Bridge.platform != null && !string.IsNullOrEmpty(Bridge.platform.id))
            {
                return Bridge.platform.id;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BridgeManager] Failed to read platform id: {ex.Message}");
        }
#endif
        return "default";
    }
}
