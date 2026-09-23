using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

// Feeds the device's keyboard (Gboard on Android, iOS keyboard, or hardware
// keyboard in the editor / desktop / WebGL) into the game.
// Guarantees immediate responsiveness with zero click-to-focus requirements.
public class NativeKeyboardInput : MonoBehaviour
{
    public static NativeKeyboardInput Instance { get; private set; }

    [Header("Refs")]
    public TMP_InputField inputField;

    [Header("Behaviour")]
    [Tooltip("Re-open the keyboard if it loses focus, so the player can keep typing.")]
    public bool keepKeyboardOpen = true;

    private string previous = "";
    private int lastHardwareInputFrame = -1;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

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
        FocusInputField();
    }

    /// <summary>
    /// Explicitly restores selection and activates the input field in EventSystem.
    /// </summary>
    public void FocusInputField()
    {
        if (inputField == null) return;
        inputField.Select();
        inputField.ActivateInputField();
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(inputField.gameObject);
    }

    void OnChanged(string current)
    {
        // Avoid duplicate keystroke processing if hardware input was already consumed this frame
        if (Time.frameCount == lastHardwareInputFrame)
        {
            previous = current;
            return;
        }

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
        // 1. Direct hardware keyboard input: instantly responsive on PC/WebGL/Editor with 0 clicks required
        if (Input.anyKeyDown)
        {
            foreach (char c in Input.inputString)
            {
                if (char.IsLetter(c) && TypingController.Instance != null)
                {
                    lastHardwareInputFrame = Time.frameCount;
                    TypingController.Instance.ReceiveChar(c);
                    if (inputField != null)
                    {
                        inputField.text = "";
                        previous = "";
                    }
                }
            }
        }

        // 2. Ensure inputField stays focused for virtual keyboards (mobile IME)
        if (!keepKeyboardOpen || inputField == null) return;
        if (!inputField.isFocused)
        {
            FocusInputField();
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
