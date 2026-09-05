using UnityEngine;

// Tracks whether the player has seen the FTUE (First Time User Experience)
// tutorial, saved to disk (PlayerPrefs). Same pattern as Wallet.cs / SaveManager.cs.
public static class FtueState
{
    const string SeenKey = "TypeKeep_HasSeenFtue";

    public static bool HasSeenFtue => PlayerPrefs.GetInt(SeenKey, 0) == 1;

    public static void MarkSeen()
    {
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();
    }

    // For testing -- lets you re-trigger the FTUE on the next Play click
    // without deleting all PlayerPrefs. Delete key "TypeKeep_HasSeenFtue" by
    // hand (or call this from a debug hook) to see it again.
    public static void ResetForTesting()
    {
        PlayerPrefs.DeleteKey(SeenKey);
        PlayerPrefs.Save();
    }
}
