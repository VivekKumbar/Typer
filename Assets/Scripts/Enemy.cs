using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

// One marching enemy carrying a word. Now with JUICE: a scale-pop on each
// correct letter, a particle burst + screen shake + sound on death, and a
// bigger shake + boom when it reaches the fortress.
public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> Active = new List<Enemy>();

    [Header("Refs")]
    public TMP_Text label;

    [Header("Reward")]
    public int coinsOnDeath = 3;
    public Coin coinPrefab;

    [Header("Juice")]
    public GameObject deathEffect;   // a Particle System prefab (optional)
    public float popScale = 1.3f;    // how much it punches up on a correct letter

    public string Word { get; private set; }
    public int TypedCount { get; private set; }
    public bool IsDefeated { get; private set; }

    private Transform target;
    private float moveSpeed = 1f;

    private Vector3 baseScale;
    private Coroutine popCo;

    void Awake() { baseScale = transform.localScale; }

    public void Init(string word, Transform fortress, float speed)
    {
        Word = word.ToUpper();
        target = fortress;
        moveSpeed = speed;
        TypedCount = 0;
        IsDefeated = false;
        RefreshLabel();
    }

    void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
    void OnDisable() { Active.Remove(this); }

    void Update()
    {
        if (IsDefeated || target == null) return;

        Vector3 dir = (target.position - transform.position).normalized;
        transform.position += dir * moveSpeed * Time.deltaTime;

        if (Vector3.Distance(transform.position, target.position) < 0.5f)
        {
            GameManager.Instance.DamageFortress(Word.Length);
            CameraShake.ShakeHit();  // big shake — tune on the Main Camera
            SfxPlayer.PlayHit();
            Die(false);
        }
    }

    public bool TryTypeLetter(char c)
    {
        if (IsDefeated || TypedCount >= Word.Length) return false;
        if (char.ToUpper(c) != Word[TypedCount]) return false;

        TypedCount++;
        RefreshLabel();
        Pop();                 // juice: punch the scale
        SfxPlayer.PlayType();  // juice: blip
        if (TypedCount >= Word.Length) Die(true);
        return true;
    }

    public char NextChar => TypedCount < Word.Length ? Word[TypedCount] : '\0';
    public float DistanceToFortress => target ? Vector3.Distance(transform.position, target.position) : Mathf.Infinity;

    void RefreshLabel()
    {
        if (label == null) return;
        string typed = Word.Substring(0, TypedCount);
        string rest = Word.Substring(TypedCount);
        label.text = "<color=#46E36B>" + typed + "</color>" + rest;
    }

    void Pop()
    {
        if (popCo != null) StopCoroutine(popCo);
        popCo = StartCoroutine(PopRoutine());
    }

    IEnumerator PopRoutine()
    {
        Vector3 big = baseScale * popScale;
        float t = 0f, dur = 0.12f;
        while (t < dur)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(big, baseScale, t / dur);
            yield return null;
        }
        transform.localScale = baseScale;
        popCo = null;
    }

    void Die(bool rewardCoins)
    {
        if (IsDefeated) return;
        IsDefeated = true;
        Active.Remove(this);

        if (rewardCoins)
        {
            if (deathEffect != null) Instantiate(deathEffect, transform.position, Quaternion.identity);
            CameraShake.ShakeKill(); // small shake — tune on the Main Camera
            SfxPlayer.PlayKill();

            if (coinPrefab != null)
                for (int i = 0; i < coinsOnDeath; i++)
                    Instantiate(coinPrefab, transform.position, Quaternion.identity).Launch();
        }
        Destroy(gameObject);
    }
}