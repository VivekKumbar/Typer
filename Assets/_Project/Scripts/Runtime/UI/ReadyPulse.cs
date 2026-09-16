using UnityEngine;
using UnityEngine.UI;

// Drop on any UI button to give it a gentle "ready" breathing pulse — scale,
// and optionally color/glow — that loops while the ability is usable. Purely
// visual: doesn't touch interactable state or ability logic, and doesn't know
// what ability it belongs to. Whichever HUD script already owns the ability's
// ready/not-ready logic just calls SetActive(bool) alongside whatever else it
// does when readiness changes.
public class ReadyPulse : MonoBehaviour
{
    [Header("Master control")]
    [Tooltip("Uncheck to disable the pulse entirely; the button stays at rest.")]
    public bool enablePulse = true;

    [Header("Scale breathing")]
    [Tooltip("Breaths per second, roughly. Keep this slow (~1-1.5) for a calm, inviting breath rather than an alarm-y blink.")]
    [Range(0.1f, 4f)] public float pulseSpeed = 1.2f;
    [Tooltip("How much the button grows at the peak of each breath. 0.07 = 7% bigger.")]
    [Range(0f, 0.5f)] public float scaleAmount = 0.07f;

    [Header("Pulse shape")]
    [Tooltip("Remaps the underlying sine breath (X = raw sine progress 0..1, Y = pulse strength actually applied 0..1). A straight diagonal line keeps a pure sine breath; bend it for a sharper peak or a lazier settle.")]
    public AnimationCurve pulseCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Color pulse (optional)")]
    [Tooltip("Also pulse an Image's color between Base Color and Highlight Color.")]
    public bool useColorPulse = false;
    [Tooltip("Image to tint. Defaults to this object's own Image if left empty.")]
    public Image colorTarget;
    [Tooltip("Color at rest (0% pulse strength).")]
    public Color baseColor = Color.white;
    [Tooltip("Color at the peak of each breath (100% pulse strength).")]
    public Color highlightColor = new Color(1f, 0.85f, 0.4f);

    [Header("Glow (optional)")]
    [Tooltip("A separate halo/glow Image behind the button. Its alpha pulses between Glow Min/Max Alpha. Leave empty to skip.")]
    public Image glowImage;
    [Tooltip("Glow alpha at rest (0% pulse strength).")]
    [Range(0f, 1f)] public float glowMinAlpha = 0f;
    [Tooltip("Glow alpha at the peak of each breath (100% pulse strength).")]
    [Range(0f, 1f)] public float glowMaxAlpha = 0.6f;

    [Header("Settle (when SetActive(false) is called)")]
    [Tooltip("How quickly scale/color/glow ease back to rest once stopped. Higher = snaps back faster, lower = lingers longer.")]
    [Range(0.5f, 20f)] public float returnSpeed = 6f;

    private bool pulsing;
    private float phase;    // 0..1 position through the current breath cycle
    private float strength; // 0..1 pulse strength currently applied
    private Vector3 baseScale;

    void Awake()
    {
        baseScale = transform.localScale;
        if (useColorPulse && colorTarget == null)
            colorTarget = GetComponent<Image>();
    }

    // Call this from whichever script already knows the ability's ready state
    // — the same place it currently enables/disables the button's interactable
    // flag or calls a highlight component. true = start breathing, false = ease
    // back to rest (charging, on cooldown, or just used).
    public void SetActive(bool on)
    {
        pulsing = on && enablePulse;
        if (on) phase = 0f; // fresh breath-in each time it becomes ready
    }

    void Update()
    {
        if (!enablePulse)
        {
            ApplyStrength(0f);
            return;
        }

        if (pulsing)
        {
            strength = SineStrength();
        }
        else if (strength > 0f)
        {
            // Ease current strength down to 0 instead of snapping.
            strength = Mathf.Lerp(strength, 0f, 1f - Mathf.Exp(-returnSpeed * Time.unscaledDeltaTime));
            if (strength < 0.001f) strength = 0f;
        }

        ApplyStrength(strength);
    }

    float SineStrength()
    {
        phase += pulseSpeed * Time.unscaledDeltaTime; // unscaled: keeps breathing through pause/slow-mo
        phase %= 1f;
        float sine01 = 0.5f * (1f - Mathf.Cos(phase * Mathf.PI * 2f)); // smooth 0 -> 1 -> 0, one breath per cycle
        return Mathf.Clamp01(pulseCurve.Evaluate(sine01));
    }

    void ApplyStrength(float s)
    {
        transform.localScale = baseScale * (1f + scaleAmount * s);

        if (useColorPulse && colorTarget != null)
            colorTarget.color = Color.Lerp(baseColor, highlightColor, s);

        if (glowImage != null)
        {
            Color c = glowImage.color;
            c.a = Mathf.Lerp(glowMinAlpha, glowMaxAlpha, s);
            glowImage.color = c;
        }
    }

    void OnDisable()
    {
        // Don't leave the button stuck mid-pulse if its panel gets hidden/shown.
        pulsing = false;
        strength = 0f;
        transform.localScale = baseScale;
        if (useColorPulse && colorTarget != null) colorTarget.color = baseColor;
        if (glowImage != null)
        {
            Color c = glowImage.color;
            c.a = glowMinAlpha;
            glowImage.color = c;
        }
    }
}
