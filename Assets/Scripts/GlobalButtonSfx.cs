using UnityEngine;
using UnityEngine.UI;

// Put ONE of these anywhere in a scene (alongside SfxPlayer is fine). Ensures
// EVERY Button already in the scene gets a click sound with zero manual
// wiring, present or future: scans once at Start (after everything else has
// finished its own setup) and adds ButtonClickSound to any Button that
// doesn't already carry one.
//
// Buttons that come from a prefab with ButtonClickSound already baked in
// (e.g. Shop item cards, category buttons) are skipped here -- they're
// already covered the instant they're instantiated, this only needs to catch
// everything else (Main Menu buttons, in-game ability buttons, confirm
// popups, shop chrome, settings, ...).
public class GlobalButtonSfx : MonoBehaviour
{
    // Start, not Awake: run after every Button in the scene (including ones
    // built by other scripts' own Awake/Start) has actually been created.
    void Start()
    {
        foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (b.GetComponent<ButtonClickSound>() == null)
                b.gameObject.AddComponent<ButtonClickSound>();
    }
}
