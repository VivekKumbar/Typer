using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Flashes the enemy's materials red when a bullet hits it.
// Put this on the enemy prefab (root). Works with any number of renderers.
public class EnemyHitFlash : MonoBehaviour
{
    [Header("Flash")]
    public Color flashColor = new Color(1f, 0.15f, 0.15f);
    [Tooltip("How long the red flash lasts, in seconds.")]
    public float flashTime = 0.12f;
    [Range(0f, 1f)] public float flashStrength = 0.85f;

    [Header("Shader")]
    [Tooltip("Color property of your material. URP Lit uses _BaseColor; older shaders use _Color.")]
    public string colorProperty = "Enemy_Material_Colour";

    private Material[] mats;
    private Color[] original;
    private int propId;
    private Coroutine co;

    void Awake()
    {
        propId = Shader.PropertyToID(colorProperty);

        var list = new List<Material>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            if (r != null) list.AddRange(r.materials); // instances, per-enemy
        mats = list.ToArray();

        original = new Color[mats.Length];
        for (int i = 0; i < mats.Length; i++)
            original[i] = (mats[i] != null && mats[i].HasProperty(propId))
                          ? mats[i].GetColor(propId) : Color.white;
    }

    public void Flash()
    {
        if (!isActiveAndEnabled || mats == null || mats.Length == 0) return;
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        // Snap to red
        for (int i = 0; i < mats.Length; i++)
            if (mats[i] != null && mats[i].HasProperty(propId))
                mats[i].SetColor(propId, Color.Lerp(original[i], flashColor, flashStrength));

        // Fade back to normal
        float t = 0f;
        while (t < flashTime)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / flashTime);
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && mats[i].HasProperty(propId))
                    mats[i].SetColor(propId, Color.Lerp(original[i], flashColor, flashStrength * k));
            yield return null;
        }

        for (int i = 0; i < mats.Length; i++)
            if (mats[i] != null && mats[i].HasProperty(propId))
                mats[i].SetColor(propId, original[i]);
        co = null;
    }

    [ContextMenu("Log Color Properties")]
    void LogColorProperties()
    {
#if UNITY_EDITOR
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || m.shader == null) continue;
                string msg = "MATERIAL '" + m.name + "' (" + m.shader.name + ")\n";
                int c = UnityEditor.ShaderUtil.GetPropertyCount(m.shader);
                for (int i = 0; i < c; i++)
                    if (UnityEditor.ShaderUtil.GetPropertyType(m.shader, i) == UnityEditor.ShaderUtil.ShaderPropertyType.Color)
                        msg += "   COLOR -> " + UnityEditor.ShaderUtil.GetPropertyName(m.shader, i) + "\n";
                Debug.Log(msg, r);
            }
#endif
    }
}