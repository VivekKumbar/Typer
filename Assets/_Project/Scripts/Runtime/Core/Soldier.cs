using UnityEngine;

// An ally you spawn with coins. It walks to the nearest enemy and defeats it on
// contact (full death juice + coins). Despawns after maxKills or lifeTime so it
// isn't a permanent god-unit. All values are editable on the prefab.
public class Soldier : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 3f;
    public float attackRange = 0.8f;

    [Header("Life")]
    public float lifeTime = 12f; // seconds before it leaves the field
    public int maxKills = 5;     // retires after this many kills

    private float age;
    private int kills;

    void Update()
    {
        age += Time.deltaTime;
        if (age >= lifeTime) { Destroy(gameObject); return; }

        Enemy nearest = FindNearestEnemy();

        if (nearest != null)
        {
            // March toward the nearest enemy (stay on the ground plane)
            Vector3 dir = nearest.transform.position - transform.position;
            dir.y = 0f;
            transform.position += dir.normalized * moveSpeed * Time.deltaTime;

            if (Vector3.Distance(transform.position, nearest.transform.position) <= attackRange)
            {
                nearest.Defeat();
                kills++;
                if (kills >= maxKills) { Destroy(gameObject); return; }
            }
        }
        else
        {
            // Nothing to fight: advance up the field
            transform.position += Vector3.forward * moveSpeed * Time.deltaTime;
        }
    }

    Enemy FindNearestEnemy()
    {
        Enemy best = null;
        float bestD = Mathf.Infinity;
        foreach (Enemy e in Enemy.Active)
        {
            if (e.IsDefeated) continue;
            float d = Vector3.Distance(transform.position, e.transform.position);
            if (d < bestD) { bestD = d; best = e; }
        }
        return best;
    }
}