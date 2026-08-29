using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Runs the between-wave upgrade draft. Tracks each upgrade's level FOR THIS
// RUN ONLY (a fresh instance each time GameScene loads, so it naturally starts
// empty — nothing is persisted to Wallet/PlayerPrefs, matching the shop's
// separate, persistent cosmetic system).
//
// WaveManager calls RunDraft() as a sub-coroutine after a wave clears and
// before the next one starts. Other systems (ComboManager, TimeSinkManager,
// ShieldManager, GameManager, Enemy, WaveManager) each hold a reference to
// their own relevant UpgradeDefinition and read UpgradeManager.Instance.LevelOf(...)
// directly — this script doesn't know the specifics of any single upgrade's
// effect, it only runs the draft itself. Head Start / Heavy Boots / Coin Magnet
// don't have an obvious existing-system owner, so their effects are wired here
// and in WaveManager/Enemy instead.
public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    [Header("Config")]
    [Tooltip("The full list of draftable upgrades.")]
    public UpgradePool pool;
    [Tooltip("Uncheck to disable the between-wave draft entirely (waves just chain with no pause).")]
    public bool enableDraft = true;
    [Tooltip("How many cards to offer each draft.")]
    [Range(1, 5)] public int cardsPerDraft = 3;
    [Tooltip("Relative pick weight for NOT-YET-OWNED upgrades (level 0) vs OWNED upgrades being leveled up. 0.5 = equal chance; higher favors offering new upgrades over leveling owned ones.")]
    [Range(0f, 1f)] public float newUpgradeWeight = 0.5f;

    [Header("UI")]
    [Tooltip("The draft panel controller. Shown/hidden automatically by RunDraft().")]
    public UpgradeDraftUI draftUI;

    [Header("Head Start (new upgrade)")]
    [Tooltip("Optional. Each level is the CHANCE (0-1) a spawning word starts with its first letter pre-typed. Boss (6): guaranteed, and pre-types the first 2 letters. Suppressed on nights with Difficulty Profile 'No Head Start' checked. Read by WaveManager at spawn time.")]
    public UpgradeDefinition headStartUpgrade;

    [Header("Heavy Boots (new upgrade)")]
    [Tooltip("Optional. Each level subtracts this much move speed from every spawning enemy (read by WaveManager at spawn time). Boss (6) additionally applies a passive slow near the tower — see the fields below.")]
    public UpgradeDefinition heavyBootsUpgrade;
    [Tooltip("Heavy Boots boss only: distance from the fortress where the near-tower slow reaches full strength.")]
    public float heavyBootsBossDangerDistance = 3f;
    [Tooltip("Heavy Boots boss only: distance from the fortress where the near-tower slow has no effect.")]
    public float heavyBootsBossSafeDistance = 8f;
    [Tooltip("Heavy Boots boss only: speed multiplier applied right at the tower (0.6 = 40% slower). Composes independently of Time Sink's own slow.")]
    [Range(0.1f, 1f)] public float heavyBootsBossNearTowerSlow = 0.6f;

    [Header("Coin Magnet (new upgrade)")]
    [Tooltip("Optional. Enemy.Die's coin-reward cap (normally 30) is raised by this many coins per level, so bigger combo multipliers actually pay off instead of being wasted on a capped reward. Boss (6) uses Boss Value the same way.")]
    public UpgradeDefinition coinMagnetUpgrade;

    private readonly Dictionary<string, int> levels = new Dictionary<string, int>();
    private UpgradeDefinition picked;

    // (definition, newLevel) — fired whenever a pick changes an upgrade's level.
    public event Action<UpgradeDefinition, int> OnUpgradeChanged;

    void Awake()
    {
        Instance = this;

        // Restore happens here (not Start) so it's guaranteed to finish before
        // ANY object's Start runs — in particular AbilityUIRegistry.Start,
        // which does its own "sync now" pass over LevelOf() rather than
        // relying on OnUpgradeChanged (which would race against its own
        // subscription if fired from here instead).
        if (SaveManager.IsContinuing && SaveManager.HasSave())
            RestoreLevels(SaveManager.LoadRun());
    }

    // Flattens the levels dictionary into RunSaveData's parallel id/level
    // arrays — JsonUtility can't serialize a Dictionary directly.
    public void CaptureLevelsInto(RunSaveData data)
    {
        data.upgradeIds = new string[levels.Count];
        data.upgradeLevels = new int[levels.Count];
        int i = 0;
        foreach (KeyValuePair<string, int> kv in levels)
        {
            data.upgradeIds[i] = kv.Key;
            data.upgradeLevels[i] = kv.Value;
            i++;
        }
    }

    void RestoreLevels(RunSaveData data)
    {
        if (data == null || data.upgradeIds == null) return;
        levels.Clear();
        for (int i = 0; i < data.upgradeIds.Length; i++)
            levels[data.upgradeIds[i]] = data.upgradeLevels[i];
        // Deliberately doesn't fire OnUpgradeChanged — nothing has subscribed
        // yet this early (Awake). AbilityUIRegistry.Start does its own
        // current-state sync instead of relying on this event for restore
        // (see its comment). GameManager's repair-boss-heal subscription only
        // needs to catch a FUTURE pick reaching boss level, so it doesn't need
        // a restore replay — a restored boss-level repair already healed the
        // player in the session that picked it.
    }

    public int LevelOf(UpgradeDefinition def)
    {
        if (def == null) return 0;
        return levels.TryGetValue(def.id, out int l) ? l : 0;
    }

    public bool IsMaxed(UpgradeDefinition def) => LevelOf(def) >= UpgradeDefinition.BossLevel;

    // Yielded by WaveManager between waves. Shows the draft, waits for a pick
    // (frame-based, so it works fine while timeScale is 0), applies it, then
    // returns. Coordinates timeScale the same way HitStop does: only freezes if
    // nothing already has, and only unfreezes if it's still the one holding it.
    public IEnumerator RunDraft()
    {
        if (!enableDraft || pool == null) yield break;

        List<UpgradeDefinition> offered = PickOffers();
        if (offered.Count == 0) yield break; // everything's maxed — nothing to offer

        bool weFroze = false;
        if (Time.timeScale != 0f)
        {
            Time.timeScale = 0f;
            weFroze = true;
        }

        picked = null;
        if (draftUI != null) draftUI.Show(offered, def => picked = def);

        while (picked == null)
            yield return null;

        if (draftUI != null) draftUI.Hide();

        ApplyPick(picked);

        if (weFroze && Time.timeScale == 0f)
            Time.timeScale = 1f;
    }

    List<UpgradeDefinition> PickOffers()
    {
        List<UpgradeDefinition> candidates = new List<UpgradeDefinition>();
        if (pool.upgrades != null)
            foreach (UpgradeDefinition u in pool.upgrades)
                if (u != null && !IsMaxed(u)) candidates.Add(u);

        List<UpgradeDefinition> result = new List<UpgradeDefinition>();
        int want = Mathf.Min(cardsPerDraft, candidates.Count);
        for (int i = 0; i < want; i++)
        {
            float totalWeight = 0f;
            foreach (UpgradeDefinition c in candidates) totalWeight += WeightOf(c);
            if (totalWeight <= 0f) break;

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            UpgradeDefinition chosen = candidates[candidates.Count - 1];
            float acc = 0f;
            foreach (UpgradeDefinition c in candidates)
            {
                acc += WeightOf(c);
                if (roll <= acc) { chosen = c; break; }
            }

            result.Add(chosen);
            candidates.Remove(chosen);
        }
        return result;
    }

    float WeightOf(UpgradeDefinition def) => LevelOf(def) == 0 ? newUpgradeWeight : (1f - newUpgradeWeight);

    void ApplyPick(UpgradeDefinition def)
    {
        int next = Mathf.Min(LevelOf(def) + 1, UpgradeDefinition.BossLevel);
        levels[def.id] = next;
        OnUpgradeChanged?.Invoke(def, next);
    }

    // Heavy Boots boss: passive near-tower slow, independent of Time Sink so
    // the two effects never fight over the same field (see Enemy.NearTowerSlowMultiplier).
    void Update()
    {
        bool bossActive = heavyBootsUpgrade != null && LevelOf(heavyBootsUpgrade) >= UpgradeDefinition.BossLevel;

        foreach (Enemy e in Enemy.Active)
        {
            if (e == null) continue;
            if (!bossActive) { e.NearTowerSlowMultiplier = 1f; continue; }

            float t = Mathf.Clamp01(Mathf.InverseLerp(heavyBootsBossSafeDistance, heavyBootsBossDangerDistance, e.DistanceToFortress));
            e.NearTowerSlowMultiplier = Mathf.Lerp(1f, heavyBootsBossNearTowerSlow, t);
        }
    }
}
