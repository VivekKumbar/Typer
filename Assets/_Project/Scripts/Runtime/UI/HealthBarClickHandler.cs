using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Enables clicking on any health bar component (top HUD bar, badge, repair bar in abilities)
// to trigger fortress repair, providing instant tactile bounce, audio, and visual screen-space toast feedback.
public class HealthBarClickHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Tooltip("Optional transform to bounce when clicked. If null, bounces this object's transform.")]
    public Transform bounceTarget;

    private Coroutine punchRoutine;
    private Vector3 initialScale = Vector3.one;
    private bool initializedScale = false;

    void Awake()
    {
        CacheScale();
    }

    void CacheScale()
    {
        if (initializedScale) return;
        Transform target = bounceTarget != null ? bounceTarget : transform;
        initialScale = target.localScale;
        initializedScale = true;
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
        TryRepair();
    }

    public void TriggerPunch()
    {
        CacheScale();
        Transform target = bounceTarget != null ? bounceTarget : transform;
        if (punchRoutine != null) StopCoroutine(punchRoutine);
        punchRoutine = StartCoroutine(DoPunch(target));
    }

    private IEnumerator DoPunch(Transform target)
    {
        float dur = 0.16f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / dur;
            float s = Mathf.Lerp(0.92f, 1f, t);
            target.localScale = initialScale * s;
            yield return null;
        }
        target.localScale = initialScale;
        punchRoutine = null;
    }

    public void TryRepair()
    {
        TriggerPunch();

        var gm = GameManager.Instance;
        if (gm == null || gm.IsGameOver) return;

        var ub = FindAnyObjectByType<UpgradeButton>();
        int cost = ub != null ? ub.cost : 10;
        int amount = ub != null ? ub.amount : 20;

        Transform toastOrigin = bounceTarget != null ? bounceTarget : transform;

        // 1. If fortress is already at 100% health
        if (gm.currentHealth >= gm.maxHealth)
        {
            SfxPlayer.PlayButtonClick();
            UIToast.ShowAt(toastOrigin, "Fortress Health Full (100%)", Color.green);
            Debug.Log($"[HealthBar] Clicked: Fortress is already full health ({gm.currentHealth}/{gm.maxHealth})");
            return;
        }

        // 2. If player cannot afford repair
        if (gm.coins < cost)
        {
            SfxPlayer.PlayWrongKey();
            UIToast.ShowAt(toastOrigin, $"Need {cost} Coins to Repair!", new Color(1f, 0.85f, 0.2f));
            Debug.Log($"[HealthBar] Clicked: Cannot afford repair (Need {cost}, have {gm.coins})");
            return;
        }

        // 3. Fortress damaged and player has sufficient coins
        if (ub != null)
        {
            ub.Purchase();
        }
        else
        {
            if (gm.SpendCoins(cost))
            {
                gm.HealFortress(amount);
                SfxPlayer.PlayGameStart();
                UIToast.ShowAt(toastOrigin, $"+{amount} HP Repaired!", Color.green);
                Debug.Log($"[HealthBar] Repaired +{amount} HP! Current: {gm.currentHealth}/{gm.maxHealth}, Coins left: {gm.coins}");
            }
        }
    }
}
