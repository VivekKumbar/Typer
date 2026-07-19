using UnityEngine;

// Spawns a homing bullet aimed at a specific enemy.
public class Tower : MonoBehaviour
{
    [Header("Refs")]
    public Bullet bulletPrefab;
    public Transform firePoint;

    [Header("Juice")]
    public GameObject muzzleFlash; // optional particle at the fire point

    public void FireAt(Enemy enemy)
    {
        if (bulletPrefab == null || enemy == null) return;

        Vector3 origin = firePoint ? firePoint.position : transform.position;
        Bullet b = Instantiate(bulletPrefab, origin, Quaternion.identity);
        b.SetTarget(enemy);

        if (muzzleFlash != null)
            Instantiate(muzzleFlash, origin, Quaternion.identity);
    }
}