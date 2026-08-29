using UnityEngine;
using UnityEngine.UI;

// Generic, zero-manual-wiring click sound for every Button already sitting
// in the scene at load time (Main Menu buttons, Settings, Shop panel chrome,
// Confirm/Consent popups, ability buttons, Game Over buttons, etc). Add ONE
// instance of this per scene (it needs no Inspector setup) and every Button
// it finds -- active or inactive -- gets wired automatically. Any Button
// added to the scene later doesn't need manual wiring either, as long as
// it's cloned from a prefab carrying ButtonClickSfx (see that file) --
// that covers the two dynamic cases in this project (shop cards, upgrade
// draft cards).
//
// The on-screen QWERTY keyboard is deliberately excluded: its keys already
// play PlayType()/PlayWrongKey() per letter (TypingController), so adding a
// generic click on top would double up sound on every keystroke.
public class GlobalButtonSfx : MonoBehaviour
{
    [Tooltip("Buttons under any of these roots are skipped. Point this at the on-screen keyboard's root (KeyboardPanel) so per-letter keys don't also get a generic click sound on top of their own type/wrong-key sound.")]
    public Transform[] excludeRoots;

    void Start()
    {
        foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            TryWire(b);
    }

    void TryWire(Button b)
    {
        if (b.GetComponent<ButtonClickSfx>() != null) return; // already self-wires via its prefab

        if (excludeRoots != null)
            foreach (Transform root in excludeRoots)
                if (root != null && b.transform.IsChildOf(root))
                    return;

        b.onClick.AddListener(SfxPlayer.PlayButtonClick);
    }
}
