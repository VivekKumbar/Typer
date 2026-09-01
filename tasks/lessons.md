# Lessons & Patterns

## Unity UI & Lifecycle

### Inactive Popup Awake / SetActive Race Condition
- **Problem**: In Unity, if a popup GameObject starts inactive in the scene (`activeSelf = false`), activating it via `gameObject.SetActive(true)` immediately and synchronously invokes its `Awake()` method.
- **Mistake**: Calling `panel.SetActive(false)` in `Awake()` where `panel == gameObject` (or panel contains the popup) immediately re-disables the GameObject during the `Show()` call. Subsequent calls like `StartCoroutine(...)` fail with:
  `Coroutine couldn't be started because the game object '<Name>' is inactive!`
- **Rule / Solution**: 
  1. Do not hide/disable the panel inside `Awake()` if the popup object starts disabled or may be activated on demand.
  2. Always explicitly activate `gameObject.SetActive(true)` before starting UI coroutines.

### Shared Enum Declarations Across Scripts

### Rewarded Ad Early Close & Completion State Isolation
- **Problem**: If an ad callback (or test mock) triggers reward logic immediately upon ad show or treats early closure (`OnAdClosed`) as successful without checking whether `OnAdRewarded` / `UnityAdsShowCompletionState.COMPLETED` was explicitly returned, players who skip or close the ad early receive rewards.
- **Rule / Solution**: 
  1. Never grant rewards immediately when calling `ShowAd()`.

### System Namespace Directives with DateTime / TimeSpan
- **Problem**: Adding timestamp or cooldown calculation logic (`DateTime`, `TimeSpan`) to a script without explicitly verifying `using System;` at the top causes `CS0246: The type or namespace name 'TimeSpan' could not be found`.
- **Rule / Solution**: Whenever introducing `DateTime`, `TimeSpan`, `Action`, or other BCL primitives to a script, ensure `using System;` is present at the file header.
