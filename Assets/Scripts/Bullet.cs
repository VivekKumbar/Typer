using UnityEngine;

// Homing projectile. Flashes the enemy red on impact.
// Set debugLog = true to print each step to the Console while troubleshooting.
public class Bullet : MonoBehaviour
{
    [Header("Flight")]
    public float speed = 25f;          // lowered so you can SEE it travel
    public float hitDistance = 0.4f;
    public float lifeTime = 1.0f;
    public bool faceTravelDirection = true;

    [Header("Impact")]
    public GameObject impactEffect;

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
        if (impactEffect != null)
            Instantiate(impactEffect, transform.position, Quaternion.identity);

        if (target != null && !target.IsDefeated)
        {
            EnemyHitFlash flash = target.GetComponent<EnemyHitFlash>();
            if (debugLog) Debug.Log("[Bullet] HIT " + target.name + " | flash component: " + (flash != null), this);
            if (flash != null) flash.Flash();
        }
        else if (debugLog) Debug.Log("[Bullet] reached target point but enemy is gone/defeated");
    }
}