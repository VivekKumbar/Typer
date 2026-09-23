using System.Collections.Generic;
using UnityEngine;

// High-performance object pool for marching enemies and boss flagships.
// Eliminates runtime Instantiate() and Destroy() spikes during wave transitions.
public class EnemyPool : MonoBehaviour
{
    public static EnemyPool Instance { get; private set; }

    [Header("Pool Config")]
    [Tooltip("Number of instances to prewarm per prefab at startup.")]
    [SerializeField] private int prewarmCountPerPrefab = 6;

    private readonly Dictionary<Enemy, Queue<Enemy>> _pools = new Dictionary<Enemy, Queue<Enemy>>();
    private readonly Dictionary<Enemy, Enemy> _instanceToPrefab = new Dictionary<Enemy, Enemy>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Prewarms the pool for the specified prefabs so wave 1 incurs zero Instantiate hitches.
    /// </summary>
    public void Prewarm(IEnumerable<Enemy> prefabs)
    {
        if (prefabs == null) return;
        foreach (Enemy prefab in prefabs)
        {
            if (prefab == null) continue;
            if (!_pools.ContainsKey(prefab))
                _pools[prefab] = new Queue<Enemy>();

            Queue<Enemy> queue = _pools[prefab];
            while (queue.Count < prewarmCountPerPrefab)
            {
                Enemy instance = CreateInstance(prefab);
                queue.Enqueue(instance);
            }
        }
    }

    /// <summary>
    /// Gets a pooled enemy or instantiates one if the pool is exhausted.
    /// </summary>
    public Enemy Get(Enemy prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        if (!_pools.ContainsKey(prefab))
            _pools[prefab] = new Queue<Enemy>();

        Queue<Enemy> queue = _pools[prefab];
        Enemy instance = null;

        while (queue.Count > 0)
        {
            Enemy candidate = queue.Dequeue();
            if (candidate != null)
            {
                instance = candidate;
                break;
            }
        }

        if (instance == null)
        {
            instance = CreateInstance(prefab);
        }

        instance.transform.SetPositionAndRotation(position, rotation);
        instance.gameObject.SetActive(true);
        return instance;
    }

    /// <summary>
    /// Returns a defeated enemy to the pool for reuse.
    /// </summary>
    public void Return(Enemy instance)
    {
        if (instance == null) return;

        instance.ResetState();
        instance.gameObject.SetActive(false);
        instance.transform.SetParent(transform, false);

        if (_instanceToPrefab.TryGetValue(instance, out Enemy prefab) && prefab != null)
        {
            if (!_pools.ContainsKey(prefab))
                _pools[prefab] = new Queue<Enemy>();

            _pools[prefab].Enqueue(instance);
        }
        else
        {
            Destroy(instance.gameObject);
        }
    }

    private Enemy CreateInstance(Enemy prefab)
    {
        Enemy instance = Instantiate(prefab, transform);
        instance.gameObject.SetActive(false);
        _instanceToPrefab[instance] = prefab;
        return instance;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        _pools.Clear();
        _instanceToPrefab.Clear();
    }
}
