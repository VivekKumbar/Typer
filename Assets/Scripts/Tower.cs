using System.Collections.Generic;
using UnityEngine;

// Fires bullets from the turret's muzzle, but only once the turret is actually
// aimed at the target. Shots requested while still traversing are QUEUED and
// released the moment the barrel lines up, so no keystroke is ever wasted.
public class Tower : MonoBehaviour
{
    [Header("Refs")]
    public Bullet bulletPrefab;
    public Transform firePoint;
    [Tooltip("Optional: if set, bullets spawn from the turret's muzzle and wait for aim.")]
    public TurretAim turret;

    [Header("Aim gating")]
    [Tooltip("How close the barrel must be to the target, in degrees, before firing.")]
    public float aimTolerance = 12f;
    [Tooltip("Safety: fire anyway if a shot has been queued this long.")]
    public float maxQueueTime = 0.5f;
    [Tooltip("Minimum seconds between queued shots so they don't all burst at once.")]
    public float shotSpacing = 0.05f;

    [Header("Juice")]
    public GameObject muzzleFlash;

    // A shot waiting for the turret to line up
    private class PendingShot
    {
        public Enemy target;
        public float queuedAt;
    }

    private readonly List<PendingShot> pending = new List<PendingShot>();
    private float lastShotTime;

    public void FireAt(Enemy enemy)
    {
        if (bulletPrefab == null || enemy == null) return;

        // No turret assigned -> old behaviour, fire immediately
        if (turret == null) { Shoot(enemy); return; }

        pending.Add(new PendingShot { target = enemy, queuedAt = Time.time });
    }

    void Update()
    {
        if (pending.Count == 0) return;

        for (int i = pending.Count - 1; i >= 0; i--)
        {
            PendingShot s = pending[i];

            // Target died while we were traversing -> drop the shot
            if (s.target == null || s.target.IsDefeated)
            {
                pending.RemoveAt(i);
                continue;
            }

            bool aimed = turret.IsAimedAt(s.target.transform, aimTolerance);
            bool waitedTooLong = Time.time - s.queuedAt >= maxQueueTime;
            bool spaced = Time.time - lastShotTime >= shotSpacing;

            if ((aimed || waitedTooLong) && spaced)
            {
                Shoot(s.target);
                pending.RemoveAt(i);
            }
        }
    }

    void Shoot(Enemy enemy)
    {
        Transform spawn = (turret != null) ? turret.GetMuzzle()
                        : (firePoint != null ? firePoint : transform);

        Bullet b = Instantiate(bulletPrefab, spawn.position, spawn.rotation);
        b.SetTarget(enemy);

        if (muzzleFlash != null)
            Instantiate(muzzleFlash, spawn.position, spawn.rotation);

        lastShotTime = Time.time;
    }
}