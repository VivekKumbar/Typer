using System.Collections.Generic;
using UnityEngine;

// The full list of draftable upgrades. Create ONE of these and assign it to
// UpgradeManager. Add upgrades here; adding a new one later is just
// create-an-asset-and-drop-it-in, no code changes.
[CreateAssetMenu(fileName = "UpgradePool", menuName = "TypeKeep/Upgrades/Pool")]
public class UpgradePool : ScriptableObject
{
    public List<UpgradeDefinition> upgrades = new List<UpgradeDefinition>();
}
