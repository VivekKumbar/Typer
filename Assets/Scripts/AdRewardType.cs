/// <summary>
/// Defines the reward types for Rewarded Ads.
/// </summary>
public enum AdRewardType
{
    None,
    MainMenu50Coins,  // Main Menu ad (+50 coins, 4-hour cooldown)
    DoubleCoins,      // Game Scene ad (+100% round earnings, no cooldown)
    Shop500Coins,     // Shop ad (+500 coins, no cooldown)
    FlatAmount        // Legacy alias for Main Menu flat amount
}
