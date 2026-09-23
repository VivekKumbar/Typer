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
    [Tooltip("Overrides WaveManager's global Break Time for the countdown shown at the START of this wave, before its enemies spawn (e.g. a longer countdown before a boss wave). -1 = use the global Break Time default.")]
    public float breakTimeOverride = -1f;
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

    [Header("Wave Progression & 1.1x Scaling Curves")]
    [Tooltip("When true, dynamically generates balanced waves using the 1.1x exponential difficulty curves. When false, falls back to the authored waves array.")]
    public bool useMathematicalScaling = true;
    [Tooltip("Base spawn interval on Wave 1 in seconds (generous onboarding intro).")]
    [SerializeField] private float baseSpawnInterval = 3.0f;
    [Tooltip("Scaling multiplier per wave for spawn interval (Interval = Base / Scale^(Wave - 1)).")]
    [SerializeField] private float spawnIntervalScaleFactor = 1.09f;
    [Tooltip("Hard floor clamp for spawn interval to ensure human reaction readability.")]
    [SerializeField] private float minSpawnIntervalClamp = 0.65f;

    [Tooltip("Base enemy count on Wave 1.")]
    [SerializeField] private int baseEnemyCount = 5;
    [Tooltip("Enemy count growth factor per wave.")]
    [SerializeField] private float enemyCountGrowthFactor = 1.1f;
    [Tooltip("Hard ceiling clamp for total enemies per wave.")]
    [SerializeField] private int maxEnemyCountClamp = 35;

    [Tooltip("Extra speed added per wave: (wave - 1) * speedBonusPerWave.")]
    [SerializeField] private float speedBonusPerWave = 0.08f;
    [Tooltip("Hard maximum speed bonus clamp to prevent runaway velocities.")]
    [SerializeField] private float maxSpeedBonusClamp = 1.8f;

    [Header("Screen Density Protection")]
    [Tooltip("Base max active enemies allowed on screen simultaneously on Wave 1.")]
    [SerializeField] private int baseMaxActiveEnemies = 2;
    [Tooltip("Maximum active enemies growth per wave.")]
    [SerializeField] private float maxActiveEnemiesGrowthPerWave = 0.35f;
    [Tooltip("Hard cap on simultaneous active enemies on screen at once to protect player flow.")]
    [SerializeField] private int hardMaxActiveEnemiesClamp = 8;

    [Header("Authored Waves (Used if useMathematicalScaling is disabled)")]
    public Wave[] waves;

    [Header("Endless mode (after authored waves if useMathematicalScaling is disabled)")]
    public int endlessStartCount = 10;
    public int endlessCountPerWave = 2;
    public float endlessSpawnInterval = 1.0f;
    public float endlessSpeedBonusPerWave = 0.1f;

    [Header("Timing")]
    [Tooltip("Seconds the wave banner stays up before enemies start spawning.")]
    public float announceTime = 1.8f;
    [Tooltip("Seconds counted down live on the banner (5, 4, 3, 2, 1...) at the START of each wave, right after the 'WAVE N' label and before enemies begin spawning -- the global default. An individual Wave entry's Break Time Override can lengthen/shorten this for that specific wave. Also used for endless-mode waves, which have no per-wave override of their own.")]
    public float breakTime = 5f;

    [Header("Spawn area (top-down, XZ ground plane)")]
    public float minX = -4f;
    public float maxX = 4f;
    public float spawnZ = 12f;
    public float groundY = 100f;

    [Header("Upgrade: Greed boss risk (optional)")]
    [Tooltip("Extra move speed added to every spawning enemy while Greed (ComboManager.greedUpgrade) is at boss level — the 'small risk' that comes with its big coin multiplier.")]
    public float greedBossSpeedBump = 0.3f;

    [Header("Boss Encounter Cadence (ZType Style)")]
    [Tooltip("The big enemy prefab (Enemy + BigEnemySpawner). Leave empty to disable this feature entirely.")]
    public Enemy bigEnemyPrefab;
    [Tooltip("A boss appears every this-many waves after first appearance (e.g. Wave 5, 8, 11, 14, 17...).")]
    public int bigEnemyEveryNWaves = 3;
    [Tooltip("The first wave a boss appears on.")]
    public int firstBigEnemyWave = 5;
    [Tooltip("Word length on the boss's FIRST appearance.")]
    public int bigEnemyStartLength = 7;
    [Tooltip("How much longer the boss's word gets each subsequent appearance (2nd, 3rd, ...).")]
    public int bigEnemyLengthGrowthPerAppearance = 1;
    [Tooltip("Optional cap on the boss's word length. 0 = no cap.")]
    public int bigEnemyMaxLength = 12;
    [Tooltip("Number of armor / word phases for the boss (Phase 1 shield, Phase 2 core).")]
    public int bossPhases = 2;
    [Tooltip("On a boss wave, the normal enemy count for that wave is multiplied by this — fewer normal enemies to focus on the boss.")]
    [Range(0f, 1f)] public float normalCountMultiplierOnBigWave = 0.4f;

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

    // True only while a wave is genuinely live (enemies spawning / on the
    // field), i.e. the player can type. False during the wave banner, the
    // start-of-wave countdown (breakTime), the boss "PREPARE YOURSELF"
    // warning, and the gap before the upgrade draft. Read by GameManager's
    // WPM clock (IsTypingWindowOpen).
    private bool waveActive = false;
    public bool IsWaveActive => waveActive;

    // DEBUG CONSOLE HOOKS -- checked inside RunWaves() below. Wave progression
    // otherwise lives entirely in that one private coroutine with no external
    // entry point, so these two small flags are the minimal way to let the
    // console short-circuit the CURRENT wave's remaining spawns/wait and
    // optionally redirect which wave comes next, without restructuring the
    // coroutine's normal flow (autosave, draft, day/night, etc. all still run
    // exactly as they would for a real wave transition).
    private bool debugSkipRequested;
    private int? debugJumpToWaveIndex;

    // "skipwave": clears the field via the real Enemy.Defeat() path (same as
    // killall) and abandons the rest of THIS wave's spawns/wait, letting the
    // coroutine fall through to its normal end-of-wave flow (draft, waveIndex++).
    public void DebugSkipWave()
    {
        DebugKillAllEnemies();
        debugSkipRequested = true;
    }

    // "setwave N": jumps straight to wave N (1-based) by abandoning the
    // current wave the same way DebugSkipWave does, then overriding waveIndex
    // for the coroutine's next iteration.
    public void DebugSetWave(int waveNumber1Based)
    {
        debugJumpToWaveIndex = Mathf.Max(0, waveNumber1Based - 1);
        DebugSkipWave();
    }

    // Shared by DebugSkipWave and the console's "killall" command -- the real
    // Enemy.Defeat() path (full death juice, coins via the normal reward
    // logic), not a fake instant-clear.
    public static void DebugKillAllEnemies()
    {
        foreach (Enemy e in new System.Collections.Generic.List<Enemy>(Enemy.Active))
            if (e != null && !e.IsDefeated) e.Defeat();
    }

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
            waveActive = false; // announce / countdown / boss warning ahead -- not typeable

            if (debugJumpToWaveIndex.HasValue)
            {
                waveIndex = debugJumpToWaveIndex.Value;
                debugJumpToWaveIndex = null;
            }

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

            // Announce the wave, then count down (5, 4, 3, 2, 1 ...) before
            // spawning actually begins -- this IS the wave's start-of-wave
            // delay (see breakTime/breakTimeOverride below), shown live on
            // the banner rather than a silent wait.
            if (banner != null) banner.Show(BannerText(w, waveIndex));
            yield return Wait(announceTime);
            if (GameOver) yield break;

            float thisBreakTime = w.breakTimeOverride >= 0f ? w.breakTimeOverride : breakTime;
            yield return Countdown(thisBreakTime);
            if (GameOver) yield break;

            // Big enemy / Boss encounter (cadence starts at Wave 5, recurs every 3rd wave)
            bool bigWave = IsBigEnemyWave(waveNumber);
            Enemy activeBoss = null;
            if (bigWave && bigEnemyPrefab != null)
            {
                // Same "this wave has a boss" check that gates the spawn itself,
                // so the warning and the boss can never go out of sync.
                if (bossWarningBanner != null)
                    yield return bossWarningBanner.ShowAndWait();
                if (GameOver) yield break;

                activeBoss = SpawnBigEnemy(waveNumber);
            }

            waveActive = true; // banner, countdown and boss warning are over -- the player can type now

            // If a boss is active, pause standard minion spawns until the boss is defeated.
            // This allows the player to focus on the boss and its mini missile fragments without screen clutter!
            if (activeBoss != null)
            {
                while (activeBoss != null && !activeBoss.IsDefeated)
                {
                    if (GameOver) yield break;
                    if (debugSkipRequested) break;
                    yield return null;
                }
            }

            // Spawn this wave's enemies (Night can scale how many; a boss
            // wave also thins the normal spawns to make room for the boss duel)
            float countMult = DayNightCycle.Instance != null ? DayNightCycle.Instance.CurrentProfile.enemyCountMultiplier : 1f;
            int enemyCount = Mathf.Max(1, Mathf.RoundToInt(w.enemyCount * countMult));
            if (bigWave) enemyCount = Mathf.Max(1, Mathf.RoundToInt(enemyCount * normalCountMultiplierOnBigWave));

            int maxActive = GetMaxActiveEnemies(waveNumber);

            for (int i = 0; i < enemyCount; i++)
            {
                if (GameOver) yield break;
                if (debugSkipRequested) break; // "skipwave"/"setwave" -- abandon this wave's remaining spawns

                // Screen density protection: wait if too many enemies are active simultaneously.
                // Ensures Wave 1 has at most 2 active enemies on screen at any time!
                while (Enemy.Active.Count >= maxActive)
                {
                    if (GameOver) yield break;
                    if (debugSkipRequested) break;
                    yield return null;
                }

                if (debugSkipRequested) break;
                if (enemyPrefabs != null && enemyPrefabs.Length > 0) SpawnOne(w.speedBonus, waveNumber);
                yield return Wait(w.spawnInterval);
            }
            debugSkipRequested = false; // consumed -- doesn't leak into the next wave

            // Wait until the field is clear before starting the next wave
            while (Enemy.Active.Count > 0)
            {
                if (GameOver) yield break;
                yield return null;
            }

            waveActive = false; // field is clear -- nothing left to type until the next wave goes live
            BridgeManager.SendLevelCompleted(waveNumber);

            // Between-wave upgrade draft: pauses, offers 3 cards, resumes on pick.
            if (UpgradeManager.Instance != null)
                yield return UpgradeManager.Instance.RunDraft();

            waveIndex++;
        }
    }

    public Wave GetWave(int index)
    {
        int waveNumber = index + 1;
        if (!useMathematicalScaling && waves != null && index < waves.Length && waves[index] != null)
        {
            return waves[index];
        }

        // Dynamic 1.1x mathematical scaling curve
        return new Wave
        {
            label = IsBigEnemyWave(waveNumber) ? $"BOSS WAVE {waveNumber}" : $"WAVE {waveNumber}",
            enemyCount = GetEnemyCount(waveNumber),
            spawnInterval = GetSpawnInterval(waveNumber),
            speedBonus = GetSpeedBonus(waveNumber),
            breakTimeOverride = -1f
        };
    }

    public float GetSpawnInterval(int waveNumber)
    {
        // Decrease spawn interval by InitialInterval / (1.1 ^ (wave - 1)) with safe floor clamp
        float interval = baseSpawnInterval / Mathf.Pow(spawnIntervalScaleFactor, waveNumber - 1);
        return Mathf.Max(minSpawnIntervalClamp, interval);
    }

    public float GetSpeedBonus(int waveNumber)
    {
        // Increase enemy approach speed by (wave - 1) * speedBonusPerWave with safe max velocity clamp
        float bonus = (waveNumber - 1) * speedBonusPerWave;
        return Mathf.Min(maxSpeedBonusClamp, bonus);
    }

    public int GetMaxActiveEnemies(int waveNumber)
    {
        // Onboarding protection: Wave 1 = 2 max, Wave 2 = 3 max, Wave 3 = 4 max
        if (waveNumber == 1) return 2;
        if (waveNumber == 2) return 3;
        if (waveNumber == 3) return 4;

        int cap = baseMaxActiveEnemies + Mathf.FloorToInt((waveNumber - 1) * maxActiveEnemiesGrowthPerWave);
        return Mathf.Clamp(cap, 2, hardMaxActiveEnemiesClamp);
    }

    public int GetEnemyCount(int waveNumber)
    {
        int count = Mathf.RoundToInt(baseEnemyCount * Mathf.Pow(enemyCountGrowthFactor, waveNumber - 1));
        return Mathf.Clamp(count, baseEnemyCount, maxEnemyCountClamp);
    }

    public void GetWordLengths(int waveNumber, Enemy prefab, out int minLen, out int maxLen)
    {
        // Dynamic word pool distribution:
        // Waves 1-3: 3-5 letters (Wave 1 strictly 3-4 simple common words)
        // Waves 4-7: 4-7 letters with occasional composite words
        // Waves 8+: Mix of fast 3-letter interceptors and 8-12 letter heavy ships
        if (waveNumber == 1)
        {
            minLen = 3;
            maxLen = 4;
        }
        else if (waveNumber <= 3)
        {
            minLen = 3;
            maxLen = 5;
        }
        else if (waveNumber <= 7)
        {
            minLen = 4;
            maxLen = 7;
        }
        else
        {
            if (prefab != null && prefab.enemyTypeId == "Runner")
            {
                minLen = 3;
                maxLen = 4;
            }
            else if (prefab != null && prefab.enemyTypeId == "Brute")
            {
                minLen = 7;
                maxLen = 11;
            }
            else
            {
                float r = Random.value;
                if (r < 0.35f) { minLen = 3; maxLen = 4; }
                else if (r < 0.75f) { minLen = 5; maxLen = 7; }
                else { minLen = 8; maxLen = Mathf.Min(12, 7 + (waveNumber - 7)); }
            }
        }
    }

    string BannerText(Wave w, int index)
    {
        bool custom = !string.IsNullOrEmpty(w.label) && w.label != "Wave";
        return custom ? w.label : ("WAVE " + (index + 1));
    }

    public bool IsBigEnemyWave(int waveNumber)
    {
        if (bigEnemyEveryNWaves <= 0 || waveNumber < firstBigEnemyWave) return false;
        return (waveNumber - firstBigEnemyWave) % bigEnemyEveryNWaves == 0;
    }

    // Spawns a high-threat flagship boss with multi-phase words (Phase 1 shield armor, Phase 2 core).
    Enemy SpawnBigEnemy(int waveNumber)
    {
        bigEnemyAppearances++;
        int baseLen = bigEnemyStartLength + (bigEnemyAppearances - 1) * bigEnemyLengthGrowthPerAppearance;
        if (bigEnemyMaxLength > 0) baseLen = Mathf.Min(baseLen, bigEnemyMaxLength);
        baseLen = Mathf.Max(4, baseLen);

        Vector3 pos = new Vector3(Random.Range(minX * 0.5f, maxX * 0.5f), groundY, spawnZ);
        Enemy boss = Instantiate(bigEnemyPrefab, pos, Quaternion.identity);

        string firstWord = wordBank != null ? wordBank.GetWord(baseLen, baseLen) : "FLAGSHIP";
        if (string.IsNullOrEmpty(firstWord)) firstWord = "DESTROYER";

        System.Func<int, string> wordProvider = (phaseIndex) =>
        {
            int pLen = Mathf.Clamp(baseLen + (phaseIndex - 1), 4, 14);
            string pw = wordBank != null ? wordBank.GetWord(pLen, pLen) : null;
            if (string.IsNullOrEmpty(pw)) pw = (phaseIndex == 2 ? "REACTOR" : "OVERLOAD");
            return pw;
        };

        boss.Init(
            word: firstWord,
            fortress: fortress,
            speedBonus: 0f, // Boss moves at deliberate flagship drift speed
            waveNumber: waveNumber,
            isBossEnemy: true,
            phases: bossPhases,
            wordProvider: wordProvider
        );

        return boss;
    }

    void SpawnOne(float speedBonus, int waveNumber)
    {
        Enemy prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        Vector3 pos = new Vector3(Random.Range(minX, maxX), groundY, spawnZ);
        Enemy e = Instantiate(prefab, pos, Quaternion.identity);

        // Dynamic word length shift
        GetWordLengths(waveNumber, prefab, out int minLen, out int maxLen);
        DifficultyProfile profile = DayNightCycle.Instance != null ? DayNightCycle.Instance.CurrentProfile : null;
        int shift = profile != null ? profile.wordLengthShift : 0;
        minLen = Mathf.Max(1, minLen + shift);
        maxLen = Mathf.Max(minLen, maxLen + shift);
        string word = wordBank != null ? wordBank.GetWord(minLen, maxLen) : "TEST";

        // Speed: wave's own bonus, day/night multiplier, Heavy Boots slow, and Greed bump
        float extraSpeed = speedBonus;
        if (profile != null) extraSpeed += prefab.moveSpeed * (profile.enemySpeedMultiplier - 1f);

        UpgradeManager um = UpgradeManager.Instance;
        if (um != null && um.heavyBootsUpgrade != null)
        {
            int hbLevel = um.LevelOf(um.heavyBootsUpgrade);
            if (hbLevel > 0 && hbLevel < UpgradeDefinition.BossLevel)
                extraSpeed -= um.heavyBootsUpgrade.ValueForLevel(hbLevel);
        }
        if (ComboManager.Instance != null && ComboManager.Instance.greedUpgrade != null && um != null
            && um.LevelOf(ComboManager.Instance.greedUpgrade) >= UpgradeDefinition.BossLevel)
        {
            extraSpeed += greedBossSpeedBump;
        }

        e.Init(
            word: word,
            fortress: fortress,
            speedBonus: extraSpeed,
            waveNumber: waveNumber,
            isBossEnemy: false,
            phases: 1,
            wordProvider: null
        );

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

    [ContextMenu("Simulate 20 Waves")]
    public void Simulate20Waves()
    {
        SimulateWaves(20);
    }

    public void SimulateWaves(int count = 20)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== WAVE PROGRESSION BALANCING SIMULATION (1.1x Scaling) ===");
        sb.AppendLine(string.Format("{0,-6} | {1,-6} | {2,-7} | {3,-8} | {4,-7} | {5,-11} | {6,-10} | {7,-8} | {8,-10}",
            "Wave", "Boss?", "Enemies", "Interval", "Speed+", "Word Len", "Max Active", "Est WPM", "Est Coins"));
        sb.AppendLine(new string('-', 98));

        for (int w = 1; w <= count; w++)
        {
            bool isBoss = IsBigEnemyWave(w);
            int enemies = GetEnemyCount(w);
            if (isBoss) enemies = Mathf.Max(2, Mathf.RoundToInt(enemies * normalCountMultiplierOnBigWave));
            float interval = GetSpawnInterval(w);
            float speedBonus = GetSpeedBonus(w);
            GetWordLengths(w, null, out int minLen, out int maxLen);
            int maxActive = GetMaxActiveEnemies(w);
            float avgLen = (minLen + maxLen) * 0.5f;
            int estWpm = Mathf.RoundToInt((avgLen / interval) * 12f);

            // Projected coin yield
            int estCoins = 0;
            for (int i = 0; i < enemies; i++)
            {
                int enemyCoins = 3 + Mathf.FloorToInt((w - 1) * 0.35f) + Mathf.Max(0, Mathf.RoundToInt(avgLen) - 4);
                estCoins += enemyCoins;
            }
            if (isBoss)
            {
                int bossCoins = 20 + 25 + w * 6;
                estCoins += bossCoins + (bossCoins / 2); // full kill + phase reward
            }

            sb.AppendLine(string.Format("{0,-6} | {1,-6} | {2,-7} | {3,-8:F2}s | +{4,-6:F2} | {5,-11} | {6,-10} | ~{7,-7} | ~{8,-10}",
                w, isBoss ? "YES" : "no", enemies, interval, speedBonus, $"{minLen}-{maxLen} chars", maxActive, $"{estWpm} WPM", estCoins));
        }

        sb.AppendLine(new string('-', 98));
        Debug.Log(sb.ToString());
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

    // Live "5, 4, 3, 2, 1..." countdown on the banner before a wave's enemies
    // start spawning. Whole seconds only (rounded up), one number per second.
    IEnumerator Countdown(float seconds)
    {
        int whole = Mathf.Max(1, Mathf.CeilToInt(seconds));
        for (int i = whole; i >= 1; i--)
        {
            if (GameOver) yield break;
            if (banner != null) banner.ShowRaw(i.ToString());
            yield return Wait(1f);
        }
        if (banner != null) banner.Hide();
    }
}
