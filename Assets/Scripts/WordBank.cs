using System.Collections.Generic;
using UnityEngine;

// Loads words from an external file and auto-sorts them BY LENGTH at runtime.
// Edit your words in Excel, save as CSV, drag the file into "Word File".
// Words can be separated by new lines, commas, semicolons, tabs, or spaces.
[CreateAssetMenu(fileName = "WordBank", menuName = "TypeKeep/Word Bank")]
public class WordBank : ScriptableObject
{
    [Header("Word source")]
    [Tooltip("Drag a .txt or .csv file here. (Export from Excel as CSV.)")]
    public TextAsset wordFile;

    [Tooltip("Used only if no Word File is assigned.")]
    [TextArea(3, 6)]
    public string fallbackWords =
        "cat dog run sun ice key map owl fox bee " +
        "wall gate bow axe pike moat " +
        "shield castle arrow sword guard tower stone raven knight " +
        "fortress catapult defender keystone barricade rampart";

    private Dictionary<int, List<string>> byLength;
    private List<string> all;

    void OnEnable() { byLength = null; all = null; } // force a rebuild each play session

    void Build()
    {
        all = new List<string>();
        byLength = new Dictionary<int, List<string>>();

        string raw = (wordFile != null && !string.IsNullOrWhiteSpace(wordFile.text))
                     ? wordFile.text : fallbackWords;

        char[] seps = { '\n', '\r', ',', ';', '\t', ' ' };
        foreach (string token in raw.Split(seps, System.StringSplitOptions.RemoveEmptyEntries))
        {
            string w = token.Trim().ToUpper();
            if (w.Length == 0) continue;

            bool lettersOnly = true;
            foreach (char c in w) if (!char.IsLetter(c)) { lettersOnly = false; break; }
            if (!lettersOnly) continue; // skip numbers / stray symbols

            all.Add(w);
            if (!byLength.TryGetValue(w.Length, out List<string> list))
            {
                list = new List<string>();
                byLength[w.Length] = list;
            }
            list.Add(w);
        }
    }

    // Returns a random word whose length is between minLen and maxLen (inclusive).
    public string GetWord(int minLen, int maxLen)
    {
        if (all == null) Build();

        List<string> pool = new List<string>();
        for (int len = minLen; len <= maxLen; len++)
            if (byLength.TryGetValue(len, out List<string> list))
                pool.AddRange(list);

        if (pool.Count == 0) pool = all;            // no words in range -> any word
        if (pool == null || pool.Count == 0) return "WORD"; // total fallback
        return pool[Random.Range(0, pool.Count)];
    }
}