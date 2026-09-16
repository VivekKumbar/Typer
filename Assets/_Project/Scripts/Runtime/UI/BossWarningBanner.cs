using System.Collections;
using UnityEngine;
using TMPro;

// Shows a big centered warning ("PREPARE YOURSELF") right before a boss wave's
// big enemy spawns. Same shape as WaveBanner (a TMP text on the Canvas,
// shown/hidden via a coroutine), but fades in/out and returns an IEnumerator
// so WaveManager can yield on it — the boss doesn't spawn until this beat
// (fade in, hold, fade out, extra delay) has fully finished.
// Put this on an ALWAYS-ACTIVE object (like the Canvas) and assign a TMP text
// child that it will show/hide.
public class BossWarningBanner : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("The TMP text this banner shows. Hidden at Awake.")]
    public TMP_Text text;

    [Header("Message")]
    [Tooltip("What the banner says.")]
    public string warningText = "PREPARE YOURSELF";

    [Header("Look (optional — make it distinct from the normal wave banner)")]
    [Tooltip("Font size for this warning. 0 = don't override whatever size is already on the text.")]
    public float fontSize = 90f;
    [Tooltip("Color for this warning (alpha is controlled by the fade, not this).")]
    public Color color = new Color(0.9f, 0.1f, 0.1f);

    [Header("Timing")]
    [Tooltip("Seconds to fade in.")]
    [Range(0f, 3f)] public float fadeInTime = 0.3f;
    [Tooltip("Seconds the text stays fully visible between the fades.")]
    public float displayDuration = 1.2f;
    [Tooltip("Seconds to fade out.")]
    [Range(0f, 3f)] public float fadeOutTime = 0.4f;
    [Tooltip("Extra pause AFTER the warning has fully faded out, before the boss actually spawns. 0 = spawn immediately once the text is gone.")]
    public float delayBeforeBossSpawns = 0.3f;

    private Coroutine running;

    void Awake()
    {
        if (text != null)
        {
            text.gameObject.SetActive(false);
            SetAlpha(0f);
        }
    }

    // Fire-and-forget version, in case something just wants to show it without
    // waiting (kept generic per the brief — e.g. for adding shake/particles
    // later without needing the caller to change how it invokes this).
    public void Show()
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(ShowAndWait());
    }

    // WaveManager calls: yield return bossWarningBanner.ShowAndWait();
    // Only returns once the whole beat is done, which is what lets the boss
    // spawn wait on it directly instead of guessing a fixed delay.
    public IEnumerator ShowAndWait()
    {
        if (text == null) yield break;

        text.text = warningText;
        if (fontSize > 0f) text.fontSize = fontSize;
        text.gameObject.SetActive(true);
        SetAlpha(0f);

        float t = 0f;
        while (t < fadeInTime)
        {
            t += Time.deltaTime;
            SetAlpha(fadeInTime > 0f ? Mathf.Clamp01(t / fadeInTime) : 1f);
            yield return null;
        }
        SetAlpha(1f);

        yield return new WaitForSeconds(displayDuration);

        t = 0f;
        while (t < fadeOutTime)
        {
            t += Time.deltaTime;
            SetAlpha(fadeOutTime > 0f ? 1f - Mathf.Clamp01(t / fadeOutTime) : 0f);
            yield return null;
        }
        SetAlpha(0f);
        text.gameObject.SetActive(false);

        if (delayBeforeBossSpawns > 0f)
            yield return new WaitForSeconds(delayBeforeBossSpawns);

        running = null;
    }

    void SetAlpha(float a)
    {
        if (text == null) return;
        Color c = color;
        c.a = a;
        text.color = c;
    }
}
