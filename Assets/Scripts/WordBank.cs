using UnityEngine;

// A ScriptableObject so you can edit word lists in the Inspector without code.
// Right-click in Project window -> Create -> TypeKeep -> Word Bank.
[CreateAssetMenu(fileName = "WordBank", menuName = "TypeKeep/Word Bank")]
public class WordBank : ScriptableObject
{
    [TextArea] public string note = "Keep words SHORT for mobile. Grouped by difficulty tier.";

    public string[] easyWords   = { "cat","dog","run","sun","ice","key","map","owl","fox","bee" };
    public string[] mediumWords = { "shield","castle","arrow","sword","guard","tower","stone","raven" };
    public string[] hardWords   = { "fortress","catapult","defender","keystone","barricade" };

    public string GetWord(int difficulty)
    {
        string[] pool = difficulty < 3 ? easyWords : difficulty < 6 ? mediumWords : hardWords;
        return pool[Random.Range(0, pool.Length)];
    }
}
