using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Persistent wave indicator displayed in the corner of the HUD.
/// Automatically listens to WaveManager wave progression events and
/// updates with a punch animation.
/// </summary>
public class WaveIndicatorUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The TextMeshPro text displaying 'Wave X'.")]
    [SerializeField] private TMP_Text waveText;

    private Coroutine punchRoutine;
    private Vector3 baseScale = Vector3.one;

    void Awake()
    {
        if (waveText == null)
            waveText = GetComponentInChildren<TMP_Text>();

        baseScale = transform.localScale;
    }

    void OnEnable()
    {
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnWaveStarted += OnWaveStarted;
            SetWave(WaveManager.Instance.currentWave);
        }
    }

    void Start()
    {
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnWaveStarted -= OnWaveStarted;
            WaveManager.Instance.OnWaveStarted += OnWaveStarted;
            SetWave(WaveManager.Instance.currentWave);
        }
    }

    void OnDisable()
    {
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.OnWaveStarted -= OnWaveStarted;
        }
    }

    void OnWaveStarted(int waveNumber)
    {
        SetWave(waveNumber);
        TriggerPunch();
    }

    public void SetWave(int waveNumber)
    {
        if (waveText != null)
        {
            waveText.text = $"Wave {Mathf.Max(1, waveNumber)}";
        }
    }

    public void TriggerPunch()
    {
        if (punchRoutine != null) StopCoroutine(punchRoutine);
        punchRoutine = StartCoroutine(PunchRoutine());
    }

    IEnumerator PunchRoutine()
    {
        float duration = 0.22f;
        float elapsed = 0f;
        Vector3 targetScale = baseScale * 1.2f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            // Quick pop out, then smooth settle back
            float s = t < 0.4f ? Mathf.Lerp(1f, 1.2f, t / 0.4f) : Mathf.Lerp(1.2f, 1f, (t - 0.4f) / 0.6f);
            transform.localScale = baseScale * s;
            yield return null;
        }

        transform.localScale = baseScale;
        punchRoutine = null;
    }
}
