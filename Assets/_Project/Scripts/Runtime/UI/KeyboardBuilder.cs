using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Builds a RESPONSIVE 3-row QWERTY keyboard that adapts to any screen size
// (small phones to tablets). It measures the panel at runtime and sizes the
// keys so the 10-key top row always fits the width, clamped so they never
// overflow the panel height. Self-configuring: just put this on the panel.
public class KeyboardBuilder : MonoBehaviour
{
    [Header("Refs")]
    public Button keyButtonPrefab;  // UI Button with a TMP_Text child
    public Transform keyboardRoot;  // the KeyboardPanel (stretched full-width)

    [Header("Layout")]
    public float spacing = 8f;            // gap between keys
    public float horizontalPadding = 16f; // inset from the panel's left/right
    public float verticalPadding = 12f;   // inset from the panel's top/bottom
    public float maxKeySize = 130f;       // cap so keys aren't huge on big tablets

    private static readonly string[] Rows =
    {
        "QWERTYUIOP", // 10 = the widest row, drives the key size
        "ASDFGHJKL",  // 9
        "ZXCVBNM"     // 7
    };

    private const int MaxKeysInRow = 10;
    private const int RowCount = 3;

    IEnumerator Start()
    {
        // Wait one frame so the Canvas has laid out the panel and its size is real
        yield return null;

        float keySize = ComputeKeySize();
        SetupRootLayout();
        BuildRows(keySize);
    }

    // Fit keys to BOTH the available width and height, keep them square
    float ComputeKeySize()
    {
        RectTransform rt = (RectTransform)keyboardRoot;
        float w = rt.rect.width;
        float h = rt.rect.height;

        float byWidth = (w - horizontalPadding * 2f - spacing * (MaxKeysInRow - 1)) / MaxKeysInRow;
        float byHeight = (h - verticalPadding * 2f - spacing * (RowCount - 1)) / RowCount;

        float size = Mathf.Min(byWidth, byHeight, maxKeySize);
        return Mathf.Floor(Mathf.Max(size, 30f)); // never smaller than 30
    }

    void SetupRootLayout()
    {
        // Remove the old grid IMMEDIATELY (deferred Destroy would still block the add this frame)
        var grid = keyboardRoot.GetComponent<GridLayoutGroup>();
        if (grid != null) DestroyImmediate(grid);

        var v = keyboardRoot.GetComponent<VerticalLayoutGroup>();
        if (v == null) v = keyboardRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        if (v == null) return; // safety: bail if a layout group still blocks it
        v.spacing = spacing;
        v.padding = new RectOffset(0, 0, (int)verticalPadding, (int)verticalPadding);
        v.childAlignment = TextAnchor.MiddleCenter;
        v.childControlWidth = false;
        v.childControlHeight = false;
        v.childForceExpandWidth = false;
        v.childForceExpandHeight = false;
    }

    void BuildRows(float keySize)
    {
        foreach (string row in Rows)
        {
            GameObject rowGO = new GameObject("Row_" + row[0], typeof(RectTransform));
            rowGO.transform.SetParent(keyboardRoot, false);

            var h = rowGO.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;

            var fitter = rowGO.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            foreach (char c in row)
            {
                Button btn = Instantiate(keyButtonPrefab, rowGO.transform);

                var le = btn.GetComponent<LayoutElement>();
                if (le == null) le = btn.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = keySize;
                le.preferredHeight = keySize;
                le.minWidth = keySize;
                le.minHeight = keySize;

                var txt = btn.GetComponentInChildren<TMP_Text>();
                if (txt != null) txt.text = c.ToString();

                char captured = c;
                btn.onClick.AddListener(() => TypingController.Instance.ReceiveChar(captured));
            }
        }
    }
}