using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Put this on the big enemy (mini-boss) prefab, alongside Enemy. Spawns a
// horde of minions in bursts WHILE this enemy is alive and walking — killing
// the big enemy (its word gets finished -> Enemy.Die -> IsDefeated) stops the
// horde IMMEDIATELY, even mid-burst. This is NOT a spawn-on-death effect:
// minions only ever appear during its life, so a fast kill means a small horde
// and a slow kill means a bigger one — the risk/reward tension.
[RequireComponent(typeof(Enemy))]
public class BigEnemySpawner : MonoBehaviour
{
    [Header("Horde (only while this enemy is alive)")]
    [Tooltip("Seconds between horde bursts while this enemy is alive.")]
    [Range(0.1f, 20f)] public float hordeBurstInterval = 3f;
    [Tooltip("Minions spawned per burst.")]
    [Range(1, 20)] public int hordeCountPerBurst = 3;
    [Tooltip("Minions spawn within this radius of the big enemy's CURRENT position — it's walking, so bursts follow wherever it currently is, not a fixed point.")]
    public float hordeSpawnRadius = 2f;
    [Tooltip("Cap on THIS spawner's own minions alive at once (defeated/destroyed ones don't count). A burst that would exceed this just spawns fewer, it doesn't queue the rest for later.")]
    public int maxAliveMinions = 15;

    [Header("Minion words")]
    [Tooltip("Minimum word length for minions.")]
    public int minionMinLetters = 1;
    [Tooltip("Maximum word length for minions.")]
    public int minionMaxLetters = 2;
    [Tooltip("If WordBank has no words in the Min/Max Letters range (it falls back to ANY word length), generate random letters instead — keeps minions trivial to type regardless of what's in the word pack.")]
    public bool allowRandomLettersForMinions = true;

    [Header("Refs")]
    [Tooltip("The minion Enemy prefab to spawn.")]
    public Enemy minionPrefab;
    [Tooltip("Auto-found from WaveManager.Instance in Start if left empty.")]
    public WordBank wordBank;
    [Tooltip("Auto-found from WaveManager.Instance in Start if left empty.")]
    public Transform fortress;

    private Enemy self;
    private Coroutine running;
    private readonly List<Enemy> spawnedMinions = new List<Enemy>();

    void Awake()
    {
        self = GetComponent<Enemy>();
    }

    void Start()
    {
        if (wordBank == null && WaveManager.Instance != null) wordBank = WaveManager.Instance.wordBank;
        if (fortress == null && WaveManager.Instance != null) fortress = WaveManager.Instance.fortress;

        running = StartCoroutine(HordeLoop());
    }

    void OnDisable()
    {
        // Safety net alongside the IsDefeated checks below — if this object is
        // disabled/destroyed for any reason, the horde stops with it.
        if (running != null) StopCoroutine(running);
        running = null;
    }

    IEnumerator HordeLoop()
    {
        while (self != null && !self.IsDefeated)
        {
            float t = 0f;
            while (t < hordeBurstInterval)
            {
                if (self == null || self.IsDefeated) yield break; // dead mid-wait -> stop now, no burst
                t += Time.deltaTime;
                yield return null;
            }

            if (self == null || self.IsDefeated) yield break; // re-check right before bursting -> never fire a burst after death

            SpawnBurst();
        }
    }

    void SpawnBurst()
    {
        if (minionPrefab == null || wordBank == null) return;

        PruneMinionList();

        for (int i = 0; i < hordeCountPerBurst; i++)
        {
            if (spawnedMinions.Count >= maxAliveMinions) break;

            Vector2 offset = Random.insideUnitCircle * hordeSpawnRadius;
            Vector3 pos = transform.position + new Vector3(offset.x, 0f, offset.y); // CURRENT position, not spawn-time

            Enemy minion = Instantiate(minionPrefab, pos, Quaternion.identity);
            minion.Init(GetMinionWord(), fortress, 0f);
            spawnedMinions.Add(minion);
        }
    }

    void PruneMinionList()
    {
        for (int i = spawnedMinions.Count - 1; i >= 0; i--)
            if (spawnedMinions[i] == null || spawnedMinions[i].IsDefeated) spawnedMinions.RemoveAt(i);
    }

    string GetMinionWord()
    {
        string word = wordBank.GetWord(minionMinLetters, minionMaxLetters);
        bool inRange = !string.IsNullOrEmpty(word) && word.Length >= minionMinLetters && word.Length <= minionMaxLetters;
        if (!inRange && allowRandomLettersForMinions)
            word = RandomLetters(Mathf.Max(1, minionMinLetters));
        return word;
    }

    string RandomLetters(int length)
    {
        var sb = new System.Text.StringBuilder(length);
        for (int i = 0; i < length; i++)
            sb.Append((char)('A' + Random.Range(0, 26)));
        return sb.ToString();
    }
}
