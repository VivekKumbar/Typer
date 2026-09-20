using UnityEngine;
using UnityEngine.UI;
using TMPro;

// One achievement tile. Spawned by AchievementsPanel for each row loaded
// from the AchievementBank CSV. Mirrors ShopItemUI's Setup/Refresh pattern.
public class AchievementCardUI : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("PLACEHOLDER — looked up from AchievementsPanel's icon list by the CSV row's iconName. Drag real art into the panel's icon list, not here.")]
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text descriptionText;
    [Tooltip("e.g. '47/100'.")]
    public TMP_Text progressText;
    [Tooltip("Optional. Non-interactable, Raycast Target off — same convention as other progress bars in this project.")]
    public Slider progressBar;
    public TMP_Text coinRewardText;

    [Header("Three-state claim button")]
    public Button claimButton;
    public TMP_Text claimLabel;
    [Tooltip("Shown only in the Claimed state, alongside the greyed-out button — lets the player tell 'already collected' apart from 'not done yet' at a glance.")]
    public GameObject claimedCheckmark;

    [Header("Claim button colors (three states) — fallback if no sprites are assigned below")]
    public Color readyColor = new Color(0.25f, 0.75f, 0.3f);  // ReadyToClaim — green
    public Color claimedColor = new Color(0.4f, 0.4f, 0.4f);  // Claimed — grey
    public Color lockedColor = new Color(0.4f, 0.4f, 0.4f);   // NotReady — grey

    [Header("Claim button art (three states) — optional, overrides the colors above")]
    [Tooltip("Each sprite already has its own baked-in label (CLAIM/CLAIM/CLAIMED), so claimLabel is hidden automatically when these are assigned.")]
    public Sprite readySprite;
    public Sprite lockedSprite;
    public Sprite claimedSprite;

    private AchievementData data;
    private AchievementsPanel panel;
    private Image claimButtonImage;

    public void Setup(AchievementData achievement, AchievementsPanel owner)
    {
        data = achievement;
        panel = owner;
        if (claimButtonImage == null && claimButton != null) claimButtonImage = claimButton.GetComponent<Image>();

        if (nameText) nameText.text = data.displayName;
        if (descriptionText) descriptionText.text = data.description;
        if (coinRewardText) coinRewardText.text = "+" + data.coinReward;
        if (iconImage)
        {
            Sprite icon = panel != null ? panel.GetIcon(data.iconName) : null;
            if (icon == null && panel != null) icon = panel.placeholderIcon;
            iconImage.sprite = icon;
            // The Icon Image is authored fully transparent; make it visible only when there's a sprite
            // (an Image with no sprite would otherwise draw a plain white box).
            iconImage.color = icon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            iconImage.preserveAspect = true;
        }

        Refresh();

        if (claimButton != null)
        {
            claimButton.onClick.RemoveAllListeners();
            claimButton.onClick.AddListener(OnClaimClicked);
        }
    }

    // Re-reads live progress/state and updates the card in place -- called
    // by AchievementsPanel whenever a stat changes or a claim happens, and
    // right after this card's own claim button is tapped.
    public void Refresh()
    {
        if (data == null) return;

        int progress = AchievementManager.GetProgress(data.metricType);
        int shownProgress = Mathf.Min(progress, data.targetValue);
        if (progressText) progressText.text = shownProgress + "/" + data.targetValue;
        if (progressBar) progressBar.value = data.targetValue > 0 ? (float)shownProgress / data.targetValue : 0f;

        AchievementManager.AchievementState state = AchievementManager.GetState(data.id);
        if (claimedCheckmark) claimedCheckmark.SetActive(state == AchievementManager.AchievementState.Claimed);

        bool useSprites = readySprite != null && lockedSprite != null && claimedSprite != null;
        if (claimLabel) claimLabel.gameObject.SetActive(!useSprites);

        switch (state)
        {
            case AchievementManager.AchievementState.ReadyToClaim:
                if (claimLabel) claimLabel.text = "CLAIM";
                if (claimButton) claimButton.interactable = true;
                if (claimButtonImage) claimButtonImage.color = readyColor;
                if (useSprites && claimButtonImage) claimButtonImage.sprite = readySprite;
                break;
            case AchievementManager.AchievementState.Claimed:
                if (claimLabel) claimLabel.text = "CLAIMED";
                if (claimButton) claimButton.interactable = false;
                if (claimButtonImage) claimButtonImage.color = claimedColor;
                if (useSprites && claimButtonImage) claimButtonImage.sprite = claimedSprite;
                break;
            default: // NotReady
                if (claimLabel) claimLabel.text = "CLAIM";
                if (claimButton) claimButton.interactable = false;
                if (claimButtonImage) claimButtonImage.color = lockedColor;
                if (useSprites && claimButtonImage) claimButtonImage.sprite = lockedSprite;
                break;
        }

        if (useSprites && claimButtonImage) claimButtonImage.color = Color.white;
    }

    void OnClaimClicked()
    {
        if (data == null) return;
        if (AchievementManager.Claim(data.id))
            Refresh(); // updates THIS card to Claimed immediately -- no panel reopen needed
    }
}
