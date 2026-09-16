using UnityEngine;
using UnityEngine.UI;

// Drop this on a Button PREFAB (not an instance) so every clone spawned from
// it automatically gets the click sound with zero further wiring -- used for
// buttons that are created at RUNTIME (shop category/item cards, upgrade
// draft cards), which a scene-wide scan like GlobalButtonSfx can't see since
// they don't exist yet when the scene loads.
//
// Existing, always-present buttons in a scene don't need this component at
// all -- GlobalButtonSfx finds and wires those automatically on scene Start.
[RequireComponent(typeof(Button))]
public class ButtonClickSfx : MonoBehaviour
{
    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(SfxPlayer.PlayButtonClick);
    }
}
