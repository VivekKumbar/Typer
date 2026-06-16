using UnityEngine;

// Homes on the enemy it was fired at. Three independent ways it gets cleaned up
// so it can NEVER linger on screen:
//   1) reaches the enemy,
//   2) its target enemy is gone (null),
//   3) a hard lifetime cap (failsafe).
public class Bullet : MonoBehaviour
{
    public float speed = 40f;
    public float lifeTime = 0.5f; // failsafe: destroyed after this no matter what
    private Transform target;
    private float age;

    public void SetTarget(Transform enemy) { target = enemy; }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= lifeTime) { Destroy(gameObject); return; }   // 3) failsafe
        if (target == null) { Destroy(gameObject); return; }   // 2) enemy gone

        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);
        if (Vector3.Distance(transform.position, target.position) < 0.3f)
            Destroy(gameObject);                                 // 1) reached it
    }
}