using UnityEngine;

// Spawns a homing bullet aimed at a specific enemy.
public class Tower : MonoBehaviour
{
    [Header("Refs")]
    public Bullet bulletPrefab;
    public Transform firePoint;

    public void FireAt(Transform enemy)
    {
        if (bulletPrefab == null || enemy == null) return;
        Vector3 origin = firePoint ? firePoint.position : transform.position;
        Bullet b = Instantiate(bulletPrefab, origin, Quaternion.identity);
        b.SetTarget(enemy);
    }
}