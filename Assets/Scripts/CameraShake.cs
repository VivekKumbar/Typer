using System.Collections;
using UnityEngine;

// Put this on the Main Camera. All values are editable in the Inspector.
// Other scripts call CameraShake.ShakeKill() / ShakeHit(), which use the
// settings below — so you tune the feel here, not in code.
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("Kill shake (small, when an enemy dies)")]
    public float killDuration = 0.12f;
    public float killMagnitude = 0.20f;

    [Header("Fortress-hit shake (big, when an enemy reaches you)")]
    public float hitDuration = 0.30f;
    public float hitMagnitude = 0.45f;

    [Header("Global controls")]
    [Tooltip("Master multiplier over every shake. 0 = none, 1 = normal.")]
    [Range(0f, 3f)] public float intensity = 1f;
    [Tooltip("Uncheck to disable all camera shake.")]
    public bool enableShake = true;

    private Vector3 basePos;
    private Coroutine running;

    void Awake()
    {
        Instance = this;
        basePos = transform.localPosition;
    }

    // Named triggers that pull from the Inspector values
    public static void ShakeKill() { if (Instance) Instance.Trigger(Instance.killDuration, Instance.killMagnitude); }
    public static void ShakeHit() { if (Instance) Instance.Trigger(Instance.hitDuration, Instance.hitMagnitude); }

    // Still available if you want a custom one-off shake from code
    public static void Shake(float duration, float magnitude) { if (Instance) Instance.Trigger(duration, magnitude); }

    void Trigger(float duration, float magnitude)
    {
        if (!enableShake || intensity <= 0f) return;
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Do(duration, magnitude * intensity));
    }

    IEnumerator Do(float duration, float magnitude)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;        // works even when the game is frozen
            float damper = 1f - (t / duration); // taper off smoothly
            Vector2 o = Random.insideUnitCircle * magnitude * damper;
            transform.localPosition = basePos + new Vector3(o.x, o.y, 0f);
            yield return null;
        }
        transform.localPosition = basePos;
        running = null;
    }
}