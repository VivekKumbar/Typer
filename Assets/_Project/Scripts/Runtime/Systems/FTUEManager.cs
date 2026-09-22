using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Drives the FTUE (First Time User Experience) tutorial scene: spawns a
// small, fixed number of STATIC enemies (no march-toward-tower movement) one
// at a time, shows the tutorial text once, and offers a "Play For Real"
// popup once the sequence is cleared. The tower is invulnerable the whole
// time (see GameManager.invulnerable on this scene's Game Manager) --
// Enemy/TypingController/WordBank are reused as-is; this script only owns
// the spawn sequencing and the UI around them.
public class FTUEManager : MonoBehaviour
{
    [Header("Sequence")]
    [Tooltip("How many enemies the player must clear before the 'Play For Real' popup appears.")]
    public int enemyCount = 2;
    [Tooltip("Maximum word length for every FTUE enemy (the minimum stays the Enemy Prefab's own Min Letters, clamped down if needed so the range never inverts).")]
    public int maxWordLength = 5;

    [Header("Refs")]
    public Enemy enemyPrefab;
    public WordBank wordBank;
    [Tooltip("Aim target enemies face/walk toward -- same field WaveManager would use. Movement is zeroed out below, so this only affects facing.")]
    public Transform fortress;
    [Tooltip("Where each FTUE enemy appears -- keep this a few units from Fortress (> 0.5) so it never satisfies Enemy's own arrival-distance check.")]
    public Transform spawnPoint;

    [Header("Tutorial text")]
    public string tutorialText = "Type the glowing letters to attack";
    [Tooltip("Fades out after the first enemy is cleared, never shown again this scene.")]
    public CanvasGroup tutorialTextGroup;
    public float tutorialFadeDuration = 0.6f;

    [Header("Instruction panel")]
    [Tooltip("The bottom panel showing instructions. Fades out after the sequence clears.")]
    public CanvasGroup typeTheWordPanelGroup;

    [Header("Skip")]
    public Button skipButton;
    public GameObject skipPopup;
    public Button skipImReadyButton;
    public Button skipPlayTutorialAgainButton;

    [Header("Completion popup (shown after the last enemy clears)")]
    public GameObject completionPopup;
    public Button playForRealButton;
    [Tooltip("Small pause after the last enemy dies before the popup appears, so the kill juice reads first.")]
    public float delayBeforeCompletionPopup = 0.6f;

    [Header("'Nice work!' banner (optional transition, shown before the completion popup)")]
    [Tooltip("Scale/fade-in banner shown briefly after delayBeforeCompletionPopup, before the completion popup itself appears. Leave empty to skip straight to the popup (old behavior) -- purely a visual beat, doesn't affect the sequence below.")]
    public CanvasGroup niceWorkBanner;
    [Tooltip("How long the banner stays fully visible before the completion popup appears.")]
    public float niceWorkBannerHoldDuration = 1.5f;
    [Tooltip("Seconds for the banner's own scale/fade-in.")]
    public float niceWorkBannerAnimDuration = 0.3f;

    [Header("Scene to load for the real game")]
    public string realGameSceneName = "GameScene";

    void Start()
    {
        Time.timeScale = 1f;

        if (tutorialTextGroup != null)
        {
            tutorialTextGroup.alpha = 1f;
            TMP_Text label = tutorialTextGroup.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = tutorialText;
        }
        if (typeTheWordPanelGroup != null)
        {
            typeTheWordPanelGroup.gameObject.SetActive(true);
            typeTheWordPanelGroup.alpha = 1f;
        }

        if (skipPopup != null) skipPopup.SetActive(false);
        if (completionPopup != null) completionPopup.SetActive(false);
        if (niceWorkBanner != null) niceWorkBanner.gameObject.SetActive(false);

        if (skipButton != null) skipButton.onClick.AddListener(ShowSkipPopup);
        if (skipImReadyButton != null) skipImReadyButton.onClick.AddListener(GoToRealGame);
        if (skipPlayTutorialAgainButton != null) skipPlayTutorialAgainButton.onClick.AddListener(RestartTutorial);
        if (playForRealButton != null) playForRealButton.onClick.AddListener(GoToRealGame);

        StartCoroutine(RunSequence());
    }

    IEnumerator RunSequence()
    {
        for (int i = 0; i < enemyCount; i++)
        {
            Enemy enemy = SpawnOne();
            yield return new WaitUntil(() => enemy == null || enemy.IsDefeated);

            if (i == 0) FadeOutTutorialText(); // first completion only
        }

        yield return new WaitForSeconds(delayBeforeCompletionPopup);

        if (typeTheWordPanelGroup != null)
        {
            yield return StartCoroutine(FadeGroup(typeTheWordPanelGroup, 0.35f));
            typeTheWordPanelGroup.gameObject.SetActive(false);
        }

        yield return PlayNiceWorkBanner();
        ShowCompletionPopup();
    }

    // Purely a visual beat between the last kill and the completion popup --
    // simple scale-up + fade-in, hold, fade back out. A no-op with no banner
    // assigned, so this can never change the sequence's actual timing/logic
    // beyond the tunable hold duration above.
    IEnumerator PlayNiceWorkBanner()
    {
        if (niceWorkBanner == null) yield break;
        if (skipPopup != null && skipPopup.activeSelf) yield break; // same guard as ShowCompletionPopup: mid-Skip-decision, don't pile on

        niceWorkBanner.gameObject.SetActive(true);
        niceWorkBanner.alpha = 0f;
        niceWorkBanner.transform.localScale = Vector3.one * 0.7f;

        float t = 0f;
        while (t < niceWorkBannerAnimDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / niceWorkBannerAnimDuration);
            niceWorkBanner.alpha = p;
            niceWorkBanner.transform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, p);
            yield return null;
        }
        niceWorkBanner.alpha = 1f;
        niceWorkBanner.transform.localScale = Vector3.one;

        yield return new WaitForSeconds(niceWorkBannerHoldDuration);

        niceWorkBanner.gameObject.SetActive(false);
    }

    Enemy SpawnOne()
    {
        if (enemyPrefab == null || spawnPoint == null || fortress == null) return null;

        Enemy e = Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);
        int minLen = Mathf.Min(e.minLetters, maxWordLength);
        int maxLen = Mathf.Max(minLen, maxWordLength);
        string word = wordBank != null ? wordBank.GetWord(minLen, maxLen) : "WORD";

        e.Init(word, fortress, 0f);
        e.moveSpeed = 0f;             // completely static -- no march toward the tower
        e.highlightNextLetter = true; // pulsing glow on the next letter, FTUE-only
        return e;
    }

    void FadeOutTutorialText()
    {
        if (tutorialTextGroup != null) StartCoroutine(FadeGroup(tutorialTextGroup, tutorialFadeDuration));
    }

    IEnumerator FadeGroup(CanvasGroup group, float duration)
    {
        float start = group.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(start, 0f, t / duration);
            yield return null;
        }
        group.alpha = 0f;
    }

    void ShowSkipPopup()
    {
        if (completionPopup != null) completionPopup.SetActive(false);
        if (skipPopup != null) skipPopup.SetActive(true);
    }

    void ShowCompletionPopup()
    {
        if (skipPopup != null && skipPopup.activeSelf) return; // player's mid-Skip-decision; don't stack popups
        if (completionPopup != null) completionPopup.SetActive(true);
    }

    void RestartTutorial()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void GoToRealGame()
    {
        FtueState.MarkSeen();
        SaveManager.IsContinuing = false; // FTUE always leads into a fresh game, never a Continue
        MusicManager.PlayGameplayMusic(); // already the gameplay track from the FTUE itself -> no restart, just guarantees it
        SceneManager.LoadScene(realGameSceneName);
    }
}
