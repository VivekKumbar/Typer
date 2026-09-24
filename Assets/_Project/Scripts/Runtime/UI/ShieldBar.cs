using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// A slider that shows shield amount. Sits under the health bar.
// Also allows clicking on the bar itself to trigger shield buy with tactile punch feedback.
public class ShieldBar : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    public Slider bar;
    [Tooltip("Optional LoadingBarUI for smooth filling like the loading bar.")]
    public LoadingBarUI loadingBar;
    [Tooltip("Optional legacy root toggle. If null, the bar frame stays visible with 0 fill.")]
    public GameObject barRoot;

    private Coroutine punchRoutine;
    private Vector3 initialScale = Vector3.one;
    private bool hasInitialScale = false;

    void Awake()
    {
        if (bar == null) bar = GetComponent<Slider>();
        if (loadingBar == null) loadingBar = GetComponent<LoadingBarUI>();

        if (!hasInitialScale)
        {
            initialScale = transform.localScale;
            hasInitialScale = true;
        }

        // Ensure child graphics can catch clicks
        foreach (var g in GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = true;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        TriggerPunch();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        BuyShield();
    }

    public void TriggerPunch()
    {
        if (!hasInitialScale)
        {
            initialScale = transform.localScale;
            hasInitialScale = true;
        }
        if (punchRoutine != null) StopCoroutine(punchRoutine);
        punchRoutine = StartCoroutine(DoPunch());
    }

    private IEnumerator DoPunch()
    {
        float dur = 0.16f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / dur;
            float s = Mathf.Lerp(0.92f, 1f, t);
            transform.localScale = initialScale * s;
            yield return null;
        }
        transform.localScale = initialScale;
        punchRoutine = null;
    }

    public void BuyShield()
    {
        TriggerPunch();
        var sb = FindAnyObjectByType<ShieldButton>();
        if (sb != null) sb.Buy();
        else if (ShieldManager.Instance != null) ShieldManager.Instance.TryRaiseShield();
    }

    void Start()
    {
        var sm = ShieldManager.Instance;
        if (sm != null)
        {
            sm.OnShieldChanged += UpdateBar;
            float frac = sm.shieldMax > 0 ? (float)sm.Current / sm.shieldMax : 0f;
            if (loadingBar != null) loadingBar.SnapTo01(frac);
            else if (bar != null) bar.value = frac;
            if (barRoot != null) barRoot.SetActive(sm.Current > 0);
        }
    }

    void OnDestroy()
    {
        if (ShieldManager.Instance != null)
            ShieldManager.Instance.OnShieldChanged -= UpdateBar;
    }

    void UpdateBar(int cur, int max)
    {
        float frac = max > 0 ? (float)cur / max : 0f;
        if (loadingBar != null)
        {
            loadingBar.SetTargetProgress01(frac);
        }
        else if (bar != null)
        {
            bar.minValue = 0f;
            bar.maxValue = 1f;
            bar.value = frac;
        }

        if (barRoot != null) barRoot.SetActive(cur > 0);
    }
}