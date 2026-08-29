using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyHitFlash : MonoBehaviour
{
    [Header("Setup")]
    // It flashes whichever of these a material has.
    public string[] colorProperties = { "Enemy_Material_Colour", "_BaseColor", "_Color" };
    public Color redColor = Color.red;
    public float flashTime = 0.15f;

    private List<Material> mats = new List<Material>();
    private List<int> propIds = new List<int>();
    private List<Color> originals = new List<Color>();
    private Coroutine co;

    // Start, not Awake: must run AFTER EnemySkinApplier.Awake() so the "original"
    // colour cached here is the equipped skin's colour, not the prefab default
    // (Unity guarantees all Awakes finish before any Start runs).
    void Start()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material m in r.materials)
            {
                if (m == null) continue;
                foreach (string p in colorProperties)
                {
                    int id = Shader.PropertyToID(p);
                    if (m.HasProperty(id))
                    {
                        mats.Add(m);
                        propIds.Add(id);
                        originals.Add(m.GetColor(id));
                        break; // one property per material
                    }
                }
            }
        }
    }

    public void Flash()
    {
        if (mats.Count == 0) return;
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        for (int i = 0; i < mats.Count; i++)
            mats[i].SetColor(propIds[i], redColor);

        yield return new WaitForSeconds(flashTime);

        for (int i = 0; i < mats.Count; i++)
            mats[i].SetColor(propIds[i], originals[i]);

        co = null;
    }
}