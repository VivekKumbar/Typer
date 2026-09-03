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

    [Header("Scene to load for the real game")]
    public string realGameSceneName = "GameScene";

    void Start()
    {
        if (tutorialTextGroup != null)
        {
            tutorialTextGroup.alpha = 1f;
            TMP_Text label = tutorialTextGroup.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = tutorialText;
        }
        if (skipPopup != null) skipPopup.SetActive(false);
        if (completionPopup != null) completionPopup.SetActive(false);

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
        ShowCompletionPopup();
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
        SceneManager.LoadScene(realGameSceneName);
    }
}
