using UnityEngine;
using UnityEngine.UI;

// A slider that shows shield amount. Sits under the health bar. Hides itself
// when there's no shield.
public class ShieldBar : MonoBehaviour
{
    public Slider bar;
    [Tooltip("Optional LoadingBarUI for smooth filling like the loading bar.")]
    public LoadingBarUI loadingBar;
    [Tooltip("Optional legacy root toggle. If null, the bar frame stays visible with 0 fill.")]
    public GameObject barRoot;

    void Awake()
    {
        if (bar == null) bar = GetComponent<Slider>();
        if (loadingBar == null) loadingBar = GetComponent<LoadingBarUI>();
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