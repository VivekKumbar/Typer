using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// A swipeable, snapping carousel of level/mode cards. The centered card is
// scaled up and fully opaque; side cards shrink and fade. Swipe or use arrows.
public class LevelCarousel : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Layout")]
    [Tooltip("Horizontal distance between card centers, in canvas units.")]
    public float spacing = 600f;
    [Tooltip("Scale of the centered (focused) card.")]
    public float focusedScale = 1f;
    [Tooltip("Scale of side cards.")]
    public float sideScale = 0.7f;
    [Tooltip("Alpha of side cards (needs a CanvasGroup on each card).")]
    [Range(0f, 1f)] public float sideAlpha = 0.5f;

    [Header("Motion")]
    public float snapSpeed = 10f;
    [Tooltip("Swipe distance (px) needed to move one card.")]
    public float swipeThreshold = 150f;

    [Header("Cards")]
    [Tooltip("The card RectTransforms, left to right. Auto-filled from children if empty.")]
    public List<RectTransform> cards = new List<RectTransform>();

    [Header("Ground Skin preview")]
    [Tooltip("The shop catalog to resolve the equipped Ground Skin's ShopItem from, for each card's background (see LevelCard.RefreshGroundSkinBackground). Leave empty to leave cards on their Inspector-assigned backgrounds.")]
    public ShopCatalog groundSkinCatalog;

    public int CurrentIndex { get; private set; }

    private float dragStartX;
    private float containerStartX;
    private bool dragging;
    private RectTransform rt;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        if (cards.Count == 0)
            foreach (RectTransform child in transform)
                cards.Add(child);
    }

    void Start()
    {
        LayoutCards();
        SnapTo(0, true);
        RefreshGroundSkinBackgrounds();
    }

    // Re-reads the equipped Ground Skin so card backgrounds update without
    // needing to close/reopen the app. OnEnable (not just Start) so this
    // re-fires if the carousel/its parent panel is ever toggled off and back
    // on; ShopUI.OnDisable also calls this directly since the Shop panel
    // itself doesn't currently disable the carousel underneath it.
    void OnEnable() { RefreshGroundSkinBackgrounds(); }

    public void RefreshGroundSkinBackgrounds()
    {
        foreach (RectTransform card in cards)
        {
            LevelCard lc = card != null ? card.GetComponent<LevelCard>() : null;
            if (lc != null) lc.RefreshGroundSkinBackground(groundSkinCatalog);
        }
    }

    void LayoutCards()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            cards[i].anchoredPosition = new Vector2(i * spacing, 0f);
            // Ensure a CanvasGroup for fading
            if (cards[i].GetComponent<CanvasGroup>() == null)
                cards[i].gameObject.AddComponent<CanvasGroup>();
        }
    }

    void Update()
    {
        if (dragging) return;

        // Glide the container so the current card sits at center
        float targetX = -CurrentIndex * spacing;
        Vector2 pos = rt.anchoredPosition;
        pos.x = Mathf.Lerp(pos.x, targetX, snapSpeed * Time.unscaledDeltaTime);
        rt.anchoredPosition = pos;

        UpdateCardVisuals();
    }

    void UpdateCardVisuals()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            // How far this card is from center (0 = centered)
            float dist = Mathf.Abs(rt.anchoredPosition.x + i * spacing) / spacing;
            float t = Mathf.Clamp01(dist);

            cards[i].localScale = Vector3.one * Mathf.Lerp(focusedScale, sideScale, t);

            CanvasGroup cg = cards[i].GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = Mathf.Lerp(1f, sideAlpha, t);
        }
    }

    // ---- swipe ----
    public void OnBeginDrag(PointerEventData e)
    {
        dragging = true;
        dragStartX = e.position.x;
        containerStartX = rt.anchoredPosition.x;
    }

    public void OnDrag(PointerEventData e)
    {
        float delta = e.position.x - dragStartX;
        Vector2 pos = rt.anchoredPosition;
        pos.x = containerStartX + delta;
        rt.anchoredPosition = pos;
        UpdateCardVisuals();
    }

    public void OnEndDrag(PointerEventData e)
    {
        dragging = false;
        float delta = e.position.x - dragStartX;

        if (delta < -swipeThreshold) SnapTo(CurrentIndex + 1);
        else if (delta > swipeThreshold) SnapTo(CurrentIndex - 1);
        // else: snaps back to current in Update
    }

    // ---- arrows / buttons ----
    public void Next() { SnapTo(CurrentIndex + 1); }
    public void Previous() { SnapTo(CurrentIndex - 1); }

    public void SnapTo(int index, bool instant = false)
    {
        CurrentIndex = Mathf.Clamp(index, 0, cards.Count - 1);
        if (instant)
        {
            rt.anchoredPosition = new Vector2(-CurrentIndex * spacing, rt.anchoredPosition.y);
            UpdateCardVisuals();
        }
    }

    // Hook a "PLAY" button to this — returns which card is centered.
    public int GetSelected() { return CurrentIndex; }

    // Returns the LevelCard of the centered card (or null).
    public LevelCard SelectedCard()
    {
        if (CurrentIndex < 0 || CurrentIndex >= cards.Count) return null;
        return cards[CurrentIndex].GetComponent<LevelCard>();
    }

    // Hook the PLAY button here. Applies the card's mode, then launches it —
    // routed through the MainMenu loader if one is assigned (for the loading bar).
    [Header("Play")]
    public MainMenu loader; // optional: routes through the loading screen

    public void PlaySelected()
    {
        LevelCard card = SelectedCard();
        if (card == null) return;

        // Apply the card's mode flags (e.g. dark mode) before launching
        if (card.setDarkMode) DarkMode.Enabled = card.forceDarkMode;
        PlayerPrefs.SetInt(card.modeKey, card.modeId);
        PlayerPrefs.Save();

        if (loader != null)
        {
            loader.gameSceneName = card.sceneToLoad; // load THIS card's scene
            loader.PlayGame();                        // shows your loading bar
        }
        else
        {
            card.Launch(); // direct load fallback
        }
    }
}