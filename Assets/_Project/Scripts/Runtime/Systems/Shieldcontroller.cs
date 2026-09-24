using System.Collections;
using UnityEngine;

// Drives your BubbleShieldShader from the game (replaces the test-input Shield.cs).
// - Rises up (dissolve 1 -> 0) when bought
// - Absorbs fortress damage via ShieldManager
// - Ripples at the hit point + flashes red on each hit
// - Sinks down (dissolve 0 -> 1) when depleted
// - Automatically dims shield color in Night mode so it doesn't over-bloom against the dark background.
// Put this on the shield sphere (same object as its Renderer).
public class ShieldController : MonoBehaviour
{
    [Header("Shader property names")]
    public string dissolveProp = "_Disolve";                 // 0 = up, 1 = gone
    public string hitPosProp = "_HitPos";
    public string displacementProp = "_DisplacementStrength";
    [Tooltip("Which color property to flash red on hit (your main shield colour). Must be the shader's REFERENCE name, not its display name — Shader Graph only uses a clean reference name if one was explicitly set on the Blackboard property; otherwise it auto-generates a GUID-suffixed one. BubbleShieldShader's Fresnel Color property was never given a clean reference, so this has to be its generated name.")]
    public string colorProp = "Color_cf12b49411d94583a269f83e6981abd1"; // Shader Graph display name "FresnelColor"

    [Header("Rise / sink")]
    [Tooltip("Seconds for the shield to rise up or sink down.")]
    public float dissolveSpeed = 2.5f;

    [Header("Hit ripple")]
    public AnimationCurve displacementCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    public float displacementMagnitude = 1f;
    public float rippleSpeed = 3f;

    [Header("Hit flash (red)")]
    public Color hitColor = Color.red;
    public float flashTime = 0.15f;

    [Header("Day / Night visual tuning")]
    [Tooltip("Multiplier applied to the shield color at night to compensate for low ambient/directional scene lighting.")]
    [Range(1f, 50f)]
    public float nightMultiplier = 25f;
    [Tooltip("Fresnel power at night. Lower values (e.g. 2.8 - 3.2) broaden the rim glow so the shield doesn't look like a dark void at night. Set to 0 to keep day power (4.34).")]
    public float nightFresnelPower = 2.8f;
    [Tooltip("Optional direct override for shield color at night. If alpha > 0, this is used instead of dayColor * nightMultiplier.")]
    public Color nightColorOverride = Color.clear;

    private Renderer rend;
    private Material mat;
    private int dissolveId, hitPosId, dispId, colorId, edgeColorId, fresnelPowerId;
    private Color dayBaseColor;
    private Color dayEdgeColor;
    private float dayFresnelPower = 4.34f;
    private Coroutine dissolveCo, rippleCo, flashCo;
    private bool subscribed;
    private bool lastNightState;

    private const string EDGE_COLOR_PROP = "Color_027e4586f058443ca29389a6ccbed930";
    private const string FRESNEL_POWER_PROP = "Vector1_cf86a053aa2c40e78a605292021f44c3";

    void Awake()
    {
        rend = GetComponent<Renderer>();
        if (rend == null) rend = GetComponentInChildren<Renderer>();
        if (rend == null)
        {
            Debug.LogError("[ShieldController] No Renderer found on " + name +
                           " or its children. Put this on the shield mesh.", this);
            enabled = false;
            return;
        }
        mat = rend.material; // instance

        dissolveId = Shader.PropertyToID(dissolveProp);
        hitPosId = Shader.PropertyToID(hitPosProp);
        dispId = Shader.PropertyToID(displacementProp);

        // Fallback for legacy scenes/prefabs that stored the display name "FresnelColor"
        if (string.IsNullOrEmpty(colorProp) || colorProp == "FresnelColor" || !mat.HasProperty(colorProp))
        {
            colorProp = "Color_cf12b49411d94583a269f83e6981abd1";
        }
        colorId = Shader.PropertyToID(colorProp);
        edgeColorId = Shader.PropertyToID(EDGE_COLOR_PROP);
        fresnelPowerId = Shader.PropertyToID(FRESNEL_POWER_PROP);

        if (mat.HasProperty(colorId))
            dayBaseColor = mat.GetColor(colorId);
        else
            dayBaseColor = new Color(0f, 2.0847f, 6.4222f, 1f);

        // If returned black/transparent from property lookup, fallback to standard day color
        if (dayBaseColor.r <= 0.001f && dayBaseColor.g <= 0.001f && dayBaseColor.b <= 0.001f)
        {
            dayBaseColor = new Color(0f, 2.0847f, 6.4222f, 1f);
        }

        if (mat.HasProperty(edgeColorId))
            dayEdgeColor = mat.GetColor(edgeColorId);
        else
            dayEdgeColor = new Color(0f, 2.7571898f, 12.844469f, 0f);

        if (mat.HasProperty(fresnelPowerId))
            dayFresnelPower = mat.GetFloat(fresnelPowerId);

        // Start fully DOWN (dissolved away)
        mat.SetFloat(dissolveId, 1f);
        rend.enabled = false;

        lastNightState = IsNightMode();
        ApplyPhaseColors(lastNightState);
    }

    void Start()
    {
        Subscribe();
        ApplyPhaseColors(IsNightMode());

        var sm = ShieldManager.Instance;
        if (sm != null && sm.IsActive)
            RaiseShield();
    }

    void Update()
    {
        bool night = IsNightMode();
        if (night != lastNightState)
        {
            lastNightState = night;
            ApplyPhaseColors(night);
        }
    }

    public bool IsNightMode()
    {
        return DarkMode.Enabled || (DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight);
    }

    public Color GetCurrentBaseColor()
    {
        if (IsNightMode())
        {
            if (nightColorOverride.a > 0f) return nightColorOverride;
            return dayBaseColor * nightMultiplier;
        }
        return dayBaseColor;
    }

    public void ApplyPhaseColors(bool isNight)
    {
        if (mat == null) return;
        Color c = isNight ? (nightColorOverride.a > 0f ? nightColorOverride : dayBaseColor * nightMultiplier) : dayBaseColor;
        if (mat.HasProperty(colorId))
        {
            mat.SetColor(colorId, c);
        }
        if (mat.HasProperty(edgeColorId))
        {
            Color edge = isNight ? (dayEdgeColor * nightMultiplier) : dayEdgeColor;
            mat.SetColor(edgeColorId, edge);
        }
        if (mat.HasProperty(fresnelPowerId))
        {
            float pow = (isNight && nightFresnelPower > 0f) ? nightFresnelPower : dayFresnelPower;
            mat.SetFloat(fresnelPowerId, pow);
        }
    }

    void Subscribe()
    {
        if (subscribed) return;
        var sm = ShieldManager.Instance;
        if (sm != null)
        {
            sm.OnShieldRaised += RaiseShield;
            sm.OnShieldBroken += SinkShield;
            sm.OnShieldChanged += OnShieldChanged;
        }
        var dn = DayNightCycle.Instance;
        if (dn != null)
        {
            dn.OnPhaseChanged += OnPhaseChanged;
        }
        subscribed = true;
    }

    void OnDisable()
    {
        if (!subscribed) return;
        var sm = ShieldManager.Instance;
        if (sm != null)
        {
            sm.OnShieldRaised -= RaiseShield;
            sm.OnShieldBroken -= SinkShield;
            sm.OnShieldChanged -= OnShieldChanged;
        }
        var dn = DayNightCycle.Instance;
        if (dn != null)
        {
            dn.OnPhaseChanged -= OnPhaseChanged;
        }
        subscribed = false;
    }

    void OnPhaseChanged(bool isNight)
    {
        lastNightState = isNight;
        ApplyPhaseColors(isNight);
    }

    // ---- rise / sink ----
    void RaiseShield()
    {
        if (rend != null) rend.enabled = true;
        ApplyPhaseColors(IsNightMode());
        if (dissolveCo != null) StopCoroutine(dissolveCo);
        dissolveCo = StartCoroutine(DissolveTo(0f, false)); // 0 = fully up
    }

    void SinkShield()
    {
        if (dissolveCo != null) StopCoroutine(dissolveCo);
        dissolveCo = StartCoroutine(DissolveTo(1f, true));  // 1 = gone, then hide
    }

    IEnumerator DissolveTo(float target, bool hideAtEnd)
    {
        if (mat == null) yield break;
        float start = mat.GetFloat(dissolveId);
        float lerp = 0f;
        while (lerp < 1f)
        {
            lerp += Time.deltaTime * dissolveSpeed;
            mat.SetFloat(dissolveId, Mathf.Lerp(start, target, lerp));
            yield return null;
        }
        mat.SetFloat(dissolveId, target);
        if (hideAtEnd) rend.enabled = false;
    }

    // ---- hit feedback ----
    private int lastValue = -1;
    void OnShieldChanged(int cur, int max)
    {
        if (cur < lastValue && cur > 0)
        {
            // took a hit while still up
            HitAt(transform.position); // fallback point; overridden below if we have contact
            Flash();
        }
        lastValue = cur;
    }

    // Call this with the enemy's position for a ripple at the exact contact point.
    public void HitAt(Vector3 worldPos)
    {
        if (mat == null) return;
        mat.SetVector(hitPosId, worldPos);
        if (rippleCo != null) StopCoroutine(rippleCo);
        rippleCo = StartCoroutine(Ripple());
    }

    IEnumerator Ripple()
    {
        if (mat == null) yield break;
        float lerp = 0f;
        while (lerp < 1f)
        {
            mat.SetFloat(dispId, displacementCurve.Evaluate(lerp) * displacementMagnitude);
            lerp += Time.deltaTime * rippleSpeed;
            yield return null;
        }
        mat.SetFloat(dispId, 0f);
    }

    void Flash()
    {
        if (flashCo != null) StopCoroutine(flashCo);
        flashCo = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        if (mat == null) yield break;
        Color currentHitColor = IsNightMode() ? hitColor * Mathf.Min(nightMultiplier, 10f) : hitColor;
        mat.SetColor(colorId, currentHitColor);
        float t = 0f;
        Color targetColor = GetCurrentBaseColor();
        while (t < flashTime)
        {
            t += Time.deltaTime;
            mat.SetColor(colorId, Color.Lerp(currentHitColor, targetColor, t / flashTime));
            yield return null;
        }
        mat.SetColor(colorId, targetColor);
    }
}