using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Dissolves this enemy's materials on death.
// Sets the property directly on material instances. Works with Shader Graph
// "Per Material" scope (which HasProperty can wrongly report as missing), so
// it does NOT block on HasProperty — it just sets the value.
public class EnemyDissolve : MonoBehaviour
{
    [Header("Shader")]
    [Tooltip("The property REFERENCE name from Shader Graph (e.g. Enemy_Material_Dissolve).")]
    public string propertyName = "Enemy_Material_Dissolve";

    [Header("Timing")]
    public float duration = 1.5f;
    public float delay = 0.3f;

    [Header("Values")]
    public float startValue = 0f;
    public float endValue = 1f;

    private Material[] instances;
    private int propId;
    private bool started;

    void Awake()
    {
        propId = Shader.PropertyToID(propertyName);

        var list = new List<Material>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            if (r != null) list.AddRange(r.materials); // per-enemy instances
        instances = list.ToArray();

        // Make sure it starts fully visible
        SetAll(startValue);
    }

    public float TotalTime => delay + duration;

    public void Dissolve()
    {
        if (started || instances == null || instances.Length == 0) return;
        started = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            SetAll(Mathf.Lerp(startValue, endValue, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        SetAll(endValue);
    }

    void SetAll(float v)
    {
        if (instances == null) return;
        foreach (Material m in instances)
            if (m != null) m.SetFloat(propId, v); // no HasProperty gate
    }
}