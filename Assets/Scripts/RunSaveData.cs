using System;
using UnityEngine;
// Plain, JsonUtility-friendly snapshot of an in-progress run — everything
// needed to resume exactly where the player left off. Written/applied by
// SaveManager. Deliberately separate from Wallet (permanent currency) and
// ShopInventory/WordPackSelection (permanent ownership + shop selection) —
// none of those are touched by this system, only in-progress run state.
[Serializable]
public class RunSaveData
{
    [Header("Wave / day-night")]
    public int waveNumber = 1;        // the wave to RESUME at (matches WaveManager.CurrentWaveNumber)
    public bool isNight;              // informational only — DayNightCycle recomputes this from waveNumber on restore
    public int bigEnemyAppearances;   // how many big-enemy appearances happened so far, for word-length scaling

    [Header("Fortress")]
    public int health;
    public int maxHealth;
    public int gmShield;              // GameManager's own flat shield pool
    public int abilityShield;         // ShieldManager's ability-driven shield pool

    [Header("Economy (this run)")]
    public int coins;                 // spendable this run
    public int coinsEarnedThisRun;

    [Header("Combo / overload (nice-to-have)")]
    public int combo;
    public int highestComboThisRun;
    public float overload;
    public bool overloadReady;

    [Header("Upgrades — UpgradeManager's level-per-id, flattened")]
    // JsonUtility can't serialize a Dictionary, so id/level are parallel arrays.
    public string[] upgradeIds = new string[0];
    public int[] upgradeLevels = new int[0];
}
