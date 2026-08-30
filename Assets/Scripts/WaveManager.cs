using System.Collections;
using UnityEngine;

// One authored wave. Add as many as you want in the Inspector.
[System.Serializable]
public class Wave
{
    [Tooltip("Optional custom banner text. Leave as 'Wave' to auto-show 'WAVE N'.")]
    public string label = "Wave";
    [Tooltip("How many enemies (words) spawn this wave.")]
    public int enemyCount = 5;
    [Tooltip("Seconds between each spawn.")]
    public float spawnInterval = 1.5f;
    [Tooltip("Extra speed added to every enemy this wave.")]
    public float speedBonus = 0f;
}

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Refs")]
    public Enemy[] enemyPrefabs;
    public WordBank wordBank;
    public Transform fortress;
    public WaveBanner banner;            // optional
    [Tooltip("Optional. Shown right before a boss wave's big enemy spawns — see the Big Enemy section below.")]
    public BossWarningBanner bossWarningBanner;

    [Header("WAVES — edit these to set words-per-wave")]
    public Wave[] waves;

    [Header("Endless mode (after the authored waves)")]
    public int endlessStartCount = 10;
    public int endlessCountPerWave = 2;
    public float endlessSpawnInterval = 1.0f;
    public float endlessSpeedBonusPerWave = 0.1f;

    [Header("Timing")]
    public float announceTime = 1.8f;    // banner duration before spawning
    public float breakTime = 1.0f;       // pause after a wave is cleared

    [Header("Spawn area (top-down, XZ ground plane)")]
    public float minX = -4f;
    public float maxX = 4f;
    public float spawnZ = 12f;
    public float groundY = 100f;

    [Header("Upgrade: Greed boss risk (optional)")]
    [Tooltip("Extra move speed added to every spawning enemy while Greed (ComboManager.greedUpgrade) is at boss level — the 'small risk' that comes with its big coin multiplier.")]
    public float greedBossSpeedBump = 0.3f;

    [Header("Big Enemy (mini-boss + horde)")]
    [Tooltip("The big enemy prefab (Enemy + BigEnemySpawner). Leave empty to disable this feature entirely.")]
    public Enemy bigEnemyPrefab;
    [Tooltip("A big enemy appears every this-many waves.")]
    public int bigEnemyEveryNWaves = 4;
    [Tooltip("The first wave a big enemy can appear on.")]
    public int firstBigEnemyWave = 4;
    [Tooltip("Word length on the big enemy's FIRST appearance.")]
    public int bigEnemyStartLength = 8;
    [Tooltip("How much longer the big enemy's word gets each subsequent appearance (2nd, 3rd, ...).")]
    public int bigEnemyLengthGrowthPerAppearance = 1;
    [Tooltip("Optional cap on the big enemy's word length. 0 = no cap.")]
    public int bigEnemyMaxLength = 0;
    [Tooltip("On a big-enemy wave, the normal enemy count for that wave is multiplied by this — fewer normal enemies to make room for the mini-boss + horde.")]
    [Range(0f, 1f)] public float normalCountMultiplierOnBigWave = 0.5f;

    // How many big enemies have spawned this run so far (1st, 2nd, 3rd...),
    // used for the length-growth formula. Per-run only, resets on scene reload.
    private int bigEnemyAppearances = 0;

    private int waveIndex = 0;

    // 1-based, matching the "WAVE N" banner — the wave reached so far this run.
    public int CurrentWaveNumber => waveIndex + 1;

    // Read by SaveManager.CaptureAndSave so the big enemy's word-length
    // scaling continues correctly after a Continue.
    public int BigEnemyAppearances => bigEnemyAppearances;

    bool GameOver => GameManager.Instance != null && GameManager.Instance.IsGameOver;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (SaveManager.IsContinuing && SaveManager.HasSave())
        {
            RunSaveData save = SaveManager.LoadRun();
            waveIndex = Mathf.Max(0, save.waveNumber - 1);
            bigEnemyAppearances = Mathf.Max(0, save.bigEnemyAppearances);
        }
    }

    void Start()
    {
        // RunContext is locked in GameManager.Awake() (guaranteed to run before
        // any Start()) — force WordBank to rebuild its cached pool from that
        // locked snapshot instead of reusing whatever a previous run in this
        // app session left cached.
        if (wordBank != null) wordBank.RebuildForNewRun();
        SfxPlayer.PlayGameStart(); // once per run -- New Game and Continue both land here
        StartCoroutine(RunWaves());
    }

    IEnumerator RunWaves()
    {
        while (true)
        {
            if (GameOver) yield break;
            Wave w = GetWave(waveIndex);
            int waveNumber = waveIndex + 1;

            // Day/night + per-wave upgrade hooks, before anything else this wave.
            if (DayNightCycle.Instance != null) DayNightCycle.Instance.ApplyForWave(waveNumber);
            if (ShieldManager.Instance != null) ShieldManager.Instance.NotifyWaveStart();
            if (ComboManager.Instance != null) ComboManager.Instance.NotifyWaveStart(DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight);
            if (GameManager.Instance != null) GameManager.Instance.ApplyRepairUpgrade();

            // Autosave checkpoint — the start of a wave is the safest point to
            // resume from (never mid-wave). Also fires on wave 1 of a brand
            // new run, so Continue works even if the player quits early.
            SaveManager.CaptureAndSave(waveNumber);
            BridgeManager.SendLevelStarted(waveNumber);

            // Announce the wave
            if (banner != null) banner.Show(BannerText(w, waveIndex));
            yield return Wait(announceTime);
            if (GameOver) yield break;

            // Big enemy: spawn once at the top of the wave so it has the whole
            // wave to walk (and horde) before the field needs to clear.
            bool bigWave = IsBigEnemyWave(waveNumber);
            if (bigWave && bigEnemyPrefab != null)
            {
                // Same "this wave has a boss" check that gates the spawn itself,
                // so the warning and the boss can never go out of sync.
                if (bossWarningBanner != null)
                    yield return bossWarningBanner.ShowAndWait();
                if (GameOver) yield break;

                SpawnBigEnemy();
            }

            // Spawn this wave's enemies (Night can scale how many; a big-enemy
            // wave also thins the normal spawns to make room for the horde)
            float countMult = DayNightCycle.Instance != null ? DayNightCycle.Instance.CurrentProfile.enemyCountMultiplier : 1f;
            int enemyCount = Mathf.Max(1, Mathf.RoundToInt(w.enemyCount * countMult));
            if (bigWave) enemyCount = Mathf.Max(1, Mathf.RoundToInt(enemyCount * normalCountMultiplierOnBigWave));

            for (int i = 0; i < enemyCount; i++)
            {
                if (GameOver) yield break;
                if (enemyPrefabs != null && enemyPrefabs.Length > 0) SpawnOne(w.speedBonus);
                yield return Wait(w.spawnInterval);
            }

            // Wait until the field is clear before starting the next wave
            while (Enemy.Active.Count > 0)
            {
                if (GameOver) yield break;
                yield return null;
            }

            BridgeManager.SendLevelCompleted(waveNumber);

            yield return Wait(breakTime);

            // Between-wave upgrade draft: pauses, offers 3 cards, resumes on pick.
            if (UpgradeManager.Instance != null)
                yield return UpgradeManager.Instance.RunDraft();

            waveIndex++;
        }
    }

    Wave GetWave(int index)
    {
        if (waves != null && index < waves.Length) return waves[index];

        // Endless: scale up after the authored list runs out
        int extra = index - (waves != null ? waves.Length : 0);
        return new Wave
        {
            label = "Wave",
            enemyCount = endlessStartCount + extra * endlessCountPerWave,
            spawnInterval = endlessSpawnInterval,
            speedBonus = extra * endlessSpeedBonusPerWave
        };
    }

    string BannerText(Wave w, int index)
    {
        bool custom = !string.IsNullOrEmpty(w.label) && w.label != "Wave";
        return custom ? w.label : ("WAVE " + (index + 1));
    }

    bool IsBigEnemyWave(int waveNumber)
    {
        if (bigEnemyEveryNWaves <= 0 || waveNumber < firstBigEnemyWave) return false;
        return (waveNumber - firstBigEnemyWave) % bigEnemyEveryNWaves == 0;
    }

    // Word length uses its own dedicated growth formula (appearance number,
    // not the day/night length shift or the prefab's own Min/Max Letters) —
    // deliberately independent so it scales predictably run over run.
    void SpawnBigEnemy()
    {
        bigEnemyAppearances++;
        int length = bigEnemyStartLength + (bigEnemyAppearances - 1) * bigEnemyLengthGrowthPerAppearance;
        if (bigEnemyMaxLength > 0) length = Mathf.Min(length, bigEnemyMaxLength);
        length = Mathf.Max(1, length);

        Vector3 pos = new Vector3(Random.Range(minX, maxX), groundY, spawnZ);
        Enemy e = Instantiate(bigEnemyPrefab, pos, Quaternion.identity);
        string word = wordBank.GetWord(length, length);
        e.Init(word, fortress, 0f);
    }

    void SpawnOne(float speedBonus)
    {
        Enemy prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        Vector3 pos = new Vector3(Random.Range(minX, maxX), groundY, spawnZ);
        Enemy e = Instantiate(prefab, pos, Quaternion.identity);

        // Day/night word length shift — the word POOL is untouched, only the
        // length range drawn from it changes.
        DifficultyProfile profile = DayNightCycle.Instance != null ? DayNightCycle.Instance.CurrentProfile : null;
        int shift = profile != null ? profile.wordLengthShift : 0;
        int minLen = Mathf.Max(1, prefab.minLetters + shift);
        int maxLen = Mathf.Max(minLen, prefab.maxLetters + shift);
        string word = wordBank.GetWord(minLen, maxLen);

        // Speed: wave's own bonus, day/night multiplier (on the prefab's base
        // speed), Heavy Boots per-level slow, and Greed's boss-only risk bump.
        float extraSpeed = speedBonus;
        if (profile != null) extraSpeed += prefab.moveSpeed * (profile.enemySpeedMultiplier - 1f);

        UpgradeManager um = UpgradeManager.Instance;
        if (um != null && um.heavyBootsUpgrade != null)
        {
            int hbLevel = um.LevelOf(um.heavyBootsUpgrade);
            if (hbLevel > 0 && hbLevel < UpgradeDefinition.BossLevel)
                extraSpeed -= um.heavyBootsUpgrade.ValueForLevel(hbLevel);
            // Boss-level Heavy Boots' near-tower slow is applied continuously
            // by UpgradeManager (Enemy.NearTowerSlowMultiplier), not here.
        }
        if (ComboManager.Instance != null && ComboManager.Instance.greedUpgrade != null && um != null
            && um.LevelOf(ComboManager.Instance.greedUpgrade) >= UpgradeDefinition.BossLevel)
        {
            extraSpeed += greedBossSpeedBump;
        }

        e.Init(word, fortress, extraSpeed);

        if (profile != null && profile.mixedCaseWords)
            e.SetDisplayWordMixedCase();

        // Head Start upgrade
        if (um != null && um.headStartUpgrade != null && (profile == null || !profile.noHeadStart))
        {
            int level = um.LevelOf(um.headStartUpgrade);
            if (level > 0)
            {
                bool boss = level >= UpgradeDefinition.BossLevel;
                float chance = boss ? 1f : Mathf.Clamp01(um.headStartUpgrade.ValueForLevel(level));
                if (Random.value < chance)
                    e.PreType(boss ? 2 : 1);
            }
        }
    }

    IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (GameOver) yield break;
            t += Time.deltaTime;
            yield return null;
        }
    }
}
