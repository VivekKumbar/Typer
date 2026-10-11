using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Fixed/single-card mode display. Multi-card swiping/carousel behavior
// has been disabled so the mode card permanently remains centered (no sliding left/right).
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

    private RectTransform rt;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        InitCards();
    }

    void Start()
    {
        LockToCenter();
        RefreshGroundSkinBackgrounds();
    }

    void OnEnable()
    {
        LockToCenter();
        RefreshGroundSkinBackgrounds();
    }

    void InitCards()
    {
        if (cards == null) cards = new List<RectTransform>();

        // Auto-fill from active child if empty
        if (cards.Count == 0)
        {
            foreach (RectTransform child in transform)
            {
                if (child.gameObject.activeSelf)
                {
                    cards.Add(child);
                    break;
                }
            }
            if (cards.Count == 0 && transform.childCount > 0)
                cards.Add(transform.GetChild(0) as RectTransform);
        }

        // Deactivate any legacy/alternate mode cards (e.g. Card_DarkMode)
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (cards.Count > 0 && child != cards[0].transform)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    public void LockToCenter()
    {
        CurrentIndex = 0;

        if (rt == null) rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            // Preserve Y offset (e.g. Y=100 in MainMenu) while locking X strictly to 0 (centered)
            rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);
        }

        float scale = focusedScale > 0.001f ? focusedScale : 1f;

        if (cards != null && cards.Count > 0 && cards[0] != null)
        {
            cards[0].gameObject.SetActive(true);
            cards[0].anchoredPosition = Vector2.zero;
            cards[0].localScale = Vector3.one * scale;

            CanvasGroup cg = cards[0].GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
        }

        // Ensure any extra cards in the list stay hidden and disabled
        if (cards != null)
        {
            for (int i = 1; i < cards.Count; i++)
            {
                if (cards[i] != null)
                    cards[i].gameObject.SetActive(false);
            }
        }
    }

    void Update()
    {
        // Enforce centered position so the card never drifts or offsets to left or right
        if (rt != null && rt.anchoredPosition.x != 0f)
        {
            rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);
        }

        float scale = focusedScale > 0.001f ? focusedScale : 1f;

        if (cards != null && cards.Count > 0 && cards[0] != null)
        {
            if (cards[0].anchoredPosition != Vector2.zero)
                cards[0].anchoredPosition = Vector2.zero;

            Vector3 expectedScale = Vector3.one * scale;
            if (cards[0].localScale != expectedScale)
                cards[0].localScale = expectedScale;
        }
    }

    // Re-reads the equipped Ground Skin so card backgrounds update without
    // needing to close/reopen the app.
    public void RefreshGroundSkinBackgrounds()
    {
        if (cards == null) return;
        foreach (RectTransform card in cards)
        {
            if (card == null) continue;
            LevelCard lc = card.GetComponent<LevelCard>();
            if (lc != null) lc.RefreshGroundSkinBackground(groundSkinCatalog);
        }
    }

    // Swiping / dragging disabled: the mode card permanently stays centered in place
    public void OnBeginDrag(PointerEventData e) { }
    public void OnDrag(PointerEventData e) { }
    public void OnEndDrag(PointerEventData e) { }

    // Navigation buttons do nothing as only a single mode is displayed
    public void Next() { }
    public void Previous() { }

    public void SnapTo(int index, bool instant = false)
    {
        LockToCenter();
    }

    public int GetSelected() { return 0; }

    // Returns the LevelCard of the centered/active card (or null).
    public LevelCard SelectedCard()
    {
        if (cards != null && cards.Count > 0 && cards[0] != null)
        {
            LevelCard lc = cards[0].GetComponent<LevelCard>();
            if (lc != null) return lc;
        }
        return GetComponentInChildren<LevelCard>(true);
    }

    // Hook the PLAY / DEFEND button here. Applies the card's mode, then launches it —
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

#if UNITY_EDITOR
    void OnValidate()
    {
        if (rt == null) rt = GetComponent<RectTransform>();
        if (rt != null && rt.anchoredPosition.x != 0f)
        {
            rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);
        }
        if (cards != null && cards.Count > 0 && cards[0] != null)
        {
            if (cards[0].anchoredPosition != Vector2.zero)
                cards[0].anchoredPosition = Vector2.zero;
        }
    }
#endif
}