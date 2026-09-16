using System.Collections.Generic;
using UnityEngine;

// The whole shop configuration. Create ONE of these and assign it to the ShopUI.
// Add categories here; add items inside each category. This is your "config file".
[CreateAssetMenu(fileName = "ShopCatalog", menuName = "TypeKeep/Shop/Catalog")]
public class ShopCatalog : ScriptableObject
{
    public List<ShopCategory> categories = new List<ShopCategory>();
}
