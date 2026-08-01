using System;
using System.Collections.Generic;
using UnityEngine;

// Tracks the typing combo (multiplies coins), and an Overload meter that fills
// as you type. When full, TriggerOverload() clears every enemy on screen.
public class ComboManager : MonoBehaviour
{
    public static ComboManager Instance { get; private set; }

    [Header("Combo")]
    public int combo;
    [Tooltip("Every this-many combo adds +1 to the coin multiplier.")]
    public int comboPerMultiplierStep = 10;
    public float maxMultiplier = 5f;

    // Highest combo reached this run (a fresh instance each time GameScene loads,
    // so this naturally starts at 0 per run — no manual reset needed).
    public int HighestComboThisRun { get; private set; }

    // True if the word currently locked in TypingController hasn't had a wrong
    // keystroke yet. Reset via NotifyNewTarget() when a new target locks, cleared
    // on a miss. Enemy.Die reads this to show the PERFECT! popup.
    public bool CurrentWordPerfect { get; private set; } = true;

    [Header("Overload")]
    [Tooltip("Correct letters needed to fill the Overload meter.")]
    public float overloadMax = 30f;
    public float overload;
    public bool overloadReady;

    // UI hooks
    public event Action<int, float> OnComboChanged;   // (combo, multiplier)
    public event Action<float> OnOverloadChanged;     // fill 0..1
    public event Action OnOverloadReady;

    public static float Multiplier => Instance ? Instance.CurrentMultiplier() : 1f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    float CurrentMultiplier()
    {
        float m = 1f + Mathf.Floor((float)combo / Mathf.Max(1, comboPerMultiplierStep));
        return Mathf.Min(m, maxMultiplier);
    }

    // Call when TypingController locks onto a fresh enemy — starts a clean
    // "no mistakes yet" slate for the word that's now being typed.
    public void NotifyNewTarget()
    {
        CurrentWordPerfect = true;
    }

    public void RegisterHit()
    {
        float multiplierBefore = CurrentMultiplier();
        combo++;
        if (combo > HighestComboThisRun) HighestComboThisRun = combo;
        OnComboChanged?.Invoke(combo, CurrentMultiplier());

        // Popup only on an actual multiplier step-up (every comboPerMultiplierStep
        // combo) — not on every hit, so it doesn't clutter the screen.
        if (CurrentMultiplier() > multiplierBefore && TypingController.Instance != null && TypingController.Instance.CurrentTarget != null)
            PopupManager.ShowCombo(TypingController.Instance.CurrentTarget.transform.position, combo);

        if (!overloadReady)
        {
            overload += 1f;
            if (overload >= overloadMax)
            {
                overload = overloadMax;
                overloadReady = true;
                OnOverloadReady?.Invoke();
            }
            OnOverloadChanged?.Invoke(overload / overloadMax);
        }
    }

    public void RegisterMiss()
    {
        CurrentWordPerfect = false;
        if (combo == 0) return;
        combo = 0;
        OnComboChanged?.Invoke(combo, CurrentMultiplier());
        // Overload is NOT drained on a miss � only the combo resets.
    }

    public void TriggerOverload()
    {
        if (!overloadReady) return;

        // Clear every active enemy (full death juice + coins via Defeat)
        List<Enemy> snapshot = new List<Enemy>(Enemy.Active);
        foreach (Enemy e in snapshot)
            if (e != null && !e.IsDefeated) e.Defeat();

        overload = 0f;
        overloadReady = false;
        OnOverloadChanged?.Invoke(0f);
        CameraShake.Shake(0.4f, 0.6f);
    }
}
