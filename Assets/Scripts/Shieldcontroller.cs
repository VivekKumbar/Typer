using System.Collections;
using UnityEngine;

// Drives your BubbleShieldShader from the game (replaces the test-input Shield.cs).
// - Rises up (dissolve 1 -> 0) when bought
// - Absorbs fortress damage via ShieldManager
// - Ripples at the hit point + flashes red on each hit
// - Sinks down (dissolve 0 -> 1) when depleted
// Put this on the shield sphere (same object as its Renderer).
public class ShieldController : MonoBehaviour
{
    [Header("Shader property names")]
    public string dissolveProp = "_Disolve";                 // 0 = up, 1 = gone
    public string hitPosProp = "_HitPos";
    public string displacementProp = "_DisplacementStrength";
    [Tooltip("Which color property to flash red on hit (your main shield colour).")]
    public string colorProp = "FresnelColor";

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

    private Renderer rend;
    private Material mat;
    private int dissolveId, hitPosId, dispId, colorId;
    private Color baseColor;
    private Coroutine dissolveCo, rippleCo, flashCo;

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
        colorId = Shader.PropertyToID(colorProp);

        baseColor = mat.GetColor(colorId);

        // Start fully DOWN (dissolved away)
        mat.SetFloat(dissolveId, 1f);
        rend.enabled = false;
    }

    private bool subscribed;

    void Start()
    {
        Subscribe();
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
            subscribed = true;
        }
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
        subscribed = false;
    }

    // ---- rise / sink ----
    void RaiseShield()
    {
        if (rend != null) rend.enabled = true;
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
        mat.SetColor(colorId, hitColor);
        float t = 0f;
        while (t < flashTime)
        {
            t += Time.deltaTime;
            mat.SetColor(colorId, Color.Lerp(hitColor, baseColor, t / flashTime));
            yield return null;
        }
        mat.SetColor(colorId, baseColor);
    }
}