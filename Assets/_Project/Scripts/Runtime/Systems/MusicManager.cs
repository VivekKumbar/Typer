using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Persistent background-music player. ONE looping AudioSource, DontDestroyOnLoad,
// so a track never restarts just because a scene loaded.
//
//   Main Menu  -> "The Tavern"      (always restarts from 0:00 whenever the player returns)
//   Gameplay   -> "Land of Knights" (GameScene AND FTUEScene count as gameplay)
//
// The menu-vs-gameplay switch is DELIBERATE: existing transition points call
// PlayMenuMusic / PlayGameplayMusic directly (MainMenu New Game / Continue / FTUE
// replay, RestartButton.GoToMenu for Game Over + Pause -> Main Menu, FTUEManager ->
// real game, LevelCard direct launch). A sceneLoaded listener is kept ONLY as a
// safety net for any path nobody hooked (and for opening a scene directly in the editor).
//
// Transitions on the single source are "fade out, swap clip, fade in" (Fade Duration
// each way). Respects GameSettings.MusicEnabled: OFF = nothing plays / stops at once;
// toggling ON starts the track for the CURRENT mode immediately.
//
// Put one instance (the MusicManager prefab) in every scene that can be a game's
// entry point -- extra copies destroy themselves in Awake, same as AdManager.
[RequireComponent(typeof(AudioSource))]
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    public enum MusicMode { None, Menu, Gameplay }

    [Header("Music clips -- single or default fallback")]
    [Tooltip("Plays on the Main Menu if no playlist is assigned. Expected: \"The Tavern\".")]
    public AudioClip mainMenuMusic;
    [Tooltip("Plays for the whole run if no playlist is assigned. Expected: \"Land of Knights\".")]
    public AudioClip gameplayMusic;

    [Header("Music playlists (random looping)")]
    [Tooltip("Playlist of audio clips for the Main Menu. When one finishes, another random one plays.")]
    public AudioClip[] mainMenuPlaylist;
    [Tooltip("Playlist of audio clips for Gameplay. When one finishes, another random one plays.")]
    public AudioClip[] gameplayPlaylist;

    [Header("Playback")]
    [Tooltip("Music volume once faded in (0-1). Independent of the SFX volume.")]
    [Range(0f, 1f)] public float musicVolume = 0.6f;
    [Tooltip("Seconds to fade OUT the old track, and again to fade IN the new one. 0 = hard cut.")]
    [Range(0f, 3f)] public float fadeDuration = 0.6f;

    [Header("Scene safety net (only used when no transition call site set the mode)")]
    [Tooltip("Loading any of these scenes switches to menu music (restarted from 0:00) if it isn't already the current mode.")]
    public string[] menuSceneNames = { "MainMenu" };
    [Tooltip("Loading any of these scenes switches to gameplay music if it isn't already the current mode.")]
    public string[] gameplaySceneNames = { "GameScene", "FTUEScene" };

    // What the game is currently "in", regardless of whether music is enabled --
    // so toggling Music ON later knows which track to start.
    public MusicMode CurrentMode { get; private set; }

    private AudioSource src;
    private Coroutine routine;
    private AudioClip targetClip;
    private bool initialized;
    private int lastMenuIndex = -1;
    private int lastGameplayIndex = -1;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;          // stop Start() from running on the doomed duplicate
            Destroy(gameObject);
            return;
        }

        Instance = this;
        initialized = true;
        DontDestroyOnLoad(gameObject);

        src = GetComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = 0f;
        src.volume = 0f;
    }

    void Start()
    {
        GameSettings.OnMusicChanged += HandleMusicToggled;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SyncToScene(SceneManager.GetActiveScene()); // the scene this manager first appeared in
    }

    void Update()
    {
        if (CurrentMode == MusicMode.None || !GameSettings.MusicEnabled || routine != null) return;
        if (src == null || AudioListener.pause) return;

        // When a track finishes naturally, pick and play the next random loop
        if (!src.isPlaying && targetClip != null)
        {
            PlayNextTrack();
        }
    }

    void PlayNextTrack()
    {
        AudioClip next = PickNextRandomClip(CurrentMode);
        if (next == null) return;

        targetClip = next;
        src.clip = next;
        src.time = 0f;
        src.volume = musicVolume;
        src.Play();
    }

    AudioClip PickNextRandomClip(MusicMode mode)
    {
        AudioClip[] list = (mode == MusicMode.Menu) ? mainMenuPlaylist : gameplayPlaylist;
        if (list != null && list.Length > 0)
        {
            if (list.Length == 1) return list[0];
            int last = (mode == MusicMode.Menu) ? lastMenuIndex : lastGameplayIndex;
            int next = Random.Range(0, list.Length);
            if (next == last)
            {
                next = (next + 1 + Random.Range(0, list.Length - 1)) % list.Length;
            }
            if (mode == MusicMode.Menu) lastMenuIndex = next;
            else lastGameplayIndex = next;
            return list[next];
        }

        // Fallback to legacy single clips
        return (mode == MusicMode.Menu) ? mainMenuMusic : gameplayMusic;
    }

    void OnDestroy()
    {
        if (!initialized) return;
        GameSettings.OnMusicChanged -= HandleMusicToggled;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (Instance == this) Instance = null;
    }

    // ---- public API (the deliberate transition call sites) ----------------------------

    // Restart From Start = true forces a fresh restart from 0:00 even if the menu track is
    // already playing (used when RETURNING to the menu). False = "make sure it's playing".
    public static void PlayMenuMusic(bool restartFromStart = false)
    {
        if (Instance != null) Instance.SetMode(MusicMode.Menu, restartFromStart);
    }

    public static void PlayGameplayMusic(bool restartFromStart = false)
    {
        if (Instance != null) Instance.SetMode(MusicMode.Gameplay, restartFromStart);
    }

    // ---- internals ---------------------------------------------------------------------

    void SetMode(MusicMode mode, bool restart)
    {
        bool modeChanged = (CurrentMode != mode);
        CurrentMode = mode;
        if (!GameSettings.MusicEnabled) return; // remembered; toggling Music ON later picks it up

        // Same mode already playing and nobody asked for a restart -> leave it alone.
        if (!modeChanged && !restart && src.isPlaying) return;

        AudioClip clip = PickNextRandomClip(mode);
        StartTransition(clip);
    }

    void HandleMusicToggled(bool on)
    {
        if (!on)
        {
            StopNow();
            return;
        }
        // Toggled ON mid-scene: start the CURRENT mode's track right away (fresh from the start).
        if (CurrentMode != MusicMode.None) StartTransition(PickNextRandomClip(CurrentMode));
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode) { SyncToScene(scene); }

    void SyncToScene(Scene scene)
    {
        if (Contains(menuSceneNames, scene.name))
        {
            if (CurrentMode != MusicMode.Menu) SetMode(MusicMode.Menu, true);       // any return to the menu = restart from 0:00
        }
        else if (Contains(gameplaySceneNames, scene.name))
        {
            if (CurrentMode != MusicMode.Gameplay) SetMode(MusicMode.Gameplay, false);
        }
    }

    static bool Contains(string[] names, string sceneName)
    {
        if (names == null) return false;
        foreach (string n in names) if (n == sceneName) return true;
        return false;
    }

    void StartTransition(AudioClip clip)
    {
        targetClip = clip;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Transition(clip));
    }

    void StopNow()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        targetClip = null;
        src.Stop();
        src.volume = 0f;
    }

    // Single-source "crossfade": fade the audible track out, swap to the new clip from time 0,
    // fade it in. Uses unscaled time so it still runs while the game is frozen (Game Over / pause).
    IEnumerator Transition(AudioClip clip)
    {
        if (src.volume > 0.001f && fadeDuration > 0f && musicVolume > 0f)
        {
            float from = src.volume;
            float dur = fadeDuration * Mathf.Clamp01(from / musicVolume);
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                src.volume = Mathf.Lerp(from, 0f, t / dur);
                yield return null;
            }
        }

        src.Stop();
        src.volume = 0f;

        if (clip != null)
        {
            src.clip = clip;
            src.time = 0f;      // ALWAYS from the beginning -- never resumes mid-track
            src.Play();

            if (fadeDuration > 0f)
            {
                for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
                {
                    src.volume = Mathf.Lerp(0f, musicVolume, t / fadeDuration);
                    yield return null;
                }
            }
            src.volume = musicVolume;
        }
        else
        {
            src.clip = null;
        }

        routine = null;
    }
}
