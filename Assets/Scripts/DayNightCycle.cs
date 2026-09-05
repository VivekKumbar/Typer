using System;
using UnityEngine;

// Per-level difficulty knobs for one phase (day or night). Both dayProfile and
// nightProfile on DayNightCycle are this same shape — day just uses gentler
// values. Doesn't touch which words exist (WordBank/word packs still supply
// every word) — only how hard: the length range and a few modifier flags.
[Serializable]
public class DifficultyProfile
{
    [Tooltip("Multiplies this wave's enemy count (e.g. 1.3 = 30% more enemies).")]
    [Range(0.25f, 3f)] public float enemyCountMultiplier = 1f;
    [Tooltip("Multiplies enemy move speed, applied on top of the prefab's own base speed and the wave's own speed bonus.")]
    [Range(0.25f, 3f)] public float enemySpeedMultiplier = 1f;
    [Tooltip("Added to both the spawning enemy's Min/Max Letters before picking a word. Negative = shorter words. The word pool itself is untouched — this only changes which length range is drawn from.")]
    [Range(-3, 6)] public int wordLengthShift = 0;
    [Tooltip("Display words with randomly mixed upper/lower case letters. Typing still matches case-insensitively — this is purely a readability difficulty knob.")]
    public bool mixedCaseWords = false;
    [Tooltip("If checked, the Head Start upgrade's effect is suppressed this wave, regardless of its level.")]
    public bool noHeadStart = false;
    [Tooltip("If checked, the Muscle Memory (typo forgiveness) upgrade's effect is suppressed this wave.")]
    public bool stricterCombo = false;
}

// Put this anywhere in the scene (e.g. alongside WaveManager). Decides day vs
// night per wave and applies the matching DifficultyProfile + dark mode.
// WaveManager calls ApplyForWave(waveNumber) at the start of every wave; other
// systems read DayNightCycle.Instance.CurrentProfile / IsNight as needed.
public class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance { get; private set; }

    [Header("Pattern")]
    [Tooltip("Every Nth wave is a NIGHT wave. 2 (default) = odd waves DAY, even waves NIGHT. 1 = every wave is night.")]
    [Range(1, 10)] public int nightEveryN = 2;
    [Tooltip("Flips the whole pattern so wave 1 is NIGHT instead of DAY.")]
    public bool startAtNight = false;

    [Header("Day profile (easy)")]
    public DifficultyProfile dayProfile = new DifficultyProfile();

    [Header("Night profile (hard — dark mode)")]
    public DifficultyProfile nightProfile = new DifficultyProfile
    {
        enemyCountMultiplier = 1.3f,
        enemySpeedMultiplier = 1.2f,
        wordLengthShift = 2,
        mixedCaseWords = true,
        noHeadStart = true,
        stricterCombo = true,
    };

    [Header("Refs")]
    [Tooltip("Auto-found in Start if left empty.")]
    public DarkModeController darkModeController;

    public bool IsNight { get; private set; }
    public DifficultyProfile CurrentProfile => IsNight ? nightProfile : dayProfile;

    // (isNight)
    public event Action<bool> OnPhaseChanged;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (darkModeController == null)
            darkModeController = FindAnyObjectByType<DarkModeController>();
    }

    public bool IsNightWave(int waveNumber1Based)
    {
        int n = Mathf.Max(1, nightEveryN);
        bool baseNight = (waveNumber1Based % n) == 0;
        return startAtNight ? !baseNight : baseNight;
    }

    // Called by WaveManager right before announcing each wave.
    public void ApplyForWave(int waveNumber1Based)
    {
        IsNight = IsNightWave(waveNumber1Based);
        ApplyCurrentPhase();
    }

    // DEBUG CONSOLE HOOK: forces an immediate day/night flip, bypassing the
    // normal per-wave pattern entirely (the pattern still applies again
    // normally at the start of the next wave via ApplyForWave above -- this
    // is a one-shot override for testing, not a permanent pattern change).
    public void DebugToggleDayNight()
    {
        IsNight = !IsNight;
        ApplyCurrentPhase();
    }

    // Shared by ApplyForWave and DebugToggleDayNight -- applies whatever
    // IsNight is currently set to. Same path DarkModeToggle uses: SetDarkMode
    // both flips DarkMode.Enabled and re-applies the scene's lighting/volumes.
    void ApplyCurrentPhase()
    {
        if (darkModeController != null) darkModeController.SetDarkMode(IsNight);
        else DarkMode.Enabled = IsNight; // still persist the flag even with no controller in-scene

        OnPhaseChanged?.Invoke(IsNight);
    }
}
