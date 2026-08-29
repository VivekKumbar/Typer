using UnityEngine;
using UnityEngine.UI;

// A slider that shows shield amount. Sits under the health bar. Hides itself
// when there's no shield.
public class ShieldBar : MonoBehaviour
{
    public Slider bar;
    [Tooltip("Hide the whole bar object when the shield is down.")]
    public GameObject barRoot;

    void Start()
    {
        var sm = ShieldManager.Instance;
        if (sm != null)
        {
            sm.OnShieldChanged += UpdateBar;
            UpdateBar(sm.Current, sm.shieldMax);
        }
    }

    void OnDestroy()
    {
        if (ShieldManager.Instance != null)
            ShieldManager.Instance.OnShieldChanged -= UpdateBar;
    }

    void UpdateBar(int cur, int max)
    {
        if (bar) { bar.maxValue = max; bar.value = cur; }
        if (barRoot) barRoot.SetActive(cur > 0);   // only show while shielded
    }
}