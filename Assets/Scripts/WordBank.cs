using UnityEngine;

// Word length tiers. Each enemy type picks one so fast enemies get short words
// and tanky enemies get long ones.
public enum WordTier { Short, Medium, Long }

[CreateAssetMenu(fileName = "WordBank", menuName = "TypeKeep/Word Bank")]
public class WordBank : ScriptableObject
{
    [TextArea] public string note = "Keep words SHORT for mobile. Grouped by tier.";

    public string[] easyWords = { "cat", "dog", "run", "sun", "ice", "key", "map", "owl", "fox", "bee" };
    public string[] mediumWords = { "shield", "castle", "arrow", "sword", "guard", "tower", "stone", "raven" };
    public string[] hardWords = { "fortress", "catapult", "defender", "keystone", "barricade" };

    public string GetWord(WordTier tier)
    {
        string[] pool = tier == WordTier.Short ? easyWords
                      : tier == WordTier.Medium ? mediumWords
                      : hardWords;
        if (pool == null || pool.Length == 0) pool = easyWords;
        return pool[Random.Range(0, pool.Length)];
    }
}