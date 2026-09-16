using System.Collections;
using UnityEngine;

// Put this on the coin counter's Text object (or its parent). Call Bounce()
// each time coins land — scales up with overshoot, then eases back to rest.
// Uses unscaledDeltaTime so it still animates if the game is paused/slowed.
public class CounterBounce : MonoBehaviour
{
    [Header("Master control")]
    [Tooltip("Uncheck to disable the bounce entirely.")]
    public bool enableBounce = true;

    [Header("Bounce")]
    [Tooltip("Peak scale multiplier at the top of the bounce. 1.25 = 25% bigger.")]
    [Range(1f, 2f)] public float bounceScale = 1.25f;
    [Tooltip("Seconds for one full bounce (up and settled back to rest).")]
    [Range(0.05f, 1f)] public float bounceDuration = 0.25f;
    [Tooltip("Shapes the bounce over time (X: 0-1 through the bounce, Y: 0=rest scale, 1=peak scale). Default overshoots then settles, like a spring. Retriggering mid-bounce restarts cleanly from rest.")]
    public AnimationCurve bounceCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.35f, 1f),
        new Keyframe(0.6f, 0.85f),
        new Keyframe(1f, 0f));

    private Vector3 baseScale;
    private Coroutine running;

    void Awake()
    {
        baseScale = transform.localScale;
    }

    public void Bounce()
    {
        if (!enableBounce) return;
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(BounceRoutine());
    }

    IEnumerator BounceRoutine()
    {
        float t = 0f;
        while (t < bounceDuration)
        {
            t += Time.unscaledDeltaTime;
            float s = bounceCurve.Evaluate(Mathf.Clamp01(t / bounceDuration));
            transform.localScale = Vector3.LerpUnclamped(baseScale, baseScale * bounceScale, s);
            yield return null;
        }
        transform.localScale = baseScale;
        running = null;
    }
}
