using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Universal click and tap trigger for all ability UI components (buttons, plates, labels, bars).
// Guarantees immediate response with tactile punch feedback and correct ability execution.
public class AbilityClickTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    public enum AbilityType { Repair, Shield, TimeSink, Overload }

    public AbilityType abilityType;
    [Tooltip("Target transform to animate with a tactile bounce punch. Defaults to this transform.")]
    public Transform bounceTarget;

    private Coroutine punchRoutine;
    private Vector3 initialScale = Vector3.one;
    private bool scaleInitialized = false;

    void Awake()
    {
        InitScale();
        // Ensure any Graphic on this object can receive raycasts
        var g = GetComponent<Graphic>();
        if (g != null) g.raycastTarget = true;
    }

    void InitScale()
    {
        if (scaleInitialized) return;
        Transform target = bounceTarget != null ? bounceTarget : transform;
        initialScale = target.localScale;
        scaleInitialized = true;
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
        Execute();
    }

    public void TriggerPunch()
    {
        InitScale();
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

    public void Execute()
    {
        TriggerPunch();
        Transform toastOrigin = bounceTarget != null ? bounceTarget : transform;

        switch (abilityType)
        {
            case AbilityType.Repair:
                ExecuteRepair(toastOrigin);
                break;

            case AbilityType.Shield:
                ExecuteShield(toastOrigin);
                break;

            case AbilityType.TimeSink:
                ExecuteTimeSink(toastOrigin);
                break;

            case AbilityType.Overload:
                ExecuteOverload(toastOrigin);
                break;
        }
    }

    void ExecuteRepair(Transform toastOrigin)
    {
        var ub = GetComponentInParent<UpgradeButton>();
        if (ub == null) ub = FindAnyObjectByType<UpgradeButton>(FindObjectsInactive.Include);

        if (ub != null)
        {
            ub.Purchase();
            return;
        }

        var gm = GameManager.Instance;
        if (gm == null || gm.IsGameOver) return;

        int cost = 10;
        int amount = 20;

        if (gm.currentHealth >= gm.maxHealth)
        {
            SfxPlayer.PlayButtonClick();
            UIToast.ShowAt(toastOrigin, "Fortress Health Full (100%)", Color.green);
            return;
        }

        if (gm.coins < cost)
        {
            SfxPlayer.PlayWrongKey();
            UIToast.ShowAt(toastOrigin, $"Need {cost} Coins to Repair!", new Color(1f, 0.85f, 0.2f));
            return;
        }

        if (gm.SpendCoins(cost))
        {
            gm.HealFortress(amount);
            SfxPlayer.PlayGameStart();
            UIToast.ShowAt(toastOrigin, $"+{amount} HP Repaired!", Color.green);
        }
    }

    void ExecuteShield(Transform toastOrigin)
    {
        var sb = GetComponentInParent<ShieldButton>();
        if (sb == null) sb = FindAnyObjectByType<ShieldButton>(FindObjectsInactive.Include);

        if (sb != null)
        {
            sb.Buy();
            return;
        }

        var sm = ShieldManager.Instance;
        var gm = GameManager.Instance;
        if (sm == null || gm == null || gm.IsGameOver) return;

        if (sm.IsActive)
        {
            SfxPlayer.PlayButtonClick();
            UIToast.ShowAt(toastOrigin, "Shield Already Active!", Color.cyan);
            return;
        }

        if (gm.coins < sm.cost)
        {
            SfxPlayer.PlayWrongKey();
            UIToast.ShowAt(toastOrigin, $"Need {sm.cost} Coins for Shield!", new Color(1f, 0.85f, 0.2f));
            return;
        }

        sm.TryRaiseShield();
        SfxPlayer.PlayGameStart();
        UIToast.ShowAt(toastOrigin, "Shield Activated!", Color.cyan);
    }

    void ExecuteTimeSink(Transform toastOrigin)
    {
        var ts = TimeSinkManager.Instance;
        if (ts == null) return;

        var gm = GameManager.Instance;
        if (gm != null && gm.IsGameOver) return;

        if (ts.IsActive)
        {
            SfxPlayer.PlayButtonClick();
            UIToast.ShowAt(toastOrigin, "Time Sink Active!", Color.cyan);
            return;
        }

        // Free ability -- clicking it activates it immediately
        if (!ts.IsReady)
        {
            ts.DebugFillCharge();
        }

        ts.Activate();
        SfxPlayer.PlayGameStart();
        UIToast.ShowAt(toastOrigin, "Time Sink Activated!", Color.cyan);
    }

    void ExecuteOverload(Transform toastOrigin)
    {
        var cm = ComboManager.Instance;
        if (cm == null) return;

        var gm = GameManager.Instance;
        if (gm != null && gm.IsGameOver) return;

        // Free ability -- clicking it activates it immediately
        if (!cm.overloadReady)
        {
            cm.DebugFillOverload();
        }

        cm.TriggerOverload();
        SfxPlayer.PlayGameStart();
        UIToast.ShowAt(toastOrigin, "Overload Blast!", Color.yellow);
    }
}
