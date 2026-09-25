using System;
using UnityEngine;

// The shield's state and rules. Singleton so the bar, bubble, and button all
// talk to it. No stacking: buying while active does nothing; once broken it can
// be bought again.
public class ShieldManager : MonoBehaviour
{
    public static ShieldManager Instance { get; private set; }

    [Header("Shield")]
    public int shieldMax = 40;      // how much damage it absorbs
    public int cost = 30;           // coins to raise it

    [Header("Shield Duration")]
    [Tooltip("How long the shield powerup lasts in seconds.")]
    public float defaultDuration = 10f;
    public float CurrentDuration { get; private set; }
    public float MaxDuration { get; private set; } = 10f;

    public int Current { get; private set; }
    public bool IsActive => CurrentDuration > 0f || Current > 0;

    [Tooltip("Optional: the visual shield, so hits ripple at the contact point.")]
    public ShieldController controller;

    [Header("Upgrade: Shield (optional)")]
    [Tooltip("If assigned, each level adds this much capacity to Shield Max. Boss (6) also auto-refills the shield once per wave, the first time it breaks.")]
    public UpgradeDefinition shieldUpgrade;

    private bool bossRefillUsedThisWave;

    // UI hooks
    public event Action<int, int> OnShieldChanged; // (current, max)
    public event Action<float, float> OnDurationChanged; // (remaining, max)
    public event Action OnShieldRaised;
    public event Action OnShieldBroken;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Restore the raw saved amount, unclamped against EffectiveShieldMax()
        // here — that reads UpgradeManager.Instance.LevelOf(), and Awake order
        // between ShieldManager and UpgradeManager isn't guaranteed, so
        // clamping now could wrongly cap a legitimately upgrade-boosted value
        // before the upgrade levels have been restored. The saved value was
        // already valid when captured, so it's safe to trust as-is.
        if (SaveManager.IsContinuing && SaveManager.HasSave())
        {
            RunSaveData save = SaveManager.LoadRun();
            Current = Mathf.Max(0, save.abilityShield);
        }
    }

    void Start()
    {
        OnShieldChanged?.Invoke(Current, EffectiveShieldMax());
        if (Current > 0)
        {
            if (controller != null)
            {
                controller.gameObject.SetActive(true);
                controller.RaiseShield();
            }
        }
    }

    void Update()
    {
        if (CurrentDuration > 0f)
        {
            CurrentDuration -= Time.deltaTime;
            OnDurationChanged?.Invoke(CurrentDuration, MaxDuration);
            if (CurrentDuration <= 0f)
            {
                CurrentDuration = 0f;
                DeactivateShield();
            }
        }
    }

    int UpgradeLevel() => (UpgradeManager.Instance != null && shieldUpgrade != null) ? UpgradeManager.Instance.LevelOf(shieldUpgrade) : 0;

    int EffectiveShieldMax()
    {
        int level = UpgradeLevel();
        if (level <= 0 || shieldUpgrade == null) return shieldMax;
        return shieldMax + Mathf.RoundToInt(shieldUpgrade.ValueForLevel(level));
    }

    // Called by WaveManager at the start of each wave.
    public void NotifyWaveStart()
    {
        bossRefillUsedThisWave = false;
    }

    // Activates the shield with a specific or default duration
    public void ActivateShield(float duration = -1f)
    {
        float dur = duration > 0f ? duration : defaultDuration;
        CurrentDuration = dur;
        MaxDuration = dur;
        Current = EffectiveShieldMax();

        if (controller != null)
        {
            controller.gameObject.SetActive(true);
            controller.RaiseShield();
        }

        OnShieldChanged?.Invoke(Current, EffectiveShieldMax());
        OnDurationChanged?.Invoke(CurrentDuration, MaxDuration);
        OnShieldRaised?.Invoke();
    }

    // Hooked to the buy button
    public void TryRaiseShield()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.IsGameOver) return;
        if (IsActive) return;                 // no stacking while it's up
        if (!gm.SpendCoins(cost)) return;     // can't afford

        ActivateShield(defaultDuration);
    }

    public void DeactivateShield()
    {
        Current = 0;
        CurrentDuration = 0f;
        OnShieldBroken?.Invoke();
        if (controller != null) controller.SinkShield();
        OnShieldChanged?.Invoke(0, EffectiveShieldMax());
        OnDurationChanged?.Invoke(0f, MaxDuration);
    }

    // Returns the leftover damage that the shield could NOT absorb.
    // hitPos lets the visual shield ripple at the exact contact point.
    public int AbsorbDamage(int amount, Vector3 hitPos)
    {
        if (!IsActive) return amount;

        if (controller != null) controller.HitAt(hitPos);

        // While duration is active, the shield completely blocks incoming damage!
        if (CurrentDuration > 0f)
        {
            return 0;
        }

        int absorbed = Mathf.Min(Current, amount);
        Current -= absorbed;
        int leftover = amount - absorbed;

        OnShieldChanged?.Invoke(Current, EffectiveShieldMax());

        if (Current <= 0)
        {
            DeactivateShield();

            // Boss (6): the first time it breaks each wave, it auto-refills for free.
            if (!bossRefillUsedThisWave && UpgradeLevel() >= UpgradeDefinition.BossLevel)
            {
                bossRefillUsedThisWave = true;
                ActivateShield(defaultDuration);
            }
        }

        return leftover;
    }

    // Overload without a position (falls back to shield centre).
    public int AbsorbDamage(int amount)
    {
        return AbsorbDamage(amount, transform.position);
    }
}