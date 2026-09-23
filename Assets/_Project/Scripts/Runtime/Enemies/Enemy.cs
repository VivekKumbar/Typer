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

    [Header("Identity")]
    [Tooltip("Optional type name for this enemy (e.g. \"Grunt\", \"Brute\", \"Runner\"). Used by EnemySkinApplier to support skins restricted to specific enemy types (ShopItem.appliesToEnemyType). Leave empty if you don't need per-type skins.")]
    public string enemyTypeId = "";

    [Header("Reward")]
    public int coinsOnDeath = 3;
    [Tooltip("No longer spawned by Die() — replaced by CoinFlyManager's fly-to-counter visual. Left here unused in case you want the old ground-hop coins for something else.")]
    public Coin coinPrefab;

    [Header("Progression & Multi-Phase Boss")]
    [Tooltip("The wave this enemy was spawned on, used to scale coin rewards.")]
    public int currentWaveNumber = 1;
    [Tooltip("Whether this enemy is a boss flagship.")]
    public bool isBoss = false;
    [Tooltip("Total armor / word phases (1 for normal enemies, 2+ for bosses).")]
    public int totalPhases = 1;
    [Tooltip("Current active phase (1-based).")]
    public int currentPhase = 1;
    private System.Func<int, string> nextPhaseWordProvider;

    [Header("Combat")]
    public int damage = 10;

    [Header("Word length for this enemy")]
    public int minLetters = 3;
    public int maxLetters = 5;

    [Header("FTUE only")]
    [Tooltip("When checked, the NEXT letter the player needs to type pulses/glows instead of just being untyped-colored -- used by FTUEScene to draw the eye to it. Leave unchecked for normal gameplay.")]
    public bool highlightNextLetter = false;
    [Tooltip("Pulse cycle speed for the glowing next letter.")]
    public float nextLetterPulseSpeed = 4f;

    [Header("Hit-stop")]
    [Tooltip("Words longer than this trigger the bigger hit-stop freeze + shake on kill.")]
    [Range(1, 15)] public int bigWordLength = 5;

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

    [Tooltip("Optional. Auto-found if left empty. Applies the equipped Enemy Skin — runs in Awake, before this so hit-flash/dissolve pick up the skinned material (see EnemySkinApplier).")]
    public EnemySkinApplier skinApplier;

    [Header("Juice")]
    public GameObject deathEffect;
    public float popScale = 1.3f;

    public string Word { get; private set; }
    public int TypedCount { get; private set; }
    public bool IsDefeated { get; private set; }
    public bool IsBlocked { get; private set; }   // held in place by a Blocker ally

    // TIME SINK multiplies this (1 = normal speed). Not a coroutine/tween —
    // just read every frame, so it always reflects whatever's currently active.
    public float SlowMultiplier = 1f;

    // Heavy Boots boss upgrade's passive near-tower slow (UpgradeManager writes
    // this every frame). Kept separate from SlowMultiplier so Time Sink's
    // temporary burst slow and this passive one never fight over the same field.
    public float NearTowerSlowMultiplier = 1f;

    // Display-only copy of Word, may have mixed case for the Night difficulty
    // profile. Word itself (used for matching) always stays uppercase.
    private string displayWord;

    public void SetBlocked(bool blocked) { IsBlocked = blocked; }

    private Transform target;
    private Vector3 baseScale;
    private float baseMoveSpeed;
    private Coroutine popCo;
    private EnemyHitFlash hitFlash;
    public EnemyHitFlash HitFlash => hitFlash;

    void Awake()
    {
        baseScale = transform.localScale;
        baseMoveSpeed = moveSpeed;
        if (enemyAnimator == null) enemyAnimator = GetComponentInChildren<EnemyAnimator>();
        if (dissolve == null) dissolve = GetComponentInChildren<EnemyDissolve>();
        if (skinApplier == null) skinApplier = GetComponentInChildren<EnemySkinApplier>();
        if (hitFlash == null) hitFlash = GetComponent<EnemyHitFlash>();
    }

    public void ResetState()
    {
        if (popCo != null) { StopCoroutine(popCo); popCo = null; }
        transform.localScale = baseScale;
        moveSpeed = baseMoveSpeed;
        IsDefeated = false;
        IsBlocked = false;
        TypedCount = 0;
        Word = "";
        displayWord = "";
        SlowMultiplier = 1f;
        NearTowerSlowMultiplier = 1f;
        nextPhaseWordProvider = null;
        if (label != null)
        {
            label.gameObject.SetActive(true);
            label.text = "";
        }
        if (dissolve != null)
        {
            dissolve.ResetDissolve();
        }
        if (enemyAnimator != null)
        {
            enemyAnimator.ResetAnimation();
        }
    }

    public void Init(string word, Transform fortress, float speedBonus, int waveNumber = 1, bool isBossEnemy = false, int phases = 1, System.Func<int, string> wordProvider = null)
    {
        Word = word.ToUpper();
        displayWord = Word;
        target = fortress;
        moveSpeed += speedBonus;
        currentWaveNumber = Mathf.Max(1, waveNumber);
        isBoss = isBossEnemy;
        totalPhases = Mathf.Max(1, phases);
        currentPhase = 1;
        nextPhaseWordProvider = wordProvider;
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
        // Repaints every frame ONLY for the FTUE's pulsing next-letter glow --
        // normal gameplay never sets highlightNextLetter, so this is a no-op
        // for every enemy outside FTUEScene.
        if (highlightNextLetter && !IsDefeated) RefreshLabel();

        if (IsDefeated || target == null) return;

        Vector3 dir = target.position - transform.position;
        dir.y = 0f;                       // stay level on the ground plane
        Vector3 flat = dir.normalized;

        // Walk (unless a Blocker ally is holding this enemy in place)
        if (!IsBlocked)
            transform.position += flat * moveSpeed * SlowMultiplier * NearTowerSlowMultiplier * Time.deltaTime;

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
        if (TypedCount >= Word.Length)
        {
            if (currentPhase < totalPhases && nextPhaseWordProvider != null)
            {
                AdvanceToNextPhase();
            }
            else
            {
                Die(true);
            }
        }
        return true;
    }

    private void AdvanceToNextPhase()
    {
        currentPhase++;
        string nextWord = nextPhaseWordProvider != null ? nextPhaseWordProvider(currentPhase) : null;
        if (string.IsNullOrEmpty(nextWord))
        {
            Die(true);
            return;
        }

        // Armor stripped feedback: hit stop, camera shake, sound effect
        CameraShake.ShakeKill();
        if (HitStop.Instance != null)
            HitStop.Stop(HitStop.Instance.smallKillFreeze);
        SfxPlayer.PlayKill();

        // Phase milestone reward: partial coin drop
        int phaseReward = CalculateCoinReward() / 2;
        if (phaseReward > 0)
        {
            CoinFlyManager.Spawn(transform.position, phaseReward);
            if (GameManager.Instance != null) GameManager.Instance.AddCoins(phaseReward);
            PopupManager.ShowCoins(transform.position, phaseReward);
        }

        // Switch word to the next armor/core phase
        Word = nextWord.ToUpper();
        displayWord = Word;
        TypedCount = 0;
        RefreshLabel();
        Pop();
    }

    public void Defeat() { Die(true); }

    public char NextChar => (!string.IsNullOrEmpty(Word) && TypedCount < Word.Length) ? Word[TypedCount] : '\0';
    public float DistanceToFortress => target ? Vector3.Distance(transform.position, target.position) : Mathf.Infinity;

    // Night difficulty profile ("Mixed Case Words"): scrambles the DISPLAYED
    // case only. Word (matching) stays uppercase, so typing is unaffected —
    // TryTypeLetter already compares case-insensitively via char.ToUpper.
    public void SetDisplayWordMixedCase()
    {
        if (string.IsNullOrEmpty(Word)) return;
        char[] chars = Word.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (Random.value < 0.5f) chars[i] = char.ToLower(chars[i]);
        displayWord = new string(chars);
        RefreshLabel();
    }

    // Head Start upgrade: advances TypedCount directly (no combo/SFX side
    // effects, since the player didn't actually type these letters).
    public void PreType(int count)
    {
        if (IsDefeated || string.IsNullOrEmpty(Word)) return;
        TypedCount = Mathf.Clamp(count, 0, Word.Length);
        RefreshLabel();
        if (TypedCount >= Word.Length) Die(true); // fully pre-typed (short word + high pre-type) is still a real kill
    }

    private static readonly System.Text.StringBuilder s_labelBuilder = new System.Text.StringBuilder(128);

    void RefreshLabel()
    {
        if (label == null) return;
        string src = string.IsNullOrEmpty(displayWord) ? Word : displayWord;
        if (string.IsNullOrEmpty(src))
        {
            label.text = "";
            return;
        }

        s_labelBuilder.Clear();

        if (TypedCount > 0)
        {
            s_labelBuilder.Append("<color=#46E36B>");
            s_labelBuilder.Append(src, 0, Mathf.Min(TypedCount, src.Length));
            s_labelBuilder.Append("</color>");
        }

        if (highlightNextLetter && TypedCount < src.Length)
        {
            float pulse = (Mathf.Sin(Time.time * nextLetterPulseSpeed) + 1f) * 0.5f;
            Color glow = Color.Lerp(new Color(1f, 0.82f, 0.2f), Color.white, pulse);
            string hex = ColorUtility.ToHtmlStringRGB(glow);

            s_labelBuilder.Append("<color=#").Append(hex).Append(">");
            s_labelBuilder.Append(src[TypedCount]);
            s_labelBuilder.Append("</color>");

            if (TypedCount + 1 < src.Length)
                s_labelBuilder.Append(src, TypedCount + 1, src.Length - (TypedCount + 1));
        }
        else
        {
            if (TypedCount < src.Length)
                s_labelBuilder.Append(src, TypedCount, src.Length - TypedCount);
        }

        if (totalPhases > 1)
        {
            s_labelBuilder.Append(" <color=#FFD700><size=70%>[")
                          .Append(currentPhase).Append("/").Append(totalPhases)
                          .Append("]</size></color>");
        }

        label.text = s_labelBuilder.ToString();
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

    public int CalculateCoinReward()
    {
        int baseReward = coinsOnDeath;
        if (isBoss)
        {
            // Massive boss jackpot scaling with wave
            baseReward += 25 + currentWaveNumber * 6;
        }
        else
        {
            // Dynamic standard enemy reward: scales with wave tier + word length
            baseReward += Mathf.FloorToInt((currentWaveNumber - 1) * 0.35f);
            if (!string.IsNullOrEmpty(Word) && Word.Length > 4)
            {
                baseReward += (Word.Length - 4);
            }
        }

        int rewardCap = isBoss ? 500 : Mathf.Max(40, 30 + currentWaveNumber * 3);
        if (UpgradeManager.Instance != null && UpgradeManager.Instance.coinMagnetUpgrade != null)
        {
            int cmLevel = UpgradeManager.Instance.LevelOf(UpgradeManager.Instance.coinMagnetUpgrade);
            if (cmLevel > 0) rewardCap += Mathf.RoundToInt(UpgradeManager.Instance.coinMagnetUpgrade.ValueForLevel(cmLevel));
        }

        return Mathf.Clamp(Mathf.RoundToInt(baseReward * ComboManager.Multiplier), baseReward, rewardCap);
    }

    void Die(bool rewardCoins)
    {
        if (IsDefeated) return;
        IsDefeated = true;
        Active.Remove(this);   // leaves the wave count immediately, so waves flow

        if (rewardCoins)
        {
            StatsManager.RecordEnemyKilled();
            if (deathEffect != null) Instantiate(deathEffect, transform.position, Quaternion.identity);

            bool big = Word.Length > bigWordLength;
            if (HitStop.Instance != null)
                HitStop.Stop(big ? HitStop.Instance.bigKillFreeze : HitStop.Instance.smallKillFreeze);
            CameraShake.ShakeKill();
            if (big && HitStop.Instance != null)
                CameraShake.Shake(HitStop.Instance.bigShakeDuration, HitStop.Instance.bigShakeMagnitude);
            SfxPlayer.PlayKill();

            int reward = CalculateCoinReward();
            if (reward > 0)
            {
                // Spawn BEFORE banking: CoinFlyManager marks the reward as
                // "pending in flight" synchronously inside Spawn(), so when
                // AddCoins() immediately fires OnCoinsChanged right after, its
                // reconciliation sees the full amount already accounted for
                // and lets the fly-in animate it in — instead of finding an
                // "unexplained" coin gain and snapping the counter instantly.
                // Either way the real economy is banked this same frame, so
                // it's correct even if the visual never finishes.
                CoinFlyManager.Spawn(transform.position, reward);
                GameManager.Instance.AddCoins(reward);
                PopupManager.ShowCoins(transform.position, reward); // one "+N", not many "+1"s
            }

            if (ComboManager.Instance != null && ComboManager.Instance.CurrentWordPerfect)
            {
                PopupManager.ShowPerfect(transform.position);
                StatsManager.RecordPerfectWord(); // feeds WordsTypedPerfectly-based achievements
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
            StartCoroutine(ReturnToPoolAfterDelay(wait));
        }
        else
        {
            DespawnOrReturn();
        }
    }

    private IEnumerator ReturnToPoolAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        DespawnOrReturn();
    }

    private void DespawnOrReturn()
    {
        if (EnemyPool.Instance != null)
        {
            EnemyPool.Instance.Return(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}