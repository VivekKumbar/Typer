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

    public int Current { get; private set; }
    public bool IsActive => Current > 0;

    [Tooltip("Optional: the visual shield, so hits ripple at the contact point.")]
    public ShieldController controller;

    // UI hooks
    public event Action<int, int> OnShieldChanged; // (current, max)
    public event Action OnShieldRaised;
    public event Action OnShieldBroken;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        OnShieldChanged?.Invoke(Current, shieldMax);
    }

    // Hooked to the buy button
    public void TryRaiseShield()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.IsGameOver) return;
        if (IsActive) return;                 // no stacking while it's up
        if (!gm.SpendCoins(cost)) return;     // can't afford

        Current = shieldMax;
        OnShieldChanged?.Invoke(Current, shieldMax);
        OnShieldRaised?.Invoke();
    }

    // Returns the leftover damage that the shield could NOT absorb.
    // hitPos lets the visual shield ripple at the exact contact point.
    public int AbsorbDamage(int amount, Vector3 hitPos)
    {
        if (Current <= 0) return amount;

        if (controller != null) controller.HitAt(hitPos);


        int absorbed = Mathf.Min(Current, amount);
        Current -= absorbed;
        int leftover = amount - absorbed;

        OnShieldChanged?.Invoke(Current, shieldMax);

        if (Current <= 0)
            OnShieldBroken?.Invoke();

        return leftover;
    }

    // Overload without a position (falls back to shield centre).
    public int AbsorbDamage(int amount)
    {
        return AbsorbDamage(amount, transform.position);
    }
}