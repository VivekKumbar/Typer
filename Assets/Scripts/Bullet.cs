using UnityEngine;

// A physical projectile that homes on its enemy, flashes it red on impact,
// and cleans itself up. Cannot linger (hard lifetime failsafe).
public class Bullet : MonoBehaviour
{
    [Header("Flight")]
    public float speed = 40f;
    public float hitDistance = 0.35f;
    public float lifeTime = 0.6f;      // failsafe so it can never hang around
    [Tooltip("Rotate the bullet to face where it's flying.")]
    public bool faceTravelDirection = true;

    [Header("Impact")]
    public GameObject impactEffect;    // optional particle prefab

    private Enemy target;
    private Vector3 lastKnownPos;
    private float age;

    public void SetTarget(Enemy enemy)
    {
        target = enemy;
        if (enemy != null) lastKnownPos = enemy.transform.position;
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= lifeTime) { Destroy(gameObject); return; }

        // Aim at the enemy if it's alive, otherwise finish the trip to where it was
        Vector3 aim = (target != null && !target.IsDefeated)
                      ? target.transform.position
                      : lastKnownPos;

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
            if (flash != null) flash.Flash();
        }
    }
}
