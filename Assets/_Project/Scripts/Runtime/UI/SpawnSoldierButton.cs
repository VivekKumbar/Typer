using UnityEngine;
using TMPro;

// Hook a UI Button's OnClick to Spawn(). Spends coins to drop a soldier at the
// fortress. Flat cost so you can keep buying an army.
public class SpawnSoldierButton : MonoBehaviour
{
    [Header("Config")]
    public Soldier soldierPrefab;
    public Transform spawnPoint; // usually the Fortress
    public int cost = 15;

    [Header("Optional label")]
    public TMP_Text labelText;

    void Start() { RefreshLabel(); }

    public void Spawn()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.IsGameOver) return;
        if (soldierPrefab == null || spawnPoint == null) return;
        if (!gm.SpendCoins(cost)) return; // not enough coins -> nothing happens

        Instantiate(soldierPrefab, spawnPoint.position, Quaternion.identity);
    }

    void RefreshLabel()
    {
        if (labelText != null) labelText.text = "Soldier (" + cost + ")";
    }
}