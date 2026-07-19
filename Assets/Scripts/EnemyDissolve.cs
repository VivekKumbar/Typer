using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Dissolves this enemy's materials on death.
// If it doesn't work, right-click the component header -> "Log Shader Properties"
// to print every float property name your shader actually exposes.
public class EnemyDissolve : MonoBehaviour
{
    [Header("Shader")]
    [Tooltip("The float property REFERENCE name (not the display name).")]
    public string propertyName = "_DissolveAmount";

    [Header("Timing")]
    public float duration = 1.5f;
    public float delay = 0.3f;

    [Header("Values")]
    public float startValue = 0f;
    public float endValue = 1f;

    [Header("Debug")]
    public bool verboseLogging = true;

    [Header("Optional")]
    public Renderer[] renderers;

    private Material[] instances;
    private bool started;

    void Awake() { Collect(); }

    void Collect()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);

        var list = new List<Material>();
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            list.AddRange(r.materials); // instances, so only THIS enemy dissolves
        }
        instances = list.ToArray();

        if (verboseLogging && instances.Length == 0)
            Debug.LogWarning("[EnemyDissolve] No renderers found on " + name, this);
    }

    public float TotalTime => delay + duration;

    public void Dissolve()
    {

        if (started) return;
        started = true;

        if (instances == null || instances.Length == 0) { Collect(); }

        // Verify the property exists before we bother animating
        int id = Shader.PropertyToID(propertyName);
        bool anyHas = false;
        foreach (Material m in instances)
            if (m != null && m.HasProperty(id)) { anyHas = true; break; }

        if (!anyHas)
        {
            Debug.LogError("[EnemyDissolve] No material has a property called '" + propertyName +
                           "'. Right-click this component -> 'Log Shader Properties' to see the real names.", this);
            return;
        }

        StartCoroutine(DissolveRoutine(id));
    }

    IEnumerator DissolveRoutine(int id)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float v = Mathf.Lerp(startValue, endValue, Mathf.Clamp01(t / duration));
            foreach (Material m in instances)
                if (m != null && m.HasProperty(id)) m.SetFloat(id, v);
            yield return null;
        }

        foreach (Material m in instances)
            if (m != null && m.HasProperty(id)) m.SetFloat(id, endValue);
    }

    // Right-click the component header in the Inspector to run this.
    [ContextMenu("Log Shader Properties")]
    void LogShaderProperties()
    {
        Renderer[] rs = GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) { Debug.LogWarning("No renderers found."); return; }

        foreach (Renderer r in rs)
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || m.shader == null) continue;
                string msg = "MATERIAL '" + m.name + "'  (shader: " + m.shader.name + ")\n";
#if UNITY_EDITOR
                int count = UnityEditor.ShaderUtil.GetPropertyCount(m.shader);
                for (int i = 0; i < count; i++)
                {
                    var type = UnityEditor.ShaderUtil.GetPropertyType(m.shader, i);
                    if (type == UnityEditor.ShaderUtil.ShaderPropertyType.Float ||
                        type == UnityEditor.ShaderUtil.ShaderPropertyType.Range)
                    {
                        msg += "   FLOAT -> " + UnityEditor.ShaderUtil.GetPropertyName(m.shader, i) + "\n";
                    }
                }
#endif
                Debug.Log(msg, r);
            }
        }
    }
}