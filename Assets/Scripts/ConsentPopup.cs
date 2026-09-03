using UnityEngine;
using UnityEngine.UI;

// Simple one-time consent gate, shown before AdsManager ever initializes the
// SDK. "Accept" = personalized-ads consent granted. "Manage" is a lightweight
// stand-in for a real CMP (Google UMP or similar) -- for now it just records
// non-personalized consent and closes. Swap Resolve()'s Manage branch for a
// real consent-management flow later without touching AdsManager's public API
// (it only ever sees a plain bool).
//
// Lives in the Main Menu scene alongside AdsManager. Portrait 1080x1920,
// simple centered dialog -- same visual weight as ConfirmPopup, just simpler
// (no preview rows, two buttons only).
public class ConsentPopup : MonoBehaviour
{
    const string ShownKey = "TypeKeep_AdsConsentShown";
    const string GrantedKey = "TypeKeep_AdsConsentGranted";

    [Header("Refs")]
    public GameObject panel; // the popup root, disabled by default once resolved
    public Button acceptButton;
    public Button manageButton;

    public static bool HasShownBefore => PlayerPrefs.GetInt(ShownKey, 0) == 1;
    // Defaults to granted (personalized) only after Accept is pressed; a
    // player who's never answered has HasShownBefore == false, so this
    // default is never actually read before a real choice exists. Adjust if
    // your region's legal default should instead start as NOT granted.
    public static bool ConsentGranted => PlayerPrefs.GetInt(GrantedKey, 1) == 1;

    void Start()
    {
        if (acceptButton != null) acceptButton.onClick.AddListener(() => Resolve(true));
        if (manageButton != null) manageButton.onClick.AddListener(() => Resolve(false));

        if (HasShownBefore)
        {
            if (panel != null) panel.SetActive(false);
        }
        else
        {
            if (panel != null) panel.SetActive(true);
        }
    }

    // No ad SDK integrated yet on this branch (WebGL-first pass, ads come
    // back before publishing) -- this GameObject is disabled in the scene so
    // the popup never shows right now, but still records the player's choice
    // for whichever SDK gets wired in later. Re-add an AdsManager-equivalent
    // field here and call its consent/init method once one exists.
    void Resolve(bool granted)
    {
        PlayerPrefs.SetInt(ShownKey, 1);
        PlayerPrefs.SetInt(GrantedKey, granted ? 1 : 0);
        PlayerPrefs.Save();
        if (panel != null) panel.SetActive(false);
    }
}
