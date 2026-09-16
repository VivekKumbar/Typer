using System.Collections;
using UnityEngine;

// Pops out of a dead enemy ON THE GROUND (XZ), then auto-collects.
public class Coin : MonoBehaviour
{
    public int value = 1;
    public float spreadRadius = 0.6f;
    public float lifeTime = 0.6f;

    public void Launch() { StartCoroutine(CollectRoutine()); }

    IEnumerator CollectRoutine()
    {
        Vector3 start = transform.position;
        Vector2 r = Random.insideUnitCircle * spreadRadius;
        Vector3 end = start + new Vector3(r.x, 0f, r.y); // spread across the ground
        float t = 0f;
        while (t < lifeTime)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(start, end, t / lifeTime);
            yield return null;
        }
        GameManager.Instance.AddCoins(value);
        Destroy(gameObject);
    }
}
