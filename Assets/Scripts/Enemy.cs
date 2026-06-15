using System.Collections.Generic;
using UnityEngine;
using TMPro;

// One marching enemy that carries a word. Maintains a global list of all
// alive enemies so the TypingController can search for a match.
public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> Active = new List<Enemy>();

    [Header("Refs")]
    public TMP_Text label;          // world-space TextMeshPro that shows the word

    [Header("Reward")]
    public int coinsOnDeath = 3;
    public Coin coinPrefab;

    public string Word { get; private set; }
    public int TypedCount { get; private set; }
    public bool IsDefeated { get; private set; }

    private Transform target;       // the fortress
    private float moveSpeed = 1f;

    public void Init(string word, Transform fortress, float speed)
    {
        Word = word.ToUpper();
        target = fortress;
        moveSpeed = speed;
        TypedCount = 0;
        IsDefeated = false;
        RefreshLabel();
    }

    void OnEnable()  { if (!Active.Contains(this)) Active.Add(this); }
    void OnDisable() { Active.Remove(this); }

    void Update()
    {
        if (IsDefeated || target == null) return;

        Vector3 dir = (target.position - transform.position).normalized;
        transform.position += dir * moveSpeed * Time.deltaTime;

        if (Vector3.Distance(transform.position, target.position) < 0.5f)
        {
            GameManager.Instance.DamageFortress(Word.Length); // longer word = bigger hit
            Die(false);
        }
    }

    // Returns true if c was the next correct letter of this enemy's word.
    public bool TryTypeLetter(char c)
    {
        if (IsDefeated || TypedCount >= Word.Length) return false;
        if (char.ToUpper(c) != Word[TypedCount]) return false;

        TypedCount++;
        RefreshLabel();
        if (TypedCount >= Word.Length) Die(true);
        return true;
    }

    public char NextChar => TypedCount < Word.Length ? Word[TypedCount] : '\0';
    public float DistanceToFortress => target ? Vector3.Distance(transform.position, target.position) : Mathf.Infinity;

    void RefreshLabel()
    {
        if (label == null) return;
        string typed = Word.Substring(0, TypedCount);
        string rest  = Word.Substring(TypedCount);
        // TMP rich text: typed letters glow green, the rest stay white.
        label.text = "<color=#46E36B>" + typed + "</color>" + rest;
    }

    void Die(bool rewardCoins)
    {
        if (IsDefeated) return;
        IsDefeated = true;
        Active.Remove(this);

        if (rewardCoins && coinPrefab != null)
        {
            for (int i = 0; i < coinsOnDeath; i++)
            {
                Coin coin = Instantiate(coinPrefab, transform.position, Quaternion.identity);
                coin.Launch();
            }
        }
        Destroy(gameObject);
    }
}
