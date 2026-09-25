using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Collectible in-world shield powerup.
/// Spawns on the playfield, floats and bobs gently, and can be collected
/// via contact with Fortress/Player, click/tap, or proximity.
/// When collected, activates the shield effect and blocks all incoming damage for its duration.
/// </summary>
public class ShieldPowerup : MonoBehaviour, IPointerClickHandler
{
    [Header("Powerup Settings")]
    [Tooltip("How long the shield powerup lasts once collected.")]
    [SerializeField] private float duration = 10f;
    [Tooltip("Seconds before an uncollected powerup despawns.")]
    [SerializeField] private float lifeTime = 25f;

    [Header("Visual Animation")]
    [SerializeField] private float bobSpeed = 3f;
    [SerializeField] private float bobHeight = 0.25f;
    [SerializeField] private float rotateSpeed = 75f;
    [SerializeField] private Transform visualTransform;

    [Header("Collection")]
    [Tooltip("If Fortress gets within this distance, auto-attract/collect.")]
    [SerializeField] private float attractDistance = 2.0f;
    [SerializeField] private float collectMoveSpeed = 8f;

    private Vector3 basePosition;
    private float age;
    private bool isCollected;
    private Transform fortressTransform;

    void Awake()
    {
        if (visualTransform == null)
            visualTransform = transform.Find("Visual") ?? transform;

        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    void Start()
    {
        basePosition = transform.position;

        var fortress = GameObject.Find("Fortress");
        if (fortress != null)
            fortressTransform = fortress.transform;

        // Subtle pop-in scale animation
        StartCoroutine(PopIn());
    }

    IEnumerator PopIn()
    {
        Vector3 targetScale = transform.localScale;
        transform.localScale = Vector3.zero;
        float elapsed = 0f;
        float popDuration = 0.25f;
        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / popDuration);
            transform.localScale = Vector3.LerpUnclamped(Vector3.zero, targetScale, Mathf.Sin(t * Mathf.PI * 0.5f));
            yield return null;
        }
        transform.localScale = targetScale;
    }

    void Update()
    {
        if (isCollected) return;

        age += Time.deltaTime;
        if (age >= lifeTime)
        {
            StartCoroutine(Despawn());
            return;
        }

        // Float & bob
        float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(basePosition.x, basePosition.y + bobOffset, basePosition.z);

        // Rotation
        if (visualTransform != null)
        {
            visualTransform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        }

        // Check proximity to Fortress/Tower
        if (fortressTransform != null)
        {
            float dist = Vector3.Distance(transform.position, fortressTransform.position);
            if (dist <= attractDistance)
            {
                Collect();
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Collect();
    }

    void OnMouseDown()
    {
        Collect();
    }

    void OnTriggerEnter(Collider other)
    {
        if (isCollected) return;

        // Triggered by Fortress, Tower, or Player
        if (other.CompareTag("Player") || other.name.Contains("Fortress") || other.GetComponent<Tower>() != null || other.GetComponent<Soldier>() != null)
        {
            Collect();
        }
    }

    public void Collect()
    {
        if (isCollected) return;
        isCollected = true;

        // Activate the shield via ShieldManager
        if (ShieldManager.Instance != null)
        {
            ShieldManager.Instance.ActivateShield(duration);
        }

        // Sound effect
        SfxPlayer.PlayGameStart();

        // Toast feedback
        UIToast.ShowAt(transform, "Shield Powerup Active!", Color.cyan);

        // Visual collection pop
        StartCoroutine(CollectRoutine());
    }

    IEnumerator CollectRoutine()
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;
        float animDuration = 0.2f;

        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animDuration);
            transform.localScale = Vector3.Lerp(startScale, startScale * 1.4f, t);
            yield return null;
        }

        Destroy(gameObject);
    }

    IEnumerator Despawn()
    {
        isCollected = true;
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;
        float animDuration = 0.3f;

        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animDuration);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        Destroy(gameObject);
    }
}
