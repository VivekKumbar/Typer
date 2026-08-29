using UnityEngine;
using UnityEngine.UI;

// Generic, reusable, self-wiring: plays SfxPlayer.PlayButtonClick() whenever
// the Button on THIS GameObject is pressed. No Inspector OnClick hookup
// needed -- it adds its own listener in Awake.
//
// Two ways this ends up on a button:
//   1. Baked directly into a button PREFAB (e.g. ItemCard/CategoryButton) so
//      every runtime-instantiated copy is covered automatically, forever,
//      with zero extra code.
//   2. Auto-attached at scene load by GlobalButtonSfx to any Button that
//      doesn't already carry one -- covers every button already sitting in a
//      scene (Main Menu, ability buttons, confirm popups, ...) with no manual
//      per-button wiring either.
[RequireComponent(typeof(Button))]
public class ButtonClickSound : MonoBehaviour
{
    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() => SfxPlayer.PlayButtonClick());
    }
}
