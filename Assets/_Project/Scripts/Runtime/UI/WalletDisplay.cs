using UnityEngine;
using TMPro;

// Shows the player's saved coin total. Put on any object with a TMP text
// (works in the Main Menu, shop, wherever).
public class WalletDisplay : MonoBehaviour
{
    public TMP_Text text;
    public string prefix = "";   // e.g. "Coins: "

    void OnEnable()
    {
        Wallet.OnChanged += Refresh;
        Refresh(Wallet.Total);
    }

    void OnDisable()
    {
        Wallet.OnChanged -= Refresh;
    }

    void Refresh(int total)
    {
        if (text != null) text.text = prefix + total.ToString();
    }
}