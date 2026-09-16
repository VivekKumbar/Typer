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
        {
            currentTarget = FindTarget(c);
            if (currentTarget != null && ComboManager.Instance != null)
                ComboManager.Instance.NotifyNewTarget(); // fresh word -> reset the "no mistakes" flag
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
            // Only fire a bullet if the enemy is still alive. The killing letter
            // shows the death burst instead of leaving an orphan bullet flying.
            if (!currentTarget.IsDefeated && tower != null)
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