using System.Collections.Generic;
using UnityEngine;

// Put this on an empty "Popup Manager" GameObject in the scene. Spawns/pools
// FloatingText popups. Other scripts call PopupManager.Show(...) or one of the
// convenience one-liners below — you tune look/feel here in the Inspector.
public class PopupManager : MonoBehaviour
{
    public static PopupManager Instance { get; private set; }

    [Header("Master control")]
    [Tooltip("Uncheck to disable all floating text popups.")]
    public bool enablePopups = true;

    [Header("Prefab")]
    [Tooltip("The FloatingText prefab to spawn/pool.")]
    public FloatingText floatingTextPrefab;
    [Tooltip("How many popups to pre-instantiate at Start, so the first ones in a wave don't hitch on Instantiate.")]
    public int prewarmCount = 10;

    [Header("Spawn placement")]
    [Tooltip("Offset above the world position each popup spawns at.")]
    public Vector3 spawnOffset = new Vector3(0f, 1.5f, 0f);
    [Tooltip("Random horizontal spread (world units) so stacked popups (e.g. Overload clears) don't overlap.")]
    [Range(0f, 1f)] public float horizontalJitter = 0.3f;

    [Header("Defaults")]
    [Tooltip("Font size used when a caller doesn't specify one.")]
    public float defaultFontSize = 36f;
    [Tooltip("Color for coin popups (+N).")]
    public Color coinColor = new Color(1f, 0.85f, 0.2f);
    [Tooltip("Color for combo milestone popups.")]
    public Color comboColor = new Color(1f, 0.5f, 0.1f);
    [Tooltip("Color for the PERFECT! popup.")]
    public Color perfectColor = new Color(0.3f, 0.95f, 1f);
    [Tooltip("Font size multiplier for combo popups, relative to Default Font Size.")]
    public float comboSizeMultiplier = 1.4f;
    [Tooltip("Font size multiplier for PERFECT! popups, relative to Default Font Size.")]
    public float perfectSizeMultiplier = 1.6f;

    private readonly Queue<FloatingText> pool = new Queue<FloatingText>();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (floatingTextPrefab == null) return;
        for (int i = 0; i < prewarmCount; i++)
            pool.Enqueue(CreateInstance());
    }

    FloatingText CreateInstance()
    {
        FloatingText ft = Instantiate(floatingTextPrefab, transform);
        ft.gameObject.SetActive(false);
        return ft;
    }

    public static void Show(Vector3 worldPos, string text, Color color)
    {
        if (Instance) Instance.Spawn(worldPos, text, color, Instance.defaultFontSize);
    }

    public static void Show(Vector3 worldPos, string text, Color color, float size)
    {
        if (Instance) Instance.Spawn(worldPos, text, color, size);
    }

    public static void ShowCoins(Vector3 worldPos, int amount)
    {
        if (!Instance) return;
        Instance.Spawn(worldPos, "+" + amount, Instance.coinColor, Instance.defaultFontSize);
    }

    public static void ShowCombo(Vector3 worldPos, int comboCount)
    {
        if (!Instance) return;
        Instance.Spawn(worldPos, "COMBO x" + comboCount + "!", Instance.comboColor, Instance.defaultFontSize * Instance.comboSizeMultiplier);
    }

    public static void ShowPerfect(Vector3 worldPos)
    {
        if (!Instance) return;
        Instance.Spawn(worldPos, "PERFECT!", Instance.perfectColor, Instance.defaultFontSize * Instance.perfectSizeMultiplier);
    }

    void Spawn(Vector3 worldPos, string text, Color color, float size)
    {
        if (!enablePopups || floatingTextPrefab == null) return;

        Vector2 jitter = Random.insideUnitCircle * horizontalJitter;
        Vector3 pos = worldPos + spawnOffset + new Vector3(jitter.x, 0f, jitter.y);

        FloatingText ft = pool.Count > 0 ? pool.Dequeue() : CreateInstance();
        ft.transform.position = pos;
        ft.gameObject.SetActive(true);
        ft.Init(text, color, size, Release);
    }

    void Release(FloatingText ft)
    {
        ft.gameObject.SetActive(false);
        pool.Enqueue(ft);
    }
}
