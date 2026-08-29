using System;
using System.Collections;
using UnityEngine;
using TMPro;

// One floating popup ("+5", "COMBO x3!", "PERFECT!"). Put a Billboard component
// on the same object so it faces the camera from the angled top-down view.
// Pooled by PopupManager — Init() resets it for reuse instead of Instantiate/Destroy
// churn during big waves.
[RequireComponent(typeof(TMP_Text))]
public class FloatingText : MonoBehaviour
{
    [Header("Motion")]
    [Tooltip("Total seconds this popup is visible before it's recycled.")]
    public float lifetime = 0.9f;
    [Tooltip("How far upward (world units) this popup rises over its lifetime.")]
    public float riseDistance = 1.2f;
    [Tooltip("Extra random sideways drift (world units) added over the lifetime, on top of PopupManager's spawn jitter.")]
    [Range(0f, 1f)] public float horizontalDrift = 0.25f;

    [Header("Pop-in")]
    [Tooltip("Seconds for the spawn scale-pop (0 -> overshoot -> settled).")]
    public float popDuration = 0.15f;
    [Tooltip("How far past full scale (1.0) the pop overshoots before settling. 1 = no overshoot.")]
    [Range(1f, 2f)] public float popOvershoot = 1.3f;

    [Header("Fade")]
    [Tooltip("Fraction of lifetime (0-1) elapsed before alpha starts fading to 0.")]
    [Range(0f, 1f)] public float fadeStartFraction = 0.6f;

    private TMP_Text label;
    private Action<FloatingText> onFinished;
    private Coroutine playing;

    void Awake()
    {
        label = GetComponent<TMP_Text>();
    }

    public void Init(string text, Color color, float size, Action<FloatingText> onFinished = null)
    {
        this.onFinished = onFinished;

        label.text = text;
        label.color = color;
        label.fontSize = size;
        label.fontStyle = FontStyles.Bold;
        label.outlineWidth = 0.2f;
        label.outlineColor = Color.black; // reads clearly over any background, light or dark

        transform.localScale = Vector3.zero;

        if (playing != null) StopCoroutine(playing);
        playing = StartCoroutine(Play());
    }

    IEnumerator Play()
    {
        Vector3 basePos = transform.position;
        Vector2 jitter2D = UnityEngine.Random.insideUnitCircle;
        Vector3 driftDir = new Vector3(jitter2D.x, 0f, jitter2D.y) * horizontalDrift;
        Color baseColor = label.color;

        // Pop-in: overshoot past full scale, then settle. Unscaled time so it
        // still animates crisply if it spawns during a hit-stop freeze.
        float t = 0f;
        while (t < popDuration)
        {
            t += Time.unscaledDeltaTime;
            transform.localScale = Vector3.one * PopCurve(Mathf.Clamp01(t / popDuration));
            yield return null;
        }
        transform.localScale = Vector3.one;

        // Rise + drift + fade
        float elapsed = 0f;
        while (elapsed < lifetime)
        {
            elapsed += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(elapsed / lifetime);
            transform.position = basePos + Vector3.up * (riseDistance * p) + driftDir * p;

            if (p >= fadeStartFraction)
            {
                float fadeP = Mathf.InverseLerp(fadeStartFraction, 1f, p);
                Color c = baseColor;
                c.a = Mathf.Lerp(baseColor.a, 0f, fadeP);
                label.color = c;
            }
            yield return null;
        }

        playing = null;
        if (onFinished != null) onFinished(this);
        else Destroy(gameObject);
    }

    float PopCurve(float p)
    {
        const float overshootPoint = 0.65f; // fraction of popDuration spent overshooting vs settling back
        if (p < overshootPoint)
            return Mathf.SmoothStep(0f, popOvershoot, p / overshootPoint);
        return Mathf.SmoothStep(popOvershoot, 1f, (p - overshootPoint) / (1f - overshootPoint));
    }
}
