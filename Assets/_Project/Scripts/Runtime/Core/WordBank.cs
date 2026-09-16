using System.Collections.Generic;
using UnityEngine;

// Loads words from an external file and auto-sorts them BY LENGTH at runtime.
// Edit your words in Excel, save as CSV, drag the file into "Word File".
// Words can be separated by new lines, commas, semicolons, tabs, or spaces.
//
// Word Packs: if the player has one or more Word Pack ShopItems SELECTED
// (RunContext's LOCKED snapshot of WordPackSelection — see RunContext.cs)
// and OWNED (ShopInventory), their word files are combined into the active
// pool instead of Word File/Fallback Words. If packs are OWNED but none are
// selected, one random owned pack is used automatically (nudges players
// toward the packs they bought without forcing a shop visit). Only when no
// packs are owned at all does this fall back to the original Word
// File/Fallback Words behavior — completely unchanged for anyone not using
// Word Packs.
//
// The pool is built lazily and cached (see Build()) — it does NOT re-read
// selection on every GetWord() call. WaveManager.Start() calls
// RebuildForNewRun() at the start of every run so the cache picks up
// whatever RunContext just locked in, instead of carrying over stale words
// from a previous run in the same app session.
//
// Variation: controls how much GetWord() avoids repeating recently-used
// words. 0 = pure random (can repeat immediately, same as the original
// behavior). 1 = maximum variety (cycles through nearly the whole active
// pool before any word repeats). Independent of Word Packs — applies to
// whatever pool is currently active, packs or default list alike.
[CreateAssetMenu(fileName = "WordBank", menuName = "TypeKeep/Word Bank")]
public class WordBank : ScriptableObject
{
    [Header("Word source (default / fallback)")]
    [Tooltip("Drag a .txt or .csv file here. (Export from Excel as CSV.)")]
    public TextAsset wordFile;

    [Tooltip("Used only if no Word File is assigned.")]
    [TextArea(3, 6)]
    public string fallbackWords =
        "cat dog run sun ice key map owl fox bee " +
        "wall gate bow axe pike moat " +
        "shield castle arrow sword guard tower stone raven knight " +
        "fortress catapult defender keystone barricade rampart";

    [Header("Word Packs")]
    [Tooltip("The shop catalog to resolve Word Pack ShopItems from. Leave empty to disable Word Packs entirely (falls back to Word File/Fallback Words like before).")]
    public ShopCatalog catalog;
    [Tooltip("Max number of Word Pack ShopItems the player can have ACTIVE at once for a run. Read by the shop's Word Packs UI and by WordPackSelection.")]
    public int maxActivePacks = 3;

    [Header("Variation")]
    [Tooltip("0 = words can repeat freely (most random, most repeats). " +
             "1 = maximum variety (cycles through as many different words as " +
             "possible before any word repeats). Tune live to taste.")]
    [Range(0f, 1f)] public float variation = 0.6f;

    [Header("Debug")]
    [Tooltip("Logs which pack ids were selected, words loaded per pack, and the final bucketed pool sizes whenever the pool is (re)built. Toggle off when you don't need it.")]
    public bool logPackSelection = false;

    private Dictionary<int, List<string>> byLength;
    private List<string> all;

    // Tracks recently-used words so GetWord() can avoid repeating them when
    // variation > 0. Cleared whenever the pool rebuilds (new run / new packs).
    private readonly Queue<string> recentHistory = new Queue<string>();

    void OnEnable() { byLength = null; all = null; recentHistory.Clear(); } // force a rebuild each play session

    // Invalidates the cached pool so the next GetWord() rebuilds it from
    // whatever RunContext has locked in right now. Call at the start of
    // every run (WaveManager.Start) — without this, the pool built during a
    // previous run in the same app session would silently keep being reused.
    public void RebuildForNewRun() { byLength = null; all = null; recentHistory.Clear(); }

    void Build()
    {
        all = new List<string>();
        byLength = new Dictionary<int, List<string>>();
        recentHistory.Clear();

        List<(string id, TextAsset file)> activeFiles = ResolveActiveFiles();
        if (activeFiles.Count > 0)
        {
            foreach (var (id, file) in activeFiles)
            {
                int before = all.Count;
                ParseInto(file.text);
                if (logPackSelection)
                    Debug.Log($"[WordBank] Pack '{id}' loaded {all.Count - before} words from '{file.name}'.");
            }
        }
        else
        {
            string raw = (wordFile != null && !string.IsNullOrWhiteSpace(wordFile.text))
                         ? wordFile.text : fallbackWords;
            ParseInto(raw);
            if (logPackSelection)
                Debug.Log($"[WordBank] No word packs active — using default word list ({all.Count} words).");
        }

        if (logPackSelection)
        {
            var bucketSizes = new List<string>();
            foreach (var kv in byLength) bucketSizes.Add($"{kv.Key}:{kv.Value.Count}");
            bucketSizes.Sort();
            Debug.Log($"[WordBank] Selected pack ids: [{string.Join(", ", RunContext.LockedWordPackIds)}]. " +
                      $"Total pool: {all.Count} words. Buckets by length: {{{string.Join(", ", bucketSizes)}}}.");
        }
    }

    List<(string id, TextAsset file)> ResolveActiveFiles()
    {
        var files = new List<(string id, TextAsset file)>();
        if (catalog == null) return files;

        List<string> selectedIds = RunContext.LockedWordPackIds;
        var selectedOwned = new List<ShopItem>();
        var allOwnedPacks = new List<ShopItem>();

        foreach (ShopCategory cat in catalog.categories)
        {
            if (cat == null || cat.items == null) continue;
            foreach (ShopItem item in cat.items)
            {
                if (item == null || item.kind != ShopItemKind.WordPack || !ShopInventory.IsOwned(item)) continue;
                allOwnedPacks.Add(item);
                if (selectedIds.Contains(item.id)) selectedOwned.Add(item);
            }
        }

        if (selectedOwned.Count > 0)
        {
            foreach (ShopItem item in selectedOwned)
            {
                TextAsset file = item.payload as TextAsset;
                if (file != null) files.Add((item.id, file));
            }
        }
        else if (allOwnedPacks.Count > 0)
        {
            // Owned but nothing manually selected -> favor a random owned pack
            // instead of silently falling back to the plain default list.
            ShopItem pick = allOwnedPacks[Random.Range(0, allOwnedPacks.Count)];
            TextAsset file = pick.payload as TextAsset;
            if (file != null) files.Add((pick.id, file));
        }

        return files;
    }

    void ParseInto(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return;

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

    // Returns a random word whose length is between minLen and maxLen (inclusive),
    // respecting the Variation slider to reduce/avoid near-term repeats.
    public string GetWord(int minLen, int maxLen)
    {
        if (all == null) Build();

        List<string> pool = new List<string>();
        for (int len = minLen; len <= maxLen; len++)
            if (byLength.TryGetValue(len, out List<string> list))
                pool.AddRange(list);

        if (pool.Count == 0) pool = all;            // no words in range -> any word
        if (pool == null || pool.Count == 0) return "WORD"; // total fallback

        string chosen = PickWithVariation(pool);
        RememberUsed(chosen);
        return chosen;
    }

    string PickWithVariation(List<string> pool)
    {
        if (variation <= 0f || pool.Count <= 1)
            return pool[Random.Range(0, pool.Count)]; // 0 = pure random, can repeat freely

        // How many recent words to actively avoid repeating, scaled by variation.
        // At variation = 1, avoid up to (pool.Count - 1) recent words -- i.e.
        // cycle through nearly the whole pool before anything repeats.
        int avoidCount = Mathf.RoundToInt(Mathf.Lerp(0, pool.Count - 1, variation));

        var excluded = new HashSet<string>();
        int taken = 0;
        foreach (string w in recentHistory)
        {
            if (taken >= avoidCount) break;
            excluded.Add(w);
            taken++;
        }

        List<string> candidates = new List<string>();
        foreach (string w in pool)
            if (!excluded.Contains(w)) candidates.Add(w);

        // If everything got excluded (small pool + high variation), fall back
        // to the full pool rather than returning nothing.
        if (candidates.Count == 0) candidates = pool;

        return candidates[Random.Range(0, candidates.Count)];
    }

    void RememberUsed(string word)
    {
        recentHistory.Enqueue(word);
        // Cap history size generously; the actual "how far back we look" is
        // controlled by avoidCount in PickWithVariation, not this cap.
        while (recentHistory.Count > 64) recentHistory.Dequeue();
    }
}