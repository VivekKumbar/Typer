using System.Collections;
using UnityEngine;
using TMPro;

// Shows a big centered message (e.g. "WAVE 2") for a moment.
// Put this on an ALWAYS-ACTIVE object (like the Canvas) and assign a TMP text
// child that it will show/hide.
public class WaveBanner : MonoBehaviour
{
    public TMP_Text text;
    public float showTime = 1.6f;

    private Coroutine co;

    void Awake() { if (text) text.gameObject.SetActive(false); }

    public void Show(string message)
    {
        if (text == null) return;
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(ShowRoutine(message));
    }

    IEnumerator ShowRoutine(string message)
    {
        text.text = message;
        text.gameObject.SetActive(true);
        yield return new WaitForSeconds(showTime);
        text.gameObject.SetActive(false);
        co = null;
    }
}