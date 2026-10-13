using System;
using System.Collections.Generic;
using UnityEngine;

// Put this on the draft panel. UpgradeManager.RunDraft() calls Show(...) with
// the 3 offered upgrades and a pick callback, then Hide() once a card is
// picked. No code changes needed to add more upgrades — this just renders
// whatever list it's given.
public class UpgradeDraftUI : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("The whole draft overlay — disabled by default, shown only during a draft.")]
    public GameObject panelRoot;
    [Tooltip("Parent with a Horizontal/Grid Layout Group the 3 cards spawn into.")]
    public Transform cardGridRoot;
    public UpgradeCardUI cardPrefab;

    [Header("Reroll Configuration (Repeatable Coin Sink)")]
    [Tooltip("Optional reference to Reroll Button. Automatically created/found if left empty.")]
    public UnityEngine.UI.Button rerollButton;
    [Tooltip("Text label displaying reroll cost.")]
    public TMPro.TMP_Text rerollLabel;
    [Tooltip("Base cost in coins for the first reroll each wave.")]
    public int baseRerollCost = 50;
    [Tooltip("Cost increase for each subsequent reroll within the same wave.")]
    public int rerollCostIncrement = 50;

    private int currentRerollCost = 50;
    private readonly List<UpgradeCardUI> spawned = new List<UpgradeCardUI>();
    private Action<UpgradeDefinition> onPicked;

    public void Show(List<UpgradeDefinition> offers, Action<UpgradeDefinition> onPick)
    {
        Show(offers, onPick, isReroll: false);
    }

    public void Show(List<UpgradeDefinition> offers, Action<UpgradeDefinition> onPick, bool isReroll)
    {
        onPicked = onPick;

        if (!isReroll)
        {
            currentRerollCost = baseRerollCost;
        }

        EnsureRerollButton();
        UpdateRerollButton();

        foreach (UpgradeCardUI c in spawned)
            if (c != null) Destroy(c.gameObject);
        spawned.Clear();

        if (cardPrefab != null && cardGridRoot != null)
        {
            foreach (UpgradeDefinition def in offers)
            {
                UpgradeCardUI card = Instantiate(cardPrefab, cardGridRoot);
                int currentLevel = UpgradeManager.Instance != null ? UpgradeManager.Instance.LevelOf(def) : 0;
                bool willBeBoss = currentLevel == UpgradeDefinition.MaxNormalLevel;
                card.Setup(def, currentLevel, willBeBoss, () => Pick(def));
                spawned.Add(card);
            }
        }

        if (panelRoot) panelRoot.SetActive(true);
    }

    public void RefreshOffers(List<UpgradeDefinition> offers, Action<UpgradeDefinition> onPick)
    {
        Show(offers, onPick, isReroll: true);
    }

    void PositionRerollButton(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        float targetY = -335f;
        if (cardGridRoot is RectTransform gridRt)
        {
            float gridH = gridRt.rect.height > 0 ? gridRt.rect.height : gridRt.sizeDelta.y;
            if (gridH <= 0) gridH = 440f;
            float gridBottom = gridRt.anchoredPosition.y - (gridH * gridRt.pivot.y);
            float btnHeight = 52f;
            targetY = gridBottom - 28f - (btnHeight * 0.5f);
        }

        rt.anchoredPosition = new Vector2(0f, targetY);
        rt.sizeDelta = new Vector2(260f, 52f);
    }

    void EnsureRerollButton()
    {
        if (panelRoot == null) return;

        if (rerollButton == null)
        {
            Transform existing = panelRoot.transform.Find("RerollButton");
            if (existing != null)
            {
                rerollButton = existing.GetComponent<UnityEngine.UI.Button>();
                rerollLabel = existing.GetComponentInChildren<TMPro.TMP_Text>();
            }
            else
            {
                // Dynamically construct a styled Reroll Button
                GameObject btnObj = new GameObject("RerollButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
                btnObj.transform.SetParent(panelRoot.transform, false);

                UnityEngine.UI.Image img = btnObj.GetComponent<UnityEngine.UI.Image>();
                img.color = new Color(0.18f, 0.22f, 0.32f, 0.95f);

                rerollButton = btnObj.GetComponent<UnityEngine.UI.Button>();
                UnityEngine.UI.ColorBlock cb = rerollButton.colors;
                cb.normalColor = new Color(0.18f, 0.22f, 0.32f, 0.95f);
                cb.highlightedColor = new Color(0.28f, 0.35f, 0.52f, 1f);
                cb.pressedColor = new Color(0.12f, 0.16f, 0.24f, 1f);
                rerollButton.colors = cb;

                var outline = btnObj.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = new Color(0.85f, 0.72f, 0.25f, 0.5f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);

                if (btnObj.GetComponent<ButtonClickSfx>() == null)
                {
                    btnObj.AddComponent<ButtonClickSfx>();
                }

                GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                textObj.transform.SetParent(btnObj.transform, false);
                RectTransform textRt = textObj.GetComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.sizeDelta = Vector2.zero;

                rerollLabel = textObj.GetComponent<TMPro.TextMeshProUGUI>();
                rerollLabel.alignment = TMPro.TextAlignmentOptions.Center;
                rerollLabel.fontSize = 20;
                rerollLabel.fontStyle = TMPro.FontStyles.Bold;
                rerollLabel.color = new Color(1f, 0.85f, 0.2f);
            }
        }

        if (rerollButton != null)
        {
            PositionRerollButton(rerollButton.GetComponent<RectTransform>());
            rerollButton.onClick.RemoveAllListeners();
            rerollButton.onClick.AddListener(OnRerollClicked);
        }
    }

    void UpdateRerollButton()
    {
        if (rerollLabel != null)
        {
            rerollLabel.text = $"REROLL ({currentRerollCost} COINS)";
        }
        if (rerollButton != null)
        {
            rerollButton.interactable = true;
        }
    }

    void OnRerollClicked()
    {
        int cost = currentRerollCost;
        bool spent = false;

        // Spend from in-run coins first; fall back to persistent wallet
        if (GameManager.Instance != null && GameManager.Instance.SpendCoins(cost))
        {
            spent = true;
        }
        else if (Wallet.Spend(cost))
        {
            spent = true;
        }

        if (!spent)
        {
            UIToast.ShowAt(rerollButton != null ? rerollButton.transform : transform, $"Need {cost} Coins to Reroll!", new Color(1f, 0.85f, 0.2f));
            return;
        }

        currentRerollCost += rerollCostIncrement;
        UpdateRerollButton();

        if (UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.RerollDraft();
        }

        UIToast.ShowAt(rerollButton != null ? rerollButton.transform : transform, "Cards Rerolled!", Color.cyan);
    }

    public void Hide()
    {
        if (panelRoot)
        {
            var cg = panelRoot.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.blocksRaycasts = false;
                cg.interactable = false;
            }
            panelRoot.SetActive(false);
        }

        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

        if (NativeKeyboardInput.Instance != null)
            NativeKeyboardInput.Instance.FocusInputField();
    }

    void Pick(UpgradeDefinition def)
    {
        onPicked?.Invoke(def);
    }
}
