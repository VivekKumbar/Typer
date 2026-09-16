using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Put this on a manager object under the HUD Canvas. Other scripts call
// CoinFlyManager.Spawn(worldKillPos, amount) on a kill; it spawns a capped
// number of coin visuals that burst out, hang, then fly to the coin counter
// along a curved path, ticking the counter's DISPLAYED total up (and bouncing
// it) as each one lands. The REAL total is banked separately and immediately
// by the caller (see Enemy.Die) — this manager only animates the catch-up
// display, so the economy is correct even if the visuals never finish.
public class CoinFlyManager : MonoBehaviour
{
    public static CoinFlyManager Instance { get; private set; }

    [Header("Master control")]
    [Tooltip("Uncheck to disable coin-fly visuals entirely (the real coin total is unaffected either way).")]
    public bool enableCoinFly = true;

    [Header("Coin visual")]
    [Tooltip("Drag your own coin prefab here (must have an Image component on its root). Takes priority over Coin Sprite below.")]
    public Image coinPrefab;
    [Tooltip("Or just a coin sprite if you don't have a prefab — used only when Coin Prefab is empty, applied to a plain generated UI Image.")]
    public Sprite coinSprite;
    [Tooltip("Uniform size (pixels) each flying coin is scaled to, regardless of the source image's native size.")]
    [Range(8f, 200f)] public float coinSize = 48f;
    [Tooltip("Random variance on Coin Size, as a fraction (0.15 = +/-15%).")]
    [Range(0f, 1f)] public float coinSizeRandomness = 0.15f;

    [Header("Targets")]
    [Tooltip("Screen-space Canvas RectTransform (or a full-stretch child of it) that flying coins are parented to and animate within.")]
    public RectTransform flyLayer;
    [Tooltip("Where coins fly TO — usually the coin icon or counter group's RectTransform.")]
    public RectTransform counterTarget;
    [Tooltip("The coin counter's text — ticks up by each coin's share as it lands.")]
    public TMP_Text counterText;
    [Tooltip("Optional. Auto-found on the same GameObject as Counter Text if left empty. Bounced each time a coin lands.")]
    public CounterBounce counterBounce;
    [Tooltip("The 3D camera used to convert the kill's world position to a screen point. Defaults to Camera.main if left empty.")]
    public Camera worldCamera;

    [Header("Spawn cap")]
    [Tooltip("Max flying coin visuals per kill, regardless of the actual reward amount. Keeps big rewards from spawning hundreds of coins.")]
    [Range(1, 20)] public int maxCoinsPerKill = 8;

    [Header("Burst-out (spawn pop)")]
    [Tooltip("How far (pixels) each coin pops outward from the kill position before hanging/flying.")]
    [Range(0f, 300f)] public float burstOutDistance = 60f;
    [Tooltip("Random variance on Burst Out Distance and direction, as a fraction.")]
    [Range(0f, 1f)] public float burstRandomness = 0.4f;
    [Tooltip("Seconds the burst-out pop takes.")]
    [Range(0.02f, 0.5f)] public float burstOutDuration = 0.12f;

    [Header("Hang + stagger")]
    [Tooltip("Seconds each coin pauses after the burst before flying to the counter.")]
    [Range(0f, 1f)] public float hangTime = 0.08f;
    [Tooltip("Extra random pause (0 to this many seconds) added per coin on top of Hang Time, so arrivals stream in rather than clump together.")]
    [Range(0f, 1f)] public float arrivalJitterTime = 0.18f;

    [Header("Flight")]
    [Tooltip("Seconds each coin takes to fly from its burst position to the counter.")]
    [Range(0.1f, 2f)] public float flyDuration = 0.6f;
    [Tooltip("Shapes the flight over time (X: 0-1 progress, Y: 0-1 eased progress). Default accelerates (slow start, fast finish) — flatten the start further for more \"whip\", or ease both ends for a gentler arrival.")]
    public AnimationCurve flyEaseCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 2f));
    [Tooltip("How far (pixels) the flight path bulges sideways at its midpoint, for a curved arc instead of a straight line. Left/right is randomized per coin.")]
    [Range(0f, 500f)] public float arcHeight = 150f;

    [Header("Pooling")]
    [Tooltip("How many coin visuals to pre-instantiate at Start, to avoid first-use hitches.")]
    public int prewarmCount = 8;

    private readonly Queue<Image> pool = new Queue<Image>();
    private int displayedTotal;
    private int pendingInFlight; // sum of shares currently mid-flight, not yet landed

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (counterBounce == null && counterText != null)
            counterBounce = counterText.GetComponent<CounterBounce>();
        if (worldCamera == null)
            worldCamera = Camera.main;

        if (flyLayer != null)
            for (int i = 0; i < prewarmCount; i++)
                pool.Enqueue(CreateInstance());

        if (GameManager.Instance != null)
        {
            displayedTotal = GameManager.Instance.coins;
            GameManager.Instance.OnCoinsChanged += HandleRealCoinsChanged;
        }
        RefreshText();
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnCoinsChanged -= HandleRealCoinsChanged;
    }

    // Catches coin additions that DIDN'T go through Spawn() (e.g. a future coin
    // source that calls GameManager.AddCoins directly) so the display never
    // permanently lags behind the real total — it just snaps in the difference
    // that no flying coin is already accounting for.
    void HandleRealCoinsChanged(int realTotal)
    {
        int expected = displayedTotal + pendingInFlight;
        if (realTotal > expected)
        {
            displayedTotal += realTotal - expected;
            RefreshText();
        }
    }

    void RefreshText()
    {
        if (counterText != null) counterText.text = displayedTotal.ToString();
    }

    public static void Spawn(Vector3 worldKillPos, int amount)
    {
        if (Instance) Instance.SpawnInternal(worldKillPos, amount);
    }

    void SpawnInternal(Vector3 worldPos, int amount)
    {
        if (!enableCoinFly || amount <= 0 || flyLayer == null || counterTarget == null) return;

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null) return;

        int count = Mathf.Clamp(amount, 1, maxCoinsPerKill);
        int[] shares = SplitShares(amount, count);

        Vector2 screenPoint = cam.WorldToScreenPoint(worldPos);
        Camera uiCam = GetUiCameraForConversion();
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(flyLayer, screenPoint, uiCam, out Vector2 spawnLocal))
            return; // unprojectable (e.g. behind the camera) — skip rather than spawn somewhere wrong

        foreach (int share in shares)
            SpawnOneCoin(spawnLocal, share);
    }

    Camera GetUiCameraForConversion()
    {
        Canvas canvas = flyLayer.GetComponentInParent<Canvas>();
        if (canvas == null) return null;
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    int[] SplitShares(int amount, int count)
    {
        int[] shares = new int[count];
        int baseShare = amount / count;
        int remainder = amount % count;
        for (int i = 0; i < count; i++)
            shares[i] = baseShare + (i < remainder ? 1 : 0);
        return shares;
    }

    void SpawnOneCoin(Vector2 spawnLocal, int share)
    {
        Image coin = pool.Count > 0 ? pool.Dequeue() : CreateInstance();
        RectTransform rt = coin.rectTransform;
        rt.SetParent(flyLayer, false);
        rt.anchoredPosition = spawnLocal;

        float size = Mathf.Max(1f, coinSize * (1f + Random.Range(-coinSizeRandomness, coinSizeRandomness)));
        rt.sizeDelta = Vector2.one * size;

        coin.gameObject.SetActive(true);
        StartCoroutine(AnimateCoin(coin, share));
    }

    Image CreateInstance()
    {
        Image img;
        if (coinPrefab != null)
        {
            img = Instantiate(coinPrefab, flyLayer);
        }
        else
        {
            GameObject go = new GameObject("FlyingCoin", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(flyLayer, false);
            img = go.GetComponent<Image>();
            img.sprite = coinSprite;
        }
        img.raycastTarget = false; // must never intercept clicks
        img.gameObject.SetActive(false);
        return img;
    }

    IEnumerator AnimateCoin(Image coin, int share)
    {
        RectTransform rt = coin.rectTransform;
        pendingInFlight += share;

        Vector2 start = rt.anchoredPosition;

        // Burst out: pop outward with random spread
        Vector2 dir = Random.insideUnitCircle.normalized;
        float dist = Mathf.Max(0f, burstOutDistance * (1f + Random.Range(-burstRandomness, burstRandomness)));
        Vector2 burstPos = start + dir * dist;

        float bt = 0f;
        while (bt < burstOutDuration)
        {
            bt += Time.unscaledDeltaTime;
            rt.anchoredPosition = Vector2.Lerp(start, burstPos, Mathf.Clamp01(bt / burstOutDuration));
            yield return null;
        }
        rt.anchoredPosition = burstPos;

        // Hang, with random extra stagger so a burst of coins streams in rather
        // than arriving all at once.
        float hang = hangTime + Random.Range(0f, arrivalJitterTime);
        if (hang > 0f) yield return new WaitForSecondsRealtime(hang);

        // Fly to the counter along a curved arc, eased per Fly Ease Curve.
        // Recomputed here (not cached) so it's accurate even if the counter
        // moved since this coin was spawned.
        Vector2 flyStart = rt.anchoredPosition;
        Vector3 counterLocal = flyLayer.InverseTransformPoint(counterTarget.position);
        Vector2 flyEnd = new Vector2(counterLocal.x, counterLocal.y);
        Vector2 along = flyEnd - flyStart;
        Vector2 perp = new Vector2(-along.y, along.x).normalized;
        float arcSign = Random.value < 0.5f ? -1f : 1f;

        float ft = 0f;
        while (ft < flyDuration)
        {
            ft += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(ft / flyDuration);
            float eased = Mathf.Clamp01(flyEaseCurve.Evaluate(p));
            Vector2 linear = Vector2.Lerp(flyStart, flyEnd, eased);
            float arc = arcHeight * arcSign * Mathf.Sin(p * Mathf.PI); // 0 at both ends, peak at the midpoint
            rt.anchoredPosition = linear + perp * arc;
            yield return null;
        }
        rt.anchoredPosition = flyEnd;

        // Landed: tick the displayed total, bounce the counter, release to the pool.
        pendingInFlight -= share;
        displayedTotal += share;
        RefreshText();
        if (counterBounce != null) counterBounce.Bounce();

        coin.gameObject.SetActive(false);
        pool.Enqueue(coin);
    }
}
