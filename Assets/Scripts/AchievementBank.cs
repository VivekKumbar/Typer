using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Loads achievement definitions from an external CSV file and parses them
// once, lazily, into a flat list -- mirrors WordBank.cs's own pattern for
// word lists. Edit in Excel, export as CSV, drag the file into
// "Achievement File". Adding/rebalancing an achievement is then just a new
// row -- no code changes needed.
//
// CSV columns (a header row is optional and auto-skipped if its first cell
// is literally "id"): id, displayName, description, metricType, targetValue,
// coinReward, iconName
//
// Parsing is tolerant like WordBank's: blank lines are skipped, each cell is
// trimmed, and a row that doesn't parse cleanly (missing columns, an
// unrecognized metricType, a non-numeric target/reward) is skipped with a
// warning rather than throwing -- one bad row in Excel shouldn't break every
// other achievement. Fields may be wrapped in "quotes" (with "" for a
// literal quote inside) if they need to contain a comma, same convention
// Excel itself uses when exporting CSV.
[CreateAssetMenu(fileName = "AchievementBank", menuName = "TypeKeep/Achievement Bank")]
public class AchievementBank : ScriptableObject
{
    [Header("Achievement source")]
    [Tooltip("Drag a .csv or .txt file here. (Export from Excel as CSV.)")]
    public TextAsset achievementFile;

    [Header("Debug")]
    [Tooltip("Logs how many rows loaded and any skipped rows whenever the list is (re)built.")]
    public bool logParsing = false;

    private List<AchievementData> all;

    void OnEnable() { all = null; } // force a rebuild each play session

    public List<AchievementData> GetAll()
    {
        if (all == null) Build();
        return all;
    }

    public AchievementData GetById(string id)
    {
        if (all == null) Build();
        if (string.IsNullOrEmpty(id)) return null;
        return all.Find(a => a.id == id);
    }

    void Build()
    {
        all = new List<AchievementData>();

        if (achievementFile == null || string.IsNullOrWhiteSpace(achievementFile.text))
        {
            if (logParsing) Debug.Log("[AchievementBank] No Achievement File assigned (or it's empty) -- 0 achievements loaded.");
            return;
        }

        int skipped = 0;
        string[] lines = achievementFile.text.Split('\n');
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim().TrimEnd('\r');
            if (line.Length == 0) continue;

            List<string> cols = SplitCsvLine(line);
            if (cols.Count < 7) { skipped++; continue; } // malformed row -- not enough columns

            string id = cols[0].Trim();
            if (id.Length == 0) { skipped++; continue; }
            if (string.Equals(id, "id", StringComparison.OrdinalIgnoreCase)) continue; // header row, not an error

            if (!Enum.TryParse(cols[3].Trim(), true, out AchievementMetricType metric))
            {
                Debug.LogWarning($"[AchievementBank] Row '{id}' has an unrecognized metricType '{cols[3]}' -- skipped.");
                skipped++;
                continue;
            }
            if (!int.TryParse(cols[4].Trim(), out int target))
            {
                Debug.LogWarning($"[AchievementBank] Row '{id}' has a non-numeric targetValue '{cols[4]}' -- skipped.");
                skipped++;
                continue;
            }
            if (!int.TryParse(cols[5].Trim(), out int reward))
            {
                Debug.LogWarning($"[AchievementBank] Row '{id}' has a non-numeric coinReward '{cols[5]}' -- skipped.");
                skipped++;
                continue;
            }

            all.Add(new AchievementData
            {
                id = id,
                displayName = cols[1].Trim(),
                description = cols[2].Trim(),
                metricType = metric,
                targetValue = target,
                coinReward = reward,
                iconName = cols[6].Trim()
            });
        }

        if (logParsing) Debug.Log($"[AchievementBank] Loaded {all.Count} achievements ({skipped} row(s) skipped).");
    }

    // Splits one CSV line into cells, honoring "quoted, fields" (with ""
    // for a literal quote) the way Excel exports commas inside text --
    // WordBank doesn't need this (its tokenizer just splits flat word
    // lists), but achievement descriptions are prose and commas in prose
    // are common.
    static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                    else inQuotes = false;
                }
                else current.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { result.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }
}
