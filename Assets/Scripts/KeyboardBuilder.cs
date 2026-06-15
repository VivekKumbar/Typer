using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Auto-generates A-Z buttons at runtime so you don't place 26 buttons by hand.
// Put this on the KeyboardPanel and give it one key-button prefab.
public class KeyboardBuilder : MonoBehaviour
{
    [Header("Refs")]
    public Button keyButtonPrefab; // a UI Button with a TMP_Text child
    public Transform keyboardRoot; // a panel with a GridLayoutGroup

    private const string LETTERS = "QWERTYUIOPASDFGHJKLZXCVBNM";

    void Start()
    {
        foreach (char c in LETTERS)
        {
            Button btn = Instantiate(keyButtonPrefab, keyboardRoot);
            TMP_Text txt = btn.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = c.ToString();
            char captured = c; // capture for the closure
            btn.onClick.AddListener(() => TypingController.Instance.ReceiveChar(captured));
        }
    }
}
