/// <summary>
/// Universal completion status returned by rewarded ad SDKs (LevelPlay, AdMob, AppLovin, Bridge, etc.).
/// </summary>
public enum AdCompletionState
{
    Completed,  // Player watched the full ad until the end -> Eligible for reward
    Skipped,    // Player skipped or dismissed the ad early -> NOT eligible
    Failed,     // Ad failed to load, stream, or render -> NOT eligible
    Canceled    // User backed out or closed the ad modal -> NOT eligible
}

/// <summary>
/// Standard Unity Ads show completion states (compatible with Unity Ads SDK).
/// </summary>
public enum UnityAdsShowCompletionState
{
    NOT_DEFINED = -1,
    SKIPPED = 0,
    COMPLETED = 1,
    UNKNOWN = 2
}
