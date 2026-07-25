using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

// One marching enemy carrying a word. Rotates to face the fortress as it walks,
// with juice: scale-pop per correct letter, particles + shake + sound on death.
public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> Active = new List<Enemy>();

    [Header("Refs")]
    public TMP_Text label;

    [Header("Reward")]
    public int coinsOnDeath = 3;
    public Coin coinPrefab;

    [Header("Combat")]
    public int damage = 10;

    [Header("Word length for this enemy")]
    public int minLetters = 3;
    public int maxLetters = 5;

    [Header("Movement")]
    public float moveSpeed = 1.5f;

    [Header("Facing (3D models)")]
    [Tooltip("How fast it turns to face the tower. Higher = snappier.")]
    public float turnSpeed = 10f;
    [Tooltip("Fix for models that don't face +Z. Try 180 if it walks backwards, 90 or -90 if sideways.")]
    public float modelYawOffset = 0f;
    [Tooltip("Uncheck if you don't want the enemy to rotate at all (e.g. flat sprites).")]
    public bool rotateTowardsTarget = true;

    [Header("Animation")]
    [Tooltip("Optional. Assign this enemy's EnemyAnimator (holds its Walk/Death clips).")]
    public EnemyAnimator enemyAnimator;
    [Tooltip("Extra seconds to linger after death so the animation can play. 0 = use the clip's own length.")]
    public float deathLinger = 0f;

    [Tooltip("Optional dissolve-on-death effect. Auto-found if left empty.")]
    public EnemyDissolve dissolve;

    [Header("Juice")]
    public GameObject deathEffect;
    public float popScale = 1.3f;

    public string Word { get; private set; }
    public int TypedCount { get; private set; }
    public bool IsDefeated { get; private set; }
    public bool IsBlocked { get; private set; }   // held in place by a Blocker ally

    public void SetBlocked(bool blocked) { IsBlocked = blocked; }

    private Transform target;
    private Vector3 baseScale;
    private Coroutine popCo;

    void Awake()
    {
        baseScale = transform.localScale;
        if (enemyAnimator == null) enemyAnimator = GetComponentInChildren<EnemyAnimator>();
        if (dissolve == null) dissolve = GetComponentInChildren<EnemyDissolve>();
    }

    public void Init(string word, Transform fortress, float speedBonus)
    {
        Word = word.ToUpper();
        target = fortress;
        moveSpeed += speedBonus;
        TypedCount = 0;
        IsDefeated = false;
        RefreshLabel();

        // Face the tower immediately on spawn (no initial spin)
        if (rotateTowardsTarget && target != null)
        {
            Vector3 d = target.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(d) * Quaternion.Euler(0f, modelYawOffset, 0f);
        }

        if (!Active.Contains(this)) Active.Add(this); // only join once it has a word
    }

    void OnDisable() { Active.Remove(this); }

    void Update()
    {
        if (IsDefeated || target == null) return;

        Vector3 dir = target.position - transform.position;
        dir.y = 0f;                       // stay level on the ground plane
        Vector3 flat = dir.normalized;

        // Walk (unless a Blocker ally is holding this enemy in place)
        if (!IsBlocked)
            transform.position += flat * moveSpeed * Time.deltaTime;

        // Turn to face the tower
        if (rotateTowardsTarget && flat.sqrMagnitude > 0.001f)
        {
            Quaternion want = Quaternion.LookRotation(flat) * Quaternion.Euler(0f, modelYawOffset, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, turnSpeed * Time.deltaTime);
        }

        if (Vector3.Distance(transform.position, target.position) < 0.5f)
        {
            GameManager.Instance.DamageFortress(damage, transform.position);
            CameraShake.ShakeHit();
            SfxPlayer.PlayHit();
            Die(false);
        }
    }

    public bool TryTypeLetter(char c)
    {
        if (IsDefeated || string.IsNullOrEmpty(Word) || TypedCount >= Word.Length) return false;
        if (char.ToUpper(c) != Word[TypedCount]) return false;

        TypedCount++;
        RefreshLabel();
        Pop();
        SfxPlayer.PlayType();
        if (TypedCount >= Word.Length) Die(true);
        return true;
    }

    public void Defeat() { Die(true); }

    public char NextChar => (!string.IsNullOrEmpty(Word) && TypedCount < Word.Length) ? Word[TypedCount] : '\0';
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
        Active.Remove(this);   // leaves the wave count immediately, so waves flow

        if (rewardCoins)
        {
            if (deathEffect != null) Instantiate(deathEffect, transform.position, Quaternion.identity);
            CameraShake.ShakeKill();
            SfxPlayer.PlayKill();

            if (coinPrefab != null)
            {
                int reward = Mathf.Clamp(Mathf.RoundToInt(coinsOnDeath * ComboManager.Multiplier), coinsOnDeath, 30);
                for (int i = 0; i < reward; i++)
                    Instantiate(coinPrefab, transform.position, Quaternion.identity).Launch();
            }
        }

        // Play the death animation + dissolve, then clean up
        float wait = deathLinger;
        if (enemyAnimator != null)
        {
            enemyAnimator.PlayDeath();
            if (wait <= 0f) wait = enemyAnimator.DeathLength;
        }
        if (dissolve != null)
        {
            dissolve.Dissolve();
            wait = Mathf.Max(wait, dissolve.TotalTime); // don't destroy mid-dissolve
        }

        if (wait > 0f)
        {
            if (label != null) label.gameObject.SetActive(false); // hide the word
            Destroy(gameObject, wait);
        }
        else Destroy(gameObject);
    }
}