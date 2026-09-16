using UnityEngine;

// Rotates the turret's movable part to aim at the enemy the player is currently
// typing, and fires bullets from a muzzle point. Put this on the turret ROOT
// (e.g. "Turret 1a") and assign the Pylon as the rotating part.
public class TurretAim : MonoBehaviour
{
    [Header("Parts")]
    [Tooltip("The part that rotates to aim — your 'Pylon'.")]
    public Transform pylon;
    [Tooltip("Empty child at the barrel tip where bullets spawn.")]
    public Transform muzzle;

    [Header("Aiming")]
    public float turnSpeed = 8f;
    [Tooltip("Only rotate horizontally (recommended for a top-down game).")]
    public bool yAxisOnly = true;
    [Tooltip("Fix if the model aims sideways. Try 90, -90 or 180.")]
    public float yawOffset = 0f;

    [Header("Idle")]
    [Tooltip("Where it points when there's no target. Leave empty to hold last aim.")]
    public Transform idleLookTarget;

    private Enemy currentTarget;

    void LateUpdate()
    {
        // The enemy the player is currently typing
        currentTarget = TypingController.Instance != null
                      ? TypingController.Instance.CurrentTarget
                      : null;

        Vector3 lookPoint;

        if (currentTarget != null && !currentTarget.IsDefeated)
            lookPoint = currentTarget.transform.position;
        else if (idleLookTarget != null)
            lookPoint = idleLookTarget.position;
        else
            return; // nothing to aim at, hold current rotation

        AimAt(lookPoint);
    }

    void AimAt(Vector3 worldPoint)
    {
        if (pylon == null) return;

        Vector3 dir = worldPoint - pylon.position;
        if (yAxisOnly) dir.y = 0f;              // stay level; don't tilt the turret
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion want = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, yawOffset, 0f);
        pylon.rotation = Quaternion.Slerp(pylon.rotation, want, turnSpeed * Time.deltaTime);
    }

    // Called by the Tower when it fires — gives the bullet spawn point.
    public Transform GetMuzzle() { return muzzle != null ? muzzle : pylon; }

    // True when the turret is roughly pointing at the target (optional gating).
    public bool IsAimedAt(Transform target, float toleranceDegrees = 15f)
    {
        if (pylon == null || target == null) return true;
        Vector3 dir = target.position - pylon.position;
        if (yAxisOnly) dir.y = 0f;
        float angle = Vector3.Angle(pylon.forward, dir);
        return angle <= toleranceDegrees;
    }
}