using UnityEngine;
using UnityEngine.UI;

// OPTION B: use this ONLY if you don't have SpawnSoldierButton in your project.
// Greys out an UpgradeButton when the player can't afford it.
// NOTE: rename this file to AffordabilityTint.cs and the class below to match
// if you want to swap it in for the other version.
[RequireComponent(typeof(Button))]
public class AffordabilityTint_UpgradeOnly : MonoBehaviour
{
    [Range(0.1f, 1f)] public float disabledAlpha = 0.4f;

    private Button button;
    private CanvasGroup group;
    private UpgradeButton upgrade;

    void Awake()
    {
        button = GetComponent<Button>();
        upgrade = GetComponent<UpgradeButton>();
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
    }

    void Update()
    {
        int cost = upgrade != null ? upgrade.cost : 0;
        bool afford = GameManager.Instance != null && GameManager.Instance.coins >= cost;

        group.alpha = afford ? 1f : disabledAlpha;
        if (button != null) button.interactable = afford;
    }
}