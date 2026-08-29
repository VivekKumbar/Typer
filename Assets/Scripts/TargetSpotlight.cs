using UnityEngine;

// A single spotlight that smoothly follows whichever enemy the player is
// currently typing, and turns RED as that enemy nears the tower.
public class TargetSpotlight : MonoBehaviour
{
    [Header("Refs")]
    public Light spotLight;
    [Tooltip("The fortress — used to measure how close the target is.")]
    public Transform fortress;

    [Header("Position")]
    public float height = 8f;
    public float followSpeed = 6f;

    [Header("Intensity")]
    public float activeIntensity = 12f;
    public float idleIntensity = 0f;
    public float fadeSpeed = 6f;

    [Header("Danger colour")]
    public Color safeColor = new Color(1f, 0.95f, 0.85f);   // warm white
    public Color dangerColor = Color.red;
    [Tooltip("Distance from the tower where the light is fully SAFE coloured.")]
    public float safeDistance = 10f;
    [Tooltip("Distance from the tower where the light is fully RED.")]
    public float dangerDistance = 3f;
    [Tooltip("Extra brightness boost when in full danger.")]
    public float dangerIntensityBoost = 6f;

    [Header("Danger pulse")]
    public bool pulseWhenClose = true;
    public float pulseSpeed = 6f;
    public float pulseAmount = 3f;

    [Header("Look")]
    public float activeSpotAngle = 30f;
    public bool tightenOnProgress = true;
    public float minSpotAngle = 18f;

    private Vector3 lastTargetPos;

    void Awake()
    {
        if (spotLight == null) spotLight = GetComponentInChildren<Light>();
        if (spotLight != null)
        {
            spotLight.type = LightType.Spot;
            spotLight.intensity = idleIntensity;
            spotLight.color = safeColor;
        }
        lastTargetPos = transform.position;
    }

    void LateUpdate()
    {
        if (spotLight == null) return;

        Enemy target = TypingController.Instance != null
                     ? TypingController.Instance.CurrentTarget
                     : null;

        bool hasTarget = target != null && !target.IsDefeated;
        if (hasTarget) lastTargetPos = target.transform.position;

        // Glide above the target
        Vector3 want = lastTargetPos + Vector3.up * height;
        transform.position = Vector3.Lerp(transform.position, want, followSpeed * Time.deltaTime);
        transform.rotation = Quaternion.LookRotation(Vector3.down);

        // --- DANGER: how close is the target to the tower? ---
        float danger = 0f;   // 0 = safe, 1 = right on top of us
        if (hasTarget && fortress != null)
        {
            float dist = Vector3.Distance(target.transform.position, fortress.position);
            danger = Mathf.InverseLerp(safeDistance, dangerDistance, dist); // closer -> 1
            danger = Mathf.Clamp01(danger);
        }

        // Colour shifts toward red as danger rises
        spotLight.color = Color.Lerp(safeColor, dangerColor, danger);

        // Brightness rises with danger, plus an optional heartbeat pulse
        float targetIntensity = hasTarget
            ? activeIntensity + dangerIntensityBoost * danger
            : idleIntensity;

        if (pulseWhenClose && hasTarget && danger > 0.5f)
            targetIntensity += Mathf.Sin(Time.time * pulseSpeed) * pulseAmount * danger;

        spotLight.intensity = Mathf.Lerp(spotLight.intensity, targetIntensity, fadeSpeed * Time.deltaTime);

        // Cone tightens as the word nears completion
        if (tightenOnProgress && hasTarget && !string.IsNullOrEmpty(target.Word))
        {
            float progress = (float)target.TypedCount / target.Word.Length;
            spotLight.spotAngle = Mathf.Lerp(activeSpotAngle, minSpotAngle, progress);
        }
        else spotLight.spotAngle = activeSpotAngle;
    }
}