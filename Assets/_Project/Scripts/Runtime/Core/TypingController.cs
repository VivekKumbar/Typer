using UnityEngine;

// The heart of the game. Receives one character at a time from ANY source
// (on-screen buttons now, hardware keyboard later) and handles target-locking
// + letter matching, exactly like ZType's auto-lock.
public class TypingController : MonoBehaviour
{
    public static TypingController Instance { get; private set; }

    [Header("Refs")]
    public Tower tower; // fires the visual bullet tracers

    private Enemy currentTarget;
    public Enemy CurrentTarget => currentTarget;   // so the spotlight can follow it

    private Camera mainCamera;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        mainCamera = Camera.main;
    }

    public void ClearTarget() => currentTarget = null;

    void Update()
    {
        if (currentTarget != null && currentTarget.IsDefeated)
            currentTarget = null;

        // Mouse click target acquisition: clicking an enemy locks onto it and processes the first letter
        if (Input.GetMouseButtonDown(0))
        {
            bool overUI = UnityEngine.EventSystems.EventSystem.current != null &&
                          UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            if (!overUI)
            {
                Enemy clicked = FindEnemyUnderCursor();
                if (clicked != null)
                {
                    LockAndTypeFirst(clicked);
                }
            }
        }
    }

    private Enemy FindEnemyUnderCursor()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return null;

        Vector2 mouse = Input.mousePosition;
        Enemy best = null;
        float bestDist = 90f; // pixel radius tolerance

        foreach (Enemy e in Enemy.Active)
        {
            if (e == null || e.IsDefeated) continue;
            Vector3 screen = mainCamera.WorldToScreenPoint(e.transform.position);
            if (screen.z > 0f)
            {
                float dist = Vector2.Distance(mouse, (Vector2)screen);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = e;
                }
            }
        }
        return best;
    }

    public void LockAndTypeFirst(Enemy enemy)
    {
        if (enemy == null || enemy.IsDefeated) return;
        currentTarget = enemy;
        if (ComboManager.Instance != null)
            ComboManager.Instance.NotifyNewTarget();

        if (NativeKeyboardInput.Instance != null)
            NativeKeyboardInput.Instance.FocusInputField();

        char firstChar = enemy.NextChar;
        if (firstChar != '\0')
        {
            ReceiveChar(firstChar);
        }
    }

    public void AutoTargetIfNone()
    {
        if (currentTarget != null && !currentTarget.IsDefeated) return;
        Enemy best = null;
        float bestDist = Mathf.Infinity;
        foreach (Enemy e in Enemy.Active)
        {
            if (e == null || e.IsDefeated) continue;
            if (e.DistanceToFortress < bestDist)
            {
                bestDist = e.DistanceToFortress;
                best = e;
            }
        }
        if (best != null)
        {
            currentTarget = best;
            if (ComboManager.Instance != null)
                ComboManager.Instance.NotifyNewTarget();
        }
    }

    // Call this from a key button, or from the keyboard, or from anything.
    public void ReceiveChar(char c)
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        c = char.ToUpper(c);

        // Not locked on yet, or locked on with 0 letters typed?
        if (currentTarget == null || currentTarget.TypedCount == 0)
        {
            // If current target does not match c, search for an enemy whose next letter is c
            if (currentTarget == null || currentTarget.NextChar != c)
            {
                Enemy matching = FindTarget(c);
                if (matching != null)
                {
                    currentTarget = matching;
                    if (ComboManager.Instance != null)
                        ComboManager.Instance.NotifyNewTarget();
                }
            }
        }

        if (currentTarget == null)
        {
            // No enemy anywhere matches this key -> still a miss, just not one
            // ComboManager tracks (there's no locked word to break combo on).
            SfxPlayer.PlayWrongKey();
            return;
        }

        bool correct = currentTarget.TryTypeLetter(c);
        if (correct)
        {
            if (ComboManager.Instance) ComboManager.Instance.RegisterHit();
            StatsManager.RecordCorrectLetter();
            if (TimeSinkManager.Instance) TimeSinkManager.Instance.AddCharge();
            // Fire bullet and play shooting sound immediately on every correct letter
            if (tower != null && currentTarget != null)
                tower.FireAt(currentTarget);
            if (currentTarget.IsDefeated) currentTarget = null; // word done, move on
        }
        else
        {
            // Wrong letter while locked on -> the combo breaks.
            if (ComboManager.Instance) ComboManager.Instance.RegisterMiss();
            StatsManager.RecordMissedLetter();
            SfxPlayer.PlayWrongKey();
        }
        // Wrong key while locked: ignore it otherwise (forgiving, like ZType).
    }

    Enemy FindTarget(char c)
    {
        Enemy best = null;
        float bestDist = Mathf.Infinity;
        foreach (Enemy e in Enemy.Active)
        {
            if (e.IsDefeated || e.NextChar != c) continue;
            if (e.DistanceToFortress < bestDist)
            {
                bestDist = e.DistanceToFortress;
                best = e;
            }
        }
        return best;
    }
}