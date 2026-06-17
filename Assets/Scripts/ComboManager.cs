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

    public void RegisterHit()
    {
        combo++;
        OnComboChanged?.Invoke(combo, CurrentMultiplier());

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
        if (combo == 0) return;
        combo = 0;
        OnComboChanged?.Invoke(combo, CurrentMultiplier());
        // Overload is NOT drained on a miss — only the combo resets.
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
