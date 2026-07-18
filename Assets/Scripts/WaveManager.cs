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
    [Header("Refs")]
    public Enemy[] enemyPrefabs;
    public WordBank wordBank;
    public Transform fortress;
    public WaveBanner banner;            // optional

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

    private int waveIndex = 0;

    bool GameOver => GameManager.Instance != null && GameManager.Instance.IsGameOver;

    void Start() { StartCoroutine(RunWaves()); }

    IEnumerator RunWaves()
    {
        while (true)
        {
            if (GameOver) yield break;
            Wave w = GetWave(waveIndex);

            // Announce the wave
            if (banner != null) banner.Show(BannerText(w, waveIndex));
            yield return Wait(announceTime);
            if (GameOver) yield break;

            // Spawn this wave's enemies
            for (int i = 0; i < w.enemyCount; i++)
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

            yield return Wait(breakTime);
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

    void SpawnOne(float speedBonus)
    {
        Enemy prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        Vector3 pos = new Vector3(Random.Range(minX, maxX), groundY, spawnZ);
        Enemy e = Instantiate(prefab, pos, Quaternion.identity);
        string word = wordBank.GetWord(prefab.minLetters, prefab.maxLetters);
        e.Init(word, fortress, speedBonus);
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