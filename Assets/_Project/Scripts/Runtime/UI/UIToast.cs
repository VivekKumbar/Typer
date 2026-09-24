using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Spawns clean, beautiful on-screen UI toasts directly onto the HUD Canvas.
// Works seamlessly in Screen Space - Overlay and across all platforms (WebGL, Editor, Mobile).
public class UIToast : MonoBehaviour
{
    private static UIToast activeToast;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private TMP_Text textComponent;

    public static void Show(string message, Color color)
    {
        ShowAt(null, message, color);
    }

    public static void ShowAt(Transform target, string message, Color color)
    {
        Canvas canvas = FindRootCanvas();
        if (canvas == null) return;

        // Clean up previous toast if still animating
        if (activeToast != null && activeToast.gameObject != null)
        {
            Destroy(activeToast.gameObject);
            activeToast = null;
        }

        GameObject toastGO = new GameObject("HUD_Toast", typeof(RectTransform), typeof(CanvasGroup));
        toastGO.transform.SetParent(canvas.transform, false);

        UIToast toast = toastGO.AddComponent<UIToast>();
        activeToast = toast;

        toast.Initialize(target, message, color, canvas);
    }

    private static Canvas FindRootCanvas()
    {
        var hud = FindAnyObjectByType<HUD>();
        if (hud != null)
        {
            Canvas c = hud.GetComponentInParent<Canvas>();
            if (c != null) return c;
        }

        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var c in canvases)
        {
            if (c.isRootCanvas && c.enabled && c.gameObject.activeInHierarchy)
                return c;
        }
        return null;
    }

    private void Initialize(Transform target, string message, Color color, Canvas canvas)
    {
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(420, 52);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        // Position toast intelligently relative to the target or center of screen
        Vector2 targetAnchored = Vector2.zero;
        if (target != null)
        {
            RectTransform targetRT = target as RectTransform;
            if (targetRT != null)
            {
                Vector3 worldPos = targetRT.position;
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, worldPos);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.transform as RectTransform, screenPoint, canvas.worldCamera, out targetAnchored);

                // If target is near top (like health bar), show below it. If near bottom (abilities), show above it.
                if (targetAnchored.y > 0)
                    targetAnchored.y -= 75f;
                else
                    targetAnchored.y += 85f;

                // Keep within reasonable horizontal bounds
                targetAnchored.x = Mathf.Clamp(targetAnchored.x, -200f, 200f);
            }
        }
        else
        {
            targetAnchored = new Vector2(0f, -120f);
        }

        rectTransform.anchoredPosition = targetAnchored;

        // Background pill
        Image bg = gameObject.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.08f, 0.12f, 0.88f);
        bg.raycastTarget = false;

        // Text element
        GameObject textGO = new GameObject("ToastText", typeof(RectTransform));
        textGO.transform.SetParent(transform, false);
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;
        textRT.offsetMin = new Vector2(16, 4);
        textRT.offsetMax = new Vector2(-16, -4);

        textComponent = textGO.AddComponent<TextMeshProUGUI>();
        textComponent.text = message;
        textComponent.fontSize = 22;
        textComponent.fontStyle = FontStyles.Bold;
        textComponent.alignment = TextAlignmentOptions.Center;
        textComponent.color = color;
        textComponent.raycastTarget = false;

        // Inherit font asset from HUD if possible
        var hud = FindAnyObjectByType<HUD>();
        if (hud != null && hud.healthText != null)
            textComponent.font = hud.healthText.font;

        StartCoroutine(AnimateToast(targetAnchored));
    }

    private IEnumerator AnimateToast(Vector2 basePos)
    {
        float duration = 1.35f;
        float elapsed = 0f;

        transform.localScale = Vector3.one * 0.75f;
        canvasGroup.alpha = 0f;

        // Pop in
        float popTime = 0.15f;
        while (elapsed < popTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / popTime;
            transform.localScale = Vector3.Lerp(Vector3.one * 0.75f, Vector3.one * 1.04f, t);
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            yield return null;
        }

        // Settle scale
        transform.localScale = Vector3.one;

        // Float upward and fade out
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            rectTransform.anchoredPosition = basePos + new Vector2(0f, 30f * t);

            if (t > 0.65f)
            {
                float fadeT = (t - 0.65f) / 0.35f;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, fadeT);
            }

            yield return null;
        }

        if (activeToast == this) activeToast = null;
        Destroy(gameObject);
    }
}
