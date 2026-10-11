using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Snaps shop item cards vertically so that each card is perfectly centered
// inside the frame. When snapped, the centered card is 100% visible with all
// borders intact. Side cards scale down and fade, and snap cleanly on swipe/scroll.
public class ShopCardSnapScroll : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    [Header("References")]
    public ScrollRect scrollRect;
    public RectTransform viewport;
    public RectTransform content;
    public GridLayoutGroup grid;

    [Header("Motion")]
    [Tooltip("Smoothing speed for gliding to the target card.")]
    public float snapSpeed = 14f;
    [Tooltip("Swipe distance (px) needed to advance to next/prev card.")]
    public float swipeThreshold = 60f;

    [Header("Card Visuals")]
    [Tooltip("Scale of side cards relative to the centered card.")]
    public float sideScale = 0.92f;
    [Tooltip("Alpha of side cards when offset from center.")]
    [Range(0f, 1f)] public float sideAlpha = 0.35f;

    public int CurrentIndex { get; private set; } = 0;

    private readonly List<RectTransform> cardRects = new List<RectTransform>();
    private readonly List<CanvasGroup> cardCanvasGroups = new List<CanvasGroup>();
    private float stepHeight = 774f;
    private bool isDragging = false;
    private float dragStartY = 0f;

    void Awake()
    {
        if (scrollRect == null) scrollRect = GetComponent<ScrollRect>();
        if (scrollRect != null)
        {
            if (viewport == null) viewport = scrollRect.viewport;
            if (content == null) content = scrollRect.content;
        }
        if (content != null && grid == null)
            grid = content.GetComponent<GridLayoutGroup>();
    }

    // Called by ShopUI whenever a category spawns its cards.
    public void RefreshCards(List<ShopItemUI> spawnedCards)
    {
        if (scrollRect == null) scrollRect = GetComponent<ScrollRect>();
        if (scrollRect != null)
        {
            if (viewport == null) viewport = scrollRect.viewport;
            if (content == null) content = scrollRect.content;
        }
        if (content != null && grid == null)
            grid = content.GetComponent<GridLayoutGroup>();

        cardRects.Clear();
        cardCanvasGroups.Clear();

        if (spawnedCards == null || spawnedCards.Count == 0) return;

        // Auto-center padding: calculate top and bottom padding so Card 0
        // is placed dead-center in the viewport when content.anchoredPosition.y == 0.
        if (grid != null && viewport != null)
        {
            float vpHeight = viewport.rect.height;
            if (vpHeight <= 0f) vpHeight = 1362f; // fallback to standard reference height
            float cellH = grid.cellSize.y;
            float centerPadding = Mathf.Max(0f, (vpHeight - cellH) * 0.5f);

            var padding = grid.padding;
            padding.top = Mathf.RoundToInt(centerPadding);
            padding.bottom = Mathf.RoundToInt(centerPadding);
            grid.padding = padding;

            stepHeight = grid.cellSize.y + grid.spacing.y;
        }
        else
        {
            stepHeight = 774f;
        }

        for (int i = 0; i < spawnedCards.Count; i++)
        {
            if (spawnedCards[i] == null) continue;
            RectTransform crt = spawnedCards[i].GetComponent<RectTransform>();
            cardRects.Add(crt);

            CanvasGroup cg = spawnedCards[i].GetComponent<CanvasGroup>();
            if (cg == null) cg = spawnedCards[i].gameObject.AddComponent<CanvasGroup>();
            cardCanvasGroups.Add(cg);

            // Hook click to focus on non-centered cards
            int cardIndex = i;
            ShopCardClickFocus clickFocus = spawnedCards[i].GetComponent<ShopCardClickFocus>();
            if (clickFocus == null) clickFocus = spawnedCards[i].gameObject.AddComponent<ShopCardClickFocus>();
            clickFocus.Init(this, cardIndex);
        }

        SnapTo(0, instant: true);
    }

    public void SnapTo(int index, bool instant = false)
    {
        if (cardRects.Count == 0)
        {
            CurrentIndex = 0;
            return;
        }

        CurrentIndex = Mathf.Clamp(index, 0, cardRects.Count - 1);
        float targetY = CurrentIndex * stepHeight;

        if (content != null)
        {
            if (instant)
            {
                Vector2 pos = content.anchoredPosition;
                pos.y = targetY;
                content.anchoredPosition = pos;
                if (scrollRect != null) scrollRect.StopMovement();
                UpdateCardVisuals();
            }
        }
    }

    public void Next()
    {
        SnapTo(CurrentIndex + 1);
    }

    public void Previous()
    {
        SnapTo(CurrentIndex - 1);
    }

    void Update()
    {
        if (cardRects.Count == 0 || content == null) return;

        if (!isDragging)
        {
            float targetY = CurrentIndex * stepHeight;
            Vector2 pos = content.anchoredPosition;
            if (Mathf.Abs(pos.y - targetY) > 0.5f)
            {
                pos.y = Mathf.Lerp(pos.y, targetY, snapSpeed * Time.unscaledDeltaTime);
                content.anchoredPosition = pos;
            }
            else if (pos.y != targetY)
            {
                pos.y = targetY;
                content.anchoredPosition = pos;
            }
        }

        UpdateCardVisuals();
    }

    void UpdateCardVisuals()
    {
        if (cardRects.Count == 0 || content == null || stepHeight <= 0f) return;
        float currentY = content.anchoredPosition.y;

        for (int i = 0; i < cardRects.Count; i++)
        {
            if (cardRects[i] == null) continue;
            float cardCenterY = i * stepHeight;
            float dist = Mathf.Abs(currentY - cardCenterY) / stepHeight;
            float t = Mathf.Clamp01(dist);

            cardRects[i].localScale = Vector3.one * Mathf.Lerp(1f, sideScale, t);

            if (i < cardCanvasGroups.Count && cardCanvasGroups[i] != null)
            {
                if (dist <= 1f)
                {
                    cardCanvasGroups[i].alpha = Mathf.Lerp(1f, sideAlpha, t);
                }
                else
                {
                    float overflow = Mathf.Clamp01((dist - 1f) / 0.5f);
                    cardCanvasGroups[i].alpha = Mathf.Lerp(sideAlpha, 0f, overflow);
                }
            }
        }
    }

    // ---- Swipe / Drag handlers ----
    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        dragStartY = eventData.position.y;
        if (scrollRect != null) scrollRect.StopMovement();
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateCardVisuals();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        if (scrollRect != null) scrollRect.StopMovement();

        float delta = eventData.position.y - dragStartY;

        // In screen space, dragging upward (delta > 0) pulls the list up -> next card
        // Dragging downward (delta < 0) pulls the list down -> previous card
        if (delta > swipeThreshold)
        {
            SnapTo(CurrentIndex + 1);
        }
        else if (delta < -swipeThreshold)
        {
            SnapTo(CurrentIndex - 1);
        }
        else
        {
            // Snap to nearest card based on current position
            if (content != null && stepHeight > 0f)
            {
                int nearest = Mathf.RoundToInt(content.anchoredPosition.y / stepHeight);
                SnapTo(nearest);
            }
            else
            {
                SnapTo(CurrentIndex);
            }
        }
    }

    // ---- Mouse Scroll Wheel ----
    public void OnScroll(PointerEventData eventData)
    {
        if (eventData.scrollDelta.y < -0.1f)
        {
            Next();
        }
        else if (eventData.scrollDelta.y > 0.1f)
        {
            Previous();
        }
    }
}
