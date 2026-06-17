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

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        if (currentTarget != null && currentTarget.IsDefeated)
            currentTarget = null;
    }

    // Call this from a key button, or from the keyboard, or from anything.
    public void ReceiveChar(char c)
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        c = char.ToUpper(c);

        // Not locked on yet? Find the closest enemy whose next letter is c.
        if (currentTarget == null)
            currentTarget = FindTarget(c);

        if (currentTarget == null) return; // no match -> ignore the keypress

        bool correct = currentTarget.TryTypeLetter(c);
        if (correct)
        {
            if (ComboManager.Instance) ComboManager.Instance.RegisterHit();
            // Only fire a bullet if the enemy is still alive. The killing letter
            // shows the death burst instead of leaving an orphan bullet flying.
            if (!currentTarget.IsDefeated && tower != null)
                tower.FireAt(currentTarget.transform);
            if (currentTarget.IsDefeated) currentTarget = null; // word done, move on
        }
        else
        {
            // Wrong letter while locked on -> the combo breaks.
            if (ComboManager.Instance) ComboManager.Instance.RegisterMiss();
        }
        // Wrong key while locked: ignore it (forgiving, like ZType).
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