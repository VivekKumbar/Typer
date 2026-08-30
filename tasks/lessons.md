# Lessons & Patterns

## Unity UI & Lifecycle

### Inactive Popup Awake / SetActive Race Condition
- **Problem**: In Unity, if a popup GameObject starts inactive in the scene (`activeSelf = false`), activating it via `gameObject.SetActive(true)` immediately and synchronously invokes its `Awake()` method.
- **Mistake**: Calling `panel.SetActive(false)` in `Awake()` where `panel == gameObject` (or panel contains the popup) immediately re-disables the GameObject during the `Show()` call. Subsequent calls like `StartCoroutine(...)` fail with:
  `Coroutine couldn't be started because the game object '<Name>' is inactive!`
- **Rule / Solution**: 
  1. Do not hide/disable the panel inside `Awake()` if the popup object starts disabled or may be activated on demand.
  2. Always explicitly activate `gameObject.SetActive(true)` before starting UI coroutines.
  3. Guard coroutines with `if (gameObject.activeInHierarchy)` and provide an immediate visual fallback if inactive.
