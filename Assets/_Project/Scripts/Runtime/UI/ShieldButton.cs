using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Hook a UI Button's OnClick to Buy().
[RequireComponent(typeof(Button))]
public class ShieldButton : MonoBehaviour
{
    public TMP_Text labelText;
    private Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        FindLabelIfNeeded();
    }

    void OnEnable()
    {
        FindLabelIfNeeded();
        RefreshLabel();
    }

    void Start()
    {
        FindLabelIfNeeded();
        RefreshLabel();
    }

    public void FindLabelIfNeeded()
    {
        if (labelText != null) return;
        if (transform.parent != null)
        {
            var t = transform.parent.Find("ShieldLabel");
            if (t != null) labelText = t.GetComponent<TMP_Text>();
        }
        if (labelText == null)
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (t != null && t.gameObject.name == "ShieldLabel")
                {
                    labelText = t;
                    break;
                }
            }
        }
    }

    public bool Buy()
    {
        var sm = ShieldManager.Instance;
        var gm = GameManager.Instance;
        if (sm == null || gm == null || gm.IsGameOver) return false;

        if (sm.IsActive)
        {
            SfxPlayer.PlayButtonClick();
            UIToast.ShowAt(transform, "Shield Already Active!", Color.cyan);
            Debug.Log($"[ShieldButton] Shield already active ({sm.Current}/{sm.shieldMax})");
            return false;
        }

        if (gm.coins < sm.cost)
        {
            SfxPlayer.PlayWrongKey();
            UIToast.ShowAt(transform, $"Need {sm.cost} Coins for Shield!", new Color(1f, 0.85f, 0.2f));
            Debug.Log($"[ShieldButton] Need {sm.cost} coins for shield, but have {gm.coins}");
            return false;
        }

        sm.TryRaiseShield();
        SfxPlayer.PlayGameStart();
        UIToast.ShowAt(transform, "Shield Activated!", Color.cyan);
        Debug.Log($"[ShieldButton] Shield raised! Coins left: {gm.coins}");
        RefreshLabel();
        return true;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (button != null && gm != null) button.interactable = !gm.IsGameOver;
        RefreshLabel();
    }

    public void RefreshLabel()
    {
        FindLabelIfNeeded();
        var sm = ShieldManager.Instance;
        if (labelText != null && sm != null)
        {
            labelText.text = sm.IsActive ? "SHIELD UP" : $"SHIELD ({sm.cost})";
        }
    }
}