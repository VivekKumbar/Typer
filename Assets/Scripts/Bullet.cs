using UnityEngine;

// Homing projectile. Flashes the enemy red on impact and spawns a small smoke
// puff at the hit point, oriented to kick back away from the bullet's travel
// direction so it reads as a real impact.
// Set debugLog = true to print each step to the Console while troubleshooting.
public class Bullet : MonoBehaviour
{
    [Header("Flight")]
    public float speed = 25f;
    public float hitDistance = 0.4f;
    public float lifeTime = 1.0f;
    public bool faceTravelDirection = true;

    [Header("Impact")]
    [Tooltip("Optional generic impact effect (sparks, flash, etc).")]
    public GameObject impactEffect;
    [Tooltip("A small smoke puff particle prefab, spawned at the hit point on every impact.")]
    public GameObject smokePuffEffect;
    [Tooltip("How long the smoke puff lasts before being cleaned up (safety, in case the prefab has no self-destroy).")]
    public float smokeLifetime = 1.5f;

    [Header("Debug")]
    public bool debugLog = false;

    private Enemy target;
    private Vector3 lastKnownPos;
    private float age;

    public void SetTarget(Enemy enemy)
    {
        target = enemy;
        if (enemy != null) lastKnownPos = enemy.transform.position;
        if (debugLog) Debug.Log("[Bullet] fired at " + (enemy ? enemy.name : "NULL"), this);
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= lifeTime) { if (debugLog) Debug.Log("[Bullet] expired without hitting"); Destroy(gameObject); return; }

        Vector3 aim = (target != null && !target.IsDefeated) ? target.transform.position : lastKnownPos;
        if (target != null && !target.IsDefeated) lastKnownPos = aim;

        Vector3 dir = aim - transform.position;
        if (faceTravelDirection && dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir);

        transform.position = Vector3.MoveTowards(transform.position, aim, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, aim) <= hitDistance)
        {
            Hit();
            Destroy(gameObject);
        }
    }

    void Hit()
    {
        // The direction the bullet was travelling when it landed — used to
        // orient the smoke puff so it kicks back away from the impact.
        Vector3 travelDir = transform.forward;

        if (impactEffect != null)
            Instantiate(impactEffect, transform.position, Quaternion.identity);

        if (smokePuffEffect != null)
        {
            // Face the puff opposite the bullet's travel direction so it looks
            // like it's kicking back off the enemy, not just appearing flat.
            Quaternion puffRotation = travelDir.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(-travelDir)
                : Quaternion.identity;

            GameObject puff = Instantiate(smokePuffEffect, transform.position, puffRotation);
            if (smokeLifetime > 0f) Destroy(puff, smokeLifetime);
        }

        if (target != null && !target.IsDefeated)
        {
            EnemyHitFlash flash = target.GetComponent<EnemyHitFlash>();
            if (debugLog) Debug.Log("[Bullet] HIT " + target.name + " | flash component: " + (flash != null), this);
            if (flash != null) flash.Flash();
        }
        else if (debugLog) Debug.Log("[Bullet] reached target point but enemy is gone/defeated");
    }
}