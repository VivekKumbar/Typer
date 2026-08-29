using UnityEngine;

// Put this on any GameObject (it adds its own AudioSource).
// Generates simple tones at runtime so you need zero audio assets.
[RequireComponent(typeof(AudioSource))]
public class SfxPlayer : MonoBehaviour
{
    public static SfxPlayer Instance { get; private set; }

    private AudioSource src;
    private AudioClip typeClip, killClip, hitClip;

    void Awake()
    {
        Instance = this;
        src = GetComponent<AudioSource>();
        src.playOnAwake = false;
        typeClip = MakeTone(880f, 0.06f, 0.20f); // high blip per letter
        killClip = MakeTone(330f, 0.18f, 0.30f); // mid thunk on kill
        hitClip = MakeTone(120f, 0.28f, 0.40f); // low boom when fortress is hit
    }

    public static void PlayType() { if (Instance && GameSettings.SfxEnabled) Instance.src.PlayOneShot(Instance.typeClip); }
    public static void PlayKill() { if (Instance && GameSettings.SfxEnabled) Instance.src.PlayOneShot(Instance.killClip); }
    public static void PlayHit() { if (Instance && GameSettings.SfxEnabled) Instance.src.PlayOneShot(Instance.hitClip); }

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
}