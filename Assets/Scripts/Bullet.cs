using UnityEngine;

// Pure visual tracer: flies to a captured position then disappears.
// Decoupled from the enemy so there is no race condition if the enemy dies.
public class Bullet : MonoBehaviour
{
    public float speed = 18f;
    private Vector3 target;
    private bool hasTarget;

    public void SetTarget(Vector3 worldTarget)
    {
        target = worldTarget;
        hasTarget = true;
    }

    void Update()
    {
        if (!hasTarget) return;
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        if (Vector3.Distance(transform.position, target) < 0.05f)
            Destroy(gameObject);
    }
}
