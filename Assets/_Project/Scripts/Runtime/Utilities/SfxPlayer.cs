using UnityEngine;

// Put this on any GameObject (it adds its own AudioSource).
// Generates simple tones at runtime so you need zero audio assets.
//
// Every event below has an Inspector-assignable AudioClip placeholder slot
// (grouped under "Sound Placeholders" below) -- if you assign a clip it's
// played as-is, otherwise a distinct procedural fallback tone plays instead,
// so the game is never silent even before real audio is dropped in.
[RequireComponent(typeof(AudioSource))]
public class SfxPlayer : MonoBehaviour
{
    public static SfxPlayer Instance { get; private set; }

    [Header("Sound Placeholders -- assign your own clips here")]
    [Tooltip("Plays the moment the player presses a key that does NOT match the required next letter (locked-on miss, or no enemy at all matches the key).")]
    public AudioClip wrongKeySound;
    [Tooltip("Plays when an enemy is actually defeated (any kill path -- typed to death, Overload screen clear, ally kill, etc).")]
    public AudioClip enemyDestroyedSound;
    [Tooltip("Plays once at the very start of a run (New Game or Continue), right as the first wave is about to begin.")]
    public AudioClip gameStartSound;
    [Tooltip("Plays once when the Main Menu scene loads.")]
    public AudioClip mainMenuSound;
    [Tooltip("Plays on every UI button press project-wide (see GlobalButtonSfx / ButtonClickSfx).")]
    public AudioClip buttonClickSound;
    [Tooltip("Plays when the run ends (fortress health hits 0), alongside the Game Over panel appearing.")]
    public AudioClip gameOverSound;

    private AudioSource src;
    private AudioClip typeClip, killClip, hitClip;
    private AudioClip wrongKeyFallback, gameStartFallback, mainMenuFallback, buttonClickFallback, gameOverFallback;

    void Awake()
    {
        Instance = this;
        src = GetComponent<AudioSource>();
        src.playOnAwake = false;

        typeClip = MakeTone(880f, 0.06f, 0.20f);  // high blip per letter
        killClip = MakeTone(330f, 0.18f, 0.30f);  // mid thunk on kill
        hitClip = MakeTone(120f, 0.28f, 0.40f);   // low boom when fortress is hit

        // Distinct procedural fallbacks -- each a different pitch/character so
        // they're all tellable apart from one another and from the 3 above,
        // even with zero real audio assets assigned.
        wrongKeyFallback = MakeBuzz(180f, 0.14f, 0.28f);       // harsh low buzz -- error
        gameStartFallback = MakeSweep(440f, 880f, 0.35f, 0.30f); // rising stinger
        mainMenuFallback = MakeSweep(660f, 990f, 0.5f, 0.22f);   // soft rising chime
        buttonClickFallback = MakeTone(1200f, 0.04f, 0.18f);     // tiny high tick
        gameOverFallback = MakeSweep(500f, 150f, 0.6f, 0.35f);   // falling dirge
    }

    public static void PlayType() { if (Instance && GameSettings.SfxEnabled) Instance.src.PlayOneShot(Instance.typeClip); }

    // Kept for backward compatibility -- Enemy.cs's existing kill-path call
    // site is untouched. Internally this is now the same event/clip as
    // PlayEnemyDestroyed() so assigning enemyDestroyedSound covers both names.
    public static void PlayKill() { PlayEnemyDestroyed(); }

    public static void PlayHit() { if (Instance && GameSettings.SfxEnabled) Instance.src.PlayOneShot(Instance.hitClip); }

    public static void PlayWrongKey()
    {
        if (!Instance || !GameSettings.SfxEnabled) return;
        Instance.src.PlayOneShot(Instance.wrongKeySound != null ? Instance.wrongKeySound : Instance.wrongKeyFallback);
    }

    public static void PlayEnemyDestroyed()
    {
        if (!Instance || !GameSettings.SfxEnabled) return;
        Instance.src.PlayOneShot(Instance.enemyDestroyedSound != null ? Instance.enemyDestroyedSound : Instance.killClip);
    }

    public static void PlayGameStart()
    {
        if (!Instance || !GameSettings.SfxEnabled) return;
        Instance.src.PlayOneShot(Instance.gameStartSound != null ? Instance.gameStartSound : Instance.gameStartFallback);
    }

    public static void PlayMainMenu()
    {
        if (!Instance || !GameSettings.SfxEnabled) return;
        Instance.src.PlayOneShot(Instance.mainMenuSound != null ? Instance.mainMenuSound : Instance.mainMenuFallback);
    }

    public static void PlayButtonClick()
    {
        if (!Instance || !GameSettings.SfxEnabled) return;
        Instance.src.PlayOneShot(Instance.buttonClickSound != null ? Instance.buttonClickSound : Instance.buttonClickFallback);
    }

    public static void PlayGameOver()
    {
        if (!Instance || !GameSettings.SfxEnabled) return;
        Instance.src.PlayOneShot(Instance.gameOverSound != null ? Instance.gameOverSound : Instance.gameOverFallback);
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

    // Harsher, buzzier tone (square-ish via a folded sine) for the wrong-key error.
    static AudioClip MakeBuzz(float freq, float duration, float volume)
    {
        int sr = 44100;
        int n = Mathf.Max(1, (int)(sr * duration));
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / sr;
            float env = 1f - (float)i / n;
            float raw = Mathf.Sin(2f * Mathf.PI * freq * t);
            float square = Mathf.Sign(raw); // hard edges -> buzzy, distinct from the pure tones
            data[i] = square * env * volume;
        }
        AudioClip clip = AudioClip.Create("buzz", n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Frequency sweep (rises or falls from startFreq to endFreq) for stingers.
    static AudioClip MakeSweep(float startFreq, float endFreq, float duration, float volume)
    {
        int sr = 44100;
        int n = Mathf.Max(1, (int)(sr * duration));
        float[] data = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float frac = (float)i / n;
            float env = 1f - frac; // fade out
            float freq = Mathf.Lerp(startFreq, endFreq, frac);
            phase += 2f * Mathf.PI * freq / sr;
            data[i] = Mathf.Sin(phase) * env * volume;
        }
        AudioClip clip = AudioClip.Create("sweep", n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }
}
