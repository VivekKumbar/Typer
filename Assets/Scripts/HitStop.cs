using System.Collections;
using UnityEngine;

// Put this on an empty "HitStop" GameObject in the scene. Other scripts call
// HitStop.Stop(duration) on a kill; it freezes Time.timeScale to 0 for that
// long, then eases it back up to 1 over Recover Duration (instead of snapping)
// — using unscaled time throughout so it all still runs while frozen. Pairs
// with CameraShake for the Vampire-Survivors "crunch".
public class HitStop : MonoBehaviour
{
    public static HitStop Instance { get; private set; }

    [Header("Master control")]
    [Tooltip("Uncheck to disable all hit-stop freezes (camera shake and popups are unaffected).")]
    public bool enableHitStop = true;

    [Header("Freeze duration (seconds)")]
    [Tooltip("Freeze length for a normal-size kill (word length <= Enemy.bigWordLength).")]
    [Range(0f, 0.3f)] public float smallKillFreeze = 0.04f;
    [Tooltip("Freeze length for a big kill (word length > Enemy.bigWordLength). Keep longer than Small Kill Freeze so the extra punch reads.")]
    [Range(0f, 0.3f)] public float bigKillFreeze = 0.09f;

    [Header("Recovery (freeze -> full speed transition)")]
    [Tooltip("Seconds (unscaled) to ease timeScale back up to 1 after the freeze ends. 0 = instant snap.")]
    [Range(0f, 0.5f)] public float recoverDuration = 0.08f;
    [Tooltip("Shapes the ramp from frozen (0) to full speed (1) over Recover Duration. X = normalized time, Y = timeScale. Drag the curve handles to tune the feel.")]
    public AnimationCurve recoverCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Extra camera shake for big kills")]
    [Tooltip("Small kills reuse CameraShake's own Kill Duration/Magnitude via ShakeKill(). Big kills add this on top for a stronger hit.")]
    [Range(0f, 0.6f)] public float bigShakeDuration = 0.18f;
    [Tooltip("Camera shake magnitude for a big kill.")]
    [Range(0f, 1f)] public float bigShakeMagnitude = 0.40f;

    enum Phase { Idle, Frozen, Recovering }
    private Phase phase = Phase.Idle;
    private Coroutine running;
    private float remaining;

    void Awake()
    {
        Instance = this;
    }

    public static void Stop(float durationSeconds)
    {
        if (Instance) Instance.Trigger(durationSeconds);
    }

    void Trigger(float durationSeconds)
    {
        if (!enableHitStop || durationSeconds <= 0f) return;

        if (phase == Phase.Frozen)
        {
            // Already frozen: take the longer of what's left and the new
            // request. Don't stack/extend beyond that.
            remaining = Mathf.Max(remaining, durationSeconds);
            return;
        }

        if (phase == Phase.Idle)
        {
            // Don't override a real pause / game over — only manage the
            // freeze we start ourselves.
            if (IsRealFreezeActive() || Time.timeScale == 0f) return;
        }

        // Fresh hard freeze. If we were mid-recovery, a new kill re-snaps it
        // for a punchy re-hit rather than blending two ramps together.
        if (running != null) StopCoroutine(running);
        remaining = durationSeconds;
        Time.timeScale = 0f;
        phase = Phase.Frozen;
        running = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        while (remaining > 0f)
        {
            // A real pause / game over kicked in mid-freeze: bail without
            // touching timeScale, it's not ours to restore anymore.
            if (IsRealFreezeActive())
            {
                phase = Phase.Idle;
                running = null;
                yield break;
            }

            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (recoverDuration <= 0f)
        {
            Time.timeScale = 1f;
            phase = Phase.Idle;
            running = null;
            yield break;
        }

        phase = Phase.Recovering;
        float t = 0f;
        while (t < recoverDuration)
        {
            if (IsRealFreezeActive())
            {
                phase = Phase.Idle;
                running = null;
                yield break;
            }

            t += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Clamp01(recoverCurve.Evaluate(Mathf.Clamp01(t / recoverDuration)));
            yield return null;
        }

        Time.timeScale = 1f;
        phase = Phase.Idle;
        running = null;
    }

    static bool IsRealFreezeActive()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return true;
        if (PauseMenu.Instance != null && PauseMenu.Instance.IsPaused) return true;
        return false;
    }
}
