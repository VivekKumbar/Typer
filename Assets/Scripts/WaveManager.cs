using System.Collections;
using UnityEngine;

// TOP-DOWN version: spawns enemies along the FAR edge of the ground (XZ plane);
// they march toward the fortress at the center. Ramps speed/frequency over time.
public class WaveManager : MonoBehaviour
{
    [Header("Refs")]
    public Enemy enemyPrefab;
    public WordBank wordBank;
    public Transform fortress;

    [Header("Spawn area (top-down, XZ ground plane)")]
    public float minX = -6f;     // left/right spread of the spawn line
    public float maxX = 6f;
    public float spawnZ = 12f;   // far edge; enemies walk from here toward the fortress
    public float groundY = 0.5f; // height enemies sit at above the ground

    [Header("Difficulty")]
    public float startSpawnInterval = 2.5f;
    public float minSpawnInterval = 0.8f;
    public float enemyBaseSpeed = 1.2f;

    private int wave = 0;
    private int spawned = 0;

    void Start() { StartCoroutine(SpawnLoop()); }

    IEnumerator SpawnLoop()
    {
        while (true)
        {
            if (GameManager.Instance != null && GameManager.Instance.IsGameOver) yield break;
            SpawnOne();
            spawned++;
            wave = spawned / 10; // harder every 10 enemies
            float interval = Mathf.Max(minSpawnInterval, startSpawnInterval - wave * 0.15f);
            yield return new WaitForSeconds(interval);
        }
    }

    void SpawnOne()
    {
        Vector3 pos = new Vector3(Random.Range(minX, maxX), groundY, spawnZ);
        Enemy e = Instantiate(enemyPrefab, pos, Quaternion.identity);
        e.Init(wordBank.GetWord(wave), fortress, enemyBaseSpeed + wave * 0.08f);
    }
}
