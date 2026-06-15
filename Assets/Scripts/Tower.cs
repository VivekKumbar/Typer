using UnityEngine;

// The fortress turret. Spawns a visual bullet toward a target position.
public class Tower : MonoBehaviour
{
    [Header("Refs")]
    public Bullet bulletPrefab;
    public Transform firePoint; // empty child where bullets spawn

    public void FireAt(Vector3 worldTarget)
    {
        if (bulletPrefab == null) return;
        Vector3 origin = firePoint ? firePoint.position : transform.position;
        Bullet b = Instantiate(bulletPrefab, origin, Quaternion.identity);
        b.SetTarget(worldTarget);
    }
}
