using System.Collections;
using UnityEngine;

public class EnemyHitFlash : MonoBehaviour
{
    [Header("Setup")]
    public string colorProperty = "Enemy_Material_Colour";
    public Color redColor = Color.red;
    public float flashTime = 0.15f;

    private Renderer[] renderers;
    private Color[] originalColors;
    private int propId;
    private Coroutine co;

    void Awake()
    {
        propId = Shader.PropertyToID(colorProperty);
        renderers = GetComponentsInChildren<Renderer>(true);

        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].material.GetColor(propId);
    }

    public void Flash()
    {
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material.SetColor(propId, redColor);

        yield return new WaitForSeconds(flashTime);

        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material.SetColor(propId, originalColors[i]);

        co = null;
    }
}