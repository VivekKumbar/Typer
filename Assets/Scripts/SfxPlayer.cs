using System;
using UnityEngine;

// Put this on any GameObject (it adds its own AudioSource).
// Generates simple tones at runtime so you need zero audio assets. Per-scene
// singleton like GameManager/ComboManager/etc -- add one to any scene that
// needs SFX (GameScene and MainMenu both do; MainMenu needs its own copy for
// the menu stinger + button clicks there).
[RequireComponent(typeof(AudioSource))]
public class SfxPlayer : MonoBehaviour
{
    public static SfxPlayer Instance { get; private set; }

    [Header("Sound Placeholders — assign your own clips here")]
    [Tooltip("Plays the instant the player presses a WRONG key -- either a typo while locked onto a target, or a key that matches no enemy on screen at all. Leave empty for a procedural buzz/error tone.")]
    public AudioClip wrongKeySound;
    [Tooltip("Plays when an enemy is actually defeated -- ANY kill source (bullets, PreType/Head-Start finishing a word, Overload's screen clear, etc all route through Enemy.Die(rewardCoins:true)). Leave empty for a procedural tone.")]
    public AudioClip enemyDestroyedSound;
    [Tooltip("Plays once at the very start of a run -- New Game or Continue, right as the first wave is about to begin. Leave empty for a procedural rising stinger.")]
    public AudioClip gameStartSound;
    [Tooltip("Plays once when the Main Menu scene loads. Leave empty for a procedural chime.")]
    public AudioClip mainMenuSound;
    [Tooltip("Plays on every UI Button press, project-wide -- see ButtonClickSound/GlobalButtonSfx. Leave empty for a procedural click tone.")]
    public AudioClip buttonClickSound;
    [Tooltip("Plays once when the fortress falls (GameManager.IsGameOver becomes true). Leave empty for a procedural falling stinger.")]
    public AudioClip gameOverSound;

    private AudioSource src;
    private AudioClip typeClip, killClip, hitClip;
    private AudioClip wrongKeyFallback, gameStartFallback, mainMenuFallback, buttonClickFallback, gameOverFallback;

    void Awake()
    {
        Instance = this;
        src = GetComponent<AudioSource>();
        src.playOnAwake = false;

        typeClip = MakeTone(880f, 0.06f, 0.20f); // high blip per letter
        killClip = MakeTone(330f, 0.18f, 0.30f); // mid thunk on kill
        hitClip = MakeTone(120f, 0.28f, 0.40f);  // low boom when fortress is hit

        // Distinct pitch/character per new event so every one of them is
        // tellable apart from the others (and from the three above) with zero
        // real audio assets assigned.
        wrongKeyFallback = MakeBuzz(180f, 0.12f, 0.35f);              // harsh square-wave buzz -- unmistakably "error", not musical
        gameStartFallback = MakeSweep(440f, 880f, 0.30f, 0.30f);      // rising sweep -- "let's go"
        mainMenuFallback = MakeTone(660f, 0.25f, 0.25f);              // gentle mid chime
        buttonClickFallback = MakeTone(1200f, 0.03f, 0.15f);          // very short, high, unobtrusive tick
        gameOverFallback = MakeSweep(440f, 110f, 0.5f, 0.35f);        // falling sweep -- "it's over"
    }

    public static void PlayType() { if (Instance && GameSettings.SfxEnabled) Instance.src.PlayOneShot(Instance.typeClip); }
    // Kept for backward compatibility -- every existing call site (Enemy.Die,
    // Overload's screen clear via Enemy.Defeat) stays exactly as-is. Same
    // event as PlayEnemyDestroyed below, just the original name.
    public static void PlayKill() { PlayEnemyDestroyed(); }
    public static void PlayHit() { if (Instance && GameSettings.SfxEnabled) Instance.src.PlayOneShot(Instance.hitClip); }

    public static void PlayWrongKey(AudioClip clipOverride = null) => PlayOneOf(clipOverride, i => i.wrongKeySound, i => i.wrongKeyFallback);
    public static void PlayEnemyDestroyed(AudioClip clipOverride = null) => PlayOneOf(clipOverride, i => i.enemyDestroyedSound, i => i.killClip);
    public static void PlayGameStart(AudioClip clipOverride = null) => PlayOneOf(clipOverride, i => i.gameStartSound, i => i.gameStartFallback);
    public static void PlayMainMenu(AudioClip clipOverride = null) => PlayOneOf(clipOverride, i => i.mainMenuSound, i => i.mainMenuFallback);
    public static void PlayButtonClick(AudioClip clipOverride = null) => PlayOneOf(clipOverride, i => i.buttonClickSound, i => i.buttonClickFallback);
    public static void PlayGameOver(AudioClip clipOverride = null) => PlayOneOf(clipOverride, i => i.gameOverSound, i => i.gameOverFallback);

    // Shared play path for every new event: an explicit override (if a caller
    // passes one) beats the Inspector-assigned placeholder clip, which beats
    // the procedural fallback -- so this is never silent even with nothing
    // assigned, and respects the Settings SFX toggle exactly like the three
    // original methods above.
    static void PlayOneOf(AudioClip clipOverride, Func<SfxPlayer, AudioClip> assigned, Func<SfxPlayer, AudioClip> fallback)
    {
        if (Instance == null || !GameSettings.SfxEnabled) return;
        AudioClip clip = clipOverride != null ? clipOverride : assigned(Instance);
        if (clip == null) clip = fallback(Instance);
        Instance.src.PlayOneShot(clip);
    }

    static AudioClip MakeTone(float freq, float duration, float volume)
    {
        int sr = 44100;
        int n = Mathf.Max(1, (int)(sr * duration));
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            float env = 1f - (float)i / n;                 // fade out (decay)
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * volume;
        }
        AudioClip clip = AudioClip.Create("tone", n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Square wave instead of a pure sine -- reads as a harsh "buzz", clearly
    // distinct in character (not just pitch) from every tone above.
    static AudioClip MakeBuzz(float freq, float duration, float volume)
    {
        int sr = 44100;
        int n = Mathf.Max(1, (int)(sr * duration));
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            float env = 1f - (float)i / n;
            float square = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * freq * t));
            data[i] = square * env * volume;
        }
        AudioClip clip = AudioClip.Create("buzz", n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Linear frequency sweep from startFreq to endFreq -- rising for "start",
    // falling for "game over".
    static AudioClip MakeSweep(float startFreq, float endFreq, float duration, float volume)
    {
        int sr = 44100;
        int n = Mathf.Max(1, (int)(sr * duration));
        float[] data = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float frac = (float)i / n;
            float freq = Mathf.Lerp(startFreq, endFreq, frac);
            phase += 2f * Mathf.PI * freq / sr;
            float env = 1f - frac; // fade out
            data[i] = Mathf.Sin(phase) * env * volume;
        }
        AudioClip clip = AudioClip.Create("sweep", n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }
}
