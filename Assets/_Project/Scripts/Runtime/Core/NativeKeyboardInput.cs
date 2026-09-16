using UnityEngine;
using TMPro;

// Feeds the device's NATIVE keyboard (Gboard on Android, iOS keyboard, or a
// hardware keyboard in the editor) into the game. It keeps a TMP_InputField
// focused and forwards each newly typed letter to the TypingController — the
// same method the on-screen keys use, so both input styles can coexist.
public class NativeKeyboardInput : MonoBehaviour
{
    [Header("Refs")]
    public TMP_InputField inputField;

    [Header("Behaviour")]
    [Tooltip("Re-open the keyboard if it loses focus, so the player can keep typing.")]
    public bool keepKeyboardOpen = true;

    private string previous = "";

    void Start()
    {
        if (inputField == null) return;
        inputField.onValueChanged.AddListener(OnChanged);
        inputField.text = "";
        previous = "";
        OpenKeyboard();
    }

    public void OpenKeyboard()
    {
        if (inputField == null) return;
        inputField.ActivateInputField(); // opens the native keyboard on a device
        inputField.Select();
    }

    void OnChanged(string current)
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        // Forward only the newly appended letters (ignore backspaces)
        if (current.Length > previous.Length)
        {
            string added = current.Substring(previous.Length);
            foreach (char c in added)
                if (char.IsLetter(c) && TypingController.Instance != null)
                    TypingController.Instance.ReceiveChar(c);
        }
        previous = current;

        // Keep the hidden buffer from growing forever
        if (current.Length >= 32)
        {
            inputField.text = "";
            previous = "";
        }
    }

    void Update()
    {
        if (!keepKeyboardOpen || inputField == null) return;
        if (!inputField.isFocused) inputField.ActivateInputField();
    }
}