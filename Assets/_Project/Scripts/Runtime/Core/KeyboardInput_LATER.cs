using UnityEngine;

// OPTIONAL - enable this LATER for hardware-keyboard support.
// Notice it just forwards characters to the SAME TypingController method,
// so nothing else needs to change. Requires legacy Input enabled
// (Project Settings > Player > Active Input Handling = "Both" or "Input Manager").
public class KeyboardInput_LATER : MonoBehaviour
{
    void Update()
    {
        if (!Input.anyKeyDown) return;
        foreach (char c in Input.inputString)
            if (char.IsLetter(c))
                TypingController.Instance.ReceiveChar(c);
    }
}
