using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Put on the full-screen background Image of the Splash scene's Canvas (the
// Image already covers the whole screen and has Raycast Target on, so
// IPointerClickHandler picks up a tap anywhere with zero extra wiring).
//
// Flow: fade in -> hold for at least Min Display Duration (driving the shared
// LoadingBarUI + a ReadyPulse "breathing" prompt so it doesn't feel frozen) ->
// auto-advance to Main Menu the moment the minimum has elapsed, OR sooner if
// the player taps and Allow Tap To Skip is on (only honored after the
// minimum -- never lets a tap cut off before the screen's actually done).
public class SplashScreen : MonoBehaviour, IPointerClickHandler
{
    [Header("Placeholder art")]
    [Tooltip("SPLASH_IMAGE_PLACEHOLDER - replace me. Full-screen background Image; also the click target for tap-to-skip.")]
    public Image backgroundImage;

    [Header("Progress / \"alive while waiting\" feel")]
    [Tooltip("Shared progress-bar piece (see LoadingBarUI) -- filled with elapsed/MinDisplayDuration while waiting. Optional.")]
    public LoadingBarUI loadingBar;
    [Tooltip("Optional. A 'Loading...' (or similar) prompt using the same breathing pulse ReadyPulse already gives ability buttons, so the screen doesn't read as frozen. SetActive(true) is called on it automatically here.")]
    public ReadyPulse promptPulse;

    [Header("Timing")]
    [Tooltip("Minimum seconds the splash screen stays up, even if everything is instantly ready. 1.5-2.5s is a good range.")]
    [Range(0.5f, 5f)] public float minDisplayDuration = 2f;
    [Tooltip("Once Min Display Duration has passed, tapping anywhere on the background jumps straight to the Main Menu instead of waiting out any remaining time.")]
    public bool allowTapToSkip = true;
    [Tooltip("Seconds to fade the whole screen in from black at the start.")]
    [Range(0f, 2f)] public float fadeInDuration = 0.3f;
    [Tooltip("Seconds to fade the whole screen out to black before loading the Main Menu.")]
    [Range(0f, 2f)] public float fadeOutDuration = 0.3f;

    [Header("Destination")]
    [Tooltip("Scene to load once the splash finishes (name must be in Build Settings).")]
    public string nextSceneName = "MainMenu";

    private CanvasGroup canvasGroup;
    private bool canSkip;
    private bool proceeding;

    // Start, not Awake/OnEnable: no singleton dependency here, but matches
    // project convention -- and CanvasGroup needs to exist before Update
    // starts driving alpha, which Start guarantees ahead of the first frame.
    void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        if (promptPulse != null) promptPulse.SetActive(true);
        if (loadingBar != null) loadingBar.SnapTo01(0f); // start visibly empty, no carry-over from a previous show

        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        yield return FadeTo(1f, fadeInDuration);

        // No real async work to track here -- the target is just a straight
        // elapsed-time ramp over Min Display Duration. LoadingBarUI's own
        // Update() smooths the DISPLAYED value toward this target every frame
        // (capped by its Max Fill Rate Per Second), so even if a single frame's
        // Time.unscaledDeltaTime spikes (e.g. a hitch right after the fade-in),
        // the bar still can't visibly jump -- only the target moves; the
        // displayed value eases toward it.
        bool preloadFinished = false;
        BridgeStorageSync.Preload(() => preloadFinished = true);

        float t = 0f;
        while (t < minDisplayDuration || !preloadFinished)
        {
            t += Time.unscaledDeltaTime; // unscaled: the splash isn't gameplay, shouldn't depend on Time.timeScale
            if (loadingBar != null) loadingBar.SetTargetProgress01(Mathf.Clamp01(t / minDisplayDuration));
            yield return null;
        }
        if (loadingBar != null) loadingBar.SetTargetProgress01(1f);

        // Deliberately proceed the moment the minimum has elapsed even if the
        // DISPLAYED bar hasn't visually caught all the way up to 100% yet (per
        // spec: never force a wait just to watch the bar finish cosmetically).
        canSkip = allowTapToSkip; // only becomes tappable once the minimum has actually elapsed
        yield return Proceed();
    }

    // IPointerClickHandler -- fires on a tap/click anywhere on this Image
    // (Background covers the full screen), no Button/UnityEvent wiring needed.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!canSkip) return;
        StartCoroutine(Proceed());
    }

    IEnumerator Proceed()
    {
        if (proceeding) yield break; // tap during the auto-advance's own Proceed(), or a double-tap -- ignore, don't double-load
        proceeding = true;
        canSkip = false;

        // Notify platform that the initial loading phase is complete
        BridgeManager.SendGameReady();

        yield return FadeTo(0f, fadeOutDuration);
        SceneManager.LoadScene(nextSceneName);
    }

    IEnumerator FadeTo(float target, float duration)
    {
        float start = canvasGroup.alpha;
        if (duration <= 0f) { canvasGroup.alpha = target; yield break; }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
            yield return null;
        }
        canvasGroup.alpha = target;
    }
}
