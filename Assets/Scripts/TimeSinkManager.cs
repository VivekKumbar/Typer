using System;
using UnityEngine;

// TIME SINK ability: charges as the player types correct letters (mirrors
// ComboManager's Overload meter), then slows every enemy for a duration based
// on how close each one is to the tower — nearest enemies slow the most.
// Deliberately does NOT touch Time.timeScale: it drives a per-enemy
// Enemy.SlowMultiplier instead, so UI/cooldowns/animations are unaffected.
public class TimeSinkManager : MonoBehaviour
{
    public static TimeSinkManager Instance { get; private set; }

    [Header("Charge")]
    [Tooltip("Correct letters needed to fill the charge meter.")]
    public float chargeMax = 40f;
    public float Charge { get; private set; }
    public bool IsReady { get; private set; }

    [Header("Effect")]
    public float duration = 6f;
    [Tooltip("Speed multiplier applied to enemies right on top of the tower.")]
    [Range(0.05f, 1f)] public float maxSlow = 0.25f;
    [Tooltip("Distance from the tower where enemies are unaffected.")]
    public float safeDistance = 10f;
    [Tooltip("Distance from the tower where enemies get the full slow.")]
    public float dangerDistance = 3f;

    public bool IsActive { get; private set; }
    public float RemainingFraction { get; private set; } // 1 -> 0 over the duration

    public event Action<float> OnChargeChanged;   // 0..1
    public event Action OnChargeReady;
    public event Action OnActivated;
    public event Action<float> OnDurationChanged; // 1..0 remaining
    public event Action OnEnded;

    float remainingTime;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        if (!IsActive) return;

        remainingTime -= Time.deltaTime;
        RemainingFraction = duration > 0f ? Mathf.Clamp01(remainingTime / duration) : 0f;
        OnDurationChanged?.Invoke(RemainingFraction);

        if (remainingTime <= 0f) { EndEffect(); return; }

        ApplySlow();
    }

    // Hook this to the same "correct letter" path ComboManager.RegisterHit uses.
    // A miss should never call this — the charge is forgiving, like Overload.
    public void AddCharge()
    {
        if (IsActive || IsReady) return;

        Charge += 1f;
        if (Charge >= chargeMax)
        {
            Charge = chargeMax;
            IsReady = true;
            OnChargeReady?.Invoke();
        }
        OnChargeChanged?.Invoke(Charge / chargeMax);
    }

    // Hooked to the Time Sink button.
    public void Activate()
    {
        if (IsActive || !IsReady) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        Charge = 0f;
        IsReady = false;
        OnChargeChanged?.Invoke(0f);

        IsActive = true;
        remainingTime = duration;
        RemainingFraction = 1f;
        OnActivated?.Invoke();
        OnDurationChanged?.Invoke(1f);

        ApplySlow(); // apply immediately so there's no one-frame gap before the next Update
    }

    // Enemies spawned mid-effect join Enemy.Active and get slowed on the very
    // next tick, since this re-applies every frame to whoever is currently active.
    void ApplySlow()
    {
        foreach (Enemy e in Enemy.Active)
        {
            if (e == null || e.IsDefeated) continue;
            float t = Mathf.Clamp01(Mathf.InverseLerp(safeDistance, dangerDistance, e.DistanceToFortress));
            e.SlowMultiplier = Mathf.Lerp(1f, maxSlow, t);
        }
    }

    void EndEffect()
    {
        IsActive = false;
        remainingTime = 0f;
        RemainingFraction = 0f;

        foreach (Enemy e in Enemy.Active)
            if (e != null) e.SlowMultiplier = 1f;

        OnDurationChanged?.Invoke(0f);
        OnEnded?.Invoke();
    }
}
