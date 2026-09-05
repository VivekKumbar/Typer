using System;
using UnityEngine;

// The player's permanent coin bank, saved to disk (PlayerPrefs).
// Separate from GameManager.coins, which is the spendable in-run total.
// Access from anywhere via Wallet.Total, Wallet.Add(), Wallet.Spend().
public static class Wallet
{
    const string KEY = "TypeKeep_TotalCoins";

    // Fired whenever the total changes, so the menu/HUD can refresh.
    public static event Action<int> OnChanged;

    public static int Total
    {
        get { return PlayerPrefs.GetInt(KEY, 0); }
        private set
        {
            PlayerPrefs.SetInt(KEY, Mathf.Max(0, value));
            PlayerPrefs.Save();
            OnChanged?.Invoke(Total);
        }
    }

    public static void Add(int amount)
    {
        if (amount <= 0) return;
        Total = Total + amount;
    }

    public static bool Spend(int amount)
    {
        if (amount <= 0) return true;
        if (Total < amount) return false;
        Total = Total - amount;
        return true;
    }

    // For a reset button / testing
    public static void ResetWallet() { Total = 0; }
}